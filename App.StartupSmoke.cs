using System.IO;
using System.Windows;
using System.Windows.Threading;
using KillerPDF.Services;

namespace KillerPDF;

public partial class App
{
    internal static bool IsStartupSmoke { get; } = Environment.GetCommandLineArgs()
        .Skip(1).Any(a => string.Equals(a, "--startup-smoke", StringComparison.OrdinalIgnoreCase));
    internal static string SmokeDataRoot { get; } = Path.Combine(
        Path.GetTempPath(), "KillerPDF-startup-smoke-" + Guid.NewGuid().ToString("N"));
    private static readonly Dictionary<string, string> SmokeSettings = new(StringComparer.Ordinal);

    // Separate from normal startup: no mutex, pipe server, registration, cleanup of user
    // temporary files, session restore, or interactive crash dialog.
    private void RunStartupSmoke(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        AppDataPaths.StartupSmokeRoot = SmokeDataRoot;
        string? report = Environment.GetEnvironmentVariable("KILLERPDF_SMOKE_REPORT");
        string phase = "arguments";
        int finished = 0;
        System.Threading.Timer? watchdog = null;

        void Record(string message)
        {
            if (string.IsNullOrWhiteSpace(report))
                throw new InvalidOperationException("KILLERPDF_SMOKE_REPORT is required.");
            File.AppendAllText(report, message + Environment.NewLine);
        }

        void Finish(Exception? error)
        {
            if (Interlocked.Exchange(ref finished, 1) != 0) return;
            watchdog?.Dispose();
            int exit = error is null ? 0 : 1;
            try
            {
                Record(error is null ? "PASS MainWindow startup" : $"FAIL {phase}: {error}");
            }
            catch (Exception reportError)
            {
                Console.Error.WriteLine(reportError);
                exit = 1;
            }
            // Shutdown closes only this process's empty window. Settings writes stay in memory.
            try { Shutdown(exit); }
            catch (Exception shutdownError)
            {
                Console.Error.WriteLine(shutdownError);
                Environment.Exit(1);
            }
            finally
            {
                try { if (Directory.Exists(SmokeDataRoot)) Directory.Delete(SmokeDataRoot, true); }
                catch { }
            }
        }

        DispatcherUnhandledException += (_, failure) =>
        {
            failure.Handled = true;
            Finish(failure.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, failure) =>
        {
            try { Record($"FAIL {phase}: {failure.ExceptionObject}"); } catch { }
        };
        TaskScheduler.UnobservedTaskException += (_, failure) =>
        {
            failure.SetObserved();
            Dispatcher.BeginInvoke(new Action(() => Finish(failure.Exception)));
        };

        try
        {
            if (e.Args.Length != 1)
                throw new ArgumentException("--startup-smoke cannot be combined with files or other commands.");
            Record($"START version={AppVersion.Display} pid={Environment.ProcessId}");
            Record($"Isolated data root={AppDataPaths.UserRoot}; temp root={TempDir}");
            watchdog = new System.Threading.Timer(_ =>
            {
                if (Interlocked.Exchange(ref finished, 1) != 0) return;
                try { Record($"FAIL timeout during {phase}"); } catch { }
                Environment.Exit(1);
            }, null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
            base.OnStartup(e);
            phase = "theme and locale";
            ThemeManager.Initialize();
            FilmGrain.Initialize();
            LocaleManager.Initialize();
            phase = "MainWindow constructor";
            var window = new MainWindow
            {
                ShowActivated = false,
                ShowInTaskbar = false,
                Opacity = 0,
                IsHitTestVisible = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = SystemParameters.VirtualScreenLeft - 4000,
                Top = SystemParameters.VirtualScreenTop - 4000
            };
            MainWindow = window;
            Record("MainWindow constructed");
            bool loaded = false;
            window.Loaded += (_, _) => { loaded = true; Record("MainWindow Loaded"); };
            window.ContentRendered += (_, _) =>
            {
                phase = "dispatcher idle after ContentRendered";
                Record("MainWindow ContentRendered");
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    if (!loaded) { Finish(new InvalidOperationException("Loaded was not observed.")); return; }
                    phase = "theme picker first open";
                    // Opening the picker synchronizes its radio buttons and loads the accent swatches.
                    // Exercise this on the actual payload, whose assembly name differs from an IDE build.
                    var button = (System.Windows.Controls.Button)window.FindName("RailThemeBtn");
                    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    var picker = (System.Windows.Controls.ContextMenu)window.FindName("ThemeFlyout");
                    if (!picker.IsOpen) { Finish(new InvalidOperationException("Theme picker did not open.")); return; }
                    picker.IsOpen = false;
                    Record("Theme picker opened");
                    Finish(null);
                }));
            };
            phase = "MainWindow Show and Loaded";
            window.Show();
        }
        catch (Exception error) { Finish(error); }
    }
}

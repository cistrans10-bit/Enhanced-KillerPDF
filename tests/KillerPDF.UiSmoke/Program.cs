using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using WpfApplication = System.Windows.Application;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;

internal static class Program
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")]
    private static extern bool CloseDesktop(IntPtr desktop);

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 4 && args[0] == "--host")
                return Host(Path.GetFullPath(args[1]), args[2], Path.GetFullPath(args[3]), false);
            if (args.Length == 3 && args[0] == "--verify-fixture")
                return Host(Path.GetFullPath(args[1]), "Dark", Path.GetFullPath(args[2]), true);
            if (args.Length != 3 || args[0] != "--interactive")
                throw new ArgumentException("Usage: KillerPDF.UiSmoke --interactive APP_DLL OUTPUT_DIRECTORY (unlocked Windows desktop required), or --verify-fixture APP_DLL OUTPUT_DIRECTORY.");
            RequireDesktop();
            string assembly = Path.GetFullPath(args[1]);
            string output = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(output);
            using var buildLock = new Mutex(false, @"Local\KillerFamilyBuildVerification");
            if (!buildLock.WaitOne(0)) throw new InvalidOperationException("A family build is active. Run UI smoke after it completes.");
            try
            {
                foreach (string theme in new[] { "Dark", "Light", "SE98" })
                    RunTheme(assembly, theme, output);
            }
            finally { buildLock.ReleaseMutex(); }
            Console.WriteLine("PASS interactive stamp acceptance, cancellation, and working-document save/reopen in Dark, Light, and SE98.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error);
            return 1;
        }
    }

    private static void RequireDesktop()
    {
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive ||
            Process.GetCurrentProcess().SessionId == 0)
            throw new InvalidOperationException("An interactive Windows session is required; hosted CI does not run this suite.");
        IntPtr desktop = OpenInputDesktop(0, false, 0x0001);
        if (desktop == IntPtr.Zero)
            throw new InvalidOperationException("The input desktop is unavailable. Unlock the session before running smoke tests.");
        CloseDesktop(desktop);
        if (Process.GetProcessesByName("KillerPDF").Length != 0 ||
            Process.GetProcessesByName("KillerPDF.App").Length != 0)
            throw new InvalidOperationException("Close the existing PDF session before running smoke tests; no user app is terminated.");
    }

    private static void RunTheme(string assembly, string theme, string output)
    {
        string folder = Path.Combine(output, theme + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (string arg in new[] { "--host", assembly, theme, folder }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the isolated dialog host.");
        try
        {
            using var automation = new UIA3Automation
            {
                ConnectionTimeout = TimeSpan.FromSeconds(2),
                TransactionTimeout = TimeSpan.FromSeconds(3)
            };
            AutomationElement Find(string id)
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < TimeSpan.FromSeconds(15))
                {
                    if (process.HasExited) throw new InvalidOperationException("Dialog host exited: " + ReadFailure(folder));
                    var element = automation.GetDesktop().FindFirstDescendant(
                        automation.ConditionFactory.ByProcessId(process.Id).And(automation.ConditionFactory.ByAutomationId(id)));
                    if (element is not null) return element;
                    Thread.Sleep(100);
                }
                var tree = new List<string> { "pid=" + process.Id + "; client-apartment=" + Thread.CurrentThread.GetApartmentState() };
                foreach (var root in automation.GetDesktop().FindAllChildren(automation.ConditionFactory.ByProcessId(process.Id)))
                {
                    tree.Add($"root {root.ControlType}; pid={root.Properties.ProcessId.ValueOrDefault}; id={root.AutomationId}; name={root.Name}; class={root.ClassName}");
                    foreach (var child in root.FindAllDescendants().Take(200))
                        tree.Add($"  {child.ControlType}; pid={child.Properties.ProcessId.ValueOrDefault}; id={child.AutomationId}; name={child.Name}; class={child.ClassName}");
                }
                File.WriteAllLines(Path.Combine(folder, "uia-timeout-" + id + ".txt"), tree);
                throw new TimeoutException("UI Automation element unavailable: " + id + ". " + ReadFailure(folder));
            }
            Find("ShowStamp").AsButton().Invoke();
            Find("StampDialog");
            Pick("StampPageNumberColor", "#123456", "PickerOK");
            Pick("StampWatermarkColor", "#ABCDEF", "PickerOK");
            // Cancel must preserve the previously accepted page-number color.
            Pick("StampPageNumberColor", "#654321", "PickerCancel");
            Find("StampApply").AsButton().Invoke();
            var watch = Stopwatch.StartNew();
            while (Find("SmokeStatus").Name != "applied:#FF123456:#FFABCDEF")
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(10))
                    throw new InvalidOperationException("Stamp colors were not preserved: " + Find("SmokeStatus").Name);
                Thread.Sleep(100);
            }
            Find("SaveReopen").AsButton().Invoke();
            watch.Restart();
            while (Find("SmokeStatus").Name != "saved:1")
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(10))
                    throw new InvalidOperationException("Save/reopen did not finish: " + Find("SmokeStatus").Name);
                Thread.Sleep(100);
            }
            Console.WriteLine("PASS " + theme + "; app sha256=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))) + "; evidence=" + folder);
            void Pick(string button, string value, string close)
            {
                Find(button).AsButton().Invoke();
                var hex = Find("PickerHex").AsTextBox();
                hex.Focus();
                hex.Text = value;
                var action = Find(close).AsButton();
                action.Focus(); // The real field commits hex edits on LostFocus.
                action.Invoke();
                // Modal windows close through an animation. Await the next enabled parent.
                var wait = Stopwatch.StartNew();
                while (!Find("StampApply").IsEnabled)
                {
                    if (wait.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Picker did not close.");
                    Thread.Sleep(100);
                }
            }
        }
        finally
        {
            // Terminate only the child host created by this case, including any modal windows.
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(5000)) throw new TimeoutException("Owned dialog host did not exit: " + process.Id);
            }
        }
    }

    private static string ReadFailure(string folder) =>
        File.Exists(Path.Combine(folder, "host-error.txt")) ? File.ReadAllText(Path.Combine(folder, "host-error.txt")) : "See retained host evidence.";

    private static int Host(string appPath, string theme, string folder, bool fixtureOnly)
    {
        Directory.CreateDirectory(folder);
        // App settings resolve to this synthetic portable launcher instead of the user's registry.
        string launcher = Path.Combine(folder, "fixture-launcher.exe");
        File.WriteAllBytes(launcher, []);
        Environment.SetEnvironmentVariable("KILLERPDF_LAUNCHER_PATH", launcher);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string candidate = Path.Combine(Path.GetDirectoryName(appPath)!, name.Name + ".dll");
            return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
        };
        var assembly = Assembly.LoadFrom(appPath);
        typeof(WpfApplication).GetField("_resourceAssembly", Static)!.SetValue(null, assembly);
        var app = new WpfApplication { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            File.WriteAllText(Path.Combine(folder, "host-error.txt"), e.Exception.ToString());
            e.Handled = true;
            app.Shutdown(1);
        };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Dark.xaml") });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Strings/en-US.xaml") });
        Type manager = assembly.GetType("KillerPDF.Services.ThemeManager", true)!;
        Type themeType = assembly.GetType("KillerPDF.Services.Theme", true)!;
        object themeValue = Enum.Parse(themeType, theme);
        manager.GetField("_current", Static)!.SetValue(null, themeValue);
        manager.GetMethod("LoadDict", Static)!.Invoke(null, [themeValue, null]);

        string source = Path.Combine(folder, "one-page.pdf");
        File.WriteAllBytes(source, NewPdf());
        Type workingType = assembly.GetType("KillerPDF.Services.PdfWorkingDocument", true)!;
        object working = workingType.GetMethod("Open", Static)!.Invoke(null, [source, false])!;
        // Maintenance retains the app's native PDFium path; it has no Overkill engine renderer.
        string appDirectory = Path.GetDirectoryName(appPath)!;
        string native = Path.Combine(appDirectory, "pdfium.dll");
        if (!File.Exists(native))
            native = Path.Combine(appDirectory, "runtimes", "win-x64", "native", "pdfium.dll");
        // Woven builds preload their embedded native through Costura; payloads keep it loose.
        if (File.Exists(native))
            NativeLibrary.Load(native);
        Assembly docnet = Assembly.Load("Docnet.Core");
        _ = docnet.GetType("Docnet.Core.DocLib", true)!.GetProperty("Instance", Static)!.GetValue(null);
        Type rendererType = assembly.GetType("KillerPDF.Services.PdfiumInterop", true)!;
        var pixels = (byte[]?)rendererType.GetMethod("RenderPageWithAnnotations", Static)!
            .Invoke(null, [source, 0, 612, 792, false, true])
            ?? throw new InvalidOperationException("Maintenance PDFium could not render the synthetic fixture.");
        var bitmap = BitmapSource.Create(612, 792, 96, 96, PixelFormats.Bgra32, null, pixels, 612 * 4);
        bitmap.Freeze();

        void SaveReopen()
        {
            string saved = Path.Combine(folder, "reopened.pdf");
            workingType.GetMethod("Save", Instance, [typeof(string)])!.Invoke(working, [saved]);
            using var reopened = (IDisposable)workingType.GetMethod("Open", Static)!.Invoke(null, [saved, false])!;
            if ((int)workingType.GetProperty("PageCount", Instance)!.GetValue(reopened)! != 1 ||
                !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(saved)))
                throw new InvalidOperationException("The synthetic working document changed during save/reopen.");
            File.WriteAllText(Path.Combine(folder, "fixture-result.txt"), "PASS: real working-document save/reopen; one synthetic page; byte-identical source.");
        }
        if (fixtureOnly)
        {
            try { SaveReopen(); Console.WriteLine("PASS synthetic PDF parses, renders, saves, and reopens through app services."); return 0; }
            finally { ((IDisposable)working).Dispose(); app.Shutdown(); }
        }
        string UiString(string key) => app.TryFindResource(key) as string
            ?? throw new InvalidOperationException("Missing host resource: " + key);
        var status = new TextBlock { Text = UiString("Str_Ready"), Margin = new Thickness(12) };
        AutomationProperties.SetAutomationId(status, "SmokeStatus");
        AutomationProperties.SetName(status, "ready");
        var panel = new StackPanel();
        var owner = new System.Windows.Window { Title = "KillerPDF", Width = 500, Height = 180, Content = panel };
        AutomationProperties.SetAutomationId(owner, "SmokeHost");
        var show = new System.Windows.Controls.Button { Content = UiString("Str_Lbl_Stamp"), Margin = new Thickness(12) };
        AutomationProperties.SetAutomationId(show, "ShowStamp");
        show.Click += (_, _) =>
        {
            Type specType = assembly.GetType("KillerPDF.StampSpec", true)!;
            object spec = Activator.CreateInstance(specType)!;
            specType.GetField("NumbersEnabled", Instance)!.SetValue(spec, true);
            specType.GetField("WmEnabled", Instance)!.SetValue(spec, true);
            Type stampType = assembly.GetType("KillerPDF.StampWindow", true)!;
            var stamp = (System.Windows.Window)Activator.CreateInstance(stampType, [owner, bitmap, 612d, 792d, 1, 0, spec, null])!;
            stamp.ContentRendered += (_, _) =>
            {
                var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(stamp);
                File.WriteAllText(Path.Combine(folder, "stamp-managed.txt"),
                    "pid=" + Environment.ProcessId + "; hwnd=" + new System.Windows.Interop.WindowInteropHelper(stamp).Handle +
                    "; attached-id=" + AutomationProperties.GetAutomationId(stamp) + "; peer-id=" + peer?.GetAutomationId() +
                    "; peer-class=" + peer?.GetClassName() + "; title=" + stamp.Title);
                Snapshot(stamp, Path.Combine(folder, "stamp-initial.png"));
            };
            stamp.ShowDialog();
            object result = stampType.GetProperty("Result", Instance)!.GetValue(stamp)!;
            if ((bool)stampType.GetProperty("Applied", Instance)!.GetValue(stamp)!)
            {
                status.Text = UiString("Str_Btn_OK");
                AutomationProperties.SetName(status, "applied:" + specType.GetField("NumColor", Instance)!.GetValue(result) +
                    ":" + specType.GetField("WmColor", Instance)!.GetValue(result));
            }
            else
            {
                status.Text = UiString("Str_Btn_Cancel");
                AutomationProperties.SetName(status, "canceled");
            }
        };
        var save = new System.Windows.Controls.Button { Content = UiString("Str_Lbl_Save"), Margin = new Thickness(12) };
        AutomationProperties.SetAutomationId(save, "SaveReopen");
        save.Click += (_, _) =>
        {
            SaveReopen(); status.Text = UiString("Str_Lbl_Save");
            AutomationProperties.SetName(status, "saved:1");
        };
        panel.Children.Add(show); panel.Children.Add(save); panel.Children.Add(status);
        owner.Closed += (_, _) => app.Shutdown();
        // Capture only rendered WPF content. No desktop, user windows, or private files are captured.
        app.Activated += (_, _) =>
        {
            foreach (System.Windows.Window window in app.Windows)
                if (AutomationProperties.GetAutomationId(window) == "ColorPickerDialog")
                    window.Dispatcher.BeginInvoke(() => Snapshot(window, Path.Combine(folder, "picker.png")));
        };
        try { app.Run(owner); return 0; }
        finally { ((IDisposable)working).Dispose(); }
    }

    private static void Snapshot(System.Windows.Window window, string path)
    {
        if (window.ActualWidth <= 0 || window.ActualHeight <= 0) return;
        var image = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static byte[] NewPdf()
    {
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>"
        ];
        var source = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        foreach (string value in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(source.ToString()));
            source.Append($"{offsets.Count} 0 obj\n{value}\nendobj\n");
        }
        int xref = Encoding.ASCII.GetByteCount(source.ToString());
        source.Append("xref\n0 4\n0000000000 65535 f \n");
        foreach (int offset in offsets) source.Append($"{offset:D10} 00000 n \n");
        source.Append($"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }
}

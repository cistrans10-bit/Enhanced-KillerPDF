using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Win32;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using KillerPDF.Services;
using PdfPigDoc = UglyToad.PdfPig.PdfDocument;

namespace KillerPDF
{
    public partial class MainWindow
    {
        // About overlay

        private void VersionLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ShowAboutOverlay();
        }

        private void ShowAboutOverlay()
        {
            // Populate dynamic values (SHA256 is slow; run on background thread)
            var version = System.Reflection.Assembly.GetExecutingAssembly()
                               .GetName().Version?.ToString(3) ?? "?";
            var (sigValid, sigSubject, sigThumbprint) = App.GetExeSignerInfo();

            AboutPublisherBlock.Text   = sigValid ? sigSubject : "(not signed or chain failed)";
            AboutThumbprintBlock.Text  = string.IsNullOrEmpty(sigThumbprint) ? "(none)" : sigThumbprint;
            AboutSha256Block.Text      = Loc("Str_About_Computing");

            // Reuse the main window's film-grain texture on the About card.
            if (GrainBrush?.ImageSource != null) AboutGrainBrush.ImageSource = GrainBrush.ImageSource;

            // Logo block: "Killer" in the primary color, "PDF" in the brand green.
            AboutLogoBlock.Inlines.Clear();
            var logoHl = new System.Windows.Documents.Hyperlink { TextDecorations = null };
            logoHl.Inlines.Add(new System.Windows.Documents.Run("Killer")
            {
                FontSize = 21,
                FontWeight = System.Windows.FontWeights.Normal,
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary")
            });
            logoHl.Inlines.Add(new System.Windows.Documents.Run("PDF")
            {
                FontFamily = UiKit.WordmarkFontPdf,
                FontSize = 27.3,
                Foreground = (System.Windows.Media.Brush)FindResource("AccentLogo")
            });
            logoHl.Click += (_, _) =>
                Process.Start(new ProcessStartInfo("https://killerpdf.net") { UseShellExecute = true });
            AboutLogoBlock.Inlines.Add(logoHl);

            // Tagline block
            AboutTaglineBlock.Inlines.Clear();
            // Localized tagline. {0} is the (untranslated) brand, so splitting on the placeholder
            // keeps "Killer Tools" a styled, clickable link while the rest translates and the brand
            // can sit anywhere in the sentence the language needs it.
            var taglineDim = (System.Windows.Media.Brush)FindResource("TextSecondary");
            var taglineText = Loc("Str_Tagline");
            int taglineBrand = taglineText.IndexOf("{0}", StringComparison.Ordinal);
            string taglinePre = taglineBrand >= 0 ? taglineText[..taglineBrand] : taglineText;
            string taglineSuf = taglineBrand >= 0 ? taglineText[(taglineBrand + 3)..] : "";
            AboutTaglineBlock.Inlines.Add(new System.Windows.Documents.Run(taglinePre) { Foreground = taglineDim });
            var ktHl = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("Killer Tools"))
            {
                Foreground      = (System.Windows.Media.Brush)FindResource("Accent"),
                TextDecorations = null
            };
            ktHl.Click += (_, _) =>
                Process.Start(new ProcessStartInfo("https://killertools.net") { UseShellExecute = true });
            AboutTaglineBlock.Inlines.Add(ktHl);
            AboutTaglineBlock.Inlines.Add(new System.Windows.Documents.Run(taglineSuf) { Foreground = taglineDim });

            // Version block (clickable - opens GitHub release)
            AboutVersionBlock.Inlines.Clear();
            var verHl = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run($"v{version}"))
            {
                Foreground      = (System.Windows.Media.Brush)FindResource("Accent"),
                TextDecorations = null
            };
            verHl.Click += (_, _) =>
                Process.Start(new ProcessStartInfo(
                    $"https://github.com/SteveTheKiller/KillerPDF/releases/tag/v{version}")
                { UseShellExecute = true });
            AboutVersionBlock.Inlines.Add(verHl);

            // Update check: hidden until/unless a newer release is confirmed online
            AboutUpdateButton.Visibility = Visibility.Collapsed;
            FadeOverlayIn(AboutOverlay);
            CheckForUpdateAsync(System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

            // Compute SHA256 off the UI thread
            System.Threading.Tasks.Task.Run(() =>
            {
                var sha256 = App.GetExeSha256();
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    (Action)(() => AboutSha256Block.Text = sha256));
            });
        }

        private void AboutOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FadeOverlayOut(AboutOverlay);
        }

        private void AboutOverlayCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void AboutOverlayClose_Click(object sender, RoutedEventArgs e)
        {
            FadeOverlayOut(AboutOverlay);
        }

        // "Clear all Data" footer link: wipes settings, downloaded OCR language packs, and temp files after
        // an explicit confirmation. Destructive, so it always warns first; the user's PDFs are untouched.
        private void AboutClearData_Click(object sender, RoutedEventArgs e)
        {
            var res = KillerDialog.Show(this,
                "This will delete all saved settings, downloaded OCR language packs, and temporary files.\n\n" +
                "Your PDF files are not affected. Continue?",
                "Clear all Data", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            App.ClearAllData();
            SetStatus(Loc("Str_St_DataCleared"));
            KillerDialog.Show(this,
                "Settings, language packs, and temp files were cleared.\n\n" +
                "Restart KillerPDF to finish clearing any files still in use this session.",
                "Clear all Data", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Quietly checks GitHub for a newer release when the About dialog opens. Runs only on
        // demand (no background service), times out fast, and silently does nothing if there is
        // no internet or the request fails. Shows the update button only if a newer tag exists.
        private async void CheckForUpdateAsync(Version? current)
        {
            if (current is null) return;
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("KillerPDF-UpdateCheck");
                var json = await http.GetStringAsync(
                    "https://api.github.com/repos/SteveTheKiller/KillerPDF/releases/latest")
                    .ConfigureAwait(false);

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("tag_name", out var tagEl)) return;
                var tag = tagEl.GetString();
                if (string.IsNullOrWhiteSpace(tag)) return;
                if (!Version.TryParse(tag!.TrimStart('v', 'V').Trim(), out var latest)) return;

                var cur = new Version(current.Major, current.Minor, current.Build < 0 ? 0 : current.Build);
                var lat = new Version(latest.Major, latest.Minor, latest.Build < 0 ? 0 : latest.Build);
                if (lat <= cur) return;

                await Dispatcher.BeginInvoke((Action)(() =>
                {
                    _updateTag = $"v{lat.ToString(3)}";
                    AboutUpdateText.Text = $"Update available: {_updateTag}";
                    AboutUpdateButton.Visibility = Visibility.Visible;
                }));
            }
            catch { /* offline, timeout, or API error - quietly do nothing */ }
        }

        private string? _updateTag;   // "vX.Y.Z" of the available update, set by CheckForUpdateAsync

        private void AboutUpdateButton_Click(object sender, RoutedEventArgs e) => DoSelfUpdateAsync();

        // One-click self-update: downloads the released exe, verifies it against the published
        // SHA256SUMS.txt, then hands off to a small batch that waits for this process to exit,
        // swaps the exe in place, and relaunches with the currently-open PDF. Falls back to the
        // releases page if anything fails (offline, checksum mismatch, unwritable location).
        private async void DoSelfUpdateAsync()
        {
            var tag = _updateTag;
            if (string.IsNullOrEmpty(tag)) return;

            if (_isDirty)
            {
                KillerDialog.Show(this, Loc("Str_Dlg_SaveBeforeUpdate"),
                    "KillerPDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = KillerDialog.Show(this,
                $"Download and install KillerPDF {tag}?\n\nThe app will close and reopen automatically.",
                "KillerPDF", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.OK) return;

            AboutUpdateButton.IsEnabled = false;
            AboutUpdateText.Text = "Downloading...";

            string? newExe = null;
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(90) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("KillerPDF-UpdateCheck");

                var exeUrl  = $"https://github.com/SteveTheKiller/KillerPDF/releases/download/{tag}/KillerPDF.exe";
                // Read the checksums from the release ASSET next to the exe, not from raw.githubusercontent
                // at the tag. Both files are uploaded to the release together, so the hash can never drift
                // from the exe the way a repo-committed file does when the tag/commit order gets muddled.
                var sumsUrl = $"https://github.com/SteveTheKiller/KillerPDF/releases/download/{tag}/SHA256SUMS.txt";

                var exeBytes = await http.GetByteArrayAsync(exeUrl);
                var sumsTxt  = await http.GetStringAsync(sumsUrl);

                // Find the expected hash for KillerPDF.exe
                string? expected = null;
                foreach (var line in sumsTxt.Replace("\r", "").Split('\n'))
                {
                    if (line.TrimStart().StartsWith("KillerPDF.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) expected = parts[^1];
                        break;
                    }
                }
                if (string.IsNullOrEmpty(expected)) throw new Exception("checksum entry not found");

                string actual;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    actual = BitConverter.ToString(sha.ComputeHash(exeBytes)).Replace("-", "");
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("checksum mismatch");

                newExe = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"KillerPDF_update_{Guid.NewGuid():N}.exe");
                File.WriteAllBytes(newExe, exeBytes);
            }
            catch
            {
                // Offline, timed out, or verification failed: restore the button and open the
                // releases page so the user can update manually.
                AboutUpdateButton.IsEnabled = true;
                AboutUpdateText.Text = $"Update available: {tag}";
                try { Process.Start(new ProcessStartInfo(
                    "https://github.com/SteveTheKiller/KillerPDF/releases/latest") { UseShellExecute = true }); }
                catch { }
                return;
            }

            // Apply the update after we exit, then relaunch reopening the current PDF.
            try
            {
                var curExe = Process.GetCurrentProcess().MainModule!.FileName;
                var reopen = _originalFile ?? _currentFile;
                var pid    = Process.GetCurrentProcess().Id;
                var relArg = string.IsNullOrEmpty(reopen) ? "" : $" \"{reopen}\"";
                var bat    = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"killerpdf_update_{Guid.NewGuid():N}.bat");

                File.WriteAllText(bat,
                    "@echo off\r\n" +
                    ":wait\r\n" +
                    $"tasklist /fi \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul\r\n" +
                    "if not errorlevel 1 ( ping -n 2 127.0.0.1 >nul & goto wait )\r\n" +
                    $"copy /y \"{newExe}\" \"{curExe}\" >nul\r\n" +
                    $"start \"\" \"{curExe}\"{relArg}\r\n" +
                    $"del \"{newExe}\" >nul 2>&1\r\n" +
                    "del \"%~f0\" >nul 2>&1\r\n");

                Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
            catch
            {
                AboutUpdateButton.IsEnabled = true;
                AboutUpdateText.Text = $"Update available: {tag}";
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using KillerPDF.Controls;

namespace KillerPDF
{
    /// <summary>The two personas of the unified editor.</summary>
    internal enum AppMode { Pdf, Docx }

    // DOCX mode: one window, two personas (Affinity-style). PDF mode is KillerPDF itself; DOCX
    // mode is WordCraft (github.com/cistrans10-bit/wordcraft-pages, built from WordCraft/ in this
    // repository), started as `wordcraft.exe --embedded --control <port>` and re-parented into
    // WordHostSlot. Its own ribbon, page view and status bar fill the window under KillerPDF's
    // tab strip, which carries the PDF / DOCX switch.
    //
    // KillerPDF drives it over WordCraft's localhost control channel: one JSON request per line,
    // authorised by the random key WordCraft writes to %APPDATA%\WordCraft\control-key.local-<port>.
    // That is how a .docx opened here lands in WordCraft (file.open), how "Open as PDF" exports
    // (file.exportPdf) and how closing asks about unsaved Word documents (file.info, file.save).
    public partial class MainWindow
    {
        private AppMode _appMode = AppMode.Pdf;
        private Process? _word;
        private int _wordPort;
        private string? _wordKey;
        private int _wordRequestId;
        private Task<bool>? _wordStarting;

        /// <summary>Extensions DOCX mode opens. WordCraft reads all of these.</summary>
        private static readonly string[] WordExtensions =
            [".docx", ".docm", ".dotx", ".dotm", ".doc", ".dot", ".rtf", ".odt", ".fodt", ".md", ".markdown"];

        internal static bool IsWordPath(string? path)
            => !string.IsNullOrEmpty(path)
               && Array.Exists(WordExtensions, ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

        private static string WordExePath => Path.Combine(AppContext.BaseDirectory, "WordCraft", "wordcraft.exe");

        // ── Switching personas ────────────────────────────────────────────

        private void ModePdf_Click(object sender, RoutedEventArgs e)
        {
            SetAppMode(AppMode.Pdf);
            UpdateLayout();
            // Repaint at the restored size: pages were laid out against a collapsed view.
            if (_doc is not null)
                ActiveViewer.RenderPage(_viewMode == ViewMode.Grid ? 0 : Math.Max(0, PageList.SelectedIndex), keepTiles: true);
        }
        private async void ModeDocx_Click(object sender, RoutedEventArgs e)
        {
            SetAppMode(AppMode.Docx);
            await EnsureWordAsync();
        }

        /// <summary>Shows one persona's chrome and hides the other's. WordCraft keeps running
        /// (and keeps its document) while PDF mode is showing.</summary>
        private void SetAppMode(AppMode mode)
        {
            _appMode = mode;
            bool docx = mode == AppMode.Docx;
            var pdfVis = docx ? Visibility.Collapsed : Visibility.Visible;

            // PDF persona: the mode bar and toolbar, the tools panel / document / pages body, the
            // document tab and the status bar.
            CraftHeaderStack.Visibility = pdfVis;
            CraftBodyGrid.Visibility = pdfVis;
            FooterBorder.Visibility = pdfVis;
            CraftDocTab.Visibility = docx ? Visibility.Collapsed : Visibility.Visible;
            if (!docx) CraftDocTab.ClearValue(VisibilityProperty);   // its style hides it with no PDF open

            // DOCX persona: WordCraft's window, and Open as PDF beside + Open.
            WordModePanel.Visibility = docx ? Visibility.Visible : Visibility.Collapsed;
            WordToPdfBtn.Visibility = docx ? Visibility.Visible : Visibility.Collapsed;

            ModePdfBtn.Tag = docx ? null : "selected";
            ModeDocxBtn.Tag = docx ? "selected" : null;

            if (docx)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, (Action)(() =>
                {
                    WordHostSlot.Repaint();
                    WordHostSlot.FocusChild();
                }));
                // Any control request wakes WordCraft's event loop, so it draws a fresh frame.
                if (_wordKey is not null) _ = WordExecuteAsync("file.info", new JsonObject());
            }
        }

        // ── Opening documents ─────────────────────────────────────────────

        /// <summary>Opens a Word-family document in DOCX mode.</summary>
        internal async void OpenInWordMode(string path)
        {
            SetAppMode(AppMode.Docx);
            if (_word is null || _word.HasExited)
            {
                // A fresh start opens the file from its command line: no round trip to wait for.
                await EnsureWordAsync(path);
                return;
            }
            if (!await EnsureWordAsync()) return;
            var reply = await WordExecuteAsync("file.open", new JsonObject { ["path"] = path });
            if (reply?["ok"]?.GetValue<bool>() != true)
                ShowWordError(Loc("Str_Studio_OpenFailed"), reply?["error"]?.ToString());
            else
                App.AddRecentFile(path);
        }

        /// <summary>"+ Open": in DOCX mode WordCraft shows its own Open dialog.</summary>
        private async void StudioOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_appMode == AppMode.Pdf)
            {
                Open_Click(sender, e);
                return;
            }
            if (await EnsureWordAsync())
                await WordExecuteAsync("file.open", new JsonObject());
        }

        /// <summary>DOCX → PDF: export the Word document to PDF and open it in PDF mode, where it
        /// can be annotated, signed, stamped, combined or OCR'd.</summary>
        private async void WordToPdf_Click(object sender, RoutedEventArgs e)
        {
            if (!await EnsureWordAsync()) return;
            var info = await WordExecuteAsync("file.info", new JsonObject());
            string? docPath = info?["result"]?["path"]?.ToString();
            string name = SafeFileStem(string.IsNullOrEmpty(docPath) ? null : Path.GetFileNameWithoutExtension(docPath));

            string dir = Path.Combine(App.TempDir, "word-export", Guid.NewGuid().ToString("N"));
            string pdf = Path.Combine(dir, name + ".pdf");
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex) { ShowWordError(Loc("Str_Studio_ExportFailed"), ex.Message); return; }

            SetStatus(Loc("Str_Studio_Exporting"));
            var reply = await WordExecuteAsync("file.exportPdf", new JsonObject { ["path"] = pdf }, TimeSpan.FromMinutes(2));
            if (reply?["ok"]?.GetValue<bool>() != true || !File.Exists(pdf))
            {
                ShowWordError(Loc("Str_Studio_ExportFailed"), reply?["error"]?.ToString());
                return;
            }
            OpenInNewTab(pdf);   // switches to PDF mode and lays it out first
        }

        private static string SafeFileStem(string? stem)
        {
            if (string.IsNullOrWhiteSpace(stem)) return "Document";
            foreach (char c in Path.GetInvalidFileNameChars()) stem = stem.Replace(c, '_');
            return stem.Length > 120 ? stem[..120] : stem;
        }

        private void ShowWordError(string message, string? detail)
            => KillerDialog.Show(this, string.IsNullOrEmpty(detail) ? message : message + "\n" + detail,
                                 "KillerPDF", MessageBoxButton.OK, MessageBoxImage.Warning);

        // ── Starting WordCraft ────────────────────────────────────────────

        /// <summary>Starts WordCraft (once) and embeds its window. True when it is ready to take
        /// requests. Concurrent callers share the one start.</summary>
        private Task<bool> EnsureWordAsync(string? openPath = null)
        {
            if (_word is { HasExited: false } && _wordKey is not null && WordHostSlot.HasChild)
                return Task.FromResult(true);
            if (_wordStarting is { IsCompleted: false }) return _wordStarting;
            _wordStarting = StartWordAsync(openPath);
            return _wordStarting;
        }

        private async Task<bool> StartWordAsync(string? openPath)
        {
            string exe = WordExePath;
            if (!File.Exists(exe))
            {
                WordStatus.Text = Loc("Str_Studio_Missing");
                WordStatus.Visibility = Visibility.Visible;
                return false;
            }
            WordStatus.Text = Loc("Str_Studio_Starting");
            WordStatus.Visibility = Visibility.Visible;

            try
            {
                _wordPort = FreeLoopbackPort();
                string keyFile = WordKeyFile(_wordPort);
                try { File.Delete(keyFile); } catch { }   // a stale key from a run that crashed

                var psi = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe)!,
                };
                psi.ArgumentList.Add("--embedded");
                psi.ArgumentList.Add("--control");
                psi.ArgumentList.Add(_wordPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(openPath)) psi.ArgumentList.Add(openPath);
                _word = Process.Start(psi) ?? throw new InvalidOperationException("no process");
                _word.EnableRaisingEvents = true;
                _word.Exited += (_, _) => Dispatcher.BeginInvoke((Action)OnWordExited);

                int pid = _word.Id;
                var (window, key) = await Task.Run(() =>
                {
                    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                    IntPtr hwnd = IntPtr.Zero;
                    string? k = null;
                    while (DateTime.UtcNow < deadline && (hwnd == IntPtr.Zero || k is null))
                    {
                        if (hwnd == IntPtr.Zero) hwnd = FindWordWindow(pid);
                        if (k is null) k = ReadKey(keyFile);
                        if (hwnd == IntPtr.Zero || k is null) Thread.Sleep(100);
                    }
                    return (hwnd, k);
                });
                if (window == IntPtr.Zero || key is null)
                    throw new TimeoutException(window == IntPtr.Zero ? "no window" : "no control key");

                _wordKey = key;
                WordHostSlot.Embed(window);
                WordStatus.Visibility = Visibility.Collapsed;
                if (!string.IsNullOrEmpty(openPath)) App.AddRecentFile(openPath);
                if (_appMode == AppMode.Docx) WordHostSlot.FocusChild();
                return true;
            }
            catch (Exception ex)
            {
                WordStatus.Text = string.Format(Loc("Str_Studio_Failed"), ex.Message);
                WordStatus.Visibility = Visibility.Visible;
                try { if (_word is { HasExited: false }) _word.Kill(); } catch { }
                _word = null;
                _wordKey = null;
                return false;
            }
        }

        private void OnWordExited()
        {
            _wordKey = null;
            WordHostSlot.Release();
            if (_closingWord) return;
            // It closed or crashed under the user: say so, and start a fresh one on the next switch.
            WordStatus.Text = Loc("Str_Studio_Exited");
            WordStatus.Visibility = Visibility.Visible;
        }

        private static int FreeLoopbackPort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static string WordKeyFile(int port)
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WordCraft",
                            $"control-key.local-{port}");

        private static string? ReadKey(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string k = File.ReadAllText(path).Trim();
                return k.Length >= 32 ? k : null;
            }
            catch { return null; }
        }

        /// <summary>WordCraft's main window: the visible, titled top-level window of that process.</summary>
        private static IntPtr FindWordWindow(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out uint owner);
                if (owner != pid) return true;
                var sb = new StringBuilder(256);
                GetWindowTextW(hwnd, sb, sb.Capacity);
                if (sb.ToString().StartsWith("WordCraft", StringComparison.Ordinal))
                {
                    found = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        // ── Control channel ───────────────────────────────────────────────

        /// <summary>Runs one WordCraft command; the reply ({"ok", "result" | "error"}) or null
        /// when WordCraft is not reachable.</summary>
        private Task<JsonNode?> WordExecuteAsync(string command, JsonObject parameters, TimeSpan? timeout = null)
        {
            var request = new JsonObject
            {
                ["method"] = "engine.execute",
                ["params"] = new JsonObject { ["command"] = command, ["params"] = parameters },
            };
            return Task.Run(() => WordRequest(request, timeout ?? TimeSpan.FromSeconds(15)));
        }

        private JsonNode? WordRequest(JsonObject request, TimeSpan timeout)
        {
            string? key = _wordKey;
            if (key is null || _wordPort == 0) return null;
            try
            {
                request["id"] = Interlocked.Increment(ref _wordRequestId);
                request["key"] = key;
                using var client = new TcpClient();
                client.ReceiveTimeout = client.SendTimeout = (int)timeout.TotalMilliseconds;
                client.Connect(IPAddress.Loopback, _wordPort);
                using var stream = client.GetStream();
                byte[] line = Encoding.UTF8.GetBytes(request.ToJsonString() + "\n");
                stream.Write(line, 0, line.Length);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string? reply = reader.ReadLine();
                return reply is null ? null : JsonNode.Parse(reply);
            }
            catch (Exception ex) when (ex is IOException or SocketException or JsonException or InvalidOperationException)
            {
                return null;
            }
        }

        // ── Closing ───────────────────────────────────────────────────────

        private bool _closingWord;

        /// <summary>Called from OnClosing before the PDF prompts. False cancels the close: the
        /// Word document has unsaved changes and the user chose Cancel, or Save needs a file name
        /// first (WordCraft's Save As dialog is then open in DOCX mode).</summary>
        private bool ConfirmCloseWord()
        {
            if (_word is null || _word.HasExited || _wordKey is null) return true;
            var info = WordRequest(new JsonObject
            {
                ["method"] = "engine.execute",
                ["params"] = new JsonObject { ["command"] = "file.info", ["params"] = new JsonObject() },
            }, TimeSpan.FromSeconds(3));
            bool dirty = info?["result"]?["dirty"]?.GetValue<bool>() == true;
            if (dirty)
            {
                SetAppMode(AppMode.Docx);
                string? path = info?["result"]?["path"]?.ToString();
                string name = string.IsNullOrEmpty(path) ? Loc("Str_Studio_Untitled") : Path.GetFileName(path);
                var res = KillerDialog.Show(this, string.Format(Loc("Str_Studio_SaveDocx"), name),
                    Loc("Str_Dlg_AppTitle"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, fadeClose: false,
                    defaultResult: MessageBoxResult.Cancel);
                if (res == MessageBoxResult.Cancel) return false;
                if (res == MessageBoxResult.Yes)
                {
                    var saved = WordRequest(new JsonObject
                    {
                        ["method"] = "engine.execute",
                        ["params"] = new JsonObject { ["command"] = "file.save", ["params"] = new JsonObject() },
                    }, TimeSpan.FromSeconds(30));
                    // No file name yet: WordCraft opened Save As. Stay open so the user can finish it.
                    if (saved?["ok"]?.GetValue<bool>() != true || saved?["result"]?["saved"]?.GetValue<bool>() == false)
                        return false;
                }
            }
            return true;
        }

        /// <summary>Ends WordCraft with the window: asks it to quit, then makes sure.</summary>
        private void ShutDownWord()
        {
            if (_word is null) return;
            _closingWord = true;
            try
            {
                if (!_word.HasExited)
                {
                    WordRequest(new JsonObject { ["method"] = "app.quit", ["params"] = new JsonObject() }, TimeSpan.FromSeconds(2));
                    if (!_word.WaitForExit(3000)) _word.Kill();
                }
            }
            catch { }
            WordHostSlot.Release();
        }

        // ── Win32 ─────────────────────────────────────────────────────────
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hwnd, StringBuilder text, int max);
    }
}

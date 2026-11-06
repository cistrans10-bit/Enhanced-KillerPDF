using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using KillerPDF.Services;

namespace KillerPDF
{
    // PDF + DOCX mode: one document, open as a Word document (WordCraft, left) and as a PDF
    // (KillerPDF, right) at the same time, page for page.
    //
    // The two are linked. A Word document's edits flow into the PDF: whenever WordCraft's
    // revision counter moves and then holds still for a moment, the document is exported again
    // and the linked PDF tab swaps to the new file, keeping its page, zoom and the markup drawn on
    // it in KillerPDF (highlights, notes, ink, stamps, signatures and text boxes are a layer over
    // the PDF until it is saved, so they ride over every re-export). Pages follow each other both
    // ways: scroll the Word document and the PDF turns to the same page, and the other way round.
    //
    // Either side can start the link:
    //   - a PDF: it is converted to an editable .docx beside it (Services/PdfToDocx.cs, page for
    //     page: every PDF page is one section of the same size) and opened in WordCraft;
    //   - a Word document: it is exported to PDF and opened in a new linked tab, which saves as
    //     <document>.pdf beside it.
    public partial class MainWindow
    {
        private sealed class EditLink
        {
            public required Controls.PdfViewer.DocumentSession Session;
            public ulong DocumentId;
            public ulong Revision;              // the revision the PDF shows
            public ulong SeenRevision;          // the revision at the last check (debounce)
            public string? PendingPdf;          // exported, waiting for the tab to be in front
            public int WordPage = -1, PdfPage = -1;
            /// <summary>Markup on pages a re-export dropped, put back if the pages return.</summary>
            public readonly Dictionary<int, List<PageAnnotation>> Stash = [];
        }

        private EditLink? _link;
        private DispatcherTimer? _linkTimer;
        private bool _linkBusy;
        private double _linkSplit = -1;          // DOCX pane width in PDF + DOCX mode (DIPs)
        private Thickness[]? _pdfChromeMargins;
        private const double LinkSplitterWidth = 6, LinkMinPane = 320;

        // ── The mode ──────────────────────────────────────────────────────

        private async void ModeBoth_Click(object sender, RoutedEventArgs e)
        {
            var from = _appMode;
            SetAppMode(AppMode.Both);
            UpdateLayout();
            RerenderPdfPane();
            if (!await EnsureWordAsync()) return;

            if (LinkAlive())
            {
                if (!ReferenceEquals(_active, _link!.Session)) ActiveViewer.SwitchToTabExt(_link.Session);
                await SyncLinkAsync(force: true);
                return;
            }

            var info = (await WordExecuteAsync("file.info", new JsonObject()))?["result"];
            bool wordHasDoc = !string.IsNullOrEmpty(info?["path"]?.ToString())
                              || info?["dirty"]?.GetValue<bool>() == true
                              || (info?["words"]?.GetValue<long>() ?? 0) > 0;
            bool pdfOpen = _doc is not null && _active is not null;

            if (pdfOpen && (from != AppMode.Docx || !wordHasDoc)) await LinkFromPdfAsync();
            else if (wordHasDoc) await LinkFromWordAsync();
            else SetStatus(Loc("Str_Linked_OpenSomething"));
        }

        /// <summary>"Edit as DOCX" (Convert tools, the PDF tab's menu): the open PDF, side by side
        /// with an editable Word copy.</summary>
        private async void EditAsDocx_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) { KillerDialog.Show(this, Loc("Str_Msg_OpenFirst")); return; }
            SetAppMode(AppMode.Both);
            UpdateLayout();
            RerenderPdfPane();
            if (!await EnsureWordAsync()) return;
            await LinkFromPdfAsync();
        }

        /// <summary>The PDF view changed size (mode switch, splitter): once layout has run, fit
        /// it again if it fits to the window, and draw the page at the new size.</summary>
        private void RerenderPdfPane()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)(() =>
            {
                if (_doc is null || _appMode == AppMode.Docx) return;
                if (_fitMode == FitMode.Page) FitToPage();
                else if (_fitMode == FitMode.Width) FitToWidth();
                ActiveViewer.RenderPage(_viewMode == ViewMode.Grid ? 0 : Math.Max(0, ActiveViewer.CurrentPageIndex), keepTiles: true);
            }));
        }

        /// <summary>Lays the two personas out side by side: WordCraft on the left, KillerPDF's
        /// toolbar, document and status bar on the right, a splitter between.</summary>
        private void ApplyBothLayout(bool both)
        {
            _pdfChromeMargins ??= [CraftHeaderStack.Margin, CraftBodyGrid.Margin, FooterBorder.Margin];
            if (!both)
            {
                CraftHeaderStack.Margin = _pdfChromeMargins[0];
                CraftBodyGrid.Margin = _pdfChromeMargins[1];
                FooterBorder.Margin = _pdfChromeMargins[2];
                WordModePanel.ClearValue(WidthProperty);
                WordModePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                LinkSplitter.Visibility = Visibility.Collapsed;
                return;
            }
            double total = Math.Max(1, RootClipGrid.ActualWidth > 0 ? RootClipGrid.ActualWidth : ActualWidth);
            if (_linkSplit < 0) _linkSplit = total / 2;
            double max = Math.Max(LinkMinPane, total - LinkMinPane - LinkSplitterWidth);
            _linkSplit = Math.Clamp(_linkSplit, Math.Min(LinkMinPane, max), max);
            double right = _linkSplit + LinkSplitterWidth;
            CraftHeaderStack.Margin = With(_pdfChromeMargins[0], right);
            CraftBodyGrid.Margin = With(_pdfChromeMargins[1], right);
            FooterBorder.Margin = With(_pdfChromeMargins[2], right);
            WordModePanel.HorizontalAlignment = HorizontalAlignment.Left;
            WordModePanel.Width = _linkSplit;
            LinkSplitter.Margin = new Thickness(_linkSplit, 0, 0, 0);
            LinkSplitter.Visibility = Visibility.Visible;

            static Thickness With(Thickness t, double left) => new(t.Left + left, t.Top, t.Right, t.Bottom);
        }

        private void LinkSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_appMode != AppMode.Both) return;
            _linkSplit += e.HorizontalChange;
            ApplyBothLayout(true);
        }

        private void LinkSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (_appMode != AppMode.Both) return;
            RerenderPdfPane();
            WordHostSlot.Repaint();
        }

        // ── Making the link ───────────────────────────────────────────────

        /// <summary>The PDF in front → an editable .docx beside it, open in WordCraft.</summary>
        private async Task LinkFromPdfAsync()
        {
            var session = _active;
            string? working = _currentFile;
            if (session is null || working is null || !File.Exists(working)) return;
            if (!await ConfirmReplaceWordDocAsync()) return;

            string original = _originalFile ?? working;
            string docx = UniqueSibling(original, ".docx");
            var busy = ShowBusyOverlay(Loc("Str_Linked_Converting"));
            try
            {
                string target = docx;
                int pages = await Task.Run(() =>
                {
                    try { return PdfToDocx.Convert(working, target); }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                    // The PDF's folder isn't writable (a download folder on a share, a CD...):
                    // the copy goes to Documents instead.
                    target = UniqueSibling(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                                        Path.GetFileName(original)), ".docx");
                    return PdfToDocx.Convert(working, target);
                });
                docx = target;
            }
            catch (Exception ex)
            {
                HideBusyOverlay(busy);
                ShowWordError(Loc("Str_Linked_ConvertFailed"), ex.Message);
                return;
            }
            HideBusyOverlay(busy);

            var reply = await WordExecuteAsync("file.open", new JsonObject { ["path"] = docx }, TimeSpan.FromMinutes(2));
            if (reply?["ok"]?.GetValue<bool>() != true)
            {
                ShowWordError(Loc("Str_Studio_OpenFailed"), reply?["error"]?.ToString());
                return;
            }
            App.AddRecentFile(docx);
            await StartLinkAsync(session);
            int page = ActiveViewer.CurrentPageIndex;
            if (page > 0) await WordExecuteAsync("edit.goto", new JsonObject { ["page"] = page + 1 });
            SetStatus(string.Format(Loc("Str_Linked_Converted"), Path.GetFileName(docx)));
        }

        /// <summary>WordCraft's document → a PDF in a new linked tab, saving as &lt;document&gt;.pdf.</summary>
        private async Task LinkFromWordAsync()
        {
            var info = (await WordExecuteAsync("file.info", new JsonObject()))?["result"];
            string? docPath = info?["path"]?.ToString();
            string? pdf = await ExportWordAsync();
            if (pdf is null) return;

            ActiveViewer.OpenInNewTabExt(pdf);
            if (_doc is null || _active is null || !string.Equals(_currentFile, pdf, StringComparison.OrdinalIgnoreCase)) return;
            // The new tab saves beside the Word document, not in the temp folder.
            _originalFile = string.IsNullOrEmpty(docPath) ? null : UniqueSibling(docPath, ".pdf");
            _active.OriginalFile = _originalFile;
            FileNameLabel.Text = _originalFile is null ? Loc("Str_Studio_Untitled") : Path.GetFileName(_originalFile);
            MarkDirty();
            await StartLinkAsync(_active);
        }

        private async Task StartLinkAsync(Controls.PdfViewer.DocumentSession session)
        {
            var info = (await WordExecuteAsync("file.info", new JsonObject()))?["result"];
            _link = new EditLink
            {
                Session = session,
                DocumentId = info?["documentId"]?.GetValue<ulong>() ?? 0,
                Revision = info?["revision"]?.GetValue<ulong>() ?? 0,
            };
            _link.SeenRevision = _link.Revision;
            _link.PdfPage = ActiveViewer.CurrentPageIndex;
            EnsureLinkTimer();
        }

        private void EnsureLinkTimer()
        {
            if (_linkTimer is null)
            {
                _linkTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(700) };
                _linkTimer.Tick += async (_, _) => await SyncLinkAsync(force: false);
            }
            _linkTimer.IsEnabled = _appMode == AppMode.Both && _link is not null;
        }

        /// <summary>Called from SetAppMode: syncs run while both are on screen; leaving PDF +
        /// DOCX for PDF mode brings the PDF up to date first.</summary>
        private void OnAppModeChangedForLink(AppMode from, AppMode to)
        {
            ApplyBothLayout(to == AppMode.Both);
            UpdateCraftChrome();
            if (from != to && to != AppMode.Docx) RerenderPdfPane();
            EnsureLinkTimer();
            if (_link is not null && from != AppMode.Pdf && to != AppMode.Docx)
                _ = SyncLinkAsync(force: true);
        }

        private bool LinkAlive()
        {
            if (_link is null) return false;
            if (Viewer.OwnsSessionExt(_link.Session) || ViewerB.OwnsSessionExt(_link.Session)) return true;
            DropLink();   // its tab was closed
            return false;
        }

        private void DropLink()
        {
            _link = null;
            if (_linkTimer is not null) _linkTimer.IsEnabled = false;
        }

        // ── Keeping them in step ──────────────────────────────────────────

        /// <summary>One sync step: re-export after an edit (once the edits pause), swap the
        /// exported PDF in when its tab is in front, and keep the two on the same page.</summary>
        private async Task SyncLinkAsync(bool force)
        {
            if (_linkBusy || !LinkAlive() || _wordKey is null) return;
            _linkBusy = true;
            try
            {
                var link = _link!;
                var info = (await WordExecuteAsync("file.info", new JsonObject(), TimeSpan.FromSeconds(5)))?["result"];
                if (info is null || !ReferenceEquals(link, _link)) return;
                ulong docId = info["documentId"]?.GetValue<ulong>() ?? 0;
                ulong rev = info["revision"]?.GetValue<ulong>() ?? 0;

                if (docId != link.DocumentId)
                {
                    // Another document was opened in WordCraft: it gets a linked PDF of its own.
                    DropLink();
                    if (_appMode == AppMode.Both) await LinkFromWordAsync();
                    return;
                }

                // Edits: export once the revision has held still for a tick (typing pauses).
                if (rev != link.Revision && (force || rev == link.SeenRevision))
                {
                    string? pdf = await ExportWordAsync(quiet: true);
                    if (pdf is not null && ReferenceEquals(link, _link))
                    {
                        link.Revision = rev;
                        if (link.PendingPdf is not null) TryDelete(link.PendingPdf);
                        link.PendingPdf = pdf;
                    }
                }
                link.SeenRevision = rev;

                if (link.PendingPdf is not null && CanSwapLinkedPdf(link))
                {
                    string pdf = link.PendingPdf;
                    link.PendingPdf = null;
                    ReplaceLinkedPdf(link, pdf);
                }

                if (_appMode == AppMode.Both && ReferenceEquals(_active, link.Session) && _doc is not null)
                    await SyncPagesAsync(link);
            }
            finally { _linkBusy = false; }
        }

        /// <summary>Not under the user's hands: the tab is in front, and no drag or text box
        /// edit is in progress there (the swap re-renders the page).</summary>
        private bool CanSwapLinkedPdf(EditLink link)
            => ReferenceEquals(_active, link.Session)
               && _doc is not null
               && Mouse.LeftButton == MouseButtonState.Released
               && Mouse.Captured is null
               && Keyboard.FocusedElement is not TextBox;

        /// <summary>The page both sides show: whichever side moved since the last look leads.</summary>
        private async Task SyncPagesAsync(EditLink link)
        {
            int pdfPage = ActiveViewer.CurrentPageIndex;
            int wordPage = await WordVisiblePageAsync();
            if (wordPage < 0 || !ReferenceEquals(link, _link)) return;
            int pageCount = _doc?.PageCount ?? 0;

            if (wordPage != link.WordPage)
            {
                link.WordPage = wordPage;
                if (wordPage != pdfPage && wordPage < pageCount)
                {
                    ActiveViewer.NavigateToPageExt(wordPage);
                    pdfPage = wordPage;
                }
                link.PdfPage = pdfPage;
            }
            else if (pdfPage != link.PdfPage)
            {
                link.PdfPage = pdfPage;
                if (pdfPage != wordPage)
                {
                    await WordExecuteAsync("edit.goto", new JsonObject { ["page"] = pdfPage + 1 });
                    link.WordPage = pdfPage;
                }
            }
        }

        /// <summary>The page filling most of WordCraft's view (0-based), from ui.inspect's screen
        /// rectangles; -1 when it can't be told.</summary>
        private async Task<int> WordVisiblePageAsync()
        {
            var reply = await Task.Run(() => WordRequest(new JsonObject { ["method"] = "ui.inspect", ["params"] = new JsonObject() },
                                                         TimeSpan.FromSeconds(3)));
            var r = reply?["result"];
            if (r?["canvasRect"] is not JsonArray canvas || canvas.Count < 4 || r["pages"] is not JsonArray pages) return -1;
            double top = canvas[1]?.GetValue<double>() ?? 0, bottom = top + (canvas[3]?.GetValue<double>() ?? 0);
            double left = canvas[0]?.GetValue<double>() ?? 0, right = left + (canvas[2]?.GetValue<double>() ?? 0);
            int best = -1;
            double bestArea = 0;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] is not JsonArray p || p.Count < 4) continue;
                double x = p[0]?.GetValue<double>() ?? 0, y = p[1]?.GetValue<double>() ?? 0;
                double w = p[2]?.GetValue<double>() ?? 0, h = p[3]?.GetValue<double>() ?? 0;
                double ow = Math.Min(right, x + w) - Math.Max(left, x), oh = Math.Min(bottom, y + h) - Math.Max(top, y);
                if (ow <= 0 || oh <= 0) continue;
                if (ow * oh > bestArea) { bestArea = ow * oh; best = i; }
            }
            return best;
        }

        /// <summary>Exports WordCraft's document to a fresh temp PDF; null on failure.</summary>
        private async Task<string?> ExportWordAsync(bool quiet = false)
        {
            string dir = Path.Combine(App.TempDir, "linked", Guid.NewGuid().ToString("N"));
            var info = (await WordExecuteAsync("file.info", new JsonObject()))?["result"];
            string? docPath = info?["path"]?.ToString();
            string pdf = Path.Combine(dir, SafeFileStem(string.IsNullOrEmpty(docPath) ? null : Path.GetFileNameWithoutExtension(docPath)) + ".pdf");
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex) { if (!quiet) ShowWordError(Loc("Str_Studio_ExportFailed"), ex.Message); return null; }
            if (!quiet) SetStatus(Loc("Str_Studio_Exporting"));
            var reply = await WordExecuteAsync("file.exportPdf", new JsonObject { ["path"] = pdf }, TimeSpan.FromMinutes(2));
            if (reply?["ok"]?.GetValue<bool>() == true && File.Exists(pdf)) return pdf;
            if (!quiet) ShowWordError(Loc("Str_Studio_ExportFailed"), reply?["error"]?.ToString());
            return null;
        }

        /// <summary>Swaps the linked tab (in front) to a re-exported PDF in place: same tab, same
        /// page, zoom and scroll, and the KillerPDF markup kept on its pages. The page images and
        /// the undo history belong to the old file and go with it.</summary>
        private void ReplaceLinkedPdf(EditLink link, string pdf)
        {
            if (!ReferenceEquals(_active, link.Session) || _doc is null) { link.PendingPdf = pdf; return; }
            int page = ActiveViewer.CurrentPageIndex;
            double? scrollH = ActiveViewer.PagePreviewPanel?.HorizontalOffset;
            double? scrollV = ActiveViewer.PagePreviewPanel?.VerticalOffset;
            var markup = _annotations.ToDictionary(kv => kv.Key, kv => kv.Value);
            string? oldFile = _currentFile;

            PdfWorkingDocument fresh;
            try { fresh = PdfWorkingDocument.Open(pdf); }
            catch (Exception ex)
            {
                SetStatus(string.Format(Loc("Str_Linked_SyncFailed"), ex.Message));
                return;
            }
            try { _doc.Close(); } catch { }
            _doc = fresh;
            _currentFile = pdf;
            CloseEngineDocumentSession();
            int pageCount = EnsureEngineDocumentSession().Pages.Count;

            // Markup: pages that are still there keep theirs; a shorter document parks the rest.
            foreach (var (p, list) in link.Stash.ToList())
                if (p < pageCount && !markup.ContainsKey(p)) { markup[p] = list; link.Stash.Remove(p); }
            _annotations.Clear();
            foreach (var (p, list) in markup)
            {
                if (p < pageCount) _annotations[p] = list;
                else link.Stash[p] = list;
            }

            _continuousLinks.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            _pageRotations.Clear();
            _renderDims.Clear();
            _formTextValues.Clear();
            _formChoiceValues.Clear();
            _formCheckValues.Clear();
            _formFontSizes.Clear();
            _formRadioValues.Clear();
            Search.ClearPageResults();
            Controls.PdfViewer.InvalidateRenderCacheExt(_active);
            _renderedPrimaryPage = -1;
            BootstrapDocumentView(Math.Clamp(page, 0, Math.Max(0, pageCount - 1)), autoFit: false, restoreFitMode: true,
                                  restoreHorizontalOffset: scrollH, restoreVerticalOffset: scrollV);
            MarkDirty();
            link.PdfPage = ActiveViewer.CurrentPageIndex;
            if (oldFile is not null && oldFile.StartsWith(Path.Combine(App.TempDir, "linked"), StringComparison.OrdinalIgnoreCase))
                TryDeleteDir(Path.GetDirectoryName(oldFile));
            SetStatus(Loc("Str_Linked_Synced"));
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>Before something else is opened into WordCraft: its unsaved document is saved,
        /// discarded or kept (Cancel). WordCraft's own file.open would drop it without asking.</summary>
        private async Task<bool> ConfirmReplaceWordDocAsync()
        {
            var info = (await WordExecuteAsync("file.info", new JsonObject()))?["result"];
            if (info?["dirty"]?.GetValue<bool>() != true) return true;
            string? path = info["path"]?.ToString();
            string name = string.IsNullOrEmpty(path) ? Loc("Str_Studio_Untitled") : Path.GetFileName(path);
            var res = KillerDialog.Show(this, string.Format(Loc("Str_Studio_SaveDocx"), name),
                Loc("Str_Dlg_AppTitle"), MessageBoxButton.YesNoCancel, MessageBoxImage.Warning,
                defaultResult: MessageBoxResult.Cancel);
            if (res == MessageBoxResult.Cancel) return false;
            if (res == MessageBoxResult.No) return true;
            var saved = await WordExecuteAsync("file.save", new JsonObject(), TimeSpan.FromSeconds(30));
            // No file name yet: WordCraft has its Save As open. Let the user finish it first.
            return saved?["ok"]?.GetValue<bool>() == true && saved?["result"]?["saved"]?.GetValue<bool>() != false;
        }

        /// <summary>"report.pdf" + ".docx" → "report.docx", or "report (2).docx" when taken.</summary>
        internal static string UniqueSibling(string path, string extension)
        {
            string dir = Path.GetDirectoryName(path) ?? "";
            string stem = SafeFileStem(Path.GetFileNameWithoutExtension(path));
            string candidate = Path.Combine(dir, stem + extension);
            for (int n = 2; File.Exists(candidate) && n < 1000; n++)
                candidate = Path.Combine(dir, $"{stem} ({n}){extension}");
            return candidate;
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { }
            TryDeleteDir(Path.GetDirectoryName(path));
        }

        private static void TryDeleteDir(string? dir)
        {
            try { if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); }
            catch { }
            try { if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharpCore.Drawing;

namespace KillerPDF
{
    public partial class MainWindow
    {
        // The document's current stamp configuration (one spec drives page numbers and/or a watermark).
        // Reopening the Stamp tool edits this; Apply rebuilds _stamps from it. Stamps live on their own
        // layer, painted BELOW annotations in RenderAllAnnotations.
        private StampSpec? _docStampSpec;
        private readonly Dictionary<int, List<StampInstance>> _stamps = [];
        // Per-page rendered stamp bounds (render-dim/canvas space) so a double-click can hit-test a stamp and
        // reopen the editor. Repopulated every RenderStamps; the stamp visuals themselves stay non-hit-testable.
        private readonly Dictionary<int, List<Rect>> _stampHitRects = [];

        private void ToolStamp_Click(object sender, RoutedEventArgs e)
        {
            if (_doc is null) { SetStatus(Loc("Str_Tf_NoRender")); return; }
            OpenStampTool();
        }

        // Opens the Stamp window seeded with the current spec (so it edits the existing stamps).
        private void OpenStampTool()
        {
            if (_doc is null) return;
            int pageIdx = PageList.SelectedIndex < 0 ? 0 : PageList.SelectedIndex;
            var src = RenderPageBitmap(pageIdx, 1100, BurnPageAnnotationsToTemp(pageIdx));
            if (src is null) { SetStatus(Loc("Str_Tf_NoRender")); return; }

            var page = _doc.Pages[pageIdx];
            var (pwpt, phpt) = EffectivePageSize(page);
            var win = new StampWindow(this, src, pwpt, phpt, _doc.PageCount, pageIdx, _docStampSpec,
                idx =>   // page-render callback for the preview stepper
                {
                    var s = RenderPageBitmap(idx, 1100, BurnPageAnnotationsToTemp(idx));
                    var (w, h) = EffectivePageSize(_doc!.Pages[idx]);
                    return (s, w, h);
                });
            win.ShowDialog();
            if (win.Applied) ApplyStampSpec(win.Result);
        }

        private void ApplyStampSpec(StampSpec spec)
        {
            _docStampSpec = (spec.NumbersEnabled || spec.WmEnabled) ? spec : null;
            UpdateStampIndicator();
            RebuildStamps();
            RerenderAllVisiblePages();
            MarkDirty();
            int pages = 0;
            foreach (var kv in _stamps) if (kv.Value.Count > 0) pages++;
            SetStatus(string.Format(Loc("Str_Stamp_Applied"), pages));
        }

        // Regenerates the per-page stamp instances from the active spec.
        private void RebuildStamps()
        {
            _stamps.Clear();
            if (_docStampSpec is null || _doc is null) return;
            int n = _doc.PageCount;

            if (_docStampSpec.NumbersEnabled)
                foreach (int p in StampPageRange(_docStampSpec.NumRange, n))
                    AddStamp(p, StampKind.PageNumber);

            if (_docStampSpec.WmEnabled)
                foreach (int p in StampPageRange(_docStampSpec.WmRange, n))
                    AddStamp(p, StampKind.Watermark);
        }

        // Keeps the Stamp toolbar button showing a persistent "hovered" (gray) background while the document
        // has active stamps, as a subtle indicator. Cleared when there are no stamps.
        private void UpdateStampIndicator()
        {
            if (ToolStampBtn is null) return;
            if (_docStampSpec is not null)
                ToolStampBtn.SetResourceReference(Control.BackgroundProperty, "BgHover");
            else
                ToolStampBtn.ClearValue(Control.BackgroundProperty);
        }

        private void AddStamp(int page, StampKind kind)
        {
            if (!_stamps.TryGetValue(page, out var list)) { list = []; _stamps[page] = list; }
            list.Add(new StampInstance { PageIndex = page, Kind = kind, Spec = _docStampSpec! });
        }

        private void RerenderAllVisiblePages()
        {
            if (_doc is null) return;
            // Re-render every currently-mapped page (the primary tile plus all multi-page tiles), so stamps
            // show on every visible page in Grid / Two-Page / Continuous, not just the selected one.
            foreach (int p in new List<int>(_pages.Keys)) RenderAllAnnotations(p);
        }

        // Painted by RenderAllAnnotations onto the page's annotation canvas, BEFORE the annotations, so
        // stamps sit visually beneath them. Coordinates are the same 2048-based render-dim space the page
        // numbers used originally, so placement matches the rest of the annotation layer.
        private void RenderStamps(int pageIndex)
        {
            _stampHitRects[pageIndex] = [];   // reset; repopulated below as stamps render
            if (_docStampSpec is null || _doc is null) return;
            if (!_stamps.TryGetValue(pageIndex, out var list) || list.Count == 0) return;

            var (rdW, rdH, _, phpt) = StampRenderDims(pageIndex);
            if (rdW <= 0 || rdH <= 0) return;
            double mx = rdW * 0.05, my = rdH * 0.04;
            var spec = _docStampSpec;

            // First page that carries a number, so numbering starts at StartNumber there.
            int firstNumPage = -1;
            if (spec.NumbersEnabled)
                foreach (int p in StampPageRange(spec.NumRange, _doc.PageCount)) { firstNumPage = p; break; }

            foreach (var st in list)
            {
                if (st.Kind == StampKind.Watermark) RenderWatermark(spec, pageIndex, rdW, rdH, phpt, mx, my);
                else RenderPageNumber(spec, pageIndex, firstNumPage, rdW, rdH, phpt, mx, my);
            }
        }

        private void RenderPageNumber(StampSpec spec, int pageIndex, int firstNumPage, double rdW, double rdH, double phpt, double mx, double my)
        {
            double fontCanvas = spec.NumFontPt * rdH / Math.Max(1, phpt);
            int number = spec.StartNumber + Math.Max(0, pageIndex - Math.Max(0, firstNumPage));
            string text = (string.IsNullOrEmpty(spec.Format) ? "{n}" : spec.Format)
                .Replace("{n}", number.ToString())
                .Replace("{N}", (_doc?.PageCount ?? 1).ToString());
            if (text.Length == 0) return;

            var tb = new TextBlock { Text = text, FontFamily = UiKit.UiFont, FontSize = Math.Max(1, fontCanvas), Foreground = new SolidColorBrush(spec.NumColor), IsHitTestVisible = false };
            var sz = MeasureEl(tb);
            int posH = spec.NumPosH;
            double x, y;
            if (posH < 0)   // custom position (center as a fraction of the page)
            {
                double cx = spec.NumCustomX;
                if (spec.NumMirror && (pageIndex % 2 == 1)) cx = 1 - cx;   // mirror flips the x-fraction
                x = cx * rdW - sz.Width / 2;
                y = spec.NumCustomY * rdH - sz.Height / 2;
            }
            else
            {
                // Mirror: on alternating pages flip left<->right so the number sits on the outer edge of a spread.
                if (spec.NumMirror && posH != 1 && (pageIndex % 2 == 1)) posH = 2 - posH;
                x = posH == 0 ? mx : posH == 2 ? rdW - sz.Width - mx : (rdW - sz.Width) / 2;
                y = spec.NumPosV == 0 ? my : spec.NumPosV == 1 ? (rdH - sz.Height) / 2 : rdH - sz.Height - my;
            }
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            _activeCanvas.Children.Add(tb);
            if (_stampHitRects.TryGetValue(pageIndex, out var rects)) rects.Add(new Rect(x, y, sz.Width, sz.Height));
        }

        private void RenderWatermark(StampSpec spec, int pageIndex, double rdW, double rdH, double phpt, double mx, double my)
        {
            FrameworkElement el;
            double w, h;
            if (spec.WmIsImage && !string.IsNullOrEmpty(spec.WmImagePath) && System.IO.File.Exists(spec.WmImagePath))
            {
                BitmapImage? bmp = LoadImageFile(spec.WmImagePath!);
                if (bmp is null) return;
                w = rdW * 0.5 * spec.WmScale;
                h = w * bmp.PixelHeight / Math.Max(1, bmp.PixelWidth);
                el = new Image { Source = bmp, Width = w, Height = h, Opacity = spec.WmOpacity, Stretch = Stretch.Fill, IsHitTestVisible = false };
            }
            else
            {
                if (string.IsNullOrEmpty(spec.WmText)) return;
                double fontCanvas = spec.WmFontPt * rdH / Math.Max(1, phpt);
                var tb = new TextBlock { Text = spec.WmText, FontFamily = new FontFamily(string.IsNullOrWhiteSpace(spec.WmFont) ? "Segoe UI" : spec.WmFont), FontWeight = FontWeights.Bold, FontSize = Math.Max(1, fontCanvas), Foreground = new SolidColorBrush(spec.WmColor), Opacity = spec.WmOpacity, IsHitTestVisible = false };
                var sz = MeasureEl(tb);
                w = sz.Width; h = sz.Height;
                el = tb;
            }

            double x, y;
            if (spec.WmPosH < 0)   // custom position (center as a fraction of the page)
            {
                x = spec.WmCustomX * rdW - w / 2;
                y = spec.WmCustomY * rdH - h / 2;
            }
            else
            {
                x = spec.WmPosH == 0 ? mx : spec.WmPosH == 2 ? rdW - w - mx : (rdW - w) / 2;
                y = spec.WmPosV == 0 ? my : spec.WmPosV == 1 ? (rdH - h) / 2 : rdH - h - my;
            }
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            el.RenderTransform = new RotateTransform(-spec.WmAngle);
            Canvas.SetLeft(el, x);
            Canvas.SetTop(el, y);
            _activeCanvas.Children.Add(el);
            if (_stampHitRects.TryGetValue(pageIndex, out var rects)) rects.Add(new Rect(x, y, w, h));
        }

        // True if a point (render-dim/canvas space) falls on a rendered stamp on this page - used to reopen
        // the Stamp Pages editor on double-click.
        private bool StampHitTest(int pageIndex, Point pos)
        {
            if (_stampHitRects.TryGetValue(pageIndex, out var rects))
                foreach (var r in rects) if (r.Contains(pos)) return true;
            return false;
        }

        // (rdW, rdH) in the 2048-based render-dim space; phpt/pwpt the page size in points (rotation-aware).
        private (double rdW, double rdH, double pwpt, double phpt) StampRenderDims(int pageIndex)
        {
            if (_doc is null) return (0, 0, 0, 0);
            double pw = _doc.Pages[pageIndex].Width.Point;
            double ph = _doc.Pages[pageIndex].Height.Point;
            if (_pageRotations.TryGetValue(pageIndex, out int rot) && (rot == 90 || rot == 270)) (pw, ph) = (ph, pw);
            double maxDim = Math.Max(1, Math.Max(pw, ph));
            return (2048.0 * pw / maxDim, 2048.0 * ph / maxDim, pw, ph);
        }

        private static Size MeasureEl(FrameworkElement el)
        {
            el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return el.DesiredSize;
        }

        private static BitmapImage? LoadImageFile(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        // 0-based page indices for a 1-based "1-3,5" range string ("" = all pages).
        private static IEnumerable<int> StampPageRange(string range, int pageCount)
        {
            var set = new SortedSet<int>();
            if (string.IsNullOrWhiteSpace(range))
            {
                for (int i = 0; i < pageCount; i++) set.Add(i);
                return set;
            }
            foreach (var part in range.Split(','))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                int dash = p.IndexOf('-');
                if (dash > 0)
                {
                    if (int.TryParse(p[..dash].Trim(), out int a) && int.TryParse(p[(dash + 1)..].Trim(), out int b))
                        for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++) if (i >= 1 && i <= pageCount) set.Add(i - 1);
                }
                else if (int.TryParse(p, out int single) && single >= 1 && single <= pageCount) set.Add(single - 1);
            }
            return set;
        }

        // ---- Export: burn the stamp layer into the PDF (below annotations) on save/flatten ----

        // Draws the active stamps into the doc via XGraphics, in PDF-point space. Called BEFORE
        // DrawAnnotationsOnDocument at each save site so stamps sit beneath annotations.
        private void DrawStampsOnDocument(int? onlyPage = null) => DrawStampsIntoDoc(_doc, _docStampSpec, onlyPage);

        // Static so the print flow can run it on a background thread against a throwaway document copy.
        private static void DrawStampsIntoDoc(PdfSharpCore.Pdf.PdfDocument? doc, StampSpec? spec, int? onlyPage = null)
        {
            if (doc is null || spec is null || (!spec.NumbersEnabled && !spec.WmEnabled)) return;
            int n = doc.PageCount;

            HashSet<int> numPages = spec.NumbersEnabled ? [.. StampPageRange(spec.NumRange, n)] : [];
            HashSet<int> wmPages  = spec.WmEnabled     ? [.. StampPageRange(spec.WmRange, n)]  : [];
            int firstNumPage = int.MaxValue;
            foreach (int p in numPages) if (p < firstNumPage) firstNumPage = p;
            if (firstNumPage == int.MaxValue) firstNumPage = 0;

            // Pre-fade the watermark image once (reused for all pages).
            XImage? wmImg = null;
            if (spec.WmEnabled && spec.WmIsImage && !string.IsNullOrEmpty(spec.WmImagePath) && System.IO.File.Exists(spec.WmImagePath))
                wmImg = LoadStampImage(spec.WmImagePath!, spec.WmOpacity);

            for (int i = 0; i < n && i < doc.PageCount; i++)
            {
                if (onlyPage.HasValue && i != onlyPage.Value) continue;
                bool doNum = numPages.Contains(i);
                bool doWm = wmPages.Contains(i);
                if (!doNum && !doWm) continue;

                var page = doc.Pages[i];
                double pw = page.Width.Point, ph = page.Height.Point;
                double mx = pw * 0.05, my = ph * 0.04;
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                if (doWm)  DrawWatermarkPdf(gfx, spec, pw, ph, mx, my, wmImg);   // watermark first (underneath)
                if (doNum) DrawNumberPdf(gfx, spec, i, firstNumPage, n, pw, ph, mx, my);
            }
        }

        private static void DrawNumberPdf(XGraphics gfx, StampSpec spec, int pageIndex, int firstNumPage, int total, double pw, double ph, double mx, double my)
        {
            int number = spec.StartNumber + Math.Max(0, pageIndex - firstNumPage);
            string text = (string.IsNullOrEmpty(spec.Format) ? "{n}" : spec.Format)
                .Replace("{n}", number.ToString()).Replace("{N}", total.ToString());
            if (text.Length == 0) return;

            var font = new XFont("Segoe UI", Math.Max(1, spec.NumFontPt), XFontStyle.Regular);
            var c = spec.NumColor;
            var brush = new XSolidBrush(XColor.FromArgb(255, c.R, c.G, c.B));
            var size = gfx.MeasureString(text, font);
            double w = size.Width, h = size.Height;

            int posH = spec.NumPosH;
            double x, y;
            if (posH < 0)   // custom
            {
                double cx = spec.NumCustomX;
                if (spec.NumMirror && (pageIndex % 2 == 1)) cx = 1 - cx;
                x = cx * pw - w / 2; y = spec.NumCustomY * ph - h / 2;
            }
            else
            {
                if (spec.NumMirror && posH != 1 && (pageIndex % 2 == 1)) posH = 2 - posH;
                x = posH == 0 ? mx : posH == 2 ? pw - w - mx : (pw - w) / 2;
                y = spec.NumPosV == 0 ? my : spec.NumPosV == 1 ? (ph - h) / 2 : ph - h - my;
            }
            gfx.DrawString(text, font, brush, new XRect(x, y, w, h), XStringFormats.TopLeft);
        }

        private static void DrawWatermarkPdf(XGraphics gfx, StampSpec spec, double pw, double ph, double mx, double my, XImage? img)
        {
            double w, h;
            XFont? font = null;
            if (spec.WmIsImage)
            {
                if (img is null) return;
                w = pw * 0.5 * spec.WmScale;
                h = w * img.PixelHeight / Math.Max(1, img.PixelWidth);
            }
            else
            {
                if (string.IsNullOrEmpty(spec.WmText)) return;
                try { font = new XFont(string.IsNullOrWhiteSpace(spec.WmFont) ? "Segoe UI" : spec.WmFont, Math.Max(1, spec.WmFontPt), XFontStyle.Bold); }
                catch { font = new XFont("Segoe UI", Math.Max(1, spec.WmFontPt), XFontStyle.Bold); }
                var size = gfx.MeasureString(spec.WmText, font);
                w = size.Width; h = size.Height;
            }

            double cx, cy;
            if (spec.WmPosH < 0) { cx = spec.WmCustomX * pw; cy = spec.WmCustomY * ph; }
            else
            {
                cx = spec.WmPosH == 0 ? mx + w / 2 : spec.WmPosH == 2 ? pw - mx - w / 2 : pw / 2;
                cy = spec.WmPosV == 0 ? my + h / 2 : spec.WmPosV == 1 ? ph / 2 : ph - my - h / 2;
            }

            var state = gfx.Save();
            gfx.TranslateTransform(cx, cy);
            gfx.RotateTransform(-spec.WmAngle);
            if (spec.WmIsImage)
            {
                gfx.DrawImage(img, -w / 2, -h / 2, w, h);
            }
            else
            {
                byte a = (byte)Math.Max(0, Math.Min(255, spec.WmOpacity * 255));
                var c = spec.WmColor;
                gfx.DrawString(spec.WmText, font, new XSolidBrush(XColor.FromArgb(a, c.R, c.G, c.B)), new XRect(-w / 2, -h / 2, w, h), XStringFormats.Center);
            }
            gfx.Restore(state);
        }

        // Loads a watermark image as an XImage, pre-faded to the requested opacity (PdfSharpCore has no
        // per-draw image opacity, so we bake it into the pixels).
        private static XImage? LoadStampImage(string path, double opacity)
        {
            try
            {
                byte[] bytes;
                if (opacity >= 0.999)
                {
                    bytes = System.IO.File.ReadAllBytes(path);
                }
                else
                {
                    using var src = System.Drawing.Image.FromFile(path);
                    using var bmp = new System.Drawing.Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = (float)Math.Max(0, Math.Min(1, opacity)) };
                        using var ia = new System.Drawing.Imaging.ImageAttributes();
                        ia.SetColorMatrix(cm);
                        g.DrawImage(src, new System.Drawing.Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, System.Drawing.GraphicsUnit.Pixel, ia);
                    }
                    using var ms = new System.IO.MemoryStream();
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    bytes = ms.ToArray();
                }
                return XImage.FromStream(() => new System.IO.MemoryStream(bytes));
            }
            catch { return null; }
        }
    }
}

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
        // ============================================================
        // Selection
        // ============================================================

        // Resolve the active theme's "SelectionAccent" color: a per-theme color picked to stay
        // readable on the white PDF page (Accent is white in several themes, and AccentBorder is a
        // pale cream that washes out on white). Falls back to brand green.
        private Color AccentColor()
            => TryFindResource("SelectionAccent") is SolidColorBrush b ? b.Color : Color.FromRgb(30, 165, 76);
        private SolidColorBrush AccentBrush(byte alpha = 255)
        {
            var c = AccentColor();
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }
        // A darker shade of the accent, used for a cover's selection chrome and its in-edit outline so a
        // cover reads as distinct from the lighter accent on the text box stacked over it.
        private SolidColorBrush DarkerAccentBrush(byte alpha = 255)
        {
            var c = AccentColor();
            return new SolidColorBrush(Color.FromArgb(alpha, (byte)(c.R * 0.6), (byte)(c.G * 0.6), (byte)(c.B * 0.6)));
        }

        private void SelectAllText()
        {
            if (_currentFile is null) return;
            int pageIdx = PageList.SelectedIndex;
            if (pageIdx < 0) return;

            try
            {
                using var pigDoc = PdfPigDoc.Open(_currentFile);
                if (pageIdx >= pigDoc.NumberOfPages) return;
                var page = pigDoc.GetPage(pageIdx + 1);
                _selectedText = WordsToText(page.GetWords());
                if (string.IsNullOrWhiteSpace(_selectedText))
                {
                    SetStatus(Loc("Str_St_NoTextOnPage"));
                    return;
                }
                Clipboard.SetText(_selectedText);
                // Visual feedback: highlight entire canvas
                ClearTextSelection();
                _selectRect = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.FromArgb(30, 74, 130, 255)),
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 74, 130, 255)),
                    StrokeThickness = 1,
                    Width = _annotationCanvas.Width,
                    Height = _annotationCanvas.Height,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(_selectRect, 0);
                Canvas.SetTop(_selectRect, 0);
                _annotationCanvas.Children.Add(_selectRect);
                SetStatus($"Selected all text - copied to clipboard");
            }
            catch (Exception ex)
            {
                SetStatus($"Select all error: {ex.Message}");
            }
        }

        private void CopySelectedText()
        {
            if (!string.IsNullOrEmpty(_selectedText))
            {
                Clipboard.SetText(_selectedText);
                SetStatus($"Copied to clipboard");
            }
            else
            {
                SetStatus(Loc("Str_St_NoTextSelected"));
            }
        }

        private void ClearTextSelection()
        {
            if (_selectRect is not null)
            {
                _activeCanvas.Children.Remove(_selectRect);
                _selectRect = null;
            }
            ClearFlowHighlightVisuals();
            _selectedText = null;
        }

        // ============================================================
        // Flowing (macOS Preview-style) text selection
        // ============================================================
        // Rather than a single drag rectangle, a Select-tool drag over real text highlights whole
        // words in reading order between the drag's start and current point - one bar per line, like
        // selecting text in Preview/a browser/a word processor - instead of a plain marquee box.

        // Builds the word/line geometry for the given page once, in that page's own canvas
        // coordinates (the inverse of the PDF->canvas scale ExtractTextFromRegion uses), so the drag's
        // live highlight and its final commit both read from memory instead of reparsing the PDF.
        private void PrepareTextSelectionData(int pageIdx)
        {
            _selWords = null; _selLines = null; _selLineBounds = null; _selFlat = null;
            _selWordsPageIdx = pageIdx;
            if (_currentFile is null || pageIdx < 0 || !_renderDims.TryGetValue(pageIdx, out var dims)) return;

            try
            {
                var (renderW, renderH) = dims;
                using var pigDoc = PdfPigDoc.Open(_currentFile);
                if (pageIdx >= pigDoc.NumberOfPages) return;
                var page = pigDoc.GetPage(pageIdx + 1);

                double pdfW = page.Width, pdfH = page.Height;
                if (pdfW <= 0 || pdfH <= 0) return;
                double cx = renderW / pdfW, cy = renderH / pdfH;

                var words = new List<(Rect Bounds, string Text)>();
                foreach (var w in page.GetWords())
                {
                    if (string.IsNullOrWhiteSpace(w.Text)) continue;
                    var bb = w.BoundingBox;
                    double left = bb.Left * cx, right = bb.Right * cx;
                    // PDF origin is bottom-left; canvas origin is top-left, so Y flips.
                    double top = (pdfH - bb.Top) * cy, bottom = (pdfH - bb.Bottom) * cy;
                    if (right <= left || bottom <= top) continue;
                    words.Add((new Rect(left, top, right - left, bottom - top), w.Text));
                }
                if (words.Count == 0) { _selWords = words; _selLines = []; _selLineBounds = []; _selFlat = []; return; }

                // Group into lines by vertical overlap (a word joins the first line whose bounds its
                // midpoint falls inside), then sort each line left to right and lines top to bottom.
                var byTop = Enumerable.Range(0, words.Count).OrderBy(i => words[i].Bounds.Top);
                var lines = new List<List<int>>();
                var lineBounds = new List<Rect>();
                foreach (var i in byTop)
                {
                    double midY = words[i].Bounds.Top + words[i].Bounds.Height / 2;
                    int match = -1;
                    for (int li = 0; li < lines.Count; li++)
                        if (midY >= lineBounds[li].Top && midY <= lineBounds[li].Bottom) { match = li; break; }
                    if (match < 0) { lines.Add([i]); lineBounds.Add(words[i].Bounds); }
                    else { lines[match].Add(i); lineBounds[match] = Rect.Union(lineBounds[match], words[i].Bounds); }
                }
                foreach (var line in lines) line.Sort((a, b) => words[a].Bounds.Left.CompareTo(words[b].Bounds.Left));
                var lineOrder = Enumerable.Range(0, lines.Count).OrderBy(li => lineBounds[li].Top).ToList();
                var sortedLines = lineOrder.Select(li => lines[li]).ToList();
                var sortedBounds = lineOrder.Select(li => lineBounds[li]).ToList();

                var flat = new List<(int WordIdx, int LineIdx)>();
                for (int li = 0; li < sortedLines.Count; li++)
                    foreach (var wi in sortedLines[li]) flat.Add((wi, li));

                _selWords = words;
                _selLines = sortedLines;
                _selLineBounds = sortedBounds;
                _selFlat = flat;
            }
            catch { _selWords = null; _selLines = null; _selLineBounds = null; _selFlat = null; }
        }

        // Maps a raw canvas point to the nearest word in reading order: nearest line vertically (or the
        // line whose band contains the point), then within that line the word whose horizontal center is
        // closest - the same "snap to nearest character/word" feel as clicking into ordinary text.
        private int NearestFlatIndex(Point p)
        {
            if (_selFlat is null || _selLineBounds is null || _selFlat.Count == 0) return -1;

            int bestLine = 0; double bestLineDist = double.MaxValue;
            for (int li = 0; li < _selLineBounds.Count; li++)
            {
                var r = _selLineBounds[li];
                double d = p.Y < r.Top ? r.Top - p.Y : p.Y > r.Bottom ? p.Y - r.Bottom : 0;
                if (d < bestLineDist) { bestLineDist = d; bestLine = li; }
            }

            var idxs = _selLines![bestLine];
            int bestWordPos = 0; double bestWordDist = double.MaxValue;
            for (int wi = 0; wi < idxs.Count; wi++)
            {
                var r = _selWords![idxs[wi]].Bounds;
                double d = Math.Abs(p.X - (r.Left + r.Width / 2));
                if (d < bestWordDist) { bestWordDist = d; bestWordPos = wi; }
            }

            int flatStart = 0;
            for (int li = 0; li < bestLine; li++) flatStart += _selLines[li].Count;
            return flatStart + bestWordPos;
        }

        // Redraws the live selection highlight (one rounded bar per spanned line) for the word range
        // [lo,hi] in reading order, replacing whatever was drawn for the previous drag position.
        private void RenderFlowHighlight(int lo, int hi, Canvas targetCanvas)
        {
            ClearFlowHighlightVisuals();
            if (_selFlat is null || lo < 0 || hi < lo) return;
            _textHighlightCanvas = targetCanvas;

            int i = Math.Max(0, lo);
            int end = Math.Min(hi, _selFlat.Count - 1);
            while (i <= end)
            {
                int line = _selFlat[i].LineIdx;
                double left = double.MaxValue, right = double.MinValue;
                var lb = _selLineBounds![line];
                while (i <= end && _selFlat[i].LineIdx == line)
                {
                    var r = _selWords![_selFlat[i].WordIdx].Bounds;
                    left = Math.Min(left, r.Left);
                    right = Math.Max(right, r.Right);
                    i++;
                }
                var rect = new Rectangle
                {
                    Fill = AccentBrush(60),
                    Width = Math.Max(2, right - left),
                    Height = lb.Height + 2,
                    RadiusX = 2,
                    RadiusY = 2,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(rect, left);
                Canvas.SetTop(rect, lb.Top - 1);
                targetCanvas.Children.Add(rect);
                _textHighlightRects.Add(rect);
            }
        }

        private void ClearFlowHighlightVisuals()
        {
            if (_textHighlightCanvas is not null)
                foreach (var r in _textHighlightRects) _textHighlightCanvas.Children.Remove(r);
            _textHighlightRects.Clear();
            _textHighlightCanvas = null;
        }

        // Live drag update: recomputes and redraws the highlighted range from the cached word geometry
        // (no PDF I/O). Returns false when there's no text on this page, so the caller can fall back to
        // the plain marquee box (e.g. a scanned page with no text layer, or an annotation drag).
        private bool UpdateFlowSelection(Point canvasStart, Point canvasCurrent, Canvas targetCanvas)
        {
            if (_selFlat is null || _selFlat.Count == 0) return false;
            int a = NearestFlatIndex(canvasStart);
            int b = NearestFlatIndex(canvasCurrent);
            if (a < 0 || b < 0) return false;
            RenderFlowHighlight(Math.Min(a, b), Math.Max(a, b), targetCanvas);
            return true;
        }

        // Mouse-up commit: turns the final [lo,hi] word range into clipboard text, preserving line
        // breaks and word spacing in reading order. Uses the same cached geometry as the live preview,
        // so the copied text always matches exactly what was highlighted.
        private void FinalizeFlowSelection(Point canvasStart, Point canvasEnd)
        {
            if (_selFlat is null || _selFlat.Count == 0) { ClearTextSelection(); return; }
            int a = NearestFlatIndex(canvasStart);
            int b = NearestFlatIndex(canvasEnd);
            if (a < 0 || b < 0) { ClearTextSelection(); return; }
            int lo = Math.Min(a, b), hi = Math.Max(a, b);

            var sb = new System.Text.StringBuilder();
            int i = lo;
            while (i <= hi)
            {
                int line = _selFlat[i].LineIdx;
                var lineWords = new List<string>();
                while (i <= hi && _selFlat[i].LineIdx == line)
                {
                    lineWords.Add(_selWords![_selFlat[i].WordIdx].Text);
                    i++;
                }
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(string.Join(" ", lineWords));
            }

            _selectedText = sb.ToString();
            if (string.IsNullOrWhiteSpace(_selectedText))
            {
                ClearTextSelection();
                SetStatus(Loc("Str_St_NoTextInSelection"));
                return;
            }
            Clipboard.SetText(_selectedText);
            SetStatus($"Copied {hi - lo + 1} word(s) to clipboard");
        }

        private void ExtractTextFromRegion(int pageIdx, Rect canvasBounds)
        {
            if (_currentFile is null || pageIdx < 0) return;
            if (!_renderDims.ContainsKey(pageIdx)) return;

            try
            {
                var (renderW, renderH) = _renderDims[pageIdx];

                using var pigDoc = PdfPigDoc.Open(_currentFile);
                if (pageIdx >= pigDoc.NumberOfPages) return;
                var page = pigDoc.GetPage(pageIdx + 1); // PdfPig is 1-based

                double pdfW = page.Width;
                double pdfH = page.Height;
                double sx = pdfW / renderW;
                double sy = pdfH / renderH;

                // Convert canvas rect to PDF coordinates (flip Y - PDF origin is bottom-left)
                double pdfLeft = canvasBounds.Left * sx;
                double pdfRight = canvasBounds.Right * sx;
                double pdfTop = pdfH - (canvasBounds.Top * sy);
                double pdfBottom = pdfH - (canvasBounds.Bottom * sy);
                // pdfTop > pdfBottom because of Y flip
                double pdfMinY = Math.Min(pdfTop, pdfBottom);
                double pdfMaxY = Math.Max(pdfTop, pdfBottom);

                var words = page.GetWords()
                    .Where(w =>
                    {
                        var bb = w.BoundingBox;
                        double cx = (bb.Left + bb.Right) / 2;
                        double cy = (bb.Bottom + bb.Top) / 2;
                        return cx >= pdfLeft && cx <= pdfRight && cy >= pdfMinY && cy <= pdfMaxY;
                    })
                    .ToList();

                if (words.Count == 0)
                {
                    SetStatus(Loc("Str_St_NoTextInSelection"));
                    ClearTextSelection();
                    return;
                }

                _selectedText = WordsToText(words);

                Clipboard.SetText(_selectedText);
                int wordCount = words.Count;
                SetStatus($"Copied {wordCount} word(s) to clipboard");
            }
            catch (Exception ex)
            {
                SetStatus($"Text extraction error: {ex.Message}");
                ClearTextSelection();
            }
        }
    }
}

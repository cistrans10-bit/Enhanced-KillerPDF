using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics;

namespace KillerPDF.Services
{
    /// <summary>
    /// PDF → editable Word document, for editing a PDF in DOCX mode (Shell/LinkedEditing.cs).
    ///
    /// Page for page: every PDF page becomes its own section with the same page size, so page N of
    /// the PDF is page N of the document and the two can be shown and scrolled side by side. On each
    /// page the letters are gathered into lines (one baseline each) and the lines into paragraphs,
    /// top to bottom, written as reflowable paragraphs that keep their position (spacing before, indents),
    /// alignment, line pitch and character formatting (font, size, bold, italic, colour). Pictures
    /// are anchored to the page at their exact position, outside the text flow (behind the text
    /// where they overlap it, as backgrounds do), so they never push text onto another page.
    /// A line with wide gaps (table rows, tabbed columns) becomes one paragraph with a tab stop at
    /// each column, and text the flow can't hold (beside other text, or in the footer band) is
    /// pinned to the page in a frame. Table borders, shading and other vector drawings are not
    /// rebuilt, and vertical text is left out.
    /// </summary>
    internal static class PdfToDocx
    {
        private const double MinMargin = 18, MaxMargin = 144;   // points

        /// <summary>Converts <paramref name="pdfPath"/> to a .docx at <paramref name="docxPath"/>.
        /// Returns the number of pages written.</summary>
        internal static int Convert(string pdfPath, string docxPath, string? password = null)
        {
            var options = new ParsingOptions { UseLenientParsing = true };
            if (!string.IsNullOrEmpty(password)) options.Password = password;
            using var pdf = PdfDocument.Open(pdfPath, options);

            var body = new StringBuilder();
            var media = new List<(string name, byte[] bytes)>();
            int pageCount = pdf.NumberOfPages;
            _nextShapeId = 1;
            for (int i = 1; i <= pageCount; i++)
            {
                Page page;
                try { page = pdf.GetPage(i); }
                catch { body.Append(EmptyPage(612, 792, i < pageCount)); continue; }
                body.Append(ConvertPage(page, media, last: i == pageCount));
            }
            if (pageCount == 0) body.Append("<w:p/>").Append(SectPr(612, 792, 72, 72, 72, 72, null));

            WritePackage(docxPath, body.ToString(), media);
            return pageCount;
        }

        // ── One page ──────────────────────────────────────────────────────

        private sealed record ImageItem(PdfRectangle Bounds, string Rel);

        private static string ConvertPage(Page page, List<(string name, byte[] bytes)> media, bool last)
        {
            double w = page.Width, h = page.Height;

            // Text: lines (letters on one baseline), then paragraphs, top to bottom.
            var paras = new List<TPara>();
            try { paras = BuildParas(BuildLines(page)); }
            catch { /* a page whose text can't be analysed still gets its pictures and its size */ }

            // Pictures, anchored to the page where they were.
            var images = new List<ImageItem>();
            try
            {
                foreach (var img in page.GetImages())
                {
                    if (img.BoundingBox.Width < 2 || img.BoundingBox.Height < 2) continue;
                    byte[]? bytes = null;
                    string ext = "png";
                    if (img.TryGetPng(out var png) && png is { Length: > 0 }) bytes = png;
                    else if (img.RawBytes.Length > 3 && img.RawBytes[0] == 0xFF && img.RawBytes[1] == 0xD8)
                    {
                        bytes = img.RawBytes.ToArray();
                        ext = "jpeg";
                    }
                    if (bytes is null) continue;
                    media.Add(($"image{media.Count + 1}.{ext}", bytes));
                    images.Add(new ImageItem(img.BoundingBox, "rIdImg" + media.Count));
                }
            }
            catch { }

            // Vector drawing (rules, table fills, cards, backgrounds, diagrams): each filled or
            // stroked path becomes a shape anchored to the page behind the text, with the PDF's
            // own geometry, so white text on a dark panel still has its panel.
            var shapes = new StringBuilder();
            try
            {
                int count = 0;
                foreach (var path in page.Paths)
                {
                    if (path.IsClipping || !(path.IsFilled || path.IsStroked)) continue;
                    if (++count > MaxShapesPerPage) break;
                    shapes.Append(Shape(path, h));
                }
            }
            catch { }

            if (paras.Count == 0 && images.Count == 0 && shapes.Length == 0) return EmptyPage(w, h, !last);

            double left = MinMargin, right = MinMargin, top = MinMargin;
            if (paras.Count > 0)
            {
                left = Clamp(paras.Min(p => p.Left), MinMargin, MaxMargin);
                right = Clamp(w - paras.Max(p => p.Right), MinMargin, MaxMargin);
                top = Clamp(paras.Min(p => h - p.First - BaselineShare * p.Pitch), MinMargin, MaxMargin);
            }
            // Small, and never below the page's lowest text (footers sit near the edge), so a page
            // whose text runs a little longer than in the PDF still fits on it.
            double bottom = paras.Count > 0 ? Clamp(paras.Min(p => p.Bottom) - 4, 0, MinMargin) : MinMargin;
            if (left + right > w - 72) { left = right = Math.Max(MinMargin, (w - 72) / 2); }
            double textWidth = w - left - right;

            // The page's pictures and drawing ride in its first paragraph, positioned against the
            // page. Stacking: page-sized pictures (backgrounds) at the back, then the drawing, then
            // the other pictures.
            var anchors = new StringBuilder();
            foreach (var im in images)
            {
                bool large = im.Bounds.Width * im.Bounds.Height > w * h * 0.25;
                bool behind = large || paras.Any(p => Overlaps(p.Bounds, im.Bounds));
                anchors.Append(Anchor(im, h, behind, large ? 1 : 3));
            }
            anchors.Append(shapes);

            var sb = new StringBuilder();
            if (paras.Count == 0)
            {
                // Pictures and drawing only: one empty paragraph holds them.
                sb.Append("<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\"/>");
                if (!last) sb.Append(SectPr(w, h, top, right, bottom, left, "nextPage"));
                sb.Append("</w:pPr>").Append(anchors).Append("</w:p>");
                if (last) sb.Append(SectPr(w, h, top, right, bottom, left, null));
                return sb.ToString();
            }

            // Vertical placement goes baseline to baseline, so nothing drifts down the page. With an
            // exact line height L, a line's baseline sits about BaselineShare·L below the line's top:
            // the space before a paragraph is the PDF's baseline distance less what the two
            // paragraphs' own lines already take up.
            //
            // Paragraphs one text flow can't hold are pinned to the page instead, as frames at their
            // exact place: one that starts higher than the flow has reached (text beside other text
            // on lines of its own) and the page's footer band, the bottom eighth of the page where
            // running footers, page numbers and logos sit — pinned, a font that sets a little larger
            // than the PDF's can't push them onto a page of their own. Everything else stays one flow
            // of paragraphs, so it edits and reflows like any document.
            double footerBand = h * 0.125;
            double prevLast = 0, prevPitch = 0;
            bool flowStarted = false;
            for (int i = 0; i < paras.Count; i++)
            {
                var para = paras[i];
                double first = para.First, pitch = para.Pitch;
                string? anchorRuns = i == 0 ? anchors.ToString() : null;
                double before = flowStarted
                    ? (prevLast - first) - (1 - BaselineShare) * prevPitch - BaselineShare * pitch
                    : (h - first) - top - BaselineShare * pitch;
                bool footer = para.Bottom < footerBand && i > 0;
                bool overlap = flowStarted && before < -1;
                if (footer || overlap)
                {
                    var frame = new FrameBox(para.Left, (h - first) - BaselineShare * pitch, para.Right - para.Left);
                    sb.Append(ParagraphXml(para, 0, left, textWidth, anchorRuns, frame));
                    continue;
                }
                sb.Append(ParagraphXml(para, Math.Max(0, before), left, textWidth, anchorRuns, null));
                prevLast = para.Last;
                prevPitch = pitch;
                flowStarted = true;
            }
            // The section ends in a hairline empty paragraph of its own, so the break never rides
            // in a framed paragraph and adds no height worth speaking of.
            sb.Append("<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"20\" w:lineRule=\"exact\"/>");
            if (!last) sb.Append(SectPr(w, h, top, right, bottom, left, "nextPage"));
            sb.Append("<w:rPr><w:sz w:val=\"2\"/></w:rPr></w:pPr></w:p>");
            if (last) sb.Append(SectPr(w, h, top, right, bottom, left, null));
            return sb.ToString();
        }

        private static bool Overlaps(PdfRectangle a, PdfRectangle b)
            => a.Left < b.Right && b.Left < a.Right && a.Bottom < b.Top && b.Bottom < a.Top;

        private static string EmptyPage(double w, double h, bool sectionBreak)
            => sectionBreak
                ? "<w:p><w:pPr>" + SectPr(w, h, 72, 72, 72, 72, "nextPage") + "</w:pPr></w:p>"
                : "<w:p/>" + SectPr(w, h, 72, 72, 72, 72, null);

        // ── Text: words, lines, paragraphs ────────────────────────────────

        /// <summary>Where a line's baseline sits in an exact-height line, as a share of the height.</summary>
        private const double BaselineShare = 0.8;

        private sealed class TWord
        {
            public readonly StringBuilder Text = new();
            public Style Style = DefaultStyle;
            public double Left, Right;
        }

        /// <summary>A run of words on one line with ordinary spaces between them. A line with a
        /// wide gap (table cells, tabbed columns, a label and its value) has several.</summary>
        private sealed class TSeg
        {
            public readonly List<TWord> Words = new();
            public double Left => Words.Count > 0 ? Words[0].Left : 0;
            public double Right => Words.Count > 0 ? Words[^1].Right : 0;
        }

        private sealed class TLine
        {
            public double Base, Size;
            public readonly List<TSeg> Segs = new();
            public double Left => Segs.Count > 0 ? Segs[0].Left : 0;
            public double Right => Segs.Count > 0 ? Segs[^1].Right : 0;
            public bool IsRow => Segs.Count > 1;
            public string FirstWord => Segs.Count > 0 && Segs[0].Words.Count > 0 ? Segs[0].Words[0].Text.ToString() : "";
        }

        private sealed class TPara
        {
            public readonly List<TLine> Lines = new();
            public double First => Lines[0].Base;
            public double Last => Lines[^1].Base;
            public double Left => Lines.Min(l => l.Left);
            public double Right => Lines.Max(l => l.Right);
            public double Size => Lines.Max(l => l.Size);
            public double Bottom => Last - Lines[^1].Size * 0.25;
            public PdfRectangle Bounds => new(Left, Bottom, Right, First + Lines[0].Size * 0.8);

            /// <summary>Line height: the median baseline distance, or 1.2 × the font size for a
            /// single line.</summary>
            public double Pitch
            {
                get
                {
                    if (Lines.Count > 1)
                    {
                        var gaps = new List<double>();
                        for (int i = 1; i < Lines.Count; i++) gaps.Add(Lines[i - 1].Base - Lines[i].Base);
                        gaps.Sort();
                        double g = gaps[gaps.Count / 2];
                        if (g > 2 && g < 240) return g;
                    }
                    return Clamp(Size * 1.2, 4, 240);
                }
            }
        }

        private static double SizeOf(Letter l)
            => Clamp(l.PointSize > 0.5 ? l.PointSize : l.FontSize, 1, 400);

        /// <summary>Letters → lines: letters whose baselines lie within a third of their size of the
        /// line's top baseline, left to right, split into words (a space, or a gap) and segments
        /// (a gap of two ems or more). The page's own space characters are kept as word breaks, so
        /// "The KillerPDF" never comes out as one word.</summary>
        private static List<TLine> BuildLines(Page page)
        {
            var letters = page.Letters
                .Where(l => l.TextOrientation == TextOrientation.Horizontal && !string.IsNullOrEmpty(l.Value))
                .OrderByDescending(l => l.StartBaseLine.Y)
                .ToList();
            var scales = FontScales(letters);
            var lines = new List<TLine>();
            var cluster = new List<Letter>();
            double clusterTop = double.NaN;
            foreach (var l in letters)
            {
                if (cluster.Count > 0 && clusterTop - l.StartBaseLine.Y > Math.Max(1, SizeOf(l) * 0.3))
                {
                    AddLine(lines, cluster, scales);
                    cluster = new List<Letter>();
                }
                if (cluster.Count == 0) clusterTop = l.StartBaseLine.Y;
                cluster.Add(l);
            }
            AddLine(lines, cluster, scales);
            return lines;
        }

        /// <summary>A size correction per embedded font the reader won't have. Such a font is
        /// shown in another typeface, and fonts draw their letters at very different sizes within
        /// the same point size (one font's capitals are half the em, another's three quarters).
        /// Measured from the PDF's own capitals, the size is scaled so the stand-in's capitals come
        /// out as tall as the original's, assuming a typical cap height of 0.7 em.</summary>
        private static Dictionary<string, double> FontScales(List<Letter> letters)
        {
            var scales = new Dictionary<string, double>();
            foreach (var g in letters.Where(l => l.Value.Length == 1 && l.Value[0] is >= 'A' and <= 'Z' and not 'J' and not 'Q')
                                     .GroupBy(l => l.FontName ?? ""))
            {
                if (KnownFonts.ContainsKey(CleanFontName(g.Key).Replace(" ", ""))) continue;
                var ratios = g.Select(l => l.BoundingBox.Height / SizeOf(l)).Where(r => r > 0.2 && r < 1.2).OrderBy(r => r).ToList();
                if (ratios.Count < 2) continue;
                double ratio = ratios[ratios.Count / 2];
                if (ratio < 0.62 || ratio > 0.78) scales[g.Key] = Clamp(ratio / 0.7, 0.5, 1.4);
            }
            return scales;
        }

        private static void AddLine(List<TLine> lines, List<Letter> cluster, Dictionary<string, double> scales)
        {
            var visible = cluster.Where(l => !string.IsNullOrWhiteSpace(l.Value)).ToList();
            if (visible.Count == 0) return;
            double baseY = visible.GroupBy(l => Math.Round(l.StartBaseLine.Y * 2) / 2)
                                  .OrderByDescending(g => g.Count()).First().First().StartBaseLine.Y;
            var sizes = visible.Select(SizeOf).OrderBy(s => s).ToList();
            var line = new TLine { Base = baseY, Size = sizes[sizes.Count / 2] };

            TSeg? seg = null;
            TWord? word = null;
            double prevRight = double.NaN, prevLeft = double.NaN;
            string prevValue = "";
            bool space = false;
            foreach (var l in cluster.OrderBy(l => Math.Min(l.StartBaseLine.X, l.EndBaseLine.X)))
            {
                if (string.IsNullOrWhiteSpace(l.Value)) { space = true; continue; }
                double x0 = Math.Min(l.StartBaseLine.X, l.EndBaseLine.X), x1 = Math.Max(l.StartBaseLine.X, l.EndBaseLine.X);
                if (x1 - x0 < 0.01) x1 = x0 + Math.Max(0.01, l.BoundingBox.Width);
                // The same glyph printed twice a hair apart (a "fake bold") is one letter.
                if (l.Value == prevValue && Math.Abs(x0 - prevLeft) < 0.6) continue;
                double em = Math.Max(SizeOf(l), line.Size);
                double gap = double.IsNaN(prevRight) ? 0 : x0 - prevRight;
                if (seg is null || gap > em * 2)
                {
                    seg = new TSeg();
                    line.Segs.Add(seg);
                    word = null;
                }
                if (word is null || space || gap > em * 0.22)
                {
                    word = new TWord { Style = StyleOf(l, scales.TryGetValue(l.FontName ?? "", out var k) ? k : 1), Left = x0 };
                    seg.Words.Add(word);
                }
                word.Text.Append(l.Value);
                word.Right = Math.Max(word.Right, x1);
                prevRight = Math.Max(double.IsNaN(prevRight) ? x1 : prevRight, x1);
                prevLeft = x0;
                prevValue = l.Value;
                space = false;
            }
            if (line.Segs.Count > 0) lines.Add(line);
        }

        private static readonly Regex ListMarker = new(@"^([•◦▪▫■□●○‣⁃–—\-\*]|\(?[0-9]{1,3}[.)]|\(?[a-zA-Z][.)])$", RegexOptions.Compiled);

        /// <summary>Lines → paragraphs: consecutive lines of one size, at the paragraph's line
        /// pitch, starting where its lines start (or centred, or flush right, with them), until a
        /// short line ends it or a list item starts a new one. A line with several segments is a
        /// paragraph of its own (its segments become tab stops).</summary>
        private static List<TPara> BuildParas(List<TLine> lines)
        {
            var paras = new List<TPara>();
            TPara? cur = null;
            foreach (var line in lines)
            {
                if (cur is not null && Continues(cur, line)) { cur.Lines.Add(line); continue; }
                cur = new TPara();
                cur.Lines.Add(line);
                paras.Add(cur);
            }
            return paras;
        }

        private static bool Continues(TPara p, TLine line)
        {
            var prev = p.Lines[^1];
            if (prev.IsRow || line.IsRow) return false;
            double size = Math.Max(line.Size, prev.Size);
            if (Math.Abs(line.Size - prev.Size) > 0.15 * size) return false;
            double gap = prev.Base - line.Base;
            if (gap < size * 0.9 || gap > size * 1.8) return false;
            if (p.Lines.Count >= 2)
            {
                double pitch = p.Lines[^2].Base - prev.Base;
                if (Math.Abs(gap - pitch) > Math.Max(1, pitch * 0.12)) return false;
            }
            if (line.Left >= prev.Right || prev.Left >= line.Right) return false;
            if (ListMarker.IsMatch(line.FirstWord)) return false;

            double tol = Math.Max(3, size * 0.3);
            bool sameLeft = Math.Abs(line.Left - prev.Left) <= tol;
            bool sameCentre = Math.Abs((line.Left + line.Right) / 2 - (prev.Left + prev.Right) / 2) <= tol;
            bool sameRight = Math.Abs(line.Right - prev.Right) <= tol;
            if (p.Lines.Count >= 2)
            {
                if (!sameLeft && !sameCentre && !sameRight) return false;
                // A left-aligned line well short of the paragraph's right edge ended it.
                if (sameLeft && !sameCentre && prev.Right < p.Right - size * 3) return false;
            }
            else
            {
                // The second line may sit left of the first (a first-line indent) or right of it
                // (a hanging indent after a list marker), by a few ems at most.
                if (!sameCentre && !sameRight && Math.Abs(line.Left - prev.Left) > size * 4) return false;
                // A first line much shorter than the next one ended its paragraph (a heading).
                if (!sameCentre && prev.Right < line.Right - size * 3) return false;
            }
            return true;
        }

        /// <summary>A paragraph pinned to the page (w:framePr): its left edge, the top of its
        /// first line, and its width, in points from the page's top-left corner.</summary>
        private sealed record FrameBox(double X, double Y, double Width);

        private static string ParagraphXml(TPara para, double before, double marginLeft, double textWidth, string? anchors, FrameBox? frame)
        {
            var lines = para.Lines;
            double pitch = para.Pitch;
            double minL = para.Left, maxR = para.Right;

            // Alignment from the line edges (the last line of a justified paragraph is short).
            const double tol = 2.5;
            var body = lines.Count > 1 ? lines.Take(lines.Count - 1).ToList() : lines;
            bool lefts = lines.Count == 1 || lines.Skip(1).All(l => Math.Abs(l.Left - lines[1].Left) < tol);
            bool rights = body.All(l => Math.Abs(l.Right - maxR) < tol);
            double centre = (minL + maxR) / 2;
            bool centred = lines.All(l => Math.Abs((l.Left + l.Right) / 2 - centre) < tol);
            string jc = "left";
            if (lines.Count > 1 && centred && !lefts) jc = "center";
            else if (lines.Count > 1 && rights && !lefts) jc = "right";
            else if (lines.Count > 2 && rights && lefts) jc = "both";
            else if (lines.Count == 1 && !para.Lines[0].IsRow && frame is null)
            {
                double pageCentre = marginLeft + textWidth / 2;
                if (Math.Abs(centre - pageCentre) < 6 && maxR - minL < textWidth * 0.8) jc = "center";
            }

            // Indents keep the paragraph where it was on the page (a frame is placed by its own x).
            // The right indent leaves a little slack, so a substituted font that sets a hair wider
            // doesn't wrap a line more than the PDF did.
            double rightEdge = marginLeft + textWidth;
            double indLeft = jc == "center" || frame is not null ? 0 : Math.Max(0, minL - marginLeft);
            double indRight = jc == "center" || frame is not null ? 0 : Math.Max(0, rightEdge - maxR - (maxR - minL) * 0.03 - 1);
            if (lines.Count == 1 && jc == "left") indRight = 0;   // a single line may grow when edited
            double firstLine = lines.Count > 1 && jc is "left" or "both" ? lines[0].Left - lines.Skip(1).Min(l => l.Left) : 0;

            var p = new StringBuilder("<w:p><w:pPr>");
            if (frame is not null)
            {
                // A little wider than the PDF's text, for the same reason as the slack above.
                p.Append("<w:framePr w:w=\"").Append(Tw(frame.Width * 1.04 + 2)).Append("\" w:x=\"").Append(Tw(frame.X))
                 .Append("\" w:y=\"").Append(Tw(Math.Max(0, frame.Y))).Append("\" w:hAnchor=\"page\" w:vAnchor=\"page\" w:wrap=\"none\"/>");
            }
            // Tab stops at each segment of a row, measured from the text column's (or frame's) left.
            if (lines.Count == 1 && lines[0].IsRow)
            {
                double origin = frame?.X ?? marginLeft;
                p.Append("<w:tabs>");
                foreach (var seg in lines[0].Segs.Skip(1))
                    p.Append("<w:tab w:val=\"left\" w:pos=\"").Append(Tw(seg.Left - origin)).Append("\"/>");
                p.Append("</w:tabs>");
            }
            p.Append("<w:spacing w:before=\"").Append(Tw(before)).Append("\" w:after=\"0\" w:line=\"")
             .Append(Tw(pitch)).Append("\" w:lineRule=\"exact\"/>");
            if (indLeft > 0.5 || indRight > 0.5 || Math.Abs(firstLine) > 0.5)
            {
                p.Append("<w:ind w:left=\"").Append(Tw(indLeft)).Append("\" w:right=\"").Append(Tw(indRight)).Append('"');
                if (firstLine > 0.5) p.Append(" w:firstLine=\"").Append(Tw(firstLine)).Append('"');
                else if (firstLine < -0.5) p.Append(" w:hanging=\"").Append(Tw(-firstLine)).Append('"');
                p.Append("/>");
            }
            p.Append("<w:jc w:val=\"").Append(jc).Append("\"/>");
            p.Append("</w:pPr>");
            if (!string.IsNullOrEmpty(anchors)) p.Append(anchors);

            // Runs: consecutive words with the same look become one run. Words join with a space,
            // segments with a tab, lines with a space (a word hyphenated across lines rejoins).
            Style? current = null;
            var text = new StringBuilder();
            void Flush()
            {
                if (current is null || text.Length == 0) return;
                p.Append(Run(current, text.ToString()));
                text.Clear();
            }
            string sep = "";
            foreach (var line in lines)
            {
                for (int s = 0; s < line.Segs.Count; s++)
                {
                    if (s > 0) sep = "\t";
                    foreach (var word in line.Segs[s].Words)
                    {
                        string t = word.Text.ToString();
                        if (word.Style != current) { text.Append(sep); sep = ""; Flush(); current = word.Style; }
                        text.Append(sep).Append(t);
                        sep = " ";
                    }
                }
                // "bound-" + "ary" → "boundary"; "well-" + "Known" keeps its hyphen.
                if (text.Length > 1 && text[^1] == '-' && char.IsLetter(text[^2]) && line != lines[^1])
                {
                    var next = lines[lines.IndexOf(line) + 1].FirstWord;
                    if (next.Length > 0 && char.IsLower(next[0])) { text.Length--; sep = ""; }
                }
            }
            Flush();
            p.Append("</w:p>");
            return p.ToString();
        }

        private sealed record Style(string Font, double Size, bool Bold, bool Italic, string Color);

        private static readonly Style DefaultStyle = new("Times New Roman", 11, false, false, "000000");

        private static Style StyleOf(Letter l, double scale)
        {
            string raw = l.FontName ?? "";
            bool bold = Regex.IsMatch(raw, "Bold|Black|Heavy|Semibold|Demi", RegexOptions.IgnoreCase);
            bool italic = Regex.IsMatch(raw, "Italic|Oblique", RegexOptions.IgnoreCase);
            try
            {
                var d = l.FontDetails;
                if (d is not null) { bold |= d.IsBold; italic |= d.IsItalic; }
            }
            catch { }
            double size = Math.Round(Clamp(SizeOf(l) * scale, 1, 400) * 2) / 2;
            string color = "000000";
            try
            {
                var (r, g, b) = l.Color.ToRGBValues();
                color = $"{(int)Math.Round(r * 255):X2}{(int)Math.Round(g * 255):X2}{(int)Math.Round(b * 255):X2}";
            }
            catch { }
            return new Style(CleanFontName(raw), size, bold, italic, color);
        }

        private static readonly Dictionary<string, string> KnownFonts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arial"] = "Arial", ["ArialMT"] = "Arial", ["Helvetica"] = "Arial", ["HelveticaNeue"] = "Arial",
            ["TimesNewRoman"] = "Times New Roman", ["TimesNewRomanPS"] = "Times New Roman", ["Times"] = "Times New Roman",
            ["CourierNew"] = "Courier New", ["CourierNewPS"] = "Courier New", ["Courier"] = "Courier New",
            ["Calibri"] = "Calibri", ["Cambria"] = "Cambria", ["Georgia"] = "Georgia", ["Verdana"] = "Verdana",
            ["SegoeUI"] = "Segoe UI", ["TrebuchetMS"] = "Trebuchet MS", ["Garamond"] = "Garamond",
            ["BookAntiqua"] = "Book Antiqua", ["Symbol"] = "Symbol",
        };

        /// <summary>"ABCDEF+TimesNewRomanPS-BoldMT" → "Times New Roman".</summary>
        internal static string CleanFontName(string raw)
        {
            string n = Regex.Replace(raw ?? "", @"^[A-Z]{6}\+", "");
            n = Regex.Replace(n, @"[-,](Bold|Italic|Oblique|Regular|Roman|Medium|Light|Black|Heavy|Semibold|Demi|Book|Condensed|BoldItalic|BoldOblique|It|Bd|BdIt)+.*$", "", RegexOptions.IgnoreCase);
            n = Regex.Replace(n, @"(PSMT|MT|PS)$", "");
            if (KnownFonts.TryGetValue(n.Replace(" ", ""), out var known)) return known;
            return n.Length is > 0 and < 64 ? n : "Times New Roman";
        }

        private static string Run(Style s, string text)
        {
            var r = new StringBuilder("<w:r><w:rPr>");
            string font = Xml(s.Font);
            r.Append("<w:rFonts w:ascii=\"").Append(font).Append("\" w:hAnsi=\"").Append(font)
             .Append("\" w:cs=\"").Append(font).Append("\" w:eastAsia=\"").Append(font).Append("\"/>");
            if (s.Bold) r.Append("<w:b/>");
            if (s.Italic) r.Append("<w:i/>");
            if (s.Color != "000000") r.Append("<w:color w:val=\"").Append(s.Color).Append("\"/>");
            int half = (int)Math.Round(s.Size * 2);
            r.Append("<w:sz w:val=\"").Append(half).Append("\"/><w:szCs w:val=\"").Append(half).Append("\"/>");
            r.Append("</w:rPr>");
            var parts = text.Split('\t');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) r.Append("<w:tab/>");
                if (parts[i].Length > 0) r.Append("<w:t xml:space=\"preserve\">").Append(Xml(parts[i])).Append("</w:t>");
            }
            r.Append("</w:r>");
            return r.ToString();
        }

        // ── Pictures ──────────────────────────────────────────────────────

        private static string Anchor(ImageItem im, double pageHeight, bool behind, int layer)
        {
            int id = _nextShapeId++;
            long cx = (long)(Math.Max(1, im.Bounds.Width) * 12700), cy = (long)(Math.Max(1, im.Bounds.Height) * 12700);
            long x = (long)(Math.Max(0, im.Bounds.Left) * 12700), y = (long)(Math.Max(0, pageHeight - im.Bounds.Top) * 12700);
            return new StringBuilder("<w:r><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"")
                .Append(layer * 100000 + id).Append("\" behindDoc=\"").Append(behind ? 1 : 0)
                .Append("\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\"><wp:simplePos x=\"0\" y=\"0\"/>")
                .Append("<wp:positionH relativeFrom=\"page\"><wp:posOffset>").Append(x).Append("</wp:posOffset></wp:positionH>")
                .Append("<wp:positionV relativeFrom=\"page\"><wp:posOffset>").Append(y).Append("</wp:posOffset></wp:positionV>")
                .Append("<wp:extent cx=\"").Append(cx).Append("\" cy=\"").Append(cy).Append("\"/>")
                .Append("<wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:wrapNone/>")
                .Append("<wp:docPr id=\"").Append(id).Append("\" name=\"Picture ").Append(id).Append("\"/><wp:cNvGraphicFramePr/>")
                .Append("<a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">")
                .Append("<a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">")
                .Append("<pic:pic xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">")
                .Append("<pic:nvPicPr><pic:cNvPr id=\"").Append(id).Append("\" name=\"Picture ").Append(id).Append("\"/><pic:cNvPicPr/></pic:nvPicPr>")
                .Append("<pic:blipFill><a:blip r:embed=\"").Append(im.Rel).Append("\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>")
                .Append("<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"").Append(cx).Append("\" cy=\"").Append(cy).Append("\"/></a:xfrm>")
                .Append("<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic>")
                .Append("</wp:anchor></w:drawing></w:r>").ToString();
        }

        // ── Vector drawing ────────────────────────────────────────────────

        private const int MaxShapesPerPage = 2000;
        [ThreadStatic] private static int _nextShapeId;

        /// <summary>One PDF path as a DrawingML custom-geometry shape (wps:wsp) anchored to the page
        /// at the path's bounds, behind the text, with its fill and stroke colours.</summary>
        private static string Shape(PdfPath path, double pageHeight)
        {
            var bounds = path.GetBoundingRectangle();
            if (bounds is not { } b) return "";
            double lw = path.IsStroked ? Clamp(path.LineWidth, 0.25, 24) : 0;
            double x0 = b.Left, top = b.Top;
            double wPt = Math.Max(b.Width, 0.01), hPt = Math.Max(b.Height, 0.01);
            long cx = Emu(wPt), cy = Emu(hPt);

            var geo = new StringBuilder();
            string P(PdfPoint pt) => "<a:pt x=\"" + Emu(Clamp(pt.X - x0, 0, wPt)).ToString(CultureInfo.InvariantCulture)
                                     + "\" y=\"" + Emu(Clamp(top - pt.Y, 0, hPt)).ToString(CultureInfo.InvariantCulture) + "\"/>";
            int commands = 0;
            foreach (var sub in path)
            {
                foreach (var cmd in sub.Commands)
                {
                    if (++commands > 5000) break;
                    switch (cmd)
                    {
                        case PdfSubpath.Move m: geo.Append("<a:moveTo>").Append(P(m.Location)).Append("</a:moveTo>"); break;
                        case PdfSubpath.Line l:
                            geo.Append("<a:lnTo>").Append(P(l.To)).Append("</a:lnTo>");
                            break;
                        case PdfSubpath.CubicBezierCurve c:
                            geo.Append("<a:cubicBezTo>").Append(P(c.FirstControlPoint)).Append(P(c.SecondControlPoint)).Append(P(c.EndPoint)).Append("</a:cubicBezTo>");
                            break;
                        case PdfSubpath.BezierCurve q:
                            foreach (var seg in q.ToLines(8)) geo.Append("<a:lnTo>").Append(P(seg.To)).Append("</a:lnTo>");
                            break;
                        case PdfSubpath.Close: geo.Append("<a:close/>"); break;
                    }
                }
            }
            if (geo.Length == 0) return "";

            string fill = path.IsFilled && ColorHex(path.FillColor) is { } f
                ? "<a:solidFill><a:srgbClr val=\"" + f + "\"/></a:solidFill>" : "<a:noFill/>";
            string line = path.IsStroked && ColorHex(path.StrokeColor) is { } st
                ? "<a:ln w=\"" + Emu(lw) + "\"><a:solidFill><a:srgbClr val=\"" + st + "\"/></a:solidFill></a:ln>" : "<a:ln><a:noFill/></a:ln>";
            int id = _nextShapeId++;
            return new StringBuilder("<w:r><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"")
                .Append(200000 + id).Append("\" behindDoc=\"1\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\"><wp:simplePos x=\"0\" y=\"0\"/>")
                .Append("<wp:positionH relativeFrom=\"page\"><wp:posOffset>").Append(Emu(Math.Max(0, x0))).Append("</wp:posOffset></wp:positionH>")
                .Append("<wp:positionV relativeFrom=\"page\"><wp:posOffset>").Append(Emu(Math.Max(0, pageHeight - top))).Append("</wp:posOffset></wp:positionV>")
                .Append("<wp:extent cx=\"").Append(cx).Append("\" cy=\"").Append(cy).Append("\"/>")
                .Append("<wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:wrapNone/>")
                .Append("<wp:docPr id=\"").Append(id).Append("\" name=\"Shape ").Append(id).Append("\"/><wp:cNvGraphicFramePr/>")
                .Append("<a:graphic xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\">")
                .Append("<a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\">")
                .Append("<wps:wsp><wps:cNvSpPr/><wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"").Append(cx).Append("\" cy=\"").Append(cy).Append("\"/></a:xfrm>")
                .Append("<a:custGeom><a:avLst/><a:gdLst/><a:ahLst/><a:cxnLst/><a:rect l=\"0\" t=\"0\" r=\"r\" b=\"b\"/><a:pathLst>")
                .Append("<a:path w=\"").Append(cx).Append("\" h=\"").Append(cy).Append("\">").Append(geo).Append("</a:path></a:pathLst></a:custGeom>")
                .Append(fill).Append(line)
                .Append("</wps:spPr><wps:bodyPr/></wps:wsp></a:graphicData></a:graphic></wp:anchor></w:drawing></w:r>").ToString();
        }

        private static long Emu(double points) => (long)Math.Round(Clamp(points, 0, 31680) * 12700);

        private static string? ColorHex(UglyToad.PdfPig.Graphics.Colors.IColor? c)
        {
            if (c is null) return "000000";
            try
            {
                var (r, g, b) = c.ToRGBValues();
                return $"{(int)Math.Round(Clamp(r, 0, 1) * 255):X2}{(int)Math.Round(Clamp(g, 0, 1) * 255):X2}{(int)Math.Round(Clamp(b, 0, 1) * 255):X2}";
            }
            catch { return null; }
        }

        // ── Sections and the package ──────────────────────────────────────

        private static string SectPr(double w, double h, double top, double right, double bottom, double left, string? type)
        {
            var s = new StringBuilder("<w:sectPr>");
            if (type is not null) s.Append("<w:type w:val=\"").Append(type).Append("\"/>");
            s.Append("<w:pgSz w:w=\"").Append(Tw(w)).Append("\" w:h=\"").Append(Tw(h)).Append('"');
            if (w > h) s.Append(" w:orient=\"landscape\"");
            s.Append("/><w:pgMar w:top=\"").Append(Tw(top)).Append("\" w:right=\"").Append(Tw(right))
             .Append("\" w:bottom=\"").Append(Tw(bottom)).Append("\" w:left=\"").Append(Tw(left))
             .Append("\" w:header=\"0\" w:footer=\"0\" w:gutter=\"0\"/></w:sectPr>");
            return s.ToString();
        }

        private static void WritePackage(string path, string body, List<(string name, byte[] bytes)> media)
        {
            string tmp = path + ".part";
            using (var fs = File.Create(tmp))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                void Entry(string name, string content)
                {
                    using var s = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
                    s.Write(content);
                }
                Entry("[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Default Extension=\"png\" ContentType=\"image/png\"/>" +
                    "<Default Extension=\"jpeg\" ContentType=\"image/jpeg\"/>" +
                    "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                    "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>" +
                    "</Types>");
                Entry("_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                    "</Relationships>");
                var rels = new StringBuilder(
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
                for (int i = 0; i < media.Count; i++)
                    rels.Append("<Relationship Id=\"rIdImg").Append(i + 1)
                        .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/")
                        .Append(media[i].name).Append("\"/>");
                rels.Append("</Relationships>");
                Entry("word/_rels/document.xml.rels", rels.ToString());
                Entry("word/styles.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                    "<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Times New Roman\" w:hAnsi=\"Times New Roman\" w:cs=\"Times New Roman\" w:eastAsia=\"Times New Roman\"/>" +
                    "<w:sz w:val=\"22\"/><w:szCs w:val=\"22\"/></w:rPr></w:rPrDefault>" +
                    "<w:pPrDefault><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>" +
                    "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/><w:qFormat/></w:style>" +
                    "</w:styles>");
                Entry("word/document.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" " +
                    "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" " +
                    "xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\">" +
                    "<w:body>" + body + "</w:body></w:document>");
                foreach (var (name, bytes) in media)
                {
                    using var s = zip.CreateEntry("word/media/" + name, CompressionLevel.NoCompression).Open();
                    s.Write(bytes, 0, bytes.Length);
                }
            }
            File.Move(tmp, path, overwrite: true);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string Tw(double points)
            => ((long)Math.Round(Clamp(points, 0, 31680) * 20)).ToString(CultureInfo.InvariantCulture);

        private static double Clamp(double v, double lo, double hi)
            => double.IsNaN(v) ? lo : Math.Min(hi, Math.Max(lo, v));

        private static string Xml(string s)
        {
            var b = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&': b.Append("&amp;"); break;
                    case '<': b.Append("&lt;"); break;
                    case '>': b.Append("&gt;"); break;
                    case '"': b.Append("&quot;"); break;
                    default:
                        // XML 1.0 forbids most control characters; PDFs sometimes carry them.
                        if (c >= 0x20 || c is '\t' or '\n' or '\r') b.Append(c);
                        break;
                }
            }
            return b.ToString();
        }
    }
}

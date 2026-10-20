using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KillerPDF.Controls
{
    // Text tool: drag out a text box, and fit its font to the box.
    //
    //   - Click (or a drag shorter than DragThreshold) places the default box at the pointer,
    //     exactly as before.
    //   - Drag draws the box: it gets the dragged width AND height, and the font is the chosen
    //     size, reduced only if a line of that size would not fit the box.
    //   - Resizing a box by a corner (while typing, or a placed text annotation) re-fits the
    //     font: the largest size at which the wrapped text still fits inside the new width and
    //     height, so a bigger box means bigger text and a smaller box smaller text. Hold Shift
    //     while dragging to resize the box only and keep the font size.
    //   - Typing into a fixed-size box shrinks the font when the text would overflow, and grows
    //     it back (never past the box's own size) when text is deleted.
    public partial class PdfViewer
    {
        /// <summary>Below this many canvas units in both directions a press is a click, not a drag.</summary>
        private const double TextDragThreshold = 8;
        private const double MinFitFont = 2;

        private Rectangle? _textDragRect;
        private Point _textDragStart;
        private int _textDragPage = -1;

        /// <summary>The size the live box's text wants to be: what it was placed or last resized
        /// at. Typing shrinks below this to avoid overflow and grows back up to it, never past.</summary>
        private double _activeTextBaseFont;

        // ── Drag to create ────────────────────────────────────────────────

        private void BeginTextBoxDrag(Point pos, int pageIdx)
        {
            _textDragStart = pos;
            _textDragPage = pageIdx;
            _gestureCanvas = _activeCanvas;
            _gesturePage = pageIdx;
            _textDragRect = new Rectangle
            {
                Fill = AccentBrush(18),
                Stroke = AccentBrush(220),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                IsHitTestVisible = false,
                Width = 0,
                Height = 0,
            };
            TextEditorLayerFor(_activeCanvas).Children.Add(_textDragRect);
            Canvas.SetLeft(_textDragRect, pos.X);
            Canvas.SetTop(_textDragRect, pos.Y);
            _activeCanvas.CaptureMouse();
        }

        /// <summary>Follows the pointer while a box is being drawn. False when no draw is in progress.</summary>
        private bool UpdateTextBoxDrag(Point pos, MouseEventArgs e)
        {
            if (_textDragRect is null) return false;
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                // The release was lost (app in the background); finish with what was drawn.
                FinishTextBoxDrag();
                return true;
            }
            var p = ClampPointToPage(_textDragPage, pos);
            Canvas.SetLeft(_textDragRect, Math.Min(p.X, _textDragStart.X));
            Canvas.SetTop(_textDragRect, Math.Min(p.Y, _textDragStart.Y));
            _textDragRect.Width = Math.Abs(p.X - _textDragStart.X);
            _textDragRect.Height = Math.Abs(p.Y - _textDragStart.Y);
            return true;
        }

        /// <summary>Ends a box draw: a click places the default box, a drag the drawn one.
        /// False when no draw was in progress.</summary>
        private bool FinishTextBoxDrag()
        {
            if (_textDragRect is null) return false;
            var rect = new Rect(Canvas.GetLeft(_textDragRect), Canvas.GetTop(_textDragRect),
                                _textDragRect.Width, _textDragRect.Height);
            RemoveFromParent(_textDragRect);
            _textDragRect = null;
            _activeCanvas?.ReleaseMouseCapture();
            int page = _textDragPage;
            _textDragPage = -1;

            if (rect.Width < TextDragThreshold && rect.Height < TextDragThreshold)
                PlaceTextBox(_textDragStart, page);
            else
                PlaceTextBox(rect.Location, page,
                    new Size(Math.Max(40, rect.Width), Math.Max(24, rect.Height)));
            return true;
        }

        // ── Fitting ───────────────────────────────────────────────────────

        /// <summary>
        /// The largest font size (canvas units) at which <paramref name="text"/>, wrapped to the box
        /// width, fits inside the box. <paramref name="chrome"/> is the horizontal and vertical space
        /// the box itself takes (padding plus border). Empty text is fitted as one line.
        /// </summary>
        internal static double FitFontToBox(string? text, double width, double height, FontFamily family,
                                            FontWeight weight, FontStyle style, double chrome, double maxFont = double.MaxValue)
        {
            // A little slack on the width: the editor's text view keeps room for the caret and wraps
            // a few pixels before a FormattedText of the same width would, and a word that fits
            // here but wraps there would vanish below the box.
            double innerW = (width - chrome) * 0.97 - 2, innerH = height - chrome;
            if (!(innerW > 1) || !(innerH > 1)) return MinFitFont;
            string sample = string.IsNullOrEmpty(text) ? "Ag" : text;
            var typeface = new Typeface(family, style, weight, FontStretches.Normal);

            bool Fits(double size)
            {
                var ft = new FormattedText(sample, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                           typeface, size, Brushes.Black, 1.0)
                { MaxTextWidth = innerW, Trimming = TextTrimming.None };
                return ft.Height <= innerH + 0.5 && ft.WidthIncludingTrailingWhitespace <= innerW + 0.5;
            }

            // A line is taller than its em, so the box height bounds the search from above.
            double hi = Math.Min(maxFont, innerH), lo = MinFitFont;
            if (hi <= lo) return lo;
            if (Fits(hi)) return hi;
            if (!Fits(lo)) return lo;
            for (int i = 0; i < 24 && hi - lo > 0.05; i++)
            {
                double mid = (lo + hi) / 2;
                if (Fits(mid)) lo = mid; else hi = mid;
            }
            return Math.Floor(lo * 10) / 10;   // a tenth of a unit: steady while dragging
        }

        /// <summary>The live box's padding (2 + 2) plus its border (1 + 1).</summary>
        private const double LiveBoxChrome = 6;

        /// <summary>A placed text annotation's padding (2 + 2), as RenderTextAnnotation draws it.</summary>
        private const double AnnotationChrome = 4;

        private static double FitLiveBox(TextBox tb, double width, double height, double maxFont = double.MaxValue)
            => FitFontToBox(tb.Text, width, height, tb.FontFamily, tb.FontWeight, tb.FontStyle, LiveBoxChrome, maxFont);

        internal static double FitAnnotation(TextAnnotation ta, double width, double height)
        {
            FontFamily family;
            try { family = new FontFamily(string.IsNullOrEmpty(ta.FontName) ? "Segoe UI" : ta.FontName); }
            catch { family = UiKit.UiFont; }
            return FitFontToBox(ta.Content, width, height, family,
                                ta.Bold ? FontWeights.Bold : FontWeights.Normal,
                                ta.Italic ? FontStyles.Italic : FontStyles.Normal, AnnotationChrome);
        }

        /// <summary>Shift held: resize the box only. Otherwise the font follows the box.</summary>
        private static bool KeepFontWhileResizing => (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        /// <summary>Called while a live box is resized by a corner: the font becomes the largest
        /// size that fits the new box, and that becomes the size typing aims for.</summary>
        private void FitLiveBoxToResize(TextBox tb, double width, double height)
        {
            if (KeepFontWhileResizing) return;
            double size = FitLiveBox(tb, width, height);
            tb.FontSize = size;
            _activeTextBaseFont = size;
        }

        /// <summary>Typing into a fixed-size box: shrink to keep the text inside, grow back toward
        /// the box's own size as text is removed. Boxes that grow with their text (no fixed
        /// height) are left alone.</summary>
        private void ActiveTextBox_TextChanged(object sender, TextChangedEventArgs e) => RefitTextBox(sender as TextBox);

        /// <summary>Re-fits the live box after a restyle (typeface, bold, italic).</summary>
        internal void RefitActiveTextBox() => RefitTextBox(_activeTextBox);

        private void RefitTextBox(TextBox? tb)
        {
            if (tb is null || double.IsNaN(tb.Height) || !(tb.Height > 0)) return;
            double width = !double.IsNaN(tb.Width) && tb.Width > 0 ? tb.Width : tb.ActualWidth;
            double baseFont = _activeTextBaseFont > 0 ? _activeTextBaseFont : tb.FontSize;
            double size = FitLiveBox(tb, width, tb.Height, baseFont);
            if (Math.Abs(size - tb.FontSize) > 0.05) tb.FontSize = size;
        }
    }
}

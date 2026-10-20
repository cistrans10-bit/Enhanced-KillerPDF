using System.Windows;

namespace KillerPDF.Services
{
    // #169: a plain page rotation used to reload with SaveTempAndReload's keepAnnotations
    // default, which cleared every overlay annotation - committed, unsaved user work was
    // destroyed the moment a page was rotated. Rotation now keeps the annotations and maps
    // their canvas coordinates through the turn instead. Coordinates live in the page's
    // render-dim space (the visual frame the user drew on); rotating the page turns that
    // frame, so the old (w, h) render dims become (h, w) when the reload re-renders.
    internal static class AnnotationRotate
    {
        /// <summary>Remaps one page's annotations for an in-app rotation by <paramref name="delta"/>
        /// degrees (clockwise positive, matching the render path), where <paramref name="oldW"/> and
        /// <paramref name="oldH"/> are the page's render dims BEFORE the turn. Region-shaped
        /// annotations (highlights, covers, ink, shapes) turn with the content they mark; text boxes
        /// and placed items (signatures, images) keep their own size and orientation and follow
        /// their center to the same page spot - their content renders upright either way.</summary>
        public static void Remap(IEnumerable<PageAnnotation> annots, int delta, double oldW, double oldH)
        {
            int d = ((delta % 360) + 360) % 360;
            if (d == 0) return;
            double newW = d == 90 || d == 270 ? oldH : oldW;
            double newH = d == 90 || d == 270 ? oldW : oldH;

            Point MapPoint(Point p) => d switch
            {
                // Forward quarter-turn of the visual frame, same convention as the render path's
                // clockwise bitmap rotation (see PdfBurn's VisualToPageMatrix, which is its inverse).
                90  => new Point(oldH - p.Y, p.X),
                270 => new Point(p.Y, oldW - p.X),
                _   => new Point(oldW - p.X, oldH - p.Y),   // 180
            };
            Rect MapRect(Rect r)
            {
                var a = MapPoint(new Point(r.X, r.Y));
                var b = MapPoint(new Point(r.Right, r.Bottom));
                return new Rect(a, b);   // Rect(Point, Point) normalizes the corners
            }
            Point MapAnchor(double x, double y, double w, double h)
            {
                var c = MapPoint(new Point(x + w / 2, y + h / 2));
                // Text, images and signatures stay upright while their centre follows the sheet.
                // A tall item near the old long edge can therefore need more room on the new axis
                // than it did before the turn. Keep the complete item reachable rather than
                // preserving an off-page coordinate the user cannot recover (#169 follow-up).
                double px = c.X - w / 2;
                double py = c.Y - h / 2;
                return new Point(
                    Math.Max(0, Math.Min(px, Math.Max(0, newW - w))),
                    Math.Max(0, Math.Min(py, Math.Max(0, newH - h))));
            }

            foreach (var annot in annots)
            {
                switch (annot)
                {
                    case HighlightAnnotation ha:   // includes CoverAnnotation
                        ha.Bounds = MapRect(ha.Bounds);
                        if (ha.Erases != null)
                            foreach (var e in ha.Erases)
                                for (int i = 0; i < e.Points.Count; i++)
                                    e.Points[i] = MapPoint(e.Points[i]);
                        break;

                    case InkAnnotation ia:
                        for (int i = 0; i < ia.Points.Count; i++)
                            ia.Points[i] = MapPoint(ia.Points[i]);
                        break;

                    case TextAnnotation ta:
                        ta.Position = MapAnchor(ta.Position.X, ta.Position.Y, ta.Width, ta.Height);
                        break;

                    case PlacedAnnotation pa:      // signature / image
                        pa.Position = MapAnchor(pa.Position.X, pa.Position.Y,
                                                pa.SourceWidth * pa.Scale, pa.SourceHeight * pa.Scale);
                        break;
                }
            }
        }
    }
}

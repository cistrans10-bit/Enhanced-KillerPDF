using System.Windows;

namespace KillerPDF.Services;

/// <summary>
/// Maps points and rectangles between a rendered page canvas and unrotated, crop-relative page
/// points, the coordinates the engine's text extraction uses. The canvas shows the page turned by
/// its effective rotation, so a 90 or 270 degree page swaps width and height on screen. Search
/// highlights, text selection and Crop all map through here.
/// </summary>
internal static class PageCanvasGeometry
{
    internal static int NormalizeRotation(int rotation) => ((rotation % 360) + 360) % 360;

    internal static (double Left, double Bottom, double Right, double Top) CanvasToPage(
        Rect canvasRect, double pageWidth, double pageHeight, double canvasWidth, double canvasHeight, int rotation)
    {
        double cx = canvasRect.X, cy = canvasRect.Y, cw = canvasRect.Width, ch = canvasRect.Height;
        (double x1, double y1, double x2, double y2) = NormalizeRotation(rotation) switch
        {
            90 => (cy * pageWidth / canvasHeight, cx * pageHeight / canvasWidth,
                (cy + ch) * pageWidth / canvasHeight, (cx + cw) * pageHeight / canvasWidth),
            180 => (pageWidth - (cx + cw) * pageWidth / canvasWidth, cy * pageHeight / canvasHeight,
                pageWidth - cx * pageWidth / canvasWidth, (cy + ch) * pageHeight / canvasHeight),
            270 => (pageWidth - (cy + ch) * pageWidth / canvasHeight, pageHeight - (cx + cw) * pageHeight / canvasWidth,
                pageWidth - cy * pageWidth / canvasHeight, pageHeight - cx * pageHeight / canvasWidth),
            _ => (cx * pageWidth / canvasWidth, pageHeight - (cy + ch) * pageHeight / canvasHeight,
                (cx + cw) * pageWidth / canvasWidth, pageHeight - cy * pageHeight / canvasHeight)
        };
        return (Math.Clamp(x1, 0, pageWidth), Math.Clamp(y1, 0, pageHeight),
            Math.Clamp(x2, 0, pageWidth), Math.Clamp(y2, 0, pageHeight));
    }

    /// <summary>Maps one canvas point to page points without clamping it to the page.</summary>
    internal static (double X, double Y) CanvasPointToPage(Point point, double pageWidth, double pageHeight,
        double canvasWidth, double canvasHeight, int rotation)
    {
        double cx = point.X, cy = point.Y;
        return NormalizeRotation(rotation) switch
        {
            90 => (cy * pageWidth / canvasHeight, cx * pageHeight / canvasWidth),
            180 => (pageWidth - cx * pageWidth / canvasWidth, cy * pageHeight / canvasHeight),
            270 => (pageWidth - cy * pageWidth / canvasHeight, pageHeight - cx * pageHeight / canvasWidth),
            _ => (cx * pageWidth / canvasWidth, pageHeight - cy * pageHeight / canvasHeight)
        };
    }

    internal static Rect PageToCanvas(double left, double bottom, double right, double top,
        double pageWidth, double pageHeight, double canvasWidth, double canvasHeight, int rotation)
    {
        return NormalizeRotation(rotation) switch
        {
            90 => new Rect(bottom * canvasWidth / pageHeight, left * canvasHeight / pageWidth,
                (top - bottom) * canvasWidth / pageHeight, (right - left) * canvasHeight / pageWidth),
            180 => new Rect((pageWidth - right) * canvasWidth / pageWidth, bottom * canvasHeight / pageHeight,
                (right - left) * canvasWidth / pageWidth, (top - bottom) * canvasHeight / pageHeight),
            270 => new Rect((pageHeight - top) * canvasWidth / pageHeight, (pageWidth - right) * canvasHeight / pageWidth,
                (top - bottom) * canvasWidth / pageHeight, (right - left) * canvasHeight / pageWidth),
            _ => new Rect(left * canvasWidth / pageWidth, (pageHeight - top) * canvasHeight / pageHeight,
                (right - left) * canvasWidth / pageWidth, (top - bottom) * canvasHeight / pageHeight)
        };
    }

    /// <summary>True when two canvas rectangles share interior area.</summary>
    internal static bool Overlaps(Rect a, Rect b) =>
        !a.IsEmpty && !b.IsEmpty && a.Right > b.Left && a.Left < b.Right && a.Bottom > b.Top && a.Top < b.Bottom;
}

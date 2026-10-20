namespace KillerPDF.Services;

internal static class ViewerRenderResolution
{
    internal static int Primary(double dpiScaleX, double dpiScaleY, double zoomLevel) =>
        (int)Math.Min(6144,
            2048 * Math.Max(dpiScaleX, dpiScaleY) * Math.Max(1.0, zoomLevel));

    internal static int Secondary(bool twoPage, double dpiScaleX, double dpiScaleY, double zoomLevel) =>
        twoPage
            ? Primary(dpiScaleX, dpiScaleY, zoomLevel)
            : (int)Math.Min(3072, 1536 * Math.Max(1.0, Math.Max(dpiScaleX, dpiScaleY)));
}

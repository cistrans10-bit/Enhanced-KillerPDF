namespace KillerPDF.Services
{
    /// <summary>
    /// Display-only pixel transforms. Callers must pass an isolated PDFium BGRA buffer;
    /// this helper intentionally mutates it in place so no second full-page allocation is needed.
    /// </summary>
    internal static class DisplayColorTransform
    {
        internal static byte[] PrepareBgraForDisplay(byte[] pixels, bool invert)
        {
            if (!invert) return pixels;

            // BGRA32: invert the visible colour channels and preserve alpha. Ignore an
            // incomplete trailing pixel defensively; PDFium normally supplies exact BGRA32.
            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                pixels[i]     = (byte)(255 - pixels[i]);
                pixels[i + 1] = (byte)(255 - pixels[i + 1]);
                pixels[i + 2] = (byte)(255 - pixels[i + 2]);
            }
            return pixels;
        }
    }
}

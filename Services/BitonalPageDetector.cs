namespace KillerPDF.Services;

internal static class BitonalPageDetector
{
    internal static bool IsOpaqueGrayscaleBgra(ReadOnlySpan<byte> pixels, int width, int height)
    {
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height * 4))
            return false;

        for (int offset = 0; offset < pixels.Length; offset += 4)
        {
            byte blue = pixels[offset];
            byte green = pixels[offset + 1];
            byte red = pixels[offset + 2];
            byte alpha = pixels[offset + 3];
            if (alpha != 255 || blue != green || green != red)
                return false;
        }
        return true;
    }
}

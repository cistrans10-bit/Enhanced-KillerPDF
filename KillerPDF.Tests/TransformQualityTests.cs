using System.Windows.Media;
using System.Windows.Media.Imaging;
using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests;

public sealed class TransformQualityTests
{
    [Fact]
    public void Grayscale_UsesPerceptualLuminanceAndPreservesAlpha()
    {
        BitmapSource source = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32,
            null, new byte[] { 0, 0, 255, 123 }, 4);

        BitmapSource result = PageQualityConverter.ApplyColorMode(
            source, PageColorMode.Grayscale, 160);

        var pixels = new byte[4];
        result.CopyPixels(pixels, 4, 0);
        Assert.Equal(pixels[0], pixels[1]);
        Assert.Equal(pixels[1], pixels[2]);
        Assert.InRange(pixels[0], 75, 77);
        Assert.Equal(123, pixels[3]);
    }

    [Fact]
    public void BlackAndWhite_UsesRequestedThreshold()
    {
        BitmapSource source = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32,
            null, new byte[] { 100, 100, 100, 255, 180, 180, 180, 255 }, 8);

        BitmapSource result = PageQualityConverter.ApplyColorMode(
            source, PageColorMode.BlackAndWhite, 160);

        var pixels = new byte[8];
        result.CopyPixels(pixels, 8, 0);
        Assert.Equal(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 }, pixels);
    }
}

using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests;

public sealed class BitonalPageDetectorTests
{
    [Fact]
    public void IsOpaqueGrayscaleBgra_AcceptsBlackWhiteAndAntialiasing()
    {
        byte[] pixels =
        [
            0, 0, 0, 255,
            127, 127, 127, 255,
            255, 255, 255, 255,
            0, 0, 0, 255
        ];

        Assert.True(BitonalPageDetector.IsOpaqueGrayscaleBgra(pixels, 2, 2));
    }

    [Theory]
    [InlineData(0, 0, 255, 255)]
    [InlineData(255, 255, 255, 128)]
    public void IsOpaqueGrayscaleBgra_RejectsColorAndTransparency(
        byte blue, byte green, byte red, byte alpha)
    {
        byte[] pixels = [blue, green, red, alpha];

        Assert.False(BitonalPageDetector.IsOpaqueGrayscaleBgra(pixels, 1, 1));
    }

    [Fact]
    public void IsOpaqueGrayscaleBgra_RejectsInvalidBufferLength()
    {
        Assert.False(BitonalPageDetector.IsOpaqueGrayscaleBgra([0, 0, 0, 255], 2, 1));
    }
}

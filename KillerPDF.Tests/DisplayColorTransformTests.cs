using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests
{
    public class DisplayColorTransformTests
    {
        [Fact]
        public void PrepareBgraForDisplay_Disabled_ReturnsOriginalPixelsUnchanged()
        {
            var pixels = new byte[] { 10, 20, 30, 40 };

            var result = DisplayColorTransform.PrepareBgraForDisplay(pixels, invert: false);

            Assert.Same(pixels, result);
            Assert.Equal(new byte[] { 10, 20, 30, 40 }, pixels);
        }

        [Fact]
        public void PrepareBgraForDisplay_InvertsRgbAndPreservesAlpha()
        {
            var pixels = new byte[]
            {
                0, 0, 0, 255,
                255, 255, 255, 17,
                10, 20, 30, 128,
            };

            DisplayColorTransform.PrepareBgraForDisplay(pixels, invert: true);

            Assert.Equal(new byte[]
            {
                255, 255, 255, 255,
                0, 0, 0, 17,
                245, 235, 225, 128,
            }, pixels);
        }

        [Fact]
        public void PrepareBgraForDisplay_Twice_RestoresOriginalPixels()
        {
            var pixels = new byte[] { 12, 34, 56, 78, 90, 123, 210, 45 };
            var original = (byte[])pixels.Clone();

            DisplayColorTransform.PrepareBgraForDisplay(pixels, invert: true);
            DisplayColorTransform.PrepareBgraForDisplay(pixels, invert: true);

            Assert.Equal(original, pixels);
        }

        [Fact]
        public void PrepareBgraForDisplay_EmptyOrPartialBuffer_IsSafe()
        {
            var empty = System.Array.Empty<byte>();
            var partial = new byte[] { 1, 2, 3 };

            DisplayColorTransform.PrepareBgraForDisplay(empty, invert: true);
            DisplayColorTransform.PrepareBgraForDisplay(partial, invert: true);

            Assert.Empty(empty);
            Assert.Equal(new byte[] { 1, 2, 3 }, partial);
        }
    }
}

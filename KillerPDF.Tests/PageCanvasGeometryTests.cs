using System.Windows;
using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests;

public sealed class PageCanvasGeometryTests
{
    // Expected canvas rectangles for a box at x 50..90, y 40..70 on a 300 by 200 point page shown
    // at twice its size. These match where the engine renderer draws the box at each rotation.
    [Theory]
    [InlineData(0, 100, 260, 80, 60)]
    [InlineData(90, 80, 100, 60, 80)]
    [InlineData(180, 420, 80, 80, 60)]
    [InlineData(270, 260, 420, 60, 80)]
    public void PageRectanglesLandWhereTheRotatedPageShowsThem(int rotation, double x, double y, double width, double height)
    {
        bool quarter = rotation is 90 or 270;
        Rect box = PageCanvasGeometry.PageToCanvas(50, 40, 90, 70, 300, 200,
            quarter ? 400 : 600, quarter ? 600 : 400, rotation);

        Assert.Equal(new Rect(x, y, width, height), box);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    [InlineData(-90)]
    public void CanvasAndPageRectanglesRoundTrip(int rotation)
    {
        bool quarter = PageCanvasGeometry.NormalizeRotation(rotation) is 90 or 270;
        double canvasWidth = quarter ? 400 : 600, canvasHeight = quarter ? 600 : 400;
        var drawn = new Rect(37, 52, 120, 33);

        var (left, bottom, right, top) = PageCanvasGeometry.CanvasToPage(drawn, 300, 200, canvasWidth, canvasHeight, rotation);
        Rect back = PageCanvasGeometry.PageToCanvas(left, bottom, right, top, 300, 200, canvasWidth, canvasHeight, rotation);

        Assert.Equal(drawn.X, back.X, 6);
        Assert.Equal(drawn.Y, back.Y, 6);
        Assert.Equal(drawn.Width, back.Width, 6);
        Assert.Equal(drawn.Height, back.Height, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void CanvasPointsMapLikeTheCornersOfARectangle(int rotation)
    {
        bool quarter = rotation is 90 or 270;
        double canvasWidth = quarter ? 400 : 600, canvasHeight = quarter ? 600 : 400;
        Rect page = PageCanvasGeometry.PageToCanvas(50, 40, 90, 70, 300, 200, canvasWidth, canvasHeight, rotation);

        var (x, y) = PageCanvasGeometry.CanvasPointToPage(
            new Point(page.X + page.Width / 2, page.Y + page.Height / 2), 300, 200, canvasWidth, canvasHeight, rotation);

        Assert.Equal(70, x, 6);
        Assert.Equal(55, y, 6);
    }
}

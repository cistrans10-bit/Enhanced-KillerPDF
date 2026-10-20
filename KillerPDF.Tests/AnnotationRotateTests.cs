using System.Windows;
using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests;

// #169: rotating a page must keep its overlay annotations and turn them with the page.
// The mapping runs in render-dim space; a +90 turn takes frame (W, H) to (H, W), so
// round-trip tests swap the frame between passes exactly as the live reload does.
public sealed class AnnotationRotateTests
{
    private const double W = 800, H = 600;

    [Fact]
    public void Highlight_QuarterTurn_TurnsWithTheContent()
    {
        var ha = new HighlightAnnotation { Bounds = new Rect(10, 20, 100, 40) };
        AnnotationRotate.Remap([ha], 90, W, H);

        // Corners (10,20)-(110,60) map to (580,10)-(540,110): the region swaps its axes.
        Assert.Equal(540, ha.Bounds.X, 3);
        Assert.Equal(10, ha.Bounds.Y, 3);
        Assert.Equal(40, ha.Bounds.Width, 3);
        Assert.Equal(100, ha.Bounds.Height, 3);
    }

    [Fact]
    public void Highlight_FourQuarterTurns_IsIdentity()
    {
        var start = new Rect(10, 20, 100, 40);
        var ha = new HighlightAnnotation { Bounds = start };
        AnnotationRotate.Remap([ha], 90, W, H);
        AnnotationRotate.Remap([ha], 90, H, W);
        AnnotationRotate.Remap([ha], 90, W, H);
        AnnotationRotate.Remap([ha], 90, H, W);
        Assert.Equal(start, ha.Bounds);
    }

    [Fact]
    public void Highlight_TurnThenCounterTurn_IsIdentity()
    {
        var start = new Rect(33, 44, 55, 66);
        var ha = new HighlightAnnotation { Bounds = start };
        AnnotationRotate.Remap([ha], 90, W, H);
        AnnotationRotate.Remap([ha], -90, H, W);
        Assert.Equal(start, ha.Bounds);
    }

    [Fact]
    public void Highlight_TwoQuarterTurns_MatchHalfTurn()
    {
        var a = new HighlightAnnotation { Bounds = new Rect(10, 20, 100, 40) };
        var b = new HighlightAnnotation { Bounds = new Rect(10, 20, 100, 40) };
        AnnotationRotate.Remap([a], 90, W, H);
        AnnotationRotate.Remap([a], 90, H, W);
        AnnotationRotate.Remap([b], 180, W, H);
        Assert.Equal(b.Bounds, a.Bounds);
    }

    [Fact]
    public void TextBox_KeepsSize_CenterFollowsThePage()
    {
        var ta = new TextAnnotation { Position = new Point(10, 70), Width = 100, Height = 40 };
        AnnotationRotate.Remap([ta], 90, W, H);

        // Center (60,90) maps to (510,60); the box keeps 100x40 around it.
        Assert.Equal(460, ta.Position.X, 3);
        Assert.Equal(40, ta.Position.Y, 3);
        Assert.Equal(100, ta.Width, 3);
        Assert.Equal(40, ta.Height, 3);
    }

    [Fact]
    public void UprightTextBoxNearLongEdge_IsClampedInsideRotatedPage()
    {
        // Fully inside the old 800x600 page. Keeping this tall box upright after a clockwise turn
        // would put its bottom at 870 on the new 600x800 page unless the remap clamps it.
        var ta = new TextAnnotation { Position = new Point(750, 100), Width = 40, Height = 200 };
        AnnotationRotate.Remap([ta], 90, W, H);

        Assert.Equal(380, ta.Position.X, 3);
        Assert.Equal(600, ta.Position.Y, 3);
        Assert.InRange(ta.Position.X + ta.Width, 0, H);
        Assert.InRange(ta.Position.Y + ta.Height, 0, W);
    }

    [Fact]
    public void UprightPlacedItemNearLongEdge_IsClampedInsideRotatedPage()
    {
        var image = new ImageAnnotation
        {
            Position = new Point(750, 100),
            SourceWidth = 40,
            SourceHeight = 200,
            Scale = 1
        };

        AnnotationRotate.Remap([image], 90, W, H);

        Assert.Equal(380, image.Position.X, 3);
        Assert.Equal(600, image.Position.Y, 3);
        Assert.InRange(image.Position.X + image.SourceWidth * image.Scale, 0, H);
        Assert.InRange(image.Position.Y + image.SourceHeight * image.Scale, 0, W);
    }

    public static TheoryData<double, double, int> QuarterTurnFrames => new()
    {
        { 1191, 842, 90 },
        { 1191, 842, -90 },
        { 842, 1191, 90 },
        { 842, 1191, -90 },
    };

    [Theory]
    [MemberData(nameof(QuarterTurnFrames))]
    public void UprightItemsNearEveryEdge_UsePostRotationBounds(double oldW, double oldH, int delta)
    {
        const double itemW = 152.906;
        const double itemH = 91.25;
        Point[] positions =
        [
            new(0, 0),
            new(oldW - itemW, 0),
            new(0, oldH - itemH),
            new(oldW - itemW, oldH - itemH),
        ];

        foreach (Point position in positions)
        {
            var text = new TextAnnotation
            {
                Position = position,
                Width = itemW,
                Height = itemH,
            };
            var image = new ImageAnnotation
            {
                Position = position,
                SourceWidth = itemW,
                SourceHeight = itemH,
                Scale = 1,
            };

            AnnotationRotate.Remap([text, image], delta, oldW, oldH);

            AssertInsideRotatedFrame(text.Position, itemW, itemH, oldH, oldW);
            AssertInsideRotatedFrame(image.Position, itemW, itemH, oldH, oldW);
        }
    }

    private static void AssertInsideRotatedFrame(
        Point position, double width, double height, double frameWidth, double frameHeight)
    {
        Assert.InRange(position.X, 0, Math.Max(0, frameWidth - width));
        Assert.InRange(position.Y, 0, Math.Max(0, frameHeight - height));
        Assert.InRange(position.X + width, 0, frameWidth);
        Assert.InRange(position.Y + height, 0, frameHeight);
    }

    [Fact]
    public void InkPoints_MapPerPoint()
    {
        var ia = new InkAnnotation { Points = [new Point(0, 0), new Point(100, 50)] };
        AnnotationRotate.Remap([ia], 90, W, H);
        Assert.Equal(new Point(H, 0), ia.Points[0]);
        Assert.Equal(new Point(H - 50, 100), ia.Points[1]);
    }
}

using System.Windows;
using System.Windows.Controls;

namespace KillerPDF.Controls;

public sealed class SafeCanvas : Canvas
{
    private Size _lastMeasure;

    protected override Size MeasureOverride(Size constraint)
    {
        try
        {
            _lastMeasure = base.MeasureOverride(constraint);
            return _lastMeasure;
        }
        catch (Exception exception) when (LayoutRace.IsCanvasCollectionChanged(exception))
        {
            LayoutRace.QueueRelayout(this);
            return _lastMeasure;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        try
        {
            return base.ArrangeOverride(finalSize);
        }
        catch (Exception exception) when (LayoutRace.IsCanvasCollectionChanged(exception))
        {
            LayoutRace.QueueRelayout(this);
            return finalSize;
        }
    }
}

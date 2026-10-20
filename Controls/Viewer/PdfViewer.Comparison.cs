using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KillerPDF.Services;

namespace KillerPDF.Controls;

public partial class PdfViewer
{
    private const string DifferenceOverlayTag = "PdfComparisonDifference";
    private const string MissingPageOverlayTag = "PdfComparisonMissingPage";

    private void ClearDifferenceRegions()
    {
        foreach (Canvas canvas in AllPageCanvases())
            for (int i = canvas.Children.Count - 1; i >= 0; i--)
                if (canvas.Children[i] is FrameworkElement element
                    && (Equals(element.Tag, DifferenceOverlayTag) || Equals(element.Tag, MissingPageOverlayTag)))
                    canvas.Children.RemoveAt(i);
    }

    private void ShowMissingComparisonPage(int page, string text)
    {
        Canvas? canvas = VisibleCanvasForPage(page);
        if (canvas is null) return;
        for (int i = canvas.Children.Count - 1; i >= 0; i--)
            if (canvas.Children[i] is FrameworkElement { Tag: MissingPageOverlayTag })
                canvas.Children.RemoveAt(i);

        var content = new Grid();
        var grain = new Border { CornerRadius = new CornerRadius(2), IsHitTestVisible = false };
        grain.SetResourceReference(Border.BackgroundProperty, "GrainBrushShared");
        grain.SetResourceReference(UIElement.OpacityProperty, "GrainOpacity");
        content.Children.Add(grain);
        content.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(16, 10, 16, 10)
        });
        var label = new Border
        {
            Tag = MissingPageOverlayTag,
            Background = new SolidColorBrush(Color.FromArgb(210, 40, 40, 40)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 55, 55)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Child = content,
            IsHitTestVisible = false
        };
        canvas.Children.Add(label);
        label.Loaded += (_, _) =>
            ScaleAndCenterMissingPageLabel(label, canvas);
        Panel.SetZIndex(label, 9401);
    }

    private void UpdateMissingPageLabelScale()
    {
        foreach (Canvas canvas in AllPageCanvases())
            foreach (Border label in canvas.Children.OfType<Border>())
                if (Equals(label.Tag, MissingPageOverlayTag))
                    ScaleAndCenterMissingPageLabel(label, canvas);
    }

    private void ScaleAndCenterMissingPageLabel(Border label, Canvas canvas)
    {
        double scale = 1.0;
        DependencyObject? current = canvas;
        while (current is not null && !ReferenceEquals(current, this))
        {
            if (current is FrameworkElement element && element.LayoutTransform is ScaleTransform layout
                && layout.ScaleX > 0.0001)
                scale *= layout.ScaleX;
            if (current is UIElement visual && visual.RenderTransform is ScaleTransform render
                && render.ScaleX > 0.0001)
                scale *= render.ScaleX;
            current = VisualTreeHelper.GetParent(current);
        }
        double inverse = scale > 0.0001 ? 1.0 / scale : 1.0;
        // Keep the notice readable while the PDF canvas changes scale.
        label.MaxWidth = Math.Max(48, canvas.ActualWidth * scale - 16);
        label.LayoutTransform = new ScaleTransform(inverse, inverse);
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, Math.Max(8, (canvas.ActualWidth - label.DesiredSize.Width) / 2));
        Canvas.SetTop(label, Math.Max(8, (canvas.ActualHeight - label.DesiredSize.Height) / 2));
    }

    private void ShowDifferenceRegions(int page, int sourceWidth, int sourceHeight,
        IReadOnlyList<DifferenceRegion> regions, int selectedRegion)
    {
        Canvas? canvas = VisibleCanvasForPage(page);
        if (canvas is null || sourceWidth <= 0 || sourceHeight <= 0) return;
        for (int i = canvas.Children.Count - 1; i >= 0; i--)
            if (canvas.Children[i] is FrameworkElement { Tag: DifferenceOverlayTag })
                canvas.Children.RemoveAt(i);

        double sx = canvas.ActualWidth / sourceWidth;
        double sy = canvas.ActualHeight / sourceHeight;
        for (int index = 0; index < regions.Count; index++)
        {
            DifferenceRegion region = regions[index];
            bool selected = index == selectedRegion;
            var box = new Rectangle
            {
                Tag = DifferenceOverlayTag,
                Width = Math.Max(2, region.Width * sx),
                Height = Math.Max(2, region.Height * sy),
                Fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)90 : (byte)42, 255, 45, 45)),
                Stroke = new SolidColorBrush(selected
                    ? Color.FromArgb(255, 255, 225, 45)
                    : Color.FromArgb(235, 255, 55, 55)),
                StrokeThickness = selected ? 3 : 2,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(box, region.X * sx);
            Canvas.SetTop(box, region.Y * sy);
            Panel.SetZIndex(box, 9400);
            canvas.Children.Add(box);
        }
    }
}

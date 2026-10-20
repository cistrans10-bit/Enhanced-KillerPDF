namespace KillerPDF.Services;

internal readonly record struct MeasurementValues(
    double Points, double Inches, double Millimetres,
    double PageWidthPoints, double PageHeightPoints);

internal static class MeasurementCalculator
{
    internal static MeasurementValues Calculate(
        double pageWidthPoints, double pageHeightPoints, int rotation,
        double renderWidth, double renderHeight, double canvasDx, double canvasDy)
    {
        if (pageWidthPoints <= 0 || pageHeightPoints <= 0 || renderWidth <= 0 || renderHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(renderWidth));
        bool quarterTurn = rotation is 90 or 270;
        double displayWidth = quarterTurn ? pageHeightPoints : pageWidthPoints;
        double displayHeight = quarterTurn ? pageWidthPoints : pageHeightPoints;
        double dxPoints = canvasDx * displayWidth / renderWidth;
        double dyPoints = canvasDy * displayHeight / renderHeight;
        double points = Math.Sqrt(dxPoints * dxPoints + dyPoints * dyPoints);
        double inches = points / 72.0;
        return new MeasurementValues(points, inches, inches * 25.4, displayWidth, displayHeight);
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace KillerPDF.Controls;

internal static class LayoutRace
{
    internal static bool IsCanvasCollectionChanged(Exception exception) =>
        exception is InvalidOperationException
        && OriginatesInPanelLayout(exception, "System.Windows.Controls.Canvas");

    internal static bool IsWrapPanelCollectionChanged(Exception exception) =>
        exception is ArgumentOutOfRangeException or IndexOutOfRangeException
        && OriginatesInPanelLayout(exception, "System.Windows.Controls.WrapPanel");

    private static bool OriginatesInPanelLayout(Exception exception, string panelType)
    {
        if (IsPanelLayoutMethod(
            exception.TargetSite?.DeclaringType?.FullName,
            exception.TargetSite?.Name,
            panelType))
            return true;

        StackFrame[]? frames = new StackTrace(exception, false).GetFrames();
        if (frames is null) return false;
        foreach (StackFrame frame in frames)
        {
            var method = frame.GetMethod();
            if (method is null) continue;
            string? declaringType = method.DeclaringType?.FullName;
            if (IsThrowHelper(declaringType, method.Name)
                || declaringType?.Contains("VisualCollection", StringComparison.Ordinal) == true
                || declaringType?.Contains("UIElementCollection", StringComparison.Ordinal) == true)
                continue;
            return IsPanelLayoutMethod(declaringType, method.Name, panelType);
        }
        return false;
    }

    private static bool IsPanelLayoutMethod(
        string? declaringType, string? methodName, string panelType) =>
        string.Equals(declaringType, panelType, StringComparison.Ordinal)
        && methodName is "MeasureOverride" or "ArrangeOverride";

    private static bool IsThrowHelper(string? declaringType, string methodName) =>
        string.Equals(declaringType, "System.ThrowHelper", StringComparison.Ordinal)
        || (declaringType is "System.ArgumentOutOfRangeException" or "System.InvalidOperationException"
            && methodName.StartsWith("Throw", StringComparison.Ordinal));

    internal static void QueueRelayout(FrameworkElement element)
    {
        element.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            element.InvalidateMeasure();
            element.InvalidateArrange();
        }));
    }
}

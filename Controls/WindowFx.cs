using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace KillerPDF
{

    // Fade a window out on close: cancel the first close, animate opacity to 0, then close for real.
    // Cancelling a modal dialog's close clears its DialogResult, so the result is kept here and
    // given back when the fade finishes; otherwise ShowDialog reported every OK as a cancel.
    internal static class WindowFx
    {
        public const int FadeMs = 150;

        public static void EnableFadeClose(Window w, int ms = FadeMs)
        {
            bool fading = false;
            bool readyToClose = false;
            bool closed = false;
            bool? result = null;
            w.Closed += (_, _) => closed = true;
            w.Closing += (s, e) =>
            {
                if (readyToClose) return;  // our own post-fade Close - let it through
                result ??= w.DialogResult;
                e.Cancel = true;           // hold off the real close until the fade finishes
                if (fading) return;        // already fading - ignore repeat triggers
                fading = true;
                var anim = new DoubleAnimation(w.Opacity, 0, new Duration(TimeSpan.FromMilliseconds(ms)))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                anim.Completed += (_, _) =>
                {
                    readyToClose = true;
                    // Setting the result of a modal dialog closes it with that result.
                    if (result is not null && w.DialogResult != result)
                        try { w.DialogResult = result; }
                        catch (InvalidOperationException) { }   // not shown as a dialog
                    if (!closed) w.Close();
                };
                w.BeginAnimation(UIElement.OpacityProperty, anim);
            };
        }
    }
}

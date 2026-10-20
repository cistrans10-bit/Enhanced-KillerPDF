using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace KillerPDF.Tests;

// A dialog with the fade-out close must still hand its result back to ShowDialog.
public sealed class WindowFxTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FadingDialogKeepsItsResult(bool accepted)
    {
        bool? shown = RunSta(() =>
        {
            Window dialog = HiddenDialog();
            WindowFx.EnableFadeClose(dialog, 20);
            dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(
                () => dialog.DialogResult = accepted, DispatcherPriority.ApplicationIdle);
            return dialog.ShowDialog();
        });

        Assert.Equal(accepted, shown);
    }

    [Fact]
    public void FadingDialogClosedWithoutAResultIsACancel()
    {
        bool? shown = RunSta(() =>
        {
            Window dialog = HiddenDialog();
            WindowFx.EnableFadeClose(dialog, 20);
            dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(
                dialog.Close, DispatcherPriority.ApplicationIdle);
            return dialog.ShowDialog();
        });

        Assert.False(shown);
    }

    [Fact]
    public void FadingWindowShownModelessStillCloses()
    {
        bool closed = RunSta(() =>
        {
            Window window = HiddenDialog();
            WindowFx.EnableFadeClose(window, 20);
            var frame = new DispatcherFrame();
            bool done = false;
            window.Closed += (_, _) => { done = true; frame.Continue = false; };
            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                window.Close, DispatcherPriority.ApplicationIdle);
            window.Show();
            Dispatcher.PushFrame(frame);
            return done;
        });

        Assert.True(closed);
    }

    private static Window HiddenDialog() => new()
    {
        Width = 40, Height = 40, Left = -32000, Top = -32000,
        WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
        WindowStartupLocation = WindowStartupLocation.Manual
    };

    private static T RunSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception exception) { error = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30)))
            throw new TimeoutException("The dialog never closed.");
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
        return result;
    }
}

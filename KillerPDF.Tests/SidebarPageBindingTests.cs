using System;
using System.Threading;
using System.Windows.Controls;
using KillerPDF.Controls;
using Xunit;

namespace KillerPDF.Tests;

public sealed class SidebarPageBindingTests
{
    // xUnit runs tests on an MTA thread and WPF elements refuse to construct there, so each body
    // runs on a short-lived STA thread with any failure rethrown on the test thread.
    private static void OnSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    private static (SidebarPageBinding Binding, ListBox List, TextBlock Total) NewSidebar()
    {
        var list = new ListBox();
        var total = new TextBlock();
        return (new SidebarPageBinding(list, total), list, total);
    }

    /// <summary>The pane-switch case: showing another document's pages must move the total with
    /// them. This is what regressed - the list changed and the total kept the old count.</summary>
    [Fact]
    public void TotalFollowsTheListOnEveryShow() => OnSta(() =>
    {
        var (binding, _, total) = NewSidebar();

        binding.Show(new object[4]);
        Assert.Equal("/ 4", total.Text);

        binding.Show(new object[3]);          // focus the three page pane
        Assert.Equal("/ 3", total.Text);

        binding.Show(new object[4]);          // and back again
        Assert.Equal("/ 4", total.Text);
    });

    /// <summary>The insert case: a page dragged in from the other pane grows the list, and the
    /// total has to grow with it rather than keep the pre-insert count.</summary>
    [Fact]
    public void TotalGrowsWhenThePageListGrows() => OnSta(() =>
    {
        var (binding, _, total) = NewSidebar();

        binding.Show(new object[3]);
        Assert.Equal("/ 3", total.Text);

        binding.Show(new object[4]);
        Assert.Equal("/ 4", total.Text);
    });

    [Fact]
    public void NoDocumentClearsBothTheListAndTheTotal() => OnSta(() =>
    {
        var (binding, list, total) = NewSidebar();
        binding.Show(new object[2]);

        binding.Show(null);

        Assert.Null(list.ItemsSource);
        Assert.Equal(SidebarPageBinding.Empty, total.Text);
    });

    [Fact]
    public void TotalCountsTheListThatIsActuallyBound() => OnSta(() =>
    {
        var (binding, list, total) = NewSidebar();
        var pages = new object[7];

        binding.Show(pages);

        Assert.Same(pages, list.ItemsSource);
        Assert.Equal("/ 7", total.Text);
    });

    /// <summary>Re-binding an identical list rebuilds every container, so the same instance must
    /// not be reassigned. The total is still written, which costs nothing and cannot drift.</summary>
    [Fact]
    public void ShowingTheSameListAgainDoesNotRebindIt() => OnSta(() =>
    {
        var (binding, list, total) = NewSidebar();
        var pages = new object[5];
        binding.Show(pages);

        list.ItemsSource = null;              // stand in for a rebind we must not see repeated
        binding.Show(pages);

        Assert.Same(pages, list.ItemsSource); // reassigned only because it genuinely changed
        Assert.Equal("/ 5", total.Text);
    });
}

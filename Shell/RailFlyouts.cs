using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace KillerPDF
{
    /// <summary>
    /// The rail's flyout buttons (family order, locked 2026-07-30: app-specific toggles, then
    /// ? / language / theme, theme bottom-most) and their flyouts. The theme and language
    /// pickers live here as
    /// family-standard flyouts: ContextMenus with FlyoutCard/FlyoutGrain chrome, opened against
    /// the content pane's rail-adjacent edge at the opening button's height so they never cover the rail,
    /// the footer, or the desktop. Their radio/dot sync is SyncPickerState (SettingsPanel.cs).
    /// </summary>
    public partial class MainWindow
    {
        // The shortcuts ? is the strip's ORIGINAL button (ShortcutHelp_Click) - it just moved
        // into the family slot above language; no second implementation was added.

        private void RailMenu_Opening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
            ToggleRailFlyout(sender == RailLangBtn ? LangFlyout : ThemeFlyout);
        }

        private void RailLang_Click(object sender, RoutedEventArgs e) => ToggleRailFlyout(LangFlyout);

        private void RailTheme_Click(object sender, RoutedEventArgs e) => ToggleRailFlyout(ThemeFlyout);

        private void ToggleRailFlyout(ContextMenu menu)
        {
            if (menu.IsOpen) { menu.IsOpen = false; return; }

            // The theme strip always faces the document. Mirroring the two columns also makes its
            // width animation grow outward from the rail on either side of the window.
            if (menu == ThemeFlyout)
                SyncThemeFlyoutSide();

            // Radios and accent dots reflect live state before the card shows - the same single
            // sync the Settings panel runs on open.
            SyncPickerState();

            // SplitHost is the full document region between the rail and the window edge. Its
            // rail-adjacent edge is left when the sidebar is left and right when the
            // sidebar is right. Using one viewer pane here stranded the flyouts on the left after
            // the sidebar moved right, and could place them in the middle of a split document.
            if (SplitHost is FrameworkElement pane)
                FlyoutPlacement.UsePane(pane, _sidebarRight);
            FlyoutPlacement.AttachAtButtonHeight(menu, menu == LangFlyout ? RailLangBtn : RailThemeBtn);

            menu.IsOpen = true;
            // 150ms ease-out, the family fade (the flyout template replaces the implicit
            // ContextMenu template, whose Loaded trigger normally drives this).
            menu.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150)))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void SyncThemeFlyoutSide()
        {
            if (ThemePickerLayout is null || ThemeSubmenu is null || AccentStripHost is null ||
                AccentStripDivider is null || AccentStrip is null)
                return;

            Grid.SetColumn(ThemeSubmenu, _sidebarRight ? 1 : 0);
            Grid.SetColumn(AccentStripHost, _sidebarRight ? 0 : 1);
            AccentStripDivider.HorizontalAlignment = _sidebarRight
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
            AccentStrip.Margin = _sidebarRight
                ? new Thickness(2, 6, 7, 6)
                : new Thickness(7, 6, 2, 6);
            ThemePickerLayout.Margin = _sidebarRight
                ? new Thickness(3, 10, 12, 10)
                : new Thickness(12, 10, 3, 10);
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using KillerPDF.Services;

namespace KillerPDF
{
    public partial class MainWindow
    {
        private enum ToolbarPlacement { Docked, Floating, Hidden }
        private enum ThumbnailBarMode { Floating, Docked, Hidden }

        private ToolbarPlacement _toolbarPlacement = ToolbarPlacement.Docked;
        private ThumbnailBarMode _thumbnailBarMode = ThumbnailBarMode.Floating;
        private bool _documentDarkMode;
        private bool _syncingViewerChromeSettings;
        private ScrollViewer? _bottomThumbnailScrollViewer;
        private readonly ConditionalWeakTable<BitmapSource, BitmapSource> _darkBitmapCache = new();

        // The page treatment is intentionally independent of the app theme. Users can combine dark
        // chrome with normal paper, or light chrome with inverted pages, without changing the PDF.
        private Brush DocumentPageBackground => _documentDarkMode ? Brushes.Black : Brushes.White;

        private Color DisplayDocumentColor(Color color) => _documentDarkMode
            ? Color.FromArgb(color.A, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B))
            : color;

        private BitmapSource DisplayDocumentBitmap(BitmapSource source)
            => !_documentDarkMode ? source : _darkBitmapCache.GetValue(source, CreateDarkBitmap);

        private static BitmapSource CreateDarkBitmap(BitmapSource source)
        {
            BitmapSource bgra = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int stride = Math.Max(1, bgra.PixelWidth * 4);
            var pixels = new byte[stride * Math.Max(1, bgra.PixelHeight)];
            bgra.CopyPixels(pixels, stride, 0);
            DisplayColorTransform.PrepareBgraForDisplay(pixels, invert: true);

            var result = new WriteableBitmap(
                bgra.PixelWidth, bgra.PixelHeight,
                bgra.DpiX > 0 ? bgra.DpiX : 96,
                bgra.DpiY > 0 ? bgra.DpiY : 96,
                PixelFormats.Bgra32, null);
            result.WritePixels(new Int32Rect(0, 0, bgra.PixelWidth, bgra.PixelHeight), pixels, stride, 0);
            result.Freeze();
            return result;
        }

        private void ReadViewerChromeSettings()
        {
            if (Enum.TryParse(App.GetSetting("ToolbarPlacement"), ignoreCase: true, out ToolbarPlacement toolbar))
                _toolbarPlacement = toolbar;
            if (Enum.TryParse(App.GetSetting("ThumbnailBarMode"), ignoreCase: true, out ThumbnailBarMode thumbnails))
                _thumbnailBarMode = thumbnails;
            _documentDarkMode = App.GetSetting("DocumentDarkMode") == "1";
        }

        private void ApplyViewerChromeLayout()
        {
            ApplyToolbarPlacement();
            ApplyThumbnailBarMode();
            UpdateViewerChromeButtons();
        }

        private void ApplyToolbarPlacement()
        {
            if (ToolbarRowBorder is null) return;
            if (_fullScreen)
            {
                ToolbarRowBorder.Visibility = Visibility.Collapsed;
                return;
            }

            ToolbarRowBorder.BeginAnimation(OpacityProperty, null);
            Grid.SetRowSpan(ToolbarRowBorder, 1);
            ToolbarRowBorder.Padding = new Thickness(0);

            switch (_toolbarPlacement)
            {
                case ToolbarPlacement.Hidden:
                    HostToolbarIn(RootClipGrid);
                    Grid.SetRow(ToolbarRowBorder, 1);
                    Panel.SetZIndex(ToolbarRowBorder, 0);
                    ToolbarRowBorder.Visibility = Visibility.Collapsed;
                    break;

                case ToolbarPlacement.Floating:
                    // Reposition the existing toolbar instead of duplicating it, preserving every command,
                    // split-popup anchor, active-tool state, and the responsive overflow machinery.
                    // Hosting it in the document viewport keeps it below tabs and out of the resizable
                    // sidebar at every window width and for either sidebar side.
                    HostToolbarIn(DocViewportGrid);
                    Grid.SetRow(ToolbarRowBorder, 0);
                    Grid.SetRowSpan(ToolbarRowBorder, 1);
                    Panel.SetZIndex(ToolbarRowBorder, 700);
                    ToolbarRowBorder.Visibility = Visibility.Visible;
                    ToolbarRowBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                    ToolbarRowBorder.VerticalAlignment = VerticalAlignment.Top;
                    ToolbarRowBorder.Margin = new Thickness(16, 10, 16, 0);
                    ToolbarRowBorder.MaxWidth = 1400;
                    ToolbarRowBorder.CornerRadius = new CornerRadius(14);
                    ToolbarRowBorder.BorderThickness = new Thickness(1);
                    ToolbarRowBorder.Padding = new Thickness(2);
                    ToolbarRowBorder.ClipToBounds = true;
                    ToolbarRowBorder.Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 16,
                        ShadowDepth = 3,
                        Direction = 270,
                        Opacity = 0.42
                    };
                    break;

                default:
                    HostToolbarIn(RootClipGrid);
                    Grid.SetRow(ToolbarRowBorder, 1);
                    Panel.SetZIndex(ToolbarRowBorder, 0);
                    ToolbarRowBorder.Visibility = Visibility.Visible;
                    ToolbarRowBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                    ToolbarRowBorder.VerticalAlignment = VerticalAlignment.Stretch;
                    ToolbarRowBorder.Margin = new Thickness(0);
                    ToolbarRowBorder.MaxWidth = double.PositiveInfinity;
                    ToolbarRowBorder.CornerRadius = new CornerRadius(0);
                    ToolbarRowBorder.BorderThickness = new Thickness(0, 0, 0, 1);
                    ToolbarRowBorder.ClipToBounds = false;
                    ToolbarRowBorder.Effect = null;
                    break;
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                (Action)(() => { QueueReflowToolbar(); ScheduleViewerRefit(); }));
        }

        private void HostToolbarIn(Grid host)
        {
            if (ReferenceEquals(System.Windows.Media.VisualTreeHelper.GetParent(ToolbarRowBorder), host)) return;
            if (System.Windows.Media.VisualTreeHelper.GetParent(ToolbarRowBorder) is Panel current)
                current.Children.Remove(ToolbarRowBorder);
            host.Children.Add(ToolbarRowBorder);
        }

        private void ApplyThumbnailBarMode()
        {
            if (ThumbnailBarBorder is null || ThumbnailBarRow is null || BottomThumbnailList is null) return;

            ThumbnailBarRow.Height = new GridLength(0);
            // Items.Count can lag an ItemsSource replacement until WPF processes the next layout pass.
            // The document model is the authoritative availability check during first-open bootstrap.
            bool hasDocument = _doc is { PageCount: > 0 } && _currentFile is not null;
            if (_fullScreen || _thumbnailBarMode == ThumbnailBarMode.Hidden || !hasDocument)
            {
                ThumbnailBarBorder.Visibility = Visibility.Collapsed;
                return;
            }

            ThumbnailBarBorder.Visibility = Visibility.Visible;
            if (_thumbnailBarMode == ThumbnailBarMode.Docked)
            {
                Grid.SetRow(ThumbnailBarBorder, 1);
                ThumbnailBarRow.Height = new GridLength(96);
                ThumbnailBarBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                ThumbnailBarBorder.VerticalAlignment = VerticalAlignment.Stretch;
                ThumbnailBarBorder.Margin = new Thickness(0);
                ThumbnailBarBorder.MaxWidth = double.PositiveInfinity;
                ThumbnailBarBorder.Height = double.NaN;
                ThumbnailBarBorder.CornerRadius = new CornerRadius(0);
                ThumbnailBarBorder.BorderThickness = new Thickness(0, 1, 0, 0);
                ThumbnailBarBorder.Effect = null;
                BottomThumbnailList.Tag = 68d;
            }
            else
            {
                Grid.SetRow(ThumbnailBarBorder, 0);
                ThumbnailBarBorder.HorizontalAlignment = HorizontalAlignment.Center;
                ThumbnailBarBorder.VerticalAlignment = VerticalAlignment.Bottom;
                ThumbnailBarBorder.Margin = new Thickness(0, 0, 0, 16);
                ThumbnailBarBorder.MaxWidth = 760;
                ThumbnailBarBorder.Height = 52;
                ThumbnailBarBorder.CornerRadius = new CornerRadius(12);
                ThumbnailBarBorder.BorderThickness = new Thickness(1);
                ThumbnailBarBorder.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 14,
                    ShadowDepth = 3,
                    Direction = 270,
                    Opacity = 0.46
                };
                BottomThumbnailList.Tag = 34d;
            }

            if (BottomThumbnailList.SelectedItem is not null)
                BottomThumbnailList.ScrollIntoView(BottomThumbnailList.SelectedItem);
        }

        private void UpdateThumbnailBarVisibility() => ApplyThumbnailBarMode();

        private void SelectToolbarPlacement(ToolbarPlacement placement)
        {
            if (_syncingViewerChromeSettings) return;
            _toolbarPlacement = placement;
            App.SetSetting("ToolbarPlacement", placement.ToString());
            ApplyToolbarPlacement();
            SyncViewerChromeSettings();
        }

        private void SelectThumbnailBarMode(ThumbnailBarMode mode)
        {
            if (_syncingViewerChromeSettings) return;
            _thumbnailBarMode = mode;
            App.SetSetting("ThumbnailBarMode", mode.ToString());
            ApplyThumbnailBarMode();
            UpdateViewerChromeButtons();
            ScheduleViewerRefit();
            SyncViewerChromeSettings();
        }

        private void SelectDocumentDarkMode(bool enabled)
        {
            if (_syncingViewerChromeSettings || _documentDarkMode == enabled)
            {
                UpdateViewerChromeButtons();
                return;
            }

            // LostFocus commits at DispatcherPriority.Background. Commit synchronously before changing the
            // display transform so an in-flight edit cannot be saved with colors from the opposite mode.
            if (_activeTextBox is not null) CommitActiveTextBox();

            _documentDarkMode = enabled;
            App.SetSetting("DocumentDarkMode", enabled ? "1" : "0");

            // Every display renderer gets an isolated PDFium buffer, so cancellation + mode-aware cache
            // keys prevent a late light render from being attached after a dark-mode switch (and vice versa).
            _continuousRenderCts?.Cancel();
            _continuousSharpenCts?.Cancel();
            _secondaryRenderCts?.Cancel();
            _thumbCts?.Cancel();
            _continuousSharpPages.Clear();
            foreach (var session in _sessions) session.RenderCache.Clear();

            UpdatePageSurfaceBackgrounds();
            RerenderForDocumentDarkMode();
            UpdateViewerChromeButtons();
            SyncViewerChromeSettings();
        }

        private void RerenderForDocumentDarkMode()
        {
            if (_doc is null || _currentFile is null)
            {
                UpdateThumbnailBarVisibility();
                return;
            }

            int page = Math.Max(0, Math.Min(_doc.PageCount - 1, PageList.SelectedIndex));
            double oldH = PagePreviewPanel.HorizontalOffset;
            double oldV = PagePreviewPanel.VerticalOffset;

            RefreshPageList();
            PageList.SelectionChanged -= PageList_SelectionChanged;
            PageList.SelectedIndex = page;
            PageList.SelectionChanged += PageList_SelectionChanged;
            _pageJumpBox.Text = (page + 1).ToString();
            _renderedPrimaryPage = -1;

            if (_viewMode == ViewMode.Continuous)
                SetupContinuousView(page, fitDefault: false);
            else
            {
                RenderPage(_viewMode == ViewMode.Grid ? 0 : page);
                ApplyZoom();
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, (Action)(() =>
            {
                PagePreviewPanel.ScrollToHorizontalOffset(oldH);
                PagePreviewPanel.ScrollToVerticalOffset(oldV);
            }));
        }

        private void UpdatePageSurfaceBackgrounds()
        {
            foreach (var child in _pageContentPanel.Children)
                if (child is Border page) page.Background = DocumentPageBackground;
            foreach (var child in _continuousPanel.Children)
                if (child is Border page) page.Background = DocumentPageBackground;
        }

        private void ScheduleViewerRefit()
        {
            if (!IsLoaded || _doc is null) return;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, (Action)(() =>
            {
                if (_fitMode == FitMode.Width) FitToWidth();
                else if (_fitMode == FitMode.Page) FitToPage();
            }));
        }

        private void UpdateViewerChromeButtons()
        {
            if (DocumentDarkModeButton is not null)
                DocumentDarkModeButton.Style = (Style)FindResource(
                    _documentDarkMode ? "ToolbarButtonAccent" : "ToolbarButton");
            if (ThumbnailDockButton is not null)
            {
                ThumbnailDockButton.Content = _thumbnailBarMode == ThumbnailBarMode.Docked ? "\uE77A" : "\uE718";
                ThumbnailDockButton.Style = (Style)FindResource(
                    _thumbnailBarMode == ThumbnailBarMode.Docked ? "ToolbarButtonAccent" : "ToolbarButton");
            }
        }

        private void SyncViewerChromeSettings()
        {
            if (ToolbarDockedRadio is null) return;
            _syncingViewerChromeSettings = true;
            try
            {
                ToolbarDockedRadio.IsChecked = _toolbarPlacement == ToolbarPlacement.Docked;
                ToolbarFloatingRadio.IsChecked = _toolbarPlacement == ToolbarPlacement.Floating;
                ToolbarHiddenRadio.IsChecked = _toolbarPlacement == ToolbarPlacement.Hidden;
                ThumbnailBarFloatingRadio.IsChecked = _thumbnailBarMode == ThumbnailBarMode.Floating;
                ThumbnailBarDockedRadio.IsChecked = _thumbnailBarMode == ThumbnailBarMode.Docked;
                ThumbnailBarHiddenRadio.IsChecked = _thumbnailBarMode == ThumbnailBarMode.Hidden;
                DocumentDarkModeCheck.IsChecked = _documentDarkMode;
            }
            finally { _syncingViewerChromeSettings = false; }
        }

        private void ToolbarDockedRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { IsChecked: true }) SelectToolbarPlacement(ToolbarPlacement.Docked);
        }

        private void ToolbarFloatingRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { IsChecked: true }) SelectToolbarPlacement(ToolbarPlacement.Floating);
        }

        private void ToolbarHiddenRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { IsChecked: true }) SelectToolbarPlacement(ToolbarPlacement.Hidden);
        }

        private void ThumbnailBarFloatingRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { IsChecked: true }) SelectThumbnailBarMode(ThumbnailBarMode.Floating);
        }

        private void ThumbnailBarDockedRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { IsChecked: true }) SelectThumbnailBarMode(ThumbnailBarMode.Docked);
        }

        private void ThumbnailBarHiddenRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { IsChecked: true }) SelectThumbnailBarMode(ThumbnailBarMode.Hidden);
        }

        private void DocumentDarkModeCheck_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb) SelectDocumentDarkMode(cb.IsChecked == true);
        }

        private void DocumentDarkModeButton_Click(object sender, RoutedEventArgs e)
            => SelectDocumentDarkMode(!_documentDarkMode);

        private void ThumbnailDockButton_Click(object sender, RoutedEventArgs e)
            => SelectThumbnailBarMode(_thumbnailBarMode == ThumbnailBarMode.Docked
                ? ThumbnailBarMode.Floating
                : ThumbnailBarMode.Docked);

        private void ThumbnailBarCloseButton_Click(object sender, RoutedEventArgs e)
            => SelectThumbnailBarMode(ThumbnailBarMode.Hidden);

        private void BottomThumbnailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BottomThumbnailList.SelectedItem is null) return;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                (Action)(() => BottomThumbnailList.ScrollIntoView(BottomThumbnailList.SelectedItem)));
        }

        private void BottomThumbnailList_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            _bottomThumbnailScrollViewer ??= FindDescendant<ScrollViewer>(BottomThumbnailList);
            if (_bottomThumbnailScrollViewer is null) return;
            // CanContentScroll uses logical item offsets, not pixels. Two line steps per wheel notch keeps
            // navigation controlled instead of jumping dozens of thumbnails at once.
            int steps = Math.Max(1, Math.Abs(e.Delta) / 120) * 2;
            for (int i = 0; i < steps; i++)
            {
                if (e.Delta > 0) _bottomThumbnailScrollViewer.LineLeft();
                else _bottomThumbnailScrollViewer.LineRight();
            }
            e.Handled = true;
        }
    }
}

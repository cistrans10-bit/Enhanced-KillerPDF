using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace KillerPDF
{
    // Chrome for modal dialog windows: Configure (borderless window setup), Frame (the rounded card +
    // title bar + grain), and BuildTitleBar (the KillerPDF wordmark + red close button).
    internal static class DialogChrome
    {
        // Keep generated dialog captions on the same close mark as the main window.
        // E711 renders noticeably smaller inside the 18x16 Win98 caption face; E8BB is
        // the shared chrome glyph used by the main title bar and fills that face correctly.
        public const string CloseGlyph = "";

        // Brush from the owner (then app) resources, with a safe fallback so the helper never throws.
        private static Brush Brush(Window? owner, string key, Brush fallback)
            => (owner?.TryFindResource(key) ?? Application.Current?.TryFindResource(key)) as Brush ?? fallback;
        private static T Value<T>(Window? owner, string key, T fallback)
            => (owner?.TryFindResource(key) ?? Application.Current?.TryFindResource(key)) is T value ? value : fallback;

        internal static Image TitleIcon(Window? owner = null)
        {
            var frames = BitmapDecoder.Create(new Uri("pack://application:,,,/Resources/kp-icon.ico"),
                BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames;
            var icon = new KillerPDF.Controls.AppIcon
            {
                Width = Value(owner, "TitleIconSize", 27.0),
                Height = Value(owner, "TitleIconSize", 27.0),
                Margin = Value(owner, "TitleIconMargin", new Thickness(0, 0, 7, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                Source = frames[0]
            };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.NearestNeighbor);
            return icon;
        }

        internal static ImageSource? GrainTexture(Window? owner)
        {
            for (Window? window = owner; window is not null; window = window.Owner)
                if (window is MainWindow main && main.GrainTexture is not null)
                    return main.GrainTexture;
            if (Application.Current?.TryFindResource("GrainTileBrush") is ImageBrush tile)
                return tile.ImageSource;
            return null;
        }

        // Builds the title bar.
        //   win       - the window being chromed (used for DragMove on the whole bar)
        //   owner      - supplies the themed brushes + the ChromeCloseButton style (pass the window's owner)
        //   fullTitle  - the complete title, e.g. "KillerPDF - Transform"; the "KillerPDF" part becomes the
        //                wordmark and the remainder (" - Transform") is rendered in the courier title font
        //   onClose    - invoked when the red close button is clicked (e.g. set a result then Close())
        public static Border BuildTitleBar(Window win, Window? owner, string? fullTitle, Action onClose)
        {
            // Transparent (not null) background so the WHOLE bar is hit-testable and acts as a drag handle.
            bool caption = Value(owner, "UseDialogCaption", false);
            var bar = new Border
            {
                Background = caption ? Brush(owner, "TitleBarBrush", Brushes.Navy) : Brushes.Transparent,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            bar.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) win.DragMove(); };

            var grid = new Grid
            {
                // KillerNotes uses the shared title-bar inset for its dialog caption too.  In
                // particular, the 2px top inset keeps the 16px caption button centered in the
                // 20px classic band instead of riding against its upper edge.
                Margin = caption
                    ? Value(owner, "TitleBarPadding", new Thickness(4, 2, 0, 0))
                    : new Thickness(0)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var wordmark = UiKit.WordmarkFont;
            var wordmarkPdf = UiKit.WordmarkFontPdf;

            // Build the wordmark row. A DropShadowEffect applied directly to text rasterizes it and
            // disables ClearType, which reads as blurry. So we LAYER it instead: a blurred black duplicate
            // sits behind a crisp, effect-free copy - soft shadow, sharp text. `shadow` paints the duplicate.
            StackPanel BuildWordmark(bool shadow)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                Brush primary   = shadow ? Brushes.Black : Brush(owner, "TextBrush", Brushes.White);
                Brush logo      = shadow ? Brushes.Black : Brush(owner, "AccentLogo", Brushes.LimeGreen);
                Brush secondary = shadow ? Brushes.Black : Brush(owner, "MutedTextBrush", Brushes.Gray);
                int kp = fullTitle?.IndexOf("KillerPDF", StringComparison.Ordinal) ?? -1;
                if (kp >= 0)
                {
                    // Killer + PDF in one TextBlock so the two sizes share a baseline (cohesive wordmark).
                    var logoTb = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
                    logoTb.Inlines.Add(new System.Windows.Documents.Run("Killer") { FontFamily = wordmark, FontWeight = FontWeights.Normal, FontSize = Value(owner, "TitleWordmarkSize", 15.0), Foreground = primary });
                    logoTb.Inlines.Add(new System.Windows.Documents.Run("PDF") { FontFamily = wordmarkPdf, FontWeight = FontWeights.Normal, FontSize = Value(owner, "TitleWordmarkBoldSize", 22.52), Foreground = logo });
                    // The qualifier ("- Transform") rides in the same TextBlock so it shares the
                    // wordmark's baseline instead of centring on its own and floating high.
                    string after = fullTitle![(kp + "KillerPDF".Length)..];
                    if (!string.IsNullOrEmpty(after))
                        logoTb.Inlines.Add(new System.Windows.Documents.Run("  " + after.TrimStart()) { FontFamily = UiKit.BrandSansFont, FontWeight = FontWeights.Normal, FontSize = 18, Foreground = secondary });
                    sp.Children.Add(logoTb);
                }
                else
                {
                    // A title without the wordmark reads as the qualifier does: Killer Sans at 18.
                    sp.Children.Add(new TextBlock { Text = fullTitle ?? "", FontFamily = UiKit.BrandSansFont, FontSize = 18, Foreground = primary, VerticalAlignment = VerticalAlignment.Center });
                }
                return sp;
            }

            var title = new Grid { Margin = caption ? new Thickness(0) : new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            if (caption)
            {
                title.Children.Add(new TextBlock
                {
                    Text = fullTitle ?? "KillerPDF", FontFamily = Value(owner, "ChromeFontFamily", UiKit.UiFont),
                    FontSize = 11, FontWeight = FontWeights.Bold,
                    Foreground = Brush(owner, "ChromeTextBrush", Brushes.White), VerticalAlignment = VerticalAlignment.Center
                });
            }
            else
            {
                var shadowLayer = BuildWordmark(true);
                // The theme's wordmark shadow strength, so a light caption (0) stays clean as the main title bar does.
                shadowLayer.Opacity = Value(owner, "WordmarkShadowOpacity", 0.5);
                shadowLayer.Effect = new BlurEffect { Radius = 2 };
                shadowLayer.RenderTransform = new TranslateTransform(0.7, 1.2);
                title.Children.Add(shadowLayer);
                title.Children.Add(BuildWordmark(false));
                // Same lift as the main title bar: Lacquer's deep descent makes the centred line sit low.
                if (fullTitle?.Contains("KillerPDF", StringComparison.Ordinal) == true)
                    title.RenderTransform = new TranslateTransform(0, -5);
            }
            // The app icon leads the caption, at the main title bar's size and spacing.
            var titleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = title.Margin
            };
            // Negative margins keep the wordmark's deep Lacquer descent out of the layout so the bar
            // never clips its drips (2px imbalance as in the main title bar); the bar sits above the
            // dialog body so the drips can hang over it.
            title.Margin = caption ? new Thickness(0) : new Thickness(0, -18, 0, -22);
            titleRow.Children.Add(TitleIcon(owner));
            titleRow.Children.Add(title);
            Grid.SetColumn(titleRow, 0);
            grid.Children.Add(titleRow);

            // The close glyph and its complete raised/pressed face live in ChromeCloseButton.
            // Supplying another glyph/font/background here was overriding that canonical style and
            // produced the off-centre X and the exposed title-bar pixel seen in classic dialogs.
            var close = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                // The hover face is part of the card's top-right corner. Centering a 26px
                // button in the 40px caption left a visible 7px strip above it.
                VerticalAlignment = VerticalAlignment.Top,
                Background = Brushes.Transparent,
                Cursor = Cursors.Arrow,
                FocusVisualStyle = null,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            // Dialogs use the family close (a bare X that turns red on hover, as on the About card);
            // only the classic caption themes keep the raised caption-style close.
            if (!caption && owner?.TryFindResource("OverlayCloseButton") is Style overlayClose)
            {
                close.Style = overlayClose;
                close.SetResourceReference(ContentControl.ContentProperty, "AboutCloseGlyph");
                close.VerticalAlignment = VerticalAlignment.Center;
            }
            else if (owner?.TryFindResource("ChromeCloseButton") is Style chromeClose)
            {
                close.Style = chromeClose;
            }
            else
            {
                close.Content = CloseGlyph;
                close.FontFamily = UiKit.IconFont;
                close.FontSize = 10;
                close.Width = 46; close.Height = 36;
                close.Foreground = Brush(owner, "DangerRed", Brushes.Red);
                close.Background = Brushes.Transparent;
                close.BorderThickness = new Thickness(0);
                close.Cursor = Cursors.Arrow;
            }
            close.SetResourceReference(FrameworkElement.WidthProperty, "DialogCloseWidth");
            close.SetResourceReference(FrameworkElement.HeightProperty, "DialogCloseHeight");
            close.SetResourceReference(FrameworkElement.MarginProperty, "DialogCaptionButtonsMargin");
            // Resizable borderless dialogs use WindowChrome. Without this exemption its resize
            // band wins the top-right hit test, turning the close button into a resize handle.
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(close, true);
            // Get the click before the caption's DragMove handler starts its modal mouse loop.
            close.PreviewMouseLeftButtonDown += (_, e) => { e.Handled = true; onClose(); };
            Grid.SetColumn(close, 1);
            grid.Children.Add(close);

            bar.Child = grid;
            Panel.SetZIndex(bar, 1);
            return bar;
        }

        // Borderless transparent window setup shared by every dialog.
        public static void Configure(Window win, Window? owner, bool resizable = false, bool fade = true)
        {
            win.Owner = owner;
            TaskbarIdentity.Track(win);
            win.WindowStyle = WindowStyle.None;
            win.AllowsTransparency = true;
            win.Background = Brushes.Transparent;
            win.ResizeMode = resizable ? ResizeMode.CanResize : ResizeMode.NoResize;
            win.WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen;
            win.FontFamily = UiKit.UiFont;
            TextOptions.SetTextFormattingMode(win, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(win, TextRenderingMode.Grayscale);
            if (fade) WindowFx.EnableFadeClose(win);
        }

        private static Border FrameRing(Window? owner, string brushKey, string thicknessKey, string? marginKey = null)
        {
            var ring = new Border
            {
                IsHitTestVisible = false,
                BorderBrush = Brush(owner, brushKey, Brushes.Transparent),
                BorderThickness = Value(owner, thicknessKey, new Thickness(0))
            };
            if (marginKey != null)
                ring.Margin = Value(owner, marginKey, new Thickness(0));
            return ring;
        }

        private static Grid WindowFrame(Window? owner)
        {
            var frame = new Grid { IsHitTestVisible = false };
            frame.Children.Add(FrameRing(owner, "WindowFrameBrush", "DialogWindowFrameThickness", "WindowFrameMargin"));
            frame.Children.Add(FrameRing(owner, "FrameInnerLightBrush", "FrameInnerLightThickness", "FrameInnerMargin"));
            frame.Children.Add(FrameRing(owner, "FrameInnerDarkBrush", "FrameInnerDarkThickness", "FrameInnerMargin"));
            frame.Children.Add(FrameRing(owner, "FrameOuterLightBrush", "FrameOuterLightThickness"));
            frame.Children.Add(FrameRing(owner, "FrameOuterDarkBrush", "FrameOuterDarkThickness"));
            return frame;
        }

        internal static UIElement WrapContent(
            Window? owner, UIElement content, Thickness? haloMargin = null)
        {
            var host = new Grid
            {
                Margin = haloMargin ?? Value(owner, "DialogHaloMargin", new Thickness(12))
            };
            var radius = Value(owner, "WindowCornerRadius", new CornerRadius(7));
            var stroke = Value(owner, "DialogFrameThickness", new Thickness(1));
            var shadowFill = Brush(owner, "WindowFrameBrush", UiKit.Brush("MenuBackgroundBrush"));
            host.Children.Add(new Border
            {
                Background = shadowFill,
                BorderBrush = shadowFill,
                BorderThickness = stroke,
                CornerRadius = radius,
                IsHitTestVisible = false,
                Effect = UiKit.ShadowDialog()
            });
            // Preserve content positions while the frame clips to its inner stroke edge.
            var layers = new Grid
            {
                Margin = new Thickness(-stroke.Left, -stroke.Top, -stroke.Right, -stroke.Bottom)
            };
            layers.Children.Add(new Border
            {
                Background = Brush(owner, "BackgroundBrush", UiKit.Brush("BackgroundBrush")),
                Margin = Value(owner, "DialogWindowFramePadding", new Thickness(0)),
                Child = content
            });
            layers.Children.Add(WindowFrame(owner));
            host.Children.Add(new Controls.PickerBorder
            {
                Background = Brush(owner, "BackgroundBrush", UiKit.Brush("BackgroundBrush")),
                BorderBrush = Brush(owner, "DialogFrameBrush", UiKit.Brush("MenuBorderBrush")),
                BorderThickness = stroke,
                CornerRadius = radius,
                Child = layers
            });
            return host;
        }

        // Standard dialog: content is inset from the same five-layer frame used by KillerNotes.
        public static UIElement Frame(
            Window win, Window? owner, string title, Action onClose, UIElement body,
            Thickness? haloMargin = null)
        {
            win.KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; onClose(); } };

            var card = new Border
            {
                // Print Preview already used BackgroundBrush directly. The shared frame used
                // MenuBackgroundBrush, making every other generated window a different color.
                Background = Brush(owner, "BackgroundBrush", UiKit.Brush("BackgroundBrush")),
                CornerRadius = UiKit.RadWindow,
                Margin = Value(owner, "WindowFramePadding", new Thickness(0))
            };

            var root = new DockPanel();
            var titleBar = BuildTitleBar(win, owner, title, onClose);
            titleBar.Height = Value(owner, "DialogTitleBarHeight", 40.0);
            DockPanel.SetDock(titleBar, Dock.Top);
            root.Children.Add(titleBar);
            root.Children.Add(body);

            var grain = GrainTexture(owner);
            if (grain != null)
            {
                var grid = new Grid();
                double op = Application.Current?.Resources["GrainOpacity"] is double go ? go : 0.05;
                grid.Children.Add(new Border
                {
                    CornerRadius = UiKit.RadWindow, IsHitTestVisible = false, Opacity = op,
                    Background = new ImageBrush(grain) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 256, 256), Stretch = Stretch.None }
                });
                grid.Children.Add(root);
                card.Child = grid;
            }
            else
            {
                var grid = new Grid();
                grid.Children.Add(root);
                card.Child = grid;
            }
            var framedContent = card.Child!;
            card.Child = null;
            return WrapContent(owner, framedContent, haloMargin);
        }

        internal static void AddBevels(Grid grid, Window? owner)
        {
            grid.Children.Add(new Border { IsHitTestVisible = false, BorderBrush = Brush(owner, "BevelLightBrush", Brushes.Transparent), BorderThickness = Value(owner, "BevelLightThickness", new Thickness(0)) });
            grid.Children.Add(new Border { IsHitTestVisible = false, BorderBrush = Brush(owner, "BevelDarkBrush", Brushes.Transparent), BorderThickness = Value(owner, "BevelDarkThickness", new Thickness(0)) });
        }
    }
}

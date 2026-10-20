using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KillerPDF.Services;

namespace KillerPDF
{
    // PdfCraft's shell around KillerPDF's document view (github.com/storytold/pdfcraft):
    //
    //   tab strip   home | document tab | + Open ................ help  light/dark | window buttons
    //   mode bar    Menu  All tools  Read  Edit  Convert  E-Sign   (Find tools and commands)  i  save  print
    //   toolbar     KillerPDF's own toolbar, kept as the contextual strip under the mode bar
    //   body        All tools panel | document (+ floating tool palette) | pages panel | rail
    //
    // Everything here drives existing commands; nothing new happens to a document. The XAML lives
    // in MainWindow.xaml (CraftDocTab, CraftModeBar, CraftToolsPanel, CraftToolPalette) and the
    // control styles in Themes/Craft/CraftStyles.xaml.
    public partial class MainWindow
    {
        [Flags]
        private enum CraftModes { None = 0, Edit = 1, Convert = 2, Sign = 4 }

        /// <summary>One row of the All tools panel. Keywords are matched (with the localized
        /// label) by the "Find tools and commands" field.</summary>
        private sealed record CraftTool(string LabelKey, string Icon, Color Hue, CraftModes Modes, Action Run, string Keywords);

        // PdfCraft's tool hues (crates/engine/src/catalog.rs).
        private static readonly Color CraftRed    = Color.FromRgb(0xE0, 0x3E, 0x3E);
        private static readonly Color CraftPurple = Color.FromRgb(0x8E, 0x4E, 0xE6);
        private static readonly Color CraftBlue   = Color.FromRgb(0x3A, 0x6F, 0xE8);
        private static readonly Color CraftGreen  = Color.FromRgb(0x2D, 0x9D, 0x5B);
        private static readonly Color CraftOrange = Color.FromRgb(0xE8, 0x8A, 0x1A);
        private static readonly Color CraftTeal   = Color.FromRgb(0x14, 0x9C, 0xA8);
        private static readonly Color CraftPink   = Color.FromRgb(0xD6, 0x3B, 0x8F);

        /// <summary>Rows shown in All tools before "View more", as in PdfCraft.</summary>
        private const int CraftToolsCollapsedCount = 14;

        private List<CraftTool>? _craftTools;
        private string _craftMode = "all";
        private bool _craftToolsExpanded;
        private bool _craftToolsPanelClosed;
        private bool _craftShellReady;

        private IReadOnlyList<CraftTool> CraftTools => _craftTools ??= BuildCraftTools();

        private List<CraftTool> BuildCraftTools()
        {
            var e = new RoutedEventArgs();
            const CraftModes Edit = CraftModes.Edit, Convert = CraftModes.Convert, Sign = CraftModes.Sign;
            return
            [
                new("Str_Craft_Tool_Export",      "file-output",         CraftBlue,   Convert,        () => ExportImages_Click(this, e),               "export image png jpeg pages"),
                new("Str_Craft_Tool_FillSign",    "pen-line",            CraftPurple, Sign | Edit,    () => ToolSignature_Click(ToolSignatureBtn, e),  "fill sign signature initials"),
                new("Str_Craft_Tool_Edit",        "file-pen-line",       CraftPink,   Edit,           () => ToolText_Click(ToolTextBtn, e),            "edit text type write"),
                new("Str_Craft_Tool_Create",      "file-plus-2",         CraftRed,    Convert,        () => New_Click(this, e),                        "create new blank document"),
                new("Str_Craft_Tool_Combine",     "files",               CraftBlue,   Edit | Convert, () => Merge_Click(this, e),                      "combine merge join files"),
                new("Str_Craft_Tool_Organize",    "layout-grid",         CraftGreen,  Edit,           () => SetViewMode(ViewMode.Grid),                "organize pages grid reorder"),
                new("Str_Craft_Tool_Comment",     "message-square-text", CraftOrange, Edit,           () => ToolHighlight_Click(ToolHighlightBtn, e),  "comment highlight annotate markup"),
                new("Str_Craft_Tool_Ocr",         "scan-text",           CraftGreen,  Convert,        () => Ocr_Click(OcrBtn, e),                      "scan ocr recognize text"),
                new("Str_Craft_Tool_Extract",     "scissors",            CraftBlue,   Edit | Convert, () => Split_Click(this, e),                      "extract split pages"),
                new("Str_Craft_Tool_Compress",    "file-down",           CraftRed,    Convert,        () => CompressToZip_Click(this, e),              "compress zip archive"),
                new("Str_Craft_Tool_Form",        "text-cursor-input",   CraftPurple, Sign | Edit,    () => ToolFormField_Click(ToolFormFieldBtn, e),  "prepare form field"),
                new("Str_Craft_Tool_Stamp",       "stamp",               CraftPurple, Sign | Edit,    () => ToolStamp_Click(ToolStampBtn, e),          "stamp watermark page number bates"),
                new("Str_Craft_Tool_Certificate", "badge-check",         CraftTeal,   Sign,           OpenSignDialog,                                  "certificate digital signature"),
                new("Str_Craft_Tool_Print",       "printer",             CraftPurple, Convert,        () => Print_Click(this, e),                      "print"),
                // Below the fold ("View more").
                new("Str_Craft_Tool_Measure",     "ruler",               CraftPink,   Edit,           () => ToolMeasure_Click(ToolMeasureBtn, e),      "measure distance area ruler"),
                new("Str_Craft_Tool_Compare",     "git-compare",         CraftPink,   Convert,        () => ComparePdf_Click(ComparePdfBtn, e),        "compare difference diff"),
                new("Str_Craft_Tool_Crop",        "crop",                CraftGreen,  Edit,           () => ToolCrop_Click(ToolCropBtn, e),            "crop trim pages"),
                new("Str_Craft_Tool_Image",       "image-plus",          CraftBlue,   Edit,           () => ToolImage_Click(ToolImageBtn, e),          "image picture insert"),
                new("Str_Craft_Tool_Flatten",     "layers",              CraftOrange, Convert,        () => SaveFlattened_Click(this, e),              "flatten rasterize"),
            ];
        }

        // ── Setup ─────────────────────────────────────────────────────────

        /// <summary>Wires the PdfCraft shell. Called once from the constructor.</summary>
        private void InitCraftShell()
        {
            ThemeManager.ThemeChanged += SyncCraftThemeToggle;
            SyncCraftThemeToggle();

            // The document tab, the palette and the tool rows follow the open document. The file
            // name is the one value every open, close and tab switch already updates.
            DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock))
                ?.AddValueChanged(FileNameLabel, (_, _) => UpdateCraftChrome());

            // Keep the palette clear of the pane's own tab strip when two documents are open.
            Viewer.TabStripBorderCtl.IsVisibleChanged += (_, _) => PlaceCraftPalette();
            Viewer.TabStripBorderCtl.SizeChanged += (_, _) => PlaceCraftPalette();

            _craftShellReady = true;
            RebuildCraftTools();
            ApplyCraftMode();
        }

        // ── Tab strip ─────────────────────────────────────────────────────

        private void CraftThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            // PdfCraft's sun/moon: flip between the two workbench themes. From any other theme
            // it goes to Light, the default.
            ThemeManager.Apply(ThemeManager.Current == Theme.Light ? Theme.Dark : Theme.Light);
        }

        private void SyncCraftThemeToggle()
        {
            if (CraftThemeToggleIcon is null) return;
            // The icon shows where the click goes: a moon while light, a sun while dark.
            string icon = ThemeManager.Current == Theme.Dark ? "Lucide.sun" : "Lucide.moon";
            if (TryFindResource(icon) is Geometry g) CraftThemeToggleIcon.Data = g;
        }

        // ── Mode bar ──────────────────────────────────────────────────────

        private void CraftMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { Tag: string mode }) return;
            // Clicking a mode always brings its panel back, including the one already selected
            // after the panel was closed with its x.
            if (mode != "read") _craftToolsPanelClosed = false;
            _craftMode = mode;
            _craftToolsExpanded = false;
            if (CraftSearchBox.Text.Length > 0) CraftSearchBox.Text = "";
            ApplyCraftMode();
        }

        private void ApplyCraftMode()
        {
            if (!_craftShellReady) return;
            bool read = _craftMode == "read";

            // Read: just the document. Every other mode has its tools panel and the toolbar.
            if (read && _currentTool != EditTool.Select) SetTool(EditTool.Select);
            ToolbarRowBorder.Visibility = read || _toolbarHidden || _fullScreen ? Visibility.Collapsed : Visibility.Visible;
            SyncCraftToolsTitle();
            RebuildCraftTools();
            UpdateCraftChrome();
        }

        /// <summary>Shows or hides the panel and palette for the current mode, document and
        /// window state.</summary>
        private void UpdateCraftChrome()
        {
            if (!_craftShellReady) return;
            bool read = _craftMode == "read";
            bool searching = CraftSearchBox.Text.Length > 0;
            bool panel = !_fullScreen && (searching || (!read && !_craftToolsPanelClosed));
            CraftToolsPanel.Visibility = panel ? Visibility.Visible : Visibility.Collapsed;
            bool hasDoc = Viewer.DocumentRef is not null;
            CraftToolPalette.Visibility = hasDoc && !read && !_fullScreen ? Visibility.Visible : Visibility.Collapsed;
            PlaceCraftPalette();
        }

        private void PlaceCraftPalette()
        {
            var strip = Viewer.TabStripBorderCtl;
            double top = strip.IsVisible ? strip.ActualHeight + 12 : 16;
            CraftToolPalette.Margin = new Thickness(16, top, 0, 0);
        }

        private void ApplyCraftFullScreen(bool entering)
        {
            CraftModeBar.Visibility = entering ? Visibility.Collapsed : Visibility.Visible;
            UpdateCraftChrome();
        }

        /// <summary>Lets the mode bar fit narrow windows: the search field narrows, then goes,
        /// then the three document actions go. The Menu and the mode tabs always stay.</summary>
        private void CraftModeBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double w = e.NewSize.Width;
            // Below 260 the placeholder no longer fits, so the field goes rather than truncating.
            CraftSearchBox.Visibility = w >= 900 ? Visibility.Visible : Visibility.Collapsed;
            CraftSearchBox.Width = w >= 1040 ? 300 : 260;
            CraftModeBarActions.Visibility = w >= 700 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CraftMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = MakeThemedMenu();
            menu.Items.Add(MakeMenuItem(Loc("Str_Lbl_New"), (s, a) => New_Click(s, a), "Ctrl+N", ""));
            menu.Items.Add(MakeMenuItem(Loc("Str_Lbl_Open") + "...", (s, a) => Open_Click(s, a), "Ctrl+O", ""));
            menu.Items.Add(MakeMenuItem(Loc("Str_TT_RecentFiles"), (_, a) => OpenRecent_Click(CraftMenuBtn, a), null, ""));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeMenuItem(Loc("Str_Menu_Save"), (s, a) => Save_Click(s, a), "Ctrl+S", ""));
            menu.Items.Add(MakeMenuItem(Loc("Str_Menu_SaveAs"), (s, a) => SaveAs_Click(s, a), "Ctrl+Shift+S", ""));
            menu.Items.Add(MakeMenuItem(Loc("Str_Lbl_Print") + "...", (s, a) => Print_Click(s, a), "Ctrl+P", ""));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeMenuItem(Loc("Str_Craft_Menu_DocInfo"), (s, a) => DocInfo_Click(s, a), "F4", ""));
            var panel = new MenuItem
            {
                Header = Loc("Str_Craft_Menu_ToolsPanel"),
                IsCheckable = true,
                IsChecked = CraftToolsPanel.Visibility == Visibility.Visible,
            };
            panel.Click += (_, _) => SetCraftToolsPanelOpen(!panel.IsChecked);
            menu.Items.Add(panel);
            var toolbar = new MenuItem
            {
                Header = Loc("Str_Craft_Menu_Toolbar"),
                IsCheckable = true,
                IsChecked = !_toolbarHidden,
                InputGestureText = "Alt+M",
            };
            toolbar.Click += (_, _) => { ToggleToolbarHidden(); ApplyCraftMode(); };
            menu.Items.Add(toolbar);
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeMenuItem(Loc("Str_Craft_Menu_Shortcuts"), (_, a) => ShortcutHelp_Click(CraftMenuBtn, a), "F1", ""));
            menu.Items.Add(MakeMenuItem(Loc("Str_Craft_Menu_About"), (_, _) => ShowAboutOverlay(), "F12", ""));

            menu.PlacementTarget = CraftMenuBtn;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        // ── Find tools and commands ───────────────────────────────────────

        /// <summary>Ctrl+K. False when the field is not on screen (narrow window, full screen),
        /// so the key falls through to whatever else wants it.</summary>
        private bool FocusCraftSearch()
        {
            if (!_craftShellReady || !CraftSearchBox.IsVisible) return false;
            CraftSearchBox.Focus();
            CraftSearchBox.SelectAll();
            return true;
        }

        private void CraftSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Results come from every tool, whatever the mode, so the panel says so.
            SyncCraftToolsTitle();
            RebuildCraftTools();
            UpdateCraftChrome();
        }

        private void SyncCraftToolsTitle()
        {
            string key = CraftSearchBox.Text.Length > 0 ? "Str_Craft_Mode_AllTools" : _craftMode switch
            {
                "edit" => "Str_Craft_Mode_Edit",
                "convert" => "Str_Craft_Mode_Convert",
                "sign" => "Str_Craft_Mode_ESign",
                _ => "Str_Craft_Mode_AllTools",
            };
            CraftToolsTitle.SetResourceReference(TextBlock.TextProperty, key);
        }

        private void CraftSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    // Run the best match, then hand the keyboard back to the document.
                    var first = CraftToolsList.Children.OfType<Button>().FirstOrDefault();
                    CraftSearchBox.Text = "";
                    Keyboard.ClearFocus();
                    first?.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, first));
                    e.Handled = true;
                    break;
                case Key.Escape:
                    CraftSearchBox.Text = "";
                    Keyboard.ClearFocus();
                    e.Handled = true;
                    break;
                case Key.Down:
                    if (CraftToolsList.Children.OfType<Button>().FirstOrDefault() is { } row)
                    {
                        row.Focus();
                        e.Handled = true;
                    }
                    break;
            }
        }

        private static bool CraftToolMatches(CraftTool tool, string label, string query)
        {
            foreach (string word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (label.IndexOf(word, StringComparison.CurrentCultureIgnoreCase) < 0
                    && tool.Keywords.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            return true;
        }

        // ── All tools panel ───────────────────────────────────────────────

        private void CraftToolsClose_Click(object sender, RoutedEventArgs e) => SetCraftToolsPanelOpen(false);

        private void SetCraftToolsPanelOpen(bool open)
        {
            _craftToolsPanelClosed = !open;
            if (!open && CraftSearchBox.Text.Length > 0) CraftSearchBox.Text = "";
            if (open && _craftMode == "read")
            {
                // The panel belongs to the tool modes; opening it from Read goes back to All tools.
                _craftMode = "all";
                CraftModeAllTools.IsChecked = true;
                ApplyCraftMode();
                return;
            }
            UpdateCraftChrome();
        }

        private void CraftToolsMore_Click(object sender, RoutedEventArgs e)
        {
            _craftToolsExpanded = !_craftToolsExpanded;
            RebuildCraftTools();
        }

        private void RebuildCraftTools()
        {
            if (!_craftShellReady) return;
            string query = CraftSearchBox.Text.Trim();
            bool searching = query.Length > 0;

            IEnumerable<CraftTool> tools = CraftTools;
            if (searching)
                tools = tools.Where(t => CraftToolMatches(t, Loc(t.LabelKey), query));
            else
            {
                CraftModes filter = _craftMode switch
                {
                    "edit" => CraftModes.Edit,
                    "convert" => CraftModes.Convert,
                    "sign" => CraftModes.Sign,
                    _ => CraftModes.None,
                };
                if (filter != CraftModes.None) tools = tools.Where(t => t.Modes.HasFlag(filter));
            }
            var list = tools.ToList();

            bool foldable = !searching && _craftMode == "all" && list.Count > CraftToolsCollapsedCount;
            CraftToolsList.Children.Clear();
            foreach (var tool in foldable && !_craftToolsExpanded ? list.Take(CraftToolsCollapsedCount) : list)
                CraftToolsList.Children.Add(MakeCraftToolRow(tool));

            CraftToolsMoreBtn.Visibility = foldable ? Visibility.Visible : Visibility.Collapsed;
            CraftToolsMoreBtn.SetResourceReference(ContentControl.ContentProperty,
                _craftToolsExpanded ? "Str_Craft_ViewLess" : "Str_Craft_ViewMore");
            CraftToolsEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private Button MakeCraftToolRow(CraftTool tool)
        {
            var hue = new SolidColorBrush(tool.Hue);
            hue.Freeze();
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(MakeLucideIcon(tool.Icon, 20, hue));
            var label = new TextBlock { Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.TextProperty, tool.LabelKey);
            row.Children.Add(label);

            var button = new Button { Content = row };
            button.SetResourceReference(StyleProperty, "CraftToolRow");
            AutomationPropertiesHelper.SetName(button, tool.LabelKey);
            button.Click += (_, _) =>
            {
                if (CraftSearchBox.Text.Length > 0) CraftSearchBox.Text = "";
                tool.Run();
            };
            return button;
        }

        /// <summary>A Lucide icon (Themes/Craft/CraftIcons.xaml) sized to <paramref name="size"/>.
        /// Without a stroke it follows the enclosing control's Foreground.</summary>
        private FrameworkElement MakeLucideIcon(string name, double size, Brush? stroke = null)
        {
            var path = new Path();
            if (TryFindResource("CraftIcon") is Style style) path.Style = style;
            if (TryFindResource("Lucide." + name) is Geometry geometry) path.Data = geometry;
            if (stroke is not null) path.Stroke = stroke;
            return new Viewbox
            {
                Width = size,
                Height = size,
                Child = path,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        // ── Floating tool palette ─────────────────────────────────────────

        /// <summary>Lights the palette button for the active tool. Called from SetTool.</summary>
        private void SyncCraftPalette(EditTool tool)
        {
            if (CraftPalSelect is null) return;   // before InitializeComponent
            var map = new (Button btn, EditTool t)[]
            {
                (CraftPalSelect, EditTool.Select),
                (CraftPalText, EditTool.Text),
                (CraftPalHighlight, EditTool.Highlight),
                (CraftPalDraw, EditTool.Draw),
                (CraftPalShape, EditTool.Shape),
                (CraftPalSignature, EditTool.Signature),
            };
            foreach (var (btn, t) in map)
                btn.Tag = t == tool ? "selected" : null;
        }

        // ── Rail ──────────────────────────────────────────────────────────

        /// <summary>PdfCraft's rail opens panels: a click opens that panel (switching the tab if
        /// another one is showing), and a click on the open panel's button closes it.</summary>
        private void CraftRailPanel_Click(object sender, RoutedEventArgs e)
        {
            bool outlines = ReferenceEquals(sender, CraftRailOutline);
            if (!_sidebarCollapsed && _sidebarShowingOutlines == outlines)
            {
                SidebarToggle_Click(sender, e);   // close the open panel
            }
            else
            {
                if (outlines) SwitchSidebarToOutlinesTab();
                else SwitchSidebarToPagesTab();
                if (_sidebarCollapsed) SidebarToggle_Click(sender, e);
            }
            SyncCraftRail();
        }

        /// <summary>Lights the rail button of the open panel. Runs whenever the sidebar's
        /// collapse state or tab changes (UpdateSidebarToggleGlyph).</summary>
        private void SyncCraftRail()
        {
            if (CraftRailPages is null) return;
            CraftRailPages.Tag = !_sidebarCollapsed && !_sidebarShowingOutlines ? "selected" : null;
            CraftRailOutline.Tag = !_sidebarCollapsed && _sidebarShowingOutlines ? "selected" : null;
        }

        // ── Toolbar icons ─────────────────────────────────────────────────

        // The toolbar's buttons drawn with PdfCraft's Lucide set instead of Segoe MDL2 glyphs,
        // keyed by their caption so the glyph table above stays the source of truth.
        private static readonly Dictionary<string, string> CraftToolbarIcons = new(StringComparer.Ordinal)
        {
            ["Str_Lbl_New"] = "file-plus-2",
            ["Str_Lbl_Open"] = "folder-open",
            ["Str_Lbl_Close"] = "file-x",
            ["Str_Lbl_Save"] = "save",
            ["Str_Lbl_Flatten"] = "layers",
            ["Str_Lbl_Ocr"] = "scan-text",
            ["Str_Lbl_Print"] = "printer",
            ["Str_Lbl_Merge"] = "files",
            ["Str_Lbl_Extract"] = "scissors",
            ["Str_Lbl_Delete"] = "trash-2",
            ["Str_Lbl_MoveUp"] = "chevron-up",
            ["Str_Lbl_MoveDown"] = "chevron-down",
            ["Str_Lbl_Select"] = "mouse-pointer-2",
            ["Str_Lbl_Text"] = "type",
            ["Str_Lbl_Highlight"] = "highlighter",
            ["Str_Lbl_Strike"] = "strikethrough",
            ["Str_Lbl_Underline"] = "underline",
            ["Str_Lbl_Line"] = "spline",
            ["Str_Lbl_Shape"] = "pentagon",
            ["Str_Lbl_Draw"] = "pencil",
            ["Str_Lbl_Crop"] = "crop",
            ["Str_Lbl_Rotate"] = "move",
            ["Str_Lbl_Image"] = "image-plus",
            ["Str_Lbl_Signature"] = "signature",
            ["Str_Lbl_Undo"] = "undo-2",
            ["Str_Lbl_Clear"] = "eraser",
            ["Str_Lbl_ZoomOut"] = "zoom-out",
            ["Str_Lbl_ZoomIn"] = "zoom-in",
            ["Str_Lbl_Search"] = "search",
            ["Str_Lbl_Stamp"] = "stamp",
            ["Str_Lbl_Measure"] = "ruler",
            ["Str_Lbl_FormField"] = "text-cursor-input",
            ["Str_TT_ComparePDFs"] = "git-compare",
        };

        /// <summary>The toolbar button's icon: the Lucide equivalent when there is one, else the
        /// original glyph.</summary>
        private FrameworkElement MakeToolbarIcon(string glyph, string labelKey, double glyphSize)
        {
            if (CraftToolbarIcons.TryGetValue(labelKey, out string? name) && TryFindResource("Lucide." + name) is Geometry)
                return MakeLucideIcon(name, glyphSize + 4);
            return MakeToolbarGlyph(glyph, glyphSize);
        }
    }

    /// <summary>Sets an accessible name from a string resource key, kept live across language
    /// switches.</summary>
    internal static class AutomationPropertiesHelper
    {
        internal static void SetName(FrameworkElement element, string resourceKey)
            => element.SetResourceReference(System.Windows.Automation.AutomationProperties.NameProperty, resourceKey);
    }
}

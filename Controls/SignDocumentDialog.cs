using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using KillerPDF.Services.Signing;
using KillerPDF.Services;
using Microsoft.Win32;

namespace KillerPDF
{
    /// <summary>
    /// Themed modal dialog that cryptographically signs the open PDF with a certificate (a .pfx/.p12
    /// file, or one from the Windows store) and writes a NEW signed copy. This is the real digital
    /// signature - distinct from the drawn "Signature" stamp tool, which only places a picture.
    /// Chrome and colors mirror PrintPreviewWindow so every KillerPDF dialog looks identical.
    /// </summary>
    internal sealed class SignDocumentDialog : Window
    {
        private readonly string _sourcePdf;

        private RadioButton _fileRadio = null!;
        private RadioButton _storeRadio = null!;
        private TextBox _pfxBox = null!;
        private PasswordBox _pwBox = null!;
        private Button _browsePfx = null!;
        private ComboBox _storeCombo = null!;
        private TextBox _reasonBox = null!;
        private TextBox _locationBox = null!;
        private TextBox _contactBox = null!;
        private CheckBox _visibleAppearance = null!;
        private TextBox _appearanceText = null!;
        private TextBox _pageBox = null!;
        private ComboBox _positionCombo = null!;
        private TextBox _appearanceWidth = null!;
        private TextBox _appearanceHeight = null!;
        private TextBox _appearanceFontSize = null!;
        private TextBlock _appearancePreview = null!;
        private TextBox _outputBox = null!;
        private readonly List<X509Certificate2> _storeCerts = [];

        // Segoe MDL2 Assets close glyph, matching the main window + print dialog chrome.
        private const string CloseGlyph = "";

        private static SolidColorBrush R(string key) => (SolidColorBrush)Application.Current.Resources[key];

        // Localized string from the active locale dictionary (falls back to the key if missing).
        private static string L(string key) => Application.Current.TryFindResource(key) as string ?? key;

        public SignDocumentDialog(Window? owner, string sourcePdf)
        {
            _sourcePdf = sourcePdf;
            Title = "KillerPDF - " + L("Str_Sign_Name");
            Width = 720;
            MaxHeight = 860;
            SizeToContent = SizeToContent.Height;
            UseLayoutRounding = true;
            DialogChrome.Configure(this, owner);
            BuildUi();
        }

        private void BuildUi()
        {
            var body = new StackPanel { Margin = new Thickness(20, 6, 20, 18) };

            body.Children.Add(new TextBlock
            {
                Text = string.Format(L("Str_Sign_Desc"), Path.GetFileName(_sourcePdf)),
                Foreground = R("MutedTextBrush"), FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14)
            });

            // --- Certificate source --------------------------------------------------------------
            body.Children.Add(Label(L("Str_Sign_Certificate")));

            _fileRadio = Radio(L("Str_Sign_FromFile"), true);
            _storeRadio = Radio(L("Str_Sign_FromStore"), false);
            _fileRadio.Checked += (_, _) => SyncSource();
            _storeRadio.Checked += (_, _) => SyncSource();
            body.Children.Add(_fileRadio);

            var fileRow = new Grid { Margin = new Thickness(20, 2, 0, 4) };
            fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fileRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _pfxBox = Field("");
            _pfxBox.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(_pfxBox, 0);
            _browsePfx = MakeButton(L("Str_Sign_Browse"), false);
            _browsePfx.Click += (_, _) => BrowsePfx();
            Grid.SetColumn(_browsePfx, 1);
            fileRow.Children.Add(_pfxBox);
            fileRow.Children.Add(_browsePfx);
            body.Children.Add(fileRow);

            body.Children.Add(new TextBlock { Text = L("Str_Sign_Password"), Foreground = R("MutedTextBrush"), FontSize = 11, Margin = new Thickness(20, 4, 0, 2) });
            _pwBox = new PasswordBox
            {
                Margin = new Thickness(20, 0, 0, 10),
                Background = R("BgCanvas"), Foreground = R("TextBrush"),
                BorderBrush = R("CardBorderBrush"), BorderThickness = new Thickness(1),
                CaretBrush = R("TextBrush"), Template = MakePasswordTemplate()
            };
            body.Children.Add(_pwBox);

            body.Children.Add(_storeRadio);
            _storeCombo = new ComboBox { Margin = new Thickness(20, 2, 0, 10), Height = 26 };
            ApplyComboStyle(_storeCombo);
            try
            {
                foreach (var c in WindowsCertificateStore.ListSigningCertificates())
                {
                    _storeCerts.Add(c);
                    _storeCombo.Items.Add(new StoreCertificateProvider(c).DisplayName);
                }
            }
            catch { /* store unavailable - leave empty */ }
            if (_storeCombo.Items.Count > 0) _storeCombo.SelectedIndex = 0;
            body.Children.Add(_storeCombo);

            // --- Metadata ------------------------------------------------------------------------
            body.Children.Add(Label(L("Str_Sign_Reason")));
            _reasonBox = Field(""); body.Children.Add(_reasonBox);
            body.Children.Add(Label(L("Str_Sign_Location")));
            _locationBox = Field(""); body.Children.Add(_locationBox);
            body.Children.Add(Label(L("Str_Sign_Contact")));
            _contactBox = Field(""); body.Children.Add(_contactBox);

            // --- Visible appearance -------------------------------------------------------------
            _visibleAppearance = new CheckBox
            {
                Content = L("Str_Sign_VisibleAppearance"),
                IsChecked = true,
                Foreground = R("TextBrush"),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 4)
            };
            if (FindOwnerStyle("ThemeCheckBox") is Style checkStyle)
                _visibleAppearance.Style = checkStyle;
            body.Children.Add(_visibleAppearance);

            var appearanceGrid = new Grid { Margin = new Thickness(20, 0, 0, 4) };
            appearanceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            appearanceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            var appearanceControls = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
            appearanceControls.Children.Add(Label(L("Str_Sign_AppearanceText")));
            _appearanceText = Field(L("Str_Sign_AppearanceDefault"));
            _appearanceText.AcceptsReturn = true;
            _appearanceText.Height = 88;
            _appearanceText.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            appearanceControls.Children.Add(_appearanceText);

            var placement = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            for (int i = 0; i < 4; i++)
                placement.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            placement.Children.Add(new TextBlock { Text = L("Str_Sign_Page"), Foreground = R("MutedTextBrush"), VerticalAlignment = VerticalAlignment.Center });
            _pageBox = Field("1"); _pageBox.Width = 48; _pageBox.Margin = new Thickness(6, 0, 12, 0);
            Grid.SetColumn(_pageBox, 1); placement.Children.Add(_pageBox);
            var positionLabel = new TextBlock { Text = L("Str_Sign_Position"), Foreground = R("MutedTextBrush"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(positionLabel, 2); placement.Children.Add(positionLabel);
            _positionCombo = new ComboBox { Width = 130, Height = 26, Margin = new Thickness(6, 0, 0, 0) };
            foreach (string key in new[] { "Str_Sign_BottomLeft", "Str_Sign_BottomRight", "Str_Sign_TopLeft", "Str_Sign_TopRight" })
                _positionCombo.Items.Add(L(key));
            _positionCombo.SelectedIndex = 0; ApplyComboStyle(_positionCombo);
            Grid.SetColumn(_positionCombo, 3); placement.Children.Add(_positionCombo);
            appearanceControls.Children.Add(placement);

            var dimensions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            dimensions.Children.Add(DimensionField(L("Str_Sign_Width"), "240", out _appearanceWidth));
            dimensions.Children.Add(DimensionField(L("Str_Sign_Height"), "84", out _appearanceHeight));
            dimensions.Children.Add(DimensionField(L("Str_Sign_FontSize"), "10", out _appearanceFontSize));
            appearanceControls.Children.Add(dimensions);
            appearanceGrid.Children.Add(appearanceControls);

            var previewBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = R("AccentBrush"),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(10),
                Height = 122,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _appearancePreview = new TextBlock
            {
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            previewBorder.Child = _appearancePreview;
            Grid.SetColumn(previewBorder, 1); appearanceGrid.Children.Add(previewBorder);
            body.Children.Add(appearanceGrid);

            _visibleAppearance.Checked += (_, _) => SyncAppearance();
            _visibleAppearance.Unchecked += (_, _) => SyncAppearance();
            _appearanceText.TextChanged += (_, _) => UpdateAppearancePreview();
            _reasonBox.TextChanged += (_, _) => UpdateAppearancePreview();
            _locationBox.TextChanged += (_, _) => UpdateAppearancePreview();

            // --- Output --------------------------------------------------------------------------
            body.Children.Add(Label(L("Str_Sign_SaveAs")));
            var outRow = new Grid();
            outRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _outputBox = Field(DefaultOutputPath());
            _outputBox.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(_outputBox, 0);
            var browseOut = MakeButton(L("Str_Sign_Browse"), false);
            browseOut.Click += (_, _) => BrowseOutput();
            Grid.SetColumn(browseOut, 1);
            outRow.Children.Add(_outputBox);
            outRow.Children.Add(browseOut);
            body.Children.Add(outRow);

            // --- Buttons -------------------------------------------------------------------------
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var sign = MakeButton(L("Str_Sign_Sign"), true);
            sign.Click += (_, _) => DoSign();
            sign.IsDefault = true;    // Enter
            var cancel = MakeButton(L("Str_Sign_Cancel"), false);
            cancel.Margin = new Thickness(8, 0, 0, 0);
            cancel.Click += (_, _) => { DialogResult = false; Close(); };
            cancel.IsCancel = true;   // Esc
            btnRow.Children.Add(sign);
            btnRow.Children.Add(cancel);
            body.Children.Add(btnRow);

            SyncSource();
            SyncAppearance();

            var scroll = new ScrollViewer
            {
                Content = body,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Content = DialogChrome.Frame(this, Owner, "KillerPDF - " + L("Str_Sign_TitleSuffix"),
                () => { DialogResult = false; Close(); }, scroll);
        }

        private string DefaultOutputPath()
        {
            string dir = Path.GetDirectoryName(_sourcePdf) ?? "";
            string name = Path.GetFileNameWithoutExtension(_sourcePdf);
            return Path.Combine(dir, name + "-signed.pdf");
        }

        // Enable only the inputs for the selected certificate source.
        private void SyncSource()
        {
            bool file = _fileRadio.IsChecked == true;
            _pfxBox.IsEnabled = _browsePfx.IsEnabled = _pwBox.IsEnabled = file;
            _storeCombo.IsEnabled = !file;
        }

        private void BrowsePfx()
        {
            var dlg = new KillerPDF.Controls.FileDialog(KillerPDF.Controls.FileDialogMode.Open)
                          { Filter = L("Str_Filter_Cert") + "|*.pfx;*.p12|" + L("Str_Filter_AllFiles") + "|*.*", Title = L("Str_Sign_ChooseCert") };
            if (dlg.ShowDialog(this) == true) _pfxBox.Text = dlg.FileName;
        }

        private void BrowseOutput()
        {
            var dlg = new KillerPDF.Controls.FileDialog(KillerPDF.Controls.FileDialogMode.Save)
                          { Filter = L("Str_Filter_Pdf") + "|*.pdf", Title = L("Str_Sign_SaveAs"), FileName = Path.GetFileName(_outputBox.Text) };
            if (dlg.ShowDialog(this) == true) _outputBox.Text = dlg.FileName;
        }

        private void DoSign()
        {
            ICertificateProvider provider;
            if (_fileRadio.IsChecked == true)
            {
                string pfx = _pfxBox.Text?.Trim() ?? "";
                if (!File.Exists(pfx)) { Warn(L("Str_Sign_NeedCertFile")); return; }
                provider = new PfxFileCertificateProvider(pfx, _pwBox.Password);
            }
            else
            {
                int i = _storeCombo.SelectedIndex;
                if (i < 0 || i >= _storeCerts.Count) { Warn(L("Str_Sign_NoStoreCert")); return; }
                provider = new StoreCertificateProvider(_storeCerts[i]);
            }

            string output = _outputBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(output)) { Warn(L("Str_Sign_NeedOutput")); return; }

            X509Certificate2 cert;
            try { cert = provider.GetCertificate(); }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // The raw Win32 text ("The specified network password is not correct.") is misleading -
                // nothing networked is involved. Almost always a wrong password or a non-.pfx file.
                Warn(L("Str_Sign_BadCert"));
                return;
            }
            catch (Exception ex) { Warn(L("Str_Sign_CertLoadFailed") + "\n\n" + ex.Message); return; }

            PdfSigner.VisibleSignatureInfo? appearance;
            try
            {
                if (!TryBuildVisibleAppearance(cert, out appearance)) return;
            }
            catch (Exception ex)
            {
                Warn(L("Str_Sign_BadAppearance") + "\n\n" + ex.Message);
                return;
            }
            try
            {
                new PdfSigner().Sign(_sourcePdf, output, cert,
                    new PdfSigner.SignInfo(_reasonBox.Text ?? "", _locationBox.Text ?? "",
                        _contactBox.Text ?? "", appearance));
            }
            catch (Exception ex)
            {
                Warn(L("Str_Sign_Failed") + "\n\n" + ex.GetType().Name + ": " + ex.Message);
                return;
            }

            KillerDialog.Show(this, L("Str_Dlg_SignedSavedTo") + "\n" + output, L("Str_Sign_Name"), MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void Warn(string msg) => KillerDialog.Show(this, msg, L("Str_Sign_Name"), MessageBoxButton.OK, MessageBoxImage.Warning);

        private static StackPanel DimensionField(string label, string value, out TextBox box)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };
            panel.Children.Add(new TextBlock { Text = label, Foreground = R("MutedTextBrush"), VerticalAlignment = VerticalAlignment.Center });
            box = Field(value); box.Width = 48; box.Margin = new Thickness(5, 0, 0, 0);
            panel.Children.Add(box);
            return panel;
        }

        private void SyncAppearance()
        {
            bool enabled = _visibleAppearance.IsChecked == true;
            _appearanceText.IsEnabled = _pageBox.IsEnabled = _positionCombo.IsEnabled =
                _appearanceWidth.IsEnabled = _appearanceHeight.IsEnabled =
                _appearanceFontSize.IsEnabled = enabled;
            _appearancePreview.Opacity = enabled ? 1 : 0.35;
            UpdateAppearancePreview();
        }

        private void UpdateAppearancePreview()
        {
            if (_appearancePreview is null || _appearanceText is null) return;
            _appearancePreview.Text = ExpandAppearanceText(
                _appearanceText.Text, L("Str_Sign_PreviewSigner"));
        }

        private string ExpandAppearanceText(string template, string signerName) => template
            .Replace("{name}", signerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", DateTimeOffset.Now.ToString("g", CultureInfo.CurrentCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{reason}", _reasonBox.Text ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{location}", _locationBox.Text ?? "", StringComparison.OrdinalIgnoreCase);

        private bool TryBuildVisibleAppearance(X509Certificate2 certificate,
            out PdfSigner.VisibleSignatureInfo? appearance)
        {
            appearance = null;
            if (_visibleAppearance.IsChecked != true) return true;
            if (!int.TryParse(_pageBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int pageNumber))
            { Warn(L("Str_Sign_BadAppearance")); return false; }
            var pages = PdfEngineIntegration.ReadPageInformation(_sourcePdf);
            if (pageNumber < 1 || pageNumber > pages.Count)
            { Warn(L("Str_Sign_BadAppearance")); return false; }
            if (!TryDimension(_appearanceWidth, out double width)
                || !TryDimension(_appearanceHeight, out double height)
                || !TryDimension(_appearanceFontSize, out double fontSize)
                || width < 72 || height < 36 || fontSize is < 6 or > 72)
            { Warn(L("Str_Sign_BadAppearance")); return false; }

            var page = pages[pageNumber - 1];
            const double margin = 36;
            width = Math.Min(width, Math.Max(1, page.Width - margin * 2));
            height = Math.Min(height, Math.Max(1, page.Height - margin * 2));
            bool right = _positionCombo.SelectedIndex is 1 or 3;
            bool top = _positionCombo.SelectedIndex is 2 or 3;
            double left = right ? page.Width - margin - width : margin;
            double bottom = top ? page.Height - margin - height : margin;
            string signer = certificate.GetNameInfo(X509NameType.SimpleName, false);
            if (string.IsNullOrWhiteSpace(signer)) signer = certificate.Subject;
            appearance = new PdfSigner.VisibleSignatureInfo(pageNumber - 1, left, bottom, width, height,
                fontSize, ExpandAppearanceText(_appearanceText.Text, signer));
            return true;

            static bool TryDimension(TextBox box, out double value) =>
                double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                && double.IsFinite(value);
        }

        // ---- themed control helpers (mirroring PrintPreviewWindow) -------------------------------
        private Style? FindOwnerStyle(string key) => Owner?.TryFindResource(key) as Style;

        private static TextBlock Label(string text) => new()
        { Text = text, Foreground = R("TextBrush"), FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };

        private RadioButton Radio(string text, bool isChecked)
        {
            var r = new RadioButton { Content = text, IsChecked = isChecked, GroupName = "CertSource", FontSize = 12, Margin = new Thickness(0, 4, 0, 2) };
            if (FindOwnerStyle("ThemeRadio") is Style s) r.Style = s; else r.Foreground = R("TextBrush");
            return r;
        }

        private void ApplyComboStyle(ComboBox combo)
        {
            if (FindOwnerStyle("DarkComboBox") is Style s) combo.Style = s;
            else { combo.Foreground = R("TextBrush"); combo.BorderBrush = R("CardBorderBrush"); }
            combo.Background = R("BgCanvas");
        }

        private static TextBox Field(string text)
        {
            var tb = new TextBox
            {
                Text = text, Margin = new Thickness(0, 0, 0, 4),
                Background = R("BgCanvas"), Foreground = R("TextBrush"),
                BorderBrush = R("CardBorderBrush"), BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4), CaretBrush = R("TextBrush"),
                SelectionBrush = R("RowSelectedBrush"), SelectionTextBrush = R("TextBrush"),
                Template = MakeTextBoxTemplate()
            };
            return tb;
        }

        private static ControlTemplate MakeTextBoxTemplate()
        {
            var b = new FrameworkElementFactory(typeof(Border));
            b.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            b.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            b.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            var sv = new FrameworkElementFactory(typeof(ScrollViewer)) { Name = "PART_ContentHost" };
            b.AppendChild(sv);
            var ct = new ControlTemplate(typeof(TextBox)) { VisualTree = b };
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
            ct.Triggers.Add(disabled);
            return ct;
        }

        private static ControlTemplate MakePasswordTemplate()
        {
            var b = new FrameworkElementFactory(typeof(Border));
            b.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            b.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            b.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            b.SetValue(Border.PaddingProperty, new Thickness(6, 4, 6, 4));
            var sv = new FrameworkElementFactory(typeof(ScrollViewer)) { Name = "PART_ContentHost" };
            b.AppendChild(sv);
            var ct = new ControlTemplate(typeof(PasswordBox)) { VisualTree = b };
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.4));
            ct.Triggers.Add(disabled);
            return ct;
        }

        private static Button MakeButton(string label, bool accent) => UiKit.Make(label, accent);
    }
}

# Run with PowerShell 7 on .NET 10 after a normal or KillerPayloadBuild release build.
# Loads the actual app resources and picker handlers with preferences redirected to owned evidence.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory
)
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Run with pwsh -Sta.' }
if ([Environment]::Version.Major -ne 10) { throw 'Use PowerShell on .NET 10.' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'Use a fresh evidence directory.' }
$null = [IO.Directory]::CreateDirectory($EvidenceDirectory)
$appDirectory = Split-Path -Parent $AssemblyPath
$desktop = @(& dotnet --list-runtimes | ForEach-Object {
    if ($_ -match '^Microsoft\.WindowsDesktop\.App (10\.\d+\.\d+) \[(.+)\]$') {
        [pscustomobject]@{ Version = [version]$Matches[1]; Path = Join-Path $Matches[2] $Matches[1] }
    }
} | Sort-Object Version -Descending)[0].Path
$resolver = {
    param($context, $name)
    foreach ($root in @($appDirectory, $desktop)) {
        $candidate = Join-Path $root ($name.Name + '.dll')
        if (Test-Path -LiteralPath $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    }
    return $null
}.GetNewClosure()
[Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolver)
foreach ($library in @('WindowsBase', 'PresentationCore', 'PresentationFramework')) {
    Add-Type -Path (Join-Path $desktop ($library + '.dll'))
}
$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$static = [Reflection.BindingFlags]'Static,Public,NonPublic'
$instance = [Reflection.BindingFlags]'Instance,Public,NonPublic'
[Windows.Application].GetField('_resourceAssembly', $static).SetValue($null, $assembly)
$paths = $assembly.GetType('KillerPDF.Services.AppDataPaths', $true)
$paths.GetProperty('StartupSmokeRoot', $static).SetValue($null, (Join-Path $EvidenceDirectory 'data'))
$app = [Activator]::CreateInstance($assembly.GetType('KillerPDF.App', $true))
$app.ShutdownMode = [Windows.ShutdownMode]::OnExplicitShutdown
$app.InitializeComponent()
$manager = $assembly.GetType('KillerPDF.Services.ThemeManager', $true)
$themes = $assembly.GetType('KillerPDF.Services.Theme', $true)
$accents = $assembly.GetType('KillerPDF.Services.DarkAccent', $true)
foreach ($service in @('ThemeManager', 'LocaleManager', 'FilmGrain')) {
    $assembly.GetType("KillerPDF.Services.$service", $true).GetMethod('Initialize', $static).Invoke($null, $null) | Out-Null
}
$windowType = $assembly.GetType('KillerPDF.MainWindow', $true)
$window = $null
$states = 0
$brushes = 0
$swatches = [Windows.Controls.WrapPanel]::new()
$swatches.Width = 400
function Call-Window($name, [object[]]$arguments) {
    $windowType.GetMethod($name, $instance).Invoke($window, $arguments) | Out-Null
}
function Open-Picker {
    $menu = $window.FindName('ThemeFlyout')
    $menu.IsOpen = $false
    Call-Window 'RailTheme_Click' @($window.FindName('RailThemeBtn'), [Windows.RoutedEventArgs]::new())
    if (!$menu.IsOpen) { throw 'Theme picker did not open.' }
    $menu.IsOpen = $false
}
try {
    $window = [Activator]::CreateInstance($windowType)
    # First open exercises RailTheme_Click -> ToggleRailFlyout -> SyncPickerState ->
    # radio Checked -> SelectTheme -> UpdateAccentStrip -> AccentStripBrush.
    Open-Picker
    $getBrush = $windowType.GetMethod('AccentStripBrush', $static)
    $load = $manager.GetMethod('LoadDict', $static)
    foreach ($themeName in [Enum]::GetNames($themes)) {
        $theme = [Enum]::Parse($themes, $themeName)
        Call-Window 'SelectTheme' @($theme)
        Open-Picker
        Open-Picker
        $states++
        if ($themeName -notin @('Dark', 'Light', 'Black', 'SE98')) { continue }
        foreach ($accent in [Enum]::GetValues($accents)) {
            $dot = [Windows.Controls.Border]::new()
            $dot.Tag = $accent.ToString()
            Call-Window 'HandleAccentDot' @($dot, $theme)
            Open-Picker
            $colors = $windowType.GetMethod('StripColorsFor', $static).Invoke($null, @($theme))
            $hex = @($colors | Where-Object { $_.Item1 -eq $accent })[0].Item2
            $brush = $getBrush.Invoke($null, @($theme, $accent, $hex))
            $again = $getBrush.Invoke($null, @($theme, $accent, $hex))
            if (!$brush.IsFrozen -or ![object]::ReferenceEquals($brush, $again)) { throw 'Swatch cache changed.' }
            $load.Invoke($null, @($theme, $accent)) | Out-Null
            $expected = if ($themeName -eq 'SE98') {
                [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($hex))
            } else { $app.Resources['SelectionBg'] }
            if ([Windows.Markup.XamlWriter]::Save($brush) -cne [Windows.Markup.XamlWriter]::Save($expected)) {
                throw "Wrong swatch: $themeName/$accent"
            }
            $states++
            $brushes++
            $tile = [Windows.Controls.Border]::new()
            $tile.Width = 48; $tile.Height = 32; $tile.Margin = [Windows.Thickness]::new(1)
            $tile.Background = $brush
            $swatches.Children.Add($tile) | Out-Null
        }
    }
    $locales = $assembly.GetType('KillerPDF.Services.Locale', $true)
    $localeManager = $assembly.GetType('KillerPDF.Services.LocaleManager', $true)
    $localeCount = 0
    foreach ($localeName in [Enum]::GetNames($locales)) {
        $localeManager.GetMethod('Apply', $static).Invoke($null, @([Enum]::Parse($locales, $localeName))) | Out-Null
        Open-Picker
        Call-Window 'RailLang_Click' @($window.FindName('RailLangBtn'), [Windows.RoutedEventArgs]::new())
        $window.FindName('LangFlyout').IsOpen = $false
        if ([string]::IsNullOrEmpty($app.TryFindResource('Str_Crash_Continue'))) { throw "Missing locale resources: $localeName" }
        $savedLocale = $assembly.GetType('KillerPDF.App').GetMethod('GetSetting', $static).Invoke($null, @('Locale'))
        if ($savedLocale -cne $localeName) { throw 'Locale setting did not persist in the isolated profile.' }
        $localeCount++
    }
    $filmGrain = $assembly.GetType('KillerPDF.Services.FilmGrain', $true)
    foreach ($enabled in @($false, $true, $false)) {
        $filmGrain.GetMethod('Set', $static).Invoke($null, @($enabled)) | Out-Null
        Open-Picker
        $savedGrain = $assembly.GetType('KillerPDF.App').GetMethod('GetSetting', $static).Invoke($null, @('FilmGrain'))
        if ($savedGrain -cne $(if ($enabled) { '1' } else { '0' })) { throw 'Film Grain setting did not persist.' }
        if (!$enabled -and $app.Resources['GrainOpacity'] -ne 0.0) { throw 'Film Grain is still visible.' }
    }
    $swatches.Measure([Windows.Size]::new(400, 136))
    $swatches.Arrange([Windows.Rect]::new(0, 0, 400, 136))
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(400, 136, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($swatches)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $EvidenceDirectory 'swatches.png'))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    $proof = "PASS $states theme/accent states; $brushes frozen cached swatches; first and repeated rail opens, explicit theme and accent handlers; $localeCount locales and persisted Film Grain toggles. Assembly=$($assembly.FullName); SHA256=$((Get-FileHash -LiteralPath $AssemblyPath).Hash)"
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'result.txt'), $proof)
    Write-Output $proof
}
catch {
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'failure.txt'), $_.Exception.ToString())
    throw
}
finally {
    if ($null -ne $window) { $window.FindName('ThemeFlyout').IsOpen = $false; $window.Close() }
    $app.Shutdown()
}


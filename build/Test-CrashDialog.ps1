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

$appType = $assembly.GetType('KillerPDF.App', $true)
$appType.GetMethod('EnsureCrashPreviewGrain', $static).Invoke($null, $null) | Out-Null
$create = $appType.GetMethod('CreateCrashDialog', $static)
$grain = $assembly.GetType('KillerPDF.Services.FilmGrain', $true)
$rows = [Collections.Generic.List[object]]::new()
function Descendants($element) {
    for ($i = 0; $i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($element); $i++) {
        $child = [Windows.Media.VisualTreeHelper]::GetChild($element, $i)
        $child
        Descendants $child
    }
}
function Render-Preview($dialog, $name) {
    $content = $dialog.Content
    $panel = [Windows.Controls.StackPanel]::new()
    $label = [Windows.Controls.TextBlock]::new()
    $label.Text = 'SYNTHETIC OFFSCREEN PREVIEW: ' + $name
    $label.Background = [Windows.Media.Brushes]::White
    $label.Foreground = [Windows.Media.Brushes]::Black
    $label.Padding = [Windows.Thickness]::new(8)
    $panel.Children.Add($label) | Out-Null
    # Render separately so the tested dialog retains its real parent and styles.
    $panel.Measure([Windows.Size]::new($dialog.Width, 30))
    $panel.Arrange([Windows.Rect]::new(0, 0, $dialog.Width, 30))
    $visual = [Windows.Media.DrawingVisual]::new()
    $draw = $visual.RenderOpen()
    try {
        $draw.DrawRectangle([Windows.Media.VisualBrush]::new($panel), $null, [Windows.Rect]::new(0, 0, $dialog.Width, 30))
        $draw.DrawRectangle([Windows.Media.VisualBrush]::new($content), $null, [Windows.Rect]::new(0, 30, $dialog.Width, $dialog.Height))
    } finally { $draw.Close() }
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new([int]$dialog.Width, [int]$dialog.Height + 30, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create((Join-Path $EvidenceDirectory ($name + '.png')))
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}
try {
    foreach ($themeName in [Enum]::GetNames($themes)) {
        $theme = [Enum]::Parse($themes, $themeName)
        $manager.GetMethod('Apply', $static).Invoke($null, @($theme)) | Out-Null
        foreach ($enabled in @($true, $false)) {
            $grain.GetMethod('Set', $static).Invoke($null, @($enabled)) | Out-Null
            foreach ($width in @(760, 620)) {
                foreach ($fatal in @($false, $true)) {
                    $ex = [IO.FileNotFoundException]::new('SYNTHETIC OFFSCREEN TEST: exception report content and scrolling.')
                    $stack = (1..45 | ForEach-Object { "   at SyntheticCrashPreview.OwnedFrame$_() in C:\fixture\synthetic\NestedSyntheticDirectoryForHorizontalScrollbarCoverage\LongPathForHorizontalScrolling.cs:line 123" }) -join [Environment]::NewLine
                    [Runtime.ExceptionServices.ExceptionDispatchInfo]::SetRemoteStackTrace($ex, $stack) | Out-Null
                    $script:result = $null
                    $complete = [Action[bool]]{ param($value) $script:result = $value }
                    [string]$logPath = Join-Path $EvidenceDirectory 'synthetic-crash.log'
                    $dialog = $create.Invoke($null, @($ex, $logPath, $fatal, $complete))
                    try {
                        $dialog.Width = $width
                        $dialog.Content.Measure([Windows.Size]::new($width, 540))
                        $dialog.Content.Arrange([Windows.Rect]::new(0, 0, $width, 540))
                        $dialog.Content.UpdateLayout()
                        $children = @(Descendants $dialog.Content)
                        $trace = @($children | Where-Object { $_ -is [Windows.Controls.TextBox] })[0]
                        $trace.ApplyTemplate() | Out-Null
                        $scroll = $trace.Template.FindName('PART_ContentHost', $trace)
                        $scroll.ApplyTemplate() | Out-Null
                        $dialog.Content.Measure([Windows.Size]::new($width, 540))
                        $dialog.Content.Arrange([Windows.Rect]::new(0, 0, $width, 540))
                        $dialog.Content.UpdateLayout()
                        if ($scroll.Style -ne $dialog.Resources[[Windows.Controls.ScrollViewer]]) { throw 'Wrong scroll host.' }
                        if ($scroll.ScrollableHeight -le 0 -or $scroll.ScrollableWidth -le 0) {
                            Render-Preview $dialog 'diagnostic'
                            throw "Both scrollbars must be exercised: $themeName/$width trace=$($trace.ActualWidth)x$($trace.ActualHeight) extent=$($scroll.ExtentWidth)x$($scroll.ExtentHeight) viewport=$($scroll.ViewportWidth)x$($scroll.ViewportHeight)."
                        }
                        $scroll.ScrollToBottom()
                        $scroll.ScrollToRightEnd()
                        $dialog.Content.UpdateLayout()
                        if ($scroll.VerticalOffset -le 0 -or $scroll.HorizontalOffset -le 0) { throw 'Scrolling did not move the report.' }
                        $cards = @($children | Where-Object { $_.GetType().FullName -eq 'KillerPDF.Controls.PickerBorder' })
                        if ($cards.Count -lt 3 -or @($cards | Where-Object { $null -eq $_.Child.Clip }).Count -ne 0) { throw 'Card content is not clipped.' }
                        $continue = @($children | Where-Object { $_ -is [Windows.Controls.Button] -and $_.Content -eq $app.Resources['Str_Crash_Continue'] })[0]
                        if ($continue.IsEnabled -eq $fatal) { throw 'Fatal report Continue state changed.' }
                        if (!$fatal) {
                            $continue.RaiseEvent([Windows.RoutedEventArgs]::new([Windows.Controls.Button]::ClickEvent))
                            if ($script:result -ne $true) { throw 'Continue did not report recovery.' }
                        }
                        $case = "$themeName/grain=$enabled/width=$width/fatal=$fatal"
                        $rows.Add([pscustomobject]@{ Case = $case; HorizontalExtent = $scroll.ScrollableWidth; VerticalExtent = $scroll.ScrollableHeight; Cards = $cards.Count })
                        if ($width -eq 760 -and !$fatal -and $themeName -in @('Dark','Light','SE98','Malaise')) {
                            Render-Preview $dialog "$themeName-grain-$enabled"
                        }
                    } finally { $dialog.Close() }
                }
            }
        }
    }
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'results.json'), ($rows | ConvertTo-Json))
    Write-Output "PASS $($rows.Count) offscreen report cases, clipped cards, both scroll offsets and fatal/recoverable state; assembly=$($assembly.FullName)"
} finally { $app.Shutdown() }

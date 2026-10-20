# Run after the chosen build with PowerShell 7 on the matching .NET runtime:
# pwsh -NoProfile -Sta -File build/Test-ColorPickerDialog.ps1 -Configuration Release
# Loads the compiled dialog without starting the app or saving preferences.
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Run this test with pwsh -Sta.'
}
$project = Join-Path $RepositoryRoot 'KillerPDF.csproj'
$properties = & dotnet msbuild $project -nologo "-p:Configuration=$Configuration" -getProperty:TargetPath,TargetFramework | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate the app project.' }
$assemblyPath = $properties.Properties.TargetPath
$framework = $properties.Properties.TargetFramework
if ($framework -notmatch '^net(?<major>\d+)\.(?<minor>\d+)-windows') {
    throw "Unsupported framework: $framework"
}
$major = [int]$Matches.major
$minor = [int]$Matches.minor
if ([Environment]::Version.Major -ne $major) { throw "Use a PowerShell host running .NET $major." }
if (!(Test-Path -LiteralPath $assemblyPath)) { throw "Build the $Configuration app before running this test." }
$runtimes = @(& dotnet --list-runtimes | ForEach-Object {
    if ($_ -match '^Microsoft\.WindowsDesktop\.App (?<version>\d+\.\d+\.\d+) \[(?<root>.+)\]$') {
        $version = [version]$Matches.version
        if ($version.Major -eq $major -and $version.Minor -eq $minor) {
            [pscustomobject]@{ Version = $version; Path = Join-Path $Matches.root $Matches.version }
        }
    }
} | Sort-Object Version -Descending)
if ($runtimes.Count -eq 0) { throw "No desktop runtime found for $framework." }
$desktopPath = $runtimes[0].Path
$resolver = {
    param($context, $name)
    $candidate = Join-Path $desktopPath ($name.Name + '.dll')
    if (Test-Path -LiteralPath $candidate) { return $context.LoadFromAssemblyPath($candidate) }
    return $null
}.GetNewClosure()
[Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolver)
foreach ($library in @('WindowsBase', 'PresentationCore', 'PresentationFramework')) {
    Add-Type -Path (Join-Path $desktopPath ($library + '.dll'))
}
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$resourceField = [Windows.Application].GetField('_resourceAssembly', [Reflection.BindingFlags]'Static,NonPublic')
if ($null -ne $resourceField) { $resourceField.SetValue($null, $assembly) }
else { [Windows.Application]::ResourceAssembly = $assembly }
$application = [Windows.Application]::new()
$application.ShutdownMode = [Windows.ShutdownMode]::OnExplicitShutdown
$manager = $assembly.GetType('KillerPDF.Services.ThemeManager', $true)
$themeType = $assembly.GetType('KillerPDF.Services.Theme', $true)
$accentType = $assembly.GetType('KillerPDF.Services.DarkAccent', $true)
$dialogType = $assembly.GetType('KillerPDF.ColorPickerDialog', $true)
$load = $manager.GetMethod('LoadDict', [Reflection.BindingFlags]'Static,NonPublic')
$current = $manager.GetField('_current', [Reflection.BindingFlags]'Static,NonPublic')
$makeButton = $dialogType.GetMethod('MakeButton', [Reflection.BindingFlags]'Static,NonPublic')
$commitHex = $dialogType.GetMethod('CommitHex', [Reflection.BindingFlags]'Instance,NonPublic')
$accept = $dialogType.GetMethod('Accept', [Reflection.BindingFlags]'Instance,NonPublic')
$hexBox = $dialogType.GetField('_hexBox', [Reflection.BindingFlags]'Instance,NonPublic')
$states = 0

function Test-Picker([string]$Theme, [object]$Accent) {
    $themeValue = [Enum]::Parse($themeType, $Theme)
    $application.Resources.Clear()
    $application.Resources.MergedDictionaries.Clear()
    $current.SetValue($null, $themeValue)
    $null = $load.Invoke($null, @($themeValue, $Accent))
    $initial = [Windows.Media.Color]::FromRgb(10, 20, 30)
    $dialog = [Activator]::CreateInstance($dialogType, [object[]]@($null, $initial))
    try {
        if ($dialog.SelectedColor -ne $initial -or $dialog.Accepted) { throw 'Initial picker state changed.' }
        if ($Theme -ne 'SE98') {
            $button = $makeButton.Invoke($null, @('OK', $true))
            if (![object]::ReferenceEquals($button.Background, $application.Resources['SelectionBg'])) {
                throw 'The primary button did not preserve the theme brush.'
            }
        }
        $hexBox.GetValue($dialog).Text = '#123456'
        $null = $commitHex.Invoke($dialog, $null)
        $null = $accept.Invoke($dialog, $null)
        if (!$dialog.Accepted -or $dialog.SelectedColor.ToString() -ne '#FF123456') {
            throw 'The picker did not commit the selected color.'
        }
    }
    finally { $dialog.Close() }
    Write-Output "PASS picker $Theme/$Accent"
}

try {
    foreach ($theme in [Enum]::GetNames($themeType)) {
        Test-Picker $theme $null
        $states++
    }
    foreach ($theme in @('Dark', 'Light', 'Black', 'SE98')) {
        foreach ($accent in [Enum]::GetValues($accentType)) {
            Test-Picker $theme $accent
            $states++
        }
    }
    # Exercise each supported brush shape independently of today's theme palette.
    foreach ($brush in @(
        [Windows.Media.SolidColorBrush]::new([Windows.Media.Colors]::Blue),
        [Windows.Media.LinearGradientBrush]::new([Windows.Media.Colors]::Blue, [Windows.Media.Colors]::White, 90),
        [Windows.Media.RadialGradientBrush]::new([Windows.Media.Colors]::Blue, [Windows.Media.Colors]::White)
    )) {
        $current.SetValue($null, [Enum]::Parse($themeType, 'Dark'))
        $application.Resources['SelectionBg'] = $brush
        $dialog = [Activator]::CreateInstance($dialogType, [object[]]@($null, [Windows.Media.Colors]::Red))
        try {
            $button = $makeButton.Invoke($null, @('OK', $true))
            if (![object]::ReferenceEquals($brush, $button.Background)) { throw 'The theme brush was changed.' }
        }
        finally { $dialog.Close() }
        Write-Output "PASS picker $($brush.GetType().Name)"
    }
    Write-Output "PASS $states theme/accent states and 3 brush shapes; assembly=$assemblyPath; sha256=$((Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash)"
}
finally {
    $application.Shutdown()
    [Runtime.Loader.AssemblyLoadContext]::Default.remove_Resolving($resolver)
}

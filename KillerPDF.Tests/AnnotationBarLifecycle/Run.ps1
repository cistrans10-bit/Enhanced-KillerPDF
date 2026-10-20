param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\..\Shell\WindowChrome.cs'),
    [string]$OutputDirectory = (Join-Path $env:TEMP ('KillerPDF-annotation-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$SourcePath = [IO.Path]::GetFullPath($SourcePath)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$source = [IO.File]::ReadAllText($SourcePath)
$start = $source.IndexOf('        internal void RepositionAnnotationBars()')
$end = $source.IndexOf('        // Snapping changes', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Cannot locate annotation lifecycle methods.' }
$fixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Program.cs.txt'))
$fixture = $fixture.Replace('    // LIVE_ANNOTATION_METHODS', $source.Substring($start, $end - $start))
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'Program.cs'), $fixture)
$dotnetRoot = Join-Path $env:ProgramFiles 'dotnet'
$dotnet = Join-Path $dotnetRoot 'dotnet.exe'
$sdk = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot 'sdk') -Directory | Where-Object Name -Match '^10\.' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
$refs = @{}
foreach ($pack in @('Microsoft.NETCore.App.Ref','Microsoft.WindowsDesktop.App.Ref')) {
    $version = Get-ChildItem -LiteralPath (Join-Path $dotnetRoot ('packs\' + $pack)) -Directory | Where-Object Name -Match '^10\.' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $version.FullName 'ref\net10.0') -Filter *.dll) { $refs[$file.Name] = $file.FullName }
}
$arguments = @('-nologo','-target:exe','-nullable:enable',('-out:"' + (Join-Path $OutputDirectory 'Regression.dll') + '"'),('"' + (Join-Path $OutputDirectory 'Program.cs') + '"'))
foreach ($reference in $refs.Values) { $arguments += '-r:"' + $reference + '"' }
$response = Join-Path $OutputDirectory 'compiler.rsp'
[IO.File]::WriteAllLines($response, $arguments)
& $dotnet (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll') '-noconfig' ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw 'Regression fixture compilation failed.' }
$runtime = '{"runtimeOptions":{"tfm":"net10.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.WindowsDesktop.App","version":"10.0.0"}]}}'
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'Regression.runtimeconfig.json'), $runtime)
$process = $null
try {
    $process = Start-Process -FilePath $dotnet -ArgumentList @(('"' + (Join-Path $OutputDirectory 'Regression.dll') + '"'), ('"' + (Join-Path $OutputDirectory 'result.json') + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $OutputDirectory 'stdout.txt') -RedirectStandardError (Join-Path $OutputDirectory 'stderr.txt')
    if (!$process.WaitForExit(10000)) { throw 'Owned regression helper timed out.' }
    $process.Refresh()
    Get-Content -LiteralPath (Join-Path $OutputDirectory 'stdout.txt')
    if ($process.ExitCode -ne 0) { throw ([IO.File]::ReadAllText((Join-Path $OutputDirectory 'stderr.txt'))) }
} finally {
    if ($process) {
        $process.Refresh()
        if (!$process.HasExited) { $process.Kill(); $process.WaitForExit(2000) | Out-Null }
        [IO.File]::WriteAllText((Join-Path $OutputDirectory 'owned-process.txt'), ('pid=' + $process.Id + '; exited=' + $process.HasExited))
        $process.Dispose()
    }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'source-sha256.txt'), (Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash)
    Write-Output ('Evidence: ' + $OutputDirectory)
}

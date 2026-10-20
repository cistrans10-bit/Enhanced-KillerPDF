[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$TrxPath,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
[xml]$project = Get-Content -LiteralPath (Join-Path $repo 'KillerPDF.csproj') -Raw
$version = [string]$project.Project.PropertyGroup[0].Version
[xml]$trx = Get-Content -LiteralPath $TrxPath -Raw
$definitions = @($trx.SelectNodes("//*[local-name()='UnitTest']"))
if (!$definitions.Count -or @($definitions | Where-Object { $_.storage -notmatch '(?i)[\\/]KillerPdf\.Engine\.Tests\.dll$' }).Count) {
    throw 'Site facts require the engine suite, not another test project.'
}
$results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
$counters = $trx.SelectSingleNode("//*[local-name()='Counters']")
if (!$counters -or !$results.Count -or @($results | Where-Object { $_.outcome -ne 'Passed' }).Count -or
    [int]$counters.total -ne $results.Count -or [int]$counters.passed -ne $results.Count) {
    throw 'Engine site facts require complete passing engine test results.'
}
$sourceFiles = @(& git -C $repo ls-files -- 'engine/KillerPdf.Engine/*.cs')
if ($LASTEXITCODE -ne 0 -or !$sourceFiles.Count) { throw 'Could not inventory tracked engine sources.' }
$sourceLines = 0
foreach ($file in $sourceFiles) {
    $sourceLines += [IO.File]::ReadAllLines((Join-Path $repo $file)).Count
}
$reports = @(Get-ChildItem (Join-Path $repo 'validation/benchmarks') -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' -and [version]$_.Name -le [version]$version } |
    Sort-Object { [version]$_.Name } -Descending)
$report = $reports | Where-Object { Test-Path (Join-Path $_.FullName 'benchmark-summary.csv') } | Select-Object -First 1
if (!$report) { throw 'No retained corpus report is available for this version.' }
$rows = @(Import-Csv (Join-Path $report.FullName 'benchmark-summary.csv') |
    Where-Object Version -eq $report.Name)
if (!$rows.Count) { throw 'The corpus summary has no rows for its reported version.' }
$corpusPdfs = [int](($rows | Measure-Object Inputs -Sum).Sum)
$corpusText = Get-Content -LiteralPath (Join-Path $report.FullName 'CORPUS.md') -Raw
if ($corpusText -notmatch '(?m)^\| Damaged-file safety \| ([\d,]+) \|') {
    throw 'The corpus report has no separate damaged-file safety count.'
}
$corpusPdfs += [int]$Matches[1].Replace(',', '')
$facts = [ordered]@{
    version = $version
    sourceFiles = $sourceFiles.Count
    sourceLines = $sourceLines
    engineTests = $results.Count
    corpusPdfs = $corpusPdfs
    corpusVersion = $report.Name
}
$site = Join-Path $repo 'pdf-landing/engine'
$content = 'window.KPDF_ENGINE_FACTS = ' + ($facts | ConvertTo-Json -Compress) + ';' + [Environment]::NewLine
if ($DryRun) { Write-Host $content; return }
$destination = Join-Path $site 'engine-site-facts.js'
$previous = if (Test-Path $destination) { [IO.File]::ReadAllText($destination) } else { '' }
if ($content -ne $previous) { [IO.File]::WriteAllText($destination, $content) }
foreach ($html in Get-ChildItem (Split-Path $site -Parent) -Filter '*.html' -Recurse) {
    $previous = [IO.File]::ReadAllText($html.FullName)
    $updated = $previous -replace '(engine-site-facts\.js\?v=)[^"]+', ('${1}' + $version)
    if ($updated -ne $previous) { [IO.File]::WriteAllText($html.FullName, $updated) }
}
Write-Host "Engine site facts updated for $version."

<#
.SYNOPSIS
    Compares two veraPDF JSON batch reports and flags any file whose
    validation outcome changed between the two runs.

.DESCRIPTION
    Used to verify that resaving PDFs through KillerPDF does not degrade
    standards conformance. Workflow:

      1. Baseline the corpus:
         verapdf --recurse --format json C:\pdf-corpus > baseline.json
      2. Resave every corpus file through KillerPDF into a mirror folder,
         preserving relative paths (see --batch-resave).
      3. Validate the resaved tree:
         verapdf --recurse --format json C:\pdf-corpus-resaved > after.json
      4. Compare:
         .\Compare-VeraPDF.ps1 -Baseline baseline.json -After after.json `
             -BaselineRoot C:\pdf-corpus -AfterRoot C:\pdf-corpus-resaved `
             -ResaveLog resave.csv

    Files are matched by path relative to the given roots. The check that
    matters for release: zero regressions. A regression is any of:
      - NEW_FAIL:          compliant in baseline, non-compliant after
      - rules added:       a file fails rules after that it did not fail before
                           (even if it already failed others)
      - PARSE_ERROR_AFTER: veraPDF parsed the baseline file but not the resave
      - MISSING_AFTER:     file present in baseline report but absent in after and not
                           recorded as an intentional batch skip

    Exit code 0 = no regressions, 1 = regressions found, 2 = usage/input error.

.NOTES
    Compatible with Windows PowerShell 5.1 and PowerShell 7.
    Part of the KillerPDF validation harness (validation/).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$Baseline,
    [Parameter(Mandatory = $true)] [string]$After,
    [Parameter(Mandatory = $true)] [string]$BaselineRoot,
    [Parameter(Mandatory = $true)] [string]$AfterRoot,
    [string]$ResaveLog,
    [string]$CsvOut,
    [switch]$ShowUnchanged
)

$ErrorActionPreference = 'Stop'

function Get-VeraJobs {
    param([string]$Path, [string]$Root)

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Error "Report not found: $Path"
        exit 2
    }

    $raw = Get-Content -LiteralPath $Path -Raw
    try {
        $json = $raw | ConvertFrom-Json
    } catch {
        Write-Error "Could not parse JSON in ${Path}: $($_.Exception.Message)"
        exit 2
    }

    if (-not $json.report -or -not $json.report.jobs) {
        Write-Error "No report.jobs found in $Path - is this a veraPDF --format json report?"
        exit 2
    }

    $rootNorm = $Root.TrimEnd('\', '/').ToLowerInvariant()
    $jobs = @{}

    foreach ($job in $json.report.jobs) {
        $name = [string]$job.itemDetails.name
        $key = $name.ToLowerInvariant()
        if ($rootNorm.Length -gt 0 -and $key.StartsWith($rootNorm)) {
            $key = $key.Substring($rootNorm.Length).TrimStart('\', '/')
        }

        # Parse failures produce a job with no validationResult entry.
        $vr = $null
        $vrProp = $job.PSObject.Properties['validationResult']
        if ($null -ne $vrProp -and $null -ne $job.validationResult) {
            $vrArr = @($job.validationResult)
            if ($vrArr.Count -gt 0) { $vr = $vrArr[0] }
        }

        $compliant = $null
        $failedRules = @()
        if ($null -ne $vr) {
            $compliant = [bool]$vr.compliant
            if ($vr.details -and $vr.details.ruleSummaries) {
                foreach ($rs in @($vr.details.ruleSummaries)) {
                    if ([string]$rs.ruleStatus -eq 'FAILED') {
                        $failedRules += ('{0} clause {1} test {2}' -f $rs.specification, $rs.clause, $rs.testNumber)
                    }
                }
            }
        }

        $jobs[$key] = [pscustomobject]@{
            Name        = $name
            ParseError  = ($null -eq $vr)
            Compliant   = $compliant
            FailedRules = @($failedRules | Sort-Object -Unique)
        }
    }

    return $jobs
}

$baseJobs  = Get-VeraJobs -Path $Baseline -Root $BaselineRoot
$afterJobs = Get-VeraJobs -Path $After -Root $AfterRoot
$skipped = @{}
if ($ResaveLog) {
    if (-not (Test-Path -LiteralPath $ResaveLog)) {
        Write-Error "Resave log not found: $ResaveLog"
        exit 2
    }
    foreach ($row in @(Import-Csv -LiteralPath $ResaveLog)) {
        if ([string]$row.Status -eq 'SKIP') {
            $skipKey = ([string]$row.File).Replace('/', '\').TrimStart('\').ToLowerInvariant()
            $skipped[$skipKey] = [string]$row.Detail
        }
    }
}

$results = New-Object System.Collections.Generic.List[object]
$regressionCount = 0
$unchangedCount = 0

foreach ($key in @($baseJobs.Keys | Sort-Object)) {
    $b = $baseJobs[$key]

    if (-not $afterJobs.ContainsKey($key)) {
        if ($skipped.ContainsKey($key)) {
            $results.Add([pscustomobject]@{
                File = $key; Change = 'SKIPPED'; Regression = $false
                Detail = $skipped[$key]
            })
            continue
        }
        $results.Add([pscustomobject]@{
            File = $key; Change = 'MISSING_AFTER'; Regression = $true
            Detail = 'In baseline report but absent from after report (resave failed or file skipped)'
        })
        $regressionCount++
        continue
    }

    $a = $afterJobs[$key]

    if ($b.ParseError -and $a.ParseError) {
        # Unparseable before and after: no change, nothing to compare.
        $unchangedCount++
        if ($ShowUnchanged) {
            $results.Add([pscustomobject]@{
                File = $key; Change = 'UNCHANGED_PARSE_ERROR'; Regression = $false
                Detail = 'veraPDF could not parse this file in either run'
            })
        }
        continue
    }

    if (-not $b.ParseError -and $a.ParseError) {
        $results.Add([pscustomobject]@{
            File = $key; Change = 'PARSE_ERROR_AFTER'; Regression = $true
            Detail = 'Baseline validated, but veraPDF could not parse the resaved file'
        })
        $regressionCount++
        continue
    }

    if ($b.ParseError -and -not $a.ParseError) {
        $results.Add([pscustomobject]@{
            File = $key; Change = 'PARSE_ERROR_RESOLVED'; Regression = $false
            Detail = 'Baseline was unparseable, resave validates. Not a regression, but verify the resave kept the file intact'
        })
        continue
    }

    $added   = @($a.FailedRules | Where-Object { $b.FailedRules -notcontains $_ })
    $removed = @($b.FailedRules | Where-Object { $a.FailedRules -notcontains $_ })

    if ($added.Count -eq 0 -and $removed.Count -eq 0) {
        $unchangedCount++
        if ($ShowUnchanged) {
            $results.Add([pscustomobject]@{
                File = $key; Change = 'UNCHANGED'; Regression = $false
                Detail = ('compliant={0}, failedRules={1}' -f $b.Compliant, $b.FailedRules.Count)
            })
        }
        continue
    }

    $change = 'RULES_CHANGED'
    if ($b.Compliant -and -not $a.Compliant) { $change = 'NEW_FAIL' }
    elseif (-not $b.Compliant -and $a.Compliant) { $change = 'NOW_COMPLIANT' }

    $isRegression = ($added.Count -gt 0)
    if ($isRegression) { $regressionCount++ }

    $detailParts = @()
    if ($added.Count -gt 0)   { $detailParts += ('ADDED: ' + ($added -join ' | ')) }
    if ($removed.Count -gt 0) { $detailParts += ('removed: ' + ($removed -join ' | ')) }

    $results.Add([pscustomobject]@{
        File = $key; Change = $change; Regression = $isRegression
        Detail = ($detailParts -join '  ;  ')
    })
}

foreach ($key in @($afterJobs.Keys | Sort-Object)) {
    if (-not $baseJobs.ContainsKey($key)) {
        $results.Add([pscustomobject]@{
            File = $key; Change = 'MISSING_BASELINE'; Regression = $false
            Detail = 'In after report but not in baseline (extra output file?)'
        })
    }
}

# ---- Output ----------------------------------------------------------------

$changed = @($results | Where-Object { $_.Change -notlike 'UNCHANGED*' })

Write-Host ''
Write-Host ('Baseline jobs : {0}' -f $baseJobs.Count)
Write-Host ('After jobs    : {0}' -f $afterJobs.Count)
Write-Host ('Unchanged     : {0}' -f $unchangedCount)
Write-Host ('Changed       : {0}' -f $changed.Count)
Write-Host ('Regressions   : {0}' -f $regressionCount)
Write-Host ''

if ($results.Count -gt 0) {
    # NOTE: keep $toShow a plain array. Wrapping the generic List in @(...) and reading .Count
    # makes the Windows PowerShell 5.1 binder throw "Argument types do not match".
    $toShow = $changed
    if ($ShowUnchanged) { $toShow = $results.ToArray() }
    if ($toShow.Count -gt 0) {
        # Plain padded strings instead of Format-Table (its 5.1 formatter has its own quirks);
        # the CSV is the real record anyway.
        $fileW = 60
        foreach ($r in $toShow) { if ($r.File.Length -gt $fileW) { $fileW = $r.File.Length } }
        Write-Host (('{0,-' + $fileW + '} {1,-20} {2,-5} {3}') -f 'File', 'Change', 'Reg', 'Detail')
        Write-Host (('{0,-' + $fileW + '} {1,-20} {2,-5} {3}') -f '----', '------', '---', '------')
        foreach ($r in $toShow) {
            Write-Host (('{0,-' + $fileW + '} {1,-20} {2,-5} {3}') -f $r.File, $r.Change, $r.Regression, $r.Detail)
        }
        Write-Host ''
    }
}

if ($CsvOut) {
    $results | Export-Csv -LiteralPath $CsvOut -NoTypeInformation -Encoding UTF8
    Write-Host ('Full results written to {0}' -f $CsvOut)
}

if ($regressionCount -gt 0) {
    Write-Host 'RESULT: FAIL - KillerPDF resave introduced regressions.' -ForegroundColor Red
    exit 1
} else {
    Write-Host 'RESULT: PASS - no conformance regressions introduced.' -ForegroundColor Green
    exit 0
}

#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [ValidateRange(1, 120)][int]$TimeoutSeconds = 45,
    [string]$EvidenceDirectory = ""
)
# Starts only a test-owned, transparent WPF process with isolated in-memory settings.
# Retains reports/stdout/stderr outside the payload; fails on timeout, failure, or missing proof.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ExePath = (Resolve-Path -LiteralPath $ExePath).Path
if (-not $EvidenceDirectory) {
    $EvidenceDirectory = Join-Path ([IO.Path]::GetTempPath()) ('KillerPDF-gui-check-' + [guid]::NewGuid().ToString('N'))
}
[IO.Directory]::CreateDirectory($EvidenceDirectory) | Out-Null
$report = Join-Path $EvidenceDirectory 'startup.log'
if ([IO.File]::Exists($report)) { throw "Refusing stale startup evidence: $report" }
$start = New-Object System.Diagnostics.ProcessStartInfo
$start.FileName = $ExePath
$start.Arguments = '--startup-smoke'
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WorkingDirectory = [IO.Path]::GetTempPath()
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['KILLERPDF_SMOKE_REPORT'] = $report
$start.EnvironmentVariables.Remove('KILLERPDF_STARTUP_TRACE')
$process = New-Object System.Diagnostics.Process
$process.StartInfo = $start
try {
    if (-not $process.Start()) { throw 'Could not start the GUI check.' }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        # Kill only the created process tree (portable launcher plus its own child).
        & taskkill.exe /PID $process.Id /T /F | Out-Null
        throw "GUI startup timed out after $TimeoutSeconds seconds. Evidence: $EvidenceDirectory"
    }
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'stdout.log'), $stdout.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'stderr.log'), $stderr.GetAwaiter().GetResult())
    if ($process.ExitCode -ne 0) {
        throw "GUI startup failed with exit code $($process.ExitCode). Evidence: $EvidenceDirectory"
    }
    $proof = if ([IO.File]::Exists($report)) { [IO.File]::ReadAllText($report) } else { '' }
    foreach ($marker in @('MainWindow constructed', 'MainWindow Loaded', 'MainWindow ContentRendered', 'Theme picker opened', 'PASS MainWindow startup')) {
        if (-not ($proof -split '\r?\n' -contains $marker)) {
            throw "GUI startup evidence lacks '$marker'. Evidence: $EvidenceDirectory"
        }
    }
    if ($proof -match '(?m)^FAIL ') { throw "GUI startup reported a failure. Evidence: $EvidenceDirectory" }
    Write-Host "GUI startup passed. Evidence: $EvidenceDirectory"
}
finally { $process.Dispose() }

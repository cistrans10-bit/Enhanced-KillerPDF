#Requires -Version 5.1
# Run in Windows PowerShell 5.1 to compile a disposable .NET Framework fixture executable.
# No app launch, signing, registry edits, user documents, or release execution.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Use Windows PowerShell 5.1 for this fixture.' }
$root = Join-Path ([IO.Path]::GetTempPath()) ('KillerPDF-gui-runner-fixture-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
$exe = Join-Path $root 'Fixture.exe'
Add-Type -OutputAssembly $exe -OutputType ConsoleApplication -TypeDefinition @'
using System;
using System.IO;
using System.Threading;
public static class Fixture {
    public static int Main(string[] args) {
        if (args.Length != 1 || args[0] != "--startup-smoke") return 2;
        string mode = Environment.GetEnvironmentVariable("KILLERPDF_RUNNER_FIXTURE");
        if (mode == "hang") { Thread.Sleep(10000); return 0; }
        if (mode == "fail") { Console.Error.WriteLine("fixture failure"); return 1; }
        if (mode == "empty") return 0;
        string report = Environment.GetEnvironmentVariable("KILLERPDF_SMOKE_REPORT");
        string proof = "MainWindow constructed\nMainWindow Loaded\nMainWindow ContentRendered\nTheme picker opened\nPASS MainWindow startup\n";
        if (mode == "startup-only") proof = proof.Replace("Theme picker opened\n", "");
        File.WriteAllText(report, proof);
        if (mode == "contradiction") File.AppendAllText(report, "FAIL fixture\n");
        return 0;
    }
}
'@
$previousMode = [Environment]::GetEnvironmentVariable('KILLERPDF_RUNNER_FIXTURE')
try {
    foreach ($mode in @('pass', 'fail', 'empty', 'startup-only', 'contradiction', 'hang')) {
        [Environment]::SetEnvironmentVariable('KILLERPDF_RUNNER_FIXTURE', $mode)
        $rejected = $false
        try {
            & (Join-Path $PSScriptRoot 'Test-GuiStartup.ps1') -ExePath $exe -EvidenceDirectory (Join-Path $root $mode) -TimeoutSeconds 1
        }
        catch { $rejected = $true; Write-Host "$mode rejected: $_" }
        if (($mode -eq 'pass') -eq $rejected) { throw "Incorrect runner result for $mode" }
    }
    Write-Host 'PASS: runner accepts complete proof; rejects nonzero exit, missing proof, failure with success, and timeout.'
}
finally {
    [Environment]::SetEnvironmentVariable('KILLERPDF_RUNNER_FIXTURE', $previousMode)
    # Retain diagnostics so timeout and exit-code evidence remains reviewable.
    Write-Host "Fixture evidence: $root"
}

$ErrorActionPreference = 'Stop'

$installedExe = Join-Path $env:ProgramFiles 'KillerPDF\KillerPDF.App.exe'
if (Test-Path -LiteralPath $installedExe) {
    Start-ChocolateyProcessAsAdmin -exeToRun $installedExe -statements '/uninstall-silent'
}

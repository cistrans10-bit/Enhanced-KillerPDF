#Requires -Version 5.1
param([string]$UnsignedPackagePath = "")
# Safe fixtures: temporary files, no signing, credentials, publishing, or release execution.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'payload-security.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('KillerPDF-security-fixture-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock]$Action, [string]$Expected) {
    try { & $Action } catch {
        Assert ($_.Exception.Message -like "*$Expected*") "Unexpected rejection: $_"
        return
    }
    throw "Expected rejection: $Expected"
}
try {
    $names = @('KillerPDF.App.exe', 'KillerPDF.App.dll', 'KillerPdf.Engine.dll')
    foreach ($name in $names + @('ThirdParty.dll')) { [IO.File]::WriteAllText((Join-Path $root $name), $name) }
    $targets = @(Get-FirstPartyPayloadFiles $root)
    Assert ($targets.Count -eq 3) 'Must select exactly three files.'
    Assert ((@($targets | ForEach-Object { [IO.Path]::GetFileName($_) }) -join ',') -eq ($names -join ',')) 'Wrong signing targets.'
    Reject { Assert-FirstPartyPayloadSignatures $root } 'Authenticode validation failed'
    [IO.File]::Delete((Join-Path $root 'KillerPdf.Engine.dll'))
    Reject { Get-FirstPartyPayloadFiles $root } 'missing'
    [IO.File]::WriteAllText((Join-Path $root 'KillerPdf.Engine.dll'), 'engine-before-signing')

    # Exercise signature gate success and every failure position without a certificate.
    $script:invalidName = ''
    $script:signatureType = 'Authenticode'
    $script:checked = @()
    function Get-AuthenticodeSignature([string]$LiteralPath) {
        $name = [IO.Path]::GetFileName($LiteralPath)
        $script:checked += $name
        [pscustomobject]@{
            Status = $(if ($name -eq $script:invalidName) { 'HashMismatch' } else { 'Valid' })
            SignatureType = $script:signatureType
        }
    }
    Assert-FirstPartyPayloadSignatures $root
    Assert (($script:checked -join ',') -eq ($names -join ',')) 'Signature check touched third-party files or omitted owned bytes.'
    foreach ($name in $names) {
        $script:invalidName = $name
        Reject { Assert-FirstPartyPayloadSignatures $root } 'Authenticode validation failed'
    }
    $script:invalidName = ''
    $script:signatureType = 'Catalog'
    Reject { Assert-FirstPartyPayloadSignatures $root } 'Authenticode validation failed'
    Remove-Item Function:\Get-AuthenticodeSignature

    $manifest = Join-Path $root 'payload.manifest'
    $files = @($targets) + @((Join-Path $root 'ThirdParty.dll'))
    Write-PayloadManifest $manifest $root $files
    $before = [IO.File]::ReadAllText($manifest)
    # Stand in for the byte change produced by signing final published DLLs.
    [IO.File]::AppendAllText((Join-Path $root 'KillerPdf.Engine.dll'), '-final-bytes')
    Write-PayloadManifest $manifest $root $files
    Assert ($before -ne [IO.File]::ReadAllText($manifest)) 'Manifest did not refresh changed DLL bytes.'
    foreach ($line in [IO.File]::ReadAllLines($manifest)) {
        $parts = $line -split "`t"
        $file = Join-Path $root $parts[2]
        Assert ($parts[0] -eq (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash) 'Wrong final manifest hash.'
        Assert ([long]$parts[1] -eq ([IO.FileInfo]$file).Length) 'Wrong final manifest length.'
    }

    # Read/parse release source only. Never invoke release.ps1.
    $repo = Split-Path -Parent $PSScriptRoot
    $release = [IO.File]::ReadAllText((Join-Path $repo 'release.ps1'))
    $pack = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'build-portable.ps1'))
    foreach ($file in @('release.ps1', 'build/build-portable.ps1', 'build/payload-security.ps1', 'build/Test-GuiStartup.ps1')) {
        $tokens = $null; $errors = $null
        $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repo $file), [ref]$tokens, [ref]$errors)
        Assert ($errors.Count -eq 0) "PowerShell parse failed: $file"
    }
    Assert ($release.IndexOf('& powershell @packageBuildArguments') -lt $release.IndexOf('foreach ($innerExe in $innerFiles)')) 'Signing must follow completed publish.'
    Assert ($release.IndexOf('signtool verify /pa /v $innerExe') -lt $release.IndexOf('-RepackOnly -RequireSignature')) 'Signatures must verify before repack.'
    Assert ($pack.IndexOf('Assert-FirstPartyPayloadSignatures $payloadDir') -lt $pack.IndexOf('Write-PayloadManifest $manifestPath')) 'Signature gate must precede manifest.'
    Assert ($pack.IndexOf('Write-PayloadManifest $manifestPath') -lt $pack.IndexOf('$archive.CreateEntry')) 'Manifest must precede compression.'
    Assert ($release.Contains('Assert-EmbeddedFirstPartySignatures $artifact')) 'Final-package verification missing.'
    Assert (-not $release.Contains("ArgumentList '--version'")) 'Release startup still uses metadata-only check.'
    Assert (-not $pack.Contains("ArgumentList '--version'")) 'Payload startup still uses metadata-only check.'
    if ($UnsignedPackagePath) {
        Reject { Assert-EmbeddedFirstPartySignatures $UnsignedPackagePath } 'Authenticode validation failed'
        Write-Host 'PASS: actual embedded unsigned first-party bytes rejected without executing the package.'
    }
    Write-Host 'PASS: exact targets, unsigned/missing/mismatched/catalog failures, final manifest hashes, publish/sign/verify/repack order, final-package and GUI gates.'
}
finally {
    Remove-Item Function:\Get-AuthenticodeSignature -ErrorAction SilentlyContinue
    [IO.Directory]::Delete($root, $true)
}

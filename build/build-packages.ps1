#Requires -Version 5.1
param(
    [string]$Configuration = "Release",
    [switch]$RepackOnly,
    [switch]$RequireSignature
)

$ErrorActionPreference = 'Stop'
$build = Join-Path $PSScriptRoot 'build-portable.ps1'

& $build -Configuration $Configuration -PackageKind Portable -RepackOnly:$RepackOnly -RequireSignature:$RequireSignature
if ($LASTEXITCODE -ne 0) { throw 'Portable package build failed.' }

& $build -Configuration $Configuration -PackageKind Installer -RepackOnly:$RepackOnly -RequireSignature:$RequireSignature
if ($LASTEXITCODE -ne 0) { throw 'Installer package build failed.' }

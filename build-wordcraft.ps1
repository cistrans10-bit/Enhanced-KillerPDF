# Builds DOCX mode's editor: WordCraft (the Rust word processor in WordCraft\) for Windows, and
# puts wordcraft.exe in WordCraft\dist, where KillerPDF.csproj picks it up and copies it next to
# KillerPDF.exe (WordCraft\wordcraft.exe). Run once before building KillerPDF, and again after
# changing anything under WordCraft\.
#
# Needs Rust (https://rustup.rs) with the MSVC toolchain. The first build takes several minutes.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'WordCraft'
$target = Join-Path $src 'target'
cargo build --release -p wordcraft --manifest-path (Join-Path $src 'Cargo.toml') --target-dir $target
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$dist = Join-Path $src 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item (Join-Path $target 'release\wordcraft.exe') (Join-Path $dist 'wordcraft.exe') -Force
Write-Host "wordcraft.exe -> $dist"

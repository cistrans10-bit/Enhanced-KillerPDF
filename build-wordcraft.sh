#!/usr/bin/env bash
# Cross-builds DOCX mode's editor (WordCraft, in WordCraft/) for Windows from Linux or macOS and
# puts wordcraft.exe in WordCraft/dist, where KillerPDF.csproj picks it up. See build-wordcraft.ps1
# for building on Windows itself.
#
# Needs Rust with the x86_64-pc-windows-gnu target and the MinGW-w64 linker:
#   rustup target add x86_64-pc-windows-gnu
#   apt install gcc-mingw-w64-x86-64 g++-mingw-w64-x86-64   (or: brew install mingw-w64)
set -euo pipefail
root="$(cd "$(dirname "$0")" && pwd)"
src="$root/WordCraft"
target="${CARGO_TARGET_DIR:-$src/target}"
cargo build --release -p wordcraft --manifest-path "$src/Cargo.toml" \
    --target x86_64-pc-windows-gnu --target-dir "$target"
mkdir -p "$src/dist"
cp -f "$target/x86_64-pc-windows-gnu/release/wordcraft.exe" "$src/dist/wordcraft.exe"
echo "wordcraft.exe -> $src/dist"

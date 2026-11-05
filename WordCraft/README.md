# WordCraft (bundled with KillerPDF)

This folder is the word processor behind **KillerPDF's DOCX mode**: a modified copy of
[WordCraft](https://github.com/cistrans10-bit/wordcraft-pages), the open-source, Rust-native word
processor, which is based on WordCraft by the ArtCraft team ([storytold/wordcraft](https://github.com/storytold/wordcraft)).
It reads and writes .docx (and .doc, .rtf, .odt, Markdown), with the ribbon, styles, tables, track
changes, references and mail merge.

KillerPDF starts it as `wordcraft.exe --embedded --control <port>` and hosts its window inside its
own (see `Shell/WordMode.cs` and `Controls/WordHost.cs` in the repository root).

## Changes from upstream

- `--embedded` (`apps/wordcraft/src/main.rs`, `crates/ui-egui/src/lib.rs`): no frame, no taskbar
  entry and no title bar row, for running inside a host window; the saved window position is left alone.
- In embedded mode the About page says what it is based on, and the community links are left out
  (`crates/ui-egui/src/dialogs.rs`, `crates/ui-egui/src/backstage.rs`).
- The ArtCraft name, wordmark and logos (upstream's `docs/brand/`) are removed, as upstream's brand
  licence requires of modified versions. This README replaces upstream's.

Imported from wordcraft-pages `6bc643656168` (branch `main`), plus the `embedded-mode` branch.

## Building

From the repository root: `build-wordcraft.ps1` on Windows, or `build-wordcraft.sh` to cross-build
from Linux or macOS. Both put `wordcraft.exe` in `WordCraft/dist`, which KillerPDF's build copies
next to `KillerPDF.exe`.

For development of WordCraft itself (tests, the CLI, the control protocol), see `CLAUDE.md`,
`AGENTS.md` and `docs/`.

## Licence

MIT OR Apache-2.0 (`LICENSE-MIT`, `LICENSE-APACHE`, `NOTICE`). Copyright (c) 2026 ArtCraft Team and
the WordCraft contributors.

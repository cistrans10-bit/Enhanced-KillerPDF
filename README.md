# KillerPDF

Free and open-source PDF editor for Windows. View, annotate, OCR, merge, split, edit text, draw, sign, fill forms, print, flatten, and open password-protected PDFs without an Adobe subscription or a phone-home. Install or run portable. Single Windows EXE, ~14.6 MB (ZIPs to 10.2MB), no runtime install required.

Landing page is hosted at [KillerPDF.net](https://killerpdf.net)

## Why this exists

I hate Adobe. Acrobat is bloated, wants a subscription to do basic things, and phones home constantly. Most of the "free" alternatives are either ad-riddled, cloud-based, or rebrands of the same PDF engine sold under three different names.

KillerPDF is what I wanted: local-only, portable, no account, no telemetry. The PDF equivalent of Notepad.

## Features

### Viewing & navigation

- High-quality rendering via PDFium
- Four view modes - Single Page, Continuous scroll, Two-Page, and Grid - that persist across sessions
- Tabbed documents: open several PDFs at once, each restoring its page, zoom, and view mode
- Full-text search across the whole document with highlighting; drag-select to copy text
- Outline/bookmark navigation and clickable links, including internal cross-references and TOC back-links
- Zoom presets with scroll-wheel sync; Fit to Width and Fit Page re-apply on resize
- Full-screen mode (F11) hides all chrome so only the document fills the screen
- Recent files on the start screen and Open menu, each with its real Windows file-type icon

### Annotate & edit

- Inline text editing with font matching against the original document
- Resizable, word-wrapping text boxes with an optional whiteout background fill
- Freehand draw, a straight-line tool, and highlight - each with its own color, opacity, and width
- Full RGB color picker: saturation/value square, hue strip, hex input, screen eyedropper, and editable palette
- Select tool to move, resize, multi-select, and restyle any annotation in place
- Insert images as resizable annotations, burned into the PDF on save
- Page-number and watermark stamping across a page range, applied as one undo

### OCR (built in, no cloud)

- OCR a whole page or a dragged region straight to the clipboard
- Make Searchable PDF: lay an invisible text layer over a scan
- Extract All Text to a `.txt` or `.md` file
- Tesseract bundled in the single EXE; extra languages download on demand

### Organize pages

- Merge multiple PDFs and split out selected pages, with drag-and-drop reordering
- Right-click sidebar: insert blank page, rotate, move, extract, or delete - on multi-page selections
- Crop with corner handles; remove crop from one page or all
- Transform: rotate by 90 degrees or a fine angle, scale, flip, and straighten a crooked scan by drawing a level line - live preview, with annotations following the transform
- Drop a folder or `.zip` onto the window to merge the PDFs and images inside into one, or open each separately

### Forms & signing

- Fill PDF forms (text, checkbox, radio) as live controls and save back to the PDF
- Digital signatures with a cloud certificate (Certum SimplySign), including click-to-sign form fields
- Draw and reuse signatures and initials, or import a PNG/JPG/BMP to place anywhere

### Output

- Print with annotations flattened, a real in-app preview, and scale / position / margins / pages-per-sheet / color / two-sided options, rendered at 300 DPI
- Save Flattened PDF: rasterize every page into a fully uneditable document
- Document Info: view and edit title, author, subject, keywords, and creator metadata

### Customize

- Six themes - Dark, Light, Black, Blood, Greed, Cyanotic - with per-theme accent colors, switchable live
- Toolbar style (icon size, text placement) and a resizable sidebar that docks left or right
- Localized UI in 8 languages (English, Spanish, Traditional and Simplified Chinese, German, French, Turkish, Bengali); contribute via `Strings/TRANSLATING.md`
- Full keyboard shortcut overlay (Ctrl+?) with a link to the online guide

### App & files

- Single portable Windows EXE, ~14.62 MB, no runtime install
- Self-installs per-user to %LOCALAPPDATA% (no UAC), registers as a PDF handler with a branded file icon, and uninstalls cleanly via Add/Remove Programs
- Opens password-protected PDFs (prompts instead of erroring) and repairs damaged ones
- Local-only: no account, no telemetry, no phone-home

## Screenshots

<p align="center">
  <img src="pdf-landing/screenshots/02.png" width="32%" alt="KillerPDF" />
  <img src="pdf-landing/screenshots/04.png" width="32%" alt="KillerPDF" />
  <img src="pdf-landing/screenshots/06.png" width="32%" alt="KillerPDF" />
  <img src="pdf-landing/screenshots/07.png" width="32%" alt="KillerPDF" />
  <img src="pdf-landing/screenshots/12.png" width="32%" alt="KillerPDF" />
  <img src="pdf-landing/screenshots/13.png" width="32%" alt="KillerPDF" />
</p>

## Requirements

- Windows 10 or 11 (x64)
- No runtime install. Everything needed is inside the EXE (targets .NET Framework 4.8, which ships with every supported Windows release).

## Download

WinGet:

```powershell
winget install killerpdf
```

Chocolately:

```powershell
choco install killerpdf
```

- Prebuilt binary: <https://github.com/SteveTheKiller/KillerPDF/releases/latest/download/KillerPDF.exe>
- Source (GPL3 corresponding source for this release): <https://github.com/SteveTheKiller/KillerPDF/releases/download/v1.6.1/KillerPDF-1.6.1-src.zip>

## Build from source

```powershell
git clone https://github.com/SteveTheKiller/KillerPDF.git
cd KillerPDF
dotnet publish -c Release
```

Output lands in `bin/Release/net48/publish/`. The publish step produces a single Costura-bundled `KillerPDF.exe` plus a versioned `KillerPDF-<version>-src.zip` for GPL3 source distribution.

Requires the .NET 8 SDK or later to build (even though the output targets .NET Framework 4.8).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

GPLv3. See [LICENSE](LICENSE). If you fork, modify, or redistribute KillerPDF, your version must also be released under GPLv3 with source available. No exceptions for commercial rebrands.

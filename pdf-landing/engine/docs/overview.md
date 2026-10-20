# The KillerPDF.Engine

The KillerPDF.Engine is an independent, UI-free .NET 10 library for reading, validating, authoring, structurally editing, signing, encrypting, and writing PDF files. It gives applications a modern PDF 2.0, PDF/A, and PDF/UA foundation without tying them to the KillerPDF desktop interface.

## Start building

Install the package from [NuGet.org](https://www.nuget.org/packages/KillerPdf.Engine):

```powershell
dotnet add package KillerPdf.Engine
```

[Getting started](getting-started.html)

Use the task guides in the sidebar when you already know what you need to do.

## What the engine covers

- PDF syntax, objects, streams, cross-reference tables, cross-reference streams, object streams, trailers, and incremental revisions
- Deterministic full rewrites and byte-preserving incremental updates
- PDF 2.0 authoring with pages, content streams, fonts, images, color spaces, patterns, transparency, and resources
- Bookmarks, named destinations, page labels, links, attachments, annotations, optional content, and AcroForm fields
- Tagged PDF, PDF/UA-2, PDF/A-4, PDF/A-4e, and PDF/A-4f authoring safeguards
- RC4, AES-128, and AES-256 password security
- Detached CMS signatures, certification permissions, field locks, verification, and signed revision analysis
- Structural diagnostics, bounded parsing, implementation limits, and round-trip validation

## Project scale

| Measure | Current 1.8.90 development tree |
| --- | --- |
| Engine source files | 153 |
| Physical C# lines | 48,388 |
| Engine tests | 1,492 |
| Corpus PDFs | 47,024 |

## A real library boundary

The engine has no dependency on WPF, KillerPDF application code, PDFium, PdfPig, PdfSharpCore, or PDFsharp. It operates on PDF bytes and typed document concepts. Rendering, OCR, text extraction, UI controls, and desktop workflows stay in the host application.

The monorepo keeps the engine, application integration, tests, and corpus gates in one reviewable change. The project boundary still enforces a reusable library that other .NET applications can reference through NuGet.

## Design principles

- Preserve existing bytes when an operation can be represented as an incremental revision.
- Fail closed when required structure cannot be interpreted or preserved safely.
- Emit deterministic output so regressions are reproducible.
- Enforce explicit implementation limits before allocating or serializing unbounded structures.
- Keep public APIs typed instead of exposing KillerPDF application state.
- Treat conformance as validator-backed behavior, not a label inferred from the PDF header.

## Source and license

The engine is developed in the [KillerPDF repository](https://github.com/SteveTheKiller/KillerPDF/tree/main/engine) and licensed under GPLv3. Detailed capability history is recorded in the [engine changelog](https://github.com/SteveTheKiller/KillerPDF/blob/main/engine/CHANGELOG.md).

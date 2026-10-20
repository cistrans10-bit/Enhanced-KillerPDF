# ADR-001: Build the PDF 2.0 document engine as an independent .NET 10 library

**Status:** Accepted
**Date:** 2026-08-22
**Decider:** Steve the Killer

**Implementation:** Completed and released on `main` in KillerPDF 1.8.0 on 2026-08-28. The branch policy below records the development process used before release.

## Context

KillerPDF 1.7.x uses PDFium for rendering, PdfPig for text extraction, and a vendored PdfSharpCore writer. That combination is proven for the current Windows application and its preservation-focused save pipeline, but it cannot provide complete PDF 2.0 authoring.

KillerPDF 1.8.0 needs an in-repository PDF document engine that can eventually own PDF syntax, object graphs, cross-reference data, page content, resources, fonts, images, annotations, forms, metadata, encryption, signatures, and the PDF 2.0 features required by ISO 32000-2. It does not render pages; PDFium retains that responsibility. The document engine must also be reusable by future non-Windows KillerPDF applications.

Changing the WPF application from .NET Framework 4.8 to modern .NET at the same time would combine an engine rewrite with an application-platform migration. Those risks should be separated.

## Decision

Create `KillerPdf.Engine` as a UI-free .NET 10 class library inside this repository, with its own .NET 10 test project. The engine may use only cross-platform runtime APIs and must not reference WPF, the KillerPDF shell, PDFium, PdfPig, PdfSharpCore, or PDFsharp.

The existing net48 application remains buildable and unchanged while the engine develops behind tests. Integration begins only after the engine can round-trip a defined preservation corpus without regression. Migration of the Windows shell to `net10.0-windows` will be decided and executed separately on the 1.8 branch.

The first vertical slice is PDF header/version handling, including `%PDF-2.0`. Later slices build downward from syntax and file structure before higher-level authoring APIs are exposed.

## Options considered

### Extend the vendored PdfSharpCore writer

Lower initial cost and immediate net48 integration, but its object model and serializer were designed around older PDF revisions. Retrofitting complete PDF 2.0 authoring would make compatibility constraints part of every new subsystem and would not establish a clean cross-platform engine boundary.

### Adopt another complete PDF library

Fastest route to broader format support, but licensing, native dependencies, API control, and long-term product identity remain outside the repository. This remains useful for differential testing, not as the 1.8 authoring core.

### Build an independent in-repository engine

Highest engineering cost, but it gives KillerPDF control over preservation behavior, authoring semantics, diagnostics, conformance fixes, licensing, and future platform support. This is the selected option.

## Consequences

- The 1.7.x application can continue receiving small fixes on `main` without absorbing unfinished engine work.
- Engine behavior is testable without starting WPF or loading native rendering libraries.
- The engine cannot be referenced by the net48 shell until the shell migrates or a deliberately limited compatibility boundary is introduced.
- PDFium remains the renderer initially; replacing rendering is not required to begin authoring.
- “Full PDF 2.0 support” will be tracked as explicit format capabilities and corpus tests, not inferred from accepting a `%PDF-2.0` header.

## Branch policy

- `main` is the released 1.7.x line.
- `develop/1.8.0` owns the engine and all 1.8-only work.
- A necessary 1.7.x fix is made on `main`, released there, then cherry-picked into `develop/1.8.0`.
- Do not merge `develop/1.8.0` back into `main` until 1.8 is release-ready.

## Initial milestones

1. Syntax primitives: header, tokens, names, strings, numbers, arrays, dictionaries, streams, indirect objects, and references.
2. File structure: classic xref tables, xref streams, trailers, object streams, incremental updates, and repair diagnostics.
3. Preservation writer: lossless object retention where possible, deterministic full rewrite, metadata/version policy, and corpus comparison.
4. Authoring model: catalog, page tree, resources, content streams, graphics state, fonts, images, annotations, forms, outlines, destinations, and attachments.
5. PDF 2.0 completion: ISO 32000-2 deltas, encryption/signatures, structure/accessibility, metadata, output intents, and feature-by-feature conformance fixtures.
6. KillerPDF integration and a separate decision on migrating the WPF shell to modern .NET.

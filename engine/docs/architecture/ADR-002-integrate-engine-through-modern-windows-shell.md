# ADR-002: Integrate the engine through a modern Windows shell

**Status:** Accepted
**Date:** 2026-08-24
**Decider:** Steve the Killer

## Context

KillerPdf.Engine targets .NET 10 so it can use current cross-platform runtime APIs and expose a reusable library without inheriting the desktop application's historical constraints. The KillerPDF Windows application still targets .NET Framework 4.8 and therefore cannot reference the engine directly.

The application currently relies on PdfSharpCore for document state, page manipulation, drawing, annotations, forms, OCR output, command-line workflows, and several preservation fallbacks. Removing that dependency in one change would combine a platform migration, a document-model replacement, and dozens of user-facing behavior changes.

The engine has reached the point where integration should begin, but preservation behavior and release packaging must remain testable after every step.

## Decision

Retarget the KillerPDF Windows application and its application test project to `net10.0-windows`. Add a direct project reference from the application to KillerPdf.Engine while retaining PdfSharpCore temporarily.

Replace PdfSharpCore through tested vertical slices. Each slice must move a coherent operation to the engine, preserve or improve its existing behavior, and leave the solution buildable. Remove the vendored PdfSharpCore project only after no production or application-test code references it.

PDFium remains the renderer and PdfPig remains the text-extraction library during this migration. The separate PDFsharp dependency remains limited to the existing signing implementation until signing is deliberately migrated.

## Options considered

### Keep the Windows shell on .NET Framework 4.8

This preserves the current deployment target but prevents a direct engine reference. An out-of-process bridge would add serialization, deployment, failure-recovery, and performance costs without improving the reusable engine API.

### Multi-target the engine for .NET Framework compatibility

This would force the new engine to carry compatibility shims and restrict its use of current runtime APIs. It would make a historical application constraint part of the public library contract and work against the goal of a modern industry-quality engine.

### Retarget the shell and replace PdfSharpCore in one change

This reaches the final dependency graph fastest in theory, but it removes the ability to isolate platform, packaging, and PDF-behavior regressions. The replacement surface is too broad for a single reviewable checkpoint.

### Retarget the shell and migrate in vertical slices

This creates the direct library boundary immediately while keeping behavior changes reviewable and measurable. It is the selected option.

## Consequences

- KillerPDF 1.8 requires the .NET 10 toolchain and a corresponding deployment strategy.
- The engine becomes a real application dependency before it becomes the sole document engine.
- PdfSharpCore remains temporarily, with its usage expected to decrease after every migration slice.
- Packaging scripts and documentation that assume `net48` must be updated for the modern shell.
- Each migrated operation can be compared against existing tests and preservation corpora.
- Rendering and text extraction are explicitly outside the PdfSharpCore removal scope.

## Migration order

1. Retarget the Windows application and application tests, then add the engine project reference.
2. Update build, packaging, and release paths for `net10.0-windows`.
3. Move read-only document inspection and metadata access to the engine.
4. Move structural page operations such as rotate, crop, reorder, delete, split, and merge.
5. Move annotations, forms, outlines, attachments, and incremental-save workflows.
6. Move drawing-heavy burn-in, OCR output, stamps, and text-editing workflows.
7. Remove remaining compatibility fallbacks, the application test reference, and the vendored PdfSharpCore project.
8. Run the full unit, corpus, qpdf, veraPDF, packaging, and launch gates before declaring the migration complete.

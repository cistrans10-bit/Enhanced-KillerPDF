# Validation

Validation is part of the engine design, not a label added after writing. Use structural inspection before editing, round-trip validation for deterministic rewrites, and independent tools for standards claims.

## Inspect unfamiliar input

```csharp
using KillerPdf.Engine.Diagnostics;

PdfInspectionReport report = PdfDocumentInspector.Inspect(source);

if (!report.IsStructurallyValid)
{
    foreach (PdfDiagnostic diagnostic in report.Diagnostics)
        Console.WriteLine($"{diagnostic.Severity}: {diagnostic.Message}");
}
```

Inspection is bounded and non-throwing for structural failures. It checks the header, revision chain, cross-reference data, indirect objects, catalog, object limits, and authentication state.

## Validate a deterministic rewrite

```csharp
using KillerPdf.Engine.Validation;

PdfRoundTripResult result = PdfRoundTripValidator.Validate(source);
if (!result.Succeeded)
    throw new InvalidOperationException(result.FailureMessage);

File.WriteAllBytes("validated.pdf", result.RewrittenBytes!);
```

The round-trip validator inspects the source, rewrites it, reopens the result, writes it a second time, and compares the two outputs. Use `ValidateAuthenticated` for password-protected input.

## Understand what this proves

| Check | What it establishes |
| --- | --- |
| Structural inspection | Required PDF structure can be resolved within explicit limits |
| Reopen after writing | The engine can parse the output it produced |
| Second deterministic write | Equivalent input produces identical normalized output |
| Independent validation | A separate implementation accepts the claimed structure or profile |

> A successful engine round trip does not replace veraPDF, qpdf, a signature verifier, or application-specific acceptance tests.

## Validation

For exact source and test counts, see the Overview. Corpus results identify the tested build and inputs. qpdf, veraPDF and OpenSSL provide separate structural, standards and signature checks.

[Overview](index.html) | [Corpus](../corpus.html)

## Validate the profile you claim

Use veraPDF for PDF/A and PDF/UA profiles, qpdf for general structural checks, and a cryptographic implementation independent of the engine for signature interoperability. Keep the exact validator version and profile in your build or release record.

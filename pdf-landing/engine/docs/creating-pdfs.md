# Creating PDFs

Use `PdfDocumentBuilder` and `PdfContentStreamBuilder` to create typed PDF 2.0 documents without assembling raw object dictionaries.

## Create pages and metadata

```csharp
using KillerPdf.Engine.Authoring;

var builder = new PdfDocumentBuilder()
    .SetMetadata(new PdfDocumentMetadata
    {
        Title = "Quarterly report",
        Author = "Example application",
        Language = "en-US"
    })
    .AddBlankPage(612, 792);

byte[] pdf = builder.Build();
```

`Build` validates coordinated document features before serialization. Invalid page geometry, incomplete standard structures, and inconsistent field definitions fail before a file is written.

## Write page content

```csharp
var content = new PdfContentStreamBuilder()
    .BeginText()
    .SetFont(PdfStandardFont.Helvetica, 18)
    .MoveText(72, 720)
    .ShowLatin1Text("Created with The KillerPDF.Engine")
    .EndText();

byte[] pdf = new PdfDocumentBuilder()
    .AddPage(612, 792, content)
    .Build();
```

The content builder also supports paths, line styles, colors, images, embedded TrueType fonts, transparency, graphics state, patterns, shadings, optional content, and tagged marked content.

## Add document features

The same builder coordinates features that otherwise require changes across the page tree, catalog, resources, name trees, structure tree, and AcroForm:

- Bookmarks, destinations, page labels, and viewer preferences
- Links, attachments, annotations, replies, and popups
- Text fields, checkboxes, radio buttons, choice fields, push buttons, and signature fields
- Output intents, embedded files, optional content groups, and structure elements

Use the typed methods on `PdfDocumentBuilder`. Avoid dropping to raw objects unless your application is implementing a PDF feature that the high-level API does not yet expose.

## Author for PDF/A and PDF/UA

The builder provides coordinated safeguards for PDF/A-4, PDF/A-4e, PDF/A-4f, and PDF/UA-2:

```csharp
var builder = new PdfDocumentBuilder()
    .EnablePdfA4Conformance()
    .EnablePdfUa2Conformance();
```

Conformance requires more than these calls. Your document still needs suitable metadata, output intents, embedded fonts, semantic structure, language information, and accessible content. Validate the finished file with an independent validator.

## Save the result

```csharp
File.WriteAllBytes("created.pdf", builder.Build());
```

Build into memory first, then commit the finished byte array to your storage layer. This prevents a failed build from leaving a partial PDF on disk.

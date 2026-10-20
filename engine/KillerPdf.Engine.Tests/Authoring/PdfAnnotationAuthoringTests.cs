using System.Text;
using KillerPdf.Engine.Authoring;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Objects;
using Xunit;

namespace KillerPdf.Engine.Tests.Authoring;

public sealed class PdfAnnotationAuthoringTests
{
    [Fact]
    public void AddTextNote_WritesContentsIdentityColorAndAppearance()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextNote(0, 72, 650, "Review résumé", new PdfRgbColor(1, 0.5, 0), open: true,
                annotationMetadata: new PdfAnnotationMetadata
                {
                    Author = "Zoë",
                    Subject = "Copy review",
                    Flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.Locked
                })
            .Build());
        PdfDictionary annotation = Annotation(document, 0);
        PdfStream appearance = Appearance(document, annotation);

        Assert.Equal("Text", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal("Review résumé", DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("Contents")])));
        Assert.True(Assert.IsType<PdfBoolean>(annotation[Name("Open")]).Value);
        Assert.Equal("KillerPDF-Note-1",
            Encoding.Latin1.GetString(Assert.IsType<PdfString>(annotation[Name("NM")]).Bytes.Span));
        Assert.Equal("Zoë", DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("T")])));
        Assert.Equal("Copy review",
            DecodeUnicode(Assert.IsType<PdfString>(annotation[Name("Subj")])));
        Assert.Equal(132, Assert.IsType<PdfInteger>(annotation[Name("F")]).Value);
        Assert.Contains("1 0.5 0 rg", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void PdfUa2_TextNoteWritesAnnotObjectReference()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Accessible note",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddBlankPage()
            .AddTextNote(0, 72, 650, "Review this paragraph")
            .AddLineAnnotation(0, new PdfPoint(72, 620), new PdfPoint(180, 620),
                contents: "Review line")
            .AddRectangleAnnotation(0, 72, 580, 100, 24,
                contents: "Review rectangle")
            .AddCaretAnnotation(0, 72, 540, 18, 24,
                contents: "Insertion point")
            .AddRedactionMark(0,
                [new PdfTextQuad(
                    new PdfPoint(72, 520), new PdfPoint(172, 520),
                    new PdfPoint(72, 500), new PdfPoint(172, 500))],
                contents: "Proposed redaction")
            .AddStructureContainer(PdfStructureType.Document)
            .Build());
        PdfDictionary annotation = Annotation(document, 0);
        PdfDictionary catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        PdfDictionary structureRoot = ResolveDictionary(document, catalog[Name("StructTreeRoot")]);
        PdfDictionary parentTree = ResolveDictionary(document, structureRoot[Name("ParentTree")]);
        PdfArray numbers = Assert.IsType<PdfArray>(parentTree[Name("Nums")]);
        PdfDictionary annotElement = ResolveDictionary(document, numbers[1]);

        Assert.True(annotation.ContainsKey(Name("StructParent")));
        Assert.Equal("Annot", Assert.IsType<PdfName>(annotElement[Name("S")]).ValueAsLatin1());
        PdfDictionary objectReference = Assert.IsType<PdfDictionary>(annotElement[Name("K")]);
        PdfDictionary referencedAnnotation = ResolveDictionary(document, objectReference[Name("Obj")]);
        Assert.Equal("Text",
            Assert.IsType<PdfName>(referencedAnnotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal("Review this paragraph",
            DecodeUnicode(Assert.IsType<PdfString>(referencedAnnotation[Name("Contents")])));
        PdfDictionary pages = ResolveDictionary(document, catalog[Name("Pages")]);
        PdfDictionary page = ResolveDictionary(document,
            Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        PdfArray annotations = Assert.IsType<PdfArray>(page[Name("Annots")]);
        Assert.Equal(5, annotations.Count);
        Assert.All(annotations, value => Assert.True(
            ResolveDictionary(document, value).ContainsKey(Name("StructParent"))));
        Assert.Equal(10, numbers.Count);

        Assert.Throws<InvalidOperationException>(() => new PdfDocumentBuilder()
            .SetMetadata(new PdfDocumentMetadata
            {
                Title = "Missing description",
                Language = "en-US"
            })
            .EnablePdfUa2Conformance()
            .AddBlankPage()
            .AddHighlight(0, 72, 650, 100, 18)
            .AddStructureContainer(PdfStructureType.Document)
            .Build());
    }

    [Fact]
    public void AddHighlight_WritesQuadPointsOpacityAndMultiplyAppearance()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddHighlight(0, 10, 20, 100, 15, "Important", opacity: 0.4)
            .Build());
        PdfDictionary annotation = Annotation(document, 0);
        PdfStream appearance = Appearance(document, annotation);
        var quadPoints = Assert.IsType<PdfArray>(annotation[Name("QuadPoints")]);
        var resources = Assert.IsType<PdfDictionary>(appearance.Dictionary[Name("Resources")]);
        var states = Assert.IsType<PdfDictionary>(resources[Name("ExtGState")]);
        var state = Assert.IsType<PdfDictionary>(states[Name("GS1")]);

        Assert.Equal("Highlight", Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.Equal(8, quadPoints.Count);
        Assert.Equal(0.4, Assert.IsType<PdfReal>(annotation[Name("CA")]).Value);
        Assert.Equal("Multiply", Assert.IsType<PdfName>(state[Name("BM")]).ValueAsLatin1());
        Assert.Contains("/GS1 gs", Encoding.ASCII.GetString(appearance.EncodedData.Span));
    }

    [Fact]
    public void AddHighlight_WithMultipleQuads_WritesUnionBoundsAndEveryTextRun()
    {
        PdfTextQuad[] quads =
        [
            new(new PdfPoint(10, 40), new PdfPoint(110, 40),
                new PdfPoint(10, 25), new PdfPoint(110, 25)),
            new(new PdfPoint(20, 20), new PdfPoint(80, 22),
                new PdfPoint(20, 5), new PdfPoint(80, 7))
        ];
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddHighlight(0, quads, "Wrapped selection")
            .Build());
        PdfDictionary annotation = Annotation(document, 0);
        var quadPoints = Assert.IsType<PdfArray>(annotation[Name("QuadPoints")]);
        var rectangle = Assert.IsType<PdfArray>(annotation[Name("Rect")]);
        string appearance = Encoding.ASCII.GetString(
            Appearance(document, annotation).EncodedData.Span);

        Assert.Equal(16, quadPoints.Count);
        Assert.Equal([10d, 5d, 110d, 40d], rectangle.Select(NumberValue));
        Assert.Equal(2, appearance.Split("\nf\n").Length - 1);
        Assert.Contains("10 0 m", appearance);
    }

    [Fact]
    public void TextMarkupQuadArguments_AreValidated()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentException>(() => builder.AddHighlight(0, []));
        Assert.Throws<ArgumentException>(() =>
            builder.AddHighlight(0, [default]));
        Assert.Throws<ArgumentException>(() => new PdfTextQuad(
            new PdfPoint(0, 0), new PdfPoint(10, 0),
            new PdfPoint(20, 0), new PdfPoint(30, 0)));
    }

    [Theory]
    [InlineData(PdfTextNoteIcon.Note, "Note")]
    [InlineData(PdfTextNoteIcon.Comment, "Comment")]
    [InlineData(PdfTextNoteIcon.Key, "Key")]
    [InlineData(PdfTextNoteIcon.Help, "Help")]
    [InlineData(PdfTextNoteIcon.NewParagraph, "NewParagraph")]
    [InlineData(PdfTextNoteIcon.Paragraph, "Paragraph")]
    [InlineData(PdfTextNoteIcon.Insert, "Insert")]
    public void AddTextNote_WritesEveryStandardIconName(
        PdfTextNoteIcon icon, string expectedName)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextNote(0, 10, 10, "Note", icon: icon)
            .Build());

        Assert.Equal(expectedName, Assert.IsType<PdfName>(
            Annotation(document, 0)[Name("Name")]).ValueAsLatin1());
    }

    [Theory]
    [InlineData(PdfTextNoteState.Marked, "Marked", "Marked")]
    [InlineData(PdfTextNoteState.Unmarked, "Unmarked", "Marked")]
    [InlineData(PdfTextNoteState.Accepted, "Accepted", "Review")]
    [InlineData(PdfTextNoteState.Rejected, "Rejected", "Review")]
    [InlineData(PdfTextNoteState.Cancelled, "Cancelled", "Review")]
    [InlineData(PdfTextNoteState.Completed, "Completed", "Review")]
    [InlineData(PdfTextNoteState.NoReviewState, "None", "Review")]
    public void AddTextNote_WritesStandardWorkflowState(
        PdfTextNoteState state, string expectedState, string expectedModel)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextNote(0, 10, 10, "Note", state: state)
            .Build());
        PdfDictionary annotation = Annotation(document, 0);

        Assert.Equal(expectedState,
            Assert.IsType<PdfName>(annotation[Name("State")]).ValueAsLatin1());
        Assert.Equal(expectedModel,
            Assert.IsType<PdfName>(annotation[Name("StateModel")]).ValueAsLatin1());
    }

    [Theory]
    [InlineData(PdfAnnotationReplyType.Reply, "R")]
    [InlineData(PdfAnnotationReplyType.Group, "Group")]
    public void AddTextNote_WritesReplyRelationship(
        PdfAnnotationReplyType replyType, string expectedName)
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextNote(0, 10, 10, "Original", name: "review-root")
            .AddTextNote(0, 40, 10, "Response", name: "review-response",
                inReplyTo: "review-root", replyType: replyType)
            .Build());
        PdfDictionary reply = Annotation(document, 1);
        PdfDictionary parent = ResolveDictionary(document, reply[Name("IRT")]);

        Assert.Equal("Original",
            DecodeUnicode(Assert.IsType<PdfString>(parent[Name("Contents")])));
        Assert.Equal(expectedName,
            Assert.IsType<PdfName>(reply[Name("RT")]).ValueAsLatin1());
        Assert.Equal("review-response",
            DecodeUnicode(Assert.IsType<PdfString>(reply[Name("NM")])));
    }

    [Fact]
    public void TextNoteReplyNames_AreValidatedBeforeAllocation()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage()
            .AddTextNote(0, 10, 10, "Original", name: "root");

        Assert.Throws<ArgumentException>(() =>
            builder.AddTextNote(0, 20, 10, "Duplicate", name: "root"));
        Assert.Throws<ArgumentException>(() =>
            builder.AddTextNote(0, 20, 10, "Dangling", inReplyTo: "missing"));
        Assert.Throws<ArgumentException>(() =>
            new PdfDocumentBuilder().AddBlankPage().AddTextNote(
                0, 20, 10, "Group", replyType: PdfAnnotationReplyType.Group));
    }

    [Fact]
    public void AddTextNote_WithPopup_WritesBidirectionalParentRelationship()
    {
        PdfDocument document = PdfDocument.Open(new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextNote(0, 10, 20, "Popup text",
                popup: new PdfAnnotationPopup(100, 200, 240, 120, open: true))
            .Build());
        PdfDictionary note = Annotation(document, 0);
        PdfDictionary popup = Annotation(document, 1);
        PdfDictionary popupFromNote = ResolveDictionary(document, note[Name("Popup")]);
        PdfDictionary noteFromPopup = ResolveDictionary(document, popup[Name("Parent")]);

        Assert.Equal("Popup",
            Assert.IsType<PdfName>(popup[Name("Subtype")]).ValueAsLatin1());
        Assert.True(Assert.IsType<PdfBoolean>(popup[Name("Open")]).Value);
        Assert.Same(popup, popupFromNote);
        Assert.Same(note, noteFromPopup);
        Assert.Equal([100d, 200d, 340d, 320d],
            Assert.IsType<PdfArray>(popup[Name("Rect")]).Select(NumberValue));
    }

    [Fact]
    public void AnnotationArguments_AreValidatedBeforeAllocation()
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddTextNote(0, 0, 0, "note", size: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddHighlight(0, 0, 0, 10, 10, opacity: 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfRgbColor(-0.1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PdfAnnotationMetadata { Flags = (PdfAnnotationFlags)1024 });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddTextNote(0, 0, 0, "note", icon: (PdfTextNoteIcon)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.AddTextNote(0, 0, 0, "note", state: (PdfTextNoteState)99));
    }

    [Theory]
    [InlineData("Underline")]
    [InlineData("StrikeOut")]
    [InlineData("Squiggly")]
    public void TextMarkupStyles_WriteTheirStandardSubtypeAndExplicitAppearance(string subtype)
    {
        var builder = new PdfDocumentBuilder().AddBlankPage();
        _ = subtype switch
        {
            "Underline" => builder.AddUnderline(0, 10, 20, 100, 15),
            "StrikeOut" => builder.AddStrikeOut(0, 10, 20, 100, 15),
            "Squiggly" => builder.AddSquiggly(0, 10, 20, 100, 15),
            _ => throw new ArgumentOutOfRangeException(nameof(subtype))
        };
        PdfDocument document = PdfDocument.Open(builder.Build());
        PdfDictionary annotation = Annotation(document, 0);

        Assert.Equal(subtype, Assert.IsType<PdfName>(annotation[Name("Subtype")]).ValueAsLatin1());
        Assert.True(Appearance(document, annotation).EncodedData.Length > 0);
    }

    [Fact]
    public void AnnotationAuthoring_IsDeterministic()
    {
        static byte[] Build() => new PdfDocumentBuilder()
            .AddBlankPage()
            .AddTextNote(0, 30, 700, "Note")
            .AddHighlight(0, 70, 650, 200, 20, "Markup")
            .AddUnderline(0, 70, 610, 200, 20)
            .Build();

        Assert.Equal(Build(), Build());
    }

    private static PdfDictionary Annotation(PdfDocument document, int index)
    {
        var catalog = ResolveDictionary(document, document.Trailer[Name("Root")]);
        var pages = ResolveDictionary(document, catalog[Name("Pages")]);
        var page = ResolveDictionary(document, Assert.IsType<PdfArray>(pages[Name("Kids")])[0]);
        return ResolveDictionary(document, Assert.IsType<PdfArray>(page[Name("Annots")])[index]);
    }

    private static PdfStream Appearance(PdfDocument document, PdfDictionary annotation) =>
        Assert.IsType<PdfStream>(document.Resolve(Assert.IsType<PdfIndirectReference>(
            Assert.IsType<PdfDictionary>(annotation[Name("AP")])[Name("N")])));
    private static string DecodeUnicode(PdfString value) =>
        Encoding.BigEndianUnicode.GetString(value.Bytes.Span[2..]);
    private static double NumberValue(PdfObject value) => value switch
    {
        PdfInteger integer => integer.Value,
        PdfReal real => real.Value,
        _ => throw new InvalidOperationException()
    };
    private static PdfDictionary ResolveDictionary(PdfDocument document, PdfObject value) =>
        Assert.IsType<PdfDictionary>(document.Resolve(Assert.IsType<PdfIndirectReference>(value)));
    private static PdfName Name(string value) => new(Encoding.ASCII.GetBytes(value));
}

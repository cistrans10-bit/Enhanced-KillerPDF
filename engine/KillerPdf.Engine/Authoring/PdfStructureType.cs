namespace KillerPdf.Engine.Authoring;

/// <summary>Standard structure types used by tagged PDF logical structure trees.</summary>
public enum PdfStructureType
{
    /// <summary>The root element for a complete document.</summary>
    Document,
    /// <summary>A large-scale document division.</summary>
    Part,
    /// <summary>A self-contained article.</summary>
    Article,
    /// <summary>A section within a document or part.</summary>
    Section,
    /// <summary>A generic block-level division.</summary>
    Division,
    /// <summary>A paragraph of text.</summary>
    Paragraph,
    /// <summary>An unnumbered heading.</summary>
    Heading,
    /// <summary>A level-one heading.</summary>
    Heading1,
    /// <summary>A level-two heading.</summary>
    Heading2,
    /// <summary>A level-three heading.</summary>
    Heading3,
    /// <summary>A level-four heading.</summary>
    Heading4,
    /// <summary>A level-five heading.</summary>
    Heading5,
    /// <summary>A level-six heading.</summary>
    Heading6,
    /// <summary>A list structure.</summary>
    List,
    /// <summary>An individual list item.</summary>
    ListItem,
    /// <summary>The label or marker of a list item.</summary>
    Label,
    /// <summary>The body content of a list item.</summary>
    ListBody,
    /// <summary>A table structure.</summary>
    Table,
    /// <summary>A row in a table.</summary>
    TableRow,
    /// <summary>A table header cell.</summary>
    TableHeaderCell,
    /// <summary>A table data cell.</summary>
    TableDataCell,
    /// <summary>A generic inline span.</summary>
    Span,
    /// <summary>An inline quotation.</summary>
    Quote,
    /// <summary>An explanatory note.</summary>
    Note,
    /// <summary>A reference to other document content.</summary>
    Reference,
    /// <summary>A fragment of computer code.</summary>
    Code,
    /// <summary>A link and its associated content.</summary>
    Link,
    /// <summary>A figure or graphical illustration.</summary>
    Figure,
    /// <summary>A mathematical formula.</summary>
    Formula,
    /// <summary>An interactive form control.</summary>
    Form
}

internal static class PdfStructureTypeNames
{
    internal static bool UsesPdf17Namespace(PdfStructureType type) =>
        type is PdfStructureType.Article or PdfStructureType.Quote or PdfStructureType.Note
            or PdfStructureType.Reference or PdfStructureType.Code;

    internal static string Name(PdfStructureType type, bool pdfUa2) =>
        pdfUa2 && type == PdfStructureType.Note ? "FENote" : Name(type);

    internal static bool UsesPdf17Namespace(PdfStructureType type, bool pdfUa2) =>
        !(pdfUa2 && type == PdfStructureType.Note) && UsesPdf17Namespace(type);

    internal static string Name(PdfStructureType type) => type switch
    {
        PdfStructureType.Document => "Document",
        PdfStructureType.Part => "Part",
        PdfStructureType.Article => "Art",
        PdfStructureType.Section => "Sect",
        PdfStructureType.Division => "Div",
        PdfStructureType.Paragraph => "P",
        PdfStructureType.Heading => "H",
        PdfStructureType.Heading1 => "H1",
        PdfStructureType.Heading2 => "H2",
        PdfStructureType.Heading3 => "H3",
        PdfStructureType.Heading4 => "H4",
        PdfStructureType.Heading5 => "H5",
        PdfStructureType.Heading6 => "H6",
        PdfStructureType.List => "L",
        PdfStructureType.ListItem => "LI",
        PdfStructureType.Label => "Lbl",
        PdfStructureType.ListBody => "LBody",
        PdfStructureType.Table => "Table",
        PdfStructureType.TableRow => "TR",
        PdfStructureType.TableHeaderCell => "TH",
        PdfStructureType.TableDataCell => "TD",
        PdfStructureType.Span => "Span",
        PdfStructureType.Quote => "Quote",
        PdfStructureType.Note => "Note",
        PdfStructureType.Reference => "Reference",
        PdfStructureType.Code => "Code",
        PdfStructureType.Link => "Link",
        PdfStructureType.Figure => "Figure",
        PdfStructureType.Formula => "Formula",
        PdfStructureType.Form => "Form",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}

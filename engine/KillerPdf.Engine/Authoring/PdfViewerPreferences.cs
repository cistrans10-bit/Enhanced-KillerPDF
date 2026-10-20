using System.Text;
using KillerPdf.Engine.Objects;
using KillerPdf.Engine.Syntax;

namespace KillerPdf.Engine.Authoring;

/// <summary>Initial page arrangements available to conforming PDF viewers.</summary>
public enum PdfPageLayout
{
    /// <summary>Shows one page at a time.</summary>
    SinglePage,
    /// <summary>Shows pages in one continuous column.</summary>
    OneColumn,
    /// <summary>Shows two continuous columns with odd pages on the left.</summary>
    TwoColumnLeft,
    /// <summary>Shows two continuous columns with odd pages on the right.</summary>
    TwoColumnRight,
    /// <summary>Shows two pages at a time with odd pages on the left.</summary>
    TwoPageLeft,
    /// <summary>Shows two pages at a time with odd pages on the right.</summary>
    TwoPageRight
}

/// <summary>Navigation or presentation modes available when a PDF opens.</summary>
public enum PdfPageMode
{
    /// <summary>Shows neither outlines nor thumbnails.</summary>
    UseNone,
    /// <summary>Shows the document outline panel.</summary>
    UseOutlines,
    /// <summary>Shows the page-thumbnail panel.</summary>
    UseThumbs,
    /// <summary>Opens the document in full-screen mode.</summary>
    FullScreen,
    /// <summary>Shows the optional-content group panel.</summary>
    UseOptionalContent,
    /// <summary>Shows the attachments panel.</summary>
    UseAttachments
}

/// <summary>Predominant reading order used when arranging pages.</summary>
public enum PdfReadingDirection
{
    /// <summary>Reads pages from left to right.</summary>
    LeftToRight,
    /// <summary>Reads pages from right to left.</summary>
    RightToLeft
}
/// <summary>Viewer scaling behavior used by the print dialog.</summary>
public enum PdfPrintScaling
{
    /// <summary>Uses the viewer application's default scaling behavior.</summary>
    ApplicationDefault,
    /// <summary>Requests no automatic print scaling.</summary>
    None
}
/// <summary>Preferred simplex or duplex print mode.</summary>
public enum PdfDuplexMode
{
    /// <summary>Leaves duplex selection to the viewer or printer.</summary>
    Default,
    /// <summary>Prints on one side of each sheet.</summary>
    Simplex,
    /// <summary>Prints duplex and flips along the short edge.</summary>
    DuplexFlipShortEdge,
    /// <summary>Prints duplex and flips along the long edge.</summary>
    DuplexFlipLongEdge
}

/// <summary>Typed viewer and print preferences stored in the document catalog.</summary>
public sealed record PdfViewerPreferences
{
    /// <summary>Requests that the viewer hide its toolbars.</summary>
    public bool HideToolbar { get; init; }
    /// <summary>Requests that the viewer hide its menu bar.</summary>
    public bool HideMenuBar { get; init; }
    /// <summary>Requests that the viewer hide window controls and user-interface elements.</summary>
    public bool HideWindowUi { get; init; }
    /// <summary>Requests that the viewer resize its window to fit the first displayed page.</summary>
    public bool FitWindow { get; init; }
    /// <summary>Requests that the viewer center its window on screen.</summary>
    public bool CenterWindow { get; init; }
    /// <summary>Displays the document title instead of the filename in the title bar.</summary>
    public bool DisplayDocumentTitle { get; init; }
    /// <summary>Selects a print tray based on the PDF page size.</summary>
    public bool PickTrayByPdfSize { get; init; }
    /// <summary>Gets the predominant reading direction.</summary>
    public PdfReadingDirection ReadingDirection { get; init; }
    /// <summary>Gets the requested print-scaling behavior.</summary>
    public PdfPrintScaling PrintScaling { get; init; }
    /// <summary>Gets the requested simplex or duplex print mode.</summary>
    public PdfDuplexMode Duplex { get; init; }

    internal PdfVersion MinimumVersion(bool requireDocumentTitle = false)
    {
        if (Duplex != PdfDuplexMode.Default || PickTrayByPdfSize)
            return PdfVersion.Pdf17;
        if (PrintScaling == PdfPrintScaling.None)
            return new PdfVersion(1, 6);
        if (DisplayDocumentTitle || requireDocumentTitle)
            return new PdfVersion(1, 4);
        if (ReadingDirection == PdfReadingDirection.RightToLeft)
            return new PdfVersion(1, 3);
        return new PdfVersion(1, 2);
    }

    internal PdfDictionary ToDictionary(bool requireDocumentTitle = false)
    {
        var entries = new List<KeyValuePair<PdfName, PdfObject>>();
        void AddTrue(string name, bool value)
        {
            if (value)
                entries.Add(new KeyValuePair<PdfName, PdfObject>(
                    Name(name), new PdfBoolean(true)));
        }
        AddTrue("HideToolbar", HideToolbar);
        AddTrue("HideMenubar", HideMenuBar);
        AddTrue("HideWindowUI", HideWindowUi);
        AddTrue("FitWindow", FitWindow);
        AddTrue("CenterWindow", CenterWindow);
        AddTrue("DisplayDocTitle", DisplayDocumentTitle || requireDocumentTitle);
        AddTrue("PickTrayByPDFSize", PickTrayByPdfSize);
        if (ReadingDirection == PdfReadingDirection.RightToLeft)
            entries.Add(new KeyValuePair<PdfName, PdfObject>(
                Name("Direction"), Name("R2L")));
        if (PrintScaling == PdfPrintScaling.None)
            entries.Add(new KeyValuePair<PdfName, PdfObject>(
                Name("PrintScaling"), Name("None")));
        if (Duplex != PdfDuplexMode.Default)
            entries.Add(new KeyValuePair<PdfName, PdfObject>(
                Name("Duplex"), Name(Duplex switch
                {
                    PdfDuplexMode.Simplex => "Simplex",
                    PdfDuplexMode.DuplexFlipShortEdge => "DuplexFlipShortEdge",
                    PdfDuplexMode.DuplexFlipLongEdge => "DuplexFlipLongEdge",
                    _ => throw new InvalidOperationException(
                        $"Unsupported duplex mode: {Duplex}.")
                })));
        return new PdfDictionary(entries);
    }

    private static PdfName Name(string value) =>
        new(Encoding.ASCII.GetBytes(value));
}

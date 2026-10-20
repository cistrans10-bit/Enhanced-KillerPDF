namespace KillerPdf.Engine.Authoring;

/// <summary>The order in which keyboard focus traverses annotations on a page.</summary>
public enum PdfPageTabOrder
{
    /// <summary>Traverses annotations in row order.</summary>
    Row,
    /// <summary>Traverses annotations in column order.</summary>
    Column,
    /// <summary>Traverses annotations in structure-tree order.</summary>
    Structure,
    /// <summary>Traverses annotations in page annotation-array order.</summary>
    AnnotationArray
}

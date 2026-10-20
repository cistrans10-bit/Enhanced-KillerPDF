namespace KillerPdf.Engine.Authoring;

/// <summary>How a signature-field lock selects affected fields.</summary>
public enum PdfSignatureLockAction
{
    /// <summary>Locks every field.</summary>
    All,
    /// <summary>Locks only the listed fields.</summary>
    Include,
    /// <summary>Locks every field except those listed.</summary>
    Exclude
}

/// <summary>Document changes permitted after an approval signature is applied.</summary>
public enum PdfSignatureLockPermission
{
    /// <summary>Permits no changes.</summary>
    NoChanges = 1,
    /// <summary>Permits form filling and approval signatures.</summary>
    FormFillingAndSignatures = 2,
    /// <summary>Also permits annotation creation, deletion, and modification.</summary>
    FormFillingSignaturesAndAnnotations = 3
}

/// <summary>Fields that become locked when a signature field is signed.</summary>
public sealed record PdfSignatureFieldLock(
    PdfSignatureLockAction Action,
    IReadOnlyList<string>? Fields = null,
    PdfSignatureLockPermission? Permission = null);

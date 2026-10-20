namespace KillerPdf.Engine.Authoring;

/// <summary>A detached signature encoding permitted by a signature seed value.</summary>
public enum PdfSignatureSubFilter
{
    /// <summary>An adbe.pkcs7.detached CMS signature.</summary>
    AdobePkcs7Detached,
    /// <summary>An ETSI.CAdES.detached CAdES signature.</summary>
    EtsiCadesDetached
}

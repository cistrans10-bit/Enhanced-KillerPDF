namespace KillerPdf.Engine.Security;

/// <summary>Identifies which password class authenticated an encrypted PDF.</summary>
public enum PdfPasswordAuthenticationRole
{
    /// <summary>No password has been authenticated.</summary>
    None,
    /// <summary>The user password was authenticated.</summary>
    User,
    /// <summary>The owner password was authenticated.</summary>
    Owner
}

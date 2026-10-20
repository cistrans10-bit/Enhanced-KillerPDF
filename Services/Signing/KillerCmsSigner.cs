using System;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace KillerPDF.Services.Signing
{
    /// <summary>Creates detached CMS signatures with .NET cryptography.</summary>
    internal sealed class KillerCmsSigner(X509Certificate2 cert)
    {
        private static readonly Oid Sha256 = new("2.16.840.1.101.3.4.2.1");

        public string CertificateName =>
            cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false) is { Length: > 0 } name
                ? name
                : cert.Subject;

        public byte[] CreateDetachedCms(ReadOnlyMemory<byte> data)
        {
            if (data.IsEmpty)
                throw new CryptographicException("The content to sign was empty.");

            var signedCms = new SignedCms(new ContentInfo(data.ToArray()), detached: true);
            var signer = new CmsSigner(cert)
            {
                DigestAlgorithm = Sha256,
                IncludeOption = X509IncludeOption.WholeChain,
            };

            // Allow a token or cloud KSP to display its PIN or confirmation prompt when required.
            signedCms.ComputeSignature(signer, silent: false);
            return signedCms.Encode();
        }
    }
}

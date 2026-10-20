using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using KillerPdf.Engine.Documents;
using KillerPdf.Engine.Signing;

namespace KillerPDF.Services.Signing
{
    /// <summary>Creates detached-CMS approval signatures through The KillerPDF.Engine.</summary>
    internal sealed class PdfSigner
    {
        public sealed record SignInfo(string Reason, string Location, string Contact,
            VisibleSignatureInfo? VisibleAppearance = null);

        public sealed record VisibleSignatureInfo(
            int PageIndex, double Left, double Bottom, double Width, double Height,
            double FontSize, string Text);

        // Keep signing behind the injected service instance used by the application and tests.
#pragma warning disable CA1822
        public void Sign(string inputPath, string outputPath, X509Certificate2 cert, SignInfo info)
#pragma warning restore CA1822
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            ArgumentNullException.ThrowIfNull(cert);
            ArgumentNullException.ThrowIfNull(info);

            if (!cert.HasPrivateKey)
                throw new InvalidOperationException(
                    "The selected certificate has no private key, so it cannot sign.");

            PdfDocument document = PdfDocument.Open(File.ReadAllBytes(inputPath));
            var cmsSigner = new KillerCmsSigner(cert);
            var options = new PdfSignatureOptions
            {
                SignerName = cmsSigner.CertificateName,
                Reason = NullIfWhiteSpace(info.Reason),
                Location = NullIfWhiteSpace(info.Location),
                ContactInformation = NullIfWhiteSpace(info.Contact),
                SignerCertificate = cert.RawDataMemory,
                PageIndex = info.VisibleAppearance?.PageIndex ?? 0,
                VisibleAppearance = info.VisibleAppearance is { } appearance
                    ? new PdfSignatureAppearance
                    {
                        Left = appearance.Left,
                        Bottom = appearance.Bottom,
                        Width = appearance.Width,
                        Height = appearance.Height,
                        FontSize = appearance.FontSize,
                        Text = appearance.Text
                    }
                    : null,
            };

            byte[] signed = PdfDetachedSignatureWriter.Sign(
                document,
                cmsSigner.CreateDetachedCms,
                options);
            File.WriteAllBytes(outputPath, signed);
        }

        private static string? NullIfWhiteSpace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

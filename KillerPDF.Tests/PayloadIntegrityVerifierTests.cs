using System.IO;
using System.Security.Cryptography;
using System.Text;
using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests;

public sealed class PayloadIntegrityVerifierTests
{
    [Fact]
    public void Verify_AcceptsMatchingManifest()
    {
        WithPayload((root, file) =>
        {
            PayloadIntegrityResult result = PayloadIntegrityVerifier.Verify(root);

            Assert.True(result.Success);
            Assert.Equal(1, result.VerifiedFiles);
            Assert.Empty(result.Errors);
        });
    }

    [Fact]
    public void Verify_ReportsTamperedFile()
    {
        WithPayload((root, file) =>
        {
            byte[] content = File.ReadAllBytes(file);
            content[0] ^= 0x01;
            File.WriteAllBytes(file, content);

            PayloadIntegrityResult result = PayloadIntegrityVerifier.Verify(root);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error =>
                error.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Verify_ReportsFilesOutsideManifest()
    {
        WithPayload((root, file) =>
        {
            File.WriteAllText(Path.Combine(root, "unlisted.dll"), "not trusted");

            PayloadIntegrityResult result = PayloadIntegrityVerifier.Verify(root);

            Assert.False(result.Success);
            Assert.Contains("Unexpected file: unlisted.dll", result.Errors);
        });
    }

    [Fact]
    public void Verify_AcceptsPortableMarkerButStillRejectsOtherUnexpectedFiles()
    {
        WithPayload((root, file) =>
        {
            File.WriteAllText(Path.Combine(root, ".killerpdf-portable"), "launcher metadata");

            PayloadIntegrityResult markerOnly = PayloadIntegrityVerifier.Verify(root);

            Assert.True(markerOnly.Success);
            Assert.Empty(markerOnly.Errors);

            File.WriteAllText(Path.Combine(root, "unlisted.dll"), "not trusted");

            PayloadIntegrityResult withUntrustedFile = PayloadIntegrityVerifier.Verify(root);

            Assert.False(withUntrustedFile.Success);
            Assert.Contains("Unexpected file: unlisted.dll", withUntrustedFile.Errors);
        });
    }

    [Fact]
    public void Verify_ReportsMissingManifest()
    {
        string root = NewTemporaryDirectory();
        try
        {
            PayloadIntegrityResult result = PayloadIntegrityVerifier.Verify(root);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Contains("Manifest not found"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Verify_RejectsUnsafeManifestPath()
    {
        string root = NewTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "payload.manifest"),
                new string('0', 64) + "\t0\t../outside.dll");

            PayloadIntegrityResult result = PayloadIntegrityVerifier.Verify(root);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Contains("Unsafe path"));
        }
        finally { Directory.Delete(root, true); }
    }

    private static void WithPayload(Action<string, string> test)
    {
        string root = NewTemporaryDirectory();
        try
        {
            string file = Path.Combine(root, "component.dll");
            byte[] content = Encoding.UTF8.GetBytes("verified payload");
            File.WriteAllBytes(file, content);
            string hash = Convert.ToHexString(SHA256.HashData(content));
            File.WriteAllText(Path.Combine(root, "payload.manifest"),
                $"{hash}\t{content.Length}\tcomponent.dll");
            test(root, file);
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "killerpdf-integrity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

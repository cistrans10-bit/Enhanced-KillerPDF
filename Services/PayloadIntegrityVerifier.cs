using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace KillerPDF.Services;

internal sealed record PayloadIntegrityResult(
    bool Success,
    int VerifiedFiles,
    IReadOnlyList<string> Errors);

internal static class PayloadIntegrityVerifier
{
    private const string ManifestName = "payload.manifest";
    private const string PortableMarkerName = ".killerpdf-portable";

    internal static PayloadIntegrityResult Verify(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string root = Path.GetFullPath(directory);
        string manifestPath = Path.Combine(root, ManifestName);
        if (!File.Exists(manifestPath))
            return Failure($"Manifest not found: {manifestPath}");

        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int verified = 0;
        int lineNumber = 0;
        foreach (string line in File.ReadLines(manifestPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            string[] parts = line.Split('\t', 3);
            if (parts.Length != 3 || parts[0].Length != 64
                || !long.TryParse(parts[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out long expectedSize)
                || expectedSize < 0)
            {
                errors.Add($"Manifest line {lineNumber} is invalid.");
                continue;
            }

            string relative;
            try { relative = NormalizeRelativePath(parts[2]); }
            catch (InvalidDataException ex)
            {
                errors.Add($"Manifest line {lineNumber}: {ex.Message}");
                continue;
            }
            if (!seen.Add(relative))
            {
                errors.Add($"Manifest contains duplicate path: {relative}");
                continue;
            }

            string path = Path.GetFullPath(Path.Combine(
                root, relative.Replace('/', Path.DirectorySeparatorChar)));
            string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Manifest contains unsafe path: {relative}");
                continue;
            }
            if (!File.Exists(path))
            {
                errors.Add($"Missing: {relative}");
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length != expectedSize)
            {
                errors.Add($"Size mismatch: {relative}");
                continue;
            }
            string actual;
            using (FileStream stream = File.OpenRead(path))
                actual = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actual, parts[0], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"SHA-256 mismatch: {relative}");
                continue;
            }
            verified++;
        }

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(file, manifestPath, StringComparison.OrdinalIgnoreCase)) continue;
            string relative = Path.GetRelativePath(root, file)
                .Replace(Path.DirectorySeparatorChar, '/');
            // The portable launcher writes this identity marker after verifying extraction. It is
            // launcher-owned metadata, not an untrusted payload addition (#279).
            if (string.Equals(relative, PortableMarkerName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Contains(relative))
                errors.Add($"Unexpected file: {relative}");
        }

        return new PayloadIntegrityResult(errors.Count == 0, verified, errors);
    }

    private static string NormalizeRelativePath(string path)
    {
        string normalized = path.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(path)
            || normalized.Contains(':')
            || normalized.Split('/').Any(part =>
                part.Length == 0 || part is "." or ".."))
            throw new InvalidDataException($"Unsafe path: {path}");
        return normalized;
    }

    private static PayloadIntegrityResult Failure(string error) =>
        new(false, 0, [error]);
}

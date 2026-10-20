using System.IO;
using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests;

public sealed class ReplaceFileAtomicallyTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kp-replace-" + Guid.NewGuid().ToString("N"));

    public ReplaceFileAtomicallyTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { }
    }

    [Fact]
    public void ReplacesAnExistingDestinationAndLeavesNoTemporaryFile()
    {
        string finished = Path.Combine(_folder, "finished.pdf");
        string destination = Path.Combine(_folder, "document.pdf");
        File.WriteAllText(finished, "new");
        File.WriteAllText(destination, "old");

        PdfEngineIntegration.ReplaceFileAtomically(finished, destination);

        Assert.Equal("new", File.ReadAllText(destination));
        Assert.Equal("new", File.ReadAllText(finished));
        Assert.Equal(2, Directory.GetFiles(_folder).Length);
    }

    [Fact]
    public void CreatesADestinationThatDoesNotExistYet()
    {
        string finished = Path.Combine(_folder, "finished.pdf");
        string destination = Path.Combine(_folder, "document.pdf");
        File.WriteAllText(finished, "new");

        PdfEngineIntegration.ReplaceFileAtomically(finished, destination);

        Assert.Equal("new", File.ReadAllText(destination));
        Assert.Equal(2, Directory.GetFiles(_folder).Length);
    }

    [Fact]
    public void KeepsThePreviousDestinationWhenTheFinishedFileIsMissing()
    {
        string destination = Path.Combine(_folder, "document.pdf");
        File.WriteAllText(destination, "old");

        Assert.Throws<FileNotFoundException>(() => PdfEngineIntegration.ReplaceFileAtomically(
            Path.Combine(_folder, "missing.pdf"), destination));

        Assert.Equal("old", File.ReadAllText(destination));
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public void KeepsThePreviousDestinationWhenItIsLockedByAnotherProgram()
    {
        string finished = Path.Combine(_folder, "finished.pdf");
        string destination = Path.Combine(_folder, "document.pdf");
        File.WriteAllText(finished, "new");
        File.WriteAllText(destination, "old");

        using (new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<Exception>(() =>
                PdfEngineIntegration.ReplaceFileAtomically(finished, destination));
        }

        Assert.Equal("old", File.ReadAllText(destination));
        Assert.Equal(2, Directory.GetFiles(_folder).Length);
    }
}

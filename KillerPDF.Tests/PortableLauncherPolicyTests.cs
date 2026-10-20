using System;
using System.IO;
using KillerLauncher;
using Xunit;

namespace KillerPDF.Tests;

public sealed class PortableLauncherPolicyTests
{
    [Theory]
    [InlineData("/install-user")]
    [InlineData("/silent")]
    [InlineData("/register-user")]
    [InlineData("/register-machine")]
    [InlineData("/REGISTER-USER")]
    public void PortablePackageRejectsInstallationAndRegistrationArguments(string argument)
    {
        Assert.True(PortableLauncherPolicy.IsInstallationArgument(argument));
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("/verify")]
    [InlineData("--verify")]
    public void PortablePackageAllowsOrdinaryArguments(string argument)
    {
        Assert.False(PortableLauncherPolicy.IsInstallationArgument(argument));
    }

    [Fact]
    public void LockedPortableDirectoryIsLeftForTheNextSweep()
    {
        string directory = Path.Combine(Path.GetTempPath(), "killerpdf-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "locked.bin");
        File.WriteAllBytes(file, [1, 2, 3]);

        try
        {
            using (File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.False(PortableDirectoryCleanup.TryDelete(directory, attempts: 1, baseDelayMilliseconds: 0));
                Assert.True(Directory.Exists(directory));
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}

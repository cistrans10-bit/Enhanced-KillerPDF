using KillerLauncher;
using Xunit;

namespace KillerPDF.Tests;

public sealed class InstallerPrerequisitePolicyTests
{
    [Fact]
    public void SilentInstallContinuesWhenDesktopRuntimeIsPresent() =>
        Assert.Null(InstallerPrerequisitePolicy.SilentInstallRejection(hasDesktopRuntime10: true));

    [Fact]
    public void SilentInstallReturnsStableFailureBeforeInstallWhenDesktopRuntimeIsMissing() =>
        Assert.Equal(InstallerPrerequisitePolicy.MissingDesktopRuntimeExitCode,
            InstallerPrerequisitePolicy.SilentInstallRejection(hasDesktopRuntime10: false));
}

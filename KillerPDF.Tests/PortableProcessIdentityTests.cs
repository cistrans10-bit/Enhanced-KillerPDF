using System.Diagnostics;
using System.Globalization;
using System.IO;
using KillerLauncher;
using Xunit;

namespace KillerPDF.Tests;

public sealed class PortableProcessIdentityTests
{
    [Fact]
    public void CurrentProcessMatchesItsPidAndStartTime()
    {
        using Process process = Process.GetCurrentProcess();

        Assert.True(PortableProcessIdentity.IsLive(
            process.Id.ToString(CultureInfo.InvariantCulture),
            process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ReusedPidDoesNotMatchDifferentStartTime()
    {
        using Process process = Process.GetCurrentProcess();

        Assert.False(PortableProcessIdentity.IsLive(
            process.Id.ToString(CultureInfo.InvariantCulture), "1"));
    }

    [Fact]
    public void LegacyChildMustRunInsideRecordedDirectory()
    {
        using Process process = Process.GetCurrentProcess();

        Assert.False(PortableProcessIdentity.IsLive(
            process.Id.ToString(CultureInfo.InvariantCulture), null,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}

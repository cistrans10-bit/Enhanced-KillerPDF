using System.IO;
using System.Text.Json;
using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests;

[Collection("Environment variables")]
public sealed class AppDataPathsTests
{
    [Fact]
    public void StartupSmokeDataDoesNotUseInheritedPortableSettings()
    {
        string root = Path.Combine(Path.GetTempPath(), $"killerpdf-smoke-isolation-{Guid.NewGuid():N}");
        string launcher = Path.Combine(root, "KillerPDF-Portable.exe");
        string isolated = Path.Combine(root, "isolated");
        string? previousLauncher = Environment.GetEnvironmentVariable("KILLERPDF_LAUNCHER_PATH");
        string? previousSmokeRoot = AppDataPaths.StartupSmokeRoot;
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(launcher, []);
            Environment.SetEnvironmentVariable("KILLERPDF_LAUNCHER_PATH", launcher);
            AppDataPaths.StartupSmokeRoot = isolated;

            Assert.Null(AppDataPaths.GetPortableSetting("LastFile"));
            Assert.Equal(isolated, AppDataPaths.LocalRoot);
            Assert.Equal(isolated, AppDataPaths.UserRoot);
            Assert.Equal(Path.Combine(isolated, "signatures.json"), AppDataPaths.SignaturesFile);
            AppDataPaths.SetPortableSetting("Locale", "en-US");
            Assert.True(File.Exists(Path.Combine(isolated, "settings.json")));
            Assert.False(Directory.Exists(Path.Combine(root, "KillerPDF-Data")));
        }
        finally
        {
            AppDataPaths.StartupSmokeRoot = previousSmokeRoot;
            Environment.SetEnvironmentVariable("KILLERPDF_LAUNCHER_PATH", previousLauncher);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PortableSettingsAreStoredBesideTheLauncher()
    {
        string root = Path.Combine(Path.GetTempPath(), $"killerpdf-portable-data-{Guid.NewGuid():N}");
        string launcher = Path.Combine(root, "KillerPDF-Portable.exe");
        string? previous = Environment.GetEnvironmentVariable("KILLERPDF_LAUNCHER_PATH");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(launcher, []);
            Environment.SetEnvironmentVariable("KILLERPDF_LAUNCHER_PATH", launcher);

            AppDataPaths.SetPortableSetting("Locale", "ja-JP");

            string dataRoot = Path.Combine(root, "KillerPDF-Data");
            Assert.Equal(dataRoot, AppDataPaths.PortableRoot);
            Assert.Equal("ja-JP", AppDataPaths.GetPortableSetting("Locale"));
            Dictionary<string, string>? settings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(Path.Combine(dataRoot, "settings.json")));
            Assert.Equal("ja-JP", settings?["Locale"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KILLERPDF_LAUNCHER_PATH", previous);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

[CollectionDefinition("Environment variables", DisableParallelization = true)]
public sealed class EnvironmentVariableCollection;

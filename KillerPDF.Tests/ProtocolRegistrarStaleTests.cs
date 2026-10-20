using System;
using System.IO;
using KillerPDF.Services;
using Microsoft.Win32;
using Xunit;

namespace KillerPDF.Tests;

/// <summary>
/// #246: a per-user protocol registration outlives the copy that wrote it, and HKCU shadows
/// HKLM, so a dead handler hijacks the browser handoff from a working machine-wide install.
///
/// ProtocolRegistrar takes its registry root as a parameter, so every case here runs against a
/// scratch key under HKCU and never touches the real Software\Classes\killerpdf.
/// </summary>
public sealed class ProtocolRegistrarStaleTests : IDisposable
{
    private const string CommandPath = @"Software\Classes\killerpdf\shell\open\command";

    // Flat, not nested under a shared parent: DeleteSubKeyTree in Dispose then removes the whole
    // thing rather than leaving an empty Software\KillerPDF.Tests behind on the tester's machine.
    private readonly string _rootPath = @"Software\KillerPDF.Tests-" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _root;
    private readonly string _directory;

    public ProtocolRegistrarStaleTests()
    {
        _root = Registry.CurrentUser.CreateSubKey(_rootPath)
            ?? throw new InvalidOperationException("The scratch registry root could not be created.");
        _directory = Path.Combine(Path.GetTempPath(), "kpdf-protocol-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        _root.Dispose();
        try { Registry.CurrentUser.DeleteSubKeyTree(_rootPath, throwOnMissingSubKey: false); } catch { }
        try { Directory.Delete(_directory, recursive: true); } catch { }
    }

    private string LivingExecutable()
    {
        string path = Path.Combine(_directory, "KillerPDF.App.exe");
        File.WriteAllBytes(path, []);
        return path;
    }

    private string DeletedExecutable() => Path.Combine(_directory, "gone", "KillerPDF.App.exe");

    private bool RegistrationExists()
    {
        using RegistryKey? command = _root.OpenSubKey(CommandPath);
        return command != null;
    }

    [Fact]
    public void RemoveStaleRegistration_DeletesARegistrationWhoseExecutableIsGone()
    {
        ProtocolRegistrar.Register(_root, DeletedExecutable());
        Assert.True(RegistrationExists());

        ProtocolRegistrar.RemoveStaleRegistration(_root);

        Assert.False(RegistrationExists());
    }

    [Fact]
    public void RemoveStaleRegistration_KeepsARegistrationWhoseExecutableStillExists()
    {
        string executable = LivingExecutable();
        ProtocolRegistrar.Register(_root, executable);

        ProtocolRegistrar.RemoveStaleRegistration(_root);

        Assert.True(RegistrationExists());
        Assert.Equal(executable, ProtocolRegistrar.RegisteredAppPath(_root));
    }

    [Fact]
    public void RemoveStaleRegistration_DoesNothingWhenThereIsNoRegistration()
    {
        ProtocolRegistrar.RemoveStaleRegistration(_root);

        Assert.False(RegistrationExists());
    }

    [Fact]
    public void RegisteredAppPath_ReadsThePathBackOutOfTheQuotedCommand()
    {
        // The command is written as "<appPath>" "%1"; a path with a space is the case that
        // makes the quotes load-bearing.
        string executable = Path.Combine(_directory, "Program Files", "KillerPDF.App.exe");

        ProtocolRegistrar.Register(_root, executable);

        Assert.Equal(executable, ProtocolRegistrar.RegisteredAppPath(_root));
    }

    [Fact]
    public void RegisteredAppPath_ReturnsNullWhenNothingIsRegistered()
    {
        Assert.Null(ProtocolRegistrar.RegisteredAppPath(_root));
    }

    [Fact]
    public void ShouldRefreshPerUser_DoesNotShadowALiveMachineHandler()
    {
        using RegistryKey user = _root.CreateSubKey("User")!;
        using RegistryKey machine = _root.CreateSubKey("Machine")!;
        ProtocolRegistrar.Register(machine, LivingExecutable());
        string portable = Path.Combine(_directory, "portable", "KillerPDF.exe");

        Assert.False(ProtocolRegistrar.ShouldRefreshPerUser(user, machine, portable));
    }

    [Fact]
    public void ShouldRefreshPerUser_DoesNotTakeAValidHandlerFromAnotherCopy()
    {
        using RegistryKey user = _root.CreateSubKey("User")!;
        using RegistryKey machine = _root.CreateSubKey("Machine")!;
        ProtocolRegistrar.Register(user, LivingExecutable());
        string portable = Path.Combine(_directory, "portable", "KillerPDF.exe");

        Assert.False(ProtocolRegistrar.ShouldRefreshPerUser(user, machine, portable));
    }

    [Fact]
    public void ShouldRefreshPerUser_AllowsFirstRegistrationAndOwnerRefresh()
    {
        using RegistryKey user = _root.CreateSubKey("User")!;
        using RegistryKey machine = _root.CreateSubKey("Machine")!;
        string executable = LivingExecutable();

        Assert.True(ProtocolRegistrar.ShouldRefreshPerUser(user, machine, executable));
        ProtocolRegistrar.Register(user, executable);
        Assert.True(ProtocolRegistrar.ShouldRefreshPerUser(user, machine, executable));
    }
}

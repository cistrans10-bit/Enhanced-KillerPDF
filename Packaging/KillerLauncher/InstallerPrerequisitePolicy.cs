namespace KillerLauncher
{
    internal static class InstallerPrerequisitePolicy
    {
        internal const int MissingDesktopRuntimeExitCode = 10;

        internal static int? SilentInstallRejection(bool hasDesktopRuntime10) =>
            hasDesktopRuntime10 ? (int?)null : MissingDesktopRuntimeExitCode;
    }
}

using System;

namespace KillerLauncher
{
    internal static class PortableLauncherPolicy
    {
        internal static bool IsInstallationArgument(string argument) =>
            string.Equals(argument, "/install-user", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "/silent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "/register-user", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "/register-machine", StringComparison.OrdinalIgnoreCase);
    }
}

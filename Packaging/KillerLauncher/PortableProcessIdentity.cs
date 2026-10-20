using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace KillerLauncher
{
    internal static class PortableProcessIdentity
    {
        internal static bool IsLive(string pidText, string? startTicksText,
            string? requiredDirectory = null)
        {
            if (!int.TryParse(pidText, NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
                return false;
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited) return false;
                if (startTicksText != null)
                {
                    if (!long.TryParse(startTicksText, NumberStyles.None,
                            CultureInfo.InvariantCulture, out long expectedTicks)
                        || process.StartTime.ToUniversalTime().Ticks != expectedTicks)
                        return false;
                }
                if (requiredDirectory == null) return true;
                string executable = process.MainModule?.FileName ?? string.Empty;
                string prefix = requiredDirectory.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                return executable.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}

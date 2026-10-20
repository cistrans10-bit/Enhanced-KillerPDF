using System;
using System.IO;
using System.Threading;

namespace KillerLauncher
{
    internal static class PortableDirectoryCleanup
    {
        internal static bool TryDelete(string directory, int attempts = 5, int baseDelayMilliseconds = 150)
        {
            if (!Directory.Exists(directory)) return true;
            if (attempts < 1) throw new ArgumentOutOfRangeException(nameof(attempts));
            if (baseDelayMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(baseDelayMilliseconds));

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                try
                {
                    foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                        try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                    Directory.Delete(directory, recursive: true);
                    return true;
                }
                catch when (attempt + 1 < attempts)
                {
                    Thread.Sleep(baseDelayMilliseconds * (attempt + 1));
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }
}

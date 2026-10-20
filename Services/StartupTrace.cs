using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace KillerPDF.Services
{
    /// <summary>
    /// Opt-in startup timing used by the release benchmark. Normal launches do no file I/O.
    /// Set KILLERPDF_STARTUP_TRACE to an output path before starting the process.
    /// </summary>
    internal static class StartupTrace
    {
        private const string TraceEnvironmentVariable = "KILLERPDF_STARTUP_TRACE";
        private static readonly Lock Gate = new();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly string? OutputPath = Environment.GetEnvironmentVariable(TraceEnvironmentVariable);
        private static bool _headerWritten;

        internal static bool Enabled => !string.IsNullOrWhiteSpace(OutputPath);

        internal static void Mark(string stage)
        {
            if (!Enabled) return;

            try
            {
                lock (Gate)
                {
                    var directory = Path.GetDirectoryName(OutputPath!);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                    var sb = new StringBuilder();
                    if (!_headerWritten)
                    {
                        _headerWritten = true;
                        var process = Process.GetCurrentProcess();
                        sb.Append("# KillerPDF startup trace | pid=")
                          .Append(process.Id)
                          .Append(" | processStartUtc=")
                          .Append(process.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                          .Append(" | traceStartUtc=")
                          .Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                          .AppendLine();
                    }

                    sb.Append(Clock.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture))
                      .Append('\t')
                      .AppendLine(stage);
                    File.AppendAllText(OutputPath!, sb.ToString(), new UTF8Encoding(false));
                }
            }
            catch
            {
                // Diagnostics must never make startup fail.
            }
        }
    }
}

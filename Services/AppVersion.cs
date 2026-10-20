using System.Reflection;

namespace KillerPDF
{
    /// <summary>One display version for the UI, CLI, diagnostics, and Windows registration.</summary>
    internal static class AppVersion
    {
        internal static string Display
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                var informational = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                if (informational == null || informational.Trim().Length == 0)
                    return assembly.GetName().Version?.ToString(3) ?? "?";

                return informational.Split('+')[0];
            }
        }
    }
}

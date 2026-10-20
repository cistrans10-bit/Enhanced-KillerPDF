using System.Runtime.CompilerServices;
using System.Text;

namespace KillerPDF.Services;

internal static class RuntimeEncodingBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}

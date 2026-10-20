using System.Text;
using Xunit;

namespace KillerPDF.Tests;

public sealed class RuntimeEncodingBootstrapTests
{
    [Fact]
    public void ModuleInitializationMakesWindows1252Available()
    {
        Encoding encoding = Encoding.GetEncoding(1252);

        Assert.Equal(1252, encoding.CodePage);
        Assert.Equal("€", encoding.GetString([0x80]));
    }
}

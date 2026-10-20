using KillerPDF.Services;
using KillerPdf.Engine.Authoring;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace KillerPDF.Tests;

public sealed class TextRunServiceTests
{
    [Fact]
    public void AvailableTextDependencyKeepsSelectableTextAndCachedGeometry()
    {
        string path = Path.Combine(Path.GetTempPath(), $"killerpdf-text-runs-{Guid.NewGuid():N}.pdf");
        try
        {
            var content = new PdfContentStreamBuilder().BeginText()
                .SetFont(PdfStandardFont.Helvetica, 12).MoveText(20, 100)
                .ShowLatin1Text("Hello").EndText();
            File.WriteAllBytes(path, new PdfDocumentBuilder().AddPage(200, 200, content).Build());
            var service = new TextRunService();

            var runs = Assert.IsType<PageTextRuns>(service.GetPage(path, 0));
            Assert.Equal("Hello", string.Concat(runs.Chars.Select(c => c.Value)));
            var line = Assert.Single(runs.Lines);
            Assert.True(TextRunService.IsOverText(runs,
                (line.Left + line.Right) / 2, (line.Top + line.Bottom) / 2));
            Assert.Same(runs, service.GetPage(path, 0));
            Assert.Null(service.GetPage(path, 1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BlockedTextDependencyDisablesSelectionAndCachesTheFailure()
    {
        var context = new BlockedPdfPigContext();
        try
        {
            var assembly = context.LoadFromAssemblyPath(typeof(TextRunService).Assembly.Location);
            var serviceType = assembly.GetType("KillerPDF.Services.TextRunService", throwOnError: true)!;
            var service = Activator.CreateInstance(serviceType, nonPublic: true)!;
            var getPage = serviceType.GetMethod("GetPage")!;
            string path = Path.Combine(Path.GetTempPath(), "blocked-text-dependency.pdf");

            Assert.Null(getPage.Invoke(service, [path, 0]));
            Assert.True(context.BlockedLoads > 0);
            int blockedLoads = context.BlockedLoads;
            Assert.Null(getPage.Invoke(service, [path, 0]));
            Assert.Equal(blockedLoads, context.BlockedLoads);
        }
        finally
        {
            context.Unload();
        }
    }

    private sealed class BlockedPdfPigContext() : AssemblyLoadContext(isCollectible: true)
    {
        public int BlockedLoads { get; private set; }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "UglyToad.PdfPig")
            {
                BlockedLoads++;
                throw new FileLoadException("An Application Control policy has blocked this file.",
                    assemblyName.Name);
            }

            return null;
        }
    }

    [Theory]
    [InlineData("English text", false)]
    [InlineData("متن فارسی", true)]
    [InlineData("نص عربي", true)]
    [InlineData("טקסט עברי", true)]
    [InlineData("1234", false)]
    public void DetectsLineDirection(string text, bool expected)
        => Assert.Equal(expected, TextRunService.IsRightToLeftText([text]));

    [Fact]
    public void RightToLeftCaretMovesFromRightEdgeToLeftEdge()
    {
        var runs = new PageTextRuns();
        runs.Chars.Add(new RunChar("א", 90, 100, 0, 0));
        runs.Chars.Add(new RunChar("ב", 80, 90, 0, 0));
        runs.Chars.Add(new RunChar("ג", 70, 80, 0, 0));
        runs.Lines.Add(new RunLine
        {
            Start = 0,
            Count = 3,
            Top = 20,
            Bottom = 10,
            Left = 70,
            Right = 100,
            RightToLeft = true,
        });

        Assert.Equal(0, TextRunService.CaretFromPoint(runs, 101, 15));
        Assert.Equal(1, TextRunService.CaretFromPoint(runs, 89, 15));
        Assert.Equal(3, TextRunService.CaretFromPoint(runs, 69, 15));
    }

    [Fact]
    public void CaretUsesHorizontalPositionWhenColumnsShareTheSameLineHeight()
    {
        var runs = new PageTextRuns();
        runs.Chars.Add(new RunChar("L", 10, 20, 0, 0));
        runs.Chars.Add(new RunChar("R", 110, 120, 1, 1));
        runs.Lines.Add(new RunLine
        {
            Start = 0, Count = 1, Top = 20, Bottom = 10,
            Left = 10, Right = 20,
        });
        runs.Lines.Add(new RunLine
        {
            Start = 1, Count = 1, Top = 20, Bottom = 10,
            Left = 110, Right = 120,
        });

        Assert.Equal(0, TextRunService.CaretFromPoint(runs, 11, 15));
        Assert.Equal(1, TextRunService.CaretFromPoint(runs, 111, 15));
        Assert.Equal(2, TextRunService.CaretFromPoint(runs, 121, 15));
    }
}

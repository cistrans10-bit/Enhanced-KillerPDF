using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests
{
    public class SearchServiceTests
    {
        private readonly SearchService _svc = new();

        [Fact]
        public void Search_EmptyQuery_ReturnsEmpty()
        {
            var result = SearchService.Search("irrelevant.pdf", "");
            Assert.Empty(result.ResultPages);
            Assert.Equal(0, result.TotalHits);
        }

        [Fact]
        public void Search_WhitespaceQuery_ReturnsEmpty()
        {
            var result = SearchService.Search("irrelevant.pdf", "   ");
            Assert.Empty(result.ResultPages);
            Assert.Equal(0, result.TotalHits);
        }

        [Fact]
        public void Search_EmptyFilePath_ReturnsEmpty()
        {
            var result = SearchService.Search("", "hello");
            Assert.Empty(result.ResultPages);
            Assert.Equal(0, result.TotalHits);
        }

        [Fact]
        public void Search_MissingFile_ReturnsEmpty()
        {
            // Should not throw; non-existent file produces no results.
            var result = SearchService.Search(@"C:\does\not\exist.pdf", "hello");
            Assert.Empty(result.ResultPages);
            Assert.Equal(0, result.TotalHits);
        }

        [Fact]
        public void SearchResult_PageRects_EmptyByDefault()
        {
            var result = new SearchResult();
            Assert.Empty(result.PageRects);
            Assert.Empty(result.ResultPages);
            Assert.Equal(0, result.TotalHits);
        }
    }
}

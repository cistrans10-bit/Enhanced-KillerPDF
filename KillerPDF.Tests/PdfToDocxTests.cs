using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests
{
    /// <summary>PDF → DOCX (PDF + DOCX mode): one section per page at the page's own size, the
    /// text kept with its spaces, wide gaps as tab stops, the footer band pinned in frames and
    /// vector drawing as shapes.</summary>
    public class PdfToDocxTests
    {
        [Fact]
        public void EveryPageBecomesOneSectionOfTheSameSize()
        {
            string dir = Directory.CreateTempSubdirectory("pdf2docx").FullName;
            string pdf = Path.Combine(dir, "in.pdf"), docx = Path.Combine(dir, "out.docx");
            File.WriteAllBytes(pdf, TwoPagePdf());

            int pages = PdfToDocx.Convert(pdf, docx);

            Assert.Equal(2, pages);
            string xml = DocumentXml(docx);
            var sizes = Regex.Matches(xml, "<w:pgSz w:w=\"(\\d+)\" w:h=\"(\\d+)\"").Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
            Assert.Equal([("11906", "16838"), ("12240", "15840")], sizes);                 // A4, then Letter (twips)
            Assert.Single(Regex.Matches(xml, "w:type w:val=\"nextPage\""));                 // one break between two pages
            Assert.EndsWith("</w:sectPr></w:body></w:document>", xml);                         // the last section closes the body
        }

        [Fact]
        public void TextKeepsItsWordsColumnsAndFooter()
        {
            string dir = Directory.CreateTempSubdirectory("pdf2docx").FullName;
            string pdf = Path.Combine(dir, "in.pdf"), docx = Path.Combine(dir, "out.docx");
            File.WriteAllBytes(pdf, TwoPagePdf());
            PdfToDocx.Convert(pdf, docx);
            string xml = DocumentXml(docx);
            string text = string.Concat(Regex.Matches(xml, "<w:t xml:space=\"preserve\">([^<]*)</w:t>").Select(m => m.Groups[1].Value + "|"));

            Assert.Contains("Quarterly report|", text);                  // spaces between words survive
            Assert.Contains("Key|", text);                               // a table row: two cells…
            Assert.Contains("Action|", text);
            Assert.Matches("<w:tabs><w:tab w:val=\"left\" w:pos=\"\\d+\"/></w:tabs>", xml);   // …a tab stop between them
            Assert.Contains("<w:tab/>", xml);
            Assert.Matches("<w:framePr [^>]*w:hAnchor=\"page\" w:vAnchor=\"page\"", xml);     // the footer is pinned
            Assert.Contains("<wps:wsp>", xml);                            // the filled bar is a shape
            Assert.Contains("<a:srgbClr val=\"1A8C4C\"/>", xml);
        }

        private static string DocumentXml(string docx)
        {
            using var zip = ZipFile.OpenRead(docx);
            Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
            Assert.NotNull(zip.GetEntry("word/styles.xml"));
            var entry = zip.GetEntry("word/document.xml");
            Assert.NotNull(entry);
            using var r = new StreamReader(entry!.Open());
            return r.ReadToEnd();
        }

        /// <summary>A4 page: a heading, a two-column row, a green bar, a footer. Letter page: one line.</summary>
        private static byte[] TwoPagePdf()
        {
            string page1 = "0.102 0.549 0.298 rg 72 700 451 6 re f\n" +
                           "BT /F1 18 Tf 72 740 Td (Quarterly report) Tj ET\n" +
                           "BT /F1 11 Tf 72 660 Td (Key) Tj ET\n" +
                           "BT /F1 11 Tf 300 660 Td (Action) Tj ET\n" +
                           "BT /F1 11 Tf 72 620 Td (Plain body text on the first page.) Tj ET\n" +
                           "BT /F1 8 Tf 72 30 Td (Footer line 01) Tj ET\n";
            string page2 = "BT /F1 12 Tf 72 700 Td (Second page) Tj ET\n";
            var objs = new[]
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.3 841.9] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 7 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                $"<< /Length {page1.Length} >>\nstream\n{page1}endstream",
                $"<< /Length {page2.Length} >>\nstream\n{page2}endstream",
            };
            var sb = new StringBuilder("%PDF-1.4\n");
            var offsets = new int[objs.Length];
            for (int i = 0; i < objs.Length; i++)
            {
                offsets[i] = sb.Length;
                sb.Append(i + 1).Append(" 0 obj\n").Append(objs[i]).Append("\nendobj\n");
            }
            int xref = sb.Length;
            sb.Append("xref\n0 ").Append(objs.Length + 1).Append("\n0000000000 65535 f \n");
            foreach (int o in offsets) sb.Append(o.ToString("D10")).Append(" 00000 n \n");
            sb.Append("trailer\n<< /Size ").Append(objs.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}

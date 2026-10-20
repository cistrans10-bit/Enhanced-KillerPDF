using System;
using System.IO;
using System.Linq;
using KillerPDF.Services;
using Xunit;

namespace KillerPDF.Tests;

// The rule this file exists to enforce: OCR LANGUAGES TRACK INTERFACE LANGUAGES.
// If KillerPDF's UI is offered in a language, OCR is offered in it too. There is no such thing as
// an interface language whose text the app cannot read.
//
// Nothing enforced it before, so it drifted: the UI shipped hu-HU while the catalog stayed at
// eleven entries, and killerpdf.net went on telling people OCR covered ten languages and that
// Polish and Hungarian "do not require OCR models".
//
// These read the real Strings folder instead of a second hardcoded list, so adding a locale fails
// the build until its model is registered. release.ps1 runs the suite, so it cannot ship broken.
public sealed class OcrCatalogTests
{
    // The test binary sits under KillerPDF.Tests\bin\<cfg>; the repo root is the ancestor that
    // holds Strings\. Walking up beats a pile of ..\..\.. that breaks whenever the layout moves.
    private static string StringsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Strings")))
            dir = dir.Parent;
        Assert.True(dir != null, "could not locate the repo's Strings folder from " + AppContext.BaseDirectory);
        return Path.Combine(dir!.FullName, "Strings");
    }

    private static string[] ShippedLocales() =>
        [.. Directory.GetFiles(StringsDir(), "*.xaml")
                 .Select(f => Path.GetFileNameWithoutExtension(f)!)
                 .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];

    [Fact]
    public void TheStringsFolderIsActuallyFound()
    {
        // Guards the guard: if the walk-up ever failed silently, every other test here would
        // compare against an empty list and pass while checking nothing.
        Assert.NotEmpty(ShippedLocales());
    }

    [Fact]
    public void EveryShippedLocaleHasAnOcrModel()
    {
        var mapped = OcrCatalog.LocaleToCode.Select(x => x.Locale).ToArray();
        var missing = ShippedLocales()
            .Where(l => !mapped.Contains(l, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(missing.Length == 0,
            "These locales ship an interface but have no OCR model in OcrCatalog.LocaleToCode: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void NoModelIsRegisteredForALocaleThatIsNotShipped()
    {
        var locales = ShippedLocales();
        var stale = OcrCatalog.LocaleToCode
            .Where(x => !locales.Contains(x.Locale, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Locale + " -> " + x.Code)
            .ToArray();

        Assert.True(stale.Length == 0,
            "These OCR entries point at locales no longer in Strings\\: " + string.Join(", ", stale));
    }

    [Fact]
    public void CatalogAndLocaleMapAgree()
    {
        var catalog = OcrCatalog.Languages.Select(x => x.Code).OrderBy(c => c, StringComparer.Ordinal).ToArray();
        var mapped = OcrCatalog.LocaleToCode.Select(x => x.Code).OrderBy(c => c, StringComparer.Ordinal).ToArray();
        Assert.Equal(mapped, catalog);
    }

    [Fact]
    public void CatalogHasNoDuplicatesAndNoBlanks()
    {
        var codes = OcrCatalog.Languages.Select(x => x.Code).ToArray();
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(OcrCatalog.Languages, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Code));
            Assert.False(string.IsNullOrWhiteSpace(e.Name));
        });
    }

    [Fact]
    public void OcrLanguageCountEqualsInterfaceLanguageCount()
    {
        // The number killerpdf.net quotes in prose. Pinning it here is what stops the site and
        // the app from disagreeing again.
        Assert.Equal(ShippedLocales().Length, OcrCatalog.Languages.Length);
    }

    [Theory]
    [InlineData("hu-HU", "hun")]
    [InlineData("pl-PL", "pol")]
    public void TheOnesThatWereMissingAreRegistered(string locale, string code)
    {
        Assert.Contains(OcrCatalog.LocaleToCode, x =>
            string.Equals(x.Locale, locale, StringComparison.OrdinalIgnoreCase) && x.Code == code);
        Assert.Contains(OcrCatalog.Languages, x => x.Code == code);
    }
}

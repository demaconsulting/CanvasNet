using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.PlantUml;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.CanvasNet.Viewer.Imaging;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Loading;
using DemaConsulting.HelpCards.Testing;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Unit tests for the CanvasNetHelp unit: the tag vocabulary, the tokens, the library source and catalog, and the
///     strict rules of CanvasNet that the validator of DemaConsulting.HelpCards applies to every library card.
/// </summary>
public partial class CanvasNetHelpTests
{
    /// <summary>
    ///     The validation options of the library cards: CanvasNet's budgets (6,000 characters a card, 3,500 a root with
    ///     cards below it, summaries of 100 characters and 60 for a root), lines of at most 80 columns outside a fenced
    ///     code block and 120 inside one, every warning an error, and the rule against command-line text.
    /// </summary>
    internal static readonly HelpValidationOptions LibraryValidation = new()
    {
        MaxCardChars = 6000,
        MaxRootIndexChars = 3500,
        MaxSummaryChars = 100,
        MaxRootSummaryChars = 60,
        MaxLineLength = 80,
        MaxFenceLineLength = 120,
        WarningsAreErrors = true,
        Rules = [new NoCommandLineTextRule()]
    };

    /// <summary>
    ///     Test that the tag lists are the closed vocabulary: the format tags, then the scope tags, each once.
    /// </summary>
    [Fact]
    public void HelpTags_All_IsTheFormatTagsThenTheScopeTags()
    {
        // Act
        var all = HelpTags.All;

        // Assert
        Assert.Equal(HelpTags.FormatTags.Concat(HelpTags.ScopeTags), all);
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(HelpTags.VegaLite, HelpTags.FormatTags);
        Assert.Contains(HelpTags.MathMl, HelpTags.FormatTags);
        Assert.Contains(HelpTags.FileAccess, HelpTags.ScopeTags);
    }

    /// <summary>
    ///     Test that the library tokens are the live constants of the code, 36 of them, formatted with the invariant
    ///     culture, and that the tool's token is not among them.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_Tokens_AreTheCodeConstants()
    {
        // Act
        var tokens = CanvasNetHelp.Tokens;

        // Assert
        static string Text(IFormattable value) => value.ToString(null, CultureInfo.InvariantCulture);
        Assert.Equal(36, tokens.Count);
        Assert.Equal(Text(Surface.MaxDimension), tokens["maxDimension"]);
        Assert.Equal(Text(ScadLimits.Default.MaxTriangles), tokens["scadMaxTriangles"]);
        Assert.Equal(Text(SourceLoader.MaxDpi), tokens["maxDpi"]);
        Assert.Equal(Text(ImageWriter.DefaultQuality), tokens["qualityDefault"]);
        Assert.Equal(Text(VegaLiteLimits.Default.MaxPaintedPixels), tokens["vlMaxPaintedPixels"]);
        Assert.Equal(Text(MermaidLimits.Default.MaxEdges), tokens["mmdMaxEdges"]);
        Assert.Equal(Text(PlantUmlLimits.Default.MaxIncludes), tokens["pumlMaxIncludes"]);
        Assert.Equal(Text(MathMlLimits.Default.MaxElements), tokens["mmlMaxElements"]);
        Assert.Equal(Text(MathMlLimits.Default.MaxTokenLength), tokens["mmlMaxTokenLength"]);
        Assert.Equal(Text(MathMlLimits.Default.MaxLayoutWork), tokens["mmlMaxLayoutWork"]);
        Assert.Equal(Text(MathMlLimits.Default.MaxPaintedGlyphs), tokens["mmlMaxPaintedGlyphs"]);
        Assert.Equal(Text(Positions.PositionCollector.MaxPositions), tokens["positionsMax"]);
        Assert.False(tokens.ContainsKey("schemaVersion"));
    }

    /// <summary>
    ///     Test that the library source is named and holds the embedded cards: a catalog built from it alone has the
    ///     library roots and a card with its example file.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_Source_LoadsTheEmbeddedCardsAndExamples()
    {
        // Act: a catalog of the source alone
        var catalog = HelpCatalog.Create(builder => builder.AddSource(CanvasNetHelp.Source));

        // Assert: the source name, the roots, and an example read from its file next to the card
        Assert.Equal(CanvasNetHelp.SourceName, Assert.Single(catalog.Sources).Name);
        Assert.Equal(CanvasNetHelp.RootIds, catalog.Roots.Select(c => c.Id));
        var ohm = Assert.Single(catalog.Find("mathml/basics")!.Examples, e => e.Name == "ohm");
        Assert.Equal("ohm.mml", ohm.FileName);
        Assert.StartsWith("<math", ohm.Content, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the library catalog is built once, the same instance on every thread.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_Catalog_IsThreadSafe_AndReturnsTheSameInstance()
    {
        // Act: read the catalog from several threads at once
        var catalogs = new HelpCatalog[8];
        Parallel.For(0, catalogs.Length, i => catalogs[i] = CanvasNetHelp.Catalog);

        // Assert
        Assert.All(catalogs, c => Assert.Same(CanvasNetHelp.Catalog, c));
    }

    /// <summary>
    ///     Test that the library catalog has the library roots in order, every card tagged with a scope tag, each format
    ///     tree tagged with its format alone, and no command-line example.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_Catalog_HasTheLibraryRootsTagsAndNoCommandExamples()
    {
        // Act
        var catalog = CanvasNetHelp.Catalog;

        // Assert: the roots, and every format tag names a root
        Assert.Equal(CanvasNetHelp.RootIds, catalog.Roots.Select(c => c.Id));
        Assert.All(HelpTags.FormatTags, tag => Assert.NotNull(catalog.Find(tag)));

        // Assert: tags and examples of every card
        foreach (var card in catalog.Cards)
        {
            Assert.Equal(CanvasNetHelp.SourceName, card.SourceName);
            Assert.Contains(card.Tags, HelpTags.ScopeTags.Contains);
            var root = card.Id.Split('/')[0];
            if (HelpTags.FormatTags.Contains(root))
            {
                Assert.Equal([root], card.Tags.Where(HelpTags.FormatTags.Contains));
            }

            Assert.DoesNotContain(card.Examples, e => e.Kind == HelpExample.CommandKind);
        }
    }

    /// <summary>
    ///     Test that the configuration of the library cards applies the closed tag vocabulary: a host card with a tag
    ///     outside it, or without a scope tag, fails the catalog with the rule of HelpCards.
    /// </summary>
    /// <param name="tags">The tags of the host card, comma separated.</param>
    /// <param name="ruleId">The rule identifier expected.</param>
    [Theory]
    [InlineData("svg,nonsense", "tag-unknown")]
    [InlineData("svg", "tag-required")]
    public void CanvasNetHelp_Configure_AppliesTheTagVocabulary(string tags, string ruleId)
    {
        // Arrange: a host source with one card
        var tagLines = string.Concat(tags.Split(',').Select(t => $"  - {t}\n"));
        var host = HelpSource.FromFiles(
            new Dictionary<string, string>
            {
                ["host.md"] = $"---\ntitle: Host\nsummary: A host card.\ntags:\n{tagLines}---\n\nHost text."
            },
            "host");

        // Act
        var exception = Assert.Throws<HelpCatalogException>(() =>
            HelpCatalog.Create(builder => CanvasNetHelp.Configure(builder).AddSource(host)));

        // Assert
        Assert.Contains(exception.Problems, p => p.RuleId == ruleId && p.File == "host.md");
    }

    /// <summary>
    ///     Test that a filter of the library catalog with a tag that is not in the vocabulary is refused.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_Catalog_Filter_UnknownTag_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => CanvasNetHelp.Catalog.Filter(["nonsense"]));
    }

    /// <summary>
    ///     Test that every library card passes the validator of DemaConsulting.HelpCards with CanvasNet's strict options:
    ///     the size and summary budgets, 80 columns outside a fenced code block and 120 inside one, reserved and
    ///     top-level headings, fence languages, unused tokens, no command-line text, and no warning at all.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_LibraryCards_PassTheStrictValidation()
    {
        // Act
        var problems = HelpCardValidator.Validate(
            builder => CanvasNetHelp.Configure(builder).ExpectedRoots(CanvasNetHelp.RootIds),
            LibraryValidation);

        // Assert
        Assert.True(problems.Count == 0, string.Join("\n", problems.Select(p => $"{p.RuleId} {p.File}:{p.Line} {p.Message}")));
    }
}

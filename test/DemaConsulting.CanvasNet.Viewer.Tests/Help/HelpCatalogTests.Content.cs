using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Pdf;
using DemaConsulting.CanvasNet.PlantUml;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.CanvasNet.Viewer.Imaging;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Unit tests for the content of the library catalog, <see cref="HelpCatalog.Default"/>: the tree and its budget,
///     the consistency of the cards with the code, the libraries, and the user guides, and the examples, which render
///     through <see cref="CanvasNetViewer"/>.
/// </summary>
public partial class HelpCatalogTests
{
    /// <summary>
    ///     The most characters the text of a library card may have.
    /// </summary>
    private const int CardBudget = 6000;

    /// <summary>
    ///     The most characters the text of a library root card that has cards below it may have.
    /// </summary>
    private const int RootIndexBudget = 3500;

    /// <summary>
    ///     The placeholder name of the identifier naming rule, which is a description and not an identifier.
    /// </summary>
    private const string NamingRule = "vl-<area>-<detail>";

    /// <summary>
    ///     The prefix of the logical names of the embedded card files of the library.
    /// </summary>
    private const string CardResourcePrefix = "DemaConsulting.CanvasNet.Viewer.Help/";

    /// <summary>
    ///     The prefix of the logical names of the embedded example files of the library.
    /// </summary>
    private const string ExampleResourcePrefix = "DemaConsulting.CanvasNet.Viewer.Help.Examples.";

    /// <summary>
    ///     The error codes only the tool reports, which the tool's command-line additions of <c>errors/codes</c> list.
    /// </summary>
    private static readonly string[] ToolOnlyErrorCodes = [ErrorCodes.OperationFailed, ErrorCodes.ThresholdFailed, ErrorCodes.PandocFilterError];

    /// <summary>
    ///     Matches the diagnostic identifiers a card may cite.
    /// </summary>
    private static readonly Regex IdentifierPattern =
        new(@"\b(?:scad|vl|mermaid|plantuml|pptx|vsdx|pdf|svg|image-fragment|image-page|image-access)-[A-Za-z0-9<>-]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Matches a file name extension (lower case, with the dot, and the two-part <c>.vl.json</c>) in a sentence.
    /// </summary>
    private static readonly Regex ExtensionToken =
        new(@"(?<=^|[\s(,])\.[a-z0-9]+(?:\.json)?(?=$|[\s),;.])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Matches a definition item of a guide: a bullet that starts with a bold term, and the rest of its first line.
    /// </summary>
    private static readonly Regex DefinitionItem = new(@"^- \*\*(?<term>.+?)\*\*(?<rest>.*)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Gets the source examples of the library as theory data: the card identifier and the example name.
    /// </summary>
    public static TheoryData<string, string> SourceExampleData =>
        [.. HelpCardText.ExamplesOfKind(HelpExample.SourceKind).Select(e => (e.Card.Id, e.Example.Name))];

    /// <summary>
    ///     Gets the snippet examples of the library as theory data: the card identifier and the example name.
    /// </summary>
    public static TheoryData<string, string> SnippetData =>
        [.. HelpCardText.ExamplesOfKind(HelpExample.SnippetKind).Select(e => (e.Card.Id, e.Example.Name))];

    /// <summary>
    ///     Gets the identifiers of every library card as theory data.
    /// </summary>
    public static TheoryData<string> LibraryCardIdData => [.. HelpCatalog.Default.Cards.Select(c => c.Id)];

    /// <summary>
    ///     Test that every library card loads with a title, a summary, and a body, that the roots are the library roots in
    ///     order, that the catalog is depth first, and that the tree of every drawing format has an example (the examples of the
    ///     other roots are command lines, which the tool adds).
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_EveryCardLoads_WithTitleSummaryAndBody()
    {
        // Arrange / Act: the library catalog
        var cards = HelpCatalog.Default.Cards;

        // Assert: the roots are the library roots, in order, and each card is followed by the cards below it
        Assert.Equal(HelpCatalog.LibraryRootOrder, cards.Where(c => c.ParentId == null).Select(c => c.Id));
        Assert.Equal(HelpCatalog.LibraryRootOrder, HelpCatalog.Default.Roots.Select(c => c.Id));
        for (var i = 1; i < cards.Count; i++)
        {
            var parent = cards[i].ParentId;
            Assert.True(parent == null || cards.Take(i).Any(c => c.Id == parent), $"Card '{cards[i].Id}' comes before its parent.");
        }

        Assert.All(cards, card =>
        {
            Assert.False(string.IsNullOrWhiteSpace(card.Title));
            Assert.False(string.IsNullOrWhiteSpace(card.Summary));
            Assert.False(string.IsNullOrWhiteSpace(card.Markdown));
        });
        Assert.All(DrawingFormats, root => Assert.NotEmpty(HelpCardText.Subtree(HelpCardText.Card(root)).SelectMany(c => c.Examples)));
    }

    /// <summary>
    ///     Test that every alias of a library root card resolves to it, ignoring case, and that an unknown name resolves
    ///     to nothing.
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_Find_EveryLibraryAlias_ResolvesIgnoringCase()
    {
        // Arrange: the documented aliases of the library roots
        var aliases = new Dictionary<string, string>
        {
            ["openscad"] = "scad",
            ["vegalite"] = "vega-lite",
            ["vl"] = "vega-lite",
            ["mmd"] = "mermaid",
            ["puml"] = "plantuml",
            ["powerpoint"] = "pptx",
            ["visio"] = "vsdx",
            ["png"] = "raster",
            ["jpeg"] = "raster",
            ["jpg"] = "raster",
            ["bmp"] = "raster",
            ["tiff"] = "raster",
            ["tif"] = "raster",
            ["gif"] = "raster",
            ["jpeg2000"] = "raster",
            ["jp2"] = "raster",
            ["file-access"] = "files",
            ["links"] = "files",
            ["position"] = "positions"
        };
        var catalog = HelpCatalog.Default;

        // Act / Assert: each alias, in lower and upper case, and each canonical name resolves to its card
        foreach (var (alias, name) in aliases)
        {
            Assert.Equal(name, catalog.Find(alias)?.Id);
            Assert.Equal(name, catalog.Find(alias.ToUpperInvariant())?.Id);
        }

        Assert.All(HelpCatalog.LibraryRootOrder, name => Assert.Equal(name, catalog.Find(name)?.Id));
        Assert.Equal(aliases.Count, catalog.Roots.Sum(t => t.Aliases.Count));
        Assert.Null(catalog.Find("nothing"));
        Assert.Null(catalog.Find("gantt"));
        Assert.Null(catalog.Find("json"));
    }

    /// <summary>
    ///     Test that no library text contains an unresolved token and that the numbers of the limits card are the live
    ///     constants.
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_Text_HasNoUnresolvedTokens_AndQuotesTheLiveLimits()
    {
        // Arrange / Act: every string of every card
        var texts = HelpCatalog.Default.Cards.SelectMany(AllStrings).ToList();
        var limits = HelpCardText.TextOf("limits");

        // Assert: no token is left, and the limits card quotes the live values
        Assert.DoesNotContain(texts, t => t.Contains("{{", StringComparison.Ordinal));
        Assert.Contains($"At most {VegaLiteLimits.Default.MaxPaintedPixels} device pixels painted", limits);
        Assert.Contains($"{Surface.MaxDimension} pixels", limits);
        Assert.Contains($"Default {ScadLimits.Default.MaxTriangles} triangles", limits);
        Assert.Contains($"At most {VegaLiteLimits.Default.MaxDataFiles} files", limits);
        Assert.Contains($"At most {MermaidLimits.Default.MaxNodes} nodes and {MermaidLimits.Default.MaxEdges} edges", limits);
        Assert.Contains($"At most {MermaidLimits.Default.MaxTextSize} characters", limits);
        Assert.Contains($"At most {MermaidLimits.Default.MaxImages} picture loads", limits);
        Assert.Contains($"At most {PlantUmlLimits.Default.MaxNodes} nodes (participants or activity nodes)", limits);
        Assert.Contains($"At most {PlantUmlLimits.Default.MaxTextSize} characters of statements", limits);
        Assert.Contains($"At most {PlantUmlLimits.Default.MaxIncludes} !include directives", limits);
        Assert.Contains($"{ImageWriter.MinQuality} to {ImageWriter.MaxQuality}, default {ImageWriter.DefaultQuality}", limits);
    }

    /// <summary>
    ///     Test that every child and every See also entry of every library card names a library card, that every card is
    ///     reachable from a root, and that no card links to itself or twice to one card.
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_Tree_ChildrenAndSeeAlsoResolve()
    {
        // Arrange: every identifier, and the cards reachable from the roots
        var catalog = HelpCatalog.Default;
        var ids = catalog.Cards.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        void Visit(HelpCard card)
        {
            Assert.True(reachable.Add(card.Id), $"Card '{card.Id}' is reached twice.");
            foreach (var child in catalog.GetChildren(card))
            {
                Assert.Equal(card.Id, child.ParentId);
                Visit(child);
            }
        }

        // Act: walk the tree from the roots
        foreach (var root in catalog.Roots)
        {
            Visit(root);
        }

        // Assert: every card is reachable, and every link names another card once
        Assert.Equal(ids.Order(StringComparer.Ordinal), reachable.Order(StringComparer.Ordinal));
        foreach (var card in catalog.Cards)
        {
            Assert.All(card.ChildIds, child => Assert.Contains(child, ids));
            Assert.All(card.SeeAlsoIds, target => Assert.Contains(target, ids));
            Assert.DoesNotContain(card.Id, card.SeeAlsoIds);
            Assert.Equal(card.SeeAlsoIds.Count, card.SeeAlsoIds.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    ///     Test that the embedded card files of the Viewer assembly are exactly the library cards, whatever path
    ///     separator the build put in the logical names.
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_Resources_AreExactlyTheLibraryCards()
    {
        // Arrange: the embedded card files, by their path
        var files = typeof(HelpCatalog).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(CardResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[CardResourcePrefix.Length..].Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act: the file of each card
        var cards = HelpCatalog.Default.Cards.Select(c => c.Id + HelpCatalog.CardExtension).Order(StringComparer.Ordinal);

        // Assert: the same set
        Assert.Equal(cards, files);
    }

    /// <summary>
    ///     Test that the names and aliases of the cards below each library card are unique, ignoring case, so every path
    ///     selects exactly one card.
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_ChildNames_AreUniqueAmongSiblings()
    {
        // Act / Assert: for each card, the names and aliases of its children are unique
        foreach (var card in HelpCatalog.Default.Cards)
        {
            var names = HelpCatalog.Default.GetChildren(card).SelectMany(c => c.Aliases.Prepend(c.Name)).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    /// <summary>
    ///     Test that every library card fits the size budget: at most 6,000 characters of text, at most 3,500 for a root
    ///     card that has cards below it, and a one-line summary of at most 60 characters for a root and 100 for any other
    ///     card.
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_Cards_FitTheSizeBudget()
    {
        // Act: the size of the text of every card
        var catalog = HelpCatalog.Default;
        var oversized = catalog.Cards
            .Select(c => (Card: c, Length: catalog.RenderText(c).Length))
            .Where(x => x.Length > (x.Card.ParentId == null && x.Card.ChildIds.Count > 0 ? RootIndexBudget : CardBudget))
            .Select(x => $"{x.Card.Id}: {x.Length}")
            .ToList();

        // Assert: nothing is over budget, and the summaries are short single lines
        Assert.True(oversized.Count == 0, "Over budget: " + string.Join(", ", oversized));
        Assert.All(catalog.Cards, card =>
        {
            Assert.DoesNotContain('\n', card.Summary);
            Assert.True(card.Summary.Length <= (card.ParentId == null ? 60 : 100), $"The summary of '{card.Id}' is too long.");
        });
    }

    /// <summary>
    ///     Test that no library card body uses a top-level heading (the title is printed as one) or a heading of the
    ///     generated footer, and that every fenced code block names its language and is closed.
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_Bodies_DoNotUseReservedHeadingsOrTopLevelHeadings()
    {
        // Act / Assert: the rules hold for every body
        foreach (var card in HelpCatalog.Default.Cards)
        {
            var fences = 0;
            foreach (var line in card.Markdown.Split('\n'))
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    Assert.True(fences % 2 == 1 || line.Length > 3, $"A code block of '{card.Id}' names no language.");
                    fences++;
                    continue;
                }

                if (fences % 2 == 1)
                {
                    continue;
                }

                Assert.False(line.StartsWith("# ", StringComparison.Ordinal), $"'{card.Id}' has a top-level heading.");
                Assert.DoesNotContain(line, HelpCatalog.FooterHeadings);
            }

            Assert.True(fences % 2 == 0, $"A code block of '{card.Id}' is not closed.");
        }
    }

    /// <summary>
    ///     Test that the text of a library card is deterministic and plain, starts with the title, prints the body as
    ///     written, keeps its lines short, and lists the cards below it, its See also cards, and its examples; and that
    ///     its JSON is one line with the card's data.
    /// </summary>
    /// <param name="id">The card identifier.</param>
    [Theory]
    [MemberData(nameof(LibraryCardIdData))]
    public void HelpCatalog_Default_RenderTextAndJson_AreDeterministicPlainAndComplete(string id)
    {
        // Arrange
        var catalog = HelpCatalog.Default;
        var card = HelpCardText.Card(id);

        // Act: render the card twice, and as JSON
        var first = catalog.RenderText(card);
        var second = catalog.RenderText(card);
        var json = catalog.RenderJson(card);

        // Assert: identical, without escape sequences, carriage returns, or a trailing newline, and the body as written
        Assert.Equal(first, second);
        Assert.DoesNotContain('\u001b', first);
        Assert.DoesNotContain('\r', first);
        Assert.False(first.EndsWith('\n'));
        Assert.StartsWith($"# {card.Title}\n", first);
        Assert.Contains($"\n{card.Markdown}\n", first + "\n");

        // Assert: lines are at most 80 columns, 120 inside a fenced code block
        var inFence = false;
        foreach (var line in first.Split('\n'))
        {
            inFence ^= line.StartsWith("```", StringComparison.Ordinal);
            Assert.True(line.Length <= (inFence ? 120 : 80), $"Line too long: {line}");
        }

        // Assert: the footer lists the children, the See also cards, and the examples
        Assert.All(card.ChildIds, child => Assert.Contains($"\n- {child}\n", first));
        Assert.All(card.SeeAlsoIds, target => Assert.Contains($"\n- {target}\n", first));
        Assert.All(card.Examples, example => Assert.Contains($"\n- {example.Name} [{example.Kind}]", first));

        // Assert: the JSON is one line with the card's data
        Assert.DoesNotContain('\n', json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(card.Id, document.RootElement.GetProperty("id").GetString());
        Assert.Equal(card.Markdown, document.RootElement.GetProperty("body").GetString());
        Assert.Equal(card.Tags, document.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(card.Examples.Select(e => e.Content), document.RootElement.GetProperty("examples").EnumerateArray().Select(e => e.GetProperty("content").GetString()));
    }

    /// <summary>
    ///     Test that the tree of every format card has the sections an agent needs (Supported, Unsupported, Options,
    ///     Diagnostics, and Known differences, each with bullets), and that the tree of every drawing format has at least two
    ///     examples (the examples of a document or raster format are command lines, which the tool adds).
    /// </summary>
    [Fact]
    public void HelpCatalog_FormatCards_SubtreesHaveSupportedUnsupportedOptionsDiagnosticsDifferencesAndExamples()
    {
        // Act / Assert: each format tree has the five headings, filled, and two examples
        foreach (var name in HelpTags.FormatTags)
        {
            var cards = HelpCardText.Subtree(HelpCardText.Card(name)).ToList();
            foreach (var heading in new[] { "Supported", "Unsupported", "Options", "Diagnostics", "Known differences" })
            {
                Assert.True(
                    cards.Any(c => HelpCardText.Bullets(c.Id, heading).Count > 0),
                    $"The cards of '{name}' lack a filled section '## {heading}'.");
            }

            Assert.True(
                !DrawingFormats.Contains(name) || cards.SelectMany(c => c.Examples).Count() >= 2,
                $"The cards of '{name}' have fewer than two examples.");
        }
    }

    /// <summary>
    ///     Test that the library examples are sources or snippets with non-empty content, the file extension their format
    ///     needs, and names unique in each root tree, and that every embedded example file is used by exactly one example.
    /// </summary>
    [Fact]
    public void HelpCatalog_Default_Examples_NamesUniqueKindsValidAndSourcesNonEmpty()
    {
        // Arrange: the extension each root's examples must have, and the embedded example files
        var extensions = new Dictionary<string, string>
        {
            ["scad"] = ".scad",
            ["vega-lite"] = ".vl.json",
            ["mermaid"] = ".mmd",
            ["plantuml"] = ".puml",
            ["svg"] = ".svg"
        };
        var files = typeof(HelpCatalog).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ExampleResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[ExampleResourcePrefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act / Assert: the names are unique in each root tree
        foreach (var root in HelpCatalog.Default.Roots)
        {
            var names = HelpCardText.Subtree(root).SelectMany(c => c.Examples).Select(e => e.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        // Act / Assert: the rules hold for every example
        foreach (var (card, example) in HelpCardText.AllExamples())
        {
            Assert.False(string.IsNullOrWhiteSpace(example.Content));
            Assert.DoesNotContain('\r', example.Content);
            Assert.False(example.Content.EndsWith('\n'));
            Assert.True(example.Kind is HelpExample.SourceKind or HelpExample.SnippetKind, $"'{card.Id}' has the example kind '{example.Kind}'.");
            Assert.NotNull(example.FileName);
            if (extensions.TryGetValue(HelpCardText.RootOf(card), out var extension))
            {
                Assert.EndsWith(extension, example.FileName);
            }
        }

        // Assert: every example file belongs to exactly one example, and each drawing format has a source example
        var used = HelpCardText.AllExamples()
            .Select(e => $"{HelpCardText.RootOf(e.Card)}-{e.Example.FileName}")
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(files, used);
        Assert.All(extensions.Keys, root => Assert.Contains(HelpCardText.ExamplesOfKind(HelpExample.SourceKind), e => HelpCardText.RootOf(e.Card) == root));
    }

    /// <summary>
    ///     Test that every source example (SCAD, Vega-Lite, Mermaid, PlantUML, SVG) renders through
    ///     <see cref="CanvasNetViewer.Render(string, ViewerRenderOptions?)"/> in strict mode and reports no diagnostic.
    /// </summary>
    /// <param name="cardId">The card of the example.</param>
    /// <param name="exampleName">The name of the example.</param>
    [Theory]
    [MemberData(nameof(SourceExampleData))]
    public void HelpCatalog_SourceExamples_RenderStrictWithoutDiagnostics(string cardId, string exampleName)
    {
        // Arrange: the example saved under its file name; a model is drawn small to stay fast
        var card = HelpCardText.Card(cardId);
        var example = Assert.Single(card.Examples, e => e.Name == exampleName);
        using var directory = new TempDirectory();
        var input = directory.Combine($"{HelpCardText.RootOf(card)}-{example.FileName}");
        File.WriteAllText(input, example.Content);
        var size = HelpCardText.RootOf(card) == "scad" ? 64 : (int?)null;

        // Act: render in strict mode
        using var image = CanvasNetViewer.Render(input, new ViewerRenderOptions { ThrowOnUnsupportedFeature = true, Width = size, Height = size });

        // Assert: it drew a picture without a diagnostic
        Assert.Empty(image.Diagnostics);
        Assert.True(image.Surface.Width > 0 && image.Surface.Height > 0);
    }

    /// <summary>
    ///     Test that each snippet, which is not rendered alone because it refers to sibling files, is well formed, links
    ///     only supported file types, and renders in strict mode without a diagnostic once its sibling files exist (the
    ///     source example with the same file name, else the first source example of that format, else the fixture
    ///     document for a PDF, PPTX, or VSDX file), embedding each link once, in order.
    /// </summary>
    /// <param name="cardId">The card of the snippet.</param>
    /// <param name="snippetName">The name of the snippet.</param>
    [Theory]
    [MemberData(nameof(SnippetData))]
    public void HelpCatalog_Snippets_RenderStrictWithTheirSiblingFiles(string cardId, string snippetName)
    {
        // Arrange: the snippet and its links (a link may carry a fragment)
        var snippet = Assert.Single(HelpCardText.Card(cardId).Examples, e => e.Name == snippetName);
        var links = SnippetLinks(snippet);
        Assert.NotEmpty(links);

        // Arrange: save the snippet and, for each distinct file, a sibling file
        using var directory = new TempDirectory();
        var input = directory.Combine(snippet.FileName!);
        File.WriteAllText(input, snippet.Content);
        foreach (var file in links.Select(href => href.Split('#')[0]).Distinct(StringComparer.Ordinal))
        {
            Assert.Contains(CanvasNetViewer.Extensions, e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase));
            WriteSibling(directory, file);
        }

        // Act: load in strict mode
        using var loaded = SourceLoader.Load(input, new SourceOptions { Strict = true });

        // Assert: no diagnostic, and one embed operation for each link, in order
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal(links, EmbeddedHrefs(loaded));
    }

    /// <summary>
    ///     Test that the <c>pictures</c> snippet of the <c>mermaid/composition</c> card renders in strict mode without a
    ///     diagnostic once its model and chart exist, with one <c>embed</c> operation for each picture.
    /// </summary>
    [Fact]
    public void HelpCatalog_MermaidComposition_PicturesSnippet_RendersStrictWithItsSiblingFiles()
    {
        // Arrange: the snippet, and the model and the chart it shows
        var snippet = Assert.Single(HelpCardText.Card("mermaid/composition").Examples, e => e.Name == "pictures");
        Assert.Equal(HelpExample.SnippetKind, snippet.Kind);
        using var directory = new TempDirectory();
        var input = directory.Combine(snippet.FileName!);
        File.WriteAllText(input, snippet.Content);
        HelpCardText.SaveExample(directory, "scad", "plate", "part.scad");
        HelpCardText.SaveExample(directory, "vega-lite", "bar", "chart.vl.json");

        // Act: load in strict mode
        using var loaded = SourceLoader.Load(input, new SourceOptions { Strict = true });

        // Assert: no diagnostic, and one embed operation for each picture
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal(["part.scad", "chart.vl.json"], EmbeddedHrefs(loaded));
    }

    /// <summary>
    ///     Test that the file types the <c>svg/embedding</c> card lists for <c>&lt;image href&gt;</c> are exactly the
    ///     extensions the sources of <see cref="CanvasNetViewer"/> claim (both directions), and that a plain <c>.json</c>
    ///     file is not linkable.
    /// </summary>
    [Fact]
    public void HelpCatalog_SvgEmbedding_HrefFileTypes_MatchTheViewerExtensions()
    {
        // Arrange: the extensions the card lists
        var text = HelpCardText.Item("svg/embedding", "href file types").Replace("`", string.Empty, StringComparison.Ordinal);

        // Act: the extensions listed, and the ones the viewer supports
        var listed = ExtensionToken.Matches(text).Select(m => m.Value).ToList();
        var supported = CanvasNetViewer.Extensions;
        var registered = CanvasNetViewer.ImageSources.SelectMany(s => s.Extensions).ToList();

        // Assert: the registered sources are the extensions, and a plain .json is not linkable
        Assert.Equal(supported.Order(StringComparer.Ordinal), registered.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(".json", supported);
        Assert.Contains(".vl.json", supported);

        // Assert: nothing is listed twice, nothing is listed that is unsupported, and nothing supported is missing
        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(listed.Except(supported, StringComparer.Ordinal));
        Assert.Empty(supported.Except(listed, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that the <c>svg/embedding</c> card states the size rule, the link rule (with the file-access policy), the
    ///     diagnostic codes, strict mode, and the example for a linked file.
    /// </summary>
    [Fact]
    public void HelpCatalog_SvgEmbedding_ImageLinks_StateSizeLinkRuleDiagnosticsAndExample()
    {
        // Arrange / Act: the card, its items, and its text
        var items = HelpCardText.Items("svg/embedding");
        var text = HelpCardText.TextOf("svg/embedding");

        // Assert: the rules, the codes, and the example are there
        Assert.Contains("size of the image element", text);
        Assert.Contains("relative to the folder of the SVG file", text);
        Assert.Contains("network URL", text);
        Assert.Contains("absolute path", text);
        Assert.Contains("strict mode", text);
        Assert.Contains("file-access policy", text);
        Assert.Contains(items, i => i.Term == "svg-image-unresolved");
        Assert.Contains(items, i => i.Term == "svg-image-too-large");
        Assert.Contains(items, i => i.Term == "link rule");
        Assert.Contains(items, i => i.Term == FolderImageResolver.AccessDeniedCode);
        Assert.Contains("<image href=\"part.scad\" x=\"10\" y=\"10\" width=\"120\" height=\"120\"/>", text);
        Assert.Contains("See a whole diagram in the example `embed` of the card `svg/embedding`.", text);
        Assert.Contains("<image href=\"part.scad\"", HelpCardText.Example("svg", "embed"));
    }

    /// <summary>
    ///     Test that the <c>svg</c> cards document the page fragment: the grammar, the parameter of each format, the four
    ///     diagnostic codes, the strict-mode behavior, the <c>&amp;amp;</c> note, the example, and that the limits quoted
    ///     are the live ones.
    /// </summary>
    [Fact]
    public void HelpCatalog_SvgFragments_PageFragment_DocumentsGrammarPerFormatCodesAndStrict()
    {
        // Arrange: the fragment items, the diagnostics, and the strict-mode option
        var fragment = HelpCardText.Item("svg/fragments", "page fragment");
        var example = HelpCardText.Item("svg/fragments", "page example");
        var diagnostics = HelpCardText.Items("svg/diagnostics");
        var strict = HelpCardText.Item("svg/options", "ThrowOnUnsupportedFeature");

        // Assert: grammar and per-format parameters
        Assert.Contains("name=value", fragment);
        Assert.Contains("page=N", fragment);
        Assert.Contains("slide=N", fragment);
        Assert.Contains("view=", fragment);
        Assert.Contains("azimuth=", fragment);
        Assert.Contains("elevation=", fragment);
        Assert.Contains("projection=", fragment);
        Assert.Contains("counts from 1", fragment);
        Assert.Contains(ImageFragment.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), fragment);
        Assert.Contains(ImageFragment.MaxParameters.ToString(System.Globalization.CultureInfo.InvariantCulture), fragment);

        // Assert: examples, and the &amp; note
        Assert.Contains("report.pdf#page=2", example);
        Assert.Contains("deck.pptx#slide=3", example);
        Assert.Contains("&amp;", example);
        Assert.Contains("See a whole diagram in the example `embed-pages` of the card `svg/fragments`.", example);
        Assert.Contains("<image href=\"report.pdf#page=2\"", HelpCardText.Example("svg", "embed-pages"));

        // Assert: each code is documented, and strict mode names the two that fail
        foreach (var code in new[] { FolderImageResolver.FragmentUnknownParameterCode, FolderImageResolver.FragmentDuplicateParameterCode, FolderImageResolver.FragmentInvalidCode, FolderImageResolver.PageOutOfRangeCode })
        {
            Assert.Contains(diagnostics, i => i.Term == code);
        }

        Assert.Contains(FolderImageResolver.FragmentInvalidCode, strict);
        Assert.Contains(FolderImageResolver.PageOutOfRangeCode, strict);
        Assert.Contains("stay warnings", strict);
    }

    /// <summary>
    ///     Test that the format cards show the embedding with their own fragment parameter and that the <c>limits</c>
    ///     card documents the live fragment limits.
    /// </summary>
    [Fact]
    public void HelpCatalog_FormatAndLimitsCards_DocumentFragmentsAndTheirLimits()
    {
        // Arrange
        var limits = HelpCardText.TextOf("limits");

        // Assert: the document cards show the embedding with their own parameter
        Assert.Contains("report.pdf#page=3", HelpCardText.TextOf("pdf"));
        Assert.Contains("deck.pptx#slide=3", HelpCardText.TextOf("pptx"));
        Assert.Contains("drawing.vsdx#page=2", HelpCardText.TextOf("vsdx"));
        Assert.Contains("part.scad#view=front", HelpCardText.TextOf("scad"));

        // Assert: the limits card quotes the live fragment limits
        Assert.Contains(ImageFragment.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + " characters", limits);
        Assert.Contains(ImageFragment.MaxParameters.ToString(System.Globalization.CultureInfo.InvariantCulture) + " parameters", limits);
    }

    /// <summary>
    ///     Test that a reader can discover the <c>&lt;image href&gt;</c> linking from the <c>scad</c> and <c>vega-lite</c>
    ///     cards: both name the element and link the <c>svg</c> card.
    /// </summary>
    [Fact]
    public void HelpCatalog_ScadAndVegaLiteCards_PointToImageHrefLinking()
    {
        // Act / Assert: each names the element and the card that explains it
        foreach (var root in new[] { "scad", "vega-lite" })
        {
            var text = HelpCardText.TextOf(root);
            Assert.True(text.Contains("<image href", StringComparison.Ordinal), $"The {root} cards do not mention <image href>.");
            Assert.True(text.Contains("the card `svg`", StringComparison.Ordinal), $"The {root} cards do not link the card 'svg'.");
        }
    }

    /// <summary>
    ///     Test that the library <c>errors/codes</c> card lists every error code of the library (every code but the ones
    ///     only the tool reports), and nothing else as a code.
    /// </summary>
    [Fact]
    public void HelpCatalog_ErrorsCodes_ListsEveryLibraryErrorCode()
    {
        // Arrange: the codes of the code, and the terms of the card
        var codes = ErrorCodeValues().Except(ToolOnlyErrorCodes, StringComparer.Ordinal).ToList();

        // Act
        var terms = HelpCardText.Items("errors/codes").Select(i => i.Term).ToList();

        // Assert: the same set
        Assert.NotEmpty(codes);
        Assert.Equal(codes.Order(StringComparer.Ordinal), terms.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that every diagnostic identifier a library card cites exists: it is covered by the trigger table (which
    ///     the trigger test proves), is an error code, is a code constant of the resolver or the flattener, is a warning
    ///     code a render test triggers (<c>svg-image-unresolved</c> and <c>svg-image-too-large</c>), or is a literal in one
    ///     of the libraries.
    /// </summary>
    [Fact]
    public void HelpCatalog_CitedDiagnosticIds_ExistInTheLibraries()
    {
        // Arrange: what a cited identifier may be, and the text of the libraries to search for literals
        var allowed = Triggers.SelectMany(t => new[] { t.Cited, t.Code })
            .Concat(["scad-warning", "vega-lite-warning", "svg-image-unresolved", "svg-image-too-large", FolderImageResolver.FragmentUnknownParameterCode, FolderImageResolver.FragmentDuplicateParameterCode, FolderImageResolver.FragmentInvalidCode, FolderImageResolver.PageOutOfRangeCode, FolderImageResolver.AccessDeniedCode, SvgFlattener.ImageNotDrawnCode, NamingRule])
            .Concat(ErrorCodeValues())
            .ToHashSet(StringComparer.Ordinal);
        var literals = LibraryAssemblies.Select(a => File.ReadAllBytes(a.Location)).ToList();

        // Act: the identifiers the library cards cite
        var cited = HelpCatalog.Default.Cards.SelectMany(AllStrings).SelectMany(s => IdentifierPattern.Matches(s).Select(m => m.Value)).Distinct().Order(StringComparer.Ordinal).ToList();

        // Assert: each cited identifier is allowed, or its literal (the part before a '<' placeholder) is in a library
        Assert.NotEmpty(cited);
        foreach (var token in cited)
        {
            var literal = token.Contains('<') ? token[..token.IndexOf('<')] : token;
            var needle = Encoding.Unicode.GetBytes(literal);
            var found = allowed.Contains(token) || literals.Any(bytes => bytes.AsSpan().IndexOf(needle) >= 0);
            Assert.True(found, $"The identifier '{token}' is cited by a help card but is neither triggered by a test nor a literal of a library.");
        }
    }

    /// <summary>
    ///     Test that the Mermaid diagram cards say what the definition lists of the Mermaid user guide say, item for item
    ///     and in order: the <c>## Syntax</c>, <c>## Relations</c>, and <c>## Tags</c> bullets of <c>mermaid/&lt;type&gt;</c>
    ///     are the guide's syntax, relation, and tag items, the <c>## Settings</c> bullets of
    ///     <c>mermaid/&lt;type&gt;/settings</c> are the guide's setting items (<c>**key** (default value): meaning</c>), and
    ///     every item of the guide's code list (<c>**code**: kind: reported when</c>) is a bullet of
    ///     <c>mermaid/unsupported</c> or <c>mermaid/diagnostics</c>. Backticks are compared, and the first letter of each
    ///     part before a <c>: </c> is compared ignoring case.
    /// </summary>
    [Fact]
    public void HelpCatalog_MermaidCards_DiagramSyntaxItems_MatchUserGuide()
    {
        // Arrange: the guide and the diagrams
        var guide = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-mermaid.md"));
        (string Card, string Heading, int MinimumRows)[] diagrams =
        [
            ("class", "### Supported Class Syntax", 30),
            ("state", "### Supported State Syntax", 18),
            ("er", "### Supported Entity Relationship Syntax", 24),
            ("pie", "### Supported Pie Syntax", 12),
            ("gantt", "### Supported Gantt Syntax", 35),
            ("mindmap", "### Supported Mindmap Syntax", 16),
            ("packet", "### Supported Packet Syntax", 16),
            ("gitgraph", "### Supported Git Graph Syntax", 21),
        ];

        // Act: the bullets that the items of the guide's lists promise, compared with the cards
        var differences = new List<string>();
        foreach (var (card, heading, minimumRows) in diagrams)
        {
            var items = GuideItems(guide, heading).ToList();
            Assert.True(items.Count >= minimumRows, $"Only {items.Count} items were found in the {card} lists of the guide.");
            var id = $"mermaid/{card}";
            var sections = HelpCardText.Headings(HelpCardText.Card(id).Markdown).Where(h => h is "Syntax" or "Relations" or "Tags").ToList();
            var syntax = items.Where(i => !IsSettingItem(i)).Select(i => i.Term + i.Tail).ToList();
            var settings = items.Where(IsSettingItem).Select(i => i.Term + i.Tail).ToList();
            CompareInOrder($"{id} ## {string.Join(", ", sections)}", syntax, sections.SelectMany(s => HelpCardText.Bullets(id, s)).ToList(), differences);
            CompareInOrder($"{id}/settings ## Settings", settings, HelpCardText.Bullets($"{id}/settings", "Settings"), differences);
        }

        var codeItems = GuideItems(guide, "### Mermaid Unsupported Features and Strict Mode").ToList();
        CompareCodeItems(codeItems, "mermaid", differences);

        // Assert: the code list has its items, and nothing differs
        Assert.True(codeItems.Count >= 18, $"Only {codeItems.Count} items were found in the code list of the guide.");
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    /// <summary>
    ///     Test that the PlantUML diagram cards say what the lists of the PlantUML user guide say, item for item and in
    ///     order: the <c>## Supported</c> bullets of <c>plantuml/sequence</c>, <c>plantuml/activity</c>,
    ///     <c>plantuml/class</c>, <c>plantuml/object</c>, <c>plantuml/state</c>, <c>plantuml/usecase</c>,
    ///     <c>plantuml/component</c>, and <c>plantuml/deployment</c> are the items of the guide's
    ///     supported syntax lists of those types, and every item of the guide's code list
    ///     (<c>**code**: kind: reported when</c>) is a bullet of <c>plantuml/unsupported</c> or <c>plantuml/diagnostics</c>.
    /// </summary>
    [Fact]
    public void HelpCatalog_PlantUmlCards_DiagramSyntaxItems_MatchUserGuide()
    {
        // Arrange: the guide
        var guide = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-plant-uml.md"));
        (string Card, string Heading, int MinimumRows)[] diagrams =
        [
            ("sequence", "### Supported Sequence Syntax", 10),
            ("activity", "### Supported Activity Syntax", 10),
            ("class", "### Supported Class Syntax", 10),
            ("object", "### Supported Object Syntax", 4),
            ("state", "### Supported State Syntax", 8),
            ("usecase", "### Supported Use Case Syntax", 6),
            ("component", "### Supported Component Syntax", 6),
            ("deployment", "### Supported Deployment Syntax", 4),
        ];

        // Act: the bullets that the items of the guide's lists promise, compared with the cards
        var differences = new List<string>();
        foreach (var (card, heading, minimumRows) in diagrams)
        {
            var items = GuideItems(guide, heading).Select(i => i.Term + i.Tail).ToList();
            Assert.True(items.Count >= minimumRows, $"Only {items.Count} items were found in the {card} list of the guide.");
            CompareInOrder($"plantuml/{card} ## Supported", items, HelpCardText.Bullets($"plantuml/{card}", "Supported"), differences);
        }

        var codeItems = GuideItems(guide, "### PlantUML Unsupported Features and Strict Mode").ToList();
        CompareCodeItems(codeItems, "plantuml", differences);

        // Assert: the code list has an item for every code of the library but the generic warning, and nothing differs
        Assert.True(codeItems.Count >= 16, $"Only {codeItems.Count} items were found in the code list of the guide.");
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    /// <summary>
    ///     Finds the folder that contains the solution file by walking up from the test output folder.
    /// </summary>
    /// <returns>The repository root.</returns>
    private static string FindRepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "CanvasNet.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException(
            $"The repository root (the folder with CanvasNet.slnx) was not found above {AppContext.BaseDirectory}.");
    }

    /// <summary>
    ///     Checks that every item of a guide's code list (<c>**code**: kind: reported when</c>) is a bullet of the
    ///     <c>unsupported</c> card (for an Unsupported item) or the <c>diagnostics</c> card of a format.
    /// </summary>
    /// <param name="codeItems">The items of the code list.</param>
    /// <param name="root">The format root card.</param>
    /// <param name="differences">The list to add each difference to.</param>
    private static void CompareCodeItems(IReadOnlyList<(string Term, string Tail)> codeItems, string root, List<string> differences)
    {
        var unsupported = HelpCardText.Bullets($"{root}/unsupported").Select(CanonicalBullet).ToList();
        var diagnostics = HelpCardText.Bullets($"{root}/diagnostics").Select(CanonicalBullet).ToList();
        foreach (var (term, tail) in codeItems)
        {
            var parts = tail.Split(": ", 3);
            Assert.True(parts.Length == 3 && parts[0].Length == 0, $"The code item '{term}' is not written as '**code**: kind: reported when'.");
            var (bullets, expected) = parts[1] == "Unsupported"
                ? (unsupported, $"{term}: {parts[2]}")
                : (diagnostics, $"{term}: {parts[1]}: {parts[2]}");
            if (!bullets.Contains(CanonicalBullet(expected)))
            {
                differences.Add($"bullet missing:{Environment.NewLine}  guide: {expected}");
            }
        }
    }

    /// <summary>
    ///     Gets the definition items of one section of a guide, from its heading to the next heading of the same or a
    ///     higher level (headings inside code blocks do not count): every bullet that starts with a bold term
    ///     (<c>- **term**</c>), with its indented continuation lines joined by single spaces.
    /// </summary>
    /// <param name="lines">The lines of the guide.</param>
    /// <param name="heading">The heading line of the section.</param>
    /// <returns>The term (without its bold markers) and the tail of each item (what follows the term), in order.</returns>
    private static IEnumerable<(string Term, string Tail)> GuideItems(string[] lines, string heading)
    {
        var start = Array.IndexOf(lines, heading);
        Assert.True(start >= 0, $"The guide has no heading '{heading}'.");
        var level = heading.TakeWhile(c => c == '#').Count();
        var items = new List<(string Term, string Tail)>();
        StringBuilder? item = null;
        string? term = null;
        var fenced = false;
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
            }

            var headingLevel = line.TakeWhile(c => c == '#').Count();
            if (!fenced && headingLevel > 0 && headingLevel <= level && line.Length > headingLevel && line[headingLevel] == ' ')
            {
                break;
            }

            if (item != null && line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
            {
                item.Append(' ').Append(line.Trim());
                continue;
            }

            if (item != null)
            {
                items.Add((term!, item.ToString()));
                item = null;
            }

            var match = fenced ? Match.Empty : DefinitionItem.Match(line);
            if (match.Success)
            {
                term = match.Groups["term"].Value;
                item = new StringBuilder(match.Groups["rest"].Value);
            }
        }

        if (item != null)
        {
            items.Add((term!, item.ToString()));
        }

        return items;
    }

    /// <summary>
    ///     Gets whether a definition item of a guide is a setting, written <c>**key** (default value): meaning</c>.
    /// </summary>
    /// <param name="item">The term and the tail of the item.</param>
    /// <returns><c>true</c> for a setting.</returns>
    private static bool IsSettingItem((string Term, string Tail) item)
    {
        return item.Tail.StartsWith(" (default ", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Compares the bullets of a card with the bullets the guide promises, in order, and records each difference.
    /// </summary>
    /// <param name="where">The card and heading, for the message.</param>
    /// <param name="expected">The bullets the guide promises.</param>
    /// <param name="actual">The bullets of the card.</param>
    /// <param name="differences">The list to add each difference to.</param>
    private static void CompareInOrder(string where, IReadOnlyList<string> expected, IReadOnlyList<string> actual, List<string> differences)
    {
        for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
        {
            var guide = i < expected.Count ? expected[i] : "(nothing)";
            var help = i < actual.Count ? actual[i] : "(nothing)";
            if (CanonicalBullet(guide) != CanonicalBullet(help))
            {
                differences.Add($"{where} bullet {i + 1}:{Environment.NewLine}  guide: {guide}{Environment.NewLine}  help:  {help}");
            }
        }
    }

    /// <summary>
    ///     Gets the canonical form of a bullet for the guide comparison: the first letter of each of its first three parts
    ///     separated by <c>: </c> in lower case.
    /// </summary>
    /// <param name="bullet">The bullet.</param>
    /// <returns>The canonical form.</returns>
    private static string CanonicalBullet(string bullet)
    {
        var parts = bullet.Split(": ", 3);
        return string.Join(": ", parts.Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
    }

    /// <summary>
    ///     Gets the files a snippet links, in order: the <c>href</c> of each SVG <c>image</c>, or the <c>img</c> of each
    ///     Mermaid node and the <c>src</c> of each <c>&lt;img&gt;</c> of a Mermaid label.
    /// </summary>
    /// <param name="snippet">The snippet.</param>
    /// <returns>The links, with their fragments.</returns>
    private static List<string> SnippetLinks(HelpExample snippet)
    {
        if (snippet.FileName!.EndsWith(".svg", StringComparison.Ordinal))
        {
            return XDocument.Parse(snippet.Content).Descendants()
                .Where(e => e.Name.LocalName == "image")
                .Select(e => (string)e.Attribute("href")!)
                .ToList();
        }

        return Regex.Matches(snippet.Content, "(?:img: \"|<img src=\")([^\"]+)\"", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    /// <summary>
    ///     Writes the file a snippet links: the source example with that file name, else the first source example with
    ///     its extension, else the fixture document of a PDF, PPTX, or VSDX file.
    /// </summary>
    /// <param name="directory">The folder of the snippet.</param>
    /// <param name="file">The file name the snippet links.</param>
    private static void WriteSibling(TempDirectory directory, string file)
    {
        var documents = new Dictionary<string, Func<byte[]>>
        {
            [".pdf"] = () => FixtureFactory.CreatePdf(),
            [".pptx"] = () => FixtureFactory.CreatePptx(),
            [".vsdx"] = () => FixtureFactory.CreateVsdx()
        };
        var sources = HelpCardText.ExamplesOfKind(HelpExample.SourceKind).Select(e => e.Example).ToList();
        var source = sources.FirstOrDefault(e => e.FileName == file)
                     ?? sources.FirstOrDefault(e => file.EndsWith(e.FileName![e.FileName!.IndexOf('.')..], StringComparison.OrdinalIgnoreCase));
        if (source != null)
        {
            File.WriteAllText(directory.Combine(file), source.Content);
            return;
        }

        var fixture = documents.First(p => file.EndsWith(p.Key, StringComparison.OrdinalIgnoreCase));
        File.WriteAllBytes(directory.Combine(file), fixture.Value());
    }

    /// <summary>
    ///     Gets the links of the <c>embed</c> operations of a loaded source, in order.
    /// </summary>
    /// <param name="loaded">The loaded source.</param>
    /// <returns>The <c>href</c> of each <c>embed</c> operation.</returns>
    private static List<string> EmbeddedHrefs(LoadedSource loaded)
    {
        return [.. loaded.Operations.Where(o => o.Op == "embed").Select(o => (string)o.Fields.First(f => f.Key == "href").Value)];
    }

    /// <summary>
    ///     Gets the values of every error code constant.
    /// </summary>
    /// <returns>The code strings.</returns>
    private static IEnumerable<string> ErrorCodeValues()
    {
        return typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);
    }

    /// <summary>
    ///     Gets every string of a card that is shown to a reader.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The title, summary, body, and example descriptions (without the content of the examples).</returns>
    private static IEnumerable<string> AllStrings(HelpCard card)
    {
        yield return card.Title;
        yield return card.Summary;
        yield return card.Markdown;
        foreach (var example in card.Examples)
        {
            yield return example.Description;
        }
    }

    /// <summary>
    ///     Gets the library assemblies whose string literals may define a diagnostic identifier.
    /// </summary>
    private static IEnumerable<Assembly> LibraryAssemblies =>
    [
        typeof(ScadDocument).Assembly,
        typeof(VegaLiteDocument).Assembly,
        typeof(MermaidDocument).Assembly,
        typeof(PlantUmlDocument).Assembly,
        typeof(PdfDocument).Assembly,
        typeof(PptxDocument).Assembly,
        typeof(VsdxDocument).Assembly,
        typeof(SvgCodec).Assembly,
        typeof(Surface).Assembly
    ];
}

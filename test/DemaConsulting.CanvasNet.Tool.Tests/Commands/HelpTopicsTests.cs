using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Pdf;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the HelpTopics class: the embedded cards, their consistency with the code, the libraries, and
///     the user guides, the examples, and the text and JSON renderings.
/// </summary>
[Collection("Sequential")]
public partial class HelpTopicsTests
{
    /// <summary>
    ///     The placeholder name of the identifier naming rule, which is a description and not an identifier.
    /// </summary>
    private const string NamingRule = "vl-<area>-<detail>";

    /// <summary>
    ///     Matches the diagnostic identifiers a card may cite.
    /// </summary>
    private static readonly Regex IdentifierPattern =
        new(@"\b(?:scad|vl|mermaid|pptx|vsdx|pdf|svg|image-fragment|image-page|image-access)-[A-Za-z0-9<>-]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Matches a file name extension (lower case, with the dot, and the two-part <c>.vl.json</c>) in a sentence.
    /// </summary>
    private static readonly Regex ExtensionToken =
        new(@"(?<=^|[\s(,])\.[a-z0-9]+(?:\.json)?(?=$|[\s),;.])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Splits a markdown table row at the cell bars that are not escaped (<c>\|</c> is a bar inside a cell).
    /// </summary>
    private static readonly Regex TableCellSplit = new(@"(?<!\\)\|", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     The names of the global options, which every command accepts.
    /// </summary>
    private static readonly string[] GlobalOptionNames =
        ["--json", "--silent", "--log", "--validate", "--results", "--result", "--depth", "--version", "--help", "-v", "-?", "-h"];

    /// <summary>
    ///     Gets the source examples as theory data: the card identifier and the example name.
    /// </summary>
    public static IEnumerable<object[]> SourceExampleData =>
        HelpTopics.SourceExamples().Select(e => new object[] { e.Card.Id, e.Example.Name });

    /// <summary>
    ///     Gets the identifiers of every card as theory data.
    /// </summary>
    public static IEnumerable<object[]> CardIdData => HelpTopics.All.Select(c => new object[] { c.Id });

    /// <summary>
    ///     Gets the identifiers of the cards that have examples of their own as theory data.
    /// </summary>
    public static IEnumerable<object[]> CardWithExamplesIdData =>
        HelpTopics.All.Where(c => c.Examples.Count > 0).Select(c => new object[] { c.Id });

    /// <summary>
    ///     Test that every card loads with a title, a summary, and a body, that the roots are the topics in order, that
    ///     the catalog is depth first, and that every root tree has an example.
    /// </summary>
    [Fact]
    public void HelpTopics_All_EveryCardLoads_WithTitleSummaryAndBody()
    {
        // Arrange / Act: load the catalog
        var cards = HelpTopics.All;

        // Assert: the roots are the topics, in order, and each card is followed by the cards below it
        Assert.Equal(HelpTopics.TopicNames, cards.Where(c => c.ParentId == null).Select(c => c.Id));
        Assert.Equal(HelpTopics.TopicNames, HelpTopics.Roots.Select(c => c.Id));
        for (var i = 1; i < cards.Count; i++)
        {
            var parent = cards[i].ParentId;
            Assert.True(parent == null || cards.Take(i).Any(c => c.Id == parent), $"Card '{cards[i].Id}' comes before its parent.");
        }

        Assert.All(cards, card =>
        {
            Assert.False(string.IsNullOrWhiteSpace(card.Title));
            Assert.False(string.IsNullOrWhiteSpace(card.Summary));
            Assert.False(string.IsNullOrWhiteSpace(card.Body));
        });
        Assert.All(HelpTopics.Roots, root => Assert.NotEmpty(HelpTopics.SubtreeExamples(root)));
    }

    /// <summary>
    ///     Test that every alias of a root card resolves to it, ignoring case, and that an unknown name resolves to
    ///     nothing.
    /// </summary>
    [Fact]
    public void HelpTopics_Find_EveryAlias_ResolvesIgnoringCase()
    {
        // Arrange: the documented aliases
        var aliases = new Dictionary<string, string>
        {
            ["openscad"] = "scad",
            ["vegalite"] = "vega-lite",
            ["vl"] = "vega-lite",
            ["mmd"] = "mermaid",
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

        // Act / Assert: each alias, in lower and upper case, and each canonical name resolves to its card
        foreach (var (alias, name) in aliases)
        {
            Assert.Equal(name, HelpTopics.Find(alias)?.Id);
            Assert.Equal(name, HelpTopics.Find(alias.ToUpperInvariant())?.Id);
        }

        Assert.All(HelpTopics.TopicNames, name => Assert.Equal(name, HelpTopics.Find(name)?.Id));
        Assert.Equal(aliases.Count, HelpTopics.Roots.Sum(t => t.Aliases.Count));
        Assert.Null(HelpTopics.Find("nothing"));
        Assert.Null(HelpTopics.Find("gantt"));
    }

    /// <summary>
    ///     Test that no root name or alias collides with a command, its alias, the reserved help name, the topics
    ///     subject, or another root.
    /// </summary>
    [Fact]
    public void HelpTopics_Names_DoNotCollideWithCommandsHelpOrEachOther()
    {
        // Arrange: every name the command line already answers to
        var registry = CommandRegistry.CreateDefault();
        var taken = registry.Commands.SelectMany(c => c.Aliases.Prepend(c.Name)).Append("help").Append("topics").ToList();
        var topicNames = HelpTopics.Roots.SelectMany(t => t.Aliases.Prepend(t.Name)).ToList();

        // Act / Assert: the root names are unique and none is taken
        Assert.Equal(topicNames.Count, topicNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(topicNames.Intersect(taken, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Test that no text contains an unresolved token and that the numbers in the cards are the live constants.
    /// </summary>
    [Fact]
    public void HelpTopics_Text_HasNoUnresolvedTokens()
    {
        // Arrange / Act: every string of every card
        var texts = HelpTopics.All.SelectMany(AllStrings).ToList();
        var limits = HelpCardText.TextOf("limits");

        // Assert: no token is left, the table holds the live values, and the limits card quotes them
        Assert.DoesNotContain(texts, t => t.Contains("{{", StringComparison.Ordinal));
        Assert.Equal(Surface.MaxDimension.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["maxDimension"]);
        Assert.Equal(ScadLimits.Default.MaxTriangles.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["scadMaxTriangles"]);
        Assert.Equal(ArgumentReader.MaxDpi.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["maxDpi"]);
        Assert.Equal(ResultWriter.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["schemaVersion"]);
        Assert.Equal(VegaLiteLimits.Default.MaxPaintedPixels.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["vlMaxPaintedPixels"]);
        Assert.Contains($"At most {VegaLiteLimits.Default.MaxPaintedPixels} device pixels painted", limits);
        Assert.Contains($"{Surface.MaxDimension} pixels", limits);
        Assert.Contains($"Default {ScadLimits.Default.MaxTriangles} triangles", limits);
        Assert.Contains($"At most {VegaLiteLimits.Default.MaxDataFiles} files", limits);
        Assert.Equal(MermaidLimits.Default.MaxEdges.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["mmdMaxEdges"]);
        Assert.Contains($"At most {MermaidLimits.Default.MaxNodes} nodes and {MermaidLimits.Default.MaxEdges} edges", limits);
        Assert.Contains($"At most {MermaidLimits.Default.MaxTextSize} characters", limits);
        Assert.Contains($"At most {MermaidLimits.Default.MaxImages} picture loads", limits);
        Assert.Contains($"{ImageWriter.MinQuality} to {ImageWriter.MaxQuality}, default {ImageWriter.DefaultQuality}", limits);
    }

    /// <summary>
    ///     Test that the tree of every format topic has the sections an agent needs (Supported, Unsupported, Options,
    ///     Diagnostics, and Known differences, each with bullets) and at least two examples.
    /// </summary>
    [Fact]
    public void HelpTopics_FormatTopics_SubtreesHaveSupportedUnsupportedOptionsDiagnosticsDifferencesAndExamples()
    {
        // Arrange / Act / Assert: each format tree has the five headings, filled, and two examples
        foreach (var name in HelpTopics.FormatTopicNames)
        {
            var cards = HelpTopics.Subtree(HelpTopics.Get(name)).ToList();
            foreach (var heading in new[] { "Supported", "Unsupported", "Options", "Diagnostics", "Known differences" })
            {
                Assert.True(
                    cards.Any(c => HelpCardText.Bullets(c.Id, heading).Count > 0),
                    $"The cards of '{name}' lack a filled section '## {heading}'.");
            }

            Assert.True(HelpTopics.SubtreeExamples(HelpTopics.Get(name)).Count() >= 2, $"The cards of '{name}' have fewer than two examples.");
        }
    }

    /// <summary>
    ///     Test that the examples have valid kinds and unique file names, that every example file is used by exactly one
    ///     example, and that source examples have non-empty content with the file extension their format needs.
    /// </summary>
    [Fact]
    public void HelpTopics_Examples_NamesUniqueKindsValidAndSourcesNonEmpty()
    {
        // Arrange: the extension each root's source examples must have, and the embedded example files
        var extensions = new Dictionary<string, string>
        {
            ["scad"] = ".scad",
            ["vega-lite"] = ".vl.json",
            ["mermaid"] = ".mmd",
            ["svg"] = ".svg"
        };
        var files = typeof(HelpTopics).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(HelpTopics.ExampleResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[HelpTopics.ExampleResourcePrefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act / Assert: the names are unique in each root tree, and the rules hold for every example
        foreach (var root in HelpTopics.Roots)
        {
            var names = HelpTopics.SubtreeExamples(root).Select(e => e.Example.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        foreach (var (card, example) in HelpTopics.AllExamples())
        {
            Assert.False(string.IsNullOrWhiteSpace(example.Content));
            Assert.DoesNotContain('\r', example.Content);
            Assert.False(example.Content.EndsWith('\n'));
            if (example.Kind == HelpTopics.CommandKind)
            {
                Assert.StartsWith("canvasnet ", example.Content);
                Assert.Null(example.FileName);
                continue;
            }

            Assert.True(HelpTopics.SourceKind == example.Kind || HelpTopics.SnippetKind == example.Kind);
            Assert.NotNull(example.FileName);
            if (extensions.TryGetValue(card.RootId, out var extension))
            {
                Assert.EndsWith(extension, example.FileName);
            }
        }

        // Assert: every example file belongs to exactly one example, and each drawing format has a source example
        var used = HelpTopics.AllExamples().Where(e => e.Example.FileName != null)
            .Select(e => $"{e.Card.RootId}-{e.Example.FileName}")
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(files, used);
        Assert.All(extensions.Keys, root => Assert.Contains(HelpTopics.SourceExamples(), e => e.Card.RootId == root));
    }

    /// <summary>
    ///     Test that every source example (SCAD, Vega-Lite, Mermaid, SVG) renders through the real pipeline with
    ///     <c>--strict</c> and reports no diagnostic.
    /// </summary>
    /// <param name="cardId">The card of the example.</param>
    /// <param name="exampleName">The name of the example.</param>
    [Theory]
    [MemberData(nameof(SourceExampleData))]
    public void HelpTopics_SourceExamples_RenderStrictWithoutDiagnostics(string cardId, string exampleName)
    {
        // Arrange: the example saved under its file name; a model is drawn small to stay fast
        var (card, example) = HelpTopics.SourceExamples().First(e => e.Card.Id == cardId && e.Example.Name == exampleName);
        using var directory = new TempDirectory();
        var input = directory.Combine($"{card.RootId}-{example.FileName}");
        var output = directory.Combine("out.png");
        File.WriteAllText(input, example.Content);
        string[] size = card.RootId == "scad" ? ["--size", "64x64"] : [];

        // Act: render with --strict
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(["render", input, "--strict", "--json", "-o", output, .. size]);

        // Assert: it succeeded without a diagnostic and wrote the picture
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.True(File.Exists(output));
    }

    /// <summary>
    ///     Test that the file types the <c>svg embedding</c> card lists for <c>&lt;image href&gt;</c> are exactly the
    ///     extensions the sources of <c>CanvasNetViewer</c> claim (both directions), that a plain <c>.json</c> file is not
    ///     linkable, and that the <c>formats</c> command lists the same extensions.
    /// </summary>
    [Fact]
    public void HelpTopics_SvgTopic_ImageHrefFileTypes_MatchTheResolverExtensions()
    {
        // Arrange: the extensions the card lists, and the ones the resolver supports
        var text = HelpCardText.Item("svg/embedding", "href file types").Replace("`", string.Empty, StringComparison.Ordinal);
        var listed = ExtensionToken.Matches(text).Select(m => m.Value).ToList();
        var supported = CanvasNetViewer.Extensions;
        var registered = CanvasNetViewer.ImageSources.SelectMany(s => s.Extensions).ToList();
        var formatted = DiscoveryCommands.InputFormats.SelectMany(f => f.Extensions).ToList();

        // Assert: the registered sources of the viewer are the ones the tool wires in, and a plain .json is not linkable
        Assert.Equal(supported.Order(StringComparer.Ordinal), registered.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(".json", supported);
        Assert.Contains(".vl.json", supported);

        // Assert: nothing is listed twice, nothing is listed that is unsupported, and nothing supported is missing
        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(listed.Except(supported, StringComparer.Ordinal));
        Assert.Empty(supported.Except(listed, StringComparer.Ordinal));

        // Assert: the formats command lists the same extensions, so the two tables cannot drift apart
        Assert.Equal(formatted.Order(StringComparer.Ordinal), listed.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that the <c>svg embedding</c> card states the size rule, the link rule (with the file-access mode), the
    ///     diagnostic codes, and the example for a linked file.
    /// </summary>
    [Fact]
    public void HelpTopics_SvgTopic_ImageLinksSection_StatesSizeLinkRuleDiagnosticAndExample()
    {
        // Arrange: the card, its items, and its text
        var items = HelpCardText.Items("svg/embedding");
        var text = HelpCardText.TextOf("svg/embedding");

        // Assert: the rules, the code, and the example are there, and the example can be printed
        Assert.Contains("size of the image element", text);
        Assert.Contains("relative to the folder of the SVG file", text);
        Assert.Contains("network URL", text);
        Assert.Contains("absolute path", text);
        Assert.Contains("--strict", text);
        Assert.Contains(items, i => i.Term == "svg-image-unresolved");
        Assert.Contains(items, i => i.Term == "svg-image-too-large");
        Assert.Contains(items, i => i.Term == "link rule");
        Assert.Contains(items, i => i.Term == FolderImageResolver.AccessDeniedCode);
        Assert.Contains("--file-access", text);
        Assert.Contains("<image href=\"part.scad\" x=\"10\" y=\"10\" width=\"120\" height=\"120\"/>", text);
        Assert.Contains("canvasnet help svg --example embed", text);
        Assert.Contains("<image href=\"part.scad\"", HelpTopics.RenderExample(HelpTopics.Get("svg"), "embed"));
    }

    /// <summary>
    ///     Gets the card identifiers and names of every snippet example as theory data.
    /// </summary>
    public static IEnumerable<object[]> SnippetData =>
        HelpTopics.Snippets().Select(e => new object[] { e.Card.Id, e.Example.Name });

    /// <summary>
    ///     Test that each snippet, which the render drift test skips because it refers to sibling files, is well formed,
    ///     links only supported file types, and renders under <c>--strict</c> without a diagnostic once its sibling files
    ///     exist (the source example with the same file name, else the first source example of that format, else the
    ///     fixture document for a PDF, PPTX, or VSDX file), drawing one <c>embed</c> operation for each link, in order.
    /// </summary>
    /// <param name="cardId">The card of the snippet.</param>
    /// <param name="snippetName">The name of the snippet.</param>
    [Theory]
    [MemberData(nameof(SnippetData))]
    public void HelpTopics_Snippets_RenderStrictWithTheirSiblingFiles(string cardId, string snippetName)
    {
        // Arrange: the snippet, which is not a source example, and its links (a link may carry a fragment)
        var snippet = HelpTopics.Snippets().Single(e => e.Card.Id == cardId && e.Example.Name == snippetName).Example;
        Assert.DoesNotContain(HelpTopics.SourceExamples(), e => e.Example == snippet);
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

        // Act: render with --strict
        var output = directory.Combine("out.png");
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--strict", "--json", "-o", output);

        // Assert: it succeeded without a diagnostic, embedded each link once in order, and wrote the picture
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        var embeds = document.RootElement.GetProperty("operations").EnumerateArray()
            .Where(o => o.GetProperty("op").GetString() == "embed")
            .Select(o => o.GetProperty("href").GetString()!)
            .ToList();
        Assert.Equal(links, embeds);
        Assert.True(File.Exists(output));
    }

    /// <summary>
    ///     Test that the <c>pictures</c> snippet of the <c>mermaid composition</c> card renders under <c>--strict</c>
    ///     without a diagnostic once its model and chart exist, and draws one <c>embed</c> operation for each picture.
    /// </summary>
    [Fact]
    public void HelpTopics_MermaidTopic_PicturesSnippet_RendersStrictWithItsSiblingFiles()
    {
        // Arrange: the snippet, which is not a source example, and the model and the chart it shows
        var card = HelpTopics.Get("mermaid/composition");
        var snippet = Assert.Single(card.Examples, e => e.Name == "pictures");
        Assert.Equal(HelpTopics.SnippetKind, snippet.Kind);
        using var directory = new TempDirectory();
        var input = directory.Combine(snippet.FileName!);
        File.WriteAllText(input, snippet.Content);
        File.WriteAllText(directory.Combine("part.scad"), HelpTopics.RenderExample(HelpTopics.Get("scad"), "plate"));
        File.WriteAllText(directory.Combine("chart.vl.json"), HelpTopics.RenderExample(HelpTopics.Get("vega-lite"), "bar"));

        // Act: render with --strict
        var output = directory.Combine("out.png");
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--strict", "--json", "-o", output);

        // Assert: it succeeded without a diagnostic and listed each picture once
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        var embeds = document.RootElement.GetProperty("operations").EnumerateArray()
            .Where(o => o.GetProperty("op").GetString() == "embed")
            .Select(o => o.GetProperty("href").GetString())
            .ToList();
        Assert.Equal(["part.scad", "chart.vl.json"], embeds);
        Assert.True(File.Exists(output));
    }

    /// <summary>
    ///     Test that the Mermaid diagram cards say what the tables of the Mermaid user guide say, row for row and in
    ///     order: the <c>## Syntax</c>, <c>## Relations</c>, and <c>## Tags</c> bullets of <c>mermaid &lt;type&gt;</c> and the
    ///     <c>## Settings</c> bullets of <c>mermaid &lt;type&gt; settings</c> are the rows of the guide's syntax, relation,
    ///     tag, and settings tables, and every row of the guide's code table is a bullet of <c>mermaid unsupported</c> or
    ///     <c>mermaid diagnostics</c>. Backticks are compared, <c>\|</c> is read as <c>|</c>, a final period is added to a
    ///     row, and the first letter of each part before a <c>: </c> is compared ignoring case.
    /// </summary>
    [Fact]
    public void HelpTopics_MermaidTopic_DiagramSyntaxItems_MatchUserGuide()
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

        // Act: the bullets that the rows of the guide's tables promise, compared with the cards
        var differences = new List<string>();
        foreach (var (card, heading, minimumRows) in diagrams)
        {
            var rows = GuideTableRows(guide, heading).ToList();
            Assert.True(rows.Count >= minimumRows, $"Only {rows.Count} rows were found in the {card} tables of the guide.");
            var expected = rows.GroupBy(r => DiagramHeading(r.Header)).ToDictionary(g => g.Key, g => g.Select(ExpectedDiagramBullet).ToList());
            foreach (var (section, bullets) in expected)
            {
                var id = section == "Settings" ? $"mermaid/{card}/settings" : $"mermaid/{card}";
                CompareInOrder($"{id} ## {section}", bullets, HelpCardText.Bullets(id, section), differences);
            }
        }

        var codeRows = GuideTableRows(guide, "### Mermaid Unsupported Features and Strict Mode").ToList();
        var unsupported = HelpCardText.Bullets("mermaid/unsupported").Select(CanonicalBullet).ToList();
        var diagnostics = HelpCardText.Bullets("mermaid/diagnostics").Select(CanonicalBullet).ToList();
        foreach (var (header, cells) in codeRows)
        {
            Assert.Equal(["Code", "Kind", "Reported when"], header);
            var (bullets, expected) = cells[1] == "Unsupported"
                ? (unsupported, $"{cells[0]}: {cells[2]}.")
                : (diagnostics, $"{cells[0]}: {cells[1]}: {cells[2]}.");
            if (!bullets.Contains(CanonicalBullet(expected)))
            {
                differences.Add($"bullet missing:{Environment.NewLine}  guide: {expected}");
            }
        }

        // Assert: the code table has its rows, and nothing differs
        Assert.True(codeRows.Count >= 18, $"Only {codeRows.Count} rows were found in the code table of the guide.");
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    /// <summary>
    ///     Test that the <c>## Reference</c> bullets of the <c>svg flatten</c> card are, word for word and in order, the
    ///     rows of the flatten reference table of the tool's user guide.
    /// </summary>
    [Fact]
    public void HelpTopics_SvgTopic_FlattenItems_MatchUserGuide()
    {
        // Arrange: the guide and the card
        var guide = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-tool.md"));
        var bullets = HelpCardText.Bullets("svg/flatten", "Reference");

        // Act: the bullets the rows of the guide's table promise
        var rows = GuideTableRows(guide, "#### Flatten Reference").ToList();

        // Assert: the table has the documented header and the same bullets in the same order
        Assert.All(rows, row => Assert.Equal(["Term", "Meaning"], row.Header));
        Assert.True(rows.Count >= 7, $"Only {rows.Count} rows were found in the flatten reference table of the guide.");
        Assert.Equal(rows.Select(row => $"{row.Cells[0]}: {row.Cells[1]}"), bullets);
    }

    /// <summary>
    ///     Test that the bullets of the <c>files</c> card are, word for word and in order, the rows of the file access
    ///     reference table of the tool's user guide, and that each has a term.
    /// </summary>
    [Fact]
    public void HelpTopics_FilesTopic_Items_MatchUserGuide()
    {
        // Arrange: the guide and every bullet of the card, in order
        var guide = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-tool.md"));
        var bullets = HelpCardText.Bullets("files");

        // Act: the rows of the reference table
        var rows = GuideTableRows(guide, "#### File Access Reference").ToList();

        // Assert: every bullet has a term, the table has the documented header, and the bullets are the rows, in order
        Assert.Equal(bullets.Count, HelpCardText.Items("files").Count);
        Assert.All(rows, row => Assert.Equal(["Term", "Meaning"], row.Header));
        Assert.True(rows.Count >= 16, $"Only {rows.Count} rows were found in the file access reference table of the guide.");
        Assert.Equal(rows.Select(row => $"{row.Cells[0]}: {row.Cells[1]}"), bullets);
    }

    /// <summary>
    ///     Test that the <c>files</c> card documents the modes, the two options, what holds in every mode, the two
    ///     diagnostics with their live codes, and the variables of the Pandoc filter (which the filter reads), and that the
    ///     <c>svg</c> and <c>errors</c> cards point to it.
    /// </summary>
    [Fact]
    public void HelpTopics_FilesTopic_DocumentsModesOptionsDiagnosticsAndPandoc()
    {
        // Arrange: the card, its terms, the filter, and the cards that point to it
        var card = HelpTopics.Get("files");
        var terms = HelpCardText.Items("files").Select(i => i.Term).ToList();
        var filter = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "filters", "canvasnet.lua"));

        // Assert: the headings and the terms
        Assert.Equal(["Modes", "Options", "In every mode", "Diagnostics", "The Pandoc filter"], HelpCardText.Headings(card.Body));
        foreach (var term in new[] { "any", "roots", "no-symlinks", "roots,no-symlinks", "--file-access <mode>", "--allow-root <folder>", "inputs", "never read", "limits" })
        {
            Assert.Contains(term, terms);
        }

        Assert.Contains(FolderImageResolver.AccessDeniedCode, terms);
        Assert.Contains(ErrorCodes.FileAccessDenied, terms);

        // Assert: the variables of the filter are terms and are read by the filter
        foreach (var variable in new[] { "CANVASNET_FILE_ACCESS", "CANVASNET_ROOTS" })
        {
            Assert.Contains(variable, terms);
            Assert.Contains(variable, filter, StringComparison.Ordinal);
        }

        // Assert: the cards that describe a denial point here
        foreach (var name in new[] { "svg", "errors" })
        {
            Assert.Contains("canvasnet help files", HelpCardText.TextOf(name), StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Test that the <c>svg</c> cards document the page fragment: the grammar, the parameter of each format, the
    ///     four diagnostic codes with their severity, the <c>--strict</c> behavior, the <c>&amp;amp;</c> note, the
    ///     example, and that the limits quoted are the live ones.
    /// </summary>
    [Fact]
    public void HelpTopics_SvgTopic_PageFragment_DocumentsGrammarPerFormatCodesAndStrict()
    {
        // Arrange: the fragment items, the diagnostics, and the options
        var fragment = HelpCardText.Item("svg/fragments", "page fragment");
        var example = HelpCardText.Item("svg/fragments", "page example");
        var diagnostics = HelpCardText.Items("svg/diagnostics");
        var strict = HelpCardText.Item("svg/options", "--strict");

        // Assert: grammar and per-format parameters
        Assert.Contains("name=value", fragment);
        Assert.Contains("page=N", fragment);
        Assert.Contains("slide=N", fragment);
        Assert.Contains("view=", fragment);
        Assert.Contains("counts from 1", fragment);
        Assert.Contains(ImageFragment.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture), fragment);
        Assert.Contains(ImageFragment.MaxParameters.ToString(System.Globalization.CultureInfo.InvariantCulture), fragment);

        // Assert: examples, and the &amp; note
        Assert.Contains("report.pdf#page=2", example);
        Assert.Contains("deck.pptx#slide=3", example);
        Assert.Contains("&amp;", example);
        Assert.Contains("canvasnet help svg --example embed-pages", example);
        Assert.Contains("<image href=\"report.pdf#page=2\"", HelpTopics.RenderExample(HelpTopics.Get("svg"), "embed-pages"));

        // Assert: each code is documented, and --strict names the two that fail
        foreach (var code in new[] { FolderImageResolver.FragmentUnknownParameterCode, FolderImageResolver.FragmentDuplicateParameterCode, FolderImageResolver.FragmentInvalidCode, FolderImageResolver.PageOutOfRangeCode })
        {
            Assert.Contains(diagnostics, i => i.Term == code);
        }

        Assert.Contains(FolderImageResolver.FragmentInvalidCode, strict);
        Assert.Contains(FolderImageResolver.PageOutOfRangeCode, strict);
        Assert.Contains("stay warnings", strict);
    }

    /// <summary>
    ///     Test that the format cards point to the page fragment, the <c>scad</c> cards document <c>#view=</c>, the
    ///     <c>json diagnostics</c> card documents <c>detail</c>, <c>location</c>, and <c>origin</c>, and <c>limits</c>
    ///     documents the fragment limits.
    /// </summary>
    [Fact]
    public void HelpTopics_FormatTopicsJsonAndLimits_DocumentFragmentsDetailLocationAndOrigin()
    {
        // Assert: the document cards show the embedding with their own parameter
        Assert.Contains("report.pdf#page=3", HelpCardText.TextOf("pdf"));
        Assert.Contains("deck.pptx#slide=3", HelpCardText.TextOf("pptx"));
        Assert.Contains("drawing.vsdx#page=2", HelpCardText.TextOf("vsdx"));
        Assert.Contains("part.scad#view=front", HelpCardText.TextOf("scad"));

        // Assert: the json diagnostics card documents the three members, and the limits card the live fragment limits
        var json = HelpCardText.Items("json/diagnostics");
        foreach (var term in new[] { "detail", "location", "origin" })
        {
            Assert.Contains(json, i => i.Term == term);
        }

        var limits = HelpCardText.TextOf("limits");
        Assert.Contains(ImageFragment.MaxLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + " characters", limits);
        Assert.Contains(ImageFragment.MaxParameters.ToString(System.Globalization.CultureInfo.InvariantCulture) + " parameters", limits);
    }

    /// <summary>
    ///     Test that an agent can discover the <c>&lt;image href&gt;</c> linking from the <c>scad</c> and
    ///     <c>vega-lite</c> cards, the <c>formats</c> listing, and the <c>render</c> usage.
    /// </summary>
    [Fact]
    public void HelpTopics_ImageHrefLinking_IsCrossReferencedFromScadVegaLiteFormatsAndRender()
    {
        // Arrange: the places that point to the svg card
        var pointers = new Dictionary<string, string>
        {
            ["scad cards"] = HelpCardText.TextOf("scad"),
            ["vega-lite cards"] = HelpCardText.TextOf("vega-lite"),
            ["formats footer"] = DiscoveryCommands.Formats.HelpFooter,
            ["render usage"] = CommandRegistry.CreateDefault().Find("render")!.Usage
        };

        // Assert: each names the element and the card that explains it
        foreach (var (place, text) in pointers)
        {
            Assert.True(text.Contains("<image href", StringComparison.Ordinal), $"The {place} do not mention <image href>.");
            Assert.True(text.Contains("canvasnet help svg", StringComparison.Ordinal), $"The {place} do not point to 'canvasnet help svg'.");
        }
    }

    /// <summary>
    ///     Test that every command line of the cards (the command examples and every line of a fenced code block that
    ///     starts with <c>canvasnet</c>) names a known command and uses only options that command (or the tool) declares,
    ///     and that every <c>canvasnet help</c> line names a card, so a documented command line cannot be a mistake.
    /// </summary>
    [Fact]
    public void HelpTopics_CommandExamples_UseKnownCommandsAndOptions()
    {
        // Arrange: the default registry, and every command line of the cards
        var registry = CommandRegistry.CreateDefault();
        var lines = HelpTopics.AllExamples().Where(x => x.Example.Kind == HelpTopics.CommandKind)
            .Select(x => (Where: $"{x.Card.Id} --example {x.Example.Name}", Line: x.Example.Content))
            .Concat(HelpTopics.All.SelectMany(c => FencedCommandLines(c.Body).Select(l => (Where: c.Id, Line: l))))
            .ToList();
        Assert.NotEmpty(lines);

        // Act / Assert: check the words of every command line
        foreach (var (where, line) in lines)
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("canvasnet", words[0]);

            var known = new HashSet<string>(GlobalOptionNames, StringComparer.Ordinal);
            if (words[1] == "help")
            {
                known.UnionWith(["--example", "--examples"]);
                var path = words.Skip(2).TakeWhile(w => !w.StartsWith('-')).ToList();
                Assert.True(
                    path.Count == 0 || path[0] == "topics" || registry.Find(path[0]) != null || HelpTopics.Resolve(path) != null,
                    $"The command line '{line}' of '{where}' names no help card.");
            }
            else
            {
                var command = registry.Find(words[1]);
                Assert.True(command != null, $"The command line '{line}' of '{where}' uses the unknown command '{words[1]}'.");
                known.UnionWith(command.Options.SelectMany(o => o.Aliases.Prepend(o.Name)));
            }

            foreach (var option in words.Skip(2).Where(w => w.StartsWith('-') && w.Length > 1 && !char.IsAsciiDigit(w[1])))
            {
                var name = option.Split('=')[0];
                Assert.True(known.Contains(name), $"The command line '{line}' of '{where}' uses the unknown option '{name}'.");
            }
        }
    }

    /// <summary>
    ///     Test that every option an <c>## Options</c> section lists as a term is an option of <c>render</c> or a global
    ///     option.
    /// </summary>
    [Fact]
    public void HelpTopics_OptionTerms_ExistInTheRenderOptionTable()
    {
        // Arrange: every name the render command and the tool accept
        var known = RenderCommand.OptionTable.SelectMany(o => o.Aliases.Prepend(o.Name)).Concat(GlobalOptionNames).ToHashSet(StringComparer.Ordinal);

        // Act / Assert: the first word of each option term of each options section is one of them
        var checkedTerms = 0;
        foreach (var card in HelpTopics.All)
        {
            foreach (var (term, _) in HelpCardText.Items(card.Id, "Options").Where(i => i.Term.StartsWith('-')))
            {
                var name = term.Split(' ')[0];
                Assert.True(known.Contains(name), $"Card '{card.Id}' lists the unknown option '{name}'.");
                checkedTerms++;
            }
        }

        Assert.True(checkedTerms > 30, $"Only {checkedTerms} option terms were found.");
    }

    /// <summary>
    ///     Test that the <c>errors codes</c> card lists every error code of the tool, and nothing else as a code.
    /// </summary>
    [Fact]
    public void HelpTopics_ErrorsTopic_ListsEveryErrorCode()
    {
        // Arrange: the codes of the code, and the terms of the card
        var codes = ErrorCodeValues().ToList();
        var terms = HelpCardText.Items("errors/codes").Select(i => i.Term).ToList();

        // Act / Assert: the same set
        Assert.NotEmpty(codes);
        Assert.Equal(codes.Order(StringComparer.Ordinal), terms.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that every diagnostic identifier a card cites exists: it is covered by the trigger table (which
    ///     the trigger test proves), is an error code, is a warning code that a render test triggers
    ///     (<c>svg-image-unresolved</c> and <c>svg-image-too-large</c> by <c>RenderCommandSvgEmbedTests</c>, and
    ///     <c>svg-flatten-image-not-drawn</c> of the Viewer library by <c>FlattenCommandTests</c>), or is a literal in one of the libraries.
    /// </summary>
    [Fact]
    public void HelpTopics_CitedDiagnosticIds_ExistInTheLibraries()
    {
        // Arrange: what a cited identifier may be, and the text of the libraries to search for literals
        var allowed = Triggers.SelectMany(t => new[] { t.Cited, t.Code }).Concat(["scad-warning", "vega-lite-warning", "svg-image-unresolved", "svg-image-too-large", FolderImageResolver.FragmentUnknownParameterCode, FolderImageResolver.FragmentDuplicateParameterCode, FolderImageResolver.FragmentInvalidCode, FolderImageResolver.PageOutOfRangeCode, FolderImageResolver.AccessDeniedCode, SvgFlattener.ImageNotDrawnCode, NamingRule]).Concat(ErrorCodeValues()).ToHashSet(StringComparer.Ordinal);
        var literals = LibraryAssemblies.Select(a => File.ReadAllBytes(a.Location)).ToList();
        var cited = HelpTopics.All.SelectMany(AllStrings).SelectMany(s => IdentifierPattern.Matches(s).Select(m => m.Value)).Distinct().Order(StringComparer.Ordinal).ToList();

        // Act / Assert: each cited identifier is allowed, or its literal (the part before a '<' placeholder) is in a library
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
    ///     Test that the text of a card is deterministic and plain, starts with the title, prints the body as written,
    ///     keeps its lines short, and lists the cards below it, its See also links (each with the summary of its target),
    ///     and its examples.
    /// </summary>
    /// <param name="id">The card identifier.</param>
    [Theory]
    [MemberData(nameof(CardIdData))]
    public void HelpTopics_RenderText_IsDeterministicPlainAndWrapped(string id)
    {
        // Arrange / Act: render the card twice
        var card = HelpTopics.Get(id);
        var first = HelpTopics.RenderText(card);
        var second = HelpTopics.RenderText(card);

        // Assert: identical, without escape sequences, carriage returns, or a trailing newline, and the body as written
        Assert.Equal(first, second);
        Assert.DoesNotContain('\u001b', first);
        Assert.DoesNotContain('\r', first);
        Assert.False(first.EndsWith('\n'));
        Assert.StartsWith($"# {card.Title}\n", first);
        Assert.Contains($"\n{card.Body}\n", first + "\n");

        // Assert: lines are at most 80 columns, 120 inside a fenced code block
        var inFence = false;
        foreach (var line in first.Split('\n'))
        {
            inFence ^= line.StartsWith("```", StringComparison.Ordinal);
            Assert.True(line.Length <= (inFence ? 120 : 80), $"Line too long: {line}");
        }

        // Assert: the footer lists the children, the See also links with their summaries, and the examples
        var flat = HelpCardText.Flatten(first);
        Assert.All(HelpTopics.ChildrenOf(card), child => Assert.Contains($"- {child.Command} {child.Summary}", flat));
        Assert.All(card.SeeAlso.Select(HelpTopics.Get), target => Assert.Contains($"- {target.Command} {target.Summary}", flat));
        Assert.All(card.Examples, example => Assert.Contains($"  {example.Name}  ", first));
        if (card.Examples.Count > 0)
        {
            Assert.Contains($"Print them in full with: {card.Command} --examples,", flat);
            Assert.Contains($"{card.Command} --example <name>", flat);
        }

        if (HelpTopics.CardsBelowWithExamples(card).Any())
        {
            Assert.Contains($"Find the cards below that have examples with: {card.Command} --examples", flat);
        }
    }

    /// <summary>
    ///     Test that a word longer than the width is kept whole on a line of its own.
    /// </summary>
    [Fact]
    public void HelpTopics_AppendWrapped_LongWord_IsNotSplit()
    {
        // Arrange: a word longer than the text width between short words
        var longWord = new string('x', HelpTopics.TextWidth + 10);
        var builder = new StringBuilder();

        // Act: wrap with a hanging indent
        HelpTopics.AppendWrapped(builder, $"a {longWord} b", "- ", "  ");

        // Assert: the long word has its own line, indented, and nothing is lost
        var lines = builder.ToString().TrimEnd().Split(Environment.NewLine);
        Assert.Equal(["- a", $"  {longWord}", "  b"], lines);
    }

    /// <summary>
    ///     Test that the JSON of a card has the documented shape, keys, and order, on one line and in ASCII.
    /// </summary>
    /// <param name="id">The card identifier.</param>
    [Theory]
    [MemberData(nameof(CardIdData))]
    public void HelpTopics_RenderJson_HasTheDocumentedShape(string id)
    {
        // Arrange / Act: render and parse the document
        var card = HelpTopics.Get(id);
        var json = HelpTopics.RenderJson(card);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Assert: one ASCII line with the envelope and the keys of a card, in order
        Assert.DoesNotContain('\n', json);
        Assert.All(json, c => Assert.True(c < 0x80));
        Assert.Equal(
            ["schemaVersion", "ok", "command", "kind", "id", "aliases", "title", "summary", "body", "children", "seeAlso", "examples"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("help", root.GetProperty("command").GetString());
        Assert.Equal("card", root.GetProperty("kind").GetString());
        Assert.Equal(card.Id, root.GetProperty("id").GetString());
        Assert.Equal(card.Aliases, root.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()));
        Assert.Equal(card.Title, root.GetProperty("title").GetString());
        Assert.Equal(card.Summary, root.GetProperty("summary").GetString());
        Assert.Equal(card.Body, root.GetProperty("body").GetString());

        // Assert: the links carry the identifier, summary, and command of their targets
        void AssertLinks(string property, IEnumerable<HelpCard> targets) =>
            Assert.Equal(
                targets.Select(t => (t.Id, t.Summary, t.Command)),
                root.GetProperty(property).EnumerateArray().Select(l => (l.GetProperty("id").GetString()!, l.GetProperty("summary").GetString()!, l.GetProperty("command").GetString()!)));
        AssertLinks("children", HelpTopics.ChildrenOf(card));
        AssertLinks("seeAlso", card.SeeAlso.Select(HelpTopics.Get));

        // Assert: the examples carry their content
        var examples = root.GetProperty("examples").EnumerateArray().ToList();
        Assert.Equal(card.Examples.Select(e => e.Content), examples.Select(e => e.GetProperty("content").GetString()));
        Assert.All(examples, e =>
        {
            Assert.False(string.IsNullOrEmpty(e.GetProperty("name").GetString()));
            Assert.Contains(e.GetProperty("kind").GetString(), new[] { "source", "command", "snippet" });
            Assert.False(string.IsNullOrEmpty(e.GetProperty("language").GetString()));
            Assert.False(string.IsNullOrEmpty(e.GetProperty("description").GetString()));
        });
    }

    /// <summary>
    ///     Test that an unknown example name is a usage error that lists the example names of the card's tree.
    /// </summary>
    [Fact]
    public void HelpTopics_RenderExample_UnknownName_ThrowsUsageErrorListingNames()
    {
        // Arrange: the scad card
        var card = HelpTopics.Get("scad");

        // Act / Assert: the error names the card and the example and lists the valid names; JSON fails the same way
        var exception = Assert.Throws<ToolException>(() => HelpTopics.RenderExample(card, "nothing"));
        Assert.Equal(ErrorCodes.UsageError, exception.Code);
        Assert.Equal("Help card 'scad' has no example 'nothing'.", exception.Message);
        Assert.Contains("plate", exception.Hint);
        Assert.Equal("help", exception.Command);
        Assert.Throws<ToolException>(() => HelpTopics.RenderExampleJson(card, "nothing"));
    }

    /// <summary>
    ///     Test that a source example is returned exactly as embedded (LF line endings, no trailing newline), from the
    ///     root card and from the card it belongs to, and that the JSON of the example describes it.
    /// </summary>
    [Fact]
    public void HelpTopics_RenderExample_Source_ReturnsExactEmbeddedContent()
    {
        // Arrange: the plate example and the embedded resource it comes from
        var card = HelpTopics.Get("scad");
        using var stream = typeof(HelpTopics).Assembly.GetManifestResourceStream($"{HelpTopics.ExampleResourcePrefix}scad-plate.scad")!;
        using var reader = new StreamReader(stream);
        var expected = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

        // Act
        var content = HelpTopics.RenderExample(card, "PLATE");
        using var oneJson = JsonDocument.Parse(HelpTopics.RenderExampleJson(card, "plate"));

        // Assert: the content is exact, from the root and from its own card, and the JSON names the card it belongs to
        Assert.Equal(expected, content);
        Assert.Equal(expected, HelpTopics.RenderExample(HelpTopics.Get("scad/language"), "plate"));
        Assert.StartsWith("// A plate with a round hole", content);
        var example = oneJson.RootElement.GetProperty("example");
        Assert.Equal("example", oneJson.RootElement.GetProperty("kind").GetString());
        Assert.Equal(expected, example.GetProperty("content").GetString());
        Assert.Equal("plate.scad", example.GetProperty("fileName").GetString());
        Assert.Equal("scad/language", example.GetProperty("card").GetString());
    }

    /// <summary>
    ///     Test that the examples text of a card prints each of its own examples in full: a heading with the name,
    ///     kind, and file name, the description, and the exact content in a code fence of its language.
    /// </summary>
    /// <param name="id">The card identifier.</param>
    [Theory]
    [MemberData(nameof(CardWithExamplesIdData))]
    public void HelpTopics_RenderExamples_CardWithExamples_PrintsEachExampleInFull(string id)
    {
        // Arrange / Act: render the examples of the card
        var card = HelpTopics.Get(id);
        var text = HelpTopics.RenderExamples(card);

        // Assert: plain LF text with the title, each example in full, and the command that prints one alone
        Assert.DoesNotContain('\r', text);
        Assert.False(text.EndsWith('\n'));
        Assert.StartsWith($"# {card.Title}: examples\n", text);
        var flat = HelpCardText.Flatten(text);
        foreach (var example in card.Examples)
        {
            var heading = example.FileName == null
                ? $"## {example.Name} [{example.Kind}]\n"
                : $"## {example.Name} [{example.Kind}] {example.FileName}\n";
            var fence = HelpTopics.CodeFence(example.Content);
            Assert.Contains(heading, text);
            Assert.Contains(example.Description, flat);
            Assert.Contains($"\n{fence}{example.Language}\n{example.Content}\n{fence}\n", text);
        }

        Assert.Contains($"{card.Command} --example <name>", flat);
        Assert.DoesNotContain("has no examples of its own", text);
    }

    /// <summary>
    ///     Test that the examples text of a card without examples of its own says so and lists the cards below it that
    ///     have examples, each with its examples command and example names, and prints no example content.
    /// </summary>
    [Fact]
    public void HelpTopics_RenderExamples_IndexCard_ListsCardsBelowWithExamples()
    {
        // Arrange: the mermaid topic (no examples of its own) and the json topic (examples of its own and below)
        var mermaid = HelpTopics.Get("mermaid");
        var json = HelpTopics.Get("json");

        // Act
        var mermaidText = HelpTopics.RenderExamples(mermaid);
        var jsonText = HelpTopics.RenderExamples(json);

        // Assert: mermaid points at every card below with examples and prints none of them
        Assert.Contains("'mermaid' has no examples of its own.", mermaidText);
        Assert.Contains("\n## Cards below with examples\n", mermaidText);
        var below = HelpTopics.CardsBelowWithExamples(mermaid).ToList();
        Assert.NotEmpty(below);
        Assert.All(below, c =>
        {
            Assert.Contains($"- {c.Command} --examples\n", mermaidText);
            Assert.All(c.Examples, e => Assert.DoesNotContain($"## {e.Name} [", mermaidText));
        });
        Assert.DoesNotContain("```", mermaidText);

        // Assert: json prints its own examples, then points at json help
        Assert.All(json.Examples, e => Assert.Contains(e.Content, jsonText));
        Assert.Contains("- canvasnet help json help --examples\n", jsonText);
        Assert.True(
            jsonText.IndexOf("--example <name>", StringComparison.Ordinal) < jsonText.IndexOf("## Cards below with examples", StringComparison.Ordinal),
            "The cards below are listed after the card's own examples.");
    }

    /// <summary>
    ///     Test that the examples JSON of a card has the documented shape: the card's own examples with their content,
    ///     without a card key, and the cards below it that have examples, with their example names.
    /// </summary>
    [Fact]
    public void HelpTopics_RenderExamplesJson_HasTheDocumentedShape()
    {
        // Arrange: the json topic, which has examples of its own and a card below with examples
        var card = HelpTopics.Get("json");

        // Act
        using var document = JsonDocument.Parse(HelpTopics.RenderExamplesJson(card));
        var root = document.RootElement;

        // Assert: the envelope and key order
        Assert.Equal(
            ["schemaVersion", "ok", "command", "kind", "id", "examples", "cards"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("examples", root.GetProperty("kind").GetString());
        Assert.Equal("json", root.GetProperty("id").GetString());

        // Assert: the card's own examples, in full and without a card key
        var examples = root.GetProperty("examples").EnumerateArray().ToList();
        Assert.Equal(card.Examples.Select(e => e.Name), examples.Select(e => e.GetProperty("name").GetString()));
        Assert.Equal(card.Examples.Select(e => e.Content), examples.Select(e => e.GetProperty("content").GetString()));
        Assert.All(examples, e => Assert.False(e.TryGetProperty("card", out _)));

        // Assert: the cards below with examples, with their names and commands
        var cards = root.GetProperty("cards").EnumerateArray().ToList();
        var below = HelpTopics.CardsBelowWithExamples(card).ToList();
        Assert.Equal(below.Select(c => c.Id), cards.Select(c => c.GetProperty("id").GetString()));
        Assert.Equal(below.Select(c => c.Command), cards.Select(c => c.GetProperty("command").GetString()));
        Assert.Equal(
            below[0].Examples.Select(e => e.Name),
            cards[0].GetProperty("examples").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["id", "summary", "command", "examples"], cards[0].EnumerateObject().Select(p => p.Name));
    }

    /// <summary>
    ///     Test that the code fence of an example is at least three backticks and longer than any run of backticks in the
    ///     content, so the content cannot end the block early.
    /// </summary>
    [Fact]
    public void HelpTopics_CodeFence_IsLongerThanAnyBacktickRunInTheContent()
    {
        // Arrange / Act / Assert
        Assert.Equal("```", HelpTopics.CodeFence("canvasnet formats --json"));
        Assert.Equal("```", HelpTopics.CodeFence("a `b` c ``d``"));
        Assert.Equal("````", HelpTopics.CodeFence("```mermaid\nflowchart LR\n```"));
        Assert.Equal("``````", HelpTopics.CodeFence("x `````"));
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
    ///     Gets the rows of every markdown table in one section of a guide, from its heading to the next heading of the
    ///     same or a higher level, each with the header of its table. Cells keep their backticks and read <c>\|</c> as
    ///     <c>|</c>.
    /// </summary>
    /// <param name="lines">The lines of the guide.</param>
    /// <param name="heading">The heading line of the section.</param>
    /// <returns>The header and the cells of each body row, in order.</returns>
    private static IEnumerable<(string[] Header, string[] Cells)> GuideTableRows(string[] lines, string heading)
    {
        var start = Array.IndexOf(lines, heading);
        Assert.True(start >= 0, $"The guide has no heading '{heading}'.");
        string[]? header = null;
        var separator = false;
        for (var i = start + 1; i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal)
                                && !lines[i].StartsWith("### ", StringComparison.Ordinal); i++)
        {
            if (!lines[i].StartsWith('|'))
            {
                header = null;
                continue;
            }

            var cells = TableCellSplit.Split(lines[i].Trim().Trim('|'))
                .Select(c => c.Replace("\\|", "|", StringComparison.Ordinal).Trim())
                .ToArray();
            if (header == null)
            {
                (header, separator) = (cells, true);
            }
            else if (separator)
            {
                separator = false;
            }
            else
            {
                yield return (header.Select(h => h.Replace("`", string.Empty, StringComparison.Ordinal)).ToArray(), cells);
            }
        }
    }

    /// <summary>
    ///     Gets the card heading that holds the rows of a diagram table of the guide.
    /// </summary>
    /// <param name="header">The header of the table.</param>
    /// <returns><c>Syntax</c>, <c>Relations</c>, <c>Tags</c>, or <c>Settings</c>.</returns>
    private static string DiagramHeading(string[] header)
    {
        return string.Join("|", header) switch
        {
            "Area|Supported" => "Syntax",
            "Relation|Drawn as" => "Relations",
            "Tag|Drawn as" => "Tags",
            "Key|Default|Meaning" => "Settings",
            _ => throw new InvalidOperationException($"The guide has a diagram table with the unknown header '{string.Join(" | ", header)}'."),
        };
    }

    /// <summary>
    ///     Gets the bullet that a row of a diagram table of the guide promises.
    /// </summary>
    /// <param name="row">The header and the cells of the row.</param>
    /// <returns>The bullet, without the leading <c>- </c>.</returns>
    private static string ExpectedDiagramBullet((string[] Header, string[] Cells) row)
    {
        var cells = row.Cells;
        return cells.Length == 3 ? $"{cells[0]} (default {cells[1]}): {cells[2]}." : $"{cells[0]}: {cells[1]}.";
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
        var sources = HelpTopics.SourceExamples().Select(e => e.Example).ToList();
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
    ///     Gets the lines of the fenced code blocks of a body that start with <c>canvasnet </c>.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The command lines.</returns>
    private static IEnumerable<string> FencedCommandLines(string body)
    {
        var inFence = false;
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence && line.StartsWith("canvasnet ", StringComparison.Ordinal))
            {
                yield return line;
            }
        }
    }

    /// <summary>
    ///     Gets the values of every error code constant of the tool.
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
    /// <returns>The title, summary, aliases, body, and examples (without the source of a source example).</returns>
    private static IEnumerable<string> AllStrings(HelpCard card)
    {
        yield return card.Title;
        yield return card.Summary;
        yield return card.Body;
        foreach (var example in card.Examples)
        {
            yield return example.Description;
            if (example.Kind == HelpTopics.CommandKind)
            {
                yield return example.Content;
            }
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
        typeof(PdfDocument).Assembly,
        typeof(PptxDocument).Assembly,
        typeof(VsdxDocument).Assembly,
        typeof(SvgCodec).Assembly,
        typeof(Surface).Assembly
    ];
}

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Viewer;
using HelpCatalog = DemaConsulting.CanvasNet.Viewer.Help.HelpCatalog;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the HelpTopics class, the command-line layer over the library's help catalog: the merged cards,
///     their consistency with the commands, the options, and the tool's user guide, the command examples, and the text
///     and JSON renderings. The content of the library cards is tested with the library (Viewer.Tests).
/// </summary>
[Collection("Sequential")]
public partial class HelpTopicsTests
{
    /// <summary>
    ///     The prefix of the logical names of the library's embedded help example files (the Viewer assembly).
    /// </summary>
    private const string LibraryExamplePrefix = "DemaConsulting.CanvasNet.Viewer.Help.Examples.";

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
    ///     The names of the global options, which every command accepts.
    /// </summary>
    private static readonly string[] GlobalOptionNames =
        ["--json", "--silent", "--log", "--validate", "--results", "--result", "--depth", "--version", "--help", "-v", "-?", "-h"];

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
            ["position"] = "positions",
            ["filter"] = "pandoc"
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
    ///     Test that no text of the merged cards contains an unresolved token and that the tool's token table holds the
    ///     live schema version (the library's tokens and the limits they quote are tested with the library).
    /// </summary>
    [Fact]
    public void HelpTopics_Text_HasNoUnresolvedTokens()
    {
        // Arrange / Act: every string of every card
        var texts = HelpTopics.All.SelectMany(AllStrings).ToList();

        // Assert: no token is left, and the tool's table holds the live schema version
        Assert.DoesNotContain(texts, t => t.Contains("{{", StringComparison.Ordinal));
        Assert.Equal(ResultWriter.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpTopics.Tokens["schemaVersion"]);
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
    ///     Test that the merged examples have valid kinds and names unique in each root tree, that every command example
    ///     is a <c>canvasnet</c> command line without a file name, and that every other example has a file name (the
    ///     library's examples are tested with the library).
    /// </summary>
    [Fact]
    public void HelpTopics_Examples_NamesUniqueKindsValidAndSourcesNonEmpty()
    {
        // Act / Assert: the names are unique in each root tree
        foreach (var root in HelpTopics.Roots)
        {
            var names = HelpTopics.SubtreeExamples(root).Select(e => e.Example.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        // Act / Assert: the rules hold for every example
        Assert.Contains(HelpTopics.AllExamples(), e => e.Example.Kind == HelpTopics.CommandKind);
        foreach (var (_, example) in HelpTopics.AllExamples())
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
        }
    }

    /// <summary>
    ///     Test that the file types the <c>svg embedding</c> card lists for <c>&lt;image href&gt;</c> are exactly the
    ///     extensions the <c>formats</c> command lists, so the two tables cannot drift apart (the library tests that the
    ///     card lists the extensions of <c>CanvasNetViewer</c>).
    /// </summary>
    [Fact]
    public void HelpTopics_SvgTopic_ImageHrefFileTypes_MatchTheFormatsCommand()
    {
        // Arrange: the extensions the card lists
        var text = HelpCardText.Item("svg/embedding", "href file types").Replace("`", string.Empty, StringComparison.Ordinal);

        // Act: the extensions listed, and the ones the formats command lists
        var listed = ExtensionToken.Matches(text).Select(m => m.Value).ToList();
        var formatted = DiscoveryCommands.InputFormats.SelectMany(f => f.Extensions).ToList();

        // Assert: the same extensions
        Assert.Equal(formatted.Order(StringComparer.Ordinal), listed.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test that the <c>## Reference</c> bullets of the <c>svg flatten</c> card (the library's) and then its
    ///     <c>## Command line</c> bullets (the tool's) are, word for word and in order, the items of the flatten reference
    ///     list of the tool's user guide.
    /// </summary>
    [Fact]
    public void HelpTopics_SvgTopic_FlattenItems_MatchUserGuide()
    {
        // Arrange: the guide and the card
        var guide = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-tool.md"));
        var bullets = HelpCardText.Bullets("svg/flatten", "Reference").Concat(HelpCardText.Bullets("svg/flatten", "Command line")).ToList();

        // Act: the bullets the items of the guide's list promise
        var items = GuideItems(guide, "#### Flatten Reference").ToList();

        // Assert: the list has its items, and they are the same bullets in the same order
        Assert.True(items.Count >= 7, $"Only {items.Count} items were found in the flatten reference list of the guide.");
        Assert.Equal(items.Select(item => item.Term + item.Tail), bullets);
    }

    /// <summary>
    ///     Test that the bullets of the <c>files</c> card are, word for word and in order, the items of the file access
    ///     reference list of the tool's user guide, and that each has a term.
    /// </summary>
    [Fact]
    public void HelpTopics_FilesTopic_Items_MatchUserGuide()
    {
        // Arrange: the guide and every bullet of the card, in order
        var guide = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-tool.md"));
        var bullets = HelpCardText.Bullets("files");

        // Act: the items of the reference list
        var items = GuideItems(guide, "#### File Access Reference").ToList();

        // Assert: every bullet has a term, the list has its items, and the bullets are the items, in order
        Assert.Equal(bullets.Count, HelpCardText.Items("files").Count);
        Assert.True(items.Count >= 16, $"Only {items.Count} items were found in the file access reference list of the guide.");
        Assert.Equal(items.Select(item => item.Term + item.Tail), bullets);
    }

    /// <summary>
    ///     Test that the <c>files</c> card documents the modes, the two options, what holds in every mode, the two
    ///     diagnostics with their live codes, and the variables of the Pandoc filter (which the filter settings read and the
    ///     <c>pandoc</c> card documents), and that the <c>svg</c> and <c>errors</c> cards point to it.
    /// </summary>
    [Fact]
    public void HelpTopics_FilesTopic_DocumentsModesOptionsDiagnosticsAndPandoc()
    {
        // Arrange: the card, its terms, the source of the filter settings, the pandoc card, and the cards that point to it
        var card = HelpTopics.Get("files");
        var terms = HelpCardText.Items("files").Select(i => i.Term).ToList();
        var settings = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "DemaConsulting.CanvasNet.Tool", "PandocFilter", "FilterSettings.cs"));
        var pandocTerms = HelpCardText.Items("pandoc").Select(i => i.Term).ToList();

        // Assert: the headings and the terms
        Assert.Equal(["Modes", "Options", "In every mode", "Diagnostics", "Command line", "The Pandoc filter"], HelpCardText.Headings(card.Body));
        foreach (var term in new[] { "any", "roots", "no-symlinks", "roots,no-symlinks", "--file-access <mode>", "--allow-root <folder>", "inputs", "never read", "limits" })
        {
            Assert.Contains(term, terms);
        }

        Assert.Contains(FolderImageResolver.AccessDeniedCode, terms);
        Assert.Contains(ErrorCodes.FileAccessDenied, terms);

        // Assert: the variables of the filter are terms of both cards and are read by the filter settings
        foreach (var variable in new[] { "CANVASNET_FILE_ACCESS", "CANVASNET_ROOTS" })
        {
            Assert.Contains(variable, terms);
            Assert.Contains(variable, pandocTerms);
            Assert.Contains($"\"{variable}\"", settings, StringComparison.Ordinal);
        }

        // Assert: the cards that describe a denial point here
        foreach (var name in new[] { "svg", "errors" })
        {
            Assert.Contains("canvasnet help files", HelpCardText.TextOf(name), StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Test that the <c>json diagnostics</c> card documents <c>detail</c>, <c>location</c>, and <c>origin</c> (the
    ///     library tests that the format cards document the page fragment and <c>limits</c> its limits).
    /// </summary>
    [Fact]
    public void HelpTopics_JsonDiagnosticsCard_DocumentsDetailLocationAndOrigin()
    {
        // Act: the items of the card
        var json = HelpCardText.Items("json/diagnostics");

        // Assert: the three members are documented
        foreach (var term in new[] { "detail", "location", "origin" })
        {
            Assert.Contains(json, i => i.Term == term);
        }
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
    ///     Test that every option an <c>## Options</c> section or a <c>## Command line</c> section (the tool's additions to
    ///     a library card) lists as a term is an option of <c>render</c> or a global option.
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
            foreach (var (term, _) in HelpCardText.Items(card.Id, "Options").Concat(HelpCardText.Items(card.Id, "Command line")).Where(i => i.Term.StartsWith('-')))
            {
                var name = term.Split(' ')[0];
                Assert.True(known.Contains(name), $"Card '{card.Id}' lists the unknown option '{name}'.");
                checkedTerms++;
            }
        }

        Assert.True(checkedTerms > 30, $"Only {checkedTerms} option terms were found.");
    }

    /// <summary>
    ///     Test that the merged <c>errors codes</c> card lists every error code, and nothing else as a code: the library's
    ///     card lists the library's codes, and the tool's command-line section lists exactly the codes only the tool
    ///     reports.
    /// </summary>
    [Fact]
    public void HelpTopics_ErrorsTopic_ListsEveryErrorCode()
    {
        // Arrange: the codes of the code, the codes only the tool reports, and the terms of the card
        var codes = ErrorCodeValues().ToList();
        string[] toolCodes = [ErrorCodes.ThresholdFailed, ErrorCodes.PandocFilterError];

        // Act: the terms of the merged card, and of the tool's section
        var terms = HelpCardText.Items("errors/codes").Select(i => i.Term).ToList();
        var toolTerms = HelpCardText.Items("errors/codes", "Command line").Select(i => i.Term).ToList();

        // Assert: the same set, and the tool's section lists the tool's codes
        Assert.NotEmpty(codes);
        Assert.Equal(codes.Order(StringComparer.Ordinal), terms.Order(StringComparer.Ordinal));
        Assert.Equal(toolCodes.Order(StringComparer.Ordinal), toolTerms.Order(StringComparer.Ordinal));
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
            ["schemaVersion", "ok", "command", "kind", "id", "aliases", "tags", "title", "summary", "body", "children", "seeAlso", "examples"],
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
        using var stream = typeof(HelpCatalog).Assembly.GetManifestResourceStream($"{LibraryExamplePrefix}scad-plate.scad")!;
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

}

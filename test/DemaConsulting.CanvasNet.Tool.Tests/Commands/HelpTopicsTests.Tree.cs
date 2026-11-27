using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Loading;
using DemaConsulting.HelpCards.Testing;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the tree of help cards: the files, the links, the names, the size budget, the addressing, and the
///     semantic checks of the loader.
/// </summary>
public partial class HelpTopicsTests
{
    /// <summary>
    ///     The validation options of the combined cards: the library's card plus the tool's command-line additions may
    ///     have 7,000 characters, a root with cards below it 4,000; summaries of 100 characters and 60 for a root; lines
    ///     of at most 80 columns outside a fenced code block and 120 inside one; and every warning an error, so every
    ///     command example must parse as a <c>canvasnet</c> command line.
    /// </summary>
    private static readonly HelpValidationOptions ToolValidation = new()
    {
        MaxCardChars = 7000,
        MaxRootIndexChars = 4000,
        MaxSummaryChars = 100,
        MaxRootSummaryChars = 60,
        MaxLineLength = 80,
        MaxFenceLineLength = 120,
        WarningsAreErrors = true
    };
    /// <summary>
    ///     Test that every child and every See also entry of every card names a card, that every card is reachable from
    ///     a root, and that no card links to itself or twice to one card.
    /// </summary>
    [Fact]
    public void HelpTopics_Tree_ChildrenAndSeeAlsoResolve()
    {
        // Arrange: every identifier, and the cards reachable from the roots
        var ids = HelpView.All.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        void Visit(HelpCard card)
        {
            Assert.True(reachable.Add(card.Id), $"Card '{card.Id}' is reached twice.");
            foreach (var child in HelpView.ChildrenOf(card))
            {
                Assert.Equal(card.Id, child.ParentId);
                Visit(child);
            }
        }

        foreach (var root in HelpTopics.Roots)
        {
            Visit(root);
        }

        // Assert: every card is reachable, and every link names another card once
        Assert.Equal(ids.Order(StringComparer.Ordinal), reachable.Order(StringComparer.Ordinal));
        foreach (var card in HelpView.All)
        {
            Assert.All(card.ChildIds, child => Assert.Contains(child, ids));
            Assert.All(card.SeeAlso, target => Assert.Contains(target, ids));
            Assert.DoesNotContain(card.Id, card.SeeAlso);
            Assert.Equal(card.SeeAlso.Count, card.SeeAlso.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    ///     Test that the tool's embedded files are exactly its own cards (the json and pandoc trees), overlays of library
    ///     cards, and the file that names the source, whatever path separator the build put in the logical names, and
    ///     that the children of every tool card are exactly the card files of its folder.
    /// </summary>
    [Fact]
    public void HelpTopics_Tree_ResourcesAreExactlyTheToolCardsAndOverlays()
    {
        // Arrange: the embedded files, by their path
        const string Prefix = "DemaConsulting.CanvasNet.Tool.Help/";
        var files = typeof(HelpTopics).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..].Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        var cardFiles = files.Where(f => f != "_source.md").ToList();
        var toolCards = HelpView.All.Where(c => c.RootId is "json" or "pandoc").ToList();

        // Assert: each file is the source file or a card of the tree, and each tool card is a file
        Assert.Contains("_source.md", files);
        Assert.All(cardFiles, f => Assert.EndsWith(".md", f));
        Assert.All(cardFiles, f => Assert.Contains(f[..^".md".Length], HelpView.All.Select(c => c.Id)));
        Assert.All(toolCards, c => Assert.Contains(c.Id + ".md", cardFiles));

        // Assert: the children of a tool card are the card files directly in its folder
        foreach (var card in toolCards)
        {
            var folder = card.Id + "/";
            var inFolder = cardFiles
                .Where(f => f.StartsWith(folder, StringComparison.Ordinal) && !f[folder.Length..].Contains('/'))
                .Select(f => f[..^".md".Length])
                .Order(StringComparer.Ordinal);
            Assert.Equal(inFolder, card.ChildIds.Order(StringComparer.Ordinal));
        }
    }
    /// <summary>
    ///     Test that the names and aliases of the cards below each card are unique, ignoring case, so every path selects
    ///     exactly one card.
    /// </summary>
    [Fact]
    public void HelpTopics_ChildNames_AreUniqueAmongSiblings()
    {
        // Act / Assert: for each card, the names and aliases of its children are unique
        foreach (var card in HelpView.All)
        {
            var names = HelpView.ChildrenOf(card).SelectMany(c => c.Aliases.Prepend(c.Name)).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.DoesNotContain(names, n => n.Equals("topics", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Test that the combined cards pass the validator of DemaConsulting.HelpCards with the tool's strict options:
    ///     the size and summary budgets, the column limits, reserved and top-level headings, fence languages, and command
    ///     examples that parse as <c>canvasnet</c> command lines (without shell expansion), with no warning at all.
    /// </summary>
    [Fact]
    public void HelpTopics_Cards_PassTheStrictValidation()
    {
        // Act
        var problems = HelpCardValidator.Validate(builder => HelpTopics.Configure(builder), ToolValidation);

        // Assert
        Assert.True(problems.Count == 0, string.Join("\n", problems.Select(p => $"{p.RuleId} {p.File}:{p.Line} {p.Message}")));
    }

    /// <summary>
    ///     Test that a command example that a shell would expand is reported by the strict validation, so the quoting of
    ///     every command example is checked.
    /// </summary>
    [Fact]
    public void HelpTopics_Validation_UnquotedPagePattern_IsReported()
    {
        // Arrange: a host card with the unquoted page pattern of an all-slides command
        var host = HelpSource.FromFiles(
            new Dictionary<string, string>
            {
                ["slides.md"] = "---\ntitle: Slides\nsummary: All slides.\ntags:\n  - hosting\nexamples:\n  - name: all\n"
                                + "    kind: command\n    language: shell\n"
                                + "    content: 'canvasnet render slides.pptx --all-pages -o out/slide-{page}.png'\n"
                                + "    description: Every slide.\n---\n\nSlides."
            },
            "host");

        // Act
        var problems = HelpCardValidator.Validate(builder => CanvasNetHelp.Configure(builder).AddSource(host).ToolName(HelpTopics.ToolName), ToolValidation);

        // Assert
        Assert.Contains(problems, p => p.RuleId == "command-parse" && p.File == "slides.md");
    }
    /// <summary>
    ///     Test that a help path resolves by names and aliases at every level, ignoring case, with the words separate or
    ///     joined by <c>/</c>, and that an unknown root resolves to nothing.
    /// </summary>
    [Fact]
    public void HelpTopics_Resolve_PathsAndAliases_FindTheCard()
    {
        // Arrange
        var catalog = HelpTopics.Catalog;

        // Act / Assert: names, aliases, case, and slashes
        Assert.Equal("mermaid/gantt", catalog.Find("mermaid gantt")?.Id);
        Assert.Equal("mermaid/gantt", catalog.Find("MMD GANTT")?.Id);
        Assert.Equal("mermaid/gantt/settings", catalog.Find("mermaid/gantt/settings")?.Id);
        Assert.Equal("mermaid/flowchart", catalog.Find("mmd graph")?.Id);
        Assert.Equal("vega-lite/encodings", catalog.Find("vl scales")?.Id);
        Assert.Equal("svg/embedding", catalog.Find("svg links")?.Id);
        Assert.Equal("scad/views", catalog.Find("openscad camera")?.Id);
        Assert.Equal("scad", catalog.Find("scad")?.Id);
        Assert.Equal("json/help", catalog.Find("json help")?.Id);
        Assert.Null(catalog.Find("nothing gantt"));
        Assert.All(HelpView.All, card => Assert.Equal(card.Id, catalog.Find(card.Path)?.Id));
    }

    /// <summary>
    ///     Test that an unknown word below a card is a usage error that suggests the closest name and lists the cards at
    ///     that level, and that a word below a card without children says so.
    /// </summary>
    [Fact]
    public void HelpTopics_Resolve_UnknownChild_ThrowsUsageErrorListingChildren()
    {
        // Arrange
        var registry = CommandRegistry.CreateDefault();

        // Act
        var typo = Assert.Throws<ToolException>(() => registry.RenderHelp(["mermaid", "giant"]));
        var leaf = Assert.Throws<ToolException>(() => registry.RenderHelp(["mermaid", "gantt", "settings", "more"]));

        // Assert: the typo is named, the closest card suggested, and the cards listed
        Assert.Equal(ErrorCodes.UsageError, typo.Code);
        Assert.Equal("Unknown help card 'giant' under 'mermaid'.", typo.Message);
        Assert.StartsWith("Did you mean 'gantt'? Cards under 'mermaid': flowchart, sequence,", typo.Hint);
        Assert.Contains("Run 'canvasnet help mermaid' for their summaries.", typo.Hint);
        Assert.Equal("help", typo.Command);
        Assert.Equal("Unknown help card 'more' under 'mermaid/gantt/settings'.", leaf.Message);
        Assert.StartsWith("'mermaid/gantt/settings' has no cards below it.", leaf.Hint);
    }
    /// <summary>
    ///     Test that the tree lists every card, depth first, with the command that prints it and its summary, as text and as JSON.
    /// </summary>
    [Fact]
    public void HelpTopics_RenderTopics_ListsEveryCard()
    {
        // Act
        var text = HelpTopics.RenderTopics(false);
        using var document = JsonDocument.Parse(HelpTopics.RenderTopics(true));
        var cards = document.RootElement.GetProperty("cards").EnumerateArray().ToList();

        // Assert: the text has a line per card, in order, and lines of at most 80 columns
        var position = 0;
        foreach (var card in HelpView.All)
        {
            var at = text.IndexOf($"\n{new string(' ', 2 * card.Depth)}{card.Command} ", position, StringComparison.Ordinal);
            Assert.True(at >= position, $"Card '{card.Id}' is missing or out of order.");
            position = at + 1;
        }

        Assert.All(text.Split('\n'), line => Assert.True(line.Length <= 80, $"Line too long: {line}"));
        Assert.Contains(HelpView.All[^1].Summary.Split(' ')[0], HelpCardText.Flatten(text));

        // Assert: the JSON lists every card with its command
        Assert.Equal("topics", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal(HelpView.All.Select(c => c.Id), cards.Select(c => c.GetProperty("id").GetString()));
        Assert.Equal(HelpView.All.Select(c => c.Command), cards.Select(c => c.GetProperty("command").GetString()));
        Assert.Equal(HelpView.All.Select(c => c.Summary), cards.Select(c => c.GetProperty("summary").GetString()));
    }
}

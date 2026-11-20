using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the tree of help cards: the files, the links, the names, the size budget, the addressing, and the
///     semantic checks of the loader.
/// </summary>
public partial class HelpTopicsTests
{
    /// <summary>
    ///     The most characters the text of a card may have: the library's card (at most 6,000 characters there) plus the
    ///     tool's command-line additions.
    /// </summary>
    private const int CardBudget = 7000;

    /// <summary>
    ///     The most characters the text of a root card that has cards below it may have (at most 3,500 in the library).
    /// </summary>
    private const int RootIndexBudget = 4000;

    /// <summary>
    ///     Test that every child and every See also entry of every card names a card, that every card is reachable from
    ///     a root, and that no card links to itself or twice to one card.
    /// </summary>
    [Fact]
    public void HelpTopics_Tree_ChildrenAndSeeAlsoResolve()
    {
        // Arrange: every identifier, and the cards reachable from the roots
        var ids = HelpTopics.All.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        void Visit(HelpCard card)
        {
            Assert.True(reachable.Add(card.Id), $"Card '{card.Id}' is reached twice.");
            foreach (var child in HelpTopics.ChildrenOf(card))
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
        foreach (var card in HelpTopics.All)
        {
            Assert.All(card.ChildIds, child => Assert.Contains(child, ids));
            Assert.All(card.SeeAlso, target => Assert.Contains(target, ids));
            Assert.DoesNotContain(card.Id, card.SeeAlso);
            Assert.Equal(card.SeeAlso.Count, card.SeeAlso.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    ///     Test that the tool's embedded card files are exactly its own cards (the json and pandoc trees) plus overlays of
    ///     library cards, whatever path separator the build put in the logical names.
    /// </summary>
    [Fact]
    public void HelpTopics_Tree_ResourcesAreExactlyTheToolCardsAndOverlays()
    {
        // Arrange: the embedded card files, by their path
        var files = typeof(HelpTopics).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(HelpTopics.CardResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[HelpTopics.CardResourcePrefix.Length..].Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        var toolCards = HelpTopics.All.Where(c => c.RootId is "json" or "pandoc").Select(c => c.Id + HelpTopics.CardExtension);

        // Assert: each file is a card of the tree, and each tool card is a file
        Assert.All(files, f => Assert.EndsWith(HelpTopics.CardExtension, f));
        Assert.All(files, f => Assert.Contains(f[..^HelpTopics.CardExtension.Length], HelpTopics.All.Select(c => c.Id)));
        Assert.All(toolCards, c => Assert.Contains(c, files));
    }

    /// <summary>
    ///     Test that the names and aliases of the cards below each card are unique, ignoring case, so every path selects
    ///     exactly one card.
    /// </summary>
    [Fact]
    public void HelpTopics_ChildNames_AreUniqueAmongSiblings()
    {
        // Act / Assert: for each card, the names and aliases of its children are unique
        foreach (var card in HelpTopics.All)
        {
            var names = HelpTopics.ChildrenOf(card).SelectMany(c => c.Aliases.Prepend(c.Name)).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.DoesNotContain(names, n => n.Equals("topics", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Test that every card fits the size budget: at most 7,000 characters of text, at most 4,000 for a root card
    ///     that has cards below it, and a one-line summary of at most 60 characters for a root and 100 for any other card.
    /// </summary>
    [Fact]
    public void HelpTopics_Cards_FitTheSizeBudget()
    {
        // Act: the size of the text of every card
        var oversized = HelpTopics.All
            .Select(c => (Card: c, Length: HelpTopics.RenderText(c).Length))
            .Where(x => x.Length > (x.Card.ParentId == null && x.Card.ChildIds.Count > 0 ? RootIndexBudget : CardBudget))
            .Select(x => $"{x.Card.Id}: {x.Length}")
            .ToList();

        // Assert: nothing is over budget, and the summaries are short single lines
        Assert.True(oversized.Count == 0, "Over budget: " + string.Join(", ", oversized));
        Assert.All(HelpTopics.All, card =>
        {
            Assert.DoesNotContain('\n', card.Summary);
            Assert.True(card.Summary.Length <= (card.ParentId == null ? 60 : 100), $"The summary of '{card.Id}' is too long.");
        });
    }

    /// <summary>
    ///     Test that a help path resolves by names and aliases at every level, ignoring case, with the words separate or
    ///     joined by <c>/</c>, and that an unknown root resolves to nothing.
    /// </summary>
    [Fact]
    public void HelpTopics_Resolve_PathsAndAliases_FindTheCard()
    {
        // Act / Assert: names, aliases, case, and slashes
        Assert.Equal("mermaid/gantt", HelpTopics.Resolve(["mermaid", "gantt"])?.Id);
        Assert.Equal("mermaid/gantt", HelpTopics.Resolve(["MMD", "GANTT"])?.Id);
        Assert.Equal("mermaid/gantt/settings", HelpTopics.Resolve(["mermaid/gantt/settings"])?.Id);
        Assert.Equal("mermaid/flowchart", HelpTopics.Resolve(["mmd", "graph"])?.Id);
        Assert.Equal("vega-lite/encodings", HelpTopics.Resolve(["vl", "scales"])?.Id);
        Assert.Equal("svg/embedding", HelpTopics.Resolve(["svg", "links"])?.Id);
        Assert.Equal("scad/views", HelpTopics.Resolve(["openscad", "camera"])?.Id);
        Assert.Equal("scad", HelpTopics.Resolve(["scad"])?.Id);
        Assert.Null(HelpTopics.Resolve(["nothing", "gantt"]));
        Assert.Null(HelpTopics.Resolve([]));
        Assert.All(HelpTopics.All, card => Assert.Equal(card.Id, HelpTopics.Resolve(card.Path.Split(' '))?.Id));
    }

    /// <summary>
    ///     Test that an unknown word below a card is a usage error that suggests the closest name and lists the cards at
    ///     that level, and that a word below a card without children says so.
    /// </summary>
    [Fact]
    public void HelpTopics_Resolve_UnknownChild_ThrowsUsageErrorListingChildren()
    {
        // Act
        var typo = Assert.Throws<ToolException>(() => HelpTopics.Resolve(["mermaid", "giant"]));
        var leaf = Assert.Throws<ToolException>(() => HelpTopics.Resolve(["mermaid", "gantt", "settings", "more"]));

        // Assert: the typo is named, the closest card suggested, and the cards listed
        Assert.Equal(ErrorCodes.UsageError, typo.Code);
        Assert.Equal("Unknown help card 'giant' under 'mermaid'.", typo.Message);
        Assert.StartsWith("Did you mean 'gantt'? Cards under 'mermaid': flowchart, sequence,", typo.Hint);
        Assert.Contains("Run 'canvasnet help mermaid' for their summaries.", typo.Hint);
        Assert.Equal("help", typo.Command);
        Assert.Equal("Unknown help card 'more' under 'mermaid gantt settings'.", leaf.Message);
        Assert.StartsWith("'mermaid gantt settings' has no cards below it.", leaf.Hint);
    }

    /// <summary>
    ///     Test that the tree lists every card, depth first, with its path and summary, as text and as JSON.
    /// </summary>
    [Fact]
    public void HelpTopics_RenderTopics_ListsEveryCard()
    {
        // Act
        var text = HelpTopics.RenderTopicsText();
        using var document = JsonDocument.Parse(HelpTopics.RenderTopicsJson());
        var cards = document.RootElement.GetProperty("cards").EnumerateArray().ToList();

        // Assert: the text has a line per card, in order, and lines of at most 80 columns
        var position = 0;
        foreach (var card in HelpTopics.All)
        {
            var at = text.IndexOf($"\n{new string(' ', 2 * card.Depth)}{card.Path} ", position, StringComparison.Ordinal);
            Assert.True(at >= position, $"Card '{card.Id}' is missing or out of order.");
            position = at + 1;
        }

        Assert.All(text.Split('\n'), line => Assert.True(line.Length <= 80, $"Line too long: {line}"));
        Assert.Contains(HelpTopics.All[^1].Summary.Split(' ')[0], HelpCardText.Flatten(text));

        // Assert: the JSON lists every card with its command
        Assert.Equal("topics", document.RootElement.GetProperty("kind").GetString());
        Assert.Equal(HelpTopics.All.Select(c => c.Id), cards.Select(c => c.GetProperty("id").GetString()));
        Assert.Equal(HelpTopics.All.Select(c => c.Command), cards.Select(c => c.GetProperty("command").GetString()));
        Assert.Equal(HelpTopics.All.Select(c => c.Summary), cards.Select(c => c.GetProperty("summary").GetString()));
    }

    /// <summary>
    ///     Test that no body uses a top-level heading (the title is printed as one) or a heading of the generated footer, and
    ///     that every fenced code block names its language and is closed.
    /// </summary>
    [Fact]
    public void HelpTopics_Bodies_DoNotUseReservedHeadingsOrTopLevelHeadings()
    {
        // Act / Assert: the rules hold for every body
        foreach (var card in HelpTopics.All)
        {
            var lines = card.Body.Split('\n');
            var fences = 0;
            foreach (var line in lines)
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
                Assert.DoesNotContain(line, HelpTopics.FooterHeadings);
            }

            Assert.True(fences % 2 == 0, $"A code block of '{card.Id}' is not closed.");
        }
    }
}

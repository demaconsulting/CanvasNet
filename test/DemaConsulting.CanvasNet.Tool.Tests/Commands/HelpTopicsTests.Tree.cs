using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the tree of help cards: the files, the links, the names, the size budget, the addressing, and the
///     semantic checks of the loader.
/// </summary>
public partial class HelpTopicsTests
{
    /// <summary>
    ///     The most characters the text of a card may have.
    /// </summary>
    private const int CardBudget = 6000;

    /// <summary>
    ///     The most characters the text of a root card that has cards below it may have.
    /// </summary>
    private const int RootIndexBudget = 3500;

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
    ///     Test that the embedded card files are exactly the cards (no orphan file and no missing file), whatever path
    ///     separator the build put in the logical names.
    /// </summary>
    [Fact]
    public void HelpTopics_Tree_ResourcesAreExactlyTheCards()
    {
        // Arrange: the embedded card files, by their path
        var files = typeof(HelpTopics).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(HelpTopics.CardResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[HelpTopics.CardResourcePrefix.Length..].Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert: each file is a card, and each card is a file
        Assert.All(files, f => Assert.EndsWith(HelpTopics.CardExtension, f));
        Assert.Equal(HelpTopics.All.Select(c => c.Id + HelpTopics.CardExtension).Order(StringComparer.Ordinal), files);
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
    ///     Test that every card fits the size budget: at most 6,000 characters of text, at most 3,500 for a root card
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

    /// <summary>
    ///     Test that a card whose front matter is well formed but wrong is an error that names the file and the line: an
    ///     unknown key, a missing title, a list where text is expected, an example of an unknown kind, an example without
    ///     a name, a command that is not the tool's, an unknown token, and a bad file name.
    /// </summary>
    /// <param name="frontMatter">The front-matter lines, separated by <c>~</c>.</param>
    /// <param name="line">The line the error must name.</param>
    /// <param name="reason">A part of the reason the error must give.</param>
    [Theory]
    [InlineData("title: T~summary: S~flavor: red", 4, "the key 'flavor' is unknown")]
    [InlineData("summary: S", 1, "lacks the key 'title'")]
    [InlineData("title:~  - T~summary: S", 2, "must be one line of text")]
    [InlineData("title: T~summary: S~aliases: x", 4, "must be a list")]
    [InlineData("title: T~summary: S~aliases:~  - Bad_Alias", 5, "the alias 'Bad_Alias'")]
    [InlineData("title: T~summary: S~examples:~  - name: a~    kind: movie~    language: x~    description: d", 5, "must be a source or a snippet")]
    [InlineData("title: T~summary: S~examples:~  - kind: command~    language: x~    description: d~    content: 'canvasnet x'", 5, "lacks the key 'name'")]
    [InlineData("title: T~summary: S~examples:~  - name: a~    kind: command~    language: x~    description: d~    content: 'ls x'", 5, "must start with 'canvasnet '")]
    [InlineData("title: T~summary: S~examples:~  - name: a~    flavor: red", 6, "the example key 'flavor' is unknown")]
    public void HelpTopics_LoadCard_SemanticErrors_ThrowWithFileAndLine(string frontMatter, int line, string reason)
    {
        // Arrange: a card with the front matter
        var text = $"---\n{frontMatter.Replace('~', '\n')}\n---\nBody.";

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => HelpTopics.LoadCard("scad/test.md", text));

        // Assert: the message names the file, the line, and the reason
        Assert.StartsWith($"Help card 'scad/test.md', line {line}: ", exception.Message);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an unknown token and a bad file name are errors that name the file, and that a good card loads with
    ///     its identifier, children, and tokens.
    /// </summary>
    [Fact]
    public void HelpTopics_LoadCard_TokensAndFileNames_AreChecked()
    {
        // Act
        var token = Assert.Throws<InvalidOperationException>(() => HelpTopics.LoadCard("scad/test.md", "---\ntitle: T\nsummary: S\n---\nAt most {{nothing}}."));
        var name = Assert.Throws<InvalidOperationException>(() => HelpTopics.LoadCard("scad/Test Card.md", "---\ntitle: T\nsummary: S\n---\nBody."));
        var card = HelpTopics.LoadCard("scad/test.md", "---\ntitle: T\nsummary: S\nchildren:\n  - more\n---\n\nAt most {{maxDimension}} pixels.\n");

        // Assert
        Assert.Equal("Help card 'scad/test.md' uses the unknown token '{{nothing}}'.", token.Message);
        Assert.StartsWith("Help card 'scad/Test Card.md' must be named", name.Message);
        Assert.Equal("scad/test", card.Id);
        Assert.Equal("test", card.Name);
        Assert.Equal("scad", card.RootId);
        Assert.Equal("scad test", card.Path);
        Assert.Equal("canvasnet help scad test", card.Command);
        Assert.Equal(["scad/test/more"], card.ChildIds);
        Assert.Equal($"At most {HelpTopics.Tokens["maxDimension"]} pixels.", card.Body);
    }

    /// <summary>
    ///     Test that the image-href file types are also listed where an agent looks for composition: the extensions of the
    ///     viewer are named in the <c>svg embedding</c> card that every composition card links to.
    /// </summary>
    [Fact]
    public void HelpTopics_EmbeddingCard_IsLinkedFromTheFormatCompositionCards()
    {
        // Act / Assert: each composition card of a format points at the embedding card or is it
        foreach (var id in HelpTopics.All.Where(c => c.Name == "composition" && c.ParentId != null).Select(c => c.Id))
        {
            var card = HelpTopics.Get(id);
            Assert.True(
                card.SeeAlso.Contains("svg/embedding") || card.Id == "svg/composition" || card.Body.Contains("canvasnet help svg embedding", StringComparison.Ordinal),
                $"'{id}' does not point at 'svg/embedding'.");
        }

        Assert.NotEmpty(CanvasNetViewer.Extensions);
    }
}

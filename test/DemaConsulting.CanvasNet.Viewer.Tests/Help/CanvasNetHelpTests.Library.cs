using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Presentation;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Unit tests for the library catalog as a host sees it: the tags of every card, filtered views of the real
///     catalog, concurrent reads, and text that reads correctly with no command line around.
/// </summary>
public partial class CanvasNetHelpTests
{
    /// <summary>
    ///     The render options of a JSON document.
    /// </summary>
    private static readonly HelpRenderOptions Json = new() { Format = HelpFormat.Json };

    /// <summary>
    ///     Matches a card link of the library's text form: <c>the card `path`</c>.
    /// </summary>
    private static readonly Regex LibraryCardLink = new(@"the card `(?<id>[a-z0-9/-]+)`", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Gets filters of the library catalog as theory data: the included and the excluded tags, comma separated.
    /// </summary>
    public static TheoryData<string, string> LibraryFilterData =>
        new()
        {
            { "mermaid", "hosting" },
            { "", "file-access" },
            { "design", "" },
            { "svg,composition", "hosting,limits" },
            { "diagnostics", "" }
        };

    /// <summary>
    ///     Test that every tag of every library card is from the closed vocabulary, at most once, that every card has a
    ///     scope tag, and that every tag of the vocabulary is used by some card.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_Default_Tags_AreFromTheClosedVocabulary_AndEveryCardHasAScopeTag()
    {
        // Arrange
        var cards = CanvasNetHelp.Catalog.Cards;

        // Act
        var used = cards.SelectMany(c => c.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        // Assert: each card's tags are known, unique, and include a scope tag
        Assert.All(cards, card =>
        {
            Assert.All(card.Tags, tag => Assert.Contains(tag, HelpTags.All));
            Assert.Equal(card.Tags.Count, card.Tags.Distinct(StringComparer.Ordinal).Count());
            Assert.Contains(card.Tags, HelpTags.ScopeTags.Contains);
        });

        // Assert: every tag is used
        Assert.Equal(HelpTags.All.Order(StringComparer.Ordinal), used);
    }

    /// <summary>
    ///     Test that no library card has command-line text: the titles, summaries, bodies, example descriptions, and
    ///     example contents never name the <c>canvasnet</c> tool or a <c>--option</c>, and no example is a command line.
    ///     The allow-list holds the text that names the tool for another reason.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_LibraryCards_HaveNoCommandLineText()
    {
        // Act: the command-line text the rule finds in every library card
        var found = NoCommandLineTextRule.CheckLibrary().Select(p => $"{p.File}: {p.Message}").ToList();

        // Assert: none
        Assert.True(found.Count == 0, "Command-line text in library cards: " + string.Join(", ", found));
    }
    /// <summary>
    ///     Test that the allow-list of the command-line text test is needed: each allowed text occurs in a library card,
    ///     and the scan finds the tool name and an option in text that has them.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_LibraryCards_CommandLineTextScan_FindsWhatItLooksFor()
    {
        // Arrange: every string of every card
        var texts = CanvasNetHelp.Catalog.Cards
            .SelectMany(c => new[] { c.Body }.Concat(c.Examples.Select(e => e.Content)))
            .ToList();

        // Act / Assert: each allowed text is used, and the patterns find command-line text but not Mermaid edges
        Assert.All(NoCommandLineTextRule.AllowList, allowed => Assert.Contains(texts, t => t.Contains(allowed, StringComparison.Ordinal)));
        Assert.Matches(NoCommandLineTextRule.ToolName, "run canvasnet render");
        Assert.DoesNotMatch(NoCommandLineTextRule.ToolName, "CanvasNet draws it");
        Assert.Matches(NoCommandLineTextRule.CommandLineOption, "use --strict");
        Assert.Matches(NoCommandLineTextRule.CommandLineOption, "(--file-access roots)");
        Assert.DoesNotMatch(NoCommandLineTextRule.CommandLineOption, "A --o B, A --x B, A --> B, A --- B, x--y");
    }

    /// <summary>
    ///     Test that a filtered view of the library catalog never points at a card it hides: the roots and children are
    ///     visible cards (a visible card below hidden ones is listed under its nearest visible ancestor, or as a root), the
    ///     See also lists, the links a host formats, the library's text links, and the JSON name only visible cards, the
    ///     index lists the visible cards, and a hidden card is not found.
    /// </summary>
    /// <param name="include">The included tags, comma separated.</param>
    /// <param name="exclude">The excluded tags, comma separated.</param>
    [Theory]
    [MemberData(nameof(LibraryFilterData))]
    public void CanvasNetHelp_DefaultFiltered_NeverPointsAtAHiddenCard(string include, string exclude)
    {
        // Arrange
        static string[] Tags(string list) => list.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var catalog = CanvasNetHelp.Catalog;

        // Act
        var view = catalog.Filter(Tags(include), Tags(exclude));
        var visible = view.Cards.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        // Assert: the view hides some cards and shows some
        Assert.NotEmpty(visible);
        Assert.True(visible.Count < catalog.Cards.Count, "The filter hides no card.");
        Assert.All(catalog.Cards.Where(c => !visible.Contains(c.Id)), hidden => Assert.Null(view.Find(hidden.Id)));

        // Assert: each visible card is listed under its nearest visible ancestor, or is a root
        foreach (var card in view.Cards)
        {
            var ancestor = card.ParentId;
            while (ancestor != null && !visible.Contains(ancestor))
            {
                ancestor = ancestor.Contains('/', StringComparison.Ordinal) ? ancestor[..ancestor.LastIndexOf('/')] : null;
            }

            var listing = ancestor == null ? view.Roots : view.GetChildren(view.Find(ancestor)!);
            Assert.Contains(card.Id, listing.Select(c => c.Id));
            Assert.Equal(card.Id, view.Find(card.Id)?.Id);
        }

        // Assert: nothing a visible card shows names a hidden card
        foreach (var card in view.Cards)
        {
            Assert.All(card.ChildIds.Concat(card.SeeAlsoIds), id => Assert.Contains(id, visible));
            Assert.All(view.GetChildren(card).Concat(view.GetSeeAlso(card)), c => Assert.Contains(c.Id, visible));
            Assert.All(LibraryCardLink.Matches(card.Body), m => Assert.Contains(m.Groups["id"].Value, visible));
            view.FormatBody(card, new HelpRenderOptions
            {
                LinkFormatter = link =>
                {
                    Assert.True(link.ExampleName != null || link.CardId == null || visible.Contains(link.CardId), $"'{card.Id}' links the hidden card '{link.CardId}'.");
                    Assert.True(link.ExampleCardId == null || visible.Contains(link.ExampleCardId), $"'{card.Id}' links an example of the hidden card '{link.ExampleCardId}'.");
                    return string.Empty;
                }
            });
            using var json = JsonDocument.Parse(view.Render(card, Json));
            var linked = json.RootElement.GetProperty("children").EnumerateArray()
                .Concat(json.RootElement.GetProperty("seeAlso").EnumerateArray())
                .Select(l => l.GetProperty("id").GetString()!);
            Assert.All(linked, id => Assert.Contains(id, visible));
            Assert.All(LibraryCardLink.Matches(view.Render(card)), m => Assert.Contains(m.Groups["id"].Value, visible));
        }

        // Assert: the index lists exactly the visible cards
        using var index = JsonDocument.Parse(view.RenderIndex(Json));
        Assert.Equal(view.Cards.Select(c => c.Id), index.RootElement.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("id").GetString()));
    }

    /// <summary>
    ///     Test that a card whose parent a view hides is still found by its full path, and that the view lists it as a
    ///     root (with no parent in the view) when no ancestor is visible.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_DefaultFiltered_VisibleCardBelowAHiddenRoot_IsARootFoundByItsPath()
    {
        // Arrange: the style of each drawing format is a design card; its format root is not
        var view = CanvasNetHelp.Catalog.Filter([HelpTags.Design]);

        // Act / Assert: the root is hidden, and its style card is a root found by path and alias of the root
        Assert.Null(view.Find("mermaid"));
        Assert.Equal("mermaid/style", view.Find("mermaid/style")?.Id);
        Assert.Equal("mermaid/style", view.Find("mmd style")?.Id);
        Assert.Contains("mermaid/style", view.Roots.Select(c => c.Id));
        Assert.Null(view.Find("mermaid/style")!.ParentId);
    }

    /// <summary>
    ///     Test that reading the library catalog from several threads at once gives the results of a single thread:
    ///     finding, filtering, and rendering.
    /// </summary>
    [Fact]
    public void CanvasNetHelp_Default_ConcurrentReads_GiveTheSameResults()
    {
        // Arrange: the results of one thread
        var catalog = CanvasNetHelp.Catalog;
        var ids = catalog.Cards.Select(c => c.Id).ToList();
        var expected = ids.Select(id => catalog.Render(catalog.Find(id)!)).ToList();
        var expectedDesign = catalog.Filter([HelpTags.Design]).RenderIndex();

        // Act: the same reads from several threads
        var texts = new string[ids.Count];
        var design = new string[8];
        Parallel.For(0, ids.Count, i => texts[i] = CanvasNetHelp.Catalog.Render(CanvasNetHelp.Catalog.Find(ids[i])!));
        Parallel.For(0, design.Length, i => design[i] = CanvasNetHelp.Catalog.Filter([HelpTags.Design]).RenderIndex());

        // Assert
        Assert.Equal(expected, texts);
        Assert.All(design, text => Assert.Equal(expectedDesign, text));
    }
}

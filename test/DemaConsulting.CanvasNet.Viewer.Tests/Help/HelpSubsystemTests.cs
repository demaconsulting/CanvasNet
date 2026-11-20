using System.Text.Json;
using DemaConsulting.CanvasNet.Viewer.Help;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Subsystem tests of the Help subsystem: the front-matter parser and the help catalog working together on the
///     embedded library cards and on a host's extension cards.
/// </summary>
public class HelpSubsystemTests
{
    /// <summary>
    ///     The overlay a host adds to the library card <c>limits</c>: a related card and a section that links a library
    ///     card and the host's own card.
    /// </summary>
    private const string LimitsOverlay =
        "---\nextends: limits\nsee-also:\n  - widgets\n---\n\n## Host limits\n\nSee [[style]] and [[widgets]].";

    /// <summary>
    ///     A host's own card, a new root of the extended catalog.
    /// </summary>
    private const string WidgetsCard =
        "---\ntitle: Widgets\nsummary: The widgets of the host.\ntags:\n  - hosting\n---\n\nThe widgets follow [[style]].";

    /// <summary>
    ///     Test that every embedded library card is parsed, resolves by its path, and renders as text and as JSON.
    /// </summary>
    [Fact]
    public void HelpSubsystem_DefaultCatalog_EveryCardResolvesByItsPathAndRenders()
    {
        // Arrange
        var catalog = HelpCatalog.Default;

        // Act and assert: every card, through its path written with spaces and in upper case
        Assert.NotEmpty(catalog.Cards);
        foreach (var card in catalog.Cards)
        {
            Assert.Same(card, catalog.Find(card.Id.Replace('/', ' ').ToUpperInvariant()));

            var text = catalog.RenderText(card);
            Assert.StartsWith($"# {card.Title}\n", text, StringComparison.Ordinal);
            Assert.Contains(card.Markdown, text, StringComparison.Ordinal);
            Assert.DoesNotContain("{{", text, StringComparison.Ordinal);

            using var json = JsonDocument.Parse(catalog.RenderJson(card));
            Assert.Equal(card.Id, json.RootElement.GetProperty("id").GetString());
            Assert.Equal(card.Tags, json.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()));
        }
    }

    /// <summary>
    ///     Test that a filtered view of the library hides the cards with an excluded tag everywhere a reader could meet
    ///     them: the roots, the cards, the lookup, the related cards, and the in-text links.
    /// </summary>
    [Fact]
    public void HelpSubsystem_FilteredView_HidesExcludedCardsFromTreeLookupAndLinks()
    {
        // Act
        var view = HelpCatalog.Default.Filter(null, [HelpTags.FileAccess, HelpTags.Hosting]);

        // Assert: no visible card carries an excluded tag, and the hidden cards are not found
        Assert.NotEmpty(view.Cards);
        Assert.DoesNotContain(view.Cards, c => c.Tags.Contains(HelpTags.FileAccess) || c.Tags.Contains(HelpTags.Hosting));
        Assert.Null(view.Find("files"));
        Assert.Null(view.Find("svg/options"));
        Assert.NotNull(view.Find("svg/style"));

        // Assert: nothing visible points at a hidden card
        var visible = view.Cards.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var card in view.Cards)
        {
            Assert.All(card.ChildIds.Concat(card.SeeAlsoIds), id => Assert.Contains(id, visible));
            Assert.DoesNotContain("the card `files`", card.Markdown, StringComparison.Ordinal);
            view.FormatMarkdown(card, link =>
            {
                Assert.Contains(link.CardId ?? card.Id, visible);
                return string.Empty;
            });
        }
    }

    /// <summary>
    ///     Test that a host's overlay and own card are parsed and layered on the library cards, that the host writes the
    ///     links of the merged text, and that the library catalog does not change.
    /// </summary>
    [Fact]
    public void HelpSubsystem_Extend_HostCardsAreLayeredOnTheLibraryCards()
    {
        // Act
        var merged = HelpCatalog.Default.ExtendWith(
            [("limits.md", LimitsOverlay), ("widgets.md", WidgetsCard)],
            new Dictionary<string, string>(),
            null);

        // Assert: the host card is a new root after the library roots, and the overlay is appended to the library card
        Assert.Equal("widgets", merged.Roots[^1].Id);
        var limits = merged.Find("limits")!;
        Assert.StartsWith(HelpCatalog.Default.Find("limits")!.Markdown, limits.Markdown, StringComparison.Ordinal);
        Assert.EndsWith("## Host limits\n\nSee the card `style` and the card `widgets`.", limits.Markdown, StringComparison.Ordinal);
        Assert.Contains("widgets", limits.SeeAlsoIds);

        // Assert: the host writes the visible links in its own words
        var hostText = merged.FormatMarkdown(limits, link => $"<{link.CardId}>");
        Assert.EndsWith("See <style> and <widgets>.", hostText, StringComparison.Ordinal);

        // Assert: hiding the host card turns the link to it into its title
        var view = merged.Filter(null, [HelpTags.Hosting]);
        Assert.EndsWith("See the card `style` and Widgets.", view.Find("limits")!.Markdown, StringComparison.Ordinal);

        // Assert: the library catalog is unchanged
        Assert.Null(HelpCatalog.Default.Find("widgets"));
        Assert.DoesNotContain("Host limits", HelpCatalog.Default.Find("limits")!.Markdown, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a malformed host card fails the extension with the file and the line of the problem, from the
    ///     front matter and from the body.
    /// </summary>
    [Fact]
    public void HelpSubsystem_Extend_MalformedHostCard_FailsWithTheFileAndTheLine()
    {
        // Arrange
        var empty = new Dictionary<string, string>();

        // Act
        var frontMatter = Assert.Throws<InvalidOperationException>(() =>
            HelpCatalog.Default.ExtendWith([("widgets.md", "---\ntitle:\tWidgets\n---\n")], empty, null));
        var link = Assert.Throws<InvalidOperationException>(() =>
            HelpCatalog.Default.ExtendWith([("widgets.md", WidgetsCard.Replace("[[style]]", "[[nothing]]", StringComparison.Ordinal))], empty, null));

        // Assert
        Assert.StartsWith("Help card 'widgets.md', line 2:", frontMatter.Message, StringComparison.Ordinal);
        Assert.StartsWith("Help card 'widgets.md', line 8:", link.Message, StringComparison.Ordinal);
        Assert.Contains("[[nothing]]", link.Message, StringComparison.Ordinal);
    }
}

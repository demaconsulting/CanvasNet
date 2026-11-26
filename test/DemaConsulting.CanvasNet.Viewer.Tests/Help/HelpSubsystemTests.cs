using System.Text.Json;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Loading;
using DemaConsulting.HelpCards.Presentation;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Subsystem tests of the Help subsystem: the library cards of CanvasNetHelp on DemaConsulting.HelpCards, alone,
///     filtered, and with a host's cards layered on them.
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
        var catalog = CanvasNetHelp.Catalog;

        // Act and assert: every card, through its path written with spaces and in upper case
        Assert.NotEmpty(catalog.Cards);
        foreach (var card in catalog.Cards)
        {
            Assert.Same(card, catalog.Find(card.Id.Replace('/', ' ').ToUpperInvariant()));

            var text = catalog.Render(card);
            Assert.StartsWith($"# {card.Title}\n", text, StringComparison.Ordinal);
            Assert.Contains(card.Body, text, StringComparison.Ordinal);
            Assert.DoesNotContain("{{", text, StringComparison.Ordinal);

            using var json = JsonDocument.Parse(catalog.Render(card, new HelpRenderOptions { Format = HelpFormat.Json }));
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
        var view = CanvasNetHelp.Catalog.Filter(null, [HelpTags.FileAccess, HelpTags.Hosting]);

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
            Assert.DoesNotContain("the card `files`", card.Body, StringComparison.Ordinal);
            view.FormatBody(card, new HelpRenderOptions
            {
                LinkFormatter = link =>
                {
                    Assert.Contains(link.ExampleCardId ?? link.CardId ?? card.Id, visible);
                    return string.Empty;
                }
            });
        }
    }

    /// <summary>
    ///     Test that a host's overlay and own card, in a source that imports the library source, are layered on the
    ///     library cards, that the host writes the links of the merged text, and that the library catalog does not change.
    /// </summary>
    [Fact]
    public void HelpSubsystem_HostSource_IsLayeredOnTheLibraryCards()
    {
        // Act
        var merged = HelpCatalog.Create(builder => CanvasNetHelp.Configure(builder).AddSource(HostSource(WidgetsCard)));

        // Assert: the host card is a new root after the library roots, and the overlay is appended to the library card
        Assert.Equal("widgets", merged.Roots[^1].Id);
        var limits = merged.Find("limits")!;
        Assert.StartsWith(CanvasNetHelp.Catalog.Find("limits")!.Body, limits.Body, StringComparison.Ordinal);
        Assert.EndsWith("## Host limits\n\nSee the card `style` and the card `widgets`.", limits.Body, StringComparison.Ordinal);
        Assert.Contains("widgets", limits.SeeAlsoIds);

        // Assert: the host writes the visible links in its own words
        var hostText = merged.FormatBody(limits, new HelpRenderOptions { LinkFormatter = link => $"<{link.CardId}>" });
        Assert.EndsWith("See <style> and <widgets>.", hostText, StringComparison.Ordinal);

        // Assert: hiding the host card turns the link to it into plain text
        var view = merged.Filter(null, [HelpTags.Hosting]);
        Assert.DoesNotContain("`widgets`", view.Find("limits")!.Body, StringComparison.Ordinal);

        // Assert: the library catalog is unchanged
        Assert.Null(CanvasNetHelp.Catalog.Find("widgets"));
        Assert.DoesNotContain("Host limits", CanvasNetHelp.Catalog.Find("limits")!.Body, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a host card that links a card that does not exist fails the catalog with the file and the line of
    ///     the link.
    /// </summary>
    [Fact]
    public void HelpSubsystem_HostSource_BrokenLink_FailsWithTheFileAndTheLine()
    {
        // Arrange
        var broken = WidgetsCard.Replace("[[style]]", "[[nothing]]", StringComparison.Ordinal);

        // Act
        var exception = Assert.Throws<HelpCatalogException>(() =>
            HelpCatalog.Create(builder => CanvasNetHelp.Configure(builder).AddSource(HostSource(broken))));

        // Assert
        var problem = Assert.Single(exception.Problems);
        Assert.Equal("widgets.md", problem.File);
        Assert.Equal(8, problem.Line);
        Assert.Contains("nothing", problem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Creates the source of a host: the overlay of <c>limits</c> and a widgets card, importing the library source.
    /// </summary>
    /// <param name="widgets">The text of the widgets card.</param>
    /// <returns>The source.</returns>
    private static HelpSource HostSource(string widgets)
    {
        return HelpSource.FromFiles(
                new Dictionary<string, string> { ["limits.md"] = LimitsOverlay, ["widgets.md"] = widgets },
                "host")
            .Imports(CanvasNetHelp.Source);
    }
}

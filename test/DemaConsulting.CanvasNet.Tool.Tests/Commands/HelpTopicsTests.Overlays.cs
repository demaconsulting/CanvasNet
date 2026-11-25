using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Tool.Commands;
using HelpCatalog = DemaConsulting.CanvasNet.Viewer.Help.HelpCatalog;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the tool's layer of the help: its own cards and its overlays of library cards, how they merge with
///     the library's cards, and how the links of the merged cards read on the command line.
/// </summary>
public partial class HelpTopicsTests
{
    /// <summary>
    ///     The roots of the cards only the tool has.
    /// </summary>
    private static readonly string[] ToolRoots = ["json", "pandoc"];

    /// <summary>
    ///     Matches a link of a card body that names a card or an example (<c>[[a/b]]</c>, <c>[[a/b#name]]</c>,
    ///     <c>[[#name]]</c>).
    /// </summary>
    private static readonly Regex LinkMarkup = new(@"\[\[(?<id>[a-z0-9/-]*)(?:#[a-z0-9-]+)?\]\]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Matches the diagnostic identifiers a card may cite.
    /// </summary>
    private static readonly Regex DiagnosticIdentifier =
        new(@"\b(?:scad|vl|mermaid|plantuml|mathml|pptx|vsdx|pdf|svg|image-fragment|image-page|image-access)-[A-Za-z0-9<>-]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Test that every embedded card file of the tool is an overlay that extends the library card at its own path, or
    ///     a card of a root only the tool has, and that there is at least one of each.
    /// </summary>
    [Fact]
    public void HelpTopics_ToolResources_AreOverlaysOfLibraryCardsOrToolCards()
    {
        // Arrange: the tool's card files
        var files = ToolCardFiles();

        // Act: the overlays and the other cards
        var overlays = files.Where(f => f.Extends != null).ToList();
        var toolCards = files.Where(f => f.Extends == null).ToList();

        // Assert: each overlay extends the library card at its own path, and each other card is under a tool root
        Assert.NotEmpty(overlays);
        Assert.NotEmpty(toolCards);
        Assert.All(overlays, o =>
        {
            Assert.Equal(o.Id, o.Extends);
            Assert.Equal(o.Id, HelpCatalog.Default.Find(o.Id)?.Id);
        });
        Assert.All(toolCards, c =>
        {
            Assert.Contains(c.Id.Split('/')[0], ToolRoots);
            Assert.Null(HelpCatalog.Default.Find(c.Id));
        });
    }

    /// <summary>
    ///     Test that the body of every card an overlay extends is, on the command line, the library card's body with its
    ///     links written as commands, followed by the overlay's body.
    /// </summary>
    [Fact]
    public void HelpTopics_OverlayCards_BodyIsTheLibraryBodyThenTheOverlayBody()
    {
        // Act / Assert: for each overlay, the merged body
        foreach (var overlay in ToolCardFiles().Where(f => f.Extends != null))
        {
            var library = HelpCatalog.Default.FormatMarkdown(HelpCatalog.Default.Find(overlay.Id)!, HelpTopics.FormatLink);
            var merged = HelpTopics.Get(overlay.Id).Body;
            Assert.StartsWith(library, merged, StringComparison.Ordinal);
            var added = merged[library.Length..].Trim('\n');
            Assert.Equal(HelpCardText.Headings(overlay.Body.Trim('\n')), HelpCardText.Headings(added));
            Assert.Equal(overlay.Body.Trim('\n').Length == 0, added.Length == 0);
        }
    }

    /// <summary>
    ///     Test that no link markup survives on the command line: every card's text and JSON write their links as
    ///     <c>canvasnet help</c> commands, never as <c>[[...]]</c> (outside code, where it is literal text such as a
    ///     Mermaid subroutine shape) and never in the library's words.
    /// </summary>
    [Fact]
    public void HelpTopics_CommandLineText_WritesLinksAsHelpCommands()
    {
        // Arrange: the ids of the cards
        var ids = HelpTopics.All.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        // Act / Assert: no link that names a card, or an example, is left in any body
        foreach (var card in HelpTopics.All)
        {
            var text = HelpTopics.RenderText(card);
            Assert.DoesNotContain(LinkMarkup.Matches(card.Body), m => m.Groups["id"].Value.Length == 0 || ids.Contains(m.Groups["id"].Value) || m.Value.Contains('#'));
            Assert.DoesNotContain(LinkMarkup.Matches(HelpTopics.RenderJson(card)), m => ids.Contains(m.Groups["id"].Value) || m.Value.Contains('#'));
            Assert.DoesNotContain("of the card `", text, StringComparison.Ordinal);
        }

        // Assert: a card link and an example link read as the commands that print them
        Assert.Contains("`canvasnet help limits`", HelpCardText.Flatten(HelpTopics.Get("plantuml/diagnostics").Body), StringComparison.Ordinal);
        Assert.Contains("`canvasnet help svg --example embed`", HelpCardText.Flatten(HelpTopics.Get("svg/embedding").Body), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the tool's cards and overlays cite only diagnostic identifiers that the library cards cite too (the
    ///     library tests that each of those exists).
    /// </summary>
    [Fact]
    public void HelpTopics_ToolCards_CiteOnlyDiagnosticIdsTheLibraryCites()
    {
        // Arrange: the text of the library cards
        var library = string.Join("\n", HelpCatalog.Default.Cards.SelectMany(c => c.Examples.Select(e => e.Description).Prepend(c.Markdown).Prepend(c.Summary).Prepend(c.Title)));

        // Act: the identifiers the tool's files cite
        var cited = ToolCardFiles().SelectMany(f => DiagnosticIdentifier.Matches(f.Text).Select(m => m.Value)).Distinct(StringComparer.Ordinal).ToList();

        // Assert
        Assert.All(cited, id => Assert.Contains(id, library, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Gets the tool's embedded card files: the card path, the <c>extends</c> value of an overlay, the body after the
    ///     front matter, and the whole text.
    /// </summary>
    /// <returns>The files, by path.</returns>
    private static List<(string Id, string? Extends, string Body, string Text)> ToolCardFiles()
    {
        var assembly = typeof(HelpTopics).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(HelpTopics.CardResourcePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
                var id = name[HelpTopics.CardResourcePrefix.Length..^HelpTopics.CardExtension.Length].Replace('\\', '/');
                var end = text.IndexOf("\n---\n", 4, StringComparison.Ordinal);
                Assert.True(text.StartsWith("---\n", StringComparison.Ordinal) && end > 0, $"The tool card '{id}' has no front matter.");
                var extends = text[4..end].Split('\n').FirstOrDefault(l => l.StartsWith("extends: ", StringComparison.Ordinal))?["extends: ".Length..].Trim();
                return (id, extends, text[(end + 5)..], text);
            })
            .ToList();
    }
}

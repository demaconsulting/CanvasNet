using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.HelpCards.Catalog;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Reads the library help cards of <see cref="CanvasNetHelp.Catalog"/> for the tests: the cards of a tree, their
///     examples, the flattened text of a card or a tree of cards, and the bullets of a card with their terms.
/// </summary>
internal static class HelpCardText
{
    /// <summary>
    ///     Matches a line break inside a paragraph or a bullet: a line feed between two lines that are not blank.
    /// </summary>
    private static readonly Regex LineBreak =
        new(@"(?<=\S)[ ]*\n[ ]*(?=\S)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Matches the term of a bullet: a leading code span or bold text followed by a colon and a space.
    /// </summary>
    private static readonly Regex TermPattern =
        new(@"^(?:`(?<term>[^`]+)`|\*\*(?<term>[^*]+)\*\*): (?<text>.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     The prefix of the logical names of the embedded files of the library cards.
    /// </summary>
    private const string ResourcePrefix = "DemaConsulting.CanvasNet.Viewer.Help/";

    /// <summary>
    ///     Gets the paths of the files embedded for the library cards: the cards, their example sources, and the file that
    ///     names the source, with <c>/</c> between the folders whatever separator the build used.
    /// </summary>
    /// <returns>The paths below the help folder, in ordinal order.</returns>
    public static IReadOnlyList<string> EmbeddedFiles()
    {
        return typeof(CanvasNetHelp).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(n => n[ResourcePrefix.Length..].Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
    /// <summary>
    ///     Gets a card of the library catalog.
    /// </summary>
    /// <param name="id">The identifier of the card.</param>
    /// <returns>The card.</returns>
    public static HelpCard Card(string id)
    {
        var card = CanvasNetHelp.Catalog.Find(id);
        Assert.True(card != null && card.Id == id, $"The library has no card '{id}'.");
        return card;
    }

    /// <summary>
    ///     Gets a card and every card below it, depth first.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The cards of the tree.</returns>
    public static IEnumerable<HelpCard> Subtree(HelpCard card)
    {
        yield return card;
        foreach (var child in CanvasNetHelp.Catalog.GetChildren(card).SelectMany(Subtree))
        {
            yield return child;
        }
    }

    /// <summary>
    ///     Gets every example of the library with the card it belongs to.
    /// </summary>
    /// <returns>The card and the example, in catalog order.</returns>
    public static IEnumerable<(HelpCard Card, HelpExample Example)> AllExamples() =>
        CanvasNetHelp.Catalog.Cards.SelectMany(c => c.Examples.Select(e => (c, e)));

    /// <summary>
    ///     Gets the examples of a kind, with the card each belongs to.
    /// </summary>
    /// <param name="kind">The kind (<see cref="HelpExample.SourceKind"/> or <see cref="HelpExample.SnippetKind"/>).</param>
    /// <returns>The card and the example, in catalog order.</returns>
    public static IEnumerable<(HelpCard Card, HelpExample Example)> ExamplesOfKind(string kind) =>
        AllExamples().Where(e => e.Example.Kind == kind);

    /// <summary>
    ///     Gets the identifier of the root card of a card.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The first part of its path.</returns>
    public static string RootOf(HelpCard card) => card.Id.Split('/')[0];

    /// <summary>
    ///     Gets the content of an example of the tree of a root card.
    /// </summary>
    /// <param name="root">The identifier of the root card.</param>
    /// <param name="name">The name of the example.</param>
    /// <returns>The content.</returns>
    public static string Example(string root, string name)
    {
        return Assert.Single(Subtree(Card(root)).SelectMany(c => c.Examples), e => e.Name == name).Content;
    }

    /// <summary>
    ///     Saves an example of the tree of a root card into a folder.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <param name="root">The identifier of the root card.</param>
    /// <param name="name">The name of the example.</param>
    /// <param name="file">The file name.</param>
    /// <returns>The path.</returns>
    public static string SaveExample(TempDirectory directory, string root, string name, string file)
    {
        var path = directory.Combine(file);
        File.WriteAllText(path, Example(root, name));
        return path;
    }

    /// <summary>
    ///     Joins the lines of every paragraph and bullet of a text into one line.
    /// </summary>
    /// <param name="text">The Markdown text.</param>
    /// <returns>The text with each line break inside a block replaced by a space.</returns>
    public static string Flatten(string text) => LineBreak.Replace(text, " ");

    /// <summary>
    ///     Gets the flattened text of a card and of every card below it: the bodies and the example descriptions.
    /// </summary>
    /// <param name="id">The identifier of the card.</param>
    /// <returns>The text.</returns>
    public static string TextOf(string id)
    {
        var parts = new List<string>();
        foreach (var card in Subtree(Card(id)))
        {
            parts.Add(Flatten(card.Body));
            parts.AddRange(card.Examples.Select(e => e.Description));
        }

        return string.Join("\n", parts);
    }

    /// <summary>
    ///     Gets the bullets of a card, each joined into one line, optionally only those under one heading.
    /// </summary>
    /// <param name="id">The identifier of the card.</param>
    /// <param name="heading">The text of a <c>##</c> heading, or <see langword="null"/> for every bullet.</param>
    /// <returns>The bullets, without the leading <c>- </c>, in order.</returns>
    public static IReadOnlyList<string> Bullets(string id, string? heading = null)
    {
        var bullets = new List<string>();
        string? current = null;
        var inSection = heading == null;
        var inFence = false;
        foreach (var line in Card(id).Body.Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                Flush();
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                inSection = heading == null || line == $"## {heading}";
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                Flush();
                current = line[2..];
                continue;
            }

            if (current != null && line.StartsWith("  ", StringComparison.Ordinal) && line.Trim().Length > 0)
            {
                current += " " + line.Trim();
                continue;
            }

            Flush();
        }

        Flush();
        return bullets;

        void Flush()
        {
            if (current != null && inSection)
            {
                bullets.Add(current);
            }

            current = null;
        }
    }

    /// <summary>
    ///     Gets the bullets of a card that have a term, as term and text.
    /// </summary>
    /// <param name="id">The identifier of the card.</param>
    /// <param name="heading">The text of a <c>##</c> heading, or <see langword="null"/> for every bullet.</param>
    /// <returns>The term and the text of each bullet that has a term, in order.</returns>
    public static IReadOnlyList<(string Term, string Text)> Items(string id, string? heading = null)
    {
        return Bullets(id, heading)
            .Select(b => TermPattern.Match(b))
            .Where(m => m.Success)
            .Select(m => (m.Groups["term"].Value, m.Groups["text"].Value))
            .ToList();
    }

    /// <summary>
    ///     Gets the text of the single bullet of a card with a term.
    /// </summary>
    /// <param name="id">The identifier of the card.</param>
    /// <param name="term">The term.</param>
    /// <returns>The text of the bullet.</returns>
    public static string Item(string id, string term)
    {
        return Assert.Single(Items(id), i => i.Term == term).Text;
    }

    /// <summary>
    ///     Gets the <c>##</c> headings of a card body, in order.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The heading texts.</returns>
    public static IReadOnlyList<string> Headings(string body)
    {
        return body.Split('\n').Where(l => l.StartsWith("## ", StringComparison.Ordinal)).Select(l => l[3..]).ToList();
    }
}

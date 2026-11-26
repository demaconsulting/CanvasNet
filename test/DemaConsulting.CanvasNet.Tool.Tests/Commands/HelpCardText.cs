using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.HelpCards.Catalog;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Reads the Markdown of the help cards for the tests: the flattened text of a card or a tree of cards, and the
///     bullets of a card with their terms.
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
    ///     Joins the lines of every paragraph and bullet of a text into one line.
    /// </summary>
    /// <param name="text">The Markdown text.</param>
    /// <returns>The text with each line break inside a block replaced by a space.</returns>
    public static string Flatten(string text) => LineBreak.Replace(text, " ");

    /// <summary>
    ///     Gets the flattened text of a card and of every card below it: the bodies, the example descriptions, and the
    ///     command lines.
    /// </summary>
    /// <param name="id">The identifier of the card.</param>
    /// <returns>The text.</returns>
    public static string TextOf(string id)
    {
        var parts = new List<string>();
        foreach (var card in HelpView.Subtree(HelpView.Get(id)))
        {
            parts.Add(Flatten(card.CommandBody));
            parts.AddRange(card.Examples.Select(e => e.Description));
            parts.AddRange(card.Examples.Where(e => e.Kind == HelpExample.CommandKind).Select(e => e.Content));
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
        foreach (var line in HelpView.Get(id).CommandBody.Split('\n'))
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

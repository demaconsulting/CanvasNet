using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     The tree data of the help catalog and the neutral in-text links of the card bodies.
/// </summary>
/// <remarks>
///     <para>
///         A body links to a card with <c>[[a/b]]</c>, to an example of a card or of a card below it with
///         <c>[[a/b#name]]</c>, and to an example of the containing card or of a card below it with <c>[[#name]]</c>.
///         Identifiers are canonical (no aliases) and a link is on one line. Links are recognized only outside fenced
///         code blocks and inline code spans, because diagram syntax such as Mermaid's <c>[[fork]]</c> uses the same
///         brackets; outside code, text that starts like a link (<c>[[</c> followed by a letter, a digit, or
///         <c>#</c>) but is not one is an error, so a typo cannot silently become literal text.
///     </para>
///     <para>
///         Each link is validated when the card is loaded: the card must exist, and the example must belong to the
///         card or to a card below it. The library cards are validated against the library alone, so a library card
///         can never point at a card that only a host adds.
///     </para>
/// </remarks>
public sealed partial class HelpCatalog
{
    /// <summary>
    ///     Matches a link: an optional canonical card identifier, then an optional <c>#</c> and example name.
    /// </summary>
    private static readonly Regex LinkPattern = new(
        @"\[\[(?<card>[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9]+(?:-[a-z0-9]+)*)*)?(?:#(?<example>[a-z0-9]+(?:-[a-z0-9]+)*))?\]\]",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Matches text that starts like a link, to report a link that does not parse.
    /// </summary>
    private static readonly Regex LinkStartPattern = new(
        @"\[\[[a-z0-9#]",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Parses the links of a body and resolves them against a tree.
    /// </summary>
    /// <param name="body">The body, tokens replaced.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <param name="bodyLine">The line of the file the body starts on, for messages.</param>
    /// <param name="containingId">The card the body belongs to, which <c>[[#name]]</c> refers to.</param>
    /// <param name="nodes">Every card by identifier.</param>
    /// <returns>The body as text parts and links.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a link does not parse, names no card, or names an example that is not in the card's tree.
    /// </exception>
    private static List<BodyPart> ParseBody(
        string body,
        string fileName,
        int bodyLine,
        string containingId,
        Dictionary<string, HelpNode> nodes)
    {
        var code = FindCode(body);
        int LineOf(int index) => bodyLine + body.AsSpan(0, index).Count('\n');

        // Split the text at every link outside code, resolving each
        var parts = new List<BodyPart>();
        var linkStarts = new HashSet<int>();
        var last = 0;
        foreach (Match match in LinkPattern.Matches(body))
        {
            var card = match.Groups["card"];
            var example = match.Groups["example"];
            if (code[match.Index] || (!card.Success && !example.Success))
            {
                continue;
            }

            var link = ResolveLink(
                card.Success ? card.Value : null,
                example.Success ? example.Value : null,
                containingId,
                nodes,
                reason => HelpFrontMatter.Error(fileName, LineOf(match.Index), $"the link '{match.Value}' {reason}"));
            parts.Add(new BodyPart(body[last..match.Index], null));
            parts.Add(new BodyPart(match.Value, link));
            linkStarts.Add(match.Index);
            last = match.Index + match.Length;
        }

        parts.Add(new BodyPart(body[last..], null));

        // Anything else outside code that starts like a link is a typo
        foreach (var start in LinkStartPattern.Matches(body).Select(m => m.Index))
        {
            if (!code[start] && !linkStarts.Contains(start))
            {
                throw HelpFrontMatter.Error(
                    fileName,
                    LineOf(start),
                    "text that starts like a link is not one; write [[a/b]], [[a/b#example]], or [[#example]] with "
                    + "canonical lower case names, or put the text in a code span");
            }
        }

        return parts.Where(p => p.Text.Length > 0).ToList();
    }

    /// <summary>
    ///     Resolves a link to its card and, for an example, the card that owns the example.
    /// </summary>
    /// <param name="cardId">The card identifier as written, or <see langword="null"/> for <c>[[#name]]</c>.</param>
    /// <param name="exampleName">The example name, or <see langword="null"/> for a card link.</param>
    /// <param name="containingId">The card that holds the link.</param>
    /// <param name="nodes">Every card by identifier.</param>
    /// <param name="error">Creates the exception for a reason.</param>
    /// <returns>The resolved link.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the card or the example does not exist.</exception>
    private static LinkTarget ResolveLink(
        string? cardId,
        string? exampleName,
        string containingId,
        Dictionary<string, HelpNode> nodes,
        Func<string, InvalidOperationException> error)
    {
        if (cardId != null && !nodes.ContainsKey(cardId))
        {
            throw error($"names no card '{cardId}'");
        }

        if (exampleName == null)
        {
            return new LinkTarget(cardId, null, null, containingId);
        }

        // An example is found in the card's tree, depth first, as the command line finds it
        var scope = cardId ?? containingId;
        HelpNode? Search(HelpNode node) =>
            node.Examples.Any(e => e.Name == exampleName)
                ? node
                : node.ChildIds.Select(id => Search(nodes[id])).FirstOrDefault(found => found != null);

        var owner = Search(nodes[scope]) ?? throw error($"names no example '{exampleName}' of the card '{scope}' or a card below it");
        return new LinkTarget(cardId, exampleName, owner.Id, containingId);
    }

    /// <summary>
    ///     Finds the characters of a body that are code: fenced code blocks and inline code spans.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>One flag per character, set for code.</returns>
    /// <remarks>
    ///     A fence is a line that starts (after spaces) with three or more backticks and ends at a line that starts with
    ///     at least as many. A code span opens with a run of backticks and closes with the next run of the same length in
    ///     the same paragraph (a span may cross a line break); a run without a match is literal text.
    /// </remarks>
    private static bool[] FindCode(string body)
    {
        var code = new bool[body.Length];
        string? fence = null;
        var paragraphStart = -1;
        var paragraphEnd = -1;
        var start = 0;
        while (start <= body.Length)
        {
            // One line at a time, with the offset of its end (the line feed or the end of the body)
            var end = body.IndexOf('\n', start);
            end = end < 0 ? body.Length : end;
            var trimmed = body[start..end].TrimStart(' ');
            if (fence != null || trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                // A fence line, or a line inside a fenced block: all code, and it ends any paragraph
                MarkSpans(body, paragraphStart, paragraphEnd, code);
                paragraphStart = -1;
                if (fence == null)
                {
                    fence = trimmed[..(trimmed.Length - trimmed.TrimStart('`').Length)];
                }
                else if (trimmed.StartsWith(fence, StringComparison.Ordinal))
                {
                    fence = null;
                }

                Array.Fill(code, true, start, end - start);
            }
            else if (trimmed.Length == 0)
            {
                // A blank line ends a paragraph
                MarkSpans(body, paragraphStart, paragraphEnd, code);
                paragraphStart = -1;
            }
            else
            {
                paragraphStart = paragraphStart < 0 ? start : paragraphStart;
                paragraphEnd = end;
            }

            start = end + 1;
        }

        MarkSpans(body, paragraphStart, paragraphEnd, code);
        return code;
    }

    /// <summary>
    ///     Marks the inline code spans of one paragraph as code.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="start">The offset of the paragraph, or a negative number when there is no paragraph.</param>
    /// <param name="end">The offset after the paragraph.</param>
    /// <param name="code">The flags to set.</param>
    private static void MarkSpans(string body, int start, int end, bool[] code)
    {
        if (start < 0)
        {
            return;
        }

        int RunAt(int index)
        {
            var run = 0;
            while (index + run < end && body[index + run] == '`')
            {
                run++;
            }

            return run;
        }

        var i = start;
        while (i < end)
        {
            if (body[i] != '`')
            {
                i++;
                continue;
            }

            // Find the next run of the same length; without one the run is literal
            var length = RunAt(i);
            var close = i + length;
            while (close < end && (body[close] != '`' || RunAt(close) != length))
            {
                close += body[close] == '`' ? RunAt(close) : 1;
            }

            if (close < end)
            {
                Array.Fill(code, true, i, close + length - i);
                i = close + length;
            }
            else
            {
                i += length;
            }
        }
    }

    /// <summary>
    ///     Writes the body of a card for this view.
    /// </summary>
    /// <param name="node">The card.</param>
    /// <param name="formatLink">The host's link writer, or <see langword="null"/> for the library form.</param>
    /// <returns>The body.</returns>
    private string WriteBody(HelpNode node, Func<HelpLink, string>? formatLink)
    {
        var builder = new StringBuilder();
        foreach (var part in node.Body)
        {
            var link = part.Link;
            if (link == null)
            {
                builder.Append(part.Text);
                continue;
            }

            // A link to a hidden card or example is plain text, so a reader is never sent where it cannot go
            var target = link.ExampleName == null ? link.CardId! : link.OwnerId!;
            if (!IsVisible(target))
            {
                builder.Append(link.ExampleName == null ? _data.ById[target].Title : "an example");
                continue;
            }

            builder.Append(formatLink != null
                ? formatLink(new HelpLink(link.CardId, link.ExampleName, link.OwnerId))
                : LibraryLinkText(link));
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Writes a visible link in the library form.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>"the card `a/b`", "the example `name`", or "the example `name` of the card `a/b`".</returns>
    private static string LibraryLinkText(LinkTarget link)
    {
        if (link.ExampleName == null)
        {
            return $"the card `{link.CardId}`";
        }

        return link.CardId == null && link.OwnerId == link.ContainingId
            ? $"the example `{link.ExampleName}`"
            : $"the example `{link.ExampleName}` of the card `{link.OwnerId}`";
    }

    /// <summary>
    ///     One part of a card body: literal text, or a link with the text it was written as.
    /// </summary>
    /// <param name="Text">The text as written.</param>
    /// <param name="Link">The resolved link, or <see langword="null"/> for literal text.</param>
    private sealed record BodyPart(string Text, LinkTarget? Link);

    /// <summary>
    ///     A resolved in-text link.
    /// </summary>
    /// <param name="CardId">The card as written, or <see langword="null"/> for <c>[[#name]]</c>.</param>
    /// <param name="ExampleName">The example, or <see langword="null"/> for a card link.</param>
    /// <param name="OwnerId">The card that owns the example, or <see langword="null"/> for a card link.</param>
    /// <param name="ContainingId">The card whose body holds the link.</param>
    private sealed record LinkTarget(string? CardId, string? ExampleName, string? OwnerId, string ContainingId);

    /// <summary>
    ///     One card of the tree, merged with its extensions, before any filter: the data every view shares.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <remarks>The properties are set while the catalog is built and never change after.</remarks>
    private sealed class HelpNode(string id)
    {
        /// <summary>
        ///     Gets the identifier: the path.
        /// </summary>
        public string Id { get; } = id;

        /// <summary>
        ///     Gets the name among the siblings: the last segment of the path.
        /// </summary>
        public string Name => Id[(Id.LastIndexOf('/') + 1)..];

        /// <summary>
        ///     Gets the identifier of the parent, or <see langword="null"/> for a root.
        /// </summary>
        public string? ParentId => Id.Contains('/', StringComparison.Ordinal) ? Id[..Id.LastIndexOf('/')] : null;

        /// <summary>
        ///     Gets the identifier of the root of the card's tree: the first segment of the path.
        /// </summary>
        public string RootId => Id.Split('/')[0];

        /// <summary>
        ///     Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the summary.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the tags.
        /// </summary>
        public IReadOnlyList<string> Tags { get; set; } = [];

        /// <summary>
        ///     Gets or sets the aliases.
        /// </summary>
        public IReadOnlyList<string> Aliases { get; set; } = [];

        /// <summary>
        ///     Gets or sets the identifiers of the children, in order.
        /// </summary>
        public IReadOnlyList<string> ChildIds { get; set; } = [];

        /// <summary>
        ///     Gets or sets the identifiers of the related cards, in order.
        /// </summary>
        public IReadOnlyList<string> SeeAlso { get; set; } = [];

        /// <summary>
        ///     Gets or sets the examples, in order.
        /// </summary>
        public IReadOnlyList<HelpExample> Examples { get; set; } = [];

        /// <summary>
        ///     Gets or sets the body: text and resolved links.
        /// </summary>
        public IReadOnlyList<BodyPart> Body { get; set; } = [];

        /// <summary>
        ///     Gets whether the card has a name or alias, ignoring case.
        /// </summary>
        /// <param name="name">The name or alias.</param>
        /// <returns><see langword="true"/> when it matches.</returns>
        public bool IsNamed(string name)
        {
            return string.Equals(Name, name, StringComparison.OrdinalIgnoreCase)
                   || Aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Copies the card, so an extension can change the copy without changing the catalog it extends.
        /// </summary>
        /// <returns>The copy.</returns>
        public HelpNode Copy()
        {
            return (HelpNode)MemberwiseClone();
        }
    }

    /// <summary>
    ///     The tree of a catalog: every card, the roots in order, the cards depth first, and the token values.
    /// </summary>
    private sealed class HelpData
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="HelpData"/> class.
        /// </summary>
        /// <param name="byId">Every card by identifier.</param>
        /// <param name="rootIds">The identifiers of the roots, in order.</param>
        /// <param name="tokens">The token values.</param>
        public HelpData(
            Dictionary<string, HelpNode> byId,
            IReadOnlyList<string> rootIds,
            IReadOnlyDictionary<string, string> tokens)
        {
            ById = byId;
            RootIds = rootIds.ToList().AsReadOnly();
            Tokens = tokens;

            // List the cards depth first, from the roots in order
            var order = new List<HelpNode>();
            void Visit(HelpNode node)
            {
                order.Add(node);
                foreach (var child in node.ChildIds)
                {
                    Visit(byId[child]);
                }
            }

            foreach (var root in RootIds)
            {
                Visit(byId[root]);
            }

            Order = order;
        }

        /// <summary>
        ///     Gets every card by identifier.
        /// </summary>
        public IReadOnlyDictionary<string, HelpNode> ById { get; }

        /// <summary>
        ///     Gets the identifiers of the root cards, in order.
        /// </summary>
        public IReadOnlyList<string> RootIds { get; }

        /// <summary>
        ///     Gets every card, depth first.
        /// </summary>
        public IReadOnlyList<HelpNode> Order { get; }

        /// <summary>
        ///     Gets the token values.
        /// </summary>
        public IReadOnlyDictionary<string, string> Tokens { get; }
    }
}

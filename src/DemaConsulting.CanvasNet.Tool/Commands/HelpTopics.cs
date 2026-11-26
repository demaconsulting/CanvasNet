using System.Globalization;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Help;
using LibraryCard = DemaConsulting.CanvasNet.Viewer.Help.HelpCard;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     One help card as the command line shows it: a short Markdown text about one subject, a node of the help tree.
/// </summary>
/// <param name="Id">
///     The identifier: the path of the card in the tree, for example <c>mermaid/gantt</c>.
/// </param>
/// <param name="Aliases">Other names that select the card among its siblings, for example <c>mmd</c>.</param>
/// <param name="Tags">The tags of the card (see <see cref="HelpTags"/>).</param>
/// <param name="Title">The title shown at the top of the card.</param>
/// <param name="Summary">The one-line summary shown in lists and in the See also footers that point at the card.</param>
/// <param name="Body">
///     The Markdown body: the library card's body followed by the tool's additions, with the tokens replaced and every
///     link written as the <c>canvasnet help</c> command that prints its target.
/// </param>
/// <param name="ChildIds">The identifiers of the cards below this one, in order.</param>
/// <param name="SeeAlso">The identifiers of related cards, in order.</param>
/// <param name="Examples">The examples of the card, in order: the library's, then the tool's.</param>
internal sealed record HelpCard(
    string Id,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Tags,
    string Title,
    string Summary,
    string Body,
    IReadOnlyList<string> ChildIds,
    IReadOnlyList<string> SeeAlso,
    IReadOnlyList<HelpExample> Examples)
{
    /// <summary>
    ///     Gets the name of the card among its siblings: the last segment of the identifier.
    /// </summary>
    public string Name => Id[(Id.LastIndexOf('/') + 1)..];

    /// <summary>
    ///     Gets the identifier of the root card of the tree the card is in: the first segment of the identifier.
    /// </summary>
    public string RootId => Id.Split('/')[0];

    /// <summary>
    ///     Gets the identifier of the parent card, or <see langword="null"/> for a root card.
    /// </summary>
    public string? ParentId => Id.Contains('/', StringComparison.Ordinal) ? Id[..Id.LastIndexOf('/')] : null;

    /// <summary>
    ///     Gets the depth of the card: 0 for a root card.
    /// </summary>
    public int Depth => Id.Count(c => c == '/');

    /// <summary>
    ///     Gets the help path of the card: its identifier with spaces between the segments, for example
    ///     <c>mermaid gantt</c>.
    /// </summary>
    public string Path => Id.Replace('/', ' ');

    /// <summary>
    ///     Gets the command that prints the card, for example <c>canvasnet help mermaid gantt</c>.
    /// </summary>
    public string Command => $"canvasnet help {Path}";
}

/// <summary>
///     The tree of help cards (formats and cross-cutting subjects) that the <c>help</c> command prints, with their
///     embedded examples: the library's help catalog with the tool's command-line layer on top.
/// </summary>
/// <remarks>
///     <para>
///         The drawing and format cards live in the Viewer library (<see cref="HelpCatalog.Default"/>), so a program
///         that uses the library gets the same knowledge. The tool adds its own cards and its command-line sections
///         with <see cref="HelpCatalog.Extend"/>: each embedded Markdown file of this assembly
///         (<c>DemaConsulting.CanvasNet.Tool.Help/&lt;path&gt;.md</c>) is either a tool-only card (the <c>json</c> and
///         <c>pandoc</c> trees) or an overlay (<c>extends: &lt;path&gt;</c>) that appends the command-line flags, the
///         command examples, and related tool cards to the library card at the same path. The combined catalog is read
///         once, on first use, and is immutable afterwards, so it is safe to use from several threads.
///     </para>
///     <para>
///         The in-text links of a card (<c>[[a/b]]</c>, <c>[[a/b#name]]</c>, <c>[[#name]]</c>) are written as the
///         command that prints the target: <c>canvasnet help a b</c>, <c>canvasnet help a b --example name</c>, and
///         <c>--example name</c>. Loading a malformed card throws <see cref="InvalidOperationException"/> naming the file
///         and the line; the unit tests load every card, so a malformed file fails the build rather than a user.
///     </para>
///     <para>
///         The rendering of a card (text and JSON) is in <c>HelpTopics.Render.cs</c>.
///     </para>
/// </remarks>
internal static partial class HelpTopics
{
    /// <summary>
    ///     The prefix of the logical names of the tool's embedded help resources.
    /// </summary>
    internal const string ResourcePrefix = "DemaConsulting.CanvasNet.Tool.Help";

    /// <summary>
    ///     The prefix of the logical names of the tool's embedded card files; the rest of the name is the path of the
    ///     file.
    /// </summary>
    internal const string CardResourcePrefix = ResourcePrefix + "/";

    /// <summary>
    ///     The prefix of the logical names of the tool's embedded example sources.
    /// </summary>
    internal const string ExampleResourcePrefix = ResourcePrefix + ".Examples.";

    /// <summary>
    ///     The extension of a card file.
    /// </summary>
    internal const string CardExtension = ".md";

    /// <summary>
    ///     The kind of an example whose content is a source file.
    /// </summary>
    internal const string SourceKind = HelpExample.SourceKind;

    /// <summary>
    ///     The kind of an example whose content is a command line.
    /// </summary>
    internal const string CommandKind = HelpExample.CommandKind;

    /// <summary>
    ///     The kind of an example whose content is text that refers to other files, so it is shown but never rendered
    ///     alone (the render drift test skips it; a dedicated test renders it with its companion files).
    /// </summary>
    internal const string SnippetKind = HelpExample.SnippetKind;

    /// <summary>
    ///     The catalog, loaded on first use.
    /// </summary>
    private static readonly Lazy<CardCatalog> Catalog = new(LoadAll);

    /// <summary>
    ///     Gets the identifiers of the root cards (the help topics), in the order they are listed.
    /// </summary>
    /// <remarks>
    ///     The loader proves that the root cards of the combined catalog (the library roots and the tool's own) are
    ///     exactly this set, so a topic cannot be added or removed without this list (and the tests that go with it)
    ///     being updated. The tool keeps its own order, so the index lists the tool's topics among the library's.
    /// </remarks>
    public static IReadOnlyList<string> TopicNames { get; } =
    [
        "scad", "vega-lite", "mermaid", "plantuml", "mathml", "svg", "pdf", "pptx", "vsdx", "raster",
        "style", "composition", "positions", "json", "errors", "limits", "files", "pandoc"
    ];

    /// <summary>
    ///     Gets the names of the topics that describe one format (and so list supported and unsupported features).
    /// </summary>
    public static IReadOnlyList<string> FormatTopicNames { get; } =
        ["scad", "vega-lite", "mermaid", "plantuml", "mathml", "svg", "pdf", "pptx", "vsdx", "raster"];

    /// <summary>
    ///     Gets the values of the tool's own <c>{{name}}</c> tokens, taken from the constants of the tool; the library
    ///     cards' tokens are filled in by the library catalog.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Tokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["schemaVersion"] = ResultWriter.SchemaVersion.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>
    ///     Gets every card, depth first: each root card of <see cref="TopicNames"/> followed by the cards below it.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an embedded card is missing or malformed.</exception>
    public static IReadOnlyList<HelpCard> All => Catalog.Value.Cards;

    /// <summary>
    ///     Gets the root cards, in <see cref="TopicNames"/> order.
    /// </summary>
    public static IEnumerable<HelpCard> Roots => TopicNames.Select(Get);

    /// <summary>
    ///     Gets a card by its identifier.
    /// </summary>
    /// <param name="id">The identifier, for example <c>mermaid/gantt</c>.</param>
    /// <returns>The card.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when there is no such card.</exception>
    public static HelpCard Get(string id)
    {
        return Catalog.Value.ById[id];
    }

    /// <summary>
    ///     Finds a root card by its name or one of its aliases.
    /// </summary>
    /// <param name="nameOrAlias">The name or alias; matched ignoring case.</param>
    /// <returns>The root card, or <see langword="null"/> when there is none.</returns>
    public static HelpCard? Find(string nameOrAlias)
    {
        return FindAmong(Roots, nameOrAlias);
    }

    /// <summary>
    ///     Gets the cards directly below a card, in order.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The child cards.</returns>
    public static IReadOnlyList<HelpCard> ChildrenOf(HelpCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.ChildIds.Select(Get).ToList();
    }

    /// <summary>
    ///     Enumerates a card and every card below it, depth first.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The card, then its descendants.</returns>
    public static IEnumerable<HelpCard> Subtree(HelpCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return All.Where(c => c.Id == card.Id || c.Id.StartsWith(card.Id + "/", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Resolves a help path to a card: the first segment is a root name or alias, every further segment the name or
    ///     alias of a card below the previous one.
    /// </summary>
    /// <param name="path">The segments; a segment may also hold several segments separated by <c>/</c>.</param>
    /// <returns>The card, or <see langword="null"/> when the first segment is not a root card.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when a further segment names no card below the previous one; the hint
    ///     suggests the closest name and lists the valid ones.
    /// </exception>
    public static HelpCard? Resolve(IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var segments = SplitPath(path);
        if (segments.Count == 0 || Find(segments[0]) is not { } card)
        {
            return null;
        }

        foreach (var segment in segments.Skip(1))
        {
            var children = ChildrenOf(card);
            card = FindAmong(children, segment) ?? throw CreateUnknownCardException(card, children, segment);
        }

        return card;
    }

    /// <summary>
    ///     Splits the segments of a help path at <c>/</c>, dropping empty segments.
    /// </summary>
    /// <param name="path">The segments as typed.</param>
    /// <returns>The segments.</returns>
    public static IReadOnlyList<string> SplitPath(IEnumerable<string> path)
    {
        return path.SelectMany(p => p.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToList();
    }

    /// <summary>
    ///     Enumerates the examples whose content is a source file, with their cards.
    /// </summary>
    /// <returns>Every <c>source</c> example of every card.</returns>
    /// <remarks>
    ///     These are the examples that the tests and the self-test render with <c>--strict</c> to prove that they are
    ///     accepted without a diagnostic.
    /// </remarks>
    public static IEnumerable<(HelpCard Card, HelpExample Example)> SourceExamples()
    {
        return AllExamples().Where(e => e.Example.Kind == SourceKind);
    }

    /// <summary>
    ///     Enumerates the examples whose content is a snippet, with their cards.
    /// </summary>
    /// <returns>Every <c>snippet</c> example of every card.</returns>
    public static IEnumerable<(HelpCard Card, HelpExample Example)> Snippets()
    {
        return AllExamples().Where(e => e.Example.Kind == SnippetKind);
    }

    /// <summary>
    ///     Enumerates every example of every card, with its card.
    /// </summary>
    /// <returns>The examples, depth first.</returns>
    public static IEnumerable<(HelpCard Card, HelpExample Example)> AllExamples()
    {
        return All.SelectMany(card => card.Examples, (card, example) => (card, example));
    }

    /// <summary>
    ///     Enumerates the examples of a card and of every card below it.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The card's own examples first, then those of its descendants, depth first.</returns>
    public static IEnumerable<(HelpCard Card, HelpExample Example)> SubtreeExamples(HelpCard card)
    {
        return Subtree(card).SelectMany(c => c.Examples, (c, example) => (c, example));
    }

    /// <summary>
    ///     Enumerates the cards below a card that have examples of their own.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The descendants of the card that have examples, depth first.</returns>
    public static IEnumerable<HelpCard> CardsBelowWithExamples(HelpCard card)
    {
        return Subtree(card).Skip(1).Where(c => c.Examples.Count > 0);
    }

    /// <summary>
    ///     Finds a card among some cards by its name or one of its aliases.
    /// </summary>
    /// <param name="cards">The cards to search.</param>
    /// <param name="nameOrAlias">The name or alias; matched ignoring case.</param>
    /// <returns>The card, or <see langword="null"/> when there is none.</returns>
    private static HelpCard? FindAmong(IEnumerable<HelpCard> cards, string nameOrAlias)
    {
        return cards.FirstOrDefault(c =>
            string.Equals(c.Name, nameOrAlias, StringComparison.OrdinalIgnoreCase)
            || c.Aliases.Any(a => string.Equals(a, nameOrAlias, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    ///     Creates the exception for a path segment that names no card below a card.
    /// </summary>
    /// <param name="parent">The card the segment should be below.</param>
    /// <param name="children">The cards below <paramref name="parent"/>.</param>
    /// <param name="segment">The segment as typed.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException CreateUnknownCardException(HelpCard parent, IReadOnlyList<HelpCard> children, string segment)
    {
        string hint;
        if (children.Count == 0)
        {
            hint = $"'{parent.Path}' has no cards below it. Run '{parent.Command}' to read it.";
        }
        else
        {
            var suggestion = CommandRegistry.ClosestName(segment, children.SelectMany(c => c.Aliases.Prepend(c.Name)));
            hint = $"Cards under '{parent.Path}': {string.Join(", ", children.Select(c => c.Name))}. "
                   + $"Run '{parent.Command}' for their summaries.";
            if (suggestion != null)
            {
                hint = $"Did you mean '{suggestion}'? {hint}";
            }
        }

        return new ToolException(ErrorCodes.UsageError, $"Unknown help card '{segment}' under '{parent.Path}'.", hint)
        {
            Command = "help"
        };
    }

    /// <summary>
    ///     Writes an in-text link as the command that prints its target.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns>
    ///     <c>`canvasnet help a b`</c>, <c>`canvasnet help a b --example name`</c>, or, for an example of the card being
    ///     read, <c>`--example name`</c>.
    /// </returns>
    internal static string FormatLink(HelpLink link)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (link.CardId == null)
        {
            return $"`--example {link.ExampleName}`";
        }

        var example = link.ExampleName == null ? string.Empty : $" --example {link.ExampleName}";
        return $"`canvasnet help {link.CardId.Replace('/', ' ')}{example}`";
    }

    /// <summary>
    ///     Loads the library catalog with the tool's cards and overlays, and converts the cards for the command line.
    /// </summary>
    /// <returns>The catalog.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a card is malformed or the topics are not the roots.</exception>
    private static CardCatalog LoadAll()
    {
        var catalog = HelpCatalog.Default.Extend(typeof(HelpTopics).Assembly, ResourcePrefix, Tokens);

        // The roots of the combined catalog are exactly the documented topics
        var roots = catalog.Roots.Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        if (!roots.SequenceEqual(TopicNames.Order(StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                $"The root help cards are {string.Join(", ", roots)}, not the topics {string.Join(", ", TopicNames)}.");
        }

        // Each card with its body written for the command line
        var byId = catalog.Cards.ToDictionary(c => c.Id, c => ToToolCard(catalog, c), StringComparer.Ordinal);

        // List the cards depth first, from the roots in their documented order
        var cards = new List<HelpCard>();
        void Visit(HelpCard card)
        {
            cards.Add(card);
            foreach (var child in card.ChildIds)
            {
                Visit(byId[child]);
            }
        }

        foreach (var root in TopicNames)
        {
            Visit(byId[root]);
        }

        return new CardCatalog(cards, byId);
    }

    /// <summary>
    ///     Converts a card of the combined catalog to the command line's card.
    /// </summary>
    /// <param name="catalog">The combined catalog.</param>
    /// <param name="card">The card.</param>
    /// <returns>The card, its links written as commands.</returns>
    private static HelpCard ToToolCard(HelpCatalog catalog, LibraryCard card)
    {
        return new HelpCard(
            card.Id,
            card.Aliases,
            card.Tags,
            card.Title,
            card.Summary,
            catalog.FormatMarkdown(card, FormatLink),
            card.ChildIds,
            card.SeeAlsoIds,
            card.Examples);
    }

    /// <summary>
    ///     The loaded cards.
    /// </summary>
    /// <param name="Cards">Every card, depth first.</param>
    /// <param name="ById">The cards by identifier.</param>
    private sealed record CardCatalog(IReadOnlyList<HelpCard> Cards, IReadOnlyDictionary<string, HelpCard> ById);
}

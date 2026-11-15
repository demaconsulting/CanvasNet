using System.Globalization;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     A tiny example of a help card: a source file, a command line, or a snippet.
/// </summary>
/// <param name="Name">
///     The name of the example, unique within the tree of its root card; selects it with <c>--example</c>.
/// </param>
/// <param name="Kind">
///     <c>source</c> (the content is a file the tool can read on its own), <c>command</c> (a command line), or
///     <c>snippet</c> (text that is shown but cannot be rendered alone, because it refers to other files).
/// </param>
/// <param name="Language">The language of the content, for example <c>openscad</c>, <c>json</c>, <c>xml</c>, or <c>shell</c>.</param>
/// <param name="FileName">The file name to save a <c>source</c> or <c>snippet</c> example as, or <see langword="null"/> for a command.</param>
/// <param name="Description">A one-line description.</param>
/// <param name="Content">The exact text of the example: LF line endings and no trailing newline.</param>
internal sealed record HelpExample(
    string Name,
    string Kind,
    string Language,
    string? FileName,
    string Description,
    string Content);

/// <summary>
///     One help card: a short Markdown text about one subject, a node of the help tree.
/// </summary>
/// <param name="Id">
///     The identifier: the path of the card file under <c>Commands/Help/</c> without <c>.md</c>, for example
///     <c>mermaid/gantt</c>.
/// </param>
/// <param name="Aliases">Other names that select the card among its siblings, for example <c>mmd</c>.</param>
/// <param name="Title">The title shown at the top of the card.</param>
/// <param name="Summary">The one-line summary shown in lists and in the See also footers that point at the card.</param>
/// <param name="Body">The Markdown body, printed as written after the <c>{{name}}</c> tokens are replaced.</param>
/// <param name="ChildIds">The identifiers of the cards below this one, in order.</param>
/// <param name="SeeAlso">The identifiers of related cards, in order.</param>
/// <param name="Examples">The examples of the card, in order.</param>
internal sealed record HelpCard(
    string Id,
    IReadOnlyList<string> Aliases,
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
///     embedded examples.
/// </summary>
/// <remarks>
///     <para>
///         Each card is one embedded Markdown file with YAML front matter
///         (<c>DemaConsulting.CanvasNet.Tool.Help/&lt;path&gt;.md</c>), and each source example is one embedded file
///         (<c>DemaConsulting.CanvasNet.Tool.Help.Examples.&lt;root&gt;-&lt;file name&gt;</c>), so an installed tool needs
///         no other files. The identifier of a card is its path, so the file layout is the tree. The catalog is read
///         once, on first use, and is immutable afterwards, so it is safe to use from several threads.
///     </para>
///     <para>
///         Numbers that the code defines (the largest image side, the resolution limit, the default triangle budget,
///         and so on) are written in the card text as <c>{{name}}</c> tokens and replaced from the live constants when
///         the card is loaded, so a documented limit cannot drift from the real one. Loading a malformed card throws
///         <see cref="InvalidOperationException"/> naming the file and the line; the unit tests load every card, so a
///         malformed file fails the build rather than a user.
///     </para>
///     <para>
///         The front-matter parser is in <c>HelpTopics.FrontMatter.cs</c>, and the rendering of a card (text and JSON)
///         is in <c>HelpTopics.Render.cs</c>.
///     </para>
/// </remarks>
internal static partial class HelpTopics
{
    /// <summary>
    ///     The prefix of the logical names of the embedded help resources.
    /// </summary>
    internal const string ResourcePrefix = "DemaConsulting.CanvasNet.Tool.Help";

    /// <summary>
    ///     The prefix of the logical names of the embedded card files; the rest of the name is the path of the file.
    /// </summary>
    internal const string CardResourcePrefix = ResourcePrefix + "/";

    /// <summary>
    ///     The prefix of the logical names of the embedded example sources.
    /// </summary>
    internal const string ExampleResourcePrefix = ResourcePrefix + ".Examples.";

    /// <summary>
    ///     The extension of a card file.
    /// </summary>
    internal const string CardExtension = ".md";

    /// <summary>
    ///     The kind of an example whose content is a source file.
    /// </summary>
    internal const string SourceKind = "source";

    /// <summary>
    ///     The kind of an example whose content is a command line.
    /// </summary>
    internal const string CommandKind = "command";

    /// <summary>
    ///     The kind of an example whose content is text that refers to other files, so it is shown but never rendered
    ///     alone (the render drift test skips it; a dedicated test renders it with its companion files).
    /// </summary>
    internal const string SnippetKind = "snippet";

    /// <summary>
    ///     The text every command-line example starts with.
    /// </summary>
    private const string CommandPrefix = "canvasnet ";

    /// <summary>
    ///     The catalog, loaded on first use.
    /// </summary>
    private static readonly Lazy<CardCatalog> Catalog = new(LoadAll);

    /// <summary>
    ///     Matches a <c>{{name}}</c> token.
    /// </summary>
    private static readonly Regex TokenPattern = new(
        @"\{\{([A-Za-z0-9]+)\}\}",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Matches a segment of a card identifier, a card name, or an alias: lower case letters and digits in groups
    ///     separated by single dashes.
    /// </summary>
    private static readonly Regex NamePattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Gets the identifiers of the root cards (the help topics), in the order they are listed.
    /// </summary>
    /// <remarks>
    ///     The loader proves that the embedded root cards are exactly this set, so a topic cannot be added or removed
    ///     without this list (and the tests that go with it) being updated.
    /// </remarks>
    public static IReadOnlyList<string> TopicNames { get; } =
    [
        "scad", "vega-lite", "mermaid", "svg", "pdf", "pptx", "vsdx", "raster",
        "style", "composition", "positions", "json", "errors", "limits", "files"
    ];

    /// <summary>
    ///     Gets the names of the topics that describe one format (and so list supported and unsupported features).
    /// </summary>
    public static IReadOnlyList<string> FormatTopicNames { get; } =
        ["scad", "vega-lite", "mermaid", "svg", "pdf", "pptx", "vsdx", "raster"];

    /// <summary>
    ///     Gets the values of the <c>{{name}}</c> tokens, taken from the constants of the code.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Tokens { get; } = CreateTokens();

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
    ///     Creates the token table from the constants of the code.
    /// </summary>
    /// <returns>The token values, formatted with the invariant culture and without thousands separators.</returns>
    private static Dictionary<string, string> CreateTokens()
    {
        var scad = ScadLimits.Default;
        var vegaLite = VegaLiteLimits.Default;
        var mermaid = MermaidLimits.Default;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = Format(ResultWriter.SchemaVersion),
            ["maxDimension"] = Format(Surface.MaxDimension),
            ["maxDpi"] = Format(ArgumentReader.MaxDpi),
            ["qualityMin"] = Format(ImageWriter.MinQuality),
            ["qualityMax"] = Format(ImageWriter.MaxQuality),
            ["qualityDefault"] = Format(ImageWriter.DefaultQuality),
            ["scadMaxTriangles"] = Format(scad.MaxTriangles),
            ["vlMaxSourceLength"] = Format(vegaLite.MaxSourceLength),
            ["vlMaxJsonDepth"] = Format(vegaLite.MaxJsonDepth),
            ["vlMaxDataFiles"] = Format(vegaLite.MaxDataFiles),
            ["vlMaxDataBytes"] = Format(vegaLite.MaxDataBytes),
            ["vlMaxDataRows"] = Format(vegaLite.MaxDataRows),
            ["vlMaxImages"] = Format(vegaLite.MaxImages),
            ["vlMaxPaintedPixels"] = Format(vegaLite.MaxPaintedPixels),
            ["mmdMaxTextSize"] = Format(mermaid.MaxTextSize),
            ["mmdMaxEdges"] = Format(mermaid.MaxEdges),
            ["mmdMaxNodes"] = Format(mermaid.MaxNodes),
            ["mmdMaxImages"] = Format(mermaid.MaxImages),
            ["fragmentMaxLength"] = Format(DemaConsulting.CanvasNet.ImageSources.ImageFragment.MaxLength),
            ["fragmentMaxParameters"] = Format(DemaConsulting.CanvasNet.ImageSources.ImageFragment.MaxParameters),
            ["positionsMax"] = Format(PositionCollector.MaxPositions),
            ["positionsMaxSelectors"] = Format(PositionSelection.MaxSelectors),
            ["positionsMaxSelectorLength"] = Format(PositionSelector.MaxSelectorLength),
            ["positionsMaxPredicates"] = Format(PositionSelector.MaxPredicates),
            ["positionsMaxDataFields"] = Format(ImagePosition.MaxDataFields)
        };
    }

    /// <summary>
    ///     Formats a number with the invariant culture.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The digits, without a thousands separator.</returns>
    private static string Format(IFormattable value)
    {
        return value.ToString(null, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Loads every embedded card and validates the tree.
    /// </summary>
    /// <returns>The catalog.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a card is malformed or the tree is inconsistent.</exception>
    private static CardCatalog LoadAll()
    {
        // Load every embedded card file; its path is its identifier
        var byId = new Dictionary<string, HelpCard>(StringComparer.Ordinal);
        foreach (var resource in typeof(HelpTopics).Assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(CardResourcePrefix, StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var fileName = resource[CardResourcePrefix.Length..].Replace('\\', '/');
            var card = LoadCard(fileName, ReadResource(resource));
            byId.Add(card.Id, card);
        }

        ValidateTree(byId);

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
    ///     Validates the tree: the roots, the children of every card, the See also links, the names and aliases, and the
    ///     example names.
    /// </summary>
    /// <param name="byId">The cards by identifier.</param>
    /// <exception cref="InvalidOperationException">Thrown when the tree is inconsistent.</exception>
    private static void ValidateTree(Dictionary<string, HelpCard> byId)
    {
        // The roots are exactly the documented topics
        var roots = byId.Values.Where(c => c.ParentId == null).Select(c => c.Id).Order(StringComparer.Ordinal).ToList();
        if (!roots.SequenceEqual(TopicNames.Order(StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                $"The root help cards are {string.Join(", ", roots)}, not the topics {string.Join(", ", TopicNames)}.");
        }

        foreach (var card in byId.Values)
        {
            // A card's children list names exactly the card files of its folder
            var files = byId.Values.Where(c => c.ParentId == card.Id).Select(c => c.Id).Order(StringComparer.Ordinal);
            var listed = card.ChildIds.Order(StringComparer.Ordinal);
            if (!files.SequenceEqual(listed))
            {
                throw new InvalidOperationException(
                    $"Help card '{card.Id}{CardExtension}' lists the children {string.Join(", ", card.ChildIds)}, but its folder holds {string.Join(", ", files)}.");
            }

            // A card below another needs the parent card
            if (card.ParentId != null && !byId.ContainsKey(card.ParentId))
            {
                throw new InvalidOperationException($"Help card '{card.Id}{CardExtension}' has no parent card '{card.ParentId}{CardExtension}'.");
            }

            // See also names existing cards, never the card itself, and never one twice
            foreach (var target in card.SeeAlso)
            {
                if (!byId.ContainsKey(target) || target == card.Id)
                {
                    throw new InvalidOperationException($"Help card '{card.Id}{CardExtension}' has the see-also entry '{target}', which is not another card.");
                }
            }

            RequireUnique(card.SeeAlso, $"see-also entry of help card '{card.Id}'");

            // The names and aliases of siblings select exactly one card
            var siblings = byId.Values.Where(c => c.ParentId == card.Id);
            RequireUnique(siblings.SelectMany(c => c.Aliases.Prepend(c.Name)), $"help card name below '{card.Id}'");
        }

        RequireUnique(byId.Values.Where(c => c.ParentId == null).SelectMany(c => c.Aliases.Prepend(c.Name)), "help topic name");

        // An example name selects exactly one example of a root's tree
        foreach (var root in TopicNames)
        {
            RequireUnique(
                byId.Values.Where(c => c.RootId == root).SelectMany(c => c.Examples).Select(e => e.Name),
                $"example name below '{root}'");
        }
    }

    /// <summary>
    ///     Loads one card from the text of its file.
    /// </summary>
    /// <param name="fileName">The path of the card file under <c>Commands/Help/</c>, with <c>/</c> separators.</param>
    /// <param name="text">The text of the file.</param>
    /// <returns>The card.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the card is malformed; the message names the file and the line.</exception>
    internal static HelpCard LoadCard(string fileName, string text)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(text);

        if (!fileName.EndsWith(CardExtension, StringComparison.Ordinal)
            || !fileName[..^CardExtension.Length].Split('/').All(NamePattern.IsMatch))
        {
            throw new InvalidOperationException(
                $"Help card '{fileName}' must be named '<name>{CardExtension}' in folders of lower case names separated by dashes.");
        }

        var id = fileName[..^CardExtension.Length];
        var document = ParseFrontMatter(text, fileName);
        var properties = new Dictionary<string, FrontMatterValue>(StringComparer.Ordinal);
        foreach (var (key, value) in document.Properties)
        {
            if (key is not ("title" or "summary" or "aliases" or "children" or "see-also" or "examples"))
            {
                throw FrontMatterError(
                    fileName,
                    value.Line,
                    $"the key '{key}' is unknown; a card has title, summary, aliases, children, see-also, and examples");
            }

            properties.Add(key, value);
        }

        var aliases = OptionalList(properties, "aliases", fileName);
        foreach (var (alias, line) in aliases.Where(a => !NamePattern.IsMatch(a.Text)))
        {
            throw FrontMatterError(fileName, line, $"the alias '{alias}' must be lower case letters, digits, and dashes");
        }

        var root = id.Split('/')[0];
        var examples = properties.TryGetValue("examples", out var exampleList)
            ? RequireList(exampleList, "examples", fileName).Select(e => LoadExample(e, root, fileName)).ToList()
            : [];

        return new HelpCard(
            id,
            aliases.Select(a => a.Text).ToList(),
            ReplaceTokens(RequireScalar(properties, "title", fileName), fileName),
            ReplaceTokens(RequireScalar(properties, "summary", fileName), fileName),
            ReplaceTokens(document.Body, fileName),
            OptionalList(properties, "children", fileName).Select(c => $"{id}/{c.Text}").ToList(),
            OptionalList(properties, "see-also", fileName).Select(s => s.Text).ToList(),
            examples);
    }

    /// <summary>
    ///     Loads one example of a card and, for a source or a snippet, the content of its embedded file.
    /// </summary>
    /// <param name="value">The front-matter value of the example: a map.</param>
    /// <param name="root">The identifier of the root card, which names the example file.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <returns>The example.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the example is malformed.</exception>
    private static HelpExample LoadExample(FrontMatterValue value, string root, string fileName)
    {
        if (value.Map == null)
        {
            throw FrontMatterError(fileName, value.Line, "an example must be a list item of 'key: value' entries");
        }

        var entries = new Dictionary<string, FrontMatterValue>(StringComparer.Ordinal);
        foreach (var (key, entry) in value.Map)
        {
            if (key is not ("name" or "kind" or "language" or "file" or "content" or "description"))
            {
                throw FrontMatterError(
                    fileName,
                    entry.Line,
                    $"the example key '{key}' is unknown; an example has name, kind, language, file, content, and description");
            }

            entries.Add(key, entry);
        }

        string Require(string key) =>
            entries.TryGetValue(key, out var entry) && entry.Scalar is { Length: > 0 } text
                ? text
                : throw FrontMatterError(fileName, value.Line, $"the example lacks the key '{key}'");

        var name = Require("name");
        var kind = Require("kind");
        var language = Require("language");
        var description = ReplaceTokens(Require("description"), fileName);
        switch (kind)
        {
            case SourceKind or SnippetKind when !entries.ContainsKey("content"):
                {
                    // A source and a snippet are files; a snippet is shown but never rendered alone
                    var file = Require("file");
                    return new HelpExample(name, kind, language, file, description, ReadResource($"{ExampleResourcePrefix}{root}-{file}"));
                }

            case CommandKind when !entries.ContainsKey("file"):
                {
                    // A command line must start with the tool's name
                    var command = ReplaceTokens(Require("content"), fileName);
                    if (!command.StartsWith(CommandPrefix, StringComparison.Ordinal))
                    {
                        throw FrontMatterError(fileName, value.Line, $"the command of the example '{name}' must start with '{CommandPrefix}'");
                    }

                    return new HelpExample(name, kind, language, null, description, command);
                }

            default:
                throw FrontMatterError(
                    fileName,
                    value.Line,
                    $"the example '{name}' must be a source or a snippet with a file, or a command with a content");
        }
    }

    /// <summary>
    ///     Reads a required scalar of the front matter.
    /// </summary>
    /// <param name="properties">The front-matter keys.</param>
    /// <param name="key">The key.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the key is missing or not a scalar.</exception>
    private static string RequireScalar(
        Dictionary<string, FrontMatterValue> properties,
        string key,
        string fileName)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            throw FrontMatterError(fileName, 1, $"the front matter lacks the key '{key}'");
        }

        return value.Scalar ?? throw FrontMatterError(fileName, value.Line, $"the key '{key}' must be one line of text");
    }

    /// <summary>
    ///     Reads an optional list of scalars of the front matter.
    /// </summary>
    /// <param name="properties">The front-matter keys.</param>
    /// <param name="key">The key.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <returns>The items with their lines; empty when the key is missing.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a list of scalars.</exception>
    private static List<(string Text, int Line)> OptionalList(
        Dictionary<string, FrontMatterValue> properties,
        string key,
        string fileName)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            return [];
        }

        return RequireList(value, key, fileName)
            .Select(item => (item.Scalar ?? throw FrontMatterError(fileName, item.Line, $"an item of '{key}' must be one line of text"), item.Line))
            .ToList();
    }

    /// <summary>
    ///     Requires a front-matter value to be a list.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="key">The key, for messages.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <returns>The items.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a list.</exception>
    private static IReadOnlyList<FrontMatterValue> RequireList(FrontMatterValue value, string key, string fileName)
    {
        return value.Items ?? throw FrontMatterError(fileName, value.Line, $"the key '{key}' must be a list of '  - ' items");
    }

    /// <summary>
    ///     Replaces the <c>{{name}}</c> tokens of a text with their values.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <returns>The text with every token replaced.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a token is unknown.</exception>
    private static string ReplaceTokens(string text, string fileName)
    {
        return TokenPattern.Replace(text, match =>
            Tokens.TryGetValue(match.Groups[1].Value, out var value)
                ? value
                : throw new InvalidOperationException($"Help card '{fileName}' uses the unknown token '{match.Value}'."));
    }

    /// <summary>
    ///     Reads an embedded text resource, with LF line endings and without trailing line feeds.
    /// </summary>
    /// <param name="logicalName">The logical name of the resource.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource does not exist.</exception>
    private static string ReadResource(string logicalName)
    {
        using var stream = typeof(HelpTopics).Assembly.GetManifestResourceStream(logicalName)
                           ?? throw new InvalidOperationException($"The embedded help resource '{logicalName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }

    /// <summary>
    ///     Requires the values to be unique, ignoring case.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="what">A description of the values, for the message.</param>
    /// <exception cref="InvalidOperationException">Thrown when a value is repeated.</exception>
    private static void RequireUnique(IEnumerable<string> values, string what)
    {
        var duplicate = values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"The {what} '{duplicate.Key}' is repeated.");
        }
    }

    /// <summary>
    ///     The loaded cards.
    /// </summary>
    /// <param name="Cards">Every card, depth first.</param>
    /// <param name="ById">The cards by identifier.</param>
    private sealed record CardCatalog(IReadOnlyList<HelpCard> Cards, IReadOnlyDictionary<string, HelpCard> ById);
}

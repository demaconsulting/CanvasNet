using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.MathMl;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.PlantUml;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     The loading of the help catalog: card files, examples, tokens, the tree validation, and the merge of extensions.
/// </summary>
/// <remarks>
///     <para>
///         Each card is one Markdown file with YAML front matter (see <see cref="HelpFrontMatter"/>) whose path is its
///         identifier, so the file layout is the tree. Numbers that the code defines are written as <c>{{name}}</c>
///         tokens and replaced from the live constants when a card is loaded, so a documented limit cannot drift from
///         the real one.
///     </para>
///     <para>
///         Every inconsistency is an <see cref="InvalidOperationException"/> naming the file (and the line where there
///         is one); the unit tests load every embedded card, so a malformed card fails the build rather than a user.
///     </para>
/// </remarks>
public sealed partial class HelpCatalog
{
    /// <summary>
    ///     The extension of a card file.
    /// </summary>
    internal const string CardExtension = ".md";

    /// <summary>
    ///     The front-matter key that makes a card file an overlay of the card it names.
    /// </summary>
    private const string ExtendsKey = "extends";

    /// <summary>
    ///     The identifiers of the library root cards, in the order they are listed.
    /// </summary>
    /// <remarks>The loader proves that the embedded library root cards are exactly this set.</remarks>
    internal static IReadOnlyList<string> LibraryRootOrder { get; } =
        Array.AsReadOnly(
        [
            "scad", "vega-lite", "mermaid", "plantuml", "mathml", "svg", "pdf", "pptx", "vsdx", "raster",
            "style", "composition", "positions", "errors", "limits", "files"
        ]);

    /// <summary>
    ///     Gets the values of the library <c>{{name}}</c> tokens, taken from the constants of the code.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> LibraryTokens { get; } = CreateLibraryTokens();

    /// <summary>
    ///     Matches a <c>{{name}}</c> token.
    /// </summary>
    private static readonly Regex TokenPattern = new(
        @"\{\{([A-Za-z0-9]+)\}\}",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Matches a segment of a card identifier, a card name, an alias, or an example name: lower case letters and
    ///     digits in groups separated by single dashes.
    /// </summary>
    private static readonly Regex NamePattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Creates the library catalog (or, in the unit tests, a catalog of test cards) from card texts.
    /// </summary>
    /// <param name="cards">The card files: the path with <c>/</c> separators (for example <c>mermaid/gantt.md</c>) and the text.</param>
    /// <param name="examples">The example files by <c>&lt;root&gt;-&lt;file name&gt;</c>.</param>
    /// <param name="rootOrder">The identifiers of the root cards, in order; the roots must be exactly these.</param>
    /// <returns>The unfiltered catalog.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a card is malformed or the tree is inconsistent.</exception>
    internal static HelpCatalog Create(
        IReadOnlyList<(string FileName, string Text)> cards,
        IReadOnlyDictionary<string, string> examples,
        IReadOnlyList<string> rootOrder)
    {
        // Parse every card; a library card is never an overlay
        var sources = cards.Select(c => ParseCard(c.FileName, c.Text, examples, LibraryTokens, null)).ToList();
        if (sources.FirstOrDefault(s => s.Extends != null) is { } overlay)
        {
            throw HelpFrontMatter.Error(overlay.FileName, overlay.ExtendsLine, $"the key '{ExtendsKey}' is only allowed in an extension");
        }

        // The roots are exactly the documented ones
        var roots = sources.Where(s => !s.Id.Contains('/', StringComparison.Ordinal)).Select(s => s.Id).Order(StringComparer.Ordinal);
        if (!roots.SequenceEqual(rootOrder.Order(StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                $"The root help cards are {string.Join(", ", roots)}, not {string.Join(", ", rootOrder)}.");
        }

        // Build and validate the tree, then the links of every body against it
        var nodes = sources.ToDictionary(s => s.Id, s => s.ToNode(), StringComparer.Ordinal);
        ValidateTree(nodes, sources, rootOrder);
        foreach (var source in sources)
        {
            nodes[source.Id].Body = ParseBody(source.Body, source.FileName, source.BodyLine, source.Id, nodes);
        }

        return new HelpCatalog(new HelpData(nodes, rootOrder, LibraryTokens), null);
    }

    /// <summary>
    ///     Gets a catalog with extension cards (texts) layered on this one; the public overload reads them from an
    ///     assembly.
    /// </summary>
    /// <param name="cards">The extension card files: the path with <c>/</c> separators and the text.</param>
    /// <param name="examples">The extension example files by <c>&lt;root&gt;-&lt;file name&gt;</c>.</param>
    /// <param name="tokens">The extra tokens, or <see langword="null"/>.</param>
    /// <returns>The combined, unfiltered catalog.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when this catalog is a filtered view, a card is malformed, an overlay names no card, a new card is
    ///     below a card of this catalog, or a token redefines a token of this catalog.
    /// </exception>
    internal HelpCatalog ExtendWith(
        IReadOnlyList<(string FileName, string Text)> cards,
        IReadOnlyDictionary<string, string> examples,
        IReadOnlyDictionary<string, string>? tokens)
    {
        if (_visible != null)
        {
            throw new InvalidOperationException("A filtered help catalog cannot be extended; extend the unfiltered catalog, then filter it.");
        }

        // The extension's tokens are added to the existing ones, never replace them
        var allTokens = new Dictionary<string, string>(_data.Tokens, StringComparer.Ordinal);
        foreach (var (name, value) in tokens ?? new Dictionary<string, string>())
        {
            if (!allTokens.TryAdd(name, value))
            {
                throw new InvalidOperationException($"The help token '{name}' of the extension redefines a token of the catalog.");
            }
        }

        // Copy the existing nodes, so this catalog is never changed
        var nodes = _data.ById.Values.ToDictionary(n => n.Id, n => n.Copy(), StringComparer.Ordinal);
        // Parsing refuses a new card in the tree of an existing card, so each added card starts a new tree
        var sources = cards.Select(c => ParseCard(c.FileName, c.Text, examples, allTokens, _data.ById)).ToList();
        var overlays = sources.Where(s => s.Extends != null).ToList();
        var added = sources.Where(s => s.Extends == null).ToList();

        // An overlay adds to the card at its own path
        foreach (var overlay in overlays)
        {
            if (overlay.Extends != overlay.Id || !nodes.TryGetValue(overlay.Id, out var target))
            {
                throw HelpFrontMatter.Error(
                    overlay.FileName,
                    overlay.ExtendsLine,
                    $"'{ExtendsKey}: {overlay.Extends}' must name the existing card at the overlay's own path, '{overlay.Id}'");
            }

            target.Title = overlay.Title ?? target.Title;
            target.Summary = overlay.Summary ?? target.Summary;
            target.SeeAlso = target.SeeAlso.Concat(overlay.SeeAlso.Where(s => !target.SeeAlso.Contains(s))).ToList();
            target.Examples = target.Examples.Concat(overlay.Examples).ToList();
        }

        // A new card starts a new tree (checked while parsing)
        foreach (var source in added)
        {
            nodes.Add(source.Id, source.ToNode());
        }

        // The new roots follow the existing ones; validate the new trees and the merged examples and related cards
        var rootOrder = _data.RootIds
            .Concat(added.Where(s => !s.Id.Contains('/', StringComparison.Ordinal)).Select(s => s.Id).Order(StringComparer.Ordinal))
            .ToList();
        ValidateTree(nodes, added.Concat(overlays).ToList(), rootOrder);

        // The links of the extension bodies resolve against the combined tree; an overlay body follows the card's body
        foreach (var source in added)
        {
            nodes[source.Id].Body = ParseBody(source.Body, source.FileName, source.BodyLine, source.Id, nodes);
        }

        foreach (var overlay in overlays.Where(o => o.Body.Length > 0))
        {
            var target = nodes[overlay.Id];
            var body = ParseBody(overlay.Body, overlay.FileName, overlay.BodyLine, overlay.Id, nodes);
            target.Body = target.Body.Count == 0 ? body : [.. target.Body, new BodyPart("\n\n", null), .. body];
        }

        return new HelpCatalog(new HelpData(nodes, rootOrder, allTokens), null);
    }

    /// <summary>
    ///     Reads the card files and example files of an assembly.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="prefix">The prefix of the logical resource names.</param>
    /// <returns>The card files (path and text) and the example files (by <c>&lt;root&gt;-&lt;file name&gt;</c>).</returns>
    private static (List<(string FileName, string Text)> Cards, Dictionary<string, string> Examples) ReadResources(
        Assembly assembly,
        string prefix)
    {
        var cardPrefix = prefix + "/";
        var examplePrefix = prefix + ".Examples.";
        var names = assembly.GetManifestResourceNames().Order(StringComparer.Ordinal).ToList();
        var cards = names
            .Where(n => n.StartsWith(cardPrefix, StringComparison.Ordinal))
            .Select(n => (n[cardPrefix.Length..].Replace('\\', '/'), ReadResource(assembly, n)))
            .ToList();
        var examples = names
            .Where(n => n.StartsWith(examplePrefix, StringComparison.Ordinal))
            .ToDictionary(n => n[examplePrefix.Length..], n => ReadResource(assembly, n), StringComparer.Ordinal);
        return (cards, examples);
    }

    /// <summary>
    ///     Reads an embedded text resource, with LF line endings and without trailing line feeds.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="logicalName">The logical name of the resource.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource does not exist.</exception>
    private static string ReadResource(Assembly assembly, string logicalName)
    {
        using var stream = assembly.GetManifestResourceStream(logicalName)
                           ?? throw new InvalidOperationException($"The embedded help resource '{logicalName}' is missing.");
        using var reader = new StreamReader(stream);
        return NormalizeText(reader.ReadToEnd());
    }

    /// <summary>
    ///     Normalizes a text: LF line endings and no trailing line feeds.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    private static string NormalizeText(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }

    /// <summary>
    ///     Parses one card file.
    /// </summary>
    /// <param name="fileName">The path of the card file, with <c>/</c> separators.</param>
    /// <param name="text">The text of the file.</param>
    /// <param name="examples">The example files by <c>&lt;root&gt;-&lt;file name&gt;</c>.</param>
    /// <param name="tokens">The token values.</param>
    /// <param name="extending">
    ///     The cards of the catalog being extended, or <see langword="null"/> when the library itself is created.
    /// </param>
    /// <returns>The parsed card, its body with tokens replaced but links not yet parsed.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the card is malformed (the message names the file and the line), or when it is an extension card
    ///     that is not an overlay but is in the tree of a card of <paramref name="extending"/> (checked before the keys).
    /// </exception>
    private static CardSource ParseCard(
        string fileName,
        string text,
        IReadOnlyDictionary<string, string> examples,
        IReadOnlyDictionary<string, string> tokens,
        IReadOnlyDictionary<string, HelpNode>? extending)
    {
        // The file name is the identifier
        if (!fileName.EndsWith(CardExtension, StringComparison.Ordinal)
            || !fileName[..^CardExtension.Length].Split('/').All(NamePattern.IsMatch))
        {
            throw new InvalidOperationException(
                $"Help card '{fileName}' must be named '<name>{CardExtension}' in folders of lower case names separated by dashes.");
        }

        var id = fileName[..^CardExtension.Length];
        var document = HelpFrontMatter.Parse(NormalizeText(text), fileName);
        var properties = document.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var isOverlay = properties.ContainsKey(ExtendsKey);

        // A new extension card starts a new tree, never a branch of an existing one; this is checked before the keys,
        // so a card left behind by a move is reported as misplaced, not as malformed
        var root = id.Split('/')[0];
        if (!isOverlay && extending != null && extending.ContainsKey(root))
        {
            throw new InvalidOperationException(
                $"Help card '{fileName}' is in the tree of the card '{root}' of the catalog: write it in the catalog's "
                + $"assembly, or make it an overlay with '{ExtendsKey}: {id}' of an existing card.");
        }

        // An overlay adds to a card; it cannot change what the card is or where it is
        string[] allowed = isOverlay
            ? [ExtendsKey, "title", "summary", "see-also", "examples"]
            : ["title", "summary", "tags", "aliases", "children", "see-also", "examples", ExtendsKey];
        var unknown = document.Properties.FirstOrDefault(p => !allowed.Contains(p.Key));
        if (unknown.Key != null)
        {
            throw HelpFrontMatter.Error(
                fileName,
                unknown.Value.Line,
                isOverlay
                    ? $"the key '{unknown.Key}' is not allowed in an overlay; an overlay has {string.Join(", ", allowed)}"
                    : $"the key '{unknown.Key}' is unknown; a card has {string.Join(", ", allowed.Where(k => k != ExtendsKey))}");
        }

        string Replace(string value) => ReplaceTokens(value, fileName, tokens);

        var aliases = OptionalList(properties, "aliases", fileName);
        var badAlias = aliases.FirstOrDefault(a => !NamePattern.IsMatch(a.Text));
        if (badAlias.Text != null)
        {
            throw HelpFrontMatter.Error(fileName, badAlias.Line, $"the alias '{badAlias.Text}' must be lower case letters, digits, and dashes");
        }

        var exampleList = properties.TryGetValue("examples", out var exampleValue)
            ? RequireList(exampleValue, "examples", fileName).Select(e => ParseExample(e, root, fileName, examples, tokens)).ToList()
            : [];

        return new CardSource(
            id,
            fileName,
            isOverlay ? RequireScalar(properties, ExtendsKey, fileName) : null,
            isOverlay ? properties[ExtendsKey].Line : 0,
            isOverlay && !properties.ContainsKey("title") ? null : Replace(RequireScalar(properties, "title", fileName)),
            isOverlay && !properties.ContainsKey("summary") ? null : Replace(RequireScalar(properties, "summary", fileName)),
            isOverlay ? [] : ParseTags(properties, fileName),
            aliases.Select(a => a.Text).ToList(),
            OptionalList(properties, "children", fileName).Select(c => $"{id}/{c.Text}").ToList(),
            OptionalList(properties, "see-also", fileName).Select(s => s.Text).ToList(),
            exampleList,
            Replace(document.Body),
            document.BodyLine);
    }

    /// <summary>
    ///     Reads and validates the tags of a card: at least one, all from the vocabulary, no repeats, and at least one
    ///     scope tag.
    /// </summary>
    /// <param name="properties">The front-matter keys.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <returns>The tags, in the order written.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the tags are missing or invalid.</exception>
    private static List<string> ParseTags(Dictionary<string, FrontMatterValue> properties, string fileName)
    {
        if (!properties.TryGetValue("tags", out var value))
        {
            throw HelpFrontMatter.Error(fileName, 1, "the front matter lacks the key 'tags'");
        }

        var tags = OptionalList(properties, "tags", fileName);
        foreach (var (tag, line) in tags)
        {
            if (!HelpTags.All.Contains(tag))
            {
                throw HelpFrontMatter.Error(fileName, line, $"the tag '{tag}' is unknown; the tags are {string.Join(", ", HelpTags.All)}");
            }
        }

        var names = tags.Select(t => t.Text).ToList();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count)
        {
            throw HelpFrontMatter.Error(fileName, value.Line, "a tag is repeated");
        }

        if (!names.Any(HelpTags.ScopeTags.Contains))
        {
            throw HelpFrontMatter.Error(
                fileName,
                value.Line,
                $"a card needs at least one scope tag ({string.Join(", ", HelpTags.ScopeTags)})");
        }

        return names;
    }

    /// <summary>
    ///     Parses one example of a card and, for a source or a snippet, reads the content of its file.
    /// </summary>
    /// <param name="value">The front-matter value of the example: a map.</param>
    /// <param name="root">The identifier of the root card, which names the example file.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <param name="examples">The example files by <c>&lt;root&gt;-&lt;file name&gt;</c>.</param>
    /// <param name="tokens">The token values.</param>
    /// <returns>The example.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the example is malformed or its file is missing.</exception>
    private static HelpExample ParseExample(
        FrontMatterValue value,
        string root,
        string fileName,
        IReadOnlyDictionary<string, string> examples,
        IReadOnlyDictionary<string, string> tokens)
    {
        if (value.Map == null)
        {
            throw HelpFrontMatter.Error(fileName, value.Line, "an example must be a list item of 'key: value' entries");
        }

        var entries = new Dictionary<string, FrontMatterValue>(StringComparer.Ordinal);
        foreach (var (key, entry) in value.Map)
        {
            if (key is not ("name" or "kind" or "language" or "file" or "content" or "description"))
            {
                throw HelpFrontMatter.Error(
                    fileName,
                    entry.Line,
                    $"the example key '{key}' is unknown; an example has name, kind, language, file, content, and description");
            }

            entries.Add(key, entry);
        }

        string Require(string key) =>
            entries.TryGetValue(key, out var entry) && entry.Scalar is { Length: > 0 } text
                ? text
                : throw HelpFrontMatter.Error(fileName, value.Line, $"the example lacks the key '{key}'");

        var name = Require("name");
        if (!NamePattern.IsMatch(name))
        {
            throw HelpFrontMatter.Error(fileName, value.Line, $"the example name '{name}' must be lower case letters, digits, and dashes");
        }

        var kind = Require("kind");
        var language = Require("language");
        var description = ReplaceTokens(Require("description"), fileName, tokens);
        switch (kind)
        {
            case HelpExample.SourceKind or HelpExample.SnippetKind when !entries.ContainsKey("content"):
                {
                    // A source and a snippet are files; a snippet is shown but never rendered alone
                    var file = Require("file");
                    var key = $"{root}-{file}";
                    var content = examples.TryGetValue(key, out var text)
                        ? NormalizeText(text)
                        : throw HelpFrontMatter.Error(fileName, value.Line, $"the example file '{key}' of the example '{name}' is missing");
                    return new HelpExample(name, kind, language, file, description, content);
                }

            case HelpExample.CommandKind when !entries.ContainsKey("file"):
                return new HelpExample(name, kind, language, null, description, ReplaceTokens(Require("content"), fileName, tokens));

            default:
                throw HelpFrontMatter.Error(
                    fileName,
                    value.Line,
                    $"the example '{name}' must be a source or a snippet with a file, or a command with a content");
        }
    }

    /// <summary>
    ///     Validates the tree: the children of the given cards, their parents, their related cards, the names and
    ///     aliases of siblings and roots, and the example names of every root tree.
    /// </summary>
    /// <param name="nodes">Every card by identifier.</param>
    /// <param name="sources">The parsed card files to check (all of them, or the cards and overlays of an extension).</param>
    /// <param name="rootOrder">The identifiers of the root cards.</param>
    /// <exception cref="InvalidOperationException">Thrown when the tree is inconsistent.</exception>
    private static void ValidateTree(
        Dictionary<string, HelpNode> nodes,
        IReadOnlyList<CardSource> sources,
        IReadOnlyList<string> rootOrder)
    {
        foreach (var source in sources)
        {
            var node = nodes[source.Id];

            // A card's children list names exactly the card files of its folder
            if (source.Extends == null)
            {
                var files = nodes.Values.Where(n => n.ParentId == node.Id).Select(n => n.Id).Order(StringComparer.Ordinal);
                if (!files.SequenceEqual(node.ChildIds.Order(StringComparer.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Help card '{source.FileName}' lists the children {string.Join(", ", node.ChildIds)}, but its folder holds {string.Join(", ", files)}.");
                }

                // A card below another needs the parent card
                if (node.ParentId != null && !nodes.ContainsKey(node.ParentId))
                {
                    throw new InvalidOperationException($"Help card '{source.FileName}' has no parent card '{node.ParentId}{CardExtension}'.");
                }

                // The names and aliases of siblings select exactly one card
                RequireUnique(
                    nodes.Values.Where(n => n.ParentId == node.Id).SelectMany(n => n.Aliases.Prepend(n.Name)),
                    $"help card name below '{node.Id}'");
            }

            // Related cards are existing other cards, each named once
            var badTarget = node.SeeAlso.FirstOrDefault(t => !nodes.ContainsKey(t) || t == node.Id);
            if (badTarget != null)
            {
                throw new InvalidOperationException($"Help card '{source.FileName}' has the see-also entry '{badTarget}', which is not another card.");
            }

            RequireUnique(source.SeeAlso, $"see-also entry of help card '{source.Id}'");
        }

        // The names and aliases of the roots select exactly one root, and an example name one example of a tree
        RequireUnique(rootOrder.Select(id => nodes[id]).SelectMany(n => n.Aliases.Prepend(n.Name)), "help root card name");
        foreach (var root in rootOrder)
        {
            RequireUnique(
                nodes.Values.Where(n => n.RootId == root).SelectMany(n => n.Examples).Select(e => e.Name),
                $"example name below '{root}'");
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
    private static string RequireScalar(Dictionary<string, FrontMatterValue> properties, string key, string fileName)
    {
        if (!properties.TryGetValue(key, out var value))
        {
            throw HelpFrontMatter.Error(fileName, 1, $"the front matter lacks the key '{key}'");
        }

        return value.Scalar ?? throw HelpFrontMatter.Error(fileName, value.Line, $"the key '{key}' must be one line of text");
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
            .Select(item => (item.Scalar ?? throw HelpFrontMatter.Error(fileName, item.Line, $"an item of '{key}' must be one line of text"), item.Line))
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
        return value.Items ?? throw HelpFrontMatter.Error(fileName, value.Line, $"the key '{key}' must be a list of '  - ' items");
    }

    /// <summary>
    ///     Replaces the <c>{{name}}</c> tokens of a text with their values.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="fileName">The name of the card file, for messages.</param>
    /// <param name="tokens">The token values.</param>
    /// <returns>The text with every token replaced.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a token is unknown.</exception>
    private static string ReplaceTokens(string text, string fileName, IReadOnlyDictionary<string, string> tokens)
    {
        return TokenPattern.Replace(text, match =>
            tokens.TryGetValue(match.Groups[1].Value, out var value)
                ? value
                : throw new InvalidOperationException($"Help card '{fileName}' uses the unknown token '{match.Value}'."));
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
    ///     Creates the library token table from the constants of the code.
    /// </summary>
    /// <returns>The token values, formatted with the invariant culture and without thousands separators.</returns>
    private static Dictionary<string, string> CreateLibraryTokens()
    {
        var scad = ScadLimits.Default;
        var vegaLite = VegaLiteLimits.Default;
        var mermaid = MermaidLimits.Default;
        var plantUml = PlantUmlLimits.Default;
        var mathMl = MathMlLimits.Default;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["maxDimension"] = Format(Surface.MaxDimension),
            ["maxDpi"] = Format(SourceLoader.MaxDpi),
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
            ["pumlMaxTextSize"] = Format(plantUml.MaxTextSize),
            ["pumlMaxNodes"] = Format(plantUml.MaxNodes),
            ["pumlMaxEdges"] = Format(plantUml.MaxEdges),
            ["pumlMaxIncludes"] = Format(plantUml.MaxIncludes),
            ["pumlMaxIncludeDepth"] = Format(plantUml.MaxIncludeDepth),
            ["mmlMaxTextSize"] = Format(mathMl.MaxTextSize),
            ["mmlMaxElements"] = Format(mathMl.MaxElements),
            ["mmlMaxNestingDepth"] = Format(mathMl.MaxNestingDepth),
            ["mmlMaxTableCells"] = Format(mathMl.MaxTableCells),
            ["mmlMaxLayoutWork"] = Format(mathMl.MaxLayoutWork),
            ["fragmentMaxLength"] = Format(ImageFragment.MaxLength),
            ["fragmentMaxParameters"] = Format(ImageFragment.MaxParameters),
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
    ///     One parsed card file, before the tree is built.
    /// </summary>
    /// <param name="Id">The identifier: the path without the extension.</param>
    /// <param name="FileName">The path of the file, for messages.</param>
    /// <param name="Extends">The card an overlay extends, or <see langword="null"/> for a card.</param>
    /// <param name="ExtendsLine">The line of the <c>extends</c> key, for messages.</param>
    /// <param name="Title">The title (tokens replaced), or <see langword="null"/> for an overlay that keeps the card's.</param>
    /// <param name="Summary">The summary (tokens replaced), or <see langword="null"/> for an overlay that keeps the card's.</param>
    /// <param name="Tags">The tags; empty for an overlay.</param>
    /// <param name="Aliases">The aliases.</param>
    /// <param name="ChildIds">The identifiers of the children.</param>
    /// <param name="SeeAlso">The identifiers of the related cards.</param>
    /// <param name="Examples">The examples.</param>
    /// <param name="Body">The body, tokens replaced, links not parsed.</param>
    /// <param name="BodyLine">The line of the file the body starts on.</param>
    private sealed record CardSource(
        string Id,
        string FileName,
        string? Extends,
        int ExtendsLine,
        string? Title,
        string? Summary,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> Aliases,
        IReadOnlyList<string> ChildIds,
        IReadOnlyList<string> SeeAlso,
        IReadOnlyList<HelpExample> Examples,
        string Body,
        int BodyLine)
    {
        /// <summary>
        ///     Creates the tree node of a card (not an overlay); its body is parsed once the tree is complete.
        /// </summary>
        /// <returns>The node.</returns>
        public HelpNode ToNode()
        {
            return new HelpNode(Id)
            {
                Title = Title!,
                Summary = Summary!,
                Tags = Tags,
                Aliases = Aliases,
                ChildIds = ChildIds,
                SeeAlso = SeeAlso,
                Examples = Examples
            };
        }
    }
}

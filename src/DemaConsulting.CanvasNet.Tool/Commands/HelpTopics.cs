// cspell:ignore canvasnet
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     One entry of a help section: optionally a term (an option, an identifier, a key) and its text.
/// </summary>
/// <param name="Term">The term the text explains, or <see langword="null"/> for a plain bullet.</param>
/// <param name="Text">The text of the item.</param>
internal sealed record HelpItem(string? Term, string Text);

/// <summary>
///     A titled list of items of a help topic, for example the supported features.
/// </summary>
/// <param name="Id">The stable kebab-case identifier of the section, unique within its topic.</param>
/// <param name="Title">The heading of the section.</param>
/// <param name="Items">The items of the section, in order.</param>
internal sealed record HelpSection(string Id, string Title, IReadOnlyList<HelpItem> Items);

/// <summary>
///     A tiny example of a help topic: a source file, a command line, or a snippet.
/// </summary>
/// <param name="Name">The name of the example, unique within its topic; selects it with <c>--example</c>.</param>
/// <param name="Kind">
///     <c>source</c> (the content is a file the tool can read on its own), <c>command</c> (a command line), or
///     <c>snippet</c> (text that is shown but cannot be rendered alone, because it refers to other files).
/// </param>
/// <param name="Language">The language of the content: <c>openscad</c>, <c>json</c>, <c>xml</c>, or <c>shell</c>.</param>
/// <param name="FileName">The file name to save a <c>source</c> example as, or <see langword="null"/> for a command or snippet.</param>
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
///     One help topic: a format or a cross-cutting subject such as the error codes.
/// </summary>
/// <param name="Name">The canonical topic name, for example <c>scad</c>.</param>
/// <param name="Aliases">Other names that select the topic, for example <c>openscad</c>.</param>
/// <param name="Title">The title shown at the top of the topic.</param>
/// <param name="Summary">The one-line summary shown in the topic list.</param>
/// <param name="Sections">The sections of the topic, in order.</param>
/// <param name="Examples">The examples of the topic, in order.</param>
internal sealed record HelpTopic(
    string Name,
    IReadOnlyList<string> Aliases,
    string Title,
    string Summary,
    IReadOnlyList<HelpSection> Sections,
    IReadOnlyList<HelpExample> Examples);

/// <summary>
///     The catalog of help topics (formats and cross-cutting subjects) that the <c>help</c> command prints, with
///     their embedded examples.
/// </summary>
/// <remarks>
///     <para>
///         The content is embedded in the tool assembly as resources: one JSON file per topic
///         (<c>DemaConsulting.CanvasNet.Tool.Help.&lt;topic&gt;.json</c>) and one file per source example
///         (<c>DemaConsulting.CanvasNet.Tool.Help.Examples.&lt;topic&gt;-&lt;file name&gt;</c>), so an installed tool needs
///         no other files. The catalog is read once, on first use, and is immutable afterwards, so it is safe to
///         use from several threads.
///     </para>
///     <para>
///         Numbers that the code defines (the largest image side, the resolution limit, the default triangle
///         budget, and so on) are written in the JSON text as <c>{{name}}</c> tokens and replaced from the live
///         constants when the topic is loaded, so a documented limit cannot drift from the real one. Loading a
///         malformed resource throws <see cref="InvalidOperationException"/>; the unit tests load every topic, so a
///         malformed file fails the build rather than a user.
///     </para>
///     <para>
///         The rendering of a topic (text and JSON) is in <c>HelpTopics.Render.cs</c>.
///     </para>
/// </remarks>
internal static partial class HelpTopics
{
    /// <summary>
    ///     The prefix of the logical names of the embedded topic files.
    /// </summary>
    internal const string ResourcePrefix = "DemaConsulting.CanvasNet.Tool.Help.";

    /// <summary>
    ///     The prefix of the logical names of the embedded example sources.
    /// </summary>
    internal const string ExampleResourcePrefix = ResourcePrefix + "Examples.";

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
    private static readonly Lazy<IReadOnlyList<HelpTopic>> Catalog = new(LoadAll);

    /// <summary>
    ///     Matches a <c>{{name}}</c> token.
    /// </summary>
    private static readonly Regex TokenPattern = new(
        @"\{\{([A-Za-z0-9]+)\}\}",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Gets the canonical topic names, in the order the topics are listed.
    /// </summary>
    /// <remarks>
    ///     A test proves that the embedded topic files are exactly this set, so a topic cannot be added or removed
    ///     without this list (and the tests that go with it) being updated.
    /// </remarks>
    public static IReadOnlyList<string> TopicNames { get; } =
        ["scad", "vega-lite", "svg", "pdf", "pptx", "vsdx", "raster", "json", "errors", "limits"];

    /// <summary>
    ///     Gets the names of the topics that describe one format (and so list supported and unsupported features).
    /// </summary>
    public static IReadOnlyList<string> FormatTopicNames { get; } =
        ["scad", "vega-lite", "svg", "pdf", "pptx", "vsdx", "raster"];

    /// <summary>
    ///     Gets the values of the <c>{{name}}</c> tokens, taken from the constants of the code.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Tokens { get; } = CreateTokens();

    /// <summary>
    ///     Gets every topic, in <see cref="TopicNames"/> order.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an embedded topic is missing or malformed.</exception>
    public static IReadOnlyList<HelpTopic> All => Catalog.Value;

    /// <summary>
    ///     Finds a topic by its name or one of its aliases.
    /// </summary>
    /// <param name="nameOrAlias">The topic name or alias; matched ignoring case.</param>
    /// <returns>The topic, or <see langword="null"/> when there is none.</returns>
    public static HelpTopic? Find(string nameOrAlias)
    {
        return All.FirstOrDefault(t =>
            string.Equals(t.Name, nameOrAlias, StringComparison.OrdinalIgnoreCase)
            || t.Aliases.Any(a => string.Equals(a, nameOrAlias, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    ///     Enumerates the examples whose content is a source file, with their topics.
    /// </summary>
    /// <returns>Every <c>source</c> example of every topic.</returns>
    /// <remarks>
    ///     These are the examples that the tests and the self-test render with <c>--strict</c> to prove that they are
    ///     accepted without a diagnostic.
    /// </remarks>
    public static IEnumerable<(HelpTopic Topic, HelpExample Example)> SourceExamples()
    {
        return All.SelectMany(
            topic => topic.Examples.Where(e => e.Kind == SourceKind),
            (topic, example) => (topic, example));
    }

    /// <summary>
    ///     Creates the token table from the constants of the code.
    /// </summary>
    /// <returns>The token values, formatted with the invariant culture and without thousands separators.</returns>
    private static Dictionary<string, string> CreateTokens()
    {
        var scad = ScadLimits.Default;
        var vegaLite = VegaLiteLimits.Default;
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
            ["fragmentMaxLength"] = Format(DemaConsulting.CanvasNet.ImageSources.ImageFragment.MaxLength),
            ["fragmentMaxParameters"] = Format(DemaConsulting.CanvasNet.ImageSources.ImageFragment.MaxParameters)
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
    ///     Loads and validates every topic of <see cref="TopicNames"/>.
    /// </summary>
    /// <returns>The topics.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a topic is missing or malformed, or a name is repeated.</exception>
    private static List<HelpTopic> LoadAll()
    {
        var topics = TopicNames.Select(LoadTopic).ToList();

        // Every name and alias must select exactly one topic
        RequireUnique(
            topics.SelectMany(t => t.Aliases.Prepend(t.Name)).Select(n => n.ToLowerInvariant()),
            "help topic name");

        return topics;
    }

    /// <summary>
    ///     Loads one topic from its embedded file.
    /// </summary>
    /// <param name="name">The canonical topic name.</param>
    /// <returns>The topic.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the file is missing or malformed.</exception>
    private static HelpTopic LoadTopic(string name)
    {
        var text = TokenPattern.Replace(ReadResource($"{ResourcePrefix}{name}.json"), ReplaceToken);
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;

        var topicName = RequireString(root, "topic", name);
        if (topicName != name)
        {
            throw new InvalidOperationException($"The help topic file '{name}.json' declares the topic '{topicName}'.");
        }

        var sections = root.GetProperty("sections").EnumerateArray().Select(s => ParseSection(s, name)).ToList();
        RequireUnique(sections.Select(s => s.Id), $"section id of topic '{name}'");

        var examples = root.GetProperty("examples").EnumerateArray().Select(e => ParseExample(e, name)).ToList();
        RequireUnique(examples.Select(e => e.Name), $"example name of topic '{name}'");

        return new HelpTopic(
            name,
            root.GetProperty("aliases").EnumerateArray().Select(a => a.GetString() ?? string.Empty).ToList(),
            RequireString(root, "title", name),
            RequireString(root, "summary", name),
            sections,
            examples);
    }

    /// <summary>
    ///     Replaces one <c>{{name}}</c> token with its value.
    /// </summary>
    /// <param name="match">The token match.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the token is unknown.</exception>
    private static string ReplaceToken(Match match)
    {
        return Tokens.TryGetValue(match.Groups[1].Value, out var value)
            ? value
            : throw new InvalidOperationException($"The help text uses the unknown token '{match.Value}'.");
    }

    /// <summary>
    ///     Parses a section.
    /// </summary>
    /// <param name="element">The section's JSON object.</param>
    /// <param name="topic">The topic name, for messages.</param>
    /// <returns>The section.</returns>
    private static HelpSection ParseSection(JsonElement element, string topic)
    {
        var items = element.GetProperty("items").EnumerateArray().Select(item =>
            new HelpItem(
                item.TryGetProperty("term", out var term) ? term.GetString() : null,
                RequireString(item, "text", topic))).ToList();
        return new HelpSection(RequireString(element, "id", topic), RequireString(element, "title", topic), items);
    }

    /// <summary>
    ///     Parses an example and reads its content: from the embedded example file for a source, from the JSON for a command.
    /// </summary>
    /// <param name="element">The example's JSON object.</param>
    /// <param name="topic">The topic name, for messages and for the name of the example file.</param>
    /// <returns>The example.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the example is malformed.</exception>
    private static HelpExample ParseExample(JsonElement element, string topic)
    {
        var name = RequireString(element, "name", topic);
        var kind = RequireString(element, "kind", topic);
        var language = RequireString(element, "language", topic);
        var description = RequireString(element, "description", topic);

        // A source and a snippet are files; a snippet is shown but never rendered alone
        if (kind is SourceKind or SnippetKind)
        {
            var fileName = RequireString(element, "fileName", topic);
            var content = ReadResource($"{ExampleResourcePrefix}{topic}-{fileName}");
            return new HelpExample(name, kind, language, fileName, description, content);
        }

        // Anything else must be a command line, and a command line must start with the tool's name
        var command = RequireString(element, "content", topic);
        if (kind != CommandKind || !command.StartsWith(CommandPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The example '{name}' of topic '{topic}' must be a source, a snippet, or a command starting with '{CommandPrefix}'.");
        }

        return new HelpExample(name, kind, language, null, description, command);
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
    ///     Reads a required, non-empty string property.
    /// </summary>
    /// <param name="element">The JSON object.</param>
    /// <param name="property">The property name.</param>
    /// <param name="topic">The topic name, for messages.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the property is missing or empty.</exception>
    private static string RequireString(JsonElement element, string property, string topic)
    {
        return element.TryGetProperty(property, out var value) && value.GetString() is { Length: > 0 } text
            ? text
            : throw new InvalidOperationException($"The help topic '{topic}' lacks the text '{property}'.");
    }

    /// <summary>
    ///     Requires the values to be unique.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="what">A description of the values, for the message.</param>
    /// <exception cref="InvalidOperationException">Thrown when a value is repeated.</exception>
    private static void RequireUnique(IEnumerable<string> values, string what)
    {
        var duplicate = values.GroupBy(v => v, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"The {what} '{duplicate.Key}' is repeated.");
        }
    }
}

using System.Globalization;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Composition;
using DemaConsulting.HelpCards.Loading;

namespace DemaConsulting.CanvasNet.Tool.Commands;

/// <summary>
///     The tree of help cards (formats and cross-cutting subjects) that the <c>help</c> command prints, with their
///     embedded examples: the Viewer library's cards with the tool's command-line layer on top, composed by
///     DemaConsulting.HelpCards.
/// </summary>
/// <remarks>
///     <para>
///         The drawing and format cards live in the Viewer library (<see cref="CanvasNetHelp"/>), so a program that uses
///         the library gets the same knowledge. The tool's <see cref="Source"/> holds the embedded Markdown files of this
///         assembly (<c>DemaConsulting.CanvasNet.Tool.Help/&lt;path&gt;</c>) and imports the library source: each file
///         is either a tool-only card (the <c>json</c> and <c>pandoc</c> trees) or an overlay
///         (<c>extends: &lt;path&gt;</c>) that appends the command-line flags, the command examples, and related tool
///         cards to the library card at the same path.
///     </para>
///     <para>
///         <see cref="Catalog"/> combines both sources once, on first use, and is immutable afterwards, so it is safe to
///         use from several threads. Building it fails with a <c>HelpCatalogException</c> naming the file and the line
///         when a card is defective; the unit tests build and validate it, so a defect fails the build rather than a
///         user. The rendering helpers (render options, the JSON envelope, and the topics document) are in
///         <c>HelpTopics.Render.cs</c>.
///     </para>
/// </remarks>
internal static partial class HelpTopics
{
    /// <summary>
    ///     The name of the tool as the user types it, used in the commands that the help prints.
    /// </summary>
    internal const string ToolName = "canvasnet";

    /// <summary>
    ///     The name of the tool source: the <c>name</c> of its <c>_source.md</c>.
    /// </summary>
    internal const string SourceName = "canvasnet-tool";

    /// <summary>
    ///     The template of the command that prints a card; <c>{path}</c> is the card identifier with spaces for slashes.
    /// </summary>
    internal const string CommandTemplate = "canvasnet help {path}";

    /// <summary>
    ///     The combined catalog, built on first use (declared after the lists it reads, so they are initialized first).
    /// </summary>
    private static readonly Lazy<HelpCatalog> CombinedCatalog = new(
        () => HelpCatalog.Create(builder => Configure(builder)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Gets the identifiers of the root cards (the help topics), in the order they are listed: the library roots
    ///     (<see cref="CanvasNetHelp.RootIds"/>), then the tool's own <c>json</c> and <c>pandoc</c>.
    /// </summary>
    /// <remarks>
    ///     <see cref="Catalog"/> is built with exactly these roots expected, so a topic cannot be added or removed
    ///     without this list (and the tests that go with it) being updated. The tool's roots follow the library's
    ///     because DemaConsulting.HelpCards orders the roots of each source separately.
    /// </remarks>
    public static IReadOnlyList<string> TopicNames { get; } =
        Array.AsReadOnly(CanvasNetHelp.RootIds.Concat(["json", "pandoc"]).ToArray());

    /// <summary>
    ///     Gets the names of the topics that describe one format (and so list supported and unsupported features).
    /// </summary>
    public static IReadOnlyList<string> FormatTopicNames { get; } =
        ["scad", "vega-lite", "mermaid", "plantuml", "mathml", "svg", "pdf", "pptx", "vsdx", "raster"];

    /// <summary>
    ///     Gets the values of the tool's own <c>{{name}}</c> tokens, taken from the constants of the tool; the library
    ///     cards' tokens are those of <see cref="CanvasNetHelp"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Tokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["schemaVersion"] = ResultWriter.SchemaVersion.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>
    ///     Gets the source of the tool's cards and overlays: the cards embedded in this assembly, with <see cref="Tokens"/>,
    ///     importing the library source so the overlays can extend library cards and the links can name them.
    /// </summary>
    public static HelpSource Source { get; } = HelpSource.FromAssembly(typeof(HelpTopics).Assembly)
        .WithTokens(Tokens)
        .Imports(CanvasNetHelp.Source);

    /// <summary>
    ///     Gets the combined catalog of the library and tool cards, built on first use.
    /// </summary>
    /// <exception cref="HelpCatalogException">Thrown when an embedded card is defective.</exception>
    public static HelpCatalog Catalog => CombinedCatalog.Value;

    /// <summary>
    ///     Gets the root cards, in <see cref="TopicNames"/> order.
    /// </summary>
    public static IReadOnlyList<HelpCard> Roots => Catalog.Roots;

    /// <summary>
    ///     Adds the library and tool cards to a catalog builder, with the tool name, the version, and the expected roots.
    /// </summary>
    /// <remarks>
    ///     This is the one place that says how the combined catalog is composed, so the help command and the validation
    ///     tests compose it the same way.
    /// </remarks>
    /// <param name="builder">The builder to configure. Must not be null.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static HelpCatalogBuilder Configure(HelpCatalogBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return CanvasNetHelp.Configure(builder)
            .AddSource(Source)
            .ToolName(ToolName)
            .ToolVersion(Program.Version)
            .ExpectedRoots(TopicNames);
    }

    /// <summary>
    ///     Finds a root card by its name or one of its aliases.
    /// </summary>
    /// <param name="nameOrAlias">The name or alias; matched ignoring case.</param>
    /// <returns>The root card, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nameOrAlias"/> is null.</exception>
    public static HelpCard? Find(string nameOrAlias)
    {
        ArgumentNullException.ThrowIfNull(nameOrAlias);
        return Roots.FirstOrDefault(c =>
            string.Equals(c.Name, nameOrAlias, StringComparison.OrdinalIgnoreCase)
            || c.Aliases.Any(a => string.Equals(a, nameOrAlias, StringComparison.OrdinalIgnoreCase)));
    }
}

using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.MathMl;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.PlantUml;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Imaging;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Composition;
using DemaConsulting.HelpCards.Loading;

namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     The help cards of the drawing and file formats, as a DemaConsulting.HelpCards source and catalog, so a host can
///     give the knowledge to a person or an agent, or layer its own cards on it.
/// </summary>
/// <remarks>
///     <para>
///         The cards are Markdown files with YAML front matter embedded in this assembly (one file per card, in folders
///         that mirror the help tree, with the tiny example sources next to their cards); DemaConsulting.HelpCards reads,
///         validates, links, filters, searches, and renders them. This class only says which cards there are and what
///         the <c>{{name}}</c> tokens of the cards mean: <see cref="Source"/> is the source with the live limits of the
///         code as tokens, <see cref="Configure"/> adds it with the closed tag vocabulary of <see cref="HelpTags"/> to a
///         catalog builder, and <see cref="Catalog"/> is the catalog of the library cards alone.
///     </para>
///     <para>
///         A host that adds its own cards creates its own source that imports <see cref="Source"/> (so its overlays can
///         extend library cards with <c>extends:</c> and its links can name them) and builds one catalog:
///         <c>HelpCatalog.Create(b =&gt; CanvasNetHelp.Configure(b).AddSource(hostSource))</c>.
///     </para>
///     <para>
///         Every member is immutable and safe to use from several threads; <see cref="Catalog"/> is built once, on first
///         use.
///     </para>
/// </remarks>
/// <example>
///     Give an agent the Mermaid authoring and composition knowledge, without what the host controls:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Viewer.Help;
///
///     var view = CanvasNetHelp.Catalog.Filter([HelpTags.Mermaid], [HelpTags.Hosting]);
///     foreach (var root in view.Roots)
///     {
///         Console.WriteLine($"{root.Id}: {root.Summary}");
///     }
///     </code>
/// </example>
public static class CanvasNetHelp
{
    /// <summary>
    ///     The name of the library source: the <c>name</c> of its <c>_source.md</c>, which external links and the
    ///     <c>source</c> of the cards carry.
    /// </summary>
    public const string SourceName = "canvasnet-viewer";

    /// <summary>
    ///     Gets the identifiers of the root cards of the library, in the order they are listed: the ten formats, then
    ///     style, composition, positions, errors, limits, and files.
    /// </summary>
    /// <remarks>
    ///     <see cref="Catalog"/> is built with exactly these roots expected, so a root card cannot be added or removed
    ///     without this list (and the tests that go with it) being updated.
    /// </remarks>
    public static IReadOnlyList<string> RootIds { get; } = Array.AsReadOnly(
    [
        "scad", "vega-lite", "mermaid", "plantuml", "mathml", "svg", "pdf", "pptx", "vsdx", "raster",
        "style", "composition", "positions", "errors", "limits", "files"
    ]);

    /// <summary>
    ///     Gets the values of the <c>{{name}}</c> tokens of the library cards, taken from the constants of the code, so
    ///     the limits the cards state are always the limits of this build.
    /// </summary>
    /// <remarks>
    ///     The values are formatted with the invariant culture and without thousands separators.
    /// </remarks>
    internal static IReadOnlyDictionary<string, string> Tokens { get; } = CreateTokens();

    /// <summary>
    ///     Gets the source of the library cards: the cards embedded in this assembly, with <see cref="Tokens"/>.
    /// </summary>
    /// <remarks>
    ///     Creating the source reads nothing; the cards are read when a catalog is created from it. A host imports this
    ///     source into its own (<c>HelpSource.Imports</c>) to extend or link to the library cards.
    /// </remarks>
    public static HelpSource Source { get; } = HelpSource.FromAssembly(typeof(CanvasNetHelp).Assembly).WithTokens(Tokens);

    /// <summary>
    ///     The catalog of the library cards, built on first use (declared after the lists it reads, so they are initialized first).
    /// </summary>
    private static readonly Lazy<HelpCatalog> LibraryCatalog = new(
        () => HelpCatalog.Create(builder => Configure(builder).ExpectedRoots(RootIds)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     Gets the catalog of the library cards, built on first use and shared afterwards.
    /// </summary>
    /// <remarks>
    ///     Use <c>Filter</c> on it to give a reader only some tags (see <see cref="HelpTags"/>), <c>Find</c> to resolve a
    ///     path such as <c>mermaid/gantt</c>, and <c>Render</c> or <c>RenderIndex</c> to write cards as text, Markdown, or
    ///     JSON.
    /// </remarks>
    /// <exception cref="HelpCatalogException">
    ///     Thrown when an embedded card is defective; the unit tests build the catalog, so a defect fails the build rather
    ///     than a user.
    /// </exception>
    public static HelpCatalog Catalog => LibraryCatalog.Value;

    /// <summary>
    ///     Adds the library cards to a catalog builder, with the closed tag vocabulary of <see cref="HelpTags"/>.
    /// </summary>
    /// <remarks>
    ///     This is the one place that says how the library cards are composed, so the library catalog, a host's combined
    ///     catalog, and the validation tests all compose them the same way. Every card must have at least one scope tag
    ///     (<see cref="HelpTags.ScopeTags"/>) and only tags of <see cref="HelpTags.All"/>.
    /// </remarks>
    /// <param name="builder">The builder to configure. Must not be null.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static HelpCatalogBuilder Configure(HelpCatalogBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(Source).TagVocabulary(HelpTags.All, HelpTags.ScopeTags);
    }

    /// <summary>
    ///     Creates the token table from the constants of the code.
    /// </summary>
    /// <returns>The 36 token values.</returns>
    private static Dictionary<string, string> CreateTokens()
    {
        var scad = ScadLimits.Default;
        var vegaLite = VegaLiteLimits.Default;
        var mermaid = MermaidLimits.Default;
        var plantUml = PlantUmlLimits.Default;
        var mathMl = MathMlLimits.Default;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // The sizes, resolution, and quality of the imaging operations
            ["maxDimension"] = Format(Surface.MaxDimension),
            ["maxDpi"] = Format(SourceLoader.MaxDpi),
            ["qualityMin"] = Format(ImageWriter.MinQuality),
            ["qualityMax"] = Format(ImageWriter.MaxQuality),
            ["qualityDefault"] = Format(ImageWriter.DefaultQuality),

            // The resource limits of each format
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
            ["mmlMaxTokenLength"] = Format(mathMl.MaxTokenLength),
            ["mmlMaxLayoutWork"] = Format(mathMl.MaxLayoutWork),
            ["mmlMaxPaintedGlyphs"] = Format(mathMl.MaxPaintedGlyphs),

            // The limits of fragments and positions
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
}

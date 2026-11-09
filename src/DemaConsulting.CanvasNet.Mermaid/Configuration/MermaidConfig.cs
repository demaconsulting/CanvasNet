// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     The flowchart section of the effective configuration (Mermaid's <c>flowchart</c> keys that the flowchart reads).
/// </summary>
/// <param name="UseMaxWidth">Whether the SVG would be scaled to its container (no meaning for a raster; kept for completeness).</param>
/// <param name="TitleTopMargin">The margin above the diagram title.</param>
/// <param name="SubGraphTitleMarginTop">The margin above a subgraph title.</param>
/// <param name="SubGraphTitleMarginBottom">The margin below a subgraph title.</param>
/// <param name="DiagramPadding">The padding around the diagram.</param>
/// <param name="HtmlLabels">The deprecated <c>flowchart.htmlLabels</c>, or null when not set.</param>
/// <param name="NodeSpacing">The spacing between nodes of a rank.</param>
/// <param name="RankSpacing">The spacing between ranks.</param>
/// <param name="Curve">The default edge curve.</param>
/// <param name="Padding">The padding inside nodes and around labels.</param>
/// <param name="DefaultRenderer">The default renderer (<c>dagre-wrapper</c>, <c>dagre-d3</c> or <c>elk</c>).</param>
/// <param name="WrappingWidth">The width at which markdown labels wrap.</param>
/// <param name="InheritDir">Whether subgraphs inherit the direction of the diagram.</param>
/// <param name="ArrowMarkerAbsolute">The copy of <c>arrowMarkerAbsolute</c> that the flowchart's initialization makes.</param>
internal sealed record FlowchartConfig(
    bool UseMaxWidth,
    double TitleTopMargin,
    double SubGraphTitleMarginTop,
    double SubGraphTitleMarginBottom,
    double DiagramPadding,
    bool? HtmlLabels,
    double NodeSpacing,
    double RankSpacing,
    string Curve,
    double Padding,
    string DefaultRenderer,
    double WrappingWidth,
    bool InheritDir,
    bool ArrowMarkerAbsolute);

/// <summary>
///     The effective configuration of one measure or render call: Mermaid's current configuration after the site
///     configuration, the frontmatter and the directives are applied, with typed access to the keys this version reads.
/// </summary>
/// <remarks>
///     The values are validated: a key whose value had the wrong type was reported and dropped before the merge, so a
///     typed property always has a value of its type. <see cref="Raw"/> keeps every key, including those of other
///     diagram types. The object is built for one call and is not shared between threads.
/// </remarks>
internal sealed class MermaidConfig
{
    /// <summary>The locations of the layers that set each top-level key last.</summary>
    private readonly IReadOnlyDictionary<string, MermaidLocation> _locations;

    /// <summary>
    ///     Initializes the typed view of an effective configuration.
    /// </summary>
    /// <param name="raw">The effective configuration.</param>
    /// <param name="locations">The location of the layer that set each top-level key last.</param>
    public MermaidConfig(JsonObject raw, IReadOnlyDictionary<string, MermaidLocation> locations)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(locations);
        Raw = raw;
        _locations = locations;
        ThemeVariables = raw["themeVariables"] as JsonObject ?? [];
        var flowchart = raw["flowchart"] as JsonObject ?? [];
        var margin = flowchart["subGraphTitleMargin"] as JsonObject ?? [];
        Flowchart = new FlowchartConfig(
            Boolean(flowchart, "useMaxWidth", true),
            Number(flowchart, "titleTopMargin", 25),
            Number(margin, "top", 0),
            Number(margin, "bottom", 0),
            Number(flowchart, "diagramPadding", 8),
            flowchart["htmlLabels"] is JsonValue html && html.TryGetValue<bool>(out var flag) ? flag : null,
            Number(flowchart, "nodeSpacing", 50),
            Number(flowchart, "rankSpacing", 50),
            Text(flowchart, "curve") ?? "basis",
            Number(flowchart, "padding", 15),
            Text(flowchart, "defaultRenderer") ?? "dagre-wrapper",
            Number(flowchart, "wrappingWidth", 200),
            Boolean(flowchart, "inheritDir", false),
            Boolean(flowchart, "arrowMarkerAbsolute", false));
    }

    /// <summary>Gets the effective configuration as JSON (Mermaid's <c>getConfig()</c>).</summary>
    public JsonObject Raw { get; }

    /// <summary>Gets the theme name as configured (it may name a theme this version does not have; the variables tell).</summary>
    public string Theme => Text(Raw, "theme") ?? "default";

    /// <summary>Gets the theme variables in effect.</summary>
    public JsonObject ThemeVariables { get; }

    /// <summary>Gets the configured font family (top level), or null.</summary>
    public string? FontFamily => Text(Raw, "fontFamily");

    /// <summary>Gets the configured alternative font family, or null.</summary>
    public string? AltFontFamily => Text(Raw, "altFontFamily");

    /// <summary>Gets the configured font size (top level).</summary>
    public double FontSize => Number(Raw, "fontSize", 16);

    /// <summary>Gets the look (<c>classic</c>, or another look that is drawn classic).</summary>
    public string Look => Text(Raw, "look") ?? "classic";

    /// <summary>Gets the layout (<c>dagre</c>, or another layout that falls back to dagre).</summary>
    public string Layout => Text(Raw, "layout") ?? "dagre";

    /// <summary>Gets the user style sheet (<c>themeCSS</c>), or null.</summary>
    public string? ThemeCss => Text(Raw, "themeCSS");

    /// <summary>Gets a value indicating whether labels follow the HTML-label rules (Mermaid's <c>getEffectiveHtmlLabels</c>).</summary>
    public bool HtmlLabels => Boolean(Raw, "htmlLabels", Flowchart.HtmlLabels ?? true);

    /// <summary>Gets a value indicating whether markdown labels wrap automatically.</summary>
    public bool MarkdownAutoWrap => Boolean(Raw, "markdownAutoWrap", true);

    /// <summary>Gets the <c>wrap</c> setting (a <c>%%{wrap}%%</c> directive sets it), or null when not set.</summary>
    public bool? Wrap => Raw["wrap"] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    /// <summary>Gets the security level (only <c>strict</c> behavior exists in this version).</summary>
    public string SecurityLevel => Text(Raw, "securityLevel") ?? "strict";

    /// <summary>Gets Mermaid's <c>maxTextSize</c>.</summary>
    public double MaxTextSize => Number(Raw, "maxTextSize", 50000);

    /// <summary>Gets Mermaid's <c>maxEdges</c>.</summary>
    public double MaxEdges => Number(Raw, "maxEdges", 500);

    /// <summary>Gets a value indicating whether element ids are deterministic.</summary>
    public bool DeterministicIds => Boolean(Raw, "deterministicIds", false);

    /// <summary>Gets the flowchart section.</summary>
    public FlowchartConfig Flowchart { get; }

    /// <summary>
    ///     Gets the detector settings of this configuration: the <c>defaultRenderer</c> of the flowchart, class and
    ///     state sections, which Mermaid's detectors read.
    /// </summary>
    public DetectorSettings DetectorSettings => new(
        Flowchart.DefaultRenderer,
        Text(Raw["class"] as JsonObject, "defaultRenderer") ?? DetectorSettings.Default.ClassRenderer,
        Text(Raw["state"] as JsonObject, "defaultRenderer") ?? DetectorSettings.Default.StateRenderer);

    /// <summary>
    ///     Gets a theme variable as text (a number is formatted as JavaScript would), or null when it is not set.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or null.</returns>
    public string? ThemeVariable(string name)
    {
        if (ThemeVariables[name] is not JsonValue value)
        {
            return null;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Number => ColorMath.FormatNumber(value.GetValue<double>()),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    /// <summary>
    ///     Gets the location of the layer that set a top-level key last (a frontmatter key, a directive), or an
    ///     unknown location for the defaults and the site configuration.
    /// </summary>
    /// <param name="key">The top-level key.</param>
    /// <returns>The location.</returns>
    public MermaidLocation LocationOf(string key) => _locations.TryGetValue(key, out var location) ? location : default;

    /// <summary>
    ///     Reads a text value.
    /// </summary>
    /// <param name="obj">The object, or null.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text, or null when absent or not a string.</returns>
    private static string? Text(JsonObject? obj, string key) =>
        obj?[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>
    ///     Reads a number value.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value when absent or not a finite number.</param>
    /// <returns>The number.</returns>
    private static double Number(JsonObject obj, string key, double fallback) =>
        obj[key] is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number) ? number : fallback;

    /// <summary>
    ///     Reads a Boolean value.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value when absent or not a Boolean.</param>
    /// <returns>The value.</returns>
    private static bool Boolean(JsonObject obj, string key, bool fallback) =>
        obj[key] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : fallback;
}

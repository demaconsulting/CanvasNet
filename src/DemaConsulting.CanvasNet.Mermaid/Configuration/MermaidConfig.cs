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
///     The class section of the effective configuration, with the keys of the state section that Mermaid's class
///     renderer reads in their place.
/// </summary>
/// <remarks>
///     <para>
///         The defaults are those of <c>defaultConfig.ts</c>, which replaces the schema's class section with three keys
///         (<c>defaultRenderer</c>, <c>hideEmptyMembersBox</c> and <c>hierarchicalNamespaces</c>) and deliberately
///         leaves <c>padding</c> undefined, so the class box, namespace and note paddings keep their own fallbacks
///         (12, 16 and 6).
///     </para>
///     <para>
///         Mermaid's <c>classRenderer-v3-unified.ts</c> takes <c>const { state: conf } = getConfig()</c>, so the spacing
///         of a class diagram is <c>state.nodeSpacing || 50</c> and <c>state.rankSpacing || 50</c> (zero falls back),
///         and its title margin is <c>state.titleTopMargin ?? 25</c> (zero is kept).
///     </para>
/// </remarks>
internal sealed record ClassConfig
{
    /// <summary>Gets the default renderer, which the class detectors read (<c>dagre-wrapper</c> selects <c>classDiagram</c>).</summary>
    public string DefaultRenderer { get; init; } = "dagre-wrapper";

    /// <summary>Gets a value indicating whether a class without members and methods is drawn without its empty member box.</summary>
    public bool HideEmptyMembersBox { get; init; }

    /// <summary>Gets a value indicating whether dotted namespace names make nested namespaces.</summary>
    public bool HierarchicalNamespaces { get; init; } = true;

    /// <summary>Gets the configured <c>class.padding</c>, or null when it is not set (each use then has its own fallback).</summary>
    public double? Padding { get; init; }

    /// <summary>Gets the spacing between the nodes of a rank (<c>state.nodeSpacing || 50</c>).</summary>
    public double NodeSpacing { get; init; } = 50;

    /// <summary>Gets the spacing between ranks (<c>state.rankSpacing || 50</c>).</summary>
    public double RankSpacing { get; init; } = 50;

    /// <summary>Gets the margin above the diagram title (<c>state.titleTopMargin ?? 25</c>).</summary>
    public double TitleTopMargin { get; init; } = 25;
}

/// <summary>
///     A configuration value that Mermaid accepts as a number or a string (a font size or a font weight), kept as
///     JavaScript holds it, because Mermaid's <c>parseFontSize</c> and its memoization keys treat the two kinds
///     differently.
/// </summary>
/// <param name="Text">The value as JavaScript's <c>String(value)</c> writes it.</param>
/// <param name="IsNumber">Whether the value is a number rather than a string.</param>
internal readonly record struct ConfigScalar(string Text, bool IsNumber)
{
    /// <summary>
    ///     Creates the value of a number.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The value, with the number's JavaScript text.</returns>
    public static ConfigScalar FromNumber(double number) => new(ColorMath.FormatNumber(number), true);

    /// <summary>
    ///     Creates the value of a string.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <returns>The value.</returns>
    public static ConfigScalar FromText(string text) => new(text, false);

    /// <summary>
    ///     Reads a JSON number or string.
    /// </summary>
    /// <param name="node">The JSON node, or null.</param>
    /// <returns>The value, or null when the node is neither a finite number nor a string.</returns>
    public static ConfigScalar? FromJson(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.String => FromText(value.GetValue<string>()),
            JsonValueKind.Number when double.IsFinite(value.GetValue<double>()) => FromNumber(value.GetValue<double>()),
            _ => null,
        };
    }

    /// <summary>
    ///     Gets whether the value is truthy in JavaScript: a non-empty string, or a number other than zero.
    /// </summary>
    public bool IsTruthy => IsNumber ? Text is not ("0" or "NaN") : Text.Length > 0;

    /// <summary>
    ///     Gets the value as JavaScript's <c>String(value)</c> writes it (Mermaid's template strings).
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString() => Text;
}

/// <summary>
///     The font of one kind of sequence text: Mermaid's <c>actorFont</c>, <c>noteFont</c> and <c>messageFont</c>, the
///     <c>{ fontFamily, fontSize, fontWeight }</c> objects that the sequence renderer measures and draws text with.
/// </summary>
/// <param name="Family">The CSS font family list.</param>
/// <param name="Size">The font size (a number of pixels, or a CSS size string).</param>
/// <param name="Weight">The font weight (a number, or a CSS weight string).</param>
internal sealed record MermaidFontConfig(string Family, ConfigScalar Size, ConfigScalar Weight);

/// <summary>
///     The sequence section of the effective configuration: Mermaid's <c>sequence</c> keys as the sequence renderer reads
///     them, after its <c>setConf</c> overrides and with the effective wrap setting.
/// </summary>
/// <remarks>
///     <para>
///         The defaults are those of Mermaid's <c>SequenceDiagramConfig</c>. The fonts are those of the renderer's
///         <c>setConf</c>, which assigns the top-level configuration over the section: a truthy top-level
///         <c>fontFamily</c>, <c>fontSize</c> or <c>fontWeight</c> replaces the family, size or weight of all three fonts.
///         The top-level <c>fontFamily</c> and <c>fontSize</c> always have defaults, so the section's own families and sizes
///         only matter when the top level sets them to an empty string or zero; its weights apply unless the top level
///         sets <c>fontWeight</c>.
///     </para>
///     <para>
///         <see cref="Wrap"/> is the value that Mermaid's sequence database falls back to for every text without a
///         <c>wrap:</c> or <c>nowrap:</c> prefix (its <c>autoWrap</c>): <c>sequence.wrap</c>, which the diagram's
///         <c>init</c> sets to true when the top-level <c>wrap</c> is true, and false when neither is set. The instance is
///         immutable.
///     </para>
/// </remarks>
internal sealed record SequenceConfig
{
    /// <summary>The default family of the actor font.</summary>
    internal const string DefaultActorFontFamily = "\"Open Sans\", sans-serif";

    /// <summary>The default family of the note and message fonts.</summary>
    internal const string DefaultTextFontFamily = "\"trebuchet ms\", verdana, arial, sans-serif";

    /// <summary>Gets a value indicating whether the SVG would be scaled to its container (no meaning for a raster).</summary>
    public bool UseMaxWidth { get; init; } = true;

    /// <summary>Gets a value indicating whether participants without messages are hidden.</summary>
    public bool HideUnusedParticipants { get; init; }

    /// <summary>Gets the width of an activation box.</summary>
    public double ActivationWidth { get; init; } = 10;

    /// <summary>Gets the margin to the left and right of the diagram.</summary>
    public double DiagramMarginX { get; init; } = 50;

    /// <summary>Gets the margin above and below the diagram.</summary>
    public double DiagramMarginY { get; init; } = 10;

    /// <summary>Gets the margin between actors.</summary>
    public double ActorMargin { get; init; } = 50;

    /// <summary>Gets the width of an actor box.</summary>
    public double Width { get; init; } = 150;

    /// <summary>Gets the height of an actor box.</summary>
    public double Height { get; init; } = 65;

    /// <summary>Gets the margin around boxes and blocks.</summary>
    public double BoxMargin { get; init; } = 10;

    /// <summary>Gets the margin around the text of a box.</summary>
    public double BoxTextMargin { get; init; } = 5;

    /// <summary>Gets the margin around notes.</summary>
    public double NoteMargin { get; init; } = 10;

    /// <summary>Gets the space between messages (not read by Mermaid's renderer; kept for completeness).</summary>
    public double MessageMargin { get; init; } = 35;

    /// <summary>Gets the alignment of message text (<c>left</c>, <c>center</c> or <c>right</c>).</summary>
    public string MessageAlign { get; init; } = "center";

    /// <summary>Gets a value indicating whether the actors are repeated below the diagram.</summary>
    public bool MirrorActors { get; init; } = true;

    /// <summary>Gets a value indicating whether the actor menus are always shown.</summary>
    public bool ForceMenus { get; init; }

    /// <summary>Gets the extra margin at the bottom of the diagram.</summary>
    public double BottomMarginAdj { get; init; } = 1;

    /// <summary>Gets a value indicating whether self messages are drawn with right angles.</summary>
    public bool RightAngles { get; init; }

    /// <summary>Gets a value indicating whether messages are numbered.</summary>
    public bool ShowSequenceNumbers { get; init; }

    /// <summary>Gets the font of actor labels, after the top-level overrides.</summary>
    public MermaidFontConfig ActorFont { get; init; } =
        new(DefaultActorFontFamily, ConfigScalar.FromNumber(14), ConfigScalar.FromNumber(400));

    /// <summary>Gets the font of notes, after the top-level overrides.</summary>
    public MermaidFontConfig NoteFont { get; init; } =
        new(DefaultTextFontFamily, ConfigScalar.FromNumber(14), ConfigScalar.FromNumber(400));

    /// <summary>Gets the alignment of note text (<c>left</c>, <c>center</c> or <c>right</c>).</summary>
    public string NoteAlign { get; init; } = "center";

    /// <summary>Gets the font of messages (and of block and box titles), after the top-level overrides.</summary>
    public MermaidFontConfig MessageFont { get; init; } =
        new(DefaultTextFontFamily, ConfigScalar.FromNumber(16), ConfigScalar.FromNumber(400));

    /// <summary>Gets a value indicating whether texts without a <c>wrap:</c> or <c>nowrap:</c> prefix wrap.</summary>
    public bool Wrap { get; init; }

    /// <summary>Gets the padding around wrapped text.</summary>
    public double WrapPadding { get; init; } = 10;

    /// <summary>Gets the width of the label box of a block.</summary>
    public double LabelBoxWidth { get; init; } = 50;

    /// <summary>Gets the height of the label box of a block.</summary>
    public double LabelBoxHeight { get; init; } = 20;

    /// <summary>Gets the actor text placement (<c>fo</c>, <c>old</c>, or null for the default <c>tspan</c> placement).</summary>
    public string? TextPlacement { get; init; }

    /// <summary>Gets the <c>arrowMarkerAbsolute</c> that <c>setConf</c> copies from the top level (markers use plain ids).</summary>
    public bool ArrowMarkerAbsolute { get; init; }
}

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
        Sequence = ReadSequence(raw);
        Class = ReadClass(raw);
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

    /// <summary>Gets the sequence section, as the sequence renderer reads it.</summary>
    public SequenceConfig Sequence { get; }

    /// <summary>Gets the class section, as the class database and renderer read it.</summary>
    public ClassConfig Class { get; }

    /// <summary>
    ///     Gets the detector settings of this configuration: the <c>defaultRenderer</c> of the flowchart, class and
    ///     state sections, which Mermaid's detectors read.
    /// </summary>
    public DetectorSettings DetectorSettings => new(
        Flowchart.DefaultRenderer,
        Class.DefaultRenderer,
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
    ///     Reads the sequence section as the sequence renderer sees it: the section's values over Mermaid's defaults, then
    ///     the top-level font overrides of <c>setConf</c> and the wrap setting of the diagram's <c>init</c>.
    /// </summary>
    /// <param name="raw">The effective configuration.</param>
    /// <returns>The sequence section.</returns>
    private static SequenceConfig ReadSequence(JsonObject raw)
    {
        var sequence = raw["sequence"] as JsonObject ?? [];
        var defaults = new SequenceConfig();

        // setConf: a truthy top-level fontFamily, fontSize or fontWeight replaces those of all three fonts
        var family = Text(raw, "fontFamily") is { Length: > 0 } topFamily ? topFamily : null;
        var size = ConfigScalar.FromJson(raw["fontSize"]) is { IsNumber: true, IsTruthy: true } topSize ? topSize : (ConfigScalar?)null;
        var weight = ConfigScalar.FromJson(raw["fontWeight"]) is { IsTruthy: true } topWeight ? topWeight : (ConfigScalar?)null;
        MermaidFontConfig Font(string prefix, MermaidFontConfig fallback) => new(
            family ?? Text(sequence, prefix + "FontFamily") ?? fallback.Family,
            size ?? ConfigScalar.FromJson(sequence[prefix + "FontSize"]) ?? fallback.Size,
            weight ?? ConfigScalar.FromJson(sequence[prefix + "FontWeight"]) ?? fallback.Weight);

        return new SequenceConfig
        {
            UseMaxWidth = Boolean(sequence, "useMaxWidth", defaults.UseMaxWidth),
            HideUnusedParticipants = Boolean(sequence, "hideUnusedParticipants", defaults.HideUnusedParticipants),
            ActivationWidth = Number(sequence, "activationWidth", defaults.ActivationWidth),
            DiagramMarginX = Number(sequence, "diagramMarginX", defaults.DiagramMarginX),
            DiagramMarginY = Number(sequence, "diagramMarginY", defaults.DiagramMarginY),
            ActorMargin = Number(sequence, "actorMargin", defaults.ActorMargin),
            Width = Number(sequence, "width", defaults.Width),
            Height = Number(sequence, "height", defaults.Height),
            BoxMargin = Number(sequence, "boxMargin", defaults.BoxMargin),
            BoxTextMargin = Number(sequence, "boxTextMargin", defaults.BoxTextMargin),
            NoteMargin = Number(sequence, "noteMargin", defaults.NoteMargin),
            MessageMargin = Number(sequence, "messageMargin", defaults.MessageMargin),
            MessageAlign = Text(sequence, "messageAlign") ?? defaults.MessageAlign,
            MirrorActors = Boolean(sequence, "mirrorActors", defaults.MirrorActors),
            ForceMenus = Boolean(sequence, "forceMenus", defaults.ForceMenus),
            BottomMarginAdj = Number(sequence, "bottomMarginAdj", defaults.BottomMarginAdj),
            RightAngles = Boolean(sequence, "rightAngles", defaults.RightAngles),
            ShowSequenceNumbers = Boolean(sequence, "showSequenceNumbers", defaults.ShowSequenceNumbers),
            ActorFont = Font("actor", defaults.ActorFont),
            NoteFont = Font("note", defaults.NoteFont),
            NoteAlign = Text(sequence, "noteAlign") ?? defaults.NoteAlign,
            MessageFont = Font("message", defaults.MessageFont),

            // The diagram's init copies a true top-level wrap into sequence.wrap; the database falls back to sequence.wrap
            Wrap = Boolean(raw, "wrap", false) || Boolean(sequence, "wrap", defaults.Wrap),
            WrapPadding = Number(sequence, "wrapPadding", defaults.WrapPadding),
            LabelBoxWidth = Number(sequence, "labelBoxWidth", defaults.LabelBoxWidth),
            LabelBoxHeight = Number(sequence, "labelBoxHeight", defaults.LabelBoxHeight),
            TextPlacement = Text(sequence, "textPlacement"),

            // setConf assigns every top-level key over the section, arrowMarkerAbsolute (which has a default) included
            ArrowMarkerAbsolute = Boolean(raw, "arrowMarkerAbsolute", Boolean(sequence, "arrowMarkerAbsolute", false)),
        };
    }

    /// <summary>
    ///     Reads the class section as the class database and renderer see it: the section's values over the defaults of
    ///     <c>defaultConfig.ts</c>, and the spacing and title margin from the state section.
    /// </summary>
    /// <param name="raw">The effective configuration.</param>
    /// <returns>The class section.</returns>
    private static ClassConfig ReadClass(JsonObject raw)
    {
        var section = raw["class"] as JsonObject ?? [];
        var state = raw["state"] as JsonObject ?? [];
        var defaults = new ClassConfig();

        // conf.nodeSpacing || 50: a missing or zero spacing falls back
        double OrDefault(double value, double fallback) => value != 0 ? value : fallback;
        return new ClassConfig
        {
            DefaultRenderer = Text(section, "defaultRenderer") ?? defaults.DefaultRenderer,
            HideEmptyMembersBox = Boolean(section, "hideEmptyMembersBox", defaults.HideEmptyMembersBox),
            HierarchicalNamespaces = Boolean(section, "hierarchicalNamespaces", defaults.HierarchicalNamespaces),
            Padding = section["padding"] is JsonValue padding && padding.TryGetValue<double>(out var value) && double.IsFinite(value) ? value : null,
            NodeSpacing = OrDefault(Number(state, "nodeSpacing", defaults.NodeSpacing), defaults.NodeSpacing),
            RankSpacing = OrDefault(Number(state, "rankSpacing", defaults.RankSpacing), defaults.RankSpacing),
            TitleTopMargin = Number(state, "titleTopMargin", defaults.TitleTopMargin),
        };
    }

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

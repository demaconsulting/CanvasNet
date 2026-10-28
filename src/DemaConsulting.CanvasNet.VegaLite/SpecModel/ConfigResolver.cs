// Portions derived from Vega-Lite and Vega (BSD-3-Clause); see third-party/README.md
// cspell:ignore tableau yellowgreenblue blueorange mercator symlog hcl dsv
// cspell:ignore geoshape scenegraph
using System.Globalization;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     The kind of value a <see cref="ConfigNode"/> holds.
/// </summary>
internal enum ConfigNodeKind
{
    /// <summary>JSON <c>null</c>.</summary>
    Null,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A string.</summary>
    String,

    /// <summary>An array of nodes.</summary>
    Array,

    /// <summary>An object: named properties.</summary>
    Object,
}

/// <summary>
///     One value of a configuration tree: an immutable JSON-like node with typed accessors and dotted-path lookup.
/// </summary>
/// <remarks>
///     Configuration comes from three places (the built-in defaults, the <c>config</c> of the specification and the
///     caller's configuration text) that all end up as <see cref="ConfigNode"/> trees so that they can be merged and
///     read in one way. Unlike <see cref="SpecNode"/> a configuration node carries no location: the effective value
///     of a setting has no single place in the text. The type is immutable and safe for concurrent use.
/// </remarks>
internal sealed class ConfigNode
{
    /// <summary>The shared empty list of items.</summary>
    private static readonly ConfigNode[] NoItems = [];

    /// <summary>The shared empty property table.</summary>
    private static readonly Dictionary<string, ConfigNode> NoProperties = [];

    /// <summary>The shared null node.</summary>
    private static readonly ConfigNode NullNode = new(ConfigNodeKind.Null, null, 0, false, NoItems, NoProperties);

    /// <summary>The elements of an array node.</summary>
    private readonly ConfigNode[] _items;

    /// <summary>The properties of an object node.</summary>
    private readonly Dictionary<string, ConfigNode> _properties;

    /// <summary>
    ///     Initializes a node.
    /// </summary>
    /// <param name="kind">The kind of value.</param>
    /// <param name="text">The text of a string node.</param>
    /// <param name="number">The value of a number node.</param>
    /// <param name="flag">The value of a boolean node.</param>
    /// <param name="items">The elements of an array node.</param>
    /// <param name="properties">The properties of an object node.</param>
    private ConfigNode(ConfigNodeKind kind, string? text, double number, bool flag, ConfigNode[] items, Dictionary<string, ConfigNode> properties)
    {
        Kind = kind;
        StringValue = text;
        NumberValue = number;
        BoolValue = flag;
        _items = items;
        _properties = properties;
    }

    /// <summary>Gets the kind of value.</summary>
    public ConfigNodeKind Kind { get; }

    /// <summary>Gets the text of a string node; null for other kinds.</summary>
    public string? StringValue { get; }

    /// <summary>Gets the value of a number node; zero for other kinds.</summary>
    public double NumberValue { get; }

    /// <summary>Gets the value of a boolean node; false for other kinds.</summary>
    public bool BoolValue { get; }

    /// <summary>Gets the elements of an array node; empty for other kinds.</summary>
    public IReadOnlyList<ConfigNode> Items => _items;

    /// <summary>Gets the properties of an object node (ordinal names, in the order they were added); empty for other kinds.</summary>
    public IReadOnlyDictionary<string, ConfigNode> Properties => _properties;

    /// <summary>Gets a value indicating whether this node is an object.</summary>
    public bool IsObject => Kind == ConfigNodeKind.Object;

    /// <summary>Gets the shared empty object.</summary>
    public static ConfigNode EmptyObject { get; } = new(ConfigNodeKind.Object, null, 0, false, NoItems, NoProperties);

    /// <summary>
    ///     Gets a property of an object node.
    /// </summary>
    /// <param name="name">The property name (ordinal).</param>
    /// <returns>The property value, or <see langword="null"/> when this is not an object or has no such property.</returns>
    public ConfigNode? this[string name] =>
        Kind == ConfigNodeKind.Object && _properties.TryGetValue(name, out var value) ? value : null;

    /// <summary>
    ///     Gets the text of this node when it is a string.
    /// </summary>
    /// <returns>The text, or <see langword="null"/> for any other kind.</returns>
    public string? AsString() => Kind == ConfigNodeKind.String ? StringValue : null;

    /// <summary>
    ///     Gets the value of this node when it is a number.
    /// </summary>
    /// <returns>The number, or <see langword="null"/> for any other kind.</returns>
    public double? AsNumber() => Kind == ConfigNodeKind.Number ? NumberValue : null;

    /// <summary>
    ///     Gets the value of this node when it is a boolean.
    /// </summary>
    /// <returns>The boolean, or <see langword="null"/> for any other kind.</returns>
    public bool? AsBool() => Kind == ConfigNodeKind.Boolean ? BoolValue : null;

    /// <summary>
    ///     Looks up a descendant by a dotted path such as <c>view.continuousWidth</c> or <c>style.guide-label.fontSize</c>.
    /// </summary>
    /// <remarks>
    ///     A path is a list of property names separated by dots; names that contain a dot cannot be addressed (the
    ///     configuration has none). A path that runs through a node that is not an object finds nothing.
    /// </remarks>
    /// <param name="path">The dotted path. Must not be null.</param>
    /// <returns>The node, or <see langword="null"/> when the path does not exist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    public ConfigNode? Get(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var node = this;
        foreach (var segment in path.Split('.'))
        {
            var next = node[segment];
            if (next is null)
            {
                return null;
            }

            node = next;
        }

        return node;
    }

    /// <summary>
    ///     Creates an object node from named values (copied).
    /// </summary>
    /// <param name="properties">The properties.</param>
    /// <returns>The node.</returns>
    public static ConfigNode Create(IReadOnlyDictionary<string, ConfigNode> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return new ConfigNode(ConfigNodeKind.Object, null, 0, false, NoItems, new Dictionary<string, ConfigNode>(properties, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Creates a string node.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The node.</returns>
    public static ConfigNode FromString(string text) => new(ConfigNodeKind.String, text, 0, false, NoItems, NoProperties);

    /// <summary>
    ///     Creates a node from the <c>config</c> object of a parsed specification.
    /// </summary>
    /// <remarks>
    ///     Recursion is bounded by the nesting depth the parser accepted (at most 256 levels).
    /// </remarks>
    /// <param name="node">The specification node.</param>
    /// <returns>The configuration node.</returns>
    public static ConfigNode FromSpec(SpecNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        switch (node.Kind)
        {
            case SpecNodeKind.String:
                return FromString(node.StringValue!);
            case SpecNodeKind.Number:
                return new ConfigNode(ConfigNodeKind.Number, null, node.NumberValue, false, NoItems, NoProperties);
            case SpecNodeKind.Boolean:
                return new ConfigNode(ConfigNodeKind.Boolean, null, 0, node.BoolValue, NoItems, NoProperties);
            case SpecNodeKind.Array:
                return new ConfigNode(ConfigNodeKind.Array, null, 0, false, node.Items.Select(FromSpec).ToArray(), NoProperties);
            case SpecNodeKind.Object:
                var properties = new Dictionary<string, ConfigNode>(StringComparer.Ordinal);
                foreach (var property in node.Properties)
                {
                    properties[property.Name!] = FromSpec(property);
                }

                return new ConfigNode(ConfigNodeKind.Object, null, 0, false, NoItems, properties);
            default:
                return NullNode;
        }
    }

    /// <summary>
    ///     Creates a node from a parsed JSON element.
    /// </summary>
    /// <remarks>
    ///     Recursion is bounded by the nesting depth of the document (the caller parses with a depth limit).
    /// </remarks>
    /// <param name="element">The element.</param>
    /// <returns>The configuration node.</returns>
    public static ConfigNode FromJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return FromString(element.GetString()!);
            case JsonValueKind.Number:
                return new ConfigNode(ConfigNodeKind.Number, null, element.GetDouble(), false, NoItems, NoProperties);
            case JsonValueKind.True or JsonValueKind.False:
                return new ConfigNode(ConfigNodeKind.Boolean, null, 0, element.GetBoolean(), NoItems, NoProperties);
            case JsonValueKind.Array:
                return new ConfigNode(ConfigNodeKind.Array, null, 0, false, element.EnumerateArray().Select(FromJson).ToArray(), NoProperties);
            case JsonValueKind.Object:
                var properties = new Dictionary<string, ConfigNode>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    properties[property.Name] = FromJson(property.Value);
                }

                return new ConfigNode(ConfigNodeKind.Object, null, 0, false, NoItems, properties);
            default:
                return NullNode;
        }
    }

    /// <summary>
    ///     Merges an overlay over a base: objects are merged property by property (recursively); every other value,
    ///     including an array, is replaced by the overlay's.
    /// </summary>
    /// <remarks>
    ///     Neither argument is modified. Recursion is bounded by the depth of the trees, which are limited when they are
    ///     built.
    /// </remarks>
    /// <param name="baseNode">The lower-precedence value.</param>
    /// <param name="overlay">The higher-precedence value.</param>
    /// <returns>The merged value.</returns>
    public static ConfigNode Merge(ConfigNode baseNode, ConfigNode overlay)
    {
        ArgumentNullException.ThrowIfNull(baseNode);
        ArgumentNullException.ThrowIfNull(overlay);
        if (baseNode.Kind != ConfigNodeKind.Object || overlay.Kind != ConfigNodeKind.Object)
        {
            return overlay;
        }

        var merged = new Dictionary<string, ConfigNode>(baseNode._properties, StringComparer.Ordinal);
        foreach (var (name, value) in overlay._properties)
        {
            merged[name] = merged.TryGetValue(name, out var existing) ? Merge(existing, value) : value;
        }

        return new ConfigNode(ConfigNodeKind.Object, null, 0, false, NoItems, merged);
    }
}

/// <summary>
///     The effective configuration of one chart: the Vega-Lite version 6 defaults, overridden by the specification's
///     <c>config</c>, overridden by the caller's configuration, with the lookups that the later stages need.
/// </summary>
/// <remarks>
///     The instance is immutable and safe for concurrent use. Plain settings are read with <see cref="Get"/> (and the
///     typed getters) from <see cref="Root"/>, the merged tree. Vega-Lite resolves some settings through
///     precedence chains instead of one path; those chains are implemented once here so that every stage agrees:
///     <see cref="ResolveMarkProperty"/>, <see cref="ResolveViewProperty"/>, <see cref="ResolveStyleProperty"/> and
///     <see cref="ResolveTitleProperty"/>.
/// </remarks>
internal sealed class ResolvedConfig
{
    /// <summary>
    ///     Initializes a resolved configuration.
    /// </summary>
    /// <param name="root">The merged tree: Vega defaults (guides only), Vega-Lite defaults, specification, caller.</param>
    /// <param name="vegaLite">The Vega-Lite layer: Vega-Lite defaults, specification, caller.</param>
    /// <param name="vega">The Vega defaults, including the mark-type defaults.</param>
    internal ResolvedConfig(ConfigNode root, ConfigNode vegaLite, ConfigNode vega)
    {
        Root = root;
        VegaLite = vegaLite;
        Vega = vega;
    }

    /// <summary>Gets the merged tree: the effective value of every setting.</summary>
    public ConfigNode Root { get; }

    /// <summary>Gets the Vega-Lite layer (defaults, specification and caller; without the Vega defaults).</summary>
    internal ConfigNode VegaLite { get; }

    /// <summary>Gets the Vega defaults, including the defaults of the Vega mark types and styles.</summary>
    internal ConfigNode Vega { get; }

    /// <summary>
    ///     Looks up a setting by dotted path in the merged tree.
    /// </summary>
    /// <param name="path">The dotted path, for example <c>view.continuousWidth</c> or <c>axis.tickSize</c>.</param>
    /// <returns>The node, or <see langword="null"/> when the setting is not defined.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    public ConfigNode? Get(string path) => Root.Get(path);

    /// <summary>
    ///     Reads a number setting.
    /// </summary>
    /// <param name="path">The dotted path.</param>
    /// <returns>The number, or <see langword="null"/> when the setting is missing or not a number.</returns>
    public double? GetNumber(string path) => Get(path)?.AsNumber();

    /// <summary>
    ///     Reads a string setting.
    /// </summary>
    /// <param name="path">The dotted path.</param>
    /// <returns>The text, or <see langword="null"/> when the setting is missing or not a string.</returns>
    public string? GetString(string path) => Get(path)?.AsString();

    /// <summary>
    ///     Reads a boolean setting.
    /// </summary>
    /// <param name="path">The dotted path.</param>
    /// <returns>The value, or <see langword="null"/> when the setting is missing or not a boolean.</returns>
    public bool? GetBool(string path) => Get(path)?.AsBool();

    /// <summary>
    ///     Reads an object setting.
    /// </summary>
    /// <param name="path">The dotted path.</param>
    /// <param name="value">Receives the object, or null.</param>
    /// <returns><see langword="true"/> when the setting is an object.</returns>
    public bool TryGetObject(string path, out ConfigNode value)
    {
        var node = Get(path);
        value = node ?? ConfigNode.EmptyObject;
        return node is { IsObject: true };
    }

    /// <summary>
    ///     Reads an array setting.
    /// </summary>
    /// <param name="path">The dotted path.</param>
    /// <param name="items">Receives the elements, or an empty list.</param>
    /// <returns><see langword="true"/> when the setting is an array.</returns>
    public bool TryGetArray(string path, out IReadOnlyList<ConfigNode> items)
    {
        var node = Get(path);
        items = node is { Kind: ConfigNodeKind.Array } ? node.Items : [];
        return node is { Kind: ConfigNodeKind.Array };
    }

    /// <summary>
    ///     Resolves a mark property the way Vega-Lite does: mark definition aside, the first of these that defines it.
    /// </summary>
    /// <remarks>
    ///     <list type="number">
    ///         <item><description><c>config.style.&lt;name&gt;</c> for the mark type and then each name in <paramref name="styles"/> (a later name overrides an earlier one);</description></item>
    ///         <item><description>the mark-specific configuration <c>config.&lt;markType&gt;</c> (for example <c>config.bar</c>);</description></item>
    ///         <item><description>the general mark configuration <c>config.mark</c>;</description></item>
    ///         <item><description>the default Vega styles of the same names (for example the <c>point</c> style with size 30);</description></item>
    ///         <item><description>the defaults of the underlying Vega mark (<c>symbol</c> for point, circle and square; <c>rect</c> for bar, rect and tick; <c>rule</c>; <c>text</c>).</description></item>
    ///     </list>
    ///     A value that the mark definition itself gives (<c>mark.size</c>) always wins and is the caller's to check first.
    /// </remarks>
    /// <param name="markType">The mark type, such as <c>bar</c>.</param>
    /// <param name="styles">The names in the mark definition's <c>style</c>, or null.</param>
    /// <param name="property">The property name, such as <c>size</c>.</param>
    /// <returns>The value, or <see langword="null"/> when no level defines it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markType"/> or <paramref name="property"/> is null.</exception>
    public ConfigNode? ResolveMarkProperty(string markType, IReadOnlyList<string>? styles, string property)
    {
        ArgumentNullException.ThrowIfNull(markType);
        ArgumentNullException.ThrowIfNull(property);
        var names = new List<string> { markType };
        if (styles is not null)
        {
            names.AddRange(styles);
        }

        return LastStyleValue(VegaLite, names, property)
            ?? VegaLite[markType]?[property]
            ?? VegaLite["mark"]?[property]
            ?? LastStyleValue(Vega, names, property)
            ?? Vega[ConfigResolver.VegaMarkType(markType)]?[property];
    }

    /// <summary>
    ///     Resolves a property of the view (the plot background and border): <c>config.style.cell</c>, then
    ///     <c>config.view</c>, then the default <c>cell</c> style.
    /// </summary>
    /// <remarks>
    ///     Vega-Lite moves <c>config.view</c> into the <c>cell</c> style, where an explicit <c>config.style.cell</c> wins
    ///     over it; the defaults are the transparent fill and the <c>#ddd</c> stroke of Vega's <c>cell</c> style.
    /// </remarks>
    /// <param name="property">The property name, such as <c>stroke</c>.</param>
    /// <returns>The value, or <see langword="null"/> when no level defines it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
    public ConfigNode? ResolveViewProperty(string property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return VegaLite["style"]?["cell"]?[property]
            ?? VegaLite["view"]?[property]
            ?? Vega["style"]?["cell"]?[property];
    }

    /// <summary>
    ///     Resolves a property of a named style (<c>guide-label</c>, <c>guide-title</c>, <c>group-title</c>, ...) from the
    ///     merged tree, where a later style name overrides an earlier one.
    /// </summary>
    /// <param name="styles">The style names in order of increasing precedence.</param>
    /// <param name="property">The property name, such as <c>fontSize</c>.</param>
    /// <returns>The value, or <see langword="null"/> when no named style defines it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public ConfigNode? ResolveStyleProperty(IEnumerable<string> styles, string property)
    {
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(property);
        return LastStyleValue(Root, styles, property);
    }

    /// <summary>
    ///     Resolves a property of the chart title or subtitle: <c>config.title</c> first, then the <c>group-title</c> or
    ///     <c>group-subtitle</c> style.
    /// </summary>
    /// <remarks>
    ///     The title configuration spells the text color <c>color</c> (the property is <c>fill</c>) and prefixes the
    ///     subtitle properties (<c>subtitleFontSize</c>); this method takes the property as the title text mark knows
    ///     it (<c>fill</c>, <c>font</c>, <c>fontSize</c>, <c>fontStyle</c>, <c>fontWeight</c>, <c>lineHeight</c>,
    ///     <c>align</c>, <c>baseline</c>, <c>dx</c>, <c>dy</c>) or as a layout property of the title (<c>anchor</c>,
    ///     <c>frame</c>, <c>offset</c>, <c>orient</c>, <c>angle</c>, <c>limit</c>, <c>subtitlePadding</c>), and maps it.
    ///     The default <c>frame</c> is not a configuration value: Vega-Lite uses <c>group</c> for single and layered
    ///     views whose anchor is <c>middle</c> or unset and Vega's <c>bounds</c> otherwise.
    /// </remarks>
    /// <param name="property">The property name.</param>
    /// <param name="subtitle"><see langword="true"/> to resolve the property of the subtitle.</param>
    /// <returns>The value, or <see langword="null"/> when no level defines it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="property"/> is null.</exception>
    public ConfigNode? ResolveTitleProperty(string property, bool subtitle = false)
    {
        ArgumentNullException.ThrowIfNull(property);

        // The configuration spells the text color "color" and prefixes the subtitle properties
        var key = property;
        if (subtitle)
        {
            key = property switch
            {
                "fill" => "subtitleColor",
                "font" => "subtitleFont",
                "fontSize" => "subtitleFontSize",
                "fontStyle" => "subtitleFontStyle",
                "fontWeight" => "subtitleFontWeight",
                "lineHeight" => "subtitleLineHeight",
                _ => property,
            };
        }
        else if (property == "fill")
        {
            key = "color";
        }

        return Root["title"]?[key] ?? Root["style"]?[subtitle ? "group-subtitle" : "group-title"]?[property];
    }

    /// <summary>
    ///     Finds a property in the named styles of a tree; a later name overrides an earlier one.
    /// </summary>
    /// <param name="tree">The configuration tree.</param>
    /// <param name="names">The style names in order of increasing precedence.</param>
    /// <param name="property">The property name.</param>
    /// <returns>The value, or null.</returns>
    private static ConfigNode? LastStyleValue(ConfigNode tree, IEnumerable<string> names, string property)
    {
        ConfigNode? found = null;
        var styles = tree["style"];
        if (styles is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            found = styles[name]?[property] ?? found;
        }

        return found;
    }
}

/// <summary>
///     Resolves the effective configuration of a chart: the Vega-Lite version 6 default configuration, overridden by the
///     specification's <c>config</c>, overridden by the configuration that the caller supplies.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why.</b> Vega-Lite charts are mostly defined by defaults the author never writes (the size of a view,
///         the colors of axes, the fonts of titles), so a renderer that does not carry those defaults draws a
///         different chart. This unit is the one place that holds them, with the precedence rules, so that the layout,
///         guide and mark stages read settings instead of embedding numbers.
///     </para>
///     <para>
///         <b>Precedence and merging.</b> From lowest to highest: the Vega defaults, the Vega-Lite version 6 defaults,
///         the <c>config</c> of the specification, and the configuration text of
///         <see cref="VegaLiteRenderOptions.Config"/>. Objects are merged property by property at every depth; any
///         other value, including an array, is replaced. (Vega-Lite's own compile function merges a caller's config
///         <i>under</i> the specification's; this package deliberately lets the caller win, so that a host can apply a
///         theme to charts it does not control. The merge is also deeper than Vega's, which merges most objects one
///         level; the results differ only for objects nested two levels below a configuration key.) A schema version of
///         5 or 6 is read with the same version 6 defaults; the <c>font</c> shorthand of the configuration is applied
///         to the text and guide styles as Vega-Lite does, while the <c>color</c> and <c>fontSize</c> shorthands are
///         not interpreted (they are ignored, not reported).
///     </para>
///     <para>
///         <b>Defaults and their sources.</b> Every number below was read from the published sources and is only a
///         constant here; no code or comment is copied. Vega-Lite 6.5.0 (the npm <c>latest</c> when this was written)
///         is the reference for the Vega-Lite defaults, and Vega 6.4.0 with <c>vega-parser</c> 7.1.3 for the Vega
///         defaults.
///     </para>
///     <list type="table">
///         <listheader><term>Setting</term><description>Default and source</description></listheader>
///         <item><term><c>background</c>, <c>padding</c>, <c>autosize</c></term><description>white, 5, pad (Vega-Lite <c>config.ts</c> <c>defaultConfig</c>; <c>autosize</c> from Vega <c>config.js</c>).</description></item>
///         <item><term><c>view</c></term><description>continuousWidth 300, continuousHeight 300, step 20 (Vega-Lite <c>config.ts</c> <c>defaultViewConfig</c> and <c>DEFAULT_STEP</c>; note that version 5 used 200). The view's fill is transparent and its stroke <c>#ddd</c> through the Vega <c>cell</c> style (<c>config.js</c>); the stroke width has no default and is 1 where a stroke is drawn (vega-scenegraph <c>util/canvas/stroke.js</c>).</description></item>
///         <item><term><c>title</c></term><description>orient top, anchor middle, offset 4, subtitlePadding 3 (Vega <c>config.js</c>; Vega-Lite's own title configuration is empty). The text styles come from <c>style</c>: <c>group-title</c> (fill <c>#000</c>, font sans-serif, size 13, weight bold) and <c>group-subtitle</c> (fill <c>#000</c>, font sans-serif, size 12).</description></item>
///         <item><term><c>axis</c>, <c>axisBand</c></term><description>minExtent 0, maxExtent 200, bandPosition 0.5, domain true, domainWidth 1, domainColor <c>#888</c>, grid false, gridWidth 1, gridColor <c>#ddd</c>, labels true, labelAngle 0, labelLimit 180, labelOffset 0, labelPadding 2, ticks true, tickColor <c>#888</c>, tickOffset 0, tickRound true, tickSize 5, tickWidth 1, titlePadding 4; <c>axisBand.tickOffset</c> -0.5 (Vega <c>config.js</c>). Label and title fonts come from the <c>guide-label</c> (size 10) and <c>guide-title</c> (size 11, bold) styles.</description></item>
///         <item><term><c>mark</c> and mark types</term><description><c>mark.color</c> <c>#4c78a8</c>, invalid break-paths-show-path-domains, timeUnitBandSize 1; <c>bar</c>: binSpacing 1, continuousBandSize 5, minBandSize 0.25, timeUnitBandPosition 0.5; <c>rect</c>: binSpacing 0 and the same sizes; <c>tick</c>: the rect values and thickness 1; <c>rule</c> and <c>text</c> color black (Vega-Lite <c>mark.ts</c> <c>defaultMarkConfig</c>, <c>defaultRectConfig</c>, <c>defaultBarConfig</c>, <c>defaultTickConfig</c>, and <c>config.ts</c>). The styles <c>point</c> and <c>circle</c> and <c>square</c> carry size 30 and strokeWidth 2, and <c>point</c> and <c>square</c> the shapes <c>circle</c> and <c>square</c> (Vega <c>config.js</c>).</description></item>
///         <item><term><c>scale</c></term><description>pointPadding 0.5, barBandPaddingInner 0.1, rectBandPaddingInner 0, tickBandPaddingInner 0.25, bandWithNestedOffsetPaddingInner and -Outer 0.2, minBandSize 2, minFontSize 8, maxFontSize 40, minOpacity 0.3, maxOpacity 0.8, minSize 4, minStrokeWidth 1, maxStrokeWidth 4, quantileCount 4, quantizeCount 4, zero true, framesPerSecond 2, animationDuration 5 (Vega-Lite <c>scale.ts</c> <c>defaultScaleConfig</c>). There is no configuration default for <c>nice</c> (true for the x and y scales unless a domain is given), <c>continuousPadding</c> (none) or <c>bandPaddingInner</c>: those are rules of the scale stage.</description></item>
///         <item><term><c>range</c></term><description><c>category</c> is the scheme <c>tableau10</c> (<c>#4c78a8 #f58518 #e45756 #72b7b2 #54a24b #eeca3b #b279a2 #ff9da6 #9d755d #bab0ac</c>), plus the Vega ordinal, heatmap, ramp, diverging and symbol ranges (Vega <c>config.js</c>; the colors from Vega-Lite <c>config.ts</c> <c>tab10</c>).</description></item>
///         <item><term>other</term><description><c>timeFormat</c> <c>%b %d, %Y</c>, <c>countTitle</c> <c>Count of Records</c>, <c>normalizedNumberFormat</c> <c>.0%</c>, <c>header</c> titlePadding and labelPadding 10, <c>facet</c> and <c>concat</c> spacing 20, the Vega-Lite legend gradient lengths, and the complete Vega <c>legend</c> and <c>projection</c> defaults.</description></item>
///     </list>
///     <para>
///         Rules that Vega-Lite applies in code rather than in configuration are exposed as constants:
///         <see cref="DefaultReducedOpacity"/>, <see cref="DefaultStep"/>, <see cref="DefaultStrokeWidth"/> and
///         <see cref="IsFilledByDefault"/>. The defaults of the boxplot, error bar, error band and selection
///         configuration are not carried because those features are not rendered.
///     </para>
///     <para>
///         The class is stateless and thread-safe; the defaults are built once.
///     </para>
/// </remarks>
internal static class ConfigResolver
{
    /// <summary>The Vega-Lite version whose defaults were studied (the npm <c>latest</c> of the time).</summary>
    public const string VegaLiteDefaultsVersion = "6.5.0";

    /// <summary>The default opacity of point, tick, circle and square marks that are not aggregated (Vega-Lite <c>compile/mark/init.ts</c>).</summary>
    public const double DefaultReducedOpacity = 0.7;

    /// <summary>The default size of a discrete step in pixels (Vega-Lite <c>config.ts</c> <c>DEFAULT_STEP</c>).</summary>
    public const double DefaultStep = 20;

    /// <summary>The width in pixels of a stroke that has no <c>strokeWidth</c> (vega-scenegraph <c>util/canvas/stroke.js</c>).</summary>
    public const double DefaultStrokeWidth = 1;

    /// <summary>The nesting depth that a configuration text may have; the same bound as <see cref="VegaLiteRenderOptions.Config"/>.</summary>
    private const int MaxCallerConfigDepth = 64;

    /// <summary>The top-level keys of the Vega defaults that name Vega mark types; they are kept out of the merged tree because Vega-Lite uses the same names for its own mark configuration.</summary>
    private static readonly string[] VegaMarkKeys = ["arc", "area", "line", "path", "rect", "rule", "shape", "symbol", "text", "trail"];

    /// <summary>The default configuration of Vega, as Vega's <c>defaultConfig</c> defines it (without the event, group and description settings).</summary>
    private static readonly ConfigNode VegaDefaults = Parse(
        """
        {
          "padding": 0,
          "autosize": "pad",
          "background": null,
          "arc": { "fill": "#4c78a8" },
          "area": { "fill": "#4c78a8" },
          "line": { "stroke": "#4c78a8", "strokeWidth": 2 },
          "path": { "stroke": "#4c78a8" },
          "rect": { "fill": "#4c78a8" },
          "rule": { "stroke": "#000" },
          "shape": { "stroke": "#4c78a8" },
          "symbol": { "fill": "#4c78a8", "size": 64 },
          "text": { "fill": "#000", "font": "sans-serif", "fontSize": 11 },
          "trail": { "fill": "#4c78a8", "size": 2 },
          "style": {
            "guide-label": { "fill": "#000", "font": "sans-serif", "fontSize": 10 },
            "guide-title": { "fill": "#000", "font": "sans-serif", "fontSize": 11, "fontWeight": "bold" },
            "group-title": { "fill": "#000", "font": "sans-serif", "fontSize": 13, "fontWeight": "bold" },
            "group-subtitle": { "fill": "#000", "font": "sans-serif", "fontSize": 12 },
            "point": { "size": 30, "strokeWidth": 2, "shape": "circle" },
            "circle": { "size": 30, "strokeWidth": 2 },
            "square": { "size": 30, "strokeWidth": 2, "shape": "square" },
            "cell": { "fill": "transparent", "stroke": "#ddd" },
            "view": { "fill": "transparent" }
          },
          "title": { "orient": "top", "anchor": "middle", "offset": 4, "subtitlePadding": 3 },
          "axis": {
            "minExtent": 0, "maxExtent": 200, "bandPosition": 0.5,
            "domain": true, "domainWidth": 1, "domainColor": "#888",
            "grid": false, "gridWidth": 1, "gridColor": "#ddd",
            "labels": true, "labelAngle": 0, "labelLimit": 180, "labelOffset": 0, "labelPadding": 2,
            "ticks": true, "tickColor": "#888", "tickOffset": 0, "tickRound": true, "tickSize": 5, "tickWidth": 1,
            "titlePadding": 4
          },
          "axisBand": { "tickOffset": -0.5 },
          "projection": { "type": "mercator" },
          "legend": {
            "orient": "right", "padding": 0, "gridAlign": "each", "columnPadding": 10, "rowPadding": 2,
            "symbolDirection": "vertical", "gradientDirection": "vertical", "gradientLength": 200,
            "gradientThickness": 16, "gradientStrokeColor": "#ddd", "gradientStrokeWidth": 0,
            "gradientLabelOffset": 2, "labelAlign": "left", "labelBaseline": "middle", "labelLimit": 160,
            "labelOffset": 4, "labelOverlap": true, "symbolLimit": 30, "symbolType": "circle", "symbolSize": 100,
            "symbolOffset": 0, "symbolStrokeWidth": 1.5, "symbolBaseFillColor": "transparent",
            "symbolBaseStrokeColor": "#888", "titleLimit": 180, "titleOrient": "top", "titlePadding": 5,
            "layout": {
              "offset": 18, "direction": "horizontal",
              "left": { "direction": "vertical" }, "right": { "direction": "vertical" }
            }
          },
          "range": {
            "category": { "scheme": "tableau10" },
            "ordinal": { "scheme": "blues" },
            "heatmap": { "scheme": "yellowgreenblue" },
            "ramp": { "scheme": "blues" },
            "diverging": { "scheme": "blueorange", "extent": [1, 0] },
            "symbol": ["circle", "square", "triangle-up", "cross", "diamond", "triangle-right", "triangle-down", "triangle-left"]
          }
        }
        """);

    /// <summary>The default configuration of Vega-Lite version 6 (the properties of <c>defaultConfig</c> that this package uses).</summary>
    private static readonly ConfigNode VegaLiteDefaults = Parse(
        """
        {
          "background": "white",
          "padding": 5,
          "timeFormat": "%b %d, %Y",
          "countTitle": "Count of Records",
          "normalizedNumberFormat": ".0%",
          "view": { "continuousWidth": 300, "continuousHeight": 300, "step": 20 },
          "mark": { "color": "#4c78a8", "invalid": "break-paths-show-path-domains", "timeUnitBandSize": 1 },
          "arc": {},
          "area": {},
          "bar": { "binSpacing": 1, "continuousBandSize": 5, "minBandSize": 0.25, "timeUnitBandPosition": 0.5 },
          "circle": {},
          "geoshape": {},
          "image": {},
          "line": {},
          "point": {},
          "rect": { "binSpacing": 0, "continuousBandSize": 5, "minBandSize": 0.25, "timeUnitBandPosition": 0.5 },
          "rule": { "color": "black" },
          "square": {},
          "text": { "color": "black" },
          "tick": { "binSpacing": 0, "continuousBandSize": 5, "minBandSize": 0.25, "timeUnitBandPosition": 0.5, "thickness": 1 },
          "trail": {},
          "scale": {
            "pointPadding": 0.5,
            "barBandPaddingInner": 0.1,
            "rectBandPaddingInner": 0,
            "tickBandPaddingInner": 0.25,
            "bandWithNestedOffsetPaddingInner": 0.2,
            "bandWithNestedOffsetPaddingOuter": 0.2,
            "minBandSize": 2,
            "minFontSize": 8,
            "maxFontSize": 40,
            "minOpacity": 0.3,
            "maxOpacity": 0.8,
            "minSize": 4,
            "minStrokeWidth": 1,
            "maxStrokeWidth": 4,
            "quantileCount": 4,
            "quantizeCount": 4,
            "zero": true,
            "framesPerSecond": 2,
            "animationDuration": 5
          },
          "projection": {},
          "legend": {
            "gradientHorizontalMaxLength": 200,
            "gradientHorizontalMinLength": 100,
            "gradientVerticalMaxLength": 200,
            "gradientVerticalMinLength": 64,
            "unselectedOpacity": 0.35
          },
          "header": { "titlePadding": 10, "labelPadding": 10 },
          "headerColumn": {},
          "headerRow": {},
          "headerFacet": {},
          "style": {},
          "title": {},
          "facet": { "spacing": 20 },
          "concat": { "spacing": 20 }
        }
        """);

    /// <summary>The Vega defaults without the mark-type keys: the part that is merged into the effective tree.</summary>
    private static readonly ConfigNode VegaGuideDefaults = WithoutKeys(VegaDefaults, VegaMarkKeys);

    /// <summary>The configuration of a chart that has no configuration of its own: the defaults alone.</summary>
    private static readonly ResolvedConfig DefaultsOnly = Build(null, null);

    /// <summary>
    ///     Gets the resolved configuration of a chart without any <c>config</c> and without caller configuration:
    ///     the Vega-Lite version 6 defaults alone.
    /// </summary>
    public static ResolvedConfig Defaults => DefaultsOnly;

    /// <summary>
    ///     Resolves the configuration of a parsed specification and an optional caller configuration.
    /// </summary>
    /// <param name="tree">The parsed specification. Must not be null.</param>
    /// <param name="callerConfig">The caller's configuration text (a JSON object), or null for none.</param>
    /// <returns>The effective configuration.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="callerConfig"/> is not a JSON object or is nested deeper than 64 levels (<see cref="VegaLiteRenderOptions.Config"/> checks this when it is set, so this only happens for a text that did not come from the options).</exception>
    public static ResolvedConfig Resolve(SpecTree tree, string? callerConfig)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return Resolve(tree.Root["config"], callerConfig);
    }

    /// <summary>
    ///     Resolves the effective configuration from the <c>config</c> node of a specification and an optional caller
    ///     configuration.
    /// </summary>
    /// <param name="specConfig">The <c>config</c> object of the specification, or null when it has none.</param>
    /// <param name="callerConfig">The caller's configuration text (a JSON object), or null for none.</param>
    /// <returns>The effective configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="callerConfig"/> is not a JSON object or is nested deeper than 64 levels.</exception>
    public static ResolvedConfig Resolve(SpecNode? specConfig, string? callerConfig) =>
        specConfig is null && callerConfig is null ? DefaultsOnly : Build(specConfig, callerConfig);

    /// <summary>
    ///     Determines whether a mark type is filled by default.
    /// </summary>
    /// <remarks>
    ///     Vega-Lite draws every mark filled except <c>point</c>, <c>line</c> and <c>rule</c> (which are outlines);
    ///     <c>mark.filled</c> in the mark definition or configuration overrides this.
    /// </remarks>
    /// <param name="markType">The mark type.</param>
    /// <returns><see langword="true"/> when the mark is filled unless configured otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="markType"/> is null.</exception>
    public static bool IsFilledByDefault(string markType)
    {
        ArgumentNullException.ThrowIfNull(markType);
        return markType is not ("point" or "line" or "rule");
    }

    /// <summary>
    ///     Maps a Vega-Lite mark type to the Vega mark that draws it, which names the Vega default of its properties.
    /// </summary>
    /// <param name="markType">The Vega-Lite mark type.</param>
    /// <returns>The Vega mark type (<c>symbol</c> for point, circle and square; <c>rect</c> for bar, rect and tick; otherwise the same name).</returns>
    internal static string VegaMarkType(string markType) => markType switch
    {
        "point" or "circle" or "square" => "symbol",
        "bar" or "tick" => "rect",
        "geoshape" => "shape",
        _ => markType,
    };

    /// <summary>
    ///     Builds the layers and the merged tree.
    /// </summary>
    /// <param name="specConfig">The <c>config</c> object of the specification, or null.</param>
    /// <param name="callerConfig">The caller's configuration text, or null.</param>
    /// <returns>The resolved configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when the caller configuration is invalid.</exception>
    private static ResolvedConfig Build(SpecNode? specConfig, string? callerConfig)
    {
        // The user's part: the specification's config, then the caller's over it
        var user = ConfigNode.EmptyObject;
        if (specConfig is { IsObject: true })
        {
            user = ConfigNode.Merge(user, ConfigNode.FromSpec(specConfig));
        }

        if (callerConfig is not null)
        {
            user = ConfigNode.Merge(user, ParseCaller(callerConfig));
        }

        // The font shorthand fills the text and guide styles below the user's explicit settings, as Vega-Lite does
        var vegaLite = VegaLiteDefaults;
        if (user["font"]?.AsString() is { } font)
        {
            vegaLite = ConfigNode.Merge(vegaLite, FontConfig(font));
        }

        vegaLite = ConfigNode.Merge(vegaLite, user);
        return new ResolvedConfig(ConfigNode.Merge(VegaGuideDefaults, vegaLite), vegaLite, VegaDefaults);
    }

    /// <summary>
    ///     Creates the configuration that the <c>font</c> shorthand stands for.
    /// </summary>
    /// <param name="font">The font family list.</param>
    /// <returns>The configuration object: the text mark font and the font of the four guide and title styles.</returns>
    private static ConfigNode FontConfig(string font)
    {
        var family = ObjectOf(("font", ConfigNode.FromString(font)));
        return ObjectOf(
            ("text", family),
            ("style", ObjectOf(("guide-label", family), ("guide-title", family), ("group-title", family), ("group-subtitle", family))));
    }

    /// <summary>
    ///     Creates an object node from named values.
    /// </summary>
    /// <param name="properties">The properties.</param>
    /// <returns>The node.</returns>
    private static ConfigNode ObjectOf(params (string Name, ConfigNode Value)[] properties) =>
        ConfigNode.Create(properties.ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal));

    /// <summary>
    ///     Parses the caller's configuration text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The configuration object.</returns>
    /// <exception cref="ArgumentException">Thrown when the text is not a JSON object or is nested too deeply.</exception>
    private static ConfigNode ParseCaller(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxCallerConfigDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("The configuration must be a JSON object.", nameof(text));
            }

            return ConfigNode.FromJson(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The configuration is not valid JSON: " + ex.Message, nameof(text), ex);
        }
    }

    /// <summary>
    ///     Parses a built-in default configuration.
    /// </summary>
    /// <param name="json">The JSON text of the defaults.</param>
    /// <returns>The configuration object.</returns>
    private static ConfigNode Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ConfigNode.FromJson(document.RootElement);
    }

    /// <summary>
    ///     Copies an object without some of its properties.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="keys">The properties to leave out.</param>
    /// <returns>The reduced object.</returns>
    private static ConfigNode WithoutKeys(ConfigNode node, string[] keys) =>
        ConfigNode.Create(node.Properties.Where(p => !keys.Contains(p.Key, StringComparer.Ordinal)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
}

// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause): legend parsing and merging; see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     The kind of a legend.
/// </summary>
internal enum LegendKind
{
    /// <summary>One symbol and one label per entry.</summary>
    Symbol,

    /// <summary>A continuous color ramp with labels at its ticks.</summary>
    Gradient,

    /// <summary>One contiguous band of color per class of a discretizing color scale, with labels at the class boundaries.</summary>
    DiscreteGradient,
}

/// <summary>
///     One encoding channel that a legend explains.
/// </summary>
/// <param name="Channel">The channel: <c>color</c>, <c>size</c>, <c>shape</c>, <c>fill</c>, <c>stroke</c>, <c>strokeDash</c>, <c>strokeWidth</c> or <c>opacity</c>.</param>
/// <param name="Scale">The resolved scale of the channel.</param>
/// <param name="Model">The scale model (a legend scale does not depend on the size of the data rectangle).</param>
/// <param name="Definition">The channel definition object of the encoding.</param>
/// <param name="Legend">The <c>legend</c> object of the channel definition, or null when it has none.</param>
internal sealed record LegendMember(string Channel, ResolvedScale Scale, ScaleModel Model, SpecNode Definition, SpecNode? Legend)
{
    /// <summary>Gets the title the layers that share this member's legend agree on (see <see cref="GuideTitle.Merge"/>), or null when only one layer contributes.</summary>
    public GuideTitle? MergedTitle { get; init; }

    /// <summary>Gets the <c>legend</c> objects of all the layers that share this member's legend in layer order (the first one is <see cref="Legend"/>), or null when only one layer contributes.</summary>
    public IReadOnlyList<SpecNode>? LayerLegends { get; init; }

    /// <summary>Gets the <c>legend</c> objects that give this member's properties, in layer order: those of all the layers that share the legend, else the one of the channel definition.</summary>
    public IReadOnlyList<SpecNode> LegendObjects => LayerLegends ?? (Legend is null ? [] : [Legend]);

    /// <summary>Gets the orient that a <c>legend</c> object gave explicitly (the first one in layer order), or null.</summary>
    public string? ExplicitOrient => LegendObjects.Select(l => l["orient"]?.AsString()).FirstOrDefault(o => o is not null);
}

/// <summary>
///     The title that one source (a layer, a channel) gives a guide, and the merge of several such titles: Vega-Lite's <c>mergeValuesWithExplicit</c> with
///     <c>mergeTitleComponent</c> as the tie-breaker, shared by the axes and the legends.
/// </summary>
/// <remarks>
///     <para>
///         A title is <b>explicit</b> when it is written (<c>axis.title</c>, <c>legend.title</c> or the <c>title</c> of the channel definition; a composite mark writes the
///         title of the fields it computes, as Vega-Lite does) and <b>implicit</b> when it is derived from the field (<c>Mean of x</c>, <c>x</c>). An explicit title wins over an
///         implicit one; two implicit titles join as the distinct titles in order, <c>a, b</c>; two different explicit titles join as <c>a, b</c> too, and an explicit title that
///         switches the title off (<c>null</c>) switches the merged title off. An explicit title without text (an empty string) is neutral in a join.
///     </para>
///     <para>
///         The lines of an implicit title are the parts that join; the lines of an explicit title are the lines of the text. Use <see cref="Resolve"/> to get what the guide shows.
///     </para>
/// </remarks>
/// <param name="IsExplicit">Whether the title is written (true) or derived (false).</param>
/// <param name="Lines">The lines of an explicit title, or the parts of an implicit one; empty for no text, null for an explicit title that switches the title off.</param>
internal sealed record GuideTitle(bool IsExplicit, IReadOnlyList<string>? Lines)
{
    /// <summary>
    ///     Creates the title that is derived from the field of a channel.
    /// </summary>
    /// <param name="text">The derived title (<c>Mean of x</c>), the field when none, or null or empty when the channel reads no field.</param>
    /// <returns>The implicit title.</returns>
    public static GuideTitle FromField(string? text) => new(false, string.IsNullOrEmpty(text) ? [] : [text]);

    /// <summary>
    ///     Creates the title that a specification writes.
    /// </summary>
    /// <param name="node">The <c>title</c> value: a string (its lines), an array of strings (the lines); <c>null</c> and every other value switch the title off.</param>
    /// <returns>The explicit title.</returns>
    public static GuideTitle FromWritten(SpecNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.IsString)
        {
            return new GuideTitle(true, node.StringValue!.Length > 0 ? [.. TextMeasurer.SplitLines(node.StringValue)] : []);
        }

        return node.IsArray ? new GuideTitle(true, [.. node.Items.Where(i => i.IsString).Select(i => i.StringValue!)]) : new GuideTitle(true, null);
    }

    /// <summary>
    ///     Merges two titles of the same guide.
    /// </summary>
    /// <param name="first">The title so far.</param>
    /// <param name="second">The title to merge in.</param>
    /// <returns>The explicit title when only one is explicit; otherwise the join of the two (the first when they are equal).</returns>
    public static GuideTitle Merge(GuideTitle first, GuideTitle second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.IsExplicit != second.IsExplicit)
        {
            return first.IsExplicit ? first : second;
        }

        if (first.Lines is not null && second.Lines is not null && first.Lines.SequenceEqual(second.Lines, StringComparer.Ordinal))
        {
            return first;
        }

        if (!first.IsExplicit)
        {
            return first with { Lines = [.. first.Lines!.Union(second.Lines!, StringComparer.Ordinal)] };
        }

        if (first.Lines is null || second.Lines is null)
        {
            return first with { Lines = null };
        }

        if (second.Lines.Count == 0)
        {
            return first;
        }

        return first.Lines.Count == 0 ? second : first with { Lines = [string.Join(", ", first.Lines.Concat(second.Lines))] };
    }

    /// <summary>
    ///     Gets the lines the guide shows.
    /// </summary>
    /// <returns>The lines (an implicit title is its parts joined by a comma); null for no title.</returns>
    public IReadOnlyList<string>? Resolve()
    {
        if (Lines is null or { Count: 0 })
        {
            return null;
        }

        return IsExplicit ? Lines : [string.Join(", ", Lines)];
    }
}

/// <summary>
///     What a legend needs to know about the mark that its symbols stand for.
/// </summary>
/// <remarks>
///     The facts are those that Vega-Lite's <c>legend/encode.ts</c> reads from the mark definition and the configuration: whether
///     the mark is filled, its fixed fill and stroke (the same colors the marks themselves get), its opacity, its fixed shape, and the
///     stroke settings of the mark configuration. A paint that a field of the encoding drives is not fixed: the legend of another channel
///     shows the base paint of the legend configuration instead.
/// </remarks>
internal sealed class LegendMarkFacts
{
    /// <summary>The marks that are drawn less opaque by default.</summary>
    private static readonly HashSet<string> ReducedOpacityMarks = ["point", "tick", "circle", "square"];

    /// <summary>The marks whose missing fill is transparent instead of none (Vega-Lite's <c>transparentIfNeeded</c>).</summary>
    private static readonly HashSet<string> TransparentMarks = ["bar", "point", "circle", "square"];

    /// <summary>Gets the mark type, or an empty text.</summary>
    public string MarkType { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the mark is filled (the <c>color</c> channel drives its fill, else its stroke).</summary>
    public bool Filled { get; init; }

    /// <summary>Gets the fixed fill color of the mark, or null when it has none.</summary>
    public string? Fill { get; init; }

    /// <summary>Gets the fixed stroke color of the mark, or null when it has none.</summary>
    public string? Stroke { get; init; }

    /// <summary>Gets a value indicating whether a field (or datum) of the encoding drives the fill.</summary>
    public bool FillIsField { get; init; }

    /// <summary>Gets a value indicating whether a field (or datum) of the encoding drives the stroke.</summary>
    public bool StrokeIsField { get; init; }

    /// <summary>Gets the opacity of the marks (the constant <c>opacity</c> channel, else the mark's, 0.7 for point-like marks), or null for none.</summary>
    public double? Opacity { get; init; }

    /// <summary>Gets the fixed shape of the marks (the <c>shape</c> value of the encoding or of the mark), or null.</summary>
    public string? Shape { get; init; }

    /// <summary>Gets the stroke width of the mark definition or configuration, or null.</summary>
    public double? StrokeWidth { get; init; }

    /// <summary>Gets the dash pattern of the mark definition or configuration, or null for a solid line.</summary>
    public IReadOnlyList<double>? StrokeDash { get; init; }

    /// <summary>Gets the fill opacity of the mark definition or configuration, or null.</summary>
    public double? FillOpacity { get; init; }

    /// <summary>Gets the stroke opacity of the mark definition or configuration, or null.</summary>
    public double? StrokeOpacity { get; init; }

    /// <summary>
    ///     Reads the facts from a view.
    /// </summary>
    /// <param name="view">The view object (its <c>mark</c> and <c>encoding</c> are read).</param>
    /// <param name="config">The effective configuration.</param>
    /// <returns>The facts.</returns>
    public static LegendMarkFacts Create(SpecNode view, ResolvedConfig config)
    {
        var markNode = view["mark"];
        var type = markNode switch
        {
            { IsString: true } => markNode.StringValue!,
            { IsObject: true } => markNode["type"]?.AsString() ?? string.Empty,
            _ => string.Empty,
        };
        var def = markNode is { IsObject: true } ? markNode : null;
        var styleNames = new List<string> { type };
        if (def?["style"] is { } style)
        {
            if (style.IsString)
            {
                styleNames.Add(style.StringValue!);
            }
            else if (style.IsArray)
            {
                styleNames.AddRange(style.Items.Where(i => i.IsString).Select(i => i.StringValue!));
            }
        }

        ConfigNode? Def(string name) => def?[name] is { } node ? ConfigNode.FromSpec(node) : null;
        ConfigNode? Vl(string name)
        {
            var vl = config.VegaLite;
            ConfigNode? found = null;
            if (vl["style"] is { } styles)
            {
                foreach (var name2 in styleNames)
                {
                    found = styles[name2]?[name] ?? found;
                }
            }

            return found ?? vl[type]?[name] ?? vl["mark"]?[name];
        }

        ConfigNode? Prop(string name) => Def(name) ?? Vl(name);


        // A trail is filled but its legend symbols are not (Vega-Lite's legend encoding uses "filled && mark !== 'trail'"): the color is their stroke
        var filled = (Prop("filled")?.AsBool() ?? ConfigResolver.IsFilledByDefault(type.Length == 0 ? "point" : type)) && !string.Equals(type, "trail", StringComparison.Ordinal);
        var color = Def("color")?.AsString();
        string? fill;
        if (Def("fill") is { } explicitFill)
        {
            fill = explicitFill.AsString();
        }
        else
        {
            fill = filled ? color ?? Vl("fill")?.AsString() ?? Vl("color")?.AsString() : Vl("fill")?.AsString();
            if (fill is null && TransparentMarks.Contains(type))
            {
                fill = "transparent";
            }
        }

        var stroke = Def("stroke") is { } explicitStroke ? explicitStroke.AsString() : Vl("stroke")?.AsString();
        if (!filled && Def("stroke") is null)
        {
            stroke = color ?? stroke ?? Vl("color")?.AsString();
        }

        double? opacity;
        if (Def("opacity")?.AsNumber() is { } specified)
        {
            opacity = specified;
        }
        else if (Vl("opacity") is null && Vl("fillOpacity") is null && Def("fillOpacity") is null && ReducedOpacityMarks.Contains(type))
        {
            opacity = ConfigResolver.DefaultReducedOpacity;
        }
        else
        {
            opacity = Vl("opacity")?.AsNumber();
        }

        var encoding = view["encoding"] is { IsObject: true } e ? e : null;

        // A color channel that no field drives gives the symbols its first conditional value, else its value (Vega-Lite's getFirstConditionValue)
        if (ChannelDefinitions.FirstValue(encoding?["fill"] ?? (filled ? encoding?["color"] : null))?.AsString() is { } channelFill)
        {
            fill = channelFill;
        }

        if (ChannelDefinitions.FirstValue(encoding?["stroke"] ?? (filled ? null : encoding?["color"]))?.AsString() is { } channelStroke)
        {
            stroke = channelStroke;
        }

        if (encoding?["opacity"] is { IsObject: true } opacityDef)
        {
            // A channel that no field drives gives the legend its constant: the largest of its value and the values of its conditions (Vega-Lite's getMaxValue)
            var constants = ChannelDefinitions.Values(opacityDef).Select(v => v.AsNumber()).OfType<double>().ToList();
            if (constants.Count > 0)
            {
                opacity = constants.Max();
            }
        }

        var shape = ChannelDefinitions.FirstValue(encoding?["shape"])?.AsString() ?? Def("shape")?.AsString();
        var dash = Prop("strokeDash") is { Kind: ConfigNodeKind.Array } dashNode
            ? (IReadOnlyList<double>)[.. dashNode.Items.Select(i => i.AsNumber()).Where(n => n is not null).Select(n => n!.Value)]
            : null;
        return new LegendMarkFacts
        {
            MarkType = type,
            Filled = filled,
            Fill = fill,
            Stroke = stroke,
            FillIsField = DrivesPaint(encoding?["fill"]) || (filled && DrivesPaint(encoding?["color"])),
            StrokeIsField = DrivesPaint(encoding?["stroke"]) || (!filled && DrivesPaint(encoding?["color"])),
            Opacity = opacity,
            Shape = shape,
            StrokeWidth = Prop("strokeWidth")?.AsNumber(),
            StrokeDash = dash,
            FillOpacity = Prop("fillOpacity")?.AsNumber(),
            StrokeOpacity = Prop("strokeOpacity")?.AsNumber(),
        };
    }

    /// <summary>
    ///     Tells whether a channel definition is driven by a field or a datum (not a constant value).
    /// </summary>
    /// <param name="definition">The channel definition, or null.</param>
    /// <returns><see langword="true"/> for a field or datum definition.</returns>
    private static bool DrivesPaint(SpecNode? definition) => definition is { IsObject: true } && ChannelDefinitions.HasField(ChannelDefinitions.FieldDefinitionOf(definition));
}

/// <summary>
///     Looks up legend properties in the order Vega-Lite and Vega define: the <c>legend</c> objects of the channels and layers that share the legend (Vega-Lite's
///     <c>mergeValuesWithExplicit</c>: a property that only one of them gives wins, and when several give it differently the first wins, layers before channels),
///     then <c>config.legend</c> with the Vega-Lite and Vega defaults.
/// </summary>
internal sealed class LegendSettings
{
    /// <summary>The effective configuration.</summary>
    private readonly ResolvedConfig _config;

    /// <summary>The <c>legend</c> objects of the members, in channel order.</summary>
    private readonly IReadOnlyList<SpecNode> _legends;

    /// <summary>
    ///     Initializes the settings.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="legends">The <c>legend</c> objects of the members of the legend that have one, in channel order.</param>
    public LegendSettings(ResolvedConfig config, IReadOnlyList<SpecNode> legends)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(legends);
        _config = config;
        _legends = legends;
    }

    /// <summary>Gets the effective configuration.</summary>
    public ResolvedConfig Config => _config;

    /// <summary>
    ///     Reads a property of the <c>legend</c> objects: the first member that has it (explicit beats implicit, else the first channel wins).
    /// </summary>
    /// <param name="name">The property.</param>
    /// <returns>The value (a null node for JSON null), or null when no legend object has it.</returns>
    public ConfigNode? Explicit(string name)
    {
        foreach (var legend in _legends)
        {
            if (legend[name] is { } node)
            {
                return ConfigNode.FromSpec(node);
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads the specification node of a property of the <c>legend</c> objects (for its location).
    /// </summary>
    /// <param name="name">The property.</param>
    /// <returns>The node, or null.</returns>
    public SpecNode? ExplicitNode(string name) => _legends.Select(l => l[name]).FirstOrDefault(n => n is not null);

    /// <summary>
    ///     Reads the distinct text values that the <c>legend</c> objects give a property, in order (Vega-Lite's conflict check of a property that is given explicitly more than once).
    /// </summary>
    /// <param name="name">The property.</param>
    /// <returns>The distinct texts; empty when no legend object gives the property as text.</returns>
    public IReadOnlyList<string> ExplicitTexts(string name) => [.. _legends.Select(l => l[name]?.AsString()).OfType<string>().Distinct()];

    /// <summary>
    ///     Reads a property with the full precedence: the legend object, then <c>config.legend</c>.
    /// </summary>
    /// <param name="name">The property.</param>
    /// <returns>The value, or null.</returns>
    public ConfigNode? Effective(string name) => Explicit(name) ?? _config.Root.Get("legend." + name);

    /// <summary>Reads a number property with the full precedence.</summary>
    /// <param name="name">The property.</param>
    /// <returns>The number, or null.</returns>
    public double? Number(string name) => Effective(name)?.AsNumber();

    /// <summary>Reads a boolean property with the full precedence.</summary>
    /// <param name="name">The property.</param>
    /// <returns>The value, or null.</returns>
    public bool? Bool(string name) => Effective(name)?.AsBool();

    /// <summary>Reads a text property with the full precedence; a number is read as its text (a font weight of 700).</summary>
    /// <param name="name">The property.</param>
    /// <returns>The text, or null.</returns>
    public string? Text(string name) => TextOf(Effective(name));

    /// <summary>Reads an array of numbers (a dash pattern) with the full precedence.</summary>
    /// <param name="name">The property.</param>
    /// <returns>The numbers, or null.</returns>
    public IReadOnlyList<double>? NumberArray(string name) =>
        Effective(name) is { Kind: ConfigNodeKind.Array } node ? node.Items.Select(i => i.AsNumber()).Where(n => n is not null).Select(n => n!.Value).ToList() : null;

    /// <summary>Reads a text property of a named style (<c>guide-label</c> or <c>guide-title</c>).</summary>
    /// <param name="style">The style name.</param>
    /// <param name="property">The property.</param>
    /// <returns>The text, or null.</returns>
    public string? StyleText(string style, string property) => TextOf(_config.ResolveStyleProperty([style], property));

    /// <summary>Reads a number property of a named style.</summary>
    /// <param name="style">The style name.</param>
    /// <param name="property">The property.</param>
    /// <returns>The number, or null.</returns>
    public double? StyleNumber(string style, string property) => _config.ResolveStyleProperty([style], property)?.AsNumber();

    /// <summary>
    ///     Reads the text of a node: a string as is, a number in invariant culture.
    /// </summary>
    /// <param name="node">The node, or null.</param>
    /// <returns>The text, or null.</returns>
    private static string? TextOf(ConfigNode? node) => node?.Kind switch
    {
        ConfigNodeKind.String => node.StringValue,
        ConfigNodeKind.Number => node.NumberValue.ToString("R", CultureInfo.InvariantCulture),
        _ => null,
    };
}

/// <summary>
///     A legend as the plan decided it: the channels it explains, its kind, orient and direction, its title and the facts of the mark.
/// </summary>
internal sealed class LegendSpec
{
    /// <summary>Gets the channels that the legend explains in channel order; the first one is the canonical scale that decides the entries.</summary>
    public required IReadOnlyList<LegendMember> Members { get; init; }

    /// <summary>Gets the kind of the legend.</summary>
    public required LegendKind Kind { get; init; }

    /// <summary>Gets the property lookup.</summary>
    public required LegendSettings Settings { get; init; }

    /// <summary>Gets the orient: <c>right</c>, <c>left</c>, <c>top</c>, <c>bottom</c>, <c>top-left</c>, <c>top-right</c>, <c>bottom-left</c>, <c>bottom-right</c> or <c>none</c>.</summary>
    public required string Orient { get; init; }

    /// <summary>Gets a value indicating whether the orient was given by a <c>legend</c> object (two legends with different given orients are not merged).</summary>
    public bool ExplicitOrient { get; init; }

    /// <summary>Gets a value indicating whether the entries and the gradient run vertically.</summary>
    public required bool Vertical { get; init; }

    /// <summary>Gets the title lines, or null for no title.</summary>
    public IReadOnlyList<string>? Title { get; init; }

    /// <summary>Gets the facts of the mark.</summary>
    public required LegendMarkFacts Mark { get; init; }

    /// <summary>Gets the location of the first channel definition, for failures.</summary>
    public VegaLiteLocation Location { get; init; }

    /// <summary>Gets the canonical member.</summary>
    public LegendMember Canonical => Members[0];

    /// <summary>Gets a value indicating whether the legend has a member for the channel.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns><see langword="true"/> when a member has the channel.</returns>
    public bool Has(string channel) => Members.Any(m => m.Channel == channel);

    /// <summary>Gets the member of a channel.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The member, or null.</returns>
    public LegendMember? Get(string channel) => Members.FirstOrDefault(m => m.Channel == channel);
}

/// <summary>
///     One layer of a layered view as the legend planning sees it.
/// </summary>
/// <param name="View">The unit view of the layer (its <c>mark</c> and merged <c>encoding</c> are read).</param>
/// <param name="Scales">The scales of the layer (those that its marks use).</param>
/// <param name="Scope">
///     Tells, for a channel, which legend the layer's channel belongs to: layers with the same scope text share the channel's legend, layers with different scopes have legends of their own.
///     Null puts every channel of the layer in one scope.
/// </param>
internal sealed record LegendLayer(SpecNode View, ResolvedScales Scales, Func<string, string>? Scope = null);

/// <summary>
///     Decides which legends a view has: for each channel with a legend, its kind and orient, and which channels share a legend.
/// </summary>
/// <remarks>
///     <para>
///         <b>Which channels.</b> In Vega-Lite's order (<c>color</c>, <c>size</c>, <c>shape</c>, <c>fill</c>, <c>stroke</c>,
///         <c>strokeDash</c>, <c>strokeWidth</c>, <c>opacity</c>) every channel that has a resolved scale and is driven by a field (or a
///         datum) gets a legend, unless its <c>legend</c> is null or false, its <c>scale</c> is null, or <c>config.legend.disable</c> is set.
///         <c>fillOpacity</c>, <c>strokeOpacity</c> and <c>angle</c> have scales but no legend. The <c>color</c> channel explains the fill of
///         a filled mark and the stroke of an unfilled one.
///     </para>
///     <para>
///         <b>Kind.</b> A continuous color scale (sequential or diverging) is a gradient legend, a discretizing color scale (quantize,
///         quantile or threshold) a discrete gradient, every other channel and scale a symbol legend; an explicit <c>legend.type</c> of
///         <c>symbol</c> forces symbols. Channels that share a legend with different kinds make it a symbol legend.
///     </para>
///     <para>
///         <b>Merging.</b> Channels whose scale domains are the same (the same derived field with the same bin, the same kind of domain,
///         the same explicit <c>scale.domain</c> and <c>sort</c>) share one legend, unless both give an <c>orient</c> and the two differ. The
///         first channel in the order above is the canonical one: its scale decides the entries. The properties of the <c>legend</c> objects
///         merge with the explicit one first and the first channel winning; the titles of the channels merge as <see cref="GuideTitle"/> says (a written title wins over a derived one, the others join as <c>a, b</c>).
///     </para>
///     <para>
///         <b>Layers.</b> The legends of a layered view are planned over all its layers (<see cref="Create(IReadOnlyList{LegendLayer}, ResolvedConfig, string?)"/>): the layers that share the legend of
///         a channel contribute one member, the other rules are the same.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class LegendPlan
{
    /// <summary>The channels that have legends, in Vega-Lite's order.</summary>
    public static readonly IReadOnlyList<string> ChannelOrder = ["color", "size", "shape", "fill", "stroke", "strokeDash", "strokeWidth", "opacity"];

    /// <summary>
    ///     Plans the legends of a view.
    /// </summary>
    /// <param name="view">The view object (its <c>mark</c> and <c>encoding</c> are read).</param>
    /// <param name="scales">The resolved scales of the view.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="sourceName">The source name for the locations of failures, or null.</param>
    /// <returns>The legends in the order of their first channel; empty when no channel has a legend.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="sourceName"/> is null.</exception>
    public static IReadOnlyList<LegendSpec> Create(SpecNode view, ResolvedScales scales, ResolvedConfig config, string? sourceName)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(scales);
        ArgumentNullException.ThrowIfNull(config);
        return Create([new LegendLayer(view, scales)], config, sourceName);
    }

    /// <summary>
    ///     Plans the legends of the layers of a layered view.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         For each channel, the layers that share a legend (the same <see cref="LegendLayer.Scope"/> for the channel) contribute one member: the first layer that has a legend for the
    ///         channel decides its scale, definition and the mark facts of the symbols; the titles of all the layers merge as <see cref="GuideTitle"/> says (a written
    ///         title of any layer wins over the titles derived from fields, the derived titles join as <c>a, b</c>, and different written titles join too); and the <c>legend</c> objects of
    ///         all the layers give the properties as Vega-Lite's <c>mergeLegendComponent</c> merges them (a property that one layer gives wins over the silence of the others, and when
    ///         several give it differently the first layer wins; explicit <c>type</c>s that differ give symbols). Layers that give different <c>orient</c>s explicitly cannot be merged, as
    ///         in Vega-Lite: the channel's legend is then independent, a legend for each layer. The merged symbols follow the first layer's mark (a documented approximation of
    ///         Vega-Lite's merge of the symbol encodings). Layers in different scopes (an independent legend or
    ///         scale) have legends of their own. The members then merge into legends exactly as those of one view do, but only within a scope.
    ///     </para>
    ///     <para>A single layer with the default scope plans exactly what <see cref="Create(SpecNode, ResolvedScales, ResolvedConfig, string?)"/> plans for its view.</para>
    /// </remarks>
    /// <param name="layers">The layers in order.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="sourceName">The source name for the locations of failures, or null.</param>
    /// <returns>The legends in the order of their first channel; empty when no channel has a legend.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="sourceName"/> is null.</exception>
    public static IReadOnlyList<LegendSpec> Create(IReadOnlyList<LegendLayer> layers, ResolvedConfig config, string? sourceName)
    {
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(config);
        if (config.GetBool("legend.disable") == true)
        {
            return [];
        }

        var facts = new Dictionary<int, LegendMarkFacts>();
        var groups = new List<(string Key, List<LegendMember> Members, int Layer)>();
        foreach (var channel in ChannelOrder)
        {
            foreach (var (member, layer) in CreateMembers(channel, layers))
            {
                var key = Key(member) + "#" + (layers[layer].Scope?.Invoke(channel) ?? string.Empty);
                var home = groups.FindIndex(g => g.Key == key && !OrientsConflict(g.Members, member) && g.Members.All(m => m.Channel != member.Channel));
                if (home >= 0)
                {
                    groups[home].Members.Add(member);
                }
                else
                {
                    groups.Add((key, [member], layer));
                }
            }
        }

        var result = new List<LegendSpec>(groups.Count);
        foreach (var (_, members, layer) in groups)
        {
            if (!facts.TryGetValue(layer, out var mark))
            {
                mark = LegendMarkFacts.Create(layers[layer].View, config);
                facts[layer] = mark;
            }

            result.Add(Build(members, mark, config, sourceName));
        }

        return result;
    }

    /// <summary>
    ///     Creates the members of a channel: one for each scope that has a layer with a legend for it.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="layers">The layers.</param>
    /// <returns>The members with the index of the layer each came from, in layer order.</returns>
    private static List<(LegendMember Member, int Layer)> CreateMembers(string channel, IReadOnlyList<LegendLayer> layers)
    {
        var found = new List<(string Scope, List<(LegendMember Member, int Layer)> Layers)>();
        for (var i = 0; i < layers.Count; i++)
        {
            if (layers[i].View["encoding"] is not { IsObject: true } encoding || !TryCreateMember(channel, encoding, layers[i].Scales, out var member))
            {
                continue;
            }

            var scope = layers[i].Scope?.Invoke(channel) ?? string.Empty;
            var home = found.FindIndex(f => f.Scope == scope);
            if (home >= 0)
            {
                found[home].Layers.Add((member, i));
            }
            else
            {
                found.Add((scope, [(member, i)]));
            }
        }

        var result = new List<(LegendMember, int)>(found.Count);
        foreach (var (_, contributions) in found)
        {
            if (contributions.Count == 1)
            {
                result.Add(contributions[0]);
            }
            else if (contributions.Select(c => c.Member.ExplicitOrient).Where(o => o is not null).Distinct().Count() > 1)
            {
                // Vega-Lite's mergeLegendComponent cannot merge legends that give different orients explicitly: the channel's legend becomes independent, a legend for each layer
                result.AddRange(contributions);
            }
            else
            {
                result.Add((MergeLayers(contributions), contributions[0].Layer));
            }
        }

        return result;
    }

    /// <summary>
    ///     Merges the members of the layers that share the legend of a channel: the first layer's member carries the titles of all the layers (<see cref="GuideTitle.Merge"/>) and their
    ///     <c>legend</c> objects, so that a property of any of them counts (the first layer that gives it wins).
    /// </summary>
    /// <param name="contributions">The members with their layers, in layer order.</param>
    /// <returns>The merged member.</returns>
    private static LegendMember MergeLayers(List<(LegendMember Member, int Layer)> contributions) =>
        contributions[0].Member with
        {
            MergedTitle = contributions.Select(c => TitleOf(c.Member)).Aggregate(GuideTitle.Merge),
            LayerLegends = [.. contributions.SelectMany(c => c.Member.LegendObjects)],
        };

    /// <summary>
    ///     Gets the title that a member gives its legend: the written title of the <c>legend</c> object or of the channel definition, else the one derived from the field (the title of the
    ///     scale, or the field for a scale without one); for a member of layers, the merge of the titles of the layers.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The title.</returns>
    private static GuideTitle TitleOf(LegendMember member)
    {
        if (member.MergedTitle is { } merged)
        {
            return merged;
        }

        var node = member.Legend?["title"] ?? member.Definition["title"];
        if (node is not null)
        {
            return GuideTitle.FromWritten(node);
        }

        return GuideTitle.FromField(member.Scale.Title is { Length: > 0 } title ? title : member.Scale.FieldPath);
    }

    /// <summary>
    ///     Creates the member of a channel when the channel has a legend.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="encoding">The encoding object.</param>
    /// <param name="scales">The scales.</param>
    /// <param name="member">Receives the member.</param>
    /// <returns><see langword="true"/> when the channel has a legend.</returns>
    private static bool TryCreateMember(string channel, SpecNode encoding, ResolvedScales scales, out LegendMember member)
    {
        member = null!;
        if (encoding[channel] is not { IsObject: true } channelDefinition || scales[channel] is not { } scale)
        {
            return false;
        }

        // The legend belongs to the field definition of the channel (a condition that holds the field, when the definition itself is a value)
        var definition = ChannelDefinitions.FieldDefinitionOf(channelDefinition);
        var legend = definition["legend"];
        if (legend is { IsNull: true } or { IsBoolean: true, BoolValue: false } || definition["scale"] is { IsNull: true })
        {
            return false;
        }

        if (scale.FieldPath is null && definition["datum"] is null)
        {
            return false;
        }

        member = new LegendMember(channel, scale, scale.CreateModel(0), definition, legend is { IsObject: true } ? legend : null);
        return true;
    }

    /// <summary>
    ///     Builds the key that tells which channels share a domain: the field, the binning, the kind of domain and the explicit domain and sort.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The key.</returns>
    private static string Key(LegendMember member)
    {
        var scale = member.Scale;
        var text = new StringBuilder();
        text.Append(scale.FieldPath ?? "datum:" + Fingerprint(member.Definition["datum"]))
            .Append('|').Append((int)scale.Bin)
            .Append('|').Append(scale.Kind is ScaleKind.Ordinal or ScaleKind.Band or ScaleKind.Point ? 'd' : 'c')
            .Append('|').Append(Fingerprint(member.Definition["scale"]?["domain"]))
            .Append('|').Append(Fingerprint(member.Definition["sort"]));
        return text.ToString();
    }

    /// <summary>
    ///     Gets a text that is equal for two equal JSON values (the kinds, the values and the order of the properties).
    /// </summary>
    /// <remarks>The text is as long as the value; the specification was bounded when it was parsed, and its nesting is limited.</remarks>
    /// <param name="node">The value, or null.</param>
    /// <returns>The text.</returns>
    private static string Fingerprint(SpecNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        AppendFingerprint(node, text);
        return text.ToString();
    }

    /// <summary>
    ///     Appends the fingerprint of a value.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="text">The builder.</param>
    private static void AppendFingerprint(SpecNode node, StringBuilder text)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.String:
                text.Append('"').Append(node.StringValue).Append('"');
                break;
            case SpecNodeKind.Number:
                text.Append(node.NumberValue.ToString("R", CultureInfo.InvariantCulture));
                break;
            case SpecNodeKind.Boolean:
                text.Append(node.BoolValue ? "true" : "false");
                break;
            case SpecNodeKind.Array:
                text.Append('[');
                foreach (var item in node.Items)
                {
                    AppendFingerprint(item, text);
                    text.Append(',');
                }

                text.Append(']');
                break;
            case SpecNodeKind.Object:
                text.Append('{');
                foreach (var property in node.Properties)
                {
                    text.Append(property.Name).Append(':');
                    AppendFingerprint(property, text);
                    text.Append(',');
                }

                text.Append('}');
                break;
            default:
                text.Append("null");
                break;
        }
    }

    /// <summary>
    ///     Tells whether a member and the members of a candidate group give different orients explicitly.
    /// </summary>
    /// <param name="members">The members of the group.</param>
    /// <param name="member">The new member.</param>
    /// <returns><see langword="true"/> when the two must not be merged.</returns>
    private static bool OrientsConflict(List<LegendMember> members, LegendMember member)
    {
        var candidate = member.ExplicitOrient;
        if (candidate is null)
        {
            return false;
        }

        var existing = members.Select(m => m.ExplicitOrient).FirstOrDefault(o => o is not null);
        return existing is not null && existing != candidate;
    }

    /// <summary>
    ///     Builds the legend of a group of members.
    /// </summary>
    /// <param name="members">The members in channel order.</param>
    /// <param name="mark">The facts of the mark.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="sourceName">The source name.</param>
    /// <returns>The legend.</returns>
    private static LegendSpec Build(List<LegendMember> members, LegendMarkFacts mark, ResolvedConfig config, string? sourceName)
    {
        var settings = new LegendSettings(config, [.. members.SelectMany(m => m.LegendObjects)]);
        var kind = ResolveKind(members, settings);
        var explicitOrient = settings.Explicit("orient")?.AsString();
        var orient = explicitOrient ?? settings.Text("orient") ?? "right";
        var vertical = ResolveVertical(settings, orient, kind);
        return new LegendSpec
        {
            Members = members,
            Kind = kind,
            Settings = settings,
            Orient = orient,
            ExplicitOrient = explicitOrient is not null,
            Vertical = vertical,
            Title = ResolveTitle(members),
            Mark = mark,
            Location = members[0].Definition.GetLocation(sourceName),
        };
    }

    /// <summary>
    ///     Resolves the kind of a legend.
    /// </summary>
    /// <param name="members">The members.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>The kind.</returns>
    private static LegendKind ResolveKind(List<LegendMember> members, LegendSettings settings)
    {
        var kinds = members.Select(GradientFor).Distinct().ToList();
        var explicitTypes = settings.ExplicitTexts("type");
        if (explicitTypes.Count > 1)
        {
            // Vega-Lite: two explicit types that differ merge to the symbol type (there are only two, and symbols are preferred)
            return LegendKind.Symbol;
        }

        var explicitType = explicitTypes.FirstOrDefault();
        if (explicitType == "symbol")
        {
            return LegendKind.Symbol;
        }

        if (explicitType == "gradient")
        {
            // Only a color scale can be shown as a ramp or as bands; for any other scale a gradient is the symbol legend
            return members.Count == 1 || kinds.Count == 1 ? GradientFor(members[0]) : LegendKind.Symbol;
        }

        return kinds.Count == 1 ? kinds[0] : LegendKind.Symbol;
    }

    /// <summary>
    ///     Gets the gradient kind a color member's scale has, or the symbol kind for every other member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The kind.</returns>
    private static LegendKind GradientFor(LegendMember member)
    {
        if (member.Channel is not ("color" or "fill" or "stroke"))
        {
            return LegendKind.Symbol;
        }

        return member.Scale.Kind switch
        {
            ScaleKind.Sequential or ScaleKind.Diverging => LegendKind.Gradient,
            ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold => LegendKind.DiscreteGradient,
            _ => LegendKind.Symbol,
        };
    }

    /// <summary>
    ///     Resolves the direction: the <c>direction</c> of the legend, Vega-Lite's default for the orient, then the configuration.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="orient">The orient.</param>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for vertical.</returns>
    private static bool ResolveVertical(LegendSettings settings, string orient, LegendKind kind)
    {
        var direction = settings.Explicit("direction")?.AsString();
        if (direction is null)
        {
            // Vega-Lite: top and bottom are horizontal; the inner corners are horizontal for gradients; left, right and none leave it to Vega
            direction = orient switch
            {
                "top" or "bottom" => "horizontal",
                "left" or "right" or "none" => null,
                _ => kind == LegendKind.Symbol ? null : "horizontal",
            };
        }

        direction ??= settings.Text("direction") ?? (kind == LegendKind.Symbol ? settings.Text("symbolDirection") : settings.Text("gradientDirection"));
        return !string.Equals(direction, "horizontal", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Resolves the title of a legend: the titles of the members merge (a written title wins over a derived one, the derived titles and different written titles join by a comma).
    /// </summary>
    /// <param name="members">The members.</param>
    /// <returns>The lines, or null for no title.</returns>
    private static IReadOnlyList<string>? ResolveTitle(List<LegendMember> members) =>
        members.Select(TitleOf).Aggregate(GuideTitle.Merge).Resolve();
}

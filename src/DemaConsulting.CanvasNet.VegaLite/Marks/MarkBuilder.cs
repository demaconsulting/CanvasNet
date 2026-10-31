// Portions derived from Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <summary>
///     Builds the scene items of the mark of a single view: <c>bar</c>, <c>rect</c>, <c>point</c>, <c>circle</c>,
///     <c>square</c>, <c>rule</c> and <c>tick</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Geometry.</b> The rules are those of Vega-Lite 6 (<c>compile/mark/*.ts</c>, <c>encode/position-rect.ts</c>,
///         <c>position-point.ts</c>, <c>position-range.ts</c>, <c>init.ts</c>). A discrete position of a <c>bar</c>, <c>rect</c> or
///         <c>tick</c> fills the band (its <c>bandwidth</c>, at least <c>minBandSize</c>); a point scale gives <c>step - 2</c>. The
///         thickness axis of a bar or tick on a continuous scale is centered on the value and as thick as
///         <c>continuousBandSize</c> (5) unless <c>size</c>, <c>width</c> or <c>height</c> say otherwise. The measure axis runs from the
///         value to zero (to the first domain value when the domain does not contain zero); an absent channel spans the whole data
///         rectangle, a bar or tick without a position on its thickness axis is centered and as thick as one minus the
///         band padding (0.1 for a bar, 0.25 for a tick) of the rectangle. A symbol sits at the center of its band or at its value (the middle of its bin for a binned field, the upper end of a stack) and
///         at the middle of the rectangle for an absent channel. A rule with only <c>x</c> is vertical across the whole height,
///         with only <c>y</c> horizontal across the whole width; with both, its stem runs from the value of the measure
///         axis to zero. A tick is a <c>thickness</c> (1) wide rectangle that is as long as its band (or <c>continuousBandSize</c>).
///     </para>
///     <para>
///         <b>Ranges, stacks and bins.</b> A stacked channel (its field is the upper end and its second field the lower end of the stack)
///         and a channel with <c>x2</c> or <c>y2</c> span from the first to the second end instead of to zero, for a bar, a rect and a rule
///         (a rule with a range on one channel runs along that channel; with a range on both it is the segment between the two points). A bar or rect
///         on a continuous channel that is binned (by the encoding, or <c>bin: "binned"</c> with <c>x2</c> or a bin <c>step</c>) spans from the start of its
///         bin to its end, each edge <c>binSpacing</c> / 2 inside (1 for a bar, 0 for a rect and a tick), the whole span shifted by the half pixel by which
///         Vega translates the axis; a bin thinner than <c>minBandSize</c> is widened to that size around its middle. A size (<c>size</c>, <c>width</c>,
///         <c>height</c>) centers the mark on the middle of the bin instead.
///     </para>
///     <para>
///         <b>Paint.</b> <c>filled</c> is on for every mark but <c>point</c> and <c>rule</c>; the color of the mark (<c>#4c78a8</c>,
///         <c>black</c> for a rule) is the fill of a filled mark and the stroke of an unfilled one (unfilled bars and symbols are filled
///         <c>transparent</c>). Explicit <c>fill</c>, <c>stroke</c>, <c>color</c>, <c>opacity</c>, <c>fillOpacity</c>, <c>strokeOpacity</c>,
///         <c>strokeWidth</c>, <c>strokeDash</c>, <c>cornerRadius</c>, <c>size</c>, <c>shape</c> and <c>angle</c> of the mark definition win over the
///         configuration (style, <c>config.&lt;mark&gt;</c>, <c>config.mark</c>, Vega's styles such as <c>point</c>: size 30, stroke
///         width 2). <c>point</c>, <c>circle</c>, <c>square</c> and <c>tick</c> get an opacity of 0.7 when nothing names an
///         opacity. A <c>color</c>, <c>fill</c> or <c>stroke</c> field is mapped through its scale (the colors of an ordinal scale, the ramp of a
///         sequential or diverging scale, the class color of a quantize, quantile or threshold scale), a
///         <c>size</c> field through the size scale (the area of a symbol, the stroke width of a rule, the thickness of a bar or
///         tick), an <c>opacity</c> field through the opacity scale, and the channels <c>strokeWidth</c>, <c>fillOpacity</c> and <c>strokeOpacity</c>
///         (every mark), <c>strokeDash</c> (every mark but a rect), <c>shape</c> (a point) and <c>angle</c> (a symbol) through theirs, each
///         replacing the mark property of the same name for the row; <c>value</c> definitions are used as given. A symbol with a
///         <c>shape</c> that is not one of the symbol names is drawn as a circle (the feature catalog reports a custom path).
///     </para>
///     <para>
///         <b>Invalid data.</b> Vega-Lite's default <c>invalid</c> mode for marks other than lines and areas is <c>filter</c>: a row
///         whose value for a channel with a continuous domain (<c>x</c>, <c>y</c>, <c>size</c>, <c>opacity</c>, <c>strokeWidth</c>, <c>fillOpacity</c>,
///         <c>strokeOpacity</c>, <c>angle</c> on a linear scale; <c>color</c>, <c>fill</c>, <c>stroke</c> on a sequential, diverging, quantize, quantile or
///         threshold scale) is null, missing, NaN or not a
///         number is not drawn. A null in a discrete field is the category <c>null</c> and is drawn; a value outside an explicit
///         discrete domain is not.
///     </para>
///     <para>
///         <b>Clipping.</b> Marks are clipped to the data rectangle only when the mark definition says <c>"clip": true</c>. Vega-Lite's
///         <c>scaleClip</c> applies to the extent of an interval selection, which is not supported, so an explicit scale domain never clips. Row-bound expressions
///         (<c>{"expr": ...}</c>, compiled by the specification parser) are evaluated against the transformed row of each mark for the
///         paint properties of the mark definition (<c>color</c>, <c>fill</c>, <c>stroke</c>, <c>opacity</c>, <c>strokeWidth</c>, and
///         <c>size</c> of a symbol) and for the constant <c>value</c> of a color, fill, stroke or opacity channel (and of a size
///         channel of a symbol); a result of the wrong kind leaves the default in place. Other mark properties that are expressions,
///         <c>tooltip</c>, <c>href</c> and the individual corner radii are ignored; the offset channels are reported by the feature catalog and ignored here.
///     </para>
///     <para>
///         <b>Conditions.</b> A color, fill, stroke, opacity, fillOpacity, strokeOpacity, strokeWidth, strokeDash, size, shape or angle channel
///         may carry a <c>condition</c> (an object, or an array of them) whose <c>test</c> is an expression or a predicate that the parser compiled
///         to an expression. For every row the conditions are tried in order and the first whose test is truthy decides: its <c>value</c> (a literal or an
///         <c>{"expr": ...}</c>), or, for a condition that holds the field of the channel, the value that the scale of the channel gives for the field of
///         the row; otherwise the channel gives what it gives without conditions (its scale or its constant <c>value</c>), or, when it has nothing, the property
///         of the mark. A test is evaluated by the expression engine under the step budget of the call, and a condition on a parameter, or a test that
///         cannot be written as an expression, is not applied (the feature catalog reports it). The class is stateless between calls and safe for
///         concurrent use.
///     </para>
/// </remarks>
internal sealed class MarkBuilder
{
    /// <summary>The number of rows between two cancellation checks and budget updates.</summary>
    private const int BatchSize = 1024;

    /// <summary>The most entries a dash pattern may have: a longer pattern is an error, as every item would carry its own copy.</summary>
    private const int MaxDashLength = 64;

    /// <summary>The half pixel by which Vega translates an axis; Vega-Lite shifts the rectangles of a binned field with it so that they meet the ticks.</summary>
    private const double AxisTranslate = 0.5;

    /// <summary>The <c>binSpacing</c> of a bar when neither the mark nor the configuration gives one.</summary>
    private const double DefaultBarBinSpacing = 1;

    /// <summary>The marks that this builder draws.</summary>
    private static readonly HashSet<string> SupportedMarks = ["bar", "rect", "point", "circle", "square", "rule", "tick"];

    /// <summary>The marks whose missing fill is transparent instead of none (Vega-Lite's <c>transparentIfNeeded</c>).</summary>
    private static readonly HashSet<string> TransparentMarks = ["bar", "point", "circle", "square"];

    /// <summary>The marks that are drawn less opaque by default.</summary>
    private static readonly HashSet<string> ReducedOpacityMarks = ["point", "tick", "circle", "square"];

    /// <summary>The effective configuration.</summary>
    private readonly ResolvedConfig _config;

    /// <summary>The budget of scene items.</summary>
    private readonly SceneBudget _budget;

    /// <summary>The source name used in locations.</summary>
    private readonly string? _sourceName;

    /// <summary>The compiled expressions and the evaluator of the call, or null when there are none.</summary>
    private readonly RowExpressions? _expressions;

    /// <summary>
    ///     Initializes a mark builder.
    /// </summary>
    /// <param name="config">The effective configuration.</param>
    /// <param name="limits">The limits (<see cref="VegaLiteLimits.MaxSceneItems"/>); the defaults when null.</param>
    /// <param name="budget">The budget of scene items shared with the other builders of the chart; a new one from the limits when null.</param>
    /// <param name="sourceName">The source name for locations, or null.</param>
    /// <param name="expressions">The compiled expressions and the evaluator of the call, which evaluate the row-bound <c>{"expr": ...}</c> values of the paint properties; null leaves such values unevaluated.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public MarkBuilder(ResolvedConfig config, VegaLiteLimits? limits = null, SceneBudget? budget = null, string? sourceName = null, RowExpressions? expressions = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _budget = budget ?? new SceneBudget((limits ?? VegaLiteLimits.Default).MaxSceneItems);
        _sourceName = sourceName;
        _expressions = expressions;
    }

    /// <summary>
    ///     Tells whether this builder draws a mark type.
    /// </summary>
    /// <param name="markType">The mark type, such as <c>bar</c>.</param>
    /// <returns><see langword="true"/> for <c>bar</c>, <c>rect</c>, <c>point</c>, <c>circle</c>, <c>square</c>, <c>rule</c> and <c>tick</c>.</returns>
    public static bool IsSupported(string? markType) => markType is not null && SupportedMarks.Contains(markType);

    /// <summary>
    ///     Builds the mark items of a view.
    /// </summary>
    /// <param name="view">The view object of the specification (its <c>mark</c> and <c>encoding</c> are read).</param>
    /// <param name="table">The data of the view.</param>
    /// <param name="scales">The resolved scales of the view.</param>
    /// <param name="width">The width of the data rectangle in pixels.</param>
    /// <param name="height">The height of the data rectangle in pixels.</param>
    /// <param name="cancellationToken">A token that stops the work.</param>
    /// <returns>A group (role <c>marks</c>) in the coordinates of the data rectangle; empty for a mark that is not drawn.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the chart has more scene items than <see cref="VegaLiteLimits.MaxSceneItems"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public SceneGroup Build(SpecNode view, DataTable table, ResolvedScales scales, double width, double height, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(scales);
        var markNode = view["mark"];
        var type = markNode switch
        {
            { IsString: true } => markNode.StringValue!,
            { IsObject: true } => markNode["type"]?.AsString() ?? string.Empty,
            _ => string.Empty,
        };
        if (!SupportedMarks.Contains(type))
        {
            _budget.Add(1, (view["mark"] ?? view).GetLocation(_sourceName));
            return new SceneGroup("marks", 0, 0);
        }

        // The marks group is charged before it is created; the mark items are charged one by one as they are made
        _budget.Add(1, (view["mark"] ?? view).GetLocation(_sourceName));
        var run = new Run(this, type, markNode is { IsObject: true } ? markNode : null, view, table, scales, width, height, cancellationToken);
        var items = run.Build();
        var clip = markNode?["clip"] is { IsBoolean: true, BoolValue: true };
        return new SceneGroup("marks", 0, 0)
        {
            Children = items,
            Clip = clip ? new SceneClip(0, 0, width, height) : null,
        };
    }

    /// <summary>
    ///     One encoding channel of the view: its definition, its scale and how to read its values from a row.
    /// </summary>
    private sealed class Channel
    {
        /// <summary>
        ///     Initializes a channel.
        /// </summary>
        /// <param name="node">The channel definition.</param>
        /// <param name="scale">The resolved scale, or null.</param>
        /// <param name="model">The scale model, or null.</param>
        /// <param name="table">The data.</param>
        /// <param name="valueExpression">The compiled <c>{"expr": ...}</c> of the constant <c>value</c> of the channel, or null.</param>
        /// <param name="secondary">The definition of the secondary channel (<c>x2</c> or <c>y2</c>) of a position channel, or null.</param>
        /// <param name="conditions">The conditions of the channel whose test the call can evaluate, in order.</param>
        public Channel(SpecNode node, ResolvedScale? scale, ScaleModel? model, DataTable table, CompiledExpression? valueExpression = null, SpecNode? secondary = null, IReadOnlyList<ChannelCondition>? conditions = null)
        {
            Scale = scale;
            Model = model;
            ValueExpression = valueExpression;
            Conditions = conditions ?? [];
            var fieldNode = ChannelDefinitions.FieldDefinitionOf(node);
            IsFieldConditional = !ReferenceEquals(fieldNode, node);
            FieldIndex = scale?.FieldPath is { } path ? table.FindField(path) : -1;
            Field2Index = scale?.Field2Path is { } path2 ? table.FindField(path2) : -1;
            SecondDatum = secondary?["datum"] switch
            {
                { IsNumber: true } n => DataValue.FromNumber(n.NumberValue),
                { IsString: true } s => DataValue.FromString(s.StringValue),
                { IsBoolean: true } b => DataValue.FromBoolean(b.BoolValue),
                _ => null,
            };
            SecondValue = secondary?["value"];
            BinStep = scale is { Bin: BinKind.Binned } && Field2Index < 0 && node["bin"]?["step"] is { IsNumber: true } step && step.NumberValue > 0 ? step.NumberValue : null;
            Datum = fieldNode["datum"] switch
            {
                { IsNumber: true } n => DataValue.FromNumber(n.NumberValue),
                { IsString: true } s => DataValue.FromString(s.StringValue),
                { IsBoolean: true } b => DataValue.FromBoolean(b.BoolValue),
                _ => null,
            };
            Value = node["value"];
        }

        /// <summary>Gets the conditions of the channel whose test the call can evaluate, in order.</summary>
        public IReadOnlyList<ChannelCondition> Conditions { get; }

        /// <summary>Gets a value indicating whether the field of the channel (and so its scale) belongs to a condition: the definition of the channel itself is a value.</summary>
        public bool IsFieldConditional { get; }

        /// <summary>Gets the resolved scale, or null.</summary>
        public ResolvedScale? Scale { get; }

        /// <summary>Gets the scale model, or null.</summary>
        public ScaleModel? Model { get; }

        /// <summary>Gets the index of the field in the table, or -1.</summary>
        public int FieldIndex { get; }

        /// <summary>Gets the index in the table of the field of the second end of the channel (the lower end of a stack, the end of a bin, the field of <c>x2</c> or <c>y2</c>), or -1.</summary>
        public int Field2Index { get; }

        /// <summary>Gets the <c>datum</c> of the secondary channel (<c>x2</c> or <c>y2</c>), or null.</summary>
        public DataValue? SecondDatum { get; }

        /// <summary>Gets the constant <c>value</c> of the secondary channel (<c>x2</c> or <c>y2</c>), or null.</summary>
        public SpecNode? SecondValue { get; }

        /// <summary>Gets the step of a field that is binned already and has no secondary channel (<c>bin: {"binned": true, "step": ...}</c>), or null.</summary>
        public double? BinStep { get; }

        /// <summary>Gets a value indicating whether the channel is the stacked channel: its field is the upper end of the stack and its second field the lower end.</summary>
        public bool IsStacked => Model is { Kind: ScaleKind.Linear } && Scale is { Stack: not null } && Field2Index >= 0;

        /// <summary>Gets a value indicating whether the channel is binned on a continuous scale and the end of each bin is known: the geometry of a bin applies.</summary>
        public bool HasBinEnds => Model is { Kind: ScaleKind.Linear } && Scale is { Bin: not BinKind.None } && (Field2Index >= 0 || BinStep is not null);

        /// <summary>Gets a value indicating whether the scale of the channel has a continuous domain, so that a row whose value is not a number is not drawn (Vega-Lite's invalid filter).</summary>
        public bool HasContinuousDomain => Model is { Kind: ScaleKind.Linear or ScaleKind.Sequential or ScaleKind.Diverging or ScaleKind.Quantize or ScaleKind.Quantile or ScaleKind.Threshold };

        /// <summary>Gets a value indicating whether the channel spans a range that the encoding gives: <c>x2</c> or <c>y2</c> next to a field that is not binned by the encoding and not stacked.</summary>
        public bool HasRange => Model is { Kind: ScaleKind.Linear } && Scale is { Stack: null, Bin: not BinKind.Binning } && (Field2Index >= 0 || SecondDatum is not null || SecondValue is not null);

        /// <summary>Gets the datum of the channel, or null.</summary>
        public DataValue? Datum { get; }

        /// <summary>Gets the constant <c>value</c> of the channel, or null.</summary>
        public SpecNode? Value { get; }

        /// <summary>Gets the compiled expression of a constant <c>value</c> given as <c>{"expr": ...}</c>, or null.</summary>
        public CompiledExpression? ValueExpression { get; }

        /// <summary>
        ///     Reads the value of the channel for a row.
        /// </summary>
        /// <param name="table">The data.</param>
        /// <param name="row">The row.</param>
        /// <returns>The field value, the datum, or null when neither exists.</returns>
        public DataValue Read(DataTable table, int row) =>
            FieldIndex >= 0 ? table.GetValue(row, FieldIndex) : Datum ?? DataValue.Null;
    }

    /// <summary>
    ///     One condition of a channel: a test that the expression engine evaluates for a row, and what the channel gives the row when the test holds.
    /// </summary>
    /// <param name="Test">The compiled test.</param>
    /// <param name="IsField">Whether the condition holds the field of the channel (its value is what the scale of the channel gives for the field of the row); otherwise it holds a value.</param>
    /// <param name="Value">The literal <c>value</c> of the condition, or null.</param>
    /// <param name="ValueExpression">The compiled <c>{"expr": ...}</c> of the value, or null.</param>
    private sealed record ChannelCondition(CompiledExpression Test, bool IsField, SpecNode? Value, CompiledExpression? ValueExpression);

    /// <summary>
    ///     What a channel gives a row: a number (null for an invalid row), or nothing at all, so that the property of the mark applies.
    /// </summary>
    /// <param name="Value">The number, or null when the row is invalid.</param>
    /// <param name="IsDefault">Whether the channel gives the row nothing (a conditional channel whose conditions did not hold and that has no value of its own).</param>
    private readonly record struct Resolved(double? Value, bool IsDefault);

    /// <summary>
    ///     How thick a bar or tick is along one axis.
    /// </summary>
    /// <param name="Fixed">A thickness in pixels, or null.</param>
    /// <param name="Band">The fraction of the band, or null.</param>
    /// <param name="Encoded">The size channel that gives the thickness per row, or null.</param>
    private sealed record Sizing(double? Fixed, double? Band, Channel? Encoded);

    /// <summary>
    ///     The paint of one item.
    /// </summary>
    /// <param name="Fill">The fill color.</param>
    /// <param name="Stroke">The stroke color.</param>
    /// <param name="StrokeWidth">The stroke width.</param>
    /// <param name="Opacity">The opacity.</param>
    /// <param name="FillOpacity">The opacity of the fill.</param>
    /// <param name="StrokeOpacity">The opacity of the stroke.</param>
    /// <param name="StrokeDash">The dash pattern of the stroke, or null for a solid line.</param>
    private readonly record struct Paint(string? Fill, string? Stroke, double? StrokeWidth, double Opacity, double FillOpacity, double StrokeOpacity, IReadOnlyList<double>? StrokeDash);

    /// <summary>
    ///     The state of building one mark: everything that is the same for every row.
    /// </summary>
    private sealed class Run
    {
        /// <summary>The owner.</summary>
        private readonly MarkBuilder _owner;

        /// <summary>The mark type.</summary>
        private readonly string _type;

        /// <summary>The mark definition object, or null for a mark given by name.</summary>
        private readonly SpecNode? _def;

        /// <summary>The style names of the mark: its type then the <c>style</c> of the definition.</summary>
        private readonly List<string> _styleNames;

        /// <summary>The data.</summary>
        private readonly DataTable _table;

        /// <summary>The width of the data rectangle.</summary>
        private readonly double _width;

        /// <summary>The height of the data rectangle.</summary>
        private readonly double _height;

        /// <summary>The orientation of the mark.</summary>
        private readonly string? _orient;

        /// <summary>The cancellation token.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The location of the mark for failures.</summary>
        private readonly VegaLiteLocation _location;

        /// <summary>The channels by name.</summary>
        private readonly Dictionary<string, Channel> _channels = new(StringComparer.Ordinal);

        /// <summary>The default fill color.</summary>
        private string? _fill;

        /// <summary>The default stroke color.</summary>
        private string? _stroke;

        /// <summary>Gets the color of fills driven by a channel, or null.</summary>
        private Channel? _fillChannel;

        /// <summary>The channel that drives the stroke, or null.</summary>
        private Channel? _strokeChannel;

        /// <summary>The default opacity.</summary>
        private double _opacity = 1;

        /// <summary>The default opacity of the fill (the <c>fillOpacity</c> of the mark).</summary>
        private double _fillOpacity = 1;

        /// <summary>The default opacity of the stroke (the <c>strokeOpacity</c> of the mark).</summary>
        private double _strokeOpacity = 1;

        /// <summary>The default dash pattern of the stroke (the <c>strokeDash</c> of the mark), or null for a solid line.</summary>
        private IReadOnlyList<double>? _dash;

        /// <summary>The channels whose scale has a continuous domain and whose rows must hold a number (the paint and symbol channels; the position and size channels check their own values).</summary>
        private readonly Channel[] _filtered;

        /// <summary>The expression that gives the fill of each row (the <c>fill</c> property, or the <c>color</c> of a filled mark), or null.</summary>
        private CompiledExpression? _fillExpression;

        /// <summary>The expression that gives the stroke of each row (the <c>stroke</c> property, or the <c>color</c> of an unfilled mark), or null.</summary>
        private CompiledExpression? _strokeExpression;

        /// <summary>The expression that gives the opacity of each row, or null.</summary>
        private CompiledExpression? _opacityExpression;

        /// <summary>The expression that gives the stroke width of each row, or null.</summary>
        private CompiledExpression? _strokeWidthExpression;

        /// <summary>The expression that gives the symbol size of each row, or null.</summary>
        private CompiledExpression? _sizeExpression;

        /// <summary>
        ///     Initializes a run.
        /// </summary>
        /// <param name="owner">The builder.</param>
        /// <param name="type">The mark type.</param>
        /// <param name="def">The mark definition object, or null.</param>
        /// <param name="view">The view.</param>
        /// <param name="table">The data.</param>
        /// <param name="scales">The scales.</param>
        /// <param name="width">The width of the data rectangle.</param>
        /// <param name="height">The height of the data rectangle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public Run(MarkBuilder owner, string type, SpecNode? def, SpecNode view, DataTable table, ResolvedScales scales, double width, double height, CancellationToken cancellationToken)
        {
            _owner = owner;
            _type = type;
            _def = def;
            _table = table;
            _width = width;
            _height = height;
            _cancellationToken = cancellationToken;
            _location = (view["mark"] ?? view).GetLocation(owner._sourceName);
            _styleNames = [type];
            if (def?["style"] is { } style)
            {
                _styleNames.AddRange(StyleNames(style));
            }

            _orient = scales.Orient ?? (type == "tick" ? "vertical" : null);
            var encoding = view["encoding"] is { IsObject: true } e ? e : null;
            foreach (var name in new[] { "x", "y", "color", "fill", "stroke", "size", "opacity", "shape", "strokeWidth", "strokeDash", "fillOpacity", "strokeOpacity", "angle" })
            {
                if (encoding?[name] is { IsObject: true } node && (name is not ("shape" or "angle" or "strokeDash") || ScaleResolver.ChannelIsDrawn(name, type)))
                {
                    var scale = scales[name];
                    var extent = name == "x" ? width : height;
                    _channels[name] = new Channel(
                        node,
                        scale,
                        scale?.CreateModel(name is "x" or "y" ? extent : 0),
                        table,
                        owner._expressions?.Compiled.GetReference(node["value"]),
                        name is "x" or "y" && encoding[name + "2"] is { IsObject: true } secondary ? secondary : null,
                        ConditionsOf(name, node));
                }
            }

            _filtered = [.. _channels.Where(c => c.Key is not ("x" or "y" or "size" or "opacity") && c.Value.HasContinuousDomain && !c.Value.IsFieldConditional).Select(c => c.Value)];
            ResolvePaint();
            foreach (var dash in Get("strokeDash")?.Scale?.DashRange ?? [])
            {
                CheckDashLength(dash.Count);
            }
        }

        /// <summary>
        ///     Lists the conditions of a channel that this call can evaluate.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="node">The channel definition.</param>
        /// <returns>The conditions in order: those on a test that the parser compiled (a condition on a parameter, a test that no expression can say, and the conditions of a call without expressions are left out).</returns>
        private List<ChannelCondition> ConditionsOf(string name, SpecNode node)
        {
            var conditions = new List<ChannelCondition>();
            if (_owner._expressions is not { } expressions || !ChannelDefinitions.CanCondition(name))
            {
                return conditions;
            }

            var fieldDefinition = ChannelDefinitions.FieldDefinitionOf(node);
            foreach (var condition in ChannelDefinitions.TestConditions(node))
            {
                if (expressions.Compiled.Get(condition["test"]) is { } test)
                {
                    conditions.Add(new ChannelCondition(
                        test,
                        ReferenceEquals(condition, fieldDefinition),
                        condition["value"],
                        expressions.Compiled.GetReference(condition["value"])));
                }
            }

            return conditions;
        }

        /// <summary>
        ///     Builds the items of the mark.
        /// </summary>
        /// <returns>The items in data order.</returns>
        public List<SceneItem> Build() => _type switch
        {
            "bar" or "rect" => Rects(),
            "tick" => Ticks(),
            "rule" => Rules(),
            _ => Symbols(),
        };

        /// <summary>
        ///     Reads the style names of a mark definition: one name or a list of names.
        /// </summary>
        /// <param name="style">The <c>style</c> property.</param>
        /// <returns>The names.</returns>
        private static IEnumerable<string> StyleNames(SpecNode style)
        {
            if (style.IsString)
            {
                return [style.StringValue!];
            }

            return style.IsArray ? style.Items.Where(i => i.IsString).Select(i => i.StringValue!) : [];
        }

        /// <summary>
        ///     Gets a channel by name.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <returns>The channel, or null when the encoding does not have it.</returns>
        private Channel? Get(string name) => _channels.GetValueOrDefault(name);

        /// <summary>
        ///     Reads a property of the mark definition.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The value, or null when the definition does not have it.</returns>
        private ConfigNode? Def(string name) => _def?[name] is { } node ? ConfigNode.FromSpec(node) : null;

        /// <summary>
        ///     Reads a property from the Vega-Lite configuration: the styles of the mark, then <c>config.&lt;mark&gt;</c>, then <c>config.mark</c>.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The value, or null.</returns>
        private ConfigNode? Vl(string name)
        {
            var vl = _owner._config.VegaLite;
            ConfigNode? found = null;
            if (vl["style"] is { } styles)
            {
                foreach (var style in _styleNames)
                {
                    found = styles[style]?[name] ?? found;
                }
            }

            return found ?? vl[_type]?[name] ?? vl["mark"]?[name];
        }

        /// <summary>
        ///     Reads a property from the mark definition, then the Vega-Lite configuration, then (optionally) the Vega styles and mark defaults.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <param name="vega"><see langword="true"/> to include Vega's own defaults (the <c>point</c> style, for example).</param>
        /// <returns>The value, or null.</returns>
        private ConfigNode? Prop(string name, bool vega = false) =>
            Def(name) ?? (vega ? _owner._config.ResolveMarkProperty(_type, _styleNames.Skip(1).ToList(), name) : Vl(name));

        /// <summary>
        ///     Resolves the color, stroke and opacity defaults the way Vega-Lite's <c>color()</c> rule and its mark definition initializer do.
        /// </summary>
        private void ResolvePaint()
        {
            var filled = Prop("filled")?.AsBool() ?? ConfigResolver.IsFilledByDefault(_type);
            var color = Def("color")?.AsString();
            if (Def("fill") is { } explicitFill)
            {
                _fill = explicitFill.AsString();
            }
            else
            {
                _fill = filled ? color ?? Vl("fill")?.AsString() ?? Vl("color")?.AsString() : Vl("fill")?.AsString();
                if (_fill is null && TransparentMarks.Contains(_type))
                {
                    _fill = "transparent";
                }
            }

            _stroke = Def("stroke") is { } explicitStroke
                ? explicitStroke.AsString()
                : Vl("stroke")?.AsString();
            if (!filled && Def("stroke") is null)
            {
                _stroke = color ?? _stroke ?? Vl("color")?.AsString();
            }

            // The color channel drives the fill of a filled mark and the stroke of an unfilled one; fill and stroke channels win
            var colorChannel = Get("color");
            _fillChannel = Get("fill") ?? (filled ? colorChannel : null);
            _strokeChannel = Get("stroke") ?? (filled ? null : colorChannel);

            // A row-bound expression of the mark definition follows the same precedence: fill (or stroke) wins over color
            _fillExpression = DefExpression(Def("fill") is not null || !filled ? "fill" : "color");
            _strokeExpression = DefExpression(Def("stroke") is not null || filled ? "stroke" : "color");
            _opacityExpression = DefExpression("opacity");
            _strokeWidthExpression = DefExpression("strokeWidth");
            _sizeExpression = DefExpression("size");

            // Opacity: the definition, else the reduced default of point-like marks that nothing configures
            if (Def("opacity")?.AsNumber() is { } opacity)
            {
                _opacity = opacity;
            }
            else if (Vl("opacity") is null && Vl("fillOpacity") is null && Def("fillOpacity") is null && ReducedOpacityMarks.Contains(_type))
            {
                _opacity = ConfigResolver.DefaultReducedOpacity;
            }
            else
            {
                _opacity = Vl("opacity")?.AsNumber() ?? 1;
            }

            // The opacities of the fill and the stroke and the dash pattern: the mark's, which a channel replaces for a row
            _fillOpacity = Prop("fillOpacity")?.AsNumber() ?? 1;
            _strokeOpacity = Prop("strokeOpacity")?.AsNumber() ?? 1;
            _dash = ConstantDash(Prop("strokeDash"));
        }

        /// <summary>
        ///     Gets the paint of one row.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="strokeWidth">The stroke width of the mark; null for the default of one pixel.</param>
        /// <param name="paint">Receives the paint.</param>
        /// <returns><see langword="false"/> when the row is invalid (a channel with a continuous domain has no number for it).</returns>
        private bool TryPaint(int row, double? strokeWidth, out Paint paint)
        {
            paint = default;
            if (!ChannelsValid(row))
            {
                return false;
            }

            var fill = ColorOf(_fillChannel, row) ?? ExpressionColor(_fillExpression, row) ?? _fill;
            var stroke = ColorOf(_strokeChannel, row) ?? ExpressionColor(_strokeExpression, row) ?? _stroke;
            var opacity = _opacity;
            if (Get("opacity") is { } channel)
            {
                if (Encoded(channel, row, _opacity) is not { } encoded)
                {
                    return false;
                }

                opacity = encoded;
            }
            else if (ExpressionNumber(_opacityExpression, row) is { } computed)
            {
                opacity = computed;
            }

            // The stroke width, the opacities and the dash pattern: a channel replaces what the mark says
            if (Get("strokeWidth") is { } widthChannel)
            {
                if (Encoded(widthChannel, row, strokeWidth) is not { } width)
                {
                    return false;
                }

                strokeWidth = width;
            }
            else
            {
                strokeWidth = ExpressionNumber(_strokeWidthExpression, row) ?? strokeWidth;
            }

            var fillOpacity = _fillOpacity;
            if (Get("fillOpacity") is { } fillChannel)
            {
                if (Encoded(fillChannel, row, _fillOpacity) is not { } encodedFill)
                {
                    return false;
                }

                fillOpacity = encodedFill;
            }

            var strokeOpacity = _strokeOpacity;
            if (Get("strokeOpacity") is { } strokeOpacityChannel)
            {
                if (Encoded(strokeOpacityChannel, row, _strokeOpacity) is not { } encodedStroke)
                {
                    return false;
                }

                strokeOpacity = encodedStroke;
            }

            var dash = Get("strokeDash") is { } dashChannel ? DashOf(dashChannel, row) ?? _dash : _dash;
            paint = new Paint(fill, stroke, strokeWidth, opacity, fillOpacity, strokeOpacity, dash);
            return true;
        }

        /// <summary>
        ///     Reads a property of the mark definition that is an <c>{"expr": ...}</c> reference the parser compiled.
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The compiled expression, or null when the definition has no such expression.</returns>
        private CompiledExpression? DefExpression(string name) =>
            _def?[name] is { IsObject: true } reference ? _owner._expressions?.Compiled.GetReference(reference) : null;

        /// <summary>
        ///     Evaluates a row-bound expression for a row.
        /// </summary>
        /// <param name="expression">The compiled expression.</param>
        /// <param name="row">The row, which is the <c>datum</c> of the expression.</param>
        /// <returns>The value.</returns>
        private DataValue Evaluate(CompiledExpression expression, int row) =>
            _owner._expressions!.Evaluator.Evaluate(expression, _table, row);

        /// <summary>
        ///     Evaluates an expression that gives a color.
        /// </summary>
        /// <param name="expression">The compiled expression, or null.</param>
        /// <param name="row">The row.</param>
        /// <returns>The text of the result; null when there is no expression or the result is not a text, so that the default applies.</returns>
        private string? ExpressionColor(CompiledExpression? expression, int row) =>
            expression is null ? null : Evaluate(expression, row).Text;

        /// <summary>
        ///     Evaluates an expression that gives a number.
        /// </summary>
        /// <param name="expression">The compiled expression, or null.</param>
        /// <param name="row">The row.</param>
        /// <returns>The result; null when there is no expression or the result is not a finite number, so that the default applies.</returns>
        private double? ExpressionNumber(CompiledExpression? expression, int row)
        {
            if (expression is null)
            {
                return null;
            }

            var value = Evaluate(expression, row);
            return value.IsNumber && double.IsFinite(value.Number) ? value.Number : null;
        }

        /// <summary>
        ///     Gets the color of a row from a color channel.
        /// </summary>
        /// <remarks>
        ///     A constant <c>value</c> (or its expression) is the color; a field is mapped through the scale of the channel: the colors of an ordinal
        ///     scale, the color of a sequential or diverging ramp, the class color of a quantize, quantile or threshold scale.
        /// </remarks>
        /// <param name="channel">The channel, or null.</param>
        /// <param name="row">The row.</param>
        /// <returns>The color, or null to use the default (also for a value outside the domain of a scale; a continuous domain that has no number for the row never gets here: see <see cref="ChannelsValid"/>).</returns>
        private string? ColorOf(Channel? channel, int row)
        {
            if (channel is null)
            {
                return null;
            }

            if (Matching(channel, row) is { } hit)
            {
                if (hit.IsField)
                {
                    return channel.Model?.MapText(channel.Read(_table, row));
                }

                if (hit.ValueExpression is { } conditional)
                {
                    return ExpressionColor(conditional, row);
                }

                return hit.Value is { IsString: true } text ? text.StringValue : null;
            }

            if (channel.ValueExpression is { } expression)
            {
                return ExpressionColor(expression, row);
            }

            if (channel.Value is { IsString: true } value)
            {
                return value.StringValue;
            }

            return channel.IsFieldConditional ? null : channel.Model?.MapText(channel.Read(_table, row));
        }

        /// <summary>
        ///     Finds the first condition of a channel whose test holds for a row.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="row">The row, which is the <c>datum</c> of the tests.</param>
        /// <returns>The condition; null when the channel has none or no test holds.</returns>
        private ChannelCondition? Matching(Channel channel, int row)
        {
            return channel.Conditions.FirstOrDefault(condition => ExpressionEvaluator.IsTruthy(Evaluate(condition.Test, row)));
        }

        /// <summary>
        ///     Works out what a size, opacity, stroke width or angle channel gives a row: the value of the first condition that holds, else its scaled field or
        ///     its constant value.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The number, or nothing at all when the channel is conditional, no condition holds and the channel has no value of its own.</returns>
        private Resolved Resolve(Channel channel, int row)
        {
            if (Matching(channel, row) is { } hit)
            {
                if (hit.IsField)
                {
                    return new Resolved(channel.Model?.Map(channel.Read(_table, row)), false);
                }

                // A condition value of the wrong kind leaves the default in place, as an expression of the wrong kind does
                var number = hit.ValueExpression is { } conditional ? ExpressionNumber(conditional, row) : hit.Value?.AsNumber();
                return number is null ? new Resolved(null, true) : new Resolved(number, false);
            }

            if (channel.ValueExpression is { } expression)
            {
                return new Resolved(ExpressionNumber(expression, row), false);
            }

            if (!channel.IsFieldConditional && channel.Model is { } model)
            {
                return new Resolved(model.Map(channel.Read(_table, row)), false);
            }

            var constant = channel.Value?.AsNumber();
            return new Resolved(constant, constant is null && channel.Conditions.Count > 0);
        }

        /// <summary>
        ///     Gets a number of a row from a size, opacity, stroke width or angle channel: its constant value, its scaled field, or the value of a condition that holds.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="row">The row.</param>
        /// <param name="fallback">What a conditional channel gives a row for which no condition holds when it has no value of its own (the property of the mark), or null.</param>
        /// <returns>The number, or null when the row has no valid value.</returns>
        private double? Encoded(Channel channel, int row, double? fallback = null)
        {
            var resolved = Resolve(channel, row);
            return resolved.IsDefault ? fallback : resolved.Value;
        }

        /// <summary>
        ///     Checks that every channel whose scale has a continuous domain has a number for a row (Vega-Lite's invalid filter).
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns><see langword="false"/> when a field of such a channel holds no number in the row.</returns>
        private bool ChannelsValid(int row)
        {
            foreach (var channel in _filtered)
            {
                if (channel.FieldIndex >= 0 && !channel.Read(_table, row).TryGetQuantitative(out _))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Reads a dash pattern that a mark property or a channel value gives.
        /// </summary>
        /// <param name="node">The value, or null.</param>
        /// <returns>The pattern, or null when the value is not a list (a solid line); the entries that are not numbers are left out.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the pattern has more than <see cref="MaxDashLength"/> entries.</exception>
        private IReadOnlyList<double>? ConstantDash(ConfigNode? node)
        {
            if (node is not { Kind: ConfigNodeKind.Array })
            {
                return null;
            }

            CheckDashLength(node.Items.Count);
            return [.. node.Items.Select(i => i.AsNumber()).Where(n => n is not null).Select(n => n!.Value)];
        }

        /// <summary>
        ///     Checks the length of a dash pattern against <see cref="MaxDashLength"/>.
        /// </summary>
        /// <param name="length">The number of entries.</param>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the pattern is longer than the limit.</exception>
        private void CheckDashLength(int length)
        {
            if (length > MaxDashLength)
            {
                throw new VegaLiteRenderException($"A strokeDash pattern has {length} entries, more than the limit of {MaxDashLength}.", _location);
            }
        }

        /// <summary>
        ///     Gets the dash pattern of a row from a <c>strokeDash</c> channel: its constant <c>value</c> or the pattern its scale gives.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The pattern, or null to use the pattern of the mark.</returns>
        private IReadOnlyList<double>? DashOf(Channel channel, int row)
        {
            if (Matching(channel, row) is { } hit)
            {
                if (hit.IsField)
                {
                    return DashFromScale(channel, row);
                }

                return hit.Value is { } conditional ? ConstantDash(ConfigNode.FromSpec(conditional)) : null;
            }

            if (!channel.IsFieldConditional && DashFromScale(channel, row) is { } scaled)
            {
                return scaled;
            }

            return channel.Value is { } value ? ConstantDash(ConfigNode.FromSpec(value)) : null;
        }

        /// <summary>
        ///     Gets the dash pattern that the scale of a <c>strokeDash</c> channel gives the field of a row.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The pattern, or null when the channel has no scale or the value has none.</returns>
        private IReadOnlyList<double>? DashFromScale(Channel channel, int row)
        {
            if (channel.Model is not { } model || channel.Scale is not { } scale)
            {
                return null;
            }

            var index = model.MapIndex(channel.Read(_table, row));
            return index >= 0 && index < scale.DashRange.Count ? scale.DashRange[index] : null;
        }

        /// <summary>
        ///     Gets the shape of a row from a <c>shape</c> channel: its constant <c>value</c> or the shape its scale gives.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The shape name or path, or null to use the shape of the mark.</returns>
        private string? ShapeOf(Channel channel, int row)
        {
            if (Matching(channel, row) is { } hit)
            {
                if (hit.IsField)
                {
                    return channel.Model?.MapText(channel.Read(_table, row));
                }

                if (hit.ValueExpression is { } conditional)
                {
                    return ExpressionColor(conditional, row);
                }

                return hit.Value is { IsString: true } text ? text.StringValue : null;
            }

            if (channel.ValueExpression is { } expression)
            {
                return ExpressionColor(expression, row);
            }

            if (!channel.IsFieldConditional && channel.Model is { } model)
            {
                return model.MapText(channel.Read(_table, row));
            }

            return channel.Value is { IsString: true } value ? value.StringValue : null;
        }

        /// <summary>
        ///     Gets the position of a symbol, rule or tick along one axis: the center of the band, the scaled value, a constant or the middle.
        /// </summary>
        /// <param name="channel">The channel, or null when the encoding has none.</param>
        /// <param name="extent">The size of the data rectangle along the axis.</param>
        /// <param name="row">The row.</param>
        /// <returns>The position, or null when the row is invalid.</returns>
        private double? PointPosition(Channel? channel, double extent, int row)
        {
            if (channel is null)
            {
                return extent / 2;
            }

            if (channel.Model is { } model)
            {
                return Center(channel, model, row);
            }

            return ConstantPosition(channel, extent) ?? extent / 2;
        }

        /// <summary>
        ///     Gets the position of the value of a row on a position channel: the center of its band, the scaled value, the upper end of a
        ///     stack, or, for a field that is binned on a continuous scale, the middle of the bin (Vega-Lite's <c>bandPosition</c> 0.5).
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="model">The scale of the channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The position, or null when the row is invalid.</returns>
        private double? Center(Channel channel, ScaleModel model, int row)
        {
            if (!channel.HasBinEnds)
            {
                return model.MapCenter(channel.Read(_table, row));
            }

            return BinEdges(channel, model, row) is { } edges ? (edges.Start + edges.End) / 2 : null;
        }

        /// <summary>
        ///     Gets the positions of the two boundaries of the bin of a row.
        /// </summary>
        /// <param name="channel">The channel (<see cref="Channel.HasBinEnds"/>).</param>
        /// <param name="model">The scale of the channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The positions of the start and of the end of the bin, or null when the row has no valid bin.</returns>
        private (double Start, double End)? BinEdges(Channel channel, ScaleModel model, int row)
        {
            var start = channel.Read(_table, row);
            if (model.Map(start) is not { } startPosition)
            {
                return null;
            }

            double? endPosition;
            if (channel.Field2Index >= 0)
            {
                endPosition = model.Map(_table.GetValue(row, channel.Field2Index));
            }
            else
            {
                endPosition = start.TryGetQuantitative(out var number) && channel.BinStep is { } step
                    ? model.Map(DataValue.FromNumber(number + step))
                    : null;
            }

            return endPosition is { } end ? (startPosition, end) : null;
        }

        /// <summary>
        ///     Reads the constant position of a <c>value</c> channel: a number, or the keywords <c>width</c> and <c>height</c>.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="extent">The size of the data rectangle along the axis.</param>
        /// <returns>The position, or null when the channel has no usable value.</returns>
        private static double? ConstantPosition(Channel channel, double extent) => ConstantPosition(channel.Value, extent);

        /// <summary>
        ///     Reads a constant position: a number, or the keywords <c>width</c> and <c>height</c>.
        /// </summary>
        /// <param name="value">The <c>value</c> of a channel, or null.</param>
        /// <param name="extent">The size of the data rectangle along the axis.</param>
        /// <returns>The position, or null when there is no usable value.</returns>
        private static double? ConstantPosition(SpecNode? value, double extent) => value switch
        {
            { IsNumber: true } n => n.NumberValue,
            { IsString: true } s when s.StringValue is "width" or "height" => extent,
            _ => null,
        };

        /// <summary>
        ///     Gets the position of the second end of a range that a channel spans: the lower end of a stack, or the field, datum or value of <c>x2</c> or <c>y2</c>.
        /// </summary>
        /// <param name="channel">The channel that is stacked or has a range.</param>
        /// <param name="model">The scale of the channel.</param>
        /// <param name="extent">The size of the data rectangle along the axis.</param>
        /// <param name="row">The row.</param>
        /// <returns>The position, or null when the row is invalid.</returns>
        private double? SecondEnd(Channel channel, ScaleModel model, double extent, int row)
        {
            // An explicit pixel value of x2 or y2 wins over the lower end of a stack
            if (channel.SecondValue is not null)
            {
                return ConstantPosition(channel.SecondValue, extent);
            }

            if (channel.Field2Index >= 0)
            {
                return model.MapCenter(_table.GetValue(row, channel.Field2Index));
            }

            if (channel.SecondDatum is { } datum)
            {
                return model.MapCenter(datum);
            }

            return ConstantPosition(channel.SecondValue, extent);
        }

        /// <summary>
        ///     Gets the position where a range of a measure axis starts (Vega-Lite's <c>zeroOrMin</c>): zero when the domain has it, else its first value.
        /// </summary>
        /// <param name="model">The scale.</param>
        /// <returns>The position.</returns>
        private static double ZeroOrMin(ScaleModel model)
        {
            if (model.Kind != ScaleKind.Linear)
            {
                return model.Domain.Count > 0 ? model.Map(model.Domain[0]) ?? 0 : 0;
            }

            var low = Math.Min(model.DomainStart, model.DomainEnd);
            var high = Math.Max(model.DomainStart, model.DomainEnd);
            return model.Map(low <= 0 && 0 <= high ? 0 : model.DomainStart);
        }

        /// <summary>
        ///     Gets the two ends of a range along one axis: the value, and the zero (or the first domain value) it runs to.
        /// </summary>
        /// <param name="isX"><see langword="true"/> for the x axis.</param>
        /// <param name="channel">The channel, or null.</param>
        /// <param name="row">The row.</param>
        /// <returns>The main end and the other end, or null when the row is invalid.</returns>
        private (double Main, double Other)? RangePair(bool isX, Channel? channel, int row)
        {
            var extent = isX ? _width : _height;

            // Without a value the range spans the data rectangle: from the far edge (x: right, y: top) to the near one
            var whole = isX ? (extent, 0.0) : (0.0, extent);
            if (channel is null)
            {
                return whole;
            }

            if (channel.Model is { } model)
            {
                if (channel.IsStacked || channel.HasRange)
                {
                    // A stack runs from its lower to its upper end, a range from x (or y) to x2 (or y2); a row with an invalid end is not drawn
                    return model.MapCenter(channel.Read(_table, row)) is { } high && SecondEnd(channel, model, extent, row) is { } low ? (high, low) : null;
                }

                return Center(channel, model, row) is { } main ? (main, ZeroOrMin(model)) : null;
            }

            return ConstantPosition(channel, extent) is { } constant ? (constant, whole.Item2) : whole;
        }

        /// <summary>
        ///     Reads how thick a bar or tick is along an axis.
        /// </summary>
        /// <param name="isX"><see langword="true"/> for the x axis.</param>
        /// <param name="useSizeChannel">Whether the <c>size</c> property and channel apply to this axis.</param>
        /// <returns>The sizing.</returns>
        private Sizing SizingFor(bool isX, bool useSizeChannel)
        {
            var node = _def?[isX ? "width" : "height"];
            double? fixedSize = node is { IsNumber: true } ? node.NumberValue : null;
            double? band = node is { IsObject: true } && node["band"] is { IsNumber: true } b ? b.NumberValue : null;
            if (fixedSize is null && band is null && useSizeChannel && Def("size")?.AsNumber() is { } size)
            {
                fixedSize = size;
            }

            var encoded = useSizeChannel ? Get("size") : null;
            if (encoded is { Model: null, Conditions.Count: 0 } && encoded.Value?.AsNumber() is { } constant)
            {
                fixedSize = constant;
                encoded = null;
            }

            return new Sizing(fixedSize, band, encoded is { Model: null, Conditions.Count: 0 } ? null : encoded);
        }

        /// <summary>
        ///     Gets the thickness for a row from a sizing.
        /// </summary>
        /// <param name="sizing">The sizing.</param>
        /// <param name="row">The row.</param>
        /// <param name="valid">Receives <see langword="false"/> when the row's size value is not a number.</param>
        /// <returns>The thickness in pixels, or null when the sizing gives none.</returns>
        private double? Thickness(Sizing sizing, int row, out bool valid)
        {
            valid = true;
            if (sizing.Encoded is { } channel)
            {
                var resolved = Resolve(channel, row);
                if (resolved.IsDefault)
                {
                    return sizing.Fixed;
                }

                valid = resolved.Value is not null;
                return resolved.Value;
            }

            return sizing.Fixed;
        }

        /// <summary>
        ///     Works out where a bar, rectangle or tick spans along one axis.
        /// </summary>
        /// <param name="isX"><see langword="true"/> for the x axis.</param>
        /// <param name="channel">The channel, or null.</param>
        /// <param name="barOrTick">Whether this is the axis along which a bar or tick has a thickness (Vega-Lite's <c>isBarOrTickBand</c>).</param>
        /// <param name="sizing">The thickness along this axis.</param>
        /// <param name="row">The row.</param>
        /// <returns>The start and the length, or null when the row is invalid.</returns>
        private (double Start, double Length)? Span(bool isX, Channel? channel, bool barOrTick, Sizing sizing, int row)
        {
            var extent = isX ? _width : _height;
            var model = channel?.Model;
            if (model is { Kind: ScaleKind.Band or ScaleKind.Point })
            {
                return BandSpan(channel!, model, sizing, row);
            }

            // A field that is binned on a continuous scale spans its bin unless a size is given (Vega-Lite's rectBinPosition)
            if (channel is { HasBinEnds: true } && model is not null && sizing.Fixed is null && sizing.Encoded is null)
            {
                return BinSpan(channel, model, row);
            }

            if (barOrTick && channel?.HasRange != true)
            {
                var thickness = Thickness(sizing, row, out var valid);
                if (!valid)
                {
                    return null;
                }

                if (model is { Kind: ScaleKind.Linear })
                {
                    var center = Center(channel!, model, row);
                    var length = thickness ?? Prop("continuousBandSize")?.AsNumber() ?? 5;
                    return center is { } c ? (c - (length / 2), length) : null;
                }

                // No position on this axis: centered, as thick as the rectangle less the band padding
                var middle = channel is null ? extent / 2 : ConstantPosition(channel, extent) ?? extent / 2;
                var padded = thickness ?? ((1 - BandPadding()) * extent);
                return (middle - (padded / 2), padded);
            }

            return RangePair(isX, channel, row) is { } pair ? (Math.Min(pair.Main, pair.Other), Math.Abs(pair.Main - pair.Other)) : null;
        }

        /// <summary>
        ///     Works out where a bar or rectangle spans along an axis whose field is binned (Vega-Lite's <c>rectBinPosition</c>): from the start of
        ///     the bin to its end, each edge drawn <c>binSpacing</c> / 2 inside it and the whole span shifted by the half pixel that the axis is
        ///     translated by; a bin thinner than <c>minBandSize</c> is widened around its middle to that size, without spacing.
        /// </summary>
        /// <param name="channel">The binned channel.</param>
        /// <param name="model">The scale of the channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The start and the length, or null when the row has no valid bin.</returns>
        private (double Start, double Length)? BinSpan(Channel channel, ScaleModel model, int row)
        {
            if (BinEdges(channel, model, row) is not { } edges)
            {
                return null;
            }

            var low = Math.Min(edges.Start, edges.End);
            var high = Math.Max(edges.Start, edges.End);
            var spacing = Prop("binSpacing")?.AsNumber() ?? (_type == "bar" ? DefaultBarBinSpacing : 0);
            var minimum = Prop("minBandSize")?.AsNumber() ?? 0;
            if (minimum > 0 && high - low < minimum)
            {
                var growth = 0.5 * (minimum - (high - low));
                low -= growth;
                high += growth;
            }
            else
            {
                low += spacing / 2;
                high -= spacing / 2;
            }

            // Edges that spacing has crossed still make a rectangle of that width, as Vega draws it
            var start = Math.Min(low, high) + AxisTranslate;
            return (start, Math.Abs(high - low));
        }

        /// <summary>
        ///     Gets the inner padding that a missing position leaves: the configured band padding of the mark.
        /// </summary>
        /// <returns>The padding as a fraction of the band.</returns>
        private double BandPadding()
        {
            var config = _owner._config;
            var name = "rectBandPaddingInner";
            if (_type == "tick")
            {
                name = "tickBandPaddingInner";
            }
            else if (_type == "bar")
            {
                name = "barBandPaddingInner";
            }
            return config.GetNumber("scale.bandPaddingInner") ?? config.GetNumber("scale." + name) ?? 0;
        }

        /// <summary>
        ///     Works out where a mark spans along a discrete axis: the band, or a centered size.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="model">The band or point scale.</param>
        /// <param name="sizing">The thickness along this axis.</param>
        /// <param name="row">The row.</param>
        /// <returns>The start and the length, or null when the row is invalid.</returns>
        private (double Start, double Length)? BandSpan(Channel channel, ScaleModel model, Sizing sizing, int row)
        {
            if (model.Map(channel.Read(_table, row)) is not { } start)
            {
                return null;
            }

            var thickness = Thickness(sizing, row, out var valid);
            if (!valid)
            {
                return null;
            }

            thickness ??= Prop("discreteBandSize")?.AsNumber();
            var isBand = model.Kind == ScaleKind.Band;
            if (thickness is { } size)
            {
                var center = start + (isBand ? model.Bandwidth / 2 : 0);
                return (center - (size / 2), size);
            }

            if (isBand)
            {
                var band = sizing.Band ?? 1;
                var minimum = Prop("minBandSize")?.AsNumber() ?? 0;
                return (start + (model.Bandwidth * (1 - band) / 2), Math.Max(minimum, model.Bandwidth * band));
            }

            var length = (model.Step > 0 ? model.Step : ConfigResolver.DefaultStep) - 2;
            return (start - (length / 2), length);
        }

        /// <summary>
        ///     Checks the cancellation token once per batch of rows.
        /// </summary>
        /// <param name="row">The row that is about to be processed.</param>
        private void Batch(int row)
        {
            if (row % BatchSize == 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        ///     Counts one item against the scene budget before it is created, so the budget counts exactly the items
        ///     that exist (a row that draws nothing costs nothing) and the item that would pass the limit is never
        ///     allocated.
        /// </summary>
        private void Reserve() => _owner._budget.Add(1, _location);

        /// <summary>
        ///     Builds the rectangles of a bar or rect mark.
        /// </summary>
        /// <returns>The items.</returns>
        private List<SceneItem> Rects()
        {
            var orient = _orient;
            var barX = _type == "bar" && orient == "vertical";
            var barY = _type == "bar" && orient == "horizontal";
            var x = Get("x");
            var y = Get("y");
            var sizingX = SizingFor(isX: true, useSizeChannel: barX);
            var sizingY = SizingFor(isX: false, useSizeChannel: barY);
            var radius = Prop("cornerRadius")?.AsNumber() ?? 0;
            var strokeWidth = Prop("strokeWidth", vega: true)?.AsNumber();
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            for (var row = 0; row < _table.RowCount; row++)
            {
                Batch(row);
                if (Span(isX: true, x, barX, sizingX, row) is not { } xs || Span(isX: false, y, barY, sizingY, row) is not { } ys || !TryPaint(row, strokeWidth, out var paint))
                {
                    continue;
                }

                Reserve();
                items.Add(Style(new SceneRect("mark-" + _type, xs.Start, ys.Start, xs.Length, ys.Length) { CornerRadius = radius }, paint));
            }

            return items;
        }

        /// <summary>
        ///     Builds the rectangles of a tick mark.
        /// </summary>
        /// <returns>The items.</returns>
        private List<SceneItem> Ticks()
        {
            var horizontal = _orient == "horizontal";
            var x = Get("x");
            var y = Get("y");
            var thickness = Prop("thickness")?.AsNumber() ?? 1;
            var sizing = SizingFor(isX: horizontal, useSizeChannel: true);
            var strokeWidth = Prop("strokeWidth", vega: true)?.AsNumber();
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            for (var row = 0; row < _table.RowCount; row++)
            {
                Batch(row);
                if (!TryPaint(row, strokeWidth, out var paint))
                {
                    continue;
                }

                // The tick runs along its size axis and has the thickness across it, centered on the value
                (double X, double Y, double Width, double Height)? box = null;
                if (horizontal)
                {
                    if (Span(isX: true, x, barOrTick: true, sizing, row) is { } xs && PointPosition(y, _height, row) is { } py)
                    {
                        box = (xs.Start, py - (thickness / 2), xs.Length, thickness);
                    }
                }
                else if (Span(isX: false, y, barOrTick: true, sizing, row) is { } ys && PointPosition(x, _width, row) is { } px)
                {
                    box = (px - (thickness / 2), ys.Start, thickness, ys.Length);
                }

                if (box is { } b)
                {
                    Reserve();
                    items.Add(Style(new SceneRect("mark-tick", b.X, b.Y, b.Width, b.Height), paint));
                }
            }

            return items;
        }

        /// <summary>
        ///     Builds the rules of a rule mark.
        /// </summary>
        /// <returns>The items.</returns>
        private List<SceneItem> Rules()
        {
            var x = Get("x");
            var y = Get("y");
            if (x is null && y is null)
            {
                return [];
            }

            // A range on one channel (x2 or y2) decides the direction of the rule, as a stack does for its measure axis
            var xRange = x is { HasRange: true };
            var yRange = y is { HasRange: true };
            var vertical = xRange == yRange ? _orient != "horizontal" : yRange;
            var widthDefault = Def("strokeWidth")?.AsNumber() ?? Def("size")?.AsNumber() ?? Prop("strokeWidth", vega: true)?.AsNumber();
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            for (var row = 0; row < _table.RowCount; row++)
            {
                Batch(row);
                var width = widthDefault;
                if (Get("size") is { } sizeChannel)
                {
                    width = Encoded(sizeChannel, row, widthDefault);
                    if (width is null)
                    {
                        continue;
                    }
                }

                if (!TryPaint(row, width, out var paint))
                {
                    continue;
                }

                (double X, double Y, double X2, double Y2)? line = null;
                if (xRange && yRange)
                {
                    // Both channels span a range: the rule is the segment from (x, y) to (x2, y2)
                    if (RangePair(isX: true, x, row) is { } both && RangePair(isX: false, y, row) is { } other)
                    {
                        line = (both.Main, other.Main, both.Other, other.Other);
                    }
                }
                else if (vertical)
                {
                    if (PointPosition(x, _width, row) is { } px && RangePair(isX: false, y, row) is { } ys)
                    {
                        line = (px, ys.Main, px, ys.Other);
                    }
                }
                else if (PointPosition(y, _height, row) is { } py && RangePair(isX: true, x, row) is { } xs)
                {
                    line = (xs.Main, py, xs.Other, py);
                }

                if (line is { } l)
                {
                    Reserve();
                    items.Add(Style(new SceneRule("mark-rule", l.X, l.Y, l.X2, l.Y2), paint));
                }
            }

            return items;
        }

        /// <summary>
        ///     Builds the symbols of a point, circle or square mark.
        /// </summary>
        /// <returns>The items.</returns>
        private List<SceneItem> Symbols()
        {
            var x = Get("x");
            var y = Get("y");
            var shape = _type switch
            {
                "circle" => "circle",
                "square" => "square",
                _ => Prop("shape", vega: true)?.AsString() ?? "circle",
            };
            var shapeChannel = _type == "point" ? Get("shape") : null;
            var angleChannel = Get("angle");
            var angle = Prop("angle")?.AsNumber() ?? 0;
            var defaultSize = Prop("size", vega: true)?.AsNumber() ?? 30;
            var strokeWidth = Prop("strokeWidth", vega: true)?.AsNumber();
            var sizeChannel = Get("size");
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            for (var row = 0; row < _table.RowCount; row++)
            {
                Batch(row);
                var size = sizeChannel is null ? ExpressionNumber(_sizeExpression, row) ?? defaultSize : Encoded(sizeChannel, row, defaultSize);
                if (size is not { } area || PointPosition(x, _width, row) is not { } px || PointPosition(y, _height, row) is not { } py || !TryPaint(row, strokeWidth, out var paint))
                {
                    continue;
                }

                var rowAngle = angle;
                if (angleChannel is not null)
                {
                    if (Encoded(angleChannel, row, angle) is not { } encodedAngle)
                    {
                        continue;
                    }

                    rowAngle = encodedAngle;
                }

                var rowShape = shapeChannel is null ? shape : ShapeOf(shapeChannel, row) ?? shape;
                Reserve();
                items.Add(Style(new SceneSymbol("mark-" + _type, px, py) { Shape = rowShape, Size = area, Angle = rowAngle }, paint));
            }

            return items;
        }

        /// <summary>
        ///     Applies the paint and the stroke properties of the mark to a rectangle.
        /// </summary>
        /// <param name="item">The rectangle.</param>
        /// <param name="paint">The paint of the row.</param>
        /// <returns>The styled rectangle.</returns>
        private SceneRect Style(SceneRect item, Paint paint) => item with
        {
            Fill = paint.Fill,
            Stroke = paint.Stroke,
            StrokeWidth = paint.StrokeWidth,
            Opacity = paint.Opacity,
            FillOpacity = paint.FillOpacity,
            StrokeOpacity = paint.StrokeOpacity,
            StrokeDash = paint.StrokeDash,
            StrokeDashOffset = Prop("strokeDashOffset")?.AsNumber() ?? 0,
            StrokeCap = Prop("strokeCap")?.AsString(),
            StrokeJoin = Prop("strokeJoin")?.AsString(),
        };

        /// <summary>
        ///     Applies the paint and the stroke properties of the mark to a symbol.
        /// </summary>
        /// <param name="item">The symbol.</param>
        /// <param name="paint">The paint of the row.</param>
        /// <returns>The styled symbol.</returns>
        private SceneSymbol Style(SceneSymbol item, Paint paint) => item with
        {
            Fill = paint.Fill,
            Stroke = paint.Stroke,
            StrokeWidth = paint.StrokeWidth,
            Opacity = paint.Opacity,
            FillOpacity = paint.FillOpacity,
            StrokeOpacity = paint.StrokeOpacity,
            StrokeDash = paint.StrokeDash,
            StrokeDashOffset = Prop("strokeDashOffset")?.AsNumber() ?? 0,
            StrokeCap = Prop("strokeCap")?.AsString(),
            StrokeJoin = Prop("strokeJoin")?.AsString(),
        };

        /// <summary>
        ///     Applies the paint and the stroke properties of the mark to a rule: the stroke carries the color and the width.
        /// </summary>
        /// <param name="item">The rule.</param>
        /// <param name="paint">The paint of the row.</param>
        /// <returns>The styled rule.</returns>
        private SceneRule Style(SceneRule item, Paint paint) => item with
        {
            Stroke = paint.Stroke ?? paint.Fill,
            StrokeWidth = paint.StrokeWidth,
            Opacity = paint.Opacity,
            StrokeOpacity = paint.StrokeOpacity,
            StrokeDash = paint.StrokeDash,
            StrokeDashOffset = Prop("strokeDashOffset")?.AsNumber() ?? 0,
            StrokeCap = Prop("strokeCap")?.AsString(),
        };
    }
}

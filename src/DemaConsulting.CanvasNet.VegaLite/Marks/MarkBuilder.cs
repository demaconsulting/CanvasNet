// Portions derived from Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

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
///         band padding (0.1 for a bar, 0.25 for a tick) of the rectangle. A symbol sits at the center of its band or at its value and
///         at the middle of the rectangle for an absent channel. A rule with only <c>x</c> is vertical across the whole height,
///         with only <c>y</c> horizontal across the whole width; with both, its stem runs from the value of the measure
///         axis to zero. A tick is a <c>thickness</c> (1) wide rectangle that is as long as its band (or <c>continuousBandSize</c>).
///     </para>
///     <para>
///         <b>Paint.</b> <c>filled</c> is on for every mark but <c>point</c> and <c>rule</c>; the color of the mark (<c>#4c78a8</c>,
///         <c>black</c> for a rule) is the fill of a filled mark and the stroke of an unfilled one (unfilled bars and symbols are filled
///         <c>transparent</c>). Explicit <c>fill</c>, <c>stroke</c>, <c>color</c>, <c>opacity</c>, <c>fillOpacity</c>, <c>strokeOpacity</c>,
///         <c>strokeWidth</c>, <c>strokeDash</c>, <c>cornerRadius</c>, <c>size</c>, <c>shape</c> and <c>angle</c> of the mark definition win over the
///         configuration (style, <c>config.&lt;mark&gt;</c>, <c>config.mark</c>, Vega's styles such as <c>point</c>: size 30, stroke
///         width 2). <c>point</c>, <c>circle</c>, <c>square</c> and <c>tick</c> get an opacity of 0.7 when nothing names an
///         opacity. A <c>color</c>, <c>fill</c> or <c>stroke</c> field is mapped through its ordinal scale (<c>tableau10</c> by default), a
///         <c>size</c> field through the size scale (the area of a symbol, the stroke width of a rule, the thickness of a bar or
///         tick) and an <c>opacity</c> field through the opacity scale; <c>value</c> definitions are used as given.
///     </para>
///     <para>
///         <b>Invalid data.</b> Vega-Lite's default <c>invalid</c> mode for marks other than lines and areas is <c>filter</c>: a row
///         whose value for a continuous channel (<c>x</c>, <c>y</c>, <c>size</c>, <c>opacity</c> on a linear scale) is null, missing, NaN or not a
///         number is not drawn. A null in a discrete field is the category <c>null</c> and is drawn; a value outside an explicit
///         discrete domain is not.
///     </para>
///     <para>
///         <b>Clipping.</b> Marks are clipped to the data rectangle only when the mark definition says <c>"clip": true</c>
///         (Vega-Lite also clips for interval-selection scales, which are not supported). Mark properties that are expressions,
///         <c>tooltip</c>, <c>href</c> and the individual corner radii are ignored; <c>x2</c>, <c>y2</c> and the offset channels are
///         reported by the feature catalog and ignored here. The class is stateless between calls and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class MarkBuilder
{
    /// <summary>The number of rows between two cancellation checks and budget updates.</summary>
    private const int BatchSize = 1024;

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

    /// <summary>
    ///     Initializes a mark builder.
    /// </summary>
    /// <param name="config">The effective configuration.</param>
    /// <param name="limits">The limits (<see cref="VegaLiteLimits.MaxSceneItems"/>); the defaults when null.</param>
    /// <param name="budget">The budget of scene items shared with the other builders of the chart; a new one from the limits when null.</param>
    /// <param name="sourceName">The source name for locations, or null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public MarkBuilder(ResolvedConfig config, VegaLiteLimits? limits = null, SceneBudget? budget = null, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _budget = budget ?? new SceneBudget((limits ?? VegaLiteLimits.Default).MaxSceneItems);
        _sourceName = sourceName;
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
        public Channel(SpecNode node, ResolvedScale? scale, ScaleModel? model, DataTable table)
        {
            Scale = scale;
            Model = model;
            FieldIndex = scale?.FieldPath is { } path ? table.FindField(path) : -1;
            Datum = node["datum"] switch
            {
                { IsNumber: true } n => DataValue.FromNumber(n.NumberValue),
                { IsString: true } s => DataValue.FromString(s.StringValue),
                { IsBoolean: true } b => DataValue.FromBoolean(b.BoolValue),
                _ => null,
            };
            Value = node["value"];
        }

        /// <summary>Gets the resolved scale, or null.</summary>
        public ResolvedScale? Scale { get; }

        /// <summary>Gets the scale model, or null.</summary>
        public ScaleModel? Model { get; }

        /// <summary>Gets the index of the field in the table, or -1.</summary>
        public int FieldIndex { get; }

        /// <summary>Gets the datum of the channel, or null.</summary>
        public DataValue? Datum { get; }

        /// <summary>Gets the constant <c>value</c> of the channel, or null.</summary>
        public SpecNode? Value { get; }

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
    private readonly record struct Paint(string? Fill, string? Stroke, double? StrokeWidth, double Opacity);

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
            foreach (var name in new[] { "x", "y", "color", "fill", "stroke", "size", "opacity" })
            {
                if (encoding?[name] is { IsObject: true } node)
                {
                    var scale = scales[name];
                    var extent = name == "x" ? width : height;
                    _channels[name] = new Channel(node, scale, scale?.CreateModel(name is "x" or "y" ? extent : 0), table);
                }
            }

            ResolvePaint();
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
        }

        /// <summary>
        ///     Gets the paint of one row.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="strokeWidth">The stroke width of the mark; null for the default of one pixel.</param>
        /// <param name="paint">Receives the paint.</param>
        /// <returns><see langword="false"/> when the row is invalid (its opacity value is not a number).</returns>
        private bool TryPaint(int row, double? strokeWidth, out Paint paint)
        {
            var fill = ColorOf(_fillChannel, row) ?? _fill;
            var stroke = ColorOf(_strokeChannel, row) ?? _stroke;
            var opacity = _opacity;
            if (Get("opacity") is { } channel)
            {
                if (Encoded(channel, row) is not { } encoded)
                {
                    paint = default;
                    return false;
                }

                opacity = encoded;
            }

            paint = new Paint(fill, stroke, strokeWidth, opacity);
            return true;
        }

        /// <summary>
        ///     Gets the color of a row from a color channel.
        /// </summary>
        /// <param name="channel">The channel, or null.</param>
        /// <param name="row">The row.</param>
        /// <returns>The color, or null to use the default.</returns>
        private string? ColorOf(Channel? channel, int row)
        {
            if (channel is null)
            {
                return null;
            }

            if (channel.Value is { IsString: true } value)
            {
                return value.StringValue;
            }

            return channel.Model is { Kind: ScaleKind.Ordinal } model ? model.MapOrdinal(channel.Read(_table, row)) : null;
        }

        /// <summary>
        ///     Gets a number of a row from a size or opacity channel: its constant value or its scaled field.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The number, or null when the row has no valid value.</returns>
        private double? Encoded(Channel channel, int row)
        {
            if (channel.Model is { } model)
            {
                return model.Kind == ScaleKind.Ordinal ? null : model.Map(channel.Read(_table, row));
            }

            return channel.Value?.AsNumber();
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
                return model.MapCenter(channel.Read(_table, row));
            }

            return ConstantPosition(channel, extent) ?? extent / 2;
        }

        /// <summary>
        ///     Reads the constant position of a <c>value</c> channel: a number, or the keywords <c>width</c> and <c>height</c>.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="extent">The size of the data rectangle along the axis.</param>
        /// <returns>The position, or null when the channel has no usable value.</returns>
        private static double? ConstantPosition(Channel channel, double extent) => channel.Value switch
        {
            { IsNumber: true } n => n.NumberValue,
            { IsString: true } s when s.StringValue is "width" or "height" => extent,
            _ => null,
        };

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
                return model.MapCenter(channel.Read(_table, row)) is { } main ? (main, ZeroOrMin(model)) : null;
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
            if (encoded?.Model is null && encoded?.Value?.AsNumber() is { } constant)
            {
                fixedSize = constant;
                encoded = null;
            }

            return new Sizing(fixedSize, band, encoded?.Model is null ? null : encoded);
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
                var value = Encoded(channel, row);
                valid = value is not null;
                return value;
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

            if (barOrTick)
            {
                var thickness = Thickness(sizing, row, out var valid);
                if (!valid)
                {
                    return null;
                }

                if (model is { Kind: ScaleKind.Linear })
                {
                    var center = model.MapCenter(channel!.Read(_table, row));
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

            var vertical = _orient != "horizontal";
            var widthDefault = Def("strokeWidth")?.AsNumber() ?? Def("size")?.AsNumber() ?? Prop("strokeWidth", vega: true)?.AsNumber();
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            for (var row = 0; row < _table.RowCount; row++)
            {
                Batch(row);
                var width = widthDefault;
                if (Get("size") is { } sizeChannel)
                {
                    width = Encoded(sizeChannel, row);
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
                if (vertical)
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
            var angle = Prop("angle")?.AsNumber() ?? 0;
            var defaultSize = Prop("size", vega: true)?.AsNumber() ?? 30;
            var strokeWidth = Prop("strokeWidth", vega: true)?.AsNumber();
            var sizeChannel = Get("size");
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            for (var row = 0; row < _table.RowCount; row++)
            {
                Batch(row);
                var size = sizeChannel is null ? defaultSize : Encoded(sizeChannel, row);
                if (size is not { } area || PointPosition(x, _width, row) is not { } px || PointPosition(y, _height, row) is not { } py || !TryPaint(row, strokeWidth, out var paint))
                {
                    continue;
                }

                Reserve();
                items.Add(Style(new SceneSymbol("mark-" + _type, px, py) { Shape = shape, Size = area, Angle = angle }, paint));
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
            FillOpacity = Prop("fillOpacity")?.AsNumber() ?? 1,
            StrokeOpacity = Prop("strokeOpacity")?.AsNumber() ?? 1,
            StrokeDash = Dash(),
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
            FillOpacity = Prop("fillOpacity")?.AsNumber() ?? 1,
            StrokeOpacity = Prop("strokeOpacity")?.AsNumber() ?? 1,
            StrokeDash = Dash(),
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
            StrokeOpacity = Prop("strokeOpacity")?.AsNumber() ?? 1,
            StrokeDash = Dash(),
            StrokeDashOffset = Prop("strokeDashOffset")?.AsNumber() ?? 0,
            StrokeCap = Prop("strokeCap")?.AsString(),
        };

        /// <summary>
        ///     Reads the dash pattern of the mark.
        /// </summary>
        /// <returns>The pattern, or null for a solid line.</returns>
        private IReadOnlyList<double>? Dash() =>
            Prop("strokeDash") is { Kind: ConfigNodeKind.Array } node ? node.Items.Select(i => i.AsNumber()).Where(n => n is not null).Select(n => n!.Value).ToList() : null;
    }
}

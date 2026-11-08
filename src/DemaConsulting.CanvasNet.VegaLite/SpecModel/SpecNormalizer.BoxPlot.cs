// Portions derived from Vega-Lite (BSD-3-Clause): compositemark/boxplot.ts (normalizeBoxPlot, boxParams); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     The expansion of the <c>boxplot</c> composite mark.
/// </content>
/// <remarks>
///     <para>
///         The expansion is Vega-Lite's <c>normalizeBoxPlot</c> without the tooltips. The default <c>extent</c> is <c>1.5</c>: a Tukey box plot whose whiskers reach the
///         smallest and the largest value that lie within that multiple of the interquartile range from the box, and whose values beyond are drawn as outlier points. The
///         layers are, in this order: for the Tukey plot, a view of the outlier points and a view of the whiskers (the rows are first given their quartiles by a
///         <c>joinaggregate</c>, the whisker view keeps the rows inside the fences and aggregates them), then a view of the box (a <c>bar</c> from the first to the third quartile
///         and a <c>tick</c> at the median, white by default); for <c>min-max</c>, a single view whose whiskers reach the minimum and the maximum.
///     </para>
///     <para>
///         The parts are the <c>box</c>, <c>median</c>, <c>rule</c> (the whisker), <c>ticks</c> (the end of the whisker, off by default) and <c>outliers</c>; each can be switched off, or
///         given mark properties, in the mark definition or in <c>config.boxplot</c>.
///     </para>
/// </remarks>
internal static partial class SpecNormalizer
{
    /// <summary>The color of the box when the mark gives none, which the median takes when the box has no height (Vega-Lite's <c>defaultBoxColor</c>).</summary>
    private const string DefaultBoxColor = "#4c78a8";

    /// <summary>
    ///     Expands a <c>boxplot</c> unit view into the views of its layers.
    /// </summary>
    /// <param name="b">The builder of the composite mark.</param>
    /// <param name="encoding">The merged encoding of the unit view.</param>
    /// <returns>The views of the layers, in drawing order; empty when every part is switched off.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, at the mark, when the orientation cannot be decided or the measure is not a field.</exception>
    private static IReadOnlyList<SpecNode> BoxPlot(CompositeBuilder b, SpecNode? encoding)
    {
        var orient = b.Orient(encoding);
        var axis = b.ContinuousAxis(encoding, orient);
        var grouping = b.Grouping(encoding!, axis);
        var plot = new BoxPlotLayers(b, axis, orient, grouping);
        return plot.Create();
    }

    /// <summary>
    ///     Builds the layers of one box plot.
    /// </summary>
    private sealed class BoxPlotLayers
    {
        /// <summary>The builder of the composite mark.</summary>
        private readonly CompositeBuilder _b;

        /// <summary>The continuous axis.</summary>
        private readonly CompositeAxis _axis;

        /// <summary>The orientation of the box: <c>vertical</c> or <c>horizontal</c>.</summary>
        private readonly string _orient;

        /// <summary>The orientation of the ticks, across the box.</summary>
        private readonly string _ticksOrient;

        /// <summary>The channels the parts share, with the size and color.</summary>
        private readonly List<SpecNode> _shared;

        /// <summary>The channels the whiskers and outliers share: all but the size and color.</summary>
        private readonly List<SpecNode> _withoutSizeAndColor;

        /// <summary>The fields that group the rows.</summary>
        private readonly List<string> _groupBy;

        /// <summary>The transforms of the grouping channels (the bins, then the time units), which run before the aggregates.</summary>
        private readonly List<SpecNode> _timeUnits;

        /// <summary>The measures that the aggregating channels add to the aggregates.</summary>
        private readonly List<SpecNode> _measures;

        /// <summary>The <c>invalid</c> of the mark definition, or null.</summary>
        private readonly SpecNode? _invalid;

        /// <summary>
        ///     Initializes the layers of a box plot.
        /// </summary>
        /// <param name="b">The builder of the composite mark.</param>
        /// <param name="axis">The continuous axis.</param>
        /// <param name="orient">The orientation.</param>
        /// <param name="grouping">The channels that group the rows, and what their definitions add.</param>
        public BoxPlotLayers(CompositeBuilder b, CompositeAxis axis, string orient, CompositeGrouping grouping)
        {
            _b = b;
            _axis = axis;
            _orient = orient;
            _ticksOrient = orient == "vertical" ? "horizontal" : "vertical";
            _shared = grouping.Channels;
            _withoutSizeAndColor = [.. grouping.Channels.Where(c => c.Name is not ("size" or "color"))];
            _groupBy = grouping.GroupBy;
            _timeUnits = grouping.Transforms;
            _measures = grouping.Measures;
            _invalid = b.Definition?["invalid"];
        }

        /// <summary>
        ///     Builds the views of the layers.
        /// </summary>
        /// <returns>The views in drawing order.</returns>
        public IReadOnlyList<SpecNode> Create()
        {
            var extent = _b.Property("extent");
            if (extent is { IsString: true })
            {
                // min-max: one view whose aggregate gives the whiskers directly
                var aggregate = Aggregate([.. Quartiles("lower_box", "upper_box", "mid_box"), Op("min", _axis.Field, Name("lower_whisker")), Op("max", _axis.Field, Name("upper_whisker"))]);
                return View([.. _timeUnits, aggregate], [.. WhiskerLayers(), .. BoxLayers()]) is { } view ? [view] : [];
            }

            var k = extent?.AsNumber() ?? 1.5;
            var lowerBox = Access(Name("lower_box"));
            var upperBox = Access(Name("upper_box"));
            var iqr = $"({upperBox} - {lowerBox})";
            var lowerWhisker = $"{lowerBox} - {JsNumber.ToString(k)} * {iqr}";
            var upperWhisker = $"{upperBox} + {JsNumber.ToString(k)} * {iqr}";
            var value = Access(_axis.Field);

            // The rows get their quartiles, then the outliers are the rows beyond the fences and the whiskers reach the extreme rows inside them
            var joinAggregate = _b.Obj(null, _b.Arr("joinaggregate", [Op("q1", _axis.Field, Name("lower_box")), Op("q3", _axis.Field, Name("upper_box"))]), GroupByNode());
            var filter = _b.Obj(null, _b.Expr("filter", $"({lowerWhisker} <= {value}) && ({value} <= {upperWhisker})"));
            var whiskerAggregate = Aggregate(
                Op("min", _axis.Field, Name("lower_whisker")),
                Op("max", _axis.Field, Name("upper_whisker")),
                Op("min", Name("lower_box"), Name("lower_box")),
                Op("max", Name("upper_box"), Name("upper_box")));
            var upper = new List<SpecNode>(2);
            if (Outliers($"({value} < {lowerWhisker}) || ({value} > {upperWhisker})") is { } outliers)
            {
                upper.Add(outliers);
            }

            if (View([filter, whiskerAggregate], WhiskerLayers()) is { } whiskers)
            {
                upper.Add(whiskers);
            }

            var views = new List<SpecNode>(2);
            if (View([.. _timeUnits, joinAggregate], upper) is { } first)
            {
                views.Add(first);
            }

            if (View([.. _timeUnits, Aggregate(Quartiles("lower_box", "upper_box", "mid_box"))], BoxLayers()) is { } box)
            {
                views.Add(box);
            }

            return views;
        }

        /// <summary>
        ///     Names a computed field after the measure.
        /// </summary>
        /// <param name="prefix">The prefix, such as <c>lower_box</c>.</param>
        /// <returns>The name of the field in the aggregated rows.</returns>
        private string Name(string prefix) => $"{prefix}_{_axis.Alias}";

        /// <summary>
        ///     Names a computed field the way the encoding refers to it: after the field as written.
        /// </summary>
        /// <param name="prefix">The prefix, such as <c>lower_box</c>.</param>
        /// <returns>The field of the channel definition.</returns>
        private string Ref(string prefix) => $"{prefix}_{_axis.Field}";

        /// <summary>
        ///     Writes the access of a field of the row in an expression.
        /// </summary>
        /// <param name="field">The field name.</param>
        /// <returns>The expression text.</returns>
        private static string Access(string field) => $"datum['{field.Replace("'", "\\'", StringComparison.Ordinal)}']";

        /// <summary>
        ///     Writes an aggregate measure: <c>{op, field, as}</c>.
        /// </summary>
        /// <param name="op">The operation.</param>
        /// <param name="field">The field.</param>
        /// <param name="output">The name of the result.</param>
        /// <returns>The measure object.</returns>
        private SpecNode Op(string op, string field, string output) => _b.Obj(null, _b.Str("op", op), _b.Str("field", field), _b.Str("as", output));

        /// <summary>
        ///     Writes the three quartile measures of the box.
        /// </summary>
        /// <param name="lower">The prefix of the first quartile.</param>
        /// <param name="upper">The prefix of the third quartile.</param>
        /// <param name="middle">The prefix of the median.</param>
        /// <returns>The measures.</returns>
        private SpecNode[] Quartiles(string lower, string upper, string middle) =>
            [Op("q1", _axis.Field, Name(lower)), Op("q3", _axis.Field, Name(upper)), Op("median", _axis.Field, Name(middle))];

        /// <summary>
        ///     Writes the <c>groupby</c> array.
        /// </summary>
        /// <returns>The array.</returns>
        private SpecNode GroupByNode() => _b.Arr("groupby", _groupBy.Select(g => _b.Str(null, g)));

        /// <summary>
        ///     Writes an <c>aggregate</c> transform that groups by the grouping fields.
        /// </summary>
        /// <param name="measures">The measures.</param>
        /// <returns>The transform entry.</returns>
        private SpecNode Aggregate(params SpecNode[] measures) => _b.Obj(null, _b.Arr("aggregate", [.. measures, .. _measures]), GroupByNode());

        /// <summary>
        ///     Writes a view of layers with transforms of its own.
        /// </summary>
        /// <param name="transforms">The transform entries.</param>
        /// <param name="layers">The views of the layers.</param>
        /// <returns>The view; null when it has no layer.</returns>
        private SpecNode? View(IReadOnlyList<SpecNode> transforms, IEnumerable<SpecNode> layers)
        {
            var list = layers.ToList();
            if (list.Count == 0)
            {
                return null;
            }

            var transform = transforms.Count > 0 ? _b.Arr("transform", transforms) : null;
            return _b.Obj(null, transform, _b.Arr("layer", list));
        }

        /// <summary>
        ///     Writes the view of one part that draws a computed field (and, for a range, a second one).
        /// </summary>
        /// <param name="part">The part name.</param>
        /// <param name="markProperties">The properties of the part's own mark.</param>
        /// <param name="prefix">The prefix of the field of the continuous channel.</param>
        /// <param name="endPrefix">The prefix of the field of the secondary channel, or null for a single position.</param>
        /// <param name="channels">The other channels of the part.</param>
        /// <returns>The view; null when the part is off.</returns>
        private SpecNode? Part(string part, SpecNode?[] markProperties, string prefix, string? endPrefix, IEnumerable<SpecNode> channels)
        {
            if (!_b.HasPart(part))
            {
                return null;
            }

            var properties = new List<SpecNode?> { _b.PositionDefinition(_axis, _axis.Channel, Ref(prefix)) };
            if (endPrefix is not null)
            {
                properties.Add(_b.PositionDefinition(_axis, _axis.Secondary, Ref(endPrefix)));
            }

            properties.AddRange(channels);
            return _b.Obj(null, _b.PartMark(part, markProperties), _b.ObjOf("encoding", properties));
        }

        /// <summary>
        ///     Writes the views of the whiskers: the two rules and, when switched on, the two end ticks.
        /// </summary>
        /// <returns>The views that are on.</returns>
        private List<SpecNode> WhiskerLayers()
        {
            return
            [
                .. new[]
                {
                    Part("rule", RuleMark(), "lower_whisker", "lower_box", _withoutSizeAndColor),
                    Part("rule", RuleMark(), "upper_box", "upper_whisker", _withoutSizeAndColor),
                    Part("ticks", EndTickMark(), "lower_whisker", null, _withoutSizeAndColor),
                    Part("ticks", EndTickMark(), "upper_whisker", null, _withoutSizeAndColor),
                }.OfType<SpecNode>(),
            ];
        }

        /// <summary>
        ///     Writes the properties of the mark of a whisker.
        /// </summary>
        /// <returns>The properties.</returns>
        private SpecNode?[] RuleMark() => [_b.Str("type", "rule"), _invalid];

        /// <summary>
        ///     Writes the properties of the mark of the tick at the end of a whisker: black, opaque, across the box.
        /// </summary>
        /// <returns>The properties.</returns>
        private SpecNode?[] EndTickMark() => [_b.Str("type", "tick"), _b.Str("color", "black"), _b.Num("opacity", 1), _b.Str("orient", _ticksOrient), _invalid];

        /// <summary>
        ///     Writes the views of the box and the median.
        /// </summary>
        /// <returns>The views that are on.</returns>
        private List<SpecNode> BoxLayers()
        {
            var size = SizeNode();
            var views = new List<SpecNode>(2);
            var box = Part(
                "box",
                [_b.Str("type", "bar"), size, _b.Str("orient", _orient), _invalid],
                "lower_box",
                "upper_box",
                _shared);
            if (box is not null)
            {
                views.Add(box);
            }

            var medianColor = _b.Config.Get("boxplot.median.color")?.AsString();
            var channels = new List<SpecNode>(_withoutSizeAndColor);
            if (_shared.FirstOrDefault(c => c.Name == "size") is { } sizeChannel)
            {
                channels.Add(sizeChannel);
            }

            channels.Add(MedianColor());
            var median = Part(
                "median",
                [_b.Str("type", "tick"), _invalid, medianColor is null ? null : _b.Str("color", medianColor), size, _b.Str("orient", _ticksOrient)],
                "mid_box",
                null,
                channels);
            if (median is not null)
            {
                views.Add(median);
            }

            return views;
        }

        /// <summary>
        ///     Writes the <c>color</c> of the median tick: the box color where the box has no height (the median would be invisible on it), otherwise the color of the median.
        /// </summary>
        /// <returns>The channel definition.</returns>
        private SpecNode MedianColor()
        {
            var test = _b.Expr("test", $"{Access(Name("lower_box"))} >= {Access(Name("upper_box"))}");
            var fallback = _shared.FirstOrDefault(c => c.Name == "color") is { IsObject: true } color
                ? color.Properties.Where(p => p.Name != "condition")
                : [_b.Str("value", BoxColor())];
            return _b.Obj("color", _b.ObjOf("condition", [test, .. fallback]));
        }

        /// <summary>
        ///     Gets the color the box has when the mark gives none.
        /// </summary>
        /// <returns>The color of <c>config.boxplot.box</c> when it is an object, otherwise of <c>config.mark</c>, otherwise the default.</returns>
        private string BoxColor()
        {
            var box = _b.Config.Get("boxplot.box");
            var color = box is { IsObject: true } ? box["color"]?.AsString() : _b.Config.Get("mark.color")?.AsString();
            return string.IsNullOrEmpty(color) ? DefaultBoxColor : color;
        }

        /// <summary>
        ///     Gets the size of the box and the median: the mark's <c>size</c>, else the configured one.
        /// </summary>
        /// <returns>The <c>size</c> property; null when there is none or it is zero.</returns>
        private SpecNode? SizeNode()
        {
            var size = _b.Property("size") ?? (_b.Config.Get("mark.size") is { } fallback ? _b.FromConfig("size", fallback) : null);
            return size is { IsNumber: true, NumberValue: 0 } ? null : size;
        }

        /// <summary>
        ///     Writes the view of the outlier points.
        /// </summary>
        /// <param name="test">The filter that keeps the rows beyond the fences.</param>
        /// <returns>The view; null when the outliers are off.</returns>
        private SpecNode? Outliers(string test)
        {
            if (!_b.HasPart("outliers"))
            {
                return null;
            }

            var properties = new List<SpecNode?> { _b.PositionDefinition(_axis, _axis.Channel, _axis.Field) };
            properties.AddRange(_withoutSizeAndColor);
            properties.Add(_shared.FirstOrDefault(c => c.Name == "color"));
            var filter = _b.Obj(null, _b.Expr("filter", test));
            return _b.Obj(null, _b.Arr("transform", [filter]), _b.PartMark("outliers", _b.Str("type", "point")), _b.ObjOf("encoding", properties));
        }
    }
}

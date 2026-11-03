// Portions derived from Vega-Lite (BSD-3-Clause): compositemark/errorbar.ts (normalizeErrorBar, errorBarParams), compositemark/errorband.ts (normalizeErrorBand) and compositemark/common.ts; see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     What an <c>errorbar</c> or <c>errorband</c> is given: the raw observations, or the ends of the range (or the errors) already computed.
/// </summary>
internal enum ErrorInput
{
    /// <summary>The raw observations: the rows are aggregated into a center and an extent per group.</summary>
    Raw,

    /// <summary>The ends of the range are fields: the measure and its <c>x2</c> or <c>y2</c>.</summary>
    UpperLower,

    /// <summary>The range is the measure plus and minus an error field: <c>xError</c> and <c>xError2</c>, or <c>yError</c> and <c>yError2</c>.</summary>
    Error,
}

/// <content>
///     The expansion of the <c>errorbar</c> and <c>errorband</c> composite marks.
/// </content>
/// <remarks>
///     <para>
///         The expansion is Vega-Lite's <c>normalizeErrorBar</c> and <c>normalizeErrorBand</c> without the tooltips. Given observations (<see cref="ErrorInput.Raw"/>), the rows are aggregated
///         per group into a center and an extent: <c>stderr</c> (the default; the mean plus and minus the standard error), <c>stdev</c> (the mean plus and minus the standard deviation),
///         <c>ci</c> (the 95% confidence interval of the mean) or <c>iqr</c> (the interquartile range around the median). Given the ends of the range, or a value plus its errors,
///         <c>calculate</c> transforms compute <c>lower_&lt;field&gt;</c> and <c>upper_&lt;field&gt;</c> from the fields of the rows, which are not aggregated.
///     </para>
///     <para>
///         The errorbar draws, in this order, the ticks at the lower and the upper end (off by default) and the rule between them; the errorband draws the band
///         (a <c>rect</c>, or an <c>area</c> when both <c>x</c> and <c>y</c> are given) and, when switched on, the borders at the lower and the upper end (a <c>rule</c>, or a <c>line</c>).
///         The ticks and borders can be switched on and the parts given mark properties in the mark definition or in <c>config.errorbar</c> and <c>config.errorband</c>.
///     </para>
/// </remarks>
internal static partial class SpecNormalizer
{
    /// <summary>
    ///     Decides the orientation of a composite mark without failing: a boxplot by its channels, an errorbar or errorband also by the channels that give the range.
    /// </summary>
    /// <param name="type">The composite mark type.</param>
    /// <param name="definition">The mark definition object, or null.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <param name="orient">Receives <c>vertical</c> or <c>horizontal</c>.</param>
    /// <param name="problem">Receives the reason when the orientation cannot be decided.</param>
    /// <returns><see langword="true"/> when the orientation was decided.</returns>
    internal static bool TryOrientation(string type, SpecNode? definition, SpecNode? encoding, out string orient, out string? problem) =>
        type == "boxplot"
            ? CompositeBuilder.TryOrient(type, definition, encoding, out orient, out problem)
            : TryErrorOrient(type, definition, encoding, out orient, out _, out problem);

    /// <summary>
    ///     Decides the orientation and the kind of input of an errorbar or errorband (Vega-Lite's <c>errorBarOrientAndInputType</c>).
    /// </summary>
    /// <param name="type">The composite mark type.</param>
    /// <param name="definition">The mark definition object, or null.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <param name="orient">Receives <c>vertical</c> (the measure is on <c>y</c>) or <c>horizontal</c>.</param>
    /// <param name="input">Receives the kind of input.</param>
    /// <param name="problem">Receives the reason when the encoding is not one the mark can draw.</param>
    /// <returns><see langword="true"/> when the encoding is valid.</returns>
    private static bool TryErrorOrient(string type, SpecNode? definition, SpecNode? encoding, out string orient, out ErrorInput input, out string? problem)
    {
        orient = "vertical";
        input = ErrorInput.Raw;
        problem = null;
        var x = encoding?["x"];
        var y = encoding?["y"];
        var (hasX2, hasY2) = (IsFieldOrDatum(encoding?["x2"]), IsFieldOrDatum(encoding?["y2"]));
        var (hasXError, hasXError2) = (IsFieldOrDatum(encoding?["xError"]), IsFieldOrDatum(encoding?["xError2"]));
        var (hasYError, hasYError2) = (IsFieldOrDatum(encoding?["yError"]), IsFieldOrDatum(encoding?["yError2"]));
        var hasRange = hasX2 || hasY2;
        var hasError = hasXError || hasXError2 || hasYError || hasYError2;
        if (!hasRange && !hasError && (IsFieldOrDatum(x) || IsFieldOrDatum(y)))
        {
            return CompositeBuilder.TryOrient(type, definition, encoding, out orient, out problem);
        }

        if (hasRange)
        {
            input = ErrorInput.UpperLower;
            if (hasError)
            {
                problem = $"An {type} cannot have both a range (x2, y2) and errors (xError, yError).";
            }
            else if (hasX2 && hasY2)
            {
                problem = $"An {type} cannot have both x2 and y2.";
            }
            else
            {
                orient = hasX2 ? "horizontal" : "vertical";
                var (axis, definitionOfAxis) = hasX2 ? ("x", x) : ("y", y);
                problem = CompositeBuilder.IsContinuous(definitionOfAxis, axis) ? null : $"Both {axis} and {axis}2 have to be quantitative in an {type}.";
            }

            return problem is null;
        }

        if (hasError)
        {
            input = ErrorInput.Error;
            if ((hasXError2 && !hasXError) || (hasYError2 && !hasYError))
            {
                problem = $"An {type} cannot have {(hasXError2 && !hasXError ? "xError2 without xError" : "yError2 without yError")}.";
            }
            else if (hasXError && hasYError)
            {
                problem = $"An {type} cannot have both xError and yError.";
            }
            else
            {
                orient = hasXError ? "horizontal" : "vertical";
                var (axis, definitionOfAxis) = hasXError ? ("x", x) : ("y", y);
                problem = CompositeBuilder.IsContinuous(definitionOfAxis, axis) ? null : $"All {axis}, {axis}Error and {axis}Error2 (if present) have to be quantitative in an {type}.";
            }

            return problem is null;
        }

        problem = $"An {type} needs a quantitative or temporal field on the x or y channel, or a range (x2, y2) or errors (xError, yError) next to it.";
        return false;
    }

    /// <summary>
    ///     Tells whether a channel definition is a field or a datum (Vega-Lite's <c>isFieldOrDatumDef</c>; a value is not).
    /// </summary>
    /// <param name="definition">The channel definition, or null.</param>
    /// <returns><see langword="true"/> for a field or datum definition.</returns>
    private static bool IsFieldOrDatum(SpecNode? definition) =>
        definition is { IsObject: true } && (definition["field"] is { IsNull: false } || definition["datum"] is { IsNull: false });

    /// <summary>
    ///     Expands an <c>errorbar</c> or <c>errorband</c> unit view into the views of its layers.
    /// </summary>
    /// <param name="b">The builder of the composite mark.</param>
    /// <param name="encoding">The merged encoding of the unit view.</param>
    /// <returns>One view with the transforms and the layers of the parts; empty when every part is switched off.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown, at the mark, when the encoding is not one the mark can draw or a channel of the range is not a field.</exception>
    private static IReadOnlyList<SpecNode> ErrorMark(CompositeBuilder b, SpecNode? encoding)
    {
        if (!TryErrorOrient(b.Type, b.Definition, encoding, out var orient, out var input, out var problem))
        {
            throw b.Error(problem!);
        }

        var axis = b.ContinuousAxis(encoding, orient);
        var (shared, groupBy) = b.Grouping(encoding!, axis);
        if (b.Type == "errorbar")
        {
            // Vega-Lite drops the size channel from the parts of an errorbar (its ticks and rule have the size of the mark definition)
            shared.RemoveAll(c => c.Name == "size");
        }

        var is2D = encoding!["x"] is { IsNull: false } && encoding["y"] is { IsNull: false };
        return new ErrorLayers(b, encoding, axis, orient, input, shared, groupBy, is2D).Create();
    }

    /// <summary>
    ///     Builds the layers of one errorbar or errorband.
    /// </summary>
    private sealed class ErrorLayers
    {
        /// <summary>The builder of the composite mark.</summary>
        private readonly CompositeBuilder _b;

        /// <summary>The merged encoding.</summary>
        private readonly SpecNode _encoding;

        /// <summary>The continuous axis.</summary>
        private readonly CompositeAxis _axis;

        /// <summary>The orientation: <c>vertical</c> or <c>horizontal</c>.</summary>
        private readonly string _orient;

        /// <summary>The kind of input.</summary>
        private readonly ErrorInput _input;

        /// <summary>The channels the parts share.</summary>
        private readonly List<SpecNode> _shared;

        /// <summary>The fields that group the rows.</summary>
        private readonly List<string> _groupBy;

        /// <summary>Whether both <c>x</c> and <c>y</c> are given (an errorband then draws an area and lines).</summary>
        private readonly bool _is2D;

        /// <summary>
        ///     Initializes the layers of an errorbar or errorband.
        /// </summary>
        /// <param name="b">The builder of the composite mark.</param>
        /// <param name="encoding">The merged encoding.</param>
        /// <param name="axis">The continuous axis.</param>
        /// <param name="orient">The orientation.</param>
        /// <param name="input">The kind of input.</param>
        /// <param name="shared">The channels the parts share.</param>
        /// <param name="groupBy">The fields that group the rows.</param>
        /// <param name="is2D">Whether both <c>x</c> and <c>y</c> are given.</param>
        public ErrorLayers(CompositeBuilder b, SpecNode? encoding, CompositeAxis axis, string orient, ErrorInput input, List<SpecNode> shared, List<string> groupBy, bool is2D)
        {
            _b = b;
            _encoding = encoding!;
            _axis = axis;
            _orient = orient;
            _input = input;
            _shared = shared;
            _groupBy = groupBy;
            _is2D = is2D;
        }

        /// <summary>
        ///     Builds the view of the parts.
        /// </summary>
        /// <returns>The view; none when every part is switched off.</returns>
        public IReadOnlyList<SpecNode> Create()
        {
            var layers = _b.Type == "errorbar" ? BarLayers() : BandLayers();
            if (layers.Count == 0)
            {
                return [];
            }

            return [_b.Obj(null, _b.Arr("transform", Transforms()), _b.Arr("layer", layers))];
        }

        /// <summary>
        ///     Names a computed field after the measure.
        /// </summary>
        /// <param name="prefix">The prefix, such as <c>lower</c>.</param>
        /// <returns>The name of the field in the rows.</returns>
        private string Name(string prefix) => $"{prefix}_{_axis.Alias}";

        /// <summary>
        ///     Names a computed field the way the encoding refers to it: after the field as written.
        /// </summary>
        /// <param name="prefix">The prefix, such as <c>lower</c>.</param>
        /// <returns>The field of the channel definition.</returns>
        private string Ref(string prefix) => $"{prefix}_{_axis.Field}";

        /// <summary>
        ///     Writes an aggregate measure: <c>{op, field, as}</c>.
        /// </summary>
        /// <param name="op">The operation.</param>
        /// <param name="output">The name of the result.</param>
        /// <returns>The measure object.</returns>
        private SpecNode Op(string op, string output) => _b.Obj(null, _b.Str("op", op), _b.Str("field", _axis.Field), _b.Str("as", output));

        /// <summary>
        ///     Writes a <c>calculate</c> transform.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="output">The name of the result.</param>
        /// <returns>The transform entry.</returns>
        private SpecNode Calculate(string expression, string output) => _b.Obj(null, _b.Expr("calculate", expression), _b.Str("as", output));

        /// <summary>
        ///     Reads the field of a channel that gives the range or the errors.
        /// </summary>
        /// <param name="channel">The channel, such as <c>x2</c> or <c>yError</c>.</param>
        /// <returns>The expression that reads the field of a row.</returns>
        /// <exception cref="VegaLiteSpecException">Thrown, at the mark, when the channel is not a field.</exception>
        private string Read(string channel) =>
            _encoding[channel]?["field"] is { IsString: true } field
                ? CompositeBuilder.Access(CompositeBuilder.AliasOf(field.StringValue!))
                : throw _b.Error($"The '{channel}' channel of an {_b.Type} must be a field.");

        /// <summary>
        ///     Writes the transforms that give every group its <c>lower_</c> and <c>upper_</c> fields.
        /// </summary>
        /// <returns>The transform entries.</returns>
        private List<SpecNode> Transforms()
        {
            var transforms = new List<SpecNode>(3);
            var own = Access(_axis.Alias);
            switch (_input)
            {
                case ErrorInput.UpperLower:
                    transforms.Add(Calculate(Read(_axis.Secondary), Name("upper")));
                    transforms.Add(Calculate(own, Name("lower")));
                    break;
                case ErrorInput.Error:
                    var error = _axis.Channel + "Error";
                    var error2 = error + "2";
                    transforms.Add(Calculate($"{own} + {Read(error)}", Name("upper")));
                    transforms.Add(Calculate(_encoding[error2] is null ? $"{own} - {Read(error)}" : $"{own} + {Read(error2)}", Name("lower")));
                    break;
                default:
                    AddAggregate(transforms);
                    break;
            }

            return transforms;
        }

        /// <summary>
        ///     Writes the transforms of the raw observations: the aggregate per group and, for a center with a deviation, the ends computed from it.
        /// </summary>
        /// <param name="transforms">Receives the transform entries.</param>
        private void AddAggregate(List<SpecNode> transforms)
        {
            var markCenter = _b.Definition?["center"]?.AsString();
            var markExtent = _b.Definition?["extent"]?.AsString();
            var center = markCenter ?? DefaultCenter(markExtent);
            var extent = markExtent ?? (center == "mean" ? "stderr" : "iqr");
            SpecNode[] measures;
            if (extent is "stderr" or "stdev")
            {
                measures = [Op(extent, Name("extent")), Op(center, Name("center"))];
            }
            else
            {
                var (centerOp, lowerOp, upperOp) = extent == "ci" ? ("mean", "ci0", "ci1") : ("median", "q1", "q3");
                measures = [Op(lowerOp, Name("lower")), Op(upperOp, Name("upper")), Op(centerOp, Name("center"))];
            }

            transforms.Add(_b.Obj(null, _b.Arr("aggregate", measures), _b.Arr("groupby", _groupBy.Select(g => _b.Str(null, g)))));
            if (extent is "stderr" or "stdev")
            {
                transforms.Add(Calculate($"{Access(Name("center"))} + {Access(Name("extent"))}", Name("upper")));
                transforms.Add(Calculate($"{Access(Name("center"))} - {Access(Name("extent"))}", Name("lower")));
            }
        }

        /// <summary>
        ///     Gets the center when the mark definition gives none: the median for an <c>iqr</c> extent, the mean for any other, otherwise the one of <c>config.errorbar</c>
        ///     (which Vega-Lite reads for an errorband too).
        /// </summary>
        /// <param name="extent">The extent of the mark definition, or null.</param>
        /// <returns><c>mean</c> or <c>median</c>.</returns>
        private string DefaultCenter(string? extent)
        {
            if (extent is not null)
            {
                return extent == "iqr" ? "median" : "mean";
            }

            return _b.Config.Get("errorbar.center")?.AsString() ?? "mean";
        }

        /// <summary>
        ///     Writes the access of a field of the row in an expression.
        /// </summary>
        /// <param name="field">The field name.</param>
        /// <returns>The expression text.</returns>
        private static string Access(string field) => CompositeBuilder.Access(field);

        /// <summary>
        ///     Writes the view of one part that draws a computed field (and, for a range, a second one).
        /// </summary>
        /// <param name="part">The part name.</param>
        /// <param name="markProperties">The properties of the part's own mark.</param>
        /// <param name="prefix">The prefix of the field of the continuous channel.</param>
        /// <param name="endPrefix">The prefix of the field of the secondary channel, or null for a single position.</param>
        /// <returns>The view; null when the part is off.</returns>
        private SpecNode? Part(string part, SpecNode?[] markProperties, string prefix, string? endPrefix)
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

            properties.AddRange(_shared);
            return _b.Obj(null, _b.PartMark(part, markProperties), _b.ObjOf("encoding", properties));
        }

        /// <summary>
        ///     Writes the views of an errorbar: the ticks at both ends and the rule between them.
        /// </summary>
        /// <returns>The views that are on.</returns>
        private List<SpecNode> BarLayers()
        {
            var thickness = _b.Definition?["thickness"];
            var size = _b.Definition?["size"];
            SpecNode?[] tick = [_b.Str("type", "tick"), _b.Str("orient", _orient == "vertical" ? "horizontal" : "vertical"), thickness, size];
            SpecNode?[] rule = [_b.Str("type", "rule"), thickness is { IsNumber: true } ? _b.Num("size", thickness.NumberValue) : null];
            return [.. new[] { Part("ticks", tick, "lower", null), Part("ticks", tick, "upper", null), Part("rule", rule, "lower", "upper") }.OfType<SpecNode>()];
        }

        /// <summary>
        ///     Writes the views of an errorband: the band between the ends and the borders at each end.
        /// </summary>
        /// <returns>The views that are on.</returns>
        private List<SpecNode> BandLayers()
        {
            // Only an area and its lines are interpolated; the tension needs an interpolation
            var interpolate = _is2D ? _b.Definition?["interpolate"] : null;
            var tension = interpolate is not null && _b.Definition?["tension"] is { IsNumber: true, NumberValue: not 0 } t ? t : null;
            SpecNode?[] band = [_b.Str("type", _is2D ? "area" : "rect"), interpolate, tension];
            SpecNode?[] borders = [_b.Str("type", _is2D ? "line" : "rule"), interpolate, tension];
            return [.. new[] { Part("band", band, "lower", "upper"), Part("borders", borders, "lower", null), Part("borders", borders, "upper", null) }.OfType<SpecNode>()];
        }
    }
}

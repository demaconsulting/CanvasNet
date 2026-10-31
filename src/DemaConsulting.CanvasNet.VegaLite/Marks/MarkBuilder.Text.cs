// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The text mark of <see cref="MarkBuilder"/>: one <see cref="SceneText"/> per row, its position, its polar offset, its font and its lines.
/// </content>
internal sealed partial class MarkBuilder
{
    /// <summary>The most characters all the labels of one text mark may have together, as a multiple of <see cref="VegaLiteLimits.MaxStringLength"/> (a label itself is bounded by <see cref="VegaLiteLimits.MaxStringLength"/>).</summary>
    private const int TextTotalFactor = 16;

    /// <summary>The separator of the two ends of a bin in the text of a binned field (Vega-Lite's <c>BIN_RANGE_DELIMITER</c>, an en dash between spaces).</summary>
    private const string BinRangeDelimiter = " \u2013 ";

    /// <summary>The font size of a text that nothing else sizes (Vega's text style).</summary>
    private const double DefaultFontSize = 11;

    /// <summary>The kinds of source of the text of a row.</summary>
    private enum TextSourceKind
    {
        /// <summary>The mark has no text: nothing is drawn.</summary>
        None,

        /// <summary>A field of the <c>text</c> channel (formatted).</summary>
        Field,

        /// <summary>The <c>datum</c> of the <c>text</c> channel (formatted).</summary>
        Datum,

        /// <summary>The constant <c>value</c> of the <c>text</c> channel.</summary>
        Value,

        /// <summary>The <c>text</c> property of the mark.</summary>
        Property,
    }

    /// <summary>The ways a value becomes text.</summary>
    private enum TextFormatKind
    {
        /// <summary>The JavaScript text of the value (an array is one line for each element).</summary>
        Plain,

        /// <summary>A d3 number format.</summary>
        Number,

        /// <summary>A time format.</summary>
        Time,

        /// <summary>The start and the end of a bin, each as a number, with an en dash between them.</summary>
        BinRange,
    }

    /// <summary>
    ///     The properties of a text mark that every label shares.
    /// </summary>
    /// <param name="Dx">The horizontal offset.</param>
    /// <param name="Dy">The vertical offset.</param>
    /// <param name="Family">The font family, or null.</param>
    /// <param name="Weight">The font weight, or null.</param>
    /// <param name="Style">The font style, or null.</param>
    /// <param name="Align">The horizontal alignment.</param>
    /// <param name="Baseline">The baseline.</param>
    /// <param name="Limit">The longest line in pixels, or 0.</param>
    /// <param name="LineHeight">The distance between two baselines, or null for the font size plus 2.</param>
    /// <param name="Ellipsis">The text that ends a line that was cut.</param>
    private sealed record TextFont(double Dx, double Dy, string? Family, string? Weight, string? Style, string Align, string Baseline, double Limit, double? LineHeight, string Ellipsis);

    /// <summary>
    ///     Where the text of the rows comes from and how a value becomes text.
    /// </summary>
    /// <param name="Kind">The kind of source.</param>
    /// <param name="FieldIndex">The index of the field in the table (a field source), or -1.</param>
    /// <param name="Field2Index">The index of the field of the end of the bin (a binned field), or -1.</param>
    /// <param name="Datum">The datum (a datum source), or null.</param>
    /// <param name="Value">The constant value (a value source), or null.</param>
    /// <param name="Property">The <c>text</c> property of the mark (a property source), or null.</param>
    /// <param name="Format">How a field or datum becomes text.</param>
    /// <param name="Number">The number format of <see cref="TextFormatKind.Number"/>, and of the ends of a <see cref="TextFormatKind.BinRange"/>.</param>
    /// <param name="Time">The time formatter of <see cref="TextFormatKind.Time"/>: an instant and the longest text allowed give the text, or null when it is longer.</param>
    private sealed record TextSource(
        TextSourceKind Kind,
        int FieldIndex = -1,
        int Field2Index = -1,
        DataValue? Datum = null,
        SpecNode? Value = null,
        ConfigNode? Property = null,
        TextFormatKind Format = TextFormatKind.Plain,
        NumberFormat? Number = null,
        Func<double, int, string?>? Time = null);

    /// <summary>
    ///     The lines of one label: a text that a delimiter may still split, or a list of lines.
    /// </summary>
    /// <remarks>
    ///     The number of lines and of characters are known before the lines exist, so the scene items of the lines can be charged before the lines are made.
    /// </remarks>
    private readonly struct TextLines
    {
        /// <summary>The text, or null for a list of lines.</summary>
        private readonly string? _text;

        /// <summary>The delimiter that splits the text, or null.</summary>
        private readonly string? _delimiter;

        /// <summary>The lines, or null for a text.</summary>
        private readonly string[]? _lines;

        /// <summary>
        ///     Initializes the lines of a text that a delimiter splits.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="delimiter">The delimiter, or null or empty for a single line.</param>
        public TextLines(string text, string? delimiter)
        {
            _text = text;
            _lines = null;
            if (string.IsNullOrEmpty(delimiter) || text.Length == 0)
            {
                _delimiter = null;
                Count = 1;
                Characters = text.Length;
                return;
            }

            // The delimiters are counted without a copy of the text: the lines are made only after they are charged
            _delimiter = delimiter;
            var delimiters = 0;
            for (var at = text.IndexOf(delimiter, StringComparison.Ordinal); at >= 0; at = text.IndexOf(delimiter, at + delimiter.Length, StringComparison.Ordinal))
            {
                delimiters++;
            }

            Count = delimiters + 1;
            Characters = text.Length - ((long)delimiters * delimiter.Length);
        }

        /// <summary>
        ///     Initializes a list of lines.
        /// </summary>
        /// <param name="lines">The lines; ownership passes to the struct.</param>
        /// <param name="characters">The number of characters of all the lines.</param>
        public TextLines(string[] lines, long characters)
        {
            _text = null;
            _delimiter = null;
            _lines = lines;
            Count = lines.Length;
            Characters = characters;
        }

        /// <summary>Gets the number of lines.</summary>
        public int Count { get; }

        /// <summary>Gets the number of characters of all the lines.</summary>
        public long Characters { get; }

        /// <summary>Gets a value indicating whether there is nothing to draw: the text is empty or the list has no line (Vega draws no text of length zero).</summary>
        public bool IsEmpty => _lines is null ? _text!.Length == 0 : _lines.Length == 0;

        /// <summary>
        ///     Makes the lines.
        /// </summary>
        /// <returns>The lines: the elements of the list, the parts of the text between the delimiters, or the text itself.</returns>
        public string[] ToArray()
        {
            if (_lines is not null)
            {
                return _lines;
            }

            return _delimiter is null ? [_text!] : _text!.Split(_delimiter, StringSplitOptions.None);
        }
    }

    /// <content>
    ///     The text mark of a run.
    /// </content>
    private sealed partial class Run
    {
        /// <summary>The number of characters of the labels made so far (every label of the mark is charged to it).</summary>
        private long _textTotal;

        /// <summary>
        ///     Builds the items of a text mark: one for each row that has a position, a paint and a text.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The position is that of <c>x</c> and <c>y</c> (the middle of the data rectangle for an absent channel) plus the polar offset of <c>radius</c> and <c>theta</c> (Vega: <c>x += r cos(theta - pi/2)</c>,
        ///         <c>y += r sin(theta - pi/2)</c>, with the mark properties of the same names as constants), then <c>dx</c> and <c>dy</c>, which the item keeps apart. The font size is the <c>size</c> channel, else
        ///         the <c>fontSize</c> of the mark, else its <c>size</c>, else 11. <c>align</c> is <c>center</c> and <c>baseline</c> is <c>middle</c> unless the mark or the configuration say otherwise.
        ///     </para>
        ///     <para>
        ///         A row is charged in this order: its position and paint are read, then its text is made under the characters that are left of the mark's total, then the characters are charged to the
        ///         evaluation steps, then the item and its extra lines are charged to the scene budget, and only then is the item created.
        ///     </para>
        /// </remarks>
        /// <returns>The items.</returns>
        /// <exception cref="VegaLiteSpecException">Thrown, with the location of the <c>format</c>, when a number or time format is not valid.</exception>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when a label is longer than <see cref="VegaLiteLimits.MaxStringLength"/>, the labels together are longer than sixteen times that, or the scene budget or the step budget is exhausted.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
        private List<SceneItem> Texts()
        {
            var source = CreateTextSource();
            if (source.Kind == TextSourceKind.None)
            {
                return [];
            }

            var x = Get("x");
            var y = Get("y");
            var theta = Get("theta");
            var radius = Get("radius");
            var thetaConstant = Prop("theta")?.AsNumber();
            var radiusConstant = Prop("radius")?.AsNumber();
            var sizeChannel = Get("size");
            var angleChannel = Get("angle");
            var angle = Prop("angle")?.AsNumber() ?? 0;
            var fontSize = Def("fontSize")?.AsNumber();
            var fontDefault = Def("size")?.AsNumber() ?? Vl("fontSize")?.AsNumber() ?? Vl("size")?.AsNumber() ?? Prop("fontSize", vega: true)?.AsNumber() ?? DefaultFontSize;
            var strokeWidth = Prop("strokeWidth", vega: true)?.AsNumber();
            var delimiter = Prop("lineBreak")?.AsString();
            var font = ReadTextFont();
            var hasPolar = theta is not null || radius is not null || thetaConstant is not null || radiusConstant is not null;
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            var rows = MarkRows();
            for (var index = 0; index < _table.RowCount; index++)
            {
                Batch(index);
                var row = rows?[index] ?? index;
                Charge(1);
                var size = sizeChannel is null
                    ? fontSize ?? ExpressionNumber(_sizeExpression, row) ?? fontDefault
                    : Encoded(sizeChannel, row, fontSize ?? fontDefault);
                if (size is not { } rowSize || PointPosition(x, _width, row) is not { } px || PointPosition(y, _height, row) is not { } py || !TryPaint(row, strokeWidth, out var paint))
                {
                    continue;
                }

                if (hasPolar)
                {
                    var r = radius is null ? radiusConstant ?? 0 : PolarValue(radius, row);
                    var t = theta is null ? thetaConstant ?? 0 : PolarValue(theta, row);
                    if (r is not { } offset || t is not { } turn)
                    {
                        continue;
                    }

                    // Vega moves the anchor by the radius along the angle that runs clockwise from twelve o'clock
                    if (offset != 0)
                    {
                        px += offset * Math.Cos(turn - (Math.PI / 2));
                        py += offset * Math.Sin(turn - (Math.PI / 2));
                    }
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

                if (LinesOf(source, row, delimiter) is not { IsEmpty: false } lines)
                {
                    continue;
                }

                // The characters are charged to the steps and the lines to the scene budget before the item exists
                _textTotal += lines.Characters;
                Charge(lines.Characters + lines.Count);
                Reserve(lines.Count);
                items.Add(Style(
                    new SceneText("mark-text", px, py, lines.ToArray())
                    {
                        Dx = font.Dx,
                        Dy = font.Dy,
                        FontFamily = font.Family,
                        FontSize = rowSize,
                        FontWeight = font.Weight,
                        FontStyle = font.Style,
                        Align = font.Align,
                        Baseline = font.Baseline,
                        Angle = rowAngle,
                        Limit = font.Limit,
                        LineHeight = font.LineHeight,
                        Ellipsis = font.Ellipsis,
                    },
                    paint));
            }

            return items;
        }

        /// <summary>
        ///     Reads the properties of the mark that are the same for every label.
        /// </summary>
        /// <returns>The offsets, the font, the alignment, the limit and the ellipsis.</returns>
        private TextFont ReadTextFont() => new(
            Prop("dx")?.AsNumber() ?? 0,
            Prop("dy")?.AsNumber() ?? 0,
            Prop("font", vega: true)?.AsString(),
            TextProperty("fontWeight"),
            TextProperty("fontStyle"),
            TextAlign(),
            TextBaseline(),
            Prop("limit")?.AsNumber() ?? 0,
            Prop("lineHeight")?.AsNumber(),
            Prop("ellipsis")?.AsString() is { Length: > 0 } ellipsis ? ellipsis : "\u2026");

        /// <summary>
        ///     Reads the horizontal alignment of the text: <c>center</c> when neither the mark nor the configuration gives one, <c>left</c> for a name Vega does not know.
        /// </summary>
        /// <returns>The alignment.</returns>
        private string TextAlign() => Prop("align", vega: true)?.AsString() switch
        {
            null => "center",
            "left" => "left",
            "right" => "right",
            "center" => "center",
            _ => "left",
        };

        /// <summary>
        ///     Reads a mark property that is a text or a number as text (a font weight is <c>bold</c> or <c>700</c>).
        /// </summary>
        /// <param name="name">The property.</param>
        /// <returns>The text; null when the property is absent or is neither.</returns>
        private string? TextProperty(string name) => Prop(name) switch
        {
            { Kind: ConfigNodeKind.String } text => text.StringValue,
            { Kind: ConfigNodeKind.Number } number => JsNumber.ToString(number.NumberValue),
            _ => null,
        };

        /// <summary>
        ///     Reads the baseline of the text: one of the names Vega knows, <c>middle</c> when nothing gives one, <c>alphabetic</c> for an unknown name.
        /// </summary>
        /// <returns>The baseline.</returns>
        private string TextBaseline() => Prop("baseline", vega: true)?.AsString() switch
        {
            null => "middle",
            "alphabetic" => "alphabetic",
            "top" => "top",
            "middle" => "middle",
            "bottom" => "bottom",
            "line-top" => "line-top",
            "line-bottom" => "line-bottom",
            _ => "alphabetic",
        };

        /// <summary>
        ///     Gets the value of a polar channel for a row: the angle in radians or the radius in pixels.
        /// </summary>
        /// <param name="channel">The <c>theta</c> or <c>radius</c> channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The value; a constant <c>value</c> as it is (0 when there is none); the middle of its slice (or where the band position puts it) for a stacked channel; null when the row has no valid value.</returns>
        private double? PolarValue(Channel channel, int row)
        {
            if (channel.Model is { } model)
            {
                return channel.IsStacked ? StackedPolarValue(channel, model, row) : Center(channel, model, row);
            }

            return channel.Value?.AsNumber() ?? 0;
        }

        /// <summary>
        ///     Applies the paint and the stroke properties of the mark to a text.
        /// </summary>
        /// <param name="item">The text.</param>
        /// <param name="paint">The paint of the row.</param>
        /// <returns>The styled text.</returns>
        private SceneText Style(SceneText item, Paint paint) => item with
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
            StrokeMiterLimit = Prop("strokeMiterLimit")?.AsNumber(),
        };

        /// <summary>
        ///     Decides where the text of the rows comes from and how a value becomes text (Vega-Lite's <c>textRef</c> and <c>formatSignalRef</c>).
        /// </summary>
        /// <remarks>
        ///     The <c>text</c> channel wins over the <c>text</c> property of the mark. A field reads the field the encoding plan names (the start of a bin or time unit, an aggregate), a
        ///     <c>datum</c> and a <c>value</c> are constants. A <c>formatType</c> that is not <c>number</c>, <c>time</c> or <c>utc</c> is a custom format: it is not evaluated and the default format of the
        ///     type is used (without the <c>format</c>). A time format applies to a temporal field, a field with a time unit and the format types <c>time</c> and <c>utc</c>, a d3 number format to a quantitative
        ///     field or any field with a <c>format</c>, and a binned field shows <c>start – end</c> (null when the start is not a number); every other value is its JavaScript text.
        /// </remarks>
        /// <returns>The source.</returns>
        /// <exception cref="VegaLiteSpecException">Thrown, with the location of the <c>format</c>, when a number or time format is not valid.</exception>
        private TextSource CreateTextSource()
        {
            if (_view["encoding"]?["text"] is not { IsObject: true } definition)
            {
                return PropertySource();
            }

            var plan = _plan?.Channel("text");
            TextSourceKind kind;
            var fieldIndex = plan is { FieldDefinition: null, FieldPath: { } path } ? _table.FindField(path) : -1;
            DataValue? datum = null;
            if (fieldIndex >= 0)
            {
                kind = TextSourceKind.Field;
            }
            else if (definition["datum"] is { } datumNode && DatumValue(datumNode) is { } scalar)
            {
                kind = TextSourceKind.Datum;
                datum = scalar;
            }
            else if (definition["value"] is { } value)
            {
                return new TextSource(TextSourceKind.Value, Value: value);
            }
            else if (plan is not null || definition["field"] is not null)
            {
                // A field that the table does not have, or a condition that is not applied: there is no text
                return new TextSource(TextSourceKind.None);
            }
            else
            {
                return PropertySource();
            }

            var formatType = definition["formatType"]?.AsString();
            string? format = null;
            if (formatType is null || FeatureCatalog.IsEvaluatedFormatType(formatType))
            {
                format = definition["format"] is { IsString: true } formatNode ? formatNode.StringValue : null;
            }
            else
            {
                // A custom format type is not evaluated (the catalog reports it): the default format of the type applies
                formatType = null;
            }

            var unit = plan?.TimeUnit?.Spec;
            var type = definition["type"]?.AsString();
            if (type is null && unit is not null)
            {
                type = "temporal";
            }
            else if (type is null && kind == TextSourceKind.Field)
            {
                type = "quantitative";
            }
            var formatLocation = (definition["format"] ?? definition).GetLocation(_owner._sourceName);
            if (formatType is "time" or "utc" || (formatType is null && (type == "temporal" || unit is not null)))
            {
                var utc = unit is { Utc: true } || definition["scale"]?["type"]?.AsString() == "utc";
                var calendar = utc ? TimeCalendar.Utc : _owner._expressions?.Evaluator.Context.Calendar ?? TimeCalendar.Utc;
                var time = TimeLabels.CreateBounded(new TimeLabels.Options(
                    format is null ? null : ConfigNode.FromString(format),
                    formatType,
                    unit,
                    _owner._config.GetString("timeFormat"),
                    calendar,
                    _owner._maxStringLength,
                    "text mark",
                    _location,
                    formatLocation));
                return new TextSource(kind, fieldIndex, Datum: datum, Format: TextFormatKind.Time, Time: time);
            }

            var field2Index = plan is { Bin: BinKind.Binning, Field2Path: { } endPath } && kind == TextSourceKind.Field ? _table.FindField(endPath) : -1;
            if (field2Index >= 0)
            {
                return new TextSource(kind, fieldIndex, field2Index, datum, Format: TextFormatKind.BinRange, Number: NumberOf(format ?? _owner._config.GetString("numberFormat") ?? string.Empty, formatLocation));
            }

            if (format is not null || type == "quantitative")
            {
                return new TextSource(kind, fieldIndex, Datum: datum, Format: TextFormatKind.Number, Number: NumberOf(format ?? _owner._config.GetString("numberFormat") ?? string.Empty, formatLocation));
            }

            return new TextSource(kind, fieldIndex, Datum: datum);
        }

        /// <summary>
        ///     Creates the source of the <c>text</c> property of the mark.
        /// </summary>
        /// <returns>The source; none when the mark has no <c>text</c> property that is a text or a list of texts.</returns>
        private TextSource PropertySource() =>
            Prop("text") is { Kind: ConfigNodeKind.String or ConfigNodeKind.Array } property
                ? new TextSource(TextSourceKind.Property, Property: property)
                : new TextSource(TextSourceKind.None);

        /// <summary>
        ///     Reads the <c>datum</c> of the <c>text</c> channel.
        /// </summary>
        /// <param name="node">The datum.</param>
        /// <returns>The value; null for a datum that is not a number, a text or a boolean (a date-time object is reported by the feature catalog and not drawn).</returns>
        private static DataValue? DatumValue(SpecNode node) => node.Kind switch
        {
            SpecNodeKind.Number => DataValue.FromNumber(node.NumberValue),
            SpecNodeKind.String => DataValue.FromString(node.StringValue),
            SpecNodeKind.Boolean => DataValue.FromBoolean(node.BoolValue),
            _ => null,
        };

        /// <summary>
        ///     Compiles a d3 number format.
        /// </summary>
        /// <param name="specifier">The specifier.</param>
        /// <param name="location">The location of the <c>format</c>.</param>
        /// <returns>The formatter.</returns>
        /// <exception cref="VegaLiteSpecException">Thrown, with the location, when the specifier is not valid.</exception>
        private static NumberFormat NumberOf(string specifier, VegaLiteLocation location)
        {
            try
            {
                return NumberFormat.Create(specifier);
            }
            catch (FormatException ex)
            {
                throw new VegaLiteSpecException($"The text format is not a valid d3 format specifier: {ex.Message}", location, ex);
            }
        }

        /// <summary>
        ///     Gets the number of characters that the next label may have: a label is at most <see cref="VegaLiteLimits.MaxStringLength"/> and all the labels of the mark together at most sixteen times that.
        /// </summary>
        /// <returns>The characters that are left for one label.</returns>
        private int TextAllowance()
        {
            var left = ((long)_owner._maxStringLength * TextTotalFactor) - _textTotal;
            return (int)Math.Max(0, Math.Min(_owner._maxStringLength, left));
        }

        /// <summary>
        ///     Creates the failure of a label that does not fit.
        /// </summary>
        /// <param name="allowance">The characters that were left for the label.</param>
        /// <returns>The exception, located at the mark.</returns>
        private VegaLiteRenderException TextTooLong(int allowance) =>
            allowance < _owner._maxStringLength
                ? new VegaLiteRenderException($"The labels of the text mark are together longer than the limit of {TextTotalFactor} times {_owner._maxStringLength} characters (VegaLiteLimits.MaxStringLength).", _location)
                : new VegaLiteRenderException($"A label of the text mark is longer than the limit of {_owner._maxStringLength} characters (VegaLiteLimits.MaxStringLength).", _location);

        /// <summary>
        ///     Makes the lines of the label of a row.
        /// </summary>
        /// <param name="source">The source of the text.</param>
        /// <param name="row">The row.</param>
        /// <param name="delimiter">The <c>lineBreak</c> of the mark, or null.</param>
        /// <returns>The lines; null when the row has no text.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the label does not fit the characters that are left.</exception>
        private TextLines? LinesOf(TextSource source, int row, string? delimiter)
        {
            var allowance = TextAllowance();
            switch (source.Kind)
            {
                case TextSourceKind.Field:
                    var start = _table.GetValue(row, source.FieldIndex);
                    return FormatValue(source, start, source.Field2Index >= 0 ? _table.GetValue(row, source.Field2Index) : DataValue.Null, allowance, delimiter);
                case TextSourceKind.Datum:
                    return FormatValue(source, source.Datum!.Value, DataValue.Null, allowance, delimiter);
                case TextSourceKind.Value:
                    return Constant(source.Value!, allowance, delimiter);
                default:
                    return Constant(source.Property!, allowance, delimiter);
            }
        }

        /// <summary>
        ///     Turns the value of a field or a datum into lines with the format of the source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="value">The value, or the start of a bin.</param>
        /// <param name="end">The end of a bin; null when the field is not binned.</param>
        /// <param name="allowance">The characters that are left for the label.</param>
        /// <param name="delimiter">The delimiter of the lines, or null.</param>
        /// <returns>The lines.</returns>
        private TextLines FormatValue(TextSource source, DataValue value, DataValue end, int allowance, string? delimiter)
        {
            string? text;
            switch (source.Format)
            {
                case TextFormatKind.Time when InstantOf(value) is { } instant:
                    text = source.Time!(instant, allowance);
                    break;
                case TextFormatKind.Number:
                    text = source.Number!.Format(ExpressionEvaluator.ToNumber(value));
                    break;
                case TextFormatKind.BinRange:
                    // A start that is not a number is shown as null, as Vega-Lite's bin format does
                    text = value.TryGetQuantitative(out var from) && !double.IsNaN(from)
                        ? source.Number!.Format(from) + BinRangeDelimiter + source.Number.Format(ExpressionEvaluator.ToNumber(end))
                        : "null";
                    break;
                default:
                    return Plain(value, allowance, delimiter);
            }

            return text is null || text.Length > allowance ? throw TextTooLong(allowance) : new TextLines(text, delimiter);
        }

        /// <summary>
        ///     Reads a value as an instant for a time format.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The milliseconds since the epoch of a date or a number; null for any other value (it is shown as its JavaScript text).</returns>
        private static double? InstantOf(DataValue value)
        {
            double? instant = value.Kind switch
            {
                DataValueKind.Date => value.Milliseconds,
                DataValueKind.Number => value.Number,
                _ => null,
            };
            return instant is { } ms && TimeCalendar.IsValid(ms) ? ms : null;
        }

        /// <summary>
        ///     Turns a value that has no format into lines: its JavaScript text, or one line for each element of an array.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="allowance">The characters that are left for the label.</param>
        /// <param name="delimiter">The delimiter of the lines, or null.</param>
        /// <returns>The lines.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the text does not fit.</exception>
        private TextLines Plain(DataValue value, int allowance, string? delimiter)
        {
            try
            {
                if (value.IsArray)
                {
                    var elements = value.Elements;
                    var lines = new string[elements.Length];
                    var left = (long)allowance;
                    var total = 0L;
                    for (var i = 0; i < elements.Length; i++)
                    {
                        // A null element is an empty line; the rest are their JavaScript text
                        lines[i] = elements[i].IsNull ? string.Empty : ExpressionEvaluator.ToJsString(elements[i], (int)Math.Max(0, left));
                        left -= lines[i].Length;
                        total += lines[i].Length;
                    }

                    return new TextLines(lines, total);
                }

                return new TextLines(value.IsNull ? "null" : ExpressionEvaluator.ToJsString(value, allowance), delimiter);
            }
            catch (ExpressionRuntimeException)
            {
                throw TextTooLong(allowance);
            }
        }

        /// <summary>
        ///     Turns the constant <c>value</c> of the channel into lines.
        /// </summary>
        /// <param name="node">The value: a text, a number, a boolean, or an array of them.</param>
        /// <param name="allowance">The characters that are left for the label.</param>
        /// <param name="delimiter">The delimiter of the lines, or null.</param>
        /// <returns>The lines; null for a value that is null or an object.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the text does not fit.</exception>
        private TextLines? Constant(SpecNode node, int allowance, string? delimiter)
        {
            if (node.IsArray)
            {
                return ConstantList(node.Items.Select(ConstantText), node.Items.Count, allowance);
            }

            return ConstantText(node) is { } text ? Checked(text, allowance, delimiter) : null;
        }

        /// <summary>
        ///     Turns the <c>text</c> property of the mark into lines.
        /// </summary>
        /// <param name="node">The property: a text or an array of texts.</param>
        /// <param name="allowance">The characters that are left for the label.</param>
        /// <param name="delimiter">The delimiter of the lines, or null.</param>
        /// <returns>The lines.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the text does not fit.</exception>
        private TextLines? Constant(ConfigNode node, int allowance, string? delimiter)
        {
            if (node.Kind == ConfigNodeKind.Array)
            {
                return ConstantList(node.Items.Select(ConstantText), node.Items.Count, allowance);
            }

            return node.AsString() is { } text ? Checked(text, allowance, delimiter) : null;
        }

        /// <summary>
        ///     Makes the lines of a constant list of texts.
        /// </summary>
        /// <param name="texts">The texts; a null entry is an empty line.</param>
        /// <param name="count">The number of entries.</param>
        /// <param name="allowance">The characters that are left for the label.</param>
        /// <returns>The lines.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the texts do not fit.</exception>
        private TextLines ConstantList(IEnumerable<string?> texts, int count, int allowance)
        {
            var lines = new string[count];
            var total = 0L;
            var index = 0;
            foreach (var text in texts)
            {
                var line = text ?? string.Empty;
                total += line.Length;
                if (total > allowance)
                {
                    throw TextTooLong(allowance);
                }

                lines[index++] = line;
            }

            return new TextLines(lines, total);
        }

        /// <summary>
        ///     Checks that a constant text fits and makes its lines.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="allowance">The characters that are left for the label.</param>
        /// <param name="delimiter">The delimiter of the lines, or null.</param>
        /// <returns>The lines.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the text does not fit.</exception>
        private TextLines Checked(string text, int allowance, string? delimiter) =>
            text.Length > allowance ? throw TextTooLong(allowance) : new TextLines(text, delimiter);

        /// <summary>
        ///     Reads a constant as the JavaScript text of the value.
        /// </summary>
        /// <param name="node">The value.</param>
        /// <returns>The text; an empty text for a null element; null for an object or an array (which has no text of its own).</returns>
        private static string? ConstantText(SpecNode node) => node.Kind switch
        {
            SpecNodeKind.String => node.StringValue,
            SpecNodeKind.Number => JsNumber.ToString(node.NumberValue),
            SpecNodeKind.Boolean => node.BoolValue ? "true" : "false",
            SpecNodeKind.Null => string.Empty,
            _ => null,
        };

        /// <summary>
        ///     Reads an element of the <c>text</c> property of the mark as text.
        /// </summary>
        /// <param name="node">The element.</param>
        /// <returns>The text; an empty text for anything that is not a text, a number or a boolean.</returns>
        private static string? ConstantText(ConfigNode node) => node.Kind switch
        {
            ConfigNodeKind.String => node.StringValue,
            ConfigNodeKind.Number => JsNumber.ToString(node.NumberValue),
            ConfigNodeKind.Boolean => node.BoolValue ? "true" : "false",
            _ => string.Empty,
        };
    }
}

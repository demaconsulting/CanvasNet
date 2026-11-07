// Portions derived from Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The image mark of <see cref="MarkBuilder"/>: one <see cref="SceneImage"/> per row, its anchor or span on each axis and its link.
/// </content>
internal sealed partial class MarkBuilder
{
    /// <summary>
    ///     Where a picture is placed along one axis.
    /// </summary>
    /// <param name="Position">The start of the span when <paramref name="Length"/> is given; otherwise the anchor of the picture.</param>
    /// <param name="Length">The length of the span, or null when the length is the natural one of the picture (Vega's missing <c>width</c> or <c>height</c>).</param>
    /// <param name="Anchor">How <paramref name="Position"/> anchors the picture: <c>left</c> or <c>top</c> for a span, the <c>align</c> or <c>baseline</c> for a position.</param>
    private readonly record struct ImageAxis(double Position, double? Length, string Anchor);

    /// <content>
    ///     The image mark of a run.
    /// </content>
    private sealed partial class Run
    {
        /// <summary>
        ///     Builds the items of an image mark: one for each row that has a position, an opacity that is a number and a link.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <b>Position and size</b> (Vega-Lite 6.5, <c>compile/mark/image.ts</c> and <c>encode/position-rect.ts</c>, <c>position-range.ts</c>; Vega's <c>vega-runtime/src/util.js</c> for the <c>x2</c>, <c>xc</c> and
        ///         <c>width</c> channels). Each axis is decided on its own, in the order of Vega-Lite's rule: a field that is binned (or has a time unit) on a continuous scale spans its bin when no size is given; an
        ///         <c>x</c> (<c>y</c>) channel with no size and no <c>x2</c> (<c>y2</c>) gives a <b>point</b>: the picture is anchored there by the <c>align</c> (<c>baseline</c>) of the mark (center and middle by default) and
        ///         has the width (height) of the picture itself, which only the painter knows, so Vega-Lite does not use <c>xc</c> for it; a discrete field with a size (<c>width</c>, <c>height</c>, <c>size</c>) spans its band
        ///         (or is centered in it with the <c>width</c> or <c>height</c> of the mark); otherwise the axis is a range: from <c>x</c> to <c>x2</c> (or, without <c>x2</c>, to the zero of the scale),
        ///         or, with a <c>width</c> (<c>height</c>) of the mark, a picture of that size anchored at <c>x</c> by the <c>align</c> (<c>baseline</c>); an axis without a channel spans the whole data rectangle, or, with a
        ///         <c>width</c> (<c>height</c>), has the picture anchored at the right edge (the top) of the data rectangle. The <c>align</c> and <c>baseline</c> of the mark otherwise do not apply, because
        ///         Vega-Lite's <c>image</c> ignores them in its base encoding. The <c>size</c> and <c>color</c> channels and the <c>angle</c>, <c>shape</c> and <c>orient</c> of the mark are ignored too.
        ///     </para>
        ///     <para>
        ///         <b>Link.</b> The <c>url</c> channel (a field, a <c>datum</c> or a <c>value</c>), else the <c>url</c> property of the mark; a value is turned into text as JavaScript does and a row whose link is
        ///         null or empty draws no picture (Vega loads nothing for it). <b>Look.</b> The <c>aspect</c> (true by default) and <c>smooth</c> (true by default) of the mark are carried by the item; the opacity is the
        ///         <c>opacity</c> of the mark or channel (1 by default: an image is not one of the marks that Vega-Lite draws less opaque).
        ///     </para>
        ///     <para>
        ///         A row is charged to the scene budget before its item is created; a position or size beyond <see cref="ScenePainter.MaxMagnitude"/> or that is not finite skips the row, as it does for the other marks.
        ///     </para>
        /// </remarks>
        /// <returns>The items.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when a link is longer than <see cref="VegaLiteLimits.MaxStringLength"/> or the scene budget is exhausted.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
        private List<SceneItem> Images()
        {
            var url = ImageUrlSource();
            if (url is null)
            {
                return [];
            }

            var x = Get("x");
            var y = Get("y");
            var aspect = Prop("aspect")?.AsBool() ?? true;
            var smooth = Prop("smooth")?.AsBool() ?? true;
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            var rows = MarkRows();
            for (var index = 0; index < _table.RowCount; index++)
            {
                Batch(index);
                var row = rows?[index] ?? index;
                Charge(1);
                if (!TryPaint(row, null, out var paint)
                    || url(row) is not { Length: > 0 } link
                    || ImageAxisOf(isX: true, x, row) is not { } horizontal
                    || ImageAxisOf(isX: false, y, row) is not { } vertical
                    || !ScenePainter.IsBounded(horizontal.Position, vertical.Position, horizontal.Length ?? 0, vertical.Length ?? 0))
                {
                    continue;
                }

                Reserve();
                items.Add(new SceneImage("mark-image", link, horizontal.Position, vertical.Position)
                {
                    Width = horizontal.Length,
                    Height = vertical.Length,
                    Aspect = aspect,
                    Align = horizontal.Anchor,
                    Baseline = vertical.Anchor,
                    Smooth = smooth,
                    Source = _location,
                    Opacity = paint.Opacity,
                });
            }

            return items;
        }

        /// <summary>
        ///     Decides where the link of each row comes from.
        /// </summary>
        /// <remarks>
        ///     The <c>url</c> channel wins over the <c>url</c> property of the mark. A field reads the field the encoding plan names, a <c>datum</c> and a <c>value</c> are constants; a condition of the channel
        ///     is not applied (the feature catalog reports it).
        /// </remarks>
        /// <returns>The function that gives the link of a row (null for a row without one); null when the mark has no link at all.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when a link is longer than <see cref="VegaLiteLimits.MaxStringLength"/>.</exception>
        private Func<int, string?>? ImageUrlSource()
        {
            if (_view["encoding"]?["url"] is not { IsObject: true } definition)
            {
                return Prop("url")?.AsString() is { } property ? _ => property : null;
            }

            var plan = _plan?.Channel("url");
            var fieldIndex = plan is { FieldDefinition: null, FieldPath: { } path } ? _table.FindField(path) : -1;
            if (fieldIndex >= 0)
            {
                return row => LinkOf(_table.GetValue(row, fieldIndex));
            }

            if (definition["datum"] is { } datum && DatumValue(datum) is { } scalar)
            {
                var text = LinkOf(scalar);
                return _ => text;
            }

            if (definition["value"] is { } value)
            {
                var text = ConstantText(value);
                return _ => text;
            }

            // A field that the table does not have, or a condition that is not applied: there is no link
            if (plan is not null || definition["field"] is not null)
            {
                return null;
            }

            return Prop("url")?.AsString() is { } fallback ? _ => fallback : null;
        }

        /// <summary>
        ///     Turns the value of a link into text as JavaScript does.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The text; null for a null value (no picture).</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the text is longer than <see cref="VegaLiteLimits.MaxStringLength"/>.</exception>
        private string? LinkOf(DataValue value)
        {
            if (value.IsNull)
            {
                return null;
            }

            try
            {
                var text = ExpressionEvaluator.ToJsString(value, _owner._maxStringLength);
                Charge(text.Length);
                return text;
            }
            catch (ExpressionRuntimeException)
            {
                throw new VegaLiteRenderException($"A link of the image mark is longer than the limit of {_owner._maxStringLength} characters (VegaLiteLimits.MaxStringLength).", _location);
            }
        }

        /// <summary>
        ///     Works out where a picture goes along one axis (Vega-Lite's <c>rectPosition</c> for the image mark).
        /// </summary>
        /// <param name="isX"><see langword="true"/> for the x axis.</param>
        /// <param name="channel">The channel, or null when the encoding has none.</param>
        /// <param name="row">The row.</param>
        /// <returns>The placement; null when the row has no valid value.</returns>
        private ImageAxis? ImageAxisOf(bool isX, Channel? channel, int row)
        {
            var extent = isX ? _width : _height;
            var span = isX ? "left" : "top";
            var model = channel?.Model;

            // A size is a truthy width (height) of the mark or its configuration, or a size of the mark or the encoding (Vega-Lite's hasSizeDef)
            var fixedSize = NonZero(Prop(isX ? "width" : "height")?.AsNumber());
            var hasSize = Get("size") is not null || fixedSize is not null || NonZero(Prop("size")?.AsNumber()) is not null;
            var hasSecondEnd = channel is { HasRange: true } or { IsStacked: true };

            // A field that is binned, or has a time unit, on a continuous scale spans its bin
            if (channel is { HasBandEnds: true, Offset: null } && model is not null && model.Kind is not (ScaleKind.Band or ScaleKind.Point) && !hasSize)
            {
                return BinSpan(channel, model, new Sizing(null, null, null), row) is { } bin ? new ImageAxis(bin.Start, bin.Length, span) : null;
            }

            // A position without a size or a second end: the picture is anchored there and has its natural size along the axis
            if (channel is not null && !hasSize && !hasSecondEnd)
            {
                var anchor = Prop(isX ? "align" : "baseline")?.AsString() ?? (isX ? "center" : "middle");
                return PointPosition(channel, extent, row) is { } point ? new ImageAxis(point, null, anchor) : null;
            }

            // A discrete field spans its band, or is as long as the width (height) of the mark and centered in the band
            if (model is { Kind: ScaleKind.Band or ScaleKind.Point } && !hasSecondEnd)
            {
                return BandSpan(channel!, model, new Sizing(fixedSize, null, null), row) is { } band ? new ImageAxis(band.Start, band.Length, span) : null;
            }

            return RangeAxis(isX, channel, fixedSize, hasSecondEnd, row);
        }

        /// <summary>
        ///     Works out the span of a picture along an axis that is a range: from a position to a second end, or a size anchored at a position.
        /// </summary>
        /// <param name="isX"><see langword="true"/> for the x axis.</param>
        /// <param name="channel">The channel, or null.</param>
        /// <param name="fixedSize">The width (height) of the mark, or null.</param>
        /// <param name="hasSecondEnd">Whether the channel has a second end (<c>x2</c> or the lower end of a stack).</param>
        /// <param name="row">The row.</param>
        /// <returns>The span; null when the row has no valid value.</returns>
        private ImageAxis? RangeAxis(bool isX, Channel? channel, double? fixedSize, bool hasSecondEnd, int row)
        {
            var extent = isX ? _width : _height;
            var span = isX ? "left" : "top";
            if (fixedSize is { } size && !hasSecondEnd)
            {
                // A size is anchored at the position (the default position of an absent channel is the right edge or the top), by the align (baseline) of the mark
                if (ImagePosition(isX, channel, row) is not { } at)
                {
                    return null;
                }

                var align = Prop(isX ? "align" : "baseline")?.AsString();
                var start = align switch
                {
                    "left" or "top" => at,
                    "right" or "bottom" => at - size,
                    _ => at - (size / 2),
                };
                return new ImageAxis(start, size, span);
            }

            // The mark's own second end (x2, y2) replaces the zero of the scale when the encoding has none
            if (!hasSecondEnd && ConfigPosition(Prop(isX ? "x2" : "y2"), extent) is { } second)
            {
                return ImagePosition(isX, channel, row) is { } from ? new ImageAxis(Math.Min(from, second), Math.Abs(from - second), span) : null;
            }

            return RangePair(isX, channel, row) is { } pair ? new ImageAxis(Math.Min(pair.Main, pair.Other), Math.Abs(pair.Main - pair.Other), span) : null;
        }

        /// <summary>
        ///     Gets the position of a picture along an axis for the rules that anchor it: the position of the channel, or, for an absent channel, the position that the mark gives or
        ///     the default of Vega-Lite's <c>zeroOrMax</c> (the right edge of the data rectangle for x, the top for y).
        /// </summary>
        /// <param name="isX"><see langword="true"/> for the x axis.</param>
        /// <param name="channel">The channel, or null.</param>
        /// <param name="row">The row.</param>
        /// <returns>The position; null when the row has no valid value.</returns>
        private double? ImagePosition(bool isX, Channel? channel, int row)
        {
            var extent = isX ? _width : _height;
            if (channel is not null)
            {
                return PointPosition(channel, extent, row);
            }

            var configured = ConfigPosition(Prop(isX ? "x" : "y"), extent);
            if (configured is not null)
            {
                return configured;
            }

            return isX ? extent : 0;
        }

        /// <summary>
        ///     Gives a number that is not zero (Vega-Lite tests a size for truthiness).
        /// </summary>
        /// <param name="value">The number, or null.</param>
        /// <returns>The number; null when it is null or zero.</returns>
        private static double? NonZero(double? value) => value is { } number && number.CompareTo(0) != 0 ? number : null;

        /// <summary>
        ///     Reads a position that a mark property gives: a number, or the keywords <c>width</c> and <c>height</c>.
        /// </summary>
        /// <param name="node">The property, or null.</param>
        /// <param name="extent">The size of the data rectangle along the axis.</param>
        /// <returns>The position, or null when there is no usable value.</returns>
        private static double? ConfigPosition(ConfigNode? node, double extent) => node switch
        {
            { Kind: ConfigNodeKind.Number } number => number.NumberValue,
            { Kind: ConfigNodeKind.String } text when text.StringValue is "width" or "height" => extent,
            _ => null,
        };
    }
}

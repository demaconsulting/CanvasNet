// Portions derived from d3-shape (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The arc mark and the polar position of a text mark: arcs are rectangles in polar coordinates (Vega-Lite's <c>arc.ts</c> and <c>rectPosition</c>).
/// </content>
internal sealed partial class MarkBuilder
{
    /// <content>
    ///     The arc mark of a run.
    /// </content>
    private sealed partial class Run
    {
        /// <summary>
        ///     Builds the items of an arc mark: one sector for each row that has a center, an angular span, a radial span and a paint.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The center is the position of <c>x</c> and <c>y</c> (the middle of the data rectangle for an absent channel). The angles are those of <c>theta</c> and <c>theta2</c>, the radii those of
        ///         <c>radius</c> (the outer one) and <c>radius2</c> (the inner one), each through <see cref="PolarSpan"/>. <c>padAngle</c> and <c>cornerRadius</c> are read from the mark definition, then the
        ///         configuration. The paint is that of the other filled marks (the mark color is the fill, a stroke only when one is given).
        ///     </para>
        ///     <para>
        ///         A row is charged in this order: one evaluation step, then its position and paint are read (a row that draws nothing costs no scene item), then one scene item is charged, and only then is the
        ///         <see cref="SceneArc"/> created. An arc has a constant size (at most sixteen path commands when painted), so the item budget is its whole cost.
        ///     </para>
        /// </remarks>
        /// <returns>The items.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the scene budget or the step budget is exhausted.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
        private List<SceneItem> Arcs()
        {
            var x = Get("x");
            var y = Get("y");
            var theta = Get("theta");
            var radius = Get("radius");
            var thetaConstant = Prop("theta")?.AsNumber();
            var theta2Constant = Prop("theta2")?.AsNumber();
            var radiusConstant = Prop("radius")?.AsNumber() ?? Prop("outerRadius")?.AsNumber();
            var radius2Constant = Prop("radius2")?.AsNumber() ?? Prop("innerRadius")?.AsNumber();
            var radiusExtent = Math.Min(_width, _height) / 2;
            var padAngle = Prop("padAngle")?.AsNumber() ?? 0;
            var cornerRadius = Prop("cornerRadius")?.AsNumber() ?? 0;
            var strokeWidth = Prop("strokeWidth", vega: true)?.AsNumber();
            var items = new List<SceneItem>(Math.Min(_table.RowCount, 4096));
            var rows = MarkRows();
            for (var index = 0; index < _table.RowCount; index++)
            {
                Batch(index);
                var row = rows?[index] ?? index;
                Charge(1);
                if (PointPosition(x, _width, row) is not { } cx
                    || PointPosition(y, _height, row) is not { } cy
                    || PolarSpan(theta, thetaConstant, theta2Constant, 2 * Math.PI, row) is not { } angles
                    || PolarSpan(radius, radiusConstant, radius2Constant, radiusExtent, row) is not { } radii
                    || !TryPaint(row, strokeWidth, out var paint))
                {
                    continue;
                }

                Reserve();
                items.Add(Style(
                    new SceneArc("mark-arc", cx, cy)
                    {
                        InnerRadius = radii.Start,
                        OuterRadius = radii.End,
                        StartAngle = angles.Start,
                        EndAngle = angles.End,
                        PadAngle = padAngle,
                        CornerRadius = cornerRadius,
                    },
                    paint));
            }

            return items;
        }

        /// <summary>
        ///     Works out where an arc spans along one polar axis (Vega-Lite's <c>rectPosition</c> for <c>theta</c> and <c>radius</c>): the angles or the radii of the two ends of the sector.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         A band (or point) scale gives its band, a stacked channel its stack from the start to the end, a channel with a secondary
        ///         channel (<c>theta2</c>, <c>radius2</c>) the range between the two, and any other field the range from zero (the
        ///         first domain value when the domain has no zero) to its scaled value. Binning a polar channel is not supported (the feature catalog reports it). A channel that is a constant <c>value</c> is the main end. The second end of a channel that has no secondary
        ///         channel is the mark property (<paramref name="secondConstant"/>) before it is zero.
        ///     </para>
        ///     <para>
        ///         Without the channel the main end is the mark property (<paramref name="mainConstant"/>), else <paramref name="maximum"/> (the whole circle, half of the smaller side), and the second end
        ///         is its mark property, else zero. The ends are returned in order: the smaller first, so that the larger of two radii is the outer one and a reversed scale still makes a sector.
        ///     </para>
        /// </remarks>
        /// <param name="channel">The <c>theta</c> or <c>radius</c> channel, or null.</param>
        /// <param name="mainConstant">The mark property of the main end (<c>theta</c>, <c>radius</c>), or null.</param>
        /// <param name="secondConstant">The mark property of the second end (<c>theta2</c>, <c>radius2</c>), or null.</param>
        /// <param name="maximum">The end that an absent channel runs to: <c>2 * PI</c> for an angle, half of the smaller side of the data rectangle for a radius.</param>
        /// <param name="row">The row.</param>
        /// <returns>The smaller and the larger end; null when the row is invalid.</returns>
        private (double Start, double End)? PolarSpan(Channel? channel, double? mainConstant, double? secondConstant, double maximum, int row)
        {
            var span = RawPolarSpan(channel, mainConstant, secondConstant, maximum, row);
            if (span is not { } ends || !double.IsFinite(ends.Start) || !double.IsFinite(ends.End))
            {
                return null;
            }

            return ends;
        }

        /// <summary>
        ///     Works out the ends of the span of <see cref="PolarSpan"/> before they are checked.
        /// </summary>
        /// <param name="channel">The <c>theta</c> or <c>radius</c> channel, or null.</param>
        /// <param name="mainConstant">The mark property of the main end, or null.</param>
        /// <param name="secondConstant">The mark property of the second end, or null.</param>
        /// <param name="maximum">The end that an absent channel runs to.</param>
        /// <param name="row">The row.</param>
        /// <returns>The smaller and the larger end; null when the row is invalid.</returns>
        private (double Start, double End)? RawPolarSpan(Channel? channel, double? mainConstant, double? secondConstant, double maximum, int row)
        {
            if (channel is null)
            {
                return Ordered(mainConstant ?? maximum, secondConstant ?? 0);
            }

            if (channel.Model is not { } model)
            {
                var second = channel.SecondValue is { } secondValue ? ConstantPosition(secondValue, maximum) : null;
                return Ordered(channel.Value?.AsNumber() ?? mainConstant ?? maximum, second ?? secondConstant ?? 0);
            }

            if (model.Kind is ScaleKind.Band or ScaleKind.Point)
            {
                return BandSpan(channel, model, new Sizing(null, null, null), row) is { } band ? (band.Start, band.Start + band.Length) : null;
            }

            if (channel.IsStacked || channel.HasRange)
            {
                // A stack runs from its lower to its upper end, a range from theta (or radius) to theta2 (or radius2)
                return model.MapCenter(channel.Read(_table, row)) is { } high && SecondEnd(channel, model, maximum, row) is { } low ? Ordered(high, low) : null;
            }

            return Center(channel, model, row) is { } main ? Ordered(main, secondConstant ?? ZeroOrMin(model)) : null;
        }

        /// <summary>
        ///     Puts the two ends of a span in order.
        /// </summary>
        /// <param name="first">One end.</param>
        /// <param name="second">The other end.</param>
        /// <returns>The smaller end and the larger end.</returns>
        private static (double Start, double End) Ordered(double first, double second) =>
            first <= second ? (first, second) : (second, first);

        /// <summary>
        ///     Applies the paint and the stroke properties of the mark to an arc.
        /// </summary>
        /// <param name="item">The arc.</param>
        /// <param name="paint">The paint of the row.</param>
        /// <returns>The styled arc.</returns>
        private SceneArc Style(SceneArc item, Paint paint) => item with
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
        ///     Gets the value of the <c>theta</c> or <c>radius</c> channel of a text mark for a row: the angle in radians or the radius in pixels.
        /// </summary>
        /// <remarks>
        ///     A stacked channel is placed inside its slice (Vega-Lite's <c>positionRef</c>): the scale of <c>(1 - bandPosition) * start + bandPosition * end</c>, with the
        ///     <c>bandPosition</c> of the field definition, which is 0.5 (the middle of the slice) when the definition has none, so that the labels of the slices of an arc mark sit in the middle of them.
        /// </remarks>
        /// <param name="channel">The stacked channel.</param>
        /// <param name="model">The scale of the channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The value; null when the row has no valid stack ends.</returns>
        private double? StackedPolarValue(Channel channel, ScaleModel model, int row)
        {
            if (!channel.Read(_table, row).TryGetQuantitative(out var end) || !_table.GetValue(row, channel.Field2Index).TryGetQuantitative(out var start))
            {
                return null;
            }

            var position = channel.BandPosition ?? 0.5;
            return model.Map(DataValue.FromNumber(((1 - position) * start) + (position * end)));
        }
    }
}

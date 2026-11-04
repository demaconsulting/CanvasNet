// Portions derived from Vega-Lite (BSD-3-Clause): nested offset scales; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The offset channels of a mark: <c>xOffset</c> and <c>yOffset</c> place a mark inside the band of its <c>x</c> or <c>y</c> position (a partial file of the mark builder unit).
/// </content>
/// <remarks>
///     <para>
///         <b>Where the rules come from.</b> Vega-Lite's <c>mark/encode/offset.ts</c>, <c>position-point.ts</c> and <c>position-rect.ts</c>: with an offset the position is read at the start of its band, and
///         the offset scale (a nested scale whose range is the bandwidth of the position scale, see <c>ScaleResolver</c>) places the mark inside it. A point-like mark sits at the position plus the
///         center of its offset band (the offset point, or the offset value of a linear or time offset); a bar, rect, tick or rule spans the band of the offset scale, which is its default size, so a
///         grouped bar chart has bars of the width of the offset band and none between the bars of a group; with an explicit size the mark is centered in its offset band.
///     </para>
/// </remarks>
internal sealed partial class MarkBuilder
{
    /// <summary>
    ///     The offset scale of a position channel and how it reads the offset value of a row.
    /// </summary>
    private sealed class ChannelOffset
    {
        /// <summary>
        ///     Initializes the offset of a channel.
        /// </summary>
        /// <param name="reader">The offset channel, whose model is the offset scale for the bandwidth of the position scale.</param>
        public ChannelOffset(Channel reader) => Reader = reader;

        /// <summary>Gets the offset channel.</summary>
        public Channel Reader { get; }

        /// <summary>Gets the offset scale model, whose range is the bandwidth of the band of the position scale, or the share of the time unit of the position that the offset occupies.</summary>
        public ScaleModel Model => Reader.Model!;

        /// <summary>
        ///     Gets where the band (or the point) of the offset of a row starts inside the band of the position.
        /// </summary>
        /// <param name="table">The data.</param>
        /// <param name="row">The row.</param>
        /// <returns>The distance in pixels; null when the offset value of the row is invalid (the row is not drawn).</returns>
        public double? Start(DataTable table, int row) => Model.Map(Reader.Read(table, row));

        /// <summary>
        ///     Gets where the center of the offset of a row is inside the band of the position.
        /// </summary>
        /// <param name="table">The data.</param>
        /// <param name="row">The row.</param>
        /// <returns>The distance in pixels (the middle of a band, the position of a point or of a linear or time value); null when the offset value of the row is invalid.</returns>
        public double? Center(DataTable table, int row) => Model.MapCenter(Reader.Read(table, row));
    }

    /// <content>
    ///     The offsets of a run.
    /// </content>
    private sealed partial class Run
    {
        /// <summary>
        ///     Gives the <c>x</c> and <c>y</c> channels the offset scales of <c>xOffset</c> and <c>yOffset</c> that were resolved for the view.
        /// </summary>
        /// <remarks>
        ///     The offset scale spans the bandwidth of the band scale of its position, so its model is created for that extent. The position of a time unit is a time scale: the offset
        ///     scale spans a share of the length that one step of the smallest part of the unit has on that scale (<see cref="TimeUnitOffsetRange"/>). A channel without a band scale or
        ///     time unit, or without an offset scale (the offset was reported and not resolved), is left as it is.
        /// </remarks>
        /// <param name="encoding">The encoding object, or null.</param>
        private void AttachOffsets(SpecNode? encoding)
        {
            foreach (var position in new[] { "x", "y" })
            {
                var name = position + "Offset";
                if (encoding?[name] is not { IsObject: true } node || Get(position) is not { Model: { } parentModel } parent || _scales[name] is not { } scale)
                {
                    continue;
                }

                if (parentModel.Kind == ScaleKind.Band)
                {
                    parent.Offset = new ChannelOffset(new Channel(node, scale, scale.CreateModel(parentModel.Bandwidth), _table));
                }
                else if (parentModel.Kind is ScaleKind.Time or ScaleKind.Utc && TimeUnitOffsetRange(parent, parentModel) is { } range)
                {
                    parent.Offset = new ChannelOffset(new Channel(node, scale, scale.CreateModel(0, range), _table));
                }
            }
        }

        /// <summary>
        ///     Works out the default range of the offset scale of a position with a time unit (Vega-Lite's <c>getOffsetRange</c>): the share of the unit that the offsets occupy, in pixels from the start of the unit.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The length of the unit is that of one step of its smallest part from 2001-01-01 (<c>durationExpr</c>) measured on the position scale, so it is the same for every unit of a chart (a month
        ///         is the 31 days of January) and is negative on a <c>y</c> scale. With the nested offset padding <c>p</c> (<c>bandWithNestedOffsetPaddingInner</c>, 0.2 by default) and the band position <c>b</c>
        ///         of the position (<c>bandPosition</c>, else the <c>timeUnitBandPosition</c> of the mark) the range is <c>[(p / 2 + b - 0.5) * length, (1 - p / 2 + b - 0.5) * length]</c>; without padding it is
        ///         <c>[0, length]</c> and the band position does not apply.
        ///     </para>
        ///     <para>
        ///         Vega-Lite has no band position for a mark that has no <c>timeUnitBandPosition</c> (a point, a line, a text) and computes a range of NaN; this port uses the middle of the unit (0.5) instead.
        ///     </para>
        /// </remarks>
        /// <param name="parent">The position channel, whose scale holds the time unit.</param>
        /// <param name="model">The time scale of the position.</param>
        /// <returns>The range in pixels; null when the unit has no duration or the scale has no finite length for it (the offset is then not drawn).</returns>
        private (double Start, double End)? TimeUnitOffsetRange(Channel parent, ScaleModel model)
        {
            if (parent.Scale is not { TimeUnit: { } unit } resolved || unit.ReferenceSpan(resolved.Calendar ?? TimeCalendar.Utc) is not { } span)
            {
                return null;
            }

            var length = model.Map(span.End) - model.Map(span.Start);
            if (!double.IsFinite(length))
            {
                return null;
            }

            var padding = _owner._config.GetNumber("scale.bandWithNestedOffsetPaddingInner") ?? 0.2;
            if (padding == 0)
            {
                return (0, length);
            }

            var position = (parent.BandPosition ?? Prop("timeUnitBandPosition")?.AsNumber() ?? 0.5) - 0.5;
            return ((padding / 2 + position) * length, (1 - (padding / 2) + position) * length);
        }

        /// <summary>
        ///     Gets the position of a mark on a channel that has an offset: the start of its band plus the center of its offset.
        /// </summary>
        /// <param name="channel">The position channel.</param>
        /// <param name="model">The band scale of the channel.</param>
        /// <param name="offset">The offset of the channel.</param>
        /// <param name="row">The row.</param>
        /// <returns>The position, or null when the row is invalid.</returns>
        private double? OffsetCenter(Channel channel, ScaleModel model, ChannelOffset offset, int row) =>
            model.Map(channel.Read(_table, row)) is { } start && offset.Center(_table, row) is { } inside ? start + inside : null;
    }
}

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

        /// <summary>Gets the offset scale model, whose range is the bandwidth of the band of the position scale.</summary>
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
        ///     The offset scale spans the bandwidth of the band scale of its position, so its model is created for that extent. A channel without a band scale or without an offset scale
        ///     (the offset was reported and not resolved) is left as it is.
        /// </remarks>
        /// <param name="encoding">The encoding object, or null.</param>
        private void AttachOffsets(SpecNode? encoding)
        {
            foreach (var position in new[] { "x", "y" })
            {
                var name = position + "Offset";
                if (encoding?[name] is { IsObject: true } node
                    && Get(position) is { Model: { Kind: ScaleKind.Band } parentModel } parent
                    && _scales[name] is { } scale)
                {
                    parent.Offset = new ChannelOffset(new Channel(node, scale, scale.CreateModel(parentModel.Bandwidth), _table));
                }
            }
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

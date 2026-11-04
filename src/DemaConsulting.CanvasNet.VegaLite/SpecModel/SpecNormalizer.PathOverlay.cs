// Portions derived from Vega-Lite (BSD-3-Clause): the path overlay normalizer (getPointOverlay); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     The rules of Vega-Lite's path overlay normalizer that more than one stage needs: whether a mark has a point overlay and
///     which channels the overlay points draw.
/// </content>
/// <remarks>
///     <para>
///         Vega-Lite splits a <c>line</c>, <c>area</c>, <c>trail</c> or <c>rule</c> that has an overlay into layers: the path (the mark without its <c>point</c> and <c>line</c>,
///         with every channel but <c>shape</c>) and the overlay <c>point</c> (every channel but <c>x2</c> and <c>y2</c>). The <c>shape</c> channel is dropped from the path layer
///         <i>before</i> the channels are checked against the mark, so it raises no warning there; it is also what <c>getPointOverlay</c> reads to switch the overlay on
///         when the mark definition does not say (<c>point</c> is missing) and the configuration does not (<c>config.&lt;mark&gt;.point</c>).
///     </para>
///     <para>
///         Every other channel goes to both layers, so each is checked against the path mark (which warns about a channel it does not draw) and drawn by the overlay points
///         when they draw it: the scales of such a channel are built for the overlay points even though the path mark has no use for them.
///     </para>
/// </remarks>
internal static partial class SpecNormalizer
{
    /// <summary>The marks that have a point overlay (<c>area</c> also has a line overlay).</summary>
    private static readonly HashSet<string> PointOverlayMarks = ["line", "area", "trail", "rule"];

    /// <summary>
    ///     Tells whether a configuration value is truthy as JavaScript sees it.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns><see langword="true"/> for true, a number other than zero, a text that is not empty, an object and an array.</returns>
    public static bool IsTruthyConfig(ConfigNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Kind switch
        {
            ConfigNodeKind.Boolean => node.BoolValue,
            ConfigNodeKind.Number => node.NumberValue != 0 && !double.IsNaN(node.NumberValue),
            ConfigNodeKind.String => node.StringValue!.Length > 0,
            ConfigNodeKind.Array or ConfigNodeKind.Object => true,
            _ => false,
        };
    }

    /// <summary>
    ///     Tells whether the encoding has a <c>shape</c> channel, which switches the point overlay of a path mark on (the <c>encoding.shape</c> of <c>getPointOverlay</c>).
    /// </summary>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <returns><see langword="true"/> when the encoding has a <c>shape</c> definition.</returns>
    public static bool HasShapeChannel(SpecNode? encoding) => encoding?["shape"] is { IsObject: true };

    /// <summary>
    ///     Tells whether a mark has a point overlay (Vega-Lite's <c>getPointOverlay</c>).
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="mark">The mark definition object, or null for a mark given by its name.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <param name="configPoint">The <c>point</c> of <c>config.&lt;mark&gt;</c>, or null.</param>
    /// <returns>
    ///     <see langword="true"/> for a <c>line</c>, <c>area</c>, <c>trail</c> or <c>rule</c> whose <c>point</c> is <c>true</c>, an object or <c>"transparent"</c>, or is missing while
    ///     <paramref name="configPoint"/> is truthy or the encoding has a <c>shape</c>; <c>false</c> and null switch the overlay off.
    /// </returns>
    public static bool HasPointOverlay(string markType, SpecNode? mark, SpecNode? encoding, ConfigNode? configPoint)
    {
        ArgumentNullException.ThrowIfNull(markType);
        if (!PointOverlayMarks.Contains(markType))
        {
            return false;
        }

        if (mark?["point"] is { } point)
        {
            return point is { IsString: true, StringValue: "transparent" } || IsTruthyConfig(ConfigNode.FromSpec(point));
        }

        return (configPoint is not null && IsTruthyConfig(configPoint)) || HasShapeChannel(encoding);
    }
}

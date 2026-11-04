// Portions derived from Vega-Lite (BSD-3-Clause): the path overlay normalizer (getPointOverlay); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     The rules of Vega-Lite's path overlay normalizer that more than one stage needs: whether a mark has a point overlay or a line overlay (an area) and
///     which channels the overlays draw.
/// </content>
/// <remarks>
///     <para>
///         Vega-Lite splits a <c>line</c>, <c>area</c>, <c>trail</c> or <c>rule</c> that has an overlay into layers: the path (the mark without its <c>point</c> and <c>line</c>,
///         with every channel but <c>shape</c>), the line overlay of an area and the overlay <c>point</c> (each with every channel but <c>x2</c> and <c>y2</c>). The <c>shape</c> channel is dropped from the path layer
///         <i>before</i> the channels are checked against the mark, so it raises no warning there; it is also what <c>getPointOverlay</c> reads to switch the overlay on
///         when the mark definition does not say (<c>point</c> is missing) and the configuration does not (<c>config.&lt;mark&gt;.point</c>).
///     </para>
///     <para>
///         Every other channel goes to all the layers, so each is checked against the path mark (which warns about a channel it does not draw) and drawn by the overlay marks
///         when they draw it: the scales of such a channel are built for the overlays even though the path mark has no use for them (the <c>size</c> and the <c>strokeDash</c> of an area
///         are the width and the dash of its line overlay, the <c>shape</c>, <c>angle</c> and <c>size</c> those of its points).
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
    ///     Works out the point overlay of a mark (Vega-Lite's <c>getPointOverlay</c> and the overlay mark of <c>PathOverlayNormalizer.run</c>).
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="mark">The mark definition object, or null for a mark given by its name.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <param name="configPoint">The <c>point</c> of <c>config.&lt;mark&gt;</c>, or null.</param>
    /// <returns>
    ///     The mark definition of the overlay: a <c>point</c> mark with <c>opacity: 1</c>, <c>filled: true</c> and the <c>clip</c> of the mark, whatever the overlay object adds to or replaces
    ///     (<c>"transparent"</c> is <c>{"opacity": 0}</c>). It exists for a <c>line</c>, <c>area</c>, <c>trail</c> or <c>rule</c> whose <c>point</c> is <c>true</c>, an object or <c>"transparent"</c>, or is missing while
    ///     <paramref name="configPoint"/> is truthy or the encoding has a <c>shape</c>; <c>false</c> and null switch the overlay off. Null when the mark has no point overlay.
    /// </returns>
    public static ConfigNode? PointOverlayDefinition(string markType, SpecNode? mark, SpecNode? encoding, ConfigNode? configPoint)
    {
        ArgumentNullException.ThrowIfNull(markType);
        if (!PointOverlayMarks.Contains(markType))
        {
            return null;
        }

        ConfigNode overlay;
        if (mark?["point"] is { } written)
        {
            var point = ConfigNode.FromSpec(written);
            if (point is { Kind: ConfigNodeKind.String, StringValue: "transparent" })
            {
                overlay = ConfigNode.Create(new Dictionary<string, ConfigNode> { ["opacity"] = ConfigNode.FromNumber(0) });
            }
            else if (IsTruthyConfig(point))
            {
                overlay = point.IsObject ? point : ConfigNode.EmptyObject;
            }
            else
            {
                return null;
            }
        }
        else if (configPoint is not null && IsTruthyConfig(configPoint))
        {
            overlay = configPoint.IsObject ? configPoint : ConfigNode.EmptyObject;
        }
        else if (HasShapeChannel(encoding))
        {
            // An encoding with a shape channel enables the overlay when neither the mark nor the configuration says anything about it
            overlay = ConfigNode.EmptyObject;
        }
        else
        {
            return null;
        }

        var defaults = new Dictionary<string, ConfigNode>(StringComparer.Ordinal)
        {
            ["opacity"] = ConfigNode.FromNumber(1),
            ["filled"] = ConfigNode.FromBoolean(true),
        };
        if (mark?["clip"] is { } clip)
        {
            defaults["clip"] = ConfigNode.FromSpec(clip);
        }

        return ConfigNode.Merge(ConfigNode.Create(defaults), overlay);
    }

    /// <summary>
    ///     Works out the line overlay of an area (Vega-Lite's <c>getLineOverlay</c> and the overlay mark of <c>PathOverlayNormalizer.run</c>).
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="mark">The mark definition object, or null for a mark given by its name.</param>
    /// <param name="configLine">The <c>line</c> of <c>config.area</c>, or null.</param>
    /// <returns>
    ///     The mark definition of the overlay: a <c>line</c> mark with the <c>clip</c>, <c>interpolate</c> and <c>tension</c> of the area, whatever the overlay object adds to or replaces. It exists for an
    ///     <c>area</c> whose <c>line</c> is <c>true</c> or an object, or is missing while <paramref name="configLine"/> is truthy; <c>false</c> and null switch it off. Null when the mark has no line overlay.
    /// </returns>
    public static ConfigNode? LineOverlayDefinition(string markType, SpecNode? mark, ConfigNode? configLine)
    {
        ArgumentNullException.ThrowIfNull(markType);
        if (markType != "area")
        {
            return null;
        }

        ConfigNode overlay;
        if (mark?["line"] is { } written)
        {
            var line = ConfigNode.FromSpec(written);
            if (!IsTruthyConfig(line))
            {
                return null;
            }

            overlay = line.IsObject ? line : ConfigNode.EmptyObject;
        }
        else if (configLine is not null && IsTruthyConfig(configLine))
        {
            overlay = configLine.IsObject ? configLine : ConfigNode.EmptyObject;
        }
        else
        {
            return null;
        }

        var defaults = new Dictionary<string, ConfigNode>(StringComparer.Ordinal);
        foreach (var name in new[] { "clip", "interpolate", "tension" })
        {
            if (mark?[name] is { } inherited)
            {
                defaults[name] = ConfigNode.FromSpec(inherited);
            }
        }

        return ConfigNode.Merge(ConfigNode.Create(defaults), overlay);
    }

    /// <summary>
    ///     Tells whether a mark has a point overlay (see <see cref="PointOverlayDefinition"/>).
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="mark">The mark definition object, or null for a mark given by its name.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <param name="configPoint">The <c>point</c> of <c>config.&lt;mark&gt;</c>, or null.</param>
    /// <returns><see langword="true"/> when the mark has a point overlay.</returns>
    public static bool HasPointOverlay(string markType, SpecNode? mark, SpecNode? encoding, ConfigNode? configPoint) =>
        PointOverlayDefinition(markType, mark, encoding, configPoint) is not null;

    /// <summary>
    ///     Tells whether an area has a line overlay (see <see cref="LineOverlayDefinition"/>).
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="mark">The mark definition object, or null for a mark given by its name.</param>
    /// <param name="configLine">The <c>line</c> of <c>config.area</c>, or null.</param>
    /// <returns><see langword="true"/> when the mark has a line overlay.</returns>
    public static bool HasLineOverlay(string markType, SpecNode? mark, ConfigNode? configLine) =>
        LineOverlayDefinition(markType, mark, configLine) is not null;

    /// <summary>
    ///     Tells whether the overlays of a path mark draw a channel: the overlay points draw what a <c>point</c> mark draws, and the line overlay of an area what a <c>line</c> draws
    ///     (Vega-Lite gives both overlay layers the whole encoding, so each keeps what its mark supports).
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="pointOverlay">Whether the mark has a point overlay.</param>
    /// <param name="lineOverlay">Whether the mark has a line overlay.</param>
    /// <returns><see langword="true"/> when an overlay draws the channel.</returns>
    public static bool OverlayDrawsChannel(string channel, bool pointOverlay, bool lineOverlay) =>
        (pointOverlay && ScaleResolver.ChannelIsDrawn(channel, "point")) || (lineOverlay && ScaleResolver.ChannelIsDrawn(channel, "line"));
}

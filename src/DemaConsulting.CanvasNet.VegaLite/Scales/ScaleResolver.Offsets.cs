// Portions derived from Vega-Lite (BSD-3-Clause): nested offset scales; see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Scales;

/// <summary>
///     What the renderer does with the <c>xOffset</c> or <c>yOffset</c> channel of a view.
/// </summary>
internal enum OffsetDecision
{
    /// <summary>The offset has a scale of its own, nested in the band of its position scale.</summary>
    Supported,

    /// <summary>The position channel is missing, is not discrete (nominal or ordinal) or has a time unit: the offset is not drawn and the position is drawn without it.</summary>
    NoDiscreteParent,

    /// <summary>A continuous offset of a <c>bar</c> or <c>area</c>, which Vega-Lite draws as a range from the start of the band: the offset is not drawn.</summary>
    Ranged,
}

/// <content>
///     The nested offset scales of <c>xOffset</c> and <c>yOffset</c> (a partial file of the scale resolver unit).
/// </content>
/// <remarks>
///     <para>
///         <b>Rules.</b> They are Vega-Lite 6's (<c>channelHasNestedOffsetScale</c>, <c>scale/type.ts</c>, <c>scale/properties.ts</c>, <c>scale/range.ts</c>): an offset needs a position channel that is a
///         discrete field or datum (that scale is then a band scale for every mark, with the nested offset paddings, 0.2 inside and outside by default); the offset scale is a band scale for the marks that
///         fill their band (bar, rect, image, rule, tick) and a point scale for the others when the field is discrete, a linear or time scale otherwise. A band offset has the offset band paddings of the
///         configuration (none by default), a point offset an outer padding of half a step. The offset scale spans the bandwidth of its position scale; with a step size the step of the position scale is the
///         step of the offset times the space of the offset domain over one minus the inner padding of the position (<c>for: "offset"</c>, the default for a discrete offset; <c>for: "position"</c> leaves
///         the step on the position scale).
///     </para>
///     <para>
///         <b>Not drawn.</b> An offset whose position is missing, continuous or has a time unit (<see cref="OffsetDecision.NoDiscreteParent"/>) and a continuous offset of a bar or area
///         (<see cref="OffsetDecision.Ranged"/>) have no scale; the feature catalog reports them.
///     </para>
/// </remarks>
internal static partial class ScaleResolver
{
    /// <summary>
    ///     Decides whether an offset channel is drawn.
    /// </summary>
    /// <param name="offsetChannel">The offset channel: <c>xOffset</c> or <c>yOffset</c>.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <param name="markType">The mark type.</param>
    /// <returns>The decision; <see cref="OffsetDecision.Supported"/> also when the encoding has no field or datum for the offset (there is nothing to report).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="offsetChannel"/> or <paramref name="markType"/> is null.</exception>
    public static OffsetDecision DecideOffset(string offsetChannel, SpecNode? encoding, string markType)
    {
        ArgumentNullException.ThrowIfNull(offsetChannel);
        ArgumentNullException.ThrowIfNull(markType);
        if (encoding?[offsetChannel] is not { IsObject: true } offset || !HasFieldOrDatum(offset))
        {
            return OffsetDecision.Supported;
        }

        var position = offsetChannel == "xOffset" ? "x" : "y";
        if (encoding[position] is not { IsObject: true } parent
            || !HasFieldOrDatum(parent)
            || ChannelDefinitions.TypeOf(parent, position, encoding) is not ("nominal" or "ordinal")
            || parent["timeUnit"] is { IsNull: false })
        {
            return OffsetDecision.NoDiscreteParent;
        }

        var continuous = ChannelDefinitions.TypeOf(offset, offsetChannel, encoding) is "quantitative" or "temporal";
        return continuous && markType is "bar" or "area" ? OffsetDecision.Ranged : OffsetDecision.Supported;
    }

    /// <summary>
    ///     Tells whether the band of a position channel holds an offset scale (Vega-Lite's <c>channelHasNestedOffsetScale</c>, for an offset that is drawn).
    /// </summary>
    /// <param name="channel">The channel; only <c>x</c> and <c>y</c> can hold one.</param>
    /// <param name="encoding">The encoding object, or null.</param>
    /// <param name="markType">The mark type (a continuous offset of a bar or area is not drawn).</param>
    /// <returns><see langword="true"/> when the position is a discrete field or datum and its offset channel has a field or datum that is drawn.</returns>
    internal static bool HasNestedOffset(string channel, SpecNode? encoding, string markType)
    {
        if (channel is not ("x" or "y") || encoding is null)
        {
            return false;
        }

        var offsetChannel = channel + "Offset";
        return encoding[offsetChannel] is { IsObject: true } offset
            && HasFieldOrDatum(offset)
            && DecideOffset(offsetChannel, encoding, markType) == OffsetDecision.Supported;
    }

    /// <summary>
    ///     Tells whether a channel definition has a field, an aggregate that counts, or a datum.
    /// </summary>
    /// <param name="definition">The channel definition.</param>
    /// <returns><see langword="true"/> when the channel reads data.</returns>
    private static bool HasFieldOrDatum(SpecNode definition) => StackRules.IsFieldDef(definition) || definition["datum"] is not null;

    /// <summary>
    ///     Gets what the step of a size is for: the <c>for</c> of the <c>step</c> object of the specification, or of the configured discrete size.
    /// </summary>
    /// <param name="size">The <c>width</c> or <c>height</c> node of the view, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="name">The property name: <c>width</c> or <c>height</c>.</param>
    /// <returns><c>position</c>, <c>offset</c> or null when the size does not say.</returns>
    private static string? StepForOf(SpecNode? size, ResolvedConfig config, string name)
    {
        if (size is not null)
        {
            return size.IsObject ? size["for"]?.AsString() : null;
        }

        var configured = config.Get("view." + name) ?? config.Get("view.discrete" + (name == "width" ? "Width" : "Height"));
        return configured is { IsObject: true } ? configured["for"]?.AsString() : null;
    }

    /// <summary>
    ///     Sizes the position scales that hold a discrete offset scale: their step becomes the step of the offset scale times the space of its domain over the free part of the band
    ///     (Vega-Lite's <c>getPositionStep</c> of <c>for: "offset"</c>, the default when the offset is discrete).
    /// </summary>
    /// <remarks>
    ///     The step of a position scale is the specification's step before this runs, so it must run exactly once for a set of scales (the offset domains it reads are final, which for the layers of a layered
    ///     view means after the shared domains are merged).
    /// </remarks>
    /// <param name="scales">The scales by channel; the position entries that are sized by a step are replaced.</param>
    /// <param name="context">The facts of the view.</param>
    /// <returns><paramref name="scales"/>.</returns>
    private static Dictionary<string, ResolvedScale> ApplyNestedOffsets(Dictionary<string, ResolvedScale> scales, Context context)
    {
        foreach (var position in new[] { "x", "y" })
        {
            if (!scales.TryGetValue(position, out var parent) || !scales.TryGetValue(position + "Offset", out var offset)
                || parent is not { Kind: ScaleKind.Band, Step: { } step }
                || offset.Kind is not (ScaleKind.Band or ScaleKind.Point))
            {
                continue;
            }

            var stepFor = (position == "x" ? context.WidthStepFor : context.HeightStepFor) ?? "offset";
            var free = 1 - parent.PaddingInner;
            if (stepFor != "offset" || free <= 0)
            {
                continue;
            }

            // A point scale is a band scale with the inner padding 1
            var inner = offset.Kind == ScaleKind.Point ? 1 : offset.PaddingInner;
            scales[position] = parent with { Step = step * ScaleModel.BandSpace(offset.DiscreteDomain.Count, inner, offset.PaddingOuter) / free };
        }

        return scales;
    }
}

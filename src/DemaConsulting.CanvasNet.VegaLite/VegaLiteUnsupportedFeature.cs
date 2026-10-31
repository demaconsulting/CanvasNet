namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     A construct of a Vega-Lite specification that is recognized but not supported (yet), reported instead of
///     being skipped silently so that a chart is never quietly drawn differently from what its author described.
/// </summary>
/// <remarks>
///     <para>
///         By default the feature is delivered to <see cref="VegaLiteRenderOptions.UnsupportedFeatureHandler"/> and
///         rendering continues with the construct left out; callers that must never accept an approximate result
///         set <see cref="VegaLiteRenderOptions.ThrowOnUnsupportedFeature"/> to get a
///         <see cref="VegaLiteUnsupportedFeatureException"/> at the first such construct instead.
///     </para>
///     <para>
///         <see cref="Feature"/> is a stable identifier (for example <c>vl-mark-geoshape</c> or
///         <c>vl-data-remote-url</c>) that tools may match on; the message is human-readable and may change. The
///         record is immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Feature">The stable <c>vl-*</c> identifier of the unsupported feature.</param>
/// <param name="Message">The description of what is not supported and, where useful, what to do instead.</param>
/// <param name="Location">The location of the construct in the specification; <c>default</c> when unknown.</param>
public sealed record VegaLiteUnsupportedFeature(string Feature, string Message, VegaLiteLocation Location)
{
    /// <summary>
    ///     Formats the report for display.
    /// </summary>
    /// <returns><c>{location}: {feature}: {message}</c>, or <c>{feature}: {message}</c> when the location is unknown.</returns>
    public override string ToString() => Location.IsKnown ? $"{Location}: {Feature}: {Message}" : $"{Feature}: {Message}";
}

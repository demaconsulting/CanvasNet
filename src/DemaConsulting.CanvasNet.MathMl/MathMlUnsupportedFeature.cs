namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     A construct of a MathML equation that is recognized but not supported (yet), reported instead of being skipped
///     silently so that an equation is never quietly drawn differently from what its author wrote.
/// </summary>
/// <remarks>
///     <para>
///         By default the feature is delivered to <see cref="MathMlRenderOptions.UnsupportedFeatureHandler"/> and
///         rendering continues with the documented fallback (an element that is not drawn is laid out as an
///         <c>mrow</c>, as MathML Core lays out an unknown element; an attribute that is not supported is ignored);
///         callers that must never accept an approximate result set
///         <see cref="MathMlRenderOptions.ThrowOnUnsupportedFeature"/> to get a
///         <see cref="MathMlUnsupportedFeatureException"/> at the first such construct instead.
///     </para>
///     <para>
///         <see cref="Feature"/> is a stable identifier (for example <c>mathml-element-unsupported</c>, see
///         <see cref="MathMlDiagnosticCodes"/>) that tools may match on; the message is human-readable and may change.
///         The record is immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Feature">The stable <c>mathml-*</c> identifier of the unsupported feature.</param>
/// <param name="Message">The description of what is not supported.</param>
/// <param name="Location">The location of the construct in the original text; <c>default</c> when unknown.</param>
public sealed record MathMlUnsupportedFeature(string Feature, string Message, MathMlLocation Location)
{
    /// <summary>
    ///     Gets the suggested way forward (what is supported instead), or <see langword="null"/> when there is none.
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    ///     Formats the report for display.
    /// </summary>
    /// <returns><c>{location}: {feature}: {message}</c>, or <c>{feature}: {message}</c> when the location is unknown.</returns>
    public override string ToString() => Location.IsKnown ? $"{Location}: {Feature}: {Message}" : $"{Feature}: {Message}";
}

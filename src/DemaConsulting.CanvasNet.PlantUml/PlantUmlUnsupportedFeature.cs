namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     A construct of a PlantUML diagram that is recognized but not supported (yet), reported instead of being skipped
///     silently so that a diagram is never quietly drawn differently from what its author wrote.
/// </summary>
/// <remarks>
///     <para>
///         By default the feature is delivered to <see cref="PlantUmlRenderOptions.UnsupportedFeatureHandler"/> and
///         rendering continues with the documented fallback (for a diagram type that is not supported, an empty
///         transparent picture); callers that must never accept an approximate result set
///         <see cref="PlantUmlRenderOptions.ThrowOnUnsupportedFeature"/> to get a
///         <see cref="PlantUmlUnsupportedFeatureException"/> at the first such construct instead.
///     </para>
///     <para>
///         <see cref="Feature"/> is a stable identifier (for example <c>plantuml-diagram-unsupported</c>, see
///         <see cref="PlantUmlDiagnosticCodes"/>) that tools may match on; the message is human-readable and may change.
///         The record is immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Feature">The stable <c>plantuml-*</c> identifier of the unsupported feature.</param>
/// <param name="Message">The description of what is not supported.</param>
/// <param name="Location">The location of the construct in the original text; <c>default</c> when unknown.</param>
public sealed record PlantUmlUnsupportedFeature(string Feature, string Message, PlantUmlLocation Location)
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

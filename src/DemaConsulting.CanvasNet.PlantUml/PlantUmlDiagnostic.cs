namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     A warning produced while reading or rendering a PlantUML diagram: something questionable that does not stop
///     rendering, such as a <c>skinparam</c> that is ignored or a <c>!pragma</c> that has no effect.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Code"/> is a stable <c>plantuml-*</c> identifier (see <see cref="PlantUmlDiagnosticCodes"/>) that
///         tools and agents may match on; <see cref="Message"/> is human-readable and may change, and <see cref="Hint"/>,
///         when present, suggests the fix. The message carries no severity prefix: presentation (for example a
///         <c>WARNING:</c> prefix) is left to the consumer.
///     </para>
///     <para>
///         The record is immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Code">The stable <c>plantuml-*</c> identifier of the warning.</param>
/// <param name="Message">The description of the problem, without the location and the code.</param>
/// <param name="Location">The location in the original text that the warning is about; <c>default</c> when unknown.</param>
public sealed record PlantUmlDiagnostic(string Code, string Message, PlantUmlLocation Location)
{
    /// <summary>
    ///     Gets the suggested fix, or <see langword="null"/> when there is none.
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    ///     Formats the diagnostic for display.
    /// </summary>
    /// <returns><c>{location}: {code}: {message}</c>, or <c>{code}: {message}</c> when the location is unknown.</returns>
    public override string ToString() => Location.IsKnown ? $"{Location}: {Code}: {Message}" : $"{Code}: {Message}";
}

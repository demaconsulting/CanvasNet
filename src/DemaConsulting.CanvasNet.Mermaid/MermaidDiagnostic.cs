namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     A warning produced while reading or rendering a Mermaid diagram: something questionable that does not stop
///     rendering, such as a configuration key that Mermaid does not define or a style declaration that is ignored.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Code"/> is a stable <c>mermaid-*</c> identifier (see <see cref="MermaidDiagnosticCodes"/>) that
///         tools and agents may match on; <see cref="Message"/> is human-readable and may change, and <see cref="Hint"/>,
///         when present, suggests the fix. The message carries no severity prefix: presentation (for example a
///         <c>WARNING:</c> prefix) is left to the consumer.
///     </para>
///     <para>
///         The record is immutable and safe for concurrent use.
///     </para>
/// </remarks>
/// <param name="Code">The stable <c>mermaid-*</c> identifier of the warning.</param>
/// <param name="Message">The description of the problem, without the location and the code.</param>
/// <param name="Location">The location in the original text that the warning is about; <c>default</c> when unknown.</param>
public sealed record MermaidDiagnostic(string Code, string Message, MermaidLocation Location)
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

namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     A warning produced while reading or rendering a Vega-Lite specification: something questionable that does
///     not stop rendering, such as an empty inline dataset or a <c>$schema</c> version that is not fully supported.
/// </summary>
/// <remarks>
///     The message starts with a stable identifier of the form <c>vl-*</c> followed by a colon (for example
///     <c>vl-schema-version: ...</c>) so tools can match diagnostics without parsing prose; it carries no severity
///     prefix, because presentation (for example a <c>WARNING:</c> prefix) is left to the consumer. The record is
///     immutable and safe for concurrent use.
/// </remarks>
/// <param name="Message">The identifier and description of the problem, without the location.</param>
/// <param name="Location">The location in the specification that the warning is about; <c>default</c> when unknown.</param>
public sealed record VegaLiteDiagnostic(string Message, VegaLiteLocation Location)
{
    /// <summary>
    ///     Formats the diagnostic for display.
    /// </summary>
    /// <returns><c>{location}: {message}</c>, or the message alone when the location is unknown.</returns>
    public override string ToString() => Location.IsKnown ? $"{Location}: {Message}" : Message;
}

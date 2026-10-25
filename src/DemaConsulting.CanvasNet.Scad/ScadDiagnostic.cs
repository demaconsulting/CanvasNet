namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     A warning produced while evaluating an OpenSCAD-language program: something questionable that does not
///     stop evaluation, such as the use of an unknown name.
/// </summary>
/// <remarks>
///     The message carries no severity prefix; presentation (for example a <c>WARNING:</c> prefix) is left to the
///     consumer. The record is immutable and safe for concurrent use.
/// </remarks>
/// <param name="Message">The description of the problem, without the location.</param>
/// <param name="Location">The location in the source that the warning is about; <c>default</c> when unknown.</param>
public sealed record ScadDiagnostic(string Message, ScadSourceLocation Location)
{
    /// <summary>
    ///     Formats the diagnostic for display.
    /// </summary>
    /// <returns><c>{location}: {message}</c>, with the location rendered using the invariant culture.</returns>
    public override string ToString() => $"{Location}: {Message}";
}

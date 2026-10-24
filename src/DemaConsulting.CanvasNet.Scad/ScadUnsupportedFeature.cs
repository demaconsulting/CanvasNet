namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     A language or geometry feature that is recognized but not supported, reported so that a program is never
///     silently mis-evaluated.
/// </summary>
/// <remarks>
///     The feature identifier is stable and lower-case, of the form <c>scad-function-name</c> or
///     <c>scad-search-form</c>, so callers can match on it. The record is immutable and safe for concurrent use.
/// </remarks>
/// <param name="Feature">The stable feature identifier.</param>
/// <param name="Message">The description of what is not supported, without the location.</param>
/// <param name="Location">The location in the source where the feature is used; <c>default</c> when unknown.</param>
public sealed record ScadUnsupportedFeature(string Feature, string Message, ScadSourceLocation Location)
{
    /// <summary>
    ///     Formats the report for display.
    /// </summary>
    /// <returns><c>{location}: {message} [{feature}]</c>, with the location rendered using the invariant culture.</returns>
    public override string ToString() => $"{Location}: {Message} [{Feature}]";
}

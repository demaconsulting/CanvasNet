// cspell:ignore minkowski multmatrix
namespace DemaConsulting.CanvasNet.Scad;

/// <summary>
///     A language or geometry feature that is recognized but not supported, reported so that a program is never
///     silently mis-evaluated.
/// </summary>
/// <remarks>
///     The feature identifier is stable and lower-case, so callers can match on it. The families are
///     <c>scad-function-name</c> and <c>scad-search-form</c> (evaluation), <c>scad-module-name</c> (a module that is
///     recognized but not supported: <c>text</c>, <c>offset</c>, <c>projection</c>, <c>surface</c>, <c>import</c>,
///     <c>roof</c>, <c>hull</c>, <c>minkowski</c>), <c>scad-modifier-highlight</c> and <c>scad-modifier-background</c>,
///     <c>scad-color-alpha</c>, <c>scad-resize-approximate</c>, <c>scad-multmatrix-projective</c>,
///     <c>scad-polygon-invalid</c>, <c>scad-polyhedron-face-invalid</c>, <c>scad-linear-extrude-scale</c>,
///     <c>scad-linear-extrude-apex-multiple-contours</c>, <c>scad-rotate-extrude-axis-crossing</c>,
///     <c>scad-rotate-extrude-axis-contact</c>, <c>scad-geometry-rejected</c> (geometry), and <c>scad-csg-open-mesh</c>,
///     <c>scad-csg-non-manifold-edge</c>, <c>scad-csg-inconsistent-winding</c>, <c>scad-csg-degenerate-triangle</c>,
///     <c>scad-csg-inverted-mesh</c> and <c>scad-csg-singular-transform</c> (a boolean operand the renderer cannot
///     resolve faithfully). The record is immutable and safe for concurrent use.
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

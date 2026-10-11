namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Describes one recognized-but-unsupported (or only approximated) Visio construct that
///     <see cref="VsdxDocument"/> encountered while resolving and rendering a page - a
///     non-throwing diagnostic delivered to <see cref="VsdxRenderOptions.UnsupportedFeatureHandler"/>
///     (or thrown as a <see cref="VsdxUnsupportedFeatureException"/> when
///     <see cref="VsdxRenderOptions.ThrowOnUnsupportedFeature"/> is set).
/// </summary>
/// <param name="Feature">
///     A short, stable, machine-matchable token identifying the feature (for example
///     <c>"vsdx-geometry-row-NURBSTo"</c> or <c>"vsdx-arrowhead-13"</c>) - the same value
///     <see cref="VsdxUnsupportedFeatureException.Feature"/> carries in strict mode.
/// </param>
/// <param name="Message">A free-text, human-readable description of what was degraded.</param>
public sealed record VsdxUnsupportedFeature(string Feature, string Message);

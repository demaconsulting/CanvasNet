namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx stealth

/// <summary>
///     The recognized <c>&lt;a:headEnd&gt;</c>/<c>&lt;a:tailEnd&gt;</c> arrowhead <c>type</c>
///     values this phase supports (Phase 2 Follow-Up: Connector Shape Rendering) - see
///     <see cref="PptxArrowheadGeometry.Build"/> for how each kind's geometry is built.
/// </summary>
internal enum PptxArrowheadKind
{
    /// <summary>No arrowhead - the schema default, and the result of an unrecognized <c>type</c> value.</summary>
    None,

    /// <summary>A solid, closed, filled triangle - the most common PowerPoint connector arrowhead.</summary>
    Triangle,

    /// <summary>A solid, closed, filled triangle with a concave ("notched") back edge.</summary>
    Stealth,

    /// <summary>A solid, closed, filled diamond (rhombus), centered on the line's own endpoint.</summary>
    Diamond,

    /// <summary>A solid, closed, filled oval (ellipse), centered on the line's own endpoint.</summary>
    Oval,

    /// <summary>An open, unfilled "chevron" - two line segments meeting at the tip, stroked rather than filled.</summary>
    Arrow,
}

/// <summary>
///     A resolved <c>&lt;a:headEnd&gt;</c>/<c>&lt;a:tailEnd&gt;</c> arrowhead style (Phase 2
///     Follow-Up: Connector Shape Rendering): its kind, and its <c>w</c>/<c>len</c> size keys -
///     everything <see cref="PptxArrowheadGeometry.Build"/> needs to build the arrowhead's own
///     local-space geometry.
/// </summary>
/// <param name="Kind">The arrowhead's recognized kind - never <see cref="PptxArrowheadKind.None"/> (see <see cref="PptxDocument.ResolveArrowhead"/>, which returns <see langword="null"/> instead for that case).</param>
/// <param name="WidthKey">
///     The arrowhead's own <c>w</c> attribute value (<c>"sm"</c>/<c>"med"</c>/<c>"lg"</c>), or
///     <c>"med"</c> when omitted - the OOXML schema default. See
///     <see cref="PptxArrowheadGeometry.Build"/> for the fixed, documented size-key-to-proportion
///     mapping this phase uses.
/// </param>
/// <param name="LengthKey">The arrowhead's own <c>len</c> attribute value, resolved the same way as <see cref="WidthKey"/>.</param>
internal sealed record PptxArrowheadStyle(PptxArrowheadKind Kind, string WidthKey, string LengthKey);

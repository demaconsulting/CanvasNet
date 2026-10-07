using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx srgb paintable

/// <summary>
///     A resolved DrawingML paint - the result of <see cref="PptxDocument.ResolveFill"/> resolving
///     a shape's <c>&lt;p:spPr&gt;</c> fill element (or an <c>&lt;a:ln&gt;</c>'s own fill element,
///     via <see cref="PptxDocument.ResolveLineStyle"/>) into a concrete, paintable value.
/// </summary>
/// <remarks>
///     A deliberately <b>closed</b> type hierarchy, mirroring <see cref="Gradient"/>'s own
///     documented rationale: <see cref="PptxPaint"/>'s constructor is
///     <see langword="private protected"/>, so only <see cref="PptxNoFill"/>,
///     <see cref="PptxSolidFill"/>, <see cref="PptxGradientFill"/>, <see cref="PptxImageFill"/>,
///     and <see cref="PptxPatternFill"/> - all declared in this same file/assembly - may derive
///     from it. A later rendering phase that consumes <see cref="PptxPaint"/> pattern-matches
///     exhaustively on exactly these five subtypes.
/// </remarks>
internal abstract record PptxPaint
{
    /// <summary>Restricts this hierarchy to the five subtypes declared in this file - see this type's remarks.</summary>
    private protected PptxPaint()
    {
    }
}

/// <summary>
///     The resolved paint for an explicit <c>&lt;a:noFill/&gt;</c> element: paints nothing.
/// </summary>
internal sealed record PptxNoFill : PptxPaint
{
    /// <summary>The single shared instance - <see cref="PptxNoFill"/> carries no state of its own.</summary>
    internal static readonly PptxNoFill Instance = new();
}

/// <summary>
///     The resolved paint for an <c>&lt;a:solidFill&gt;</c> element: a single concrete
///     <see cref="Rgba32"/> color, already resolved from whichever color-definition element
///     (<c>&lt;a:srgbClr&gt;</c>/<c>&lt;a:schemeClr&gt;</c>/<c>&lt;a:sysClr&gt;</c>) and color
///     transform chain (<c>lumMod</c>/<c>lumOff</c>/<c>shade</c>/<c>tint</c>/<c>alpha</c>) it
///     declared - see <see cref="PptxDocument.ResolveColor"/>.
/// </summary>
/// <param name="Color">The resolved, concrete color.</param>
internal sealed record PptxSolidFill(Rgba32 Color) : PptxPaint;

/// <summary>
///     The resolved paint for an <c>&lt;a:gradFill&gt;</c> element declaring a linear gradient
///     (<c>&lt;a:lin&gt;</c>) - the only gradient kind resolved this phase; a radial or path
///     gradient (<c>&lt;a:path&gt;</c>) throws <see cref="PptxUnsupportedFeatureException"/> - see
///     <see cref="PptxDocument.ResolveGradientFill"/>.
/// </summary>
/// <param name="Gradient">
///     The resolved core <see cref="Drawing.Gradient"/> (always a <see cref="LinearGradient"/>
///     this phase), already positioned in the owning shape's own local geometry coordinate space.
/// </param>
internal sealed record PptxGradientFill(Gradient Gradient) : PptxPaint;

/// <summary>
///     The resolved paint for an <c>&lt;a:blipFill&gt;</c> element used as a shape/paragraph
///     <em>fill</em> (as distinct from a <c>&lt;p:pic&gt;</c> picture <em>shape</em>, which is
///     resolved by the separate <see cref="PptxDocument.ResolvePictureSurface"/>/
///     <see cref="PptxDocument.PaintPicture"/> pipeline) - a decoded raster image, repeated
///     (<c>&lt;a:tile&gt;</c>) or stretched (<c>&lt;a:stretch&gt;</c>, optionally cropped by
///     <c>&lt;a:fillRect&gt;</c>) across the owning shape's own fill region. See
///     <see cref="PptxDocument.ResolveImageFillTransform"/> for how <see cref="ImageToLocalTransform"/>
///     is derived from either sub-element.
/// </summary>
/// <param name="Image">
///     The fully decoded raster image (see <see cref="PptxDocument.ResolvePictureSurface"/>, reused
///     unchanged for this fill-context <c>&lt;a:blipFill&gt;</c>, which is structurally identical
///     to a <c>&lt;p:pic&gt;</c>'s own <c>&lt;p:blipFill&gt;</c>). Not owned/disposed by this
///     record - the resolving <see cref="PptxDocument"/> instance remains responsible for its own
///     decoded-image lifetime, mirroring <see cref="Drawing.TilePaint"/>'s own documented
///     non-ownership convention.
/// </param>
/// <param name="ImageToLocalTransform">
///     The transform mapping <see cref="Image"/>'s own pixel-space coordinates (pattern space,
///     with <c>(0,0)</c> at its top-left corner) into the owning shape's local EMU coordinate
///     space (the same space <see cref="PptxDocument.ResolveShapeGeometry"/>'s own returned
///     <see cref="Geometry.Path"/> is expressed in) - composed with a caller's own
///     shape-to-surface transform at fill time, mirroring <see cref="PptxGradientFill"/>'s own
///     <see cref="Gradient.WithTransform"/> composition pattern.
/// </param>
internal sealed record PptxImageFill(Surface Image, Matrix3x2 ImageToLocalTransform) : PptxPaint;

/// <summary>
///     The ECMA-376 <c>ST_PresetPatternVal</c> preset-pattern names this project resolves (as
///     distinct from throwing <see cref="PptxUnsupportedFeatureException"/>) for an
///     <c>&lt;a:pattFill prst="..."/&gt;</c> element - see <see cref="PptxDocument.ResolveFill"/>.
/// </summary>
/// <remarks>
///     <para>
///     <b>Covered (30 of the 54 named <c>ST_PresetPatternVal</c> values)</b>: the two preset names
///     the project's own <c>pythonpptx-dml-fill.pptx</c> fixture requires
///     (<see cref="Divot"/>, <see cref="Wave"/>); the horizontal/vertical stripe family
///     (<see cref="Horz"/>, <see cref="Vert"/>, <see cref="LtHorz"/>, <see cref="LtVert"/>,
///     <see cref="DkHorz"/>, <see cref="DkVert"/>); the diagonal-stripe family
///     (<see cref="DnDiag"/>, <see cref="UpDiag"/>, <see cref="LtDnDiag"/>,
///     <see cref="LtUpDiag"/>, <see cref="DkDnDiag"/>, <see cref="DkUpDiag"/>,
///     <see cref="WdDnDiag"/>, <see cref="WdUpDiag"/>); the cross-hatch family
///     (<see cref="Cross"/>, <see cref="DiagCross"/>); and the percentage/dot-density family
///     (<see cref="Pct5"/>, <see cref="Pct10"/>, <see cref="Pct20"/>, <see cref="Pct25"/>,
///     <see cref="Pct30"/>, <see cref="Pct40"/>, <see cref="Pct50"/>, <see cref="Pct60"/>,
///     <see cref="Pct70"/>, <see cref="Pct75"/>, <see cref="Pct80"/>, <see cref="Pct90"/>).
///     </para>
///     <para>
///     <b>Explicitly deferred (the remaining 24 names)</b>, each still throwing
///     <see cref="PptxUnsupportedFeatureException"/> (feature token <c>"pptx-pattern-fill"</c>)
///     from <see cref="PptxDocument.ResolveFill"/>: <c>narHorz</c>, <c>narVert</c>,
///     <c>dashHorz</c>, <c>dashVert</c>, <c>dashDnDiag</c>, <c>dashUpDiag</c>, <c>diagBrick</c>,
///     <c>horzBrick</c>, <c>plaid</c>, <c>sphere</c>, <c>weave</c>, <c>shingle</c>, <c>trellis</c>,
///     <c>zigZag</c>, <c>dotGrid</c>, <c>dotDmnd</c>, <c>openDmnd</c>, <c>solidDmnd</c>,
///     <c>smCheck</c>, <c>lgCheck</c>, <c>smGrid</c>, <c>lgGrid</c>, <c>smConfetti</c>,
///     <c>lgConfetti</c>.
///     </para>
///     <para>
///     A separate, non-named edge case - an <c>&lt;a:pattFill&gt;</c> with no <c>prst</c>
///     attribute at all (non-conformant per ECMA-376, but present in the real
///     <c>pythonpptx-dml-fill.pptx</c> fixture) - is not a member of this enum at all: it resolves
///     to <see cref="PptxNoFill.Instance"/> directly, never reaching
///     <see cref="PptxPatternFill"/> - see <see cref="PptxDocument.ResolveFill"/>'s remarks.
///     </para>
/// </remarks>
internal enum PptxPresetPattern
{
    /// <summary><c>prst="horz"</c>: equal-width alternating horizontal stripes.</summary>
    Horz,

    /// <summary><c>prst="vert"</c>: equal-width alternating vertical stripes.</summary>
    Vert,

    /// <summary><c>prst="ltHorz"</c>: thin, sparse horizontal stripes (mostly background).</summary>
    LtHorz,

    /// <summary><c>prst="ltVert"</c>: thin, sparse vertical stripes (mostly background).</summary>
    LtVert,

    /// <summary><c>prst="dkHorz"</c>: thick, dense horizontal stripes (mostly foreground).</summary>
    DkHorz,

    /// <summary><c>prst="dkVert"</c>: thick, dense vertical stripes (mostly foreground).</summary>
    DkVert,

    /// <summary><c>prst="dnDiag"</c>: equal-width alternating diagonal stripes, falling left-to-right.</summary>
    DnDiag,

    /// <summary><c>prst="upDiag"</c>: equal-width alternating diagonal stripes, rising left-to-right.</summary>
    UpDiag,

    /// <summary><c>prst="ltDnDiag"</c>: thin, sparse falling diagonal stripes.</summary>
    LtDnDiag,

    /// <summary><c>prst="ltUpDiag"</c>: thin, sparse rising diagonal stripes.</summary>
    LtUpDiag,

    /// <summary><c>prst="dkDnDiag"</c>: thick, dense falling diagonal stripes.</summary>
    DkDnDiag,

    /// <summary><c>prst="dkUpDiag"</c>: thick, dense rising diagonal stripes.</summary>
    DkUpDiag,

    /// <summary><c>prst="wdDnDiag"</c>: wide falling diagonal bands.</summary>
    WdDnDiag,

    /// <summary><c>prst="wdUpDiag"</c>: wide rising diagonal bands.</summary>
    WdUpDiag,

    /// <summary><c>prst="cross"</c>: a horizontal/vertical cross-hatch grid.</summary>
    Cross,

    /// <summary><c>prst="diagCross"</c>: a diagonal cross-hatch grid.</summary>
    DiagCross,

    /// <summary><c>prst="pct5"</c>: an approximately 5%-density dot fill.</summary>
    Pct5,

    /// <summary><c>prst="pct10"</c>: an approximately 10%-density dot fill.</summary>
    Pct10,

    /// <summary><c>prst="pct20"</c>: an approximately 20%-density dot fill.</summary>
    Pct20,

    /// <summary><c>prst="pct25"</c>: an approximately 25%-density dot fill.</summary>
    Pct25,

    /// <summary><c>prst="pct30"</c>: an approximately 30%-density dot fill.</summary>
    Pct30,

    /// <summary><c>prst="pct40"</c>: an approximately 40%-density dot fill.</summary>
    Pct40,

    /// <summary><c>prst="pct50"</c>: an approximately 50%-density dot fill.</summary>
    Pct50,

    /// <summary><c>prst="pct60"</c>: an approximately 60%-density dot fill.</summary>
    Pct60,

    /// <summary><c>prst="pct70"</c>: an approximately 70%-density dot fill.</summary>
    Pct70,

    /// <summary><c>prst="pct75"</c>: an approximately 75%-density dot fill.</summary>
    Pct75,

    /// <summary><c>prst="pct80"</c>: an approximately 80%-density dot fill.</summary>
    Pct80,

    /// <summary><c>prst="pct90"</c>: an approximately 90%-density dot fill.</summary>
    Pct90,

    /// <summary><c>prst="divot"</c>: small diamond-shaped dots, fixture-mandatory (see this enum's remarks).</summary>
    Divot,

    /// <summary><c>prst="wave"</c>: a wavy horizontal line, fixture-mandatory (see this enum's remarks).</summary>
    Wave,
}

/// <summary>
///     The resolved paint for an <c>&lt;a:pattFill prst="..."/&gt;</c> element naming a covered
///     <see cref="PptxPresetPattern"/>: a procedurally synthesized repeating tile (see
///     <see cref="PptxPatternTileRenderer"/>) alternating <see cref="Foreground"/>/
///     <see cref="Background"/> per the named preset's own pixel rule.
/// </summary>
/// <param name="Preset">The resolved, covered preset-pattern name.</param>
/// <param name="Foreground">
///     The pattern's resolved foreground color (the <c>&lt;a:fgClr&gt;</c> child's own
///     color-definition element, via <see cref="PptxDocument.ResolveColor"/>; black when
///     <c>&lt;a:fgClr&gt;</c> is absent - see <see cref="PptxDocument.ResolveFill"/>'s remarks).
/// </param>
/// <param name="Background">
///     The pattern's resolved background color (the <c>&lt;a:bgClr&gt;</c> child's own
///     color-definition element, via <see cref="PptxDocument.ResolveColor"/>; white when
///     <c>&lt;a:bgClr&gt;</c> is absent - see <see cref="PptxDocument.ResolveFill"/>'s remarks).
/// </param>
internal sealed record PptxPatternFill(PptxPresetPattern Preset, Rgba32 Foreground, Rgba32 Background) : PptxPaint;

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
///     <see cref="PptxSolidFill"/>, <see cref="PptxGradientFill"/>, and <see cref="PptxImageFill"/> -
///     all declared in this same file/assembly - may derive from it. A later rendering phase that
///     consumes <see cref="PptxPaint"/> pattern-matches exhaustively on exactly these four
///     subtypes.
/// </remarks>
internal abstract record PptxPaint
{
    /// <summary>Restricts this hierarchy to the four subtypes declared in this file - see this type's remarks.</summary>
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

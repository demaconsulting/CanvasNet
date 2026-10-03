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
///     <see cref="PptxSolidFill"/>, and <see cref="PptxGradientFill"/> - all declared in this same
///     file/assembly - may derive from it. A later rendering phase that consumes
///     <see cref="PptxPaint"/> pattern-matches exhaustively on exactly these three subtypes.
/// </remarks>
internal abstract record PptxPaint
{
    /// <summary>Restricts this hierarchy to the three subtypes declared in this file - see this type's remarks.</summary>
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

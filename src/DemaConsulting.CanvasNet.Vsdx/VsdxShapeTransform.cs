namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio LocPin davehoward

/// <summary>
///     A shape's resolved 2-D affine transform from its own local coordinate box (origin bottom-
///     left, spanning <c>[0, Width] x [0, Height]</c>, in inches) into page-space inches, per the
///     unified Visio "shape to page" transform (see the format reference's §4.3): translate so
///     the pin/rotation-center (<see cref="LocPinX"/>/<see cref="LocPinY"/>) is the origin, apply
///     <see cref="FlipX"/>/<see cref="FlipY"/>, rotate by <see cref="Angle"/> (radians,
///     counter-clockwise), then translate to (<see cref="PinX"/>, <see cref="PinY"/>).
/// </summary>
/// <remarks>
///     <para>
///         <strong>1-D shapes (connectors/lines)</strong> use exactly this same transform: a 1-D
///         shape's <c>PinX</c>/<c>PinY</c>/<c>Width</c>/<c>Height</c>/<c>Angle</c> cells are
///         themselves pre-evaluated (baked) by Visio from its <c>BeginX</c>/<c>BeginY</c>/
///         <c>EndX</c>/<c>EndY</c> endpoints (for example <c>Width = SQRT((EndX-BeginX)^2 +
///         (EndY-BeginY)^2)</c>, <c>Angle = ATAN2(EndY-BeginY, EndX-BeginX)</c> - confirmed
///         directly against <c>davehoward-test9-rect-and-line.vsdx</c>'s Shape ID='2', "Line A").
///         Since the stored <c>V</c> values already reflect that computation, no distinct 1-D
///         transform type is needed - resolving a 1-D shape's <c>PinX</c>/<c>PinY</c>/<c>Width</c>/
///         <c>Height</c>/<c>Angle</c> cells exactly like any 2-D shape's own cells and constructing
///         the same <see cref="VsdxShapeTransform"/> is sufficient.
///     </para>
///     <para>
///         <strong>Deliberate deviation from the originating plan report's exact signature:</strong>
///         this type stores every field as <see cref="double"/> and exposes a
///         <see cref="ToPage(double, double)"/> overload returning a <c>(double X, double Y)</c>
///         tuple, rather than operating on
///         <c>System.Numerics.Vector2</c> (as <see cref="DemaConsulting.CanvasNet.Geometry.Path"/>/
///         <c>PathBuilder</c> do). <c>Vector2</c>'s 32-bit <see cref="float"/> components carry
///         only about 7 significant decimal digits; at the page-space magnitudes exercised by this
///         unit's own worked rotation-transform regression test (page coordinates around 10
///         inches, needing a tolerance far tighter than a few float ULPs), routing the core
///         rotation/translation arithmetic through <c>Vector2</c> would make an exact-digit
///         regression test like this milestone's <c>VsdxTransformTests</c> impossible to assert
///         meaningfully. Keeping the transform math in <see cref="double"/> costs nothing (no
///         <see cref="DemaConsulting.CanvasNet.Geometry.Path"/> is built in page space this
///         milestone - see <see cref="VsdxGeometrySection"/>'s own remarks) and preserves full
///         precision for whichever later milestone (7) first needs to build a page-space
///         <see cref="DemaConsulting.CanvasNet.Geometry.Path"/>, at which point a
///         <see cref="float"/>-narrowing convenience overload can be added without revisiting this
///         type's core math.
///     </para>
/// </remarks>
/// <param name="PinX">The shape's pin X position, in page-space inches.</param>
/// <param name="PinY">The shape's pin Y position, in page-space inches.</param>
/// <param name="Width">The shape's local box width, in inches.</param>
/// <param name="Height">The shape's local box height, in inches.</param>
/// <param name="LocPinX">The local-box X coordinate of the pin/rotation-center, in inches.</param>
/// <param name="LocPinY">The local-box Y coordinate of the pin/rotation-center, in inches.</param>
/// <param name="Angle">The shape's rotation, in radians, counter-clockwise.</param>
/// <param name="FlipX">Whether the local box is mirrored about the <see cref="LocPinX"/> axis before rotation.</param>
/// <param name="FlipY">Whether the local box is mirrored about the <see cref="LocPinY"/> axis before rotation.</param>
internal sealed record VsdxShapeTransform(
    double PinX,
    double PinY,
    double Width,
    double Height,
    double LocPinX,
    double LocPinY,
    double Angle,
    bool FlipX,
    bool FlipY)
{
    /// <summary>
    ///     Maps a shape-local point into page-space inches, per this transform's unified
    ///     translate/flip/rotate/translate formula.
    /// </summary>
    /// <param name="localX">The local-box X coordinate, in inches.</param>
    /// <param name="localY">The local-box Y coordinate, in inches.</param>
    /// <returns>The corresponding page-space point, in inches.</returns>
    public (double X, double Y) ToPage(double localX, double localY)
    {
        var dx = localX - LocPinX;
        var dy = localY - LocPinY;

        if (FlipX)
        {
            dx = -dx;
        }

        if (FlipY)
        {
            dy = -dy;
        }

        var cos = Math.Cos(Angle);
        var sin = Math.Sin(Angle);
        var rx = (dx * cos) - (dy * sin);
        var ry = (dx * sin) + (dy * cos);

        return (PinX + rx, PinY + ry);
    }
}

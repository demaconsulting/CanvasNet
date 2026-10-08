namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio LocPin

/// <summary>
///     Implements the <see cref="VsdxDocument"/> shape transform resolver: builds a
///     <see cref="VsdxShapeTransform"/> from a shape's merged flat cell bag's own <c>PinX</c>/
///     <c>PinY</c>/<c>Width</c>/<c>Height</c>/<c>LocPinX</c>/<c>LocPinY</c>/<c>Angle</c>/
///     <c>FlipX</c>/<c>FlipY</c> cells. Applies uniformly to both 2-D shapes and 1-D shapes
///     (connectors/lines) - see <see cref="VsdxShapeTransform"/>'s own remarks for why no distinct
///     1-D transform type is needed.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>Builds a shape's resolved transform from its merged flat cell bag.</summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <returns>The resolved <see cref="VsdxShapeTransform"/>.</returns>
    private static VsdxShapeTransform BuildTransform(VsdxCellBag effectiveCells) =>
        new(
            PinX: effectiveCells.GetDouble("PinX"),
            PinY: effectiveCells.GetDouble("PinY"),
            Width: effectiveCells.GetDouble("Width"),
            Height: effectiveCells.GetDouble("Height"),
            LocPinX: effectiveCells.GetDouble("LocPinX"),
            LocPinY: effectiveCells.GetDouble("LocPinY"),
            Angle: effectiveCells.GetDouble("Angle"),
            FlipX: effectiveCells.GetBool("FlipX"),
            FlipY: effectiveCells.GetBool("FlipY"));
}

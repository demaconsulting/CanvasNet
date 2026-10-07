namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio LocPin

/// <summary>
///     Implements the <see cref="VsdxDocument"/> text-box transform resolver: a shape's text box
///     defaults to filling its own geometry bounding box (format reference §8.3 - "implicit
///     default is... none of the plain-rectangle shapes in the samples emit <c>Txt*</c> cells at
///     all, so absence of <c>Txt*</c> cells means text box == shape bounding box, centered pin,
///     zero rotation"), overridden cell-by-cell when the shape's merged flat cell bag carries a
///     literal <c>TxtPinX</c>/<c>TxtPinY</c>/<c>TxtWidth</c>/<c>TxtHeight</c>/<c>TxtLocPinX</c>/
///     <c>TxtLocPinY</c>/<c>TxtAngle</c> cell of its own.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     Builds a shape's resolved text-box transform.
    /// </summary>
    /// <param name="effectiveCells">The shape's merged flat cell bag.</param>
    /// <param name="shapeTransform">The shape's own resolved transform (supplies the implicit-default <c>Width</c>/<c>Height</c>).</param>
    /// <returns>The resolved <see cref="VsdxTextBoxTransform"/>.</returns>
    private static VsdxTextBoxTransform BuildTextBox(VsdxCellBag effectiveCells, VsdxShapeTransform shapeTransform)
    {
        var width = effectiveCells.TryGetLiteral("TxtWidth", out _)
            ? effectiveCells.GetDouble("TxtWidth", shapeTransform.Width)
            : shapeTransform.Width;

        var height = effectiveCells.TryGetLiteral("TxtHeight", out _)
            ? effectiveCells.GetDouble("TxtHeight", shapeTransform.Height)
            : shapeTransform.Height;

        var pinX = effectiveCells.TryGetLiteral("TxtPinX", out _)
            ? effectiveCells.GetDouble("TxtPinX", width * 0.5)
            : width * 0.5;

        var pinY = effectiveCells.TryGetLiteral("TxtPinY", out _)
            ? effectiveCells.GetDouble("TxtPinY", height * 0.5)
            : height * 0.5;

        var locPinX = effectiveCells.TryGetLiteral("TxtLocPinX", out _)
            ? effectiveCells.GetDouble("TxtLocPinX", width * 0.5)
            : width * 0.5;

        var locPinY = effectiveCells.TryGetLiteral("TxtLocPinY", out _)
            ? effectiveCells.GetDouble("TxtLocPinY", height * 0.5)
            : height * 0.5;

        var angle = effectiveCells.GetDouble("TxtAngle");

        return new VsdxTextBoxTransform(pinX, pinY, width, height, locPinX, locPinY, angle);
    }
}

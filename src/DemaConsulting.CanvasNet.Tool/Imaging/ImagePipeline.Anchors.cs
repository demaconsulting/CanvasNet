namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <content>
///     The mapping of anchor pixel positions through the steps the pipeline applied.
/// </content>
internal static partial class ImagePipeline
{
    /// <summary>
    ///     Maps the pixel positions of anchors from the loaded image through the operations the pipeline applied.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The positions are continuous (pixel <c>i</c> spans <c>[i, i + 1)</c>, origin top-left), so each step
    ///         is an exact coordinate change computed from what the step reported: <c>crop</c> and <c>trim</c> subtract
    ///         their <c>x</c> and <c>y</c>; <c>resize</c> and <c>fit</c> scale by the ratio of the new to the old
    ///         size; <c>pad</c> adds its <c>left</c> and <c>top</c>; <c>flip</c> mirrors the axis it names; a
    ///         <c>rotate</c> is a right-angle move (0, 90, 180 or 270 degrees clockwise) or a rotation about the image
    ///         center onto the expanded canvas, with the convention of <see cref="DemaConsulting.CanvasNet.Canvas.Surface.RotateArbitrary(double, DemaConsulting.CanvasNet.Canvas.Rgba32, bool)"/>.
    ///         Steps that change colors only (including <c>grid</c>) leave the position alone.
    ///     </para>
    ///     <para>
    ///         A position that cannot be followed becomes <see langword="null"/> for the rest of the pipeline: a
    ///         <c>deskew</c> that corrected the image, and any other step that changed the size of the image without
    ///         being one of the steps above (so a future geometry step fails safe instead of reporting a wrong
    ///         position). A <see langword="null"/> position stays <see langword="null"/>.
    ///     </para>
    ///     <para>The method is pure and thread-safe; the input list is not modified.</para>
    /// </remarks>
    /// <param name="anchors">The anchors with their positions in the loaded image.</param>
    /// <param name="width">The width of the loaded image in pixels.</param>
    /// <param name="height">The height of the loaded image in pixels.</param>
    /// <param name="applied">The operations the pipeline applied to the loaded image, in order.</param>
    /// <returns>The anchors with their positions in the image the pipeline produced.</returns>
    internal static IReadOnlyList<SourceAnchor> MapAnchors(
        IReadOnlyList<SourceAnchor> anchors,
        int width,
        int height,
        IReadOnlyList<AppliedOperation> applied)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(applied);

        var result = new List<SourceAnchor>(anchors.Count);
        foreach (var anchor in anchors)
        {
            // Follow one anchor through every step; a lost position stays lost
            var x = anchor.PixelX;
            var y = anchor.PixelY;
            var w = width;
            var h = height;
            foreach (var step in applied)
            {
                (x, y, w, h) = MapStep(step, x, y, w, h);
            }

            result.Add(anchor with { PixelX = x, PixelY = y });
        }

        return result;
    }

    /// <summary>
    ///     Maps one position through one applied operation.
    /// </summary>
    /// <param name="step">The operation as reported by the pipeline.</param>
    /// <param name="x">The horizontal position before the step, or <see langword="null"/> when it is lost.</param>
    /// <param name="y">The vertical position before the step, or <see langword="null"/> when it is lost.</param>
    /// <param name="w">The width of the image before the step.</param>
    /// <param name="h">The height of the image before the step.</param>
    /// <returns>The position after the step (lost when it cannot be followed) and the size of the image after it.</returns>
    private static (double? X, double? Y, int Width, int Height) MapStep(AppliedOperation step, double? x, double? y, int w, int h)
    {
        // The size after the step is what the step reported; a step that does not report one keeps the size
        var newW = FieldInt(step, "width") ?? w;
        var newH = FieldInt(step, "height") ?? h;
        if (x is not { } px || y is not { } py)
        {
            return (null, null, newW, newH);
        }

        switch (step.Op)
        {
            case "crop":
            case "trim":
                return (px - (FieldInt(step, "x") ?? 0), py - (FieldInt(step, "y") ?? 0), newW, newH);
            case "resize":
            case "fit":
                return (px * newW / w, py * newH / h, newW, newH);
            case "pad":
                return (px + (FieldInt(step, "left") ?? 0), py + (FieldInt(step, "top") ?? 0), newW, newH);
            case "flip":
                var axis = FieldText(step, "axis");
                return (
                    axis == "vertical" ? px : w - px,
                    axis == "horizontal" ? py : h - py,
                    newW,
                    newH);
            case "rotate":
                var (rx, ry) = MapRotate(px, py, w, h, newW, newH, FieldDouble(step, "degrees") ?? 0.0);
                return (rx, ry, newW, newH);
            case "deskew" when FieldBool(step, "applied") == true:
                return (null, null, newW, newH);
            default:
                // A color-only step keeps the size and the position; a step that resized the image without being known is not followed
                return newW == w && newH == h ? (px, py, newW, newH) : (null, null, newW, newH);
        }
    }

    /// <summary>
    ///     Maps a position through a clockwise rotation.
    /// </summary>
    /// <param name="x">The horizontal position before the rotation.</param>
    /// <param name="y">The vertical position before the rotation.</param>
    /// <param name="w">The width before the rotation.</param>
    /// <param name="h">The height before the rotation.</param>
    /// <param name="newW">The width after the rotation.</param>
    /// <param name="newH">The height after the rotation.</param>
    /// <param name="degrees">The clockwise angle in degrees as reported by the step (a right angle is normalized to 0, 90, 180 or 270).</param>
    /// <returns>The position after the rotation.</returns>
    private static (double X, double Y) MapRotate(double x, double y, int w, int h, int newW, int newH, double degrees)
    {
        // The rotation of the surface samples the source at u = dx cos + dy sin + W/2, v = -dx sin + dy cos + H/2 for
        // the destination offset (dx, dy) from the new center; the inverse of that is the forward map used here
        var theta = (degrees % 360.0) * Math.PI / 180.0;
        var cos = Math.Cos(theta);
        var sin = Math.Sin(theta);

        // A right angle is an exact pixel move: use the exact sine and cosine so no rounding noise leaks into the position
        var quarters = Math.Round(degrees / 90.0);
        if (Math.Abs(degrees - (quarters * 90.0)) < 1e-9)
        {
            var turn = (((int)quarters % 4) + 4) % 4;
            (cos, sin) = turn switch
            {
                1 => (0.0, 1.0),
                2 => (-1.0, 0.0),
                3 => (0.0, -1.0),
                _ => (1.0, 0.0),
            };
        }
        var u = x - (w / 2.0);
        var v = y - (h / 2.0);
        return ((u * cos) - (v * sin) + (newW / 2.0), (u * sin) + (v * cos) + (newH / 2.0));
    }

    /// <summary>
    ///     Reads a numeric field of an applied operation as an integer.
    /// </summary>
    /// <param name="step">The operation.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when the field is absent or not an integer.</returns>
    private static int? FieldInt(AppliedOperation step, string name) =>
        FieldValue(step, name) is int value ? value : null;

    /// <summary>
    ///     Reads a numeric field of an applied operation as a double.
    /// </summary>
    /// <param name="step">The operation.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when the field is absent or not a number.</returns>
    private static double? FieldDouble(AppliedOperation step, string name) =>
        FieldValue(step, name) switch
        {
            int i => i,
            double d => d,
            _ => null,
        };

    /// <summary>
    ///     Reads a text field of an applied operation.
    /// </summary>
    /// <param name="step">The operation.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when the field is absent or not text.</returns>
    private static string? FieldText(AppliedOperation step, string name) =>
        FieldValue(step, name) as string;

    /// <summary>
    ///     Reads a Boolean field of an applied operation.
    /// </summary>
    /// <param name="step">The operation.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when the field is absent or not a Boolean.</returns>
    private static bool? FieldBool(AppliedOperation step, string name) =>
        FieldValue(step, name) is bool value ? value : null;

    /// <summary>
    ///     Finds a field of an applied operation by name.
    /// </summary>
    /// <param name="step">The operation.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The value, or <see langword="null"/> when the operation has no such field.</returns>
    private static object? FieldValue(AppliedOperation step, string name) =>
        step.Fields.FirstOrDefault(f => string.Equals(f.Key, name, StringComparison.Ordinal)).Value;
}

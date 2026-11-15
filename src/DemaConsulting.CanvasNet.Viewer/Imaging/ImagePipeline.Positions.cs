using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <content>
///     The mapping of feature positions through the steps the pipeline applied.
/// </content>
public static partial class ImagePipeline
{
    /// <summary>
    ///     Maps the boxes and attachment pixels of feature positions from the loaded image through the operations the
    ///     pipeline applied.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The steps are those <see cref="MapAnchors"/> follows, with the same conventions: <c>crop</c> and <c>trim</c>
    ///         translate, <c>resize</c> and <c>fit</c> scale, <c>pad</c> translates, <c>flip</c> mirrors, <c>rotate</c>
    ///         turns about the image center (a right angle exactly), and the color steps (including <c>grid</c>) change
    ///         nothing. Every step is an affine change of coordinates, so the steps are composed into one transform first;
    ///         the pixel and the four corners of the original box are then transformed once and the corners are enclosed
    ///         once, so a box never grows from enclosing a rotated box again at a later step.
    ///     </para>
    ///     <para>
    ///         A crop does not clip or recompute the box: a feature the crop cut off keeps its full box, partly or wholly
    ///         outside the image. A <c>deskew</c> that corrected the image, and any other step that changed the size of the
    ///         image without being one of the steps above, loses every position: its box and pixel become
    ///         <see langword="null"/>.
    ///     </para>
    ///     <para>The method is pure and thread-safe; the input list is not modified.</para>
    /// </remarks>
    /// <param name="positions">The positions in the loaded image.</param>
    /// <param name="width">The width of the loaded image in pixels.</param>
    /// <param name="height">The height of the loaded image in pixels.</param>
    /// <param name="applied">The operations the pipeline applied to the loaded image, in order.</param>
    /// <returns>The positions in the image the pipeline produced, in the same order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="positions"/> or <paramref name="applied"/> is null.</exception>
    public static IReadOnlyList<ImagePosition> MapPositions(
        IReadOnlyList<ImagePosition> positions,
        int width,
        int height,
        IReadOnlyList<AppliedOperation> applied)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(applied);

        // Compose one transform over every step; a step that cannot be followed loses every position
        Affine? transform = Affine.Identity;
        var w = width;
        var h = height;
        foreach (var step in applied)
        {
            var (next, newW, newH) = StepTransform(step, w, h);
            transform = transform is { } current && next is { } change ? change.After(current) : null;
            (w, h) = (newW, newH);
        }

        // Transform each pixel and the corners of each original box once
        var result = new List<ImagePosition>(positions.Count);
        foreach (var position in positions)
        {
            if (transform is not { } map)
            {
                result.Add(position with { Box = null, Pixel = null });
                continue;
            }

            var box = position.Box is { } original
                ? PositionBox.Enclose([.. original.Corners().Select(map.Map)])
                : (PositionBox?)null;
            var pixel = position.Pixel is { } point ? map.Map(point) : (PositionPoint?)null;
            result.Add(position with { Box = box, Pixel = pixel });
        }

        return result;
    }

    /// <summary>
    ///     Gets the change of coordinates of one applied operation.
    /// </summary>
    /// <param name="step">The operation as reported by the pipeline.</param>
    /// <param name="w">The width of the image before the step.</param>
    /// <param name="h">The height of the image before the step.</param>
    /// <returns>The transform (null when the step cannot be followed) and the size of the image after the step.</returns>
    private static (Affine? Transform, int Width, int Height) StepTransform(AppliedOperation step, int w, int h)
    {
        // The size after the step is what the step reported; a step that does not report one keeps the size
        var newW = FieldInt(step, "width") ?? w;
        var newH = FieldInt(step, "height") ?? h;
        switch (step.Op)
        {
            case "crop":
            case "trim":
                return (Affine.Translation(-(FieldInt(step, "x") ?? 0), -(FieldInt(step, "y") ?? 0)), newW, newH);
            case "resize":
            case "fit":
                return (new Affine((double)newW / w, 0, 0, (double)newH / h, 0, 0), newW, newH);
            case "pad":
                return (Affine.Translation(FieldInt(step, "left") ?? 0, FieldInt(step, "top") ?? 0), newW, newH);
            case "flip":
                var axis = FieldText(step, "axis");
                var mirrorX = axis != "vertical";
                var mirrorY = axis != "horizontal";
                return (new Affine(mirrorX ? -1 : 1, 0, 0, mirrorY ? -1 : 1, mirrorX ? w : 0, mirrorY ? h : 0), newW, newH);
            case "rotate":
                // The same map as an anchor: offsets from the old center are turned and moved to the new center
                var (cos, sin) = RotationOf(FieldDouble(step, "degrees") ?? 0.0);
                var tx = (newW / 2.0) - (cos * (w / 2.0)) + (sin * (h / 2.0));
                var ty = (newH / 2.0) - (sin * (w / 2.0)) - (cos * (h / 2.0));
                return (new Affine(cos, sin, -sin, cos, tx, ty), newW, newH);
            case "deskew" when FieldBool(step, "applied") == true:
                return (null, newW, newH);
            default:
                // A color-only step keeps the size and the coordinates; a step that resized the image without being known is not followed
                return (newW == w && newH == h ? Affine.Identity : null, newW, newH);
        }
    }

    /// <summary>
    ///     Gets the cosine and sine of a clockwise rotation, exact for a right angle.
    /// </summary>
    /// <param name="degrees">The clockwise angle in degrees.</param>
    /// <returns>The cosine and the sine.</returns>
    private static (double Cos, double Sin) RotationOf(double degrees)
    {
        var quarters = Math.Round(degrees / 90.0);
        if (Math.Abs(degrees - (quarters * 90.0)) < 1e-9)
        {
            return ((((int)quarters % 4) + 4) % 4) switch
            {
                1 => (0.0, 1.0),
                2 => (-1.0, 0.0),
                3 => (0.0, -1.0),
                _ => (1.0, 0.0),
            };
        }

        var theta = (degrees % 360.0) * Math.PI / 180.0;
        return (Math.Cos(theta), Math.Sin(theta));
    }

    /// <summary>
    ///     An affine change of coordinates: <c>x' = A x + C y + E</c>, <c>y' = B x + D y + F</c>.
    /// </summary>
    /// <param name="A">The x scale of x.</param>
    /// <param name="B">The y share of x.</param>
    /// <param name="C">The x share of y.</param>
    /// <param name="D">The y scale of y.</param>
    /// <param name="E">The x translation.</param>
    /// <param name="F">The y translation.</param>
    private readonly record struct Affine(double A, double B, double C, double D, double E, double F)
    {
        /// <summary>
        ///     Gets the transform that changes nothing.
        /// </summary>
        public static Affine Identity => new(1, 0, 0, 1, 0, 0);

        /// <summary>
        ///     Creates a translation.
        /// </summary>
        /// <param name="x">The horizontal move.</param>
        /// <param name="y">The vertical move.</param>
        /// <returns>The transform.</returns>
        public static Affine Translation(double x, double y) => new(1, 0, 0, 1, x, y);

        /// <summary>
        ///     Composes this transform after another one.
        /// </summary>
        /// <param name="first">The transform applied first.</param>
        /// <returns>The transform that applies <paramref name="first"/>, then this one.</returns>
        public Affine After(Affine first) => new(
            (A * first.A) + (C * first.B),
            (B * first.A) + (D * first.B),
            (A * first.C) + (C * first.D),
            (B * first.C) + (D * first.D),
            (A * first.E) + (C * first.F) + E,
            (B * first.E) + (D * first.F) + F);

        /// <summary>
        ///     Applies the transform to a point.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>The transformed point.</returns>
        public PositionPoint Map(PositionPoint point) =>
            new((A * point.X) + (C * point.Y) + E, (B * point.X) + (D * point.Y) + F);
    }
}

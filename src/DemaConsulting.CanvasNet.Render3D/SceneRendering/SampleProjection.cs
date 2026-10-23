namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Maps view-space positions to the global sample grid and describes the guard band that keeps fixed-point arithmetic in range.
/// </summary>
/// <remarks>
///     <para>
///         The sample grid is the output size multiplied by the supersampling factor; sample
///         coordinates have y pointing down. A view-space point with homogeneous weight <c>w</c>
///         (<c>-Z</c> for perspective, 1 for orthographic) maps to
///         <c>sx = W/2 + (X * ScaleX / w) * W/2</c> and <c>sy = H/2 - (Y * ScaleY / w) * H/2</c>.
///     </para>
///     <para>
///         <b>Guard band:</b> geometry projecting more than <see cref="GuardBandSamples"/> (2^20)
///         samples from the screen centre is clipped to that limit before conversion to 24.8 fixed
///         point. Fixed-point magnitudes are then below about 2^29, so edge-function products
///         (below about 2^59) always fit in a signed 64-bit integer.
///     </para>
/// </remarks>
internal readonly struct SampleProjection
{
    /// <summary>The guard-band half extent in samples (2^20), measured from the screen centre.</summary>
    public const double GuardBandSamples = 1048576.0;

    /// <summary>
    ///     Creates a projection from explicit scalars (used directly by unit tests).
    /// </summary>
    /// <param name="isOrthographic"><see langword="true"/> for orthographic projection.</param>
    /// <param name="scaleX">The view-X to clip-X factor.</param>
    /// <param name="scaleY">The view-Y to clip-Y factor.</param>
    /// <param name="sampleWidth">The sample grid width (output width times supersampling).</param>
    /// <param name="sampleHeight">The sample grid height.</param>
    /// <param name="near">The near distance (greater than zero).</param>
    public SampleProjection(bool isOrthographic, double scaleX, double scaleY, int sampleWidth, int sampleHeight, double near)
    {
        IsOrthographic = isOrthographic;
        ScaleX = scaleX;
        ScaleY = scaleY;
        HalfWidth = sampleWidth / 2.0;
        HalfHeight = sampleHeight / 2.0;
        Near = near;
    }

    /// <summary>Gets a value indicating whether the projection is orthographic.</summary>
    public bool IsOrthographic { get; }

    /// <summary>Gets the view-X to clip-X factor.</summary>
    public double ScaleX { get; }

    /// <summary>Gets the view-Y to clip-Y factor.</summary>
    public double ScaleY { get; }

    /// <summary>Gets half the sample grid width.</summary>
    public double HalfWidth { get; }

    /// <summary>Gets half the sample grid height.</summary>
    public double HalfHeight { get; }

    /// <summary>Gets the near clip distance.</summary>
    public double Near { get; }

    /// <summary>
    ///     Creates the projection for a view transform and sample grid.
    /// </summary>
    /// <param name="view">The view transform.</param>
    /// <param name="sampleWidth">The sample grid width.</param>
    /// <param name="sampleHeight">The sample grid height.</param>
    /// <param name="near">The near clip distance.</param>
    /// <returns>The projection.</returns>
    public static SampleProjection Create(in ViewTransform view, int sampleWidth, int sampleHeight, double near) =>
        new(view.IsOrthographic, view.ClipScaleX, view.ClipScaleY, sampleWidth, sampleHeight, near);

    /// <summary>
    ///     Projects a view-space position (already clipped to the near plane) to sample coordinates.
    /// </summary>
    /// <param name="vertex">The view-space position.</param>
    /// <returns>The sample-space X and Y (y down).</returns>
    public (double X, double Y) ToSample(in ClipVertex vertex)
    {
        var w = IsOrthographic ? 1.0 : vertex.Depth;
        var sx = HalfWidth + (vertex.X * ScaleX / w * HalfWidth);
        var sy = HalfHeight - (vertex.Y * ScaleY / w * HalfHeight);
        return (sx, sy);
    }

    /// <summary>Determines whether a vertex projects inside the guard band.</summary>
    /// <param name="vertex">The view-space position (already near-clipped).</param>
    /// <returns><see langword="true"/> when both sample coordinates are within the guard band.</returns>
    public bool IsInsideGuardBand(in ClipVertex vertex)
    {
        var (sx, sy) = ToSample(vertex);
        return Math.Abs(sx - HalfWidth) <= GuardBandSamples && Math.Abs(sy - HalfHeight) <= GuardBandSamples;
    }

    /// <summary>
    ///     Builds one of the four guard-band side planes (0 = +X, 1 = -X, 2 = +Y, 3 = -Y).
    /// </summary>
    /// <param name="index">The plane index, 0 to 3.</param>
    /// <returns>The clip plane whose inside is the guard band.</returns>
    public ClipPlane GuardPlane(int index)
    {
        // Express |clip| <= g * w as a linear plane; w is -Z for perspective and 1 for orthographic
        var horizontal = index < 2;
        var g = GuardBandSamples / (horizontal ? HalfWidth : HalfHeight);
        var scale = horizontal ? ScaleX : ScaleY;
        var sign = (index & 1) == 0 ? -1.0 : 1.0;
        var wCoefficient = IsOrthographic ? 0.0 : -g;
        var constant = IsOrthographic ? g : 0.0;

        return horizontal
            ? new ClipPlane(sign * scale, 0.0, wCoefficient, constant)
            : new ClipPlane(0.0, sign * scale, wCoefficient, constant);
    }
}

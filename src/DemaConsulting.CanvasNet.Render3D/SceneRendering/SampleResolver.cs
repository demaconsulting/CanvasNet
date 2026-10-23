using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Resolves a tile's sub-pixel samples into output pixels with a k x k box filter in premultiplied integer arithmetic.
/// </summary>
/// <remarks>
///     <para>
///         For <c>n = k*k</c> samples with alpha <c>a</c> and color <c>c</c> (untouched samples take
///         the background color): <c>A = (sum a + n/2) / n</c> and, when <c>sum a &gt; 0</c>,
///         <c>C = (sum c*a + (sum a)/2) / sum a</c>, else <c>C = 0</c>. Averaging
///         premultiplied values keeps edge pixels over a transparent background free of dark
///         fringes (their color stays the surface color; only alpha is reduced). For fully opaque
///         samples this reduces to the plain rounded average <c>(sum c + n/2) / n</c>.
///     </para>
///     <para>
///         The resolver writes only the pixel rectangle of the given tile, so different tiles may be
///         resolved concurrently into the same <see cref="Surface"/> (rows are disjoint memory).
///         Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class SampleResolver
{
    /// <summary>
    ///     Resolves the pixels of one tile into the surface.
    /// </summary>
    /// <param name="workspace">The tile's sample buffers; its origin must be <c>(pixels.X0 * k, pixels.Y0 * k)</c>.</param>
    /// <param name="supersampling">The supersampling factor <c>k</c>.</param>
    /// <param name="background">The color of untouched samples.</param>
    /// <param name="surface">The destination surface.</param>
    /// <param name="pixels">The output pixel rectangle covered by the tile (half-open).</param>
    public static void Resolve(TileWorkspace workspace, int supersampling, Rgba32 background, Surface surface, TileRect pixels)
    {
        var k = supersampling;
        var n = k * k;

        for (var py = pixels.Y0; py < pixels.Y1; py++)
        {
            var row = surface.GetRowSpan(py);
            for (var px = pixels.X0; px < pixels.X1; px++)
            {
                // Sum the premultiplied channels of the k x k samples of this pixel
                long sumA = 0;
                long sumR = 0;
                long sumG = 0;
                long sumB = 0;
                for (var sy = 0; sy < k; sy++)
                {
                    var index = workspace.IndexOf(px * k, (py * k) + sy);
                    for (var sx = 0; sx < k; sx++, index++)
                    {
                        var color = float.IsPositiveInfinity(workspace.Depth[index]) ? background : workspace.Colors[index];
                        sumA += color.A;
                        sumR += color.R * color.A;
                        sumG += color.G * color.A;
                        sumB += color.B * color.A;
                    }
                }

                row[px] = Average(sumA, sumR, sumG, sumB, n);
            }
        }
    }

    /// <summary>
    ///     Converts premultiplied sums into one straight-alpha pixel.
    /// </summary>
    /// <param name="sumA">The sum of the sample alphas.</param>
    /// <param name="sumR">The sum of red times alpha.</param>
    /// <param name="sumG">The sum of green times alpha.</param>
    /// <param name="sumB">The sum of blue times alpha.</param>
    /// <param name="n">The number of samples.</param>
    /// <returns>The averaged pixel.</returns>
    private static Rgba32 Average(long sumA, long sumR, long sumG, long sumB, int n)
    {
        // Fully transparent: no color information exists
        if (sumA == 0)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        var half = sumA / 2;
        return new Rgba32(
            (byte)((sumR + half) / sumA),
            (byte)((sumG + half) / sumA),
            (byte)((sumB + half) / sumA),
            (byte)((sumA + (n / 2)) / n));
    }
}

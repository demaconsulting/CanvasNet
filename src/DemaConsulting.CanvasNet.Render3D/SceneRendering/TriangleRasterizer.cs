// cspell:ignore binner

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Triangle setup (clipping, projection, fixed-point conversion, winding) and fixed-point
///     top-left-rule rasterization.
/// </summary>
/// <remarks>
///     <para>
///         <b>Setup.</b> A view-space triangle is clipped against the near plane (always) and the
///         guard band (only when a vertex projects outside it), fan-triangulated, projected to the
///         sample grid in double precision, rounded to 24.8 fixed point and normalized to positive
///         <c>Area2</c>. With a y-down screen a triangle that is counter-clockwise (outward) as seen
///         by the viewer has a <b>negative</b> raw <c>Area2</c> and is front-facing; the rasterizer
///         swaps two vertices to make the area positive. Zero-area results are discarded.
///     </para>
///     <para>
///         <b>Rasterization.</b> Coverage uses only 64-bit integer edge functions: sample
///         <c>(i, j)</c> has its centre at <c>(256 i + 128, 256 j + 128)</c>; a sample is covered when
///         all three edge functions are positive, or zero on an edge that is a top or left edge
///         (with the normalized orientation an edge <c>(dx, dy)</c> owns its boundary samples iff
///         <c>dy &lt; 0</c> or <c>dy == 0 &amp;&amp; dx &gt; 0</c>). A neighboring triangle sees the shared edge
///         reversed, so every boundary sample belongs to exactly one of them: no gaps, no double
///         hits. Float arithmetic is used only for the interpolated depth of covered samples.
///     </para>
///     <para>
///         All members are static, stateless and thread-safe (the visitor and output list are the
///         caller's).
///     </para>
/// </remarks>
internal static class TriangleRasterizer
{
    /// <summary>Fixed-point units per sample (24.8 format).</summary>
    private const int FixedOne = 256;

    /// <summary>Fixed-point offset of a sample centre within its cell (half a sample).</summary>
    private const int FixedHalf = 128;

    /// <summary>Fractional bits of the fixed-point format.</summary>
    private const double FixedScale = 256.0;

    /// <summary>Largest accepted fixed-point magnitude; larger values (which the guard band prevents) are discarded defensively.</summary>
    private const double FixedLimit = 1073741824.0;

    /// <summary>
    ///     Clips, projects and prepares one view-space triangle, appending zero or more
    ///     <see cref="ScreenTriangle"/>s to <paramref name="output"/>.
    /// </summary>
    /// <param name="triangle">The view-space triangle in counter-clockwise-outward order.</param>
    /// <param name="projection">The sample-space projection (including the near distance).</param>
    /// <param name="output">The list the prepared triangles are appended to.</param>
    public static void Setup(in ViewTriangle triangle, in SampleProjection projection, List<ScreenTriangle> output)
    {
        // Overflowed or NaN geometry cannot be projected; skip it
        if (!IsFinite(triangle.A) || !IsFinite(triangle.B) || !IsFinite(triangle.C))
        {
            return;
        }

        // Ping-pong buffers for the (at most five) clip passes
        Span<ClipVertex> first = stackalloc ClipVertex[TriangleClipper.MaxVertices];
        Span<ClipVertex> second = stackalloc ClipVertex[TriangleClipper.MaxVertices];
        first[0] = triangle.A;
        first[1] = triangle.B;
        first[2] = triangle.C;

        // Near plane first: it guarantees positive depth for the projection that follows
        var count = TriangleClipper.Clip(first[..3], second, TriangleClipper.NearPlane(projection.Near));
        if (count < 3)
        {
            return;
        }

        var current = second;
        var other = first;

        // Only clip against the guard band when some vertex actually lies outside it
        var needsGuard = false;
        for (var i = 0; i < count && !needsGuard; i++)
        {
            needsGuard = !projection.IsInsideGuardBand(current[i]);
        }

        if (needsGuard)
        {
            for (var plane = 0; plane < 4 && count >= 3; plane++)
            {
                count = TriangleClipper.Clip(current[..count], other, projection.GuardPlane(plane));
                var swap = current;
                current = other;
                other = swap;
            }

            if (count < 3)
            {
                return;
            }
        }

        // Fan-triangulate the clipped (convex) polygon
        for (var i = 1; i < count - 1; i++)
        {
            Emit(current[0], current[i], current[i + 1], triangle, projection, output);
        }
    }

    /// <summary>
    ///     Rasterizes one prepared triangle into the given tile, calling the visitor for every
    ///     covered sample.
    /// </summary>
    /// <typeparam name="TVisitor">The visitor type (a struct, so the call is inlined).</typeparam>
    /// <param name="triangle">The prepared triangle (positive <see cref="ScreenTriangle.Area2"/>).</param>
    /// <param name="tile">The sample rectangle to restrict coverage to.</param>
    /// <param name="visitor">The visitor receiving covered samples.</param>
    public static void Rasterize<TVisitor>(in ScreenTriangle triangle, TileRect tile, ref TVisitor visitor)
        where TVisitor : struct, IFragmentVisitor
    {
        long x0 = triangle.X0;
        long y0 = triangle.Y0;
        long x1 = triangle.X1;
        long y1 = triangle.Y1;
        long x2 = triangle.X2;
        long y2 = triangle.Y2;

        // Sample range whose centres can lie inside the bounding box, clipped to the tile
        var bounds = GetSampleBounds(in triangle);
        var xStart = (int)Math.Max(tile.X0, bounds.XStart);
        var xEnd = (int)Math.Min(tile.X1 - 1, bounds.XEnd);
        var yStart = (int)Math.Max(tile.Y0, bounds.YStart);
        var yEnd = (int)Math.Min(tile.Y1 - 1, bounds.YEnd);
        if (xStart > xEnd || yStart > yEnd)
        {
            return;
        }

        // Edge k runs v(k) -> v(k+1); E_k(p) = (bx-ax)(py-ay) - (by-ay)(px-ax) is positive inside
        var bias0 = OwnsBoundary(x1 - x0, y1 - y0);
        var bias1 = OwnsBoundary(x2 - x1, y2 - y1);
        var bias2 = OwnsBoundary(x0 - x2, y0 - y2);
        var stepX0 = (y0 - y1) * FixedOne;
        var stepX1 = (y1 - y2) * FixedOne;
        var stepX2 = (y2 - y0) * FixedOne;
        var stepY0 = (x1 - x0) * FixedOne;
        var stepY1 = (x2 - x1) * FixedOne;
        var stepY2 = (x0 - x2) * FixedOne;

        // Edge values at the first sample centre; later samples add constant steps (no re-evaluation)
        var px = (xStart * (long)FixedOne) + FixedHalf;
        var py = (yStart * (long)FixedOne) + FixedHalf;
        var rowE0 = ((x1 - x0) * (py - y0)) - ((y1 - y0) * (px - x0)) + bias0;
        var rowE1 = ((x2 - x1) * (py - y1)) - ((y2 - y1) * (px - x1)) + bias1;
        var rowE2 = ((x0 - x2) * (py - y2)) - ((y0 - y2) * (px - x2)) + bias2;

        // Depth interpolation coefficients: lambda0 = E1/A, lambda1 = E2/A, lambda2 = E0/A
        var invArea = 1.0 / triangle.Area2;
        var perspective = triangle.PerspectiveDepth;
        var c0 = (perspective ? 1.0 / triangle.Depth0 : triangle.Depth0) * invArea;
        var c1 = (perspective ? 1.0 / triangle.Depth1 : triangle.Depth1) * invArea;
        var c2 = (perspective ? 1.0 / triangle.Depth2 : triangle.Depth2) * invArea;

        for (var y = yStart; y <= yEnd; y++)
        {
            var e0 = rowE0;
            var e1 = rowE1;
            var e2 = rowE2;
            for (var x = xStart; x <= xEnd; x++)
            {
                // Edge values carry the top-left bias, so strictly positive means covered
                if (e0 > 0 && e1 > 0 && e2 > 0)
                {
                    // Undo the bias for the barycentric weights (it is 0 or 1 fixed unit^2: negligible but removed for exactness)
                    var interpolated = ((e1 - bias1) * c0) + ((e2 - bias2) * c1) + ((e0 - bias0) * c2);
                    var depth = perspective ? 1.0 / interpolated : interpolated;
                    visitor.Visit(x, y, (float)depth);
                }

                e0 += stepX0;
                e1 += stepX1;
                e2 += stepX2;
            }

            rowE0 += stepY0;
            rowE1 += stepY1;
            rowE2 += stepY2;
        }
    }

    /// <summary>
    ///     Computes the inclusive range of sample indices whose centres lie inside the triangle's
    ///     fixed-point bounding box (empty when <c>End &lt; Start</c>).
    /// </summary>
    /// <remarks>Shared by the rasterizer and the tile binner so both agree exactly on which samples a triangle can touch.</remarks>
    /// <param name="triangle">The prepared triangle.</param>
    /// <returns>The first and last sample column and row.</returns>
    public static (long XStart, long XEnd, long YStart, long YEnd) GetSampleBounds(in ScreenTriangle triangle)
    {
        var minX = Math.Min(triangle.X0, Math.Min(triangle.X1, triangle.X2));
        var maxX = Math.Max(triangle.X0, Math.Max(triangle.X1, triangle.X2));
        var minY = Math.Min(triangle.Y0, Math.Min(triangle.Y1, triangle.Y2));
        var maxY = Math.Max(triangle.Y0, Math.Max(triangle.Y1, triangle.Y2));

        // Sample i has its centre at 256 i + 128
        return (
            CeilDiv(minX - FixedHalf, FixedOne),
            FloorDiv(maxX - FixedHalf, FixedOne),
            CeilDiv(minY - FixedHalf, FixedOne),
            FloorDiv(maxY - FixedHalf, FixedOne));
    }
    /// <summary>
    ///     Projects, rounds and orients one clipped triangle, appending it to the output when it has non-zero area.
    /// </summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    /// <param name="source">The source triangle supplying priority and colors.</param>
    /// <param name="projection">The sample projection.</param>
    /// <param name="output">The output list.</param>
    private static void Emit(
        in ClipVertex a,
        in ClipVertex b,
        in ClipVertex c,
        in ViewTriangle source,
        in SampleProjection projection,
        List<ScreenTriangle> output)
    {
        // Project to sample space in double, then round to 24.8 fixed point
        var (ax, ay) = projection.ToSample(a);
        var (bx, by) = projection.ToSample(b);
        var (cx, cy) = projection.ToSample(c);
        if (!TryFixed(ax, out var fx0) || !TryFixed(ay, out var fy0)
            || !TryFixed(bx, out var fx1) || !TryFixed(by, out var fy1)
            || !TryFixed(cx, out var fx2) || !TryFixed(cy, out var fy2))
        {
            return;
        }

        var area2 = ((long)(fx1 - fx0) * (fy2 - fy0)) - ((long)(fy1 - fy0) * (fx2 - fx0));
        if (area2 == 0)
        {
            return;
        }

        // On a y-down screen a counter-clockwise (outward) triangle seen from the front has a negative area
        var front = area2 < 0;
        var d0 = (float)a.Depth;
        var d1 = (float)b.Depth;
        var d2 = (float)c.Depth;
        output.Add(front
            ? new ScreenTriangle
            {
                X0 = fx0,
                Y0 = fy0,
                X1 = fx2,
                Y1 = fy2,
                X2 = fx1,
                Y2 = fy1,
                Area2 = -area2,
                Depth0 = d0,
                Depth1 = d2,
                Depth2 = d1,
                PerspectiveDepth = !projection.IsOrthographic,
                Priority = source.Priority,
                BackFacing = false,
                Color = source.FrontColor,
                Leaf = source.Leaf,
                EnterColor = source.FrontEnterColor,
                ExitColor = source.FrontExitColor,
            }
            : new ScreenTriangle
            {
                X0 = fx0,
                Y0 = fy0,
                X1 = fx1,
                Y1 = fy1,
                X2 = fx2,
                Y2 = fy2,
                Area2 = area2,
                Depth0 = d0,
                Depth1 = d1,
                Depth2 = d2,
                PerspectiveDepth = !projection.IsOrthographic,
                Priority = source.Priority,
                BackFacing = true,
                Color = source.BackColor,
                Leaf = source.Leaf,
                EnterColor = source.BackEnterColor,
                ExitColor = source.BackExitColor,
            });
    }

    /// <summary>Rounds a sample coordinate to 24.8 fixed point, rejecting non-finite or out-of-range values.</summary>
    /// <param name="sample">The sample-space coordinate.</param>
    /// <param name="fixedValue">Receives the fixed-point value.</param>
    /// <returns><see langword="true"/> when the value is representable.</returns>
    private static bool TryFixed(double sample, out int fixedValue)
    {
        var scaled = Math.Round(sample * FixedScale);
        if (!double.IsFinite(scaled) || Math.Abs(scaled) > FixedLimit)
        {
            fixedValue = 0;
            return false;
        }

        fixedValue = (int)scaled;
        return true;
    }

    /// <summary>
    ///     Returns 1 when an edge with direction <paramref name="dx"/>, <paramref name="dy"/> owns
    ///     the samples lying exactly on it (a top or left edge in the normalized, y-down
    ///     orientation), otherwise 0. Adding the result to the edge function turns the
    ///     "positive, or zero on an owned edge" test into a plain "strictly positive" test.
    /// </summary>
    /// <param name="dx">The edge X extent.</param>
    /// <param name="dy">The edge Y extent.</param>
    /// <returns>The top-left bias, 0 or 1.</returns>
    private static long OwnsBoundary(long dx, long dy) => dy < 0 || (dy == 0 && dx > 0) ? 1 : 0;

    /// <summary>Divides rounding toward positive infinity.</summary>
    /// <param name="value">The dividend.</param>
    /// <param name="divisor">The positive divisor.</param>
    /// <returns>The ceiling of the quotient.</returns>
    private static long CeilDiv(long value, long divisor) => -FloorDiv(-value, divisor);

    /// <summary>Divides rounding toward negative infinity.</summary>
    /// <param name="value">The dividend.</param>
    /// <param name="divisor">The positive divisor.</param>
    /// <returns>The floor of the quotient.</returns>
    private static long FloorDiv(long value, long divisor)
    {
        var quotient = value / divisor;
        return (value % divisor != 0 && value < 0) ? quotient - 1 : quotient;
    }

    /// <summary>Determines whether all three coordinates of a vertex are finite.</summary>
    /// <param name="vertex">The vertex.</param>
    /// <returns><see langword="true"/> when finite.</returns>
    private static bool IsFinite(in ClipVertex vertex) =>
        double.IsFinite(vertex.X) && double.IsFinite(vertex.Y) && double.IsFinite(vertex.Z);
}

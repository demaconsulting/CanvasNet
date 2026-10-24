// cspell:ignore Binner

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The result of binning triangles into tiles: for every tile, the ascending list of indices of the triangles that may touch it.
/// </summary>
/// <remarks>Stored as one flat index array plus per-tile offsets (a compressed-row layout) so binning needs no per-tile allocations.</remarks>
internal sealed class TileBins
{
    /// <summary>Per-tile start offsets into <see cref="_indices"/>; one extra entry marks the end.</summary>
    private readonly int[] _offsets;

    /// <summary>The flat triangle index array.</summary>
    private readonly int[] _indices;

    /// <summary>
    ///     Creates a bin set.
    /// </summary>
    /// <param name="tilesX">The number of tile columns.</param>
    /// <param name="tilesY">The number of tile rows.</param>
    /// <param name="offsets">Per-tile start offsets (<c>tilesX * tilesY + 1</c> entries).</param>
    /// <param name="indices">The flat triangle index array.</param>
    public TileBins(int tilesX, int tilesY, int[] offsets, int[] indices)
    {
        TilesX = tilesX;
        TilesY = tilesY;
        _offsets = offsets;
        _indices = indices;
    }

    /// <summary>Gets the number of tile columns.</summary>
    public int TilesX { get; }

    /// <summary>Gets the number of tile rows.</summary>
    public int TilesY { get; }

    /// <summary>Gets the triangle indices of a tile in ascending order.</summary>
    /// <param name="tileIndex">The row-major tile index.</param>
    /// <returns>The triangle indices.</returns>
    public ReadOnlySpan<int> GetTriangles(int tileIndex) =>
        _indices.AsSpan(_offsets[tileIndex], _offsets[tileIndex + 1] - _offsets[tileIndex]);
}

/// <summary>
///     Assigns triangles to the screen tiles their bounding boxes overlap using a two-pass counting sort.
/// </summary>
/// <remarks>
///     Pass 1 counts the triangles per tile and prefix-sums the counts; pass 2 fills a flat index
///     array. Triangles are visited in ascending order in both passes, so every tile's list is in
///     ascending triangle order, which the depth-test tie rule relies on for determinism.
///     Stateless and thread-safe.
/// </remarks>
internal static class TileBinner
{
    /// <summary>
    ///     Bins the triangles.
    /// </summary>
    /// <param name="triangles">The prepared triangles in draw order.</param>
    /// <param name="tilesX">The number of tile columns.</param>
    /// <param name="tilesY">The number of tile rows.</param>
    /// <param name="tileSamples">The tile edge length in samples.</param>
    /// <returns>The bins.</returns>
    public static TileBins Bin(ReadOnlySpan<ScreenTriangle> triangles, int tilesX, int tilesY, int tileSamples)
    {
        var tileCount = tilesX * tilesY;
        var offsets = new int[tileCount + 1];

        // Pass 1: count triangles per tile (stored one slot ahead so a prefix sum yields the start offsets)
        for (var t = 0; t < triangles.Length; t++)
        {
            if (!TryGetTileRange(in triangles[t], tilesX, tilesY, tileSamples, out var range))
            {
                continue;
            }

            for (var ty = range.Y0; ty <= range.Y1; ty++)
            {
                for (var tx = range.X0; tx <= range.X1; tx++)
                {
                    offsets[(ty * tilesX) + tx + 1]++;
                }
            }
        }

        for (var i = 1; i < offsets.Length; i++)
        {
            offsets[i] += offsets[i - 1];
        }

        // Pass 2: fill, advancing a per-tile cursor
        var indices = new int[offsets[tileCount]];
        var cursor = new int[tileCount];
        Array.Copy(offsets, cursor, tileCount);
        for (var t = 0; t < triangles.Length; t++)
        {
            if (!TryGetTileRange(in triangles[t], tilesX, tilesY, tileSamples, out var range))
            {
                continue;
            }

            for (var ty = range.Y0; ty <= range.Y1; ty++)
            {
                for (var tx = range.X0; tx <= range.X1; tx++)
                {
                    indices[cursor[(ty * tilesX) + tx]++] = t;
                }
            }
        }

        return new TileBins(tilesX, tilesY, offsets, indices);
    }

    /// <summary>
    ///     Finds the inclusive range of tiles a triangle's sample bounding box overlaps.
    /// </summary>
    /// <param name="triangle">The triangle.</param>
    /// <param name="tilesX">The number of tile columns.</param>
    /// <param name="tilesY">The number of tile rows.</param>
    /// <param name="tileSamples">The tile edge length in samples.</param>
    /// <param name="range">Receives the tile range.</param>
    /// <returns><see langword="false"/> when the triangle can touch no tile.</returns>
    private static bool TryGetTileRange(in ScreenTriangle triangle, int tilesX, int tilesY, int tileSamples, out TileRect range)
    {
        var bounds = TriangleRasterizer.GetSampleBounds(in triangle);
        var gridWidth = (long)tilesX * tileSamples;
        var gridHeight = (long)tilesY * tileSamples;

        // No covered sample centre inside the bounding box, or entirely off the grid
        if (bounds.XEnd < bounds.XStart || bounds.YEnd < bounds.YStart
            || bounds.XEnd < 0 || bounds.YEnd < 0 || bounds.XStart >= gridWidth || bounds.YStart >= gridHeight)
        {
            range = default;
            return false;
        }

        range = new TileRect(
            (int)(Math.Max(bounds.XStart, 0) / tileSamples),
            (int)(Math.Max(bounds.YStart, 0) / tileSamples),
            (int)(Math.Min(bounds.XEnd, gridWidth - 1) / tileSamples),
            (int)(Math.Min(bounds.YEnd, gridHeight - 1) / tileSamples));
        return true;
    }
}

using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Per-worker sample buffers for one tile: depth, winner metadata and color for every sample.
/// </summary>
/// <remarks>
///     <para>
///         A tile is at most 32x32 output pixels, i.e. <c>(32 k)^2</c> samples at supersampling
///         <c>k</c>, so a workspace is a few hundred kilobytes regardless of image size and is
///         reused for every tile a worker renders (no per-tile allocation).
///     </para>
///     <para>
///         <b>Not thread-safe:</b> each worker thread owns its own instance.
///     </para>
/// </remarks>
internal sealed class TileWorkspace
{
    /// <summary>
    ///     Creates a workspace able to hold <paramref name="capacity"/> samples.
    /// </summary>
    /// <param name="capacity">The maximum number of samples in one tile (width times height).</param>
    public TileWorkspace(int capacity)
    {
        Depth = new float[capacity];
        Meta = new int[capacity];
        Colors = new Rgba32[capacity];
    }

    /// <summary>Gets the nearest depth recorded per sample (positive infinity = untouched background).</summary>
    public float[] Depth { get; }

    /// <summary>Gets the packed winner metadata per sample: <c>(priority &lt;&lt; 1) | backFacing</c>.</summary>
    public int[] Meta { get; }

    /// <summary>Gets the shaded color of the winning triangle per sample (valid where <see cref="Depth"/> is finite).</summary>
    public Rgba32[] Colors { get; }

    /// <summary>Gets the global sample column of the tile's first column.</summary>
    public int OriginX { get; private set; }

    /// <summary>Gets the global sample row of the tile's first row.</summary>
    public int OriginY { get; private set; }

    /// <summary>Gets the tile width in samples.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the tile height in samples.</summary>
    public int Height { get; private set; }

    /// <summary>
    ///     Starts a new tile: records its position and size and resets every sample to "no surface".
    /// </summary>
    /// <param name="originX">The global sample column of the tile's first column.</param>
    /// <param name="originY">The global sample row of the tile's first row.</param>
    /// <param name="width">The tile width in samples.</param>
    /// <param name="height">The tile height in samples.</param>
    public void Begin(int originX, int originY, int width, int height)
    {
        OriginX = originX;
        OriginY = originY;
        Width = width;
        Height = height;
        Array.Fill(Depth, float.PositiveInfinity, 0, width * height);
    }

    /// <summary>Maps a global sample coordinate inside the tile to its buffer index.</summary>
    /// <param name="sampleX">The global sample column.</param>
    /// <param name="sampleY">The global sample row.</param>
    /// <returns>The index into <see cref="Depth"/>, <see cref="Meta"/> and <see cref="Colors"/>.</returns>
    public int IndexOf(int sampleX, int sampleY) => ((sampleY - OriginY) * Width) + (sampleX - OriginX);
}

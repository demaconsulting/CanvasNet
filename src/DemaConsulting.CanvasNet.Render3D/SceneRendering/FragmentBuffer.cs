namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Per-worker, reusable store of every fragment (triangle-sample hit) of one tile, grouped by
///     sample, collected in an exact-sized count pass and fill pass.
/// </summary>
/// <remarks>
///     <para>
///         <b>Two passes.</b> The CSG path needs <em>all</em> fragments of a sample, not just the
///         nearest. Rasterization is deterministic, so the same triangles rasterized twice produce the
///         same fragments: <see cref="CountTriangle"/> (one call per binned triangle) counts hits per
///         sample, <see cref="EndCounting"/> turns the counts into offsets and makes the storage exactly
///         large enough, and <see cref="AddTriangle"/> (same triangles, same order) writes the fragments.
///         The result is a flat array with one contiguous run per sample, with no per-sample
///         allocation. The order inside a run is insertion order; consumers that need a defined order
///         (the CSG resolver) sort by their own total order.
///     </para>
///     <para>
///         <b>Budget.</b> The total number of fragments of the tile is checked after every counted
///         triangle against <see cref="MaxFragments"/>; exceeding it throws
///         <see cref="RenderBudgetExceededException"/> whose content depends only on the budget. The
///         total only grows during counting, so whether the budget is exceeded depends solely on the
///         set of triangles of the tile (never on triangle order or on which worker handled the tile).
///         Fragments beyond the far distance are kept (the resolver needs them to infer ray state).
///     </para>
///     <para>
///         <b>Reuse.</b> Arrays only ever grow, to exactly the size a tile needs, and are reused by the
///         next tile; they never exceed the budget. <b>Not thread-safe:</b> each worker owns one instance.
///     </para>
/// </remarks>
internal sealed class FragmentBuffer
{
    /// <summary>The state of the current tile.</summary>
    private enum Phase
    {
        /// <summary>No tile is being collected.</summary>
        Idle,

        /// <summary>The count pass is running.</summary>
        Counting,

        /// <summary>The fill pass is running or finished.</summary>
        Filling,
    }

    /// <summary>The stored fragments, one contiguous run per sample (the array may be longer than needed).</summary>
    private Fragment[] _fragments = [];

    /// <summary>The number of fragments per sample during counting.</summary>
    private int[] _counts = [];

    /// <summary>The start of each sample's run (<c>SampleCount + 1</c> entries).</summary>
    private int[] _offsets = [0];

    /// <summary>The next write position per sample during the fill pass.</summary>
    private int[] _cursor = [];

    /// <summary>The current phase.</summary>
    private Phase _phase = Phase.Idle;

    /// <summary>The sample rectangle of the current tile.</summary>
    private TileRect _tile;

    /// <summary>
    ///     Creates a buffer with the given fragment budget per tile.
    /// </summary>
    /// <param name="maxFragments">The maximum number of fragments one tile may hold (1 to <see cref="Array.MaxLength"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxFragments"/> is outside 1 to <see cref="Array.MaxLength"/>.</exception>
    public FragmentBuffer(long maxFragments)
    {
        // The storage is array-backed, so the budget can never usefully exceed the array limit
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFragments, 1L);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxFragments, (long)Array.MaxLength);
        MaxFragments = maxFragments;
    }

    /// <summary>
    ///     One fragment: the depth at which a triangle covers a sample, and the triangle's index.
    /// </summary>
    /// <param name="Depth">The interpolated view depth at the sample centre.</param>
    /// <param name="Triangle">The index of the triangle in the renderer's triangle list.</param>
    public readonly record struct Fragment(float Depth, int Triangle);

    /// <summary>Gets the fragment budget per tile.</summary>
    public long MaxFragments { get; }

    /// <summary>Gets the number of samples of the current tile.</summary>
    public int SampleCount => _tile.Width * _tile.Height;

    /// <summary>Gets the number of fragments counted for the current tile so far.</summary>
    public long FragmentCount { get; private set; }

    /// <summary>Gets the number of fragments the fragment storage can hold without reallocation.</summary>
    public int Capacity => _fragments.Length;

    /// <summary>
    ///     Starts collecting a new tile, discarding the previous one while keeping the storage.
    /// </summary>
    /// <param name="originX">The global sample column of the tile's first column.</param>
    /// <param name="originY">The global sample row of the tile's first row.</param>
    /// <param name="width">The tile width in samples.</param>
    /// <param name="height">The tile height in samples.</param>
    public void Begin(int originX, int originY, int width, int height)
    {
        _tile = new TileRect(originX, originY, originX + width, originY + height);
        var samples = width * height;

        // Grow the per-sample arrays when needed (they are reused across tiles)
        if (_counts.Length < samples)
        {
            _counts = new int[samples];
            _cursor = new int[samples];
            _offsets = new int[samples + 1];
        }

        Array.Clear(_counts, 0, samples);
        FragmentCount = 0;
        _phase = Phase.Counting;
    }

    /// <summary>
    ///     Counts the samples of the current tile covered by a triangle (count pass).
    /// </summary>
    /// <param name="triangle">The triangle to count.</param>
    /// <exception cref="InvalidOperationException">Thrown when the count pass is not running.</exception>
    /// <exception cref="RenderBudgetExceededException">Thrown when the tile now holds more than <see cref="MaxFragments"/> fragments.</exception>
    public void CountTriangle(in ScreenTriangle triangle)
    {
        EnsurePhase(Phase.Counting);

        // Count every covered sample of the tile
        var visitor = new CountVisitor(_counts, _tile);
        TriangleRasterizer.Rasterize(in triangle, _tile, ref visitor);
        FragmentCount += visitor.Total;

        // The total only grows, so exceeding the budget depends only on the tile's triangle set
        if (FragmentCount > MaxFragments)
        {
            _phase = Phase.Idle;
            throw new RenderBudgetExceededException(MaxFragments);
        }
    }

    /// <summary>
    ///     Ends the count pass: computes each sample's run and makes the storage exactly large enough.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the count pass is not running.</exception>
    public void EndCounting()
    {
        EnsurePhase(Phase.Counting);

        // Prefix-sum the counts into run offsets and write cursors
        var samples = SampleCount;
        _offsets[0] = 0;
        for (var i = 0; i < samples; i++)
        {
            _offsets[i + 1] = _offsets[i] + _counts[i];
            _cursor[i] = _offsets[i];
        }

        // Grow (never shrink) the fragment storage to exactly the tile's need
        var total = (int)FragmentCount;
        if (_fragments.Length < total)
        {
            _fragments = new Fragment[total];
        }

        _phase = Phase.Filling;
    }

    /// <summary>
    ///     Stores the fragments of a triangle (fill pass). Triangles must be added with the same
    ///     rasterization as they were counted.
    /// </summary>
    /// <param name="triangleIndex">The index of the triangle in the renderer's triangle list.</param>
    /// <param name="triangle">The triangle to store.</param>
    /// <exception cref="InvalidOperationException">Thrown when the count pass has not been ended.</exception>
    public void AddTriangle(int triangleIndex, in ScreenTriangle triangle)
    {
        EnsurePhase(Phase.Filling);

        var visitor = new FillVisitor(_fragments, _cursor, _tile, triangleIndex);
        TriangleRasterizer.Rasterize(in triangle, _tile, ref visitor);
    }

    /// <summary>
    ///     Gets the fragments of one sample for reading and in-place sorting.
    /// </summary>
    /// <param name="sampleIndex">The sample's index in the tile (row-major, as in <see cref="TileWorkspace.IndexOf"/>).</param>
    /// <returns>The fragments of the sample in insertion order.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the fill pass did not store exactly the counted fragments of the sample.</exception>
    public Span<Fragment> GetSample(int sampleIndex)
    {
        EnsurePhase(Phase.Filling);

        // A complete fill leaves every cursor at the end of its run
        var start = _offsets[sampleIndex];
        var end = _offsets[sampleIndex + 1];
        if (_cursor[sampleIndex] != end)
        {
            throw new InvalidOperationException("The fill pass did not store the counted fragments of the sample.");
        }

        return _fragments.AsSpan(start, end - start);
    }

    /// <summary>Verifies the buffer is in the expected phase.</summary>
    /// <param name="expected">The required phase.</param>
    /// <exception cref="InvalidOperationException">Thrown when the buffer is in a different phase.</exception>
    private void EnsurePhase(Phase expected)
    {
        if (_phase != expected)
        {
            throw new InvalidOperationException($"The fragment buffer is in the {_phase} phase; {expected} was required.");
        }
    }

    /// <summary>Count-pass visitor: increments the hit count of each covered sample.</summary>
    private struct CountVisitor : IFragmentVisitor
    {
        /// <summary>The per-sample counts.</summary>
        private readonly int[] _counts;

        /// <summary>The tile's first sample column.</summary>
        private readonly int _originX;

        /// <summary>The tile's first sample row.</summary>
        private readonly int _originY;

        /// <summary>The tile width in samples.</summary>
        private readonly int _width;

        /// <summary>Creates a visitor writing to the given counts.</summary>
        /// <param name="counts">The per-sample counts (already cleared).</param>
        /// <param name="tile">The tile's sample rectangle.</param>
        public CountVisitor(int[] counts, TileRect tile)
        {
            _counts = counts;
            _originX = tile.X0;
            _originY = tile.Y0;
            _width = tile.Width;
            Total = 0;
        }

        /// <summary>Gets the number of fragments visited.</summary>
        public long Total { get; private set; }

        /// <inheritdoc/>
        public void Visit(int sampleX, int sampleY, float depth)
        {
            _counts[((sampleY - _originY) * _width) + (sampleX - _originX)]++;
            Total++;
        }
    }

    /// <summary>Fill-pass visitor: appends a fragment to the run of each covered sample.</summary>
    private readonly struct FillVisitor : IFragmentVisitor
    {
        /// <summary>The fragment storage.</summary>
        private readonly Fragment[] _fragments;

        /// <summary>The next write position per sample.</summary>
        private readonly int[] _cursor;

        /// <summary>The tile's first sample column.</summary>
        private readonly int _originX;

        /// <summary>The tile's first sample row.</summary>
        private readonly int _originY;

        /// <summary>The tile width in samples.</summary>
        private readonly int _width;

        /// <summary>The index of the triangle being stored.</summary>
        private readonly int _triangle;

        /// <summary>Creates a visitor storing one triangle's fragments.</summary>
        /// <param name="fragments">The fragment storage.</param>
        /// <param name="cursor">The per-sample write positions.</param>
        /// <param name="tile">The tile's sample rectangle.</param>
        /// <param name="triangle">The triangle index.</param>
        public FillVisitor(Fragment[] fragments, int[] cursor, TileRect tile, int triangle)
        {
            _fragments = fragments;
            _cursor = cursor;
            _originX = tile.X0;
            _originY = tile.Y0;
            _width = tile.Width;
            _triangle = triangle;
        }

        /// <inheritdoc/>
        public void Visit(int sampleX, int sampleY, float depth)
        {
            var sample = ((sampleY - _originY) * _width) + (sampleX - _originX);
            _fragments[_cursor[sample]++] = new Fragment(depth, _triangle);
        }
    }
}

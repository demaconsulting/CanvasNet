using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     Resolves, per sample, which surface a ray sees in a CSG scene from the sample's fragment list.
/// </summary>
/// <remarks>
///     <para>
///         <b>Ray state.</b> Every closed leaf crosses the ray as matched pairs of front-facing
///         (entering, +1) and back-facing (leaving, -1) fragments, so a leaf's <em>signed crossing
///         counter</em> is positive exactly while the ray is inside the leaf. The counter at the start
///         of the ray is minus the sum of the signs of <em>all</em> the sample's fragments (a ray
///         starting inside a solid sees only the exit), which makes a camera inside material correct
///         as long as the near plane clips nothing. Fragments beyond the far distance take part in
///         this inference.
///     </para>
///     <para>
///         <b>Sweep.</b> Fragments are sorted by the total order (depth, triangle index), so the
///         result never depends on insertion order. They are then swept in <b>tie groups</b>: a group
///         starts at a fragment and contains every following fragment whose depth is within
///         <c>epsilon</c> of the group's <em>first</em> depth (coincident faces of different operands
///         differ only by float noise). All fragments of a group are applied to the counters
///         <em>atomically</em>, and only then is the compiled <see cref="CsgProgram"/> re-evaluated, and
///         only when some leaf changed state. Processing coincident faces one at a time would flip the
///         root between them and leave a speckle; for example, in <c>cube - box</c> with the box flush
///         on the cube's bottom, the box's and the cube's bottom back faces form one group, so the
///         result stays "outside" across the shared plane and no surface is drawn there.
///     </para>
///     <para>
///         <b>Visible surface.</b> The first group that changes the root result is the visible one. When
///         the root changes from outside to inside, the chosen fragment's <see cref="ScreenTriangle.EnterColor"/>
///         is used (for a back-facing fragment this is a cut surface of a subtrahend, whose color the
///         renderer already computed from the flipped normal and the subtrahend's own color); when it
///         changes from inside to outside (only possible when the ray starts inside), the
///         <see cref="ScreenTriangle.ExitColor"/> is used. Within the group the candidates are the
///         fragments whose leaf changed state; those whose leaf <em>alone</em> explains the change
///         (reverting just that leaf restores the previous result) are preferred, then the lower draw
///         priority and then the lower triangle index decide. If the chosen depth lies beyond the far
///         distance, nothing is visible.
///     </para>
///     <para>
///         <b>Cost and state.</b> The per-sample scratch (counters, leaf bitset, boolean stack) is
///         owned by the instance and only touched for leaves that have fragments, so a sample costs
///         O(fragments log fragments) plus one program evaluation per changing group.
///         <b>Not thread-safe:</b> each worker owns one resolver; the program is shared and immutable.
///     </para>
/// </remarks>
internal sealed class CsgResolver
{
    /// <summary>The compiled CSG expression.</summary>
    private readonly CsgProgram _program;

    /// <summary>The signed crossing counter per leaf (all zero between samples).</summary>
    private readonly int[] _counters;

    /// <summary>The "inside" state per leaf as a bitset (all clear between samples).</summary>
    private readonly ulong[] _bits;

    /// <summary>Marks the leaves that changed state in the current tie group (all clear between groups).</summary>
    private readonly bool[] _changed;

    /// <summary>The evaluation stack of the program.</summary>
    private readonly bool[] _stack;

    /// <summary>
    ///     Creates a resolver for a program.
    /// </summary>
    /// <param name="program">The compiled CSG expression.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="program"/> is <see langword="null"/>.</exception>
    public CsgResolver(CsgProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        _program = program;
        _counters = new int[program.LeafCount];
        _bits = new ulong[program.BitsetWords];
        _changed = new bool[program.LeafCount];
        _stack = new bool[program.MaxStackDepth];
    }

    /// <summary>
    ///     Resolves every sample of a tile and writes the visible surface into the tile workspace.
    /// </summary>
    /// <param name="fragments">The tile's fragments (filled), laid out like <paramref name="workspace"/>.</param>
    /// <param name="triangles">The renderer's triangle list that the fragments index.</param>
    /// <param name="workspace">The tile buffers (already begun) receiving depth, color and metadata.</param>
    /// <param name="epsilon">The depth tolerance of a tie group (the scene-relative epsilon of the z-buffer path).</param>
    /// <param name="farDepth">The far distance; surfaces deeper than this are not drawn.</param>
    /// <param name="cancellationToken">Observed once per sample row.</param>
    /// <exception cref="OperationCanceledException">Thrown when cancellation was requested.</exception>
    public void ResolveTile(
        FragmentBuffer fragments,
        ReadOnlySpan<ScreenTriangle> triangles,
        TileWorkspace workspace,
        float epsilon,
        float farDepth,
        CancellationToken cancellationToken)
    {
        var width = workspace.Width;
        var height = workspace.Height;
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                if (Resolve(fragments.GetSample(index), triangles, epsilon, farDepth, out var depth, out var color, out var meta))
                {
                    workspace.Depth[index] = depth;
                    workspace.Colors[index] = color;
                    workspace.Meta[index] = meta;
                }
                else
                {
                    workspace.Depth[index] = float.PositiveInfinity;
                }
            }
        }
    }

    /// <summary>
    ///     Resolves one sample from its fragments.
    /// </summary>
    /// <param name="fragments">The sample's fragments in any order; they are sorted in place.</param>
    /// <param name="triangles">The triangle list the fragments index; every <see cref="ScreenTriangle.Leaf"/> must be below the program's leaf count.</param>
    /// <param name="epsilon">The depth tolerance of a tie group (not negative).</param>
    /// <param name="farDepth">The far distance; a visible surface deeper than this is discarded.</param>
    /// <param name="depth">Receives the depth of the visible surface (positive infinity when none).</param>
    /// <param name="color">Receives the shade of the visible surface.</param>
    /// <param name="meta">Receives the winner metadata <c>(priority &lt;&lt; 1) | backFacing</c>.</param>
    /// <returns><see langword="true"/> when a surface is visible.</returns>
    public bool Resolve(
        Span<FragmentBuffer.Fragment> fragments,
        ReadOnlySpan<ScreenTriangle> triangles,
        float epsilon,
        float farDepth,
        out float depth,
        out Rgba32 color,
        out int meta)
    {
        depth = float.PositiveInfinity;
        color = default;
        meta = 0;
        if (fragments.IsEmpty)
        {
            return false;
        }

        // A total order makes the outcome independent of how the fragments were collected
        fragments.Sort(default(FragmentOrder));

        // The counter at the ray start is minus the sum of all signed crossings (front +1, back -1)
        foreach (var fragment in fragments)
        {
            ref readonly var triangle = ref triangles[fragment.Triangle];
            _counters[triangle.Leaf] += triangle.BackFacing ? 1 : -1;
        }

        foreach (var fragment in fragments)
        {
            var leaf = triangles[fragment.Triangle].Leaf;
            SetBit(leaf, _counters[leaf] > 0);
        }

        var inside = _program.Evaluate(_bits, _stack);
        var found = Sweep(fragments, triangles, epsilon, farDepth, inside, out var chosen, out var enters);

        // Restore the all-zero scratch state for the next sample
        foreach (var fragment in fragments)
        {
            var leaf = triangles[fragment.Triangle].Leaf;
            _counters[leaf] = 0;
            SetBit(leaf, false);
        }

        if (!found)
        {
            return false;
        }

        ref readonly var visible = ref triangles[chosen.Triangle];
        depth = chosen.Depth;
        color = enters ? visible.EnterColor : visible.ExitColor;
        meta = (visible.Priority << 1) | (visible.BackFacing ? 1 : 0);
        return true;
    }

    /// <summary>
    ///     Sweeps the sorted fragments in tie groups and finds the first group that changes the root result.
    /// </summary>
    /// <param name="fragments">The sorted fragments.</param>
    /// <param name="triangles">The triangle list.</param>
    /// <param name="epsilon">The tie-group tolerance.</param>
    /// <param name="farDepth">The far distance.</param>
    /// <param name="insideAtStart">The root result before the first fragment.</param>
    /// <param name="chosen">Receives the visible fragment.</param>
    /// <param name="enters">Receives <see langword="true"/> when the root changes from outside to inside.</param>
    /// <returns><see langword="true"/> when a visible surface was found within the far distance.</returns>
    private bool Sweep(
        Span<FragmentBuffer.Fragment> fragments,
        ReadOnlySpan<ScreenTriangle> triangles,
        float epsilon,
        float farDepth,
        bool insideAtStart,
        out FragmentBuffer.Fragment chosen,
        out bool enters)
    {
        chosen = default;
        enters = false;
        var inside = insideAtStart;
        var start = 0;

        while (start < fragments.Length)
        {
            // Nothing beyond the far distance can be visible
            var first = fragments[start].Depth;
            if (first > farDepth)
            {
                return false;
            }

            // The tie group is anchored at its first depth so it cannot chain across a gap
            var end = start + 1;
            while (end < fragments.Length && fragments[end].Depth - first <= epsilon)
            {
                end++;
            }

            // Apply the whole group to the counters, then see which leaves changed state
            var group = fragments[start..end];
            foreach (var fragment in group)
            {
                ref readonly var triangle = ref triangles[fragment.Triangle];
                _counters[triangle.Leaf] += triangle.BackFacing ? -1 : 1;
            }

            var anyChanged = false;
            foreach (var fragment in group)
            {
                var leaf = triangles[fragment.Triangle].Leaf;
                if ((_counters[leaf] > 0) != GetBit(leaf))
                {
                    _changed[leaf] = true;
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                foreach (var fragment in group)
                {
                    var leaf = triangles[fragment.Triangle].Leaf;
                    if (_changed[leaf])
                    {
                        SetBit(leaf, _counters[leaf] > 0);
                    }
                }

                var now = _program.Evaluate(_bits, _stack);
                if (now != inside)
                {
                    chosen = Choose(group, triangles, inside);
                    enters = now;
                    ClearChanged(group, triangles);
                    return chosen.Depth <= farDepth;
                }

                ClearChanged(group, triangles);
            }

            start = end;
        }

        return false;
    }

    /// <summary>
    ///     Chooses the visible fragment of the group that changed the root result.
    /// </summary>
    /// <param name="group">The tie group; the leaf bits already hold the state after the group.</param>
    /// <param name="triangles">The triangle list.</param>
    /// <param name="insideBefore">The root result before the group.</param>
    /// <returns>The fragment preferring a leaf that alone explains the change, then the lower priority, then the lower triangle index.</returns>
    private FragmentBuffer.Fragment Choose(
        Span<FragmentBuffer.Fragment> group,
        ReadOnlySpan<ScreenTriangle> triangles,
        bool insideBefore)
    {
        var best = default(FragmentBuffer.Fragment);
        var bestAlone = false;
        var bestPriority = 0;
        var haveBest = false;

        foreach (var fragment in group)
        {
            var leaf = triangles[fragment.Triangle].Leaf;
            if (!_changed[leaf])
            {
                continue;
            }

            // Revert just this leaf: if the root result returns to its previous value the leaf alone explains the change
            SetBit(leaf, !GetBit(leaf));
            var alone = _program.Evaluate(_bits, _stack) == insideBefore;
            SetBit(leaf, !GetBit(leaf));

            var priority = triangles[fragment.Triangle].Priority;
            if (!haveBest || IsBetter(alone, priority, fragment.Triangle, bestAlone, bestPriority, best.Triangle))
            {
                best = fragment;
                bestAlone = alone;
                bestPriority = priority;
                haveBest = true;
            }
        }

        return best;
    }

    /// <summary>
    ///     Compares two candidate fragments: a leaf that alone explains the change wins, then the
    ///     lower draw priority, then the lower triangle index.
    /// </summary>
    /// <param name="alone">Whether the candidate's leaf alone explains the change.</param>
    /// <param name="priority">The candidate's draw priority.</param>
    /// <param name="triangle">The candidate's triangle index.</param>
    /// <param name="bestAlone">Whether the current best's leaf alone explains the change.</param>
    /// <param name="bestPriority">The current best's draw priority.</param>
    /// <param name="bestTriangle">The current best's triangle index.</param>
    /// <returns><see langword="true"/> when the candidate replaces the current best.</returns>
    private static bool IsBetter(bool alone, int priority, int triangle, bool bestAlone, int bestPriority, int bestTriangle)
    {
        if (alone != bestAlone)
        {
            return alone;
        }

        if (priority != bestPriority)
        {
            return priority < bestPriority;
        }

        return triangle < bestTriangle;
    }

    /// <summary>Clears the per-group "changed" marks of the leaves touched by a group.</summary>
    /// <param name="group">The tie group.</param>
    /// <param name="triangles">The triangle list.</param>
    private void ClearChanged(Span<FragmentBuffer.Fragment> group, ReadOnlySpan<ScreenTriangle> triangles)
    {
        foreach (var fragment in group)
        {
            _changed[triangles[fragment.Triangle].Leaf] = false;
        }
    }

    /// <summary>Reads the inside state of a leaf.</summary>
    /// <param name="leaf">The leaf index.</param>
    /// <returns><see langword="true"/> when the leaf is inside.</returns>
    private bool GetBit(int leaf) => ((_bits[leaf >> 6] >> (leaf & 63)) & 1UL) != 0UL;

    /// <summary>Writes the inside state of a leaf.</summary>
    /// <param name="leaf">The leaf index.</param>
    /// <param name="value">The new state.</param>
    private void SetBit(int leaf, bool value)
    {
        var mask = 1UL << (leaf & 63);
        if (value)
        {
            _bits[leaf >> 6] |= mask;
        }
        else
        {
            _bits[leaf >> 6] &= ~mask;
        }
    }

    /// <summary>The total order of fragments: by depth, then by triangle index.</summary>
    private readonly struct FragmentOrder : IComparer<FragmentBuffer.Fragment>
    {
        /// <inheritdoc/>
        public int Compare(FragmentBuffer.Fragment x, FragmentBuffer.Fragment y)
        {
            var byDepth = x.Depth.CompareTo(y.Depth);
            return byDepth != 0 ? byDepth : x.Triangle.CompareTo(y.Triangle);
        }
    }
}

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
///         starting inside a solid sees only the exit), which makes a camera inside material correct.
///         The camera's near plane clips the fragments in front of it, so the counter at the start of
///         the ray is the state <em>at the near plane</em>, exactly what the remaining fragments imply:
///         the result is the CSG solid clipped to the near plane of the view frustum (no cap surfaces are
///         drawn on the near plane; the cut is simply open). A camera inside a hollow room therefore
///         sees the room's walls. Fragments beyond the far distance take part in
///         this inference.
///     </para>
///     <para>
///         <b>Sweep.</b> Fragments are sorted by the total order (depth, triangle index), so the
///         result never depends on insertion order. They are then swept in <b>tie groups</b>: a group
///         starts at a fragment and contains the following fragments whose depth gap to the group's
///         <em>first</em> fragment is within <c>epsilon</c> (coincident faces of different operands
///         differ only by float noise). Fragments of <em>different mesh instances</em> are also grouped
///         when the gap is within <c>epsilon</c> plus the depth noise of one fixed-point vertex quantum
///         on their two triangles (their <see cref="ScreenTriangle.DepthSlope"/> times 1/128 sample,
///         capped at 1000 epsilons) <em>and the two triangles are plausibly the same plane</em> (parallel
///         normals, either orientation, and a plane distance within <c>epsilon</c>): two meshes whose
///         coincident faces are equal only up to rounding rasterize from vertices that may differ by a quantum,
///         and at oblique angles that moves their depths apart by far more than <c>epsilon</c>, whereas
///         genuinely separate, closely spaced surfaces are different planes and keep their own events.
///         Triangles of one instance share rounded vertices, so they only ever use <c>epsilon</c>. All fragments of a group are applied to the counters
///         <em>atomically</em>, and only then is the compiled <see cref="CsgProgram"/> re-evaluated, and
///         only when some leaf changed state. Processing coincident faces one at a time would flip the
///         root between them and leave a speckle; for example, in <c>cube - box</c> with the box flush
///         on the cube's bottom, the box's and the cube's bottom back faces form one group, so the
///         result stays "outside" across the shared plane and no surface is drawn there.
///     </para>
///     <para>
///         <b>Visible surfaces (layers).</b> Every group that changes a program's result is a <b>layer</b>.
///         When the result changes from outside to inside, the chosen fragment's <see cref="ScreenTriangle.EnterColor"/>
///         is used (for a back-facing fragment this is a cut surface of a subtrahend, or the far wall of a
///         translucent solid, whose color the renderer already computed from the flipped normal and the
///         triangle's own color). The <see cref="ScreenTriangle.ExitColor"/> is used only for the
///         <em>first</em> change of each program (the main program or an overlay program) when it is an exit
///         (the ray starts inside that solid and the viewer sees the inside of material, in the back-face color
///         at the owning appearance's alpha); every later change uses <see cref="ScreenTriangle.EnterColor"/>. Within the group the candidates are the
///         fragments whose leaf changed state; those whose leaf <em>alone</em> explains the change
///         (reverting just that leaf restores the previous result) are preferred, then the lower draw
///         priority and then the lower triangle index decide. If the chosen depth lies beyond the far
///         distance, the sweep ends.
///     </para>
///     <para>
///         <b>Transparency and overlays.</b> The sweep does not stop at the first layer. The layer colors
///         (straight alpha, the alpha of the owning appearance) are accumulated front to back by a
///         <see cref="LayerCompositor"/> until an opaque layer is found, the far distance is passed or the
///         fragments end, and the remainder is composited over the background color. Besides the main program
///         (index 0) the resolver evaluates any number of <b>overlay programs</b> over their own leaves: each is an
///         independent solid whose layers are interleaved by depth with those of the main solid. Within one
///         tie group the layers of overlay programs are applied before the main program's (then by draw
///         priority and triangle index), so an overlay in front of a coincident main face tints it rather than
///         being hidden by it. The workspace receives the composite color with the depth and metadata of the
///         first layer; a sample without any layer is background (infinite depth). With a single program whose
///         first layer is opaque the result equals the classic first-visible-surface result.
///     </para>
///     <para>
///         <b>Cost and state.</b> The per-sample scratch (counters, leaf bitset, boolean stack) is
///         owned by the instance and only touched for leaves that have fragments, so a sample costs
///         O(fragments log fragments) plus one program evaluation per changing group and program.
///         <b>Not thread-safe:</b> each worker owns one resolver; the programs are shared and immutable.
///     </para>
/// </remarks>
internal sealed class CsgResolver
{
    /// <summary>
    ///     The depth error, in samples, that rounding the vertices of two independently placed triangles to
    ///     24.8 fixed point can cause: one quantum is 1/256 sample, so two quanta (1/128) cover the rounding of
    ///     both triangles; multiplied by the depth slope per sample it gives the depth noise of coincident faces.
    /// </summary>
    private const float SlopeQuantum = 1.0f / 128.0f;

    /// <summary>The largest slope-derived tolerance, as a multiple of the base tolerance (bounds the merging of grazing faces).</summary>
    private const float SlopeToleranceCap = 1000.0f;

    /// <summary>The largest deviation of the absolute normal dot product from one (about 0.08 degrees) for two faces to count as parallel.</summary>
    private const double NormalTolerance = 1e-6;

    /// <summary>The compiled CSG expressions: the main program first, then the overlay programs.</summary>
    private readonly CsgProgram[] _programs;

    /// <summary>The background color the remaining transmittance of a sample is composited over.</summary>
    private readonly Rgba32 _background;

    /// <summary>The index of the program that owns each leaf, or <c>-1</c> for a leaf no program reads.</summary>
    private readonly int[] _programOfLeaf;

    /// <summary>The current result of each program during a sample.</summary>
    private readonly bool[] _inside;

    /// <summary>Marks the programs that own a leaf that changed in the current tie group (all clear between groups).</summary>
    private readonly bool[] _touched;

    /// <summary>The layers produced by the current tie group (one at most per program).</summary>
    private readonly Layer[] _layers;

    /// <summary>The serial of the sample in which each program last changed its result (zero before any); a program's first change in a sample is the one that still differs from the current serial.</summary>
    private readonly long[] _firstChangeSample;

    /// <summary>The serial of the sample being resolved (incremented for every sample, so it is never zero while resolving).</summary>
    private long _sample;

    /// <summary>The signed crossing counter per leaf (all zero between samples).</summary>
    private readonly int[] _counters;

    /// <summary>The "inside" state per leaf as a bitset (all clear between samples).</summary>
    private readonly ulong[] _bits;

    /// <summary>Marks the leaves that changed state in the current tie group (all clear between groups).</summary>
    private readonly bool[] _changed;

    /// <summary>The evaluation stack shared by the programs.</summary>
    private readonly bool[] _stack;

    /// <summary>
    ///     Creates a resolver for a single program over a transparent black background.
    /// </summary>
    /// <param name="program">The compiled CSG expression.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="program"/> is <see langword="null"/>.</exception>
    public CsgResolver(CsgProgram program)
        : this(WrapProgram(program))
    {
    }

    /// <summary>
    ///     Creates a resolver for a main program and any number of overlay programs.
    /// </summary>
    /// <param name="programs">The programs: index 0 is the main program, the others are overlay bodies; they share one leaf numbering and every leaf belongs to at most one program.</param>
    /// <param name="background">The color the remainder of a sample is composited over (its alpha is honored).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="programs"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="programs"/> is empty, contains <see langword="null"/>, or two different programs read the same leaf.</exception>
    public CsgResolver(IReadOnlyList<CsgProgram> programs, Rgba32 background = default)
    {
        ArgumentNullException.ThrowIfNull(programs);
        if (programs.Count == 0 || programs.Any(p => p is null))
        {
            throw new ArgumentException("At least one program is required and none may be null.", nameof(programs));
        }

        _programs = [.. programs];
        _background = background;
        var leafCount = _programs.Max(p => p.LeafCount);
        _programOfLeaf = new int[leafCount];
        Array.Fill(_programOfLeaf, -1);
        var maxStack = 0;
        for (var index = 0; index < _programs.Length; index++)
        {
            maxStack = Math.Max(maxStack, _programs[index].MaxStackDepth);
            foreach (var instruction in _programs[index].Instructions)
            {
                if (instruction.Op != CsgOpCode.PushLeaf)
                {
                    continue;
                }

                if (_programOfLeaf[instruction.Operand] >= 0 && _programOfLeaf[instruction.Operand] != index)
                {
                    throw new ArgumentException("A leaf must belong to exactly one program.", nameof(programs));
                }

                _programOfLeaf[instruction.Operand] = index;
            }
        }

        _inside = new bool[_programs.Length];
        _touched = new bool[_programs.Length];
        _layers = new Layer[_programs.Length];
        _firstChangeSample = new long[_programs.Length];
        _counters = new int[leafCount];
        _bits = new ulong[(leafCount + 63) >> 6];
        _changed = new bool[leafCount];
        _stack = new bool[maxStack];
    }

    /// <summary>Wraps one program into the program list (validating it).</summary>
    /// <param name="program">The program.</param>
    /// <returns>The single-element list.</returns>
    private static CsgProgram[] WrapProgram(CsgProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return [program];
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
    /// <param name="color">Receives the composite color of the visible layers over the background (the shade of the visible surface when it is opaque).</param>
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
        _sample++;

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

        for (var p = 0; p < _programs.Length; p++)
        {
            _inside[p] = _programs[p].Evaluate(_bits, _stack);
        }

        var compositor = default(LayerCompositor);
        var found = Sweep(fragments, triangles, epsilon, farDepth, ref compositor, out var first);

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

        // The first layer supplies depth and metadata; the color is the composite of all layers over the background
        ref readonly var visible = ref triangles[first.Triangle];
        depth = first.Depth;
        color = compositor.Finish(_background);
        meta = (visible.Priority << 1) | (visible.BackFacing ? 1 : 0);
        return true;
    }

    /// <summary>
    ///     Sweeps the sorted fragments in tie groups and accumulates the layers: every group that changes the result of a
    ///     program contributes one layer, until an opaque layer, the far distance or the end of the fragments.
    /// </summary>
    /// <param name="fragments">The sorted fragments.</param>
    /// <param name="triangles">The triangle list.</param>
    /// <param name="epsilon">The tie-group tolerance.</param>
    /// <param name="farDepth">The far distance.</param>
    /// <param name="compositor">Receives the layers front to back.</param>
    /// <param name="first">Receives the fragment of the first layer (the nearest).</param>
    /// <returns><see langword="true"/> when at least one layer was found within the far distance.</returns>
    private bool Sweep(
        Span<FragmentBuffer.Fragment> fragments,
        ReadOnlySpan<ScreenTriangle> triangles,
        float epsilon,
        float farDepth,
        ref LayerCompositor compositor,
        out FragmentBuffer.Fragment first)
    {
        first = default;
        var found = false;
        var start = 0;

        while (start < fragments.Length)
        {
            // Nothing beyond the far distance can be visible
            if (fragments[start].Depth > farDepth)
            {
                break;
            }

            // The tie group is anchored at its first fragment so it cannot chain across a gap
            var end = start + 1;
            while (end < fragments.Length && JoinsGroup(fragments, start, end, triangles, epsilon))
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
                var layerCount = 0;
                var beyondFar = false;
                foreach (var fragment in group)
                {
                    var leaf = triangles[fragment.Triangle].Leaf;
                    if (_changed[leaf])
                    {
                        SetBit(leaf, _counters[leaf] > 0);
                        if (_programOfLeaf[leaf] >= 0)
                        {
                            _touched[_programOfLeaf[leaf]] = true;
                        }
                    }
                }

                // Re-evaluate, in ascending program order, every program that owns a changed leaf
                for (var p = 0; p < _programs.Length; p++)
                {
                    if (!_touched[p])
                    {
                        continue;
                    }

                    _touched[p] = false;
                    var now = _programs[p].Evaluate(_bits, _stack);
                    if (now == _inside[p])
                    {
                        continue;
                    }

                    var chosen = Choose(group, triangles, p, _inside[p]);
                    _inside[p] = now;
                    if (chosen.Depth > farDepth)
                    {
                        beyondFar = true;
                        continue;
                    }

                    // Only the first change of a program can be an exit from a ray that started inside it
                    ref readonly var visible = ref triangles[chosen.Triangle];
                    var useExit = !now && _firstChangeSample[p] != _sample;
                    _firstChangeSample[p] = _sample;
                    _layers[layerCount++] = new Layer(chosen, visible.Priority, p != 0, useExit ? visible.ExitColor : visible.EnterColor);
                }

                ClearChanged(group, triangles);

                // Overlay layers go in front of the main layer of the same group, then by priority and triangle index
                SortLayers(layerCount);
                for (var i = 0; i < layerCount; i++)
                {
                    if (!found)
                    {
                        first = _layers[i].Fragment;
                        found = true;
                    }

                    compositor.Add(_layers[i].Color);
                    if (compositor.IsOpaque)
                    {
                        return true;
                    }
                }

                if (beyondFar)
                {
                    break;
                }
            }

            start = end;
        }

        return found;
    }

    /// <summary>
    ///     Orders the first <paramref name="count"/> layers of the current group: overlay programs first, then the lower
    ///     draw priority, then the lower triangle index (insertion sort; at most one layer per program).
    /// </summary>
    /// <param name="count">The number of layers.</param>
    private void SortLayers(int count)
    {
        for (var i = 1; i < count; i++)
        {
            var layer = _layers[i];
            var j = i - 1;
            while (j >= 0 && Precedes(layer, _layers[j]))
            {
                _layers[j + 1] = _layers[j];
                j--;
            }

            _layers[j + 1] = layer;
        }
    }

    /// <summary>Decides whether layer <paramref name="a"/> is applied before layer <paramref name="b"/> within a tie group.</summary>
    /// <param name="a">The first layer.</param>
    /// <param name="b">The second layer.</param>
    /// <returns><see langword="true"/> when <paramref name="a"/> goes first.</returns>
    private static bool Precedes(in Layer a, in Layer b)
    {
        if (a.IsOverlay != b.IsOverlay)
        {
            return a.IsOverlay;
        }

        if (a.Priority != b.Priority)
        {
            return a.Priority < b.Priority;
        }

        return a.Fragment.Triangle < b.Fragment.Triangle;
    }

    /// <summary>One layer found in a tie group.</summary>
    /// <param name="Fragment">The chosen fragment.</param>
    /// <param name="Priority">The draw priority of the fragment's triangle.</param>
    /// <param name="IsOverlay">Whether the layer belongs to an overlay program.</param>
    /// <param name="Color">The layer's straight-alpha shade.</param>
    private readonly record struct Layer(FragmentBuffer.Fragment Fragment, int Priority, bool IsOverlay, Rgba32 Color);

    /// <summary>
    ///     Chooses the visible fragment of the group that changed the result of a program.
    /// </summary>
    /// <param name="group">The tie group; the leaf bits already hold the state after the group.</param>
    /// <param name="triangles">The triangle list.</param>
    /// <param name="program">The index of the program whose result changed; only fragments of its leaves are candidates.</param>
    /// <param name="insideBefore">The program's result before the group.</param>
    /// <returns>The fragment preferring a leaf that alone explains the change, then the lower priority, then the lower triangle index.</returns>
    private FragmentBuffer.Fragment Choose(
        Span<FragmentBuffer.Fragment> group,
        ReadOnlySpan<ScreenTriangle> triangles,
        int program,
        bool insideBefore)
    {
        var best = default(FragmentBuffer.Fragment);
        var bestAlone = false;
        var bestPriority = 0;
        var haveBest = false;

        foreach (var fragment in group)
        {
            var leaf = triangles[fragment.Triangle].Leaf;
            if (!_changed[leaf] || _programOfLeaf[leaf] != program)
            {
                continue;
            }

            // Revert just this leaf: if the program result returns to its previous value the leaf alone explains the change
            SetBit(leaf, !GetBit(leaf));
            var alone = _programs[program].Evaluate(_bits, _stack) == insideBefore;
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
    ///     Decides whether a fragment joins the tie group that starts at <paramref name="start"/> and
    ///     currently extends over <c>[start, end)</c>.
    /// </summary>
    /// <param name="fragments">The sorted fragments.</param>
    /// <param name="start">The index of the group's first (anchor) fragment.</param>
    /// <param name="end">The index of the candidate (the group so far is <c>[start, end)</c>).</param>
    /// <param name="triangles">The triangle list.</param>
    /// <param name="epsilon">The base depth tolerance.</param>
    /// <returns>
    ///     <see langword="true"/> when the candidate's depth gap to the anchor is within the base tolerance or,
    ///     provided no fragment of the group is from the candidate's own mesh instance and the candidate's triangle
    ///     is coplanar with the anchor's (see <see cref="AreCoplanar"/>), within the base
    ///     tolerance plus the depth noise that one fixed-point vertex quantum causes on the two triangles
    ///     (their slopes times <see cref="SlopeQuantum"/>, capped at <see cref="SlopeToleranceCap"/> base tolerances).
    /// </returns>
    private static bool JoinsGroup(
        Span<FragmentBuffer.Fragment> fragments,
        int start,
        int end,
        ReadOnlySpan<ScreenTriangle> triangles,
        float epsilon)
    {
        var anchor = fragments[start];
        var candidate = fragments[end];
        var gap = candidate.Depth - anchor.Depth;
        if (gap <= epsilon)
        {
            return true;
        }

        // Triangles of one instance share rounded vertices, so their depths agree to the base tolerance and a
        // fragment of an instance already in the group is a different event (e.g. the far side of a thin fold);
        // independently placed meshes with coincident faces can disagree by a vertex quantum times the slope
        ref readonly var b = ref triangles[candidate.Triangle];
        for (var i = start; i < end; i++)
        {
            if (triangles[fragments[i].Triangle].Priority == b.Priority)
            {
                return false;
            }
        }

        // Widen only for faces that are plausibly the same plane: a depth gap that merely follows from the slope
        // would otherwise also swallow genuinely separate, closely spaced oblique surfaces
        if (!AreCoplanar(in triangles[anchor.Triangle], in b, epsilon))
        {
            return false;
        }

        var noise = Math.Min((triangles[anchor.Triangle].DepthSlope + b.DepthSlope) * SlopeQuantum, SlopeToleranceCap * epsilon);
        return gap <= epsilon + noise;
    }

    /// <summary>
    ///     Decides whether two triangles lie in the same plane within a tolerance: their unit normals are
    ///     parallel (either orientation) and the nearer of the two centroid-to-plane distances is within the tolerance.
    /// </summary>
    /// <remarks>
    ///     Measuring from the larger triangle's plane (the smaller distance) keeps the angular noise of a tiny
    ///     triangle's normal from rejecting a genuinely coincident face. A triangle without a plane (zero normal)
    ///     is never coplanar with anything.
    /// </remarks>
    /// <param name="a">The first triangle.</param>
    /// <param name="b">The second triangle.</param>
    /// <param name="tolerance">The largest plane distance counted as coincident (the base depth tolerance).</param>
    /// <returns><see langword="true"/> when the triangles are plausibly coincident.</returns>
    private static bool AreCoplanar(in ScreenTriangle a, in ScreenTriangle b, float tolerance)
    {
        var na = a.PlaneNormal;
        var nb = b.PlaneNormal;
        var parallel = Math.Abs(((double)na.X * nb.X) + ((double)na.Y * nb.Y) + ((double)na.Z * nb.Z));
        if (!(parallel >= 1.0 - NormalTolerance))
        {
            return false;
        }

        var dx = (double)b.PlanePoint.X - a.PlanePoint.X;
        var dy = (double)b.PlanePoint.Y - a.PlanePoint.Y;
        var dz = (double)b.PlanePoint.Z - a.PlanePoint.Z;
        var fromA = Math.Abs((na.X * dx) + (na.Y * dy) + (na.Z * dz));
        var fromB = Math.Abs((nb.X * dx) + (nb.Y * dy) + (nb.Z * dz));
        return Math.Min(fromA, fromB) <= tolerance;
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

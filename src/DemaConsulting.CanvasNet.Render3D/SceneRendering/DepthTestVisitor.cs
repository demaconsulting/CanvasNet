namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The z-buffer visitor used for rendering: keeps the nearest fragment per sample with a
///     deterministic tolerance-based tie rule.
/// </summary>
/// <remarks>
///     <para>
///         <b>Tie rule.</b> With <c>eps</c> the depth tolerance, a new fragment at depth <c>d</c>
///         replaces the stored depth <c>old</c> when <c>d &lt; old - eps</c> and is discarded when
///         <c>d &gt; old + eps</c>. Within the tolerance the fragments are treated as coincident
///         (coplanar faces of different meshes differ only by float noise): a front-facing fragment
///         beats a back-facing one, then the lower <c>Priority</c> (earlier in scene pre-order)
///         wins, and otherwise the earlier-rasterized (lower triangle index) fragment stays. The
///         stored depth becomes <c>min(old, d)</c>. This is not a strict weak order but the
///         evaluation order is fixed (triangles are binned in ascending index order and tiles are
///         independent), so the outcome is deterministic.
///     </para>
///     <para>
///         The visitor is a small struct holding references to the worker's <see cref="TileWorkspace"/>
///         and one triangle's attributes; create one per triangle.
///     </para>
/// </remarks>
internal readonly struct DepthTestVisitor : IFragmentVisitor
{
    /// <summary>The tile buffers being written.</summary>
    private readonly TileWorkspace _workspace;

    /// <summary>The triangle being rasterized.</summary>
    private readonly ScreenTriangle _triangle;

    /// <summary>The packed winner metadata of the triangle: <c>(priority &lt;&lt; 1) | backFacing</c>.</summary>
    private readonly int _meta;

    /// <summary>The depth tolerance below which two fragments are treated as coincident.</summary>
    private readonly float _epsilon;

    /// <summary>The far distance beyond which fragments are discarded.</summary>
    private readonly float _farDepth;

    /// <summary>
    ///     Creates a visitor for one triangle.
    /// </summary>
    /// <param name="workspace">The tile buffers (already begun for the current tile).</param>
    /// <param name="triangle">The triangle being rasterized.</param>
    /// <param name="epsilon">The depth tie tolerance (not negative).</param>
    /// <param name="farDepth">The far distance; deeper fragments are discarded.</param>
    public DepthTestVisitor(TileWorkspace workspace, in ScreenTriangle triangle, float epsilon, float farDepth)
    {
        _workspace = workspace;
        _triangle = triangle;
        _meta = (triangle.Priority << 1) | (triangle.BackFacing ? 1 : 0);
        _epsilon = epsilon;
        _farDepth = farDepth;
    }

    /// <inheritdoc/>
    public void Visit(int sampleX, int sampleY, float depth)
    {
        // Beyond the far distance nothing is drawn
        if (depth > _farDepth)
        {
            return;
        }

        var index = _workspace.IndexOf(sampleX, sampleY);
        var stored = _workspace.Depth[index];

        if (depth < stored - _epsilon)
        {
            // Clearly nearer (also true for untouched samples, whose depth is +infinity)
            Store(index, depth);
        }
        else if (depth <= stored + _epsilon)
        {
            // Coincident within tolerance: front beats back, then lower priority wins; otherwise the earlier fragment stays
            var oldMeta = _workspace.Meta[index];
            var oldBack = (oldMeta & 1) != 0;
            var oldPriority = oldMeta >> 1;
            var newBack = _triangle.BackFacing;
            var newWins = newBack != oldBack ? !newBack : _triangle.Priority < oldPriority;

            if (newWins)
            {
                Store(index, Math.Min(stored, depth));
            }
            else
            {
                _workspace.Depth[index] = Math.Min(stored, depth);
            }
        }
    }

    /// <summary>Records this triangle as the winner of a sample.</summary>
    /// <param name="index">The sample's buffer index.</param>
    /// <param name="depth">The depth to store.</param>
    private void Store(int index, float depth)
    {
        _workspace.Depth[index] = depth;
        _workspace.Meta[index] = _meta;
        _workspace.Colors[index] = _triangle.Color;
    }
}

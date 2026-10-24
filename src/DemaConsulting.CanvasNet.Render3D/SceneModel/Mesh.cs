// cspell:ignore Orientable

using System.Collections.ObjectModel;

namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     An immutable indexed triangle mesh in double-precision model space.
/// </summary>
/// <remarks>
///     <para>
///         The constructor validates and defensively copies its inputs, so a mesh can never change
///         after construction and can be shared freely between scene nodes and threads.
///         <b>Winding contract:</b> triangles are counter-clockwise when seen from outside, i.e. the
///         right-hand-rule normal of <c>(v1 - v0) x (v2 - v0)</c> points out of the solid. A closed
///         mesh with that orientation has a positive <see cref="SignedVolume"/>; callers that
///         receive an inside-out mesh can repair it with <see cref="Reverse"/>.
///     </para>
///     <para>
///         A mesh may have zero triangles (it then has empty <see cref="Bounds"/> and renders
///         nothing). All members are thread-safe.
///     </para>
/// </remarks>
public sealed class Mesh
{
    /// <summary>The vertex positions (owned; never exposed mutably).</summary>
    private readonly Vector3D[] _vertices;

    /// <summary>The triangle index list, three indices per triangle (owned; never exposed mutably).</summary>
    private readonly int[] _indices;

    /// <summary>Read-only wrapper over <see cref="_vertices"/> handed to callers.</summary>
    private readonly ReadOnlyCollection<Vector3D> _vertexView;

    /// <summary>Read-only wrapper over <see cref="_indices"/> handed to callers.</summary>
    private readonly ReadOnlyCollection<int> _indexView;

    /// <summary>Lazily computed topology defects plus one (0 = unknown; idempotent, so benign races are harmless).</summary>
    private int _defectState;

    /// <summary>Lazily computed operand defects (topology plus geometric degeneracy) plus one (0 = unknown; idempotent).</summary>
    private int _operandDefectState;

    /// <summary>
    ///     Creates a mesh from vertex positions and triangle indices, copying both.
    /// </summary>
    /// <param name="vertices">The vertex positions; every component must be finite.</param>
    /// <param name="triangleIndices">
    ///     The triangle index list; its length must be a multiple of three and every index must
    ///     address a vertex. Triangles are counter-clockwise when seen from outside.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the index count is not a multiple of three or a vertex is not finite.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an index does not address a vertex.</exception>
    public Mesh(IEnumerable<Vector3D> vertices, IEnumerable<int> triangleIndices)
        : this(CopyOf(vertices, nameof(vertices)), CopyOf(triangleIndices, nameof(triangleIndices)))
    {
    }

    /// <summary>
    ///     Creates a mesh that takes ownership of already-private arrays (no copy), validating them.
    /// </summary>
    /// <param name="vertices">Privately owned vertex array.</param>
    /// <param name="indices">Privately owned index array.</param>
    private Mesh(Vector3D[] vertices, int[] indices)
    {
        // Validate structure before storing anything so a bad mesh can never exist
        if (indices.Length % 3 != 0)
        {
            throw new ArgumentException("The triangle index count must be a multiple of three.", nameof(indices));
        }

        var bounds = BoundingBox3D.Empty;
        foreach (var vertex in vertices)
        {
            if (!vertex.IsFinite)
            {
                throw new ArgumentException("Every vertex component must be finite.", nameof(vertices));
            }

            bounds = bounds.Union(vertex);
        }

        foreach (var index in indices)
        {
            if ((uint)index >= (uint)vertices.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(indices), index, "A triangle index does not address a vertex.");
            }
        }

        _vertices = vertices;
        _indices = indices;
        _vertexView = Array.AsReadOnly(vertices);
        _indexView = Array.AsReadOnly(indices);
        Bounds = bounds;
    }

    /// <summary>Gets the vertex positions as a read-only list.</summary>
    public IReadOnlyList<Vector3D> Vertices => _vertexView;

    /// <summary>Gets the triangle index list (three indices per triangle) as a read-only list.</summary>
    public IReadOnlyList<int> TriangleIndices => _indexView;

    /// <summary>Gets the number of vertices.</summary>
    public int VertexCount => _vertices.Length;

    /// <summary>Gets the number of triangles.</summary>
    public int TriangleCount => _indices.Length / 3;

    /// <summary>Gets the axis-aligned bounds of all vertices (<see cref="BoundingBox3D.Empty"/> when there are none).</summary>
    public BoundingBox3D Bounds { get; }

    /// <summary>
    ///     Gets a value indicating whether the mesh is a closed, consistently oriented surface:
    ///     every directed edge occurs once and is matched by its reverse (see the winding
    ///     contract in the type remarks). Computed lazily on first use and cached.
    /// </summary>
    public bool IsClosed => _indices.Length > 0 && TopologyDefects == MeshTopologyDefects.None;

    /// <summary>
    ///     Gets the topology defects of the mesh (boundary edges, non-manifold edges, inconsistent
    ///     winding, degenerate triangles), computed lazily on first use and cached; used by the
    ///     renderer to report unsuitable constructive-solid-geometry operands.
    /// </summary>
    internal MeshTopologyDefects TopologyDefects
    {
        get
        {
            // The analysis is deterministic, so a racing recomputation stores the same value
            var state = Volatile.Read(ref _defectState);
            if (state == 0)
            {
                state = (int)MeshTopology.Analyze(_indices) + 1;
                Volatile.Write(ref _defectState, state);
            }

            return (MeshTopologyDefects)(state - 1);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the mesh is a defect-free closed surface that is wound
    ///     inside out: its signed volume is negative by more than the rounding error bound of its own
    ///     computation (see <see cref="MeshTopology.SignedVolume(ReadOnlySpan{Vector3D}, ReadOnlySpan{int}, Vector3D, out double)"/>).
    ///     The criterion is scale-aware, so a thin but valid solid (for example a reversed
    ///     1 x 1 x 1e-12 box) is detected while a zero-volume surface is not. A multi-shell mesh with
    ///     cavities (inner shells wound inward) keeps a positive total volume and is therefore not inverted.
    /// </summary>
    internal bool IsInverted
    {
        get
        {
            if (!IsClosed)
            {
                return false;
            }

            var volume = MeshTopology.SignedVolume(_vertices, _indices, Bounds.Center, out var errorBound);
            return volume < -errorBound;
        }
    }

    /// <summary>
    ///     Gets the defects that make the mesh unsuitable as a constructive-solid-geometry operand: the
    ///     <see cref="TopologyDefects"/> plus <see cref="MeshTopologyDefects.DegenerateTriangle"/> for triangles
    ///     with distinct but coincident or collinear vertices, which the renderer discards. Computed lazily on
    ///     first use and cached. Unlike <see cref="IsClosed"/>, geometric degeneracy is included.
    /// </summary>
    internal MeshTopologyDefects OperandDefects
    {
        get
        {
            // The analysis is deterministic, so a racing recomputation stores the same value
            var state = Volatile.Read(ref _operandDefectState);
            if (state == 0)
            {
                state = (int)MeshTopology.Analyze(_vertices, _indices) + 1;
                Volatile.Write(ref _operandDefectState, state);
            }

            return (MeshTopologyDefects)(state - 1);
        }
    }

    /// <summary>
    ///     Gets the signed volume enclosed by the mesh (divergence theorem, computed relative to
    ///     the bounds center for precision). Positive for a closed, outward counter-clockwise
    ///     mesh; negative when the mesh is inside out; meaningless for open meshes.
    /// </summary>
    public double SignedVolume =>
        Bounds.IsEmpty ? 0.0 : MeshTopology.SignedVolume(_vertices, _indices, Bounds.Center);

    /// <summary>Gets the total surface area (the sum of all triangle areas).</summary>
    public double SurfaceArea => MeshTopology.SurfaceArea(_vertices, _indices);

    /// <summary>Gets the vertices as a span for the renderer (no copy; internal so callers cannot mutate).</summary>
    internal ReadOnlySpan<Vector3D> VertexSpan => _vertices;

    /// <summary>Gets the triangle indices as a span for the renderer (no copy; internal so callers cannot mutate).</summary>
    internal ReadOnlySpan<int> IndexSpan => _indices;

    /// <summary>
    ///     Returns a new mesh with every vertex transformed by <paramref name="matrix"/>. When the
    ///     matrix mirrors space (negative determinant) the winding of every triangle is reversed
    ///     so the result still has outward counter-clockwise orientation and a positive volume.
    /// </summary>
    /// <param name="matrix">The model transform to apply.</param>
    /// <returns>A new transformed mesh; this instance is unchanged.</returns>
    /// <exception cref="ArgumentException">Thrown when the matrix or any transformed vertex is not finite.</exception>
    public Mesh Transform(Matrix4x4D matrix)
    {
        // A non-finite matrix cannot produce a valid mesh
        if (!matrix.IsFinite)
        {
            throw new ArgumentException("The transform must be finite.", nameof(matrix));
        }

        var vertices = new Vector3D[_vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            var transformed = matrix.TransformPoint(_vertices[i]);
            if (!transformed.IsFinite)
            {
                throw new ArgumentException("The transform produced a non-finite vertex.", nameof(matrix));
            }

            vertices[i] = transformed;
        }

        // Mirroring flips orientation, so swap two indices per triangle to keep outward-CCW winding
        var indices = (int[])_indices.Clone();
        if (matrix.Determinant < 0.0)
        {
            FlipWinding(indices);
        }

        return new Mesh(vertices, indices);
    }

    /// <summary>
    ///     Returns a new mesh with the winding of every triangle reversed (inside-out repair or
    ///     deliberate inversion); the signed volume changes sign.
    /// </summary>
    /// <returns>A new mesh sharing no storage with this one.</returns>
    public Mesh Reverse()
    {
        var indices = (int[])_indices.Clone();
        FlipWinding(indices);
        return new Mesh((Vector3D[])_vertices.Clone(), indices);
    }

    /// <summary>Swaps the second and third index of every triangle in place.</summary>
    /// <param name="indices">The privately owned index array to modify.</param>
    private static void FlipWinding(int[] indices)
    {
        for (var t = 0; t < indices.Length; t += 3)
        {
            (indices[t + 1], indices[t + 2]) = (indices[t + 2], indices[t + 1]);
        }
    }

    /// <summary>Copies a sequence into a new array, rejecting <see langword="null"/>.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The sequence to copy.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    /// <returns>A new array holding the elements.</returns>
    private static T[] CopyOf<T>(IEnumerable<T> source, string paramName)
    {
        if (source is null)
        {
            throw new ArgumentNullException(paramName);
        }

        return source.ToArray();
    }
}

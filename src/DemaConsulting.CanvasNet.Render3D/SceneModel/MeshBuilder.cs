namespace DemaConsulting.CanvasNet.Render3D.SceneModel;

/// <summary>
///     Incrementally assembles a <see cref="Mesh"/>, welding identical vertices so that tessellated
///     surfaces share vertices by index and therefore form closed, crack-free meshes.
/// </summary>
/// <remarks>
///     Welding uses exact component equality (negative and positive zero are equal; NaN is
///     rejected), never a tolerance: primitives compute each vertex once and refer to it by index,
///     so bitwise-identical positions are the only ones that should merge. The builder is not
///     thread-safe; <see cref="Build"/> may be called repeatedly and every result is independent.
/// </remarks>
public sealed class MeshBuilder
{
    /// <summary>Vertices in insertion order.</summary>
    private readonly List<Vector3D> _vertices = [];

    /// <summary>Triangle indices, three per triangle.</summary>
    private readonly List<int> _indices = [];

    /// <summary>Maps a position to the index of its welded vertex.</summary>
    private readonly Dictionary<Vector3D, int> _lookup = [];

    /// <summary>Gets the number of distinct vertices added so far.</summary>
    public int VertexCount => _vertices.Count;

    /// <summary>Gets the number of triangles added so far.</summary>
    public int TriangleCount => _indices.Count / 3;

    /// <summary>
    ///     Adds a vertex, or returns the index of an existing vertex at exactly the same position.
    /// </summary>
    /// <param name="position">The vertex position; every component must be finite.</param>
    /// <returns>The (possibly pre-existing) vertex index.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="position"/> is not finite.</exception>
    public int AddVertex(Vector3D position)
    {
        // Non-finite positions would break welding (NaN) and the Mesh invariants
        if (!position.IsFinite)
        {
            throw new ArgumentException("The vertex position must be finite.", nameof(position));
        }

        if (_lookup.TryGetValue(position, out var existing))
        {
            return existing;
        }

        var index = _vertices.Count;
        _vertices.Add(position);
        _lookup.Add(position, index);
        return index;
    }

    /// <summary>
    ///     Adds one triangle whose vertices are counter-clockwise when seen from outside.
    /// </summary>
    /// <param name="a">The first vertex index.</param>
    /// <param name="b">The second vertex index.</param>
    /// <param name="c">The third vertex index.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an index does not address an added vertex.</exception>
    /// <exception cref="ArgumentException">Thrown when two of the indices are equal (degenerate triangle).</exception>
    public void AddTriangle(int a, int b, int c)
    {
        // Validate every index before mutating state so a failed call leaves the builder unchanged
        CheckIndex(a, nameof(a));
        CheckIndex(b, nameof(b));
        CheckIndex(c, nameof(c));
        if (a == b || b == c || a == c)
        {
            throw new ArgumentException("A triangle must have three distinct vertex indices.");
        }

        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }

    /// <summary>
    ///     Adds a convex counter-clockwise polygon, fan-triangulated from its first vertex.
    /// </summary>
    /// <param name="polygon">The polygon's vertex indices in counter-clockwise order (at least three).</param>
    /// <exception cref="ArgumentException">Thrown when fewer than three indices are supplied or a triangle would be degenerate.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an index does not address an added vertex.</exception>
    public void AddPolygon(ReadOnlySpan<int> polygon)
    {
        if (polygon.Length < 3)
        {
            throw new ArgumentException("A polygon requires at least three vertices.", nameof(polygon));
        }

        // Fan around the first vertex: (0,1,2), (0,2,3), ...
        for (var i = 1; i < polygon.Length - 1; i++)
        {
            AddTriangle(polygon[0], polygon[i], polygon[i + 1]);
        }
    }

    /// <summary>
    ///     Creates a <see cref="Mesh"/> from the current contents. The builder remains usable and
    ///     every returned mesh is independent of later additions.
    /// </summary>
    /// <returns>The new mesh.</returns>
    public Mesh Build() => new(_vertices, _indices);

    /// <summary>Validates that an index addresses an already-added vertex.</summary>
    /// <param name="index">The index to check.</param>
    /// <param name="paramName">The parameter name for the exception.</param>
    private void CheckIndex(int index, string paramName)
    {
        if ((uint)index >= (uint)_vertices.Count)
        {
            throw new ArgumentOutOfRangeException(paramName, index, "The vertex index does not address an added vertex.");
        }
    }
}

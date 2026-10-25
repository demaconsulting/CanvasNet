// cspell:ignore bijectively
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The kind of a node of a <see cref="ScadShape2D"/> tree.
/// </summary>
internal enum ScadShape2DKind
{
    /// <summary>A region produced by a 2D primitive.</summary>
    Leaf,

    /// <summary>A planar affine transform of one child.</summary>
    Transform,

    /// <summary>An appearance (color) applied to one child.</summary>
    Color,

    /// <summary>A union, difference or intersection of several children.</summary>
    Csg,
}

/// <summary>
///     A 2D result of the geometry builder: a tree of regions, planar transforms, colors and boolean operations
///     that is <i>not</i> evaluated into one region.
/// </summary>
/// <remarks>
///     <para>
///         The package has no 2D boolean kernel. Instead a 2D tree is kept symbolic and an extrusion
///         (<c>linear_extrude</c>, <c>rotate_extrude</c>, or the slab of a top-level 2D shape) is <i>distributed
///         over the tree</i>: the extrusion of <c>difference() { A; B; }</c> is the 3D difference of the extrusion of
///         <c>A</c> and the extrusion of <c>B</c>. That is exact because both extrusions map a point of the profile
///         domain to a point of the solid bijectively, so set operations commute with them; the renderer's
///         constructive solid geometry then resolves the result per pixel.
///     </para>
///     <para>
///         The transform of a node is the in-plane part of a 4x4 matrix in the row-vector convention of
///         <see cref="Matrix4x4D"/> (rows and columns 0 and 1 plus the translation), with the Z row and column
///         of the identity. Nodes are immutable and consumed once by their parent (the instantiation tree has no
///         shared nodes); every walk over a tree is iterative.
///     </para>
/// </remarks>
internal sealed class ScadShape2D
{
    /// <summary>
    ///     Initializes a node.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="children">The children.</param>
    private ScadShape2D(ScadShape2DKind kind, IReadOnlyList<ScadShape2D> children)
    {
        Kind = kind;
        Children = children;
    }

    /// <summary>Gets the kind of the node.</summary>
    public ScadShape2DKind Kind { get; }

    /// <summary>Gets the children (one for a transform or a color, any number for a boolean, none for a leaf).</summary>
    public IReadOnlyList<ScadShape2D> Children { get; }

    /// <summary>Gets the region of a <see cref="ScadShape2DKind.Leaf"/>.</summary>
    public PolygonRegion? Region { get; private init; }

    /// <summary>Gets the location of the instance that produced a <see cref="ScadShape2DKind.Leaf"/>.</summary>
    public ScadSourceLocation Location { get; private init; }

    /// <summary>Gets the planar matrix of a <see cref="ScadShape2DKind.Transform"/>.</summary>
    public Matrix4x4D Matrix { get; private init; }

    /// <summary>Gets the appearance of a <see cref="ScadShape2DKind.Color"/>.</summary>
    public NodeAppearance? Appearance { get; private init; }

    /// <summary>Gets the operation of a <see cref="ScadShape2DKind.Csg"/>.</summary>
    public CsgOperation Operation { get; private init; }

    /// <summary>
    ///     Creates a leaf.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="location">The location of the producing instance.</param>
    /// <returns>The node.</returns>
    public static ScadShape2D CreateLeaf(PolygonRegion region, ScadSourceLocation location) =>
        new(ScadShape2DKind.Leaf, []) { Region = region, Location = location };

    /// <summary>
    ///     Creates a planar transform of a child.
    /// </summary>
    /// <param name="child">The child.</param>
    /// <param name="matrix">The planar matrix (see the type remarks).</param>
    /// <returns>The node.</returns>
    public static ScadShape2D CreateTransform(ScadShape2D child, Matrix4x4D matrix) =>
        new(ScadShape2DKind.Transform, [child]) { Matrix = matrix };

    /// <summary>
    ///     Creates a color applied to a child.
    /// </summary>
    /// <param name="child">The child.</param>
    /// <param name="appearance">The appearance.</param>
    /// <returns>The node.</returns>
    public static ScadShape2D CreateColor(ScadShape2D child, NodeAppearance appearance) =>
        new(ScadShape2DKind.Color, [child]) { Appearance = appearance };

    /// <summary>
    ///     Creates a boolean combination of children.
    /// </summary>
    /// <param name="operation">The operation; for a difference the first child is the minuend.</param>
    /// <param name="children">The children (at least one).</param>
    /// <returns>The node.</returns>
    public static ScadShape2D CreateCsg(CsgOperation operation, IReadOnlyList<ScadShape2D> children) =>
        new(ScadShape2DKind.Csg, children) { Operation = operation };

    /// <summary>
    ///     Gets the leaves of the tree in document order together with the planar matrix that takes each leaf into
    ///     the coordinate system of the root, walking iteratively.
    /// </summary>
    /// <returns>The leaves and their accumulated matrices.</returns>
    public List<(ScadShape2D Leaf, Matrix4x4D Matrix)> EnumerateLeaves()
    {
        var result = new List<(ScadShape2D, Matrix4x4D)>();
        var stack = new Stack<(ScadShape2D Node, Matrix4x4D Matrix)>();
        stack.Push((this, Matrix4x4D.Identity));
        while (stack.Count > 0)
        {
            var (node, matrix) = stack.Pop();
            if (node.Kind == ScadShape2DKind.Leaf)
            {
                result.Add((node, matrix));
                continue;
            }

            // A transform applies to its child first, then to everything above it
            var childMatrix = node.Kind == ScadShape2DKind.Transform ? node.Matrix * matrix : matrix;
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push((node.Children[i], childMatrix));
            }
        }

        return result;
    }

    /// <summary>
    ///     Computes the exact bounds of every vertex of every leaf of the tree (including operands that a boolean
    ///     would remove), as a box in the XY plane at Z = 0.
    /// </summary>
    /// <returns>The bounds; empty when the tree has no vertices.</returns>
    public BoundingBox3D ComputeVertexBounds()
    {
        var bounds = BoundingBox3D.Empty;
        foreach (var (leaf, matrix) in EnumerateLeaves())
        {
            foreach (var vertex in leaf.Region!.Vertices)
            {
                bounds = bounds.Union(matrix.TransformPoint(new Vector3D(vertex.X, vertex.Y, 0.0)));
            }
        }

        return bounds;
    }
}

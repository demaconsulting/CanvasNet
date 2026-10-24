// cspell:ignore tessellator

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Render3D.Tessellation;

namespace DemaConsulting.CanvasNet.Render3D.Tests;

/// <summary>
///     Shared assertion and construction helpers for the Render3D test project: vector and
///     matrix comparisons with tolerances and (in later units) pixel counting, mesh orientation
///     checks and fragment-counting visitors.
/// </summary>
internal static class Render3DTestHelpers
{
    /// <summary>
    ///     Creates an axis-aligned box mesh (8 vertices, 12 triangles, outward counter-clockwise
    ///     winding) without using the production tessellator, so mesh tests stay independent.
    /// </summary>
    /// <param name="min">The minimum corner.</param>
    /// <param name="size">The edge lengths along X, Y and Z.</param>
    /// <returns>The box mesh.</returns>
    public static Mesh CreateBoxMesh(Vector3D min, Vector3D size)
    {
        // Vertex i has x at bit 0, y at bit 1 and z at bit 2 of i
        var vertices = new Vector3D[8];
        for (var i = 0; i < 8; i++)
        {
            vertices[i] = new Vector3D(
                min.X + (((i & 1) != 0) ? size.X : 0.0),
                min.Y + (((i & 2) != 0) ? size.Y : 0.0),
                min.Z + (((i & 4) != 0) ? size.Z : 0.0));
        }

        int[] indices =
        [
            0, 2, 1, 1, 2, 3, // -Z
            4, 5, 7, 4, 7, 6, // +Z
            0, 1, 5, 0, 5, 4, // -Y
            2, 7, 3, 2, 6, 7, // +Y
            0, 4, 6, 0, 6, 2, // -X
            1, 3, 7, 1, 7, 5, // +X
        ];
        return new Mesh(vertices, indices);
    }

    /// <summary>
    ///     Creates the unit-corner tetrahedron <c>(0,0,0) (1,0,0) (0,1,0) (0,0,1)</c> with
    ///     outward counter-clockwise winding (volume 1/6).
    /// </summary>
    /// <returns>The tetrahedron mesh.</returns>
    public static Mesh CreateTetrahedronMesh() =>
        new(
            [Vector3D.Zero, Vector3D.UnitX, Vector3D.UnitY, Vector3D.UnitZ],
            [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3]);

    /// <summary>
    ///     Concatenates meshes into one multi-shell mesh (vertices and re-based indices appended in order).
    /// </summary>
    /// <param name="meshes">The meshes to combine.</param>
    /// <returns>The combined mesh.</returns>
    public static Mesh Combine(params Mesh[] meshes)
    {
        var vertices = new List<Vector3D>();
        var indices = new List<int>();
        foreach (var mesh in meshes)
        {
            var offset = vertices.Count;
            vertices.AddRange(mesh.Vertices);
            indices.AddRange(mesh.TriangleIndices.Select(i => i + offset));
        }

        return new Mesh(vertices, indices);
    }

    /// <summary>
    ///     Creates the STL-style unwelded form of a mesh: every triangle gets three vertices of its own.
    /// </summary>
    /// <param name="mesh">The mesh to split into per-triangle vertices.</param>
    /// <returns>An equivalent mesh with <c>3 * TriangleCount</c> vertices.</returns>
    public static Mesh ToUnwelded(Mesh mesh) =>
        new(mesh.TriangleIndices.Select(i => mesh.Vertices[i]).ToArray(), Enumerable.Range(0, mesh.TriangleIndices.Count));

    /// <summary>
    ///     Asserts that a convex mesh is closed, has positive signed volume and that every
    ///     triangle's normal points away from the vertex centroid (i.e. winding is outward).
    /// </summary>
    /// <param name="mesh">The convex mesh to check.</param>
    public static void AssertClosedAndOutward(Mesh mesh)
    {
        Assert.True(mesh.IsClosed, "The mesh must be closed.");
        Assert.True(mesh.SignedVolume > 0.0, "The mesh must have positive signed volume.");

        // The vertex average lies strictly inside a convex polyhedron
        var centroid = Vector3D.Zero;
        foreach (var vertex in mesh.Vertices)
        {
            centroid += vertex;
        }

        centroid /= mesh.VertexCount;

        var vertices = mesh.Vertices;
        var indices = mesh.TriangleIndices;
        for (var t = 0; t < indices.Count; t += 3)
        {
            var a = vertices[indices[t]];
            var b = vertices[indices[t + 1]];
            var c = vertices[indices[t + 2]];
            var normal = Vector3D.Cross(b - a, c - a);
            Assert.True(normal.Length > 0.0, $"Triangle {t / 3} is degenerate.");
            var outward = (a + b + c) / 3.0 - centroid;
            Assert.True(Vector3D.Dot(normal, outward) > 0.0, $"Triangle {t / 3} faces inward.");
        }
    }

    /// <summary>
    ///     Asserts that two vectors are component-wise equal within an absolute tolerance.
    /// </summary>
    /// <param name="expected">The expected vector.</param>
    /// <param name="actual">The actual vector.</param>
    /// <param name="tolerance">The absolute per-component tolerance.</param>
    public static void AssertVector(Vector3D expected, Vector3D actual, double tolerance = 1e-12)
    {
        Assert.Equal(expected.X, actual.X, tolerance);
        Assert.Equal(expected.Y, actual.Y, tolerance);
        Assert.Equal(expected.Z, actual.Z, tolerance);
    }

    /// <summary>
    ///     Asserts that two matrices are element-wise equal within an absolute tolerance.
    /// </summary>
    /// <param name="expected">The expected matrix.</param>
    /// <param name="actual">The actual matrix.</param>
    /// <param name="tolerance">The absolute per-element tolerance.</param>
    public static void AssertMatrix(Matrix4x4D expected, Matrix4x4D actual, double tolerance = 1e-12)
    {
        Assert.Equal(expected.M11, actual.M11, tolerance);
        Assert.Equal(expected.M12, actual.M12, tolerance);
        Assert.Equal(expected.M13, actual.M13, tolerance);
        Assert.Equal(expected.M14, actual.M14, tolerance);
        Assert.Equal(expected.M21, actual.M21, tolerance);
        Assert.Equal(expected.M22, actual.M22, tolerance);
        Assert.Equal(expected.M23, actual.M23, tolerance);
        Assert.Equal(expected.M24, actual.M24, tolerance);
        Assert.Equal(expected.M31, actual.M31, tolerance);
        Assert.Equal(expected.M32, actual.M32, tolerance);
        Assert.Equal(expected.M33, actual.M33, tolerance);
        Assert.Equal(expected.M34, actual.M34, tolerance);
        Assert.Equal(expected.M41, actual.M41, tolerance);
        Assert.Equal(expected.M42, actual.M42, tolerance);
        Assert.Equal(expected.M43, actual.M43, tolerance);
        Assert.Equal(expected.M44, actual.M44, tolerance);
    }
    /// <summary>
    ///     Builds a prepared <see cref="ScreenTriangle"/> directly from sample-space vertices,
    ///     rounding to 24.8 fixed point and normalizing to positive area.
    /// </summary>
    /// <param name="a">Vertex A in sample units (y down).</param>
    /// <param name="b">Vertex B in sample units.</param>
    /// <param name="c">Vertex C in sample units.</param>
    /// <param name="depth">The depth at all three vertices (constant-depth triangle).</param>
    /// <param name="priority">The draw priority.</param>
    /// <returns>The prepared triangle.</returns>
    public static ScreenTriangle MakeScreenTriangle((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, float depth = 1.0f, int priority = 0)
    {
        // Convert to 24.8 fixed point exactly as the production setup does
        var x0 = (int)Math.Round(a.X * 256.0);
        var y0 = (int)Math.Round(a.Y * 256.0);
        var x1 = (int)Math.Round(b.X * 256.0);
        var y1 = (int)Math.Round(b.Y * 256.0);
        var x2 = (int)Math.Round(c.X * 256.0);
        var y2 = (int)Math.Round(c.Y * 256.0);
        var area2 = ((long)(x1 - x0) * (y2 - y0)) - ((long)(y1 - y0) * (x2 - x0));
        Assert.NotEqual(0L, area2);

        // Swap two vertices when needed so the area is positive
        if (area2 < 0)
        {
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
            area2 = -area2;
        }

        return new ScreenTriangle
        {
            X0 = x0,
            Y0 = y0,
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Area2 = area2,
            Depth0 = depth,
            Depth1 = depth,
            Depth2 = depth,
            Priority = priority,
            Color = new Rgba32(1, 2, 3, 255),
        };
    }

    /// <summary>
    ///     Asserts that two surfaces have identical dimensions and byte-identical pixel data.
    /// </summary>
    /// <param name="expected">The expected surface.</param>
    /// <param name="actual">The actual surface.</param>
    public static void AssertSurfacesIdentical(Surface expected, Surface actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        for (var y = 0; y < expected.Height; y++)
        {
            Assert.True(
                MemoryExtensionsEqual(expected.GetRowSpanBytes(y), actual.GetRowSpanBytes(y)),
                $"Row {y} differs.");
        }
    }

    /// <summary>
    ///     Counts the pixels of <paramref name="surface"/> that equal <paramref name="color"/>.
    /// </summary>
    /// <param name="surface">The surface to scan.</param>
    /// <param name="color">The color to count.</param>
    /// <returns>The number of matching pixels.</returns>
    public static int CountPixels(Surface surface, Rgba32 color)
    {
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Creates a flat quad mesh (two triangles) from four corners given counter-clockwise as seen from the front.
    /// </summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    /// <param name="d">The fourth corner.</param>
    /// <returns>The open (non-closed) quad mesh.</returns>
    public static Mesh CreateQuadMesh(Vector3D a, Vector3D b, Vector3D c, Vector3D d) =>
        new([a, b, c, d], [0, 1, 2, 0, 2, 3]);

    /// <summary>
    ///     Creates an axis-aligned square quad in the plane <c>z = z0</c> facing +Z.
    /// </summary>
    /// <param name="minX">The minimum X.</param>
    /// <param name="minY">The minimum Y.</param>
    /// <param name="maxX">The maximum X.</param>
    /// <param name="maxY">The maximum Y.</param>
    /// <param name="z0">The plane height.</param>
    /// <returns>The quad mesh.</returns>
    public static Mesh CreateFloorQuad(double minX, double minY, double maxX, double maxY, double z0 = 0.0) =>
        CreateQuadMesh(new Vector3D(minX, minY, z0), new Vector3D(maxX, minY, z0), new Vector3D(maxX, maxY, z0), new Vector3D(minX, maxY, z0));

    /// <summary>
    ///     Creates an orthographic camera.
    /// </summary>
    /// <param name="eye">The eye position.</param>
    /// <param name="target">The target position.</param>
    /// <param name="up">The up direction.</param>
    /// <param name="height">The visible world height.</param>
    /// <returns>The camera.</returns>
    public static Camera OrthoCamera(Vector3D eye, Vector3D target, Vector3D up, double height) =>
        new() { Eye = eye, Target = target, Up = up, Projection = CameraProjection.Orthographic, OrthographicHeight = height };

    /// <summary>
    ///     Creates options that render the pure base colors (a single full-intensity ambient light), without
    ///     antialiasing unless requested, so pixel colors can be asserted exactly.
    /// </summary>
    /// <param name="supersampling">The supersampling factor.</param>
    /// <param name="background">The background color, or <see langword="null"/> for opaque white.</param>
    /// <param name="defaultColor">The default surface color, or <see langword="null"/> for the library default.</param>
    /// <param name="backFace">The back-face color, or <see langword="null"/> for the library default.</param>
    /// <returns>The options.</returns>
    public static SceneRenderOptions FlatOptions(int supersampling = 1, Rgba32? background = null, Rgba32? defaultColor = null, Rgba32? backFace = null) =>
        new()
        {
            Lights = [new AmbientLight { Intensity = 1.0f }],
            Supersampling = supersampling,
            BackgroundColor = background ?? new Rgba32(255, 255, 255, 255),
            DefaultColor = defaultColor ?? new Rgba32(180, 180, 180, 255),
            BackFaceColor = backFace ?? new Rgba32(200, 70, 70, 255),
        };

    /// <summary>
    ///     Collects the distinct pixel colors of a surface.
    /// </summary>
    /// <param name="surface">The surface to scan.</param>
    /// <returns>The set of distinct colors.</returns>
    public static HashSet<Rgba32> DistinctColors(Surface surface)
    {
        var colors = new HashSet<Rgba32>();
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                colors.Add(pixel);
            }
        }

        return colors;
    }

    /// <summary>
    ///     Finds the inclusive bounding box of all pixels that differ from a color.
    /// </summary>
    /// <param name="surface">The surface to scan.</param>
    /// <param name="background">The color to ignore.</param>
    /// <returns>The bounding box and the number of differing pixels (count 0 when none).</returns>
    public static (int MinX, int MinY, int MaxX, int MaxY, int Count) BoundsOfNon(Surface surface, Rgba32 background)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = -1;
        var maxY = -1;
        var count = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                if (surface[x, y] == background)
                {
                    continue;
                }

                count++;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        return (minX, minY, maxX, maxY, count);
    }

    /// <summary>
    ///     Builds a scene of a sphere, a cylinder and a box for determinism and fitting tests.
    /// </summary>
    /// <returns>The mixed-primitive scene.</returns>
    public static Scene CreateMixedScene()
    {
        var sphere = new MeshNode(PrimitiveTessellator.Sphere(1.5, 48));
        var cylinder = new MeshNode(PrimitiveTessellator.Cylinder(3.0, 1.0, 0.5, 32, center: true))
        {
            Transform = Matrix4x4D.CreateTranslation(new Vector3D(3.5, 0, 0)),
            Appearance = new NodeAppearance(new Rgba32(60, 140, 220, 255)),
        };
        var box = new MeshNode(PrimitiveTessellator.Box(new Vector3D(1.5, 2.0, 1.0), center: true))
        {
            Transform = Matrix4x4D.CreateRotationZ(25.0) * Matrix4x4D.CreateTranslation(new Vector3D(-3.0, 1.0, 0.5)),
            Appearance = new NodeAppearance(new Rgba32(220, 120, 60, 255)),
        };
        return new Scene(new GroupNode([sphere, cylinder, box]));
    }

    /// <summary>
    ///     Creates a top-down orthographic camera whose image maps world <c>x</c> in <c>[0, width]</c> and
    ///     <c>y</c> in <c>[0, height]</c> to pixel columns and rows one-to-one (pixel centre
    ///     <c>(column + 0.5, height - row - 0.5)</c>), looking down -Z from high above.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>The camera.</returns>
    public static Camera BoxGridCamera(int width, int height) =>
        OrthoCamera(new Vector3D(width / 2.0, height / 2.0, 200.0), new Vector3D(width / 2.0, height / 2.0, 0.0), Vector3D.UnitY, height);

    /// <summary>
    ///     Creates a mesh node of a unit-colored closed box.
    /// </summary>
    /// <param name="min">The minimum corner.</param>
    /// <param name="size">The edge lengths.</param>
    /// <param name="color">The flat color of the node.</param>
    /// <returns>The mesh node.</returns>
    public static MeshNode ColoredBox(Vector3D min, Vector3D size, Rgba32 color) =>
        new(CreateBoxMesh(min, size)) { Appearance = new NodeAppearance(color) };

    /// <summary>
    ///     Creates an asymmetric closed solid (a sheared box: a parallelepiped that is not symmetric under
    ///     any axis mirror) so a wrongly mirrored or wrongly wound copy can never look like the correct one.
    /// </summary>
    /// <returns>The asymmetric closed mesh with outward counter-clockwise winding.</returns>
    public static Mesh CreateAsymmetricSolid()
    {
        var box = CreateBoxMesh(new Vector3D(-1.5, -1.0, -0.5), new Vector3D(3.0, 2.0, 1.0));
        var shear = Matrix4x4D.FromRowMajor(
        [
            1.0, 0.4, 0.0, 0.0,
            0.0, 1.0, 0.3, 0.0,
            0.0, 0.0, 1.0, 0.0,
            0.0, 0.0, 0.0, 1.0,
        ]);
        return box.Transform(shear);
    }

    /// <summary>
    ///     Counts pixels that have no 4-neighbor of the same color (isolated speckles), ignoring the image border.
    /// </summary>
    /// <param name="surface">The surface to scan.</param>
    /// <returns>The number of isolated pixels.</returns>
    public static int CountIsolatedPixels(Surface surface)
    {
        var count = 0;
        for (var y = 1; y < surface.Height - 1; y++)
        {
            for (var x = 1; x < surface.Width - 1; x++)
            {
                var c = surface[x, y];
                if (surface[x - 1, y] != c && surface[x + 1, y] != c && surface[x, y - 1] != c && surface[x, y + 1] != c)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    ///     Compares two byte spans for equality (a span-friendly wrapper so callers need no extension imports).
    /// </summary>
    /// <param name="left">The first span.</param>
    /// <param name="right">The second span.</param>
    /// <returns><see langword="true"/> when the contents are equal.</returns>
    private static bool MemoryExtensionsEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) => left.SequenceEqual(right);
}

/// <summary>
///     A node of an axis-aligned box CSG tree used as the analytic oracle for rendered coverage: it can
///     build the equivalent scene node and evaluate, for a vertical ray, the exact set of solid heights.
/// </summary>
internal abstract class BoxCsgTree
{
    /// <summary>Builds the scene node of the tree.</summary>
    /// <returns>The scene node.</returns>
    public abstract SceneNode ToSceneNode();

    /// <summary>Collects the leaves of the tree in pre-order.</summary>
    /// <param name="leaves">The list receiving the leaves.</param>
    public abstract void CollectLeaves(List<BoxCsgLeaf> leaves);

    /// <summary>
    ///     Evaluates the solid along the vertical ray through <c>(x, y)</c>.
    /// </summary>
    /// <param name="x">The ray's world X.</param>
    /// <param name="y">The ray's world Y.</param>
    /// <returns>The sorted, disjoint, closed-open <c>(low, high)</c> height intervals inside the solid.</returns>
    public abstract List<(double Low, double High)> Intervals(double x, double y);

    /// <summary>
    ///     Determines the exact color a top-down orthographic render must show at a pixel centre.
    /// </summary>
    /// <param name="x">The world X of the pixel centre.</param>
    /// <param name="y">The world Y of the pixel centre.</param>
    /// <param name="background">The background color.</param>
    /// <returns>The expected pixel color.</returns>
    public Rgba32 ExpectedColor(double x, double y, Rgba32 background)
    {
        var intervals = Intervals(x, y);
        if (intervals.Count == 0)
        {
            return background;
        }

        // The viewer is above everything: the visible surface is at the top end of the highest interval
        var top = intervals[^1].High;
        var leaves = new List<BoxCsgLeaf>();
        CollectLeaves(leaves);
        foreach (var leaf in leaves)
        {
            // Heights are distinct across leaves, so exactly one leaf owns a face at the visible height
            if (leaf.Covers(x, y) && (Math.Abs(leaf.MaxZ - top) < 1e-9 || Math.Abs(leaf.MinZ - top) < 1e-9))
            {
                return leaf.Color;
            }
        }

        throw new InvalidOperationException("No leaf owns the visible surface.");
    }

    /// <summary>
    ///     Builds a seeded pseudo-random box CSG tree whose leaves have pairwise distinct heights and
    ///     integer footprints, so pixel centres never lie on an edge and no two faces are coincident.
    /// </summary>
    /// <param name="random">The random source.</param>
    /// <param name="leafCount">The number of box leaves (at least one).</param>
    /// <param name="width">The grid width in world units.</param>
    /// <param name="height">The grid height in world units.</param>
    /// <returns>The tree.</returns>
    public static BoxCsgTree CreateRandom(Random random, int leafCount, int width, int height)
    {
        // Distinct heights: shuffle 1..4n and hand two to every leaf
        var heights = Enumerable.Range(1, 4 * leafCount).OrderBy(_ => random.Next()).ToArray();
        var leaves = new List<BoxCsgTree>();
        for (var i = 0; i < leafCount; i++)
        {
            var z0 = Math.Min(heights[2 * i], heights[(2 * i) + 1]);
            var z1 = Math.Max(heights[2 * i], heights[(2 * i) + 1]);
            var x0 = random.Next(0, width - 1);
            var x1 = random.Next(x0 + 1, width + 1);
            var y0 = random.Next(0, height - 1);
            var y1 = random.Next(y0 + 1, height + 1);
            var color = new Rgba32((byte)(30 + (20 * i)), (byte)(220 - (15 * i)), (byte)(60 + (10 * i)), 255);
            leaves.Add(new BoxCsgLeaf(x0, y0, z0, x1, y1, z1, color));
        }

        return Group(random, leaves, 0);
    }

    /// <summary>Recursively groups leaves into operation nodes.</summary>
    /// <param name="random">The random source.</param>
    /// <param name="nodes">The nodes to combine (at least one).</param>
    /// <param name="depth">The current depth.</param>
    /// <returns>The combined tree.</returns>
    private static BoxCsgTree Group(Random random, List<BoxCsgTree> nodes, int depth)
    {
        if (nodes.Count == 1)
        {
            return nodes[0];
        }

        // Split into two or three consecutive groups, then combine them with a random operation
        var parts = depth >= 3 || nodes.Count < 3 ? 2 : random.Next(2, 4);
        var combined = new List<BoxCsgTree>();
        var start = 0;
        for (var p = 0; p < parts; p++)
        {
            var remainingParts = parts - p;
            var size = p == parts - 1 ? nodes.Count - start : random.Next(1, nodes.Count - start - remainingParts + 2);
            combined.Add(Group(random, nodes.GetRange(start, size), depth + 1));
            start += size;
        }

        var operation = (CsgOperation)random.Next(0, 3);
        return new BoxCsgOp(operation, combined);
    }
}

/// <summary>An axis-aligned box leaf of a <see cref="BoxCsgTree"/> with integer corners.</summary>
internal sealed class BoxCsgLeaf : BoxCsgTree
{
    /// <summary>
    ///     Creates a box leaf.
    /// </summary>
    /// <param name="minX">The minimum X.</param>
    /// <param name="minY">The minimum Y.</param>
    /// <param name="minZ">The minimum Z.</param>
    /// <param name="maxX">The maximum X.</param>
    /// <param name="maxY">The maximum Y.</param>
    /// <param name="maxZ">The maximum Z.</param>
    /// <param name="color">The flat color of the box.</param>
    public BoxCsgLeaf(int minX, int minY, int minZ, int maxX, int maxY, int maxZ, Rgba32 color)
    {
        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
        Color = color;
    }

    /// <summary>Gets the minimum X.</summary>
    public int MinX { get; }

    /// <summary>Gets the minimum Y.</summary>
    public int MinY { get; }

    /// <summary>Gets the minimum Z.</summary>
    public int MinZ { get; }

    /// <summary>Gets the maximum X.</summary>
    public int MaxX { get; }

    /// <summary>Gets the maximum Y.</summary>
    public int MaxY { get; }

    /// <summary>Gets the maximum Z.</summary>
    public int MaxZ { get; }

    /// <summary>Gets the flat color of the box.</summary>
    public Rgba32 Color { get; }

    /// <summary>Determines whether the vertical ray through <c>(x, y)</c> passes through the box.</summary>
    /// <param name="x">The ray's world X.</param>
    /// <param name="y">The ray's world Y.</param>
    /// <returns><see langword="true"/> when the box covers the point.</returns>
    public bool Covers(double x, double y) => x > MinX && x < MaxX && y > MinY && y < MaxY;

    /// <inheritdoc/>
    public override SceneNode ToSceneNode() =>
        Render3DTestHelpers.ColoredBox(new Vector3D(MinX, MinY, MinZ), new Vector3D(MaxX - MinX, MaxY - MinY, MaxZ - MinZ), Color);

    /// <inheritdoc/>
    public override void CollectLeaves(List<BoxCsgLeaf> leaves) => leaves.Add(this);

    /// <inheritdoc/>
    public override List<(double Low, double High)> Intervals(double x, double y) =>
        Covers(x, y) ? [(MinZ, MaxZ)] : [];
}

/// <summary>A union, difference or intersection node of a <see cref="BoxCsgTree"/>.</summary>
internal sealed class BoxCsgOp : BoxCsgTree
{
    /// <summary>
    ///     Creates an operation node.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="children">The operands in order (at least one).</param>
    public BoxCsgOp(CsgOperation operation, IReadOnlyList<BoxCsgTree> children)
    {
        Operation = operation;
        Children = children;
    }

    /// <summary>Gets the operation.</summary>
    public CsgOperation Operation { get; }

    /// <summary>Gets the operands.</summary>
    public IReadOnlyList<BoxCsgTree> Children { get; }

    /// <inheritdoc/>
    public override SceneNode ToSceneNode() => new CsgNode(Operation, Children.Select(c => c.ToSceneNode()));

    /// <inheritdoc/>
    public override void CollectLeaves(List<BoxCsgLeaf> leaves)
    {
        foreach (var child in Children)
        {
            child.CollectLeaves(leaves);
        }
    }

    /// <inheritdoc/>
    public override List<(double Low, double High)> Intervals(double x, double y)
    {
        var result = Children[0].Intervals(x, y);
        for (var i = 1; i < Children.Count; i++)
        {
            var other = Children[i].Intervals(x, y);
            result = Operation switch
            {
                CsgOperation.Union => Union(result, other),
                CsgOperation.Intersection => Intersect(result, other),
                _ => Subtract(result, other),
            };
        }

        return result;
    }

    /// <summary>Unions two interval lists.</summary>
    /// <param name="a">The first list.</param>
    /// <param name="b">The second list.</param>
    /// <returns>The merged, disjoint list.</returns>
    private static List<(double Low, double High)> Union(List<(double Low, double High)> a, List<(double Low, double High)> b)
    {
        var all = a.Concat(b).OrderBy(i => i.Low).ToList();
        var merged = new List<(double Low, double High)>();
        foreach (var interval in all)
        {
            if (merged.Count > 0 && interval.Low <= merged[^1].High)
            {
                merged[^1] = (merged[^1].Low, Math.Max(merged[^1].High, interval.High));
            }
            else
            {
                merged.Add(interval);
            }
        }

        return merged;
    }

    /// <summary>Intersects two interval lists.</summary>
    /// <param name="a">The first list.</param>
    /// <param name="b">The second list.</param>
    /// <returns>The intersection.</returns>
    private static List<(double Low, double High)> Intersect(List<(double Low, double High)> a, List<(double Low, double High)> b)
    {
        var result = new List<(double Low, double High)>();
        foreach (var left in a)
        {
            foreach (var right in b)
            {
                var low = Math.Max(left.Low, right.Low);
                var high = Math.Min(left.High, right.High);
                if (low < high)
                {
                    result.Add((low, high));
                }
            }
        }

        return result.OrderBy(i => i.Low).ToList();
    }

    /// <summary>Subtracts the second interval list from the first.</summary>
    /// <param name="a">The minuend.</param>
    /// <param name="b">The subtrahend.</param>
    /// <returns>The difference.</returns>
    private static List<(double Low, double High)> Subtract(List<(double Low, double High)> a, List<(double Low, double High)> b)
    {
        var result = a;
        foreach (var cut in b)
        {
            var next = new List<(double Low, double High)>();
            foreach (var interval in result)
            {
                if (cut.High <= interval.Low || cut.Low >= interval.High)
                {
                    next.Add(interval);
                    continue;
                }

                if (interval.Low < cut.Low)
                {
                    next.Add((interval.Low, cut.Low));
                }

                if (cut.High < interval.High)
                {
                    next.Add((cut.High, interval.High));
                }
            }

            result = next;
        }

        return result;
    }
}

/// <summary>
///     A fragment visitor for tests that records how many times each sample was covered and the depth of the last hit.
/// </summary>
internal readonly struct CountingVisitor : IFragmentVisitor
{
    /// <summary>
    ///     Creates a visitor for a sample grid of the given size.
    /// </summary>
    /// <param name="width">The grid width in samples.</param>
    /// <param name="height">The grid height in samples.</param>
    public CountingVisitor(int width, int height)
    {
        Width = width;
        Counts = new int[width * height];
        Depths = new float[width * height];
    }

    /// <summary>Gets the grid width.</summary>
    public int Width { get; }

    /// <summary>Gets the per-sample hit counts, row-major.</summary>
    public int[] Counts { get; }

    /// <summary>Gets the depth of the last hit per sample, row-major.</summary>
    public float[] Depths { get; }

    /// <summary>Gets the total number of covered sample hits.</summary>
    public int Total => Counts.Sum();

    /// <inheritdoc/>
    public void Visit(int sampleX, int sampleY, float depth)
    {
        Counts[(sampleY * Width) + sampleX]++;
        Depths[(sampleY * Width) + sampleX] = depth;
    }
}

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
    ///     Compares two byte spans for equality (a span-friendly wrapper so callers need no extension imports).
    /// </summary>
    /// <param name="left">The first span.</param>
    /// <param name="right">The second span.</param>
    /// <returns><see langword="true"/> when the contents are equal.</returns>
    private static bool MemoryExtensionsEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) => left.SequenceEqual(right);
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

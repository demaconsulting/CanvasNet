using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;

namespace DemaConsulting.CanvasNet.Render3D.Tests.SceneRendering;

/// <summary>
///     Unit tests for the internal <c>CsgResolver</c> (CsgResolver unit).
/// </summary>
public class CsgResolverTests
{
    /// <summary>The mesh shared by every node; the resolver never looks at geometry.</summary>
    private static readonly Mesh SharedMesh = Render3DTestHelpers.CreateTetrahedronMesh();

    /// <summary>The default tie tolerance of the tests.</summary>
    private const float Epsilon = 1e-3f;

    /// <summary>The default far distance of the tests.</summary>
    private const float Far = 100.0f;

    /// <summary>An axis-aligned integer box; the half-open ranges make adjacent boxes share no point.</summary>
    /// <param name="X0">The minimum X.</param>
    /// <param name="Y0">The minimum Y.</param>
    /// <param name="Z0">The minimum Z (depth).</param>
    /// <param name="X1">The maximum X (exclusive).</param>
    /// <param name="Y1">The maximum Y (exclusive).</param>
    /// <param name="Z1">The maximum Z (exclusive).</param>
    private sealed record Box(int X0, int Y0, int Z0, int X1, int Y1, int Z1)
    {
        /// <summary>Tests whether the ray at (x, y) is inside the box at depth <paramref name="t"/>.</summary>
        /// <param name="x">The ray X.</param>
        /// <param name="y">The ray Y.</param>
        /// <param name="t">The depth.</param>
        /// <returns><see langword="true"/> when inside.</returns>
        public bool Contains(double x, double y, double t) =>
            X0 <= x && x < X1 && Y0 <= y && y < Y1 && Z0 <= t && t < Z1;

        /// <summary>Tests whether the ray at (x, y) crosses the box in the XY plane.</summary>
        /// <param name="x">The ray X.</param>
        /// <param name="y">The ray Y.</param>
        /// <returns><see langword="true"/> when the ray meets the box footprint.</returns>
        public bool Covers(double x, double y) => X0 <= x && x < X1 && Y0 <= y && y < Y1;
    }

    /// <summary>Totals of a differential run, used to prove the test is not vacuous.</summary>
    /// <param name="Rays">The number of rays resolved.</param>
    /// <param name="Visible">The rays with a visible surface.</param>
    /// <param name="Exits">The rays whose visible event is an exit (camera inside material).</param>
    /// <param name="Ties">The rays whose visible depth is shared by two or more fragments.</param>
    private readonly record struct Stats(int Rays, int Visible, int Exits, int Ties)
    {
        /// <summary>Adds two totals.</summary>
        /// <param name="other">The other totals.</param>
        /// <returns>The sum.</returns>
        public Stats Plus(Stats other) => new(Rays + other.Rays, Visible + other.Visible, Exits + other.Exits, Ties + other.Ties);
    }

    /// <summary>Creates a mesh node.</summary>
    /// <returns>A new node.</returns>
    private static MeshNode M() => new(SharedMesh);

    /// <summary>The enter shade of a triangle: red = leaf, green = priority, blue = 1.</summary>
    /// <param name="leaf">The leaf.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Enter(int leaf, int priority) => new((byte)leaf, (byte)priority, 1, 255);

    /// <summary>The exit shade of a triangle: red = leaf, green = priority, blue = 2.</summary>
    /// <param name="leaf">The leaf.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Exit(int leaf, int priority) => new((byte)leaf, (byte)priority, 2, 255);

    /// <summary>Creates a triangle with identifiable colors (the resolver never reads its coordinates).</summary>
    /// <param name="leaf">The leaf.</param>
    /// <param name="back">Whether the triangle is back-facing.</param>
    /// <param name="priority">The draw priority.</param>
    /// <returns>The triangle.</returns>
    private static ScreenTriangle Tri(int leaf, bool back, int priority = 0) =>
        new()
        {
            Leaf = leaf,
            BackFacing = back,
            Priority = priority,
            EnterColor = Enter(leaf, priority),
            ExitColor = Exit(leaf, priority),
        };

    /// <summary>Creates a fragment.</summary>
    /// <param name="depth">The depth.</param>
    /// <param name="triangle">The triangle index.</param>
    /// <returns>The fragment.</returns>
    private static FragmentBuffer.Fragment F(float depth, int triangle) => new(depth, triangle);

    /// <summary>Resolves one sample of a scene.</summary>
    /// <param name="root">The scene root.</param>
    /// <param name="triangles">The triangle list.</param>
    /// <param name="fragments">The sample's fragments.</param>
    /// <param name="epsilon">The tie tolerance.</param>
    /// <param name="far">The far distance.</param>
    /// <returns>The outcome.</returns>
    private static (bool Found, float Depth, Rgba32 Color, int Meta) Resolve(
        SceneNode root,
        ScreenTriangle[] triangles,
        FragmentBuffer.Fragment[] fragments,
        float epsilon = Epsilon,
        float far = Far)
    {
        var program = CsgCompiler.Compile(root).Program!;
        return ResolveWith(new CsgResolver(program), triangles, fragments, epsilon, far);
    }

    /// <summary>Resolves one sample with an existing resolver on a copy of the fragments.</summary>
    /// <param name="resolver">The resolver.</param>
    /// <param name="triangles">The triangle list.</param>
    /// <param name="fragments">The sample's fragments.</param>
    /// <param name="epsilon">The tie tolerance.</param>
    /// <param name="far">The far distance.</param>
    /// <returns>The outcome.</returns>
    private static (bool Found, float Depth, Rgba32 Color, int Meta) ResolveWith(
        CsgResolver resolver,
        ScreenTriangle[] triangles,
        FragmentBuffer.Fragment[] fragments,
        float epsilon = Epsilon,
        float far = Far)
    {
        var copy = (FragmentBuffer.Fragment[])fragments.Clone();
        var found = resolver.Resolve(copy, triangles, epsilon, far, out var depth, out var color, out var meta);
        return (found, depth, color, meta);
    }

    /// <summary>The scene <c>A - B</c> (leaf 0 minus leaf 1).</summary>
    /// <returns>The root.</returns>
    private static CsgNode Difference() => new(CsgOperation.Difference, [M(), M()]);

    /// <summary>The triangles of <c>A - B</c>: 0 = A front, 1 = A back, 2 = B front, 3 = B back (priorities 0, 0, 1, 1).</summary>
    /// <returns>The triangle list.</returns>
    private static ScreenTriangle[] DifferenceTriangles() => [Tri(0, false, 0), Tri(0, true, 0), Tri(1, false, 1), Tri(1, true, 1)];

    /// <summary>Places every triangle in one plane facing the viewer at the given depth (all faces then count as coincident).</summary>
    /// <param name="triangles">The triangles.</param>
    /// <param name="z">The plane depth.</param>
    /// <returns>The triangles with their plane set.</returns>
    private static ScreenTriangle[] OnSharedPlane(ScreenTriangle[] triangles, float z) =>
        [.. triangles.Select(t => t with { PlaneNormal = Vector3.UnitZ, PlanePoint = new Vector3(0.0f, 0.0f, z) })];

    /// <summary>
    ///     Validates that the first surface of the minuend is visible when the ray meets it first.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_RayHitsMinuendFirst_ShowsMinuendFrontFace()
    {
        // Arrange: A front 1, B front 2, B back 3, A back 4 (the subtrahend is inside the minuend)
        var fragments = new[] { F(4, 1), F(2, 2), F(1, 0), F(3, 3) };

        // Act
        var result = Resolve(Difference(), DifferenceTriangles(), fragments);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(1.0f, result.Depth);
        Assert.Equal(Enter(0, 0), result.Color);
        Assert.Equal(0, result.Meta);
    }

    /// <summary>
    ///     Validates that a cut surface is the back face of the subtrahend, reported with the enter shade and a back-facing meta.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_RayPassesThroughSubtrahend_ShowsSubtrahendBackFaceAsCutSurface()
    {
        // Arrange: B front 1, A front 2, B back 3, A back 4 (the minuend starts inside the subtrahend)
        var fragments = new[] { F(1, 2), F(2, 0), F(3, 3), F(4, 1) };

        // Act
        var result = Resolve(Difference(), DifferenceTriangles(), fragments);

        // Assert: the root first becomes inside where the subtrahend ends
        Assert.True(result.Found);
        Assert.Equal(3.0f, result.Depth);
        Assert.Equal(Enter(1, 1), result.Color);
        Assert.Equal((1 << 1) | 1, result.Meta);
    }

    /// <summary>
    ///     Validates that samples without any visible surface report none and the resolver stays reusable.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_NoFragmentsOrOnlySubtrahend_ShowsNothing()
    {
        // Arrange
        var resolver = new CsgResolver(CsgCompiler.Compile(Difference()).Program!);
        var triangles = DifferenceTriangles();

        // Act
        var empty = ResolveWith(resolver, triangles, []);
        var onlySubtrahend = ResolveWith(resolver, triangles, [F(2, 2), F(3, 3)]);
        var afterwards = ResolveWith(resolver, triangles, [F(1, 0), F(4, 1)]);

        // Assert
        Assert.False(empty.Found);
        Assert.True(float.IsPositiveInfinity(empty.Depth));
        Assert.False(onlySubtrahend.Found);
        Assert.True(float.IsPositiveInfinity(onlySubtrahend.Depth));
        Assert.True(afterwards.Found);
        Assert.Equal(1.0f, afterwards.Depth);
    }

    /// <summary>
    ///     Validates the initial state inferred from the whole ray when the camera is inside the minuend: the first exit is visible with the exit shade.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_CameraInsideMinuend_ShowsFirstExitWithExitShade()
    {
        // Arrange: only the minuend's back face is in front of the camera
        var fragments = new[] { F(5, 1) };

        // Act
        var result = Resolve(Difference(), DifferenceTriangles(), fragments);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(5.0f, result.Depth);
        Assert.Equal(Exit(0, 0), result.Color);
        Assert.Equal(1, result.Meta);
    }

    /// <summary>
    ///     Validates that entering a subtrahend while inside the minuend is the visible exit event, shown with that triangle's exit shade.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_CameraInsideMinuendThenSubtrahendFront_ShowsSubtrahendFrontWithExitShade()
    {
        // Arrange: camera inside A, ray enters B at 2 and leaves it at 3
        var fragments = new[] { F(2, 2), F(3, 3), F(5, 1) };

        // Act
        var result = Resolve(Difference(), DifferenceTriangles(), fragments);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(2.0f, result.Depth);
        Assert.Equal(Exit(1, 1), result.Color);
        Assert.Equal(1 << 1, result.Meta);
    }

    /// <summary>
    ///     Validates that a camera inside both operands first sees the subtrahend's back face as a cut surface.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_CameraInsideBothOperands_ShowsSubtrahendCutSurface()
    {
        // Arrange: both fronts are behind the camera
        var fragments = new[] { F(3, 3), F(5, 1) };

        // Act
        var result = Resolve(Difference(), DifferenceTriangles(), fragments);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(3.0f, result.Depth);
        Assert.Equal(Enter(1, 1), result.Color);
    }

    /// <summary>
    ///     Validates that fragments beyond the far distance still determine the initial state but are never shown.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_FragmentsBeyondFar_InferStateButAreNotVisible()
    {
        // Arrange: camera inside A whose back face lies beyond far = 10; B is crossed at 3..4
        var fragments = new[] { F(3, 2), F(4, 3), F(20, 1) };

        // Act
        var visible = Resolve(Difference(), DifferenceTriangles(), fragments, far: 10.0f);
        var nearFar = Resolve(Difference(), DifferenceTriangles(), fragments, far: 2.5f);
        var onlyBeyond = Resolve(Difference(), DifferenceTriangles(), [F(20, 1)], far: 10.0f);

        // Assert: the exit at B's front (depth 3) is seen only when it is within far
        Assert.True(visible.Found);
        Assert.Equal(3.0f, visible.Depth);
        Assert.Equal(Exit(1, 1), visible.Color);
        Assert.False(nearFar.Found);
        Assert.False(onlyBeyond.Found);
    }

    /// <summary>
    ///     Validates the speckle-free tie group: a flush face between the operands is removed whichever fragment is rasterized first.
    /// </summary>
    /// <param name="offset">The depth offset of the subtrahend's flush front face relative to the minuend's (within the tolerance).</param>
    [Theory]
    [InlineData(0.0f)]
    [InlineData(1e-5f)]
    [InlineData(-1e-5f)]
    public void CsgResolver_Resolve_CoincidentFrontFaces_AreOneAtomicGroupAndFlushFaceIsRemoved(float offset)
    {
        // Arrange: ray from below through cube - box with the box's bottom flush with the cube's bottom (A front 1, B front 1+offset, B back 6, A back 11)
        var fragments = new[] { F(1, 0), F(1 + offset, 2), F(6, 3), F(11, 1) };

        // Act
        var result = Resolve(Difference(), DifferenceTriangles(), fragments);

        // Assert: the root changes only where the box ends, so the cut surface (box top) is seen, not the flush bottom
        Assert.True(result.Found);
        Assert.Equal(6.0f, result.Depth);
        Assert.Equal(Enter(1, 1), result.Color);
    }

    /// <summary>
    ///     Validates that events farther apart than the tolerance are separate groups, so the earlier one is seen, and that a larger tolerance merges them.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_FragmentsBeyondTolerance_AreSeparateGroups()
    {
        // Arrange: A front 1, B front 1.01
        var fragments = new[] { F(1, 0), F(1.01f, 2), F(6, 3), F(11, 1) };

        // Act
        var separate = Resolve(Difference(), DifferenceTriangles(), fragments, epsilon: 1e-3f);
        var merged = Resolve(Difference(), DifferenceTriangles(), fragments, epsilon: 0.1f);

        // Assert
        Assert.Equal(1.0f, separate.Depth);
        Assert.Equal(Enter(0, 0), separate.Color);
        Assert.Equal(6.0f, merged.Depth);
        Assert.Equal(Enter(1, 1), merged.Color);
    }

    /// <summary>
    ///     Validates that coincident front faces of different instances whose depths differ by fixed-point noise (the
    ///     gap is within their slope times a vertex quantum) form one group, while without slope information they do not.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_CoincidentFacesWithSlopeNoise_AreOneGroupAcrossInstances()
    {
        // Arrange: A front 1.0 and B front 1.004 (noise of an oblique plane), B back 6, A back 11; both fronts lie in one plane
        var fragments = new[] { F(1.0f, 0), F(1.004f, 2), F(6, 3), F(11, 1) };
        var flat = DifferenceTriangles();
        var oblique = OnSharedPlane(DifferenceTriangles().Select(t => t with { DepthSlope = 1.0f }).ToArray(), 1.0f);

        // Act
        var withoutSlope = Resolve(Difference(), flat, fragments, epsilon: 1e-4f);
        var withSlope = Resolve(Difference(), oblique, fragments, epsilon: 1e-4f);

        // Assert: without slope the minuend front is seen; with slope the flush faces cancel and the cut surface at B's back remains
        Assert.Equal(1.0f, withoutSlope.Depth);
        Assert.Equal(Enter(0, 0), withoutSlope.Color);
        Assert.Equal(6.0f, withSlope.Depth);
        Assert.Equal(Enter(1, 1), withSlope.Color);
    }

    /// <summary>
    ///     Validates that slope-derived widening needs plane coincidence: two separate, closely spaced oblique
    ///     faces (parallel planes 0.0005 apart, with a slope that would otherwise merge them) stay two events,
    ///     so the real transition at the first face is seen, while the same faces made coincident are one group.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_SeparateCloselySpacedObliqueFaces_AreNotMerged()
    {
        // Arrange: a 45 degree oblique plane family; A front on the plane at distance 1, B front on a parallel plane 0.0005 farther
        var normal = new Vector3(0.0f, MathF.Sqrt(0.5f), MathF.Sqrt(0.5f));
        var fragments = new[] { F(1.0f, 0), F(1.0005f, 2), F(6, 3), F(11, 1) };
        ScreenTriangle[] Build(float separation) =>
        [
            Tri(0, false, 0) with { DepthSlope = 1.0f, PlaneNormal = normal, PlanePoint = normal },
            Tri(0, true, 0) with { DepthSlope = 1.0f },
            Tri(1, false, 1) with { DepthSlope = 1.0f, PlaneNormal = normal, PlanePoint = normal * (1.0f + separation) },
            Tri(1, true, 1) with { DepthSlope = 1.0f },
        ];

        // Act
        var separate = Resolve(Difference(), Build(0.0005f), fragments, epsilon: 1e-6f);
        var coincident = Resolve(Difference(), Build(0.0f), fragments, epsilon: 1e-6f);

        // Assert: separate faces keep the minuend entry visible; the coincident faces cancel and the cut surface at B's back remains
        Assert.Equal(1.0f, separate.Depth);
        Assert.Equal(Enter(0, 0), separate.Color);
        Assert.Equal(6.0f, coincident.Depth);
        Assert.Equal(Enter(1, 1), coincident.Color);
    }

    /// <summary>
    ///     Validates that faces that are not parallel, or that have no plane information, are never widened into one group.
    /// </summary>
    /// <param name="tilt">Whether the subtrahend's plane is tilted (otherwise it has no plane at all).</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CsgResolver_Resolve_NonParallelOrUnknownPlane_IsNotWidened(bool tilt)
    {
        // Arrange: the same depth gap as the coincident case, but B's plane is tilted by about 5.7 degrees or unknown
        var fragments = new[] { F(1.0f, 0), F(1.004f, 2), F(6, 3), F(11, 1) };
        var triangles = DifferenceTriangles().Select(t => t with { DepthSlope = 1.0f }).ToArray();
        triangles[0] = triangles[0] with { PlaneNormal = Vector3.UnitZ, PlanePoint = Vector3.UnitZ };
        if (tilt)
        {
            triangles[2] = triangles[2] with { PlaneNormal = Vector3.Normalize(new Vector3(0.1f, 0.0f, 1.0f)), PlanePoint = Vector3.UnitZ };
        }

        // Act
        var result = Resolve(Difference(), triangles, fragments, epsilon: 1e-4f);

        // Assert: not merged, so the minuend front is seen first
        Assert.Equal(1.0f, result.Depth);
        Assert.Equal(Enter(0, 0), result.Color);
    }

    /// <summary>
    ///     Validates that slope-derived noise never merges two fragments of the same instance (a thin fold or wall
    ///     keeps its two sides as separate events) and is capped.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_SlopeNoise_NeverMergesSameInstanceAndIsCapped()
    {
        // Arrange: a thin minuend (front 1.0, back 1.004) with a steep slope; and a flush subtrahend front 1.2 with a huge slope
        var steep = OnSharedPlane(DifferenceTriangles().Select(t => t with { DepthSlope = 1.0f }).ToArray(), 1.0f);
        var thin = new[] { F(1.0f, 0), F(1.004f, 1) };
        var huge = OnSharedPlane(DifferenceTriangles().Select(t => t with { DepthSlope = 1.0e6f }).ToArray(), 1.0f);
        var far = new[] { F(1.0f, 0), F(1.2f, 2), F(6, 3), F(11, 1) };

        // Act
        var thinResult = Resolve(Difference(), steep, thin, epsilon: 1e-4f);
        var cappedResult = Resolve(Difference(), huge, far, epsilon: 1e-4f);

        // Assert: the thin sheet is seen (its two sides are not one group); the cap (1000 tolerances = 0.1) keeps a 0.2 gap apart
        Assert.True(thinResult.Found);
        Assert.Equal(1.0f, thinResult.Depth);
        Assert.Equal(1.0f, cappedResult.Depth);
        Assert.Equal(Enter(0, 0), cappedResult.Color);
    }

    /// <summary>
    ///     Validates that a fragment cannot join a group through slope noise when its own instance is already in
    ///     the group: the front and back faces of two coincident solids resolve as two pairs, never crosswise.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_CoincidentSolidsAtThinFold_ResolvesAsFrontPairAndBackPair()
    {
        // Arrange: both solids enter at 1.0, the subtrahend leaves at 1.0062 and the minuend 25 millionths later (a grazing fold)
        var triangles = new[]
        {
            Tri(0, false, 0) with { DepthSlope = 0.33f },
            Tri(0, true, 0) with { DepthSlope = 0.6f },
            Tri(1, false, 1) with { DepthSlope = 0.29f },
            Tri(1, true, 1) with { DepthSlope = 0.53f },
        };
        triangles = OnSharedPlane(triangles, 1.0f);
        var fragments = new[] { F(1.0f, 0), F(1.0f, 2), F(1.0062f, 3), F(1.00623f, 1) };

        // Act
        var result = Resolve(Difference(), triangles, fragments, epsilon: 5.4e-5f);

        // Assert: nothing is visible; a crosswise grouping would flip the root at the minuend front
        Assert.False(result.Found);
    }

    /// <summary>
    ///     Validates that a tie group is anchored at its first depth and does not chain across a series of close fragments.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_ChainOfCloseFragments_GroupIsAnchoredAtFirstDepth()
    {
        // Arrange: B front 1.0, A front 1.0008 (same group as B front), B back 1.0016 (farther than epsilon from the anchor)
        var fragments = new[] { F(1.0f, 2), F(1.0008f, 0), F(1.0016f, 3), F(5, 1) };

        // Act
        var result = Resolve(Difference(), DifferenceTriangles(), fragments, epsilon: 0.001f);

        // Assert: anchored groups {B front, A front} and {B back}: the root changes at B's back face; a chained group would show A's front face
        Assert.True(result.Found);
        Assert.Equal(1.0016f, result.Depth);
        Assert.Equal(Enter(1, 1), result.Color);
    }

    /// <summary>
    ///     Validates that coincident back faces of the operands (camera inside both) do not flip the result between them, in either order, while separate groups would.
    /// </summary>
    /// <param name="offset">The depth offset of the subtrahend's back face relative to the minuend's.</param>
    /// <param name="expectedFound">Whether a surface is visible.</param>
    [Theory]
    [InlineData(0.0f, false)]
    [InlineData(1e-5f, false)]
    [InlineData(-1e-5f, false)]
    [InlineData(0.01f, false)]
    [InlineData(-0.01f, true)]
    public void CsgResolver_Resolve_CoincidentBackFaces_DoNotSpeckle(float offset, bool expectedFound)
    {
        // Arrange: camera inside A and B, both leave at depth 5 (+ offset for B); beyond the tolerance order decides
        var fragments = new[] { F(5, 1), F(5 + offset, 3) };

        // Act
        var result = Resolve(Difference(), DifferenceTriangles(), fragments, epsilon: 1e-3f);

        // Assert: within the tolerance both leave together (outside -> outside, nothing drawn); B leaving alone first would show a cut surface
        Assert.Equal(expectedFound, result.Found);
    }

    /// <summary>
    ///     Validates that a union leaf uses a signed crossing counter: a leaf stays inside until all its overlapping members are left.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_OverlappingUnionLeaf_StaysInsideUntilLastMemberLeft()
    {
        // Arrange: (m0 + m1) - c with overlapping members [1,3] and [2,4] and c = [0.5,3.5]
        var root = new CsgNode(CsgOperation.Difference, [new GroupNode([M(), M()]), M()]);
        ScreenTriangle[] triangles =
        [
            Tri(0, false, 0), Tri(0, true, 0), Tri(0, false, 1), Tri(0, true, 1), Tri(1, false, 2), Tri(1, true, 2),
        ];
        var fragments = new[] { F(0.5f, 4), F(1, 0), F(2, 2), F(3, 1), F(3.5f, 5), F(4, 3) };

        // Act
        var result = Resolve(root, triangles, fragments);

        // Assert: after m0 is left at 3 the leaf is still inside (m1), so c ending at 3.5 exposes its back face
        Assert.True(result.Found);
        Assert.Equal(3.5f, result.Depth);
        Assert.Equal(Enter(1, 2), result.Color);
        Assert.Equal((2 << 1) | 1, result.Meta);
    }

    /// <summary>
    ///     Validates the intersection of two operands: the visible surface is where the ray enters the second one inside the first.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_Intersection_ShowsEntryIntoOverlap()
    {
        // Arrange: A front 1, B front 2, A back 4, B back 5
        var root = new CsgNode(CsgOperation.Intersection, [M(), M()]);
        var triangles = DifferenceTriangles();
        var overlap = new[] { F(1, 0), F(2, 2), F(4, 1), F(5, 3) };
        var disjoint = new[] { F(1, 0), F(2, 1), F(3, 2), F(4, 3) };
        var inside = new[] { F(4, 1), F(5, 3) };

        // Act
        var enter = Resolve(root, triangles, overlap);
        var none = Resolve(root, triangles, disjoint);
        var exit = Resolve(root, triangles, inside);

        // Assert
        Assert.True(enter.Found);
        Assert.Equal(2.0f, enter.Depth);
        Assert.Equal(Enter(1, 1), enter.Color);
        Assert.False(none.Found);
        Assert.True(exit.Found);
        Assert.Equal(4.0f, exit.Depth);
        Assert.Equal(Exit(0, 0), exit.Color);
    }

    /// <summary>
    ///     Validates the choice within a tie group where both leaves alone explain the change: the lower priority wins.
    /// </summary>
    /// <param name="minuendPriority">The draw priority of the minuend's front face.</param>
    /// <param name="subtrahendPriority">The draw priority of the subtrahend's back face.</param>
    /// <param name="expectedLeaf">The leaf of the chosen fragment.</param>
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(5, 1, 1)]
    public void CsgResolver_Resolve_FlushEnterAndExit_LowerPriorityOfEquallyExplainingFragmentsWins(int minuendPriority, int subtrahendPriority, int expectedLeaf)
    {
        // Arrange: the subtrahend ends exactly where the minuend starts (B front 0.5, B back 1, A front 1)
        ScreenTriangle[] triangles =
        [
            Tri(0, false, minuendPriority), Tri(0, true, minuendPriority), Tri(1, false, subtrahendPriority), Tri(1, true, subtrahendPriority),
        ];
        var fragments = new[] { F(0.5f, 2), F(1, 3), F(1, 0), F(4, 1) };

        // Act
        var result = Resolve(Difference(), triangles, fragments);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(1.0f, result.Depth);
        Assert.Equal(expectedLeaf, result.Color.R);
        Assert.Equal(1, result.Color.B);
    }

    /// <summary>
    ///     Validates that equal priorities are broken by the lower triangle index.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_EqualPriority_LowerTriangleIndexWins()
    {
        // Arrange: same flush case with equal priorities; the subtrahend's triangles come first in the list
        ScreenTriangle[] triangles = [Tri(1, false, 3), Tri(1, true, 3), Tri(0, false, 3), Tri(0, true, 3)];
        var fragments = new[] { F(0.5f, 0), F(1, 1), F(1, 2), F(4, 3) };

        // Act
        var result = Resolve(Difference(), triangles, fragments);

        // Assert: triangle 1 (subtrahend back) has the lower index than triangle 2 (minuend front)
        Assert.True(result.Found);
        Assert.Equal(1, result.Color.R);
    }

    /// <summary>
    ///     Validates that a leaf which alone explains the change is preferred over a leaf with a better priority whose change is irrelevant.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_OnlyOneLeafExplainsTheChange_ItIsPreferredOverBetterPriority()
    {
        // Arrange: root = L0 + (L1 ∩ L2); L0 and L1 enter together while L2 stays outside, so L1's change is irrelevant
        var root = new GroupNode([M(), new CsgNode(CsgOperation.Intersection, [M(), M()])]);
        ScreenTriangle[] triangles = [Tri(0, false, 9), Tri(0, true, 9), Tri(1, false, 1), Tri(1, true, 1), Tri(2, false, 0), Tri(2, true, 0)];
        var fragments = new[] { F(1, 0), F(1, 2), F(2, 1), F(2, 3) };

        // Act: every insertion order gives the same result
        var results = Permutations([0, 1, 2, 3])
            .Select(order => Resolve(root, triangles, order.Select(i => fragments[i]).ToArray()))
            .Distinct()
            .ToList();

        // Assert: leaf 0 (priority 9) alone explains the change
        var result = Assert.Single(results);
        Assert.True(result.Found);
        Assert.Equal(0, result.Color.R);
        Assert.Equal(9, result.Color.G);
    }

    /// <summary>
    ///     Validates the fallback when no single leaf explains the change: the lowest priority among all changed leaves wins.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_NoSingleLeafExplainsTheChange_LowestPriorityOfChangedLeavesWins()
    {
        // Arrange: root = (L0 ∩ L1) + L2 with all three entering together
        var root = new GroupNode([new CsgNode(CsgOperation.Intersection, [M(), M()]), M()]);
        ScreenTriangle[] triangles = [Tri(0, false, 5), Tri(0, true, 5), Tri(1, false, 2), Tri(1, true, 2), Tri(2, false, 7), Tri(2, true, 7)];
        var fragments = new[] { F(1, 0), F(1, 2), F(1, 4), F(3, 1), F(3, 3), F(3, 5) };

        // Act
        var results = Permutations([0, 1, 2])
            .Select(order => Resolve(root, triangles, order.Select(i => fragments[i]).Concat(fragments.Skip(3)).ToArray()))
            .Distinct()
            .ToList();

        // Assert: leaf 1 has the lowest priority (2)
        var result = Assert.Single(results);
        Assert.True(result.Found);
        Assert.Equal(1, result.Color.R);
        Assert.Equal(Enter(1, 2), result.Color);
    }

    /// <summary>
    ///     Validates that unmatched or duplicated fragments (an open or inverted operand) never throw, are deterministic for any order, and leave no state behind.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_UnbalancedFragments_AreBoundedDeterministicAndLeaveNoState()
    {
        // Arrange
        var resolver = new CsgResolver(CsgCompiler.Compile(Difference()).Program!);
        var triangles = DifferenceTriangles();
        var random = new Random(777);

        for (var trial = 0; trial < 300; trial++)
        {
            // Random multiset of fragments over the four triangles, ignoring pairing
            var count = random.Next(1, 9);
            var fragments = Enumerable.Range(0, count)
                .Select(_ => F(random.Next(1, 6), random.Next(4)))
                .DistinctBy(f => (f.Depth, f.Triangle))
                .ToArray();

            // Act
            var reference = ResolveWith(resolver, triangles, fragments);
            var shuffled = fragments.OrderBy(_ => random.Next()).ToArray();
            var again = ResolveWith(resolver, triangles, shuffled);

            // Assert: same outcome for any order
            Assert.Equal(reference, again);
        }

        // The scratch state is clean: a valid sample afterwards resolves exactly
        var valid = ResolveWith(resolver, triangles, [F(1, 0), F(2, 2), F(3, 3), F(4, 1)]);
        Assert.Equal(1.0f, valid.Depth);
        Assert.Equal(Enter(0, 0), valid.Color);
    }

    /// <summary>
    ///     Validates that every box scene, including ties and a camera inside the material, matches the analytic ray-interval oracle for any fragment order.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_RandomBoxTrees_MatchAnalyticOracle()
    {
        // Arrange / Act: 600 seeded random trees over integer boxes (exact ties are common)
        var total = new Stats(0, 0, 0, 0);
        for (var seed = 0; seed < 600; seed++)
        {
            var random = new Random(seed);
            var boxes = new Dictionary<MeshNode, Box>(ReferenceEqualityComparer.Instance);
            var root = RandomTree(random, boxes, [], 4);
            total = total.Plus(RunDifferential(root, boxes, random));
        }

        // Assert: the test exercised visible surfaces, exits (camera inside) and exact tie groups
        Assert.True(total.Rays > 3000, $"rays={total.Rays}");
        Assert.True(total.Visible > 500, $"visible={total.Visible} exits={total.Exits} ties={total.Ties}");
        Assert.True(total.Exits > 20, $"exits={total.Exits}");
        Assert.True(total.Ties > 100, $"ties={total.Ties}");
    }

    /// <summary>
    ///     Validates the oracle match for scenes with more than 64 leaves (a bitset of several words).
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_MoreThan64Leaves_MatchesAnalyticOracle()
    {
        // Arrange: a 90-leaf difference, a 70-leaf intersection and a union of both
        var random = new Random(2024);
        var boxes = new Dictionary<MeshNode, Box>(ReferenceEqualityComparer.Instance);
        var difference = new CsgNode(
            CsgOperation.Difference,
            Enumerable.Range(0, 90).Select(i => (SceneNode)NewBox(random, boxes, size: i == 0 ? 2 : 0)).ToArray());
        var intersection = new CsgNode(
            CsgOperation.Intersection,
            Enumerable.Range(0, 70).Select(_ => (SceneNode)NewBox(random, boxes, size: 2)).ToArray());
        var union = new GroupNode([difference, intersection]);

        // Act
        var total = RunDifferential(difference, boxes, random)
            .Plus(RunDifferential(intersection, boxes, random))
            .Plus(RunDifferential(union, boxes, random));

        // Assert
        Assert.True(CsgCompiler.Compile(union).LeafCount > 64);
        Assert.True(total.Visible > 20, $"visible={total.Visible}");
    }

    /// <summary>
    ///     Validates the whole-tile path: fragments from the real rasterizer and fragment buffer resolve into the tile workspace.
    /// </summary>
    [Fact]
    public void CsgResolver_ResolveTile_RasterizedOperands_WritesVisibleSurfaceIntoWorkspace()
    {
        // Arrange: A covers the 8x8 tile (front 1, back 5); B covers the left half (front 0.5, back 3)
        var triangles = new List<ScreenTriangle>
        {
            Render3DTestHelpers.MakeScreenTriangle((0, 0), (8, 0), (0, 8), 1.0f) with { Leaf = 0, Priority = 0, EnterColor = Enter(0, 0), ExitColor = Exit(0, 0) },
            Render3DTestHelpers.MakeScreenTriangle((8, 0), (8, 8), (0, 8), 1.0f) with { Leaf = 0, Priority = 0, EnterColor = Enter(0, 0), ExitColor = Exit(0, 0) },
            Render3DTestHelpers.MakeScreenTriangle((0, 0), (8, 0), (0, 8), 5.0f) with { Leaf = 0, Priority = 0, BackFacing = true, EnterColor = Enter(0, 0), ExitColor = Exit(0, 0) },
            Render3DTestHelpers.MakeScreenTriangle((8, 0), (8, 8), (0, 8), 5.0f) with { Leaf = 0, Priority = 0, BackFacing = true, EnterColor = Enter(0, 0), ExitColor = Exit(0, 0) },
            Render3DTestHelpers.MakeScreenTriangle((0, 0), (4, 0), (0, 8), 0.5f) with { Leaf = 1, Priority = 1, EnterColor = Enter(1, 1), ExitColor = Exit(1, 1) },
            Render3DTestHelpers.MakeScreenTriangle((4, 0), (4, 8), (0, 8), 0.5f) with { Leaf = 1, Priority = 1, EnterColor = Enter(1, 1), ExitColor = Exit(1, 1) },
            Render3DTestHelpers.MakeScreenTriangle((0, 0), (4, 0), (0, 8), 3.0f) with { Leaf = 1, Priority = 1, BackFacing = true, EnterColor = Enter(1, 1), ExitColor = Exit(1, 1) },
            Render3DTestHelpers.MakeScreenTriangle((4, 0), (4, 8), (0, 8), 3.0f) with { Leaf = 1, Priority = 1, BackFacing = true, EnterColor = Enter(1, 1), ExitColor = Exit(1, 1) },
        };
        var buffer = new FragmentBuffer(10_000);
        buffer.Begin(0, 0, 8, 8);
        triangles.ForEach(t => buffer.CountTriangle(t));
        buffer.EndCounting();
        for (var i = 0; i < triangles.Count; i++)
        {
            buffer.AddTriangle(i, triangles[i]);
        }

        var resolver = new CsgResolver(CsgCompiler.Compile(Difference()).Program!);
        var workspace = new TileWorkspace(64);
        workspace.Begin(0, 0, 8, 8);

        // Act
        resolver.ResolveTile(buffer, [.. triangles], workspace, Epsilon, Far, CancellationToken.None);
        var cutDepth = (float[])workspace.Depth.Clone();
        var cutColors = (Rgba32[])workspace.Colors.Clone();
        var cutMeta = (int[])workspace.Meta.Clone();
        resolver.ResolveTile(buffer, [.. triangles], workspace, Epsilon, 2.0f, CancellationToken.None);

        // Assert: left columns show B's back face (cut) at depth 3, right columns A's front face at depth 1
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var index = workspace.IndexOf(x, y);
                if (x < 4)
                {
                    Assert.Equal(3.0f, cutDepth[index]);
                    Assert.Equal(Enter(1, 1), cutColors[index]);
                    Assert.Equal((1 << 1) | 1, cutMeta[index]);

                    // With far = 2 the cut surface is not drawn
                    Assert.True(float.IsPositiveInfinity(workspace.Depth[index]));
                }
                else
                {
                    Assert.Equal(1.0f, cutDepth[index]);
                    Assert.Equal(Enter(0, 0), cutColors[index]);
                    Assert.Equal(0, cutMeta[index]);
                    Assert.Equal(1.0f, workspace.Depth[index]);
                }
            }
        }
    }

    /// <summary>
    ///     Validates that cancellation is observed while resolving a tile.
    /// </summary>
    [Fact]
    public void CsgResolver_ResolveTile_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var buffer = new FragmentBuffer(100);
        buffer.Begin(0, 0, 2, 2);
        buffer.EndCounting();
        var resolver = new CsgResolver(CsgCompiler.Compile(Difference()).Program!);
        var workspace = new TileWorkspace(4);
        workspace.Begin(0, 0, 2, 2);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(
            () => resolver.ResolveTile(buffer, [], workspace, Epsilon, Far, cancellation.Token));
    }

    /// <summary>
    ///     Validates the argument check of the constructor.
    /// </summary>
    [Fact]
    public void CsgResolver_Constructor_NullProgram_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new CsgResolver(null!));
    }

    /// <summary>The program holding only leaf 0.</summary>
    private static CsgProgram LeafProgram(int leaf, int leafCount) => new([new CsgInstruction(CsgOpCode.PushLeaf, leaf)], leafCount);

    /// <summary>The union of leaf 0 and leaf 1.</summary>
    private static CsgProgram UnionProgram() =>
        new([new CsgInstruction(CsgOpCode.PushLeaf, 0), new CsgInstruction(CsgOpCode.PushLeaf, 1), new CsgInstruction(CsgOpCode.Or, 0)], 2);

    /// <summary>The opaque white background of the layer tests.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>Creates a triangle with explicit shades.</summary>
    /// <param name="leaf">The leaf.</param>
    /// <param name="back">Whether the triangle is back-facing.</param>
    /// <param name="priority">The draw priority.</param>
    /// <param name="enter">The enter shade.</param>
    /// <param name="exit">The exit shade.</param>
    /// <returns>The triangle.</returns>
    private static ScreenTriangle TriColor(int leaf, bool back, int priority, Rgba32 enter, Rgba32 exit) =>
        new() { Leaf = leaf, BackFacing = back, Priority = priority, EnterColor = enter, ExitColor = exit };

    /// <summary>Resolves one sample with a program list and a background on a copy of the fragments.</summary>
    /// <param name="programs">The programs (main first).</param>
    /// <param name="background">The background.</param>
    /// <param name="triangles">The triangles.</param>
    /// <param name="fragments">The fragments.</param>
    /// <param name="far">The far distance.</param>
    /// <returns>The outcome.</returns>
    private static (bool Found, float Depth, Rgba32 Color, int Meta) ResolveLayers(
        CsgProgram[] programs,
        Rgba32 background,
        ScreenTriangle[] triangles,
        FragmentBuffer.Fragment[] fragments,
        float far = Far) =>
        ResolveWith(new CsgResolver(programs, background), triangles, fragments, Epsilon, far);

    /// <summary>
    ///     Composites straight-alpha layers, nearest first, over a background with the resolver's integer arithmetic.
    /// </summary>
    /// <param name="background">The background.</param>
    /// <param name="layers">The layers, nearest first.</param>
    /// <returns>The composite.</returns>
    private static Rgba32 Composite(Rgba32 background, params Rgba32[] layers)
    {
        var compositor = default(LayerCompositor);
        foreach (var layer in layers)
        {
            compositor.Add(layer);
        }

        return compositor.Finish(background);
    }

    /// <summary>
    ///     Validates that a single opaque layer returns its color exactly over any background.
    /// </summary>
    [Fact]
    public void LayerCompositor_SingleOpaqueLayer_ReturnsLayerColorExactly()
    {
        // Arrange
        Rgba32[] backgrounds = [new(0, 0, 0, 0), White, new(10, 20, 30, 128)];
        Rgba32[] layers = [new(0, 0, 0, 255), new(255, 255, 255, 255), new(17, 130, 241, 255)];

        foreach (var layer in layers)
        {
            foreach (var background in backgrounds)
            {
                // Act
                var compositor = default(LayerCompositor);
                compositor.Add(layer);

                // Assert
                Assert.True(compositor.IsOpaque);
                Assert.Equal(layer, compositor.Finish(background));
            }
        }
    }

    /// <summary>
    ///     Validates that a translucent layer over an opaque background matches the closed form within one level.
    /// </summary>
    [Fact]
    public void LayerCompositor_HalfAlphaOverOpaqueBackground_MatchesClosedForm()
    {
        // Arrange
        var color = new Rgba32(200, 100, 20, 0);
        var background = new Rgba32(10, 250, 90, 255);

        foreach (var alpha in new byte[] { 1, 64, 127, 128, 200, 254 })
        {
            // Act
            var compositor = default(LayerCompositor);
            compositor.Add(new Rgba32(color.R, color.G, color.B, alpha));
            var result = compositor.Finish(background);

            // Assert
            Assert.False(compositor.IsOpaque);
            Assert.Equal(255, result.A);
            Assert.InRange(result.R, ((alpha * color.R) + ((255 - alpha) * background.R) + 127) / 255 - 1, (((alpha * color.R) + ((255 - alpha) * background.R) + 127) / 255) + 1);
            Assert.InRange(result.G, ((alpha * color.G) + ((255 - alpha) * background.G) + 127) / 255 - 1, (((alpha * color.G) + ((255 - alpha) * background.G) + 127) / 255) + 1);
            Assert.InRange(result.B, ((alpha * color.B) + ((255 - alpha) * background.B) + 127) / 255 - 1, (((alpha * color.B) + ((255 - alpha) * background.B) + 127) / 255) + 1);
        }
    }

    /// <summary>
    ///     Validates that over a transparent background the result keeps the straight layer color with the accumulated alpha.
    /// </summary>
    [Fact]
    public void LayerCompositor_TransparentBackground_YieldsAccumulatedAlphaAndStraightColor()
    {
        // Arrange
        var compositor = default(LayerCompositor);
        compositor.Add(new Rgba32(100, 150, 200, 100));

        // Act
        var result = compositor.Finish(new Rgba32(0, 0, 0, 0));

        // Assert
        Assert.Equal(new Rgba32(100, 150, 200, 100), result);
        Assert.Equal(new Rgba32(0, 0, 0, 0), default(LayerCompositor).Finish(new Rgba32(0, 0, 0, 0)));
    }

    /// <summary>
    ///     Validates that the transmittance never goes negative and an opaque layer ends accumulation.
    /// </summary>
    [Fact]
    public void LayerCompositor_ManyLayers_TransmittanceNeverGoesNegative_AndOpaqueStops()
    {
        // Arrange
        var compositor = default(LayerCompositor);
        var previousAlpha = 0;

        // Act / Assert: the accumulated alpha only grows and never exceeds 255
        for (var i = 0; i < 40; i++)
        {
            compositor.Add(new Rgba32(255, 0, 0, 200));
            var alpha = compositor.Finish(new Rgba32(0, 0, 0, 0)).A;
            Assert.InRange(alpha, previousAlpha, 255);
            previousAlpha = alpha;
        }

        Assert.True(previousAlpha > 250);
        compositor.Add(new Rgba32(0, 255, 0, 255));
        Assert.True(compositor.IsOpaque);
        var final = compositor.Finish(White);
        Assert.Equal(255, final.A);
        Assert.Equal(255, final.R);
        Assert.Equal(0, final.G);
    }

    /// <summary>
    ///     Validates that fully transparent layers change nothing.
    /// </summary>
    [Fact]
    public void LayerCompositor_AlphaZeroLayers_ContributeNothing()
    {
        // Arrange
        var compositor = default(LayerCompositor);

        // Act
        compositor.Add(new Rgba32(1, 2, 3, 0));
        compositor.Add(new Rgba32(250, 251, 252, 0));

        // Assert
        Assert.False(compositor.IsOpaque);
        Assert.Equal(White, compositor.Finish(White));
        Assert.Equal(new Rgba32(0, 0, 0, 0), compositor.Finish(new Rgba32(0, 0, 0, 0)));
    }

    /// <summary>
    ///     Validates the closed-form composite of a translucent solid in front of an opaque one.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_TranslucentFront_CompositesOverOpaqueBack()
    {
        // Arrange: translucent red solid [1, 2] in front of an opaque blue solid [3, 4]; union program
        var red = new Rgba32(255, 0, 0, 128);
        var blue = new Rgba32(0, 0, 255, 255);
        ScreenTriangle[] triangles =
        [
            TriColor(0, false, 0, red, red), TriColor(0, true, 0, red, red),
            TriColor(1, false, 1, blue, blue), TriColor(1, true, 1, blue, blue),
        ];
        var fragments = new[] { F(1, 0), F(2, 1), F(3, 2), F(4, 3) };

        // Act
        var result = ResolveLayers([UnionProgram()], White, triangles, fragments);

        // Assert: 128 + 64 of red, the remaining 63 of blue; the opaque layer ends the sweep
        Assert.True(result.Found);
        Assert.Equal(new Rgba32(192, 0, 63, 255), result.Color);
        Assert.Equal(1.0f, result.Depth);
        Assert.Equal(0, result.Meta);
    }

    /// <summary>
    ///     Validates that the layer order follows depth, never the insertion order of the fragments.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_TwoTranslucentLayers_AreOrderedByDepth_NotInsertion()
    {
        // Arrange: two separate translucent solids, red [1, 2] and blue [3, 4]
        var red = new Rgba32(255, 0, 0, 128);
        var blue = new Rgba32(0, 0, 255, 128);
        ScreenTriangle[] triangles =
        [
            TriColor(0, false, 0, red, red), TriColor(0, true, 0, red, red),
            TriColor(1, false, 1, blue, blue), TriColor(1, true, 1, blue, blue),
        ];
        var expected = default(LayerCompositor);
        expected.Add(red);
        expected.Add(red);
        expected.Add(blue);
        expected.Add(blue);
        var resolver = new CsgResolver([UnionProgram()], White);

        // Act / Assert: every insertion order yields the depth-ordered composite
        var sorted = new[] { F(1, 0), F(2, 1), F(3, 2), F(4, 3) };
        foreach (var order in Permutations([0, 1, 2, 3]))
        {
            var result = ResolveWith(resolver, triangles, [.. order.Select(i => sorted[i])]);
            Assert.True(result.Found);
            Assert.Equal(expected.Finish(White), result.Color);
        }

        // The composite really depends on the depth order (swapping the colors changes it)
        var swapped = default(LayerCompositor);
        swapped.Add(blue);
        swapped.Add(blue);
        swapped.Add(red);
        swapped.Add(red);
        Assert.NotEqual(expected.Finish(White), swapped.Finish(White));
    }

    /// <summary>
    ///     Validates that an opaque first layer stops the sweep and gives exactly the classic result over any background.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_OpaqueFirstLayer_StopsSweep_AndEqualsLegacyResult()
    {
        // Arrange: the classic A - B ray that hits the minuend first, with more events behind it
        var fragments = new[] { F(4, 1), F(2, 2), F(1, 0), F(3, 3) };
        var program = CsgCompiler.Compile(Difference()).Program!;
        var legacy = ResolveWith(new CsgResolver(program), DifferenceTriangles(), fragments);

        // Act: a transparent, a translucent and an opaque background, plus an unused overlay program
        var results = new[]
        {
            ResolveLayers([program], new Rgba32(0, 0, 0, 0), DifferenceTriangles(), fragments),
            ResolveLayers([program], new Rgba32(1, 2, 3, 77), DifferenceTriangles(), fragments),
            ResolveLayers([program], White, DifferenceTriangles(), fragments),
        };

        // Assert
        Assert.Equal(Enter(0, 0), legacy.Color);
        Assert.All(results, r => Assert.Equal(legacy, r));
    }

    /// <summary>
    ///     Validates that the far wall of a translucent solid uses its own shade, not the back-face (exit) shade.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_TranslucentExitAfterEnter_UsesOwnShadeNotBackFaceColor()
    {
        // Arrange: the back triangle's exit shade is an opaque green that would hide everything if it were used
        var red = new Rgba32(255, 0, 0, 128);
        var blue = new Rgba32(0, 0, 255, 128);
        var green = new Rgba32(0, 255, 0, 255);
        ScreenTriangle[] triangles = [TriColor(0, false, 0, red, green), TriColor(0, true, 0, blue, green)];

        // Act
        var result = ResolveLayers([LeafProgram(0, 1)], White, triangles, [F(1, 0), F(2, 1)]);

        // Assert: 128 of red, 64 of blue, 63 of the white background
        Assert.Equal(new Rgba32(191, 63, 127, 255), result.Color);
    }

    /// <summary>
    ///     Validates that a ray starting inside a translucent main solid shows the exit shade with its alpha.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_RayStartsInsideTranslucentMain_UsesExitColorWithAlpha()
    {
        // Arrange: only the back face is ahead; the exit shade is translucent green, the enter shade is opaque red
        var exit = new Rgba32(0, 255, 0, 128);
        ScreenTriangle[] triangles = [TriColor(0, true, 0, new Rgba32(255, 0, 0, 255), exit)];

        // Act
        var result = ResolveLayers([LeafProgram(0, 1)], White, triangles, [F(2, 0)]);

        // Assert
        Assert.True(result.Found);
        Assert.Equal(new Rgba32(127, 255, 127, 255), result.Color);
        Assert.Equal(2.0f, result.Depth);
        Assert.Equal(1, result.Meta);
    }

    /// <summary>
    ///     Validates that a ray starting inside an overlay body shows the exit shade with the overlay's alpha,
    ///     exactly like a ray starting inside a translucent main solid.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_RayStartsInsideOverlay_UsesExitColorWithAlpha()
    {
        // Arrange: opaque green main [3, 4] behind a translucent overlay whose only fragment ahead is its back face at 2;
        // the exit shade is translucent red, the enter shade is translucent blue
        var green = new Rgba32(0, 255, 0, 255);
        var exit = new Rgba32(255, 0, 0, 128);
        var enter = new Rgba32(0, 0, 255, 128);
        ScreenTriangle[] triangles =
        [
            TriColor(0, false, 0, green, green), TriColor(0, true, 0, green, green),
            TriColor(1, true, 1, enter, exit),
        ];

        // Act
        var result = ResolveLayers([LeafProgram(0, 2), LeafProgram(1, 2)], White, triangles, [F(2, 2), F(3, 0), F(4, 1)]);

        // Assert: the exit layer tints the green main face; depth and meta come from the overlay's back face
        Assert.Equal(Composite(White, exit, green), result.Color);
        Assert.Equal(2.0f, result.Depth);
        Assert.Equal((1 << 1) | 1, result.Meta);
    }

    /// <summary>
    ///     Validates that only the first change of an overlay uses the exit shade: a second pass through the overlay
    ///     enters and leaves it with the enter shade.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_OverlayRayStartsInsideThenReenters_UsesExitShadeOnlyOnce()
    {
        // Arrange: the ray leaves the overlay at 1 (its start was inside), re-enters at 2 and leaves again at 3
        var exit = new Rgba32(255, 0, 0, 128);
        var enter = new Rgba32(0, 0, 255, 128);
        ScreenTriangle[] triangles = [TriColor(0, false, 1, enter, exit), TriColor(0, true, 1, enter, exit), TriColor(0, false, 1, enter, exit), TriColor(0, true, 1, enter, exit)];

        // Act: the leaf has a back face at 1 and 3 and a front face at 2, so the ray starts inside it twice over
        var result = ResolveLayers([LeafProgram(0, 1)], White, triangles, [F(1, 1), F(2, 2), F(3, 3)]);

        // Assert: exit shade, then the enter shade at the front face, then the enter shade at the far wall
        Assert.Equal(Composite(White, exit, enter, enter), result.Color);
    }

    /// <summary>
    ///     Validates that the exit shade of a program is not used for the first change of another program.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_RayStartsInsideMainAndOverlay_EachProgramUsesItsOwnFirstExit()
    {
        // Arrange: both programs start inside; the overlay leaves at 1, the main leaves at 2
        var mainExit = new Rgba32(255, 0, 0, 128);
        var mainEnter = new Rgba32(1, 1, 1, 128);
        var ghostExit = new Rgba32(0, 0, 255, 64);
        var ghostEnter = new Rgba32(2, 2, 2, 64);
        ScreenTriangle[] triangles = [TriColor(0, true, 0, mainEnter, mainExit), TriColor(1, true, 1, ghostEnter, ghostExit)];

        // Act
        var result = ResolveLayers([LeafProgram(0, 2), LeafProgram(1, 2)], White, triangles, [F(1, 1), F(2, 0)]);

        // Assert
        Assert.Equal(Composite(White, ghostExit, mainExit), result.Color);
    }

    /// <summary>
    ///     Validates that layers beyond the far distance are ignored.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_LayersBeyondFar_AreIgnored()
    {
        // Arrange
        var red = new Rgba32(255, 0, 0, 128);
        ScreenTriangle[] triangles = [TriColor(0, false, 0, red, red), TriColor(0, true, 0, red, red)];

        // Act
        var result = ResolveLayers([LeafProgram(0, 1)], White, triangles, [F(1, 0), F(2, 1)], far: 1.5f);

        // Assert: only the front layer counts
        Assert.True(result.Found);
        Assert.Equal(new Rgba32(255, 127, 127, 255), result.Color);
    }

    /// <summary>
    ///     Validates that an overlay layer is applied before a coincident main layer, so it tints rather than hides.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_OverlayCoincidentWithMain_OverlayIsInFront()
    {
        // Arrange: main opaque green and overlay translucent red share depth 1 (main has the lower triangle index and priority)
        var green = new Rgba32(0, 255, 0, 255);
        var red = new Rgba32(255, 0, 0, 128);
        ScreenTriangle[] triangles =
        [
            TriColor(0, false, 0, green, green), TriColor(0, true, 0, green, green),
            TriColor(1, false, 1, red, red), TriColor(1, true, 1, red, red),
        ];

        // Act
        var result = ResolveLayers([LeafProgram(0, 2), LeafProgram(1, 2)], White, triangles, [F(1, 0), F(5, 1), F(1, 2), F(4, 3)]);

        // Assert: 128 of red over the remaining 127 of green; depth and meta come from the overlay layer
        Assert.Equal(new Rgba32(128, 127, 0, 255), result.Color);
        Assert.Equal(1.0f, result.Depth);
        Assert.Equal(1 << 1, result.Meta);
    }

    /// <summary>
    ///     Validates that an overlay behind an opaque main surface is hidden.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_OverlayBehindOpaqueMain_IsHidden()
    {
        // Arrange: opaque green main [1, 2], translucent red overlay [3, 4]
        var green = new Rgba32(0, 255, 0, 255);
        var red = new Rgba32(255, 0, 0, 128);
        ScreenTriangle[] triangles =
        [
            TriColor(0, false, 0, green, green), TriColor(0, true, 0, green, green),
            TriColor(1, false, 1, red, red), TriColor(1, true, 1, red, red),
        ];

        // Act
        var result = ResolveLayers([LeafProgram(0, 2), LeafProgram(1, 2)], White, triangles, [F(1, 0), F(2, 1), F(3, 2), F(4, 3)]);

        // Assert
        Assert.Equal(green, result.Color);
        Assert.Equal(1.0f, result.Depth);
    }

    /// <summary>
    ///     Validates that overlay leaves never influence the main program (an overlay does not subtract).
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_OverlayLeavesDoNotAffectMainProgram()
    {
        // Arrange: translucent main A [1, 4] containing a translucent overlay [2, 3]; compiled as A - overlay(B)
        var main = new Rgba32(0, 200, 0, 128);
        var ghost = new Rgba32(255, 0, 0, 64);
        var compilation = CsgCompiler.Compile(new CsgNode(CsgOperation.Difference, [M(), new OverlayNode(ghost, [M()])]));
        ScreenTriangle[] triangles =
        [
            TriColor(0, false, 0, main, main), TriColor(0, true, 0, main, main),
            TriColor(1, false, 1, ghost, ghost), TriColor(1, true, 1, ghost, ghost),
        ];
        var fragments = new[] { F(1, 0), F(2, 2), F(3, 3), F(4, 1) };
        var expected = default(LayerCompositor);
        expected.Add(main);
        expected.Add(ghost);
        expected.Add(ghost);
        expected.Add(main);

        // Act
        var result = ResolveLayers([compilation.Program!, .. compilation.OverlayPrograms!], White, triangles, fragments);

        // Assert: main enters, overlay enters and leaves, main leaves; there is no cut where the overlay is
        Assert.Equal(expected.Finish(White), result.Color);
    }

    /// <summary>
    ///     Validates that no scratch state survives between samples when overlays are present.
    /// </summary>
    [Fact]
    public void CsgResolver_Resolve_ScratchIsClearedBetweenSamples_WithOverlays()
    {
        // Arrange
        var main = new Rgba32(0, 200, 0, 128);
        var ghost = new Rgba32(255, 0, 0, 64);
        ScreenTriangle[] triangles =
        [
            TriColor(0, false, 0, main, main), TriColor(0, true, 0, main, main),
            TriColor(1, false, 1, ghost, ghost), TriColor(1, true, 1, ghost, ghost),
        ];
        var resolver = new CsgResolver([LeafProgram(0, 2), LeafProgram(1, 2)], White);
        var sampleA = new[] { F(1, 0), F(2, 2), F(3, 3), F(4, 1) };
        var sampleB = new[] { F(2, 3), F(2.5f, 1), F(0.5f, 2) };
        var sampleC = new[] { F(1, 2), F(1.2f, 3), F(5, 0), F(6, 1) };

        // Act
        var first = ResolveWith(resolver, triangles, sampleA);
        var other = ResolveWith(resolver, triangles, sampleB);
        var third = ResolveWith(resolver, triangles, sampleC);
        var again = ResolveWith(resolver, triangles, sampleA);

        // Assert
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.NotEqual(first, third);
        Assert.Equal(other, ResolveWith(new CsgResolver([LeafProgram(0, 2), LeafProgram(1, 2)], White), triangles, sampleB));
    }

    /// <summary>
    ///     Validates the argument checks of the program-list constructor.
    /// </summary>
    [Fact]
    public void CsgResolver_Constructor_NullOrEmptyProgramList_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new CsgResolver((IReadOnlyList<CsgProgram>)null!));
        Assert.Throws<ArgumentException>(() => new CsgResolver(Array.Empty<CsgProgram>()));
        Assert.Throws<ArgumentException>(() => new CsgResolver([LeafProgram(0, 1), null!]));

        // A leaf may belong to only one program
        Assert.Throws<ArgumentException>(() => new CsgResolver([LeafProgram(0, 1), LeafProgram(0, 1)]));
    }

    /// <summary>
    ///     Creates a random box mesh node and records its box.
    /// </summary>
    /// <param name="random">The random source.</param>
    /// <param name="boxes">The node-to-box map.</param>
    /// <param name="size">Zero for a unit-footprint box, one for a medium box, two for a large box (more likely to overlap others).</param>
    /// <returns>The node.</returns>
    private static MeshNode NewBox(Random random, Dictionary<MeshNode, Box> boxes, int size = 1)
    {
        var x0 = random.Next(0, size == 2 ? 2 : 3);
        var y0 = random.Next(0, size == 2 ? 2 : 3);
        var z0 = random.Next(-2, size == 2 ? 2 : 6);
        var node = M();
        boxes[node] = size switch
        {
            0 => new Box(x0, y0, z0, x0 + 1, y0 + 1, z0 + random.Next(1, 3)),
            2 => new Box(x0, y0, z0, x0 + random.Next(3, 5), y0 + random.Next(3, 5), z0 + random.Next(5, 9)),
            _ => new Box(x0, y0, z0, x0 + random.Next(1, 4), y0 + random.Next(1, 4), z0 + random.Next(1, 7)),
        };
        return node;
    }

    /// <summary>
    ///     Builds a random scene tree (occasionally reusing a node, which makes it a DAG).
    /// </summary>
    /// <param name="random">The random source.</param>
    /// <param name="boxes">The node-to-box map.</param>
    /// <param name="pool">The nodes created so far (candidates for reuse).</param>
    /// <param name="depth">The remaining depth.</param>
    /// <returns>The tree.</returns>
    private static SceneNode RandomTree(Random random, Dictionary<MeshNode, Box> boxes, List<MeshNode> pool, int depth)
    {
        if (depth == 0 || random.Next(10) < 3)
        {
            if (pool.Count > 0 && random.Next(10) == 0)
            {
                return pool[random.Next(pool.Count)];
            }

            var leaf = NewBox(random, boxes);
            pool.Add(leaf);
            return leaf;
        }

        var kind = random.Next(4);
        var count = kind < 2 ? random.Next(0, 4) : random.Next(1, 5);
        var children = Enumerable.Range(0, count).Select(_ => RandomTree(random, boxes, pool, depth - 1)).ToArray();
        return kind switch
        {
            0 => new GroupNode(children),
            1 => new CsgNode(CsgOperation.Union, children),
            2 => new CsgNode(CsgOperation.Difference, children),
            _ => new CsgNode(CsgOperation.Intersection, children),
        };
    }

    /// <summary>
    ///     The analytic definition of the solid: is the point at ray (x, y) and depth t inside the tree?
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="boxes">The node-to-box map.</param>
    /// <param name="x">The ray X.</param>
    /// <param name="y">The ray Y.</param>
    /// <param name="t">The depth.</param>
    /// <returns><see langword="true"/> when inside.</returns>
    private static bool Inside(SceneNode node, Dictionary<MeshNode, Box> boxes, double x, double y, double t)
    {
        switch (node)
        {
            case MeshNode mesh:
                return boxes[mesh].Contains(x, y, t);
            case GroupNode group:
                return group.Children.Any(c => Inside(c, boxes, x, y, t));
            default:
                var csg = (CsgNode)node;
                return csg.Operation switch
                {
                    CsgOperation.Union => csg.Children.Any(c => Inside(c, boxes, x, y, t)),
                    CsgOperation.Difference => csg.Children.Count > 0
                        && Inside(csg.Children[0], boxes, x, y, t)
                        && !csg.Children.Skip(1).Any(c => Inside(c, boxes, x, y, t)),
                    _ => csg.Children.Count > 0 && csg.Children.All(c => Inside(c, boxes, x, y, t)),
                };
        }
    }

    /// <summary>
    ///     Resolves a grid of rays through a box scene and compares each with the analytic oracle, in sorted and shuffled order.
    /// </summary>
    /// <param name="root">The scene root.</param>
    /// <param name="boxes">The node-to-box map.</param>
    /// <param name="random">The random source for shuffling.</param>
    /// <returns>The totals.</returns>
    private static Stats RunDifferential(SceneNode root, Dictionary<MeshNode, Box> boxes, Random random)
    {
        var compilation = CsgCompiler.Compile(root);
        if (compilation.Program is null)
        {
            // Union-only scenes are rendered by the z-buffer and never reach the resolver
            return default;
        }

        // Triangle 2i is the front and 2i + 1 the back face of instance i; its colors identify the instance (red/blue) and the event kind (green)
        var instances = new Scene(root).EnumerateInstances().ToArray();
        var triangles = new ScreenTriangle[instances.Length * 2];
        for (var i = 0; i < instances.Length; i++)
        {
            var leaf = compilation.LeafOfInstance[i];
            var enter = new Rgba32((byte)(i & 255), 1, (byte)(i >> 8), 255);
            var exit = new Rgba32((byte)(i & 255), 2, (byte)(i >> 8), 255);
            triangles[2 * i] = new ScreenTriangle { Leaf = leaf, BackFacing = false, Priority = i, EnterColor = enter, ExitColor = exit };
            triangles[(2 * i) + 1] = new ScreenTriangle { Leaf = leaf, BackFacing = true, Priority = i, EnterColor = enter, ExitColor = exit };
        }

        var resolver = new CsgResolver(compilation.Program);
        var rays = 0;
        var visible = 0;
        var exits = 0;
        var ties = 0;
        for (var ix = 0; ix < 5; ix++)
        {
            for (var iy = 0; iy < 5; iy++)
            {
                double x = ix + 0.5;
                double y = iy + 0.5;

                // Fragments in front of the camera (depth > 0) only: boxes straddling depth 0 contribute just their exit
                var fragments = new List<FragmentBuffer.Fragment>();
                for (var i = 0; i < instances.Length; i++)
                {
                    var box = boxes[instances[i].Node];
                    if (!box.Covers(x, y))
                    {
                        continue;
                    }

                    if (box.Z0 > 0)
                    {
                        fragments.Add(F(box.Z0, 2 * i));
                    }

                    if (box.Z1 > 0)
                    {
                        fragments.Add(F(box.Z1, (2 * i) + 1));
                    }
                }

                // The analytic expectation: the first event depth where the solid changes, given the state at depth 0.5
                var expectedFound = false;
                var expectedDepth = 0.0f;
                var expectedEnter = false;
                var previous = Inside(root, boxes, x, y, 0.5);
                foreach (var depth in fragments.Select(f => f.Depth).Distinct().Order())
                {
                    var now = Inside(root, boxes, x, y, depth + 0.5);
                    if (now != previous)
                    {
                        expectedFound = true;
                        expectedDepth = depth;
                        expectedEnter = now;
                        break;
                    }
                }

                var sorted = ResolveWith(resolver, triangles, [.. fragments], 0.25f);
                var shuffled = ResolveWith(resolver, triangles, [.. fragments.OrderBy(_ => random.Next())], 0.25f);

                Assert.Equal(expectedFound, sorted.Found);
                Assert.Equal(sorted, shuffled);
                rays++;
                if (expectedFound)
                {
                    var chosen = sorted.Color.R | (sorted.Color.B << 8);
                    Assert.Equal(expectedDepth, sorted.Depth);
                    Assert.Equal(expectedEnter ? 1 : 2, sorted.Color.G);
                    Assert.Contains(fragments, f => Math.Abs(f.Depth - expectedDepth) < 1e-6f && f.Triangle / 2 == chosen && (f.Triangle & 1) == (sorted.Meta & 1));
                    Assert.Equal(chosen << 1, sorted.Meta & ~1);
                    visible++;
                    exits += expectedEnter ? 0 : 1;
                    ties += fragments.Count(f => Math.Abs(f.Depth - expectedDepth) < 1e-6f) > 1 ? 1 : 0;
                }
            }
        }

        return new Stats(rays, visible, exits, ties);
    }

    /// <summary>
    ///     Enumerates all permutations of the items.
    /// </summary>
    /// <param name="items">The items (at most a handful).</param>
    /// <returns>Every ordering.</returns>
    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (var i = 0; i < items.Length; i++)
        {
            var rest = items.Where((_, index) => index != i).ToArray();
            foreach (var tail in Permutations(rest))
            {
                yield return [items[i], .. tail];
            }
        }
    }
}

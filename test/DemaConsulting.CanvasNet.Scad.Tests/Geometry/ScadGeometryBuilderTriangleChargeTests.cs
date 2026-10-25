// cspell:ignore extrude polyhedron
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests that the triangle budget of <see cref="ScadGeometryBuilder"/> is charged with exactly the triangles of
///     the meshes created, for every geometry path, and that the exact count (not an estimate) is what the limit
///     applies to, in particular for extrusions of profiles with holes and several contours.
/// </summary>
public class ScadGeometryBuilderTriangleChargeTests
{
    /// <summary>One outer boundary and two holes, 4 + 4 + 4 = 12 vertices.</summary>
    private const string OuterWithTwoHoles =
        "polygon([[0, 0], [20, 0], [20, 10], [0, 10], [2, 2], [8, 2], [8, 8], [2, 8], [12, 2], [18, 2], [18, 8], [12, 8]], " +
        "[[0, 1, 2, 3], [4, 5, 6, 7], [8, 9, 10, 11]])";

    /// <summary>
    ///     Validates that the number charged equals the number of triangles of the meshes in the scene for a matrix of
    ///     shapes: primitives, polyhedra, flat slabs, linear extrusions (holes, islands, twist, slices, scale, apex,
    ///     3D-over-2D differences) and rotate extrusions (full, partial, holes, poles, negative profiles and angles).
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("cube(2);")]
    [InlineData("cube([1, 2, 3], center = true);")]
    [InlineData("sphere(1, $fn = 3);")]
    [InlineData("sphere(2, $fn = 8);")]
    [InlineData("sphere(2, $fn = 9);")]
    [InlineData("sphere(5);")]
    [InlineData("cylinder(h = 2, r = 1, $fn = 7);")]
    [InlineData("cylinder(h = 2, r1 = 2, r2 = 1, $fn = 9);")]
    [InlineData("cylinder(h = 2, r1 = 2, r2 = 0, $fn = 9);")]
    [InlineData("cylinder(h = 2, r1 = 0, r2 = 2, $fn = 9);")]
    [InlineData("polyhedron([[0,0,0],[1,0,0],[0,1,0],[0,0,1]], [[0,1,2],[0,3,1],[1,3,2],[2,3,0]]);")]
    [InlineData("polyhedron([[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]], [[0,1,2,3],[4,5,1,0],[7,6,5,4],[5,6,2,1],[6,7,3,2],[7,4,0,3]]);")]
    [InlineData("square(3);")]
    [InlineData("circle(2, $fn = 12);")]
    [InlineData("polygon([[0, 0], [10, 0], [10, 10], [0, 10], [2, 2], [8, 2], [8, 8], [2, 8]], [[0, 1, 2, 3], [4, 5, 6, 7]]);")]
    [InlineData("square(3); translate([5, 0]) circle(1, $fn = 5);")]
    [InlineData("difference() { square(10); translate([2, 2]) circle(3, $fn = 10); }")]
    [InlineData("linear_extrude(2) square(3);")]
    [InlineData("linear_extrude(2) circle(3, $fn = 11);")]
    [InlineData("linear_extrude(2) polygon([[0, 0], [10, 0], [10, 10], [0, 10], [2, 2], [8, 2], [8, 8], [2, 8]], [[0, 1, 2, 3], [4, 5, 6, 7]]);")]
    [InlineData("linear_extrude(2) polygon([[0, 0], [20, 0], [20, 20], [0, 20], [3, 3], [17, 3], [17, 17], [3, 17], [6, 6], [14, 6], [14, 14], [6, 14]], [[0, 1, 2, 3], [4, 5, 6, 7], [8, 9, 10, 11]]);")]
    [InlineData("linear_extrude(2) { square(3); translate([5, 0]) square(3); translate([10, 0]) circle(1, $fn = 6); }")]
    [InlineData("linear_extrude(2) difference() { square(10); translate([2, 2]) square(3); translate([6, 6]) square(3); }")]
    [InlineData("linear_extrude(height = 4, slices = 5) square(3);")]
    [InlineData("linear_extrude(height = 4, twist = 90) square(3);")]
    [InlineData("linear_extrude(height = 4, twist = 90, slices = 3) polygon([[0, 0], [10, 0], [10, 10], [0, 10], [2, 2], [8, 2], [8, 8], [2, 8]], [[0, 1, 2, 3], [4, 5, 6, 7]]);")]
    [InlineData("linear_extrude(height = 4, scale = 2, center = true) circle(2, $fn = 9);")]
    [InlineData("linear_extrude(height = 4, scale = [1, 2], slices = 2) polygon([[1, 1], [10, 1], [10, 10], [1, 10], [3, 3], [8, 3], [8, 8], [3, 8]], [[0, 1, 2, 3], [4, 5, 6, 7]]);")]
    [InlineData("linear_extrude(height = 4, scale = 0) circle(2, $fn = 9);")]
    [InlineData("linear_extrude(height = 4, scale = 0, slices = 4) square(2, center = true);")]
    [InlineData("rotate_extrude($fn = 12) translate([2, 0]) square(1);")]
    [InlineData("rotate_extrude(angle = 90, $fn = 12) translate([2, 0]) square([1, 5]);")]
    [InlineData("rotate_extrude(angle = 200, $fn = 9) translate([2, 0]) circle(1, $fn = 7);")]
    [InlineData("rotate_extrude(angle = -45, $fn = 16) translate([2, 0]) square([1, 5]);")]
    [InlineData("rotate_extrude($fn = 10) square([2, 3]);")]
    [InlineData("rotate_extrude(angle = 120, $fn = 10) polygon([[0, 0], [2, 0], [0, 3]]);")]
    [InlineData("rotate_extrude($fn = 16) polygon([[1, 0], [4, 0], [4, 4], [1, 4], [2, 1], [3, 1], [3, 3], [2, 3]], [[0, 1, 2, 3], [4, 5, 6, 7]]);")]
    [InlineData("rotate_extrude(angle = 90, $fn = 16) polygon([[1, 0], [4, 0], [4, 4], [1, 4], [2, 1], [3, 1], [3, 3], [2, 3]], [[0, 1, 2, 3], [4, 5, 6, 7]]);")]
    [InlineData("rotate_extrude(angle = 90, $fn = 8) translate([-4, 0]) square([1, 2]);")]
    [InlineData("rotate_extrude(angle = -90, $fn = 8) translate([-4, 0]) square([1, 2]);")]
    [InlineData("rotate_extrude($fn = 8) difference() { translate([2, 0]) square([3, 3]); translate([3, 1]) square(1); }")]
    [InlineData("rotate_extrude(angle = 60, $fn = 8) { translate([2, 0]) square(1); translate([5, 0]) square([1, 2]); }")]
    [InlineData("translate([3, 0, 0]) rotate([0, 0, 30]) linear_extrude(1) square(2); cube(1); sphere(1, $fn = 6);")]
    public void ScadGeometryBuilder_Build_ChargesExactlyTheTrianglesCreated(string source)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert: something was built, nothing was reported as rejected, and the charge is the mesh triangle count
        Assert.NotEmpty(result.Meshes);
        Assert.DoesNotContain("scad-geometry-rejected", result.Features);
        Assert.Equal(result.MeshTriangles, result.TrianglesCharged);
    }

    /// <summary>
    ///     Validates that every limit between the exact count and one below it separates acceptance from rejection, for
    ///     every kind of geometry: the limit equal to the charge accepts and the limit one below rejects.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("cube(2);")]
    [InlineData("sphere(2, $fn = 9);")]
    [InlineData("cylinder(h = 2, r1 = 2, r2 = 0, $fn = 9);")]
    [InlineData("circle(2, $fn = 12);")]
    [InlineData("linear_extrude(2) polygon([[0, 0], [10, 0], [10, 10], [0, 10], [2, 2], [8, 2], [8, 8], [2, 8]], [[0, 1, 2, 3], [4, 5, 6, 7]]);")]
    [InlineData("linear_extrude(height = 4, scale = 0, slices = 4) square(2, center = true);")]
    [InlineData("rotate_extrude(angle = 90, $fn = 16) polygon([[1, 0], [4, 0], [4, 4], [1, 4], [2, 1], [3, 1], [3, 3], [2, 3]], [[0, 1, 2, 3], [4, 5, 6, 7]]);")]
    [InlineData("rotate_extrude(angle = 120, $fn = 10) polygon([[0, 0], [2, 0], [0, 3]]);")]
    public void ScadGeometryBuilder_Build_LimitAtTheExactCount_AcceptsAndOneBelowRejects(string source)
    {
        // Arrange
        var exact = Build(source).TrianglesCharged;

        // Act
        var accepted = Build(source, Settings(maxTriangles: exact));
        var rejected = Assert.Throws<ScadEvaluationException>(() => Build(source, Settings(maxTriangles: exact - 1)));

        // Assert
        Assert.Equal(exact, accepted.MeshTriangles);
        Assert.Contains($"limit {exact - 1}", rejected.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the boundary of a linear extrusion of one outer boundary with two holes (12 vertices): 24 wall
    ///     triangles and two caps of 12 + 2 * 2 - 2 * 1 = 14 triangles are 52, so a limit of 51 rejects and 52 accepts
    ///     (a count of four triangles per vertex, 48, would have wrongly accepted a limit of 48).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_LinearExtrudeOfOuterWithTwoHoles_ChargesFiftyTwo()
    {
        // Arrange
        var source = $"linear_extrude(1) {OuterWithTwoHoles};";

        // Act
        var accepted = Build(source, Settings(maxTriangles: 52));
        var rejected = Assert.Throws<ScadEvaluationException>(() => Build(source, Settings(maxTriangles: 51)));
        var fourPerVertex = Assert.Throws<ScadEvaluationException>(() => Build(source, Settings(maxTriangles: 48)));

        // Assert
        Assert.Equal(52, accepted.SingleMesh.TriangleCount);
        Assert.Equal(52, accepted.TrianglesCharged);
        Assert.Contains("limit 51", rejected.Message, StringComparison.Ordinal);
        Assert.Contains("limit 48", fourPerVertex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the same boundary for a partial <c>rotate_extrude</c> of a profile with a hole and for the slab of
    ///     a flat shape with two holes: the exact counts are the limits.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_RotateExtrudeAndSlabWithHoles_ChargeTheExactCaps()
    {
        // Arrange: a profile of 8 vertices with a hole; 4 segments of a quarter turn of $fn = 16 give
        // 4 * 16 wall triangles and two caps of 8 + 2 - 2 = 8 triangles each
        var rotate = "rotate_extrude(angle = 90, $fn = 16) polygon([[1, 0], [4, 0], [4, 4], [1, 4], [2, 1], [3, 1], [3, 3], [2, 3]], [[0, 1, 2, 3], [4, 5, 6, 7]]);";

        // Act: a slab has 2 * 12 wall triangles and two caps of 14 triangles
        var rotated = Build(rotate, Settings(maxTriangles: 80));
        var rotateRejected = Assert.Throws<ScadEvaluationException>(() => Build(rotate, Settings(maxTriangles: 79)));
        var slab = Build(OuterWithTwoHoles + ";", Settings(maxTriangles: 52));
        var slabRejected = Assert.Throws<ScadEvaluationException>(() => Build(OuterWithTwoHoles + ";", Settings(maxTriangles: 51)));

        // Assert
        Assert.Equal(80, rotated.SingleMesh.TriangleCount);
        Assert.Contains("limit 79", rotateRejected.Message, StringComparison.Ordinal);
        Assert.Equal(52, slab.SingleMesh.TriangleCount);
        Assert.Contains("limit 51", slabRejected.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an extrusion that does not fit the budget is refused before any geometry is built, even when
    ///     its profile has many holes (the cost of the caps is part of the check).
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_ExtrusionOverBudget_FailsWithTheLocation()
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Build($"\n  linear_extrude(1) {OuterWithTwoHoles};", Settings(maxTriangles: 51)));

        // Assert
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
        Assert.Contains("Triangle limit exceeded (limit 51)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a hostile slice count over several profile leaves is refused before anything is
    ///     allocated, whatever the budget: the aggregate estimate saturates, and it must neither be charged nor be
    ///     refunded leaf by leaf (which would drive the counter negative and let later geometry bypass the limit).
    /// </summary>
    /// <param name="maxTriangles">The triangle budget, down to the largest representable one.</param>
    [Theory]
    [InlineData(5_000_000L)]
    [InlineData(long.MaxValue)]
    public void ScadGeometryBuilder_Build_HugeSlicesWithTwoLeaves_IsRefusedWhateverTheBudget(long maxTriangles)
    {
        // Arrange
        const string Source = "\n  linear_extrude(height = 1, slices = 1e308) { square(1); translate([5, 0]) square(1); }";

        // Act
        var ex = Assert.Throws<ScadEvaluationException>(() => Build(Source, Settings(maxTriangles: maxTriangles)));

        // Assert
        Assert.Equal(new ScadSourceLocation(null, 2, 3), ex.Location);
        Assert.Contains("Triangle limit exceeded", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that many leaves whose saturated charges would overflow a sum are refused as over the limit,
    ///     not charged with a wrapped-around (possibly negative) total.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_ManySaturatedLeaves_AreRefusedNotWrapped()
    {
        // Arrange: twelve leaves of 1e18 each would sum past the range of a long
        var leaves = string.Join(' ', Enumerable.Range(0, 12).Select(i => $"translate([{i * 3}, 0]) square(1);"));
        var source = $"linear_extrude(height = 1, slices = 1e308) {{ {leaves} }}";

        // Act / Assert
        Assert.Throws<ScadEvaluationException>(() => Build(source, Settings(maxTriangles: long.MaxValue)));
    }

    /// <summary>
    ///     Validates that the charge of an extrusion the renderer's extruder rejects is returned in full (every
    ///     leaf, the rejected one and the ones never attempted), so that the counter equals the triangles of the
    ///     meshes that exist and the budget is still available to the geometry that follows.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilder_Build_RejectedExtrusion_RefundsItsWholeCharge()
    {
        // Arrange: an angle too small for the extruder (reported as rejected); the two leaves charge 24 triangles
        // together, and the two cubes that follow need exactly 24, so the budget is only enough when all is refunded
        const string Source =
            "rotate_extrude(angle = 5e-324) { translate([1, 0]) square(1); translate([4, 0]) square(1); }\n" +
            "cube(1); translate([3, 0, 0]) cube(1);";

        // Act
        var result = Build(Source, Settings(maxTriangles: 24));

        // Assert
        Assert.Contains("scad-geometry-rejected", result.Features);
        Assert.Equal(2, result.Meshes.Count);
        Assert.Equal(24, result.MeshTriangles);
        Assert.Equal(result.MeshTriangles, result.TrianglesCharged);
    }
}

// cspell:ignore multmatrix
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Unit tests of <see cref="DemaConsulting.CanvasNet.Scad.Geometry.ScadTransforms"/>: <c>translate</c>,
///     <c>rotate</c>, <c>scale</c>, <c>mirror</c> and <c>multmatrix</c>, on solids and on 2D shapes, checked with
///     analytic bounds and volumes of an asymmetric <c>cube([1, 2, 3])</c> (extents 1, 2 and 3 identify every axis).
/// </summary>
public class ScadTransformsTests
{
    /// <summary>The program text of the asymmetric test solid.</summary>
    private const string Box = "cube([1, 2, 3]);";

    /// <summary>
    ///     Builds a program and asserts that its single solid has the given world bounds and keeps the volume of
    ///     the box times a factor (a rigid or scaled transform keeps the solid closed and outward oriented).
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="min">The expected minimum corner.</param>
    /// <param name="max">The expected maximum corner.</param>
    /// <param name="volume">The expected volume.</param>
    private static void AssertBox(string source, double[] min, double[] max, double volume = 6.0)
    {
        var result = Build(source);
        var mesh = result.WorldMesh;
        AssertBounds(mesh.Bounds, new Vector3D(min[0], min[1], min[2]), new Vector3D(max[0], max[1], max[2]));
        AssertSolidVolume(mesh, volume);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates the accepted forms of <c>translate</c>.
    /// </summary>
    /// <param name="call">The translate call.</param>
    /// <param name="dx">The expected X offset.</param>
    /// <param name="dy">The expected Y offset.</param>
    /// <param name="dz">The expected Z offset.</param>
    [Theory]
    [InlineData("translate([10, 20, 30])", 10.0, 20.0, 30.0)]
    [InlineData("translate(v = [1, 2, 3])", 1.0, 2.0, 3.0)]
    [InlineData("translate([5, 6])", 5.0, 6.0, 0.0)]
    [InlineData("translate([7])", 7.0, 0.0, 0.0)]
    [InlineData("translate([-1, -2, -3])", -1.0, -2.0, -3.0)]
    [InlineData("translate([0, 0, 0])", 0.0, 0.0, 0.0)]
    [InlineData("translate()", 0.0, 0.0, 0.0)]
    public void ScadTransforms_Translate_Forms_MoveTheSolid(string call, double dx, double dy, double dz)
    {
        // Arrange / Act / Assert
        AssertBox($"{call} {Box}", [dx, dy, dz], [dx + 1, dy + 2, dz + 3]);
    }

    /// <summary>
    ///     Validates that an identity transform adds no wrapper node (the scene is the mesh itself).
    /// </summary>
    [Fact]
    public void ScadTransforms_Translate_Identity_AddsNoWrapper()
    {
        // Arrange / Act
        var result = Build($"translate([0, 0, 0]) {Box}");

        // Assert
        Assert.IsType<MeshNode>(result.Scene.Root);
    }

    /// <summary>
    ///     Validates that an invalid translate argument is a warning at the instance and removes the instance.
    /// </summary>
    /// <param name="call">The translate call.</param>
    [Theory]
    [InlineData("translate(\"x\")")]
    [InlineData("translate([1, \"a\", 0])")]
    [InlineData("translate([1, 2, 3, 4])")]
    [InlineData("translate([])")]
    [InlineData("translate(5)")]
    [InlineData("translate([1 / 0, 0, 0])")]
    public void ScadTransforms_Translate_InvalidVector_WarnsAndRemovesInstance(string call)
    {
        // Arrange / Act
        var result = Build($"\n  {call} {Box}");

        // Assert
        Assert.Empty(result.Meshes);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), warning.Location);
        Assert.Contains("translate: v must be a vector", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a transform with no children builds nothing and reports nothing.
    /// </summary>
    [Fact]
    public void ScadTransforms_Translate_NoChildren_IsSilentlyEmpty()
    {
        // Arrange / Act
        var result = Build("translate([1, 2, 3]) { }");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
    }

    /// <summary>
    ///     Validates that a transform applies to the union of all its children.
    /// </summary>
    [Fact]
    public void ScadTransforms_Translate_SeveralChildren_MovesThemTogether()
    {
        // Arrange / Act
        var result = Build("translate([100, 0, 0]) { cube(1); translate([5, 0, 0]) cube(1); }");

        // Assert
        var bounds = result.Scene.ComputeBounds();
        AssertBounds(bounds, new Vector3D(100, 0, 0), new Vector3D(106, 1, 1));
        Assert.Equal(2, result.Meshes.Count);
    }

    /// <summary>
    ///     Validates the Euler (vector) form of <c>rotate</c>: X, then Y, then Z, with analytic bounds that
    ///     identify the order.
    /// </summary>
    /// <param name="call">The rotate call.</param>
    /// <param name="min">The expected minimum corner.</param>
    /// <param name="max">The expected maximum corner.</param>
    [Theory]
    [InlineData("rotate([90, 0, 0])", new[] { 0.0, -3, 0 }, new[] { 1.0, 0, 2 })]
    [InlineData("rotate([0, 90, 0])", new[] { 0.0, 0, -1 }, new[] { 3.0, 2, 0 })]
    [InlineData("rotate([0, 0, 90])", new[] { -2.0, 0, 0 }, new[] { 0.0, 1, 3 })]
    [InlineData("rotate(a = [0, 0, 90])", new[] { -2.0, 0, 0 }, new[] { 0.0, 1, 3 })]
    [InlineData("rotate([90, 0, 90])", new[] { 0.0, 0, 0 }, new[] { 3.0, 1, 2 })]
    [InlineData("rotate([90, 90, 0])", new[] { 0.0, -3, -1 }, new[] { 2.0, 0, 0 })]
    [InlineData("rotate([0, 90, 90])", new[] { -2.0, 0, -1 }, new[] { 0.0, 3, 0 })]
    [InlineData("rotate([180, 0, 0])", new[] { 0.0, -2, -3 }, new[] { 1.0, 0, 0 })]
    [InlineData("rotate([0, 0, 0])", new[] { 0.0, 0, 0 }, new[] { 1.0, 2, 3 })]
    [InlineData("rotate([90])", new[] { 0.0, -3, 0 }, new[] { 1.0, 0, 2 })]
    [InlineData("rotate([90, 0])", new[] { 0.0, -3, 0 }, new[] { 1.0, 0, 2 })]
    public void ScadTransforms_Rotate_EulerVector_AppliesXThenYThenZ(string call, double[] min, double[] max)
    {
        // Arrange / Act / Assert
        AssertBox($"{call} {Box}", min, max);
    }

    /// <summary>
    ///     Validates that a scalar angle rotates about Z.
    /// </summary>
    [Fact]
    public void ScadTransforms_Rotate_Scalar_RotatesAboutZ()
    {
        // Arrange / Act / Assert
        AssertBox($"rotate(90) {Box}", [-2, 0, 0], [0, 1, 3]);
        AssertBox($"rotate(a = -90) {Box}", [0, -1, 0], [2, 0, 3]);
    }

    /// <summary>
    ///     Validates the axis-angle form of <c>rotate</c>.
    /// </summary>
    /// <param name="call">The rotate call.</param>
    /// <param name="min">The expected minimum corner.</param>
    /// <param name="max">The expected maximum corner.</param>
    [Theory]
    [InlineData("rotate(a = 90, v = [0, 0, 1])", new[] { -2.0, 0, 0 }, new[] { 0.0, 1, 3 })]
    [InlineData("rotate(90, [1, 0, 0])", new[] { 0.0, -3, 0 }, new[] { 1.0, 0, 2 })]
    [InlineData("rotate(90, [0, 1, 0])", new[] { 0.0, 0, -1 }, new[] { 3.0, 2, 0 })]
    [InlineData("rotate(180, [1, 1, 0])", new[] { 0.0, 0, -3 }, new[] { 2.0, 1, 0 })]
    [InlineData("rotate(90, [0, 0, 5])", new[] { -2.0, 0, 0 }, new[] { 0.0, 1, 3 })]
    [InlineData("rotate(a = 0, v = [1, 2, 3])", new[] { 0.0, 0, 0 }, new[] { 1.0, 2, 3 })]
    [InlineData("rotate(a = 360, v = [0, 1, 0])", new[] { 0.0, 0, 0 }, new[] { 1.0, 2, 3 })]
    public void ScadTransforms_Rotate_AxisAngle_RotatesAboutTheAxis(string call, double[] min, double[] max)
    {
        // Arrange / Act / Assert
        AssertBox($"{call} {Box}", min, max);
    }

    /// <summary>
    ///     Validates that a zero axis warns and leaves the children unrotated.
    /// </summary>
    [Fact]
    public void ScadTransforms_Rotate_ZeroAxis_WarnsAndDoesNotRotate()
    {
        // Arrange / Act
        var result = Build($"\nrotate(90, [0, 0, 0]) {Box}");

        // Assert
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(1, 2, 3));
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(new ScadSourceLocation(null, 2, 1), warning.Location);
        Assert.Contains("axis v is zero", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an axis given together with a vector angle is ignored with a warning.
    /// </summary>
    [Fact]
    public void ScadTransforms_Rotate_VectorAngleWithAxis_WarnsAndUsesEuler()
    {
        // Arrange / Act
        var result = Build($"rotate(a = [0, 0, 90], v = [1, 0, 0]) {Box}");

        // Assert
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(-2, 0, 0), new Vector3D(0, 1, 3));
        Assert.Contains(result.Warnings, w => w.Message.Contains("v is ignored", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates the invalid rotate and scale arguments: each warns and removes the instance.
    /// </summary>
    /// <param name="call">The transform call.</param>
    [Theory]
    [InlineData("rotate(\"x\")")]
    [InlineData("rotate([1, 2, 3, 4])")]
    [InlineData("rotate([1, \"a\"])")]
    [InlineData("rotate(a = 90, v = [1, 0])")]
    [InlineData("rotate(a = 90, v = \"z\")")]
    [InlineData("rotate([])")]
    [InlineData("scale(\"x\")")]
    [InlineData("scale([1, 2, 3, 4])")]
    [InlineData("scale([1, \"a\"])")]
    [InlineData("scale(1 / 0)")]
    public void ScadTransforms_RotateAndScale_InvalidArguments_WarnAndRemoveInstance(string call)
    {
        // Arrange / Act
        var result = Build($"{call} {Box}");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Single(result.Warnings);
    }

    /// <summary>
    ///     Validates that <c>rotate()</c> without an angle changes nothing.
    /// </summary>
    [Fact]
    public void ScadTransforms_Rotate_NoAngle_IsIdentity()
    {
        // Arrange / Act / Assert
        AssertBox($"rotate() {Box}", [0, 0, 0], [1, 2, 3]);
    }

    /// <summary>
    ///     Validates <c>scale</c> with a scalar, a vector and a short vector.
    /// </summary>
    /// <param name="call">The scale call.</param>
    /// <param name="max">The expected maximum corner.</param>
    /// <param name="volume">The expected volume.</param>
    [Theory]
    [InlineData("scale(2)", new[] { 2.0, 4, 6 }, 48.0)]
    [InlineData("scale(v = 0.5)", new[] { 0.5, 1, 1.5 }, 0.75)]
    [InlineData("scale([2, 3, 4])", new[] { 2.0, 6, 12 }, 144.0)]
    [InlineData("scale([2, 3])", new[] { 2.0, 6, 3 }, 36.0)]
    [InlineData("scale([2])", new[] { 2.0, 2, 3 }, 12.0)]
    [InlineData("scale()", new[] { 1.0, 2, 3 }, 6.0)]
    public void ScadTransforms_Scale_Forms_ScaleAboutTheOrigin(string call, double[] max, double volume)
    {
        // Arrange / Act / Assert
        AssertBox($"{call} {Box}", [0, 0, 0], max, volume);
    }

    /// <summary>
    ///     Validates that a negative scale mirrors: the world mesh is still closed and outward oriented, while the
    ///     instance transform has a negative determinant (the renderer flips the winding of such instances).
    /// </summary>
    [Fact]
    public void ScadTransforms_Scale_Negative_MirrorsAndKeepsOrientation()
    {
        // Arrange / Act
        var result = Build($"scale([-1, 1, 1]) {Box}");

        // Assert
        var leaf = Assert.Single(result.Leaves);
        Assert.True(leaf.World.Determinant < 0.0);
        AssertBounds(leaf.WorldMesh.Bounds, new Vector3D(-1, 0, 0), new Vector3D(0, 2, 3));
        AssertSolidVolume(leaf.WorldMesh, 6.0);
    }

    /// <summary>
    ///     Validates that a scale that collapses space (a zero factor) warns and removes the instance.
    /// </summary>
    /// <param name="call">The scale call.</param>
    [Theory]
    [InlineData("scale(0)")]
    [InlineData("scale([1, 0, 1])")]
    [InlineData("scale([0, 1])")]
    public void ScadTransforms_Scale_Singular_WarnsAndRemovesInstance(string call)
    {
        // Arrange / Act
        var result = Build($"\n {call} {Box}");

        // Assert
        Assert.Empty(result.Meshes);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(new ScadSourceLocation(null, 2, 2), warning.Location);
        Assert.Contains("singular", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates <c>mirror</c> through coordinate and oblique planes.
    /// </summary>
    /// <param name="call">The mirror call.</param>
    /// <param name="min">The expected minimum corner.</param>
    /// <param name="max">The expected maximum corner.</param>
    [Theory]
    [InlineData("mirror([1, 0, 0])", new[] { -1.0, 0, 0 }, new[] { 0.0, 2, 3 })]
    [InlineData("mirror(v = [0, 1, 0])", new[] { 0.0, -2, 0 }, new[] { 1.0, 0, 3 })]
    [InlineData("mirror([0, 0, 1])", new[] { 0.0, 0, -3 }, new[] { 1.0, 2, 0 })]
    [InlineData("mirror([0, 0, 7])", new[] { 0.0, 0, -3 }, new[] { 1.0, 2, 0 })]
    [InlineData("mirror([1, 1, 0])", new[] { -2.0, -1, 0 }, new[] { 0.0, 0, 3 })]
    [InlineData("mirror([1])", new[] { -1.0, 0, 0 }, new[] { 0.0, 2, 3 })]
    public void ScadTransforms_Mirror_Planes_ReflectAndKeepOrientation(string call, double[] min, double[] max)
    {
        // Arrange / Act
        var result = Build($"{call} {Box}");

        // Assert
        var leaf = Assert.Single(result.Leaves);
        Assert.True(leaf.World.Determinant < 0.0);
        AssertBounds(leaf.WorldMesh.Bounds, new Vector3D(min[0], min[1], min[2]), new Vector3D(max[0], max[1], max[2]));
        AssertSolidVolume(leaf.WorldMesh, 6.0);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a zero mirror normal warns and leaves the children as they are.
    /// </summary>
    [Fact]
    public void ScadTransforms_Mirror_ZeroNormal_WarnsAndDoesNotMirror()
    {
        // Arrange / Act
        var result = Build($"mirror([0, 0, 0]) {Box}");

        // Assert
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(1, 2, 3));
        Assert.Contains(result.Warnings, w => w.Message.Contains("normal v is zero", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that an invalid mirror argument warns and removes the instance, while <c>mirror()</c> alone is
    ///     the identity.
    /// </summary>
    [Fact]
    public void ScadTransforms_Mirror_InvalidAndAbsent_AreHandled()
    {
        // Arrange / Act
        var invalid = Build($"mirror(\"x\") {Box}");
        var absent = Build($"mirror() {Box}");

        // Assert
        Assert.Empty(invalid.Meshes);
        Assert.Single(invalid.Warnings);
        AssertBounds(absent.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(1, 2, 3));
        Assert.Empty(absent.Warnings);
    }

    /// <summary>
    ///     Validates that <c>multmatrix</c> takes a column-vector matrix: a translation in the fourth column, an
    ///     asymmetric solid exposing any transposition mistake.
    /// </summary>
    [Fact]
    public void ScadTransforms_Multmatrix_TranslationColumn_IsTransposedCorrectly()
    {
        // Arrange / Act / Assert
        AssertBox($"multmatrix([[1, 0, 0, 5], [0, 1, 0, 6], [0, 0, 1, 7], [0, 0, 0, 1]]) {Box}", [5, 6, 7], [6, 8, 10]);
    }

    /// <summary>
    ///     Validates that a rotation matrix in column-vector convention rotates like the equivalent <c>rotate</c>.
    /// </summary>
    [Fact]
    public void ScadTransforms_Multmatrix_RotationMatrix_EqualsRotate()
    {
        // Arrange / Act
        var viaMatrix = Build($"multmatrix([[0, -1, 0, 0], [1, 0, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]) {Box}");
        var viaRotate = Build($"rotate(90) {Box}");

        // Assert
        AssertBounds(viaMatrix.WorldMesh.Bounds, viaRotate.WorldMesh.Bounds.Min, viaRotate.WorldMesh.Bounds.Max);
        AssertBounds(viaMatrix.WorldMesh.Bounds, new Vector3D(-2, 0, 0), new Vector3D(0, 1, 3));
    }

    /// <summary>
    ///     Validates the three-row form of <c>multmatrix</c> (the implicit bottom row is <c>[0, 0, 0, 1]</c>).
    /// </summary>
    [Fact]
    public void ScadTransforms_Multmatrix_ThreeRows_IsAffine()
    {
        // Arrange / Act / Assert
        AssertBox($"multmatrix([[2, 0, 0, 1], [0, 3, 0, 0], [0, 0, 4, 0]]) {Box}", [1, 0, 0], [3, 6, 12], 144.0);
    }

    /// <summary>
    ///     Validates a shear: each axis mixes into another, so only the full matrix explains the bounds.
    /// </summary>
    [Fact]
    public void ScadTransforms_Multmatrix_Shear_ShearsTheSolid()
    {
        // Arrange / Act
        var result = Build($"multmatrix([[1, 1, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]) {Box}");

        // Assert: x' = x + y, so x spans 0..3 while the volume is unchanged (determinant 1)
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(3, 2, 3));
        AssertSolidVolume(result.WorldMesh, 6.0);
    }

    /// <summary>
    ///     Validates that the identity matrix adds no wrapper.
    /// </summary>
    [Fact]
    public void ScadTransforms_Multmatrix_Identity_AddsNoWrapper()
    {
        // Arrange / Act
        var result = Build($"multmatrix([[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]) {Box}");

        // Assert
        Assert.IsType<MeshNode>(result.Scene.Root);
    }

    /// <summary>
    ///     Validates that a projective bottom row is reported as the unsupported feature with its location and that
    ///     the children are omitted.
    /// </summary>
    /// <param name="bottom">The bottom row.</param>
    [Theory]
    [InlineData("[0, 0, 1, 1]")]
    [InlineData("[0, 0, 0, 2]")]
    [InlineData("[1, 0, 0, 1]")]
    [InlineData("[0, 0, 0, 0]")]
    public void ScadTransforms_Multmatrix_ProjectiveRow_IsReportedAndOmitted(string bottom)
    {
        // Arrange / Act
        var result = Build($"\n  multmatrix([[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], {bottom}]) {Box}");

        // Assert
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal("scad-multmatrix-projective", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a singular matrix warns and removes the instance, also when it is only nearly singular.
    /// </summary>
    /// <param name="matrix">The matrix.</param>
    [Theory]
    [InlineData("[[0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 1]]")]
    [InlineData("[[1, 0, 0, 0], [1, 0, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]")]
    [InlineData("[[1, 2, 3, 0], [2, 4, 6, 0], [0, 0, 1, 0], [0, 0, 0, 1]]")]
    public void ScadTransforms_Multmatrix_Singular_WarnsAndRemovesInstance(string matrix)
    {
        // Arrange / Act
        var result = Build($"multmatrix({matrix}) {Box}");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Contains(result.Warnings, w => w.Message.Contains("singular", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a malformed matrix warns and removes the instance.
    /// </summary>
    /// <param name="matrix">The matrix.</param>
    [Theory]
    [InlineData("5")]
    [InlineData("[[1, 0, 0], [0, 1, 0], [0, 0, 1]]")]
    [InlineData("[[1, 0, 0, 0], [0, 1, 0, 0]]")]
    [InlineData("[[1, 0, 0, \"a\"], [0, 1, 0, 0], [0, 0, 1, 0]]")]
    [InlineData("[1, 2, 3, 4]")]
    public void ScadTransforms_Multmatrix_Malformed_WarnsAndRemovesInstance(string matrix)
    {
        // Arrange / Act
        var result = Build($"multmatrix({matrix}) {Box}");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Single(result.Warnings);
    }

    /// <summary>
    ///     Validates that nested transforms compose child first: the innermost applies first.
    /// </summary>
    [Fact]
    public void ScadTransforms_Nesting_AppliesInnermostFirst()
    {
        // Arrange / Act
        var rotateThenMove = Build($"translate([10, 0, 0]) rotate(90) {Box}");
        var moveThenRotate = Build($"rotate(90) translate([10, 0, 0]) {Box}");

        // Assert: rotate(90) maps x to y and y to -x
        AssertBounds(rotateThenMove.WorldMesh.Bounds, new Vector3D(8, 0, 0), new Vector3D(10, 1, 3));
        AssertBounds(moveThenRotate.WorldMesh.Bounds, new Vector3D(-2, 10, 0), new Vector3D(0, 11, 3));
    }

    /// <summary>
    ///     Validates the scale-then-translate and translate-then-scale difference (scale is about the origin).
    /// </summary>
    [Fact]
    public void ScadTransforms_Nesting_ScaleIsAboutTheOrigin()
    {
        // Arrange / Act
        var scaleOfMoved = Build("scale(2) translate([1, 0, 0]) cube(1);");
        var movedOfScaled = Build("translate([1, 0, 0]) scale(2) cube(1);");

        // Assert
        AssertBounds(scaleOfMoved.WorldMesh.Bounds, new Vector3D(2, 0, 0), new Vector3D(4, 2, 2));
        AssertBounds(movedOfScaled.WorldMesh.Bounds, new Vector3D(1, 0, 0), new Vector3D(3, 2, 2));
    }

    /// <summary>
    ///     Validates that a user module call and a loop are transparent to a transform above them.
    /// </summary>
    [Fact]
    public void ScadTransforms_Transform_AppliesThroughModulesAndLoops()
    {
        // Arrange / Act
        var result = Build("module m() cube(1);\ntranslate([0, 0, 5]) for (i = [0:2]) translate([i * 2, 0, 0]) m();");

        // Assert
        AssertBounds(result.Scene.ComputeBounds(), new Vector3D(0, 0, 5), new Vector3D(5, 1, 6));
        Assert.Equal(3, result.Meshes.Count);
    }

    /// <summary>
    ///     Validates unknown and surplus arguments of a transform: each warns, the transform still applies.
    /// </summary>
    [Fact]
    public void ScadTransforms_Arguments_UnknownAndSurplus_Warn()
    {
        // Arrange / Act
        var result = Build("translate([1, 0, 0], 5) cube(1);\ntranslate(v = [1, 0, 0], q = 3) cube(1);");

        // Assert
        Assert.Contains(result.Warnings, w => w.Message.Contains("extra argument", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Message.Contains("unknown argument 'q'", StringComparison.Ordinal));
        Assert.Equal(2, result.Meshes.Count);
    }

    /// <summary>
    ///     Validates that <c>$fn</c> given to a transform is not reported as an unknown argument.
    /// </summary>
    [Fact]
    public void ScadTransforms_Arguments_SpecialVariable_IsIgnoredSilently()
    {
        // Arrange / Act
        var result = Build("translate([1, 0, 0], $fn = 8) cube(1);");

        // Assert
        Assert.Empty(result.Warnings);
        Assert.Single(result.Meshes);
    }

    /// <summary>
    ///     Validates that a transform whose determinant overflows (huge scales) warns and removes
    ///     the instance instead of throwing.
    /// </summary>
    [Fact]
    public void ScadTransforms_NonFiniteMatrix_WarnsAndRemovesInstance()
    {
        // Arrange / Act
        var result = Build("multmatrix([[1e308, 0, 0, 0], [0, 1e308, 0, 0], [0, 0, 1e308, 0], [0, 0, 0, 1]]) cube(1);");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.NotEmpty(result.Warnings);
    }

    /// <summary>
    ///     Validates the 2D transforms: a 2D shape moved, rotated, scaled and mirrored becomes the expected slab.
    /// </summary>
    /// <param name="call">The transform call.</param>
    /// <param name="min">The expected minimum corner of the slab.</param>
    /// <param name="max">The expected maximum corner of the slab.</param>
    [Theory]
    [InlineData("translate([5, 6]) ", new[] { 5.0, 6 }, new[] { 6.0, 8 })]
    [InlineData("rotate(90) ", new[] { -2.0, 0 }, new[] { 0.0, 1 })]
    [InlineData("rotate([0, 0, 180]) ", new[] { -1.0, -2 }, new[] { 0.0, 0 })]
    [InlineData("scale([2, 3]) ", new[] { 0.0, 0 }, new[] { 2.0, 6 })]
    [InlineData("scale([1, 1, 0]) ", new[] { 0.0, 0 }, new[] { 1.0, 2 })]
    [InlineData("mirror([1, 0]) ", new[] { -1.0, 0 }, new[] { 0.0, 2 })]
    [InlineData("mirror([0, 1, 0]) ", new[] { 0.0, -2 }, new[] { 1.0, 0 })]
    [InlineData("multmatrix([[1, 0, 0, 3], [0, 1, 0, 4], [0, 0, 1, 0]]) ", new[] { 3.0, 4 }, new[] { 4.0, 6 })]
    public void ScadTransforms_Flat_TransformsBecomeTheExpectedSlab(string call, double[] min, double[] max)
    {
        // Arrange / Act
        var result = Build($"{call}square([1, 2]);");

        // Assert
        var mesh = result.WorldMesh;
        AssertBounds(mesh.Bounds, new Vector3D(min[0], min[1], 0), new Vector3D(max[0], max[1], 0.1));
        AssertSolidVolume(mesh, (max[0] - min[0]) * (max[1] - min[1]) * 0.1);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a Z translation of a 2D shape is ignored with a warning, and that a transform that
    ///     collapses the plane (a rotation about X by 90 degrees) removes the shape.
    /// </summary>
    [Fact]
    public void ScadTransforms_Flat_OutOfPlaneParts_AreIgnoredWithWarning()
    {
        // Arrange / Act
        var moved = Build("\ntranslate([1, 0, 5]) square(1);");
        var tilted = Build("rotate([90, 0, 0]) square(1);");

        // Assert
        AssertBounds(moved.WorldMesh.Bounds, new Vector3D(1, 0, 0), new Vector3D(2, 1, 0.1));
        var warning = Assert.Single(moved.Warnings);
        Assert.Equal(new ScadSourceLocation(null, 2, 1), warning.Location);
        Assert.Contains("Z part", warning.Message, StringComparison.Ordinal);
        Assert.Empty(tilted.Meshes);
        Assert.Contains(tilted.Warnings, w => w.Message.Contains("singular", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a 2D shape with a hole is transformed as a whole (the hole follows the outline).
    /// </summary>
    [Fact]
    public void ScadTransforms_Flat_ShapeWithHole_KeepsItsHole()
    {
        // Arrange / Act
        var result = Build("translate([10, 0]) polygon([[0, 0], [4, 0], [4, 4], [0, 4], [1, 1], [3, 1], [3, 3], [1, 3]], [[0, 1, 2, 3], [4, 5, 6, 7]]);");

        // Assert: area 16 - 4 = 12 and a thickness of 0.1
        var mesh = result.WorldMesh;
        AssertSolidVolume(mesh, 12.0 * 0.1);
        AssertBounds(mesh.Bounds, new Vector3D(10, 0, 0), new Vector3D(14, 4, 0.1));
    }
}

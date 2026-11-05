// cspell:ignore multmatrix minkowski
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Geometry;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests of the anchor pass of <see cref="ScadGeometryBuilder"/>: the world point of an anchor under the enclosing
///     transforms, the containers it may sit in, the contexts that cannot be located, and the guarantee that an anchor
///     never changes the geometry.
/// </summary>
public class ScadGeometryBuilderAnchorTests
{
    /// <summary>The tolerance of a computed coordinate.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>
    ///     Asserts one anchor of a build.
    /// </summary>
    /// <param name="anchor">The anchor.</param>
    /// <param name="name">The expected name.</param>
    /// <param name="x">The expected world X.</param>
    /// <param name="y">The expected world Y.</param>
    /// <param name="z">The expected world Z.</param>
    private static void AssertAnchor(ScadWorldAnchor anchor, string name, double x, double y, double z)
    {
        Assert.Equal(name, anchor.Name);
        Assert.Equal(x, anchor.Point.X, Tolerance);
        Assert.Equal(y, anchor.Point.Y, Tolerance);
        Assert.Equal(z, anchor.Point.Z, Tolerance);
    }

    /// <summary>
    ///     Validates the world point under each of the five matrix transforms.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="x">The expected world X.</param>
    /// <param name="y">The expected world Y.</param>
    /// <param name="z">The expected world Z.</param>
    [Theory]
    [InlineData("echo(\"anchor\", \"a\", [1, 2, 3]);", 1, 2, 3)]
    [InlineData("translate([1, 2, 3]) { echo(\"anchor\", \"a\", [1, 1, 1]); }", 2, 3, 4)]
    [InlineData("translate([1, 2, 3]) echo(\"anchor\", \"a\", [1, 1, 1]);", 2, 3, 4)]
    [InlineData("rotate([0, 0, 90]) { echo(\"anchor\", \"a\", [1, 0, 0]); }", 0, 1, 0)]
    [InlineData("rotate(90) { echo(\"anchor\", \"a\", [1, 0, 0]); }", 0, 1, 0)]
    [InlineData("rotate(a = 90, v = [1, 0, 0]) { echo(\"anchor\", \"a\", [0, 1, 0]); }", 0, 0, 1)]
    [InlineData("rotate([90, 0, 0]) { echo(\"anchor\", \"a\", [0, 1, 0]); }", 0, 0, 1)]
    [InlineData("scale([2, 3, 4]) { echo(\"anchor\", \"a\", [1, 1, 1]); }", 2, 3, 4)]
    [InlineData("scale(2) { echo(\"anchor\", \"a\", [1, 2, 3]); }", 2, 4, 6)]
    [InlineData("mirror([1, 0, 0]) { echo(\"anchor\", \"a\", [1, 2, 3]); }", -1, 2, 3)]
    [InlineData("multmatrix([[1, 0, 0, 10], [0, 1, 0, 20], [0, 0, 1, 30], [0, 0, 0, 1]]) { echo(\"anchor\", \"a\", [1, 1, 1]); }", 11, 21, 31)]
    [InlineData("multmatrix([[1, 0, 0, 10], [0, 1, 0, 20], [0, 0, 1, 30]]) { echo(\"anchor\", \"a\", [1, 1, 1]); }", 11, 21, 31)]
    [InlineData("multmatrix([[0, -1, 0, 0], [1, 0, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]) { echo(\"anchor\", \"a\", [1, 0, 0]); }", 0, 1, 0)]
    public void ScadGeometryBuilderAnchor_MatrixTransform_AppliesToThePoint(string source, double x, double y, double z)
    {
        // Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Warnings);
        AssertAnchor(Assert.Single(result.Anchors), "a", x, y, z);
    }

    /// <summary>
    ///     Validates that nested transforms apply innermost first: scale, then rotate, then translate.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderAnchor_NestedTransforms_ApplyInnermostFirst()
    {
        // Arrange
        const string Source = "translate([10, 0, 0]) rotate([0, 0, 90]) scale(2) { echo(\"anchor\", \"a\", [1, 0, 0]); }";

        // Act
        var result = Build(Source);

        // Assert: (1, 0, 0) scaled to (2, 0, 0), turned to (0, 2, 0), moved to (10, 2, 0)
        AssertAnchor(Assert.Single(result.Anchors), "a", 10, 2, 0);
    }

    /// <summary>
    ///     Validates that the transforms around a user module call and inside its body both apply.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderAnchor_ThroughUserModule_AppliesCallerAndBodyTransforms()
    {
        // Arrange
        const string Source = "module m(h) { translate([0, 0, h]) { cube(1); echo(\"anchor\", \"top\", [0, 0, 1]); } }\ntranslate([5, 0, 0]) m(10);";

        // Act
        var result = Build(Source);

        // Assert
        AssertAnchor(Assert.Single(result.Anchors), "top", 5, 0, 11);
    }

    /// <summary>
    ///     Validates that the containers that combine or color their children pass the matrix through unchanged.
    /// </summary>
    /// <param name="container">The container module.</param>
    [Theory]
    [InlineData("union()")]
    [InlineData("difference()")]
    [InlineData("intersection()")]
    [InlineData("hull()")]
    [InlineData("minkowski()")]
    [InlineData("color(\"red\")")]
    [InlineData("render()")]
    [InlineData("for (i = [0 : 1])")]
    [InlineData("intersection_for (i = [0 : 1])")]
    public void ScadGeometryBuilderAnchor_PassThroughContainer_KeepsTheEnclosingMatrix(string container)
    {
        // Arrange
        var source = $"translate([1, 2, 3]) {container} {{ cube(2); cube(3); echo(\"anchor\", \"a\", [0, 0, 1]); }}";

        // Act
        var result = Build(source);

        // Assert: one anchor per iteration for the loops, all at the same place
        Assert.Empty(result.Warnings);
        Assert.NotEmpty(result.Anchors);
        Assert.All(result.Anchors, a => AssertAnchor(a, "a", 1, 2, 4));
    }

    /// <summary>
    ///     Validates that the <c>#</c> and <c>%</c> modifiers do not change where an anchor is.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderAnchor_UnderHighlightAndBackground_KeepsTheMatrix()
    {
        // Arrange
        const string Source = "translate([1, 0, 0]) { #{ echo(\"anchor\", \"h\", [0, 1, 0]); } %{ echo(\"anchor\", \"g\", [0, 2, 0]); } }";

        // Act
        var result = Build(Source);

        // Assert
        Assert.Equal(2, result.Anchors.Count);
        AssertAnchor(result.Anchors[0], "h", 1, 1, 0);
        AssertAnchor(result.Anchors[1], "g", 1, 2, 0);
    }

    /// <summary>
    ///     Validates that an anchor never changes the geometry, even as the first child of a difference.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderAnchor_FirstChildOfDifference_DoesNotChangeTheScene()
    {
        // Arrange
        const string Plain = "difference() { cube(10); cube(4); }";
        const string WithAnchor = "difference() { echo(\"anchor\", \"a\", [0, 0, 0]); cube(10); cube(4); echo(\"anchor\", \"b\", [1, 1, 1]); }";

        // Act
        var plain = Build(Plain);
        var anchored = Build(WithAnchor);

        // Assert: the same solid, the same bounds, no reports
        Assert.Empty(anchored.Warnings);
        Assert.Equal(2, anchored.Anchors.Count);
        Assert.Equal(plain.MeshTriangles, anchored.MeshTriangles);
        Assert.Equal(plain.Scene.ComputeBounds(), anchored.Scene.ComputeBounds());
    }

    /// <summary>
    ///     Validates that an anchor named like a module is still only an anchor.
    /// </summary>
    [Theory]
    [InlineData("cube")]
    [InlineData("translate")]
    [InlineData("difference")]
    [InlineData("unknown_thing")]
    public void ScadGeometryBuilderAnchor_NamedLikeAModule_IsNotGeometry(string name)
    {
        // Act
        var result = Build($"echo(\"anchor\", \"{name}\", [0, 0, 0]);");

        // Assert
        Assert.Empty(result.Meshes);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.Unsupported);
        Assert.Equal(name, Assert.Single(result.Anchors).Name);
    }

    /// <summary>
    ///     Validates that a model with <c>!</c> reports the anchors of the marked subtree only, without the transforms
    ///     of its ancestors.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderAnchor_ShowOnlyModifier_UsesTheMarkedSubtreeWithoutAncestorTransforms()
    {
        // Arrange
        const string Source = "echo(\"anchor\", \"outside\", [0, 0, 0]);\ntranslate([100, 0, 0]) {\n  cube(1);\n  echo(\"anchor\", \"ancestor\", [0, 0, 0]);\n  ! translate([1, 0, 0]) { cube(1); echo(\"anchor\", \"marked\", [0, 0, 5]); }\n}";

        // Act
        var result = Build(Source);

        // Assert: only the marked subtree is the model, with only its own transform
        AssertAnchor(Assert.Single(result.Anchors), "marked", 1, 0, 5);
    }

    /// <summary>
    ///     Validates that an anchor inside a module that changes the coordinates in a way the pass cannot follow is
    ///     skipped with a warning that names the module, while the geometry is unaffected.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="module">The module the warning names.</param>
    [Theory]
    [InlineData("resize([10, 10, 10]) { cube(1); echo(\"anchor\", \"a\", [0, 0, 0]); }", "resize")]
    [InlineData("linear_extrude(5) { square(1); echo(\"anchor\", \"a\", [0, 0, 0]); }", "linear_extrude")]
    [InlineData("rotate_extrude() { translate([2, 0]) square(1); echo(\"anchor\", \"a\", [0, 0, 0]); }", "rotate_extrude")]
    [InlineData("projection() { cube(1); echo(\"anchor\", \"a\", [0, 0, 0]); }", "projection")]
    [InlineData("offset(r = 1) { square(1); echo(\"anchor\", \"a\", [0, 0, 0]); }", "offset")]
    [InlineData("cube(1) { echo(\"anchor\", \"a\", [0, 0, 0]); }", "cube")]
    public void ScadGeometryBuilderAnchor_UnsupportedContext_SkipsWithWarning(string source, string module)
    {
        // Act
        var result = Build(source);

        // Assert: no anchor, and the warning about the anchor names the module (other warnings may also exist)
        Assert.Empty(result.Anchors);
        var warning = Assert.Single(result.Warnings, w => w.Message.StartsWith("scad-anchor-invalid:", StringComparison.Ordinal));
        Assert.Contains($"inside {module},", warning.Message, StringComparison.Ordinal);
        Assert.Contains("'a'", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an anchor below a transform that is invalid or collapses space is dropped silently, with no
    ///     duplicate of the warning the geometry builder gives for the transform itself.
    /// </summary>
    /// <param name="open">The transform that is opened around the children.</param>
    [Theory]
    [InlineData("scale([0, 1, 1])")]
    [InlineData("translate(\"x\")")]
    [InlineData("multmatrix([[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 1, 1]])")]
    public void ScadGeometryBuilderAnchor_UnderCollapsingTransform_IsDroppedWithoutExtraWarnings(string open)
    {
        // Arrange: the same model without the anchor gives the reference reports
        var plain = Build($"{open} {{ cube(1); }}");
        var anchored = Build($"{open} {{ cube(1); echo(\"anchor\", \"a\", [0, 0, 0]); }}");

        // Assert
        Assert.Empty(anchored.Anchors);
        Assert.NotEmpty(plain.WarningTexts.Concat(plain.Features));
        Assert.Equal(plain.WarningTexts, anchored.WarningTexts);
        Assert.Equal(plain.Features, anchored.Features);
    }

    /// <summary>
    ///     Validates that a point that leaves the safe coordinate range is skipped with the anchor warning.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderAnchor_OutOfRangeAfterTransforms_IsSkippedWithWarning()
    {
        // Arrange
        const string Source = "scale(1e60) scale(1e60) { echo(\"anchor\", \"huge\", [1, 0, 0]); }";

        // Act
        var result = Build(Source);

        // Assert
        Assert.Empty(result.Anchors);
        var warning = Assert.Single(result.Warnings);
        Assert.StartsWith("scad-anchor-invalid:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("'huge'", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the anchors come in echo order, which is the order of the echo lines, across transforms,
    ///     modules, loops and duplicates.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderAnchor_Order_IsTheEchoOrder()
    {
        // Arrange
        const string Source = "echo(\"anchor\", \"a\", [0, 0, 0]);\nmodule m(n) { translate([1, 0, 0]) echo(\"anchor\", n, [0, 0, 0]); }\ntranslate([0, 5, 0]) { m(\"b\"); for (i = [1 : 2]) echo(\"anchor\", str(\"c\", i), [i, 0, 0]); m(\"d\"); }\necho(\"anchor\", \"a\", [9, 9, 9]);";
        var sink = new ScadEvaluationSink(ScadLimits.Default);

        // Act
        var result = Build(Source, sink: sink);

        // Assert: the names follow the echo lines, duplicates included
        var echoed = sink.EchoLines.Select(line => line.Split('"')[3]).ToList();
        Assert.Equal(["a", "b", "c1", "c2", "d", "a"], echoed);
        Assert.Equal(echoed, result.Anchors.Select(a => a.Name));
        AssertAnchor(result.Anchors[1], "b", 1, 5, 0);
        AssertAnchor(result.Anchors[3], "c2", 2, 5, 0);
        AssertAnchor(result.Anchors[5], "a", 9, 9, 9);
    }

    /// <summary>
    ///     Validates that a model without anchors reports none.
    /// </summary>
    [Fact]
    public void ScadGeometryBuilderAnchor_NoDeclaration_ReportsNone()
    {
        // Act
        var result = Build("cube(1);");

        // Assert
        Assert.Empty(result.Anchors);
    }
}

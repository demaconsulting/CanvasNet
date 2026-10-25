// cspell:ignore extrude
using DemaConsulting.CanvasNet.Render3D.SceneModel;
using static DemaConsulting.CanvasNet.Scad.Tests.Geometry.ScadGeometryTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Geometry;

/// <summary>
///     Tests that every way the Render3D extruders and the polygon validator can reject an extrusion is mapped to
///     a specific, located report (or the catch-all <c>scad-geometry-rejected</c>) and never crashes the build;
///     strict mode throws the same report and the public handler receives it.
/// </summary>
public class ScadExtrusionsMappingTests
{
    /// <summary>A profile with a hole (two contours).</summary>
    private const string Holed = "polygon([[0, 0], [10, 0], [10, 10], [0, 10], [3, 3], [7, 3], [7, 7], [3, 7]], [[0, 1, 2, 3], [4, 5, 6, 7]])";

    /// <summary>
    ///     Gets the contract rows: the program, the expected feature and the token at whose location it is reported.
    /// </summary>
    public static TheoryData<string, string, string> Rows => new()
    {
        { $"\n  linear_extrude(5, scale = 0) {Holed};", "scad-linear-extrude-apex-multiple-contours", "linear_extrude" },
        { "\n  linear_extrude(5, scale = -2) square(1);", "scad-linear-extrude-scale", "linear_extrude" },
        { "\n  linear_extrude(5, scale = [0, 2]) square(1);", "scad-linear-extrude-scale", "linear_extrude" },
        { "\n  rotate_extrude() translate([-1, 0]) square(2);", "scad-rotate-extrude-axis-crossing", "rotate_extrude" },
        { "\n  rotate_extrude() polygon([[0, 0], [2, -1], [2, 1]]);", "scad-rotate-extrude-axis-contact", "rotate_extrude" },
        { "\n  linear_extrude(1) scale([1e200, 1]) scale([1e200, 1]) square(1);", "scad-polygon-invalid", "square(1)" },
        { "\n  rotate_extrude() scale([1e200, 1]) scale([1e200, 1]) translate([1, 0]) square(1);", "scad-polygon-invalid", "square(1)" },
        { "\n  rotate_extrude(angle = 5e-324) translate([1, 0]) square(1);", "scad-geometry-rejected", "rotate_extrude" },
    };

    /// <summary>
    ///     Computes the location of the first occurrence of a token in a program.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="token">The token.</param>
    /// <returns>The one-based line and column.</returns>
    private static ScadSourceLocation LocationOf(string source, string token)
    {
        var index = source.IndexOf(token, StringComparison.Ordinal);
        var line = source[..index].Count(c => c == '\n') + 1;
        var column = index - source[..index].LastIndexOf('\n');
        return new ScadSourceLocation(null, line, column);
    }

    /// <summary>
    ///     Validates that each row produces exactly the mapped report at the right location, builds nothing for the
    ///     rejected extrusion and does not throw.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="feature">The expected feature.</param>
    /// <param name="token">The token at whose location the report is made.</param>
    [Theory]
    [MemberData(nameof(Rows))]
    public void ScadExtrusionsMapping_Row_IsReportedWithItsFeatureAndLocation(string source, string feature, string token)
    {
        // Arrange / Act
        var result = Build(source);

        // Assert
        Assert.Empty(result.Meshes);
        var report = Assert.Single(result.Unsupported);
        Assert.Equal(feature, report.Feature);
        Assert.Equal(LocationOf(source, token), report.Location);
        Assert.False(string.IsNullOrWhiteSpace(report.Message));
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that strict mode throws the same feature and location for each row.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="feature">The expected feature.</param>
    /// <param name="token">The token at whose location the report is made.</param>
    [Theory]
    [MemberData(nameof(Rows))]
    public void ScadExtrusionsMapping_Row_StrictModeThrowsTheSameReport(string source, string feature, string token)
    {
        // Arrange / Act
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(() => Build(source, Settings(strict: true)));

        // Assert
        Assert.Equal(feature, ex.Feature);
        Assert.Equal(LocationOf(source, token), ex.Location);
    }

    /// <summary>
    ///     Validates the public path: the unsupported-feature handler of <see cref="ScadRenderOptions"/> receives the
    ///     report with its location, and the strict option throws it from <c>BuildScene</c>.
    /// </summary>
    [Fact]
    public void ScadExtrusionsMapping_PublicApi_DeliversAndThrows()
    {
        // Arrange
        const string source = "cube(1);\n  rotate_extrude() translate([-1, 0]) square(2);";
        var reports = new List<ScadUnsupportedFeature>();
        var document = ScadDocument.Parse(source);

        // Act
        var scene = document.BuildScene(new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { UnsupportedFeatureHandler = reports.Add } }, TestContext.Current.CancellationToken);
        var ex = Assert.Throws<ScadUnsupportedFeatureException>(
            () => document.BuildScene(new ScadRenderOptions { Evaluation = new ScadEvaluationOptions { ThrowOnUnsupportedFeature = true } }, TestContext.Current.CancellationToken));

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("scad-rotate-extrude-axis-crossing", report.Feature);
        Assert.Equal(new ScadSourceLocation(null, 2, 3), report.Location);
        Assert.Equal(1, scene.ComputeBounds().Max.X, 1e-12);
        Assert.Equal(report.Feature, ex.Feature);
    }

    /// <summary>
    ///     Validates that a rejected extrusion inside a boolean empties only what it must: the rejected operand is
    ///     an empty operand (a rejected subtrahend vanishes, a rejected minuend empties the difference).
    /// </summary>
    [Fact]
    public void ScadExtrusionsMapping_RejectedOperand_IsAnEmptyOperand()
    {
        // Arrange / Act
        var asSubtrahend = Build("difference() { cube(5); rotate_extrude() translate([-1, 0]) square(2); }");
        var asMinuend = Build("difference() { rotate_extrude() translate([-1, 0]) square(2); cube(5); }");

        // Assert
        Assert.Single(asSubtrahend.Meshes);
        Assert.Empty(asMinuend.Meshes);
        Assert.Equal(["scad-rotate-extrude-axis-crossing"], asSubtrahend.Features);
        Assert.Equal(["scad-rotate-extrude-axis-crossing"], asMinuend.Features);
    }

    /// <summary>
    ///     Validates that unusual and extreme extrusion inputs never crash the build: each either builds, is
    ///     reported, or fails with the documented evaluation error for a resource limit.
    /// </summary>
    /// <param name="source">The program.</param>
    [Theory]
    [InlineData("linear_extrude(1e308) square(1);")]
    [InlineData("linear_extrude(1e-320) square(1);")]
    [InlineData("linear_extrude(1, scale = 1e-320) square(1);")]
    [InlineData("linear_extrude(1, scale = 1e308) square(1);")]
    [InlineData("linear_extrude(1, scale = [1e-320, 1e-320]) square(1);")]
    [InlineData("linear_extrude(1, scale = [1e308, 1e-300]) square(1);")]
    [InlineData("linear_extrude(1, twist = 1e308, slices = 2) square(1);")]
    [InlineData("linear_extrude(1, twist = -1e308, slices = 1) square(1);")]
    [InlineData("linear_extrude(1e308, twist = 720, slices = 3, scale = 1e308) square(1e150);")]
    [InlineData("linear_extrude(5) scale(1e-200) square(1);")]
    [InlineData("linear_extrude(5) translate([1e308, 1e308]) square(1);")]
    [InlineData("linear_extrude(5) polygon([[0, 0], [1e-200, 0], [0, 1e-200]]);")]
    [InlineData("linear_extrude(5) polygon([[0, 0], [1, 0], [0.5, 0], [0.25, 0]]);")]
    [InlineData("linear_extrude(5) polygon([[0, 0], [1, 1], [1, 0], [0, 1]]);")]
    [InlineData("linear_extrude(5, slices = 1, scale = 0) circle(1e-9);")]
    [InlineData("linear_extrude(5, twist = 90, scale = 0) square(1);")]
    [InlineData("linear_extrude(5, twist = 90, scale = [1, 5]) translate([5, 5]) square(1);")]
    [InlineData("rotate_extrude(angle = 5e-324) translate([1, 0]) square(1);")]
    [InlineData("rotate_extrude(angle = 1e-300, $fn = 3) translate([1, 0]) square(1);")]
    [InlineData("rotate_extrude(angle = 1e-10) translate([1e300, 0]) square(1);")]
    [InlineData("rotate_extrude() translate([1e308, 0]) square(1e300);")]
    [InlineData("rotate_extrude($fn = 3) translate([1e-300, 0]) square(1e-300);")]
    [InlineData("rotate_extrude(angle = 1e300) translate([1, 0]) square(1);")]
    [InlineData("rotate_extrude(angle = -1e300) translate([1, 0]) square(1);")]
    [InlineData("rotate_extrude() translate([0, 0]) polygon([[0, 0], [1, 0], [1, 1]]);")]
    [InlineData("rotate_extrude() polygon([[0, 0], [0, 1], [0, 2]]);")]
    [InlineData("rotate_extrude() difference() { translate([1, 0]) square(3); translate([2, 1]) square(1); }")]
    [InlineData("rotate_extrude() union() { translate([1, 0]) square(1); translate([1, 1]) square(1); }")]
    [InlineData("rotate_extrude() { translate([1, 0]) square(1); translate([1, 1]) square(1); translate([1, 0.5]) square(1); }")]
    [InlineData("linear_extrude(3) { square(2); translate([1, 1]) square(2); }")]
    public void ScadExtrusionsMapping_ExtremeInputs_NeverCrashTheBuild(string source)
    {
        // Arrange / Act
        var exception = Record.Exception(() => Build(source));

        // Assert: only a resource limit may stop a build
        Assert.True(exception is null or ScadEvaluationException, $"{source} threw {exception}");
    }

    /// <summary>
    ///     Validates that a 3D shape with a rejected polygon inside a 2D boolean still reports and does not crash
    ///     the extrusion above it.
    /// </summary>
    [Fact]
    public void ScadExtrusionsMapping_InvalidPolygonInsideBoolean_IsReportedAndTheRestExtrudes()
    {
        // Arrange / Act
        var result = Build("linear_extrude(2) difference() { square(5); polygon([[0, 0], [1, 1], [1, 0], [0, 1]]); }");

        // Assert: the bow-tie polygon is invalid, so the difference is the square alone
        Assert.Equal(["scad-polygon-invalid"], result.Features);
        AssertBounds(result.WorldMesh.Bounds, new Vector3D(0, 0, 0), new Vector3D(5, 5, 2));
    }
}

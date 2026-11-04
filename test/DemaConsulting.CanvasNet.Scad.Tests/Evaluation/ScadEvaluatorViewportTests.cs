using DemaConsulting.CanvasNet.Render3D.SceneModel;
using DemaConsulting.CanvasNet.Scad.Evaluation;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Unit tests of the viewport special variables <c>$vpt</c>, <c>$vpr</c>, <c>$vpd</c> and <c>$vpf</c> in the
///     evaluator: their defaults, the read-back of what the main file assigns, invalid values, overrides and the files
///     that count.
/// </summary>
public class ScadEvaluatorViewportTests
{
    /// <summary>
    ///     Validates the documented defaults of a fresh viewer, and that nothing is reported for reading them.
    /// </summary>
    [Fact]
    public void ScadEvaluatorViewport_Defaults_AreTheViewerDefaults()
    {
        // Arrange / Act
        var output = ScadEvaluatorTestSupport.Run("echo($vpt, $vpr, $vpd, $vpf);");

        // Assert
        Assert.Equal(["ECHO: [0, 0, 0], [55, 0, 25], 140, 22.5"], output.Echo);
        Assert.Empty(output.Diagnostics);
        Assert.Equal(ScadDocumentViewport.None, output.Viewport);
    }

    /// <summary>
    ///     Validates that the values the caller supplies are what the document sees until it assigns its own.
    /// </summary>
    [Fact]
    public void ScadEvaluatorViewport_SuppliedValues_AreVisibleToTheDocument()
    {
        // Arrange
        var settings = new ScadEvaluationSettings { Viewport = new ScadViewportValues(new Vector3D(1, 2, 3), new Vector3D(10, 20, 30), 77, 33) };

        // Act
        var output = ScadEvaluatorTestSupport.Run("echo($vpt, $vpr, $vpd, $vpf);", settings);

        // Assert: visible, and not "assigned" by the document
        Assert.Equal(["ECHO: [1, 2, 3], [10, 20, 30], 77, 33"], output.Echo);
        Assert.Equal(ScadDocumentViewport.None, output.Viewport);
    }

    /// <summary>
    ///     Validates the read-back of top-level assignments, including a vector of two numbers (the third is zero).
    /// </summary>
    [Fact]
    public void ScadEvaluatorViewport_TopLevelAssignments_AreReadBack()
    {
        // Arrange / Act
        var output = ScadEvaluatorTestSupport.Run("$vpt = [1, 2];\n$vpr = [10, 20, 30];\n$vpd = 99;\n$vpf = 40;\ncube(1);");

        // Assert
        Assert.Equal(new ScadDocumentViewport(new Vector3D(1, 2, 0), new Vector3D(10, 20, 30), 99.0, 40.0), output.Viewport);
        Assert.True(output.Viewport!.IsManual);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that only <c>$vpr</c> assigned is not a manual camera.
    /// </summary>
    [Fact]
    public void ScadEvaluatorViewport_OnlyRotation_IsNotManual()
    {
        // Arrange / Act
        var output = ScadEvaluatorTestSupport.Run("$vpr = [60, 0, 10];");

        // Assert
        Assert.Equal(new Vector3D(60, 0, 10), output.Viewport!.Rotation);
        Assert.False(output.Viewport.IsManual);
    }

    /// <summary>
    ///     Validates that values the viewer cannot use are reported once, with the value, and ignored (the document
    ///     still sees what it assigned).
    /// </summary>
    /// <param name="assignment">The assignment.</param>
    /// <param name="expected">The expected warning text.</param>
    [Theory]
    [InlineData("$vpr = [1, 2, 3, 4];", "Unable to convert $vpr=[1, 2, 3, 4] to a vec3 or vec2 of numbers; the viewport variable is ignored")]
    [InlineData("$vpt = \"x\";", "Unable to convert $vpt=\"x\" to a vec3 or vec2 of numbers; the viewport variable is ignored")]
    [InlineData("$vpt = [1, \"a\", 3];", "Unable to convert $vpt=[1, \"a\", 3] to a vec3 or vec2 of numbers; the viewport variable is ignored")]
    [InlineData("$vpd = -5;", "Unable to convert $vpd=-5 to a finite number greater than 0; the viewport variable is ignored")]
    [InlineData("$vpd = [1];", "Unable to convert $vpd=[1] to a finite number greater than 0; the viewport variable is ignored")]
    [InlineData("$vpf = 180;", "Unable to convert $vpf=180 to a number between 0 and 180 (exclusive); the viewport variable is ignored")]
    [InlineData("$vpf = 0;", "Unable to convert $vpf=0 to a number between 0 and 180 (exclusive); the viewport variable is ignored")]
    public void ScadEvaluatorViewport_InvalidValue_WarnsOnceAndIsIgnored(string assignment, string expected)
    {
        // Arrange / Act
        var output = ScadEvaluatorTestSupport.Run($"\n{assignment}\ncube(1);");

        // Assert
        var warning = Assert.Single(output.Diagnostics);
        Assert.Equal(expected, warning.Message);
        Assert.Equal(2, warning.Location.Line);
        Assert.Equal(ScadDocumentViewport.None, output.Viewport);
    }

    /// <summary>
    ///     Validates that an override from outside the file wins over the file's own assignment, and that an override
    ///     alone counts as assigned.
    /// </summary>
    [Fact]
    public void ScadEvaluatorViewport_Override_WinsOverTheAssignment()
    {
        // Arrange
        var settings = new ScadEvaluationSettings { Overrides = new Dictionary<string, ScadValue> { ["$vpd"] = ScadValue.FromNumber(7) } };

        // Act
        var assigned = ScadEvaluatorTestSupport.Run("$vpd = 9;\necho($vpd);", settings);
        var alone = ScadEvaluatorTestSupport.Run("cube(1);", settings);

        // Assert
        Assert.Equal(7.0, assigned.Viewport!.Distance);
        Assert.Equal(["ECHO: 7"], assigned.Echo);
        Assert.Equal(7.0, alone.Viewport!.Distance);
    }

    /// <summary>
    ///     Validates that an assignment can use <c>$t</c> and other variables, and that a self-reference does not crash.
    /// </summary>
    [Fact]
    public void ScadEvaluatorViewport_Assignment_CanUseTheAnimationTimeAndOtherVariables()
    {
        // Arrange
        var settings = new ScadEvaluationSettings { Time = 0.25 };

        // Act
        var animated = ScadEvaluatorTestSupport.Run("$vpr = [0, 0, $t * 360];", settings);
        var derived = ScadEvaluatorTestSupport.Run("w = 30;\n$vpd = w * 2;");
        var circular = ScadEvaluatorTestSupport.Run("$vpd = $vpd * 2;");

        // Assert
        Assert.Equal(new Vector3D(0, 0, 90), animated.Viewport!.Rotation);
        Assert.Equal(60.0, derived.Viewport!.Distance);
        Assert.Contains(circular.Diagnostics, d => d.Message.Contains("Circular reference", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that an assignment in an included file counts (as in the language) and one in a used library does not,
    ///     and that an assignment that is not at the top level of the main file has no effect on the viewport.
    /// </summary>
    [Fact]
    public void ScadEvaluatorViewport_IncludeCountsAndUseAndNestedScopesDoNot()
    {
        // Arrange
        var files = new ScadVirtualFiles(("v.scad", "$vpd = 33;\n"));

        // Act
        var included = ScadEvaluatorTestSupport.Run("include <v.scad>\ncube(1);", files: files);
        var used = ScadEvaluatorTestSupport.Run("use <v.scad>\ncube(1);", files: files);
        var nested = ScadEvaluatorTestSupport.Run("module m() { $vpd = 5; cube(1); }\nm();");

        // Assert
        Assert.Equal(33.0, included.Viewport!.Distance);
        Assert.Equal(ScadDocumentViewport.None, used.Viewport);
        Assert.Equal(ScadDocumentViewport.None, nested.Viewport);
    }

    /// <summary>
    ///     Validates that the viewport variables are special variables like the others: a module can rebind them for its
    ///     children, which does not change the viewport.
    /// </summary>
    [Fact]
    public void ScadEvaluatorViewport_ReboundInAModule_IsVisibleToChildrenOnly()
    {
        // Arrange / Act
        var output = ScadEvaluatorTestSupport.Run("module m() { echo($vpd); children(); }\nm($vpd = 12) echo($vpd);\necho($vpd);");

        // Assert
        Assert.Equal(["ECHO: 12", "ECHO: 12", "ECHO: 140"], output.Echo);
        Assert.Equal(ScadDocumentViewport.None, output.Viewport);
    }
}

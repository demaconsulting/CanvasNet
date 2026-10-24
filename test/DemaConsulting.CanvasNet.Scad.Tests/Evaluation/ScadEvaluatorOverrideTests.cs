using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of the variable overrides (the <c>-D</c> style definitions) applied by <see cref="ScadEvaluator"/>.
/// </summary>
public class ScadEvaluatorOverrideTests
{
    /// <summary>
    ///     Creates settings with overrides.
    /// </summary>
    /// <param name="pairs">The override pairs.</param>
    /// <returns>The settings.</returns>
    private static ScadEvaluationSettings Overriding(params (string Name, ScadValue Value)[] pairs) => new()
    {
        Overrides = pairs.ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal),
    };

    /// <summary>
    ///     Validates that an override replaces an assignment and that the replaced right-hand side is never evaluated.
    /// </summary>
    [Fact]
    public void ScadEvaluatorOverride_ReplacesAssignment_WithoutEvaluatingIt()
    {
        // Arrange
        var settings = Overriding(("x", ScadValue.FromNumber(5)));

        // Act
        var output = Run("x = echo(\"rhs\") 1;\necho(x);", settings);

        // Assert
        Assert.Equal("ECHO: 5", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that an override creates the variable when the program does not assign it.
    /// </summary>
    [Fact]
    public void ScadEvaluatorOverride_AbsentVariable_IsCreated()
    {
        // Arrange
        var settings = Overriding(("y", ScadValue.FromString("hello")));

        // Act
        var output = Run("echo(y);", settings);

        // Assert
        Assert.Equal("ECHO: \"hello\"", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that an override replaces every top-level assignment of the name, without a reassignment warning.
    /// </summary>
    [Fact]
    public void ScadEvaluatorOverride_RepeatedAssignments_AreAllReplaced()
    {
        // Arrange
        var settings = Overriding(("x", ScadValue.FromNumber(9)));

        // Act
        var output = Run("x = 1;\nx = 2;\necho(x);", settings);

        // Assert
        Assert.Equal("ECHO: 9", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that overrides reach functions and modules that read the variable, and that nested assignments
    ///     (a different scope) are not replaced.
    /// </summary>
    [Fact]
    public void ScadEvaluatorOverride_Scope_AppliesToTopLevelOnly()
    {
        // Arrange
        var settings = Overriding(("n", ScadValue.FromNumber(3)));
        const string Source = "n = 1;\nfunction f() = n;\nmodule m() { n = 100; echo(n); }\nm();\necho(f());";

        // Act
        var echo = Echo(Source);
        var overridden = Run(Source, settings).Echo;

        // Assert
        Assert.Equal(["ECHO: 100", "ECHO: 1"], echo);
        Assert.Equal(["ECHO: 100", "ECHO: 3"], overridden);
    }

    /// <summary>
    ///     Validates that vector overrides keep their structure.
    /// </summary>
    [Fact]
    public void ScadEvaluatorOverride_VectorValue_IsUsableAsVector()
    {
        // Arrange
        var settings = Overriding(("v", ScadValue.FromVector([ScadValue.FromNumber(1), ScadValue.FromNumber(2)])));

        // Act
        var output = Run("v = [0];\necho(v[1], len(v));", settings);

        // Assert
        Assert.Equal("ECHO: 2, 2", Assert.Single(output.Echo));
    }

    /// <summary>
    ///     Validates that the order the overrides are supplied in does not matter.
    /// </summary>
    [Fact]
    public void ScadEvaluatorOverride_Order_DoesNotMatter()
    {
        // Arrange
        const string Source = "echo(a, b, c);";
        var forward = Overriding(("a", ScadValue.FromNumber(1)), ("b", ScadValue.FromNumber(2)), ("c", ScadValue.FromNumber(3)));
        var backward = Overriding(("c", ScadValue.FromNumber(3)), ("b", ScadValue.FromNumber(2)), ("a", ScadValue.FromNumber(1)));

        // Act
        var first = Run(Source, forward).Echo;
        var second = Run(Source, backward).Echo;

        // Assert
        Assert.Equal(["ECHO: 1, 2, 3"], first);
        Assert.Equal(first, second);
    }

    /// <summary>
    ///     Validates that an override of a name used by a later dependent assignment flows into it.
    /// </summary>
    [Fact]
    public void ScadEvaluatorOverride_DependentAssignments_SeeOverriddenValue()
    {
        // Arrange
        var settings = Overriding(("width", ScadValue.FromNumber(10)));

        // Act
        var output = Run("width = 1;\narea = width * width;\ncube(area);", settings);

        // Assert
        Assert.Equal("cube(100)", ScadInstanceDumper.Dump(output.Roots));
    }
}

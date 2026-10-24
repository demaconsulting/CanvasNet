using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of <c>$</c> special variables in <see cref="ScadEvaluator"/>: defaults, dynamic scoping through
///     modules, functions and <c>children()</c>, and named <c>$</c> arguments.
/// </summary>
public class ScadEvaluatorSpecialVariableTests
{
    /// <summary>
    ///     Validates the defaults of the special variables.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_Defaults_AreDocumentedValues()
    {
        // Arrange
        const string Source = "echo($fn, $fa, $fs, $t, $preview);";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("0, 12, 2, 0, true", text);
    }

    /// <summary>
    ///     Validates that the time setting becomes <c>$t</c>.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_Time_IsTakenFromSettings()
    {
        // Arrange
        var settings = new ScadEvaluationSettings { Time = 0.25 };

        // Act
        var output = Run("echo($t);", settings);

        // Assert
        Assert.Equal("ECHO: 0.25", Assert.Single(output.Echo));
    }

    /// <summary>
    ///     Validates dynamic scoping: a <c>$</c> variable set in a caller is visible to everything it calls, and
    ///     is unbound again outside.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_SetInCaller_IsVisibleThroughNestedCalls()
    {
        // Arrange
        const string Source = """
            module leaf() { echo($x); }
            module middle() { leaf(); }
            module top() { $x = 5; middle(); }
            top();
            leaf();
            """;

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 5", "ECHO: undef"], output.Echo);
        Assert.Equal("Ignoring unknown variable '$x'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates dynamic scoping through functions, including a <c>let</c> binding of a <c>$</c> variable.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_BoundByLet_IsVisibleInCalledFunction()
    {
        // Arrange
        const string Source = "function g() = $d;\nfunction h() = let($d = 7) g();\necho(h(), g());";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: 7, undef", Assert.Single(output.Echo));
        Assert.Single(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that a named <c>$</c> argument binds even when the callee has no such parameter.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_NamedArgumentWithoutParameter_BindsDynamically()
    {
        // Arrange
        const string Source = "module m() { echo($q); }\nm($q = 3);\nfunction f() = $r;\necho(f($r = 8));";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 3", "ECHO: 8"], output.Echo);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates shadowing: a <c>$</c> parameter, with or without a default, overrides the outer binding for the call.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_Parameter_ShadowsOuterBinding()
    {
        // Arrange
        const string Source = """
            $q = 1;
            module show() { echo($q); }
            module wrapped($q = 2) { show(); }
            wrapped();
            show();
            wrapped($q = 9);
            """;

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("2|1|9", text);
    }

    /// <summary>
    ///     Validates that <c>$children</c> is the number of child slots.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_Children_CountsSlots()
    {
        // Arrange
        const string Source = "module m() { echo($children); }\nm();\nm() cube(1);\nm() { cube(1); sphere(2); { cube(3); } }";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("0|1|3", text);
    }

    /// <summary>
    ///     Validates that <c>$</c> variables set in a module before <c>children()</c> are visible to the children.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_SetBeforeChildren_IsVisibleToChildren()
    {
        // Arrange
        const string Source = "module m() { $v = 4; children(); }\nm() { echo($v); }";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("4", text);
    }

    /// <summary>
    ///     Validates that the fragment settings of instances follow the dynamic <c>$fn</c> and a named argument overrides it.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_Fragments_FollowDynamicBindingAndArgumentOverride()
    {
        // Arrange
        const string Source = "$fn = 20;\nmodule m() { $fa = 3; sphere(1); sphere(2, $fn = 50); }\nm();\ncube(1);";

        // Act
        var output = Run(Source);

        // Assert
        var group = output.Roots[0];
        Assert.Equal(new ScadFragmentSettings(20, 3, 2), group.Children[0].Fragments);
        Assert.Equal(new ScadFragmentSettings(50, 3, 2), group.Children[1].Fragments);
        Assert.Equal(new ScadFragmentSettings(20, 12, 2), output.Roots[1].Fragments);
    }

    /// <summary>
    ///     Validates that an override of a special variable is visible everywhere.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_OverrideOfSpecial_ReplacesDefault()
    {
        // Arrange
        var settings = new ScadEvaluationSettings
        {
            Overrides = new Dictionary<string, ScadValue> { ["$fn"] = ScadValue.FromNumber(64) },
        };

        // Act
        var output = Run("module m() { echo($fn); }\nm();", settings);

        // Assert
        Assert.Equal("ECHO: 64", Assert.Single(output.Echo));
    }

    /// <summary>
    ///     Validates that tail recursion that binds no <c>$</c> name leaves the special chain alone, so it is unbounded
    ///     by the recursion limit even when that limit is tiny.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_TailRecursionWithoutSpecials_DoesNotGrowChain()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = 16 });

        // Act
        var output = Run("function loop(n) = n == 0 ? 0 : loop(n - 1);\necho(loop(20000));", settings);

        // Assert
        Assert.Equal("ECHO: 0", Assert.Single(output.Echo));
    }

    /// <summary>
    ///     Validates that recursion binding a <c>$</c> parameter at every level is bounded by the recursion limit,
    ///     because each level adds a frame to the dynamic chain.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_TailRecursionBindingSpecial_IsBoundedByRecursionLimit()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = 50 });
        const string Source = "function loop(n, $d = 0) = n == 0 ? $d : loop(n - 1, $d = $d + 1);\necho(loop(40), loop(100));";

        // Act
        var ex = Fails(Source, settings);

        // Assert
        Assert.Contains("Recursion too deep (limit 50)", ex.Message, StringComparison.Ordinal);
    }
}

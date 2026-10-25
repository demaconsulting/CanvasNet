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
    ///     Validates that a <c>$</c> variable assigned in a child list is visible to that list's children, whatever
    ///     the module does (the repro of the original defect).
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ChildListAssignment_IsVisibleToChildren()
    {
        // Arrange
        const string Source = "module m() { children(); }\nm() { $x = 4; echo($x); }";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: 4", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates precedence: the child list's binding shadows a same-named binding of the module, and the module
    ///     body itself never sees the child list's binding.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ChildListAssignment_ShadowsModuleBinding()
    {
        // Arrange
        const string Source = "module m() { $x = 9; echo($x); children(); echo($x); }\nm() { $x = 4; echo($x); }";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("9|4|9", text);
    }

    /// <summary>
    ///     Validates that the child list's special variables are evaluated under the call site's chain, once, even
    ///     when the module rebinds the variable they depend on.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ChildListAssignment_DependsOnCallSiteSpecials()
    {
        // Arrange
        const string Source = "module m() { $a = 100; children(); }\n$a = 2;\nm() { $b = $a * 3; echo($b); }";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("6", text);
    }

    /// <summary>
    ///     Validates that the dynamic scope of the children reaches modules and functions they call.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ChildListAssignment_ReachesModulesCalledByChildren()
    {
        // Arrange
        const string Source = "module show() { echo($x, f()); }\nfunction f() = $x * 2;\nmodule m() { children(); }\nm() { $x = 4; show(); }";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("4, 8", text);
    }

    /// <summary>
    ///     Validates that the binding exists only for the children: neither the module body nor the call site sees it.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ChildListAssignment_IsNotVisibleAtTheCallSite()
    {
        // Arrange
        const string Source = "module m() { children(); echo($x); }\nm() { $x = 4; echo($x); }\necho($x);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 4", "ECHO: undef", "ECHO: undef"], output.Echo);
        Assert.Equal(2, output.Diagnostics.Count);
    }

    /// <summary>
    ///     Validates that children forwarded through a nested module still see the binding of their own child list.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ChildListAssignment_ReachesChildrenForwardedThroughNestedModule()
    {
        // Arrange
        const string Source = """
            module inner() { children(); }
            module outer() { inner() { children(); } }
            outer() { $x = 4; echo($x); }
            """;

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("4", text);
    }

    /// <summary>
    ///     Validates that a child selected by index sees the binding of the list it belongs to.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ChildListAssignment_SelectedChild_SeesIt()
    {
        // Arrange
        const string Source = "module m() { children(1); }\nm() { echo(\"first\"); $x = 4; echo($x); }";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("4", text);
    }

    /// <summary>
    ///     Validates that the extra frame a child list adds counts towards the recursion limit: forwarding children
    ///     through deeply nested modules ends in the limit error, never in a stack error.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ChildListAssignment_InRecursion_IsBoundedByRecursionLimit()
    {
        // Arrange
        var settings = With(new ScadLimits { MaxRecursionDepth = 16 });
        const string Source = """
            module nest(n) { if (n > 0) nest(n - 1) { $d = n; children(); } else children(); }
            nest(1000) { echo($d); }
            """;

        // Act
        var ex = Fails(Source, settings);

        // Assert
        Assert.Contains("Recursion too deep (limit 16)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the regression twin of the module-binding case: a child list that assigns only ordinary
    ///     variables adds no special frame, so the module's bindings still reach its children.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_ModuleBinding_WithoutChildListSpecials_StillReachesChildren()
    {
        // Arrange
        const string Source = "module m() { $v = 4; children(); }\nm() { y = 1; echo($v, y); }";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: 4, 1", Assert.Single(output.Echo));
        Assert.Empty(output.Diagnostics);
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

    /// <summary>
    ///     Validates that a named <c>$</c> argument of a built-in module is bound dynamically for the children of that
    ///     instance only.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_BuiltinModuleArgument_BindsForChildrenOnly()
    {
        // Arrange
        const string Source = """
            translate([0, 0, 0], $x = 7) echo($x);
            echo($x);
            """;

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 7", "ECHO: undef"], output.Echo);
        Assert.Equal("Ignoring unknown variable '$x'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that the <c>$</c> argument is visible to modules and functions that the children call, and that
    ///     the arguments of the instance are still evaluated in the scope of the call site.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_BuiltinModuleArgument_IsDynamicAndArgumentsUseCallSite()
    {
        // Arrange
        const string Source = """
            $x = 1;
            function f() = $x * 10;
            module show() { echo($x, f()); }
            translate([0, 0, 0], $x = $x + 1) show();
            show();
            translate([0, 0, 0], $x = 1, $x = 2) echo($x);
            """;

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("2, 20|1, 10|2", text);
    }

    /// <summary>
    ///     Validates that <c>$fn</c>, <c>$fa</c> and <c>$fs</c> arguments are recorded for the instance itself and
    ///     inherited by everything inside it, but not by its siblings.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_BuiltinModuleFragmentArgument_PropagatesToNestedChildren()
    {
        // Arrange
        const string Source = """
            rotate(45, $fn = 8, $fa = 3, $fs = 0.5) { sphere(1); translate([1, 0, 0]) cube(1); }
            sphere(2);
            """;

        // Act
        var roots = Run(Source).Roots;

        // Assert
        var expected = new ScadFragmentSettings(8, 3, 0.5);
        Assert.Equal(expected, roots[0].Fragments);
        Assert.Equal(expected, roots[0].Children[0].Fragments);
        Assert.Equal(expected, roots[0].Children[1].Fragments);
        Assert.Equal(expected, roots[0].Children[1].Children[0].Fragments);
        Assert.Equal(new ScadFragmentSettings(0, 12, 2), roots[1].Fragments);
    }

    /// <summary>
    ///     Validates the precedence of fragment settings: the nearest binding wins, a built-in argument beats an
    ///     assignment of the enclosing scope, and a user-module argument reaches built-in children.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_BuiltinModuleFragmentArgument_NearestBindingWins()
    {
        // Arrange
        const string Source = """
            $fn = 30;
            translate([0, 0, 0], $fn = 8) {
                sphere(1);
                sphere(1, $fn = 3);
                rotate(1, $fn = 16) sphere(2);
            }
            sphere(3);
            module wrap() { sphere(4); }
            wrap($fn = 12);
            """;

        // Act
        var roots = Run(Source).Roots;

        // Assert
        Assert.Equal(8, roots[0].Fragments.Fn);
        Assert.Equal(8, roots[0].Children[0].Fragments.Fn);
        Assert.Equal(3, roots[0].Children[1].Fragments.Fn);
        Assert.Equal(16, roots[0].Children[2].Fragments.Fn);
        Assert.Equal(16, roots[0].Children[2].Children[0].Fragments.Fn);
        Assert.Equal(30, roots[1].Fragments.Fn);
        Assert.Equal(12, roots[2].Children[0].Fragments.Fn);
    }

    /// <summary>
    ///     Validates that a <c>$</c> argument given to a user module is seen by its body and by the children it
    ///     re-evaluates with <c>children()</c>.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_UserModuleArgument_ReachesBodyAndChildren()
    {
        // Arrange
        const string Source = """
            module m() { echo("body", $x); children(); }
            m($x = 3) echo("child", $x);
            """;

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("\"body\", 3|\"child\", 3", text);
    }

    /// <summary>
    ///     Validates that a <c>$</c> argument of a built-in module wrapped around <c>children()</c> is in effect for
    ///     the re-evaluated children, and that fragments set around the user-module call reach them too.
    /// </summary>
    [Fact]
    public void ScadEvaluatorSpecialVariable_BuiltinModuleArgumentAroundChildren_ReachesReevaluatedChildren()
    {
        // Arrange
        const string Source = """
            module wrap() { translate([0, 0, 0], $k = 5) children(); }
            wrap() { echo($k); sphere(1); }
            translate([0, 0, 0], $fn = 6) wrap() sphere(2);
            wrap() sphere(3);
            """;

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: 5", Assert.Single(output.Echo));
        Assert.Equal(0, output.Roots[0].Children[0].Children[0].Fragments.Fn);
        Assert.Equal(6, output.Roots[1].Children[0].Children[0].Children[0].Fragments.Fn);
        Assert.Equal(0, output.Roots[2].Children[0].Children[0].Fragments.Fn);
    }
}

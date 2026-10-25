using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of user modules, defaults, recursion and <c>children()</c> in
///     <see cref="DemaConsulting.CanvasNet.Scad.Evaluation.ScadEvaluator"/>. Trees are compared as location-free dumps.
/// </summary>
public class ScadEvaluatorModuleTests
{
    /// <summary>
    ///     Validates that a user module call produces a group named after the module holding its body's instances.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Call_ProducesNamedGroupOfBodyInstances()
    {
        // Arrange
        const string Source = "module part(w = 2) { cube(w); sphere(1); }\npart();\npart(5);";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("group<part>{cube(2);sphere(1)};group<part>{cube(5);sphere(1)}", tree);
    }

    /// <summary>
    ///     Validates that a module with an empty body still produces an (empty) group.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_EmptyBody_ProducesEmptyGroup()
    {
        // Arrange
        const string Source = "module nothing_here() {}\nnothing_here();";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("group<nothing_here>", tree);
    }

    /// <summary>
    ///     Validates recursion between modules.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Recursion_NestsGroups()
    {
        // Arrange
        const string Source = "module r(n) { if (n > 0) { cube(n); r(n - 1); } }\nr(3);";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("group<r>{cube(3);group<r>{cube(2);group<r>{cube(1);group<r>}}}", tree);
    }

    /// <summary>
    ///     Validates that a user module shadows a built-in of the same name.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_UserModule_ShadowsBuiltin()
    {
        // Arrange
        const string Source = "module cube(s) { echo(\"mine\", s); }\ncube(3);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: \"mine\", 3", Assert.Single(output.Echo));
        Assert.Equal("group<cube>", ScadInstanceDumper.Dump(output.Roots));
    }

    /// <summary>
    ///     Validates module parameter binding: defaults that see earlier parameters, named arguments, and warnings
    ///     for extra and unknown arguments.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_ArgumentBinding_FollowsFunctionRules()
    {
        // Arrange
        const string Source = """
            module m(a, b = a * 2) { echo(a, b); }
            m(1);
            m(b = 1, a = 5);
            m(1, 2, 3);
            m(1, z = 4);
            """;

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 1, 2", "ECHO: 5, 1", "ECHO: 1, 2", "ECHO: 1, 2"], output.Echo);
        Assert.Equal(
            ["Ignoring extra argument for module 'm'", "Ignoring unknown argument 'z' for module 'm'"],
            output.Diagnostics.Select(d => d.Message));
    }

    /// <summary>
    ///     Validates that <c>children()</c> with no argument places every child, in order.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Children_AllInOrder()
    {
        // Arrange
        const string Source = "module wrap() { translate([1, 0, 0]) children(); }\nwrap() { cube(1); sphere(2); }";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("group<wrap>{translate([1, 0, 0]){cube(1);sphere(2)}}", tree);
    }

    /// <summary>
    ///     Validates selecting children by number, vector, range and named argument.
    /// </summary>
    /// <param name="selector">The argument text of <c>children</c>.</param>
    /// <param name="expected">The expected dump of the module group.</param>
    [Theory]
    [InlineData("1", "group<pick>{sphere(2)}")]
    [InlineData("index = 2", "group<pick>{cylinder(3)}")]
    [InlineData("[0, 2]", "group<pick>{cube(1);cylinder(3)}")]
    [InlineData("[2, 0, 2]", "group<pick>{cylinder(3);cube(1);cylinder(3)}")]
    [InlineData("[1 : 2]", "group<pick>{sphere(2);cylinder(3)}")]
    [InlineData("[0 : 2 : 2]", "group<pick>{cube(1);cylinder(3)}")]
    [InlineData("[]", "group<pick>")]
    public void ScadEvaluatorModule_Children_Selection_PicksSlots(string selector, string expected)
    {
        // Arrange
        var source = $"module pick() {{ children({selector}); }}\npick() {{ cube(1); sphere(2); cylinder(3); }}";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal(expected, ScadInstanceDumper.Dump(output.Roots));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that invalid child indexes warn and place nothing.
    /// </summary>
    /// <param name="selector">The argument text of <c>children</c>.</param>
    [Theory]
    [InlineData("5")]
    [InlineData("-1")]
    [InlineData("0.5")]
    [InlineData("\"a\"")]
    [InlineData("undef")]
    [InlineData("[0 / 0]")]
    public void ScadEvaluatorModule_Children_InvalidIndex_WarnsAndPlacesNothing(string selector)
    {
        // Arrange
        var source = $"module pick() {{ children({selector}); }}\npick() cube(1);";

        // Act
        var output = Run(source);

        // Assert
        Assert.Equal("group<pick>", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal("Ignoring invalid child index", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates the warning for <c>children()</c> outside a module.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Children_OutsideModule_WarnsAndPlacesNothing()
    {
        // Arrange
        const string Source = "children();\ncube(1);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("cube(1)", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal("children() used outside a module", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates slot counting: nested braces are flattened, while a <c>for</c> or <c>if</c> is one slot however
    ///     many instances it produces, and assignments are not slots.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Children_SlotCounting()
    {
        // Arrange
        const string Source = """
            module count() { echo($children); }
            count() { cube(1); { cube(2); cube(3); } }
            count() { for (i = [0 : 2]) cube(i); if (true) { cube(1); cube(2); } }
            count() { s = 4; cube(s); }
            module first() { children(0); }
            first() { for (i = [0 : 2]) cube(i); sphere(1); }
            """;

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(["ECHO: 3", "ECHO: 2", "ECHO: 1"], output.Echo);
        Assert.Equal(
            "group<count>;group<count>;group<count>;group<first>{group<for>{cube(0);cube(1);cube(2)}}",
            ScadInstanceDumper.Dump(output.Roots));
    }

    /// <summary>
    ///     Validates that assignments in a child list are visible to the children.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Children_SeeAssignmentsOfTheirList()
    {
        // Arrange
        const string Source = "module m() { children(); }\nm() { s = 4; cube(s); }";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("group<m>{cube(4)}", tree);
    }

    /// <summary>
    ///     Validates that children are evaluated lexically at the call site, not in the module body.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Children_AreEvaluatedAtTheCallSite()
    {
        // Arrange
        const string Source = "x = 1;\nmodule m() { x = 2; children(); }\nm() echo(x);";

        // Act
        var text = EchoText(Source);

        // Assert
        Assert.Equal("1", text);
    }

    /// <summary>
    ///     Validates that children pass through a module that forwards them with its own <c>children()</c>.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Children_ForwardedThroughNestedModule()
    {
        // Arrange
        const string Source = "module outer() { inner() children(); }\nmodule inner() { children(); }\nouter() cube(1);";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("group<outer>{group<inner>{cube(1)}}", tree);
    }

    /// <summary>
    ///     Validates that an unknown module warns and that neither its arguments nor its children are evaluated.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Unknown_WarnsAndSkipsChildren()
    {
        // Arrange
        const string Source = "mystery(echo(\"arg\") 1) echo(\"child\");\ncube(1);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Empty(output.Echo);
        Assert.Equal("cube(1)", ScadInstanceDumper.Dump(output.Roots));
        var warning = Assert.Single(output.Diagnostics);
        Assert.Equal("Ignoring unknown module 'mystery'", warning.Message);
        Assert.Equal(1, warning.Location.Column);
    }

    /// <summary>
    ///     Validates that nested module definitions are visible only inside their enclosing module.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_NestedDefinition_IsLocal()
    {
        // Arrange
        const string Source = "module outer() { module leaf() { cube(1); } leaf(); }\nouter();\nleaf();";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("group<outer>{group<leaf>{cube(1)}}", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal("Ignoring unknown module 'leaf'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that module bodies can use <c>echo</c>, <c>assert</c> and <c>let</c> statements.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Body_EchoLetAssertStatements()
    {
        // Arrange
        const string Source = "module m(n) { assert(n > 0, \"n must be positive\"); let(k = n * 2) echo(k) cube(k); }\nm(2);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("ECHO: 4", Assert.Single(output.Echo));
        Assert.Equal("group<m>{cube(4)}", ScadInstanceDumper.Dump(output.Roots));
    }

    /// <summary>
    ///     Validates that an assertion failing inside a module aborts with the message.
    /// </summary>
    [Fact]
    public void ScadEvaluatorModule_Body_AssertFailure_Aborts()
    {
        // Arrange
        const string Source = "module m(n) { assert(n > 0, \"n must be positive\"); }\nm(0);";

        // Act
        var ex = Fails(Source);

        // Assert
        Assert.Contains("Assertion failed: n must be positive", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, ex.Location.Line);
    }
}

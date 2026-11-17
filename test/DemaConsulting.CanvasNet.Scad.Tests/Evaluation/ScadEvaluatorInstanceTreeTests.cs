using DemaConsulting.CanvasNet.Scad.Evaluation;
using static DemaConsulting.CanvasNet.Scad.Tests.Evaluation.ScadEvaluatorTestSupport;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Tests of the shape of the instantiation tree produced by <see cref="ScadEvaluator"/>: built-in modules,
///     groups, splicing constructs, modifiers, fragment settings and counting.
/// </summary>
public class ScadEvaluatorInstanceTreeTests
{
    /// <summary>
    ///     Validates that a built-in module carries its evaluated arguments, names preserved, and its children.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="expected">The expected dump.</param>
    [Theory]
    [InlineData("translate([1, 0, 0]) cube(5);", "translate([1, 0, 0]){cube(5)}")]
    [InlineData("a = 2; cube(a * 3);", "cube(6)")]
    [InlineData("cube(size = [1, 2, 3], center = true);", "cube(size=[1, 2, 3], center=true)")]
    [InlineData("difference() { cube(10); sphere(r = 6); }", "difference(){cube(10);sphere(r=6)}")]
    [InlineData("import(file = \"model.stl\");", "import(file=\"model.stl\")")]
    [InlineData("text(\"hi\", size = 3);", "text(\"hi\", size=3)")]
    [InlineData("color(\"red\") { cube(1); cube(2); }", "color(\"red\"){cube(1);cube(2)}")]
    [InlineData("cube(1); sphere(2);", "cube(1);sphere(2)")]
    [InlineData("rotate(a = 90, v = [0, 0, 1]) cylinder(h = 1, r = 2);", "rotate(a=90, v=[0, 0, 1]){cylinder(h=1, r=2)}")]
    public void ScadEvaluatorInstanceTree_BuiltinModules_CarryEvaluatedArgumentsAndChildren(string source, string expected)
    {
        // Arrange / Act
        var tree = Tree(source);

        // Assert
        Assert.Equal(expected, tree);
    }

    /// <summary>
    ///     Validates that every recognized built-in module name produces a built-in instance.
    /// </summary>
    /// <param name="name">The module name.</param>
    [Theory]
    [InlineData("cube")]
    [InlineData("sphere")]
    [InlineData("cylinder")]
    [InlineData("polyhedron")]
    [InlineData("square")]
    [InlineData("circle")]
    [InlineData("polygon")]
    [InlineData("text")]
    [InlineData("translate")]
    [InlineData("rotate")]
    [InlineData("scale")]
    [InlineData("mirror")]
    [InlineData("multmatrix")]
    [InlineData("resize")]
    [InlineData("color")]
    [InlineData("union")]
    [InlineData("difference")]
    [InlineData("intersection")]
    [InlineData("linear_extrude")]
    [InlineData("rotate_extrude")]
    [InlineData("hull")]
    [InlineData("minkowski")]
    [InlineData("offset")]
    [InlineData("projection")]
    [InlineData("render")]
    [InlineData("surface")]
    [InlineData("import")]
    [InlineData("roof")]
    public void ScadEvaluatorInstanceTree_RecognizedBuiltinName_ProducesBuiltinInstance(string name)
    {
        // Arrange / Act
        var output = Run($"{name}();");

        // Assert
        var instance = Assert.Single(output.Roots);
        Assert.Equal(ScadInstanceKind.BuiltinModule, instance.Kind);
        Assert.Equal(name, instance.Name);
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates how modifiers appear in the tree.
    /// </summary>
    /// <param name="source">The program.</param>
    /// <param name="expected">The expected dump.</param>
    [Theory]
    [InlineData("#cube(1);", "#cube(1)")]
    [InlineData("!sphere(2);", "!sphere(2)")]
    [InlineData("%cylinder(1);", "%cylinder(1)")]
    [InlineData("#%cube(1);", "#%cube(1)")]
    [InlineData("*cube(1);", "")]
    [InlineData("*#cube(1);", "")]
    [InlineData("cube(1); *sphere(2); cube(3);", "cube(1);cube(3)")]
    [InlineData("#{ cube(1); cube(2); }", "#group<group>{cube(1);cube(2)}")]
    [InlineData("#if (true) cube(1);", "#cube(1)")]
    [InlineData("#if (false) cube(1);", "")]
    [InlineData("%translate([1, 0, 0]) cube(1);", "%translate([1, 0, 0]){cube(1)}")]
    [InlineData("#for (i = [0 : 1]) cube(i);", "#group<for>{cube(0);cube(1)}")]
    [InlineData("translate([1, 0, 0]) !cube(1);", "translate([1, 0, 0]){!cube(1)}")]
    public void ScadEvaluatorInstanceTree_Modifiers_AreMergedOntoInstances(string source, string expected)
    {
        // Arrange / Act
        var output = Run(source);

        // Assert
        Assert.Equal(expected, ScadInstanceDumper.Dump(output.Roots));
        Assert.Empty(output.Diagnostics);
    }

    /// <summary>
    ///     Validates that the disable modifier prunes the statement before evaluation: no side effect, no loop, no failure.
    /// </summary>
    [Fact]
    public void ScadEvaluatorInstanceTree_DisableModifier_IsNeverEvaluated()
    {
        // Arrange
        const string Source = "*for (i = [0 : 100000]) cube(i);\n*echo(\"never\") cube(1);\n*{ assert(false); }\ncube(1);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Empty(output.Echo);
        Assert.Equal("cube(1)", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal(0, output.LoopIterations);
        Assert.True(output.Steps < 40);
        Assert.Equal(1, output.InstanceCount);
    }

    /// <summary>
    ///     Validates that <c>if</c>, braces and <c>let</c> splice into the parent without a node of their own.
    /// </summary>
    [Fact]
    public void ScadEvaluatorInstanceTree_SplicingConstructs_HaveNoNodeOfTheirOwn()
    {
        // Arrange
        const string Source = "union() { if (true) { cube(1); cube(2); } { sphere(3); } let(a = 4) cylinder(a); }";

        // Act
        var tree = Tree(Source);

        // Assert
        Assert.Equal("union(){cube(1);cube(2);sphere(3);cylinder(4)}", tree);
    }

    /// <summary>
    ///     Validates the lookup order: a user module shadows a built-in, an unknown name warns and produces nothing.
    /// </summary>
    [Fact]
    public void ScadEvaluatorInstanceTree_LookupOrder_UserBeforeBuiltinBeforeUnknown()
    {
        // Arrange
        const string Source = "module sphere(r) { cube(r); }\nsphere(1);\ncylinder(1);\nunknown_thing(1);";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal("group<sphere>{cube(1)};cylinder(1)", ScadInstanceDumper.Dump(output.Roots));
        Assert.Equal("Ignoring unknown module 'unknown_thing'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates that a statement form of <c>each</c> is just an unknown module.
    /// </summary>
    [Fact]
    public void ScadEvaluatorInstanceTree_EachStatement_IsAnUnknownModule()
    {
        // Arrange / Act
        var output = Run("each([1]);");

        // Assert
        Assert.Empty(output.Roots);
        Assert.Equal("Ignoring unknown module 'each'", Assert.Single(output.Diagnostics).Message);
    }

    /// <summary>
    ///     Validates the fragment settings: defaults, inheritance from the dynamic variables, and a named argument override.
    /// </summary>
    [Fact]
    public void ScadEvaluatorInstanceTree_Fragments_DefaultInheritAndOverride()
    {
        // Arrange
        const string Source = "cube(1);\n$fn = 30;\nsphere(1);\nsphere(2, $fa = 4, $fs = 0.5);\nsphere(3, $fn = \"x\");";

        // Act
        var roots = Run(Source).Roots;

        // Assert
        Assert.Equal(new ScadFragmentSettings(30, 12, 2), roots[0].Fragments);
        Assert.Equal(new ScadFragmentSettings(30, 12, 2), roots[1].Fragments);
        Assert.Equal(new ScadFragmentSettings(30, 4, 0.5), roots[2].Fragments);
        Assert.Equal(new ScadFragmentSettings(30, 12, 2), roots[3].Fragments);
        Assert.Equal("$fa", roots[2].Arguments[1].Name);
    }

    /// <summary>
    ///     Validates the source locations of instances: the module name token.
    /// </summary>
    [Fact]
    public void ScadEvaluatorInstanceTree_Location_IsTheModuleNameToken()
    {
        // Arrange
        const string Source = "\n  cube(1);\nfor (i = [0 : 1]) {\n    sphere(i);\n}";

        // Act
        var roots = Run(Source).Roots;

        // Assert
        Assert.Equal((2, 3), (roots[0].Location.Line, roots[0].Location.Column));
        Assert.Equal(3, roots[1].Location.Line);
        Assert.Equal((4, 5), (roots[1].Children[0].Location.Line, roots[1].Children[0].Location.Column));
    }

    /// <summary>
    ///     Validates that every instance, groups included, is counted.
    /// </summary>
    [Fact]
    public void ScadEvaluatorInstanceTree_InstanceCount_CountsEveryNode()
    {
        // Arrange
        const string Source = "translate([0, 0, 0]) { cube(1); cube(2); }\nfor (i = [0 : 1]) sphere(1);\nmodule m() { cube(1); }\nm();";

        // Act
        var output = Run(Source);

        // Assert
        Assert.Equal(3 + 3 + 2, output.InstanceCount);
    }

    /// <summary>
    ///     Validates that the evaluated tree is not affected by later evaluations: results are independent per call.
    /// </summary>
    [Fact]
    public void ScadEvaluatorInstanceTree_TwoEvaluations_ProduceEqualDumps()
    {
        // Arrange
        const string Source = "for (i = [0 : 2]) translate([i, 0, 0]) cube(1);";

        // Act
        var first = Tree(Source);
        var second = Tree(Source);

        // Assert
        Assert.Equal(first, second);
    }
}

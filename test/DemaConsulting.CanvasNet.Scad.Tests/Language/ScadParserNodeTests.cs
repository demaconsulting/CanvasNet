using System.Reflection;
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadParser"/> node locations and the immutability of the produced tree.
/// </summary>
public class ScadParserNodeTests
{
    /// <summary>
    ///     Builds a location without a file name.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="column">The column.</param>
    /// <returns>The location.</returns>
    private static ScadSourceLocation At(int line, int column) => new(null, line, column);

    /// <summary>
    ///     Validates statement locations: first modifier, keyword or name token.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Locations_Statements_PointAtIdentifyingToken()
    {
        // Arrange
        var source = "x = 1;\n  #cube(1);\nmodule m() {}\n{ a(); }\nif (a) b();\ninclude <p>\nfunction f() = 1;";

        // Act
        var statements = ScadParserTestSupport.Parse(source).Statements;

        // Assert
        Assert.Equal(At(1, 1), statements[0].Location);
        var modified = Assert.IsType<ScadModifiedStatement>(statements[1]);
        Assert.Equal(At(2, 3), modified.Location);
        Assert.Equal(At(2, 4), modified.Statement.Location);
        Assert.Equal(At(3, 1), statements[2].Location);
        Assert.Equal(At(4, 1), statements[3].Location);
        Assert.Equal(At(5, 1), statements[4].Location);
        Assert.Equal(At(6, 1), statements[5].Location);
        Assert.Equal(At(7, 1), statements[6].Location);
    }

    /// <summary>
    ///     Validates expression locations: operator, bracket or keyword tokens.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Locations_Expressions_PointAtIdentifyingToken()
    {
        // Arrange: "x = -a + b[0].y;" places the operators at known columns
        var binary = Assert.IsType<ScadBinaryExpression>(ScadParserTestSupport.ExprNode("-a + b[0].y"));
        var unary = Assert.IsType<ScadUnaryExpression>(binary.Left);
        var member = Assert.IsType<ScadMemberExpression>(binary.Right);
        var index = Assert.IsType<ScadIndexExpression>(member.Target);

        // Act / Assert: columns include the "x = " prefix
        Assert.Equal(At(1, 8), binary.Location);
        Assert.Equal(At(1, 5), unary.Location);
        Assert.Equal(At(1, 6), unary.Operand.Location);
        Assert.Equal(At(1, 14), member.Location);
        Assert.Equal(At(1, 11), index.Location);
        Assert.Equal(At(1, 10), index.Target.Location);
    }

    /// <summary>
    ///     Validates the locations of vectors, ranges, conditionals, calls and prefix forms.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Locations_Expressions_BracketAndKeywordForms()
    {
        // Arrange / Act / Assert: each expression starts at column 5 after "x = "
        Assert.Equal(At(1, 5), ScadParserTestSupport.ExprNode("[1,2]").Location);
        Assert.Equal(At(1, 5), ScadParserTestSupport.ExprNode("[1:2]").Location);
        Assert.Equal(At(1, 7), ScadParserTestSupport.ExprNode("a ? b : c").Location);
        Assert.Equal(At(1, 6), ScadParserTestSupport.ExprNode("f(1)").Location);
        Assert.Equal(At(1, 5), ScadParserTestSupport.ExprNode("function(x) x").Location);
        Assert.Equal(At(1, 5), ScadParserTestSupport.ExprNode("let(a=1) a").Location);
        Assert.Equal(At(1, 5), ScadParserTestSupport.ExprNode("echo(1) 2").Location);
        Assert.Equal(At(1, 5), ScadParserTestSupport.ExprNode("assert(1) 2").Location);
        Assert.Equal(At(1, 7), ScadParserTestSupport.ExprNode("2 ^ 3").Location);
        var vector = Assert.IsType<ScadVectorExpression>(ScadParserTestSupport.ExprNode("[for (i=a) i]"));
        Assert.Equal(At(1, 6), vector.Elements[0].Location);
    }

    /// <summary>
    ///     Validates that parts (parameters, arguments, assignments) are located at their first token.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Locations_Parts_PointAtFirstToken()
    {
        // Arrange
        var program = ScadParserTestSupport.Parse("module m(a, b = 1) { f(2, n = 3); }\nfor (i = [0:1]) g();");

        // Act
        var module = Assert.IsType<ScadModuleDefinition>(program.Statements[0]);
        var call = Assert.IsType<ScadModuleInstantiation>(module.Body[0]);
        var loop = Assert.IsType<ScadForStatement>(program.Statements[1]);

        // Assert
        Assert.Equal(At(1, 10), module.Parameters[0].Location);
        Assert.Equal(At(1, 13), module.Parameters[1].Location);
        Assert.Equal(At(1, 24), call.Arguments[0].Location);
        Assert.Equal(At(1, 27), call.Arguments[1].Location);
        Assert.Equal(At(2, 6), loop.Iterators[0].Location);
    }

    /// <summary>
    ///     Validates that the file name is propagated to every node location.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Locations_FileName_Propagated()
    {
        // Arrange / Act
        var program = ScadParserTestSupport.Parse("a = f(1) + [2];\nb();", "dir/p.scad");
        var assignment = Assert.IsType<ScadAssignmentStatement>(program.Statements[0]);
        var binary = Assert.IsType<ScadBinaryExpression>(assignment.Value);

        // Assert
        Assert.Equal(new ScadSourceLocation("dir/p.scad", 1, 1), program.Location);
        Assert.Equal("dir/p.scad", assignment.Location.File);
        Assert.Equal("dir/p.scad", binary.Location.File);
        Assert.Equal("dir/p.scad", binary.Left.Location.File);
        Assert.Equal("dir/p.scad", program.Statements[1].Location.File);
    }

    /// <summary>
    ///     Validates that every collection in the tree is read-only.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Result_Collections_AreReadOnly()
    {
        // Arrange
        var program = ScadParserTestSupport.Parse("module m(a) { x(1) { y(); } }\nv = [1, 2];");
        var module = Assert.IsType<ScadModuleDefinition>(program.Statements[0]);
        var call = Assert.IsType<ScadModuleInstantiation>(module.Body[0]);
        var vector = Assert.IsType<ScadVectorExpression>(Assert.IsType<ScadAssignmentStatement>(program.Statements[1]).Value);

        // Act / Assert: each list is read-only through the mutable interface
        Assert.True(((ICollection<ScadStatement>)program.Statements).IsReadOnly);
        Assert.True(((ICollection<ScadParameter>)module.Parameters).IsReadOnly);
        Assert.True(((ICollection<ScadStatement>)module.Body).IsReadOnly);
        Assert.True(((ICollection<ScadArgument>)call.Arguments).IsReadOnly);
        Assert.True(((ICollection<ScadStatement>)call.Children).IsReadOnly);
        Assert.True(((ICollection<ScadExpression>)vector.Elements).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ((ICollection<ScadStatement>)program.Statements).Clear());
    }

    /// <summary>
    ///     Validates by reflection that no node type has a public setter other than an init accessor.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Result_Nodes_AreImmutableRecords()
    {
        // Arrange: every concrete or abstract node type of the language assembly
        var nodeTypes = typeof(ScadParser).Assembly.GetTypes()
            .Where(t => typeof(ScadNode).IsAssignableFrom(t))
            .ToList();

        // Act: collect any property whose setter is not init-only
        var mutable = nodeTypes
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(p => p.SetMethod is not null
                && !p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        // Assert: all node types were found, are records (they have a compiler-generated clone), and are immutable
        Assert.True(nodeTypes.Count > 40);
        Assert.Empty(mutable);
        Assert.All(
            nodeTypes,
            t => Assert.NotNull(t.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)));
    }
}

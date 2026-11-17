using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Unit tests for <see cref="ScadParser"/> statements: assignments, definitions, instantiations, modifiers
///     and directives.
/// </summary>
public class ScadParserStatementTests
{
    /// <summary>
    ///     Validates a file-scope assignment.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Assignment_FileScope_ProducesAssignmentStatement()
    {
        // Arrange / Act
        var dump = ScadParserTestSupport.Stmts("x = 5;");

        // Assert
        Assert.Equal("(= x 5)", dump);
    }

    /// <summary>
    ///     Validates assignment to a special variable.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_SpecialVariableAssignment_ProducesAssignmentStatement()
    {
        // Arrange / Act / Assert
        Assert.Equal("(= $fn 64)", ScadParserTestSupport.Stmts("$fn = 64;"));
    }

    /// <summary>
    ///     Validates assignments inside a block, and that empty statements produce no nodes.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_AssignmentInBlock_ProducesBlockOfAssignments()
    {
        // Arrange / Act / Assert
        Assert.Equal("(block (= a 1) (= b a))", ScadParserTestSupport.Stmts("{ a = 1; ; b = a; }"));
        Assert.Equal(string.Empty, ScadParserTestSupport.Stmts(";;"));
    }

    /// <summary>
    ///     Validates module definitions with default parameters.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_ModuleDefinition_WithDefaultParameters_ProducesModuleDefinition()
    {
        // Arrange / Act
        var dump = ScadParserTestSupport.Stmts("module m(a, b = 2, $fn = 8) { cube(a); }");

        // Assert
        Assert.Equal("(module m (a (= b 2) (= $fn 8)) (inst cube (a)))", dump);
    }

    /// <summary>
    ///     Validates function definitions.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_FunctionDefinition_ProducesFunctionDefinition()
    {
        // Arrange / Act / Assert
        Assert.Equal("(function f (x (= y 1)) (+ x y))", ScadParserTestSupport.Stmts("function f(x, y = 1) = x + y;"));
        Assert.Equal("(function g () 3)", ScadParserTestSupport.Stmts("function g() = 3;"));
    }

    /// <summary>
    ///     Validates nested definitions inside braces.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_NestedDefinitionInBlock_ProducesNestedDefinitions()
    {
        // Arrange / Act
        var dump = ScadParserTestSupport.Stmts("module outer() { module inner() { } function g() = 1; }");

        // Assert
        Assert.Equal("(module outer () (module inner ()) (function g () 1))", dump);
    }

    /// <summary>
    ///     Validates an unbraced module body and an empty module body.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_ModuleWithSingleStatementBody_ProducesSingleChildBody()
    {
        // Arrange / Act / Assert
        Assert.Equal("(module m () (inst cube (1)))", ScadParserTestSupport.Stmts("module m() cube(1);"));
        Assert.Equal("(module m ())", ScadParserTestSupport.Stmts("module m();"));
    }

    /// <summary>
    ///     Validates a trailing comma in parameter lists.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_ParameterTrailingComma_IsAccepted()
    {
        // Arrange / Act / Assert
        Assert.Equal("(module m (a b))", ScadParserTestSupport.Stmts("module m(a, b,);"));
        Assert.Equal("(function f (a) a)", ScadParserTestSupport.Stmts("function f(a,) = a;"));
    }

    /// <summary>
    ///     Validates positional and named arguments.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Instantiation_PositionalAndNamedArguments_ProducesArguments()
    {
        // Arrange / Act
        var dump = ScadParserTestSupport.Stmts("cube([1, 2, 3], center = true);");

        // Assert
        Assert.Equal("(inst cube ((vec 1 2 3) (arg center true)))", dump);
    }

    /// <summary>
    ///     Validates that a trailing semicolon means no children.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Instantiation_SemicolonOnly_HasNoChildren()
    {
        // Arrange / Act
        var program = ScadParserTestSupport.Parse("sphere(1);");

        // Assert: the instantiation has no children
        var inst = Assert.IsType<ScadModuleInstantiation>(Assert.Single(program.Statements));
        Assert.Empty(inst.Children);
        Assert.Equal("sphere", inst.Name);
    }

    /// <summary>
    ///     Validates a single unbraced child.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Instantiation_SingleChild_ProducesNestedInstantiation()
    {
        // Arrange / Act / Assert
        Assert.Equal(
            "(inst translate ((vec 1 0 0)) (inst cube (1)))",
            ScadParserTestSupport.Stmts("translate([1,0,0]) cube(1);"));
    }

    /// <summary>
    ///     Validates braced children, which are flattened without an extra node.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Instantiation_BlockChildren_AreFlattened()
    {
        // Arrange / Act / Assert
        Assert.Equal("(inst union () (inst cube (1)) (inst sphere (2)))", ScadParserTestSupport.Stmts("union() { cube(1); sphere(2); }"));
        Assert.Equal("(inst a ())", ScadParserTestSupport.Stmts("a() {}"));
        Assert.Equal("(inst a () (block (inst b ())))", ScadParserTestSupport.Stmts("a() { { b(); } }"));
    }

    /// <summary>
    ///     Validates a chain of single children.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Instantiation_ChainOfSingleChildren_NestsRightward()
    {
        // Arrange / Act / Assert
        Assert.Equal("(inst a () (inst b () (inst c ())))", ScadParserTestSupport.Stmts("a() b() c();"));
    }

    /// <summary>
    ///     Validates that names are not interpreted by the parser.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Instantiation_UnknownName_Parses()
    {
        // Arrange / Act / Assert
        Assert.Equal("(inst frobnicate (1 (arg x 2)))", ScadParserTestSupport.Stmts("frobnicate(1, x=2);"));
    }

    /// <summary>
    ///     Validates a trailing comma in argument lists and the distinction between named arguments and equality.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Instantiation_TrailingCommaAndEquality_AreDistinguished()
    {
        // Arrange / Act / Assert
        Assert.Equal("(inst f (1 2))", ScadParserTestSupport.Stmts("f(1, 2,);"));
        Assert.Equal("(inst f ((== a 1)))", ScadParserTestSupport.Stmts("f(a == 1);"));
        Assert.Equal("(inst f ((arg a 1)))", ScadParserTestSupport.Stmts("f(a = 1);"));
    }

    /// <summary>
    ///     Validates each modifier character.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="expected">The expected dump.</param>
    [Theory]
    [InlineData("*cube(1);", "(mod * (inst cube (1)))")]
    [InlineData("!cube(1);", "(mod ! (inst cube (1)))")]
    [InlineData("#cube(1);", "(mod # (inst cube (1)))")]
    [InlineData("%cube(1);", "(mod % (inst cube (1)))")]
    public void ScadParser_Parse_Modifier_Each_ProducesModifiedStatement(string source, string expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, ScadParserTestSupport.Stmts(source));
    }

    /// <summary>
    ///     Validates that combined modifiers accumulate flags on a single node.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Modifiers_Combined_AccumulateFlags()
    {
        // Arrange / Act
        var program = ScadParserTestSupport.Parse("#!cube(1);");

        // Assert
        var modified = Assert.IsType<ScadModifiedStatement>(Assert.Single(program.Statements));
        Assert.Equal(ScadModifiers.Highlight | ScadModifiers.ShowOnly, modified.Modifiers);
        Assert.IsType<ScadModuleInstantiation>(modified.Statement);
        Assert.Equal("(mod !# (inst cube (1)))", ScadParserTestSupport.Stmts("#!cube(1);"));
    }

    /// <summary>
    ///     Validates modifiers on loops, conditionals, let statements, blocks and child positions.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Modifier_OnForIfLetBlock_ProducesModifiedStatements()
    {
        // Arrange / Act / Assert
        Assert.Equal("(mod * (for ((i (range 0 1))) (inst a ())))", ScadParserTestSupport.Stmts("*for (i=[0:1]) a();"));
        Assert.Equal("(mod # (if x (then (inst a ()))))", ScadParserTestSupport.Stmts("#if (x) a();"));
        Assert.Equal("(mod % (let ((a 1)) (inst b ())))", ScadParserTestSupport.Stmts("%let (a=1) b();"));
        Assert.Equal("(mod ! (block (inst a ())))", ScadParserTestSupport.Stmts("!{ a(); }"));
        Assert.Equal("(inst t () (mod # (inst c ())))", ScadParserTestSupport.Stmts("t() #c();"));
        Assert.Equal("(inst t () (mod # (block (inst c ()))))", ScadParserTestSupport.Stmts("t() #{ c(); }"));
    }

    /// <summary>
    ///     Validates that a modifier before anything but an instantiation or block is rejected at that token.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="column">The expected column.</param>
    [Theory]
    [InlineData("#x = 5;", 2)]
    [InlineData("*;", 2)]
    [InlineData("#module m() {}", 2)]
    [InlineData("%function f() = 1;", 2)]
    [InlineData("#", 2)]
    [InlineData("a() *x = 1;", 6)]
    public void ScadParser_Parse_Modifier_BeforeAssignment_Throws(string source, int column)
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError(source, 1, column, "A modifier must be followed by a module instantiation or block");
    }

    /// <summary>
    ///     Validates file-scope directives in source order.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Directive_FileScope_RecordedInOrder()
    {
        // Arrange / Act
        var program = ScadParserTestSupport.Parse("include <a.scad>\nuse <b/c.scad>;\nx = 1;");

        // Assert: the stray ';' after the directive is an empty statement
        Assert.Equal("(include \"a.scad\") (use \"b/c.scad\") (= x 1)", ScadAstDumper.DumpStatements(program));
        var first = Assert.IsType<ScadDirectiveStatement>(program.Statements[0]);
        Assert.Equal(ScadDirectiveKind.Include, first.Kind);
        Assert.Equal("a.scad", first.Path);
    }

    /// <summary>
    ///     Validates that directives are rejected anywhere but file scope.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="column">The expected column.</param>
    [Theory]
    [InlineData("{ include <a.scad> }", 1, 3)]
    [InlineData("module m() {\n  use <a.scad>\n}", 2, 3)]
    [InlineData("if (x) {\n    include <a>\n}", 2, 5)]
    public void ScadParser_Parse_Directive_InsideBlockOrModule_Throws(string source, int line, int column)
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError(source, line, column, "include/use is only allowed at file scope");
    }

    /// <summary>
    ///     Validates that a directive in an unbraced child position is rejected.
    /// </summary>
    [Fact]
    public void ScadParser_Parse_Directive_AsSingleChild_Throws()
    {
        // Arrange / Act / Assert
        ScadParserTestSupport.AssertParseError("a() include <x>", 1, 5, "Expected ';', '{' or a module instantiation but found 'include <x>'");
        ScadParserTestSupport.AssertParseError("a() use <y>", 1, 5, "Expected ';', '{' or a module instantiation but found 'use <y>'");
    }
}

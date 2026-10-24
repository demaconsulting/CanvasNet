using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Helpers shared by the <see cref="ScadParser"/> unit tests.
/// </summary>
internal static class ScadParserTestSupport
{
    /// <summary>
    ///     Parses a source text.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="file">The optional source name.</param>
    /// <returns>The program.</returns>
    public static ScadProgram Parse(string source, string? file = null) => ScadParser.Parse(source, file);

    /// <summary>
    ///     Parses a source text and dumps its statements.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The statements dump, separated by spaces.</returns>
    public static string Stmts(string source) => ScadAstDumper.DumpStatements(Parse(source));

    /// <summary>
    ///     Parses an expression (as the value of an assignment) and returns its node.
    /// </summary>
    /// <param name="expression">The expression source.</param>
    /// <returns>The expression node.</returns>
    public static ScadExpression ExprNode(string expression) =>
        ((ScadAssignmentStatement)Parse($"x = {expression};").Statements[0]).Value;

    /// <summary>
    ///     Parses an expression and dumps it.
    /// </summary>
    /// <param name="expression">The expression source.</param>
    /// <returns>The dump.</returns>
    public static string Expr(string expression) => ScadAstDumper.DumpExpression(ExprNode(expression));

    /// <summary>
    ///     Asserts that parsing fails with a syntax error at an exact location.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="column">The expected column.</param>
    /// <param name="fragment">Text the message must contain.</param>
    /// <returns>The thrown exception.</returns>
    public static ScadSyntaxException AssertParseError(string source, int line, int column, string fragment) =>
        ScadTestHelpers.AssertSyntaxError(() => Parse(source), line, column, fragment);

    /// <summary>
    ///     Builds a string by repeating a unit.
    /// </summary>
    /// <param name="unit">The text to repeat.</param>
    /// <param name="count">The repeat count.</param>
    /// <returns>The repeated text.</returns>
    public static string Repeat(string unit, int count) => string.Concat(Enumerable.Repeat(unit, count));
}

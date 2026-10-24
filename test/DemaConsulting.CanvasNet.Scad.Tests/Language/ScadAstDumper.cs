using System.Globalization;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Language;

/// <summary>
///     Test helper that renders a syntax tree as a compact, location-free S-expression so that parser tests
///     assert on readable strings instead of record equality.
/// </summary>
/// <remarks>
///     Examples: <c>1 + 2 * 3</c> is <c>(+ 1 (* 2 3))</c>; <c>for (i=[0:3]) cube(i);</c> is
///     <c>(for ((i (range 0 3))) (inst cube (i)))</c>. Positional arguments dump as their expression and named
///     arguments as <c>(arg name value)</c>; a parameter with a default dumps as <c>(= name default)</c>.
///     The dumper recurses, which is safe because the parser bounds tree depth.
/// </remarks>
internal static class ScadAstDumper
{
    /// <summary>
    ///     Dumps a whole program.
    /// </summary>
    /// <param name="program">The program.</param>
    /// <returns>The <c>(program ...)</c> text.</returns>
    public static string Dump(ScadProgram program) => $"(program{Prefixed(program.Statements.Select(DumpStatement))})";

    /// <summary>
    ///     Dumps the statements of a program separated by spaces, without the program wrapper.
    /// </summary>
    /// <param name="program">The program.</param>
    /// <returns>The statements text.</returns>
    public static string DumpStatements(ScadProgram program) => string.Join(' ', program.Statements.Select(DumpStatement));

    /// <summary>
    ///     Dumps one statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The text.</returns>
    public static string DumpStatement(ScadStatement statement) => statement switch
    {
        ScadAssignmentStatement s => $"(= {s.Name} {DumpExpression(s.Value)})",
        ScadModuleDefinition s => $"(module {s.Name} {Parameters(s.Parameters)}{Prefixed(s.Body.Select(DumpStatement))})",
        ScadFunctionDefinition s => $"(function {s.Name} {Parameters(s.Parameters)} {DumpExpression(s.Body)})",
        ScadModuleInstantiation s => $"(inst {s.Name} {Arguments(s.Arguments)}{Prefixed(s.Children.Select(DumpStatement))})",
        ScadBlockStatement s => $"(block{Prefixed(s.Statements.Select(DumpStatement))})",
        ScadModifiedStatement s => $"(mod {ModifierText(s.Modifiers)} {DumpStatement(s.Statement)})",
        ScadDirectiveStatement s => $"({s.Kind.ToString().ToLowerInvariant()} \"{s.Path}\")",
        _ => ControlStatement(statement),
    };

    /// <summary>
    ///     Dumps the control-flow and keyword statements.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The text.</returns>
    private static string ControlStatement(ScadStatement statement) => statement switch
    {
        ScadIfStatement s => $"(if {DumpExpression(s.Condition)} (then{Prefixed(s.Then.Select(DumpStatement))})"
            + (s.Else is null ? string.Empty : $" (else{Prefixed(s.Else.Select(DumpStatement))})") + ")",
        ScadForStatement s => $"(for {Assignments(s.Iterators)}{Prefixed(s.Body.Select(DumpStatement))})",
        ScadIntersectionForStatement s => $"(ifor {Assignments(s.Iterators)}{Prefixed(s.Body.Select(DumpStatement))})",
        ScadLetStatement s => $"(let {Assignments(s.Assignments)}{Prefixed(s.Body.Select(DumpStatement))})",
        ScadEchoStatement s => $"(echo {Arguments(s.Arguments)}{Prefixed(s.Children.Select(DumpStatement))})",
        ScadAssertStatement s => $"(assert {Arguments(s.Arguments)}{Prefixed(s.Children.Select(DumpStatement))})",
        _ => throw new InvalidOperationException($"Unknown statement {statement.GetType().Name}"),
    };

    /// <summary>
    ///     Dumps one expression.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    public static string DumpExpression(ScadExpression expression) => expression switch
    {
        ScadNumberLiteral e => Number(e.Value),
        ScadStringLiteral e => $"\"{e.Value}\"",
        ScadBooleanLiteral e => e.Value ? "true" : "false",
        ScadUndefLiteral => "undef",
        ScadIdentifierExpression e => e.Name,
        ScadVectorExpression e => $"(vec{Prefixed(e.Elements.Select(DumpExpression))})",
        ScadRangeExpression e => e.Step is null
            ? $"(range {DumpExpression(e.Start)} {DumpExpression(e.End)})"
            : $"(range {DumpExpression(e.Start)} {DumpExpression(e.Step)} {DumpExpression(e.End)})",
        ScadUnaryExpression e => $"({UnarySymbol(e.Operator)} {DumpExpression(e.Operand)})",
        ScadBinaryExpression e => $"({BinarySymbol(e.Operator)} {DumpExpression(e.Left)} {DumpExpression(e.Right)})",
        ScadConditionalExpression e => $"(?: {DumpExpression(e.Condition)} {DumpExpression(e.WhenTrue)} {DumpExpression(e.WhenFalse)})",
        ScadIndexExpression e => $"(index {DumpExpression(e.Target)} {DumpExpression(e.Index)})",
        ScadMemberExpression e => $"(member {DumpExpression(e.Target)} {e.Member})",
        ScadCallExpression e => $"(call {DumpExpression(e.Callee)}{Prefixed(e.Arguments.Select(Argument))})",
        _ => PrefixOrComprehension(expression),
    };

    /// <summary>
    ///     Dumps the keyword-introduced expression forms and list comprehension elements.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>The text.</returns>
    private static string PrefixOrComprehension(ScadExpression expression) => expression switch
    {
        ScadFunctionLiteral e => $"(fn {Parameters(e.Parameters)} {DumpExpression(e.Body)})",
        ScadLetExpression e => $"(let {Assignments(e.Assignments)} {DumpExpression(e.Body)})",
        ScadEchoExpression e => $"(echo {Arguments(e.Arguments)} {DumpExpression(e.Body)})",
        ScadAssertExpression e => $"(assert {Arguments(e.Arguments)} {DumpExpression(e.Body)})",
        ScadListForExpression e => $"(lfor {Assignments(e.Iterators)} {DumpExpression(e.Body)})",
        ScadListCForExpression e => $"(lcfor {Assignments(e.Initializers)} {DumpExpression(e.Condition)} {Assignments(e.Increments)} {DumpExpression(e.Body)})",
        ScadListIfExpression e => $"(lif {DumpExpression(e.Condition)} {DumpExpression(e.WhenTrue)}"
            + (e.WhenFalse is null ? string.Empty : " " + DumpExpression(e.WhenFalse)) + ")",
        ScadListLetExpression e => $"(llet {Assignments(e.Assignments)} {DumpExpression(e.Body)})",
        ScadListEachExpression e => $"(each {DumpExpression(e.Value)})",
        _ => throw new InvalidOperationException($"Unknown expression {expression.GetType().Name}"),
    };

    /// <summary>
    ///     Joins items with a leading space each.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The text, empty when there are no items.</returns>
    private static string Prefixed(IEnumerable<string> items) => string.Concat(items.Select(i => " " + i));

    /// <summary>
    ///     Dumps a number without trailing zeros using the invariant culture.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    private static string Number(double value) => double.IsPositiveInfinity(value)
        ? "inf"
        : value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Dumps a parameter list as <c>(a (= b 1))</c>.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The text.</returns>
    private static string Parameters(IEnumerable<ScadParameter> parameters) =>
        "(" + string.Join(' ', parameters.Select(p => p.Default is null ? p.Name : $"(= {p.Name} {DumpExpression(p.Default)})")) + ")";

    /// <summary>
    ///     Dumps an argument list as <c>(1 (arg n 2))</c>.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The text.</returns>
    private static string Arguments(IEnumerable<ScadArgument> arguments) =>
        "(" + string.Join(' ', arguments.Select(Argument)) + ")";

    /// <summary>
    ///     Dumps one argument.
    /// </summary>
    /// <param name="argument">The argument.</param>
    /// <returns>The expression text, or <c>(arg name value)</c> for a named argument.</returns>
    private static string Argument(ScadArgument argument) => argument.Name is null
        ? DumpExpression(argument.Value)
        : $"(arg {argument.Name} {DumpExpression(argument.Value)})";

    /// <summary>
    ///     Dumps a binding list as <c>((i 1) (j 2))</c>.
    /// </summary>
    /// <param name="assignments">The bindings.</param>
    /// <returns>The text.</returns>
    private static string Assignments(IEnumerable<ScadAssignment> assignments) =>
        "(" + string.Join(' ', assignments.Select(a => $"({a.Name} {DumpExpression(a.Value)})")) + ")";

    /// <summary>
    ///     Renders modifier flags as characters in the fixed order <c>* ! # %</c>.
    /// </summary>
    /// <param name="modifiers">The modifiers.</param>
    /// <returns>The characters.</returns>
    private static string ModifierText(ScadModifiers modifiers) =>
        (modifiers.HasFlag(ScadModifiers.Disable) ? "*" : string.Empty)
        + (modifiers.HasFlag(ScadModifiers.ShowOnly) ? "!" : string.Empty)
        + (modifiers.HasFlag(ScadModifiers.Highlight) ? "#" : string.Empty)
        + (modifiers.HasFlag(ScadModifiers.Background) ? "%" : string.Empty);

    /// <summary>
    ///     Gets the dump symbol of a unary operator.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <returns>The symbol.</returns>
    private static string UnarySymbol(ScadUnaryOperator op) => op switch
    {
        ScadUnaryOperator.Plus => "pos",
        ScadUnaryOperator.Negate => "neg",
        _ => "!",
    };

    /// <summary>
    ///     Gets the dump symbol of a binary operator.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <returns>The symbol.</returns>
    private static string BinarySymbol(ScadBinaryOperator op) => op switch
    {
        ScadBinaryOperator.Add => "+",
        ScadBinaryOperator.Subtract => "-",
        ScadBinaryOperator.Multiply => "*",
        ScadBinaryOperator.Divide => "/",
        ScadBinaryOperator.Modulo => "%",
        ScadBinaryOperator.Power => "^",
        ScadBinaryOperator.Less => "<",
        ScadBinaryOperator.LessEqual => "<=",
        ScadBinaryOperator.Greater => ">",
        ScadBinaryOperator.GreaterEqual => ">=",
        ScadBinaryOperator.Equal => "==",
        ScadBinaryOperator.NotEqual => "!=",
        ScadBinaryOperator.And => "&&",
        _ => "||",
    };
}

namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     A variable assignment statement: <c>name = value;</c>.
/// </summary>
/// <param name="Name">The assigned name.</param>
/// <param name="Value">The value expression.</param>
internal sealed record ScadAssignmentStatement(string Name, ScadExpression Value) : ScadStatement;

/// <summary>
///     A module definition: <c>module name(params) body</c>.
/// </summary>
/// <param name="Name">The module name.</param>
/// <param name="Parameters">The formal parameters.</param>
/// <param name="Body">The body statements (a braced body is flattened; <c>;</c> is empty).</param>
internal sealed record ScadModuleDefinition(
    string Name,
    IReadOnlyList<ScadParameter> Parameters,
    IReadOnlyList<ScadStatement> Body) : ScadStatement;

/// <summary>
///     A function definition: <c>function name(params) = expression;</c>.
/// </summary>
/// <param name="Name">The function name.</param>
/// <param name="Parameters">The formal parameters.</param>
/// <param name="Body">The result expression.</param>
internal sealed record ScadFunctionDefinition(
    string Name,
    IReadOnlyList<ScadParameter> Parameters,
    ScadExpression Body) : ScadStatement;

/// <summary>
///     A module instantiation: <c>name(args) children</c>. The parser is name-agnostic, so built-in and
///     unknown modules look the same.
/// </summary>
/// <param name="Name">The module name.</param>
/// <param name="Arguments">The arguments.</param>
/// <param name="Children">The child statements (empty for a trailing <c>;</c>).</param>
internal sealed record ScadModuleInstantiation(
    string Name,
    IReadOnlyList<ScadArgument> Arguments,
    IReadOnlyList<ScadStatement> Children) : ScadStatement;

/// <summary>
///     A braced block that stands on its own as a statement.
/// </summary>
/// <param name="Statements">The statements inside the braces.</param>
internal sealed record ScadBlockStatement(IReadOnlyList<ScadStatement> Statements) : ScadStatement;

/// <summary>
///     A statement preceded by one or more modifier characters.
/// </summary>
/// <param name="Modifiers">The combined modifiers.</param>
/// <param name="Statement">The modified instantiation-like statement or block.</param>
internal sealed record ScadModifiedStatement(ScadModifiers Modifiers, ScadStatement Statement) : ScadStatement;

/// <summary>
///     An <c>if</c> statement with an optional <c>else</c>.
/// </summary>
/// <param name="Condition">The condition.</param>
/// <param name="Then">The statements of the true branch.</param>
/// <param name="Else">The statements of the false branch; null when there is no <c>else</c>, empty for <c>else ;</c>.</param>
internal sealed record ScadIfStatement(
    ScadExpression Condition,
    IReadOnlyList<ScadStatement> Then,
    IReadOnlyList<ScadStatement>? Else) : ScadStatement;

/// <summary>
///     A <c>for</c> loop statement.
/// </summary>
/// <param name="Iterators">The loop variable bindings (nested-loop semantics).</param>
/// <param name="Body">The loop body statements.</param>
internal sealed record ScadForStatement(
    IReadOnlyList<ScadAssignment> Iterators,
    IReadOnlyList<ScadStatement> Body) : ScadStatement;

/// <summary>
///     An <c>intersection_for</c> loop statement.
/// </summary>
/// <param name="Iterators">The loop variable bindings.</param>
/// <param name="Body">The loop body statements.</param>
internal sealed record ScadIntersectionForStatement(
    IReadOnlyList<ScadAssignment> Iterators,
    IReadOnlyList<ScadStatement> Body) : ScadStatement;

/// <summary>
///     A <c>let</c> statement that scopes assignments over its body.
/// </summary>
/// <param name="Assignments">The bindings.</param>
/// <param name="Body">The body statements.</param>
internal sealed record ScadLetStatement(
    IReadOnlyList<ScadAssignment> Assignments,
    IReadOnlyList<ScadStatement> Body) : ScadStatement;

/// <summary>
///     An <c>echo</c> statement.
/// </summary>
/// <param name="Arguments">The echoed arguments.</param>
/// <param name="Children">The child statements.</param>
internal sealed record ScadEchoStatement(
    IReadOnlyList<ScadArgument> Arguments,
    IReadOnlyList<ScadStatement> Children) : ScadStatement;

/// <summary>
///     An <c>assert</c> statement.
/// </summary>
/// <param name="Arguments">The condition and optional message arguments.</param>
/// <param name="Children">The child statements.</param>
internal sealed record ScadAssertStatement(
    IReadOnlyList<ScadArgument> Arguments,
    IReadOnlyList<ScadStatement> Children) : ScadStatement;

/// <summary>
///     A file-scope <c>include</c> or <c>use</c> directive; it is recorded but never resolved by the parser.
/// </summary>
/// <param name="Kind">The directive kind.</param>
/// <param name="Path">The text between the angle brackets.</param>
internal sealed record ScadDirectiveStatement(ScadDirectiveKind Kind, string Path) : ScadStatement;

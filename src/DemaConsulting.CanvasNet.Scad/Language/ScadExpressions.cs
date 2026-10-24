namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>A numeric literal.</summary>
/// <param name="Value">The value.</param>
internal sealed record ScadNumberLiteral(double Value) : ScadExpression;

/// <summary>A string literal.</summary>
/// <param name="Value">The decoded value.</param>
internal sealed record ScadStringLiteral(string Value) : ScadExpression;

/// <summary>A <c>true</c> or <c>false</c> literal.</summary>
/// <param name="Value">The value.</param>
internal sealed record ScadBooleanLiteral(bool Value) : ScadExpression;

/// <summary>The <c>undef</c> literal.</summary>
internal sealed record ScadUndefLiteral : ScadExpression;

/// <summary>A reference to a variable, function or special variable.</summary>
/// <param name="Name">The name.</param>
internal sealed record ScadIdentifierExpression(string Name) : ScadExpression;

/// <summary>A vector literal <c>[a, b, c]</c>; elements may be list comprehension elements.</summary>
/// <param name="Elements">The elements.</param>
internal sealed record ScadVectorExpression(IReadOnlyList<ScadExpression> Elements) : ScadExpression;

/// <summary>A range <c>[start : end]</c> or <c>[start : step : end]</c> (written order).</summary>
/// <param name="Start">The first value.</param>
/// <param name="Step">The increment, or null when omitted.</param>
/// <param name="End">The last value.</param>
internal sealed record ScadRangeExpression(ScadExpression Start, ScadExpression? Step, ScadExpression End) : ScadExpression;

/// <summary>A unary prefix expression.</summary>
/// <param name="Operator">The operator.</param>
/// <param name="Operand">The operand.</param>
internal sealed record ScadUnaryExpression(ScadUnaryOperator Operator, ScadExpression Operand) : ScadExpression;

/// <summary>A binary expression.</summary>
/// <param name="Operator">The operator.</param>
/// <param name="Left">The left operand.</param>
/// <param name="Right">The right operand.</param>
internal sealed record ScadBinaryExpression(ScadBinaryOperator Operator, ScadExpression Left, ScadExpression Right) : ScadExpression;

/// <summary>A conditional expression <c>condition ? whenTrue : whenFalse</c>.</summary>
/// <param name="Condition">The condition.</param>
/// <param name="WhenTrue">The value when true.</param>
/// <param name="WhenFalse">The value when false.</param>
internal sealed record ScadConditionalExpression(ScadExpression Condition, ScadExpression WhenTrue, ScadExpression WhenFalse) : ScadExpression;

/// <summary>An index expression <c>target[index]</c>.</summary>
/// <param name="Target">The indexed expression.</param>
/// <param name="Index">The index expression.</param>
internal sealed record ScadIndexExpression(ScadExpression Target, ScadExpression Index) : ScadExpression;

/// <summary>A member access <c>target.member</c>.</summary>
/// <param name="Target">The expression whose member is read.</param>
/// <param name="Member">The member name.</param>
internal sealed record ScadMemberExpression(ScadExpression Target, string Member) : ScadExpression;

/// <summary>A call <c>callee(arguments)</c> on any expression.</summary>
/// <param name="Callee">The called expression.</param>
/// <param name="Arguments">The arguments.</param>
internal sealed record ScadCallExpression(ScadExpression Callee, IReadOnlyList<ScadArgument> Arguments) : ScadExpression;

/// <summary>A function literal <c>function(params) body</c>.</summary>
/// <param name="Parameters">The formal parameters.</param>
/// <param name="Body">The result expression.</param>
internal sealed record ScadFunctionLiteral(IReadOnlyList<ScadParameter> Parameters, ScadExpression Body) : ScadExpression;

/// <summary>A <c>let</c> expression <c>let(assignments) body</c>.</summary>
/// <param name="Assignments">The bindings.</param>
/// <param name="Body">The body expression.</param>
internal sealed record ScadLetExpression(IReadOnlyList<ScadAssignment> Assignments, ScadExpression Body) : ScadExpression;

/// <summary>An <c>echo</c> expression <c>echo(args) body</c>.</summary>
/// <param name="Arguments">The echoed arguments.</param>
/// <param name="Body">The body expression.</param>
internal sealed record ScadEchoExpression(IReadOnlyList<ScadArgument> Arguments, ScadExpression Body) : ScadExpression;

/// <summary>An <c>assert</c> expression <c>assert(args) body</c>.</summary>
/// <param name="Arguments">The condition and optional message arguments.</param>
/// <param name="Body">The body expression.</param>
internal sealed record ScadAssertExpression(IReadOnlyList<ScadArgument> Arguments, ScadExpression Body) : ScadExpression;

/// <summary>A list comprehension element <c>for (iterators) body</c>.</summary>
/// <param name="Iterators">The loop variable bindings (nested-loop semantics).</param>
/// <param name="Body">The element produced per iteration.</param>
internal sealed record ScadListForExpression(IReadOnlyList<ScadAssignment> Iterators, ScadExpression Body) : ScadExpression;

/// <summary>A C-style list comprehension element <c>for (init; condition; increment) body</c>.</summary>
/// <param name="Initializers">The initial bindings (may be empty).</param>
/// <param name="Condition">The loop condition.</param>
/// <param name="Increments">The per-iteration bindings (may be empty).</param>
/// <param name="Body">The element produced per iteration.</param>
internal sealed record ScadListCForExpression(
    IReadOnlyList<ScadAssignment> Initializers,
    ScadExpression Condition,
    IReadOnlyList<ScadAssignment> Increments,
    ScadExpression Body) : ScadExpression;

/// <summary>A list comprehension element <c>if (condition) whenTrue [else whenFalse]</c>.</summary>
/// <param name="Condition">The condition.</param>
/// <param name="WhenTrue">The element when true.</param>
/// <param name="WhenFalse">The element when false, or null when there is no <c>else</c>.</param>
internal sealed record ScadListIfExpression(ScadExpression Condition, ScadExpression WhenTrue, ScadExpression? WhenFalse) : ScadExpression;

/// <summary>A list comprehension element <c>let (assignments) body</c>.</summary>
/// <param name="Assignments">The bindings.</param>
/// <param name="Body">The element.</param>
internal sealed record ScadListLetExpression(IReadOnlyList<ScadAssignment> Assignments, ScadExpression Body) : ScadExpression;

/// <summary>A list comprehension element <c>each value</c> that splices the elements of a vector.</summary>
/// <param name="Value">The spliced expression.</param>
internal sealed record ScadListEachExpression(ScadExpression Value) : ScadExpression;

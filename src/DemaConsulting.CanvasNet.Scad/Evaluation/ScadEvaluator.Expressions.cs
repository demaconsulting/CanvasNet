using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Expression evaluation.
/// </summary>
internal sealed partial class ScadEvaluator
{
    /// <summary>
    ///     Evaluates an expression. Every expression node is one frame and one step.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="scope">The scope to evaluate it in.</param>
    /// <returns>The value.</returns>
    private ScadValue Evaluate(ScadExpression expression, ScadScope scope)
    {
        EnterFrame(expression.Location);
        Charge(1);
        var value = EvaluateNode(expression, scope);
        LeaveFrame();
        return value;
    }

    /// <summary>
    ///     Dispatches an expression to its evaluation. Kept separate from the frame-counting method, with every arm a
    ///     single call, so that the frames that nest during deep recursion stay small.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The value.</returns>
    private ScadValue EvaluateNode(ScadExpression expression, ScadScope scope) => expression switch
    {
        ScadNumberLiteral number => ScadValue.FromNumber(number.Value),
        ScadIdentifierExpression identifier => LookupVariable(identifier.Name, scope, identifier.Location),
        ScadBinaryExpression binary => EvaluateBinary(binary, scope),
        ScadCallExpression call => EvaluateCall(call, scope),
        ScadConditionalExpression conditional => EvaluateConditional(conditional, scope),
        ScadStringLiteral text => ScadValue.FromString(text.Value),
        ScadBooleanLiteral boolean => ScadValue.FromBoolean(boolean.Value),
        ScadUndefLiteral => ScadValue.Undef,
        ScadVectorExpression vector => EvaluateVector(vector, scope),
        ScadRangeExpression range => EvaluateRange(range, scope),
        ScadUnaryExpression unary => EvaluateUnary(unary, scope),
        ScadIndexExpression index => EvaluateIndex(index, scope),
        ScadMemberExpression member => EvaluateMember(member, scope),
        ScadFunctionLiteral literal => ScadValue.FromFunction(new ScadClosure(null, literal.Parameters, literal.Body, scope, literal.Location)),
        ScadLetExpression let => EvaluateLet(let, scope),
        ScadEchoExpression echo => EvaluateEchoExpression(echo, scope),
        ScadAssertExpression assert => EvaluateAssertExpression(assert, scope),
        _ => EvaluateStrayElement(expression),
    };

    /// <summary>
    ///     Evaluates a conditional expression: only the chosen branch is evaluated.
    /// </summary>
    /// <param name="conditional">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The value of the chosen branch.</returns>
    private ScadValue EvaluateConditional(ScadConditionalExpression conditional, ScadScope scope)
    {
        var chosen = Evaluate(conditional.Condition, scope).IsTrue ? conditional.WhenTrue : conditional.WhenFalse;
        return Evaluate(chosen, scope);
    }

    /// <summary>
    ///     Evaluates an index expression.
    /// </summary>
    /// <param name="index">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The element, or <c>undef</c>.</returns>
    private ScadValue EvaluateIndex(ScadIndexExpression index, ScadScope scope)
    {
        var target = Evaluate(index.Target, scope);
        return target.Index(Evaluate(index.Index, scope), this);
    }

    /// <summary>
    ///     Evaluates a member access.
    /// </summary>
    /// <param name="member">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The member, or <c>undef</c>.</returns>
    private ScadValue EvaluateMember(ScadMemberExpression member, ScadScope scope) =>
        Evaluate(member.Target, scope).Member(member.Member);

    /// <summary>
    ///     Evaluates a <c>let</c> expression.
    /// </summary>
    /// <param name="let">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The value of the body.</returns>
    private ScadValue EvaluateLet(ScadLetExpression let, ScadScope scope) =>
        Evaluate(let.Body, BuildLetScope(let.Assignments, scope));

    /// <summary>
    ///     Reports a list comprehension element that appears outside a vector literal.
    /// </summary>
    /// <param name="expression">The element.</param>
    /// <returns><c>undef</c>.</returns>
    private ScadValue EvaluateStrayElement(ScadExpression expression)
    {
        Warn("A list comprehension element is only valid inside a vector", expression.Location);
        return ScadValue.Undef;
    }

    /// <summary>
    ///     Evaluates a binary expression; <c>&amp;&amp;</c> and <c>||</c> short-circuit.
    /// </summary>
    /// <param name="binary">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The value.</returns>
    private ScadValue EvaluateBinary(ScadBinaryExpression binary, ScadScope scope)
    {
        var left = Evaluate(binary.Left, scope);
        if (binary.Operator is ScadBinaryOperator.And or ScadBinaryOperator.Or)
        {
            // The right operand is evaluated only when the left one does not decide the result
            var decided = binary.Operator == ScadBinaryOperator.And ? !left.IsTrue : left.IsTrue;
            return decided
                ? ScadValue.FromBoolean(binary.Operator == ScadBinaryOperator.Or)
                : ScadValue.FromBoolean(Evaluate(binary.Right, scope).IsTrue);
        }

        var right = Evaluate(binary.Right, scope);
        ChargeDeep(left);
        ChargeDeep(right);

        // A product of two vectors may be a matrix product: its limit and step diagnostics belong to the operator
        if (binary.Operator == ScadBinaryOperator.Multiply && left.Kind == ScadValueKind.Vector && right.Kind == ScadValueKind.Vector)
        {
            _hostLocation = binary.Location;
            _lastFrameLocation = binary.Location;
        }

        var result = ScadValue.Binary(binary.Operator, left, right, out var typeError, this);
        if (typeError)
        {
            Warn($"Cannot apply operator '{OperatorText(binary.Operator)}' to {left.KindDescription} and {right.KindDescription}", binary.Location);
        }

        return result;
    }

    /// <summary>
    ///     Evaluates a unary expression.
    /// </summary>
    /// <param name="unary">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The value.</returns>
    private ScadValue EvaluateUnary(ScadUnaryExpression unary, ScadScope scope)
    {
        var operand = Evaluate(unary.Operand, scope);
        ChargeDeep(operand);
        var result = ScadValue.Unary(unary.Operator, operand, out var typeError);
        if (typeError)
        {
            Warn($"Cannot apply operator '{(unary.Operator == ScadUnaryOperator.Plus ? "+" : "-")}' to {operand.KindDescription}", unary.Location);
        }

        return result;
    }

    /// <summary>
    ///     Evaluates a range expression.
    /// </summary>
    /// <param name="range">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The range value, or <c>undef</c> with a warning when a part is not a number.</returns>
    private ScadValue EvaluateRange(ScadRangeExpression range, ScadScope scope)
    {
        var start = Evaluate(range.Start, scope);
        var step = range.Step is null ? ScadValue.FromNumber(1) : Evaluate(range.Step, scope);
        var end = Evaluate(range.End, scope);
        if (start.Kind != ScadValueKind.Number || step.Kind != ScadValueKind.Number || end.Kind != ScadValueKind.Number)
        {
            Warn("Range bounds and step must be numbers", range.Location);
            return ScadValue.Undef;
        }

        return ScadValue.FromRange(start.AsNumber, step.AsNumber, end.AsNumber);
    }

    /// <summary>
    ///     Evaluates the <c>ECHO</c> side effect of an <c>echo</c> expression, then its body.
    /// </summary>
    /// <param name="echo">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The value of the body.</returns>
    private ScadValue EvaluateEchoExpression(ScadEchoExpression echo, ScadScope scope)
    {
        DoEcho(echo.Arguments, scope, echo.Location);
        return Evaluate(echo.Body, scope);
    }

    /// <summary>
    ///     Evaluates the check of an <c>assert</c> expression, then its body.
    /// </summary>
    /// <param name="assert">The expression.</param>
    /// <param name="scope">The scope.</param>
    /// <returns>The value of the body.</returns>
    private ScadValue EvaluateAssertExpression(ScadAssertExpression assert, ScadScope scope)
    {
        DoAssert(assert.Arguments, scope, assert.Location);
        return Evaluate(assert.Body, scope);
    }

    /// <summary>
    ///     Charges the extra steps of an operation on a vector: one per element, nested elements included.
    /// </summary>
    /// <remarks>
    ///     The count stops as soon as it exceeds the remaining step budget, so the check itself is bounded.
    /// </remarks>
    /// <param name="value">The operand.</param>
    private void ChargeDeep(ScadValue value)
    {
        if (value.Kind == ScadValueKind.Vector)
        {
            Charge(DeepCount(value, _limits.MaxEvaluationSteps - _steps));
        }
    }

    /// <summary>
    ///     Counts the elements of a vector, including those of nested vectors, stopping once the count exceeds a cap.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cap">The count beyond which counting stops.</param>
    /// <returns>The count (at least one for a vector), possibly an underestimate once it exceeds the cap.</returns>
    private static long DeepCount(ScadValue value, long cap)
    {
        var items = value.AsVector.AsSpan();
        long count = Math.Max(1, items.Length);
        foreach (var item in items)
        {
            if (count > cap)
            {
                break;
            }

            if (item.Kind == ScadValueKind.Vector)
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
                count += DeepCount(item, cap - count);
            }
        }

        return count;
    }

    /// <summary>
    ///     Gets the source text of a binary operator.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <returns>The text.</returns>
    private static string OperatorText(ScadBinaryOperator op) => op switch
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

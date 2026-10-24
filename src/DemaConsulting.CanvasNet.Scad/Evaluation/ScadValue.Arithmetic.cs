using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Operators, indexing and member access of <see cref="ScadValue"/>.
/// </summary>
internal readonly partial struct ScadValue
{
    /// <summary>
    ///     Applies a binary operator to two evaluated operands.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Equality, inequality, the four relational operators and the logical operators accept every pair of
    ///         values and never report a type error (the logical operators here are the non-short-circuit form that
    ///         yields booleans; the evaluator short-circuits before calling). The arithmetic operators accept
    ///         number with number and the vector forms (element-wise addition and subtraction, scaling, dot,
    ///         matrix and vector products, vector division by a number);
    ///         any other pairing, including any <c>undef</c> operand, yields <c>undef</c> and sets
    ///         <paramref name="typeError"/> so that the evaluator can warn.
    ///     </para>
    ///     <para>
    ///         For vector arithmetic the result keeps its shape: when elements of a vector pair cannot be combined
    ///         only those elements become <c>undef</c> and the error flag is still set.
    ///     </para>
    /// </remarks>
    /// <param name="op">The operator.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="typeError">Set to <see langword="true"/> when the operands (or some of their elements) were invalid for the operator.</param>
    /// <returns>The result.</returns>
    public static ScadValue Binary(ScadBinaryOperator op, ScadValue left, ScadValue right, out bool typeError)
    {
        typeError = false;
        switch (op)
        {
            case ScadBinaryOperator.Equal:
                return FromBoolean(left.Equals(right));
            case ScadBinaryOperator.NotEqual:
                return FromBoolean(!left.Equals(right));
            case ScadBinaryOperator.Less:
            case ScadBinaryOperator.LessEqual:
            case ScadBinaryOperator.Greater:
            case ScadBinaryOperator.GreaterEqual:
                return FromBoolean(Relational(op, left, right));
            case ScadBinaryOperator.And:
                return FromBoolean(left.IsTrue && right.IsTrue);
            case ScadBinaryOperator.Or:
                return FromBoolean(left.IsTrue || right.IsTrue);
            default:
                return Arithmetic(op, left, right, ref typeError);
        }
    }

    /// <summary>
    ///     Applies a unary operator to an evaluated operand.
    /// </summary>
    /// <remarks>
    ///     Logical not yields a boolean for any operand. Unary plus is the identity on numbers and vectors and
    ///     negation negates a number or every element of a vector (recursively); any other operand yields
    ///     <c>undef</c> with <paramref name="typeError"/> set.
    /// </remarks>
    /// <param name="op">The operator.</param>
    /// <param name="operand">The operand.</param>
    /// <param name="typeError">Set to <see langword="true"/> when the operand (or an element) was invalid.</param>
    /// <returns>The result.</returns>
    public static ScadValue Unary(ScadUnaryOperator op, ScadValue operand, out bool typeError)
    {
        typeError = false;
        if (op == ScadUnaryOperator.Not)
        {
            return FromBoolean(!operand.IsTrue);
        }

        if (op == ScadUnaryOperator.Plus)
        {
            return operand._kind is ScadValueKind.Number or ScadValueKind.Vector
                ? operand
                : InvalidOperand(ref typeError);
        }

        return Negate(operand, ref typeError);
    }

    /// <summary>
    ///     Indexes a vector or a string.
    /// </summary>
    /// <remarks>
    ///     The index must be a finite number and is truncated toward zero. A vector yields the element at that
    ///     position and a string yields the one-scalar string there (strings are indexed by Unicode scalar, so a
    ///     surrogate pair is one element). A negative or too large position, a non-numeric or non-finite index, and
    ///     any other target (number, boolean, range, function, <c>undef</c>) yield <c>undef</c> silently.
    /// </remarks>
    /// <param name="index">The index value.</param>
    /// <returns>The element, or <c>undef</c>.</returns>
    public ScadValue Index(ScadValue index)
    {
        if (index._kind != ScadValueKind.Number || !double.IsFinite(index._number))
        {
            return Undef;
        }

        // Truncate toward zero first; a negative result (below -1 before truncation) is out of range
        var position = Math.Truncate(index._number);
        if (position < 0 || position >= int.MaxValue)
        {
            return Undef;
        }

        switch (_kind)
        {
            case ScadValueKind.Vector:
                var vector = (ScadVector)_reference!;
                return position < vector.Count ? vector[(int)position] : Undef;
            case ScadValueKind.String:
                var scalar = ScadText.ScalarAt((string)_reference!, (long)position);
                return scalar is null ? Undef : FromString(scalar);
            default:
                return Undef;
        }
    }

    /// <summary>
    ///     Reads a member of the value.
    /// </summary>
    /// <remarks>
    ///     On a vector, <c>x</c>, <c>y</c> and <c>z</c> are the elements at positions 0, 1 and 2; every other
    ///     member name, and every other kind of value, yields <c>undef</c>.
    /// </remarks>
    /// <param name="name">The member name.</param>
    /// <returns>The member value, or <c>undef</c>.</returns>
    public ScadValue Member(string name)
    {
        if (_kind != ScadValueKind.Vector)
        {
            return Undef;
        }

        var position = name switch
        {
            "x" => 0,
            "y" => 1,
            "z" => 2,
            _ => -1,
        };
        return position < 0 ? Undef : Index(FromNumber(position));
    }

    /// <summary>
    ///     Applies one of the arithmetic operators.
    /// </summary>
    /// <param name="op">The operator (add, subtract, multiply, divide, modulo or power).</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="typeError">Receives the error flag.</param>
    /// <returns>The result.</returns>
    private static ScadValue Arithmetic(ScadBinaryOperator op, ScadValue left, ScadValue right, ref bool typeError)
    {
        // Every arithmetic operator shares the scalar case; the vector forms differ per operator
        if (left._kind == ScadValueKind.Number && right._kind == ScadValueKind.Number)
        {
            return FromNumber(ApplyScalar(op, left._number, right._number));
        }

        return op switch
        {
            ScadBinaryOperator.Add or ScadBinaryOperator.Subtract => AddOrSubtract(op, left, right, ref typeError),
            ScadBinaryOperator.Multiply => Multiply(left, right, ref typeError),
            ScadBinaryOperator.Divide => Divide(left, right, ref typeError),
            _ => InvalidOperand(ref typeError),
        };
    }

    /// <summary>
    ///     Applies an arithmetic operator to two numbers.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="left">The left number.</param>
    /// <param name="right">The right number.</param>
    /// <returns>The IEEE result: division by zero gives an infinity or <c>NaN</c>, modulo is the C <c>fmod</c> remainder.</returns>
    private static double ApplyScalar(ScadBinaryOperator op, double left, double right) => op switch
    {
        ScadBinaryOperator.Add => left + right,
        ScadBinaryOperator.Subtract => left - right,
        ScadBinaryOperator.Multiply => left * right,
        ScadBinaryOperator.Divide => left / right,
        ScadBinaryOperator.Modulo => left % right,
        _ => Math.Pow(left, right),
    };

    /// <summary>
    ///     Adds or subtracts two vectors element-wise.
    /// </summary>
    /// <param name="op">Add or subtract.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="typeError">Receives the error flag.</param>
    /// <returns>The element-wise result, or <c>undef</c> when the operands are not two vectors of equal length.</returns>
    private static ScadValue AddOrSubtract(ScadBinaryOperator op, ScadValue left, ScadValue right, ref bool typeError)
    {
        if (left._kind != ScadValueKind.Vector || right._kind != ScadValueKind.Vector)
        {
            return InvalidOperand(ref typeError);
        }

        RuntimeHelpers.EnsureSufficientExecutionStack();
        var a = (ScadVector)left._reference!;
        var b = (ScadVector)right._reference!;
        if (a.Count != b.Count)
        {
            return InvalidOperand(ref typeError);
        }

        // Combine position by position; elements may themselves be vectors
        var items = new ScadValue[a.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = Arithmetic(op, a[i], b[i], ref typeError);
        }

        return FromVector(items);
    }

    /// <summary>
    ///     Multiplies two values: scalar by scalar, vector by scalar, scalar by vector, and the dot, matrix and vector products.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="typeError">Receives the error flag.</param>
    /// <returns>The product, or <c>undef</c> for invalid operands.</returns>
    private static ScadValue Multiply(ScadValue left, ScadValue right, ref bool typeError)
    {
        if (left._kind == ScadValueKind.Vector && right._kind == ScadValueKind.Vector)
        {
            return ProductOfVectors((ScadVector)left._reference!, (ScadVector)right._reference!, ref typeError);
        }

        // Exactly one vector and one number: scale every element (recursively)
        if (left._kind == ScadValueKind.Vector && right._kind == ScadValueKind.Number)
        {
            return ScaleElements(ScadBinaryOperator.Multiply, (ScadVector)left._reference!, right, ref typeError);
        }

        if (left._kind == ScadValueKind.Number && right._kind == ScadValueKind.Vector)
        {
            return ScaleElements(ScadBinaryOperator.Multiply, (ScadVector)right._reference!, left, ref typeError);
        }

        return InvalidOperand(ref typeError);
    }

    /// <summary>
    ///     Divides a vector by a number element-wise; every other pairing is invalid.
    /// </summary>
    /// <param name="left">The dividend.</param>
    /// <param name="right">The divisor.</param>
    /// <param name="typeError">Receives the error flag.</param>
    /// <returns>The quotient, or <c>undef</c> for invalid operands (including a number divided by a vector).</returns>
    private static ScadValue Divide(ScadValue left, ScadValue right, ref bool typeError) =>
        left._kind == ScadValueKind.Vector && right._kind == ScadValueKind.Number
            ? ScaleElements(ScadBinaryOperator.Divide, (ScadVector)left._reference!, right, ref typeError)
            : InvalidOperand(ref typeError);

    /// <summary>
    ///     Applies multiplication or division by a scalar to every element of a vector.
    /// </summary>
    /// <param name="op">Multiply or divide (the vector is always the left operand of a division).</param>
    /// <param name="vector">The vector.</param>
    /// <param name="scalar">The number value.</param>
    /// <param name="typeError">Receives the error flag.</param>
    /// <returns>The scaled vector.</returns>
    private static ScadValue ScaleElements(ScadBinaryOperator op, ScadVector vector, ScadValue scalar, ref bool typeError)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var items = new ScadValue[vector.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = Arithmetic(op, vector[i], scalar, ref typeError);
        }

        return FromVector(items);
    }

    /// <summary>
    ///     Negates a number or every element of a vector.
    /// </summary>
    /// <param name="operand">The operand.</param>
    /// <param name="typeError">Receives the error flag.</param>
    /// <returns>The negation, or <c>undef</c> for an invalid operand.</returns>
    private static ScadValue Negate(ScadValue operand, ref bool typeError)
    {
        if (operand._kind == ScadValueKind.Number)
        {
            return FromNumber(-operand._number);
        }

        if (operand._kind != ScadValueKind.Vector)
        {
            return InvalidOperand(ref typeError);
        }

        RuntimeHelpers.EnsureSufficientExecutionStack();
        var vector = (ScadVector)operand._reference!;
        var items = new ScadValue[vector.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = Negate(vector[i], ref typeError);
        }

        return FromVector(items);
    }

    /// <summary>
    ///     Records an invalid operand and produces the <c>undef</c> result.
    /// </summary>
    /// <param name="typeError">The error flag to set.</param>
    /// <returns><c>undef</c>.</returns>
    private static ScadValue InvalidOperand(ref bool typeError)
    {
        typeError = true;
        return Undef;
    }
}

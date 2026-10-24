using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Ordering of <see cref="ScadValue"/> (the <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> and <c>&gt;=</c> operators).
/// </summary>
internal readonly partial struct ScadValue
{
    /// <summary>
    ///     Compares two values with the language's ordering rules.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Only these pairings are ordered: number with number (IEEE; <c>NaN</c> is unordered), string with
    ///         string (ordinal UTF-16), boolean with boolean (<c>false</c> before <c>true</c>), and vector with
    ///         vector (lexicographic, a proper prefix is smaller; an unordered pair of elements makes the whole
    ///         comparison unordered once all earlier elements were equal).
    ///     </para>
    ///     <para>
    ///         Every other pairing, including mixed kinds, <c>undef</c>, ranges and functions, is unordered: all four
    ///         relational operators are then <see langword="false"/> and no diagnostic is raised.
    ///     </para>
    /// </remarks>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <param name="result">A negative number, zero or a positive number when the values are ordered; otherwise zero.</param>
    /// <returns><see langword="true"/> when the values are ordered.</returns>
    public static bool TryCompare(ScadValue left, ScadValue right, out int result)
    {
        result = 0;
        if (left._kind != right._kind)
        {
            return false;
        }

        switch (left._kind)
        {
            case ScadValueKind.Boolean:
            case ScadValueKind.Number:
                return TryCompareNumbers(left._number, right._number, out result);
            case ScadValueKind.String:
                result = Math.Sign(string.CompareOrdinal((string)left._reference!, (string)right._reference!));
                return true;
            case ScadValueKind.Vector:
                return TryCompareVectors((ScadVector)left._reference!, (ScadVector)right._reference!, out result);
            default:
                return false;
        }
    }

    /// <summary>
    ///     Applies one of the four relational operators.
    /// </summary>
    /// <param name="op">The operator; one of the four relational operators.</param>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The outcome; <see langword="false"/> whenever the values are unordered.</returns>
    private static bool Relational(ScadBinaryOperator op, ScadValue left, ScadValue right)
    {
        if (!TryCompare(left, right, out var order))
        {
            return false;
        }

        return op switch
        {
            ScadBinaryOperator.Less => order < 0,
            ScadBinaryOperator.LessEqual => order <= 0,
            ScadBinaryOperator.Greater => order > 0,
            _ => order >= 0,
        };
    }

    /// <summary>
    ///     Compares two numbers (or two booleans stored as one and zero).
    /// </summary>
    /// <param name="left">The left number.</param>
    /// <param name="right">The right number.</param>
    /// <param name="result">The sign of the comparison; zero when unordered.</param>
    /// <returns><see langword="false"/> when either number is <c>NaN</c>.</returns>
    private static bool TryCompareNumbers(double left, double right, out int result)
    {
        result = 0;
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return false;
        }

        // -0 and 0 are equal, so only the ordinary relational operators decide
        if (left < right)
        {
            result = -1;
        }
        else if (left > right)
        {
            result = 1;
        }

        return true;
    }

    /// <summary>
    ///     Compares two vectors lexicographically.
    /// </summary>
    /// <param name="left">The left vector.</param>
    /// <param name="right">The right vector.</param>
    /// <param name="result">The sign of the comparison; zero when unordered.</param>
    /// <returns><see langword="false"/> when an element pair that decides the comparison is unordered.</returns>
    private static bool TryCompareVectors(ScadVector left, ScadVector right, out int result)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        result = 0;

        // The first pair of elements that differs decides; an unordered pair makes the whole comparison unordered
        var common = Math.Min(left.Count, right.Count);
        for (var i = 0; i < common; i++)
        {
            if (!TryCompare(left[i], right[i], out var order))
            {
                return false;
            }

            if (order != 0)
            {
                result = order;
                return true;
            }
        }

        // All common elements are equal: the shorter vector is the smaller one
        result = left.Count.CompareTo(right.Count);
        return true;
    }
}

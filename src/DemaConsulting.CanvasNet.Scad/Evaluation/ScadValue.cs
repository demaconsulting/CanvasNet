using System.Runtime.CompilerServices;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     An evaluated OpenSCAD-language value: <c>undef</c>, boolean, number, string, vector, range or function.
/// </summary>
/// <remarks>
///     <para>
///         The value is a small immutable struct (a kind tag, a <see cref="double"/> and one reference), so
///         numbers and booleans never allocate. Strings are .NET strings, vectors are <see cref="ScadVector"/>,
///         ranges are lazy <see cref="ScadRange"/> and functions wrap a <see cref="IScadFunctionValue"/>.
///         <c>default(ScadValue)</c> is <c>undef</c>.
///     </para>
///     <para>
///         The semantics (truthiness, equality, ordering, arithmetic, text forms) are CanvasNet's own dialect and
///         are specified in the partial files of this type. Every operation is pure and deterministic. Recursive
///         operations over nested vectors check the remaining stack at every level and rely on the nesting bound
///         <see cref="MaxNestingDepth"/> that the evaluator enforces when it constructs vectors.
///     </para>
/// </remarks>
internal readonly partial struct ScadValue : IEquatable<ScadValue>
{
    /// <summary>The maximum vector nesting depth the evaluator permits.</summary>
    public const int MaxNestingDepth = 256;

    /// <summary>The kind tag.</summary>
    private readonly ScadValueKind _kind;

    /// <summary>The payload of a number (the value) or a boolean (one or zero).</summary>
    private readonly double _number;

    /// <summary>The payload of a string, vector, range or function.</summary>
    private readonly object? _reference;

    /// <summary>
    ///     Initializes a value.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="number">The numeric payload.</param>
    /// <param name="reference">The reference payload.</param>
    private ScadValue(ScadValueKind kind, double number, object? reference)
    {
        _kind = kind;
        _number = number;
        _reference = reference;
    }

    /// <summary>
    ///     Gets the kind of the value.
    /// </summary>
    public ScadValueKind Kind => _kind;

    /// <summary>
    ///     Gets the <c>undef</c> value.
    /// </summary>
    public static ScadValue Undef => default;

    /// <summary>
    ///     Gets the boolean <c>true</c>.
    /// </summary>
    public static ScadValue True => new(ScadValueKind.Boolean, 1, null);

    /// <summary>
    ///     Gets the boolean <c>false</c>.
    /// </summary>
    public static ScadValue False => new(ScadValueKind.Boolean, 0, null);

    /// <summary>
    ///     Gets the numeric payload of a number value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a number.</exception>
    public double AsNumber => _kind == ScadValueKind.Number ? _number : throw WrongKind(ScadValueKind.Number);

    /// <summary>
    ///     Gets the payload of a boolean value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a boolean.</exception>
    public bool AsBoolean => _kind == ScadValueKind.Boolean ? _number != 0 : throw WrongKind(ScadValueKind.Boolean);

    /// <summary>
    ///     Gets the payload of a string value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a string.</exception>
    public string AsString => _kind == ScadValueKind.String ? (string)_reference! : throw WrongKind(ScadValueKind.String);

    /// <summary>
    ///     Gets the payload of a vector value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a vector.</exception>
    public ScadVector AsVector => _kind == ScadValueKind.Vector ? (ScadVector)_reference! : throw WrongKind(ScadValueKind.Vector);

    /// <summary>
    ///     Gets the payload of a range value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a range.</exception>
    public ScadRange AsRange => _kind == ScadValueKind.Range ? (ScadRange)_reference! : throw WrongKind(ScadValueKind.Range);

    /// <summary>
    ///     Gets the payload of a function value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the value is not a function.</exception>
    public IScadFunctionValue AsFunction => _kind == ScadValueKind.Function ? (IScadFunctionValue)_reference! : throw WrongKind(ScadValueKind.Function);

    /// <summary>
    ///     Gets the vector nesting depth: zero for every non-vector, one for a vector without vector elements.
    /// </summary>
    public int Depth => _kind == ScadValueKind.Vector ? ((ScadVector)_reference!).Depth : 0;

    /// <summary>
    ///     Gets a value indicating whether the value is true when used as a condition.
    /// </summary>
    /// <remarks>
    ///     False for <c>undef</c>, <c>false</c>, <c>0</c> and <c>-0</c>, the empty string and the empty vector; true
    ///     for everything else, including <c>NaN</c>, every non-empty string or vector, every range and every
    ///     function.
    /// </remarks>
    public bool IsTrue => _kind switch
    {
        ScadValueKind.Undef => false,
        ScadValueKind.Boolean or ScadValueKind.Number => _number != 0,
        ScadValueKind.String => ((string)_reference!).Length > 0,
        ScadValueKind.Vector => ((ScadVector)_reference!).Count > 0,
        _ => true,
    };

    /// <summary>
    ///     Gets a phrase that names the kind of the value for diagnostics, for example "a number" or "undef".
    /// </summary>
    public string KindDescription => _kind switch
    {
        ScadValueKind.Undef => "undef",
        ScadValueKind.Boolean => "a boolean",
        ScadValueKind.Number => "a number",
        ScadValueKind.String => "a string",
        ScadValueKind.Vector => "a vector",
        ScadValueKind.Range => "a range",
        _ => "a function",
    };

    /// <summary>
    ///     Creates a number value.
    /// </summary>
    /// <param name="value">The number; any double, including <c>NaN</c> and the infinities.</param>
    /// <returns>The value.</returns>
    public static ScadValue FromNumber(double value) => new(ScadValueKind.Number, value, null);

    /// <summary>
    ///     Creates a boolean value.
    /// </summary>
    /// <param name="value">The boolean.</param>
    /// <returns>The value.</returns>
    public static ScadValue FromBoolean(bool value) => value ? True : False;

    /// <summary>
    ///     Creates a string value.
    /// </summary>
    /// <param name="value">The string; must not be null.</param>
    /// <returns>The value.</returns>
    public static ScadValue FromString(string value) => new(ScadValueKind.String, 0, value);

    /// <summary>
    ///     Creates a vector value that takes ownership of an element array.
    /// </summary>
    /// <remarks>
    ///     The nesting depth is computed here but not limited; the evaluator rejects a vector deeper than
    ///     <see cref="MaxNestingDepth"/> after constructing it.
    /// </remarks>
    /// <param name="items">The elements; the caller must not modify the array afterwards.</param>
    /// <returns>The value.</returns>
    public static ScadValue FromVector(ScadValue[] items) => new(ScadValueKind.Vector, 0, new ScadVector(items));

    /// <summary>
    ///     Creates a vector value from a list of elements.
    /// </summary>
    /// <param name="items">The elements; the list is copied.</param>
    /// <returns>The value.</returns>
    public static ScadValue FromVector(IReadOnlyCollection<ScadValue> items)
    {
        var copy = new ScadValue[items.Count];
        var index = 0;
        foreach (var item in items)
        {
            copy[index++] = item;
        }

        return FromVector(copy);
    }

    /// <summary>
    ///     Creates a range value.
    /// </summary>
    /// <param name="start">The first value.</param>
    /// <param name="step">The increment.</param>
    /// <param name="end">The inclusive limit.</param>
    /// <returns>The value.</returns>
    public static ScadValue FromRange(double start, double step, double end) =>
        new(ScadValueKind.Range, 0, new ScadRange(start, step, end));

    /// <summary>
    ///     Creates a function value.
    /// </summary>
    /// <param name="function">The function payload; must not be null.</param>
    /// <returns>The value.</returns>
    public static ScadValue FromFunction(IScadFunctionValue function) => new(ScadValueKind.Function, 0, function);

    /// <summary>
    ///     Tests two values for equality using the language's <c>==</c> semantics.
    /// </summary>
    /// <remarks>
    ///     Values are equal only when they have the same kind (a number never equals a boolean). Numbers compare by
    ///     IEEE rules (<c>NaN</c> equals nothing, not even itself; <c>0</c> equals <c>-0</c>), strings by ordinal comparison,
    ///     vectors element-wise and recursively with equal length, ranges by (start, step, end), functions by
    ///     reference, and <c>undef</c> equals <c>undef</c>.
    /// </remarks>
    /// <param name="other">The other value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public bool Equals(ScadValue other)
    {
        // Different kinds are never equal
        if (_kind != other._kind)
        {
            return false;
        }

        return _kind switch
        {
            ScadValueKind.Undef => true,
            ScadValueKind.Boolean or ScadValueKind.Number => ScadNumbers.AreEqual(_number, other._number),
            ScadValueKind.String => string.Equals((string)_reference!, (string)other._reference!, StringComparison.Ordinal),
            ScadValueKind.Vector => VectorsEqual((ScadVector)_reference!, (ScadVector)other._reference!),
            ScadValueKind.Range => ((ScadRange)_reference!).Equals((ScadRange)other._reference!),
            _ => ReferenceEquals(_reference, other._reference),
        };
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ScadValue other && Equals(other);

    /// <summary>
    ///     Computes a hash code consistent with <see cref="Equals(ScadValue)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        // Mirror the equality rules: kind plus the payload that takes part in equality
        var payload = _kind switch
        {
            ScadValueKind.Undef => 0,
            ScadValueKind.Boolean or ScadValueKind.Number => _number.GetHashCode(),
            ScadValueKind.String => string.GetHashCode((string)_reference!, StringComparison.Ordinal),
            ScadValueKind.Vector => VectorHash((ScadVector)_reference!),
            ScadValueKind.Range => _reference!.GetHashCode(),
            _ => RuntimeHelpers.GetHashCode(_reference),
        };
        return HashCode.Combine((int)_kind, payload);
    }

    /// <summary>
    ///     Tests two values for equality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when <see cref="Equals(ScadValue)"/> holds.</returns>
    public static bool operator ==(ScadValue left, ScadValue right) => left.Equals(right);

    /// <summary>
    ///     Tests two values for inequality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are not equal.</returns>
    public static bool operator !=(ScadValue left, ScadValue right) => !left.Equals(right);

    /// <summary>
    ///     Compares two vectors element by element.
    /// </summary>
    /// <param name="left">The left vector.</param>
    /// <param name="right">The right vector.</param>
    /// <returns><see langword="true"/> when the lengths match and every pair of elements is equal.</returns>
    private static bool VectorsEqual(ScadVector left, ScadVector right)
    {
        // Each level checks the stack so that deeply nested vectors cannot overflow it
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Hashes the elements of a vector.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The combined hash code.</returns>
    private static int VectorHash(ScadVector vector)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var hash = new HashCode();
        foreach (var item in vector.AsSpan())
        {
            hash.Add(item.GetHashCode());
        }

        return hash.ToHashCode();
    }

    /// <summary>
    ///     Creates the exception for reading a payload of the wrong kind.
    /// </summary>
    /// <param name="expected">The kind that was requested.</param>
    /// <returns>The exception to throw.</returns>
    private InvalidOperationException WrongKind(ScadValueKind expected) =>
        new($"The value is {KindDescription}, not of kind {expected}.");
}

using System.Globalization;

namespace DemaConsulting.CanvasNet.Positions;

/// <summary>
///     The kind of a <see cref="PositionValue"/>.
/// </summary>
public enum PositionValueKind
{
    /// <summary>
    ///     The value is null: the channel is present but holds no value.
    /// </summary>
    Null,

    /// <summary>
    ///     The value is <see langword="true"/> or <see langword="false"/>.
    /// </summary>
    Boolean,

    /// <summary>
    ///     The value is a finite number.
    /// </summary>
    Number,

    /// <summary>
    ///     The value is text (a date is canonical UTC ISO-8601 text such as <c>2024-11-01T00:00:00.000Z</c>).
    /// </summary>
    String
}

/// <summary>
///     A scalar value of a feature (the normalized value of a chart channel, the value of a pie slice) or of a selector
///     predicate, compared with typed equality.
/// </summary>
/// <remarks>
///     <para>
///         Two values are equal only when they have the same kind and the same value: the number <c>3</c> equals the
///         number <c>3.0</c>, but not the text <c>"3"</c>; text is compared case-sensitively. A number is
///         always finite, so equality is well defined. <c>default(PositionValue)</c> is <see cref="Null"/>.
///     </para>
///     <para>
///         The value is immutable and thread-safe.
///     </para>
/// </remarks>
public readonly struct PositionValue : IEquatable<PositionValue>
{
    /// <summary>
    ///     The text of a <see cref="PositionValueKind.String"/> value.
    /// </summary>
    private readonly string? _text;

    /// <summary>
    ///     The number of a <see cref="PositionValueKind.Number"/> value, or 1 for <see langword="true"/> and 0 for
    ///     <see langword="false"/>.
    /// </summary>
    private readonly double _number;

    /// <summary>
    ///     Initializes a value.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="number">The number (or the boolean as 0 or 1).</param>
    /// <param name="text">The text.</param>
    private PositionValue(PositionValueKind kind, double number, string? text)
    {
        Kind = kind;
        _number = number;
        _text = text;
    }

    /// <summary>
    ///     Gets the null value.
    /// </summary>
    public static PositionValue Null => default;

    /// <summary>
    ///     Gets the kind of the value.
    /// </summary>
    public PositionValueKind Kind { get; }

    /// <summary>
    ///     Gets the boolean of a <see cref="PositionValueKind.Boolean"/> value; <see langword="false"/> for another kind.
    /// </summary>
    public bool BooleanValue => Kind == PositionValueKind.Boolean && _number != 0;

    /// <summary>
    ///     Gets the number of a <see cref="PositionValueKind.Number"/> value; 0 for another kind.
    /// </summary>
    public double NumberValue => Kind == PositionValueKind.Number ? _number : 0;

    /// <summary>
    ///     Gets the text of a <see cref="PositionValueKind.String"/> value; <see langword="null"/> for another kind.
    /// </summary>
    public string? StringValue => Kind == PositionValueKind.String ? _text : null;

    /// <summary>
    ///     Creates a boolean value.
    /// </summary>
    /// <param name="value">The boolean.</param>
    /// <returns>The value.</returns>
    public static PositionValue FromBoolean(bool value)
    {
        return new PositionValue(PositionValueKind.Boolean, value ? 1 : 0, null);
    }

    /// <summary>
    ///     Creates a number value.
    /// </summary>
    /// <param name="value">The number; must be finite.</param>
    /// <returns>The value (negative zero is stored as zero).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is NaN or infinite.</exception>
    public static PositionValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A position value number must be finite.");
        }

        // Store negative zero as zero (adding positive zero does that), so that the hash code agrees with the equality
        return new PositionValue(PositionValueKind.Number, value + 0.0, null);
    }

    /// <summary>
    ///     Creates a text value.
    /// </summary>
    /// <param name="value">The text; must not be null.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static PositionValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new PositionValue(PositionValueKind.String, 0, value);
    }

    /// <summary>
    ///     Converts a plain scalar to a value, as a parser produces it.
    /// </summary>
    /// <remarks>
    ///     Accepts <see langword="null"/>, a <see cref="bool"/>, a finite <see cref="double"/> (or another finite
    ///     number type) and a <see cref="string"/>, which are the scalars of
    ///     <see cref="ImageSources.ImageParameterValues.TryParseParameter(string, out object?, out string?)"/>.
    /// </remarks>
    /// <param name="value">The scalar.</param>
    /// <param name="result">The value when the conversion succeeds.</param>
    /// <returns><see langword="true"/> when the scalar is one of the accepted kinds.</returns>
    public static bool TryFromObject(object? value, out PositionValue result)
    {
        // Map each accepted scalar kind; anything else (lists, dictionaries, non-finite numbers) is refused
        switch (value)
        {
            case null:
                result = Null;
                return true;
            case bool flag:
                result = FromBoolean(flag);
                return true;
            case string text:
                result = FromString(text);
                return true;
            case double number when double.IsFinite(number):
                result = FromNumber(number);
                return true;
            case float single when float.IsFinite(single):
                result = FromNumber(single);
                return true;
            case int or long or short or byte or sbyte or ushort or uint or ulong or decimal:
                result = FromNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                return true;
            default:
                result = Null;
                return false;
        }
    }

    /// <inheritdoc/>
    public bool Equals(PositionValue other)
    {
        // Typed equality: the kinds must agree before the values are compared
        return Kind == other.Kind && Kind switch
        {
            PositionValueKind.Null => true,
            PositionValueKind.String => string.Equals(_text, other._text, StringComparison.Ordinal),
            // A number is finite and never negative zero, so equal bits are equal numbers
            _ => BitConverter.DoubleToInt64Bits(_number) == BitConverter.DoubleToInt64Bits(other._number)
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is PositionValue other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Kind == PositionValueKind.String
            ? HashCode.Combine(Kind, StringComparer.Ordinal.GetHashCode(_text!))
            : HashCode.Combine(Kind, _number);
    }

    /// <summary>
    ///     Gets the value as invariant text: <c>null</c>, <c>true</c>, <c>false</c>, the round-trip number, or the text.
    /// </summary>
    /// <returns>The text of the value.</returns>
    public override string ToString()
    {
        return Kind switch
        {
            PositionValueKind.Null => "null",
            PositionValueKind.Boolean => BooleanValue ? "true" : "false",
            PositionValueKind.Number => _number.ToString("R", CultureInfo.InvariantCulture),
            _ => _text!
        };
    }

    /// <summary>
    ///     Compares two values with typed equality.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public static bool operator ==(PositionValue left, PositionValue right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Compares two values with typed equality.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the values differ.</returns>
    public static bool operator !=(PositionValue left, PositionValue right)
    {
        return !left.Equals(right);
    }
}

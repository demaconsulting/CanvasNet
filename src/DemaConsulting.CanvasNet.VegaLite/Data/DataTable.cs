using System.Globalization;
using System.Numerics;
using System.Text;

namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The kind of a <see cref="DataValue"/>.
/// </summary>
internal enum DataValueKind
{
    /// <summary>The absence of a value (JSON <c>null</c>, a missing field).</summary>
    Null,

    /// <summary>A boolean.</summary>
    Boolean,

    /// <summary>A number (a JSON number is always finite; computed values may not be).</summary>
    Number,

    /// <summary>A date: an instant in time, held as whole or fractional milliseconds since 1970-01-01T00:00:00Z (UTC).</summary>
    Date,

    /// <summary>A string.</summary>
    String,

    /// <summary>An immutable array of values (a computed expression result; see <see cref="DataValue.FromArray"/>).</summary>
    Array,

    /// <summary>An immutable object with ordered string keys (a computed expression result; see <see cref="DataObject"/>).</summary>
    Object,

    /// <summary>A regular expression object produced by an expression (an opaque, immutable payload).</summary>
    RegExp,
}

/// <summary>
///     One value of a data row: null, boolean, number, date or string, or (as the result of an expression) an
///     immutable array, object or regular expression.
/// </summary>
/// <remarks>
///     <para>
///         The composite kinds exist for the expression engine: they hold immutable payloads, are equal only to themselves
///         (JavaScript identity), are never produced by the data loaders, and are ordered after the scalar kinds by
///         <see cref="Compare"/>. Their text follows JavaScript: an array is its comma-joined elements, an object is
///         <c>[object Object]</c> and a regular expression is <c>/source/flags</c>.
///     </para>
///     <para>
///         A date is an instant held as milliseconds since the Unix epoch in UTC (the way JavaScript's <c>Date</c>
///         holds it), so it is deterministic and independent of the time zone and culture of the host: its quantitative
///         value is that number of milliseconds and its text is the ISO 8601 form <c>yyyy-MM-ddTHH:mm:ss.fffZ</c>. A
///         date appears in the data when the data names it with <c>format.parse</c> or the data of a delimited file is
///         recognized as ISO 8601 dates, or when the caller supplies a <see cref="DateTime"/> or
///         <see cref="DateTimeOffset"/>. The type is an
///         immutable value type. Equality follows JavaScript's SameValueZero rules (the same kind and the same value;
///         <c>NaN</c> equals <c>NaN</c>; zero equals negative zero), so a value can be used as a dictionary key the way
///         Vega uses values as keys of an ordinal domain.
///     </para>
///     <para>
///         <see cref="Compare"/> is the ascending order Vega uses for sorted domains, with one deliberate difference:
///         values of different kinds are ordered by kind (null, boolean, number, date, string) instead of through
///         JavaScript's implicit type coercions, so the order is total and stable.
///     </para>
/// </remarks>
internal readonly struct DataValue : IEquatable<DataValue>
{
    /// <summary>The smallest date in milliseconds since the epoch: 0001-01-01T00:00:00Z.</summary>
    internal const double MinDateMilliseconds = -62135596800000.0;

    /// <summary>The largest date in milliseconds since the epoch: 9999-12-31T23:59:59.999Z.</summary>
    internal const double MaxDateMilliseconds = 253402300799999.0;

    /// <summary>The numeric payload of a number or a date (or 1/0 of a boolean).</summary>
    private readonly double _number;

    /// <summary>The text of a string, or the immutable payload of an array, object or regular expression.</summary>
    private readonly object? _reference;

    /// <summary>
    ///     Initializes a value.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="number">The number (1 or 0 for a boolean).</param>
    /// <param name="reference">The text of a string or the payload of a composite value.</param>
    private DataValue(DataValueKind kind, double number, object? reference)
    {
        Kind = kind;
        _number = number;
        _reference = reference;
    }

    /// <summary>Gets the null value.</summary>
    public static DataValue Null => default;

    /// <summary>Gets the kind of this value.</summary>
    public DataValueKind Kind { get; }

    /// <summary>Gets a value indicating whether this value is null.</summary>
    public bool IsNull => Kind == DataValueKind.Null;

    /// <summary>Gets a value indicating whether this value is a boolean.</summary>
    public bool IsBoolean => Kind == DataValueKind.Boolean;

    /// <summary>Gets a value indicating whether this value is a number.</summary>
    public bool IsNumber => Kind == DataValueKind.Number;

    /// <summary>Gets a value indicating whether this value is a string.</summary>
    public bool IsString => Kind == DataValueKind.String;

    /// <summary>Gets a value indicating whether this value is a date.</summary>
    public bool IsDate => Kind == DataValueKind.Date;

    /// <summary>Gets the number of a number value; zero for other kinds.</summary>
    public double Number => Kind == DataValueKind.Number ? _number : 0;

    /// <summary>Gets the milliseconds since 1970-01-01T00:00:00Z of a date value; zero for other kinds.</summary>
    public double Milliseconds => Kind == DataValueKind.Date ? _number : 0;

    /// <summary>Gets the boolean of a boolean value; false for other kinds.</summary>
    public bool Boolean => Kind == DataValueKind.Boolean && _number != 0;

    /// <summary>Gets the text of a string value; null for other kinds.</summary>
    public string? Text => _reference as string;

    /// <summary>Gets the elements of an array value; empty for other kinds.</summary>
    public ReadOnlySpan<DataValue> Elements => _reference is DataArray array ? array.Items : default;

    /// <summary>Gets the object of an object value; null for other kinds.</summary>
    public DataObject? ObjectValue => _reference as DataObject;

    /// <summary>Gets the opaque payload of a regular expression value; null for other kinds.</summary>
    public object? RegExpPayload => Kind == DataValueKind.RegExp ? _reference : null;

    /// <summary>Gets a value indicating whether this value is an array.</summary>
    public bool IsArray => Kind == DataValueKind.Array;

    /// <summary>Gets a value indicating whether this value is an object.</summary>
    public bool IsObject => Kind == DataValueKind.Object;

    /// <summary>Gets a value indicating whether this value is a regular expression.</summary>
    public bool IsRegExp => Kind == DataValueKind.RegExp;

    /// <summary>
    ///     Creates a boolean value.
    /// </summary>
    /// <param name="value">The boolean.</param>
    /// <returns>The value.</returns>
    public static DataValue FromBoolean(bool value) => new(DataValueKind.Boolean, value ? 1 : 0, null);

    /// <summary>
    ///     Creates a number value.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The value.</returns>
    public static DataValue FromNumber(double value) => new(DataValueKind.Number, value, null);

    /// <summary>
    ///     Creates a string value.
    /// </summary>
    /// <param name="value">The text. A null text gives the null value.</param>
    /// <returns>The value.</returns>
    public static DataValue FromString(string? value) => value is null ? default : new DataValue(DataValueKind.String, 0, value);

    /// <summary>
    ///     Creates a date value.
    /// </summary>
    /// <param name="milliseconds">The milliseconds since 1970-01-01T00:00:00Z; must be finite and within the range of <see cref="DateTime"/> (years 1 to 9999).</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="milliseconds"/> is not finite or is outside the supported range.</exception>
    public static DataValue FromDate(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < MinDateMilliseconds || milliseconds > MaxDateMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "A date must be a finite number of milliseconds within the years 1 to 9999.");
        }

        // Negative zero and zero are the same instant; one representation keeps equality and hashing simple
        return new DataValue(DataValueKind.Date, milliseconds == 0 ? 0 : milliseconds, null);
    }

    /// <summary>
    ///     Gets the milliseconds since the epoch of a UTC point in time, exactly to the sub-millisecond fraction.
    /// </summary>
    /// <remarks>
    ///     <see cref="TimeSpan.TotalMilliseconds"/> divides a tick count that is beyond the exact range of a double for the
    ///     distant years, which would turn the last whole millisecond into a nearby fraction. The whole milliseconds are
    ///     computed in integer arithmetic here, so every whole millisecond of the supported range is exact.
    /// </remarks>
    /// <param name="utc">The point in time, taken as UTC.</param>
    /// <returns>The milliseconds since 1970-01-01T00:00:00Z.</returns>
    public static double MillisecondsSinceEpoch(DateTime utc)
    {
        var ticks = utc.Ticks - DateTime.UnixEpoch.Ticks;
        return (ticks / TimeSpan.TicksPerMillisecond) + ((ticks % TimeSpan.TicksPerMillisecond) / (double)TimeSpan.TicksPerMillisecond);
    }

    /// <summary>
    ///     Creates a date value from a point in time.
    /// </summary>
    /// <remarks>
    ///     A <see cref="DateTime"/> of kind <see cref="DateTimeKind.Local"/> is converted to UTC; one of kind
    ///     <see cref="DateTimeKind.Unspecified"/> is taken as UTC, so the result never depends on the host time zone.
    /// </remarks>
    /// <param name="value">The point in time.</param>
    /// <returns>The value.</returns>
    public static DataValue FromDateTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return FromDate(MillisecondsSinceEpoch(utc));
    }

    /// <summary>
    ///     Tries to read the value as the number of a quantitative field.
    /// </summary>
    /// <remarks>
    ///     Vega-Lite parses a quantitative field as a number: numbers are taken as they are, a date is its milliseconds
    ///     since the epoch, a boolean is 1 or 0, and a
    ///     string is read as a decimal number (surrounding white space allowed). Null, an empty string, a string that is
    ///     not a number, <c>NaN</c> and infinities are invalid and are not drawn.
    /// </remarks>
    /// <param name="number">Receives the finite number.</param>
    /// <returns><see langword="true"/> when the value is a valid finite number.</returns>
    public bool TryGetQuantitative(out double number)
    {
        switch (Kind)
        {
            case DataValueKind.Number:
                number = _number;
                return double.IsFinite(number);
            case DataValueKind.Boolean or DataValueKind.Date:
                number = _number;
                return true;
            case DataValueKind.String:
                return JsNumber.TryParse(Text!, out number) && double.IsFinite(number) && Text!.AsSpan().Trim().Length > 0;
            default:
                number = 0;
                return false;
        }
    }

    /// <summary>
    ///     Formats the value the way JavaScript's <c>String()</c> does, with the invariant culture.
    /// </summary>
    /// <remarks>
    ///     Null is <c>null</c>, a boolean is <c>true</c> or <c>false</c>, a string is itself, a date is its ISO 8601 form in
    ///     UTC (<c>2024-03-05T00:00:00.000Z</c>, like <c>toISOString()</c>), and a number follows the
    ///     JavaScript rules: <c>1</c> is <c>1</c>, <c>0.5</c> is <c>0.5</c>, <c>1e21</c> is <c>1e+21</c>, an integer
    ///     below 1e21 has no decimal point and negative zero is <c>0</c>.
    /// </remarks>
    /// <returns>The text.</returns>
    public string ToText() => Kind switch
    {
        DataValueKind.Boolean => _number != 0 ? "true" : "false",
        DataValueKind.Number => JsNumber.ToString(_number),
        DataValueKind.Date => DateTime.UnixEpoch.AddMilliseconds(_number).ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'", CultureInfo.InvariantCulture),
        DataValueKind.String => Text!,
        DataValueKind.Array => ArrayText(Elements),
        DataValueKind.Object => "[object Object]",
        DataValueKind.RegExp => _reference!.ToString() ?? string.Empty,
        _ => "null",
    };

    /// <summary>
    ///     Creates an array value.
    /// </summary>
    /// <remarks>The elements are copied, so later changes to the argument do not affect the value.</remarks>
    /// <param name="elements">The elements.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public static DataValue FromArray(IEnumerable<DataValue> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return new DataValue(DataValueKind.Array, 0, new DataArray(elements.ToArray()));
    }

    /// <summary>
    ///     Creates an array value from an array that the caller never changes again.
    /// </summary>
    /// <param name="elements">The elements; ownership passes to the value.</param>
    /// <returns>The value.</returns>
    internal static DataValue FromOwnedArray(DataValue[] elements) => new(DataValueKind.Array, 0, new DataArray(elements));

    /// <summary>
    ///     Creates an object value.
    /// </summary>
    /// <param name="value">The immutable object.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static DataValue FromObject(DataObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DataValue(DataValueKind.Object, 0, value);
    }

    /// <summary>
    ///     Creates a regular expression value around an opaque, immutable payload.
    /// </summary>
    /// <param name="payload">The payload; its <c>ToString()</c> is the text of the value (for example <c>/a+/i</c>).</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload"/> is null.</exception>
    public static DataValue FromRegExp(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new DataValue(DataValueKind.RegExp, 0, payload);
    }

    /// <summary>
    ///     Formats an array like JavaScript's <c>Array.prototype.join()</c> with a comma.
    /// </summary>
    /// <param name="elements">The elements.</param>
    /// <returns>The text; null elements are empty.</returns>
    private static string ArrayText(ReadOnlySpan<DataValue> elements)
    {
        var text = new StringBuilder();
        for (var i = 0; i < elements.Length; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            if (!elements[i].IsNull)
            {
                text.Append(elements[i].ToText());
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Compares two values in ascending order (the order of a sorted Vega domain).
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>Negative when <paramref name="a"/> sorts first, positive when <paramref name="b"/> does, otherwise zero.</returns>
    public static int Compare(DataValue a, DataValue b)
    {
        // Values of different kinds are ordered by kind, which makes the order total
        if (a.Kind != b.Kind)
        {
            return a.Kind.CompareTo(b.Kind);
        }

        switch (a.Kind)
        {
            case DataValueKind.Number:
                // Like Vega's comparator NaN sorts before every other number
                var aIsNaN = double.IsNaN(a._number);
                var bIsNaN = double.IsNaN(b._number);
                if (aIsNaN || bIsNaN)
                {
                    if (aIsNaN && bIsNaN)
                    {
                        return 0;
                    }

                    return aIsNaN ? -1 : 1;
                }

                return Math.Sign(a._number.CompareTo(b._number));
            case DataValueKind.Boolean or DataValueKind.Date:
                return Math.Sign(a._number.CompareTo(b._number));
            case DataValueKind.String:
                // JavaScript compares strings by UTF-16 code units, which is the ordinal comparison
                return Math.Sign(string.CompareOrdinal(a.Text, b.Text));
            default:
                return 0;
        }
    }

    /// <summary>
    ///     Determines whether two values are the same (SameValueZero).
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they have the same kind and value.</returns>
    public static bool operator ==(DataValue left, DataValue right) => left.Equals(right);

    /// <summary>
    ///     Determines whether two values differ.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they differ in kind or value.</returns>
    public static bool operator !=(DataValue left, DataValue right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(DataValue other) =>
        Kind == other.Kind
        && Kind switch
        {
            DataValueKind.String => string.Equals(Text, other.Text, StringComparison.Ordinal),
            DataValueKind.Array or DataValueKind.Object or DataValueKind.RegExp => ReferenceEquals(_reference, other._reference),
            DataValueKind.Number or DataValueKind.Date => _number.CompareTo(other._number) == 0,
            DataValueKind.Boolean => Boolean == other.Boolean,
            _ => true,
        };

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DataValue other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Kind switch
    {
        DataValueKind.String => HashCode.Combine(Kind, Text!.GetHashCode(StringComparison.Ordinal)),
        DataValueKind.Array or DataValueKind.Object or DataValueKind.RegExp => HashCode.Combine(Kind, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_reference)),
        DataValueKind.Number or DataValueKind.Date => HashCode.Combine(Kind, JsNumber.IsZero(_number) ? 0.0 : _number),
        DataValueKind.Boolean => HashCode.Combine(Kind, _number),
        _ => 0,
    };

    /// <inheritdoc/>
    public override string ToString() => ToText();
}

/// <summary>
///     Conversions between numbers and text with the semantics of JavaScript's <c>Number</c>.
/// </summary>
/// <remarks>
///     Vega-Lite labels, domains and the d3 number formats are defined through JavaScript's number-to-string rules, so
///     the Data and Scales units share this helper to produce the same text as the reference implementation, with the
///     invariant culture and without any dependence on the current culture.
/// </remarks>
internal static class JsNumber
{
    /// <summary>
    ///     Converts a number to text like JavaScript's <c>Number.prototype.toString()</c>.
    /// </summary>
    /// <remarks>
    ///     The digits are the shortest that identify the number; the text is plain decimal for exponents from -6 to 20
    ///     and uses an exponent otherwise (<c>1e+21</c>, <c>1.5e-7</c>). Negative zero gives <c>0</c>.
    /// </remarks>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string ToString(double value)
    {
        // Special values first: zero (either sign), NaN and the infinities
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (IsZero(value))
        {
            return "0";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "Infinity" : "-Infinity";
        }

        // Spell the magnitude from its shortest digits and the position n of the decimal point (value = 0.digits x 10^n)
        ShortestDecimal(Math.Abs(value), out var digits, out var n);
        var k = digits.Length;
        var text = new StringBuilder(32);
        if (value < 0)
        {
            text.Append('-');
        }

        if (k <= n && n <= 21)
        {
            text.Append(digits).Append('0', n - k);
        }
        else if (0 < n && n <= 21)
        {
            text.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
        }
        else if (-6 < n && n <= 0)
        {
            text.Append("0.").Append('0', -n).Append(digits);
        }
        else
        {
            var exponent = n - 1;
            text.Append(digits[0]);
            if (k > 1)
            {
                text.Append('.').Append(digits, 1, k - 1);
            }

            text.Append('e').Append(exponent < 0 ? '-' : '+').Append(Math.Abs(exponent).ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    /// <summary>
    ///     Finds the shortest decimal digits that identify a positive finite number.
    /// </summary>
    /// <remarks>
    ///     The result satisfies value = 0.<paramref name="digits"/> x 10^<paramref name="pointPosition"/>: the digits
    ///     have neither leading nor trailing zeros, and among the shortest digit strings that read back as the same
    ///     double the closest one is chosen (the <c>R</c> format of .NET Core 3.0 and later).
    /// </remarks>
    /// <param name="value">The number; must be positive and finite.</param>
    /// <param name="digits">Receives the significant digits.</param>
    /// <param name="pointPosition">Receives the position of the decimal point relative to the first digit.</param>
    public static void ShortestDecimal(double value, out string digits, out int pointPosition)
    {
        // Split the round-trip text into mantissa and exponent
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponent = 0;
        var e = text.IndexOf('E', StringComparison.Ordinal);
        if (e >= 0)
        {
            exponent = int.Parse(text.AsSpan(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            text = text[..e];
        }

        // Remove the decimal point, remembering where it was, then strip the zeros that carry no information
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        var raw = dot >= 0 ? text.Remove(dot, 1) : text;
        var point = (dot >= 0 ? dot : text.Length) + exponent;
        var start = 0;
        while (start < raw.Length - 1 && raw[start] == '0')
        {
            start++;
        }

        point -= start;
        digits = raw[start..].TrimEnd('0');
        pointPosition = point;
        if (digits.Length == 0)
        {
            digits = "0";
        }
    }

    /// <summary>
    ///     Reads a decimal number from text like JavaScript's <c>Number(text)</c> for the common syntaxes.
    /// </summary>
    /// <remarks>
    ///     Surrounding white space is ignored and an empty text is zero, as in JavaScript. Signs, a decimal point and an
    ///     exponent are accepted; <c>Infinity</c> and <c>NaN</c> are accepted; hexadecimal, octal and binary literals
    ///     are not.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the number, or NaN when the text is not a number.</param>
    /// <returns><see langword="true"/> when the text is a number (so the result is not NaN because of a syntax error).</returns>
    public static bool TryParse(string text, out double value)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.Length == 0)
        {
            value = 0;
            return true;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = double.NaN;
        return false;
    }

    /// <summary>
    ///     Rounds a number like JavaScript's <c>Math.round</c>: half-way cases go toward positive infinity.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The rounded number (negative zero is kept for values from -0.5 up to zero).</returns>
    public static double Round(double value)
    {
        // Non-finite values and integers are their own result
        if (!double.IsFinite(value))
        {
            return value;
        }

        var floor = Math.Floor(value);
        var rounded = value - floor >= 0.5 ? floor + 1 : floor;

        // Values from -0.5 up to (but not including) zero round to negative zero
        return IsZero(rounded) && value < 0 ? -0.0 : rounded;
    }

    /// <summary>
    ///     Tells whether a number is zero (positive or negative zero).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> for 0 and -0; false for everything else, including NaN.</returns>
    public static bool IsZero(double value) => value.CompareTo(0.0) == 0;

    /// <summary>
    ///     Writes a non-negative integer-valued double in a radix.
    /// </summary>
    /// <param name="value">The integer value; finite and not negative.</param>
    /// <param name="radix">The radix: 2, 8 or 16.</param>
    /// <returns>The digits, lower case.</returns>
    public static string ToRadix(double value, int radix)
    {
        // The exact integer value of the double is converted, so large values lose nothing
        var number = new BigInteger(value);
        if (number.IsZero)
        {
            return "0";
        }

        const string Alphabet = "0123456789abcdef";
        var chars = new List<char>();
        var divisor = new BigInteger(radix);
        while (!number.IsZero)
        {
            number = BigInteger.DivRem(number, divisor, out var remainder);
            chars.Add(Alphabet[(int)remainder]);
        }

        chars.Reverse();
        return new string([.. chars]);
    }
}

/// <summary>
///     Vega-Lite field paths: the text that addresses a (possibly nested) property of a data row.
/// </summary>
/// <remarks>
///     <para>
///         A field is a dotted or bracketed path: <c>a.b</c> is the property <c>b</c> of the property <c>a</c>,
///         <c>a[0]</c> and <c>a.0</c> address the first element of an array (or the property named 0), and
///         <c>a['b.c']</c> or <c>a\.b</c> address a property whose name contains a dot. A backslash escapes the next
///         character; a bracket may hold a quoted name.
///     </para>
///     <para>
///         The data table stores every leaf of a nested row under the canonical text of its path
///         (<see cref="Join"/>), so a lookup canonicalizes the requested path first and then needs only one dictionary
///         access.
///     </para>
/// </remarks>
internal static class FieldPath
{
    /// <summary>
    ///     Splits a field path into the property names it addresses.
    /// </summary>
    /// <param name="path">The path text.</param>
    /// <returns>The names, outermost first; <see langword="null"/> when the path is malformed (an unclosed bracket or quote).</returns>
    public static IReadOnlyList<string>? Parse(string? path)
    {
        if (path is null)
        {
            return null;
        }

        var segments = new List<string>();
        var current = new StringBuilder();
        var inBracket = false;
        var bracketStart = false;
        var afterQuote = false;
        var quote = '\0';
        var position = 0;
        while (position < path.Length)
        {
            // A backslash makes the next character an ordinary one; a trailing backslash is dropped
            var c = path[position++];
            var escaped = false;
            if (c == '\\')
            {
                if (position >= path.Length)
                {
                    break;
                }

                c = path[position++];
                escaped = true;
            }

            if (quote != '\0')
            {
                // Inside quotes only the closing quote is special
                if (!escaped && c == quote)
                {
                    segments.Add(current.ToString());
                    current.Clear();
                    quote = '\0';
                    afterQuote = true;
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            // A backslash-escaped character is never special
            var control = escaped ? '\0' : c;
            if (inBracket && bracketStart && control is '"' or '\'')
            {
                quote = c;
                bracketStart = false;
            }
            else if (control == '.' && !inBracket)
            {
                Flush(segments, current);
                afterQuote = false;
            }
            else if (control == '[' && !inBracket)
            {
                Flush(segments, current);
                inBracket = true;
                bracketStart = true;
                afterQuote = false;
            }
            else if (control == ']' && inBracket)
            {
                if (!afterQuote)
                {
                    Flush(segments, current);
                }

                inBracket = false;
                bracketStart = false;
                afterQuote = false;
            }
            else if (control == ']')
            {
                // A closing bracket without an opening one
                return null;
            }
            else
            {
                current.Append(c);
                bracketStart = false;
            }
        }

        // An unclosed bracket or quote makes the path malformed
        if (inBracket || quote != '\0')
        {
            return null;
        }

        Flush(segments, current);
        return segments;
    }

    /// <summary>
    ///     Joins property names into the canonical text of a path.
    /// </summary>
    /// <remarks>
    ///     The names are escaped (a backslash and a dot get a backslash) and joined with dots, so different name lists
    ///     never give the same text.
    /// </remarks>
    /// <param name="segments">The names, outermost first.</param>
    /// <returns>The canonical text.</returns>
    public static string Join(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        return string.Join('.', segments.Select(Escape));
    }

    /// <summary>
    ///     Converts the text of a field path to its canonical form.
    /// </summary>
    /// <param name="path">The path text.</param>
    /// <returns>The canonical text; <see langword="null"/> when the path is malformed or addresses nothing.</returns>
    public static string? Canonicalize(string? path)
    {
        var segments = Parse(path);
        return segments is null or { Count: 0 } ? null : Join(segments);
    }

    /// <summary>
    ///     Escapes one property name for the canonical text.
    /// </summary>
    /// <param name="segment">The name.</param>
    /// <returns>The escaped name.</returns>
    public static string Escape(string segment) =>
        segment.Contains('\\') || segment.Contains('.')
            ? segment.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(".", "\\.", StringComparison.Ordinal)
            : segment;

    /// <summary>
    ///     Adds the text collected so far as a segment, unless it is empty.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <param name="current">The text being collected; cleared.</param>
    private static void Flush(List<string> segments, StringBuilder current)
    {
        if (current.Length > 0)
        {
            segments.Add(current.ToString());
            current.Clear();
        }
    }
}

/// <summary>
///     An immutable table of data rows with scalar values.
/// </summary>
/// <remarks>
///     <para>
///         The table has a fixed list of fields in ordinal (code unit) order, so the layout never depends on the order
///         in which fields happened to appear in the data, and a rectangular block of <see cref="DataValue"/> cells;
///         a row that lacks a field holds null there. A nested JSON object is stored flattened: every leaf is a field
///         named by the canonical text of its path (see <see cref="FieldPath"/>).
///     </para>
///     <para>
///         The row count and the field count are bounded by <see cref="VegaLiteLimits.MaxDataRows"/> and
///         <see cref="VegaLiteLimits.MaxFieldCount"/>; a table that would exceed them is not built and a
///         <see cref="VegaLiteRenderException"/> names the location of the data. The table is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class DataTable
{
    /// <summary>The fields in ordinal order (canonical path texts).</summary>
    private readonly string[] _fields;

    /// <summary>The field positions by canonical name.</summary>
    private readonly Dictionary<string, int> _index;

    /// <summary>The cells, row by row.</summary>
    private readonly DataValue[] _cells;

    /// <summary>
    ///     Initializes a table.
    /// </summary>
    /// <param name="fields">The fields in ordinal order.</param>
    /// <param name="cells">The cells, row by row.</param>
    /// <param name="rowCount">The number of rows.</param>
    private DataTable(string[] fields, DataValue[] cells, int rowCount)
    {
        _fields = fields;
        _cells = cells;
        RowCount = rowCount;
        _index = new Dictionary<string, int>(fields.Length, StringComparer.Ordinal);
        for (var i = 0; i < fields.Length; i++)
        {
            _index[fields[i]] = i;
        }
    }

    /// <summary>Gets the table without rows and fields.</summary>
    public static DataTable Empty { get; } = new([], [], 0);

    /// <summary>Gets the number of rows.</summary>
    public int RowCount { get; }

    /// <summary>Gets the number of fields.</summary>
    public int FieldCount => _fields.Length;

    /// <summary>Gets the fields (canonical path texts) in ordinal order.</summary>
    public IReadOnlyList<string> Fields => _fields;

    /// <summary>
    ///     Creates a table from rows given as value arrays.
    /// </summary>
    /// <remarks>
    ///     The table orders its fields by code unit (ordinal order) whatever the order of <paramref name="fields"/>; the values of a row are
    ///     matched to the fields by position in the argument order. The input is copied, so later changes to it do not
    ///     affect the table.
    /// </remarks>
    /// <param name="fields">The field names (canonical path texts), without duplicates.</param>
    /// <param name="rows">The rows; each has one value per field.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="location">The location reported when a limit is exceeded.</param>
    /// <returns>The table.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> or <paramref name="rows"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a field name is duplicated or a row has the wrong number of values.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit is exceeded.</exception>
    public static DataTable Create(
        IReadOnlyList<string> fields,
        IEnumerable<IReadOnlyList<DataValue>> rows,
        VegaLiteLimits? limits = null,
        VegaLiteLocation location = default)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(rows);
        limits ??= VegaLiteLimits.Default;

        // Order the fields by code unit, remembering where each one was given
        if (fields.Count > limits.MaxFieldCount)
        {
            throw new VegaLiteRenderException(
                $"The data has {fields.Count} fields, more than the limit of {limits.MaxFieldCount} (MaxFieldCount).", location);
        }

        var order = Enumerable.Range(0, fields.Count).OrderBy(i => fields[i], StringComparer.Ordinal).ToArray();
        var sorted = order.Select(i => fields[i]).ToArray();
        if (sorted.Distinct(StringComparer.Ordinal).Count() != sorted.Length)
        {
            throw new ArgumentException("The field names must be unique.", nameof(fields));
        }

        // Copy the rows in table field order, stopping as soon as the row limit is exceeded
        var cells = new List<DataValue>();
        var rowCount = 0;
        foreach (var row in rows)
        {
            if (rowCount >= limits.MaxDataRows)
            {
                throw RowLimit(limits, location);
            }

            if (row.Count != fields.Count)
            {
                throw new ArgumentException("Every row must have one value per field.", nameof(rows));
            }

            foreach (var source in order)
            {
                cells.Add(row[source]);
            }

            rowCount++;
        }

        return new DataTable(sorted, cells.ToArray(), rowCount);
    }

    /// <summary>
    ///     Creates a table from rows given as dictionaries of field values.
    /// </summary>
    /// <remarks>
    ///     The fields of the table are the union of the keys of all rows, in ordinal order; a row without a key holds
    ///     null there.
    /// </remarks>
    /// <param name="records">The rows; the keys are canonical path texts.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="location">The location reported when a limit is exceeded.</param>
    /// <returns>The table.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="records"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit is exceeded.</exception>
    public static DataTable FromRecords(
        IEnumerable<IReadOnlyDictionary<string, DataValue>> records,
        VegaLiteLimits? limits = null,
        VegaLiteLocation location = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        limits ??= VegaLiteLimits.Default;

        // Collect the rows and the union of their keys, enforcing both limits while reading
        var rows = new List<IReadOnlyDictionary<string, DataValue>>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (rows.Count >= limits.MaxDataRows)
            {
                throw RowLimit(limits, location);
            }

            rows.Add(record);
            foreach (var key in record.Keys)
            {
                if (names.Add(key) && names.Count > limits.MaxFieldCount)
                {
                    throw new VegaLiteRenderException(
                        $"The data has more than {limits.MaxFieldCount} fields (MaxFieldCount).", location);
                }
            }
        }

        // Lay the cells out in ordinal field order
        var fields = names.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var cells = new DataValue[rows.Count * fields.Length];
        for (var r = 0; r < rows.Count; r++)
        {
            for (var f = 0; f < fields.Length; f++)
            {
                cells[(r * fields.Length) + f] = rows[r].TryGetValue(fields[f], out var value) ? value : default;
            }
        }

        return new DataTable(fields, cells, rows.Count);
    }

    /// <summary>
    ///     Finds a field by its canonical name.
    /// </summary>
    /// <param name="canonicalName">The canonical path text.</param>
    /// <returns>The position of the field, or -1 when the table has no such field.</returns>
    public int IndexOfField(string canonicalName) =>
        canonicalName is not null && _index.TryGetValue(canonicalName, out var index) ? index : -1;

    /// <summary>
    ///     Finds a field by a Vega-Lite field path (<c>a.b</c>, <c>a\.b</c>, <c>a[0]</c>).
    /// </summary>
    /// <param name="fieldPath">The path as written in the specification.</param>
    /// <returns>The position of the field, or -1 when the path is malformed or the table has no such field.</returns>
    public int FindField(string? fieldPath)
    {
        var canonical = FieldPath.Canonicalize(fieldPath);
        return canonical is null ? -1 : IndexOfField(canonical);
    }

    /// <summary>
    ///     Gets one cell.
    /// </summary>
    /// <param name="row">The zero-based row.</param>
    /// <param name="fieldIndex">The zero-based field position.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a position is out of range.</exception>
    public DataValue GetValue(int row, int fieldIndex)
    {
        CheckRow(row);
        ArgumentOutOfRangeException.ThrowIfNegative(fieldIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fieldIndex, _fields.Length);
        return _cells[(row * _fields.Length) + fieldIndex];
    }

    /// <summary>
    ///     Gets the value of a field of a row, addressing the field by path.
    /// </summary>
    /// <param name="row">The zero-based row.</param>
    /// <param name="fieldPath">The field path as written in the specification.</param>
    /// <returns>The value; null when the table has no such field.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the row is out of range.</exception>
    public DataValue GetValue(int row, string? fieldPath)
    {
        CheckRow(row);
        var index = FindField(fieldPath);
        return index < 0 ? default : _cells[(row * _fields.Length) + index];
    }

    /// <summary>
    ///     Gets the number in a cell.
    /// </summary>
    /// <param name="row">The zero-based row.</param>
    /// <param name="fieldIndex">The zero-based field position.</param>
    /// <returns>The number, or <see langword="null"/> when the cell does not hold a number.</returns>
    public double? GetNumber(int row, int fieldIndex)
    {
        var value = GetValue(row, fieldIndex);
        return value.IsNumber ? value.Number : null;
    }

    /// <summary>
    ///     Gets the string in a cell.
    /// </summary>
    /// <param name="row">The zero-based row.</param>
    /// <param name="fieldIndex">The zero-based field position.</param>
    /// <returns>The text, or <see langword="null"/> when the cell does not hold a string.</returns>
    public string? GetString(int row, int fieldIndex) => GetValue(row, fieldIndex).Text;

    /// <summary>
    ///     Gets the boolean in a cell.
    /// </summary>
    /// <param name="row">The zero-based row.</param>
    /// <param name="fieldIndex">The zero-based field position.</param>
    /// <returns>The boolean, or <see langword="null"/> when the cell does not hold a boolean.</returns>
    public bool? GetBoolean(int row, int fieldIndex)
    {
        var value = GetValue(row, fieldIndex);
        return value.IsBoolean ? value.Boolean : null;
    }

    /// <summary>
    ///     Gets the cells of a row, in field order.
    /// </summary>
    /// <param name="row">The zero-based row.</param>
    /// <returns>A read-only view of the row's cells.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the row is out of range.</exception>
    public ReadOnlySpan<DataValue> GetRow(int row)
    {
        CheckRow(row);
        return _cells.AsSpan(row * _fields.Length, _fields.Length);
    }

    /// <summary>
    ///     Enumerates the values of one field, row by row.
    /// </summary>
    /// <param name="fieldIndex">The zero-based field position.</param>
    /// <returns>The values in row order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the field position is out of range.</exception>
    public IEnumerable<DataValue> Column(int fieldIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fieldIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fieldIndex, _fields.Length);
        return EnumerateColumn(fieldIndex);
    }

    /// <summary>
    ///     Creates the exception for a table that has too many rows.
    /// </summary>
    /// <param name="limits">The limits.</param>
    /// <param name="location">The location of the data.</param>
    /// <returns>The exception.</returns>
    private static VegaLiteRenderException RowLimit(VegaLiteLimits limits, VegaLiteLocation location) =>
        new($"The data has more than {limits.MaxDataRows} rows, the limit (MaxDataRows).", location);

    /// <summary>
    ///     Walks one column of the table.
    /// </summary>
    /// <param name="fieldIndex">The field position.</param>
    /// <returns>The values.</returns>
    private IEnumerable<DataValue> EnumerateColumn(int fieldIndex)
    {
        for (var row = 0; row < RowCount; row++)
        {
            yield return _cells[(row * _fields.Length) + fieldIndex];
        }
    }

    /// <summary>
    ///     Checks that a row number is valid.
    /// </summary>
    /// <param name="row">The row.</param>
    private void CheckRow(int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, RowCount);
    }
}

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Unicode-scalar helpers for strings, which are UTF-16 internally but are indexed, counted and searched as
///     sequences of Unicode scalars by the language.
/// </summary>
/// <remarks>
///     A surrogate pair is one scalar; an unpaired surrogate is also counted as one scalar (and is its own code
///     point) so that every string, even ill-formed text, has a well-defined and deterministic scalar sequence.
///     All members are pure and thread-safe.
/// </remarks>
internal static class ScadText
{
    /// <summary>The largest Unicode code point.</summary>
    private const int MaxCodePoint = 0x10FFFF;

    /// <summary>The first UTF-16 surrogate code unit.</summary>
    private const int FirstSurrogate = 0xD800;

    /// <summary>The last UTF-16 surrogate code unit.</summary>
    private const int LastSurrogate = 0xDFFF;

    /// <summary>
    ///     Counts the Unicode scalars of a string.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <returns>The number of scalars.</returns>
    public static int ScalarCount(string text)
    {
        // Fast path: without surrogates every UTF-16 unit is a scalar
        if (!text.AsSpan().ContainsAnyInRange((char)FirstSurrogate, (char)LastSurrogate))
        {
            return text.Length;
        }

        // Otherwise count each well-formed pair once
        var position = 0;
        var count = 0;
        while (position < text.Length)
        {
            position += ScalarLength(text, position);
            count++;
        }

        return count;
    }

    /// <summary>
    ///     Gets one scalar of a string as a string.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <param name="index">The zero-based scalar index.</param>
    /// <returns>The scalar's text (one or two UTF-16 units), or <see langword="null"/> when out of range.</returns>
    public static string? ScalarAt(string text, long index)
    {
        // Scalars never outnumber UTF-16 units, so a larger index is certainly out of range
        if (index < 0 || index >= text.Length)
        {
            return null;
        }

        // Fast path: when nothing up to the index is a surrogate, the scalar index equals the unit index
        var prefix = text.AsSpan(0, (int)index + 1);
        if (!prefix.ContainsAnyInRange((char)FirstSurrogate, (char)LastSurrogate))
        {
            return text[(int)index].ToString();
        }

        // Slow path: walk the scalars from the start
        var position = 0;
        for (long i = 0; i < index && position < text.Length; i++)
        {
            position += ScalarLength(text, position);
        }

        return position < text.Length ? text.Substring(position, ScalarLength(text, position)) : null;
    }

    /// <summary>
    ///     Splits a string into its scalars.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <returns>One string per scalar, in order.</returns>
    public static List<string> Scalars(string text)
    {
        var result = new List<string>(text.Length);
        var position = 0;
        while (position < text.Length)
        {
            var length = ScalarLength(text, position);
            result.Add(text.Substring(position, length));
            position += length;
        }

        return result;
    }

    /// <summary>
    ///     Gets the code point of the first scalar of a non-empty string.
    /// </summary>
    /// <param name="text">The string; must not be empty.</param>
    /// <returns>The code point (an unpaired surrogate is its own code point).</returns>
    public static int FirstCodePoint(string text) =>
        ScalarLength(text, 0) == 2 ? char.ConvertToUtf32(text[0], text[1]) : text[0];

    /// <summary>
    ///     Converts a code point to a string.
    /// </summary>
    /// <param name="codePoint">The numeric code point.</param>
    /// <returns>The scalar's text, or <see langword="null"/> when the value is not a valid Unicode scalar value (negative, above U+10FFFF, or a surrogate).</returns>
    public static string? FromCodePoint(double codePoint)
    {
        // Only whole numbers in the scalar-value ranges are valid
        if (!(codePoint >= 0 && codePoint <= MaxCodePoint) || !ScadNumbers.AreEqual(codePoint, Math.Floor(codePoint)))
        {
            return null;
        }

        var value = (int)codePoint;
        return value is >= FirstSurrogate and <= LastSurrogate ? null : char.ConvertFromUtf32(value);
    }

    /// <summary>
    ///     Gets the length in UTF-16 units of the scalar that starts at a position.
    /// </summary>
    /// <param name="text">The string.</param>
    /// <param name="position">The position; must be within the string.</param>
    /// <returns>Two for a well-formed surrogate pair; otherwise one.</returns>
    private static int ScalarLength(string text, int position) =>
        char.IsHighSurrogate(text[position]) && position + 1 < text.Length && char.IsLowSurrogate(text[position + 1]) ? 2 : 1;
}

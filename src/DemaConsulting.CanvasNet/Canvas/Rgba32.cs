using System.Globalization;
using System.Runtime.InteropServices;

namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     Represents a single 32-bit RGBA pixel (8 bits per channel).
/// </summary>
/// <remarks>
///     This is the first pixel format supported by CanvasNet. It is deliberately kept as a
///     plain 4-byte carrier of channel values with no additional behavior, so that
///     <see cref="Surface"/> row buffers can be reinterpreted between raw <see cref="byte"/> spans
///     and <see cref="Rgba32"/> spans via <see cref="System.Runtime.InteropServices.MemoryMarshal"/>
///     without any copying or conversion. Additional pixel formats (if ever needed) would be
///     introduced as new standalone struct types rather than by extending this one, keeping this
///     type's memory layout stable.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct Rgba32 : IEquatable<Rgba32>
{
    /// <summary>
    ///     The red channel value.
    /// </summary>
    public byte R;

    /// <summary>
    ///     The green channel value.
    /// </summary>
    public byte G;

    /// <summary>
    ///     The blue channel value.
    /// </summary>
    public byte B;

    /// <summary>
    ///     The alpha (opacity) channel value.
    /// </summary>
    public byte A;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba32"/> struct with the specified channel values.
    /// </summary>
    /// <param name="r">The red channel value.</param>
    /// <param name="g">The green channel value.</param>
    /// <param name="b">The blue channel value.</param>
    /// <param name="a">The alpha channel value.</param>
    /// <remarks>
    ///     Provides a convenient way to construct a fully specified pixel value in a single
    ///     expression, rather than requiring callers to set each field individually.
    /// </remarks>
    public Rgba32(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    ///     Determines whether this instance and another <see cref="Rgba32"/> have equal channel values.
    /// </summary>
    /// <param name="other">The other <see cref="Rgba32"/> to compare against.</param>
    /// <returns><see langword="true"/> if all four channels are equal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    ///     Value equality is provided so that pixel values can be directly compared in tests and
    ///     application code, matching the value-type semantics expected of a plain data carrier.
    /// </remarks>
    public readonly bool Equals(Rgba32 other) => R == other.R && G == other.G && B == other.B && A == other.A;

    /// <summary>
    ///     Determines whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="obj"/> is an <see cref="Rgba32"/> with equal
    ///     channel values; otherwise, <see langword="false"/>.
    /// </returns>
    public override readonly bool Equals(object? obj) => obj is Rgba32 other && Equals(other);

    /// <summary>
    ///     Returns a hash code for this instance based on its channel values.
    /// </summary>
    /// <returns>A hash code for this instance.</returns>
    public override readonly int GetHashCode() =>
        // Packing all four channels into a single int gives a fast, allocation-free hash that
        // is consistent with Equals (equal instances always produce equal packed values), without
        // depending on System.HashCode, which is not available on all supported target frameworks
        (R << 24) | (G << 16) | (B << 8) | A;

    /// <summary>
    ///     Determines whether two <see cref="Rgba32"/> instances have equal channel values.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Rgba32 left, Rgba32 right) => left.Equals(right);

    /// <summary>
    ///     Determines whether two <see cref="Rgba32"/> instances have different channel values.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are different; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Rgba32 left, Rgba32 right) => !left.Equals(right);

    /// <summary>
    ///     Parses a hexadecimal color string in the form <c>#RRGGBB</c> or <c>#AARRGGBB</c>
    ///     (case-insensitive) into an <see cref="Rgba32"/> value.
    /// </summary>
    /// <param name="s">The hexadecimal color string to parse.</param>
    /// <returns>The parsed <see cref="Rgba32"/> value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException">
    ///     Thrown when <paramref name="s"/> is not a valid <c>#RRGGBB</c> or <c>#AARRGGBB</c>
    ///     hexadecimal color string. Shorthand forms (<c>#RGB</c>, <c>#ARGB</c>) are not
    ///     supported and result in this exception.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///     <c>#RRGGBB</c> parses to red/green/blue with alpha implicitly <c>255</c> (fully
    ///     opaque). <c>#AARRGGBB</c> parses to alpha/red/green/blue directly. Both forms are
    ///     case-insensitive: <c>#ABCDEF</c> and <c>#abcdef</c> parse identically.
    ///     </para>
    ///     <para>
    ///     Delegates to the shared internal <see cref="TryParseCore"/> so that the accept/reject
    ///     behavior is provably identical to <see cref="TryParse"/>.
    ///     </para>
    /// </remarks>
    public static Rgba32 Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!TryParseCore(s, out var result, out var reason))
        {
            throw new FormatException(reason);
        }

        return result;
    }

    /// <summary>
    ///     Attempts to parse a hexadecimal color string in the form <c>#RRGGBB</c> or
    ///     <c>#AARRGGBB</c> (case-insensitive) into an <see cref="Rgba32"/> value. Never throws.
    /// </summary>
    /// <param name="s">The hexadecimal color string to parse, or <see langword="null"/>.</param>
    /// <param name="result">
    ///     When this method returns <see langword="true"/>, the parsed value; otherwise, the
    ///     default (<c>0,0,0,0</c>) <see cref="Rgba32"/> value.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="s"/> was successfully parsed as either
    ///     <c>#RRGGBB</c> or <c>#AARRGGBB</c>; <see langword="false"/> otherwise, including when
    ///     <paramref name="s"/> is <see langword="null"/>.
    /// </returns>
    public static bool TryParse(string? s, out Rgba32 result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        return TryParseCore(s, out result, out _);
    }

    /// <summary>
    ///     Shared accept/reject core used by both <see cref="Parse"/> and <see cref="TryParse"/>.
    ///     Never throws.
    /// </summary>
    /// <param name="s">A non-null candidate hexadecimal color string.</param>
    /// <param name="result">
    ///     The parsed color on success; the default (<c>0,0,0,0</c>) value on failure.
    /// </param>
    /// <param name="reason">
    ///     A human-readable failure message on failure; <see langword="null"/> on success. Used
    ///     verbatim as the <see cref="FormatException"/> message thrown by <see cref="Parse"/>.
    /// </param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> on failure.</returns>
    private static bool TryParseCore(string s, out Rgba32 result, out string? reason)
    {
        result = default;

        if (s.Length != 7 && s.Length != 9)
        {
            reason = "Rgba32 hex string must be '#RRGGBB' or '#AARRGGBB'.";
            return false;
        }

        if (s[0] != '#')
        {
            reason = "Rgba32 hex string must start with '#'.";
            return false;
        }

        // Reject any non-hex character before letting byte.TryParse succeed on partially valid
        // inputs (for example, byte.TryParse would accept whitespace or a sign character that we
        // deliberately do not permit in a hex color literal).
        for (var i = 1; i < s.Length; i++)
        {
            if (!IsHexDigit(s[i]))
            {
                reason = $"Rgba32 hex string contains a non-hexadecimal character '{s[i]}'.";
                return false;
            }
        }

        const NumberStyles hex = NumberStyles.HexNumber;
        var culture = CultureInfo.InvariantCulture;

        byte a, r, g, b;
        if (s.Length == 7)
        {
            if (!byte.TryParse(s.AsSpan(1, 2), hex, culture, out r) ||
                !byte.TryParse(s.AsSpan(3, 2), hex, culture, out g) ||
                !byte.TryParse(s.AsSpan(5, 2), hex, culture, out b))
            {
                reason = "Rgba32 hex string contains a non-hexadecimal character.";
                return false;
            }

            a = 255;
        }
        else
        {
            if (!byte.TryParse(s.AsSpan(1, 2), hex, culture, out a) ||
                !byte.TryParse(s.AsSpan(3, 2), hex, culture, out r) ||
                !byte.TryParse(s.AsSpan(5, 2), hex, culture, out g) ||
                !byte.TryParse(s.AsSpan(7, 2), hex, culture, out b))
            {
                reason = "Rgba32 hex string contains a non-hexadecimal character.";
                return false;
            }
        }

        result = new Rgba32(r, g, b, a);
        reason = null;
        return true;
    }

    /// <summary>Returns whether <paramref name="c"/> is a valid ASCII hexadecimal digit.</summary>
    private static bool IsHexDigit(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}

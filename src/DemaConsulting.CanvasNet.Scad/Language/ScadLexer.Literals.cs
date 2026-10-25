using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     Number and string literal scanning for <see cref="ScadLexer"/>.
/// </summary>
internal sealed partial class ScadLexer
{
    /// <summary>
    ///     Scans a numeric literal: digits with an optional fraction (including a trailing or leading dot)
    ///     and an optional exponent.
    /// </summary>
    /// <remarks>
    ///     The literal carries no sign and no base prefix. A letter directly after the number starts a new
    ///     token; overflow produces an infinity rather than an error.
    /// </remarks>
    private void ScanNumber()
    {
        var location = Here();
        var start = _pos;
        SkipDigits();

        // A dot after the integer part (or at the very start, for ".5") begins the fraction
        if (Current == '.')
        {
            _pos++;
            SkipDigits();
        }

        ScanExponent();

        var text = _source[start.._pos];
        var value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        Add(ScadTokenKind.Number, text, value, location);
    }

    /// <summary>
    ///     Advances over ASCII decimal digits.
    /// </summary>
    private void SkipDigits()
    {
        while (char.IsAsciiDigit(Current))
        {
            _pos++;
        }
    }

    /// <summary>
    ///     Consumes an exponent suffix, but only when <c>e</c> or <c>E</c> is followed by an optionally
    ///     signed digit; otherwise the letter is left to start the next token.
    /// </summary>
    private void ScanExponent()
    {
        if (Current is not ('e' or 'E'))
        {
            return;
        }

        var digitOffset = Peek(1) is '+' or '-' ? 2 : 1;
        if (char.IsAsciiDigit(Peek(digitOffset)))
        {
            _pos += digitOffset;
            SkipDigits();
        }
    }

    /// <summary>
    ///     Scans a string literal and decodes its escapes.
    /// </summary>
    /// <exception cref="ScadSyntaxException">
    ///     Thrown at the opening quote when the string is unterminated, or at the backslash for a bad escape.
    /// </exception>
    private void ScanString()
    {
        var location = Here();
        _pos++;
        var value = new StringBuilder();

        // Read characters until the closing quote; raw line breaks are part of the value
        while (true)
        {
            if (_pos >= _source.Length)
            {
                throw Error("Unterminated string literal", location);
            }

            var c = _source[_pos];
            if (c == '"')
            {
                _pos++;
                break;
            }

            if (c == '\\')
            {
                AppendEscape(value, location);
            }
            else
            {
                AppendRawCharacter(value);
            }
        }

        Add(ScadTokenKind.String, value.ToString(), 0, location);
    }

    /// <summary>
    ///     Appends one ordinary string character, keeping line breaks verbatim and counting lines.
    /// </summary>
    /// <param name="value">The string being built.</param>
    private void AppendRawCharacter(StringBuilder value)
    {
        var start = _pos;
        if (TryConsumeLineBreak())
        {
            value.Append(_source, start, _pos - start);
        }
        else
        {
            value.Append(_source[_pos]);
            _pos++;
        }
    }

    /// <summary>
    ///     Decodes the escape sequence at the current backslash and appends its value.
    /// </summary>
    /// <param name="value">The string being built.</param>
    /// <param name="stringStart">The location of the opening quote (reported if the input ends mid-escape).</param>
    /// <exception cref="ScadSyntaxException">Thrown, at the backslash, for any malformed or unknown escape.</exception>
    private void AppendEscape(StringBuilder value, ScadSourceLocation stringStart)
    {
        var escapeLocation = Here();
        _pos++;
        if (_pos >= _source.Length)
        {
            throw Error("Unterminated string literal", stringStart);
        }

        var letter = _source[_pos];
        _pos++;

        // Single-character escapes first, then the hexadecimal code point forms
        var simple = SimpleEscape(letter);
        if (simple.HasValue)
        {
            value.Append(simple.Value);
            return;
        }

        var digits = HexDigitCount(letter);
        if (digits == 0)
        {
            throw Error($"Invalid escape sequence '\\{letter}'", escapeLocation);
        }

        value.Append(ReadCodePoint(letter, digits, escapeLocation));
    }

    /// <summary>
    ///     Maps a single-character escape letter to the character it denotes.
    /// </summary>
    /// <param name="letter">The character after the backslash.</param>
    /// <returns>The decoded character, or null when the letter is not a simple escape.</returns>
    private static char? SimpleEscape(char letter) => letter switch
    {
        'n' => '\n',
        't' => '\t',
        'r' => '\r',
        '"' => '"',
        '\\' => '\\',
        _ => null,
    };

    /// <summary>
    ///     Gets the number of hexadecimal digits a code point escape letter requires.
    /// </summary>
    /// <param name="letter">The character after the backslash.</param>
    /// <returns>2 for <c>x</c>, 4 for <c>u</c>, 6 for <c>U</c>, otherwise 0.</returns>
    private static int HexDigitCount(char letter) => letter switch
    {
        'x' => 2,
        'u' => 4,
        'U' => 6,
        _ => 0,
    };

    /// <summary>
    ///     Reads the hexadecimal digits of a code point escape and converts them to text.
    /// </summary>
    /// <param name="letter">The escape letter (<c>x</c>, <c>u</c> or <c>U</c>), for diagnostics.</param>
    /// <param name="digits">The exact number of hexadecimal digits required.</param>
    /// <param name="escapeLocation">The location of the backslash.</param>
    /// <returns>The code point as UTF-16 text (a surrogate pair above U+FFFF).</returns>
    /// <exception cref="ScadSyntaxException">
    ///     Thrown when the digits are missing or not hexadecimal, or the value is a surrogate or above U+10FFFF.
    /// </exception>
    private string ReadCodePoint(char letter, int digits, ScadSourceLocation escapeLocation)
    {
        // Exactly the required number of ASCII hexadecimal digits must follow
        if (_pos + digits > _source.Length || !IsAllHex(_source.AsSpan(_pos, digits)))
        {
            throw Error(string.Create(CultureInfo.InvariantCulture, $"Invalid escape sequence '\\{letter}': expected {digits} hexadecimal digits"), escapeLocation);
        }

        var codePoint = int.Parse(_source.AsSpan(_pos, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        _pos += digits;

        // Surrogates and values beyond the Unicode range cannot be represented as text
        if (codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF)
        {
            throw Error($"Invalid escape sequence '\\{letter}': not a valid Unicode scalar value", escapeLocation);
        }

        return char.ConvertFromUtf32(codePoint);
    }

    /// <summary>
    ///     Determines whether every character is an ASCII hexadecimal digit.
    /// </summary>
    /// <param name="text">The characters to test.</param>
    /// <returns><see langword="true"/> when all characters are hexadecimal digits.</returns>
    private static bool IsAllHex(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}

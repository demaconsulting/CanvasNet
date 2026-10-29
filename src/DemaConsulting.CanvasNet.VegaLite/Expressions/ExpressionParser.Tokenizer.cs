using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The kind of a token of an expression.
/// </summary>
internal enum ExpressionTokenKind
{
    /// <summary>The end of the expression text.</summary>
    End,

    /// <summary>A number literal.</summary>
    Number,

    /// <summary>A string literal.</summary>
    String,

    /// <summary>An identifier or a keyword.</summary>
    Identifier,

    /// <summary>An operator or a delimiter.</summary>
    Punctuator,

    /// <summary>A regular expression literal.</summary>
    RegExp,
}

/// <summary>
///     One token of an expression.
/// </summary>
/// <param name="Kind">The kind.</param>
/// <param name="Offset">The zero-based UTF-16 offset of the first character.</param>
/// <param name="Length">The number of characters of the token in the expression text.</param>
/// <param name="Text">The identifier, punctuator, string value or regular expression pattern.</param>
/// <param name="Number">The value of a number literal.</param>
/// <param name="Flags">The flags of a regular expression literal.</param>
internal readonly record struct ExpressionToken(
    ExpressionTokenKind Kind,
    int Offset,
    int Length,
    string Text,
    double Number,
    string Flags);

/// <summary>
///     The tokenizer half of the expression parser (a partial file of the parser unit).
/// </summary>
/// <remarks>
///     The tokenizer reads the whole text in one pass with an index and no recursion, so its cost and memory are linear
///     in the length that the parser already checked against <see cref="VegaLiteLimits.MaxExpressionLength"/>. Operators
///     that the language rejects (assignments, increments, <c>**</c>, <c>??</c>, <c>=&gt;</c>) are still tokenized as
///     one token so the parser can name them in its error. A <c>/</c> starts a regular expression literal exactly when
///     the previous token cannot end an operand.
/// </remarks>
internal sealed partial class ExpressionParser
{
    /// <summary>The punctuators of three characters.</summary>
    private static readonly string[] ThreeCharacterPunctuators = ["===", "!==", ">>>", "**=", "<<=", ">>=", "..."];

    /// <summary>The punctuators of two characters.</summary>
    private static readonly string[] TwoCharacterPunctuators =
    [
        "&&", "||", "==", "!=", "<=", ">=", "<<", ">>", "++", "--", "**", "??", "=>", "+=", "-=", "*=", "/=", "%=", "&=", "|=",
        "^=", "?.",
    ];

    /// <summary>The punctuators of one character.</summary>
    private const string OneCharacterPunctuators = "+-*/%<>!~?:.,()[]{}&|^=;";

    /// <summary>
    ///     Splits the expression text into tokens.
    /// </summary>
    /// <returns>The tokens, ending with an end token.</returns>
    /// <exception cref="VegaLiteSpecException">Thrown for a character or literal that is not valid.</exception>
    private List<ExpressionToken> Tokenize()
    {
        var tokens = new List<ExpressionToken>();
        var position = 0;
        while (true)
        {
            while (position < _source.Length && (char.IsWhiteSpace(_source[position]) || _source[position] == '\uFEFF'))
            {
                position++;
            }

            if (position >= _source.Length)
            {
                tokens.Add(new ExpressionToken(ExpressionTokenKind.End, _source.Length, 0, string.Empty, 0, string.Empty));
                return tokens;
            }

            var c = _source[position];
            var previous = tokens.Count > 0 ? tokens[^1] : default;
            ExpressionToken token;
            if (IsIdentifierStart(c))
            {
                token = ReadIdentifier(position);
            }
            else if (char.IsAsciiDigit(c) || (c == '.' && position + 1 < _source.Length && char.IsAsciiDigit(_source[position + 1])))
            {
                token = ReadNumber(position);
            }
            else if (c is '\'' or '"')
            {
                token = ReadString(position);
            }
            else if (c == '/' && (tokens.Count == 0 || RegExpAllowedAfter(previous)))
            {
                token = ReadRegExp(position);
            }
            else
            {
                token = ReadPunctuator(position);
            }

            tokens.Add(token);
            position = token.Offset + token.Length;
        }
    }

    /// <summary>
    ///     Tells whether a regular expression literal may follow a token, that is whether the token cannot end an operand.
    /// </summary>
    /// <param name="previous">The previous token.</param>
    /// <returns><see langword="true"/> when a <c>/</c> after the token starts a regular expression.</returns>
    private static bool RegExpAllowedAfter(ExpressionToken previous) => previous.Kind switch
    {
        ExpressionTokenKind.Identifier => RegExpKeywords.Contains(previous.Text),
        ExpressionTokenKind.Number or ExpressionTokenKind.String or ExpressionTokenKind.RegExp => false,
        ExpressionTokenKind.Punctuator => previous.Text is not (")" or "]" or "}"),
        _ => true,
    };

    /// <summary>
    ///     The keywords after which an operand follows, so a <c>/</c> starts a regular expression (the deferred <c>in</c> and
    ///     <c>instanceof</c> operators, the prefix operators and the statement keywords); other identifiers end an operand.
    /// </summary>
    private static readonly HashSet<string> RegExpKeywords = new(StringComparer.Ordinal)
    {
        "in", "instanceof", "typeof", "void", "delete", "new", "return", "case", "throw", "else", "do", "yield", "await",
    };

    /// <summary>
    ///     Tells whether a character can start an identifier.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a letter, underscore or dollar sign.</returns>
    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c is '_' or '$';

    /// <summary>
    ///     Tells whether a character can continue an identifier.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a letter, digit, underscore or dollar sign.</returns>
    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    /// <summary>
    ///     Reads an identifier.
    /// </summary>
    /// <param name="start">The offset of its first character.</param>
    /// <returns>The token.</returns>
    private ExpressionToken ReadIdentifier(int start)
    {
        var end = start + 1;
        while (end < _source.Length && IsIdentifierPart(_source[end]))
        {
            end++;
        }

        return new ExpressionToken(ExpressionTokenKind.Identifier, start, end - start, _source[start..end], 0, string.Empty);
    }

    /// <summary>
    ///     Reads a decimal or hexadecimal number literal.
    /// </summary>
    /// <param name="start">The offset of its first character.</param>
    /// <returns>The token.</returns>
    private ExpressionToken ReadNumber(int start)
    {
        var end = start;
        double value;
        if (_source[start] == '0' && start + 1 < _source.Length && _source[start + 1] is 'x' or 'X')
        {
            // A hexadecimal literal needs at least one digit; the value is accumulated so it cannot overflow an integer
            end = start + 2;
            value = 0;
            while (end < _source.Length && char.IsAsciiHexDigit(_source[end]))
            {
                value = (value * 16) + HexValue(_source[end]);
                end++;
            }

            if (end == start + 2)
            {
                throw Fail(start, "a hexadecimal literal needs at least one digit");
            }
        }
        else
        {
            while (end < _source.Length && char.IsAsciiDigit(_source[end]))
            {
                end++;
            }

            if (end < _source.Length && _source[end] == '.')
            {
                end++;
                while (end < _source.Length && char.IsAsciiDigit(_source[end]))
                {
                    end++;
                }
            }

            if (end < _source.Length && _source[end] is 'e' or 'E')
            {
                end = ReadExponent(start, end);
            }

            value = double.Parse(_source.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // A number must not run into an identifier ("1a") and must be finite ("1e999")
        if (end < _source.Length && IsIdentifierPart(_source[end]))
        {
            throw Fail(start, "a number literal must not be followed directly by a letter or digit");
        }

        if (!double.IsFinite(value))
        {
            throw Fail(start, "the number literal is too large");
        }

        return new ExpressionToken(ExpressionTokenKind.Number, start, end - start, _source[start..end], value, string.Empty);
    }

    /// <summary>
    ///     Reads the exponent part of a decimal literal.
    /// </summary>
    /// <param name="start">The offset of the literal.</param>
    /// <param name="exponent">The offset of the <c>e</c>.</param>
    /// <returns>The offset after the exponent.</returns>
    private int ReadExponent(int start, int exponent)
    {
        var end = exponent + 1;
        if (end < _source.Length && _source[end] is '+' or '-')
        {
            end++;
        }

        var digits = end;
        while (end < _source.Length && char.IsAsciiDigit(_source[end]))
        {
            end++;
        }

        if (end == digits)
        {
            throw Fail(start, "the exponent of a number literal needs at least one digit");
        }

        return end;
    }

    /// <summary>
    ///     Reads a string literal with JavaScript escapes.
    /// </summary>
    /// <param name="start">The offset of the opening quote.</param>
    /// <returns>The token.</returns>
    private ExpressionToken ReadString(int start)
    {
        var quote = _source[start];
        var value = new StringBuilder();
        var position = start + 1;
        while (true)
        {
            if (position >= _source.Length || _source[position] is '\n' or '\r')
            {
                throw Fail(start, "the string literal is not terminated");
            }

            var c = _source[position];
            if (c == quote)
            {
                return new ExpressionToken(ExpressionTokenKind.String, start, position + 1 - start, value.ToString(), 0, string.Empty);
            }

            if (c != '\\')
            {
                value.Append(c);
                position++;
                continue;
            }

            position = ReadEscape(position, value);
        }
    }

    /// <summary>
    ///     Reads one escape sequence of a string literal.
    /// </summary>
    /// <param name="backslash">The offset of the backslash.</param>
    /// <param name="value">The string value being built.</param>
    /// <returns>The offset after the escape sequence.</returns>
    private int ReadEscape(int backslash, StringBuilder value)
    {
        if (backslash + 1 >= _source.Length)
        {
            throw Fail(backslash, "the string literal is not terminated");
        }

        var e = _source[backslash + 1];
        var next = backslash + 2;
        switch (e)
        {
            case 'n':
                value.Append('\n');
                return next;
            case 'r':
                value.Append('\r');
                return next;
            case 't':
                value.Append('\t');
                return next;
            case 'b':
                value.Append('\b');
                return next;
            case 'f':
                value.Append('\f');
                return next;
            case 'v':
                value.Append('\v');
                return next;
            case '0' when next >= _source.Length || !char.IsAsciiDigit(_source[next]):
                value.Append('\0');
                return next;
            case >= '0' and <= '9':
                throw Fail(backslash, "octal escape sequences are not supported in string literals");
            case 'x':
                return ReadHexEscape(backslash, next, 2, value);
            case 'u':
                return ReadUnicodeEscape(backslash, next, value);
            case '\r':
                return next < _source.Length && _source[next] == '\n' ? next + 1 : next;
            case '\n' or '\u2028' or '\u2029':
                return next;
            default:
                value.Append(e);
                return next;
        }
    }

    /// <summary>
    ///     Reads the digits of a <c>\x</c> or <c>\u</c> escape.
    /// </summary>
    /// <param name="backslash">The offset of the backslash.</param>
    /// <param name="digits">The offset of the first digit.</param>
    /// <param name="count">The number of digits.</param>
    /// <param name="value">The string value being built.</param>
    /// <returns>The offset after the escape sequence.</returns>
    private int ReadHexEscape(int backslash, int digits, int count, StringBuilder value)
    {
        var code = 0;
        for (var i = 0; i < count; i++)
        {
            if (digits + i >= _source.Length || !char.IsAsciiHexDigit(_source[digits + i]))
            {
                throw Fail(backslash, "the escape sequence needs hexadecimal digits");
            }

            code = (code * 16) + HexValue(_source[digits + i]);
        }

        value.Append((char)code);
        return digits + count;
    }

    /// <summary>
    ///     Reads a <c>\uHHHH</c> or <c>\u{H...}</c> escape.
    /// </summary>
    /// <param name="backslash">The offset of the backslash.</param>
    /// <param name="afterU">The offset after the <c>u</c>.</param>
    /// <param name="value">The string value being built.</param>
    /// <returns>The offset after the escape sequence.</returns>
    private int ReadUnicodeEscape(int backslash, int afterU, StringBuilder value)
    {
        if (afterU >= _source.Length || _source[afterU] != '{')
        {
            return ReadHexEscape(backslash, afterU, 4, value);
        }

        var position = afterU + 1;
        var code = 0;
        var digits = 0;
        while (position < _source.Length && char.IsAsciiHexDigit(_source[position]))
        {
            code = (code * 16) + HexValue(_source[position]);
            position++;
            digits++;
            if (code > 0x10FFFF)
            {
                throw Fail(backslash, "the code point of the escape sequence is too large");
            }
        }

        if (digits == 0 || position >= _source.Length || _source[position] != '}' || code is >= 0xD800 and <= 0xDFFF)
        {
            throw Fail(backslash, "the escape sequence is not a valid code point");
        }

        value.Append(char.ConvertFromUtf32(code));
        return position + 1;
    }

    /// <summary>
    ///     Reads a regular expression literal.
    /// </summary>
    /// <param name="start">The offset of the opening slash.</param>
    /// <returns>The token.</returns>
    private ExpressionToken ReadRegExp(int start)
    {
        var position = start + 1;
        var inClass = false;
        while (true)
        {
            if (position >= _source.Length || _source[position] is '\n' or '\r')
            {
                throw Fail(start, "the regular expression literal is not terminated");
            }

            var c = _source[position];
            if (c == '\\' && position + 1 < _source.Length)
            {
                position += 2;
                continue;
            }

            if (c == '[')
            {
                inClass = true;
            }
            else if (c == ']')
            {
                inClass = false;
            }
            else if (c == '/' && !inClass)
            {
                break;
            }

            position++;
        }

        var pattern = _source[(start + 1)..position];
        var flagsEnd = position + 1;
        while (flagsEnd < _source.Length && IsIdentifierPart(_source[flagsEnd]))
        {
            flagsEnd++;
        }

        if (pattern.Length == 0)
        {
            throw Fail(start, "the regular expression literal is empty");
        }

        return new ExpressionToken(
            ExpressionTokenKind.RegExp, start, flagsEnd - start, pattern, 0, _source[(position + 1)..flagsEnd]);
    }

    /// <summary>
    ///     Gets the value of a hexadecimal digit.
    /// </summary>
    /// <param name="c">The digit; must satisfy <see cref="char.IsAsciiHexDigit"/>.</param>
    /// <returns>The value from 0 to 15.</returns>
    private static int HexValue(char c) => char.IsAsciiDigit(c) ? c - '0' : (c | 0x20) - 'a' + 10;

    /// <summary>
    ///     Reads an operator or delimiter, preferring the longest one.
    /// </summary>
    /// <param name="start">The offset of its first character.</param>
    /// <returns>The token.</returns>
    private ExpressionToken ReadPunctuator(int start)
    {
        var three = ThreeCharacterPunctuators.FirstOrDefault(p => string.CompareOrdinal(_source, start, p, 0, 3) == 0);
        if (three is not null)
        {
            return Punctuator(start, three);
        }

        // "?." followed by a digit is a conditional operator and a number such as ".5"
        var two = TwoCharacterPunctuators.FirstOrDefault(
            p => string.CompareOrdinal(_source, start, p, 0, 2) == 0
                && !(p == "?." && start + 2 < _source.Length && char.IsAsciiDigit(_source[start + 2])));
        if (two is not null)
        {
            return Punctuator(start, two);
        }

        if (OneCharacterPunctuators.Contains(_source[start], StringComparison.Ordinal))
        {
            return Punctuator(start, _source[start].ToString());
        }

        throw Fail(start, $"the character '{_source[start]}' is not valid in an expression");
    }

    /// <summary>
    ///     Creates a punctuator token.
    /// </summary>
    /// <param name="start">The offset.</param>
    /// <param name="text">The text.</param>
    /// <returns>The token.</returns>
    private static ExpressionToken Punctuator(int start, string text) =>
        new(ExpressionTokenKind.Punctuator, start, text.Length, text, 0, string.Empty);
}

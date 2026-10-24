// cspell:ignore FEFF
namespace DemaConsulting.CanvasNet.Scad.Language;

/// <summary>
///     Converts OpenSCAD-language source text into a list of <see cref="ScadToken"/> values.
/// </summary>
/// <remarks>
///     <para>
///         The lexer exists as a separate stage so that the parser can look ahead over a plain token list and
///         so that every lexical error is reported (fail-fast, as a <see cref="ScadSyntaxException"/>) before
///         any grammatical analysis begins. It is iterative (never recursive) and therefore safe on hostile
///         input of any shape; the input size is bounded by the caller.
///     </para>
///     <para>
///         Locations are one-based; columns count UTF-16 code units, a tab is one column, and
///         <c>\r\n</c>, <c>\n</c> and a lone <c>\r</c> each end exactly one line. A leading byte-order mark is
///         skipped without consuming a column. Instances are single-use and not thread-safe; the static
///         <see cref="Tokenize"/> entry point is safe for concurrent use.
///     </para>
///     <para>
///         This file holds the dispatch loop, trivia (whitespace and comments), identifiers, keywords,
///         directives and operators; numbers and strings live in <c>ScadLexer.Literals.cs</c>.
///     </para>
/// </remarks>
internal sealed partial class ScadLexer
{
    /// <summary>The reserved words (excluding the <c>include</c>/<c>use</c> directives), keyed by exact lexeme.</summary>
    private static readonly Dictionary<string, ScadTokenKind> Keywords = new(StringComparer.Ordinal)
    {
        ["module"] = ScadTokenKind.Module,
        ["function"] = ScadTokenKind.Function,
        ["if"] = ScadTokenKind.If,
        ["else"] = ScadTokenKind.Else,
        ["for"] = ScadTokenKind.For,
        ["intersection_for"] = ScadTokenKind.IntersectionFor,
        ["let"] = ScadTokenKind.Let,
        ["each"] = ScadTokenKind.Each,
        ["assert"] = ScadTokenKind.Assert,
        ["echo"] = ScadTokenKind.Echo,
        ["true"] = ScadTokenKind.True,
        ["false"] = ScadTokenKind.False,
        ["undef"] = ScadTokenKind.Undef,
    };

    /// <summary>The single-character operators and punctuation, keyed by character.</summary>
    private static readonly Dictionary<char, ScadTokenKind> SingleCharOperators = new()
    {
        ['('] = ScadTokenKind.LeftParen,
        [')'] = ScadTokenKind.RightParen,
        ['{'] = ScadTokenKind.LeftBrace,
        ['}'] = ScadTokenKind.RightBrace,
        ['['] = ScadTokenKind.LeftBracket,
        [']'] = ScadTokenKind.RightBracket,
        [';'] = ScadTokenKind.Semicolon,
        [','] = ScadTokenKind.Comma,
        ['.'] = ScadTokenKind.Dot,
        [':'] = ScadTokenKind.Colon,
        ['?'] = ScadTokenKind.Question,
        ['='] = ScadTokenKind.Assign,
        ['<'] = ScadTokenKind.Less,
        ['>'] = ScadTokenKind.Greater,
        ['+'] = ScadTokenKind.Plus,
        ['-'] = ScadTokenKind.Minus,
        ['*'] = ScadTokenKind.Star,
        ['/'] = ScadTokenKind.Slash,
        ['%'] = ScadTokenKind.Percent,
        ['^'] = ScadTokenKind.Caret,
        ['!'] = ScadTokenKind.Bang,
        ['#'] = ScadTokenKind.Hash,
    };

    /// <summary>The source text being tokenized.</summary>
    private readonly string _source;

    /// <summary>The source name placed in every location (may be null).</summary>
    private readonly string? _file;

    /// <summary>The tokens produced so far.</summary>
    private readonly List<ScadToken> _tokens = [];

    /// <summary>The index of the next character to read.</summary>
    private int _pos;

    /// <summary>The one-based number of the line containing <see cref="_pos"/>.</summary>
    private int _line = 1;

    /// <summary>The index of the first character of the current line (columns are relative to it).</summary>
    private int _lineStart;

    /// <summary>
    ///     Initializes a lexer over the given source, skipping a leading byte-order mark.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="file">The source name used in locations.</param>
    private ScadLexer(string source, string? file)
    {
        _source = source;
        _file = file;

        // A BOM is not part of the program text: skip it and start column counting after it
        if (source.Length > 0 && source[0] == '\uFEFF')
        {
            _pos = 1;
            _lineStart = 1;
        }
    }

    /// <summary>
    ///     Tokenizes a complete source text.
    /// </summary>
    /// <param name="source">The source text. Must not be null.</param>
    /// <param name="file">The source name placed in token locations; may be null.</param>
    /// <returns>The tokens in source order, always ending with exactly one <see cref="ScadTokenKind.EndOfFile"/> token.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ScadSyntaxException">Thrown at the first lexical error, with its location.</exception>
    public static IReadOnlyList<ScadToken> Tokenize(string source, string? file)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ScadLexer(source, file).Run();
    }

    /// <summary>
    ///     Runs the scan loop to completion.
    /// </summary>
    /// <returns>The token list including the end-of-file token.</returns>
    private List<ScadToken> Run()
    {
        // Alternate between skipping trivia and scanning one token until the input is exhausted
        while (true)
        {
            SkipTrivia();
            if (_pos >= _source.Length)
            {
                break;
            }

            ScanToken();
        }

        // The end-of-file token sits just after the last character
        Add(ScadTokenKind.EndOfFile, null, 0, Here());
        return _tokens;
    }

    /// <summary>
    ///     Gets the current character, or NUL when the input is exhausted.
    /// </summary>
    private char Current => Peek(0);

    /// <summary>
    ///     Looks ahead without consuming.
    /// </summary>
    /// <param name="offset">The distance from the current position.</param>
    /// <returns>The character, or NUL when beyond the end of the input.</returns>
    private char Peek(int offset) => _pos + offset < _source.Length ? _source[_pos + offset] : '\0';

    /// <summary>
    ///     Gets the location of the current position.
    /// </summary>
    /// <returns>The one-based line and column.</returns>
    private ScadSourceLocation Here() => new(_file, _line, _pos - _lineStart + 1);

    /// <summary>
    ///     Appends a token to the output.
    /// </summary>
    /// <param name="kind">The token kind.</param>
    /// <param name="text">The token text.</param>
    /// <param name="number">The numeric value.</param>
    /// <param name="location">The token location.</param>
    private void Add(ScadTokenKind kind, string? text, double number, ScadSourceLocation location) =>
        _tokens.Add(new ScadToken(kind, text, number, location));

    /// <summary>
    ///     Creates a syntax error at a location.
    /// </summary>
    /// <param name="description">The description without location prefix.</param>
    /// <param name="location">The error location.</param>
    /// <returns>The exception to throw.</returns>
    private static ScadSyntaxException Error(string description, ScadSourceLocation location) =>
        new(description, location);

    /// <summary>
    ///     Consumes a line break (<c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>) at the current position, if any,
    ///     and advances the line counter.
    /// </summary>
    /// <returns><see langword="true"/> when a line break was consumed.</returns>
    private bool TryConsumeLineBreak()
    {
        var c = Current;
        if (c != '\n' && c != '\r')
        {
            return false;
        }

        // A CR LF pair is a single line break
        _pos += c == '\r' && Peek(1) == '\n' ? 2 : 1;
        _line++;
        _lineStart = _pos;
        return true;
    }

    /// <summary>
    ///     Skips whitespace, line breaks and comments.
    /// </summary>
    private void SkipTrivia()
    {
        while (SkipOneTrivia())
        {
            // Each iteration consumes one whitespace character, line break or comment
        }
    }

    /// <summary>
    ///     Skips one unit of trivia at the current position.
    /// </summary>
    /// <returns><see langword="true"/> when something was skipped.</returns>
    private bool SkipOneTrivia()
    {
        if (_pos >= _source.Length)
        {
            return false;
        }

        var c = _source[_pos];
        if (c is ' ' or '\t' or '\v' or '\f')
        {
            _pos++;
            return true;
        }

        if (TryConsumeLineBreak())
        {
            return true;
        }

        // Only "//" and "/*" start comments; a lone "/" is the divide operator
        if (c == '/' && Peek(1) == '/')
        {
            SkipLineComment();
            return true;
        }

        if (c == '/' && Peek(1) == '*')
        {
            SkipBlockComment();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Skips a line comment up to (not including) the line end.
    /// </summary>
    private void SkipLineComment()
    {
        while (_pos < _source.Length && _source[_pos] != '\n' && _source[_pos] != '\r')
        {
            _pos++;
        }
    }

    /// <summary>
    ///     Skips a (non-nesting) block comment, counting the lines it spans.
    /// </summary>
    /// <exception cref="ScadSyntaxException">Thrown, at the opening marker, when the comment is not closed.</exception>
    private void SkipBlockComment()
    {
        // The search starts after "/*" so that "/*/" does not close itself
        var start = Here();
        var end = _source.IndexOf("*/", _pos + 2, StringComparison.Ordinal);
        if (end < 0)
        {
            throw Error("Unterminated block comment", start);
        }

        // Walk the comment body so that embedded line breaks advance the line counter
        _pos += 2;
        while (_pos < end + 2)
        {
            if (!TryConsumeLineBreak())
            {
                _pos++;
            }
        }
    }

    /// <summary>
    ///     Scans one token starting at a non-trivia character.
    /// </summary>
    private void ScanToken()
    {
        var c = _source[_pos];
        if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(Peek(1))))
        {
            ScanNumber();
        }
        else if (c == '"')
        {
            ScanString();
        }
        else if (IsIdentifierStart(c))
        {
            ScanWord();
        }
        else if (c == '$')
        {
            ScanSpecialVariable();
        }
        else
        {
            ScanOperator();
        }
    }

    /// <summary>
    ///     Determines whether a character can start an ordinary identifier (ASCII letter or underscore).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a letter or underscore.</returns>
    private static bool IsIdentifierStart(char c) => char.IsAsciiLetter(c) || c == '_';

    /// <summary>
    ///     Determines whether a character can continue an identifier (ASCII letter, digit or underscore).
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a letter, digit or underscore.</returns>
    private static bool IsIdentifierPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    /// <summary>
    ///     Scans an identifier, keyword or directive.
    /// </summary>
    private void ScanWord()
    {
        var location = Here();
        var start = _pos;
        while (IsIdentifierPart(Current))
        {
            _pos++;
        }

        var text = _source[start.._pos];

        // include and use introduce a path token, not an identifier
        if (text is "include" or "use")
        {
            ScanDirective(text, location);
            return;
        }

        var kind = Keywords.TryGetValue(text, out var keyword) ? keyword : ScadTokenKind.Identifier;
        Add(kind, text, 0, location);
    }

    /// <summary>
    ///     Scans a special variable such as <c>$fn</c>.
    /// </summary>
    /// <exception cref="ScadSyntaxException">Thrown when the <c>$</c> is not followed by a name.</exception>
    private void ScanSpecialVariable()
    {
        var location = Here();
        var start = _pos;
        _pos++;
        while (IsIdentifierPart(Current))
        {
            _pos++;
        }

        // A lone "$" has no name and is never valid
        if (_pos == start + 1)
        {
            throw Error("Expected a name after '$'", location);
        }

        Add(ScadTokenKind.Identifier, _source[start.._pos], 0, location);
    }

    /// <summary>
    ///     Scans the angle-bracketed path of an <c>include</c> or <c>use</c> directive.
    /// </summary>
    /// <param name="word">The directive keyword (<c>include</c> or <c>use</c>).</param>
    /// <param name="location">The location of the keyword.</param>
    /// <exception cref="ScadSyntaxException">Thrown when the path is missing, unterminated or empty.</exception>
    private void ScanDirective(string word, ScadSourceLocation location)
    {
        // The opening bracket may follow whitespace and comments
        SkipTrivia();
        if (_pos >= _source.Length || _source[_pos] != '<')
        {
            throw Error($"Expected '<' after '{word}'", location);
        }

        // The path runs to the closing bracket and may not span lines
        var start = _pos + 1;
        var end = start;
        while (end < _source.Length && _source[end] is not ('>' or '\r' or '\n'))
        {
            end++;
        }

        if (end >= _source.Length || _source[end] != '>')
        {
            throw Error("Unterminated include/use path", location);
        }

        if (end == start)
        {
            throw Error("Empty include/use path", location);
        }

        Add(word == "include" ? ScadTokenKind.Include : ScadTokenKind.Use, _source[start..end], 0, location);
        _pos = end + 1;
    }

    /// <summary>
    ///     Maps a two-character operator, if the pair forms one.
    /// </summary>
    /// <param name="first">The first character.</param>
    /// <param name="second">The second character.</param>
    /// <returns>The operator kind, or null when the pair is not an operator.</returns>
    private static ScadTokenKind? TwoCharOperator(char first, char second) => (first, second) switch
    {
        ('=', '=') => ScadTokenKind.EqualEqual,
        ('!', '=') => ScadTokenKind.NotEqual,
        ('<', '=') => ScadTokenKind.LessEqual,
        ('>', '=') => ScadTokenKind.GreaterEqual,
        ('&', '&') => ScadTokenKind.AndAnd,
        ('|', '|') => ScadTokenKind.OrOr,
        _ => null,
    };

    /// <summary>
    ///     Scans an operator or punctuation token using longest match.
    /// </summary>
    /// <exception cref="ScadSyntaxException">Thrown for any character that cannot start a token.</exception>
    private void ScanOperator()
    {
        var location = Here();
        var c = _source[_pos];

        // Two-character operators win over their one-character prefixes
        var two = TwoCharOperator(c, Peek(1));
        if (two.HasValue)
        {
            Add(two.Value, _source.Substring(_pos, 2), 0, location);
            _pos += 2;
            return;
        }

        if (SingleCharOperators.TryGetValue(c, out var kind))
        {
            Add(kind, c.ToString(), 0, location);
            _pos++;
            return;
        }

        // A lone & or | is a likely typo for the logical operators, so hint at them
        var hint = c is '&' or '|' ? $"; did you mean '{c}{c}'?" : string.Empty;
        throw Error($"Unexpected character '{c}'{hint}", location);
    }
}

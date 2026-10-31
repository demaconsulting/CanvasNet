// Portions derived from d3-time (ISC), d3-time-format (ISC), d3-scale (ISC) and Vega (BSD-3-Clause); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The legacy (non-ISO) date forms of <see cref="JsDateParser"/>.
/// </summary>
internal static partial class JsDateParser
{
    /// <summary>The longest run of digits a number token may have (it must fit an <see cref="int"/>).</summary>
    private const int MaxNumberDigits = 9;

    /// <summary>
    ///     Parses one of the documented legacy forms.
    /// </summary>
    /// <param name="text">The text, at most <see cref="MaxLength"/> characters.</param>
    /// <param name="calendar">The calendar whose zone is used when the text has no zone designator.</param>
    /// <returns>The instant, or NaN when the text is not one of the forms.</returns>
    private static double ParseLegacy(string text, TimeCalendar calendar)
    {
        var tokens = Tokenize(text);
        return tokens is null ? double.NaN : new LegacyReader(text, tokens).Read(calendar);
    }

    /// <summary>
    ///     Splits the text into numbers, words and the symbols <c>, / : - + .</c>; white space and parenthesized comments are skipped.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The tokens, or null when the text has a character or a number that no form uses.</returns>
    private static LegacyToken[]? Tokenize(string text)
    {
        var tokens = new List<LegacyToken>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (TimeFormat.IsAsciiDigit(c))
            {
                if (!ReadNumber(text, ref i, tokens))
                {
                    return null;
                }
            }
            else if (IsAsciiLetter(c))
            {
                var start = i;
                while (i < text.Length && IsAsciiLetter(text[i]))
                {
                    i++;
                }

                tokens.Add(new LegacyToken(LegacyKind.Word, start, i - start, 0, '\0'));
            }
            else if (c == '(')
            {
                if (!SkipComment(text, ref i))
                {
                    return null;
                }
            }
            else if (c is ',' or '/' or ':' or '-' or '+' or '.')
            {
                tokens.Add(new LegacyToken(LegacyKind.Symbol, i, 1, 0, c));
                i++;
            }
            else
            {
                return null;
            }
        }

        return [.. tokens];
    }

    /// <summary>
    ///     Reads a run of digits as a number token.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The position of the first digit; advanced past the digits.</param>
    /// <param name="tokens">The token list.</param>
    /// <returns>False when the number has too many digits.</returns>
    private static bool ReadNumber(string text, ref int i, List<LegacyToken> tokens)
    {
        var start = i;
        var value = 0;
        while (i < text.Length && TimeFormat.IsAsciiDigit(text[i]))
        {
            if (i - start >= MaxNumberDigits)
            {
                return false;
            }

            value = (value * 10) + (text[i] - '0');
            i++;
        }

        tokens.Add(new LegacyToken(LegacyKind.Number, start, i - start, value, '\0'));
        return true;
    }

    /// <summary>
    ///     Skips a parenthesized comment, which may nest.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="i">The position of the opening parenthesis; advanced past the closing one.</param>
    /// <returns>False when the comment is not closed.</returns>
    private static bool SkipComment(string text, ref int i)
    {
        var depth = 0;
        while (i < text.Length)
        {
            var delta = text[i] == '(' ? 1 : 0;
            if (text[i] == ')')
            {
                delta = -1;
            }

            depth += delta;
            i++;
            if (depth == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether a character is an ASCII letter.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>True for <c>A</c> to <c>Z</c> and <c>a</c> to <c>z</c>.</returns>
    private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

    /// <summary>The kind of a legacy token.</summary>
    private enum LegacyKind
    {
        /// <summary>A run of digits.</summary>
        Number,

        /// <summary>A run of ASCII letters.</summary>
        Word,

        /// <summary>One of the symbols.</summary>
        Symbol,
    }

    /// <summary>One token of a legacy date.</summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Start">The start in the text.</param>
    /// <param name="Length">The length in the text.</param>
    /// <param name="Value">The value of a number.</param>
    /// <param name="Symbol">The character of a symbol.</param>
    private readonly record struct LegacyToken(LegacyKind Kind, int Start, int Length, int Value, char Symbol);

    /// <summary>
    ///     A recursive-descent reader over the tokens of one legacy date.
    /// </summary>
    private sealed class LegacyReader
    {
        /// <summary>The text.</summary>
        private readonly string _text;

        /// <summary>The tokens.</summary>
        private readonly LegacyToken[] _tokens;

        /// <summary>The index of the next token.</summary>
        private int _position;

        /// <summary>The year.</summary>
        private int _year;

        /// <summary>The month from 1.</summary>
        private int _month;

        /// <summary>The day of the month.</summary>
        private int _day;

        /// <summary>The hour as written.</summary>
        private int _hour;

        /// <summary>The minute.</summary>
        private int _minute;

        /// <summary>The second.</summary>
        private int _second;

        /// <summary>The millisecond.</summary>
        private int _millisecond;

        /// <summary>The day period read: 0 for AM, 1 for PM, -1 for none.</summary>
        private int _period = -1;

        /// <summary>Whether a zone designator was read.</summary>
        private bool _hasZone;

        /// <summary>The offset east of UTC in minutes.</summary>
        private int _offsetMinutes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LegacyReader"/> class.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="tokens">The tokens of the text.</param>
        internal LegacyReader(string text, LegacyToken[] tokens)
        {
            _text = text;
            _tokens = tokens;
        }

        /// <summary>
        ///     Reads the whole text.
        /// </summary>
        /// <param name="calendar">The calendar for a text without a zone designator.</param>
        /// <returns>The instant, or NaN.</returns>
        internal double Read(TimeCalendar calendar)
        {
            SkipWeekday();
            if (!(ReadMonthFirst() || ReadDayFirst() || ReadYearFirst() || ReadMonthDayYear()) || !ReadTimeAndZone())
            {
                return double.NaN;
            }

            if (_position != _tokens.Length || _day < 1 || _day > DaysInMonth(_year, _month))
            {
                return double.NaN;
            }

            var hour = _hour;
            if (_period >= 0)
            {
                if (_hour < 1 || _hour > 12)
                {
                    return double.NaN;
                }

                hour = (_hour % 12) + (_period * 12);
            }

            if (hour > 23 || _minute > 59 || _second > 59)
            {
                return double.NaN;
            }

            return _hasZone
                ? TimeCalendar.Utc.FromParts(_year, _month - 1, _day, hour, _minute - _offsetMinutes, _second, _millisecond)
                : calendar.FromParts(_year, _month - 1, _day, hour, _minute, _second, _millisecond);
        }

        /// <summary>
        ///     Reads the optional time, day period and zone after the date.
        /// </summary>
        /// <returns>False when something that starts a time or zone is malformed.</returns>
        private bool ReadTimeAndZone()
        {
            if (IsNumber(0) && IsSymbol(1, ':'))
            {
                if (!ReadTime())
                {
                    return false;
                }

                ReadPeriod();
            }

            return ReadZone();
        }

        /// <summary>
        ///     Skips an optional weekday name and the comma after it.
        /// </summary>
        private void SkipWeekday()
        {
            if (IsWord(0) && NameIndex(TimeFormat.Days, _tokens[_position]) >= 0)
            {
                _position++;
                if (IsSymbol(0, ','))
                {
                    _position++;
                }
            }
        }

        /// <summary>
        ///     Reads <c>Month [.] day [,] year</c>.
        /// </summary>
        /// <returns>True when the form matched (the position moved), false when not (the position is unchanged).</returns>
        private bool ReadMonthFirst()
        {
            var start = _position;
            if (IsWord(0) && NameIndex(TimeFormat.Months, _tokens[_position]) is var month and >= 0)
            {
                _position++;
                SkipOptional('.');
                if (ReadNumberToken(1, 2, out var day))
                {
                    SkipOptional(',');
                    if (ReadNumberToken(4, 4, out var year))
                    {
                        (_month, _day, _year) = (month + 1, day, year);
                        return true;
                    }
                }
            }

            _position = start;
            return false;
        }

        /// <summary>
        ///     Reads <c>day Month [.] [,] year</c>.
        /// </summary>
        /// <returns>True when the form matched, false when not (the position is unchanged).</returns>
        private bool ReadDayFirst()
        {
            var start = _position;
            if (IsNumber(0) && IsWord(1) && NameIndex(TimeFormat.Months, _tokens[_position + 1]) is var month and >= 0 && ReadNumberToken(1, 2, out var day))
            {
                _position++;
                SkipOptional('.');
                SkipOptional(',');
                if (day > 0 && ReadNumberToken(4, 4, out var year))
                {
                    (_month, _day, _year) = (month + 1, day, year);
                    return true;
                }
            }

            _position = start;
            return false;
        }

        /// <summary>
        ///     Reads <c>year/month/day</c>.
        /// </summary>
        /// <returns>True when the form matched, false when not (the position is unchanged).</returns>
        private bool ReadYearFirst()
        {
            var start = _position;
            if (ReadNumberToken(4, 4, out var year) && ReadSlash() && ReadNumberToken(1, 2, out var month) && ReadSlash() && ReadNumberToken(1, 2, out var day)
                && month is >= 1 and <= 12)
            {
                (_month, _day, _year) = (month, day, year);
                return true;
            }

            _position = start;
            return false;
        }

        /// <summary>
        ///     Reads <c>month/day/year</c>.
        /// </summary>
        /// <returns>True when the form matched, false when not (the position is unchanged).</returns>
        private bool ReadMonthDayYear()
        {
            var start = _position;
            if (ReadNumberToken(1, 2, out var month) && ReadSlash() && ReadNumberToken(1, 2, out var day) && ReadSlash() && ReadNumberToken(4, 4, out var year)
                && month is >= 1 and <= 12)
            {
                (_month, _day, _year) = (month, day, year);
                return true;
            }

            _position = start;
            return false;
        }

        /// <summary>
        ///     Reads <c>h:mm[:ss[.fff]]</c>.
        /// </summary>
        /// <returns>False when the time is malformed.</returns>
        private bool ReadTime()
        {
            if (!ReadNumberToken(1, 2, out _hour) || !ReadColon() || !ReadNumberToken(2, 2, out _minute))
            {
                return false;
            }

            if (IsSymbol(0, ':'))
            {
                _position++;
                if (!ReadNumberToken(2, 2, out _second))
                {
                    return false;
                }

                if (IsSymbol(0, '.'))
                {
                    _position++;
                    return IsNumber(0) && ReadFraction();
                }
            }

            return true;
        }

        /// <summary>
        ///     Reads the digits after the decimal point of the seconds.
        /// </summary>
        /// <returns>True when a number token was read.</returns>
        private bool ReadFraction()
        {
            var token = _tokens[_position++];
            _millisecond = FractionToMilliseconds(_text, token.Start, token.Length);
            return true;
        }

        /// <summary>
        ///     Reads an optional AM or PM.
        /// </summary>
        private void ReadPeriod()
        {
            if (IsWord(0))
            {
                var index = Array.FindIndex(TimeFormat.Periods, p => IsExactWord(_tokens[_position], p));
                if (index >= 0)
                {
                    _period = index;
                    _position++;
                }
            }
        }

        /// <summary>
        ///     Reads an optional zone: <c>UTC</c>, <c>GMT</c>, <c>UT</c> or <c>Z</c> with an optional offset, or an offset alone.
        /// </summary>
        /// <returns>False when an offset is malformed.</returns>
        private bool ReadZone()
        {
            if (IsWord(0))
            {
                var token = _tokens[_position];
                if (!(IsExactWord(token, "utc") || IsExactWord(token, "gmt") || IsExactWord(token, "ut") || IsExactWord(token, "z")))
                {
                    return true;
                }

                _position++;
                _hasZone = true;
                return !(IsSymbol(0, '+') || IsSymbol(0, '-')) || ReadOffset();
            }

            return !(IsSymbol(0, '+') || IsSymbol(0, '-')) || ReadOffset();
        }

        /// <summary>
        ///     Reads <c>±hhmm</c>, <c>±hh</c> or <c>±hh:mm</c> at the sign.
        /// </summary>
        /// <returns>False when malformed.</returns>
        private bool ReadOffset()
        {
            var sign = _tokens[_position++].Symbol == '-' ? -1 : 1;
            if (!IsNumber(0))
            {
                return false;
            }

            var token = _tokens[_position];
            int hours;
            var minutes = 0;
            if (token.Length == 4)
            {
                (hours, minutes) = (token.Value / 100, token.Value % 100);
                _position++;
            }
            else if (!ReadNumberToken(1, 2, out hours) || (IsSymbol(0, ':') && !(ReadColon() && ReadNumberToken(2, 2, out minutes))))
            {
                return false;
            }

            _hasZone = true;
            _offsetMinutes = sign * ((hours * 60) + minutes);
            return hours < 24 && minutes < 60;
        }

        /// <summary>
        ///     Reads a number token with a digit count in a range.
        /// </summary>
        /// <param name="minDigits">The fewest digits.</param>
        /// <param name="maxDigits">The most digits.</param>
        /// <param name="value">The number.</param>
        /// <returns>True when read (the position moved).</returns>
        private bool ReadNumberToken(int minDigits, int maxDigits, out int value)
        {
            value = 0;
            if (!IsNumber(0) || _tokens[_position].Length < minDigits || _tokens[_position].Length > maxDigits)
            {
                return false;
            }

            value = _tokens[_position++].Value;
            return true;
        }

        /// <summary>
        ///     Reads a slash.
        /// </summary>
        /// <returns>True when read.</returns>
        private bool ReadSlash() => ReadSymbol('/');

        /// <summary>
        ///     Reads a colon.
        /// </summary>
        /// <returns>True when read.</returns>
        private bool ReadColon() => ReadSymbol(':');

        /// <summary>
        ///     Reads a symbol when it is next.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <returns>True when read.</returns>
        private bool ReadSymbol(char symbol)
        {
            if (!IsSymbol(0, symbol))
            {
                return false;
            }

            _position++;
            return true;
        }

        /// <summary>
        ///     Skips a symbol when it is next.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        private void SkipOptional(char symbol) => ReadSymbol(symbol);

        /// <summary>
        ///     Tells whether the token at an offset from the position is a number.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns>True for a number.</returns>
        private bool IsNumber(int offset) => _position + offset < _tokens.Length && _tokens[_position + offset].Kind == LegacyKind.Number;

        /// <summary>
        ///     Tells whether the token at an offset from the position is a word.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns>True for a word.</returns>
        private bool IsWord(int offset) => _position + offset < _tokens.Length && _tokens[_position + offset].Kind == LegacyKind.Word;

        /// <summary>
        ///     Tells whether the token at an offset from the position is a given symbol.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="symbol">The symbol.</param>
        /// <returns>True for that symbol.</returns>
        private bool IsSymbol(int offset, char symbol) =>
            _position + offset < _tokens.Length && _tokens[_position + offset].Kind == LegacyKind.Symbol && _tokens[_position + offset].Symbol == symbol;

        /// <summary>
        ///     Tells whether a word token is a given word, ignoring ASCII case.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="word">The word.</param>
        /// <returns>True when equal.</returns>
        private bool IsExactWord(LegacyToken token, string word) =>
            token.Kind == LegacyKind.Word && token.Length == word.Length && TimeFormat.MatchesName(_text, token.Start, word);

        /// <summary>
        ///     Finds the name a word stands for: the whole name or an abbreviation of at least three letters.
        /// </summary>
        /// <param name="names">The full names.</param>
        /// <param name="token">The word token.</param>
        /// <returns>The index of the name, or -1.</returns>
        private int NameIndex(string[] names, LegacyToken token)
        {
            if (token.Length < 3)
            {
                return -1;
            }

            for (var i = 0; i < names.Length; i++)
            {
                if (token.Length <= names[i].Length && TimeFormat.MatchesName(_text, token.Start, names[i][..token.Length]))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

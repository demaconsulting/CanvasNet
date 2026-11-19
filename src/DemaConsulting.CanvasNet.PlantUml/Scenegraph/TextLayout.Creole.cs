// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Scenegraph;

/// <summary>
///     A piece of a line with one style, before it is measured.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Font">The font.</param>
/// <param name="Color">The color.</param>
/// <param name="Underline">Whether the piece is underlined.</param>
/// <param name="Strike">Whether the piece is struck through.</param>
internal sealed record CreolePiece(string Text, SceneFont Font, Rgba32 Color, bool Underline, bool Strike);

/// <content>
///     The creole markup subset: <c>**bold**</c>, <c>//italic//</c>, <c>""mono""</c>, <c>__underline__</c>,
///     <c>--strike--</c>, the tags <c>&lt;b&gt;</c>, <c>&lt;i&gt;</c>, <c>&lt;u&gt;</c>, <c>&lt;s&gt;</c>,
///     <c>&lt;color:x&gt;</c>, <c>&lt;size:n&gt;</c> and <c>&lt;font:x&gt;</c>, <c>&lt;U+XXXX&gt;</c> characters, and the
///     <c>~</c> escape.
/// </content>
internal sealed partial class TextLayout
{
    /// <summary>
    ///     Parses the creole markup of one line into styled pieces, keeping the style of open markup from one line to the
    ///     next, as PlantUML keeps an open tag.
    /// </summary>
    /// <remarks>
    ///     Markup outside the subset (images, sprites, icons, tables, lists, headings, rules, and the background tag) is
    ///     kept as text and remembered in <see cref="Unsupported"/>. The parser is used by one layout on one thread.
    /// </remarks>
    /// <param name="font">The base font.</param>
    /// <param name="color">The base color.</param>
    private sealed class CreoleParser(SceneFont font, Rgba32 color)
    {
        /// <summary>The longest tag that is read, in characters.</summary>
        private const int MaxTagLength = 64;

        /// <summary>The largest font size a size tag may set, in pixels.</summary>
        private const double MaxSize = 500;

        /// <summary>The pieces of the line being parsed.</summary>
        private readonly List<CreolePiece> _pieces = [];

        /// <summary>The text of the piece being built.</summary>
        private readonly StringBuilder _text = new();

        /// <summary>The colors opened by color tags, innermost last.</summary>
        private readonly Stack<Rgba32> _colors = new();

        /// <summary>The sizes opened by size tags, innermost last.</summary>
        private readonly Stack<double> _sizes = new();

        /// <summary>The families opened by font tags, innermost last.</summary>
        private readonly Stack<string> _families = new();

        /// <summary>Whether bold markup is open.</summary>
        private bool _bold;

        /// <summary>Whether italic markup is open.</summary>
        private bool _italic;

        /// <summary>Whether monospace markup is open.</summary>
        private bool _mono;

        /// <summary>Whether underline markup is open.</summary>
        private bool _underline;

        /// <summary>Whether strike markup is open.</summary>
        private bool _strike;

        /// <summary>
        ///     Gets the first markup outside the subset that was met, or null.
        /// </summary>
        public string? Unsupported { get; private set; }

        /// <summary>
        ///     Parses one line.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <returns>The pieces of the line.</returns>
        public List<CreolePiece> Parse(string line)
        {
            _pieces.Clear();
            NoteLineMarkup(line);
            var i = 0;
            while (i < line.Length)
            {
                i = Step(line, i);
            }

            Flush();
            return [.. _pieces];
        }

        /// <summary>
        ///     Reads the markup at a position.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="i">The position.</param>
        /// <returns>The position after what was read.</returns>
        private int Step(string line, int i)
        {
            var c = line[i];
            var next = i + 1 < line.Length ? line[i + 1] : '\0';

            // The tilde escapes the next character
            if (c == '~' && next != '\0')
            {
                _text.Append(next);
                return i + 2;
            }

            // The doubled-character toggles
            if (c == next && c is '*' or '/' or '"' or '_' or '-' && Toggle(line, i, c))
            {
                return i + 2;
            }

            if (c == '<' && TryTag(line, i, out var end))
            {
                return end;
            }

            _text.Append(c);
            return i + 1;
        }

        /// <summary>
        ///     Toggles the style of a doubled markup character.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="i">The position of the pair.</param>
        /// <param name="c">The character.</param>
        /// <returns>True when the pair is markup.</returns>
        private bool Toggle(string line, int i, char c)
        {
            switch (c)
            {
                case '*':
                    Flush();
                    _bold = !_bold;
                    return true;
                case '/' when i == 0 || line[i - 1] != ':':
                    // A URL (http://) is not italic markup
                    Flush();
                    _italic = !_italic;
                    return true;
                case '"':
                    Flush();
                    _mono = !_mono;
                    return true;
                case '_':
                    Flush();
                    _underline = !_underline;
                    return true;
                case '-' when _strike || OpensStrike(line, i):
                    Flush();
                    _strike = !_strike;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Decides whether a pair of dashes opens struck text: the text after it is not a space or a dash and a closing
        ///     pair follows on the line.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="i">The position of the pair.</param>
        /// <returns>True when it opens struck text.</returns>
        private static bool OpensStrike(string line, int i)
        {
            var start = i + 2;
            return start < line.Length && line[start] is not ' ' and not '-' && line.IndexOf("--", start + 1, StringComparison.Ordinal) > 0;
        }

        /// <summary>
        ///     Reads a tag.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="i">The position of the opening bracket.</param>
        /// <param name="end">Receives the position after the tag.</param>
        /// <returns>True when a tag was read (or recognized as unsupported and kept as text).</returns>
        private bool TryTag(string line, int i, out int end)
        {
            end = i;
            var close = line.IndexOf('>', i + 1);
            if (close < 0 || close - i > MaxTagLength)
            {
                return false;
            }

            var tag = line[(i + 1)..close];
            var lower = tag.ToLowerInvariant();
            if (TryStyleTag(lower, tag))
            {
                end = close + 1;
                return true;
            }

            // A Unicode character written as <U+XXXX>
            if (lower.StartsWith("u+", StringComparison.Ordinal) &&
                int.TryParse(tag[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) &&
                code is > 0 and <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF))
            {
                _text.Append(char.ConvertFromUtf32(code));
                end = close + 1;
                return true;
            }

            // Images, sprites, icons and the background are kept as text
            if (lower.StartsWith("img", StringComparison.Ordinal) || lower.StartsWith('$') || lower.StartsWith('&') ||
                lower.StartsWith("back", StringComparison.Ordinal) || lower.StartsWith("/back", StringComparison.Ordinal))
            {
                Unsupported ??= "<" + tag + ">";
            }

            return false;
        }

        /// <summary>
        ///     Applies a style tag.
        /// </summary>
        /// <param name="lower">The tag in lowercase.</param>
        /// <param name="tag">The tag as written.</param>
        /// <returns>True when the tag is a style tag of the subset.</returns>
        private bool TryStyleTag(string lower, string tag)
        {
            switch (lower)
            {
                case "b" or "/b":
                    Flush();
                    _bold = lower == "b";
                    return true;
                case "i" or "/i":
                    Flush();
                    _italic = lower == "i";
                    return true;
                case "u" or "/u":
                    Flush();
                    _underline = lower == "u";
                    return true;
                case "s" or "/s" or "strike" or "/strike" or "del" or "/del":
                    Flush();
                    _strike = !lower.StartsWith('/');
                    return true;
                case "/color":
                    Flush();
                    _colors.TryPop(out _);
                    return true;
                case "/size":
                    Flush();
                    _sizes.TryPop(out _);
                    return true;
                case "/font":
                    Flush();
                    _families.TryPop(out _);
                    return true;
            }

            var argument = ArgumentOf(tag);
            if (lower.StartsWith("color", StringComparison.Ordinal) && SkinParams.TryParseColor(argument, out var c))
            {
                Flush();
                _colors.Push(c);
                return true;
            }

            if (lower.StartsWith("size", StringComparison.Ordinal) &&
                double.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size is > 0 and <= MaxSize)
            {
                Flush();
                _sizes.Push(size);
                return true;
            }

            if (lower.StartsWith("font", StringComparison.Ordinal) && argument.Length > 0)
            {
                Flush();
                _families.Push(argument);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Gets the argument of a tag written <c>name:value</c> or <c>name value</c>.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <returns>The trimmed argument, or an empty text.</returns>
        private static string ArgumentOf(string tag)
        {
            var separator = tag.IndexOfAny([':', ' ', '=']);
            return separator < 0 ? string.Empty : tag[(separator + 1)..].Trim().Trim('"');
        }

        /// <summary>
        ///     Remembers the line-level markup that is kept as text: tables, lists, headings, and rules.
        /// </summary>
        /// <param name="line">The line.</param>
        private void NoteLineMarkup(string line)
        {
            var trimmed = line.Trim();
            var isTable = trimmed.Length > 1 && trimmed[0] == '|' && trimmed[^1] == '|';
            var isList = trimmed.StartsWith("* ", StringComparison.Ordinal) || trimmed.StartsWith("# ", StringComparison.Ordinal);
            var isHeading = trimmed.StartsWith("= ", StringComparison.Ordinal) || trimmed.StartsWith("== ", StringComparison.Ordinal);
            var isRule = trimmed.Length >= 4 && (trimmed.All(c => c == '-') || trimmed.All(c => c == '=') || trimmed.All(c => c == '_'));
            if (isTable || isList || isHeading || isRule)
            {
                Unsupported ??= trimmed.Length <= 20 ? trimmed : trimmed[..20];
            }
        }

        /// <summary>
        ///     Ends the piece being built with the current style.
        /// </summary>
        private void Flush()
        {
            if (_text.Length == 0)
            {
                return;
            }

            var family = _families.Count > 0 ? _families.Peek() : font.Family;
            if (_mono)
            {
                family = "Monospaced";
            }

            var size = _sizes.Count > 0 ? _sizes.Peek() : font.Size;
            var pieceFont = new SceneFont(family, size, font.Bold || _bold, font.Italic || _italic);
            _pieces.Add(new CreolePiece(_text.ToString(), pieceFont, _colors.Count > 0 ? _colors.Peek() : color, _underline, _strike));
            _text.Clear();
        }
    }
}

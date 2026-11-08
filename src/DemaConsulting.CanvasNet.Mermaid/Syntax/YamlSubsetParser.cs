// Portions derived from js-yaml (MIT) as bundled by mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Syntax;

/// <summary>
///     Reads the YAML that Mermaid reads with js-yaml's <c>load(text, { schema: JSON_SCHEMA })</c>: the frontmatter of a
///     diagram and the <c>@{ ... }</c> shape data of nodes and edges.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> The reader is a port of js-yaml's loader (the version Mermaid bundles), so block and flow
///         mappings and sequences, plain, single-quoted, double-quoted and block scalars (<c>|</c>, <c>&gt;</c>, with chomping
///         and indentation indicators), comments, folding, indentation rules, duplicate-key detection and the implicit
///         scalar types of the <c>JSON_SCHEMA</c> (null, boolean, integer, float, see <see cref="ResolvePlain"/>) behave
///         as in Mermaid, and so do its errors (reported as <see cref="MermaidDiagnosticCodes.YamlInvalid"/>).
///     </para>
///     <para>
///         <b>Refused constructs.</b> Anchors (<c>&amp;a</c>), aliases (<c>*a</c>), tags (<c>!tag</c>), explicit keys
///         (<c>? key</c>) and directives (<c>%YAML</c>) are refused with <see cref="MermaidDiagnosticCodes.YamlUnsupported"/>:
///         js-yaml accepts them, but aliases can expand a small text into a huge value and none of them is needed by a
///         diagram. A key <c>&lt;&lt;</c> is an ordinary key, as it is under the <c>JSON_SCHEMA</c>. Several documents are an
///         error, as in js-yaml's <c>load</c>.
///     </para>
///     <para>
///         <b>Limits.</b> Collections may nest at most <see cref="MermaidLimits.MaxYamlDepth"/> deep (a
///         <see cref="MermaidSyntaxException"/> with <see cref="MermaidDiagnosticCodes.LimitExceeded"/>), so the recursion
///         is bounded; every other step consumes input, so the time is linear in the text apart from the duplicate-key check.
///     </para>
///     <para>
///         An instance reads one text on one thread; <see cref="Parse"/> is the entry point and is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed partial class YamlSubsetParser
{
    /// <summary>The context of a node inside a flow collection.</summary>
    private const int ContextFlowIn = 1;

    /// <summary>The context of a block mapping key.</summary>
    private const int ContextFlowOut = 2;

    /// <summary>The context of a block sequence entry.</summary>
    private const int ContextBlockIn = 3;

    /// <summary>The context of a block mapping value or of the document.</summary>
    private const int ContextBlockOut = 4;

    /// <summary>The chomping of a block scalar without an indicator: one final line break.</summary>
    private const int ChompingClip = 1;

    /// <summary>The chomping indicator <c>-</c>: no final line break.</summary>
    private const int ChompingStrip = 2;

    /// <summary>The chomping indicator <c>+</c>: every final line break.</summary>
    private const int ChompingKeep = 3;

    /// <summary>The input with a final line break and the terminating NUL that js-yaml appends.</summary>
    private readonly string _input;

    /// <summary>The length of the input without the terminating NUL.</summary>
    private readonly int _length;

    /// <summary>Maps an input position to the original text.</summary>
    private readonly Func<int, MermaidLocation> _locate;

    /// <summary>The deepest collection nesting accepted.</summary>
    private readonly int _maxDepth;

    /// <summary>The scalar being read.</summary>
    private readonly StringBuilder _scalar = new();

    /// <summary>The current position.</summary>
    private int _position;

    /// <summary>The number of the current line (zero-based).</summary>
    private int _line;

    /// <summary>The position at which the current line starts.</summary>
    private int _lineStart;

    /// <summary>The indentation of the current line.</summary>
    private int _lineIndent;

    /// <summary>The position of the first tab of the current line's indentation, or -1.</summary>
    private int _firstTabInLine = -1;

    /// <summary>The number of collections that are open.</summary>
    private int _collections;

    /// <summary>The result of the last node read (js-yaml's <c>state.result</c>), or null.</summary>
    private YamlNode? _result;

    /// <summary>
    ///     Initializes a reader.
    /// </summary>
    /// <param name="input">The prepared input.</param>
    /// <param name="locate">Maps an input position to the original text.</param>
    /// <param name="maxDepth">The deepest collection nesting accepted.</param>
    private YamlSubsetParser(string input, Func<int, MermaidLocation> locate, int maxDepth)
    {
        _input = input + "\0";
        _length = input.Length;
        _locate = locate;
        _maxDepth = maxDepth;
    }

    /// <summary>
    ///     Reads a YAML text that holds at most one document.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="locate">Maps a position of <paramref name="text"/> to the original text (for locations and errors).</param>
    /// <param name="maxDepth">The deepest collection nesting accepted (<see cref="MermaidLimits.MaxYamlDepth"/>).</param>
    /// <returns>The value of the document, or <see langword="null"/> when the text holds no document or an empty one (js-yaml's <c>undefined</c> and <c>null</c>).</returns>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is not valid YAML, uses a refused construct, or nests too deeply.</exception>
    public static YamlNode? Parse(string text, Func<int, MermaidLocation> locate, int maxDepth)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(locate);

        // As js-yaml's loadDocuments: a final line break is added and a byte-order mark is removed
        var originalLength = text.Length;
        var shift = 0;
        if (text.Length != 0)
        {
            if (text[^1] is not '\n' and not '\r')
            {
                text += "\n";
            }

            if (text[0] == '\uFEFF')
            {
                text = text[1..];
                shift = 1;
            }
        }

        var parser = new YamlSubsetParser(text, position => locate(Math.Min(position + shift, originalLength)), maxDepth);
        return parser.ReadDocuments();
    }

    /// <summary>
    ///     Reads the documents of the input; more than one is an error (js-yaml's <c>load</c>).
    /// </summary>
    /// <returns>The value of the single document, or null.</returns>
    private YamlNode? ReadDocuments()
    {
        var nul = _input.IndexOf('\0', StringComparison.Ordinal);
        if (nul < _length)
        {
            _position = nul;
            throw Invalid("null byte is not allowed in input");
        }

        while (Ch(_position) == ' ')
        {
            _lineIndent++;
            _position++;
        }

        var documents = 0;
        YamlNode? result = null;
        while (_position < _length - 1)
        {
            if (documents == 1)
            {
                throw Invalid("expected a single document in the stream, but found more");
            }

            result = ReadDocument();
            documents++;
        }

        return result;
    }

    /// <summary>
    ///     Reads one document (js-yaml's <c>readDocument</c>, without directives).
    /// </summary>
    /// <returns>The value of the document, or null.</returns>
    private YamlNode? ReadDocument()
    {
        SkipSeparationSpace(true);

        // Directives are refused
        if (_lineIndent == 0 && Ch(_position) == '%')
        {
            throw Unsupported("YAML directives (%YAML, %TAG) are not supported");
        }

        // An explicit document start is allowed
        if (_lineIndent == 0 && Ch(_position) == '-' && Ch(_position + 1) == '-' && Ch(_position + 2) == '-')
        {
            _position += 3;
            SkipSeparationSpace(true);
        }

        ComposeNode(_lineIndent - 1, ContextBlockOut, false, true);
        SkipSeparationSpace(true);
        var result = _result;

        if (_position == _lineStart && TestDocumentSeparator())
        {
            if (Ch(_position) == '.')
            {
                _position += 3;
                SkipSeparationSpace(true);
            }

            return result;
        }

        if (_position < _length - 1)
        {
            throw Invalid("end of the stream or a document separator is expected");
        }

        return result;
    }

    /// <summary>
    ///     Composes one node (js-yaml's <c>composeNode</c>): seeks to its content, decides by its first character and
    ///     indentation what kind of node it is, reads it, and resolves a plain scalar.
    /// </summary>
    /// <param name="parentIndent">The indentation of the parent node.</param>
    /// <param name="nodeContext">The context (flow in, flow out, block in, block out).</param>
    /// <param name="allowToSeek">Whether the node may start on a following line.</param>
    /// <param name="allowCompact">Whether a block collection may start on the current line.</param>
    /// <returns>Whether a node with content was read.</returns>
    private bool ComposeNode(int parentIndent, int nodeContext, bool allowToSeek, bool allowCompact)
    {
        var indentStatus = 1;
        var atNewLine = false;
        var hasContent = false;
        _result = null;

        var allowBlockStyles = nodeContext is ContextBlockOut or ContextBlockIn;
        var allowBlockScalars = allowBlockStyles;
        var allowBlockCollections = allowBlockStyles;

        // Seek to the content and compare its indentation with the parent's
        if (allowToSeek && SkipSeparationSpace(true) != 0)
        {
            atNewLine = true;
            indentStatus = _lineIndent.CompareTo(parentIndent);
            indentStatus = Math.Sign(indentStatus);
        }

        // Node properties (tags and anchors) are refused where js-yaml would read them
        if (indentStatus == 1 && Ch(_position) is '!' or '&')
        {
            throw Unsupported(Ch(_position) == '!' ? "YAML tags are not supported" : "YAML anchors are not supported");
        }

        if (allowBlockCollections)
        {
            allowBlockCollections = atNewLine || allowCompact;
        }

        if (indentStatus == 1 || nodeContext == ContextBlockOut)
        {
            var flowIndent = nodeContext is ContextFlowIn or ContextFlowOut ? parentIndent : parentIndent + 1;
            var blockIndent = _position - _lineStart;
            var start = _position;

            if (indentStatus == 1)
            {
                if ((allowBlockCollections && (ReadBlockSequence(blockIndent) || ReadBlockMapping(blockIndent, flowIndent))) ||
                    ReadFlowCollection(flowIndent))
                {
                    hasContent = true;
                }
                else if ((allowBlockScalars && ReadBlockScalar(flowIndent)) || ReadSingleQuotedScalar() || ReadDoubleQuotedScalar())
                {
                    hasContent = true;
                    _result = new YamlScalar(_scalar.ToString(), _locate(start));
                }
                else if (Ch(_position) == '*')
                {
                    throw Unsupported("YAML aliases are not supported");
                }
                else if (ReadPlainScalar(flowIndent, nodeContext == ContextFlowIn))
                {
                    hasContent = true;
                    _result = new YamlScalar(ResolvePlain(_scalar.ToString()), _locate(start));
                }
            }
            else if (indentStatus == 0)
            {
                // A block sequence may have the same indentation as its parent mapping key
                hasContent = allowBlockCollections && ReadBlockSequence(blockIndent);
            }
        }

        return hasContent;
    }

    /// <summary>
    ///     Reads a block sequence (js-yaml's <c>readBlockSequence</c>).
    /// </summary>
    /// <param name="nodeIndent">The indentation of the sequence.</param>
    /// <returns>Whether a sequence was read.</returns>
    private bool ReadBlockSequence(int nodeIndent)
    {
        if (_firstTabInLine != -1)
        {
            return false;
        }

        var start = _position;
        var items = new List<YamlNode>();
        var detected = false;
        var ch = Ch(_position);

        while (ch != '\0')
        {
            if (_firstTabInLine != -1)
            {
                _position = _firstTabInLine;
                throw Invalid("tab characters must not be used in indentation");
            }

            if (ch != '-' || !IsWsOrEol(Ch(_position + 1)))
            {
                break;
            }

            if (!detected)
            {
                EnterCollection(start);
            }

            detected = true;
            var itemStart = _position;
            _position++;

            // An entry with nothing on its line and nothing more indented below is null
            if (SkipSeparationSpace(true) != 0 && _lineIndent <= nodeIndent)
            {
                items.Add(new YamlScalar(null, _locate(itemStart)));
                ch = Ch(_position);
                continue;
            }

            var line = _line;
            ComposeNode(nodeIndent, ContextBlockIn, false, true);
            items.Add(_result ?? new YamlScalar(null, _locate(itemStart)));
            SkipSeparationSpace(true);
            ch = Ch(_position);

            if ((_line == line || _lineIndent > nodeIndent) && ch != '\0')
            {
                throw Invalid("bad indentation of a sequence entry");
            }

            if (_lineIndent < nodeIndent)
            {
                break;
            }
        }

        if (!detected)
        {
            return false;
        }

        _collections--;
        _result = new YamlSequence(items, _locate(start));
        return true;
    }

    /// <summary>
    ///     Reads a block mapping (js-yaml's <c>readBlockMapping</c>, implicit keys only).
    /// </summary>
    /// <param name="nodeIndent">The indentation of the mapping.</param>
    /// <param name="flowIndent">The indentation for its keys.</param>
    /// <returns>Whether a mapping was read; when the first key turns out not to be one, the node read is kept as the result.</returns>
    private bool ReadBlockMapping(int nodeIndent, int flowIndent)
    {
        if (_firstTabInLine != -1)
        {
            return false;
        }

        var start = _position;
        var entries = new List<YamlEntry>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var detected = false;
        var ch = Ch(_position);

        while (ch != '\0')
        {
            if (_firstTabInLine != -1)
            {
                _position = _firstTabInLine;
                throw Invalid("tab characters must not be used in indentation");
            }

            var following = Ch(_position + 1);
            var line = _line;

            // Explicit keys are refused; a ':' without a key is js-yaml's error
            if (ch is '?' or ':' && IsWsOrEol(following))
            {
                throw ch == '?'
                    ? Unsupported("explicit YAML keys ('? key') are not supported")
                    : Invalid("incomplete explicit mapping pair; a key node is missed; or followed by a non-tabulated empty line");
            }

            // The implicit key: a flow-style node on one line followed by ':'
            var keyStart = _position;
            if (!ComposeNode(flowIndent, ContextFlowOut, false, true))
            {
                break;
            }

            var keyNode = _result;
            if (_line != line)
            {
                if (detected)
                {
                    throw Invalid("can not read a block mapping entry; a multiline key may not be an implicit key", keyStart);
                }

                return true;
            }

            ch = Ch(_position);
            while (IsWhiteSpace(ch))
            {
                ch = Ch(++_position);
            }

            if (ch != ':')
            {
                if (detected)
                {
                    throw Invalid("can not read an implicit mapping pair; a colon is missed");
                }

                // Not a mapping after all: keep the node that was read
                return true;
            }

            ch = Ch(++_position);
            if (!IsWsOrEol(ch))
            {
                throw Invalid("a whitespace character is expected after the key-value separator within a block mapping");
            }

            if (!detected)
            {
                EnterCollection(start);
            }

            detected = true;
            var key = KeyString(keyNode, keyStart);

            // The value: on the same line, or on following lines more indented than the mapping (an implicit key is
            // always on the line of its ':', so the value is always composed)
            var value = ComposeNode(nodeIndent, ContextBlockOut, true, false) ? _result : null;
            StoreEntry(entries, keys, key, value, keyStart);
            SkipSeparationSpace(true);
            ch = Ch(_position);

            if ((_line == line || _lineIndent > nodeIndent) && ch != '\0')
            {
                throw Invalid("bad indentation of a mapping entry");
            }

            if (_lineIndent < nodeIndent)
            {
                break;
            }
        }

        if (!detected)
        {
            return false;
        }

        _collections--;
        _result = new YamlMapping(entries, _locate(start));
        return true;
    }

    /// <summary>
    ///     Reads a flow sequence or mapping (js-yaml's <c>readFlowCollection</c>).
    /// </summary>
    /// <param name="nodeIndent">The indentation of the collection.</param>
    /// <returns>Whether a flow collection was read.</returns>
    private bool ReadFlowCollection(int nodeIndent)
    {
        var ch = Ch(_position);
        if (ch is not '[' and not '{')
        {
            return false;
        }

        var start = _position;
        var isMapping = ch == '{';
        var terminator = isMapping ? '}' : ']';
        var items = new List<YamlNode>();
        var entries = new List<YamlEntry>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var readNext = true;
        EnterCollection(start);
        ch = Ch(++_position);

        while (ch != '\0')
        {
            SkipSeparationSpace(true);
            ch = Ch(_position);

            if (ch == terminator)
            {
                _position++;
                _collections--;
                _result = isMapping ? new YamlMapping(entries, _locate(start)) : new YamlSequence(items, _locate(start));
                return true;
            }

            if (!readNext)
            {
                throw Invalid("missed comma between flow collection entries");
            }

            if (ch == ',')
            {
                throw Invalid("expected the node content, but found ','");
            }

            if (ch == '?' && IsWsOrEol(Ch(_position + 1)))
            {
                throw Unsupported("explicit YAML keys ('? key') are not supported");
            }

            // The entry: a node, then optionally ':' and a value on the same line
            var line = _line;
            var entryStart = _position;
            ComposeNode(nodeIndent, ContextFlowIn, false, true);
            var keyNode = _result;
            SkipSeparationSpace(true);
            ch = Ch(_position);

            YamlNode? valueNode = null;
            var isPair = false;
            if (_line == line && ch == ':')
            {
                isPair = true;
                _position++;
                SkipSeparationSpace(true);
                ComposeNode(nodeIndent, ContextFlowIn, false, true);
                valueNode = _result;
            }

            if (isMapping)
            {
                StoreEntry(entries, keys, KeyString(keyNode, entryStart), valueNode, entryStart);
            }
            else if (isPair)
            {
                // A single pair inside a flow sequence is a one-entry mapping
                var pair = new List<YamlEntry>();
                StoreEntry(pair, new HashSet<string>(StringComparer.Ordinal), KeyString(keyNode, entryStart), valueNode, entryStart);
                items.Add(new YamlMapping(pair, _locate(entryStart)));
            }
            else
            {
                items.Add(keyNode ?? new YamlScalar(null, _locate(entryStart)));
            }

            SkipSeparationSpace(true);
            ch = Ch(_position);
            if (ch == ',')
            {
                readNext = true;
                ch = Ch(++_position);
            }
            else
            {
                readNext = false;
            }
        }

        throw Invalid("unexpected end of the stream within a flow collection");
    }

    /// <summary>
    ///     Reads a literal (<c>|</c>) or folded (<c>&gt;</c>) block scalar into the scalar buffer (js-yaml's <c>readBlockScalar</c>).
    /// </summary>
    /// <param name="nodeIndent">The indentation the content must exceed.</param>
    /// <returns>Whether a block scalar was read.</returns>
    private bool ReadBlockScalar(int nodeIndent)
    {
        var ch = Ch(_position);
        if (ch is not '|' and not '>')
        {
            return false;
        }

        var folding = ch == '>';
        var chomping = ChompingClip;
        var didReadContent = false;
        var detectedIndent = false;
        var textIndent = nodeIndent;
        var emptyLines = 0;
        var atMoreIndented = false;
        _scalar.Clear();

        // The header: a chomping indicator and an indentation indicator, in either order
        while (ch != '\0')
        {
            ch = Ch(++_position);
            if (ch is '+' or '-')
            {
                if (chomping != ChompingClip)
                {
                    throw Invalid("repeat of a chomping mode identifier");
                }

                chomping = ch == '+' ? ChompingKeep : ChompingStrip;
            }
            else if (char.IsAsciiDigit(ch))
            {
                if (ch == '0')
                {
                    throw Invalid("bad explicit indentation width of a block scalar; it cannot be less than one");
                }

                if (detectedIndent)
                {
                    throw Invalid("repeat of an indentation width identifier");
                }

                textIndent = nodeIndent + (ch - '0') - 1;
                detectedIndent = true;
            }
            else
            {
                break;
            }
        }

        // An optional comment after the header
        if (IsWhiteSpace(ch))
        {
            do
            {
                ch = Ch(++_position);
            }
            while (IsWhiteSpace(ch));

            if (ch == '#')
            {
                do
                {
                    ch = Ch(++_position);
                }
                while (!IsEol(ch) && ch != '\0');
            }
        }

        // The content lines
        while (ch != '\0')
        {
            ReadLineBreak();
            _lineIndent = 0;
            ch = Ch(_position);

            while ((!detectedIndent || _lineIndent < textIndent) && ch == ' ')
            {
                _lineIndent++;
                ch = Ch(++_position);
            }

            if (!detectedIndent && _lineIndent > textIndent)
            {
                textIndent = _lineIndent;
            }

            if (IsEol(ch))
            {
                emptyLines++;
                continue;
            }

            if (!detectedIndent && textIndent == 0)
            {
                throw Invalid("missing indentation for block scalar");
            }

            // A less indented line ends the scalar: apply the chomping
            if (_lineIndent < textIndent)
            {
                if (chomping == ChompingKeep)
                {
                    _scalar.Append('\n', didReadContent ? 1 + emptyLines : emptyLines);
                }
                else if (chomping == ChompingClip && didReadContent)
                {
                    _scalar.Append('\n');
                }

                break;
            }

            AppendBlockLineBreaks(folding, ch, didReadContent, emptyLines, ref atMoreIndented);
            didReadContent = true;
            detectedIndent = true;
            emptyLines = 0;

            var captureStart = _position;
            while (!IsEol(ch) && ch != '\0')
            {
                ch = Ch(++_position);
            }

            Capture(captureStart, _position, false);
        }

        return true;
    }

    /// <summary>
    ///     Appends the line breaks between two content lines of a block scalar (the folding rules of js-yaml).
    /// </summary>
    /// <param name="folding">Whether the scalar is folded.</param>
    /// <param name="ch">The first character of the new line.</param>
    /// <param name="didReadContent">Whether a content line was read before.</param>
    /// <param name="emptyLines">The number of empty lines before the new line.</param>
    /// <param name="atMoreIndented">Whether the previous line was more indented (updated).</param>
    private void AppendBlockLineBreaks(bool folding, char ch, bool didReadContent, int emptyLines, ref bool atMoreIndented)
    {
        if (!folding)
        {
            // Literal style: every line break is kept except the one of the header
            _scalar.Append('\n', didReadContent ? 1 + emptyLines : emptyLines);
            return;
        }

        if (IsWhiteSpace(ch))
        {
            // More-indented lines are not folded
            atMoreIndented = true;
            _scalar.Append('\n', didReadContent ? 1 + emptyLines : emptyLines);
        }
        else if (atMoreIndented)
        {
            atMoreIndented = false;
            _scalar.Append('\n', emptyLines + 1);
        }
        else if (emptyLines == 0)
        {
            if (didReadContent)
            {
                _scalar.Append(' ');
            }
        }
        else
        {
            _scalar.Append('\n', emptyLines);
        }
    }

    /// <summary>
    ///     Reads a plain scalar into the scalar buffer (js-yaml's <c>readPlainScalar</c>).
    /// </summary>
    /// <param name="nodeIndent">The indentation a continuation line must reach.</param>
    /// <param name="withinFlowCollection">Whether the scalar is inside a flow collection (where flow indicators end it).</param>
    /// <returns>Whether a non-empty plain scalar was read.</returns>
    private bool ReadPlainScalar(int nodeIndent, bool withinFlowCollection)
    {
        var ch = Ch(_position);
        if (IsWsOrEol(ch) || IsFlowIndicator(ch) || ch is '#' or '&' or '*' or '!' or '|' or '>' or '\'' or '"' or '%' or '@' or '`')
        {
            return false;
        }

        if (ch is '?' or '-')
        {
            var following = Ch(_position + 1);
            if (IsWsOrEol(following) || (withinFlowCollection && IsFlowIndicator(following)))
            {
                return false;
            }
        }

        _scalar.Clear();
        var captureStart = _position;
        var captureEnd = _position;
        var hasPendingContent = false;
        int savedLine = 0, savedLineStart = 0, savedLineIndent = 0;

        while (ch != '\0')
        {
            if (ch == ':')
            {
                var following = Ch(_position + 1);
                if (IsWsOrEol(following) || (withinFlowCollection && IsFlowIndicator(following)))
                {
                    break;
                }
            }
            else if (ch == '#')
            {
                if (IsWsOrEol(Ch(_position - 1)))
                {
                    break;
                }
            }
            else if ((_position == _lineStart && TestDocumentSeparator()) || (withinFlowCollection && IsFlowIndicator(ch)))
            {
                break;
            }
            else if (IsEol(ch))
            {
                // A line break: continue on the next line when it is indented enough, otherwise stop before the break
                savedLine = _line;
                savedLineStart = _lineStart;
                savedLineIndent = _lineIndent;
                SkipSeparationSpace(false);

                if (_lineIndent >= nodeIndent)
                {
                    hasPendingContent = true;
                    ch = Ch(_position);
                    continue;
                }

                _position = captureEnd;
                _line = savedLine;
                _lineStart = savedLineStart;
                _lineIndent = savedLineIndent;
                break;
            }

            if (hasPendingContent)
            {
                Capture(captureStart, captureEnd, false);
                WriteFoldedLines(_line - savedLine);
                captureStart = captureEnd = _position;
                hasPendingContent = false;
            }

            if (!IsWhiteSpace(ch))
            {
                captureEnd = _position + 1;
            }

            ch = Ch(++_position);
        }

        Capture(captureStart, captureEnd, false);
        return _scalar.Length > 0;
    }

    /// <summary>
    ///     Reads a single-quoted scalar into the scalar buffer (js-yaml's <c>readSingleQuotedScalar</c>).
    /// </summary>
    /// <returns>Whether a single-quoted scalar was read.</returns>
    private bool ReadSingleQuotedScalar()
    {
        if (Ch(_position) != '\'')
        {
            return false;
        }

        _scalar.Clear();
        _position++;
        var captureStart = _position;
        var captureEnd = _position;
        char ch;

        while ((ch = Ch(_position)) != '\0')
        {
            if (ch == '\'')
            {
                // A doubled quote is a quote; a single one ends the scalar
                Capture(captureStart, _position, true);
                ch = Ch(++_position);
                if (ch != '\'')
                {
                    return true;
                }

                captureStart = _position;
                _position++;
                captureEnd = _position;
            }
            else if (IsEol(ch))
            {
                Capture(captureStart, captureEnd, true);
                WriteFoldedLines(SkipSeparationSpace(false));
                captureStart = captureEnd = _position;
            }
            else if (_position == _lineStart && TestDocumentSeparator())
            {
                throw Invalid("unexpected end of the document within a single quoted scalar");
            }
            else
            {
                _position++;
                if (!IsWhiteSpace(ch))
                {
                    captureEnd = _position;
                }
            }
        }

        throw Invalid("unexpected end of the stream within a single quoted scalar");
    }

    /// <summary>
    ///     Reads a double-quoted scalar into the scalar buffer, with its escapes (js-yaml's <c>readDoubleQuotedScalar</c>).
    /// </summary>
    /// <returns>Whether a double-quoted scalar was read.</returns>
    private bool ReadDoubleQuotedScalar()
    {
        if (Ch(_position) != '"')
        {
            return false;
        }

        _scalar.Clear();
        _position++;
        var captureStart = _position;
        var captureEnd = _position;
        char ch;

        while ((ch = Ch(_position)) != '\0')
        {
            if (ch == '"')
            {
                Capture(captureStart, _position, true);
                _position++;
                return true;
            }

            if (ch == '\\')
            {
                Capture(captureStart, _position, true);
                ReadEscape();
                captureStart = captureEnd = _position;
            }
            else if (IsEol(ch))
            {
                Capture(captureStart, captureEnd, true);
                WriteFoldedLines(SkipSeparationSpace(false));
                captureStart = captureEnd = _position;
            }
            else if (_position == _lineStart && TestDocumentSeparator())
            {
                throw Invalid("unexpected end of the document within a double quoted scalar");
            }
            else
            {
                _position++;
                if (!IsWhiteSpace(ch))
                {
                    captureEnd = _position;
                }
            }
        }

        throw Invalid("unexpected end of the stream within a double quoted scalar");
    }

    /// <summary>
    ///     Reads the escape after a backslash of a double-quoted scalar.
    /// </summary>
    private void ReadEscape()
    {
        var ch = Ch(++_position);

        // An escaped line break joins the lines without a space
        if (IsEol(ch))
        {
            SkipSeparationSpace(false);
            return;
        }

        if (SimpleEscape(ch) is { } simple)
        {
            _scalar.Append(simple);
            _position++;
            return;
        }

        var hexLength = ch switch { 'x' => 2, 'u' => 4, 'U' => 8, _ => 0 };
        if (hexLength == 0)
        {
            throw Invalid("unknown escape sequence");
        }

        long code = 0;
        for (; hexLength > 0; hexLength--)
        {
            ch = Ch(++_position);
            if (!char.IsAsciiHexDigit(ch))
            {
                throw Invalid("expected hexadecimal character");
            }

            code = (code << 4) + int.Parse(ch.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        AppendCodePoint(_scalar, code);
        _position++;
    }

    /// <summary>
    ///     Skips white space, comments and line breaks (js-yaml's <c>skipSeparationSpace</c>), tracking lines and the
    ///     indentation of the last line reached.
    /// </summary>
    /// <param name="allowComments">Whether comments are skipped too.</param>
    /// <returns>The number of line breaks skipped.</returns>
    private int SkipSeparationSpace(bool allowComments)
    {
        var lineBreaks = 0;
        var ch = Ch(_position);

        while (ch != '\0')
        {
            while (IsWhiteSpace(ch))
            {
                if (ch == '\t' && _firstTabInLine == -1)
                {
                    _firstTabInLine = _position;
                }

                ch = Ch(++_position);
            }

            if (allowComments && ch == '#')
            {
                do
                {
                    ch = Ch(++_position);
                }
                while (ch is not '\n' and not '\r' and not '\0');
            }

            if (!IsEol(ch))
            {
                break;
            }

            ReadLineBreak();
            ch = Ch(_position);
            lineBreaks++;
            _lineIndent = 0;
            while (ch == ' ')
            {
                _lineIndent++;
                ch = Ch(++_position);
            }
        }

        return lineBreaks;
    }

    /// <summary>
    ///     Consumes one line break (<c>\n</c>, <c>\r</c> or <c>\r\n</c>) and starts a new line.
    /// </summary>
    private void ReadLineBreak()
    {
        var ch = Ch(_position);
        if (ch == '\n')
        {
            _position++;
        }
        else if (ch == '\r')
        {
            _position++;
            if (Ch(_position) == '\n')
            {
                _position++;
            }
        }
        else
        {
            throw Invalid("a line break is expected");
        }

        _line++;
        _lineStart = _position;
        _firstTabInLine = -1;
    }

    /// <summary>
    ///     Tests for a document marker (<c>---</c> or <c>...</c> followed by white space or the end) at the position.
    /// </summary>
    /// <returns>Whether there is one.</returns>
    private bool TestDocumentSeparator()
    {
        var ch = Ch(_position);
        return ch is '-' or '.' && Ch(_position + 1) == ch && Ch(_position + 2) == ch &&
               (Ch(_position + 3) == '\0' || IsWsOrEol(Ch(_position + 3)));
    }

    /// <summary>
    ///     Appends the line folding of a multi-line flow scalar: one break is a space, more are line feeds.
    /// </summary>
    /// <param name="count">The number of line breaks.</param>
    private void WriteFoldedLines(int count)
    {
        if (count == 1)
        {
            _scalar.Append(' ');
        }
        else if (count > 1)
        {
            _scalar.Append('\n', count - 1);
        }
    }

    /// <summary>
    ///     Appends a segment of the input to the scalar buffer after checking its characters.
    /// </summary>
    /// <param name="start">The first position.</param>
    /// <param name="end">The position after the last.</param>
    /// <param name="quoted">Whether the segment comes from a quoted scalar.</param>
    private void Capture(int start, int end, bool quoted)
    {
        if (start >= end)
        {
            return;
        }

        var segment = _input.AsSpan(start, end - start);
        if (!IsPrintable(segment, quoted))
        {
            throw Invalid(quoted ? "expected valid JSON character" : "the stream contains non-printable characters");
        }

        _scalar.Append(segment);
    }

    /// <summary>
    ///     Counts a collection that starts and fails when it nests deeper than the limit.
    /// </summary>
    /// <param name="start">The position of the collection, for the error.</param>
    private void EnterCollection(int start)
    {
        _collections++;
        if (_collections > _maxDepth)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the YAML nests collections deeper than the limit of {_maxDepth} (MermaidLimits.MaxYamlDepth)"),
                _locate(start),
                "flatten the YAML");
        }
    }


    /// <summary>
    ///     Converts a key node to the string key of a JavaScript object (js-yaml's <c>storeMappingPair</c>).
    /// </summary>
    /// <param name="key">The key node, or null for an empty key.</param>
    /// <param name="position">The position of the key, for errors.</param>
    /// <returns>The key string.</returns>
    private string KeyString(YamlNode? key, int position)
    {
        if (key is YamlSequence sequence && sequence.Items.Any(item => item is YamlSequence))
        {
            throw Invalid("nested arrays are not supported inside keys", position);
        }

        return key?.ToJsString() ?? "null";
    }

    /// <summary>
    ///     Stores a key and value, refusing a duplicated key.
    /// </summary>
    /// <param name="entries">The entries of the mapping.</param>
    /// <param name="keys">The keys stored so far.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, or null for an empty value.</param>
    /// <param name="keyPosition">The position of the key.</param>
    private void StoreEntry(List<YamlEntry> entries, HashSet<string> keys, string key, YamlNode? value, int keyPosition)
    {
        if (!keys.Add(key))
        {
            throw Invalid("duplicated mapping key", keyPosition);
        }

        var location = _locate(keyPosition);
        entries.Add(new YamlEntry(key, value ?? new YamlScalar(null, location), location));
    }

    /// <summary>
    ///     Gets the character at a position; the terminating NUL past the end.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns>The character.</returns>
    private char Ch(int position) => position >= 0 && position < _input.Length ? _input[position] : '\0';

    /// <summary>
    ///     Creates the exception for invalid YAML at the current position (or another one).
    /// </summary>
    /// <param name="message">js-yaml's description of the problem.</param>
    /// <param name="position">The position, or -1 for the current one.</param>
    /// <returns>The exception, ready to throw.</returns>
    private MermaidSyntaxException Invalid(string message, int position = -1) =>
        new(
            MermaidDiagnosticCodes.YamlInvalid,
            $"the YAML is not valid: {message}",
            _locate(position < 0 ? _position : position),
            "check the indentation (spaces only) and quote values that contain ': ', '#' or start with a special character");

    /// <summary>
    ///     Creates the exception for a refused YAML construct at the current position.
    /// </summary>
    /// <param name="message">The description of the construct.</param>
    /// <returns>The exception, ready to throw.</returns>
    private MermaidSyntaxException Unsupported(string message) =>
        new(
            MermaidDiagnosticCodes.YamlUnsupported,
            message,
            _locate(_position),
            "write the value out in full; anchors, aliases, tags, explicit keys and directives are refused for safety");

    /// <summary>Gets a value indicating whether a character is a space or a tab (js-yaml's <c>isWhiteSpace</c>).</summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsWhiteSpace(char c) => c is ' ' or '\t';

    /// <summary>Gets a value indicating whether a character is a line feed or a carriage return.</summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsEol(char c) => c is '\n' or '\r';

    /// <summary>Gets a value indicating whether a character is white space or a line break.</summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsWsOrEol(char c) => IsWhiteSpace(c) || IsEol(c);

    /// <summary>Gets a value indicating whether a character is a flow indicator (<c>, [ ] { }</c>).</summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsFlowIndicator(char c) => c is ',' or '[' or ']' or '{' or '}';
}

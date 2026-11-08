// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <summary>
///     The token kinds of the flowchart grammar: the names of the terminals of Mermaid's <c>flow.jison</c>.
/// </summary>
/// <remarks>
///     The kinds are the jison terminal names, so messages and <see cref="MermaidSyntaxException.Expected"/> lists read
///     like Mermaid's parse errors. The one exception is <see cref="Eof"/>, the <c>EOF</c> terminal that the lexer
///     produces once at the end of the text, which must be told apart from <see cref="SourceScanner.EndOfInput"/> (the
///     end of the token stream, jison's <c>$end</c>). The class only holds constants and is safe for concurrent use.
/// </remarks>
internal static class FlowchartTokens
{
    /// <summary>A semicolon (<c>SEMI</c>).</summary>
    public const string Semi = "SEMI";

    /// <summary>One or more line breaks (<c>NEWLINE</c>).</summary>
    public const string Newline = "NEWLINE";

    /// <summary>One white-space character (<c>SPACE</c>).</summary>
    public const string Space = "SPACE";

    /// <summary>The <c>EOF</c> terminal, produced once when the lexer reaches the end of the text in its initial state.</summary>
    public const string Eof = "EOF_TERMINAL";

    /// <summary>The <c>graph</c> or <c>flowchart</c> keyword (<c>GRAPH</c>).</summary>
    public const string Graph = "GRAPH";

    /// <summary>A header without a direction (<c>NODIR</c>).</summary>
    public const string NoDir = "NODIR";

    /// <summary>The direction of the header (<c>DIR</c>).</summary>
    public const string Dir = "DIR";

    /// <summary>The <c>subgraph</c> keyword.</summary>
    public const string Subgraph = "subgraph";

    /// <summary>A square bracket that opens a label (<c>SQS</c>).</summary>
    public const string Sqs = "SQS";

    /// <summary>A square bracket that closes a label (<c>SQE</c>).</summary>
    public const string Sqe = "SQE";

    /// <summary>The <c>end</c> keyword with the white space after it.</summary>
    public const string End = "end";

    /// <summary>The <c>accTitle:</c> keyword.</summary>
    public const string AccTitle = "acc_title";

    /// <summary>The text of an accessible title.</summary>
    public const string AccTitleValue = "acc_title_value";

    /// <summary>The <c>accDescr:</c> keyword.</summary>
    public const string AccDescr = "acc_descr";

    /// <summary>The text of a one-line accessible description.</summary>
    public const string AccDescrValue = "acc_descr_value";

    /// <summary>The text of a multi-line accessible description (<c>accDescr { ... }</c>).</summary>
    public const string AccDescrMultilineValue = "acc_descr_multiline_value";

    /// <summary>A part of the shape data of an <c>@{ ... }</c> block (<c>SHAPE_DATA</c>).</summary>
    public const string ShapeData = "SHAPE_DATA";

    /// <summary>An ampersand that chains nodes (<c>AMP</c>).</summary>
    public const string Amp = "AMP";

    /// <summary>The <c>:::</c> class separator (<c>STYLE_SEPARATOR</c>).</summary>
    public const string StyleSeparator = "STYLE_SEPARATOR";

    /// <summary>The <c>(((</c> opener of a double circle.</summary>
    public const string DoubleCircleStart = "DOUBLECIRCLESTART";

    /// <summary>The <c>)))</c> closer of a double circle.</summary>
    public const string DoubleCircleEnd = "DOUBLECIRCLEEND";

    /// <summary>An opening parenthesis (<c>PS</c>).</summary>
    public const string Ps = "PS";

    /// <summary>A closing parenthesis (<c>PE</c>).</summary>
    public const string Pe = "PE";

    /// <summary>The <c>(-</c> opener of an ellipse.</summary>
    public const string EllipseStart = "(-";

    /// <summary>The <c>-)</c> closer of an ellipse.</summary>
    public const string EllipseEnd = "-)";

    /// <summary>The <c>([</c> opener of a stadium.</summary>
    public const string StadiumStart = "STADIUMSTART";

    /// <summary>The <c>])</c> closer of a stadium.</summary>
    public const string StadiumEnd = "STADIUMEND";

    /// <summary>The <c>[[</c> opener of a subroutine.</summary>
    public const string SubroutineStart = "SUBROUTINESTART";

    /// <summary>The <c>]]</c> closer of a subroutine.</summary>
    public const string SubroutineEnd = "SUBROUTINEEND";

    /// <summary>The <c>[|</c> opener of a vertex with a property.</summary>
    public const string VertexWithPropsStart = "VERTEX_WITH_PROPS_START";

    /// <summary>A colon (<c>COLON</c>).</summary>
    public const string Colon = "COLON";

    /// <summary>A vertical bar (<c>PIPE</c>).</summary>
    public const string Pipe = "PIPE";

    /// <summary>The <c>[(</c> opener of a cylinder.</summary>
    public const string CylinderStart = "CYLINDERSTART";

    /// <summary>The <c>)]</c> closer of a cylinder.</summary>
    public const string CylinderEnd = "CYLINDEREND";

    /// <summary>A brace that opens a diamond label (<c>DIAMOND_START</c>).</summary>
    public const string DiamondStart = "DIAMOND_START";

    /// <summary>A brace that closes a diamond label (<c>DIAMOND_STOP</c>).</summary>
    public const string DiamondStop = "DIAMOND_STOP";

    /// <summary>A <c>&gt;</c> (<c>TAGEND</c>): the opener of an odd shape.</summary>
    public const string TagEnd = "TAGEND";

    /// <summary>The <c>[/</c> opener of a trapezoid or a lean-right shape.</summary>
    public const string TrapStart = "TRAPSTART";

    /// <summary>The <c>\]</c> closer of a trapezoid or a lean-left shape.</summary>
    public const string TrapEnd = "TRAPEND";

    /// <summary>The <c>[\</c> opener of an inverted trapezoid or a lean-left shape.</summary>
    public const string InvTrapStart = "INVTRAPSTART";

    /// <summary>The <c>/]</c> closer of an inverted trapezoid or a lean-right shape.</summary>
    public const string InvTrapEnd = "INVTRAPEND";

    /// <summary>
    ///     A terminal of the grammar that the lexer never produces (<c>TESTSTR</c>); jison lists it as expected after a
    ///     link, so the parser does too.
    /// </summary>
    public const string TestStr = "TESTSTR";

    /// <summary>The start of a link with text (<c>--</c>, <c>==</c> or <c>-.</c>; <c>START_LINK</c>).</summary>
    public const string StartLink = "START_LINK";

    /// <summary>A link (<c>LINK</c>).</summary>
    public const string Link = "LINK";

    /// <summary>The id of an edge (<c>e1@</c>; <c>LINK_ID</c>).</summary>
    public const string LinkId = "LINK_ID";

    /// <summary>The text of a quoted string (<c>STR</c>).</summary>
    public const string Str = "STR";

    /// <summary>The text of a markdown string (<c>MD_STR</c>).</summary>
    public const string MdStr = "MD_STR";

    /// <summary>The <c>style</c> keyword.</summary>
    public const string Style = "STYLE";

    /// <summary>The <c>linkStyle</c> keyword.</summary>
    public const string LinkStyle = "LINKSTYLE";

    /// <summary>The <c>classDef</c> keyword.</summary>
    public const string ClassDef = "CLASSDEF";

    /// <summary>The <c>class</c> keyword.</summary>
    public const string Class = "CLASS";

    /// <summary>The ids of a <c>click</c> statement (the keyword is part of the match).</summary>
    public const string Click = "CLICK";

    /// <summary>The <c>v</c> arrow head word (<c>DOWN</c>).</summary>
    public const string Down = "DOWN";

    /// <summary>The <c>^</c> character (<c>UP</c>).</summary>
    public const string Up = "UP";

    /// <summary>The function name of a <c>call</c>.</summary>
    public const string CallbackName = "CALLBACKNAME";

    /// <summary>The arguments of a <c>call</c>.</summary>
    public const string CallbackArgs = "CALLBACKARGS";

    /// <summary>The <c>href</c> keyword.</summary>
    public const string Href = "HREF";

    /// <summary>A link target (<c>_self</c>, <c>_blank</c>, <c>_parent</c> or <c>_top</c>).</summary>
    public const string LinkTarget = "LINK_TARGET";

    /// <summary>The <c>default</c> keyword.</summary>
    public const string Default = "DEFAULT";

    /// <summary>The <c>interpolate</c> keyword.</summary>
    public const string Interpolate = "INTERPOLATE";

    /// <summary>A run of digits (<c>NUM</c>).</summary>
    public const string Num = "NUM";

    /// <summary>A comma (<c>COMMA</c>).</summary>
    public const string Comma = "COMMA";

    /// <summary>A run of id characters (<c>NODE_STRING</c>).</summary>
    public const string NodeString = "NODE_STRING";

    /// <summary>A <c>#</c> (<c>BRKT</c>).</summary>
    public const string Brkt = "BRKT";

    /// <summary>A lone minus (<c>MINUS</c>).</summary>
    public const string Minus = "MINUS";

    /// <summary>An asterisk (<c>MULT</c>).</summary>
    public const string Mult = "MULT";

    /// <summary>A letter outside ASCII (<c>UNICODE_TEXT</c>).</summary>
    public const string UnicodeText = "UNICODE_TEXT";

    /// <summary>Label text (<c>TEXT</c>).</summary>
    public const string Text = "TEXT";

    /// <summary>A <c>&lt;</c> (<c>TAGSTART</c>).</summary>
    public const string TagStart = "TAGSTART";

    /// <summary>One character of the text of a link (<c>EDGE_TEXT</c>).</summary>
    public const string EdgeText = "EDGE_TEXT";

    /// <summary>A <c>direction TB</c> statement.</summary>
    public const string DirectionTb = "direction_tb";

    /// <summary>A <c>direction BT</c> statement.</summary>
    public const string DirectionBt = "direction_bt";

    /// <summary>A <c>direction RL</c> statement.</summary>
    public const string DirectionRl = "direction_rl";

    /// <summary>A <c>direction LR</c> statement.</summary>
    public const string DirectionLr = "direction_lr";

    /// <summary>A <c>direction TD</c> statement.</summary>
    public const string DirectionTd = "direction_td";

    /// <summary>The <c>\|</c> separator, which the grammar does not accept anywhere (<c>SEP</c>).</summary>
    public const string Sep = "SEP";

    /// <summary>A lone double quote that no string rule takes (<c>QUOTE</c>; not accepted anywhere).</summary>
    public const string Quote = "QUOTE";

    /// <summary>The terminal numbers of the jison grammar, which order the expected tokens of an error as jison lists them.</summary>
    private static readonly FrozenDictionary<string, int> Numbers = new Dictionary<string, int>
    {
        [Semi] = 8,
        [Newline] = 9,
        [Space] = 10,
        [Eof] = 11,
        [Graph] = 12,
        [NoDir] = 13,
        [Dir] = 14,
        [Subgraph] = 27,
        [Sqs] = 29,
        [Sqe] = 31,
        [End] = 32,
        [AccTitle] = 34,
        [AccTitleValue] = 35,
        [AccDescr] = 36,
        [AccDescrValue] = 37,
        [AccDescrMultilineValue] = 38,
        [ShapeData] = 40,
        [Amp] = 44,
        [StyleSeparator] = 46,
        [DoubleCircleStart] = 48,
        [DoubleCircleEnd] = 49,
        [Ps] = 50,
        [Pe] = 51,
        [EllipseStart] = 52,
        [EllipseEnd] = 53,
        [StadiumStart] = 54,
        [StadiumEnd] = 55,
        [SubroutineStart] = 56,
        [SubroutineEnd] = 57,
        [VertexWithPropsStart] = 58,
        [Colon] = 60,
        [Pipe] = 62,
        [CylinderStart] = 63,
        [CylinderEnd] = 64,
        [DiamondStart] = 65,
        [DiamondStop] = 66,
        [TagEnd] = 67,
        [TrapStart] = 68,
        [TrapEnd] = 69,
        [InvTrapStart] = 70,
        [InvTrapEnd] = 71,
        [TestStr] = 74,
        [StartLink] = 75,
        [Link] = 77,
        [LinkId] = 78,
        [Str] = 80,
        [MdStr] = 81,
        [Style] = 84,
        [LinkStyle] = 85,
        [ClassDef] = 86,
        [Class] = 87,
        [Click] = 88,
        [Down] = 89,
        [Up] = 90,
        [CallbackName] = 95,
        [CallbackArgs] = 96,
        [Href] = 97,
        [LinkTarget] = 98,
        [Default] = 102,
        [Interpolate] = 104,
        [Num] = 105,
        [Comma] = 106,
        [NodeString] = 109,
        [Brkt] = 111,
        [Minus] = 114,
        [Mult] = 115,
        [UnicodeText] = 116,
        [Text] = 117,
        [TagStart] = 118,
        [EdgeText] = 119,
        [DirectionTb] = 121,
        [DirectionBt] = 122,
        [DirectionRl] = 123,
        [DirectionLr] = 124,
        [DirectionTd] = 125,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the jison name of a token kind, as Mermaid's parse errors print it.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name: the kind itself, <c>EOF</c> for <see cref="Eof"/>, or <c>end of input</c> for the end of the stream.</returns>
    public static string JisonName(string kind) => kind switch
    {
        Eof => "EOF",
        SourceScanner.EndOfInput => "end of input",
        _ => kind,
    };

    /// <summary>
    ///     Gets the jison terminal number of a token kind, which orders expected-token lists as jison prints them.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The number; kinds the grammar does not know sort last.</returns>
    public static int Number(string kind) => Numbers.TryGetValue(kind, out var number) ? number : int.MaxValue;
}

/// <content>
///     The lexer: a port of the rules and start conditions of the lexer that jison generates from Mermaid's
///     <c>flow.jison</c>, with hand-written matchers in place of its regular expressions.
/// </content>
internal sealed partial class FlowchartParser
{
    /// <summary>The rule indexes active in every start condition other than the initial one, in jison order.</summary>
    private static readonly int[] SharedRules = [21, 24, 79, 82, 84, 86, 90, 92, 96, 97, 110, 112, 114, 116];

    /// <summary>
    ///     The start conditions of the lexer and the indexes of their rules, in the order jison tries them (the
    ///     <c>conditions</c> table of the generated lexer).
    /// </summary>
    private static readonly FrozenDictionary<string, IReadOnlyList<int>> Conditions = new Dictionary<string, IReadOnlyList<int>>
    {
        ["shapeDataEndBracket"] = SharedRules,
        ["shapeDataStr"] = With(9, 10),
        ["shapeData"] = With(8, 11, 12),
        ["callbackargs"] = With(17, 18),
        ["callbackname"] = With(14, 15, 16),
        ["href"] = SharedRules,
        ["click"] = With(33, 34),
        ["dottedEdgeText"] = With(76, 78),
        ["thickEdgeText"] = With(73, 75),
        ["edgeText"] = With(70, 72),
        ["trapText"] = With(93, 94, 95),
        ["ellipseText"] = With(80, 81),
        ["text"] = With(83, 85, 89, 91, 109, 111, 113, 115, 117),
        ["vertex"] = SharedRules,
        ["dir"] = With(45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55),
        ["acc_descr_multiline"] = With(5, 6),
        ["acc_descr"] = With(3),
        ["acc_title"] = With(1),
        ["md_string"] = With(19, 20),
        ["string"] = With(22, 23),
        [SourceScanner.InitialState] =
        [
            0, 2, 4, 7, 13, 21, 24, 25, 26, 27, 28, 29, 30, 31, 32, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 56, 57, 58,
            59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 73, 74, 76, 77, 79, 82, 84, 86, 87, 88, 90, 92, 96, 97,
            98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 110, 112, 114, 116, 118, 119, 120, 121,
        ],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    ///     The letters outside ASCII that the <c>UNICODE_TEXT</c> rule matches, as sorted inclusive ranges of UTF-16 code
    ///     units (generated from the rule's character classes).
    /// </summary>
    private static readonly (int Low, int High)[] UnicodeLetters =
    [
        (0x00AA, 0x00AA), (0x00B5, 0x00B5), (0x00BA, 0x00BA), (0x00C0, 0x00D6), (0x00D8, 0x00F6), (0x00F8, 0x02C1),
        (0x02C6, 0x02D1), (0x02E0, 0x02E4), (0x02EC, 0x02EC), (0x02EE, 0x02EE), (0x0370, 0x0374), (0x0376, 0x0377),
        (0x037A, 0x037D), (0x0386, 0x0386), (0x0388, 0x038A), (0x038C, 0x038C), (0x038E, 0x03A1), (0x03A3, 0x03F5),
        (0x03F7, 0x0481), (0x048A, 0x0527), (0x0531, 0x0556), (0x0559, 0x0559), (0x0561, 0x0587), (0x05D0, 0x05EA),
        (0x05F0, 0x05F2), (0x0620, 0x064A), (0x066E, 0x066F), (0x0671, 0x06D3), (0x06D5, 0x06D5), (0x06E5, 0x06E6),
        (0x06EE, 0x06EF), (0x06FA, 0x06FC), (0x06FF, 0x06FF), (0x0710, 0x0710), (0x0712, 0x072F), (0x074D, 0x07A5),
        (0x07B1, 0x07B1), (0x07CA, 0x07EA), (0x07F4, 0x07F5), (0x07FA, 0x07FA), (0x0800, 0x0815), (0x081A, 0x081A),
        (0x0824, 0x0824), (0x0828, 0x0828), (0x0840, 0x0858), (0x08A0, 0x08A0), (0x08A2, 0x08AC), (0x0904, 0x0939),
        (0x093D, 0x093D), (0x0950, 0x0950), (0x0958, 0x0961), (0x0971, 0x0977), (0x0979, 0x097F), (0x0985, 0x098C),
        (0x098F, 0x0990), (0x0993, 0x09A8), (0x09AA, 0x09B0), (0x09B2, 0x09B2), (0x09B6, 0x09B9), (0x09BD, 0x09BD),
        (0x09CE, 0x09CE), (0x09DC, 0x09DD), (0x09DF, 0x09E1), (0x09F0, 0x09F1), (0x0A05, 0x0A0A), (0x0A0F, 0x0A10),
        (0x0A13, 0x0A28), (0x0A2A, 0x0A30), (0x0A32, 0x0A33), (0x0A35, 0x0A36), (0x0A38, 0x0A39), (0x0A59, 0x0A5C),
        (0x0A5E, 0x0A5E), (0x0A72, 0x0A74), (0x0A85, 0x0A8D), (0x0A8F, 0x0A91), (0x0A93, 0x0AA8), (0x0AAA, 0x0AB0),
        (0x0AB2, 0x0AB3), (0x0AB5, 0x0AB9), (0x0ABD, 0x0ABD), (0x0AD0, 0x0AD0), (0x0AE0, 0x0AE1), (0x0B05, 0x0B0C),
        (0x0B0F, 0x0B10), (0x0B13, 0x0B28), (0x0B2A, 0x0B30), (0x0B32, 0x0B33), (0x0B35, 0x0B39), (0x0B3D, 0x0B3D),
        (0x0B5C, 0x0B5D), (0x0B5F, 0x0B61), (0x0B71, 0x0B71), (0x0B83, 0x0B83), (0x0B85, 0x0B8A), (0x0B8E, 0x0B90),
        (0x0B92, 0x0B95), (0x0B99, 0x0B9A), (0x0B9C, 0x0B9C), (0x0B9E, 0x0B9F), (0x0BA3, 0x0BA4), (0x0BA8, 0x0BAA),
        (0x0BAE, 0x0BB9), (0x0BD0, 0x0BD0), (0x0C05, 0x0C0C), (0x0C0E, 0x0C10), (0x0C12, 0x0C28), (0x0C2A, 0x0C33),
        (0x0C35, 0x0C39), (0x0C3D, 0x0C3D), (0x0C58, 0x0C59), (0x0C60, 0x0C61), (0x0C85, 0x0C8C), (0x0C8E, 0x0C90),
        (0x0C92, 0x0CA8), (0x0CAA, 0x0CB3), (0x0CB5, 0x0CB9), (0x0CBD, 0x0CBD), (0x0CDE, 0x0CDE), (0x0CE0, 0x0CE1),
        (0x0CF1, 0x0CF2), (0x0D05, 0x0D0C), (0x0D0E, 0x0D10), (0x0D12, 0x0D3A), (0x0D3D, 0x0D3D), (0x0D4E, 0x0D4E),
        (0x0D60, 0x0D61), (0x0D7A, 0x0D7F), (0x0D85, 0x0D96), (0x0D9A, 0x0DB1), (0x0DB3, 0x0DBB), (0x0DBD, 0x0DBD),
        (0x0DC0, 0x0DC6), (0x0E01, 0x0E30), (0x0E32, 0x0E33), (0x0E40, 0x0E46), (0x0E81, 0x0E82), (0x0E84, 0x0E84),
        (0x0E87, 0x0E88), (0x0E8A, 0x0E8A), (0x0E8D, 0x0E8D), (0x0E94, 0x0E97), (0x0E99, 0x0E9F), (0x0EA1, 0x0EA3),
        (0x0EA5, 0x0EA5), (0x0EA7, 0x0EA7), (0x0EAA, 0x0EAB), (0x0EAD, 0x0EB0), (0x0EB2, 0x0EB3), (0x0EBD, 0x0EBD),
        (0x0EC0, 0x0EC4), (0x0EC6, 0x0EC6), (0x0EDC, 0x0EDF), (0x0F00, 0x0F00), (0x0F40, 0x0F47), (0x0F49, 0x0F6C),
        (0x0F88, 0x0F8C), (0x1000, 0x102A), (0x103F, 0x103F), (0x1050, 0x1055), (0x105A, 0x105D), (0x1061, 0x1061),
        (0x1065, 0x1066), (0x106E, 0x1070), (0x1075, 0x1081), (0x108E, 0x108E), (0x10A0, 0x10C5), (0x10C7, 0x10C7),
        (0x10CD, 0x10CD), (0x10D0, 0x10FA), (0x10FC, 0x1248), (0x124A, 0x124D), (0x1250, 0x1256), (0x1258, 0x1258),
        (0x125A, 0x125D), (0x1260, 0x1288), (0x128A, 0x128D), (0x1290, 0x12B0), (0x12B2, 0x12B5), (0x12B8, 0x12BE),
        (0x12C0, 0x12C0), (0x12C2, 0x12C5), (0x12C8, 0x12D6), (0x12D8, 0x1310), (0x1312, 0x1315), (0x1318, 0x135A),
        (0x1380, 0x138F), (0x13A0, 0x13F4), (0x1401, 0x166C), (0x166F, 0x167F), (0x1681, 0x169A), (0x16A0, 0x16EA),
        (0x1700, 0x170C), (0x170E, 0x1711), (0x1720, 0x1731), (0x1740, 0x1751), (0x1760, 0x176C), (0x176E, 0x1770),
        (0x1780, 0x17B3), (0x17D7, 0x17D7), (0x17DC, 0x17DC), (0x1820, 0x1877), (0x1880, 0x18A8), (0x18AA, 0x18AA),
        (0x18B0, 0x18F5), (0x1900, 0x191C), (0x1950, 0x196D), (0x1970, 0x1974), (0x1980, 0x19AB), (0x19C1, 0x19C7),
        (0x1A00, 0x1A16), (0x1A20, 0x1A54), (0x1AA7, 0x1AA7), (0x1B05, 0x1B33), (0x1B45, 0x1B4B), (0x1B83, 0x1BA0),
        (0x1BAE, 0x1BAF), (0x1BBA, 0x1BE5), (0x1C00, 0x1C23), (0x1C4D, 0x1C4F), (0x1C5A, 0x1C7D), (0x1CE9, 0x1CEC),
        (0x1CEE, 0x1CF1), (0x1CF5, 0x1CF6), (0x1D00, 0x1DBF), (0x1E00, 0x1F15), (0x1F18, 0x1F1D), (0x1F20, 0x1F45),
        (0x1F48, 0x1F4D), (0x1F50, 0x1F57), (0x1F59, 0x1F59), (0x1F5B, 0x1F5B), (0x1F5D, 0x1F5D), (0x1F5F, 0x1F7D),
        (0x1F80, 0x1FB4), (0x1FB6, 0x1FBC), (0x1FBE, 0x1FBE), (0x1FC2, 0x1FC4), (0x1FC6, 0x1FCC), (0x1FD0, 0x1FD3),
        (0x1FD6, 0x1FDB), (0x1FE0, 0x1FEC), (0x1FF2, 0x1FF4), (0x1FF6, 0x1FFC), (0x2071, 0x2071), (0x207F, 0x207F),
        (0x2090, 0x209C), (0x2102, 0x2102), (0x2107, 0x2107), (0x210A, 0x2113), (0x2115, 0x2115), (0x2119, 0x211D),
        (0x2124, 0x2124), (0x2126, 0x2126), (0x2128, 0x2128), (0x212A, 0x212D), (0x212F, 0x2139), (0x213C, 0x213F),
        (0x2145, 0x2149), (0x214E, 0x214E), (0x2183, 0x2184), (0x2C00, 0x2C2E), (0x2C30, 0x2C5E), (0x2C60, 0x2CE4),
        (0x2CEB, 0x2CEE), (0x2CF2, 0x2CF3), (0x2D00, 0x2D25), (0x2D27, 0x2D27), (0x2D2D, 0x2D2D), (0x2D30, 0x2D67),
        (0x2D6F, 0x2D6F), (0x2D80, 0x2D96), (0x2DA0, 0x2DA6), (0x2DA8, 0x2DAE), (0x2DB0, 0x2DB6), (0x2DB8, 0x2DBE),
        (0x2DC0, 0x2DC6), (0x2DC8, 0x2DCE), (0x2DD0, 0x2DD6), (0x2DD8, 0x2DDE), (0x2E2F, 0x2E2F), (0x3005, 0x3006),
        (0x3031, 0x3035), (0x303B, 0x303C), (0x3041, 0x3096), (0x309D, 0x309F), (0x30A1, 0x30FA), (0x30FC, 0x30FF),
        (0x3105, 0x312D), (0x3131, 0x318E), (0x31A0, 0x31BA), (0x31F0, 0x31FF), (0x3400, 0x4DB5), (0x4E00, 0x9FCC),
        (0xA000, 0xA48C), (0xA4D0, 0xA4FD), (0xA500, 0xA60C), (0xA610, 0xA61F), (0xA62A, 0xA62B), (0xA640, 0xA66E),
        (0xA67F, 0xA697), (0xA6A0, 0xA6E5), (0xA717, 0xA71F), (0xA722, 0xA788), (0xA78B, 0xA78E), (0xA790, 0xA793),
        (0xA7A0, 0xA7AA), (0xA7F8, 0xA801), (0xA803, 0xA805), (0xA807, 0xA80A), (0xA80C, 0xA822), (0xA840, 0xA873),
        (0xA882, 0xA8B3), (0xA8F2, 0xA8F7), (0xA8FB, 0xA8FB), (0xA90A, 0xA925), (0xA930, 0xA946), (0xA960, 0xA97C),
        (0xA984, 0xA9B2), (0xA9CF, 0xA9CF), (0xAA00, 0xAA28), (0xAA40, 0xAA42), (0xAA44, 0xAA4B), (0xAA60, 0xAA76),
        (0xAA7A, 0xAA7A), (0xAA80, 0xAAAF), (0xAAB1, 0xAAB1), (0xAAB5, 0xAAB6), (0xAAB9, 0xAABD), (0xAAC0, 0xAAC0),
        (0xAAC2, 0xAAC2), (0xAADB, 0xAADD), (0xAAE0, 0xAAEA), (0xAAF2, 0xAAF4), (0xAB01, 0xAB06), (0xAB09, 0xAB0E),
        (0xAB11, 0xAB16), (0xAB20, 0xAB26), (0xAB28, 0xAB2E), (0xABC0, 0xABE2), (0xAC00, 0xD7A3), (0xD7B0, 0xD7C6),
        (0xD7CB, 0xD7FB), (0xF900, 0xFA6D), (0xFA70, 0xFAD9), (0xFB00, 0xFB06), (0xFB13, 0xFB17), (0xFB1D, 0xFB1D),
        (0xFB1F, 0xFB28), (0xFB2A, 0xFB36), (0xFB38, 0xFB3C), (0xFB3E, 0xFB3E), (0xFB40, 0xFB41), (0xFB43, 0xFB44),
        (0xFB46, 0xFBB1), (0xFBD3, 0xFD3D), (0xFD50, 0xFD8F), (0xFD92, 0xFDC7), (0xFDF0, 0xFDFB), (0xFE70, 0xFE74),
        (0xFE76, 0xFEFC), (0xFF21, 0xFF3A), (0xFF41, 0xFF5A), (0xFF66, 0xFFBE), (0xFFC2, 0xFFC7), (0xFFCA, 0xFFCF),
        (0xFFD2, 0xFFD7), (0xFFDA, 0xFFDC),
    ];

    /// <summary>The start of the run of non-space characters that <see cref="_linkIdAt"/> belongs to.</summary>
    private int _linkIdRunStart;

    /// <summary>The end of the run of non-space characters that <see cref="_linkIdAt"/> belongs to.</summary>
    private int _linkIdRunEnd;

    /// <summary>The last usable <c>@</c> of the cached run, or -1.</summary>
    private int _linkIdAt = -1;

    /// <summary>The position the cached line of the direction rules was computed from.</summary>
    private int _directionLineStart;

    /// <summary>The end of the cached line of the direction rules (its first line terminator).</summary>
    private int _directionLineEnd;

    /// <summary>The last <c>direction</c> word of the cached line for each direction, or -1.</summary>
    private readonly Dictionary<string, int> _directionCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Builds a rule list of a start condition: the shared rules merged with the condition's own, in index order (the
    ///     order jison tries them).
    /// </summary>
    /// <param name="own">The condition's own rule indexes.</param>
    /// <returns>The sorted indexes.</returns>
    private static int[] With(params int[] own) => [.. SharedRules.Concat(own).Order()];

    /// <summary>
    ///     Creates the lexer of a text: the jison rules bound to this parser, whose caches and first-graph flag they use.
    /// </summary>
    /// <param name="text">The text, mapped to the original.</param>
    /// <returns>The scanner.</returns>
    private SourceScanner CreateScanner(MappedText text) => new(text, CreateRules(), Conditions);

    /// <summary>
    ///     Creates the 122 rules of the lexer, in jison order.
    /// </summary>
    /// <returns>The rules.</returns>
    private ScanRule[] CreateRules()
    {
        // Matchers of fixed texts and character classes
        static ScanMatcher Lit(string text) => ScanPatterns.Literal(text);
        static ScanMatcher Char(Func<char, bool> predicate) => (s, p) => p < s.Length && predicate(s[p]) ? 1 : -1;
        static ScanMatcher Run(Func<char, bool> predicate, int minimum) => ScanPatterns.Run(predicate, minimum);

        // Actions
        static Func<SourceScanner, string?> Token(string kind) => _ => kind;
        static Func<SourceScanner, string?> Push(string state, string? kind) => s =>
        {
            s.Begin(state);
            return kind;
        };
        static Func<SourceScanner, string?> Pop(string? kind) => s =>
        {
            s.PopState();
            return kind;
        };

        return
        [
            /* 0 */ new("accTitle", AccStart("accTitle", ':'), Push("acc_title", FlowchartTokens.AccTitle)),
            /* 1 */ new("accTitleValue", Run(c => c != '\n', 0), Pop(FlowchartTokens.AccTitleValue)),
            /* 2 */ new("accDescr", AccStart("accDescr", ':'), Push("acc_descr", FlowchartTokens.AccDescr)),
            /* 3 */ new("accDescrValue", Run(c => c != '\n', 0), Pop(FlowchartTokens.AccDescrValue)),
            /* 4 */ new("accDescrMultiline", AccStart("accDescr", '{'), Push("acc_descr_multiline", null)),
            /* 5 */ new("accDescrMultilineEnd", Lit("}"), Pop(null)),
            /* 6 */ new("accDescrMultilineValue", Run(c => c != '}', 0), Token(FlowchartTokens.AccDescrMultilineValue)),
            /* 7 */ new("shapeDataStart", Lit("@{"), s =>
            {
                s.Begin("shapeData");
                s.Text = string.Empty;
                return FlowchartTokens.ShapeData;
            }),
            /* 8 */ new("shapeDataQuote", Lit("\""), Push("shapeDataStr", FlowchartTokens.ShapeData)),
            /* 9 */ new("shapeDataQuoteEnd", Lit("\""), Pop(FlowchartTokens.ShapeData)),
            /* 10 */ new("shapeDataString", Run(c => c != '"', 1), s =>
            {
                s.Text = ReplaceLineBreaks(s.Text);
                return FlowchartTokens.ShapeData;
            }),
            /* 11 */ new("shapeDataText", Run(c => c is not ('}' or '^' or '"'), 1), Token(FlowchartTokens.ShapeData)),
            /* 12 */ new("shapeDataEnd", Lit("}"), Pop(null)),
            /* 13 */ new("call", ScanPatterns.Sequence(Lit("call"), Run(ScanPatterns.IsJsWhitespace, 1)), Push("callbackname", null)),
            /* 14 */ new("callbackEmptyArgs", EmptyParentheses, Pop(null)),
            /* 15 */ new("callbackArgsStart", Lit("("), s =>
            {
                s.PopState();
                s.Begin("callbackargs");
                return null;
            }),
            /* 16 */ new("callbackName", Run(c => c != '(', 0), Token(FlowchartTokens.CallbackName)),
            /* 17 */ new("callbackArgsEnd", Lit(")"), Pop(null)),
            /* 18 */ new("callbackArgs", Run(c => c != ')', 0), Token(FlowchartTokens.CallbackArgs)),
            /* 19 */ new("markdownText", Run(c => c is not ('`' or '"'), 1), Token(FlowchartTokens.MdStr)),
            /* 20 */ new("markdownEnd", Lit("`\""), Pop(null)),
            /* 21 */ new("markdownStart", Lit("\"`"), Push("md_string", null)),
            /* 22 */ new("stringText", Run(c => c != '"', 1), Token(FlowchartTokens.Str)),
            /* 23 */ new("stringEnd", Lit("\""), Pop(null)),
            /* 24 */ new("stringStart", Lit("\""), Push("string", null)),
            /* 25 */ new("style", Keyword("style"), Token(FlowchartTokens.Style)),
            /* 26 */ new("default", Keyword("default"), Token(FlowchartTokens.Default)),
            /* 27 */ new("linkStyle", Keyword("linkStyle"), Token(FlowchartTokens.LinkStyle)),
            /* 28 */ new("interpolate", Keyword("interpolate"), Token(FlowchartTokens.Interpolate)),
            /* 29 */ new("classDef", Keyword("classDef"), Token(FlowchartTokens.ClassDef)),
            /* 30 */ new("class", Keyword("class"), Token(FlowchartTokens.Class)),
            /* 31 */ new("href", ScanPatterns.Sequence(Lit("href"), Char(ScanPatterns.IsJsWhitespace)), Token(FlowchartTokens.Href)),
            /* 32 */ new("click", ScanPatterns.Sequence(Lit("click"), Run(ScanPatterns.IsJsWhitespace, 1)), Push("click", null)),
            /* 33 */ new("clickEnd", Char(ScanPatterns.IsJsWhitespace), Pop(null)),
            /* 34 */ new("clickIds", Run(c => !ScanPatterns.IsJsWhitespace(c), 0), Token(FlowchartTokens.Click)),
            /* 35 */ new("flowchartElk", Keyword("flowchart-elk"), GraphKeyword),
            /* 36 */ new("swimlaneBeta", Keyword("swimlane-beta"), GraphKeyword),
            /* 37 */ new("graph", Keyword("graph"), GraphKeyword),
            /* 38 */ new("flowchart", Keyword("flowchart"), GraphKeyword),
            /* 39 */ new("subgraph", Keyword("subgraph"), Token(FlowchartTokens.Subgraph)),
            /* 40 */ new("end", ScanPatterns.Sequence(Keyword("end"), Run(ScanPatterns.IsJsWhitespace, 0)), Token(FlowchartTokens.End)),
            /* 41 */ new("_self", Keyword("_self"), Token(FlowchartTokens.LinkTarget)),
            /* 42 */ new("_blank", Keyword("_blank"), Token(FlowchartTokens.LinkTarget)),
            /* 43 */ new("_parent", Keyword("_parent"), Token(FlowchartTokens.LinkTarget)),
            /* 44 */ new("_top", Keyword("_top"), Token(FlowchartTokens.LinkTarget)),
            /* 45 */ new("noDirection", NoDirection, Pop(FlowchartTokens.NoDir)),
            /* 46 */ new("dirLR", SpacedDirection("LR", true), Pop(FlowchartTokens.Dir)),
            /* 47 */ new("dirRL", SpacedDirection("RL", true), Pop(FlowchartTokens.Dir)),
            /* 48 */ new("dirTB", SpacedDirection("TB", true), Pop(FlowchartTokens.Dir)),
            /* 49 */ new("dirBT", SpacedDirection("BT", true), Pop(FlowchartTokens.Dir)),
            /* 50 */ new("dirTD", SpacedDirection("TD", true), Pop(FlowchartTokens.Dir)),
            /* 51 */ new("dirBR", SpacedDirection("BR", true), Pop(FlowchartTokens.Dir)),
            /* 52 */ new("dirLeft", SpacedDirection("<", false), Pop(FlowchartTokens.Dir)),
            /* 53 */ new("dirRight", SpacedDirection(">", false), Pop(FlowchartTokens.Dir)),
            /* 54 */ new("dirUp", SpacedDirection("^", false), Pop(FlowchartTokens.Dir)),
            /* 55 */ new("dirDown", SpacedDirection("v", true), Pop(FlowchartTokens.Dir)),
            /* 56 */ new("directionTB", DirectionLine("TB"), Token(FlowchartTokens.DirectionTb)),
            /* 57 */ new("directionBT", DirectionLine("BT"), Token(FlowchartTokens.DirectionBt)),
            /* 58 */ new("directionRL", DirectionLine("RL"), Token(FlowchartTokens.DirectionRl)),
            /* 59 */ new("directionLR", DirectionLine("LR"), Token(FlowchartTokens.DirectionLr)),
            /* 60 */ new("directionTD", DirectionLine("TD"), Token(FlowchartTokens.DirectionTd)),
            /* 61 */ new("linkId", MatchLinkId, Token(FlowchartTokens.LinkId)),
            /* 62 */ new("num", Run(char.IsAsciiDigit, 1), Token(FlowchartTokens.Num)),
            /* 63 */ new("brkt", Lit("#"), Token(FlowchartTokens.Brkt)),
            /* 64 */ new("styleSeparator", Lit(":::"), Token(FlowchartTokens.StyleSeparator)),
            /* 65 */ new("colon", Lit(":"), Token(FlowchartTokens.Colon)),
            /* 66 */ new("amp", Lit("&"), Token(FlowchartTokens.Amp)),
            /* 67 */ new("semi", Lit(";"), Token(FlowchartTokens.Semi)),
            /* 68 */ new("comma", Lit(","), Token(FlowchartTokens.Comma)),
            /* 69 */ new("mult", Lit("*"), Token(FlowchartTokens.Mult)),
            /* 70 */ new("link", (s, p) => MatchArrow(s, p, '-'), Pop(FlowchartTokens.Link)),
            /* 71 */ new("startLink", (s, p) => MatchStartLink(s, p, "--"), Push("edgeText", FlowchartTokens.StartLink)),
            /* 72 */ new("edgeText", EdgeTextCharacter, Token(FlowchartTokens.EdgeText)),
            /* 73 */ new("thickLink", (s, p) => MatchArrow(s, p, '='), Pop(FlowchartTokens.Link)),
            /* 74 */ new("startThickLink", (s, p) => MatchStartLink(s, p, "=="), Push("thickEdgeText", FlowchartTokens.StartLink)),
            /* 75 */ new("thickEdgeText", Char(c => c != '='), Token(FlowchartTokens.EdgeText)),
            /* 76 */ new("dottedLink", MatchDottedArrow, Pop(FlowchartTokens.Link)),
            /* 77 */ new("startDottedLink", (s, p) => MatchStartLink(s, p, "-."), Push("dottedEdgeText", FlowchartTokens.StartLink)),
            /* 78 */ new("dottedEdgeText", Char(c => c != '.'), Token(FlowchartTokens.EdgeText)),
            /* 79 */ new("invisibleLink", MatchInvisibleLink, Token(FlowchartTokens.Link)),
            /* 80 */ new("ellipseEnd", (s, p) => p + 1 < s.Length && s[p] is '-' or '/' or ')' && s[p + 1] == ')' ? 2 : -1, Pop(FlowchartTokens.EllipseEnd)),
            /* 81 */ new("ellipseText", Char(c => c is not ('(' or ')' or '[' or ']' or '{' or '}')), Token(FlowchartTokens.Text)),
            /* 82 */ new("ellipseStart", Lit("(-"), Push("ellipseText", FlowchartTokens.EllipseStart)),
            /* 83 */ new("stadiumEnd", Lit("])"), Pop(FlowchartTokens.StadiumEnd)),
            /* 84 */ new("stadiumStart", Lit("(["), Push("text", FlowchartTokens.StadiumStart)),
            /* 85 */ new("subroutineEnd", Lit("]]"), Pop(FlowchartTokens.SubroutineEnd)),
            /* 86 */ new("subroutineStart", Lit("[["), Push("text", FlowchartTokens.SubroutineStart)),
            /* 87 */ new("vertexWithPropsStart", Lit("[|"), Token(FlowchartTokens.VertexWithPropsStart)),
            /* 88 */ new("oddStart", Lit(">"), Push("text", FlowchartTokens.TagEnd)),
            /* 89 */ new("cylinderEnd", Lit(")]"), Pop(FlowchartTokens.CylinderEnd)),
            /* 90 */ new("cylinderStart", Lit("[("), Push("text", FlowchartTokens.CylinderStart)),
            /* 91 */ new("doubleCircleEnd", Lit(")))"), Pop(FlowchartTokens.DoubleCircleEnd)),
            /* 92 */ new("doubleCircleStart", Lit("((("), Push("text", FlowchartTokens.DoubleCircleStart)),
            /* 93 */ new("trapEnd", (s, p) => p + 1 < s.Length && s[p] is '\\' or '(' or '?' or '=' or ']' && s[p + 1] == ']' ? 2 : -1, Pop(FlowchartTokens.TrapEnd)),
            /* 94 */ new("invTrapEnd", Lit("/]"), Pop(FlowchartTokens.InvTrapEnd)),
            /* 95 */ new("trapText", TrapTextRun, Token(FlowchartTokens.Text)),
            /* 96 */ new("trapStart", Lit("[/"), Push("trapText", FlowchartTokens.TrapStart)),
            /* 97 */ new("invTrapStart", Lit("[\\"), Push("trapText", FlowchartTokens.InvTrapStart)),
            /* 98 */ new("tagStart", Lit("<"), Token(FlowchartTokens.TagStart)),
            /* 99 */ new("tagEnd", Lit(">"), Token(FlowchartTokens.TagEnd)),
            /* 100 */ new("up", Lit("^"), Token(FlowchartTokens.Up)),
            /* 101 */ new("sep", Lit("\\|"), Token(FlowchartTokens.Sep)),
            /* 102 */ new("down", Keyword("v"), Token(FlowchartTokens.Down)),
            /* 103 */ new("multAgain", Lit("*"), Token(FlowchartTokens.Mult)),
            /* 104 */ new("brktAgain", Lit("#"), Token(FlowchartTokens.Brkt)),
            /* 105 */ new("ampAgain", Lit("&"), Token(FlowchartTokens.Amp)),
            /* 106 */ new("nodeString", NodeStringRun, Token(FlowchartTokens.NodeString)),
            /* 107 */ new("minus", Lit("-"), Token(FlowchartTokens.Minus)),
            /* 108 */ new("unicodeText", Char(IsUnicodeLetter), Token(FlowchartTokens.UnicodeText)),
            /* 109 */ new("pipeEnd", Lit("|"), Pop(FlowchartTokens.Pipe)),
            /* 110 */ new("pipeStart", Lit("|"), Push("text", FlowchartTokens.Pipe)),
            /* 111 */ new("parenthesisEnd", Lit(")"), Pop(FlowchartTokens.Pe)),
            /* 112 */ new("parenthesisStart", Lit("("), Push("text", FlowchartTokens.Ps)),
            /* 113 */ new("squareEnd", Lit("]"), Pop(FlowchartTokens.Sqe)),
            /* 114 */ new("squareStart", Lit("["), Push("text", FlowchartTokens.Sqs)),
            /* 115 */ new("diamondStop", Lit("}"), Pop(FlowchartTokens.DiamondStop)),
            /* 116 */ new("diamondStart", Lit("{"), Push("text", FlowchartTokens.DiamondStart)),
            /* 117 */ new("text", Run(c => c is not ('[' or ']' or '(' or ')' or '{' or '}' or '|' or '"'), 1), Token(FlowchartTokens.Text)),
            /* 118 */ new("quote", Lit("\""), Token(FlowchartTokens.Quote)),
            /* 119 */ new("newline", NewlineRun, Token(FlowchartTokens.Newline)),
            /* 120 */ new("space", Char(ScanPatterns.IsJsWhitespace), Token(FlowchartTokens.Space)),
            /* 121 */ new("eof", ScanPatterns.EndOfInput, Token(FlowchartTokens.Eof)),
        ];
    }

    /// <summary>
    ///     The action of the header keywords: the first one enters the direction state (Mermaid's <c>firstGraph</c>).
    /// </summary>
    /// <param name="scanner">The scanner.</param>
    /// <returns>The <c>GRAPH</c> token kind.</returns>
    private string? GraphKeyword(SourceScanner scanner)
    {
        if (_model.FirstGraph())
        {
            scanner.Begin("dir");
        }

        return FlowchartTokens.Graph;
    }

    /// <summary>
    ///     Matches a keyword followed by a word boundary (<c>word\b</c>; every keyword ends with a word character).
    /// </summary>
    /// <param name="word">The keyword.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher Keyword(string word) => (s, p) =>
        Starts(s, p, word) && (p + word.Length == s.Length || !ScanPatterns.IsJsWordChar(s[p + word.Length]))
            ? word.Length
            : -1;

    /// <summary>
    ///     Matches <c>keyword\s*X\s*</c>, the start of an accessibility statement.
    /// </summary>
    /// <param name="keyword">The keyword.</param>
    /// <param name="separator">The separator character (<c>:</c> or <c>{</c>).</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher AccStart(string keyword, char separator) => (s, p) =>
    {
        if (!Starts(s, p, keyword))
        {
            return -1;
        }

        var i = SkipSpaces(s, p + keyword.Length);
        return i < s.Length && s[i] == separator ? SkipSpaces(s, i + 1) - p : -1;
    };

    /// <summary>
    ///     Matches <c>\([\s]*\)</c>, an empty argument list of a <c>call</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int EmptyParentheses(string s, int p)
    {
        if (p >= s.Length || s[p] != '(')
        {
            return -1;
        }

        var i = SkipSpaces(s, p + 1);
        return i < s.Length && s[i] == ')' ? i + 1 - p : -1;
    }

    /// <summary>
    ///     Matches <c>(\r?\n)*\s*\n</c>: white space up to and including its last line feed (a header without a direction).
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int NoDirection(string s, int p)
    {
        var end = SkipSpaces(s, p);
        for (var i = end - 1; i >= p; i--)
        {
            if (s[i] == '\n')
            {
                return i + 1 - p;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Matches <c>\s*X</c> or <c>\s*X\b</c>, a direction after the header keyword.
    /// </summary>
    /// <param name="direction">The direction text.</param>
    /// <param name="boundary">Whether a word boundary must follow.</param>
    /// <returns>The matcher.</returns>
    private static ScanMatcher SpacedDirection(string direction, bool boundary) => (s, p) =>
    {
        var i = SkipSpaces(s, p);
        if (!Starts(s, i, direction))
        {
            return -1;
        }

        var end = i + direction.Length;
        return !boundary || end == s.Length || !ScanPatterns.IsJsWordChar(s[end]) ? end - p : -1;
    };

    /// <summary>
    ///     Gets the end of the run of JavaScript white space that starts at a position.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The position after the run.</returns>
    private static int SkipSpaces(string s, int p)
    {
        while (p < s.Length && ScanPatterns.IsJsWhitespace(s[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>
    ///     Matches <c>(\r?\n)+</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int NewlineRun(string s, int p)
    {
        var i = p;
        while (true)
        {
            if (i < s.Length && s[i] == '\n')
            {
                i++;
            }
            else if (i + 1 < s.Length && s[i] == '\r' && s[i + 1] == '\n')
            {
                i += 2;
            }
            else
            {
                return i > p ? i - p : -1;
            }
        }
    }

    /// <summary>
    ///     Matches the <c>NODE_STRING</c> rule: <c>([A-Za-z0-9!"#$%&amp;'*+.`?\\_/]|-(?=[^&gt;\-.]))+</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int NodeStringRun(string s, int p)
    {
        var i = p;
        while (i < s.Length)
        {
            var c = s[i];
            var idCharacter = char.IsAsciiLetterOrDigit(c)
                || c is '!' or '"' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '.' or '`' or '?' or '\\' or '_' or '/';
            var lonelyMinus = c == '-' && i + 1 < s.Length && s[i + 1] is not ('>' or '-' or '.');
            if (!idCharacter && !lonelyMinus)
            {
                break;
            }

            i++;
        }

        return i > p ? i - p : -1;
    }

    /// <summary>
    ///     Matches the <c>TEXT</c> rule of trapezoid labels: <c>\/(?!\])|\\(?!\])|[^\\\[\]\(\)\{\}\/]+</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int TrapTextRun(string s, int p)
    {
        if (p >= s.Length)
        {
            return -1;
        }

        // A slash or a backslash alone, unless it closes the label
        if (s[p] is '/' or '\\')
        {
            return p + 1 < s.Length && s[p + 1] == ']' ? -1 : 1;
        }

        var i = p;
        while (i < s.Length && s[i] is not ('\\' or '[' or ']' or '(' or ')' or '{' or '}' or '/'))
        {
            i++;
        }

        return i > p ? i - p : -1;
    }

    /// <summary>
    ///     Matches one character of the text of a <c>--</c> link: <c>[^-]|-(?!-)</c>.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>1, or -1.</returns>
    private static int EdgeTextCharacter(string s, int p)
    {
        if (p >= s.Length)
        {
            return -1;
        }

        return s[p] != '-' || p + 1 >= s.Length || s[p + 1] != '-' ? 1 : -1;
    }

    /// <summary>
    ///     Matches <c>\s*[xo&lt;]?--+[-xo&gt;]\s*</c> (or the same with <c>=</c>), a complete link, as the backtracking
    ///     expression does: the run of strokes may give its last stroke to the head.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="stroke">The stroke character: <c>-</c> or <c>=</c>.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchArrow(string s, int p, char stroke)
    {
        var i = SkipSpaces(s, p);
        if (i < s.Length && s[i] is 'x' or 'o' or '<')
        {
            i++;
        }

        var strokes = i;
        while (strokes < s.Length && s[strokes] == stroke)
        {
            strokes++;
        }

        // Two strokes and a head, or three strokes or more of which the last is the head
        var count = strokes - i;
        int end;
        if (count >= 2 && strokes < s.Length && s[strokes] is 'x' or 'o' or '>')
        {
            end = strokes + 1;
        }
        else if (count >= 3)
        {
            end = strokes;
        }
        else
        {
            return -1;
        }

        return SkipSpaces(s, end) - p;
    }

    /// <summary>
    ///     Matches <c>\s*[xo&lt;]?--\s*</c> (or the same with <c>==</c> or <c>-.</c>), the start of a link with text.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="start">The two characters of the start.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchStartLink(string s, int p, string start)
    {
        var i = SkipSpaces(s, p);
        if (Starts(s, i, start))
        {
            return SkipSpaces(s, i + 2) - p;
        }

        return i < s.Length && s[i] is 'x' or 'o' or '<' && Starts(s, i + 1, start) ? SkipSpaces(s, i + 3) - p : -1;
    }

    /// <summary>
    ///     Matches <c>\s*[xo&lt;]?-?\.+-[xo&gt;]?\s*</c>, a complete dotted link.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchDottedArrow(string s, int p)
    {
        // The optional head is taken when it is there; giving it back cannot help, since the strokes start with '-' or '.'
        var i = SkipSpaces(s, p);
        if (i < s.Length && s[i] is 'x' or 'o' or '<')
        {
            i++;
        }

        if (i < s.Length && s[i] == '-')
        {
            i++;
        }

        var dots = i;
        while (dots < s.Length && s[dots] == '.')
        {
            dots++;
        }

        if (dots == i || dots >= s.Length || s[dots] != '-')
        {
            return -1;
        }

        var end = dots + 1;
        if (end < s.Length && s[end] is 'x' or 'o' or '>')
        {
            end++;
        }

        return SkipSpaces(s, end) - p;
    }

    /// <summary>
    ///     Matches <c>\s*~~[\~]+\s*</c>, an invisible link.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private static int MatchInvisibleLink(string s, int p)
    {
        var i = SkipSpaces(s, p);
        var tildes = i;
        while (tildes < s.Length && s[tildes] == '~')
        {
            tildes++;
        }

        return tildes - i >= 3 ? SkipSpaces(s, tildes) - p : -1;
    }

    /// <summary>
    ///     Gets a value indicating whether a text appears at a position.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="text">The text.</param>
    /// <returns>Whether it does.</returns>
    private static bool Starts(string s, int p, string text) =>
        p >= 0 && p + text.Length <= s.Length && string.CompareOrdinal(s, p, text, 0, text.Length) == 0;

    /// <summary>
    ///     Gets a value indicating whether a character is one of the letters of the <c>UNICODE_TEXT</c> rule.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsUnicodeLetter(char c)
    {
        // Binary search of the sorted ranges
        var low = 0;
        var high = UnicodeLetters.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var (from, to) = UnicodeLetters[middle];
            if (c < from)
            {
                high = middle - 1;
            }
            else if (c > to)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Replaces every line feed and the white space after it by <c>&lt;br/&gt;</c>, as the shape-data string rule does
    ///     (<c>yytext.replace(/\n\s*/g, "&lt;br/&gt;")</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The replaced text.</returns>
    private static string ReplaceLineBreaks(string text)
    {
        if (!text.Contains('\n', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 8);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\n')
            {
                builder.Append("<br/>");
                i = SkipSpaces(text, i + 1);
            }
            else
            {
                builder.Append(text[i]);
                i++;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Matches the <c>LINK_ID</c> rule, <c>[^\s"]+@(?=[^\{"])</c>, as the backtracking expression does: the match
    ///     ends at the last <c>@</c> of the run of non-space characters that is followed by a character other than
    ///     <c>{</c> and <c>"</c>.
    /// </summary>
    /// <remarks>
    ///     The run and its last usable <c>@</c> are cached, so lexing a long run token by token stays linear.
    /// </remarks>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <returns>The length, or -1.</returns>
    private int MatchLinkId(string s, int p)
    {
        if (p >= s.Length || ScanPatterns.IsJsWhitespace(s[p]) || s[p] == '"')
        {
            return -1;
        }

        // Positions inside a run share its end and its last usable '@'
        if (p < _linkIdRunStart || p >= _linkIdRunEnd)
        {
            var end = p;
            while (end < s.Length && !ScanPatterns.IsJsWhitespace(s[end]) && s[end] != '"')
            {
                end++;
            }

            _linkIdRunStart = p;
            _linkIdRunEnd = end;
            _linkIdAt = -1;
            for (var k = end - 1; k > p; k--)
            {
                if (s[k] == '@' && k + 1 < s.Length && s[k + 1] is not ('{' or '"'))
                {
                    _linkIdAt = k;
                    break;
                }
            }
        }

        return _linkIdAt > p ? _linkIdAt + 1 - p : -1;
    }

    /// <summary>
    ///     Creates the matcher of a <c>.*direction\s+XX[^\n]*</c> rule: a line that contains a direction statement
    ///     anywhere after the position is one token.
    /// </summary>
    /// <param name="direction">The direction (<c>TB</c>, <c>BT</c>, <c>RL</c>, <c>LR</c> or <c>TD</c>).</param>
    /// <returns>The matcher.</returns>
    private ScanMatcher DirectionLine(string direction) => (s, p) =>
    {
        var occurrence = LastDirectionOn(s, p, direction);
        if (occurrence < p)
        {
            return -1;
        }

        // The match runs to the line feed after the direction word (the white space before it may span lines)
        var after = SkipSpaces(s, occurrence + DirectionWord.Length) + direction.Length;
        var end = s.IndexOf('\n', after);
        return (end < 0 ? s.Length : end) - p;
    };

    /// <summary>The word of a direction statement.</summary>
    private const string DirectionWord = "direction";

    /// <summary>
    ///     Finds the last <c>direction\s+XX</c> that starts on the line of a position, cached per line and direction so
    ///     that lexing a long line token by token stays linear.
    /// </summary>
    /// <param name="s">The input.</param>
    /// <param name="p">The position.</param>
    /// <param name="direction">The direction.</param>
    /// <returns>The position of the <c>direction</c> word, or -1.</returns>
    private int LastDirectionOn(string s, int p, string direction)
    {
        // The line of the position ends at the first line terminator ('.' does not match one)
        if (p < _directionLineStart || p >= _directionLineEnd)
        {
            var lineEnd = p;
            while (lineEnd < s.Length && !ScanPatterns.IsJsLineTerminator(s[lineEnd]))
            {
                lineEnd++;
            }

            _directionLineStart = p;
            _directionLineEnd = lineEnd;
            _directionCache.Clear();
        }

        if (!_directionCache.TryGetValue(direction, out var found))
        {
            // The last occurrence wins, as the greedy '.*' takes it
            found = -1;
            for (var k = _directionLineEnd - DirectionWord.Length; k >= _directionLineStart; k--)
            {
                if (!Starts(s, k, DirectionWord))
                {
                    continue;
                }

                var spaces = SkipSpaces(s, k + DirectionWord.Length);
                if (spaces > k + DirectionWord.Length && Starts(s, spaces, direction))
                {
                    found = k;
                    break;
                }
            }

            _directionCache[direction] = found;
        }

        return found;
    }
}

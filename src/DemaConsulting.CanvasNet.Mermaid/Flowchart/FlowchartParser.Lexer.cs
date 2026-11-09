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
            /* 108 */ new("unicodeText", Char(ScanPatterns.IsUnicodeLetter), Token(FlowchartTokens.UnicodeText)),
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

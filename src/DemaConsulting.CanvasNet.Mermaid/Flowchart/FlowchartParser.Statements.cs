// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <content>
///     The statements of the grammar: vertex statements with their nodes and shapes, <c>style</c>, <c>classDef</c>,
///     <c>class</c>, <c>linkStyle</c> and <c>click</c>.
/// </content>
internal sealed partial class FlowchartParser
{
    /// <summary>The tokens of a style (<c>styleComponent</c>; <c>UNIT</c> and <c>PCT</c> are never lexed).</summary>
    private static readonly string[] StyleTokens =
    [
        FlowchartTokens.Num, FlowchartTokens.NodeString, FlowchartTokens.Colon, FlowchartTokens.Space,
        FlowchartTokens.Brkt, FlowchartTokens.Style,
    ];

    /// <summary>The tokens of a word of <c>linkStyle ... interpolate</c> and <c>click</c> (<c>alphaNumToken</c>).</summary>
    private static readonly string[] AlphaNumTokens =
    [
        FlowchartTokens.Dir, FlowchartTokens.Amp, FlowchartTokens.Colon, FlowchartTokens.Down, FlowchartTokens.Num,
        FlowchartTokens.Comma, FlowchartTokens.NodeString, FlowchartTokens.Brkt, FlowchartTokens.Minus,
        FlowchartTokens.Mult, FlowchartTokens.UnicodeText,
    ];

    /// <summary>The tokens that start a link.</summary>
    private static readonly string[] LinkStarts = [FlowchartTokens.LinkId, FlowchartTokens.StartLink, FlowchartTokens.Link];

    /// <summary>
    ///     Parses a vertex statement (<c>vertexStatement</c>): a node, then links each followed by a node.
    /// </summary>
    /// <remarks>
    ///     Each link adds an edge from every node of the previous group to every node of the next one. The value is every
    ///     node of the statement, the last group first, as Mermaid builds it for subgraph membership.
    /// </remarks>
    /// <returns>The node ids of the statement.</returns>
    private List<string> ParseVertexStatement()
    {
        var group = ParseNodeGroup();
        var groups = new List<List<string>> { group };
        while (Is(LinkStarts))
        {
            var linkToken = Current;
            var link = ParseLink();
            var next = ParseNodeGroup();
            _model.AddLink(group, next, link, _cursor.Scanner.Locate(linkToken.Start));
            groups.Add(next);
            group = next;
        }

        // The last group first, each group in its own order
        return [.. Enumerable.Reverse(groups).SelectMany(g => g)];
    }

    /// <summary>
    ///     Parses a group of nodes chained with <c>&amp;</c> (<c>node</c>) and the shape data or spaces after it.
    /// </summary>
    /// <remarks>
    ///     Shape data after a node is applied once the node after the next <c>&amp;</c> is read, and shape data at the end
    ///     of the group before the group is used, which is when Mermaid's parser reduces those rules.
    /// </remarks>
    /// <returns>The node ids, in order.</returns>
    private List<string> ParseNodeGroup()
    {
        var nodes = new List<string> { ParseStyledVertex() };
        while (true)
        {
            if (Is(FlowchartTokens.ShapeData))
            {
                var (data, location) = ParseShapeData();
                if (!Is(FlowchartTokens.Space))
                {
                    // node shapeData: the end of the group
                    _model.AddVertex(nodes[^1], location, shapeData: data);
                    return nodes;
                }

                // node shapeData spaceList AMP spaceList styledVertex
                SkipSpaceList();
                Expect(FlowchartTokens.Amp);
                SkipSpaceList();
                var next = ParseStyledVertex();
                _model.AddVertex(nodes[^1], location, shapeData: data);
                nodes.Add(next);
                continue;
            }

            if (!Is(FlowchartTokens.Space))
            {
                return nodes;
            }

            // node spaceList: the end of the group, unless an '&' continues it
            SkipSpaceList();
            if (!Is(FlowchartTokens.Amp))
            {
                return nodes;
            }

            Advance();
            SkipSpaceList();
            nodes.Add(ParseStyledVertex());
        }
    }

    /// <summary>
    ///     Parses the shape data of an <c>@{ ... }</c> block: its <c>SHAPE_DATA</c> tokens, concatenated.
    /// </summary>
    /// <returns>The shape data and the location of the block.</returns>
    private (ShapeDataText Data, MermaidLocation Location) ParseShapeData()
    {
        var scanner = _cursor.Scanner;
        var location = scanner.Locate(Current.Start);
        var builder = new StringBuilder();
        var segments = new List<(int Offset, ScanToken Token)>();
        while (Is(FlowchartTokens.ShapeData))
        {
            var token = Advance();
            segments.Add((builder.Length, token));
            builder.Append(token.Text);
        }

        // A position of the text maps into its token, or to the token's start when the lexer changed the text
        MermaidLocation Locate(int position)
        {
            var index = segments.FindLastIndex(s => s.Offset <= position);
            if (index < 0)
            {
                return location;
            }

            var (offset, token) = segments[index];
            var exact = token.Text.Length == token.End - token.Start
                && string.CompareOrdinal(scanner.Input.Text, token.Start, token.Text, 0, token.Text.Length) == 0;
            return scanner.Locate(exact ? token.Start + Math.Min(position - offset, token.Text.Length) : token.Start);
        }

        return (new ShapeDataText(builder.ToString(), Locate), location);
    }

    /// <summary>
    ///     Parses a vertex with its optional class (<c>styledVertex</c>): <c>vertex</c> or <c>vertex:::class</c>.
    /// </summary>
    /// <returns>The vertex id.</returns>
    private string ParseStyledVertex()
    {
        var id = ParseVertex();
        if (Is(FlowchartTokens.StyleSeparator))
        {
            Advance();
            _model.SetClass(id, ParseIdString());
        }

        return id;
    }

    /// <summary>
    ///     Parses a vertex (<c>vertex</c>): an id, and a label in the brackets of a shape.
    /// </summary>
    /// <returns>The vertex id.</returns>
    private string ParseVertex()
    {
        var location = _cursor.Scanner.Locate(Current.Start);
        var id = ParseIdString();

        // The bracket pairs and the shape each gives
        (string Open, string Close, string Type)[] simple =
        [
            (FlowchartTokens.Sqs, FlowchartTokens.Sqe, "square"),
            (FlowchartTokens.DoubleCircleStart, FlowchartTokens.DoubleCircleEnd, "doublecircle"),
            (FlowchartTokens.EllipseStart, FlowchartTokens.EllipseEnd, "ellipse"),
            (FlowchartTokens.StadiumStart, FlowchartTokens.StadiumEnd, "stadium"),
            (FlowchartTokens.SubroutineStart, FlowchartTokens.SubroutineEnd, "subroutine"),
            (FlowchartTokens.CylinderStart, FlowchartTokens.CylinderEnd, "cylinder"),
            (FlowchartTokens.TagEnd, FlowchartTokens.Sqe, "odd"),
        ];
        foreach (var (open, close, type) in simple)
        {
            if (Is(open))
            {
                Advance();
                var text = ParseText();
                Expect(close);
                _model.AddVertex(id, location, text, type);
                return id;
            }
        }

        if (Is(FlowchartTokens.Ps))
        {
            // (text) is round, ((text)) is a circle
            Advance();
            var circle = Is(FlowchartTokens.Ps);
            if (circle)
            {
                Advance();
            }

            var text = ParseText();
            Expect(FlowchartTokens.Pe);
            if (circle)
            {
                Expect(FlowchartTokens.Pe);
            }

            _model.AddVertex(id, location, text, circle ? "circle" : "round");
            return id;
        }

        if (Is(FlowchartTokens.DiamondStart))
        {
            // {text} is a diamond, {{text}} a hexagon
            Advance();
            var hexagon = Is(FlowchartTokens.DiamondStart);
            if (hexagon)
            {
                Advance();
            }

            var text = ParseText();
            Expect(FlowchartTokens.DiamondStop);
            if (hexagon)
            {
                Expect(FlowchartTokens.DiamondStop);
            }

            _model.AddVertex(id, location, text, hexagon ? "hexagon" : "diamond");
            return id;
        }

        if (Is(FlowchartTokens.TrapStart, FlowchartTokens.InvTrapStart))
        {
            // [/text\] trapezoid, [/text/] lean right, [\text\] inverted trapezoid, [\text/] lean left
            var inverted = Advance().Kind == FlowchartTokens.InvTrapStart;
            var text = ParseText();
            var close = Expect(FlowchartTokens.TrapEnd, FlowchartTokens.InvTrapEnd).Kind;
            var type = (inverted, close == FlowchartTokens.TrapEnd) switch
            {
                (false, true) => "trapezoid",
                (false, false) => "lean_right",
                (true, false) => "inv_trapezoid",
                (true, true) => "lean_left",
            };
            _model.AddVertex(id, location, text, type);
            return id;
        }

        if (Is(FlowchartTokens.VertexWithPropsStart))
        {
            // id[|field:value|text]
            Advance();
            var field = Expect(FlowchartTokens.NodeString).Text;
            Expect(FlowchartTokens.Colon);
            var value = Expect(FlowchartTokens.NodeString).Text;
            Expect(FlowchartTokens.Pipe);
            var text = ParseText();
            Expect(FlowchartTokens.Sqe);
            _model.AddVertex(id, location, text, "rect", props: new KeyValuePair<string, string>(field, value));
            return id;
        }

        _model.AddVertex(id, location);
        return id;
    }

    /// <summary>
    ///     Parses an id (<c>idString</c>): one or more id tokens, concatenated.
    /// </summary>
    /// <returns>The id.</returns>
    private string ParseIdString()
    {
        if (!Is(IdTokens))
        {
            throw Fail("write a node id: letters, digits and the characters -_.!?#$%&*+/");
        }

        var builder = new StringBuilder(Advance().Text);
        while (Is(IdTokens))
        {
            builder.Append(Advance().Text);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Parses a word of <c>interpolate</c> or <c>click</c> (<c>alphaNum</c>): one or more word tokens, concatenated.
    /// </summary>
    /// <returns>The word.</returns>
    private string ParseAlphaNum()
    {
        if (!Is(AlphaNumTokens))
        {
            throw Fail("write a name of letters and digits");
        }

        var builder = new StringBuilder(Advance().Text);
        while (Is(AlphaNumTokens))
        {
            builder.Append(Advance().Text);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Parses styles (<c>stylesOpt</c>): styles separated by commas, each a run of style tokens.
    /// </summary>
    /// <returns>The styles; a new list, which <c>linkStyle</c> shares between its edges.</returns>
    private List<string> ParseStyles()
    {
        var styles = new List<string> { ParseStyle() };
        while (Is(FlowchartTokens.Comma))
        {
            Advance();
            styles.Add(ParseStyle());
        }

        return styles;
    }

    /// <summary>
    ///     Parses one style (<c>style</c>): one or more style tokens, concatenated.
    /// </summary>
    /// <returns>The style.</returns>
    private string ParseStyle()
    {
        if (!Is(StyleTokens))
        {
            throw Fail("write a style such as fill:#f9f or stroke-width:2px");
        }

        var builder = new StringBuilder(Advance().Text);
        while (Is(StyleTokens))
        {
            builder.Append(Advance().Text);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Parses <c>style id styles</c>.
    /// </summary>
    /// <param name="location">The location of the statement.</param>
    private void ParseStyleStatement(MermaidLocation location)
    {
        Advance();
        Expect(FlowchartTokens.Space);
        var id = ParseIdString();
        Expect(FlowchartTokens.Space);
        var styles = ParseStyles();
        ExpectSeparator();
        _model.AddVertex(id, location, style: styles);
    }

    /// <summary>
    ///     Parses <c>classDef names styles</c>.
    /// </summary>
    private void ParseClassDefStatement()
    {
        Advance();
        Expect(FlowchartTokens.Space);
        var ids = ParseIdString();
        Expect(FlowchartTokens.Space);
        var styles = ParseStyles();
        ExpectSeparator();
        _model.AddClass(ids, styles);
    }

    /// <summary>
    ///     Parses <c>class ids name</c>.
    /// </summary>
    private void ParseClassStatement()
    {
        Advance();
        Expect(FlowchartTokens.Space);
        var ids = ParseIdString();
        Expect(FlowchartTokens.Space);
        var className = ParseIdString();
        ExpectSeparator();
        _model.SetClass(ids, className);
    }

    /// <summary>
    ///     Parses <c>linkStyle default|n,m [interpolate curve] [styles]</c>.
    /// </summary>
    /// <param name="location">The location of the statement.</param>
    private void ParseLinkStyleStatement(MermaidLocation location)
    {
        Advance();
        Expect(FlowchartTokens.Space);

        // The edges: default, or indexes separated by commas
        var positions = new List<string>();
        if (Is(FlowchartTokens.Default))
        {
            positions.Add(Advance().Text);
        }
        else if (Is(FlowchartTokens.Num))
        {
            positions.Add(Advance().Text);
            while (Is(FlowchartTokens.Comma))
            {
                Advance();
                positions.Add(Expect(FlowchartTokens.Num).Text);
            }
        }
        else
        {
            throw Fail("write 'default' or the edge indexes, for example linkStyle 0,2 stroke:red");
        }

        Expect(FlowchartTokens.Space);
        if (!Is(FlowchartTokens.Interpolate))
        {
            var styles = ParseStyles();
            ExpectSeparator();
            _model.UpdateLink(positions, styles, location);
            return;
        }

        // interpolate curve, optionally followed by styles
        Advance();
        Expect(FlowchartTokens.Space);
        var curve = ParseAlphaNum();
        if (Is(FlowchartTokens.Space))
        {
            Advance();
            var styles = ParseStyles();
            ExpectSeparator();
            _model.UpdateLinkInterpolate(positions, curve, location);
            _model.UpdateLink(positions, styles, location);
            return;
        }

        ExpectSeparator();
        _model.UpdateLinkInterpolate(positions, curve, location);
    }

    /// <summary>
    ///     Parses a <c>click</c> statement in any of its forms (a callback, <c>call</c>, <c>href</c> or a link, with a
    ///     tooltip and a target); only the <c>clickable</c> class it adds is kept.
    /// </summary>
    private void ParseClickStatement()
    {
        var ids = Advance().Text;
        if (Is(FlowchartTokens.CallbackName))
        {
            // call name(args) "tooltip"
            Advance();
            if (Is(FlowchartTokens.CallbackArgs))
            {
                Advance();
            }

            ParseOptionalTooltip();
        }
        else if (Is(FlowchartTokens.Href, FlowchartTokens.Str))
        {
            // [href] "url" ["tooltip"] [target]
            if (Current.Kind == FlowchartTokens.Href)
            {
                Advance();
                Expect(FlowchartTokens.Str);
            }
            else
            {
                Advance();
            }

            if (Is(FlowchartTokens.Space))
            {
                Advance();
                if (Is(FlowchartTokens.Str))
                {
                    Advance();
                    if (Is(FlowchartTokens.Space))
                    {
                        Advance();
                        Expect(FlowchartTokens.LinkTarget);
                    }
                }
                else
                {
                    Expect(FlowchartTokens.LinkTarget);
                }
            }
        }
        else if (Is(AlphaNumTokens))
        {
            // callback "tooltip"
            ParseAlphaNum();
            ParseOptionalTooltip();
        }
        else
        {
            throw Fail("write a callback name, call name(), href \"url\" or \"url\" after the node id");
        }

        ExpectSeparator();
        _model.SetClickable(ids);
    }

    /// <summary>
    ///     Parses an optional tooltip: a space and a quoted string.
    /// </summary>
    private void ParseOptionalTooltip()
    {
        if (Is(FlowchartTokens.Space))
        {
            Advance();
            Expect(FlowchartTokens.Str);
        }
    }
}

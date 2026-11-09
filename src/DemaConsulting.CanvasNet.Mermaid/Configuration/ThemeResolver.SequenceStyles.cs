// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     The resolved stroke of a line: color, width and dash pattern.
/// </summary>
/// <param name="Color">The stroke color, or null for none.</param>
/// <param name="Width">The stroke width in pixels.</param>
/// <param name="Dash">The dash lengths in pixels, empty for a solid line.</param>
internal sealed record StrokePaint(Rgba32? Color, double Width, IReadOnlyList<double> Dash);

/// <summary>
///     A drop shadow: the CSS filter <c>drop-shadow(offset-x offset-y blur color)</c>.
/// </summary>
/// <param name="OffsetX">The horizontal offset in pixels.</param>
/// <param name="OffsetY">The vertical offset in pixels.</param>
/// <param name="Blur">The standard deviation of the blur in pixels (the third length of <c>drop-shadow()</c>); not negative.</param>
/// <param name="Color">The shadow color.</param>
internal sealed record ShadowPaint(double OffsetX, double OffsetY, double Blur, Rgba32 Color);

/// <summary>
///     The style sheet Mermaid writes for a sequence diagram (its <c>sequence/styles.js</c> after the global rules of
///     <c>styles.ts</c>), built from the theme variables of a configuration, with the rules of <c>themeCSS</c> applied
///     after it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Cascade.</b> Mermaid scopes every rule to the diagram's SVG, and a CSS rule beats a presentation attribute,
///         so the paint of an element is its rules' declarations where they have one and its attributes elsewhere. The
///         resolvers take the attributes Mermaid's <c>svgDraw</c> gives an element and the selectors that match it, from
///         the least to the most specific, and apply the rules in that order: <see cref="Shape"/>, <see cref="Line"/>,
///         <see cref="TextColor"/> and <see cref="Shadow"/>.
///     </para>
///     <para>
///         <b>Facts the typed resolvers encode.</b> The rule <c>.actor-man circle, line</c> matches every <c>line</c> of
///         the diagram, so lifelines and block lines are 2 pixels wide (<see cref="Lifeline"/>, <see cref="LoopLine"/>),
///         as is a message drawn as a <c>line</c> unless its message rule says otherwise (<see cref="MessageLine"/>). The
///         rule <c>g rect.rect</c> gives a box (a rectangle in a group) the theme's <c>dropShadow</c> and its
///         <c>nodeBorder</c> stroke (<see cref="Box"/>, <see cref="BoxShadow"/>); the <c>rect</c> block backgrounds,
///         which are not in a group, get neither. The rules for the <c>neo</c> look are kept for <c>themeCSS</c> but
///         never apply, because that look is drawn classic.
///     </para>
///     <para>
///         An instance is immutable.
///     </para>
/// </remarks>
internal sealed class SequenceStyles : DiagramStyles
{
    /// <summary>
    ///     Initializes the styles from their rules.
    /// </summary>
    /// <param name="rules">The rules in cascade order.</param>
    private SequenceStyles(IReadOnlyList<StyleRule> rules)
        : base(rules)
    {
    }

    /// <summary>Gets the selectors of the sequence diagram's style sheet, which <c>themeCSS</c> may use.</summary>
    public static FrozenSet<string> KnownSelectors { get; } = BuildRules(new Dictionary<string, string?>(), [])
        .Select(rule => rule.Selector)
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets the stroke of lifelines (<c>line.actor-line</c>, drawn with the attributes <c>#999</c> and 0.5 pixels).</summary>
    public StrokePaint Lifeline => Line(new StrokePaint(Css("#999"), 0.5, []), "line", ".actor-line");

    /// <summary>Gets the stroke of block lines (<c>line.loopLine</c>).</summary>
    public StrokePaint LoopLine => Line(new StrokePaint(null, 1, []), "line", ".loopLine");

    /// <summary>Gets the paint of block label boxes (<c>polygon.labelBox</c>, which has no paint attributes).</summary>
    public ShapePaint LabelBox => Shape(new ShapePaint(Css("#000"), null, 1), ".labelBox");

    /// <summary>Gets the shadow of a box (<c>g rect.rect</c>: the theme's <c>dropShadow</c>), or null for none.</summary>
    public ShadowPaint? BoxShadow => Shadow("g rect.rect");

    /// <summary>Gets the shadow of a forced actor menu panel (<c>.actorPopupMenuPanel</c>), or null for none.</summary>
    public ShadowPaint? MenuPanelShadow => Shadow(".actorPopupMenuPanel");

    /// <summary>Gets the color of arrowheads (<c>[id$="-arrowhead"] path</c>), or null for none.</summary>
    public Rgba32? ArrowheadColor => Resolve("[id$=\"-arrowhead\"] path").Paint("fill");

    /// <summary>Gets the color of cross heads (<c>[id$="-crosshead"] path</c>), or null for none.</summary>
    public Rgba32? CrossheadColor => Resolve("[id$=\"-crosshead\"] path").Paint("fill");

    /// <summary>Gets the color of the sequence number circle (<c>[id$="-sequencenumber"]</c>), or null for none.</summary>
    public Rgba32? SequenceNumberMarkerColor => Resolve("[id$=\"-sequencenumber\"]").Paint("fill");

    /// <summary>
    ///     Builds the styles of a configuration: the sequence diagram's rules from its theme variables, then the known
    ///     rules of its <c>themeCSS</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="sink">The report sink for <c>themeCSS</c> problems, or null.</param>
    /// <returns>The styles.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    /// <exception cref="MermaidUnsupportedFeatureException">Thrown in strict mode at the first unsupported <c>themeCSS</c> rule.</exception>
    public static SequenceStyles Create(MermaidConfig config, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new SequenceStyles(BuildRules(VariablesOf(config), UserRules(config, KnownSelectors, sink)));
    }

    /// <summary>
    ///     Parses the value of a CSS <c>filter</c> that this version draws: <c>none</c>, or one <c>drop-shadow()</c>
    ///     with an optional color first or last and two or three lengths (a missing blur is 0, a missing color is the
    ///     current color, black).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="shadow">The shadow, or null for <c>none</c>.</param>
    /// <returns>Whether the value is <c>none</c> or a valid drop shadow; any other filter is false.</returns>
    public static bool TryParseFilter(string value, out ShadowPaint? shadow)
    {
        ArgumentNullException.ThrowIfNull(value);
        shadow = null;
        var text = value.Trim();
        if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // One drop-shadow() function and nothing after it
        const string Function = "drop-shadow(";
        if (!text.StartsWith(Function, StringComparison.OrdinalIgnoreCase) || ClosingParenthesis(text, Function.Length - 1) != text.Length - 1)
        {
            return false;
        }

        // The arguments: an optional color first or last, and two or three lengths
        var tokens = SplitTokens(text[Function.Length..^1]);
        Rgba32? color = null;
        if (tokens.Count > 0 && !TryParseSignedLength(tokens[0], out _) && CssColor.TryParse(tokens[0], out var first))
        {
            color = first;
            tokens.RemoveAt(0);
        }
        else if (tokens.Count > 0 && !TryParseSignedLength(tokens[^1], out _) && CssColor.TryParse(tokens[^1], out var last))
        {
            color = last;
            tokens.RemoveAt(tokens.Count - 1);
        }

        var lengths = new List<double>();
        foreach (var token in tokens)
        {
            if (!TryParseSignedLength(token, out var length))
            {
                return false;
            }

            lengths.Add(length);
        }

        if (lengths.Count is < 2 or > 3 || (lengths.Count == 3 && lengths[2] < 0))
        {
            return false;
        }

        shadow = new ShadowPaint(lengths[0], lengths[1], lengths.Count == 3 ? lengths[2] : 0, color ?? new Rgba32(0, 0, 0, 255));
        return true;
    }

    /// <summary>
    ///     Resolves the paint of a shape: the declarations of the rules that match it, over its attributes.
    /// </summary>
    /// <param name="attributes">The paint of the element's attributes (an SVG default where it has none).</param>
    /// <param name="selectors">The selectors that match the element, from the least to the most specific.</param>
    /// <returns>The paint.</returns>
    public ShapePaint Shape(ShapePaint attributes, params string[] selectors)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        var set = Cascade(selectors);
        return new ShapePaint(
            set["fill"] is null ? attributes.Fill : set.Paint("fill"),
            set["stroke"] is null ? attributes.Stroke : set.Paint("stroke"),
            set.Length("stroke-width", attributes.StrokeWidth));
    }

    /// <summary>
    ///     Resolves the stroke of a line or path: the declarations of the rules that match it, over its attributes.
    /// </summary>
    /// <param name="attributes">The stroke of the element's attributes (an SVG default where it has none).</param>
    /// <param name="selectors">The selectors that match the element, from the least to the most specific.</param>
    /// <returns>The stroke.</returns>
    public StrokePaint Line(StrokePaint attributes, params string[] selectors)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        var set = Cascade(selectors);
        var dash = set["stroke-dasharray"] is { } pattern && StyleDeclarations.TryParseDashArray(pattern, out var parsed) ? parsed : attributes.Dash;
        return new StrokePaint(
            set["stroke"] is null ? attributes.Color : set.Paint("stroke"),
            set.Length("stroke-width", attributes.Width),
            dash);
    }

    /// <summary>
    ///     Resolves the color of a text: the fill of the rules that match it, else its attribute, else the fill it
    ///     inherits from the root rule.
    /// </summary>
    /// <param name="attribute">The element's <c>fill</c> attribute, or null when it has none.</param>
    /// <param name="selectors">The selectors that match the element, from the least to the most specific.</param>
    /// <returns>The color, or null when a rule says <c>none</c>.</returns>
    public Rgba32? TextColor(Rgba32? attribute, params string[] selectors)
    {
        var set = Cascade(selectors);
        if (set["fill"] is not null)
        {
            return set.Paint("fill");
        }

        return attribute ?? Resolve(Root).Paint("fill") ?? new Rgba32(0, 0, 0, 255);
    }

    /// <summary>
    ///     Resolves the drop shadow of an element from the <c>filter</c> of the rules that match it.
    /// </summary>
    /// <param name="selectors">The selectors that match the element, from the least to the most specific.</param>
    /// <returns>The shadow, or null for none.</returns>
    public ShadowPaint? Shadow(params string[] selectors) =>
        Cascade(selectors)["filter"] is { } filter && TryParseFilter(filter, out var shadow) ? shadow : null;

    /// <summary>
    ///     Resolves the paint of a box (<c>g rect.rect</c>: its <c>nodeBorder</c> stroke over the box's attributes).
    /// </summary>
    /// <param name="attributes">The paint of the box rectangle's attributes (its color, or none).</param>
    /// <returns>The paint.</returns>
    public ShapePaint Box(ShapePaint attributes) => Shape(attributes, "g rect.rect");

    /// <summary>
    ///     Resolves the stroke of a message: <c>.messageLine0</c> (solid) or <c>.messageLine1</c> (dotted), over the
    ///     <c>line</c> rule when the message is a <c>line</c> element (a straight message) rather than a path.
    /// </summary>
    /// <param name="dotted">Whether the message is dotted.</param>
    /// <param name="isLineElement">Whether the message is drawn as a <c>line</c> element.</param>
    /// <returns>The stroke (the attributes are black, 1.5 pixels).</returns>
    public StrokePaint MessageLine(bool dotted, bool isLineElement)
    {
        var attributes = new StrokePaint(Css("black"), 1.5, []);
        var selector = dotted ? ".messageLine1" : ".messageLine0";
        return isLineElement ? Line(attributes, "line", selector) : Line(attributes, selector);
    }

    /// <summary>
    ///     Builds the rules: the global rules of Mermaid's <c>getStyles</c>, the sequence diagram's <c>styles.js</c> in its
    ///     order, then the user's.
    /// </summary>
    /// <param name="v">The theme variables as text.</param>
    /// <param name="user">The user rules.</param>
    /// <returns>The rules.</returns>
    private static List<StyleRule> BuildRules(IReadOnlyDictionary<string, string?> v, IReadOnlyList<StyleRule> user)
    {
        var builder = new RuleBuilder(v);
        string? Var(string name) => builder.Var(name);
        void Add(string selectors, params (string Property, string? Value)[] declarations) => builder.Add(selectors, declarations);
        var dropShadow = Var("dropShadow") ?? "none";
        var noteFontWeight = Var("noteFontWeight") is { Length: > 0 } weight ? weight : null;

        // Global rules (styles.ts getStyles) before the diagram styles
        builder.AddGlobalRules();

        // The sequence diagram's styles.js; the label box filter is that of the classic look
        Add(".actor", ("stroke", Var("actorBorder")), ("fill", Var("actorBkg")), ("stroke-width", Var("strokeWidth") ?? "1"));
        Add("rect.actor.outer-path[data-look=\"neo\"]", ("filter", dropShadow));
        Add("rect.note[data-look=\"neo\"]", ("stroke", Var("noteBorderColor")), ("fill", Var("noteBkgColor")), ("filter", dropShadow));
        Add("text.actor > tspan", ("fill", Var("actorTextColor")), ("stroke", "none"));
        Add(".actor-line", ("stroke", Var("actorLineColor")));
        Add(".innerArc", ("stroke-width", "1.5"), ("stroke-dasharray", "none"));
        Add(".messageLine0", ("stroke-width", "1.5"), ("stroke-dasharray", "none"), ("stroke", Var("signalColor")));
        Add(".messageLine1", ("stroke-width", "1.5"), ("stroke-dasharray", "2, 2"), ("stroke", Var("signalColor")));
        Add("[id$=\"-arrowhead\"] path", ("fill", Var("signalColor")), ("stroke", Var("signalColor")));
        Add(".sequenceNumber", ("fill", Var("sequenceNumberColor")));
        Add("[id$=\"-sequencenumber\"]", ("fill", Var("signalColor")));
        Add("[id$=\"-crosshead\"] path", ("fill", Var("signalColor")), ("stroke", Var("signalColor")));
        Add(".messageText", ("fill", Var("signalTextColor")), ("stroke", "none"));
        Add(".labelBox", ("stroke", Var("labelBoxBorderColor")), ("fill", Var("labelBoxBkgColor")), ("filter", "none"));
        Add(".labelText,.labelText > tspan", ("fill", Var("labelTextColor")), ("stroke", "none"));
        Add(".loopText,.loopText > tspan", ("fill", Var("loopTextColor")), ("stroke", "none"));
        Add(".sectionTitle,.sectionTitle > tspan", ("fill", Var("loopTextColor")), ("stroke", "none"));
        Add(
            ".loopLine",
            ("stroke-width", "2px"),
            ("stroke-dasharray", "2, 2"),
            ("stroke", Var("labelBoxBorderColor")),
            ("fill", Var("labelBoxBorderColor")));
        Add(".note", ("stroke", Var("noteBorderColor")), ("fill", Var("noteBkgColor")));
        Add(".noteText,.noteText > tspan", ("fill", Var("noteTextColor")), ("stroke", "none"), ("font-weight", noteFontWeight));
        Add(".activation0", ("fill", Var("activationBkgColor")), ("stroke", Var("activationBorderColor")));
        Add(".activation1", ("fill", Var("activationBkgColor")), ("stroke", Var("activationBorderColor")));
        Add(".activation2", ("fill", Var("activationBkgColor")), ("stroke", Var("activationBorderColor")));
        Add(".actorPopupMenuPanel", ("fill", Var("actorBkg")), ("filter", "drop-shadow(3px 5px 2px rgb(0 0 0 / 0.4))"));
        Add(".actor-man circle,line", ("fill", Var("actorBkg")), ("stroke-width", "2px"));
        Add("g rect.rect", ("filter", dropShadow), ("stroke", Var("nodeBorder")));

        // The user styles come last
        builder.Rules.AddRange(user);
        return builder.Rules;
    }

    /// <summary>
    ///     Parses a CSS color constant of Mermaid's drawing code.
    /// </summary>
    /// <param name="text">The color.</param>
    /// <returns>The color.</returns>
    private static Rgba32 Css(string text) => CssColor.Parse(text);

    /// <summary>
    ///     Finds the parenthesis that closes an opening one.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="open">The index of the opening parenthesis.</param>
    /// <returns>The index of the closing parenthesis, or -1 when it is not closed.</returns>
    private static int ClosingParenthesis(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            depth += text[i] switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            if (depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Splits the arguments of a CSS function at white space outside parentheses.
    /// </summary>
    /// <param name="text">The arguments.</param>
    /// <returns>The tokens.</returns>
    private static List<string> SplitTokens(string text)
    {
        var tokens = new List<string>();
        var start = -1;
        var depth = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            var end = i == text.Length || (depth == 0 && char.IsWhiteSpace(text[i]));
            if (end)
            {
                if (start >= 0)
                {
                    tokens.Add(text[start..i]);
                    start = -1;
                }

                continue;
            }

            depth += text[i] switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            if (start < 0)
            {
                start = i;
            }
        }

        return tokens;
    }

    /// <summary>
    ///     Parses a CSS length that may be negative: a number with a unit, or zero without one.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="length">The length in pixels.</param>
    /// <returns>Whether the token is a length.</returns>
    private static bool TryParseSignedLength(string token, out double length)
    {
        var negative = token.StartsWith('-');
        var magnitude = negative || token.StartsWith('+') ? token[1..] : token;
        length = 0;
        if (magnitude.Length == 0 || !(char.IsDigit(magnitude[0]) || magnitude[0] == '.') ||
            !StyleDeclarations.TryParseLength(magnitude, 16, out var value))
        {
            return false;
        }

        // A number without a unit is only a length when it is zero
        if ((char.IsDigit(magnitude[^1]) || magnitude[^1] == '.') && value != 0)
        {
            return false;
        }

        length = negative ? -value : value;
        return true;
    }

    /// <summary>
    ///     Cascades the declarations of selectors in order.
    /// </summary>
    /// <param name="selectors">The selectors, from the least to the most specific.</param>
    /// <returns>The declarations.</returns>
    private StyleDeclarationSet Cascade(string[] selectors)
    {
        var set = StyleDeclarationSet.Empty;
        foreach (var selector in selectors)
        {
            set = set.Cascade(Resolve(selector));
        }

        return set;
    }
}

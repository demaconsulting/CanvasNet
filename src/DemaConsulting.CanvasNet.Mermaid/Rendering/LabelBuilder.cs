// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;
using DemaConsulting.CanvasNet.Rendering;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     The text style a label starts from: the CSS family list, the size, the weight, the style and the color that the
///     style sheet and the element's own label styles give it.
/// </summary>
/// <param name="Family">The CSS <c>font-family</c> list.</param>
/// <param name="Size">The font size in pixels.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Italic">Whether the text is italic.</param>
/// <param name="Color">The text color.</param>
internal sealed record LabelFont(string Family, double Size, bool Bold, bool Italic, Rgba32 Color);

/// <summary>
///     What to lay out: the label text and the options of Mermaid's <c>createText</c> (and of <c>labelHelper</c>, which
///     calls it for a node).
/// </summary>
/// <param name="Text">The label text as the diagram model holds it.</param>
/// <param name="Font">The style the text starts from.</param>
internal sealed record LabelRequest(string Text, LabelFont Font)
{
    /// <summary>Gets a value indicating whether the text is a markdown string (<c>labelType</c> <c>markdown</c>).</summary>
    public bool Markdown { get; init; }

    /// <summary>Gets a value indicating whether HTML labels are used (the effective <c>htmlLabels</c>).</summary>
    public bool HtmlLabels { get; init; } = true;

    /// <summary>Gets the width at which the text wraps (<c>wrappingWidth</c>); positive infinity never wraps.</summary>
    public double Width { get; init; } = 200;

    /// <summary>Gets a value indicating whether the label is a node label; an SVG edge label is centered on its anchor.</summary>
    public bool IsNode { get; init; } = true;

    /// <summary>Gets a value indicating whether the label has a background (the label of an icon or image node).</summary>
    public bool AddBackground { get; init; }

    /// <summary>Gets the CSS padding of the HTML paragraph (2 for the label of an icon or image node, else 0).</summary>
    public double ParagraphPadding { get; init; }

    /// <summary>Gets a value indicating whether markdown text wraps at spaces (<c>markdownAutoWrap</c>).</summary>
    public bool MarkdownAutoWrap { get; init; } = true;

    /// <summary>
    ///     Gets a value indicating whether the text first gets <c>labelHelper</c>'s treatment (entity placeholders decoded,
    ///     then sanitized), as for node labels.
    /// </summary>
    public bool Prepare { get; init; } = true;

    /// <summary>Gets the font size of the configuration (<c>fontSize</c>), which sizes a picture alone in a label.</summary>
    public double ConfigFontSize { get; init; } = 16;

    /// <summary>
    ///     Gets the font size the label inherits from the diagram's root style (the theme's <c>fontSize</c>), with which
    ///     an SVG label's lines are wrapped before the label's own style applies.
    /// </summary>
    public double RootFontSize { get; init; } = 16;

    /// <summary>
    ///     Gets a value indicating whether the lines of an SVG label are centered on x 0 (node and edge labels) rather than
    ///     starting there.
    /// </summary>
    public bool CenterSvgText { get; init; } = true;

    /// <summary>Gets the location of the element the label belongs to, for reports.</summary>
    public MermaidLocation Location { get; init; }
}

/// <summary>
///     A run of one line of a measured label: text with one font and color.
/// </summary>
/// <param name="Text">The text, as drawn.</param>
/// <param name="Font">The font.</param>
/// <param name="Color">The color.</param>
/// <param name="Width">The advance width.</param>
internal sealed record LabelRun(string Text, SceneFont Font, Rgba32 Color, double Width);

/// <summary>
///     One line of a measured label, in the coordinates of the label (before the label's translation).
/// </summary>
/// <param name="X">The left end of the line.</param>
/// <param name="Baseline">The y of the baseline.</param>
/// <param name="Width">The advance width of the line.</param>
/// <param name="Box">The box of the line (its line box for an HTML label, its glyph box for an SVG label).</param>
/// <param name="Runs">The runs in order.</param>
internal sealed record LabelLine(double X, double Baseline, double Width, SceneRect Box, IReadOnlyList<LabelRun> Runs);

/// <summary>
///     A picture of a label (an <c>&lt;img&gt;</c> of an HTML label), in the coordinates of the label.
/// </summary>
/// <param name="Box">The box the picture fills.</param>
/// <param name="Href">The link of the picture.</param>
internal sealed record LabelImage(SceneRect Box, string Href);

/// <summary>
///     A measured label: what Mermaid's <c>createText</c> draws and the box its <c>labelHelper</c> measures
///     (<c>getBoundingClientRect</c> of the HTML label, <c>getBBox</c> of the SVG text).
/// </summary>
/// <remarks>
///     The coordinates are those of the label's own group before the shape translates it: an HTML label's box starts at
///     the origin; an SVG label's text is anchored at x 0 with its first baseline one em below the origin, so its box has
///     a negative x and a small positive y. <see cref="OffsetX"/> and <see cref="OffsetY"/> are Mermaid's
///     <c>bbox.x - (bbox.left ?? 0)</c> and <c>bbox.y - (bbox.top ?? 0)</c>, which the shapes use to place the label.
/// </remarks>
/// <param name="IsHtml">Whether the label is an HTML label.</param>
/// <param name="Box">The measured box.</param>
/// <param name="Lines">The lines.</param>
/// <param name="Images">The pictures.</param>
internal sealed record LabelLayout(bool IsHtml, SceneRect Box, IReadOnlyList<LabelLine> Lines, IReadOnlyList<LabelImage> Images)
{
    /// <summary>Gets the width of the box.</summary>
    public double Width => Box.Width;

    /// <summary>Gets the height of the box.</summary>
    public double Height => Box.Height;

    /// <summary>Gets Mermaid's <c>bbox.x - (bbox.left ?? 0)</c>: zero for an HTML label, the box's x for an SVG label.</summary>
    public double OffsetX => IsHtml ? 0 : Box.X;

    /// <summary>Gets Mermaid's <c>bbox.y - (bbox.top ?? 0)</c>: zero for an HTML label, the box's y for an SVG label.</summary>
    public double OffsetY => IsHtml ? 0 : Box.Y;

    /// <summary>
    ///     Gets a label of a given box with nothing to draw (a stand-in for a measured label, and the empty label).
    /// </summary>
    /// <param name="isHtml">Whether the label is an HTML label.</param>
    /// <param name="box">The box.</param>
    /// <returns>The label.</returns>
    public static LabelLayout Of(bool isHtml, SceneRect box) => new(isHtml, box, [], []);

    /// <summary>
    ///     Gets the scene items that draw the label when its group is translated by an offset.
    /// </summary>
    /// <param name="dx">The horizontal translation of the label's group.</param>
    /// <param name="dy">The vertical translation of the label's group.</param>
    /// <param name="role">The role of the text items.</param>
    /// <param name="id">The id of the element the label belongs to, or null.</param>
    /// <param name="location">The location of the element, for reports.</param>
    /// <returns>The text lines and pictures, in drawing order.</returns>
    public IReadOnlyList<SceneItem> ToScene(double dx, double dy, string role, string? id, MermaidLocation location)
    {
        var items = new List<SceneItem>();
        foreach (var line in Lines)
        {
            if (line.Runs.Count == 0)
            {
                continue;
            }

            var runs = line.Runs.Select(run => new SceneTextRun(run.Text, run.Font, run.Color)).ToList();
            items.Add(new SceneText(line.X + dx, line.Baseline + dy, SceneTextAnchor.Start, runs, line.Box.Offset(dx, dy))
            {
                Role = role,
                Id = id,
                Location = location,
            });
        }

        foreach (var image in Images)
        {
            items.Add(new SceneImage(image.Box.Offset(dx, dy), image.Href) { Role = role + "-image", Id = id, Location = location });
        }

        return items;
    }
}

/// <summary>
///     Lays out and measures label text as Mermaid's <c>createText</c> does: plain and markdown strings, HTML labels
///     (a <c>&lt;foreignObject&gt;</c> whose box the browser measures) and SVG labels (<c>&lt;tspan&gt;</c> lines).
/// </summary>
/// <remarks>
///     <para>
///         <b>Text.</b> A node's text first gets <c>labelHelper</c>'s treatment: Mermaid's entity placeholders are
///         decoded and the text is sanitized as Mermaid's <c>sanitizeText</c> does. A markdown string becomes HTML or
///         lines as <c>handle-markdown-text.ts</c> makes them (<c>**strong**</c>, <c>*em*</c>, escapes, line breaks; any
///         other markdown is kept as written). <c>fa:fa-...</c> icon names are removed (the model reports them once).
///     </para>
///     <para>
///         <b>HTML labels.</b> The HTML is laid out with the rules of Mermaid's <c>&lt;div&gt;</c>: line boxes of
///         1.5 times the font size, centered lines, white space collapsed and no wrapping; when the text is at least as
///         wide as the wrapping width, it is laid out again in a box of exactly that width with the spaces kept and lines
///         broken after spaces and hyphens, as the browser does after Mermaid's <c>break-spaces</c> retry. Only the
///         elements <c>b</c>, <c>strong</c>, <c>i</c>, <c>em</c>, <c>br</c>, <c>img</c>, <c>span</c> (with a style) and
///         <c>p</c> are understood; any other element is dropped with its text kept and reported as
///         <see cref="MermaidDiagnosticCodes.HtmlTagIgnored"/>.
///     </para>
///     <para>
///         <b>SVG labels.</b> The lines are those of Mermaid's <c>createFormattedText</c>: words joined by single spaces,
///         a line wider than the wrapping width split at words (and a word wider than it split at characters), the lines
///         1.1 em apart. The box is that of the browser's <c>getBBox</c>: the advance of the widest line with the glyph
///         outlines rounded out to whole pixels, from the rounded ascent above the first baseline to the rounded descent
///         below the last.
///     </para>
///     <para>
///         <b>Fonts.</b> Text is measured with the faces of the call's <see cref="FontResolver"/> (the bundled fonts),
///         which the painter draws with too. A character no bundled font has is reported as
///         <see cref="MermaidDiagnosticCodes.TextMissingGlyph"/>, once per label.
///     </para>
///     <para>
///         An instance belongs to one call on one thread.
///     </para>
/// </remarks>
internal sealed partial class LabelBuilder
{
    /// <summary>The line height of an HTML label, in em (Mermaid's <c>line-height: 1.5</c>).</summary>
    internal const double HtmlLineHeight = 1.5;

    /// <summary>The line spacing of an SVG label, in em (<c>createFormattedText</c>'s <c>lineHeight</c>).</summary>
    internal const double SvgLineHeight = 1.1;

    /// <summary>The padding of the background of an SVG label (<c>createFormattedText</c>).</summary>
    internal const double SvgBackgroundPadding = 2;

    /// <summary>The factor of the configured font size that a picture alone in an HTML label is wide (<c>configureLabelImages</c>).</summary>
    internal const double LoneImageFactor = 5;

    /// <summary>The most code points one missing-glyph report lists.</summary>
    private const int MaxReportedGlyphs = 8;

    /// <summary>The fonts of the call.</summary>
    private readonly FontResolver _fonts;

    /// <summary>The report sink of the call, or null.</summary>
    private readonly MermaidReportSink? _sink;

    /// <summary>The pictures of the call, or null when labels have none.</summary>
    private readonly NodeImages? _images;

    /// <summary>The resolved faces by family, weight and style.</summary>
    private readonly Dictionary<(string Family, bool Bold, bool Italic), TrueTypeFont> _faces = [];

    /// <summary>
    ///     Initializes a builder.
    /// </summary>
    /// <param name="fonts">The font resolver of the call (shared with the painter).</param>
    /// <param name="sink">The report sink of the call, or null for no reports.</param>
    /// <param name="images">The pictures of the call, or null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fonts"/> is null.</exception>
    public LabelBuilder(FontResolver fonts, MermaidReportSink? sink, NodeImages? images)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        _fonts = fonts;
        _sink = sink;
        _images = images;
    }

    /// <summary>
    ///     Lays out and measures a label.
    /// </summary>
    /// <param name="request">The label and its options.</param>
    /// <returns>The measured label.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    public LabelLayout Build(LabelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = request.Text;
        if (request.Prepare)
        {
            // labelHelper: sanitizeText(decodeEntities(label))
            text = HtmlSanitizer.Sanitize(DecodeEntities(text));
        }

        return request.HtmlLabels ? BuildHtml(text, request) : BuildSvg(text, request);
    }

    /// <summary>
    ///     Decodes Mermaid's entity placeholders (its <c>decodeEntities</c>): <c>ﬂ°°</c> to <c>&amp;#</c>, <c>ﬂ°</c> to
    ///     <c>&amp;</c> and <c>¶ß</c> to <c>;</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with the placeholders replaced.</returns>
    internal static string DecodeEntities(string text) =>
        text.Replace("\ufb02\u00b0\u00b0", "&#", StringComparison.Ordinal)
            .Replace("\ufb02\u00b0", "&", StringComparison.Ordinal)
            .Replace("\u00b6\u00df", ";", StringComparison.Ordinal);

    /// <summary>
    ///     Gets the face of a family list, weight and style.
    /// </summary>
    /// <param name="family">The CSS family list.</param>
    /// <param name="bold">Whether bold.</param>
    /// <param name="italic">Whether italic.</param>
    /// <returns>The face.</returns>
    internal TrueTypeFont Face(string family, bool bold, bool italic)
    {
        var key = (family, bold, italic);
        if (!_faces.TryGetValue(key, out var face))
        {
            face = _fonts.Resolve(family, bold, italic, out _).Font;
            _faces[key] = face;
        }

        return face;
    }

    /// <summary>
    ///     Measures the advance of a text in a font.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <returns>The advance width.</returns>
    internal double Advance(string text, SceneFont font)
    {
        if (text.Length == 0 || !(font.Size > 0))
        {
            return 0;
        }

        return TextRenderer.MeasureText(text, Face(font.Family, font.Bold, font.Italic), (float)font.Size).Width;
    }

    /// <summary>
    ///     Gets the ascent and descent the browser uses for a font: the font's metrics at the size, each rounded to a
    ///     whole pixel.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The rounded ascent and descent.</returns>
    internal (double Ascent, double Descent) VerticalMetrics(SceneFont font)
    {
        var face = Face(font.Family, font.Bold, font.Italic);
        var scale = font.Size / face.UnitsPerEm;
        return (Math.Round(face.Ascender * scale, MidpointRounding.AwayFromZero), Math.Round(-face.Descender * scale, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    ///     Reports the characters of a label that no bundled font has.
    /// </summary>
    /// <param name="lines">The lines of the label.</param>
    /// <param name="location">The location of the element.</param>
    private void ReportMissingGlyphs(IEnumerable<LabelLine> lines, MermaidLocation location)
    {
        if (_sink is null)
        {
            return;
        }

        var missing = new SortedSet<int>();
        foreach (var run in lines.SelectMany(line => line.Runs))
        {
            missing.UnionWith(GlyphFallback.FindMissing(Face(run.Font.Family, run.Font.Bold, run.Font.Italic), run.Text));
        }

        if (missing.Count == 0)
        {
            return;
        }

        var list = string.Join(", ", missing.Take(MaxReportedGlyphs).Select(c => string.Create(CultureInfo.InvariantCulture, $"U+{c:X4}")));
        var more = missing.Count > MaxReportedGlyphs ? ", ..." : string.Empty;
        _sink.Warn(
            MermaidDiagnosticCodes.TextMissingGlyph,
            $"no bundled font has a glyph for {list}{more}; the font's missing-glyph box is drawn instead.",
            location,
            "use characters of the Latin, Greek, Cyrillic or symbol ranges, which the bundled fonts cover");
    }
}

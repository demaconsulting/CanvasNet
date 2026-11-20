// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.State;

/// <summary>
///     The image of one element of a state diagram: its size, the outline its transitions end on, and the painter of
///     its items.
/// </summary>
/// <remarks>
///     The image is measured once, when the diagram is lowered for a call, and is immutable afterwards.
/// </remarks>
/// <param name="width">The width.</param>
/// <param name="height">The height.</param>
/// <param name="shape">The outline the transitions end on (inscribed in the box).</param>
/// <param name="draw">The painter: the items with the top-left corner at a point.</param>
internal sealed class StateImage(double width, double height, ClipShape shape, Func<double, double, IReadOnlyList<SceneItem>> draw) : ISvekEntityImage
{
    /// <inheritdoc/>
    public double Width { get; } = width;

    /// <inheritdoc/>
    public double Height { get; } = height;

    /// <inheritdoc/>
    public SvekClip Clip => new(shape, new SceneRect(0, 0, Width, Height));

    /// <inheritdoc/>
    public IReadOnlyList<SceneItem> Draw(double x, double y) => draw(x, y);
}

/// <summary>
///     The image of a composite state (PlantUML's <c>Cluster.drawUState</c> with its <c>RoundedContainer</c>): a
///     rounded outline with a filled header band that holds the name, a line under it, and the descriptions of the
///     composite state below the line.
/// </summary>
/// <remarks>
///     The members are <see cref="StateRenderer.CompositePadding"/> from the outline; an image is immutable and safe for
///     concurrent use.
/// </remarks>
internal sealed class StateCompositeImage : ISvekClusterImage
{
    /// <summary>The composite state.</summary>
    private readonly StateElement _element;

    /// <summary>The laid-out name.</summary>
    private readonly TextBlock _name;

    /// <summary>The laid-out descriptions.</summary>
    private readonly StateDescription _description;

    /// <summary>The fill of the header (and of the whole outline when the state has its own color), or null.</summary>
    private readonly Rgba32? _headerFill;

    /// <summary>The stroke of the outline, or null.</summary>
    private readonly SceneStroke? _stroke;

    /// <summary>The radius of the corners.</summary>
    private readonly double _radius;

    /// <summary>The height of the name band.</summary>
    private readonly double _nameHeight;

    /// <summary>
    ///     Initializes an image.
    /// </summary>
    /// <param name="element">The composite state.</param>
    /// <param name="name">The laid-out name.</param>
    /// <param name="description">The laid-out descriptions.</param>
    /// <param name="style">The style of states.</param>
    public StateCompositeImage(StateElement element, TextBlock name, StateDescription description, ElementStyle style)
    {
        (_element, _name, _description) = (element, name, description);
        _headerFill = element.Fill ?? style.Fill;
        var line = element.Line ?? style.Line ?? SkinTheme.LineColor;
        _stroke = style.LineWidth > 0 ? new SceneStroke(line, style.LineWidth) : null;
        _radius = style.RoundCorner / 2;
        _nameHeight = name.Height + (2 * StateRenderer.Margin);
        HeaderHeight = _nameHeight + (description.Height > 0 ? description.Height + StateRenderer.Margin : 0);
        MinWidth = Math.Max(name.Width, description.Width) + (4 * StateRenderer.Margin);
    }

    /// <inheritdoc/>
    public double HeaderHeight { get; }

    /// <inheritdoc/>
    public double MinWidth { get; }

    /// <inheritdoc/>
    public double Padding => StateRenderer.CompositePadding;

    /// <inheritdoc/>
    public IReadOnlyList<SceneItem> Draw(SceneRect box)
    {
        var (x, y, w, h) = (box.X, box.Y, box.Width, box.Height);
        var items = new List<SceneItem>();
        ScenePath Shape(IReadOnlyList<SceneSegment> segments, Rgba32? fill, SceneStroke? stroke) =>
            new(segments) { Role = "cluster", Id = _element.Id, Location = _element.Location, Fill = fill, Stroke = stroke };

        // The header band filled, the outline (filled whole only when the state has its own color), the line under the
        // name and under the descriptions
        items.Add(Shape(StateRenderer.RoundedTop(x, y, w, Math.Min(HeaderHeight, h), _radius), _headerFill, null));
        items.Add(Shape(SceneShapes.Rect(x, y, w, h, _radius), _element.Fill, _stroke));
        items.Add(Shape(SceneShapes.Line(x, y + _nameHeight, x + w, y + _nameHeight), null, _stroke));
        if (_description.Height > 0)
        {
            items.Add(Shape(SceneShapes.Line(x, y + HeaderHeight, x + w, y + HeaderHeight), null, _stroke));
        }

        // The name centered 5 below the top, the descriptions 5 from the left below the name band
        items.AddRange(_name.Draw(x + ((w - _name.Width) / 2), y + StateRenderer.Margin, TextAlign.Center, "cluster-title", _element.Location)
            .Select(t => t with { Id = _element.Id }));
        items.AddRange(_description.Draw(x + StateRenderer.Margin, y + _nameHeight + (StateRenderer.Margin / 2), _element.Id, _element.Location, _stroke?.Color));
        return items;
    }
}

/// <summary>
///     The laid-out description lines of a state: blocks of text separated by horizontal rules (a line of four or more
///     dashes, PlantUML's creole rule).
/// </summary>
/// <param name="Parts">The parts in order: a block of text, or null for a rule.</param>
internal sealed record StateDescription(IReadOnlyList<TextBlock?> Parts)
{
    /// <summary>The height of a rule: the line is in its middle.</summary>
    public const double RuleHeight = 10;

    /// <summary>Gets the empty description.</summary>
    public static StateDescription Empty { get; } = new([]);

    /// <summary>Gets the width: the widest block.</summary>
    public double Width => Parts.OfType<TextBlock>().Select(b => b.Width).DefaultIfEmpty(0).Max();

    /// <summary>Gets the height: the blocks and the rules.</summary>
    public double Height => Parts.Sum(p => p?.Height ?? RuleHeight);

    /// <summary>
    ///     Draws the description with its top-left corner at a point.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="id">The id the items carry.</param>
    /// <param name="location">The location the items carry.</param>
    /// <param name="rule">The color of the rules, or null for none.</param>
    /// <returns>The items.</returns>
    public IEnumerable<SceneItem> Draw(double x, double y, string id, PlantUmlLocation location, Rgba32? rule)
    {
        var width = Width;
        foreach (var part in Parts)
        {
            if (part is null)
            {
                if (rule is { } color)
                {
                    yield return new ScenePath(SceneShapes.Line(x, y + (RuleHeight / 2), x + width, y + (RuleHeight / 2)))
                    {
                        Role = "state-rule",
                        Id = id,
                        Location = location,
                        Stroke = new SceneStroke(color, 1),
                    };
                }

                y += RuleHeight;
                continue;
            }

            foreach (var text in part.Draw(x, y, TextAlign.Left, "state-description", location))
            {
                yield return text with { Id = id };
            }

            y += part.Height;
        }
    }
}

/// <summary>
///     Measures and paints the elements of state diagrams as PlantUML does (<c>svek/image/EntityImageState</c>,
///     <c>EntityImageStateCommon</c>, <c>EntityImageStateEmptyDescription</c>, <c>EntityImageCircleStart</c>,
///     <c>EntityImageCircleEnd</c>, <c>EntityImageBranch</c>, <c>EntityImageSynchroBar</c>,
///     <c>EntityImagePseudoState</c>, <c>EntityImageDeepHistory</c>, and the composite states of
///     <c>svek/Cluster.drawUState</c> and <c>RoundedContainer</c>).
/// </summary>
/// <remarks>
///     <para>
///         <b>State.</b> A rectangle with corners of 25 (a radius of 12.5) filled <c>#F1F1F1</c> and outlined
///         <c>#181818</c> half a pixel wide, at least 50 by 50: the name (14) centered 5 below the top, a line 5 below
///         the name, and the descriptions (14) 5 from the left and 5 below the line; the box is the name and the
///         descriptions with margins of 10 on each side. After <c>hide empty description</c> a state without
///         descriptions is at least 50 by 40, its name centered, without the line.
///     </para>
///     <para>
///         <b>Pseudo-states.</b> The start is a circle of 20 filled and outlined <c>#222222</c>; the end a ring of 22
///         (outlined <c>#222222</c>) around a filled circle of 12; a junction a filled circle of 12; a choice a diamond of
///         24 by 24; a fork or a join a <c>#555555</c> bar of 80 by 8 (8 by 80 in a left to right diagram); a history a
///         circle of 22 filled <c>#F1F1F1</c> with <c>H</c> (or <c>H*</c>) centered in it.
///     </para>
///     <para>
///         Every item carries the element's id and one of the roles <c>state</c>, <c>state-separator</c>,
///         <c>state-name</c>, <c>state-description</c>, <c>state-rule</c>, <c>state-start</c>, <c>state-end</c>,
///         <c>state-junction</c>, <c>state-choice</c>, <c>state-bar</c>, <c>state-history</c>, and
///         <c>state-history-text</c>; a composite state draws <c>cluster</c>, <c>cluster-title</c>,
///         <c>state-description</c>, and <c>state-rule</c> items. The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static class StateRenderer
{
    /// <summary>The margin around the name and the descriptions (PlantUML's <c>IEntityImage.MARGIN</c> and <c>MARGIN_LINE</c>).</summary>
    public const double Margin = 5;

    /// <summary>The smallest width of a state (<c>EntityImageState.MIN_WIDTH</c>).</summary>
    public const double MinWidth = 50;

    /// <summary>The smallest height of a state (<c>EntityImageState.MIN_HEIGHT</c>).</summary>
    public const double MinHeight = 50;

    /// <summary>The smallest height of a state drawn without descriptions (<c>EntityImageStateEmptyDescription</c>).</summary>
    public const double EmptyMinHeight = 40;

    /// <summary>The size of the start circle (<c>CircleStart.SIZE</c>).</summary>
    public const double StartSize = 20;

    /// <summary>The size of a junction (<c>CircleStart.JUNCTION_SIZE</c>).</summary>
    public const double JunctionSize = 12;

    /// <summary>The size of the end ring (<c>CircleEnd.SIZE</c>).</summary>
    public const double EndSize = 22;

    /// <summary>The gap between the end ring and its inner circle.</summary>
    public const double EndInset = 5;

    /// <summary>The size of a choice diamond (twice <c>EntityImageBranch.SIZE</c>).</summary>
    public const double ChoiceSize = 24;

    /// <summary>The length of a fork or join bar.</summary>
    public const double BarLength = 80;

    /// <summary>The thickness of a fork or join bar.</summary>
    public const double BarThickness = 8;

    /// <summary>The size of a history circle (<c>EntityImagePseudoState.SIZE</c>).</summary>
    public const double HistorySize = 22;

    /// <summary>The space between the members of a composite state and its outline.</summary>
    public const double CompositePadding = 10;

    /// <summary>
    ///     Builds the image of a simple state or a pseudo-state.
    /// </summary>
    /// <param name="element">The element (not a composite state).</param>
    /// <param name="hideEmptyDescription">Whether a state without descriptions is drawn without its line.</param>
    /// <param name="rankDir">The rank direction (a fork bar lies across it).</param>
    /// <param name="theme">The look.</param>
    /// <param name="text">The text layout of the call.</param>
    /// <param name="sink">The report sink of the call, or null.</param>
    /// <returns>The image.</returns>
    public static StateImage Image(StateElement element, bool hideEmptyDescription, SvekRankDir rankDir, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(text);
        return element.Kind switch
        {
            StateKind.Start => Circle(element, theme[SkinElement.StateStart], StartSize, "state-start"),
            StateKind.Junction => Circle(element, theme[SkinElement.StateStart], JunctionSize, "state-junction"),
            StateKind.End => EndImage(element, theme[SkinElement.StateEnd]),
            StateKind.Choice => ChoiceImage(element, theme[SkinElement.StatePseudo]),
            StateKind.ForkJoin => BarImage(element, theme[SkinElement.ActivityBar], rankDir),
            StateKind.History or StateKind.DeepHistory => HistoryImage(element, theme[SkinElement.StatePseudo], text, sink),
            _ => BoxImage(element, hideEmptyDescription, theme, text, sink),
        };
    }

    /// <summary>
    ///     Builds the image of a composite state.
    /// </summary>
    /// <param name="element">The composite state.</param>
    /// <param name="theme">The look.</param>
    /// <param name="text">The text layout of the call.</param>
    /// <param name="sink">The report sink of the call, or null.</param>
    /// <returns>The image.</returns>
    public static StateCompositeImage CompositeImage(StateElement element, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(text);
        var style = theme[SkinElement.State];
        var name = Layout(text, element.Display, style, sink, element.Location);
        var description = Describe(element, theme, text, sink);
        return new StateCompositeImage(element, name, description, style);
    }

    /// <summary>
    ///     Lays out the descriptions of an element, split at its rules.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="theme">The look.</param>
    /// <param name="text">The text layout.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The description.</returns>
    public static StateDescription Describe(StateElement element, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(text);
        if (element.Descriptions.Count == 0)
        {
            return StateDescription.Empty;
        }

        var style = theme[SkinElement.StateAttribute];
        var parts = new List<TextBlock?>();
        var pending = new List<string>();
        void Flush()
        {
            if (pending.Count > 0)
            {
                parts.Add(Layout(text, string.Join('\n', pending), style, sink, element.Location));
                pending.Clear();
            }
        }

        foreach (var line in element.Descriptions)
        {
            if (IsRule(line))
            {
                Flush();
                parts.Add(null);
            }
            else
            {
                pending.Add(line);
            }
        }

        Flush();
        return new StateDescription(parts);
    }

    /// <summary>
    ///     Decides whether a description line is a horizontal rule (four or more dashes and nothing else).
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>True when it is.</returns>
    public static bool IsRule(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var trimmed = line.Trim();
        return trimmed.Length >= 4 && trimmed.All(c => c == '-');
    }

    /// <summary>
    ///     Builds the outline of a band with rounded top corners and a square bottom.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="radius">The radius of the top corners.</param>
    /// <returns>The closed outline.</returns>
    public static IReadOnlyList<SceneSegment> RoundedTop(double x, double y, double width, double height, double radius)
    {
        // A quarter circle as one cubic curve (the usual 0.5523 handle)
        const double Kappa = 0.5522847498;
        var r = Math.Max(0, Math.Min(radius, Math.Min(height, width / 2)));
        var k = Kappa * r;
        return
        [
            SceneSegment.Move(x, y + height),
            SceneSegment.Line(x, y + r),
            SceneSegment.Cubic(x, y + r - k, x + r - k, y, x + r, y),
            SceneSegment.Line(x + width - r, y),
            SceneSegment.Cubic(x + width - r + k, y, x + width, y + r - k, x + width, y + r),
            SceneSegment.Line(x + width, y + height),
            SceneSegment.Close(),
        ];
    }

    /// <summary>
    ///     Builds the image of a simple state: its name band and its descriptions, or its name alone after
    ///     <c>hide empty description</c>.
    /// </summary>
    /// <param name="element">The state.</param>
    /// <param name="hideEmptyDescription">Whether a state without descriptions is drawn without its line.</param>
    /// <param name="theme">The look.</param>
    /// <param name="text">The text layout.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The image.</returns>
    private static StateImage BoxImage(StateElement element, bool hideEmptyDescription, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
    {
        var style = theme[SkinElement.State];
        var name = Layout(text, element.Display, style, sink, element.Location);
        var description = Describe(element, theme, text, sink);
        var fill = element.Fill ?? style.Fill;
        var line = element.Line ?? style.Line ?? SkinTheme.LineColor;
        var stroke = style.LineWidth > 0 ? new SceneStroke(line, style.LineWidth) : null;
        var radius = style.RoundCorner / 2;
        ScenePath Path(IReadOnlyList<SceneSegment> segments, string role, Rgba32? pathFill) =>
            new(segments) { Role = role, Id = element.Id, Location = element.Location, Fill = pathFill, Stroke = stroke };
        IEnumerable<SceneItem> Texts(IEnumerable<SceneText> texts) => texts.Select(t => t with { Id = element.Id });

        if (hideEmptyDescription && element.Descriptions.Count == 0)
        {
            var (ew, eh) = (Math.Max(name.Width + (2 * Margin), MinWidth), Math.Max(name.Height + (2 * Margin), EmptyMinHeight));
            return new StateImage(ew, eh, ClipShape.Rectangle, (x, y) =>
            {
                var items = new List<SceneItem> { Path(SceneShapes.Rect(x, y, ew, eh, radius), "state", fill) };
                items.AddRange(Texts(name.Draw(x + ((ew - name.Width) / 2), y + ((eh - name.Height) / 2), TextAlign.Center, "state-name", element.Location)));
                return items;
            });
        }

        var width = Math.Max(Math.Max(name.Width, description.Width) + (4 * Margin), MinWidth);
        var height = Math.Max(name.Height + description.Height + (4 * Margin), MinHeight);
        var lineY = Margin + name.Height + Margin;
        return new StateImage(width, height, ClipShape.Rectangle, (x, y) =>
        {
            var items = new List<SceneItem>
            {
                Path(SceneShapes.Rect(x, y, width, height, radius), "state", fill),
                Path(SceneShapes.Line(x, y + lineY, x + width, y + lineY), "state-separator", null),
            };
            items.AddRange(Texts(name.Draw(x + ((width - name.Width) / 2), y + Margin, TextAlign.Center, "state-name", element.Location)));
            items.AddRange(description.Draw(x + Margin, y + lineY + Margin, element.Id, element.Location, line));
            return items;
        });
    }

    /// <summary>
    ///     Builds the image of a filled circle (the start, a junction).
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">The style of the circle.</param>
    /// <param name="size">The diameter.</param>
    /// <param name="role">The role of the circle.</param>
    /// <returns>The image.</returns>
    private static StateImage Circle(StateElement element, ElementStyle style, double size, string role)
    {
        var fill = element.Fill ?? style.Fill;
        var stroke = StrokeOf(element.Line ?? style.Line, style.LineWidth);
        return new StateImage(size, size, ClipShape.Ellipse, (x, y) =>
        [
            new ScenePath(SceneShapes.Ellipse(x + (size / 2), y + (size / 2), size / 2, size / 2))
            {
                Role = role, Id = element.Id, Location = element.Location, Fill = fill, Stroke = stroke,
            },
        ]);
    }

    /// <summary>
    ///     Builds the image of the end: a ring around a filled circle.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">The style of the end.</param>
    /// <returns>The image.</returns>
    private static StateImage EndImage(StateElement element, ElementStyle style)
    {
        var fill = element.Fill ?? style.Fill;
        var stroke = StrokeOf(element.Line ?? style.Line, style.LineWidth);
        const double Half = EndSize / 2;
        return new StateImage(EndSize, EndSize, ClipShape.Ellipse, (x, y) =>
        [
            new ScenePath(SceneShapes.Ellipse(x + Half, y + Half, Half, Half))
            {
                Role = "state-end", Id = element.Id, Location = element.Location, Stroke = stroke,
            },
            new ScenePath(SceneShapes.Ellipse(x + Half, y + Half, Half - EndInset, Half - EndInset))
            {
                Role = "state-end", Id = element.Id, Location = element.Location, Fill = fill, Stroke = stroke,
            },
        ]);
    }

    /// <summary>
    ///     Builds the image of a choice: a diamond.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">The style of the pseudo-states.</param>
    /// <returns>The image.</returns>
    private static StateImage ChoiceImage(StateElement element, ElementStyle style)
    {
        var fill = element.Fill ?? style.Fill;
        var stroke = StrokeOf(element.Line ?? style.Line ?? SkinTheme.LineColor, style.LineWidth);
        const double Half = ChoiceSize / 2;
        return new StateImage(ChoiceSize, ChoiceSize, ClipShape.Diamond, (x, y) =>
        [
            new ScenePath(SceneShapes.Polygon((x + Half, y), (x + ChoiceSize, y + Half), (x + Half, y + ChoiceSize), (x, y + Half)))
            {
                Role = "state-choice", Id = element.Id, Location = element.Location, Fill = fill, Stroke = stroke,
            },
        ]);
    }

    /// <summary>
    ///     Builds the image of a fork or a join: a bar across the rank direction.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">The style of the bars.</param>
    /// <param name="rankDir">The rank direction.</param>
    /// <returns>The image.</returns>
    private static StateImage BarImage(StateElement element, ElementStyle style, SvekRankDir rankDir)
    {
        var fill = element.Fill ?? style.Fill;
        var (w, h) = rankDir == SvekRankDir.LeftToRight ? (BarThickness, BarLength) : (BarLength, BarThickness);
        return new StateImage(w, h, ClipShape.Rectangle, (x, y) =>
        [
            new ScenePath(SceneShapes.Rect(x, y, w, h)) { Role = "state-bar", Id = element.Id, Location = element.Location, Fill = fill },
        ]);
    }

    /// <summary>
    ///     Builds the image of a history: a circle with <c>H</c> or <c>H*</c>.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="style">The style of the pseudo-states.</param>
    /// <param name="text">The text layout.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <returns>The image.</returns>
    private static StateImage HistoryImage(StateElement element, ElementStyle style, TextLayout text, PlantUmlReportSink? sink)
    {
        var fill = element.Fill ?? style.Fill;
        var stroke = StrokeOf(element.Line ?? style.Line ?? SkinTheme.LineColor, style.LineWidth);
        var mark = Layout(text, element.Kind == StateKind.DeepHistory ? "H*" : "H", style, sink, element.Location);
        const double Half = HistorySize / 2;
        return new StateImage(HistorySize, HistorySize, ClipShape.Ellipse, (x, y) =>
        {
            var items = new List<SceneItem>
            {
                new ScenePath(SceneShapes.Ellipse(x + Half, y + Half, Half, Half))
                {
                    Role = "state-history", Id = element.Id, Location = element.Location, Fill = fill, Stroke = stroke,
                },
            };
            items.AddRange(mark.Draw(x + ((HistorySize - mark.Width) / 2), y + ((HistorySize - mark.Height) / 2), TextAlign.Center, "state-history-text", element.Location)
                .Select(t => t with { Id = element.Id }));
            return items;
        });
    }

    /// <summary>
    ///     Lays out a text in the font of a style.
    /// </summary>
    /// <param name="text">The text layout.</param>
    /// <param name="value">The text.</param>
    /// <param name="style">The style.</param>
    /// <param name="sink">The report sink, or null.</param>
    /// <param name="location">The location for reports.</param>
    /// <returns>The block.</returns>
    private static TextBlock Layout(TextLayout text, string value, ElementStyle style, PlantUmlReportSink? sink, PlantUmlLocation location) =>
        text.Layout(value, new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic), style.FontColor, 0, sink, location);

    /// <summary>
    ///     Creates a stroke, or null for no line.
    /// </summary>
    /// <param name="color">The color, or null.</param>
    /// <param name="width">The width.</param>
    /// <returns>The stroke.</returns>
    private static SceneStroke? StrokeOf(Rgba32? color, double width) =>
        color is { } c && width > 0 ? new SceneStroke(c, width) : null;
}

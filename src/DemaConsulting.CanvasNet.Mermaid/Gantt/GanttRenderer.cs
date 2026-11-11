// Portions derived from mermaid (MIT) and d3 (ISC); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     The gantt renderer: a port of Mermaid's <c>ganttRenderer.js</c> <c>draw</c> (with d3-axis for the time axes) that
///     turns a <see cref="GanttModel"/> into a scene, with the paint of Mermaid's gantt style sheet
///     (<see cref="GanttStyles"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Layout.</b> As in Mermaid: the chart is the page's width wide (784 pixels, the container of mermaid-cli's
///         800-pixel page within its 8-pixel margins, unless <c>gantt.useWidth</c> is set) and has a row of the bar height
///         and gap for each task that is not a vertical marker (in compact mode, as many rows as the most overlapping tasks
///         of each section), between two top paddings. A time scale maps the earliest start to the latest end onto the
///         width between the left and right paddings, rounded to whole pixels. Tasks are drawn in order of their start
///         (vertical markers last) over the background rows of their sections, with their texts inside the bar when they
///         fit, else to its right, or to its left when they would leave the chart; the section titles are at the left, the
///         axis (and a top axis) carries the grid, excluded days are shaded, and the today line and the title come last.
///     </para>
///     <para>
///         <b>Today.</b> The today line is drawn at <see cref="MermaidRenderOptions.Now"/>; without it the line is not drawn
///         (a <c>todayMarker</c> style is then reported as <see cref="MermaidDiagnosticCodes.GanttTodayUnavailable"/>).
///     </para>
///     <para>
///         <b>Roles.</b> The items carry roles and ids for tests and the reference comparison: <c>gantt-exclude</c> (the
///         id <c>exclude-YYYY-MM-DD</c>), the <c>gantt-axis</c> groups (<c>bottom</c>, <c>top</c>) with their
///         <c>gantt-tick</c> lines and <c>gantt-tick-label</c> texts, the <c>gantt-section</c> rows, the <c>gantt-task</c>
///         bars and the <c>gantt-task-text</c> texts (the task id, and the id with <c>-text</c>), the
///         <c>gantt-section-title</c> lines, the <c>gantt-today</c> line and the <c>title</c>.
///     </para>
///     <para>
///         <b>Cost.</b> The work meter (<see cref="MermaidLimits.MaxLayoutWork"/>) is charged for the task dates (see
///         <see cref="GanttModel.Compile"/>), each day of the excluded days, each step of the tick generation and each
///         tick label, the rows of the compact mode, the sorts and each measured text, and checks cancellation. Each tick
///         label is checked against <see cref="MermaidLimits.MaxLabelLength"/>.
///     </para>
///     <para>
///         An instance draws one render on one thread.
///     </para>
/// </remarks>
internal sealed partial class GanttRenderer
{
    /// <summary>The width of mermaid-cli's page container (an 800-pixel page with 8-pixel body margins).</summary>
    internal const double PageWidth = 784;

    /// <summary>The names an object inherits in JavaScript, which a section name hits in compact mode.</summary>
    private static readonly HashSet<string> ObjectPrototypeNames = new(StringComparer.Ordinal)
    {
        "__proto__", "constructor", "hasOwnProperty", "isPrototypeOf", "propertyIsEnumerable", "toLocaleString", "toString",
        "valueOf", "__defineGetter__", "__defineSetter__", "__lookupGetter__", "__lookupSetter__",
    };

    /// <summary>The model.</summary>
    private readonly GanttModel _model;

    /// <summary>The context of the render.</summary>
    private readonly DiagramRenderContext _context;

    /// <summary>The gantt configuration of the render.</summary>
    private readonly GanttConfig _conf;

    /// <summary>The style sheet of the render.</summary>
    private readonly GanttStyles _styles;

    /// <summary>The label builder of the render (text metrics).</summary>
    private readonly LabelBuilder _labels;

    /// <summary>The work meter of the render.</summary>
    private readonly LayoutWorkMeter _meter;

    /// <summary>The clock of the render.</summary>
    private readonly GanttClock _clock;

    /// <summary>The items of the scene, in paint order.</summary>
    private readonly List<SceneItem> _items = [];

    /// <summary>The tasks of the render.</summary>
    private List<GanttTask> _tasks = [];

    /// <summary>The sections of the tasks that are not vertical markers, each once (Mermaid's <c>categories</c>).</summary>
    private List<string> _categories = [];

    /// <summary>The rows of each section, in the order of a JavaScript object's keys (Mermaid's <c>categoryHeights</c>).</summary>
    private ObjectKeyMap<double> _categoryHeights = new();

    /// <summary>The time scale.</summary>
    private TimeScale _scale = new(double.NaN, double.NaN, 0, 0);

    /// <summary>The width of the chart.</summary>
    private double _w;

    /// <summary>The height of the chart.</summary>
    private double _h;

    /// <summary>
    ///     Initializes a renderer for one render.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The context of the render.</param>
    public GanttRenderer(GanttModel model, DiagramRenderContext context)
    {
        _model = model;
        _context = context;
        _conf = context.Config.Gantt;
        _styles = GanttStyles.Create(context.Config, context.Sink);
        _meter = new LayoutWorkMeter(context.Options.Limits.MaxLayoutWork, model.KeywordLocation, context.CancellationToken);
        _labels = new LabelBuilder(context.Fonts, context.Sink, null) { Meter = _meter };
        _clock = new GanttClock(context.Options.Now, context.Sink);
    }

    /// <summary>Gets the svg element of the chart, the root of every style chain.</summary>
    private static GanttElement Svg { get; } = new("svg", []);

    /// <summary>Gets a group without classes.</summary>
    private static GanttElement Group { get; } = new("g", []);

    /// <summary>
    ///     Gets the text the browser shows for a text that d3 sets as the content of an SVG text: Mermaid's entity
    ///     placeholders become character references when the SVG is written and are shown as their characters, and the
    ///     white space is collapsed as in all SVG text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text as shown.</returns>
    internal static string DisplayText(string text)
    {
        const string Prefix = "\ufb02\u00b0";
        const string Suffix = "\u00b6\u00df";
        var builder = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var start = text.IndexOf(Prefix, i, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf(Suffix, start + Prefix.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                builder.Append(text, i, text.Length - i);
                break;
            }

            builder.Append(text, i, start - i);
            builder.Append(HtmlSanitizer.DecodeReferences(LabelBuilder.DecodeEntities(text[start..(end + Suffix.Length)])));
            i = end + Suffix.Length;
        }

        return LabelBuilder.CollapseSvgWhiteSpace(builder.ToString());
    }

    /// <summary>
    ///     Draws the chart.
    /// </summary>
    /// <returns>The scene, whose view box is the chart's width and height.</returns>
    /// <exception cref="MermaidRenderException">Thrown, located, when the task dates fail or a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the render is canceled.</exception>
    public Scene Draw()
    {
        _tasks = _model.Compile(_clock, _meter);
        _w = _conf.UseWidth ?? PageWidth;
        var withoutVert = _tasks.Where(task => !task.Raw.Vert).ToList();
        _categories = CheckUnique(withoutVert.Select(task => task.Section));
        _h = 2 * _conf.TopPadding;
        if (_conf.DisplayMode == "compact")
        {
            CompactRows(withoutVert);
        }
        else
        {
            _h += withoutVert.Count * (_conf.BarHeight + _conf.BarGap);
            foreach (var category in _categories.Where(c => c != "__proto__"))
            {
                _categoryHeights[category] = withoutVert.Count(task => task.Section == category);
            }
        }

        // The time scale from the earliest start to the latest end, rounded to whole pixels
        var min = TimeAxis.Min(_tasks.Select(task => task.StartTime!.Value));
        var max = TimeAxis.Max(_tasks.Select(task => task.EndTime!.Value));
        _scale = new TimeScale(min?.Time ?? double.NaN, max?.Time ?? double.NaN, 0, _w - _conf.LeftPadding - _conf.RightPadding);

        // The tasks in order of their start (a stable sort)
        _meter.Charge(1 + _tasks.Count);
        var sorted = _tasks.OrderBy(task => task.StartTime!.Value.Time).ToList();

        var gap = _conf.BarHeight + _conf.BarGap;
        DrawExcludeDays(gap, sorted);
        MakeGrid();
        DrawRects(sorted, gap);
        VertLabels(gap);
        DrawToday();
        DrawTitle();
        return new Scene(new SceneRect(0, 0, _w, _h), _items);
    }

    /// <summary>
    ///     Gets the unique values of a list in their order (Mermaid's <c>checkUnique</c>, whose object never holds the key
    ///     <c>__proto__</c>, so that name is kept each time).
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The unique values.</returns>
    private static List<string> CheckUnique(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return [.. values.Where(value => value == "__proto__" || seen.Add(value))];
    }

    /// <summary>
    ///     Assigns the rows of the compact mode (the <c>getMaxIntersections</c> of each section, in the order of a
    ///     JavaScript object's keys) and grows the height.
    /// </summary>
    /// <param name="withoutVert">The tasks that are not vertical markers.</param>
    private void CompactRows(List<GanttTask> withoutVert)
    {
        var categoryElements = new ObjectKeyMap<List<GanttTask>>();
        foreach (var task in withoutVert)
        {
            if (ObjectPrototypeNames.Contains(task.Section))
            {
                // categoryElements[name] is an inherited value, whose push is not a function
                throw new MermaidRenderException(
                    MermaidDiagnosticCodes.RenderFailed,
                    $"Mermaid fails on a section named '{task.Section}' in compact mode (categoryElements[section].push is not a function)",
                    task.Raw.Location,
                    "rename the section, or leave the compact display mode");
            }

            if (categoryElements.TryGetValue(task.Section, out var list))
            {
                list.Add(task);
            }
            else
            {
                categoryElements[task.Section] = [task];
            }
        }

        var intersections = 0.0;
        foreach (var category in categoryElements.Keys())
        {
            var height = MaxIntersections(categoryElements[category], intersections) + 1;
            intersections += height;
            _h += height * (_conf.BarHeight + _conf.BarGap);
            _categoryHeights[category] = height;
        }
    }

    /// <summary>
    ///     Assigns each task of a section the first row where it starts after the end of the row's last task (Mermaid's
    ///     <c>getMaxIntersections</c>), in order of the start and the row.
    /// </summary>
    /// <param name="tasks">The tasks of the section.</param>
    /// <param name="orderOffset">The first row of the section.</param>
    /// <returns>The last row used, from 0.</returns>
    private double MaxIntersections(List<GanttTask> tasks, double orderOffset)
    {
        var timeline = Enumerable.Repeat(double.NegativeInfinity, tasks.Count).ToArray();
        _meter.Charge(1 + tasks.Count);
        var sorted = tasks.OrderBy(t => t.StartTime!.Value.Time).ThenBy(t => t.Order).ToList();
        var max = 0;
        foreach (var element in sorted)
        {
            for (var j = 0; j < timeline.Length; j++)
            {
                _meter.Charge(1);
                if (element.StartTime!.Value.Time >= timeline[j])
                {
                    timeline[j] = element.EndTime!.Value.Time;
                    element.Order = j + orderOffset;
                    max = Math.Max(max, j);
                    break;
                }
            }
        }

        return max;
    }

    /// <summary>
    ///     Gets the section index of a task's class (the last section of <c>categories</c> that is the task's, as the loops
    ///     without a break in Mermaid's class functions find it).
    /// </summary>
    /// <remarks>
    ///     The modulo is in doubles, as in JavaScript: <c>numberSectionStyles: 0</c> gives NaN (the class <c>taskNaN</c>,
    ///     which no style rule names), as Mermaid draws it, and never a division by zero.
    /// </remarks>
    /// <param name="section">The section.</param>
    /// <returns>The index modulo the number of section styles, as JavaScript writes it.</returns>
    private string SectionNumber(string section)
    {
        var number = 0.0;
        for (var i = 0; i < _categories.Count; i++)
        {
            if (_categories[i] == section)
            {
                number = i % _conf.NumberSectionStyles;
            }
        }

        return YamlSubsetParser.FormatJsNumber(number);
    }

    /// <summary>
    ///     Gets the class index of a background row or a section title (the first section of <c>categories</c> that
    ///     matches), or null when none matches.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <returns>The index modulo the number of section styles, as JavaScript writes it, or null.</returns>
    private string? FirstSectionNumber(string section)
    {
        var index = _categories.IndexOf(section);
        return index < 0 ? null : YamlSubsetParser.FormatJsNumber(index % _conf.NumberSectionStyles);
    }

    /// <summary>
    ///     Maps a date to the chart (<c>timeScale(date)</c>).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The x from the left padding, or NaN.</returns>
    private double X(JsDate date) => _scale.Map(date.Time);

    /// <summary>
    ///     Draws the title (the <c>titleText</c> at the middle of the width, at the title's top margin).
    /// </summary>
    private void DrawTitle()
    {
        var title = _model.DiagramTitle;
        if (title.Length == 0 && _context.Source.Frontmatter?.Title is { Length: > 0 } frontmatter)
        {
            title = GanttModel.Sanitize(frontmatter);
        }

        var style = _styles.Compute([Svg, GanttElement.Of("text", "titleText")]);
        if (TextLine(title, _w / 2, _conf.TitleTopMargin, style, "title", null, _model.TitleLocation) is { } line)
        {
            _items.Add(line);
        }
    }

    /// <summary>
    ///     Makes one SVG text with the box the browser's <c>getBBox</c> gives it.
    /// </summary>
    /// <param name="content">The text content (with Mermaid's entity placeholders).</param>
    /// <param name="x">The x of the anchor; NaN is the attribute's default, zero.</param>
    /// <param name="baseline">The y of the baseline.</param>
    /// <param name="style">The computed style.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id, or null.</param>
    /// <param name="location">The location of the construct.</param>
    /// <returns>The text, or null when it shows nothing.</returns>
    private SceneText? TextLine(string content, double x, double baseline, GanttComputedStyle style, string role, string? id, MermaidLocation location)
    {
        var text = DisplayText(content);
        if (text.Length == 0 || !StyleDeclarations.IsDrawableFontSize(style.FontSize))
        {
            return null;
        }

        x = double.IsFinite(x) ? x : 0;
        baseline = double.IsFinite(baseline) ? baseline : 0;
        _meter.Charge(1 + text.Length);
        var font = new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic);
        var (ascent, descent) = _labels.VerticalMetrics(font);
        var (advance, left, right) = _labels.SvgLineExtent(text, font);
        var start = style.TextAnchor switch
        {
            "middle" => x - (advance / 2),
            "end" => x - advance,
            _ => x,
        };
        var anchor = style.TextAnchor switch
        {
            "middle" => SceneTextAnchor.Middle,
            "end" => SceneTextAnchor.End,
            _ => SceneTextAnchor.Start,
        };
        var color = style.Faded(style.Fill) ?? new Rgba32(0, 0, 0, 0);
        return new SceneText(x, baseline, anchor, [new SceneTextRun(text, font, color)], new SceneRect(start + left, baseline - ascent, right - left, ascent + descent))
        {
            Role = role,
            Id = id,
            Location = location,
            Stroke = style.Faded(style.Stroke) is { } stroke && style.StrokeWidth > 0 ? new SceneStroke(stroke, style.StrokeWidth) : null,
        };
    }

    /// <summary>
    ///     Measures a text as the browser's <c>getBBox</c> does before the text's class is set: in the root's font family
    ///     at a size, with Mermaid's entity placeholders not yet decoded.
    /// </summary>
    /// <param name="content">The text content.</param>
    /// <param name="fontSize">The font size of its attribute.</param>
    /// <returns>The width of its box.</returns>
    private double MeasureBeforeClass(string content, double fontSize)
    {
        var measured = LabelBuilder.CollapseSvgWhiteSpace(content);
        _meter.Charge(1 + measured.Length);
        var style = _styles.Compute([Svg, Group, GanttElement.Of("text", string.Empty, ("font-size", YamlSubsetParser.FormatJsNumber(fontSize)))]);
        if (measured.Length == 0 || !StyleDeclarations.IsDrawableFontSize(style.FontSize))
        {
            return 0;
        }

        var (_, left, right) = _labels.SvgLineExtent(measured, new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic));
        return right - left;
    }
}

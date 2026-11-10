// Portions derived from mermaid (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <content>
///     The parts of the chart: <c>drawExcludeDays</c>, <c>drawRects</c> (the section rows, the bars and their texts),
///     <c>vertLabels</c> (the section titles) and <c>drawToday</c>.
/// </content>
internal sealed partial class GanttRenderer
{
    /// <summary>The CSS transform of a milestone in Mermaid's style sheet.</summary>
    private const string MilestoneTransform = "rotate(45deg) scale(0.8,0.8)";

    /// <summary>
    ///     Shades the excluded days (<c>drawExcludeDays</c>): each run of excluded days from the earliest start to the
    ///     latest end, unless the tasks span more than five years (then Mermaid only warns).
    /// </summary>
    /// <param name="gap">The row height.</param>
    /// <param name="tasks">The tasks in order of their start.</param>
    private void DrawExcludeDays(double gap, List<GanttTask> tasks)
    {
        _ = gap;
        if (_model.Excludes.Count == 0 && _model.Includes.Count == 0)
        {
            return;
        }

        JsDate? minTime = null;
        JsDate? maxTime = null;
        foreach (var task in tasks)
        {
            if (minTime is null || task.StartTime!.Value < minTime.Value)
            {
                minTime = task.StartTime;
            }

            if (maxTime is null || task.EndTime!.Value > maxTime.Value)
            {
                maxTime = task.EndTime;
            }
        }

        if (minTime is not { } first || maxTime is not { } last)
        {
            return;
        }

        if (Dayjs.DiffYears(last, first) > 5)
        {
            _context.Sink.Warn(
                MermaidDiagnosticCodes.GanttExcludesSkipped,
                "the tasks span more than five years, so the excluded days are not shaded, as in Mermaid",
                _model.KeywordLocation,
                "shorten the chart to five years to see the excluded days");
            return;
        }

        // The runs of excluded days; a run that is still open at the end is not drawn, as in Mermaid
        var ranges = new List<(JsDate Start, JsDate End)>();
        (JsDate Start, JsDate End)? range = null;
        var dayCost = _model.DayCost();
        for (var d = first; d.Time <= last.Time; d = Dayjs.Add(d, 1, "d"))
        {
            _meter.Charge(dayCost);
            bool excluded;
            try
            {
                excluded = _model.IsInvalidDate(d, _clock);
            }
            catch (NotSupportedException ex)
            {
                throw new MermaidRenderException(MermaidDiagnosticCodes.RenderFailed, ex.Message, _model.KeywordLocation, "remove the week (w, ww, wo, gggg) and zone name (z, zzz) tokens from the dateFormat");
            }

            if (excluded)
            {
                range = range is { } open ? (open.Start, d) : (d, d);
            }
            else if (range is { } closed)
            {
                ranges.Add(closed);
                range = null;
            }
        }

        var style = _styles.Compute([Svg, Group, GanttElement.Of("rect", "exclude-range")]);
        foreach (var (start, end) in ranges)
        {
            var x = X(Dayjs.StartOfDay(start)) + _conf.LeftPadding;
            var width = X(Dayjs.EndOfDay(end)) - X(Dayjs.StartOfDay(start));
            var height = _h - _conf.TopPadding - _conf.GridLineStartPadding;
            if (Rect(x, _conf.GridLineStartPadding, width, height, 0, style, "gantt-exclude", "exclude-" + Dayjs.Format(start, "YYYY-MM-DD", _clock.Today), _model.KeywordLocation) is { } rect)
            {
                _items.Add(rect);
            }
        }
    }

    /// <summary>
    ///     Draws the section rows, the bars and their texts (<c>drawRects</c>).
    /// </summary>
    /// <param name="sorted">The tasks in order of their start.</param>
    /// <param name="gap">The row height.</param>
    private void DrawRects(List<GanttTask> sorted, double gap)
    {
        // Vertical markers last (a stable sort)
        var tasks = sorted.Where(t => !t.Raw.Vert).Concat(sorted.Where(t => t.Raw.Vert)).ToList();
        var withoutVert = tasks.Where(t => !t.Raw.Vert).ToList();
        var topPad = _conf.TopPadding;
        var sidePad = _conf.LeftPadding;
        var barHeight = _conf.BarHeight;

        // The background rows, one per row, with the first task of the row's section
        var seen = new HashSet<double>();
        foreach (var task in withoutVert)
        {
            if (!seen.Add(task.Order))
            {
                continue;
            }

            var number = FirstSectionNumber(task.Section) ?? "0";
            var style = _styles.Compute([Svg, Group, GanttElement.Of("rect", "section section" + number)]);
            if (Rect(0, (task.Order * gap) + topPad - 2, _w - (_conf.RightPadding / 2), gap, 0, style, "gantt-section", task.Section, task.Raw.Location) is { } row)
            {
                _items.Add(row);
            }
        }

        // The bars
        foreach (var task in tasks)
        {
            DrawBar(task, withoutVert.Count, gap);
        }

        // The texts, after all bars
        foreach (var task in tasks)
        {
            DrawTaskText(task, withoutVert.Count, gap);
        }

        _ = sidePad;
        _ = barHeight;
    }

    /// <summary>
    ///     Draws the bar of a task: a rounded rectangle, a milestone square turned by its CSS transform, or a vertical
    ///     marker from the grid's top over all rows.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="rows">The number of tasks that are not vertical markers.</param>
    /// <param name="gap">The row height.</param>
    private void DrawBar(GanttTask task, int rows, double gap)
    {
        var raw = task.Raw;
        var startTime = task.StartTime!.Value;
        var endTime = task.EndTime!.Value;
        var start = X(startTime);
        var end = X(endTime);
        var barHeight = _conf.BarHeight;
        double x;
        double y;
        double width;
        double height;
        if (raw.Milestone)
        {
            x = start + _conf.LeftPadding + (0.5 * (end - start)) - (0.5 * barHeight);
            width = barHeight;
        }
        else
        {
            x = start + _conf.LeftPadding;
            width = raw.Vert ? 0.08 * barHeight : X(task.RenderEndTime ?? endTime) - start;
        }

        if (raw.Vert)
        {
            y = _conf.GridLineStartPadding;
            height = (rows * (_conf.BarHeight + _conf.BarGap)) + (_conf.BarHeight * 2);
        }
        else
        {
            y = (task.Order * gap) + _conf.TopPadding;
            height = barHeight;
        }

        var style = _styles.Compute([Svg, Group, GanttElement.Of("rect", BarClass(task))]);
        if (Rect(x, y, width, height, 3, style, "gantt-task", raw.Id ?? "undefined", raw.Location) is not { } bar)
        {
            return;
        }

        // A milestone is turned 45 degrees and scaled by 0.8 around its transform-origin
        if (style.Transform?.Trim() == MilestoneTransform)
        {
            var originX = start + _conf.LeftPadding + (0.5 * (end - start));
            var originY = (task.Order * gap) + _conf.TopPadding + (0.5 * barHeight);
            bar = Transformed(bar, originX, originY, Math.PI / 4, 0.8);
        }

        _items.Add(bar);
    }

    /// <summary>
    ///     Gets the class of a task's bar, as Mermaid builds it.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <returns>The class attribute.</returns>
    private string BarClass(GanttTask task)
    {
        var raw = task.Raw;
        var classes = string.Join(' ', raw.Classes);
        string taskClass;
        if (raw.Active)
        {
            taskClass = raw.Crit ? " activeCrit" : " active";
        }
        else if (raw.Done)
        {
            taskClass = raw.Crit ? " doneCrit" : " done";
        }
        else
        {
            taskClass = raw.Crit ? " crit" : string.Empty;
        }

        if (taskClass.Length == 0)
        {
            taskClass = " task";
        }

        if (raw.Milestone)
        {
            taskClass = " milestone " + taskClass;
        }

        if (raw.Vert)
        {
            taskClass = " vert " + taskClass;
        }

        return "task" + taskClass + SectionNumber(task.Section) + " " + classes;
    }

    /// <summary>
    ///     Draws the text of a task: inside its bar when it fits, else right of it, or left of it when it would leave the
    ///     chart; a vertical marker's text below the rows.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="rows">The number of tasks that are not vertical markers.</param>
    /// <param name="gap">The row height.</param>
    private void DrawTaskText(GanttTask task, int rows, double gap)
    {
        var raw = task.Raw;
        var barHeight = _conf.BarHeight;
        var sidePad = _conf.LeftPadding;
        var textWidth = MeasureBeforeClass(raw.Task, _conf.FontSize);
        var startTime = task.StartTime!.Value;
        var endTime = task.EndTime!.Value;

        // The x: from the drawn end, the milestone's square
        var startX = X(startTime);
        var endX = X(task.RenderEndTime ?? endTime);
        if (raw.Milestone)
        {
            startX += (0.5 * (X(endTime) - X(startTime))) - (0.5 * barHeight);
            endX = startX + barHeight;
        }

        double x;
        if (raw.Vert)
        {
            x = X(startTime) + sidePad;
        }
        else if (textWidth > endX - startX)
        {
            x = endX + textWidth + (1.5 * sidePad) > _w ? startX + sidePad - 5 : endX + sidePad + 5;
        }
        else
        {
            x = ((endX - startX) / 2) + startX + sidePad;
        }

        var y = raw.Vert
            ? _conf.GridLineStartPadding + (rows * (_conf.BarHeight + _conf.BarGap)) + 60
            : (task.Order * gap) + (_conf.BarHeight / 2) + ((_conf.FontSize / 2) - 2) + _conf.TopPadding;

        var style = _styles.Compute([
            Svg,
            Group,
            GanttElement.Of("text", TextClass(task, textWidth), ("font-size", YamlSubsetParser.FormatJsNumber(_conf.FontSize))),
        ]);
        if (TextLine(raw.Task, x, y, style, "gantt-task-text", (raw.Id ?? "undefined") + "-text", raw.Location) is { } text)
        {
            _items.Add(text);
        }
    }

    /// <summary>
    ///     Gets the class of a task's text, as Mermaid builds it: from the end (not the drawn end) and the unshifted start of
    ///     a milestone, so it may differ from the side the x was chosen for.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <param name="textWidth">The width of the text.</param>
    /// <returns>The class attribute (without the measured width, which no rule reads).</returns>
    private string TextClass(GanttTask task, double textWidth)
    {
        var raw = task.Raw;
        var startX = X(task.StartTime!.Value);
        var endX = raw.Milestone ? startX + _conf.BarHeight : X(task.EndTime!.Value);
        var classes = string.Join(' ', raw.Classes);
        var number = SectionNumber(task.Section);
        var taskType = string.Empty;
        if (raw.Active)
        {
            taskType = (raw.Crit ? "activeCritText" : "activeText") + number;
        }

        if (raw.Done)
        {
            taskType += (raw.Crit ? " doneCritText" : " doneText") + number;
        }
        else if (raw.Crit)
        {
            taskType += " critText" + number;
        }

        if (raw.Milestone)
        {
            taskType += " milestoneText";
        }

        if (raw.Vert)
        {
            taskType += " vertText";
        }

        if (textWidth > endX - startX)
        {
            return endX + textWidth + (1.5 * _conf.LeftPadding) > _w
                ? classes + " taskTextOutsideLeft taskTextOutside" + number + " " + taskType
                : classes + " taskTextOutsideRight taskTextOutside" + number + " " + taskType;
        }

        return classes + " taskText taskText" + number + " " + taskType;
    }

    /// <summary>
    ///     Draws the section titles (<c>vertLabels</c>): one text per section in the order of a JavaScript object's keys, at
    ///     the middle of its rows (after the rows of the sections before it), its lines split at <c>&lt;br&gt;</c> tags and
    ///     centered on the central baseline.
    /// </summary>
    /// <param name="gap">The row height.</param>
    private void VertLabels(double gap)
    {
        var prevGap = 0.0;
        var keys = _categoryHeights.Keys();
        for (var i = 0; i < keys.Length; i++)
        {
            var category = keys[i];
            var rows = _categoryHeights[category];
            if (i > 0)
            {
                prevGap += _categoryHeights[keys[i - 1]];
            }

            var y = ((rows * gap) / 2) + (prevGap * gap) + _conf.TopPadding;
            var number = FirstSectionNumber(category);
            var textElement = GanttElement.Of(
                "text",
                number is null ? "sectionTitle" : "sectionTitle sectionTitle" + number,
                ("font-size", YamlSubsetParser.FormatJsNumber(_conf.SectionFontSize)));
            var style = _styles.Compute([Svg, Group, textElement, GanttElement.Of("tspan", string.Empty)]);
            var lines = LabelBuilder.SplitBreaks(category);
            var fontSize = style.FontSize;
            var dy = -(lines.Count - 1) / 2.0;
            var (ascent, descent) = _labels.VerticalMetrics(new SceneFont(style.FontFamily, fontSize, style.Bold, style.Italic));
            for (var j = 0; j < lines.Count; j++)
            {
                var baseline = y + ((dy + j) * fontSize) + ((ascent - descent) / 2);
                if (TextLine(lines[j], 10, baseline, style, "gantt-section-title", category, _model.KeywordLocation) is { } line)
                {
                    _items.Add(line);
                }
            }
        }
    }

    /// <summary>
    ///     Draws the today line (<c>drawToday</c>) at <see cref="MermaidRenderOptions.Now"/>, from the title's top margin to
    ///     as far above the bottom, with the <c>todayMarker</c> style (its commas made semicolons) over the
    ///     <c>today</c> rule; nothing for <c>todayMarker off</c> or without a clock.
    /// </summary>
    private void DrawToday()
    {
        var marker = _model.TodayMarker;
        if (marker == "off")
        {
            return;
        }

        if (!_clock.IsKnown)
        {
            if (marker.Length > 0)
            {
                _context.Sink.Warn(
                    MermaidDiagnosticCodes.GanttTodayUnavailable,
                    "the today marker is not drawn: MermaidRenderOptions.Now is not set",
                    _model.TodayMarkerLocation,
                    "set MermaidRenderOptions.Now to the time the chart is drawn for, or write todayMarker off");
            }

            return;
        }

        var inline = marker.Length > 0
            ? StyleDeclarations.ParseBlock(marker.Replace(',', ';'), restrict: true, _model.TodayMarkerLocation, _context.Sink)
            : null;
        var style = _styles.Compute([Svg, GanttElement.Of("g", "today"), GanttElement.Of("line", "today") with { Inline = inline }]);
        var x = X(_clock.Today) + _conf.LeftPadding;
        x = double.IsFinite(x) ? x : 0;
        if (style.Faded(style.Stroke) is not { } stroke || style.StrokeWidth <= 0)
        {
            return;
        }

        _items.Add(new ScenePath([SceneSegment.Move(x, _conf.TitleTopMargin), SceneSegment.Line(x, _h - _conf.TitleTopMargin)])
        {
            Role = "gantt-today",
            Location = _model.TodayMarkerLocation,
            Stroke = new SceneStroke(stroke, style.StrokeWidth) { Dash = style.Dash.Count > 0 ? style.Dash : null },
        });
    }

    /// <summary>
    ///     Makes an SVG rectangle with rounded corners and its computed paint; a rectangle with a negative or zero size, or
    ///     a size that is not a number, is not rendered, as in SVG.
    /// </summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="r">The corner radius (<c>rx</c> and <c>ry</c>).</param>
    /// <param name="style">The computed style.</param>
    /// <param name="role">The role.</param>
    /// <param name="id">The id.</param>
    /// <param name="location">The location.</param>
    /// <returns>The path, or null.</returns>
    private static ScenePath? Rect(double x, double y, double width, double height, double r, GanttComputedStyle style, string role, string? id, MermaidLocation location)
    {
        if (!(width > 0) || !(height > 0) || !double.IsFinite(x) || !double.IsFinite(y))
        {
            return null;
        }

        var rx = Math.Min(r, width / 2);
        var ry = Math.Min(r, height / 2);
        SceneSegment[] segments = rx > 0 && ry > 0
            ?
            [
                SceneSegment.Move(x + rx, y),
                SceneSegment.Line(x + width - rx, y),
                SceneSegment.Arc(rx, ry, 0, false, true, x + width, y + ry),
                SceneSegment.Line(x + width, y + height - ry),
                SceneSegment.Arc(rx, ry, 0, false, true, x + width - rx, y + height),
                SceneSegment.Line(x + rx, y + height),
                SceneSegment.Arc(rx, ry, 0, false, true, x, y + height - ry),
                SceneSegment.Line(x, y + ry),
                SceneSegment.Arc(rx, ry, 0, false, true, x + rx, y),
                SceneSegment.Close(),
            ]
            :
            [
                SceneSegment.Move(x, y),
                SceneSegment.Line(x + width, y),
                SceneSegment.Line(x + width, y + height),
                SceneSegment.Line(x, y + height),
                SceneSegment.Close(),
            ];
        var stroke = style.Faded(style.Stroke);
        var dash = style.Dash.Count > 0 ? style.Dash : null;
        return new ScenePath(segments)
        {
            Role = role,
            Id = id,
            Location = location,
            Fill = style.Faded(style.Fill),
            Stroke = stroke is { } color && style.StrokeWidth > 0 ? new SceneStroke(color, style.StrokeWidth) { Dash = dash } : null,
        };
    }

    /// <summary>
    ///     Turns and scales a path around an origin (a CSS <c>rotate</c> then <c>scale</c>); the stroke is scaled too.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="originX">The x of the origin.</param>
    /// <param name="originY">The y of the origin.</param>
    /// <param name="angle">The angle in radians, clockwise.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The path.</returns>
    private static ScenePath Transformed(ScenePath path, double originX, double originY, double angle, double scale)
    {
        var (sin, cos) = Math.SinCos(angle);
        (double X, double Y) Map(double px, double py)
        {
            var dx = (px - originX) * scale;
            var dy = (py - originY) * scale;
            return (originX + (dx * cos) - (dy * sin), originY + (dx * sin) + (dy * cos));
        }

        var segments = path.Segments.Select(segment =>
        {
            var (x, y) = Map(segment.X, segment.Y);
            return segment.Kind == SceneSegmentKind.ArcTo
                ? segment with { X = x, Y = y, X1 = segment.X1 * scale, Y1 = segment.Y1 * scale, X2 = segment.X2 + (angle * 180 / Math.PI) }
                : segment with { X = x, Y = y };
        }).ToList();
        return path with
        {
            Segments = segments,
            Stroke = path.Stroke is { } stroke ? stroke with { Width = stroke.Width * scale } : null,
        };
    }

}

// Portions derived from mermaid (MIT) and d3 (ISC); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <content>
///     The time axes: Mermaid's <c>makeGrid</c> (the axis format, the custom tick interval with its tick-count guard and
///     the week intervals by weekday) and d3-axis's <c>axisBottom</c> and <c>axisTop</c> (ticks at whole pixels plus the
///     half-pixel offset, grid lines of the tick size, labels three pixels past the axis).
/// </content>
internal sealed partial class GanttRenderer
{
    /// <summary>The most ticks a custom tick interval may make (Mermaid's <c>MAX_TICK_COUNT</c>).</summary>
    private const double MaxTickCount = 10000;

    /// <summary>d3-axis's offset of the ticks at a device pixel ratio of 1.</summary>
    private const double AxisOffset = 0.5;

    /// <summary>d3-axis's padding between a tick and its label.</summary>
    private const double TickPadding = 3;

    /// <summary>The units of a custom tick interval and their lengths in dayjs's <c>duration</c> (a month is 365/12 days).</summary>
    private static readonly (string Unit, double Milliseconds)[] TickUnits =
    [
        ("millisecond", 1), ("second", 1000), ("minute", 60_000), ("hour", 3_600_000), ("day", 86_400_000),
        ("week", 604_800_000), ("month", 2_628_000_000),
    ];

    /// <summary>
    ///     Draws the bottom axis and, with <c>gantt.topAxis</c>, the top axis (<c>makeGrid</c>).
    /// </summary>
    private void MakeGrid()
    {
        var dateFormat = _model.DateFormat;
        string axisFormat;
        if (_model.AxisFormat.Length > 0)
        {
            axisFormat = _model.AxisFormat;
        }
        else
        {
            axisFormat = dateFormat == "D" ? "%d" : _conf.AxisFormat ?? "%Y-%m-%d";
        }

        var tickSize = -_h + _conf.TopPadding + _conf.GridLineStartPadding;
        var tickInterval = string.IsNullOrEmpty(_model.TickInterval) ? _conf.TickInterval : _model.TickInterval;
        var bottom = Ticks(tickInterval, warn: true);
        DrawAxis("bottom", _h - 50, 1, tickSize, bottom, axisFormat, "1em");
        if (_conf.TopAxis)
        {
            DrawAxis("top", _conf.TopPadding, -1, tickSize, Ticks(tickInterval, warn: false), axisFormat, "0em");
        }
    }

    /// <summary>
    ///     Gets the ticks of an axis: at a custom interval (<c>^([1-9]\d*)(millisecond|second|...|month)$</c>) when its
    ///     estimated count is at most 10,000, else d3's default ten ticks.
    /// </summary>
    /// <param name="tickInterval">The tick interval, or null.</param>
    /// <param name="warn">Whether a skipped interval is reported (Mermaid warns for the bottom axis only).</param>
    /// <returns>The ticks.</returns>
    private List<JsDate> Ticks(string? tickInterval, bool warn)
    {
        void OnStep() => _meter.Charge(1);
        if (ParseTickInterval(tickInterval) is { } custom)
        {
            var (every, unit, unitMs) = custom;
            var min = _scale.Domain0;
            var max = _scale.Domain1;
            var intervalMs = every * unitMs;

            // Mermaid's getEstimatedTickCount counts intervals, not ticks: a span of exactly 10,000 intervals passes and
            // draws 10,001 ticks (both ends), as mermaid-cli does
            var estimate = min > max || !(intervalMs > 0) ? double.PositiveInfinity : Math.Ceiling((max - min) / intervalMs);
            if (estimate > MaxTickCount)
            {
                if (warn)
                {
                    _context.Sink.Warn(
                        MermaidDiagnosticCodes.GanttTicksSkipped,
                        string.Create(CultureInfo.InvariantCulture, $"the tick interval '{tickInterval}' would make {estimate} ticks, more than 10000, so the default ticks are drawn, as in Mermaid"),
                        _model.KeywordLocation,
                        "choose a longer tickInterval for the span of the chart");
                }
            }
            else
            {
                var interval = unit switch
                {
                    "millisecond" => TimeAxis.Millisecond,
                    "second" => TimeAxis.Second,
                    "minute" => TimeAxis.Minute,
                    "hour" => TimeAxis.Hour,
                    "day" => TimeAxis.Day,
                    "week" => _model.Weekday switch
                    {
                        "monday" => TimeAxis.Monday,
                        "tuesday" => TimeAxis.Tuesday,
                        "wednesday" => TimeAxis.Wednesday,
                        "thursday" => TimeAxis.Thursday,
                        "friday" => TimeAxis.Friday,
                        "saturday" => TimeAxis.Saturday,
                        _ => TimeAxis.Sunday,
                    },
                    _ => TimeAxis.Month,
                };

                // An interval that every cannot make (an infinite step) is null, which d3's time scale takes as 10 ticks
                return interval.Every(every) is { } stepped ? _scale.Ticks(stepped, OnStep) : _scale.Ticks(10, OnStep);
            }
        }

        return _scale.Ticks(10, OnStep);
    }

    /// <summary>
    ///     Parses a custom tick interval (<c>/^([1-9]\d*)(millisecond|second|minute|hour|day|week|month)$/</c>).
    /// </summary>
    /// <param name="text">The interval, or null.</param>
    /// <returns>The step, the unit and the unit's length in milliseconds, or null when the text is no interval.</returns>
    internal static (double Every, string Unit, double UnitMs)? ParseTickInterval(string? text)
    {
        if (text is null || text.Length == 0 || text[0] is < '1' or > '9')
        {
            return null;
        }

        var digits = 1;
        while (digits < text.Length && char.IsAsciiDigit(text[digits]))
        {
            digits++;
        }

        var unit = text[digits..];
        foreach (var (name, milliseconds) in TickUnits)
        {
            if (unit == name)
            {
                return (double.Parse(text.AsSpan(0, digits), NumberStyles.None, CultureInfo.InvariantCulture), name, milliseconds);
            }
        }

        return null;
    }

    /// <summary>
    ///     Draws an axis (d3-axis): a group at the left padding and a y, with each tick's grid line and label.
    /// </summary>
    /// <param name="id">The axis: <c>bottom</c> or <c>top</c>.</param>
    /// <param name="y">The y of the axis.</param>
    /// <param name="k">1 for the bottom axis, -1 for the top one.</param>
    /// <param name="tickSize">The inner and outer tick size.</param>
    /// <param name="ticks">The ticks.</param>
    /// <param name="axisFormat">The d3 time format of the labels.</param>
    /// <param name="dy">The <c>dy</c> of the labels.</param>
    private void DrawAxis(string id, double y, double k, double tickSize, List<JsDate> ticks, string axisFormat, string dy)
    {
        var grid = GanttElement.Of("g", "grid", ("fill", "none"), ("font-size", "10"), ("font-family", "sans-serif"), ("text-anchor", "middle"));
        var tick = GanttElement.Of("g", "tick", ("opacity", "1"));
        var lineStyle = _styles.Compute([Svg, grid, tick, GanttElement.Of("line", string.Empty, ("stroke", "currentColor"))]);
        var textElement = GanttElement.Of("text", string.Empty, ("fill", "#000"), ("stroke", "none"), ("font-size", "10")) with
        {
            Inline = new StyleDeclarationSet([new StyleDeclaration("text-anchor", "middle", false)]),
        };
        var textStyle = _styles.Compute([Svg, grid, tick, textElement]);
        var children = new List<SceneItem>();

        // The domain path, which the style sheet hides (stroke-width 0)
        var domainStyle = _styles.Compute([Svg, grid, GanttElement.Of("path", "domain", ("stroke", "currentColor"))]);
        var range1 = _w - _conf.LeftPadding - _conf.RightPadding + AxisOffset;
        if (domainStyle.Faded(domainStyle.Stroke) is { } domainStroke && domainStyle.StrokeWidth > 0)
        {
            children.Add(new ScenePath(
            [
                SceneSegment.Move(AxisOffset, k * tickSize),
                SceneSegment.Line(AxisOffset, AxisOffset),
                SceneSegment.Line(range1, AxisOffset),
                SceneSegment.Line(range1, k * tickSize),
            ])
            {
                Role = "gantt-axis-domain",
                Id = id,
                Location = _model.KeywordLocation,
                Stroke = new SceneStroke(domainStroke, domainStyle.StrokeWidth),
            });
        }

        var spacing = Math.Max(tickSize, 0) + TickPadding;
        var em = dy == "1em" ? textStyle.FontSize : 0;
        foreach (var value in ticks)
        {
            var x = _scale.Map(value.Time) + AxisOffset;
            x = double.IsFinite(x) ? x : 0;
            if (lineStyle.Faded(lineStyle.Stroke) is { } stroke && lineStyle.StrokeWidth > 0 && double.IsFinite(tickSize))
            {
                children.Add(new ScenePath([SceneSegment.Move(x, 0), SceneSegment.Line(x, k * tickSize)])
                {
                    Role = "gantt-tick",
                    Id = id,
                    Location = _model.KeywordLocation,
                    Stroke = new SceneStroke(stroke, lineStyle.StrokeWidth),
                });
            }

            // The label, its length checked before it is measured
            _meter.Charge(1 + axisFormat.Length);
            var label = TimeAxis.Format(axisFormat, value);
            if (label.Length > _context.Options.Limits.MaxLabelLength)
            {
                throw new MermaidRenderException(
                    MermaidDiagnosticCodes.LimitExceeded,
                    string.Create(CultureInfo.InvariantCulture, $"a tick label is {label.Length} characters long, more than the limit of {_context.Options.Limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                    _model.KeywordLocation,
                    "shorten the axisFormat, or raise MermaidLimits.MaxLabelLength when the source is trusted");
            }

            if (TextLine(label, x, (k * spacing) + em, textStyle, "gantt-tick-label", id, _model.KeywordLocation) is { } text)
            {
                children.Add(text);
            }
        }

        _items.Add(new SceneGroup(_conf.LeftPadding, y, children) { Role = "gantt-axis", Id = id, Location = _model.KeywordLocation });
    }
}

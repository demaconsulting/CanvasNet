// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Gantt;

/// <summary>
///     How a task's start is given (the <c>raw.startTime</c> of Mermaid's task).
/// </summary>
internal enum GanttStartKind
{
    /// <summary>The task has no start: its data has more than three fields (Mermaid leaves it undefined).</summary>
    None,

    /// <summary>The task starts at the end of the task before it (<c>prevTaskEnd</c>).</summary>
    PreviousTaskEnd,

    /// <summary>The task starts at a date or after other tasks (<c>getStartDate</c>).</summary>
    StartDate,
}

/// <summary>
///     A task as Mermaid's gantt database holds it after parsing (its raw task): the name, section, id, tags, order,
///     classes and the start and end data that a render compiles into dates.
/// </summary>
internal sealed class GanttRawTask
{
    /// <summary>Gets or sets the section (Mermaid's <c>section</c> and <c>type</c>; empty before the first section).</summary>
    public required string Section { get; init; }

    /// <summary>Gets or sets the name as written (the <c>taskTxt</c> token, trailing white space included).</summary>
    public required string Task { get; init; }

    /// <summary>Gets or sets the id, or null when the data has more than three fields (undefined in Mermaid).</summary>
    public string? Id { get; init; }

    /// <summary>Gets or sets the id of the task before it, or null for none (undefined in Mermaid).</summary>
    public string? PreviousTaskId { get; init; }

    /// <summary>Gets or sets how the start is given.</summary>
    public GanttStartKind StartKind { get; init; }

    /// <summary>Gets or sets the start data (a date, a timestamp or <c>after</c> ids), or null.</summary>
    public string? StartData { get; init; }

    /// <summary>Gets or sets the end data (a date, a duration or <c>until</c> ids), or null.</summary>
    public string? EndData { get; init; }

    /// <summary>Gets or sets a value indicating whether the task has the <c>active</c> tag.</summary>
    public bool Active { get; init; }

    /// <summary>Gets or sets a value indicating whether the task has the <c>done</c> tag.</summary>
    public bool Done { get; init; }

    /// <summary>Gets or sets a value indicating whether the task has the <c>crit</c> tag.</summary>
    public bool Crit { get; init; }

    /// <summary>Gets or sets a value indicating whether the task has the <c>milestone</c> tag.</summary>
    public bool Milestone { get; init; }

    /// <summary>Gets or sets a value indicating whether the task has the <c>vert</c> tag (a vertical marker).</summary>
    public bool Vert { get; init; }

    /// <summary>Gets or sets the row of the task in the order of the text (-1 for a vertical marker).</summary>
    public int Order { get; init; }

    /// <summary>Gets the classes (<c>clickable</c> once for each click call on the task).</summary>
    public List<string> Classes { get; } = [];

    /// <summary>Gets or sets the location of the task statement.</summary>
    public MermaidLocation Location { get; init; }
}

/// <summary>
///     A parsed gantt chart: the state of Mermaid's gantt database (<c>ganttDb.js</c> with the title and accessibility
///     texts of <c>commonDb.ts</c>) and the metadata of the document; a render compiles its task dates
///     (<see cref="Compile"/>) and lays it out with <see cref="GanttRenderer"/>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Database.</b> The setters store their texts as the grammar passes them (trailing white space kept; the uses
///         trim). <c>excludes</c> and <c>includes</c> merge their lower-cased tokens across statements, each once. A task's
///         data is split on commas; leading tags (<c>active</c>, <c>done</c>, <c>crit</c>, <c>milestone</c>, <c>vert</c>)
///         are taken off, then one field is an end after the previous task, two are a start and an end, and three are an
///         id, a start and an end; a task without an id is <c>task1</c>, <c>task2</c> and so on. A vertical marker has the
///         row -1, the others the next row. Ids are kept in a JavaScript object, so a later task with the same id replaces
///         the earlier one for lookups, and the id <c>__proto__</c> is never found.
///     </para>
///     <para>
///         <b>Links.</b> A <c>click</c> adds the class <c>clickable</c> to each of its tasks that exists, once for each call;
///         an <c>href</c> is sanitized as in Mermaid's <c>strict</c> security level (<see cref="UrlSanitizer"/>) and kept
///         as data; links are never drawn as links and callbacks are never bound.
///     </para>
///     <para>
///         The model is filled by one parser on one thread and never changed after parsing; compiling and laying out keep
///         their state per call, so it is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed partial class GanttModel : IMermaidDiagram
{
    /// <summary>The tags of a task, in the order Mermaid tests them.</summary>
    private static readonly string[] TaskTags = ["active", "done", "crit", "milestone", "vert"];

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The tasks in the order of the text.</summary>
    private readonly List<GanttRawTask> _tasks = [];

    /// <summary>The index of the last task of each id (Mermaid's <c>taskDb</c>).</summary>
    private readonly Dictionary<string, int> _taskDb = new(StringComparer.Ordinal);

    /// <summary>The section statements in order.</summary>
    private readonly List<string> _sections = [];

    /// <summary>The <c>excludes</c> tokens, each once.</summary>
    private readonly List<string> _excludes = [];

    /// <summary>The <c>includes</c> tokens, each once.</summary>
    private readonly List<string> _includes = [];

    /// <summary>The links by task id, in the order they were first set.</summary>
    private readonly List<KeyValuePair<string, string>> _links = [];

    /// <summary>The number of tasks without an id so far (Mermaid's <c>taskCnt</c>).</summary>
    private int _taskCount;

    /// <summary>The id of the last task, or null (Mermaid's <c>lastTaskID</c>).</summary>
    private string? _lastTaskId;

    /// <summary>The next row (Mermaid's <c>lastOrder</c>).</summary>
    private int _lastOrder;

    /// <summary>The current section (Mermaid's <c>currentSection</c>).</summary>
    private string _currentSection = string.Empty;

    /// <summary>
    ///     Initializes an empty model.
    /// </summary>
    /// <param name="limits">The limits of the open call.</param>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    public GanttModel(MermaidLimits limits, MermaidLocation keywordLocation)
    {
        _limits = limits;
        KeywordLocation = keywordLocation;
        TodayMarkerLocation = keywordLocation;
        TitleLocation = keywordLocation;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Gets the date format as written (<c>getDateFormat</c>; empty by default).</summary>
    public string DateFormat { get; private set; } = string.Empty;

    /// <summary>Gets the axis format as written (<c>getAxisFormat</c>; empty by default).</summary>
    public string AxisFormat { get; private set; } = string.Empty;

    /// <summary>Gets the tick interval as written, or null when none is set (<c>getTickInterval</c>).</summary>
    public string? TickInterval { get; private set; }

    /// <summary>Gets the today marker setting as written: empty, <c>off</c> or a style (<c>getTodayMarker</c>).</summary>
    public string TodayMarker { get; private set; } = string.Empty;

    /// <summary>Gets the location of the last <c>todayMarker</c> statement, or the keyword's.</summary>
    public MermaidLocation TodayMarkerLocation { get; private set; }

    /// <summary>Gets a value indicating whether end dates are inclusive (<c>endDatesAreInclusive</c>).</summary>
    public bool InclusiveEndDates { get; private set; }

    /// <summary>Gets the weekday the week ticks start on (<c>getWeekday</c>; <c>sunday</c> by default).</summary>
    public string Weekday { get; private set; } = "sunday";

    /// <summary>Gets the first day of the weekend: <c>friday</c> or <c>saturday</c> (the default).</summary>
    public string Weekend { get; private set; } = "saturday";

    /// <summary>Gets the title of the <c>title</c> statement, sanitized (empty when there is none).</summary>
    public string DiagramTitle { get; private set; } = string.Empty;

    /// <summary>Gets the location of the last title statement, or the keyword's.</summary>
    public MermaidLocation TitleLocation { get; private set; }

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; private set; } = string.Empty;

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; private set; } = string.Empty;

    /// <summary>Gets the section statements in order (<c>getSections</c>).</summary>
    public IReadOnlyList<string> Sections => _sections;

    /// <summary>Gets the tasks in the order of the text.</summary>
    public IReadOnlyList<GanttRawTask> Tasks => _tasks;

    /// <summary>Gets the <c>excludes</c> tokens (<c>getExcludes</c>).</summary>
    public IReadOnlyList<string> Excludes => _excludes;

    /// <summary>Gets the <c>includes</c> tokens (<c>getIncludes</c>).</summary>
    public IReadOnlyList<string> Includes => _includes;

    /// <summary>Gets the sanitized links by task id, in the order they were first set (<c>getLinks</c>).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Links => _links;

    /// <summary>Gets the title the diagram declares, trimmed, or null (the frontmatter title is the document's fallback).</summary>
    public string? Title => Preprocessor.TrimJs(DiagramTitle) is { Length: > 0 } title ? title : null;

    /// <summary>Gets the accessible title, or null.</summary>
    public string? AccTitle => AccessibleTitle.Length > 0 ? AccessibleTitle : null;

    /// <summary>Gets the direction: a gantt chart has none.</summary>
    public string? Direction => null;

    /// <summary>Gets the number of tasks, vertical markers included.</summary>
    public int NodeCount => _tasks.Count;

    /// <summary>Gets the number of edges: a gantt chart has none.</summary>
    public int EdgeCount => 0;

    /// <summary>
    ///     Gets the number of sections whose titles are drawn: the sections of the tasks that are not vertical markers,
    ///     each once (a section named <c>__proto__</c> is not drawn, as in Mermaid).
    /// </summary>
    public int SubgraphCount => _tasks
        .Where(task => !task.Vert && task.Section != "__proto__")
        .Select(task => task.Section)
        .Distinct(StringComparer.Ordinal)
        .Count();

    /// <inheritdoc />
    public Scene Layout(DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new GanttRenderer(this, context).Draw();
    }

    /// <summary>
    ///     Checks the length of a text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="location">The location of the text.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, when the text is too long.</exception>
    internal static void CheckLength(int length, MermaidLimits limits, MermaidLocation location)
    {
        if (length > limits.MaxLabelLength)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {length} characters long, more than the limit of {limits.MaxLabelLength} (MermaidLimits.MaxLabelLength)"),
                location,
                "shorten the text, or raise MermaidLimits.MaxLabelLength when the source is trusted");
        }
    }

    /// <summary>
    ///     Sets the date format (<c>setDateFormat</c>).
    /// </summary>
    /// <param name="text">The format as written.</param>
    public void SetDateFormat(string text) => DateFormat = text;

    /// <summary>
    ///     Makes end dates inclusive (<c>enableInclusiveEndDates</c>).
    /// </summary>
    public void EnableInclusiveEndDates() => InclusiveEndDates = true;

    /// <summary>
    ///     Sets the axis format (<c>setAxisFormat</c>).
    /// </summary>
    /// <param name="text">The format as written.</param>
    public void SetAxisFormat(string text) => AxisFormat = text;

    /// <summary>
    ///     Sets the tick interval (<c>setTickInterval</c>).
    /// </summary>
    /// <param name="text">The interval as written.</param>
    public void SetTickInterval(string text) => TickInterval = text;

    /// <summary>
    ///     Sets the today marker (<c>setTodayMarker</c>).
    /// </summary>
    /// <param name="text">The setting as written.</param>
    /// <param name="location">The location of the statement.</param>
    public void SetTodayMarker(string text, MermaidLocation location)
    {
        TodayMarker = text;
        TodayMarkerLocation = location;
    }

    /// <summary>
    ///     Adds the tokens of an <c>excludes</c> statement (<c>setExcludes</c>: lower-cased, split on white space and
    ///     commas, each once).
    /// </summary>
    /// <param name="text">The tokens as written.</param>
    public void SetExcludes(string text) => MergeTokens(_excludes, text);

    /// <summary>
    ///     Adds the tokens of an <c>includes</c> statement (<c>setIncludes</c>).
    /// </summary>
    /// <param name="text">The tokens as written.</param>
    public void SetIncludes(string text) => MergeTokens(_includes, text);

    /// <summary>
    ///     Sets the weekday the week ticks start on (<c>setWeekday</c>).
    /// </summary>
    /// <param name="day">The day, in lower case.</param>
    public void SetWeekday(string day) => Weekday = day;

    /// <summary>
    ///     Sets the first day of the weekend (<c>setWeekend</c>).
    /// </summary>
    /// <param name="day">The day: <c>friday</c> or <c>saturday</c>.</param>
    public void SetWeekend(string day) => Weekend = day;

    /// <summary>
    ///     Sets the title (<c>setDiagramTitle</c>: sanitized).
    /// </summary>
    /// <param name="text">The title as written.</param>
    /// <param name="location">The location of the statement.</param>
    public void SetDiagramTitle(string text, MermaidLocation location)
    {
        DiagramTitle = Checked(Sanitize(text), location);
        TitleLocation = location;
    }

    /// <summary>
    ///     Sets the accessible title (Mermaid's <c>setAccTitle</c>: sanitized, leading white space removed).
    /// </summary>
    /// <param name="text">The title.</param>
    /// <param name="location">The location of the statement.</param>
    public void SetAccessibleTitle(string text, MermaidLocation location)
    {
        var sanitized = Sanitize(text);
        var start = 0;
        while (start < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[start]))
        {
            start++;
        }

        AccessibleTitle = Checked(sanitized[start..], location);
    }

    /// <summary>
    ///     Sets the accessible description (Mermaid's <c>setAccDescription</c>: sanitized, the white space after each line
    ///     feed removed).
    /// </summary>
    /// <param name="text">The description.</param>
    /// <param name="location">The location of the statement.</param>
    public void SetAccessibleDescription(string text, MermaidLocation location)
    {
        var sanitized = Sanitize(text);
        var builder = new StringBuilder(sanitized.Length);
        var i = 0;
        while (i < sanitized.Length)
        {
            builder.Append(sanitized[i]);
            i++;
            if (sanitized[i - 1] == '\n')
            {
                while (i < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[i]))
                {
                    i++;
                }
            }
        }

        AccessibleDescription = Checked(builder.ToString(), location);
    }

    /// <summary>
    ///     Starts a section (<c>addSection</c>).
    /// </summary>
    /// <param name="text">The section name as written.</param>
    public void AddSection(string text)
    {
        _currentSection = text;
        _sections.Add(text);
    }

    /// <summary>
    ///     Adds a task (<c>addTask</c> with <c>parseData</c>): its tags, id, start and end data, row and section.
    /// </summary>
    /// <param name="name">The name (the <c>taskTxt</c> token).</param>
    /// <param name="data">The data (the <c>taskData</c> token, with its colon).</param>
    /// <param name="location">The location of the task statement.</param>
    /// <exception cref="MermaidSyntaxException">Thrown where Mermaid fails: when the data holds only tags.</exception>
    public void AddTask(string name, string data, MermaidLocation location)
    {
        var fields = (data.StartsWith(':') ? data[1..] : data).Split(',').ToList();

        // getTaskTags: leading tags (/^\s*tag\s*$/) come off until none matches; a list that runs out is read past its end
        var tags = new HashSet<string>(StringComparer.Ordinal);
        var found = true;
        while (found)
        {
            found = false;
            foreach (var tag in TaskTags)
            {
                if (fields.Count == 0)
                {
                    throw new MermaidSyntaxException(
                        MermaidDiagnosticCodes.SyntaxError,
                        "Mermaid fails on a task whose data holds only tags (Cannot read properties of undefined (reading 'match'))",
                        location,
                        "give the task a start date or a duration after its tags, such as ':done, 2024-01-01, 3d'");
                }

                if (Preprocessor.TrimJs(fields[0]) == tag)
                {
                    tags.Add(tag);
                    fields.RemoveAt(0);
                    found = true;
                }
            }
        }

        for (var i = 0; i < fields.Count; i++)
        {
            fields[i] = Preprocessor.TrimJs(fields[i]);
        }

        string? id = null;
        var kind = GanttStartKind.None;
        string? start = null;
        string? end = null;
        switch (fields.Count)
        {
            case 1:
                id = NextId();
                kind = GanttStartKind.PreviousTaskEnd;
                end = fields[0];
                break;
            case 2:
                id = NextId();
                kind = GanttStartKind.StartDate;
                start = fields[0];
                end = fields[1];
                break;
            case 3:
                id = fields[0];
                kind = GanttStartKind.StartDate;
                start = fields[1];
                end = fields[2];
                break;
        }

        var vert = tags.Contains("vert");
        var task = new GanttRawTask
        {
            Section = _currentSection,
            Task = name,
            Id = id,
            PreviousTaskId = _lastTaskId,
            StartKind = kind,
            StartData = start,
            EndData = end,
            Active = tags.Contains("active"),
            Done = tags.Contains("done"),
            Crit = tags.Contains("crit"),
            Milestone = tags.Contains("milestone"),
            Vert = vert,
            Order = vert ? -1 : _lastOrder++,
            Location = location,
        };
        _tasks.Add(task);
        _lastTaskId = id;

        // taskDb[id] = position, a JavaScript object: undefined is the key "undefined", and __proto__ is never stored
        var key = id ?? "undefined";
        if (key != "__proto__")
        {
            _taskDb[key] = _tasks.Count - 1;
        }
    }

    /// <summary>
    ///     Adds the class <c>clickable</c> to the tasks of a click statement with a call (<c>setClickEvent</c>; the
    ///     callback itself is never bound).
    /// </summary>
    /// <param name="ids">The ids, separated by commas.</param>
    /// <param name="functionName">The function name.</param>
    /// <param name="functionArgs">The arguments, or null.</param>
    /// <param name="location">The location of the statement.</param>
    public void SetClickEvent(string ids, string functionName, string? functionArgs, MermaidLocation location)
    {
        _ = functionName;
        _ = functionArgs;
        SetClass(ids, location);
    }

    /// <summary>
    ///     Sets the link of the tasks of a click statement (<c>setLink</c>: the URL sanitized, then the class
    ///     <c>clickable</c>).
    /// </summary>
    /// <param name="ids">The ids, separated by commas.</param>
    /// <param name="url">The URL as written.</param>
    /// <param name="location">The location of the statement.</param>
    public void SetLink(string ids, string url, MermaidLocation location)
    {
        var link = Checked(UrlSanitizer.Sanitize(url), location);
        foreach (var id in ids.Split(','))
        {
            if (FindTask(id) is null)
            {
                continue;
            }

            var index = _links.FindIndex(entry => entry.Key == id);
            if (index >= 0)
            {
                _links[index] = new KeyValuePair<string, string>(id, link);
            }
            else
            {
                _links.Add(new KeyValuePair<string, string>(id, link));
            }
        }

        SetClass(ids, location);
    }

    /// <summary>
    ///     Finds a task by id (<c>findTaskById</c>): the last task with the id; a missing id (null) is the key
    ///     <c>undefined</c>, and <c>__proto__</c> is never found.
    /// </summary>
    /// <param name="id">The id, or null for undefined.</param>
    /// <returns>The task, or null.</returns>
    public GanttRawTask? FindTask(string? id) =>
        _taskDb.TryGetValue(id ?? "undefined", out var index) ? _tasks[index] : null;

    /// <summary>
    ///     Gets the index of a task by id, as <see cref="FindTask"/> finds it.
    /// </summary>
    /// <param name="id">The id, or null for undefined.</param>
    /// <returns>The index, or -1.</returns>
    internal int IndexOf(string? id) => _taskDb.TryGetValue(id ?? "undefined", out var index) ? index : -1;

    /// <summary>
    ///     Sanitizes a text as Mermaid's <c>sanitizeText</c> does (an empty text is kept).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sanitized text.</returns>
    internal static string Sanitize(string text) => text.Length == 0 ? text : HtmlSanitizer.Sanitize(text);

    /// <summary>
    ///     Merges the lower-cased tokens of a text into a list, each once (Mermaid's <c>mergeTokens</c>: split on
    ///     <c>/[\s,]+/</c>, empty tokens dropped).
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="text">The text.</param>
    private static void MergeTokens(List<string> list, string text)
    {
        var lower = text.ToLowerInvariant();
        var seen = new HashSet<string>(list, StringComparer.Ordinal);
        var i = 0;
        while (i < lower.Length)
        {
            while (i < lower.Length && (lower[i] == ',' || ScanPatterns.IsJsWhitespace(lower[i])))
            {
                i++;
            }

            var start = i;
            while (i < lower.Length && lower[i] != ',' && !ScanPatterns.IsJsWhitespace(lower[i]))
            {
                i++;
            }

            if (i > start && seen.Add(lower[start..i]))
            {
                list.Add(lower[start..i]);
            }
        }
    }

    /// <summary>
    ///     Gives the next id of a task without one (<c>parseId</c>).
    /// </summary>
    /// <returns>The id.</returns>
    private string NextId() => "task" + (++_taskCount).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Adds the class <c>clickable</c> to each task of a list of ids that exists (<c>setClass</c>), and checks the class
    ///     list Mermaid joins into the task text's class against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="ids">The ids, separated by commas.</param>
    /// <param name="location">The location of the statement.</param>
    private void SetClass(string ids, MermaidLocation location)
    {
        foreach (var id in ids.Split(','))
        {
            if (FindTask(id) is { } task)
            {
                task.Classes.Add("clickable");
                CheckLength((task.Classes.Count * ("clickable".Length + 1)) - 1, _limits, location);
            }
        }
    }

    /// <summary>
    ///     Checks a derived text against <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location.</param>
    /// <returns>The text.</returns>
    private string Checked(string text, MermaidLocation location)
    {
        CheckLength(text.Length, _limits, location);
        return text;
    }
}

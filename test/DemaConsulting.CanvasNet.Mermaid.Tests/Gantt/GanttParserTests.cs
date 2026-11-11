using DemaConsulting.CanvasNet.Mermaid.Gantt;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Gantt;

/// <summary>
///     Unit tests for <c>GanttParser</c>: the jison lexer rules in order with their quirks, the grammar with its located
///     errors and expected tokens, the statement values with their <c>substr</c> offsets, the click forms, Mermaid's own
///     failures, and the limits on every kept text.
/// </summary>
public sealed class GanttParserTests
{
    /// <summary>
    ///     Validates that every statement passes its value as the grammar's action does: the text after the keyword and
    ///     its one white-space character, trailing white space kept.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_Statements_KeepTheActionsSubstrings()
    {
        // Arrange
        const string Text = "gantt\ndateFormat  YYYY-MM-DD \naxisFormat %d \ntickInterval 1week\ntodayMarker stroke:red,opacity:0.5\n" +
            "excludes Weekends, 2024-01-01\nincludes 2024-01-06\nweekday monday\nweekend friday\ninclusiveEndDates\ntitle  A title \n";

        // Act
        var model = Parse(Text);

        // Assert
        Assert.Equal(" YYYY-MM-DD ", model.DateFormat);
        Assert.Equal("%d ", model.AxisFormat);
        Assert.Equal("1week", model.TickInterval);
        Assert.Equal("stroke:red,opacity:0.5", model.TodayMarker);
        Assert.Equal(["weekends", "2024-01-01"], model.Excludes);
        Assert.Equal(["2024-01-06"], model.Includes);
        Assert.Equal(("monday", "friday", true), (model.Weekday, model.Weekend, model.InclusiveEndDates));
        Assert.Equal(" A title ", model.DiagramTitle);
        Assert.Equal("A title", model.Title);
    }

    /// <summary>
    ///     Validates that keywords are case-insensitive and that a keyword's one white-space character may be a line feed.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_KeywordsAnyCaseAndLineFeedSeparator_AreRead()
    {
        // Act
        var model = Parse("gantt\nDATEFORMAT YYYY-MM-DD\nTitle\nNext line\nSECTION S\nA :a, 2024-01-01, 1d\n");

        // Assert
        Assert.Equal("YYYY-MM-DD", model.DateFormat);
        Assert.Equal("Next line", model.DiagramTitle);
        Assert.Equal(["S"], model.Sections);
    }

    /// <summary>
    ///     Validates the comment rules: <c>%%</c> and a single <c>%</c> start comments, and a line that starts with a
    ///     character followed by <c>%</c> is skipped with the line feed before it, but <c>}%</c> is not.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_CommentQuirks_FollowJisonRuleOrder()
    {
        // Act
        var model = Parse("gantt\n%% one\n% two\nA :a, 2024-01-01, 1d\nx% skipped\nB :b, after a, 1d\n");
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("gantt\n}% kept\n"));

        // Assert
        Assert.Equal(["a", "b"], model.Tasks.Select(t => t.Id));
        Assert.Equal(["taskData"], exception.Expected);
    }

    /// <summary>
    ///     Validates the task fields: the name up to the colon with its trailing space, the leading tags, and one, two or
    ///     three fields with generated ids.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_Tasks_SplitNameTagsAndFields()
    {
        // Act
        var model = Parse("gantt\nsection S\nFirst task :crit, done, a1, 2024-01-01, 3d\nSecond :2024-02-01, 1w\nThird :2d\nMarker :vert, 2024-01-02, 0d\n");

        // Assert
        var tasks = model.Tasks;
        Assert.Equal(["First task ", "Second ", "Third ", "Marker "], tasks.Select(t => t.Task));
        Assert.Equal(["a1", "task1", "task2", "task3"], tasks.Select(t => t.Id));
        Assert.True(tasks[0].Crit && tasks[0].Done && !tasks[0].Active);
        Assert.Equal((GanttStartKind.StartDate, "2024-01-01", "3d"), (tasks[0].StartKind, tasks[0].StartData, tasks[0].EndData));
        Assert.Equal((GanttStartKind.PreviousTaskEnd, "task1"), (tasks[2].StartKind, tasks[2].PreviousTaskId));
        Assert.Equal([0, 1, 2, -1], tasks.Select(t => t.Order));
        Assert.Equal((4, 0, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
    }

    /// <summary>
    ///     Validates the seven click forms: each adds <c>clickable</c> once per call to the tasks that exist, and a link is
    ///     sanitized and kept by id.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_ClickForms_AddClassesAndSanitizedLinks()
    {
        // Act
        var model = Parse(
            "gantt\nA :a, 2024-01-01, 1d\nB :b, 2024-01-01, 1d\nclick a href \"javascript:alert(1)\"\nclick b call cb(1, \"x\") href \"https://example.com\"\n" +
            "click a,b,zz call cb()\nclick b href \"https://example.com/b\" call cb()\n");

        // Assert
        Assert.Equal(["clickable", "clickable"], model.Tasks[0].Classes);
        Assert.Equal(["clickable", "clickable", "clickable", "clickable", "clickable"], model.Tasks[1].Classes);
        Assert.Equal([("a", "about:blank"), ("b", "https://example.com/b")], model.Links.Select(l => (l.Key, l.Value)));
    }

    /// <summary>
    ///     Validates the grammar errors: the token found and the expected tokens of the LALR state, in jison's order, at the
    ///     token's location.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_GrammarErrors_ListTheExpectedTokensOfJison()
    {
        // Act
        var noData = Assert.Throws<MermaidSyntaxException>(() => Parse("gantt\nA task\n"));
        var date = Assert.Throws<MermaidSyntaxException>(() => Parse("gantt\n  2024-01-01\n"));
        var click = Assert.Throws<MermaidSyntaxException>(() => Parse("gantt\nclick a\nB :1d\n"));
        var callAfter = Assert.Throws<MermaidSyntaxException>(() => Parse("gantt\nclick a call cb() call x\n"));

        // Assert
        Assert.Equal(["taskData"], noData.Expected);
        Assert.Contains("found 'NL'", noData.Message, StringComparison.Ordinal);
        Assert.Equal((2, 3), (date.Location.Line, date.Location.Column));
        Assert.Equal(27, date.Expected.Count);
        Assert.Equal(("EOF", "click"), (date.Expected[0], date.Expected[^1]));
        Assert.Equal(["callbackname", "href"], click.Expected);
        Assert.Equal("href", callAfter.Expected[^1]);
        Assert.Contains("found 'callbackname'", callAfter.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates Mermaid's own failures: the <c>topAxis</c> statement and a task whose data holds only tags fail, located,
    ///     with no expected tokens.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_MermaidsOwnFailures_FailLocated()
    {
        // Act
        var topAxis = Assert.Throws<MermaidSyntaxException>(() => Parse("gantt\n  topAxis\n"));
        var tags = Assert.Throws<MermaidSyntaxException>(() => Parse("gantt\nA :done, crit\n"));

        // Assert
        Assert.Equal((2, 3), (topAxis.Location.Line, topAxis.Location.Column));
        Assert.Contains("topAxis", topAxis.Message, StringComparison.Ordinal);
        Assert.Empty(topAxis.Expected);
        Assert.Equal(2, tags.Location.Line);
        Assert.Contains("only tags", tags.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the task and section statements together count against <see cref="MermaidLimits.MaxNodes"/>.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_TooManyTasksAndSections_ExceedsMaxNodes()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 3 };

        // Act
        var model = Parse("gantt\nsection A\nT1 :1d\nT2 :1d\n", limits);
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse("gantt\nsection A\nT1 :1d\nsection B\nT2 :1d\n", limits));

        // Assert
        Assert.Equal(2, model.Tasks.Count);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(5, exception.Location.Line);
        Assert.Contains("MaxNodes", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every kept text is checked against <see cref="MermaidLimits.MaxLabelLength"/>: the title, the
    ///     sections, task names and data, the formats, the excludes, the today marker, the accessibility texts and every
    ///     part of a click statement.
    /// </summary>
    /// <param name="template">The text, with <c>{0}</c> where the long text goes.</param>
    [Theory]
    [InlineData("gantt\ntitle {0}\n")]
    [InlineData("gantt\nsection {0}\n")]
    [InlineData("gantt\n{0} :1d\n")]
    [InlineData("gantt\nA :{0}\n")]
    [InlineData("gantt\ndateFormat {0}\n")]
    [InlineData("gantt\naxisFormat {0}\n")]
    [InlineData("gantt\ntickInterval {0}\n")]
    [InlineData("gantt\nexcludes {0}\n")]
    [InlineData("gantt\nincludes {0}\n")]
    [InlineData("gantt\ntodayMarker {0}\n")]
    [InlineData("gantt\naccTitle: {0}\n")]
    [InlineData("gantt\naccDescr: {0}\n")]
    [InlineData("gantt\naccDescr {{ {0} }}\n")]
    [InlineData("gantt\nclick {0} href \"x\"\n")]
    [InlineData("gantt\nclick a href \"{0}\"\n")]
    [InlineData("gantt\nclick a call {0}()\n")]
    [InlineData("gantt\nclick a call f({0})\n")]
    public void GanttParser_Parse_LongText_ExceedsMaxLabelLength(string template)
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 50 };
        var text = string.Format(System.Globalization.CultureInfo.InvariantCulture, template, new string('x', 60));

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Contains("MaxLabelLength", exception.Message, StringComparison.Ordinal);
        Assert.True(exception.Location.IsKnown);
    }

    /// <summary>
    ///     Validates that the class list a task's text joins grows with each click call and is checked against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>.
    /// </summary>
    [Fact]
    public void GanttParser_Parse_ManyClicks_ClassListExceedsMaxLabelLength()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 50 };
        var text = "gantt\nA :a, 2024-01-01, 1d\n" + string.Concat(Enumerable.Repeat("click a call f()\n", 6));

        // Act
        var exception = Assert.Throws<MermaidSyntaxException>(() => Parse(text, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(8, exception.Location.Line);
    }

    /// <summary>
    ///     Parses a gantt chart.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The model.</returns>
    internal static GanttModel Parse(string text, MermaidLimits? limits = null)
    {
        limits ??= MermaidLimits.Default;
        var source = Preprocessor.Run(text, null, limits);
        return GanttParser.Parse(source, DiagramDetector.Detect(source.Code, DetectorSettings.Default), limits);
    }
}

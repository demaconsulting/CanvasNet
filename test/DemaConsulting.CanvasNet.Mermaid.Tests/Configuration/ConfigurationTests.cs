using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

/// <summary>
///     Subsystem tests of the Configuration subsystem: the resolver, the themes, the khroma port and the style
///     declarations working together on whole diagram texts, and the configuration reaching the document's pipeline.
/// </summary>
public class ConfigurationTests
{
    /// <summary>
    ///     Validates a text that configures everything at once: the site theme is overridden by the frontmatter, a
    ///     directive overrides theme variables, <c>themeCSS</c> overrides a rule, and the problems are reported at their
    ///     locations.
    /// </summary>
    [Fact]
    public void Configuration_ResolveThemeAndStyles_FullText()
    {
        // Arrange
        const string Text = "---\nconfig:\n  theme: forest\n  flowchart:\n    curve: linear\n    rankSpacing: wide\n---\n" +
                            "%%{init: {'theme': 'dark', 'themeVariables': {'primaryColor': '#004400', 'lineColor': '#ff0000'}, 'themeCSS': '.cluster rect { stroke-dasharray: 5 }'}}%%\n" +
                            "flowchart LR\n  A --> B\n";
        var source = Preprocessor.Run(Text, "full.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var options = new MermaidRenderOptions { Theme = "neutral" };

        // Act
        var config = MermaidConfigResolver.Resolve(source, "flowchart-v2", options, sink);
        var styles = FlowchartStyles.Create(config, sink);
        var warnings = new List<MermaidDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal("dark", config.Theme);
        Assert.Equal("linear", config.Flowchart.Curve);
        Assert.Equal(50, config.Flowchart.RankSpacing);
        Assert.Equal("#1f2020", config.ThemeVariable("mainBkg"));
        Assert.Equal(CssColor.Parse("#ff0000"), styles.EdgeColor);
        Assert.Equal(CssColor.Parse("#ff0000"), styles.MarkerColor);
        Assert.Equal("5", styles.Resolve(".cluster rect")["stroke-dasharray"]);
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, new MermaidLocation("full.mmd", 6, 5)), (warning.Code, warning.Location));
    }

    /// <summary>
    ///     Validates a sequence diagram text that configures its section and its style at once: the frontmatter theme
    ///     and sequence settings take effect, a <c>themeCSS</c> rule of a sequence selector overrides the theme, the
    ///     participant paint comes from the theme, and a sequence value of the wrong type is reported at its key.
    /// </summary>
    [Fact]
    public void Configuration_ResolveSequenceSectionAndStyles_FullText()
    {
        // Arrange
        const string Text = "---\nconfig:\n  theme: forest\n  sequence:\n    mirrorActors: false\n    noteAlign: 5\n---\n" +
                            "%%{init: {'themeCSS': '.note { fill: #ff0000 }'}}%%\n" +
                            "sequenceDiagram\n  A->>B: x\n";
        var source = Preprocessor.Run(Text, "seq.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);

        // Act
        var config = MermaidConfigResolver.Resolve(source, "sequence", null, sink);
        var styles = SequenceStyles.Create(config, sink);
        var warnings = new List<MermaidDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal("forest", config.Theme);
        Assert.False(config.Sequence.MirrorActors);
        Assert.Equal("center", config.Sequence.NoteAlign);
        Assert.Equal(CssColor.Parse("#ff0000"), styles.Shape(new ShapePaint(null, null, 1), ".note").Fill);
        Assert.Equal(CssColor.Parse(config.ThemeVariable("actorBkg")!), styles.Shape(new ShapePaint(null, null, 1), ".actor").Fill);
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, 6), (warning.Code, warning.Location.Line));
    }

    /// <summary>
    ///     Validates a class diagram text that configures its section and its style at once: the class settings and the
    ///     state spacing the class renderer reads take effect, a <c>themeCSS</c> rule of a class selector overrides the
    ///     theme, the class box paint comes from the theme, and a class value of the wrong type is reported at its key.
    /// </summary>
    [Fact]
    public void Configuration_ResolveClassSectionAndStyles_FullText()
    {
        // Arrange
        const string Text = "---\nconfig:\n  theme: forest\n  class:\n    padding: 5\n    hideEmptyMembersBox: maybe\n  state:\n    nodeSpacing: 80\n---\n" +
                            "%%{init: {'themeCSS': '.divider { stroke: #ff0000 }'}}%%\n" +
                            "classDiagram\n  class A\n";
        var source = Preprocessor.Run(Text, "class.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);

        // Act
        var config = MermaidConfigResolver.Resolve(source, "classDiagram", null, sink);
        var styles = ClassStyles.Create(config, sink);
        var warnings = new List<MermaidDiagnostic>();
        sink.Deliver(warnings.Add, null);

        // Assert
        Assert.Equal("forest", config.Theme);
        Assert.Equal(((double?)5, false, 80.0), (config.Class.Padding, config.Class.HideEmptyMembersBox, config.Class.NodeSpacing));
        Assert.Equal(CssColor.Parse("#ff0000"), styles.Divider.Stroke);
        Assert.Equal(CssColor.Parse(config.ThemeVariable("mainBkg")!), styles.Node.Fill);
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, 6), (warning.Code, warning.Location.Line));
    }

    /// <summary>
    ///     Validates a state diagram text and an entity relationship diagram text that each configure their section and
    ///     their style at once: the section's settings take effect, a <c>themeCSS</c> rule of the diagram's own selector
    ///     overrides the theme without a report, the node paint comes from the theme, neither sheet centers the SVG text
    ///     of a node, and a section value of the wrong type is reported at its key.
    /// </summary>
    [Fact]
    public void Configuration_ResolveStateAndErSectionsAndStyles_FullText()
    {
        // Arrange
        const string StateText = "---\nconfig:\n  theme: forest\n  state:\n    rankSpacing: 70\n    titleTopMargin: yes\n---\n" +
                                 "%%{init: {'themeCSS': '.transition { stroke: #ff0000 }'}}%%\n" +
                                 "stateDiagram-v2\n  A --> B\n";
        const string ErText = "---\nconfig:\n  theme: dark\n  er:\n    minEntityWidth: 30\n    entityPadding: wide\n---\n" +
                              "%%{init: {'themeCSS': '.relationshipLine { stroke: #0000ff }'}}%%\n" +
                              "erDiagram\n  A ||--o{ B : has\n";
        var stateSink = new MermaidReportSink(100, throwOnUnsupported: false);
        var erSink = new MermaidReportSink(100, throwOnUnsupported: false);

        // Act
        var stateConfig = MermaidConfigResolver.Resolve(Preprocessor.Run(StateText, "state.mmd", MermaidLimits.Default), "stateDiagram", null, stateSink);
        var stateStyles = StateStyles.Create(stateConfig, stateSink);
        var erConfig = MermaidConfigResolver.Resolve(Preprocessor.Run(ErText, "er.mmd", MermaidLimits.Default), "er", null, erSink);
        var erStyles = ErStyles.Create(erConfig, erSink);
        var stateWarnings = new List<MermaidDiagnostic>();
        var erWarnings = new List<MermaidDiagnostic>();
        var unsupported = new List<MermaidUnsupportedFeature>();
        stateSink.Deliver(stateWarnings.Add, unsupported.Add);
        erSink.Deliver(erWarnings.Add, unsupported.Add);

        // Assert
        Assert.Equal((70.0, 25.0), (stateConfig.State.RankSpacing, stateConfig.State.TitleTopMargin));
        Assert.Equal(CssColor.Parse("#ff0000"), stateStyles.Resolve(".transition").Paint("stroke"));
        Assert.Equal(CssColor.Parse(stateConfig.ThemeVariable("mainBkg")!), stateStyles.Node.Fill);
        Assert.Equal((30.0, 15.0), (erConfig.Er.MinEntityWidth, erConfig.Er.EntityPadding));
        Assert.Equal(CssColor.Parse("#0000ff"), erStyles.Resolve(".relationshipLine").Paint("stroke"));
        Assert.Equal(CssColor.Parse(erConfig.ThemeVariable("mainBkg")!), erStyles.Node.Fill);
        Assert.Equal((false, false, 16.0), (stateStyles.CentersNodeSvgText, erStyles.CentersNodeSvgText, erStyles.RootText.FontSize));
        Assert.Empty(unsupported);
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, 6), (Assert.Single(stateWarnings).Code, stateWarnings[0].Location.Line));
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, 6), (Assert.Single(erWarnings).Code, erWarnings[0].Location.Line));
    }

    /// <summary>
    ///     Validates a pie chart text and a gantt chart text that each configure their section and their style at once:
    ///     the section's settings take effect, a <c>themeCSS</c> rule of the chart's own selector overrides the theme
    ///     without a report, the theme's colors reach the style sheet, and a section value of the wrong type is reported at
    ///     its key.
    /// </summary>
    [Fact]
    public void Configuration_ResolvePieAndGanttSectionsAndStyles_FullText()
    {
        // Arrange
        const string PieText = "---\nconfig:\n  theme: forest\n  pie:\n    donutHole: 0.4\n    textPosition: far\n---\n" +
                               "%%{init: {'themeCSS': '.pieOuterCircle { stroke: #ff0000 }'}}%%\n" +
                               "pie\n  \"a\" : 1\n";
        const string GanttText = "---\nconfig:\n  theme: dark\n  gantt:\n    barHeight: 30\n    barGap: wide\n---\n" +
                                 "%%{init: {'themeCSS': '.task0 { fill: #0000ff }'}}%%\n" +
                                 "gantt\n  A :1d\n";
        var pieSink = new MermaidReportSink(100, throwOnUnsupported: false);
        var ganttSink = new MermaidReportSink(100, throwOnUnsupported: false);

        // Act
        var pieConfig = MermaidConfigResolver.Resolve(Preprocessor.Run(PieText, "pie.mmd", MermaidLimits.Default), "pie", null, pieSink);
        var pieStyles = PieStyles.Create(pieConfig, pieSink);
        var ganttConfig = MermaidConfigResolver.Resolve(Preprocessor.Run(GanttText, "gantt.mmd", MermaidLimits.Default), "gantt", null, ganttSink);
        var ganttStyles = GanttStyles.Create(ganttConfig, ganttSink);
        var bar = ganttStyles.Compute([new GanttElement("svg", []), new GanttElement("g", []), GanttElement.Of("rect", "task task0")]);
        var pieWarnings = new List<MermaidDiagnostic>();
        var ganttWarnings = new List<MermaidDiagnostic>();
        var unsupported = new List<MermaidUnsupportedFeature>();
        pieSink.Deliver(pieWarnings.Add, unsupported.Add);
        ganttSink.Deliver(ganttWarnings.Add, unsupported.Add);

        // Assert
        Assert.Equal((0.4, 0.75), (pieConfig.Pie.DonutHole, pieConfig.Pie.TextPosition));
        Assert.Equal(CssColor.Parse("#ff0000"), pieStyles.OuterCircle.Stroke);
        Assert.Equal((30.0, 4.0), (ganttConfig.Gantt.BarHeight, ganttConfig.Gantt.BarGap));
        Assert.Equal(CssColor.Parse("#0000ff"), bar.Fill);
        Assert.Empty(unsupported);
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, 6), (Assert.Single(pieWarnings).Code, pieWarnings[0].Location.Line));
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, 6), (Assert.Single(ganttWarnings).Code, ganttWarnings[0].Location.Line));
    }

    /// <summary>
    ///     Validates that each of the five themes gives the flowchart its node fill (the theme's <c>mainBkg</c>) through
    ///     the site theme option.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="fill">The expected node fill.</param>
    [Theory]
    [InlineData("default", "#ECECFF")]
    [InlineData("base", "#fff4dd")]
    [InlineData("dark", "#1f2020")]
    [InlineData("forest", "#cde498")]
    [InlineData("neutral", "#eee")]
    public void Configuration_ThemeOption_NodeFill(string theme, string fill)
    {
        // Arrange
        var source = Preprocessor.Run(MermaidTestHelpers.FlowchartText, null, MermaidLimits.Default);

        // Act
        var config = MermaidConfigResolver.Resolve(source, "flowchart-v2", new MermaidRenderOptions { Theme = theme }, null);
        var styles = FlowchartStyles.Create(config, null);

        // Assert
        Assert.Equal(CssColor.Parse(fill), styles.Node.Fill);
    }

    /// <summary>
    ///     Validates that the text's configuration chooses the detector: a flowchart whose default renderer is ELK is
    ///     detected as Mermaid's ELK flowchart, which is still drawn as a flowchart.
    /// </summary>
    [Fact]
    public void Configuration_Document_TextConfigurationDrivesDetection()
    {
        // Arrange
        const string Text = "---\nconfig:\n  flowchart:\n    defaultRenderer: elk\n---\nflowchart LR\n  A --> B\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var plain = MermaidDocument.Parse(MermaidTestHelpers.FlowchartText);

        // Assert
        Assert.Equal(("flowchart-elk", "flowchart", true), (document.Detected.MermaidId, document.DiagramType, document.IsSupported));
        Assert.Equal("flowchart-v2", plain.Detected.MermaidId);
    }

    /// <summary>
    ///     Validates that the configuration problems of a drawn type are delivered by a render call, located.
    /// </summary>
    [Fact]
    public void Configuration_Document_RenderDeliversConfigurationWarnings()
    {
        // Arrange
        const string Text = "---\nconfig:\n  securityLevel: loose\n---\nflowchart LR\n  A --> B\n";
        var document = MermaidDocument.Parse(Text, new MermaidOpenOptions { SourceName = "w.mmd" });
        var (options, warnings, _) = MermaidTestHelpers.Collecting();

        // Act: the delivery happens whether or not the flowchart is drawn by this build
        try
        {
            document.Render(options, TestContext.Current.CancellationToken).Dispose();
        }
        catch (MermaidRenderException)
        {
            // Drawing may not be available yet; the reports are delivered either way
        }

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.ConfigSecureKey, new MermaidLocation("w.mmd", 3, 3)), (warning.Code, warning.Location));
    }

    /// <summary>
    ///     Validates that the configuration of a type that is not drawn is not reported: its only report is that it is not
    ///     drawn.
    /// </summary>
    [Fact]
    public void Configuration_Document_UnsupportedType_ConfigurationSilent()
    {
        // Arrange
        const string Text = "---\nconfig:\n  securityLevel: loose\n  notAKey: 1\n---\njourney\n  title x\n";
        var document = MermaidDocument.Parse(Text);
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        using var surface = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(warnings);
        Assert.Equal(MermaidDiagnosticCodes.DiagramUnsupported, Assert.Single(unsupported).Feature);
    }
}

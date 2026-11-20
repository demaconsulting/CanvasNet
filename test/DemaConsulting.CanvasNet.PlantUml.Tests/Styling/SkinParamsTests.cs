using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Styling;

/// <summary>
///     Unit tests for the <c>SkinParams</c> unit: PlantUML's colors, the line and block forms, the supported subset, and
///     the reports of what is ignored.
/// </summary>
public class SkinParamsTests
{
    /// <summary>
    ///     Gets the statements of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The non-empty statements.</returns>
    private static List<SourceStatement> Statements(string text) =>
        [.. Preprocessor.Run(text, null, PlantUmlLimits.Default).Block.Statements.Where(s => s.Text.Length > 0)];

    /// <summary>
    ///     Reads every skin parameter of a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The entries.</returns>
    private static List<SkinParamEntry> Read(string text)
    {
        var statements = Statements(text);
        var entries = new List<SkinParamEntry>();
        for (var i = 0; i < statements.Count; i++)
        {
            Assert.True(SkinParams.TryRead(statements, ref i, entries));
        }

        return entries;
    }

    /// <summary>
    ///     Validates PlantUML's color forms.
    /// </summary>
    [Fact]
    public void PlantUmlSkinParams_TryParseColor_ReadsPlantUmlForms()
    {
        // Arrange / Act / Assert
        Assert.True(SkinParams.TryParseColor("LightBlue", out var named));
        Assert.Equal(new Rgba32(0xAD, 0xD8, 0xE6, 255), named);
        Assert.True(SkinParams.TryParseColor("#red", out var hashed));
        Assert.Equal(new Rgba32(255, 0, 0, 255), hashed);
        Assert.True(SkinParams.TryParseColor("#1A2B3C", out var six));
        Assert.Equal(new Rgba32(0x1A, 0x2B, 0x3C, 255), six);
        Assert.True(SkinParams.TryParseColor("#abc", out var three));
        Assert.Equal(new Rgba32(0xAA, 0xBB, 0xCC, 255), three);
        Assert.True(SkinParams.TryParseColor("#8", out var gray));
        Assert.Equal(new Rgba32(0x88, 0x88, 0x88, 255), gray);
        Assert.True(SkinParams.TryParseColor("transparent", out var clear));
        Assert.Equal(0, clear.A);
        Assert.True(SkinParams.TryParseColor("#red/blue", out var gradient));
        Assert.Equal(new Rgba32(255, 0, 0, 255), gradient);
        Assert.False(SkinParams.TryParseColor("not a color", out _));
        Assert.False(SkinParams.TryParseColor(" ", out _));
    }

    /// <summary>
    ///     Validates the line and block forms of the statement.
    /// </summary>
    [Fact]
    public void PlantUmlSkinParams_TryRead_LineAndBlockForms()
    {
        // Arrange / Act
        var entries = Read("skinparam backgroundColor #EEE\nskinparam participant {\n  BackgroundColor red\n  FontSize 18\n}\n");

        // Assert
        Assert.Equal(["backgroundColor", "participantBackgroundColor", "participantFontSize"], entries.Select(e => e.Name));
        Assert.Equal(["#EEE", "red", "18"], entries.Select(e => e.Value));
        Assert.Equal([1, 3, 4], entries.Select(e => e.Location.Line));
    }

    /// <summary>
    ///     Validates the supported subset: document settings, sequence settings and element properties.
    /// </summary>
    [Fact]
    public void PlantUmlSkinParams_Apply_SupportedSubset_ChangesTheme()
    {
        // Arrange
        var entries = Read(string.Join('\n',
            "skinparam backgroundColor transparent",
            "skinparam defaultFontSize 18",
            "skinparam sequenceArrowThickness 2",
            "skinparam maxMessageSize 100",
            "skinparam footbox hide",
            "skinparam responseMessageBelowArrow true",
            "skinparam sequenceMessageAlign center",
            "skinparam ParticipantBackgroundColor #ABCDEF",
            "skinparam sequenceGroupBackgroundColor yellow",
            "skinparam noteBorderThickness 2",
            "skinparam arrowColor blue",
            "skinparam lifeLineBackgroundColor gold",
            "skinparam titleFontStyle italic",
            string.Empty));
        var reports = new List<PlantUmlPendingReport>();

        // Act
        var theme = SkinParams.Apply(SkinTheme.Default, entries, reports);

        // Assert
        Assert.Empty(reports);
        Assert.Null(theme.Background);
        Assert.Equal((18.0, 13.0), (theme[SkinElement.Participant].FontSize, theme[SkinElement.Note].FontSize));
        Assert.Equal((2.0, 100.0, false, true, "center"), (theme.ArrowThickness, theme.MaxMessageSize, theme.Footbox, theme.ResponseBelowArrow, theme.MessageAlign));
        Assert.Equal(new Rgba32(0xAB, 0xCD, 0xEF, 255), theme[SkinElement.Participant].Fill);
        Assert.Equal(new Rgba32(255, 255, 0, 255), theme[SkinElement.GroupHeader].Fill);
        Assert.Null(theme[SkinElement.Group].Fill);
        Assert.Equal(2, theme[SkinElement.Note].LineWidth);
        Assert.Equal(new Rgba32(0, 0, 255, 255), theme[SkinElement.Arrow].Line);
        Assert.Equal(new Rgba32(255, 215, 0, 255), theme[SkinElement.ActivationBox].Fill);
        Assert.True(theme[SkinElement.Title].Italic);
    }

    /// <summary>
    ///     Validates that an unknown parameter, a value that cannot be read, and a stereotyped parameter are reported as
    ///     warnings at their lines and have no effect.
    /// </summary>
    [Fact]
    public void PlantUmlSkinParams_Apply_Ignored_ReportsWarnings()
    {
        // Arrange
        var entries = Read("skinparam monochrome true\nskinparam noteFontSize big\nskinparam participant<<db>>BackgroundColor red\n");
        var reports = new List<PlantUmlPendingReport>();

        // Act
        var theme = SkinParams.Apply(SkinTheme.Default, entries, reports);

        // Assert
        Assert.Equal(3, reports.Count);
        Assert.All(reports, r => Assert.Equal((true, PlantUmlDiagnosticCodes.SkinParamIgnored), (r.IsWarning, r.Code)));
        Assert.Equal([1, 2, 3], reports.Select(r => r.Location.Line));
        Assert.Equal(SkinTheme.Default[SkinElement.Note], theme[SkinElement.Note]);
    }

    /// <summary>
    ///     Validates the graph subset: the class, object, map, package, and member prefixes and the class settings are
    ///     applied in a class diagram, <c>arrow</c> styles its links too, and the same parameters are reported as
    ///     ignored in a sequence or activity diagram.
    /// </summary>
    [Fact]
    public void PlantUmlSkinParams_Apply_GraphSubset_OnlyInGraphDiagrams()
    {
        // Arrange
        var entries = new List<SkinParamEntry>
        {
            new("classBackgroundColor", "#FFEEDD", default),
            new("classStereotypeFontSize", "10", default),
            new("classAttributeFontColor", "blue", default),
            new("objectBorderColor", "red", default),
            new("packageBorderThickness", "2", default),
            new("arrowColor", "green", default),
            new("classAttributeIconSize", "0", default),
            new("circledCharacterRadius", "9", default),
        };
        var graphReports = new List<DemaConsulting.CanvasNet.PlantUml.PlantUmlPendingReport>();
        var sequenceReports = new List<DemaConsulting.CanvasNet.PlantUml.PlantUmlPendingReport>();

        // Act
        var graph = SkinParams.Apply(SkinTheme.Default, entries, graphReports, graph: true);
        var sequence = SkinParams.Apply(SkinTheme.Default, entries, sequenceReports);

        // Assert
        Assert.Empty(graphReports);
        Assert.Equal(new Rgba32(0xFF, 0xEE, 0xDD, 255), graph[SkinElement.Class].Fill);
        Assert.Equal(10, graph[SkinElement.ClassStereotype].FontSize);
        Assert.Equal(new Rgba32(0, 0, 0xFF, 255), graph[SkinElement.ClassAttribute].FontColor);
        Assert.Equal(new Rgba32(0xFF, 0, 0, 255), graph[SkinElement.Object].Line);
        Assert.Equal(2, graph[SkinElement.Package].LineWidth);
        Assert.Equal(new Rgba32(0, 0x80, 0, 255), graph[SkinElement.SvekArrow].Line);
        Assert.Equal((0.0, 9.0), (graph.ClassAttributeIconSize, graph.CircledCharacterRadius));
        Assert.Equal(7, sequenceReports.Count);
        Assert.All(sequenceReports, r => Assert.Equal(PlantUmlDiagnosticCodes.SkinParamIgnored, r.Code));
        Assert.Equal(new Rgba32(0, 0x80, 0, 255), sequence[SkinElement.Arrow].Line);
    }

    /// <summary>
    ///     Validates the description subset: the prefixes of the description symbols (component, interface, node,
    ///     rectangle, folder, frame, cloud, card) style their elements in a description diagram, a graph prefix of the same
    ///     diagram still applies, and a class diagram reports the description prefixes as ignored.
    /// </summary>
    [Fact]
    public void PlantUmlSkinParams_Apply_DescriptionSubset_OnlyInDescriptionDiagrams()
    {
        // Arrange
        var entries = new List<SkinParamEntry>
        {
            new("componentBackgroundColor", "#DDEEFF", default),
            new("componentBorderColor", "#336699", default),
            new("interfaceBackgroundColor", "#FFFFCC", default),
            new("nodeFontColor", "blue", default),
            new("rectangleBorderThickness", "2", default),
            new("folderBackgroundColor", "#EEEEEE", default),
            new("frameFontSize", "12", default),
            new("cloudBorderColor", "red", default),
            new("cardBackgroundColor", "white", default),
            new("databaseBackgroundColor", "#ABCDEF", default),
            new("packageBorderColor", "green", default),
        };
        var descriptionReports = new List<DemaConsulting.CanvasNet.PlantUml.PlantUmlPendingReport>();
        var classReports = new List<DemaConsulting.CanvasNet.PlantUml.PlantUmlPendingReport>();

        // Act
        var description = SkinParams.Apply(SkinTheme.Description, entries, descriptionReports, graph: true, description: true);
        SkinParams.Apply(SkinTheme.Default, entries, classReports, graph: true);

        // Assert
        Assert.Equal((new Rgba32(0xDD, 0xEE, 0xFF, 255), new Rgba32(0x33, 0x66, 0x99, 255)), (description[SkinElement.Component].Fill!.Value, description[SkinElement.Component].Line!.Value));
        Assert.Equal(new Rgba32(0xFF, 0xFF, 0xCC, 255), description[SkinElement.Interface].Fill);
        Assert.Equal(new Rgba32(0, 0, 0xFF, 255), description[SkinElement.Node].FontColor);
        Assert.Equal(2, description[SkinElement.Rectangle].LineWidth);
        Assert.Equal(new Rgba32(0xEE, 0xEE, 0xEE, 255), description[SkinElement.Folder].Fill);
        Assert.Equal(12, description[SkinElement.Frame].FontSize);
        Assert.Equal(new Rgba32(0xFF, 0, 0, 255), description[SkinElement.Cloud].Line);
        Assert.Equal(SkinTheme.White, description[SkinElement.Card].Fill);
        Assert.Equal(new Rgba32(0xAB, 0xCD, 0xEF, 255), description[SkinElement.Database].Fill);
        Assert.Equal(new Rgba32(0, 0x80, 0, 255), description[SkinElement.Package].Line);
        Assert.Empty(descriptionReports);
        Assert.Equal(9, classReports.Count);
    }

    /// <summary>
    ///     Validates the state subset of the graph diagrams: the state and state attribute prefixes, and the start and
    ///     end colors (one color for the fill and the line); a sequence diagram reports them as ignored.
    /// </summary>
    [Fact]
    public void PlantUmlSkinParams_Apply_StateSubset_OnlyInGraphDiagrams()
    {
        // Arrange
        var entries = new List<SkinParamEntry>
        {
            new("stateBackgroundColor", "#E0F0FF", default),
            new("stateBorderColor", "#3060A0", default),
            new("stateFontSize", "12", default),
            new("stateAttributeFontColor", "blue", default),
            new("stateStartColor", "green", default),
            new("stateEndColor", "#FF0000", default),
            new("stateEndColor", "notacolor", default),
        };
        var graphReports = new List<DemaConsulting.CanvasNet.PlantUml.PlantUmlPendingReport>();
        var sequenceReports = new List<DemaConsulting.CanvasNet.PlantUml.PlantUmlPendingReport>();

        // Act
        var graph = SkinParams.Apply(SkinTheme.Default, entries, graphReports, graph: true);
        SkinParams.Apply(SkinTheme.Default, entries, sequenceReports);

        // Assert
        Assert.Equal((new Rgba32(0xE0, 0xF0, 0xFF, 255), new Rgba32(0x30, 0x60, 0xA0, 255), 12.0), (graph[SkinElement.State].Fill!.Value, graph[SkinElement.State].Line!.Value, graph[SkinElement.State].FontSize));
        Assert.Equal(new Rgba32(0, 0, 0xFF, 255), graph[SkinElement.StateAttribute].FontColor);
        Assert.Equal((new Rgba32(0, 0x80, 0, 255), new Rgba32(0, 0x80, 0, 255)), (graph[SkinElement.StateStart].Fill!.Value, graph[SkinElement.StateStart].Line!.Value));
        Assert.Equal((new Rgba32(0xFF, 0, 0, 255), new Rgba32(0xFF, 0, 0, 255)), (graph[SkinElement.StateEnd].Fill!.Value, graph[SkinElement.StateEnd].Line!.Value));
        Assert.Single(graphReports);
        Assert.Equal(7, sequenceReports.Count);
    }

    /// <summary>
    ///     Validates that a style block is reported once and skipped to its end.
    /// </summary>
    [Fact]
    public void PlantUmlSkinParams_TrySkipStyle_ReportsAndSkips()
    {
        // Arrange
        var statements = Statements("<style>\nroot {\n  FontSize 20\n}\n</style>\nA -> B\n");
        var reports = new List<PlantUmlPendingReport>();
        var index = 0;

        // Act
        var skipped = SkinParams.TrySkipStyle(statements, ref index, reports);

        // Assert
        Assert.True(skipped);
        Assert.Equal("</style>", statements[index].Text);
        Assert.Equal(PlantUmlDiagnosticCodes.StyleUnsupported, Assert.Single(reports).Code);
    }
}

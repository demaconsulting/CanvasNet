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

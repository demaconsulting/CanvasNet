using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Styling;

/// <summary>
///     Subsystem tests for the <c>Styling</c> subsystem: the skin parameters of a diagram applied over the default look.
/// </summary>
public class StylingTests
{
    /// <summary>
    ///     Validates that the skin parameters of a diagram, in both forms, change the default look and that what is
    ///     ignored is reported.
    /// </summary>
    [Fact]
    public void PlantUmlStyling_SkinParamsOverDefaultLook_GiveDiagramLook()
    {
        // Arrange
        const string text = "@startuml\nskinparam note {\n  BackgroundColor #FFAAAA\n  FontColor white\n}\nskinparam shadowing false\nskinparam handwritten true\nA -> B\n@enduml\n";
        var statements = Preprocessor.Run(text, null, PlantUmlLimits.Default).Block.Statements.Where(s => s.Text.Length > 0).ToList();
        var entries = new List<SkinParamEntry>();
        for (var i = 0; i < statements.Count; i++)
        {
            SkinParams.TryRead(statements, ref i, entries);
        }

        var reports = new List<PlantUmlPendingReport>();

        // Act
        var theme = SkinParams.Apply(SkinTheme.Default, entries, reports);

        // Assert
        Assert.Equal(new Canvas.Rgba32(0xFF, 0xAA, 0xAA, 255), theme[SkinElement.Note].Fill);
        Assert.Equal(new Canvas.Rgba32(255, 255, 255, 255), theme[SkinElement.Note].FontColor);
        Assert.False(theme.Shadowing);
        Assert.Equal(SkinTheme.Default[SkinElement.Participant], theme[SkinElement.Participant]);
        Assert.Equal(7, Assert.Single(reports).Location.Line);
    }
}

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Styling;

/// <summary>
///     Unit tests for the <c>SkinTheme</c> unit: the values of PlantUML's default skin and the immutable changes.
/// </summary>
public class SkinThemeTests
{
    /// <summary>
    ///     Validates the default look of the sequence elements.
    /// </summary>
    [Fact]
    public void PlantUmlSkinTheme_Default_HasPlantUmlSequenceLook()
    {
        // Arrange
        var theme = SkinTheme.Default;

        // Act
        var participant = theme[SkinElement.Participant];
        var actor = theme[SkinElement.Actor];

        // Assert
        Assert.Equal(("SansSerif", 14.0, SkinColors.GreyBlue), (participant.FontFamily, participant.FontSize, participant.Fill!.Value));
        Assert.Equal((new Rgba32(0x18, 0x18, 0x18, 255), 0.5, 5.0, 7.0), (participant.Line!.Value, participant.LineWidth, participant.RoundCorner, participant.Padding));
        Assert.Equal(0, actor.RoundCorner);
        Assert.Equal((13.0, SkinColors.Note, 0.5), (theme[SkinElement.Note].FontSize, theme[SkinElement.Note].Fill!.Value, theme[SkinElement.Note].LineWidth));
        Assert.Equal((13.0, 1.0), (theme[SkinElement.Arrow].FontSize, theme[SkinElement.Arrow].LineWidth));
        Assert.Equal((SkinColors.Black, 1.5, 11.0, true), (theme[SkinElement.Group].Line!.Value, theme[SkinElement.Group].LineWidth, theme[SkinElement.Group].FontSize, theme[SkinElement.Group].Bold));
        Assert.Equal([5.0, 5.0], theme[SkinElement.LifeLine].Dash);
        Assert.Equal((10.0, new Rgba32(0x88, 0x88, 0x88, 255)), (theme[SkinElement.Header].FontSize, theme[SkinElement.Header].FontColor));
        Assert.True(theme[SkinElement.Title].Bold);
        Assert.Equal(new Rgba32(255, 255, 255, 255), theme.Background);
        Assert.True(theme.Footbox);
        Assert.Equal(1, theme.ArrowThickness);
    }

    /// <summary>
    ///     Validates the default look of the activity elements.
    /// </summary>
    [Fact]
    public void PlantUmlSkinTheme_Default_HasPlantUmlActivityLook()
    {
        // Arrange
        var theme = SkinTheme.Default;

        // Act / Assert
        Assert.Equal((12.0, new Rgba32(0xF1, 0xF1, 0xF1, 255), 25.0), (theme[SkinElement.Activity].FontSize, theme[SkinElement.Activity].Fill!.Value, theme[SkinElement.Activity].RoundCorner));
        Assert.Equal(18, theme[SkinElement.Swimlane].FontSize);
        Assert.Equal(new Rgba32(0x55, 0x55, 0x55, 255), theme[SkinElement.ActivityBar].Fill);
    }

    /// <summary>
    ///     Validates the default look of the links and clusters of the diagrams that PlantUML lays out with Graphviz.
    /// </summary>
    [Fact]
    public void PlantUmlSkinTheme_Default_HasPlantUmlGraphLook()
    {
        // Arrange
        var theme = SkinTheme.Default;

        // Act / Assert
        Assert.Equal((13.0, SkinTheme.LineColor, 1.0), (theme[SkinElement.SvekArrow].FontSize, theme[SkinElement.SvekArrow].Line!.Value, theme[SkinElement.SvekArrow].LineWidth));
        Assert.Equal((SkinTheme.Black, 1.5, 5.0, true), (theme[SkinElement.Package].Line!.Value, theme[SkinElement.Package].LineWidth, theme[SkinElement.Package].RoundCorner, theme[SkinElement.Package].Bold));
        Assert.Equal((SkinTheme.LineColor, 1.0, 5.0, true), (theme[SkinElement.Container].Line!.Value, theme[SkinElement.Container].LineWidth, theme[SkinElement.Container].RoundCorner, theme[SkinElement.Container].Bold));
        Assert.Null(theme[SkinElement.Package].Fill);
        Assert.Null(theme[SkinElement.Container].Fill);
    }

    /// <summary>
    ///     Validates the default look of the class and object boxes: light grey, outlined half a pixel wide with corners
    ///     of 5, stereotypes in italic 12, members in black 14, visibility icons of 10, and circles of radius 11.
    /// </summary>
    [Fact]
    public void PlantUmlSkinTheme_Default_HasPlantUmlClassLook()
    {
        // Arrange
        var theme = SkinTheme.Default;

        // Act / Assert
        foreach (var element in new[] { SkinElement.Class, SkinElement.Object })
        {
            Assert.Equal((SkinTheme.CommonBackground, SkinTheme.LineColor, 0.5, 5.0, 14.0), (theme[element].Fill!.Value, theme[element].Line!.Value, theme[element].LineWidth, theme[element].RoundCorner, theme[element].FontSize));
        }

        Assert.Equal((12.0, true), (theme[SkinElement.ClassStereotype].FontSize, theme[SkinElement.ClassStereotype].Italic));
        Assert.Equal((14.0, SkinTheme.Black), (theme[SkinElement.ClassAttribute].FontSize, theme[SkinElement.ClassAttribute].FontColor));
        Assert.Equal((10.0, 11.0), (theme.ClassAttributeIconSize, theme.CircledCharacterRadius));
    }

    /// <summary>
    ///     Validates that a change returns a new theme and leaves the default unchanged.
    /// </summary>
    [Fact]
    public void PlantUmlSkinTheme_With_ReturnsChangedCopy()
    {
        // Arrange
        var theme = SkinTheme.Default;

        // Act
        var changed = theme.With(SkinElement.Note, s => s with { FontSize = 20 });

        // Assert
        Assert.Equal(20, changed[SkinElement.Note].FontSize);
        Assert.Equal(13, theme[SkinElement.Note].FontSize);
        Assert.Equal(theme[SkinElement.Participant], changed[SkinElement.Participant]);
    }
}

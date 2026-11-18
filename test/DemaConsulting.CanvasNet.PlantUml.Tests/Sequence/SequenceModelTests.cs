using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Sequence;

/// <summary>
///     Unit tests for the <c>SequenceModel</c> unit: the facts of a sequence diagram, its reports, and its layout entry
///     point.
/// </summary>
public class SequenceModelTests
{
    /// <summary>
    ///     Validates the facts: participants are nodes, messages are edges, boxes are subgraphs, and the title.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceModel_Facts_CountParticipantsMessagesAndBoxes()
    {
        // Act
        var model = SequenceTestHelpers.Parse("title T\nbox X\nparticipant A\nend box\nA -> B\nB -> B\nnote over A : n");

        // Assert
        Assert.Equal("T", model.Title);
        Assert.Equal(2, model.NodeCount);
        Assert.Equal(2, model.EdgeCount);
        Assert.Equal(1, model.SubgraphCount);
        Assert.True(model.Events.OfType<SequenceMessage>().Last().IsSelf);
    }

    /// <summary>
    ///     Validates that a model keeps the reports of its parser in text order.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceModel_Reports_KeepParserReportsInOrder()
    {
        // Act
        var model = SequenceTestHelpers.Parse("skinparam unknownThing 3\nnewpage\nA -> B");

        // Assert
        Assert.Equal(
            [PlantUmlDiagnosticCodes.SkinParamIgnored, PlantUmlDiagnosticCodes.StatementUnsupported],
            model.Reports.Select(r => r.Code));
    }

    /// <summary>
    ///     Validates that the layout gives a scene with the diagram's background, its own scale, and the page texts.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceModel_Layout_GivesSceneWithBackgroundScaleAndPage()
    {
        // Arrange
        var model = SequenceTestHelpers.Parse("skinparam backgroundColor #EEEEEE\nscale 2\ntitle Hello\nA -> B");

        // Act
        var scene = model.Layout(SequenceTestHelpers.Context());

        // Assert
        Assert.Equal(new Canvas.Rgba32(0xEE, 0xEE, 0xEE, 255), scene.Background);
        Assert.Equal(2, scene.Scale);
        Assert.Contains(scene.Items.OfType<SceneText>(), t => t.Role == "title" && t.Text == "Hello");
        Assert.Equal(0, scene.ViewBox.X);
    }

    /// <summary>
    ///     Validates that the constructor rejects null arguments.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceModel_Constructor_RejectsNull()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => new SequenceModel(null!, [], [], PageTexts.None, SkinTheme.Default, [], default));
        Assert.Throws<ArgumentNullException>(() => new SequenceModel([], [], [], PageTexts.None, null!, [], default));
        Assert.Throws<ArgumentNullException>(() => SequenceTestHelpers.Parse("A -> B").Layout(null!));
    }
}

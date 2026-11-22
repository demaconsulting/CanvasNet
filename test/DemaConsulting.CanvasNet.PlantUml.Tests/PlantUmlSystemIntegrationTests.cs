using System.Text;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     System integration tests for the <c>CanvasNetPlantUml</c> system: reading PlantUML text through the public API.
/// </summary>
public class PlantUmlSystemIntegrationTests
{
    /// <summary>A sequence diagram that exercises the reader end to end.</summary>
    private const string Text = "' a sequence diagram\n@startuml\n!$who = \"Alice\"\nparticipant $who\n$who -> Bob : hello\n@enduml\n";

    /// <summary>
    ///     Validates that a string, a stream and a file of the same text give the same document.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Open_StringStreamAndPath_Agree()
    {
        // Arrange
        var path = PlantUmlTestHelpers.WriteTempFile(Encoding.UTF8.GetBytes(Text));
        try
        {
            // Act
            var parsed = PlantUmlDocument.Parse(Text);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Text));
            var streamed = PlantUmlDocument.Open(stream);
            var opened = PlantUmlDocument.Open(path);

            // Assert
            Assert.All([parsed, streamed, opened], document => Assert.Equal("sequence", document.DiagramType));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that an invalid diagram fails with a located, coded, hinted error in the original text.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Open_InvalidText_FailsLocated()
    {
        // Arrange
        const string text = "@startuml\nAlice -> Bob\nthis is not a statement\n@enduml\n";

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse(text, new PlantUmlOpenOptions { SourceName = "bad.puml" }));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new PlantUmlLocation("bad.puml", 3, 1), ex.Location);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates that one unreadable message from a participant named <c>Agent</c> (a deployment keyword) fails at
    ///     the message as a sequence syntax error, not at a valid participant declaration as a deployment error.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Open_BadMessageFromKeywordNamedParticipant_FailsAtTheMessage()
    {
        // Arrange
        const string text = "@startuml\nactor User\nparticipant Agent\nUser -> Agent : request\nAgent -> : call tool\nAgent --> User : answer\n@enduml\n";

        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => PlantUmlDocument.Parse(text, new PlantUmlOpenOptions { SourceName = "diagram.puml" }));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new PlantUmlLocation("diagram.puml", 5, 1), ex.Location);
        Assert.Contains("'Agent -> : call tool' is not a statement of a sequence diagram", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("deployment", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the supported preprocessor subset is applied before the diagram type is detected, offline.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Preprocessor_SubsetIsApplied()
    {
        // Arrange
        const string text = "@startuml\n' the macro decides the type\n!define DECLARE class\n!$name = \"Car\"\nDECLARE $name\n@enduml\n";

        // Act
        var document = PlantUmlDocument.Parse(text);

        // Assert
        Assert.Equal("class", document.DiagramType);
    }

    /// <summary>
    ///     Validates that a text over the size limit is refused before it is read.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_Limits_OversizedText_IsRefused()
    {
        // Arrange
        var options = new PlantUmlOpenOptions { Limits = new PlantUmlLimits { MaxTextSize = 100 } };
        var text = "@startuml\n" + string.Concat(Enumerable.Repeat("Alice -> Bob : a message\n", 50)) + "@enduml\n";

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => PlantUmlDocument.Parse(text, options));
    }

    /// <summary>
    ///     Validates that every other PlantUML diagram type is recognized and named.
    /// </summary>
    [Fact]
    public void CanvasNetPlantUml_OtherDiagramTypes_AreRecognized()
    {
        // Arrange
        var texts = new Dictionary<string, string>
        {
            ["activity-legacy"] = "@startuml\n(*) --> A\n@enduml\n",
            ["timing"] = "@startuml\nrobust \"Web\" as W\n@enduml\n",
            ["mindmap"] = "@startmindmap\n* a\n@endmindmap\n",
            ["gantt"] = "@startgantt\n[T] lasts 1 day\n@endgantt\n",
            ["json"] = "@startjson\n{}\n@endjson\n",
        };

        // Act
        var types = texts.ToDictionary(pair => pair.Key, pair => PlantUmlDocument.Parse(pair.Value));

        // Assert
        Assert.All(types, pair =>
        {
            Assert.Equal(pair.Key, pair.Value.DiagramType);
            Assert.False(pair.Value.IsSupported);
        });
    }

    /// <summary>
    ///     Validates that a skin parameter block (<c>skinparam element { BackgroundColor ... }</c>) is read in every drawn
    ///     type: the diagram keeps its type, renders without reports, and fills its element with the block's color.
    /// </summary>
    /// <param name="type">The expected type.</param>
    /// <param name="element">The element the block styles.</param>
    /// <param name="body">The statements after the block.</param>
    [Theory]
    [InlineData("sequence", "participant", "A -> B : x")]
    [InlineData("activity", "activity", "start\n:a;\nstop")]
    [InlineData("class", "class", "class A\nA --> B")]
    [InlineData("object", "object", "object O")]
    [InlineData("state", "state", "[*] --> S")]
    [InlineData("usecase", "usecase", "actor A\nA --> (U)")]
    [InlineData("component", "component", "[C] --> [D]")]
    [InlineData("deployment", "node", "node N")]
    public void CanvasNetPlantUml_SkinParamBlock_ColorsTheElementInEveryType(string type, string element, string body)
    {
        // Arrange
        var document = PlantUmlDocument.Parse($"@startuml\nskinparam {element} {{\n  BackgroundColor #DBEAFE\n}}\n{body}\n@enduml\n");
        var (options, warnings, unsupported) = PlantUmlTestHelpers.Collecting(strict: true);

        // Act
        var scene = document.LayoutScene(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(type, document.DiagramType);
        Assert.Empty(warnings);
        Assert.Empty(unsupported);
        Assert.Contains(scene.Items.OfType<ScenePath>(), path => path.Fill == new Canvas.Rgba32(0xDB, 0xEA, 0xFE, 255));
    }
}

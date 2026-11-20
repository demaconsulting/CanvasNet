using System.Text;

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
}

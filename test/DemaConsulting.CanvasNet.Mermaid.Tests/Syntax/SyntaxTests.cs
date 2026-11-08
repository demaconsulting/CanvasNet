using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Syntax;

/// <summary>
///     Subsystem tests of the Syntax subsystem: the preprocessing, the YAML reader and the detector working together on a
///     whole diagram text, with locations in the original text.
/// </summary>
public class SyntaxTests
{
    /// <summary>
    ///     Validates a text with every part Mermaid removes before parsing: the frontmatter is read, the directive is found,
    ///     the comments go, the type is detected at its location, and the parser text maps back to the original lines.
    /// </summary>
    [Fact]
    public void Syntax_PreprocessAndDetect_FullText_KeepsOriginalLocations()
    {
        // Arrange
        const string Text = "---\r\ntitle: Pipeline\r\nconfig:\r\n  flowchart:\r\n    curve: linear\r\n---\r\n" +
                            "%%{init: {'theme':'dark'}}%%\r\n%% a comment\r\nflowchart LR\r\n  A[#quot;x#quot;] --> B\r\n";

        // Act
        var source = Preprocessor.Run(Text, "p.mmd", MermaidLimits.Default);
        var detected = DiagramDetector.Detect(source.Code, DetectorSettings.Default);
        var b = source.ParserText.Text.LastIndexOf('B');

        // Assert
        Assert.Equal("Pipeline", source.Frontmatter!.Title);
        var flowchart = Assert.IsType<YamlMapping>(Assert.IsType<YamlMapping>(source.Frontmatter.Config).Get("flowchart"));
        Assert.Equal("linear", ((YamlScalar)flowchart.Get("curve")!).Value);
        Assert.Equal(("init", "{\"theme\":\"dark\"}"), (source.Directives[0].Type, source.Directives[0].Arguments));
        Assert.Equal(("flowchart-v2", new MermaidLocation("p.mmd", 9, 1)), (detected.MermaidId, detected.Location));
        Assert.Equal(new MermaidLocation("p.mmd", 10, 24), source.ParserText.Locate(b));
    }

    /// <summary>
    ///     Validates that a refused YAML construct in the frontmatter stops the text with its location.
    /// </summary>
    [Fact]
    public void Syntax_Preprocess_FrontmatterAlias_IsRefusedAtLocation()
    {
        // Arrange
        const string Text = "---\nbase: &b {a: 1}\nother: *b\n---\npie";

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Preprocessor.Run(Text, null, MermaidLimits.Default));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.YamlUnsupported, ex.Code);
        Assert.Equal(new MermaidLocation(null, 2, 7), ex.Location);
    }
}

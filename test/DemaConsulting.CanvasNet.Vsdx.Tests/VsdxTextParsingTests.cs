// cspell:ignore vsdx Visio davehoward

namespace DemaConsulting.CanvasNet.Vsdx.Tests;

/// <summary>
///     Unit-level tests for <c>VsdxDocument.Text.cs</c>'s <c>&lt;Text&gt;</c> element parser: the
///     <c>&lt;cp&gt;</c>/<c>&lt;pp&gt;</c> run/paragraph marker interleaving rule (format reference
///     §8.1), and the shape's own direct <c>Section N="Character"</c>/<c>"Paragraph"</c> row
///     parsing.
/// </summary>
public class VsdxTextParsingTests
{
    /// <summary>Builds a minimal one-shape package whose shape embeds the literal <paramref name="textXml"/> as its own <c>&lt;Text&gt;</c> child.</summary>
    private static VsdxDocument OpenWithTextShape(string textXml, string extraSectionXml = "")
    {
        var shapeXml =
            $"""
            <Shape ID="1" Type="Shape">
              <Cell N="PinX" V="1"/><Cell N="PinY" V="1"/><Cell N="Width" V="1"/><Cell N="Height" V="1"/>
              <Cell N="LocPinX" V="0.5"/><Cell N="LocPinY" V="0.5"/><Cell N="Angle" V="0"/>
              <Section N="Geometry" IX="0">
                <Row T="MoveTo" IX="1"><Cell N="X" V="0"/><Cell N="Y" V="0"/></Row>
              </Section>
              {textXml}
              {extraSectionXml}
            </Shape>
            """;
        using var stream = VsdxTestPackages.BuildPackage(shapeXml);
        return VsdxDocument.Open(stream);
    }

    /// <summary>
    ///     Proves <c>&lt;Text&gt;Shape A\n&lt;/Text&gt;</c> (mirrors
    ///     <c>davehoward-test4-connectors.vsdx</c> Shape 1 verbatim) parses to one run with both
    ///     row indices <see langword="null"/> and the trailing newline preserved verbatim.
    /// </summary>
    [Fact]
    public void TextParsing_NoMarkers_WholeTextIsOneImplicitRun()
    {
        // Arrange / Act
        using var document = OpenWithTextShape("<Text>Shape A\n</Text>");
        var rawText = VsdxDocument.ParseTextElement(
            System.Xml.Linq.XElement.Parse("<Text xmlns='http://schemas.microsoft.com/office/visio/2012/main'>Shape A\n</Text>"));

        // Assert
        var run = Assert.Single(rawText.Runs);
        Assert.Equal("Shape A\n", run.Text);
        Assert.Null(run.CharacterRowIndex);
        Assert.Null(run.ParagraphRowIndex);
    }

    /// <summary>
    ///     Proves <c>&lt;Text&gt;&lt;cp IX='0'/&gt;Text Color\n&lt;/Text&gt;</c> (mirrors
    ///     <c>davehoward-test12-colors.vsdx</c> Shape 2 verbatim) attaches <c>CharacterRowIndex
    ///     == 0</c> to the single following text segment.
    /// </summary>
    [Fact]
    public void TextParsing_SingleCpMarker_RowIndexAttachedToFollowingSegment()
    {
        // Arrange
        const string xml =
            "<Text xmlns='http://schemas.microsoft.com/office/visio/2012/main'><cp IX='0'/>Text Color\n</Text>";

        // Act
        var rawText = VsdxDocument.ParseTextElement(System.Xml.Linq.XElement.Parse(xml));

        // Assert
        var run = Assert.Single(rawText.Runs);
        Assert.Equal("Text Color\n", run.Text);
        Assert.Equal(0, run.CharacterRowIndex);
        Assert.Null(run.ParagraphRowIndex);
    }

    /// <summary>
    ///     Proves <c>&lt;Text&gt;&lt;pp IX="0"/&gt;&lt;cp IX="0"/&gt;Shape B&lt;/Text&gt;</c>
    ///     (mirrors <c>davehoward-test5-master.vsdx</c> master1.xml verbatim) attaches both row
    ///     indices to the single following text segment.
    /// </summary>
    [Fact]
    public void TextParsing_PpThenCpMarkers_BothRowIndicesAttached()
    {
        // Arrange
        const string xml =
            "<Text xmlns='http://schemas.microsoft.com/office/visio/2012/main'><pp IX=\"0\"/><cp IX=\"0\"/>Shape B</Text>";

        // Act
        var rawText = VsdxDocument.ParseTextElement(System.Xml.Linq.XElement.Parse(xml));

        // Assert
        var run = Assert.Single(rawText.Runs);
        Assert.Equal("Shape B", run.Text);
        Assert.Equal(0, run.CharacterRowIndex);
        Assert.Equal(0, run.ParagraphRowIndex);
    }

    /// <summary>
    ///     Proves multiple distinct <c>&lt;cp&gt;</c> markers within a single <c>&lt;Text&gt;</c>
    ///     element each start a new run until the next marker or the end of the element -
    ///     a documented, format-reference-rule-only case (synthetic; no corpus fixture exercises
    ///     this - see the originating plan report's Assumption #2).
    /// </summary>
    [Fact]
    public void TextParsing_MultipleCpMarkers_EachStartsNewRunUntilNextMarkerOrEnd()
    {
        // Arrange
        const string xml =
            "<Text xmlns='http://schemas.microsoft.com/office/visio/2012/main'><cp IX=\"0\"/>Red <cp IX=\"1\"/>Blue</Text>";

        // Act
        var rawText = VsdxDocument.ParseTextElement(System.Xml.Linq.XElement.Parse(xml));

        // Assert
        Assert.Equal(2, rawText.Runs.Count);
        Assert.Equal("Red ", rawText.Runs[0].Text);
        Assert.Equal(0, rawText.Runs[0].CharacterRowIndex);
        Assert.Equal("Blue", rawText.Runs[1].Text);
        Assert.Equal(1, rawText.Runs[1].CharacterRowIndex);
    }

    /// <summary>
    ///     Proves <c>&lt;Text&gt;&lt;fld IX='0'&gt;42 U&lt;/fld&gt;&lt;/Text&gt;</c> (mirrors
    ///     <c>60973.vsdx</c>'s <c>master32.xml</c> Shape ID='9' verbatim) parses the Field
    ///     element's own nested text content as a literal run, rather than the field being
    ///     silently dropped entirely - this milestone's Finding #3 "15 U" label sub-symptom fix
    ///     (<c>VsdxDocument.Text.cs</c>'s <c>BuildRawRuns</c> <c>&lt;fld&gt;</c> case).
    /// </summary>
    [Fact]
    public void TextParsing_FldElement_NestedTextContentParsedAsLiteralRun()
    {
        // Arrange
        const string xml =
            "<Text xmlns='http://schemas.microsoft.com/office/visio/2012/main'><fld IX='0'>42 U</fld></Text>";

        // Act
        var rawText = VsdxDocument.ParseTextElement(System.Xml.Linq.XElement.Parse(xml));

        // Assert
        var run = Assert.Single(rawText.Runs);
        Assert.Equal("42 U", run.Text);
    }

    /// <summary>
    ///     Proves a <c>&lt;fld&gt;</c> element interleaved with plain text on either side merges
    ///     into the surrounding run exactly like a plain <c>XText</c> segment would (no run
    ///     boundary is introduced merely because the content came from a <c>&lt;fld&gt;</c>
    ///     element rather than directly from <c>XText</c>).
    /// </summary>
    [Fact]
    public void TextParsing_FldElementInterleavedWithText_MergesIntoSurroundingRun()
    {
        // Arrange
        const string xml =
            "<Text xmlns='http://schemas.microsoft.com/office/visio/2012/main'><cp IX='0'/>Capacity: <fld IX='1'>15 U</fld> max</Text>";

        // Act
        var rawText = VsdxDocument.ParseTextElement(System.Xml.Linq.XElement.Parse(xml));

        // Assert
        var run = Assert.Single(rawText.Runs);
        Assert.Equal("Capacity: 15 U max", run.Text);
        Assert.Equal(0, run.CharacterRowIndex);
    }

    /// <summary>
    ///     Proves a shape's own direct <c>Section N="Character"</c> (no section-level <c>IX=</c>)
    ///     parses into a row-indexed cell-bag dictionary, distinct from
    ///     <c>Section N="Geometry" IX="0"</c>'s own section-indexed shape.
    /// </summary>
    [Fact]
    public void TextParsing_ShapeOwnSectionCharacterRow_ParsedAsRowIndexedCellBag()
    {
        // Arrange
        using var document = OpenWithTextShape(
            "<Text><cp IX=\"0\"/>Red Text</Text>",
            """<Section N="Character"><Row IX="0"><Cell N="Color" V="#ff0000"/></Row></Section>""");

        // Act
        var shape = document.GetPageShapes(0)[0];

        // Assert
        var run = Assert.Single(shape.TextRuns!);
        Assert.Equal(255, run.Color.R);
        Assert.Equal(0, run.Color.G);
        Assert.Equal(0, run.Color.B);
    }
}

using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Syntax;

/// <summary>
///     Subsystem tests for the <c>Syntax</c> subsystem: a text with references, comments, CDATA, a foreign element and
///     an annotation is read into the equation tree with every location in the original text.
/// </summary>
public class SyntaxTests
{
    /// <summary>The text of the tests: references shift the columns of the rest of their lines.</summary>
    private const string Text = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<math xmlns=\"http://www.w3.org/1998/Math/MathML\" xmlns:svg=\"http://www.w3.org/2000/svg\">\n" +
        "  <!-- &alpha; in a comment -->\n" +
        "  <mi>&alpha;</mi><mo>&InvisibleTimes;</mo><mi mathvariant=\"bold\">&Bopf;</mi>\n" +
        "  <mtext><![CDATA[a < b]]></mtext><svg:rect/>\n" +
        "  <semantics><mn>1</mn><annotation encoding=\"text/plain\">one &amp; only</annotation></semantics>\n" +
        "</math>\n";

    /// <summary>
    ///     Validates that the text is read in place: every element with its text and location, the CDATA kept, the foreign
    ///     element skipped and reported, and the annotation listed.
    /// </summary>
    [Fact]
    public void Syntax_Read_MixedText_IsReadInPlaceWithMappedLocations()
    {
        // Act
        var result = MathMlReader.Read(Text, "eq.mml", MathMlLimits.Default);

        // Assert: the elements, their texts and their locations in the original text
        var children = result.Root.ElementChildren.ToList();
        Assert.Equal(["mi", "mo", "mi", "mtext", "semantics"], children.Select(child => child.Name));
        Assert.Equal(["\u03B1", "\u2062", "\U0001D539", "a < b"], children.Take(4).Select(child => child.TextContent));
        Assert.Equal(new MathMlLocation("eq.mml", 4, 4), children[0].Location);
        Assert.Equal(new MathMlLocation("eq.mml", 4, 20), children[1].Location);
        Assert.Equal(new MathMlLocation("eq.mml", 4, 45), children[2].Location);
        Assert.Equal(new MathMlLocation("eq.mml", 4, 48), children[2].Attributes["mathvariant"].Location);

        // The foreign element is reported where it is written, and the annotation is listed
        var report = Assert.Single(result.Reports);
        Assert.Equal((MathMlDiagnosticCodes.ElementUnsupported, new MathMlLocation("eq.mml", 5, 36)), (report.Code, report.Location));
        Assert.Equal([new MathMlAnnotation("text/plain", "one & only")], result.Annotations);
        Assert.Equal(8, result.ElementCount);
    }

    /// <summary>
    ///     Validates that an error after references on the same line is located in the original text.
    /// </summary>
    [Fact]
    public void Syntax_Read_ErrorAfterReferences_IsLocatedInOriginalText()
    {
        // Arrange: the end tag does not match; "&Bopf;" grew by three characters before it
        const string text = "<math>\n<mi>&Bopf;</mo></math>";

        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read(text, null, MathMlLimits.Default));

        // Assert
        Assert.Equal(2, ex.Location.Line);
        Assert.InRange(ex.Location.Column, 11, 14);
    }

    /// <summary>
    ///     Validates that the attribute values of a read tree parse as the layout reads them (a named space, a length, a
    ///     relative script level, a boolean, a color written with a named reference, and an inline style) and that a link
    ///     is reported where it is written, not kept.
    /// </summary>
    [Fact]
    public void Syntax_ReadAndParse_AttributesOfTheTree_AreParsedAndLinksReported()
    {
        // Arrange
        const string text = "<math><mspace width=\"thinmathspace\" height=\"1.5ex\"/>" +
            "<mstyle scriptlevel=\"+1\" displaystyle=\"true\" mathcolor=\"&num;ff0000\" style=\"color: blue; math-depth: add(2)\"><mi href=\"https://example.invalid/\">x</mi></mstyle></math>";

        // Act
        var result = MathMlReader.Read(text, null, MathMlLimits.Default);
        var space = result.Root.ElementChildren.First();
        var style = result.Root.ElementChildren.Last();
        var widthParsed = AttributeValues.TryParseLength(space.GetAttribute("width"), out var width);
        var heightParsed = AttributeValues.TryParseLength(space.GetAttribute("height"), out var height);
        var levelParsed = AttributeValues.TryParseScriptLevel(style.GetAttribute("scriptlevel"), out var relative, out var level);
        var displayParsed = AttributeValues.TryParseBoolean(style.GetAttribute("displaystyle"), out var display);
        var colorParsed = AttributeValues.TryParseColor(style.GetAttribute("mathcolor"), out var color);
        var declarations = StyleDeclarations.Parse(style.GetAttribute("style")!);

        // Assert
        Assert.True(widthParsed && heightParsed && levelParsed && displayParsed && colorParsed);
        Assert.Equal(new MathLength(3.0 / 18, MathLengthUnit.Em), width);
        Assert.Equal(new MathLength(1.5, MathLengthUnit.Ex), height);
        Assert.Equal((true, 1, true), (relative, level, display));
        Assert.Equal(new Rgba32(255, 0, 0, 255), color);
        Assert.Equal(new Rgba32(0, 0, 255, 255), declarations.Color);
        Assert.Equal(new MathDepthValue(MathDepthKind.Add, 2), declarations.MathDepth);
        Assert.Empty(declarations.Issues);
        var link = Assert.Single(result.Reports);
        Assert.Equal((MathMlDiagnosticCodes.AttributeUnsupported, 1, 166), (link.Code, link.Location.Line, link.Location.Column));
        Assert.Null(style.ElementChildren.Single().GetAttribute("href"));
    }

    /// <summary>
    ///     Validates that a document type declaration and an XML declaration of another encoding are refused through the
    ///     subsystem with located syntax errors, while a UTF-8 declaration is accepted.
    /// </summary>
    [Fact]
    public void Syntax_Read_RefusedDeclarations_AreLocated()
    {
        // Act
        var doctype = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<!DOCTYPE math>\n<math/>", "d.mml", MathMlLimits.Default));
        var encoding = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n<math/>", "e.mml", MathMlLimits.Default));
        var accepted = MathMlReader.Read("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<math/>", null, MathMlLimits.Default);

        // Assert
        Assert.Equal((MathMlDiagnosticCodes.SyntaxError, "d.mml", 1), (doctype.Code, doctype.Location.SourceName, doctype.Location.Line));
        Assert.Contains("document type", doctype.Hint, StringComparison.Ordinal);
        Assert.Equal((MathMlDiagnosticCodes.SyntaxError, new MathMlLocation("e.mml", 1, 21)), (encoding.Code, encoding.Location));
        Assert.Equal("math", accepted.Root.Name);
    }
}

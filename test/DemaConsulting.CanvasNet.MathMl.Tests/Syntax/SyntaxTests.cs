using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Syntax;

/// <summary>
///     Subsystem tests for the <c>Syntax</c> subsystem: a text with references, comments, CDATA, a foreign element and
///     an annotation is read into the equation tree with every location in the original text; literal characters are read
///     as written; another root, an unknown name, and the reading limits are refused with located errors; and the
///     attribute values of the tree are parsed or refused.
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

        // Assert: the end tag name is at column 13 of the original (16 of the rewritten text)
        Assert.Equal(new MathMlLocation(null, 2, 13), ex.Location);
    }

    /// <summary>
    ///     Validates that literal characters outside ASCII (Greek, the minus sign, CJK, and a character outside the basic
    ///     plane) are read through the full pipeline exactly as written, next to references, with the locations after
    ///     them counted in UTF-16 code units.
    /// </summary>
    [Fact]
    public void Syntax_Read_LiteralUnicodeText_IsReadAsWritten()
    {
        // Arrange: the astral x counts as two columns, the reference after it grows by three characters
        const string text = "<math alttext=\"\u03B1\u2212\u4E2D\">\n<mi>\U0001D465</mi><mo>\u2212</mo><mi>&Bopf;</mi><mtext>\u4E2D\u6587</mtext></math>";

        // Act
        var result = MathMlReader.Read(text, "u.mml", MathMlLimits.Default);

        // Assert
        var children = result.Root.ElementChildren.ToList();
        Assert.Equal(["\U0001D465", "\u2212", "\U0001D539", "\u4E2D\u6587"], children.Select(child => child.TextContent));
        Assert.Equal("\u03B1\u2212\u4E2D", result.Root.GetAttribute("alttext"));
        Assert.Equal(new MathMlLocation("u.mml", 2, 38), children[3].Location);
        Assert.Empty(result.Reports);
    }

    /// <summary>
    ///     Validates the refusals of reading through the subsystem: another root and an unknown named reference fail with
    ///     located syntax errors and hints.
    /// </summary>
    [Fact]
    public void Syntax_Read_WrongRootAndUnknownName_AreRefusedLocated()
    {
        // Act
        var root = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<?xml version=\"1.0\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\"/>", "r.mml", MathMlLimits.Default));
        var unknown = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<math>\n<mi>&alpha;&notaname;</mi></math>", "n.mml", MathMlLimits.Default));

        // Assert
        Assert.Equal((MathMlDiagnosticCodes.SyntaxError, new MathMlLocation("r.mml", 2, 2)), (root.Code, root.Location));
        Assert.Contains("<svg>", root.Message, StringComparison.Ordinal);
        Assert.NotNull(root.Hint);
        Assert.Equal((MathMlDiagnosticCodes.SyntaxError, new MathMlLocation("n.mml", 2, 12)), (unknown.Code, unknown.Location));
        Assert.Contains("&notaname;", unknown.Message, StringComparison.Ordinal);
        Assert.NotNull(unknown.Hint);
    }

    /// <summary>
    ///     Validates the reading limits through the full read, after references that change the length of the line: the
    ///     element, nesting, and token-length limits fail with located limit errors that name the property.
    /// </summary>
    [Fact]
    public void Syntax_Read_Limits_AreEnforcedAndLocatedInOriginalText()
    {
        // Arrange: "&Bopf;" grows by three characters before every limit is reached
        const string text = "<math><mi>&Bopf;</mi><mrow><mrow><mi>&alpha;&beta;&gamma;</mi></mrow></mrow></math>";

        // Act
        var elements = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read(text, null, new MathMlLimits { MaxElements = 3 }));
        var depth = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read(text, null, new MathMlLimits { MaxNestingDepth = 3 }));
        var token = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read(text, null, new MathMlLimits { MaxTokenLength = 2 }));

        // Assert: the second mrow (column 29), the mi inside it (column 35), and the text of that mi (column 38)
        Assert.Equal((MathMlDiagnosticCodes.LimitExceeded, new MathMlLocation(null, 1, 29)), (elements.Code, elements.Location));
        Assert.Contains("MathMlLimits.MaxElements", elements.Message, StringComparison.Ordinal);
        Assert.Equal((MathMlDiagnosticCodes.LimitExceeded, new MathMlLocation(null, 1, 35)), (depth.Code, depth.Location));
        Assert.Contains("MathMlLimits.MaxNestingDepth", depth.Message, StringComparison.Ordinal);
        Assert.Equal((MathMlDiagnosticCodes.LimitExceeded, new MathMlLocation(null, 1, 38)), (token.Code, token.Location));
        Assert.Contains("MathMlLimits.MaxTokenLength", token.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the invalid attribute values of a read tree are refused by the parsers (so that the layout can
    ///     report and ignore them), while the valid unsigned integers of the same tree are read.
    /// </summary>
    [Fact]
    public void Syntax_ReadAndParse_InvalidAttributeValues_AreRefused()
    {
        // Arrange
        const string text = "<math><mtable><mtr><mtd rowspan=\"2\" columnspan=\"-1\">" +
            "<mstyle scriptlevel=\"1.5\" displaystyle=\"yes\" mathcolor=\"reddish\" style=\"color: nope\"><mspace width=\"wide\"/></mstyle>" +
            "</mtd></mtr></mtable></math>";

        // Act
        var result = MathMlReader.Read(text, null, MathMlLimits.Default);
        var cell = result.Root.ElementChildren.Single().ElementChildren.Single().ElementChildren.Single();
        var style = cell.ElementChildren.Single();
        var space = style.ElementChildren.Single();
        var declarations = StyleDeclarations.Parse(style.GetAttribute("style")!);

        // Assert
        Assert.True(AttributeValues.TryParseUnsignedInteger(cell.GetAttribute("rowspan"), out var rowspan));
        Assert.Equal(2, rowspan);
        Assert.False(AttributeValues.TryParseUnsignedInteger(cell.GetAttribute("columnspan"), out _));
        Assert.False(AttributeValues.TryParseScriptLevel(style.GetAttribute("scriptlevel"), out _, out _));
        Assert.False(AttributeValues.TryParseBoolean(style.GetAttribute("displaystyle"), out _));
        Assert.False(AttributeValues.TryParseColor(style.GetAttribute("mathcolor"), out _));
        Assert.False(AttributeValues.TryParseLength(space.GetAttribute("width"), out _));
        Assert.Null(declarations.Color);
        Assert.Single(declarations.Issues);
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

using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests.Syntax;

/// <summary>
///     Unit tests for <see cref="MathMlReader"/>: the root, foreign content, whitespace, limits, annotations, links and
///     locations.
/// </summary>
public class MathMlReaderTests
{
    /// <summary>
    ///     Validates that a <c>math</c> root with no namespace (as HTML writes it) and one with a prefix are accepted.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_NoNamespaceRoot_IsAccepted()
    {
        // Act
        var plain = MathMlReader.Read("<math><mi>x</mi></math>", null, MathMlLimits.Default);
        var prefixed = MathMlReader.Read("<m:math xmlns:m=\"http://www.w3.org/1998/Math/MathML\"><m:mi>x</m:mi></m:math>", null, MathMlLimits.Default);

        // Assert
        Assert.Equal(("math", 2), (plain.Root.Name, plain.ElementCount));
        Assert.Equal("mi", prefixed.Root.ElementChildren.Single().Name);
    }

    /// <summary>
    ///     Validates that another root element fails at its location with a hint.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_WrongRoot_FailsLocated()
    {
        // Act
        var html = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("\n<html/>", "a.mml", MathMlLimits.Default));
        var foreignMath = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<math xmlns=\"urn:other\"/>", null, MathMlLimits.Default));

        // Assert
        Assert.Equal(new MathMlLocation("a.mml", 2, 2), html.Location);
        Assert.Contains("<html>", html.Message, StringComparison.Ordinal);
        Assert.NotNull(html.Hint);
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, foreignMath.Code);
    }

    /// <summary>
    ///     Validates that an element of another namespace and markup inside a token are skipped with their subtrees and
    ///     reported at their locations.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_ForeignElement_IsSkippedAndReported()
    {
        // Arrange
        const string text = "<math xmlns:h=\"http://www.w3.org/1999/xhtml\">\n<h:b><mi>x</mi></h:b><mtext>a<mi>y</mi>b</mtext></math>";

        // Act
        var result = MathMlReader.Read(text, null, MathMlLimits.Default);

        // Assert
        var mtext = Assert.Single(result.Root.ElementChildren);
        Assert.Equal("ab", mtext.TextContent);
        Assert.Equal(2, result.ElementCount);
        Assert.Equal([new MathMlLocation(null, 2, 2), new MathMlLocation(null, 2, 31)], result.Reports.Select(report => report.Location));
        Assert.All(result.Reports, report => Assert.Equal((false, MathMlDiagnosticCodes.ElementUnsupported), (report.IsWarning, report.Code)));
    }

    /// <summary>
    ///     Validates that token text is trimmed and collapsed, and that whitespace between elements is dropped.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_TokenWhitespace_IsCollapsed()
    {
        // Act
        var result = MathMlReader.Read("<math>\n  <mtext>\n   if \t and\r\n  only </mtext>\n  <mi> x </mi>\n</math>", null, MathMlLimits.Default);

        // Assert
        Assert.Equal(["if and only", "x"], result.Root.ElementChildren.Select(child => child.TextContent));
        Assert.Equal(2, result.Root.Children.Count);
        Assert.Empty(result.Reports);
    }

    /// <summary>
    ///     Validates the element limit.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_ElementLimit_Throws()
    {
        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<math><mi>a</mi><mi>b</mi></math>", null, new MathMlLimits { MaxElements = 2 }));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxElements", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new MathMlLocation(null, 1, 18), ex.Location);
    }

    /// <summary>
    ///     Validates the nesting limit.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_DepthLimit_Throws()
    {
        // Act
        var accepted = MathMlReader.Read("<math><mrow><mi>a</mi></mrow></math>", null, new MathMlLimits { MaxNestingDepth = 3 });
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<math><mrow><mrow><mi>a</mi></mrow></mrow></math>", null, new MathMlLimits { MaxNestingDepth = 3 }));

        // Assert
        Assert.Equal(3, accepted.ElementCount);
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxNestingDepth", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the token length limit, counted on the raw text.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_TokenLengthLimit_Throws()
    {
        // Act
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<math><mn>123456</mn></math>", null, new MathMlLimits { MaxTokenLength = 5 }));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxTokenLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the content of the annotations is kept raw (text, or markup) and never reported.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_AnnotationXml_KeepsContentUnlaidOut()
    {
        // Arrange
        const string text = "<math><semantics><mi>x</mi>" +
            "<annotation encoding=\"application/x-tex\">  x &lt; 1 </annotation>" +
            "<annotation-xml encoding=\"MathML-Content\"><ci>x</ci></annotation-xml>" +
            "<annotation/></semantics></math>";

        // Act
        var result = MathMlReader.Read(text, null, MathMlLimits.Default);

        // Assert
        Assert.Equal(
            [new MathMlAnnotation("application/x-tex", "  x < 1 "), new MathMlAnnotation("MathML-Content", "<ci>x</ci>"), new MathMlAnnotation(null, string.Empty)],
            result.Annotations);
        Assert.Empty(result.Reports);
        Assert.Equal(6, result.ElementCount);
    }

    /// <summary>
    ///     Validates that links are reported and not kept, that attributes of other namespaces are ignored, and that
    ///     attributes keep their locations.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_Attributes_KeepLocationsAndReportLinks()
    {
        // Arrange
        const string text = "<math xmlns:xlink=\"http://www.w3.org/1999/xlink\" xml:lang=\"en\">\n<mi mathcolor=\"red\" href=\"x\" xlink:href=\"y\">x</mi></math>";

        // Act
        var result = MathMlReader.Read(text, null, MathMlLimits.Default);

        // Assert
        var mi = result.Root.ElementChildren.Single();
        Assert.Equal(["mathcolor"], mi.Attributes.Keys);
        Assert.Equal(new MathMlLocation(null, 2, 5), mi.Attributes["mathcolor"].Location);
        Assert.Empty(result.Root.Attributes);
        Assert.Equal(2, result.Reports.Count);
        Assert.All(result.Reports, report => Assert.Equal(MathMlDiagnosticCodes.AttributeUnsupported, report.Code));
    }

    /// <summary>
    ///     Validates that text directly inside a layout element is kept and reported, and that an unpaired surrogate is
    ///     refused.
    /// </summary>
    [Fact]
    public void MathMlReader_Read_TextOutsideTokens_IsKeptAndReported()
    {
        // Act
        var result = MathMlReader.Read("<math><mrow> loose  text <mi>x</mi></mrow></math>", null, MathMlLimits.Default);
        var ex = Assert.Throws<MathMlSyntaxException>(() => MathMlReader.Read("<math><mi>\uD800</mi></math>", null, MathMlLimits.Default));

        // Assert
        var mrow = result.Root.ElementChildren.Single();
        Assert.Equal("loose text", Assert.IsType<MathText>(mrow.Children[0]).Text);
        Assert.Equal(MathMlDiagnosticCodes.ElementUnsupported, Assert.Single(result.Reports).Code);
        Assert.Equal(new MathMlLocation(null, 1, 11), ex.Location);
    }
}

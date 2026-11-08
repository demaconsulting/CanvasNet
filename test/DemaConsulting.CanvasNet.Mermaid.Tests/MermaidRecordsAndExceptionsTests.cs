namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Unit tests for the small public records, codes and exceptions of the Mermaid package.
/// </summary>
public class MermaidRecordsAndExceptionsTests
{
    /// <summary>The location used by the tests.</summary>
    private static readonly MermaidLocation Where = new("flow.mmd", 4, 12);

    /// <summary>
    ///     Validates the display forms of a location: file, line and column; without a file; unknown.
    /// </summary>
    [Fact]
    public void MermaidLocation_ToString_Forms_AreFileLineColumn()
    {
        // Arrange / Act / Assert
        Assert.Equal("flow.mmd:4:12", Where.ToString());
        Assert.Equal("4:12", new MermaidLocation(null, 4, 12).ToString());
        Assert.Equal("flow.mmd:?", new MermaidLocation("flow.mmd", 0, 0).ToString());
        Assert.Equal("?", default(MermaidLocation).ToString());
    }

    /// <summary>
    ///     Validates that a location is known only with a line.
    /// </summary>
    [Fact]
    public void MermaidLocation_IsKnown_DependsOnLine()
    {
        // Arrange / Act / Assert
        Assert.True(Where.IsKnown);
        Assert.False(default(MermaidLocation).IsKnown);
        Assert.Equal(Where, new MermaidLocation("flow.mmd", 4, 12));
    }

    /// <summary>
    ///     Validates the display form of a diagnostic, with and without a location, and its hint.
    /// </summary>
    [Fact]
    public void MermaidDiagnostic_ToString_PrefixesLocationAndCode()
    {
        // Arrange
        var diagnostic = new MermaidDiagnostic("mermaid-style-ignored", "the declaration is ignored", Where) { Hint = "remove it" };

        // Act / Assert
        Assert.Equal("flow.mmd:4:12: mermaid-style-ignored: the declaration is ignored", diagnostic.ToString());
        Assert.Equal("mermaid-style-ignored: x", new MermaidDiagnostic("mermaid-style-ignored", "x", default).ToString());
        Assert.Equal("remove it", diagnostic.Hint);
    }

    /// <summary>
    ///     Validates the display form and value equality of an unsupported-feature report.
    /// </summary>
    [Fact]
    public void MermaidUnsupportedFeature_ToString_IncludesFeatureAndLocation()
    {
        // Arrange
        var report = new MermaidUnsupportedFeature("mermaid-diagram-unsupported", "not drawn", Where) { Hint = "use a flowchart" };

        // Act / Assert
        Assert.Equal("flow.mmd:4:12: mermaid-diagram-unsupported: not drawn", report.ToString());
        Assert.Equal("mermaid-diagram-unsupported: not drawn", new MermaidUnsupportedFeature("mermaid-diagram-unsupported", "not drawn", default).ToString());
        Assert.Equal(report, report with { });
    }

    /// <summary>
    ///     Validates that sizes have value equality.
    /// </summary>
    [Fact]
    public void MermaidSize_Equality_IsByValue()
    {
        // Arrange / Act / Assert
        Assert.Equal(new MermaidSize(3, 4), new MermaidSize(3, 4));
        Assert.NotEqual(new MermaidSize(3, 4), new MermaidSize(4, 3));
    }

    /// <summary>
    ///     Validates the located syntax exception: message, code, location, hint and expected tokens.
    /// </summary>
    [Fact]
    public void MermaidSyntaxException_Located_CarriesCodeLocationHintAndExpected()
    {
        // Arrange / Act
        var ex = new MermaidSyntaxException(
            MermaidDiagnosticCodes.SyntaxError,
            "expected '-->' or a new line, found 'PS'",
            Where,
            "quote the label",
            ["'-->'", "a new line"]);

        // Assert
        Assert.Equal("flow.mmd:4:12: mermaid-syntax-error: expected '-->' or a new line, found 'PS'", ex.Message);
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, Where, "quote the label"), (ex.Code, ex.Location, ex.Hint));
        Assert.Equal(["'-->'", "a new line"], ex.Expected);
        Assert.IsAssignableFrom<FormatException>(ex);
    }

    /// <summary>
    ///     Validates the standard constructors of the syntax exception: the general code and an unknown location.
    /// </summary>
    [Fact]
    public void MermaidSyntaxException_StandardConstructors_UseGeneralCode()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var plain = new MermaidSyntaxException();
        var message = new MermaidSyntaxException("text");
        var wrapped = new MermaidSyntaxException("text", inner);

        // Assert
        Assert.All([plain, message, wrapped], ex => Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code));
        Assert.Equal(("text", inner), (wrapped.Message, wrapped.InnerException));
        Assert.False(message.Location.IsKnown);
        Assert.Empty(message.Expected);
        Assert.Throws<ArgumentException>(() => new MermaidSyntaxException(string.Empty, "x", Where));
    }

    /// <summary>
    ///     Validates the render exception: located form and standard constructors.
    /// </summary>
    [Fact]
    public void MermaidRenderException_Constructors_CarryCodeAndLocation()
    {
        // Arrange / Act
        var located = new MermaidRenderException(MermaidDiagnosticCodes.LimitExceeded, "too many edges (MermaidLimits.MaxEdges)", Where, "split the diagram");
        var plain = new MermaidRenderException("text", new InvalidOperationException());

        // Assert
        Assert.Equal("flow.mmd:4:12: mermaid-limit-exceeded: too many edges (MermaidLimits.MaxEdges)", located.Message);
        Assert.Equal((MermaidDiagnosticCodes.LimitExceeded, "split the diagram"), (located.Code, located.Hint));
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, plain.Code);
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, new MermaidRenderException().Code);
        Assert.IsAssignableFrom<InvalidOperationException>(located);
    }

    /// <summary>
    ///     Validates the unsupported-feature exception: located form and standard constructors.
    /// </summary>
    [Fact]
    public void MermaidUnsupportedFeatureException_Constructors_CarryFeatureAndLocation()
    {
        // Arrange / Act
        var located = new MermaidUnsupportedFeatureException(MermaidDiagnosticCodes.DiagramUnsupported, "not drawn", Where, "hint");
        var plain = new MermaidUnsupportedFeatureException("text");

        // Assert
        Assert.Equal("flow.mmd:4:12: mermaid-diagram-unsupported: not drawn", located.Message);
        Assert.Equal((MermaidDiagnosticCodes.DiagramUnsupported, Where, "hint"), (located.Feature, located.Location, located.Hint));
        Assert.Equal(string.Empty, plain.Feature);
        Assert.Equal(string.Empty, new MermaidUnsupportedFeatureException().Feature);
        Assert.Equal(string.Empty, new MermaidUnsupportedFeatureException("t", new InvalidOperationException()).Feature);
        Assert.IsAssignableFrom<IOException>(located);
    }

    /// <summary>
    ///     Validates that every published code is a distinct <c>mermaid-*</c> identifier.
    /// </summary>
    [Fact]
    public void MermaidDiagnosticCodes_AllCodes_AreDistinctMermaidIdentifiers()
    {
        // Arrange
        var codes = typeof(MermaidDiagnosticCodes)
            .GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        // Act / Assert
        Assert.True(codes.Count >= 20);
        Assert.All(codes, code => Assert.Matches("^mermaid-[a-z-]+$", code));
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }
}

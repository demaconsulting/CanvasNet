namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Unit tests for the small public records, codes and exceptions of the PlantUml package.
/// </summary>
public class PlantUmlRecordsAndExceptionsTests
{
    /// <summary>The location used by the tests.</summary>
    private static readonly PlantUmlLocation Where = new("seq.puml", 4, 12);

    /// <summary>
    ///     Validates the display forms of a location: file, line and column; without a file; unknown.
    /// </summary>
    [Fact]
    public void PlantUmlLocation_ToString_Forms_AreFileLineColumn()
    {
        // Arrange / Act / Assert
        Assert.Equal("seq.puml:4:12", Where.ToString());
        Assert.Equal("4:12", new PlantUmlLocation(null, 4, 12).ToString());
        Assert.Equal("seq.puml:?", new PlantUmlLocation("seq.puml", 0, 0).ToString());
        Assert.Equal("?", default(PlantUmlLocation).ToString());
    }

    /// <summary>
    ///     Validates that a location is known only with a line.
    /// </summary>
    [Fact]
    public void PlantUmlLocation_IsKnown_DependsOnLine()
    {
        // Arrange / Act / Assert
        Assert.True(Where.IsKnown);
        Assert.False(default(PlantUmlLocation).IsKnown);
        Assert.Equal(Where, new PlantUmlLocation("seq.puml", 4, 12));
    }

    /// <summary>
    ///     Validates the display form of a diagnostic, with and without a location, and its hint.
    /// </summary>
    [Fact]
    public void PlantUmlDiagnostic_ToString_PrefixesLocationAndCode()
    {
        // Arrange
        var diagnostic = new PlantUmlDiagnostic("plantuml-skinparam-ignored", "the declaration is ignored", Where) { Hint = "remove it" };

        // Act / Assert
        Assert.Equal("seq.puml:4:12: plantuml-skinparam-ignored: the declaration is ignored", diagnostic.ToString());
        Assert.Equal("plantuml-skinparam-ignored: x", new PlantUmlDiagnostic("plantuml-skinparam-ignored", "x", default).ToString());
        Assert.Equal("remove it", diagnostic.Hint);
    }

    /// <summary>
    ///     Validates the display form and value equality of an unsupported-feature report.
    /// </summary>
    [Fact]
    public void PlantUmlUnsupportedFeature_ToString_IncludesFeatureAndLocation()
    {
        // Arrange
        var report = new PlantUmlUnsupportedFeature("plantuml-diagram-unsupported", "not drawn", Where) { Hint = "use a sequence diagram" };

        // Act / Assert
        Assert.Equal("seq.puml:4:12: plantuml-diagram-unsupported: not drawn", report.ToString());
        Assert.Equal("plantuml-diagram-unsupported: not drawn", new PlantUmlUnsupportedFeature("plantuml-diagram-unsupported", "not drawn", default).ToString());
        Assert.Equal(report, report with { });
    }

    /// <summary>
    ///     Validates that sizes have value equality.
    /// </summary>
    [Fact]
    public void PlantUmlSize_Equality_IsByValue()
    {
        // Arrange / Act / Assert
        Assert.Equal(new PlantUmlSize(3, 4), new PlantUmlSize(3, 4));
        Assert.NotEqual(new PlantUmlSize(3, 4), new PlantUmlSize(4, 3));
    }

    /// <summary>
    ///     Validates the located syntax exception: message, code, location, hint and expected tokens.
    /// </summary>
    [Fact]
    public void PlantUmlSyntaxException_Located_CarriesCodeLocationHintAndExpected()
    {
        // Arrange / Act
        var ex = new PlantUmlSyntaxException(
            PlantUmlDiagnosticCodes.SyntaxError,
            "expected an arrow, found 'PS'",
            Where,
            "write A -> B",
            ["an arrow"]);

        // Assert
        Assert.Equal("seq.puml:4:12: plantuml-syntax-error: expected an arrow, found 'PS'", ex.Message);
        Assert.Equal((PlantUmlDiagnosticCodes.SyntaxError, Where, "write A -> B"), (ex.Code, ex.Location, ex.Hint));
        Assert.Equal(["an arrow"], ex.Expected);
        Assert.IsAssignableFrom<FormatException>(ex);
    }

    /// <summary>
    ///     Validates the standard constructors of the syntax exception: the general code and an unknown location.
    /// </summary>
    [Fact]
    public void PlantUmlSyntaxException_StandardConstructors_UseGeneralCode()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var plain = new PlantUmlSyntaxException();
        var message = new PlantUmlSyntaxException("text");
        var wrapped = new PlantUmlSyntaxException("text", inner);

        // Assert
        Assert.All([plain, message, wrapped], ex => Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code));
        Assert.Equal(("text", inner), (wrapped.Message, wrapped.InnerException));
        Assert.False(message.Location.IsKnown);
        Assert.Empty(message.Expected);
        Assert.Throws<ArgumentException>(() => new PlantUmlSyntaxException(string.Empty, "x", Where));
    }

    /// <summary>
    ///     Validates the render exception: located form and standard constructors.
    /// </summary>
    [Fact]
    public void PlantUmlRenderException_Constructors_CarryCodeAndLocation()
    {
        // Arrange / Act
        var located = new PlantUmlRenderException(PlantUmlDiagnosticCodes.LimitExceeded, "too many edges (PlantUmlLimits.MaxEdges)", Where, "split the diagram");
        var plain = new PlantUmlRenderException("text", new InvalidOperationException());

        // Assert
        Assert.Equal("seq.puml:4:12: plantuml-limit-exceeded: too many edges (PlantUmlLimits.MaxEdges)", located.Message);
        Assert.Equal((PlantUmlDiagnosticCodes.LimitExceeded, "split the diagram"), (located.Code, located.Hint));
        Assert.Equal(PlantUmlDiagnosticCodes.RenderFailed, plain.Code);
        Assert.Equal(PlantUmlDiagnosticCodes.RenderFailed, new PlantUmlRenderException().Code);
        Assert.IsAssignableFrom<InvalidOperationException>(located);
    }

    /// <summary>
    ///     Validates the unsupported-feature exception: located form and standard constructors.
    /// </summary>
    [Fact]
    public void PlantUmlUnsupportedFeatureException_Constructors_CarryFeatureAndLocation()
    {
        // Arrange / Act
        var located = new PlantUmlUnsupportedFeatureException(PlantUmlDiagnosticCodes.DiagramUnsupported, "not drawn", Where, "hint");
        var plain = new PlantUmlUnsupportedFeatureException("text");

        // Assert
        Assert.Equal("seq.puml:4:12: plantuml-diagram-unsupported: not drawn", located.Message);
        Assert.Equal((PlantUmlDiagnosticCodes.DiagramUnsupported, Where, "hint"), (located.Feature, located.Location, located.Hint));
        Assert.Equal(string.Empty, plain.Feature);
        Assert.Equal(string.Empty, new PlantUmlUnsupportedFeatureException().Feature);
        Assert.Equal(string.Empty, new PlantUmlUnsupportedFeatureException("t", new InvalidOperationException()).Feature);
        Assert.IsAssignableFrom<IOException>(located);
    }

    /// <summary>
    ///     Validates that every published code is a distinct <c>plantuml-*</c> identifier.
    /// </summary>
    [Fact]
    public void PlantUmlDiagnosticCodes_AllCodes_AreDistinctPlantUmlIdentifiers()
    {
        // Arrange
        var codes = typeof(PlantUmlDiagnosticCodes)
            .GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        // Act / Assert
        Assert.Equal(17, codes.Count);
        Assert.All(codes, code => Assert.Matches("^plantuml-[a-z-]+$", code));
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }
}

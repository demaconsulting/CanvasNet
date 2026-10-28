namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Unit tests for the small public records and exceptions of the VegaLite package.
/// </summary>
public class VegaLiteRecordsAndExceptionsTests
{
    /// <summary>The location used by the tests.</summary>
    private static readonly VegaLiteLocation Where = new("s.json", "/mark", 2, 5);

    /// <summary>
    ///     Validates the display form of a diagnostic.
    /// </summary>
    [Fact]
    public void VegaLiteDiagnostic_ToString_KnownLocation_PrefixesLocation()
    {
        // Arrange
        var diagnostic = new VegaLiteDiagnostic("vl-test: text", Where);

        // Act / Assert
        Assert.Equal("s.json:2:5 /mark: vl-test: text", diagnostic.ToString());
    }

    /// <summary>
    ///     Validates that a diagnostic without a location is just the message.
    /// </summary>
    [Fact]
    public void VegaLiteDiagnostic_ToString_UnknownLocation_IsMessageOnly()
    {
        // Arrange
        var diagnostic = new VegaLiteDiagnostic("vl-test: text", default);

        // Act / Assert
        Assert.Equal("vl-test: text", diagnostic.ToString());
    }

    /// <summary>
    ///     Validates the display form of an unsupported-feature report.
    /// </summary>
    [Fact]
    public void VegaLiteUnsupportedFeature_ToString_IncludesFeatureAndLocation()
    {
        // Arrange
        var report = new VegaLiteUnsupportedFeature("vl-mark-arc", "not yet", Where);

        // Act / Assert
        Assert.Equal("s.json:2:5 /mark: vl-mark-arc: not yet", report.ToString());
        Assert.Equal("vl-mark-arc: not yet", new VegaLiteUnsupportedFeature("vl-mark-arc", "not yet", default).ToString());
    }

    /// <summary>
    ///     Validates that sizes and data references have value equality.
    /// </summary>
    [Fact]
    public void VegaLiteSize_And_DataReference_HaveValueEquality()
    {
        // Arrange / Act / Assert
        Assert.Equal(new VegaLiteSize(3, 4), new VegaLiteSize(3, 4));
        Assert.Equal(
            new VegaLiteDataReference(VegaLiteDataKind.Inline, null, 2, Where),
            new VegaLiteDataReference(VegaLiteDataKind.Inline, null, 2, Where));
        Assert.NotEqual(
            new VegaLiteDataReference(VegaLiteDataKind.Inline, null, 2, Where),
            new VegaLiteDataReference(VegaLiteDataKind.Url, "a.csv", null, Where));
    }

    /// <summary>
    ///     Validates the specification exception: base type, location in the message and property.
    /// </summary>
    [Fact]
    public void VegaLiteSpecException_WithLocation_CarriesLocationAndPrefixesMessage()
    {
        // Arrange / Act
        var ex = new VegaLiteSpecException("bad thing", Where);

        // Assert
        Assert.IsAssignableFrom<FormatException>(ex);
        Assert.Equal(Where, ex.Location);
        Assert.Equal("s.json:2:5 /mark: bad thing", ex.Message);
    }

    /// <summary>
    ///     Validates the other constructors of the specification exception.
    /// </summary>
    [Fact]
    public void VegaLiteSpecException_OtherConstructors_LeaveLocationUnknownOrKeepInner()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var empty = new VegaLiteSpecException();
        var message = new VegaLiteSpecException("m");
        var withInner = new VegaLiteSpecException("m", inner);
        var located = new VegaLiteSpecException("m", Where, inner);
        var unlocated = new VegaLiteSpecException("m", default(VegaLiteLocation));

        // Assert
        Assert.False(empty.Location.IsKnown);
        Assert.Equal("m", message.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Same(inner, located.InnerException);
        Assert.Equal(Where, located.Location);
        Assert.Equal("m", unlocated.Message);
    }

    /// <summary>
    ///     Validates the render exception: base type, location and constructors.
    /// </summary>
    [Fact]
    public void VegaLiteRenderException_Constructors_CarryLocationAndInner()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var located = new VegaLiteRenderException("boom", Where);
        var withInner = new VegaLiteRenderException("m", inner);

        // Assert
        Assert.IsAssignableFrom<InvalidOperationException>(located);
        Assert.Equal(Where, located.Location);
        Assert.Equal("s.json:2:5 /mark: boom", located.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.False(new VegaLiteRenderException().Location.IsKnown);
        Assert.Equal("m", new VegaLiteRenderException("m").Message);
    }

    /// <summary>
    ///     Validates the unsupported-feature exception: base type, feature, location and constructors.
    /// </summary>
    [Fact]
    public void VegaLiteUnsupportedFeatureException_Constructors_CarryFeatureAndLocation()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var located = new VegaLiteUnsupportedFeatureException("vl-mark-arc", "no arcs", Where);
        var withInner = new VegaLiteUnsupportedFeatureException("m", inner);

        // Assert
        Assert.IsAssignableFrom<IOException>(located);
        Assert.Equal("vl-mark-arc", located.Feature);
        Assert.Equal(Where, located.Location);
        Assert.Equal("s.json:2:5 /mark: no arcs", located.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Equal(string.Empty, new VegaLiteUnsupportedFeatureException().Feature);
        Assert.Equal(string.Empty, new VegaLiteUnsupportedFeatureException("m").Feature);
        Assert.Equal(string.Empty, withInner.Feature);
    }
}

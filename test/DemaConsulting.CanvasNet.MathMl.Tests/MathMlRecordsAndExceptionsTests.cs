namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Unit tests for the public records and exceptions: <see cref="MathMlLocation"/>, <see cref="MathMlDiagnostic"/>,
///     <see cref="MathMlUnsupportedFeature"/>, <see cref="MathMlSize"/>, <see cref="MathMlAnnotation"/> and the three
///     exception types.
/// </summary>
public class MathMlRecordsAndExceptionsTests
{
    /// <summary>
    ///     Validates the formatting of a location with and without a source name and position.
    /// </summary>
    [Fact]
    public void MathMlLocation_ToString_FormatsEditorPosition()
    {
        // Act / Assert
        Assert.Equal("a.mml:3:7", new MathMlLocation("a.mml", 3, 7).ToString());
        Assert.Equal("3:7", new MathMlLocation(null, 3, 7).ToString());
        Assert.Equal("a.mml:?", new MathMlLocation("a.mml", 0, 0).ToString());
        Assert.False(default(MathMlLocation).IsKnown);
    }

    /// <summary>
    ///     Validates the formatting of the reports, with and without a known location.
    /// </summary>
    [Fact]
    public void MathMlDiagnostic_ToString_PrefixesKnownLocation()
    {
        // Arrange
        var located = new MathMlDiagnostic("mathml-x", "message", new MathMlLocation(null, 1, 2)) { Hint = "hint" };
        var unlocated = new MathMlUnsupportedFeature("mathml-y", "message", default);

        // Act / Assert
        Assert.Equal("1:2: mathml-x: message", located.ToString());
        Assert.Equal("hint", located.Hint);
        Assert.Equal("mathml-y: message", unlocated.ToString());
    }

    /// <summary>
    ///     Validates the value semantics of the size and the annotation.
    /// </summary>
    [Fact]
    public void MathMlSize_AndAnnotation_HaveValueEquality()
    {
        // Act / Assert
        Assert.Equal(new MathMlSize(3, 4, 2), new MathMlSize(3, 4, 2));
        Assert.Equal(new MathMlAnnotation("application/x-tex", "x"), new MathMlAnnotation("application/x-tex", "x"));
    }

    /// <summary>
    ///     Validates the located constructors of the exceptions: code, location, hint and message.
    /// </summary>
    [Fact]
    public void MathMlExceptions_LocatedConstructors_CarryCodeLocationAndHint()
    {
        // Arrange
        var location = new MathMlLocation("a.mml", 2, 5);

        // Act
        var syntax = new MathMlSyntaxException(MathMlDiagnosticCodes.SyntaxError, "bad", location, "fix");
        var render = new MathMlRenderException(MathMlDiagnosticCodes.LimitExceeded, "big", location, "shrink");
        var unsupported = new MathMlUnsupportedFeatureException(MathMlDiagnosticCodes.ElementUnsupported, "later", location, "wait");

        // Assert
        Assert.Equal(("a.mml:2:5: mathml-syntax-error: bad", "fix"), (syntax.Message, syntax.Hint));
        Assert.Equal((MathMlDiagnosticCodes.LimitExceeded, location, "shrink"), (render.Code, render.Location, render.Hint));
        Assert.Equal((MathMlDiagnosticCodes.ElementUnsupported, location, "wait"), (unsupported.Feature, unsupported.Location, unsupported.Hint));
        Assert.Throws<ArgumentException>(() => new MathMlSyntaxException(string.Empty, "x", location));
    }

    /// <summary>
    ///     Validates the standard constructors of the exceptions: the default codes and an unknown location.
    /// </summary>
    [Fact]
    public void MathMlExceptions_StandardConstructors_UseDefaultCodes()
    {
        // Act
        var inner = new InvalidOperationException("inner");
        var syntax = new MathMlSyntaxException("message", inner);
        var render = new MathMlRenderException("message", inner);
        var unsupported = new MathMlUnsupportedFeatureException("message", inner);

        // Assert
        Assert.Equal((MathMlDiagnosticCodes.SyntaxError, inner), (syntax.Code, syntax.InnerException));
        Assert.Equal((MathMlDiagnosticCodes.RenderFailed, inner), (render.Code, render.InnerException));
        Assert.Equal((string.Empty, inner), (unsupported.Feature, unsupported.InnerException));
        Assert.Equal(MathMlDiagnosticCodes.SyntaxError, new MathMlSyntaxException().Code);
        Assert.Equal(MathMlDiagnosticCodes.RenderFailed, new MathMlRenderException("m").Code);
        Assert.Equal(string.Empty, new MathMlUnsupportedFeatureException().Feature);
        Assert.IsAssignableFrom<FormatException>(syntax);
        Assert.IsAssignableFrom<InvalidOperationException>(render);
        Assert.IsAssignableFrom<IOException>(unsupported);
    }

    /// <summary>
    ///     Validates that every published code is a distinct <c>mathml-*</c> identifier.
    /// </summary>
    [Fact]
    public void MathMlDiagnosticCodes_AllCodes_AreDistinctMathMlIdentifiers()
    {
        // Act
        var codes = typeof(MathMlDiagnosticCodes).GetFields().Select(field => (string)field.GetValue(null)!).ToList();

        // Assert
        Assert.Equal(11, codes.Count);
        Assert.Contains("mathml-stretch-unavailable", codes);
        Assert.Contains("mathml-child-count", codes);
        Assert.All(codes, code => Assert.StartsWith("mathml-", code, StringComparison.Ordinal));
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }
}

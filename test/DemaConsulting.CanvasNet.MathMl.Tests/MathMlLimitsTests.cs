namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Unit tests for <see cref="MathMlLimits"/>: defaults and validation.
/// </summary>
public class MathMlLimitsTests
{
    /// <summary>
    ///     Validates the default limits.
    /// </summary>
    [Fact]
    public void MathMlLimits_Default_HasDocumentedValues()
    {
        // Act
        var limits = MathMlLimits.Default;

        // Assert
        Assert.Equal((1_048_576, 100_000, 256, 10_000), (limits.MaxTextSize, limits.MaxElements, limits.MaxNestingDepth, limits.MaxTokenLength));
        Assert.Equal((10_000_000L, 1_000_000, 1_000), (limits.MaxLayoutWork, limits.MaxPaintedGlyphs, limits.MaxDiagnostics));
    }

    /// <summary>
    ///     Validates that values below one are rejected with the property name.
    /// </summary>
    [Fact]
    public void MathMlLimits_ValueBelowOne_Throws()
    {
        // Act / Assert
        Assert.Equal("MaxTextSize", Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlLimits { MaxTextSize = 0 }).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlLimits { MaxElements = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlLimits { MaxTokenLength = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlLimits { MaxLayoutWork = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlLimits { MaxPaintedGlyphs = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlLimits { MaxDiagnostics = -1 });
    }

    /// <summary>
    ///     Validates the range of the nesting depth: 1 to 1,024, which bounds the recursion of the layout.
    /// </summary>
    [Fact]
    public void MathMlLimits_MaxNestingDepth_OutsideRange_Throws()
    {
        // Act
        var deepest = new MathMlLimits { MaxNestingDepth = 1_024 };

        // Assert
        Assert.Equal(1_024, deepest.MaxNestingDepth);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlLimits { MaxNestingDepth = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlLimits { MaxNestingDepth = 1_025 });
    }
}

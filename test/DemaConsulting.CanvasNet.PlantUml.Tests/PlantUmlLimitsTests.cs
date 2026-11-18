namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Unit tests for <see cref="PlantUmlLimits"/>: the defaults (those of the Mermaid package) and the validation of
///     every property.
/// </summary>
public class PlantUmlLimitsTests
{
    /// <summary>
    ///     Validates the default limits.
    /// </summary>
    [Fact]
    public void PlantUmlLimits_Default_HasDocumentedValues()
    {
        // Arrange
        var limits = PlantUmlLimits.Default;

        // Act / Assert: the values mirror the Mermaid package, plus the include limits
        Assert.Equal(50_000, limits.MaxTextSize);
        Assert.Equal(500, limits.MaxEdges);
        Assert.Equal(2_000, limits.MaxNodes);
        Assert.Equal(4_096, limits.MaxLabelLength);
        Assert.Equal(32, limits.MaxNestingDepth);
        Assert.Equal(16, limits.MaxIncludeDepth);
        Assert.Equal(64, limits.MaxIncludes);
        Assert.Equal(50_000_000L, limits.MaxLayoutWork);
        Assert.Equal(250_000_000L, limits.MaxPaintedPixels);
        Assert.Equal(1_000, limits.MaxDiagnostics);
    }

    /// <summary>
    ///     Validates that every limit refuses values below one and keeps a valid value.
    /// </summary>
    [Fact]
    public void PlantUmlLimits_Init_BelowOne_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxTextSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxEdges = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxNodes = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxLabelLength = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxIncludes = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxLayoutWork = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxPaintedPixels = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxDiagnostics = 0 });
        Assert.Equal(7, new PlantUmlLimits { MaxEdges = 7 }.MaxEdges);
    }

    /// <summary>
    ///     Validates the ranges of the depth limits, which bound recursion.
    /// </summary>
    [Fact]
    public void PlantUmlLimits_Init_DepthOutsideRange_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxNestingDepth = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxNestingDepth = 257 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxIncludeDepth = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlLimits { MaxIncludeDepth = 65 });
        Assert.Equal((256, 64), (new PlantUmlLimits { MaxNestingDepth = 256 }.MaxNestingDepth, new PlantUmlLimits { MaxIncludeDepth = 64 }.MaxIncludeDepth));
    }
}

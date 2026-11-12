namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Unit tests for <see cref="MermaidLimits"/>: the defaults (Mermaid's own where Mermaid has the limit) and the
///     validation of every property.
/// </summary>
public class MermaidLimitsTests
{
    /// <summary>
    ///     Validates the default limits.
    /// </summary>
    [Fact]
    public void MermaidLimits_Default_HasDocumentedValues()
    {
        // Arrange
        var limits = MermaidLimits.Default;

        // Act / Assert: maxTextSize and maxEdges are Mermaid's defaults
        Assert.Equal(50_000, limits.MaxTextSize);
        Assert.Equal(500, limits.MaxEdges);
        Assert.Equal(2_000, limits.MaxNodes);
        Assert.Equal(4_096, limits.MaxLabelLength);
        Assert.Equal(32, limits.MaxNestingDepth);
        Assert.Equal(32, limits.MaxYamlDepth);
        Assert.Equal(50_000_000L, limits.MaxLayoutWork);
        Assert.Equal(256, limits.MaxImages);
        Assert.Equal(250_000_000L, limits.MaxPaintedPixels);
        Assert.Equal(1_000, limits.MaxDiagnostics);
    }

    /// <summary>
    ///     Validates that every limit refuses values below one and keeps a valid value.
    /// </summary>
    [Fact]
    public void MermaidLimits_Init_BelowOne_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxTextSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxEdges = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxNodes = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxLabelLength = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxLayoutWork = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxImages = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxPaintedPixels = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxDiagnostics = 0 });
        Assert.Equal(7, new MermaidLimits { MaxEdges = 7 }.MaxEdges);
    }

    /// <summary>
    ///     Validates the ranges of the depth limits, which bound recursion.
    /// </summary>
    [Fact]
    public void MermaidLimits_Init_DepthOutsideRange_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxNestingDepth = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxNestingDepth = 257 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxYamlDepth = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidLimits { MaxYamlDepth = 257 });
        Assert.Equal((256, 1), (new MermaidLimits { MaxNestingDepth = 256 }.MaxNestingDepth, new MermaidLimits { MaxYamlDepth = 1 }.MaxYamlDepth));
    }
}

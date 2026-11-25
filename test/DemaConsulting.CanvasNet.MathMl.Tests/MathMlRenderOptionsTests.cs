using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Unit tests for <see cref="MathMlRenderOptions"/> and <see cref="MathMlOpenOptions"/>: defaults and validation.
/// </summary>
public class MathMlRenderOptionsTests
{
    /// <summary>
    ///     Validates the defaults: 16 px, scale 1, the document's display, black text, a transparent background.
    /// </summary>
    [Fact]
    public void MathMlRenderOptions_Defaults_AreBrowserDefaults()
    {
        // Act
        var options = new MathMlRenderOptions();

        // Assert
        Assert.Equal((16.0, 1.0, (MathMlDisplay?)null), (options.FontSize, options.Scale, options.Display));
        Assert.Equal(new Rgba32(0, 0, 0, 255), options.Color);
        Assert.Null(options.Background);
        Assert.Same(MathMlLimits.Default, options.Limits);
        Assert.False(options.ThrowOnUnsupportedFeature);
    }

    /// <summary>
    ///     Validates that a font size or a scale that is not finite and positive, or too large, is rejected.
    /// </summary>
    /// <param name="value">The value.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(10_000)]
    public void MathMlRenderOptions_FontSizeAndScale_InvalidValue_Throws(double value)
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlRenderOptions { FontSize = value });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MathMlRenderOptions { Scale = value });
    }

    /// <summary>
    ///     Validates that null limits are rejected.
    /// </summary>
    [Fact]
    public void MathMlRenderOptions_Limits_Null_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new MathMlRenderOptions { Limits = null! });
    }

    /// <summary>
    ///     Validates the open options: a whitespace source name is rejected; no limits means the defaults.
    /// </summary>
    [Fact]
    public void MathMlOpenOptions_SourceNameAndLimits_AreValidated()
    {
        // Act
        var options = new MathMlOpenOptions { SourceName = "a.mml" };

        // Assert
        Assert.Equal("a.mml", options.SourceName);
        Assert.Same(MathMlLimits.Default, options.EffectiveLimits);
        Assert.Throws<ArgumentException>(() => new MathMlOpenOptions { SourceName = " " });
    }
}

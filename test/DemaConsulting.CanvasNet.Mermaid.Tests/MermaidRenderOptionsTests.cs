using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Unit tests for <see cref="MermaidRenderOptions"/> and <see cref="MermaidOpenOptions"/>: defaults and validation.
/// </summary>
public class MermaidRenderOptionsTests
{
    /// <summary>
    ///     Validates the defaults: scale 1, transparent background, no theme or configuration, default limits, lenient.
    /// </summary>
    [Fact]
    public void MermaidRenderOptions_Defaults_AreDocumented()
    {
        // Arrange
        var options = new MermaidRenderOptions();

        // Act / Assert
        Assert.Equal(1, options.Scale);
        Assert.Null(options.Background);
        Assert.Null(options.Config);
        Assert.Null(options.Theme);
        Assert.Null(options.ImageResolver);
        Assert.Same(MermaidLimits.Default, options.Limits);
        Assert.False(options.ThrowOnUnsupportedFeature);
    }

    /// <summary>
    ///     Validates that a scale that is not a positive finite number up to the surface limit is refused.
    /// </summary>
    [Fact]
    public void MermaidRenderOptions_Scale_Invalid_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidRenderOptions { Scale = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidRenderOptions { Scale = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MermaidRenderOptions { Scale = Surface.MaxDimension + 1 });
        Assert.Equal(2.5, new MermaidRenderOptions { Scale = 2.5 }.Scale);
    }

    /// <summary>
    ///     Validates that the five Mermaid themes are accepted and anything else is refused.
    /// </summary>
    [Fact]
    public void MermaidRenderOptions_Theme_OnlyMermaidThemes_AreAccepted()
    {
        // Arrange / Act / Assert
        Assert.Equal(["default", "base", "dark", "forest", "neutral"], MermaidRenderOptions.ThemeNames);
        foreach (var theme in MermaidRenderOptions.ThemeNames)
        {
            Assert.Equal(theme, new MermaidRenderOptions { Theme = theme }.Theme);
        }

        var ex = Assert.Throws<ArgumentException>(() => new MermaidRenderOptions { Theme = "Dark" });
        Assert.Contains("default, base, dark, forest, neutral", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the configuration must be a JSON object.
    /// </summary>
    [Fact]
    public void MermaidRenderOptions_Config_NotJsonObject_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => new MermaidRenderOptions { Config = "[1]" });
        Assert.Throws<ArgumentException>(() => new MermaidRenderOptions { Config = "{ not json" });
        Assert.Throws<ArgumentException>(() => new MermaidRenderOptions { Config = new string('[', 100) + new string(']', 100) });
        Assert.Equal("{\"theme\":\"dark\"}", new MermaidRenderOptions { Config = "{\"theme\":\"dark\"}" }.Config);
    }

    /// <summary>
    ///     Validates that the limits cannot be null.
    /// </summary>
    [Fact]
    public void MermaidRenderOptions_Limits_Null_Throws()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new MermaidRenderOptions { Limits = null! });
    }

    /// <summary>
    ///     Validates the open options: an empty source name is refused and the effective limits default.
    /// </summary>
    [Fact]
    public void MermaidOpenOptions_SourceName_Whitespace_Throws()
    {
        // Arrange
        var limits = new MermaidLimits { MaxTextSize = 10 };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => new MermaidOpenOptions { SourceName = " " });
        Assert.Equal("a.mmd", new MermaidOpenOptions { SourceName = "a.mmd" }.SourceName);
        Assert.Same(MermaidLimits.Default, new MermaidOpenOptions().EffectiveLimits);
        Assert.Same(limits, new MermaidOpenOptions { Limits = limits }.EffectiveLimits);
    }
}

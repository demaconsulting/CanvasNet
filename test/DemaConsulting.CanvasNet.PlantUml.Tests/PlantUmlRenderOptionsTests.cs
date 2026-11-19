namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Unit tests for <see cref="PlantUmlRenderOptions"/>: the defaults and the validation of every property.
/// </summary>
public class PlantUmlRenderOptionsTests
{
    /// <summary>
    ///     Validates the defaults of the options.
    /// </summary>
    [Fact]
    public void PlantUmlRenderOptions_Defaults_AreDocumented()
    {
        // Arrange / Act
        var options = new PlantUmlRenderOptions();

        // Assert
        Assert.Equal(1, options.Scale);
        Assert.Null(options.Background);
        Assert.Same(PlantUmlLimits.Default, options.Limits);
        Assert.Null(options.WarningHandler);
        Assert.Null(options.UnsupportedFeatureHandler);
        Assert.False(options.ThrowOnUnsupportedFeature);
    }

    /// <summary>
    ///     Validates that the scale must be finite, positive and at most the largest surface side, and the limits not null.
    /// </summary>
    [Fact]
    public void PlantUmlRenderOptions_Init_InvalidValues_Throw()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlRenderOptions { Scale = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlRenderOptions { Scale = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlRenderOptions { Scale = double.PositiveInfinity });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlantUmlRenderOptions { Scale = Canvas.Surface.MaxDimension + 1 });
        Assert.Throws<ArgumentNullException>(() => new PlantUmlRenderOptions { Limits = null! });
        Assert.Equal(2.5, new PlantUmlRenderOptions { Scale = 2.5 }.Scale);
    }
}

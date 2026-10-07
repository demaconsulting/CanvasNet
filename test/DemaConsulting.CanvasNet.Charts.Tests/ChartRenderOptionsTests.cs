using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Charts.Tests;

/// <summary>
///     Tests for <see cref="ChartRenderOptions"/>'s immutability guarantees.
/// </summary>
public class ChartRenderOptionsTests
{
    /// <summary>
    ///     Proves <see cref="ChartRenderOptions.ColorPalette"/> defensively copies the caller's
    ///     list at construction time, so a later mutation of the original, caller-owned array
    ///     cannot retroactively change an already-constructed, supposedly-immutable
    ///     <see cref="ChartRenderOptions"/> instance's own resolved palette.
    /// </summary>
    [Fact]
    public void ColorPalette_MutatedAfterConstruction_DoesNotAffectOptionsInstance()
    {
        // Arrange
        var original = new Rgba32[] { new(1, 2, 3, 255) };
        var options = new ChartRenderOptions { ColorPalette = original };

        // Act
        original[0] = new Rgba32(9, 9, 9, 255);

        // Assert
        Assert.Equal(new Rgba32(1, 2, 3, 255), options.ColorPalette[0]);
    }
}

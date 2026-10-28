using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the category colors.
/// </summary>
public class ColorSchemesTests
{
    /// <summary>
    ///     Validates that tableau10 holds the ten colors of the Vega-Lite default category palette in order, and that
    ///     categories cycle through them.
    /// </summary>
    [Fact]
    public void ColorSchemes_Tableau10_ReturnsDocumentedCategoryColors()
    {
        // Arrange: the documented palette
        string[] documented =
        [
            "#4c78a8", "#f58518", "#e45756", "#72b7b2", "#54a24b",
            "#eeca3b", "#b279a2", "#ff9da6", "#9d755d", "#bab0ac",
        ];

        // Act / Assert: the palette in category order
        Assert.Equal(documented, ColorSchemes.Tableau10);
        Assert.Equal("tableau10", ColorSchemes.Tableau10Name);
        for (var i = 0; i < documented.Length; i++)
        {
            Assert.Equal(documented[i], ColorSchemes.Tableau10Color(i));
        }

        // Assert: category i gets color i mod 10
        Assert.Equal("#4c78a8", ColorSchemes.Tableau10Color(10));
        Assert.Equal("#54a24b", ColorSchemes.Tableau10Color(24));
        Assert.Equal("#ff9da6", ColorSchemes.CategoryColor(ColorSchemes.Tableau10, 107));
        Assert.Equal("b", ColorSchemes.CategoryColor(["a", "b", "c"], 4));
    }

    /// <summary>
    ///     Validates the lookup of a scheme by name and the rejection of invalid arguments.
    /// </summary>
    [Fact]
    public void ColorSchemes_TryGetScheme_FindsOnlyTableau10IgnoringCase()
    {
        // Arrange / Act / Assert
        Assert.True(ColorSchemes.TryGetScheme("tableau10", out var colors));
        Assert.Equal(10, colors.Count);
        Assert.True(ColorSchemes.TryGetScheme("TABLEAU10", out _));
        Assert.False(ColorSchemes.TryGetScheme("blues", out var none));
        Assert.Empty(none);
        Assert.False(ColorSchemes.TryGetScheme(null, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorSchemes.Tableau10Color(-1));
        Assert.Throws<ArgumentException>(() => ColorSchemes.CategoryColor([], 0));
        Assert.Throws<ArgumentNullException>(() => ColorSchemes.CategoryColor(null!, 0));
    }
}

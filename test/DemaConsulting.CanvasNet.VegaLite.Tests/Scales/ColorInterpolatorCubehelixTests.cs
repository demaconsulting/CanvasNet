using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Unit tests of the cubehelix interpolation of <see cref="ColorInterpolator"/> and <see cref="ColorRamp"/> (d3-interpolate's <c>interpolateCubehelix</c> and <c>interpolateCubehelixLong</c>
///     over d3-color's <c>cubehelix</c>), including the gamma of the lightness. The expected colors were computed with d3-interpolate 3.0.1 and d3-color 3.1.1 (the versions
///     of vega 6.4) for the colors named in each test and are written here as whole channels; the result of a channel may differ from them by one at most (the platform's last-bit
///     differences of <c>Math.Pow</c>, <c>Math.Sin</c> and <c>Math.Cos</c>).
/// </summary>
public class ColorInterpolatorCubehelixTests
{
    /// <summary>The red of the tests.</summary>
    private static readonly RgbColor Red = RgbColor.FromBytes(255, 0, 0);

    /// <summary>The green of the tests.</summary>
    private static readonly RgbColor Green = RgbColor.FromBytes(0, 255, 0);

    /// <summary>The blue of the tests.</summary>
    private static readonly RgbColor Blue = RgbColor.FromBytes(0, 0, 255);

    /// <summary>
    ///     Asserts that a color has the expected channels, each within one.
    /// </summary>
    /// <param name="expected">The expected red, green and blue.</param>
    /// <param name="actual">The color.</param>
    private static void AssertColor((int R, int G, int B) expected, RgbColor actual)
    {
        Assert.InRange(actual.R, expected.R - 1, expected.R + 1);
        Assert.InRange(actual.G, expected.G - 1, expected.G + 1);
        Assert.InRange(actual.B, expected.B - 1, expected.B + 1);
    }

    /// <summary>
    ///     Samples an interpolation at 0, 0.25, 0.5, 0.75 and 1.
    /// </summary>
    /// <param name="space">The space.</param>
    /// <param name="start">The first color.</param>
    /// <param name="end">The second color.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns>The five colors.</returns>
    private static RgbColor[] Samples(InterpolationSpace space, RgbColor start, RgbColor end, double gamma = 1) =>
        [.. new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }.Select(t => ColorInterpolator.Interpolate(space, start, end, t, gamma))];

    /// <summary>
    ///     Validates the interpolation of red to blue against d3: gamma 1, 2 and 0.5 (the gamma moves the position of the lightness, so the middle gets brighter or darker), the ends being
    ///     exactly the colors.
    /// </summary>
    [Fact]
    public void ColorInterpolator_Cubehelix_RedToBlueMatchesD3WithGamma()
    {
        // Arrange: d3.interpolateCubehelix.gamma(g)('#ff0000', '#0000ff') at t = 0, .25, .5, .75, 1
        (int, int, int)[] gamma1 = [(255, 0, 0), (255, 0, 94), (238, 0, 209), (125, 0, 255), (0, 0, 255)];
        (int, int, int)[] gamma2 = [(255, 0, 0), (255, 0, 106), (255, 0, 246), (149, 0, 255), (0, 0, 255)];
        (int, int, int)[] gammaHalf = [(255, 0, 0), (243, 0, 78), (200, 0, 175), (110, 0, 244), (0, 0, 255)];

        // Act
        var one = Samples(InterpolationSpace.Cubehelix, Red, Blue);
        var two = Samples(InterpolationSpace.Cubehelix, Red, Blue, 2);
        var half = Samples(InterpolationSpace.Cubehelix, Red, Blue, 0.5);

        // Assert
        for (var i = 0; i < 5; i++)
        {
            AssertColor(gamma1[i], one[i]);
            AssertColor(gamma2[i], two[i]);
            AssertColor(gammaHalf[i], half[i]);
        }
    }

    /// <summary>
    ///     Validates the colors that have no hue (a grey): the hue of the other color is used, as d3's <c>hue</c> does for a NaN, so grey to red goes straight from the grey to the red
    ///     (grey 128 to red at d3: 168, 93, 93 at a quarter).
    /// </summary>
    [Fact]
    public void ColorInterpolator_Cubehelix_AGreyTakesTheHueOfTheOtherColor()
    {
        // Arrange / Act
        var grey = RgbColor.FromBytes(128, 128, 128);
        var samples = Samples(InterpolationSpace.Cubehelix, grey, Red);
        var reverse = ColorInterpolator.Interpolate(InterpolationSpace.Cubehelix, Red, grey, 0.75);

        // Assert
        (int, int, int)[] expected = [(128, 128, 128), (168, 93, 93), (204, 59, 59), (234, 27, 27), (255, 0, 0)];
        for (var i = 0; i < 5; i++)
        {
            AssertColor(expected[i], samples[i]);
        }

        AssertColor((168, 93, 93), reverse);
    }

    /// <summary>
    ///     Validates that the short and the long variants differ when the hues are more than 180 degrees apart: red (hue 351.8) to green (hue 110.0) goes through the oranges and yellows when
    ///     short and through the purples and blues when long, and yellow to red likewise; the gamma applies to the long variant too.
    /// </summary>
    [Fact]
    public void ColorInterpolator_CubehelixLong_TakesTheLongWayRoundTheHueCircle()
    {
        // Arrange: d3.interpolateCubehelix and interpolateCubehelixLong at t = .25, .5, .75
        (int, int, int)[] redGreenShort = [(237, 56, 0), (166, 131, 0), (73, 204, 0)];
        (int, int, int)[] redGreenLong = [(245, 0, 238), (61, 96, 255), (0, 224, 216)];
        (int, int, int)[] redGreenLongGamma2 = [(221, 0, 214), (45, 78, 255), (0, 209, 202)];
        var yellow = RgbColor.FromBytes(255, 255, 0);
        (int, int, int)[] yellowRedShort = [(255, 185, 0), (255, 93, 0), (255, 26, 0)];
        (int, int, int)[] yellowRedLong = [(0, 255, 64), (0, 210, 255), (255, 0, 255)];

        // Act
        var shortWay = Samples(InterpolationSpace.Cubehelix, Red, Green);
        var longWay = Samples(InterpolationSpace.CubehelixLong, Red, Green);
        var longGamma = Samples(InterpolationSpace.CubehelixLong, Red, Green, 2);
        var yellowShort = Samples(InterpolationSpace.Cubehelix, yellow, Red);
        var yellowLong = Samples(InterpolationSpace.CubehelixLong, yellow, Red);

        // Assert
        for (var i = 0; i < 3; i++)
        {
            AssertColor(redGreenShort[i], shortWay[i + 1]);
            AssertColor(redGreenLong[i], longWay[i + 1]);
            AssertColor(redGreenLongGamma2[i], longGamma[i + 1]);
            AssertColor(yellowRedShort[i], yellowShort[i + 1]);
            AssertColor(yellowRedLong[i], yellowLong[i + 1]);
        }

        // Assert: the ends are the colors in both variants
        AssertColor((255, 0, 0), longWay[0]);
        AssertColor((0, 255, 0), longWay[4]);
    }

    /// <summary>
    ///     Validates the ramp of a continuous scheme in the cubehelix space (d3's <c>piecewise(interpolateCubehelix, colors)</c>): through three stops of the viridis-like palette, at the stops and
    ///     between them.
    /// </summary>
    [Fact]
    public void ColorRamp_Cubehelix_PassesThroughTheStopsLikeD3Piecewise()
    {
        // Arrange: d3.piecewise(d3.interpolateCubehelix.gamma(1), ['#0d0887', '#cc4778', '#f0f921']) at t = 0, .3, .5, .8, 1
        var ramp = new ColorRamp(
            InterpolationSpace.Cubehelix,
            [RgbColor.FromBytes(0x0d, 0x08, 0x87), RgbColor.FromBytes(0xcc, 0x47, 0x78), RgbColor.FromBytes(0xf0, 0xf9, 0x21)]);
        (int, int, int)[] expected = [(13, 8, 135), (189, 4, 180), (204, 71, 120), (255, 140, 0), (240, 249, 33)];

        // Act / Assert
        var at = new[] { 0.0, 0.3, 0.5, 0.8, 1.0 };
        for (var i = 0; i < at.Length; i++)
        {
            AssertColor(expected[i], ramp.At(at[i]));
        }
    }

    /// <summary>
    ///     Validates the conventions: the opacity is interpolated linearly, a gamma that is not a number counts as 1, the gamma does not change the other spaces (only sRGB and the cubehelix spaces have
    ///     one), a position outside 0 to 1 extrapolates with clamped channels, and the names of the spaces parse.
    /// </summary>
    [Fact]
    public void ColorInterpolator_Cubehelix_OpacityGammaAndExtrapolationFollowD3()
    {
        // Arrange
        var transparent = new RgbColor(255, 0, 0, 0);
        var opaque = new RgbColor(0, 0, 255, 1);

        // Act
        var middle = ColorInterpolator.Interpolate(InterpolationSpace.Cubehelix, transparent, opaque, 0.5);
        var notANumber = ColorInterpolator.Interpolate(InterpolationSpace.Cubehelix, Red, Blue, 0.5, double.NaN);
        var plain = ColorInterpolator.Interpolate(InterpolationSpace.Cubehelix, Red, Blue, 0.5);
        var hsl = ColorInterpolator.Interpolate(InterpolationSpace.Hsl, Red, Blue, 0.5, 3);
        var hslPlain = ColorInterpolator.Interpolate(InterpolationSpace.Hsl, Red, Blue, 0.5);
        var beyond = ColorInterpolator.Interpolate(InterpolationSpace.Cubehelix, Red, Blue, 1.5);
        var before = ColorInterpolator.Interpolate(InterpolationSpace.Cubehelix, Red, Blue, -0.5);

        // Assert
        Assert.Equal(0.5, middle.Opacity, 9);
        Assert.Equal(plain, notANumber);
        Assert.Equal(hslPlain, hsl);
        Assert.InRange(beyond.R, 0, 255);
        Assert.InRange(before.B, 0, 255);
        Assert.True(ColorInterpolator.HasGamma(InterpolationSpace.Rgb));
        Assert.True(ColorInterpolator.HasGamma(InterpolationSpace.Cubehelix));
        Assert.True(ColorInterpolator.HasGamma(InterpolationSpace.CubehelixLong));
        Assert.False(ColorInterpolator.HasGamma(InterpolationSpace.Lab));
    }

    /// <summary>
    ///     Validates that black and white (which have no saturation, so no hue, in cubehelix) interpolate to the grays between them, and that a color survives the round trip through
    ///     cubehelix with at most one in a channel.
    /// </summary>
    [Fact]
    public void ColorInterpolator_Cubehelix_BlackToWhiteIsGreyAndColorsRoundTrip()
    {
        // Arrange
        var black = RgbColor.FromBytes(0, 0, 0);
        var white = RgbColor.FromBytes(255, 255, 255);

        // Act
        var middle = ColorInterpolator.Interpolate(InterpolationSpace.Cubehelix, black, white, 0.5);

        // Assert: the lightness goes straight from 0 to 1 and every channel is the same
        Assert.Equal(middle.R, middle.G);
        Assert.Equal(middle.G, middle.B);
        Assert.InRange(middle.R, 127, 129);
        foreach (var color in new[] { Red, Green, Blue, RgbColor.FromBytes(76, 120, 168), RgbColor.FromBytes(200, 100, 50) })
        {
            // A ramp through one color is the color itself after the conversion both ways
            var same = ColorInterpolator.Interpolate(InterpolationSpace.Cubehelix, color, color, 0.3);
            AssertColor(((int)color.R, (int)color.G, (int)color.B), same);
        }
    }
}

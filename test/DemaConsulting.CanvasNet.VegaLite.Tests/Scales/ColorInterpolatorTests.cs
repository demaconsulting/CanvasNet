using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the color interpolation: sRGB piecewise ramps, the HSL, CIELAB and HCL spaces, gamma, rounding and the
///     quantizing helpers. The expected values are derived by hand from the formulas (never copied from a library).
/// </summary>
public class ColorInterpolatorTests
{
    /// <summary>Opaque black.</summary>
    private static readonly RgbColor Black = RgbColor.FromBytes(0, 0, 0);

    /// <summary>Opaque white.</summary>
    private static readonly RgbColor White = RgbColor.FromBytes(255, 255, 255);

    /// <summary>Opaque red.</summary>
    private static readonly RgbColor Red = RgbColor.FromBytes(255, 0, 0);

    /// <summary>Opaque blue.</summary>
    private static readonly RgbColor Blue = RgbColor.FromBytes(0, 0, 255);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Asserts that two colors are equal channel by channel within a tolerance of whole 8-bit steps.
    /// </summary>
    /// <param name="expected">The expected channels.</param>
    /// <param name="actual">The actual color.</param>
    /// <param name="tolerance">The tolerance per channel.</param>
    private static void AssertNear((int R, int G, int B) expected, RgbColor actual, int tolerance)
    {
        Assert.InRange(actual.R, expected.R - tolerance, expected.R + tolerance);
        Assert.InRange(actual.G, expected.G - tolerance, expected.G + tolerance);
        Assert.InRange(actual.B, expected.B - tolerance, expected.B + tolerance);
    }

    /// <summary>
    ///     Validates the rounding (half away from zero), the clamping and the opacity of the sRGB interpolation.
    /// </summary>
    [Fact]
    public void ColorInterpolator_Rgb_RoundsHalfAwayAndClamps()
    {
        // Arrange / Act: black to white; 127.5 rounds up, 63.75 to 64, 191.25 to 191
        var half = ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 0.5);
        var quarter = ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 0.25);
        var threeQuarters = ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 0.75);

        // Assert
        Assert.Equal(new RgbColor(128, 128, 128, 1), half);
        Assert.Equal(64, quarter.R);
        Assert.Equal(191, threeQuarters.R);
        Assert.Equal("#808080", half.ToCss());

        // Assert: the ends are exact, positions outside 0 to 1 extrapolate and are clamped
        Assert.Equal(Black, ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 0));
        Assert.Equal(White, ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 1));
        Assert.Equal(White, ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 2));
        Assert.Equal(Black, ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, -1));

        // Assert: 10 + 0.25 * 10 = 12.5 rounds to 13, 30 + 0.25 * 30 = 37.5 rounds to 38
        var ramp = ColorInterpolator.Interpolate(
            InterpolationSpace.Rgb,
            RgbColor.FromBytes(10, 20, 30),
            RgbColor.FromBytes(20, 40, 60),
            0.25);
        Assert.Equal("#0d1926", ramp.ToCss());
        Assert.Equal((13, 25, 38), ((int)ramp.R, (int)ramp.G, (int)ramp.B));

        // Assert: the opacity is interpolated linearly and written as an alpha byte (0.5 * 255 = 127.5 rounds to 128)
        var faded = ColorInterpolator.Interpolate(InterpolationSpace.Rgb, RgbColor.FromBytes(0, 0, 0, 0), White, 0.5);
        Assert.Equal(0.5, faded.Opacity, 12);
        Assert.Equal("#80808080", faded.ToCss());
    }

    /// <summary>
    ///     Validates the HSL, CIELAB and HCL spaces against vectors derived by hand from the conversion formulas.
    /// </summary>
    [Fact]
    public void ColorInterpolator_Hsl_Lab_Hcl_Match_HandDerivedVectors()
    {
        // Assert: red (hue 0) to blue (hue 240): the shorter way goes through hue 300 (magenta), the long way through 120 (green)
        Assert.Equal(new RgbColor(255, 0, 255, 1), ColorInterpolator.Interpolate(InterpolationSpace.Hsl, Red, Blue, 0.5));
        Assert.Equal(new RgbColor(0, 255, 0, 1), ColorInterpolator.Interpolate(InterpolationSpace.HslLong, Red, Blue, 0.5));

        // Assert: the ends of every space return the colors (within one step for the nonlinear spaces)
        foreach (var space in Enum.GetValues<InterpolationSpace>())
        {
            AssertNear((255, 0, 0), ColorInterpolator.Interpolate(space, Red, Blue, 0), 1);
            AssertNear((0, 0, 255), ColorInterpolator.Interpolate(space, Red, Blue, 1), 1);
            AssertNear((18, 52, 86), ColorInterpolator.Interpolate(space, RgbColor.FromBytes(18, 52, 86), White, 0), 1);
        }

        // Assert: a grey has no hue, so grey to red keeps the hue of red; at 0.5 the saturation is 0.5 and the lightness
        // (128/255 + 0.5) / 2 = 0.50098, so m2 = 0.50098 + (1 - 0.50098) * 0.5 = 0.75049 (191.4) and m1 = 0.25147 (64.1)
        var greyToRed = ColorInterpolator.Interpolate(InterpolationSpace.Hsl, RgbColor.FromBytes(128, 128, 128), Red, 0.5);
        Assert.Equal((191, 64, 64), ((int)greyToRed.R, (int)greyToRed.G, (int)greyToRed.B));

        // Assert: black to white in CIELAB passes through L = 50, which is sRGB 118.9 (D50 white point), so 119
        AssertNear((119, 119, 119), ColorInterpolator.Interpolate(InterpolationSpace.Lab, Black, White, 0.5), 1);

        // Assert: greys have no hue and no chroma, so HCL gives the CIELAB result
        foreach (var t in new[] { 0.1, 0.5, 0.9 })
        {
            var lab = ColorInterpolator.Interpolate(InterpolationSpace.Lab, Black, White, t);
            AssertNear(((int)lab.R, (int)lab.G, (int)lab.B), ColorInterpolator.Interpolate(InterpolationSpace.Hcl, Black, White, t), 1);
            AssertNear(((int)lab.R, (int)lab.G, (int)lab.B), ColorInterpolator.Interpolate(InterpolationSpace.HclLong, Black, White, t), 1);
        }

        // Assert: CIELAB of one color (red) into the same color stays within one step at every position
        foreach (var t in new[] { 0d, 0.3, 1d })
        {
            AssertNear((255, 0, 0), ColorInterpolator.Interpolate(InterpolationSpace.Lab, Red, Red, t), 1);
            AssertNear((255, 0, 0), ColorInterpolator.Interpolate(InterpolationSpace.Hcl, Red, Red, t), 1);
        }

        // Assert: the HCL shorter way and the long way differ for colors whose hues are more than half a turn apart
        var shorter = ColorInterpolator.Interpolate(InterpolationSpace.Hcl, Red, Blue, 0.5);
        var longer = ColorInterpolator.Interpolate(InterpolationSpace.HclLong, Red, Blue, 0.5);
        Assert.NotEqual(shorter, longer);
    }

    /// <summary>
    ///     Validates that a gamma changes only the sRGB space, with the values raised to the gamma, interpolated and raised back.
    /// </summary>
    [Fact]
    public void ColorInterpolator_Gamma_AppliesToRgbOnly()
    {
        // Arrange / Act: black to white at the middle with gamma 2: sqrt((0 + 0.5 * 255^2)) = sqrt(32512.5) = 180.31
        var gamma = ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 0.5, 2);

        // Assert
        Assert.Equal(new RgbColor(180, 180, 180, 1), gamma);
        Assert.Equal(
            ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 0.5, 1),
            ColorInterpolator.Interpolate(InterpolationSpace.Rgb, Black, White, 0.5));

        // Assert: the other spaces ignore the gamma
        Assert.Equal(
            ColorInterpolator.Interpolate(InterpolationSpace.Hsl, Red, Blue, 0.5),
            ColorInterpolator.Interpolate(InterpolationSpace.Hsl, Red, Blue, 0.5, 2));

        // Assert: a ramp applies the gamma too
        var ramp = new ColorRamp(InterpolationSpace.Rgb, [Black, White], 2);
        Assert.Equal(gamma, ramp.At(0.5));
    }

    /// <summary>
    ///     Validates the piecewise segment choice, the extrapolation beyond the ends and the degenerate inputs.
    /// </summary>
    [Fact]
    public void ColorInterpolator_Piecewise_ChoosesTheSegmentAndExtrapolates()
    {
        // Arrange: black, red, yellow at positions 0, 0.5 and 1
        RgbColor[] stops = [Black, Red, RgbColor.FromBytes(255, 255, 0)];
        var ramp = new ColorRamp(InterpolationSpace.Rgb, stops);

        // Act / Assert: the stops themselves
        Assert.Equal(3, ramp.StopCount);
        Assert.Equal(Black, ramp.At(0));
        Assert.Equal(Red, ramp.At(0.5));
        Assert.Equal(RgbColor.FromBytes(255, 255, 0), ramp.At(1));

        // Assert: the middle of each segment (127.5 rounds up)
        Assert.Equal("#800000", ramp.CssAt(0.25));
        Assert.Equal("#ff8000", ramp.CssAt(0.75));
        Assert.Equal(ColorInterpolator.Piecewise(InterpolationSpace.Rgb, stops, 0.75), ramp.At(0.75));

        // Assert: outside 0 to 1 the first or last segment continues and the channels are clamped
        Assert.Equal(Black, ramp.At(-0.5));
        Assert.Equal(RgbColor.FromBytes(255, 255, 0), ramp.At(1.5));

        // Assert: NaN counts as 0 and the infinities as the ends
        Assert.Equal(Black, ramp.At(double.NaN));
        Assert.Equal(Black, ramp.At(double.NegativeInfinity));
        Assert.Equal(RgbColor.FromBytes(255, 255, 0), ramp.At(double.PositiveInfinity));

        // Assert: one color is a constant ramp; no color and no list are argument errors
        var single = new ColorRamp(InterpolationSpace.Rgb, [Red]);
        Assert.Equal(Red, single.At(0.3));
        Assert.Equal(Red, single.At(7));
        Assert.Throws<ArgumentException>(() => new ColorRamp(InterpolationSpace.Rgb, []));
        Assert.Throws<ArgumentNullException>(() => new ColorRamp(InterpolationSpace.Rgb, null!));
    }

    /// <summary>
    ///     Validates the range and quantize helpers: samples at i / (count + 1) without the end points, sub-ranges, reversal and cancellation.
    /// </summary>
    [Fact]
    public void ColorInterpolator_RangeAndQuantize_SampleWithoutTheEndPoints()
    {
        // Arrange: a black to white ramp
        var ramp = new ColorRamp(InterpolationSpace.Rgb, [Black, White]);

        // Act / Assert: three samples at 0.25, 0.5 and 0.75: 63.75, 127.5 and 191.25
        var samples = ColorInterpolator.Quantize(ramp.At, 3, Ct);
        Assert.Equal(["#404040", "#808080", "#bfbfbf"], samples.Select(c => c.ToCss()));
        Assert.Empty(ColorInterpolator.Quantize(ramp.At, 0, Ct));

        // Assert: a sub-range maps 0 to its start and 1 to its end; a descending sub-range reverses
        var upper = ColorInterpolator.Range(ramp.At, 0.5, 1);
        Assert.Equal("#808080", upper(0).ToCss());
        Assert.Equal("#ffffff", upper(1).ToCss());
        var reversed = ColorInterpolator.Range(ramp.At, 1, 0);
        Assert.Equal("#ffffff", reversed(0).ToCss());
        Assert.Equal("#000000", reversed(1).ToCss());

        // Assert: argument and cancellation checks
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorInterpolator.Quantize(ramp.At, -1, Ct));
        Assert.Throws<ArgumentNullException>(() => ColorInterpolator.Quantize(null!, 1, Ct));
        Assert.Throws<ArgumentNullException>(() => ColorInterpolator.Range(null!, 0, 1));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ColorInterpolator.Quantize(ramp.At, 10, cancelled.Token));
    }

    /// <summary>
    ///     Validates the conversions of a color to hexadecimal text, to a core pixel and back, and the parsing of hexadecimal colors.
    /// </summary>
    [Fact]
    public void ColorInterpolator_RgbColor_ConvertsAndParses()
    {
        // Arrange / Act / Assert: parsing with and without the number sign, in either case
        Assert.True(RgbColor.TryParseHex("#4C78a8", out var parsed));
        Assert.Equal(RgbColor.FromBytes(0x4c, 0x78, 0xa8), parsed);
        Assert.True(RgbColor.TryParseHex("ff0000", out var bare));
        Assert.Equal(Red, bare);
        Assert.False(RgbColor.TryParseHex("#fff", out _));
        Assert.False(RgbColor.TryParseHex("#gg0000", out _));
        Assert.False(RgbColor.TryParseHex("#ff00000", out _));
        Assert.False(RgbColor.TryParseHex(string.Empty, out _));

        // Assert: the pixel conversions and the text
        Assert.Equal("#4c78a8", parsed.ToCss());
        Assert.Equal(new Rgba32(0x4c, 0x78, 0xa8, 255), parsed.ToRgba32());
        Assert.Equal(parsed, RgbColor.FromRgba32(new Rgba32(0x4c, 0x78, 0xa8, 255)));
        Assert.Equal("#ff000080", RgbColor.FromRgba32(new Rgba32(255, 0, 0, 128)).ToCss());

        // Assert: out of range and NaN channels are clamped and zero
        Assert.Equal(new Rgba32(255, 0, 0, 255), new RgbColor(300, -5, double.NaN, 1).ToRgba32());

        // Assert: the names of the interpolation spaces
        Assert.True(ColorInterpolator.TryParseSpace("hcl-long", out var space));
        Assert.Equal(InterpolationSpace.HclLong, space);
        Assert.True(ColorInterpolator.TryParseSpace("rgb", out _));
        Assert.True(ColorInterpolator.TryParseSpace("hsl", out _));
        Assert.True(ColorInterpolator.TryParseSpace("hsl-long", out _));
        Assert.True(ColorInterpolator.TryParseSpace("lab", out _));
        Assert.True(ColorInterpolator.TryParseSpace("hcl", out _));
        Assert.False(ColorInterpolator.TryParseSpace("cubehelix", out _));
        Assert.False(ColorInterpolator.TryParseSpace(null, out _));
    }

    /// <summary>
    ///     Validates that the interpolation is deterministic: repeated and concurrent evaluation gives identical colors.
    /// </summary>
    [Fact]
    public void ColorInterpolator_Interpolation_IsDeterministic()
    {
        // Arrange: a ramp in every space over the same stops
        RgbColor[] stops = [Red, RgbColor.FromBytes(255, 200, 0), RgbColor.FromBytes(20, 130, 60), Blue];

        foreach (var space in Enum.GetValues<InterpolationSpace>())
        {
            var ramp = new ColorRamp(space, stops);
            var first = Enumerable.Range(0, 200).Select(i => ramp.At(i / 199d)).ToArray();

            // Act: evaluate again, concurrently, from several threads
            var again = Enumerable.Range(0, 200).Select(i => ramp.At(i / 199d)).ToArray();
            var parallel = new RgbColor[200];
            Parallel.For(0, 200, i => parallel[i] = ramp.At(i / 199d));

            // Assert
            Assert.Equal(first, again);
            Assert.Equal(first, parallel);
        }
    }
}

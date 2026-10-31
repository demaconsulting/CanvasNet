using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the scale model: linear, band, point and ordinal mappings against positions computed by hand from the
///     d3-scale formulas.
/// </summary>
public class ScaleModelTests
{
    /// <summary>The tolerance for positions computed with fractions.</summary>
    private const int Precision = 9;

    /// <summary>
    ///     Builds string categories.
    /// </summary>
    /// <param name="names">The category names.</param>
    /// <returns>The categories as data values.</returns>
    private static DataValue[] Categories(params string[] names) => [.. names.Select(DataValue.FromString)];

    /// <summary>
    ///     Validates the piecewise (polylinear) linear scale against values derived by hand from d3-scale's
    ///     <c>continuous.js</c> (<c>polymap</c>, <c>bimap</c>, <c>bisectRight</c>, <c>normalize</c>, <c>clamper</c>): the segment
    ///     that a value picks, extrapolation, clamping, inversion, descending domains, truncation to the shorter of domain and
    ///     range, zero-width segments, rounding and invalid arguments.
    /// </summary>
    [Fact]
    public void ScaleModel_Map_PiecewiseLinearFollowsD3Polymap()
    {
        // Arrange: domain 0..10..100 onto range 0..50..100: the first segment is 5 per unit, the second 0.5666... per unit
        var scale = ScaleModel.CreateLinear([0, 10, 100], [0, 50, 100]);

        // Assert: 5 is halfway through the first segment (25); 10 is the start of the second (bisectRight sends an equal
        // value up), 55 is halfway through the second (75); the ends are the ends of the range
        Assert.Equal(25, scale.Map(5), Precision);
        Assert.Equal(50, scale.Map(10), Precision);
        Assert.Equal(75, scale.Map(55), Precision);
        Assert.Equal(0, scale.Map(0), Precision);
        Assert.Equal(100, scale.Map(100), Precision);

        // Assert: outside the domain the end segments continue: -10 is one whole first segment below (-50), 200 is
        // t = 190/90 through the second segment: 50 * (1 - 19/9) + 100 * 19/9 = 1400 / 9
        Assert.Equal(-50, scale.Map(-10), Precision);
        Assert.Equal(1400.0 / 9, scale.Map(200), Precision);
        Assert.True(double.IsNaN(scale.Map(double.NaN)));

        // Assert: the complete arrays give the ends that axes and ticks use
        Assert.Equal((0.0, 100.0, 0.0, 100.0), (scale.DomainStart, scale.DomainEnd, scale.RangeStart, scale.RangeEnd));

        // Assert: the inverse is the polymap of the range onto the domain: 25 is halfway through the first segment (5), 75
        // halfway through the second (55); 50 belongs to the second segment, where it is the start (10)
        Assert.Equal(5, scale.Invert(25), Precision);
        Assert.Equal(55, scale.Invert(75), Precision);
        Assert.Equal(10, scale.Invert(50), Precision);

        // Assert: clamp limits to the first and last domain entry before mapping, and the inverse result to the same
        var clamped = ScaleModel.CreateLinear([0, 10, 100], [0, 50, 100], clamp: true);
        Assert.Equal(0, clamped.Map(-10), Precision);
        Assert.Equal(100, clamped.Map(200), Precision);
        Assert.Equal(75, clamped.Map(55), Precision);
        Assert.Equal(100, clamped.Invert(5000), Precision);

        // Assert: a descending domain reverses both arrays: domain 100 -> range 0, 10 -> 50, 0 -> 100. Value 5 is halfway
        // from 100 down to 50 (75); 55 halfway from 50 down to 0 (25)
        var descending = ScaleModel.CreateLinear([100, 10, 0], [0, 50, 100]);
        Assert.Equal(75, descending.Map(5), Precision);
        Assert.Equal(25, descending.Map(55), Precision);
        Assert.Equal(0, descending.Map(100), Precision);
        Assert.Equal(100, descending.Map(0), Precision);
        Assert.Equal(55, descending.Invert(25), Precision);

        // Assert: a range that descends gives a descending scale with the same segments
        var descendingRange = ScaleModel.CreateLinear([0, 10, 100], [100, 50, 0]);
        Assert.Equal(75, descendingRange.Map(5), Precision);
        Assert.Equal(25, descendingRange.Map(55), Precision);
        Assert.Equal(5, descendingRange.Invert(75), Precision);
    }

    /// <summary>
    ///     Validates that a domain and a range of different lengths use the first <c>min(length)</c> entries, as d3 does, and
    ///     that the descending case reverses the complete arrays first.
    /// </summary>
    [Fact]
    public void ScaleModel_Map_PiecewiseTruncatesToTheShorterOfDomainAndRange()
    {
        // Arrange: three domain entries but two range entries: d3 uses bimap over domain[0..1] and range[0..1] only
        var longDomain = ScaleModel.CreateLinear([0, 10, 100], [0, 200]);

        // Assert: 5 is halfway through 0..10 (100) and 100 continues the line (t = 10 gives 2000); the complete arrays
        // still give the ends d3 reports, but clamp uses domain[0] and domain[1]
        Assert.Equal(100, longDomain.Map(5), Precision);
        Assert.Equal(2000, longDomain.Map(100), Precision);
        Assert.Equal((0.0, 100.0, 0.0, 200.0), (longDomain.DomainStart, longDomain.DomainEnd, longDomain.RangeStart, longDomain.RangeEnd));
        Assert.Equal(200, ScaleModel.CreateLinear([0, 10, 100], [0, 200], clamp: true).Map(100), Precision);
        Assert.Equal(5, longDomain.Invert(100), Precision);

        // Arrange: two domain entries and three range entries: only range[0..1] is used, 0..10 onto 0..50
        var longRange = ScaleModel.CreateLinear([0, 10], [0, 50, 100]);
        Assert.Equal(25, longRange.Map(5), Precision);
        Assert.Equal(50, longRange.Map(10), Precision);
        Assert.Equal(100, longRange.RangeEnd);

        // Arrange: four domain entries and three range entries: polymap over the first three entries of each
        var fourByThree = ScaleModel.CreateLinear([0, 10, 100, 1000], [0, 1, 2]);
        Assert.Equal(2, fourByThree.Map(100), Precision);
        Assert.Equal(0.5, fourByThree.Map(5), Precision);

        // Assert: beyond the last used entry the last segment continues: t = 990/90 = 11, 1 * (1 - 11) + 2 * 11 = 12
        Assert.Equal(12, fourByThree.Map(1000), Precision);
        Assert.Equal(2, ScaleModel.CreateLinear([0, 10, 100, 1000], [0, 1, 2], clamp: true).Map(1000), Precision);

        // Assert: d3 reverses the complete arrays when the last used domain entry is below the first, then uses the first
        // three of the reversed arrays: domain [10, 5, 0, -1] becomes [-1, 0, 5, 10] and range [0, 1, 2] becomes [2, 1, 0], so
        // the used pairs are -1 -> 2, 0 -> 1, 5 -> 0. (This is d3's behavior, kept as it is.)
        var quirk = ScaleModel.CreateLinear([10, 5, 0, -1], [0, 1, 2]);
        Assert.Equal(2, quirk.Map(-1), Precision);
        Assert.Equal(1, quirk.Map(0), Precision);
        Assert.Equal(0, quirk.Map(5), Precision);
        Assert.Equal(-1, quirk.Map(10), Precision);
    }

    /// <summary>
    ///     Validates zero-width segments, rounding and argument checks of the piecewise scale.
    /// </summary>
    [Fact]
    public void ScaleModel_CreateLinear_PiecewiseEdgeCases()
    {
        // Assert: a segment of zero width maps to the middle of its output segment (d3's normalize returns 0.5); the equal
        // value 0 belongs to the next segment, so it is the start of 50..100
        var flat = ScaleModel.CreateLinear([0, 0, 10], [0, 50, 100]);
        Assert.Equal(25, flat.Map(-1), Precision);
        Assert.Equal(50, flat.Map(0), Precision);
        Assert.Equal(75, flat.Map(5), Precision);

        // Assert: round applies to the result of the segment: 1 is a third of the way to 10 in the first segment (3.33 -> 3)
        var rounded = ScaleModel.CreateLinear([0, 3, 9], [0, 10, 40], round: true);
        Assert.Equal(3, rounded.Map(1));
        Assert.Equal(25, rounded.Map(6));

        // Assert: both arrays need two entries, and neither may be null
        Assert.Throws<ArgumentException>(() => ScaleModel.CreateLinear([1], [0, 1]));
        Assert.Throws<ArgumentException>(() => ScaleModel.CreateLinear([0, 1], [1]));
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateLinear(null!, [0, 1]));
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateLinear([0, 1], null!));
    }

    /// <summary>
    ///     Validates the positions of linear, band and point scales against hand-derived values: linear interpolation,
    ///     reversed ranges and domains, clamping, rounding, bands with padding, alignment and rounding, and points.
    /// </summary>
    [Fact]
    public void ScaleModel_Map_LinearBandAndPointMatchAnalyticPositions()
    {
        // Arrange / Act / Assert: linear 0..10 onto 0..100 is a factor of ten, without clamping it extrapolates
        var linear = ScaleModel.CreateLinear(0, 10, 0, 100);
        Assert.Equal(50, linear.Map(5));
        Assert.Equal(0, linear.Map(0));
        Assert.Equal(100, linear.Map(10));
        Assert.Equal(120, linear.Map(12));
        Assert.Equal(25, linear.Map(DataValue.FromString("2.5")));
        Assert.Null(linear.Map(DataValue.FromString("abc")));
        Assert.True(double.IsNaN(linear.Map(double.NaN)));
        Assert.Equal(7.5, linear.Invert(75));

        // Assert: a clamped scale stops at the ends of the domain and inverts to the domain
        var clamped = ScaleModel.CreateLinear(0, 10, 0, 100, clamp: true);
        Assert.Equal(100, clamped.Map(12));
        Assert.Equal(0, clamped.Map(-3));
        Assert.Equal(10, clamped.Invert(500));

        // Assert: a y scale runs from the bottom (height) to the top (0)
        var y = ScaleModel.CreateLinear(0, 10, 200, 0);
        Assert.Equal(150, y.Map(2.5));
        Assert.Equal(200, y.Map(0));
        Assert.Equal(0, y.Map(10));
        Assert.Equal(2.5, y.Invert(150));

        // Assert: a reversed domain maps the large value to the start of the range
        var reversedDomain = ScaleModel.CreateLinear(10, 0, 0, 100);
        Assert.Equal(0, reversedDomain.Map(10));
        Assert.Equal(100, reversedDomain.Map(0));
        Assert.Equal(75, reversedDomain.Map(2.5));
        Assert.Equal(2.5, reversedDomain.Invert(75));

        // Assert: a domain of zero width maps to the middle of the range, and so does a range of zero width on inversion
        Assert.Equal(50, ScaleModel.CreateLinear(5, 5, 0, 100).Map(5));
        Assert.Equal(5, ScaleModel.CreateLinear(0, 10, 40, 40).Invert(40));

        // Assert: rounding applies to the results (1/3 of 10 is 3.33, 2/3 is 6.67)
        var rounded = ScaleModel.CreateLinear(0, 3, 0, 10, round: true);
        Assert.Equal(3, rounded.Map(1));
        Assert.Equal(7, rounded.Map(2));

        // Assert: three bands without padding split 300 into thirds
        var bands = ScaleModel.CreateBand(Categories("a", "b", "c"), 0, 300);
        Assert.Equal(100, bands.Step);
        Assert.Equal(100, bands.Bandwidth);
        Assert.Equal([0.0, 100.0, 200.0], new[] { "a", "b", "c" }.Select(c => bands.Map(DataValue.FromString(c))!.Value).ToArray());
        Assert.Equal(50, bands.MapCenter(DataValue.FromString("a")));
        Assert.Null(bands.Map(DataValue.FromString("zzz")));
        Assert.Equal(2, bands.IndexOf(DataValue.FromString("c")));
        Assert.Equal(-1, bands.IndexOf(DataValue.FromString("zzz")));

        // Assert: Vega-Lite's bar padding (inner 0.1, outer 0.05) makes the space 3 - 0.1 + 0.1 = 3 steps of 100; the bands
        // are 90 wide and start 5 pixels in, because (300 - 100 * 2.9) / 2 = 5
        var bars = ScaleModel.CreateBand(Categories("a", "b", "c"), 0, 300, 0.1, 0.05);
        Assert.Equal(100, bars.Step, Precision);
        Assert.Equal(90, bars.Bandwidth, Precision);
        Assert.Equal(5, bars.Map(DataValue.FromString("a"))!.Value, Precision);
        Assert.Equal(105, bars.Map(DataValue.FromString("b"))!.Value, Precision);
        Assert.Equal(205, bars.Map(DataValue.FromString("c"))!.Value, Precision);
        Assert.Equal(50, bars.MapCenter(DataValue.FromString("a"))!.Value, Precision);

        // Assert: a reversed range lays the bands out from the other end
        var reversedBands = ScaleModel.CreateBand(Categories("a", "b", "c"), 300, 0, 0.1, 0.05);
        Assert.Equal(205, reversedBands.Map(DataValue.FromString("a"))!.Value, Precision);
        Assert.Equal(105, reversedBands.Map(DataValue.FromString("b"))!.Value, Precision);
        Assert.Equal(5, reversedBands.Map(DataValue.FromString("c"))!.Value, Precision);

        // Assert: rounding floors the step (100/3 = 33.3 -> 33) and rounds the start ((100 - 99) / 2 = 0.5 -> 1, half up)
        var roundedBands = ScaleModel.CreateBand(Categories("a", "b", "c"), 0, 100, round: true);
        Assert.Equal(33, roundedBands.Step);
        Assert.Equal(33, roundedBands.Bandwidth);
        Assert.Equal([1.0, 34.0, 67.0], new[] { "a", "b", "c" }.Select(c => roundedBands.Map(DataValue.FromString(c))!.Value).ToArray());

        // Assert: alignment moves the leftover pixel: 0 keeps the bands at the start, 1 at the end
        Assert.Equal(0, ScaleModel.CreateBand(Categories("a", "b", "c"), 0, 100, align: 0, round: true).Map(DataValue.FromString("a")));
        Assert.Equal(1, ScaleModel.CreateBand(Categories("a", "b", "c"), 0, 100, align: 1, round: true).Map(DataValue.FromString("a")));

        // Assert: three points with half a step of padding sit at (i + 0.5) * 100 / 3
        var points = ScaleModel.CreatePoint(Categories("a", "b", "c"), 0, 100, 0.5);
        Assert.Equal(100.0 / 3, points.Step, Precision);
        Assert.Equal(0, points.Bandwidth);
        Assert.Equal(100.0 / 6, points.Map(DataValue.FromString("a"))!.Value, Precision);
        Assert.Equal(50, points.Map(DataValue.FromString("b"))!.Value, Precision);
        Assert.Equal(500.0 / 6, points.Map(DataValue.FromString("c"))!.Value, Precision);
        Assert.Equal(1, points.PaddingInner);
        Assert.Equal(points.Map(DataValue.FromString("a")), points.MapCenter(DataValue.FromString("a")));

        // Assert: points without padding reach both ends, and a single point is centered
        var tight = ScaleModel.CreatePoint(Categories("a", "b", "c"), 0, 100);
        Assert.Equal([0.0, 50.0, 100.0], new[] { "a", "b", "c" }.Select(c => tight.Map(DataValue.FromString(c))!.Value).ToArray());
        Assert.Equal(50, ScaleModel.CreatePoint(Categories("only"), 0, 100, 0.5).Map(DataValue.FromString("only")));

        // Assert: duplicate categories are ignored and numbers are distinct from strings
        var mixed = ScaleModel.CreateBand([DataValue.FromNumber(1), DataValue.FromString("1"), DataValue.FromNumber(1)], 0, 20);
        Assert.Equal(2, mixed.Domain.Count);
        Assert.Equal(10, mixed.Map(DataValue.FromString("1")));
        Assert.Equal(0, mixed.Map(DataValue.FromNumber(1)));

        // Assert: an empty domain maps nothing
        Assert.Null(ScaleModel.CreateBand([], 0, 100).Map(DataValue.FromString("a")));
    }

    /// <summary>
    ///     Validates the ordinal scale: categories cycle through a shorter range and unknown values map to nothing.
    /// </summary>
    [Fact]
    public void ScaleModel_MapOrdinal_CyclesThroughTheRange()
    {
        // Arrange: five categories and a range of two colors
        var scale = ScaleModel.CreateOrdinal(Categories("a", "b", "c", "d", "e"), ["red", "blue"]);

        // Act / Assert
        Assert.Equal(["red", "blue", "red", "blue", "red"], new[] { "a", "b", "c", "d", "e" }.Select(c => scale.MapOrdinal(DataValue.FromString(c))).ToArray());
        Assert.Null(scale.MapOrdinal(DataValue.FromString("zzz")));
        Assert.Null(ScaleModel.CreateOrdinal(Categories("a"), []).MapOrdinal(DataValue.FromString("a")));
        Assert.Equal(ScaleKind.Ordinal, scale.Kind);
        Assert.Equal(["red", "blue"], scale.OrdinalRange);
    }

    /// <summary>
    ///     Validates the number of steps a band or point scale needs for a given step (Vega's bandspace).
    /// </summary>
    [Fact]
    public void ScaleModel_BandSpace_MatchesVega()
    {
        // Arrange / Act / Assert: count - paddingInner + 2 * paddingOuter, at least 1, zero without categories
        Assert.Equal(3, ScaleModel.BandSpace(3, 0.1, 0.05), Precision);
        Assert.Equal(4, ScaleModel.BandSpace(4, 1, 0.5), Precision);
        Assert.Equal(1, ScaleModel.BandSpace(1, 1, 0));
        Assert.Equal(0, ScaleModel.BandSpace(0, 0.1, 0.05));
        Assert.Equal(5, ScaleModel.BandSpace(5, 0, 0));
    }

    /// <summary>
    ///     Validates that operations of the wrong kind of scale are rejected.
    /// </summary>
    [Fact]
    public void ScaleModel_WrongKind_Throws()
    {
        // Arrange
        var band = ScaleModel.CreateBand(Categories("a"), 0, 10);
        var linear = ScaleModel.CreateLinear(0, 1, 0, 1);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => band.Map(1.0));
        Assert.Throws<InvalidOperationException>(() => band.Invert(1));
        Assert.Throws<InvalidOperationException>(() => linear.MapOrdinal(DataValue.FromString("a")));
        Assert.Null(linear.MapCenter(DataValue.FromString("x")));
        Assert.Equal(ScaleKind.Band, band.Kind);
        Assert.Equal(ScaleKind.Point, ScaleModel.CreatePoint(Categories("a"), 0, 1).Kind);
        Assert.Throws<InvalidOperationException>(() => linear.MapColor(1));
        Assert.Throws<ArgumentException>(() => ScaleModel.CreateSequential([0, 1, 2, 3], t => default));
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateSequential([0, 1], null!));
        Assert.Null(linear.MapText(DataValue.FromNumber(1)));
        Assert.Equal(-1, linear.MapIndex(DataValue.FromNumber(1)));
    }

    /// <summary>The red channel of a color, which the probe interpolators below set to 100 times the position.</summary>
    /// <param name="color">The color of a probe interpolator.</param>
    /// <returns>The position times 100.</returns>
    private static double Position(RgbColor? color) => color!.Value.R;

    /// <summary>An interpolator that exposes the position it is asked for as the red channel (100 times the position).</summary>
    /// <param name="t">The position.</param>
    /// <returns>The probe color.</returns>
    private static RgbColor Probe(double t) => new(t * 100, 0, 0, 1);

    /// <summary>
    ///     Validates the sequential scale against d3's <c>scaleSequential</c>: <c>t = (x - x0) / (x1 - x0)</c> without clamping beyond
    ///     the domain, clamping on request, a reversed domain, a domain of zero width (the middle of the interpolator), and invalid input.
    /// </summary>
    [Fact]
    public void ScaleModel_Sequential_MapsThroughTheInterpolator()
    {
        // Arrange: the domain 0..10 with the probe interpolator
        var scale = ScaleModel.CreateSequential([0, 10], Probe);

        // Assert: t is the fraction of the domain; beyond it the position extrapolates
        Assert.Equal(ScaleKind.Sequential, scale.Kind);
        Assert.Equal((0.0, 10.0), (scale.DomainStart, scale.DomainEnd));
        Assert.Equal(0, Position(scale.MapColor(0)), Precision);
        Assert.Equal(50, Position(scale.MapColor(5)), Precision);
        Assert.Equal(100, Position(scale.MapColor(10)), Precision);
        Assert.Equal(200, Position(scale.MapColor(20)), Precision);
        Assert.Equal(-100, Position(scale.MapColor(-10)), Precision);
        Assert.Null(scale.MapColor(double.NaN));

        // Assert: an offset domain 10..20: 15 is the middle
        Assert.Equal(50, Position(ScaleModel.CreateSequential([10, 20], Probe).MapColor(15)), Precision);

        // Assert: clamping limits the position to 0..1
        var clamped = ScaleModel.CreateSequential([0, 10], Probe, clamp: true);
        Assert.Equal(100, Position(clamped.MapColor(20)), Precision);
        Assert.Equal(0, Position(clamped.MapColor(-10)), Precision);
        Assert.Equal(50, Position(clamped.MapColor(5)), Precision);

        // Assert: a reversed domain 10..0 runs the interpolator backwards
        var reversed = ScaleModel.CreateSequential([10, 0], Probe);
        Assert.Equal(0, Position(reversed.MapColor(10)), Precision);
        Assert.Equal(100, Position(reversed.MapColor(0)), Precision);
        Assert.Equal(75, Position(reversed.MapColor(2.5)), Precision);

        // Assert: a domain of zero width gives the middle of the interpolator for every number, with or without clamping
        var flat = ScaleModel.CreateSequential([3, 3], Probe);
        Assert.Equal(50, Position(flat.MapColor(3)), Precision);
        Assert.Equal(50, Position(flat.MapColor(99)), Precision);
        Assert.Equal(50, Position(ScaleModel.CreateSequential([3, 3], Probe, clamp: true).MapColor(-7)), Precision);

        // Assert: data values are read as numbers (numeric text too), anything else has no color; the color is formatted as CSS
        var gray = ScaleModel.CreateSequential([0, 10], new ColorRamp(InterpolationSpace.Rgb, [RgbColor.FromBytes(0, 0, 0), RgbColor.FromBytes(255, 255, 255)]).At);
        Assert.Equal("#808080", gray.MapText(DataValue.FromNumber(5)));
        Assert.Equal("#808080", gray.MapText(DataValue.FromString("5")));
        Assert.Equal("#000000", gray.MapText(DataValue.FromNumber(0)));
        Assert.Null(gray.MapText(DataValue.FromString("five")));
        Assert.Null(gray.MapText(DataValue.Null));
        Assert.Null(gray.Map(DataValue.FromNumber(5)));
    }

    /// <summary>
    ///     Validates the diverging scale against d3's <c>scaleDiverging</c>: the two halves of the domain have their own slope around the
    ///     middle value, a descending domain swaps the sides, equal entries give the middle for the side they cover, and clamping.
    /// </summary>
    [Fact]
    public void ScaleModel_Diverging_UsesTheTwoSlopes()
    {
        // Arrange: a symmetric domain 0..50..100: 0.01 of the interpolator per unit on both sides
        var symmetric = ScaleModel.CreateSequential([0, 50, 100], Probe);

        // Assert: the middle value is the middle of the interpolator, the ends are its ends, the quarters its quarters
        Assert.Equal(ScaleKind.Diverging, symmetric.Kind);
        Assert.Equal((0.0, 50.0, 100.0), (symmetric.DomainStart, symmetric.DomainMid, symmetric.DomainEnd));
        Assert.Equal(50, Position(symmetric.MapColor(50)), Precision);
        Assert.Equal(0, Position(symmetric.MapColor(0)), Precision);
        Assert.Equal(100, Position(symmetric.MapColor(100)), Precision);
        Assert.Equal(25, Position(symmetric.MapColor(25)), Precision);
        Assert.Equal(75, Position(symmetric.MapColor(75)), Precision);

        // Assert: an asymmetric domain 0..10..100: 0.05 per unit below the middle, 0.5 / 90 above it
        var asymmetric = ScaleModel.CreateSequential([0, 10, 100], Probe);
        Assert.Equal(25, Position(asymmetric.MapColor(5)), Precision);
        Assert.Equal(50, Position(asymmetric.MapColor(10)), Precision);
        Assert.Equal(75, Position(asymmetric.MapColor(55)), Precision);
        Assert.Equal(100, Position(asymmetric.MapColor(100)), Precision);
        Assert.Equal(-25, Position(asymmetric.MapColor(-5)), Precision);

        // Assert: a descending domain 100..50..0 maps 100 to 0 and 0 to 1 (the slopes are negative on both sides)
        var descending = ScaleModel.CreateSequential([100, 50, 0], Probe);
        Assert.Equal(0, Position(descending.MapColor(100)), Precision);
        Assert.Equal(50, Position(descending.MapColor(50)), Precision);
        Assert.Equal(100, Position(descending.MapColor(0)), Precision);
        Assert.Equal(25, Position(descending.MapColor(75)), Precision);

        // Assert: the first two entries equal: everything below the middle is the middle color, above it the upper slope applies
        var flatLower = ScaleModel.CreateSequential([5, 5, 15], Probe);
        Assert.Equal(50, Position(flatLower.MapColor(3)), Precision);
        Assert.Equal(75, Position(flatLower.MapColor(10)), Precision);

        // Assert: clamping limits the position to 0..1: 200 would be 2
        Assert.Equal(200, Position(symmetric.MapColor(200)), Precision);
        Assert.Equal(100, Position(ScaleModel.CreateSequential([0, 50, 100], Probe, clamp: true).MapColor(200)), Precision);
        Assert.Null(symmetric.MapColor(double.NaN));
    }

    /// <summary>
    ///     Validates the quantize scale against d3's <c>scaleQuantize</c>: equal-width intervals with thresholds
    ///     <c>((i + 1) * x1 - (i - n) * x0) / (n + 1)</c>, values on a threshold going up, values outside the domain taking the end values.
    /// </summary>
    [Fact]
    public void ScaleModel_Quantize_ThresholdsAreEqualWidth()
    {
        // Arrange: the domain 0..10 cut into four intervals: thresholds 2.5, 5 and 7.5
        var scale = ScaleModel.CreateQuantize(0, 10, ScaleRange.OfTexts(["a", "b", "c", "d"]));

        // Assert: the thresholds and the interval of each value (a value on a threshold belongs to the interval above it)
        Assert.Equal(ScaleKind.Quantize, scale.Kind);
        Assert.Equal([2.5, 5, 7.5], scale.Thresholds);
        Assert.Equal(["a", "a", "b", "c", "d", "d"], new[] { 0.0, 2.4, 2.5, 5, 7.5, 10 }.Select(v => scale.MapText(DataValue.FromNumber(v))).ToArray());

        // Assert: values outside the domain take the first or the last value; text that is not a number has none
        Assert.Equal("a", scale.MapText(DataValue.FromNumber(-5)));
        Assert.Equal("d", scale.MapText(DataValue.FromNumber(15)));
        Assert.Null(scale.MapText(DataValue.FromString("n/a")));
        Assert.Null(scale.MapText(DataValue.Null));
        Assert.Equal(2, scale.MapIndex(DataValue.FromNumber(6)));
        Assert.Equal(-1, scale.MapIndex(DataValue.FromNumber(double.NaN)));

        // Assert: numbers as the range, and an offset domain 100..200 in two intervals with the threshold 150
        var numbers = ScaleModel.CreateQuantize(100, 200, ScaleRange.OfNumbers([1, 2]));
        Assert.Equal([150.0], numbers.Thresholds);
        Assert.Equal((1.0, 2.0), (numbers.Map(DataValue.FromNumber(149))!.Value, numbers.Map(DataValue.FromNumber(150))!.Value));
        Assert.Null(numbers.MapText(DataValue.FromNumber(120)));

        // Assert: a single value has no threshold, an empty range maps nothing, and a count-only range has indices
        Assert.Empty(ScaleModel.CreateQuantize(0, 10, ScaleRange.OfTexts(["only"])).Thresholds);
        Assert.Equal("only", ScaleModel.CreateQuantize(0, 10, ScaleRange.OfTexts(["only"])).MapText(DataValue.FromNumber(7)));
        Assert.Null(ScaleModel.CreateQuantize(0, 10, ScaleRange.OfTexts([])).MapText(DataValue.FromNumber(7)));
        Assert.Equal(1, ScaleModel.CreateQuantize(0, 10, ScaleRange.OfCount(2)).MapIndex(DataValue.FromNumber(9)));
    }

    /// <summary>
    ///     Validates the quantile scale against d3's <c>scaleQuantile</c>: the thresholds are the R-7 quantiles <c>i / n</c> of the sorted
    ///     values (linear interpolation between the closest ranks), a value on a threshold goes up, and one group per range value.
    /// </summary>
    [Fact]
    public void ScaleModel_Quantile_UsesR7Thresholds()
    {
        // Arrange: eight values and four groups: the quartiles of 1..8 by R-7 are 1 + 7 * 0.25 = 2.75, 4.5 and 6.25
        var scale = ScaleModel.CreateQuantile([1, 2, 3, 4, 5, 6, 7, 8], ScaleRange.OfTexts(["q1", "q2", "q3", "q4"]));

        // Assert
        Assert.Equal(ScaleKind.Quantile, scale.Kind);
        Assert.Equal([2.75, 4.5, 6.25], scale.Thresholds);
        Assert.Equal((1.0, 8.0), (scale.DomainStart, scale.DomainEnd));
        Assert.Equal(
            ["q1", "q1", "q2", "q2", "q3", "q3", "q4", "q4"],
            new[] { 1.0, 2.7, 2.75, 4.4, 4.5, 6.2, 6.25, 100 }.Select(v => scale.MapText(DataValue.FromNumber(v))).ToArray());

        // Assert: two groups cut at the median (4.5); one group has no threshold
        Assert.Equal([4.5], ScaleModel.CreateQuantile([1, 2, 3, 4, 5, 6, 7, 8], ScaleRange.OfCount(2)).Thresholds);
        Assert.Empty(ScaleModel.CreateQuantile([1, 2, 3], ScaleRange.OfCount(1)).Thresholds);
        Assert.Empty(ScaleModel.CreateQuantile([1, 2, 3], ScaleRange.OfCount(0)).Thresholds);

        // Assert: repeated values and a single value; five groups of 0, 10, 10, 10, 100 have the quintiles 8, 10, 10 and 28
        var repeated = ScaleModel.CreateQuantile([0, 10, 10, 10, 100], ScaleRange.OfCount(5)).Thresholds;
        Assert.Equal(4, repeated.Count);
        Assert.Equal(8, repeated[0], Precision);
        Assert.Equal(10, repeated[1], Precision);
        Assert.Equal(10, repeated[2], Precision);
        Assert.Equal(28, repeated[3], Precision);
        Assert.Equal([7.0, 7], ScaleModel.CreateQuantile([7], ScaleRange.OfCount(3)).Thresholds);

        // Assert: invalid arguments
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateQuantile(null!, ScaleRange.OfCount(2)));
    }

    /// <summary>
    ///     Validates the threshold scale against d3's <c>scaleThreshold</c>: explicit thresholds, a value on a threshold going up, and only
    ///     as many thresholds as the range has values less one.
    /// </summary>
    [Fact]
    public void ScaleModel_Threshold_Bisects()
    {
        // Arrange: two thresholds, three intervals
        var scale = ScaleModel.CreateThreshold([10, 20], ScaleRange.OfTexts(["low", "mid", "high"]));

        // Assert
        Assert.Equal(ScaleKind.Threshold, scale.Kind);
        Assert.Equal(["low", "mid", "mid", "high", "high"], new[] { 5.0, 10, 15, 20, 25 }.Select(v => scale.MapText(DataValue.FromNumber(v))).ToArray());
        Assert.Null(scale.MapText(DataValue.FromString("x")));

        // Assert: a range with too few values uses only the first thresholds (d3's n = min(thresholds, range - 1))
        var short1 = ScaleModel.CreateThreshold([10, 20, 30], ScaleRange.OfTexts(["a", "b"]));
        Assert.Equal([10.0], short1.Thresholds);
        Assert.Equal(["a", "b", "b"], new[] { 5.0, 15, 100 }.Select(v => short1.MapText(DataValue.FromNumber(v))).ToArray());

        // Assert: more values than thresholds + 1 leaves the extra values unused; an empty range maps nothing
        Assert.Equal("b", ScaleModel.CreateThreshold([10], ScaleRange.OfTexts(["a", "b", "c", "d"])).MapText(DataValue.FromNumber(50)));
        Assert.Null(ScaleModel.CreateThreshold([10], ScaleRange.OfTexts([])).MapText(DataValue.FromNumber(50)));
        Assert.Equal(0, ScaleModel.CreateThreshold([], ScaleRange.OfCount(2)).MapIndex(DataValue.FromNumber(1)));
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateThreshold(null!, ScaleRange.OfCount(2)));
    }

    /// <summary>
    ///     Validates the bin-ordinal scale against Vega's <c>scaleBinOrdinal</c>: the value of the bin between two edges, the bin index
    ///     cycling through the range, no value below the first edge, and the value that follows the last bin at or above the last edge.
    /// </summary>
    [Fact]
    public void ScaleModel_BinOrdinal_MapsByBinIndex()
    {
        // Arrange: three bins of width 10
        var scale = ScaleModel.CreateBinOrdinal([0, 10, 20, 30], ScaleRange.OfTexts(["a", "b", "c"]));

        // Assert: a value on an edge belongs to the bin that starts there
        Assert.Equal(ScaleKind.BinOrdinal, scale.Kind);
        Assert.Equal(["a", "a", "b", "b", "c", "c"], new[] { 0.0, 5, 10, 19.5, 20, 29 }.Select(v => scale.MapText(DataValue.FromNumber(v))).ToArray());

        // Assert: below the first edge there is no value; at or above the last edge Vega's index (bisectRight - 1 = 3) cycles to the first value
        Assert.Null(scale.MapText(DataValue.FromNumber(-1)));
        Assert.Equal("a", scale.MapText(DataValue.FromNumber(30)));
        Assert.Equal("a", scale.MapText(DataValue.FromNumber(35)));
        Assert.Null(scale.MapText(DataValue.FromString("n/a")));

        // Assert: a range shorter than the bins cycles: the third bin takes the first of two values
        var cycling = ScaleModel.CreateBinOrdinal([0, 10, 20, 30], ScaleRange.OfNumbers([1, 2]));
        Assert.Equal([1.0, 2, 1], new[] { 5.0, 15, 25 }.Select(v => cycling.Map(DataValue.FromNumber(v))!.Value).ToArray());
        Assert.Equal((0.0, 30.0), (cycling.DomainStart, cycling.DomainEnd));
        Assert.Equal([0.0, 10, 20, 30], cycling.Thresholds);
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateBinOrdinal(null!, ScaleRange.OfCount(1)));
    }

    /// <summary>
    ///     Validates the range of an ordinal or discretizing scale: texts, numbers or only a count, copies of their source, the
    ///     lookup by index, and an ordinal scale with numbers or an index only.
    /// </summary>
    [Fact]
    public void ScaleModel_Range_HoldsTextsNumbersOrACount()
    {
        // Arrange: ranges copy their values
        var source = new List<string> { "x", "y" };
        var texts = ScaleRange.OfTexts(source);
        source.Add("z");

        // Assert: texts
        Assert.Equal(2, texts.Count);
        Assert.Equal(["x", "y"], texts.Texts);
        Assert.Equal("y", texts.TextAt(1));
        Assert.Null(texts.TextAt(2));
        Assert.Null(texts.TextAt(-1));
        Assert.Null(texts.NumberAt(0));
        Assert.Empty(texts.Numbers);

        // Assert: numbers and a count
        var numbers = ScaleRange.OfNumbers([4, 361]);
        Assert.Equal([4.0, 361], numbers.Numbers);
        Assert.Equal(361, numbers.NumberAt(1));
        Assert.Null(numbers.NumberAt(2));
        Assert.Null(numbers.TextAt(0));
        Assert.Equal(3, ScaleRange.OfCount(3).Count);
        Assert.Equal(0, ScaleRange.OfCount(-2).Count);
        Assert.Equal(0, default(ScaleRange).Count);
        Assert.Throws<ArgumentNullException>(() => ScaleRange.OfTexts(null!));
        Assert.Throws<ArgumentNullException>(() => ScaleRange.OfNumbers(null!));

        // Assert: an ordinal scale with numbers cycles through them; with a count it gives the index of the value
        var ordinal = ScaleModel.CreateOrdinal(Categories("a", "b", "c"), ScaleRange.OfNumbers([4, 361]));
        Assert.Equal([4.0, 361, 4], new[] { "a", "b", "c" }.Select(c => ordinal.Map(DataValue.FromString(c))!.Value).ToArray());
        Assert.Null(ordinal.Map(DataValue.FromString("zzz")));
        Assert.Null(ordinal.MapText(DataValue.FromString("a")));
        var indexed = ScaleModel.CreateOrdinal(Categories("a", "b", "c"), ScaleRange.OfCount(2));
        Assert.Equal([0, 1, 0, -1], new[] { "a", "b", "c", "zzz" }.Select(c => indexed.MapIndex(DataValue.FromString(c))).ToArray());
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateOrdinal(null!, ScaleRange.OfCount(1)));
    }

    /// <summary>
    ///     Validates the log scale against d3's <c>scaleLog</c> (the linear map of <c>ln x</c>): decades are evenly spaced, the inverse undoes
    ///     the map, values outside the domain extrapolate, clamping happens in the domain, a number that is not above zero has no position, a
    ///     domain below zero is reflected (<c>-ln(-x)</c>), and a descending domain runs backwards.
    /// </summary>
    [Fact]
    public void ScaleModel_Transform_LogMapsDecadesEvenly()
    {
        // Arrange: three decades onto 300 pixels, 100 pixels per decade
        var scale = ScaleModel.CreateLinear([1, 1000], [0, 300], transform: ScaleTransform.Log(10));

        // Assert: it is a linear scale with the log transform, and each power of ten is one decade further
        Assert.Equal(ScaleKind.Linear, scale.Kind);
        Assert.Equal(ScaleTransformKind.Log, scale.Transform.Kind);
        Assert.Equal(10, scale.Transform.Parameter);
        Assert.Equal(0, scale.Map(1), Precision);
        Assert.Equal(100, scale.Map(10), Precision);
        Assert.Equal(200, scale.Map(100), Precision);
        Assert.Equal(300, scale.Map(1000), Precision);

        // Assert: the geometric mean of the ends is the middle; below the domain the map continues (0.1 is one decade below the start)
        Assert.Equal(150, scale.Map(Math.Sqrt(1000)), Precision);
        Assert.Equal(-100, scale.Map(0.1), Precision);

        // Assert: the inverse is the exponential of the linear inverse
        Assert.Equal(10, scale.Invert(100), Precision);
        Assert.Equal(1000, scale.Invert(300), Precision);

        // Assert: a number that is not above zero has no position (the logarithm is not defined), and says so as null for a data value
        Assert.True(double.IsNaN(scale.Map(0)));
        Assert.True(double.IsNaN(scale.Map(-5)));
        Assert.Null(scale.Map(DataValue.FromNumber(0)));
        Assert.Null(scale.Map(DataValue.FromNumber(-5)));
        Assert.Equal(200.0, scale.Map(DataValue.FromNumber(100))!.Value, Precision);

        // Assert: clamping works on the domain before the logarithm, so 0 and 10000 are held at the ends
        var clamped = ScaleModel.CreateLinear([1, 1000], [0, 300], clamp: true, transform: ScaleTransform.Log(10));
        Assert.Equal(300, clamped.Map(10000), Precision);
        Assert.Equal(0, clamped.Map(0.001), Precision);
        Assert.Equal(0, clamped.Map(0), Precision);

        // Assert: a domain below zero is reflected: -10 is two thirds of the way from -1000 to -1, -100 one third
        var negative = ScaleModel.CreateLinear([-1000, -1], [0, 300], transform: ScaleTransform.Log(10));
        Assert.Equal(0, negative.Map(-1000), Precision);
        Assert.Equal(100, negative.Map(-100), Precision);
        Assert.Equal(200, negative.Map(-10), Precision);
        Assert.Equal(300, negative.Map(-1), Precision);
        Assert.Equal(-10, negative.Invert(200), Precision);
        Assert.Null(negative.Map(DataValue.FromNumber(10)));

        // Assert: a descending domain maps its first entry to the first range entry: 1000 is at 0, 1 at 300
        var descending = ScaleModel.CreateLinear([1000, 1], [0, 300], transform: ScaleTransform.Log(10));
        Assert.Equal(0, descending.Map(1000), Precision);
        Assert.Equal(100, descending.Map(100), Precision);
        Assert.Equal(300, descending.Map(1), Precision);

        // Assert: the base does not change the map of a linear scale over ln x (only the ticks), so base 2 gives the same positions
        Assert.Equal(100, ScaleModel.CreateLinear([1, 1000], [0, 300], transform: ScaleTransform.Log(2)).Map(10), Precision);
    }

    /// <summary>
    ///     Validates the pow and sqrt scales against d3's <c>scalePow</c> and <c>scaleSqrt</c>: the sign-preserving power <c>x &lt; 0 ? -(-x)^e : x^e</c>,
    ///     the exponent 1 as the identity, the square root (and its square as the inverse) for the exponent 0.5, and extrapolation.
    /// </summary>
    [Fact]
    public void ScaleModel_Transform_PowAndSqrtFollowTheSignedPower()
    {
        // Assert: exponent 2 on 0..10 -> 0..100 is x squared: 5 is a quarter of the way (T = 25 of 100)
        var square = ScaleModel.CreateLinear([0, 10], [0, 100], transform: ScaleTransform.Pow(2));
        Assert.Equal(25, square.Map(5), Precision);
        Assert.Equal(100, square.Map(10), Precision);
        Assert.Equal(5, square.Invert(25), Precision);

        // Assert: below zero the power keeps the sign, so -2 is -4 (T = -4 of 100): the map continues below the start
        Assert.Equal(-4, square.Map(-2), Precision);

        // Assert: the exponent 0.5 on 0..100 -> 0..10 is the square root: 25 maps to 5, 1 to 1; the inverse squares
        var root = ScaleModel.CreateLinear([0, 100], [0, 10], transform: ScaleTransform.Pow(0.5));
        Assert.Equal(5, root.Map(25), Precision);
        Assert.Equal(1, root.Map(1), Precision);
        Assert.Equal(10, root.Map(100), Precision);
        Assert.Equal(25, root.Invert(5), Precision);

        // Assert: the signed root of -100..100 -> 0..200: -25 is -5 of -10..10 (50), 0 the middle (100), 25 is 150; the inverse of 50 is -25
        var signedRoot = ScaleModel.CreateLinear([-100, 100], [0, 200], transform: ScaleTransform.Pow(0.5));
        Assert.Equal(50, signedRoot.Map(-25), Precision);
        Assert.Equal(100, signedRoot.Map(0), Precision);
        Assert.Equal(150, signedRoot.Map(25), Precision);
        Assert.Equal(-25, signedRoot.Invert(50), Precision);

        // Assert: the cube of -2..2 -> 0..16: T = -8..8, so 1 (T = 1) is 9 and -1 is 7; the inverse takes the cube root with the sign
        var cube = ScaleModel.CreateLinear([-2, 2], [0, 16], transform: ScaleTransform.Pow(3));
        Assert.Equal(9, cube.Map(1), Precision);
        Assert.Equal(7, cube.Map(-1), Precision);
        Assert.Equal(1, cube.Invert(9), Precision);
        Assert.Equal(-1, cube.Invert(7), Precision);

        // Assert: the exponent 1 is no transform at all, and every number (also NaN) is handled as in a linear scale
        var identity = ScaleModel.CreateLinear([0, 10], [0, 100], transform: ScaleTransform.Pow(1));
        Assert.True(identity.Transform.IsLinear);
        Assert.Equal(30, identity.Map(3), Precision);
        Assert.True(double.IsNaN(identity.Map(double.NaN)));
        Assert.Equal(25, ScaleModel.CreateLinear([0, 10], [0, 100]).Map(2.5), Precision);
        Assert.True(ScaleTransform.Linear.IsLinear);
        Assert.False(ScaleTransform.Log(10).IsLinear);
    }

    /// <summary>
    ///     Validates the symlog scale against d3's <c>scaleSymlog</c>: <c>sign(x) * log1p(|x| / c)</c> with its inverse
    ///     <c>sign(y) * expm1(|y|) * c</c>, symmetric around zero, the constant as the size of the linear region, and an accurate
    ///     <c>log1p</c> for tiny numbers (a naive <c>ln(1 + x)</c> loses them).
    /// </summary>
    [Fact]
    public void ScaleModel_Transform_SymlogIsSymmetricAndAccurateNearZero()
    {
        // Arrange: the constant 1 on -(e-1)..(e-1) -> 0..100: T(e-1) = ln(e) = 1, so the ends are at 0 and 100 and zero is the middle
        var edge = Math.E - 1;
        var scale = ScaleModel.CreateLinear([-edge, edge], [0, 100], transform: ScaleTransform.Symlog(1));

        // Assert: 1 is ln 2 of the 1 that the end has, so 50 + 50 ln 2 above the middle; -1 is symmetric below it
        Assert.Equal(50, scale.Map(0), Precision);
        Assert.Equal(50 + (50 * Math.Log(2)), scale.Map(1), Precision);
        Assert.Equal(50 - (50 * Math.Log(2)), scale.Map(-1), Precision);
        Assert.Equal(100, scale.Map(edge), Precision);

        // Assert: the inverse is sign * expm1 * c
        Assert.Equal(1, scale.Invert(50 + (50 * Math.Log(2))), Precision);
        Assert.Equal(-1, scale.Invert(50 - (50 * Math.Log(2))), Precision);
        Assert.Equal(0, scale.Invert(50), Precision);

        // Assert: the constant 10 on 0..90 -> 0..1: T(90) = ln(1 + 9) = ln 10, and 9 is ln(1.9) / ln 10 of the way
        var tenth = ScaleModel.CreateLinear([0, 90], [0, 1], transform: ScaleTransform.Symlog(10));
        Assert.Equal(Math.Log(1.9) / Math.Log(10), tenth.Map(9), Precision);
        Assert.Equal(9, tenth.Invert(Math.Log(1.9) / Math.Log(10)), Precision);

        // Assert: a tiny number keeps its digits: ln(1 + 1e-12) is 1e-12 (to 12 digits), so the map is 1e-12 / ln 2, within a part in a billion
        var unit = ScaleModel.CreateLinear([0, 1], [0, 1], transform: ScaleTransform.Symlog(1));
        var expected = 1e-12 / Math.Log(2);
        Assert.InRange(unit.Map(1e-12) / expected, 1 - 1e-9, 1 + 1e-9);
        Assert.InRange(unit.Invert(expected) / 1e-12, 1 - 1e-9, 1 + 1e-9);
        Assert.True(double.IsNaN(unit.Map(double.NaN)));
    }

    /// <summary>
    ///     Validates the color scales over a transform (d3's <c>scaleSequentialLog</c> and <c>scaleDivergingLog</c>): the position in the
    ///     interpolator is taken from the transformed numbers, the middle value of a diverging scale included, and a number the transform
    ///     cannot take has no color.
    /// </summary>
    [Fact]
    public void ScaleModel_Transform_SequentialAndDivergingUseTheTransformedPosition()
    {
        // Arrange: two decades 1..100 on the probe interpolator: each decade is half of it
        var sequential = ScaleModel.CreateSequential([1, 100], Probe, transform: ScaleTransform.Log(10));

        // Assert: 10 is the middle, the ends are the ends, 1000 extrapolates to 150; no color for zero or below
        Assert.Equal(ScaleKind.Sequential, sequential.Kind);
        Assert.Equal(0, Position(sequential.MapColor(1)), Precision);
        Assert.Equal(50, Position(sequential.MapColor(10)), Precision);
        Assert.Equal(100, Position(sequential.MapColor(100)), Precision);
        Assert.Equal(150, Position(sequential.MapColor(1000)), Precision);
        Assert.Null(sequential.MapColor(0));
        Assert.Null(sequential.MapColor(-3));

        // Assert: clamping holds the position at the ends, and then a number below the domain has the first color (d3 clamps the position)
        var clamped = ScaleModel.CreateSequential([1, 100], Probe, clamp: true, transform: ScaleTransform.Log(10));
        Assert.Equal(100, Position(clamped.MapColor(10000)), Precision);
        Assert.Equal(0, Position(clamped.MapColor(0)), Precision);

        // Assert: a square root scale 0..100: 25 is half (sqrt 25 = 5 of 10)
        Assert.Equal(50, Position(ScaleModel.CreateSequential([0, 100], Probe, transform: ScaleTransform.Pow(0.5)).MapColor(25)), Precision);

        // Assert: a diverging log scale 1..10..1000 puts the middle value at the middle color, and each side by its own decades:
        // below the middle one decade gives half of the lower side (so 1 is 0), above it 100 is half of the upper side (T = 2 of 1..3 above 1)
        var diverging = ScaleModel.CreateSequential([1, 10, 1000], Probe, transform: ScaleTransform.Log(10));
        Assert.Equal(ScaleKind.Diverging, diverging.Kind);
        Assert.Equal(0, Position(diverging.MapColor(1)), Precision);
        Assert.Equal(50, Position(diverging.MapColor(10)), Precision);
        Assert.Equal(75, Position(diverging.MapColor(100)), Precision);
        Assert.Equal(100, Position(diverging.MapColor(1000)), Precision);
    }

    /// <summary>
    ///     Validates <see cref="ScaleModel.Fraction"/>, the position of a value between the ends of a domain along the transformed domain
    ///     (Vega's <c>scaleFraction</c>, which the labels and the stops of a gradient legend use), including the domain without span.
    /// </summary>
    [Fact]
    public void ScaleModel_Fraction_FollowsTheTransformedDomain()
    {
        // Assert: linear is the plain fraction, a log scale puts a decade at a third of three decades
        Assert.Equal(0.25, ScaleModel.CreateLinear([0, 8], [0, 1]).Fraction(2), Precision);
        Assert.Equal(1.0 / 3, ScaleModel.CreateLinear([1, 1000], [0, 1], transform: ScaleTransform.Log(10)).Fraction(10), Precision);

        // Assert: the square root scale 0..100: 25 is half; a reflected log domain -1000..-1: -100 is one third
        Assert.Equal(0.5, ScaleModel.CreateLinear([0, 100], [0, 1], transform: ScaleTransform.Pow(0.5)).Fraction(25), Precision);
        Assert.Equal(1.0 / 3, ScaleModel.CreateLinear([-1000, -1], [0, 1], transform: ScaleTransform.Log(10)).Fraction(-100), Precision);

        // Assert: a domain without span has its fraction in the middle
        Assert.Equal(0.5, ScaleModel.CreateLinear([5, 5], [0, 1]).Fraction(5), Precision);
        Assert.Equal(0.5, ScaleModel.CreateLinear([5, 5], [0, 1], transform: ScaleTransform.Log(10)).Fraction(7), Precision);
    }
}

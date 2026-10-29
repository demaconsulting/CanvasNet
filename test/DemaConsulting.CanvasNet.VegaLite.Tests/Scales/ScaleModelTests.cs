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
    }
}

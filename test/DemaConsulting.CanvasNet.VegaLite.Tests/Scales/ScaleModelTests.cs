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

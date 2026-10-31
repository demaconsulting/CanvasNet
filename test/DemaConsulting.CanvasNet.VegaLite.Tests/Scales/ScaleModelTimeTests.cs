using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the time and UTC scale models: the d3 <c>scaleTime</c> arithmetic over milliseconds (map, invert, clamp, round, reversed and
///     piecewise domains), with the instants of the domains computed with the base class library and the expected positions worked out by hand.
/// </summary>
public class ScaleModelTimeTests
{
    /// <summary>One day in milliseconds.</summary>
    private const double Day = 86_400_000;

    /// <summary>The tolerance of a position or an instant: floating point noise of the interpolation (one unit in the last place of a millisecond count is 0.0002).</summary>
    private const double Tolerance = 0.01;

    /// <summary>Gets 2024-01-01T00:00Z.</summary>
    private static double Jan1 => TimeTestSupport.Ms(2024, 1, 1);

    /// <summary>Gets 2024-01-11T00:00Z.</summary>
    private static double Jan11 => TimeTestSupport.Ms(2024, 1, 11);

    /// <summary>
    ///     Creates the scale of the tests: January 1 to January 11, 2024 over 0 to 400 pixels (40 pixels a day).
    /// </summary>
    /// <param name="clamp">Whether the scale clamps.</param>
    /// <param name="round">Whether the scale rounds.</param>
    /// <param name="utc">Whether the scale is a UTC scale.</param>
    /// <returns>The scale.</returns>
    private static ScaleModel TenDays(bool clamp = false, bool round = false, bool utc = false) =>
        ScaleModel.CreateTime([Jan1, Jan11], [0, 400], clamp, round, TimeCalendar.Utc, utc);

    /// <summary>
    ///     Asserts that a position is the expected number, up to floating point noise.
    /// </summary>
    /// <param name="expected">The expected position.</param>
    /// <param name="actual">The actual position, which must exist.</param>
    private static void Near(double expected, double? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected, actual.Value, Tolerance);
    }

    /// <summary>
    ///     Validates the linear map of dates: a tenth of the span is a tenth of the range, from either side.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_MapsDatesLinearly()
    {
        // Arrange
        var scale = TenDays();

        // Act / Assert: January 6 is five of ten days, 200 of 400 pixels; the ends map to the ends
        Assert.Equal(ScaleKind.Time, scale.Kind);
        Assert.True(scale.IsLinearLike);
        Near(200, scale.Map(TimeTestSupport.Ms(2024, 1, 6)));
        Near(200, scale.Map(DataValue.FromDate(TimeTestSupport.Ms(2024, 1, 6))));
        Near(0, scale.Map(DataValue.FromDate(Jan1)));
        Near(400, scale.Map(DataValue.FromDate(Jan11)));

        // Assert: six hours is a quarter of a day, 10 pixels; and a number of milliseconds is read like a date
        Near(10, scale.Map(Jan1 + (Day / 4)));
        Near(10, scale.Map(DataValue.FromNumber(Jan1 + (Day / 4))));

        // Assert: outside the domain the line continues
        Near(-280, scale.Map(TimeTestSupport.Ms(2023, 12, 25)));
        Near(440, scale.Map(TimeTestSupport.Ms(2024, 1, 12)));
    }

    /// <summary>
    ///     Validates that a position maps back to the instant (as milliseconds, not a date) and that the round trip holds.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_Invert_RoundTrips()
    {
        // Arrange
        var scale = TenDays();

        // Act / Assert: 200 pixels is January 6; 40 pixels is one day on
        Near(TimeTestSupport.Ms(2024, 1, 6), scale.Invert(200));
        Near(Jan1 + Day, scale.Invert(40));
        foreach (var ms in new[] { Jan1, Jan1 + 12_345_678, TimeTestSupport.Ms(2024, 1, 9, 17), Jan11 })
        {
            Near(ms, scale.Invert(scale.Map(ms)));
        }

        Assert.True(double.IsNaN(scale.Invert(double.NaN)));
    }

    /// <summary>
    ///     Validates <c>clamp</c>: a date before or after the domain gets the end of the range.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_Clamp()
    {
        // Arrange
        var scale = TenDays(clamp: true);

        // Act / Assert
        Assert.True(scale.Clamp);
        Near(0, scale.Map(TimeTestSupport.Ms(2023, 12, 25)));
        Near(400, scale.Map(TimeTestSupport.Ms(2024, 2, 1)));
        Near(200, scale.Map(TimeTestSupport.Ms(2024, 1, 6)));
        Near(Jan1, scale.Invert(-50));
        Near(Jan11, scale.Invert(450));
    }

    /// <summary>
    ///     Validates a descending domain and a descending range: d3 reverses both arrays, so the map stays a straight line.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_Reverse()
    {
        // Arrange: the domain runs backwards, then the range does
        var backwardsDomain = ScaleModel.CreateTime([Jan11, Jan1], [0, 400], false, false, TimeCalendar.Utc, false);
        var backwardsRange = ScaleModel.CreateTime([Jan1, Jan11], [400, 0], false, false, TimeCalendar.Utc, false);

        // Act / Assert: January 11 is at the start of the first, and the middle is in the middle of both
        Near(0, backwardsDomain.Map(Jan11));
        Near(400, backwardsDomain.Map(Jan1));
        Near(200, backwardsDomain.Map(TimeTestSupport.Ms(2024, 1, 6)));
        Assert.Equal(Jan11, backwardsDomain.DomainStart);
        Near(400, backwardsRange.Map(Jan1));
        Near(0, backwardsRange.Map(Jan11));
        Near(TimeTestSupport.Ms(2024, 1, 6), backwardsRange.Invert(200));
    }

    /// <summary>
    ///     Validates a domain and a range of three entries: d3's piecewise map, one segment per pair of neighbors.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_Piecewise3Entries()
    {
        // Arrange: January 1 to 2 over 0 to 100 pixels, January 2 to 11 over 100 to 300
        var jan2 = TimeTestSupport.Ms(2024, 1, 2);
        var scale = ScaleModel.CreateTime([Jan1, jan2, Jan11], [0, 100, 300], false, false, TimeCalendar.Utc, false);

        // Act / Assert: half a day is half of the first segment; 4.5 days beyond January 2 is half of the second
        Near(50, scale.Map(Jan1 + (Day / 2)));
        Near(100, scale.Map(jan2));
        Near(200, scale.Map(jan2 + (4.5 * Day)));
        Near(300, scale.Map(Jan11));
        Assert.Equal(Jan1, scale.DomainStart);
        Assert.Equal(Jan11, scale.DomainEnd);
    }

    /// <summary>
    ///     Validates that a value that is not a date or a number has no position, and that the operations of the other kinds still reject a time scale.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_InvalidDateHasNoPosition()
    {
        // Arrange
        var scale = TenDays();

        // Act / Assert
        Assert.Null(scale.Map(DataValue.Null));
        Assert.Null(scale.Map(DataValue.FromString("not a date")));
        Assert.Null(scale.Map(DataValue.FromNumber(double.NaN)));
        Assert.True(double.IsNaN(scale.Map(double.NaN)));
        Assert.Throws<InvalidOperationException>(() => scale.MapOrdinal(DataValue.FromDate(Jan1)));
        Assert.Throws<InvalidOperationException>(() => scale.MapColor(0));
    }

    /// <summary>
    ///     Validates that the time and the UTC scale do the same arithmetic (only the kind and the calendar of the ticks differ), and <c>round</c>.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_Utc_And_Time_MapIdentically()
    {
        // Arrange
        var time = TenDays();
        var utc = TenDays(utc: true);
        var zone = ScaleModel.CreateTime([Jan1, Jan11], [0, 400], false, false, TimeCalendar.Create(TimeTestSupport.Fixed(5, 30)), false);

        // Act / Assert: the same positions for any instant
        foreach (var ms in new[] { Jan1, TimeTestSupport.Ms(2024, 1, 3, 7, 30), Jan11 + Day })
        {
            Assert.Equal(time.Map(ms), utc.Map(ms));
            Assert.Equal(time.Map(ms), zone.Map(ms));
        }

        Assert.Equal(ScaleKind.Utc, utc.Kind);
        Assert.True(utc.IsLinearLike);

        // Assert: rounding applies to both: January 2 of January 1 to 4 over 0 to 10 is 3.33...
        var rounded = ScaleModel.CreateTime([Jan1, TimeTestSupport.Ms(2024, 1, 4)], [0, 10], false, true, TimeCalendar.Utc, true);
        var exact = ScaleModel.CreateTime([Jan1, TimeTestSupport.Ms(2024, 1, 4)], [0, 10], false, false, TimeCalendar.Utc, true);
        Assert.True(rounded.Round);
        Near(3, rounded.Map(TimeTestSupport.Ms(2024, 1, 2)));
        Near(10d / 3, exact.Map(TimeTestSupport.Ms(2024, 1, 2)));
    }

    /// <summary>
    ///     Validates that the model keeps the calendar of its ticks and that the other kinds have none; and the argument checks.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_Calendar_IsStored()
    {
        // Arrange
        var zone = TimeCalendar.Create(TimeTestSupport.UsLike());
        var scale = ScaleModel.CreateTime([Jan1, Jan11], [0, 400], false, false, zone, false);

        // Act / Assert
        Assert.Same(zone, scale.Calendar);
        Assert.Same(TimeCalendar.Utc, TenDays(utc: true).Calendar);
        Assert.Null(ScaleModel.CreateLinear(0, 1, 0, 10).Calendar);
        Assert.True(ScaleModel.CreateLinear(0, 1, 0, 10).IsLinearLike);
        Assert.False(ScaleModel.CreateBand([DataValue.FromString("a")], 0, 10).IsLinearLike);
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateTime([Jan1, Jan11], [0, 400], false, false, null!, false));
        Assert.Throws<ArgumentNullException>(() => ScaleModel.CreateTime(null!, [0, 400], false, false, zone, false));
        Assert.Throws<ArgumentException>(() => ScaleModel.CreateTime([Jan1], [0, 400], false, false, zone, false));
        Assert.Throws<ArgumentException>(() => ScaleModel.CreateTime([Jan1, Jan11], [0], false, false, zone, false));
    }

    /// <summary>
    ///     Validates that <c>Fraction</c> (the position of a value in the domain) works for a time scale, from 0 at the start to 1 at the end.
    /// </summary>
    [Fact]
    public void ScaleModel_Time_Fraction_IsThePositionInTheDomain()
    {
        // Arrange
        var scale = TenDays();

        // Act / Assert: January 3 is two tenths of the way
        Assert.Equal(0.2, scale.Fraction(TimeTestSupport.Ms(2024, 1, 3)), Tolerance);
        Assert.Equal(0, scale.Fraction(Jan1));
        Assert.Equal(1, scale.Fraction(Jan11));
    }
}

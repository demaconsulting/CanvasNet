using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the time units of <see cref="EncodingPlan"/>: the derived field names (<c>yearmonth_date</c> and its end), the band end of a mark that spans
///     its unit, the default titles in the three title modes, the dimensions of an implicit aggregate and the units that a bin or an aggregate replaces. The expectations
///     are derived by hand from Vega-Lite's naming, title and band rules.
/// </summary>
public class EncodingPlanTimeTests
{
    /// <summary>
    ///     Creates the plan of a view.
    /// </summary>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="mark">The JSON text of the mark.</param>
    /// <param name="config">The caller's configuration text, or null.</param>
    /// <returns>The plan.</returns>
    private static EncodingPlan Create(string encoding, string mark = "\"bar\"", string? config = null)
    {
        var tree = SpecParser.Parse("{\"data\":{\"values\":[{\"a\":1}]},\"mark\":" + mark + ",\"encoding\":" + encoding + "}");
        return EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, config));
    }

    /// <summary>
    ///     Validates the field of a time unit and its end: <c>yearmonth_date</c> and <c>yearmonth_date_end</c>, the channel reads the start and spans to the end,
    ///     the plan records the unit, and the encoding is no longer an identity.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_NamesATimeUnitFieldAndItsEnd()
    {
        // Act
        var plan = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"yearmonth"},"y":{"aggregate":"count","type":"quantitative"}}""");

        // Assert
        var x = plan.Channel("x")!;
        Assert.Equal("yearmonth_date", x.ValueField);
        Assert.Equal("yearmonth_date", x.FieldPath);
        Assert.Equal("yearmonth_date", x.RawField);
        Assert.Equal("yearmonth_date_end", x.Field2Path);
        Assert.Equal("date", x.SourceField);
        Assert.True(x.HasBandEnd);
        var unit = Assert.Single(plan.TimeUnits);
        Assert.Equal(("yearmonth_date", "yearmonth_date_end", "date"), (unit.StartName, unit.EndName, unit.SourceField));
        Assert.True(unit.HasBandEnd);
        Assert.Same(unit, x.TimeUnit);
        Assert.False(plan.IsIdentity);
    }

    /// <summary>
    ///     Validates the names of the object forms: the unit, then the other members as <c>_name_value</c>, the <c>utc</c> prefix, and <c>timeunit</c> for the
    ///     <c>maxbins</c> form that names no unit.
    /// </summary>
    /// <param name="unit">The <c>timeUnit</c> value.</param>
    /// <param name="name">The expected literal name of the start field.</param>
    [Theory]
    [InlineData("\"yearmonth\"", "yearmonth_date")]
    [InlineData("\"utcmonth\"", "utcmonth_date")]
    [InlineData("""{"unit":"month","step":2}""", "month_step_2_date")]
    [InlineData("""{"unit":"month","utc":true}""", "utcmonth_date")]
    [InlineData("""{"maxbins":10}""", "timeunit_maxbins_10_date")]
    [InlineData("""{"unit":"yearmonthdate","step":7}""", "yearmonthdate_step_7_date")]
    public void EncodingPlan_Create_ObjectFormNames(string unit, string name)
    {
        // Act
        var plan = Create("{\"x\":{\"field\":\"date\",\"type\":\"temporal\",\"timeUnit\":" + unit + "}}");

        // Assert
        var x = plan.Channel("x")!;
        Assert.Equal(name, x.ValueField);
        Assert.Equal(name + "_end", x.Field2Path);
        Assert.Equal(name, Assert.Single(plan.TimeUnits).StartName);
    }

    /// <summary>
    ///     Validates the band end of the marks: a bar, a rect and a tick have a band position by default and span their unit, a point has none and sits at the start.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="hasBandEnd">Whether the mark spans the unit.</param>
    [Theory]
    [InlineData("bar", true)]
    [InlineData("rect", true)]
    [InlineData("tick", true)]
    [InlineData("point", false)]
    [InlineData("circle", false)]
    [InlineData("rule", false)]
    public void EncodingPlan_Create_MarksWithABandPositionSpanTheirUnit(string mark, bool hasBandEnd)
    {
        // Act
        var plan = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"yearmonth"}}""", "\"" + mark + "\"");

        // Assert
        var x = plan.Channel("x")!;
        Assert.Equal(hasBandEnd, x.HasBandEnd);
        Assert.Equal(hasBandEnd ? "yearmonth_date_end" : null, x.Field2Path);
        Assert.Equal(hasBandEnd, Assert.Single(plan.TimeUnits).HasBandEnd);
    }

    /// <summary>
    ///     Validates a bar with a time unit (the end is the second end) and a point with a time unit (none), and that the definition's own <c>bandPosition</c> or the
    ///     mark's <c>timeUnitBandPosition</c> gives a point the band end.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_BarWithTimeUnit_HasBandEnd_PointWithTimeUnit_HasNoBandEnd()
    {
        // Act
        var bar = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month"}}""", "\"bar\"");
        var point = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month"}}""", "\"point\"");
        var positioned = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month","bandPosition":0.5}}""", "\"point\"");
        var marked = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month"}}""", """{"type":"point","timeUnitBandPosition":0.25}""");
        var configured = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month"}}""", "\"point\"", """{"point":{"timeUnitBandPosition":0.5}}""");

        // Assert
        Assert.Equal("month_date_end", bar.Channel("x")!.Field2Path);
        Assert.Null(point.Channel("x")!.Field2Path);
        Assert.False(point.Channel("x")!.HasBandEnd);
        Assert.True(positioned.Channel("x")!.HasBandEnd);
        Assert.True(marked.Channel("x")!.HasBandEnd);
        Assert.True(configured.Channel("x")!.HasBandEnd);
    }

    /// <summary>
    ///     Validates that a discrete (ordinal or nominal) time unit field has no band end: the unit is the category, and a quantitative field is not a time unit field.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_OrdinalTimeUnit_HasNoBandEnd()
    {
        // Act
        var ordinal = Create("""{"x":{"field":"date","type":"ordinal","timeUnit":"month"}}""");
        var nominal = Create("""{"x":{"field":"date","type":"nominal","timeUnit":"month"}}""");

        // Assert
        Assert.Null(ordinal.Channel("x")!.Field2Path);
        Assert.False(ordinal.Channel("x")!.HasBandEnd);
        Assert.Equal("month_date", ordinal.Channel("x")!.ValueField);
        Assert.False(nominal.Channel("x")!.HasBandEnd);
    }

    /// <summary>
    ///     Validates that a field without a type that has a time unit is temporal (Vega-Lite's default), that a color channel reads the start only, and that a position
    ///     channel with a secondary channel keeps the secondary channel as its second end.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_SecondaryChannel_SuppressesBandEnd()
    {
        // Act
        var untyped = Create("""{"x":{"field":"date","timeUnit":"month"}}""");
        var color = Create("""{"x":{"field":"a","type":"nominal"},"color":{"field":"date","type":"temporal","timeUnit":"year"}}""");
        var ranged = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month"},"x2":{"field":"until","type":"temporal","timeUnit":"month"}}""");

        // Assert
        Assert.True(untyped.Channel("x")!.HasBandEnd);
        Assert.Equal("year_date", color.Channel("color")!.ValueField);
        Assert.Null(color.Channel("color")!.Field2Path);
        Assert.False(color.Channel("color")!.HasBandEnd);
        Assert.False(ranged.Channel("x")!.HasBandEnd);
        Assert.Equal("month_until", ranged.Channel("x")!.Field2Path);
        Assert.Equal(["month_date", "month_until"], ranged.TimeUnits.Select(u => u.StartName).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    ///     Validates the default titles in the three modes: verbal <c>date (year-month)</c> (the parts of the unit joined with a dash), functional <c>YEARMONTH(date)</c>
    ///     and plain <c>date</c>; the <c>maxbins</c> form is the field (verbal) or <c>TIMEUNIT(date)</c> (functional); a UTC unit keeps its prefix in the functional title.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_TitlesVerbalFunctionalPlain()
    {
        // Arrange
        static string Title(string unit, string? mode)
        {
            var config = mode is null ? null : "{\"fieldTitle\":\"" + mode + "\"}";
            return Create("{\"x\":{\"field\":\"date\",\"type\":\"temporal\",\"timeUnit\":" + unit + "}}", "\"bar\"", config).Channel("x")!.Title!;
        }

        // Act / Assert
        Assert.Equal("date (year-month)", Title("\"yearmonth\"", null));
        Assert.Equal("date (year-month)", Title("\"yearmonth\"", "verbal"));
        Assert.Equal("YEARMONTH(date)", Title("\"yearmonth\"", "functional"));
        Assert.Equal("date", Title("\"yearmonth\"", "plain"));
        Assert.Equal("date (year-week-day)", Title("\"yearweekday\"", null));
        Assert.Equal("date (month)", Title("{\"unit\":\"month\",\"step\":2}", null));
        Assert.Equal("MONTH(date)", Title("{\"unit\":\"month\",\"step\":2}", "functional"));
        Assert.Equal("date", Title("{\"maxbins\":10}", null));
        Assert.Equal("TIMEUNIT(date)", Title("{\"maxbins\":10}", "functional"));
        Assert.Equal("date (year-month)", Title("\"utcyearmonth\"", null));
        Assert.Equal("UTCYEARMONTH(date)", Title("\"utcyearmonth\"", "functional"));
    }

    /// <summary>
    ///     Validates that an aggregate groups by the start of a unit, and by its end too when the channel spans the unit (the end follows from the start, so it adds no
    ///     group, and the marks need it after the aggregate).
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_AggregateGroupsByTimeUnitStart()
    {
        // Act
        var bar = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"yearmonth"},"y":{"aggregate":"count","type":"quantitative"}}""");
        var point = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"yearmonth"},"y":{"aggregate":"count","type":"quantitative"}}""", "\"point\"");
        var ordinal = Create("""{"x":{"field":"date","type":"ordinal","timeUnit":"month"},"y":{"field":"v","aggregate":"sum","type":"quantitative"}}""");

        // Assert
        Assert.Equal(["yearmonth_date", "yearmonth_date_end"], bar.Aggregate!.GroupBy);
        Assert.Equal(["yearmonth_date"], point.Aggregate!.GroupBy);
        Assert.Equal(["month_date"], ordinal.Aggregate!.GroupBy);
        Assert.Equal(["__count"], bar.Aggregate.Measures.Select(m => m.Name));
    }

    /// <summary>
    ///     Validates that a bin wins over a time unit on the same definition, that an aggregate of the field replaces the unit (the unit applies to a field that is read as it
    ///     is), and that a binned unit is ignored (the field is used as it is).
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_BinWinsOverTimeUnit()
    {
        // Act
        var binned = Create("""{"x":{"field":"a","type":"quantitative","bin":true,"timeUnit":"month"}}""");
        var aggregated = Create("""{"y":{"field":"a","type":"quantitative","aggregate":"max","timeUnit":"month"}}""");
        var ignored = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"binnedyearmonth"}}""");

        // Assert
        Assert.Empty(binned.TimeUnits);
        Assert.Equal(BinKind.Binning, binned.Channel("x")!.Bin);
        Assert.Equal("bin_maxbins_10_a", binned.Channel("x")!.ValueField);
        Assert.Empty(aggregated.TimeUnits);
        Assert.Equal("max_a", aggregated.Channel("y")!.ValueField);
        Assert.Empty(ignored.TimeUnits);
        Assert.Equal("date", ignored.Channel("x")!.ValueField);
        Assert.True(ignored.IsIdentity);
    }

    /// <summary>
    ///     Validates that channels that share a field and a unit share one unit, and that a different unit of the same field is another one.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_SharedAndDistinctUnits()
    {
        // Act
        var shared = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month"},"color":{"field":"date","type":"ordinal","timeUnit":"month"}}""");
        var distinct = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month"},"color":{"field":"date","type":"ordinal","timeUnit":"year"}}""");

        // Assert: the band end of the position channel is the one of the shared unit
        var unit = Assert.Single(shared.TimeUnits);
        Assert.True(unit.HasBandEnd);
        Assert.Same(unit, shared.Channel("x")!.TimeUnit);
        Assert.Same(unit, shared.Channel("color")!.TimeUnit);
        Assert.False(shared.Channel("color")!.HasBandEnd);
        Assert.Equal(["month_date", "year_date"], distinct.TimeUnits.Select(u => u.StartName).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    ///     Validates the stack of a bar chart whose layers are a time unit: the layers are ordered by the unit start (descending on y), and a time unit on the other position
    ///     channel is a group of the stack with its end.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_StackUsesTheTimeUnitFields()
    {
        // Act
        var plan = Create("""{"x":{"field":"c","type":"nominal"},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"date","type":"ordinal","timeUnit":"month"}}""");
        var grouped = Create("""{"x":{"field":"date","type":"temporal","timeUnit":"month"},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"c","type":"nominal"}}""");

        // Assert
        Assert.Equal([("month_date", true)], plan.Stack!.Sort.Select(s => (s.Field, s.Descending)));
        Assert.Equal(["month_date", "month_date_end"], grouped.Stack!.GroupBy);
    }

    /// <summary>
    ///     Validates that the units of a condition's field definition and of a view without an encoding are planned or empty as expected.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_WithoutAnEncodingThereAreNoUnits()
    {
        // Act
        var tree = SpecParser.Parse("{\"data\":{\"values\":[]},\"mark\":\"bar\"}");
        var plan = EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, null));

        // Assert
        Assert.Empty(plan.TimeUnits);
        Assert.True(plan.IsIdentity);
    }
}

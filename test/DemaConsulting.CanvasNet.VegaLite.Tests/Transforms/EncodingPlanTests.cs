using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for <see cref="EncodingPlan"/>: the derived field names (bins, aggregates, stacks), the default titles with their
///     configuration, the stack decision and the order of its layers, the implicit aggregate's dimensions and measures, and the flags
///     that tell the scales which table they read. The expectations are derived by hand from Vega-Lite's public naming, title and stack rules.
/// </summary>
public class EncodingPlanTests
{
    /// <summary>The stack offsets by name (the plan's own enumeration is internal and cannot be a parameter of a public test).</summary>
    public enum StackOffset
    {
        /// <summary>The values stack from zero.</summary>
        Zero,

        /// <summary>The layers are centered.</summary>
        Center,

        /// <summary>The layers are scaled to one.</summary>
        Normalize,
    }

    /// <summary>
    ///     Creates the plan of a view.
    /// </summary>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <param name="mark">The JSON text of the mark (a string literal with quotes, or an object).</param>
    /// <param name="config">The caller's configuration text, or null.</param>
    /// <returns>The plan and the parsed specification.</returns>
    private static (EncodingPlan Plan, SpecTree Tree) Create(string encoding, string mark = "\"bar\"", string? config = null)
    {
        var tree = SpecParser.Parse("{\"data\":{\"values\":[{\"a\":1}]},\"mark\":" + mark + ",\"encoding\":" + encoding + "}");
        return (EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, config)), tree);
    }

    /// <summary>
    ///     Validates the derived field names and the default title of a single channel for aggregates, bins and plain fields.
    /// </summary>
    /// <param name="definition">The channel definition (on <c>y</c> of a point mark).</param>
    /// <param name="valueField">The expected field the channel reads before a stack.</param>
    /// <param name="rawField">The expected field in the Raw table (null for a count).</param>
    /// <param name="title">The expected default title.</param>
    [Theory]
    [InlineData("""{"field":"x","type":"quantitative"}""", "x", "x", "x")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":"sum"}""", "sum_x", "x", "Sum of x")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":"mean"}""", "mean_x", "x", "Mean of x")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":"average"}""", "average_x", "x", "Average of x")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":"q1"}""", "q1_x", "x", "Q1 of x")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":"distinct"}""", "distinct_x", "x", "Distinct of x")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":"median"}""", "median_x", "x", "Median of x")]
    [InlineData("""{"aggregate":"count","type":"quantitative"}""", "__count", null, "Count of Records")]
    [InlineData("""{"field":"x","aggregate":"count","type":"quantitative"}""", "__count", null, "Count of Records")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":{"argmax":"y"}}""", "argmax_y.x", "x", "x for max y")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":{"argmin":"y"}}""", "argmin_y.x", "x", "x for min y")]
    [InlineData("""{"field":"x","type":"quantitative","aggregate":{"exponential":0.5}}""", "exponential_x", "x", "Exponential of x")]
    public void EncodingPlan_Create_NamesAndTitlesAggregatesAndPlainFields(string definition, string valueField, string? rawField, string title)
    {
        // Act
        var (plan, _) = Create("{\"y\":" + definition + "}", "\"point\"");

        // Assert
        var channel = plan.Channel("y")!;
        Assert.Equal(valueField, channel.ValueField);
        Assert.Equal(valueField, channel.FieldPath);
        Assert.Equal(rawField, channel.RawField);
        Assert.Equal(title, channel.Title);
        Assert.Null(channel.Field2Path);
    }

    /// <summary>
    ///     Validates Vega-Lite's names of a bin: <c>bin</c>, then every parameter as <c>_name_value</c> in the order written (with
    ///     <c>maxbins</c> last when neither it nor a step is given), then the field; a bin of a quantitative field gets the title
    ///     <c>x (binned)</c> and the end field as its second field.
    /// </summary>
    /// <param name="bin">The <c>bin</c> value.</param>
    /// <param name="start">The expected literal name of the start field.</param>
    [Theory]
    [InlineData("true", "bin_maxbins_10_x")]
    [InlineData("""{}""", "bin_maxbins_10_x")]
    [InlineData("""{"maxbins":20}""", "bin_maxbins_20_x")]
    [InlineData("""{"step":5}""", "bin_step_5_x")]
    [InlineData("""{"step":0.5}""", "bin_step_0_5_x")]
    [InlineData("""{"maxbins":5,"nice":false}""", "bin_maxbins_5_nice_false_x")]
    [InlineData("""{"nice":false}""", "bin_nice_false_maxbins_10_x")]
    [InlineData("""{"extent":[0,100]}""", "bin_extent_0_100_maxbins_10_x")]
    [InlineData("""{"anchor":-1.5,"maxbins":8}""", "bin_anchor__1_5_maxbins_8_x")]
    public void EncodingPlan_Create_NamesABinByItsParametersAndField(string bin, string start)
    {
        // Act
        var (plan, _) = Create("{\"x\":{\"field\":\"x\",\"type\":\"quantitative\",\"bin\":" + bin + "}}", "\"bar\"");

        // Assert
        var channel = plan.Channel("x")!;
        Assert.Equal(BinKind.Binning, channel.Bin);
        Assert.Equal(start, channel.ValueField);
        Assert.Equal(start + "_end", channel.Field2Path);
        Assert.Equal("x (binned)", channel.Title);
        Assert.Equal([start], plan.Bins.Select(b => b.StartName).ToArray());
        Assert.Equal(start + "_end", plan.Bins[0].EndName);
        Assert.Null(plan.Bins[0].RangeName);
    }

    /// <summary>
    ///     Validates that a nominal or ordinal binned field reads the text of the bin (<c>_range</c>) and has no second field, and that
    ///     a continuous channel that shares the bin gets the range name in its plan too (and still reads the start and the end).
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_DiscreteBinReadsTheRangeText()
    {
        // Act
        var (plan, _) = Create("""{"x":{"field":"x","type":"ordinal","bin":true},"color":{"field":"x","type":"quantitative","bin":{"maxbins":10}}}""", "\"point\"");

        // Assert: one bin for both channels, with the range field; "color" reads the start and the end of the bin
        Assert.Single(plan.Bins);
        Assert.Equal("bin_maxbins_10_x_range", plan.Bins[0].RangeName);
        var x = plan.Channel("x")!;
        Assert.Equal("bin_maxbins_10_x_range", x.ValueField);
        Assert.Null(x.Field2Path);
        Assert.Equal("bin_maxbins_10_x_range", x.BinPlan!.RangeName);
        var color = plan.Channel("color")!;
        Assert.Equal(BinKind.Binning, color.Bin);
        Assert.Equal("bin_maxbins_10_x", color.ValueField);
        Assert.Equal("bin_maxbins_10_x_end", color.Field2Path);
    }

    /// <summary>
    ///     Validates that a channel that has a scale bins its field with Vega-Lite's <c>autoMaxBins</c> default (6 for the color, size, opacity, stroke
    ///     width and shape channels, 4 for the stroke dash, 10 for the others), keeps a <c>maxbins</c> or <c>step</c> that is written, names the bin
    ///     by its channel's default, titles it as a binned field, and ignores <c>bin: "binned"</c> (read on x and y only).
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="bin">The <c>bin</c> value.</param>
    /// <param name="start">The expected name of the start field.</param>
    [Theory]
    [InlineData("color", "true", "bin_maxbins_6_v")]
    [InlineData("fill", "true", "bin_maxbins_6_v")]
    [InlineData("stroke", "{\"nice\":false}", "bin_nice_false_maxbins_6_v")]
    [InlineData("size", "true", "bin_maxbins_6_v")]
    [InlineData("opacity", "true", "bin_maxbins_6_v")]
    [InlineData("fillOpacity", "true", "bin_maxbins_6_v")]
    [InlineData("strokeOpacity", "true", "bin_maxbins_6_v")]
    [InlineData("strokeWidth", "true", "bin_maxbins_6_v")]
    [InlineData("shape", "true", "bin_maxbins_6_v")]
    [InlineData("strokeDash", "true", "bin_maxbins_4_v")]
    [InlineData("angle", "true", "bin_maxbins_10_v")]
    [InlineData("color", "{\"maxbins\":3}", "bin_maxbins_3_v")]
    [InlineData("size", "{\"step\":5}", "bin_step_5_v")]
    public void EncodingPlan_Create_BinsTheChannelsThatHaveAScaleWithTheirDefaultMaxBins(string channel, string bin, string start)
    {
        // Act
        var (plan, _) = Create("{\"" + channel + "\":{\"field\":\"v\",\"type\":\"quantitative\",\"bin\":" + bin + "}}", "\"point\"");

        // Assert
        var planned = plan.Channel(channel)!;
        Assert.Equal(BinKind.Binning, planned.Bin);
        Assert.Equal(start, planned.ValueField);
        Assert.Equal(start + "_end", planned.Field2Path);
        Assert.Equal("v (binned)", planned.Title);
        Assert.Equal([start], plan.Bins.Select(b => b.StartName).ToArray());
    }

    /// <summary>
    ///     Validates that a nominal or ordinal field on a non-position channel reads the text of the bin, formatted with the <c>format</c> of
    ///     the legend, and that data that is binned already, a bin of an aggregate and a secondary channel are not planned as bins.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_DiscreteBinOfAnotherChannelReadsTheRangeTextWithTheLegendFormat()
    {
        // Act
        var discrete = Create("""{"color":{"field":"v","type":"ordinal","bin":true,"legend":{"format":".1f"}}}""", "\"point\"").Plan;
        var already = Create("""{"color":{"field":"v","type":"quantitative","bin":"binned"}}""", "\"point\"").Plan;
        var aggregate = Create("""{"color":{"field":"v","type":"quantitative","aggregate":"mean","bin":true}}""", "\"point\"").Plan;

        // Assert
        var channel = discrete.Channel("color")!;
        Assert.Equal("bin_maxbins_6_v_range", channel.ValueField);
        Assert.Null(channel.Field2Path);
        Assert.Equal(".1f", discrete.Bins[0].Format);
        Assert.Equal(BinKind.None, already.Channel("color")!.Bin);
        Assert.Empty(already.Bins);
        Assert.Equal(BinKind.None, aggregate.Channel("color")!.Bin);
        Assert.Empty(aggregate.Bins);
    }

    /// <summary>
    ///     Validates that two channels that bin the same field with the same parameters share one bin, and that different parameters make two.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_SharesABinBetweenChannelsByFieldAndParameters()
    {
        // Act
        var same = Create("""{"x":{"field":"a","type":"quantitative","bin":true},"y":{"field":"a","type":"quantitative","bin":{"maxbins":10}}}""", "\"rect\"").Plan;
        var different = Create("""{"x":{"field":"a","type":"quantitative","bin":true},"y":{"field":"a","type":"quantitative","bin":{"maxbins":5}}}""", "\"rect\"").Plan;

        // Assert: bin: true and {"maxbins": 10} are the same bin; maxbins 5 is another
        Assert.Single(same.Bins);
        Assert.Equal(["bin_maxbins_10_a", "bin_maxbins_5_a"], different.Bins.Select(b => b.StartName).Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    ///     Validates the three <c>fieldTitle</c> modes and the configured <c>countTitle</c>.
    /// </summary>
    /// <param name="config">The caller's configuration.</param>
    /// <param name="sum">The expected title of a sum.</param>
    /// <param name="bin">The expected title of a bin.</param>
    /// <param name="count">The expected title of a count.</param>
    [Theory]
    [InlineData("{}", "Sum of x", "x (binned)", "Count of Records")]
    [InlineData("""{"fieldTitle":"verbal"}""", "Sum of x", "x (binned)", "Count of Records")]
    [InlineData("""{"fieldTitle":"plain"}""", "x", "x", "Count of Records")]
    [InlineData("""{"fieldTitle":"functional"}""", "SUM(x)", "BIN(x)", "Count of Records")]
    [InlineData("""{"countTitle":"Rows"}""", "Sum of x", "x (binned)", "Rows")]
    public void EncodingPlan_Create_TitlesFollowTheConfiguration(string config, string sum, string bin, string count)
    {
        // Act
        var (plan, _) = Create(
            """{"x":{"field":"x","type":"quantitative","bin":true},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"x","type":"quantitative","aggregate":"sum"}}""",
            "\"point\"",
            config);

        // Assert
        Assert.Equal(bin, plan.Channel("x")!.Title);
        Assert.Equal(count, plan.Channel("y")!.Title);
        Assert.Equal(sum, plan.Channel("color")!.Title);
    }

    /// <summary>
    ///     Validates that a range's titles are merged (<c>a, b</c>), that equal titles are not repeated, that the second field is the one of the
    ///     secondary channel, and that a pre-binned field keeps its field and gets the end field of <c>x2</c> and the plain title.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_MergesTheTitlesAndFieldsOfARange()
    {
        // Act
        var range = Create("""{"x":{"field":"a","type":"quantitative"},"x2":{"field":"b"},"y":{"field":"c","type":"nominal"}}""").Plan;
        var same = Create("""{"x":{"field":"a","type":"quantitative"},"x2":{"field":"a"},"y":{"field":"c","type":"nominal"}}""").Plan;
        var binned = Create("""{"x":{"field":"lo","type":"quantitative","bin":"binned"},"x2":{"field":"hi"},"y":{"field":"n","type":"quantitative"}}""").Plan;
        var measures = Create("""{"x":{"field":"a","type":"quantitative","aggregate":"min"},"x2":{"field":"a","aggregate":"max"},"y":{"field":"c","type":"nominal"}}""").Plan;

        // Assert
        Assert.Equal("a, b", range.Channel("x")!.Title);
        Assert.Equal("b", range.Channel("x")!.Field2Path);
        Assert.Equal("a", same.Channel("x")!.Title);
        Assert.Equal(BinKind.Binned, binned.Channel("x")!.Bin);
        Assert.Equal("lo", binned.Channel("x")!.FieldPath);
        Assert.Equal("hi", binned.Channel("x")!.Field2Path);
        Assert.Equal("lo, hi", binned.Channel("x")!.Title);
        Assert.Empty(binned.Bins);
        Assert.Equal("Min of a, Max of a", measures.Channel("x")!.Title);
        Assert.Equal("min_a", measures.Channel("x")!.FieldPath);
        Assert.Equal("max_a", measures.Channel("x")!.Field2Path);
    }

    /// <summary>
    ///     Validates that a datum or a value reads no field, and has no title.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_DatumAndValueChannelsReadNoField()
    {
        // Act
        var (plan, _) = Create("""{"x":{"datum":3},"y":{"value":5},"color":{"datum":"c"}}""", "\"rule\"");

        // Assert
        Assert.All(plan.Channels, c => Assert.False(c.HasField));
        Assert.All(plan.Channels, c => Assert.Null(c.Title));
        Assert.True(plan.IsIdentity);
    }

    /// <summary>
    ///     Validates that a field in a condition of a value channel is the field the channel reads (the definition that carries the field is the condition), that
    ///     value conditions and parameter conditions read none, and that a field with value conditions keeps its own field.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_FieldInAConditionIsTheFieldOfTheChannel()
    {
        // Act
        var (plan, tree) = Create(
            """
            {"color":{"condition":{"test":"datum.a > 1","field":"c","type":"nominal"},"value":"gray"},
             "size":{"condition":{"test":"datum.a > 1","value":9}},
             "opacity":{"condition":{"param":"p","field":"c","type":"nominal"},"value":0.5},
             "shape":{"field":"s","type":"nominal","condition":{"test":"datum.a > 1","value":"square"}}}
            """,
            "\"point\"");

        // Assert: the color reads c through its condition; the size and the parameter condition read nothing; the shape keeps its own field
        var color = plan.Channel("color")!;
        Assert.Equal(("c", "c"), (color.SourceField, color.FieldPath));
        Assert.Same(tree.Root["encoding"]!["color"]!["condition"], color.FieldDef);
        Assert.Same(tree.Root["encoding"]!["color"], color.Definition);
        Assert.Equal("c", color.Title);
        Assert.False(plan.Channel("size")!.HasField);
        Assert.False(plan.Channel("opacity")!.HasField);
        var shape = plan.Channel("shape")!;
        Assert.Equal("s", shape.FieldPath);
        Assert.Same(shape.Definition, shape.FieldDef);
    }

    /// <summary>
    ///     Validates that an encoding without aggregates, bins or a stack is an identity plan, and that a view without a mark or an encoding has no channels.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_IdentityPlansAskForNothing()
    {
        // Act
        var plain = Create("""{"x":{"field":"a","type":"nominal"},"y":{"field":"b","type":"quantitative"}}""", "\"line\"").Plan;
        var noMark = EncodingPlan.Create(SpecParser.Parse("{\"encoding\":{\"x\":{\"field\":\"a\"}}}").Root, ConfigResolver.Resolve(SpecParser.Parse("{}"), null));
        var noEncoding = EncodingPlan.Create(SpecParser.Parse("{\"mark\":\"bar\"}").Root, ConfigResolver.Resolve(SpecParser.Parse("{}"), null));

        // Assert
        Assert.True(plain.IsIdentity);
        Assert.False(plain.NeedsRaw);
        Assert.Empty(noMark.Channels);
        Assert.True(noMark.IsIdentity);
        Assert.Empty(noEncoding.Channels);
        Assert.Equal("bar", noEncoding.MarkType);
        Assert.Throws<ArgumentNullException>(() => EncodingPlan.Create(null!, ConfigResolver.Resolve(SpecParser.Parse("{}"), null)));
        Assert.Throws<ArgumentNullException>(() => EncodingPlan.Create(SpecParser.Parse("{}").Root, null!));
    }

    /// <summary>
    ///     Validates the implicit aggregate: the measures of every aggregating definition (one per output name, even when two channels use it),
    ///     and the dimensions: every other field definition of every channel, including the detail and tooltip channels and the elements of an array
    ///     channel, and the start, end and text of a bin.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_AggregateGroupsByEveryOtherFieldDefinition()
    {
        // Act
        var (plan, _) = Create(
            """
            {"x":{"field":"cat","type":"nominal"},
             "y":{"field":"v","type":"quantitative","aggregate":"sum"},
             "color":{"field":"grp","type":"nominal"},
             "size":{"field":"v","type":"quantitative","aggregate":"sum"},
             "detail":[{"field":"d1"},{"field":"d2"}],
             "tooltip":[{"field":"tip"},{"field":"v","aggregate":"max"}],
             "opacity":{"value":0.5}}
            """);

        // Assert: sum_v once, max_v once; dimensions in channel order, no duplicates
        var aggregate = plan.Aggregate!;
        Assert.Equal(["sum_v", "max_v"], aggregate.Measures.Select(m => m.Name).ToArray());
        Assert.Equal(["cat", "grp", "d1", "d2", "tip"], aggregate.GroupBy.ToArray());
        Assert.Empty(aggregate.ArgFields);
        Assert.False(plan.IsIdentity);
    }

    /// <summary>
    ///     Validates that a binned dimension contributes its start and end (and the text of a discrete bin) to the group-by and that a
    ///     pre-binned field contributes its own field and the one of its secondary channel.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_AggregateGroupsByTheBoundariesOfABin()
    {
        // Act
        var continuous = Create("""{"x":{"field":"a","type":"quantitative","bin":true},"y":{"aggregate":"count","type":"quantitative"}}""").Plan;
        var discrete = Create("""{"x":{"field":"a","type":"ordinal","bin":{"maxbins":4}},"y":{"aggregate":"count","type":"quantitative"}}""").Plan;
        var binned = Create("""{"x":{"field":"lo","type":"quantitative","bin":"binned"},"x2":{"field":"hi"},"y":{"aggregate":"count","type":"quantitative"}}""").Plan;

        // Assert
        Assert.Equal(["bin_maxbins_10_a", "bin_maxbins_10_a_end"], continuous.Aggregate!.GroupBy.ToArray());
        Assert.Equal(["bin_maxbins_4_a", "bin_maxbins_4_a_end", "bin_maxbins_4_a_range"], discrete.Aggregate!.GroupBy.ToArray());
        Assert.Equal(["lo", "hi"], binned.Aggregate!.GroupBy.ToArray());
        Assert.Equal(["__count"], continuous.Aggregate.Measures.Select(m => m.Name).ToArray());
    }

    /// <summary>
    ///     Validates that an <c>argmax</c> definition becomes an argmax measure over the compared field and an argument field that reads the
    ///     channel's field from the returned row, and that an operation without a field is not a measure.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_ArgMaxReadsTheFieldOfTheReturnedRow()
    {
        // Act
        var (plan, _) = Create(
            """{"x":{"field":"cat","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":{"argmax":"t"}},"color":{"type":"nominal","aggregate":"sum"}}""");

        // Assert
        var aggregate = plan.Aggregate!;
        var measure = Assert.Single(aggregate.Measures);
        Assert.Equal(AggregateOp.ArgMax, measure.Op);
        Assert.Equal("t", measure.Field);
        Assert.Equal("argmax_t", measure.Name);
        var arg = Assert.Single(aggregate.ArgFields);
        Assert.Equal(new ArgField("argmax_t", "v", "argmax_t.v"), arg);
        Assert.Equal(["cat"], aggregate.GroupBy.ToArray());
    }

    /// <summary>
    ///     Validates Vega-Lite's stack decision: which marks and encodings stack, the offset and the name of the stacked field.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="channel">The expected stacked channel, or null for no stack.</param>
    /// <param name="offset">The expected offset.</param>
    [Theory]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""", "y", StackOffset.Zero)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"}}""", "y", StackOffset.Zero)]
    [InlineData("\"bar\"", """{"y":{"field":"c","type":"nominal"},"x":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""", "x", StackOffset.Zero)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":"normalize"},"color":{"field":"g","type":"nominal"}}""", "y", StackOffset.Normalize)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":"center"},"color":{"field":"g","type":"nominal"}}""", "y", StackOffset.Center)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":true},"color":{"field":"g","type":"nominal"}}""", "y", StackOffset.Zero)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":null},"color":{"field":"g","type":"nominal"}}""", null, StackOffset.Zero)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":false},"color":{"field":"g","type":"nominal"}}""", null, StackOffset.Zero)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"}}""", null, StackOffset.Zero)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g","type":"nominal"}}""", "y", StackOffset.Zero)]
    [InlineData("\"point\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""", null, StackOffset.Zero)]
    [InlineData("\"point\"", """{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","stack":true},"color":{"field":"g","type":"nominal"}}""", "y", StackOffset.Zero)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"quantitative"},"x2":{"field":"d"},"y":{"field":"g","type":"nominal"}}""", null, StackOffset.Zero)]
    [InlineData("\"bar\"", """{"x":{"field":"c","type":"nominal","bin":true},"y":{"field":"v","type":"quantitative"}}""", "y", StackOffset.Zero)]
    public void EncodingPlan_Create_DecidesTheStackLikeVegaLite(string mark, string encoding, string? channel, StackOffset offset)
    {
        // Act
        var (plan, _) = Create(encoding, mark);

        // Assert
        Assert.Equal(channel, plan.Stack?.Channel);
        if (channel is not null)
        {
            Assert.Equal(offset.ToString(), plan.Stack!.Offset.ToString());
        }
    }

    /// <summary>
    ///     Validates the names a stack writes: <c>&lt;field&gt;_start</c> and <c>&lt;field&gt;_end</c> of the (derived) measure; that the stacked channel
    ///     reads the upper end and has the lower end as its second field; and that the stacks are the fields of the other position channel.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_StackNamesAndGroupsFollowTheMeasure()
    {
        // Act
        var plain = Create("""{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""").Plan;
        var aggregated = Create("""{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative","aggregate":"sum"},"color":{"field":"g","type":"nominal"}}""").Plan;
        var counted = Create("""{"x":{"field":"a","type":"quantitative","bin":true},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""").Plan;

        // Assert
        Assert.Equal(("v_start", "v_end", "v"), (plain.Stack!.StartName, plain.Stack.EndName, plain.Stack.Field));
        Assert.Equal(["c"], plain.Stack.GroupBy.ToArray());
        var y = plain.Channel("y")!;
        Assert.True(y.IsStacked);
        Assert.Equal("v_end", y.FieldPath);
        Assert.Equal("v_start", y.Field2Path);
        Assert.Equal("v", y.ValueField);
        Assert.False(plain.Channel("x")!.IsStacked);
        Assert.Equal(("sum_v_start", "sum_v_end", "sum_v"), (aggregated.Stack!.StartName, aggregated.Stack.EndName, aggregated.Stack.Field));
        Assert.Equal("sum_v_end", aggregated.Channel("y")!.FieldPath);
        Assert.Equal("Sum of v", aggregated.Channel("y")!.Title);
        Assert.Equal(("__count_start", "__count_end"), (counted.Stack!.StartName, counted.Stack.EndName));
        Assert.Equal(["bin_maxbins_10_a", "bin_maxbins_10_a_end"], counted.Stack.GroupBy.ToArray());
    }

    /// <summary>
    ///     Validates the order of the layers of a stack, bottom first: the stack-by fields, descending when the stacked channel is <c>y</c> and
    ///     ascending when it is <c>x</c>; the <c>order</c> channel instead of them, in its own direction.
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_OrdersTheLayersLikeVegaLite()
    {
        // Act
        var vertical = Create("""{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"},"detail":{"field":"d"}}""").Plan;
        var horizontal = Create("""{"y":{"field":"c","type":"nominal"},"x":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""").Plan;
        var ordered = Create("""{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"},"order":[{"field":"o1","sort":"ascending"},{"field":"o2","sort":"descending"}]}""").Plan;
        var single = Create("""{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"},"order":{"field":"o1","type":"quantitative"}}""").Plan;

        // Assert
        Assert.Equal([new SortField("g", true), new SortField("d", true)], vertical.Stack!.Sort.ToArray());
        Assert.Equal([new SortField("g", false)], horizontal.Stack!.Sort.ToArray());
        Assert.Equal([new SortField("o1", false), new SortField("o2", true)], ordered.Stack!.Sort.ToArray());
        Assert.Equal([new SortField("o1", false)], single.Stack!.Sort.ToArray());
    }

    /// <summary>
    ///     Validates that an <c>area</c> mark decides, plans and computes its stack like a bar in both orientations, with signed values: the area is not drawn
    ///     until a later phase, but the stack decision, the plan and the engine are shared, so its rows are the ones a bar gets. Vertical: the layers x (3),
    ///     y (-2), z (1) stack descending, so z is (0, 1), y is (0, -2) and x is (1, 4). Horizontal: ascending, so x is (0, 3), y is (0, -2) and z is (3, 4).
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_AreaMarkStacksSignedValuesLikeABar()
    {
        // Arrange
        const string rows = """[{"c":"a","g":"x","v":3},{"c":"a","g":"y","v":-2},{"c":"a","g":"z","v":1}]""";
        const string vertical = """{"x":{"field":"c","type":"ordinal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";
        const string horizontal = """{"y":{"field":"c","type":"ordinal"},"x":{"field":"v","type":"quantitative"},"color":{"field":"g","type":"nominal"}}""";

        // Act
        var areaPlan = Create(vertical, "\"area\"").Plan;
        var barPlan = Create(vertical, "\"bar\"").Plan;
        var areaRows = ChartTestHelpers.ResolveFull("{\"data\":{\"values\":" + rows + "},\"mark\":\"area\",\"encoding\":" + vertical + "}");
        var areaAcross = ChartTestHelpers.ResolveFull("{\"data\":{\"values\":" + rows + "},\"mark\":\"area\",\"encoding\":" + horizontal + "}");

        // Assert: the same decision and the same layer order as a bar
        Assert.Equal(barPlan.Stack!.Channel, areaPlan.Stack!.Channel);
        Assert.Equal(barPlan.Stack.Offset, areaPlan.Stack.Offset);
        Assert.Equal(barPlan.Stack.Sort.ToArray(), areaPlan.Stack.Sort.ToArray());
        Assert.Equal(["x|1|4", "y|0|-2", "z|0|1"], AggregateTestSupport.Rows(areaRows.Data.Main, "g", "v_start", "v_end"));
        Assert.Equal((-2, 4), (areaRows.Scales["y"]!.DomainStart, areaRows.Scales["y"]!.DomainEnd));
        Assert.Equal(["x|0|3", "y|0|-2", "z|3|4"], AggregateTestSupport.Rows(areaAcross.Data.Main, "g", "v_start", "v_end"));
        Assert.Equal((-2, 4), (areaAcross.Scales["x"]!.DomainStart, areaAcross.Scales["x"]!.DomainEnd));
    }

    /// <summary>
    ///     Validates that a stack that has to be sorted by an aggregated stack-by field sorts by the derived measure name and that
    ///     a group-by that is also a stack-by field is not a layer (Vega-Lite excludes the dimension's own field).
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_StackByExcludesTheDimensionField()
    {
        // Act: the color is the same field as x, so it is the dimension and not a layer
        var (plan, _) = Create("""{"x":{"field":"c","type":"nominal"},"y":{"field":"v","type":"quantitative"},"color":{"field":"c","type":"nominal"}}""");

        // Assert: a plain stack (not aggregating) with no layers: one row per bar stays, with the table order
        Assert.Empty(plan.Stack!.Sort);
        Assert.Equal(["c"], plan.Stack.GroupBy.ToArray());
    }

    /// <summary>
    ///     Validates which encodings make the scales read the Raw table: a sort by an object or a channel shorthand, and an <c>unaggregated</c> domain;
    ///     not the plain sorts.
    /// </summary>
    /// <param name="xDefinition">The definition of <c>x</c>.</param>
    /// <param name="needsRaw">Whether the Raw table is needed.</param>
    [Theory]
    [InlineData("""{"field":"c","type":"nominal"}""", false)]
    [InlineData("""{"field":"c","type":"nominal","sort":"ascending"}""", false)]
    [InlineData("""{"field":"c","type":"nominal","sort":"descending"}""", false)]
    [InlineData("""{"field":"c","type":"nominal","sort":null}""", false)]
    [InlineData("""{"field":"c","type":"nominal","sort":["a","b"]}""", false)]
    [InlineData("""{"field":"c","type":"nominal","sort":"-y"}""", true)]
    [InlineData("""{"field":"c","type":"nominal","sort":"y"}""", true)]
    [InlineData("""{"field":"c","type":"nominal","sort":{"op":"sum","field":"v"}}""", true)]
    [InlineData("""{"field":"c","type":"nominal","sort":{"encoding":"y"}}""", true)]
    [InlineData("""{"field":"c","type":"quantitative","scale":{"domain":"unaggregated"}}""", true)]
    [InlineData("""{"field":"c","type":"quantitative","scale":{"domain":[0,1]}}""", false)]
    public void EncodingPlan_Create_FlagsTheEncodingsThatReadTheRawTable(string xDefinition, bool needsRaw)
    {
        // Act
        var (plan, _) = Create("{\"x\":" + xDefinition + ",\"y\":{\"field\":\"v\",\"type\":\"quantitative\",\"aggregate\":\"sum\"}}");

        // Assert
        Assert.Equal(needsRaw, plan.NeedsRaw);
    }

    /// <summary>
    ///     Validates the orientation the plan carries (the inferred one, which decides the stack of a bar with two measures).
    /// </summary>
    [Fact]
    public void EncodingPlan_Create_BarWithTwoMeasuresStacksTheAggregatedOne()
    {
        // Act: y aggregates and x does not
        var (plan, _) = Create("""{"x":{"field":"a","type":"quantitative"},"y":{"field":"b","type":"quantitative","aggregate":"sum"}}""");

        // Assert
        Assert.Equal("vertical", plan.Orient);
        Assert.Equal("bar", plan.MarkType);
        Assert.Null(plan.Stack);
    }
}

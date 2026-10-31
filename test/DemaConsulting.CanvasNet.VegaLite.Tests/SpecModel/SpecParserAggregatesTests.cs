using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests for the validation of the aggregating transforms (<c>aggregate</c>, <c>joinaggregate</c>, <c>window</c>, <c>bin</c>,
///     <c>stack</c>, <c>impute</c>) and of the <c>aggregate</c>, <c>bin</c>, <c>stack</c>, <c>sort</c> and <c>order</c> properties of an
///     encoding, which are part of <see cref="SpecParser"/>.
/// </summary>
public class SpecParserAggregatesTests
{
    /// <summary>
    ///     Parses a specification that holds one transform and returns the exception the parser throws for it.
    /// </summary>
    /// <param name="entry">The text of the transform object.</param>
    /// <returns>The exception.</returns>
    private static VegaLiteSpecException FailTransform(string entry) =>
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"transform\":[" + entry + "]}", "t.json"));

    /// <summary>
    ///     Parses a specification that holds one channel definition of a bar and returns the exception the parser throws for it.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="definition">The text of the channel value.</param>
    /// <returns>The exception.</returns>
    private static VegaLiteSpecException FailChannel(string channel, string definition) =>
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"mark\":\"bar\",\"encoding\":{\"" + channel + "\":" + definition + "}}", "t.json"));

    /// <summary>
    ///     Asserts that an exception is located at a pointer with a text position and the file.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="pointer">The expected pointer.</param>
    private static void AssertLocated(VegaLiteSpecException ex, string pointer)
    {
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Equal("t.json", ex.Location.File);
        Assert.True(ex.Location.Line >= 1);
        Assert.True(ex.Location.Column >= 1);
        Assert.StartsWith("t.json:", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a wrongly shaped <c>aggregate</c> or <c>joinaggregate</c> transform is a located error at the offending value.
    /// </summary>
    /// <param name="entry">The text of the transform object.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("""{"aggregate":3}""", "/transform/0/aggregate")]
    [InlineData("""{"aggregate":{"op":"count"}}""", "/transform/0/aggregate")]
    [InlineData("""{"aggregate":[3]}""", "/transform/0/aggregate/0")]
    [InlineData("""{"aggregate":[{"op":"sum"}]}""", "/transform/0/aggregate/0")]
    [InlineData("""{"aggregate":[{"field":"a"}]}""", "/transform/0/aggregate/0")]
    [InlineData("""{"aggregate":[{"op":3,"field":"a"}]}""", "/transform/0/aggregate/0/op")]
    [InlineData("""{"aggregate":[{"op":"nope","field":"a"}]}""", "/transform/0/aggregate/0/op")]
    [InlineData("""{"aggregate":[{"op":"SUM","field":"a"}]}""", "/transform/0/aggregate/0/op")]
    [InlineData("""{"aggregate":[{"op":"rank","field":"a"}]}""", "/transform/0/aggregate/0/op")]
    [InlineData("""{"aggregate":[{"op":"count"},{"op":"mean","field":"a"},{"op":"median"}]}""", "/transform/0/aggregate/2")]
    [InlineData("""{"aggregate":[{"op":"count","as":""}]}""", "/transform/0/aggregate/0/as")]
    [InlineData("""{"aggregate":[{"op":"count","as":3}]}""", "/transform/0/aggregate/0/as")]
    [InlineData("""{"aggregate":[{"op":"sum","field":"a["}]}""", "/transform/0/aggregate/0/field")]
    [InlineData("""{"aggregate":[{"op":"sum","field":3}]}""", "/transform/0/aggregate/0/field")]
    [InlineData("""{"aggregate":[{"op":"exponential","field":"a","param":0}]}""", "/transform/0/aggregate/0/param")]
    [InlineData("""{"aggregate":[{"op":"exponentialb","field":"a","param":1.5}]}""", "/transform/0/aggregate/0/param")]
    [InlineData("""{"aggregate":[{"op":"exponential","field":"a","param":"r"}]}""", "/transform/0/aggregate/0/param")]
    [InlineData("""{"aggregate":[],"groupby":"a"}""", "/transform/0/groupby")]
    [InlineData("""{"aggregate":[],"groupby":[3]}""", "/transform/0/groupby/0")]
    [InlineData("""{"aggregate":[],"groupby":["a["]}""", "/transform/0/groupby/0")]
    [InlineData("""{"joinaggregate":3}""", "/transform/0/joinaggregate")]
    [InlineData("""{"joinaggregate":[{"op":"sum"}]}""", "/transform/0/joinaggregate/0")]
    [InlineData("""{"joinaggregate":[{"op":"nope","field":"a"}]}""", "/transform/0/joinaggregate/0/op")]
    [InlineData("""{"joinaggregate":[{"op":"max","field":"a","as":""}]}""", "/transform/0/joinaggregate/0/as")]
    [InlineData("""{"joinaggregate":[],"groupby":{"a":1}}""", "/transform/0/groupby")]
    public void SpecParser_Parse_AggregateTransformShapesArePinned(string entry, string pointer) =>
        AssertLocated(FailTransform(entry), pointer);

    /// <summary>
    ///     Validates that a wrongly shaped <c>window</c> transform is a located error at the offending value.
    /// </summary>
    /// <param name="entry">The text of the transform object.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("""{"window":3}""", "/transform/0/window")]
    [InlineData("""{"window":["rank"]}""", "/transform/0/window/0")]
    [InlineData("""{"window":[{"op":"nope"}]}""", "/transform/0/window/0/op")]
    [InlineData("""{"window":[{"op":"rank","as":""}]}""", "/transform/0/window/0/as")]
    [InlineData("""{"window":[{"op":"ntile"}]}""", "/transform/0/window/0")]
    [InlineData("""{"window":[{"op":"ntile","param":0}]}""", "/transform/0/window/0/param")]
    [InlineData("""{"window":[{"op":"ntile","param":1.5}]}""", "/transform/0/window/0/param")]
    [InlineData("""{"window":[{"op":"ntile","param":"4"}]}""", "/transform/0/window/0/param")]
    [InlineData("""{"window":[{"op":"ntile","param":2147483648}]}""", "/transform/0/window/0/param")]
    [InlineData("""{"window":[{"op":"lag"}]}""", "/transform/0/window/0")]
    [InlineData("""{"window":[{"op":"lag","field":"a","param":0}]}""", "/transform/0/window/0/param")]
    [InlineData("""{"window":[{"op":"lead","field":"a","param":-1}]}""", "/transform/0/window/0/param")]
    [InlineData("""{"window":[{"op":"nth_value","field":"a"}]}""", "/transform/0/window/0")]
    [InlineData("""{"window":[{"op":"nth_value","field":"a","param":0}]}""", "/transform/0/window/0/param")]
    [InlineData("""{"window":[{"op":"first_value"}]}""", "/transform/0/window/0")]
    [InlineData("""{"window":[{"op":"sum"}]}""", "/transform/0/window/0")]
    [InlineData("""{"window":[{"op":"exponential","field":"a","param":2}]}""", "/transform/0/window/0/param")]
    [InlineData("""{"window":[{"op":"rank"}],"groupby":"a"}""", "/transform/0/groupby")]
    [InlineData("""{"window":[{"op":"rank"}],"sort":3}""", "/transform/0/sort")]
    [InlineData("""{"window":[{"op":"rank"}],"sort":["a"]}""", "/transform/0/sort/0")]
    [InlineData("""{"window":[{"op":"rank"}],"sort":[{"order":"ascending"}]}""", "/transform/0/sort/0")]
    [InlineData("""{"window":[{"op":"rank"}],"sort":[{"field":3}]}""", "/transform/0/sort/0/field")]
    [InlineData("""{"window":[{"op":"rank"}],"sort":[{"field":"a","order":"up"}]}""", "/transform/0/sort/0/order")]
    [InlineData("""{"window":[{"op":"rank"}],"frame":3}""", "/transform/0/frame")]
    [InlineData("""{"window":[{"op":"rank"}],"frame":[1]}""", "/transform/0/frame")]
    [InlineData("""{"window":[{"op":"rank"}],"frame":[1,2,3]}""", "/transform/0/frame")]
    [InlineData("""{"window":[{"op":"rank"}],"frame":[1.5,2]}""", "/transform/0/frame/0")]
    [InlineData("""{"window":[{"op":"rank"}],"frame":[null,"a"]}""", "/transform/0/frame/1")]
    [InlineData("""{"window":[{"op":"rank"}],"frame":[0,3000000000]}""", "/transform/0/frame/1")]
    [InlineData("""{"window":[{"op":"rank"}],"ignorePeers":"yes"}""", "/transform/0/ignorePeers")]
    public void SpecParser_Parse_WindowTransformShapesArePinned(string entry, string pointer) =>
        AssertLocated(FailTransform(entry), pointer);

    /// <summary>
    ///     Validates that a wrongly shaped <c>bin</c> transform, or wrong bin parameters, are a located error at the offending value.
    /// </summary>
    /// <param name="entry">The text of the transform object.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("""{"bin":3,"field":"a"}""", "/transform/0/bin")]
    [InlineData("""{"bin":false,"field":"a"}""", "/transform/0/bin")]
    [InlineData("""{"bin":"binned","field":"a"}""", "/transform/0/bin")]
    [InlineData("""{"bin":true}""", "/transform/0")]
    [InlineData("""{"bin":true,"field":3}""", "/transform/0/field")]
    [InlineData("""{"bin":true,"field":"a["}""", "/transform/0/field")]
    [InlineData("""{"bin":true,"field":"a","as":""}""", "/transform/0/as")]
    [InlineData("""{"bin":true,"field":"a","as":3}""", "/transform/0/as")]
    [InlineData("""{"bin":true,"field":"a","as":["x"]}""", "/transform/0/as")]
    [InlineData("""{"bin":true,"field":"a","as":["x","y","z"]}""", "/transform/0/as")]
    [InlineData("""{"bin":true,"field":"a","as":["x",""]}""", "/transform/0/as")]
    [InlineData("""{"bin":{"maxbins":1},"field":"a"}""", "/transform/0/bin/maxbins")]
    [InlineData("""{"bin":{"maxbins":"ten"},"field":"a"}""", "/transform/0/bin/maxbins")]
    [InlineData("""{"bin":{"step":0},"field":"a"}""", "/transform/0/bin/step")]
    [InlineData("""{"bin":{"step":-5},"field":"a"}""", "/transform/0/bin/step")]
    [InlineData("""{"bin":{"steps":[]},"field":"a"}""", "/transform/0/bin/steps")]
    [InlineData("""{"bin":{"steps":5},"field":"a"}""", "/transform/0/bin/steps")]
    [InlineData("""{"bin":{"steps":[1,0]},"field":"a"}""", "/transform/0/bin/steps/1")]
    [InlineData("""{"bin":{"steps":[1,"2"]},"field":"a"}""", "/transform/0/bin/steps/1")]
    [InlineData("""{"bin":{"divide":[]},"field":"a"}""", "/transform/0/bin/divide")]
    [InlineData("""{"bin":{"divide":[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17]},"field":"a"}""", "/transform/0/bin/divide")]
    [InlineData("""{"bin":{"divide":[5,-2]},"field":"a"}""", "/transform/0/bin/divide/1")]
    [InlineData("""{"bin":{"minstep":-1},"field":"a"}""", "/transform/0/bin/minstep")]
    [InlineData("""{"bin":{"base":1},"field":"a"}""", "/transform/0/bin/base")]
    [InlineData("""{"bin":{"base":"ten"},"field":"a"}""", "/transform/0/bin/base")]
    [InlineData("""{"bin":{"anchor":"zero"},"field":"a"}""", "/transform/0/bin/anchor")]
    [InlineData("""{"bin":{"nice":1},"field":"a"}""", "/transform/0/bin/nice")]
    [InlineData("""{"bin":{"binned":"yes"},"field":"a"}""", "/transform/0/bin/binned")]
    [InlineData("""{"bin":{"extent":[5,1]},"field":"a"}""", "/transform/0/bin/extent")]
    [InlineData("""{"bin":{"extent":[1]},"field":"a"}""", "/transform/0/bin/extent")]
    [InlineData("""{"bin":{"extent":["a",2]},"field":"a"}""", "/transform/0/bin/extent")]
    [InlineData("""{"bin":{"extent":3},"field":"a"}""", "/transform/0/bin/extent")]
    [InlineData("""{"bin":{"extent":{"param":3}},"field":"a"}""", "/transform/0/bin/extent")]
    public void SpecParser_Parse_BinParamsAndBinTransformShapesArePinned(string entry, string pointer) =>
        AssertLocated(FailTransform(entry), pointer);

    /// <summary>
    ///     Validates that a wrongly shaped <c>stack</c> or <c>impute</c> transform is a located error at the offending value.
    /// </summary>
    /// <param name="entry">The text of the transform object.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("""{"stack":3}""", "/transform/0/stack")]
    [InlineData("""{"stack":"a["}""", "/transform/0/stack")]
    [InlineData("""{"stack":"a","groupby":"g"}""", "/transform/0/groupby")]
    [InlineData("""{"stack":"a","groupby":[1]}""", "/transform/0/groupby/0")]
    [InlineData("""{"stack":"a","offset":"up"}""", "/transform/0/offset")]
    [InlineData("""{"stack":"a","offset":3}""", "/transform/0/offset")]
    [InlineData("""{"stack":"a","sort":3}""", "/transform/0/sort")]
    [InlineData("""{"stack":"a","sort":[{"field":"b","order":null},{"order":"ascending"}]}""", "/transform/0/sort/1")]
    [InlineData("""{"stack":"a","as":""}""", "/transform/0/as")]
    [InlineData("""{"stack":"a","as":["x"]}""", "/transform/0/as")]
    [InlineData("""{"stack":"a","as":3}""", "/transform/0/as")]
    [InlineData("""{"impute":"a"}""", "/transform/0")]
    [InlineData("""{"impute":3,"key":"k"}""", "/transform/0/impute")]
    [InlineData("""{"impute":"a","key":3}""", "/transform/0/key")]
    [InlineData("""{"impute":"a","key":"k["}""", "/transform/0/key")]
    [InlineData("""{"impute":"a","key":"k","keyvals":3}""", "/transform/0/keyvals")]
    [InlineData("""{"impute":"a","key":"k","keyvals":[[1]]}""", "/transform/0/keyvals/0")]
    [InlineData("""{"impute":"a","key":"k","keyvals":[1,{"x":1}]}""", "/transform/0/keyvals/1")]
    [InlineData("""{"impute":"a","key":"k","keyvals":{"start":0}}""", "/transform/0/keyvals")]
    [InlineData("""{"impute":"a","key":"k","keyvals":{"stop":"x"}}""", "/transform/0/keyvals/stop")]
    [InlineData("""{"impute":"a","key":"k","keyvals":{"stop":5,"start":"a"}}""", "/transform/0/keyvals/start")]
    [InlineData("""{"impute":"a","key":"k","keyvals":{"stop":5,"step":0}}""", "/transform/0/keyvals/step")]
    [InlineData("""{"impute":"a","key":"k","method":"mode"}""", "/transform/0/method")]
    [InlineData("""{"impute":"a","key":"k","method":3}""", "/transform/0/method")]
    [InlineData("""{"impute":"a","key":"k","value":{"a":1}}""", "/transform/0/value")]
    [InlineData("""{"impute":"a","key":"k","value":[1]}""", "/transform/0/value")]
    [InlineData("""{"impute":"a","key":"k","groupby":"g"}""", "/transform/0/groupby")]
    [InlineData("""{"impute":"a","key":"k","frame":[null]}""", "/transform/0/frame")]
    [InlineData("""{"impute":"a","key":"k","frame":[0.5,null]}""", "/transform/0/frame/0")]
    public void SpecParser_Parse_StackAndImputeTransformShapesArePinned(string entry, string pointer)
    {
        // Arrange / Act
        var ex = FailTransform(entry);

        // Assert
        AssertLocated(ex, pointer);
    }

    /// <summary>
    ///     Validates that a wrongly shaped <c>aggregate</c>, <c>bin</c>, <c>stack</c> or <c>sort</c> of a channel definition is a located
    ///     error at the offending value.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="definition">The text of the channel value.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("y", """{"field":"a","aggregate":"nope"}""", "/encoding/y/aggregate")]
    [InlineData("y", """{"field":"a","aggregate":"SUM"}""", "/encoding/y/aggregate")]
    [InlineData("y", """{"field":"a","aggregate":"argmax"}""", "/encoding/y/aggregate")]
    [InlineData("y", """{"field":"a","aggregate":"argmin"}""", "/encoding/y/aggregate")]
    [InlineData("y", """{"field":"a","aggregate":3}""", "/encoding/y/aggregate")]
    [InlineData("y", """{"field":"a","aggregate":{}}""", "/encoding/y/aggregate")]
    [InlineData("y", """{"field":"a","aggregate":{"foo":1}}""", "/encoding/y/aggregate")]
    [InlineData("y", """{"field":"a","aggregate":{"argmax":"b","argmin":"c"}}""", "/encoding/y/aggregate")]
    [InlineData("y", """{"field":"a","aggregate":{"argmax":3}}""", "/encoding/y/aggregate/argmax")]
    [InlineData("y", """{"field":"a","aggregate":{"argmin":"b["}}""", "/encoding/y/aggregate/argmin")]
    [InlineData("y", """{"field":"a","aggregate":{"exponential":0}}""", "/encoding/y/aggregate/exponential")]
    [InlineData("y", """{"field":"a","aggregate":{"exponentialb":2}}""", "/encoding/y/aggregate/exponentialb")]
    [InlineData("y", """{"field":"a","aggregate":{"exponential":"r"}}""", "/encoding/y/aggregate/exponential")]
    [InlineData("x", """{"field":"a","bin":"foo"}""", "/encoding/x/bin")]
    [InlineData("x", """{"field":"a","bin":3}""", "/encoding/x/bin")]
    [InlineData("x", """{"field":"a","bin":{"maxbins":1}}""", "/encoding/x/bin/maxbins")]
    [InlineData("x", """{"field":"a","bin":{"step":0}}""", "/encoding/x/bin/step")]
    [InlineData("x", """{"field":"a","bin":{"extent":[3,1]}}""", "/encoding/x/bin/extent")]
    [InlineData("x", """{"field":"a","bin":{"steps":[]}}""", "/encoding/x/bin/steps")]
    [InlineData("y", """{"field":"a","stack":"sideways"}""", "/encoding/y/stack")]
    [InlineData("y", """{"field":"a","stack":3}""", "/encoding/y/stack")]
    [InlineData("x", """{"field":"a","sort":"sideways"}""", "/encoding/x/sort")]
    [InlineData("x", """{"field":"a","sort":"-nope"}""", "/encoding/x/sort")]
    [InlineData("x", """{"field":"a","sort":"--y"}""", "/encoding/x/sort")]
    [InlineData("x", """{"field":"a","sort":3}""", "/encoding/x/sort")]
    [InlineData("x", """{"field":"a","sort":{"op":"nope"}}""", "/encoding/x/sort/op")]
    [InlineData("x", """{"field":"a","sort":{"op":"argmax"}}""", "/encoding/x/sort/op")]
    [InlineData("x", """{"field":"a","sort":{"op":"argmin","field":"v"}}""", "/encoding/x/sort/op")]
    [InlineData("x", """{"field":"a","sort":{"op":3}}""", "/encoding/x/sort/op")]
    [InlineData("x", """{"field":"a","sort":{"order":"up"}}""", "/encoding/x/sort/order")]
    [InlineData("x", """{"field":"a","sort":{"order":3}}""", "/encoding/x/sort/order")]
    [InlineData("x", """{"field":"a","sort":{"field":"v["}}""", "/encoding/x/sort/field")]
    [InlineData("x", """{"field":"a","sort":{"field":3}}""", "/encoding/x/sort/field")]
    [InlineData("x", """{"field":"a","sort":{"encoding":"nope"}}""", "/encoding/x/sort/encoding")]
    [InlineData("x", """{"field":"a","sort":{"encoding":3}}""", "/encoding/x/sort/encoding")]
    [InlineData("x", """{"field":"a","sort":{"encoding":"y","field":"v"}}""", "/encoding/x/sort/field")]
    [InlineData("x", """{"field":"a","sort":{"encoding":"y","op":"sum"}}""", "/encoding/x/sort/op")]
    [InlineData("x", """{"field":"a","sort":[{"a":1}]}""", "/encoding/x/sort/0")]
    [InlineData("x", """{"field":"a","sort":[["a"]]}""", "/encoding/x/sort/0")]
    [InlineData("x", """{"field":"a","sort":["a",{}]}""", "/encoding/x/sort/1")]
    [InlineData("x", """{"field":"a","sort":["a","b",[]]}""", "/encoding/x/sort/2")]
    [InlineData("x", """{"field":"a","type":"nominal","sort":[{"year":2024}]}""", "/encoding/x/sort/0")]
    [InlineData("x", """{"field":"a","type":"temporal","sort":[{"yr":2024}]}""", "/encoding/x/sort/0/yr")]
    [InlineData("x", """{"field":"a","type":"temporal","sort":[{"year":2024},{}]}""", "/encoding/x/sort/1")]
    [InlineData("x", """{"field":"a","type":"temporal","sort":[{"year":{"a":1}}]}""", "/encoding/x/sort/0/year")]
    [InlineData("order", """{"field":"a","sort":"sideways"}""", "/encoding/order/sort")]
    [InlineData("order", """{"field":"a","sort":{"op":"sum"}}""", "/encoding/order/sort")]
    [InlineData("order", """[{"field":"a"},{"field":"b","sort":"sideways"}]""", "/encoding/order/1/sort")]
    [InlineData("order", """3""", "/encoding/order")]
    [InlineData("order", """[3]""", "/encoding/order/0")]
    [InlineData("x2", """3""", "/encoding/x2")]
    [InlineData("y2", """{"field":"a","aggregate":"nope"}""", "/encoding/y2/aggregate")]
    public void SpecParser_Parse_EncodingAggregateBinStackSortShapesArePinned(string channel, string definition, string pointer)
    {
        // Arrange / Act
        var ex = FailChannel(channel, definition);

        // Assert
        AssertLocated(ex, pointer);
    }

    /// <summary>
    ///     Validates the diagnostics of a <c>sort</c> that is accepted: an array that mixes the kinds of its values is one warning at the array (and the
    ///     values are still accepted, null included), an array of one kind is silent, a date-time object is accepted on a temporal field, and a key that a
    ///     sort object does not define is a <c>vl-unknown-property</c> warning at the key.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_SortArrayAndObjectWarningsAreLocated()
    {
        // Arrange
        static SpecTree Parse(string definition) =>
            SpecParser.Parse("{\"mark\":\"bar\",\"encoding\":{\"x\":" + definition + "}}", "t.json");

        // Act
        var mixed = Parse("""{"field":"a","sort":[1,"b"]}""");
        var withNull = Parse("""{"field":"a","sort":[null,"a"]}""");
        var withBoolean = Parse("""{"field":"a","sort":["a",true,"b"]}""");
        var strings = Parse("""{"field":"a","sort":["a","b"]}""");
        var numbers = Parse("""{"field":"a","type":"ordinal","sort":[3,1,2]}""");
        var booleans = Parse("""{"field":"a","sort":[true,false]}""");
        var nulls = Parse("""{"field":"a","sort":[null,null]}""");
        var empty = Parse("""{"field":"a","sort":[]}""");
        var temporal = Parse("""{"field":"a","type":"temporal","sort":[{"year":2024,"month":"jan"},{"year":2025,"utc":true}]}""");
        var unknown = Parse("""{"field":"a","sort":{"op":"sum","bogus":1}}""");
        var known = Parse("""{"field":"a","sort":{"op":"sum","field":"v","order":"descending"}}""");

        // Assert: one warning at the array, with the pointer, the line and the column
        var warning = Assert.Single(mixed.Warnings);
        Assert.Equal("vl-encoding-sort-mixed-types", warning.Id);
        Assert.Equal(FeatureReportKind.Warning, warning.Kind);
        Assert.Equal("/encoding/x/sort", warning.Location.Pointer);
        Assert.Equal("t.json", warning.Location.File);
        Assert.True(warning.Location.Line >= 1 && warning.Location.Column >= 1);
        Assert.Equal("vl-encoding-sort-mixed-types", Assert.Single(withNull.Warnings).Id);
        Assert.Equal("vl-encoding-sort-mixed-types", Assert.Single(withBoolean.Warnings).Id);
        Assert.Equal(3, withBoolean.Root["encoding"]!["x"]!["sort"]!.Items.Count);

        // Assert: arrays of one kind (and no array) have nothing to warn about
        Assert.Empty(strings.Warnings);
        Assert.Empty(numbers.Warnings);
        Assert.Empty(booleans.Warnings);
        Assert.Empty(nulls.Warnings);
        Assert.Empty(empty.Warnings);
        Assert.Empty(temporal.Warnings);
        Assert.Empty(known.Warnings);

        // Assert: an unknown key of a sort object is warned about at the key, once
        var unknownWarning = Assert.Single(unknown.Warnings);
        Assert.Equal("vl-unknown-property", unknownWarning.Id);
        Assert.Equal("/encoding/x/sort/bogus", unknownWarning.Location.Pointer);
        Assert.Contains("a sort definition", unknownWarning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the channel that a <c>sort</c> refers to: the shorthand and the <c>encoding</c> of a sort object; any other form refers to none.
    /// </summary>
    [Fact]
    public void SpecParser_TryGetSortChannel_ReadsTheShorthandAndTheEncoding()
    {
        // Arrange
        static string? Channel(string sort) =>
            SpecParser.TryGetSortChannel(SpecParser.Parse("{\"s\":" + sort + "}").Root["s"]!, out var channel) ? channel : null;

        // Act / Assert
        Assert.Equal("y", Channel("\"y\""));
        Assert.Equal("y", Channel("\"-y\""));
        Assert.Equal("color", Channel("""{"encoding":"color","order":"descending"}"""));
        Assert.Null(Channel("\"ascending\""));
        Assert.Null(Channel("""{"op":"sum","field":"v"}"""));
        Assert.Null(Channel("""["a","b"]"""));
        Assert.Null(Channel("null"));
        Assert.Throws<ArgumentNullException>(() => SpecParser.TryGetSortChannel(null!, out _));
    }

    /// <summary>
    ///     Validates that every valid form of the aggregating transforms is accepted without a warning, including every operation name.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ValidAggregatingTransformsAreAccepted()
    {
        // Arrange: original specification that uses each transform in its valid forms
        const string json = """
            {
              "data": { "values": [ { "a": 1, "g": "x", "k": 1 } ] },
              "transform": [
                { "aggregate": [ { "op": "count" }, { "op": "sum", "field": "a", "as": "t" }, { "op": "average", "field": "a" },
                                 { "op": "q1", "field": "a" }, { "op": "argmax", "field": "a" }, { "op": "exponential", "field": "a", "param": 0.5 },
                                 { "op": "values", "field": "a", "param": 3 } ], "groupby": [ "g", "a.b" ] },
                { "aggregate": [] },
                { "joinaggregate": [ { "op": "max", "field": "a", "as": "m" }, { "op": "missing", "field": "a" } ] },
                { "window": [ { "op": "row_number" }, { "op": "rank", "as": "r" }, { "op": "ntile", "param": 4 }, { "op": "lag", "field": "a" },
                              { "op": "lead", "field": "a", "param": 2 }, { "op": "nth_value", "field": "a", "param": 1 },
                              { "op": "sum", "field": "a" }, { "op": "count" }, { "op": "prev_value", "field": "a" } ],
                  "sort": [ { "field": "a" }, { "field": "k", "order": "descending" }, { "field": "g", "order": null } ],
                  "groupby": [ "g" ], "frame": [ null, 0 ], "ignorePeers": true },
                { "window": [ { "op": "mean", "field": "a" } ], "frame": [ -3, 3 ] },
                { "bin": true, "field": "a" },
                { "bin": { "maxbins": 20, "extent": [ 0, 100 ], "nice": false, "step": 5, "steps": [ 1, 2, 5 ], "minstep": 0.5, "divide": [ 5, 2 ], "base": 10, "anchor": 0, "binned": false },
                  "field": "a", "as": [ "b0", "b1" ] },
                { "bin": { "extent": { "param": "brush" } }, "field": "a", "as": "b" },
                { "stack": "a" },
                { "stack": "a", "groupby": [ "g" ], "offset": "normalize", "sort": [ { "field": "k", "order": "descending" } ], "as": [ "s0", "s1" ] },
                { "stack": "a", "offset": "center", "as": "s" },
                { "impute": "a", "key": "k" },
                { "impute": "a", "key": "k", "keyvals": [ 1, "b", null, true ], "method": "mean", "groupby": [ "g" ], "frame": [ -1, 1 ] },
                { "impute": "a", "key": "k", "keyvals": { "start": 0, "stop": 10, "step": 2 }, "method": "value", "value": null },
                { "impute": "a", "key": "k", "keyvals": { "stop": 10, "step": -1 }, "value": "none" }
              ],
              "mark": "bar"
            }
            """;

        // Act
        var tree = SpecParser.Parse(json);

        // Assert: accepted and nothing to warn about
        Assert.Empty(tree.Warnings);
        Assert.Equal(15, tree.Root["transform"]!.Items.Count);
    }

    /// <summary>
    ///     Validates that every valid form of the encoding properties and secondary channels is accepted without a warning.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ValidEncodingAggregateBinStackSortFormsAreAccepted()
    {
        // Arrange: original specification that uses the properties in their valid forms
        const string json = """
            {
              "data": { "values": [ { "a": 1 } ] },
              "mark": "bar",
              "encoding": {
                "x": { "field": "a", "type": "quantitative", "bin": { "maxbins": 8, "extent": [ 0, 10 ], "anchor": 0 }, "sort": { "op": "sum", "field": "v", "order": "descending" } },
                "x2": { "field": "b", "type": "quantitative", "aggregate": "max", "title": "end" },
                "y": { "aggregate": "count", "type": "quantitative", "stack": "normalize" },
                "color": { "field": "c", "type": "nominal", "aggregate": { "argmax": "d" }, "sort": "-y" },
                "size": { "field": "c", "aggregate": { "exponentialb": 0.5 }, "sort": { "encoding": "y", "order": "ascending" } },
                "opacity": { "field": "c", "aggregate": "average", "bin": "binned", "stack": true, "sort": [ "a", "b" ] },
                "stroke": { "field": "c", "bin": false, "stack": null, "sort": null, "aggregate": null },
                "detail": [ { "field": "a", "aggregate": "distinct" }, { "field": "b" } ],
                "order": [ { "field": "a", "sort": "descending" }, { "field": "b", "aggregate": "sum" }, { "value": 1 } ]
              }
            }
            """;

        // Act
        var tree = SpecParser.Parse(json);

        // Assert
        Assert.Empty(tree.Warnings);
        Assert.Equal("/encoding/order/0/sort", tree.Root["encoding"]!["order"]!.Items[0]["sort"]!.Pointer);
    }

    /// <summary>
    ///     Validates that the sort shorthand helper accepts a channel with an optional minus sign and nothing else.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">Whether it is a shorthand.</param>
    /// <param name="channel">The expected channel.</param>
    /// <param name="descending">The expected direction.</param>
    [Theory]
    [InlineData("y", true, "y", false)]
    [InlineData("-y", true, "y", true)]
    [InlineData("-strokeWidth", true, "strokeWidth", true)]
    [InlineData("fillOpacity", true, "fillOpacity", false)]
    [InlineData("ascending", false, "", false)]
    [InlineData("--y", false, "", true)]
    [InlineData("-", false, "", true)]
    [InlineData("", false, "", false)]
    [InlineData("Y", false, "", false)]
    [InlineData("tooltip", false, "", false)]
    [InlineData(null, false, "", false)]
    public void SpecParser_TryParseSortShorthand_AcceptsAChannelWithAnOptionalMinus(string? text, bool expected, string channel, bool descending)
    {
        // Arrange / Act
        var result = SpecParser.TryParseSortShorthand(text, out var actualChannel, out var actualDescending);

        // Assert
        Assert.Equal(expected, result);
        Assert.Equal(channel, expected ? actualChannel : channel);
        Assert.Equal(descending, actualDescending);
    }

    /// <summary>
    ///     Validates which sort values the specification model says the renderer applies.
    /// </summary>
    /// <param name="sort">The text of the <c>sort</c> value of the x channel.</param>
    /// <param name="supported">Whether the sort is supported.</param>
    [Theory]
    [InlineData("\"ascending\"", true)]
    [InlineData("\"descending\"", true)]
    [InlineData("null", true)]
    [InlineData("[\"a\"]", true)]
    [InlineData("\"y\"", true)]
    [InlineData("\"-y\"", true)]
    [InlineData("\"-x\"", true)]
    [InlineData("\"-color\"", false)]
    [InlineData("\"-opacity\"", false)]
    [InlineData("{\"field\":\"v\"}", true)]
    [InlineData("{\"op\":\"min\"}", true)]
    [InlineData("{\"op\":\"values\",\"field\":\"v\"}", false)]
    [InlineData("{\"op\":\"exponential\",\"field\":\"v\"}", false)]
    [InlineData("{\"op\":\"exponentialb\",\"field\":\"v\"}", false)]
    [InlineData("{\"encoding\":\"y\"}", true)]
    [InlineData("{\"encoding\":\"color\"}", false)]
    [InlineData("{\"encoding\":\"size\",\"order\":\"descending\"}", false)]
    public void SpecParser_IsSupportedSort_RequiresAnOperationOrChannelTheRendererComputes(string sort, bool supported)
    {
        // Arrange: x with the sort and y that reads a field, in the same encoding
        var tree = SpecParser.Parse("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\",\"sort\":" + sort + "},\"y\":{\"aggregate\":\"count\"}}}");
        var definition = tree.Root["encoding"]!["x"]!;

        // Act
        var result = SpecParser.IsSupportedSort(definition["sort"]!, definition);

        // Assert
        Assert.Equal(supported, result);
    }

    /// <summary>
    ///     Validates that the supported-sort helper rejects null arguments.
    /// </summary>
    [Fact]
    public void SpecParser_IsSupportedSort_RejectsNullArguments()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"sort\":\"ascending\"}}}");
        var definition = tree.Root["encoding"]!["x"]!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SpecParser.IsSupportedSort(null!, definition));
        Assert.Throws<ArgumentNullException>(() => SpecParser.IsSupportedSort(definition["sort"]!, null!));
    }

    /// <summary>
    ///     Validates that every aggregating transform name (and the time unit transform) is one the renderer accepts, and that the plain-transform predicate still
    ///     rejects the names of later phases.
    /// </summary>
    [Fact]
    public void SpecParser_IsSupportedTransform_AcceptsTheAggregatingTransformsAndNotTheLaterPhases()
    {
        // Arrange / Act / Assert
        Assert.All(
            new[] { "calculate", "filter", "fold", "flatten", "sample", "pivot", "lookup", "aggregate", "joinaggregate", "window", "bin", "stack", "impute", "timeUnit" },
            name => Assert.True(SpecParser.IsSupportedTransform(name)));
        Assert.All(
            new[] { "regression", "loess", "density", "quantile", "extent", "timeunit", "" },
            name => Assert.False(SpecParser.IsSupportedTransform(name)));
    }
}

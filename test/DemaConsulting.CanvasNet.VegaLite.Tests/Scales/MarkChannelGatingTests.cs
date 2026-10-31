using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of which marks draw which channels (Vega-Lite's <c>supportMark</c> table for <c>theta</c>, <c>radius</c>, <c>size</c>, <c>strokeDash</c>, <c>angle</c> and <c>shape</c>), the reports
///     of the feature catalog that follow from it, and the marks whose <c>size</c> conditions are evaluated.
/// </summary>
public class MarkChannelGatingTests
{
    /// <summary>The marks of Vega-Lite that carry the channels of the table.</summary>
    private static readonly string[] Marks =
        ["point", "circle", "square", "rect", "bar", "tick", "rule", "line", "area", "trail", "text", "arc", "image"];

    /// <summary>
    ///     Validates the support table: for every channel the exact set of marks that draw it.
    /// </summary>
    [Fact]
    public void ScaleResolver_ChannelIsDrawn_FollowsTheSupportTable()
    {
        // Arrange: the marks that draw each channel, from Vega-Lite's channel support (every mark draws the channels that are not listed)
        var table = new Dictionary<string, string[]>
        {
            ["theta"] = ["text", "arc"],
            ["radius"] = ["text", "arc"],
            ["theta2"] = ["arc"],
            ["radius2"] = ["arc"],
            ["size"] = ["point", "circle", "square", "bar", "tick", "rule", "line", "trail", "text"],
            ["strokeDash"] = ["point", "circle", "square", "bar", "tick", "rule", "line", "image"],
            ["angle"] = ["point", "circle", "square", "text"],
            ["shape"] = ["point"],
            ["color"] = Marks,
            ["fill"] = Marks,
            ["stroke"] = Marks,
            ["opacity"] = Marks,
            ["fillOpacity"] = Marks,
            ["strokeOpacity"] = Marks,
            ["strokeWidth"] = Marks,
            ["x"] = Marks,
            ["y"] = Marks,
        };

        // Act / Assert: all 13 marks for each channel
        foreach (var (channel, drawers) in table)
        {
            foreach (var mark in Marks)
            {
                Assert.True(
                    drawers.Contains(mark) == ScaleResolver.ChannelIsDrawn(channel, mark),
                    $"The channel '{channel}' on the '{mark}' mark: expected {drawers.Contains(mark)}.");
            }
        }
    }

    /// <summary>
    ///     Validates that a channel which the mark does not draw has no scale: a size on an area and an arc, a stroke dash on a trail, a text and an area, an angle on a bar.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_ChannelsThatTheMarkDoesNotDrawHaveNoScale()
    {
        // Arrange: (mark, channel, field type) with a field that every mark could read
        var vectors = new (string Mark, string Channel, string Type, bool HasScale)[]
        {
            ("area", "size", "quantitative", false),
            ("arc", "size", "quantitative", false),
            ("line", "size", "quantitative", true),
            ("trail", "size", "quantitative", true),
            ("text", "size", "quantitative", true),
            ("area", "strokeDash", "nominal", false),
            ("trail", "strokeDash", "nominal", false),
            ("text", "strokeDash", "nominal", false),
            ("arc", "strokeDash", "nominal", false),
            ("line", "strokeDash", "nominal", true),
            ("text", "angle", "quantitative", true),
            ("line", "angle", "quantitative", false),
            ("arc", "shape", "nominal", false),
        };

        // Act / Assert
        foreach (var (mark, channel, type, hasScale) in vectors)
        {
            var tree = SpecParser.Parse(
                "{\"data\": {\"values\": [{\"f\": 1, \"g\": \"a\"}, {\"f\": 2, \"g\": \"b\"}]}, \"mark\": \"" + mark + "\", \"encoding\": {\""
                + channel + "\": {\"field\": \"" + (type == "nominal" ? "g" : "f") + "\", \"type\": \"" + type + "\"}}}");
            var table = DataLoader.Load(tree.Root["data"], tree.SourceName, null, TestContext.Current.CancellationToken);
            var scales = ScaleResolver.Resolve(tree.Root, table, ConfigResolver.Resolve(tree, null), tree.SourceName, null, TestContext.Current.CancellationToken);
            Assert.True(hasScale == (scales[channel] is not null), $"The '{channel}' scale of the '{mark}' mark: expected {hasScale}.");
        }
    }

    /// <summary>
    ///     Validates the reports of the feature catalog for the channels that the mark does not draw: a size on an area or an arc, a stroke dash on a trail, text, area or arc,
    ///     a shape on a line; and that a size on a rect (a long-standing exception) and the channels that are drawn are not reported.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ReportsChannelsThatTheMarkDoesNotDraw()
    {
        // Arrange / Act / Assert: the mark itself is reported too while it is not drawn; only the channel reports are listed
        Assert.Equal(["vl-channel-size"], ChannelReports("area", """ "size": {"field":"v","type":"quantitative"} """));
        Assert.Equal(["vl-channel-size"], ChannelReports("arc", """ "size": {"value": 5} """));
        Assert.Equal(["vl-channel-strokedash"], ChannelReports("trail", """ "strokeDash": {"field":"v","type":"nominal"} """));
        Assert.Equal(["vl-channel-strokedash"], ChannelReports("text", """ "strokeDash": {"value": [2, 2]} """));
        Assert.Equal(["vl-channel-strokedash"], ChannelReports("area", """ "strokeDash": {"field":"v","type":"nominal"} """));
        Assert.Equal(["vl-channel-shape"], ChannelReports("line", """ "shape": {"field":"v","type":"nominal"} """));
        Assert.Equal(["vl-channel-angle"], ChannelReports("line", """ "angle": {"field":"v","type":"quantitative"} """));

        // Assert: the drawn channels are not reported
        Assert.Empty(ChannelReports("line", """ "size": {"field":"v","type":"quantitative"}, "strokeDash": {"field":"v","type":"nominal"} """));
        Assert.Empty(ChannelReports("text", """ "size": {"field":"v","type":"quantitative"}, "angle": {"field":"v","type":"quantitative"} """));
        Assert.Empty(ChannelReports("trail", """ "size": {"field":"v","type":"quantitative"} """));
        Assert.Empty(ChannelReports("bar", """ "size": {"value": 4}, "strokeDash": {"value": [1, 1]} """));
        Assert.Empty(ChannelReports("rect", """ "size": {"value": 4} """));
    }

    /// <summary>
    ///     Validates which marks evaluate the conditions of a <c>size</c> channel: the ones that use a size (symbols, bar, rect, tick, rule, line, trail and text), shown by an invalid
    ///     expression that is a specification error only where the condition is evaluated; and that <c>theta</c> and <c>radius</c> take no condition.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_EvaluatesSizeConditionsOfMarksThatUseASize()
    {
        // Arrange: a condition whose value is an expression that does not compile
        const string broken = """ "size": { "condition": { "test": "datum.v > 1", "value": {"expr": "datum.v >"} } } """;

        // Act / Assert: compiled (and so rejected) for the marks that evaluate their size conditions
        foreach (var mark in new[] { "point", "circle", "square", "bar", "rect", "tick", "rule", "line", "trail", "text" })
        {
            Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec(mark, broken)));
        }

        // Assert: not evaluated, so not compiled, for the marks that have no size
        foreach (var mark in new[] { "area", "arc" })
        {
            Assert.NotNull(SpecParser.Parse(Spec(mark, broken)));
        }

        // Assert: the polar channels take no condition (Vega-Lite defines none), the others of the table do
        Assert.False(ChannelDefinitions.CanCondition("theta"));
        Assert.False(ChannelDefinitions.CanCondition("radius"));
        Assert.False(ChannelDefinitions.CanCondition("theta2"));
        Assert.False(ChannelDefinitions.CanCondition("radius2"));
        Assert.True(ChannelDefinitions.CanCondition("size"));
        Assert.True(ChannelDefinitions.CanCondition("angle"));
    }

    /// <summary>
    ///     Builds a specification of one mark over a tiny data set.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="channels">The properties of the encoding object, without the braces.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string channels) =>
        "{\"data\": {\"values\": [{\"v\": 1}, {\"v\": 2}]}, \"mark\": \"" + mark + "\", \"encoding\": {" + channels + "}}";

    /// <summary>
    ///     Lists the identifiers of the channel reports (<c>vl-channel-*</c>) of a one-mark specification.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="channels">The properties of the encoding object, without the braces.</param>
    /// <returns>The identifiers in the order of the catalog.</returns>
    private static string[] ChannelReports(string mark, string channels) =>
        [.. FeatureCatalog.Visit(SpecParser.Parse(Spec(mark, channels))).Where(r => r.Id.StartsWith("vl-channel-", StringComparison.Ordinal)).Select(r => r.Id)];
}

using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the scale resolver for binned fields on channels other than <c>x</c> and <c>y</c>: the bin-ordinal scale that a binned quantitative
///     color gets by default (its domain is the edges of the bins, its colors one per bin), the continuous and discrete scales of the other channels,
///     the explicit scale types that are accepted and the ones that give the default, an empty field, the limit on the bins and the cancellation.
///     The bins are derived by hand from Vega's algorithm (<c>maxbins</c> 6 over 3 to 97 gives the step 20 and the edges 0 to 100).
/// </summary>
public class ScaleResolverBinTests
{
    /// <summary>The rows: values from 3 to 97 that fall in the five bins 0-20, 20-40, 40-60, 60-80 and 80-100 as 2, 1, 2, 1 and 2 rows.</summary>
    private const string Data = """
        "data": { "values": [
          { "k": "a", "v": 3 }, { "k": "b", "v": 12 }, { "k": "a", "v": 27 }, { "k": "c", "v": 41 },
          { "k": "b", "v": 58 }, { "k": "a", "v": 66 }, { "k": "c", "v": 83 }, { "k": "b", "v": 97 }
        ] }
        """;

    /// <summary>The tolerance for numbers computed with fractions.</summary>
    private const int Precision = 9;

    /// <summary>
    ///     Builds a specification over the data.
    /// </summary>
    /// <param name="encoding">The encoding object text.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="data">The data property text; the sample rows when null.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string encoding, string mark = "point", string? data = null) =>
        "{" + (data ?? Data) + ", \"mark\": \"" + mark + "\", \"encoding\": " + encoding + "}";

    /// <summary>
    ///     Builds the encoding of one binned channel over the field <c>v</c>.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="scale">The scale object text, or empty.</param>
    /// <param name="bin">The <c>bin</c> value text.</param>
    /// <returns>The encoding object text.</returns>
    private static string Enc(string channel, string type, string scale = "", string bin = "true")
    {
        var scaleText = scale.Length > 0 ? ",\"scale\":" + scale : string.Empty;
        return "{\"" + channel + "\":{\"field\":\"v\",\"type\":\"" + type + "\",\"bin\":" + bin + scaleText + "}}";
    }

    /// <summary>
    ///     Resolves a specification and returns the scale of a channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="encoding">The encoding object text.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="data">The data property text, or null.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The scale.</returns>
    private static ResolvedScale Scale(string channel, string encoding, string mark = "point", string? data = null, VegaLiteLimits? limits = null) =>
        ChartTestHelpers.Parts(Spec(encoding, mark, data), null, limits).Scales[channel]
        ?? throw new InvalidOperationException("The test specification has no scale for " + channel);

    /// <summary>
    ///     Maps a number through a model to its text.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    private static string? Text(ScaleModel model, double value) => model.MapText(DataValue.FromNumber(value));

    /// <summary>
    ///     Validates the default scale of a binned quantitative color: the bin-ordinal scale whose domain is the six edges of the five bins, whose range has
    ///     one color per bin (the blues scheme sampled five times), and that gives each number the color of its bin, nothing below the first edge, and the
    ///     first color to a number past the last edge (Vega's wrap of the bin index).
    /// </summary>
    [Fact]
    public void ScaleResolverBin_BinnedColor_DefaultsToBinOrdinalOverTheBinEdges()
    {
        // Act
        var scale = Scale("color", Enc("color", "quantitative"));
        var model = scale.CreateModel(0);

        // Assert: the kind, the bins and the field the scale reads (the start of the bin)
        Assert.Equal(ScaleKind.BinOrdinal, scale.Kind);
        Assert.Equal(BinKind.Binning, scale.Bin);
        Assert.Equal(new BinSpec(0, 100, 20), scale.BinSpec);
        Assert.Equal("bin_maxbins_6_v", scale.FieldPath);
        Assert.Equal("v (binned)", scale.Title);
        Assert.Equal([0.0, 20, 40, 60, 80, 100], scale.Domain!);
        Assert.Equal((0.0, 100.0), (scale.DomainStart, scale.DomainEnd));

        // Assert: five colors, all different, and the color of a bin is that of any number in it
        Assert.Equal(5, scale.ColorRange.Count);
        Assert.Equal(5, scale.ColorRange.Distinct().Count());
        for (var bin = 0; bin < 5; bin++)
        {
            Assert.Equal(scale.ColorRange[bin], Text(model, 20 * bin));
            Assert.Equal(scale.ColorRange[bin], Text(model, (20 * bin) + 19.5));
        }

        // Assert: below the first edge there is no color; at the last edge the index wraps to the first color, as in Vega's scaleBinOrdinal
        Assert.Null(Text(model, -0.5));
        Assert.Null(model.MapText(DataValue.FromString("text")));
        Assert.Equal(scale.ColorRange[0], Text(model, 100));
    }

    /// <summary>
    ///     Validates the kind of scale a binned field gets for every channel, field type and explicit scale type: <c>bin-ordinal</c> is the default of a quantitative
    ///     color, fill and stroke, the other channels keep their usual scale (linear for the numeric ones, ordinal for shape and dash) and read the start of
    ///     the bin, a nominal or ordinal field reads the text of the bin, and the explicit types are the ones of Vega-Lite (an explicit <c>bin-ordinal</c> on a
    ///     quantitative binned color is the default; anywhere else it is replaced by the default).
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="type">The field type.</param>
    /// <param name="scaleType">The explicit scale type, or empty.</param>
    /// <param name="expected">The expected kind.</param>
    [Theory]
    [InlineData("color", "quantitative", "", ScaleKind.BinOrdinal)]
    [InlineData("fill", "quantitative", "", ScaleKind.BinOrdinal)]
    [InlineData("stroke", "quantitative", "", ScaleKind.BinOrdinal)]
    [InlineData("color", "quantitative", "bin-ordinal", ScaleKind.BinOrdinal)]
    [InlineData("color", "quantitative", "ordinal", ScaleKind.BinOrdinal)]
    [InlineData("color", "quantitative", "linear", ScaleKind.Sequential)]
    [InlineData("color", "quantitative", "sequential", ScaleKind.Sequential)]
    [InlineData("color", "quantitative", "quantize", ScaleKind.Quantize)]
    [InlineData("color", "quantitative", "threshold", ScaleKind.Threshold)]
    [InlineData("color", "ordinal", "", ScaleKind.Ordinal)]
    [InlineData("color", "nominal", "bin-ordinal", ScaleKind.Ordinal)]
    [InlineData("size", "quantitative", "", ScaleKind.Linear)]
    [InlineData("size", "quantitative", "bin-ordinal", ScaleKind.Linear)]
    [InlineData("size", "quantitative", "quantize", ScaleKind.Quantize)]
    [InlineData("opacity", "quantitative", "", ScaleKind.Linear)]
    [InlineData("fillOpacity", "quantitative", "", ScaleKind.Linear)]
    [InlineData("strokeOpacity", "quantitative", "", ScaleKind.Linear)]
    [InlineData("strokeWidth", "quantitative", "", ScaleKind.Linear)]
    [InlineData("angle", "quantitative", "", ScaleKind.Linear)]
    [InlineData("shape", "quantitative", "", ScaleKind.Ordinal)]
    [InlineData("shape", "quantitative", "bin-ordinal", ScaleKind.Ordinal)]
    [InlineData("strokeDash", "quantitative", "", ScaleKind.Ordinal)]
    [InlineData("size", "ordinal", "", ScaleKind.Point)]
    public void ScaleResolverBin_Kind_FollowsTheVegaLiteTypeRulesForBinnedFields(string channel, string type, string scaleType, object expected)
    {
        // Arrange
        var scale = scaleType.Length > 0 ? "{\"type\":\"" + scaleType + "\"}" : string.Empty;
        var maxBins = channel switch
        {
            "strokeDash" => "4",
            "angle" => "10",
            _ => "6",
        };

        // Act
        var resolved = Scale(channel, Enc(channel, type, scale));

        // Assert: the default number of bins of the channel names the field; a nominal or ordinal field reads the text of the bin
        Assert.Equal((ScaleKind)expected, resolved.Kind);
        Assert.Equal(BinKind.Binning, resolved.Bin);
        Assert.Equal("bin_maxbins_" + maxBins + "_v" + (type == "quantitative" ? string.Empty : "_range"), resolved.FieldPath);
    }

    /// <summary>
    ///     Validates the continuous scales of a binned field on a numeric channel and with an explicit continuous type on a color: the domain runs from the
    ///     start of the first bin to the stop of the last (the bins of 132 to 176 with <c>maxbins</c> 6 are 130 to 180 by 10), without a zero or a nice, and the
    ///     size scale starts at zero as an unbinned size does.
    /// </summary>
    [Fact]
    public void ScaleResolverBin_ContinuousScales_SpanTheBins()
    {
        // Arrange: five values between 132 and 176; the span 44 asks for ceil(44 / 6) = 8, which the step 10 covers in five bins
        const string Values = """
            "data": { "values": [ { "v": 132 }, { "v": 148 }, { "v": 155 }, { "v": 170 }, { "v": 176 } ] }
            """;

        // Act
        var opacity = Scale("opacity", Enc("opacity", "quantitative"), data: Values);
        var size = Scale("size", Enc("size", "quantitative"), data: Values);
        var linear = Scale("color", Enc("color", "quantitative", """{"type":"linear"}"""), data: Values);
        var binOrdinal = Scale("color", Enc("color", "quantitative"), data: Values);

        // Assert: the bins are 130 to 180; opacity has no zero, size has one
        Assert.Equal(ScaleKind.Linear, opacity.Kind);
        Assert.Equal((130.0, 180.0), (opacity.DomainStart, opacity.DomainEnd));
        Assert.Null(opacity.NiceCount);
        Assert.Equal(new BinSpec(130, 180, 10), opacity.BinSpec);
        Assert.Equal((0.0, 180.0), (size.DomainStart, size.DomainEnd));
        Assert.Equal((130.0, 180.0), (linear.DomainStart, linear.DomainEnd));
        Assert.Equal(ScaleKind.Sequential, linear.Kind);

        // Assert: a value maps by its bin start (the field the scale reads) through the linear scale: 150 is the middle of the opacity range 0.3 to 0.8
        var model = opacity.CreateModel(0);
        Assert.Equal(0.3 + (0.5 * (150 - 130) / 50), model.Map(150), Precision);
        Assert.Equal([130.0, 140, 150, 160, 170, 180], binOrdinal.Domain!);
        Assert.Equal(5, binOrdinal.ColorRange.Count);
    }

    /// <summary>
    ///     Validates the bin-ordinal scale of an explicit color range and reversal: the colors are the ones given, one per bin and cycling when there are fewer,
    ///     and <c>reverse</c> turns them around.
    /// </summary>
    [Fact]
    public void ScaleResolverBin_BinOrdinal_UsesTheRangeAndTheReversalOfTheSpecification()
    {
        // Act
        var listed = Scale("color", Enc("color", "quantitative", """{"range":["#ff0000","#00ff00","#0000ff"]}""")).CreateModel(0);
        var reversed = Scale("color", Enc("color", "quantitative", """{"range":["#ff0000","#00ff00","#0000ff"],"reverse":true}""")).CreateModel(0);
        var scheme = Scale("color", Enc("color", "quantitative", """{"scheme":{"name":"greens","count":3}}"""));

        // Assert: five bins over three colors cycle (the bin index modulo the range, as in Vega)
        Assert.Equal(["#ff0000", "#00ff00", "#0000ff", "#ff0000", "#00ff00"], new[] { 5.0, 25, 45, 65, 85 }.Select(v => Text(listed, v)));
        Assert.Equal(["#0000ff", "#00ff00", "#ff0000", "#0000ff", "#00ff00"], new[] { 5.0, 25, 45, 65, 85 }.Select(v => Text(reversed, v)));

        // Assert: a scheme count is not used by a bin-ordinal scale, which has one color per bin (Vega's n - 1)
        Assert.Equal(5, scheme.ColorRange.Count);
    }

    /// <summary>
    ///     Validates the text of a nominal or ordinal binned color (the discrete form): the domain holds the texts <c>0 – 20</c>, <c>20 – 40</c> and so on in
    ///     the order of the bins, and an explicit bin-ordinal type is replaced by the ordinal scale.
    /// </summary>
    [Fact]
    public void ScaleResolverBin_DiscreteBinnedColor_ReadsTheTextOfTheBins()
    {
        // Act
        var ordinal = Scale("color", Enc("color", "ordinal"));
        var forced = Scale("color", Enc("color", "nominal", """{"type":"bin-ordinal"}"""));

        // Assert: the texts in bin order, and the same scale for the explicit type
        Assert.Equal(ScaleKind.Ordinal, ordinal.Kind);
        Assert.Equal(
            ["0 \u2013 20", "20 \u2013 40", "40 \u2013 60", "60 \u2013 80", "80 \u2013 100"],
            ordinal.DiscreteDomain.Select(d => d.Text));
        Assert.Equal(ScaleKind.Ordinal, forced.Kind);
        Assert.Equal(5, forced.DiscreteDomain.Count);
    }

    /// <summary>
    ///     Validates that a field without a finite value has no bins: the scale has no edge and no color, a value maps to nothing, and nothing is thrown.
    /// </summary>
    [Fact]
    public void ScaleResolverBin_FieldWithoutNumbers_HasAnEmptyBinOrdinalScale()
    {
        // Arrange: the field is text and null
        const string Values = """
            "data": { "values": [ { "v": "x" }, { "v": null }, { "w": 1 } ] }
            """;

        // Act
        var scale = Scale("color", Enc("color", "quantitative"), data: Values);
        var model = scale.CreateModel(0);

        // Assert
        Assert.Equal(ScaleKind.BinOrdinal, scale.Kind);
        Assert.Null(scale.BinSpec);
        Assert.Empty(scale.Domain!);
        Assert.Empty(scale.ColorRange);
        Assert.Null(Text(model, 5));
    }

    /// <summary>
    ///     Validates the limit on the bins: <c>maxbins</c> 6 passes a limit of six and fails a limit of five, and an exact step that makes five bins passes a limit of
    ///     five and fails a limit of four, each time with a located error that names the limit at the <c>bin</c> of the channel, before any scale is built.
    /// </summary>
    [Fact]
    public void ScaleResolverBin_Limits_AreEnforcedOnTheBinsOfAChannel()
    {
        // Act
        var fine = Scale("color", Enc("color", "quantitative"), limits: new VegaLiteLimits { MaxScaleDomainSize = 6 });
        var tooMany = Assert.Throws<VegaLiteRenderException>(() => Scale("color", Enc("color", "quantitative"), limits: new VegaLiteLimits { MaxScaleDomainSize = 5 }));
        var tooManyForSize = Assert.Throws<VegaLiteRenderException>(() => Scale("size", Enc("size", "quantitative"), limits: new VegaLiteLimits { MaxScaleDomainSize = 3 }));
        var stepped = Scale("color", Enc("color", "quantitative", bin: """{"step":20}"""), limits: new VegaLiteLimits { MaxScaleDomainSize = 5 });
        var tooManySteps = Assert.Throws<VegaLiteRenderException>(() => Scale("color", Enc("color", "quantitative", bin: """{"step":20}"""), limits: new VegaLiteLimits { MaxScaleDomainSize = 4 }));

        // Assert
        Assert.Equal(6, fine.Domain!.Count);
        Assert.Equal(6, stepped.Domain!.Count);
        Assert.Equal("/encoding/color/bin", tooMany.Location.Pointer);
        Assert.Contains("MaxScaleDomainSize", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/size/bin", tooManyForSize.Location.Pointer);
        Assert.Equal("/encoding/color/bin", tooManySteps.Location.Pointer);
        Assert.Contains("5 bins", tooManySteps.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the bins of a channel are charged to the evaluation budget (the rows are read for the extent and for the assignment) and that
    ///     a failure names the budget.
    /// </summary>
    [Fact]
    public void ScaleResolverBin_Work_IsChargedToTheEvaluationBudget()
    {
        // Arrange: 5000 rows read by the bin of the color channel
        const string Source = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":5000,\"as\":\"id\"}},\"mark\":\"point\",\"encoding\":{\"color\":{\"field\":\"id\",\"type\":\"quantitative\",\"bin\":true}}}";

        // Act
        var parts = ChartTestHelpers.ResolveFull(Source);
        var charged = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.ResolveFull(Source, new VegaLiteLimits { MaxEvaluationSteps = 40 }));

        // Assert: 5000 rows over the extent 0 to 4999 with maxbins 6 give the step 1000 (the edges 0 to 5000); the failure names the budget
        var scale = parts.Scales["color"]!;
        Assert.Equal(ScaleKind.BinOrdinal, scale.Kind);
        Assert.Equal([0.0, 1000, 2000, 3000, 4000, 5000], scale.Domain!);
        Assert.Contains("MaxEvaluationSteps", charged.Message, StringComparison.Ordinal);
    }
}

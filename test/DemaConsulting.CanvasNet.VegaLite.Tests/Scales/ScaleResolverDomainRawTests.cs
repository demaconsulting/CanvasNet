using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the raw domain of a scale (<c>scale.domainRaw</c> and <c>scale.domain</c> of the form <c>{"param": ...}</c>): Vega's <c>configureDomain</c> takes it as the domain and nothing
///     adjusts it afterwards, and Vega-Lite's <c>scaleClip</c> clips the marks of a view whose x or y scale has a domain that a parameter gives.
/// </summary>
public class ScaleResolverDomainRawTests
{
    /// <summary>
    ///     Builds a view of three rows (x: 1, 4 and 7) whose x scale is given.
    /// </summary>
    /// <param name="parameters">The <c>params</c> array (written out), or an empty text.</param>
    /// <param name="scale">The <c>scale</c> object of the x channel (written out).</param>
    /// <param name="type">The type of the x field.</param>
    /// <returns>The specification text.</returns>
    private static string View(string parameters, string scale, string type = "quantitative") =>
        "{ " + (parameters.Length == 0 ? string.Empty : "\"params\": " + parameters + ", ")
        + "\"data\": { \"values\": [ { \"a\": 1, \"k\": \"b\" }, { \"a\": 4, \"k\": \"a\" }, { \"a\": 7, \"k\": \"c\" } ] }, \"mark\": \"point\", "
        + "\"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"" + type + "\", \"scale\": " + scale + " } } }";

    /// <summary>
    ///     Validates that the value of a variable parameter is the domain as it is: no zero rule, <c>nice</c>, pixel <c>padding</c>, <c>domainMin</c> or <c>domainMax</c>, no sharing of the
    ///     domain of the data, and the marks are clipped.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_AParameterDomainIsTheDomainAsItIs()
    {
        // Arrange: the data runs from 1 to 7; the raw domain is [2, 6.5] and the scale asks for everything that would change it
        var json = View("""[ { "name": "xr", "value": [2, 6.5] } ]""", """{ "domain": { "param": "xr" }, "zero": true, "nice": true, "padding": 25, "domainMin": 0, "domainMax": 10 }""");

        // Act
        var scale = ChartTestHelpers.Parts(json).Scales["x"]!;

        // Assert
        Assert.Equal((2.0, 6.5), (scale.DomainStart, scale.DomainEnd));
        Assert.Null(scale.Domain);
        Assert.True(scale.HasRawDomain);
        Assert.True(scale.ClipMarks);
        Assert.False(scale.HasExplicitDomain);
        Assert.Null(scale.NiceCount);
        Assert.Equal(0, scale.PaddingPixels);
    }

    /// <summary>
    ///     Validates that a raw domain of more than two numbers is a piecewise domain, as an explicit domain of the same numbers is.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_AParameterDomainOfThreeNumbersIsPiecewise()
    {
        // Arrange / Act
        var scale = ChartTestHelpers.Parts(View("""[ { "name": "xr", "value": [0, 2, 10] } ]""", """{ "domain": { "param": "xr" } }""")).Scales["x"]!;

        // Assert
        Assert.Equal([0.0, 2.0, 10.0], scale.Domain);
        Assert.Equal((0.0, 10.0), (scale.DomainStart, scale.DomainEnd));
    }

    /// <summary>
    ///     Validates that a parameter without a value, and a parameter that is not a variable parameter, leave the domain of the scale as it is, while the marks are still clipped (the scale has
    ///     the extent of a parameter whether or not the parameter gives a value).
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_AParameterWithoutAValueKeepsTheDomainOfTheData()
    {
        // Arrange: the same chart with no domain, with a parameter that has no value, with a selection parameter, and with an undeclared name is not possible (a reference is not checked)
        var plain = ChartTestHelpers.Parts(View(string.Empty, "{}")).Scales["x"]!;
        var none = ChartTestHelpers.Parts(View("""[ { "name": "xr" } ]""", """{ "domain": { "param": "xr" } }""")).Scales["x"]!;
        var selection = ChartTestHelpers.Parts(View("""[ { "name": "xr", "select": { "type": "interval" } } ]""", """{ "domain": { "param": "xr" } }""")).Scales["x"]!;

        // Assert
        foreach (var scale in new[] { none, selection })
        {
            Assert.Equal((plain.DomainStart, plain.DomainEnd, plain.NiceCount, plain.PaddingPixels), (scale.DomainStart, scale.DomainEnd, scale.NiceCount, scale.PaddingPixels));
            Assert.False(scale.HasRawDomain);
            Assert.True(scale.ClipMarks);
        }

        Assert.False(plain.ClipMarks);
    }

    /// <summary>
    ///     Validates that the <c>domainRaw</c> that the specification writes wins over the domain a parameter gives (Vega-Lite assembles the scale properties after the selection extent), and does
    ///     not clip by itself.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_DomainRawWinsOverTheDomainOfAParameter()
    {
        // Arrange / Act
        var only = ChartTestHelpers.Parts(View(string.Empty, """{ "domainRaw": [3, 5] }""")).Scales["x"]!;
        var both = ChartTestHelpers.Parts(View("""[ { "name": "xr", "value": [2, 6] } ]""", """{ "domainRaw": [3, 5], "domain": { "param": "xr" } }""")).Scales["x"]!;

        // Assert
        Assert.Equal((3.0, 5.0, true, false), (only.DomainStart, only.DomainEnd, only.HasRawDomain, only.ClipMarks));
        Assert.Equal((3.0, 5.0, true, true), (both.DomainStart, both.DomainEnd, both.HasRawDomain, both.ClipMarks));
    }

    /// <summary>
    ///     Validates the raw domain of a discrete scale: the categories are the entries in order, each once, and an entry that no row has is still a category (the band is reserved).
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_AParameterDomainOfADiscreteScaleIsTheListOfCategories()
    {
        // Arrange: the data has the categories b, a and c; the domain lists c, z, a and c again
        var json = View("""[ { "name": "order", "value": ["c", "z", "a", "c"] } ]""", """{ "domain": { "param": "order" } }""", "nominal");

        // Act
        var scale = ChartTestHelpers.Parts(json).Scales["x"]!;

        // Assert
        Assert.Equal(["c", "z", "a"], scale.DiscreteDomain.Select(v => v.Text));
        Assert.True(scale.HasRawDomain);
        Assert.True(scale.ClipMarks);
        Assert.Equal(DomainOrder.Custom, scale.DomainOrder);
    }

    /// <summary>
    ///     Validates that a log scale refuses a raw domain that crosses zero with a located render error (Vega would draw nothing), at the channel.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_ALogScaleRefusesARawDomainThatCrossesZero()
    {
        // Arrange
        var json = View("""[ { "name": "xr", "value": [-1, 10] } ]""", """{ "type": "log", "domain": { "param": "xr" } }""");

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => ChartTestHelpers.Parts(json));

        // Assert
        Assert.Equal("/encoding/x", exception.Location.Pointer);
        Assert.Contains("log scale", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a discretizing scale keeps its own domain whatever a raw domain says (its domain is the data's, or its own), though the marks are still clipped.
    /// </summary>
    [Fact]
    public void ScaleResolver_Resolve_ADiscretizingScaleKeepsItsOwnDomain()
    {
        // Arrange: a quantize color scale (a position channel cannot have one)
        static string Colors(string parameters, string scale) =>
            "{ " + parameters + "\"data\": { \"values\": [ { \"a\": 1 }, { \"a\": 4 }, { \"a\": 7 } ] }, \"mark\": \"point\", "
            + "\"encoding\": { \"color\": { \"field\": \"a\", \"type\": \"quantitative\", \"scale\": " + scale + " } } }";
        var plain = ChartTestHelpers.Parts(Colors(string.Empty, """{ "type": "quantize" }""")).Scales["color"]!;
        const string declared = "\"params\": [ { \"name\": \"xr\", \"value\": [2, 6] } ], ";
        var raw = ChartTestHelpers.Parts(Colors(declared, """{ "type": "quantize", "domain": { "param": "xr" } }""")).Scales["color"]!;

        // Assert
        Assert.Equal((plain.DomainStart, plain.DomainEnd), (raw.DomainStart, raw.DomainEnd));
        Assert.False(raw.HasRawDomain);
        Assert.True(raw.ClipMarks);
    }
}

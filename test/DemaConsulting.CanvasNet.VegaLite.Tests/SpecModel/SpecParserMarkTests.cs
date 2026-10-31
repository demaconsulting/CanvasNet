using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests for the validation of the path mark properties of a mark definition in <see cref="SpecParser"/> (<c>interpolate</c>, <c>tension</c>, <c>point</c>,
///     <c>line</c>, <c>invalid</c>, <c>order</c>, the stroke and the polar properties): located errors for a wrong value and the checks of the overlay objects.
/// </summary>
public class SpecParserMarkTests
{
    /// <summary>The 15 interpolation names of Vega-Lite 6.</summary>
    private static readonly string[] InterpolateNames =
    [
        "basis", "basis-open", "basis-closed", "bundle", "cardinal", "cardinal-open", "cardinal-closed", "catmull-rom",
        "linear", "linear-closed", "monotone", "natural", "step", "step-before", "step-after",
    ];

    /// <summary>
    ///     Builds a specification with a mark definition that has one extra property.
    /// </summary>
    /// <param name="type">The mark type.</param>
    /// <param name="property">The text of the property: <c>"name": value</c>.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string type, string property) =>
        "{\"mark\":{\"type\":\"" + type + "\"," + property + "},\"encoding\":{\"x\":{\"field\":\"t\",\"type\":\"quantitative\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"}}}";

    /// <summary>
    ///     Validates that each of the 15 interpolation names of Vega-Lite is accepted, also as an expression reference, and that the other path properties take their kinds.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_AcceptsTheFifteenInterpolateNames()
    {
        // Arrange / Act / Assert: every name parses without a warning
        Assert.Equal(15, InterpolateNames.Length);
        foreach (var name in InterpolateNames)
        {
            var tree = SpecParser.Parse(Spec("line", "\"interpolate\":\"" + name + "\""));
            Assert.Empty(tree.Warnings);
        }

        // Assert: the other properties of the path marks and of the polar marks are accepted in their kinds, null switches off what can be switched off
        string[] valid =
        [
            "\"interpolate\":{\"expr\":\"'linear'\"}", "\"tension\":0.5", "\"tension\":{\"expr\":\"1\"}", "\"point\":true", "\"point\":false", "\"point\":null",
            "\"point\":\"transparent\"", "\"point\":{\"color\":\"red\",\"size\":10}", "\"line\":true", "\"line\":null", "\"line\":{\"strokeWidth\":3}",
            "\"invalid\":\"filter\"", "\"invalid\":\"break-paths-filter-domains\"", "\"invalid\":\"break-paths-show-domains\"",
            "\"invalid\":\"break-paths-show-path-domains\"", "\"invalid\":\"show\"", "\"invalid\":null", "\"order\":true", "\"order\":false", "\"order\":null",
            "\"strokeDashOffset\":2", "\"strokeMiterLimit\":4", "\"padAngle\":0.1", "\"innerRadius\":10", "\"outerRadius\":50", "\"radius\":40", "\"radius2\":5",
            "\"theta\":1", "\"theta2\":2",
        ];
        foreach (var property in valid)
        {
            Assert.Empty(SpecParser.Parse(Spec("area", property)).Warnings);
        }
    }

    /// <summary>
    ///     Validates that a wrong <c>interpolate</c>, <c>invalid</c>, <c>point</c> or numeric property is a located error at the value (the Vega-only
    ///     <c>catmull-rom-open</c> is not an interpolation of Vega-Lite).
    /// </summary>
    /// <param name="property">The text of the property.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("\"interpolate\":\"catmull-rom-open\"", "/mark/interpolate")]
    [InlineData("\"interpolate\":\"catmull-rom-closed\"", "/mark/interpolate")]
    [InlineData("\"interpolate\":\"Linear\"", "/mark/interpolate")]
    [InlineData("\"interpolate\":3", "/mark/interpolate")]
    [InlineData("\"interpolate\":null", "/mark/interpolate")]
    [InlineData("\"tension\":\"x\"", "/mark/tension")]
    [InlineData("\"tension\":true", "/mark/tension")]
    [InlineData("\"invalid\":\"hide\"", "/mark/invalid")]
    [InlineData("\"invalid\":3", "/mark/invalid")]
    [InlineData("\"invalid\":true", "/mark/invalid")]
    [InlineData("\"point\":\"opaque\"", "/mark/point")]
    [InlineData("\"point\":3", "/mark/point")]
    [InlineData("\"point\":[1]", "/mark/point")]
    [InlineData("\"line\":\"yes\"", "/mark/line")]
    [InlineData("\"line\":1", "/mark/line")]
    [InlineData("\"order\":\"o\"", "/mark/order")]
    [InlineData("\"order\":1", "/mark/order")]
    [InlineData("\"strokeDashOffset\":\"1\"", "/mark/strokeDashOffset")]
    [InlineData("\"strokeMiterLimit\":\"a\"", "/mark/strokeMiterLimit")]
    [InlineData("\"padAngle\":true", "/mark/padAngle")]
    [InlineData("\"innerRadius\":\"x\"", "/mark/innerRadius")]
    [InlineData("\"outerRadius\":[1]", "/mark/outerRadius")]
    [InlineData("\"radius\":\"x\"", "/mark/radius")]
    [InlineData("\"radius2\":\"x\"", "/mark/radius2")]
    [InlineData("\"theta\":\"x\"", "/mark/theta")]
    [InlineData("\"theta2\":{}", "/mark/theta2")]
    public void SpecParser_Parse_RejectsABadInterpolateOrInvalidOrPointValue(string property, string pointer)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec("line", property), "t.json"));

        // Assert
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Equal("t.json", ex.Location.File);
        Assert.True(ex.Location.Line >= 1 && ex.Location.Column >= 1);
    }

    /// <summary>
    ///     Validates that the objects of the <c>point</c> and <c>line</c> overlays are checked like mark definitions: a wrong value is a located error, an unknown
    ///     property is warned about at its key, and the expression references inside them are reported by the feature catalog as not evaluated.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ValidatesOverlayObjectsLikeMarkDefinitions()
    {
        // Assert: a wrong kind inside an overlay is an error at the overlay's property
        var color = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec("line", "\"point\":{\"color\":3}"), "t.json"));
        Assert.Equal("/mark/point/color", color.Location.Pointer);
        var interpolate = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec("area", "\"line\":{\"interpolate\":\"nope\"}"), "t.json"));
        Assert.Equal("/mark/line/interpolate", interpolate.Location.Pointer);
        var invalid = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec("area", "\"point\":{\"invalid\":\"hide\"}"), "t.json"));
        Assert.Equal("/mark/point/invalid", invalid.Location.Pointer);

        // Assert: a property that no mark has is a warning at its key, not an error
        var tree = SpecParser.Parse(Spec("line", "\"point\":{\"filled\":false,\"size\":50,\"foo\":1}"), "t.json");
        var warning = Assert.Single(tree.Warnings);
        Assert.Equal(("vl-unknown-property", "/mark/point/foo"), (warning.Id, warning.Location.Pointer));
        Assert.Empty(SpecParser.Parse(Spec("area", "\"line\":{\"strokeWidth\":3,\"interpolate\":\"step\"},\"point\":{\"filled\":false,\"size\":50}")).Warnings);

        // Assert: an expression reference inside an overlay object is not evaluated, and the catalog reports it where it is
        var reports = FeatureCatalog.Visit(SpecParser.Parse(Spec("line", "\"point\":{\"color\":{\"expr\":\"'red'\"}}"), "t.json"));
        var report = Assert.Single(reports);
        Assert.Equal(("vl-expr-ref", "/mark/point/color"), (report.Id, report.Location.Pointer));
    }
}

using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Tests of what <see cref="FeatureCatalog"/> says about the <c>image</c> mark (Vega-Lite Phase 13): the mark is reported as <c>vl-mark-image</c> only when no image resolver was supplied
///     (the library never reads a picture on its own), the <c>url</c> channel and the <c>x2</c> and <c>y2</c> channels are drawn by it and reported for the marks that ignore them,
///     and the properties that every drawn mark has (<c>invalid</c>, a condition) are checked for it too.
/// </summary>
public class FeatureCatalogImageTests
{
    /// <summary>
    ///     Visits a specification text.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="hasResolver">Whether the caller supplied an image resolver.</param>
    /// <returns>The identifier and the pointer of each report.</returns>
    private static List<(string Id, string Pointer)> Visit(string json, bool hasResolver = false) =>
        FeatureCatalog.Visit(SpecParser.Parse(json), null, hasResolver).Select(r => (r.Id, r.Location.Pointer)).ToList();

    /// <summary>
    ///     Test that an image mark is reported as <c>vl-mark-image</c> at the mark without a resolver, in every place a mark can be, and not with a resolver.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ImageMark_IsReportedOnlyWithoutAResolver()
    {
        // Arrange: a mark written as a string, as an object, in a layer and in a concatenation
        const string json = """
            {
              "mark": "image",
              "layer": [ { "mark": { "type": "image" } } ],
              "hconcat": [ { "mark": "image" } ]
            }
            """;

        // Act
        var without = Visit(json);
        var with = Visit(json, hasResolver: true);

        // Assert
        Assert.Equal(
            [("vl-mark-image", "/mark"), ("vl-mark-image", "/layer/0/mark"), ("vl-mark-image", "/hconcat/0/mark")],
            without.Where(r => r.Id == "vl-mark-image"));
        Assert.DoesNotContain(with, r => r.Id == "vl-mark-image");
    }

    /// <summary>
    ///     Test that the <c>url</c> channel is drawn by an image mark and reported by any other mark, as Vega-Lite ignores it there.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_UrlChannel_IsSupportedOnlyOnImage()
    {
        // Act
        var image = Visit("""{ "mark": "image", "encoding": { "url": { "field": "u", "type": "nominal" } } }""", hasResolver: true);
        var bar = Visit("""{ "mark": "bar", "encoding": { "url": { "field": "u", "type": "nominal" } } }""");

        // Assert
        Assert.Empty(image);
        Assert.Equal([("vl-channel-url", "/encoding/url")], bar);
    }

    /// <summary>
    ///     Test that an image mark may have a second end of its position, which a point mark may not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_SecondEndChannel_IsSupportedOnImage()
    {
        // Arrange
        const string encoding = """
            "encoding": { "x": { "field": "a", "type": "quantitative" }, "x2": { "field": "b" }, "y": { "field": "c", "type": "quantitative" }, "y2": { "field": "d" } }
            """;

        // Act
        var image = Visit("{ \"mark\": \"image\", " + encoding + " }", hasResolver: true);
        var point = Visit("{ \"mark\": \"point\", " + encoding + " }");

        // Assert
        Assert.Empty(image);
        Assert.Equal(["vl-channel-x2", "vl-channel-y2"], point.Select(r => r.Id));
    }

    /// <summary>
    ///     Test that an image mark is checked like any drawn mark: the warning of the invalid mode that filters the domains and a condition on the link are reported.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ImageMark_IsCheckedLikeADrawnMark()
    {
        // Act
        var invalid = Visit("""{ "mark": { "type": "image", "invalid": "filter" } }""", hasResolver: true);
        var condition = Visit(
            """{ "mark": "image", "encoding": { "url": { "condition": { "test": "datum.a > 1", "value": "a.png" }, "value": "b.png" } } }""",
            hasResolver: true);

        // Assert
        Assert.Equal([("vl-mark-invalid-domains", "/mark/invalid")], invalid);
        Assert.Equal(["vl-encoding-condition"], condition.Select(r => r.Id));
    }

    /// <summary>
    ///     Test that a layered view reports the image of a layer once for the layer that has it, so a resolver removes all of them.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ImageLayers_AreAllSilencedByAResolver()
    {
        // Arrange
        const string json = """{ "layer": [ { "mark": "image" }, { "mark": "bar" }, { "mark": "image" } ] }""";

        // Act
        var without = Visit(json);
        var with = Visit(json, hasResolver: true);

        // Assert
        Assert.Equal([("vl-mark-image", "/layer/0/mark"), ("vl-mark-image", "/layer/2/mark")], without);
        Assert.Empty(with);
    }
}

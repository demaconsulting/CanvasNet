using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the rules that layers and composite marks add to <see cref="SpecParser"/>: the validation of <c>resolve</c> and of the properties of
///     <c>boxplot</c>, <c>errorbar</c> and <c>errorband</c>, and the compilation of the expressions of the views of a composition.
/// </summary>
public class SpecParserResolveTests
{
    /// <summary>
    ///     Validates that a well-formed <c>resolve</c> is accepted without a warning, for every kind and for channels of each kind.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ValidResolveIsAccepted()
    {
        // Arrange / Act
        var tree = SpecParser.Parse(
            """
            { "resolve": { "scale": { "x": "shared", "y": "independent", "color": "independent", "theta": "shared" },
                           "axis": { "x": "independent", "y": "shared" },
                           "legend": { "color": "independent", "shape": "shared" } },
              "layer": [ { "mark": "bar" } ] }
            """);

        // Assert
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Validates that a <c>resolve</c> that is not an object, a kind that is not an object, a channel the kind does not accept and a mode that is neither
    ///     <c>shared</c> nor <c>independent</c> are located specification errors.
    /// </summary>
    [Theory]
    [InlineData("""{ "resolve": 3 }""", "/resolve")]
    [InlineData("""{ "resolve": { "scale": "x" } }""", "/resolve/scale")]
    [InlineData("""{ "resolve": { "scale": { "bogus": "shared" } } }""", "/resolve/scale/bogus")]
    [InlineData("""{ "resolve": { "axis": { "color": "shared" } } }""", "/resolve/axis/color")]
    [InlineData("""{ "resolve": { "legend": { "x": "shared" } } }""", "/resolve/legend/x")]
    [InlineData("""{ "resolve": { "scale": { "x": "both" } } }""", "/resolve/scale/x")]
    [InlineData("""{ "resolve": { "scale": { "x": true } } }""", "/resolve/scale/x")]
    [InlineData("""{ "layer": [ { "resolve": { "scale": { "y": null } }, "mark": "bar" } ] }""", "/layer/0/resolve/scale/y")]
    public void SpecParser_Parse_MalformedResolveReportsThePointer(string json, string pointer)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json, "r.json"));

        // Assert
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Equal("r.json", ex.Location.File);
        Assert.True(ex.Location.Line >= 1 && ex.Location.Column >= 1);
    }

    /// <summary>
    ///     Validates that an unknown kind in <c>resolve</c> is a warning, not an error.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_UnknownResolveKindIsAWarning()
    {
        // Arrange / Act
        var tree = SpecParser.Parse("""{ "resolve": { "title": { "x": "shared" } }, "mark": "bar" }""");

        // Assert
        var warning = Assert.Single(tree.Warnings);
        Assert.Equal("vl-unknown-property", warning.Id);
        Assert.Equal("/resolve/title", warning.Location.Pointer);
    }

    /// <summary>
    ///     Validates the channels each kind of <c>resolve</c> accepts.
    /// </summary>
    [Theory]
    [InlineData("scale", "x", true)]
    [InlineData("scale", "xOffset", true)]
    [InlineData("scale", "radius", true)]
    [InlineData("scale", "x2", false)]
    [InlineData("axis", "y", true)]
    [InlineData("axis", "size", false)]
    [InlineData("legend", "strokeDash", true)]
    [InlineData("legend", "y", false)]
    [InlineData("title", "x", false)]
    public void SpecParser_IsResolveChannel_FollowsTheVegaLiteMaps(string kind, string channel, bool expected) =>
        Assert.Equal(expected, SpecParser.IsResolveChannel(kind, channel));

    /// <summary>
    ///     Validates that the parts of a composite mark may be a boolean or an object, the extent follows the mark, and the center is mean or median.
    /// </summary>
    [Theory]
    [InlineData("""{ "type": "boxplot", "extent": 2, "box": true, "median": { "color": "red" }, "rule": false, "ticks": {}, "outliers": false }""")]
    [InlineData("""{ "type": "boxplot", "extent": "min-max" }""")]
    [InlineData("""{ "type": "errorbar", "extent": "stdev", "center": "median", "rule": true, "ticks": { "size": 3 } }""")]
    [InlineData("""{ "type": "errorband", "extent": "ci", "band": { "opacity": 0.5 }, "borders": true, "interpolate": "monotone" }""")]
    public void SpecParser_Parse_ValidCompositeMarkIsAccepted(string mark)
    {
        // Arrange / Act
        var tree = SpecParser.Parse($$"""{ "mark": {{mark}} }""");

        // Assert
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Validates that a composite mark property of the wrong type or an extent the mark does not know is a located specification error.
    /// </summary>
    [Theory]
    [InlineData("""{ "type": "boxplot", "extent": "ci" }""", "/mark/extent")]
    [InlineData("""{ "type": "boxplot", "extent": -1 }""", "/mark/extent")]
    [InlineData("""{ "type": "boxplot", "extent": true }""", "/mark/extent")]
    [InlineData("""{ "type": "boxplot", "box": "yes" }""", "/mark/box")]
    [InlineData("""{ "type": "boxplot", "median": 3 }""", "/mark/median")]
    [InlineData("""{ "type": "boxplot", "outliers": [] }""", "/mark/outliers")]
    [InlineData("""{ "type": "errorbar", "extent": "min-max" }""", "/mark/extent")]
    [InlineData("""{ "type": "errorbar", "extent": 2 }""", "/mark/extent")]
    [InlineData("""{ "type": "errorbar", "center": "mode" }""", "/mark/center")]
    [InlineData("""{ "type": "errorbar", "ticks": "all" }""", "/mark/ticks")]
    [InlineData("""{ "type": "errorband", "extent": "range" }""", "/mark/extent")]
    [InlineData("""{ "type": "errorband", "band": 1 }""", "/mark/band")]
    [InlineData("""{ "type": "errorband", "borders": "x" }""", "/mark/borders")]
    public void SpecParser_Parse_MalformedCompositeMarkReportsThePointer(string mark, string pointer)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse($$"""{ "mark": {{mark}} }"""));

        // Assert
        Assert.Equal(pointer, ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the composite part properties are not checked on a primitive mark (they are not properties of it).
    /// </summary>
    [Fact]
    public void SpecParser_Parse_CompositeRulesDoNotApplyToPrimitiveMarks()
    {
        // Arrange / Act
        var tree = SpecParser.Parse("""{ "mark": { "type": "bar", "extent": "anything", "band": 1 } }""");

        // Assert
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Validates that the expressions of the views of a layered view and of a concatenation are compiled alike (the transform of a layer and of the layered view, a condition
    ///     of a layer's encoding and a size condition of the layered view's own encoding, and the condition of a concatenated view), so a view of a composition is validated as any view.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ExpressionsOfComposedViewsAreCompiled()
    {
        // Arrange / Act
        var layered = SpecParser.Parse(
            """
            { "encoding": { "size": { "condition": { "test": { "field": "q", "gt": 1 }, "value": 5 }, "value": 1 } },
              "layer": [ { "mark": "point", "encoding": { "color": { "condition": { "test": { "field": "q", "gt": 1 }, "value": "red" }, "value": "gray" } } } ] }
            """);
        var concat = SpecParser.Parse(
            """
            { "hconcat": [ { "mark": "point", "encoding": { "color": { "condition": { "test": { "field": "q", "gt": 1 }, "value": "red" }, "value": "gray" } } } ] }
            """);

        // Assert
        Assert.NotNull(layered.Expressions.Get(layered.Root["encoding"]!["size"]!["condition"]!["test"]));
        Assert.NotNull(layered.Expressions.Get(layered.Root["layer"]!.Items[0]["encoding"]!["color"]!["condition"]!["test"]));
        Assert.NotNull(concat.Expressions.Get(concat.Root["hconcat"]!.Items[0]["encoding"]!["color"]!["condition"]!["test"]));
    }
}

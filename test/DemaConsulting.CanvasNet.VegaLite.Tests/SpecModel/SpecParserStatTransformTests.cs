using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests for the validation of the statistical transforms (<c>regression</c>, <c>loess</c>, <c>density</c>, <c>quantile</c> and <c>extent</c>) and of the <c>op</c> of a
///     <c>pivot</c>, which are part of <see cref="SpecParser"/>: the shape rules of the Vega-Lite schema (with the bounds the renderer adds), Vega-Lite's rule that an <c>extent</c> next
///     to a regression or a density is an option of that transform, and the visibility of the parameter that an <c>extent</c> transform computes.
/// </summary>
public class SpecParserStatTransformTests
{
    /// <summary>
    ///     Parses a specification that holds one transform and returns the exception the parser throws for it.
    /// </summary>
    /// <param name="entry">The text of the transform object.</param>
    /// <returns>The exception.</returns>
    private static VegaLiteSpecException FailTransform(string entry) =>
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"transform\":[" + entry + "]}", "t.json"));

    /// <summary>
    ///     Validates that a wrongly shaped statistical transform is a located error at the offending value.
    /// </summary>
    /// <param name="entry">The text of the transform object.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("""{"regression":3,"on":"x"}""", "/transform/0/regression")]
    [InlineData("""{"regression":"y"}""", "/transform/0")]
    [InlineData("""{"regression":"y","on":3}""", "/transform/0/on")]
    [InlineData("""{"regression":"y","on":"x","method":"cubic"}""", "/transform/0/method")]
    [InlineData("""{"regression":"y","on":"x","method":3}""", "/transform/0/method")]
    [InlineData("""{"regression":"y","on":"x","order":2.5}""", "/transform/0/order")]
    [InlineData("""{"regression":"y","on":"x","order":-1}""", "/transform/0/order")]
    [InlineData("""{"regression":"y","on":"x","order":65}""", "/transform/0/order")]
    [InlineData("""{"regression":"y","on":"x","order":"3"}""", "/transform/0/order")]
    [InlineData("""{"regression":"y","on":"x","extent":[1]}""", "/transform/0/extent")]
    [InlineData("""{"regression":"y","on":"x","extent":[1,"2"]}""", "/transform/0/extent")]
    [InlineData("""{"regression":"y","on":"x","extent":5}""", "/transform/0/extent")]
    [InlineData("""{"regression":"y","on":"x","params":"yes"}""", "/transform/0/params")]
    [InlineData("""{"regression":"y","on":"x","groupby":"g"}""", "/transform/0/groupby")]
    [InlineData("""{"regression":"y","on":"x","groupby":[3]}""", "/transform/0/groupby/0")]
    [InlineData("""{"regression":"y","on":"x","as":["a","b","c"]}""", "/transform/0/as")]
    [InlineData("""{"regression":"y","on":"x","as":[]}""", "/transform/0/as")]
    [InlineData("""{"regression":"y","on":"x","as":["a",""]}""", "/transform/0/as")]
    [InlineData("""{"loess":"y"}""", "/transform/0")]
    [InlineData("""{"loess":"y","on":"x","bandwidth":1.5}""", "/transform/0/bandwidth")]
    [InlineData("""{"loess":"y","on":"x","bandwidth":-0.1}""", "/transform/0/bandwidth")]
    [InlineData("""{"loess":"y","on":"x","bandwidth":"wide"}""", "/transform/0/bandwidth")]
    [InlineData("""{"density":3}""", "/transform/0/density")]
    [InlineData("""{"density":"v","cumulative":"yes"}""", "/transform/0/cumulative")]
    [InlineData("""{"density":"v","counts":1}""", "/transform/0/counts")]
    [InlineData("""{"density":"v","bandwidth":-1}""", "/transform/0/bandwidth")]
    [InlineData("""{"density":"v","steps":-5}""", "/transform/0/steps")]
    [InlineData("""{"density":"v","minsteps":"many"}""", "/transform/0/minsteps")]
    [InlineData("""{"density":"v","maxsteps":-1}""", "/transform/0/maxsteps")]
    [InlineData("""{"density":"v","extent":[1,2,3]}""", "/transform/0/extent")]
    [InlineData("""{"density":"v","resolve":"both"}""", "/transform/0/resolve")]
    [InlineData("""{"density":"v","as":[1]}""", "/transform/0/as")]
    [InlineData("""{"quantile":3}""", "/transform/0/quantile")]
    [InlineData("""{"quantile":"v","probs":"all"}""", "/transform/0/probs")]
    [InlineData("""{"quantile":"v","probs":[0.5,"x"]}""", "/transform/0/probs")]
    [InlineData("""{"quantile":"v","step":-0.1}""", "/transform/0/step")]
    [InlineData("""{"quantile":"v","step":"fine"}""", "/transform/0/step")]
    [InlineData("""{"quantile":"v","groupby":3}""", "/transform/0/groupby")]
    [InlineData("""{"extent":3,"param":"e"}""", "/transform/0/extent")]
    [InlineData("""{"extent":"v"}""", "/transform/0")]
    [InlineData("""{"extent":"v","param":""}""", "/transform/0/param")]
    [InlineData("""{"extent":"v","param":3}""", "/transform/0/param")]
    [InlineData("""{"pivot":"k","value":"v","op":"mode"}""", "/transform/0/op")]
    [InlineData("""{"pivot":"k","value":"v","op":3}""", "/transform/0/op")]
    [InlineData("""{"pivot":"k","value":"v","op":"Sum"}""", "/transform/0/op")]
    public void SpecParser_Parse_StatisticalTransformShapesAreLocatedErrors(string entry, string pointer)
    {
        // Arrange / Act
        var error = FailTransform(entry);

        // Assert
        Assert.Equal(pointer, error.Location.Pointer);
        Assert.Equal("t.json", error.Location.File);
        Assert.True(error.Location.Line >= 1 && error.Location.Column >= 1);
    }

    /// <summary>
    ///     Validates that the well-formed forms of every statistical transform (every method, every option, every pivot operation) are accepted.
    /// </summary>
    /// <param name="entry">The text of the transform object.</param>
    [Theory]
    [InlineData("""{"regression":"y","on":"x"}""")]
    [InlineData("""{"regression":"y","on":"x","method":"poly","order":0}""")]
    [InlineData("""{"regression":"y","on":"x","method":"poly","order":64}""")]
    [InlineData("""{"regression":"y","on":"x","method":"log","extent":[1,10],"params":true,"groupby":["g"],"as":["a","b"]}""")]
    [InlineData("""{"regression":"y","on":"x","as":["a"]}""")]
    [InlineData("""{"loess":"y","on":"x","bandwidth":0}""")]
    [InlineData("""{"loess":"y","on":"x","groupby":[]}""")]
    [InlineData("""{"loess":"y","on":"x","bandwidth":1,"groupby":["g"],"as":["a","b"]}""")]
    [InlineData("""{"density":"v"}""")]
    [InlineData("""{"density":"v","groupby":["g"],"cumulative":true,"counts":true,"bandwidth":0,"steps":10,"minsteps":5,"maxsteps":50,"extent":[5,1],"resolve":"independent","as":["x","p"]}""")]
    [InlineData("""{"quantile":"v"}""")]
    [InlineData("""{"quantile":"v","probs":[0.1,1.5,-1],"step":0,"groupby":["g"],"as":["p","q"]}""")]
    [InlineData("""{"extent":"v","param":"e"}""")]
    [InlineData("""{"pivot":"k","value":"v","op":"q1"}""")]
    [InlineData("""{"pivot":"k","value":"v","op":"argmax"}""")]
    [InlineData("""{"pivot":"k","value":"v","op":"average"}""")]
    public void SpecParser_Parse_AcceptsTheWellFormedStatisticalTransforms(string entry)
    {
        // Arrange / Act
        var tree = SpecParser.Parse("{\"transform\":[" + entry + "]}", "t.json");

        // Assert
        Assert.True(SpecParser.IsSupportedTransform(SpecParser.FindTransformOperation(tree.Root["transform"]!.Items[0])!.Name!));
    }

    /// <summary>
    ///     Validates Vega-Lite's classification: an entry with an <c>extent</c> property is the extent transform unless it also has a <c>regression</c> or a <c>density</c> (an option of those),
    ///     in whatever order the properties are written; every other entry is classified by its first transform property.
    /// </summary>
    /// <param name="entry">The transform entry.</param>
    /// <param name="expected">The expected transform name.</param>
    [Theory]
    [InlineData("""{"extent":"v","param":"e"}""", "extent")]
    [InlineData("""{"extent":[0,10],"regression":"y","on":"x"}""", "regression")]
    [InlineData("""{"regression":"y","on":"x","extent":[0,10]}""", "regression")]
    [InlineData("""{"extent":[0,10],"density":"v"}""", "density")]
    [InlineData("""{"density":"v","extent":[0,10]}""", "density")]
    [InlineData("""{"extent":"v","param":"e","groupby":["g"]}""", "extent")]
    [InlineData("""{"calculate":"1","as":"a"}""", "calculate")]
    [InlineData("""{"quantile":"v","groupby":["g"]}""", "quantile")]
    public void SpecParser_FindTransformOperation_AnExtentNextToARegressionOrDensityIsTheirOption(string entry, string expected)
    {
        // Arrange
        var tree = SpecParser.Parse("{\"transform\":[" + entry + "]}", "t.json");
        var node = tree.Root["transform"]!.Items[0];

        // Act
        var operation = SpecParser.FindTransformOperation(node);

        // Assert
        Assert.Equal(expected, operation!.Name);
    }

    /// <summary>
    ///     Validates that an expression may name the parameter of an <c>extent</c> transform without declaring it in <c>params</c>: in the transform that follows it, in a view below the one that
    ///     holds the extent, and as a variable of a mark property; and that an expression that names an unknown parameter is still an error.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_AnExtentParameterIsVisibleToTheExpressionsOfTheViewAndItsDescendants()
    {
        // Arrange
        const string sameView = """{"transform":[{"extent":"v","param":"e"},{"calculate":"datum.v / e[1]","as":"r"}]}""";
        const string below = """{"transform":[{"extent":"v","param":"e"}],"layer":[{"mark":"point","transform":[{"filter":"datum.v < e[1]"}]}]}""";

        // Act
        var same = SpecParser.Parse(sameView, "t.json");
        var nested = SpecParser.Parse(below, "t.json");
        var unknown = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("""{"transform":[{"extent":"v","param":"e"},{"calculate":"e[1] + other","as":"r"}]}""", "t.json"));

        // Assert
        Assert.NotNull(same);
        Assert.NotNull(nested);
        Assert.Contains("'other'", unknown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the parameter of an extent that is not visible from a node (a sibling view, or a regression's <c>extent</c> property) is not a parameter, and that
    ///     <see cref="SpecParser.ExtentParameterNames"/> lists the parameters of the extent transforms of a whole specification in document order without repeats.
    /// </summary>
    [Fact]
    public void SpecParser_IsExtentParameter_OnlyExtentTransformsOfTheViewOrAncestorsCount()
    {
        // Arrange
        const string json = """
            {
              "transform": [ { "extent": "v", "param": "top" }, { "regression": "y", "on": "x", "extent": [0, 1] } ],
              "hconcat": [
                { "transform": [ { "extent": "v", "param": "left" }, { "extent": "w", "param": "top" } ], "mark": "point" },
                { "layer": [ { "transform": [ { "extent": "v", "param": "right" } ], "mark": "point" } ] }
              ]
            }
            """;
        var tree = SpecParser.Parse(json, "t.json");
        var left = tree.Root["hconcat"]!.Items[0];
        var right = tree.Root["hconcat"]!.Items[1]["layer"]!.Items[0];

        // Act / Assert
        Assert.True(SpecParser.IsExtentParameter(left, "left"));
        Assert.True(SpecParser.IsExtentParameter(left, "top"));
        Assert.False(SpecParser.IsExtentParameter(left, "right"));
        Assert.True(SpecParser.IsExtentParameter(right, "top"));
        Assert.True(SpecParser.IsExtentParameter(right, "right"));
        Assert.False(SpecParser.IsExtentParameter(right, "left"));
        Assert.Equal(["top", "left", "right"], SpecParser.ExtentParameterNames(tree.Root));
        Assert.Empty(SpecParser.ExtentParameterNames(SpecParser.Parse("""{"mark":"point"}""", "t.json").Root));
    }

    /// <summary>
    ///     Validates that the parameter of an extent transform must not be the name of a declared parameter (two signals of one name are an error in Vega).
    /// </summary>
    [Fact]
    public void SpecParser_Parse_AnExtentParameterCannotRepeatADeclaredParameter()
    {
        // Arrange / Act
        var error = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(
            """{"params":[{"name":"e","value":1}],"transform":[{"extent":"v","param":"e"}]}""", "t.json"));

        // Assert
        Assert.Equal("/transform/0/param", error.Location.Pointer);
        Assert.Contains("'e'", error.Message, StringComparison.Ordinal);
    }
}

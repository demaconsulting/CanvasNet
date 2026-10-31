using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests for the validation of the <c>impute</c> object of a channel definition (<c>keyvals</c>, <c>method</c>, <c>value</c> and <c>frame</c>), which is part
///     of <see cref="SpecParser"/>: located errors for a wrong value, the bound of the number of key values, and the warning for a member Vega-Lite does not define.
/// </summary>
public class SpecParserImputeTests
{
    /// <summary>
    ///     Builds a line chart specification whose <c>y</c> channel carries an impute object.
    /// </summary>
    /// <param name="impute">The text of the <c>impute</c> value.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string impute) =>
        "{\"mark\":\"line\",\"encoding\":{\"x\":{\"field\":\"t\",\"type\":\"quantitative\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\",\"impute\":" + impute + "}}}";

    /// <summary>
    ///     Validates that a wrongly shaped <c>impute</c> object is a located error at the offending value, and that the number of key values is bounded by
    ///     <see cref="VegaLiteLimits.MaxScaleDomainSize"/> (two here) for an array and for the sequence <c>{start, stop, step}</c>.
    /// </summary>
    /// <param name="impute">The text of the <c>impute</c> value.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("\"mean\"", "/encoding/y/impute")]
    [InlineData("true", "/encoding/y/impute")]
    [InlineData("[1]", "/encoding/y/impute")]
    [InlineData("""{"method":"mode"}""", "/encoding/y/impute/method")]
    [InlineData("""{"method":3}""", "/encoding/y/impute/method")]
    [InlineData("""{"method":"MEAN"}""", "/encoding/y/impute/method")]
    [InlineData("""{"keyvals":3}""", "/encoding/y/impute/keyvals")]
    [InlineData("""{"keyvals":"a"}""", "/encoding/y/impute/keyvals")]
    [InlineData("""{"keyvals":[[1]]}""", "/encoding/y/impute/keyvals/0")]
    [InlineData("""{"keyvals":[1,{"x":1}]}""", "/encoding/y/impute/keyvals/1")]
    [InlineData("""{"keyvals":{"start":0}}""", "/encoding/y/impute/keyvals")]
    [InlineData("""{"keyvals":{"stop":"x"}}""", "/encoding/y/impute/keyvals/stop")]
    [InlineData("""{"keyvals":{"stop":5,"start":"a"}}""", "/encoding/y/impute/keyvals/start")]
    [InlineData("""{"keyvals":{"stop":5,"step":0}}""", "/encoding/y/impute/keyvals/step")]
    [InlineData("""{"keyvals":[1,2,3]}""", "/encoding/y/impute/keyvals")]
    [InlineData("""{"keyvals":{"start":0,"stop":3}}""", "/encoding/y/impute/keyvals")]
    [InlineData("""{"keyvals":{"start":10,"stop":0,"step":-3}}""", "/encoding/y/impute/keyvals")]
    [InlineData("""{"keyvals":{"stop":1e300,"step":1e-300}}""", "/encoding/y/impute/keyvals")]
    [InlineData("""{"value":{"a":1}}""", "/encoding/y/impute/value")]
    [InlineData("""{"value":[1]}""", "/encoding/y/impute/value")]
    [InlineData("""{"frame":3}""", "/encoding/y/impute/frame")]
    [InlineData("""{"frame":[null]}""", "/encoding/y/impute/frame")]
    [InlineData("""{"frame":[0.5,null]}""", "/encoding/y/impute/frame/0")]
    [InlineData("""{"frame":[null,"a"]}""", "/encoding/y/impute/frame/1")]
    public void SpecParser_Parse_RejectsABadImputeObjectWithItsLocation(string impute, string pointer)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Spec(impute), "t.json", new VegaLiteLimits { MaxScaleDomainSize = 2 }));

        // Assert
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Equal("t.json", ex.Location.File);
        Assert.True(ex.Location.Line >= 1 && ex.Location.Column >= 1);
        Assert.StartsWith("t.json:", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that every valid form of the object is accepted without a warning: empty, each method, a constant of every scalar kind, a frame with nulls and
    ///     integers, key values as an array (up to the limit) and as a sequence (with a negative step too), and an explicit null.
    /// </summary>
    /// <param name="impute">The text of the <c>impute</c> value.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"method":"value","value":"n/a"}""")]
    [InlineData("""{"method":"mean","frame":[-1,1]}""")]
    [InlineData("""{"method":"median","frame":[null,null]}""")]
    [InlineData("""{"method":"max","frame":[null,0]}""")]
    [InlineData("""{"method":"min"}""")]
    [InlineData("""{"value":null}""")]
    [InlineData("""{"value":true}""")]
    [InlineData("""{"value":-2.5}""")]
    [InlineData("""{"keyvals":[1,"b"]}""")]
    [InlineData("""{"keyvals":[null,true]}""")]
    [InlineData("""{"keyvals":{"stop":2}}""")]
    [InlineData("""{"keyvals":{"start":0,"stop":10,"step":5}}""")]
    [InlineData("""{"keyvals":{"start":2,"stop":0,"step":-1}}""")]
    [InlineData("""{"keyvals":{"start":0,"stop":3,"step":-1}}""")]
    public void SpecParser_Parse_AcceptsTheImputeForms(string impute)
    {
        // Act
        var tree = SpecParser.Parse(Spec(impute), "t.json", new VegaLiteLimits { MaxScaleDomainSize = 2 });

        // Assert
        Assert.Empty(tree.Warnings);
        Assert.NotNull(tree.Root["encoding"]!["y"]!["impute"]);
    }

    /// <summary>
    ///     Validates that a member that Vega-Lite does not define is a warning located at its key, and that the object is otherwise accepted.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_WarnsAboutUnknownImputeMembers()
    {
        // Act
        var tree = SpecParser.Parse(Spec("""{"method":"mean","groupby":["g"],"key":"k"}"""), "t.json");

        // Assert
        Assert.Equal(["vl-unknown-property", "vl-unknown-property"], tree.Warnings.Select(w => w.Id));
        Assert.Equal(["/encoding/y/impute/groupby", "/encoding/y/impute/key"], tree.Warnings.Select(w => w.Location.Pointer));
        Assert.All(tree.Warnings, w => Assert.Equal(FeatureReportKind.Warning, w.Kind));
        Assert.Contains("an impute definition", tree.Warnings[0].Message, StringComparison.Ordinal);
    }
}

using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the validation of selection parameters, the legacy <c>selection</c> property and the predicates on them in <see cref="SpecParser"/>, and of the lookup of a selection by name
///     (<see cref="SpecParser.FindParameter"/>): a selection is visible from every view of the specification, a variable parameter only from the views below its declaration.
/// </summary>
public class SpecParserSelectionTests
{
    /// <summary>
    ///     Parses a point chart whose only special property is the <c>params</c> array.
    /// </summary>
    /// <param name="parameters">The text of the <c>params</c> array.</param>
    /// <returns>The tree.</returns>
    private static SpecTree Parse(string parameters) =>
        SpecParser.Parse("{ \"params\": " + parameters + ", \"data\": { \"values\": [ { \"a\": 1 } ] }, \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" } } }");

    /// <summary>
    ///     Validates that a <c>select</c> that is not <c>point</c> or <c>interval</c> (as a text or as the <c>type</c> of an object) is a located error.
    /// </summary>
    /// <param name="select">The text of the <c>select</c> value.</param>
    /// <param name="pointer">The expected pointer of the error.</param>
    [Theory]
    [InlineData("\"lasso\"", "/params/0/select")]
    [InlineData("{ \"type\": \"lasso\" }", "/params/0/select/type")]
    [InlineData("{ \"on\": \"click\" }", "/params/0/select")]
    [InlineData("3", "/params/0/select")]
    public void SpecParser_Parse_SelectWithAnUnknownTypeIsAnError(string select, string pointer)
    {
        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => Parse("[ { \"name\": \"s\", \"select\": " + select + " } ]"));

        // Assert
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Contains("'point' or 'interval'", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the options of a selection that the renderer reads: <c>fields</c> and <c>encodings</c> are arrays of texts, <c>resolve</c> is one of three words, and <c>views</c> is an array.
    /// </summary>
    /// <param name="parameter">The text of the parameter object.</param>
    /// <param name="pointer">The expected pointer of the error.</param>
    [Theory]
    [InlineData("{ \"name\": \"s\", \"select\": { \"type\": \"point\", \"fields\": \"a\" } }", "/params/0/select/fields")]
    [InlineData("{ \"name\": \"s\", \"select\": { \"type\": \"point\", \"fields\": [1] } }", "/params/0/select/fields")]
    [InlineData("{ \"name\": \"s\", \"select\": { \"type\": \"interval\", \"encodings\": [\"x\", 2] } }", "/params/0/select/encodings")]
    [InlineData("{ \"name\": \"s\", \"select\": { \"type\": \"interval\", \"resolve\": \"first\" } }", "/params/0/select/resolve")]
    [InlineData("{ \"name\": \"s\", \"select\": \"interval\", \"views\": \"left\" }", "/params/0/views")]
    public void SpecParser_Parse_MalformedSelectionOptionsAreErrors(string parameter, string pointer)
    {
        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => Parse("[ " + parameter + " ]"));

        // Assert
        Assert.Equal(pointer, ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the interaction options are accepted whatever their value (they are ignored), and that the accepted forms of a selection parse without a warning.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_SelectionFormsAreAccepted()
    {
        // Arrange
        const string parameters = """
            [ { "name": "a", "select": "point" },
              { "name": "b", "select": { "type": "interval", "encodings": ["x"], "on": "[mousedown, mouseup] > mousemove", "clear": false, "translate": false, "zoom": "wheel", "mark": { "fill": "red" } },
                "value": { "x": [1, 2] }, "bind": "scales" },
              { "name": "c", "select": { "type": "point", "fields": ["a"], "toggle": "event.shiftKey", "nearest": true, "resolve": "union" }, "value": [ { "a": 1 } ],
                "bind": { "a": { "input": "select", "options": [1, 2] } }, "views": ["one", ["x", "y"]] } ]
            """;

        // Act
        var tree = Parse(parameters);

        // Assert
        Assert.Empty(tree.Warnings);
        Assert.Equal(["a", "b", "c"], SpecMetadata.Read(tree).ParameterNames);
    }

    /// <summary>
    ///     Validates the legacy <c>selection</c> property: an object of selections with a <c>type</c> of <c>single</c>, <c>multi</c> or <c>interval</c> is accepted; another type, or a value that is not an
    ///     object, is a located error.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_LegacySelectionIsValidated()
    {
        // Arrange
        const string rest = ", \"data\": { \"values\": [ { \"a\": 1 } ] }, \"mark\": \"point\" }";

        // Act
        var accepted = SpecParser.Parse("{ \"selection\": { \"a\": { \"type\": \"single\" }, \"b\": { \"type\": \"multi\", \"fields\": [\"a\"] }, \"c\": { \"type\": \"interval\" } }" + rest);
        var unknown = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"selection\": { \"a\": { \"type\": \"point\" } }" + rest));
        var missing = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"selection\": { \"a\": {} }" + rest));
        var notObject = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"selection\": [ \"a\" ]" + rest));

        // Assert
        Assert.Empty(accepted.Warnings);
        Assert.Equal("/selection/a/type", unknown.Location.Pointer);
        Assert.Equal("/selection/a", missing.Location.Pointer);
        Assert.Equal("/selection", notObject.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the predicates on selections name a selection of the specification: a <c>param</c> or <c>selection</c> that no view declares is a located error, in a filter and in a condition.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_PredicateOnAnUndeclaredSelectionIsAnError()
    {
        // Arrange
        const string head = "{ \"params\": [ { \"name\": \"s\", \"select\": \"interval\" } ], \"data\": { \"values\": [ { \"a\": 1 } ] }, ";
        const string tail = ", \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"a\", \"type\": \"quantitative\" }, \"color\": COLOR } }";

        // Act
        var filter = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(head + "\"transform\": [ { \"filter\": { \"param\": \"gone\" } } ]" + tail.Replace("COLOR", "{ \"value\": \"red\" }", StringComparison.Ordinal)));
        var legacyFilter = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(head + "\"transform\": [ { \"filter\": { \"selection\": { \"and\": [ \"s\", \"gone\" ] } } } ]" + tail.Replace("COLOR", "{ \"value\": \"red\" }", StringComparison.Ordinal)));
        var condition = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(head + "\"width\": 100" + tail.Replace("COLOR", "{ \"condition\": { \"selection\": \"gone\", \"value\": \"blue\" }, \"value\": \"red\" }", StringComparison.Ordinal)));

        // Assert
        Assert.Equal("/transform/0/filter/param", filter.Location.Pointer);
        Assert.Equal("/transform/0/filter/selection/and/1", legacyFilter.Location.Pointer);
        Assert.Equal("/encoding/color/condition/selection", condition.Location.Pointer);
    }

    /// <summary>
    ///     Validates the lookup of a parameter by name: a selection of another view is visible (Vega-Lite merges the selections of the children into their parents), the first declaration of a name wins, and the variable
    ///     parameter of another view is not visible.
    /// </summary>
    [Fact]
    public void SpecParser_FindParameter_SelectionsAreVisibleFromEveryView()
    {
        // Arrange: the selection of the first layer is used by a condition of the second; the variable parameter of the first layer is not visible from the second
        const string json = """
            { "data": { "values": [ { "a": 1 } ] },
              "layer": [
                { "params": [ { "name": "s", "select": "interval" } ], "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } },
                { "mark": "square", "encoding": { "x": { "field": "a", "type": "quantitative" }, "color": { "condition": { "param": "s", "value": "red" }, "value": "blue" } } } ] }
            """;
        var tree = SpecParser.Parse(json);
        var second = tree.Root["layer"]!.Items[1];
        var first = tree.Root["layer"]!.Items[0];

        // Act
        var found = SpecParser.FindParameter(second["encoding"]!["color"]!, "s");
        var own = SpecParser.FindParameter(first["encoding"]!, "s");

        // Assert: both find the declaration in the first layer; the second layer's condition compiled the selection test
        Assert.Same(first["params"]!.Items[0], found);
        Assert.Same(first["params"]!.Items[0], own);
        Assert.Null(SpecParser.FindParameter(second, "missing"));
        Assert.Equal(
            "(!length(data(\"s_store\")) || vlSelectionTest(\"s_store\", datum))",
            tree.Expressions.Get(second["encoding"]!["color"]!["condition"]!["param"])!.Source);
    }
}

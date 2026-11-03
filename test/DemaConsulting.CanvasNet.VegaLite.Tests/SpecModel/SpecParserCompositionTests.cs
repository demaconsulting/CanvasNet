using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the rules that concatenations, facets and repeats add to <see cref="SpecParser"/>: the required <c>spec</c>, the shapes of <c>facet</c>, <c>repeat</c>,
///     <c>columns</c>, <c>spacing</c>, <c>align</c>, <c>center</c> and <c>header</c>, the <c>{"repeat": ...}</c> references, and the views that cannot be composed.
/// </summary>
public class SpecParserCompositionTests
{
    /// <summary>The repeated view of the specifications below.</summary>
    private const string Bar = "{ \"mark\": \"bar\" }";

    /// <summary>
    ///     Validates that a <c>facet</c> without a <c>spec</c> is a specification error located at the <c>facet</c>.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_FacetWithoutSpecIsError()
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("""{ "facet": { "field": "a" } }"""));

        // Assert
        Assert.Equal("/facet", ex.Location.Pointer);
        Assert.Contains("spec", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a facet mapping whose entry has no <c>field</c>, and a <c>facet</c> that names no field at all, are specification errors.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_FacetMappingWithoutFieldIsError()
    {
        // Arrange / Act
        var entry = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"facet\": { \"row\": { \"type\": \"nominal\" } }, \"spec\": " + Bar + " }"));
        var empty = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"facet\": { }, \"spec\": " + Bar + " }"));
        var wrongKind = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"facet\": { \"column\": \"a\" }, \"spec\": " + Bar + " }"));

        // Assert
        Assert.Equal("/facet/row", entry.Location.Pointer);
        Assert.Equal("/facet", empty.Location.Pointer);
        Assert.Equal("/facet/column", wrongKind.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a <c>repeat</c> without a <c>spec</c> is a specification error located at the <c>repeat</c>.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_RepeatWithoutSpecIsError()
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("""{ "repeat": [ "a" ] }"""));

        // Assert
        Assert.Equal("/repeat", ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the values of a <c>repeat</c> must be strings, whether it is an array or has a <c>row</c>, <c>column</c> or <c>layer</c> array.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_RepeatArrayOfNonStringsIsError()
    {
        // Arrange / Act
        var array = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"repeat\": [ \"a\", 1 ], \"spec\": " + Bar + " }"));
        var row = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"repeat\": { \"row\": [ true ] }, \"spec\": " + Bar + " }"));
        var notArray = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"repeat\": { \"column\": \"a\" }, \"spec\": " + Bar + " }"));
        var number = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"repeat\": 3, \"spec\": " + Bar + " }"));

        // Assert
        Assert.Equal("/repeat/1", array.Location.Pointer);
        Assert.Equal("/repeat/row/0", row.Location.Pointer);
        Assert.Equal("/repeat/column", notArray.Location.Pointer);
        Assert.Equal("/repeat", number.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a key of a <c>repeat</c> object other than <c>row</c>, <c>column</c> and <c>layer</c> is a warning, not an error.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_RepeatObjectWithUnknownKeyWarns()
    {
        // Arrange / Act
        var tree = SpecParser.Parse("{ \"repeat\": { \"row\": [ \"a\" ], \"diagonal\": [ \"b\" ] }, \"spec\": " + Bar + " }");

        // Assert
        var warning = Assert.Single(tree.Warnings);
        Assert.Equal("vl-unknown-property", warning.Id);
        Assert.Equal("/repeat/diagonal", warning.Location.Pointer);
    }

    /// <summary>
    ///     Validates that <c>columns</c> must be a non-negative integer, on a view and on a <c>facet</c> channel definition.
    /// </summary>
    /// <param name="columns">The JSON text of the value.</param>
    /// <param name="pointer">The pointer the error is expected at.</param>
    [Theory]
    [InlineData("-1", "/columns")]
    [InlineData("1.5", "/columns")]
    [InlineData("\"2\"", "/columns")]
    [InlineData("{}", "/columns")]
    public void SpecParser_Parse_ColumnsMustBeNonNegativeInteger(string columns, string pointer)
    {
        // Arrange / Act
        var view = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"concat\": [ " + Bar + " ], \"columns\": " + columns + " }"));
        var channel = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("{ \"mark\": \"bar\", \"encoding\": { \"facet\": { \"field\": \"a\", \"columns\": " + columns + " } } }"));

        // Assert
        Assert.Equal(pointer, view.Location.Pointer);
        Assert.Equal("/encoding/facet/columns", channel.Location.Pointer);
    }

    /// <summary>
    ///     Validates that zero and positive whole numbers are accepted as <c>columns</c>.
    /// </summary>
    /// <param name="columns">The columns.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(12)]
    public void SpecParser_Parse_ColumnsAcceptsWholeNumbers(int columns)
    {
        // Arrange / Act
        var tree = SpecParser.Parse("{ \"concat\": [ " + Bar + " ], \"columns\": " + columns + " }");

        // Assert
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Validates that <c>spacing</c> is a number or an object of numbers for <c>row</c> and <c>column</c>, and that an unknown key of the object warns.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_SpacingAcceptsNumberAndRowColumnObject()
    {
        // Arrange / Act
        var number = SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"spacing\": 5 }");
        var pair = SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"spacing\": { \"row\": 1, \"column\": 2 } }");
        var extra = SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"spacing\": { \"row\": 1, \"diagonal\": 2 } }");
        var text = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"spacing\": \"5\" }"));
        var entry = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"spacing\": { \"row\": \"x\" } }"));

        // Assert
        Assert.Empty(number.Warnings);
        Assert.Empty(pair.Warnings);
        Assert.Equal("/spacing/diagonal", Assert.Single(extra.Warnings).Location.Pointer);
        Assert.Equal("/spacing", text.Location.Pointer);
        Assert.Equal("/spacing/row", entry.Location.Pointer);
    }

    /// <summary>
    ///     Validates the shapes of <c>align</c> (one of <c>all</c>, <c>each</c>, <c>none</c>, or an object of them) and <c>center</c> (a boolean, or an object of booleans).
    /// </summary>
    [Fact]
    public void SpecParser_Parse_AlignCenterShapes()
    {
        // Arrange / Act
        var valid = SpecParser.Parse(
            "{ \"hconcat\": [ " + Bar + " ], \"align\": { \"row\": \"each\", \"column\": \"none\" }, \"center\": { \"column\": false } }");
        var simple = SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"align\": \"all\", \"center\": true }");
        var unknownAlign = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"align\": \"sideways\" }"));
        var wrongEntry = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"align\": { \"row\": 1 } }"));
        var wrongCenter = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"center\": \"yes\" }"));
        var wrongCenterEntry = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"center\": { \"row\": 1 } }"));
        var extra = SpecParser.Parse("{ \"hconcat\": [ " + Bar + " ], \"center\": { \"row\": true, \"both\": true } }");

        // Assert
        Assert.Empty(valid.Warnings);
        Assert.Empty(simple.Warnings);
        Assert.Equal("/align", unknownAlign.Location.Pointer);
        Assert.Equal("/align/row", wrongEntry.Location.Pointer);
        Assert.Equal("/center", wrongCenter.Location.Pointer);
        Assert.Equal("/center/row", wrongCenterEntry.Location.Pointer);
        Assert.Equal("/center/both", Assert.Single(extra.Warnings).Location.Pointer);
    }

    /// <summary>
    ///     Validates that a <c>{"repeat": ...}</c> reference is accepted in a <c>field</c>, a <c>datum</c>, a <c>sort.field</c>, a facet field and the field of a condition, and that a
    ///     reference naming anything other than <c>row</c>, <c>column</c>, <c>repeat</c> or <c>layer</c> is an error at the name.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_RepeatRefIsAcceptedAsField()
    {
        // Arrange
        const string Valid =
            """
            { "repeat": { "row": [ "a" ], "column": [ "b" ], "layer": [ "c" ] },
              "spec": { "mark": "point", "encoding": {
                "x": { "field": { "repeat": "row" }, "type": "quantitative", "sort": { "field": { "repeat": "layer" }, "op": "mean" } },
                "y": { "datum": { "repeat": "column" } },
                "row": { "field": { "repeat": "repeat" } },
                "color": { "condition": { "test": "datum.a > 1", "field": { "repeat": "layer" }, "type": "quantitative" }, "value": "gray" } } } }
            """;

        // Act
        var tree = SpecParser.Parse(Valid);
        var field = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "mark": "bar", "encoding": { "x": { "field": { "repeat": "diagonal" } } } }"""));
        var datum = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "mark": "bar", "encoding": { "x": { "datum": { "repeat": 1 } } } }"""));
        var sort = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "mark": "bar", "encoding": { "x": { "field": "a", "sort": { "field": { "repeat": "x" } } } } }"""));
        var condition = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "mark": "bar", "encoding": { "color": { "condition": [ { "test": "datum.a", "field": { "repeat": "x" } } ] } } }"""));

        // Assert
        Assert.Empty(tree.Warnings);
        Assert.Equal("/encoding/x/field/repeat", field.Location.Pointer);
        Assert.Equal("/encoding/x/datum/repeat", datum.Location.Pointer);
        Assert.Equal("/encoding/x/sort/field/repeat", sort.Location.Pointer);
        Assert.Equal("/encoding/color/condition/0/field/repeat", condition.Location.Pointer);
    }

    /// <summary>
    ///     Validates the <c>header</c> of a faceting channel and of a facet field: the properties have the shapes of Vega-Lite's header, a wrong one is an error at the value,
    ///     an unknown one is a warning, and <c>null</c> is accepted.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_HeaderPropertyShapes()
    {
        // Arrange
        const string Valid =
            """
            { "mark": "bar", "encoding": { "row": { "field": "a", "header": {
                "title": "T", "labelAngle": 0, "labelPadding": 4, "labels": false, "labelOrient": "left", "titleFontSize": 12, "labelAnchor": "start", "format": ".1f" } } } }
            """;

        // Act
        var tree = SpecParser.Parse(Valid);
        var nullHeader = SpecParser.Parse("""{ "mark": "bar", "encoding": { "row": { "field": "a", "header": null } } }""");
        var unknown = SpecParser.Parse("""{ "mark": "bar", "encoding": { "column": { "field": "a", "header": { "bogus": 1 } } } }""");
        var angle = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "mark": "bar", "encoding": { "row": { "field": "a", "header": { "labelAngle": "x" } } } }"""));
        var orient = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "mark": "bar", "encoding": { "row": { "field": "a", "header": { "labelOrient": "middle" } } } }"""));
        var labels = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "facet": { "field": "a", "header": { "labels": 1 } }, "spec": { "mark": "bar" } }"""));

        // Assert
        Assert.Empty(tree.Warnings);
        Assert.Empty(nullHeader.Warnings);
        var warning = Assert.Single(unknown.Warnings);
        Assert.Equal("vl-unknown-property", warning.Id);
        Assert.Equal("/encoding/column/header/bogus", warning.Location.Pointer);
        Assert.Equal("/encoding/row/header/labelAngle", angle.Location.Pointer);
        Assert.Equal("/encoding/row/header/labelOrient", orient.Location.Pointer);
        Assert.Equal("/facet/header/labels", labels.Location.Pointer);
    }

    /// <summary>
    ///     Validates that <c>resolve</c> on a concatenation accepts the position channels and their offsets for scales, and the axis of <c>x</c>, without a warning.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ResolveOnConcatAcceptsXAndOffsetChannels()
    {
        // Arrange / Act
        var tree = SpecParser.Parse(
            "{ \"hconcat\": [ " + Bar + " ], \"resolve\": { \"scale\": { \"x\": \"shared\", \"xOffset\": \"independent\", \"color\": \"independent\" }, \"axis\": { \"x\": \"shared\" } } }");

        // Assert
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Validates that a <c>facet</c> accepts a field definition with its own <c>spec</c> and the layout properties of the view, and that the views of the <c>spec</c> are validated.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_FacetWithFieldDefinitionAndLayoutIsAccepted()
    {
        // Arrange / Act
        var tree = SpecParser.Parse(
            "{ \"facet\": { \"field\": \"a\", \"type\": \"nominal\", \"sort\": \"descending\" }, \"columns\": 3, \"spacing\": 4, \"bounds\": \"flush\", \"spec\": " + Bar + " }");
        var inner = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "facet": { "field": "a" }, "spec": { "mark": "bar", "width": "wide" } }"""));

        // Assert
        Assert.Empty(tree.Warnings);
        Assert.Equal("/spec/width", inner.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the <c>spec</c> of a facet and a view of a <c>layer</c> cannot be a concatenation, a facet or a repeat, while the <c>spec</c> of a repeat can.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_FacetSpecAndLayerChildCannotBeCompositions()
    {
        // Arrange / Act
        var facet = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "facet": { "field": "a" }, "spec": { "repeat": [ "a" ], "spec": { "mark": "bar" } } }"""));
        var layer = Assert.Throws<VegaLiteSpecException>(
            () => SpecParser.Parse("""{ "layer": [ { "facet": { "field": "a" }, "spec": { "mark": "bar" } } ] }"""));
        var repeat = SpecParser.Parse("""{ "repeat": [ "a" ], "spec": { "hconcat": [ { "mark": "bar" } ] } }""");

        // Assert
        Assert.Equal("/spec", facet.Location.Pointer);
        Assert.Equal("/layer/0", layer.Location.Pointer);
        Assert.Empty(repeat.Warnings);
    }
}

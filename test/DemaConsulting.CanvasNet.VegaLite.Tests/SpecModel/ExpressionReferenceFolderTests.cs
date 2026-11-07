using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using Owner = DemaConsulting.CanvasNet.VegaLite.SpecModel.ExpressionReferenceSites.Owner;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Tests of the expression reference folder (the evaluation, once, of the <c>{"expr": ...}</c> values of the static sites) and of the table of sites it works from. Every expected value is
///     derived by hand from the expressions and the parameters of the specification text.
/// </summary>
public class ExpressionReferenceFolderTests
{
    /// <summary>The name the test specifications give to every location.</summary>
    private const string File = "fold.vl.json";

    /// <summary>
    ///     Parses a specification, evaluates its parameters and folds it the way the pipeline does.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="overrides">The values of the caller, or null.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The parsed tree and the result of the fold.</returns>
    private static (SpecTree Parsed, FoldResult Result) Fold(string json, IReadOnlyDictionary<string, object?>? overrides = null, VegaLiteLimits? limits = null)
    {
        var parsed = SpecParser.Parse(json, File, limits);
        var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(limits, TestContext.Current.CancellationToken, values);
        ParameterEnvironment.Populate(parsed, overrides, values, evaluator);
        return (parsed, ExpressionReferenceFolder.Fold(parsed, evaluator));
    }

    /// <summary>
    ///     Follows a JSON pointer from a node.
    /// </summary>
    /// <param name="root">The node to start at.</param>
    /// <param name="pointer">The pointer, such as <c>/title/fontSize</c>.</param>
    /// <returns>The node.</returns>
    private static SpecNode At(SpecNode root, string pointer)
    {
        var node = root;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node.IsArray ? node.Items[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)] : node[segment]!;
        }

        return node;
    }

    /// <summary>
    ///     Validates that a specification without a static expression reference is returned as it is: the same tree, no warning.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_WithoutReferences_ReturnsTheSameTree()
    {
        // Arrange / Act
        var (parsed, result) = Fold("""{ "title": "T", "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }""");

        // Assert
        Assert.Same(parsed, result.Tree);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a reference is replaced by the value of its expression with the parameters at their initial state: the expression of a parameter wins over its value, and a
    ///     parameter can read another one (6 is 3 doubled, the font size 16 is 6 plus 10).
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_ReplacesAReferenceByItsValue()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "base", "value": 3 }, { "name": "twice", "expr": "base * 2" }, { "name": "label", "value": "ignored", "expr": "'n=' + twice" } ],
              "title": { "text": { "expr": "label" }, "fontSize": { "expr": "twice + 10" }, "color": { "expr": "twice > 5 ? 'red' : 'blue'" } },
              "padding": { "expr": "twice" },
              "background": { "expr": "'#eeeeee'" },
              "mark": "point"
            }
            """;

        // Act
        var (_, result) = Fold(json);

        // Assert
        var root = result.Tree.Root;
        Assert.Equal("n=6", At(root, "/title/text").StringValue);
        Assert.Equal(16, At(root, "/title/fontSize").NumberValue);
        Assert.Equal("red", At(root, "/title/color").StringValue);
        Assert.Equal(6, At(root, "/padding").NumberValue);
        Assert.Equal("#eeeeee", At(root, "/background").StringValue);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the value of the caller replaces the definition of a parameter (the expression is not evaluated), also a structured value, and that a folded reference reads it.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_ReadsTheValuesOfTheCaller()
    {
        // Arrange: the caller gives a size and a structured value (an array of two numbers)
        const string json = """
            {
              "params": [ { "name": "size", "value": 11 }, { "name": "range", "value": [0, 1] } ],
              "title": { "text": "T", "fontSize": { "expr": "size" } },
              "mark": "point",
              "encoding": { "x": { "field": "a", "type": "quantitative", "scale": { "domain": { "expr": "range" } } } }
            }
            """;
        var overrides = new Dictionary<string, object?> { ["size"] = 20, ["range"] = new object[] { 5, 9 } };

        // Act
        var (_, result) = Fold(json, overrides);

        // Assert: the size is the caller's, the domain is the array [5, 9]
        var root = result.Tree.Root;
        Assert.Equal(20, At(root, "/title/fontSize").NumberValue);
        var domain = At(root, "/encoding/x/scale/domain");
        Assert.True(domain.IsArray);
        Assert.Equal([5.0, 9.0], domain.Items.Select(i => i.NumberValue));
    }

    /// <summary>
    ///     Validates that the subtrees that hold no reference keep their identity (so the compiled expressions, which are keyed by node, stay valid), that the nodes on the way to a reference
    ///     are copies, and that a folded value is located at the reference it replaces.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_KeepsUntouchedSubtreesAndLocatesTheValues()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "p", "value": 4 } ],
              "data": { "values": [ { "a": 1 } ] },
              "title": { "text": "T", "fontSize": { "expr": "p" } },
              "mark": { "type": "point", "filled": true },
              "encoding": { "x": { "field": "a", "type": "quantitative" } }
            }
            """;

        // Act
        var (parsed, result) = Fold(json);

        // Assert: what holds no reference is the very same node; what leads to one is a copy that reads like the original
        Assert.NotSame(parsed.Root, result.Tree.Root);
        Assert.Same(parsed.Root["data"], result.Tree.Root["data"]);
        Assert.Same(parsed.Root["params"], result.Tree.Root["params"]);
        Assert.Same(parsed.Root["mark"], result.Tree.Root["mark"]);
        Assert.Same(parsed.Root["encoding"], result.Tree.Root["encoding"]);
        Assert.NotSame(parsed.Root["title"], result.Tree.Root["title"]);
        Assert.Same(parsed.Root["title"]!["text"], result.Tree.Root["title"]!["text"]);
        Assert.Equal("/title", result.Tree.Root["title"]!.Pointer);

        // Assert: the value has the location of the reference it replaces
        var reference = parsed.Root["title"]!["fontSize"]!;
        var folded = result.Tree.Root["title"]!["fontSize"]!;
        Assert.Equal((reference.Pointer, reference.Line, reference.Column), (folded.Pointer, folded.Line, folded.Column));
        Assert.Equal("fontSize", folded.Name);
        Assert.Equal(4, folded.NumberValue);
        Assert.Same(parsed.Root["title"], folded.Parent);
    }

    /// <summary>
    ///     Validates that the references of the row-bound sites are not static: the paint properties of the mark and the value of a paint channel keep their path (the mark evaluates them for every
    ///     row), so the tree is returned as it is and they are not listed.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_LeavesTheRowBoundSitesToTheMarks()
    {
        // Arrange: a symbol mark whose paint properties and color value are row-bound
        const string json = """
            {
              "params": [ { "name": "p", "value": 4 } ],
              "mark": { "type": "point", "color": { "expr": "'red'" }, "size": { "expr": "p" }, "opacity": { "expr": "0.5" }, "strokeWidth": { "expr": "2" }, "fill": { "expr": "'blue'" }, "stroke": { "expr": "'green'" } },
              "encoding": { "color": { "value": { "expr": "'teal'" } }, "opacity": { "value": { "expr": "p / 8" } } }
            }
            """;

        // Act
        var (parsed, result) = Fold(json);

        // Assert
        Assert.Empty(parsed.Expressions.StaticReferences);
        Assert.Same(parsed, result.Tree);
    }

    /// <summary>
    ///     Validates that a reference that reads <c>datum</c> stays in place: a static site has no row.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_AReferenceThatReadsTheDatumStaysInPlace()
    {
        // Arrange
        const string json = """{ "title": { "text": "T", "fontSize": { "expr": "datum.size" }, "color": { "expr": "'red'" } }, "mark": "point" }""";

        // Act
        var (parsed, result) = Fold(json);

        // Assert: the font size is still the reference, the color is folded
        Assert.True(At(result.Tree.Root, "/title/fontSize").IsObject);
        Assert.Same(parsed.Root["title"]!["fontSize"], At(result.Tree.Root, "/title/fontSize"));
        Assert.Equal("red", At(result.Tree.Root, "/title/color").StringValue);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a date is folded to its milliseconds only in a scale domain, where Vega builds the domain of a time scale from numbers; at any other site the date has no form the
    ///     property can read, so the reference stays and a warning says so (2020-01-01 is 1577836800000 ms, 2020-01-02 is a day later).
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_ADateIsFoldedOnlyInADomain()
    {
        // Arrange: a time scale domain of two dates (an entry, and the whole domain), and a date as a font size
        const string json = """
            {
              "title": { "text": "T", "fontSize": { "expr": "datetime(2020, 0, 1)" } },
              "mark": "point",
              "encoding": {
                "x": { "field": "d", "type": "temporal", "scale": { "domain": [ { "expr": "datetime(2020, 0, 1)" }, { "expr": "datetime(2020, 0, 2)" } ], "domainMax": { "expr": "datetime(2020, 0, 3)" } } },
                "y": { "field": "e", "type": "temporal", "scale": { "domain": { "expr": "[datetime(2020, 0, 1), datetime(2020, 0, 2)]" } } }
              }
            }
            """;

        // Act
        var (parsed, result) = Fold(json);

        // Assert: the domains are numbers (the test evaluates in UTC)
        var start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var first = At(result.Tree.Root, "/encoding/x/scale/domain");
        Assert.Equal([start, start + 86_400_000], first.Items.Select(i => i.NumberValue));
        Assert.Equal(start + (2 * 86_400_000), At(result.Tree.Root, "/encoding/x/scale/domainMax").NumberValue);
        Assert.Equal([start, start + 86_400_000], At(result.Tree.Root, "/encoding/y/scale/domain").Items.Select(i => i.NumberValue));

        // Assert: the font size stays a reference and is reported at it
        Assert.Same(parsed.Root["title"]!["fontSize"], At(result.Tree.Root, "/title/fontSize"));
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(("vl-expr-ref", "/title/fontSize", FeatureReportKind.Warning), (warning.Id, warning.Location.Pointer, warning.Kind));
        Assert.Equal(File, warning.Location.File);
    }

    /// <summary>
    ///     Validates that each entry of a scale <c>range</c> that is an expression reference is folded (the Vega-Lite 6.5.0 schema gives the array form of <c>range</c> entries of a number, a string,
    ///     a list of numbers or an <c>ExprRef</c>, and gives the whole <c>range</c> no expression reference), and that the <c>rangeMin</c> and <c>rangeMax</c> are folded as a whole.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_FoldsTheEntriesOfAScaleRange()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "low", "value": 4 }, { "name": "color", "value": "green" } ],
              "mark": "point",
              "encoding": {
                "x": { "field": "a", "type": "quantitative", "scale": { "range": [ { "expr": "low" }, 100 ], "rangeMax": { "expr": "low * 30" } } },
                "color": { "field": "b", "type": "nominal", "scale": { "range": [ "red", { "expr": "color" } ] } }
              }
            }
            """;

        // Act
        var (_, result) = Fold(json);

        // Assert
        var root = result.Tree.Root;
        Assert.Equal([4.0, 100.0], At(root, "/encoding/x/scale/range").Items.Select(i => i.NumberValue));
        Assert.Equal(120, At(root, "/encoding/x/scale/rangeMax").NumberValue);
        Assert.Equal(["red", "green"], At(root, "/encoding/color/scale/range").Items.Select(i => i.StringValue));
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates the <c>text</c> of a title: a number or a boolean is its JavaScript text, an array is read line by line, and null stays null.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_TheTextOfATitleIsText()
    {
        // Arrange / Act
        var (_, number) = Fold("""{ "title": { "text": { "expr": "1 / 4" } }, "mark": "point" }""");
        var (_, flag) = Fold("""{ "title": { "text": { "expr": "1 < 2" } }, "mark": "point" }""");
        var (_, lines) = Fold("""{ "title": { "text": { "expr": "['a', 2]" } }, "mark": "point" }""");
        var (_, none) = Fold("""{ "title": { "text": { "expr": "null" } }, "mark": "point" }""");

        // Assert
        Assert.Equal("0.25", At(number.Tree.Root, "/title/text").StringValue);
        Assert.Equal("true", At(flag.Tree.Root, "/title/text").StringValue);
        Assert.Equal(["a", "2"], At(lines.Tree.Root, "/title/text").Items.Select(i => i.StringValue));
        Assert.True(At(none.Tree.Root, "/title/text").IsNull);
    }

    /// <summary>
    ///     Validates that every kind of value an expression can give folds to the node of that kind: a string, a boolean, null, an array and an object; and that a number that is not finite
    ///     (JSON has none) folds to null, which leaves the property unset.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_FoldsEveryKindOfValue()
    {
        // Arrange: values at sites of several owners
        const string json = """
            {
              "padding": { "expr": "1 / 0" },
              "title": { "text": "T", "angle": { "expr": "0 / 0" }, "anchor": { "expr": "'start'" }, "aria": { "expr": "!0" }, "frame": { "expr": "null" } },
              "mark": { "type": "point", "tooltip": { "expr": "{content: 'data', n: 2}" } },
              "encoding": { "x": { "field": "a", "type": "quantitative", "axis": { "values": { "expr": "[1, 2, 3]" } } } }
            }
            """;

        // Act
        var (_, result) = Fold(json);

        // Assert
        var root = result.Tree.Root;
        Assert.True(At(root, "/padding").IsNull);
        Assert.True(At(root, "/title/angle").IsNull);
        Assert.Equal("start", At(root, "/title/anchor").StringValue);
        Assert.True(At(root, "/title/aria").BoolValue);
        Assert.True(At(root, "/title/frame").IsNull);
        var tooltip = At(root, "/mark/tooltip");
        Assert.Equal(("data", 2.0), (tooltip["content"]!.StringValue, tooltip["n"]!.NumberValue));
        Assert.Equal([1.0, 2.0, 3.0], At(root, "/encoding/x/axis/values").Items.Select(i => i.NumberValue));
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that a regular expression (which no property can hold) stays a reference and is reported, in an array as well.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_AValueWithNoFormStaysAReference()
    {
        // Arrange / Act
        var (parsed, result) = Fold("""{ "title": { "text": "T", "font": { "expr": "regexp('a')" } }, "mark": "point" }""");

        // Assert
        Assert.Same(parsed.Root["title"]!["font"], At(result.Tree.Root, "/title/font"));
        Assert.Equal("/title/font", Assert.Single(result.Warnings).Location.Pointer);
    }

    /// <summary>
    ///     Validates that an expression that exceeds the step budget is a located render error at the reference.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_TheStepBudgetIsEnforcedAtTheReference()
    {
        // Arrange: the expression needs more than two steps
        const string json = """{ "title": { "text": "T", "fontSize": { "expr": "1 + 2 + 3 + 4" } }, "mark": "point" }""";

        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(() => Fold(json, null, new VegaLiteLimits { MaxEvaluationSteps = 2 }));

        // Assert
        Assert.Equal("/title/fontSize/expr", exception.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a text longer than the string limit is a located render error.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_ALongTextIsALocatedError()
    {
        // Arrange: the parameter holds a text of 50 characters, the limit is 40
        var json = """{ "params": [ { "name": "p", "value": "x" } ], "title": { "text": { "expr": "p" } }, "mark": "point" }""";
        var limits = new VegaLiteLimits { MaxStringLength = 40 };
        var overrides = new Dictionary<string, object?> { ["p"] = new string('y', 30) };

        // Act: 30 characters fit; the text that the expression builds does not
        var fits = Fold(json, overrides, limits).Result;
        var grows = Assert.Throws<VegaLiteRenderException>(() => Fold(
            """{ "params": [ { "name": "p", "value": "x" } ], "title": { "text": { "expr": "p + p" } }, "mark": "point" }""",
            overrides,
            limits));

        // Assert
        Assert.Equal(30, At(fits.Tree.Root, "/title/text").StringValue!.Length);
        Assert.Contains("MaxStringLength", grows.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the sites of the nested views: the layers, the views of a concatenation and the template of a facet take the properties of a view (title, mark, guides, scales), but only the
    ///     top-level view has a background, a padding and a configuration.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_NestedViewsFoldTheirOwnSites()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "p", "value": 7 } ],
              "background": { "expr": "'white'" },
              "hconcat": [
                { "title": { "text": "A", "fontSize": { "expr": "p" } }, "padding": { "expr": "1" }, "mark": { "type": "bar", "size": { "expr": "p + 1" } },
                  "encoding": { "x": { "field": "a", "type": "nominal", "axis": { "labelAngle": { "expr": "p * 2" } } } } },
                { "facet": { "field": "f", "header": { "labelFontSize": { "expr": "p - 1" } } },
                  "spec": { "mark": "point", "encoding": { "y": { "field": "b", "type": "quantitative", "scale": { "zero": { "expr": "false" } } } } } }
              ]
            }
            """;

        // Act
        var (_, result) = Fold(json);

        // Assert
        var root = result.Tree.Root;
        Assert.Equal("white", At(root, "/background").StringValue);
        Assert.Equal(7, At(root, "/hconcat/0/title/fontSize").NumberValue);
        Assert.Equal(8, At(root, "/hconcat/0/mark/size").NumberValue);
        Assert.Equal(14, At(root, "/hconcat/0/encoding/x/axis/labelAngle").NumberValue);
        Assert.Equal(6, At(root, "/hconcat/1/facet/header/labelFontSize").NumberValue);
        Assert.False(At(root, "/hconcat/1/spec/encoding/y/scale/zero").BoolValue);

        // Assert: the padding of a nested view is not a site (only the top-level view has one), so it stays a reference
        Assert.True(At(root, "/hconcat/0/padding").IsObject);
    }

    /// <summary>
    ///     Validates that the walk never enters the data, the datasets, the transforms and the parameters: an inline row that has an <c>expr</c> key is data, and a predicate is not a site.
    /// </summary>
    [Fact]
    public void ExpressionReferenceSites_Find_DoesNotEnterDataTransformsAndParameters()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "p", "value": 1, "expr": "2" } ],
              "datasets": { "d": [ { "expr": "1" } ] },
              "data": { "values": [ { "expr": "1", "title": { "expr": "2" } } ] },
              "transform": [ { "filter": { "field": "a", "equal": { "expr": "p" } } }, { "calculate": "p", "as": "b" } ],
              "mark": "point",
              "encoding": { "x": { "field": "a", "type": "quantitative", "condition": { "test": { "field": "a", "equal": { "expr": "p" } }, "value": 1 } } }
            }
            """;

        // Act
        var sites = ExpressionReferenceSites.Find(SpecParser.Parse(json).Root);

        // Assert
        Assert.Empty(sites);
    }

    /// <summary>
    ///     Validates the sites of the configuration: the top-level properties, the title, the axes (all the <c>axis*</c> keys), the legend, the headers, the view, the scale, the mark
    ///     configurations (a mark type, <c>mark</c> and a named style) and the parts of a composite mark; a configuration of a nested view is not read.
    /// </summary>
    [Fact]
    public void ExpressionReferenceSites_Find_ReadsTheConfiguration()
    {
        // Arrange
        const string json = """
            {
              "mark": "point",
              "config": {
                "background": { "expr": "'x'" }, "lineBreak": { "expr": "'|'" }, "padding": { "expr": "1" },
                "title": { "fontSize": { "expr": "1" } },
                "axis": { "labelFontSize": { "expr": "1" } }, "axisX": { "tickSize": { "expr": "1" } }, "axisQuantitative": { "gridWidth": { "expr": "1" } },
                "legend": { "symbolSize": { "expr": "1" } }, "header": { "labelFontSize": { "expr": "1" } }, "headerRow": { "titleFontSize": { "expr": "1" } },
                "view": { "stroke": { "expr": "'red'" } }, "scale": { "pointPadding": { "expr": "1" } },
                "mark": { "fontSize": { "expr": "1" } }, "bar": { "cornerRadius": { "expr": "1" } }, "style": { "mine": { "dx": { "expr": "1" } } },
                "boxplot": { "box": { "fill": { "expr": "'red'" } } },
                "range": { "category": { "expr": "'ignored'" } }
              }
            }
            """;

        // Act
        var pointers = ExpressionReferenceSites.Find(SpecParser.Parse(json).Root).Select(s => s.Reference.Pointer).ToHashSet();

        // Assert: everything that Vega-Lite's schema allows is a site; the named color ranges are not
        string[] expected =
        [
            "/config/background", "/config/lineBreak", "/config/padding", "/config/title/fontSize", "/config/axis/labelFontSize", "/config/axisX/tickSize", "/config/axisQuantitative/gridWidth",
            "/config/legend/symbolSize", "/config/header/labelFontSize", "/config/headerRow/titleFontSize", "/config/view/stroke", "/config/scale/pointPadding", "/config/mark/fontSize",
            "/config/bar/cornerRadius", "/config/style/mine/dx", "/config/boxplot/box/fill",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), pointers.Order(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Validates that a configuration is folded like the same properties anywhere else.
    /// </summary>
    [Fact]
    public void ExpressionReferenceFolder_Fold_FoldsTheConfiguration()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "p", "value": 9 } ],
              "mark": "point",
              "config": { "axis": { "labelFontSize": { "expr": "p" } }, "title": { "color": { "expr": "'navy'" } } }
            }
            """;

        // Act
        var (_, result) = Fold(json);

        // Assert
        Assert.Equal(9, At(result.Tree.Root, "/config/axis/labelFontSize").NumberValue);
        Assert.Equal("navy", At(result.Tree.Root, "/config/title/color").StringValue);
    }

    /// <summary>
    ///     Validates that every property of the table of sites folds at its owner: a reference to <c>7</c> becomes the number 7 (the text of the properties that take a text), at the
    ///     pointer of the reference, for every owner of the table. The paint properties of a mark that the marks evaluate for every row are not static and are the exception.
    /// </summary>
    /// <param name="ownerName">The name of the kind of object (a member of <see cref="ExpressionReferenceSites.Owner"/>).</param>
    /// <param name="property">The property.</param>
    [Theory]
    [MemberData(nameof(AllSites))]
    public void ExpressionReferenceFolder_Fold_EverySiteOfTheTableFolds(string ownerName, string property)
    {
        // Arrange: the specification that puts the reference at the property of the owner
        var owner = Enum.Parse<Owner>(ownerName);
        var (json, pointer) = Template(owner, property);

        // Act
        var (parsed, result) = Fold(json);

        // Assert: the reference is a static site and its value, 7, is in its place
        Assert.Contains(pointer, parsed.Expressions.StaticReferences.Select(r => r.Reference.Pointer));
        var folded = At(result.Tree.Root, pointer);
        if (property == "text" && owner is Owner.Title or Owner.Mark)
        {
            Assert.Equal("7", folded.StringValue);
        }
        else
        {
            Assert.Equal(7, folded.NumberValue);
        }

        Assert.Empty(result.Warnings);
    }

    /// <summary>
    ///     Validates that the paint properties of a mark that the marks evaluate for every row are not static sites of a symbol mark.
    /// </summary>
    /// <param name="property">The property.</param>
    [Theory]
    [InlineData("color")]
    [InlineData("fill")]
    [InlineData("stroke")]
    [InlineData("opacity")]
    [InlineData("strokeWidth")]
    [InlineData("size")]
    public void ExpressionReferenceSites_Find_ThePaintPropertiesOfAMarkAreRowBound(string property)
    {
        // Arrange / Act
        var parsed = SpecParser.Parse($$"""{ "mark": { "type": "point", "{{property}}": { "expr": "7" } } }""");

        // Assert: the sites include the property, the parser compiled it as a row-bound expression and does not list it as static
        Assert.Contains($"/mark/{property}", ExpressionReferenceSites.Find(parsed.Root).Select(s => s.Reference.Pointer));
        Assert.Empty(parsed.Expressions.StaticReferences);
        Assert.NotNull(parsed.Expressions.GetReference(parsed.Root["mark"]![property]));
    }

    /// <summary>
    ///     Validates that the table of sites holds exactly the properties that Vega-Lite's schema lists for each owner (the counts of the version 6 schema: 24 for a title with the
    ///     anchor, 75 for a mark, 65 for an axis, 68 for a legend, 20 for a header, 21 for a scale, 14 for the scale configuration, 12 for a view background).
    /// </summary>
    [Fact]
    public void ExpressionReferenceSites_PropertiesOf_MatchesTheSchemaCounts()
    {
        // Arrange / Act / Assert
        Assert.Equal(24, ExpressionReferenceSites.PropertiesOf(Owner.Title).Count);
        Assert.Equal(75, ExpressionReferenceSites.PropertiesOf(Owner.Mark).Count);
        Assert.Equal(65, ExpressionReferenceSites.PropertiesOf(Owner.Axis).Count);
        Assert.Equal(68, ExpressionReferenceSites.PropertiesOf(Owner.Legend).Count);
        Assert.Equal(20, ExpressionReferenceSites.PropertiesOf(Owner.Header).Count);
        Assert.Equal(21, ExpressionReferenceSites.PropertiesOf(Owner.Scale).Count);
        Assert.Equal(14, ExpressionReferenceSites.PropertiesOf(Owner.ScaleConfig).Count);
        Assert.Equal(12, ExpressionReferenceSites.PropertiesOf(Owner.ViewBackground).Count);
        Assert.Equal(["background", "padding"], ExpressionReferenceSites.PropertiesOf(Owner.RootView).Order(StringComparer.Ordinal));
        Assert.Equal(["background", "lineBreak", "padding"], ExpressionReferenceSites.PropertiesOf(Owner.Config).Order(StringComparer.Ordinal));
        Assert.Equal(["datum", "value"], ExpressionReferenceSites.PropertiesOf(Owner.ChannelDef).Order(StringComparer.Ordinal));
        Assert.Empty(ExpressionReferenceSites.PropertiesOf(Owner.Condition));
    }

    /// <summary>
    ///     Validates that the <c>value</c> and <c>datum</c> of a condition are not static (the mark evaluates them for every row) while the scale and the guides of its field are.
    /// </summary>
    [Fact]
    public void ExpressionReferenceSites_Find_AConditionHasNoStaticValue()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "p", "value": 1 } ],
              "mark": "point",
              "encoding": { "color": { "condition": { "test": "datum.a > p", "field": "a", "type": "quantitative", "scale": { "nice": { "expr": "true" } }, "value": { "expr": "'red'" } }, "value": "grey" } }
            }
            """;

        // Act
        var sites = ExpressionReferenceSites.Find(SpecParser.Parse(json).Root).Select(s => s.Reference.Pointer).ToList();

        // Assert: the nice of the scale of the condition's field is static; its value is not a site (the mark evaluates it for every row)
        Assert.Equal(["/encoding/color/condition/scale/nice"], sites);
    }

    /// <summary>
    ///     Gets every property of every owner of the table that the theory <see cref="ExpressionReferenceFolder_Fold_EverySiteOfTheTableFolds"/> checks, except the row-bound ones.
    /// </summary>
    /// <returns>The owner and property pairs.</returns>
    public static TheoryData<string, string> AllSites()
    {
        var rowBound = new HashSet<string> { "color", "fill", "stroke", "opacity", "strokeWidth", "size" };
        var data = new TheoryData<string, string>();
        foreach (var owner in Enum.GetValues<Owner>())
        {
            foreach (var property in ExpressionReferenceSites.PropertiesOf(owner).Order(StringComparer.Ordinal))
            {
                if (owner == Owner.Mark && rowBound.Contains(property))
                {
                    continue;
                }

                data.Add(owner.ToString(), property);
            }
        }

        return data;
    }

    /// <summary>
    ///     Writes the specification that holds a reference to <c>7</c> at a property of an owner.
    /// </summary>
    /// <param name="owner">The kind of object.</param>
    /// <param name="property">The property.</param>
    /// <returns>The specification and the pointer of the reference.</returns>
    private static (string Json, string Pointer) Template(Owner owner, string property)
    {
        const string reference = """{ "expr": "7" }""";
        string Put(string key) => "\"" + key + "\": " + reference;
        const string x = """{ "field": "a", "type": "quantitative", """;
        return owner switch
        {
            Owner.RootView => ("{ " + Put(property) + ", \"mark\": \"point\" }", $"/{property}"),
            Owner.Title => ("{ \"title\": { " + Put(property) + " }, \"mark\": \"point\" }", $"/title/{property}"),
            Owner.Mark => ("{ \"mark\": { \"type\": \"point\", " + Put(property) + " } }", $"/mark/{property}"),
            Owner.Axis => ("{ \"mark\": \"point\", \"encoding\": { \"x\": " + x + "\"axis\": { " + Put(property) + " } } } }", $"/encoding/x/axis/{property}"),
            Owner.Legend => ("{ \"mark\": \"point\", \"encoding\": { \"color\": { \"field\": \"a\", \"type\": \"nominal\", \"legend\": { " + Put(property) + " } } } }", $"/encoding/color/legend/{property}"),
            Owner.Header => ("{ \"mark\": \"point\", \"encoding\": { \"row\": { \"field\": \"a\", \"type\": \"nominal\", \"header\": { " + Put(property) + " } } } }", $"/encoding/row/header/{property}"),
            Owner.Scale => ("{ \"mark\": \"point\", \"encoding\": { \"x\": " + x + "\"scale\": { " + Put(property) + " } } } }", $"/encoding/x/scale/{property}"),
            Owner.ScaleConfig => ("{ \"mark\": \"point\", \"config\": { \"scale\": { " + Put(property) + " } } }", $"/config/scale/{property}"),
            Owner.ViewBackground => ("{ \"mark\": \"point\", \"view\": { " + Put(property) + " } }", $"/view/{property}"),
            Owner.Config => ("{ \"mark\": \"point\", \"config\": { " + Put(property) + " } }", $"/config/{property}"),
            Owner.ChannelDef => ("{ \"mark\": \"point\", \"encoding\": { \"angle\": { " + Put(property) + " } } }", $"/encoding/angle/{property}"),
            _ => throw new ArgumentOutOfRangeException(nameof(owner)),
        };
    }
}

// cspell:ignore nosuchproperty
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests for <see cref="ConfigResolver"/>, <see cref="ResolvedConfig"/> and <see cref="ConfigNode"/>: the
///     Vega-Lite version 6 defaults, the precedence of specification and caller configuration, and the lookups.
/// </summary>
public class ConfigResolverTests
{
    /// <summary>
    ///     Resolves the configuration of a specification that has the given <c>config</c> text.
    /// </summary>
    /// <param name="specConfig">The JSON text of the <c>config</c> object, or null for a specification without one.</param>
    /// <param name="caller">The caller's configuration text, or null.</param>
    /// <param name="schema">The <c>$schema</c> to put in the specification, or null.</param>
    /// <returns>The resolved configuration.</returns>
    private static ResolvedConfig Resolve(string? specConfig, string? caller = null, string? schema = null)
    {
        var parts = new List<string>();
        if (schema is not null)
        {
            parts.Add("\"$schema\":\"" + schema + "\"");
        }

        if (specConfig is not null)
        {
            parts.Add("\"config\":" + specConfig);
        }

        return ConfigResolver.Resolve(SpecParser.Parse("{" + string.Join(",", parts) + "}"), caller);
    }

    /// <summary>
    ///     Flattens a configuration tree to sorted <c>path=value</c> lines so that two trees can be compared.
    /// </summary>
    /// <param name="node">The tree.</param>
    /// <param name="path">The path of the node.</param>
    /// <returns>The lines.</returns>
    private static IEnumerable<string> Flatten(ConfigNode node, string path = "")
    {
        switch (node.Kind)
        {
            case ConfigNodeKind.Object:
                return node.Properties.OrderBy(p => p.Key, StringComparer.Ordinal).SelectMany(p => Flatten(p.Value, path + "/" + p.Key)).DefaultIfEmpty(path + "={}");
            case ConfigNodeKind.Array:
                return node.Items.SelectMany((item, i) => Flatten(item, path + "[" + i + "]")).DefaultIfEmpty(path + "=[]");
            case ConfigNodeKind.String:
                return [path + "=\"" + node.StringValue + "\""];
            case ConfigNodeKind.Number:
                return [path + "=" + node.NumberValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)];
            case ConfigNodeKind.Boolean:
                return [path + "=" + (node.BoolValue ? "true" : "false")];
            default:
                return [path + "=null"];
        }
    }

    /// <summary>
    ///     Validates the Vega-Lite version 6 default values that later stages rely on (pinned, so that a change of the
    ///     table is a deliberate decision), and that the specification's configuration overrides them without losing the
    ///     rest of the defaults.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_VegaLiteV6DefaultsAndOverrides()
    {
        // Arrange: no configuration at all, then a specification with its own configuration
        var defaults = Resolve(null);
        var overridden = Resolve(
            "{\"view\":{\"continuousWidth\":400},\"axis\":{\"gridColor\":\"#eee\",\"grid\":true},\"mark\":{\"color\":\"red\"},"
            + "\"title\":{\"fontSize\":20,\"color\":\"navy\",\"subtitleFontSize\":15,\"offset\":10},\"style\":{\"guide-label\":{\"fontSize\":12}},"
            + "\"scale\":{\"pointPadding\":0.25}}");

        // Assert: the top-level defaults. Sources: Vega-Lite 6.5.0 src/config.ts defaultConfig (background white, padding 5)
        // and Vega vega-parser src/config.js (autosize pad)
        Assert.Equal("white", defaults.GetString("background"));
        Assert.Equal(5, defaults.GetNumber("padding"));
        Assert.Equal("pad", defaults.GetString("autosize"));
        Assert.Equal("%b %d, %Y", defaults.GetString("timeFormat"));
        Assert.Equal("Count of Records", defaults.GetString("countTitle"));
        Assert.Equal(".0%", defaults.GetString("normalizedNumberFormat"));

        // Assert: the view. Source: Vega-Lite 6.5.0 src/config.ts defaultViewConfig (300, 300, step 20 = DEFAULT_STEP);
        // note that Vega-Lite 5 used 200. The cell style is Vega config.js (transparent fill, #ddd stroke)
        Assert.Equal(300, defaults.GetNumber("view.continuousWidth"));
        Assert.Equal(300, defaults.GetNumber("view.continuousHeight"));
        Assert.Equal(20, defaults.GetNumber("view.step"));
        Assert.Equal(ConfigResolver.DefaultStep, defaults.GetNumber("view.step"));
        Assert.Equal("#ddd", defaults.ResolveViewProperty("stroke")!.StringValue);
        Assert.Equal("transparent", defaults.ResolveViewProperty("fill")!.StringValue);
        Assert.Null(defaults.ResolveViewProperty("strokeWidth"));

        // Assert: the title. Source: Vega config.js title (orient top, anchor middle, offset 4, subtitlePadding 3) and the
        // group-title and group-subtitle styles (#000, sans-serif, 13 bold and 12); Vega-Lite's title configuration is empty
        Assert.Equal("top", defaults.ResolveTitleProperty("orient")!.StringValue);
        Assert.Equal("middle", defaults.ResolveTitleProperty("anchor")!.StringValue);
        Assert.Equal(4, defaults.ResolveTitleProperty("offset")!.NumberValue);
        Assert.Equal(3, defaults.ResolveTitleProperty("subtitlePadding")!.NumberValue);
        Assert.Equal("#000", defaults.ResolveTitleProperty("fill")!.StringValue);
        Assert.Equal("sans-serif", defaults.ResolveTitleProperty("font")!.StringValue);
        Assert.Equal(13, defaults.ResolveTitleProperty("fontSize")!.NumberValue);
        Assert.Equal("bold", defaults.ResolveTitleProperty("fontWeight")!.StringValue);
        Assert.Equal("#000", defaults.ResolveTitleProperty("fill", subtitle: true)!.StringValue);
        Assert.Equal(12, defaults.ResolveTitleProperty("fontSize", subtitle: true)!.NumberValue);
        Assert.Null(defaults.ResolveTitleProperty("fontWeight", subtitle: true));
        Assert.Null(defaults.ResolveTitleProperty("frame"));

        // Assert: the axis. Source: Vega config.js axis and axisBand
        Assert.Equal(0, defaults.GetNumber("axis.minExtent"));
        Assert.Equal(200, defaults.GetNumber("axis.maxExtent"));
        Assert.Equal(0.5, defaults.GetNumber("axis.bandPosition"));
        Assert.True(defaults.GetBool("axis.domain"));
        Assert.Equal(1, defaults.GetNumber("axis.domainWidth"));
        Assert.Equal("#888", defaults.GetString("axis.domainColor"));
        Assert.False(defaults.GetBool("axis.grid"));
        Assert.Equal(1, defaults.GetNumber("axis.gridWidth"));
        Assert.Equal("#ddd", defaults.GetString("axis.gridColor"));
        Assert.True(defaults.GetBool("axis.labels"));
        Assert.Equal(0, defaults.GetNumber("axis.labelAngle"));
        Assert.Equal(180, defaults.GetNumber("axis.labelLimit"));
        Assert.Equal(0, defaults.GetNumber("axis.labelOffset"));
        Assert.Equal(2, defaults.GetNumber("axis.labelPadding"));
        Assert.True(defaults.GetBool("axis.ticks"));
        Assert.Equal("#888", defaults.GetString("axis.tickColor"));
        Assert.Equal(0, defaults.GetNumber("axis.tickOffset"));
        Assert.True(defaults.GetBool("axis.tickRound"));
        Assert.Equal(5, defaults.GetNumber("axis.tickSize"));
        Assert.Equal(1, defaults.GetNumber("axis.tickWidth"));
        Assert.Equal(4, defaults.GetNumber("axis.titlePadding"));
        Assert.Equal(-0.5, defaults.GetNumber("axisBand.tickOffset"));
        Assert.Equal(10, defaults.ResolveStyleProperty(["guide-label"], "fontSize")!.NumberValue);
        Assert.Equal(11, defaults.ResolveStyleProperty(["guide-title"], "fontSize")!.NumberValue);
        Assert.Equal("bold", defaults.ResolveStyleProperty(["guide-title"], "fontWeight")!.StringValue);
        Assert.Equal("#000", defaults.ResolveStyleProperty(["guide-label"], "fill")!.StringValue);

        // Assert: the marks. Source: Vega-Lite 6.5.0 src/mark.ts defaultMarkConfig, defaultRectConfig, defaultBarConfig,
        // defaultTickConfig and src/config.ts (rule and text color black); Vega config.js styles point, circle, square
        Assert.Equal("#4c78a8", defaults.GetString("mark.color"));
        Assert.Equal("break-paths-show-path-domains", defaults.GetString("mark.invalid"));
        Assert.Equal(1, defaults.GetNumber("mark.timeUnitBandSize"));
        Assert.Equal(1, defaults.GetNumber("bar.binSpacing"));
        Assert.Equal(5, defaults.GetNumber("bar.continuousBandSize"));
        Assert.Equal(0.25, defaults.GetNumber("bar.minBandSize"));
        Assert.Equal(0.5, defaults.GetNumber("bar.timeUnitBandPosition"));
        Assert.Equal(0, defaults.GetNumber("rect.binSpacing"));
        Assert.Equal(5, defaults.GetNumber("rect.continuousBandSize"));
        Assert.Equal(0, defaults.GetNumber("tick.binSpacing"));
        Assert.Equal(1, defaults.GetNumber("tick.thickness"));
        Assert.Equal(5, defaults.GetNumber("tick.continuousBandSize"));
        Assert.Equal("black", defaults.GetString("rule.color"));
        Assert.Equal("black", defaults.GetString("text.color"));
        Assert.Equal(30, defaults.ResolveMarkProperty("point", null, "size")!.NumberValue);
        Assert.Equal(2, defaults.ResolveMarkProperty("point", null, "strokeWidth")!.NumberValue);
        Assert.Equal("circle", defaults.ResolveMarkProperty("point", null, "shape")!.StringValue);
        Assert.Equal(30, defaults.ResolveMarkProperty("circle", null, "size")!.NumberValue);
        Assert.Equal("square", defaults.ResolveMarkProperty("square", null, "shape")!.StringValue);
        Assert.Equal("#4c78a8", defaults.ResolveMarkProperty("bar", null, "color")!.StringValue);
        Assert.Equal("black", defaults.ResolveMarkProperty("rule", null, "color")!.StringValue);
        Assert.Equal(64, defaults.ResolveMarkProperty("point", null, "nosuchproperty")?.NumberValue ?? 64);

        // Assert: the scale configuration. Source: Vega-Lite 6.5.0 src/scale.ts defaultScaleConfig
        Assert.Equal(0.5, defaults.GetNumber("scale.pointPadding"));
        Assert.Equal(0.1, defaults.GetNumber("scale.barBandPaddingInner"));
        Assert.Equal(0, defaults.GetNumber("scale.rectBandPaddingInner"));
        Assert.Equal(0.25, defaults.GetNumber("scale.tickBandPaddingInner"));
        Assert.Equal(0.2, defaults.GetNumber("scale.bandWithNestedOffsetPaddingInner"));
        Assert.Equal(0.2, defaults.GetNumber("scale.bandWithNestedOffsetPaddingOuter"));
        Assert.Equal(2, defaults.GetNumber("scale.minBandSize"));
        Assert.Equal(8, defaults.GetNumber("scale.minFontSize"));
        Assert.Equal(40, defaults.GetNumber("scale.maxFontSize"));
        Assert.Equal(0.3, defaults.GetNumber("scale.minOpacity"));
        Assert.Equal(0.8, defaults.GetNumber("scale.maxOpacity"));
        Assert.Equal(4, defaults.GetNumber("scale.minSize"));
        Assert.Equal(1, defaults.GetNumber("scale.minStrokeWidth"));
        Assert.Equal(4, defaults.GetNumber("scale.maxStrokeWidth"));
        Assert.Equal(4, defaults.GetNumber("scale.quantileCount"));
        Assert.Equal(4, defaults.GetNumber("scale.quantizeCount"));
        Assert.True(defaults.GetBool("scale.zero"));
        Assert.Null(defaults.Get("scale.nice"));
        Assert.Null(defaults.Get("scale.continuousPadding"));

        // Assert: the default palette. Source: Vega config.js range.category scheme tableau10
        Assert.Equal("tableau10", defaults.GetString("range.category.scheme"));
        Assert.Equal("blues", defaults.GetString("range.ordinal.scheme"));
        Assert.True(defaults.TryGetArray("range.symbol", out var symbols));
        Assert.Equal(["circle", "square", "triangle-up", "cross", "diamond", "triangle-right", "triangle-down", "triangle-left"], symbols.Select(s => s.StringValue));

        // Assert: the rules that live in code
        Assert.Equal(0.7, ConfigResolver.DefaultReducedOpacity);
        Assert.Equal(1, ConfigResolver.DefaultStrokeWidth);
        Assert.Equal("6.5.0", ConfigResolver.VegaLiteDefaultsVersion);

        // Assert: the specification's configuration overrides what it names, at any depth, and keeps every other default
        Assert.Equal(400, overridden.GetNumber("view.continuousWidth"));
        Assert.Equal(300, overridden.GetNumber("view.continuousHeight"));
        Assert.Equal(20, overridden.GetNumber("view.step"));
        Assert.Equal("#eee", overridden.GetString("axis.gridColor"));
        Assert.True(overridden.GetBool("axis.grid"));
        Assert.Equal("#888", overridden.GetString("axis.domainColor"));
        Assert.Equal("red", overridden.GetString("mark.color"));
        Assert.Equal("red", overridden.ResolveMarkProperty("bar", null, "color")!.StringValue);
        Assert.Equal(0.25, overridden.GetNumber("scale.pointPadding"));
        Assert.Equal(0.1, overridden.GetNumber("scale.barBandPaddingInner"));

        // Assert: title configuration wins over the group-title style; its color spelling maps to fill; offset is a plain value
        Assert.Equal(20, overridden.ResolveTitleProperty("fontSize")!.NumberValue);
        Assert.Equal("navy", overridden.ResolveTitleProperty("fill")!.StringValue);
        Assert.Equal(15, overridden.ResolveTitleProperty("fontSize", subtitle: true)!.NumberValue);
        Assert.Equal(10, overridden.ResolveTitleProperty("offset")!.NumberValue);
        Assert.Equal("bold", overridden.ResolveTitleProperty("fontWeight")!.StringValue);

        // Assert: a user style merges into the default style of the same name property by property
        Assert.Equal(12, overridden.ResolveStyleProperty(["guide-label"], "fontSize")!.NumberValue);
        Assert.Equal("sans-serif", overridden.ResolveStyleProperty(["guide-label"], "font")!.StringValue);
    }

    /// <summary>
    ///     Validates that a schema of version 5 (and 6, and none) is read with the version 6 defaults: the effective
    ///     configuration does not depend on the schema.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_SchemaV5UsesV6Defaults()
    {
        // Arrange
        const string v5 = "https://vega.github.io/schema/vega-lite/v5.json";
        const string v6 = "https://vega.github.io/schema/vega-lite/v6.json";

        // Act
        var fromV5 = Resolve(null, null, v5);
        var fromV6 = Resolve(null, null, v6);
        var fromNone = Resolve(null);
        var withConfig = Resolve("{\"view\":{\"continuousHeight\":111}}", null, v5);

        // Assert: identical trees, and the Vega-Lite 6 view size (not the 200 of version 5)
        Assert.Equal(Flatten(fromV6.Root), Flatten(fromV5.Root));
        Assert.Equal(Flatten(fromNone.Root), Flatten(fromV5.Root));
        Assert.Equal(300, fromV5.GetNumber("view.continuousWidth"));
        Assert.Equal(300, fromV5.GetNumber("view.continuousHeight"));
        Assert.Equal("white", fromV5.GetString("background"));
        Assert.Same(ConfigResolver.Defaults, fromNone);
        Assert.Equal(111, withConfig.GetNumber("view.continuousHeight"));
        Assert.Equal(300, withConfig.GetNumber("view.continuousWidth"));
    }

    /// <summary>
    ///     Validates the precedence and merge rules: defaults, then the specification's configuration, then the
    ///     caller's; objects merge at any depth and arrays are replaced.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_CallerConfigOverridesSpecConfig()
    {
        // Arrange
        const string spec = "{\"view\":{\"continuousWidth\":400,\"continuousHeight\":250},\"range\":{\"category\":[\"red\",\"green\"]},"
            + "\"axis\":{\"domainColor\":\"blue\",\"tickSize\":9},\"style\":{\"guide-title\":{\"fontSize\":14}}}";
        const string caller = "{\"view\":{\"continuousWidth\":500},\"range\":{\"category\":[\"black\"]},\"background\":\"yellow\","
            + "\"axis\":{\"tickSize\":7},\"style\":{\"guide-title\":{\"fontWeight\":\"normal\"}}}";

        // Act
        var onlySpec = Resolve(spec);
        var both = Resolve(spec, caller);
        var onlyCaller = Resolve(null, caller);

        // Assert: the caller wins where both define a value, the specification fills in the rest, the defaults the remainder
        Assert.Equal(400, onlySpec.GetNumber("view.continuousWidth"));
        Assert.Equal(500, both.GetNumber("view.continuousWidth"));
        Assert.Equal(250, both.GetNumber("view.continuousHeight"));
        Assert.Equal(20, both.GetNumber("view.step"));
        Assert.Equal("yellow", both.GetString("background"));
        Assert.Equal("white", onlySpec.GetString("background"));
        Assert.Equal("blue", both.GetString("axis.domainColor"));
        Assert.Equal(7, both.GetNumber("axis.tickSize"));
        Assert.Equal(9, onlySpec.GetNumber("axis.tickSize"));
        Assert.Equal("#888", both.GetString("axis.tickColor"));
        Assert.Equal(300, onlyCaller.GetNumber("view.continuousHeight"));
        Assert.Equal(500, onlyCaller.GetNumber("view.continuousWidth"));

        // Assert: arrays are replaced as a whole, not merged element by element
        Assert.True(both.TryGetArray("range.category", out var palette));
        Assert.Equal(["black"], palette.Select(p => p.StringValue));
        Assert.True(onlySpec.TryGetArray("range.category", out var specPalette));
        Assert.Equal(["red", "green"], specPalette.Select(p => p.StringValue));
        Assert.False(ConfigResolver.Defaults.TryGetArray("range.category", out _));

        // Assert: nested objects merge from all three sources
        Assert.Equal(14, both.ResolveStyleProperty(["guide-title"], "fontSize")!.NumberValue);
        Assert.Equal("normal", both.ResolveStyleProperty(["guide-title"], "fontWeight")!.StringValue);
        Assert.Equal("sans-serif", both.ResolveStyleProperty(["guide-title"], "font")!.StringValue);

        // Assert: resolving never modifies the shared defaults
        Assert.Equal(300, ConfigResolver.Defaults.GetNumber("view.continuousWidth"));
        Assert.Equal("tableau10", ConfigResolver.Defaults.GetString("range.category.scheme"));
    }

    /// <summary>
    ///     Validates that a caller configuration that is not a JSON object is an argument error.
    /// </summary>
    /// <param name="caller">The configuration text.</param>
    [Theory]
    [InlineData("[]")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("{bad")]
    [InlineData("")]
    public void ConfigResolver_Resolve_InvalidCallerConfigThrowsArgumentException(string caller)
    {
        // Arrange
        var tree = SpecParser.Parse("{}");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ConfigResolver.Resolve(tree, caller));
    }

    /// <summary>
    ///     Validates that a caller configuration nested deeper than the option limit is an argument error.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_TooDeepCallerConfigThrowsArgumentException()
    {
        // Arrange: 70 levels, beyond the limit of 64
        var deep = string.Concat(Enumerable.Repeat("{\"a\":", 70)) + "1" + new string('}', 70);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ConfigResolver.Resolve(SpecParser.Parse("{}"), deep));
        Assert.Throws<ArgumentNullException>(() => ConfigResolver.Resolve((SpecTree)null!, null));
    }

    /// <summary>
    ///     Validates the mark property precedence: style names, then the mark-specific configuration, then the general
    ///     mark configuration, then the Vega defaults.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_MarkPropertiesFollowTheStylePrecedenceChain()
    {
        // Arrange: each level defines the size differently for bar; the point has only the general level
        var config = Resolve(
            "{\"mark\":{\"size\":1,\"opacity\":0.9},\"bar\":{\"size\":2,\"cornerRadius\":4},\"style\":{\"bar\":{\"size\":3},\"first\":{\"size\":4},\"second\":{\"size\":5,\"fill\":\"green\"}}}");

        // Act / Assert: the style named like the mark beats the mark-specific configuration, which beats the general one
        Assert.Equal(3, config.ResolveMarkProperty("bar", null, "size")!.NumberValue);
        Assert.Equal(4, config.ResolveMarkProperty("bar", ["first"], "size")!.NumberValue);
        Assert.Equal(5, config.ResolveMarkProperty("bar", ["first", "second"], "size")!.NumberValue);
        Assert.Equal(4, config.ResolveMarkProperty("bar", ["second", "first"], "size")!.NumberValue);
        Assert.Equal("green", config.ResolveMarkProperty("bar", ["first", "second"], "fill")!.StringValue);

        // Assert: a property that a level does not define falls through to the next level
        Assert.Equal(4, config.ResolveMarkProperty("bar", null, "cornerRadius")!.NumberValue);
        Assert.Equal(0.9, config.ResolveMarkProperty("bar", null, "opacity")!.NumberValue);
        Assert.Equal(1, config.ResolveMarkProperty("point", null, "size")!.NumberValue);
        Assert.Equal(1, config.ResolveMarkProperty("circle", null, "size")!.NumberValue);

        // Assert: the Vega default styles are the last resort, below every Vega-Lite level
        var plain = Resolve("{}");
        Assert.Equal(30, plain.ResolveMarkProperty("circle", null, "size")!.NumberValue);
        Assert.Equal(64, plain.Vega["symbol"]!["size"]!.NumberValue);
        Assert.Equal("#4c78a8", plain.Vega["rect"]!["fill"]!.StringValue);
        Assert.Equal("#000", plain.Vega["rule"]!["stroke"]!.StringValue);
        Assert.Null(plain.ResolveMarkProperty("tick", null, "size"));
        Assert.Equal("rect", ConfigResolver.VegaMarkType("bar"));
        Assert.Equal("rect", ConfigResolver.VegaMarkType("tick"));
        Assert.Equal("rect", ConfigResolver.VegaMarkType("rect"));
        Assert.Equal("symbol", ConfigResolver.VegaMarkType("square"));
        Assert.Equal("rule", ConfigResolver.VegaMarkType("rule"));
    }

    /// <summary>
    ///     Validates that the Vega mark-type defaults do not leak into the merged tree, where Vega-Lite uses the same
    ///     names for its own mark configuration.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_VegaMarkDefaultsStayOutOfTheMergedTree()
    {
        // Arrange / Act
        var config = ConfigResolver.Defaults;

        // Assert: config.rect is Vega-Lite's rectangle configuration (no fill), not Vega's rect mark default
        Assert.Null(config.Get("rect.fill"));
        Assert.Equal(5, config.GetNumber("rect.continuousBandSize"));
        Assert.Null(config.Get("symbol"));
        Assert.Null(config.Get("text.fontSize"));
        Assert.Equal("#4c78a8", config.Vega["arc"]!["fill"]!.StringValue);
    }

    /// <summary>
    ///     Validates the view property precedence: <c>style.cell</c>, then <c>view</c>, then the default cell style.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_ViewPropertiesFollowTheCellStyleChain()
    {
        // Arrange
        var viewOnly = Resolve("{\"view\":{\"stroke\":\"red\",\"fill\":\"#fafafa\"}}");
        var both = Resolve("{\"view\":{\"stroke\":\"red\"},\"style\":{\"cell\":{\"stroke\":\"blue\"}}}");
        var none = Resolve("{\"view\":{\"stroke\":null}}");

        // Act / Assert
        Assert.Equal("red", viewOnly.ResolveViewProperty("stroke")!.StringValue);
        Assert.Equal("#fafafa", viewOnly.ResolveViewProperty("fill")!.StringValue);
        Assert.Equal("blue", both.ResolveViewProperty("stroke")!.StringValue);
        Assert.Equal("transparent", both.ResolveViewProperty("fill")!.StringValue);
        Assert.Equal(ConfigNodeKind.Null, none.ResolveViewProperty("stroke")!.Kind);
        Assert.Equal(300, both.ResolveViewProperty("continuousWidth")!.NumberValue);
    }

    /// <summary>
    ///     Validates the <c>font</c> shorthand: it fills the font of the text and guide styles below the explicit
    ///     settings.
    /// </summary>
    [Fact]
    public void ConfigResolver_Resolve_FontShorthandAppliesToTextAndGuideStyles()
    {
        // Arrange: a font for everything, but an explicit font for guide titles
        var config = Resolve("{\"font\":\"Georgia, serif\",\"style\":{\"guide-title\":{\"font\":\"Courier\"}}}");

        // Act / Assert
        Assert.Equal("Georgia, serif", config.ResolveTitleProperty("font")!.StringValue);
        Assert.Equal("Georgia, serif", config.ResolveTitleProperty("font", subtitle: true)!.StringValue);
        Assert.Equal("Georgia, serif", config.ResolveStyleProperty(["guide-label"], "font")!.StringValue);
        Assert.Equal("Courier", config.ResolveStyleProperty(["guide-title"], "font")!.StringValue);
        Assert.Equal("Georgia, serif", config.GetString("text.font"));
        Assert.Equal("Georgia, serif", config.GetString("font"));
        Assert.Equal(13, config.ResolveTitleProperty("fontSize")!.NumberValue);
    }

    /// <summary>
    ///     Validates the typed accessors, the dotted path lookup and the object and array getters.
    /// </summary>
    [Fact]
    public void ResolvedConfig_Accessors_ReadTypedValuesByDottedPath()
    {
        // Arrange
        var config = Resolve("{\"custom\":{\"n\":1.5,\"s\":\"t\",\"b\":true,\"z\":null,\"list\":[1,\"two\"]}}");

        // Act / Assert: typed getters return null on a kind mismatch or a missing path
        Assert.Equal(1.5, config.GetNumber("custom.n"));
        Assert.Null(config.GetNumber("custom.s"));
        Assert.Equal("t", config.GetString("custom.s"));
        Assert.Null(config.GetString("custom.n"));
        Assert.True(config.GetBool("custom.b"));
        Assert.Null(config.GetBool("custom.n"));
        Assert.Null(config.GetNumber("custom.missing"));
        Assert.Null(config.GetNumber("missing.deeper.still"));
        Assert.Null(config.Get("custom.n.deeper"));
        Assert.Equal(ConfigNodeKind.Null, config.Get("custom.z")!.Kind);

        // Act / Assert: object and array getters
        Assert.True(config.TryGetObject("custom", out var custom));
        Assert.Equal(["n", "s", "b", "z", "list"], custom.Properties.Keys);
        Assert.False(config.TryGetObject("custom.n", out var notObject));
        Assert.Empty(notObject.Properties);
        Assert.True(config.TryGetArray("custom.list", out var list));
        Assert.Equal(2, list.Count);
        Assert.Equal("two", list[1].StringValue);
        Assert.False(config.TryGetArray("custom.n", out var notArray));
        Assert.Empty(notArray);
        Assert.Throws<ArgumentNullException>(() => config.Get(null!));
    }

    /// <summary>
    ///     Validates the deep merge of configuration nodes: objects merge, everything else is replaced, and the inputs
    ///     are not modified.
    /// </summary>
    [Fact]
    public void ConfigNode_Merge_MergesObjectsAndReplacesEverythingElse()
    {
        // Arrange
        var lowRoot = ConfigNode.FromSpec(SpecParser.Parse("{\"a\":{\"x\":1,\"y\":2},\"list\":[1,2],\"s\":\"low\",\"o\":{\"k\":1}}").Root);
        var high = ConfigNode.FromSpec(SpecParser.Parse("{\"a\":{\"y\":3,\"z\":4},\"list\":[9],\"s\":{\"now\":\"object\"},\"o\":5}").Root);

        // Act
        var merged = ConfigNode.Merge(lowRoot, high);

        // Assert
        Assert.Equal(1, merged.Get("a.x")!.NumberValue);
        Assert.Equal(3, merged.Get("a.y")!.NumberValue);
        Assert.Equal(4, merged.Get("a.z")!.NumberValue);
        Assert.Equal([9d], merged.Get("list")!.Items.Select(i => i.NumberValue));
        Assert.Equal("object", merged.Get("s.now")!.StringValue);
        Assert.Equal(5, merged.Get("o")!.NumberValue);

        // Assert: the inputs are unchanged, and merging two objects with nothing in them gives an empty object
        Assert.Equal(2, lowRoot.Get("a.y")!.NumberValue);
        Assert.Equal([1d, 2d], lowRoot.Get("list")!.Items.Select(i => i.NumberValue));
        Assert.Equal("low", lowRoot.Get("s")!.StringValue);
        Assert.Empty(ConfigNode.Merge(ConfigNode.EmptyObject, ConfigNode.EmptyObject).Properties);
    }

    /// <summary>
    ///     Validates which mark types are filled by default.
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="filled">Whether the mark is filled by default.</param>
    [Theory]
    [InlineData("bar", true)]
    [InlineData("rect", true)]
    [InlineData("circle", true)]
    [InlineData("square", true)]
    [InlineData("tick", true)]
    [InlineData("text", true)]
    [InlineData("area", true)]
    [InlineData("point", false)]
    [InlineData("line", false)]
    [InlineData("rule", false)]
    public void ConfigResolver_IsFilledByDefault_PointLineAndRuleAreOutlines(string markType, bool filled)
    {
        // Arrange / Act
        var result = ConfigResolver.IsFilledByDefault(markType);

        // Assert
        Assert.Equal(filled, result);
    }
}

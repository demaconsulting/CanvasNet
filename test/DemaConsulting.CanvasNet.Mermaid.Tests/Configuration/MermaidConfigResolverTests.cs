using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

/// <summary>
///     Unit tests of <see cref="MermaidConfigResolver"/>: precedence, secure keys, unknown keys, values of the wrong type,
///     directive JSON, theme recomputation and the located reports.
/// </summary>
public class MermaidConfigResolverTests
{
    /// <summary>A flowchart body that follows the configuration of a test.</summary>
    private const string Flow = "flowchart LR\n  A --> B\n";

    /// <summary>
    ///     Validates the precedence of the theme: the defaults, then the theme option, then the site configuration, then
    ///     the frontmatter, then the directives.
    /// </summary>
    /// <param name="option">The theme option, or null.</param>
    /// <param name="site">The theme of the site configuration, or null.</param>
    /// <param name="frontmatter">The theme of the frontmatter, or null.</param>
    /// <param name="directive">The theme of a directive, or null.</param>
    /// <param name="expected">The effective theme.</param>
    [Theory]
    [InlineData(null, null, null, null, "default")]
    [InlineData("dark", null, null, null, "dark")]
    [InlineData("dark", "forest", null, null, "forest")]
    [InlineData("dark", "forest", "neutral", null, "neutral")]
    [InlineData("dark", "forest", "neutral", "base", "base")]
    [InlineData(null, null, "neutral", "base", "base")]
    public void MermaidConfigResolver_Resolve_ThemePrecedence(string? option, string? site, string? frontmatter, string? directive, string expected)
    {
        // Arrange
        var text = (frontmatter is null ? string.Empty : $"---\nconfig:\n  theme: {frontmatter}\n---\n") +
                   (directive is null ? string.Empty : $"%%{{init: {{'theme': '{directive}'}}}}%%\n") + Flow;
        var options = new MermaidRenderOptions { Theme = option, Config = site is null ? null : $"{{\"theme\": \"{site}\"}}" };

        // Act
        var (config, warnings) = Resolve(text, options);

        // Assert
        Assert.Empty(warnings);
        Assert.Equal(expected, config.Theme);
        Assert.Equal((string?)ThemeResolver.GetThemeVariables(expected, null)["mainBkg"], config.ThemeVariable("mainBkg"));
    }

    /// <summary>
    ///     Validates that the frontmatter cannot set a secure key: the change is ignored and reported at the key.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_SecureKeyInFrontmatter_IgnoredAndLocated()
    {
        // Arrange
        const string Text = "---\nconfig:\n  securityLevel: loose\n  maxEdges: 2\n---\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Equal("strict", config.SecurityLevel);
        Assert.Equal(500, config.MaxEdges);
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigSecureKey, warning.Code));
        Assert.Equal(new MermaidLocation("t.mmd", 3, 3), warnings[0].Location);
        Assert.Equal(new MermaidLocation("t.mmd", 4, 3), warnings[1].Location);
    }

    /// <summary>
    ///     Validates that a directive cannot set a secure key, nor one that the site configuration made secure.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_SecureKeysOfSite_IgnoredInDirective()
    {
        // Arrange
        const string Text = "%%{init: {\"theme\": \"dark\", \"maxTextSize\": 10}}%%\n" + Flow;
        var options = new MermaidRenderOptions { Config = "{\"secure\": [\"theme\"], \"maxEdges\": 10}" };

        // Act
        var (config, warnings) = Resolve(Text, options);

        // Assert
        Assert.Equal("default", config.Theme);
        Assert.Equal(10, config.MaxEdges);
        Assert.Equal(50000, config.MaxTextSize);
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal((MermaidDiagnosticCodes.ConfigSecureKey, new MermaidLocation("t.mmd", 1, 1)), (warning.Code, warning.Location)));
    }

    /// <summary>
    ///     Validates that an unknown key is reported with the nearest known key and has no effect.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_UnknownKey_ReportedWithHint()
    {
        // Arrange
        const string Text = "---\nconfig:\n  flowchart:\n    nodeSpaces: 10\n---\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.ConfigUnknownKey, warning.Code);
        Assert.Equal(new MermaidLocation("t.mmd", 4, 5), warning.Location);
        Assert.Equal("did you mean 'nodeSpacing'?", warning.Hint);
        Assert.Contains("flowchart.nodeSpaces", warning.Message, StringComparison.Ordinal);
        Assert.Equal(50, config.Flowchart.NodeSpacing);
    }

    /// <summary>
    ///     Validates that a value of the wrong type is reported and dropped, so the value of the lower layer is kept.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_WrongType_KeepsLowerLayer()
    {
        // Arrange
        const string Text = "---\nconfig:\n  flowchart:\n    nodeSpacing: wide\n    htmlLabels: 1\n---\n" + Flow;
        var options = new MermaidRenderOptions { Config = "{\"flowchart\": {\"nodeSpacing\": 70}}" };

        // Act
        var (config, warnings) = Resolve(Text, options);

        // Assert
        Assert.Equal(70, config.Flowchart.NodeSpacing);
        Assert.Null(config.Flowchart.HtmlLabels);
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code));
        Assert.Equal(new MermaidLocation("t.mmd", 4, 5), warnings[0].Location);
    }

    /// <summary>
    ///     Validates the directive JSON with Mermaid's quote replacement and the move of the <c>config</c> key to the
    ///     flowchart section.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_DirectiveSingleQuotesAndConfigKey()
    {
        // Arrange
        const string Text = "%%{init: {'theme':'dark', 'config': {'curve': 'linear', 'nodeSpacing': 11}}}%%\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Empty(warnings);
        Assert.Equal("dark", config.Theme);
        Assert.Equal("linear", config.Flowchart.Curve);
        Assert.Equal(11, config.Flowchart.NodeSpacing);
    }

    /// <summary>
    ///     Validates that invalid JSON in one init directive is reported and makes every init directive ignored, as in
    ///     Mermaid.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_InvalidJson_IgnoresAllInitDirectives()
    {
        // Arrange
        const string Text = "%%{init: {\"theme\": \"dark\"}}%%\n%%{init: {theme: forest}}%%\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Equal("default", config.Theme);
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, new MermaidLocation("t.mmd", 2, 1)), (warning.Code, warning.Location));
    }

    /// <summary>
    ///     Validates that init arguments that are not a JSON object are reported and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_NonObjectArguments_Reported()
    {
        // Arrange
        const string Text = "%%{init: [1, 2]}%%\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Equal("default", config.Theme);
        Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, Assert.Single(warnings).Code);
    }

    /// <summary>
    ///     Validates that a theme variable khroma cannot read is reported and dropped, and the theme derived without it.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_ThemeVariableNotAColor_Dropped()
    {
        // Arrange
        const string Text = "%%{init: {\"theme\": \"base\", \"themeVariables\": {\"primaryColor\": \"nonsense\", \"lineColor\": \"#0000ff\"}}}%%\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Equal("#fff4dd", config.ThemeVariable("mainBkg"));
        Assert.Equal("#0000ff", config.ThemeVariable("lineColor"));
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code);
        Assert.Contains("primaryColor", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a theme variable with characters Mermaid refuses, and a string with markup, are reported and
    ///     ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_RefusedStrings_Reported()
    {
        // Arrange
        const string Text = "%%{init: {\"fontFamily\": \"<b>x</b>\", \"themeVariables\": {\"fontFamily\": \"sans-serif\"}}}%%\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Equal(MermaidConfigResolver.DefaultFontFamily, config.FontFamily);
        Assert.Equal("\"trebuchet ms\", verdana, arial, sans-serif", config.ThemeVariable("fontFamily"));
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code));
    }

    /// <summary>
    ///     Validates that theme variables that make the theme fail without a single culprit (a hostile color limit) are
    ///     reported and ignored as a whole, within bounds.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_HostileColorLimit_FallsBackToTheme()
    {
        // Arrange
        const string Text = "%%{init: {\"theme\": \"default\", \"themeVariables\": {\"THEME_COLOR_LIMIT\": 1e12, \"lineColor\": \"#ff0000\"}}}%%\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Equal("#333333", config.ThemeVariable("lineColor"));
        Assert.Equal("12", config.ThemeVariable("THEME_COLOR_LIMIT"));
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, new MermaidLocation("t.mmd", 1, 1)), (warning.Code, warning.Location));
    }

    /// <summary>
    ///     Validates that a theme this version does not have is reported and has no effect.
    /// </summary>
    /// <param name="theme">The theme name.</param>
    /// <param name="unsupported">Whether the name is a newer Mermaid theme.</param>
    [Theory]
    [InlineData("neo", true)]
    [InlineData("nope", false)]
    public void MermaidConfigResolver_Resolve_UnavailableTheme_Reported(string theme, bool unsupported)
    {
        // Arrange
        var text = $"---\nconfig:\n  theme: {theme}\n---\n" + Flow;

        // Act
        var (config, warnings) = Resolve(text);

        // Assert
        Assert.Equal("#ECECFF", config.ThemeVariable("mainBkg"));
        var warning = Assert.Single(warnings);
        Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, new MermaidLocation("t.mmd", 3, 3)), (warning.Code, warning.Location));
        Assert.Equal(unsupported, warning.Message.Contains("not supported by this version", StringComparison.Ordinal));
        Assert.Contains("default, base, dark, forest or neutral", warning.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a frontmatter <c>config</c> that is not a mapping is reported and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_FrontmatterConfigNotMapping_Reported()
    {
        // Arrange
        const string Text = "---\nconfig: 5\n---\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Equal("default", config.Theme);
        Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, Assert.Single(warnings).Code);
    }

    /// <summary>
    ///     Validates the wrap directive, the HTML-label rule and the locations of the keys.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_WrapHtmlLabelsAndLocations()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  flowchart:\n    htmlLabels: false\n---\n%%{wrap}%%\n%%{init: {\"layout\": \"elk\"}}%%\n" + Flow;

        // Act
        var (config, warnings) = Resolve(Text);

        // Assert
        Assert.Empty(warnings);
        Assert.True(config.Wrap);
        Assert.False(config.HtmlLabels);
        Assert.Equal(("handDrawn", "elk"), (config.Look, config.Layout));
        Assert.Equal(new MermaidLocation("t.mmd", 3, 3), config.LocationOf("look"));
        Assert.Equal(new MermaidLocation("t.mmd", 8, 1), config.LocationOf("layout"));
        Assert.Equal(new MermaidLocation("t.mmd", 7, 1), config.LocationOf("wrap"));
        Assert.False(config.LocationOf("theme").IsKnown);
    }

    /// <summary>
    ///     Validates the detector settings and the ELK layout of a flowchart whose default renderer is <c>elk</c>.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_ElkRenderer_SettingsAndLayout()
    {
        // Arrange
        const string Text = "---\nconfig:\n  flowchart:\n    defaultRenderer: elk\n  class:\n    defaultRenderer: dagre-d3\n---\n" + Flow;

        // Act
        var (config, _) = Resolve(Text, diagramId: "flowchart-v2");

        // Assert
        Assert.Equal(new DetectorSettings("elk", "dagre-d3", "dagre-wrapper"), config.DetectorSettings);
        Assert.Equal("elk", config.Layout);
        Assert.False(config.Flowchart.ArrowMarkerAbsolute);
    }

    /// <summary>
    ///     Validates that resolving without a sink reports nothing and gives the same configuration.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_WithoutSink_SameResult()
    {
        // Arrange
        const string Text = "---\nconfig:\n  securityLevel: loose\n  theme: dark\n---\n%%{init: {bad}}%%\n" + Flow;
        var source = Preprocessor.Run(Text, "t.mmd", MermaidLimits.Default);

        // Act
        var silent = MermaidConfigResolver.Resolve(source, "flowchart-v2", null, null);
        var (reported, warnings) = Resolve(Text);

        // Assert
        Assert.NotEmpty(warnings);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(silent.Raw, reported.Raw));
    }

    /// <summary>
    ///     Validates that many unknown keys are each reported, and that the nearest-key hints are bounded.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_ManyUnknownKeys_HintsBounded()
    {
        // Arrange
        var keys = string.Join(", ", Enumerable.Range(0, 40).Select(i => $"\"padding{i}\": 1"));
        var text = $"%%{{init: {{{keys}}}}}%%\n" + Flow;

        // Act
        var (_, warnings) = Resolve(text);

        // Assert
        Assert.Equal(40, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigUnknownKey, warning.Code));
        Assert.Equal(32, warnings.Count(warning => warning.Hint is not null));
    }

    /// <summary>
    ///     Validates the merge primitives: <c>assignWithDepth</c> keeps objects from being clobbered and skips nulls,
    ///     lodash's merge replaces with nulls, and <c>sanitizeCss</c> refuses unbalanced braces.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_MergePrimitives_FollowMermaid()
    {
        // Arrange
        var dst = System.Text.Json.Nodes.JsonNode.Parse("{\"foo\": {\"bar\": \"bar\"}, \"bar\": \"foo\", \"list\": [1, 2]}")!;
        var src = System.Text.Json.Nodes.JsonNode.Parse("{\"foo\": \"foo\", \"bar\": \"bar\", \"gone\": null, \"list\": [2, 3]}")!;
        var target = System.Text.Json.Nodes.JsonNode.Parse("{\"a\": {\"b\": 1}}")!.AsObject();

        // Act
        var assigned = MermaidConfigResolver.AssignWithDepth(dst, src);
        MermaidConfigResolver.LodashMerge(target, System.Text.Json.Nodes.JsonNode.Parse("{\"a\": {\"b\": null, \"c\": 2}}")!.AsObject());

        // Assert
        Assert.Equal("{\"foo\":{\"bar\":\"bar\"},\"bar\":\"bar\",\"list\":[1,2,3]}", assigned!.ToJsonString());
        Assert.Equal("{\"a\":{\"b\":null,\"c\":2}}", target.ToJsonString());
        Assert.Equal("{ /* ERROR: Unbalanced CSS */ }", MermaidConfigResolver.SanitizeCss("a } b {"));
        Assert.Equal(".a { b }", MermaidConfigResolver.SanitizeCss(".a { b }"));
    }

    /// <summary>
    ///     Resolves the configuration of a text and collects the warnings.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="options">The options, or null.</param>
    /// <param name="diagramId">The diagram id.</param>
    /// <returns>The configuration and the warnings.</returns>
    private static (MermaidConfig Config, List<MermaidDiagnostic> Warnings) Resolve(string text, MermaidRenderOptions? options = null, string diagramId = "flowchart-v2")
    {
        var source = Preprocessor.Run(text, "t.mmd", MermaidLimits.Default);
        var sink = new MermaidReportSink(1000, throwOnUnsupported: false);
        var config = MermaidConfigResolver.Resolve(source, diagramId, options, sink);
        var warnings = new List<MermaidDiagnostic>();
        sink.Deliver(warnings.Add, null);
        return (config, warnings);
    }
}

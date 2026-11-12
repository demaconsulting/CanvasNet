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

    /// <summary>A sequence diagram body that follows the configuration of a test.</summary>
    private const string Sequence = "sequenceDiagram\n  Alice->>Bob: Hi\n";

    /// <summary>A class diagram.</summary>
    private const string Class = "classDiagram\n  class Animal\n";

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
    ///     Validates the sequence section without configuration: Mermaid's defaults, with the fonts of the renderer's
    ///     <c>setConf</c> (the top-level family and size of 16 pixels replace the section's own).
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_SequenceDefaults_MatchMermaid()
    {
        // Act
        var (config, warnings) = Resolve(Sequence, diagramId: "sequence");

        // Assert
        Assert.Empty(warnings);
        var sequence = config.Sequence;
        Assert.Equal(
            (false, 10, 50, 10, 50, 150, 65, 10, 5, 10, 35),
            (sequence.HideUnusedParticipants, sequence.ActivationWidth, sequence.DiagramMarginX, sequence.DiagramMarginY, sequence.ActorMargin,
             sequence.Width, sequence.Height, sequence.BoxMargin, sequence.BoxTextMargin, sequence.NoteMargin, sequence.MessageMargin));
        Assert.Equal(
            ("center", "center", true, false, 1, false, false, false, 10, 50, 20),
            (sequence.MessageAlign, sequence.NoteAlign, sequence.MirrorActors, sequence.ForceMenus, sequence.BottomMarginAdj, sequence.RightAngles,
             sequence.ShowSequenceNumbers, sequence.Wrap, sequence.WrapPadding, sequence.LabelBoxWidth, sequence.LabelBoxHeight));
        Assert.True(sequence.UseMaxWidth);
        Assert.Null(sequence.TextPlacement);
        var expectedFont = new MermaidFontConfig(MermaidConfigResolver.DefaultFontFamily, ConfigScalar.FromNumber(16), ConfigScalar.FromNumber(400));
        Assert.Equal(expectedFont, sequence.ActorFont);
        Assert.Equal(expectedFont, sequence.NoteFont);
        Assert.Equal(expectedFont, sequence.MessageFont);
    }

    /// <summary>
    ///     Validates the <c>setConf</c> font rules: the section's sizes and families have no effect while the top level
    ///     has its defaults, its weights apply, and a top-level <c>fontWeight</c> replaces them all.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_SequenceFonts_FollowSetConf()
    {
        // Arrange
        const string Section = "---\nconfig:\n  sequence:\n    actorFontSize: 30\n    noteFontFamily: serif\n    noteFontWeight: bold\n    messageFontSize: \"20px\"\n---\n";
        const string Top = "---\nconfig:\n  fontSize: 12\n  fontFamily: courier\n  fontWeight: 600\n  sequence:\n    noteFontWeight: bold\n---\n";

        // Act
        var (section, sectionWarnings) = Resolve(Section + Sequence, diagramId: "sequence");
        var (top, topWarnings) = Resolve(Top + Sequence, diagramId: "sequence");

        // Assert
        Assert.Empty(sectionWarnings);
        Assert.Empty(topWarnings);
        Assert.Equal(new MermaidFontConfig(MermaidConfigResolver.DefaultFontFamily, ConfigScalar.FromNumber(16), ConfigScalar.FromNumber(400)), section.Sequence.ActorFont);
        Assert.Equal(new MermaidFontConfig(MermaidConfigResolver.DefaultFontFamily, ConfigScalar.FromNumber(16), ConfigScalar.FromText("bold")), section.Sequence.NoteFont);
        Assert.Equal(new MermaidFontConfig("courier", ConfigScalar.FromNumber(12), ConfigScalar.FromNumber(600)), top.Sequence.NoteFont);
        Assert.Equal(top.Sequence.NoteFont, top.Sequence.ActorFont);
        Assert.Equal(top.Sequence.NoteFont, top.Sequence.MessageFont);
    }

    /// <summary>
    ///     Validates the sequence wrap setting: a true top-level <c>wrap</c> (or a <c>%%{wrap}%%</c> directive) or a true
    ///     <c>sequence.wrap</c> makes texts wrap; a false top-level <c>wrap</c> does not turn off the section's.
    /// </summary>
    /// <param name="config">The frontmatter configuration lines.</param>
    /// <param name="directive">Whether a <c>%%{wrap}%%</c> directive is written.</param>
    /// <param name="expected">The effective wrap setting.</param>
    [Theory]
    [InlineData("", false, false)]
    [InlineData("  wrap: true\n", false, true)]
    [InlineData("  sequence:\n    wrap: true\n", false, true)]
    [InlineData("  wrap: false\n  sequence:\n    wrap: true\n", false, true)]
    [InlineData("  wrap: true\n  sequence:\n    wrap: false\n", false, true)]
    [InlineData("  wrap: false\n", false, false)]
    [InlineData("", true, true)]
    public void MermaidConfigResolver_Resolve_SequenceWrap_FollowsInit(string config, bool directive, bool expected)
    {
        // Arrange
        var text = (config.Length > 0 ? $"---\nconfig:\n{config}---\n" : string.Empty) + (directive ? "%%{wrap}%%\n" : string.Empty) + Sequence;

        // Act
        var (resolved, warnings) = Resolve(text, diagramId: "sequence");

        // Assert
        Assert.Empty(warnings);
        Assert.Equal(expected, resolved.Sequence.Wrap);
    }

    /// <summary>
    ///     Validates that sequence values of the wrong type and alignments Mermaid does not know are reported at the key
    ///     and ignored, while a font size string and the alignments <c>drawText</c> honors are accepted.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_SequenceInvalidValues_ReportedAndIgnored()
    {
        // Arrange
        const string Text = "%%{init: {\"sequence\": {\"mirrorActors\": \"no\", \"width\": \"wide\", \"actorFontSize\": true, " +
                            "\"noteAlign\": \"diagonal\", \"messageAlign\": \"end\", \"messageFontSize\": \"18px\"}}}%%\n" + Sequence;

        // Act
        var (config, warnings) = Resolve(Text, diagramId: "sequence");

        // Assert
        Assert.Equal(4, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, new MermaidLocation("t.mmd", 1, 1)), (warning.Code, warning.Location)));
        Assert.Contains(warnings, warning => warning.Message.Contains("sequence.noteAlign", StringComparison.Ordinal) && warning.Message.Contains("left, center, right, start, middle or end", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Message.Contains("sequence.actorFontSize", StringComparison.Ordinal) && warning.Message.Contains("a number or a string", StringComparison.Ordinal));
        Assert.True(config.Sequence.MirrorActors);
        Assert.Equal(150, config.Sequence.Width);
        Assert.Equal("center", config.Sequence.NoteAlign);
        Assert.Equal("end", config.Sequence.MessageAlign);
        Assert.Equal("18px", (string?)config.Raw["sequence"]!["messageFontSize"]);
    }

    /// <summary>
    ///     Validates that an alignment Mermaid's <c>drawText</c> does not honor is reported with a message and a hint that
    ///     name all six alignments it does honor (<c>left</c>, <c>center</c>, <c>right</c>, <c>start</c>, <c>middle</c> and
    ///     <c>end</c>), and is ignored.
    /// </summary>
    /// <param name="key">The alignment key of the sequence section.</param>
    [Theory]
    [InlineData("messageAlign")]
    [InlineData("noteAlign")]
    public void MermaidConfigResolver_Resolve_SequenceAlignInvalid_ReportsAllSixValues(string key)
    {
        // Arrange: an alignment that drawText does not know
        var text = $"%%{{init: {{\"sequence\": {{\"{key}\": \"justify\"}}}}}}%%\n" + Sequence;

        // Act
        var (config, warnings) = Resolve(text, diagramId: "sequence");

        // Assert: one located warning naming all six values in the message and the hint; the default is kept
        var warning = Assert.Single(warnings);
        Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code);
        Assert.Contains($"sequence.{key}", warning.Message, StringComparison.Ordinal);
        Assert.Contains("must be left, center, right, start, middle or end", warning.Message, StringComparison.Ordinal);
        Assert.Equal("write \"left\", \"center\", \"right\", \"start\", \"middle\" or \"end\"", warning.Hint);
        Assert.Equal("center", key == "messageAlign" ? config.Sequence.MessageAlign : config.Sequence.NoteAlign);
    }

    /// <summary>
    ///     Validates that each of the six alignments Mermaid's <c>drawText</c> honors is accepted without a warning for
    ///     both sequence alignment keys.
    /// </summary>
    /// <param name="value">The alignment.</param>
    [Theory]
    [InlineData("left")]
    [InlineData("center")]
    [InlineData("right")]
    [InlineData("start")]
    [InlineData("middle")]
    [InlineData("end")]
    public void MermaidConfigResolver_Resolve_SequenceAlignValues_Accepted(string value)
    {
        // Arrange
        var text = $"%%{{init: {{\"sequence\": {{\"messageAlign\": \"{value}\", \"noteAlign\": \"{value}\"}}}}}}%%\n" + Sequence;

        // Act
        var (config, warnings) = Resolve(text, diagramId: "sequence");

        // Assert
        Assert.Empty(warnings);
        Assert.Equal(value, config.Sequence.MessageAlign);
        Assert.Equal(value, config.Sequence.NoteAlign);
    }

    /// <summary>
    ///     Validates that a directive's <c>config</c> key moves to the sequence section for a sequence diagram.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_SequenceDirectiveConfig_MovesToSection()
    {
        // Arrange
        const string Text = "%%{init: {\"config\": {\"mirrorActors\": false, \"actorMargin\": 80, \"textPlacement\": \"old\"}}}%%\n" + Sequence;

        // Act
        var (config, warnings) = Resolve(Text, diagramId: "sequence");

        // Assert
        Assert.Empty(warnings);
        Assert.False(config.Sequence.MirrorActors);
        Assert.Equal(80, config.Sequence.ActorMargin);
        Assert.Equal("old", config.Sequence.TextPlacement);
    }

    /// <summary>
    ///     Validates the class section without configuration: the three keys of <c>defaultConfig.ts</c> (no
    ///     <c>padding</c>), and the spacing and title margin of the state section's fallbacks.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_ClassDefaults_MatchMermaid()
    {
        // Act
        var (config, warnings) = Resolve(Class, diagramId: "classDiagram");

        // Assert
        Assert.Empty(warnings);
        Assert.Equal(["defaultRenderer", "hideEmptyMembersBox", "hierarchicalNamespaces"], config.Raw["class"]!.AsObject().Select(pair => pair.Key));
        Assert.Equal(new ClassConfig(), config.Class);
        Assert.Equal(("dagre-wrapper", false, true, (double?)null), (config.Class.DefaultRenderer, config.Class.HideEmptyMembersBox, config.Class.HierarchicalNamespaces, config.Class.Padding));
        Assert.Equal((50.0, 50.0, 25.0), (config.Class.NodeSpacing, config.Class.RankSpacing, config.Class.TitleTopMargin));
    }

    /// <summary>
    ///     Validates the class renderer's reads: the class keys from the class section, and the spacing and title margin
    ///     from the state section, where a zero spacing falls back to 50 (<c>||</c>) and a zero margin is kept (<c>??</c>).
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_ClassSpacingFromState_FollowsClassRenderer()
    {
        // Arrange
        const string Text = "---\nconfig:\n  class:\n    padding: 0\n    hideEmptyMembersBox: true\n    hierarchicalNamespaces: false\n" +
                            "  state:\n    nodeSpacing: 0\n    rankSpacing: 80\n    titleTopMargin: 0\n---\n" + Class;

        // Act
        var (config, warnings) = Resolve(Text, diagramId: "classDiagram");

        // Assert
        Assert.Empty(warnings);
        Assert.Equal((true, false, (double?)0), (config.Class.HideEmptyMembersBox, config.Class.HierarchicalNamespaces, config.Class.Padding));
        Assert.Equal((50.0, 80.0, 0.0), (config.Class.NodeSpacing, config.Class.RankSpacing, config.Class.TitleTopMargin));
    }

    /// <summary>
    ///     Validates that class and state values of the wrong type are reported at the key and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_ClassInvalidValues_ReportedAndIgnored()
    {
        // Arrange
        const string Text = "%%{init: {\"class\": {\"padding\": \"big\", \"hierarchicalNamespaces\": \"no\"}, \"state\": {\"nodeSpacing\": \"wide\"}}}%%\n" + Class;

        // Act
        var (config, warnings) = Resolve(Text, diagramId: "classDiagram");

        // Assert
        Assert.Equal(3, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal((MermaidDiagnosticCodes.ConfigInvalid, new MermaidLocation("t.mmd", 1, 1)), (warning.Code, warning.Location)));
        Assert.Contains(warnings, warning => warning.Message.Contains("class.padding", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Message.Contains("state.nodeSpacing", StringComparison.Ordinal));
        Assert.Equal(new ClassConfig(), config.Class);
    }

    /// <summary>
    ///     Validates the state and entity relationship sections without configuration: the schema's defaults in the
    ///     effective configuration, and the values their renderers and shapes read.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_StateAndErDefaults_MatchMermaid()
    {
        // Act
        var (state, stateWarnings) = Resolve("stateDiagram-v2\n  A --> B\n", diagramId: "stateDiagram");
        var (er, erWarnings) = Resolve("erDiagram\n  A ||--o{ B : has\n", diagramId: "er");

        // Assert
        Assert.Empty(stateWarnings);
        Assert.Empty(erWarnings);
        Assert.Equal(18, state.Raw["state"]!.AsObject().Count);
        Assert.Equal((8.0, "20"), (state.Raw["state"]!["padding"]!.GetValue<double>(), state.Raw["state"]!["edgeLengthFactor"]!.GetValue<string>()));
        Assert.Equal(12, er.Raw["er"]!.AsObject().Count);
        Assert.Equal((75.0, "honeydew"), (er.Raw["er"]!["minEntityHeight"]!.GetValue<double>(), er.Raw["er"]!["fill"]!.GetValue<string>()));
        Assert.Equal(new StateConfig(), state.State);
        Assert.Equal(("dagre-wrapper", 50.0, 50.0, 25.0), (state.State.DefaultRenderer, state.State.NodeSpacing, state.State.RankSpacing, state.State.TitleTopMargin));
        Assert.Equal(new ErConfig(), er.Er);
        Assert.Equal((20.0, 15.0, 100.0), (er.Er.DiagramPadding, er.Er.EntityPadding, er.Er.MinEntityWidth));
        Assert.Equal((140.0, 80.0, 25.0), (er.Er.NodeSpacing, er.Er.RankSpacing, er.Er.TitleTopMargin));
    }

    /// <summary>
    ///     Validates the state and entity relationship renderers' reads: a zero spacing falls back to the default
    ///     (<c>||</c>), a zero title margin is kept (<c>??</c>), and the entity box keys are taken as given.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_StateAndErSpacing_FollowTheRenderers()
    {
        // Arrange
        const string Text = "---\nconfig:\n  state:\n    nodeSpacing: 0\n    rankSpacing: 70\n    titleTopMargin: 0\n" +
                            "  er:\n    nodeSpacing: 0\n    rankSpacing: 60\n    titleTopMargin: 0\n    diagramPadding: 5\n    entityPadding: 4\n    minEntityWidth: 30\n---\n" +
                            "erDiagram\n  A ||--o{ B : has\n";

        // Act
        var (config, warnings) = Resolve(Text, diagramId: "er");

        // Assert
        Assert.Empty(warnings);
        Assert.Equal((50.0, 70.0, 0.0), (config.State.NodeSpacing, config.State.RankSpacing, config.State.TitleTopMargin));
        Assert.Equal((140.0, 60.0, 0.0), (config.Er.NodeSpacing, config.Er.RankSpacing, config.Er.TitleTopMargin));
        Assert.Equal((5.0, 4.0, 30.0), (config.Er.DiagramPadding, config.Er.EntityPadding, config.Er.MinEntityWidth));
    }

    /// <summary>
    ///     Validates the pie section: the schema's defaults without configuration, the values a frontmatter sets, and a
    ///     value of the wrong type reported at its key and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_PieSection_DefaultsValuesAndInvalidValues()
    {
        // Arrange
        const string Set = "---\nconfig:\n  pie:\n    textPosition: 0.5\n    donutHole: 0.3\n    legendPosition: top\n    highlightSlice: Dogs\n---\npie\n\"Dogs\" : 1\n";
        const string Wrong = "%%{init: {\"pie\": {\"textPosition\": \"far\", \"legendPosition\": 3}}}%%\npie\n\"a\" : 1\n";

        // Act
        var (defaults, defaultWarnings) = Resolve("pie\n\"a\" : 1\n", diagramId: "pie");
        var (set, setWarnings) = Resolve(Set, diagramId: "pie");
        var (wrong, wrongWarnings) = Resolve(Wrong, diagramId: "pie");

        // Assert
        Assert.Empty(defaultWarnings);
        Assert.Empty(setWarnings);
        Assert.Equal(5, defaults.Raw["pie"]!.AsObject().Count);
        Assert.Equal(new PieConfig(), defaults.Pie);
        Assert.Equal((0.75, 0.0, "right", string.Empty), (defaults.Pie.TextPosition, defaults.Pie.DonutHole, defaults.Pie.LegendPosition, defaults.Pie.HighlightSlice));
        Assert.Equal((0.5, 0.3, "top", "Dogs"), (set.Pie.TextPosition, set.Pie.DonutHole, set.Pie.LegendPosition, set.Pie.HighlightSlice));
        Assert.Equal(2, wrongWarnings.Count);
        Assert.All(wrongWarnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code));
        Assert.Equal(new PieConfig(), wrong.Pie);
    }

    /// <summary>
    ///     Validates the gantt section: the schema's defaults without configuration (no tick interval and no width), the
    ///     values a frontmatter sets (its <c>displayMode</c> included), and a value of the wrong type reported and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_GanttSection_DefaultsValuesAndInvalidValues()
    {
        // Arrange
        const string Set = "---\ndisplayMode: compact\nconfig:\n  gantt:\n    barHeight: 30\n    axisFormat: '%d'\n    tickInterval: 1week\n    topAxis: true\n    useWidth: 900\n---\ngantt\nA :1d\n";
        const string Wrong = "%%{init: {\"gantt\": {\"barHeight\": \"tall\", \"topAxis\": 1}}}%%\ngantt\nA :1d\n";

        // Act
        var (defaults, defaultWarnings) = Resolve("gantt\nA :1d\n", diagramId: "gantt");
        var (set, setWarnings) = Resolve(Set, diagramId: "gantt");
        var (wrong, wrongWarnings) = Resolve(Wrong, diagramId: "gantt");

        // Assert
        Assert.Empty(defaultWarnings);
        Assert.Empty(setWarnings);
        Assert.Equal(15, defaults.Raw["gantt"]!.AsObject().Count);
        Assert.Equal(new GanttConfig(), defaults.Gantt);
        Assert.Equal((25.0, 20.0, 4.0, 50.0, 75.0, 75.0, 35.0), (defaults.Gantt.TitleTopMargin, defaults.Gantt.BarHeight, defaults.Gantt.BarGap, defaults.Gantt.TopPadding, defaults.Gantt.RightPadding, defaults.Gantt.LeftPadding, defaults.Gantt.GridLineStartPadding));
        Assert.Equal((11.0, 11.0, 4.0, "%Y-%m-%d", (string?)null, false, string.Empty, (double?)null), (defaults.Gantt.FontSize, defaults.Gantt.SectionFontSize, defaults.Gantt.NumberSectionStyles, defaults.Gantt.AxisFormat, defaults.Gantt.TickInterval, defaults.Gantt.TopAxis, defaults.Gantt.DisplayMode, defaults.Gantt.UseWidth));
        Assert.Equal((30.0, "%d", "1week", true, "compact", (double?)900), (set.Gantt.BarHeight, set.Gantt.AxisFormat, set.Gantt.TickInterval, set.Gantt.TopAxis, set.Gantt.DisplayMode, set.Gantt.UseWidth));
        Assert.Equal(2, wrongWarnings.Count);
        Assert.All(wrongWarnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code));
        Assert.Equal(new GanttConfig(), wrong.Gantt);
    }

    /// <summary>
    ///     Validates the mindmap section: the schema's defaults without configuration, the values a frontmatter sets, and a
    ///     value of the wrong type reported at its key and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_MindmapSection_DefaultsValuesAndInvalidValues()
    {
        // Arrange
        const string Set = "---\nconfig:\n  mindmap:\n    padding: 4\n    maxNodeWidth: 120\n    layoutAlgorithm: other\n    useMaxWidth: false\n---\nmindmap\n  root\n";
        const string Wrong = "%%{init: {\"mindmap\": {\"padding\": \"wide\", \"maxNodeWidth\": true}}}%%\nmindmap\n  root\n";

        // Act
        var (defaults, defaultWarnings) = Resolve("mindmap\n  root\n", diagramId: "mindmap");
        var (set, setWarnings) = Resolve(Set, diagramId: "mindmap");
        var (wrong, wrongWarnings) = Resolve(Wrong, diagramId: "mindmap");

        // Assert
        Assert.Empty(defaultWarnings);
        Assert.Empty(setWarnings);
        Assert.Equal(4, defaults.Raw["mindmap"]!.AsObject().Count);
        Assert.Equal(new MindmapConfig(), defaults.Mindmap);
        Assert.Equal((10.0, 200.0, "cose-bilkent", true), (defaults.Mindmap.Padding, defaults.Mindmap.MaxNodeWidth, defaults.Mindmap.LayoutAlgorithm, defaults.Mindmap.UseMaxWidth));
        Assert.Equal((4.0, 120.0, "other", false), (set.Mindmap.Padding, set.Mindmap.MaxNodeWidth, set.Mindmap.LayoutAlgorithm, set.Mindmap.UseMaxWidth));
        Assert.Equal(2, wrongWarnings.Count);
        Assert.All(wrongWarnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code));
        Assert.Contains(wrongWarnings, warning => warning.Message.Contains("mindmap.padding", StringComparison.Ordinal));
        Assert.Equal(new MindmapConfig(), wrong.Mindmap);
    }

    /// <summary>
    ///     Validates the packet section: the schema's defaults without configuration (the effective <c>paddingY</c> grown
    ///     by 10 for the bit numbers), the values a frontmatter sets (without bit numbers the padding is not grown), and
    ///     values of the wrong type reported at their keys and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_PacketSection_DefaultsValuesAndInvalidValues()
    {
        // Arrange
        const string Set = "---\nconfig:\n  packet:\n    rowHeight: 40\n    bitWidth: 20\n    bitsPerRow: 16\n    showBits: false\n    paddingX: 2\n    paddingY: 8\n    useMaxWidth: false\n---\npacket\n0: \"a\"\n";
        const string Wrong = "%%{init: {\"packet\": {\"bitsPerRow\": \"wide\", \"showBits\": 1, \"paddingY\": \"7\"}}}%%\npacket\n0: \"a\"\n";

        // Act
        var (defaults, defaultWarnings) = Resolve("packet\n0: \"a\"\n", diagramId: "packet");
        var (set, setWarnings) = Resolve(Set, diagramId: "packet");
        var (wrong, wrongWarnings) = Resolve(Wrong, diagramId: "packet");

        // Assert
        Assert.Empty(defaultWarnings);
        Assert.Empty(setWarnings);
        Assert.Equal(7, defaults.Raw["packet"]!.AsObject().Count);
        Assert.Equal(new PacketConfig(), defaults.Packet);
        Assert.Equal((32.0, 32.0, 32.0, true, 5.0, 5.0, true, 15.0), (defaults.Packet.RowHeight, defaults.Packet.BitWidth, defaults.Packet.BitsPerRow, defaults.Packet.ShowBits, defaults.Packet.PaddingX, defaults.Packet.PaddingY, defaults.Packet.UseMaxWidth, defaults.Packet.EffectivePaddingY));
        Assert.Equal((40.0, 20.0, 16.0, false, 2.0, 8.0, false, 8.0), (set.Packet.RowHeight, set.Packet.BitWidth, set.Packet.BitsPerRow, set.Packet.ShowBits, set.Packet.PaddingX, set.Packet.PaddingY, set.Packet.UseMaxWidth, set.Packet.EffectivePaddingY));
        Assert.Equal(3, wrongWarnings.Count);
        Assert.All(wrongWarnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code));
        Assert.Contains(wrongWarnings, warning => warning.Message.Contains("packet.bitsPerRow", StringComparison.Ordinal));
        Assert.Equal(new PacketConfig(), wrong.Packet);
    }

    /// <summary>
    ///     Validates the gitGraph section: the schema's defaults without configuration (its eleven keys, with the
    ///     <c>nodeLabel</c> object), the values a frontmatter sets, a site configuration's main branch under the text's
    ///     other settings, and values of the wrong type (a main branch name that is not a string, a <c>nodeLabel</c>
    ///     coordinate that is not a number) reported at their keys and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_GitGraphSection_DefaultsValuesAndInvalidValues()
    {
        // Arrange
        const string Set = "---\nconfig:\n  gitGraph:\n    titleTopMargin: 40\n    diagramPadding: 20\n    mainBranchName: trunk\n" +
                           "    mainBranchOrder: 2.5\n    showCommitLabel: false\n    showBranches: false\n    rotateCommitLabel: false\n" +
                           "    parallelCommits: true\n    useMaxWidth: false\n---\ngitGraph\n  commit\n";
        const string Wrong = "%%{init: {\"gitGraph\": {\"mainBranchName\": 7, \"parallelCommits\": \"yes\", \"nodeLabel\": {\"x\": \"left\"}}}}%%\ngitGraph\n  commit\n";
        var site = new MermaidRenderOptions { Config = """{ "gitGraph": { "mainBranchName": "release" } }""" };

        // Act
        var (defaults, defaultWarnings) = Resolve("gitGraph\n  commit\n", diagramId: "gitGraph");
        var (set, setWarnings) = Resolve(Set, diagramId: "gitGraph");
        var (sited, sitedWarnings) = Resolve("---\nconfig:\n  gitGraph:\n    parallelCommits: true\n---\ngitGraph\n  commit\n", site, "gitGraph");
        var (wrong, wrongWarnings) = Resolve(Wrong, diagramId: "gitGraph");

        // Assert
        Assert.Empty(defaultWarnings);
        Assert.Empty(setWarnings);
        Assert.Empty(sitedWarnings);
        Assert.Equal(11, defaults.Raw["gitGraph"]!.AsObject().Count);
        Assert.Equal(4, defaults.Raw["gitGraph"]!["nodeLabel"]!.AsObject().Count);
        Assert.Equal(new GitGraphConfig(), defaults.GitGraph);
        Assert.Equal(
            (25.0, 8.0, "main", 0.0, true, true, true, false, true),
            (defaults.GitGraph.TitleTopMargin, defaults.GitGraph.DiagramPadding, defaults.GitGraph.MainBranchName, defaults.GitGraph.MainBranchOrder,
             defaults.GitGraph.ShowCommitLabel, defaults.GitGraph.ShowBranches, defaults.GitGraph.RotateCommitLabel, defaults.GitGraph.ParallelCommits,
             defaults.GitGraph.UseMaxWidth));
        Assert.Equal(
            (40.0, 20.0, "trunk", 2.5, false, false, false, true, false),
            (set.GitGraph.TitleTopMargin, set.GitGraph.DiagramPadding, set.GitGraph.MainBranchName, set.GitGraph.MainBranchOrder,
             set.GitGraph.ShowCommitLabel, set.GitGraph.ShowBranches, set.GitGraph.RotateCommitLabel, set.GitGraph.ParallelCommits,
             set.GitGraph.UseMaxWidth));
        Assert.Equal(("release", true), (sited.GitGraph.MainBranchName, sited.GitGraph.ParallelCommits));
        Assert.Equal(3, wrongWarnings.Count);
        Assert.All(wrongWarnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code));
        Assert.Contains(wrongWarnings, warning => warning.Message.Contains("gitGraph.mainBranchName", StringComparison.Ordinal));
        Assert.Contains(wrongWarnings, warning => warning.Message.Contains("gitGraph.nodeLabel.x", StringComparison.Ordinal));
        Assert.Equal(new GitGraphConfig(), wrong.GitGraph);
        Assert.Equal(-25.0, wrong.Raw["gitGraph"]!["nodeLabel"]!["x"]!.GetValue<double>());
    }

    /// <summary>
    ///     Validates the mindmap's layout as its database's <c>getData</c> sets it: <c>cose-bilkent</c> unless the site
    ///     configuration, the frontmatter or a directive sets a layout (which is kept, even <c>dagre</c>), and only for a
    ///     mindmap (a flowchart keeps the default <c>dagre</c>; <c>mindmap.layoutAlgorithm</c> changes nothing).
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_MindmapLayout_IsCoseBilkentUnlessUserDefined()
    {
        // Arrange
        const string Mindmap = "mindmap\n  root\n";
        var site = new MermaidRenderOptions { Config = "{\"layout\": \"dagre\"}" };

        // Act
        var (plain, _) = Resolve(Mindmap, diagramId: "mindmap");
        var (algorithm, _) = Resolve("---\nconfig:\n  mindmap:\n    layoutAlgorithm: elk\n---\n" + Mindmap, diagramId: "mindmap");
        var (frontmatter, _) = Resolve("---\nconfig:\n  layout: elk\n---\n" + Mindmap, diagramId: "mindmap");
        var (directive, _) = Resolve("%%{init: {\"layout\": \"tidy-tree\"}}%%\n" + Mindmap, diagramId: "mindmap");
        var (siteDagre, _) = Resolve(Mindmap, site, diagramId: "mindmap");
        var (flowchart, _) = Resolve(Flow);

        // Assert
        Assert.Equal(("cose-bilkent", "cose-bilkent"), (plain.Layout, algorithm.Layout));
        Assert.Equal(("elk", "tidy-tree", "dagre"), (frontmatter.Layout, directive.Layout, siteDagre.Layout));
        Assert.Equal("dagre", flowchart.Layout);
    }

    /// <summary>
    ///     Validates that state and entity relationship values of the wrong type are reported at the key and ignored.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_Resolve_StateAndErInvalidValues_ReportedAndIgnored()
    {
        // Arrange
        const string Text = "%%{init: {\"state\": {\"defaultRenderer\": 3}, \"er\": {\"entityPadding\": \"big\", \"useMaxWidth\": \"no\"}}}%%\nerDiagram\n  A ||--o{ B : has\n";

        // Act
        var (config, warnings) = Resolve(Text, diagramId: "er");

        // Assert
        Assert.Equal(3, warnings.Count);
        Assert.All(warnings, warning => Assert.Equal(MermaidDiagnosticCodes.ConfigInvalid, warning.Code));
        Assert.Contains(warnings, warning => warning.Message.Contains("state.defaultRenderer", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Message.Contains("er.entityPadding", StringComparison.Ordinal));
        Assert.Equal(new StateConfig(), config.State);
        Assert.Equal(new ErConfig(), config.Er);
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

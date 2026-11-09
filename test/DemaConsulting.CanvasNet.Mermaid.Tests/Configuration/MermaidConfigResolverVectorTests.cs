using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Configuration;

/// <summary>
///     Unit tests of <see cref="MermaidConfigResolver"/> against the configurations the pinned Mermaid computes:
///     <c>mermaid.initialize(site)</c>, then <c>mermaid.parse(text)</c>, then <c>getConfig()</c>.
/// </summary>
public class MermaidConfigResolverVectorTests
{
    /// <summary>
    ///     Gets the indexes of the recorded configuration cases.
    /// </summary>
    public static TheoryData<int> ConfigCases => UpstreamVectors.Indexes(UpstreamVectors.Configs);

    /// <summary>
    ///     Validates each recorded case: the global values, the flowchart section, <c>gantt.displayMode</c> and (when
    ///     recorded) every theme variable equal Mermaid's.
    /// </summary>
    /// <param name="index">The index of the case.</param>
    [Theory]
    [MemberData(nameof(ConfigCases))]
    public void MermaidConfigResolver_Resolve_MatchesMermaid(int index)
    {
        // Arrange
        var vector = UpstreamVectors.Configs[index]!.AsObject();
        var name = (string)vector["name"]!;
        var expected = vector["expected"]!.AsObject();

        // Act
        var config = ResolveLikeMermaid(vector["site"]!.AsObject(), (string)vector["text"]!);

        // Assert
        var scalars = new JsonObject();
        foreach (var (key, value) in config.Raw)
        {
            if (value is not JsonObject)
            {
                scalars[key] = value?.DeepClone();
            }
        }

        AssertSame(name, "config", expected["config"], scalars);
        AssertSame(name, "flowchart", expected["flowchart"], config.Raw["flowchart"]);
        if ((string?)expected["gantt"]?["displayMode"] is { Length: > 0 } displayMode)
        {
            Assert.Equal(displayMode, (string?)config.Raw["gantt"]?["displayMode"]);
        }

        if (expected["themeVariables"] is { } themeVariables)
        {
            AssertSame(name, "themeVariables", themeVariables, config.ThemeVariables);
        }
    }

    /// <summary>
    ///     Validates that the configuration keys equal the keys Mermaid's <c>sanitizeDirective</c> accepts.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_ConfigKeys_MatchMermaid()
    {
        // Arrange
        var expected = UpstreamVectors.Themes["configKeys"]!.AsArray().Select(key => (string)key!).ToHashSet(StringComparer.Ordinal);

        // Act
        var actual = MermaidConfigResolver.ConfigKeys;

        // Assert
        Assert.Empty(expected.Except(actual));
        Assert.Empty(actual.Except(expected));
    }

    /// <summary>
    ///     Validates that the global and flowchart defaults equal Mermaid's default configuration.
    /// </summary>
    [Fact]
    public void MermaidConfigResolver_CreateDefaults_MatchMermaid()
    {
        // Arrange
        var expected = UpstreamVectors.Themes["defaults"]!.AsObject();
        var global = expected["global"]!.AsObject();

        // Act
        var defaults = MermaidConfigResolver.CreateDefaults();

        // Assert
        foreach (var (key, value) in global)
        {
            // Mermaid's undefined defaults (themeCSS, deterministicIDSeed) are recorded as null and are absent here
            if (value is null)
            {
                Assert.False(defaults.ContainsKey(key), key);
                continue;
            }

            Assert.True(UpstreamVectors.Difference(value, defaults[key]) is null, key);
        }

        Assert.Null(UpstreamVectors.Difference(expected["flowchart"], defaults["flowchart"]));
    }

    /// <summary>
    ///     Resolves a configuration the way Mermaid's <c>parse</c> builds it: the site configuration as
    ///     <c>initialize</c>, the text's frontmatter and directives, and the diagram detected with the result.
    /// </summary>
    /// <param name="site">The site configuration.</param>
    /// <param name="text">The text.</param>
    /// <returns>The configuration.</returns>
    internal static MermaidConfig ResolveLikeMermaid(JsonObject site, string text)
    {
        var options = new MermaidRenderOptions { Config = site.ToJsonString() };
        var source = Preprocessor.Run(text, null, MermaidLimits.Default);
        var first = MermaidConfigResolver.Resolve(source, null, options, null);
        var detected = DiagramDetector.Detect(source.Code, first.DetectorSettings);
        return MermaidConfigResolver.Resolve(source, detected.MermaidId, options, null);
    }

    /// <summary>
    ///     Asserts that two JSON values are equal and names the first difference.
    /// </summary>
    /// <param name="name">The case.</param>
    /// <param name="part">The part compared.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    private static void AssertSame(string name, string part, JsonNode? expected, JsonNode? actual)
    {
        var difference = UpstreamVectors.Difference(expected, actual, part);
        Assert.True(difference is null, $"{name}: {difference}");
    }
}

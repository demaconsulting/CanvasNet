// cspell:ignore textmetrics mercator geoshape geojson longitude hconcat minkowski multmatrix polyhedron
using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     The trigger table of the HelpTopics tests: for every diagnostic identifier a topic cites, a tiny source that
///     makes the real pipeline report it.
/// </summary>
public partial class HelpTopicsTests
{
    /// <summary>
    ///     A diagnostic identifier cited by a topic and a source that triggers it.
    /// </summary>
    /// <param name="Cited">The identifier as a topic writes it (a family such as <c>vl-mark-&lt;type&gt;</c> or a single identifier).</param>
    /// <param name="Code">The identifier the tool reports for <paramref name="Source"/>.</param>
    /// <param name="IsVegaLite"><see langword="true"/> for a chart, <see langword="false"/> for a model.</param>
    /// <param name="Source">The text of the model or chart.</param>
    /// <param name="IsWarning"><see langword="true"/> for a Vega-Lite warning, whose identifier is in the message.</param>
    private sealed record Trigger(string Cited, string Code, bool IsVegaLite, string Source, bool IsWarning = false);

    /// <summary>
    ///     Gets the trigger table.
    /// </summary>
    private static readonly IReadOnlyList<Trigger> Triggers =
    [
        new("scad-module-offset", "scad-module-offset", false, "offset(r=1) square(2);"),
        new("scad-projection-too-complex", "scad-projection-too-complex", false, "projection() difference() { sphere(r = 10, $fn = 100); cube(5, center = true); }"),
        new("scad-projection-open-mesh", "scad-projection-open-mesh", false, "projection(cut = true) polyhedron(points = [[0,0,-1],[4,0,-1],[0,4,-1],[0,0,3]], faces = [[0,2,1],[0,1,3],[1,2,3]]);"),
        new("scad-module-surface", "scad-module-surface", false, "surface(\"x.dat\");"),
        new("scad-module-import", "scad-module-import", false, "import(\"x.stl\");"),
        new("scad-module-roof", "scad-module-roof", false, "roof() square(2);"),
        new("scad-hull-csg-operand", "scad-hull-csg-operand", false, "hull() difference() { cube(2); cube(1); }"),
        new("scad-hull-mixed-dimensions", "scad-hull-mixed-dimensions", false, "hull() { square(2); cube(1); }"),
        new("scad-minkowski-nonconvex", "scad-minkowski-nonconvex", false, "minkowski() { difference() { cube(3); cube(1); } sphere(1); }"),
        new("scad-minkowski-2d", "scad-minkowski-2d", false, "minkowski() { square(2); circle(1); }"),
        new("scad-modifier-highlight", "scad-modifier-highlight", false, "#square(2);"),
        new("scad-modifier-background", "scad-modifier-background", false, "%square(2);"),
        new("scad-multmatrix-projective", "scad-multmatrix-projective", false, "multmatrix([[1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,1,1]]) cube(1);"),
        new("scad-resize-approximate", "scad-resize-approximate", false, "resize([5,5,5]) difference() { cube(3); cube(1); }"),
        new("scad-function-<name>", "scad-function-textmetrics", false, "x = textmetrics(\"a\"); cube(1);"),
        new("vl-mark-<type>", "vl-mark-geoshape", true, Chart(mark: "\"geoshape\"")),
        new("vl-channel-geo", "vl-channel-geo", true, Chart(extraEncoding: ",\"longitude\":{\"field\":\"x\",\"type\":\"quantitative\"}")),
        new("vl-channel-url", "vl-channel-url", true, Chart(mark: "\"bar\"", extraEncoding: ",\"url\":{\"field\":\"y\",\"type\":\"nominal\"}")),
        new("vl-data-remote-url", "vl-data-remote-url", true, Chart(data: "{\"url\":\"https://example.com/d.csv\"}")),
        new("vl-data-geo", "vl-data-geo", true, Chart(data: "{\"sphere\":true}")),
        new("vl-projection", "vl-projection", true, Chart(top: ",\"projection\":{\"type\":\"mercator\"}")),
        new("vl-param-selection-layer", "vl-param-selection-layer", true, "{\"params\":[{\"name\":\"p\",\"select\":\"interval\"}],\"data\":{\"values\":[{\"x\":1,\"y\":2}]},\"layer\":[" + Layer() + "," + Layer() + "]}"),
        new("vl-param-selection-id", "vl-param-selection-id", true, Chart(top: ",\"params\":[{\"name\":\"p\",\"select\":\"point\",\"value\":2}]")),
        new("vl-param-selection-animation", "vl-param-selection-animation", true, Chart(top: ",\"params\":[{\"name\":\"p\",\"select\":{\"type\":\"point\",\"on\":\"timer\"}}]")),
        new("vl-param-selection-projection", "vl-param-selection-projection", true, Chart(top: ",\"params\":[{\"name\":\"p\",\"select\":{\"type\":\"interval\",\"encodings\":[\"size\"]}}]"), true),
        new("vl-param-variable", "vl-param-variable", true, "{\"hconcat\":[" + Chart(top: ",\"params\":[{\"name\":\"v\",\"value\":1}]") + "]}"),
        new("vl-transform-lookup-param", "vl-transform-lookup-param", true, Chart(top: ",\"params\":[{\"name\":\"p\",\"select\":\"interval\"}],\"transform\":[{\"lookup\":\"x\",\"from\":{\"param\":\"p\"}}]")),
        new("vl-expr-ref", "vl-expr-ref", true, Chart(top: ",\"title\":{\"text\":\"T\",\"fontSize\":{\"expr\":\"datum.x\"}}")),
        new("vl-param-extent-static", "vl-param-extent-static", true, Chart(top: ",\"title\":{\"text\":{\"expr\":\"'max ' + e[1]\"}},\"transform\":[{\"extent\":\"x\",\"param\":\"e\"}]"), true),
        new("vl-expr-function-<name>", "vl-expr-function-now", true, Chart(top: ",\"transform\":[{\"calculate\":\"now()\",\"as\":\"z\"}]")),
        new("vl-guide-encode", "vl-guide-encode", true, Chart(x: "{\"field\":\"x\",\"type\":\"quantitative\",\"axis\":{\"encode\":{}}}")),
        new("vl-offset-position", "vl-offset-position", true, Chart(extraEncoding: ",\"xOffset\":{\"field\":\"y\",\"type\":\"nominal\"}")),
        new("vl-offset-ranged", "vl-offset-ranged", true, Chart(mark: "\"bar\"", x: "{\"field\":\"x\",\"type\":\"nominal\"}", extraEncoding: ",\"xOffset\":{\"field\":\"y\",\"type\":\"temporal\"}")),
        new("vl-scale-invalid-value", "vl-scale-invalid-value", true, Chart(top: ",\"config\":{\"scale\":{\"invalid\":{\"x\":{\"value\":null}}}}")),
        new("vl-shape-path-invalid", "vl-shape-path-invalid", true, Chart(mark: "{\"type\":\"point\",\"shape\":\"M0,0L\"}"), true),
        new("vl-scale-<type>", "vl-scale-identity", true, Chart(x: "{\"field\":\"x\",\"type\":\"quantitative\",\"scale\":{\"type\":\"identity\"}}")),
        new("vl-type-geojson", "vl-type-geojson", true, Chart(x: "{\"field\":\"x\",\"type\":\"geojson\"}")),
        new("vl-unknown-property", "vl-unknown-property", true, Chart(top: ",\"bogus\":1"), true),
        new("vl-schema-version", "vl-schema-version", true, Chart(top: ",\"$schema\":\"https://vega.github.io/schema/vega-lite/v4.json\""), true),
        new("vl-layout-fit-step", "vl-layout-fit-step", true, Chart(mark: "\"bar\"", x: "{\"field\":\"x\",\"type\":\"ordinal\"}", top: ",\"autosize\":\"fit\",\"width\":{\"step\":20}"), true),
        new("vl-scale-domain-raw", "vl-scale-domain-raw", true, Chart(top: ",\"params\":[{\"name\":\"d\",\"value\":\"abc\"}]", x: "{\"field\":\"x\",\"type\":\"quantitative\",\"scale\":{\"domain\":{\"param\":\"d\"}}}"), true),
        new("vl-composition-property-ignored", "vl-composition-property-ignored", true, "{\"hconcat\":[" + Chart() + "],\"width\":100}", true),
        new("vl-text-missing-glyph", "vl-text-missing-glyph", true, Chart(mark: "{\"type\":\"text\",\"text\":\"a \\uE000 b\"}"), true)
    ];

    /// <summary>
    ///     Gets the cited identifiers of the trigger table as theory data.
    /// </summary>
    public static IEnumerable<object[]> TriggerData => Triggers.Select(t => new object[] { t.Cited });

    /// <summary>
    ///     Test that every cited identifier of the trigger table is reported by the real pipeline for its source: a
    ///     model or chart construct is left out and listed in the diagnostics with that code (a Vega-Lite warning
    ///     has the code <c>vega-lite-warning</c> and the identifier at the start of its message text).
    /// </summary>
    /// <param name="cited">The cited identifier.</param>
    [Theory]
    [MemberData(nameof(TriggerData))]
    public void HelpTopics_DiagnosticTriggers_ProduceTheCitedIds(string cited)
    {
        // Arrange: the source of the trigger saved with the extension of its format
        var trigger = Triggers.First(t => t.Cited == cited);
        using var directory = new TempDirectory();
        var input = directory.Combine(trigger.IsVegaLite ? "trigger.vl.json" : "trigger.scad");
        File.WriteAllText(input, trigger.Source);
        string[] size = trigger.IsVegaLite ? [] : ["--size", "64x64"];

        // Act: render without --strict
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke(["render", input, "--json", "-o", directory.Combine("out.png"), .. size]);

        // Assert: it succeeded and the diagnostics carry the identifier
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostics = document.RootElement.GetProperty("diagnostics").EnumerateArray().ToList();
        var expectedCode = trigger.IsWarning ? "vega-lite-warning" : trigger.Code;
        Assert.Contains(
            diagnostics,
            d => d.GetProperty("code").GetString() == expectedCode
                 && (!trigger.IsWarning || d.GetProperty("message").GetString()!.Contains($"{trigger.Code}:", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Test that each image-link code the <c>svg</c> topic cites is reported by the real pipeline for a link that
    ///     causes it, with the severity the topic states, and that <c>--strict</c> fails exactly for the two that
    ///     leave the image out.
    /// </summary>
    /// <param name="href">The href of the link, written as XML attribute text (so <c>&amp;amp;</c> separates parameters).</param>
    /// <param name="code">The code the pipeline reports.</param>
    /// <param name="severity">The severity of the report.</param>
    /// <param name="strictFails">Whether <c>--strict</c> turns the report into an error.</param>
    [Theory]
    [InlineData("report.pdf#pag=2", "image-fragment-unknown-parameter", "warning", false)]
    [InlineData("report.pdf#page=1&amp;page=2", "image-fragment-duplicate-parameter", "warning", false)]
    [InlineData("report.pdf#page=0", "image-fragment-invalid", "unsupported", true)]
    [InlineData("report.pdf#page=9", "image-page-out-of-range", "unsupported", true)]
    public void HelpTopics_DiagnosticTriggers_ImageFragmentCodes_AreReportedWithTheirSeverity(string href, string code, string severity, bool strictFails)
    {
        // Arrange: a diagram linking a two-page PDF
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("report.pdf"), FixtureFactory.CreatePdf());
        var input = FixtureFactory.WriteText(
            directory.Path,
            "diagram.svg",
            $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 40 40' width='40' height='40'><image href='{href}' x='0' y='0' width='40' height='40'/></svg>");

        // Act: render without and with --strict
        var plain = ToolHost.Invoke("render", input, "--json", "-o", directory.Combine("out.png"));
        var strict = ToolHost.Invoke("render", input, "--strict", "--json", "-o", directory.Combine("strict.png"));

        // Assert: the code and severity are reported, and --strict fails only for an image that was not drawn
        Assert.True(plain.ExitCode == 0, plain.StdErr + plain.StdOut);
        using var document = JsonDocument.Parse(plain.StdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(code, diagnostic.GetProperty("code").GetString());
        Assert.Equal(severity, diagnostic.GetProperty("severity").GetString());
        Assert.Equal(strictFails ? 1 : 0, strict.ExitCode);
    }

    /// <summary>
    ///     Test that <c>--strict</c> turns the reports of a model and of a chart into the <c>unsupported-feature</c> error.
    /// </summary>
    /// <param name="cited">The cited identifier whose source is run with <c>--strict</c>.</param>
    [Theory]
    [InlineData("scad-module-offset")]
    [InlineData("vl-mark-<type>")]
    public void HelpTopics_DiagnosticTriggers_Strict_FailsWithUnsupportedFeature(string cited)
    {
        // Arrange: the source of an unsupported-feature trigger
        var trigger = Triggers.First(t => t.Cited == cited);
        using var directory = new TempDirectory();
        var input = directory.Combine(trigger.IsVegaLite ? "trigger.vl.json" : "trigger.scad");
        File.WriteAllText(input, trigger.Source);
        string[] size = trigger.IsVegaLite ? [] : ["--size", "64x64"];

        // Act: render with --strict
        var (exitCode, stdOut, _) = ToolHost.Invoke(["render", input, "--strict", "--json", "-o", directory.Combine("out.png"), .. size]);

        // Assert: the failure document carries the code
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal(ErrorCodes.UnsupportedFeature, document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(directory.Combine("out.png")));
    }

    /// <summary>
    ///     Builds the text of a small Vega-Lite chart with optional variations.
    /// </summary>
    /// <param name="mark">The JSON of the mark.</param>
    /// <param name="top">Extra top-level properties, starting with a comma.</param>
    /// <param name="x">The JSON of the x channel.</param>
    /// <param name="data">The JSON of the data.</param>
    /// <param name="extraEncoding">Extra channels of the encoding, starting with a comma.</param>
    /// <returns>The chart text.</returns>
    private static string Chart(
        string mark = "\"point\"",
        string top = "",
        string x = "{\"field\":\"x\",\"type\":\"quantitative\"}",
        string data = "{\"values\":[{\"x\":1,\"y\":2},{\"x\":2,\"y\":3}]}",
        string extraEncoding = "")
    {
        return "{\"data\":" + data + ",\"mark\":" + mark + ",\"encoding\":{\"x\":" + x
               + ",\"y\":{\"field\":\"y\",\"type\":\"quantitative\"}" + extraEncoding + "}" + top + "}";
    }

    /// <summary>
    ///     Builds a point view with an x and a y channel, for a layered chart.
    /// </summary>
    /// <returns>The view text.</returns>
    private static string Layer() =>
        "{\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"x\",\"type\":\"quantitative\"},\"y\":{\"field\":\"y\",\"type\":\"quantitative\"}}}";
}

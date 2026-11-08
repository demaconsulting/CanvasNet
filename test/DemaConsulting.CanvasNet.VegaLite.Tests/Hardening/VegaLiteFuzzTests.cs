using System.Globalization;
using System.Text.Json.Nodes;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     The structured fuzz suite of the Vega-Lite renderer: seeded, schema-shaped specifications and mutations of the repository's fixtures are measured and rendered under tight
///     limits, and every case must pass the oracle of <see cref="VegaLiteFuzzHarness"/> (only documented, located exceptions, no hang, bounded memory, two runs give the same
///     result). A failing case reports its seed, its index and the text of the specification so that it can be turned into a regression test.
/// </summary>
/// <remarks>
///     <para>
///         The default run is small and fixed (300 cases with a fixed seed) so that it can run in every build. Larger runs are opt-in through environment variables:
///         <c>CANVASNET_VL_FUZZ_CASES</c> (the total number of cases, split evenly between the three kinds; default 300), <c>CANVASNET_VL_FUZZ_SEED</c> (the seed; default 20260601)
///         and <c>CANVASNET_VL_FUZZ_DUMP_DIR</c> (a folder that receives the text of every failing specification). For example, a run of five thousand cases is
///         <c>CANVASNET_VL_FUZZ_CASES=5000 dotnet test test/DemaConsulting.CanvasNet.VegaLite.Tests --configuration Release</c>.
///     </para>
/// </remarks>
public class VegaLiteFuzzTests
{
    /// <summary>The environment variable that sets the total number of cases.</summary>
    private const string CasesVariable = "CANVASNET_VL_FUZZ_CASES";

    /// <summary>The environment variable that sets the seed.</summary>
    private const string SeedVariable = "CANVASNET_VL_FUZZ_SEED";

    /// <summary>The environment variable that names the folder that receives failing specifications.</summary>
    private const string DumpVariable = "CANVASNET_VL_FUZZ_DUMP_DIR";

    /// <summary>The default number of cases.</summary>
    private const int DefaultCases = 300;

    /// <summary>The default seed.</summary>
    private const ulong DefaultSeed = 20260601;

    /// <summary>The longest time one case may take before it counts as a hang.</summary>
    private static readonly TimeSpan CaseTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates the generator: the same seed and index give the same specification, different indexes give different ones, and the specification is valid JSON.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Fuzz_GeneratorIsSeededAndDeterministic()
    {
        // Arrange / Act
        var first = VegaLiteFuzzGenerator.Generate(7, 3).ToJsonString();
        var again = VegaLiteFuzzGenerator.Generate(7, 3).ToJsonString();
        var other = VegaLiteFuzzGenerator.Generate(7, 4).ToJsonString();
        var rng = new FuzzRandom(1);
        var rngAgain = new FuzzRandom(1);

        // Assert: same input, same output; a different index, a different specification; the random source is a fixed sequence (SplitMix64 test vector)
        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.NotNull(JsonNode.Parse(first));
        Assert.Equal(rng.NextULong(), rngAgain.NextULong());
        Assert.Equal(0xE220A8397B1DCDAFUL, new FuzzRandom(0).NextULong()); // cspell:disable-line
    }

    /// <summary>
    ///     Validates the oracle itself: the cases of the fuzz suite are not trivially rejected, so the suite reaches the layout and the painter (a large share of the generated
    ///     specifications renders, and a large share of the mutated fixtures renders).
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Fuzz_GeneratedSpecificationsReachTheRenderer()
    {
        // Arrange / Act
        var tallies = RunKind(Kind.Generated, Math.Max(1, TotalCases() / 3), out var failures);

        // Assert
        Assert.True(failures.Count == 0, FailureReport(failures));
        var total = tallies.Values.Sum();
        Assert.True(tallies.GetValueOrDefault("rendered") * 100 >= total * 10, "Fewer than 10% of the generated specifications render: " + Describe(tallies));
    }

    /// <summary>
    ///     Validates that mutated copies of the repository's fixtures only ever fail in documented ways, never hang, and are deterministic.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Fuzz_MutatedFixturesFailOnlyInDocumentedWays()
    {
        // Arrange / Act
        var tallies = RunKind(Kind.MutatedFixture, Math.Max(1, TotalCases() / 3), out var failures);

        // Assert
        Assert.True(failures.Count == 0, FailureReport(failures));
        var total = tallies.Values.Sum();
        Assert.True(tallies.GetValueOrDefault("rendered") * 100 >= total * 15, "Fewer than 15% of the mutated fixtures render: " + Describe(tallies));
    }

    /// <summary>
    ///     Validates that mutated copies of generated specifications (the deepest corner cases: valid structure with a boundary value in a random place) only ever fail in documented ways.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_Fuzz_MutatedGeneratedSpecificationsFailOnlyInDocumentedWays()
    {
        // Arrange / Act
        var tallies = RunKind(Kind.MutatedGenerated, Math.Max(1, TotalCases() / 3), out var failures);

        // Assert
        Assert.True(failures.Count == 0, FailureReport(failures));
        Assert.True(tallies.Values.Sum() > 0);
    }

    /// <summary>
    ///     Validates the regression corpus: the specifications that the fuzz suite found to break the oracle (each was an unhandled exception or an exception without a location) pass the oracle now,
    ///     with the outcome that the fix gives. A new defect that the opt-in runs find is added here as a row, with its seed and index in the comment.
    /// </summary>
    /// <param name="name">The name of the case.</param>
    /// <param name="spec">The specification.</param>
    /// <param name="outcome">The expected outcome.</param>
    [Theory]
    [InlineData(
        "denormal-tick-span (seed 31, index 2592: OverflowException in Ticks.GetTicks)",
        """{ "data": { "values": [ { "y": 1e-300 }, { "y": 5e-324 } ] }, "mark": "tick", "encoding": { "y": { "field": "y", "type": "quantitative", "axis": { "tickCount": 1e10 } } } }""",
        "rendered")]
    [InlineData(
        "infinite-padding (seed 51, index 3991: ArgumentOutOfRangeException from the scene without a location)",
        """{ "mark": "geoshape", "padding": 1.7976931348623157E+308, "data": { "values": [ { "a": 1 } ] } }""",
        "render-error")]
    [InlineData(
        "boxplot-empty-field (seed 63, index 2230: ArgumentNullException in TableLayout)",
        """{ "data": { "values": [ { "n0": 1 }, { "n0": 2 } ] }, "mark": "boxplot", "encoding": { "x": { "field": "", "type": "quantitative" }, "y": { "field": "n0", "type": "quantitative" } } }""",
        "render-error")]
    [InlineData(
        "errorband-empty-repeat-field (seed 63, index 2230)",
        """{ "data": { "values": [ { "n0": 1 }, { "n0": 2 } ] }, "repeat": { "row": [""] }, "spec": { "mark": "errorband", "encoding": { "x": { "field": { "repeat": "row" }, "type": "quantitative" }, "y": { "field": "n0", "type": "quantitative" } } } }""",
        "render-error")]
    public void CanvasNetVegaLite_SystemIntegration_Fuzz_FoundDefectsStayFixed(string name, string spec, string outcome)
    {
        // Arrange / Act
        var result = VegaLiteFuzzHarness.Run(spec, AppContext.BaseDirectory, strict: false, CaseTimeout, Ct);

        // Assert
        Assert.True(result.Failure is null, name + ": " + result.Failure);
        Assert.Equal(outcome, result.Outcome);
    }

    /// <summary>The three kinds of cases.</summary>
    private enum Kind
    {
        /// <summary>A generated specification.</summary>
        Generated,

        /// <summary>A mutated fixture.</summary>
        MutatedFixture,

        /// <summary>A mutated generated specification.</summary>
        MutatedGenerated,
    }

    /// <summary>
    ///     Gets the total number of cases of the run.
    /// </summary>
    /// <returns>The number of cases.</returns>
    private static int TotalCases() =>
        int.TryParse(Environment.GetEnvironmentVariable(CasesVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var cases) && cases > 0 ? cases : DefaultCases;

    /// <summary>
    ///     Gets the seed of the run.
    /// </summary>
    /// <returns>The seed.</returns>
    private static ulong Seed() =>
        ulong.TryParse(Environment.GetEnvironmentVariable(SeedVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var seed) ? seed : DefaultSeed;

    /// <summary>
    ///     Runs the cases of one kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="count">The number of cases.</param>
    /// <param name="failures">The descriptions of the failing cases (at most ten).</param>
    /// <returns>The number of cases per outcome.</returns>
    private static Dictionary<string, int> RunKind(Kind kind, int count, out List<string> failures)
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var fixtures = Directory.GetFiles(fixtureDirectory, "*.vl.json").OrderBy(p => p, StringComparer.Ordinal).Select(p => JsonNode.Parse(File.ReadAllText(p))!).ToList();
        var seed = Seed() + (ulong)kind;
        var dump = Environment.GetEnvironmentVariable(DumpVariable);
        var tallies = new Dictionary<string, int>(StringComparer.Ordinal);
        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        failures = [];
        for (var index = 0; index < count; index++)
        {
            Ct.ThrowIfCancellationRequested();
            var rng = FuzzRandom.ForCase(seed, index);
            var spec = kind switch
            {
                Kind.Generated => VegaLiteFuzzGenerator.Generate(seed, index),
                Kind.MutatedFixture => VegaLiteFuzzMutator.Mutate(fixtures[index % fixtures.Count], fixtures[rng.Next(fixtures.Count)], rng),
                _ => VegaLiteFuzzMutator.Mutate(VegaLiteFuzzGenerator.Generate(seed, index, clean: true), VegaLiteFuzzGenerator.Generate(seed, index + 100_000, clean: true), rng),
            };
            var text = spec.ToJsonString();
            var result = VegaLiteFuzzHarness.Run(text, fixtureDirectory, index % 8 == 0, CaseTimeout, Ct);
            tallies[result.Outcome] = tallies.GetValueOrDefault(result.Outcome) + 1;
            if (result.Detail is { } detail)
            {
                var reason = result.Outcome + ": " + System.Text.RegularExpressions.Regex.Replace(detail, "[0-9]+", "#");
                reasons[reason.Length > 140 ? reason[..140] : reason] = reasons.GetValueOrDefault(reason.Length > 140 ? reason[..140] : reason) + 1;
            }
            if (result.Failure is null)
            {
                continue;
            }

            if (failures.Count < 10)
            {
                failures.Add($"kind {kind}, seed {seed}, index {index} ({result.Outcome}): {result.Failure}\n--- specification\n{text}");
            }

            if (!string.IsNullOrEmpty(dump))
            {
                Directory.CreateDirectory(dump);
                File.WriteAllText(Path.Combine(dump, $"fuzz-{kind}-{seed}-{index}.vl.json"), text);
            }
        }

        var summary = $"Fuzz {kind}: {count} cases, seed {seed}: {Describe(tallies)}";
        TestContext.Current.SendDiagnosticMessage(summary);
        if (!string.IsNullOrEmpty(dump))
        {
            Directory.CreateDirectory(dump);
            File.AppendAllText(Path.Combine(dump, "fuzz-summary.txt"), summary + "\n" + string.Join("\n", reasons.OrderByDescending(r => r.Value).ThenBy(r => r.Key, StringComparer.Ordinal).Take(25).Select(r => "    " + r.Value + " x " + r.Key)) + "\n");
        }

        return tallies;
    }

    /// <summary>
    ///     Writes the outcome counts as text.
    /// </summary>
    /// <param name="tallies">The number of cases per outcome.</param>
    /// <returns>The text.</returns>
    private static string Describe(Dictionary<string, int> tallies) => string.Join(", ", tallies.OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => t.Key + "=" + t.Value));

    /// <summary>
    ///     Writes the failing cases as one message.
    /// </summary>
    /// <param name="failures">The descriptions.</param>
    /// <returns>The message.</returns>
    private static string FailureReport(List<string> failures) => failures.Count == 0 ? string.Empty : $"{failures.Count} failing case(s) (first ten shown):\n\n" + string.Join("\n\n=====\n\n", failures);
}

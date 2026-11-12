using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Cose;
using DemaConsulting.CanvasNet.Mermaid.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Cose;

/// <summary>
///     Unit tests of <see cref="CoseLayout"/> against the layouts the pinned Mermaid's cose-bilkent computes in Chrome
///     (<c>Fixtures/upstream-vectors/cose-layouts.json</c>, recorded by <c>tools/visual-diff/mermaid/gen-cose-goldens.mjs</c>).
/// </summary>
public class CoseGoldenTests
{
    /// <summary>
    ///     Gets the case indexes, as theory data.
    /// </summary>
    /// <returns>The indexes.</returns>
    public static TheoryData<int> Cases() => UpstreamVectors.Indexes(UpstreamVectors.CoseLayouts);

    /// <summary>
    ///     Validates that the layout of a golden tree gives Chrome's node centers, edge points and iteration count bit
    ///     for bit, or fails where Chrome's layout throws.
    /// </summary>
    /// <param name="index">The index of the case.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void CoseLayout_Run_GoldenTree_MatchesChrome(int index)
    {
        // Arrange: no work limit, so even the cases with millions of grid cells run to the end
        var golden = UpstreamVectors.CoseLayouts[index]!.AsObject();
        var (nodes, edges) = Input(golden);
        var meter = new LayoutWorkMeter(long.MaxValue, default, CancellationToken.None);

        // Act / Assert: a case Chrome could not lay out fails as a render failure
        if (golden["error"] is not null)
        {
            var ex = Assert.Throws<MermaidRenderException>(() => CoseLayout.Run(nodes, edges, meter));
            Assert.Equal(MermaidDiagnosticCodes.RenderFailed, ex.Code);
            return;
        }

        var result = CoseLayout.Run(nodes, edges, meter);

        // Assert: the iterations, every center and every edge point
        var name = (string)golden["name"]!;
        Assert.Equal((int)golden["iterations"]!, result.Iterations);
        var centers = golden["centers"]!.AsArray();
        Assert.Equal(centers.Count, result.Centers.Count);
        for (var i = 0; i < centers.Count; i++)
        {
            AssertSame(centers[i]![0], result.Centers[i].X, $"{name}: center {i} x");
            AssertSame(centers[i]![1], result.Centers[i].Y, $"{name}: center {i} y");
        }

        var points = golden["points"]!.AsArray();
        Assert.Equal(points.Count, result.Edges.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var edge = result.Edges[i];
            double[] actual = [edge.Start.X, edge.Start.Y, edge.Mid.X, edge.Mid.Y, edge.End.X, edge.End.Y];
            for (var k = 0; k < actual.Length; k++)
            {
                AssertSame(points[i]![k], actual[k], $"{name}: edge {i} point {k}");
            }
        }
    }

    /// <summary>
    ///     Reads the input of a golden case.
    /// </summary>
    /// <param name="golden">The case.</param>
    /// <returns>The node sizes and edges.</returns>
    internal static (CoseSize[] Nodes, CoseLink[] Edges) Input(JsonObject golden)
    {
        var nodes = golden["nodes"]!.AsArray()
            .Select(node => new CoseSize(Number(node![0]), Number(node[1])))
            .ToArray();
        var edges = golden["edges"]!.AsArray()
            .Select(edge => new CoseLink((int)edge![0]!, (int)edge[1]!))
            .ToArray();
        return (nodes, edges);
    }

    /// <summary>
    ///     Reads a number of the goldens: a JSON number, null for NaN, or a string for an infinity.
    /// </summary>
    /// <param name="node">The JSON value.</param>
    /// <returns>The number.</returns>
    internal static double Number(JsonNode? node) => node switch
    {
        null => double.NaN,
        JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.String =>
            double.Parse(value.GetValue<string>(), CultureInfo.InvariantCulture),
        _ => node.GetValue<double>(),
    };

    /// <summary>
    ///     Asserts that a coordinate equals the golden one exactly (<see cref="double.CompareTo(double)"/>: NaN equals
    ///     NaN, and -0 equals 0, which JSON writes as 0).
    /// </summary>
    /// <param name="expected">The golden value (null for NaN).</param>
    /// <param name="actual">The computed value.</param>
    /// <param name="what">What the value is, for the message.</param>
    private static void AssertSame(JsonNode? expected, double actual, string what)
    {
        var value = Number(expected);
        if (double.IsNaN(value))
        {
            Assert.True(double.IsNaN(actual), $"{what}: expected NaN, got {actual:R}");
            return;
        }

        Assert.True(value.CompareTo(actual) == 0, $"{what}: expected {value:R}, got {actual:R}");
    }
}

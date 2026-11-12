using System.Globalization;
using System.Numerics;
using DemaConsulting.CanvasNet.Mermaid.Cose;
using DemaConsulting.CanvasNet.Mermaid.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Cose;

/// <summary>
///     Unit tests of <see cref="CoseLayout"/>: the tree check, the placement, the iteration, the grid, the edge points,
///     the work charges and the correctly rounded math.
/// </summary>
public class CoseLayoutTests
{
    /// <summary>The work the layout of the two-node tree takes (pinned).</summary>
    private const long TwoNodeWork = 903;

    /// <summary>The work the layout of the path of ten nodes takes (pinned).</summary>
    private const long PathWork = 5_598;

    /// <summary>
    ///     Validates that an input that is not one tree (wrong edge count, an end outside the nodes, a cycle with a
    ///     disconnected node) is refused.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_NotOneTree_ThrowsArgumentException()
    {
        // Arrange
        var sizes = Sizes(3);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => CoseLayout.Run(sizes, [new CoseLink(0, 1)], Meter()));
        Assert.Throws<ArgumentException>(() => CoseLayout.Run(sizes, [new CoseLink(0, 1), new CoseLink(1, 3)], Meter()));
        Assert.Throws<ArgumentException>(() => CoseLayout.Run(sizes, [new CoseLink(0, 1), new CoseLink(1, 0)], Meter()));
        Assert.Throws<ArgumentException>(() => CoseLayout.Run(sizes, [new CoseLink(0, 1), new CoseLink(2, 2)], Meter()));
    }

    /// <summary>
    ///     Validates that no nodes give an empty layout without iterations.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_NoNodes_ReturnsEmpty()
    {
        // Act
        var result = CoseLayout.Run([], [], Meter());

        // Assert
        Assert.Empty(result.Centers);
        Assert.Empty(result.Edges);
        Assert.Equal(0, result.Iterations);
    }

    /// <summary>
    ///     Validates that a lone node feels no force and ends with its corner at the margin of 15 from the origin, after
    ///     the first convergence check.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_SingleNode_CornerAtMargin()
    {
        // Act
        var result = CoseLayout.Run([new CoseSize(60, 30)], [], Meter());

        // Assert
        Assert.Equal(new CosePoint(45, 30), result.Centers[0]);
        Assert.Equal(100, result.Iterations);
    }

    /// <summary>
    ///     Validates that two nodes are placed on a horizontal line (the child at 180 degrees from the center node) and
    ///     moved so the drawing's corner is at the margin.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_TwoNodes_HorizontalFromMargin()
    {
        // Act
        var result = CoseLayout.Run([new CoseSize(80, 40), new CoseSize(120, 40)], [new CoseLink(0, 1)], Meter());

        // Assert: the child lies left of the center node, both centers at the same height
        Assert.Equal(35, result.Centers[0].Y);
        Assert.Equal(35, result.Centers[1].Y);
        Assert.Equal(75, result.Centers[1].X);
        Assert.True(result.Centers[0].X > result.Centers[1].X + 100);
    }

    /// <summary>
    ///     Validates the center search of the radial layout: each pass keeps the nodes at odd positions, so the center
    ///     of a path in breadth-first order is not its middle node.
    /// </summary>
    [Fact]
    public void CoseLayout_FindCenterOfTree_Lists_KeepsOddPositions()
    {
        // Arrange
        var meter = Meter();

        // Act / Assert
        Assert.Equal(0, CoseLayout.FindCenterOfTree([0], meter));
        Assert.Equal(0, CoseLayout.FindCenterOfTree([0, 1], meter));
        Assert.Equal(1, CoseLayout.FindCenterOfTree([0, 1, 2], meter));
        Assert.Equal(1, CoseLayout.FindCenterOfTree([0, 1, 2, 3, 4], meter));
        Assert.Equal(3, CoseLayout.FindCenterOfTree(Enumerable.Range(0, 8).ToArray(), meter));
        Assert.Equal(7, CoseLayout.FindCenterOfTree(Enumerable.Range(0, 20).ToArray(), meter));
    }

    /// <summary>
    ///     Validates the start of the cooling schedule: a cooling factor of 1 up to 1000 nodes, lower beyond (at least
    ///     0.33), and an iteration limit of 2500 up to 500 nodes, five per node beyond.
    /// </summary>
    [Fact]
    public void CoseLayout_CoolingStart_NodeCounts_FollowsAdaptation()
    {
        // Act / Assert
        Assert.Equal((1.0, 2500.0), CoseLayout.CoolingStart(1));
        Assert.Equal((1.0, 2500.0), CoseLayout.CoolingStart(500));
        Assert.Equal((1.0, 2505.0), CoseLayout.CoolingStart(501));
        Assert.Equal((1.0, 5000.0), CoseLayout.CoolingStart(1000));
        Assert.Equal((1.0 - (1.0 / 4000 * (1 - 0.33)), 5005.0), CoseLayout.CoolingStart(1001));
        Assert.Equal((0.33, 30000.0), CoseLayout.CoolingStart(6000));
    }

    /// <summary>
    ///     Validates JavaScript's parseInt of a whole number: numbers from 1e21 on keep only their first digit.
    /// </summary>
    [Fact]
    public void CoseLayout_ParseInt_WholeNumbers_FollowsJavaScript()
    {
        // Act / Assert
        Assert.Equal(12, CoseLayout.ParseInt(12));
        Assert.Equal(-7, CoseLayout.ParseInt(-7));
        Assert.Equal(999999999999999900000.0, CoseLayout.ParseInt(999999999999999900000.0));
        Assert.Equal(1, CoseLayout.ParseInt(1e21));
        Assert.Equal(2, CoseLayout.ParseInt(2.5e22));
        Assert.Equal(-3, CoseLayout.ParseInt(-3e25));
        Assert.True(double.IsPositive(CoseLayout.ParseInt(-0.0)));
        Assert.True(double.IsNaN(CoseLayout.ParseInt(double.NaN)));
        Assert.True(double.IsNaN(CoseLayout.ParseInt(double.PositiveInfinity)));
    }

    /// <summary>
    ///     Validates that a corner beyond the world boundary is clamped to it when the layout moves the nodes, as
    ///     layout-base's LNode.transform does, so a wide lone node does not end at the margin.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_BeyondWorldBoundary_ClampsCorner()
    {
        // Act
        var result = CoseLayout.Run([new CoseSize(3e6, 40)], [], Meter());

        // Assert: without the clamp the center would be 15 + 1.5e6
        Assert.Equal(new CosePoint(1998800, 35), result.Centers[0]);
    }

    /// <summary>
    ///     Validates that a node outside the repulsion grid (which a JavaScript TypeError stops in Mermaid) and a grid
    ///     JavaScript cannot build (a RangeError) fail the layout as render failures.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_GridFailures_ThrowsRenderFailed()
    {
        // Arrange
        var location = new MermaidLocation("m.mmd", 2, 3);

        // Act
        var outside = Assert.Throws<MermaidRenderException>(() =>
            CoseLayout.Run([new CoseSize(1e24, 40), new CoseSize(50, 40)], [new CoseLink(0, 1)], Meter(location: location)));
        var invalid = Assert.Throws<MermaidRenderException>(() =>
            CoseLayout.Run([new CoseSize(double.PositiveInfinity, 40), new CoseSize(50, 40)], [new CoseLink(0, 1)], Meter(location: location)));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, outside.Code);
        Assert.Contains("outside the repulsion grid", outside.Message, StringComparison.Ordinal);
        Assert.Equal(location, outside.Location);
        Assert.Equal(MermaidDiagnosticCodes.RenderFailed, invalid.Code);
        Assert.Contains("cannot be built", invalid.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates cytoscape's points of a straight edge: the ends 15 pixels from the centers toward each other and the
    ///     middle point between them.
    /// </summary>
    [Fact]
    public void CoseLayout_StraightEdgePoints_ApartCenters_EndsOnCircles()
    {
        // Act
        var points = CoseLayout.StraightEdgePoints(new CosePoint(0, 0), new CosePoint(100, 0));
        var diagonal = CoseLayout.StraightEdgePoints(new CosePoint(10, 20), new CosePoint(40, 60));

        // Assert
        Assert.Equal(new CoseEdgePoints(new CosePoint(15, 0), new CosePoint(50, 0), new CosePoint(85, 0)), points);
        Assert.Equal(19, diagonal.Start.X, 12);
        Assert.Equal(32, diagonal.Start.Y, 12);
        Assert.Equal(31, diagonal.End.X, 12);
        Assert.Equal(48, diagonal.End.Y, 12);
        Assert.Equal(25, diagonal.Mid.X, 12);
        Assert.Equal(40, diagonal.Mid.Y, 12);
    }

    /// <summary>
    ///     Validates that centers closer than 15 pixels (or exactly 15 apart) give NaN points, cytoscape's bad line,
    ///     which the renderer drops.
    /// </summary>
    [Fact]
    public void CoseLayout_StraightEdgePoints_CloseCenters_NaN()
    {
        // Act
        var close = CoseLayout.StraightEdgePoints(new CosePoint(0, 0), new CosePoint(10, 0));
        var touching = CoseLayout.StraightEdgePoints(new CosePoint(0, 0), new CosePoint(15, 0));

        // Assert
        Assert.True(double.IsNaN(close.Start.X) && double.IsNaN(close.Mid.Y) && double.IsNaN(close.End.X));
        Assert.True(double.IsNaN(touching.Start.X) && double.IsNaN(touching.End.X));
    }

    /// <summary>
    ///     Validates the pinned work of two layouts: the charges are deterministic.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_SmallTrees_ChargesPinnedWork()
    {
        // Arrange
        var two = Meter();
        var path = Meter();

        // Act
        CoseLayout.Run([new CoseSize(80, 40), new CoseSize(120, 40)], [new CoseLink(0, 1)], two);
        CoseLayout.Run(Sizes(10), Enumerable.Range(1, 9).Select(i => new CoseLink(i - 1, i)).ToArray(), path);

        // Assert
        Assert.Equal(TwoNodeWork, two.Used);
        Assert.Equal(PathWork, path.Used);
    }

    /// <summary>
    ///     Validates that the work limit stops the layout with a located limit failure.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_SmallBudget_LimitExceeded()
    {
        // Arrange
        var location = new MermaidLocation("m.mmd", 1, 1);

        // Act
        var ex = Assert.Throws<MermaidRenderException>(() =>
            CoseLayout.Run(Sizes(10), Enumerable.Range(1, 9).Select(i => new CoseLink(0, i)).ToArray(), Meter(1000, location)));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(location, ex.Location);
    }

    /// <summary>
    ///     Validates that a canceled call stops the layout.
    /// </summary>
    [Fact]
    public void CoseLayout_Run_Canceled_ThrowsOperationCanceled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var meter = new LayoutWorkMeter(long.MaxValue, default, source.Token);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => CoseLayout.Run(Sizes(3), [new CoseLink(0, 1), new CoseLink(0, 2)], meter));
    }

    /// <summary>
    ///     Validates the special cases of the sine and cosine: signed zeros, and the domain.
    /// </summary>
    [Fact]
    public void CoseLayout_SinCos_SpecialCases_FollowJavaScriptAndDomain()
    {
        // Act / Assert
        Assert.True(double.IsNegative(CoseLayout.Sin(-0.0, out _)));
        Assert.Equal(0.0, CoseLayout.Sin(0.0, out _));
        Assert.Equal(1.0, CoseLayout.Cos(-0.0, out _));
        Assert.Equal(-CoseLayout.Sin(1.5, out _), CoseLayout.Sin(-1.5, out _));
        Assert.Equal(CoseLayout.Cos(1.5, out _), CoseLayout.Cos(-1.5, out _));
        Assert.Equal(double.Epsilon, CoseLayout.Sin(double.Epsilon, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Sin(double.NaN, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Sin(8.5, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Cos(double.NegativeInfinity, out _));
    }

    /// <summary>
    ///     Validates the special cases of the logarithm: exactly 0 for 1, subnormal arguments, and the domain.
    /// </summary>
    [Fact]
    public void CoseLayout_Log_SpecialCases_FollowJavaScriptAndDomain()
    {
        // Act / Assert
        Assert.Equal(0.0, CoseLayout.Log(1, out var steps));
        Assert.Equal(0, steps);
        Assert.Equal(-744.4400719213812, CoseLayout.Log(double.Epsilon, out _));
        Assert.Equal(709.782712893384, CoseLayout.Log(double.MaxValue, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Log(0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Log(-1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Log(double.PositiveInfinity, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Log(double.NaN, out _));
    }

    /// <summary>
    ///     Validates the special cases of the power, in V8's order, exact powers, overflow and underflow, and the domain.
    /// </summary>
    [Fact]
    public void CoseLayout_Pow_SpecialCases_FollowV8AndDomain()
    {
        // Act / Assert: V8's and ECMAScript's special cases
        Assert.True(double.IsNaN(CoseLayout.Pow(2, double.NaN, out _)));
        Assert.True(double.IsNaN(CoseLayout.Pow(-1, double.PositiveInfinity, out _)));
        Assert.Equal(9, CoseLayout.Pow(-3, 2, out _));
        Assert.Equal(3, CoseLayout.Pow(9, 0.5, out _));
        Assert.Equal(double.PositiveInfinity, CoseLayout.Pow(double.NegativeInfinity, 0.5, out _));
        Assert.True(double.IsPositive(CoseLayout.Pow(-0.0, 0.5, out _)));
        Assert.Equal(1, CoseLayout.Pow(double.NaN, 0, out _));
        Assert.True(double.IsNaN(CoseLayout.Pow(double.NaN, 3, out _)));
        Assert.Equal(1, CoseLayout.Pow(1, 1234.5, out _));

        // Exact powers, over- and underflow
        Assert.Equal(1024, CoseLayout.Pow(2, 10, out _));
        Assert.Equal(8, CoseLayout.Pow(4, 1.5, out _));
        Assert.Equal(0.1, CoseLayout.Pow(10, -1, out _));
        Assert.Equal(double.PositiveInfinity, CoseLayout.Pow(2, 1100, out _));
        Assert.Equal(0.0, CoseLayout.Pow(2, -1100, out _));
        Assert.Equal(double.Epsilon, CoseLayout.Pow(2, -1074, out _));
        Assert.Equal(double.PositiveInfinity, CoseLayout.Pow(1.5, 1e20, out _));
        Assert.Equal(0.0, CoseLayout.Pow(1.5, -1e20, out _));

        // The domain
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Pow(-2, 3, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Pow(0, 3, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.Pow(2, double.PositiveInfinity, out _));
    }

    /// <summary>
    ///     Validates rounding to the nearest double: ties to even, subnormal results, overflow, and the sign of zero.
    /// </summary>
    [Fact]
    public void CoseLayout_RoundToDouble_Values_RoundToNearestEven()
    {
        // Arrange: 2^53 + 1 and 2^53 + 3 are ties between neighbors 2 apart
        var big = BigInteger.One << 53;

        // Act / Assert
        Assert.Equal(9007199254740992.0, CoseLayout.RoundToDouble(big + 1, 0));
        Assert.Equal(9007199254740996.0, CoseLayout.RoundToDouble(big + 3, 0));
        Assert.Equal(-9007199254740996.0, CoseLayout.RoundToDouble(-(big + 3), 0));
        Assert.Equal(double.Epsilon, CoseLayout.RoundToDouble(3, -1076));
        Assert.Equal(0.0, CoseLayout.RoundToDouble(1, -1076));
        Assert.True(double.IsNegative(CoseLayout.RoundToDouble(-1, -1076)));
        Assert.Equal(double.PositiveInfinity, CoseLayout.RoundToDouble(1, 1024));
        Assert.Equal(double.MaxValue, CoseLayout.RoundToDouble((BigInteger.One << 53) - 1, 971));
    }

    /// <summary>
    ///     Validates Ziv's escalation: a value whose first approximation straddles a rounding midpoint is decided at the
    ///     next precision; one that stays on the midpoint cannot be rounded.
    /// </summary>
    [Fact]
    public void CoseLayout_RoundCorrectly_NearMidpoint_Escalates()
    {
        // Arrange: the exact value 1 + 2^-53 + 2^-300 lies just above the midpoint between 1 and the next double;
        // within 2^24 units of the first precision it cannot be told from the midpoint, at the second it can
        static (BigInteger Value, int Exponent) NearMidpoint(int precision)
        {
            var scale = precision + 30;
            var value = (BigInteger.One << scale) + (BigInteger.One << (scale - 53)) + (BigInteger.One << (scale - 300));
            return (value, -scale);
        }

        static (BigInteger Value, int Exponent) OnMidpoint(int precision) =>
            ((BigInteger.One << 53) + 1, 0);

        // Act
        var result = CoseLayout.RoundCorrectly(NearMidpoint, out var steps);

        // Assert
        Assert.Equal(1.0000000000000002, result);
        Assert.Equal(1, steps);
        Assert.Throws<ArgumentOutOfRangeException>(() => CoseLayout.RoundCorrectly(OnMidpoint, out _));
    }

    /// <summary>
    ///     Validates that the sine and cosine equal Chrome's, bit for bit, on every angle of the math vectors.
    /// </summary>
    [Fact]
    public void CoseLayout_SinCos_ChromeVectors_BitEqual()
    {
        // Arrange
        var vectors = UpstreamVectors.JsMath["trig"]!.AsArray();
        var mismatches = new List<string>();

        // Act
        foreach (var vector in vectors)
        {
            var x = Bits((string)vector![0]!);
            var sin = CoseLayout.Sin(x, out _);
            var cos = CoseLayout.Cos(x, out _);
            if (Hex(sin) != (string)vector[1]! || Hex(cos) != (string)vector[2]!)
            {
                mismatches.Add($"{x:R}: sin {Hex(sin)} (Chrome {vector[1]}), cos {Hex(cos)} (Chrome {vector[2]})");
            }
        }

        // Assert
        Assert.True(vectors.Count > 20000);
        Assert.Empty(mismatches);
    }

    /// <summary>
    ///     Validates that the logarithm equals Chrome's, bit for bit, on every argument of the math vectors.
    /// </summary>
    [Fact]
    public void CoseLayout_Log_ChromeVectors_BitEqual()
    {
        // Arrange
        var vectors = UpstreamVectors.JsMath["log"]!.AsArray();
        var mismatches = new List<string>();

        // Act
        foreach (var vector in vectors)
        {
            var x = Bits((string)vector![0]!);
            var log = CoseLayout.Log(x, out _);
            if (Hex(log) != (string)vector[1]!)
            {
                mismatches.Add($"{x:R}: {Hex(log)} (Chrome {vector[1]})");
            }
        }

        // Assert
        Assert.True(vectors.Count > 5000);
        Assert.Empty(mismatches);
    }

    /// <summary>
    ///     Validates that the power equals Chrome's, bit for bit, on every cooling pair of the math vectors.
    /// </summary>
    [Fact]
    public void CoseLayout_Pow_ChromeCoolingPairs_BitEqual()
    {
        // Arrange
        var vectors = UpstreamVectors.JsMath["pow"]!.AsArray();
        var mismatches = new List<string>();

        // Act
        foreach (var vector in vectors)
        {
            var x = Bits((string)vector![0]!);
            var y = Bits((string)vector[1]!);
            var pow = CoseLayout.Pow(x, y, out _);
            if (Hex(pow) != (string)vector[2]!)
            {
                mismatches.Add($"{x:R}^{y:R}: {Hex(pow)} (Chrome {vector[2]})");
            }
        }

        // Assert
        Assert.True(vectors.Count > 800);
        Assert.Empty(mismatches);
    }

    /// <summary>
    ///     Creates a work meter.
    /// </summary>
    /// <param name="maxWork">The work limit.</param>
    /// <param name="location">The location of failures.</param>
    /// <returns>The meter.</returns>
    private static LayoutWorkMeter Meter(long maxWork = 50_000_000, MermaidLocation location = default) =>
        new(maxWork, location, CancellationToken.None);

    /// <summary>
    ///     Creates node sizes: widths from 60 in steps of 10, height 40.
    /// </summary>
    /// <param name="count">The number of nodes.</param>
    /// <returns>The sizes.</returns>
    private static CoseSize[] Sizes(int count) =>
        Enumerable.Range(0, count).Select(i => new CoseSize(60 + (10 * i), 40)).ToArray();

    /// <summary>
    ///     Reads a double from its hexadecimal bits.
    /// </summary>
    /// <param name="hex">The 16 hexadecimal digits.</param>
    /// <returns>The double.</returns>
    private static double Bits(string hex) =>
        BitConverter.Int64BitsToDouble(long.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    /// <summary>
    ///     Writes the hexadecimal bits of a double.
    /// </summary>
    /// <param name="value">The double.</param>
    /// <returns>The 16 hexadecimal digits.</returns>
    private static string Hex(double value) =>
        BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);
}

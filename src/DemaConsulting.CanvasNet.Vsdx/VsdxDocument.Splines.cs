using System.Globalization;
using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio NURBS NURBSTo PolylineTo SplineStart SplineKnot knotLast xType yType Cox

/// <summary>
///     Implements the <see cref="VsdxDocument"/> curve-row resolvers that <c>VsdxDocument.Geometry.cs</c>
///     delegates to: <c>PolylineTo</c> (a <c>POLYLINE(...)</c> formula), <c>NURBSTo</c> (a
///     <c>NURBS(...)</c> formula) and the <c>SplineStart</c>/<c>SplineKnot</c> row group. NURBS and
///     spline rows are evaluated as a clamped, rational Cox-de Boor B-spline and flattened into
///     <see cref="PathBuilder.LineTo"/> segments. Every input size is hard-capped so a hostile
///     file cannot make resolution allocate or loop without bound.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>The most control points a single polyline, NURBS or spline run may declare; beyond this the row is reported and skipped (or the run truncated).</summary>
    internal const int MaxCurveControlPoints = 1024;

    /// <summary>The highest B-spline degree evaluated (the Visio documentation allows 1 to 25).</summary>
    internal const int MaxCurveDegree = 25;

    /// <summary>The most line segments a single flattened curve may produce.</summary>
    internal const int MaxCurveSegments = 4096;

    /// <summary>The minimum number of flattening segments per curve.</summary>
    private const int MinCurveSegments = 16;

    /// <summary>The flattening segments generated per control point.</summary>
    private const int SegmentsPerControlPoint = 16;

    /// <summary>The in-progress state of a <c>SplineStart</c>/<c>SplineKnot</c> row group.</summary>
    private sealed class SplineAccumulator
    {
        /// <summary>Gets the control points; the first is the pen position when the spline began.</summary>
        public List<Vector2> Points { get; } = [];

        /// <summary>Gets the knots in Visio order (first, second, then one per <c>SplineKnot</c>).</summary>
        public List<double> Knots { get; } = [];

        /// <summary>Gets or sets the last knot (<c>SplineStart</c> cell <c>C</c>).</summary>
        public double LastKnot { get; set; }

        /// <summary>Gets or sets the degree (<c>SplineStart</c> cell <c>D</c>).</summary>
        public int Degree { get; set; }
    }

    /// <summary>Begins a spline at the <c>SplineStart</c> row <paramref name="cells"/>, with <paramref name="pen"/> as its first control point.</summary>
    /// <param name="pen">The pen position, the spline's first control point.</param>
    /// <param name="cells">The <c>SplineStart</c> row cells.</param>
    /// <returns>The new accumulator.</returns>
    private static SplineAccumulator BeginSpline(Vector2 pen, VsdxCellBag cells)
    {
        var spline = new SplineAccumulator
        {
            LastKnot = cells.GetDouble("C"),
            Degree = (int)Math.Clamp(Math.Round(cells.GetDouble("D", 3d)), 1d, MaxCurveDegree),
        };
        spline.Points.Add(pen);
        spline.Points.Add(ToLocalPoint(cells.GetDouble("X"), cells.GetDouble("Y")));
        spline.Knots.Add(cells.GetDouble("B"));
        spline.Knots.Add(cells.GetDouble("A"));
        return spline;
    }

    /// <summary>Adds the <c>SplineKnot</c> row <paramref name="cells"/> to <paramref name="spline"/>, reporting and ignoring it past the control-point cap.</summary>
    /// <param name="spline">The in-progress spline.</param>
    /// <param name="cells">The <c>SplineKnot</c> row cells.</param>
    private static void AddSplineKnot(SplineAccumulator spline, VsdxCellBag cells)
    {
        if (spline.Points.Count >= MaxCurveControlPoints)
        {
            ReportUnsupported(
                "vsdx-geometry-row-SplineKnot",
                $"A spline has more than {MaxCurveControlPoints} control points; the excess points were skipped.");
            return;
        }

        spline.Points.Add(ToLocalPoint(cells.GetDouble("X"), cells.GetDouble("Y")));
        spline.Knots.Add(cells.GetDouble("A"));
    }

    /// <summary>Flattens a finished spline into <paramref name="builder"/> and returns the new pen position.</summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="spline">The finished spline.</param>
    /// <returns>The last control point (the new pen position).</returns>
    private static Vector2 FlushSpline(PathBuilder builder, SplineAccumulator spline)
    {
        var knots = new List<double>(spline.Knots) { spline.LastKnot };
        AppendNurbs(builder, spline.Points, null, knots, spline.Degree);
        return spline.Points[^1];
    }

    /// <summary>
    ///     Appends a <c>NURBSTo</c> row (end point <c>X</c>/<c>Y</c> with knot <c>A</c> and weight
    ///     <c>B</c>; first knot <c>C</c> and first weight <c>D</c> for the pen position; the
    ///     remaining points, knots and the last knot in the <c>E</c> cell's
    ///     <c>NURBS(knotLast, degree, xType, yType, x, y, knot, weight, ...)</c> formula, where an
    ///     <c>xType</c>/<c>yType</c> of 0 means a fraction of <c>Width</c>/<c>Height</c> and 1 means
    ///     local coordinates).
    /// </summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="start">The pen position (first control point).</param>
    /// <param name="cells">The row's cells.</param>
    /// <param name="width">The shape's width in inches.</param>
    /// <param name="height">The shape's height in inches.</param>
    /// <param name="end">The row's end point when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the row was resolved; <see langword="false"/> when its formula is missing, malformed or over the caps (nothing was appended).</returns>
    private static bool TryAppendNurbsTo(PathBuilder builder, Vector2 start, VsdxCellBag cells, double width, double height, out Vector2 end)
    {
        end = start;
        if (!TryGetFormulaArguments(cells, "E", "NURBS", out var args) ||
            args.Count < 4 || (args.Count - 4) % 4 != 0 || (args.Count - 4) / 4 + 2 > MaxCurveControlPoints)
        {
            return false;
        }

        var xScale = args[2] == 0d ? width : 1d;
        var yScale = args[3] == 0d ? height : 1d;
        var points = new List<Vector2> { start };
        var weights = new List<double> { cells.GetDouble("D", 1d) };
        var knots = new List<double> { cells.GetDouble("C") };
        for (var i = 4; i < args.Count; i += 4)
        {
            points.Add(ToLocalPoint(args[i] * xScale, args[i + 1] * yScale));
            knots.Add(args[i + 2]);
            weights.Add(args[i + 3]);
        }

        end = ToLocalPoint(cells.GetDouble("X"), cells.GetDouble("Y"));
        points.Add(end);
        knots.Add(cells.GetDouble("A"));
        weights.Add(cells.GetDouble("B", 1d));
        knots.Add(args[0]);

        AppendNurbs(builder, points, weights, knots, (int)Math.Clamp(Math.Round(args[1]), 1d, MaxCurveDegree));
        return true;
    }

    /// <summary>
    ///     Appends a <c>PolylineTo</c> row: the vertices of the <c>A</c> cell's
    ///     <c>POLYLINE(xType, yType, x1, y1, ...)</c> formula (0 means a fraction of
    ///     <c>Width</c>/<c>Height</c>, 1 local coordinates) as line segments, then the row's
    ///     <c>X</c>/<c>Y</c> end point when it differs from the last vertex.
    /// </summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="cells">The row's cells.</param>
    /// <param name="width">The shape's width in inches.</param>
    /// <param name="height">The shape's height in inches.</param>
    /// <param name="end">The row's end point when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the row was resolved; <see langword="false"/> when its formula is missing, malformed or over the cap (nothing was appended).</returns>
    private static bool TryAppendPolylineTo(PathBuilder builder, VsdxCellBag cells, double width, double height, out Vector2 end)
    {
        end = Vector2.Zero;
        if (!TryGetFormulaArguments(cells, "A", "POLYLINE", out var args) ||
            args.Count < 2 || (args.Count - 2) % 2 != 0 || (args.Count - 2) / 2 > MaxCurveControlPoints)
        {
            return false;
        }

        var xScale = args[0] == 0d ? width : 1d;
        var yScale = args[1] == 0d ? height : 1d;
        end = ToLocalPoint(cells.GetDouble("X"), cells.GetDouble("Y"));
        var last = (Vector2?)null;
        for (var i = 2; i < args.Count; i += 2)
        {
            last = ToLocalPoint(args[i] * xScale, args[i + 1] * yScale);
            builder.LineTo(last.Value);
        }

        if (last != end)
        {
            builder.LineTo(end);
        }

        return true;
    }

    /// <summary>Parses the numeric arguments of a <c>NAME(a, b, ...)</c> formula held in cell <paramref name="cellName"/>.</summary>
    /// <param name="cells">The row's cells.</param>
    /// <param name="cellName">The cell holding the formula.</param>
    /// <param name="functionName">The expected function name.</param>
    /// <param name="args">The parsed arguments when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the formula is present, has the expected function name, and every argument is a finite number.</returns>
    private static bool TryGetFormulaArguments(VsdxCellBag cells, string cellName, string functionName, out List<double> args)
    {
        args = [];
        if (!cells.TryGet(cellName, out var cell))
        {
            return false;
        }

        var text = cell.Formula is { Length: > 0 } formula && !cell.IsInherited ? formula : cell.Value;
        text = text.Trim();
        if (!text.StartsWith(functionName, StringComparison.OrdinalIgnoreCase) || !text.EndsWith(')'))
        {
            return false;
        }

        var open = text.IndexOf('(');
        if (open < 0 || text[functionName.Length..open].Trim().Length != 0)
        {
            return false;
        }

        var body = text[(open + 1)..^1];
        // Each argument needs at least a comma, so the text length bounds the argument count.
        if (body.Length > MaxCurveControlPoints * 128)
        {
            return false;
        }

        foreach (var part in body.Split(','))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            {
                return false;
            }

            args.Add(value);
        }

        return true;
    }

    /// <summary>
    ///     Evaluates a rational B-spline and appends it to <paramref name="builder"/> as line
    ///     segments. The Visio knot list holds one knot per control point plus the last knot
    ///     (<c>n + 1</c> values); it is padded with the last knot to the <c>n + degree + 1</c>
    ///     values a clamped B-spline needs. Degenerate input (decreasing knots, an empty parameter
    ///     range) degrades to a polyline through the control points and is reported.
    /// </summary>
    /// <param name="builder">The path builder to append to.</param>
    /// <param name="points">The control points, the first being the pen position.</param>
    /// <param name="weights">The control-point weights, or <see langword="null"/> for all 1.</param>
    /// <param name="knots">The Visio knot list.</param>
    /// <param name="requestedDegree">The requested degree.</param>
    private static void AppendNurbs(
        PathBuilder builder,
        IReadOnlyList<Vector2> points,
        IReadOnlyList<double>? weights,
        List<double> knots,
        int requestedDegree)
    {
        var count = points.Count;
        var degree = Math.Min(requestedDegree, count - 1);
        var target = count + degree + 1;
        while (knots.Count < target)
        {
            knots.Add(knots[^1]);
        }

        var samples = TryFlattenNurbs(points, weights, knots, degree);
        if (samples is null)
        {
            ReportUnsupported(
                "vsdx-geometry-spline-approximated",
                "A spline or NURBS row has invalid knots or weights; it is drawn as a polyline through its control points.");
            samples = points.Skip(1).ToList();
        }

        foreach (var sample in samples)
        {
            builder.LineTo(sample);
        }
    }

    /// <summary>Evaluates the clamped rational B-spline at evenly spaced parameters over its valid domain.</summary>
    /// <param name="points">The control points.</param>
    /// <param name="weights">The weights, or <see langword="null"/> for all 1.</param>
    /// <param name="knots">The full knot vector (at least <c>n + degree + 1</c> values).</param>
    /// <param name="degree">The degree, at most <c>n - 1</c>.</param>
    /// <returns>The sampled points, or <see langword="null"/> when the knots or weights are invalid.</returns>
    private static List<Vector2>? TryFlattenNurbs(IReadOnlyList<Vector2> points, IReadOnlyList<double>? weights, List<double> knots, int degree)
    {
        var n = points.Count;
        for (var i = 0; i < n + degree + 1; i++)
        {
            if (!double.IsFinite(knots[i]) || (i > 0 && knots[i] < knots[i - 1]))
            {
                return null;
            }
        }

        var w = new double[n];
        for (var i = 0; i < n; i++)
        {
            w[i] = weights is null ? 1d : weights[i];
            if (!double.IsFinite(w[i]) || w[i] <= 0d)
            {
                w[i] = 1d;
            }
        }

        var low = knots[degree];
        var high = knots[n];
        if (high <= low)
        {
            return null;
        }

        // Rightmost non-empty knot span at or below n - 1, used for the closed end of the domain.
        var lastSpan = n - 1;
        while (lastSpan > degree && knots[lastSpan] >= knots[lastSpan + 1])
        {
            lastSpan--;
        }

        var segments = Math.Clamp(n * SegmentsPerControlPoint, MinCurveSegments, MaxCurveSegments);
        var result = new List<Vector2>(segments);
        var span = degree;
        for (var s = 1; s <= segments; s++)
        {
            var t = s == segments ? high : low + ((high - low) * s / segments);
            if (s == segments)
            {
                span = lastSpan;
            }
            else
            {
                while (span < n - 1 && t >= knots[span + 1])
                {
                    span++;
                }
            }

            result.Add(EvaluateDeBoor(points, w, knots, degree, span, t));
        }

        return result;
    }

    /// <summary>Evaluates a rational B-spline at parameter <paramref name="t"/> in knot span <paramref name="span"/> with de Boor's algorithm on homogeneous coordinates.</summary>
    /// <param name="points">The control points.</param>
    /// <param name="w">The positive weights.</param>
    /// <param name="knots">The knot vector.</param>
    /// <param name="degree">The degree.</param>
    /// <param name="span">The knot span index containing <paramref name="t"/>.</param>
    /// <param name="t">The parameter.</param>
    /// <returns>The curve point.</returns>
    private static Vector2 EvaluateDeBoor(IReadOnlyList<Vector2> points, double[] w, List<double> knots, int degree, int span, double t)
    {
        var x = new double[degree + 1];
        var y = new double[degree + 1];
        var h = new double[degree + 1];
        for (var j = 0; j <= degree; j++)
        {
            var index = j + span - degree;
            x[j] = points[index].X * w[index];
            y[j] = points[index].Y * w[index];
            h[j] = w[index];
        }

        for (var r = 1; r <= degree; r++)
        {
            for (var j = degree; j >= r; j--)
            {
                var left = knots[j + span - degree];
                var denominator = knots[j + 1 + span - r] - left;
                var alpha = denominator <= 0d ? 0d : (t - left) / denominator;
                x[j] = ((1d - alpha) * x[j - 1]) + (alpha * x[j]);
                y[j] = ((1d - alpha) * y[j - 1]) + (alpha * y[j]);
                h[j] = ((1d - alpha) * h[j - 1]) + (alpha * h[j]);
            }
        }

        return new Vector2((float)(x[degree] / h[degree]), (float)(y[degree] / h[degree]));
    }
}

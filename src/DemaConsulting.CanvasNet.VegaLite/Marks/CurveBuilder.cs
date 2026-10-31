// Portions derived from d3-shape (ISC), d3-path (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <summary>
///     The interpolation of a path mark: the curves of Vega-Lite's <c>interpolate</c> property (Vega's <c>curves()</c>, which are the
///     d3-shape curves).
/// </summary>
internal enum CurveKind
{
    /// <summary>Straight segments (<c>linear</c>).</summary>
    Linear,

    /// <summary>Straight segments and a closing segment (<c>linear-closed</c>).</summary>
    LinearClosed,

    /// <summary>A step that changes at the middle between two points (<c>step</c>).</summary>
    Step,

    /// <summary>A step that changes at the first point of each interval (<c>step-before</c>).</summary>
    StepBefore,

    /// <summary>A step that changes at the second point of each interval (<c>step-after</c>).</summary>
    StepAfter,

    /// <summary>A uniform cubic B-spline (<c>basis</c>).</summary>
    Basis,

    /// <summary>A uniform cubic B-spline that does not touch the ends (<c>basis-open</c>).</summary>
    BasisOpen,

    /// <summary>A closed uniform cubic B-spline (<c>basis-closed</c>).</summary>
    BasisClosed,

    /// <summary>A straightened B-spline (<c>bundle</c>); the <c>tension</c> is its <c>beta</c> (default 0.85).</summary>
    Bundle,

    /// <summary>A cardinal spline (<c>cardinal</c>); the <c>tension</c> is its tension (default 0).</summary>
    Cardinal,

    /// <summary>A cardinal spline that does not touch the ends (<c>cardinal-open</c>).</summary>
    CardinalOpen,

    /// <summary>A closed cardinal spline (<c>cardinal-closed</c>).</summary>
    CardinalClosed,

    /// <summary>A Catmull-Rom spline (<c>catmull-rom</c>); the <c>tension</c> is its <c>alpha</c> (default 0.5).</summary>
    CatmullRom,

    /// <summary>A monotone cubic that is a function of x (<c>monotone</c>, vertical orientation).</summary>
    MonotoneX,

    /// <summary>A monotone cubic that is a function of y (<c>monotone</c> of a horizontal area).</summary>
    MonotoneY,

    /// <summary>A natural cubic spline (<c>natural</c>).</summary>
    Natural,
}

/// <summary>
///     A vertex of a line in pixels.
/// </summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
/// <param name="Defined">
///     Whether the vertex exists; an undefined vertex (or one with a coordinate that is not finite) ends the current subpath.
/// </param>
internal readonly record struct CurvePoint(double X, double Y, bool Defined);

/// <summary>
///     A vertex of an area in pixels.
/// </summary>
/// <param name="Along">The coordinate along the axis of the area: x of a vertical area, y of a horizontal one.</param>
/// <param name="Top">The coordinate of the value edge across: y1 of a vertical area, x1 of a horizontal one.</param>
/// <param name="Base">The coordinate of the baseline across: y0 of a vertical area, x0 of a horizontal one.</param>
/// <param name="Defined">
///     Whether the vertex exists; an undefined vertex (or one with a coordinate that is not finite) ends the current run.
/// </param>
internal readonly record struct AreaPoint(double Along, double Top, double Base, bool Defined);

/// <summary>
///     Generates the outline of a line or an area through a list of pixel points: the d3-shape <c>line</c> and <c>area</c>
///     generators with their curves, as Vega's <c>line</c> and <c>area</c> marks use them.
/// </summary>
/// <remarks>
///     <para>
///         <b>Origin.</b> The curve classes are literal ports of the d3-shape curves (<c>linear</c>, <c>linearClosed</c>,
///         <c>step</c>, <c>basis</c>, <c>basisOpen</c>, <c>basisClosed</c>, <c>bundle</c>, <c>cardinal*</c>, <c>catmullRom</c>,
///         <c>monotone</c>, <c>natural</c>) including the state machines <c>lineStart</c>, <c>point</c>, <c>lineEnd</c>,
///         <c>areaStart</c> and <c>areaEnd</c> and the toggle (<c>_line</c>) that makes the baseline pass of an area continue
///         the path of the top line; <see cref="Line"/> and <see cref="Area"/> are the generators of <c>line.js</c> and
///         <c>area.js</c>. The path context is a <see cref="List{T}"/> of <see cref="ScenePathSegment"/>: <c>moveTo</c>,
///         <c>lineTo</c>, <c>bezierCurveTo</c> and <c>closePath</c> of d3-path. As in d3-path a close is written only after a
///         move.
///     </para>
///     <para>
///         <b>Names and tension.</b> Vega's <c>curves()</c> maps the <c>tension</c> property to the <c>beta</c> of a bundle
///         (default 0.85), to the tension of a cardinal curve (default 0) and to the <c>alpha</c> of a Catmull-Rom curve
///         (default 0.5; 0 gives a cardinal curve); the other curves ignore it. d3 accepts any number as a tension and so does Vega-Lite
///         (its schema has no range), so the tension is not range-checked, neither in a mark definition nor in the configuration; a
///         <c>tension</c> that is not finite counts as absent, and a finite one cannot produce an unbounded drawing because the path
///         context bounds every coordinate it writes (see <b>Totality and limits</b>). <see cref="TryParse"/> accepts exactly the 15 names of Vega-Lite's <c>Interpolate</c>; the Vega-only
///         <c>catmull-rom-open</c> and <c>catmull-rom-closed</c> are not Vega-Lite values and are not implemented.
///     </para>
///     <para>
///         <b>Area.</b> d3 gives the <c>bundle</c> curve no <c>areaStart</c>/<c>areaEnd</c>, so an area cannot use it
///         (<see cref="SupportsArea"/>); <see cref="Area"/> throws <see cref="NotSupportedException"/> for it and the caller
///         substitutes a supported curve.
///     </para>
///     <para>
///         <b>Totality and limits.</b> A point with a coordinate that is not finite, or whose magnitude is above
///         <see cref="ScenePainter.MaxMagnitude"/> (the one bound that the painter applies to everything it draws), is treated as undefined and
///         ends the current subpath. Values that arise inside a curve (an overflow of huge coordinates, the long tangents of a huge tension) are
///         never written: a move or line to a point that is not drawable is skipped and a cubic with a control point that is not drawable becomes a
///         line to its end point. The
///         generators poll the cancellation token every 1,024 points. <see cref="MaxSegments"/> is an upper bound of the
///         number of segments that the generators write, so that the caller can charge the budget before generating.
///     </para>
/// </remarks>
internal static partial class CurveBuilder
{
    /// <summary>The number of points between two polls of the cancellation token (a power of two).</summary>
    private const int PollInterval = 1024;

    /// <summary>The tolerance of the Catmull-Rom length test (<c>epsilon</c> of d3-shape's <c>math.js</c>).</summary>
    private const double Epsilon = 1e-12;

    /// <summary>The default <c>beta</c> of the bundle curve.</summary>
    private const double DefaultBeta = 0.85;

    /// <summary>The default <c>alpha</c> of the Catmull-Rom curve.</summary>
    private const double DefaultAlpha = 0.5;

    /// <summary>
    ///     Resolves the name of an interpolation (one of the 15 values of Vega-Lite's <c>Interpolate</c>).
    /// </summary>
    /// <param name="name">The name; the comparison is case-sensitive.</param>
    /// <param name="horizontalArea">
    ///     Whether the mark is an area with a horizontal orientation; <c>monotone</c> is then <see cref="CurveKind.MonotoneY"/>.
    /// </param>
    /// <param name="kind">The curve when the name is known.</param>
    /// <returns><see langword="true"/> when the name is one of the 15 names.</returns>
    public static bool TryParse(string? name, bool horizontalArea, out CurveKind kind)
    {
        kind = CurveKind.Linear;
        switch (name)
        {
            case "linear": kind = CurveKind.Linear; return true;
            case "linear-closed": kind = CurveKind.LinearClosed; return true;
            case "step": kind = CurveKind.Step; return true;
            case "step-before": kind = CurveKind.StepBefore; return true;
            case "step-after": kind = CurveKind.StepAfter; return true;
            case "basis": kind = CurveKind.Basis; return true;
            case "basis-open": kind = CurveKind.BasisOpen; return true;
            case "basis-closed": kind = CurveKind.BasisClosed; return true;
            case "bundle": kind = CurveKind.Bundle; return true;
            case "cardinal": kind = CurveKind.Cardinal; return true;
            case "cardinal-open": kind = CurveKind.CardinalOpen; return true;
            case "cardinal-closed": kind = CurveKind.CardinalClosed; return true;
            case "catmull-rom": kind = CurveKind.CatmullRom; return true;
            case "natural": kind = CurveKind.Natural; return true;
            case "monotone":
                // Vega-Lite's monotone is a function of x unless the area is horizontal
                kind = horizontalArea ? CurveKind.MonotoneY : CurveKind.MonotoneX;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     Tells whether an area can be drawn with a curve.
    /// </summary>
    /// <param name="kind">The curve.</param>
    /// <returns><see langword="false"/> for <see cref="CurveKind.Bundle"/>, which has no area methods in d3-shape.</returns>
    public static bool SupportsArea(CurveKind kind) => kind != CurveKind.Bundle;

    /// <summary>
    ///     Computes an upper bound of the number of segments that <see cref="Line"/> or <see cref="Area"/> writes.
    /// </summary>
    /// <remarks>
    ///     The bound is <c>factor * points * (area ? 2 : 1) + extra</c> with a factor of 2 for the step curves (two lines per
    ///     interval) and 1 otherwise. <c>extra</c> covers the moves, closes and end pieces that a run writes besides its points, and
    ///     depends on the curve and on whether an area is drawn (<see cref="RunExtras"/>). The proof has three parts.
    ///     (1) The runs are independent: every run starts with <c>lineStart</c> and ends with <c>lineEnd</c>, and the segment count of a
    ///     d3-shape curve does not depend on the coordinates (they only choose the control points; the sink can drop a segment, never add one).
    ///     (2) The curves are state machines with at most four states (no point, one, two, three or more), so the count of a run of
    ///     <c>n</c> points is <c>factor * n * (area ? 2 : 1) + e(n)</c> with <c>e</c> constant for <c>n</c> of 3 or more; the test
    ///     <c>CurveBuilder_MaxSegments_IsAnUpperBoundForEveryKind</c> sweeps every run pattern up to 12 points for every curve, for lines and areas,
    ///     and the table was taken from that sweep. A <c>bundle</c> with a <c>tension</c> of 1 is a basis curve, so <c>bundle</c> has the
    ///     numbers of <c>basis</c>. (3) With <c>D</c> points in <c>R</c> runs, a run of three or more points has two points more than the
    ///     one that every run has, so at most <c>(D - R) / 2</c> runs are that long; the others (one or two points) write at most
    ///     <see cref="RunExtras"/> <c>small</c> extra segments, the long ones at most <c>big</c>. The sum is largest when as many runs as possible are long.
    ///     The runs of isolated points (<c>D = R</c>) are therefore charged only <c>small</c> each (one for a line of ordinary curves). The
    ///     caller must count the points that are defined for the generators, that is the points whose coordinates
    ///     <see cref="ScenePainter.IsBounded(double)"/> accepts: a point that the generators treat as undefined would split a run
    ///     and could write more than the bound. A run that has points
    ///     counts as at least one run, and there are never more runs than points. The result saturates at <see cref="long.MaxValue"/>.
    /// </remarks>
    /// <param name="kind">The curve.</param>
    /// <param name="definedPoints">The number of defined points (each point is used once by a line, twice by an area).</param>
    /// <param name="runs">The number of runs of consecutive defined points.</param>
    /// <param name="area">Whether the outline of an area is generated.</param>
    /// <returns>The bound; 0 when there are no points.</returns>
    public static long MaxSegments(CurveKind kind, long definedPoints, long runs, bool area)
    {
        // Nothing to draw without points
        if (definedPoints <= 0)
        {
            return 0;
        }

        // A huge input saturates instead of overflowing
        const long limit = long.MaxValue / 16;
        if (definedPoints > limit || runs > limit)
        {
            return long.MaxValue;
        }

        // The step curves write up to two lines per interval, an area writes every point twice
        var perPoint = (kind is CurveKind.Step or CurveKind.StepBefore or CurveKind.StepAfter ? 2L : 1L) * (area ? 2L : 1L);
        var runCount = Math.Clamp(runs, 1L, definedPoints);
        var (small, big) = RunExtras(kind, area);

        // A run of three or more points has at least two points beyond the first, so at most (D - R) / 2 runs are that long
        var longRuns = Math.Min(runCount, (definedPoints - runCount) / 2);
        return (perPoint * definedPoints) + (big * longRuns) + (small * (runCount - longRuns));
    }

    /// <summary>
    ///     Gets the most segments, beyond <c>factor * n * (area ? 2 : 1)</c>, that one run of <c>n</c> points writes.
    /// </summary>
    /// <remarks>
    ///     The values are the maxima, over the runs of up to 12 points, of the count that the generators write less the points part, and never
    ///     less than 0. For a line: a move for the first point of the ordinary curves (1 for linear, cardinal, Catmull-Rom, monotone and natural),
    ///     the close of the closed curves, the end pieces of basis (a curve and a line, 2). The open curves write fewer segments than their points and have 0.
    ///     For an area the baseline pass continues the path of the top line and the outline is closed, which adds one (two for the closed curves; five
    ///     for a basis curve with three points or more, whose two passes have the two end pieces each).
    /// </remarks>
    /// <param name="kind">The curve.</param>
    /// <param name="area">Whether the outline of an area is generated.</param>
    /// <returns>The extra segments of a run of one or two points (<c>small</c>) and of any run (<c>big</c>, at least <c>small</c>).</returns>
    private static (long Small, long Big) RunExtras(CurveKind kind, bool area) => (kind, area) switch
    {
        (CurveKind.Step, false) or (CurveKind.StepBefore, _) or (CurveKind.StepAfter, _) => (0, 0),
        (CurveKind.Step, true) => (1, 1),
        (CurveKind.BasisOpen, _) or (CurveKind.CardinalOpen, _) => (0, 0),
        (CurveKind.LinearClosed or CurveKind.BasisClosed or CurveKind.CardinalClosed, false) => (1, 1),
        (CurveKind.LinearClosed or CurveKind.BasisClosed or CurveKind.CardinalClosed, true) => (2, 2),
        (CurveKind.Basis or CurveKind.Bundle, false) => (1, 2),
        (CurveKind.Basis or CurveKind.Bundle, true) => (1, 5),
        _ => (1, 1),
    };

    /// <summary>
    ///     Creates the curve object of a kind.
    /// </summary>
    /// <param name="kind">The curve.</param>
    /// <param name="tension">The <c>tension</c> property or <see langword="null"/>.</param>
    /// <param name="sink">The path context.</param>
    /// <returns>The new curve.</returns>
    private static Curve Create(CurveKind kind, double? tension, SegmentSink sink)
    {
        // A tension that is not a finite number is treated as absent
        var value = tension is { } t && double.IsFinite(t) ? t : (double?)null;
        switch (kind)
        {
            case CurveKind.LinearClosed:
                return new LinearClosedCurve(sink);
            case CurveKind.Step:
                return new StepCurve(sink, 0.5);
            case CurveKind.StepBefore:
                return new StepCurve(sink, 0);
            case CurveKind.StepAfter:
                return new StepCurve(sink, 1);
            case CurveKind.Basis:
                return new BasisCurve(sink);
            case CurveKind.BasisOpen:
                return new BasisOpenCurve(sink);
            case CurveKind.BasisClosed:
                return new BasisClosedCurve(sink);
            case CurveKind.Bundle:
                {
                    // A beta of 1 is the basis curve itself (as in d3)
                    var beta = value ?? DefaultBeta;
                    return beta.CompareTo(1) == 0 ? new BasisCurve(sink) : new BundleCurve(sink, beta);
                }

            case CurveKind.Cardinal:
                return new CardinalCurve(sink, value ?? 0);
            case CurveKind.CardinalOpen:
                return new CardinalOpenCurve(sink, value ?? 0);
            case CurveKind.CardinalClosed:
                return new CardinalClosedCurve(sink, value ?? 0);
            case CurveKind.CatmullRom:
                {
                    // An alpha of 0 is the cardinal curve without tension (as in d3)
                    var alpha = value ?? DefaultAlpha;
                    return alpha == 0 ? new CardinalCurve(sink, 0) : new CatmullRomCurve(sink, alpha);
                }

            case CurveKind.MonotoneX:
                return new MonotoneCurve(sink, false);
            case CurveKind.MonotoneY:
                return new MonotoneCurve(sink, true);
            case CurveKind.Natural:
                return new NaturalCurve(sink);
            default:
                return new LinearCurve(sink);
        }
    }

    /// <summary>
    ///     The path context of d3-path that the curves draw on: it appends <see cref="ScenePathSegment"/> values and never writes
    ///     a coordinate that is not finite.
    /// </summary>
    private sealed class SegmentSink
    {
        /// <summary>The list that receives the segments.</summary>
        private readonly List<ScenePathSegment> _output;

        /// <summary>Whether a move has been written (d3-path writes a close only then).</summary>
        private bool _hasCurrentPoint;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SegmentSink"/> class.
        /// </summary>
        /// <param name="output">The list that receives the segments.</param>
        public SegmentSink(List<ScenePathSegment> output) => _output = output;

        /// <summary>
        ///     Starts a subpath (<c>moveTo</c>).
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public void MoveTo(double x, double y)
        {
            if (ScenePainter.IsBounded(x, y))
            {
                _output.Add(ScenePathSegment.Move(x, y));
                _hasCurrentPoint = true;
            }
        }

        /// <summary>
        ///     Adds a straight segment (<c>lineTo</c>).
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public void LineTo(double x, double y)
        {
            if (ScenePainter.IsBounded(x, y))
            {
                _output.Add(ScenePathSegment.Line(x, y));
            }
        }

        /// <summary>
        ///     Adds a cubic Bezier segment (<c>bezierCurveTo</c>).
        /// </summary>
        /// <param name="x1">The x coordinate of the first control point.</param>
        /// <param name="y1">The y coordinate of the first control point.</param>
        /// <param name="x2">The x coordinate of the second control point.</param>
        /// <param name="y2">The y coordinate of the second control point.</param>
        /// <param name="x">The x coordinate of the end point.</param>
        /// <param name="y">The y coordinate of the end point.</param>
        public void CubicTo(double x1, double y1, double x2, double y2, double x, double y)
        {
            // A control point that is not finite or is beyond the painter's magnitude bound (a huge tension makes the
            // tangents of a cardinal or Catmull-Rom curve as long as it likes) falls back to a straight line to the end point
            if (ScenePainter.IsBounded(x1, y1, x2, y2))
            {
                if (ScenePainter.IsBounded(x, y))
                {
                    _output.Add(ScenePathSegment.Cubic(x1, y1, x2, y2, x, y));
                }
            }
            else
            {
                LineTo(x, y);
            }
        }

        /// <summary>
        ///     Closes the subpath (<c>closePath</c>); nothing is written before the first move.
        /// </summary>
        public void Close()
        {
            if (_hasCurrentPoint)
            {
                _output.Add(ScenePathSegment.Close());
            }
        }
    }

    /// <summary>
    ///     A d3-shape curve: the state machine that turns the points of a line into segments.
    /// </summary>
    private abstract class Curve
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Curve"/> class.
        /// </summary>
        /// <param name="sink">The path context.</param>
        protected Curve(SegmentSink sink) => Out = sink;

        /// <summary>Gets the path context.</summary>
        protected SegmentSink Out { get; }

        /// <summary>
        ///     Gets or sets the area pass (<c>_line</c> of d3): NaN outside an area, 0 for the top line and 1 for the baseline.
        /// </summary>
        protected double Pass { get; set; } = double.NaN;

        /// <summary>Gets a value indicating whether <c>_line</c> is truthy in d3 (the baseline pass continues the path).</summary>
        protected bool Continues => Pass != 0 && !double.IsNaN(Pass);

        /// <summary>
        ///     Tells whether <c>lineEnd</c> closes the subpath: <c>_line || (_line !== 0 &amp;&amp; _point === n)</c>.
        /// </summary>
        /// <param name="point">The state of the point counter.</param>
        /// <param name="only">The counter value at which a line (outside the area top pass) is closed.</param>
        /// <returns><see langword="true"/> when the path is closed.</returns>
        protected bool ClosesAt(int point, int only) => Pass != 0 && (!double.IsNaN(Pass) || point == only);

        /// <summary>Starts an area (<c>areaStart</c>).</summary>
        public virtual void AreaStart() => Pass = 0;

        /// <summary>Ends an area (<c>areaEnd</c>).</summary>
        public virtual void AreaEnd() => Pass = double.NaN;

        /// <summary>Starts a line (<c>lineStart</c>).</summary>
        public abstract void LineStart();

        /// <summary>Ends a line (<c>lineEnd</c>).</summary>
        public abstract void LineEnd();

        /// <summary>
        ///     Adds a point (<c>point</c>).
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public abstract void Point(double x, double y);

        /// <summary>
        ///     Writes the first point of a line: a line to it on the baseline pass, otherwise a move.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        protected void Begin(double x, double y)
        {
            if (Continues)
            {
                Out.LineTo(x, y);
            }
            else
            {
                Out.MoveTo(x, y);
            }
        }

        /// <summary>Flips the area pass at the end of a line (<c>this._line = 1 - this._line</c>).</summary>
        protected void FlipPass() => Pass = 1 - Pass;
    }

    /// <summary>The <c>linear</c> curve.</summary>
    private sealed class LinearCurve : Curve
    {
        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="LinearCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        public LinearCurve(SegmentSink sink)
            : base(sink)
        {
        }

        /// <inheritdoc/>
        public override void LineStart() => _point = 0;

        /// <inheritdoc/>
        public override void LineEnd()
        {
            if (ClosesAt(_point, 1))
            {
                Out.Close();
            }

            FlipPass();
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            if (_point == 0)
            {
                _point = 1;
                Begin(x, y);
            }
            else
            {
                _point = 2;
                Out.LineTo(x, y);
            }
        }
    }

    /// <summary>The <c>linearClosed</c> curve (no area passes).</summary>
    private sealed class LinearClosedCurve : Curve
    {
        /// <summary>Whether a point has been added.</summary>
        private bool _started;

        /// <summary>Initializes a new instance of the <see cref="LinearClosedCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        public LinearClosedCurve(SegmentSink sink)
            : base(sink)
        {
        }

        /// <inheritdoc/>
        public override void AreaStart()
        {
            // d3 uses a no-op: a closed curve is its own area
        }

        /// <inheritdoc/>
        public override void AreaEnd()
        {
            // d3 uses a no-op: a closed curve is its own area
        }

        /// <inheritdoc/>
        public override void LineStart() => _started = false;

        /// <inheritdoc/>
        public override void LineEnd()
        {
            if (_started)
            {
                Out.Close();
            }
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            if (_started)
            {
                Out.LineTo(x, y);
            }
            else
            {
                _started = true;
                Out.MoveTo(x, y);
            }
        }
    }

    /// <summary>The <c>step</c>, <c>stepBefore</c> and <c>stepAfter</c> curves.</summary>
    private sealed class StepCurve : Curve
    {
        /// <summary>The position of the change within an interval: 0 (before), 0.5 or 1 (after); flipped for the baseline pass.</summary>
        private double _t;

        /// <summary>The previous point.</summary>
        private double _x, _y;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="StepCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        /// <param name="t">The position of the change within an interval.</param>
        public StepCurve(SegmentSink sink, double t)
            : base(sink) => _t = t;

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x = _y = double.NaN;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            // The last interval of a centered step ends with a line to the final point
            if (_t is > 0 and < 1 && _point == 2)
            {
                Out.LineTo(_x, _y);
            }

            if (ClosesAt(_point, 1))
            {
                Out.Close();
            }

            // Within an area the baseline pass runs backward, so the change moves to the other end of the interval
            if (Pass >= 0)
            {
                _t = 1 - _t;
                FlipPass();
            }
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            if (_point == 0)
            {
                _point = 1;
                Begin(x, y);
            }
            else
            {
                _point = 2;
                if (_t <= 0)
                {
                    Out.LineTo(_x, y);
                    Out.LineTo(x, y);
                }
                else
                {
                    var x1 = (_x * (1 - _t)) + (x * _t);
                    Out.LineTo(x1, _y);
                    Out.LineTo(x1, y);
                }
            }

            _x = x;
            _y = y;
        }
    }

    /// <summary>The <c>basis</c> curve: a uniform cubic B-spline through the first and last point.</summary>
    private sealed class BasisCurve : Curve
    {
        /// <summary>The previous two points.</summary>
        private double _x0, _y0, _x1, _y1;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="BasisCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        public BasisCurve(SegmentSink sink)
            : base(sink)
        {
        }

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x0 = _x1 = _y0 = _y1 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            // The last knots: a curve to the last point (with three or more points) and a line to it
            if (_point == 3)
            {
                BasisSegment(Out, _x0, _y0, _x1, _y1, _x1, _y1);
            }

            if (_point is 2 or 3)
            {
                Out.LineTo(_x1, _y1);
            }

            if (ClosesAt(_point, 1))
            {
                Out.Close();
            }

            FlipPass();
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            switch (_point)
            {
                case 0:
                    _point = 1;
                    Begin(x, y);
                    break;
                case 1:
                    _point = 2;
                    break;
                case 2:
                    // The first knot is a sixth of the way: a line to it, then the first curve
                    _point = 3;
                    Out.LineTo(((5 * _x0) + _x1) / 6, ((5 * _y0) + _y1) / 6);
                    BasisSegment(Out, _x0, _y0, _x1, _y1, x, y);
                    break;
                default:
                    BasisSegment(Out, _x0, _y0, _x1, _y1, x, y);
                    break;
            }

            _x0 = _x1;
            _x1 = x;
            _y0 = _y1;
            _y1 = y;
        }
    }

    /// <summary>The <c>basisOpen</c> curve: a B-spline that starts and ends at the first and last knot.</summary>
    private sealed class BasisOpenCurve : Curve
    {
        /// <summary>The previous two points.</summary>
        private double _x0, _y0, _x1, _y1;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="BasisOpenCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        public BasisOpenCurve(SegmentSink sink)
            : base(sink)
        {
        }

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x0 = _x1 = _y0 = _y1 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            if (ClosesAt(_point, 3))
            {
                Out.Close();
            }

            FlipPass();
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            switch (_point)
            {
                case 0:
                    _point = 1;
                    break;
                case 1:
                    _point = 2;
                    break;
                case 2:
                    // The first knot of an open curve starts the path
                    _point = 3;
                    Begin(((_x0 + (4 * _x1)) + x) / 6, ((_y0 + (4 * _y1)) + y) / 6);
                    break;
                default:
                    _point = 4;
                    BasisSegment(Out, _x0, _y0, _x1, _y1, x, y);
                    break;
            }

            _x0 = _x1;
            _x1 = x;
            _y0 = _y1;
            _y1 = y;
        }
    }

    /// <summary>The <c>basisClosed</c> curve (no area passes).</summary>
    private sealed class BasisClosedCurve : Curve
    {
        /// <summary>The previous two points.</summary>
        private double _x0, _y0, _x1, _y1;

        /// <summary>The first three points of the loop, replayed at the end.</summary>
        private double _x2, _y2, _x3, _y3, _x4, _y4;

        /// <summary>The point counter.</summary>
        private int _point;

        /// <summary>Initializes a new instance of the <see cref="BasisClosedCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        public BasisClosedCurve(SegmentSink sink)
            : base(sink)
        {
        }

        /// <inheritdoc/>
        public override void AreaStart()
        {
            // d3 uses a no-op: a closed curve is its own area
        }

        /// <inheritdoc/>
        public override void AreaEnd()
        {
            // d3 uses a no-op: a closed curve is its own area
        }

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x0 = _x1 = _x2 = _x3 = _x4 = double.NaN;
            _y0 = _y1 = _y2 = _y3 = _y4 = double.NaN;
            _point = 0;
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            switch (_point)
            {
                case 1:
                    Out.MoveTo(_x2, _y2);
                    Out.Close();
                    break;
                case 2:
                    Out.MoveTo((_x2 + (2 * _x3)) / 3, (_y2 + (2 * _y3)) / 3);
                    Out.LineTo((_x3 + (2 * _x2)) / 3, (_y3 + (2 * _y2)) / 3);
                    Out.Close();
                    break;
                case 3:
                    // Replay the first three points to close the loop
                    Point(_x2, _y2);
                    Point(_x3, _y3);
                    Point(_x4, _y4);
                    break;
            }
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            switch (_point)
            {
                case 0:
                    _point = 1;
                    _x2 = x;
                    _y2 = y;
                    break;
                case 1:
                    _point = 2;
                    _x3 = x;
                    _y3 = y;
                    break;
                case 2:
                    _point = 3;
                    _x4 = x;
                    _y4 = y;
                    Out.MoveTo(((_x0 + (4 * _x1)) + x) / 6, ((_y0 + (4 * _y1)) + y) / 6);
                    break;
                default:
                    BasisSegment(Out, _x0, _y0, _x1, _y1, x, y);
                    break;
            }

            _x0 = _x1;
            _x1 = x;
            _y0 = _y1;
            _y1 = y;
        }
    }

    /// <summary>The <c>bundle</c> curve: the points are blended towards the straight line between the ends and fed to a basis curve.</summary>
    private sealed class BundleCurve : Curve
    {
        /// <summary>The basis curve that draws the blended points.</summary>
        private readonly BasisCurve _basis;

        /// <summary>The blending factor: 1 is the basis curve, 0 a straight line.</summary>
        private readonly double _beta;

        /// <summary>The buffered coordinates of the current line.</summary>
        private List<double> _x = [], _y = [];

        /// <summary>Initializes a new instance of the <see cref="BundleCurve"/> class.</summary>
        /// <param name="sink">The path context.</param>
        /// <param name="beta">The blending factor.</param>
        public BundleCurve(SegmentSink sink, double beta)
            : base(sink)
        {
            _basis = new BasisCurve(sink);
            _beta = beta;
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">Always: the bundle curve has no area methods in d3-shape.</exception>
        public override void AreaStart() => throw new NotSupportedException("The bundle curve cannot draw an area.");

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">Always: the bundle curve has no area methods in d3-shape.</exception>
        public override void AreaEnd() => throw new NotSupportedException("The bundle curve cannot draw an area.");

        /// <inheritdoc/>
        public override void LineStart()
        {
            _x = [];
            _y = [];
            _basis.LineStart();
        }

        /// <inheritdoc/>
        public override void LineEnd()
        {
            // With at least two points the blended points go to the basis curve
            var j = _x.Count - 1;
            if (j > 0)
            {
                var x0 = _x[0];
                var y0 = _y[0];
                var dx = _x[j] - x0;
                var dy = _y[j] - y0;
                for (var i = 0; i <= j; i++)
                {
                    var t = (double)i / j;
                    _basis.Point(
                        (_beta * _x[i]) + ((1 - _beta) * (x0 + (t * dx))),
                        (_beta * _y[i]) + ((1 - _beta) * (y0 + (t * dy))));
                }
            }

            _x = [];
            _y = [];
            _basis.LineEnd();
        }

        /// <inheritdoc/>
        public override void Point(double x, double y)
        {
            _x.Add(x);
            _y.Add(y);
        }
    }

    /// <summary>
    ///     Writes a segment of a uniform cubic B-spline (<c>point</c> of <c>basis.js</c>).
    /// </summary>
    /// <param name="sink">The path context.</param>
    /// <param name="x0">The x coordinate of the point before the previous one.</param>
    /// <param name="y0">The y coordinate of the point before the previous one.</param>
    /// <param name="x1">The x coordinate of the previous point.</param>
    /// <param name="y1">The y coordinate of the previous point.</param>
    /// <param name="x">The x coordinate of the new point.</param>
    /// <param name="y">The y coordinate of the new point.</param>
    private static void BasisSegment(SegmentSink sink, double x0, double y0, double x1, double y1, double x, double y) =>
        sink.CubicTo(
            ((2 * x0) + x1) / 3,
            ((2 * y0) + y1) / 3,
            (x0 + (2 * x1)) / 3,
            (y0 + (2 * y1)) / 3,
            ((x0 + (4 * x1)) + x) / 6,
            ((y0 + (4 * y1)) + y) / 6);
}

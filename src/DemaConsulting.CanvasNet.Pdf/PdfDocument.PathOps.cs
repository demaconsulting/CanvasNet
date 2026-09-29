using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The fixed fill/stroke color used by every path-painting operator in this phase.
    /// </summary>
    /// <remarks>
    ///     Phase 2 implements no color space or color-setting operator (<c>rg</c>, <c>g</c>,
    ///     <c>k</c>, <c>sc</c>/<c>scn</c>, and their stroking counterparts are all silently
    ///     skipped, like any other unrecognized operator) - every painted pixel is opaque black,
    ///     regardless of what color operators a real-world content stream may issue. This is a
    ///     documented, intentional, in-progress limitation of this phase, not a bug; a later phase
    ///     is expected to add real color support.
    /// </remarks>
    private static readonly Rgba32 OpaqueBlack = new(0, 0, 0, 255);

    /// <summary>
    ///     The minimum device-space (pixel) stroke line width this phase ever paints with,
    ///     implementing the PDF specification's "a line width of 0 shall be rendered as the
    ///     thinnest line that can be rendered at device resolution" rule (interpreted here as
    ///     exactly one device pixel). This also covers any positive but very thin user-space
    ///     width that would otherwise scale down to zero or a negative/invalid device width.
    /// </summary>
    private const float MinimumDeviceLineWidth = 1f;

    /// <summary>
    ///     The path currently under construction by the path-construction operators, for the
    ///     content stream currently being executed by <see cref="ExecuteContentStream"/>.
    ///     Cleared after every path-painting operator; the surrounding graphics state is
    ///     unaffected by that clear.
    /// </summary>
    private PathBuilder _pathBuilder = null!;

    /// <summary>
    ///     The current point, in <em>untransformed PDF user-space</em> coordinates (the CTM is
    ///     applied only when a point is actually baked into <see cref="_pathBuilder"/>) - tracked
    ///     separately from whatever point was last baked into the path builder because the
    ///     <c>v</c>/<c>y</c> curve-shorthand operators and <c>h</c>'s closepath both need the
    ///     current point in the same untransformed space the next operator's own operands arrive
    ///     in.
    /// </summary>
    private Vector2 _currentPoint;

    /// <summary>
    ///     The point, in untransformed PDF user-space coordinates, the current subpath most
    ///     recently started at (via <c>m</c> or <c>re</c>) - restored into
    ///     <see cref="_currentPoint"/> by <c>h</c>'s closepath.
    /// </summary>
    private Vector2 _subpathStart;

    /// <summary>
    ///     <see langword="true"/> once a subpath has been started (via <c>m</c>) and not yet
    ///     closed (via <c>h</c>, or implicitly by <c>re</c>'s own trailing closepath); mirrors
    ///     <see cref="Geometry.PathBuilder"/>'s own internal "can draw" state so a malformed
    ///     "draw before move" content stream is rejected with <see cref="InvalidDataException"/>
    ///     (this class's own convention for malformed content-derived input) rather than letting
    ///     <see cref="Geometry.PathBuilder"/>'s <see cref="InvalidOperationException"/> escape
    ///     unwrapped.
    /// </summary>
    private bool _hasOpenSubpath;

    /// <summary>Transforms a PDF user-space point into device pixel-space, via the current CTM.</summary>
    private Vector2 Transform(Vector2 userSpacePoint) => Vector2.Transform(userSpacePoint, _gs.CurrentTransform);

    private Vector2 Transform(double x, double y) => Transform(new Vector2((float)x, (float)y));

    /// <summary>Handles the <c>m x y</c> operator: starts a new subpath at <c>(x, y)</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 2 numbers.
    /// </exception>
    private void OpMoveTo(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "m", 2);
        var point = new Vector2((float)values[0], (float)values[1]);
        _pathBuilder.MoveTo(Transform(point));
        _currentPoint = point;
        _subpathStart = point;
        _hasOpenSubpath = true;
    }

    /// <summary>Handles the <c>l x y</c> operator: draws a line to <c>(x, y)</c>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 2 numbers, or when no
    ///     subpath is currently open.
    /// </exception>
    private void OpLineTo(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "l", 2);
        RequireOpenSubpath("l");
        var point = new Vector2((float)values[0], (float)values[1]);
        _pathBuilder.LineTo(Transform(point));
        _currentPoint = point;
    }

    /// <summary>
    ///     Handles the <c>c x1 y1 x2 y2 x3 y3</c> operator: draws a cubic Bezier curve to
    ///     <c>(x3, y3)</c> with the given control points.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 6 numbers, or when no
    ///     subpath is currently open.
    /// </exception>
    private void OpCurveTo(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "c", 6);
        RequireOpenSubpath("c");
        var control1 = new Vector2((float)values[0], (float)values[1]);
        var control2 = new Vector2((float)values[2], (float)values[3]);
        var end = new Vector2((float)values[4], (float)values[5]);
        _pathBuilder.CubicBezierTo(Transform(control1), Transform(control2), Transform(end));
        _currentPoint = end;
    }

    /// <summary>
    ///     Handles the <c>v x2 y2 x3 y3</c> operator: draws a cubic Bezier curve to
    ///     <c>(x3, y3)</c> whose first control point is the current point.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 4 numbers, or when no
    ///     subpath is currently open.
    /// </exception>
    private void OpCurveToV(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "v", 4);
        RequireOpenSubpath("v");
        var control2 = new Vector2((float)values[0], (float)values[1]);
        var end = new Vector2((float)values[2], (float)values[3]);
        _pathBuilder.CubicBezierTo(Transform(_currentPoint), Transform(control2), Transform(end));
        _currentPoint = end;
    }

    /// <summary>
    ///     Handles the <c>y x1 y1 x3 y3</c> operator: draws a cubic Bezier curve to
    ///     <c>(x3, y3)</c> whose second control point is the endpoint itself.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 4 numbers, or when no
    ///     subpath is currently open.
    /// </exception>
    private void OpCurveToY(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "y", 4);
        RequireOpenSubpath("y");
        var control1 = new Vector2((float)values[0], (float)values[1]);
        var end = new Vector2((float)values[2], (float)values[3]);
        _pathBuilder.CubicBezierTo(Transform(control1), Transform(end), Transform(end));
        _currentPoint = end;
    }

    /// <summary>Handles the <c>h</c> operator: closes the current subpath.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not empty, or when no subpath is currently
    ///     open.
    /// </exception>
    private void OpClosePath(IReadOnlyList<PdfObject> operands)
    {
        RequireOperandCount(operands, "h", 0);
        RequireOpenSubpath("h");
        _pathBuilder.Close();
        _currentPoint = _subpathStart;
        _hasOpenSubpath = false;
    }

    /// <summary>
    ///     Handles the <c>re x y w h</c> operator: appends a closed rectangular subpath, built
    ///     directly from four transformed corner points plus a closepath, equivalent to
    ///     <c>m</c>/<c>l</c>/<c>l</c>/<c>l</c>/<c>h</c> but without re-dispatching through those
    ///     handlers' own operand-stack expectations.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly 4 numbers.
    /// </exception>
    private void OpRectangle(IReadOnlyList<PdfObject> operands)
    {
        var values = RequireNumbers(operands, "re", 4);
        var x = values[0];
        var y = values[1];
        var w = values[2];
        var h = values[3];

        _pathBuilder.MoveTo(Transform(x, y));
        _pathBuilder.LineTo(Transform(x + w, y));
        _pathBuilder.LineTo(Transform(x + w, y + h));
        _pathBuilder.LineTo(Transform(x, y + h));
        _pathBuilder.Close();

        // Per spec, 're' always starts a fresh, independently-closed subpath: the current point
        // ends at (x, y), and the subpath is already closed (no further draw op may follow
        // without an intervening 'm'/'re').
        _currentPoint = new Vector2((float)x, (float)y);
        _subpathStart = _currentPoint;
        _hasOpenSubpath = false;
    }

    /// <summary>
    ///     Paints (and always clears) the current path per one of the ten path-painting
    ///     operators' documented fill-rule/stroke/close-first combination.
    /// </summary>
    /// <param name="fill"><see langword="true"/> if the path is filled.</param>
    /// <param name="fillRule">The fill rule to apply, when <paramref name="fill"/> is <see langword="true"/>.</param>
    /// <param name="stroke"><see langword="true"/> if the path is stroked.</param>
    /// <param name="closeFirst">
    ///     <see langword="true"/> to close the current (already device-space-baked) subpath
    ///     before painting (the <c>s</c>/<c>b</c>/<c>b*</c> operators' documented behavior).
    /// </param>
    private void PaintCurrentPath(bool fill, FillRule fillRule, bool stroke, bool closeFirst)
    {
        if (closeFirst && _hasOpenSubpath)
        {
            _pathBuilder.Close();
            _currentPoint = _subpathStart;
            _hasOpenSubpath = false;
        }

        var path = _pathBuilder.Build();

        if (fill)
        {
            PathFiller.Fill(_surface, path, OpaqueBlack, fillRule);
        }

        if (stroke)
        {
            var style = new StrokeStyle(
                DeviceLineWidth(),
                _gs.LineCap,
                _gs.LineJoin,
                Math.Max(_gs.MiterLimit, 1f),
                ScaledDashArray(),
                ScaledDashPhase());
            var outline = PathStroker.Stroke(path, style);
            PathFiller.Fill(_surface, outline, OpaqueBlack, FillRule.NonZero);
        }

        // Per spec, every path-painting operator (including 'n') always clears the current
        // path. The surrounding graphics state is entirely unaffected by this clear.
        _pathBuilder.Clear();
        _hasOpenSubpath = false;
    }

    /// <summary>
    ///     Computes the current CTM's geometric-mean device-space scale factor, used to convert a
    ///     user-space stroke width/dash length into device pixel-space.
    /// </summary>
    /// <remarks>
    ///     A non-uniformly scaled or skewed CTM (whether from the base CTM's own independent
    ///     X/Y requested-size scaling, or from a user <c>cm</c>) makes "the" stroke width
    ///     ill-defined in device space; this phase uses the well-established
    ///     <c>sqrt(|det(CTM.Linear)|)</c> approximation (the same one most simple PDF/SVG
    ///     renderers use), a documented Phase 2 simplification rather than an exactly anisotropic
    ///     stroke.
    /// </remarks>
    private float DeviceScale()
    {
        var m = _gs.CurrentTransform;
        var determinant = (m.M11 * m.M22) - (m.M12 * m.M21);
        return MathF.Sqrt(MathF.Abs(determinant));
    }

    /// <summary>
    ///     Computes the device-space (pixel) stroke width for the current graphics state's
    ///     user-space <see cref="GraphicsState.LineWidth"/>, applying <see cref="DeviceScale"/>
    ///     and the <see cref="MinimumDeviceLineWidth"/> floor.
    /// </summary>
    private float DeviceLineWidth() => MathF.Max(_gs.LineWidth * DeviceScale(), MinimumDeviceLineWidth);

    /// <summary>
    ///     Computes the device-space (pixel) dash array for the current graphics state's
    ///     user-space <see cref="GraphicsState.DashArray"/>, applying <see cref="DeviceScale"/>.
    /// </summary>
    /// <returns>
    ///     The scaled dash array, or <see langword="null"/> when the current graphics state has
    ///     no dash pattern (a solid stroke).
    /// </returns>
    private IReadOnlyList<float>? ScaledDashArray()
    {
        if (_gs.DashArray is null)
        {
            return null;
        }

        var scale = DeviceScale();
        var scaled = new float[_gs.DashArray.Count];
        for (var i = 0; i < scaled.Length; i++)
        {
            scaled[i] = _gs.DashArray[i] * scale;
        }

        return scaled;
    }

    /// <summary>
    ///     Computes the device-space (pixel) dash phase for the current graphics state's
    ///     user-space <see cref="GraphicsState.DashPhase"/>, applying <see cref="DeviceScale"/>.
    /// </summary>
    private float ScaledDashPhase() => _gs.DashPhase * DeviceScale();

    /// <summary>
    ///     Validates that a subpath is currently open, throwing when a path-construction operator
    ///     that requires one (every operator except <c>m</c> and <c>re</c>) is issued before the
    ///     first <c>m</c>/<c>re</c>, or after a preceding <c>h</c>/<c>re</c> without an
    ///     intervening <c>m</c>/<c>re</c>.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when no subpath is currently open.</exception>
    private void RequireOpenSubpath(string operatorName)
    {
        if (!_hasOpenSubpath)
        {
            throw new InvalidDataException(
                $"Operator '{operatorName}' requires an already-open subpath (a preceding 'm' or 're').");
        }
    }

    /// <summary>
    ///     Validates that <paramref name="operands"/> contains exactly <paramref name="count"/>
    ///     entries, for operators (such as the path-painting operators) that require an exact
    ///     operand count but no specific kind.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> does not contain exactly <paramref name="count"/>
    ///     entries.
    /// </exception>
    private static void RequireOperandCount(IReadOnlyList<PdfObject> operands, string operatorName, int count)
    {
        if (operands.Count != count)
        {
            throw new InvalidDataException(
                $"Operator '{operatorName}' requires exactly {count} operand(s); got {operands.Count}.");
        }
    }
}

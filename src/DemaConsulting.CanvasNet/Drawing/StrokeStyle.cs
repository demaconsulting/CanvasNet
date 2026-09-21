using System.Collections.ObjectModel;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     Describes the geometry used when converting a path stroke into fillable outline polygons.
/// </summary>
/// <remarks>
///     <see cref="StrokeStyle"/> is immutable and thread-safe after construction. A caller builds
///     one style once, then reuses it across repeated <see cref="PathStroker.Stroke"/> calls to
///     apply the same width, cap, join, miter-limit, and dash behavior to different paths.
/// </remarks>
public sealed class StrokeStyle
{
    /// <summary>
    ///     Initializes a new stroke-style snapshot.
    /// </summary>
    /// <param name="width">
    ///     The stroke width, in path-space units. Must be a finite value greater than zero.
    /// </param>
    /// <param name="cap">
    ///     The cap geometry applied to each exposed end of an open stroked segment. Must be a
    ///     defined <see cref="LineCap"/> value. Defaults to <see cref="LineCap.Butt"/>.
    /// </param>
    /// <param name="join">
    ///     The join geometry applied where consecutive stroked segments meet. Must be a defined
    ///     <see cref="LineJoin"/> value. Defaults to <see cref="LineJoin.Miter"/>.
    /// </param>
    /// <param name="miterLimit">
    ///     The SVG-style miter-limit ratio (<c>miterLength / strokeWidth</c>) above which a
    ///     miter join falls back to a bevel. Must be finite and greater than or equal to one.
    ///     Defaults to <c>4f</c>.
    /// </param>
    /// <param name="dashArray">
    ///     Optional dash lengths alternating on/off along the stroked path. A <see langword="null"/>
    ///     or empty list means a solid stroke. Every entry must be finite and greater than or
    ///     equal to zero, and the list must not consist entirely of zeros.
    /// </param>
    /// <param name="dashOffset">
    ///     The distance, in path-space units, into the dash pattern at which stroking starts.
    ///     Must be finite. Defaults to <c>0f</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="width"/> is not finite or is less than or equal to zero;
    ///     when <paramref name="cap"/> or <paramref name="join"/> is not a defined enum value;
    ///     when <paramref name="miterLimit"/> is not finite or is less than one; or when
    ///     <paramref name="dashOffset"/> is not finite.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="dashArray"/> contains a negative or non-finite entry, or
    ///     when every entry is zero.
    /// </exception>
    public StrokeStyle(
        float width,
        LineCap cap = LineCap.Butt,
        LineJoin join = LineJoin.Miter,
        float miterLimit = 4f,
        IReadOnlyList<float>? dashArray = null,
        float dashOffset = 0f)
    {
        if (!float.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), width, "Width must be a finite value greater than zero.");
        }

        if (!Enum.IsDefined<LineCap>(cap))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cap), cap, "Cap must be a defined LineCap value.");
        }

        if (!Enum.IsDefined<LineJoin>(join))
        {
            throw new ArgumentOutOfRangeException(
                nameof(join), join, "Join must be a defined LineJoin value.");
        }

        if (!float.IsFinite(miterLimit) || miterLimit < 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(miterLimit), miterLimit, "Miter limit must be a finite value greater than or equal to one.");
        }

        if (!float.IsFinite(dashOffset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dashOffset), dashOffset, "Dash offset must be a finite value.");
        }

        Width = width;
        Cap = cap;
        Join = join;
        MiterLimit = miterLimit;
        DashArray = CopyDashArray(dashArray);
        DashOffset = dashOffset;
    }

    /// <summary>
    ///     Gets the stroke width, in path-space units.
    /// </summary>
    public float Width { get; }

    /// <summary>
    ///     Gets the cap geometry applied to each exposed end of an open stroked segment.
    /// </summary>
    public LineCap Cap { get; }

    /// <summary>
    ///     Gets the join geometry applied where consecutive stroked segments meet.
    /// </summary>
    public LineJoin Join { get; }

    /// <summary>
    ///     Gets the SVG-style miter-limit ratio above which a miter falls back to a bevel.
    /// </summary>
    public float MiterLimit { get; }

    /// <summary>
    ///     Gets the optional alternating on/off dash lengths, or <see langword="null"/> for a
    ///     solid stroke.
    /// </summary>
    public IReadOnlyList<float>? DashArray { get; }

    /// <summary>
    ///     Gets the distance into the dash pattern at which stroking starts.
    /// </summary>
    public float DashOffset { get; }

    /// <summary>
    ///     Copies and validates <paramref name="dashArray"/>, producing an immutable snapshot or
    ///     <see langword="null"/> for a solid stroke.
    /// </summary>
    /// <param name="dashArray">The caller-supplied dash array.</param>
    /// <returns>The copied, read-only dash array snapshot, or <see langword="null"/>.</returns>
    private static IReadOnlyList<float>? CopyDashArray(IReadOnlyList<float>? dashArray)
    {
        if (dashArray == null || dashArray.Count == 0)
        {
            return null;
        }

        var values = new float[dashArray.Count];
        var anyPositive = false;
        for (var i = 0; i < dashArray.Count; i++)
        {
            var value = dashArray[i];
            if (!float.IsFinite(value) || value < 0f)
            {
                throw new ArgumentException(
                    "Dash array entries must be finite values greater than or equal to zero.",
                    nameof(dashArray));
            }

            if (value > 0f)
            {
                anyPositive = true;
            }

            values[i] = value;
        }

        if (!anyPositive)
        {
            throw new ArgumentException(
                "Dash array entries must not all be zero.",
                nameof(dashArray));
        }

        return new ReadOnlyCollection<float>(values);
    }
}

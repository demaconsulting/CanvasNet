using System.Numerics;

namespace DemaConsulting.CanvasNet.Geometry;

/// <summary>
///     Represents an axis-aligned bounding rectangle using the position-plus-size convention
///     (<see cref="X"/>, <see cref="Y"/>, <see cref="Width"/>, <see cref="Height"/>).
/// </summary>
/// <remarks>
///     <see cref="Empty"/> uses a WPF-style "union identity" sentinel
///     (<c>X = Y = float.PositiveInfinity</c>, <c>Width = Height = float.NegativeInfinity</c>)
///     rather than the more common all-zero "empty" convention (as used by, for example,
///     <c>System.Drawing.RectangleF.Empty</c>). This is a deliberate, load-bearing design choice:
///     it makes <see cref="Union(Rect,Rect)"/> an identity operation over <see cref="Empty"/>
///     (<c>Union(Empty, r) == r</c> for every rectangle <c>r</c>), which lets
///     <see cref="Path.GetBounds"/> fold <see cref="Union(Rect)"/> over zero or more subpaths and
///     always get the mathematically correct answer (an empty path's bounds are
///     <see cref="Empty"/>, and a one-subpath path's bounds are exactly that subpath's bounds)
///     with no special-casing for "no subpaths yet". An all-zero "empty" rectangle would instead
///     silently corrupt every union with a real rectangle containing the origin.
/// </remarks>
public readonly struct Rect : IEquatable<Rect>
{
    /// <summary>
    ///     The x-coordinate of the rectangle's top-left corner.
    /// </summary>
    public float X { get; }

    /// <summary>
    ///     The y-coordinate of the rectangle's top-left corner.
    /// </summary>
    public float Y { get; }

    /// <summary>
    ///     The width of the rectangle. May be negative only for the <see cref="Empty"/> sentinel;
    ///     see <see cref="IsEmpty"/>.
    /// </summary>
    public float Width { get; }

    /// <summary>
    ///     The height of the rectangle. May be negative only for the <see cref="Empty"/> sentinel;
    ///     see <see cref="IsEmpty"/>.
    /// </summary>
    public float Height { get; }

    /// <summary>
    ///     The empty rectangle, used as the identity element for <see cref="Union(Rect,Rect)"/>.
    /// </summary>
    /// <remarks>
    ///     Represented as <c>X = Y = float.PositiveInfinity</c>, <c>Width = Height =
    ///     float.NegativeInfinity</c> so that <see cref="Left"/>/<see cref="Top"/> are
    ///     <see cref="float.PositiveInfinity"/> and <see cref="Right"/>/<see cref="Bottom"/> are
    ///     <see cref="float.NegativeInfinity"/> - every real rectangle's bounds fall strictly
    ///     inside this "inverted" extent, so unioning with any real rectangle always yields
    ///     exactly that rectangle. See the type-level remarks for why this convention (rather than
    ///     an all-zero rectangle) was chosen.
    /// </remarks>
    public static readonly Rect Empty = new(
        float.PositiveInfinity,
        float.PositiveInfinity,
        float.NegativeInfinity,
        float.NegativeInfinity);

    /// <summary>
    ///     The x-coordinate of the rectangle's left edge. Equal to <see cref="X"/>.
    /// </summary>
    public float Left => X;

    /// <summary>
    ///     The y-coordinate of the rectangle's top edge. Equal to <see cref="Y"/>.
    /// </summary>
    public float Top => Y;

    /// <summary>
    ///     The x-coordinate of the rectangle's right edge. Equal to <see cref="X"/> + <see cref="Width"/>.
    /// </summary>
    /// <remarks>
    ///     Guards against the <c>PositiveInfinity + NegativeInfinity == NaN</c> IEEE 754 pitfall
    ///     that would otherwise occur for <see cref="Empty"/> (whose <see cref="X"/> is
    ///     <see cref="float.PositiveInfinity"/> and whose <see cref="Width"/> is
    ///     <see cref="float.NegativeInfinity"/>): when <see cref="Width"/> is negative infinity,
    ///     the result is defined to be <see cref="float.NegativeInfinity"/> directly, matching the
    ///     documented "inverted extent" behavior <see cref="Empty"/> relies on.
    /// </remarks>
    public float Right => float.IsNegativeInfinity(Width) ? float.NegativeInfinity : X + Width;

    /// <summary>
    ///     The y-coordinate of the rectangle's bottom edge. Equal to <see cref="Y"/> + <see cref="Height"/>.
    /// </summary>
    /// <remarks>
    ///     Guards against the same <c>PositiveInfinity + NegativeInfinity == NaN</c> pitfall as
    ///     <see cref="Right"/>; see its remarks for details.
    /// </remarks>
    public float Bottom => float.IsNegativeInfinity(Height) ? float.NegativeInfinity : Y + Height;

    /// <summary>
    ///     The rectangle's top-left corner position, as a vector.
    /// </summary>
    public Vector2 Location => new(X, Y);

    /// <summary>
    ///     The rectangle's size, as a vector.
    /// </summary>
    public Vector2 Size => new(Width, Height);

    /// <summary>
    ///     The rectangle's top-left corner. Equal to <see cref="Location"/>.
    /// </summary>
    public Vector2 TopLeft => new(X, Y);

    /// <summary>
    ///     The rectangle's bottom-right corner.
    /// </summary>
    public Vector2 BottomRight => new(Right, Bottom);

    /// <summary>
    ///     <see langword="true"/> if this rectangle is the <see cref="Empty"/> sentinel (or any
    ///     other rectangle with a negative <see cref="Width"/>); otherwise, <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///     Only <see cref="Width"/> is tested (not <see cref="Height"/>) because
    ///     <see cref="Empty"/>'s construction always keeps both negative together; testing one
    ///     field is sufficient and matches the WPF convention this type follows.
    /// </remarks>
    public bool IsEmpty => Width < 0;

    /// <summary>
    ///     Initializes a new rectangle with the specified position and size.
    /// </summary>
    /// <param name="x">The x-coordinate of the top-left corner.</param>
    /// <param name="y">The y-coordinate of the top-left corner.</param>
    /// <param name="width">The width. May be negative only to construct an empty-like sentinel.</param>
    /// <param name="height">The height. May be negative only to construct an empty-like sentinel.</param>
    public Rect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    ///     Determines whether the specified point lies within this rectangle, using a
    ///     half-open interval on both axes (the left/top edges are included; the right/bottom
    ///     edges are excluded).
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><see langword="true"/> if the point lies within this rectangle; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    ///     An <see cref="Empty"/> (or any negative-size) rectangle contains no points: its
    ///     inverted extent (<see cref="Left"/> greater than <see cref="Right"/>) makes every
    ///     comparison below fail for any finite <paramref name="point"/>.
    /// </remarks>
    public bool Contains(Vector2 point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

    /// <summary>
    ///     Returns the smallest rectangle containing both this rectangle and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The other rectangle to union with.</param>
    /// <returns>The smallest enclosing rectangle.</returns>
    public Rect Union(Rect other) => Union(this, other);

    /// <summary>
    ///     Returns the smallest rectangle containing both <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    /// <param name="a">The first rectangle.</param>
    /// <param name="b">The second rectangle.</param>
    /// <returns>
    ///     The smallest enclosing rectangle. If either input is <see cref="Empty"/>, the result is
    ///     exactly the other input (see the type-level remarks for why this identity holds).
    /// </returns>
    public static Rect Union(Rect a, Rect b)
    {
        var left = Math.Min(a.Left, b.Left);
        var top = Math.Min(a.Top, b.Top);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    ///     Returns the intersection of this rectangle and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The other rectangle to intersect with.</param>
    /// <returns>
    ///     The overlapping region, or <see cref="Empty"/> if the rectangles are disjoint or merely
    ///     touch along an edge (zero-area contact is treated as no overlap).
    /// </returns>
    public Rect Intersect(Rect other) => Intersect(this, other);

    /// <summary>
    ///     Returns the intersection of <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    /// <param name="a">The first rectangle.</param>
    /// <param name="b">The second rectangle.</param>
    /// <returns>
    ///     The overlapping region, or <see cref="Empty"/> if the rectangles are disjoint or merely
    ///     touch along an edge (zero-area contact is treated as no overlap).
    /// </returns>
    public static Rect Intersect(Rect a, Rect b)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);

        // A non-positive extent on either axis means the rectangles do not overlap - including
        // the boundary case where they merely touch along an edge (right == left or
        // bottom == top), which yields a zero-width or zero-height rectangle with no actual
        // overlapping area. Return the canonical Empty sentinel rather than a rectangle with a
        // nonsensical negative-but-not-canonical size (or a non-Empty rectangle with zero area).
        return right <= left || bottom <= top ? Empty : new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    ///     Returns the smallest axis-aligned rectangle enclosing this rectangle after applying
    ///     <paramref name="matrix"/>.
    /// </summary>
    /// <param name="matrix">The transform to apply.</param>
    /// <returns>The transformed, re-enclosed rectangle.</returns>
    /// <remarks>
    ///     All four corners are transformed individually and then re-enclosed via
    ///     <see cref="Union(Rect,Rect)"/>-equivalent min/max folding, rather than transforming only
    ///     the top-left and bottom-right corners. Transforming just two opposite corners is
    ///     mathematically incorrect for any matrix containing rotation or shear: the image of an
    ///     axis-aligned rectangle under such a transform is a parallelogram, not another
    ///     axis-aligned rectangle, so its true bounding box can only be recovered by considering
    ///     all four transformed corners.
    /// </remarks>
    public Rect Transform(Matrix3x2 matrix)
    {
        // Empty's corners include +/-Infinity; transforming them directly can produce NaN (e.g.
        // a zero matrix element multiplied by an infinite coordinate). Preserve Empty as an
        // identity/no-op rather than letting it degrade into a NaN-filled rectangle.
        if (IsEmpty)
        {
            return Empty;
        }

        var p0 = Vector2.Transform(TopLeft, matrix);
        var p1 = Vector2.Transform(new Vector2(Right, Top), matrix);
        var p2 = Vector2.Transform(new Vector2(Left, Bottom), matrix);
        var p3 = Vector2.Transform(BottomRight, matrix);

        var minX = Math.Min(Math.Min(p0.X, p1.X), Math.Min(p2.X, p3.X));
        var minY = Math.Min(Math.Min(p0.Y, p1.Y), Math.Min(p2.Y, p3.Y));
        var maxX = Math.Max(Math.Max(p0.X, p1.X), Math.Max(p2.X, p3.X));
        var maxY = Math.Max(Math.Max(p0.Y, p1.Y), Math.Max(p2.Y, p3.Y));

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    ///     Determines whether this instance and another <see cref="Rect"/> have equal field values.
    /// </summary>
    /// <param name="other">The other <see cref="Rect"/> to compare against.</param>
    /// <returns><see langword="true"/> if all four fields are equal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    ///     Compares the raw bit pattern of each field rather than using <c>==</c>/tolerance-based
    ///     comparison: this is exact snapshot equality of stored field values (matching the
    ///     precedent set by <see cref="DemaConsulting.CanvasNet.Canvas.Rgba32"/>'s exact byte
    ///     equality), not a comparison of independently computed floating-point results, so no
    ///     rounding tolerance is appropriate here.
    /// </remarks>
    public bool Equals(Rect other) =>
        BitsEqual(X, other.X) && BitsEqual(Y, other.Y) && BitsEqual(Width, other.Width) && BitsEqual(Height, other.Height);

    /// <summary>
    ///     Determines whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="obj"/> is a <see cref="Rect"/> with equal
    ///     field values; otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj) => obj is Rect other && Equals(other);

    /// <summary>
    ///     Compares the raw bit patterns of two <see cref="float"/> values for exact equality.
    /// </summary>
    private static bool BitsEqual(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);

    /// <summary>
    ///     Returns a hash code for this instance based on its field values.
    /// </summary>
    /// <returns>A hash code for this instance.</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <summary>
    ///     Determines whether two <see cref="Rect"/> instances have equal field values.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Rect left, Rect right) => left.Equals(right);

    /// <summary>
    ///     Determines whether two <see cref="Rect"/> instances have different field values.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are different; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

    /// <summary>
    ///     Returns a string representation of this rectangle's field values.
    /// </summary>
    /// <returns>A human-readable string in the form <c>"{X=.., Y=.., Width=.., Height=..}"</c>.</returns>
    public override string ToString() => $"{{X={X}, Y={Y}, Width={Width}, Height={Height}}}";
}

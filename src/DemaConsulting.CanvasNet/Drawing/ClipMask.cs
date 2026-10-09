using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Drawing;

/// <summary>
///     A per-pixel antialiased coverage mask, bound to a fixed <c>width</c> x <c>height</c>
///     device pixel extent, representing a PDF content stream's current clipping path
///     (PDF 32000-1 &#xA7;8.5.4).
/// </summary>
/// <remarks>
///     <para>
///     Not a public <see cref="Drawing"/> unit (unlike <see cref="PathFiller"/>,
///     <see cref="Gradient"/>, or <see cref="TilePaint"/>): this is an internal implementation
///     detail of clip-path enforcement, consumed only by <c>DemaConsulting.CanvasNet.Pdf</c> (an
///     <c>InternalsVisibleTo</c> friend assembly) via the internal clip-aware
///     <see cref="PathFiller"/>/<see cref="ScanlineRasterizer"/> <c>Fill</c> overloads - exactly
///     the same "internal helper documented inline alongside <see cref="PathFiller"/>" treatment
///     already given to <see cref="EdgeFlattener"/> and <see cref="ScanlineRasterizer"/>
///     themselves, rather than the "new public unit with its own full companion-artifact set"
///     treatment given to a genuinely new public capability such as <see cref="TilePaint"/>. See
///     <c>path-filler.md</c> for the design-level treatment of clip-path support.
///     </para>
///     <para>
///     <b>Coverage, not a boolean mask.</b> Every pixel stores a <c>[0, 1]</c> antialiased
///     coverage fraction - identical in meaning to the per-pixel fill coverage
///     <see cref="ScanlineRasterizer"/> itself computes - rather than a hard inside/outside bit,
///     so a clip path's own edge antialiasing composes smoothly with whatever is painted through
///     it, rather than producing a jagged, aliased clip boundary.
///     </para>
///     <para>
///     <b>Immutable and reused, never mutated in place.</b> <see cref="FromPath"/> builds a fresh
///     mask from a path's own geometry; <see cref="Intersect"/> always returns a new instance
///     holding the elementwise product of two masks, never modifying either operand - this is
///     what lets <c>PdfDocument.GraphicsState.Clone()</c> safely copy a <see cref="ClipMask"/>
///     reference as-is for <c>q</c>/<c>Q</c> save/restore scoping (PDF 32000-1 &#xA7;8.4.2) without
///     any risk of one graphics state's clip later being mutated out from under another.
///     </para>
/// </remarks>
internal sealed class ClipMask
{
    /// <summary>
    ///     The per-pixel <c>[0, 1]</c> coverage buffer, row-major, sized
    ///     <see cref="_boundsWidth"/> * <see cref="_boundsHeight"/> - only the clip path's own
    ///     bounding box, not the full <see cref="Width"/> x <see cref="Height"/> device pixel
    ///     extent. A clip path is typically a small fraction of the page (for example, a single
    ///     table cell or figure), so bounding the buffer this way keeps a clip's memory cost
    ///     proportional to the area it actually restricts, rather than forcing every clip -
    ///     however small - to pay for a full-page-sized allocation (and a full-page-sized
    ///     <see cref="Intersect"/> operation) regardless of its own extent.
    /// </summary>
    private readonly float[] _coverage;

    /// <summary>The device pixel column <see cref="_coverage"/>'s column <c>0</c> corresponds to.</summary>
    private readonly int _originX;

    /// <summary>The device pixel row <see cref="_coverage"/>'s row <c>0</c> corresponds to.</summary>
    private readonly int _originY;

    /// <summary>The width, in pixel columns, of <see cref="_coverage"/>'s own bounding box.</summary>
    private readonly int _boundsWidth;

    /// <summary>The height, in pixel rows, of <see cref="_coverage"/>'s own bounding box.</summary>
    private readonly int _boundsHeight;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClipMask"/> class directly from an
    ///     already-computed, bounding-box-restricted coverage buffer - used by both
    ///     <see cref="FromPath"/> and <see cref="Intersect"/>, which differ only in how they
    ///     produce that buffer and its bounding box.
    /// </summary>
    private ClipMask(float[] coverage, int width, int height, int originX, int originY, int boundsWidth, int boundsHeight)
    {
        _coverage = coverage;
        _originX = originX;
        _originY = originY;
        _boundsWidth = boundsWidth;
        _boundsHeight = boundsHeight;
        Width = width;
        Height = height;
    }

    /// <summary>The device pixel extent's width, in whole pixel columns, this mask covers.</summary>
    public int Width { get; }

    /// <summary>The device pixel extent's height, in whole pixel rows, this mask covers.</summary>
    public int Height { get; }

    /// <summary>
    ///     Builds a new <see cref="ClipMask"/> whose per-pixel coverage is <paramref name="path"/>'s
    ///     own antialiased fill coverage, interpreted under <paramref name="fillRule"/>, across a
    ///     <paramref name="width"/> x <paramref name="height"/> device pixel extent.
    /// </summary>
    /// <param name="path">The clipping path's geometry. Must not be <see langword="null"/>.</param>
    /// <param name="fillRule">
    ///     The rule used to interpret <paramref name="path"/> for clipping purposes - <c>W</c>
    ///     uses <see cref="FillRule.NonZero"/>, <c>W*</c> uses <see cref="FillRule.EvenOdd"/>
    ///     (PDF 32000-1 &#xA7;8.5.4).
    /// </param>
    /// <param name="width">The device pixel extent's width, in whole pixel columns. Must be greater than zero.</param>
    /// <param name="height">The device pixel extent's height, in whole pixel rows. Must be greater than zero.</param>
    /// <param name="flattenTolerance">
    ///     The maximum allowed perpendicular deviation between each curve in <paramref name="path"/>
    ///     and the polyline used to approximate it. Must be greater than zero. Defaults to
    ///     <c>0.25f</c>, identical to <see cref="PathFiller.Fill(Canvas.Surface, Path, Canvas.Rgba32, FillRule, float)"/>'s
    ///     own default.
    /// </param>
    /// <returns>
    ///     A new <see cref="ClipMask"/> whose coverage is zero everywhere outside
    ///     <paramref name="path"/>'s own filled region (including, trivially, when
    ///     <paramref name="path"/> has an empty bounding box or lies entirely outside the
    ///     <paramref name="width"/> x <paramref name="height"/> extent).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="fillRule"/> is not a defined <see cref="FillRule"/> value,
    ///     when <paramref name="flattenTolerance"/> is not a finite value greater than zero, or
    ///     when <paramref name="width"/> or <paramref name="height"/> is not greater than zero.
    /// </exception>
    public static ClipMask FromPath(Path path, FillRule fillRule, int width, int height, float flattenTolerance = 0.25f)
    {
        ArgumentNullException.ThrowIfNull(path);
        PathFiller.ValidateFillArgs(fillRule, flattenTolerance);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        var polygons = EdgeFlattener.Flatten(path, flattenTolerance);
        var pathBounds = PathFiller.GetPolygonBounds(polygons);
        var extentBounds = new Rect(0, 0, width, height);
        var clipBounds = Rect.Intersect(pathBounds, extentBounds);
        if (clipBounds.IsEmpty)
        {
            return new ClipMask([], width, height, 0, 0, 0, 0);
        }

        // Round the (already extent-clipped) float clip bounds outward to the smallest whole
        // pixel rectangle that contains them, and allocate the coverage buffer sized to only that
        // rectangle - never the full width x height device pixel extent.
        var originX = (int)MathF.Floor(clipBounds.Left);
        var originY = (int)MathF.Floor(clipBounds.Top);
        var boundsWidth = (int)MathF.Ceiling(clipBounds.Right) - originX;
        var boundsHeight = (int)MathF.Ceiling(clipBounds.Bottom) - originY;

        var coverage = new float[boundsWidth * boundsHeight];
        ScanlineRasterizer.AccumulateCoverageMask(polygons, fillRule, clipBounds, coverage, boundsWidth, originX, originY);

        return new ClipMask(coverage, width, height, originX, originY, boundsWidth, boundsHeight);
    }

    /// <summary>
    ///     Returns a new <see cref="ClipMask"/> holding the elementwise product of this mask's
    ///     coverage and <paramref name="other"/>'s coverage - the geometric intersection of the
    ///     two clipping paths (PDF 32000-1 &#xA7;8.5.4: "the new clipping path ... shall be the
    ///     intersection of the current clipping path and the newly constructed path"), since the
    ///     product of two <c>[0, 1]</c> coverage fractions is itself a coverage fraction that is
    ///     never larger than either operand, and is zero wherever either operand is zero.
    /// </summary>
    /// <param name="other">
    ///     The other clip mask to intersect with. Must not be <see langword="null"/>, and must
    ///     share this mask's own <see cref="Width"/>/<see cref="Height"/> (every <see cref="ClipMask"/>
    ///     live within a single PDF page render shares the same device pixel extent).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="other"/>'s <see cref="Width"/>/<see cref="Height"/> differ
    ///     from this mask's own.
    /// </exception>
    public ClipMask Intersect(ClipMask other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Width != Width || other.Height != Height)
        {
            throw new ArgumentException(
                "Cannot intersect clip masks covering different device pixel extents.", nameof(other));
        }

        // The elementwise product of two coverage masks is zero everywhere outside either
        // operand's own bounding box (see GetCoverage), so the result's own bounding box never
        // needs to extend beyond the two operands' bounding-box overlap - restricting it that way
        // (rather than to the full device pixel extent) is what keeps a chain of nested clips
        // (PDF 32000-1 &#xA7;8.4.2 q/Q scoping) from growing memory with each intersection.
        var minX = Math.Max(_originX, other._originX);
        var minY = Math.Max(_originY, other._originY);
        var maxX = Math.Min(_originX + _boundsWidth, other._originX + other._boundsWidth);
        var maxY = Math.Min(_originY + _boundsHeight, other._originY + other._boundsHeight);
        if (maxX <= minX || maxY <= minY)
        {
            return new ClipMask([], Width, Height, 0, 0, 0, 0);
        }

        var boundsWidth = maxX - minX;
        var boundsHeight = maxY - minY;
        var result = new float[boundsWidth * boundsHeight];
        for (var y = 0; y < boundsHeight; y++)
        {
            var deviceY = minY + y;
            var rowOffset = y * boundsWidth;
            for (var x = 0; x < boundsWidth; x++)
            {
                var deviceX = minX + x;
                result[rowOffset + x] = GetCoverage(deviceX, deviceY) * other.GetCoverage(deviceX, deviceY);
            }
        }

        return new ClipMask(result, Width, Height, minX, minY, boundsWidth, boundsHeight);
    }

    /// <summary>
    ///     Returns this mask's antialiased coverage, in <c>[0, 1]</c>, at device pixel
    ///     <paramref name="x"/>, <paramref name="y"/>.
    /// </summary>
    /// <param name="x">The zero-based device pixel column.</param>
    /// <param name="y">The zero-based device pixel row.</param>
    /// <returns>
    ///     The coverage at <paramref name="x"/>, <paramref name="y"/>, or <c>0f</c> if the
    ///     coordinate lies outside this mask's own <see cref="Width"/> x <see cref="Height"/>
    ///     extent - matching the "clip restricts to an area, it never grants coverage beyond what
    ///     it was built for" semantics every caller needs, without every caller re-deriving its
    ///     own bounds check.
    /// </returns>
    public float GetCoverage(int x, int y)
    {
        var localX = x - _originX;
        var localY = y - _originY;
        if ((uint)localX >= (uint)_boundsWidth || (uint)localY >= (uint)_boundsHeight)
        {
            return 0f;
        }

        return _coverage[(localY * _boundsWidth) + localX];
    }
}

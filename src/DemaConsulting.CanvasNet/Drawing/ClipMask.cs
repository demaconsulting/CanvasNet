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
    /// <summary>The per-pixel <c>[0, 1]</c> coverage buffer, row-major, sized <c>Width * Height</c>.</summary>
    private readonly float[] _coverage;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ClipMask"/> class directly from an
    ///     already-computed coverage buffer - used by both <see cref="FromPath"/> and
    ///     <see cref="Intersect"/>, which differ only in how they produce that buffer.
    /// </summary>
    private ClipMask(float[] coverage, int width, int height)
    {
        _coverage = coverage;
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

        var coverage = new float[width * height];

        var polygons = EdgeFlattener.Flatten(path, flattenTolerance);
        var pathBounds = PathFiller.GetPolygonBounds(polygons);
        var extentBounds = new Rect(0, 0, width, height);
        var clipBounds = Rect.Intersect(pathBounds, extentBounds);
        if (!clipBounds.IsEmpty)
        {
            ScanlineRasterizer.AccumulateCoverageMask(polygons, fillRule, clipBounds, coverage, width);
        }

        return new ClipMask(coverage, width, height);
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

        var result = new float[_coverage.Length];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = _coverage[i] * other._coverage[i];
        }

        return new ClipMask(result, Width, Height);
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
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return 0f;
        }

        return _coverage[(y * Width) + x];
    }
}

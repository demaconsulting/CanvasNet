using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio stealth

/// <summary>
///     Builds the local-space <see cref="Path"/> geometry for a resolved <see cref="VsdxArrowhead"/>,
///     following <c>DemaConsulting.CanvasNet.Pptx.PptxArrowheadGeometry</c>'s own "single, fixed,
///     documented proportion" approximation philosophy - [MS-VSDX] does not publish an exact
///     arrowhead-size table for its <c>BeginArrowSize</c>/<c>EndArrowSize</c> index values (<c>0</c>,
///     very small, through <c>6</c>, colossal), only that larger indices draw progressively larger
///     arrowheads relative to the connector's own line weight.
/// </summary>
/// <remarks>
///     <para>
///         Every builder below sizes its output relative to <c>strokeWidthInches</c>'s
///         own value (the connector's own resolved <see cref="VsdxResolvedPaint.StrokeWidthInches"/>):
///         an arrowhead is always a proportion of its own line's thickness, not an absolute size,
///         consistent with how Visio itself scales an arrowhead as its line gets thicker or thinner.
///     </para>
///     <para>
///         <b>Local coordinate space.</b> Every builder below places the arrowhead's own tip at the
///         local origin <c>(0,0)</c>, pointing along local <c>+x</c> (i.e. the arrowhead's own body
///         extends toward local <c>-x</c>, "behind" the tip) - a caller orients/positions it by
///         composing a rotation (aligning local <c>+x</c> with the connector's own tangent direction
///         at that endpoint, in page space) and a translation (to that endpoint's own resolved
///         page-space position), exactly like <c>PptxArrowheadGeometry.Build</c>'s own "local box,
///         caller transforms it into place" convention.
///     </para>
/// </remarks>
internal static class VsdxArrowheadGeometry
{
    /// <summary>
    ///     The fixed, documented base proportion (relative to the line's own width) an arrowhead's
    ///     half-width is built at, before applying <see cref="SizeScaleTable"/>'s own per-index
    ///     scale factor - chosen so a mid-sized arrowhead reads clearly against its own line
    ///     without overwhelming a thin stroke, consistent with typical Visio proportions.
    /// </summary>
    private const double BaseHalfWidthFactor = 1.5d;

    /// <summary>
    ///     The fixed, documented base proportion (relative to the line's own width) an arrowhead's
    ///     own length is built at, before applying <see cref="SizeScaleTable"/>'s own per-index
    ///     scale factor - slightly longer than it is wide, matching a typical triangular Visio
    ///     arrowhead's own aspect ratio.
    /// </summary>
    private const double BaseLengthFactor = 3.6d;

    /// <summary>
    ///     A documented, evenly-spaced size-index to scale-factor table (<c>0.5x</c> at index
    ///     <c>0</c> through <c>2.0x</c> at index <c>6</c>, about a <c>1.0x</c> default at index
    ///     <c>2</c>) - a best-effort approximation, since Visio's own exact published arrowhead-
    ///     size table is not derivable from the sample corpus (same documented-approximation
    ///     philosophy <c>PptxArrowheadGeometry</c>'s own size-key table already establishes). An
    ///     out-of-range index (see <see cref="VsdxDocument.ParseArrowheadSizeIndex"/>'s own
    ///     defensive clamp - never expected for a well-formed document) clamps to the nearest
    ///     endpoint rather than throwing.
    /// </summary>
    private static readonly double[] SizeScaleTable = [0.5d, 0.75d, 1.0d, 1.25d, 1.5d, 1.75d, 2.0d];

    /// <summary>
    ///     Builds <paramref name="style"/>'s own local-space <see cref="Path"/>, sized relative to
    ///     <paramref name="strokeWidthInches"/> and <paramref name="sizeIndex"/> (see this class's
    ///     own remarks).
    /// </summary>
    /// <param name="style">The resolved arrowhead style to build. <see cref="VsdxArrowheadStyle.None"/> yields an empty, non-filled result.</param>
    /// <param name="sizeIndex">The resolved <c>BeginArrowSize</c>/<c>EndArrowSize</c> index, clamped into <see cref="SizeScaleTable"/>'s own <c>[0, 6]</c> range.</param>
    /// <param name="strokeWidthInches">The connector line's own resolved stroke width, in inches.</param>
    /// <returns>
    ///     The built local-space <see cref="VsdxArrowheadGeometryResult"/> (see this class's own
    ///     remarks for its coordinate convention): <see cref="VsdxArrowheadStyle.Arrow"/>/
    ///     <see cref="VsdxArrowheadStyle.Stealth"/>/<see cref="VsdxArrowheadStyle.Circle"/> each
    ///     return a closed, fillable path; <see cref="VsdxArrowheadStyle.OpenArrow"/>/
    ///     <see cref="VsdxArrowheadStyle.Diamond"/> return a path meant to be <em>stroked</em>,
    ///     not filled (an open, unfilled line arrowhead and an unfilled diamond outline,
    ///     respectively - see <see cref="VsdxArrowheadStyle"/>'s own remarks). <see cref="Path.Empty"/>
    ///     (never filled) when <paramref name="style"/> is <see cref="VsdxArrowheadStyle.None"/> or
    ///     <paramref name="strokeWidthInches"/> is non-positive (an arrowhead has no meaningful
    ///     size to scale from).
    /// </returns>
    internal static VsdxArrowheadGeometryResult Build(VsdxArrowheadStyle style, int sizeIndex, double strokeWidthInches)
    {
        if (style == VsdxArrowheadStyle.None || strokeWidthInches <= 0d)
        {
            return new VsdxArrowheadGeometryResult(Path.Empty, IsFilled: false);
        }

        var scale = SizeScaleTable[Math.Clamp(sizeIndex, 0, SizeScaleTable.Length - 1)];
        var halfWidth = (float)(strokeWidthInches * BaseHalfWidthFactor * scale);
        var length = (float)(strokeWidthInches * BaseLengthFactor * scale);

        return style switch
        {
            VsdxArrowheadStyle.Arrow => new VsdxArrowheadGeometryResult(
                Polygon(
                    Vector2.Zero,
                    new Vector2(-length, -halfWidth),
                    new Vector2(-length, halfWidth)),
                IsFilled: true),

            VsdxArrowheadStyle.Stealth => new VsdxArrowheadGeometryResult(
                Polygon(
                    Vector2.Zero,
                    new Vector2(-length, -halfWidth),
                    new Vector2(-length * 0.6f, 0f),
                    new Vector2(-length, halfWidth)),
                IsFilled: true),

            VsdxArrowheadStyle.Diamond => new VsdxArrowheadGeometryResult(
                Polygon(
                    Vector2.Zero,
                    new Vector2(-length / 2f, -halfWidth),
                    new Vector2(-length, 0f),
                    new Vector2(-length / 2f, halfWidth)),
                IsFilled: false),

            VsdxArrowheadStyle.Circle => new VsdxArrowheadGeometryResult(
                Path.Circle(-halfWidth, 0f, halfWidth),
                IsFilled: true),

            VsdxArrowheadStyle.OpenArrow => new VsdxArrowheadGeometryResult(
                OpenChevron(length, halfWidth),
                IsFilled: false),

            _ => new VsdxArrowheadGeometryResult(Path.Empty, IsFilled: false),
        };
    }

    /// <summary>Builds a closed polygon from the given vertices, in order (used by every filled/outlined arrowhead kind).</summary>
    private static Path Polygon(params Vector2[] points)
    {
        var builder = new PathBuilder().MoveTo(points[0]);
        for (var i = 1; i < points.Length; i++)
        {
            builder.LineTo(points[i]);
        }

        return builder.Close().Build();
    }

    /// <summary>
    ///     Builds the open, two-segment "chevron" shape for <see cref="VsdxArrowheadStyle.OpenArrow"/>:
    ///     a "V" formed by the two line segments meeting at the tip - stroked, not filled, by the
    ///     caller (see <see cref="Build"/>'s own return-value remarks).
    /// </summary>
    private static Path OpenChevron(float length, float halfWidth) =>
        new PathBuilder()
            .MoveTo(new Vector2(-length, -halfWidth))
            .LineTo(Vector2.Zero)
            .LineTo(new Vector2(-length, halfWidth))
            .Build();
}

/// <summary>
///     A single built arrowhead's local-space outline and whether it should be painted via a fill
///     (a closed, solid shape) or via a stroke (an open or hollow outline) - see
///     <see cref="VsdxArrowheadGeometry.Build"/>'s own remarks.
/// </summary>
/// <param name="Path">The arrowhead's local-space geometry (see <see cref="VsdxArrowheadGeometry"/>'s own coordinate-convention remarks).</param>
/// <param name="IsFilled">
///     <see langword="true"/> when <see cref="Path"/> should be filled solid with the connector's
///     own resolved stroke color; <see langword="false"/> when it should instead be stroked (as
///     a thin outline) with that same color.
/// </param>
internal sealed record VsdxArrowheadGeometryResult(Path Path, bool IsFilled);

using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx stealth

/// <summary>
///     Builds the local-space <see cref="Path"/> geometry for a single <c>&lt;a:headEnd&gt;</c>/
///     <c>&lt;a:tailEnd&gt;</c> arrowhead (Phase 2 Follow-Up: Connector Shape Rendering), following
///     <see cref="PptxPresetGeometry"/>'s own "single, fixed, documented proportion" approximation
///     philosophy - OOXML does not specify exact arrowhead proportions for its <c>sm</c>/<c>med</c>/
///     <c>lg</c> <c>w</c>/<c>len</c> size keys, only that they are relative to each other.
/// </summary>
/// <remarks>
///     <para>
///     Every builder below sizes its output relative to <c>lineWidthEmu</c> (the connector's own
///     resolved stroke width - see <see cref="PptxDocument.ResolveConnectorLineStyle"/>): an
///     arrowhead's <c>w</c>/<c>len</c> keys are always proportions of its own line's thickness,
///     not absolute sizes, consistent with how PowerPoint itself scales an arrowhead as its line
///     gets thicker or thinner.
///     </para>
///     <para>
///     <b>Local coordinate space.</b> Every builder below places the arrowhead's own tip at the
///     local origin <c>(0,0)</c>, pointing along local <c>+x</c> (i.e. the arrowhead's own body
///     extends toward local <c>-x</c>, "behind" the tip) - a caller orients/positions it by
///     composing a rotation (aligning local <c>+x</c> with the connector's own tangent direction
///     at that endpoint) and a translation (to that endpoint's own position), exactly like
///     <see cref="PptxPresetGeometry.Build"/>'s own "local box, caller transforms it into place"
///     convention.
///     </para>
/// </remarks>
internal static class PptxArrowheadGeometry
{
    /// <summary>
    ///     The fixed, documented base proportion (relative to the line's own width) an arrowhead's
    ///     half-width is built at when its own <c>w</c> key is <c>"med"</c> (the schema default) -
    ///     chosen so a <c>"med"</c> arrowhead reads clearly against its own line without
    ///     overwhelming a thin stroke, consistent with typical PowerPoint proportions.
    /// </summary>
    private const float BaseHalfWidthFactor = 1.5f;

    /// <summary>
    ///     The fixed, documented base proportion (relative to the line's own width) an arrowhead's
    ///     own length is built at when its own <c>len</c> key is <c>"med"</c> - slightly longer
    ///     than it is wide, matching a typical triangular PowerPoint arrowhead's own aspect ratio.
    /// </summary>
    private const float BaseLengthFactor = 3.6f;

    /// <summary>
    ///     Builds <paramref name="style"/>'s own local-space <see cref="Path"/>, sized relative to
    ///     <paramref name="lineWidthEmu"/> (see this class's own remarks).
    /// </summary>
    /// <param name="style">The resolved arrowhead style to build.</param>
    /// <param name="lineWidthEmu">The connector line's own resolved stroke width, in EMU.</param>
    /// <returns>
    ///     The built local-space <see cref="Path"/> (see this class's own remarks for its
    ///     coordinate convention), or <see cref="Path.Empty"/> when <paramref name="lineWidthEmu"/>
    ///     is non-positive (an arrowhead has no meaningful size to scale from).
    ///     <see cref="PptxArrowheadKind.Triangle"/>/<see cref="PptxArrowheadKind.Stealth"/>/
    ///     <see cref="PptxArrowheadKind.Diamond"/>/<see cref="PptxArrowheadKind.Oval"/> each return
    ///     a closed, fillable path; <see cref="PptxArrowheadKind.Arrow"/> returns a small, open,
    ///     two-segment path meant to be <em>stroked</em>, not filled - see
    ///     <see cref="PptxDocument.RenderConnector"/>'s own dispatch.
    /// </returns>
    internal static Path Build(PptxArrowheadStyle style, float lineWidthEmu)
    {
        if (lineWidthEmu <= 0f)
        {
            return Path.Empty;
        }

        var halfWidth = lineWidthEmu * BaseHalfWidthFactor * SizeScale(style.WidthKey);
        var length = lineWidthEmu * BaseLengthFactor * SizeScale(style.LengthKey);

        return style.Kind switch
        {
            PptxArrowheadKind.Triangle => Polygon(
                Vector2.Zero,
                new Vector2(-length, -halfWidth),
                new Vector2(-length, halfWidth)),

            PptxArrowheadKind.Stealth => Polygon(
                Vector2.Zero,
                new Vector2(-length, -halfWidth),
                new Vector2(-length * 0.6f, 0f),
                new Vector2(-length, halfWidth)),

            PptxArrowheadKind.Diamond => Polygon(
                new Vector2(length / 2f, 0f),
                new Vector2(0f, -halfWidth),
                new Vector2(-length / 2f, 0f),
                new Vector2(0f, halfWidth)),

            PptxArrowheadKind.Oval => PptxPresetGeometry.Ellipse(0f, 0f, length / 2f, halfWidth),

            PptxArrowheadKind.Arrow => OpenChevron(length, halfWidth),

            _ => Path.Empty,
        };
    }

    /// <summary>
    ///     Maps an arrowhead size key (<c>"sm"</c>/<c>"med"</c>/<c>"lg"</c>) to a fixed, documented
    ///     scale factor relative to <see cref="BaseHalfWidthFactor"/>/<see cref="BaseLengthFactor"/>
    ///     - an unrecognized or absent key resolves to the same scale as <c>"med"</c> (the OOXML
    ///     schema default), matching <see cref="PptxDocument.ResolveArrowhead"/>'s own fallback.
    /// </summary>
    private static float SizeScale(string key) => key switch
    {
        "sm" => 0.75f,
        "lg" => 1.5f,
        _ => 1f,
    };

    /// <summary>Builds a closed polygon from the given vertices, in order (used by every filled arrowhead kind).</summary>
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
    ///     Builds the open, two-segment "chevron" shape for <see cref="PptxArrowheadKind.Arrow"/>:
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

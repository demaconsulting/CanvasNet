using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

/// <summary>
///     Implements the <see cref="PptxDocument"/> text-painting primitive (Phase 1d): paints a
///     resolved <see cref="PptxTextLayout"/>'s glyph ink onto a <see cref="Surface"/>, and the
///     DrawingML-specific system/bundled font-resolution helper <see cref="ResolveTextFont"/> this
///     phase's default <c>fontResolver</c> delegate is driven with (see
///     <see cref="ResolveTextLayout"/>). Mirrors <c>PdfDocument.Text.cs</c>'s own glyph-painting
///     pattern and <c>PdfDocument.FontFallback.cs</c>'s own system/bundled fallback pattern -
///     see those files' remarks for the established precedent this phase reuses rather than
///     duplicating design rationale for.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     The painted underline stroke's thickness, as a fraction of the underlined run's own
    ///     <see cref="PptxUnderlineSegment.SizeEmu"/> (Phase 2 Follow-Up: Underline Rendering).
    ///     This is a pragmatic, <c>SizeEmu</c>-proportional approximation - the OpenType
    ///     <c>post</c> table's own <c>underlineThickness</c> value is not parsed this phase, a
    ///     documented limitation (see the design document's "Phase 2 Follow-Up: Underline
    ///     Rendering" section).
    /// </summary>
    private const float UnderlineThicknessRatio = 0.05f;

    /// <summary>
    ///     The painted underline stroke's offset below the baseline, as a fraction of the
    ///     underlined run's own <see cref="PptxUnderlineSegment.SizeEmu"/> (Phase 2 Follow-Up:
    ///     Underline Rendering). This is a pragmatic, <c>SizeEmu</c>-proportional approximation -
    ///     the OpenType <c>post</c> table's own <c>underlinePosition</c> value is not parsed this
    ///     phase, a documented limitation (see the design document's "Phase 2 Follow-Up:
    ///     Underline Rendering" section).
    /// </summary>
    private const float UnderlineOffsetRatio = 0.08f;

    /// <summary>The additional gap, as a fraction of <see cref="PptxUnderlineSegment.SizeEmu"/>, between a double underline's two painted lines.</summary>
    private const float DoubleUnderlineGapRatio = 0.06f;

    /// <summary>
    ///     Paints every glyph in <paramref name="layout"/> onto <paramref name="surface"/>,
    ///     composing each glyph's own <c>1/UnitsPerEm</c> outline scale, its resolved size, its
    ///     shape-local baseline origin, and <paramref name="shapeToSurfaceTransform"/> (the
    ///     owning shape's own <see cref="PptxShapeFrame.Transform"/>, or a further-composed
    ///     transform for a shape nested inside a group) into a single glyph-to-surface matrix.
    ///     A glyph whose outline has no subpaths (a space or control character - though
    ///     <see cref="ResolveTextLayout"/> already omits whitespace glyphs from
    ///     <see cref="PptxTextLayout.Glyphs"/>, a defensive, resolver-agnostic check) is skipped
    ///     entirely rather than painting an empty fill. After every glyph is painted, every
    ///     <see cref="PptxTextLayout.Underlines"/> segment (Phase 2 Follow-Up: Underline
    ///     Rendering) is painted as one or two thin filled rectangles (see
    ///     <see cref="UnderlineThicknessRatio"/>/<see cref="UnderlineOffsetRatio"/>'s own remarks
    ///     for the documented thickness/offset approximation) - <see cref="PptxUnderlineStyle.Double"/>
    ///     paints two thinner, gapped rectangles; every other non-<see cref="PptxUnderlineStyle.None"/>
    ///     style (<see cref="PptxUnderlineStyle.Single"/>/<see cref="PptxUnderlineStyle.Other"/>)
    ///     paints exactly one - never throwing for an unrecognized style.
    /// </summary>
    /// <param name="surface">The surface to paint onto.</param>
    /// <param name="layout">The resolved text layout to paint.</param>
    /// <param name="shapeToSurfaceTransform">The transform mapping the owning shape's own local coordinate space into surface pixel space.</param>
    internal static void PaintTextLayout(Surface surface, PptxTextLayout layout, Matrix3x2 shapeToSurfaceTransform)
    {
        foreach (var glyph in layout.Glyphs)
        {
            var outline = glyph.Font.GetGlyphOutline(glyph.GlyphIndex);
            if (outline.Subpaths.Count == 0)
            {
                continue;
            }

            // TrueType glyph outlines use a y-up font coordinate system (positive y points toward
            // the ascender), but CanvasNet's shape-local/surface space is y-down (see
            // Path.Rectangle's own doc comments for the authoritative convention) - the glyph
            // scale's Y component must therefore be negative so outline coordinates above the
            // baseline (positive font-space y) map to ink above OriginYEmu (negative shape-local
            // y, relative to the baseline), not below it. A positive Y scale here would paint
            // every glyph upside-down and offset below the baseline instead of above it.
            var glyphScaleEmu = glyph.SizeEmu / glyph.Font.UnitsPerEm;
            var glyphMatrix =
                Matrix3x2.CreateScale(glyphScaleEmu, -glyphScaleEmu) *
                Matrix3x2.CreateTranslation(glyph.OriginXEmu, glyph.OriginYEmu) *
                shapeToSurfaceTransform;

            var builder = new PathBuilder();
            AppendTransformedGlyphOutline(builder, outline, glyphMatrix);
            var path = builder.Build();

            PathFiller.Fill(surface, path, glyph.Color, FillRule.NonZero);
        }

        foreach (var segment in layout.Underlines)
        {
            PaintUnderlineSegment(surface, segment, shapeToSurfaceTransform);
        }
    }

    /// <summary>
    ///     Paints one <see cref="PptxUnderlineSegment"/> (Phase 2 Follow-Up: Underline Rendering)
    ///     as one or two thin, axis-aligned, shape-local rectangles (built via
    ///     <see cref="Path.Rectangle"/>, matching every other shape-local rectangle fill in this
    ///     assembly - for example <c>PptxDocument.Tables.cs</c>'s own cell-rectangle fills),
    ///     transformed through <paramref name="shapeToSurfaceTransform"/> only (no glyph-space
    ///     Y-flip - unlike a glyph outline, a rectangle built directly in shape-local, already
    ///     y-down space needs no sign flip to orient correctly).
    /// </summary>
    private static void PaintUnderlineSegment(Surface surface, PptxUnderlineSegment segment, Matrix3x2 shapeToSurfaceTransform)
    {
        var widthEmu = segment.EndXEmu - segment.StartXEmu;
        if (widthEmu <= 0f)
        {
            return;
        }

        var thicknessEmu = segment.SizeEmu * UnderlineThicknessRatio;
        var offsetEmu = segment.SizeEmu * UnderlineOffsetRatio;

        if (segment.Style == PptxUnderlineStyle.Double)
        {
            var lineThicknessEmu = thicknessEmu / 2f;
            var gapEmu = segment.SizeEmu * DoubleUnderlineGapRatio;
            FillUnderlineRectangle(surface, segment, shapeToSurfaceTransform, widthEmu, segment.BaselineYEmu + offsetEmu, lineThicknessEmu);
            FillUnderlineRectangle(surface, segment, shapeToSurfaceTransform, widthEmu, segment.BaselineYEmu + offsetEmu + lineThicknessEmu + gapEmu, lineThicknessEmu);
            return;
        }

        // Single/Other/any unrecognized style: exactly one solid rectangle - never throw.
        FillUnderlineRectangle(surface, segment, shapeToSurfaceTransform, widthEmu, segment.BaselineYEmu + offsetEmu, thicknessEmu);
    }

    /// <summary>Fills one underline rectangle spanning <paramref name="widthEmu"/> starting at <paramref name="topYEmu"/>, <paramref name="heightEmu"/> tall.</summary>
    private static void FillUnderlineRectangle(
        Surface surface, PptxUnderlineSegment segment, Matrix3x2 shapeToSurfaceTransform, float widthEmu, float topYEmu, float heightEmu)
    {
        var rectanglePath = Path.Rectangle(segment.StartXEmu, topYEmu, widthEmu, heightEmu).Transform(shapeToSurfaceTransform);
        PathFiller.Fill(surface, rectanglePath, segment.Color, FillRule.NonZero);
    }

    /// <summary>
    ///     Resolves a DrawingML <c>&lt;a:latin typeface="..."/&gt;</c> family name hint to a run's
    ///     single <em>primary</em> <see cref="TrueTypeFont"/> only: searches the host operating
    ///     system's installed fonts via <see cref="SystemFontCatalog.FindBestMatch"/> (DrawingML
    ///     never signals serif/fixed-pitch classification the way PDF's Standard-14/
    ///     FontDescriptor flags do, so both are always passed as <see langword="false"/>), falling
    ///     back to a bundled Liberation Sans/Serif font (<see cref="SystemFontCatalog.LoadBundledFallback"/>,
    ///     also always <c>serif: false, fixedPitch: false</c>) when no system font matches. This
    ///     is the default <c>fontResolver</c> delegate production callers drive
    ///     <see cref="ResolveTextLayout"/> with; a test may inject its own delegate instead for
    ///     deterministic, machine-independent pixel assertions (see <see cref="ResolveTextLayout"/>'s
    ///     own remarks). It deliberately never inspects a run's actual text/codepoints - a
    ///     <em>per-character</em> glyph-coverage fallback (consulted only when this primary font
    ///     turns out to lack a specific, individual character the run's text contains) is a
    ///     separate concern that lives in <c>PptxDocument.TextLayout.cs</c>'s private
    ///     <c>ResolveGlyph</c> helper and its own <c>fallbackFontResolver</c> parameter, not here.
    /// </summary>
    /// <param name="familyNameHint">The <c>&lt;a:latin typeface="..."/&gt;</c> family name hint.</param>
    /// <param name="bold">Whether a bold variant is preferred.</param>
    /// <param name="italic">Whether an italic variant is preferred.</param>
    /// <returns>The resolved <see cref="TrueTypeFont"/>.</returns>
    internal static TrueTypeFont ResolveTextFont(string familyNameHint, bool bold, bool italic)
    {
        var match = SystemFontCatalog.FindBestMatch(familyNameHint, bold, italic, serif: false, fixedPitch: false);
        return match is { } found
            ? TrueTypeFont.Load(found.FilePath, found.FaceIndex)
            : SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold, italic);
    }

    /// <summary>
    ///     Re-issues a glyph outline's subpaths/commands into <paramref name="builder"/>, mapping
    ///     every point through <paramref name="transform"/> - the same local re-implementation
    ///     pattern <c>PdfDocument.Text.cs</c>'s own <c>AppendTransformedGlyphOutline</c> already
    ///     establishes (that method is private to the <c>DemaConsulting.CanvasNet.Pdf</c> assembly
    ///     and cannot be called from here).
    /// </summary>
    /// <exception cref="NotSupportedException">
    ///     Thrown for a <see cref="PathCommandType.ArcTo"/> command - <see cref="TrueTypeFont.GetGlyphOutline"/>
    ///     never produces one (TrueType glyph outlines are built only from lines and quadratic
    ///     Bezier curves), so encountering one indicates an internal defect, not a data-driven
    ///     condition.
    /// </exception>
    private static void AppendTransformedGlyphOutline(PathBuilder builder, Path outline, Matrix3x2 transform)
    {
        foreach (var subpath in outline.Subpaths)
        {
            builder.MoveTo(Vector2.Transform(subpath.Start, transform));
            foreach (var command in subpath.Commands)
            {
                switch (command.Type)
                {
                    case PathCommandType.LineTo:
                        builder.LineTo(Vector2.Transform(command.EndPoint, transform));
                        break;

                    case PathCommandType.QuadraticBezierTo:
                        builder.QuadraticBezierTo(
                            Vector2.Transform(command.Control1, transform),
                            Vector2.Transform(command.EndPoint, transform));
                        break;

                    case PathCommandType.CubicBezierTo:
                        builder.CubicBezierTo(
                            Vector2.Transform(command.Control1, transform),
                            Vector2.Transform(command.Control2, transform),
                            Vector2.Transform(command.EndPoint, transform));
                        break;

                    case PathCommandType.Close:
                        builder.Close();
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Unsupported path command type '{command.Type}' encountered while transforming a glyph outline.");
                }
            }
        }
    }
}

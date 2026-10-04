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
    ///     Paints every glyph in <paramref name="layout"/> onto <paramref name="surface"/>,
    ///     composing each glyph's own <c>1/UnitsPerEm</c> outline scale, its resolved size, its
    ///     shape-local baseline origin, and <paramref name="shapeToSurfaceTransform"/> (the
    ///     owning shape's own <see cref="PptxShapeFrame.Transform"/>, or a further-composed
    ///     transform for a shape nested inside a group) into a single glyph-to-surface matrix.
    ///     A glyph whose outline has no subpaths (a space or control character - though
    ///     <see cref="ResolveTextLayout"/> already omits whitespace glyphs from
    ///     <see cref="PptxTextLayout.Glyphs"/>, a defensive, resolver-agnostic check) is skipped
    ///     entirely rather than painting an empty fill.
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

            var glyphMatrix =
                Matrix3x2.CreateScale(glyph.SizeEmu / glyph.Font.UnitsPerEm) *
                Matrix3x2.CreateTranslation(glyph.OriginXEmu, glyph.OriginYEmu) *
                shapeToSurfaceTransform;

            var builder = new PathBuilder();
            AppendTransformedGlyphOutline(builder, outline, glyphMatrix);
            var path = builder.Build();

            PathFiller.Fill(surface, path, glyph.Color, FillRule.NonZero);
        }
    }

    /// <summary>
    ///     Resolves a DrawingML <c>&lt;a:latin typeface="..."/&gt;</c> family name hint to a
    ///     <see cref="TrueTypeFont"/>: searches the host operating system's installed fonts via
    ///     <see cref="SystemFontCatalog.FindBestMatch"/> (DrawingML never signals serif/
    ///     fixed-pitch classification the way PDF's Standard-14/FontDescriptor flags do, so both
    ///     are always passed as <see langword="false"/>), falling back to a bundled Liberation
    ///     Sans/Serif font (<see cref="SystemFontCatalog.LoadBundledFallback"/>, also always
    ///     <c>serif: false, fixedPitch: false</c>) when no system font matches. This is the
    ///     default <c>fontResolver</c> delegate production callers drive <see cref="ResolveTextLayout"/>
    ///     with; a test may inject its own delegate instead for deterministic, machine-independent
    ///     pixel assertions (see <see cref="ResolveTextLayout"/>'s own remarks).
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

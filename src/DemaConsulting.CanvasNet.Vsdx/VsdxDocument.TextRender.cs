using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio

/// <summary>
///     Implements the <see cref="VsdxDocument"/> text-painting primitive: paints a resolved
///     <see cref="VsdxTextLayout"/>'s glyph ink onto a <see cref="Surface"/>, and the
///     system/bundled font-resolution helper <see cref="ResolveTextFont"/> this milestone's
///     <c>fontResolver</c> delegate is driven with. A direct structural trim of
///     <c>PptxDocument.TextRender.cs</c>'s own glyph-painting pattern - no run-level outline
///     stroke or underline support exists in the resolved <see cref="VsdxTextLayout"/> model this
///     milestone produces, so neither is painted here (out of scope - see the originating plan
///     report's Scope section). Wired into the shipped public rendering pipeline since
///     Milestone 7: <c>VsdxDocument.Render.cs</c>'s <c>RenderShapeRecursive</c> calls
///     <see cref="PaintTextLayout"/> for every resolved shape whose effective <c>HideText</c>
///     cell does not suppress it.
/// </summary>
public sealed partial class VsdxDocument
{
    /// <summary>
    ///     Paints every glyph in <paramref name="layout"/> onto <paramref name="surface"/>,
    ///     composing each glyph's own <c>1/UnitsPerEm</c> outline scale, its resolved size, its
    ///     shape-local baseline origin, and <paramref name="shapeToSurfaceTransform"/> into a
    ///     single glyph-to-surface matrix. A glyph whose outline has no subpaths (a space or
    ///     control character - though <see cref="ResolveTextLayout"/> already omits whitespace
    ///     glyphs from <see cref="VsdxTextLayout.Glyphs"/>, a defensive, resolver-agnostic check)
    ///     is skipped entirely rather than painting an empty fill.
    /// </summary>
    /// <param name="surface">The surface to paint onto.</param>
    /// <param name="layout">The resolved text layout to paint.</param>
    /// <param name="shapeToSurfaceTransform">The transform mapping the owning shape's own local coordinate space into surface pixel space.</param>
    internal static void PaintTextLayout(Surface surface, VsdxTextLayout layout, Matrix3x2 shapeToSurfaceTransform)
    {
        foreach (var glyph in layout.Glyphs)
        {
            var outline = glyph.Font.GetGlyphOutline(glyph.GlyphIndex);
            if (outline.Subpaths.Count == 0)
            {
                continue;
            }

            // TrueType glyph outlines use a y-up font coordinate system (positive y points toward
            // the ascender), matching this unit's own shape-local space convention (see
            // VsdxShapeTransform's own remarks) - unlike Pptx's y-down DrawingML space, no sign
            // flip on the glyph scale's Y component is needed here: outline coordinates above the
            // baseline (positive font-space y) must map to ink above OriginYInches (also positive-
            // up, shape-local y), which a positive Y scale already achieves directly.
            var glyphScale = (float)(glyph.SizeInches / glyph.Font.UnitsPerEm);

            var localGlyphMatrix =
                Matrix3x2.CreateScale(glyphScale, glyphScale) *
                Matrix3x2.CreateTranslation((float)glyph.OriginXInches, (float)glyph.OriginYInches);

            var builder = new PathBuilder();
            AppendTransformedGlyphOutline(builder, outline, localGlyphMatrix);
            var localPath = builder.Build();

            PathFiller.Fill(surface, localPath.Transform(shapeToSurfaceTransform), glyph.Color, FillRule.NonZero);
        }
    }

    /// <summary>
    ///     Resolves a VisioML <c>Font</c> cell's family-name hint to a run's <see cref="TrueTypeFont"/>:
    ///     searches the host operating system's installed fonts via
    ///     <see cref="SystemFontCatalog.FindBestMatch"/> (VisioML never signals serif/fixed-pitch
    ///     classification, so both are always passed as <see langword="false"/>, mirroring
    ///     <c>PptxDocument.ResolveTextFont</c>'s own convention), falling back to a bundled
    ///     Liberation Sans/Serif font (<see cref="SystemFontCatalog.LoadBundledFallback"/>) when no
    ///     system font matches. This is the default <c>fontResolver</c> delegate production
    ///     callers drive <see cref="ResolveTextLayout"/> with; a test may inject its own delegate
    ///     instead for deterministic, machine-independent assertions.
    /// </summary>
    /// <param name="familyNameHint">The resolved <c>Font</c> cell's family name hint.</param>
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
    ///     pattern <c>PptxDocument.TextRender.cs</c>'s own <c>AppendTransformedGlyphOutline</c>
    ///     already establishes (that method is private to the
    ///     <c>DemaConsulting.CanvasNet.Pptx</c> assembly and cannot be called from here).
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

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
///     <c>PptxDocument.TextRender.cs</c>'s own glyph-painting pattern. No run-level outline stroke exists in the resolved <see cref="VsdxTextLayout"/>
///     model, so none is painted (out of scope - see the originating plan report's Scope
///     section); underline, strikethrough and overline <c>DecorationBars</c> are painted before
///     the glyphs. Wired into the shipped public rendering pipeline since
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
        foreach (var bar in layout.DecorationBars)
        {
            var half = (float)(bar.ThicknessInches / 2d);
            var cy = (float)bar.CenterYInches;
            var barBuilder = new PathBuilder();
            barBuilder.MoveTo(new Vector2((float)bar.X0Inches, cy - half));
            barBuilder.LineTo(new Vector2((float)bar.X1Inches, cy - half));
            barBuilder.LineTo(new Vector2((float)bar.X1Inches, cy + half));
            barBuilder.LineTo(new Vector2((float)bar.X0Inches, cy + half));
            barBuilder.Close();
            PathFiller.Fill(surface, barBuilder.Build().Transform(shapeToSurfaceTransform), bar.Color, FillRule.NonZero);
        }

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

    /// <summary>Preferred glyph-fallback families, searched in order before any other installed font.</summary>
    private static readonly string[] FallbackFamilies =
    [
        "Segoe UI Symbol", "Segoe UI Emoji", "Arial Unicode MS", "Cambria Math", "DejaVu Sans", "Noto Sans Symbols",
        "Noto Sans Symbols2", "Noto Sans", "Symbola", "Apple Symbols", "Segoe UI", "Arial", "Liberation Sans",
    ];

    /// <summary>Per-(code point, bold, italic) fallback results (null = nothing covers it), shared by all documents.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int CodePoint, bool Bold, bool Italic), TrueTypeFont?> FallbackResults = new();

    /// <summary>Loaded fallback fonts by file/face, <see langword="null"/> when the file cannot be parsed.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Path, int Face), TrueTypeFont?> FallbackFontCache = new();

    /// <summary>
    ///     Default per-glyph fallback: finds an installed font (preferred symbol/Unicode families
    ///     first, then every other catalog face, then the bundled Liberation faces) that has a
    ///     glyph for <paramref name="codePoint"/>, preferring the requested bold/italic style.
    ///     Results are cached; <see langword="null"/> means no font covers the code point.
    /// </summary>
    internal static TrueTypeFont? ResolveGlyphFallbackFont(int codePoint, bool bold, bool italic) =>
        FallbackResults.GetOrAdd((codePoint, bold, italic), static key => FindGlyphFallbackFont(key.CodePoint, key.Bold, key.Italic));

    private static TrueTypeFont? FindGlyphFallbackFont(int codePoint, bool bold, bool italic)
    {
        static TrueTypeFont? LoadUncached(string path, int face)
        {
            try
            {
                return TrueTypeFont.Load(path, face);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }

        // Preferred families are few, so their fonts stay cached; the catalog-wide sweep
        // loads each face transiently so an uncovered code point cannot pin every font in memory.
        static TrueTypeFont? Load(SystemFontInfo info, bool cache) =>
            cache
                ? FallbackFontCache.GetOrAdd((info.FilePath, info.FaceIndex), static key => LoadUncached(key.Path, key.Face))
                : LoadUncached(info.FilePath, info.FaceIndex);

        IEnumerable<(SystemFontInfo Info, bool Cache)> Candidates()
        {
            var fonts = SystemFontCatalog.Fonts;
            foreach (var family in FallbackFamilies)
            {
                foreach (var info in fonts.Where(f => string.Equals(f.FamilyName, family, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(f => (f.Bold == bold ? 0 : 1) + (f.Italic == italic ? 0 : 1)))
                {
                    yield return (info, true);
                }
            }

            foreach (var info in fonts.OrderBy(f => (f.Bold == bold ? 0 : 1) + (f.Italic == italic ? 0 : 1)))
            {
                yield return (info, false);
            }
        }

        foreach (var (info, cache) in Candidates())
        {
            if (Load(info, cache) is { } font && font.GetGlyphIndex(codePoint) != 0)
            {
                // A sweep hit is promoted into the shared cache (keeping the instance just loaded) so all
                // results for the same file/face share one font rather than each holding a private copy.
                return cache ? font : FallbackFontCache.GetOrAdd((info.FilePath, info.FaceIndex), font)!;
            }
        }

        var bundled = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold, italic);
        return bundled.GetGlyphIndex(codePoint) != 0 ? bundled : null;
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
            ? SystemFontCache.GetOrAdd((found.FilePath, found.FaceIndex), static key => TrueTypeFont.Load(key.Path, key.Face))
            : SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold, italic);
    }

    /// <summary>
    ///     Process-lifetime cache of the system fonts <see cref="ResolveTextFont"/> has loaded,
    ///     keyed by file path and face index, so a page with many text shapes does not re-read and
    ///     re-parse the same font file once per shape. A <see cref="TrueTypeFont"/> is immutable
    ///     and thread-safe, so sharing one instance is behavior-identical.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Path, int Face), TrueTypeFont> SystemFontCache = new();

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

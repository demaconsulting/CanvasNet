using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Drawing;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore blipfill srcrect pptx embed asvg prst cust reimplementation

/// <summary>
///     Implements the <see cref="PptxDocument"/> picture-shape resolvers and painting primitive
///     (Phase 1e): decoding a <c>&lt;p:pic&gt;</c>'s <c>&lt;p:blipFill&gt;</c> into a core
///     <see cref="Surface"/> (<see cref="ResolvePictureSurface"/>), resolving its optional
///     <c>&lt;a:srcRect&gt;</c> crop (<see cref="ResolveSrcRect"/>), and compositing the decoded
///     image onto a destination <see cref="Surface"/> (<see cref="PaintPicture"/>) - see
///     <c>pptx-document.md</c>'s "Images (Phase 1e)" design section for the full dispatch and
///     compositing rationale. Only a dedicated <c>&lt;p:pic&gt;</c> picture <em>shape</em> is in
///     scope this phase - an ordinary shape's own <em>background</em> picture fill
///     (<c>&lt;p:spPr&gt;/&lt;a:blipFill&gt;</c>, resolved via <see cref="ResolveFill"/>) remains
///     a deliberately separate, still-unsupported case (see <c>PptxDocument.Paint.cs</c>'s
///     <c>FillResolution</c> requirement, unchanged this phase).
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     The <c>uri</c> attribute value identifying the Microsoft SVG blip extension
    ///     (<c>&lt;a:ext uri="{96DAC541-7B7A-43D3-8B79-37D633B846F1}"&gt;&lt;asvg:svgBlip
    ///     .../&gt;&lt;/a:ext&gt;</c>) - see <see cref="HasSvgOnlyExtensionFallback"/>.
    /// </summary>
    private const string SvgBlipExtensionUri = "{96DAC541-7B7A-43D3-8B79-37D633B846F1}";

    /// <summary>
    ///     Resolves a <c>&lt;p:pic&gt;</c> shape's <c>&lt;p:blipFill&gt;</c> element into a
    ///     fully decoded raster <see cref="Surface"/>: resolves its <c>&lt;a:blip r:embed="..."/&gt;</c>
    ///     relationship to the owning media part, resolves that part's content type, and
    ///     dispatches to the matching sibling raster codec's own <c>Load(Stream)</c> entry point.
    /// </summary>
    /// <param name="ownerPartPath">
    ///     The part path of the slide (or other part) that owns <paramref name="blipFillElement"/> -
    ///     needed to resolve the <c>r:embed</c> relationship, which is scoped to its source part's
    ///     own <c>.rels</c> file (see <see cref="ResolveRelationship"/>).
    /// </param>
    /// <param name="blipFillElement">The shape's <c>&lt;p:blipFill&gt;</c> element.</param>
    /// <returns>The fully decoded raster image, as a core <see cref="Surface"/>.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="ownerPartPath"/> or <paramref name="blipFillElement"/> is null.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="blipFillElement"/> has no <c>&lt;a:blip&gt;</c> child, when
    ///     that child has neither an <c>r:embed</c> nor an <c>r:link</c> attribute, or when the
    ///     resolved media part/relationship is otherwise malformed (propagated unchanged from
    ///     <see cref="ResolveRelationship"/>/<see cref="ResolvePart"/>/<see cref="GetPartBytes"/>).
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown (feature token <c>"pptx-image-link"</c>) when <c>&lt;a:blip&gt;</c> declares only
    ///     an <c>r:link</c> (a linked, non-embedded image - requires external/network resolution,
    ///     out of scope this phase), (feature token <c>"pptx-image-svg-only"</c>) when
    ///     <c>&lt;a:blip&gt;</c> declares neither <c>r:embed</c> nor <c>r:link</c> but instead
    ///     carries only a Microsoft SVG extension (<c>&lt;a:extLst&gt;/&lt;a:ext uri="{96DAC541-
    ///     7B7A-43D3-8B79-37D633B846F1}"&gt;&lt;asvg:svgBlip&gt;</c>) with no raster fallback - a
    ///     well-formed, valid "Insert Icon"-style SVG-only picture this package does not yet
    ///     decode, or (feature token <c>"pptx-image-format"</c>) when the resolved media part's
    ///     content type is not one of the five raster formats this package's codecs decode (for
    ///     example an EMF/WMF vector picture, or an SVG image - common PowerPoint picture formats
    ///     this phase's raster-only codecs cannot decode).
    /// </exception>
    /// <remarks>
    ///     A codec decode failure (corrupt image bytes) is <em>not</em> wrapped here - it
    ///     propagates as whatever exception the matching codec's own <c>Load(Stream)</c> throws
    ///     (typically <see cref="InvalidDataException"/> or
    ///     <see cref="Codecs.UnsupportedImageFeatureException"/>), consistent with how this
    ///     package never wraps a sibling package's own, already-well-defined exception types.
    /// </remarks>
    internal Surface ResolvePictureSurface(string ownerPartPath, XElement blipFillElement)
    {
        ArgumentNullException.ThrowIfNull(ownerPartPath);
        ArgumentNullException.ThrowIfNull(blipFillElement);

        var blip = blipFillElement.Element(DrawingNamespace + "blip") ??
            throw new InvalidDataException("A <p:blipFill> element has no <a:blip> child.");

        var embedId = (string?)blip.Attribute(RelationshipRefNamespace + "embed");
        if (embedId is null)
        {
            if (blip.Attribute(RelationshipRefNamespace + "link") is not null)
            {
                throw new PptxUnsupportedFeatureException(
                    "pptx-image-link",
                    "A linked (not embedded) <a:blip r:link=\"...\"> image is not supported.");
            }

            if (HasSvgOnlyExtensionFallback(blip))
            {
                throw new PptxUnsupportedFeatureException(
                    "pptx-image-svg-only",
                    "An <a:blip> declares only a Microsoft SVG extension (<asvg:svgBlip>) with no " +
                    "r:embed/r:link raster fallback; SVG-only pictures are not supported.");
            }

            throw new InvalidDataException("An <a:blip> element has neither an 'r:embed' nor an 'r:link' attribute.");
        }

        var mediaPartPath = ResolveRelationship(ownerPartPath, embedId);
        var contentType = ResolvePart(mediaPartPath);
        var decoder = ResolveRasterDecoder(contentType) ?? throw new PptxUnsupportedFeatureException(
            "pptx-image-format",
            $"Image content type '{contentType}' is not a supported raster format.");

        var bytes = GetPartBytes(mediaPartPath);
        using var stream = new MemoryStream(bytes);
        return decoder(stream);
    }

    /// <summary>
    ///     Maps a media part's resolved OPC content type to the matching sibling raster codec's
    ///     own <c>Load(Stream)</c> entry point - pure dispatch, mirroring
    ///     <c>DemaConsulting.CanvasNet.Svg</c>'s <c>SvgCodec.Image.cs</c>'s own
    ///     <c>ResolveRasterDecoder</c> MIME-type dispatcher exactly (same five codecs, same
    ///     recognized content-type strings).
    /// </summary>
    /// <param name="contentType">The media part's resolved OPC content type.</param>
    /// <returns>
    ///     <see cref="PngCodec.Load(Stream)"/>, <see cref="JpegCodec.Load(Stream)"/>,
    ///     <see cref="BmpCodec.Load(Stream)"/>, <see cref="TiffCodec.Load(Stream)"/>, or
    ///     <see cref="GifCodec.Load(Stream)"/> for the matching recognized content type; otherwise
    ///     <see langword="null"/> for any unrecognized content type (for example
    ///     <c>"image/x-emf"</c>, <c>"image/x-wmf"</c>, or <c>"image/svg+xml"</c>).
    /// </returns>
    private static Func<Stream, Surface>? ResolveRasterDecoder(string contentType) => contentType switch
    {
        "image/png" => PngCodec.Load,
        "image/jpeg" or "image/jpg" => JpegCodec.Load,
        "image/bmp" or "image/x-bmp" or "image/x-ms-bmp" => BmpCodec.Load,
        "image/tiff" or "image/x-tiff" => TiffCodec.Load,
        "image/gif" => GifCodec.Load,
        _ => null
    };

    /// <summary>
    ///     Detects the Microsoft "SVG-only" <c>&lt;a:blip&gt;</c> fallback pattern: an
    ///     <c>&lt;a:extLst&gt;</c> child containing an <c>&lt;a:ext uri="{96DAC541-7B7A-43D3-8B79-
    ///     37D633B846F1}"&gt;</c> wrapping an <c>&lt;asvg:svgBlip&gt;</c> extension element - a
    ///     well-formed, valid OOXML picture ("Insert Icon"-style SVG with no raster fallback) that
    ///     this package does not yet decode, distinct from a genuinely malformed <c>&lt;a:blip&gt;</c>
    ///     with neither an embed/link attribute nor any recognized extension.
    /// </summary>
    /// <param name="blip">The <c>&lt;a:blip&gt;</c> element to inspect.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="blip"/> has an <c>&lt;a:extLst&gt;</c> child
    ///     with an <c>&lt;a:ext&gt;</c> whose <c>uri</c> attribute matches
    ///     <see cref="SvgBlipExtensionUri"/>; otherwise <see langword="false"/>.
    /// </returns>
    private static bool HasSvgOnlyExtensionFallback(XElement blip)
    {
        var extLst = blip.Element(DrawingNamespace + "extLst");
        if (extLst is null)
        {
            return false;
        }

        foreach (var ext in extLst.Elements(DrawingNamespace + "ext"))
        {
            if ((string?)ext.Attribute("uri") == SvgBlipExtensionUri)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Resolves a <c>&lt;p:blipFill&gt;</c>'s optional <c>&lt;a:srcRect&gt;</c> crop element
    ///     into a <see cref="PptxSrcRect"/>.
    /// </summary>
    /// <param name="blipFillElement">The shape's <c>&lt;p:blipFill&gt;</c> element.</param>
    /// <returns>
    ///     The resolved <see cref="PptxSrcRect"/>, or <see langword="null"/> when
    ///     <paramref name="blipFillElement"/> declares no <c>&lt;a:srcRect&gt;</c> (meaning "no
    ///     crop" - the whole source image is sampled).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blipFillElement"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a present <c>&lt;a:srcRect&gt;</c>'s <c>l</c>/<c>t</c>/<c>r</c>/<c>b</c>
    ///     attribute is present but not a valid, finite number.
    /// </exception>
    internal static PptxSrcRect? ResolveSrcRect(XElement blipFillElement)
    {
        ArgumentNullException.ThrowIfNull(blipFillElement);

        var srcRect = blipFillElement.Element(DrawingNamespace + "srcRect");
        if (srcRect is null)
        {
            return null;
        }

        return new PptxSrcRect(
            ParseSrcRectEdge(srcRect, "l"),
            ParseSrcRectEdge(srcRect, "t"),
            ParseSrcRectEdge(srcRect, "r"),
            ParseSrcRectEdge(srcRect, "b"));
    }

    /// <summary>
    ///     Parses a single <c>&lt;a:srcRect&gt;</c> edge attribute (<c>l</c>/<c>t</c>/<c>r</c>/
    ///     <c>b</c>), each OOXML's own 1/100000-of-a-percent unit, into a <c>[0,1]</c>-scaled
    ///     fraction, defaulting to <c>0</c> (no crop on that edge) when the attribute itself is
    ///     absent (per the OOXML schema default).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the attribute is present but not a valid, finite number.
    /// </exception>
    private static float ParseSrcRectEdge(XElement srcRectElement, string attributeName)
    {
        var value = (string?)srcRectElement.Attribute(attributeName);
        if (value is null)
        {
            return 0f;
        }

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidDataException(
                $"An <a:srcRect> element has a non-numeric '{attributeName}' attribute value '{value}'.");
        }

        return parsed / 100000f;
    }

    /// <summary>
    ///     Resolves a <c>&lt;p:pic&gt;</c> picture shape's own <c>&lt;p:spPr&gt;</c> clip geometry -
    ///     its <c>&lt;a:prstGeom&gt;</c> or <c>&lt;a:custGeom&gt;</c> child - into a
    ///     <see cref="Path"/> sized to the picture's own local <c>(0,0)</c>-<c>(widthEmu,
    ///     heightEmu)</c> coordinate space, reusing the exact same preset/custom-geometry dispatch
    ///     <see cref="ResolveShapeGeometry"/> already uses for an ordinary auto-shape
    ///     (<see cref="PptxPresetGeometry.Build"/>/<see cref="ResolveCustomGeometry"/>) - this is a
    ///     thin, picture-specific wrapper around that identical dispatch, not a reimplementation
    ///     (see <c>pptx-document.md</c>'s "Phase 2 Follow-Up: Picture Preset-Geometry Clipping"
    ///     design section).
    /// </summary>
    /// <param name="spPrElement">The picture's own <c>&lt;p:spPr&gt;</c> element.</param>
    /// <param name="widthEmu">The picture's own declared width, in EMU (<see cref="PptxShapeFrame.WidthEmu"/>).</param>
    /// <param name="heightEmu">The picture's own declared height, in EMU (<see cref="PptxShapeFrame.HeightEmu"/>).</param>
    /// <returns>
    ///     <see langword="null"/> (meaning "no clip - paint the full bounding-box rectangle",
    ///     preserving this package's original, pre-fix behavior) when <paramref name="spPrElement"/>
    ///     declares neither <c>&lt;a:prstGeom&gt;</c> nor <c>&lt;a:custGeom&gt;</c> at all, or when
    ///     it declares <c>&lt;a:prstGeom prst="rect"&gt;</c> (an explicit full-rectangle preset
    ///     resolves to the same full bounding-box rectangle a "no clip" paint already produces, so
    ///     clipping to it would be a pure no-op); otherwise the resolved, shape-local
    ///     <see cref="Path"/> to clip the picture's painted image to.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="spPrElement"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a present <c>&lt;a:prstGeom&gt;</c> element has no <c>prst</c> attribute -
    ///     mirrors <see cref="ResolveShapeGeometry"/>'s identical check for an auto-shape.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <c>&lt;a:prstGeom&gt;</c> names a preset this phase does not support - see
    ///     <see cref="PptxPresetGeometry.Build"/>. Propagated unchanged, exactly as it already
    ///     propagates for an auto-shape with the same unsupported preset name.
    /// </exception>
    /// <remarks>
    ///     Unlike <see cref="ResolveShapeGeometry"/> (which always resolves some geometry or
    ///     throws for a shape with neither <c>&lt;a:prstGeom&gt;</c> nor <c>&lt;a:custGeom&gt;</c>
    ///     at all), this method tolerates "no geometry at all" by returning <see langword="null"/>:
    ///     a <c>&lt;p:pic&gt;</c>'s <c>&lt;p:spPr&gt;</c> declaring neither element is a
    ///     schema-valid, historically-unclipped picture, not a malformed document - an auto-shape
    ///     with no resolvable geometry is instead already skipped entirely upstream in
    ///     <c>RenderShape</c>, a different (shape-tree-level, not geometry-resolver-level) point
    ///     of tolerance. <c>&lt;a:custGeom&gt;</c> on a picture is clipped via the same reused
    ///     <see cref="ResolveCustomGeometry"/> resolver (not scoped out), since it costs no
    ///     additional implementation beyond this one dispatch branch.
    /// </remarks>
    internal static Path? ResolvePictureClipPath(XElement spPrElement, float widthEmu, float heightEmu)
    {
        ArgumentNullException.ThrowIfNull(spPrElement);

        var prstGeom = spPrElement.Element(DrawingNamespace + "prstGeom");
        if (prstGeom is not null)
        {
            var prst = (string?)prstGeom.Attribute("prst") ??
                throw new InvalidDataException("An <a:prstGeom> element has no 'prst' attribute.");
            return prst == "rect" ? null : PptxPresetGeometry.Build(prst, widthEmu, heightEmu);
        }

        var custGeom = spPrElement.Element(DrawingNamespace + "custGeom");
        return custGeom is not null ? ResolveCustomGeometry(custGeom, widthEmu, heightEmu) : null;
    }

    /// <summary>
    ///     Resolves a <c>&lt;p:pic&gt;</c> picture shape's own <c>&lt;p:spPr&gt;</c> geometry - its
    ///     <c>&lt;a:prstGeom&gt;</c> or <c>&lt;a:custGeom&gt;</c> child - into a <see cref="Path"/>
    ///     ALWAYS representing a concrete, closed boundary suitable for stroke-outlining the
    ///     picture's own <c>&lt;a:ln&gt;</c> (see <c>RenderPicture</c>) - unlike
    ///     <see cref="ResolvePictureClipPath"/>, which deliberately collapses "no geometry at all"
    ///     and "explicit <c>&lt;a:prstGeom prst="rect"&gt;</c>" to <see langword="null"/> ("no clip
    ///     needed", a pure optimization for its own image-content-clipping use case, where a full
    ///     bounding-box rectangle clip is a no-op). A stroke outline has no equivalent "no-op"
    ///     shortcut: an implicit/explicit full-rectangle picture with an <c>&lt;a:ln&gt;</c> must
    ///     still stroke an actual rectangle boundary, exactly as an ordinary <c>&lt;p:sp&gt;</c>
    ///     auto-shape with an implicit/explicit <c>rect</c> preset already does via
    ///     <see cref="ResolveShapeGeometry"/> - so this method always returns a real path, reusing
    ///     the identical preset/custom-geometry dispatch (<see cref="PptxPresetGeometry.Build"/>/
    ///     <see cref="ResolveCustomGeometry"/>) <see cref="ResolvePictureClipPath"/> itself already
    ///     reuses, rather than reimplementing it (see <c>pptx-document.md</c>'s "Phase 2 Follow-Up:
    ///     Picture Own-Stroke Outline Rendering" design section).
    /// </summary>
    /// <param name="spPrElement">The picture's own <c>&lt;p:spPr&gt;</c> element.</param>
    /// <param name="widthEmu">The picture's own declared width, in EMU (<see cref="PptxShapeFrame.WidthEmu"/>).</param>
    /// <param name="heightEmu">The picture's own declared height, in EMU (<see cref="PptxShapeFrame.HeightEmu"/>).</param>
    /// <returns>
    ///     The resolved, shape-local <see cref="Path"/> to stroke: the resolved preset/custom
    ///     geometry when <paramref name="spPrElement"/> declares either, or the implicit full
    ///     bounding-box rectangle (<c>PptxPresetGeometry.Build("rect", widthEmu, heightEmu)</c>)
    ///     when it declares neither - never <see langword="null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="spPrElement"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a present <c>&lt;a:prstGeom&gt;</c> element has no <c>prst</c> attribute -
    ///     mirrors <see cref="ResolvePictureClipPath"/>'s/<see cref="ResolveShapeGeometry"/>'s
    ///     identical check for an auto-shape.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <c>&lt;a:prstGeom&gt;</c> names a preset this phase does not support - see
    ///     <see cref="PptxPresetGeometry.Build"/>. Propagated unchanged, exactly as it already
    ///     propagates for an auto-shape or for <see cref="ResolvePictureClipPath"/> with the same
    ///     unsupported preset name.
    /// </exception>
    internal static Path ResolvePictureGeometryPath(XElement spPrElement, float widthEmu, float heightEmu)
    {
        ArgumentNullException.ThrowIfNull(spPrElement);

        var prstGeom = spPrElement.Element(DrawingNamespace + "prstGeom");
        if (prstGeom is not null)
        {
            var prst = (string?)prstGeom.Attribute("prst") ??
                throw new InvalidDataException("An <a:prstGeom> element has no 'prst' attribute.");
            return PptxPresetGeometry.Build(prst, widthEmu, heightEmu);
        }

        var custGeom = spPrElement.Element(DrawingNamespace + "custGeom");
        if (custGeom is not null)
        {
            return ResolveCustomGeometry(custGeom, widthEmu, heightEmu);
        }

        // Neither geometry child is present: an implicit full-rectangle boundary - the same
        // bounding-box footprint an unclipped picture already paints its image content into -
        // so an <a:ln> on a geometry-less picture still strokes a complete, closed rectangle
        // rather than being silently dropped.
        return PptxPresetGeometry.Build("rect", widthEmu, heightEmu);
    }

    /// <summary>
    ///     Composites a decoded picture <paramref name="image"/> onto <paramref name="surface"/>,
    ///     mapping its (optionally <paramref name="srcRect"/>-cropped) unit square through a
    ///     <c>CreateScale(widthEmu, heightEmu) * shapeToSurfaceTransform</c> transform and
    ///     nearest-neighbor sampling the source pixel for every destination pixel in the
    ///     transformed footprint's device-space bounding box - mirroring
    ///     <c>DemaConsulting.CanvasNet.Pdf</c>'s <c>PdfDocument.Images.cs</c>'s own
    ///     <c>CompositeImageOntoSurface</c> bounding-box rasterization loop exactly, with one
    ///     deliberate difference: <strong>no <c>(1 - v)</c> row flip</strong> is applied, because
    ///     (unlike PDF's own y-up image-space convention) this package's shape-local/surface space
    ///     is already y-down (see <c>PaintTextLayout</c>'s own documented y-down convention) -
    ///     image row <c>0</c> is already the image's own top row in this same y-down sense, so no
    ///     flip is needed to land it at the shape's own top edge. Each sampled source pixel is
    ///     alpha-blended "over" the existing destination pixel via
    ///     <see cref="Rgba32.CompositeOver"/> (standard Porter-Duff "over" compositing), exactly
    ///     matching <c>CompositeImageOntoSurface</c>'s own per-pixel blending - not overwritten
    ///     outright - so a source pixel with a non-opaque (including fully transparent) alpha
    ///     channel lets the existing destination content show through correctly instead of being
    ///     replaced by whatever RGB value happens to be stored alongside that transparent alpha.
    /// </summary>
    /// <param name="surface">The destination surface to paint onto.</param>
    /// <param name="image">The already fully decoded source image (see <see cref="ResolvePictureSurface"/>).</param>
    /// <param name="srcRect">
    ///     The resolved <c>&lt;a:srcRect&gt;</c> crop (see <see cref="ResolveSrcRect"/>), or
    ///     <see langword="null"/> for "no crop" (the whole source image is sampled).
    /// </param>
    /// <param name="shapeToSurfaceTransform">
    ///     The transform mapping the owning shape's own local <c>(0,0)</c>-<c>(widthEmu,
    ///     heightEmu)</c> coordinate space into surface pixel space (the shape's own
    ///     <see cref="PptxShapeFrame.Transform"/>, or a further group-composed transform).
    /// </param>
    /// <param name="widthEmu">The owning shape's own declared width, in EMU (<see cref="PptxShapeFrame.WidthEmu"/>).</param>
    /// <param name="heightEmu">The owning shape's own declared height, in EMU (<see cref="PptxShapeFrame.HeightEmu"/>).</param>
    /// <param name="clipPath">
    ///     The picture's own resolved, shape-local clip geometry (see
    ///     <see cref="ResolvePictureClipPath"/>), or <see langword="null"/> (the default) to paint
    ///     the full bounding-box rectangle unclipped - this package's original behavior. When
    ///     non-null, <paramref name="clipPath"/> is transformed by <paramref name="shapeToSurfaceTransform"/>
    ///     (the same transform <see cref="ResolvePictureClipPath"/>'s caller already uses to place
    ///     the picture itself) and filled as an opaque white mask onto a fresh, same-size,
    ///     transparent <see cref="Surface"/> via <see cref="PathFiller.Fill(Surface, Path, Rgba32, FillRule, float)"/>
    ///     (reusing the exact same anti-aliased path-fill rasterizer every shape/table fill in
    ///     this codebase already uses, rather than inventing a new clip primitive); each sampled
    ///     source pixel's own alpha channel is then scaled by that mask pixel's alpha (<c>0</c>
    ///     outside the clip geometry, <c>255</c> fully inside it, an anti-aliased in-between value
    ///     exactly on its edge) before compositing via <see cref="Rgba32.CompositeOver"/> - see
    ///     <c>pptx-document.md</c>'s "Phase 2 Follow-Up: Picture Preset-Geometry Clipping" design
    ///     section for the full rationale.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> or <paramref name="image"/> is null.</exception>
    /// <remarks>
    ///     A non-invertible (degenerate, zero-area) composed transform silently paints nothing,
    ///     rather than throwing - mirroring <c>CompositeImageOntoSurface</c>'s own documented
    ///     degenerate-transform behavior.
    /// </remarks>
    internal static void PaintPicture(
        Surface surface,
        Surface image,
        PptxSrcRect? srcRect,
        Matrix3x2 shapeToSurfaceTransform,
        float widthEmu,
        float heightEmu,
        Path? clipPath = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(image);

        var unitToSurface = Matrix3x2.CreateScale(widthEmu, heightEmu) * shapeToSurfaceTransform;
        if (!Matrix3x2.Invert(unitToSurface, out var surfaceToUnit))
        {
            return;
        }

        var corner00 = Vector2.Transform(new Vector2(0, 0), unitToSurface);
        var corner10 = Vector2.Transform(new Vector2(1, 0), unitToSurface);
        var corner01 = Vector2.Transform(new Vector2(0, 1), unitToSurface);
        var corner11 = Vector2.Transform(new Vector2(1, 1), unitToSurface);

        var minX = MathF.Min(MathF.Min(corner00.X, corner10.X), MathF.Min(corner01.X, corner11.X));
        var maxX = MathF.Max(MathF.Max(corner00.X, corner10.X), MathF.Max(corner01.X, corner11.X));
        var minY = MathF.Min(MathF.Min(corner00.Y, corner10.Y), MathF.Min(corner01.Y, corner11.Y));
        var maxY = MathF.Max(MathF.Max(corner00.Y, corner10.Y), MathF.Max(corner01.Y, corner11.Y));

        var startX = Math.Max(0, (int)MathF.Floor(minX));
        var endX = Math.Min(surface.Width - 1, (int)MathF.Ceiling(maxX));
        var startY = Math.Max(0, (int)MathF.Floor(minY));
        var endY = Math.Min(surface.Height - 1, (int)MathF.Ceiling(maxY));

        var left = srcRect?.Left ?? 0f;
        var top = srcRect?.Top ?? 0f;
        var right = srcRect?.Right ?? 0f;
        var bottom = srcRect?.Bottom ?? 0f;

        // Build the clip-coverage mask (see this method's own <paramref name="clipPath"/> remarks)
        // once, up front, rather than per-pixel - the same opaque-white-path-fill-onto-a-fresh-
        // transparent-surface pattern already used by DemaConsulting.CanvasNet.Svg's own
        // ApplyClipPath, repurposed here as a per-pixel alpha-scaling mask instead of a
        // post-composite coverage clip.
        using var clipMask = clipPath is null ? null : new Surface(surface.Width, surface.Height);
        if (clipPath is not null && clipMask is not null)
        {
            var transformedClip = clipPath.Transform(shapeToSurfaceTransform);
            PathFiller.Fill(clipMask, transformedClip, new Rgba32(255, 255, 255, 255));
        }

        for (var y = startY; y <= endY; y++)
        {
            for (var x = startX; x <= endX; x++)
            {
                var devicePoint = new Vector2(x + 0.5f, y + 0.5f);
                var unitPoint = Vector2.Transform(devicePoint, surfaceToUnit);
                var u = unitPoint.X;
                var v = unitPoint.Y;
                if (u < 0 || u >= 1 || v < 0 || v >= 1)
                {
                    continue;
                }

                byte maskAlpha = 255;
                if (clipMask is not null)
                {
                    maskAlpha = clipMask[x, y].A;
                    if (maskAlpha == 0)
                    {
                        continue;
                    }
                }

                // Remap the unit-square sample point through the srcRect crop: imageU/imageV are
                // the fraction of the way across the *uncropped* source image this destination
                // pixel samples from - no (1 - v) flip, per this method's own remarks.
                var imageU = left + u * (1f - left - right);
                var imageV = top + v * (1f - top - bottom);

                var column = Math.Clamp((int)MathF.Floor(imageU * image.Width), 0, image.Width - 1);
                var row = Math.Clamp((int)MathF.Floor(imageV * image.Height), 0, image.Height - 1);
                var sourcePixel = image[column, row];
                if (maskAlpha != 255)
                {
                    var scaledAlpha = (byte)Math.Clamp(MathF.Round(sourcePixel.A * (maskAlpha / 255f)), 0, 255);
                    sourcePixel = new Rgba32(sourcePixel.R, sourcePixel.G, sourcePixel.B, scaledAlpha);
                }

                surface[x, y] = Rgba32.CompositeOver(surface[x, y], sourcePixel);
            }
        }
    }
}

using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore blipfill srcrect pptx embed

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
    ///     out of scope this phase), or (feature token <c>"pptx-image-format"</c>) when the
    ///     resolved media part's content type is not one of the five raster formats this package's
    ///     codecs decode (for example an EMF/WMF vector picture, or an SVG image - common
    ///     PowerPoint picture formats this phase's raster-only codecs cannot decode).
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
    ///     flip is needed to land it at the shape's own top edge.
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
        float heightEmu)
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

                // Remap the unit-square sample point through the srcRect crop: imageU/imageV are
                // the fraction of the way across the *uncropped* source image this destination
                // pixel samples from - no (1 - v) flip, per this method's own remarks.
                var imageU = left + u * (1f - left - right);
                var imageV = top + v * (1f - top - bottom);

                var column = Math.Clamp((int)MathF.Floor(imageU * image.Width), 0, image.Width - 1);
                var row = Math.Clamp((int)MathF.Floor(imageV * image.Height), 0, image.Height - 1);
                surface[x, y] = image[column, row];
            }
        }
    }
}

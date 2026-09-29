// cspell:ignore xobject bitdepth
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     Handles the <c>/name Do</c> operator: resolves a named XObject from the current page's
    ///     <c>/Resources/XObject</c> dictionary and, for an image XObject, decodes and composites
    ///     it onto the destination surface through the current transformation matrix.
    /// </summary>
    /// <remarks>
    ///     A <c>/Subtype /Form</c> XObject is explicitly rejected with
    ///     <see cref="UnsupportedImageFeatureException"/> - not silently skipped - since Form
    ///     XObject rendering (a nested content stream with its own resources) is out of this
    ///     phase's scope.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 name operand, when the name
    ///     is not declared in the current page's <c>/Resources/XObject</c> dictionary (or no
    ///     <c>/Resources</c> exists at all), when the resolved value is not a stream, or when the
    ///     stream's <c>/Subtype</c> is missing or is neither <c>Image</c> nor <c>Form</c>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown for a <c>/Subtype /Form</c> XObject, or propagated from
    ///     <see cref="DecodeImageXObject"/> for an unsupported image feature.
    /// </exception>
    private void OpDrawXObject(IReadOnlyList<PdfObject> operands)
    {
        RequireOperandCount(operands, "Do", 1);
        if (operands[0].Kind != PdfKind.Name)
        {
            throw new InvalidDataException("Operator 'Do' requires a name operand.");
        }

        var name = operands[0].Text;
        var xObjectDictionary = _resources?.Get("XObject");
        var resolvedDictionary = xObjectDictionary is null ? null : Resolve(xObjectDictionary);
        var entry = resolvedDictionary?.Get(name);
        if (entry is null)
        {
            throw new InvalidDataException(
                $"Undefined XObject '/{name}' (not declared in the current page's /Resources/XObject).");
        }

        var xObject = Resolve(entry);
        if (xObject.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException($"XObject '/{name}' does not resolve to a stream.");
        }

        var subtype = GetNameValue(xObject, "Subtype");
        switch (subtype)
        {
            case "Form":
                throw new UnsupportedImageFeatureException(
                    "pdf-form-xobject",
                    "Form XObjects (nested content streams) are not supported in this phase.");

            case "Image":
                var image = DecodeImageXObject(xObject);
                CompositeImageOntoSurface(image, _gs.CurrentTransform);
                break;

            default:
                throw new InvalidDataException($"XObject '/{name}' has a missing or unrecognized /Subtype.");
        }
    }

    /// <summary>
    ///     Decodes an image XObject stream into a fully opaque <see cref="Surface"/> of its
    ///     declared <c>/Width</c> x <c>/Height</c>.
    /// </summary>
    /// <remarks>
    ///     A <c>/Filter /DCTDecode</c> image (and no other filter) is decoded directly via
    ///     <see cref="Codecs.JpegCodec.Load(Stream)"/> against the stream's raw bytes, trusting
    ///     the decoded <see cref="Surface"/>'s own width/height over the PDF <c>/Width</c>/
    ///     <c>/Height</c> entries (a documented leniency: a mismatch is tolerated, not rejected).
    ///     Every other supported case decodes via the general <c>FlateDecode</c>(+predictor)
    ///     pipeline and interprets the resulting raw samples per <c>/ColorSpace</c> (device color
    ///     spaces only) and <c>/BitsPerComponent</c> (<c>8</c> only). <c>/SMask</c>/<c>/Mask</c>
    ///     are never consulted - every decoded image is treated as fully opaque, a documented
    ///     Phase 3 limitation.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Width</c>/<c>/Height</c> is missing, not a number, or not positive,
    ///     when <c>/ColorSpace</c> is missing, or when <c>DCTDecode</c> is combined with any
    ///     other filter.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/ColorSpace</c> names an unsupported color space, or when
    ///     <c>/BitsPerComponent</c> is not <c>8</c> (for a non-<c>DCTDecode</c> image).
    /// </exception>
    private Surface DecodeImageXObject(PdfObject stream)
    {
        var width = RequireIntEntry(stream, "Width");
        var height = RequireIntEntry(stream, "Height");
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Image XObject /Width and /Height must be positive integers.");
        }

        var (filterNames, _) = ResolveFilterPipeline(stream);
        if (filterNames.Count == 1 && filterNames[0] == "DCTDecode")
        {
            var rawBytes = GetStreamRawBytes(stream);
            return JpegCodec.Load(new MemoryStream(rawBytes));
        }

        if (filterNames.Contains("DCTDecode"))
        {
            throw new InvalidDataException("DCTDecode combined with another filter is not supported.");
        }

        var decoded = GetStreamDecodedBytes(stream);

        var colorSpaceObject = stream.Get("ColorSpace")
            ?? throw new InvalidDataException("Image XObject is missing required /ColorSpace.");
        var colorSpaceKind = ResolveColorSpaceValue(Resolve(colorSpaceObject));

        var bitsPerComponent = RequireIntEntry(stream, "BitsPerComponent");
        if (bitsPerComponent != 8)
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-image-bitdepth-{bitsPerComponent}",
                $"Image XObjects with {bitsPerComponent}-bit components are not supported; only 8 is supported.");
        }

        var componentCount = ComponentCount(colorSpaceKind);
        var surface = new Surface(width, height);
        var rowBytes = componentCount * width;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * rowBytes;
            for (var x = 0; x < width; x++)
            {
                var pixelOffset = rowOffset + (x * componentCount);
                surface[x, y] = SamplesToColor(colorSpaceKind, decoded, pixelOffset, componentCount);
            }
        }

        return surface;
    }

    /// <summary>
    ///     Converts <paramref name="count"/> consecutive raw 8-bit samples (each in <c>[0, 255]</c>)
    ///     starting at <paramref name="offset"/> into an opaque <see cref="Rgba32"/> color, reusing
    ///     <see cref="ColorFromComponents"/> so image decoding and the <c>rg</c>/<c>k</c>/<c>sc</c>
    ///     color operators share exactly the same color-space conversion formulas.
    /// </summary>
    private static Rgba32 SamplesToColor(PdfColorSpaceKind colorSpace, byte[] decoded, int offset, int count)
    {
        var components = new double[count];
        for (var i = 0; i < count; i++)
        {
            components[i] = decoded[offset + i] / 255.0;
        }

        return ColorFromComponents(colorSpace, components);
    }

    /// <summary>
    ///     Composites a decoded image onto the destination surface, mapping the image's unit
    ///     square (<c>[0, 1] x [0, 1]</c>) through <paramref name="ctm"/> onto device space and
    ///     sampling the nearest source pixel for every destination pixel in the transformed
    ///     footprint's device-space bounding box.
    /// </summary>
    /// <remarks>
    ///     Nearest-neighbor sampling only - no bilinear interpolation - a documented Phase 3
    ///     simplification. A non-invertible (degenerate) <paramref name="ctm"/> silently paints
    ///     nothing, rather than throwing. Per the PDF specification's image-space convention,
    ///     image sample row <c>0</c> is the <em>top</em> of the unit square (the opposite of
    ///     user-space's y-up convention): <c>row = floor((1 - v) * image.Height)</c>.
    /// </remarks>
    private void CompositeImageOntoSurface(Surface image, Matrix3x2 ctm)
    {
        if (!Matrix3x2.Invert(ctm, out var inverse))
        {
            return;
        }

        var corner00 = Vector2.Transform(new Vector2(0, 0), ctm);
        var corner10 = Vector2.Transform(new Vector2(1, 0), ctm);
        var corner01 = Vector2.Transform(new Vector2(0, 1), ctm);
        var corner11 = Vector2.Transform(new Vector2(1, 1), ctm);

        var minX = Math.Min(Math.Min(corner00.X, corner10.X), Math.Min(corner01.X, corner11.X));
        var maxX = Math.Max(Math.Max(corner00.X, corner10.X), Math.Max(corner01.X, corner11.X));
        var minY = Math.Min(Math.Min(corner00.Y, corner10.Y), Math.Min(corner01.Y, corner11.Y));
        var maxY = Math.Max(Math.Max(corner00.Y, corner10.Y), Math.Max(corner01.Y, corner11.Y));

        var startX = Math.Max(0, (int)Math.Floor(minX));
        var endX = Math.Min(_surface.Width - 1, (int)Math.Ceiling(maxX));
        var startY = Math.Max(0, (int)Math.Floor(minY));
        var endY = Math.Min(_surface.Height - 1, (int)Math.Ceiling(maxY));

        for (var y = startY; y <= endY; y++)
        {
            for (var x = startX; x <= endX; x++)
            {
                var devicePoint = new Vector2(x + 0.5f, y + 0.5f);
                var unitPoint = Vector2.Transform(devicePoint, inverse);
                var u = unitPoint.X;
                var v = unitPoint.Y;
                if (u < 0 || u >= 1 || v < 0 || v >= 1)
                {
                    continue;
                }

                var column = Math.Clamp((int)Math.Floor(u * image.Width), 0, image.Width - 1);
                var row = Math.Clamp((int)Math.Floor((1 - v) * image.Height), 0, image.Height - 1);
                _surface[x, y] = image[column, row];
            }
        }
    }

    /// <summary>Reads a required integer dictionary entry (resolving an indirect reference).</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the entry is absent or does not resolve to a number.
    /// </exception>
    private int RequireIntEntry(PdfObject dictionary, string key)
    {
        var entry = dictionary.Get(key) ?? throw new InvalidDataException($"Stream is missing required /{key}.");
        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Number)
        {
            throw new InvalidDataException($"/{key} must be a number.");
        }

        return (int)resolved.Number;
    }
}

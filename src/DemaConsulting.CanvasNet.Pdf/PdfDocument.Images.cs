// cspell:ignore xobject bitdepth ccittfax EOFB
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
    ///     A <c>/Subtype /Form</c> XObject is decoded and executed as a nested content stream by
    ///     <see cref="OpDrawFormXObject"/> - see that method's own remarks for exactly what is
    ///     (and is not) supported.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="operands"/> is not exactly 1 name operand, when the name
    ///     is not declared in the current page's <c>/Resources/XObject</c> dictionary (or no
    ///     <c>/Resources</c> exists at all), when the resolved value is not a stream, when the
    ///     stream's <c>/Subtype</c> is missing or is neither <c>Image</c> nor <c>Form</c>, or
    ///     propagated from <see cref="OpDrawFormXObject"/> (recursion-depth exceeded, or a
    ///     malformed Form <c>/Matrix</c>).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="DecodeImageXObject"/> for an unsupported image feature.
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
                OpDrawFormXObject(xObject);
                break;

            case "Image":
                var image = DecodeImageXObject(xObject);
                CompositeImageOntoSurface(image, _gs.CurrentTransform);
                break;

            default:
                throw new InvalidDataException($"XObject '/{name}' has a missing or unrecognized /Subtype.");
        }
    }

    /// <summary>
    ///     The maximum number of nested <c>/Subtype /Form</c> XObject invocations
    ///     <see cref="OpDrawFormXObject"/> allows before failing closed, bounding both pathological
    ///     (self-referencing) Form content and the native C# call-stack depth this interpreter
    ///     uses to execute nested content streams.
    /// </summary>
    private const int MaxFormNestingDepth = 12;

    /// <summary>
    ///     The current <c>/Subtype /Form</c> XObject nesting depth, incremented/decremented around
    ///     every <see cref="OpDrawFormXObject"/> call. Reset to <c>0</c> at the start of every
    ///     top-level <see cref="ExecuteContentStream"/> call.
    /// </summary>
    private int _formNestingDepth;

    /// <summary>
    ///     Handles a <c>/Subtype /Form</c> XObject resolved by <see cref="OpDrawXObject"/>:
    ///     decodes its content stream and executes it as a nested content stream, implicitly
    ///     bracketed like <c>q</c> ... <c>Q</c> around the invoking stream's own graphics state
    ///     and <c>/Resources</c>.
    /// </summary>
    /// <param name="formStream">The already-resolved <c>/Subtype /Form</c> stream object.</param>
    /// <remarks>
    ///     <para>
    ///         The Form's optional <c>/Matrix</c> (read by <see cref="ReadFormMatrix"/>; identity
    ///         when absent) is concatenated into the current transformation matrix using exactly
    ///         the same left-multiply convention as the <c>cm</c> operator (see
    ///         <see cref="OpConcatMatrix"/>): the Form's matrix is applied first/innermost, the
    ///         CTM in effect when <c>Do</c> was invoked is applied second/outermost. The Form's
    ///         own <c>/Resources</c> dictionary is used when present; otherwise the invoking
    ///         stream's current <see cref="_resources"/> is used unchanged (resource-scope
    ///         fallback).
    ///     </para>
    ///     <para>
    ///         The nested content stream inherits the invoking stream's current graphics state
    ///         (colors, line style, font, etc.) as its own starting state, and a fresh, empty
    ///         graphics-state stack (so an unbalanced <c>q</c>/<c>Q</c> inside the Form can never
    ///         touch the invoking stream's own saved states - see <see cref="OpPopGraphicsState"/>'s
    ///         own documented leniency toward a bare <c>Q</c>). Once the nested execution returns
    ///         (successfully or via a thrown exception), the invoking stream's own
    ///         <see cref="_resources"/>, graphics state, graphics-state stack, and pending clip
    ///         fill rule (<see cref="_pendingClipFillRule"/>) are restored exactly as they were
    ///         before this method ran - mutations made inside the Form (CTM, colors, font
    ///         selection, an unconsumed <c>W</c>/<c>W*</c>, etc.) never leak back out, matching an
    ///         implicit <c>q</c> ... <c>Q</c> bracketing. Path-construction state (<see cref="_pathBuilder"/>,
    ///         <see cref="_currentPoint"/>, etc.) and <see cref="_fontCache"/> are deliberately
    ///         <em>not</em> saved/restored: they are not part of the PDF graphics-state stack, and
    ///         any path-painting/surface side effects performed by the Form's content must persist
    ///         exactly like any other painting operator's side effects.
    ///     </para>
    ///     <para>
    ///         <strong>Phase 13 limitations</strong>: no <c>/BBox</c> clipping is applied (the
    ///         Form's content paints without being clipped to its declared bounding box), and no
    ///         <c>/Group</c> (transparency group) handling is performed - the Form's content
    ///         simply paints directly onto the destination surface exactly like the invoking
    ///         stream's own content.
    ///     </para>
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the current nesting depth already equals <see cref="MaxFormNestingDepth"/>,
    ///     when <c>/Matrix</c> is present but is not an array of exactly 6 numbers (propagated
    ///     from <see cref="ReadFormMatrix"/>), or propagated from nested execution of the Form's
    ///     own content stream.
    /// </exception>
    private void OpDrawFormXObject(PdfObject formStream)
    {
        if (_formNestingDepth >= MaxFormNestingDepth)
        {
            throw new InvalidDataException(
                $"Form XObject nesting exceeds the maximum supported depth of {MaxFormNestingDepth}.");
        }

        var formMatrix = ReadFormMatrix(formStream);
        var ownResources = formStream.Get("Resources");
        var resolvedFormResources = ownResources is null ? _resources : Resolve(ownResources);
        var contentBytes = GetStreamDecodedBytes(formStream);

        var savedResources = _resources;
        var savedGs = _gs;
        var savedGsStack = _gsStack;
        var savedPendingClipFillRule = _pendingClipFillRule;
        var savedTextClipBuilder = _textClipBuilder;
        var savedTextClipPending = _textClipPending;
        _textClipBuilder = null;
        _textClipPending = false;
        _formNestingDepth++;
        try
        {
            _resources = resolvedFormResources;
            var nestedGs = savedGs.Clone();
            nestedGs.CurrentTransform = formMatrix * savedGs.CurrentTransform;
            _gs = nestedGs;
            _gsStack = new Stack<GraphicsState>();
            _pendingClipFillRule = null;
            ExecuteOperators(contentBytes);
        }
        finally
        {
            _formNestingDepth--;
            _resources = savedResources;
            _gs = savedGs;
            _gsStack = savedGsStack;
            _pendingClipFillRule = savedPendingClipFillRule;
            _textClipBuilder = savedTextClipBuilder;
            _textClipPending = savedTextClipPending;
        }
    }

    /// <summary>
    ///     Reads a <c>/Subtype /Form</c> XObject's optional <c>/Matrix</c> entry: the identity
    ///     matrix when absent, otherwise an array of exactly 6 numbers interpreted exactly like
    ///     the <c>cm</c> operator's 6 operands.
    /// </summary>
    /// <param name="formStream">The already-resolved <c>/Subtype /Form</c> stream object.</param>
    /// <returns>The Form's declared matrix, or <see cref="Matrix3x2.Identity"/> when absent.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Matrix</c> is present but is not an array of exactly 6 numbers.
    /// </exception>
    private Matrix3x2 ReadFormMatrix(PdfObject formStream) => ReadOptionalMatrix(formStream);

    /// <summary>
    ///     Reads a dictionary's optional <c>/Matrix</c>-style entry: the identity matrix when
    ///     absent, otherwise an array of exactly 6 numbers interpreted exactly like the
    ///     <c>cm</c> operator's 6 operands. Shared by <see cref="ReadFormMatrix"/> (a Form
    ///     XObject's own <c>/Matrix</c>) and the Pattern-space <c>/Matrix</c> resolution in
    ///     <c>PdfDocument.Patterns.cs</c>, so both parse the exact same array shape via one
    ///     implementation.
    /// </summary>
    /// <param name="dictionary">The already-resolved dictionary (or stream) object.</param>
    /// <param name="key">The dictionary key to read. Defaults to <c>"Matrix"</c>.</param>
    /// <returns>The declared matrix, or <see cref="Matrix3x2.Identity"/> when absent.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the entry is present but is not an array of exactly 6 numbers.
    /// </exception>
    private Matrix3x2 ReadOptionalMatrix(PdfObject dictionary, string key = "Matrix")
    {
        var matrixEntry = dictionary.Get(key);
        if (matrixEntry is null)
        {
            return Matrix3x2.Identity;
        }

        var resolved = Resolve(matrixEntry);
        if (resolved.Kind != PdfKind.Array || resolved.Items.Count != 6)
        {
            throw new InvalidDataException($"/{key} must be an array of exactly 6 numbers.");
        }

        var values = new double[6];
        for (var i = 0; i < 6; i++)
        {
            var item = Resolve(resolved.Items[i]);
            if (item.Kind != PdfKind.Number)
            {
                throw new InvalidDataException($"/{key} entries must all be numbers.");
            }

            values[i] = item.Number;
        }

        return new Matrix3x2(
            (float)values[0],
            (float)values[1],
            (float)values[2],
            (float)values[3],
            (float)values[4],
            (float)values[5]);
    }

    /// <summary>
    ///     Decodes an image XObject stream into a <see cref="Surface"/> of its declared
    ///     <c>/Width</c> x <c>/Height</c>: opaque unless the image has an <c>/SMask</c> (or JPX
    ///     opacity channel), which supplies the alpha channel.
    /// </summary>
    /// <remarks>
    ///     A <c>/Filter /DCTDecode</c> image (and no other filter) is decoded directly via
    ///     <see cref="Codecs.JpegCodec.Load(Stream)"/> against the stream's raw bytes, trusting
    ///     the decoded <see cref="Surface"/>'s own width/height over the PDF <c>/Width</c>/
    ///     <c>/Height</c> entries (a documented leniency: a mismatch is tolerated, not rejected).
    ///     A <c>/Filter /CCITTFaxDecode</c> image (and no other filter) is likewise decoded
    ///     directly via <see cref="DecodeCcittFax"/> against the stream's raw bytes: only Group 4
    ///     (<c>/DecodeParms /K</c> negative) ITU-T T.6 two-dimensional (MMR) coding is supported -
    ///     Group 3 (<c>/K</c> <c>0</c> or greater) and an explicit <c>/EndOfLine true</c> both
    ///     fail closed - and the decoded samples are still routed through the general
    ///     <c>/ColorSpace</c> pipeline below (unlike <c>DCTDecode</c>, which never touches it),
    ///     using <c>/DecodeParms</c>'s own <c>/Columns</c>/<c>/Rows</c> (not <c>/Width</c>/
    ///     <c>/Height</c>) as the authoritative surface dimensions, mirroring <c>DCTDecode</c>'s
    ///     own "trust the decoder's own dimensions" leniency; <c>/ColorSpace</c> defaults to
    ///     <c>DeviceGray</c> when absent (CCITT images conventionally omit it), and any resolved
    ///     color space with more than 1 component is rejected. <c>/EndOfBlock</c> is never
    ///     consulted - decoding always stops after exactly the resolved row count, so any
    ///     trailing <c>EOFB</c>/<c>RTC</c> marker bits (or a partial final block truncated before
    ///     the full row count) are never specially detected or reported. Every other supported
    ///     case decodes via the general <c>FlateDecode</c>(+predictor) pipeline and interprets
    ///     the resulting raw samples per <c>/ColorSpace</c> (device color spaces, <c>/ICCBased</c>,
    ///     and <c>/Indexed</c> - see <see cref="PdfColorSpace"/>) and <c>/BitsPerComponent</c>
    ///     (<c>1</c>, <c>2</c>, <c>4</c>, <c>8</c> or <c>16</c>; 16-bit samples keep their high
    ///     byte, and <c>/Indexed</c> images may not be 16-bit). A <c>/Filter /JPXDecode</c> image (and no other filter) is decoded
    ///     by <see cref="DecodeJpxImageXObject"/>. An explicit <c>/SMask</c> image (any supported
    ///     encoding, including <c>JPXDecode</c>) supplies the alpha channel, resampled to the
    ///     base image with nearest-neighbor sampling (the mask must be a single-component,
    ///     non-<c>/Indexed</c> image and is multiplied into the base surface in place); the
    ///     <c>/Decode</c> array is applied to the samples of every encoding (including soft-mask
    ///     images and <c>DCTDecode</c> images; 4-channel CMYK/YCCK JPEGs are rejected outright by
    ///     <see cref="JpegCodec"/>, so a <c>/Decode</c> can never be silently ignored on one); <c>/Mask</c> (stencil/color-key masking) and
    ///     <c>/Matte</c> are never consulted, a documented limitation. JPEG 2000 is never
    ///     decoded from an inline image because inline images (<c>BI</c>/<c>ID</c>/<c>EI</c>)
    ///     are not supported at all (the operators are ignored), which also satisfies the
    ///     specification's restriction of <c>JPXDecode</c> to image XObjects.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Width</c>/<c>/Height</c> is missing, not a number, or not positive,
    ///     when <c>/ColorSpace</c> is missing (for a non-<c>CCITTFaxDecode</c>/non-<c>JPXDecode</c> image), when
    ///     <c>DCTDecode</c> or <c>JPXDecode</c> is combined with any other filter, when
    ///     <c>CCITTFaxDecode</c> is
    ///     combined with any other filter, or propagated from <see cref="DecodeCcittFax"/> for a
    ///     malformed/truncated CCITT bit stream.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/ColorSpace</c> names an unsupported color space, when
    ///     <c>/BitsPerComponent</c> is not <c>1</c>/<c>2</c>/<c>4</c>/<c>8</c>/<c>16</c> (for a non-<c>DCTDecode</c>/non-
    ///     <c>CCITTFaxDecode</c> image), when a <c>CCITTFaxDecode</c> image's resolved
    ///     <c>/ColorSpace</c> has more than 1 component, or propagated from
    ///     <see cref="DecodeCcittFax"/> when <c>/DecodeParms /K</c> is <c>0</c> or greater
    ///     (Group 3) or <c>/EndOfLine</c> is <see langword="true"/>.
    /// </exception>
    private Surface DecodeImageXObject(PdfObject stream)
    {
        var surface = DecodeImageSamples(stream).Surface;
        var softMaskEntry = stream.Get("SMask");
        if (softMaskEntry is null)
        {
            return surface;
        }

        try
        {
            var softMask = Resolve(softMaskEntry);
            if (softMask.Kind != PdfKind.Stream)
            {
                throw new InvalidDataException("Image XObject /SMask must be an image XObject stream.");
            }

            if (GetNameValue(softMask, "Subtype") != "Image")
            {
                throw new InvalidDataException("Image XObject /SMask must be an image XObject (/Subtype /Image).");
            }

            // The soft-mask image is decoded through exactly the same sample pipeline (including
            // JPXDecode and /Decode) but its own /SMask, if any, is never consulted (no nested masks).
            var mask = DecodeImageSamples(softMask);
            using var maskSurface = mask.Surface;
            if (!mask.IsGray)
            {
                throw new InvalidDataException("Image XObject /SMask must be a single-component DeviceGray image.");
            }

            ApplySoftMask(surface, maskSurface);
            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Multiplies the alpha channel of <paramref name="image"/>, in place, by the luminance
    ///     (red channel of the DeviceGray-decoded mask) of <paramref name="mask"/>, resampling the
    ///     mask to the image's dimensions with nearest-neighbor sampling.
    /// </summary>
    private static void ApplySoftMask(Surface image, Surface mask)
    {
        for (var y = 0; y < image.Height; y++)
        {
            var my = (int)((long)y * mask.Height / image.Height);
            for (var x = 0; x < image.Width; x++)
            {
                var mx = (int)((long)x * mask.Width / image.Width);
                var pixel = image[x, y];
                var opacity = mask[mx, my].R;
                image[x, y] = new Rgba32(pixel.R, pixel.G, pixel.B, (byte)(((pixel.A * opacity) + 127) / 255));
            }
        }
    }

    /// <summary>
    ///     A decoded image-sample surface together with whether it is a single-component,
    ///     non-<c>/Indexed</c> (gray) image - the only kind valid as a soft-mask image.
    /// </summary>
    private readonly record struct DecodedImage(Surface Surface, bool IsGray);

    /// <summary>
    ///     Decodes the sample data of an image XObject stream (everything
    ///     <see cref="DecodeImageXObject"/> documents except <c>/SMask</c> handling), applying the
    ///     stream's <c>/Decode</c> array to the samples for every encoding.
    /// </summary>
    private DecodedImage DecodeImageSamples(PdfObject stream)
    {
        var width = RequireIntEntry(stream, "Width");
        var height = RequireIntEntry(stream, "Height");
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Image XObject /Width and /Height must be positive integers.");
        }

        if (width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            throw new InvalidDataException(
                $"Image XObject dimensions {width}x{height} exceed the maximum supported size of " +
                $"{Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        var (filterNames, filterParms) = ResolveFilterPipeline(stream);
        if (filterNames.Count == 1 && filterNames[0] == "DCTDecode")
        {
            var rawBytes = GetStreamRawBytes(stream);
            var channels = JpegCodec.GetInfo(new MemoryStream(rawBytes)).Channels;

            // JpegCodec only accepts 1-channel (gray) and 3-channel (YCbCr) JPEGs - a 4-channel
            // (CMYK/YCCK) JPEG already fails closed in GetInfo above - and both decode to exactly
            // their component samples, so the shared /Decode path applies exactly.
            var jpegDecodeRanges = ResolveDecodeArray(stream, channels, indexed: false);
            var jpegSurface = JpegCodec.Load(new MemoryStream(rawBytes));
            if (jpegDecodeRanges is not null)
            {
                ApplyDecodeToSurface(jpegSurface, channels, jpegDecodeRanges);
            }

            return new DecodedImage(jpegSurface, channels == 1);
        }

        if (filterNames.Contains("DCTDecode"))
        {
            throw new InvalidDataException("DCTDecode combined with another filter is not supported.");
        }

        if (filterNames.Count == 1 && filterNames[0] == "JPXDecode")
        {
            return DecodeJpxImageXObject(stream);
        }

        if (filterNames.Contains("JPXDecode"))
        {
            throw new InvalidDataException("JPXDecode combined with another filter is not supported.");
        }

        if (filterNames.Count == 1 && filterNames[0] == "CCITTFaxDecode")
        {
            return DecodeCcittFaxImageXObject(stream, filterParms[0], height);
        }

        if (filterNames.Contains("CCITTFaxDecode"))
        {
            throw new InvalidDataException("CCITTFaxDecode combined with another filter is not supported.");
        }

        var decoded = GetStreamDecodedBytes(stream);

        var colorSpaceObject = stream.Get("ColorSpace")
            ?? throw new InvalidDataException("Image XObject is missing required /ColorSpace.");
        var colorSpace = ResolveColorSpaceValue(Resolve(colorSpaceObject));

        var bitsPerComponent = RequireIntEntry(stream, "BitsPerComponent");
        if (bitsPerComponent is not (1 or 2 or 4 or 8 or 16))
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-image-bitdepth-{bitsPerComponent}",
                $"Image XObjects with {bitsPerComponent}-bit components are not supported; only 1, 2, 4, 8 and 16 are supported.");
        }

        var componentCount = ComponentCount(colorSpace);
        var indexed = colorSpace.Kind == PdfColorSpace.Family.Indexed;
        if (indexed && bitsPerComponent == 16)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-image-bitdepth-16",
                "An /Indexed image with 16-bit indices is not supported.");
        }

        // Normalize every depth to one byte per component (non-indexed samples scaled to 0-255,
        // /Indexed samples kept as raw palette indices) so /Decode and color conversion share one path.
        var samples = UnpackSamples(decoded, width, height, componentCount, bitsPerComponent, indexed);
        var decodeRanges = ResolveDecodeArray(stream, componentCount, indexed, bitsPerComponent);
        if (decodeRanges is not null)
        {
            if (bitsPerComponent == 8)
            {
                // UnpackSamples returns the (possibly cached) decoded stream bytes for 8-bit data;
                // every other depth already produced a private buffer that can be modified in place.
                samples = (byte[])samples.Clone();
            }

            ApplyDecodeArray(samples, componentCount, decodeRanges, indexed, (1 << bitsPerComponent) - 1);
        }

        var surface = new Surface(width, height);
        var rowBytes = componentCount * width;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * rowBytes;
            for (var x = 0; x < width; x++)
            {
                var pixelOffset = rowOffset + (x * componentCount);
                surface[x, y] = SamplesToColor(colorSpace, samples, pixelOffset, componentCount);
            }
        }

        return new DecodedImage(surface, componentCount == 1 && !indexed);
    }

    /// <summary>The decoder limits for JPX images: the renderer's maximum image dimensions.</summary>
    private static readonly Jpeg2000DecoderLimits JpxDecoderLimits = new()
    {
        MaxWidth = Surface.MaxDimension,
        MaxHeight = Surface.MaxDimension,
    };

    /// <summary>
    ///     Decodes a bare <c>/Filter /JPXDecode</c> image XObject (JPEG 2000) via
    ///     <see cref="Jpeg2000Codec.Decode(byte[])"/>, trusting the decoded dimensions over
    ///     <c>/Width</c>/<c>/Height</c> like <c>DCTDecode</c>.
    /// </summary>
    /// <remarks>
    ///     When <c>/ColorSpace</c> is absent the JPEG 2000 data's own color space (gray, sRGB/sYCC,
    ///     CMYK, or by channel count) is used; when present it overrides it (device spaces,
    ///     <c>/ICCBased</c> by component count, and <c>/Indexed</c>, where the sample is the raw
    ///     palette index - which requires 8-bit index data, since samples are scaled to 8 bits)
    ///     and its component count must equal the decoded color channel count. An overriding
    ///     <c>/ColorSpace</c> is rejected when the JP2 data has its own palette (the decoder has
    ///     already expanded it), and an <c>/Indexed</c> one when the samples are not 8-bit. An optional
    ///     <c>/Decode</c> array maps each 8-bit sample linearly. <c>/SMaskInData</c> <c>1</c> uses
    ///     the codestream's opacity channel as alpha, <c>2</c> additionally un-premultiplies the
    ///     component samples (before <c>/Decode</c> and the color conversion; rejected for
    ///     <c>/Indexed</c>); both are ignored when the image has an explicit <c>/SMask</c>. The decode is
    ///     bounded by the dictionary's <c>/Width</c>/<c>/Height</c> through
    ///     <see cref="Jpeg2000DecoderLimits"/>, so an oversized codestream is rejected before allocation.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown for malformed JPEG 2000 data, an invalid <c>/Decode</c> or <c>/SMaskInData</c>,
    ///     or a <c>/ColorSpace</c> whose component count disagrees with the decoded data.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown for JPEG 2000 features the decoder does not support, an unsupported <c>/ColorSpace</c>,
    ///     a <c>/ColorSpace</c> override on palette-mapped JP2 data, or an <c>/Indexed</c> space over non-8-bit samples.
    /// </exception>
    private DecodedImage DecodeJpxImageXObject(PdfObject stream)
    {
        var smaskInData = GetIntEntry(stream, "SMaskInData", 0);
        if (smaskInData is < 0 or > 2)
        {
            throw new InvalidDataException("Image XObject /SMaskInData must be 0, 1, or 2.");
        }

        // Bound the decode explicitly by the renderer's own maximum image size: the codec checks
        // the limits while parsing the header, before allocating any plane. The PDF /Width and
        // /Height are deliberately not used as the limit - like DCTDecode, the decoder's own
        // dimensions are trusted over a mismatching dictionary.
        var jp2 = Jpeg2000Codec.Decode(GetStreamRawBytes(stream), JpxDecoderLimits);

        var colorSpaceObject = stream.Get("ColorSpace");
        PdfColorSpace colorSpace;
        if (colorSpaceObject is null)
        {
            colorSpace = jp2.ColorSpace switch
            {
                Jpeg2000ColorSpace.Gray => PdfColorSpace.DeviceGray,
                Jpeg2000ColorSpace.Srgb => PdfColorSpace.DeviceRGB,
                Jpeg2000ColorSpace.Cmyk => PdfColorSpace.DeviceCMYK,
                _ => jp2.ColorChannelCount switch
                {
                    1 => PdfColorSpace.DeviceGray,
                    3 => PdfColorSpace.DeviceRGB,
                    4 => PdfColorSpace.DeviceCMYK,
                    _ => throw new UnsupportedImageFeatureException(
                        "pdf-jpx-colorspace",
                        $"JPXDecode images with {jp2.ColorChannelCount} color channels need an explicit /ColorSpace."),
                },
            };
        }
        else
        {
            colorSpace = ResolveColorSpaceValue(Resolve(colorSpaceObject));
            if (colorSpace.Kind == PdfColorSpace.Family.Pattern)
            {
                throw new InvalidDataException("Image XObject /ColorSpace must not be /Pattern.");
            }
        }

        if (colorSpaceObject is not null && jp2.HasPalette)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-jpx-palette-colorspace",
                "JPXDecode images whose JP2 data has a palette cannot be combined with a PDF /ColorSpace override.");
        }

        if (colorSpace.Kind == PdfColorSpace.Family.Indexed && jp2.BitDepth != 8)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-jpx-indexed-bit-depth",
                $"JPXDecode images with an /Indexed /ColorSpace require 8-bit samples, not {jp2.BitDepth}-bit.");
        }

        var componentCount = ComponentCount(colorSpace);
        if (componentCount != jp2.ColorChannelCount)
        {
            throw new InvalidDataException(
                $"JPXDecode image has {jp2.ColorChannelCount} color channels but /ColorSpace has {componentCount} components.");
        }

        var indexed = colorSpace.Kind == PdfColorSpace.Family.Indexed;
        var samples = jp2.ColorSamples;
        var useAlpha = smaskInData != 0 && jp2.AlphaSamples is not null && stream.Get("SMask") is null;
        if (useAlpha && smaskInData == 2)
        {
            // /SMaskInData 2 means the color samples are premultiplied by opacity: undo that on
            // the component samples, before /Decode and the color-space conversion, because the
            // conversion (notably CMYK -> RGB) is not linear in the premultiplied values.
            if (indexed)
            {
                throw new UnsupportedImageFeatureException(
                    "pdf-jpx-smaskindata-indexed",
                    "JPXDecode images with an /Indexed /ColorSpace cannot use /SMaskInData 2 (premultiplied indices).");
            }

            samples = UnpremultiplySamples(samples, jp2.AlphaSamples!, componentCount);
        }

        var decodeRanges = ResolveDecodeArray(stream, componentCount, indexed);
        if (decodeRanges is not null)
        {
            // samples is either the codec result (owned by this call) or an unpremultiplied copy.
            ApplyDecodeArray(samples, componentCount, decodeRanges, indexed);
        }

        var surface = new Surface(jp2.Width, jp2.Height);
        var pixelCount = jp2.Width * jp2.Height;
        for (var i = 0; i < pixelCount; i++)
        {
            var color = SamplesToColor(colorSpace, samples, i * componentCount, componentCount);
            if (useAlpha)
            {
                color.A = jp2.AlphaSamples![i];
            }

            surface[i % jp2.Width, i / jp2.Width] = color;
        }

        return new DecodedImage(surface, componentCount == 1 && !indexed);
    }

    /// <summary>
    ///     Divides each interleaved 8-bit component sample by its pixel's opacity
    ///     (<c>v' = min(255, v * 255 / alpha)</c>), undoing premultiplication; fully transparent
    ///     pixels (alpha <c>0</c>) are left unchanged.
    /// </summary>
    private static byte[] UnpremultiplySamples(byte[] samples, byte[] alpha, int componentCount)
    {
        var result = new byte[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            var a = alpha[i / componentCount];
            result[i] = a == 0 ? samples[i] : (byte)Math.Min(255, ((samples[i] * 255) + (a / 2)) / a);
        }

        return result;
    }

    /// <summary>
    ///     Resolves and validates an image <c>/Decode</c> array into <c>2 x components</c> numbers
    ///     (<c>Dmin</c>, <c>Dmax</c> per component), shared by every image encoding.
    /// </summary>
    /// <returns>
    ///     The ranges, or <see langword="null"/> when <c>/Decode</c> is absent or is the identity
    ///     mapping (<c>[0 1]</c> per component, or <c>[0 255]</c> for an <c>/Indexed</c> space).
    /// </returns>
    /// <exception cref="InvalidDataException">Thrown when the array is not <c>2 x components</c> numbers.</exception>
    private double[]? ResolveDecodeArray(PdfObject stream, int componentCount, bool indexed, int bitsPerComponent = 8)
    {
        var entry = stream.Get("Decode");
        if (entry is null)
        {
            return null;
        }

        var decode = Resolve(entry);
        if (decode.Kind != PdfKind.Array || decode.Items.Count != 2 * componentCount)
        {
            throw new InvalidDataException($"Image XObject /Decode must be an array of {2 * componentCount} numbers.");
        }

        var ranges = new double[2 * componentCount];
        for (var i = 0; i < ranges.Length; i++)
        {
            var item = Resolve(decode.Items[i]);
            if (item.Kind != PdfKind.Number)
            {
                throw new InvalidDataException("Image XObject /Decode must contain only numbers.");
            }

            ranges[i] = item.Number;
        }

        var identityMax = indexed ? (double)((1 << bitsPerComponent) - 1) : 1.0;
        var identity = true;
        for (var c = 0; c < componentCount; c++)
        {
            identity &= Math.Abs(ranges[2 * c]) < 1e-9 && Math.Abs(ranges[(2 * c) + 1] - identityMax) < 1e-9;
        }

        return identity ? null : ranges;
    }

    /// <summary>
    ///     Applies resolved <c>/Decode</c> ranges, in place, to interleaved 8-bit samples:
    ///     <c>v' = Dmin + (v / 255) * (Dmax - Dmin)</c> per component, re-quantized to 8 bits
    ///     (for a non-<c>/Indexed</c> space the decode range is <c>[0, 1]</c>-normalized; for
    ///     <c>/Indexed</c> it is in palette-index units).
    /// </summary>
    private static void ApplyDecodeArray(
        byte[] samples, int componentCount, double[] ranges, bool indexed, int indexedMaxSample = 255)
    {
        var maxSample = indexed ? indexedMaxSample : 255;
        for (var i = 0; i < samples.Length; i++)
        {
            var component = i % componentCount;
            var min = ranges[2 * component];
            var max = ranges[(2 * component) + 1];
            var value = min + (samples[i] / (double)maxSample * (max - min));
            var scaled = indexed ? value : value * 255.0;
            samples[i] = (byte)Math.Clamp((int)Math.Round(scaled), 0, 255);
        }
    }

    /// <summary>
    ///     Applies resolved <c>/Decode</c> ranges, in place, to the component samples of a decoded
    ///     1-channel (gray) or 3-channel (RGB) <see cref="Surface"/> (a JPEG whose decoder output is
    ///     exactly its component samples).
    /// </summary>
    private static void ApplyDecodeToSurface(Surface surface, int channels, double[] ranges)
    {
        var samples = new byte[surface.Width * surface.Height * channels];
        var index = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var pixel = surface[x, y];
                samples[index++] = pixel.R;
                if (channels == 3)
                {
                    samples[index++] = pixel.G;
                    samples[index++] = pixel.B;
                }
            }
        }

        ApplyDecodeArray(samples, channels, ranges, indexed: false);
        index = 0;
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var r = samples[index++];
                if (channels == 3)
                {
                    var g = samples[index++];
                    var b = samples[index++];
                    surface[x, y] = new Rgba32(r, g, b, 255);
                }
                else
                {
                    surface[x, y] = new Rgba32(r, r, r, 255);
                }
            }
        }
    }

    /// <summary>
    ///     Unpacks raw image sample data of any supported <c>/BitsPerComponent</c> (1, 2, 4, 8 or
    ///     16; rows are padded to whole bytes) into one byte per component. Non-<c>/Indexed</c>
    ///     samples are scaled to the 0-255 range (16-bit samples keep their high byte);
    ///     <c>/Indexed</c> samples stay raw palette indices.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="data"/> is shorter than the image needs.</exception>
    private static byte[] UnpackSamples(
        byte[] data, int width, int height, int componentCount, int bitsPerComponent, bool indexed)
    {
        var perRow = (long)width * componentCount;
        var rowBytes = ((perRow * bitsPerComponent) + 7) / 8;
        if (data.Length < rowBytes * height)
        {
            throw new InvalidDataException("Image XObject sample data is truncated.");
        }

        if (bitsPerComponent == 8)
        {
            return data;
        }

        var maxValue = (1 << bitsPerComponent) - 1;
        var result = new byte[perRow * height];
        var o = 0;
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * rowBytes;
            for (var i = 0L; i < perRow; i++)
            {
                if (bitsPerComponent == 16)
                {
                    result[o++] = data[rowStart + (2 * i)];
                    continue;
                }

                var bitOffset = i * bitsPerComponent;
                var shift = 8 - bitsPerComponent - (int)(bitOffset & 7);
                var value = (data[rowStart + (bitOffset >> 3)] >> shift) & maxValue;
                result[o++] = (byte)(indexed ? value : value * 255 / maxValue);
            }
        }

        return result;
    }

    /// <summary>
    ///     Decodes a bare <c>/Filter /CCITTFaxDecode</c> image XObject (<see cref="DecodeImageXObject"/>'s
    ///     own dedicated branch): resolves <c>/K</c>/<c>/Columns</c>/<c>/Rows</c>/<c>/BlackIs1</c>/
    ///     <c>/EncodedByteAlign</c>/<c>/EndOfLine</c> from <paramref name="parm"/> (the pipeline's
    ///     sole, possibly-<see langword="null"/> <c>/DecodeParms</c> entry), defaulting
    ///     <c>/Rows</c> to <paramref name="imageHeight"/> when absent or <c>0</c>, then decodes via
    ///     <see cref="DecodeCcittFax"/> and composites the result through the shared
    ///     <c>/ColorSpace</c> pipeline (<see cref="SamplesToColor"/>), using <c>/Columns</c>/the
    ///     resolved <c>/Rows</c> - not <c>/Width</c>/<c>/Height</c> - as the authoritative surface
    ///     dimensions.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the resolved <c>/Columns</c> or <c>/Rows</c> is zero, negative, or exceeds
    ///     <see cref="Surface.MaxDimension"/>, or propagated from <see cref="DecodeCcittFax"/> for
    ///     a malformed/truncated CCITT bit stream.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the resolved <c>/ColorSpace</c> has more than 1 component, or propagated
    ///     from <see cref="DecodeCcittFax"/> when <c>/K</c> is <c>0</c> or greater or
    ///     <c>/EndOfLine</c> is <see langword="true"/>.
    /// </exception>
    private DecodedImage DecodeCcittFaxImageXObject(PdfObject stream, PdfObject? parm, int imageHeight)
    {
        var rawBytes = GetStreamRawBytes(stream);
        var k = parm is null ? 0 : GetIntEntry(parm, "K", 0);
        var columns = parm is null ? 1728 : GetIntEntry(parm, "Columns", 1728);
        var rowsParam = parm is null ? 0 : GetIntEntry(parm, "Rows", 0);
        var rows = rowsParam > 0 ? rowsParam : imageHeight;
        var blackIs1 = parm is not null && GetBoolEntry(parm, "BlackIs1", false);
        var encodedByteAlign = parm is not null && GetBoolEntry(parm, "EncodedByteAlign", false);
        var endOfLine = parm is not null && GetBoolEntry(parm, "EndOfLine", false);

        // Reject oversized, zero, or negative /Columns or /Rows before decoding - DecodeCcittFax
        // allocates its output buffer (rowBytes * rows) sized directly by these attacker-controlled
        // values, so the bounds-check must run before that call (not merely before the later
        // `new Surface(...)` call) to prevent a huge, zero-sized, or negative-sized internal
        // allocation from ever happening for a crafted image dictionary.
        if (columns <= 0 || rows <= 0 || columns > Surface.MaxDimension || rows > Surface.MaxDimension)
        {
            throw new InvalidDataException(
                $"CCITTFaxDecode image dimensions {columns}x{rows} are invalid or exceed the maximum supported " +
                $"size of {Surface.MaxDimension}x{Surface.MaxDimension}.");
        }

        var decoded = DecodeCcittFax(rawBytes, k, columns, rows, blackIs1, encodedByteAlign, endOfLine);

        var colorSpaceObject = stream.Get("ColorSpace");
        var colorSpace = colorSpaceObject is null
            ? PdfColorSpace.DeviceGray
            : ResolveColorSpaceValue(Resolve(colorSpaceObject));

        var componentCount = ComponentCount(colorSpace);
        if (componentCount != 1)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-ccittfax-colorspace",
                "CCITTFaxDecode images require a single-component /ColorSpace.");
        }

        var indexed = colorSpace.Kind == PdfColorSpace.Family.Indexed;
        var decodeRanges = ResolveDecodeArray(stream, 1, indexed);
        if (decodeRanges is not null)
        {
            ApplyDecodeArray(decoded, 1, decodeRanges, indexed);
        }

        var surface = new Surface(columns, rows);
        for (var y = 0; y < rows; y++)
        {
            var rowOffset = y * columns;
            for (var x = 0; x < columns; x++)
            {
                surface[x, y] = SamplesToColor(colorSpace, decoded, rowOffset + x, 1);
            }
        }

        return new DecodedImage(surface, !indexed);
    }

    /// <summary>
    ///     Converts <paramref name="count"/> consecutive raw 8-bit samples (each in <c>[0, 255]</c>)
    ///     starting at <paramref name="offset"/> into an opaque <see cref="Rgba32"/> color, reusing
    ///     <see cref="ColorFromComponents"/> so image decoding and the <c>rg</c>/<c>k</c>/<c>sc</c>
    ///     color operators share exactly the same color-space conversion formulas.
    /// </summary>
    private static Rgba32 SamplesToColor(PdfColorSpace colorSpace, byte[] decoded, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > decoded.Length)
        {
            throw new InvalidDataException("Image XObject sample data is truncated.");
        }

        if (colorSpace.Kind == PdfColorSpace.Family.Indexed)
        {
            // An /Indexed sample is a raw palette index, not a [0, 1]-normalized component - do
            // not divide by 255.0 like every other color space below.
            return ColorFromComponents(colorSpace, [(double)decoded[offset]]);
        }

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
    ///     user-space's y-up convention): <c>row = floor((1 - v) * image.Height)</c>. Each sampled
    ///     source pixel is alpha-blended "over" the existing destination pixel via
    ///     <see cref="Rgba32.CompositeOver"/> (standard Porter-Duff "over" compositing) rather
    ///     than overwriting it outright, so a source pixel with a non-opaque (including fully
    ///     transparent) alpha channel lets the existing destination content show through
    ///     correctly instead of being replaced by whatever RGB value happens to be stored
    ///     alongside that transparent alpha. When the current graphics state has an active
    ///     <see cref="GraphicsState.Clip"/> (PDF 32000-1 &#xA7;8.5.4), each sampled source pixel's
    ///     own alpha is first multiplied by that pixel's clip coverage - restricting image
    ///     painting to the current clipping path exactly like every <see cref="Drawing.PathFiller"/>
    ///     fill/stroke call site already does, since <c>Do</c> (<see cref="OpDrawXObject"/>)
    ///     bypasses <see cref="Drawing.PathFiller"/> entirely and must therefore enforce the clip
    ///     directly here instead. The graphics state itself is read via a null-conditional access
    ///     (<c>_gs?.Clip</c>, treating an absent graphics state identically to "no clip active")
    ///     purely so <c>PdfDocumentImageCompositingTests</c>'s reflection-based direct invocation
    ///     against an otherwise-uninitialized <see cref="PdfDocument"/> instance (which never runs
    ///     <see cref="ExecuteContentStream"/>, and so never assigns <c>_gs</c>) keeps working -
    ///     every real content-stream-driven call always has a non-null <c>_gs</c> by this point.
    /// </remarks>
    private void CompositeImageOntoSurface(Surface image, Matrix3x2 ctm)
    {
        if (!Matrix3x2.Invert(ctm, out var inverse))
        {
            return;
        }

        var clip = _gs?.Clip;
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

                var clipCoverage = clip?.GetCoverage(x, y) ?? 1f;
                if (clipCoverage <= 0f)
                {
                    continue;
                }

                var column = Math.Clamp((int)Math.Floor(u * image.Width), 0, image.Width - 1);
                var row = Math.Clamp((int)Math.Floor((1 - v) * image.Height), 0, image.Height - 1);
                var sourcePixel = image[column, row];
                if (clipCoverage < 1f)
                {
                    // Round the same way Surface's own compositing pipeline does (AwayFromZero,
                    // not the default ToEven), so a clipped image's edge alpha never differs by
                    // one level from a clipped vector fill's at an exact n + 0.5 midpoint.
                    sourcePixel = new Rgba32(
                        sourcePixel.R, sourcePixel.G, sourcePixel.B,
                        (byte)Math.Round(sourcePixel.A * clipCoverage, MidpointRounding.AwayFromZero));
                }

                _surface[x, y] = Rgba32.CompositeOver(_surface[x, y], sourcePixel);
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

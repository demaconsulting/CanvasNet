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
    ///         <see cref="_resources"/>, graphics state, and graphics-state stack are restored
    ///         exactly as they were before this method ran - mutations made inside the Form (CTM,
    ///         colors, font selection, etc.) never leak back out, matching an implicit <c>q</c>
    ///         ... <c>Q</c> bracketing. Path-construction state (<see cref="_pathBuilder"/>,
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
        _formNestingDepth++;
        try
        {
            _resources = resolvedFormResources;
            var nestedGs = savedGs.Clone();
            nestedGs.CurrentTransform = formMatrix * savedGs.CurrentTransform;
            _gs = nestedGs;
            _gsStack = new Stack<GraphicsState>();
            ExecuteOperators(contentBytes);
        }
        finally
        {
            _formNestingDepth--;
            _resources = savedResources;
            _gs = savedGs;
            _gsStack = savedGsStack;
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
    ///     Decodes an image XObject stream into a fully opaque <see cref="Surface"/> of its
    ///     declared <c>/Width</c> x <c>/Height</c>.
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
    ///     (<c>8</c> only). <c>/SMask</c>/<c>/Mask</c> are never consulted - every decoded image
    ///     is treated as fully opaque, a documented Phase 3 limitation.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Width</c>/<c>/Height</c> is missing, not a number, or not positive,
    ///     when <c>/ColorSpace</c> is missing (for a non-<c>CCITTFaxDecode</c> image), when
    ///     <c>DCTDecode</c> is combined with any other filter, when <c>CCITTFaxDecode</c> is
    ///     combined with any other filter, or propagated from <see cref="DecodeCcittFax"/> for a
    ///     malformed/truncated CCITT bit stream.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/ColorSpace</c> names an unsupported color space, when
    ///     <c>/BitsPerComponent</c> is not <c>8</c> (for a non-<c>DCTDecode</c>/non-
    ///     <c>CCITTFaxDecode</c> image), when a <c>CCITTFaxDecode</c> image's resolved
    ///     <c>/ColorSpace</c> has more than 1 component, or propagated from
    ///     <see cref="DecodeCcittFax"/> when <c>/DecodeParms /K</c> is <c>0</c> or greater
    ///     (Group 3) or <c>/EndOfLine</c> is <see langword="true"/>.
    /// </exception>
    private Surface DecodeImageXObject(PdfObject stream)
    {
        var width = RequireIntEntry(stream, "Width");
        var height = RequireIntEntry(stream, "Height");
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("Image XObject /Width and /Height must be positive integers.");
        }

        var (filterNames, filterParms) = ResolveFilterPipeline(stream);
        if (filterNames.Count == 1 && filterNames[0] == "DCTDecode")
        {
            var rawBytes = GetStreamRawBytes(stream);
            return JpegCodec.Load(new MemoryStream(rawBytes));
        }

        if (filterNames.Contains("DCTDecode"))
        {
            throw new InvalidDataException("DCTDecode combined with another filter is not supported.");
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
        if (bitsPerComponent != 8)
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-image-bitdepth-{bitsPerComponent}",
                $"Image XObjects with {bitsPerComponent}-bit components are not supported; only 8 is supported.");
        }

        var componentCount = ComponentCount(colorSpace);
        var surface = new Surface(width, height);
        var rowBytes = componentCount * width;
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * rowBytes;
            for (var x = 0; x < width; x++)
            {
                var pixelOffset = rowOffset + (x * componentCount);
                surface[x, y] = SamplesToColor(colorSpace, decoded, pixelOffset, componentCount);
            }
        }

        return surface;
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
    ///     Propagated from <see cref="DecodeCcittFax"/> for a malformed/truncated CCITT bit
    ///     stream.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the resolved <c>/ColorSpace</c> has more than 1 component, or propagated
    ///     from <see cref="DecodeCcittFax"/> when <c>/K</c> is <c>0</c> or greater or
    ///     <c>/EndOfLine</c> is <see langword="true"/>.
    /// </exception>
    private Surface DecodeCcittFaxImageXObject(PdfObject stream, PdfObject? parm, int imageHeight)
    {
        var rawBytes = GetStreamRawBytes(stream);
        var k = parm is null ? 0 : GetIntEntry(parm, "K", 0);
        var columns = parm is null ? 1728 : GetIntEntry(parm, "Columns", 1728);
        var rowsParam = parm is null ? 0 : GetIntEntry(parm, "Rows", 0);
        var rows = rowsParam > 0 ? rowsParam : imageHeight;
        var blackIs1 = parm is not null && GetBoolEntry(parm, "BlackIs1", false);
        var encodedByteAlign = parm is not null && GetBoolEntry(parm, "EncodedByteAlign", false);
        var endOfLine = parm is not null && GetBoolEntry(parm, "EndOfLine", false);

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

        var surface = new Surface(columns, rows);
        for (var y = 0; y < rows; y++)
        {
            var rowOffset = y * columns;
            for (var x = 0; x < columns; x++)
            {
                surface[x, y] = SamplesToColor(colorSpace, decoded, rowOffset + x, 1);
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
    private static Rgba32 SamplesToColor(PdfColorSpace colorSpace, byte[] decoded, int offset, int count)
    {
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

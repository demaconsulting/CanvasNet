// cspell:ignore bitdepth reimplementation diffability
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     Resolves a stream's <c>/Filter</c> (absent, a single <c>Name</c>, or an <c>Array</c> of
    ///     names) and its parallel <c>/DecodeParms</c> (absent, a single dictionary/<see langword="null"/>,
    ///     or an array of dictionaries/<see langword="null"/>, aligned 1:1 with <c>/Filter</c>)
    ///     into an ordered pipeline description.
    /// </summary>
    /// <param name="streamObject">The stream whose <c>/Filter</c>/<c>/DecodeParms</c> to resolve.</param>
    /// <returns>
    ///     The ordered filter names (empty when <c>/Filter</c> is absent) and their parallel,
    ///     possibly-<see langword="null"/> <c>/DecodeParms</c> dictionaries, one per filter name.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Filter</c> is neither a name nor an array of names, or when
    ///     <c>/DecodeParms</c> does not match <c>/Filter</c>'s own shape (a single dictionary/
    ///     <see langword="null"/> for a bare name, an array of the same length as <c>/Filter</c>
    ///     for an array).
    /// </exception>
    private (IReadOnlyList<string> Names, IReadOnlyList<PdfObject?> Parms) ResolveFilterPipeline(PdfObject streamObject)
    {
        var filterObject = streamObject.Get("Filter");
        if (filterObject is null)
        {
            return ([], []);
        }

        var resolvedFilter = Resolve(filterObject);
        List<string> names;
        if (resolvedFilter.Kind == PdfKind.Name)
        {
            names = [resolvedFilter.Text];
        }
        else if (resolvedFilter.Kind == PdfKind.Array)
        {
            names = new List<string>(resolvedFilter.Items.Count);
            foreach (var item in resolvedFilter.Items)
            {
                var resolvedItem = Resolve(item);
                if (resolvedItem.Kind != PdfKind.Name)
                {
                    throw new InvalidDataException("/Filter array entries must be names.");
                }

                names.Add(resolvedItem.Text);
            }
        }
        else
        {
            throw new InvalidDataException("Unsupported /Filter value.");
        }

        var parms = ResolveDecodeParms(streamObject, resolvedFilter, names.Count);
        return (names, parms);
    }

    /// <summary>Resolves <c>/DecodeParms</c> into a list of length <paramref name="filterCount"/>, aligned 1:1 with <c>/Filter</c>.</summary>
    private List<PdfObject?> ResolveDecodeParms(PdfObject streamObject, PdfObject resolvedFilter, int filterCount)
    {
        var parmsObject = streamObject.Get("DecodeParms");
        if (parmsObject is null)
        {
            return new List<PdfObject?>(new PdfObject?[filterCount]);
        }

        var resolvedParms = Resolve(parmsObject);
        if (resolvedFilter.Kind == PdfKind.Name)
        {
            return [resolvedParms.Kind == PdfKind.Null ? null : resolvedParms];
        }

        if (resolvedParms.Kind != PdfKind.Array)
        {
            throw new InvalidDataException("/DecodeParms must be an array when /Filter is an array.");
        }

        if (resolvedParms.Items.Count != filterCount)
        {
            throw new InvalidDataException("/DecodeParms array length must match /Filter array length.");
        }

        var parms = new List<PdfObject?>(filterCount);
        foreach (var item in resolvedParms.Items)
        {
            var resolvedItem = Resolve(item);
            parms.Add(resolvedItem.Kind == PdfKind.Null ? null : resolvedItem);
        }

        return parms;
    }

    /// <summary>
    ///     Returns a stream's fully decoded data bytes, applying its ordered <c>/Filter</c>/
    ///     <c>/DecodeParms</c> pipeline.
    /// </summary>
    /// <remarks>
    ///     Supports <c>FlateDecode</c> and <c>LZWDecode</c> (either optionally followed by a
    ///     PNG - predictor values <c>10</c>-<c>15</c> - or TIFF - predictor value <c>2</c> -
    ///     predictor reversal), plus <c>ASCII85Decode</c>, <c>ASCIIHexDecode</c>, and
    ///     <c>RunLengthDecode</c> (none of which carry a predictor: PDF only ever declares
    ///     <c>/Predictor</c> alongside the two image-compression filters). Any other filter name
    ///     (including <c>DCTDecode</c> and <c>CCITTFaxDecode</c>, both of which
    ///     <c>PdfDocument.Images.cs</c> always detects and bypasses - decoding each directly via
    ///     <c>Codecs.JpegCodec</c>/<see cref="DecodeCcittFax"/> respectively - before ever calling
    ///     this method; and <c>JPXDecode</c>, which remains unsupported) is rejected with
    ///     <see cref="UnsupportedImageFeatureException"/>. This is the same behavior Phase 1/2
    ///     already relied on for cross-reference streams, object streams, and page
    ///     <c>/Contents</c> (all of which only ever use a bare <c>FlateDecode</c> filter with no
    ///     <c>/DecodeParms</c>), generalized to a full ordered pipeline.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Filter</c>/<c>/DecodeParms</c> is malformed, when a declared
    ///     <c>/Predictor</c> value is not <c>1</c>, <c>2</c>, or in <c>10..15</c>, or when a
    ///     filtered stream's own bytes are malformed for its declared filter (see the individual
    ///     <c>Decode*</c> methods).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when a filter name other than <c>FlateDecode</c>, <c>LZWDecode</c>,
    ///     <c>ASCII85Decode</c>, <c>ASCIIHexDecode</c>, or <c>RunLengthDecode</c> is declared, or
    ///     when a TIFF predictor (<c>/Predictor 2</c>) is combined with a
    ///     <c>/BitsPerComponent</c> other than <c>8</c>.
    /// </exception>
    private byte[] GetStreamDecodedBytes(PdfObject streamObject)
    {
        var raw = GetStreamRawBytes(streamObject);
        var (names, parms) = ResolveFilterPipeline(streamObject);

        var data = raw;
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            var parm = parms[i];

            data = name switch
            {
                "FlateDecode" => ZlibDecompress(data),
                "LZWDecode" => DecodeLzw(data, parm is null || GetIntEntry(parm, "EarlyChange", 1) != 0),
                "ASCII85Decode" => DecodeAscii85(data),
                "ASCIIHexDecode" => DecodeAsciiHex(data),
                "RunLengthDecode" => DecodeRunLength(data),
                _ => throw new UnsupportedImageFeatureException(
                    $"pdf-filter-{name}",
                    $"Stream filter '{name}' is not supported in this phase."),
            };

            if (name is "FlateDecode" or "LZWDecode" && parm is not null && GetIntEntry(parm, "Predictor", 1) > 1)
            {
                data = ApplyPredictor(data, parm);
            }
        }

        return data;
    }

    /// <summary>
    ///     The maximum per-row byte size <see cref="ApplyPredictor"/> allows its
    ///     <c>/Colors</c>/<c>/BitsPerComponent</c>/<c>/Columns</c>-derived row stride to reach,
    ///     computed in <see langword="long"/> arithmetic before this cap is enforced so an
    ///     attacker-controlled <c>/Columns</c> or <c>/Colors</c> cannot overflow the plain
    ///     <see langword="int"/> arithmetic <see cref="ApplyTiffPredictor"/>/
    ///     <see cref="ApplyPngPredictor"/> perform afterward. <c>int.MaxValue / 2</c> is far
    ///     larger than any legitimate PDF row could need while still leaving headroom for the
    ///     <c>+ 1</c> PNG filter-type-byte stride adjustment to stay within <see langword="int"/>
    ///     range, matching the buffer-size caps <c>8d4bc3f</c> established elsewhere in this file.
    /// </summary>
    private const long PredictorMaxRowBytes = int.MaxValue / 2;

    /// <summary>
    ///     Reverses a <c>/DecodeParms</c>-declared PNG (<c>/Predictor</c> <c>10</c>-<c>15</c>) or
    ///     TIFF (<c>/Predictor 2</c>) predictor against already-<c>FlateDecode</c>-decompressed
    ///     bytes.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Predictor</c> is not <c>2</c> or in <c>10..15</c>, when
    ///     <c>/Colors</c>, <c>/BitsPerComponent</c>, or <c>/Columns</c> are not legal PDF values
    ///     (defense-in-depth: a malformed/negative or zero value here could otherwise divide by
    ///     zero or underflow downstream row/stride computations), or when the resolved row size
    ///     exceeds <see cref="PredictorMaxRowBytes"/> (defense-in-depth: an adversarially large
    ///     <c>/Colors</c> or <c>/Columns</c> could otherwise overflow the downstream
    ///     <see langword="int"/> row/stride computations).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when a TIFF predictor (<c>/Predictor 2</c>) is declared with a
    ///     <c>/BitsPerComponent</c> other than <c>8</c>.
    /// </exception>
    private byte[] ApplyPredictor(byte[] data, PdfObject parms)
    {
        var predictor = GetIntEntry(parms, "Predictor", 1);
        var colors = GetIntEntry(parms, "Colors", 1);
        var bitsPerComponent = GetIntEntry(parms, "BitsPerComponent", 8);
        var columns = GetIntEntry(parms, "Columns", 1);

        if (colors < 1)
        {
            throw new InvalidDataException("Predictor /Colors must be a positive integer.");
        }

        if (bitsPerComponent is not (1 or 2 or 4 or 8 or 16))
        {
            throw new InvalidDataException("Predictor /BitsPerComponent must be 1, 2, 4, 8, or 16.");
        }

        if (columns < 1)
        {
            throw new InvalidDataException("Predictor /Columns must be a positive integer.");
        }

        // Compute the row stride in `long` arithmetic - `/Colors` and `/Columns` are each only
        // bounded below (positive) above, so their product with `/BitsPerComponent` could
        // otherwise overflow plain `int` arithmetic and wrap around to a small or negative
        // value, defeating the positivity checks above and corrupting the downstream
        // allocation/stride math in ApplyTiffPredictor/ApplyPngPredictor.
        var rowBytesLong = ((long)colors * bitsPerComponent * columns + 7) / 8;
        if (rowBytesLong > PredictorMaxRowBytes)
        {
            throw new InvalidDataException(
                $"Predictor row size of {rowBytesLong} bytes exceeds the maximum supported size of " +
                $"{PredictorMaxRowBytes} bytes.");
        }

        if (predictor == 2)
        {
            return ApplyTiffPredictor(data, colors, bitsPerComponent, columns);
        }

        if (predictor is >= 10 and <= 15)
        {
            return ApplyPngPredictor(data, colors, bitsPerComponent, columns);
        }

        throw new InvalidDataException($"Unsupported /Predictor value {predictor}.");
    }

    /// <summary>
    ///     Reverses a TIFF (<c>/Predictor 2</c>) horizontal-difference predictor: every component
    ///     byte (after the first pixel's own components) was encoded as its difference from the
    ///     same component's earlier byte in the same row - no per-row filter-type byte is present
    ///     (unlike the PNG predictor forms).
    /// </summary>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="bitsPerComponent"/> is not <c>8</c>.
    /// </exception>
    private static byte[] ApplyTiffPredictor(byte[] data, int colors, int bitsPerComponent, int columns)
    {
        if (bitsPerComponent != 8)
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-tiff-predictor-bitdepth-{bitsPerComponent}",
                $"TIFF predictor with {bitsPerComponent}-bit components is not supported.");
        }

        var bytesPerPixel = Math.Max(1, colors * bitsPerComponent / 8);
        var rowBytes = (colors * bitsPerComponent * columns + 7) / 8;
        var result = (byte[])data.Clone();
        var rowCount = rowBytes == 0 ? 0 : result.Length / rowBytes;
        for (var row = 0; row < rowCount; row++)
        {
            var rowStart = row * rowBytes;
            for (var i = bytesPerPixel; i < rowBytes; i++)
            {
                result[rowStart + i] = (byte)(result[rowStart + i] + result[rowStart + i - bytesPerPixel]);
            }
        }

        return result;
    }

    /// <summary>
    ///     Reverses a PNG (<c>/Predictor</c> <c>10</c>-<c>15</c>) predictor: every row is prefixed
    ///     with its own filter-type byte (<c>0</c>-<c>4</c>), reconstructed via
    ///     <see cref="DefilterRow"/> - an independent reimplementation of
    ///     <c>Codecs/Png/PngCodec.Filtering.cs</c>'s exact algorithm (not reusable across
    ///     assemblies: those methods are <see langword="private"/>), named identically for direct
    ///     diffability against the original.
    /// </summary>
    private static byte[] ApplyPngPredictor(byte[] data, int colors, int bitsPerComponent, int columns)
    {
        var bpp = Math.Max(1, colors * bitsPerComponent / 8);
        var rowBytes = (colors * bitsPerComponent * columns + 7) / 8;
        var stride = rowBytes + 1;
        var rowCount = stride == 0 ? 0 : data.Length / stride;
        var result = new byte[rowCount * rowBytes];
        var previousRow = new byte[rowBytes];

        for (var row = 0; row < rowCount; row++)
        {
            var srcOffset = row * stride;
            var filterType = data[srcOffset];
            var filteredRow = data.AsSpan(srcOffset + 1, rowBytes);
            var outputRow = result.AsSpan(row * rowBytes, rowBytes);
            DefilterRow(filterType, filteredRow, previousRow, outputRow, bpp);
            outputRow.CopyTo(previousRow);
        }

        return result;
    }

    /// <summary>
    ///     Reconstructs one filtered PNG scanline into its raw (unfiltered) pixel bytes. See
    ///     <c>Codecs/Png/PngCodec.Filtering.cs</c>'s identically named/shaped method - this is an
    ///     independent reimplementation of the same algorithm.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="filterType"/> is not a value from 0 to 4.
    /// </exception>
    private static void DefilterRow(
        byte filterType,
        ReadOnlySpan<byte> filtered,
        ReadOnlySpan<byte> previousRow,
        Span<byte> output,
        int bpp)
    {
        switch (filterType)
        {
            case 0: // None
                filtered.CopyTo(output);
                break;

            case 1: // Sub
                DefilterSub(filtered, output, bpp);
                break;

            case 2: // Up
                DefilterUp(filtered, previousRow, output);
                break;

            case 3: // Average
                DefilterAverage(filtered, previousRow, output, bpp);
                break;

            case 4: // Paeth
                DefilterPaeth(filtered, previousRow, output, bpp);
                break;

            default:
                throw new InvalidDataException(
                    $"Unsupported PNG filter type {filterType}; only filter types 0-4 are supported.");
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 1 (Sub).</summary>
    private static void DefilterSub(ReadOnlySpan<byte> filtered, Span<byte> output, int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            output[i] = (byte)(filtered[i] + left);
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 2 (Up).</summary>
    private static void DefilterUp(ReadOnlySpan<byte> filtered, ReadOnlySpan<byte> previousRow, Span<byte> output)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            output[i] = (byte)(filtered[i] + previousRow[i]);
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 3 (Average).</summary>
    private static void DefilterAverage(
        ReadOnlySpan<byte> filtered,
        ReadOnlySpan<byte> previousRow,
        Span<byte> output,
        int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            int up = previousRow[i];
            output[i] = (byte)(filtered[i] + ((left + up) / 2));
        }
    }

    /// <summary>Reconstructs a scanline filtered with PNG filter type 4 (Paeth).</summary>
    private static void DefilterPaeth(
        ReadOnlySpan<byte> filtered,
        ReadOnlySpan<byte> previousRow,
        Span<byte> output,
        int bpp)
    {
        for (var i = 0; i < filtered.Length; i++)
        {
            int left = i >= bpp ? output[i - bpp] : 0;
            int up = previousRow[i];
            int upperLeft = i >= bpp ? previousRow[i - bpp] : 0;
            output[i] = (byte)(filtered[i] + PaethPredictor(left, up, upperLeft));
        }
    }

    /// <summary>
    ///     Computes the PNG Paeth predictor value for a pixel from its left, upper, and
    ///     upper-left neighbor byte values.
    /// </summary>
    private static int PaethPredictor(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

    /// <summary>Reads an integer dictionary entry (resolving an indirect reference), or a default when absent.</summary>
    /// <exception cref="InvalidDataException">Thrown when the entry is present but not a number.</exception>
    private int GetIntEntry(PdfObject dictionary, string key, int defaultValue)
    {
        var entry = dictionary.Get(key);
        if (entry is null)
        {
            return defaultValue;
        }

        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Number)
        {
            throw new InvalidDataException($"/{key} must be a number.");
        }

        return (int)resolved.Number;
    }

    /// <summary>Reads a boolean dictionary entry (resolving an indirect reference), or a default when absent.</summary>
    /// <exception cref="InvalidDataException">Thrown when the entry is present but not a boolean.</exception>
    private bool GetBoolEntry(PdfObject dictionary, string key, bool defaultValue)
    {
        var entry = dictionary.Get(key);
        if (entry is null)
        {
            return defaultValue;
        }

        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Boolean)
        {
            throw new InvalidDataException($"/{key} must be a boolean.");
        }

        return resolved.Boolean;
    }
}

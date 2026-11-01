// cspell:ignore Lzw
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The maximum total number of decoded output bytes <see cref="DecodeLzw"/> allows before
    ///     failing closed, bounding a decompression-bomb-style crafted stream (a tiny compressed
    ///     input whose table entries chain into an enormous expanded output) from exhausting
    ///     memory. 64 MiB is generous for any legitimate PDF image/content stream.
    /// </summary>
    private const int LzwMaxOutputBytes = 64 * 1024 * 1024;

    /// <summary>
    ///     Decodes an <c>LZWDecode</c>-filtered stream (ISO 32000-1/2 section 7.4.4): an initial
    ///     258-entry table (codes 0-255 literal bytes, 256 Clear, 257 EOD, first dynamically
    ///     assigned code 258), MSB-first variable 9-12 bit code packing, and code-width growth
    ///     timing controlled by <paramref name="earlyChange"/>.
    /// </summary>
    /// <remarks>
    ///     The algorithm itself is the core assembly's internal, output-bounded
    ///     <c>LzwDecoder</c> (<c>Codecs/LzwDecoder.cs</c>), shared with <c>TiffCodec</c>'s
    ///     TIFF-flavor LZW (which is the same stream layout with the early-change timing fixed)
    ///     through the core assembly's <c>InternalsVisibleTo</c> grant to this package; this
    ///     method only supplies the PDF-specific <c>/EarlyChange</c> choice and output cap.
    /// </remarks>
    /// <param name="data">The raw (pre-<c>LZWDecode</c>) stream bytes.</param>
    /// <param name="earlyChange">
    ///     The resolved <c>/DecodeParms /EarlyChange</c> value: <see langword="true"/> (the PDF
    ///     default) grows the code width one code earlier than <see langword="false"/>.
    /// </param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the bit stream is truncated before an <c>EOD</c> code is reached, when a
    ///     decoded code is invalid (out of range for the current table state), or when the
    ///     decoded output exceeds <see cref="LzwMaxOutputBytes"/>.
    /// </exception>
    private static byte[] DecodeLzw(byte[] data, bool earlyChange) =>
        LzwDecoder.Decode(data, earlyChange, LzwMaxOutputBytes);
}

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>The RunLengthDecode length byte value that marks end-of-data.</summary>
    private const byte RunLengthEodMarker = 128;

    /// <summary>
    ///     The maximum total number of decoded output bytes <see cref="DecodeRunLength"/> allows
    ///     before failing closed, bounding a decompression-bomb-style crafted stream (each 2-byte
    ///     "repeat" run can expand to up to 128 output bytes, a 64x amplification) from
    ///     exhausting memory. 64 MiB is generous for any legitimate PDF image/content stream,
    ///     matching <c>LzwMaxOutputBytes</c> in <c>PdfDocument.Filters.Lzw.cs</c>.
    /// </summary>
    private const int RunLengthMaxOutputBytes = 64 * 1024 * 1024;

    /// <summary>
    ///     Decodes a <c>RunLengthDecode</c>-filtered stream (ISO 32000-1/2 section 7.4.5, the
    ///     "PackBits" scheme): each run begins with a length byte - <c>0</c>-<c>127</c> copies
    ///     the following <c>length + 1</c> bytes literally, <c>129</c>-<c>255</c> repeats the
    ///     single following byte <c>257 - length</c> times, and <c>128</c> marks EOD.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a literal or repeat run is truncated (not enough bytes remain), the stream
    ///     ends without an EOD (<c>128</c>) length byte, or the decoded output exceeds
    ///     <see cref="RunLengthMaxOutputBytes"/>.
    /// </exception>
    private static byte[] DecodeRunLength(byte[] data)
    {
        var output = new List<byte>();
        var foundEod = false;

        var i = 0;
        while (i < data.Length)
        {
            var length = data[i++];

            if (length == RunLengthEodMarker)
            {
                foundEod = true;
                break;
            }

            if (length <= 127)
            {
                var count = length + 1;
                if (i + count > data.Length)
                {
                    throw new InvalidDataException("Truncated RunLengthDecode literal run.");
                }

                for (var k = 0; k < count; k++)
                {
                    output.Add(data[i + k]);
                }

                i += count;
            }
            else
            {
                if (i >= data.Length)
                {
                    throw new InvalidDataException("Truncated RunLengthDecode repeat run.");
                }

                var repeatByte = data[i++];
                var count = 257 - length;
                for (var k = 0; k < count; k++)
                {
                    output.Add(repeatByte);
                }
            }

            // Check the running total after every run (not just once at the end) - bounding the
            // intermediate allocation itself, not merely the final returned array, matching the
            // DecodeLzw/ZlibDecompress output-cap convention elsewhere in this class.
            if (output.Count > RunLengthMaxOutputBytes)
            {
                throw new InvalidDataException(
                    $"RunLengthDecode output exceeds the maximum supported size of {RunLengthMaxOutputBytes} bytes.");
            }
        }

        if (!foundEod)
        {
            throw new InvalidDataException("RunLengthDecode stream is missing its EOD length byte (128).");
        }

        return [.. output];
    }
}

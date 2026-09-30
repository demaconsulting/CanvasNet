namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>The RunLengthDecode length byte value that marks end-of-data.</summary>
    private const byte RunLengthEodMarker = 128;

    /// <summary>
    ///     Decodes a <c>RunLengthDecode</c>-filtered stream (ISO 32000-1/2 section 7.4.5, the
    ///     "PackBits" scheme): each run begins with a length byte - <c>0</c>-<c>127</c> copies
    ///     the following <c>length + 1</c> bytes literally, <c>129</c>-<c>255</c> repeats the
    ///     single following byte <c>257 - length</c> times, and <c>128</c> marks EOD.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a literal or repeat run is truncated (not enough bytes remain), or the
    ///     stream ends without an EOD (<c>128</c>) length byte.
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
        }

        if (!foundEod)
        {
            throw new InvalidDataException("RunLengthDecode stream is missing its EOD length byte (128).");
        }

        return [.. output];
    }
}

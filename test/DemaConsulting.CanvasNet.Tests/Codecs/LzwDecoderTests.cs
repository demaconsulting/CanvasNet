// cspell:ignore Lzw KwKwK
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the internal <see cref="LzwDecoder"/> shared by <see cref="TiffCodec"/> and the
///     PDF <c>LZWDecode</c> filter: both code-width timings, table-full behavior, and the hard output bound.
/// </summary>
public class LzwDecoderTests
{
    /// <summary>
    ///     A test-only classic-LZW encoder (independent of the decoder under test) producing a
    ///     Clear-prefixed, EOD-terminated MSB-first stream; it never emits a second Clear, so a
    ///     long input fills the 4096-entry table.
    /// </summary>
    private static byte[] Encode(byte[] data, bool earlyChange)
    {
        var earlyChangeAmount = earlyChange ? 1 : 0;
        var bits = new List<int>();

        void Emit(int code, int width)
        {
            for (var i = width - 1; i >= 0; i--)
            {
                bits.Add((code >> i) & 1);
            }
        }

        var table = new Dictionary<(int Prefix, byte Suffix), int>();
        var codeWidth = 9;
        var nextCode = 258;
        var extCode = (1 << codeWidth) + 1 - earlyChangeAmount;

        Emit(256, codeWidth);
        if (data.Length > 0)
        {
            var currentCode = (int)data[0];
            for (var i = 1; i < data.Length; i++)
            {
                var next = data[i];
                if (table.TryGetValue((currentCode, next), out var extended))
                {
                    currentCode = extended;
                    continue;
                }

                Emit(currentCode, codeWidth);
                if (nextCode < 4096)
                {
                    table[(currentCode, next)] = nextCode;
                    nextCode++;
                    if (nextCode >= extCode && codeWidth < 12)
                    {
                        codeWidth++;
                        extCode = (1 << codeWidth) + 1 - earlyChangeAmount;
                    }
                }

                currentCode = next;
            }

            Emit(currentCode, codeWidth);
        }

        Emit(257, codeWidth);
        return PackBits(bits);
    }

    /// <summary>Packs hand-written <c>(code, width)</c> pairs MSB-first, padding the last byte with zero bits.</summary>
    private static byte[] Pack(params (int Code, int Width)[] codes)
    {
        var bits = new List<int>();
        foreach (var (code, width) in codes)
        {
            for (var i = width - 1; i >= 0; i--)
            {
                bits.Add((code >> i) & 1);
            }
        }

        return PackBits(bits);
    }

    /// <summary>Packs a bit list into bytes, MSB first, padding with zero bits.</summary>
    private static byte[] PackBits(List<int> bits)
    {
        while (bits.Count % 8 != 0)
        {
            bits.Add(0);
        }

        var output = new byte[bits.Count / 8];
        for (var i = 0; i < output.Length; i++)
        {
            var value = 0;
            for (var j = 0; j < 8; j++)
            {
                value = (value << 1) | bits[(i * 8) + j];
            }

            output[i] = (byte)value;
        }

        return output;
    }

    /// <summary>Builds deterministic, moderately compressible data long enough to cross every code-width boundary.</summary>
    private static byte[] BuildVariedData(int length)
    {
        var data = new byte[length];
        var state = 12345u;
        for (var i = 0; i < length; i++)
        {
            state = (state * 1103515245u) + 12345u;
            data[i] = (byte)((state >> 16) % 7);
        }

        return data;
    }

    /// <summary>Proves the ISO 32000 section 7.4.4.2 worked example decodes to its expected bytes.</summary>
    [Fact]
    public void LzwDecoder_Decode_SpecExample_DecodesExpectedBytes()
    {
        byte[] spec = [0x80, 0x0B, 0x60, 0x50, 0x22, 0x0C, 0x0C, 0x85, 0x01];

        var decoded = LzwDecoder.Decode(spec, earlyChange: true, maxOutputBytes: 1024);

        Assert.Equal("-----A---B"u8.ToArray(), decoded);
    }

    /// <summary>Proves both code-width timings round-trip data that crosses every width boundary and fills the table.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LzwDecoder_Decode_RoundTripsAcrossWidthBoundariesAndFullTable(bool earlyChange)
    {
        var data = BuildVariedData(40_000);
        var encoded = Encode(data, earlyChange);

        var decoded = LzwDecoder.Decode(encoded, earlyChange, data.Length);

        Assert.Equal(data, decoded);
    }

    /// <summary>Proves decoding with the opposite timing from the one used to encode fails closed rather than returning data.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LzwDecoder_Decode_WrongEarlyChangeTiming_ThrowsInvalidDataException(bool encodedEarly)
    {
        var data = BuildVariedData(2_000);
        var encoded = Encode(data, encodedEarly);

        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(encoded, !encodedEarly, data.Length));
    }

    /// <summary>Proves the self-referential "KwKwK" code (a long run of one byte) decodes correctly.</summary>
    [Fact]
    public void LzwDecoder_Decode_RepeatedByteRun_DecodesKwKwKCodes()
    {
        var data = new byte[5_000];
        Array.Fill(data, (byte)'A');

        var decoded = LzwDecoder.Decode(Encode(data, true), earlyChange: true, maxOutputBytes: data.Length);

        Assert.Equal(data, decoded);
    }

    /// <summary>Proves output of exactly the limit is accepted and one byte over is rejected.</summary>
    [Fact]
    public void LzwDecoder_Decode_OutputLimit_AcceptsExactSizeAndRejectsOneMore()
    {
        var data = BuildVariedData(1_000);
        var encoded = Encode(data, true);

        Assert.Equal(data, LzwDecoder.Decode(encoded, true, 1_000));
        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(encoded, true, 999));
    }

    /// <summary>Proves a tiny stream that expands into a huge output (a decompression bomb) is rejected at the limit.</summary>
    [Fact]
    public void LzwDecoder_Decode_Bomb_RejectedAtLimit()
    {
        var data = new byte[4 * 1024 * 1024];
        Array.Fill(data, (byte)'A');
        var bomb = Encode(data, true);
        Assert.True(bomb.Length < 8_000, "the test stream must be tiny relative to its expansion");

        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(bomb, true, 64 * 1024));
    }

    /// <summary>Proves a zero limit accepts an empty stream and rejects any output.</summary>
    [Fact]
    public void LzwDecoder_Decode_ZeroLimit_AcceptsOnlyEmptyOutput()
    {
        Assert.Empty(LzwDecoder.Decode(Pack((256, 9), (257, 9)), true, 0));
        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(Pack((256, 9), (65, 9), (257, 9)), true, 0));
    }

    /// <summary>Proves an unrepresentable limit is rejected.</summary>
    [Fact]
    public void LzwDecoder_Decode_UnrepresentableLimit_ThrowsInvalidDataException()
    {
        var stream = Pack((256, 9), (257, 9));

        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(stream, true, -1));
        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(stream, true, long.MaxValue));
    }

    /// <summary>Proves a leading Clear code is optional for the shared decoder (the TIFF caller requires it itself).</summary>
    [Fact]
    public void LzwDecoder_Decode_NoLeadingClear_Decodes()
    {
        var decoded = LzwDecoder.Decode(Pack((65, 9), (66, 9), (257, 9)), true, 16);

        Assert.Equal<byte>([65, 66], decoded);
    }

    /// <summary>Proves a Clear code mid-stream resets the table, so a code assigned before it is no longer valid.</summary>
    [Fact]
    public void LzwDecoder_Decode_ClearMidStream_ResetsTable()
    {
        var stream = Pack((256, 9), (65, 9), (65, 9), (65, 9), (256, 9), (66, 9), (260, 9));

        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(stream, true, 64));
    }

    /// <summary>Proves a stream that ends before its end-of-data code is rejected.</summary>
    [Fact]
    public void LzwDecoder_Decode_MissingEndCode_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(Pack((256, 9), (65, 9), (66, 9)), true, 64));
        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode([], true, 64));
    }

    /// <summary>Proves codes that are not valid for the current table state are rejected.</summary>
    [Fact]
    public void LzwDecoder_Decode_InvalidCode_ThrowsInvalidDataException()
    {
        // 300 before any table entry exists, and a code beyond "the next code" after a literal
        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(Pack((256, 9), (300, 9), (257, 9)), true, 64));
        Assert.Throws<InvalidDataException>(() => LzwDecoder.Decode(Pack((256, 9), (65, 9), (300, 9), (257, 9)), true, 64));
    }
}

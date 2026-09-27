using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class JpegCodec
{
    // ================================================================================================
    // Encoder
    // ================================================================================================

    private static class Encoder
    {
        private sealed class EncodeHuffmanEntry
        {
            public int Code;
            public int Length;
        }

        /// <summary>
        ///     Performs the forward 8x8 DCT (ITU-T T.81 Annex A.3.2) on a level-shifted spatial-domain
        ///     block, returning natural-order coefficients.
        /// </summary>
        private static double[][] Fdct2D(double[][] pixel)
        {
            var tempT = NewBlock(); // tempT[u][y]
            for (var y = 0; y < 8; y++)
            {
                for (var u = 0; u < 8; u++)
                {
                    tempT[u][y] = 0.5 * DotProduct8(pixel[y], Basis[u]);
                }
            }

            var natural = NewBlock(); // natural[v][u]
            for (var u = 0; u < 8; u++)
            {
                for (var v = 0; v < 8; v++)
                {
                    natural[v][u] = 0.5 * DotProduct8(tempT[u], Basis[v]);
                }
            }

            return natural;
        }

        private static void Quantize(double[][] natural, int[] quantZigzag, int[] outCoeffs)
        {
            for (var z = 0; z < 64; z++)
            {
                var n = ZigZagOrder[z];
                var value = natural[n / 8][n % 8] / quantZigzag[z];
                outCoeffs[z] = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>
        ///     Converts one RGB pixel to YCbCr using the ITU-R BT.601 coefficients (the inverse of
        ///     the decoder's pixel conversion), used by the encoder.
        /// </summary>
        private static void ConvertRgbToYCbCr(byte r, byte g, byte b, out byte y, out byte cb, out byte cr)
        {
            var yf = (0.299 * r) + (0.587 * g) + (0.114 * b);
            var cbf = 128 - (0.168736 * r) - (0.331264 * g) + (0.5 * b);
            var crf = 128 + (0.5 * r) - (0.418688 * g) - (0.081312 * b);
            y = ClampToByte(yf);
            cb = ClampToByte(cbf);
            cr = ClampToByte(crf);
        }

        public static void Encode(Surface surface, Stream stream, int quality)
        {
            var width = surface.Width;
            var height = surface.Height;

            var scale = quality < 50 ? 5000 / quality : 200 - (quality * 2);
            var lumaQuant = ScaleQuantTable(StandardLuminanceQuantTable, scale);
            var chromaQuant = ScaleQuantTable(StandardChrominanceQuantTable, scale);
            var lumaQuantZigZag = ToZigZag(lumaQuant);
            var chromaQuantZigZag = ToZigZag(chromaQuant);

            var dcLumaCodes = BuildEncodeTable(StandardDcLuminanceBits, StandardDcLuminanceValues);
            var acLumaCodes = BuildEncodeTable(StandardAcLuminanceBits, StandardAcLuminanceValues);
            var dcChromaCodes = BuildEncodeTable(StandardDcChrominanceBits, StandardDcChrominanceValues);
            var acChromaCodes = BuildEncodeTable(StandardAcChrominanceBits, StandardAcChrominanceValues);

            var mcusAcross = (width + 15) / 16;
            var mcusDown = (height + 15) / 16;
            var paddedWidth = mcusAcross * 16;
            var paddedHeight = mcusDown * 16;

            var planes = BuildPlanes(surface, new PlaneDimensions(width, height, paddedWidth, paddedHeight));
            var yPlane = planes.Y;
            var cbPlane = planes.Cb;
            var crPlane = planes.Cr;
            var chromaWidth = planes.ChromaWidth;

            WriteSoi(stream);
            WriteDqt(stream, 0, lumaQuantZigZag);
            WriteDqt(stream, 1, chromaQuantZigZag);
            WriteSof0(stream, width, height);
            WriteDht(stream, 0, 0, StandardDcLuminanceBits, StandardDcLuminanceValues);
            WriteDht(stream, 1, 0, StandardAcLuminanceBits, StandardAcLuminanceValues);
            WriteDht(stream, 0, 1, StandardDcChrominanceBits, StandardDcChrominanceValues);
            WriteDht(stream, 1, 1, StandardAcChrominanceBits, StandardAcChrominanceValues);
            WriteSos(stream);

            var writer = new BitWriter(stream);
            var dcPredY = 0;
            var dcPredCb = 0;
            var dcPredCr = 0;
            var coeffs = new int[64];

            for (var mcuY = 0; mcuY < mcusDown; mcuY++)
            {
                for (var mcuX = 0; mcuX < mcusAcross; mcuX++)
                {
                    for (var v = 0; v < 2; v++)
                    {
                        for (var h = 0; h < 2; h++)
                        {
                            var block = ExtractBlock(yPlane, paddedWidth, (mcuX * 16) + (h * 8), (mcuY * 16) + (v * 8));
                            EncodeBlock(block, lumaQuantZigZag, coeffs, ref dcPredY, dcLumaCodes, acLumaCodes, writer);
                        }
                    }

                    var cbBlock = ExtractBlock(cbPlane, chromaWidth, mcuX * 8, mcuY * 8);
                    EncodeBlock(cbBlock, chromaQuantZigZag, coeffs, ref dcPredCb, dcChromaCodes, acChromaCodes, writer);

                    var crBlock = ExtractBlock(crPlane, chromaWidth, mcuX * 8, mcuY * 8);
                    EncodeBlock(crBlock, chromaQuantZigZag, coeffs, ref dcPredCr, dcChromaCodes, acChromaCodes, writer);
                }
            }

            writer.FlushWithPadding();
            WriteMarker(stream, MarkerEoi);
        }

        private static int[] ScaleQuantTable(int[] baseTable, int scale)
        {
            var result = new int[64];
            for (var i = 0; i < 64; i++)
            {
                var value = ((baseTable[i] * scale) + 50) / 100;
                result[i] = Math.Clamp(value, 1, 255);
            }

            return result;
        }

        private static int[] ToZigZag(int[] natural)
        {
            var result = new int[64];
            for (var z = 0; z < 64; z++)
            {
                result[z] = natural[ZigZagOrder[z]];
            }

            return result;
        }

        private static Dictionary<int, EncodeHuffmanEntry> BuildEncodeTable(byte[] bits, byte[] values)
        {
            var result = new Dictionary<int, EncodeHuffmanEntry>();
            var code = 0;
            var pointer = 0;
            for (var length = 1; length <= 16; length++)
            {
                var count = bits[length - 1];
                for (var i = 0; i < count; i++)
                {
                    result[values[pointer]] = new EncodeHuffmanEntry { Code = code, Length = length };
                    code++;
                    pointer++;
                }

                code <<= 1;
            }

            return result;
        }

        /// <summary>
        ///     The source image dimensions together with the MCU-padded dimensions used for
        ///     chroma subsampling, grouped into a single parameter so <see cref="BuildPlanes"/>
        ///     does not need to accept each one individually.
        /// </summary>
        private readonly record struct PlaneDimensions(int Width, int Height, int PaddedWidth, int PaddedHeight);

        /// <summary>
        ///     The Y/Cb/Cr sample planes produced by <see cref="BuildPlanes"/>: a full-resolution
        ///     luma plane and 2x2 box-downsampled, MCU-padded chroma planes, alongside the chroma
        ///     planes' shared row stride.
        /// </summary>
        private readonly record struct PlaneSet(byte[] Y, byte[] Cb, byte[] Cr, int ChromaWidth);

        private static PlaneSet BuildPlanes(Surface surface, PlaneDimensions dimensions)
        {
            var width = dimensions.Width;
            var height = dimensions.Height;
            var paddedWidth = dimensions.PaddedWidth;
            var paddedHeight = dimensions.PaddedHeight;

            var fullY = new byte[width * height];
            var fullCb = new byte[width * height];
            var fullCr = new byte[width * height];

            for (var y = 0; y < height; y++)
            {
                var rowBytes = surface.GetRowSpanBytes(y);
                var rowOffset = y * width;
                for (var x = 0; x < width; x++)
                {
                    var idx = x * 4;
                    ConvertRgbToYCbCr(rowBytes[idx], rowBytes[idx + 1], rowBytes[idx + 2], out var yy, out var cb, out var cr);
                    fullY[rowOffset + x] = yy;
                    fullCb[rowOffset + x] = cb;
                    fullCr[rowOffset + x] = cr;
                }
            }

            var yPlane = PadReplicate(fullY, width, height, paddedWidth, paddedHeight);
            var paddedCb = PadReplicate(fullCb, width, height, paddedWidth, paddedHeight);
            var paddedCr = PadReplicate(fullCr, width, height, paddedWidth, paddedHeight);

            var chromaWidth = paddedWidth / 2;
            var cbPlane = DownsampleBox2X2(paddedCb, paddedWidth, paddedHeight);
            var crPlane = DownsampleBox2X2(paddedCr, paddedWidth, paddedHeight);

            return new PlaneSet(yPlane, cbPlane, crPlane, chromaWidth);
        }

        private static byte[] PadReplicate(byte[] source, int width, int height, int paddedWidth, int paddedHeight)
        {
            var result = new byte[paddedWidth * paddedHeight];
            for (var y = 0; y < paddedHeight; y++)
            {
                var srcY = Math.Min(y, height - 1);
                var srcRow = srcY * width;
                var dstRow = y * paddedWidth;
                for (var x = 0; x < paddedWidth; x++)
                {
                    var srcX = Math.Min(x, width - 1);
                    result[dstRow + x] = source[srcRow + srcX];
                }
            }

            return result;
        }

        private static byte[] DownsampleBox2X2(byte[] source, int width, int height)
        {
            var outWidth = width / 2;
            var outHeight = height / 2;
            var result = new byte[outWidth * outHeight];
            for (var y = 0; y < outHeight; y++)
            {
                var srcRow0 = (y * 2) * width;
                var srcRow1 = ((y * 2) + 1) * width;
                var dstRow = y * outWidth;
                for (var x = 0; x < outWidth; x++)
                {
                    var srcX = x * 2;
                    var sum = source[srcRow0 + srcX] + source[srcRow0 + srcX + 1] +
                              source[srcRow1 + srcX] + source[srcRow1 + srcX + 1];
                    result[dstRow + x] = (byte)((sum + 2) / 4);
                }
            }

            return result;
        }

        private static double[][] ExtractBlock(byte[] plane, int stride, int startX, int startY)
        {
            var block = NewBlock();
            for (var y = 0; y < 8; y++)
            {
                var rowOffset = ((startY + y) * stride) + startX;
                for (var x = 0; x < 8; x++)
                {
                    block[y][x] = plane[rowOffset + x] - 128.0;
                }
            }

            return block;
        }

        private static void EncodeBlock(
            double[][] block,
            int[] quantZigzag,
            int[] coeffs,
            ref int dcPredictor,
            Dictionary<int, EncodeHuffmanEntry> dcCodes,
            Dictionary<int, EncodeHuffmanEntry> acCodes,
            BitWriter writer)
        {
            var natural = Fdct2D(block);
            Quantize(natural, quantZigzag, coeffs);

            var diff = coeffs[0] - dcPredictor;
            dcPredictor = coeffs[0];
            WriteDcValue(diff, dcCodes, writer);

            var runLength = 0;
            for (var k = 1; k < 64; k++)
            {
                if (coeffs[k] == 0)
                {
                    runLength++;
                    continue;
                }

                while (runLength >= 16)
                {
                    WriteHuffman(acCodes, 0xF0, writer); // ZRL
                    runLength -= 16;
                }

                var size = MagnitudeSize(coeffs[k]);
                WriteHuffman(acCodes, (runLength << 4) | size, writer);
                WriteMagnitudeBits(coeffs[k], size, writer);
                runLength = 0;
            }

            if (runLength > 0)
            {
                WriteHuffman(acCodes, 0x00, writer); // EOB
            }
        }

        private static void WriteDcValue(int diff, Dictionary<int, EncodeHuffmanEntry> dcCodes, BitWriter writer)
        {
            var size = MagnitudeSize(diff);
            WriteHuffman(dcCodes, size, writer);
            WriteMagnitudeBits(diff, size, writer);
        }

        private static int MagnitudeSize(int value)
        {
            var magnitude = Math.Abs(value);
            var size = 0;
            while (magnitude != 0)
            {
                size++;
                magnitude >>= 1;
            }

            return size;
        }

        private static void WriteMagnitudeBits(int value, int size, BitWriter writer)
        {
            if (size == 0)
            {
                return;
            }

            var bits = value >= 0 ? value : value + (1 << size) - 1;
            writer.WriteBits(bits, size);
        }

        private static void WriteHuffman(Dictionary<int, EncodeHuffmanEntry> table, int symbol, BitWriter writer)
        {
            var entry = table[symbol];
            writer.WriteBits(entry.Code, entry.Length);
        }

        private static void WriteMarker(Stream stream, byte marker)
        {
            stream.WriteByte(MarkerPrefix);
            stream.WriteByte(marker);
        }

        private static void WriteUInt16Be(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static void WriteSoi(Stream stream) => WriteMarker(stream, MarkerSoi);

        private static void WriteDqt(Stream stream, int id, int[] tableZigZag)
        {
            WriteMarker(stream, MarkerDqt);
            WriteUInt16Be(stream, 2 + 1 + 64);
            stream.WriteByte((byte)id);
            for (var i = 0; i < 64; i++)
            {
                stream.WriteByte((byte)tableZigZag[i]);
            }
        }

        private static void WriteSof0(Stream stream, int width, int height)
        {
            WriteMarker(stream, MarkerSof0);
            WriteUInt16Be(stream, 2 + 1 + 2 + 2 + 1 + (3 * 3));
            stream.WriteByte(8); // precision
            WriteUInt16Be(stream, height);
            WriteUInt16Be(stream, width);
            stream.WriteByte(3); // number of components

            stream.WriteByte(1); // Y id
            stream.WriteByte(0x22); // H=2, V=2
            stream.WriteByte(0); // quant table 0

            stream.WriteByte(2); // Cb id
            stream.WriteByte(0x11); // H=1, V=1
            stream.WriteByte(1); // quant table 1

            stream.WriteByte(3); // Cr id
            stream.WriteByte(0x11); // H=1, V=1
            stream.WriteByte(1); // quant table 1
        }

        private static void WriteDht(Stream stream, int tableClass, int id, byte[] bits, byte[] values)
        {
            WriteMarker(stream, MarkerDht);
            WriteUInt16Be(stream, 2 + 1 + 16 + values.Length);
            stream.WriteByte((byte)((tableClass << 4) | id));
            foreach (var b in bits)
            {
                stream.WriteByte(b);
            }

            foreach (var v in values)
            {
                stream.WriteByte(v);
            }
        }

        private static void WriteSos(Stream stream)
        {
            WriteMarker(stream, MarkerSos);
            WriteUInt16Be(stream, 2 + 1 + (3 * 2) + 3);
            stream.WriteByte(3); // number of components in scan

            stream.WriteByte(1); // Y id
            stream.WriteByte(0x00); // DC=0, AC=0

            stream.WriteByte(2); // Cb id
            stream.WriteByte(0x11); // DC=1, AC=1

            stream.WriteByte(3); // Cr id
            stream.WriteByte(0x11); // DC=1, AC=1

            stream.WriteByte(0); // Ss
            stream.WriteByte(63); // Se
            stream.WriteByte(0x00); // Ah/Al
        }
    }
}

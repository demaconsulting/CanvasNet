using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class JpegCodec
{
    private static partial class Decoder
    {
        /// <summary>
        ///     The tables and frame-level parameters shared by every scan within a JPEG stream,
        ///     grouped into a single parameter so <see cref="DecodeScan"/> does not need to accept
        ///     each one individually.
        /// </summary>
        private readonly record struct ScanDecodeContext(
            Component[] Components,
            Dictionary<int, HuffmanTable> DcTables,
            Dictionary<int, HuffmanTable> AcTables,
            bool Progressive,
            int RestartInterval);

        /// <summary>
        ///     The component selectors and spectral-selection/successive-approximation parameters
        ///     parsed from a single SOS segment, produced by <see cref="ParseScanHeader"/>.
        /// </summary>
        private readonly record struct ScanHeader(Component[] ScanComponents, int Ss, int Se, int Ah, int Al);

        /// <summary>
        ///     The MCU-grid dimensions shared by every scan in the frame, derived from the maximum
        ///     component sampling factors and the frame's pixel dimensions.
        /// </summary>
        private readonly record struct McuGrid(int HMax, int VMax, int McusAcross, int McusDown);

        private static int DecodeScan(byte[] file, int pos, int frameWidth, int frameHeight, ScanDecodeContext context)
        {
            var (header, p) = ParseScanHeader(file, pos, context.Components, context.Progressive);

            // Determine (and allocate, on first use) the MCU-grid dimensions shared by every scan.
            var hMax = context.Components.Max(c => c.H);
            var vMax = context.Components.Max(c => c.V);
            var mcusAcross = (frameWidth + (8 * hMax) - 1) / (8 * hMax);
            var mcusDown = (frameHeight + (8 * vMax) - 1) / (8 * vMax);
            var grid = new McuGrid(hMax, vMax, mcusAcross, mcusDown);

            EnsureComponentBlocksAllocated(context.Components, mcusAcross, mcusDown);

            foreach (var component in header.ScanComponents)
            {
                component.DcPredictor = 0;
            }

            var reader = new BitReader(file, p);

            return DecodeScanUnits(reader, header, context, frameWidth, frameHeight, grid);
        }

        /// <summary>
        ///     Parses a SOS segment's component selectors (assigning each referenced component's
        ///     DC/AC Huffman table selectors) and spectral-selection/successive-approximation
        ///     parameters, validating the segment length and baseline spectral-range constraints.
        /// </summary>
        private static (ScanHeader Header, int Position) ParseScanHeader(
            byte[] file, int pos, Component[] components, bool progressive)
        {
            var length = ReadUInt16Be(file, pos);
            var p = pos + 2;
            var numComponentsInScan = ReadByte(file, p++);
            var scanComponents = new Component[numComponentsInScan];
            for (var i = 0; i < numComponentsInScan; i++)
            {
                var selector = ReadByte(file, p++);
                var tableSelectors = ReadByte(file, p++);
                var component = Array.Find(components, c => c.Id == selector) ??
                                 throw new InvalidDataException($"SOS references unknown component id {selector}.");
                component.DcSelector = tableSelectors >> 4;
                component.AcSelector = tableSelectors & 0xF;
                scanComponents[i] = component;
            }

            var ss = ReadByte(file, p++);
            var se = ReadByte(file, p++);
            var ahAl = ReadByte(file, p++);
            var ah = ahAl >> 4;
            var al = ahAl & 0xF;

            if (p != pos + length)
            {
                throw new InvalidDataException("Malformed JPEG SOS segment length.");
            }

            if (!progressive && (ss != 0 || se != 63 || ah != 0 || al != 0))
            {
                throw new InvalidDataException("Baseline JPEG scan must cover the full spectral range with no successive approximation.");
            }

            return (new ScanHeader(scanComponents, ss, se, ah, al), p);
        }

        /// <summary>
        ///     Allocates each component's block storage the first time it is referenced by any
        ///     scan, sized to the shared MCU grid so later scans referencing the same component
        ///     reuse the same block array.
        /// </summary>
        private static void EnsureComponentBlocksAllocated(Component[] components, int mcusAcross, int mcusDown)
        {
            foreach (var component in components.Where(component => component.Blocks == null))
            {
                component.BlocksPerLineMcu = mcusAcross * component.H;
                component.BlocksPerColumnMcu = mcusDown * component.V;
                var count = component.BlocksPerLineMcu * component.BlocksPerColumnMcu;
                component.Blocks = new int[count][];
                for (var i = 0; i < count; i++)
                {
                    component.Blocks[i] = new int[64];
                }
            }
        }

        /// <summary>
        ///     Decodes every MCU (interleaved scan) or block (non-interleaved scan) in the scan,
        ///     honoring restart markers at the configured restart interval, and returns the stream
        ///     position immediately following the last decoded unit.
        /// </summary>
        private static int DecodeScanUnits(
            BitReader reader, ScanHeader header, ScanDecodeContext context, int frameWidth, int frameHeight, McuGrid grid)
        {
            var eobRun = 0;
            var scanComponents = header.ScanComponents;
            var interleaved = scanComponents.Length > 1;
            int totalUnits;
            int nonInterleavedBlocksPerLine = 0;
            if (interleaved)
            {
                totalUnits = grid.McusAcross * grid.McusDown;
            }
            else
            {
                var comp = scanComponents[0];
                var compSamplesPerLine = ((frameWidth * comp.H) + grid.HMax - 1) / grid.HMax;
                var compSamplesPerColumn = ((frameHeight * comp.V) + grid.VMax - 1) / grid.VMax;
                nonInterleavedBlocksPerLine = (compSamplesPerLine + 7) / 8;
                var blocksPerColumn = (compSamplesPerColumn + 7) / 8;
                totalUnits = nonInterleavedBlocksPerLine * blocksPerColumn;
            }

            var unitsSinceRestart = 0;
            var blockContext = new BlockDecodeContext(
                context.DcTables, context.AcTables, context.Progressive, header.Ss, header.Se, header.Ah, header.Al);
            for (var unit = 0; unit < totalUnits; unit++)
            {
                if (context.RestartInterval > 0 && unitsSinceRestart == context.RestartInterval)
                {
                    reader.Realign();
                    reader.ExpectRestartMarker();
                    foreach (var component in scanComponents)
                    {
                        component.DcPredictor = 0;
                    }

                    eobRun = 0;
                    unitsSinceRestart = 0;
                }

                if (interleaved)
                {
                    DecodeInterleavedUnit(unit, grid.McusAcross, scanComponents, reader, blockContext, ref eobRun);
                }
                else
                {
                    var comp = scanComponents[0];
                    var blockCol = unit % nonInterleavedBlocksPerLine;
                    var blockRow = unit / nonInterleavedBlocksPerLine;
                    var block = comp.Blocks![(blockRow * comp.BlocksPerLineMcu) + blockCol];
                    DecodeBlock(block, comp, reader, blockContext, ref eobRun);
                }

                unitsSinceRestart++;
            }

            return reader.Position;
        }

        /// <summary>
        ///     Decodes every block of every scan component making up a single interleaved MCU at
        ///     the given MCU index.
        /// </summary>
        private static void DecodeInterleavedUnit(
            int unit,
            int mcusAcross,
            Component[] scanComponents,
            BitReader reader,
            BlockDecodeContext blockContext,
            ref int eobRun)
        {
            var mcuX = unit % mcusAcross;
            var mcuY = unit / mcusAcross;
            foreach (var component in scanComponents)
            {
                for (var v = 0; v < component.V; v++)
                {
                    for (var h = 0; h < component.H; h++)
                    {
                        var blockCol = (mcuX * component.H) + h;
                        var blockRow = (mcuY * component.V) + v;
                        var block = component.Blocks![(blockRow * component.BlocksPerLineMcu) + blockCol];
                        DecodeBlock(block, component, reader, blockContext, ref eobRun);
                    }
                }
            }
        }

        /// <summary>
        ///     The Huffman tables and spectral-selection/successive-approximation parameters
        ///     shared by every block decoded within a single scan, grouped into a single parameter
        ///     so <see cref="DecodeBlock"/> does not need to accept each one individually.
        /// </summary>
        private readonly record struct BlockDecodeContext(
            Dictionary<int, HuffmanTable> DcTables,
            Dictionary<int, HuffmanTable> AcTables,
            bool Progressive,
            int Ss,
            int Se,
            int Ah,
            int Al);

        private static void DecodeBlock(int[] block, Component component, BitReader reader, BlockDecodeContext context, ref int eobRun)
        {
            if (!context.Progressive)
            {
                DecodeBaselineBlock(block, component, reader, context.DcTables, context.AcTables);
                return;
            }

            if (context.Ss == 0)
            {
                DecodeProgressiveDc(block, component, reader, context.DcTables, context.Ah, context.Al);
                return;
            }

            var acTableProgressive = GetTable(context.AcTables, component.AcSelector, "AC");
            if (context.Ah == 0)
            {
                DecodeAcFirst(block, acTableProgressive, reader, context.Ss, context.Se, context.Al, ref eobRun);
            }
            else
            {
                DecodeAcRefine(block, acTableProgressive, reader, context.Ss, context.Se, context.Al, ref eobRun);
            }
        }

        /// <summary>
        ///     Decodes a single baseline (sequential DCT) block: the DC coefficient via
        ///     differential prediction, followed by all non-zero AC coefficients in zigzag order
        ///     up to the first end-of-block run or index 63.
        /// </summary>
        private static void DecodeBaselineBlock(
            int[] block,
            Component component,
            BitReader reader,
            Dictionary<int, HuffmanTable> dcTables,
            Dictionary<int, HuffmanTable> acTables)
        {
            var dcTable = GetTable(dcTables, component.DcSelector, "DC");
            var acTable = GetTable(acTables, component.AcSelector, "AC");

            var t = BitReader.Decode(dcTable, reader);
            var diff = reader.Receive(t);
            component.DcPredictor += diff;
            block[0] = component.DcPredictor;

            var k = 1;
            while (k < 64)
            {
                var rs = BitReader.Decode(acTable, reader);
                var r = rs >> 4;
                var s = rs & 0xF;
                if (s == 0)
                {
                    if (r != 15)
                    {
                        break;
                    }

                    k += 16;
                }
                else
                {
                    k += r;
                    if (k > 63)
                    {
                        throw new InvalidDataException("Malformed JPEG entropy-coded data: AC coefficient index out of range.");
                    }

                    block[k] = reader.Receive(s);
                    k++;
                }
            }
        }

        /// <summary>
        ///     Decodes the DC coefficient of a progressive-scan block: a full Huffman-coded
        ///     magnitude/diff on the first DC scan (successive approximation high bit), or a
        ///     single successive-approximation refinement bit on any later DC scan.
        /// </summary>
        private static void DecodeProgressiveDc(
            int[] block, Component component, BitReader reader, Dictionary<int, HuffmanTable> dcTables, int ah, int al)
        {
            if (ah == 0)
            {
                var dcTable = GetTable(dcTables, component.DcSelector, "DC");
                var t = BitReader.Decode(dcTable, reader);
                var diff = reader.Receive(t);
                component.DcPredictor += diff;
                block[0] = component.DcPredictor << al;
            }
            else
            {
                if (reader.ReadBit() != 0)
                {
                    block[0] |= 1 << al;
                }
            }
        }

        private static void DecodeAcFirst(int[] block, HuffmanTable acTable, BitReader reader, int ss, int se, int al, ref int eobRun)
        {
            if (eobRun > 0)
            {
                eobRun--;
                return;
            }

            var k = ss;
            while (k <= se)
            {
                var rs = BitReader.Decode(acTable, reader);
                var r = rs >> 4;
                var s = rs & 0xF;
                if (s == 0)
                {
                    if (r < 15)
                    {
                        eobRun = (1 << r) - 1;
                        if (r > 0)
                        {
                            eobRun += reader.ReadBits(r);
                        }

                        break;
                    }

                    k += 16;
                }
                else
                {
                    k += r;
                    if (k > se)
                    {
                        throw new InvalidDataException("Malformed JPEG entropy-coded data: AC coefficient index out of range.");
                    }

                    block[k] = reader.Receive(s) << al;
                    k++;
                }
            }
        }

        private static void DecodeAcRefine(int[] block, HuffmanTable acTable, BitReader reader, int ss, int se, int al, ref int eobRun)
        {
            var rp = new RefinementParams(reader, se, 1 << al, -1 << al);
            var k = ss;

            if (eobRun == 0)
            {
                DecodeAcRefineNewCoefficients(block, acTable, rp, ref k, ref eobRun);
            }

            if (eobRun > 0)
            {
                RefineRemainingCoefficients(block, ref k, rp);
                eobRun--;
            }
        }

        /// <summary>
        ///     Groups the successive-approximation refinement state (ITU-T T.81 section G.1.2.3)
        ///     shared by the AC-refinement coefficient walkers: the bit reader, the end-of-band
        ///     index, and the positive/negative refinement bit values.
        /// </summary>
        private readonly record struct RefinementParams(BitReader Reader, int Se, int P1, int M1);

        /// <summary>
        ///     Applies a successive-approximation refinement bit to a single already-nonzero
        ///     coefficient, per ITU-T T.81 section G.1.2.3: the bit is only consumed (and the
        ///     coefficient only nudged toward zero-away) when the coefficient's next refinement
        ///     bit position is still unset.
        /// </summary>
        private static void RefineNonZeroCoefficient(int[] block, int k, RefinementParams rp)
        {
            if (block[k] != 0 && rp.Reader.ReadBit() != 0 && (block[k] & rp.P1) == 0)
            {
                block[k] += block[k] >= 0 ? rp.P1 : rp.M1;
            }
        }

        /// <summary>
        ///     Refines every remaining nonzero coefficient from <paramref name="k"/> through
        ///     <paramref name="rp"/>'s end-of-band index without placing any new coefficients, used
        ///     while an end-of-band run inherited from an earlier RS pair is still being consumed.
        /// </summary>
        private static void RefineRemainingCoefficients(int[] block, ref int k, RefinementParams rp)
        {
            while (k <= rp.Se)
            {
                RefineNonZeroCoefficient(block, k, rp);
                k++;
            }
        }

        /// <summary>
        ///     Decodes RS (run-length/size) pairs from the AC refinement scan, each of which
        ///     either starts a new end-of-band run (terminating this method) or specifies how many
        ///     zero coefficients to skip before placing one new nonzero coefficient; every
        ///     already-nonzero coefficient encountered along the way is refined per
        ///     <see cref="RefineNonZeroCoefficient"/>.
        /// </summary>
        private static void DecodeAcRefineNewCoefficients(
            int[] block, HuffmanTable acTable, RefinementParams rp, ref int k, ref int eobRun)
        {
            while (k <= rp.Se)
            {
                var rs = BitReader.Decode(acTable, rp.Reader);
                var r = rs >> 4;
                var s = rs & 0xF;
                var newValue = 0;

                if (s == 0)
                {
                    if (r < 15)
                    {
                        eobRun = 1 << r;
                        if (r > 0)
                        {
                            eobRun += rp.Reader.ReadBits(r);
                        }

                        r = 64; // sentinel: skip remaining coefficients (refinement only) below
                    }
                }
                else
                {
                    newValue = rp.Reader.ReadBit() != 0 ? rp.P1 : rp.M1;
                }

                RefineOrPlaceCoefficient(block, ref k, rp, r, newValue);
            }
        }

        /// <summary>
        ///     Walks coefficients from <paramref name="k"/> through <paramref name="rp"/>'s
        ///     end-of-band index, refining every already-nonzero coefficient, and counting down
        ///     <paramref name="zeroRunLength"/> zero coefficients before placing
        ///     <paramref name="newValue"/> (if nonzero) into the next zero coefficient slot and
        ///     stopping.
        /// </summary>
        private static void RefineOrPlaceCoefficient(
            int[] block, ref int k, RefinementParams rp, int zeroRunLength, int newValue)
        {
            var r = zeroRunLength;
            while (k <= rp.Se)
            {
                if (block[k] != 0)
                {
                    RefineNonZeroCoefficient(block, k, rp);
                }
                else
                {
                    if (r == 0)
                    {
                        if (newValue != 0)
                        {
                            block[k] = newValue;
                        }

                        k++;
                        break;
                    }

                    r--;
                }

                k++;
            }
        }

        private static HuffmanTable GetTable(Dictionary<int, HuffmanTable> tables, int selector, string kind)
        {
            if (!tables.TryGetValue(selector, out var table))
            {
                throw new InvalidDataException($"JPEG scan references undefined {kind} Huffman table {selector}.");
            }

            return table;
        }

        /// <summary>
        ///     Performs the inverse 8x8 DCT (ITU-T T.81 Annex A.3.3) on a natural-order coefficient
        ///     block, returning the spatial-domain block (still level-shifted, i.e. centered on zero).
        /// </summary>
        private static double[][] Idct2D(double[][] naturalCoeffs)
        {
            var tempT = NewBlock(); // tempT[x][v]
            for (var v = 0; v < 8; v++)
            {
                for (var x = 0; x < 8; x++)
                {
                    tempT[x][v] = 0.5 * DotProduct8(naturalCoeffs[v], BasisT[x]);
                }
            }

            var output = NewBlock(); // output[y][x]
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    output[y][x] = 0.5 * DotProduct8(tempT[x], BasisT[y]);
                }
            }

            return output;
        }

        private static double[][] Dequantize(int[] zigzagCoeffs, int[] quantZigzag)
        {
            var natural = NewBlock();
            for (var z = 0; z < 64; z++)
            {
                var n = ZigZagOrder[z];
                natural[n / 8][n % 8] = (double)zigzagCoeffs[z] * quantZigzag[z];
            }

            return natural;
        }

        /// <summary>
        ///     The reconstructed spatial-domain sample plane for each frame component, alongside
        ///     each plane's row stride (which may exceed the frame width when the component's
        ///     MCU-aligned block grid is wider than the actual image).
        /// </summary>
        private readonly record struct ComponentPlanes(byte[][] Planes, int[] Strides);

        private static Surface AssembleCanvas(int width, int height, Component[] components, Dictionary<int, int[]> quantTables)
        {
            var hMax = components.Max(c => c.H);
            var vMax = components.Max(c => c.V);

            var planes = ReconstructComponentPlanes(components, quantTables);

            var surface = new Surface(width, height);

            if (components.Length == 1)
            {
                WriteGrayscaleSurface(surface, planes.Planes[0], planes.Strides[0], width, height);
                return surface;
            }

            WriteColorSurface(surface, components, planes, width, height, hMax, vMax);
            return surface;
        }

        /// <summary>
        ///     Dequantizes and inverse-DCTs every block of every frame component, assembling each
        ///     component's blocks into a single MCU-aligned spatial-domain sample plane.
        /// </summary>
        private static ComponentPlanes ReconstructComponentPlanes(Component[] components, Dictionary<int, int[]> quantTables)
        {
            var planes = new byte[components.Length][];
            var planeStrides = new int[components.Length];

            for (var ci = 0; ci < components.Length; ci++)
            {
                var component = components[ci];
                if (!quantTables.TryGetValue(component.QuantSelector, out var quant))
                {
                    throw new InvalidDataException($"JPEG frame references undefined quantization table {component.QuantSelector}.");
                }

                var planeWidth = component.BlocksPerLineMcu * 8;
                var planeHeight = component.BlocksPerColumnMcu * 8;
                var plane = new byte[planeWidth * planeHeight];
                planeStrides[ci] = planeWidth;

                ReconstructComponentBlocks(component, quant, plane, planeWidth);

                planes[ci] = plane;
            }

            return new ComponentPlanes(planes, planeStrides);
        }

        /// <summary>
        ///     Dequantizes, inverse-DCTs, and level-shifts every 8x8 block of a single component
        ///     into its destination position within the component's spatial-domain sample plane.
        /// </summary>
        private static void ReconstructComponentBlocks(Component component, int[] quant, byte[] plane, int planeWidth)
        {
            for (var blockRow = 0; blockRow < component.BlocksPerColumnMcu; blockRow++)
            {
                for (var blockCol = 0; blockCol < component.BlocksPerLineMcu; blockCol++)
                {
                    var block = component.Blocks![(blockRow * component.BlocksPerLineMcu) + blockCol];
                    var natural = Dequantize(block, quant);
                    var spatial = Idct2D(natural);

                    for (var y = 0; y < 8; y++)
                    {
                        var rowOffset = ((blockRow * 8) + y) * planeWidth + (blockCol * 8);
                        for (var x = 0; x < 8; x++)
                        {
                            plane[rowOffset + x] = ClampToByte(spatial[y][x] + 128.0);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Writes a single-component (grayscale) plane into the destination surface, expanding
        ///     each sample to an opaque R == G == B pixel.
        /// </summary>
        private static void WriteGrayscaleSurface(Surface surface, byte[] plane, int stride, int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                var rowBytes = surface.GetRowSpanBytes(y);
                var srcRow = y * stride;
                for (var x = 0; x < width; x++)
                {
                    var v = plane[srcRow + x];
                    var idx = x * 4;
                    rowBytes[idx] = v;
                    rowBytes[idx + 1] = v;
                    rowBytes[idx + 2] = v;
                    rowBytes[idx + 3] = 255;
                }
            }
        }

        /// <summary>
        ///     Writes a three-component (Y/Cb/Cr) set of planes into the destination surface,
        ///     upsampling any subsampled chroma planes to full resolution and converting each row
        ///     to opaque RGB.
        /// </summary>
        private static void WriteColorSurface(
            Surface surface, Component[] components, ComponentPlanes planes, int width, int height, int hMax, int vMax)
        {
            var cbComp = components[1];
            var crComp = components[2];
            var yPlane = planes.Planes[0];
            var cbPlane = planes.Planes[1];
            var crPlane = planes.Planes[2];
            var yStride = planes.Strides[0];
            var cbStride = planes.Strides[1];
            var crStride = planes.Strides[2];

            var yRow = new byte[width];
            var cbRow = new byte[width];
            var crRow = new byte[width];
            var rRow = new byte[width];
            var gRow = new byte[width];
            var bRow = new byte[width];

            for (var y = 0; y < height; y++)
            {
                var yPlaneRow = y * yStride;
                var cbPlaneRow = (y * cbComp.V / vMax) * cbStride;
                var crPlaneRow = (y * crComp.V / vMax) * crStride;

                for (var x = 0; x < width; x++)
                {
                    yRow[x] = yPlane[yPlaneRow + x];
                    cbRow[x] = cbPlane[cbPlaneRow + (x * cbComp.H / hMax)];
                    crRow[x] = crPlane[crPlaneRow + (x * crComp.H / hMax)];
                }

                ConvertYCbCrRowToRgb(yRow, cbRow, crRow, rRow, gRow, bRow, width);

                var rowBytes = surface.GetRowSpanBytes(y);
                for (var x = 0; x < width; x++)
                {
                    var idx = x * 4;
                    rowBytes[idx] = rRow[x];
                    rowBytes[idx + 1] = gRow[x];
                    rowBytes[idx + 2] = bRow[x];
                    rowBytes[idx + 3] = 255;
                }
            }
        }
    }
}

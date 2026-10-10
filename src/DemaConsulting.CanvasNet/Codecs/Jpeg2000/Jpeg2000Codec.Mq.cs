namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // MQ arithmetic decoder (ITU-T T.800 Annex C) and the raw (bypass) bit reader
    // ================================================================================================

    /// <summary>The MQ-coder probability state table (Table C.2): Qe, NMPS, NLPS and SWITCH per state.</summary>
    private static readonly ushort[] MqQe =
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601,
        0x5601, 0x5401, 0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201, 0x1C01, 0x1801, 0x1601, 0x1401,
        0x1201, 0x1101, 0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
        0x0015, 0x0009, 0x0005, 0x0001, 0x5601,
    ];

    private static readonly byte[] MqNextMps =
    [
        1, 2, 3, 4, 5, 38, 7, 8, 9, 10, 11, 12, 13, 29, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
        33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 45, 46,
    ];

    private static readonly byte[] MqNextLps =
    [
        1, 6, 9, 12, 29, 33, 6, 14, 14, 14, 17, 18, 20, 21, 14, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
        29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 46,
    ];

    private static readonly byte[] MqSwitch =
    [
        1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0,
    ];

    /// <summary>The number of MQ contexts used by the tier-1 coder (0-8 ZC, 9-13 SC, 14-16 MR, 17 RL, 18 UNI).</summary>
    private const int MqContextCount = 19;

    /// <summary>The MQ decoder with its adaptive context states.</summary>
    internal sealed class MqDecoder
    {
        private readonly byte[] _state = new byte[MqContextCount];
        private readonly byte[] _mps = new byte[MqContextCount];
        private byte[] _data = [];
        private int _pos;
        private int _end;
        private uint _c;
        private uint _a;
        private int _ct;

        /// <summary>Resets all contexts to their initial states (Table D.7).</summary>
        public void ResetContexts()
        {
            Array.Clear(_state);
            Array.Clear(_mps);
            _state[0] = 4;
            _state[17] = 3;
            _state[18] = 46;
        }

        /// <summary>Initializes the arithmetic decoder on a codeword segment (INITDEC).</summary>
        /// <param name="data">The buffer holding the segment.</param>
        /// <param name="start">The first byte of the segment.</param>
        /// <param name="length">The segment length in bytes.</param>
        public void Init(byte[] data, int start, int length)
        {
            _data = data;
            _pos = start;
            _end = start + length;
            _c = (uint)ByteAt(_pos) << 16;
            ByteIn();
            _c <<= 7;
            _ct -= 7;
            _a = 0x8000;
        }

        /// <summary>Decodes one decision in context <paramref name="cx"/>.</summary>
        /// <param name="cx">The context index.</param>
        /// <returns>The decoded bit.</returns>
        public int DecodeBit(int cx)
        {
            int i = _state[cx];
            uint qe = MqQe[i];
            int d;
            _a -= qe;
            if ((_c >> 16) < qe)
            {
                // Conditional exchange on the LPS interval.
                if (_a < qe)
                {
                    d = _mps[cx];
                    _state[cx] = MqNextMps[i];
                }
                else
                {
                    d = 1 - _mps[cx];
                    if (MqSwitch[i] != 0)
                    {
                        _mps[cx] = (byte)(1 - _mps[cx]);
                    }

                    _state[cx] = MqNextLps[i];
                }

                _a = qe;
                Renormalize();
            }
            else
            {
                _c -= qe << 16;
                if ((_a & 0x8000) == 0)
                {
                    if (_a < qe)
                    {
                        d = 1 - _mps[cx];
                        if (MqSwitch[i] != 0)
                        {
                            _mps[cx] = (byte)(1 - _mps[cx]);
                        }

                        _state[cx] = MqNextLps[i];
                    }
                    else
                    {
                        d = _mps[cx];
                        _state[cx] = MqNextMps[i];
                    }

                    Renormalize();
                }
                else
                {
                    d = _mps[cx];
                }
            }

            return d;
        }

        private int ByteAt(int index) => index < _end ? _data[index] : 0xFF;

        private void ByteIn()
        {
            // Past the end of the segment the decoder is fed 0xFF bytes, as if a marker followed.
            var cur = ByteAt(_pos);
            var next = (uint)ByteAt(_pos + 1);
            if (cur == 0xFF)
            {
                if (next > 0x8F)
                {
                    _c += 0xFF00;
                    _ct = 8;
                }
                else
                {
                    _pos++;
                    _c += next << 9;
                    _ct = 7;
                }
            }
            else
            {
                _pos++;
                _c += next << 8;
                _ct = 8;
            }
        }

        private void Renormalize()
        {
            do
            {
                if (_ct == 0)
                {
                    ByteIn();
                }

                _a <<= 1;
                _c <<= 1;
                _ct--;
            }
            while ((_a & 0x8000) == 0);
        }
    }

    /// <summary>Reads the raw (selective arithmetic coding bypass) bits of a codeword segment.</summary>
    internal sealed class RawBitReader
    {
        private byte[] _data = [];
        private int _pos;
        private int _end;
        private int _c;
        private int _ct;

        /// <summary>Starts reading a raw segment.</summary>
        /// <param name="data">The buffer holding the segment.</param>
        /// <param name="start">The first byte of the segment.</param>
        /// <param name="length">The segment length in bytes.</param>
        public void Init(byte[] data, int start, int length)
        {
            _data = data;
            _pos = start;
            _end = start + length;
            _c = 0;
            _ct = 0;
        }

        /// <summary>Reads one raw bit; reading past the segment yields the 0xFF fill.</summary>
        /// <returns>The bit.</returns>
        public int ReadBit()
        {
            if (_ct == 0)
            {
                // A byte following 0xFF only has seven payload bits.
                var next = _pos < _end ? _data[_pos] : 0xFF;
                if (_c == 0xFF)
                {
                    if (next > 0x8F)
                    {
                        _c = 0xFF;
                        _ct = 8;
                    }
                    else
                    {
                        _c = next;
                        _pos++;
                        _ct = 7;
                    }
                }
                else
                {
                    _c = next;
                    if (_pos < _end)
                    {
                        _pos++;
                    }

                    _ct = 8;
                }
            }

            _ct--;
            return (_c >> _ct) & 1;
        }
    }
}

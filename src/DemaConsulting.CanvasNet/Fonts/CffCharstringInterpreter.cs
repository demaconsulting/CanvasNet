// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO CFF charstring charstrings subr subrs
// cspell:ignore hintmask cntrmask rmoveto hmoveto vmoveto rlineto hlineto vlineto
// cspell:ignore rrcurveto hhcurveto vvcurveto hvcurveto vhcurveto callsubr callgsubr
// cspell:ignore endchar seac hstem vstem hstemhm vstemhm rcurveline rlinecurve
using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Executes a single glyph's Type 2 charstring bytecode (as defined by the CFF/Type 2
///     Charstring Format specification) into <see cref="Path"/> geometry.
/// </summary>
/// <remarks>
///     <para>
///     Supports exactly this operator set: stem hints (<c>hstem</c>/<c>vstem</c>/<c>hstemhm</c>/
///     <c>vstemhm</c>), hint masks (<c>hintmask</c>/<c>cntrmask</c>), moves (<c>rmoveto</c>/
///     <c>hmoveto</c>/<c>vmoveto</c>), lines (<c>rlineto</c>/<c>hlineto</c>/<c>vlineto</c>),
///     curves (<c>rrcurveto</c>/<c>hhcurveto</c>/<c>vvcurveto</c>/<c>hvcurveto</c>/<c>vhcurveto</c>),
///     subroutine calls (<c>callsubr</c>/<c>callgsubr</c>/<c>return</c>), and <c>endchar</c>. Every
///     other operator - including the two-byte escape operators (flex, arithmetic/logical, and
///     other Type 1 heritage operators) and the deprecated 4-operand <c>seac</c>-style accent
///     composition form of <c>endchar</c> - is unsupported and rejected with
///     <see cref="InvalidDataException"/> rather than silently ignored or mis-decoded.
///     </para>
///     <para>
///     Stem hints only affect hint execution (out of scope for this library, which never rasterizes
///     via hinting) - their operands are consumed purely to keep the operand stack correctly
///     positioned and to compute the correct <c>hintmask</c>/<c>cntrmask</c> byte count from the
///     running total of declared stem hints (including any implicit trailing <c>vstem</c> group
///     immediately preceding a mask operator).
///     </para>
///     <para>
///     <c>callsubr</c>/<c>callgsubr</c> nesting is bounded by <see cref="MaxCallDepth"/>, and the
///     total number of charstring tokens (operands and operators, across every subroutine call
///     within one <see cref="Decode"/> invocation) is bounded by <see cref="MaxOperatorSteps"/> -
///     mirroring <see cref="GlyfLocaReader"/>'s depth/total-count precedent for bounding malicious
///     or pathological font data deterministically rather than by a timer.
///     </para>
/// </remarks>
internal static class CffCharstringInterpreter
{
    /// <summary>
    ///     The maximum <c>callsubr</c>/<c>callgsubr</c> nesting depth permitted before
    ///     <see cref="InvalidDataException"/> is thrown.
    /// </summary>
    private const int MaxCallDepth = 10;

    /// <summary>
    ///     The maximum total number of charstring tokens (numeric operands and operators)
    ///     permitted across a single <see cref="Decode"/> call, including every subroutine
    ///     invoked - bounding pathological/malicious subroutine call patterns deterministically.
    /// </summary>
    private const int MaxOperatorSteps = 200_000;

    /// <summary>
    ///     The maximum operand stack depth permitted before <see cref="InvalidDataException"/> is
    ///     thrown - generously above the Type 2 specification's nominal 48-operand limit to
    ///     tolerate any well-formed charstring using only this type's supported operator set.
    /// </summary>
    private const int MaxStackSize = 96;

    /// <summary>
    ///     Decodes a single glyph's Type 2 charstring bytecode into <see cref="Path"/> geometry.
    /// </summary>
    /// <param name="data">
    ///     The CFF table's own byte array (see <see cref="CffTable"/>'s remarks) - every
    ///     <c>Offset</c> in <paramref name="charstring"/>, <paramref name="globalSubrs"/>, and
    ///     <paramref name="localSubrs"/> is relative to this array's start.
    /// </param>
    /// <param name="charstring">The target glyph's own charstring byte range.</param>
    /// <param name="globalSubrs">Every global subroutine's byte range, in index order.</param>
    /// <param name="localSubrs">Every local subroutine's byte range, in index order.</param>
    /// <returns>The decoded glyph outline, or <see cref="Path.Empty"/> for an empty charstring.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the charstring bytecode is malformed or truncated, uses an operand count an
    ///     operator does not accept, uses an unsupported operator (including any two-byte escape
    ///     operator), uses the deprecated 4-operand <c>seac</c>-style form of <c>endchar</c>,
    ///     references an out-of-range subroutine index, or exceeds the call-depth or
    ///     operator-step bound.
    /// </exception>
    public static Path Decode(
        byte[] data,
        (int Offset, int Length) charstring,
        IReadOnlyList<(int Offset, int Length)> globalSubrs,
        IReadOnlyList<(int Offset, int Length)> localSubrs)
    {
        var state = new InterpreterState(data, globalSubrs, localSubrs);
        state.Run(charstring, 0);
        state.CloseIfOpen();
        return state.Builder.Build();
    }

    /// <summary>
    ///     Holds the mutable interpreter state (operand stack, current point, stem-hint count,
    ///     width-parsed flag, and resource-bound counters) shared across every nested
    ///     <see cref="Run"/> call for a single <see cref="Decode"/> invocation.
    /// </summary>
    private sealed class InterpreterState
    {
        private readonly byte[] _data;
        private readonly IReadOnlyList<(int Offset, int Length)> _globalSubrs;
        private readonly IReadOnlyList<(int Offset, int Length)> _localSubrs;
        private readonly int _globalBias;
        private readonly int _localBias;
        private readonly List<double> _stack = [];
        private int _stems;
        private bool _widthParsed;
        private bool _hasOpenPath;
        private bool _done;
        private double _x;
        private double _y;
        private int _steps;

        public PathBuilder Builder { get; } = new();

        public InterpreterState(
            byte[] data,
            IReadOnlyList<(int Offset, int Length)> globalSubrs,
            IReadOnlyList<(int Offset, int Length)> localSubrs)
        {
            _data = data;
            _globalSubrs = globalSubrs;
            _localSubrs = localSubrs;
            _globalBias = Bias(globalSubrs.Count);
            _localBias = Bias(localSubrs.Count);
        }

        /// <summary>
        ///     Computes the subroutine index bias applied by <c>callsubr</c>/<c>callgsubr</c>,
        ///     per the Type 2 specification: <c>107</c> for fewer than 1240 subroutines,
        ///     <c>1131</c> for fewer than 33900, otherwise <c>32768</c>.
        /// </summary>
        private static int Bias(int count)
        {
            if (count < 1240)
            {
                return 107;
            }

            return count < 33900 ? 1131 : 32768;
        }

        /// <summary>
        ///     Executes one charstring's (or subroutine's) bytecode range, dispatching every
        ///     numeric operand and operator until <c>return</c>, <c>endchar</c>, or the end of the
        ///     byte range is reached.
        /// </summary>
        public void Run((int Offset, int Length) range, int depth)
        {
            if (_done)
            {
                return;
            }

            if (depth > MaxCallDepth)
            {
                throw new InvalidDataException("CFF charstring subroutine call nesting exceeds the supported depth.");
            }

            var pos = range.Offset;
            var end = range.Offset + range.Length;
            while (pos < end && !_done)
            {
                Step();
                var b0 = _data[pos];
                if (b0 >= 32 || b0 == 28)
                {
                    pos = ReadNumber(pos);
                    continue;
                }

                if (b0 == 12)
                {
                    if (pos + 1 >= end)
                    {
                        throw new InvalidDataException("CFF charstring escape operator is truncated.");
                    }

                    throw new InvalidDataException($"Unsupported CFF charstring escape operator (12 {_data[pos + 1]}).");
                }

                pos++;
                switch (b0)
                {
                    case 1:
                    case 3:
                    case 18:
                    case 23: // hstem, vstem, hstemhm, vstemhm
                        HandleStems();
                        break;

                    case 19:
                    case 20: // hintmask, cntrmask
                        HandleStems();
                        var maskBytes = (_stems + 7) / 8;
                        if (pos + maskBytes > end)
                        {
                            throw new InvalidDataException("CFF charstring hint mask is truncated.");
                        }

                        pos += maskBytes;
                        break;

                    case 21: HandleRMoveTo(); break;
                    case 22: HandleAxisMoveTo(isX: true); break; // hmoveto
                    case 4: HandleAxisMoveTo(isX: false); break; // vmoveto
                    case 5: HandleRLineTo(); break;
                    case 6: HandleAltLineTo(startHorizontal: true); break; // hlineto
                    case 7: HandleAltLineTo(startHorizontal: false); break; // vlineto
                    case 8: HandleRRCurveTo(); break;
                    case 26: HandleVVCurveTo(); break;
                    case 27: HandleHHCurveTo(); break;
                    case 31: HandleAltCurveTo(startHorizontal: true); break; // hvcurveto
                    case 30: HandleAltCurveTo(startHorizontal: false); break; // vhcurveto
                    case 10: CallSubr(local: true, depth); break; // callsubr
                    case 29: CallSubr(local: false, depth); break; // callgsubr
                    case 11: return; // return
                    case 14: // endchar
                        HandleEndChar();
                        _done = true;
                        break;

                    default:
                        throw new InvalidDataException($"Unsupported CFF charstring operator ({b0}).");
                }
            }
        }

        /// <summary>
        ///     Closes the currently open subpath (if any) - called once after the outermost
        ///     <see cref="Run"/> call returns, so a charstring that never reaches <c>endchar</c>
        ///     (malformed, or simply truncated at the byte range's end) still yields a
        ///     well-formed closed path for whatever contour data it did produce.
        /// </summary>
        public void CloseIfOpen()
        {
            if (_hasOpenPath)
            {
                Builder.Close();
                _hasOpenPath = false;
            }
        }

        /// <summary>
        ///     Charges one unit against the total operator-step budget, throwing once
        ///     <see cref="MaxOperatorSteps"/> is exceeded.
        /// </summary>
        private void Step()
        {
            _steps++;
            if (_steps > MaxOperatorSteps)
            {
                throw new InvalidDataException("CFF charstring execution exceeds the supported operator budget.");
            }
        }

        /// <summary>
        ///     Reads one Type 2 numeric operand token starting at <paramref name="pos"/> and
        ///     pushes it onto the operand stack.
        /// </summary>
        private int ReadNumber(int pos)
        {
            var b0 = _data[pos];
            double value;
            int consumed;
            if (b0 == 28)
            {
                if (pos + 3 > _data.Length)
                {
                    throw new InvalidDataException("CFF charstring numeric operand is truncated.");
                }

                value = SfntContainer.ReadInt16(_data, pos + 1);
                consumed = 3;
            }
            else if (b0 == 255)
            {
                if (pos + 5 > _data.Length)
                {
                    throw new InvalidDataException("CFF charstring numeric operand is truncated.");
                }

                value = SfntContainer.ReadInt32(_data, pos + 1) / 65536.0;
                consumed = 5;
            }
            else if (b0 is >= 32 and <= 246)
            {
                value = b0 - 139;
                consumed = 1;
            }
            else if (b0 is >= 247 and <= 250)
            {
                if (pos + 2 > _data.Length)
                {
                    throw new InvalidDataException("CFF charstring numeric operand is truncated.");
                }

                value = ((b0 - 247) * 256) + _data[pos + 1] + 108;
                consumed = 2;
            }
            else if (b0 is >= 251 and <= 254)
            {
                if (pos + 2 > _data.Length)
                {
                    throw new InvalidDataException("CFF charstring numeric operand is truncated.");
                }

                value = (-(b0 - 251) * 256) - _data[pos + 1] - 108;
                consumed = 2;
            }
            else
            {
                throw new InvalidDataException($"Unexpected CFF charstring byte (0x{b0:X2}) where a numeric operand or operator was expected.");
            }

            if (_stack.Count >= MaxStackSize)
            {
                throw new InvalidDataException("CFF charstring operand stack exceeds the supported size.");
            }

            _stack.Add(value);
            return pos + consumed;
        }

        /// <summary>
        ///     Consumes a leading width operand, if present, the first time any stack-clearing
        ///     operator executes - the Type 2 format allows a glyph's advance width to be encoded
        ///     as one extra leading operand on the very first such operator only.
        /// </summary>
        private void ConsumeOptionalWidth(int expectedCount)
        {
            if (_widthParsed)
            {
                return;
            }

            if (_stack.Count == expectedCount + 1)
            {
                _stack.RemoveAt(0);
            }

            _widthParsed = true;
        }

        /// <summary>
        ///     Handles <c>hstem</c>/<c>vstem</c>/<c>hstemhm</c>/<c>vstemhm</c>: consumes the
        ///     operand stack in stem-hint pairs (an odd leftover operand on the first
        ///     stack-clearing operator is the optional width), accumulating the running stem
        ///     count used by <c>hintmask</c>/<c>cntrmask</c>.
        /// </summary>
        private void HandleStems()
        {
            var count = _stack.Count;
            if (!_widthParsed)
            {
                if (count % 2 != 0)
                {
                    _stack.RemoveAt(0);
                    count--;
                }

                _widthParsed = true;
            }
            else if (count % 2 != 0)
            {
                throw new InvalidDataException("CFF charstring stem-hint operator has an odd operand count.");
            }

            _stems += count / 2;
            _stack.Clear();
        }

        private void HandleRMoveTo()
        {
            ConsumeOptionalWidth(2);
            if (_stack.Count != 2)
            {
                throw new InvalidDataException("CFF charstring 'rmoveto' requires exactly two operands.");
            }

            CloseIfOpen();
            _x += _stack[0];
            _y += _stack[1];
            Builder.MoveTo(CurrentPoint());
            _hasOpenPath = true;
            _stack.Clear();
        }

        private void HandleAxisMoveTo(bool isX)
        {
            ConsumeOptionalWidth(1);
            if (_stack.Count != 1)
            {
                throw new InvalidDataException("CFF charstring 'hmoveto'/'vmoveto' requires exactly one operand.");
            }

            CloseIfOpen();
            if (isX)
            {
                _x += _stack[0];
            }
            else
            {
                _y += _stack[0];
            }

            Builder.MoveTo(CurrentPoint());
            _hasOpenPath = true;
            _stack.Clear();
        }

        private void HandleRLineTo()
        {
            if (_stack.Count == 0 || _stack.Count % 2 != 0)
            {
                throw new InvalidDataException("CFF charstring 'rlineto' requires a positive, even operand count.");
            }

            for (var i = 0; i < _stack.Count; i += 2)
            {
                _x += _stack[i];
                _y += _stack[i + 1];
                Builder.LineTo(CurrentPoint());
            }

            _stack.Clear();
        }

        private void HandleAltLineTo(bool startHorizontal)
        {
            var horizontal = startHorizontal;
            foreach (var value in _stack)
            {
                if (horizontal)
                {
                    _x += value;
                }
                else
                {
                    _y += value;
                }

                Builder.LineTo(CurrentPoint());
                horizontal = !horizontal;
            }

            _stack.Clear();
        }

        private void HandleRRCurveTo()
        {
            if (_stack.Count == 0 || _stack.Count % 6 != 0)
            {
                throw new InvalidDataException("CFF charstring 'rrcurveto' requires a positive multiple-of-six operand count.");
            }

            for (var i = 0; i < _stack.Count; i += 6)
            {
                var c1X = _x + _stack[i];
                var c1Y = _y + _stack[i + 1];
                var c2X = c1X + _stack[i + 2];
                var c2Y = c1Y + _stack[i + 3];
                _x = c2X + _stack[i + 4];
                _y = c2Y + _stack[i + 5];
                Builder.CubicBezierTo(Pt(c1X, c1Y), Pt(c2X, c2Y), CurrentPoint());
            }

            _stack.Clear();
        }

        private void HandleVVCurveTo()
        {
            var index = 0;
            double dx1 = 0;
            var remaining = _stack.Count;
            if (remaining % 4 == 1)
            {
                dx1 = _stack[0];
                index = 1;
                remaining--;
            }

            if (remaining == 0 || remaining % 4 != 0)
            {
                throw new InvalidDataException("CFF charstring 'vvcurveto' has an unexpected operand count.");
            }

            var first = true;
            while (index < _stack.Count)
            {
                var c1X = _x + (first ? dx1 : 0);
                var c1Y = _y + _stack[index];
                var c2X = c1X + _stack[index + 1];
                var c2Y = c1Y + _stack[index + 2];
                _x = c2X;
                _y = c2Y + _stack[index + 3];
                Builder.CubicBezierTo(Pt(c1X, c1Y), Pt(c2X, c2Y), CurrentPoint());
                index += 4;
                first = false;
            }

            _stack.Clear();
        }

        private void HandleHHCurveTo()
        {
            var index = 0;
            double dy1 = 0;
            var remaining = _stack.Count;
            if (remaining % 4 == 1)
            {
                dy1 = _stack[0];
                index = 1;
                remaining--;
            }

            if (remaining == 0 || remaining % 4 != 0)
            {
                throw new InvalidDataException("CFF charstring 'hhcurveto' has an unexpected operand count.");
            }

            var first = true;
            while (index < _stack.Count)
            {
                var c1X = _x + _stack[index];
                var c1Y = _y + (first ? dy1 : 0);
                var c2X = c1X + _stack[index + 1];
                var c2Y = c1Y + _stack[index + 2];
                _x = c2X + _stack[index + 3];
                _y = c2Y;
                Builder.CubicBezierTo(Pt(c1X, c1Y), Pt(c2X, c2Y), CurrentPoint());
                index += 4;
                first = false;
            }

            _stack.Clear();
        }

        /// <summary>
        ///     Handles <c>hvcurveto</c>/<c>vhcurveto</c>: a sequence of curves whose starting
        ///     tangent direction alternates (horizontal, vertical, horizontal, ...) each curve,
        ///     with the very last curve in the sequence optionally carrying one extra operand for
        ///     its otherwise-implicit final-axis offset.
        /// </summary>
        private void HandleAltCurveTo(bool startHorizontal)
        {
            var count = _stack.Count;
            if (count < 4 || (count % 4 != 0 && count % 4 != 1))
            {
                throw new InvalidDataException("CFF charstring 'hvcurveto'/'vhcurveto' has an unexpected operand count.");
            }

            var horizontal = startHorizontal;
            var index = 0;
            while (index + 4 <= count)
            {
                var isLast = index + 4 == count - 1;
                double c1X, c1Y, c2X, c2Y;
                if (horizontal)
                {
                    c1X = _x + _stack[index];
                    c1Y = _y;
                    c2X = c1X + _stack[index + 1];
                    c2Y = c1Y + _stack[index + 2];
                    _y = c2Y + _stack[index + 3];
                    _x = isLast ? c2X + _stack[index + 4] : c2X;
                }
                else
                {
                    c1X = _x;
                    c1Y = _y + _stack[index];
                    c2X = c1X + _stack[index + 1];
                    c2Y = c1Y + _stack[index + 2];
                    _x = c2X + _stack[index + 3];
                    _y = isLast ? c2Y + _stack[index + 4] : c2Y;
                }

                Builder.CubicBezierTo(Pt(c1X, c1Y), Pt(c2X, c2Y), CurrentPoint());
                horizontal = !horizontal;
                index += 4;
            }

            _stack.Clear();
        }

        private void CallSubr(bool local, int depth)
        {
            if (_stack.Count == 0)
            {
                throw new InvalidDataException("CFF charstring 'callsubr'/'callgsubr' is missing its subroutine index operand.");
            }

            var index = (int)_stack[^1];
            _stack.RemoveAt(_stack.Count - 1);

            var subrs = local ? _localSubrs : _globalSubrs;
            var bias = local ? _localBias : _globalBias;
            var selected = index + bias;
            if (selected < 0 || selected >= subrs.Count)
            {
                throw new InvalidDataException("CFF charstring subroutine index is out of range.");
            }

            Run(subrs[selected], depth + 1);
        }

        /// <summary>
        ///     Handles <c>endchar</c>: an optional leading width, followed by either zero
        ///     operands (the normal case) or exactly four operands (the deprecated
        ///     <c>seac</c>-style accent composition form, rejected with
        ///     <see cref="InvalidDataException"/> rather than silently ignoring the accent).
        /// </summary>
        private void HandleEndChar()
        {
            var count = _stack.Count;
            if (!_widthParsed)
            {
                if (count == 1 || count == 5)
                {
                    _stack.RemoveAt(0);
                    count--;
                }

                _widthParsed = true;
            }

            if (count == 4)
            {
                throw new InvalidDataException(
                    "CFF charstring 'endchar' with the deprecated 4-operand seac-style accent composition form is not supported.");
            }

            if (count != 0)
            {
                throw new InvalidDataException("CFF charstring 'endchar' has an unexpected operand count.");
            }

            CloseIfOpen();
            _stack.Clear();
        }

        private Vector2 CurrentPoint() => Pt(_x, _y);

        private static Vector2 Pt(double x, double y) => new((float)x, (float)y);
    }
}

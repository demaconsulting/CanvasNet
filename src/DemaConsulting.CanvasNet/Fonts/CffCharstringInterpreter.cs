// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO CFF charstring charstrings subr subrs
// cspell:ignore hintmask cntrmask rmoveto hmoveto vmoveto rlineto hlineto vlineto
// cspell:ignore rrcurveto hhcurveto vvcurveto hvcurveto vhcurveto callsubr callgsubr
// cspell:ignore endchar seac hstem vstem hstemhm vstemhm rcurveline rlinecurve bchar achar adx ady
using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Executes a single glyph's Type 2 charstring bytecode (as defined by the CFF/Type 2
///     Charstring Format specification) into <see cref="Path"/> geometry, alongside the glyph's
///     resolved advance width.
/// </summary>
/// <remarks>
///     <para>
///     Supports exactly this operator set: stem hints (<c>hstem</c>/<c>vstem</c>/<c>hstemhm</c>/
///     <c>vstemhm</c>), hint masks (<c>hintmask</c>/<c>cntrmask</c>), moves (<c>rmoveto</c>/
///     <c>hmoveto</c>/<c>vmoveto</c>), lines (<c>rlineto</c>/<c>hlineto</c>/<c>vlineto</c>),
///     curves (<c>rrcurveto</c>/<c>hhcurveto</c>/<c>vvcurveto</c>/<c>hvcurveto</c>/<c>vhcurveto</c>),
///     subroutine calls (<c>callsubr</c>/<c>callgsubr</c>/<c>return</c>), and <c>endchar</c> -
///     including its deprecated 4-operand <c>seac</c>-style accent composition form (<c>adx ady
///     bchar achar endchar</c>), which this class composes when a
///     <c>resolveStandardEncodedGlyph</c> callback is supplied to <see cref="Decode"/>
///     (see <see cref="Decode"/>'s remarks). Every other operator - including the two-byte escape
///     operators (flex, arithmetic/logical, and other Type 1 heritage operators) - is unsupported
///     and rejected with <see cref="InvalidDataException"/> rather than silently ignored or
///     mis-decoded.
///     </para>
///     <para>
///     Stem hints only affect hint execution (out of scope for this library, which never rasterizes
///     via hinting) - their operands are consumed purely to keep the operand stack correctly
///     positioned and to compute the correct <c>hintmask</c>/<c>cntrmask</c> byte count from the
///     running total of declared stem hints (including any implicit trailing <c>vstem</c> group
///     immediately preceding a mask operator).
///     </para>
///     <para>
///     Per the Type 2 Charstring Format specification's own width convention, a glyph's charstring
///     may carry one extra leading operand - on whichever stack-clearing operator executes first
///     (a stem-hint operator, a move operator, or <c>endchar</c>) - encoding its advance width as
///     a delta from the font's Private DICT <c>nominalWidthX</c> operand; if no such extra operand
///     is present, the glyph's width is instead the Private DICT's <c>defaultWidthX</c> operand.
///     <see cref="Decode"/> resolves this directly, returning the glyph's final width alongside
///     its outline, so callers (see <see cref="CffTable.GetAdvanceWidth"/>) never need to
///     replicate this convention themselves.
///     </para>
///     <para>
///     <c>callsubr</c>/<c>callgsubr</c> nesting is bounded by <see cref="MaxCallDepth"/>, and the
///     total number of charstring tokens (operands and operators, across every subroutine call
///     within one <see cref="Decode"/> invocation) is bounded by <see cref="MaxOperatorSteps"/> -
///     mirroring <see cref="GlyfLocaReader"/>'s depth/total-count precedent for bounding malicious
///     or pathological font data deterministically rather than by a timer.
///     </para>
///     <para>
///     The deprecated <c>seac</c>-style 4-operand form of <c>endchar</c> (<c>adx ady bchar achar
///     endchar</c>) composes a "base" glyph and an "accent" glyph - both identified by Adobe
///     StandardEncoding code, resolved to a glyph name via <see cref="CffStandardEncoding"/> and
///     then to a glyph index in the <em>same</em> font - into one outline: the accent's own
///     outline is translated by <c>(adx, ady)</c> (<see cref="Path.Transform"/>) and combined with
///     the base's own untranslated outline. This class has no knowledge of any font's charset or
///     glyph-name resolution, so it cannot perform that resolution itself; instead,
///     <see cref="Decode"/> accepts an optional <c>resolveStandardEncodedGlyph</c>
///     callback mapping a StandardEncoding code to that code's resolved glyph's own already-
///     decoded outline. When this callback is <see langword="null"/> - including, deliberately,
///     when <see cref="CffTable"/> recursively decodes a seac component's own charstring (see
///     <see cref="CffTable.GetGlyphOutline"/>'s remarks) - a seac-style <c>endchar</c> is rejected
///     with <see cref="InvalidDataException"/> exactly as before, which is what naturally rejects
///     a doubly-nested seac composition without needing a separate depth counter: the component
///     decode's own resolver argument is simply never supplied.
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
    ///     Decodes a single glyph's Type 2 charstring bytecode into <see cref="Path"/> geometry
    ///     and its resolved advance width.
    /// </summary>
    /// <param name="data">
    ///     The CFF table's own byte array (see <see cref="CffTable"/>'s remarks) - every
    ///     <c>Offset</c> in <paramref name="charstring"/>, <paramref name="globalSubrs"/>, and
    ///     <paramref name="localSubrs"/> is relative to this array's start.
    /// </param>
    /// <param name="charstring">The target glyph's own charstring byte range.</param>
    /// <param name="globalSubrs">Every global subroutine's byte range, in index order.</param>
    /// <param name="localSubrs">Every local subroutine's byte range, in index order.</param>
    /// <param name="defaultWidthX">
    ///     The font's Private DICT <c>defaultWidthX</c> operand (<c>0</c> if absent) - the
    ///     glyph's resolved width when its charstring carries no leading width operand.
    /// </param>
    /// <param name="nominalWidthX">
    ///     The font's Private DICT <c>nominalWidthX</c> operand (<c>0</c> if absent) - added to
    ///     the glyph's charstring-encoded width delta when one is present.
    /// </param>
    /// <param name="resolveStandardEncodedGlyph">
    ///     When supplied, resolves an Adobe StandardEncoding code to that code's glyph's own
    ///     already-decoded <see cref="Path"/> outline - enabling this call's charstring to use the
    ///     deprecated 4-operand <c>seac</c>-style accent composition form of <c>endchar</c> (see
    ///     this class's remarks). When <see langword="null"/> (the default), a seac-style
    ///     <c>endchar</c> is rejected with <see cref="InvalidDataException"/> - this is also how
    ///     <see cref="CffTable"/> naturally rejects a doubly-nested seac composition, by omitting
    ///     this argument on a component glyph's own recursive <see cref="Decode"/> call.
    /// </param>
    /// <returns>
    ///     The decoded glyph outline (<see cref="Path.Empty"/> for an empty charstring, or the
    ///     composed base+accent outline for a seac-style <c>endchar</c>) alongside the glyph's
    ///     resolved advance width - see this class's remarks for the exact width resolution
    ///     convention (a seac-style glyph's width still follows that same convention, never the
    ///     base/accent components' own widths).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the charstring bytecode is malformed or truncated, uses an operand count an
    ///     operator does not accept, uses an unsupported operator (including any two-byte escape
    ///     operator), uses the deprecated 4-operand <c>seac</c>-style form of <c>endchar</c> with
    ///     no <paramref name="resolveStandardEncodedGlyph"/> supplied, references an out-of-range
    ///     subroutine index, or exceeds the call-depth or operator-step bound.
    /// </exception>
    public static (Path Outline, double Width) Decode(
        byte[] data,
        (int Offset, int Length) charstring,
        IReadOnlyList<(int Offset, int Length)> globalSubrs,
        IReadOnlyList<(int Offset, int Length)> localSubrs,
        double defaultWidthX = 0,
        double nominalWidthX = 0,
        Func<int, Path>? resolveStandardEncodedGlyph = null)
    {
        var state = new InterpreterState(data, globalSubrs, localSubrs, resolveStandardEncodedGlyph);
        state.Run(charstring, 0);
        state.CloseIfOpen();
        var width = state.WidthDelta.HasValue ? nominalWidthX + state.WidthDelta.Value : defaultWidthX;
        return (state.SeacOutline ?? state.Builder.Build(), width);
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
        private readonly Func<int, Path>? _resolveStandardEncodedGlyph;
        private readonly int _globalBias;
        private readonly int _localBias;
        private readonly List<double> _stack = [];
        private int _stems;
        private bool _widthParsed;
        private double? _widthDelta;
        private bool _hasOpenPath;
        private bool _done;
        private double _x;
        private double _y;
        private int _steps;

        public PathBuilder Builder { get; } = new();

        /// <summary>
        ///     The composed base+accent outline produced by a seac-style <c>endchar</c> (see
        ///     <see cref="HandleSeacEndChar"/>), or <see langword="null"/> if this charstring
        ///     used the normal <c>endchar</c> form - in which case <see cref="Decode"/> instead
        ///     builds the outline from <see cref="Builder"/> as usual.
        /// </summary>
        public Path? SeacOutline { get; private set; }

        /// <summary>
        ///     The charstring's leading width operand's raw value, if one was present (see
        ///     <see cref="ConsumeOptionalWidth"/>/<see cref="HandleStems"/>/
        ///     <see cref="HandleEndChar"/>), or <see langword="null"/> if the charstring had none
        ///     - in which case <see cref="Decode"/> resolves the glyph's width to
        ///     <c>defaultWidthX</c> rather than <c>nominalWidthX + WidthDelta</c>.
        /// </summary>
        public double? WidthDelta => _widthDelta;

        public InterpreterState(
            byte[] data,
            IReadOnlyList<(int Offset, int Length)> globalSubrs,
            IReadOnlyList<(int Offset, int Length)> localSubrs,
            Func<int, Path>? resolveStandardEncodedGlyph)
        {
            _data = data;
            _globalSubrs = globalSubrs;
            _localSubrs = localSubrs;
            _resolveStandardEncodedGlyph = resolveStandardEncodedGlyph;
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
                _widthDelta = _stack[0];
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
                    _widthDelta = _stack[0];
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
        ///     operands (the normal case, closing and finishing the outline built so far) or
        ///     exactly four operands (the deprecated <c>seac</c>-style accent composition form -
        ///     see <see cref="HandleSeacEndChar"/>).
        /// </summary>
        private void HandleEndChar()
        {
            var count = _stack.Count;
            if (!_widthParsed)
            {
                if (count == 1 || count == 5)
                {
                    _widthDelta = _stack[0];
                    _stack.RemoveAt(0);
                    count--;
                }

                _widthParsed = true;
            }

            if (count == 4)
            {
                HandleSeacEndChar();
                return;
            }

            if (count != 0)
            {
                throw new InvalidDataException("CFF charstring 'endchar' has an unexpected operand count.");
            }

            CloseIfOpen();
            _stack.Clear();
        }

        /// <summary>
        ///     Handles the deprecated 4-operand <c>seac</c>-style form of <c>endchar</c> (<c>adx
        ///     ady bchar achar endchar</c>): composes a "base" glyph (<c>bchar</c>) and an
        ///     "accent" glyph (<c>achar</c>) - both identified by Adobe StandardEncoding code via
        ///     <see cref="_resolveStandardEncodedGlyph"/> - into <see cref="SeacOutline"/>, with
        ///     the accent translated by <c>(adx, ady)</c> and combined with the base's own
        ///     untranslated outline.
        /// </summary>
        /// <exception cref="InvalidDataException">
        ///     Thrown when <see cref="_resolveStandardEncodedGlyph"/> is <see langword="null"/> -
        ///     covering both "no resolver supplied at all" (the normal top-level rejection) and,
        ///     critically, "this charstring is itself a seac component being decoded without a
        ///     resolver" (<see cref="CffTable"/> deliberately omits one for that recursive call),
        ///     which is what naturally rejects a doubly-nested seac composition.
        /// </exception>
        private void HandleSeacEndChar()
        {
            if (_resolveStandardEncodedGlyph is null)
            {
                throw new InvalidDataException(
                    "CFF charstring 'endchar' with the deprecated 4-operand seac-style accent composition form requires a StandardEncoding glyph resolver, which is not available here (either no resolver was supplied, or this charstring is itself a seac component - nested seac composition is not supported).");
            }

            var adx = _stack[0];
            var ady = _stack[1];
            var bchar = (int)_stack[2];
            var achar = (int)_stack[3];
            _stack.Clear();

            var baseOutline = _resolveStandardEncodedGlyph(bchar);
            var accentOutline = _resolveStandardEncodedGlyph(achar);
            var translatedAccent = accentOutline.Transform(Matrix3x2.CreateTranslation((float)adx, (float)ady));

            SeacOutline = CombineOutlines(baseOutline, translatedAccent);
            CloseIfOpen();
        }

        /// <summary>
        ///     Combines two already-decoded outlines into one <see cref="Path"/> by concatenating
        ///     their <see cref="Path.Subpaths"/> lists - the seac-style composite's final outline
        ///     is simply the base glyph's subpaths followed by the (already translated) accent
        ///     glyph's subpaths, with no further transform or fill-rule adjustment needed, the
        ///     same subpath-list-concatenation principle <see cref="GlyfLocaReader"/> applies when
        ///     combining a composite <c>glyf</c> glyph's components.
        /// </summary>
        private static Path CombineOutlines(Path first, Path second)
        {
            if (first.Subpaths.Count == 0)
            {
                return second;
            }

            if (second.Subpaths.Count == 0)
            {
                return first;
            }

            var combined = new List<Subpath>(first.Subpaths.Count + second.Subpaths.Count);
            combined.AddRange(first.Subpaths);
            combined.AddRange(second.Subpaths);
            return new Path(combined);
        }

        private Vector2 CurrentPoint() => Pt(_x, _y);

        private static Vector2 Pt(double x, double y) => new((float)x, (float)y);
    }
}

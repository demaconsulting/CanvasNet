// cspell:ignore charstring charstrings subr subrs hsbw rmoveto hmoveto vmoveto rlineto hlineto
// cspell:ignore vlineto rrcurveto vhcurveto hvcurveto callsubr callothersubr dotsection vstem
// cspell:ignore hstem endchar seac setcurrentpoint othersubr othersubrs
using System.Globalization;
using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Executes a single glyph's classic PostScript Type 1 charstring bytecode (as defined by the
///     Adobe Type 1 Font Format specification) into <see cref="Path"/> geometry.
/// </summary>
/// <remarks>
///     <para>
///     Supports the complete Type 1 operator set: stem hints (<c>hstem</c>/<c>vstem</c>/
///     <c>hstem3</c>/<c>vstem3</c>) and <c>dotsection</c> (consumed and discarded - this library
///     never rasterizes via hinting); the side-bearing/width operators <c>hsbw</c>/<c>sbw</c>;
///     moves (<c>rmoveto</c>/<c>hmoveto</c>/<c>vmoveto</c>); lines (<c>rlineto</c>/<c>hlineto</c>/
///     <c>vlineto</c>); curves (<c>rrcurveto</c>/<c>hvcurveto</c>/<c>vhcurveto</c>);
///     <c>closepath</c>; subroutine calls (<c>callsubr</c>/<c>return</c> - unlike Type 2, Type 1
///     subroutine indices carry no bias); <c>div</c>; the "OtherSubrs" mechanism
///     (<c>callothersubr</c>/<c>pop</c>/<c>setcurrentpoint</c>), including real flex geometry
///     (OtherSubrs 0/1/2) and hint replacement (OtherSubrs 3, a transparent pass-through); and
///     <c>endchar</c>. <c>seac</c> (accented composite glyph construction) is explicitly rejected
///     with <see cref="InvalidDataException"/> - a documented Non-Goal.
///     </para>
///     <para>
///     Unlike <see cref="CffCharstringInterpreter"/> (which always discards its optional leading
///     width operand, since CFF/OpenType fonts supply advance width independently via <c>hmtx</c>),
///     <see cref="Decode"/> <em>returns</em> the glyph's width: a Type 1 font has no <c>hmtx</c>
///     equivalent, so its own <c>hsbw</c>/<c>sbw</c> operator is the only source of that value.
///     </para>
///     <para>
///     Type 1's "flex" mechanism replaces what would otherwise be two <c>rrcurveto</c> path
///     segments with a sequence of seven <c>rmoveto</c> calls bracketed by OtherSubrs 1 ("begin
///     flex") and OtherSubrs 0 ("end flex") calls (with an OtherSubrs 2 "point mark" call after
///     each <c>rmoveto</c>). While flexing, every <c>rmoveto</c>/<c>hmoveto</c>/<c>vmoveto</c>
///     delta is buffered as an absolute point rather than emitted as a real path move. On "end
///     flex", the first buffered point (a reference point used only by a real PostScript
///     interpreter to decide whether to actually flex, never part of the final outline) is
///     discarded, and the remaining six buffered points become the two control points and end
///     point of each of two <c>rrcurveto</c>-equivalent cubic Bezier segments.
///     </para>
///     <para>
///     <c>callsubr</c> nesting is bounded by <see cref="MaxCallDepth"/>, and the total number of
///     charstring tokens executed (across every subroutine call within one <see cref="Decode"/>
///     invocation) is bounded by <see cref="MaxOperatorSteps"/> - mirroring
///     <see cref="CffCharstringInterpreter"/>'s deterministic resource-bounding posture.
///     </para>
/// </remarks>
internal static class Type1CharstringInterpreter
{
    /// <summary>
    ///     The maximum <c>callsubr</c> nesting depth permitted before <see cref="InvalidDataException"/>
    ///     is thrown.
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
    ///     thrown.
    /// </summary>
    private const int MaxStackSize = 96;

    /// <summary>
    ///     The maximum depth permitted for the small, separate "PostScript interpreter" operand
    ///     stack used by the <c>callothersubr</c>/<c>pop</c> mechanism before
    ///     <see cref="InvalidDataException"/> is thrown.
    /// </summary>
    private const int MaxPsStackSize = 32;

    /// <summary>
    ///     Decodes a single glyph's Type 1 charstring bytecode into <see cref="Path"/> geometry and
    ///     its declared advance width.
    /// </summary>
    /// <param name="data">
    ///     The font's own private-dictionary byte array (see <see cref="Type1Table"/>'s remarks) -
    ///     every <c>Offset</c> in <paramref name="charstring"/> and <paramref name="subrs"/> is
    ///     relative to this array's start.
    /// </param>
    /// <param name="charstring">The target glyph's own charstring byte range.</param>
    /// <param name="subrs">Every subroutine's byte range, indexed by subroutine number.</param>
    /// <returns>
    ///     The decoded glyph outline (or <see cref="Path.Empty"/> for an empty charstring) and the
    ///     width declared by the charstring's <c>hsbw</c>/<c>sbw</c> operator (<c>0</c> if neither
    ///     was ever executed).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the charstring bytecode is malformed or truncated, uses an operand count an
    ///     operator does not accept, uses an unsupported operator or escape operator (including
    ///     <c>seac</c>), uses an unsupported/malformed <c>callothersubr</c> sequence, references an
    ///     out-of-range subroutine index, or exceeds the call-depth, operator-step, or stack-size
    ///     bound.
    /// </exception>
    public static (Path Outline, double Width) Decode(
        byte[] data,
        (int Offset, int Length) charstring,
        IReadOnlyList<(int Offset, int Length)> subrs)
    {
        var state = new InterpreterState(data, subrs);
        state.Run(charstring, 0);
        state.CloseIfOpen();
        return (state.Builder.Build(), state.Width);
    }

    /// <summary>
    ///     Reads one Type 1 numeric operand token starting at <paramref name="pos"/> and pushes it
    ///     onto <paramref name="stack"/>, returning the position immediately after the token.
    /// </summary>
    /// <remarks>
    ///     Exposed (rather than private to <see cref="InterpreterState"/>) so <see cref="Type1Table"/>
    ///     can reuse this exact number encoding for its own cheap <c>hsbw</c>/<c>sbw</c>-only
    ///     advance-width peek, without duplicating a second, potentially-divergent implementation.
    ///     Type 1's numeric encoding differs from Type 2's in one respect: the 5-byte <c>255</c>
    ///     form introduces a plain 32-bit signed integer, not Type 2's 16.16 fixed-point value.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the numeric token is truncated, or when <paramref name="stack"/> already
    ///     holds <see cref="MaxStackSize"/> entries.
    /// </exception>
    internal static int ReadNumber(byte[] data, int pos, int end, List<double> stack)
    {
        var b0 = data[pos];
        double value;
        int consumed;
        if (b0 is >= 32 and <= 246)
        {
            value = b0 - 139;
            consumed = 1;
        }
        else if (b0 is >= 247 and <= 250)
        {
            if (pos + 2 > end)
            {
                throw new InvalidDataException("Type 1 charstring numeric operand is truncated.");
            }

            value = ((b0 - 247) * 256) + data[pos + 1] + 108;
            consumed = 2;
        }
        else if (b0 is >= 251 and <= 254)
        {
            if (pos + 2 > end)
            {
                throw new InvalidDataException("Type 1 charstring numeric operand is truncated.");
            }

            value = (-(b0 - 251) * 256) - data[pos + 1] - 108;
            consumed = 2;
        }
        else if (b0 == 255)
        {
            if (pos + 5 > end)
            {
                throw new InvalidDataException("Type 1 charstring numeric operand is truncated.");
            }

            value = SfntContainer.ReadInt32(data, pos + 1);
            consumed = 5;
        }
        else
        {
            throw new InvalidDataException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Unexpected Type 1 charstring byte (0x{b0:X2}) where a numeric operand or operator was expected."));
        }

        if (stack.Count >= MaxStackSize)
        {
            throw new InvalidDataException("Type 1 charstring operand stack exceeds the supported size.");
        }

        stack.Add(value);
        return pos + consumed;
    }

    /// <summary>
    ///     Holds the mutable interpreter state (operand stack, "PostScript interpreter" stack,
    ///     current point, width, flex-buffering state, and resource-bound counters) shared across
    ///     every nested <see cref="Run"/> call for a single <see cref="Decode"/> invocation.
    /// </summary>
    private sealed class InterpreterState
    {
        private readonly byte[] _data;
        private readonly IReadOnlyList<(int Offset, int Length)> _subrs;
        private readonly List<double> _stack = [];
        private readonly List<double> _psStack = [];
        private readonly List<(double X, double Y)> _flexPoints = [];
        private bool _hasOpenPath;
        private bool _done;
        private bool _flexing;
        private double _x;
        private double _y;
        private int _steps;

        public PathBuilder Builder { get; } = new();

        public double Width { get; private set; }

        public InterpreterState(byte[] data, IReadOnlyList<(int Offset, int Length)> subrs)
        {
            _data = data;
            _subrs = subrs;
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
                throw new InvalidDataException("Type 1 charstring subroutine call nesting exceeds the supported depth.");
            }

            var pos = range.Offset;
            var end = range.Offset + range.Length;
            while (pos < end && !_done)
            {
                Step();
                var b0 = _data[pos];
                if (b0 >= 32)
                {
                    pos = ReadNumber(_data, pos, end, _stack);
                    continue;
                }

                if (b0 == 12)
                {
                    if (pos + 1 >= end)
                    {
                        throw new InvalidDataException("Type 1 charstring escape operator is truncated.");
                    }

                    var escOp = _data[pos + 1];
                    pos += 2;
                    switch (escOp)
                    {
                        case 0: // dotsection
                        case 1: // vstem3
                        case 2: // hstem3
                            _stack.Clear();
                            break;

                        case 6: // seac
                            throw new InvalidDataException(
                                "Type 1 charstring 'seac' (accented composite glyph construction) is not supported.");

                        case 7: HandleSbw(); break;
                        case 12: HandleDiv(); break;
                        case 16: HandleCallOtherSubr(); break;
                        case 17: HandlePop(); break;
                        case 33: HandleSetCurrentPoint(); break;

                        default:
                            throw new InvalidDataException(
                                string.Create(CultureInfo.InvariantCulture, $"Unsupported Type 1 charstring escape operator (12 {escOp})."));
                    }

                    continue;
                }

                pos++;
                switch (b0)
                {
                    case 1: // hstem
                    case 3: // vstem
                        _stack.Clear();
                        break;

                    case 4: HandleAxisMoveTo(isX: false); break; // vmoveto
                    case 5: HandleRLineTo(); break;
                    case 6: HandleAxisLineTo(isX: true); break; // hlineto
                    case 7: HandleAxisLineTo(isX: false); break; // vlineto
                    case 8: HandleRRCurveTo(); break;
                    case 9: HandleClosePath(); break;
                    case 10: CallSubr(depth); break;
                    case 11: return; // return
                    case 13: HandleHsbw(); break;
                    case 14: HandleEndChar(); break;
                    case 21: HandleRMoveTo(); break;
                    case 22: HandleAxisMoveTo(isX: true); break; // hmoveto
                    case 30: HandleVhCurveTo(); break;
                    case 31: HandleHvCurveTo(); break;

                    default:
                        throw new InvalidDataException(
                            string.Create(CultureInfo.InvariantCulture, $"Unsupported Type 1 charstring operator ({b0})."));
                }
            }
        }

        /// <summary>
        ///     Closes the currently open subpath (if any) - called once after the outermost
        ///     <see cref="Run"/> call returns, so a charstring that never reaches <c>endchar</c>
        ///     still yields a well-formed closed path for whatever contour data it did produce.
        /// </summary>
        public void CloseIfOpen()
        {
            if (_hasOpenPath)
            {
                Builder.Close();
                _hasOpenPath = false;
            }
        }

        private void Step()
        {
            _steps++;
            if (_steps > MaxOperatorSteps)
            {
                throw new InvalidDataException("Type 1 charstring execution exceeds the supported operator budget.");
            }
        }

        private void HandleHsbw()
        {
            if (_stack.Count != 2)
            {
                throw new InvalidDataException("Type 1 charstring 'hsbw' requires exactly two operands.");
            }

            _x = _stack[0];
            _y = 0;
            Width = _stack[1];
            _stack.Clear();
        }

        private void HandleSbw()
        {
            if (_stack.Count != 4)
            {
                throw new InvalidDataException("Type 1 charstring 'sbw' requires exactly four operands.");
            }

            _x = _stack[0];
            _y = _stack[1];
            Width = _stack[2];
            _stack.Clear();
        }

        private void HandleRMoveTo()
        {
            if (_stack.Count != 2)
            {
                throw new InvalidDataException("Type 1 charstring 'rmoveto' requires exactly two operands.");
            }

            ApplyMove(_stack[0], _stack[1]);
            _stack.Clear();
        }

        private void HandleAxisMoveTo(bool isX)
        {
            if (_stack.Count != 1)
            {
                throw new InvalidDataException("Type 1 charstring 'hmoveto'/'vmoveto' requires exactly one operand.");
            }

            ApplyMove(isX ? _stack[0] : 0, isX ? 0 : _stack[0]);
            _stack.Clear();
        }

        /// <summary>
        ///     Applies a move delta: while flexing (see this class's remarks), buffers the
        ///     resulting absolute point instead of emitting a real path move; otherwise closes any
        ///     currently open subpath and starts a new one.
        /// </summary>
        private void ApplyMove(double dx, double dy)
        {
            _x += dx;
            _y += dy;

            if (_flexing)
            {
                if (_flexPoints.Count >= 7)
                {
                    throw new InvalidDataException("Type 1 charstring flex sequence buffered more than seven points.");
                }

                _flexPoints.Add((_x, _y));
                return;
            }

            CloseIfOpen();
            Builder.MoveTo(CurrentPoint());
            _hasOpenPath = true;
        }

        private void HandleRLineTo()
        {
            if (_stack.Count != 2)
            {
                throw new InvalidDataException("Type 1 charstring 'rlineto' requires exactly two operands.");
            }

            _x += _stack[0];
            _y += _stack[1];
            Builder.LineTo(CurrentPoint());
            _stack.Clear();
        }

        private void HandleAxisLineTo(bool isX)
        {
            if (_stack.Count != 1)
            {
                throw new InvalidDataException("Type 1 charstring 'hlineto'/'vlineto' requires exactly one operand.");
            }

            if (isX)
            {
                _x += _stack[0];
            }
            else
            {
                _y += _stack[0];
            }

            Builder.LineTo(CurrentPoint());
            _stack.Clear();
        }

        private void HandleRRCurveTo()
        {
            if (_stack.Count != 6)
            {
                throw new InvalidDataException("Type 1 charstring 'rrcurveto' requires exactly six operands.");
            }

            var c1X = _x + _stack[0];
            var c1Y = _y + _stack[1];
            var c2X = c1X + _stack[2];
            var c2Y = c1Y + _stack[3];
            _x = c2X + _stack[4];
            _y = c2Y + _stack[5];
            Builder.CubicBezierTo(Pt(c1X, c1Y), Pt(c2X, c2Y), CurrentPoint());
            _stack.Clear();
        }

        private void HandleVhCurveTo()
        {
            if (_stack.Count != 4)
            {
                throw new InvalidDataException("Type 1 charstring 'vhcurveto' requires exactly four operands.");
            }

            var c1X = _x;
            var c1Y = _y + _stack[0];
            var c2X = c1X + _stack[1];
            var c2Y = c1Y + _stack[2];
            _x = c2X + _stack[3];
            _y = c2Y;
            Builder.CubicBezierTo(Pt(c1X, c1Y), Pt(c2X, c2Y), CurrentPoint());
            _stack.Clear();
        }

        private void HandleHvCurveTo()
        {
            if (_stack.Count != 4)
            {
                throw new InvalidDataException("Type 1 charstring 'hvcurveto' requires exactly four operands.");
            }

            var c1X = _x + _stack[0];
            var c1Y = _y;
            var c2X = c1X + _stack[1];
            var c2Y = c1Y + _stack[2];
            _x = c2X;
            _y = c2Y + _stack[3];
            Builder.CubicBezierTo(Pt(c1X, c1Y), Pt(c2X, c2Y), CurrentPoint());
            _stack.Clear();
        }

        private void HandleClosePath()
        {
            if (_hasOpenPath)
            {
                Builder.Close();
                _hasOpenPath = false;
            }

            _stack.Clear();
        }

        private void CallSubr(int depth)
        {
            if (_stack.Count == 0)
            {
                throw new InvalidDataException("Type 1 charstring 'callsubr' is missing its subroutine index operand.");
            }

            var index = (int)_stack[^1];
            _stack.RemoveAt(_stack.Count - 1);

            if (index < 0 || index >= _subrs.Count)
            {
                throw new InvalidDataException("Type 1 charstring subroutine index is out of range.");
            }

            Run(_subrs[index], depth + 1);
        }

        /// <summary>
        ///     Handles the binary <c>div</c> operator: consumes exactly the top two operands
        ///     (regardless of how many other operands already sit beneath them on the stack - a
        ///     common technique for constructing one operand of a later, still-pending operator
        ///     via division) and pushes their quotient.
        /// </summary>
        private void HandleDiv()
        {
            if (_stack.Count < 2)
            {
                throw new InvalidDataException("Type 1 charstring 'div' requires at least two operands.");
            }

            var divisor = _stack[^1];
            var dividend = _stack[^2];
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);
            _stack.Add(dividend / divisor);
        }

        /// <summary>
        ///     Handles <c>callothersubr</c>: pops the OtherSubrs number and argument count, then
        ///     that many arguments, and dispatches to the three recognized OtherSubrs (flex
        ///     begin/point-mark/end, and hint replacement) - see this class's remarks for the flex
        ///     geometry conversion. Any other OtherSubrs number is rejected (fail closed).
        /// </summary>
        private void HandleCallOtherSubr()
        {
            if (_stack.Count < 2)
            {
                throw new InvalidDataException(
                    "Type 1 charstring 'callothersubr' requires at least an argument count and an OtherSubrs number.");
            }

            var otherSubrNumber = (int)_stack[^1];
            var argCount = (int)_stack[^2];
            _stack.RemoveAt(_stack.Count - 1);
            _stack.RemoveAt(_stack.Count - 1);

            if (argCount < 0 || argCount > _stack.Count)
            {
                throw new InvalidDataException("Type 1 charstring 'callothersubr' declares an invalid argument count.");
            }

            var args = _stack.GetRange(_stack.Count - argCount, argCount);
            _stack.RemoveRange(_stack.Count - argCount, argCount);

            switch (otherSubrNumber)
            {
                case 1: // begin flex
                    _flexing = true;
                    _flexPoints.Clear();
                    break;

                case 2: // flex point mark - no-op; the preceding rmoveto already buffered the point
                    break;

                case 0: // end flex
                    if (!_flexing)
                    {
                        throw new InvalidDataException("Type 1 charstring flex 'end' OtherSubrs called without a matching 'begin'.");
                    }

                    if (args.Count != 3)
                    {
                        throw new InvalidDataException("Type 1 charstring flex 'end' OtherSubrs requires exactly three arguments.");
                    }

                    if (_flexPoints.Count != 7)
                    {
                        throw new InvalidDataException("Type 1 charstring flex sequence did not buffer exactly seven points.");
                    }

                    EmitFlex();
                    _flexing = false;

                    // The real PostScript flex procedure leaves (x, y) on the PostScript
                    // interpreter stack for the charstring's following 'pop pop setcurrentpoint'
                    // to retrieve, in that order - push y first (bottom) then x (top), so the
                    // first 'pop' yields x and the second yields y.
                    PushPs(args[2]);
                    PushPs(args[1]);
                    break;

                case 3: // hint replacement - transparent pass-through
                    if (args.Count != 1)
                    {
                        throw new InvalidDataException("Type 1 charstring hint-replacement OtherSubrs requires exactly one argument.");
                    }

                    PushPs(args[0]);
                    break;

                default:
                    throw new InvalidDataException(
                        string.Create(CultureInfo.InvariantCulture, $"Unsupported Type 1 charstring 'callothersubr' index ({otherSubrNumber})."));
            }
        }

        /// <summary>
        ///     Converts the seven buffered flex reference points into two cubic Bezier segments:
        ///     the first buffered point is a reference point discarded per the Type 1 flex
        ///     mechanism (see this class's remarks), and the remaining six become, in order, the
        ///     first curve's two control points and join point, then the second curve's two
        ///     control points and end point.
        /// </summary>
        private void EmitFlex()
        {
            if (!_hasOpenPath)
            {
                throw new InvalidDataException("Type 1 charstring flex sequence occurred before the first moveto.");
            }

            var c1A = _flexPoints[1];
            var c1B = _flexPoints[2];
            var joint = _flexPoints[3];
            var c2A = _flexPoints[4];
            var c2B = _flexPoints[5];
            var flexEnd = _flexPoints[6];

            Builder.CubicBezierTo(Pt(c1A.X, c1A.Y), Pt(c1B.X, c1B.Y), Pt(joint.X, joint.Y));
            Builder.CubicBezierTo(Pt(c2A.X, c2A.Y), Pt(c2B.X, c2B.Y), Pt(flexEnd.X, flexEnd.Y));
        }

        private void PushPs(double value)
        {
            if (_psStack.Count >= MaxPsStackSize)
            {
                throw new InvalidDataException("Type 1 charstring PostScript-interpreter stack exceeds the supported size.");
            }

            _psStack.Add(value);
        }

        private void HandlePop()
        {
            if (_psStack.Count == 0)
            {
                throw new InvalidDataException("Type 1 charstring 'pop' with an empty PostScript-interpreter stack.");
            }

            var value = _psStack[^1];
            _psStack.RemoveAt(_psStack.Count - 1);

            if (_stack.Count >= MaxStackSize)
            {
                throw new InvalidDataException("Type 1 charstring operand stack exceeds the supported size.");
            }

            _stack.Add(value);
        }

        private void HandleSetCurrentPoint()
        {
            if (_stack.Count != 2)
            {
                throw new InvalidDataException("Type 1 charstring 'setcurrentpoint' requires exactly two operands.");
            }

            _x = _stack[0];
            _y = _stack[1];
            _stack.Clear();
        }

        private void HandleEndChar()
        {
            CloseIfOpen();
            _stack.Clear();
            _done = true;
        }

        private Vector2 CurrentPoint() => Pt(_x, _y);

        private static Vector2 Pt(double x, double y) => new((float)x, (float)y);
    }
}

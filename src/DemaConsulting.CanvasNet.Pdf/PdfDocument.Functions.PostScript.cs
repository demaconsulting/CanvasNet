// cspell:ignore idiv bitshift cvi cvr ifelse exch
using System.Globalization;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    // PDF 32000-1 §7.10.5: the PostScript calculator (/FunctionType 4). A restricted, loop-free
    // subset of PostScript: the program is parsed once into a flat instruction array (the
    // `{...} if` / `{...} {...} ifelse` blocks become relative jumps), then executed against a
    // fixed-size stack. Every dimension is bounded - program length, block nesting, stack depth
    // and executed instructions - so a hostile program cannot consume unbounded time or memory.

    /// <summary>The maximum operand-stack depth (the specification's own limit).</summary>
    private const int MaxPostScriptStackDepth = 100;

    /// <summary>The maximum number of instructions in a compiled program.</summary>
    private const int MaxPostScriptInstructions = 8192;

    /// <summary>The maximum nesting depth of <c>{ }</c> blocks.</summary>
    private const int MaxPostScriptNesting = 16;

    /// <summary>The maximum number of instructions executed by one evaluation.</summary>
    private const int MaxPostScriptExecutedInstructions = 65536;

    /// <summary>The maximum size in bytes of a decoded program stream.</summary>
    private const int MaxPostScriptSourceBytes = 256 * 1024;

    /// <summary>The kind of a calculator stack value.</summary>
    private enum PsType : byte
    {
        Int,
        Real,
        Bool,
    }

    /// <summary>A calculator stack value: an integer, a real, or a boolean (stored as 0/1).</summary>
    private readonly record struct PsValue(double Value, PsType Type);

    /// <summary>The calculator operations.</summary>
    private enum PsOp : byte
    {
        PushInt,
        PushReal,
        PushTrue,
        PushFalse,
        Jz,
        Jmp,
        Abs,
        Add,
        Atan,
        Ceiling,
        Cos,
        Cvi,
        Cvr,
        Div,
        Exp,
        Floor,
        Idiv,
        Ln,
        Log,
        Mod,
        Mul,
        Neg,
        Round,
        Sin,
        Sqrt,
        Sub,
        Truncate,
        And,
        Bitshift,
        Eq,
        Ge,
        Gt,
        Le,
        Lt,
        Ne,
        Not,
        Or,
        Xor,
        Copy,
        Dup,
        Exch,
        Index,
        Pop,
        Roll,
    }

    /// <summary>One compiled instruction: an operation plus its numeric operand or jump distance.</summary>
    private readonly record struct PsInstruction(PsOp Op, double Value = 0, int Offset = 0);

    /// <summary>The operator-name to operation table (excludes <c>if</c>/<c>ifelse</c>/<c>true</c>/<c>false</c>).</summary>
    private static readonly Dictionary<string, PsOp> PostScriptOperators = new(StringComparer.Ordinal)
    {
        ["abs"] = PsOp.Abs,
        ["add"] = PsOp.Add,
        ["atan"] = PsOp.Atan,
        ["ceiling"] = PsOp.Ceiling,
        ["cos"] = PsOp.Cos,
        ["cvi"] = PsOp.Cvi,
        ["cvr"] = PsOp.Cvr,
        ["div"] = PsOp.Div,
        ["exp"] = PsOp.Exp,
        ["floor"] = PsOp.Floor,
        ["idiv"] = PsOp.Idiv,
        ["ln"] = PsOp.Ln,
        ["log"] = PsOp.Log,
        ["mod"] = PsOp.Mod,
        ["mul"] = PsOp.Mul,
        ["neg"] = PsOp.Neg,
        ["round"] = PsOp.Round,
        ["sin"] = PsOp.Sin,
        ["sqrt"] = PsOp.Sqrt,
        ["sub"] = PsOp.Sub,
        ["truncate"] = PsOp.Truncate,
        ["and"] = PsOp.And,
        ["bitshift"] = PsOp.Bitshift,
        ["eq"] = PsOp.Eq,
        ["ge"] = PsOp.Ge,
        ["gt"] = PsOp.Gt,
        ["le"] = PsOp.Le,
        ["lt"] = PsOp.Lt,
        ["ne"] = PsOp.Ne,
        ["not"] = PsOp.Not,
        ["or"] = PsOp.Or,
        ["xor"] = PsOp.Xor,
        ["copy"] = PsOp.Copy,
        ["dup"] = PsOp.Dup,
        ["exch"] = PsOp.Exch,
        ["index"] = PsOp.Index,
        ["pop"] = PsOp.Pop,
        ["roll"] = PsOp.Roll,
    };

    /// <summary>
    ///     A resolved <c>/FunctionType 4</c> (PostScript calculator) function: <c>m</c> inputs
    ///     clamped to <c>/Domain</c>, <c>n</c> outputs taken from the top of the stack and clipped
    ///     to <c>/Range</c>. Evaluation does not allocate.
    /// </summary>
    internal sealed class PostScriptFunction : IPdfMultiFunction, IPdfFunction
    {
        private readonly PsInstruction[] _code;
        private readonly double[] _domain;
        private readonly double[] _range;

        internal PostScriptFunction(byte[] source, double[] domain, double[] range)
        {
            _domain = domain;
            _range = range;
            _code = Compile(source);
        }

        /// <inheritdoc/>
        public int InputCount => _domain.Length / 2;

        /// <inheritdoc/>
        public int OutputCount => _range.Length / 2;

        /// <inheritdoc/>
        public void Evaluate(ReadOnlySpan<double> input, Span<double> output)
        {
            Span<PsValue> stack = stackalloc PsValue[MaxPostScriptStackDepth];
            var sp = 0;
            for (var i = 0; i < InputCount; i++)
            {
                var lo = Math.Min(_domain[2 * i], _domain[(2 * i) + 1]);
                var hi = Math.Max(_domain[2 * i], _domain[(2 * i) + 1]);
                var value = i < input.Length ? input[i] : 0.0;
                stack[sp++] = new PsValue(Math.Clamp(double.IsNaN(value) ? lo : value, lo, hi), PsType.Real);
            }

            Execute(_code, stack, ref sp);

            var n = OutputCount;
            if (sp < n)
            {
                throw new InvalidDataException(
                    $"PostScript calculator function left {sp} value(s) on the stack; {n} expected.");
            }

            for (var j = 0; j < n; j++)
            {
                var top = stack[sp - n + j];
                if (top.Type == PsType.Bool)
                {
                    throw new InvalidDataException("PostScript calculator function produced a boolean output.");
                }

                output[j] = Math.Clamp(
                    top.Value,
                    Math.Min(_range[2 * j], _range[(2 * j) + 1]),
                    Math.Max(_range[2 * j], _range[(2 * j) + 1]));
            }
        }

        /// <inheritdoc/>
        double[] IPdfFunction.Evaluate(double input)
        {
            if (InputCount != 1)
            {
                throw new InvalidOperationException("Only a single-input function can be evaluated with one value.");
            }

            var result = new double[OutputCount];
            Span<double> in1 = [input];
            Evaluate(in1, result);
            return result;
        }

        // ---------------------------------------------------------------- parsing

        private static PsInstruction[] Compile(byte[] source)
        {
            if (source.Length > MaxPostScriptSourceBytes)
            {
                throw new InvalidDataException("PostScript calculator program is too large.");
            }

            var pos = 0;
            SkipTrivia(source, ref pos);
            if (pos >= source.Length || source[pos] != (byte)'{')
            {
                throw new InvalidDataException("PostScript calculator program must begin with '{'.");
            }

            pos++;
            var code = ParseBlock(source, ref pos, 1);
            SkipTrivia(source, ref pos);
            if (pos < source.Length)
            {
                throw new InvalidDataException("PostScript calculator program has content after its closing '}'.");
            }

            return [.. code];
        }

        private static bool IsSpace(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or 0;

        private static void SkipTrivia(byte[] src, ref int pos)
        {
            while (pos < src.Length)
            {
                if (IsSpace(src[pos]))
                {
                    pos++;
                }
                else if (src[pos] == (byte)'%')
                {
                    while (pos < src.Length && src[pos] != (byte)'\n' && src[pos] != (byte)'\r')
                    {
                        pos++;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private static List<PsInstruction> ParseBlock(byte[] src, ref int pos, int depth)
        {
            if (depth > MaxPostScriptNesting)
            {
                throw new InvalidDataException("PostScript calculator program nests blocks too deeply.");
            }

            var code = new List<PsInstruction>();
            var pending = new List<List<PsInstruction>>();
            while (true)
            {
                SkipTrivia(src, ref pos);
                if (pos >= src.Length)
                {
                    throw new InvalidDataException("PostScript calculator program is missing a closing '}'.");
                }

                var c = src[pos];
                if (c == (byte)'{')
                {
                    pos++;
                    if (pending.Count >= 2)
                    {
                        throw new InvalidDataException("PostScript calculator program has too many consecutive blocks.");
                    }

                    pending.Add(ParseBlock(src, ref pos, depth + 1));
                    continue;
                }

                if (c == (byte)'}')
                {
                    pos++;
                    if (pending.Count > 0)
                    {
                        throw new InvalidDataException("PostScript calculator block is not followed by 'if' or 'ifelse'.");
                    }

                    return code;
                }

                var start = pos;
                while (pos < src.Length && !IsSpace(src[pos]) && src[pos] != (byte)'{' && src[pos] != (byte)'}'
                    && src[pos] != (byte)'%')
                {
                    pos++;
                }

                var token = System.Text.Encoding.ASCII.GetString(src, start, pos - start);
                switch (token)
                {
                    case "if":
                        if (pending.Count != 1)
                        {
                            throw new InvalidDataException("PostScript calculator 'if' requires exactly one preceding block.");
                        }

                        code.Add(new PsInstruction(PsOp.Jz, Offset: pending[0].Count));
                        code.AddRange(pending[0]);
                        pending.Clear();
                        break;
                    case "ifelse":
                        if (pending.Count != 2)
                        {
                            throw new InvalidDataException("PostScript calculator 'ifelse' requires exactly two preceding blocks.");
                        }

                        code.Add(new PsInstruction(PsOp.Jz, Offset: pending[0].Count + 1));
                        code.AddRange(pending[0]);
                        code.Add(new PsInstruction(PsOp.Jmp, Offset: pending[1].Count));
                        code.AddRange(pending[1]);
                        pending.Clear();
                        break;
                    default:
                        if (pending.Count > 0)
                        {
                            throw new InvalidDataException("PostScript calculator block is not followed by 'if' or 'ifelse'.");
                        }

                        code.Add(ParseToken(token));
                        break;
                }

                if (code.Count > MaxPostScriptInstructions)
                {
                    throw new InvalidDataException("PostScript calculator program has too many instructions.");
                }
            }
        }

        private static PsInstruction ParseToken(string token)
        {
            switch (token)
            {
                case "true":
                    return new PsInstruction(PsOp.PushTrue);
                case "false":
                    return new PsInstruction(PsOp.PushFalse);
            }

            if (PostScriptOperators.TryGetValue(token, out var op))
            {
                return new PsInstruction(op);
            }

            if (token.Length > 0 && (char.IsAsciiDigit(token[0]) || token[0] is '+' or '-' or '.')
                && double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && double.IsFinite(number))
            {
                var isInteger = token.AsSpan().IndexOfAny(".eE") < 0 && Math.Abs(number) <= int.MaxValue;
                return new PsInstruction(isInteger ? PsOp.PushInt : PsOp.PushReal, number);
            }

            throw new InvalidDataException($"PostScript calculator program has an unsupported token '{token}'.");
        }

        // -------------------------------------------------------------- execution

        private static void Execute(PsInstruction[] code, Span<PsValue> stack, ref int sp)
        {
            var pc = 0;
            var budget = MaxPostScriptExecutedInstructions;
            while (pc < code.Length)
            {
                if (--budget < 0)
                {
                    throw new InvalidDataException("PostScript calculator program exceeded its execution budget.");
                }

                var ins = code[pc++];
                switch (ins.Op)
                {
                    case PsOp.PushInt:
                        Push(stack, ref sp, new PsValue(ins.Value, PsType.Int));
                        break;
                    case PsOp.PushReal:
                        Push(stack, ref sp, new PsValue(ins.Value, PsType.Real));
                        break;
                    case PsOp.PushTrue:
                        Push(stack, ref sp, new PsValue(1, PsType.Bool));
                        break;
                    case PsOp.PushFalse:
                        Push(stack, ref sp, new PsValue(0, PsType.Bool));
                        break;
                    case PsOp.Jz:
                        if (PopBool(stack, ref sp) == 0)
                        {
                            pc += ins.Offset;
                        }

                        break;
                    case PsOp.Jmp:
                        pc += ins.Offset;
                        break;
                    case PsOp.Add:
                    case PsOp.Sub:
                    case PsOp.Mul:
                        Arithmetic(ins.Op, stack, ref sp);
                        break;
                    case PsOp.Div:
                        {
                            var b = PopNumber(stack, ref sp);
                            var a = PopNumber(stack, ref sp);
                            if (b.Value == 0)
                            {
                                throw new InvalidDataException("PostScript calculator division by zero.");
                            }

                            Push(stack, ref sp, new PsValue(a.Value / b.Value, PsType.Real));
                            break;
                        }

                    case PsOp.Idiv:
                    case PsOp.Mod:
                        {
                            var b = PopInt(stack, ref sp);
                            var a = PopInt(stack, ref sp);
                            if (b == 0)
                            {
                                throw new InvalidDataException("PostScript calculator integer division by zero.");
                            }

                            Push(stack, ref sp, new PsValue(ins.Op == PsOp.Idiv ? (long)a / b : a % (long)b, PsType.Int));
                            break;
                        }

                    case PsOp.Neg:
                    case PsOp.Abs:
                        {
                            var a = PopNumber(stack, ref sp);
                            Push(stack, ref sp, new PsValue(ins.Op == PsOp.Neg ? -a.Value : Math.Abs(a.Value), a.Type));
                            break;
                        }

                    case PsOp.Ceiling:
                    case PsOp.Floor:
                    case PsOp.Round:
                    case PsOp.Truncate:
                        {
                            var a = PopNumber(stack, ref sp);
                            var r = ins.Op switch
                            {
                                PsOp.Ceiling => Math.Ceiling(a.Value),
                                PsOp.Floor => Math.Floor(a.Value),
                                PsOp.Round => Math.Floor(a.Value + 0.5),
                                _ => Math.Truncate(a.Value),
                            };
                            Push(stack, ref sp, new PsValue(r, a.Type));
                            break;
                        }

                    case PsOp.Cvi:
                        {
                            var a = PopNumber(stack, ref sp);
                            var t = Math.Truncate(a.Value);
                            if (Math.Abs(t) > int.MaxValue)
                            {
                                throw new InvalidDataException("PostScript calculator 'cvi' overflow.");
                            }

                            Push(stack, ref sp, new PsValue(t, PsType.Int));
                            break;
                        }

                    case PsOp.Cvr:
                        {
                            var a = PopNumber(stack, ref sp);
                            Push(stack, ref sp, new PsValue(a.Value, PsType.Real));
                            break;
                        }

                    case PsOp.Sqrt:
                    case PsOp.Ln:
                    case PsOp.Log:
                        {
                            var a = PopNumber(stack, ref sp).Value;
                            if (ins.Op == PsOp.Sqrt ? a < 0 : a <= 0)
                            {
                                throw new InvalidDataException("PostScript calculator math domain error.");
                            }

                            var r = ins.Op switch
                            {
                                PsOp.Sqrt => Math.Sqrt(a),
                                PsOp.Ln => Math.Log(a),
                                _ => Math.Log10(a),
                            };
                            Push(stack, ref sp, new PsValue(r, PsType.Real));
                            break;
                        }

                    case PsOp.Sin:
                    case PsOp.Cos:
                        {
                            var radians = PopNumber(stack, ref sp).Value * Math.PI / 180.0;
                            var r = ins.Op == PsOp.Sin ? Math.Sin(radians) : Math.Cos(radians);
                            Push(stack, ref sp, new PsValue(r, PsType.Real));
                            break;
                        }

                    case PsOp.Atan:
                        {
                            var den = PopNumber(stack, ref sp).Value;
                            var num = PopNumber(stack, ref sp).Value;
                            if (num == 0 && den == 0)
                            {
                                throw new InvalidDataException("PostScript calculator 'atan' of (0, 0).");
                            }

                            var degrees = Math.Atan2(num, den) * 180.0 / Math.PI;
                            if (degrees < 0)
                            {
                                degrees += 360.0;
                            }

                            Push(stack, ref sp, new PsValue(degrees, PsType.Real));
                            break;
                        }

                    case PsOp.Exp:
                        {
                            var exponent = PopNumber(stack, ref sp).Value;
                            var @base = PopNumber(stack, ref sp).Value;
                            var r = Math.Pow(@base, exponent);
                            if (!double.IsFinite(r))
                            {
                                throw new InvalidDataException("PostScript calculator 'exp' result is not finite.");
                            }

                            Push(stack, ref sp, new PsValue(r, PsType.Real));
                            break;
                        }

                    case PsOp.Eq:
                    case PsOp.Ne:
                        {
                            var b = Pop(stack, ref sp);
                            var a = Pop(stack, ref sp);
                            var equal = (a.Type == PsType.Bool) == (b.Type == PsType.Bool) && a.Value.CompareTo(b.Value) == 0;
                            Push(stack, ref sp, new PsValue((ins.Op == PsOp.Eq) == equal ? 1 : 0, PsType.Bool));
                            break;
                        }

                    case PsOp.Ge:
                    case PsOp.Gt:
                    case PsOp.Le:
                    case PsOp.Lt:
                        {
                            var b = PopNumber(stack, ref sp).Value;
                            var a = PopNumber(stack, ref sp).Value;
                            var result = ins.Op switch
                            {
                                PsOp.Ge => a >= b,
                                PsOp.Gt => a > b,
                                PsOp.Le => a <= b,
                                _ => a < b,
                            };
                            Push(stack, ref sp, new PsValue(result ? 1 : 0, PsType.Bool));
                            break;
                        }

                    case PsOp.And:
                    case PsOp.Or:
                    case PsOp.Xor:
                        {
                            var b = Pop(stack, ref sp);
                            var a = Pop(stack, ref sp);
                            if (a.Type == PsType.Bool && b.Type == PsType.Bool)
                            {
                                var x = a.Value != 0;
                                var y = b.Value != 0;
                                var r = ins.Op switch { PsOp.And => x && y, PsOp.Or => x || y, _ => x ^ y };
                                Push(stack, ref sp, new PsValue(r ? 1 : 0, PsType.Bool));
                            }
                            else if (a.Type == PsType.Int && b.Type == PsType.Int)
                            {
                                var x = (int)a.Value;
                                var y = (int)b.Value;
                                var r = ins.Op switch { PsOp.And => x & y, PsOp.Or => x | y, _ => x ^ y };
                                Push(stack, ref sp, new PsValue(r, PsType.Int));
                            }
                            else
                            {
                                throw new InvalidDataException("PostScript calculator logical operands have mismatched types.");
                            }

                            break;
                        }

                    case PsOp.Not:
                        {
                            var a = Pop(stack, ref sp);
                            switch (a.Type)
                            {
                                case PsType.Bool:
                                    Push(stack, ref sp, new PsValue(a.Value != 0 ? 0 : 1, PsType.Bool));
                                    break;
                                case PsType.Int:
                                    Push(stack, ref sp, new PsValue(~(int)a.Value, PsType.Int));
                                    break;
                                default:
                                    throw new InvalidDataException("PostScript calculator 'not' requires a boolean or integer.");
                            }

                            break;
                        }

                    case PsOp.Bitshift:
                        {
                            var shift = PopInt(stack, ref sp);
                            var value = (uint)PopInt(stack, ref sp);
                            var r = shift switch
                            {
                                >= 32 or <= -32 => 0u,
                                >= 0 => value << shift,
                                _ => value >> -shift,
                            };
                            Push(stack, ref sp, new PsValue((int)r, PsType.Int));
                            break;
                        }

                    case PsOp.Copy:
                        {
                            var count = PopInt(stack, ref sp);
                            if (count < 0 || count > sp || sp + count > MaxPostScriptStackDepth)
                            {
                                throw new InvalidDataException("PostScript calculator 'copy' count is out of range.");
                            }

                            stack.Slice(sp - count, count).CopyTo(stack[sp..]);
                            sp += count;
                            break;
                        }

                    case PsOp.Dup:
                        {
                            var a = Pop(stack, ref sp);
                            Push(stack, ref sp, a);
                            Push(stack, ref sp, a);
                            break;
                        }

                    case PsOp.Exch:
                        {
                            var b = Pop(stack, ref sp);
                            var a = Pop(stack, ref sp);
                            Push(stack, ref sp, b);
                            Push(stack, ref sp, a);
                            break;
                        }

                    case PsOp.Index:
                        {
                            var depth = PopInt(stack, ref sp);
                            if (depth < 0 || depth >= sp)
                            {
                                throw new InvalidDataException("PostScript calculator 'index' is out of range.");
                            }

                            Push(stack, ref sp, stack[sp - 1 - depth]);
                            break;
                        }

                    case PsOp.Pop:
                        Pop(stack, ref sp);
                        break;
                    case PsOp.Roll:
                        {
                            var j = PopInt(stack, ref sp);
                            var n = PopInt(stack, ref sp);
                            if (n < 0 || n > sp)
                            {
                                throw new InvalidDataException("PostScript calculator 'roll' count is out of range.");
                            }

                            if (n > 1)
                            {
                                var shift = ((j % n) + n) % n;
                                var group = stack.Slice(sp - n, n);
                                group.Reverse();
                                group[..shift].Reverse();
                                group[shift..].Reverse();
                            }

                            break;
                        }

                    default:
                        throw new InvalidDataException("PostScript calculator program is corrupt.");
                }
            }
        }

        private static void Push(Span<PsValue> stack, ref int sp, PsValue value)
        {
            if (sp >= MaxPostScriptStackDepth)
            {
                throw new InvalidDataException("PostScript calculator stack overflow.");
            }

            stack[sp++] = value;
        }

        private static PsValue Pop(Span<PsValue> stack, ref int sp)
        {
            if (sp <= 0)
            {
                throw new InvalidDataException("PostScript calculator stack underflow.");
            }

            return stack[--sp];
        }

        private static PsValue PopNumber(Span<PsValue> stack, ref int sp)
        {
            var value = Pop(stack, ref sp);
            if (value.Type == PsType.Bool)
            {
                throw new InvalidDataException("PostScript calculator expected a number.");
            }

            return value;
        }

        private static int PopInt(Span<PsValue> stack, ref int sp)
        {
            var value = Pop(stack, ref sp);
            if (value.Type != PsType.Int)
            {
                throw new InvalidDataException("PostScript calculator expected an integer.");
            }

            return (int)value.Value;
        }

        private static double PopBool(Span<PsValue> stack, ref int sp)
        {
            var value = Pop(stack, ref sp);
            if (value.Type != PsType.Bool)
            {
                throw new InvalidDataException("PostScript calculator expected a boolean.");
            }

            return value.Value;
        }

        private static void Arithmetic(PsOp op, Span<PsValue> stack, ref int sp)
        {
            var b = PopNumber(stack, ref sp);
            var a = PopNumber(stack, ref sp);
            var r = op switch
            {
                PsOp.Add => a.Value + b.Value,
                PsOp.Sub => a.Value - b.Value,
                _ => a.Value * b.Value,
            };
            var integer = a.Type == PsType.Int && b.Type == PsType.Int && Math.Abs(r) <= int.MaxValue;
            Push(stack, ref sp, new PsValue(r, integer ? PsType.Int : PsType.Real));
        }
    }

    /// <summary>Resolves a <c>/FunctionType 4</c> stream into a <see cref="PostScriptFunction"/>.</summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the function is not a stream, <c>/Domain</c>/<c>/Range</c> are missing or
    ///     malformed, or the program is malformed or exceeds a resource bound.
    /// </exception>
    private PostScriptFunction ResolvePostScriptFunction(PdfObject function)
    {
        if (function.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/FunctionType 4 function must be a stream.");
        }

        var domain = RequireNumberArray(function, "Domain");
        if (domain.Length < 2 || domain.Length % 2 != 0 || domain.Length / 2 > MaxDeviceNComponents
            || domain.Any(static d => !double.IsFinite(d)))
        {
            throw new InvalidDataException("/Domain must be 1 to 32 finite number pairs.");
        }

        var range = RequireNumberArray(function, "Range");
        if (range.Length == 0 || range.Length % 2 != 0 || range.Length / 2 > MaxFunctionOutputs
            || range.Any(static r => !double.IsFinite(r)))
        {
            throw new InvalidDataException($"/Range must be 1 to {MaxFunctionOutputs} finite number pairs.");
        }

        return new PostScriptFunction(GetStreamDecodedBytes(function), domain, range);
    }
}

using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The datum an expression reads with <c>datum</c>, supplied by a caller that has its own row representation.
/// </summary>
/// <remarks>
///     The evaluator calls <see cref="GetField"/> for <c>datum.name</c> and <c>datum['name']</c>, and
///     <see cref="ToValue"/> only when the whole datum is used as a value. Implementations are used by one evaluator on
///     one thread.
/// </remarks>
internal interface IExpressionDatum
{
    /// <summary>
    ///     Gets the value of a field of the datum.
    /// </summary>
    /// <param name="name">The property name as written in the expression (not a field path).</param>
    /// <returns>The value; null when the datum has no such field.</returns>
    DataValue GetField(string name);

    /// <summary>
    ///     Gets the whole datum as a value.
    /// </summary>
    /// <returns>The value, normally an object.</returns>
    DataValue ToValue();
}

/// <summary>
///     The culture-invariant interpreter of compiled expressions: JavaScript and Vega semantics over
///     <see cref="DataValue"/>, with an explicit step budget and cancellation.
/// </summary>
/// <remarks>
///     <para>
///         An evaluator serves one call of the renderer on one thread: it owns the step counter of the call (one step per
///         executed instruction, plus the size-proportional charges of functions, so the budget bounds all work),
///         the cancellation token and the parameter values. It is not thread safe, but it has no shared mutable state:
///         evaluators created for different calls or threads never influence each other, a <see cref="CompiledExpression"/>
///         can be shared freely, and the same expression, datum and parameters give the same result in every call and
///         under every current culture.
///     </para>
///     <para>
///         The step budget is <see cref="VegaLiteLimits.MaxEvaluationSteps"/>; exhausting it throws a
///         <see cref="VegaLiteRenderException"/> located at the expression with the offset of the instruction that ran
///         out. The cancellation token is checked when an evaluation starts and whenever the step counter passes a
///         multiple of 1,024; cancellation throws <see cref="OperationCanceledException"/>. The program has no
///         recursion, so the depth of an expression never threatens the stack; <see cref="CompiledExpression.Depth"/>
///         reports the nesting that the parser bounded by <see cref="VegaLiteLimits.MaxExpressionDepth"/>.
///     </para>
///     <para>
///         Semantics: missing members, members of null and unsupported constructs (unknown functions, signals,
///         events, deferred operators) evaluate to null; division by zero gives an infinity or NaN; <c>&amp;&amp;</c>,
///         <c>||</c> and <c>?:</c> evaluate only the branch they need and return the operand, not a boolean. Because
///         the value model has one null for both JavaScript's <c>null</c> and <c>undefined</c>, null converts to zero
///         in arithmetic.
///     </para>
/// </remarks>
internal sealed partial class ExpressionEvaluator
{
    /// <summary>The initial capacity of the value stack.</summary>
    private const int InitialStackSize = 16;

    /// <summary>The per-evaluation context shared with the functions.</summary>
    private readonly ExpressionFunctionContext _context;

    /// <summary>The parameter values by name, or null when there are none.</summary>
    private readonly IReadOnlyDictionary<string, DataValue>? _parameters;

    /// <summary>The value stack, reused between evaluations.</summary>
    private DataValue[] _stack = new DataValue[InitialStackSize];

    /// <summary>The way the current datum is supplied.</summary>
    private DatumMode _mode;

    /// <summary>The table of the current datum in table mode.</summary>
    private DataTable? _table;

    /// <summary>The row of the current datum in table mode.</summary>
    private int _row;

    /// <summary>The arrays and objects rebuilt from the current row in table mode by canonical path, or null before the first.</summary>
    private Dictionary<string, DataValue>? _nested;

    /// <summary>The current datum in value mode.</summary>
    private DataValue _datumValue;

    /// <summary>The current datum in custom mode.</summary>
    private IExpressionDatum? _custom;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExpressionEvaluator"/> class for one call.
    /// </summary>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="cancellationToken">The cancellation token of the call.</param>
    /// <param name="parameters">The parameter values by name; identifiers that name no entry evaluate to null.</param>
    public ExpressionEvaluator(
        VegaLiteLimits? limits,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, DataValue>? parameters = null)
    {
        _context = new ExpressionFunctionContext(limits, cancellationToken);
        _parameters = parameters;
    }

    /// <summary>How the datum of the current evaluation is supplied.</summary>
    private enum DatumMode
    {
        /// <summary>There is no datum.</summary>
        None,

        /// <summary>The datum is a row of a table.</summary>
        Table,

        /// <summary>The datum is a value, normally an object.</summary>
        Value,

        /// <summary>The datum is supplied by a caller-defined object.</summary>
        Custom,
    }

    /// <summary>Gets the number of steps this evaluator has used in the call.</summary>
    public long StepsUsed => _context.Steps;

    /// <summary>Gets the longest text, in characters, that an evaluation may produce or read as a value.</summary>
    public int MaxStringLength => _context.MaxStringLength;

    /// <summary>
    ///     Evaluates an expression that does not read a datum.
    /// </summary>
    /// <param name="expression">The compiled expression.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the step budget, the string limit or a regular expression limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public DataValue Evaluate(CompiledExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _mode = DatumMode.None;
        return Run(expression);
    }

    /// <summary>
    ///     Evaluates an expression against a row of a table.
    /// </summary>
    /// <remarks>
    ///     <c>datum.name</c> reads the field with the canonical name of <c>name</c>; when the table has no such field but
    ///     has fields below it (a flattened nested object, <c>name.child</c>), the nested object is rebuilt from them.
    /// </remarks>
    /// <param name="expression">The compiled expression.</param>
    /// <param name="table">The table.</param>
    /// <param name="row">The zero-based row.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the row is out of range.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit of the evaluation is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public DataValue Evaluate(CompiledExpression expression, DataTable table, int row)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, table.RowCount);
        _mode = DatumMode.Table;
        _table = table;
        _row = row;
        _nested?.Clear();
        return Run(expression);
    }

    /// <summary>
    ///     Evaluates an expression against a datum that is a value.
    /// </summary>
    /// <param name="expression">The compiled expression.</param>
    /// <param name="datum">The datum; <c>datum.name</c> reads the member of an object value and is null for other kinds.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit of the evaluation is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public DataValue Evaluate(CompiledExpression expression, DataValue datum)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _mode = DatumMode.Value;
        _datumValue = datum;
        return Run(expression);
    }

    /// <summary>
    ///     Evaluates an expression against a caller-defined datum.
    /// </summary>
    /// <param name="expression">The compiled expression.</param>
    /// <param name="datum">The datum.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit of the evaluation is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public DataValue Evaluate(CompiledExpression expression, IExpressionDatum datum)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(datum);
        _mode = DatumMode.Custom;
        _custom = datum;
        return Run(expression);
    }

    /// <summary>
    ///     Runs the program of a compiled expression.
    /// </summary>
    /// <param name="expression">The compiled expression.</param>
    /// <returns>The value left on the stack.</returns>
    private DataValue Run(CompiledExpression expression)
    {
        // A canceled call never starts an evaluation
        _context.ThrowIfCanceled();
        if (_stack.Length < expression.MaxStack)
        {
            _stack = new DataValue[Math.Max(expression.MaxStack, _stack.Length * 2)];
        }

        var stack = _stack;
        var program = expression.Program;
        var sp = 0;
        var pc = 0;
        try
        {
            while (pc < program.Length)
            {
                // The instruction is taken first so that a failure in the step accounting names this instruction
                var instruction = program[pc++];
                _context.Step();
                sp = Execute(expression, instruction, stack, sp, ref pc);
            }

            // A text that is the result as it is (a parameter, a field, a literal) never went through a function that checks it,
            // and neither does a text inside an array or object literal, which is checked here at any depth
            var result = stack[0];
            if (result.HasTextLongerThan(_context.MaxStringLength))
            {
                throw new ExpressionRuntimeException(
                    $"The expression produced a string longer than the limit of {_context.MaxStringLength} characters (VegaLiteLimits.MaxStringLength).");
            }

            return result;
        }
        catch (ExpressionRuntimeException ex)
        {
            var offset = program[Math.Max(pc - 1, 0)].Offset;
            throw new VegaLiteRenderException($"{ex.Message.TrimEnd('.')} at expression offset {offset}.", expression.Location);
        }
        finally
        {
            Array.Clear(stack, 0, expression.MaxStack);
            _table = null;
            _nested?.Clear();
            _custom = null;
            _datumValue = default;
        }
    }

    /// <summary>
    ///     Executes one instruction.
    /// </summary>
    /// <param name="expression">The compiled expression.</param>
    /// <param name="instruction">The instruction.</param>
    /// <param name="stack">The value stack.</param>
    /// <param name="sp">The stack height before the instruction.</param>
    /// <param name="pc">The program counter, already advanced past the instruction; jumps replace it.</param>
    /// <returns>The stack height after the instruction.</returns>
    private int Execute(CompiledExpression expression, ExpressionInstruction instruction, DataValue[] stack, int sp, ref int pc)
    {
        var tables = expression.Tables;
        var limit = _context.MaxStringLength;
        switch (instruction.Op)
        {
            case ExpressionOpcode.PushConstant:
                stack[sp++] = tables.Constants[instruction.Operand];
                break;
            case ExpressionOpcode.PushDatum:
                stack[sp++] = DatumAsValue();
                break;
            case ExpressionOpcode.DatumField:
                stack[sp++] = GetDatumField(tables.Names[instruction.Operand]);
                break;
            case ExpressionOpcode.DatumIndex:
                stack[sp - 1] = GetDatumField(KeyToName(stack[sp - 1], limit));
                break;
            case ExpressionOpcode.PushParameter:
                stack[sp++] = _parameters is not null && _parameters.TryGetValue(tables.Names[instruction.Operand], out var parameter)
                    ? parameter
                    : default;
                break;
            case ExpressionOpcode.Member:
                stack[sp - 1] = GetNamed(stack[sp - 1], tables.Names[instruction.Operand]);
                break;
            case ExpressionOpcode.Index:
                sp--;
                stack[sp - 1] = GetByKey(stack[sp - 1], stack[sp], limit);
                break;
            case ExpressionOpcode.JumpIfFalsyKeep:
                if (IsTruthy(stack[sp - 1]))
                {
                    sp--;
                }
                else
                {
                    pc = instruction.Operand;
                }

                break;
            case ExpressionOpcode.JumpIfTruthyKeep:
                if (IsTruthy(stack[sp - 1]))
                {
                    pc = instruction.Operand;
                }
                else
                {
                    sp--;
                }

                break;
            case ExpressionOpcode.JumpIfFalsy:
                sp--;
                if (!IsTruthy(stack[sp]))
                {
                    pc = instruction.Operand;
                }

                break;
            case ExpressionOpcode.Jump:
                pc = instruction.Operand;
                break;
            case ExpressionOpcode.MakeArray:
                sp -= instruction.Argument;
                _context.ChargeSize(instruction.Argument);
                stack[sp] = DataValue.FromOwnedArray(stack.AsSpan(sp, instruction.Argument).ToArray());
                sp++;
                break;
            case ExpressionOpcode.MakeObject:
                sp -= 2 * instruction.Argument;
                _context.ChargeSize(instruction.Argument);
                stack[sp] = DataValue.FromObject(MakeObject(stack, sp, instruction.Argument));
                sp++;
                break;
            case ExpressionOpcode.Call:
                sp -= instruction.Argument;
                stack[sp] = tables.Functions[instruction.Operand](stack.AsSpan(sp, instruction.Argument), _context);
                sp++;
                break;
            case ExpressionOpcode.Unsupported:
                sp -= instruction.Argument;
                stack[sp] = default;
                sp++;
                break;
            default:
                sp = ExecuteOperator(instruction.Op, stack, sp);
                break;
        }

        return sp;
    }

    /// <summary>
    ///     Executes a unary or binary operator.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="stack">The value stack.</param>
    /// <param name="sp">The stack height before the operator.</param>
    /// <returns>The stack height after the operator.</returns>
    private int ExecuteOperator(ExpressionOpcode op, DataValue[] stack, int sp)
    {
        var limit = _context.MaxStringLength;
        switch (op)
        {
            case ExpressionOpcode.Negate:
                stack[sp - 1] = DataValue.FromNumber(-ToNumber(stack[sp - 1]));
                return sp;
            case ExpressionOpcode.Plus:
                stack[sp - 1] = DataValue.FromNumber(ToNumber(stack[sp - 1]));
                return sp;
            case ExpressionOpcode.Not:
                stack[sp - 1] = DataValue.FromBoolean(!IsTruthy(stack[sp - 1]));
                return sp;
            case ExpressionOpcode.BitNot:
                stack[sp - 1] = DataValue.FromNumber(~ToInt32(ToNumber(stack[sp - 1])));
                return sp;
        }

        // Every remaining operator takes two operands and leaves one result
        var right = stack[--sp];
        var left = stack[sp - 1];
        stack[sp - 1] = op switch
        {
            ExpressionOpcode.Add => Add(left, right),
            ExpressionOpcode.Subtract => DataValue.FromNumber(ToNumber(left) - ToNumber(right)),
            ExpressionOpcode.Multiply => DataValue.FromNumber(ToNumber(left) * ToNumber(right)),
            ExpressionOpcode.Divide => DataValue.FromNumber(ToNumber(left) / ToNumber(right)),
            ExpressionOpcode.Modulo => DataValue.FromNumber(ToNumber(left) % ToNumber(right)),
            ExpressionOpcode.StrictEqual => DataValue.FromBoolean(StrictEquals(left, right)),
            ExpressionOpcode.StrictNotEqual => DataValue.FromBoolean(!StrictEquals(left, right)),
            ExpressionOpcode.LooseEqual => DataValue.FromBoolean(LooseEquals(left, right, limit)),
            ExpressionOpcode.LooseNotEqual => DataValue.FromBoolean(!LooseEquals(left, right, limit)),
            ExpressionOpcode.Less => DataValue.FromBoolean(RelationalLess(left, right, limit) == 1),
            ExpressionOpcode.Greater => DataValue.FromBoolean(RelationalLess(right, left, limit) == 1),
            ExpressionOpcode.LessEqual => DataValue.FromBoolean(RelationalLess(right, left, limit) == 0),
            ExpressionOpcode.GreaterEqual => DataValue.FromBoolean(RelationalLess(left, right, limit) == 0),
            _ => throw new InvalidOperationException($"Unexpected opcode {op}."),
        };
        return sp;
    }

    /// <summary>
    ///     Adds two values: string concatenation when either primitive is a string, numeric addition otherwise.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum or the concatenation.</returns>
    private DataValue Add(DataValue left, DataValue right)
    {
        if (left.IsNumber && right.IsNumber)
        {
            return DataValue.FromNumber(left.Number + right.Number);
        }

        var limit = _context.MaxStringLength;
        var a = ToPrimitive(left, false, limit);
        var b = ToPrimitive(right, false, limit);
        if (!a.IsString && !b.IsString)
        {
            return DataValue.FromNumber(ToNumber(a) + ToNumber(b));
        }

        var first = ToJsString(a, limit);
        var second = ToJsString(b, limit);
        var length = (long)first.Length + second.Length;
        _context.CheckStringLength(length);
        _context.ChargeSize(length);
        return DataValue.FromString(string.Concat(first, second));
    }

    /// <summary>
    ///     Builds an object from key/value pairs on the stack.
    /// </summary>
    /// <param name="stack">The value stack.</param>
    /// <param name="start">The position of the first key.</param>
    /// <param name="count">The number of pairs.</param>
    /// <returns>The object.</returns>
    private static DataObject MakeObject(DataValue[] stack, int start, int count)
    {
        var members = new KeyValuePair<string, DataValue>[count];
        for (var i = 0; i < count; i++)
        {
            members[i] = new KeyValuePair<string, DataValue>(stack[start + (2 * i)].Text!, stack[start + (2 * i) + 1]);
        }

        return DataObject.Create(members);
    }

    /// <summary>
    ///     Gets a member of a value by name.
    /// </summary>
    /// <param name="target">The value.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The member; null when the value has no such member.</returns>
    private static DataValue GetNamed(DataValue target, string name)
    {
        switch (target.Kind)
        {
            case DataValueKind.Object:
                return target.ObjectValue!.TryGetValue(name, out var member) ? member : default;
            case DataValueKind.Array:
                if (name == "length")
                {
                    return DataValue.FromNumber(target.Elements.Length);
                }

                return TryParseIndex(name, out var index) && index < target.Elements.Length ? target.Elements[index] : default;
            case DataValueKind.String:
                if (name == "length")
                {
                    return DataValue.FromNumber(target.Text!.Length);
                }

                return TryParseIndex(name, out var position) && position < target.Text!.Length
                    ? DataValue.FromString(target.Text[position].ToString())
                    : default;
            case DataValueKind.RegExp:
                return RegExpMember((ExpressionRegex)target.RegExpPayload!, name);
            default:
                return default;
        }
    }

    /// <summary>
    ///     Gets a property of a regular expression.
    /// </summary>
    /// <param name="regex">The regular expression.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The property; null for an unknown name.</returns>
    private static DataValue RegExpMember(ExpressionRegex regex, string name) => name switch
    {
        "source" => DataValue.FromString(regex.Source),
        "flags" => DataValue.FromString(regex.Flags),
        "global" => DataValue.FromBoolean(regex.IsGlobal),
        _ => default,
    };

    /// <summary>
    ///     Gets a member of a value by a key computed at runtime.
    /// </summary>
    /// <param name="target">The value.</param>
    /// <param name="key">The key: an index for arrays and strings, a name otherwise.</param>
    /// <param name="maxLength">The longest key text allowed.</param>
    /// <returns>The member; null when there is none.</returns>
    private static DataValue GetByKey(DataValue target, DataValue key, int maxLength)
    {
        if (key.IsNumber && (target.IsArray || target.IsString))
        {
            var number = key.Number;
            var length = target.IsArray ? target.Elements.Length : target.Text!.Length;
            if (number < 0 || number >= length || Math.Floor(number).CompareTo(number) != 0)
            {
                return default;
            }

            var position = (int)number;
            return target.IsArray ? target.Elements[position] : DataValue.FromString(target.Text![position].ToString());
        }

        return GetNamed(target, KeyToName(key, maxLength));
    }

    /// <summary>
    ///     Converts a key to a property name the way JavaScript does.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="maxLength">The longest text allowed.</param>
    /// <returns>The name.</returns>
    private static string KeyToName(DataValue key, int maxLength) => ToJsString(key, maxLength);

    /// <summary>
    ///     Parses a canonical array index.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="index">Receives the index.</param>
    /// <returns><see langword="true"/> when the name is a non-negative integer without leading zeros.</returns>
    private static bool TryParseIndex(string name, out int index)
    {
        index = 0;
        if (name.Length == 0 || name.Length > 9 || (name.Length > 1 && name[0] == '0'))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }

            index = (index * 10) + (c - '0');
        }

        return true;
    }
}

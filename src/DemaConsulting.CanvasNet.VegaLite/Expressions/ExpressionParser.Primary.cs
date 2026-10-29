using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The primary and postfix half of the expression parser (a partial file of the parser unit).
/// </summary>
/// <remarks>
///     A primary expression is read together with what must be decided after it: an identifier is a function call,
///     a field access of <c>datum</c> or a parameter reference only once the next token is known, so the identifier is
///     held back (<see cref="Pending"/>) until then. This lets <c>datum.x</c> compile to a single field read instead of
///     materializing the whole row, and lets <c>name(args)</c> choose between a function of the closed table, a view
///     state construct and an unsupported call.
/// </remarks>
internal sealed partial class ExpressionParser
{
    /// <summary>
    ///     Parses a primary expression followed by member accesses, index accesses and calls.
    /// </summary>
    private void ParsePostfix()
    {
        var pending = Pending.None;
        var name = string.Empty;
        var nameOffset = Current.Offset;
        if (Current.Kind == ExpressionTokenKind.Identifier)
        {
            pending = ParseIdentifier(out name);
        }
        else
        {
            ParsePrimary();
        }

        while (true)
        {
            var token = Current;
            if (IsPunctuator("."))
            {
                Advance();
                if (Current.Kind != ExpressionTokenKind.Identifier)
                {
                    throw Fail(Current.Offset, $"expected a property name after '.' but found {Describe(Current)}");
                }

                var property = Current;
                Advance();
                if (pending == Pending.Datum)
                {
                    _referencesDatum = true;
                    Emit(ExpressionOpcode.DatumField, AddName(property.Text), 0, token.Offset, 1);
                    pending = Pending.None;
                }
                else
                {
                    pending = FlushPending(pending, name, nameOffset);
                    Emit(ExpressionOpcode.Member, AddName(property.Text), 0, token.Offset, 0);
                }
            }
            else if (IsPunctuator("["))
            {
                pending = ParseIndex(pending, name, nameOffset);
            }
            else if (IsPunctuator("("))
            {
                pending = ParseCall(pending, name, nameOffset);
            }
            else
            {
                break;
            }
        }

        FlushPending(pending, name, nameOffset);
    }

    /// <summary>
    ///     Parses a bracketed index access.
    /// </summary>
    /// <param name="pending">What was read before the bracket.</param>
    /// <param name="name">The pending identifier.</param>
    /// <param name="nameOffset">The offset of the pending identifier.</param>
    /// <returns>The pending state after the access.</returns>
    private Pending ParseIndex(Pending pending, string name, int nameOffset)
    {
        var open = Current.Offset;
        Advance();
        if (pending == Pending.Datum)
        {
            // The key is computed first and the datum is never materialized
            _referencesDatum = true;
            Enter(open);
            ParseTernary();
            Leave();
            Expect("]");
            Emit(ExpressionOpcode.DatumIndex, 0, 0, open, 0);
            return Pending.None;
        }

        pending = FlushPending(pending, name, nameOffset);
        Enter(open);
        ParseTernary();
        Leave();
        Expect("]");
        Emit(ExpressionOpcode.Index, 0, 0, open, -1);
        return pending;
    }

    /// <summary>
    ///     Parses the argument list of a call.
    /// </summary>
    /// <param name="pending">What was read before the parenthesis.</param>
    /// <param name="name">The pending identifier.</param>
    /// <param name="nameOffset">The offset of the pending identifier.</param>
    /// <returns>The pending state after the call.</returns>
    private Pending ParseCall(Pending pending, string name, int nameOffset)
    {
        var open = Current.Offset;
        if (pending != Pending.Name)
        {
            throw Fail(open, "only a named function can be called; a method call such as a.b() is not supported");
        }

        var count = ParseArguments();
        if (ViewStateNames.Contains(name))
        {
            RecordConstruct(ExpressionConstructKind.ViewState, name, nameOffset);
            Emit(ExpressionOpcode.Unsupported, 0, count, nameOffset, 1 - count);
        }
        else if (ExpressionFunctions.TryGet(name, out var function))
        {
            if (!_functionIndex.TryGetValue(name, out var index))
            {
                index = _functions.Count;
                _functions.Add(function);
                _functionIndex[name] = index;
            }

            Emit(ExpressionOpcode.Call, index, count, nameOffset, 1 - count);
        }
        else
        {
            RecordConstruct(ExpressionConstructKind.Function, name, nameOffset);
            Emit(ExpressionOpcode.Unsupported, 0, count, nameOffset, 1 - count);
        }

        return Pending.None;
    }

    /// <summary>
    ///     Parses a parenthesized, comma-separated argument list and emits the arguments.
    /// </summary>
    /// <returns>The number of arguments.</returns>
    private int ParseArguments()
    {
        var open = Current.Offset;
        Advance();
        Enter(open);
        var count = 0;
        if (!IsPunctuator(")"))
        {
            while (true)
            {
                ParseTernary();
                count++;
                if (!IsPunctuator(","))
                {
                    break;
                }

                Advance();
            }
        }

        Expect(")");
        Leave();
        return count;
    }

    /// <summary>
    ///     Classifies an identifier primary: a literal keyword, a constant, <c>datum</c>, or a name held back.
    /// </summary>
    /// <param name="name">Receives the identifier text.</param>
    /// <returns>What is pending after the identifier.</returns>
    private Pending ParseIdentifier(out string name)
    {
        var token = Current;
        name = token.Text;
        switch (token.Text)
        {
            case "null":
                EmitConstant(default, token.Offset);
                break;
            case "true":
                EmitConstant(DataValue.FromBoolean(true), token.Offset);
                break;
            case "false":
                EmitConstant(DataValue.FromBoolean(false), token.Offset);
                break;
            case "NaN":
                EmitConstant(DataValue.FromNumber(double.NaN), token.Offset);
                break;
            case "Infinity":
                EmitConstant(DataValue.FromNumber(double.PositiveInfinity), token.Offset);
                break;
            case "PI":
                EmitConstant(DataValue.FromNumber(Math.PI), token.Offset);
                break;
            case "E":
                EmitConstant(DataValue.FromNumber(Math.E), token.Offset);
                break;
            case "datum":
                Advance();
                return Pending.Datum;
            default:
                if (ReservedWords.Contains(token.Text))
                {
                    throw Fail(token.Offset, $"the word '{token.Text}' is not supported in expressions");
                }

                Advance();
                return Pending.Name;
        }

        Advance();
        return Pending.None;
    }

    /// <summary>
    ///     Emits the instruction for an identifier that turned out to be a value.
    /// </summary>
    /// <param name="pending">What is pending.</param>
    /// <param name="name">The pending identifier.</param>
    /// <param name="offset">The offset of the identifier.</param>
    /// <returns>Always <see cref="Pending.None"/>.</returns>
    private Pending FlushPending(Pending pending, string name, int offset)
    {
        switch (pending)
        {
            case Pending.Datum:
                _referencesDatum = true;
                Emit(ExpressionOpcode.PushDatum, 0, 0, offset, 1);
                break;
            case Pending.Name when ViewStateNames.Contains(name):
                RecordConstruct(ExpressionConstructKind.ViewState, name, offset);
                Emit(ExpressionOpcode.Unsupported, 0, 0, offset, 1);
                break;
            case Pending.Name:
                if (!_parameters.Exists(p => string.Equals(p.Name, name, StringComparison.Ordinal)))
                {
                    _parameters.Add(new ExpressionReference(name, offset));
                }

                Emit(ExpressionOpcode.PushParameter, AddName(name), 0, offset, 1);
                break;
        }

        return Pending.None;
    }

    /// <summary>
    ///     Parses a primary expression that is not an identifier.
    /// </summary>
    private void ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case ExpressionTokenKind.Number:
                Advance();
                EmitConstant(DataValue.FromNumber(token.Number), token.Offset);
                break;
            case ExpressionTokenKind.String:
                Advance();
                EmitConstant(DataValue.FromString(token.Text), token.Offset);
                break;
            case ExpressionTokenKind.RegExp:
                Advance();
                EmitConstant(DataValue.FromRegExp(CreateRegExp(token)), token.Offset);
                break;
            case ExpressionTokenKind.Punctuator when token.Text == "(":
                Advance();
                Enter(token.Offset);
                ParseTernary();
                Expect(")");
                Leave();
                break;
            case ExpressionTokenKind.Punctuator when token.Text == "[":
                ParseArray();
                break;
            case ExpressionTokenKind.Punctuator when token.Text == "{":
                ParseObject();
                break;
            default:
                throw Fail(token.Offset, $"unexpected {Describe(token)}");
        }
    }

    /// <summary>
    ///     Compiles the regular expression of a literal, turning a failure into a located error.
    /// </summary>
    /// <param name="token">The regular expression token.</param>
    /// <returns>The regular expression.</returns>
    private ExpressionRegex CreateRegExp(ExpressionToken token)
    {
        try
        {
            return ExpressionRegex.Create(token.Text, token.Flags);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw Fail(token.Offset, ex.Message.TrimEnd('.'));
        }
    }

    /// <summary>
    ///     Parses an array literal.
    /// </summary>
    private void ParseArray()
    {
        var open = Current.Offset;
        Advance();
        Enter(open);
        var count = 0;
        while (!IsPunctuator("]"))
        {
            ParseTernary();
            count++;
            if (!IsPunctuator(","))
            {
                break;
            }

            Advance();
        }

        Expect("]");
        Leave();
        Emit(ExpressionOpcode.MakeArray, 0, count, open, 1 - count);
    }

    /// <summary>
    ///     Parses an object literal.
    /// </summary>
    private void ParseObject()
    {
        var open = Current.Offset;
        Advance();
        Enter(open);
        var count = 0;
        while (!IsPunctuator("}"))
        {
            var key = Current;
            var name = key.Kind switch
            {
                ExpressionTokenKind.Identifier or ExpressionTokenKind.String => key.Text,
                ExpressionTokenKind.Number => JsNumber.ToString(key.Number),
                _ => throw Fail(key.Offset, $"expected a property name but found {Describe(key)}"),
            };
            Advance();
            Expect(":");
            EmitConstant(DataValue.FromString(name), key.Offset);
            ParseTernary();
            count++;
            if (!IsPunctuator(","))
            {
                break;
            }

            Advance();
        }

        Expect("}");
        Leave();
        Emit(ExpressionOpcode.MakeObject, 0, count, open, 1 - (2 * count));
    }
}

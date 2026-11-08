// Portions derived from Vega-Lite (BSD-3-Clause): the meaning of the field predicates (equal, lt, lte, gt, gte, range, oneOf, valid); see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     Writes a Vega-Lite predicate (the <c>test</c> of a condition) as the source text of an equivalent expression.
/// </summary>
/// <remarks>
///     <para>
///         A <c>test</c> is an expression text or a predicate object: a field predicate (<c>equal</c>, <c>lt</c>, <c>lte</c>, <c>gt</c>, <c>gte</c>,
///         <c>range</c>, <c>oneOf</c>, <c>valid</c>), a composition (<c>and</c>, <c>or</c>, <c>not</c>), or a variable parameter. The conditions of
///         the marks are evaluated by the expression engine, so a predicate object is turned into the expression that means the same (a field
///         predicate on <c>x</c> with <c>gt</c> 3 is <c>datum["x"] &gt; 3</c>), which the parser compiles once and the marks evaluate for every row
///         under the limits and the step budget of the engine.
///     </para>
///     <para>
///         A predicate that no expression can say is not written: a parameter that is not declared, or a variable parameter whose name is not an identifier. A selection is written as the text that
///         Vega-Lite compiles for it (<c>!length(data("brush_store")) || vlSelectionTest("brush_store", datum)</c>), which the selection functions of the expression engine evaluate. A <c>timeUnit</c> and a comparison with a date-time
///         object are written with the date and time functions (<c>time(datetime(year(datum["d"]), month(datum["d"]), 1, 0, 0, 0, 0))</c>, the way Vega-Lite's
///         <c>fieldFilterExpression</c> does), so they are evaluated in the calendar of the chart; the parts <c>week</c> and <c>dayofyear</c> of a time unit are
///         not written (Vega-Lite does not either). The class is static and pure.
///     </para>
/// </remarks>
internal static class PredicateSource
{
    /// <summary>The comparison operators of a field predicate, in the order they are looked for.</summary>
    private static readonly string[] Operators = ["equal", "lt", "lte", "gt", "gte", "range", "oneOf", "valid"];

    /// <summary>
    ///     Writes a predicate as expression source.
    /// </summary>
    /// <param name="predicate">The <c>test</c> value: an expression text or a predicate object that the parser has validated.</param>
    /// <param name="source">Receives the expression source; empty when the predicate cannot be written.</param>
    /// <returns><see langword="true"/> when the whole predicate could be written as an expression.</returns>
    public static bool TryBuild(SpecNode predicate, out string source) =>
        Build(predicate, int.MaxValue, out source, out _) == Status.Written;

    /// <summary>
    ///     Writes a predicate as expression source, counting the characters as they are written and refusing a text that passes a budget before it is allocated.
    /// </summary>
    /// <remarks>
    ///     The text of a membership list repeats the expression that reads the field once per member, so a long field name and a large list multiply: the product is not bounded by the
    ///     document. The writer keeps the text only while it is within the budget; past it, it drops what it has and only counts, so the exact length of the text that would have been
    ///     written is reported without ever allocating it.
    /// </remarks>
    /// <param name="predicate">The <c>test</c> value: an expression text or a predicate object that the parser has validated.</param>
    /// <param name="budget">The most characters that the text may have.</param>
    /// <param name="source">Receives the expression source; empty unless the result is <see cref="Status.Written"/>.</param>
    /// <param name="length">Receives the number of characters of the text (of what was written until a part that cannot be written).</param>
    /// <returns>
    ///     <see cref="Status.Unsupported"/> when a part cannot be written (this wins over a text that is too long), <see cref="Status.TooLong"/> when the text has more characters than
    ///     the budget, else <see cref="Status.Written"/>.
    /// </returns>
    public static Status Build(SpecNode predicate, long budget, out string source, out long length)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var text = new SourceWriter(budget);
        var written = AppendPredicate(predicate, text);
        length = text.Length;
        source = written && !text.Overflowed ? text.ToString() : string.Empty;
        if (!written)
        {
            return Status.Unsupported;
        }

        return text.Overflowed ? Status.TooLong : Status.Written;
    }

    /// <summary>
    ///     Tells whether a predicate has a part that an expression cannot say (an undeclared parameter, a variable parameter whose name is not an identifier, a selection of an unknown type, or a value that is not a valid date-time object).
    /// </summary>
    /// <remarks>The text is only counted (the budget is zero), so a predicate with a large expansion costs no memory to ask.</remarks>
    /// <param name="predicate">The <c>test</c> value.</param>
    /// <returns><see langword="true"/> when <see cref="TryBuild"/> would fail.</returns>
    public static bool IsUnsupported(SpecNode predicate) => Build(predicate, 0, out _, out _) == Status.Unsupported;

    /// <summary>The outcome of <see cref="Build"/>.</summary>
    internal enum Status
    {
        /// <summary>A part of the predicate cannot be written as an expression.</summary>
        Unsupported,

        /// <summary>The predicate was written within the budget.</summary>
        Written,

        /// <summary>The text of the predicate has more characters than the budget.</summary>
        TooLong,
    }

    /// <summary>
    ///     Collects the text of an expression while counting its characters: the text is kept up to a budget, then only counted.
    /// </summary>
    /// <param name="budget">The most characters to keep.</param>
    private sealed class SourceWriter(long budget)
    {
        /// <summary>The text so far, or null once the budget has been passed.</summary>
        private StringBuilder? _text = new();

        /// <summary>Gets the number of characters written, counted also after the budget has been passed.</summary>
        public long Length { get; private set; }

        /// <summary>Gets a value indicating whether more characters than the budget were written (the text was dropped).</summary>
        public bool Overflowed => _text is null;

        /// <summary>
        ///     Writes a text.
        /// </summary>
        /// <param name="value">The text; nothing for null.</param>
        /// <returns>The writer.</returns>
        public SourceWriter Append(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Length += value.Length;
                if (Length > budget)
                {
                    _text = null;
                }

                _text?.Append(value);
            }

            return this;
        }

        /// <summary>
        ///     Writes a character.
        /// </summary>
        /// <param name="value">The character.</param>
        /// <returns>The writer.</returns>
        public SourceWriter Append(char value)
        {
            Length++;
            if (Length > budget)
            {
                _text = null;
            }

            _text?.Append(value);
            return this;
        }

        /// <inheritdoc/>
        public override string ToString() => _text?.ToString() ?? string.Empty;
    }

    /// <summary>
    ///     Writes one predicate.
    /// </summary>
    /// <param name="node">The predicate.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> when a part cannot be written.</returns>
    private static bool AppendPredicate(SpecNode node, SourceWriter text)
    {
        if (node.IsString)
        {
            text.Append('(').Append(node.StringValue).Append(')');
            return true;
        }

        if (!node.IsObject)
        {
            return false;
        }

        foreach (var composition in new[] { "and", "or" })
        {
            if (node[composition] is { IsArray: true } items)
            {
                return AppendComposition(items, composition == "and", text);
            }
        }

        if (node["not"] is { } negated)
        {
            text.Append("!(");
            var inner = AppendPredicate(negated, text);
            text.Append(')');
            return inner;
        }

        if (node["param"] is not null)
        {
            return AppendParameter(node, text);
        }

        if (node["selection"] is { } legacy)
        {
            return AppendLegacySelection(legacy, text);
        }

        return AppendField(node, text);
    }

    /// <summary>
    ///     Writes an <c>and</c> or an <c>or</c> of predicates.
    /// </summary>
    /// <param name="items">The array of predicates.</param>
    /// <param name="all"><see langword="true"/> for <c>and</c>.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> when a part cannot be written.</returns>
    private static bool AppendComposition(SpecNode items, bool all, SourceWriter text)
    {
        text.Append('(');
        if (items.Items.Count == 0)
        {
            text.Append(all ? "true" : "false");
        }

        for (var i = 0; i < items.Items.Count; i++)
        {
            if (i > 0)
            {
                text.Append(all ? " && " : " || ");
            }

            if (!AppendPredicate(items.Items[i], text))
            {
                return false;
            }
        }

        text.Append(')');
        return true;
    }

    /// <summary>
    ///     Writes a predicate on a parameter: a variable parameter as its name (a truthiness test), a selection parameter as the text that Vega-Lite compiles for it.
    /// </summary>
    /// <remarks>
    ///     A selection is in when its store has no tuple (<c>empty</c> is true, the default) or the datum is in a tuple, so the text is
    ///     <c>(!length(data("brush_store")) || vlSelectionTest("brush_store", datum))</c>; with <c>empty: false</c> an empty selection selects nothing:
    ///     <c>(length(data("brush_store")) &amp;&amp; vlSelectionTest("brush_store", datum))</c>. A selection that does not combine its units globally passes its
    ///     <c>resolve</c> as the third argument, and a point selection over the identity of the rows uses <c>vlSelectionIdTest</c>.
    /// </remarks>
    /// <param name="owner">The object that holds <c>param</c> and, optionally, <c>empty</c>: a predicate or a condition.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> for an undeclared parameter, or a variable parameter whose name is not an identifier.</returns>
    private static bool AppendParameter(SpecNode owner, SourceWriter text)
    {
        var parameter = owner["param"]!;
        return parameter.IsString
            && AppendNamed(parameter, parameter.StringValue!, owner["empty"] is { IsBoolean: true, BoolValue: false }, text);
    }

    /// <summary>
    ///     Writes the predicate on a parameter that is named.
    /// </summary>
    /// <param name="at">The node whose scope decides which parameters are visible.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="emptyIsFalse">Whether an empty selection selects nothing (<c>empty: false</c>).</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> for an undeclared name, or a variable parameter whose name is not an identifier.</returns>
    private static bool AppendNamed(SpecNode at, string name, bool emptyIsFalse, SourceWriter text)
    {
        if (SpecParser.FindParameter(at, name) is not { } declaration)
        {
            return false;
        }

        if (!SelectionDeclarations.IsSelection(declaration))
        {
            if (!IsIdentifier(name))
            {
                return false;
            }

            text.Append('(').Append(name).Append(')');
            return true;
        }

        if (SelectionDeclarations.TypeOf(declaration) is not { } type)
        {
            return false;
        }

        var store = "\"" + SelectionDeclarations.VarName(name) + "_store\"";
        var function = SelectionDeclarations.UsesRowIdentity(declaration) ? "vlSelectionIdTest(" : "vlSelectionTest(";
        var resolve = SelectionDeclarations.ResolveOf(declaration, type);
        var test = function + store + ", datum" + (resolve == "global" ? ")" : ", \"" + resolve + "\")");
        var length = "length(data(" + store + "))";
        if (emptyIsFalse || SelectionDeclarations.EmptyIsNone(declaration))
        {
            text.Append('(').Append(length).Append(" && ").Append(test).Append(')');
        }
        else
        {
            text.Append("(!").Append(length).Append(" || ").Append(test).Append(')');
        }

        return true;
    }

    /// <summary>
    ///     Writes a predicate of the legacy form <c>{"selection": ...}</c>: the name of a selection, or a logical composition of names.
    /// </summary>
    /// <param name="selection">The value of <c>selection</c>.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> when a name is not a declared parameter.</returns>
    private static bool AppendLegacySelection(SpecNode selection, SourceWriter text)
    {
        if (selection.IsString)
        {
            return AppendNamed(selection, selection.StringValue!, false, text);
        }

        if (!selection.IsObject)
        {
            return false;
        }

        foreach (var composition in new[] { "and", "or" })
        {
            if (selection[composition] is { IsArray: true } items)
            {
                text.Append('(');
                if (items.Items.Count == 0)
                {
                    text.Append(composition == "and" ? "true" : "false");
                }

                for (var i = 0; i < items.Items.Count; i++)
                {
                    if (i > 0)
                    {
                        text.Append(composition == "and" ? " && " : " || ");
                    }

                    if (!AppendLegacySelection(items.Items[i], text))
                    {
                        return false;
                    }
                }

                text.Append(')');
                return true;
            }
        }

        if (selection["not"] is { } negated)
        {
            text.Append("!(");
            var inner = AppendLegacySelection(negated, text);
            text.Append(')');
            return inner;
        }

        return false;
    }

    /// <summary>
    ///     Tells whether a field predicate is written as an expression: it has a time unit that keeps a part, or it compares with an object (a date-time
    ///     object, or an expression reference).
    /// </summary>
    /// <remarks>
    ///     The predicates of this kind cannot be evaluated natively because the calendar of the chart decides what a date-time means (and a reference is an expression), so the
    ///     parser compiles the expression that <see cref="TryBuild"/> writes for them. A <c>valid</c> test and a comparison with plain values are
    ///     not of this kind.
    /// </remarks>
    /// <param name="predicate">The predicate object.</param>
    /// <returns><see langword="true"/> for a time unit predicate or a comparison with an object.</returns>
    public static bool IsTimeAware(SpecNode predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (!predicate.IsObject || predicate["field"] is null || FirstComparison(predicate) is not { } comparison || comparison.Name == "valid")
        {
            return false;
        }

        return ReadUnit(predicate) is not null || comparison.IsObject || comparison.Items.Any(i => i.IsObject);
    }

    /// <summary>
    ///     Reads the time unit of a predicate when it keeps a part: a unit with parts, including a <c>binned</c> one (whose field is compared as it is, the data being floored already);
    ///     the <c>maxbins</c> form keeps none and is ignored (the field is compared as it is, as Vega-Lite's <c>fieldFilterExpression</c> does for a unit that names no <c>unit</c>).
    /// </summary>
    /// <param name="predicate">The predicate object.</param>
    /// <returns>The unit, or null when the predicate has none that applies.</returns>
    public static TimeUnitSpec? ReadUnit(SpecNode predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (predicate["timeUnit"] is not { IsNull: false } node || !TimeUnitSpec.TryRead(node, out var parsed))
        {
            return null;
        }

        return parsed.Parts.Count > 0 ? parsed : null;
    }

    /// <summary>
    ///     Finds the comparison of a field predicate.
    /// </summary>
    /// <param name="predicate">The predicate object.</param>
    /// <returns>The first of the comparison operators that the predicate has, or null.</returns>
    private static SpecNode? FirstComparison(SpecNode predicate) => Operators.Select(name => predicate[name]).FirstOrDefault(n => n is not null);

    /// <summary>
    ///     Tells whether a comparison value (or an array of them) holds an object that is not an expression reference.
    /// </summary>
    /// <param name="comparison">The value of the operator.</param>
    /// <returns><see langword="true"/> when a date-time object is compared.</returns>
    private static bool HasDateObject(SpecNode comparison) =>
        IsDateObject(comparison) || comparison.Items.Any(IsDateObject);

    /// <summary>
    ///     Tells whether a value is an object that is not an expression reference.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns><see langword="true"/> for a date-time object.</returns>
    private static bool IsDateObject(SpecNode node) => node.IsObject && node["expr"] is not { IsString: true };

    /// <summary>
    ///     Writes a field predicate.
    /// </summary>
    /// <param name="node">The predicate object.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> for a field that is not a path or a value that cannot be written.</returns>
    private static bool AppendField(SpecNode node, SourceWriter text)
    {
        if (node["field"] is not { IsString: true } field || FieldPath.Parse(field.StringValue) is not { } segments)
        {
            return false;
        }

        var comparison = FirstComparison(node);
        if (comparison is null)
        {
            return false;
        }

        var path = new StringBuilder("datum");
        foreach (var segment in segments)
        {
            path.Append('[').Append(Quote(segment)).Append(']');
        }

        var value = path.ToString();
        if (comparison.Name == "valid")
        {
            text.Append(comparison.BoolValue ? string.Empty : "!").Append("isValid(").Append(value).Append(')');
            return true;
        }

        var unit = ReadUnit(node);
        if (unit is not null || HasDateObject(comparison))
        {
            return AppendTime(comparison, unit, value, text);
        }

        switch (comparison.Name)
        {
            case "equal":
                text.Append('(').Append(value).Append(" === ").Append(Literal(comparison)).Append(')');
                return true;
            case "lt":
                text.Append('(').Append(value).Append(" < ").Append(Literal(comparison)).Append(')');
                return true;
            case "lte":
                text.Append('(').Append(value).Append(" <= ").Append(Literal(comparison)).Append(')');
                return true;
            case "gt":
                text.Append('(').Append(value).Append(" > ").Append(Literal(comparison)).Append(')');
                return true;
            case "gte":
                text.Append('(').Append(value).Append(" >= ").Append(Literal(comparison)).Append(')');
                return true;
            case "range":
                AppendRange(value, comparison, text);
                return true;
            default:
                AppendOneOf(value, comparison, text);
                return true;
        }
    }

    /// <summary>
    ///     Writes a comparison of the instant of a field (a time unit of it, or the date itself) with date values (Vega-Lite's <c>fieldFilterExpression</c>).
    /// </summary>
    /// <remarks>
    ///     The left side is <c>time(datetime(year(f), month(f), ...))</c> with the parts of the unit (the <c>utc</c> functions and <c>utc(...)</c> for a UTC
    ///     unit; Vega-Lite itself ignores the UTC flag here), the parts <c>week</c> and <c>dayofyear</c> are not written (as in Vega-Lite) and the
    ///     others default as a <c>datetime</c> object does (year 2012, January, the first). Without a unit it is <c>time(f)</c>. A range is written with
    ///     <c>min</c> and <c>max</c> so that reversed ends are accepted, and a membership test with <c>indexof</c>.
    /// </remarks>
    /// <param name="comparison">The operator node.</param>
    /// <param name="unit">The time unit that applies, or null.</param>
    /// <param name="field">The expression that reads the field.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> when a value cannot be written.</returns>
    private static bool AppendTime(SpecNode comparison, TimeUnitSpec? unit, string field, SourceWriter text)
    {
        var left = unit is null ? "time(" + field + ")" : UnitInstant(unit, field);
        switch (comparison.Name)
        {
            case "range":
                return AppendTimeRange(left, comparison, unit, text);
            case "oneOf":
                text.Append("(indexof([");
                for (var i = 0; i < comparison.Items.Count; i++)
                {
                    if (!TimeOperand(comparison.Items[i], unit, out var member))
                    {
                        return false;
                    }

                    text.Append(i > 0 ? ", " : string.Empty).Append(member);
                }

                text.Append("], ").Append(left).Append(") !== -1)");
                return true;
            default:
                if (!TimeOperand(comparison, unit, out var operand))
                {
                    return false;
                }

                var op = comparison.Name switch
                {
                    "equal" => "===",
                    "lt" => "<",
                    "lte" => "<=",
                    "gt" => ">",
                    _ => ">=",
                };
                text.Append('(').Append(left).Append(op).Append(operand).Append(')');
                return true;
        }
    }

    /// <summary>
    ///     Writes an inclusive range of instants; an end that is null is open and reversed ends are accepted.
    /// </summary>
    /// <param name="left">The instant of the field.</param>
    /// <param name="range">The array of the two ends.</param>
    /// <param name="unit">The time unit that applies, or null.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> when an end cannot be written.</returns>
    private static bool AppendTimeRange(string left, SpecNode range, TimeUnitSpec? unit, SourceWriter text)
    {
        var lowNode = range.Items[0];
        var highNode = range.Items[1];
        string? low = null;
        string? high = null;
        if ((!lowNode.IsNull && !TimeOperand(lowNode, unit, out low)) || (!highNode.IsNull && !TimeOperand(highNode, unit, out high)))
        {
            return false;
        }

        if (low is not null && high is not null)
        {
            text.Append('(').Append(left).Append(" >= min(").Append(low).Append(", ").Append(high).Append(") && ")
                .Append(left).Append(" <= max(").Append(low).Append(", ").Append(high).Append("))");
        }
        else if (low is not null || high is not null)
        {
            text.Append('(').Append(left).Append(low is not null ? " >= " : " <= ").Append(low ?? high).Append(')');
        }
        else
        {
            text.Append("(true)");
        }

        return true;
    }

    /// <summary>
    ///     Writes the instant of the part of a field that a time unit keeps: <c>time(datetime(...))</c>.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <param name="field">The expression that reads the field.</param>
    /// <returns>The expression.</returns>
    private static string UnitInstant(TimeUnitSpec unit, string field)
    {
        // A binned unit's field holds the start of the unit already: Vega-Lite compares time(rawField) (fieldFilterExpression, src/predicate.ts, binned branch)
        if (unit.IsBinned)
        {
            return "time(" + field + ")";
        }

        var prefix = unit.Utc ? "utc" : string.Empty;
        string Part(string name) => name == "quarter" ? "(" + prefix + "quarter(" + field + ")-1)" : prefix + name + "(" + field + ")";
        bool Has(string name) => unit.Parts.Contains(name);
        string Choose(string first, string second, string fallback)
        {
            if (Has(first))
            {
                return Part(first);
            }

            if (!Has(second))
            {
                return fallback;
            }

            return second == "quarter" ? Part(second) + "*3" : Part(second) + "+1";
        }

        var parts = new[]
        {
            Has("year") ? Part("year") : "2012",
            Choose("month", "quarter", "0"),
            Choose("date", "day", "1"),
            Has("hours") ? Part("hours") : "0",
            Has("minutes") ? Part("minutes") : "0",
            Has("seconds") ? Part("seconds") : "0",
            Has("milliseconds") ? Part("milliseconds") : "0",
        };
        return "time(" + (unit.Utc ? "utc(" : "datetime(") + string.Join(", ", parts) + "))";
    }

    /// <summary>
    ///     Writes a value that a date is compared with as an instant (Vega-Lite's <c>valueExpr</c> with <c>time</c>).
    /// </summary>
    /// <remarks>
    ///     A date-time object is its <c>datetime(...)</c> call. A number or a text compared with a single local unit is the part of the unit when it is
    ///     below 10000 (a number) or is not a date (a text, a month or day name), else a number is milliseconds since the epoch and a text is read as a date.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="unit">The time unit that applies, or null.</param>
    /// <param name="operand">Receives the expression.</param>
    /// <returns><see langword="false"/> when the value is a date-time object that is not valid.</returns>
    private static bool TimeOperand(SpecNode value, TimeUnitSpec? unit, out string operand)
    {
        operand = string.Empty;
        switch (value.Kind)
        {
            case SpecNodeKind.Object when value["expr"] is { IsString: true } expression:
                operand = "time(" + expression.StringValue + ")";
                return true;
            case SpecNodeKind.Object:
                if (!DateTimeObject.TryRead(value, out var date, out _, out _))
                {
                    return false;
                }

                operand = "time(" + date.ToExpression() + ")";
                return true;
            case SpecNodeKind.Number:
                if (IsSingleUnit(unit) && value.NumberValue < 10000)
                {
                    return DateTimeObject.TryUnitPartExpression(unit!.Parts[0], value.NumberValue, null, out var part) && Wrap(part, out operand);
                }

                operand = Literal(value);
                return true;
            case SpecNodeKind.String:
                var text = value.StringValue!;
                if (!JsDateParser.TryParse(text, TimeCalendar.Utc, out _))
                {
                    if (IsSingleUnit(unit) && DateTimeObject.TryUnitPartExpression(unit!.Parts[0], double.NaN, text, out var named))
                    {
                        return Wrap(named, out operand);
                    }

                    operand = "time(null)";
                    return true;
                }

                operand = "time(datetime(" + Quote(text) + "))";
                return true;
            default:
                operand = Literal(value);
                return true;
        }
    }

    /// <summary>
    ///     Tells whether a number or a text compared with a unit stands for the one part of the unit (Vega-Lite's <c>isLocalSingleTimeUnit</c> of the normalized unit): a single local unit, or the
    ///     single part of a <c>binned</c> unit (<c>binnedyear</c>), whose <c>utc</c> flag the normalization removes.
    /// </summary>
    /// <param name="unit">The unit, or null.</param>
    /// <returns><see langword="true"/> when the unit keeps one part and is local (or binned).</returns>
    private static bool IsSingleUnit(TimeUnitSpec? unit) => unit is { IsSingleLocalUnit: true } or { IsBinned: true, Parts.Count: 1 };

    /// <summary>
    ///     Wraps a <c>datetime(...)</c> call in <c>time(...)</c>.
    /// </summary>
    /// <param name="call">The call.</param>
    /// <param name="operand">Receives the wrapped call.</param>
    /// <returns>Always true (so that callers can chain it).</returns>
    private static bool Wrap(string call, out string operand)
    {
        operand = "time(" + call + ")";
        return true;
    }

    /// <summary>
    ///     Writes an inclusive range (the endpoints in ascending order when both are numbers, as Vega-Lite swaps them).
    /// </summary>
    /// <param name="value">The expression that reads the field.</param>
    /// <param name="range">The array of the two endpoints.</param>
    /// <param name="text">The text written so far.</param>
    private static void AppendRange(string value, SpecNode range, SourceWriter text)
    {
        var low = range.Items[0];
        var high = range.Items[1];
        if (low.IsNumber && high.IsNumber && high.NumberValue < low.NumberValue)
        {
            (low, high) = (high, low);
        }

        text.Append('(');
        if (!low.IsNull)
        {
            text.Append(value).Append(" >= ").Append(Literal(low));
        }

        if (!low.IsNull && !high.IsNull)
        {
            text.Append(" && ");
        }

        if (!high.IsNull)
        {
            text.Append(value).Append(" <= ").Append(Literal(high));
        }

        text.Append(low.IsNull && high.IsNull ? "true" : string.Empty).Append(')');
    }

    /// <summary>
    ///     Writes a membership test as strict equalities.
    /// </summary>
    /// <param name="value">The expression that reads the field.</param>
    /// <param name="members">The array of members.</param>
    /// <param name="text">The text written so far.</param>
    private static void AppendOneOf(string value, SpecNode members, SourceWriter text)
    {
        text.Append('(');
        if (members.Items.Count == 0)
        {
            text.Append("false");
        }

        for (var i = 0; i < members.Items.Count; i++)
        {
            text.Append(i > 0 ? " || " : string.Empty).Append(value).Append(" === ").Append(Literal(members.Items[i]));
        }

        text.Append(')');
    }

    /// <summary>
    ///     Writes a JSON scalar as an expression literal.
    /// </summary>
    /// <param name="node">The scalar.</param>
    /// <returns>The literal.</returns>
    private static string Literal(SpecNode node) => node.Kind switch
    {
        SpecNodeKind.Number => JsNumber.ToString(node.NumberValue),
        SpecNodeKind.String => Quote(node.StringValue!),
        SpecNodeKind.Boolean => node.BoolValue ? "true" : "false",
        SpecNodeKind.Object when node["expr"] is { IsString: true } expression => "(" + expression.StringValue + ")",
        _ => "null",
    };

    /// <summary>
    ///     Writes a text as a double-quoted string literal with JavaScript escapes.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The literal.</returns>
    private static string Quote(string value)
    {
        var text = new StringBuilder(value.Length + 2).Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                case '\r':
                    text.Append("\\r");
                    break;
                case '\t':
                    text.Append("\\t");
                    break;
                case < ' ':
                    text.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                default:
                    text.Append(c);
                    break;
            }
        }

        return text.Append('"').ToString();
    }

    /// <summary>
    ///     Tells whether a name can be written as an identifier of the expression language.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> for letters, digits, underscores, and dollar signs that do not start with a digit.</returns>
    private static bool IsIdentifier(string name) =>
        name.Length > 0 && !char.IsAsciiDigit(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '$');
}

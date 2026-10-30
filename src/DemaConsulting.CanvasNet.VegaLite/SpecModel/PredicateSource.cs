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
///         A predicate that no expression can say is not written: a selection or a parameter that is not a variable parameter, a <c>timeUnit</c>,
///         and a comparison with a date object (the feature catalog reports them). The class is static and pure.
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
    public static bool TryBuild(SpecNode predicate, out string source)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var text = new StringBuilder();
        var written = Append(predicate, text);
        source = written ? text.ToString() : string.Empty;
        return written;
    }

    /// <summary>
    ///     Tells whether a predicate has a part that an expression cannot say (a selection, a parameter that is not a variable parameter, a time unit, a date object).
    /// </summary>
    /// <param name="predicate">The <c>test</c> value.</param>
    /// <returns><see langword="true"/> when <see cref="TryBuild"/> would fail.</returns>
    public static bool IsUnsupported(SpecNode predicate) => !TryBuild(predicate, out _);

    /// <summary>
    ///     Writes one predicate.
    /// </summary>
    /// <param name="node">The predicate.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> when a part cannot be written.</returns>
    private static bool Append(SpecNode node, StringBuilder text)
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
            var inner = Append(negated, text);
            text.Append(')');
            return inner;
        }

        if (node["param"] is { } parameter)
        {
            return AppendParameter(parameter, text);
        }

        return node["selection"] is null && AppendField(node, text);
    }

    /// <summary>
    ///     Writes an <c>and</c> or an <c>or</c> of predicates.
    /// </summary>
    /// <param name="items">The array of predicates.</param>
    /// <param name="all"><see langword="true"/> for <c>and</c>.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> when a part cannot be written.</returns>
    private static bool AppendComposition(SpecNode items, bool all, StringBuilder text)
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

            if (!Append(items.Items[i], text))
            {
                return false;
            }
        }

        text.Append(')');
        return true;
    }

    /// <summary>
    ///     Writes a variable parameter as its name; a selection parameter has no value an expression can read.
    /// </summary>
    /// <param name="parameter">The <c>param</c> value.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> for a selection parameter, an undeclared one, or a name that is not an identifier.</returns>
    private static bool AppendParameter(SpecNode parameter, StringBuilder text)
    {
        if (!parameter.IsString
            || SpecParser.FindParameter(parameter, parameter.StringValue!) is not { } declaration
            || declaration["select"] is not null
            || !IsIdentifier(parameter.StringValue!))
        {
            return false;
        }

        text.Append('(').Append(parameter.StringValue).Append(')');
        return true;
    }

    /// <summary>
    ///     Writes a field predicate.
    /// </summary>
    /// <param name="node">The predicate object.</param>
    /// <param name="text">The text written so far.</param>
    /// <returns><see langword="false"/> for a time unit, a comparison with a date object, or a field that is not a path.</returns>
    private static bool AppendField(SpecNode node, StringBuilder text)
    {
        if (node["timeUnit"] is { IsNull: false } || node["field"] is not { IsString: true } field || FieldPath.Parse(field.StringValue) is not { } segments)
        {
            return false;
        }

        var comparison = Operators.Select(name => node[name]).FirstOrDefault(n => n is not null);
        if (comparison is null || comparison.IsObject || comparison.Items.Any(i => i.IsObject))
        {
            return false;
        }

        var path = new StringBuilder("datum");
        foreach (var segment in segments)
        {
            path.Append('[').Append(Quote(segment)).Append(']');
        }

        var value = path.ToString();
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
            case "oneOf":
                AppendOneOf(value, comparison, text);
                return true;
            default:
                text.Append(comparison.BoolValue ? string.Empty : "!").Append("isValid(").Append(value).Append(')');
                return true;
        }
    }

    /// <summary>
    ///     Writes an inclusive range (the endpoints in ascending order when both are numbers, as Vega-Lite swaps them).
    /// </summary>
    /// <param name="value">The expression that reads the field.</param>
    /// <param name="range">The array of the two endpoints.</param>
    /// <param name="text">The text written so far.</param>
    private static void AppendRange(string value, SpecNode range, StringBuilder text)
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
    private static void AppendOneOf(string value, SpecNode members, StringBuilder text)
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

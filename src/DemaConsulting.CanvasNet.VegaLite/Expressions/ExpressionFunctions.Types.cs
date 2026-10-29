using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The type-checking and conversion functions of the closed function table (a partial file of the functions unit).
/// </summary>
/// <remarks>
///     The predicates follow Vega: <c>isNumber</c> accepts NaN and the infinities, <c>isObject</c> accepts arrays, dates
///     and regular expressions, <c>isNaN</c> and <c>isFinite</c> do not coerce their argument (like
///     <c>Number.isNaN</c> and <c>Number.isFinite</c>) and <c>isValid</c> rejects null and NaN. The conversions map null
///     and the empty string to null, so a missing value stays missing.
/// </remarks>
internal static partial class ExpressionFunctions
{
    /// <summary>
    ///     Adds the type-checking and conversion functions.
    /// </summary>
    /// <param name="table">The table being built.</param>
    private static void AddTypes(Dictionary<string, ExpressionFunction> table)
    {
        table["isArray"] = (a, _) => DataValue.FromBoolean(Arg(a, 0).IsArray);
        table["isBoolean"] = (a, _) => DataValue.FromBoolean(Arg(a, 0).IsBoolean);
        table["isDate"] = (a, _) => DataValue.FromBoolean(Arg(a, 0).IsDate);
        table["isNumber"] = (a, _) => DataValue.FromBoolean(Arg(a, 0).IsNumber);
        table["isRegExp"] = (a, _) => DataValue.FromBoolean(Arg(a, 0).IsRegExp);
        table["isString"] = (a, _) => DataValue.FromBoolean(Arg(a, 0).IsString);
        table["isObject"] = (a, _) => DataValue.FromBoolean(Arg(a, 0).Kind is DataValueKind.Object or DataValueKind.Array or DataValueKind.Date or DataValueKind.RegExp);
        table["isFinite"] = (a, _) => DataValue.FromBoolean(Arg(a, 0) is { IsNumber: true } n && double.IsFinite(n.Number));
        table["isNaN"] = (a, _) => DataValue.FromBoolean(Arg(a, 0) is { IsNumber: true } n && double.IsNaN(n.Number));
        table["isValid"] = (a, _) => DataValue.FromBoolean(Arg(a, 0) is { IsNull: false } v && !(v.IsNumber && double.IsNaN(v.Number)));
        table["toBoolean"] = (a, _) => ToBoolean(Arg(a, 0));
        table["toNumber"] = (a, c) =>
        {
            var value = Arg(a, 0);
            if (IsNullOrEmptyString(value))
            {
                return default;
            }

            if (value.IsString)
            {
                c.ChargeSize(value.Text!.Length);
            }

            return DataValue.FromNumber(ExpressionEvaluator.ToNumber(value));
        };
        table["toString"] = (a, c) =>
        {
            if (IsNullOrEmptyString(Arg(a, 0)))
            {
                return default;
            }

            var text = ExpressionEvaluator.ToJsString(Arg(a, 0), c.MaxStringLength);
            c.ChargeSize(text.Length);
            return DataValue.FromString(text);
        };
    }

    /// <summary>
    ///     Tells whether a value is null or the empty string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for null and the empty string.</returns>
    private static bool IsNullOrEmptyString(DataValue value) => value.IsNull || (value.IsString && value.Text!.Length == 0);

    /// <summary>
    ///     The <c>toBoolean</c> conversion: null and the empty string give null, falsy values and the texts
    ///     <c>false</c> and <c>0</c> give false, everything else true.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The boolean or null.</returns>
    private static DataValue ToBoolean(DataValue value)
    {
        if (IsNullOrEmptyString(value))
        {
            return default;
        }

        var isFalseText = value.IsString && value.Text is "false" or "0";
        return DataValue.FromBoolean(ExpressionEvaluator.IsTruthy(value) && !isFalseText);
    }
}

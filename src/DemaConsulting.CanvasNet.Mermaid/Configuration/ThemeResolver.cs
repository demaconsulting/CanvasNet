// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text.Json.Nodes;
using static DemaConsulting.CanvasNet.Mermaid.Configuration.ColorMath;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     A JavaScript object as Mermaid's theme classes use <c>this</c>: string keys in insertion order with JavaScript
///     values (<c>string</c>, <c>double</c>, <c>bool</c>, <c>null</c>, <see cref="Undefined"/>, nested objects).
/// </summary>
/// <remarks>
///     A key that is assigned <c>undefined</c> stays in the object (as in JavaScript) but is left out of its JSON form.
///     An instance belongs to one theme computation on one thread.
/// </remarks>
internal sealed class ThemeObject
{
    /// <summary>The JavaScript <c>undefined</c> value.</summary>
    public static readonly object Undefined = new();

    /// <summary>The keys in insertion order.</summary>
    private readonly List<string> _keys = [];

    /// <summary>The values by key.</summary>
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the keys in insertion order (JavaScript's <c>Object.keys</c>; theme keys are never integer-like).
    /// </summary>
    public IReadOnlyList<string> Keys => _keys;

    /// <summary>
    ///     Gets or sets a property; reading a missing property gives <see cref="Undefined"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The value.</returns>
    public object? this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : Undefined;
        set
        {
            if (!_values.ContainsKey(key))
            {
                _keys.Add(key);
            }

            _values[key] = value;
        }
    }

    /// <summary>
    ///     JavaScript's <c>this.key = this.key || fallback</c>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The fallback, evaluated only when the value is falsy.</param>
    public void Or(string key, Func<object?> fallback) => this[key] = Js.Or(this[key], fallback);

    /// <summary>
    ///     JavaScript's <c>this.key = this.key ?? fallback</c>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The fallback, evaluated only when the value is null or undefined.</param>
    public void Nullish(string key, Func<object?> fallback)
    {
        var value = this[key];
        this[key] = value is null || ReferenceEquals(value, Undefined) ? fallback() : value;
    }

    /// <summary>
    ///     Converts the object to JSON as <c>JSON.stringify</c> would: undefined properties are left out and a number
    ///     that is not finite becomes null.
    /// </summary>
    /// <returns>The JSON object.</returns>
    public JsonObject ToJson()
    {
        var json = new JsonObject();
        foreach (var key in _keys)
        {
            var value = _values[key];
            if (!ReferenceEquals(value, Undefined))
            {
                json[key] = Js.ToJson(value);
            }
        }

        return json;
    }
}

/// <summary>
///     The JavaScript semantics the theme classes rely on: truthiness, <c>||</c>, optional member access and the
///     conversion between JSON and JavaScript values.
/// </summary>
internal static class Js
{
    /// <summary>
    ///     Gets whether a value is truthy: not <c>undefined</c>, <c>null</c>, <c>false</c>, <c>0</c>, <c>NaN</c> or
    ///     the empty string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether it is truthy.</returns>
    public static bool Truthy(object? value) => value switch
    {
        null => false,
        bool flag => flag,
        double number => IsTruthy(number),
        string text => text.Length > 0,
        _ => !ReferenceEquals(value, ThemeObject.Undefined),
    };

    /// <summary>
    ///     JavaScript's <c>a || b</c>.
    /// </summary>
    /// <param name="value">The first operand.</param>
    /// <param name="fallback">The second operand, evaluated only when the first is falsy.</param>
    /// <returns>The result.</returns>
    public static object? Or(object? value, Func<object?> fallback) => Truthy(value) ? value : fallback();

    /// <summary>
    ///     JavaScript's <c>value?.key</c> on a theme value: the member of an object, else <c>undefined</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="key">The key.</param>
    /// <returns>The member.</returns>
    public static object? Member(object? value, string key) =>
        value is ThemeObject obj ? obj[key] : ThemeObject.Undefined;

    /// <summary>
    ///     JavaScript's <c>Number(value)</c> for the values a theme holds.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The number.</returns>
    public static double ToNumber(object? value) => value switch
    {
        null => 0,
        bool flag => flag ? 1 : 0,
        double number => number,
        string text => StringToNumber(text),
        _ => double.NaN,
    };

    /// <summary>
    ///     JavaScript's <c>Number(text)</c> for decimal texts: white space is ignored, an empty text is zero and anything
    ///     that is not a number is NaN.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The number.</returns>
    private static double StringToNumber(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return 0;
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
    }

    /// <summary>
    ///     Converts a JSON value to a JavaScript value (an object becomes a <see cref="ThemeObject"/>).
    /// </summary>
    /// <param name="node">The JSON value.</param>
    /// <returns>The JavaScript value.</returns>
    public static object? FromJson(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
                var result = new ThemeObject();
                foreach (var (key, value) in obj)
                {
                    result[key] = FromJson(value);
                }

                return result;
            case JsonArray array:
                return array.DeepClone();
            default:
                var element = node.AsValue();
                if (element.TryGetValue<string>(out var text))
                {
                    return text;
                }

                if (element.TryGetValue<bool>(out var flag))
                {
                    return flag;
                }

                return element.TryGetValue<double>(out var number) ? number : null;
        }
    }

    /// <summary>
    ///     Converts a JavaScript value to JSON as <c>JSON.stringify</c> would.
    /// </summary>
    /// <param name="value">The value (never <see cref="ThemeObject.Undefined"/>).</param>
    /// <returns>The JSON value.</returns>
    public static JsonNode? ToJson(object? value) => value switch
    {
        null => null,
        string text => JsonValue.Create(text),
        bool flag => JsonValue.Create(flag),
        double number => double.IsFinite(number) ? JsonValue.Create(number) : null,
        ThemeObject obj => obj.ToJson(),
        JsonNode node => node.DeepClone(),
        _ => null,
    };
}

/// <summary>
///     Mermaid's five built-in themes: computes the theme variables of a theme from the user's overrides with each
///     theme's exact derivation order (Mermaid's <c>getThemeVariables</c>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> Each theme is a port of its <c>theme-*.js</c> class: the constructor's base values, then
///         <c>calculate</c> (the <c>default</c> theme first resets every <c>'calculated'</c> placeholder), which copies
///         the overrides in, derives every color in <c>updateColors</c> in the class's own order with the khroma port
///         (<see cref="ColorMath"/>), and copies the overrides in again so that an overridden derived value wins. Every
///         variable of every diagram type is computed, so the result equals Mermaid's object variable for variable; the
///         vectors recorded from the pinned Mermaid check this.
///     </para>
///     <para>
///         <b>Bounds.</b> A user value of <c>THEME_COLOR_LIMIT</c> controls loops in the themes; the loops are capped at
///         <see cref="MaxColorLimit"/> iterations so that a hostile value cannot make the computation unbounded.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class ThemeResolver
{
    /// <summary>The largest number of color-scale iterations a theme performs, whatever <c>THEME_COLOR_LIMIT</c> says.</summary>
    internal const int MaxColorLimit = 64;

    /// <summary>The supported themes, in Mermaid's documentation order.</summary>
    private static readonly Dictionary<string, ThemeDefinition> Themes = new(StringComparer.Ordinal)
    {
        ["default"] = new(ConstructDefault, UpdateDefault, ClearsCalculated: true),
        ["base"] = new(ConstructBase, UpdateBase, ClearsCalculated: false),
        ["dark"] = new(ConstructDark, UpdateDark, ClearsCalculated: false),
        ["forest"] = new(ConstructForest, UpdateForest, ClearsCalculated: false),
        ["neutral"] = new(ConstructNeutral, UpdateNeutral, ClearsCalculated: false),
    };

    /// <summary>
    ///     Gets the names of the themes Mermaid 11 defines that this version does not draw (the "neo" and "redux"
    ///     families).
    /// </summary>
    public static IReadOnlyList<string> UnsupportedMermaidThemes { get; } =
        ["neo", "neo-dark", "redux", "redux-dark", "redux-color", "redux-dark-color"];

    /// <summary>
    ///     Gets whether a value names one of the supported themes.
    /// </summary>
    /// <param name="theme">The value.</param>
    /// <returns>Whether it is a supported theme name.</returns>
    public static bool IsTheme(object? theme) => theme is string name && Themes.ContainsKey(name);

    /// <summary>
    ///     Computes the variables of a theme (Mermaid's <c>getThemeVariables(userOverrides)</c>).
    /// </summary>
    /// <param name="theme">The theme name; it must be supported.</param>
    /// <param name="overrides">The user's overrides: a JSON object, or anything else (or null) for none.</param>
    /// <returns>The theme variables as JSON.</returns>
    /// <exception cref="ArgumentException">Thrown when the theme is not supported.</exception>
    /// <exception cref="ColorFormatException">Thrown when a value the theme derives colors from is not a color.</exception>
    public static JsonObject GetThemeVariables(string theme, JsonNode? overrides)
    {
        if (!Themes.TryGetValue(theme, out var definition))
        {
            throw new ArgumentException($"'{theme}' is not a supported theme.", nameof(theme));
        }

        var t = new ThemeObject();
        definition.Construct(t);

        // calculate(): the default theme first turns every 'calculated' placeholder into undefined
        if (definition.ClearsCalculated)
        {
            foreach (var key in t.Keys.Where(key => Equals(t[key], "calculated")).ToList())
            {
                t[key] = ThemeObject.Undefined;
            }
        }

        // typeof overrides !== 'object': only the derivation
        var copied = OverrideEntries(overrides);
        if (copied is null)
        {
            definition.UpdateColors(t);
            return t.ToJson();
        }

        // The overrides are copied in, the colors derived, and the overrides copied in again
        foreach (var (key, value) in copied)
        {
            t[key] = value;
        }

        definition.UpdateColors(t);
        foreach (var (key, value) in copied)
        {
            t[key] = value;
        }

        return t.ToJson();
    }

    /// <summary>
    ///     Gets the entries of the overrides as JavaScript values (<c>Object.keys(overrides)</c>), or null when the
    ///     overrides are not an object.
    /// </summary>
    /// <param name="overrides">The overrides.</param>
    /// <returns>The entries, or null.</returns>
    private static List<(string Key, object? Value)>? OverrideEntries(JsonNode? overrides) => overrides switch
    {
        JsonObject obj => obj.Select(pair => (pair.Key, Js.FromJson(pair.Value))).ToList(),
        JsonArray array => array.Select((item, index) => (index.ToString(CultureInfo.InvariantCulture), Js.FromJson(item))).ToList(),
        _ => null,
    };

    /// <summary>
    ///     Mermaid's <c>mkBorder</c>: a border color derived from a fill.
    /// </summary>
    /// <param name="color">The fill color.</param>
    /// <param name="darkMode">The theme's <c>darkMode</c> value.</param>
    /// <returns>The border color.</returns>
    private static string MkBorder(object? color, object? darkMode) =>
        Js.Truthy(darkMode) ? Adjust(color, s: -40, l: 10) : Adjust(color, s: -40, l: -10);

    /// <summary>
    ///     Gets the number of color-scale iterations: <c>THEME_COLOR_LIMIT</c> as a JavaScript loop bound, capped.
    /// </summary>
    /// <param name="t">The theme.</param>
    /// <returns>The iteration count.</returns>
    private static int ColorLimit(ThemeObject t)
    {
        var limit = Js.ToNumber(t["THEME_COLOR_LIMIT"]);
        if (!(limit > 0))
        {
            return 0;
        }

        return (int)Math.Min(Math.Ceiling(limit), MaxColorLimit);
    }

    /// <summary>
    ///     A key with an index (JavaScript's <c>'cScale' + i</c>).
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <param name="index">The index.</param>
    /// <returns>The key.</returns>
    private static string Key(string prefix, int index) => prefix + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     The quadrant-chart block, identical in every theme.
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void Quadrant(ThemeObject t)
    {
        t.Or("quadrant1Fill", () => t["primaryColor"]);
        t.Or("quadrant2Fill", () => Adjust(t["primaryColor"], r: 5, g: 5, b: 5));
        t.Or("quadrant3Fill", () => Adjust(t["primaryColor"], r: 10, g: 10, b: 10));
        t.Or("quadrant4Fill", () => Adjust(t["primaryColor"], r: 15, g: 15, b: 15));
        t.Or("quadrant1TextFill", () => t["primaryTextColor"]);
        t.Or("quadrant2TextFill", () => Adjust(t["primaryTextColor"], r: -5, g: -5, b: -5));
        t.Or("quadrant3TextFill", () => Adjust(t["primaryTextColor"], r: -10, g: -10, b: -10));
        t.Or("quadrant4TextFill", () => Adjust(t["primaryTextColor"], r: -15, g: -15, b: -15));

        // (this.quadrantPointFill || isDark(this.quadrant1Fill)) ? lighten(...) : darken(...), lighten without an amount
        t["quadrantPointFill"] = Js.Truthy(t["quadrantPointFill"]) || IsDark(t["quadrant1Fill"])
            ? Lighten(t["quadrant1Fill"])
            : Darken(t["quadrant1Fill"]);
        t.Or("quadrantPointTextFill", () => t["primaryTextColor"]);
        t.Or("quadrantXAxisTextFill", () => t["primaryTextColor"]);
        t.Or("quadrantYAxisTextFill", () => t["primaryTextColor"]);
        t.Or("quadrantInternalBorderStrokeFill", () => t["primaryBorderColor"]);
        t.Or("quadrantExternalBorderStrokeFill", () => t["primaryBorderColor"]);
        t.Or("quadrantTitleFill", () => t["primaryTextColor"]);
    }

    /// <summary>
    ///     The radar block, identical in every theme.
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void Radar(ThemeObject t)
    {
        var old = t["radar"];
        var radar = new ThemeObject();
        radar["axisColor"] = Js.Or(Js.Member(old, "axisColor"), () => t["lineColor"]);
        radar["axisStrokeWidth"] = Js.Or(Js.Member(old, "axisStrokeWidth"), () => 2.0);
        radar["axisLabelFontSize"] = Js.Or(Js.Member(old, "axisLabelFontSize"), () => 12.0);
        radar["curveOpacity"] = Js.Or(Js.Member(old, "curveOpacity"), () => 0.5);
        radar["curveStrokeWidth"] = Js.Or(Js.Member(old, "curveStrokeWidth"), () => 2.0);
        radar["graticuleColor"] = Js.Or(Js.Member(old, "graticuleColor"), () => "#DEDEDE");
        radar["graticuleStrokeWidth"] = Js.Or(Js.Member(old, "graticuleStrokeWidth"), () => 1.0);
        radar["graticuleOpacity"] = Js.Or(Js.Member(old, "graticuleOpacity"), () => 0.3);
        radar["legendBoxSize"] = Js.Or(Js.Member(old, "legendBoxSize"), () => 12.0);
        radar["legendFontSize"] = Js.Or(Js.Member(old, "legendFontSize"), () => 12.0);
        t["radar"] = radar;
    }

    /// <summary>
    ///     The cynefin block; the themes differ only in their colors.
    /// </summary>
    /// <param name="t">The theme.</param>
    /// <param name="colors">The cliff, complex, complicated, chaotic, clear and confusion colors.</param>
    private static void Cynefin(ThemeObject t, (string Cliff, string Complex, string Complicated, string Chaotic, string Clear, string Confusion) colors)
    {
        var old = t["cynefin"];
        var cynefin = new ThemeObject();
        cynefin["domainFontSize"] = Js.Or(Js.Member(old, "domainFontSize"), () => 16.0);
        cynefin["itemFontSize"] = Js.Or(Js.Member(old, "itemFontSize"), () => 12.0);
        cynefin["boundaryColor"] = Js.Or(Js.Member(old, "boundaryColor"), () => t["lineColor"]);
        cynefin["boundaryWidth"] = Js.Or(Js.Member(old, "boundaryWidth"), () => 2.0);
        cynefin["cliffColor"] = Js.Or(Js.Member(old, "cliffColor"), () => colors.Cliff);
        cynefin["cliffWidth"] = Js.Or(Js.Member(old, "cliffWidth"), () => 4.0);
        cynefin["arrowColor"] = Js.Or(Js.Member(old, "arrowColor"), () => t["lineColor"]);
        cynefin["arrowWidth"] = Js.Or(Js.Member(old, "arrowWidth"), () => 2.0);
        cynefin["complexBg"] = Js.Or(Js.Member(old, "complexBg"), () => colors.Complex);
        cynefin["complicatedBg"] = Js.Or(Js.Member(old, "complicatedBg"), () => colors.Complicated);
        cynefin["chaoticBg"] = Js.Or(Js.Member(old, "chaoticBg"), () => colors.Chaotic);
        cynefin["clearBg"] = Js.Or(Js.Member(old, "clearBg"), () => colors.Clear);
        cynefin["confusionBg"] = Js.Or(Js.Member(old, "confusionBg"), () => colors.Confusion);
        cynefin["textColor"] = Js.Or(Js.Member(old, "textColor"), () => t["textColor"]);
        cynefin["labelColor"] = Js.Or(Js.Member(old, "labelColor"), () => t["primaryTextColor"]);
        t["cynefin"] = cynefin;
    }

    /// <summary>
    ///     The xychart block; the themes differ only in their palette.
    /// </summary>
    /// <param name="t">The theme.</param>
    /// <param name="palette">The plot color palette.</param>
    private static void XyChart(ThemeObject t, string palette)
    {
        var old = t["xyChart"];
        var chart = new ThemeObject();
        chart["backgroundColor"] = Js.Or(Js.Member(old, "backgroundColor"), () => t["background"]);
        foreach (var key in (ReadOnlySpan<string>)[
                     "titleColor", "dataLabelColor", "legendTextColor", "xAxisTitleColor", "xAxisLabelColor", "xAxisTickColor",
                     "xAxisLineColor", "yAxisTitleColor", "yAxisLabelColor", "yAxisTickColor", "yAxisLineColor"])
        {
            chart[key] = Js.Or(Js.Member(old, key), () => t["primaryTextColor"]);
        }

        chart["plotColorPalette"] = Js.Or(Js.Member(old, "plotColorPalette"), () => palette);
        t["xyChart"] = chart;
    }

    /// <summary>
    ///     The wardley block; the themes differ in the evolution color and in the fill of components and annotations.
    /// </summary>
    /// <param name="t">The theme.</param>
    /// <param name="evolutionColor">The default <c>wardleyEvolutionColor</c>.</param>
    /// <param name="fillKey">The variable the component and annotation fills default to.</param>
    private static void Wardley(ThemeObject t, string evolutionColor, string fillKey)
    {
        t.Or("wardleyEvolutionColor", () => evolutionColor);
        var old = t["wardley"];
        var wardley = new ThemeObject();
        wardley["backgroundColor"] = Js.Or(Js.Member(old, "backgroundColor"), () => t["background"]);
        wardley["axisColor"] = Js.Or(Js.Member(old, "axisColor"), () => t["lineColor"]);
        wardley["axisTextColor"] = Js.Or(Js.Member(old, "axisTextColor"), () => t["primaryTextColor"]);
        wardley["gridColor"] = Js.Or(Js.Member(old, "gridColor"), () => t["gridColor"]);
        wardley["componentFill"] = Js.Or(Js.Member(old, "componentFill"), () => t[fillKey]);
        wardley["componentStroke"] = Js.Or(Js.Member(old, "componentStroke"), () => t["lineColor"]);
        wardley["componentLabelColor"] = Js.Or(Js.Member(old, "componentLabelColor"), () => t["primaryTextColor"]);
        wardley["linkStroke"] = Js.Or(Js.Member(old, "linkStroke"), () => t["lineColor"]);
        wardley["evolutionStroke"] = Js.Or(Js.Member(old, "evolutionStroke"), () => t["wardleyEvolutionColor"]);
        wardley["annotationStroke"] = Js.Or(Js.Member(old, "annotationStroke"), () => t["lineColor"]);
        wardley["annotationTextColor"] = Js.Or(Js.Member(old, "annotationTextColor"), () => t["primaryTextColor"]);
        wardley["annotationFill"] = Js.Or(Js.Member(old, "annotationFill"), () => t[fillKey]);
        t["wardley"] = wardley;
    }

    /// <summary>
    ///     The pie text and stroke block; the themes differ in the variable the title and legend colors default to.
    /// </summary>
    /// <param name="t">The theme.</param>
    /// <param name="textColorKey">The variable the title and legend text colors default to.</param>
    private static void PieText(ThemeObject t, string textColorKey)
    {
        t.Or("pieTitleTextSize", () => "25px");
        t.Or("pieTitleTextColor", () => t[textColorKey]);
        t.Or("pieSectionTextSize", () => "17px");
        t.Or("pieSectionTextColor", () => t["textColor"]);
        t.Or("pieLegendTextSize", () => "17px");
        t.Or("pieLegendTextColor", () => t[textColorKey]);
        t.Or("pieStrokeColor", () => "black");
        t.Or("pieStrokeWidth", () => "2px");
        t.Or("pieOuterStrokeWidth", () => "2px");
        t.Or("pieOuterStrokeColor", () => "black");
        t.Or("pieOpacity", () => "0.7");
    }

    /// <summary>
    ///     The requirement-diagram block; the themes differ in the relation label background.
    /// </summary>
    /// <param name="t">The theme.</param>
    /// <param name="relationLabelBackground">The default relation label background.</param>
    private static void Requirement(ThemeObject t, Func<object?> relationLabelBackground)
    {
        t.Or("requirementBackground", () => t["primaryColor"]);
        t.Or("requirementBorderColor", () => t["primaryBorderColor"]);
        t.Or("requirementBorderSize", () => "1");
        t.Or("requirementTextColor", () => t["primaryTextColor"]);
        t.Or("relationColor", () => t["lineColor"]);
        t.Or("relationLabelBackground", relationLabelBackground);
        t.Or("relationLabelColor", () => t["actorTextColor"]);
    }

    /// <summary>
    ///     The git colors of the light themes: eight derived colors, lightened in dark mode and darkened otherwise.
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void GitColors(ThemeObject t)
    {
        t.Or("git0", () => t["primaryColor"]);
        t.Or("git1", () => t["secondaryColor"]);
        t.Or("git2", () => t["tertiaryColor"]);
        t.Or("git3", () => Adjust(t["primaryColor"], h: -30));
        t.Or("git4", () => Adjust(t["primaryColor"], h: -60));
        t.Or("git5", () => Adjust(t["primaryColor"], h: -90));
        t.Or("git6", () => Adjust(t["primaryColor"], h: 60));
        t.Or("git7", () => Adjust(t["primaryColor"], h: 120));
        var dark = Js.Truthy(t["darkMode"]);
        for (var i = 0; i < 8; i++)
        {
            t[Key("git", i)] = dark ? Lighten(t[Key("git", i)], 25) : Darken(t[Key("git", i)], 25);
        }
    }

    /// <summary>
    ///     The inverted git colors of indexes 1 to 7 (index 0 differs between the themes).
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void GitInverted(ThemeObject t)
    {
        for (var i = 1; i < 8; i++)
        {
            var index = i;
            t.Or(Key("gitInv", index), () => Invert(t[Key("git", index)]));
        }
    }

    /// <summary>
    ///     The git branch labels of the default, dark and forest themes: inverted label text for branches 0 and 3.
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void GitBranchLabels(ThemeObject t)
    {
        for (var i = 0; i < 8; i++)
        {
            t.Or(Key("gitBranchLabel", i), i is 0 or 3 ? () => Invert(t["labelTextColor"]) : () => t["labelTextColor"]);
        }
    }

    /// <summary>
    ///     The git tag and commit label block, identical in every theme.
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void GitTagsAndCommits(ThemeObject t)
    {
        t.Or("tagLabelColor", () => t["primaryTextColor"]);
        t.Or("tagLabelBackground", () => t["primaryColor"]);
        t["tagLabelBorder"] = Js.Or(t["tagBorder"], () => t["primaryBorderColor"]);
        t.Or("tagLabelFontSize", () => "10px");
        t.Or("commitLabelColor", () => t["secondaryTextColor"]);
        t.Or("commitLabelBackground", () => t["secondaryColor"]);
        t.Or("commitLabelFontSize", () => "10px");
    }

    /// <summary>
    ///     The event-modeling block of the light themes.
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void EventModelingLight(ThemeObject t)
    {
        t.Or("emUiFill", () => "white");
        t.Or("emUiStroke", () => "#dbdada");
        t.Or("emProcessorFill", () => "#edb3f6");
        t.Or("emProcessorStroke", () => "#b88cbf");
        t.Or("emReadModelFill", () => "#d3f1a2");
        t.Or("emReadModelStroke", () => "#a3b732");
        t.Or("emCommandFill", () => "#bcd6fe");
        t.Or("emCommandStroke", () => "#679ac3");
        t.Or("emEventFill", () => "#ffb778");
        t.Or("emEventStroke", () => "#c19a0f");
        t.Or("emSwimlaneBackgroundOdd", () => "rgb(250,250,250)");
        t.Or("emSwimlaneBackgroundStroke", () => "rgb(240,240,240)");
        t.Or("emArrowhead", () => t["lineColor"]);
        t.Or("emRelationStroke", () => t["lineColor"]);
    }

    /// <summary>
    ///     The entity-relationship attribute rows of the light themes (Mermaid's old hard-coded values).
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void AttributeRows(ThemeObject t)
    {
        t.Or("attributeBackgroundColorOdd", () => "#ffffff");
        t.Or("attributeBackgroundColorEven", () => "#f2f2f2");
    }

    /// <summary>
    ///     The journey fill types, identical in the default, dark, forest and neutral themes (unconditional).
    /// </summary>
    /// <param name="t">The theme.</param>
    private static void FillTypes(ThemeObject t)
    {
        t["fillType0"] = t["primaryColor"];
        t["fillType1"] = t["secondaryColor"];
        t["fillType2"] = Adjust(t["primaryColor"], h: 64);
        t["fillType3"] = Adjust(t["secondaryColor"], h: 64);
        t["fillType4"] = Adjust(t["primaryColor"], h: -64);
        t["fillType5"] = Adjust(t["secondaryColor"], h: -64);
        t["fillType6"] = Adjust(t["primaryColor"], h: 128);
        t["fillType7"] = Adjust(t["secondaryColor"], h: 128);
    }

    /// <summary>
    ///     The twelve color-scale defaults that the default, base, dark and forest themes derive from the primary color.
    /// </summary>
    /// <param name="t">The theme.</param>
    /// <param name="hue8Lightness">The lightness change of <c>cScale8</c> (150 in the base theme, 0 elsewhere).</param>
    private static void ColorScaleDefaults(ThemeObject t, double hue8Lightness = 0)
    {
        t.Or("cScale0", () => t["primaryColor"]);
        t.Or("cScale1", () => t["secondaryColor"]);
        t.Or("cScale2", () => t["tertiaryColor"]);
        t.Or("cScale3", () => Adjust(t["primaryColor"], h: 30));
        t.Or("cScale4", () => Adjust(t["primaryColor"], h: 60));
        t.Or("cScale5", () => Adjust(t["primaryColor"], h: 90));
        t.Or("cScale6", () => Adjust(t["primaryColor"], h: 120));
        t.Or("cScale7", () => Adjust(t["primaryColor"], h: 150));
        t.Or("cScale8", () => Adjust(t["primaryColor"], h: 210, l: hue8Lightness));
        t.Or("cScale9", () => Adjust(t["primaryColor"], h: 270));
        t.Or("cScale10", () => Adjust(t["primaryColor"], h: 300));
        t.Or("cScale11", () => Adjust(t["primaryColor"], h: 330));
    }

    /// <summary>
    ///     A theme class: its constructor, its <c>updateColors</c>, and whether its <c>calculate</c> clears the
    ///     <c>'calculated'</c> placeholders.
    /// </summary>
    /// <param name="Construct">The constructor.</param>
    /// <param name="UpdateColors">The derivation.</param>
    /// <param name="ClearsCalculated">Whether placeholders are cleared before the overrides are copied.</param>
    private sealed record ThemeDefinition(Action<ThemeObject> Construct, Action<ThemeObject> UpdateColors, bool ClearsCalculated);
}

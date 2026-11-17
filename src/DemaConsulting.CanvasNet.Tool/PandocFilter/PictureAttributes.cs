using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The options a picture's attributes ask for, as the Pandoc filter passes them to the tool, and the attributes that
///     stay on the picture.
/// </summary>
/// <param name="Options">The <c>--name=value</c> options, sorted by their bytes so that the attribute order does not change the key.</param>
/// <param name="Keep">The attributes that stay on the picture, in their original order (a repeated key keeps its last value).</param>
internal sealed record PictureOptions(IReadOnlyList<string> Options, IReadOnlyList<KeyValuePair<string, string>> Keep);

/// <summary>
///     Reads the attributes of a picture or a Mermaid code block: which kind of input a picture is, the checks of every
///     drawing attribute with the Lua filter's messages, the defaults, the sorted options, and the attributes that stay.
/// </summary>
/// <remarks>
///     A literal port of <c>kind_of</c>, <c>is_svg</c>, the <c>OPTIONS</c> table, <c>build_options</c>, and
///     <c>block_name</c> of the Lua filter (version 16). Every check stops the build with
///     <see cref="ErrorCodes.PandocFilterError"/> and a message that names the picture, the attribute, and what is
///     expected. The attributes are checked in their order; a repeated attribute is checked each time and its last
///     value wins (a repeated <c>define-NAME</c> or <c>param-NAME</c> adds an option each time), as Lua's <c>pairs</c>
///     over a Pandoc attribute list did. Numbers are written with <c>%.15g</c> (<see cref="FormatNumber"/>). Stateless
///     and thread-safe.
/// </remarks>
internal static class PictureAttributes
{
    /// <summary>The kind of a Vega-Lite chart (<c>.vl.json</c>, <c>.vl</c>).</summary>
    internal const string VegaLite = "vegalite";

    /// <summary>The kind of an OpenSCAD model (<c>.scad</c>).</summary>
    internal const string Scad = "scad";

    /// <summary>The kind of a Visio drawing (<c>.vsdx</c>).</summary>
    internal const string Vsdx = "vsdx";

    /// <summary>The kind of a PowerPoint presentation (<c>.pptx</c>).</summary>
    internal const string Pptx = "pptx";

    /// <summary>The kind of a PDF document (<c>.pdf</c>).</summary>
    internal const string Pdf = "pdf";

    /// <summary>The kind of a Mermaid diagram (<c>.mmd</c>, <c>.mermaid</c>, or a code block of class mermaid).</summary>
    internal const string Mermaid = "mermaid";

    /// <summary>The size a model is drawn at unless <c>size</c> or <c>scale</c> is given.</summary>
    internal const string ScadDefaultSize = "1600x1200";

    /// <summary>The background of a model unless one is given (charts and documents keep the tool's own default).</summary>
    internal const string DefaultBackground = "white";

    /// <summary>The triangle budget of a model: the limit DocPilot's preview uses, so a model that previews also builds.</summary>
    internal const string MaxTriangles = "2000000";

    /// <summary>The largest width or height of <c>size</c>, in pixels.</summary>
    internal const int MaxPixels = 8192;

    /// <summary>The file name end of an SVG drawing, which the filter flattens when it links a picture only the tool draws.</summary>
    internal const string SvgSuffix = ".svg";

    /// <summary>
    ///     The file name ends of the inputs the filter draws, checked in this order.
    /// </summary>
    private static readonly (string Suffix, string Kind)[] Suffixes =
    [
        (".vl.json", VegaLite), (".vl", VegaLite), (".scad", Scad), (".vsdx", Vsdx), (".pptx", Pptx), (".pdf", Pdf),
        (".mmd", Mermaid), (".mermaid", Mermaid)
    ];

    /// <summary>
    ///     The classes of the code blocks the filter draws, each with the kind it draws them as.
    /// </summary>
    private static readonly (string Class, string Kind)[] BlockClassKinds = [(Mermaid, Mermaid)];

    /// <summary>
    ///     Gets the lowercase file name ends of every local picture the filter may change: <see cref="SvgSuffix"/> and the
    ///     ends of the inputs it draws, in the order they are checked.
    /// </summary>
    /// <remarks>
    ///     A picture whose <c>src</c> has no URL scheme and ends in none of these (in any letter case) is left alone, so
    ///     the shim writes this list into its fast path (<see cref="PandocFilterShim"/>).
    /// </remarks>
    internal static IReadOnlyList<string> PictureSuffixes { get; } = [SvgSuffix, .. Suffixes.Select(s => s.Suffix)];

    /// <summary>
    ///     Gets the classes of the code blocks the filter draws (exact, case-sensitive); any other code block is left alone,
    ///     so the shim writes this list into its fast path (<see cref="PandocFilterShim"/>).
    /// </summary>
    internal static IReadOnlyList<string> BlockClasses { get; } = [.. BlockClassKinds.Select(b => b.Class)];

    /// <summary>The views of a model.</summary>
    private static readonly HashSet<string> Views = new(StringComparer.Ordinal) { "iso", "front", "back", "left", "right", "top", "bottom" };

    /// <summary>The projections of a model.</summary>
    private static readonly HashSet<string> Projections = new(StringComparer.Ordinal) { "ortho", "orthographic", "perspective" };

    /// <summary>The themes of a Mermaid diagram.</summary>
    private static readonly HashSet<string> Themes = new(StringComparer.Ordinal) { "default", "base", "dark", "forest", "neutral" };

    /// <summary>The kinds that have pages (<c>page</c> and <c>dpi</c> apply).</summary>
    private static readonly HashSet<string> Paged = new(StringComparer.Ordinal) { Vsdx, Pptx, Pdf };

    /// <summary>The kinds the model options apply to.</summary>
    private static readonly HashSet<string> ScadKinds = new(StringComparer.Ordinal) { Scad };

    /// <summary>The kinds the diagram options apply to.</summary>
    private static readonly HashSet<string> MermaidKinds = new(StringComparer.Ordinal) { Mermaid };

    /// <summary>
    ///     Gets the kind of input a picture's <c>src</c> names.
    /// </summary>
    /// <param name="src">The <c>src</c> as written.</param>
    /// <returns>
    ///     One of the kind constants, or <see langword="null"/> when the filter leaves the picture alone: a name with a URL
    ///     scheme (two or more characters before the colon, so a drive letter is not one), or an unhandled file name end.
    /// </returns>
    internal static string? KindOf(string src)
    {
        if (HasUrlScheme(src))
        {
            return null;
        }

        var lower = FilterPaths.AsciiLower(src);
        foreach (var (suffix, kind) in Suffixes)
        {
            if (lower.EndsWith(suffix, StringComparison.Ordinal))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>
    ///     Determines whether a picture is a local SVG drawing (checked before <see cref="KindOf"/>).
    /// </summary>
    /// <param name="src">The <c>src</c> as written.</param>
    /// <returns><see langword="true"/> when the name has no URL scheme and ends in <c>.svg</c> in any letter case.</returns>
    internal static bool IsSvg(string src)
    {
        return !HasUrlScheme(src) && FilterPaths.AsciiLower(src).EndsWith(SvgSuffix, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets the kind a code block is drawn as, from its classes.
    /// </summary>
    /// <param name="classes">The block's classes.</param>
    /// <returns>The kind of the first of <see cref="BlockClasses"/> the block has, or <see langword="null"/> when it has none (it is left alone).</returns>
    internal static string? BlockKindOf(IReadOnlyList<string> classes)
    {
        foreach (var (blockClass, kind) in BlockClassKinds)
        {
            if (classes.Contains(blockClass, StringComparer.Ordinal))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>
    ///     Determines whether a text starts with a URL scheme of two or more characters (<c>^%a[%w+.-]+:</c>).
    /// </summary>
    /// <param name="src">The text.</param>
    /// <returns><see langword="true"/> for <c>https:</c>, <c>data:</c>, and the like; <see langword="false"/> for <c>C:</c>.</returns>
    internal static bool HasUrlScheme(string src)
    {
        return FilterPaths.SchemeOf(src) is { Length: >= 2 };
    }

    /// <summary>
    ///     Reads the drawing attributes of a picture of a kind the tool draws (<c>build_options</c>).
    /// </summary>
    /// <param name="name">The picture's name for messages: its <c>src</c>, or the name of a code block.</param>
    /// <param name="kind">The kind of the picture (one of the kind constants).</param>
    /// <param name="attributes">The picture's attributes, in their order.</param>
    /// <returns>The sorted tool options, with the defaults the filter adds, and the attributes that stay.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.PandocFilterError"/> when an attribute is not valid, applies to another kind,
    ///     or <c>size</c> and <c>scale</c> are both given.
    /// </exception>
    internal static PictureOptions Build(string name, string kind, IEnumerable<KeyValuePair<string, string>> attributes)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var extra = new List<string>();
        var keep = new KeepList();

        // Each attribute in order: a parameter, a variable, a drawing option, or one that stays on the picture
        foreach (var (key, value) in attributes)
        {
            if (key.StartsWith("param-", StringComparison.Ordinal))
            {
                var param = key[6..];
                if (kind != VegaLite)
                {
                    throw Fail($"'{name}' has {key}={value}, but param-NAME applies to Vega-Lite charts only");
                }

                if (param.Length == 0)
                {
                    throw Fail($"'{name}' has {key}; the parameter name cannot be empty");
                }

                if (param.Contains('=', StringComparison.Ordinal))
                {
                    throw Fail($"'{name}' has {key}; the parameter name cannot contain an equals sign");
                }

                PlainText(name, key, value);
                extra.Add($"--param={param}={value}");
            }
            else if (key.StartsWith("define-", StringComparison.Ordinal))
            {
                var variable = key[7..];
                if (kind != Scad)
                {
                    throw Fail($"'{name}' has {key}={value}, but define-NAME applies to .scad models only");
                }

                if (!IsIdentifier(variable))
                {
                    throw Fail($"'{name}' has {key}; the variable name must be letters, digits and underscores, not starting with a digit");
                }

                PlainText(name, key, value);
                extra.Add($"--define={variable}={value}");
            }
            else if (IsOption(key))
            {
                var applies = AppliesTo(key);
                if (applies != null && !applies.Value.Kinds.Contains(kind))
                {
                    throw Fail($"'{name}' has {key}={value}, but {key} applies to {applies.Value.Text} only");
                }

                PlainText(name, key, value);
                values[key] = Check(name, key, value, kind);
            }
            else
            {
                keep.Set(key, value);
            }
        }

        // The defaults the filter adds, after the checks of the attributes as written
        if (values.ContainsKey("size") && values.ContainsKey("scale"))
        {
            throw Fail($"'{name}' has both size and scale; use one of them");
        }

        if (kind == Scad)
        {
            if (!values.ContainsKey("size") && !values.ContainsKey("scale"))
            {
                values["size"] = ScadDefaultSize;
            }

            values.TryAdd("background", DefaultBackground);
            values["max-triangles"] = MaxTriangles;
        }

        if (Paged.Contains(kind))
        {
            values.TryAdd("page", "1");
        }

        // Sorted, so the same attributes in another order give the same picture name
        var options = values.Select(v => $"--{v.Key}={v.Value}").Concat(extra).ToList();
        FilterPaths.SortBytes(options);
        return new PictureOptions(options, keep.Items);
    }

    /// <summary>
    ///     Reads the attributes of an SVG drawing: only <c>scale</c> is read, every other attribute stays.
    /// </summary>
    /// <param name="name">The drawing's <c>src</c>, for messages.</param>
    /// <param name="attributes">The drawing's attributes, in their order.</param>
    /// <returns>The <c>--scale</c> options (one per <c>scale</c> attribute, in order) and the attributes that stay.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when <c>scale</c> is not valid.</exception>
    internal static PictureOptions BuildSvg(string name, IEnumerable<KeyValuePair<string, string>> attributes)
    {
        var options = new List<string>();
        var keep = new KeepList();
        foreach (var (key, value) in attributes)
        {
            if (key == "scale")
            {
                PlainText(name, key, value);
                options.Add("--scale=" + Check(name, key, value, "svg"));
            }
            else
            {
                keep.Set(key, value);
            }
        }

        return new PictureOptions(options, keep.Items);
    }

    /// <summary>
    ///     Splits the <c>caption</c> attribute off the attributes of a Mermaid code block.
    /// </summary>
    /// <param name="attributes">The block's attributes, in their order.</param>
    /// <returns>
    ///     The caption (the last one; <see langword="null"/> when there is none) and the other attributes, a repeated key
    ///     once with its last value, as the Lua table the filter built.
    /// </returns>
    internal static (string? Caption, IReadOnlyList<KeyValuePair<string, string>> Attributes) SplitCaption(IEnumerable<KeyValuePair<string, string>> attributes)
    {
        string? caption = null;
        var rest = new KeepList();
        foreach (var (key, value) in attributes)
        {
            if (key == "caption")
            {
                caption = value;
            }
            else
            {
                rest.Set(key, value);
            }
        }

        return (caption, rest.Items);
    }

    /// <summary>
    ///     Gets the name a message gives a Mermaid code block (<c>block_name</c>).
    /// </summary>
    /// <param name="identifier">The block's identifier; empty when it has none.</param>
    /// <param name="text">The block's text.</param>
    /// <returns>
    ///     <c>mermaid block #id</c>, or <c>mermaid block (first line)</c> with the first non-blank line trimmed and cut to 40
    ///     characters followed by <c>...</c>.
    /// </returns>
    internal static string BlockName(string identifier, string text)
    {
        if (identifier.Length > 0)
        {
            return "mermaid block #" + identifier;
        }

        // The first line after any leading white space, without trailing white space
        var start = FilterPaths.TrimLuaSpaceStart(text);
        var end = start.IndexOfAny(['\r', '\n']);
        var first = FilterPaths.TrimLuaSpaceEnd(end < 0 ? start : start[..end]);
        var runes = first.EnumerateRunes().ToList();
        if (runes.Count > 40)
        {
            first = string.Concat(runes.Take(40).Select(r => r.ToString())) + "...";
        }

        return $"mermaid block ({first})";
    }

    /// <summary>
    ///     Writes a number as C's <c>%.15g</c> (Lua's <c>string.format("%.15g", n)</c>).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>
    ///     Fifteen significant digits without trailing zeros, in fixed notation for exponents from -4 to 14, else in
    ///     scientific notation with a lowercase <c>e</c>, a sign, and at least two exponent digits (<c>1e-05</c>).
    /// </returns>
    /// <remarks><c>G15</c> differs: it writes <c>1E-05</c>.</remarks>
    internal static string FormatNumber(double value)
    {
        return FormatG(value, 15);
    }

    /// <summary>
    ///     Writes a number as C's <c>%.Pg</c>.
    /// </summary>
    /// <param name="value">The number; finite.</param>
    /// <param name="precision">The number of significant digits, 1 or more.</param>
    /// <returns>The text.</returns>
    internal static string FormatG(double value, int precision)
    {
        if (value == 0)
        {
            return double.IsNegative(value) ? "-0" : "0";
        }

        // The exponent of the number rounded to the precision decides between the two notations
        var scientific = value.ToString("E" + (precision - 1).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var e = scientific.IndexOf('E', StringComparison.Ordinal);
        var exponent = int.Parse(scientific[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        if (exponent < -4 || exponent >= precision)
        {
            var sign = exponent < 0 ? "-" : "+";
            return TrimZeros(scientific[..e]) + "e" + sign + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
        }

        var digits = (precision - 1 - exponent).ToString(CultureInfo.InvariantCulture);
        return TrimZeros(value.ToString("F" + digits, CultureInfo.InvariantCulture));
    }

    /// <summary>
    ///     Checks the value of a drawing option and returns it as the option takes it.
    /// </summary>
    /// <param name="name">The picture's name.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <param name="kind">The kind of the picture.</param>
    /// <returns>The normalized value.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the value is not valid.</exception>
    private static string Check(string name, string key, string value, string kind)
    {
        return key switch
        {
            "size" => SizeValue(name, value),
            "scale" => NumberValue(name, key, value, 0.001, "0.001", 8192, "8192", exclusive: false, whole: false),
            "background" => BackgroundValue(name, value),
            "page" => PageValue(name, value),
            "dpi" => NumberValue(name, key, value, 0, "0", 2400, "2400", exclusive: true, whole: kind == Vsdx),
            "view" => OneOf(name, key, value, Views, "iso, front, back, left, right, top or bottom"),
            "azimuth" => NumberValue(name, key, value, -360000, "-360000", 360000, "360000", exclusive: false, whole: false),
            "elevation" => NumberValue(name, key, value, -90, "-90", 90, "90", exclusive: false, whole: false),
            "projection" => OneOf(name, key, value, Projections, "ortho or perspective"),
            _ => OneOf(name, key, value, Themes, "default, base, dark, forest or neutral")
        };
    }

    /// <summary>
    ///     Determines whether an attribute is a drawing option.
    /// </summary>
    /// <param name="key">The attribute.</param>
    /// <returns><see langword="true"/> for the keys of the Lua filter's <c>OPTIONS</c> table.</returns>
    private static bool IsOption(string key)
    {
        return key is "size" or "scale" or "background" or "page" or "dpi" or "view" or "azimuth" or "elevation" or "projection" or "theme";
    }

    /// <summary>
    ///     Gets the kinds a drawing option applies to, and how a message names them.
    /// </summary>
    /// <param name="key">The option.</param>
    /// <returns>The kinds and their text, or <see langword="null"/> for an option of every kind.</returns>
    private static (HashSet<string> Kinds, string Text)? AppliesTo(string key)
    {
        return key switch
        {
            "page" or "dpi" => (Paged, ".vsdx, .pptx and .pdf files"),
            "view" or "azimuth" or "elevation" or "projection" => (ScadKinds, ".scad models"),
            "theme" => (MermaidKinds, "Mermaid diagrams"),
            _ => null
        };
    }

    /// <summary>
    ///     Checks that a value is plain text: not empty and without control characters.
    /// </summary>
    /// <param name="name">The picture's name.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the value is empty or has a character 0 to 31 or 127.</exception>
    private static void PlainText(string name, string key, string value)
    {
        if (value.Length == 0 || value.Any(c => c < ' ' || c == '\x7F'))
        {
            throw Fail($"'{name}' has {key}={value}; the value must be text without control characters");
        }
    }

    /// <summary>
    ///     Checks a number in a range (<c>number_value</c>): only plain decimals are numbers.
    /// </summary>
    /// <param name="name">The picture's name.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <param name="minimum">The smallest value (or the bound it must be above).</param>
    /// <param name="minimumText">The minimum as the message writes it.</param>
    /// <param name="maximum">The largest value.</param>
    /// <param name="maximumText">The maximum as the message writes it.</param>
    /// <param name="exclusive">Whether the value must be above the minimum rather than at least it.</param>
    /// <param name="whole">Whether the value must be a whole number.</param>
    /// <returns>The number written with <see cref="FormatNumber"/>.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the value is not such a number.</exception>
    private static string NumberValue(
        string name, string key, string value, double minimum, string minimumText, double maximum, string maximumText, bool exclusive, bool whole)
    {
        var number = ParseDecimal(value);
        if (number is not { } n || (whole && !double.IsInteger(n)) || (exclusive ? n <= minimum : n < minimum) || n > maximum)
        {
            var expected = whole ? "a whole number" : "a number";
            var bound = exclusive ? " above " : " from ";
            throw Fail($"'{name}' has {key}={value}; write {expected}{bound}{minimumText} up to {maximumText}");
        }

        return FormatNumber(n);
    }

    /// <summary>
    ///     Reads a plain decimal as Lua's <c>tonumber</c> after the filter's patterns (<c>-?%d+</c>, <c>-?%d+.%d+</c>,
    ///     <c>-?.%d+</c>).
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The number, or <see langword="null"/> when the text is not such a decimal.</returns>
    /// <remarks>An integer that fits 64 bits is an integer in Lua, so <c>-0</c> is 0, not negative zero.</remarks>
    private static double? ParseDecimal(string value)
    {
        var body = value.StartsWith('-') ? value[1..] : value;
        var dot = body.IndexOf('.', StringComparison.Ordinal);
        var integerPart = dot < 0 ? body : body[..dot];
        var fraction = dot < 0 ? null : body[(dot + 1)..];
        var valid = fraction == null
            ? integerPart.Length > 0 && integerPart.All(char.IsAsciiDigit)
            : integerPart.All(char.IsAsciiDigit) && fraction.Length > 0 && fraction.All(char.IsAsciiDigit);
        if (!valid)
        {
            return null;
        }

        return fraction == null ? LuaInteger(value) : double.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Reads a decimal integer as Lua's <c>tonumber</c>: a 64-bit integer when it fits, else a float.
    /// </summary>
    /// <param name="digits">The digits, with an optional leading minus sign.</param>
    /// <returns>The value.</returns>
    private static double LuaInteger(string digits)
    {
        return long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer)
            ? integer
            : double.Parse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Writes a whole number as Lua's <c>tostring(tonumber(digits))</c>: the integer without leading zeros, or the float
    ///     (<c>%.14g</c>) when it does not fit 64 bits.
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The text.</returns>
    private static string LuaIntegerText(string digits)
    {
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var integer)
            ? integer.ToString(CultureInfo.InvariantCulture)
            : FormatG(double.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture), 14);
    }

    /// <summary>
    ///     Checks <c>size</c>: <c>WIDTHxHEIGHT</c> in pixels, each 1 to 8192.
    /// </summary>
    /// <param name="name">The picture's name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The size without leading zeros.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the size is not valid.</exception>
    private static string SizeValue(string name, string value)
    {
        var x = value.IndexOf('x', StringComparison.Ordinal);
        var width = x < 0 ? string.Empty : value[..x];
        var height = x < 0 ? string.Empty : value[(x + 1)..];
        static bool InRange(string digits) =>
            digits.Length > 0 && digits.All(char.IsAsciiDigit) && LuaInteger(digits) is >= 1 and <= MaxPixels;
        if (!InRange(width) || !InRange(height))
        {
            throw Fail($"'{name}' has size={value}; write WIDTHxHEIGHT in pixels (each 1 to {MaxPixels}), for example 1600x1200");
        }

        return LuaIntegerText(width) + "x" + LuaIntegerText(height);
    }

    /// <summary>
    ///     Checks <c>background</c>: <c>transparent</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c>, or a color name of letters.
    /// </summary>
    /// <param name="name">The picture's name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The value as written.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the value is not such a color.</exception>
    private static string BackgroundValue(string name, string value)
    {
        var hex = value.Length is 7 or 9 && value[0] == '#' && value[1..].All(char.IsAsciiHexDigit);
        if (value != "transparent" && !hex && !value.All(char.IsAsciiLetter))
        {
            throw Fail($"'{name}' has background={value}; write a color such as #F0F0F0, a name such as white, or transparent");
        }

        return value;
    }

    /// <summary>
    ///     Checks <c>page</c>: a whole number counted from 1.
    /// </summary>
    /// <param name="name">The picture's name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The page without leading zeros.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the value is not a page number.</exception>
    private static string PageValue(string name, string value)
    {
        if (value.Length == 0 || !value.All(char.IsAsciiDigit) || !double.IsFinite(LuaInteger(value)) || LuaInteger(value) < 1)
        {
            throw Fail($"'{name}' has page={value}; write the page number, counting from 1");
        }

        return LuaIntegerText(value);
    }

    /// <summary>
    ///     Checks a value against a list of words, ignoring the ASCII letter case.
    /// </summary>
    /// <param name="name">The picture's name.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <param name="words">The accepted words, lowercase.</param>
    /// <param name="expected">The words as the message lists them.</param>
    /// <returns>The value in lowercase.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the value is not one of the words.</exception>
    private static string OneOf(string name, string key, string value, HashSet<string> words, string expected)
    {
        var lower = FilterPaths.AsciiLower(value);
        if (!words.Contains(lower))
        {
            throw Fail($"'{name}' has {key}={value}; write {expected}");
        }

        return lower;
    }

    /// <summary>
    ///     Determines whether a text is a model variable name (<c>^[%a_][%w_]*$</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for ASCII letters, digits, and underscores, not starting with a digit.</returns>
    private static bool IsIdentifier(string text)
    {
        return text.Length > 0 && (char.IsAsciiLetter(text[0]) || text[0] == '_') && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    }

    /// <summary>
    ///     Removes the trailing zeros of the fraction of a number, and the dot when nothing follows it.
    /// </summary>
    /// <param name="text">The number.</param>
    /// <returns>The number without trailing zeros.</returns>
    private static string TrimZeros(string text)
    {
        return text.Contains('.', StringComparison.Ordinal) ? text.TrimEnd('0').TrimEnd('.') : text;
    }

    /// <summary>
    ///     Creates the error of an attribute.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException Fail(string message)
    {
        return new ToolException(ErrorCodes.PandocFilterError, message);
    }

    /// <summary>
    ///     The attributes that stay on a picture: in the order first met, a repeated key with its last value, as a Lua
    ///     table assignment keeps it.
    /// </summary>
    private sealed class KeepList
    {
        /// <summary>
        ///     The attributes in order.
        /// </summary>
        private readonly List<KeyValuePair<string, string>> _items = [];

        /// <summary>
        ///     The position of each key in <see cref="_items"/>.
        /// </summary>
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);

        /// <summary>
        ///     Gets the attributes in order.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Items => _items;

        /// <summary>
        ///     Sets an attribute: a new key is added at the end, a known key gets the new value in its place.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void Set(string key, string value)
        {
            if (_index.TryGetValue(key, out var position))
            {
                _items[position] = new(key, value);
                return;
            }

            _index[key] = _items.Count;
            _items.Add(new(key, value));
        }
    }
}

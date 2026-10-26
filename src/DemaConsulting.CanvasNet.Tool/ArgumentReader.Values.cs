// cspell:ignore canvasnet rgba dpi rrggbbaa
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Tool;

/// <summary>
///     The value grammars of <see cref="ArgumentReader"/>: parsers that turn the text of an option value or a
///     positional operand into a typed value.
/// </summary>
/// <remarks>
///     Every parser takes the text and the name of the option (or operand) it came from, which is used only in
///     the message, and throws a <see cref="ToolException"/> with the code <see cref="ErrorCodes.UsageError"/>
///     (or <see cref="ErrorCodes.SizeLimit"/> for a size beyond <see cref="Surface.MaxDimension"/>) that states
///     the expected form and gives an example. Numbers are always written with a decimal point (invariant
///     culture) and never carry a thousands separator.
/// </remarks>
internal sealed partial class ArgumentReader
{
    /// <summary>
    ///     The largest accepted dots-per-inch value.
    /// </summary>
    private const double MaxDpi = 2400;

    /// <summary>
    ///     The named colors, with their opaque RGB values (the CSS basic colors and orange).
    /// </summary>
    private static readonly Dictionary<string, (byte R, byte G, byte B)> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = (0x00, 0x00, 0x00),
        ["silver"] = (0xC0, 0xC0, 0xC0),
        ["gray"] = (0x80, 0x80, 0x80),
        ["grey"] = (0x80, 0x80, 0x80),
        ["white"] = (0xFF, 0xFF, 0xFF),
        ["maroon"] = (0x80, 0x00, 0x00),
        ["red"] = (0xFF, 0x00, 0x00),
        ["purple"] = (0x80, 0x00, 0x80),
        ["fuchsia"] = (0xFF, 0x00, 0xFF),
        ["magenta"] = (0xFF, 0x00, 0xFF),
        ["green"] = (0x00, 0x80, 0x00),
        ["lime"] = (0x00, 0xFF, 0x00),
        ["olive"] = (0x80, 0x80, 0x00),
        ["yellow"] = (0xFF, 0xFF, 0x00),
        ["navy"] = (0x00, 0x00, 0x80),
        ["blue"] = (0x00, 0x00, 0xFF),
        ["teal"] = (0x00, 0x80, 0x80),
        ["aqua"] = (0x00, 0xFF, 0xFF),
        ["cyan"] = (0x00, 0xFF, 0xFF),
        ["orange"] = (0xFF, 0xA5, 0x00)
    };

    /// <summary>
    ///     Parses an integer within a range.
    /// </summary>
    /// <param name="text">The text, an optional sign followed by digits.</param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not an integer within the range.</exception>
    public static int ParseInt(string text, string option, int min, int max)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < min || value > max)
        {
            throw Invalid(text, option, $"an integer from {min} to {max}", min.ToString(CultureInfo.InvariantCulture));
        }

        return value;
    }

    /// <summary>
    ///     Parses a finite number within a range.
    /// </summary>
    /// <param name="text">The text, for example <c>-12.5</c> or <c>1e3</c>.</param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <param name="min">The smallest accepted value.</param>
    /// <param name="max">The largest accepted value.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a finite number within the range.</exception>
    public static double ParseNumber(string text, string option, double min, double max)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value) || value < min || value > max)
        {
            throw Invalid(
                text,
                option,
                $"a number from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}",
                min.ToString(CultureInfo.InvariantCulture));
        }

        return value;
    }

    /// <summary>
    ///     Parses a page number.
    /// </summary>
    /// <param name="text">The text, a whole number of at least 1 (the first page is 1).</param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <returns>The 1-based page number.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a whole number of at least 1.</exception>
    public static int ParsePage(string text, string option)
    {
        return ParseInt(text, option, 1, int.MaxValue);
    }

    /// <summary>
    ///     Parses a resolution in dots per inch.
    /// </summary>
    /// <param name="text">The text, a number greater than 0 and at most 2400.</param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <returns>The resolution.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a number in range.</exception>
    public static float ParseDpi(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value) || value <= 0 || value > MaxDpi)
        {
            throw Invalid(text, option, $"a resolution greater than 0 and at most {MaxDpi.ToString(CultureInfo.InvariantCulture)} dots per inch", "300");
        }

        return (float)value;
    }

    /// <summary>
    ///     Parses an image size written <c>WxH</c>.
    /// </summary>
    /// <param name="text">The text: a width and a height in whole pixels separated by <c>x</c>, for example <c>800x600</c>.</param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <returns>The width and height in pixels, each at least 1.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the text is not of the form <c>WxH</c>, and with code
    ///     <c>size-limit</c> when a side is larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    public static (int Width, int Height) ParseSize(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        var separator = text.IndexOfAny(['x', 'X']);
        if (separator <= 0 || separator == text.Length - 1
            || !TryParsePixels(text[..separator], out var width)
            || !TryParsePixels(text[(separator + 1)..], out var height)
            || width < 1 || height < 1)
        {
            throw Invalid(text, option, "a size written WxH in whole pixels, each at least 1", "800x600");
        }

        RequireWithinLimit(width, text, option);
        RequireWithinLimit(height, text, option);
        return ((int)width, (int)height);
    }

    /// <summary>
    ///     Parses a rectangle written <c>x,y,w,h</c> or <c>WxH+X+Y</c>.
    /// </summary>
    /// <param name="text">
    ///     The text. Either four comma-separated values <c>x,y,w,h</c> (the origin, then the size), or
    ///     <c>WxH+X+Y</c> (the size, then the origin; <c>WxH</c> alone means the origin 0,0). Each value is
    ///     whole pixels, optionally followed by <c>px</c>, or a percentage written with <c>%</c> (which may
    ///     have a fraction), for example <c>10,10,200,100</c>, <c>200x100+10+10</c>, or <c>50%,0,50%,100%</c>.
    /// </param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <returns>The rectangle; the offsets are not negative and the size is positive.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a valid rectangle.</exception>
    public static GeometrySpec ParseGeometry(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        const string Expected = "a rectangle written x,y,w,h or WxH+X+Y, each value in pixels (10 or 10px) or percent (10%)";
        const string Example = "10,10,200,100";

        var zero = new Dimension(0, false);
        Dimension[]? parts;
        if (text.Contains(','))
        {
            // x,y,w,h
            var fields = text.Split(',');
            parts = fields.Length == 4 ? ParseDimensions(fields) : null;
            if (parts == null)
            {
                throw Invalid(text, option, Expected, Example);
            }

            return Validated(new GeometrySpec(parts[0], parts[1], parts[2], parts[3]), text, option, Expected, Example);
        }

        // WxH+X+Y
        var offsets = text.Split('+');
        var sizeField = offsets[0];
        var separator = FindSizeSeparator(sizeField);
        if ((offsets.Length != 1 && offsets.Length != 3) || separator <= 0)
        {
            throw Invalid(text, option, Expected, Example);
        }

        parts = ParseDimensions([sizeField[..separator], sizeField[(separator + 1)..], .. offsets.Skip(1)]);
        if (parts == null)
        {
            throw Invalid(text, option, Expected, Example);
        }

        var origin = offsets.Length == 3 ? (X: parts[2], Y: parts[3]) : (X: zero, Y: zero);
        return Validated(new GeometrySpec(origin.X, origin.Y, parts[0], parts[1]), text, option, Expected, Example);
    }

    /// <summary>
    ///     Parses a color.
    /// </summary>
    /// <param name="text">
    ///     The text: <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> (CSS order: the alpha
    ///     pair, when present, comes last; the <c>#</c> may be left out, which avoids shell quoting), a color
    ///     name (black, silver, gray, grey, white, maroon, red, purple, fuchsia, magenta, green, lime, olive,
    ///     yellow, navy, blue, teal, aqua, cyan, orange; any case), or <c>transparent</c>.
    /// </param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <returns>The color with straight (non-premultiplied) alpha.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a color.</exception>
    /// <remarks>
    ///     This is deliberately not <see cref="Rgba32.Parse(string)"/>, which reads the alpha pair first.
    /// </remarks>
    public static Rgba32 ParseColor(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        var trimmed = text.Trim();
        if (string.Equals(trimmed, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return new Rgba32(0, 0, 0, 0);
        }

        if (NamedColors.TryGetValue(trimmed, out var named))
        {
            return new Rgba32(named.R, named.G, named.B, 0xFF);
        }

        var hex = trimmed.StartsWith('#') ? trimmed[1..] : trimmed;
        if (hex.Length is 3 or 4 or 6 or 8 && hex.All(char.IsAsciiHexDigit))
        {
            // Short forms double each digit (#abc is #aabbcc); alpha defaults to opaque
            var full = hex.Length <= 4 ? string.Concat(hex.Select(c => new string(c, 2))) : hex;
            var r = Convert.ToByte(full[..2], 16);
            var g = Convert.ToByte(full[2..4], 16);
            var b = Convert.ToByte(full[4..6], 16);
            var a = full.Length == 8 ? Convert.ToByte(full[6..8], 16) : (byte)0xFF;
            return new Rgba32(r, g, b, a);
        }

        throw Invalid(text, option, "a color: #RGB, #RRGGBB, #RRGGBBAA, a color name, or transparent", "#336699");
    }

    /// <summary>
    ///     Parses an angle in degrees.
    /// </summary>
    /// <param name="text">The text: a number, optionally followed by <c>deg</c>, for example <c>90</c>, <c>-90</c>, or <c>12.5deg</c>.</param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <returns>The angle normalized to at least 0 and less than 360 (so <c>-90</c> is <c>270</c>).</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a finite number of degrees.</exception>
    public static double ParseDegrees(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        var number = text.EndsWith("deg", StringComparison.OrdinalIgnoreCase) ? text[..^3] : text;
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
        {
            throw Invalid(text, option, "an angle in degrees", "90");
        }

        var normalized = value % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    /// <summary>
    ///     Parses a flip axis.
    /// </summary>
    /// <param name="text">The text: <c>h</c> or <c>horizontal</c>, <c>v</c> or <c>vertical</c>, or <c>both</c> (any case).</param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <returns>The axis.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the text is not a flip axis.</exception>
    public static FlipAxis ParseFlip(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        return text.ToLowerInvariant() switch
        {
            "h" or "horizontal" => FlipAxis.Horizontal,
            "v" or "vertical" => FlipAxis.Vertical,
            "both" => FlipAxis.Both,
            _ => throw Invalid(text, option, "h, v, or both", "h")
        };
    }

    /// <summary>
    ///     Parses border widths.
    /// </summary>
    /// <param name="text">
    ///     The text: <c>N</c> (the same width on every side) or <c>l,t,r,b</c> (left, top, right, bottom), in
    ///     whole pixels, for example <c>10</c> or <c>0,0,20,20</c>.
    /// </param>
    /// <param name="option">The option or operand name, for messages.</param>
    /// <returns>The border widths, each at least 0.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the text is not of that form, and with code
    ///     <c>size-limit</c> when a width is larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    public static PadSpec ParsePad(string text, string option)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(option);

        var fields = text.Split(',');
        var widths = new long[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            if ((fields.Length != 1 && fields.Length != 4) || !TryParsePixels(fields[i], out widths[i]))
            {
                throw Invalid(text, option, "a border width N or l,t,r,b in whole pixels", "10");
            }

            RequireWithinLimit(widths[i], text, option);
        }

        return fields.Length == 1
            ? new PadSpec((int)widths[0], (int)widths[0], (int)widths[0], (int)widths[0])
            : new PadSpec((int)widths[0], (int)widths[1], (int)widths[2], (int)widths[3]);
    }

    /// <summary>
    ///     Parses <c>-D name=value</c> definitions.
    /// </summary>
    /// <param name="defs">The definitions, each written <c>name=value</c>.</param>
    /// <returns>
    ///     The values by name; a later definition replaces an earlier one with the same name. A value is a
    ///     <see cref="double"/> when it is a finite number (<c>2</c>, <c>-1.5</c>, <c>1e3</c>), a
    ///     <see cref="bool"/> for <c>true</c> or <c>false</c>, and otherwise a <see cref="string"/> (one pair of
    ///     surrounding single or double quotes is removed, so <c>name="a b"</c> and <c>name=a b</c> are the same).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="defs"/> is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when a definition has no <c>=</c>, has a name that is not an
    ///     identifier (letters, digits, <c>_</c>, and <c>$</c>, not starting with a digit), or has a value that
    ///     starts with <c>[</c> (vectors are not supported).
    /// </exception>
    public static IReadOnlyDictionary<string, object?> ParseDefines(IEnumerable<string> defs)
    {
        ArgumentNullException.ThrowIfNull(defs);

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            var equals = def.IndexOf('=');
            var name = equals < 0 ? def : def[..equals];
            if (equals < 0 || !IsIdentifier(name))
            {
                throw Invalid(def, "-D", "name=value, where name is an identifier", "width=20");
            }

            var value = def[(equals + 1)..];
            if (value.StartsWith('['))
            {
                throw Invalid(def, "-D", "a number, true, false, or text (vector values are not supported)", "width=20");
            }

            result[name] = ParseLiteral(value);
        }

        return result;
    }

    /// <summary>
    ///     Parses the value of a definition.
    /// </summary>
    /// <param name="value">The text after the <c>=</c>.</param>
    /// <returns>A <see cref="double"/>, <see cref="bool"/>, or <see cref="string"/>.</returns>
    private static object ParseLiteral(string value)
    {
        if (value == "true")
        {
            return true;
        }

        if (value == "false")
        {
            return false;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
        {
            return number;
        }

        var quoted = value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0];
        return quoted ? value[1..^1] : value;
    }

    /// <summary>
    ///     Determines whether text is an identifier: letters, digits, <c>_</c>, and <c>$</c>, not starting with a digit.
    /// </summary>
    /// <param name="name">The text.</param>
    /// <returns><see langword="true"/> when the text is an identifier.</returns>
    private static bool IsIdentifier(string name)
    {
        return name.Length > 0
               && !char.IsAsciiDigit(name[0])
               && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '$');
    }

    /// <summary>
    ///     Finds the <c>x</c> that separates the width from the height in <c>WxH</c>, where either side may end in <c>px</c>.
    /// </summary>
    /// <param name="text">The text before the first <c>+</c>.</param>
    /// <returns>The index of the separator, or -1.</returns>
    private static int FindSizeSeparator(string text)
    {
        // The x of a "px" unit follows a 'p'; the separator never does
        for (var i = 1; i < text.Length; i++)
        {
            if ((text[i] == 'x' || text[i] == 'X') && text[i - 1] is not ('p' or 'P'))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Parses a list of dimension texts.
    /// </summary>
    /// <param name="fields">The texts.</param>
    /// <returns>The dimensions, or <see langword="null"/> when any text is not a dimension.</returns>
    private static Dimension[]? ParseDimensions(IReadOnlyList<string> fields)
    {
        var result = new Dimension[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            var dimension = ParseDimension(fields[i].Trim());
            if (dimension == null)
            {
                return null;
            }

            result[i] = dimension.Value;
        }

        return result;
    }

    /// <summary>
    ///     Parses one dimension: whole pixels (optionally with <c>px</c>) or a percentage with an optional fraction.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The dimension, or <see langword="null"/> when the text is not one.</returns>
    private static Dimension? ParseDimension(string text)
    {
        if (text.EndsWith('%'))
        {
            var percent = text[..^1];
            return percent.Length > 0
                   && percent.All(c => char.IsAsciiDigit(c) || c == '.')
                   && double.TryParse(percent, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
                   && value <= 100
                ? new Dimension(value, true)
                : null;
        }

        var pixels = text.EndsWith("px", StringComparison.OrdinalIgnoreCase) ? text[..^2] : text;
        return TryParsePixels(pixels, out var count) ? new Dimension(count, false) : null;
    }

    /// <summary>
    ///     Parses whole pixels: one or more digits, no sign.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The value when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the text is a whole number of pixels that fits in an <see cref="int"/>.</returns>
    private static bool TryParsePixels(string text, out long value)
    {
        value = 0;
        return text.Length > 0
               && text.All(char.IsAsciiDigit)
               && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
               && value <= int.MaxValue;
    }

    /// <summary>
    ///     Requires a pixel count not to exceed <see cref="Surface.MaxDimension"/>.
    /// </summary>
    /// <param name="pixels">The pixel count.</param>
    /// <param name="text">The text being parsed, for the message.</param>
    /// <param name="option">The option or operand name, for the message.</param>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when the count is too large.</exception>
    private static void RequireWithinLimit(long pixels, string text, string option)
    {
        if (pixels > Surface.MaxDimension)
        {
            throw new ToolException(
                ErrorCodes.SizeLimit,
                $"The value '{text}' for {option} exceeds the maximum image dimension of {Surface.MaxDimension} pixels.",
                $"Use at most {Surface.MaxDimension} pixels per side.");
        }
    }

    /// <summary>
    ///     Checks that a rectangle has a positive size.
    /// </summary>
    /// <param name="geometry">The rectangle.</param>
    /// <param name="text">The text being parsed, for the message.</param>
    /// <param name="option">The option or operand name, for the message.</param>
    /// <param name="expected">The expected form, for the message.</param>
    /// <param name="example">An example, for the message.</param>
    /// <returns>The rectangle.</returns>
    /// <exception cref="ToolException">Thrown with code <c>usage-error</c> when the width or height is 0.</exception>
    private static GeometrySpec Validated(GeometrySpec geometry, string text, string option, string expected, string example)
    {
        if (geometry.Width.Value <= 0 || geometry.Height.Value <= 0)
        {
            throw Invalid(text, option, expected + " with a width and height greater than 0", example);
        }

        return geometry;
    }

    /// <summary>
    ///     Creates the exception for a value that does not match its grammar.
    /// </summary>
    /// <param name="text">The text as typed.</param>
    /// <param name="option">The option or operand name.</param>
    /// <param name="expected">A description of the expected form.</param>
    /// <param name="example">An example of a valid value.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException Invalid(string text, string option, string expected, string example)
    {
        return new ToolException(
            ErrorCodes.UsageError,
            $"Invalid value '{text}' for {option}: expected {expected}.",
            $"Example: {option} {example}");
    }
}

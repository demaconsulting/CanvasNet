// cspell:ignore openscad
using System.Globalization;
using System.Text.Json;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The grammar of the values a user gives to a model variable (<c>-D width=20</c>, <c>#def.width=20</c>) or to a
///     chart parameter (<c>--param threshold=5</c>, <c>#param.threshold=5</c>), so that the command line, the library and
///     the fragment of a link read one text the same way.
/// </summary>
/// <remarks>
///     <para>
///         <b>Variables</b> (<see cref="TryParseVariable(string, out object?, out string?)"/>): <c>true</c> and
///         <c>false</c> are a <see cref="bool"/>; a finite number (<c>2</c>, <c>-1.5</c>, <c>1e3</c>, always with a decimal
///         point, never a thousands separator) is a <see cref="double"/>; a value that starts with <c>[</c> is refused (vectors
///         are not supported); anything else is a <see cref="string"/>, with one pair of surrounding single or double quotes
///         removed, so <c>label="a b"</c> and <c>label=a b</c> are the same. This is the grammar of <c>-D</c> of the tool.
///     </para>
///     <para>
///         <b>Parameters</b> (<see cref="TryParseParameter(string, out object?, out string?)"/>): <c>true</c>, <c>false</c>
///         and <c>null</c>; a finite number (a <see cref="double"/>); text that starts with <c>{</c>, <c>[</c> or <c>"</c> is
///         read as strict JSON (a <see cref="Dictionary{TKey, TValue}"/> of <see langword="string"/> and
///         <see langword="object"/> for an object, a <see cref="List{T}"/> of <see langword="object"/> for an array, a
///         <see cref="string"/>, and so on), nested at most 32 levels and holding at most 65,536 values, and refused when it
///         is not valid JSON; anything else is a <see cref="string"/> with one pair of surrounding single quotes removed,
///         so <c>region=EU</c> works without quotes. This is the grammar of <c>--param</c> of the tool; the values it
///         produces are the ones <c>VegaLiteRenderOptions.Parameters</c> accepts.
///     </para>
///     <para>
///         The methods never throw for a bad value: they say why it is bad. <see cref="ReadVariables(ImageFragment, string)"/>
///         and <see cref="ReadParameters(ImageFragment, string)"/> read every parameter of a fragment with a prefix and
///         refuse the link with <see cref="FolderImageResolver.FragmentInvalidCode"/> when a value is bad. The class is
///         static and thread-safe.
///     </para>
///     <example>
///         <code language="csharp">
///         var fragment = ImageFragment.Parse("def.width=20&amp;def.label=%22A%22");
///         IReadOnlyDictionary&lt;string, object?&gt; defines = ImageParameterValues.ReadVariables(fragment, "def.");
///         // width: 20.0 (a double), label: "A" (the quotes are removed)
///
///         if (ImageParameterValues.TryParseParameter("{\"x\":[2,6]}", out var brush, out var problem))
///         {
///             // brush is a Dictionary&lt;string, object?&gt; holding a List&lt;object?&gt; of two doubles
///         }
///         </code>
///     </example>
/// </remarks>
public static class ImageParameterValues
{
    /// <summary>
    ///     The deepest nesting of a structured parameter value: 32 levels.
    /// </summary>
    public const int MaxDepth = 32;

    /// <summary>
    ///     The largest number of values one structured parameter value may hold, nested values included: 65,536.
    /// </summary>
    public const int MaxValues = 65_536;

    /// <summary>
    ///     Parses the value of a model variable.
    /// </summary>
    /// <param name="text">The text after the <c>=</c>.</param>
    /// <param name="value">The value when parsing succeeds: a <see cref="double"/>, a <see cref="bool"/> or a <see cref="string"/>.</param>
    /// <param name="problem">Why the text is not usable when parsing fails; <see langword="null"/> otherwise.</param>
    /// <returns><see langword="true"/> when the text is a usable value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static bool TryParseVariable(string text, out object? value, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);

        problem = null;
        if (text.StartsWith('['))
        {
            value = null;
            problem = "vector values are not supported";
            return false;
        }

        value = ParseScalar(text) ?? Unquote(text, true);
        return true;
    }

    /// <summary>
    ///     Parses the value of a chart parameter.
    /// </summary>
    /// <param name="text">The text after the <c>=</c>.</param>
    /// <param name="value">
    ///     The value when parsing succeeds: <see langword="null"/>, a <see cref="bool"/>, a <see cref="double"/>, a
    ///     <see cref="string"/>, a <see cref="List{T}"/> of values or a <see cref="Dictionary{TKey, TValue}"/> of values.
    /// </param>
    /// <param name="problem">Why the text is not usable when parsing fails; <see langword="null"/> otherwise.</param>
    /// <returns><see langword="true"/> when the text is a usable value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static bool TryParseParameter(string text, out object? value, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);

        problem = null;
        if (text == "null")
        {
            value = null;
            return true;
        }

        var scalar = ParseScalar(text);
        if (scalar is not null)
        {
            value = scalar;
            return true;
        }

        if (text.Length > 0 && text[0] is '{' or '[' or '"')
        {
            return TryParseJson(text, out value, out problem);
        }

        value = Unquote(text, false);
        return true;
    }

    /// <summary>
    ///     Reads the model variables of a fragment: every parameter whose name starts with the prefix.
    /// </summary>
    /// <param name="fragment">The fragment of the link.</param>
    /// <param name="prefix">The prefix of the names, for example <c>def.</c>; not empty.</param>
    /// <returns>The values by name (the part after the prefix, as written); empty when there is no such parameter.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fragment"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is null or empty.</exception>
    /// <exception cref="ImageSourceRefusedException">
    ///     Thrown, with the code <see cref="FolderImageResolver.FragmentInvalidCode"/>, when a value is not a usable
    ///     variable value (see <see cref="TryParseVariable(string, out object?, out string?)"/>).
    /// </exception>
    public static IReadOnlyDictionary<string, object?> ReadVariables(ImageFragment fragment, string prefix)
    {
        return Read(fragment, prefix, TryParseVariable, "a number, true, false or text");
    }

    /// <summary>
    ///     Reads the chart parameters of a fragment: every parameter whose name starts with the prefix.
    /// </summary>
    /// <param name="fragment">The fragment of the link.</param>
    /// <param name="prefix">The prefix of the names, for example <c>param.</c>; not empty.</param>
    /// <returns>The values by name (the part after the prefix, as written); empty when there is no such parameter.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fragment"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="prefix"/> is null or empty.</exception>
    /// <exception cref="ImageSourceRefusedException">
    ///     Thrown, with the code <see cref="FolderImageResolver.FragmentInvalidCode"/>, when a value is not a usable
    ///     parameter value (see <see cref="TryParseParameter(string, out object?, out string?)"/>).
    /// </exception>
    public static IReadOnlyDictionary<string, object?> ReadParameters(ImageFragment fragment, string prefix)
    {
        return Read(fragment, prefix, TryParseParameter, "a number, true, false, null, text, or a JSON array or object");
    }

    /// <summary>
    ///     Reads the prefixed parameters of a fragment with a value grammar.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    /// <param name="prefix">The prefix.</param>
    /// <param name="parse">The grammar.</param>
    /// <param name="expected">What a value may be, for the message.</param>
    /// <returns>The values by name.</returns>
    private static Dictionary<string, object?> Read(ImageFragment fragment, string prefix, TryParse parse, string expected)
    {
        ArgumentNullException.ThrowIfNull(fragment);

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var parameter in fragment.GetPrefixed(prefix))
        {
            if (!parse(parameter.Value, out var value, out var problem))
            {
                throw new ImageSourceRefusedException(
                    FolderImageResolver.FragmentInvalidCode,
                    $"the fragment parameter {ImageFragment.Quote(prefix + parameter.Name)} must be {expected}, not {ImageFragment.Quote(parameter.Value)}: {problem}");
            }

            result[parameter.Name] = value;
        }

        return result;
    }

    /// <summary>
    ///     The shape of a value grammar.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The value.</param>
    /// <param name="problem">The problem.</param>
    /// <returns>Whether the text is usable.</returns>
    private delegate bool TryParse(string text, out object? value, out string? problem);

    /// <summary>
    ///     Parses <c>true</c>, <c>false</c> and a finite number.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>A <see cref="bool"/> or a <see cref="double"/>, or <see langword="null"/> when the text is neither.</returns>
    private static object? ParseScalar(string text)
    {
        if (text == "true")
        {
            return true;
        }

        if (text == "false")
        {
            return false;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
        {
            return number;
        }

        return null;
    }

    /// <summary>
    ///     Removes one pair of surrounding quotes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="doubleToo">Whether a pair of double quotes counts as well as a pair of single quotes.</param>
    /// <returns>The text without the quotes, or the text as it is.</returns>
    private static string Unquote(string text, bool doubleToo)
    {
        var quoted = text.Length >= 2 && (text[0] == '\'' || (doubleToo && text[0] == '"')) && text[^1] == text[0];
        return quoted ? text[1..^1] : text;
    }

    /// <summary>
    ///     Reads a JSON text into plain values.
    /// </summary>
    /// <param name="text">The JSON text.</param>
    /// <param name="value">The value.</param>
    /// <param name="problem">The problem.</param>
    /// <returns>Whether the text is valid JSON within the limits.</returns>
    private static bool TryParseJson(string text, out object? value, out string? problem)
    {
        value = null;
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxDepth });
            var remaining = MaxValues;
            value = Convert(document.RootElement, ref remaining);
            problem = null;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            problem = ex is JsonException json ? $"not valid JSON ({FirstLine(json.Message)})" : FirstLine(ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     Converts a JSON element to plain values.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="remaining">The number of values that may still be converted.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">Thrown when a number is not finite or there are too many values.</exception>
    private static object? Convert(JsonElement element, ref int remaining)
    {
        if (--remaining < 0)
        {
            throw new FormatException($"it holds more than {MaxValues.ToString(CultureInfo.InvariantCulture)} values");
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                return element.TryGetDouble(out var number) && double.IsFinite(number)
                    ? number
                    : throw new FormatException("a number is not finite");
            case JsonValueKind.Array:
                var items = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    items.Add(Convert(item, ref remaining));
                }

                return items;
            case JsonValueKind.Object:
                var members = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var member in element.EnumerateObject())
                {
                    members[member.Name] = Convert(member.Value, ref remaining);
                }

                return members;
            default:
                return null;
        }
    }

    /// <summary>
    ///     Gets the first line of a message, so that a note stays on one line.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>Its first line.</returns>
    private static string FirstLine(string message)
    {
        // The message of a JSON failure ends with the position ("Path: $ | LineNumber: 0 | ..."), which means nothing to a user of a link
        var end = message.IndexOfAny(['\r', '\n']);
        var line = end < 0 ? message : message[..end];
        var position = line.IndexOf(" Path:", StringComparison.Ordinal);
        if (position < 0)
        {
            position = line.IndexOf(" LineNumber:", StringComparison.Ordinal);
        }

        return position < 0 ? line : line[..position].TrimEnd();
    }
}

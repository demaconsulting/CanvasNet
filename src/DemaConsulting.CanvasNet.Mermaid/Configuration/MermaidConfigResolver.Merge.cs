// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     The JSON operations of Mermaid's configuration code: <c>assignWithDepth</c>, lodash's <c>merge</c> (as
///     <c>cleanAndMerge</c> uses it), <c>sanitizeDirective</c>, <c>sanitize</c>, <c>sanitizeCss</c>, and the
///     <c>JSON.parse</c> of directive arguments.
/// </content>
internal static partial class MermaidConfigResolver
{
    /// <summary>The nesting depth a JSON text (a directive or the site configuration) may have.</summary>
    internal const int MaxJsonDepth = 64;

    /// <summary>The keys of <c>sanitizeDirective</c> that hold user-defined dictionaries, whose values are validated instead of their keys.</summary>
    private static readonly string[] DictionaryKeys = ["nodeColors", "filenameIcons", "extensionIcons"];

    /// <summary>The configuration keys whose string values <c>sanitizeDirective</c> passes through <c>sanitizeCss</c> (by substring).</summary>
    private static readonly string[] CssKeys = ["themeCSS", "fontFamily", "altFontFamily"];

    /// <summary>
    ///     Mermaid's <c>assignWithDepth(dst, src, { depth })</c>: merges <paramref name="src"/> into
    ///     <paramref name="dst"/> to a depth, without letting a value of another type clobber an object, skipping
    ///     null source values, merging arrays as sets, and assigning shallowly below the depth.
    /// </summary>
    /// <param name="dst">The destination (null is JavaScript's undefined or null).</param>
    /// <param name="src">The source.</param>
    /// <param name="depth">The depth.</param>
    /// <returns>The merged value: <paramref name="dst"/> itself, or a copy of <paramref name="src"/>.</returns>
    internal static JsonNode? AssignWithDepth(JsonNode? dst, JsonNode? src, int depth = 2)
    {
        // An array source is applied element by element to a non-array destination, or merged as a set into an array
        if (src is JsonArray sourceItems)
        {
            if (dst is JsonArray targetItems)
            {
                foreach (var item in sourceItems.Where(item => !targetItems.Any(existing => StrictEquals(existing, item))))
                {
                    targetItems.Add(item?.DeepClone());
                }

                return targetItems;
            }

            foreach (var item in sourceItems)
            {
                AssignWithDepth(dst, item, depth);
            }

            return dst;
        }

        // Below the depth (or into nothing) the source is assigned shallowly, or replaces the destination
        if (dst is null || depth <= 0)
        {
            if (dst is JsonObject shallowTarget && src is JsonObject shallowSource)
            {
                foreach (var (key, value) in shallowSource)
                {
                    shallowTarget[key] = value?.DeepClone();
                }

                return shallowTarget;
            }

            return src?.DeepClone();
        }

        if (src is JsonObject source && dst is JsonObject target)
        {
            foreach (var (key, value) in source)
            {
                AssignEntry(target, key, value, depth);
            }
        }

        return dst;
    }

    /// <summary>
    ///     lodash's <c>merge(target, source)</c> for JSON values: objects are merged deeply, arrays index by index, and
    ///     any other source value (null included) replaces the target value.
    /// </summary>
    /// <param name="target">The target, changed in place.</param>
    /// <param name="source">The source.</param>
    internal static void LodashMerge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            target[key] = MergeValue(target[key], value);
        }
    }

    /// <summary>
    ///     Mermaid's <c>sanitizeDirective</c>: removes the keys that are not configuration keys, the dangerous keys and the
    ///     null values, validates dictionary-style values, passes CSS values through <c>sanitizeCss</c>, and empties
    ///     theme variables with characters outside Mermaid's allowed set (here: removes them).
    /// </summary>
    /// <param name="args">The directive value, changed in place.</param>
    internal static void SanitizeDirective(JsonNode? args)
    {
        if (args is JsonArray items)
        {
            foreach (var item in items)
            {
                SanitizeDirective(item);
            }

            return;
        }

        if (args is not JsonObject obj)
        {
            return;
        }

        foreach (var key in obj.Select(pair => pair.Key).ToList())
        {
            var value = obj[key];
            if (IsRefusedKey(key) || !ConfigKeys.Contains(key) || value is null)
            {
                obj.Remove(key);
                continue;
            }

            if (value is JsonArray list)
            {
                SanitizeDirective(list);
                continue;
            }

            if (value is JsonObject section)
            {
                if (DictionaryKeys.Contains(key))
                {
                    SanitizeDictionary(section, key);
                }
                else
                {
                    SanitizeDirective(section);
                }

                continue;
            }

            if (CssKeys.Any(cssKey => key.Contains(cssKey, StringComparison.Ordinal)) && TryGetString(value, out var css))
            {
                obj[key] = SanitizeCss(css);
            }
        }

        // Theme variables with other characters are emptied by Mermaid; this version removes them (reported earlier)
        if (obj["themeVariables"] is JsonObject themeVariables)
        {
            var refused = themeVariables
                .Where(pair => TryGetString(pair.Value, out var text) && !IsAllowedThemeValue(text))
                .Select(pair => pair.Key)
                .ToList();
            foreach (var key in refused)
            {
                themeVariables.Remove(key);
            }
        }
    }

    /// <summary>
    ///     Mermaid's <c>sanitize</c>: removes the secure keys, the keys that start with <c>__</c> and the string values
    ///     with <c>&lt;</c>, <c>&gt;</c> or <c>url(data:</c>, at every depth.
    /// </summary>
    /// <param name="options">The directive value, changed in place.</param>
    /// <param name="secure">The secure keys of the site configuration.</param>
    internal static void Sanitize(JsonNode? options, IReadOnlyCollection<string> secure)
    {
        if (options is not JsonObject obj)
        {
            return;
        }

        var refused = obj
            .Select(pair => pair.Key)
            .Where(key => key == "secure" || secure.Contains(key) || key.StartsWith("__", StringComparison.Ordinal))
            .ToList();
        foreach (var key in refused)
        {
            obj.Remove(key);
        }

        foreach (var key in obj.Select(pair => pair.Key).ToList())
        {
            var value = obj[key];
            if (TryGetString(value, out var text) && IsUnsafeString(text))
            {
                obj.Remove(key);
            }
            else if (value is JsonObject or JsonArray)
            {
                Sanitize(value, secure);
            }
        }
    }

    /// <summary>
    ///     Mermaid's <c>sanitizeCss</c>: a text whose braces are not balanced (or close before they open) is replaced by
    ///     an error comment.
    /// </summary>
    /// <param name="text">The CSS text.</param>
    /// <returns>The text, or the error comment.</returns>
    internal static string SanitizeCss(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return IsBalancedCss(text) ? text : "{ /* ERROR: Unbalanced CSS */ }";
    }

    /// <summary>
    ///     Gets whether the braces of a CSS text are balanced as <c>sanitizeCss</c> checks them.
    /// </summary>
    /// <param name="text">The CSS text.</param>
    /// <returns>Whether they are.</returns>
    internal static bool IsBalancedCss(string text)
    {
        var open = 0;
        var close = 0;
        foreach (var c in text)
        {
            if (open < close)
            {
                return false;
            }

            if (c == '{')
            {
                open++;
            }
            else if (c == '}')
            {
                close++;
            }
        }

        return open == close;
    }

    /// <summary>
    ///     Gets whether a string value is refused by <c>sanitize</c> (it could inject markup or a data URL).
    /// </summary>
    /// <param name="text">The value.</param>
    /// <returns>Whether it is refused.</returns>
    internal static bool IsUnsafeString(string text) =>
        text.Contains('<', StringComparison.Ordinal) || text.Contains('>', StringComparison.Ordinal) ||
        text.Contains("url(data:", StringComparison.Ordinal);

    /// <summary>
    ///     Gets whether a theme variable value only has the characters <c>sanitizeDirective</c> allows
    ///     (<c>/^[\d "#%(),.;A-Za-z]+$/</c>).
    /// </summary>
    /// <param name="text">The value.</param>
    /// <returns>Whether it is allowed.</returns>
    internal static bool IsAllowedThemeValue(string text) =>
        text.Length > 0 && text.All(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '"' or '#' or '%' or '(' or ')' or ',' or '.' or ';');

    /// <summary>
    ///     Gets whether a key is refused by <c>sanitizeDirective</c> whatever its value (prototype pollution guards).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>Whether it is refused.</returns>
    internal static bool IsRefusedKey(string key) =>
        key.StartsWith("__", StringComparison.Ordinal) || key.Contains("proto", StringComparison.Ordinal) ||
        key.Contains("constr", StringComparison.Ordinal);

    /// <summary>
    ///     Parses a JSON text as <c>JSON.parse</c> does (strict JSON; a repeated key keeps its last value), with numbers as
    ///     doubles and a bounded depth.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="node">The value.</param>
    /// <param name="error">The parse error, when it fails.</param>
    /// <returns>Whether the text is JSON.</returns>
    internal static bool TryParseJson(string text, out JsonNode? node, out string? error)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxJsonDepth });
            node = FromElement(document.RootElement);
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            node = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    ///     Gets whether a JSON value is JavaScript-truthy.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>Whether it is truthy.</returns>
    internal static bool IsTruthy(JsonNode? node) => node switch
    {
        null => false,
        JsonObject or JsonArray => true,
        JsonValue value => value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>().Length > 0,
            JsonValueKind.Number => ColorMath.IsTruthy(value.GetValue<double>()),
            JsonValueKind.True => true,
            _ => false,
        },
        _ => false,
    };

    /// <summary>
    ///     Reads a JSON string.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <param name="text">The string.</param>
    /// <returns>Whether the value is a string.</returns>
    internal static bool TryGetString(JsonNode? node, out string text)
    {
        if (node is JsonValue value && value.GetValueKind() == JsonValueKind.String)
        {
            text = value.GetValue<string>();
            return true;
        }

        text = string.Empty;
        return false;
    }

    /// <summary>
    ///     Converts a parsed JSON element to a node with doubles for numbers and the last value of a repeated key.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The node.</returns>
    private static JsonNode? FromElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var property in element.EnumerateObject())
                {
                    obj[property.Name] = FromElement(property.Value);
                }

                return obj;
            case JsonValueKind.Array:
                var array = new JsonArray();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(FromElement(item));
                }

                return array;
            case JsonValueKind.String:
                return JsonValue.Create(element.GetString());
            case JsonValueKind.Number:
                // JSON.parse reads a number too large for a double as Infinity
                return JsonValue.Create(element.TryGetDouble(out var number)
                    ? number
                    : double.Parse(element.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture));
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            default:
                return null;
        }
    }

    /// <summary>
    ///     Applies one entry of an <c>assignWithDepth</c> source to the destination object.
    /// </summary>
    /// <param name="target">The destination object.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The source value.</param>
    /// <param name="depth">The current depth.</param>
    private static void AssignEntry(JsonObject target, string key, JsonNode? value, int depth)
    {
        // A null source value is skipped
        if (value is null)
        {
            return;
        }

        var exists = target.TryGetPropertyValue(key, out var current);
        var isArray = value is JsonArray;
        if (isArray || value is JsonObject)
        {
            if (!exists)
            {
                current = isArray ? new JsonArray() : new JsonObject();
                target[key] = current;
            }

            // An object or array destination is merged one level deeper; a null destination takes the source; any
            // other value is not clobbered
            if (current is null)
            {
                target[key] = value.DeepClone();
            }
            else if (current is JsonObject or JsonArray)
            {
                var merged = AssignWithDepth(current, value, depth - 1);
                if (!ReferenceEquals(merged, current))
                {
                    target[key] = merged;
                }
            }

            return;
        }

        // A primitive replaces anything that is not an object, an array or null
        if (!exists || current is JsonValue)
        {
            target[key] = value.DeepClone();
        }
    }

    /// <summary>
    ///     Computes the merged value of one lodash <c>merge</c> step.
    /// </summary>
    /// <param name="target">The target value.</param>
    /// <param name="source">The source value.</param>
    /// <returns>The new target value.</returns>
    private static JsonNode? MergeValue(JsonNode? target, JsonNode? source)
    {
        switch (source)
        {
            case JsonObject sourceObject:
                var obj = target is JsonObject existing ? (JsonObject)existing.DeepClone() : [];
                LodashMerge(obj, sourceObject);
                return obj;
            case JsonArray sourceArray:
                var array = target is JsonArray existingArray ? (JsonArray)existingArray.DeepClone() : [];
                for (var i = 0; i < sourceArray.Count; i++)
                {
                    var merged = MergeValue(i < array.Count ? array[i] : null, sourceArray[i]);
                    if (i < array.Count)
                    {
                        array[i] = merged;
                    }
                    else
                    {
                        array.Add(merged);
                    }
                }

                return array;
            default:
                return source?.DeepClone();
        }
    }

    /// <summary>
    ///     Mermaid's <c>sanitizeDictionaryConfig</c>: removes the entries of a user dictionary with a dangerous key or a
    ///     value that is not a string of the expected form.
    /// </summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The configuration key, which selects the value form.</param>
    private static void SanitizeDictionary(JsonObject dictionary, string key)
    {
        foreach (var entry in dictionary.Select(pair => pair.Key).ToList())
        {
            if (IsRefusedKey(entry) || !TryGetString(dictionary[entry], out var text) || !IsDictionaryValue(key, text))
            {
                dictionary.Remove(entry);
            }
        }
    }

    /// <summary>
    ///     Gets whether a dictionary value has the form <c>sanitizeDirective</c> expects: a CSS color for
    ///     <c>nodeColors</c>, an icon name (<c>pack:name</c>) for the icon dictionaries.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="text">The value.</param>
    /// <returns>Whether it is valid.</returns>
    private static bool IsDictionaryValue(string key, string text)
    {
        if (key != "nodeColors")
        {
            // /^[\w-]+(?::[\w-]+)?$/
            var parts = text.Split(':');
            return parts.Length <= 2 && parts.All(part => part.Length > 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'));
        }

        // /^#[\da-f]{3,8}$|^rgb\([\d\s%,.]+\)$|^hsl\([\d\s%,.]+\)$|^[a-z]+$/i
        if (text.StartsWith('#'))
        {
            return text.Length is >= 4 and <= 9 && text.Skip(1).All(char.IsAsciiHexDigit);
        }

        foreach (var function in (ReadOnlySpan<string>)["rgb(", "hsl("])
        {
            if (text.StartsWith(function, StringComparison.OrdinalIgnoreCase) && text.EndsWith(')') && text.Length > function.Length + 1)
            {
                return text[function.Length..^1].All(c => char.IsAsciiDigit(c) || char.IsWhiteSpace(c) || c is '%' or ',' or '.');
            }
        }

        return text.Length > 0 && text.All(char.IsAsciiLetter);
    }

    /// <summary>
    ///     JavaScript's <c>===</c> for the elements of a JSON array (objects and arrays are never equal: they are
    ///     distinct references).
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>Whether they are strictly equal.</returns>
    private static bool StrictEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a is JsonValue && b is JsonValue && JsonNode.DeepEquals(a, b);
    }
}

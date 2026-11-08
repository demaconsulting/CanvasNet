// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     The validation of one configuration layer (the site configuration, the frontmatter, a directive) with located
///     reports: unknown keys, secure keys, refused keys and values, values of the wrong type, and theme names.
/// </content>
internal static partial class MermaidConfigResolver
{
    /// <summary>The number of unknown keys per call for which the nearest known key is computed.</summary>
    private const int MaxKeyHints = 32;

    /// <summary>The keys whose default value is an array: they are configuration keys, but not in <see cref="ConfigKeys"/>.</summary>
    private static readonly string[] ArrayKeys = ["secure", "actorColours", "sectionFills", "sectionColours"];

    /// <summary>The keys this version reads, by path, with the kind of value each must have.</summary>
    private static readonly Dictionary<string, ValueKind> TypedKeys = new(StringComparer.Ordinal)
    {
        ["theme"] = ValueKind.Text,
        ["look"] = ValueKind.Text,
        ["layout"] = ValueKind.Text,
        ["handDrawnSeed"] = ValueKind.Number,
        ["fontFamily"] = ValueKind.Text,
        ["altFontFamily"] = ValueKind.Text,
        ["fontSize"] = ValueKind.Number,
        ["themeCSS"] = ValueKind.Text,
        ["darkMode"] = ValueKind.Boolean,
        ["htmlLabels"] = ValueKind.Boolean,
        ["markdownAutoWrap"] = ValueKind.Boolean,
        ["wrap"] = ValueKind.Boolean,
        ["deterministicIds"] = ValueKind.Boolean,
        ["deterministicIDSeed"] = ValueKind.Text,
        ["arrowMarkerAbsolute"] = ValueKind.Boolean,
        ["securityLevel"] = ValueKind.Text,
        ["maxTextSize"] = ValueKind.Number,
        ["maxEdges"] = ValueKind.Number,
        ["themeVariables"] = ValueKind.Object,
        ["flowchart"] = ValueKind.Object,
        ["flowchart.useMaxWidth"] = ValueKind.Boolean,
        ["flowchart.titleTopMargin"] = ValueKind.Number,
        ["flowchart.subGraphTitleMargin"] = ValueKind.Object,
        ["flowchart.subGraphTitleMargin.top"] = ValueKind.Number,
        ["flowchart.subGraphTitleMargin.bottom"] = ValueKind.Number,
        ["flowchart.diagramPadding"] = ValueKind.Number,
        ["flowchart.htmlLabels"] = ValueKind.Boolean,
        ["flowchart.nodeSpacing"] = ValueKind.Number,
        ["flowchart.rankSpacing"] = ValueKind.Number,
        ["flowchart.curve"] = ValueKind.Text,
        ["flowchart.padding"] = ValueKind.Number,
        ["flowchart.defaultRenderer"] = ValueKind.Text,
        ["flowchart.wrappingWidth"] = ValueKind.Number,
        ["flowchart.inheritDir"] = ValueKind.Boolean,
        ["flowchart.arrowMarkerAbsolute"] = ValueKind.Boolean,
    };

    /// <summary>
    ///     The kind of value a typed key must have.
    /// </summary>
    internal enum ValueKind
    {
        /// <summary>A string.</summary>
        Text,

        /// <summary>A finite number.</summary>
        Number,

        /// <summary>A Boolean.</summary>
        Boolean,

        /// <summary>An object.</summary>
        Object,
    }

    /// <summary>
    ///     Where a configuration layer comes from.
    /// </summary>
    internal enum LayerKind
    {
        /// <summary>The site configuration (<see cref="MermaidRenderOptions.Config"/> and <see cref="MermaidRenderOptions.Theme"/>).</summary>
        Site,

        /// <summary>The <c>config</c> of the frontmatter.</summary>
        Frontmatter,

        /// <summary>An <c>init</c> or <c>initialize</c> directive.</summary>
        Directive,
    }

    /// <summary>
    ///     Validates one layer: reports every problem at its location, and removes the values of the wrong type (which
    ///     Mermaid would use as they are) so that the lower layers keep their values.
    /// </summary>
    /// <param name="context">The reporting context of the call.</param>
    /// <param name="layer">The layer, changed in place.</param>
    /// <param name="kind">Where the layer comes from.</param>
    /// <param name="locate">The location of a key path in the layer.</param>
    /// <param name="moveKey">For a directive, the section its <c>config</c> key is moved to; otherwise null.</param>
    private static void ValidateLayer(ResolveContext context, JsonObject layer, LayerKind kind, Func<string, MermaidLocation> locate, string? moveKey)
    {
        var validator = new LayerValidator(context, kind, locate, moveKey);
        validator.Validate(layer, string.Empty, string.Empty);
    }

    /// <summary>
    ///     Gets the text that names a layer in a message.
    /// </summary>
    /// <param name="kind">The layer.</param>
    /// <returns>The text.</returns>
    private static string Describe(LayerKind kind) => kind switch
    {
        LayerKind.Site => "the site configuration",
        LayerKind.Frontmatter => "the frontmatter",
        _ => "an init directive",
    };

    /// <summary>
    ///     The state of one validation walk.
    /// </summary>
    /// <param name="context">The reporting context of the call.</param>
    /// <param name="kind">Where the layer comes from.</param>
    /// <param name="locate">The location of a key path in the layer.</param>
    /// <param name="moveKey">For a directive, the section its <c>config</c> key is moved to; otherwise null.</param>
    private sealed class LayerValidator(ResolveContext context, LayerKind kind, Func<string, MermaidLocation> locate, string? moveKey)
    {
        /// <summary>Gets a value indicating whether the layer comes from the text (and is sanitized by Mermaid).</summary>
        private bool IsText => kind != LayerKind.Site;

        /// <summary>
        ///     Validates an object of the layer.
        /// </summary>
        /// <param name="obj">The object, changed in place.</param>
        /// <param name="path">The path of the object in the layer, as written.</param>
        /// <param name="schemaPath">The path of the object in the configuration (after a <c>config</c> key moves).</param>
        public void Validate(JsonObject obj, string path, string schemaPath)
        {
            foreach (var (key, value) in obj.ToList())
            {
                var childPath = path.Length == 0 ? key : $"{path}.{key}";
                var location = locate(childPath);

                // A directive's config key is moved to the section of the diagram type; its content is checked there
                if (moveKey is not null && path.Length == 0 && key == "config")
                {
                    if (value is JsonObject moved)
                    {
                        Validate(moved, childPath, moveKey);
                    }

                    continue;
                }

                var childSchemaPath = schemaPath.Length == 0 ? key : $"{schemaPath}.{key}";
                if (!CheckKey(obj, key, childPath, childSchemaPath, location))
                {
                    continue;
                }

                if (TypedKeys.TryGetValue(childSchemaPath, out var expected) && value is not null && !HasKind(value, expected))
                {
                    context.Warn(
                        MermaidDiagnosticCodes.ConfigInvalid,
                        $"the configuration value '{childPath}' in {Describe(kind)} must be {DescribeKind(expected)}; it is ignored",
                        location,
                        $"write {DescribeKind(expected)}, for example {ExampleOf(expected)}");
                    obj.Remove(key);
                    continue;
                }

                CheckValue(obj, key, value, childPath, childSchemaPath, location);
            }
        }

        /// <summary>
        ///     Checks a key: secure, refused and unknown keys are reported.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key.</param>
        /// <param name="path">The path of the key as written.</param>
        /// <param name="schemaPath">The path of the key in the configuration.</param>
        /// <param name="location">The location of the key.</param>
        /// <returns>Whether the value should be checked further.</returns>
        private bool CheckKey(JsonObject obj, string key, string path, string schemaPath, MermaidLocation location)
        {
            if (IsText && (key == "secure" || context.SecureKeys.Contains(key)))
            {
                context.Warn(
                    MermaidDiagnosticCodes.ConfigSecureKey,
                    $"the secure configuration key '{path}' cannot be set by {Describe(kind)}; it is ignored",
                    location,
                    "set it in the site configuration (MermaidRenderOptions.Config) instead");
                return false;
            }

            if (IsText ? IsRefusedKey(key) : key.StartsWith("__", StringComparison.Ordinal))
            {
                context.Warn(
                    MermaidDiagnosticCodes.ConfigUnknownKey,
                    $"the configuration key '{path}' in {Describe(kind)} is refused; it is ignored",
                    location);
                return false;
            }

            // Theme variables of other themes, and the keys whose default is an array, are valid in the site
            // configuration, which Mermaid does not sanitize
            var underThemeVariables = schemaPath.StartsWith("themeVariables.", StringComparison.Ordinal);
            var site = kind == LayerKind.Site;
            if (!ConfigKeys.Contains(key) && !(site && (underThemeVariables || ArrayKeys.Contains(key))))
            {
                context.Warn(
                    MermaidDiagnosticCodes.ConfigUnknownKey,
                    $"'{path}' in {Describe(kind)} is not a configuration key Mermaid accepts there; it is ignored",
                    location,
                    context.NearestKeyHint(key));
                if (IsText)
                {
                    obj.Remove(key);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        ///     Checks a value of a known key: theme names, refused strings, theme variable characters, unbalanced CSS,
        ///     and the content of objects.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="path">The path as written.</param>
        /// <param name="schemaPath">The path in the configuration.</param>
        /// <param name="location">The location of the key.</param>
        private void CheckValue(JsonObject obj, string key, JsonNode? value, string path, string schemaPath, MermaidLocation location)
        {
            if (schemaPath == "theme" && TryGetString(value, out var theme) && !ThemeResolver.IsTheme(theme))
            {
                context.WarnTheme(theme, Describe(kind), location);
            }

            if (TryGetString(value, out var text) && IsText)
            {
                CheckTextValue(obj, key, text, path, schemaPath, location);
                return;
            }

            if (value is not JsonObject child)
            {
                return;
            }

            if (IsText && DictionaryKeys.Contains(key))
            {
                return;
            }

            Validate(child, path, schemaPath);
        }

        /// <summary>
        ///     Checks a string value of the text: the characters Mermaid refuses, and unbalanced CSS.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key.</param>
        /// <param name="text">The value.</param>
        /// <param name="path">The path as written.</param>
        /// <param name="schemaPath">The path in the configuration.</param>
        /// <param name="location">The location of the key.</param>
        private void CheckTextValue(JsonObject obj, string key, string text, string path, string schemaPath, MermaidLocation location)
        {
            if (IsUnsafeString(text))
            {
                context.Warn(
                    MermaidDiagnosticCodes.ConfigInvalid,
                    $"the configuration value '{path}' in {Describe(kind)} contains '<', '>' or 'url(data:'; it is ignored",
                    location);
                return;
            }

            if (schemaPath == $"themeVariables.{key}" && !IsAllowedThemeValue(text))
            {
                context.Warn(
                    MermaidDiagnosticCodes.ConfigInvalid,
                    $"the theme variable '{path}' in {Describe(kind)} has characters other than letters, digits, spaces and \"#%(),.; ; it is ignored",
                    location,
                    "write a color such as #ff6600 or rgb(255, 102, 0)");
                obj.Remove(key);
                return;
            }

            if (CssKeys.Any(cssKey => key.Contains(cssKey, StringComparison.Ordinal)) && !IsBalancedCss(text))
            {
                context.Warn(
                    MermaidDiagnosticCodes.ConfigInvalid,
                    $"the configuration value '{path}' in {Describe(kind)} has unbalanced braces; it is ignored",
                    location);
            }
        }

        /// <summary>
        ///     Gets whether a value has a kind.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="expected">The kind.</param>
        /// <returns>Whether it has.</returns>
        private static bool HasKind(JsonNode value, ValueKind expected) => expected switch
        {
            ValueKind.Object => value is JsonObject,
            ValueKind.Text => value is JsonValue text && text.GetValueKind() == JsonValueKind.String,
            ValueKind.Boolean => value is JsonValue flag && flag.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
            _ => value is JsonValue number && number.GetValueKind() == JsonValueKind.Number && double.IsFinite(number.GetValue<double>()),
        };

        /// <summary>
        ///     Describes a kind of value.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The text.</returns>
        private static string DescribeKind(ValueKind kind) => kind switch
        {
            ValueKind.Object => "an object",
            ValueKind.Text => "a string",
            ValueKind.Boolean => "true or false",
            _ => "a finite number",
        };

        /// <summary>
        ///     Gives an example of a kind of value.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <returns>The example.</returns>
        private static string ExampleOf(ValueKind kind) => kind switch
        {
            ValueKind.Object => "{ \"key\": \"value\" }",
            ValueKind.Text => "\"text\"",
            ValueKind.Boolean => "true",
            _ => "50",
        };
    }

    /// <summary>
    ///     The reporting context of one resolution: the sink (null when nothing is reported), the secure keys and the
    ///     budget of nearest-key hints.
    /// </summary>
    /// <param name="sink">The report sink, or null.</param>
    private sealed class ResolveContext(MermaidReportSink? sink)
    {
        /// <summary>The number of nearest-key hints computed so far.</summary>
        private int _hints;

        /// <summary>Gets or sets the secure keys of the site configuration.</summary>
        public IReadOnlyCollection<string> SecureKeys { get; set; } = DefaultSecureKeys;

        /// <summary>
        ///     Records a warning when reporting.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="message">The message.</param>
        /// <param name="location">The location.</param>
        /// <param name="hint">The hint, or null.</param>
        public void Warn(string code, string message, MermaidLocation location, string? hint = null) =>
            sink?.Warn(code, message, location, hint);

        /// <summary>
        ///     Reports a theme name this version does not have.
        /// </summary>
        /// <param name="theme">The name.</param>
        /// <param name="where">The layer.</param>
        /// <param name="location">The location.</param>
        public void WarnTheme(string theme, string where, MermaidLocation location)
        {
            var known = ThemeResolver.UnsupportedMermaidThemes.Contains(theme)
                ? $"the Mermaid theme '{theme}' in {where} is not supported by this version; it has no effect"
                : $"'{theme}' in {where} is not a Mermaid theme; it has no effect";
            Warn(MermaidDiagnosticCodes.ConfigInvalid, known, location, "use one of the themes default, base, dark, forest or neutral");
        }

        /// <summary>
        ///     Builds the hint for an unknown key: the nearest known key by edit distance, while the budget lasts.
        /// </summary>
        /// <param name="key">The unknown key.</param>
        /// <returns>The hint, or null.</returns>
        public string? NearestKeyHint(string key)
        {
            const int MaxHintKeyLength = 40;
            if (sink is null || key.Length > MaxHintKeyLength || _hints >= MaxKeyHints)
            {
                return null;
            }

            _hints++;
            var probe = key.ToLowerInvariant();
            var nearest = ConfigKeys
                .Order(StringComparer.Ordinal)
                .Select(candidate => (Key: candidate, Distance: DiagramDetector.EditDistance(probe, candidate.ToLowerInvariant())))
                .MinBy(candidate => candidate.Distance);
            return nearest.Key is not null && nearest.Distance <= Math.Max(2, nearest.Key.Length / 3)
                ? string.Create(CultureInfo.InvariantCulture, $"did you mean '{nearest.Key}'?")
                : null;
        }
    }
}

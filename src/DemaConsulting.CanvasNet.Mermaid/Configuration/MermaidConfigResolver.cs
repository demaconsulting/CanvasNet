// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <summary>
///     Resolves the effective configuration of a measure or render call exactly as Mermaid builds its current
///     configuration: the defaults, the site configuration, the frontmatter <c>config</c> and the <c>init</c> directives,
///     with Mermaid's merge rules, sanitizing, secure keys and theme recomputation.
/// </summary>
/// <remarks>
///     <para>
///         <b>Precedence</b> (lowest first): the defaults (with the <c>default</c> theme's variables);
///         <see cref="MermaidRenderOptions.Theme"/> and <see cref="MermaidRenderOptions.Config"/>, which play
///         <c>mermaid.initialize</c> (as <c>mmdc -t</c> and <c>-c</c> do; a <c>theme</c> in the configuration wins over
///         the option); the frontmatter <c>config</c>; the <c>%%{init: ...}%%</c> and <c>%%{initialize: ...}%%</c>
///         directives in text order. A <c>%%{wrap}%%</c> directive sets <c>wrap</c>.
///     </para>
///     <para>
///         <b>Fidelity.</b> The steps are ports of Mermaid's <c>initialize</c>, <c>setSiteConfig</c>,
///         <c>preprocessDiagram</c> (<c>processFrontmatter</c>, <c>processDirectives</c>, <c>detectInit</c>,
///         <c>cleanAndMerge</c>), <c>addDirective</c> and <c>updateCurrentConfig</c>, with <c>assignWithDepth</c>, lodash's
///         <c>merge</c>, <c>sanitizeDirective</c> and <c>sanitize</c>, the flowchart's <c>init</c> and the layout step of the
///         mindmap's <c>getData</c>. So a directive's
///         <c>config</c> key moves to the section of the diagram type, a directive that sets <c>themeVariables</c> without
///         a <c>theme</c> overrides variables without deriving the others again, a JSON error in one init directive
///         makes Mermaid ignore all of them, and a <c>null</c> in a directive cancels the frontmatter's value.
///     </para>
///     <para>
///         <b>Reports</b> (warnings, never errors): <see cref="MermaidDiagnosticCodes.ConfigSecureKey"/> when the text
///         sets a secure key, <see cref="MermaidDiagnosticCodes.ConfigUnknownKey"/> for a key Mermaid does not define
///         (with the nearest key as the hint), and <see cref="MermaidDiagnosticCodes.ConfigInvalid"/> for invalid JSON, a
///         value of the wrong type (dropped, so the lower layer's value is kept), a refused string, a theme this version
///         does not have, and a theme variable that is not a color. Each report is located at the frontmatter key or at
///         the directive.
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use; every call builds its own objects.
///     </para>
/// </remarks>
internal static partial class MermaidConfigResolver
{
    /// <summary>The number of times a theme is computed again after dropping a theme variable that is not a color.</summary>
    private const int MaxThemeRetries = 16;

    /// <summary>
    ///     Resolves the effective configuration of a call.
    /// </summary>
    /// <param name="source">The preprocessed text (frontmatter and directives).</param>
    /// <param name="diagramId">The Mermaid id of the detected diagram (for the flowchart's and the mindmap's initialization), or null before detection.</param>
    /// <param name="options">The options of the call (the site configuration), or null for none.</param>
    /// <param name="sink">The sink the problems are reported to, or null to resolve silently.</param>
    /// <returns>The effective configuration.</returns>
    public static MermaidConfig Resolve(PreprocessedSource source, string? diagramId, MermaidRenderOptions? options, MermaidReportSink? sink)
    {
        ArgumentNullException.ThrowIfNull(source);
        var context = new ResolveContext(sink);
        var locations = new Dictionary<string, MermaidLocation>(StringComparer.Ordinal);

        // mermaid.initialize(site) and setSiteConfig
        var (siteConfig, configFromInitialize) = Initialize(context, options);
        context.SecureKeys = SecureKeysOf(siteConfig);

        // preprocessDiagram: the frontmatter config, the init directives and wrap, merged
        var frontmatter = ReadFrontmatter(context, source, locations);
        var directive = ReadDirectives(context, source, locations);
        var merged = new JsonObject();
        LodashMerge(merged, frontmatter);
        LodashMerge(merged, directive);

        // addDirective
        SanitizeDirective(merged);
        if (IsTruthy(merged["fontFamily"]) && !IsTruthy((merged["themeVariables"] as JsonObject)?["fontFamily"]))
        {
            var themeVariables = merged["themeVariables"] as JsonObject ?? [];
            themeVariables = (JsonObject)themeVariables.DeepClone();
            themeVariables["fontFamily"] = merged["fontFamily"]!.DeepClone();
            merged["themeVariables"] = themeVariables;
        }

        // updateCurrentConfig(siteConfig, [merged])
        var config = UpdateCurrentConfig(context, siteConfig, configFromInitialize, merged, locations);

        // The flowchart's init(config)
        if (diagramId is "flowchart-v2" or "flowchart" or "flowchart-elk")
        {
            InitializeFlowchart(config, configFromInitialize, merged, diagramId);
        }

        // The mindmap database's getData
        if (diagramId == "mindmap")
        {
            InitializeMindmap(config, configFromInitialize, merged);
        }

        return new MermaidConfig(config, locations);
    }

    /// <summary>
    ///     Mermaid's <c>initialize</c> followed by <c>setSiteConfig</c>: the site configuration over the defaults, with the
    ///     theme variables of the site theme.
    /// </summary>
    /// <param name="context">The reporting context.</param>
    /// <param name="options">The options of the call, or null.</param>
    /// <returns>The site configuration and the copy of the options that <c>saveConfigFromInitialize</c> keeps.</returns>
    private static (JsonObject SiteConfig, JsonObject ConfigFromInitialize) Initialize(ResolveContext context, MermaidRenderOptions? options)
    {
        // The user options as mmdc builds them: { startOnLoad: false, theme } shallowly overridden by the configuration
        var user = new JsonObject { ["startOnLoad"] = false };
        if (options?.Theme is { } theme)
        {
            user["theme"] = theme;
        }

        if (options?.Config is { } text && TryParseJson(text, out var parsed, out _) && parsed is JsonObject site)
        {
            foreach (var (key, value) in site.ToList())
            {
                site.Remove(key);
                user[key] = value;
            }
        }

        ValidateLayer(context, user, LayerKind.Site, _ => default, moveKey: null);

        // initialize(userOptions)
        var initOptions = (JsonObject)AssignWithDepth(new JsonObject(), user)!;
        if (IsTruthy(initOptions["fontFamily"]) && !IsTruthy((initOptions["themeVariables"] as JsonObject)?["fontFamily"]))
        {
            if (initOptions["themeVariables"] is not JsonObject)
            {
                initOptions["themeVariables"] = new JsonObject();
            }

            initOptions["themeVariables"]!["fontFamily"] = initOptions["fontFamily"]!.DeepClone();
        }

        var configFromInitialize = (JsonObject)AssignWithDepth(new JsonObject(), initOptions)!;
        var siteTheme = ThemeName(initOptions["theme"]);
        initOptions["themeVariables"] = ComputeTheme(context, siteTheme ?? "default", initOptions["themeVariables"], default);

        // setSiteConfig(options)
        var siteConfig = (JsonObject)AssignWithDepth(AssignWithDepth(new JsonObject(), CreateDefaults()), initOptions)!;
        if (siteTheme is not null)
        {
            siteConfig["themeVariables"] = ComputeTheme(context, siteTheme, initOptions["themeVariables"], default);
        }

        return (siteConfig, configFromInitialize);
    }

    /// <summary>
    ///     Mermaid's <c>updateCurrentConfig</c> with one directive: the sanitized directive over the site configuration,
    ///     and the theme computed again when the directive names a theme.
    /// </summary>
    /// <param name="context">The reporting context.</param>
    /// <param name="siteConfig">The site configuration.</param>
    /// <param name="configFromInitialize">The options kept by <c>saveConfigFromInitialize</c>.</param>
    /// <param name="directive">The merged directive of the text, changed in place by the sanitizing.</param>
    /// <param name="locations">The locations of the top-level keys.</param>
    /// <returns>The current configuration.</returns>
    private static JsonObject UpdateCurrentConfig(
        ResolveContext context,
        JsonObject siteConfig,
        JsonObject configFromInitialize,
        JsonObject directive,
        Dictionary<string, MermaidLocation> locations)
    {
        var config = (JsonObject)AssignWithDepth(new JsonObject(), siteConfig)!;
        Sanitize(directive, context.SecureKeys);
        var sum = (JsonObject)AssignWithDepth(new JsonObject(), directive)!;
        config = (JsonObject)AssignWithDepth(config, sum)!;

        if (ThemeName(sum["theme"]) is not null)
        {
            var initial = (JsonObject)AssignWithDepth(new JsonObject(), configFromInitialize)!;
            var baseVariables = IsTruthy(initial["themeVariables"]) ? initial["themeVariables"]!.DeepClone() : new JsonObject();
            var themeVariables = AssignWithDepth(baseVariables, sum["themeVariables"]);
            if (ThemeName(config["theme"]) is { } theme)
            {
                var location = locations.TryGetValue("themeVariables", out var found) ? found : locations.GetValueOrDefault("theme");
                config["themeVariables"] = ComputeTheme(context, theme, themeVariables, location);
            }
        }

        return config;
    }

    /// <summary>
    ///     The flowchart's <c>init</c>: the layout of the user configuration (or <c>elk</c> for an ELK flowchart), and the
    ///     copy of <c>arrowMarkerAbsolute</c> into the flowchart section.
    /// </summary>
    /// <param name="config">The current configuration, changed in place.</param>
    /// <param name="configFromInitialize">The options kept by <c>saveConfigFromInitialize</c>.</param>
    /// <param name="directive">The sanitized directive of the text.</param>
    /// <param name="diagramId">The Mermaid id of the diagram.</param>
    private static void InitializeFlowchart(JsonObject config, JsonObject configFromInitialize, JsonObject directive, string diagramId)
    {
        if (config["flowchart"] is not JsonObject)
        {
            config["flowchart"] = new JsonObject();
        }

        // The ELK detector sets layout on the configuration it is given, which init receives
        var userDefined = (JsonObject)AssignWithDepth(AssignWithDepth(new JsonObject(), configFromInitialize), directive)!;
        var elk = diagramId == "flowchart-elk" ||
                  (config["flowchart"]!["defaultRenderer"] is JsonValue renderer && TryGetString(renderer, out var name) && name == "elk");
        var layout = userDefined["layout"] ?? (elk ? JsonValue.Create("elk") : config["layout"]);
        if (IsTruthy(layout))
        {
            config["layout"] = layout!.DeepClone();
        }

        config["flowchart"]!["arrowMarkerAbsolute"] = config["arrowMarkerAbsolute"]?.DeepClone();
    }

    /// <summary>
    ///     The layout step of the mindmap database's <c>getData</c>: the layout becomes <c>cose-bilkent</c> unless the user
    ///     configuration (<c>getUserDefinedConfig</c>: the site configuration, the frontmatter and the directives, not the
    ///     defaults) sets one.
    /// </summary>
    /// <param name="config">The current configuration, changed in place.</param>
    /// <param name="configFromInitialize">The options kept by <c>saveConfigFromInitialize</c>.</param>
    /// <param name="directive">The sanitized directive of the text.</param>
    private static void InitializeMindmap(JsonObject config, JsonObject configFromInitialize, JsonObject directive)
    {
        var userDefined = (JsonObject)AssignWithDepth(AssignWithDepth(new JsonObject(), configFromInitialize), directive)!;
        if (userDefined["layout"] is null)
        {
            config["layout"] = "cose-bilkent";
        }
    }

    /// <summary>
    ///     Reads the frontmatter <c>config</c> as Mermaid's <c>processFrontmatter</c> does (with <c>displayMode</c> moved
    ///     to <c>gantt.displayMode</c>), validated, with the locations of its keys.
    /// </summary>
    /// <param name="context">The reporting context.</param>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="locations">The locations of the top-level keys, updated.</param>
    /// <returns>The frontmatter layer.</returns>
    private static JsonObject ReadFrontmatter(ResolveContext context, PreprocessedSource source, Dictionary<string, MermaidLocation> locations)
    {
        var frontmatter = source.Frontmatter;
        if (frontmatter is null)
        {
            return [];
        }

        var paths = new Dictionary<string, MermaidLocation>(StringComparer.Ordinal);
        var layer = new JsonObject();
        if (frontmatter.Config is YamlMapping mapping)
        {
            layer = (JsonObject)FromYaml(mapping, string.Empty, paths)!;
        }
        else if (frontmatter.Config is not null)
        {
            context.Warn(
                MermaidDiagnosticCodes.ConfigInvalid,
                "the frontmatter config must be a mapping; it is ignored",
                frontmatter.Config.Location,
                "write the configuration as keys under config:, for example 'config:' then '  theme: dark'");
        }

        if (frontmatter.DisplayMode is { } displayMode && (layer["gantt"] is JsonObject || !IsTruthy(layer["gantt"])))
        {
            var gantt = layer["gantt"] as JsonObject ?? [];
            gantt["displayMode"] = displayMode;
            layer["gantt"] = gantt;
        }

        MermaidLocation Locate(string path) => paths.TryGetValue(path, out var location) ? location : frontmatter.Location;
        ValidateLayer(context, layer, LayerKind.Frontmatter, Locate, moveKey: null);
        foreach (var (key, _) in layer)
        {
            locations[key] = Locate(key);
        }

        return layer;
    }

    /// <summary>
    ///     Reads the directives as Mermaid's <c>processDirectives</c> does: the <c>init</c> and <c>initialize</c>
    ///     directives merged (<c>detectInit</c>, with the <c>config</c> key moved to the diagram's section), and
    ///     <c>wrap</c>.
    /// </summary>
    /// <param name="context">The reporting context.</param>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="locations">The locations of the top-level keys, updated.</param>
    /// <returns>The directive layer.</returns>
    private static JsonObject ReadDirectives(ResolveContext context, PreprocessedSource source, Dictionary<string, MermaidLocation> locations)
    {
        var moveKey = ConfigMoveKey(source);
        var result = DetectInit(context, source, moveKey, locations) ?? [];

        // A %%{wrap}%% directive; the arguments of every matching directive are parsed first, and an error drops them all
        var wraps = source.Directives.Where(d => d.Type.Contains("wrap", StringComparison.Ordinal)).ToList();
        var parseFailed = wraps.Any(d => d.Arguments is { } text && d.ArgumentsAreJson && !TryParseJson(text, out _, out _));
        if (!parseFailed && wraps.Count > 0)
        {
            if (wraps.Count > 1)
            {
                result["wrap"] = wraps.Any(d => d.Type == "wrap");
                locations["wrap"] = wraps[^1].Location;
            }
            else if (wraps[0].Type == "wrap")
            {
                result["wrap"] = true;
                locations["wrap"] = wraps[0].Location;
            }
        }

        return result;
    }

    /// <summary>
    ///     Mermaid's <c>detectInit</c>: the arguments of the <c>init</c> and <c>initialize</c> directives, merged, with the
    ///     <c>config</c> key moved to the section of the diagram type.
    /// </summary>
    /// <param name="context">The reporting context.</param>
    /// <param name="source">The preprocessed text.</param>
    /// <param name="moveKey">The section the <c>config</c> key moves to.</param>
    /// <param name="locations">The locations of the top-level keys, updated.</param>
    /// <returns>The merged arguments, or null when there are none.</returns>
    private static JsonObject? DetectInit(ResolveContext context, PreprocessedSource source, string moveKey, Dictionary<string, MermaidLocation> locations)
    {
        var inits = source.Directives.Where(d => IsInitType(d.Type)).ToList();
        if (inits.Count == 0)
        {
            return null;
        }

        // JSON.parse runs on every init directive first; one error makes Mermaid ignore them all
        var arguments = new List<(JsonNode? Value, DirectiveText Directive)>();
        foreach (var init in inits)
        {
            if (init.Arguments is { } text && init.ArgumentsAreJson)
            {
                if (!TryParseJson(text, out var node, out var error))
                {
                    context.Warn(
                        MermaidDiagnosticCodes.ConfigInvalid,
                        $"the '{init.Type}' directive is not valid JSON ({error}); Mermaid ignores every init directive of the text",
                        init.Location,
                        "write the arguments as a JSON object, for example %%{init: {\"theme\": \"dark\"}}%%");
                    return null;
                }

                arguments.Add((node, init));
            }
            else
            {
                arguments.Add((init.Arguments is { } word ? JsonValue.Create(word) : null, init));
            }
        }

        // Only a single directive keeps its config key: several are sanitized before the move, which deletes it
        var keepsConfig = arguments.Count == 1;
        foreach (var (value, init) in arguments)
        {
            if (value is JsonObject obj)
            {
                ValidateLayer(context, obj, LayerKind.Directive, _ => init.Location, keepsConfig ? moveKey : null);
                foreach (var (key, _) in obj)
                {
                    locations[key == "config" ? moveKey : key] = init.Location;
                }
            }
            else if (value is not null)
            {
                context.Warn(
                    MermaidDiagnosticCodes.ConfigInvalid,
                    $"the arguments of the '{init.Type}' directive must be a JSON object; they are ignored",
                    init.Location,
                    "write the arguments as a JSON object, for example %%{init: {\"theme\": \"dark\"}}%%");
            }
        }

        // One directive is used as it is; several are sanitized and merged in order
        JsonObject? result;
        if (arguments.Count == 1)
        {
            result = arguments[0].Value as JsonObject;
        }
        else
        {
            result = [];
            foreach (var (value, _) in arguments)
            {
                SanitizeDirective(value);
                AssignWithDepth(result, value);
            }
        }

        if (result is not null && result.TryGetPropertyValue("config", out var moved))
        {
            result.Remove("config");
            result[moveKey] = moved;
        }

        return result;
    }

    /// <summary>
    ///     Gets the section a directive's <c>config</c> key moves to: the type that Mermaid's <c>detectType</c> finds
    ///     with the default configuration (<c>flowchart-v2</c> is <c>flowchart</c>).
    /// </summary>
    /// <param name="source">The preprocessed text.</param>
    /// <returns>The section key.</returns>
    private static string ConfigMoveKey(PreprocessedSource source)
    {
        try
        {
            var id = DiagramDetector.Detect(source.Code, DetectorSettings.Default).MermaidId;
            return id == "flowchart-v2" ? "flowchart" : id;
        }
        catch (MermaidSyntaxException)
        {
            return "flowchart";
        }
    }

    /// <summary>
    ///     Gets whether a directive type is an init directive (Mermaid's <c>/(?:init\b)|(?:initialize\b)/</c> on a type
    ///     of word characters, so the type ends with one of the names).
    /// </summary>
    /// <param name="type">The directive type.</param>
    /// <returns>Whether it is an init directive.</returns>
    private static bool IsInitType(string type) =>
        type.EndsWith("init", StringComparison.Ordinal) || type.EndsWith("initialize", StringComparison.Ordinal);

    /// <summary>
    ///     Computes theme variables; a theme variable that is not a color (khroma throws for it in Mermaid) is reported,
    ///     dropped and the theme computed again.
    /// </summary>
    /// <param name="context">The reporting context.</param>
    /// <param name="theme">The theme name.</param>
    /// <param name="overrides">The user's theme variables.</param>
    /// <param name="location">The location of the theme variables.</param>
    /// <returns>The theme variables.</returns>
    private static JsonObject ComputeTheme(ResolveContext context, string theme, JsonNode? overrides, MermaidLocation location)
    {
        var current = overrides?.DeepClone();
        for (var attempt = 0; attempt <= MaxThemeRetries; attempt++)
        {
            try
            {
                return ThemeResolver.GetThemeVariables(theme, current);
            }
            catch (ColorFormatException ex)
            {
                // The variables whose value khroma could not read are dropped
                var offending = current is JsonObject obj
                    ? obj.Where(pair => SameValue(pair.Value, ex.Value)).Select(pair => pair.Key).ToList()
                    : [];
                foreach (var key in offending)
                {
                    context.Warn(
                        MermaidDiagnosticCodes.ConfigInvalid,
                        $"the theme variable '{key}' is not a color the '{theme}' theme can derive colors from; it is ignored",
                        location,
                        "write a color such as #ff6600, rgb(255, 102, 0), hsl(24, 100%, 50%) or a color name");
                    ((JsonObject)current!).Remove(key);
                }

                if (offending.Count == 0)
                {
                    break;
                }
            }
        }

        context.Warn(
            MermaidDiagnosticCodes.ConfigInvalid,
            $"the theme variables could not be applied to the '{theme}' theme; they are ignored",
            location);
        return ThemeResolver.GetThemeVariables(theme, null);
    }

    /// <summary>
    ///     Gets whether a JSON value holds a JavaScript value that khroma failed on.
    /// </summary>
    /// <param name="node">The JSON value.</param>
    /// <param name="value">The JavaScript value.</param>
    /// <returns>Whether they are the same value.</returns>
    private static bool SameValue(JsonNode? node, object? value) => value switch
    {
        string text => TryGetString(node, out var other) && other == text,
        double number => node is JsonValue v && v.GetValueKind() == System.Text.Json.JsonValueKind.Number && ColorMath.Same(v.GetValue<double>(), number),
        bool flag => node is JsonValue b && b.TryGetValue<bool>(out var other) && other == flag,
        null => node is null,
        ThemeObject => node is JsonObject,
        _ => false,
    };

    /// <summary>
    ///     Gets the theme name of a value when it names a supported theme (Mermaid's <c>theme &amp;&amp; theme in themes</c>).
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The theme name, or null.</returns>
    private static string? ThemeName(JsonNode? node) =>
        TryGetString(node, out var name) && ThemeResolver.IsTheme(name) ? name : null;

    /// <summary>
    ///     Gets the secure keys of a site configuration (its <c>secure</c> list).
    /// </summary>
    /// <param name="siteConfig">The site configuration.</param>
    /// <returns>The keys.</returns>
    private static HashSet<string> SecureKeysOf(JsonObject siteConfig)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (siteConfig["secure"] is JsonArray secure)
        {
            foreach (var item in secure)
            {
                if (TryGetString(item, out var key))
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    /// <summary>
    ///     Converts a YAML value of the frontmatter to JSON, recording the location of every key path.
    /// </summary>
    /// <param name="node">The YAML value.</param>
    /// <param name="path">The path of the value.</param>
    /// <param name="locations">The locations by key path.</param>
    /// <returns>The JSON value.</returns>
    private static JsonNode? FromYaml(YamlNode node, string path, Dictionary<string, MermaidLocation> locations)
    {
        switch (node)
        {
            case YamlMapping mapping:
                var obj = new JsonObject();
                foreach (var entry in mapping.Entries)
                {
                    var childPath = path.Length == 0 ? entry.Key : $"{path}.{entry.Key}";
                    locations[childPath] = entry.KeyLocation;
                    obj[entry.Key] = FromYaml(entry.Value, childPath, locations);
                }

                return obj;
            case YamlSequence sequence:
                var array = new JsonArray();
                foreach (var item in sequence.Items)
                {
                    array.Add(FromYaml(item, path, locations));
                }

                return array;
            case YamlScalar { Value: string text }:
                return JsonValue.Create(text);
            case YamlScalar { Value: bool flag }:
                return JsonValue.Create(flag);
            case YamlScalar { Value: double number }:
                return JsonValue.Create(number);
            default:
                return null;
        }
    }
}

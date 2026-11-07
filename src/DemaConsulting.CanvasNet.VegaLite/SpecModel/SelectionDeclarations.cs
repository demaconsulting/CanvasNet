// Portions derived from Vega-Lite (BSD-3-Clause): the selection parameter and its legacy form (selection.ts, selectioncompat.ts); see third-party/README.md
namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     Reads the declaration of a selection: a parameter with a <c>select</c> (<c>{"name": "brush", "select": "interval"}</c>), or an entry of the legacy
///     <c>selection</c> object of a view (<c>{"selection": {"brush": {"type": "interval"}}}</c>), which Vega-Lite maps to the same thing.
/// </summary>
/// <remarks>
///     <para>
///         The legacy form maps as Vega-Lite's <c>selectioncompat</c> does: the type <c>single</c> is a point selection that does not toggle, <c>multi</c> a point selection,
///         <c>interval</c> an interval selection, <c>init</c> is the <c>value</c>, and <c>empty: "none"</c> makes every predicate on the selection empty-false (the default
///         <c>all</c> is empty-true). The class reads the specification tree only: it is static and pure. A declaration is addressed by the node that holds it (the parameter object, or
///         the entry of the legacy object), whose parent chain gives the view it belongs to.
///     </para>
/// </remarks>
internal static partial class SelectionDeclarations
{
    /// <summary>The field that holds the identity of a row in a selection over row identities (Vega-Lite's <c>SELECTION_ID</c>).</summary>
    private const string SelectionIdField = "_vgsid_";

    /// <summary>The encoding channels that a selection can project over (Vega-Lite's <c>SingleDefUnitChannel</c>).</summary>
    private static readonly HashSet<string> ProjectionChannels = new(
        [
            "x", "y", "x2", "y2", "theta", "theta2", "radius", "radius2", "longitude", "longitude2", "latitude", "latitude2", "xOffset", "yOffset",
            "color", "fill", "stroke", "time", "opacity", "fillOpacity", "strokeOpacity", "strokeWidth", "strokeDash", "size", "angle", "shape",
            "text", "key", "href", "url", "description",
        ],
        StringComparer.Ordinal);

    /// <summary>
    ///     Tells whether a node declares a selection.
    /// </summary>
    /// <param name="declaration">The parameter object, or an entry of a legacy <c>selection</c> object.</param>
    /// <returns><see langword="true"/> for a parameter with a <c>select</c> and for an entry of a legacy <c>selection</c> object.</returns>
    public static bool IsSelection(SpecNode declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return declaration.IsObject && (declaration["select"] is not null || IsLegacy(declaration));
    }

    /// <summary>
    ///     Makes the name of a selection a valid variable name, the way Vega-Lite's <c>varName</c> does: every character that is not a letter, a digit or an underscore becomes an underscore, and a name
    ///     that starts with a digit gets a leading underscore.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The variable name; the store of the selection is this name followed by <c>_store</c>.</returns>
    public static string VarName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new System.Text.StringBuilder(name.Length + 1);
        if (name.Length > 0 && name[0] is >= '0' and <= '9')
        {
            builder.Append('_');
        }

        foreach (var c in name)
        {
            builder.Append(c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' ? c : '_');
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Tells whether a node is an entry of a legacy <c>selection</c> object.
    /// </summary>
    /// <param name="declaration">The node.</param>
    /// <returns><see langword="true"/> for the value of a property of a <c>selection</c> object.</returns>
    public static bool IsLegacy(SpecNode declaration) =>
        declaration.IsObject && declaration.Parent is { IsObject: true, Name: "selection" } holder && holder.Parent is { IsObject: true };

    /// <summary>
    ///     Gets the name of a selection.
    /// </summary>
    /// <param name="declaration">The declaration node.</param>
    /// <returns>The <c>name</c> of a parameter, the key of a legacy entry; null when there is none.</returns>
    public static string? NameOf(SpecNode declaration) =>
        IsLegacy(declaration) ? declaration.Name : declaration["name"]?.StringValue;

    /// <summary>
    ///     Gets the type of a selection: <c>point</c> or <c>interval</c>.
    /// </summary>
    /// <param name="declaration">The declaration node.</param>
    /// <returns>The type; null when the declaration names a type that does not exist.</returns>
    public static string? TypeOf(SpecNode declaration)
    {
        var select = declaration["select"];
        var text = IsLegacy(declaration) ? declaration["type"]?.StringValue : select?.StringValue ?? select?["type"]?.StringValue;
        return text switch
        {
            "point" or "single" or "multi" => "point",
            "interval" => "interval",
            _ => null,
        };
    }

    /// <summary>
    ///     Gets the object that holds the options of the selection (<c>select</c> when it is an object, the legacy entry itself).
    /// </summary>
    /// <param name="declaration">The declaration node.</param>
    /// <returns>The object; null when the selection is written as a bare type name.</returns>
    public static SpecNode? OptionsOf(SpecNode declaration)
    {
        if (IsLegacy(declaration))
        {
            return declaration;
        }

        return declaration["select"] is { IsObject: true } select ? select : null;
    }

    /// <summary>
    ///     Gets the initial value of a selection.
    /// </summary>
    /// <param name="declaration">The declaration node.</param>
    /// <returns>The <c>value</c> of a parameter, the <c>init</c> of a legacy entry; null when there is none.</returns>
    public static SpecNode? ValueOf(SpecNode declaration) =>
        IsLegacy(declaration) ? declaration["init"] : declaration["value"];

    /// <summary>
    ///     Tells whether a legacy selection has <c>empty: "none"</c>, so that a predicate on it that does not say otherwise is empty-false.
    /// </summary>
    /// <param name="declaration">The declaration node.</param>
    /// <returns><see langword="true"/> when the legacy entry says <c>none</c>.</returns>
    public static bool EmptyIsNone(SpecNode declaration) =>
        IsLegacy(declaration) && declaration["empty"]?.StringValue == "none";

    /// <summary>
    ///     Gets how the tuples of the units are combined: the <c>resolve</c> of the selection, else the <c>resolve</c> of the default configuration of its type, else <c>global</c>.
    /// </summary>
    /// <param name="declaration">The declaration node.</param>
    /// <param name="type">The type of the selection.</param>
    /// <returns><c>global</c>, <c>union</c> or <c>intersect</c>.</returns>
    public static string ResolveOf(SpecNode declaration, string type)
    {
        if (OptionsOf(declaration)?["resolve"] is { IsString: true } own)
        {
            return own.StringValue!;
        }

        SpecNode root = declaration;
        while (root.Parent is { } up)
        {
            root = up;
        }

        return root["config"]?["selection"]?[type]?["resolve"] is { IsString: true } configured ? configured.StringValue! : "global";
    }

    /// <summary>
    ///     Tells whether a name is an encoding channel that a selection projects over (Vega-Lite's <c>isSingleDefUnitChannel</c>).
    /// </summary>
    /// <param name="name">The key of an initial value, or a channel name.</param>
    /// <returns><see langword="true"/> for the name of a channel.</returns>
    public static bool IsProjectionChannel(string name) => ProjectionChannels.Contains(name);

    /// <summary>
    ///     Tells whether a selection is projected over the identity of the rows (Vega-Lite's <c>project.hasSelectionId</c>, which chooses <c>vlSelectionIdTest</c> over <c>vlSelectionTest</c>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This follows <c>project.ts</c> for the fields of the projection, as the specification writes it: an explicit <c>fields</c> or <c>encodings</c> decides, and its presence is what counts, so an
    ///         empty list is an empty projection that does not fall back to the defaults (the same holds for the model). Without one the keys of the initial value decide (a key that is not a channel is
    ///         a field of a point selection; for an interval it makes the default encodings apply), and without those the defaults of the selection type decide: the <c>fields</c> of the
    ///         <c>selection.point</c> of the configuration of the specification, else the row identity (<c>_vgsid_</c>) for a point selection; the interval has none. The encodings never project the row
    ///         identity.
    ///     </para>
    ///     <para>
    ///         Without <paramref name="config"/> the defaults are those of the specification's own configuration. The text of a predicate is written without the caller's configuration
    ///         (<see cref="VegaLiteRenderOptions.Config"/>) and the caller's values, which the specification does not know; the selection functions of the expression engine give the same
    ///         answer for the store of a field projection whichever of the two is named, so the text of the predicate stays correct when they decide differently. A finding about what
    ///         the renderer draws passes the effective configuration (defaults, then the specification, then the caller), which is what the selection model projects with.
    ///     </para>
    /// </remarks>
    /// <param name="declaration">The declaration node.</param>
    /// <param name="config">The effective configuration whose <c>selection</c> defaults decide; null to use the specification's own configuration.</param>
    /// <returns><see langword="true"/> when the projection has the field <c>_vgsid_</c>.</returns>
    public static bool UsesRowIdentity(SpecNode declaration, ResolvedConfig? config = null)
    {
        if (TypeOf(declaration) is not { } type)
        {
            return false;
        }

        var options = OptionsOf(declaration);
        List<string>? configuredEncodings;
        List<string>? configuredFields;
        if (config is null)
        {
            var defaults = ConfigOf(declaration)?["selection"]?[type];
            configuredEncodings = StringsOf(defaults?["encodings"]);
            configuredFields = StringsOf(defaults?["fields"]);
        }
        else
        {
            var defaults = config.VegaLite["selection"]?[type];
            configuredEncodings = StringsOf(defaults?["encodings"]);
            configuredFields = StringsOf(defaults?["fields"]);
        }

        var fields = StringsOf(options?["fields"]);
        var encodings = StringsOf(options?["encodings"]);
        var defaultEncodings = configuredEncodings ?? (type == "interval" ? ["x", "y"] : null);
        var defaultFields = configuredFields ?? (type == "point" ? [SelectionIdField] : null);

        // The keys of the initial value (a mapping, or each mapping of a list) name the projection when the selection does not
        if (fields is null && encodings is null && ValueOf(declaration) is { } value)
        {
            IEnumerable<SpecNode> mappings = value.IsArray ? value.Items : [value];
            foreach (var key in mappings.Where(m => m.IsObject).SelectMany(m => m.Properties.Select(p => p.Name!)))
            {
                if (IsProjectionChannel(key))
                {
                    (encodings ??= []).Add(key);
                }
                else if (type == "interval")
                {
                    encodings = defaultEncodings is null ? null : [.. defaultEncodings];
                }
                else
                {
                    (fields ??= []).Add(key);
                }
            }
        }

        if (fields is null && encodings is null)
        {
            fields = defaultFields;
        }

        return fields?.Contains(SelectionIdField, StringComparer.Ordinal) == true;
    }

    /// <summary>
    ///     Gets the <c>config</c> of the specification that a declaration belongs to.
    /// </summary>
    /// <param name="declaration">The declaration node.</param>
    /// <returns>The <c>config</c> object of the root; null when there is none.</returns>
    private static SpecNode? ConfigOf(SpecNode declaration)
    {
        SpecNode root = declaration;
        while (root.Parent is { } up)
        {
            root = up;
        }

        return root["config"] is { IsObject: true } config ? config : null;
    }

    /// <summary>
    ///     Reads a list of texts.
    /// </summary>
    /// <param name="node">The value, or null.</param>
    /// <returns>The texts of an array (an empty array is an empty list); null when the value is not an array.</returns>
    private static List<string>? StringsOf(SpecNode? node) =>
        node is { IsArray: true } ? [.. node.Items.Where(i => i.IsString).Select(i => i.StringValue!)] : null;

    /// <summary>
    ///     Reads a list of texts from a configuration value.
    /// </summary>
    /// <param name="node">The value, or null.</param>
    /// <returns>The texts of an array (an empty array is an empty list); null when the value is not an array.</returns>
    private static List<string>? StringsOf(ConfigNode? node) =>
        node is { Kind: ConfigNodeKind.Array } ? [.. node.Items.Where(i => i.Kind == ConfigNodeKind.String).Select(i => i.StringValue!)] : null;

    /// <summary>
    ///     Lists the selection declarations of a view: its <c>params</c> with a <c>select</c>, then the entries of its legacy <c>selection</c> object.
    /// </summary>
    /// <param name="view">The view object.</param>
    /// <returns>The declaration nodes in document order.</returns>
    public static IEnumerable<SpecNode> DeclarationsOf(SpecNode view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return ReadDeclarations(view);
    }

    /// <summary>
    ///     Reads the selection declarations of a view lazily.
    /// </summary>
    /// <param name="view">The view object.</param>
    /// <returns>The declaration nodes in document order.</returns>
    private static IEnumerable<SpecNode> ReadDeclarations(SpecNode view)
    {
        if (view["params"] is { IsArray: true } parameters)
        {
            foreach (var parameter in parameters.Items.Where(p => p.IsObject && p["select"] is not null && p["name"] is { IsString: true }))
            {
                yield return parameter;
            }
        }

        if (view["selection"] is { IsObject: true } legacy)
        {
            foreach (var entry in legacy.Properties.Where(p => p.IsObject))
            {
                yield return entry;
            }
        }
    }
}

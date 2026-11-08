using System.Text.Json.Nodes;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     Mutates a valid specification (the repository's fixtures and the generated specifications) at random: a leaf is replaced by a boundary value, a
///     key is deleted or inserted, an array is multiplied, a subtree is swapped with one of another specification, a type name is flipped. The same seed gives the same mutation on
///     every runtime (see <see cref="FuzzRandom"/>).
/// </summary>
internal static class VegaLiteFuzzMutator
{
    /// <summary>The deepest nesting that is walked.</summary>
    private const int MaxDepth = 40;

    /// <summary>The keys that are inserted into objects: properties of marks, encodings, scales, axes, legends, transforms and layouts that matter to the limits.</summary>
    private static readonly string[] KeyPool =
    [
        "width", "height", "step", "columns", "spacing", "tickCount", "labelLimit", "labelAngle", "size", "strokeWidth", "opacity", "fontSize", "padding", "offset", "maxbins", "extent", "steps", "minsteps",
        "maxsteps", "bandwidth", "order", "limit", "frame", "probs", "param", "on", "as", "groupby", "sort", "stack", "bin", "aggregate", "timeUnit", "scale", "axis", "legend", "format", "title", "domain",
        "range", "zero", "nice", "exponent", "base", "constant", "paddingInner", "paddingOuter", "rangeMin", "rangeMax", "values", "symbolSize", "gradientLength", "gradientThickness", "labelPadding",
        "titlePadding", "tickSize", "dash", "strokeDash", "interpolate", "tension", "cornerRadius", "innerRadius", "radius", "theta", "padAngle", "thickness", "binSpacing", "discreteBandSize", "aspect",
        "shape", "text", "url", "dx", "dy", "angle", "lineHeight", "lineBreak", "align", "baseline", "invalid", "clip", "point", "line", "condition", "value", "datum", "field", "type", "expr", "test", "filter",
        "calculate", "keyvals", "method", "fields", "encodings", "autosize", "view", "config", "resolve", "bounds",
    ];

    /// <summary>The numbers that sit on the edges of the integer and floating point ranges.</summary>
    private static readonly double[] BoundaryNumbers =
    [
        0, -0.0, 1, -1, 0.5, 2, 3, 10, 100, 1e-9, 1e-300, double.Epsilon, 255, 65535, 1e6, 1e9, 2147483647, 2147483648, 4294967296, 9.3e18, 1e300, double.MaxValue, -1e300, -2147483648,
    ];

    /// <summary>The texts that are hostile to parsers, formatters and expression evaluators.</summary>
    private static readonly string[] HostileTexts =
    [
        string.Empty, " ", "\u0000", "é", "\ud83d\ude00", "a\nb", "datum.", "((((", "{x}", "%Y-%m-%d", "1000000000d", ".1000000f", "$,.2f", "\\u", "container", "1e999",
    ];

    /// <summary>The names that are exchanged for each other: types of fields, marks, scales and operations.</summary>
    private static readonly string[] TypeNames =
    [
        "quantitative", "nominal", "ordinal", "temporal", "bar", "point", "line", "area", "rect", "text", "arc", "tick", "rule", "image", "trail", "boxplot", "linear", "log", "pow", "time", "band", "point", "sum", "mean",
        "count", "median", "distinct", "stack", "zero", "center", "normalize", "pad", "fit", "none",
    ];

    /// <summary>
    ///     Mutates a specification.
    /// </summary>
    /// <param name="spec">The specification; it is not changed.</param>
    /// <param name="other">Another specification that supplies subtrees for swapping; it is not changed.</param>
    /// <param name="rng">The random source.</param>
    /// <returns>The mutated copy.</returns>
    public static JsonNode Mutate(JsonNode spec, JsonNode other, FuzzRandom rng)
    {
        var root = spec.DeepClone();
        var count = rng.Next(1, 4);
        for (var i = 0; i < count; i++)
        {
            var sites = new List<(JsonNode Parent, string? Key, int Index, JsonNode? Node)>();
            Collect(root, sites, 0);
            if (sites.Count == 0)
            {
                break;
            }

            ApplyOne(root, sites, rng.Pick(sites), other, rng);
        }

        return root;
    }

    /// <summary>
    ///     Collects every child of every object and array.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="sites">The collected children.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void Collect(JsonNode node, List<(JsonNode Parent, string? Key, int Index, JsonNode? Node)> sites, int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj)
                {
                    sites.Add((obj, key, -1, child));
                    if (child is not null)
                    {
                        Collect(child, sites, depth + 1);
                    }
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    sites.Add((array, null, i, array[i]));
                    if (array[i] is { } child)
                    {
                        Collect(child, sites, depth + 1);
                    }
                }

                break;
        }
    }

    /// <summary>
    ///     Applies one mutation at a site.
    /// </summary>
    /// <param name="root">The root, for the rare mutation that replaces it.</param>
    /// <param name="sites">All the sites.</param>
    /// <param name="site">The site.</param>
    /// <param name="other">The other specification.</param>
    /// <param name="rng">The random source.</param>
    private static void ApplyOne(JsonNode root, List<(JsonNode Parent, string? Key, int Index, JsonNode? Node)> sites, (JsonNode Parent, string? Key, int Index, JsonNode? Node) site, JsonNode other, FuzzRandom rng)
    {
        _ = root;
        switch (rng.Next(10))
        {
            case 0 or 1 or 2:
                Set(site, ReplacementLeaf(rng));
                break;
            case 3:
                Set(site, site.Node is JsonValue value && value.TryGetValue<double>(out var number) ? JsonValue.Create(Scaled(number, rng)) : ReplacementLeaf(rng));
                break;
            case 4:
                Delete(site);
                break;
            case 5:
                InsertKey(sites, rng);
                break;
            case 6:
                Multiply(sites, rng);
                break;
            case 7:
                var donors = new List<(JsonNode Parent, string? Key, int Index, JsonNode? Node)>();
                Collect(other, donors, 0);
                if (donors.Count > 0 && rng.Pick(donors).Node is { } donor)
                {
                    Set(site, donor.DeepClone());
                }

                break;
            case 8:
                if (site.Node is JsonValue text && text.TryGetValue<string>(out var name) && Array.IndexOf(TypeNames, name) >= 0)
                {
                    Set(site, rng.Pick(TypeNames));
                }
                else
                {
                    Set(site, new JsonArray(site.Node?.DeepClone(), site.Node?.DeepClone()));
                }

                break;
            default:
                Set(site, new JsonObject { [rng.Pick(KeyPool)] = ReplacementLeaf(rng) });
                break;
        }
    }

    /// <summary>
    ///     Replaces the node at a site.
    /// </summary>
    /// <param name="site">The site.</param>
    /// <param name="replacement">The replacement.</param>
    private static void Set((JsonNode Parent, string? Key, int Index, JsonNode? Node) site, JsonNode? replacement)
    {
        if (site.Parent is JsonObject obj)
        {
            obj[site.Key!] = replacement;
        }
        else if (site.Parent is JsonArray array && site.Index < array.Count)
        {
            array[site.Index] = replacement;
        }
    }

    /// <summary>
    ///     Removes the node at a site.
    /// </summary>
    /// <param name="site">The site.</param>
    private static void Delete((JsonNode Parent, string? Key, int Index, JsonNode? Node) site)
    {
        if (site.Parent is JsonObject obj)
        {
            obj.Remove(site.Key!);
        }
        else if (site.Parent is JsonArray array && site.Index < array.Count)
        {
            array.RemoveAt(site.Index);
        }
    }

    /// <summary>
    ///     Inserts a key from the pool into a random object.
    /// </summary>
    /// <param name="sites">All the sites.</param>
    /// <param name="rng">The random source.</param>
    private static void InsertKey(List<(JsonNode Parent, string? Key, int Index, JsonNode? Node)> sites, FuzzRandom rng)
    {
        var objects = sites.Where(s => s.Node is JsonObject).Select(s => (JsonObject)s.Node!).ToList();
        if (objects.Count > 0)
        {
            rng.Pick(objects)[rng.Pick(KeyPool)] = ReplacementLeaf(rng);
        }
    }

    /// <summary>
    ///     Doubles or multiplies by fifty the elements of a random array.
    /// </summary>
    /// <param name="sites">All the sites.</param>
    /// <param name="rng">The random source.</param>
    private static void Multiply(List<(JsonNode Parent, string? Key, int Index, JsonNode? Node)> sites, FuzzRandom rng)
    {
        var arrays = sites.Where(s => s.Node is JsonArray { Count: > 0 }).Select(s => (JsonArray)s.Node!).ToList();
        if (arrays.Count == 0)
        {
            return;
        }

        var array = rng.Pick(arrays);
        var copies = rng.Chance(0.7) ? 1 : 49;
        var originals = array.Select(e => e?.DeepClone()).ToList();
        for (var c = 0; c < copies; c++)
        {
            foreach (var element in originals)
            {
                array.Add(element?.DeepClone());
            }
        }
    }

    /// <summary>
    ///     Builds a leaf: a boundary number, a hostile text, null or a boolean.
    /// </summary>
    /// <param name="rng">The random source.</param>
    /// <returns>The leaf.</returns>
    private static JsonNode? ReplacementLeaf(FuzzRandom rng) => rng.Next(10) switch
    {
        0 or 1 or 2 or 3 => JsonValue.Create(rng.Chance(0.3) ? -rng.Pick(BoundaryNumbers) : rng.Pick(BoundaryNumbers)),
        4 => JsonValue.Create(rng.Next(-5, 60)),
        5 or 6 => JsonValue.Create(rng.Pick(HostileTexts)),
        7 => null,
        8 => JsonValue.Create(rng.Chance(0.5)),
        _ => JsonValue.Create(new string('z', rng.Pick([300, 3000]))),
    };

    /// <summary>
    ///     Scales a number by a power of ten or negates it.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <param name="rng">The random source.</param>
    /// <returns>The new number.</returns>
    private static double Scaled(double number, FuzzRandom rng)
    {
        var scaled = rng.Next(4) switch
        {
            0 => number * 1000,
            1 => number / 1000,
            2 => -number,
            _ => number * 1e15,
        };
        return double.IsFinite(scaled) ? scaled : number;
    }
}

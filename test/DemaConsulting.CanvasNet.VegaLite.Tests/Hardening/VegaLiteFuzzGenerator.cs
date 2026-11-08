using System.Globalization;
using System.Text.Json.Nodes;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     Generates Vega-Lite specifications that are shaped like the schema (the right keys in the right places, with values of the right kinds) but are
///     steered towards the edges of every range: a third of all numbers come from a table of boundary values, and every text slot sometimes holds a hostile
///     text. The same seed and index always give the same specification on every runtime (see <see cref="FuzzRandom"/>).
/// </summary>
internal sealed class VegaLiteFuzzGenerator
{
    /// <summary>The numbers that sit on the edges of the integer and floating point ranges.</summary>
    private static readonly double[] BoundaryNumbers =
    [
        0, -0.0, 1, -1, 0.5, 2, 3, 10, 100, 1e-9, 1e-300, double.Epsilon, 255, 65535, 1e6, 1e9, 2147483647, 2147483648, 4294967296, 9.3e18, 1e300, double.MaxValue, -1e300, -2147483648,
    ];

    /// <summary>The texts that are hostile to parsers, formatters and expression evaluators.</summary>
    private static readonly string[] HostileTexts =
    [
        string.Empty, " ", "\u0000", "é", "\ud83d\ude00", "a\nb", "datum.", "((((", "))))", "{x}", "%Y-%m-%d", "%", "1000000000d", ".1000000f", "$,.2f", "~s", "+.3e", "[", "\\u", "'", "\"", "d3", "container", "0", "1e999",
    ];

    /// <summary>The fields of the generated data and of the fields that transforms add.</summary>
    private static readonly string[] NumericFields = ["n0", "n1", "n2", "n3", "w", "x2", "count", "mean_n0", "bin_n0", "bin_n0_end", "value", "prob", "density"];

    /// <summary>The nominal fields.</summary>
    private static readonly string[] NominalFields = ["c0", "c1", "key", "label"];

    /// <summary>The mark types.</summary>
    private static readonly string[] MarkTypes =
    [
        "bar", "bar", "point", "point", "line", "line", "area", "circle", "square", "rect", "rule", "text", "tick", "arc", "trail", "image", "boxplot", "errorbar", "errorband", "geoshape", "point", "bar",
    ];

    /// <summary>The channels of an encoding.</summary>
    private static readonly string[] Channels =
    [
        "x", "y", "x2", "y2", "color", "fill", "stroke", "opacity", "size", "shape", "strokeWidth", "strokeDash", "text", "tooltip", "detail", "order", "theta", "radius", "xOffset", "yOffset", "href", "url",
        "latitude", "longitude", "angle", "fillOpacity", "strokeOpacity", "description", "key",
    ];

    /// <summary>The aggregation operations.</summary>
    private static readonly string[] Aggregates =
    [
        "count", "sum", "mean", "average", "median", "min", "max", "distinct", "valid", "missing", "variance", "stdev", "q1", "q3", "ci0", "ci1", "stderr", "product", "values", "variancep", "stdevp",
    ];

    /// <summary>The window operations.</summary>
    private static readonly string[] WindowOps =
    [
        "row_number", "rank", "dense_rank", "percent_rank", "cume_dist", "ntile", "lag", "lead", "first_value", "last_value", "nth_value", "sum", "mean", "count", "max",
    ];

    /// <summary>The time units.</summary>
    private static readonly string[] TimeUnits =
    [
        "year", "quarter", "month", "week", "day", "dayofyear", "date", "hours", "minutes", "seconds", "milliseconds", "yearmonth", "yearmonthdate", "monthdate", "yearquarter", "utcyear", "utcmonthdate", "hoursminutes",
        "binnedyearmonth", "binnedutcyearmonthdate",
    ];

    /// <summary>The scale types.</summary>
    private static readonly string[] ScaleTypes = ["linear", "log", "pow", "sqrt", "symlog", "time", "utc", "ordinal", "band", "point", "quantile", "quantize", "threshold", "bin-ordinal", "identity"];

    /// <summary>The color schemes and a few names that do not exist.</summary>
    private static readonly string[] Schemes = ["blues", "viridis", "category10", "tableau20", "rainbow", "sinebow", "cubehelix", "spectral", "nonexistent", "redblue"];

    /// <summary>The functions of generated expressions that take numbers.</summary>
    private static readonly string[] NumberFunctions = ["abs", "ceil", "floor", "round", "sqrt", "exp", "log", "sin", "cos", "tan", "atan", "isNaN", "isFinite", "sign", "pow", "min", "max", "clamp", "lerp", "pad", "sequence", "random", "densityNormal", "cumulativeNormal", "quantileNormal", "densityLogNormal", "cumulativeLogNormal", "quantileLogNormal", "densityUniform", "cumulativeUniform", "quantileUniform"];

    /// <summary>The random source.</summary>
    private readonly FuzzRandom _rng;

    /// <summary>The datasets that the specification names.</summary>
    private readonly JsonObject _datasets = [];

    /// <summary>The names of the declared parameters.</summary>
    private readonly List<string> _params = [];

    /// <summary>The number of views that were created.</summary>
    private int _views;

    /// <summary>The share of boundary numbers and hostile texts that this specification gets: zero (ordinary values only), a little, or all of them.</summary>
    private readonly double _wild;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VegaLiteFuzzGenerator"/> class.
    /// </summary>
    /// <param name="rng">The random source.</param>
    /// <param name="clean">Whether the specification gets ordinary values only (so that a mutation supplies the hostility).</param>
    private VegaLiteFuzzGenerator(FuzzRandom rng, bool clean)
    {
        _rng = rng;
        var level = rng.Next(10);
        _wild = clean || level < 4 ? 0 : WildShare(level);
    }

    /// <summary>
    ///     Gets the share of hostile values of a specification that is not clean.
    /// </summary>
    /// <param name="level">A random level from zero to nine.</param>
    /// <returns>The share.</returns>
    private static double WildShare(int level) => level < 8 ? 0.2 : 1.0;

    /// <summary>
    ///     Generates one specification.
    /// </summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="index">The index of the case.</param>
    /// <param name="clean">Whether the specification gets ordinary values only.</param>
    /// <returns>The specification.</returns>
    public static JsonObject Generate(ulong seed, int index, bool clean = false) => new VegaLiteFuzzGenerator(FuzzRandom.ForCase(seed, index), clean).Build();

    /// <summary>
    ///     Builds the top level of the specification.
    /// </summary>
    /// <returns>The specification.</returns>
    private JsonObject Build()
    {
        var spec = new JsonObject();
        if (_rng.Chance(0.5))
        {
            spec["$schema"] = "https://vega.github.io/schema/vega-lite/v6.json";
        }

        if (_rng.Chance(0.2))
        {
            spec["params"] = Params();
        }

        spec["data"] = Data();
        Shell(spec, 0);
        TopLevelProperties(spec);
        if (_datasets.Count > 0)
        {
            spec["datasets"] = _datasets;
        }

        return spec;
    }

    /// <summary>
    ///     Adds the properties that every kind of view shares at the top level (size, padding, title, configuration).
    /// </summary>
    /// <param name="spec">The specification.</param>
    private void TopLevelProperties(JsonObject spec)
    {
        if (_rng.Chance(0.4))
        {
            spec["width"] = LayoutSize(400);
        }

        if (_rng.Chance(0.4))
        {
            spec["height"] = LayoutSize(300);
        }

        MaybeAdd(spec, "padding", 0.15, () => _rng.Chance(0.5) ? Num(0, 30) : new JsonObject { ["left"] = Num(0, 20), ["top"] = Num(0, 20) });
        MaybeAdd(spec, "autosize", 0.15, () => _rng.Chance(0.5) ? _rng.Pick(["pad", "fit", "none", "fit-x", "fit-y"]) : new JsonObject { ["type"] = _rng.Pick(["pad", "fit", "none"]), ["contains"] = _rng.Pick(["content", "padding"]), ["resize"] = _rng.Chance(0.5) });
        MaybeAdd(spec, "background", 0.15, () => Color());
        MaybeAdd(spec, "title", 0.25, () => _rng.Chance(0.5) ? Text() : new JsonObject { ["text"] = Text(), ["subtitle"] = Text(), ["anchor"] = _rng.Pick(["start", "middle", "end"]), ["fontSize"] = Num(1, 40), ["orient"] = _rng.Pick(["top", "bottom", "left", "right"]), ["offset"] = Num(0, 30), ["limit"] = Num(0, 400) });
        MaybeAdd(spec, "usermeta", 0.05, () => new JsonObject { ["k"] = Text() });
        MaybeAdd(spec, "config", 0.3, Config);
        MaybeAdd(spec, "view", 0.1, () => new JsonObject { ["stroke"] = Color(), ["fill"] = Color(), ["continuousWidth"] = Num(10, 300), ["strokeWidth"] = Num(0, 10) });
    }

    /// <summary>
    ///     Builds a configuration object.
    /// </summary>
    /// <returns>The configuration.</returns>
    private JsonObject Config()
    {
        var config = new JsonObject();
        MaybeAdd(config, "axis", 0.5, () => new JsonObject { ["labelFontSize"] = Num(1, 30), ["tickCount"] = Num(0, 20), ["grid"] = _rng.Chance(0.5), ["labelLimit"] = Num(0, 300), ["titlePadding"] = Num(0, 20), ["labelAngle"] = Num(-360, 360) });
        MaybeAdd(config, "mark", 0.3, () => MarkProperties());
        MaybeAdd(config, "bar", 0.2, () => new JsonObject { ["binSpacing"] = Num(0, 10), ["continuousBandSize"] = Num(1, 30), ["discreteBandSize"] = Num(1, 30) });
        MaybeAdd(config, "range", 0.2, () => new JsonObject { ["category"] = new JsonArray(Color(), Color(), Color()), ["ramp"] = new JsonObject { ["scheme"] = _rng.Pick(Schemes) } });
        MaybeAdd(config, "scale", 0.2, () => new JsonObject { ["bandPaddingInner"] = Num(0, 1), ["continuousPadding"] = Num(0, 50), ["maxBandSize"] = Num(1, 100), ["minSize"] = Num(0, 20), ["maxSize"] = Num(0, 2000), ["zero"] = _rng.Chance(0.5), ["invalid"] = ScaleInvalid() });
        MaybeAdd(config, "legend", 0.2, () => new JsonObject { ["symbolSize"] = Num(0, 2000), ["columns"] = Num(0, 6), ["labelLimit"] = Num(0, 200), ["orient"] = _rng.Pick(["right", "left", "top", "bottom", "none", "top-left"]) });
        MaybeAdd(config, "view", 0.2, () => new JsonObject { ["continuousWidth"] = Num(10, 300), ["stroke"] = Color() });
        MaybeAdd(config, "title", 0.1, () => new JsonObject { ["fontSize"] = Num(1, 40) });
        MaybeAdd(config, "customFormatTypes", 0.05, () => JsonValue.Create(true));
        MaybeAdd(config, "numberFormat", 0.1, () => Format());
        MaybeAdd(config, "timeFormat", 0.1, () => Format());
        MaybeAdd(config, "locale", 0.02, () => new JsonObject { ["decimal"] = Text() });
        return config;
    }

    /// <summary>
    ///     Builds a <c>config.scale.invalid</c> object: a few channels with <c>"zero-or-min"</c>, a number or text <c>value</c> (a boundary number, a color, a pixel position) or an unreadable entry.
    /// </summary>
    /// <returns>The object.</returns>
    private JsonObject ScaleInvalid()
    {
        var invalid = new JsonObject();
        foreach (var channel in new[] { "x", "y", "color", "size", "opacity", "strokeWidth", "angle" })
        {
            MaybeAdd(invalid, channel, 0.4, () => _rng.Next(5) switch
            {
                0 => JsonValue.Create("zero-or-min"),
                1 => new JsonObject { ["value"] = Boundary() },
                2 => new JsonObject { ["value"] = Color() },
                3 => new JsonObject { ["value"] = Num(-100, 500) },
                _ => new JsonObject { ["value"] = _rng.Pick<JsonNode?>([null, JsonValue.Create(true), new JsonArray(1)]) },
            });
        }

        return invalid;
    }

    /// <summary>
    ///     Builds the data property.
    /// </summary>
    /// <returns>The data.</returns>
    private JsonNode Data()
    {
        var kind = _rng.Next(100);
        if (kind < 72)
        {
            return new JsonObject { ["values"] = Rows(_rng.Chance(0.05) ? _rng.Next(50, 400) : _rng.Next(0, 13)) };
        }

        if (kind < 84)
        {
            var sequence = new JsonObject { ["start"] = Num(-10, 10), ["stop"] = _rng.Chance(0.1) ? Num(0, 5000) : Num(0, 60) };
            MaybeAdd(sequence, "step", 0.4, () => Num(1, 5));
            MaybeAdd(sequence, "as", 0.3, () => "n0");
            return new JsonObject { ["sequence"] = sequence };
        }

        if (kind < 96)
        {
            var name = "ds" + _datasets.Count;
            _datasets[name] = Rows(_rng.Next(1, 10));
            return new JsonObject { ["name"] = name };
        }

        return new JsonObject { ["name"] = Text() };
    }

    /// <summary>
    ///     Builds rows of made-up data.
    /// </summary>
    /// <param name="count">The number of rows.</param>
    /// <returns>The rows.</returns>
    private JsonArray Rows(int count)
    {
        var rows = new JsonArray();
        for (var i = 0; i < count; i++)
        {
            var row = new JsonObject();
            foreach (var name in new[] { "n0", "n1", "n2", "n3" })
            {
                if (_rng.Chance(0.97))
                {
                    row[name] = DataNumber();
                }
            }

            row["c0"] = _rng.Chance(0.1) ? null : _rng.Pick(["a", "b", "c", "d", ""]);
            row["c1"] = _rng.Chance(0.05) ? Hostile() : _rng.Pick(["p", "q"]);
            row["t0"] = _rng.Chance(0.1) ? _rng.Pick(["not a date", "", "2024-13-45", "0000-01-01", "9999-12-31T23:59:59Z", "1970-01-01"]) : DateText(i);
            if (_rng.Chance(0.05))
            {
                row["list"] = new JsonArray(Num(0, 5), Num(0, 5));
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    ///     Builds a number of the data: mostly small, sometimes null or a boundary value.
    /// </summary>
    /// <returns>The value.</returns>
    private JsonNode? DataNumber()
    {
        var kind = _rng.Next(100);
        if (kind < 8)
        {
            return null;
        }

        if (kind < 18)
        {
            return Boundary();
        }

        return kind < 60 ? Num(-50, 50) : JsonValue.Create(Math.Round((_rng.NextDouble() * 200) - 50, 2));
    }

    /// <summary>
    ///     Builds a date text.
    /// </summary>
    /// <param name="i">The index of the row.</param>
    /// <returns>The text.</returns>
    private string DateText(int i) =>
        new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays((i * 17) + _rng.Next(0, 40)).AddHours(_rng.Next(0, 24)).ToString(_rng.Chance(0.5) ? "yyyy-MM-dd" : "yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Builds the parameters.
    /// </summary>
    /// <returns>The parameters.</returns>
    private JsonArray Params()
    {
        var parameters = new JsonArray();
        var count = _rng.Next(1, 3);
        for (var i = 0; i < count; i++)
        {
            var name = "p" + i;
            _params.Add(name);
            var parameter = new JsonObject { ["name"] = name };
            if (_rng.Chance(0.5))
            {
                parameter["value"] = _rng.Chance(0.3) ? Text() : Num(-10, 10);
                MaybeAdd(parameter, "bind", 0.4, () => new JsonObject { ["input"] = "range", ["min"] = Num(-10, 0), ["max"] = Num(1, 20), ["step"] = Num(0, 3) });
            }
            else
            {
                var select = new JsonObject { ["type"] = _rng.Pick(["point", "interval"]) };
                MaybeAdd(select, "encodings", 0.5, () => new JsonArray(_rng.Pick(["x", "y", "color"])));
                MaybeAdd(select, "fields", 0.3, () => new JsonArray(_rng.Pick(NominalFields)));
                MaybeAdd(select, "on", 0.2, () => Text());
                MaybeAdd(select, "clear", 0.1, () => _rng.Chance(0.5));
                MaybeAdd(select, "nearest", 0.1, () => _rng.Chance(0.5));
                parameter["select"] = select;
                MaybeAdd(parameter, "value", 0.3, () => _rng.Chance(0.5) ? new JsonObject { ["x"] = new JsonArray(Num(0, 5), Num(5, 10)) } : new JsonArray(new JsonObject { ["c0"] = "a" }));
            }

            parameters.Add(parameter);
        }

        return parameters;
    }

    /// <summary>
    ///     Adds the shell of the view (a single view, a layer, a concatenation, a facet or a repeat) to a specification or a view.
    /// </summary>
    /// <param name="spec">The object to extend.</param>
    /// <param name="depth">The nesting depth.</param>
    private void Shell(JsonObject spec, int depth)
    {
        _views++;
        var kind = depth >= 3 || _views > 12 ? 0 : _rng.Next(100);
        if (kind < 50)
        {
            Unit(spec);
        }
        else if (kind < 66)
        {
            Layer(spec, depth);
        }
        else if (kind < 76)
        {
            Concat(spec, depth);
        }
        else if (kind < 86)
        {
            FacetShell(spec, depth);
        }
        else if (kind < 96)
        {
            Repeat(spec, depth);
        }
        else
        {
            spec["vconcat"] = new JsonArray();
        }

        if (_rng.Chance(0.15))
        {
            spec["transform"] = Transforms(spec["transform"] as JsonArray);
        }
    }

    /// <summary>
    ///     Adds a mark, an encoding and transforms to an object.
    /// </summary>
    /// <param name="spec">The object to extend.</param>
    private void Unit(JsonObject spec)
    {
        var type = _rng.Pick(MarkTypes);
        spec["mark"] = _rng.Chance(0.5) ? type : MarkWithProperties(type);
        spec["encoding"] = Encoding(type);
        if (_rng.Chance(0.55))
        {
            spec["transform"] = Transforms(null);
        }

        if (_rng.Chance(0.15))
        {
            spec["width"] = Num(20, 300);
            spec["height"] = Num(20, 300);
        }
    }

    /// <summary>
    ///     Adds layers to an object.
    /// </summary>
    /// <param name="spec">The object to extend.</param>
    /// <param name="depth">The nesting depth.</param>
    private void Layer(JsonObject spec, int depth)
    {
        var layers = new JsonArray();
        for (var i = _rng.Next(1, 4); i > 0; i--)
        {
            var child = new JsonObject();
            if (_rng.Chance(0.15))
            {
                child["data"] = Data();
            }

            // Only unit and layered views can be layered
            _views++;
            if (depth < 2 && _rng.Chance(0.2))
            {
                Layer(child, depth + 1);
            }
            else
            {
                Unit(child);
            }

            layers.Add(child);
        }

        spec["layer"] = layers;
        if (_rng.Chance(0.5))
        {
            spec["encoding"] = Encoding("point");
        }

        if (_rng.Chance(0.2))
        {
            spec["resolve"] = new JsonObject { ["scale"] = new JsonObject { [_rng.Pick(["x", "y", "color", "size"])] = _rng.Pick(["independent", "shared", "bogus"]) }, ["axis"] = new JsonObject { ["y"] = "independent" }, ["legend"] = new JsonObject { ["color"] = "independent" } };
        }
    }

    /// <summary>
    ///     Adds a concatenation to an object.
    /// </summary>
    /// <param name="spec">The object to extend.</param>
    /// <param name="depth">The nesting depth.</param>
    private void Concat(JsonObject spec, int depth)
    {
        var children = new JsonArray();
        for (var i = _rng.Next(0, 5); i > 0; i--)
        {
            var child = new JsonObject();
            Shell(child, depth + 1);
            children.Add(child);
        }

        var key = _rng.Pick(["hconcat", "vconcat", "concat"]);
        spec[key] = children;
        if (key == "concat" || _rng.Chance(0.1))
        {
            MaybeAdd(spec, "columns", 0.8, () => Num(0, 5));
        }

        MaybeAdd(spec, "spacing", 0.2, () => Num(-5, 40));
        MaybeAdd(spec, "bounds", 0.1, () => _rng.Pick(["full", "flush"]));
        MaybeAdd(spec, "center", 0.1, () => _rng.Chance(0.5));
        MaybeAdd(spec, "resolve", 0.2, () => new JsonObject { ["scale"] = new JsonObject { ["y"] = "independent", ["color"] = "independent" } });
    }

    /// <summary>
    ///     Adds a facet to an object (the <c>facet</c> property with a <c>spec</c>, or a <c>row</c> or <c>column</c> channel).
    /// </summary>
    /// <param name="spec">The object to extend.</param>
    /// <param name="depth">The nesting depth.</param>
    private void FacetShell(JsonObject spec, int depth)
    {
        if (_rng.Chance(0.5))
        {
            Unit(spec);
            var encoding = (JsonObject)spec["encoding"]!;
            encoding[_rng.Pick(["row", "column", "facet"])] = FacetDef();
            return;
        }

        spec["facet"] = _rng.Chance(0.5) ? FacetDef() : new JsonObject { ["row"] = FacetDef(), ["column"] = FacetDef() };
        var child = new JsonObject();
        if (_rng.Chance(0.5))
        {
            Unit(child);
        }
        else
        {
            Layer(child, depth + 1);
        }

        spec["spec"] = child;
        MaybeAdd(spec, "columns", 0.5, () => Num(0, 6));
        MaybeAdd(spec, "spacing", 0.2, () => Num(-5, 40));
        MaybeAdd(spec, "resolve", 0.2, () => new JsonObject { ["scale"] = new JsonObject { ["y"] = "independent" }, ["axis"] = new JsonObject { ["x"] = "independent" } });
    }

    /// <summary>
    ///     Builds a facet field definition.
    /// </summary>
    /// <returns>The definition.</returns>
    private JsonObject FacetDef()
    {
        var def = new JsonObject { ["field"] = _rng.Chance(0.15) ? _rng.Pick(NumericFields) : _rng.Pick(NominalFields), ["type"] = _rng.Pick(["nominal", "ordinal", "quantitative", "temporal"]) };
        MaybeAdd(def, "header", 0.3, () => new JsonObject { ["labelAngle"] = Num(-180, 180), ["title"] = Text(), ["labelOrient"] = _rng.Pick(["top", "bottom", "left", "right"]), ["labelFontSize"] = Num(1, 30), ["labelLimit"] = Num(0, 200) });
        MaybeAdd(def, "sort", 0.2, () => _rng.Pick(["ascending", "descending"]));
        MaybeAdd(def, "bin", 0.1, () => true);
        MaybeAdd(def, "timeUnit", 0.1, () => _rng.Pick(TimeUnits));
        MaybeAdd(def, "aggregate", 0.05, () => _rng.Pick(Aggregates));
        return def;
    }

    /// <summary>
    ///     Adds a repeat to an object.
    /// </summary>
    /// <param name="spec">The object to extend.</param>
    /// <param name="depth">The nesting depth.</param>
    private void Repeat(JsonObject spec, int depth)
    {
        var fields = new JsonArray();
        for (var i = _rng.Next(0, 4); i > 0; i--)
        {
            fields.Add(_rng.Pick(NumericFields));
        }

        string reference;
        if (_rng.Chance(0.5))
        {
            spec["repeat"] = fields;
            reference = "repeat";
            MaybeAdd(spec, "columns", 0.5, () => Num(0, 4));
        }
        else
        {
            var columns = new JsonArray();
            for (var i = _rng.Next(0, 3); i > 0; i--)
            {
                columns.Add(_rng.Pick(NumericFields));
            }

            spec["repeat"] = new JsonObject { ["row"] = fields, ["column"] = columns };
            reference = _rng.Pick(["row", "column"]);
        }

        var child = new JsonObject { ["mark"] = _rng.Pick(MarkTypes), ["encoding"] = new JsonObject { ["x"] = new JsonObject { ["field"] = new JsonObject { ["repeat"] = reference }, ["type"] = "quantitative" }, ["y"] = ChannelDef("y") } };
        if (_rng.Chance(0.2))
        {
            child["layer"] = new JsonArray(new JsonObject { ["mark"] = "point" }, new JsonObject { ["mark"] = "line" });
            child.Remove("mark");
        }

        spec["spec"] = child;
        _ = depth;
        MaybeAdd(spec, "spacing", 0.2, () => Num(-5, 40));
    }

    /// <summary>
    ///     Builds a mark definition with properties.
    /// </summary>
    /// <param name="type">The mark type.</param>
    /// <returns>The definition.</returns>
    private JsonObject MarkWithProperties(string type)
    {
        var mark = MarkProperties();
        mark["type"] = type;
        return mark;
    }

    /// <summary>
    ///     Builds a few mark properties.
    /// </summary>
    /// <returns>The properties.</returns>
    private JsonObject MarkProperties()
    {
        var mark = new JsonObject();
        var count = _rng.Next(0, 6);
        for (var i = 0; i < count; i++)
        {
            var key = _rng.Next(48) switch
            {
                0 => "size",
                1 => "strokeWidth",
                2 => "opacity",
                3 => "fillOpacity",
                4 => "strokeOpacity",
                5 => "filled",
                6 => "color",
                7 => "fill",
                8 => "stroke",
                9 => "strokeDash",
                10 => "strokeDashOffset",
                11 => "strokeCap",
                12 => "strokeJoin",
                13 => "interpolate",
                14 => "tension",
                15 => "point",
                16 => "line",
                17 => "cornerRadius",
                18 => "width",
                19 => "height",
                20 => "orient",
                21 => "align",
                22 => "baseline",
                23 => "angle",
                24 => "dx",
                25 => "dy",
                26 => "fontSize",
                27 => "font",
                28 => "fontWeight",
                29 => "limit",
                30 => "lineBreak",
                31 => "lineHeight",
                32 => "text",
                33 => "radius",
                34 => "theta",
                35 => "innerRadius",
                36 => "padAngle",
                37 => "thickness",
                38 => "binSpacing",
                39 => "clip",
                40 => "invalid",
                41 => "shape",
                42 => "aspect",
                43 => "url",
                44 => "extent",
                45 => "tooltip",
                46 => "discreteBandSize",
                _ => "xOffset",
            };
            mark[key] = MarkValue(key);
        }

        return mark;
    }

    /// <summary>
    ///     Builds the value of a mark property.
    /// </summary>
    /// <param name="key">The property.</param>
    /// <returns>The value.</returns>
    private JsonNode? MarkValue(string key)
    {
        if (_params.Count > 0 && _rng.Chance(0.1))
        {
            return new JsonObject { ["expr"] = _rng.Pick(_params) + " * 2" };
        }

        switch (key)
        {
            case "color" or "fill" or "stroke":
                return Color();
            case "filled" or "clip" or "aspect" or "point" or "line" or "tooltip":
                return _rng.Chance(0.2) ? new JsonObject { ["filled"] = _rng.Chance(0.5), ["size"] = Num(0, 500), ["color"] = Color() } : _rng.Chance(0.5);
            case "strokeDash":
                return new JsonArray(Num(0, 10), Num(0, 10));
            case "interpolate":
                return _rng.Pick(["linear", "step", "step-before", "step-after", "basis", "cardinal", "monotone", "natural", "catmull-rom", "bundle", "basis-closed", "bogus"]);
            case "orient":
                return _rng.Pick(["horizontal", "vertical", "diagonal"]);
            case "align":
                return _rng.Pick(["left", "center", "right", "justify"]);
            case "baseline":
                return _rng.Pick(["top", "middle", "bottom", "alphabetic", "line-top", "line-bottom"]);
            case "invalid":
                return _rng.Pick(["filter", "break-paths-filter-domains", "break-paths-show-domains", "show", null, "bogus"]);
            case "shape":
                return _rng.Chance(0.5) ? _rng.Pick(["circle", "square", "cross", "diamond", "triangle-up", "triangle-down", "triangle-left", "arrow", "wedge", "stroke", "bogus"]) : _rng.Pick(["M0,0L1,1", "M-1,-1h2v2z", "M0,0A1,1,0,1,1,0,1", "M", "M0,0C1,1,2,2,3,3Z", "M1e300,0L-1e300,5", "L1,1", "M0,0Q1,1,2,2T3,3"]);
            case "text" or "url" or "font" or "fontWeight" or "strokeCap" or "strokeJoin" or "lineBreak":
                return Text();
            case "extent":
                return _rng.Chance(0.5) ? _rng.Pick(["min-max"]) : Num(0, 3);
            default:
                return Num(0, 100);
        }
    }

    /// <summary>
    ///     Builds an encoding.
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <returns>The encoding.</returns>
    private JsonObject Encoding(string markType)
    {
        var encoding = new JsonObject();
        var count = _rng.Next(1, 7);
        if (markType == "arc" && _rng.Chance(0.5))
        {
            encoding["theta"] = ChannelDef("theta");
            count--;
        }

        for (var i = 0; i < count; i++)
        {
            var channel = _rng.Chance(0.4) ? _rng.Pick(["x", "y"]) : _rng.Pick(Channels);
            encoding[channel] = channel is "detail" or "tooltip" or "order" && _rng.Chance(0.3) ? new JsonArray(ChannelDef(channel), ChannelDef(channel)) : ChannelDef(channel);
        }

        return encoding;
    }

    /// <summary>
    ///     Builds a channel definition.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The definition.</returns>
    private JsonObject ChannelDef(string channel)
    {
        var def = new JsonObject();
        var kind = _rng.Next(100);
        if (kind < 8)
        {
            def["value"] = _rng.Chance(0.5) ? Num(0, 300) : Color();
        }
        else if (kind < 13)
        {
            def["datum"] = _rng.Chance(0.5) ? Num(-10, 100) : "a";
            def["type"] = _rng.Pick(["quantitative", "nominal"]);
        }
        else
        {
            FieldDef(def, channel);
        }

        MaybeAdd(def, "condition", 0.1, () => Condition(channel));
        MaybeAdd(def, "title", 0.1, () => _rng.Chance(0.2) ? null : Text());
        return def;
    }

    /// <summary>
    ///     Fills in a field definition.
    /// </summary>
    /// <param name="def">The definition.</param>
    /// <param name="channel">The channel.</param>
    private void FieldDef(JsonObject def, string channel)
    {
        var temporal = _rng.Chance(0.15);
        var numeric = _rng.Chance(0.6);
        def["field"] = FieldName(temporal, numeric);
        def["type"] = FieldType(temporal, numeric);
        MaybeAdd(def, "aggregate", 0.2, () => _rng.Pick(Aggregates));
        MaybeAdd(def, "bin", 0.12, BinDef);
        MaybeAdd(def, "timeUnit", temporal ? 0.5 : 0.05, () => _rng.Chance(0.15) ? new JsonObject { ["unit"] = _rng.Pick(TimeUnits), ["step"] = Num(0, 10), ["maxbins"] = Num(0, 50) } : _rng.Pick(TimeUnits));
        MaybeAdd(def, "stack", 0.1, () => _rng.Pick<JsonNode?>(["zero", "normalize", "center", null, true, false, "bogus"]));
        MaybeAdd(def, "sort", 0.15, SortDef);
        MaybeAdd(def, "scale", 0.35, () => Scale(channel));
        MaybeAdd(def, "axis", 0.3, Axis);
        MaybeAdd(def, "legend", 0.25, Legend);
        MaybeAdd(def, "format", 0.1, () => Format());
        MaybeAdd(def, "formatType", 0.03, () => _rng.Pick(["number", "time", "custom"]));
        MaybeAdd(def, "band", 0.05, () => Num(0, 1));
        MaybeAdd(def, "bandPosition", 0.05, () => Num(-1, 2));
        MaybeAdd(def, "impute", 0.04, () => new JsonObject { ["method"] = _rng.Pick(["mean", "max", "value"]), ["value"] = Num(0, 5) });
    }

    /// <summary>
    ///     Builds a bin definition.
    /// </summary>
    /// <returns>The definition.</returns>
    private JsonNode BinDef()
    {
        if (_rng.Chance(0.4))
        {
            return true;
        }

        var bin = new JsonObject();
        MaybeAdd(bin, "maxbins", 0.7, () => Num(0, 60));
        MaybeAdd(bin, "step", 0.4, () => Num(0, 20));
        MaybeAdd(bin, "extent", 0.3, () => new JsonArray(Num(-100, 0), Num(0, 200)));
        MaybeAdd(bin, "base", 0.2, () => Num(0, 12));
        MaybeAdd(bin, "nice", 0.2, () => _rng.Chance(0.5));
        MaybeAdd(bin, "minstep", 0.2, () => Num(0, 5));
        MaybeAdd(bin, "divide", 0.1, () => new JsonArray(Num(0, 5), Num(0, 5)));
        MaybeAdd(bin, "anchor", 0.1, () => Num(-10, 10));
        MaybeAdd(bin, "steps", 0.1, () => new JsonArray(Num(1, 5), Num(5, 10)));
        return bin;
    }

    /// <summary>
    ///     Builds a sort definition.
    /// </summary>
    /// <returns>The definition.</returns>
    private JsonNode? SortDef()
    {
        return _rng.Next(5) switch
        {
            0 => _rng.Pick(["ascending", "descending", "-x", "y", "bogus"]),
            1 => null,
            2 => new JsonArray("a", "b", "z", "a"),
            3 => new JsonObject { ["field"] = _rng.Pick(NumericFields), ["op"] = _rng.Pick(Aggregates), ["order"] = _rng.Pick(["ascending", "descending"]) },
            _ => new JsonObject { ["encoding"] = _rng.Pick(["x", "y", "color", "size"]), ["order"] = _rng.Pick(["ascending", "descending"]) },
        };
    }

    /// <summary>
    ///     Builds a scale definition.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The definition.</returns>
    private JsonNode? Scale(string channel)
    {
        if (_rng.Chance(0.06))
        {
            return null;
        }

        var scale = new JsonObject();
        MaybeAdd(scale, "type", 0.6, () => _rng.Pick(ScaleTypes));
        MaybeAdd(scale, "domain", 0.35, () => _rng.Next(4) switch
        {
            0 => new JsonArray(Num(-100, 0), Num(0, 100)),
            1 => new JsonArray("a", "b", "c"),
            2 => new JsonObject { ["unionWith"] = new JsonArray(Num(0, 10)) },
            _ => new JsonArray(Boundary(), Boundary()),
        });
        MaybeAdd(scale, "range", 0.3, () => _rng.Chance(0.5) ? new JsonArray(Num(0, 100), Num(100, 500)) : new JsonArray(Color(), Color(), Color()));
        MaybeAdd(scale, "zero", 0.25, () => _rng.Chance(0.5));
        MaybeAdd(scale, "nice", 0.25, () => _rng.Chance(0.5) ? _rng.Chance(0.5) : Num(0, 20));
        MaybeAdd(scale, "padding", 0.2, () => Num(0, 100));
        MaybeAdd(scale, "paddingInner", 0.2, () => Num(-1, 2));
        MaybeAdd(scale, "paddingOuter", 0.2, () => Num(-1, 2));
        MaybeAdd(scale, "exponent", 0.2, () => Num(-3, 5));
        MaybeAdd(scale, "base", 0.15, () => Num(-1, 20));
        MaybeAdd(scale, "constant", 0.15, () => Num(-1, 5));
        MaybeAdd(scale, "reverse", 0.15, () => _rng.Chance(0.5));
        MaybeAdd(scale, "clamp", 0.15, () => _rng.Chance(0.5));
        MaybeAdd(scale, "round", 0.1, () => _rng.Chance(0.5));
        MaybeAdd(scale, "scheme", channel is "color" or "fill" or "stroke" ? 0.5 : 0.1, () => _rng.Chance(0.2) ? new JsonObject { ["name"] = _rng.Pick(Schemes), ["extent"] = new JsonArray(Num(0, 1), Num(0, 1)), ["count"] = Num(0, 20) } : _rng.Pick(Schemes));
        MaybeAdd(scale, "rangeMin", 0.1, () => Num(-10, 100));
        MaybeAdd(scale, "rangeMax", 0.1, () => Num(-10, 5000));
        MaybeAdd(scale, "interpolate", 0.05, () => _rng.Pick(["rgb", "hcl", "lab", "cubehelix", "bogus"]));
        return scale;
    }

    /// <summary>
    ///     Builds an axis definition.
    /// </summary>
    /// <returns>The definition.</returns>
    private JsonNode? Axis()
    {
        if (_rng.Chance(0.12))
        {
            return null;
        }

        var axis = new JsonObject();
        MaybeAdd(axis, "title", 0.4, () => _rng.Chance(0.2) ? null : Text());
        MaybeAdd(axis, "labelAngle", 0.3, () => Num(-720, 720));
        MaybeAdd(axis, "labelLimit", 0.2, () => Num(-5, 400));
        MaybeAdd(axis, "tickCount", 0.3, () => _rng.Chance(0.2) ? new JsonObject { ["interval"] = _rng.Pick(["day", "month", "year", "millisecond", "bogus"]), ["step"] = Num(0, 10) } : Num(0, 5000));
        MaybeAdd(axis, "values", 0.15, () => _rng.Chance(0.1) ? new JsonArray(Num(0, 5), "a", null) : new JsonArray(Num(-5, 5), Num(0, 50), Boundary()));
        MaybeAdd(axis, "format", 0.2, () => Format());
        MaybeAdd(axis, "grid", 0.2, () => _rng.Chance(0.5));
        MaybeAdd(axis, "orient", 0.2, () => _rng.Pick(["top", "bottom", "left", "right", "center"]));
        MaybeAdd(axis, "offset", 0.2, () => Num(-50, 100));
        MaybeAdd(axis, "titlePadding", 0.2, () => Num(-10, 100));
        MaybeAdd(axis, "labelPadding", 0.2, () => Num(-10, 100));
        MaybeAdd(axis, "tickSize", 0.2, () => Num(-5, 50));
        MaybeAdd(axis, "labelOverlap", 0.2, () => _rng.Pick<JsonNode?>(["parity", "greedy", true, false, "bogus"]));
        MaybeAdd(axis, "labelFontSize", 0.2, () => Num(0, 80));
        MaybeAdd(axis, "labelExpr", 0.1, () => Expression(2));
        MaybeAdd(axis, "labelSeparation", 0.1, () => Num(-5, 20));
        MaybeAdd(axis, "labelBound", 0.1, () => _rng.Chance(0.5) ? _rng.Chance(0.5) : Num(0, 10));
        MaybeAdd(axis, "domain", 0.1, () => _rng.Chance(0.5));
        MaybeAdd(axis, "ticks", 0.1, () => _rng.Chance(0.5));
        MaybeAdd(axis, "gridDash", 0.1, () => new JsonArray(Num(0, 10), Num(0, 10)));
        MaybeAdd(axis, "tickMinStep", 0.1, () => Num(-1, 100));
        MaybeAdd(axis, "bandPosition", 0.05, () => Num(-1, 2));
        return axis;
    }

    /// <summary>
    ///     Builds a legend definition.
    /// </summary>
    /// <returns>The definition.</returns>
    private JsonNode? Legend()
    {
        if (_rng.Chance(0.15))
        {
            return null;
        }

        var legend = new JsonObject();
        MaybeAdd(legend, "title", 0.4, () => Text());
        MaybeAdd(legend, "orient", 0.3, () => _rng.Pick(["right", "left", "top", "bottom", "none", "top-left", "bottom-right", "bogus"]));
        MaybeAdd(legend, "columns", 0.2, () => Num(-1, 10));
        MaybeAdd(legend, "symbolSize", 0.2, () => Num(-10, 5000));
        MaybeAdd(legend, "symbolType", 0.15, () => _rng.Pick(["circle", "square", "M0,0L1,1", "bogus"]));
        MaybeAdd(legend, "labelLimit", 0.2, () => Num(-5, 400));
        MaybeAdd(legend, "gradientLength", 0.15, () => Num(-5, 3000));
        MaybeAdd(legend, "gradientThickness", 0.15, () => Num(-5, 300));
        MaybeAdd(legend, "type", 0.15, () => _rng.Pick(["symbol", "gradient", "bogus"]));
        MaybeAdd(legend, "values", 0.1, () => new JsonArray(Num(0, 5), Num(0, 50)));
        MaybeAdd(legend, "tickCount", 0.1, () => Num(0, 3000));
        MaybeAdd(legend, "direction", 0.1, () => _rng.Pick(["horizontal", "vertical"]));
        MaybeAdd(legend, "labelExpr", 0.1, () => Expression(2));
        MaybeAdd(legend, "format", 0.1, () => Format());
        MaybeAdd(legend, "symbolLimit", 0.1, () => Num(-1, 100));
        MaybeAdd(legend, "clipHeight", 0.1, () => Num(-1, 100));
        MaybeAdd(legend, "padding", 0.1, () => Num(-10, 100));
        MaybeAdd(legend, "offset", 0.1, () => Num(-10, 100));
        return legend;
    }

    /// <summary>
    ///     Builds a conditional value for a channel.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The condition.</returns>
    private JsonNode Condition(string channel)
    {
        var condition = new JsonObject();
        if (_params.Count > 0 && _rng.Chance(0.5))
        {
            condition["param"] = _rng.Pick(_params);
        }
        else
        {
            condition["test"] = _rng.Chance(0.6) ? Predicate(1) : Expression(2);
        }

        if (_rng.Chance(0.5))
        {
            condition["value"] = channel is "color" or "fill" or "stroke" ? Color() : Num(0, 10);
        }
        else
        {
            FieldDef(condition, channel);
        }

        return _rng.Chance(0.2) ? new JsonArray(condition, new JsonObject { ["test"] = "true", ["value"] = Num(0, 5) }) : condition;
    }

    /// <summary>
    ///     Builds the transforms of a view.
    /// </summary>
    /// <param name="existing">The transforms that are there already; they are kept in front.</param>
    /// <returns>The transforms.</returns>
    private JsonArray Transforms(JsonArray? existing)
    {
        var transforms = existing ?? [];
        for (var i = _rng.Next(1, 4); i > 0; i--)
        {
            transforms.Add(Transform());
        }

        return transforms;
    }

    /// <summary>
    ///     Builds one transform.
    /// </summary>
    /// <returns>The transform.</returns>
    private JsonObject Transform()
    {
        switch (_rng.Next(24))
        {
            case 0 or 1 or 2:
                return new JsonObject { ["calculate"] = Expression(3), ["as"] = _rng.Pick(["w", "x2", "n0", "label", "value"]) };
            case 3 or 4:
                return new JsonObject { ["filter"] = _rng.Chance(0.5) ? Expression(2) : Predicate(2) };
            case 5:
                return new JsonObject { ["fold"] = new JsonArray("n0", "n1", "n2"), ["as"] = new JsonArray("key", "value") };
            case 6:
                return new JsonObject { ["flatten"] = new JsonArray("list"), ["as"] = new JsonArray("w") };
            case 7:
                return new JsonObject { ["pivot"] = "c0", ["value"] = "n0", ["groupby"] = new JsonArray("c1"), ["op"] = _rng.Pick(Aggregates), ["limit"] = _rng.Chance(0.4) ? Boundary() : Num(0, 5) };
            case 8:
                return new JsonObject { ["lookup"] = "c0", ["from"] = new JsonObject { ["data"] = new JsonObject { ["values"] = Rows(_rng.Next(0, 5)) }, ["key"] = "c0", ["fields"] = new JsonArray("n1") }, ["as"] = new JsonArray("w") };
            case 9:
                return new JsonObject { ["sample"] = _rng.Chance(0.4) ? Boundary() : Num(-1, 20) };
            case 10 or 11:
                return Aggregate(false);
            case 12:
                return Aggregate(true);
            case 13:
                return Window();
            case 14:
                return Impute();
            case 15:
                return new JsonObject { ["bin"] = BinDef(), ["field"] = _rng.Pick(NumericFields), ["as"] = _rng.Chance(0.5) ? "bin_n0" : new JsonArray("b0", "b1") };
            case 16:
                return new JsonObject { ["timeUnit"] = _rng.Pick(TimeUnits), ["field"] = "t0", ["as"] = "tu" };
            case 17:
                return new JsonObject { ["stack"] = _rng.Pick(NumericFields), ["groupby"] = new JsonArray("c0"), ["offset"] = _rng.Pick(["zero", "center", "normalize"]), ["as"] = new JsonArray("s0", "s1") };
            case 18:
                return Regression();
            case 19:
                return Loess();
            case 20:
                return Density();
            case 21:
                return new JsonObject { ["quantile"] = _rng.Pick(NumericFields), ["probs"] = _rng.Chance(0.5) ? new JsonArray(Num(0, 1), Boundary()) : null, ["step"] = _rng.Chance(0.5) ? Boundary() : Num(0, 1), ["groupby"] = _rng.Chance(0.3) ? new JsonArray("c0") : null };
            case 22:
                return new JsonObject { ["joinaggregate"] = new JsonArray(new JsonObject { ["op"] = _rng.Pick(Aggregates), ["field"] = _rng.Pick(NumericFields), ["as"] = "ja" }), ["groupby"] = new JsonArray("c0") };
            default:
                return new JsonObject { ["extent"] = _rng.Pick(NumericFields), ["param"] = "ext" };
        }
    }

    /// <summary>
    ///     Builds an aggregate transform.
    /// </summary>
    /// <param name="withDeviations">Whether the operation list is bad on purpose.</param>
    /// <returns>The transform.</returns>
    private JsonObject Aggregate(bool withDeviations)
    {
        var ops = new JsonArray();
        for (var i = _rng.Next(1, 4); i > 0; i--)
        {
            var op = new JsonObject { ["op"] = _rng.Pick(Aggregates), ["as"] = "a" + i };
            if (!withDeviations || _rng.Chance(0.7))
            {
                op["field"] = _rng.Pick(NumericFields);
            }

            ops.Add(op);
        }

        var transform = new JsonObject { ["aggregate"] = ops };
        MaybeAdd(transform, "groupby", 0.7, () => new JsonArray(_rng.Pick(NominalFields)));
        return transform;
    }

    /// <summary>
    ///     Builds a window transform.
    /// </summary>
    /// <returns>The transform.</returns>
    private JsonObject Window()
    {
        var ops = new JsonArray();
        for (var i = _rng.Next(1, 3); i > 0; i--)
        {
            var op = new JsonObject { ["op"] = _rng.Pick(WindowOps), ["field"] = _rng.Pick(NumericFields), ["as"] = "wn" + i };
            MaybeAdd(op, "param", 0.4, () => _rng.Chance(0.3) ? Boundary() : Num(-2, 5));
            ops.Add(op);
        }

        var transform = new JsonObject { ["window"] = ops };
        MaybeAdd(transform, "frame", 0.5, () => new JsonArray(_rng.Chance(0.2) ? null : Num(-5, 0), _rng.Chance(0.2) ? null : Num(0, 5)));
        MaybeAdd(transform, "sort", 0.6, () => new JsonArray(new JsonObject { ["field"] = _rng.Pick(NumericFields), ["order"] = _rng.Pick(["ascending", "descending"]) }));
        MaybeAdd(transform, "groupby", 0.4, () => new JsonArray(_rng.Pick(NominalFields)));
        MaybeAdd(transform, "ignorePeers", 0.2, () => _rng.Chance(0.5));
        return transform;
    }

    /// <summary>
    ///     Builds an impute transform.
    /// </summary>
    /// <returns>The transform.</returns>
    private JsonObject Impute()
    {
        var transform = new JsonObject { ["impute"] = _rng.Pick(NumericFields), ["key"] = _rng.Pick(["n1", "c0", "n2"]) };
        MaybeAdd(transform, "method", 0.5, () => _rng.Pick(["value", "mean", "median", "max", "min", "bogus"]));
        MaybeAdd(transform, "value", 0.5, () => _rng.Chance(0.4) ? Boundary() : Num(0, 5));
        MaybeAdd(transform, "groupby", 0.4, () => new JsonArray(_rng.Pick(NominalFields)));
        MaybeAdd(transform, "frame", 0.3, () => new JsonArray(Num(-3, 0), Num(0, 3)));
        MaybeAdd(transform, "keyvals", 0.3, () => _rng.Chance(0.5) ? new JsonArray(Num(0, 5), Num(0, 5)) : new JsonObject { ["start"] = Num(-5, 5), ["stop"] = Num(5, 50), ["step"] = Num(0, 3) });
        return transform;
    }

    /// <summary>
    ///     Builds a regression transform.
    /// </summary>
    /// <returns>The transform.</returns>
    private JsonObject Regression()
    {
        var transform = new JsonObject { ["regression"] = _rng.Pick(NumericFields), ["on"] = _rng.Pick(NumericFields) };
        MaybeAdd(transform, "method", 0.7, () => _rng.Pick(["linear", "log", "exp", "pow", "quad", "poly", "bogus"]));
        MaybeAdd(transform, "order", 0.5, () => _rng.Chance(0.3) ? Boundary() : Num(0, 8));
        MaybeAdd(transform, "extent", 0.4, () => new JsonArray(_rng.Chance(0.2) ? Boundary() : Num(-10, 0), _rng.Chance(0.2) ? Boundary() : Num(0, 100)));
        MaybeAdd(transform, "params", 0.3, () => _rng.Chance(0.5));
        MaybeAdd(transform, "groupby", 0.3, () => new JsonArray(_rng.Pick(NominalFields)));
        return transform;
    }

    /// <summary>
    ///     Builds a loess transform.
    /// </summary>
    /// <returns>The transform.</returns>
    private JsonObject Loess()
    {
        var transform = new JsonObject { ["loess"] = _rng.Pick(NumericFields), ["on"] = _rng.Pick(NumericFields) };
        MaybeAdd(transform, "bandwidth", 0.7, () => _rng.Chance(0.3 * _wild) ? Boundary() : Frac());
        MaybeAdd(transform, "groupby", 0.3, () => new JsonArray(_rng.Pick(NominalFields)));
        return transform;
    }

    /// <summary>
    ///     Builds a density transform.
    /// </summary>
    /// <returns>The transform.</returns>
    private JsonObject Density()
    {
        var transform = new JsonObject { ["density"] = _rng.Pick(NumericFields) };
        MaybeAdd(transform, "extent", 0.5, () => new JsonArray(_rng.Chance(0.2) ? Boundary() : Num(-10, 0), _rng.Chance(0.2) ? Boundary() : Num(0, 100)));
        MaybeAdd(transform, "steps", 0.5, () => _rng.Chance(0.4) ? Boundary() : Num(0, 200));
        MaybeAdd(transform, "minsteps", 0.2, () => _rng.Chance(0.3) ? Boundary() : Num(0, 50));
        MaybeAdd(transform, "maxsteps", 0.2, () => _rng.Chance(0.3) ? Boundary() : Num(0, 500));
        MaybeAdd(transform, "bandwidth", 0.3, () => _rng.Chance(0.3 * _wild) ? Boundary() : Frac());
        MaybeAdd(transform, "counts", 0.3, () => _rng.Chance(0.5));
        MaybeAdd(transform, "cumulative", 0.3, () => _rng.Chance(0.5));
        MaybeAdd(transform, "groupby", 0.3, () => new JsonArray(_rng.Pick(NominalFields)));
        MaybeAdd(transform, "as", 0.2, () => new JsonArray("value", "density"));
        return transform;
    }

    /// <summary>
    ///     Builds a predicate.
    /// </summary>
    /// <param name="depth">The nesting depth that is still allowed.</param>
    /// <returns>The predicate.</returns>
    private JsonNode Predicate(int depth)
    {
        if (depth > 0 && _rng.Chance(0.25))
        {
            return new JsonObject { [_rng.Pick(["and", "or"])] = new JsonArray(Predicate(depth - 1), Predicate(depth - 1)) };
        }

        if (depth > 0 && _rng.Chance(0.1))
        {
            return new JsonObject { ["not"] = Predicate(depth - 1) };
        }

        if (_params.Count > 0 && _rng.Chance(0.15))
        {
            return new JsonObject { ["param"] = _rng.Pick(_params) };
        }

        var predicate = new JsonObject { ["field"] = _rng.Chance(0.2) ? "t0" : _rng.Pick(NumericFields) };
        switch (_rng.Next(7))
        {
            case 0:
                predicate["range"] = new JsonArray(Boundary(), Num(0, 50));
                break;
            case 1:
                predicate["oneOf"] = new JsonArray(Num(0, 10), "a", null, Boundary());
                break;
            case 2:
                predicate["equal"] = _rng.Chance(0.5) ? Num(-5, 5) : "a";
                break;
            case 3:
                predicate["lt"] = Boundary();
                break;
            case 4:
                predicate["gte"] = Num(-50, 50);
                break;
            case 5:
                predicate["valid"] = _rng.Chance(0.5);
                break;
            default:
                predicate["range"] = new JsonArray(Text(), Text());
                predicate["timeUnit"] = _rng.Pick(TimeUnits);
                break;
        }

        return predicate;
    }

    /// <summary>
    ///     Builds an expression: usually a small tree of operators and functions over fields and numbers, sometimes a hostile text.
    /// </summary>
    /// <param name="depth">The nesting depth that is still allowed.</param>
    /// <returns>The expression text.</returns>
    private string Expression(int depth)
    {
        if (_rng.Chance(0.04))
        {
            return Hostile();
        }

        if (depth <= 0 || _rng.Chance(0.25))
        {
            return Atom();
        }

        switch (_rng.Next(8))
        {
            case 0 or 1 or 2:
                return "(" + Expression(depth - 1) + " " + _rng.Pick(["+", "-", "*", "/", "%", "**", ">", "<", "==", "===", "&&", "||", "&", "|", "<<", ">>", ">>>", "^"]) + " " + Expression(depth - 1) + ")";
            case 3:
                return "(" + Expression(depth - 1) + " ? " + Expression(depth - 1) + " : " + Expression(depth - 1) + ")";
            case 4:
                return _rng.Pick(["-", "!", "+", "~"]) + Expression(depth - 1);
            case 5:
                return "format(" + Expression(depth - 1) + ", '" + _rng.Pick([",.2f", "d", "s", ".1000000f", "$,.0%", "x", "+.3e", "0>10"]) + "')";
            case 6:
                return _rng.Pick(["upper", "lower", "trim", "length", "toString", "toNumber", "toBoolean", "toDate", "year", "month", "isDate", "isValid", "time", "timeFormat"]) + "(" + Expression(depth - 1) + ")";
            default:
                {
                    var function = _rng.Pick(NumberFunctions);
                    return function + "(" + Expression(depth - 1) + ", " + Expression(depth - 1) + (function is "clamp" or "lerp" ? ", " + Expression(depth - 1) : string.Empty) + ")";
                }
        }
    }

    /// <summary>
    ///     Builds a leaf of an expression.
    /// </summary>
    /// <returns>The text.</returns>
    private string Atom()
    {
        return _rng.Next(8) switch
        {
            0 => "datum." + _rng.Pick(NumericFields),
            1 => "datum." + _rng.Pick(NumericFields),
            2 => "datum." + _rng.Pick(NominalFields),
            3 => "datum.t0",
            4 => "'" + _rng.Pick(["a", "", "2024-01-01", "x y", "%Y"]) + "'",
            5 => _params.Count > 0 ? _rng.Pick(_params) : "PI",
            6 => Boundary().ToJsonString(),
            _ => _rng.Pick(["null", "true", "false", "NaN", "Infinity", "PI", "E", "datum", "[1, 2, 3]", "{a: 1}", "datetime(2024, 0, 1)"]),
        };
    }

    /// <summary>
    ///     Builds a number format or a time format text.
    /// </summary>
    /// <returns>The text.</returns>
    private string Format() => _rng.Chance(0.4) ? Hostile() : _rng.Pick([",.2f", "d", "s", "%", "$,.0f", "%Y", "%b %d", "%H:%M:%S.%L", "%A, %B %e", "+.0e", "x", "b", ".3g", "r", "c", "05d", ">20,"]);

    /// <summary>
    ///     Builds a color.
    /// </summary>
    /// <returns>The color text.</returns>
    private string Color() => _rng.Chance(0.15 * _wild)
        ? _rng.Pick(["", "nope", "#", "#12", "#gggggg", "rgb(1,2", "hsl(400, 200%, 50%)", "rgba(0,0,0,2)", "transparent", "currentColor", "#12345678", "color(display-p3 1 0 0)", "rgb(1e999,0,0)"])
        : _rng.Pick(["red", "steelblue", "#336699", "rgb(10,20,30)", "hsl(120, 50%, 50%)", "rgba(255,0,0,0.5)", "#abc", "none", "white", "black"]);

    /// <summary>
    ///     Builds a text: mostly ordinary, sometimes hostile, rarely very long.
    /// </summary>
    /// <returns>The text.</returns>
    private string Text()
    {
        var kind = _rng.Next(100);
        if (kind < 80)
        {
            return _rng.Pick(["Title", "A", "b c", "Sales", "x", "Name", "Hello, world", "2024"]);
        }

        if (kind < 96)
        {
            return Hostile();
        }

        return new string('a', _rng.Chance(_wild) ? _rng.Pick([300, 3000]) : 20);
    }

    /// <summary>
    ///     Builds a number of the specification: mostly within an ordinary range, otherwise from the table of boundary values.
    /// </summary>
    /// <param name="min">The smallest ordinary value.</param>
    /// <param name="max">The largest ordinary value.</param>
    /// <returns>The number.</returns>
    private JsonNode Num(int min, int max)
    {
        if (_rng.Chance(0.3))
        {
            return Boundary();
        }

        // Fractions only where the specification is wild: many properties are integers
        return _rng.Chance(0.25 * _wild) ? JsonValue.Create(Math.Round(min + ((max - min) * _rng.NextDouble()), 3)) : JsonValue.Create(_rng.Next(min, max));
    }

    /// <summary>
    ///     Builds a fraction from zero to one.
    /// </summary>
    /// <returns>The number.</returns>
    private JsonNode Frac() => JsonValue.Create(Math.Round(_rng.NextDouble(), 2));

    /// <summary>
    ///     Builds the width or the height of a view: a number, a step or the container.
    /// </summary>
    /// <param name="max">The largest ordinary size.</param>
    /// <returns>The size.</returns>
    private JsonNode LayoutSize(int max)
    {
        var kind = _rng.Next(100);
        if (kind < 10)
        {
            return "container";
        }

        return kind < 25 ? new JsonObject { ["step"] = Num(5, 40) } : Num(20, max);
    }

    /// <summary>
    ///     Picks the field of a field definition.
    /// </summary>
    /// <param name="temporal">Whether the field is the date.</param>
    /// <param name="numeric">Whether the field is a number.</param>
    /// <returns>The field.</returns>
    private string FieldName(bool temporal, bool numeric)
    {
        if (_rng.Chance(0.03 * _wild))
        {
            return Text();
        }

        if (temporal)
        {
            return "t0";
        }

        return numeric ? _rng.Pick(NumericFields) : _rng.Pick(NominalFields);
    }

    /// <summary>
    ///     Picks the type of a field definition, mostly the one that fits the field.
    /// </summary>
    /// <param name="temporal">Whether the field is the date.</param>
    /// <param name="numeric">Whether the field is a number.</param>
    /// <returns>The type.</returns>
    private string FieldType(bool temporal, bool numeric)
    {
        if (_rng.Chance(0.05 * _wild))
        {
            return Text();
        }

        if (temporal)
        {
            return _rng.Pick(["temporal", "temporal", "ordinal"]);
        }

        return numeric ? _rng.Pick(["quantitative", "quantitative", "ordinal", "nominal"]) : _rng.Pick(["nominal", "ordinal", "nominal", "quantitative"]);
    }

    /// <summary>
    ///     Builds a boundary number.
    /// </summary>
    /// <returns>The number.</returns>
    private JsonNode Boundary()
    {
        // A specification that is not wild has ordinary numbers only, so that a good share of the cases reaches the layout and the painter
        if (!_rng.Chance(_wild))
        {
            return JsonValue.Create(_rng.Next(1, 20));
        }

        var value = _rng.Pick(BoundaryNumbers);
        return JsonValue.Create(_rng.Chance(0.3) ? -value : value);
    }

    /// <summary>
    ///     Picks a hostile text; a specification that is not wild gets an ordinary text instead.
    /// </summary>
    /// <returns>The text.</returns>
    private string Hostile() => _rng.Chance(_wild) ? _rng.Pick(HostileTexts) : "a";

    /// <summary>
    ///     Adds a property with a probability.
    /// </summary>
    /// <param name="target">The object.</param>
    /// <param name="name">The property.</param>
    /// <param name="probability">The probability.</param>
    /// <param name="value">The factory of the value.</param>
    private void MaybeAdd(JsonObject target, string name, double probability, Func<JsonNode?> value)
    {
        if (_rng.Chance(probability))
        {
            target[name] = value();
        }
    }
}

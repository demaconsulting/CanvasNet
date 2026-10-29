using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     Whether a <see cref="FeatureReport"/> is a warning about the text or a construct that is recognized but not
///     rendered.
/// </summary>
internal enum FeatureReportKind
{
    /// <summary>A problem with the text that does not stop rendering (delivered as a <see cref="VegaLiteDiagnostic"/>).</summary>
    Warning,

    /// <summary>A recognized Vega-Lite construct that the renderer does not implement (delivered as a <see cref="VegaLiteUnsupportedFeature"/>).</summary>
    UnsupportedFeature,
}

/// <summary>
///     One finding about a specification: a stable identifier, a human-readable description and the location of the
///     construct it is about.
/// </summary>
/// <param name="Id">The stable <c>vl-*</c> identifier (lower case, for example <c>vl-mark-arc</c>).</param>
/// <param name="Message">The description, without the identifier and the location.</param>
/// <param name="Location">The location of the construct (the value, or the key for an unknown property or a duplicate key).</param>
/// <param name="Kind">Whether the finding is a warning or an unsupported feature.</param>
internal sealed record FeatureReport(string Id, string Message, VegaLiteLocation Location, FeatureReportKind Kind);

/// <summary>
///     Lists every construct of a Vega-Lite specification that the renderer recognizes but does not implement, with
///     a stable identifier and the exact location, so that nothing is ever skipped silently.
/// </summary>
/// <remarks>
///     <para>
///         The catalog is the single place that defines the boundary of what the renderer supports. It reads the
///         <see cref="SpecTree"/> only (no data, no files, no network) and returns one report per construct, in
///         document order (sorted by line and column; reports at the same position keep the order they were found in).
///         Reports about supported constructs are never produced: a supported mark, channel, scale type or data source
///         (inline rows, a relative local CSV, TSV, DSV or JSON file, a named dataset, a sequence, <c>format.parse</c>
///         and <c>format.property</c>) is simply absent from the result. Remote data is the only kind of file source that is reported, because it is
///         never fetched; a local file that cannot be read is not a feature of the specification and fails the render instead. Strict mode of the document throws at the first
///         <see cref="FeatureReportKind.UnsupportedFeature"/>. The catalog is stateless and thread-safe.
///     </para>
///     <para>
///         <b>Stable identifiers.</b> Identifiers are lower case, <c>vl-&lt;area&gt;-&lt;detail&gt;</c>, and a name taken from the
///         specification is lower-cased without separators (for example <c>timeUnit</c> gives <c>timeunit</c>). The
///         table is part of the public contract of the package; an identifier is never renamed once released.
///     </para>
///     <list type="table">
///         <listheader><term>Identifier</term><description>Reported at, and when</description></listheader>
///         <item><term><c>vl-schema-version</c> (warning)</term><description><c>$schema</c>, when the major version is not 5 or 6 or the URL names no Vega-Lite version; the text is interpreted with the version 6 defaults.</description></item>
///         <item><term><c>vl-data-empty</c> (warning)</term><description>the <c>data</c> object, when it has an empty <c>values</c> array or no source at all.</description></item>
///         <item><term><c>vl-data-remote-url</c></term><description>the <c>data</c> object, when <c>url</c> has a scheme (<c>http</c>, <c>https</c>, <c>ftp</c>, <c>file</c>, <c>data</c>, <c>javascript</c>, a drive letter) or is an absolute path (Unix <c>/etc/hosts</c>, Windows rooted), a network path or a UNC path; never fetched, never given to a file resolver, and the dataset is empty. A relative local path is supported and not reported (a local file that cannot be read is an error of the render, not a report).</description></item>
///         <item><term><c>vl-data-parse-date-format</c></term><description>an entry of <c>format.parse</c> that names a date with a custom format (<c>date:'%d/%m/%Y'</c>, <c>utc:'...'</c>); the field is left as read.</description></item>
///         <item><term><c>vl-data-values-text</c></term><description>inline <c>values</c> (or a top-level dataset that a <c>data.name</c> refers to) given as a text instead of an array of rows.</description></item>
///         <item><term><c>vl-data-geo</c></term><description><c>sphere</c>, <c>graticule</c>, a topojson format, or inline <c>values</c> (or a referenced top-level dataset) given as an object.</description></item>
///         <item><term><c>vl-mark-&lt;type&gt;</c></term><description>the <c>mark</c>, for every mark type except bar, rect, point, circle, square, rule and tick.</description></item>
///         <item><term><c>vl-channel-&lt;name&gt;</c></term><description>an encoding channel other than x, y, color, fill, stroke, size and opacity; <c>tooltip</c>, <c>href</c>, <c>description</c> and <c>key</c> are accepted and ignored without a report; <c>longitude</c>, <c>latitude</c>, <c>longitude2</c> and <c>latitude2</c> report <c>vl-channel-geo</c>.</description></item>
///         <item><term><c>vl-type-temporal</c>, <c>vl-type-geojson</c></term><description>the <c>type</c> of a supported channel.</description></item>
///         <item><term><c>vl-scale-color-ramp</c></term><description>the <c>type</c> of a <c>color</c>, <c>fill</c> or <c>stroke</c> channel that has a <c>field</c> and is quantitative (a continuous color scale is not drawn; the default mark color is used).</description></item>
///         <item><term><c>vl-encoding-aggregate</c>, <c>-bin</c>, <c>-timeunit</c>, <c>-stack</c>, <c>-sort</c>, <c>-condition</c>, <c>-impute</c>, <c>-bandposition</c></term><description>that property of a supported channel. <c>bin</c> is reported unless it is false or null; <c>stack</c> unless it is false or null (an implicit stack of bars is reported at the measure channel); <c>sort</c> only for a channel shorthand (such as <c>"-y"</c>) or an object that is not a supported sort by field (an object with an <c>op</c> of count, sum, mean, average, median, min or max, an optional <c>field</c> and an optional <c>order</c>), not for ascending, descending, null or an array of values.</description></item>
///         <item><term><c>vl-scale-&lt;type&gt;</c></term><description><c>scale.type</c>, for every type except linear, band, point and ordinal.</description></item>
///         <item><term><c>vl-scale-property-&lt;name&gt;</c></term><description>a scale property other than type, domain, range, zero, nice, padding, reverse, paddingInner, paddingOuter, align, round and clamp; <c>scheme</c> is reported unless it names <c>tableau10</c> (compared without regard to case, as the scale resolver does) or the scale has an explicit color <c>range</c>, which overrides it.</description></item>
///         <item><term><c>vl-guide-legend</c></term><description>the <c>legend</c> (or the channel when it has none) of a color, fill, stroke, size or opacity channel with a <c>field</c>, unless <c>legend</c> or <c>scale</c> is null.</description></item>
///         <item><term><c>vl-guide-encode</c></term><description><c>axis.encode</c> and <c>legend.encode</c>.</description></item>
///         <item><term><c>vl-guide-axis-&lt;name&gt;</c></term><description>an axis property from the list of unsupported axis properties (<c>labelBound</c>, <c>tickBand</c>, <c>tickExtra</c>, <c>position</c>, <c>translate</c>, <c>bandPosition</c>, <c>titleX</c>, <c>titleY</c>).</description></item>
///         <item><term><c>vl-format-type-custom</c></term><description><c>axis.formatType</c>.</description></item>
///         <item><term><c>vl-expr-ref</c>, <c>vl-expr-label</c></term><description>an <c>{"expr": ...}</c> value that the renderer does not evaluate (anywhere in a view's title, size, padding, background, view, in a mark property other than the row-bound paint properties, or in an encoding other than the <c>value</c> of a color, fill, stroke, opacity or symbol size channel), and <c>axis.labelExpr</c>. The row-bound sites are evaluated and are reported only for what their expression cannot do.</description></item>
///         <item><term><c>vl-expr-function-&lt;name&gt;</c>, <c>vl-expr-view-state</c>, <c>vl-expr-construct-&lt;name&gt;</c></term><description>the expression text of a supported site (a calculate, a filter, a variable parameter or a row-bound expression reference): a call of a function outside the whitelist (the date, format and color functions among them), a use of view state (<c>signal</c>, <c>event</c>, <c>item</c>, <c>width</c>, <c>height</c>), or a deferred operator (bitwise, shift, <c>in</c>). The construct evaluates to null; the position is that of the expression text and the message names the offset inside it.</description></item>
///         <item><term><c>vl-transform-&lt;name&gt;</c></term><description>every entry of <c>transform</c> that the renderer does not compute: the transforms of later phases (<c>aggregate</c>, <c>bin</c>, <c>density</c>, <c>extent</c>, <c>impute</c>, <c>joinaggregate</c>, <c>loess</c>, <c>quantile</c>, <c>regression</c>, <c>stack</c>, <c>timeunit</c>, <c>window</c>), and any transform of a nested view of a composition, which is not drawn. <c>calculate</c>, <c>filter</c>, <c>fold</c>, <c>flatten</c>, <c>sample</c>, <c>pivot</c> and <c>lookup</c> of the drawn view are computed and not reported.</description></item>
///         <item><term><c>vl-transform-filter-param</c>, <c>-filter-timeunit</c>, <c>-filter-datetime</c>, <c>-pivot-op</c>, <c>-lookup-param</c></term><description>the parts of a computed transform that are not supported: a filter predicate on a selection parameter (every row passes), a <c>timeUnit</c> of a predicate (ignored), a date object in a predicate (the predicate is ignored), a pivot <c>op</c> other than sum and count (sum is used) and a lookup in the data of a parameter (not applied).</description></item>
///         <item><term><c>vl-composition-layer</c>, <c>-concat</c>, <c>-hconcat</c>, <c>-vconcat</c>, <c>-facet</c>, <c>-repeat</c></term><description>that property; the views nested in it are examined too.</description></item>
///         <item><term><c>vl-param-variable</c>, <c>vl-param-selection</c></term><description>every selection parameter (it has <c>select</c>) and the legacy top-level <c>selection</c> report <c>vl-param-selection</c>; a variable parameter of a nested view reports <c>vl-param-variable</c>. The variable parameters of the root view are computed and are not reported. <c>bind</c> is ignored.</description></item>
///         <item><term><c>vl-projection</c></term><description>the <c>projection</c> of a view.</description></item>
///         <item><term><c>vl-layout-autosize-&lt;type&gt;</c></term><description><c>autosize</c> types other than pad and none (fit, fit-x, fit-y).</description></item>
///     </list>
///     <para>
///         The warnings <c>vl-duplicate-key</c> and <c>vl-unknown-property</c> are produced by <see cref="SpecParser"/>
///         and are available in <see cref="SpecTree.Warnings"/>. The warning <c>vl-data-parse-failed</c> is not part of the
///         catalog: it needs the data, so the data stage produces it while a chart is measured or rendered (values that an
///         explicit <c>format.parse</c> conversion could not read, one warning per field).
///     </para>
/// </remarks>
internal static class FeatureCatalog
{
    /// <summary>The mark types that the renderer draws; any other mark type is reported.</summary>
    private static readonly HashSet<string> SupportedMarks = ["bar", "rect", "point", "circle", "square", "rule", "tick"];

    /// <summary>The channels that the renderer draws; any other channel is reported unless it is ignored.</summary>
    private static readonly HashSet<string> SupportedChannels = ["x", "y", "color", "fill", "stroke", "size", "opacity"];

    /// <summary>The channels that are accepted and ignored without a report (interaction, accessibility and linking).</summary>
    private static readonly HashSet<string> IgnoredChannels = ["tooltip", "href", "description", "key"];

    /// <summary>The geographic position channels, which all report <c>vl-channel-geo</c>.</summary>
    private static readonly HashSet<string> GeoChannels = ["longitude", "latitude", "longitude2", "latitude2"];

    /// <summary>The channels that get a legend when they are driven by a field.</summary>
    private static readonly HashSet<string> LegendChannels = ["color", "fill", "stroke", "size", "opacity"];

    /// <summary>The channels whose field values group bars into a stack.</summary>
    private static readonly HashSet<string> StackByChannels =
    [
        "color", "fill", "stroke", "size", "opacity", "fillOpacity", "strokeOpacity", "strokeWidth", "strokeDash", "shape", "angle", "detail", "text",
    ];

    /// <summary>The scale types that the renderer implements.</summary>
    private static readonly HashSet<string> SupportedScaleTypes = ["linear", "band", "point", "ordinal"];

    /// <summary>The scale properties that the renderer implements; <c>scheme</c> only for <c>tableau10</c> (see the scale visit).</summary>
    private static readonly HashSet<string> SupportedScaleProperties =
    [
        "type", "domain", "range", "zero", "nice", "padding", "reverse", "paddingInner", "paddingOuter", "align", "round", "clamp", "scheme",
    ];

    /// <summary>The axis properties that the renderer does not implement; each reports <c>vl-guide-axis-&lt;name&gt;</c>.</summary>
    private static readonly HashSet<string> UnsupportedAxisProperties =
    [
        "labelBound", "tickBand", "tickExtra", "position", "translate", "bandPosition", "titleX", "titleY",
    ];

    /// <summary>The properties of a view in which an <c>{"expr": ...}</c> value is looked for.</summary>
    private static readonly HashSet<string> ExpressionScopes = ["title", "width", "height", "padding", "background", "view", "mark", "encoding"];

    /// <summary>The composition properties, each reported as <c>vl-composition-&lt;name&gt;</c>.</summary>
    private static readonly HashSet<string> CompositionProperties = ["layer", "concat", "hconcat", "vconcat", "facet", "repeat"];

    /// <summary>
    ///     Lists the recognized-but-unsupported constructs of a specification.
    /// </summary>
    /// <param name="tree">The parsed specification. Must not be null.</param>
    /// <returns>The reports in document order; empty when the specification uses only supported constructs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> is null.</exception>
    public static IReadOnlyList<FeatureReport> Visit(SpecTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var walker = new Walker(tree);
        walker.Run();

        // LINQ's ordering is stable, so reports at one position keep the order in which they were found
        return walker.Reports.OrderBy(r => r.Location.Line).ThenBy(r => r.Location.Column).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Determines whether a data URL names something other than a relative local path: it has a scheme
    ///     (<c>http</c>, <c>file</c>, <c>data</c>, a drive letter, and so on) or is a network or absolute path.
    /// </summary>
    /// <remarks>
    ///     Such a URL is never handed to a file resolver: a chart renderer that fetches resources named by an
    ///     untrusted specification would be a server-side request forgery primitive, so remote data is reported and
    ///     the dataset is left empty instead. The decision does not depend on the host operating system: a Unix
    ///     absolute path (<c>/etc/hosts</c>), a path rooted at a backslash, a drive path (<c>C:\data</c>, <c>C:data</c>),
    ///     a UNC or network path (<c>\\server\share</c>, <c>//server/share</c>) and a <c>file:</c> URI are all
    ///     classified alike on every platform.
    /// </remarks>
    /// <param name="url">The URL as written.</param>
    /// <returns><see langword="true"/> when the URL is not a plain relative path.</returns>
    public static bool IsSchemeBearingUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var trimmed = url.AsSpan().TrimStart();

        // Absolute and network paths of either operating system family: a leading '/' (a Unix absolute path or a network
        // path) or '\' (a Windows rooted or UNC path), whichever the host is; Path.IsPathRooted adds what the host knows
        if (trimmed.StartsWith("/", StringComparison.Ordinal) || trimmed.StartsWith("\\", StringComparison.Ordinal) || Path.IsPathRooted(trimmed))
        {
            return true;
        }

        // A scheme is a letter followed by letters, digits, '+', '-' or '.', then a colon
        if (trimmed.IsEmpty || !char.IsAsciiLetter(trimmed[0]))
        {
            return false;
        }

        for (var i = 1; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == ':')
            {
                return true;
            }

            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     The state of one visit: the tree and the reports found so far.
    /// </summary>
    private sealed class Walker
    {
        /// <summary>The specification.</summary>
        private readonly SpecTree _tree;

        /// <summary>
        ///     Initializes a walker.
        /// </summary>
        /// <param name="tree">The specification.</param>
        public Walker(SpecTree tree) => _tree = tree;

        /// <summary>Gets the reports found so far, in the order they were found.</summary>
        public List<FeatureReport> Reports { get; } = [];

        /// <summary>
        ///     Builds an identifier from an area and a name taken from the specification.
        /// </summary>
        /// <param name="area">The area, such as <c>mark</c>.</param>
        /// <param name="name">The name; it is lower-cased with the invariant culture.</param>
        /// <returns>The identifier, such as <c>vl-mark-arc</c>.</returns>
        private static string Id(string area, string name) => $"vl-{area}-{name.ToLowerInvariant()}";

        /// <summary>
        ///     Visits the schema and every view.
        /// </summary>
        public void Run()
        {
            VisitSchema(_tree.Root["$schema"]);
            foreach (var view in _tree.EnumerateViews())
            {
                VisitView(view);
            }
        }

        /// <summary>
        ///     Reports a schema version that is not 5 or 6.
        /// </summary>
        /// <param name="schema">The <c>$schema</c> value, or null.</param>
        private void VisitSchema(SpecNode? schema)
        {
            if (schema is not { IsString: true })
            {
                return;
            }

            var version = SpecParser.ParseSchemaMajorVersion(schema.StringValue!);
            if (version is 5 or 6)
            {
                return;
            }

            var found = version is null
                ? "does not name a Vega-Lite version"
                : string.Create(CultureInfo.InvariantCulture, $"names Vega-Lite version {version}");
            Warn("vl-schema-version", $"The $schema {found}; the specification is interpreted with the Vega-Lite 6 defaults.", schema);
        }

        /// <summary>
        ///     Visits the properties of one view.
        /// </summary>
        /// <param name="view">The view object.</param>
        private void VisitView(SpecNode view)
        {
            foreach (var property in view.Properties)
            {
                var name = property.Name!;
                switch (name)
                {
                    case "data":
                        VisitData(property);
                        break;
                    case "mark":
                        VisitMark(property);
                        break;
                    case "encoding":
                        VisitEncoding(view, property);
                        break;
                    case "transform":
                        VisitTransforms(property, SpecParser.IsDrawnView(_tree.Root, view));
                        break;
                    case "params":
                        VisitParams(property, ReferenceEquals(view, _tree.Root));
                        break;
                    case "selection":
                        Unsupported("vl-param-selection", "Selections are not supported yet; the static default state is not applied.", property);
                        break;
                    case "projection":
                        Unsupported("vl-projection", "Map projections are not supported.", property);
                        break;
                    case "autosize":
                        VisitAutosize(property);
                        break;
                    default:
                        if (CompositionProperties.Contains(name))
                        {
                            Unsupported(Id("composition", name), $"The '{name}' composition is not supported yet.", property);
                        }

                        break;
                }

                if (ExpressionScopes.Contains(name))
                {
                    VisitExpressions(property);
                }
            }
        }

        /// <summary>
        ///     Visits a <c>data</c> definition.
        /// </summary>
        /// <param name="data">The value.</param>
        private void VisitData(SpecNode data)
        {
            if (!data.IsObject)
            {
                return;
            }

            // The source is chosen by precedence, not by the order of the keys: inline values first, then a url, a
            // sequence generator, a name, and the geographic generators. Local files, sequences and named datasets are
            // supported and not reported
            var values = data["values"];
            var url = data["url"];
            var sequence = data["sequence"];
            var name = data["name"];
            var geo = data["sphere"] ?? data["graticule"];
            if (values is not null)
            {
                VisitInlineValues(data, values);
            }
            else if (url is not null)
            {
                VisitUrl(data, url);
            }
            else if (sequence is null && name is not null)
            {
                VisitNamedData(data, name);
            }
            else if (sequence is null && geo is not null)
            {
                Unsupported("vl-data-geo", "Geographic data generators (sphere, graticule) are not supported.", geo);
            }
            else if (sequence is null)
            {
                Warn("vl-data-empty", "The data definition has no source (values, url, name or sequence); the chart is empty.", data);
            }

            if (data["format"] is { IsObject: true } format)
            {
                VisitFormat(format);
            }
        }

        /// <summary>
        ///     Visits a named data set: a top-level dataset that is not an array of rows is not read.
        /// </summary>
        /// <param name="data">The data object.</param>
        /// <param name="name">The <c>name</c> value.</param>
        private void VisitNamedData(SpecNode data, SpecNode name)
        {
            if (!name.IsString || _tree.Root["datasets"]?[name.StringValue!] is not { } dataset)
            {
                return;
            }

            switch (dataset.Kind)
            {
                case SpecNodeKind.String:
                    Unsupported("vl-data-values-text", "A dataset given as text is not read; write it as an array of objects.", data);
                    break;
                case SpecNodeKind.Object:
                    Unsupported("vl-data-geo", "A dataset given as an object (GeoJSON or TopoJSON) is not supported.", data);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        ///     Visits the inline <c>values</c> of a data definition.
        /// </summary>
        /// <param name="data">The data object.</param>
        /// <param name="values">The <c>values</c> value.</param>
        private void VisitInlineValues(SpecNode data, SpecNode values)
        {
            switch (values.Kind)
            {
                case SpecNodeKind.Array when values.Items.Count == 0:
                    Warn("vl-data-empty", "The inline data has no rows; the chart is empty.", data);
                    break;
                case SpecNodeKind.String:
                    Unsupported("vl-data-values-text", "Inline data given as text is not read; write it as an array of objects, or put it in a file next to the specification.", values);
                    break;
                case SpecNodeKind.Object:
                    Unsupported("vl-data-geo", "Inline data given as an object (GeoJSON or TopoJSON) is not supported.", values);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        ///     Visits the <c>url</c> of a data definition.
        /// </summary>
        /// <param name="data">The data object.</param>
        /// <param name="url">The <c>url</c> value.</param>
        private void VisitUrl(SpecNode data, SpecNode url)
        {
            // A relative local path is supported (a file that cannot be read fails the render); only remote data is reported
            if (url.IsString && IsSchemeBearingUrl(url.StringValue!))
            {
                Unsupported(
                    "vl-data-remote-url",
                    "Remote data is never fetched; download the file next to the specification and refer to it by a relative path. The dataset is treated as empty.",
                    data);
            }
        }

        /// <summary>
        ///     Visits the <c>format</c> of a data definition.
        /// </summary>
        /// <param name="format">The format object.</param>
        private void VisitFormat(SpecNode format)
        {
            // Custom date formats are the one part of format.parse that is recognized and not applied
            if (format["parse"] is { IsObject: true } parse)
            {
                foreach (var entry in parse.Properties)
                {
                    if (entry.IsString && ParseDirectives.TryClassify(entry.StringValue!, out var parseType) && parseType == ParseType.CustomDate)
                    {
                        Unsupported(
                            "vl-data-parse-date-format",
                            $"The date format {entry.StringValue} of the field '{entry.Name}' is not supported; the field is left as read (use ISO 8601 dates and 'date').",
                            entry);
                    }
                }
            }

            if (format["type"] is { IsString: true } type && type.StringValue == "topojson")
            {
                Unsupported("vl-data-geo", "The topojson data format is not supported.", type);
            }
        }

        /// <summary>
        ///     Visits a <c>mark</c>.
        /// </summary>
        /// <param name="mark">The value (a mark type or a mark definition).</param>
        private void VisitMark(SpecNode mark)
        {
            var type = mark.IsString ? mark.StringValue : mark["type"]?.StringValue;
            if (type is not null && !SupportedMarks.Contains(type))
            {
                Unsupported(Id("mark", type), $"The mark type '{type}' is not rendered yet.", mark);
            }
        }

        /// <summary>
        ///     Visits an <c>encoding</c>.
        /// </summary>
        /// <param name="view">The view that owns the encoding (its mark decides about implicit stacking).</param>
        /// <param name="encoding">The encoding object.</param>
        private void VisitEncoding(SpecNode view, SpecNode encoding)
        {
            if (!encoding.IsObject)
            {
                return;
            }

            foreach (var channel in encoding.Properties)
            {
                var name = channel.Name!;
                if (IgnoredChannels.Contains(name))
                {
                    continue;
                }

                if (GeoChannels.Contains(name))
                {
                    Unsupported("vl-channel-geo", $"The geographic channel '{name}' is not supported.", channel);
                }
                else if (!SupportedChannels.Contains(name))
                {
                    // Channels that Vega-Lite defines but the renderer does not draw; unknown names were already warned about
                    if (SpecParser.IsChannelName(name))
                    {
                        Unsupported(Id("channel", name), $"The '{name}' channel is not supported yet.", channel);
                    }
                }
                else if (channel.IsObject)
                {
                    VisitChannelDefinition(name, channel);
                }
            }

            VisitImplicitStack(view, encoding);
        }

        /// <summary>
        ///     Visits the properties of the definition of a supported channel.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitChannelDefinition(string name, SpecNode definition)
        {
            if (definition["type"] is { IsString: true } type)
            {
                switch (type.StringValue)
                {
                    case "temporal":
                        Unsupported("vl-type-temporal", "Temporal fields are not supported yet.", type);
                        break;
                    case "geojson":
                        Unsupported("vl-type-geojson", "GeoJSON fields are not supported.", type);
                        break;
                    case "quantitative" when name is "color" or "fill" or "stroke" && definition["field"] is not null:
                        Unsupported("vl-scale-color-ramp", "Continuous color scales are not supported yet; the default mark color is used.", type);
                        break;
                    default:
                        break;
                }
            }

            ReportProperty(definition, "aggregate", n => !n.IsNull, "Aggregation is not supported yet.");
            ReportProperty(definition, "bin", IsEnabled, "Binning is not supported yet.");
            ReportProperty(definition, "timeUnit", n => !n.IsNull, "Time units are not supported yet.");
            ReportProperty(definition, "stack", IsEnabled, "Stacking is not supported yet.");
            ReportProperty(definition, "sort", IsSortByField, "Sorting by another field or channel is not supported yet.");
            ReportProperty(definition, "condition", _ => true, "Conditional encodings are not supported yet.");
            ReportProperty(definition, "impute", n => !n.IsNull, "Imputation is not supported yet.");
            ReportProperty(definition, "bandPosition", _ => true, "The band position of a field is not supported yet.");

            var scale = definition["scale"];
            if (scale is { IsObject: true })
            {
                VisitScale(scale);
            }

            if (definition["axis"] is { IsObject: true } axis)
            {
                VisitAxis(axis);
            }

            VisitLegend(name, definition, scale);
        }

        /// <summary>
        ///     Reports a property of a channel definition when a condition on its value holds.
        /// </summary>
        /// <param name="definition">The channel definition object.</param>
        /// <param name="property">The property name; the identifier is <c>vl-encoding-&lt;name&gt;</c>.</param>
        /// <param name="isUsed">Decides whether the value is a use of the unsupported feature.</param>
        /// <param name="message">The description.</param>
        private void ReportProperty(SpecNode definition, string property, Func<SpecNode, bool> isUsed, string message)
        {
            if (definition[property] is { } node && isUsed(node))
            {
                Unsupported(Id("encoding", property), message, node);
            }
        }

        /// <summary>
        ///     Determines whether a feature switch is on: anything except null and false (an object or a string
        ///     such as <c>"binned"</c> or <c>"zero"</c> switches the feature on).
        /// </summary>
        /// <param name="node">The value.</param>
        /// <returns><see langword="true"/> when the value turns the feature on.</returns>
        private static bool IsEnabled(SpecNode node) => !node.IsNull && !(node.IsBoolean && !node.BoolValue);

        /// <summary>
        ///     Determines whether a <c>sort</c> value sorts by another field or channel.
        /// </summary>
        /// <param name="sort">The value.</param>
        /// <returns><see langword="true"/> for an object or a channel shorthand; false for ascending, descending, null and an array of values.</returns>
        private static bool IsSortByField(SpecNode sort) => sort.Kind switch
        {
            SpecNodeKind.Object => !ScaleResolver.IsSupportedFieldSort(sort),
            SpecNodeKind.String => sort.StringValue is not ("ascending" or "descending"),
            _ => false,
        };

        /// <summary>
        ///     Visits a scale definition.
        /// </summary>
        /// <param name="scale">The scale object.</param>
        private void VisitScale(SpecNode scale)
        {
            foreach (var property in scale.Properties)
            {
                var name = property.Name!;
                if (name == "type")
                {
                    if (property.IsString && !SupportedScaleTypes.Contains(property.StringValue!))
                    {
                        Unsupported(Id("scale", property.StringValue!), $"The '{property.StringValue}' scale type is not supported yet.", property);
                    }
                }
                else if (name == "scheme")
                {
                    // Only the default categorical palette is available (the lookup the scale resolver uses, so the two agree on
                    // the name's case); any other scheme falls back to it. An explicit color range overrides a scheme, so a scheme
                    // next to one is never applied and is not reported
                    var scheme = property.IsString ? property.StringValue : property["name"]?.AsString();
                    var hasRange = scale["range"] is { IsArray: true, Items.Count: > 0 } range && range.Items.All(i => i.IsString);
                    if (!hasRange && !ColorSchemes.TryGetScheme(scheme, out _))
                    {
                        Unsupported(Id("scale-property", name), $"The color scheme '{scheme}' is not available; tableau10 is used instead.", property);
                    }
                }
                else if (!SupportedScaleProperties.Contains(name))
                {
                    Unsupported(Id("scale-property", name), $"The scale property '{name}' is not supported yet.", property);
                }
            }
        }

        /// <summary>
        ///     Visits an axis definition.
        /// </summary>
        /// <param name="axis">The axis object.</param>
        private void VisitAxis(SpecNode axis)
        {
            foreach (var property in axis.Properties)
            {
                var name = property.Name!;
                switch (name)
                {
                    case "encode":
                        Unsupported("vl-guide-encode", "Custom guide encodings (axis.encode) are not supported.", property);
                        break;
                    case "labelExpr":
                        Unsupported("vl-expr-label", "Label expressions (axis.labelExpr) are not supported yet.", property);
                        break;
                    case "formatType":
                        Unsupported("vl-format-type-custom", "Custom format types (axis.formatType) are not supported.", property);
                        break;
                    default:
                        if (UnsupportedAxisProperties.Contains(name))
                        {
                            Unsupported(Id("guide-axis", name), $"The axis property '{name}' is not supported yet.", property);
                        }

                        break;
                }
            }
        }

        /// <summary>
        ///     Reports the legend that Vega-Lite would draw for a channel driven by a field.
        /// </summary>
        /// <param name="channel">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        /// <param name="scale">The <c>scale</c> value of the channel, or null.</param>
        private void VisitLegend(string channel, SpecNode definition, SpecNode? scale)
        {
            var legend = definition["legend"];
            if (!LegendChannels.Contains(channel) || definition["field"] is null || legend is { IsNull: true } || scale is { IsNull: true })
            {
                return;
            }

            Unsupported("vl-guide-legend", "Legends are not supported yet.", legend ?? definition);
            if (legend is { IsObject: true } && legend["encode"] is { } encode)
            {
                Unsupported("vl-guide-encode", "Custom guide encodings (legend.encode) are not supported.", encode);
            }
        }

        /// <summary>
        ///     Reports bars that Vega-Lite would stack by default.
        /// </summary>
        /// <remarks>
        ///     Bars are stacked implicitly when a quantitative measure sits on one position channel, the other position
        ///     channel is discrete (or absent) and another channel groups the rows by a different field. The check is
        ///     deliberately conservative: it reports the common case and never a chart that Vega-Lite would draw unstacked
        ///     (an explicit <c>stack</c> of false or null, a range with <c>x2</c> or <c>y2</c>, an aggregate, a binned or
        ///     time-unit measure).
        /// </remarks>
        /// <param name="view">The view that owns the encoding.</param>
        /// <param name="encoding">The encoding object.</param>
        private void VisitImplicitStack(SpecNode view, SpecNode encoding)
        {
            var mark = view["mark"];
            string? type = null;
            if (mark is not null)
            {
                type = mark.IsString ? mark.StringValue : mark["type"]?.StringValue;
            }

            if (type != "bar" || encoding["x2"] is not null || encoding["y2"] is not null)
            {
                return;
            }

            var x = encoding["x"];
            var y = encoding["y"];
            SpecNode? measure = null;
            SpecNode? dimension = null;
            if (IsPlainMeasure(x) && (y is null || IsDiscrete(y)))
            {
                (measure, dimension) = (x, y);
            }
            else if (IsPlainMeasure(y) && (x is null || IsDiscrete(x)))
            {
                (measure, dimension) = (y, x);
            }

            if (measure is null)
            {
                return;
            }

            var dimensionField = dimension?["field"]?.StringValue;
            var groups = StackByChannels
                .Select(name => encoding[name])
                .Where(c => c is not null)
                .SelectMany(c => c!.IsArray ? c.Items : [c])
                .Any(def => def.IsObject && def["aggregate"] is null && def["field"]?.StringValue is { } field && field != dimensionField);
            if (groups)
            {
                Unsupported(
                    "vl-encoding-stack",
                    "Bars that share a position are stacked by default in Vega-Lite; stacking is not supported yet (set \"stack\": null on the measure to draw them unstacked).",
                    measure);
            }
        }

        /// <summary>
        ///     Determines whether a position channel is a plain quantitative field that Vega-Lite may stack.
        /// </summary>
        /// <param name="channel">The channel definition, or null.</param>
        /// <returns><see langword="true"/> for a quantitative field without aggregate, bin, time unit or explicit stack.</returns>
        private static bool IsPlainMeasure(SpecNode? channel) =>
            channel is { IsObject: true }
            && channel["field"] is { IsString: true }
            && channel["type"]?.StringValue == "quantitative"
            && channel["aggregate"] is null
            && !(channel["bin"] is { } bin && IsEnabled(bin))
            && channel["timeUnit"] is null
            && channel["stack"] is null;

        /// <summary>
        ///     Determines whether a position channel is a discrete field.
        /// </summary>
        /// <param name="channel">The channel definition.</param>
        /// <returns><see langword="true"/> for a nominal or ordinal field.</returns>
        private static bool IsDiscrete(SpecNode channel) =>
            channel.IsObject && channel["type"]?.StringValue is "nominal" or "ordinal";

        /// <summary>
        ///     Visits the entries of <c>transform</c>.
        /// </summary>
        /// <remarks>
        ///     The transforms the renderer computes (calculate, filter, fold, flatten, sample, pivot, lookup) are reported only
        ///     for the parts they do not support: expression constructs, filter predicates on time units, selection
        ///     parameters or date objects, a pivot operation other than sum and count, and a lookup in the data of a parameter.
        ///     A transform of a view that is not drawn (a nested view of a composition) is never applied and is reported as
        ///     <c>vl-transform-&lt;name&gt;</c>, like every transform of a later phase.
        /// </remarks>
        /// <param name="transforms">The array.</param>
        /// <param name="drawn">Whether the view that owns the transforms is the view the renderer draws.</param>
        private void VisitTransforms(SpecNode transforms, bool drawn)
        {
            foreach (var entry in transforms.Items)
            {
                var operation = SpecParser.FindTransformOperation(entry);
                if (operation is null)
                {
                    continue;
                }

                var name = operation.Name!;
                if (!drawn || !SpecParser.IsSupportedTransform(name))
                {
                    Unsupported(Id("transform", name), $"The '{name}' transform is not supported yet.", entry);
                    continue;
                }

                switch (name)
                {
                    case "calculate":
                        ReportConstructs(operation);
                        break;
                    case "filter":
                        VisitPredicate(operation);
                        break;
                    case "pivot":
                        if (entry["op"] is { IsString: true } op && op.StringValue is not ("sum" or "count"))
                        {
                            Unsupported("vl-transform-pivot-op", $"The pivot operation '{op.StringValue}' is not supported yet; sum is used.", op);
                        }

                        break;
                    case "lookup":
                        VisitLookupSource(entry["from"]!);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        ///     Visits the source of a <c>lookup</c> transform.
        /// </summary>
        /// <param name="from">The <c>from</c> object.</param>
        private void VisitLookupSource(SpecNode from)
        {
            if (from["param"] is { } parameter)
            {
                Unsupported("vl-transform-lookup-param", "A lookup in the data of a parameter is not supported yet; the transform is not applied.", parameter);
            }
            else if (from["data"] is { } data)
            {
                VisitData(data);
            }
        }

        /// <summary>
        ///     Visits a filter: an expression text or a predicate, including the predicates of a composition.
        /// </summary>
        /// <remarks>
        ///     The walk uses an explicit stack, so the nesting of a composition cannot overflow the call stack.
        /// </remarks>
        /// <param name="filter">The <c>filter</c> value.</param>
        private void VisitPredicate(SpecNode filter)
        {
            var pending = new Stack<SpecNode>();
            pending.Push(filter);
            while (pending.Count > 0)
            {
                var predicate = pending.Pop();
                if (predicate.IsString)
                {
                    ReportConstructs(predicate);
                    continue;
                }

                if (!predicate.IsObject)
                {
                    continue;
                }

                var composed = predicate["and"] ?? predicate["or"];
                var named = predicate["param"] ?? predicate["selection"];
                if (composed is { IsArray: true })
                {
                    for (var i = composed.Items.Count - 1; i >= 0; i--)
                    {
                        pending.Push(composed.Items[i]);
                    }
                }
                else if (predicate["not"] is { } negated)
                {
                    pending.Push(negated);
                }
                else if (named is not null)
                {
                    VisitParameterPredicate(named);
                }
                else
                {
                    VisitFieldPredicate(predicate);
                }
            }
        }

        /// <summary>
        ///     Visits a predicate that names a parameter: only a variable parameter can be evaluated.
        /// </summary>
        /// <param name="reference">The parameter name value.</param>
        private void VisitParameterPredicate(SpecNode reference)
        {
            var declaration = reference.IsString ? SpecParser.FindParameter(reference, reference.StringValue!) : null;
            if (reference.Name == "selection" || declaration is null || declaration["select"] is not null)
            {
                Unsupported("vl-transform-filter-param", "A filter on a selection is not supported yet; every row passes.", reference);
            }
        }

        /// <summary>
        ///     Visits a field predicate: time units and date objects are not supported.
        /// </summary>
        /// <param name="predicate">The predicate object.</param>
        private void VisitFieldPredicate(SpecNode predicate)
        {
            if (predicate["timeUnit"] is { } timeUnit && !timeUnit.IsNull)
            {
                Unsupported("vl-transform-filter-timeunit", "A time unit in a filter predicate is not supported yet; it is ignored.", timeUnit);
            }

            foreach (var comparison in new[] { "equal", "lt", "lte", "gt", "gte", "range", "oneOf" }.Select(name => predicate[name]).OfType<SpecNode>())
            {
                var date = comparison.IsObject ? comparison : comparison.Items.FirstOrDefault(i => i.IsObject);
                if (date is not null)
                {
                    Unsupported("vl-transform-filter-datetime", "A date object in a filter predicate is not supported yet; the predicate is ignored.", date);
                }
            }
        }

        /// <summary>
        ///     Reports the constructs of an expression that the engine parses but does not execute.
        /// </summary>
        /// <param name="text">The string node that holds the expression text.</param>
        private void ReportConstructs(SpecNode text)
        {
            if (_tree.Expressions.Get(text) is not { } expression)
            {
                return;
            }

            var findings = new List<(int Offset, string Id, string Message)>();
            foreach (var construct in expression.UnsupportedConstructs)
            {
                findings.Add((construct.Offset, construct.Identifier, DescribeConstruct(construct)));
            }

            foreach (var reference in expression.ParameterReferences.Where(r => SpecParser.IsViewSignal(r.Name) && !SpecParser.IsParameterDeclared(text, r.Name)))
            {
                findings.Add((
                    reference.Offset,
                    "vl-expr-view-state",
                    $"The view signal '{reference.Name}' is not available in expressions (at expression offset {reference.Offset}); it evaluates to null."));
            }

            foreach (var (_, id, message) in findings.OrderBy(f => f.Offset))
            {
                Unsupported(id, message, text);
            }
        }

        /// <summary>
        ///     Describes an unsupported expression construct.
        /// </summary>
        /// <param name="construct">The construct.</param>
        /// <returns>The description, with the offset in the expression text.</returns>
        private static string DescribeConstruct(ExpressionConstruct construct) => construct.Kind switch
        {
            ExpressionConstructKind.Function =>
                $"The expression function '{construct.Name}' is not supported yet (at expression offset {construct.Offset}); it evaluates to null.",
            ExpressionConstructKind.ViewState =>
                $"View state ('{construct.Name}') is not available in expressions (at expression offset {construct.Offset}); it evaluates to null.",
            _ => $"The expression construct '{construct.Name}' is not supported yet (at expression offset {construct.Offset}); it evaluates to null.",
        };

        /// <summary>
        ///     Visits the entries of <c>params</c>.
        /// </summary>
        /// <param name="parameters">The array.</param>
        /// <param name="isRoot">Whether the view that owns the parameters is the root of the specification.</param>
        private void VisitParams(SpecNode parameters, bool isRoot)
        {
            foreach (var parameter in parameters.Items)
            {
                if (parameter["select"] is not null)
                {
                    Unsupported("vl-param-selection", "Selection parameters are not supported yet.", parameter);
                }
                else if (!isRoot)
                {
                    Unsupported("vl-param-variable", "Variable parameters of a nested view are not supported yet.", parameter);
                }
                else if (parameter["expr"] is { IsString: true } expression)
                {
                    ReportConstructs(expression);
                }
            }
        }

        /// <summary>
        ///     Visits an <c>autosize</c> value.
        /// </summary>
        /// <param name="autosize">The value (a type name or an object with a <c>type</c>).</param>
        private void VisitAutosize(SpecNode autosize)
        {
            var type = autosize.IsObject ? autosize["type"] : autosize;
            if (type is { IsString: true } && type.StringValue is not ("pad" or "none"))
            {
                Unsupported(Id("layout-autosize", type.StringValue!), $"The autosize type '{type.StringValue}' is not supported yet.", type);
            }
        }

        /// <summary>
        ///     Looks for <c>{"expr": ...}</c> values inside a property of a view and reports each one.
        /// </summary>
        /// <param name="scope">The property value to search.</param>
        private void VisitExpressions(SpecNode scope)
        {
            // An explicit stack keeps the walk safe for the deepest accepted nesting; an expression reference is a
            // leaf, so its contents are not searched
            var pending = new Stack<SpecNode>();
            pending.Push(scope);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node.IsObject && node["expr"] is { IsString: true } text)
                {
                    // An expression of a supported site (compiled by the parser) is reported only for what it cannot do;
                    // anywhere else the reference is not evaluated
                    if (_tree.Expressions.Get(text) is not null)
                    {
                        ReportConstructs(text);
                    }
                    else
                    {
                        Unsupported("vl-expr-ref", "Expression references are not supported here yet.", node);
                    }

                    continue;
                }

                if (node.IsObject || node.IsArray)
                {
                    var children = node.IsObject ? node.Properties : node.Items;
                    for (var i = children.Count - 1; i >= 0; i--)
                    {
                        pending.Push(children[i]);
                    }
                }
            }
        }

        /// <summary>
        ///     Records a warning.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="message">The description.</param>
        /// <param name="node">The node the finding is about.</param>
        private void Warn(string id, string message, SpecNode node) =>
            Reports.Add(new FeatureReport(id, message, _tree.Locate(node), FeatureReportKind.Warning));

        /// <summary>
        ///     Records an unsupported construct.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="message">The description.</param>
        /// <param name="node">The node the finding is about.</param>
        private void Unsupported(string id, string message, SpecNode node) =>
            Reports.Add(new FeatureReport(id, message, _tree.Locate(node), FeatureReportKind.UnsupportedFeature));
    }
}

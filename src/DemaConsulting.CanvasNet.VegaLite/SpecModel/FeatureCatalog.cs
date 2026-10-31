using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

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
///         <item><term><c>vl-data-values-text</c></term><description>inline <c>values</c> (or a top-level dataset that a <c>data.name</c> refers to) given as a text instead of an array of rows.</description></item>
///         <item><term><c>vl-data-geo</c></term><description><c>sphere</c>, <c>graticule</c>, a topojson format, or inline <c>values</c> (or a referenced top-level dataset) given as an object.</description></item>
///         <item><term><c>vl-mark-&lt;type&gt;</c></term><description>the <c>mark</c>, for every mark type except bar, rect, point, circle, square, rule and tick.</description></item>
///         <item><term><c>vl-channel-&lt;name&gt;</c></term><description>an encoding channel other than x, y, color, fill, stroke, size, opacity, shape, strokeWidth, strokeDash, fillOpacity, strokeOpacity and angle; <c>shape</c> is drawn by a point, <c>angle</c> by a point, circle or square and <c>strokeDash</c> by every mark but a rect, and the channel is reported (and ignored) for any other mark; <c>tooltip</c>, <c>href</c>, <c>description</c> and <c>key</c> are accepted and ignored without a report; <c>longitude</c>, <c>latitude</c>, <c>longitude2</c> and <c>latitude2</c> report <c>vl-channel-geo</c>. <c>x2</c> and <c>y2</c> are accepted (the range of a bar, rect or rule) when the mark is one of those and the channel they extend (<c>x</c> or <c>y</c>) is present, and report <c>vl-channel-x2</c> / <c>vl-channel-y2</c> otherwise. <c>order</c> is accepted when it orders a stack (see <see cref="StackRules"/>) and reports <c>vl-channel-order</c> otherwise, because the drawing order of marks is not rendered yet.</description></item>
///         <item><term><c>vl-type-geojson</c></term><description>the <c>type</c> of a supported channel.</description></item>
///         <item><term><c>vl-shape-path</c></term><description>a shape that is not a symbol name: the <c>shape</c> of a mark, the <c>value</c> of a <c>shape</c> channel, an entry of its <c>scale.range</c> or the <c>symbolType</c> of a legend (custom SVG path shapes are not drawn; a circle is).</description></item>
///         <item><term><c>vl-scale-interpolate-cubehelix</c></term><description>a <c>scale.interpolate</c> of the type <c>cubehelix</c> or <c>cubehelix-long</c> (RGB interpolation is used).</description></item>
///         <item><term><c>vl-scale-type-ignored</c> (warning)</term><description>a <c>scale.type</c> that the channel or the field type cannot use (the default type is used, as Vega-Lite does), and a quantitative field on a <c>shape</c> or <c>strokeDash</c> channel without a discretizing scale type (the scale is ordinal). A type that the renderer does not draw reports <c>vl-scale-&lt;type&gt;</c> only.</description></item>
///         <item><term><c>vl-encoding-impute</c>, <c>-bandposition</c>, <c>-bandposition-rect</c>, <c>-datum-datetime</c></term><description>that property of a supported channel (the <c>impute</c> of a channel needs the grouping fields of a path mark; the <c>impute</c> transform is supported). <c>-bandposition</c> is a <c>bandPosition</c> on a field without a time unit (or with a bin); <c>-bandposition-rect</c> is a <c>bandPosition</c> or <c>timeUnitBandPosition</c> other than 0.5 on a bar or rect whose field has a time unit (0.5 is used); <c>-datum-datetime</c> is a date-time object as the <c>datum</c> of a channel (the channel is ignored).</description></item>
///         <item><term><c>vl-timeunit-binned</c>, <c>vl-timeunit-maxbins</c></term><description>a <c>timeUnit</c> that is not applied: a <c>binned</c> unit (a name that starts with <c>binned</c>, or <c>binned: true</c>) in an encoding, a condition, a predicate or the <c>timeUnit</c> transform (the unit is ignored and the field is used as it is), and the <c>{"maxbins": n}</c> form without a unit in a predicate (ignored). The encoding and the transform apply <c>maxbins</c>.</description></item>
///         <item><term><c>vl-predicate-timeunit-part</c> (warning)</term><description>the <c>timeUnit</c> of a predicate (a filter or a condition test) whose unit has a <c>week</c> or <c>dayofyear</c> part; like Vega-Lite a predicate does not apply those parts.</description></item>
///         <item><term><c>vl-scale-nice-interval</c>, <c>vl-guide-axis-tickcount-interval</c> (warnings)</term><description>a time interval (a text or <c>{interval, step}</c>) as <c>scale.nice</c> or as the <c>tickCount</c> of an axis or legend on a scale that is not a time scale (it is ignored).</description></item>
///         <item><term><c>vl-scale-zero-ignored</c> (warning)</term><description>a <c>scale.zero</c> on a temporal field with a time or UTC scale or a color scale, where zero is not meaningful (it is ignored, as Vega-Lite does).</description></item>
///         <item><term><c>vl-encoding-condition</c></term><description>the <c>condition</c> of a channel that takes none (a position, secondary or order channel). The color, fill, stroke, opacity, fillOpacity, strokeOpacity, strokeWidth, strokeDash, size, shape and angle channels evaluate the <c>test</c> of a condition for every row.</description></item>
///         <item><term><c>vl-encoding-condition-param</c></term><description>a condition with a <c>param</c> (it never holds), and a <c>test</c> that names a selection or a parameter that is not a variable parameter.</description></item>
///         <item><term><c>vl-encoding-bin</c></term><description>the <c>bin</c> of a channel that cannot bin (<c>order</c> and the secondary channels), and <c>bin: "binned"</c> (data that is binned already) on a channel other than <c>x</c> and <c>y</c>, unless it is false or null. <c>aggregate</c>, <c>bin</c> (on <c>x</c>, <c>y</c> and the channels that have a scale), <c>stack</c> and <c>sort</c> are otherwise supported and are not reported.</description></item>
///         <item><term><c>vl-encoding-bin-extent-param</c></term><description>a <c>bin.extent</c> that is a <c>{"param": ...}</c> object in an encoding; the extent of the data is used.</description></item>
///         <item><term><c>vl-encoding-stack-nonlinear</c></term><description>an explicit <c>stack</c> (not false or null) of <c>x</c> or <c>y</c> whose <c>scale.type</c> is not <c>linear</c>; Vega-Lite does not stack on a log, pow, sqrt or symlog scale, so those marks are not stacked, and any other type is stacked on the linear scale.</description></item>
///         <item><term><c>vl-encoding-sort</c></term><description>a <c>sort</c> that is not applied: a channel shorthand (such as <c>"-y"</c>) or an <c>encoding</c> that names a channel the encoding does not have or that has neither a <c>field</c> nor an <c>aggregate</c>, and an object whose <c>op</c> is <c>values</c>, <c>exponential</c> or <c>exponentialb</c>. Ascending, descending, null and an array of values are always supported. The message names the channel that the sort refers to when that is the problem (the sort stays a report, not a specification error, because a channel can come from a parent encoding after normalization).</description></item>
///         <item><term><c>vl-encoding-sort-ignored</c> (warning)</term><description>a <c>sort</c> array, channel shorthand, or object with a <c>field</c>, <c>op</c> or <c>encoding</c> on a quantitative field whose scale is continuous (no discrete <c>scale.type</c>): there are no categories to order and the sort is ignored. <c>ascending</c>, <c>descending</c> and null are not reported.</description></item>
///         <item><term><c>vl-encoding-sort-mixed-types</c> (warning)</term><description>a <c>sort</c> array that mixes strings, numbers, booleans or null; produced by <see cref="SpecParser"/> at the array. Values are matched by kind (<c>1</c> does not position <c>"1"</c>), as Vega-Lite's strict comparison does. An object or array item of a <c>sort</c> array is a specification error instead (a date-time object is accepted on a temporal field).</description></item>
///         <item><term><c>vl-scale-&lt;type&gt;</c></term><description><c>scale.type</c>, for every type except linear, log, pow, sqrt, symlog, sequential, time, utc, band, point, ordinal, quantile, quantize, threshold and bin-ordinal.</description></item>
///         <item><term><c>vl-scale-property-&lt;name&gt;</c></term><description>a scale property other than type, domain, range, zero, nice, padding, reverse, paddingInner, paddingOuter, align, round, clamp, scheme, domainMid, domainMin, domainMax, rangeMin, rangeMax, interpolate, base, exponent and constant. An unknown color scheme name is a specification error of the scale resolver, not a report.</description></item>
///         <item><term><c>vl-guide-legend-titleorient</c>, <c>vl-guide-legend-clipheight</c></term><description>the <c>titleOrient</c> of a legend when it is not <c>top</c> (the title is drawn above the entries) and its <c>clipHeight</c>; legends themselves are drawn and are not reported.</description></item>
///         <item><term><c>vl-guide-encode</c></term><description><c>axis.encode</c> and <c>legend.encode</c>.</description></item>
///         <item><term><c>vl-guide-axis-&lt;name&gt;</c></term><description>an axis property from the list of unsupported axis properties (<c>labelBound</c>, <c>tickBand</c>, <c>tickExtra</c>, <c>position</c>, <c>translate</c>, <c>bandPosition</c>, <c>titleX</c>, <c>titleY</c>).</description></item>
///         <item><term><c>vl-format-type-custom</c></term><description><c>axis.formatType</c> and <c>legend.formatType</c> other than <c>number</c>, <c>time</c> and <c>utc</c> (a custom format type is not evaluated; the default format is used).</description></item>
///         <item><term><c>vl-expr-ref</c>, <c>vl-expr-label</c></term><description>an <c>{"expr": ...}</c> value that the renderer does not evaluate (anywhere in a view's title, size, padding, background, view, in a mark property other than the row-bound paint properties, or in an encoding other than the <c>value</c> of a color, fill, stroke, opacity or symbol size channel), and <c>axis.labelExpr</c> and <c>legend.labelExpr</c>. The row-bound sites are evaluated and are reported only for what their expression cannot do.</description></item>
///         <item><term><c>vl-expr-function-&lt;name&gt;</c>, <c>vl-expr-view-state</c>, <c>vl-expr-construct-&lt;name&gt;</c></term><description>the expression text of a supported site (a calculate, a filter, a variable parameter or a row-bound expression reference): a call of a function outside the whitelist (the number format and color functions among them), a use of view state (<c>signal</c>, <c>event</c>, <c>item</c>, <c>width</c>, <c>height</c>), or a deferred operator (bitwise, shift, <c>in</c>). The construct evaluates to null; the position is that of the expression text and the message names the offset inside it.</description></item>
///         <item><term><c>vl-transform-&lt;name&gt;</c></term><description>every entry of <c>transform</c> that the renderer does not compute: the transforms of later phases (<c>density</c>, <c>extent</c>, <c>loess</c>, <c>quantile</c>, <c>regression</c>), and any transform of a nested view of a composition, which is not drawn (including <c>aggregate</c>, <c>joinaggregate</c>, <c>window</c>, <c>bin</c>, <c>stack</c>, <c>impute</c> and <c>timeunit</c>). <c>calculate</c>, <c>filter</c>, <c>fold</c>, <c>flatten</c>, <c>sample</c>, <c>pivot</c>, <c>lookup</c>, <c>aggregate</c>, <c>joinaggregate</c>, <c>window</c>, <c>bin</c>, <c>stack</c>, <c>impute</c> and <c>timeUnit</c> of the drawn view are computed and not reported.</description></item>
///         <item><term><c>vl-transform-filter-param</c>, <c>-pivot-op</c>, <c>-lookup-param</c>, <c>-bin-extent-param</c></term><description>the parts of a computed transform that are not supported: a filter predicate on a selection parameter (every row passes), a pivot <c>op</c> other than sum and count (sum is used), a lookup in the data of a parameter (not applied) and a <c>bin</c> whose <c>extent</c> is a parameter (the extent of the data is used).</description></item>
///         <item><term><c>vl-composition-layer</c>, <c>-concat</c>, <c>-hconcat</c>, <c>-vconcat</c>, <c>-facet</c>, <c>-repeat</c></term><description>that property; the views nested in it are examined too.</description></item>
///         <item><term><c>vl-param-variable</c>, <c>vl-param-selection</c></term><description>every selection parameter (it has <c>select</c>) and the legacy top-level <c>selection</c> report <c>vl-param-selection</c>; a variable parameter of a nested view reports <c>vl-param-variable</c>. The variable parameters of the root view are computed and are not reported. <c>bind</c> is ignored.</description></item>
///         <item><term><c>vl-projection</c></term><description>the <c>projection</c> of a view.</description></item>
///         <item><term><c>vl-layout-autosize-&lt;type&gt;</c></term><description><c>autosize</c> types other than pad and none (fit, fit-x, fit-y).</description></item>
///     </list>
///     <para>
///         The warnings <c>vl-duplicate-key</c>, <c>vl-unknown-property</c> and <c>vl-encoding-sort-mixed-types</c> are produced by <see cref="SpecParser"/>
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
    private static readonly HashSet<string> SupportedChannels =
    [
        "x", "y", "color", "fill", "stroke", "size", "opacity", "shape", "strokeWidth", "strokeDash", "fillOpacity", "strokeOpacity", "angle",
    ];

    /// <summary>The channels that are accepted and ignored without a report (interaction, accessibility and linking).</summary>
    private static readonly HashSet<string> IgnoredChannels = ["tooltip", "href", "description", "key"];

    /// <summary>The geographic position channels, which all report <c>vl-channel-geo</c>.</summary>
    private static readonly HashSet<string> GeoChannels = ["longitude", "latitude", "longitude2", "latitude2"];

    /// <summary>The channels that get a legend when they are driven by a field.</summary>
    private static readonly HashSet<string> LegendChannels = ["color", "fill", "stroke", "size", "opacity", "shape", "strokeWidth", "strokeDash"];

    /// <summary>The marks that draw a range between a position and its secondary channel (<c>x2</c> and <c>y2</c>).</summary>
    private static readonly HashSet<string> RangedMarks = ["bar", "rect", "rule"];

    /// <summary>The channels on which <c>bin</c> is applied: the position channels and every channel that has a scale (a <c>bin: "binned"</c> is read on the position channels only).</summary>
    private static readonly HashSet<string> BinnedChannels =
    [
        "x", "y", "color", "fill", "stroke", "size", "opacity", "shape", "strokeWidth", "strokeDash", "fillOpacity", "strokeOpacity", "angle",
    ];

    /// <summary>The scale types that the renderer implements.</summary>
    private static readonly HashSet<string> SupportedScaleTypes = ["linear", "log", "pow", "sqrt", "symlog", "sequential", "band", "point", "ordinal", "quantile", "quantize", "threshold", "bin-ordinal", "time", "utc"];

    /// <summary>The scale types that order categories (a quantitative field with one of them is not on a continuous scale).</summary>
    private static readonly HashSet<string> DiscreteScaleTypes = ["band", "point", "ordinal", "bin-ordinal", "quantile", "quantize", "threshold"];

    /// <summary>The scale properties that the renderer implements (<c>interpolate</c> except for the cubehelix types, see the scale visit).</summary>
    private static readonly HashSet<string> SupportedScaleProperties =
    [
        "type", "domain", "range", "zero", "nice", "padding", "reverse", "paddingInner", "paddingOuter", "align", "round", "clamp", "scheme",
        "domainMid", "domainMin", "domainMax", "rangeMin", "rangeMax", "interpolate", "base", "exponent", "constant",
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
    ///     Tells whether a <c>formatType</c> of an axis or a legend is one that the renderer evaluates: <c>number</c> (d3 number format), <c>time</c> (d3 time format in the zone
    ///     of the chart) and <c>utc</c> (d3 time format in UTC). Any other name is a custom format type that the host would have to provide; it is reported and the default format is used.
    /// </summary>
    /// <param name="formatType">The format type, or null when the property is not a text.</param>
    /// <returns><see langword="true"/> for <c>number</c>, <c>time</c> and <c>utc</c>.</returns>
    public static bool IsEvaluatedFormatType(string? formatType) => formatType is "number" or "time" or "utc";

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

        /// <summary>The mark definition object of the view whose encoding is visited, or null when the mark is a name.</summary>
        private SpecNode? _mark;

        /// <summary>The mark type of the view whose encoding is visited.</summary>
        private string _markType = string.Empty;

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
            else if (mark["shape"] is { IsString: true } shape)
            {
                VisitShapeName(shape);
            }
        }

        /// <summary>
        ///     Visits an <c>encoding</c>.
        /// </summary>
        /// <param name="view">The view that owns the encoding (its mark decides about the secondary position channels and the stack).</param>
        /// <param name="encoding">The encoding object.</param>
        private void VisitEncoding(SpecNode view, SpecNode encoding)
        {
            if (!encoding.IsObject)
            {
                return;
            }

            var mark = view["mark"];
            var markType = (mark is { IsString: true } ? mark.StringValue : mark?["type"]?.AsString()) ?? string.Empty;
            _mark = mark is { IsObject: true } ? mark : null;
            _markType = markType;
            // The order channel orders a stack that the plan will make, which is decided with the orientation that the scales infer
            var stacks = new Lazy<bool>(() => StackRules.Decide(markType, ScaleResolver.InferOrient(markType, mark?["orient"]?.AsString(), encoding), encoding) is not null);
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
                else if (name is "x2" or "y2")
                {
                    // A range is drawn by a bar, a rect or a rule that has the position channel it extends
                    if (RangedMarks.Contains(markType) && encoding[name[..1]] is { IsObject: true })
                    {
                        VisitSecondaryDefinition(name, channel);
                    }
                    else
                    {
                        Unsupported(Id("channel", name), $"The '{name}' channel is only supported for a bar, rect or rule mark that has the '{name[..1]}' channel.", channel);
                    }
                }
                else if (name == "order")
                {
                    // The order channel is the order of a stack; the order of the marks themselves is not drawn yet
                    if (stacks.Value)
                    {
                        VisitSecondaryDefinition(name, channel);
                    }
                    else
                    {
                        Unsupported("vl-channel-order", "The 'order' channel is only supported when it orders a stack; the drawing order of marks is not supported yet.", channel);
                    }
                }
                else if (!SupportedChannels.Contains(name))
                {
                    // Channels that Vega-Lite defines but the renderer does not draw; unknown names were already warned about
                    if (SpecParser.IsChannelName(name))
                    {
                        Unsupported(Id("channel", name), $"The '{name}' channel is not supported yet.", channel);
                    }
                }
                else if (name is "shape" or "angle" or "strokeDash" && !ScaleResolver.ChannelIsDrawn(name, markType))
                {
                    // The channel is supported, but the mark does not draw it (a shape needs a point, an angle a symbol, a dash a mark with a stroke line)
                    Unsupported(Id("channel", name), $"The '{name}' channel is not drawn by the '{markType}' mark; it is ignored.", channel);
                }
                else if (channel.IsObject)
                {
                    VisitChannelDefinition(name, channel);
                }
            }
        }

        /// <summary>
        ///     Visits the definition (or the array of definitions) of a secondary or order channel that the renderer accepts.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="channel">The channel value.</param>
        private void VisitSecondaryDefinition(string name, SpecNode channel)
        {
            foreach (var definition in channel.IsArray ? channel.Items : [channel])
            {
                if (definition.IsObject)
                {
                    VisitChannelDefinition(name, definition);
                }
            }
        }

        /// <summary>
        ///     Visits the properties of the definition of a supported channel.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitChannelDefinition(string name, SpecNode definition)
        {
            if (definition["type"] is { IsString: true, StringValue: "geojson" } type)
            {
                Unsupported("vl-type-geojson", "GeoJSON fields are not supported.", type);
            }

            VisitTimeUnit(definition["timeUnit"], maxBinsUnsupported: false);
            VisitBin(name, definition);
            VisitStack(name, definition);
            VisitSort(name, definition);
            VisitCondition(name, definition);
            ReportProperty(definition, "impute", n => !n.IsNull, "Imputation of a channel is not supported yet (the impute transform is).");
            VisitBandPosition(definition);
            if (definition["datum"] is { IsObject: true } datum && SpecParser.IsDateTimeShaped(datum))
            {
                Unsupported("vl-encoding-datum-datetime", "A date-time object as the datum of a channel is not supported yet; the channel is ignored.", datum);
            }

            if (name == "shape" && definition["value"] is { IsString: true } shape)
            {
                VisitShapeName(shape);
            }

            var scale = definition["scale"];
            if (scale is { IsObject: true })
            {
                VisitScale(name, scale);
            }

            VisitScaleType(name, definition);
            VisitIntervals(name, definition);

            if (definition["axis"] is { IsObject: true } axis)
            {
                VisitAxis(axis);
            }

            VisitLegend(name, definition);
        }

        /// <summary>
        ///     Reports a time unit that the renderer does not apply: a <c>binned</c> unit (the data is already floored; the unit is ignored and the field is used as it is),
        ///     and, where it cannot be resolved (a predicate), the <c>maxbins</c> form that names no unit.
        /// </summary>
        /// <param name="timeUnit">The <c>timeUnit</c> value, or null.</param>
        /// <param name="maxBinsUnsupported">Whether the <c>maxbins</c> form without a unit is not applied here (a predicate has no data extent to bin).</param>
        private void VisitTimeUnit(SpecNode? timeUnit, bool maxBinsUnsupported)
        {
            if (timeUnit is not { IsNull: false } || !TryReadTimeUnit(timeUnit, out var unit))
            {
                return;
            }

            if (unit.IsBinned)
            {
                Unsupported("vl-timeunit-binned", "A binned time unit is not supported; the unit is ignored and the field is used as it is.", timeUnit);
            }
            else if (maxBinsUnsupported && unit.Parts.Count == 0)
            {
                Unsupported("vl-timeunit-maxbins", "A time unit given only as maxbins is not supported in a predicate; the unit is ignored and the field is compared as it is.", timeUnit);
            }
        }

        /// <summary>
        ///     Reads a <c>timeUnit</c> value, which the parser has validated.
        /// </summary>
        /// <param name="node">The string or object.</param>
        /// <param name="unit">Receives the unit.</param>
        /// <returns><see langword="true"/> when the value is a time unit.</returns>
        private static bool TryReadTimeUnit(SpecNode node, [NotNullWhen(true)] out TimeUnitSpec? unit) =>
            TimeUnitSpec.TryRead(node, out unit);

        /// <summary>
        ///     Reports the <c>bandPosition</c> that is not applied: one on a field without a time unit, and one other than 0.5 (or a mark
        ///     <c>timeUnitBandPosition</c> other than 0.5) on a bar or rect whose field has a time unit.
        /// </summary>
        /// <remarks>
        ///     Vega-Lite positions the rect of a time unit with shifted start and end fields when the band position is not the middle (0.5 with the band size 1,
        ///     which spans the unit). The renderer spans the unit; a position on a field without a time unit, or with a bin, is not applied either.
        /// </remarks>
        /// <param name="definition">The channel definition object.</param>
        private void VisitBandPosition(SpecNode definition)
        {
            var hasUnit = definition["timeUnit"] is { IsNull: false } timeUnit && TryReadTimeUnit(timeUnit, out var unit) && !unit.IsBinned && unit.Parts.Count > 0;
            if (definition["bandPosition"] is { } position)
            {
                if (!hasUnit)
                {
                    Unsupported("vl-encoding-bandposition", "The band position of a field is not supported yet.", position);
                }
                else if (_markType is "bar" or "rect" && !(position.IsNumber && position.NumberValue.CompareTo(0.5) == 0))
                {
                    Unsupported("vl-encoding-bandposition-rect", "A band position other than 0.5 on the rect of a time unit is not supported; 0.5 is used.", position);
                }

                return;
            }

            if (hasUnit && _markType is "bar" or "rect" && TimeUnitBandPosition(_markType) is { } configured && configured.IsNumber && configured.NumberValue.CompareTo(0.5) != 0)
            {
                Unsupported("vl-encoding-bandposition-rect", "A timeUnitBandPosition other than 0.5 on a bar or rect is not supported; 0.5 is used.", configured);
            }
        }

        /// <summary>
        ///     Finds the <c>timeUnitBandPosition</c> that the specification gives a mark: in the mark definition, else in <c>config.&lt;mark&gt;</c> or <c>config.mark</c>.
        /// </summary>
        /// <param name="markType">The mark type.</param>
        /// <returns>The value node, or null when the specification gives none.</returns>
        private SpecNode? TimeUnitBandPosition(string markType) =>
            _mark?["timeUnitBandPosition"]
            ?? _tree.Root["config"]?[markType]?["timeUnitBandPosition"]
            ?? _tree.Root["config"]?["mark"]?["timeUnitBandPosition"];

        /// <summary>
        ///     Warns about an interval given as <c>tickCount</c> or <c>nice</c> where the scale is not a time scale (the interval is ignored).
        /// </summary>
        /// <param name="channel">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitIntervals(string channel, SpecNode definition)
        {
            // A temporal field has a time scale whatever type is written (only time and utc are accepted for it; on a color channel it is the time scale of Vega-Lite, drawn here as a sequential one)
            var scaleType = definition["scale"]?["type"]?.AsString();
            var isTemporal = definition["type"]?.AsString() == "temporal";
            var isTime = isTemporal ? channel is not ("shape" or "strokeDash") : scaleType is "time" or "utc";
            if (isTime)
            {
                return;
            }

            if (definition["scale"]?["nice"] is { } nice && IsIntervalValue(nice))
            {
                Warn("vl-scale-nice-interval", "A time interval as 'nice' needs a time scale; it is ignored.", nice);
            }

            foreach (var guide in new[] { definition["axis"], definition["legend"] })
            {
                if (guide?["tickCount"] is { } tickCount && IsIntervalValue(tickCount))
                {
                    Warn("vl-guide-axis-tickcount-interval", "A time interval as 'tickCount' needs a time scale; the default tick count is used.", tickCount);
                }
            }
        }

        /// <summary>
        ///     Tells whether a <c>tickCount</c> or <c>nice</c> value is an interval (a text or an object, not an expression reference).
        /// </summary>
        /// <param name="node">The value.</param>
        /// <returns><see langword="true"/> for a text or an interval object.</returns>
        private static bool IsIntervalValue(SpecNode node) => node.IsString || (node.IsObject && node["expr"] is not { IsString: true });

        /// <summary>
        ///     Reports a <c>sort</c> that is not applied: one the renderer cannot compute, one that refers to a channel the encoding does
        ///     not have, and one that a continuous scale ignores.
        /// </summary>
        /// <remarks>
        ///     <c>ascending</c>, <c>descending</c> and null are meaningful on every scale. A sort array, a channel shorthand and an object
        ///     that sorts by a field, an operation or a channel orders the categories of a discrete scale; on a quantitative field
        ///     (whose scale is continuous unless <c>scale.type</c> names a discrete scale) it has nothing to order and is ignored, which
        ///     is the warning <c>vl-encoding-sort-ignored</c>.
        /// </remarks>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitSort(string name, SpecNode definition)
        {
            if (definition["sort"] is not { } sort)
            {
                return;
            }

            if (!SpecParser.IsSupportedSort(sort, definition))
            {
                if (SpecParser.TryGetSortChannel(sort, out var other))
                {
                    Unsupported(
                        "vl-encoding-sort",
                        $"The sort refers to the '{other}' channel, which this encoding does not have (or which reads no field); the categories are sorted ascending.",
                        sort);
                }
                else
                {
                    Unsupported("vl-encoding-sort", "Sorting by this operation is not supported yet; the categories are sorted ascending.", sort);
                }

                return;
            }

            if (name is not ("order" or "x2" or "y2") && SortOrdersCategories(sort) && IsContinuous(name, definition))
            {
                Warn(
                    "vl-encoding-sort-ignored",
                    "The sort orders the categories of a discrete scale; this field is quantitative with a continuous scale, so the sort is ignored.",
                    sort);
            }
        }

        /// <summary>
        ///     Determines whether a <c>sort</c> lists, or computes, an order of categories (everything but ascending, descending, null and an order-only object).
        /// </summary>
        /// <param name="sort">The <c>sort</c> value.</param>
        /// <returns><see langword="true"/> for an array, a channel shorthand, or an object with a field, an operation or an encoding.</returns>
        private static bool SortOrdersCategories(SpecNode sort) => sort.Kind switch
        {
            SpecNodeKind.Array => true,
            SpecNodeKind.String => sort.StringValue is not ("ascending" or "descending"),
            SpecNodeKind.Object => sort["field"] is not null || sort["op"] is not null || sort["encoding"] is not null,
            _ => false,
        };

        /// <summary>
        ///     Determines whether a channel definition is a quantitative field on a continuous scale (the shape and stroke dash channels
        ///     have categories only, so their scale is never continuous).
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        /// <returns><see langword="true"/> for the type <c>quantitative</c> without a discrete <c>scale.type</c>.</returns>
        private static bool IsContinuous(string name, SpecNode definition) =>
            definition["type"] is { IsString: true, StringValue: "quantitative" }
            && name is not ("shape" or "strokeDash")
            && !(definition["scale"]?["type"] is { IsString: true } scaleType && DiscreteScaleTypes.Contains(scaleType.StringValue!));

        /// <summary>
        ///     Determines whether a channel definition asks the encoding to bin its field (<c>bin</c> true or an object, not <c>"binned"</c>).
        /// </summary>
        /// <param name="definition">The channel definition object.</param>
        /// <returns><see langword="true"/> when the bins are computed from the data.</returns>
        private static bool IsBinning(SpecNode definition) =>
            definition["bin"] is { } bin && IsEnabled(bin) && !(bin.IsString || bin["binned"] is { IsBoolean: true, BoolValue: true });

        /// <summary>
        ///     Reports the <c>bin</c> of a channel that is not a position channel, and a bin extent that is a parameter.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitBin(string name, SpecNode definition)
        {
            if (definition["bin"] is not { } bin || !IsEnabled(bin))
            {
                return;
            }

            if (!BinnedChannels.Contains(name))
            {
                Unsupported("vl-encoding-bin", $"Binning is not supported on the '{name}' channel; it bins the x and y channels and the channels that have a scale.", bin);
            }
            else if (name is not ("x" or "y") && (bin is { IsString: true } || bin["binned"] is { IsBoolean: true, BoolValue: true }))
            {
                Unsupported("vl-encoding-bin", $"A field that is binned already (bin: \"binned\") is only supported on the x and y channels, not on '{name}'.", bin);
            }
            else if (bin is { IsObject: true } && bin["extent"] is { IsObject: true } extent)
            {
                Unsupported("vl-encoding-bin-extent-param", "A bin extent that is a parameter is not supported yet; the extent of the data is used.", extent);
            }
        }

        /// <summary>
        ///     Reports an explicit <c>stack</c> on a scale that is not linear.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitStack(string name, SpecNode definition)
        {
            if (name is not ("x" or "y") || definition["stack"] is not { } stack || !StackRules.IsEnabled(stack))
            {
                return;
            }

            if (definition["scale"]?["type"] is { IsString: true } scaleType && scaleType.StringValue != "linear")
            {
                // Vega-Lite does not stack on a log, pow, sqrt or symlog scale: the stack is off and the marks start at the baseline
                var message = scaleType.StringValue is "log" or "pow" or "sqrt" or "symlog"
                    ? $"Stacking on a '{scaleType.StringValue}' scale is not supported; Vega-Lite does not stack on it, so the marks are not stacked."
                    : $"Stacking on a '{scaleType.StringValue}' scale is not supported; the values are stacked on the linear scale.";
                Unsupported("vl-encoding-stack-nonlinear", message, stack);
            }
        }

        /// <summary>
        ///     Reports the <c>condition</c> of a channel definition that is not applied, and visits the field definition of a condition that is.
        /// </summary>
        /// <remarks>
        ///     A condition with a <c>test</c> is evaluated for every row on the channels that accept one. A condition on a channel that cannot take one
        ///     is reported as a whole; a condition on a parameter (or a test that refers to a selection, a time unit or a date object)
        ///     is reported and never holds.
        /// </remarks>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitCondition(string name, SpecNode definition)
        {
            if (definition["condition"] is not { } condition)
            {
                return;
            }

            if (!ChannelDefinitions.CanCondition(name))
            {
                Unsupported("vl-encoding-condition", $"A conditional encoding is not supported on the '{name}' channel; the condition is ignored.", condition);
                return;
            }

            foreach (var item in condition.IsArray ? condition.Items : [condition])
            {
                if (!item.IsObject)
                {
                    continue;
                }

                if (item["param"] is not null)
                {
                    Unsupported("vl-encoding-condition-param", "A condition on a parameter is not supported yet; it never holds.", item);
                    continue;
                }

                if (item["test"] is { } test)
                {
                    VisitConditionTest(test);
                }

                VisitChannelDefinition(name, item);
            }
        }

        /// <summary>
        ///     Reports the part of the test of a condition that the renderer cannot evaluate.
        /// </summary>
        /// <param name="test">The <c>test</c> value: an expression text or a predicate object.</param>
        private void VisitConditionTest(SpecNode test)
        {
            if (test.IsString)
            {
                ReportConstructs(test);
                return;
            }

            if (!test.IsObject)
            {
                return;
            }

            // The strings inside a predicate that has a part nothing can evaluate are reported with it; a predicate that can be evaluated reports its time units only
            var unsupported = PredicateSource.IsUnsupported(test);
            var pending = new Stack<SpecNode>();
            pending.Push(test);
            while (pending.Count > 0)
            {
                var predicate = pending.Pop();
                if (predicate.IsString)
                {
                    if (unsupported)
                    {
                        ReportConstructs(predicate);
                    }
                }
                else if (predicate.IsObject)
                {
                    VisitConditionPredicate(predicate, pending);
                }
            }
        }

        /// <summary>
        ///     Reports one object of a predicate that cannot be evaluated, or queues its parts.
        /// </summary>
        /// <param name="predicate">The predicate object.</param>
        /// <param name="pending">The predicates still to visit.</param>
        private void VisitConditionPredicate(SpecNode predicate, Stack<SpecNode> pending)
        {
            var composed = predicate["and"] ?? predicate["or"];
            var named = predicate["param"] ?? predicate["selection"];
            if (composed is { IsArray: true })
            {
                foreach (var item in composed.Items)
                {
                    pending.Push(item);
                }
            }
            else if (predicate["not"] is { } negated)
            {
                pending.Push(negated);
            }
            else if (named is not null)
            {
                Unsupported("vl-encoding-condition-param", "A test on a selection or a parameter that is not a variable is not supported yet; the condition never holds.", named);
            }
            else
            {
                VisitPredicateTimeUnit(predicate);
            }
        }

        /// <summary>
        ///     Reports the time unit of a field predicate: a <c>binned</c> unit or the <c>maxbins</c> form (ignored), and the parts of a unit that a predicate does not apply.
        /// </summary>
        /// <remarks>
        ///     Like Vega-Lite, a predicate does not apply the <c>week</c> and <c>dayofyear</c> parts of a time unit (the transform and the encoding do); the
        ///     predicate then compares with the other parts of the unit, which is the warning <c>vl-predicate-timeunit-part</c>.
        /// </remarks>
        /// <param name="predicate">The field predicate object.</param>
        private void VisitPredicateTimeUnit(SpecNode predicate)
        {
            if (predicate["timeUnit"] is not { IsNull: false } timeUnit)
            {
                return;
            }

            VisitTimeUnit(timeUnit, maxBinsUnsupported: true);
            if (PredicateSource.ReadUnit(predicate) is { } unit && (unit.Parts.Contains("week") || unit.Parts.Contains("dayofyear")))
            {
                Warn(
                    "vl-predicate-timeunit-part",
                    "A predicate does not apply the week and dayofyear parts of a time unit (as in Vega-Lite); the other parts are compared.",
                    timeUnit);
            }
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
        ///     Visits a scale definition.
        /// </summary>
        /// <param name="channel">The channel the scale belongs to.</param>
        /// <param name="scale">The scale object.</param>
        private void VisitScale(string channel, SpecNode scale)
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
                else if (name == "interpolate")
                {
                    // Cubehelix is recognized and interpolates in RGB instead; the other interpolation types are implemented
                    var type = property.IsString ? property.StringValue : property["type"]?.AsString();
                    if (type is "cubehelix" or "cubehelix-long")
                    {
                        Unsupported("vl-scale-interpolate-cubehelix", $"The '{type}' color interpolation is not supported; RGB interpolation is used.", property);
                    }
                }
                else if (name == "range" && channel == "shape" && property.IsArray)
                {
                    foreach (var item in property.Items)
                    {
                        VisitShapeName(item);
                    }
                }
                else if (!SupportedScaleProperties.Contains(name))
                {
                    Unsupported(Id("scale-property", name), $"The scale property '{name}' is not supported yet.", property);
                }
            }
        }

        /// <summary>
        ///     Reports a shape that is a custom SVG path instead of one of the symbol names; the painter draws a circle for it.
        /// </summary>
        /// <param name="shape">The value: a shape name or a path.</param>
        private void VisitShapeName(SpecNode shape)
        {
            if (shape.IsString && !ScaleResolver.IsKnownShape(shape.StringValue!))
            {
                Unsupported("vl-shape-path", $"The shape '{shape.StringValue}' is not a symbol name; custom SVG path shapes are not supported, and a circle is drawn.", shape);
            }
        }

        /// <summary>
        ///     Warns about a <c>scale.type</c> that the channel or the field type cannot use, and about a quantitative field on a channel
        ///     that has categories only.
        /// </summary>
        /// <remarks>
        ///     Vega-Lite replaces the type with the default one and warns; so does the scale resolver. A type that the renderer does not
        ///     draw is reported as <c>vl-scale-&lt;type&gt;</c> instead and is not repeated here.
        /// </remarks>
        /// <param name="channel">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitScaleType(string channel, SpecNode definition)
        {
            if (definition["type"] is not { IsString: true, StringValue: "quantitative" or "temporal" or "nominal" or "ordinal" } fieldType || definition["field"] is null)
            {
                return;
            }

            // Zero is not meaningful for time: Vega-Lite drops the property and says so
            if (fieldType.StringValue == "temporal" && definition["scale"]?["zero"] is { } zero
                && definition["scale"]?["type"]?.AsString() is null or "time" or "utc")
            {
                Warn("vl-scale-zero-ignored", "The scale property 'zero' is not applicable to a time scale; it is ignored.", zero);
            }

            if (definition["scale"]?["type"] is { IsString: true } scaleType)
            {
                if (SupportedScaleTypes.Contains(scaleType.StringValue!) && !ScaleResolver.AcceptsScaleType(channel, fieldType.StringValue, scaleType.StringValue!, IsBinning(definition)))
                {
                    Warn(
                        "vl-scale-type-ignored",
                        $"The '{scaleType.StringValue}' scale type cannot be used for the '{channel}' channel with a {fieldType.StringValue} field; the default scale type is used.",
                        scaleType);
                }
            }
            else if (channel is "shape" or "strokeDash" && fieldType.StringValue is "quantitative" or "temporal")
            {
                Warn(
                    "vl-scale-type-ignored",
                    $"The '{channel}' channel shows categories, so a {fieldType.StringValue} field gets an ordinal scale; use a nominal or ordinal field{(fieldType.StringValue == "quantitative" ? " or a discretizing scale type (quantize, quantile, threshold)" : string.Empty)}.",
                    fieldType);
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
                    case "formatType" when !IsEvaluatedFormatType(property.AsString()):
                        Unsupported("vl-format-type-custom", "Custom format types (axis.formatType) are not supported; the format type must be number, time or utc.", property);
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
        ///     Reports the parts of a legend definition that the renderer does not draw.
        /// </summary>
        /// <remarks>
        ///     Legends themselves are drawn (by the <c>LegendPlan</c> and <c>LegendBuilder</c> units) and are not reported. A <c>titleOrient</c> other than <c>top</c> and a
        ///     <c>clipHeight</c> are not drawn, <c>encode</c>, <c>labelExpr</c> and <c>formatType</c> are not evaluated, and a
        ///     <c>symbolType</c> that is not a symbol name is a custom shape path (a circle is drawn).
        /// </remarks>
        /// <param name="channel">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitLegend(string channel, SpecNode definition)
        {
            if (!LegendChannels.Contains(channel) || definition["legend"] is not { IsObject: true } legend)
            {
                return;
            }

            foreach (var property in legend.Properties)
            {
                switch (property.Name)
                {
                    case "encode":
                        Unsupported("vl-guide-encode", "Custom guide encodings (legend.encode) are not supported.", property);
                        break;
                    case "labelExpr":
                        Unsupported("vl-expr-label", "Label expressions (legend.labelExpr) are not supported yet.", property);
                        break;
                    case "formatType" when !IsEvaluatedFormatType(property.AsString()):
                        Unsupported("vl-format-type-custom", "Custom format types (legend.formatType) are not supported; the format type must be number, time or utc.", property);
                        break;
                    case "titleOrient" when property.IsString && property.StringValue != "top":
                        Unsupported(
                            "vl-guide-legend-titleorient",
                            $"The legend title orient '{property.StringValue}' is not supported; the title is drawn above the legend.",
                            property);
                        break;
                    case "clipHeight":
                        Unsupported("vl-guide-legend-clipheight", "The legend property 'clipHeight' is not supported yet.", property);
                        break;
                    case "symbolType":
                        VisitShapeName(property);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        ///     Visits the entries of <c>transform</c>.
        /// </summary>
        /// <remarks>
        ///     The transforms the renderer computes (calculate, filter, fold, flatten, sample, pivot, lookup, aggregate, joinaggregate,
        ///     window, bin, stack, impute, timeUnit) are reported only for the parts they do not support: expression constructs, a
        ///     binned time unit, the time unit of a predicate (see <c>vl-predicate-timeunit-part</c>), selection parameters, a pivot operation other than sum and count, a lookup
        ///     in the data of a parameter, and a bin extent that is a parameter. A transform of a view that is not drawn (a nested
        ///     view of a composition) is never applied and is reported as <c>vl-transform-&lt;name&gt;</c>, like every transform of a
        ///     later phase.
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
                    case "timeUnit":
                        VisitTimeUnit(operation, maxBinsUnsupported: false);
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
                    case "bin":
                        if (operation is { IsObject: true } && operation["extent"] is { IsObject: true } extent)
                        {
                            Unsupported("vl-transform-bin-extent-param", "A bin extent that is a parameter is not supported yet; the extent of the data is used.", extent);
                        }

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
        ///     Visits a field predicate: its time unit.
        /// </summary>
        /// <param name="predicate">The predicate object.</param>
        private void VisitFieldPredicate(SpecNode predicate) => VisitPredicateTimeUnit(predicate);

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

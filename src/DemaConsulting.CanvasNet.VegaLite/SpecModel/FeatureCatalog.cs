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
/// <param name="Id">The stable <c>vl-*</c> identifier (lower case, for example <c>vl-mark-geoshape</c>).</param>
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
///         <item><term><c>vl-mark-&lt;type&gt;</c></term><description>the <c>mark</c>, for every mark type except bar, rect, point, circle, square, rule, tick, line, area, trail, text, arc, boxplot, errorbar and errorband (<c>vl-mark-boxplot</c>, <c>vl-mark-errorbar</c> and <c>vl-mark-errorband</c> were retired in Phase 10 and are never produced).</description></item>
///         <item><term><c>vl-composite-grouping</c></term><description>a channel of a <c>boxplot</c>, <c>errorbar</c> or <c>errorband</c> that groups the rows (every channel but the measure axis, the second ends and errors, and the tooltip) and has a <c>bin</c>, a <c>timeUnit</c> or an <c>aggregate</c>: the rows are grouped by the raw field (a bin or a time unit is not applied) and a channel that aggregates is left out of the layers.</description></item>
///         <item><term><c>vl-channel-xerror</c>, <c>-xerror2</c>, <c>-yerror</c>, <c>-yerror2</c></term><description>an error channel on a mark other than <c>errorbar</c> and <c>errorband</c>, which use them (the channels are ignored). The <c>x2</c> and <c>y2</c> channels of those two marks are the ends of the range and are not reported; the <c>size</c> channel of an <c>errorbar</c> reports <c>vl-channel-size</c> (Vega-Lite drops it).</description></item>
///         <item><term><c>vl-mark-invalid-show</c>, <c>vl-mark-invalid-domains</c> (warning), <c>vl-mark-interpolate-bundle-area</c>, <c>vl-mark-interpolate-unknown</c></term><description>the <c>invalid</c> of a mark: <c>show</c> and null report <c>vl-mark-invalid-show</c> (the default mode applies), and <c>filter</c> and <c>break-paths-filter-domains</c> warn that the scale domains of this renderer still include the invalid rows; the <c>interpolate: "bundle"</c> of an <c>area</c>, which d3-shape cannot draw (the area uses the linear interpolation); and an <c>interpolate</c> name of a line or area that is not one of Vega-Lite's 15 (the parser rejects it in a mark definition, so it can only come from the configuration; the line is drawn with the linear interpolation). The value is read as the mark builder reads it: from the mark definition, else from the <c>config.style</c> entries of the mark, <c>config.&lt;mark&gt;</c> or <c>config.mark</c> of the specification; a value from the configuration is reported at the configuration (once, however many marks use it).</description></item>
///         <item><term><c>vl-channel-&lt;name&gt;</c></term><description>an encoding channel other than x, y, xOffset, yOffset, color, fill, stroke, size, opacity, shape, strokeWidth, strokeDash, fillOpacity, strokeOpacity, angle, text, theta, radius, theta2, radius2, order and detail; <c>shape</c> is drawn by a point, <c>text</c> by a text mark, <c>angle</c> by a point, circle, square or text, <c>theta</c> and <c>radius</c> by an arc or text mark, <c>theta2</c> and <c>radius2</c> by an arc mark, <c>strokeDash</c> by every mark but a rect, area, trail, text and arc and <c>size</c> by every mark but a rect, an area and an arc, and the channel is reported (and ignored) for any other mark; <c>tooltip</c>, <c>href</c>, <c>description</c> and <c>key</c> are accepted and ignored without a report; <c>longitude</c>, <c>latitude</c>, <c>longitude2</c> and <c>latitude2</c> report <c>vl-channel-geo</c>. <c>x2</c> and <c>y2</c> are accepted (the range of a bar, rect, rule or area) when the mark is one of those and the channel they extend (<c>x</c> or <c>y</c>) is present, and report <c>vl-channel-x2</c> / <c>vl-channel-y2</c> otherwise. <c>order</c> (the order of a stack, of the points of a path, or the drawing order of the marks) and <c>detail</c> (it groups the points of a path) are accepted by every mark.</description></item>
///         <item><term><c>vl-type-geojson</c></term><description>the <c>type</c> of a supported channel.</description></item>
///         <item><term><c>vl-shape-path</c></term><description>a shape that is not a symbol name: the <c>shape</c> of a mark, the <c>value</c> of a <c>shape</c> channel, an entry of its <c>scale.range</c> or the <c>symbolType</c> of a legend (custom SVG path shapes are not drawn; a circle is).</description></item>
///         <item><term><c>vl-scale-interpolate-cubehelix</c></term><description>a <c>scale.interpolate</c> of the type <c>cubehelix</c> or <c>cubehelix-long</c> (RGB interpolation is used).</description></item>
///         <item><term><c>vl-scale-type-ignored</c> (warning)</term><description>a <c>scale.type</c> that the channel or the field type cannot use (the default type is used, as Vega-Lite does), and a quantitative field on a <c>shape</c> or <c>strokeDash</c> channel without a discretizing scale type (the scale is ordinal). A type that the renderer does not draw reports <c>vl-scale-&lt;type&gt;</c> only.</description></item>
///         <item><term><c>vl-encoding-impute</c>, <c>-bandposition</c>, <c>-bandposition-rect</c>, <c>-datum-datetime</c></term><description>that property of a supported channel (<c>vl-encoding-impute</c> is the <c>impute</c> of a channel other than <c>x</c> and <c>y</c>: the impute of a position channel is run by the pipeline, the impute of any other channel is ignored). <c>-bandposition</c> is a <c>bandPosition</c> on a field without a time unit (or with a bin; not the one of a stacked <c>theta</c> or <c>radius</c> of a text mark, which places the label in its slice); <c>-bandposition-rect</c> is a <c>bandPosition</c> or <c>timeUnitBandPosition</c> other than 0.5 on a bar or rect whose field has a time unit (0.5 is used); <c>-datum-datetime</c> is a date-time object as the <c>datum</c> of a channel (the channel is ignored).</description></item>
///         <item><term><c>vl-encoding-stack-impute-bin</c> (warning)</term><description>the dimension channel of a stacked <c>line</c>, <c>area</c> or <c>trail</c> that is binned and has stack-by fields (and whose stacked channel does not say <c>impute: null</c>): Vega-Lite fills the layers with zeros for the bins they lack, this renderer does not impute a binned dimension.</description></item>
///         <item><term><c>vl-timeunit-binned</c>, <c>vl-timeunit-maxbins</c></term><description>a <c>timeUnit</c> that is not applied: a <c>binned</c> unit (a name that starts with <c>binned</c>, or <c>binned: true</c>) in an encoding, a condition, a predicate or the <c>timeUnit</c> transform (the unit is ignored and the field is used as it is), and the <c>{"maxbins": n}</c> form without a unit in a predicate (ignored). The encoding and the transform apply <c>maxbins</c>.</description></item>
///         <item><term><c>vl-predicate-timeunit-part</c> (warning)</term><description>the <c>timeUnit</c> of a predicate (a filter or a condition test) whose unit has a <c>week</c> or <c>dayofyear</c> part; like Vega-Lite a predicate does not apply those parts.</description></item>
///         <item><term><c>vl-scale-nice-interval</c>, <c>vl-guide-axis-tickcount-interval</c> (warnings)</term><description>a time interval (a text or <c>{interval, step}</c>) as <c>scale.nice</c> or as the <c>tickCount</c> of an axis or legend on a scale that is not a time scale (it is ignored).</description></item>
///         <item><term><c>vl-scale-zero-ignored</c> (warning)</term><description>a <c>scale.zero</c> on a temporal field with a time or UTC scale or a color scale, where zero is not meaningful (it is ignored, as Vega-Lite does).</description></item>
///         <item><term><c>vl-encoding-condition</c></term><description>the <c>condition</c> of a channel that takes none (a position, secondary or order channel). The color, fill, stroke, opacity, fillOpacity, strokeOpacity, strokeWidth, strokeDash, size, shape and angle channels evaluate the <c>test</c> of a condition (and the <c>param</c> of a condition on a selection or a variable parameter) for every row. <c>vl-encoding-condition-param</c> was retired in Phase 12 and is never produced again.</description></item>
///         <item><term><c>vl-encoding-bin</c></term><description>the <c>bin</c> of a channel that cannot bin (<c>order</c> and the secondary channels), and <c>bin: "binned"</c> (data that is binned already) on a channel other than <c>x</c> and <c>y</c>, unless it is false or null. <c>aggregate</c>, <c>bin</c> (on <c>x</c>, <c>y</c>, <c>text</c> and the channels that have a scale), <c>stack</c> and <c>sort</c> are otherwise supported and are not reported.</description></item>
///         <item><term><c>vl-encoding-bin-extent-param</c></term><description>retired in Phase 12 and never produced again: a <c>bin.extent</c> that is a <c>{"param": ...}</c> object in an encoding sets the span that chooses the step and the domain of the scale (<see cref="Transforms.BinSettings.Span"/>).</description></item>
///         <item><term><c>vl-encoding-stack-nonlinear</c></term><description>an explicit <c>stack</c> (not false or null) of <c>x</c> or <c>y</c> whose <c>scale.type</c> is not <c>linear</c>; Vega-Lite does not stack on a log, pow, sqrt or symlog scale, so those marks are not stacked, and any other type is stacked on the linear scale.</description></item>
///         <item><term><c>vl-encoding-sort</c></term><description>a <c>sort</c> that is not applied: a channel shorthand (such as <c>"-y"</c>) or an <c>encoding</c> that names a channel the encoding does not have or that has neither a <c>field</c> nor an <c>aggregate</c>, and an object whose <c>op</c> is <c>values</c>, <c>exponential</c> or <c>exponentialb</c>. Ascending, descending, null and an array of values are always supported. The message names the channel that the sort refers to when that is the problem (the sort stays a report, not a specification error, because a channel can come from a parent encoding after normalization).</description></item>
///         <item><term><c>vl-encoding-sort-ignored</c> (warning)</term><description>a <c>sort</c> array, channel shorthand, or object with a <c>field</c>, <c>op</c> or <c>encoding</c> on a quantitative field whose scale is continuous (no discrete <c>scale.type</c>): there are no categories to order and the sort is ignored. <c>ascending</c>, <c>descending</c> and null are not reported.</description></item>
///         <item><term><c>vl-encoding-sort-mixed-types</c> (warning)</term><description>a <c>sort</c> array that mixes strings, numbers, booleans or null; produced by <see cref="SpecParser"/> at the array. Values are matched by kind (<c>1</c> does not position <c>"1"</c>), as Vega-Lite's strict comparison does. An object or array item of a <c>sort</c> array is a specification error instead (a date-time object is accepted on a temporal field).</description></item>
///         <item><term><c>vl-scale-&lt;type&gt;</c></term><description><c>scale.type</c>, for every type except linear, log, pow, sqrt, symlog, sequential, time, utc, band, point, ordinal, quantile, quantize, threshold and bin-ordinal.</description></item>
///         <item><term><c>vl-scale-property-&lt;name&gt;</c></term><description>a scale property other than type, domain, range, zero, nice, padding, reverse, paddingInner, paddingOuter, align, round, clamp, scheme, domainMid, domainMin, domainMax, domainRaw, rangeMin, rangeMax, interpolate, base, exponent and constant (<c>vl-scale-property-domainRaw</c> was retired in Phase 12 and is never produced again). An unknown color scheme name is a specification error of the scale resolver, not a report.</description></item>
///         <item><term><c>vl-offset-position</c>, <c>vl-offset-ranged</c></term><description>an <c>xOffset</c> or <c>yOffset</c> that is not drawn (the marks are drawn at the position of their <c>x</c> or <c>y</c> channel): <c>vl-offset-position</c> when the position channel is missing, is quantitative, is temporal without a time unit (Vega-Lite rejects it), has a time unit that cannot hold an offset (a unit whose smallest part is the day of the week, a binned unit, the <c>maxbins</c> form, a <c>rect</c> mark) on a position that is not discrete; a temporal position with a time unit is nested in the duration of its unit and drawn; <c>vl-offset-ranged</c> for a continuous (quantitative or temporal) offset of a bar or area, which Vega-Lite draws as a range from the start of the band. The offsets of the other marks are drawn: a discrete one by a band or point scale, a continuous one by a linear or time scale (a strip of points).</description></item>
///         <item><term><c>vl-guide-legend-titleorient</c>, <c>vl-guide-legend-clipheight</c></term><description>the <c>titleOrient</c> of a legend when it is not <c>top</c> (the title is drawn above the entries) and its <c>clipHeight</c>; legends themselves are drawn and are not reported.</description></item>
///         <item><term><c>vl-guide-encode</c></term><description><c>axis.encode</c> and <c>legend.encode</c>.</description></item>
///         <item><term><c>vl-guide-axis-&lt;name&gt;</c></term><description>an axis property from the list of unsupported axis properties (<c>labelBound</c>, <c>tickBand</c>, <c>tickExtra</c>, <c>position</c>, <c>translate</c>, <c>bandPosition</c>, <c>titleX</c>, <c>titleY</c>).</description></item>
///         <item><term><c>vl-format-type-custom</c></term><description><c>axis.formatType</c>, <c>legend.formatType</c>, the <c>header.formatType</c> of a facet field and the <c>formatType</c> of a <c>text</c> channel other than <c>number</c>, <c>time</c> and <c>utc</c> (a custom format type is not evaluated; the default format is used).</description></item>
///         <item><term><c>vl-expr-ref</c></term><description>an <c>{"expr": ...}</c> value that the renderer does not evaluate: at a property that Vega-Lite's schema does not allow one at (a width or height, a facet's field definitions), at a static site that reads <c>datum</c> (a static site is evaluated once, without a row), or in the <c>Config</c> of the render options (<see cref="VisitCallerConfig"/>). The static sites (<see cref="ExpressionReferenceSites"/>: title, padding, background, mark, axis, legend, header, scale and view properties, the <c>value</c> and <c>datum</c> of the other channels, and the root <c>config</c>), the row-bound sites and the <c>labelExpr</c> of axes, legends and headers are evaluated and are reported only for what their expression cannot do; a date result at a static site that takes none is the warning <c>vl-expr-ref</c> that the fold delivers (<see cref="ExpressionReferenceFolder"/>). <c>vl-expr-label</c> (the <c>axis.labelExpr</c>, <c>legend.labelExpr</c> and <c>header.labelExpr</c> that were not evaluated) was retired when label expressions were implemented and is never produced again.</description></item>
///         <item><term><c>vl-expr-function-&lt;name&gt;</c>, <c>vl-expr-view-state</c>, <c>vl-expr-construct-&lt;name&gt;</c></term><description>the expression text of a supported site (a calculate, a filter, a variable parameter or a row-bound expression reference): a call of a function outside the whitelist (the number format and color functions among them), a use of view state (<c>signal</c>, <c>event</c>, <c>item</c>, <c>width</c>, <c>height</c>), or a deferred operator (bitwise, shift, <c>in</c>). The construct evaluates to null; the position is that of the expression text and the message names the offset inside it.</description></item>
///         <item><term><c>vl-transform-&lt;name&gt;</c></term><description>every entry of <c>transform</c> that the renderer does not compute: the transforms of later phases (<c>density</c>, <c>extent</c>, <c>loess</c>, <c>quantile</c>, <c>regression</c>; "not supported yet"). <c>calculate</c>, <c>filter</c>, <c>fold</c>, <c>flatten</c>, <c>sample</c>, <c>pivot</c>, <c>lookup</c>, <c>aggregate</c>, <c>joinaggregate</c>, <c>window</c>, <c>bin</c>, <c>stack</c>, <c>impute</c> and <c>timeUnit</c> of any view (the single view, a layered view and its layers, and the views of a <c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>facet</c> or <c>repeat</c>) are computed and not reported. The Phase 10 message "The '&lt;name&gt;' transform is not applied: the view is a '&lt;composition&gt;' composition, which is not supported yet." was retired in Phase 11: every composition is drawn, so no transform is left unapplied by one.</description></item>
///         <item><term><c>vl-transform-pivot-op</c>, <c>-lookup-param</c></term><description>the parts of a computed transform that are not supported: a pivot <c>op</c> other than sum and count (sum is used) and a lookup in the data of a parameter (not applied; it needs the final table of another view filtered by the selection). <c>vl-transform-filter-param</c> (a filter predicate on a selection parameter) and <c>vl-transform-bin-extent-param</c> (a <c>bin</c> whose <c>extent</c> is a parameter) were retired in Phase 12 and are never produced again: the filter evaluates the selection, and the extent sets the span that chooses the bin step.</description></item>
///         <item><term><c>vl-composition-property-ignored</c> (warning)</term><description>a <c>width</c>, <c>height</c> or <c>autosize</c> (other than <c>pad</c>) of a concatenation, a facet or a repeat of rows or columns (only the views inside have a size), the <c>"container"</c> size of a view inside one, and the <c>padding</c>, <c>background</c>, <c>config</c>, <c>datasets</c> or <c>autosize</c> of a view inside one: Vega-Lite ignores them and only the top-level view decides them. The composition is drawn padded (<c>autosize: pad</c>). A <c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>repeat</c> and <c>facet</c> are drawn and not reported: <c>vl-composition-concat</c>, <c>-hconcat</c>, <c>-vconcat</c>, <c>-repeat</c> and <c>-facet</c> were retired in Phase 11 and are never produced (<c>vl-composition-layer</c> was retired in Phase 10); the views nested in a composition are examined too.</description></item>
///         <item><term><c>vl-channel-row</c>, <c>-column</c>, <c>-facet</c></term><description>a <c>row</c>, <c>column</c> or <c>facet</c> channel of a unit view that cannot be faceted: a layer of a layered view, a layered view whose encoding holds the channel, or the <c>spec</c> of a <c>facet</c> (Vega-Lite facets only a unit view that is the root, a child of a concatenation or a copy of a repeat; the channel is ignored there, use the <c>facet</c> operator). The channels of a unit view that is faceted are drawn and not reported.</description></item>
///         <item><term><c>vl-facet-field-transform</c>, <c>vl-facet-sort</c>, <c>vl-header-orient</c></term><description>the parts of the definition of a facet field (a <c>facet</c> operator's field or <c>row</c> and <c>column</c> fields, or the <c>row</c>, <c>column</c> and <c>facet</c> channel of a unit view) that are not drawn: a <c>bin</c>, <c>timeUnit</c> or <c>aggregate</c> of the field is not applied (the raw value of the field is the cell; <c>vl-facet-field-transform</c>, at the property); a <c>sort</c> by a field and an operation (an object) orders the cells by the facet field in ascending order (<c>vl-facet-sort</c>); a <c>header.labelOrient</c> or <c>header.titleOrient</c> on a side that the header does not draw on (the default side is used; <c>vl-header-orient</c>): a row honors <c>left</c> and <c>right</c>, a column <c>top</c> and <c>bottom</c>, the label of a wrapped facet (the title of its cells) any side and its title <c>top</c> and <c>bottom</c>. A custom <c>header.formatType</c> reports <c>vl-format-type-custom</c>; <c>header.labelExpr</c> is evaluated.</description></item>
///         <item><term><c>vl-concat-axis-shared</c>, <c>vl-composition-columns-ignored</c>, <c>vl-repeat-ref-unresolved</c> (warnings)</term><description>produced by the renderer from the view tree, not by the catalog (so each is reported once, from the tree that is drawn): <c>resolve.axis.x</c> or <c>y</c> set to <c>shared</c> on a concatenation (the children keep their own axes, as Vega-Lite does), <c>columns</c> together with a <c>row</c> or <c>column</c> mapping of a facet or repeat (ignored), and a <c>{"repeat": ...}</c> reference that the enclosing repeat does not define (the channel is dropped).</description></item>
///         <item><term><c>vl-layer-property-ignored</c> (warning)</term><description>a <c>width</c>, <c>height</c>, <c>autosize</c>, <c>padding</c>, <c>background</c>, <c>config</c>, <c>datasets</c> or <c>title</c> of a view inside a layered view (a layer or a nested layered view): only the top-level view decides them.</description></item>
///         <item><term><c>vl-layer-scale-conflict</c> (warning)</term><description>produced by the renderer, not by the catalog: views (the layers of a layered view, the children of a concatenation) whose scales of a shared channel cannot be merged (a continuous with a discrete scale, a linear with a logarithmic or a time scale) keep scales of their own; reported at the channel of the view that was left out.</description></item>
///         <item><term><c>vl-param-variable</c>, <c>vl-param-selection-layer</c>, <c>-id</c>, <c>-animation</c>, <c>-projection</c></term><description>the parameters that are not drawn as Vega-Lite does. A variable parameter of a nested view reports <c>vl-param-variable</c>: Vega-Lite compiles only the variable parameters of the top-level view into signals, so a nested one is ignored. The variable parameters of the root view are computed and are not reported. A selection parameter (it has <c>select</c>; the legacy top-level <c>selection</c> is the same) is drawn in its initial state (<see cref="Transforms.SelectionModel"/>), with <c>bind: "scales"</c> (the domain of the scales) and <c>bind: "legend"</c> (the dimming of the legend entries) drawn, and the input-widget <c>bind</c>, <c>on</c>, <c>clear</c>, <c>toggle</c>, <c>nearest</c>, <c>translate</c> and <c>zoom</c> ignored without a report; the parts that are not modeled are reported at the declaration: <c>vl-param-selection-layer</c> (a selection that two or more layers of a layered view receive: Vega-Lite compiles duplicate signals that Vega rejects; it is applied as an empty selection), <c>vl-param-selection-id</c> (a point selection that names no fields or encodings and has a <c>value</c>: it selects rows by identity, which is not modeled; applied as empty), and <c>vl-param-selection-animation</c> (<c>on</c> a timer, or the <c>time</c> encoding). <c>vl-param-selection-projection</c> is a warning produced by the selection model, not by the catalog (it needs the encodings of the unit views). <c>vl-param-selection</c> was retired in Phase 12 and is never produced again.</description></item>
///         <item><term><c>vl-projection</c></term><description>the <c>projection</c> of a view.</description></item>
///         <item><term><c>vl-layout-fit-step</c> (warning)</term><description>produced by the renderer's layout, not by the catalog (it needs the scales): an <c>autosize</c> of <c>fit</c>, <c>fit-x</c> or <c>fit-y</c> (also the one that <c>"container"</c> implies) whose width or height is sized by a step, a <c>{"step": n}</c> or a missing size on a discrete scale; Vega-Lite drops the fit for that dimension (<c>fit</c> becomes <c>fit-y</c> or <c>fit-x</c>, or <c>pad</c> for both; <c>fit-x</c> with a step width becomes <c>fit-y</c>, as Vega-Lite does) and so does the renderer. <c>fit</c>, <c>fit-x</c> and <c>fit-y</c> are drawn for a single and a layered view and are not reported: <c>vl-layout-autosize-fit</c>, <c>-fit-x</c> and <c>-fit-y</c> (the family <c>vl-layout-autosize-&lt;type&gt;</c>) were retired in Phase 11 and are never produced. A fit or a <c>"container"</c> size of a composition is reported as <c>vl-composition-property-ignored</c>.</description></item>
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
    private static readonly HashSet<string> SupportedMarks = ["bar", "rect", "point", "circle", "square", "rule", "tick", "line", "area", "trail", "text", "arc", "boxplot", "errorbar", "errorband"];

    /// <summary>The channels that the renderer draws; any other channel is reported unless it is ignored.</summary>
    private static readonly HashSet<string> SupportedChannels =
    [
        "x", "y", "xOffset", "yOffset", "color", "fill", "stroke", "size", "opacity", "shape", "strokeWidth", "strokeDash", "fillOpacity", "strokeOpacity", "angle", "text", "theta", "radius", "theta2", "radius2",
    ];

    /// <summary>The channels that are accepted and ignored without a report (interaction, accessibility and linking).</summary>
    private static readonly HashSet<string> IgnoredChannels = ["tooltip", "href", "description", "key"];

    /// <summary>The geographic position channels, which all report <c>vl-channel-geo</c>.</summary>
    private static readonly HashSet<string> GeoChannels = ["longitude", "latitude", "longitude2", "latitude2"];

    /// <summary>The channels that get a legend when they are driven by a field.</summary>
    private static readonly HashSet<string> LegendChannels = ["color", "fill", "stroke", "size", "opacity", "shape", "strokeWidth", "strokeDash"];

    /// <summary>The marks that draw a range between a position and its secondary channel (<c>x2</c> and <c>y2</c>).</summary>
    private static readonly HashSet<string> RangedMarks = ["bar", "rect", "rule", "area"];

    /// <summary>The channels on which <c>bin</c> is applied: the position channels and every channel that has a scale (a <c>bin: "binned"</c> is read on the position channels only).</summary>
    private static readonly HashSet<string> BinnedChannels =
    [
        "x", "y", "color", "fill", "stroke", "size", "opacity", "shape", "strokeWidth", "strokeDash", "fillOpacity", "strokeOpacity", "angle", "text",
    ];

    /// <summary>The scale types that the renderer implements.</summary>
    private static readonly HashSet<string> SupportedScaleTypes = ["linear", "log", "pow", "sqrt", "symlog", "sequential", "band", "point", "ordinal", "quantile", "quantize", "threshold", "bin-ordinal", "time", "utc"];

    /// <summary>The scale types that order categories (a quantitative field with one of them is not on a continuous scale).</summary>
    private static readonly HashSet<string> DiscreteScaleTypes = ["band", "point", "ordinal", "bin-ordinal", "quantile", "quantize", "threshold"];

    /// <summary>The scale properties that the renderer implements (<c>interpolate</c> except for the cubehelix types, see the scale visit).</summary>
    private static readonly HashSet<string> SupportedScaleProperties =
    [
        "type", "domain", "range", "zero", "nice", "padding", "reverse", "paddingInner", "paddingOuter", "align", "round", "clamp", "scheme",
        "domainMid", "domainMin", "domainMax", "domainRaw", "rangeMin", "rangeMax", "interpolate", "base", "exponent", "constant",
    ];

    /// <summary>The axis properties that the renderer does not implement; each reports <c>vl-guide-axis-&lt;name&gt;</c>.</summary>
    private static readonly HashSet<string> UnsupportedAxisProperties =
    [
        "labelBound", "tickBand", "tickExtra", "position", "translate", "bandPosition", "titleX", "titleY",
    ];

    /// <summary>The properties of a view in which an <c>{"expr": ...}</c> value is looked for.</summary>
    private static readonly HashSet<string> ExpressionScopes = ["title", "width", "height", "padding", "background", "view", "mark", "encoding", "facet"];

    /// <summary>The message for an expression reference inside a selection, where Vega-Lite's schema allows none (the brush <c>mark</c>, the events, the projection and the initial value take plain values).</summary>
    private const string SelectionReferenceMessage =
        "Vega-Lite's schema does not allow an expression reference in a selection (the brush mark, the events, the projection and the initial value take plain values); the reference is ignored and the property keeps its default.";

    /// <summary>The properties of a view inside a concatenation, a facet or a repeat that only the top-level view decides: a nested view's own value is ignored (<c>vl-composition-property-ignored</c>).</summary>
    private static readonly string[] NestedIgnoredProperties = ["padding", "background", "config", "datasets", "autosize"];

    /// <summary>The properties that name a concatenation in a view.</summary>
    private static readonly string[] ConcatProperties = ["concat", "hconcat", "vconcat"];

    /// <summary>The faceting channels of an encoding: a unit view that has one is a facet unless it is a layer or the <c>spec</c> of a facet.</summary>
    private static readonly string[] FacetChannels = ["row", "column", "facet"];
    /// <summary>The properties of a view in a layered view that only the top-level view decides: a layer's own value is ignored (<c>vl-layer-property-ignored</c>).</summary>
    private static readonly string[] LayerIgnoredProperties = ["width", "height", "autosize", "padding", "background", "config", "datasets", "title"];

    /// <summary>
    ///     Lists the recognized-but-unsupported constructs of a specification.
    /// </summary>
    /// <param name="tree">The parsed specification. Must not be null.</param>
    /// <param name="config">
    ///     The effective configuration (defaults, specification and the caller's configuration), or null to read the configuration of the specification alone. The decisions that depend on
    ///     the configuration of the renderer (whether a path mark has a point overlay, which says whether a <c>shape</c> is drawn) use it, so a rendering with a caller's configuration
    ///     reports what it draws.
    /// </param>
    /// <returns>The reports in document order; empty when the specification uses only supported constructs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tree"/> is null.</exception>
    public static IReadOnlyList<FeatureReport> Visit(SpecTree tree, ResolvedConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var walker = new Walker(tree, config);
        walker.Run();

        // LINQ's ordering is stable, so reports at one position keep the order in which they were found; a finding that several layers share (a channel of the encoding that
        // the layers inherit) is reported once
        return walker.Reports
            .DistinctBy(r => (r.Id, r.Location.Line, r.Location.Column, r.Message, r.Kind))
            .OrderBy(r => r.Location.Line).ThenBy(r => r.Location.Column).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Lists the expression references (<c>{"expr": ...}</c>) in the configuration text of the caller's render options.
    /// </summary>
    /// <remarks>
    ///     The configuration of the caller is merged over the defaults as it is: nothing evaluates an expression in it, so a reference there would be read as an object where a
    ///     value is expected and its property would silently keep its default. Each reference is reported, with the JSON pointer inside the text of the caller's configuration in the message
    ///     (the text has no position in the specification, so the location is unknown).
    /// </remarks>
    /// <param name="callerConfig">The configuration text, which the render options validated as a JSON object, or null.</param>
    /// <returns>One report for each reference, in document order; empty when there is none.</returns>
    public static IReadOnlyList<FeatureReport> VisitCallerConfig(string? callerConfig)
    {
        if (string.IsNullOrEmpty(callerConfig))
        {
            return [];
        }

        // The text was validated as a JSON object of bounded depth when the options were created; an explicit stack keeps the walk independent of the call stack anyway
        var reports = new List<FeatureReport>();
        using var document = System.Text.Json.JsonDocument.Parse(callerConfig, new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 });
        var pending = new Stack<(System.Text.Json.JsonElement Element, string Pointer)>();
        pending.Push((document.RootElement, string.Empty));
        while (pending.Count > 0)
        {
            var (element, pointer) = pending.Pop();
            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (element.TryGetProperty("expr", out var expression) && expression.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    reports.Add(new FeatureReport(
                        "vl-expr-ref",
                        $"An expression reference in the Config of the render options (at {(pointer.Length == 0 ? "the root" : pointer)}) is not evaluated; the property keeps its default (the config of the specification is evaluated).",
                        default,
                        FeatureReportKind.UnsupportedFeature));
                    continue;
                }

                foreach (var property in element.EnumerateObject().Reverse())
                {
                    pending.Push((property.Value, pointer + "/" + SpecNode.EscapePointerSegment(property.Name)));
                }
            }
            else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var index = element.GetArrayLength();
                foreach (var item in element.EnumerateArray().Reverse())
                {
                    index--;
                    pending.Push((item, pointer + "/" + index.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        return reports;
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

        /// <summary>The findings that <see cref="ReportOnce"/> has recorded, by identifier and node.</summary>
        private readonly HashSet<(string Id, SpecNode Node)> _reported = [];

        /// <summary>The mark type of the view whose encoding is visited.</summary>
        private string _markType = string.Empty;

        /// <summary>The stack decision of the view whose encoding is visited (decided when it is first needed), or null outside an encoding.</summary>
        private Lazy<StackDecision?>? _stack;

        /// <summary>The effective configuration, or null when only the configuration of the specification is read.</summary>
        private readonly ResolvedConfig? _config;

        /// <summary>The expression texts of the static sites, which are evaluated once without a row.</summary>
        private readonly HashSet<SpecNode> _staticTexts;

        /// <summary>The declarations of the selections that two or more layers receive, found on first use.</summary>
        private HashSet<SpecNode>? _duplicateSelections;

        /// <summary>
        ///     Initializes a walker.
        /// </summary>
        /// <param name="tree">The specification.</param>
        /// <param name="config">The effective configuration, or null.</param>
        public Walker(SpecTree tree, ResolvedConfig? config)
        {
            _tree = tree;
            _config = config;
            _staticTexts = new HashSet<SpecNode>(tree.Expressions.StaticReferences.Select(r => r.Text), ReferenceEqualityComparer.Instance);
        }

        /// <summary>
        ///     Reads the <c>point</c> or <c>line</c> that the configuration gives a mark type: the effective configuration when there is one, else the specification's own.
        /// </summary>
        /// <param name="markType">The mark type.</param>
        /// <param name="overlay">The property, <c>point</c> or <c>line</c>.</param>
        /// <returns>The value, or null.</returns>
        private ConfigNode? ConfiguredOverlay(string markType, string overlay)
        {
            if (_config is not null)
            {
                return _config.VegaLite[markType]?[overlay];
            }

            return _tree.Root["config"]?[markType]?[overlay] is { } node ? ConfigNode.FromSpec(node) : null;
        }

        /// <summary>Gets the reports found so far, in the order they were found.</summary>
        public List<FeatureReport> Reports { get; } = [];

        /// <summary>
        ///     Builds an identifier from an area and a name taken from the specification.
        /// </summary>
        /// <param name="area">The area, such as <c>mark</c>.</param>
        /// <param name="name">The name; it is lower-cased with the invariant culture.</param>
        /// <returns>The identifier, such as <c>vl-mark-geoshape</c>.</returns>
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

            VisitConfigLabelExpressions();
            // The encoding of a layered view and of each layer is merged into the encoding of every layer below it, which is what the layer's mark draws: it is visited with
            // the mark of that layer (a channel that several layers inherit is found once for each, and reported once)
            foreach (var unit in LayerUnits().Where(u => !ReferenceEquals(u.Unit, u.Source)).Select(u => u.Unit!))
            {
                if (unit["encoding"] is { } encoding)
                {
                    VisitEncoding(unit, encoding);
                }
            }
        }

        /// <summary>
        ///     Determines whether a view is a unit view that Vega-Lite facets because it has a <c>row</c>, <c>column</c> or <c>facet</c> channel: not a layer, not a layered view and not the
        ///     <c>spec</c> of a facet (the root, a child of a concatenation and a copy of a repeat are).
        /// </summary>
        /// <param name="view">The view object.</param>
        /// <returns><see langword="true"/> for a faceted unit view.</returns>
        private static bool IsFacetedUnit(SpecNode view) =>
            view["layer"] is null
            && view["encoding"] is { IsObject: true } encoding
            && FacetChannels.Any(channel => encoding[channel] is { IsObject: true })
            && !IsLayerElement(view)
            && !IsFacetSpec(view);

        /// <summary>
        ///     Determines whether a view is the <c>spec</c> of a facet.
        /// </summary>
        /// <param name="view">The view object.</param>
        /// <returns><see langword="true"/> for the <c>spec</c> of a view that has a <c>facet</c>.</returns>
        private static bool IsFacetSpec(SpecNode view) => view is { Name: "spec", Parent: { IsObject: true } owner } && owner["facet"] is not null;

        /// <summary>
        ///     Determines whether a view is an element of the <c>layer</c> array of another view.
        /// </summary>
        /// <param name="view">The view object.</param>
        /// <returns><see langword="true"/> for a layer.</returns>
        private static bool IsLayerElement(SpecNode view) => view.Parent is { IsArray: true, Name: "layer" };

        /// <summary>
        ///     Determines whether a view is a concatenation, a facet, or a repeat of rows, columns or values (a repeat of <c>layer</c> values only is a layered view).
        /// </summary>
        /// <param name="view">The view object.</param>
        /// <returns><see langword="true"/> for a composition.</returns>
        private static bool IsComposition(SpecNode view) =>
            ConcatProperties.Any(name => view[name] is not null)
            || view["facet"] is not null
            || view["repeat"] is { } repeat && (repeat.IsArray || repeat["row"] is not null || repeat["column"] is not null);

        /// <summary>
        ///     Determines whether a view is a child of a concatenation or the <c>spec</c> of a facet or of a repeat of rows, columns or values.
        /// </summary>
        /// <param name="view">The view object.</param>
        /// <returns><see langword="true"/> for a view that a composition holds.</returns>
        private static bool IsInComposition(SpecNode view) =>
            view.Parent is { IsArray: true, Name: "concat" or "hconcat" or "vconcat" }
            || view is { Name: "spec", Parent: { IsObject: true } owner } && (owner["facet"] is not null || IsComposition(owner));

        /// <summary>
        ///     Lists the layers of the specification (the unit views inside layered views, concatenations, facets and repeats; a facet's template once) in document order.
        /// </summary>
        /// <returns>The layers; empty when the specification has none or its layers are nested more deeply than the largest limit allows (the render fails then).</returns>
        private List<LayerNode> LayerUnits() => [.. LayerTreeOrNull()?.Units ?? []];

        /// <summary>
        ///     Builds the layer tree of the specification for the findings that need to know which unit views a declaration applies to.
        /// </summary>
        /// <returns>The tree; null when the specification is nested more deeply than the largest limit allows (the render fails then).</returns>
        private LayerTree? LayerTreeOrNull()
        {
            try
            {
                // Bounded, so that a repeat is not expanded without limit here before the document enforces its own MaxViews
                return SpecNormalizer.Layers(_tree, new VegaLiteLimits { MaxViews = 100_000, MaxCompositionDepth = 64 });
            }
            catch (VegaLiteRenderException)
            {
                return null;
            }
        }

        /// <summary>
        ///     Finds the selections that Vega-Lite compiles to duplicate signals: a selection that two or more units of one layered view receive.
        /// </summary>
        /// <returns>The declaration nodes.</returns>
        private HashSet<SpecNode> DuplicateSelections()
        {
            if (_duplicateSelections is null)
            {
                _duplicateSelections = [];
                if (LayerTreeOrNull() is { } layers)
                {
                    foreach (var assignment in SelectionDeclarations.Assign(layers).Where(a => a.IsDuplicate))
                    {
                        _duplicateSelections.Add(assignment.Declaration);
                    }
                }
            }

            return _duplicateSelections;
        }

        /// <summary>
        ///     Reports what the renderer does not model of a selection: an animation, a projection over the identity of the rows with an initial value, and a selection that Vega rejects.
        /// </summary>
        /// <remarks>
        ///     The interaction of a selection (<c>on</c>, <c>clear</c>, <c>toggle</c>, <c>nearest</c>, <c>translate</c>, <c>zoom</c>, the input widgets of <c>bind</c>) is ignored without a report: a static chart is the state before any event.
        /// </remarks>
        /// <param name="declaration">The parameter object or the legacy selection entry.</param>
        private void VisitSelection(SpecNode declaration)
        {
            VisitSelectionReferences(declaration);
            var options = SelectionDeclarations.OptionsOf(declaration);
            var timer = options?["on"] is { } on && (on.StringValue?.Contains("timer", StringComparison.Ordinal) == true || on["type"]?.StringValue == "timer");
            if (timer || options?["encodings"] is { IsArray: true } encodings && encodings.Items.Any(e => e.StringValue == "time"))
            {
                Unsupported("vl-param-selection-animation", "An animation selection (a timer, or the 'time' encoding) is not supported; the selection is drawn in its initial state, without the animation.", declaration);
            }

            if (SelectionDeclarations.UsesRowIdentity(declaration, _config) && SelectionDeclarations.ValueOf(declaration) is { IsNull: false })
            {
                Unsupported(
                    "vl-param-selection-id",
                    "A point selection that names no fields or encodings selects rows by their identity, which is not modeled; its initial value is ignored and the selection is empty.",
                    declaration);
            }

            if (DuplicateSelections().Contains(declaration))
            {
                Unsupported(
                    "vl-param-selection-layer",
                    "The selection is declared for two or more layers of a layered view, which Vega-Lite compiles to duplicate signals that Vega rejects; declare it in one layer or name the layer in 'views'. It is applied as an empty selection.",
                    declaration);
            }
        }

        /// <summary>
        ///     Reports each <c>{"expr": ...}</c> inside the declaration of a selection.
        /// </summary>
        /// <remarks>
        ///     Vega-Lite's schema (6.5.0) allows no expression reference anywhere in a selection: <c>BrushConfig</c> (<c>fill</c>, <c>fillOpacity</c>, <c>stroke</c> and the other properties of the brush),
        ///     <c>PointSelectionConfig</c> and <c>IntervalSelectionConfig</c> (<c>on</c>, <c>clear</c>, <c>fields</c>, <c>encodings</c>, <c>translate</c>, <c>zoom</c>, ...) and the initial <c>value</c> take
        ///     plain values, a stream or a mapping, and Vega-Lite copies the value to a Vega mark or event stream as it is, where an object is invalid. The reference is therefore not a static site: it is
        ///     not evaluated, and the brush keeps the defaults for the properties it would have set.
        /// </remarks>
        /// <param name="declaration">The parameter object or the legacy selection entry.</param>
        private void VisitSelectionReferences(SpecNode declaration)
        {
            var pending = new Stack<SpecNode>();
            foreach (var property in declaration.Properties)
            {
                pending.Push(property);
            }

            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node.IsObject && node["expr"] is { IsString: true })
                {
                    Unsupported("vl-expr-ref", SelectionReferenceMessage, node);
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
        ///     Tells whether a node lies inside <c>config.selection</c> of the specification.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns><see langword="true"/> when an ancestor is the <c>selection</c> of the <c>config</c>.</returns>
        private static bool IsInConfigSelection(SpecNode node)
        {
            for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            {
                if (parent.Name == "selection" && parent.Parent?.Name == "config")
                {
                    return true;
                }
            }

            return false;
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
            var isRoot = ReferenceEquals(view, _tree.Root);
            var composition = IsComposition(view);

            // The encoding of a view that is drawn through layers is visited with the layer it is merged into
            var layered = view["layer"] is not null || IsLayerElement(view);
            if (IsLayerElement(view))
            {
                VisitLayerProperties(view);
            }

            VisitCompositionProperties(view, composition);

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
                        if (!layered)
                        {
                            VisitEncoding(view, property);
                        }

                        break;
                    case "transform":
                        VisitTransforms(property);
                        break;
                    case "params":
                        VisitParams(property, isRoot);
                        break;
                    case "selection":
                        foreach (var entry in property.Properties.Where(p => p.IsObject))
                        {
                            VisitSelection(entry);
                        }

                        break;
                    case "projection":
                        Unsupported("vl-projection", "Map projections are not supported.", property);
                        break;
                    case "facet":
                        VisitFacet(property);
                        break;
                    default:
                        break;
                }

                if (ExpressionScopes.Contains(name) || (name == "config" && isRoot))
                {
                    VisitExpressions(property);
                }
            }
        }

        /// <summary>
        ///     Warns about the properties that Vega-Lite ignores in a composition and in the views it holds.
        /// </summary>
        /// <remarks>
        ///     A concatenation, a facet and a repeat have no size, no autosize and no data rectangle of their own: the views inside have them (a <c>width</c> or <c>height</c> of the composition, and an
        ///     <c>autosize</c> other than <c>pad</c>, are ignored and the composition is drawn padded). A view inside one has its own size, but not a <c>"container"</c> size, a padding, a background, a
        ///     configuration, datasets or an autosize: only the top-level view decides those.
        /// </remarks>
        /// <param name="view">The view.</param>
        /// <param name="composition">Whether the view is a concatenation, a facet or a repeat.</param>
        private void VisitCompositionProperties(SpecNode view, bool composition)
        {
            var nested = IsInComposition(view);
            if (composition)
            {
                foreach (var name in new[] { "width", "height" })
                {
                    if (view[name] is { } size)
                    {
                        Warn("vl-composition-property-ignored", $"The '{name}' of a composition is ignored; the views inside have their own size.", size);
                    }
                }

                if (!nested && view["autosize"] is { } autosize && (autosize.IsObject ? autosize["type"] : autosize) is { IsString: true } type && type.StringValue != "pad")
                {
                    Warn("vl-composition-property-ignored", $"The autosize type '{type.StringValue}' of a composition is ignored; a composition is padded.", type);
                }
            }
            else if (nested)
            {
                foreach (var name in new[] { "width", "height" })
                {
                    if (view[name] is { IsString: true } size && size.StringValue == "container")
                    {
                        Warn("vl-composition-property-ignored", $"The \"container\" {name} of a view inside a composition is ignored; the view has its default size.", size);
                    }
                }
            }

            if (nested)
            {
                foreach (var name in NestedIgnoredProperties)
                {
                    if (view[name] is { } property)
                    {
                        Warn("vl-composition-property-ignored", $"The '{name}' of a view inside a composition is ignored; set it on the top-level view.", property);
                    }
                }
            }
        }

        /// <summary>
        ///     Warns about the properties of a view in a layered view that only the top-level view decides.
        /// </summary>
        /// <param name="view">The view that is a layer or a layered view inside a layered view.</param>
        private void VisitLayerProperties(SpecNode view)
        {
            foreach (var name in LayerIgnoredProperties)
            {
                if (view[name] is { } property)
                {
                    Warn("vl-layer-property-ignored", $"The '{name}' of a view inside a layer is ignored; set it on the top-level view.", property);
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
            else
            {
                if (mark["shape"] is { IsString: true } shape)
                {
                    VisitShapeName(shape);
                }

                VisitMarkInvalid(MarkSetting(mark, type, "invalid"));
                if (type is "line" or "area" && MarkSetting(mark, type, "interpolate") is { IsString: true } interpolate)
                {
                    VisitMarkInterpolate(type, interpolate);
                }
            }
        }

        /// <summary>
        ///     Finds a property that the specification gives a mark, the way the mark builder reads it: in the mark definition, else in the configuration (the
        ///     <c>config.style</c> entries named by the mark type and by the <c>style</c> of the mark, the later ones winning, then <c>config.&lt;mark&gt;</c>, then <c>config.mark</c>).
        /// </summary>
        /// <remarks>
        ///     A value that comes from the configuration is reported at the configuration, not at the mark that uses it; the same value used by several marks is reported once.
        ///     Only the configuration of the specification is visible here; the configuration that a caller passes to the renderer is not part of the tree.
        /// </remarks>
        /// <param name="mark">The mark value (a mark type or a mark definition).</param>
        /// <param name="type">The mark type, or null.</param>
        /// <param name="name">The property.</param>
        /// <returns>The value node, or null when the specification gives none.</returns>
        private SpecNode? MarkSetting(SpecNode mark, string? type, string name)
        {
            if (mark[name] is { } own)
            {
                return own;
            }

            if (_tree.Root["config"] is not { IsObject: true } config)
            {
                return null;
            }

            SpecNode? found = null;
            if (config["style"] is { IsObject: true } styles)
            {
                var names = new List<string>();
                if (type is not null)
                {
                    names.Add(type);
                }

                if (mark["style"] is { } style)
                {
                    if (style.IsString)
                    {
                        names.Add(style.StringValue!);
                    }
                    else if (style.Kind == SpecNodeKind.Array)
                    {
                        names.AddRange(style.Items.Where(i => i.IsString).Select(i => i.StringValue!));
                    }
                }

                foreach (var styleName in names)
                {
                    found = styles[styleName]?[name] ?? found;
                }
            }

            return found ?? (type is null ? null : config[type]?[name]) ?? config["mark"]?[name];
        }

        /// <summary>
        ///     Visits the <c>interpolate</c> of a line or an area: a name that Vega-Lite does not know is drawn as <c>linear</c> (only the configuration can hold one, the parser rejects it in a mark),
        ///     and <c>bundle</c> cannot draw an area.
        /// </summary>
        /// <param name="type">The mark type, <c>line</c> or <c>area</c>.</param>
        /// <param name="interpolate">The string value.</param>
        private void VisitMarkInterpolate(string type, SpecNode interpolate)
        {
            if (!SpecParser.IsInterpolateName(interpolate.StringValue!))
            {
                ReportOnce(
                    "vl-mark-interpolate-unknown",
                    $"The interpolation '{interpolate.StringValue}' is not one of Vega-Lite's interpolations; the 'linear' interpolation is used.",
                    interpolate,
                    FeatureReportKind.UnsupportedFeature);
            }
            else if (type == "area" && interpolate.StringValue == "bundle")
            {
                ReportOnce(
                    "vl-mark-interpolate-bundle-area",
                    "The 'bundle' interpolation cannot draw an area (d3-shape gives it no area outline); the area is drawn with the 'linear' interpolation.",
                    interpolate,
                    FeatureReportKind.UnsupportedFeature);
            }
        }

        /// <summary>
        ///     Records a finding once for a node (a configuration value that several marks use is reported once).
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="message">The description.</param>
        /// <param name="node">The node the finding is about.</param>
        /// <param name="kind">The kind of finding.</param>
        private void ReportOnce(string id, string message, SpecNode node, FeatureReportKind kind)
        {
            if (_reported.Add((id, node)))
            {
                Reports.Add(new FeatureReport(id, message, _tree.Locate(node), kind));
            }
        }

        /// <summary>
        ///     Visits the <c>invalid</c> property of a mark: <c>show</c> (and null) are not drawn, and the modes that filter the scale domains keep the rows in the domains.
        /// </summary>
        /// <param name="invalid">The value of the property (from the mark or from the configuration), or null when there is none.</param>
        private void VisitMarkInvalid(SpecNode? invalid)
        {
            if (invalid is null)
            {
                return;
            }

            if (invalid.IsNull || invalid is { IsString: true, StringValue: "show" })
            {
                ReportOnce("vl-mark-invalid-show", "The 'show' invalid mode (and null) is not supported: invalid values are not drawn at a fallback position; the default mode applies.", invalid, FeatureReportKind.UnsupportedFeature);
            }
            else if (invalid is { IsString: true, StringValue: "filter" or "break-paths-filter-domains" })
            {
                ReportOnce("vl-mark-invalid-domains", $"The invalid mode '{invalid.StringValue}' should leave the invalid rows out of the scale domains; the domains of this renderer always include them.", invalid, FeatureReportKind.Warning);
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
            var stackDecision = new Lazy<StackDecision?>(() => StackRules.Decide(markType, ScaleResolver.InferOrient(markType, mark?["orient"]?.AsString(), encoding), encoding));
            _stack = stackDecision;
            var is2D = markType == "errorband" && encoding["x"] is { IsNull: false } && encoding["y"] is { IsNull: false };
            // The overlays of a path mark: the configuration is the effective one when the caller gave one (a rendering, see Visit), else that of the specification alone
            var pointOverlay = SpecNormalizer.HasPointOverlay(markType, _mark, encoding, ConfiguredOverlay(markType, "point"));
            var lineOverlay = SpecNormalizer.HasLineOverlay(markType, _mark, ConfiguredOverlay(markType, "line"));
            var drawnAs = markType switch
            {
                "boxplot" => "point",
                "errorbar" => "rule",
                "errorband" => is2D ? "area" : "rect",
                _ => markType,
            };
            if (markType is "boxplot" or "errorbar" or "errorband")
            {
                VisitCompositeGrouping(markType, mark, encoding);
            }

            foreach (var channel in encoding.Properties)
            {
                var name = channel.Name!;
                if (IgnoredChannels.Contains(name))
                {
                    continue;
                }

                if (FacetChannels.Contains(name))
                {
                    // A unit view that is faceted: the field definition decides its cells and their headers; anywhere else (a layer, the spec of a facet) the channel facets nothing
                    if (IsFacetedUnit(view))
                    {
                        VisitFacetFieldDefinition(name, channel);
                    }
                    else
                    {
                        Unsupported(Id("channel", name), $"The '{name}' channel only facets a unit view that is not a layer or the spec of a facet; it is ignored here (use the facet operator).", channel);
                    }

                    continue;
                }

                if (GeoChannels.Contains(name))
                {
                    Unsupported("vl-channel-geo", $"The geographic channel '{name}' is not supported.", channel);
                }
                else if (name is "x2" or "y2")
                {
                    // A range is drawn by a bar, a rect, a rule or an area that has the position channel it extends; an errorbar and an errorband take it as the end of the range
                    if ((RangedMarks.Contains(markType) || markType is "errorbar" or "errorband") && encoding[name[..1]] is { IsObject: true })
                    {
                        VisitSecondaryDefinition(name, channel);
                    }
                    else
                    {
                        Unsupported(Id("channel", name), $"The '{name}' channel is only supported for a bar, rect, rule, area, errorbar or errorband mark that has the '{name[..1]}' channel.", channel);
                    }
                }
                else if (name is "xError" or "xError2" or "yError" or "yError2")
                {
                    // The errors of the range of an errorbar or errorband
                    if (markType is "errorbar" or "errorband")
                    {
                        VisitSecondaryDefinition(name, channel);
                    }
                    else
                    {
                        Unsupported(Id("channel", name), $"The '{name}' channel is only supported for an errorbar or errorband mark; it is ignored.", channel);
                    }
                }
                else if (name == "size" && markType == "errorbar")
                {
                    // Vega-Lite drops the size channel from the parts of an errorbar; the mark's own size and thickness give the sizes
                    Unsupported(Id("channel", name), "The 'size' channel is not drawn by the 'errorbar' mark; it is ignored (use the 'size' and 'thickness' of the mark).", channel);
                }
                else if (name is "order" or "detail")
                {
                    // The order channel orders a stack, the points of a path or the drawing of the marks; the detail channel groups the points of a path (and is accepted by every mark)
                    VisitSecondaryDefinition(name, channel);
                }
                else if (!SupportedChannels.Contains(name))
                {
                    // Channels that Vega-Lite defines but the renderer does not draw; unknown names were already warned about
                    if (SpecParser.IsChannelName(name))
                    {
                        Unsupported(Id("channel", name), $"The '{name}' channel is not supported yet.", channel);
                    }
                }
                else if ((name is "shape" or "angle" or "strokeDash" or "text" or "theta" or "radius" or "theta2" or "radius2" || (name == "size" && markType is "area" or "arc")) && !ScaleResolver.ChannelIsDrawn(name, drawnAs)
                    && !(name == "shape" && pointOverlay))
                {
                    // The channel is supported, but the mark does not draw it (a shape needs a point, an angle a symbol or text, a dash a mark with a stroke line, a size a mark that has one, a text a text mark,
                    // an angle (theta) or radius a text or arc mark and their second ends an arc mark); the outliers of a boxplot are points. A shape of a path mark with a point overlay is not reported:
                    // Vega-Lite drops it from the path layer before it checks the channels, and the overlay points draw it. An angle, a dash or a size is checked against the path mark as it is (Vega-Lite
                    // warns about it there), and the overlay marks still draw it: the points what a point draws, the line overlay of an area what a line draws.
                    var drawnByOverlay = name != "shape" && SpecNormalizer.OverlayDrawsChannel(name, pointOverlay, lineOverlay);
                    var overlayName = "line overlay";
                    if (SpecNormalizer.OverlayDrawsChannel(name, pointOverlay, false))
                    {
                        overlayName = SpecNormalizer.OverlayDrawsChannel(name, false, lineOverlay) ? "point and line overlays" : "point overlay";
                    }
                    Unsupported(Id("channel", name), drawnByOverlay
                        ? $"The '{name}' channel is not drawn by the '{markType}' mark itself; only its {overlayName} draws it."
                        : $"The '{name}' channel is not drawn by the '{markType}' mark; it is ignored.", channel);

                    // The overlay draws the channel, so its definition is visited as any other: its own unsupported properties and constructs are still reported
                    if (drawnByOverlay && channel.IsObject)
                    {
                        VisitChannelDefinition(name, channel);
                    }
                }
                else if (channel.IsObject)
                {
                    VisitChannelDefinition(name, channel);
                }
            }

            VisitStackImpute(markType, encoding, stackDecision.Value);
        }

        /// <summary>
        ///     Reports the channels of a composite mark that group its rows and have a <c>bin</c>, a <c>timeUnit</c> or an <c>aggregate</c>, which the expansion does not apply.
        /// </summary>
        /// <param name="type">The composite mark type: <c>boxplot</c>, <c>errorbar</c> or <c>errorband</c>.</param>
        /// <param name="mark">The mark of the view: a string or a mark definition.</param>
        /// <param name="encoding">The encoding object.</param>
        private void VisitCompositeGrouping(string type, SpecNode? mark, SpecNode encoding)
        {
            var definition = mark is { IsObject: true } ? mark : null;
            if (!SpecNormalizer.TryOrientation(type, definition, encoding, out var orient, out _))
            {
                return;
            }

            var measure = orient == "vertical" ? "y" : "x";
            foreach (var channel in encoding.Properties.Where(c => c.IsObject && c.Name != measure && c.Name is not ("x2" or "y2" or "xError" or "xError2" or "yError" or "yError2" or "tooltip")))
            {
                if (channel["aggregate"] is { IsNull: false })
                {
                    Unsupported("vl-composite-grouping", $"The '{channel.Name}' channel of the {type} has an aggregate, which a grouping channel cannot have; the channel is left out.", channel);
                }
                else if ((channel["bin"] is { } bin && IsEnabled(bin)) || channel["timeUnit"] is { IsNull: false })
                {
                    Unsupported("vl-composite-grouping", $"The bin or time unit of the '{channel.Name}' channel of the {type} is not applied: the rows are grouped by the field as it is.", channel);
                }
            }
        }

        /// <summary>
        ///     Warns that the implicit impute of a stacked line or area does not impute a binned dimension.
        /// </summary>
        /// <remarks>
        ///     Vega-Lite fills the layers of a stacked path mark with zeros for every key they lack. It does so for a binned dimension through a computed mid-point field, which this renderer does not
        ///     have, so a binned dimension is stacked as it is (a layer that lacks a bin has no point there).
        /// </remarks>
        /// <param name="markType">The mark type.</param>
        /// <param name="encoding">The encoding object.</param>
        /// <param name="decision">The stack decision of the view, or null when the marks are not stacked.</param>
        private void VisitStackImpute(string markType, SpecNode encoding, StackDecision? decision)
        {
            if (decision is null || markType is not ("line" or "area" or "trail") || decision.StackBy.Count == 0 || encoding[decision.Channel]?["impute"] is { IsNull: true })
            {
                return;
            }

            var dimensionChannel = StackRules.DimensionChannel(decision.Channel);
            if (encoding[dimensionChannel] is not { IsObject: true } dimension || !StackRules.IsFieldDef(dimension) || !StackRules.IsEnabled(dimension["bin"]))
            {
                return;
            }

            Warn("vl-encoding-stack-impute-bin", $"The layers of a stacked '{markType}' mark are not imputed along the binned '{dimensionChannel}' channel; a layer that lacks a bin has no point there.", dimension);
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
            if (name is "xOffset" or "yOffset")
            {
                VisitOffset(name, definition);
            }

            if (name is not ("x" or "y"))
            {
                // The impute of a position channel is run by the pipeline (EncodingPlan.Impute); on any other channel it is not
                ReportProperty(definition, "impute", n => !n.IsNull, $"Imputation is only supported on the 'x' and 'y' channels, not on '{name}'; the impute is ignored.");
            }
            VisitBandPosition(name, definition);
            if (definition["datum"] is { IsObject: true } datum && SpecParser.IsDateTimeShaped(datum))
            {
                Unsupported("vl-encoding-datum-datetime", "A date-time object as the datum of a channel is not supported yet; the channel is ignored.", datum);
            }

            if (name == "shape" && definition["value"] is { IsString: true } shape)
            {
                VisitShapeName(shape);
            }

            if (name == "text" && definition["formatType"] is { IsString: true } formatType && !IsEvaluatedFormatType(formatType.StringValue))
            {
                Unsupported("vl-format-type-custom", "Custom format types (the formatType of a text channel) are not supported; the format type must be number, time or utc.", formatType);
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
        ///     Reports the <c>bandPosition</c> that is not applied: one on a field without a time unit (except the one that places the label of a text mark inside the slice of its stacked <c>theta</c> or
        ///     <c>radius</c>), and one other than 0.5 (or a mark <c>timeUnitBandPosition</c> other than 0.5) on a bar or rect whose field has a time unit.
        /// </summary>
        /// <remarks>
        ///     Vega-Lite positions the rect of a time unit with shifted start and end fields when the band position is not the middle (0.5 with the band size 1,
        ///     which spans the unit). The renderer spans the unit; a position on a field without a time unit, or with a bin, is not applied either.
        /// </remarks>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitBandPosition(string name, SpecNode definition)
        {
            var hasUnit = definition["timeUnit"] is { IsNull: false } timeUnit && TryReadTimeUnit(timeUnit, out var unit) && !unit.IsBinned && unit.Parts.Count > 0;
            if (definition["bandPosition"] is { } position)
            {
                if (!hasUnit)
                {
                    if (!(_markType == "text" && name is "theta" or "radius" && _stack?.Value?.Channel == name))
                    {
                        Unsupported("vl-encoding-bandposition", "The band position of a field is not supported yet.", position);
                    }
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
            var isTemporal = ChannelDefinitions.TypeOf(definition, channel) == "temporal";
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
            ChannelDefinitions.TypeOf(definition, name) == "quantitative"
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
        ///     Reports an offset channel that the renderer does not draw (<see cref="ScaleResolver.DecideOffset"/>): one without a position channel to nest in, and a ranged one.
        /// </summary>
        /// <param name="name">The channel name: <c>xOffset</c> or <c>yOffset</c>.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitOffset(string name, SpecNode definition)
        {
            if (definition.Parent is not { IsObject: true } encoding)
            {
                return;
            }

            var position = name[..1];
            switch (ScaleResolver.DecideOffset(name, encoding, _markType))
            {
                case OffsetDecision.NoDiscreteParent:
                    Unsupported(
                        "vl-offset-position",
                        $"The '{name}' channel needs a nominal or ordinal '{position}' channel (with or without a time unit), or a temporal one with a supported time unit (not 'day' or 'weekday', binned or 'maxbins'; not a 'rect' mark), to nest in; the offset is ignored and the marks are drawn at the '{position}' position.",
                        definition);
                    break;
                case OffsetDecision.Ranged:
                    Unsupported(
                        "vl-offset-ranged",
                        $"A continuous '{name}' of a '{_markType}' mark (a ranged offset) is not supported; the offset is ignored and the marks are drawn at the '{position}' position.",
                        definition);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        ///     Reports the <c>bin</c> of a channel that cannot bin, and a field that is binned already on a channel other than <c>x</c> and <c>y</c>.
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
        }

        /// <summary>
        ///     Reports an explicit <c>stack</c> on a scale that is not linear.
        /// </summary>
        /// <param name="name">The channel name.</param>
        /// <param name="definition">The channel definition object.</param>
        private void VisitStack(string name, SpecNode definition)
        {
            if (name is not ("x" or "y" or "theta" or "radius") || definition["stack"] is not { } stack || !StackRules.IsEnabled(stack))
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

                // A condition on a parameter holds for the rows a selection contains (or when a variable parameter is truthy); its field, value and datum are visited like those of a test
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
            else if (named is null)
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
            if (definition["field"] is null || (definition["type"] is null && ChannelDefinitions.MainChannelOf(channel) is not null))
            {
                return;
            }

            // A field without a type has the type Vega-Lite infers; a secondary channel without one has no scale of its own
            var fieldTypeName = ChannelDefinitions.TypeOf(definition, channel);
            if (fieldTypeName is not ("quantitative" or "temporal" or "nominal" or "ordinal"))
            {
                return;
            }

            var fieldType = definition["type"] ?? definition;

            // Zero is not meaningful for time: Vega-Lite drops the property and says so
            if (fieldTypeName == "temporal" && definition["scale"]?["zero"] is { } zero
                && definition["scale"]?["type"]?.AsString() is null or "time" or "utc")
            {
                Warn("vl-scale-zero-ignored", "The scale property 'zero' is not applicable to a time scale; it is ignored.", zero);
            }

            if (definition["scale"]?["type"] is { IsString: true } scaleType)
            {
                if (SupportedScaleTypes.Contains(scaleType.StringValue!)
                    && !ScaleResolver.AcceptsScaleType(channel, fieldTypeName, scaleType.StringValue!, IsBinning(definition), ScaleResolver.HasNestedOffset(channel, definition.Parent, _markType)))
                {
                    Warn(
                        "vl-scale-type-ignored",
                        $"The '{scaleType.StringValue}' scale type cannot be used for the '{channel}' channel with a {fieldTypeName} field; the default scale type is used.",
                        scaleType);
                }
            }
            else if (channel is "shape" or "strokeDash" && fieldTypeName is "quantitative" or "temporal")
            {
                Warn(
                    "vl-scale-type-ignored",
                    $"The '{channel}' channel shows categories, so a {fieldTypeName} field gets an ordinal scale; use a nominal or ordinal field{(fieldTypeName == "quantitative" ? " or a discretizing scale type (quantize, quantile, threshold)" : string.Empty)}.",
                    fieldType);
            }
        }

        /// <summary>
        ///     Visits the <c>facet</c> of a facet view: one field definition (a wrapped facet), or an object with the <c>row</c> and <c>column</c> field definitions.
        /// </summary>
        /// <param name="facet">The <c>facet</c> value.</param>
        private void VisitFacet(SpecNode facet)
        {
            if (!facet.IsObject)
            {
                return;
            }

            if (facet["field"] is not null)
            {
                VisitFacetFieldDefinition("facet", facet);
                return;
            }

            foreach (var channel in new[] { "row", "column" })
            {
                if (facet[channel] is { IsObject: true } definition)
                {
                    VisitFacetFieldDefinition(channel, definition);
                }
            }
        }

        /// <summary>
        ///     Reports the parts of the definition of a facet field that the renderer does not draw.
        /// </summary>
        /// <remarks>
        ///     The facet itself is drawn: the distinct values of the field are its cells. A <c>bin</c>, <c>timeUnit</c> or <c>aggregate</c> of the field is not applied (the raw value is the
        ///     cell), a <c>sort</c> by a field and operation orders the cells by the field itself, and the <c>header</c> reports the same constructs as an axis does (a
        ///     custom <c>formatType</c>; the <c>labelExpr</c> is evaluated and reports only what its expression cannot do) and the sides that the header does not draw on (<c>labelOrient</c> and <c>titleOrient</c>; see <see cref="Guides.HeaderSettings.IsHonoredOrient"/>).
        /// </remarks>
        /// <param name="channel">The facet channel: <c>row</c>, <c>column</c> or <c>facet</c>.</param>
        /// <param name="definition">The field definition.</param>
        private void VisitFacetFieldDefinition(string channel, SpecNode definition)
        {
            if (definition["bin"] is { } bin && IsEnabled(bin))
            {
                Unsupported("vl-facet-field-transform", "The bin of a facet field is not applied; the value of the field is the value of the cell.", bin);
            }

            if (definition["timeUnit"] is { IsNull: false } timeUnit)
            {
                Unsupported("vl-facet-field-transform", "The time unit of a facet field is not applied; the value of the field is the value of the cell.", timeUnit);
            }

            if (definition["aggregate"] is { IsNull: false } aggregate)
            {
                Unsupported("vl-facet-field-transform", "The aggregate of a facet field is not applied; the value of the field is the value of the cell.", aggregate);
            }

            if (definition["sort"] is { IsObject: true } sort)
            {
                Unsupported("vl-facet-sort", "A facet sort by a field and an operation is not supported; the cells are in ascending order of the facet field.", sort);
            }

            if (definition["header"] is not { IsObject: true } header)
            {
                return;
            }

            foreach (var property in header.Properties)
            {
                switch (property.Name)
                {
                    case "labelExpr":
                        ReportConstructs(property);
                        break;
                    case "formatType" when !IsEvaluatedFormatType(property.AsString()):
                        Unsupported("vl-format-type-custom", "Custom format types (header.formatType) are not supported; the format type must be number, time or utc.", property);
                        break;
                    case "labelOrient":
                    case "titleOrient":
                        if (property.AsString() is { } side && side is "top" or "bottom" or "left" or "right" && !Guides.HeaderSettings.IsHonoredOrient(channel, property.Name, side))
                        {
                            Unsupported("vl-header-orient", $"The {property.Name} '{side}' is not supported for the '{channel}' channel; the default side is used.", property);
                        }

                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        ///     Reports what the <c>labelExpr</c> of the guide configurations of the specification cannot do (<c>config.axis</c> and the other axis configurations, <c>config.header</c>
        ///     and the header configurations of the channels). <c>config.legend</c> has no <c>labelExpr</c> in Vega-Lite and is not read.
        /// </summary>
        private void VisitConfigLabelExpressions()
        {
            if (_tree.Root["config"] is not { IsObject: true } config)
            {
                return;
            }

            foreach (var property in config.Properties)
            {
                if (property.IsObject && property.Name is { } name && (name.StartsWith("axis", StringComparison.Ordinal) || name is "header" or "headerRow" or "headerColumn" or "headerFacet")
                    && property["labelExpr"] is { IsString: true } text)
                {
                    ReportConstructs(text);
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
                        ReportConstructs(property);
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
        ///     <c>clipHeight</c> are not drawn, <c>encode</c> and <c>formatType</c> are not evaluated (the <c>labelExpr</c> is evaluated and reports only what its expression cannot do), and a
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
                        ReportConstructs(property);
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
        ///     binned time unit, the time unit of a predicate (see <c>vl-predicate-timeunit-part</c>), the selection parameters that are not modeled (see <c>vl-param-selection-layer</c>), a pivot operation other than sum and count, a lookup
        ///     in the data of a parameter. Every view is drawn (a view in a concatenation, a facet or a repeat too), so its transforms are applied; a transform of
        ///     a later phase is reported as not supported yet.
        /// </remarks>
        /// <param name="transforms">The array.</param>
        private void VisitTransforms(SpecNode transforms)
        {
            foreach (var entry in transforms.Items)
            {
                var operation = SpecParser.FindTransformOperation(entry);
                if (operation is null)
                {
                    continue;
                }

                var name = operation.Name!;
                if (!SpecParser.IsSupportedTransform(name))
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
                else if (named is null)
                {
                    VisitFieldPredicate(predicate);
                }
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
                    VisitSelection(parameter);
                }
                else if (!isRoot)
                {
                    Unsupported(
                        "vl-param-variable",
                        "Vega-Lite compiles only the variable parameters of the top-level view; this one declares no signal and is ignored (an expression that reads it fails in Vega, and evaluates to null here).",
                        parameter);
                }
                else if (parameter["expr"] is { IsString: true } expression)
                {
                    ReportConstructs(expression);
                }
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
                    // An expression of a supported site (compiled by the parser) is reported only for what it cannot do; anywhere else the reference is not evaluated.
                    // A reference of a static site is evaluated once, with no row: one that reads the datum has nothing to read
                    if (_tree.Expressions.Get(text) is { } compiled)
                    {
                        if (compiled.ReferencesDatum && _staticTexts.Contains(text))
                        {
                            Unsupported("vl-expr-ref", "The expression reads 'datum', which this property does not have (it is evaluated once, not for every row); the expression reference is ignored.", node);
                        }

                        ReportConstructs(text);
                    }
                    else
                    {
                        Unsupported("vl-expr-ref", IsInConfigSelection(node) ? SelectionReferenceMessage : "Expression references are not supported here yet.", node);
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

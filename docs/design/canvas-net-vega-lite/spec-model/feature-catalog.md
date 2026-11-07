### FeatureCatalog

![SpecModel Structure](VegaLiteSpecModelView.svg)

The `FeatureCatalog` unit is the second software unit in the `SpecModel` subsystem. It is one source file,
`FeatureCatalog.cs`, in the `DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace, and defines three `internal`
types: `FeatureCatalog` (static class), `FeatureReport` (sealed record), and `FeatureReportKind` (enumeration).

#### Purpose

`FeatureCatalog` lists every construct of a Vega-Lite specification that the renderer recognizes but does not
implement, each with a stable `vl-*` identifier and the exact location, so that nothing is ever skipped silently. It
is the single place that defines the boundary of what the renderer supports: a supported mark, channel, scale type, or
data source is simply absent from its result. It reads the parsed tree only (no data, no files, no network).

The unit's behavior is specified by the `CanvasNetVegaLite-SpecModel-FeatureCatalog-*` requirements in
`docs/reqstream/canvas-net-vega-lite/spec-model/feature-catalog.yaml`. The complete identifier table is in the XML
documentation of `FeatureCatalog` and in _Supported And Deferred Features_ in _CanvasNetVegaLite System Design_.

#### Data Model

**`FeatureReport`** is `(string Id, string Message, VegaLiteLocation Location, FeatureReportKind Kind)`: the lower-case
`vl-<area>-<detail>` identifier, a human-readable description without the identifier and location, the location of the
construct (the value; for an unknown property or a duplicate key, the key), and the kind. **`FeatureReportKind`** is
`Warning` (a problem with the text that does not stop rendering, delivered as a `VegaLiteDiagnostic`) or
`UnsupportedFeature` (a recognized construct that is not rendered, delivered as a `VegaLiteUnsupportedFeature`).

**The supported sets** are private read-only sets in the class and define the boundary: the supported marks (bar, rect,
point, circle, square, rule, tick, line, area, trail, text, arc, and, since phase 10, boxplot, errorbar, errorband), the
supported channels (x, y, xOffset, yOffset, color, fill, stroke, size,
opacity, shape, strokeWidth, strokeDash, fillOpacity, strokeOpacity, angle, text, theta, radius, theta2, radius2, and,
since
phase 10, xError, xError2, yError, yError2; `order`
and `detail` are accepted by every mark and are not in the set), the marks that
draw a range between a position channel and its secondary channel (bar, rect, rule, area), the channels on which `bin`
is
applied (x, y, `text`, and every channel that has a scale), the ignored
channels (tooltip, href, description, key), the geographic channels, the legend channels (color, fill, stroke, size,
opacity, shape, strokeWidth, strokeDash), the supported scale types
(linear, log, pow, sqrt, symlog, sequential, time, utc, band, point, ordinal, quantile, quantize, threshold,
bin-ordinal), the
supported scale properties (type, domain, range, zero, nice, padding, reverse,
paddingInner, paddingOuter, align, round, clamp, scheme, domainMid, domainMin, domainMax, rangeMin, rangeMax,
interpolate, base, exponent, constant; any other, such as `bins`, is reported; `domainRaw` is supported since phase 12,
see
the raw domain of the scale resolver), the
unsupported axis properties, the scopes in which an
expression value is looked for, and the composition properties. An identifier is built from
an area and a name taken from the specification, lower-cased with the invariant culture and without separators
(`timeUnit` gives `timeunit`).

**Identifiers.** The areas are `schema`, `data`, `mark`, `channel`, `type`, `scale`, `encoding`, `guide`, `format`,
`expr`, `transform`, `composition`, `param`, `projection`, and `layout`; the table in the system design lists every
identifier with its trigger. The name of an expression function is lower-cased with the invariant culture in
`vl-expr-function-<name>` like the
other names taken from the specification (the message keeps the function name as written, so `timeFormat` and
`timeformat` share one identifier and keep distinct messages). The identifiers `vl-expr-calculate`,
`vl-expr-filter`, and (for the drawn view) the
`vl-transform-` identifiers of the supported transforms (the six aggregating ones included since phase 6) are retired:
those constructs are supported and absent from the
result; so are `vl-encoding-aggregate` and `vl-encoding-stack` (and the report of an implicit bar stack), because
`aggregate`, `stack`, and `bin` on `x` and `y` are computed. The phase 6 identifiers are `vl-encoding-bin` (now only for
a `bin: "binned"` on a channel other than `x` and `y`, and for a `bin` on a channel that cannot bin: `order`, the
secondary channels, and (since phase 9) `theta` and `radius`, whose bins are not drawn; the other channels that have a
scale
bin from the data since phase 7, and `text` since phase 9), `vl-encoding-bin-extent-param`
and `vl-transform-bin-extent-param` (a bin extent that
is a parameter; retired in phase 12 and never produced again: the extent sets the span that chooses the step),
`vl-encoding-stack-nonlinear` (an explicit stack of a channel whose `scale.type` is not
linear; the marks of a log, pow, sqrt, or symlog scale are not stacked, because Vega-Lite does not stack on
them), `vl-channel-x2` and `vl-channel-y2` (reported only where the channel is not applied), and
`vl-encoding-sort` (now only for a sort that is not applied) and the new warning `vl-encoding-sort-ignored` (a sort that
a continuous scale ignores). The identifier `vl-channel-order`, which phase 6 reported where the channel orders no
stack, is retired in phase 9: `order` is accepted by every mark (see below). The phase 7 identifier
`vl-encoding-condition` is narrowed to a `condition` on a channel that
takes none (the position, secondary, and order channels); a condition with a `test` on the color, fill, stroke, opacity,
fillOpacity, strokeOpacity, strokeWidth, strokeDash, size, shape, and angle channels is evaluated by the marks, so it is
not reported. A condition with a `param` or a `test` that names a selection or a variable parameter is evaluated (a
selection by its test, a variable parameter by its truthiness), so the identifier `vl-encoding-condition-param` is
retired in phase 12 and no longer produced. The identifier `vl-encoding-condition-test` (a
`timeUnit` or a date object in the test of a condition) is retired in phase 8: those predicates are written as
expressions
and evaluated, so only the parts of a time unit that a predicate does not apply are reported (see below).
`VisitCondition` walks the conditions
of a channel (a predicate composition is walked with an explicit stack, so its nesting cannot overflow the call stack),
reports the constructs of a test that is an expression text, and visits the field properties of a field condition like
those of a channel definition. The warnings `vl-duplicate-key`, `vl-unknown-property`, and
`vl-encoding-sort-mixed-types` are
produced by `SpecParser`, and the render-time
warnings `vl-expr-runtime` and `vl-transform-runtime` by `RowTransforms` (they need the data; the
catalog never produces them).

**Phase 8 (time) identifiers.** Time is supported, so the identifiers that said it was not are retired and absent from
the
result: `vl-type-temporal` (a temporal field is read and gets a time scale), `vl-encoding-timeunit` (a time unit in an
encoding is computed), `vl-transform-timeunit` (the transform is computed, in any view: the single view, a layer, and
the
views of a concatenation, a facet or a repeat),
`vl-transform-filter-timeunit` and `vl-transform-filter-datetime` (time and date-time predicates are evaluated),
`vl-encoding-condition-test`, `vl-data-parse-date-format` (a custom date format of `format.parse` is applied), and
`vl-scale-time` and `vl-scale-utc` (`time` and `utc` join the supported scale types). The identifiers that the phase
adds are:

- `vl-timeunit-binned` - a `binned` time unit (a name that starts with `binned`, or `binned: true`) in an encoding, a
  condition, a predicate, or the `timeUnit` transform; the unit is ignored and the field is used as it is. It is also
  an `UnsupportedFeature`, so strict mode throws at the first one.
- `vl-timeunit-maxbins` - the `{"maxbins": n}` form of a unit that names no unit, in a **predicate** (a predicate has no
  data extent to bin; the unit is ignored). The encoding and the transform apply `maxbins`.
- `vl-predicate-timeunit-part` (warning) - the `timeUnit` of a predicate (a filter or a condition test) whose unit has a
  `week` or `dayofyear` part, which a predicate does not apply (as in Vega-Lite); the other parts are compared.
- `vl-encoding-bandposition-rect` - a `bandPosition` or `timeUnitBandPosition` other than 0.5 on a bar or rect whose
  field
  has a time unit (0.5 is used). The `timeUnitBandPosition` is read from the mark definition, else from `config.<mark>`,
  else from `config.mark` of the specification (the catalog has no caller `Config`). `timeUnitBandSize` is not reported.
  `vl-encoding-bandposition` is **narrowed**: it remains for a `bandPosition` on a field without a time unit and for one
  on a binned field (the geometry of a bin does not apply it), and no longer covers a field with a time unit.
- `vl-encoding-datum-datetime` - a date-time object as the `datum` of a channel (the channel is ignored).
- `vl-scale-nice-interval` and `vl-guide-axis-tickcount-interval` (warnings) - a time interval (a text or an
  `{interval, step}` object) as `scale.nice` or as the `tickCount` of an axis or legend on a channel that has no time
  scale;
  it is ignored and the default is used.
- `vl-scale-zero-ignored` (warning) - a `scale.zero` on a temporal field with a time or UTC scale (or the sequential
  scale
  of a temporal color), where zero is not meaningful (ignored, as Vega-Lite does; the scale resolver ignores it on every
  temporal field).
- `vl-scale-type-ignored` is **extended**: a temporal field accepts only the `time` and `utc` scale types, so any other
  `scale.type` on it falls back to the default with this warning, as does `time` or `utc` on a quantitative, nominal, or
  ordinal field, and a temporal field on a `shape` or `strokeDash` channel without a scale type.
- `vl-format-type-custom` is **narrowed**: `formatType` of an axis or legend is reported only when it is not `number`,
  `time`, or `utc` (`FeatureCatalog.IsEvaluatedFormatType`); a custom format type that a host would have to provide
  still uses the default format.

**Phase 9 (line, area, trail, text, and arc) identifiers.** The marks `line`, `area`, `trail`, `text`, and `arc` are
drawn, so the identifiers `vl-mark-line`, `vl-mark-area`, `vl-mark-trail`, `vl-mark-text`, and `vl-mark-arc` are retired
(the supported mark set of the catalog and `MarkBuilder.SupportedMarks` change in the same commit), and so are
`vl-channel-detail` and `vl-channel-order`: both channels are accepted by every mark. `vl-mark-image` and
`vl-mark-geoshape` stay (the composite marks `vl-mark-boxplot`, `vl-mark-errorbar`, and `vl-mark-errorband` stayed in
phase 9 and are retired in phase 10, see below); **`geoshape` is now the
sentinel "unsupported mark"** of the tests and examples (and `encoding.longitude`, which reports `vl-channel-geo`, is
the
sentinel unsupported channel), where `arc` and `theta` were before. The identifiers that change are:

- `vl-channel-theta`, `vl-channel-radius`, `vl-channel-theta2`, `vl-channel-radius2`, `vl-channel-text`,
  `vl-channel-size`, `vl-channel-strokedash`, `vl-channel-angle`, and `vl-channel-shape` are **narrowed** to the marks
  that do not draw the channel (the message says that the channel is not drawn by the mark and is ignored), by
  `ScaleResolver.ChannelIsDrawn`: `theta` and
  `radius` are drawn by `arc` and `text`, `theta2` and `radius2` by `arc`, `text` by `text`, `shape` by `point`,
  `angle` by `point`, `circle`, `square`, and `text`, `strokeDash` by every mark but `rect`, `area`, `trail`, `text`,
  and
  `arc`, and `size` by every mark but `rect`, `area`, `arc`, and `image` (so `size` on an `area` or an `arc` is newly
  reported; the `rect` size stays unreported as before). `vl-channel-x2` and `vl-channel-y2` are narrowed too: a bar,
  rect, rule, and (new) area that has the channel they extend accepts them. A `shape` of a `line`, `area`, `trail`, or
  `rule` that has a point overlay (`SpecNormalizer.HasPointOverlay`: an `encoding.shape` switches the overlay on unless
  `point` is `false` or null) is **not** reported, because the overlay points draw it and Vega-Lite drops it from the
  path
  layer before it checks the channels; an `angle`, a `strokeDash` or a `size` that the path mark does not draw but its
  overlay points do is still reported (the path layer warns about it in Vega-Lite) with the message that only the point
  overlay draws it. The catalog reads `config.<mark>.point` and `config.area.line` from the effective configuration:
  `FeatureCatalog.Visit(tree, config)` takes it (the renderer passes the configuration with the caller's, see
  `VegaLiteDocument.ReportsFor`), and `Visit(tree)` reads the configuration of the specification alone. The channels of
  the overlays are `SpecNormalizer.OverlayDrawsChannel` (a point draws shape, angle, size and dash; the line overlay of
  an area draws size and dash); the message names the overlay that draws it ("only its point overlay", "its line
  overlay",
  "its point and line overlays"). The definition of a channel that an overlay draws is still visited (its own
  unsupported
  properties and constructs are reported); only the path-specific report is replaced. A `shape` is reported only when
  there is no point overlay, whatever the configuration
  (the shape itself switches the overlay on).
- `vl-encoding-impute` is narrowed to a channel other than `x` and `y`: the impute of a position channel is run by the
  pipeline, and the impute of any other channel is ignored and reported.
- `vl-encoding-bandposition` no longer reports the `bandPosition` of the **stacked** `theta` or `radius` of a text mark
  (it places the label inside its slice, the only place where `bandPosition` is applied); every other
  `bandPosition` on a
  field without a time unit is reported as before.
- `vl-encoding-stack-nonlinear` also checks the `theta` and `radius` channels.
- `vl-format-type-custom` is also reported for a `formatType` of a `text` channel that is not `number`, `time`, or `utc`
  (the default format is used and the `format` is ignored).
- `vl-mark-invalid-show` - the `invalid` property of a mark when it is `show` or null: the marks are not drawn
  at a fallback position and the default mode applies (`UnsupportedFeature`, located at the value).
- `vl-mark-invalid-domains` (warning) - an `invalid` of `filter` or `break-paths-filter-domains`: those modes leave the
  invalid rows out of the scale domains, and the domains of this renderer always include them.
- `vl-mark-interpolate-bundle-area` - the `interpolate: "bundle"` of an `area`, which d3-shape cannot draw as an area
  (`UnsupportedFeature`; the area is drawn with the linear interpolation).
- `vl-mark-interpolate-unknown` - the `interpolate` of a `line` or `area` that is not one of the fifteen names of
  Vega-Lite (`UnsupportedFeature`; the line is drawn with the linear interpolation). The parser rejects such a name
  in a mark definition, so it can only come from the configuration, which the parser does not check.

  These identifiers are reported for the value that the mark builder reads, which can come from the configuration
  of the specification: `MarkSetting` resolves a property in the order of `MarkBuilder.Vl`, that is the mark
  definition, then the `config.style` entries named by the mark type and by the `style` of the mark (the later ones
  win), then `config.<mark>`, then `config.mark`. A value that comes from the configuration is located **at the
  configuration** (for example `/config/line/invalid`) and reported once however many marks use it (`ReportOnce`
  keys a report by identifier and node). Only the configuration inside the specification is visible to the catalog:
  the configuration that a caller passes to the renderer is not part of the parsed tree and is not checked.
- `vl-encoding-stack-impute-bin` (warning) - the dimension channel of a stacked `line`, `area`, or `trail` that has
  stack-by fields, is binned, and whose stacked channel does not say `impute: null`: Vega-Lite fills the layers with
  zeros
  for the bins they lack, and this renderer does not impute a binned dimension. It is located at the dimension
  definition.

The identifier `vl-scale-polar-discrete` that the plan kept as a fallback was **not added**: the discrete polar scales
(a band scale for an arc and a point scale for a text mark) are implemented.

**Phase 10 (layers and composite marks) identifiers.** The `layer` composition and the composite marks are drawn, so
**`vl-composition-layer`** and **`vl-mark-boxplot`**, **`vl-mark-errorbar`**, and **`vl-mark-errorband`** are retired
(they were reported until phase 9 and are no longer produced); the identifiers are not reused. The
sentinel "unsupported nested view" of the tests and examples is now `hconcat`. The identifiers that change or are new
are:

- `vl-transform-<name>` had a **reworded message** in Phase 10 for a transform of a view that was not drawn:
  `The '<name>' transform is not applied: the view is a '<composition>' composition, which is not supported yet.` That
  wording was **retired in Phase 11** (it was only produced for a view inside a facet, which is drawn now; the catalog's
  own `IsDrawnView` and `FindUndrawnComposition` rules are deleted): every transform of every view is applied. The
  transforms of a later phase (`regression`, `loess`, `density`, `quantile`, `extent`) kept "is not supported yet" until
  phase 13, which computes them: their identifiers are **retired** (see _Phase 13 identifiers_). A top-level
  `transform` of a layered view, and the transforms of its layers, are **not
  reported**: they are computed (this was the false report that a layered chart with a top-level `calculate` produced).
- `vl-layer-property-ignored` (warning) - a `width`, `height`, `autosize`, `padding`, `background`, `config`,
  `datasets`,
  or `title` of a view inside a layered view (a layer or a nested layered view), located at the property: only the
  top-level view decides it.
- `vl-layer-scale-conflict` (warning) - **produced by the renderer** (the scale resolver and the document), not by the
  catalog, because it needs the scales: layers whose scales of a shared channel cannot be merged.
- `vl-composite-grouping` - a channel of a `boxplot`, `errorbar`, or `errorband` that groups the rows (every channel but
  the measure axis, the second ends and error channels, and the tooltip) that has an enabled `bin`, a `timeUnit`, or an
  `aggregate`: the rows are grouped by the raw field. The orientation is decided by `SpecNormalizer.TryOrientation`;
  nothing is reported when it cannot be decided (the render fails with a specification error).
- `vl-channel-xerror`, `vl-channel-xerror2`, `vl-channel-yerror`, and `vl-channel-yerror2` - an error channel on a mark
  other than `errorbar` and `errorband`, which use them; the channel is ignored. The `x2` and `y2` channels are accepted
  on
  those two marks as the ends of the range, and `size` on an `errorbar` reports `vl-channel-size` (Vega-Lite drops it).
  `shape`, `angle`, and `strokeDash` are accepted on a `boxplot` (its outliers are points). The channel-drawn rule
  (`ScaleResolver.ChannelIsDrawn`) sees an `errorbar` as a `rule`, an `errorband` as an `area` or a `rect`, and a
  `boxplot` as a `point` for these channels.

**Phase 11 (concatenation, repeat and facet) identifiers.** `concat`, `hconcat`, `vconcat`, `repeat`, and `facet` are
drawn, so **`vl-composition-concat`**, **`vl-composition-hconcat`**, **`vl-composition-vconcat`**,
**`vl-composition-repeat`**, and **`vl-composition-facet`** are retired (they were reported until phase 10, and the last
until the facet was drawn; they are no longer produced); the identifiers are not reused. The sentinel "unsupported
nested view" of the tests is a `geoshape` mark or a `projection`. A unit view with a `row`, `column`, or `facet` channel
is a facet; the catalog reports the channel (`vl-channel-<name>`) only where Vega-Lite cannot facet the unit (a layer, a
layered view whose encoding holds the channel, the `spec` of a facet). The identifiers that change or are new are:

- `vl-transform-<name>` kept only its "not supported yet" message (the transforms of later phases; retired in phase
  13): the retired text
  "not applied: the view is a '`<composition>`' composition" is never produced, because the transforms of the views of
  every composition (a concatenation, a facet, a repeat) are computed, like those of a layer.
- `vl-channel-row`, `vl-channel-column`, `vl-channel-facet` are narrowed to a unit view that cannot be faceted (see
  above)
  and reworded: "The '`<name>`' channel only facets a unit view that is not a layer or the spec of a facet; it is
  ignored here (use the facet operator)." The channels of a unit view that is faceted (the root, a child of a
  concatenation, a copy of a repeat) are drawn: `VisitEncoding` hands their field definitions to
  `VisitFacetFieldDefinition`, which is also what visits the `facet` property (`VisitFacet`: a field definition, or an
  object with `row` and `column`).
- `vl-facet-field-transform` - a `bin`, `timeUnit` or `aggregate` of a facet field, at that property; the raw value of
  the
  field is the value of the cell. `vl-facet-sort` - a `sort` that is an object (a field and an operation) of a facet
  field;
  the cells are in ascending order of the field. `vl-header-orient` - a `header.labelOrient` or `header.titleOrient` on
  a side that the header does not draw on (`HeaderSettings.IsHonoredOrient`: a row honors `left` and `right`, a column
  `top` and `bottom`, the label of a wrapped facet any side and its title `top` and `bottom`); the default side is used.
  `header.labelExpr` is evaluated and, like every label expression, reports only the constructs its expression cannot
  do (see _Label expression identifiers_ below), a custom `header.formatType` the existing
  `vl-format-type-custom`, and an `{"expr": ...}` in a facet property `vl-expr-ref` (`facet` joins the expression
  scopes).
- `vl-composition-property-ignored` (warning) - a `width`, `height`, or `autosize` other than `pad` of a concatenation,
  a facet, or a repeat of rows or columns, the `"container"` size of a view inside one, and the `padding`, `background`,
  `config`, `datasets`, or `autosize` of a view inside one, located at the property (the `type` of an `autosize`
  object).
  Vega-Lite drops these properties; the composition is drawn padded (this covers `fit`, `fit-x`, `fit-y`, a
  `"container"` width or height, and `none`, as `normalizeAutoSize` and the fit rules of Vega-Lite do for a view that
  is not a unit or a layer). The report of an `autosize` type of a composition as `vl-layout-autosize-<type>` is
  replaced by this warning.
- `vl-layout-autosize-fit`, `-fit-x`, and `-fit-y` (the family `vl-layout-autosize-<type>`) are **retired** in Phase 11
  and never produced: `fit`, `fit-x`, and `fit-y` are drawn for a single and a layered view (`ViewLayout`).
  `vl-layout-fit-step` (warning) is **produced by the renderer's layout**, not by the catalog, because it needs the
  scales: a fit that a step size drops (an explicit `{"step": n}` or no size on a discrete scale; Vega-Lite's
  `droppingFit`), located at the `autosize` (else at the size, else at the view), with the dimension and the resulting
  autosize in the message. Ids are never reused.
- `vl-concat-axis-shared`, `vl-composition-columns-ignored`, and `vl-repeat-ref-unresolved` (warnings) - **produced by
  the
  renderer** from the view tree (`LayerTree.Warnings`, forwarded from the one tree that is drawn, so each is delivered
  once), not by the catalog: a `resolve.axis.x` or `y` set to `shared` on a concatenation (the children keep their own
  axes, as Vega-Lite does), `columns` next to a `row` or `column` mapping of a facet or repeat (ignored), and a
  `{"repeat": ...}` reference that the enclosing repeat does not define (the channel is dropped).
- `vl-layer-scale-conflict` now says "view" instead of "layer" in its message: it is also produced for the children
  of a concatenation that share a channel.

**Label expression identifiers.** The `labelExpr` of an axis, a legend, and a facet header (and of `config.axis`, the
other axis configurations, `config.header`, `headerRow`, `headerColumn`, and `headerFacet`) is compiled by `SpecParser`
when the document is opened and is evaluated by the guides, so **`vl-expr-label` is retired**: it is never produced
again and its identifier is never reused. `ReportConstructs` reports only what the expression cannot do:
`vl-expr-function-<name>`, `vl-expr-view-state`, and `vl-expr-construct-<name>`, located at the expression text (also
for the `labelExpr` of an axis or header configuration; `VisitConfigLabelExpressions` visits `config`). A `labelExpr` in
`config.legend` is **neither compiled nor reported**: Vega-Lite 6 has no such property and the renderer does not read
it. The number `format(datum.value, ',')` is one of the 100 whitelisted functions (`timeFormat` was already), so it
is evaluated and **not reported**: `vl-expr-function-format` is **retired** (it is no longer produced, and is never
reused). A function that is still outside the table, such as `luminance`, `geoarea`, or `now`, is reported and
evaluates to null (an empty label).

**Offset channel identifiers.** `xOffset` and `yOffset` are supported channels. `VisitOffset` calls
`ScaleResolver.DecideOffset`, the rule that the scale resolver also applies, so the report and the drawing agree:
**`vl-offset-position`** when the offset is ignored because the position channel (`x` for `xOffset`, `y` for `yOffset`)
is missing, is quantitative, is temporal without a `timeUnit`, or is temporal
with a `timeUnit` that cannot hold an offset (a unit whose smallest part is the day of the week such as `day`,
`weekday`, or `yearweekday`, a binned unit, a `maxbins` unit, or a `rect` mark); a temporal position with another time
unit (`TimeUnitParent`, for example `year` or `yearmonth`) is **not reported**: the offset is drawn inside the unit, and
so is not a nominal or ordinal position with a time unit, which is an ordinary band parent.
The message is "The 'xOffset' channel needs a nominal or ordinal 'x' channel (with or without a time unit), or a
temporal one with
a supported time unit (not 'day' or 'weekday', binned or 'maxbins'; not a 'rect' mark), to nest in; the offset is
ignored
...". **`vl-offset-ranged`** is reported when a continuous (quantitative
or temporal) offset sits on a `bar` or `area`, which Vega-Lite draws as a range; both are unsupported-feature reports
located at the offset channel definition and neither is produced for an offset that is drawn. An explicit
`scale.type` of `point` on a position that holds an offset is the existing warning `vl-scale-type-ignored`
(`ScaleResolver.AcceptsScaleType` takes the nested-offset flag, so the default band scale is used). A `bin` on an offset
channel is the existing `vl-encoding-bin`. Both new identifiers are never reused once retired.

**Phase 13 (statistical transforms) identifiers.** The transforms `regression`, `loess`, `density`, `quantile`, and
`extent` are computed (see _StatTransforms_), so **`vl-transform-regression`**, **`vl-transform-loess`**,
**`vl-transform-density`**, **`vl-transform-quantile`**, and **`vl-transform-extent`** are retired, and so is
**`vl-transform-pivot-op`** (a pivot computes every aggregate operation); none is produced any more, and none is reused.
The `vl-transform-<name>` family now has only `vl-transform-lookup-param`. One identifier is new:

- `vl-param-extent-static` (warning) - **produced by the `ExpressionReferenceFolder`**, not by the catalog walk,
  because it
  needs the compiled references: an expression reference at a static site (a title, a mark property, a scale
  property, ...) that reads the parameter of an `extent` transform. The properties of a static site are fixed before
  the data is read, so the parameter is null there; the report names the parameter and is located at the reference.
  An expression that the data stage evaluates (a `calculate`, a `filter`, a later `bin.extent`, or a `scale.domain`
  that is a parameter reference) reads the computed extent and is not reported.

#### Key Methods

- `Visit(SpecTree tree)` - requires a non-null tree, runs a private `Walker`, and returns the reports sorted by line and
  then column with a stable sort, so reports at one position keep the order in which they were found, after removing the
  duplicates (the same identifier, location, message, and kind, so an inherited channel that several layers reach is
  reported once). It visits the
  `$schema` value and then every view of `tree.EnumerateViews()`; the encoding of a view that is drawn through `layer`
  is
  not visited on its own but as the **merged encoding of each layer** (from `SpecNormalizer.Layers` with generous
  limits; the nodes keep their original pointers) with that layer's mark, so a channel is judged by the mark that
  draws it. Since Phase 11 the units of every composition are in that list too (a concatenation, a facet's template
  once,
  and a repeat), and a unit view that is a child of a concatenation or the `spec` of a repeat or a facet is visited on
  its own as well, so that a repeated view is judged with the repeated field in place (the reports that the copies share
  are made once, at the template).
- The walker's `VisitSchema` warns `vl-schema-version` for a major version other than 5 and 6 or a URL that names no
  Vega-Lite version.
- `VisitView` dispatches each property of a view: `data`, `mark`, `encoding`, `transform`, `params`, the legacy
  `selection` (`VisitSelection`), `projection` (`vl-projection`), and the composition property
  `facet` (`VisitFacet`; `layer`, `concat`, `hconcat`, `vconcat`, `repeat`, and `facet` are drawn and not reported);
  for a view inside a layered view it warns `vl-layer-property-ignored` for the properties that only the top-level view
  decides; `VisitCompositionProperties` warns `vl-composition-property-ignored` for the `width`, `height`, and an
  `autosize` other than `pad` of a concatenation, a facet, or a repeat of rows or columns (a repeat of layers is a
  layered
  view), and, for a view inside one, for a `"container"` size and for its `padding`, `background`, `config`, `datasets`,
  and `autosize`; and, for the properties in the expression scopes (and the `config` of the top-level view), looks for
  `{"expr": ...}` values
  (`vl-expr-ref`, only where the reference is not evaluated: see the `{"expr": ...}` scan below). The warnings
  `vl-concat-axis-shared`, `vl-composition-columns-ignored`, and `vl-repeat-ref-unresolved`
  are produced by the renderer from the view tree (`LayerTree.Warnings`), not by the catalog.
- `VisitData` chooses the source by precedence (inline `values`, then `url`, then `sequence`, then `name`, then the
  geographic generators), not by key order. An empty `values` array or a `data` object without a source warns
  `vl-data-empty`; a `url` is classified with `IsSchemeBearingUrl` and only remote data is reported
  (`vl-data-remote-url`; a relative local path is supported and not reported); a `name` whose top-level dataset is
  text or an object is reported as `vl-data-values-text` or `vl-data-geo` (`VisitNamedData`); inline `values` given as
  text or as an object are reported the same way; and `VisitFormat` reports only a `topojson`
  format type (`vl-data-geo`). Sequences, named datasets, the csv, tsv, dsv, and json formats, parse maps (custom date
  formats included, which the data stage applies), and
  `format.property` are supported and are not reported. The retired identifiers `vl-data-file`, `vl-data-named`,
  `vl-data-sequence`, `vl-data-parse`, `vl-data-parse-date-format`, `vl-data-format-<type>`, and `vl-data-property` are
  no longer produced.
- `VisitMark` reports `vl-mark-<type>` for every mark type outside the supported set (`geoshape`; the
  composite marks are supported) and, for a supported mark, visits the
  `shape` name, the `invalid` and `interpolate` properties of the mark or of the configuration (`MarkSetting`;
  `VisitMarkInvalid`: `vl-mark-invalid-show` for `show` and null, the warning
  `vl-mark-invalid-domains` for `filter` and `break-paths-filter-domains`; `VisitMarkInterpolate`: the `interpolate:
  "bundle"`
  of an `area` (`vl-mark-interpolate-bundle-area`) and a name of a `line` or `area` that Vega-Lite does not know
  (`vl-mark-interpolate-unknown`)). **The `image` mark** (phase 13) is in the supported set and is reported at its mark
  as
  `vl-mark-image` only when `Visit(tree, config, hasImageResolver)` was called without a resolver
  (`VegaLiteDocument.ReportsFor`;
  the document's own `Reports`, computed at construction, are those of a caller without one): the mark is laid out and
  measured as usual and draws no picture, because the library never reads a file or a network resource on its own. With
  a
  resolver nothing is reported by the catalog; a picture that the resolver does not give is the warning
  `vl-mark-image-unresolved` of the painter (the catalog cannot know the answer). The `url` channel is supported on the
  image
  mark (`ScaleResolver.ChannelIsDrawn`) and reported as `vl-channel-url` on every other mark, the `x2` and `y2` channels
  are accepted on it (`RangedMarks`), a condition of `url` is reported as `vl-encoding-condition` (the channel takes
  none),
  and the `invalid` mode is checked like that of any drawn mark. `VisitEncoding` reports the
  channels outside the supported set (`vl-channel-<name>`, `vl-channel-geo`), the offset channels that are not drawn
  (`VisitOffset`: `vl-offset-position`, `vl-offset-ranged`), the field type (`vl-type-geojson`; a
  `temporal` field is supported), the encoding properties (`vl-encoding-<name>`), the scale type and properties
  (`vl-scale-<type>`, `vl-scale-property-<name>`, `vl-scale-interpolate-cubehelix`, `vl-scale-type-ignored`), the parts
  of a legend that are not drawn (`VisitLegend`: `vl-guide-encode`, `vl-format-type-custom` (for a
  `formatType` other than `number`, `time`, and `utc`),
  `vl-guide-legend-titleorient`, `vl-guide-legend-clipheight`, and `vl-shape-path` for a custom `symbolType`; its
  `labelExpr` reports only its unsupported constructs), the custom
  path shapes (`vl-shape-path`), and the axis
  properties (`vl-guide-axis-<name>`, `vl-guide-encode`, `vl-format-type-custom`; the `labelExpr` of an axis reports
  only
  its unsupported constructs). `VisitEncoding` also
  remembers the mark definition and the mark type of the view (for the band position rule). The phase 6 rules:
  `x2` and `y2` are accepted (and their definitions visited like any channel definition) for a bar, rect, rule, or
  area that has the channel they extend, and report `vl-channel-x2` or `vl-channel-y2` otherwise; **`order` and
  `detail` are accepted by every mark** (their definitions are visited like any channel definition, and
  `vl-channel-order` and `vl-channel-detail` are retired); the stack decision of the view
  (`StackRules.Decide` with the orientation of `ScaleResolver.InferOrient`, so the catalog and the encoding plan
  agree) is made lazily and is used by the band position rule and by `VisitStackImpute` (see below); `VisitBin`
  reports `vl-encoding-bin`
  for a `bin` on a channel that cannot bin (`BinnedChannels` holds `x`, `y`, `text`, and the channels that have a
  scale) and for
  a `bin: "binned"` on any channel but `x` and `y` (`IsBinning` tells the bins of the data from the data that is binned
  already); a bin extent that is a parameter is supported (`vl-encoding-bin-extent-param` was retired in phase 12);
  `VisitStack` reports `vl-encoding-stack-nonlinear` for the `x`, `y`, `theta`, and `radius` channels; `aggregate`,
  `bin` on the channels that bin, and `stack` are otherwise
  supported and never reported; `sort` is reported as `vl-encoding-sort` when `SpecParser.IsSupportedSort` rejects it (a
  channel shorthand or an `encoding` that names a channel without a field or an aggregate, or an `op` of `values` or an
  exponential operation); the message of a missing channel names it (`SpecParser.TryGetSortChannel`: "The sort refers to
  the 'y' channel, which this encoding does not have (or which reads no field); the categories are sorted ascending.").
  It stays a report and not a specification error, because after a later normalization the channel may legitimately come
  from a layer or a parent encoding. `VisitSort` also warns `vl-encoding-sort-ignored` when a sort array, a channel
  shorthand, or an object with a `field`, `op`, or `encoding` sits on a channel (other than `order`, `x2`, and `y2`)
  whose type is `quantitative` and whose `scale.type` is not a discrete one (`band point ordinal bin-ordinal quantile
  quantize threshold`; a `shape` or `strokeDash` channel never counts as continuous): a continuous scale has no
  categories to order. `ascending`, `descending`, `null`, and an object with only
  an `order` are never reported;
  `timeUnit` is applied (see the phase 8 rules below), an encoding `impute` is reported only outside the `x` and `y`
  channels (the pipeline runs the impute of a position channel), and `bandPosition` is reported
  only as described for `vl-encoding-bandposition` and `vl-encoding-bandposition-rect` (the stacked `theta` or
  `radius` of
  a text mark is the exception that `VisitBandPosition` does not report); `condition` is reported only on
  a channel that takes
  none, or for a parameter (see the phase 7 identifiers above).
- The phase 9 rules: `SupportedChannels` also holds `text`, `theta`, `radius`, `theta2`, and `radius2`, and the
  report of a
  channel that the mark does not draw covers them all: `VisitEncoding` reports `vl-channel-<name>` for `shape`,
  `angle`,
  `strokeDash`, `text`, `theta`, `radius`, `theta2`, and `radius2` on a mark for which
  `ScaleResolver.ChannelIsDrawn` is false,
  and for `size` on an `area` or an `arc` (the `size` of a `rect` is not reported, as before). `VisitStackImpute` warns
  `vl-encoding-stack-impute-bin` when the stack decision exists, the mark is a `line`, `area`, or `trail`, the decision
  has stack-by fields, the stacked definition does not say `impute: null`, and the dimension channel (found with
  `StackRules.DimensionChannel`) is a field definition with an enabled `bin` (also `"binned"`); it is located at the
  dimension definition. `VisitChannelDefinition` reports `vl-format-type-custom` for a `formatType` of a `text` channel
  that `IsEvaluatedFormatType` does not accept.
- The phase 7 rules: `SupportedChannels` also holds `shape`, `strokeWidth`, `strokeDash`, `fillOpacity`,
  `strokeOpacity`, and `angle`, but a `shape`, `angle`, or `strokeDash` channel is reported (`vl-channel-<name>`) and
  ignored for a mark that does not draw it (`ScaleResolver.ChannelIsDrawn`). `SupportedScaleTypes` also holds
  `sequential`, `quantile`, `quantize`, `threshold`, and `bin-ordinal` (the `vl-scale-bin-ordinal` identifier is
  retired),
  and `SupportedScaleProperties` also `domainMid`, `domainMin`,
  `domainMax`, `domainRaw` (phase 12), `rangeMin`, `rangeMax`, and `interpolate`; `scheme` is never reported (an
  unknown name is a specification
  error of the scale resolver), so the retired identifier `vl-scale-color-ramp` is no longer produced. `VisitScale`
  reports `vl-scale-interpolate-cubehelix` for the two cubehelix interpolation types, and `VisitShapeName` reports
  `vl-shape-path` for a shape that `ScaleResolver.IsKnownShape` does not know (the `shape` of a mark, the `value` of a
  `shape` channel, and the entries of its `scale.range`). `VisitScaleType` warns `vl-scale-type-ignored` when a
  supported `scale.type` is not accepted for the channel and the field type (`ScaleResolver.AcceptsScaleType`, which
  also takes whether the encoding bins the field: `bin-ordinal` is accepted for a binned quantitative color only; the
  rule
  the resolver replaces it by the default type with) and for a quantitative field on a `shape` or `strokeDash` channel
  that names no scale type; a type that is not drawn is reported only as `vl-scale-<type>`. Legends are drawn, so the
  identifier `vl-guide-legend` is retired: `VisitLegend` visits the `legend` object of a field-driven channel and
  reports
  only what the legend builder does not draw.
- The phase 8 rules: `SupportedScaleTypes` also holds `time` and `utc` (so `vl-scale-time` and `vl-scale-utc` are
  retired). `VisitChannelDefinition` no longer reports a temporal type or a time unit; it calls `VisitTimeUnit`, which
  reads the unit with `TimeUnitSpec.TryRead` and reports `vl-timeunit-binned` for a binned unit (and
  `vl-timeunit-maxbins` for a `{maxbins}` form without a unit where that cannot be applied, which is in a predicate);
  `VisitBandPosition` reports `vl-encoding-bandposition` and `vl-encoding-bandposition-rect` by the rules above (a
  `bandPosition` on a field with a real, unbinned time unit is reported only for a bar or rect and a value other than
  0.5);
  the `datum` that is a date-time object (`SpecParser.IsDateTimeShaped`) is `vl-encoding-datum-datetime`;
  `VisitIntervals` warns `vl-scale-nice-interval` and `vl-guide-axis-tickcount-interval` for an interval on a channel
  that is not a time scale (a temporal field has a time scale on every channel but `shape` and `strokeDash`, and a
  non-temporal field has one only when its `scale.type` is `time` or `utc`); `VisitScaleType` now also looks at temporal
  fields: it warns `vl-scale-zero-ignored` for a `scale.zero` on a temporal field whose scale type is absent, `time`, or
  `utc`, and `vl-scale-type-ignored` for a type that `ScaleResolver.AcceptsScaleType` does not accept (only `time` and
  `utc`
  are accepted for a temporal field) and for a temporal field on a `shape` or `strokeDash` channel without a scale type;
  `VisitFormat` no longer reports a `format.parse` entry. The static `IsEvaluatedFormatType(formatType)` tells the
  format types that are evaluated (`number`, `time`, `utc`); the axis and legend `formatType` rules use it.
- `VisitTransforms(transforms, drawn, view)` reports the entries as `vl-transform-<name>` for nothing any more: every
  transform of Vega-Lite is computed (the transforms of the later phases - `regression`, `loess`, `density`, `quantile`,
  and `extent` - since phase 13, see _Phase 13 identifiers_) and every transform of a view that is not drawn is gone
  (none since phase 11: every composition is drawn, so the "not applied" message
  is retired, see _Phase 10 identifiers_). For the computed transforms of a drawn view (the single view, a layered view
  and its layers, and the views of a `concat`, `hconcat`, `vconcat`, `facet`, or `repeat`: `calculate`, `filter`,
  `fold`, `flatten`, `sample`, `pivot`, `lookup`, `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, `impute`,
  `timeUnit`, `regression`, `loess`, `density`, `quantile`, `extent`) it reports only their unsupported parts:
  `ReportConstructs` turns the constructs that the compiled
  expression collected into `vl-expr-function-<name>`, `vl-expr-view-state`, and `vl-expr-construct-<name>` reports (and
  an undeclared view signal into `vl-expr-view-state`), sorted by their offset and located at the expression string,
  with `(at expression offset N)` in the message; `VisitPredicate` walks a filter with an explicit stack (so composition
  depth cannot overflow the call stack) and reports no selection predicate (a `selection` or a `param` filter is
  evaluated;
  `vl-transform-filter-param` is retired in phase 12), and, through `VisitFieldPredicate`, the time unit of a field
  predicate (`VisitPredicateTimeUnit`:
  `vl-timeunit-binned`, `vl-timeunit-maxbins`, and the warning `vl-predicate-timeunit-part`; the retired
  `vl-transform-filter-timeunit` and `vl-transform-filter-datetime` are no longer produced); a `timeUnit` transform
  reports `vl-timeunit-binned` for a binned unit; a pivot `op` other than `sum` and `count` is
  `vl-transform-pivot-op` no more (retired in phase 13: a pivot computes every aggregate operation), and a lookup
  `from.param` is `vl-transform-lookup-param` while its `from.data` is visited
  like any `data` object (`vl-data-*`); a `bin` transform whose `extent` is a parameter is supported
  (`vl-transform-bin-extent-param` was retired in phase 12). - `VisitParams(parameters, isRoot)` calls
  `VisitSelection` for every selection parameter (and `VisitView` for the legacy `selection` property): a selection is
  drawn in
  its initial state, so it is not reported as a whole; the catalog reports an animation selection (an `on` with a timer,
  or
  the `time` encoding: `vl-param-selection-animation`; every other interaction property is ignored
  without a report), a static point selection with a `value` over the identity of the rows (`vl-param-selection-id`;
  the projection is decided by the effective configuration when the catalog is visited with one, so a `selection.point`
  of the caller's `Config` that names `fields` or `encodings` makes the selection modeled, as in the selection model),
  and a selection
  that two or more layers of one layered view receive (`vl-param-selection-layer`, found with
  `SelectionDeclarations.Assign`,
  because Vega rejects the duplicate signal; the selection is treated as empty). The projection warning of
  the selection model (`vl-param-selection-projection`, and `vl-param-selection-id` for a value of the caller) is added
  by
  `VegaLiteDocument`. `VisitParams` reports, for a variable parameter, `vl-param-variable` when the view is not
  the root (Vega-Lite compiles only the variable parameters of the top-level view, so a nested one declares no signal
  and
  is ignored, an expression that reads it fails in Vega and evaluates to null here); a variable parameter of the root
  view is computed, so only the constructs of its `expr` are reported.
`VisitAutosize` was removed in Phase 11: no autosize type is reported (see `vl-layout-fit-step` above).
  The `{"expr": ...}` scan (`VisitExpressions`, with
  an explicit stack) reports as `vl-expr-ref` only an expression reference that is not evaluated: one at a site that
  Vega-Lite's schema does not allow (the `width`, the `height`, an expression in a predicate outside the compiled sites)
  and one at a static site (a site of the parser's `StaticReferences`) whose expression reads `datum`, which has no row
  there. A reference that was compiled (the row-bound sites, and the static sites that the folder replaces by their
  value: the title, the padding, the background, the view, the properties of guides, scales, and marks, and the
  `config` of the top-level view, which is scanned too) is reported only for its
unsupported constructs. A reference whose value no property can hold is reported at render time by the folder, with the
  same identifier, not by the catalog.
- `VisitSelectionReferences(declaration)` (called by `VisitSelection` for every parameter with a `select` and every
  entry of the legacy `selection`) reports as `vl-expr-ref` every `{"expr": ...}` inside the declaration - the `select`
  options (`mark`, `on`, `clear`, `fields`, `encodings`, `translate`, `zoom`), the initial `value` or `init`, and
  `bind` - at its own pointer, found with an explicit stack. Vega-Lite's schema (6.5.0: `BrushConfig`,
  `PointSelectionConfig`, `IntervalSelectionConfig` and their `WithoutType` forms, `SelectionInit`) allows no
  `ExprRef` anywhere in a selection, so none is a static site: it is not compiled, folded, or evaluated, and the
  message says so. The same message is the one of a reference below `config.selection` (found by the scan above,
  which recognizes the path by the parent links of the node); `SelectionModel` then keeps the default of the
  brush `mark` for such a property (see the selection model unit).
- `VisitCallerConfig(string? callerConfig)` - reports as `vl-expr-ref` (unsupported feature, no location, the JSON
  pointer inside the text in the message) every `{"expr": ...}` object in the `Config` text of the caller's render
  options, found with an explicit stack in document order (the text was validated as a JSON object of at most 64
  levels). Nothing evaluates an expression in that text, which is merged over the defaults as it is, so the property
  would silently keep its default; `VegaLiteDocument.ReportsFor` adds these reports when the options carry a `Config`.
- `IsSchemeBearingUrl(string url)` - true for a URL that starts with `/` or a backslash (a Unix
  absolute path, a Windows rooted path, a network or UNC path), is rooted according to `Path.IsPathRooted`, or has a
  scheme (a letter followed by letters, digits, `+`, `-`, or `.`, then a colon, which includes a drive letter and
  `file:` URIs); the result does not depend on the host operating system. Such a URL is never
  handed to a resolver, because a renderer that fetches resources named by an untrusted specification would be a
  server-side request forgery primitive.

**Preconditions and postconditions**: the tree is non-null and valid; the result is a new read-only list in document
order, empty when the specification uses only supported constructs. The catalog is stateless and thread-safe.

#### Error Handling

The catalog never throws for the content of a specification: every recognized-but-unsupported construct is a report,
and every unknown construct is left to the parser's `vl-unknown-property` warning. A null tree throws
`ArgumentNullException`, which is not converted. The unit does not read data rows, so its cost does not depend on the
data volume, and it uses an explicit view enumeration, so nested views cannot overflow the stack. Strict mode is not
applied here: the document records the reports in a sink, which throws at the first unsupported feature.

#### Dependencies

- **`SpecTree`**, **`SpecNode`**, and **`SpecParser`** - the tree and the parser's helpers (`ParseSchemaMajorVersion`,
  `IsChannelName`, `FindTransformOperation`, `IsSupportedTransform`,
  `IsViewSignal`, `FindParameter`), in
  the `SpecParser` unit, and the compiled expressions of the tree (`SpecExpressions`)
- **`SpecNormalizer`** - the view tree (`Layers`, with a bounded view limit) whose merged unit views the catalog
  visits for
  each layer that is not inside a facet, and
  `TryOrientation` that decides which channel of a composite mark groups the rows (see _SpecNormalizer Unit Design_)
- **`CompiledExpression`** and **`ExpressionConstruct`** - the constructs that the parser collected, from the
  `ExpressionParser` unit of the `Expressions` subsystem
- **`SpecParser.IsSupportedSort`** - the rule for a supported encoding `sort` (in the `SpecParser` unit, over the shared
  operation registry `AggregateMeasures.CanOrderCategories` of the `Transforms` subsystem), so that the report and the
  sort of the scale resolver agree
- **`StackRules`** (the `Transforms` subsystem) and **`ScaleResolver.InferOrient`** (the `Scales` subsystem) - the
  decision that a view stacks (on the Cartesian or the polar channels) and the dimension channel of a stack, which
  decide the
  stack-impute warning and the band position of a stacked polar text channel, shared with the encoding plan so
  that the report and the pipeline decide alike; **`ScaleResolver.ChannelIsDrawn`** - the support table of the channels
  per mark, shared with the scale resolver
- **`TimeUnitSpec`** - the time unit reader of the `Data` subsystem (see _Time Calendar Design_), so that the report and
  the unit that is applied agree on what a binned or `maxbins` unit is
- **`ScaleResolver.AcceptsScaleType`**, **`ScaleResolver.DecideOffset`**, and **`ScaleResolver.HasNestedOffset`** - the
  scale type rule per channel and field type (including temporal fields) and the offset rules, in
  the `Scales` subsystem
- **`VegaLiteLocation`** - the public location type of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization` (invariant-culture formatting of messages)

#### Callers

`VegaLiteDocument` calls `Visit` once when a document is created and merges the result with the parse warnings; the
pipeline delivers the reports through the sink. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/FeatureCatalogTests.cs` (and `FeatureCatalogLayerTests.cs`,
`FeatureCatalogCompositeTests.cs`, and `FeatureCatalogLabelExprAndOffsetTests.cs` for the label expressions and the
offset channels) call `Visit` and `IsSchemeBearingUrl`
directly through `InternalsVisibleTo`; the test `FeatureCatalog_Visit_OrderChannelIsAcceptedForEveryMark` pins that
`order`
is accepted by every mark.

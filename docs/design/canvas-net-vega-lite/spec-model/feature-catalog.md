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
point, circle, square, rule, tick), the supported channels (x, y, color, fill, stroke, size, opacity, shape, strokeWidth,
strokeDash, fillOpacity, strokeOpacity, angle), the marks that
draw a range between a position channel and its secondary channel (bar, rect, rule), the channels on which `bin` is
applied (x, y, and every channel that has a scale), the ignored
channels (tooltip, href, description, key), the geographic channels, the legend channels (color, fill, stroke, size,
opacity, shape, strokeWidth, strokeDash), the supported scale types
(linear, log, pow, sqrt, symlog, sequential, time, utc, band, point, ordinal, quantile, quantize, threshold,
bin-ordinal), the
supported scale properties (type, domain, range, zero, nice, padding, reverse,
paddingInner, paddingOuter, align, round, clamp, scheme, domainMid, domainMin, domainMax, rangeMin, rangeMax,
interpolate, base, exponent, constant; any other, such as `bins`, is reported), the
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
a `bin: "binned"` on a channel other than `x` and `y`, and for a `bin` on a channel that cannot bin: `order` and the
secondary channels; the other channels that have a scale bin from the data since phase 7), `vl-encoding-bin-extent-param`
and `vl-transform-bin-extent-param` (a bin extent that
is a parameter; the
extent of the data is used), `vl-encoding-stack-nonlinear` (an explicit stack of a channel whose `scale.type` is not
linear; the marks of a log, pow, sqrt, or symlog scale are not stacked, because Vega-Lite does not stack on
them), `vl-channel-x2`, `vl-channel-y2`, and
`vl-channel-order` (reported only where the channel is not applied), and
`vl-encoding-sort` (now only for a sort that is not applied) and the new warning `vl-encoding-sort-ignored` (a sort that
a continuous scale ignores). The phase 7 identifier `vl-encoding-condition` is narrowed to a `condition` on a channel that
takes none (the position, secondary, and order channels); a condition with a `test` on the color, fill, stroke, opacity,
fillOpacity, strokeOpacity, strokeWidth, strokeDash, size, shape, and angle channels is evaluated by the marks, so it is
not reported. A condition with a `param`, and a `test` that names a selection or a parameter that is not a variable,
report `vl-encoding-condition-param` (the condition never holds). The identifier `vl-encoding-condition-test` (a
`timeUnit` or a date object in the test of a condition) is retired in phase 8: those predicates are written as expressions
and evaluated, so only the parts of a time unit that a predicate does not apply are reported (see below).
`VisitCondition` walks the conditions
of a channel (a predicate composition is walked with an explicit stack, so its nesting cannot overflow the call stack),
reports the constructs of a test that is an expression text, and visits the field properties of a field condition like
those of a channel definition. The warnings `vl-duplicate-key`, `vl-unknown-property`, and
`vl-encoding-sort-mixed-types` are
produced by `SpecParser`, and the render-time
warnings `vl-expr-runtime` and `vl-transform-runtime` by `RowTransforms` (they need the data; the
catalog never produces them).

**Phase 8 (time) identifiers.** Time is supported, so the identifiers that said it was not are retired and absent from the
result: `vl-type-temporal` (a temporal field is read and gets a time scale), `vl-encoding-timeunit` (a time unit in an
encoding is computed), `vl-transform-timeunit` (for a drawn view; the transform is computed, and a `timeUnit` transform
of a view that is not drawn is still reported by the generic `vl-transform-<name>` rule),
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
- `vl-encoding-bandposition-rect` - a `bandPosition` or `timeUnitBandPosition` other than 0.5 on a bar or rect whose field
  has a time unit (0.5 is used). The `timeUnitBandPosition` is read from the mark definition, else from `config.<mark>`,
  else from `config.mark` of the specification (the catalog has no caller `Config`). `timeUnitBandSize` is not reported.
  `vl-encoding-bandposition` is **narrowed**: it remains for a `bandPosition` on a field without a time unit and for one
  on a binned field (the geometry of a bin does not apply it), and no longer covers a field with a time unit.
- `vl-encoding-datum-datetime` - a date-time object as the `datum` of a channel (the channel is ignored).
- `vl-scale-nice-interval` and `vl-guide-axis-tickcount-interval` (warnings) - a time interval (a text or an
  `{interval, step}` object) as `scale.nice` or as the `tickCount` of an axis or legend on a channel that has no time scale;
  it is ignored and the default is used.
- `vl-scale-zero-ignored` (warning) - a `scale.zero` on a temporal field with a time or UTC scale, where zero is not
  meaningful (ignored, as Vega-Lite does).
- `vl-scale-type-ignored` is **extended**: a temporal field accepts only the `time` and `utc` scale types, so any other
  `scale.type` on it falls back to the default with this warning, as does `time` or `utc` on a quantitative, nominal, or
  ordinal field, and a temporal field on a `shape` or `strokeDash` channel without a scale type.
- `vl-format-type-custom` is **narrowed**: `formatType` of an axis or legend is reported only when it is not `number`,
  `time`, or `utc` (`FeatureCatalog.IsEvaluatedFormatType`); a custom format type that a host would have to provide
  still uses the default format.

#### Key Methods

- `Visit(SpecTree tree)` - requires a non-null tree, runs a private `Walker`, and returns the reports sorted by line and
  then column with a stable sort, so reports at one position keep the order in which they were found. It visits the
  `$schema` value and then every view of `tree.EnumerateViews()`.
- The walker's `VisitSchema` warns `vl-schema-version` for a major version other than 5 and 6 or a URL that names no
  Vega-Lite version.
- `VisitView` dispatches each property of a view: `data`, `mark`, `encoding`, `transform`, `params`, the legacy
  `selection` (`vl-param-selection`), `projection` (`vl-projection`), `autosize`, and the composition properties
  (`vl-composition-<name>`); and, for the properties in the expression scopes, looks for `{"expr": ...}` values
  (`vl-expr-ref`).
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
- `VisitMark` reports `vl-mark-<type>` for every mark type outside the supported set. `VisitEncoding` reports the
  channels outside the supported set (`vl-channel-<name>`, `vl-channel-geo`), the field type (`vl-type-geojson`; a
  `temporal` field is supported), the encoding properties (`vl-encoding-<name>`), the scale type and properties
  (`vl-scale-<type>`, `vl-scale-property-<name>`, `vl-scale-interpolate-cubehelix`, `vl-scale-type-ignored`), the parts
  of a legend that are not drawn (`VisitLegend`: `vl-guide-encode`, `vl-expr-label`, `vl-format-type-custom` (for a
  `formatType` other than `number`, `time`, and `utc`),
  `vl-guide-legend-titleorient`, `vl-guide-legend-clipheight`, and `vl-shape-path` for a custom `symbolType`), the custom
  path shapes (`vl-shape-path`), and the axis
  properties (`vl-guide-axis-<name>`, `vl-guide-encode`, `vl-format-type-custom`, `vl-expr-label`). `VisitEncoding` also
  remembers the mark definition and the mark type of the view (for the band position rule). The phase 6 rules:
  `x2` and `y2` are accepted (and their definitions visited like any channel definition) for a bar, rect, or rule that
  has the channel they extend, and report `vl-channel-x2` or `vl-channel-y2` otherwise; `order` is accepted when it
  orders a stack, decided lazily by `StackRules.Decide` with the orientation of `ScaleResolver.InferOrient` (so the
  catalog and the encoding plan agree), and reports `vl-channel-order` otherwise; `VisitBin` reports `vl-encoding-bin`
  for a `bin` on a channel that cannot bin (`BinnedChannels` holds `x`, `y`, and the channels that have a scale) and for
  a `bin: "binned"` on any channel but `x` and `y` (`IsBinning` tells the bins of the data from the data that is binned
  already), and `vl-encoding-bin-extent-param` for an extent that is a parameter;
  `VisitStack` reports `vl-encoding-stack-nonlinear`; `aggregate`, `bin` on the channels that bin, and `stack` are otherwise
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
  `timeUnit` is applied (see the phase 8 rules below), an encoding `impute` stays reported, and `bandPosition` is reported
  only as described for `vl-encoding-bandposition` and `vl-encoding-bandposition-rect`; `condition` is reported only on
  a channel that takes
  none, or for a parameter (see the phase 7 identifiers above).
- The phase 7 rules: `SupportedChannels` also holds `shape`, `strokeWidth`, `strokeDash`, `fillOpacity`,
  `strokeOpacity`, and `angle`, but a `shape`, `angle`, or `strokeDash` channel is reported (`vl-channel-<name>`) and
  ignored for a mark that does not draw it (`ScaleResolver.ChannelIsDrawn`). `SupportedScaleTypes` also holds
  `sequential`, `quantile`, `quantize`, `threshold`, and `bin-ordinal` (the `vl-scale-bin-ordinal` identifier is retired),
  and `SupportedScaleProperties` also `domainMid`, `domainMin`,
  `domainMax`, `rangeMin`, `rangeMax`, and `interpolate`; `scheme` is never reported (an unknown name is a specification
  error of the scale resolver), so the retired identifier `vl-scale-color-ramp` is no longer produced. `VisitScale`
  reports `vl-scale-interpolate-cubehelix` for the two cubehelix interpolation types, and `VisitShapeName` reports
  `vl-shape-path` for a shape that `ScaleResolver.IsKnownShape` does not know (the `shape` of a mark, the `value` of a
  `shape` channel, and the entries of its `scale.range`). `VisitScaleType` warns `vl-scale-type-ignored` when a
  supported `scale.type` is not accepted for the channel and the field type (`ScaleResolver.AcceptsScaleType`, which
  also takes whether the encoding bins the field: `bin-ordinal` is accepted for a binned quantitative color only; the rule
  the resolver replaces it by the default type with) and for a quantitative field on a `shape` or `strokeDash` channel
  that names no scale type; a type that is not drawn is reported only as `vl-scale-<type>`. Legends are drawn, so the
  identifier `vl-guide-legend` is retired: `VisitLegend` visits the `legend` object of a field-driven channel and reports
  only what the legend builder does not draw.
- The phase 8 rules: `SupportedScaleTypes` also holds `time` and `utc` (so `vl-scale-time` and `vl-scale-utc` are
  retired). `VisitChannelDefinition` no longer reports a temporal type or a time unit; it calls `VisitTimeUnit`, which
  reads the unit with `TimeUnitSpec.TryRead` and reports `vl-timeunit-binned` for a binned unit (and
  `vl-timeunit-maxbins` for a `{maxbins}` form without a unit where that cannot be applied, which is in a predicate);
  `VisitBandPosition` reports `vl-encoding-bandposition` and `vl-encoding-bandposition-rect` by the rules above (a
  `bandPosition` on a field with a real, unbinned time unit is reported only for a bar or rect and a value other than 0.5);
  the `datum` that is a date-time object (`SpecParser.IsDateTimeShaped`) is `vl-encoding-datum-datetime`;
  `VisitIntervals` warns `vl-scale-nice-interval` and `vl-guide-axis-tickcount-interval` for an interval on a channel
  that is not a time scale (a temporal field has a time scale on every channel but `shape` and `strokeDash`, and a
  non-temporal field has one only when its `scale.type` is `time` or `utc`); `VisitScaleType` now also looks at temporal
  fields: it warns `vl-scale-zero-ignored` for a `scale.zero` on a temporal field whose scale type is absent, `time`, or
  `utc`, and `vl-scale-type-ignored` for a type that `ScaleResolver.AcceptsScaleType` does not accept (only `time` and `utc`
  are accepted for a temporal field) and for a temporal field on a `shape` or `strokeDash` channel without a scale type;
  `VisitFormat` no longer reports a `format.parse` entry. The static `IsEvaluatedFormatType(formatType)` tells the
  format types that are evaluated (`number`, `time`, `utc`); the axis and legend `formatType` rules use it.
- `VisitTransforms(transforms, drawn)` reports the entries as `vl-transform-<name>` only for what is **not computed**:
  the transforms of later phases (`density`, `extent`, `loess`, `quantile`, `regression`) and every
  transform of a
  view that is not drawn (a nested view of a composition, aggregating transforms and `timeunit` included). For the
  computed transforms
  of
  the drawn view (`calculate`, `filter`, `fold`, `flatten`, `sample`,
  `pivot`, `lookup`, `aggregate`, `joinaggregate`, `window`, `bin`, `stack`, `impute`, `timeUnit`) it reports only their
  unsupported
  parts: `ReportConstructs` turns the constructs that the compiled
  expression collected into `vl-expr-function-<name>`, `vl-expr-view-state`, and `vl-expr-construct-<name>` reports (and
  an undeclared view signal into `vl-expr-view-state`), sorted by their offset and located at the expression string,
  with `(at expression offset N)` in the message; `VisitPredicate` walks a filter with an explicit stack (so composition
  depth cannot overflow the call stack) and reports `vl-transform-filter-param` (a `selection`, or a `param` that names
  a selection parameter), and, through `VisitFieldPredicate`, the time unit of a field predicate (`VisitPredicateTimeUnit`:
  `vl-timeunit-binned`, `vl-timeunit-maxbins`, and the warning `vl-predicate-timeunit-part`; the retired
  `vl-transform-filter-timeunit` and `vl-transform-filter-datetime` are no longer produced); a `timeUnit` transform
  reports `vl-timeunit-binned` for a binned unit; a pivot `op` other than `sum` and `count` is
  `vl-transform-pivot-op`, and a lookup `from.param` is `vl-transform-lookup-param` while its `from.data` is visited
  like any `data` object (`vl-data-*`); a `bin` transform whose `extent` is a parameter is
`vl-transform-bin-extent-param` (the extent of the data is used). - `VisitParams(parameters, isRoot)` reports every
  selection parameter as `vl-param-selection` and, for a variable parameter, `vl-param-variable` when the view is not
  the root; a variable parameter of the root view is computed, so only the constructs of its `expr` are reported.
`VisitAutosize` reports `vl-layout-autosize-<type>` for fit modes. - The `{"expr": ...}` scan (`VisitExpressions`, with
  an explicit stack) reports as `vl-expr-ref` every expression reference that the parser did **not** compile (a title,
  size, padding, background, view, any other mark property, any other encoding value); a reference that was compiled
  (the paint properties of the mark and the `value` of the paint channels of the drawn view) is reported only for its
unsupported constructs. - `IsSchemeBearingUrl(string url)` - true for a URL that starts with `/` or a backslash (a Unix
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
  `IsChannelName`, `FindTransformOperation`, `IsSupportedTransform`, `IsDrawnView`, `IsViewSignal`, `FindParameter`), in
  the `SpecParser` unit, and the compiled expressions of the tree (`SpecExpressions`)
- **`CompiledExpression`** and **`ExpressionConstruct`** - the constructs that the parser collected, from the
  `ExpressionParser` unit of the `Expressions` subsystem
- **`SpecParser.IsSupportedSort`** - the rule for a supported encoding `sort` (in the `SpecParser` unit, over the shared
  operation registry `AggregateMeasures.CanOrderCategories` of the `Transforms` subsystem), so that the report and the
  sort of the scale resolver agree
- **`StackRules`** (the `Transforms` subsystem) and **`ScaleResolver.InferOrient`** (the `Scales` subsystem) - the
  decision that a view stacks, which decides whether the `order` channel is accepted, shared with the encoding plan so
  that the report and the pipeline decide alike
- **`TimeUnitSpec`** - the time unit reader of the `Data` subsystem (see _Time Calendar Design_), so that the report and
  the unit that is applied agree on what a binned or `maxbins` unit is
- **`ScaleResolver.AcceptsScaleType`** - the scale type rule per channel and field type (including temporal fields), in
  the `Scales` subsystem
- **`VegaLiteLocation`** - the public location type of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization` (invariant-culture formatting of messages)

#### Callers

`VegaLiteDocument` calls `Visit` once when a document is created and merges the result with the parse warnings; the
pipeline delivers the reports through the sink. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/FeatureCatalogTests.cs` call `Visit` and `IsSchemeBearingUrl`
directly through `InternalsVisibleTo`.

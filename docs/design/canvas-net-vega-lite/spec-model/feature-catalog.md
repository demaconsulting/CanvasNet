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
point, circle, square, rule, tick), the supported channels (x, y, color, fill, stroke, size, opacity), the ignored
channels (tooltip, href, description, key), the geographic channels, the legend channels, the supported scale types
(linear, band, point, ordinal), the supported scale properties (type, domain, range, zero, nice, padding, reverse,
paddingInner, paddingOuter, align, round, clamp, scheme; `scheme` is reported unless it names `tableau10`, compared
without regard to case like the scale resolver, or the scale has an explicit color `range` that overrides it), the
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
`vl-transform-` identifiers of the supported transforms are retired: those constructs are supported and absent from the
result. The warnings `vl-duplicate-key` and `vl-unknown-property` are produced by `SpecParser`, and the render-time
warnings `vl-expr-runtime` and `vl-transform-runtime` by `RowTransforms` (they need the data; the
catalog never produces them).

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
  text or as an object are reported the same way; and `VisitFormat` reports only a `format.parse` entry that names a
  custom date format (`vl-data-parse-date-format`, recognized with `ParseDirectives.TryClassify`) and a `topojson`
  format type (`vl-data-geo`). Sequences, named datasets, the csv, tsv, dsv, and json formats, parse maps, and
  `format.property` are supported and are not reported. The retired identifiers `vl-data-file`, `vl-data-named`,
  `vl-data-sequence`, `vl-data-parse`, `vl-data-format-<type>`, and `vl-data-property` are no longer produced.
- `VisitMark` reports `vl-mark-<type>` for every mark type outside the supported set. `VisitEncoding` reports the
  channels outside the supported set (`vl-channel-<name>`, `vl-channel-geo`), the field type (`vl-type-temporal`,
  `vl-type-geojson`), the encoding properties (`vl-encoding-<name>`, applying the rules for `bin`, `stack`, and `sort`
  and the conservative implicit bar-stack heuristic at the measure channel), the scale type and properties
  (`vl-scale-<type>`, `vl-scale-property-<name>`, `vl-scale-color-ramp`), the legend (`vl-guide-legend`), and the axis
  properties (`vl-guide-axis-<name>`, `vl-guide-encode`, `vl-format-type-custom`, `vl-expr-label`).
- `VisitTransforms(transforms, drawn)` reports the entries as `vl-transform-<name>` only for what is **not computed**: the
  transforms of later phases (`aggregate`, `bin`, `density`, `extent`, `impute`, `joinaggregate`, `loess`, `quantile`,
  `regression`, `stack`, `timeunit`, `window`) and every transform of a view that is not drawn (a nested view of a
  composition). For the computed transforms of the drawn view (`calculate`, `filter`, `fold`, `flatten`, `sample`,
  `pivot`, `lookup`) it reports only their unsupported parts: `ReportConstructs` turns the constructs that the compiled
  expression collected into `vl-expr-function-<name>`, `vl-expr-view-state`, and `vl-expr-construct-<name>` reports (and
  an undeclared view signal into `vl-expr-view-state`), sorted by their offset and located at the expression string, with
  `(at expression offset N)` in the message; `VisitPredicate` walks a filter with an explicit stack (so composition depth
  cannot overflow the call stack) and reports `vl-transform-filter-param` (a `selection`, or a `param` that names a
  selection parameter), `vl-transform-filter-timeunit` (a `timeUnit` in a field predicate), and `vl-transform-filter-datetime`
  (a date object in a comparison); a pivot `op` other than `sum` and `count` is `vl-transform-pivot-op`, and a lookup `from.param`
  is `vl-transform-lookup-param` while its `from.data` is visited like any `data` object (`vl-data-*`).
- `VisitParams(parameters, isRoot)` reports every selection parameter as `vl-param-selection` and, for a variable parameter,
  `vl-param-variable` when the view is not the root; a variable parameter of the root view is computed, so only the
  constructs of its `expr` are reported. `VisitAutosize` reports `vl-layout-autosize-<type>` for fit modes.
- The `{"expr": ...}` scan (`VisitExpressions`, with an explicit stack) reports as `vl-expr-ref` every expression reference
  that the parser did **not** compile (a title, size, padding, background, view, any other mark property, any other encoding
  value); a reference that was compiled (the paint properties of the mark and the `value` of the paint channels of the drawn
  view) is reported only for its unsupported constructs. `sort` is reported as `vl-encoding-sort`
  only when it is a shorthand or an object that `ScaleResolver.IsSupportedFieldSort` rejects.
- `IsSchemeBearingUrl(string url)` - true for a URL that starts with `/` or a backslash (a Unix absolute path, a
  Windows rooted path, a network or UNC path), is rooted according to `Path.IsPathRooted`, or has a scheme (a letter
  followed by letters, digits, `+`, `-`, or `.`, then a colon, which includes a drive letter and `file:` URIs); the
  result does not depend on the host operating system. Such a URL is never
  handed to a resolver, because a renderer that fetches resources named by an untrusted specification would be a
  server-side request forgery primitive.

**Preconditions and postconditions**: the tree is non-null and valid; the result is a new read-only list in document
order, empty when the specification uses only supported constructs. The catalog is stateless and thread-safe.

#### Error Handling

The catalog never throws for the content of a specification: every recognized-but-unsupported construct is a report,
and every unknown construct is left to the parser's `vl-unknown-property` warning. A null tree throws
`ArgumentNullException`, which is not converted. The unit does not read data rows, so its cost does not depend on the data
volume, and it uses an explicit view enumeration, so nested views cannot overflow the stack. Strict mode is not applied
here: the document records the reports in a sink, which throws at the first unsupported feature.

#### Dependencies

- **`SpecTree`**, **`SpecNode`**, and **`SpecParser`** - the tree and the parser's helpers (`ParseSchemaMajorVersion`,
  `IsChannelName`, `FindTransformOperation`, `IsSupportedTransform`, `IsDrawnView`, `IsViewSignal`, `FindParameter`), in
  the `SpecParser` unit, and the compiled expressions of the tree (`SpecExpressions`)
- **`CompiledExpression`** and **`ExpressionConstruct`** - the constructs that the parser collected, from the
  `ExpressionParser` unit of the `Expressions` subsystem
- **`ScaleResolver.IsSupportedFieldSort`** - the rule for a supported sort by field, in the
  `Scales` subsystem, so that the
  report and the sort agree
- **`ParseDirectives`** - the classification of a `format.parse` entry, in the `DelimitedTextParser` unit of the `Data`
  subsystem, so that the report and the loading agree on what a custom date format is
- **`VegaLiteLocation`** - the public location type of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization` (invariant-culture formatting of messages)

#### Callers

`VegaLiteDocument` calls `Visit` once when a document is created and merges the result with the parse warnings; the
pipeline delivers the reports through the sink. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/FeatureCatalogTests.cs` call `Visit` and `IsSchemeBearingUrl`
directly through `InternalsVisibleTo`.

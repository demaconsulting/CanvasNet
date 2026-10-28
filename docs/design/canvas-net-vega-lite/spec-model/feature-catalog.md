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
paddingInner, paddingOuter, align, round, clamp, scheme), the unsupported axis properties, the scopes in which an
expression value is looked for, the composition properties, and the delimited text formats. An identifier is built from
an area and a name taken from the specification, lower-cased with the invariant culture and without separators
(`timeUnit` gives `timeunit`).

**Identifiers.** The areas are `schema`, `data`, `mark`, `channel`, `type`, `scale`, `encoding`, `guide`, `format`,
`expr`, `transform`, `composition`, `param`, `projection`, and `layout`; the table in the system design lists every
identifier with its trigger. The warnings `vl-duplicate-key` and `vl-unknown-property` are produced by `SpecParser`.

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
  `vl-data-empty`; a `url` is classified with `IsSchemeBearingUrl` as `vl-data-remote-url` or `vl-data-file`; the
  `format` properties report `vl-data-parse`, `vl-data-format-<type>`, and `vl-data-property`.
- `VisitMark` reports `vl-mark-<type>` for every mark type outside the supported set. `VisitEncoding` reports the
  channels outside the supported set (`vl-channel-<name>`, `vl-channel-geo`), the field type (`vl-type-temporal`,
  `vl-type-geojson`), the encoding properties (`vl-encoding-<name>`, applying the rules for `bin`, `stack`, and `sort`
  and the conservative implicit bar-stack heuristic at the measure channel), the scale type and properties
  (`vl-scale-<type>`, `vl-scale-property-<name>`, `vl-scale-color-ramp`), the legend (`vl-guide-legend`), and the axis
  properties (`vl-guide-axis-<name>`, `vl-guide-encode`, `vl-format-type-custom`, `vl-expr-label`).
- `VisitTransforms` reports every entry as `vl-transform-<name>` (and `vl-expr-calculate` or `vl-expr-filter` for an
  expression); `VisitParams` reports `vl-param-variable` or `vl-param-selection`; `VisitAutosize` reports
  `vl-layout-autosize-<type>` for fit modes.
- `IsSchemeBearingUrl(string url)` - true for a URL that starts with `//` or a backslash, or has a scheme (a letter
  followed by letters, digits, `+`, `-`, or `.`, then a colon, which includes a drive letter); such a URL is never
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
  `IsChannelName`, `FindTransformOperation`), in the `SpecParser` unit
- **`VegaLiteLocation`** - the public location type of the `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization` (invariant-culture formatting of messages)

#### Callers

`VegaLiteDocument` calls `Visit` once when a document is created and merges the result with the parse warnings; the
pipeline delivers the reports through the sink. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/SpecModel/FeatureCatalogTests.cs` call `Visit` and `IsSchemeBearingUrl`
directly through `InternalsVisibleTo`.

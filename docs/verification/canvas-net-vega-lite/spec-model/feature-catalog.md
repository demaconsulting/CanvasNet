### FeatureCatalog Unit Verification Design

This document describes the unit-level verification strategy for the `FeatureCatalog` unit in the
`DemaConsulting.CanvasNet.VegaLite.SpecModel` namespace: the internal static `FeatureCatalog` class (`Visit` and
`IsSchemeBearingUrl`), the `FeatureReport` record, and the `FeatureReportKind` enumeration. The catalog walks a parsed
specification tree and reports every recognized construct that this release does not draw, each with a stable
lower-case `vl-*` identifier, a message, the JSON pointer, and the one-based line and column of the construct, plus the
warnings about the schema version and empty data.

#### Verification Approach

The unit is verified by parsing small, original specification strings with the real `SpecParser` and asserting on the
list of reports returned by `Visit`: the identifier, the pointer, and the kind of each report, in document order. The
assertions are written as lists of `(identifier, pointer)` pairs so that the whole expected output of a specification
is visible in the test; the expected lists are derived by hand from the identifier table in the design (the supported
subset is the complement of what is reported). Constructs that are supported or silently ignored (for example the
`tooltip` and `href` channels, an unknown channel that the parser already warns about, `bind` in a parameter) are
asserted to produce no report, so that a feature which becomes supported in a later release has to change a test. The
unit's dependencies are the parser's tree types, including the compiled expressions of the tree (the catalog reports
the unsupported constructs that they list), so no mocking or stubbing is required; the internal types
are reached through the test assembly's `InternalsVisibleTo` access.

Several scenarios are xUnit theories whose data rows cover each form (every mark type, every autosize type, every
schema version, every URL shape); this document lists each test method once. The shared helper `Visit` in
`FeatureCatalogTests` parses a specification and returns the reports as `(identifier, pointer)` pairs.

Unit tests reside in `FeatureCatalogTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`SpecModel` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test parses its own source strings; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any missing report, extra report, wrong identifier, wrong pointer, wrong line or column, wrong kind, or
wrong order constitutes a failure.

#### Unit-Level Test Scenarios

##### Supported Specification Has No Reports

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_SupportedSpecificationHasNoReports`

Asserts that an original specification that uses only supported constructs (a title with subtitle and anchor, a fixed
width and a `step` height, padding, autosize `pad`, a background, inline data, a `bar` mark with a color, a band scale
with padding, axis labels with an angle and no title, a descending sort, a linear scale with `zero`, `nice`, an
explicit domain and range, an axis with a tick count and a format, value and datum channels, legends switched off,
a `null` scale, and the silently ignored `tooltip`, `href`, `description`, and `key` channels) produces no reports
at all.

##### Recognized Unsupported Constructs Return Stable Identifiers

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_RecognizedUnsupportedPropertiesReturnsStableIds`,
`FeatureCatalogTests.FeatureCatalog_Visit_ReportsAreInDocumentOrder`,
`FeatureCatalogTests.FeatureCatalog_Visit_MarkReportCarriesLineAndColumn`

Asserts that one original specification that deliberately uses a version 4 schema, an expression title, autosize
`fit`, a variable and a selection parameter, a projection, file data with a `csv` format and a `parse` map that holds a
custom date format and a plain `number`, three transforms (`calculate`, `filter`, `aggregate`) in a root view that is a
composition (so it is not drawn and its transforms are reported by name), an `arc` mark, a temporal `x` with a time
unit, a `y` with aggregate, bin, stack, sort, and condition, a log scale with a scheme, a legend with `encode`, a
`theta` and a `latitude` channel, and a layer with a `geoshape` yields exactly twenty-five reports whose
`(identifier, pointer)` list is asserted in document order (for example `vl-data-parse-date-format` at
`/data/format/parse/d`, `vl-encoding-stack` at `/encoding/y/stack`, and `vl-mark-geoshape` at `/layer/0/mark`; the local
file, the plain `number`, the variable parameter of the root view, and the expression strings of the `calculate` and
`filter` transforms are silent, and no `vl-expr-calculate`, `vl-expr-filter`, or `vl-param-variable` exists any more),
that only the schema version is a warning while every other report is an unsupported feature, that
all carry a file, line, column, and message (the schema report is at line 2, column 14), and that the supported
`tooltip` channel is absent; that reports are sorted by position in the text and not by the order in which the
constructs are visited (an encoding written before the mark, a nested view before the parent's later properties);
and that the location of a mark report is the value of the `mark` property, with its line and column.

##### Marks And Channels

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedMarkReportsMarkId`,
`FeatureCatalogTests.FeatureCatalog_Visit_ChannelsReportExceptSupportedAndIgnored`,
`FeatureCatalogTests.FeatureCatalog_Visit_UnknownChannelIsNotReported`

Asserts, as a theory over seventeen mark types, that `bar`, `rect`, `point`, `circle`, `square`, `rule`, and `tick`
are silent and that `arc`, `area`, `image`, `line`, `text`, `trail`, `geoshape`, `boxplot`, `errorbar`, and
`errorband` each report `vl-mark-<type>` at the mark; that every channel except `x`, `y`, `color`, `fill`, `stroke`,
`size`, and `opacity` reports `vl-channel-<name>` (the four geographic channels sharing one identifier) while
`tooltip`, `href`, `description`, and `key` are accepted silently; and that an unknown channel name is not reported
again by the catalog (the parser warns about it once).

##### Data

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_RemoteUrlsRemainVlDataRemoteUrl`,
`FeatureCatalogTests.FeatureCatalog_IsSchemeBearingUrl_ClassifiesUrls`,
`FeatureCatalogTests.FeatureCatalog_Visit_LocalUrlsAndPhase4FormatsAreSupported`,
`FeatureCatalogTests.FeatureCatalog_Visit_CustomDateFormatReportsStableIdentifier`,
`FeatureCatalogTests.FeatureCatalog_Visit_DataSourcesReportTheirOwnIdentifiers`

Asserts that remote data (every scheme-bearing form) reports `vl-data-remote-url` at the data object, never as a
warning, and that a relative local path reports nothing; that, as a theory, `http`, upper-case `HTTPS`, `file`
(`file:///`, `file:/`, and `FILE://`), `data`,
`javascript`, and `ftp` URLs, a drive letter path (`C:\`, `C:name`, and `d:/`), a protocol-relative path, a Unix absolute
path (`/etc/hosts`), a backslash-rooted path, a UNC path, and a URL with leading blanks are scheme-bearing on every host
operating system, while `a.csv`, `data/a.csv`, `../a.csv`, `a b.csv`, the empty string, and `1:2.csv` are not; that
relative local urls, the csv, tsv, dsv, and json format types (with a delimiter, a `property` path, and `parse` maps of
`date`, `number`, `boolean`, and `string`, `"auto"`, and `null`), a named dataset, and a sequence are supported and
report nothing, so that strict mode accepts them; that a custom date format of a parse map (`date:'...'`, `utc:'...'`)
reports `vl-data-parse-date-format` at its entry while plain `date` does not; and that empty or sourceless inline data
warns with `vl-data-empty` while the recognized sources that are not read have their own identifiers
(`vl-data-values-text` for text values or a referenced text dataset, and `vl-data-geo` for object values, `sphere`, a
`topojson` format, or a referenced object dataset) and a referenced array dataset is silent. The retired
identifiers (`vl-data-file`, `vl-data-named`, `vl-data-sequence`, `vl-data-parse`, `vl-data-format-<type>`, and
`vl-data-property`) are not produced for any of these.

##### Schema Version

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_SchemaVersionWarnsOutsideFiveAndSix`

Asserts as a theory that schema URLs for versions 5, 6, and 5.17.0 are silent and that version 4, version 7, and a
URL that is not a Vega-Lite schema produce a `vl-schema-version` warning at `/$schema`.

##### Encoding, Scale, And Axis Rules

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_EncodingPropertiesReportOnlyWhenTheyChangeTheChart`,
`FeatureCatalogTests.FeatureCatalog_Visit_ImplicitBarStackIsReportedAtTheMeasure`,
`FeatureCatalogTests.FeatureCatalog_Visit_ScaleTypesAndPropertiesReportOutsideTheSupportedSubset`,
`FeatureCatalogTests.FeatureCatalog_Visit_ColorSchemesAndContinuousColorsReportOutsideTheSupportedSubset`,
`FeatureCatalogTests.FeatureCatalog_Visit_AxisPropertiesReportOnlyTheUnsupportedOnes`

Asserts that the encoding identifiers (`vl-encoding-aggregate`, `-bin`, `-timeunit`, `-stack`, `-sort`, `-condition`,
`-impute`, `-bandposition`) are reported only when the property changes the chart (`bin: false`, `stack: null`, a
`null` aggregate, and an ascending sort shorthand are silent, as are a `null` sort and an array sort, while a sort object
that names an `encoding` is reported at the sort); that, as a theory over nine specifications, bars that
Vega-Lite would stack by default are reported once at the measure channel and charts that it would draw unstacked
(an explicit `stack: null`, a stack-by field equal to the dimension field, an `y2` channel, a non-bar mark, a value
color, two quantitative channels) are not; that unsupported scale types and properties report
`vl-scale-<type>` and `vl-scale-property-<name>` while the supported subset does not; that a color scheme other than
`tableau10` reports `vl-scale-property-scheme` and a quantitative color field reports `vl-scale-color-ramp`; and that
guide `encode`, `labelExpr`, `formatType`, and the listed unsupported axis properties are reported while the honored
properties are not.

##### Legends

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_LegendsAreReportedOnlyWhenOneWouldBeDrawn`

Asserts that `vl-guide-legend` is reported for field-driven color, size, and opacity channels unless the legend or
the scale is `null`, and never for value definitions, the data, or position channels.

##### Transforms, Expressions, Parameters, Compositions, And Autosize

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_TransformsExpressionsAndParametersAreReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_CompositionsAreReportedAndNestedViewsExamined`,
`FeatureCatalogTests.FeatureCatalog_Visit_AutosizeFitModesAreReported`

Asserts, on a view that is drawn, that expression references in properties that are not evaluated (a `width` that is an
expression) report `vl-expr-ref` while the row-bound color expression of an encoding value is silent, that the
selection parameter reports `vl-param-selection` while the variable parameter (with its `bind`, which is ignored) is
silent, that a `filter` on a field predicate is silent while the `timeUnit` transform and the `joinaggregate` transform
report `vl-transform-timeunit` and `vl-transform-joinaggregate` at their index, and that an expression reference in a
mark `size` of a bar is reported at that property; that a specification with `hconcat`, a nested `vconcat`, `concat`,
`facet`, `spec`, `repeat`, `resolve`, and `projection` reports the compositions and the projection and also examines the
views nested at any depth (`/hconcat/0/mark`, `/hconcat/1/vconcat/0/mark`, `/spec/mark`); and that, as a theory,
autosize `pad` and `none` (also in object form) are silent while `fit`, `fit-x`, and `fit-y` report
`vl-layout-autosize-<type>` at the type.

##### Supported Phase 5 Transforms, Parameters, And Expressions Are Silent

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_SupportedPhase5TransformsAndParametersAreNotReported`

Asserts that an original specification that uses a variable parameter with a value and one with an expression and a
`bind`, the transforms `calculate`, `filter` (an expression, and a predicate composed of `and`, `not`, `range`, `oneOf`,
and a `param`), `fold`, `flatten`, `sample`, `pivot` with the `sum` operation, and a `lookup` in a named dataset with a
default, and row-bound expression references in the mark color, size, opacity, and stroke width and in the color, size,
and opacity channel values produces no reports at all, so strict mode accepts them.

##### Unsupported Expression Constructs Are Reported At The Expression

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedExpressionConstructsAreReportedAtTheExpression`,
`FeatureCatalogTests.FeatureCatalog_Visit_DifferentlyCasedFunctionNames_ShareOneLowerCaseIdentifier`

Asserts that the constructs that the compiled expressions list are reported once each, in document order, at the string
value of the expression (the pointer of the `expr`, `calculate`, or `filter` string, and of an expression inside a
predicate such as `/transform/4/filter/or/0`), as unsupported features with the message form "… (at expression offset
N)": `vl-expr-function-datetime`, `vl-expr-function-timeformat`, `vl-expr-function-rgb`, and `vl-expr-function-format` (the
name lower-cased; the message of the `timeFormat` report names it as written); `vl-expr-view-state` for `signal` and
`event` in one filter and an undeclared `height` while
`width`, which the specification declares as a parameter, is silent; and `vl-expr-construct-bitwise-and` and
`vl-expr-construct-in`. It asserts that the first report is at line 2 (the parameter's expression) and that the message
of the view-state report names `'height'`. A second scenario calls `timeFormat`, `timeformat`, and `TIMEFORMAT` in one
expression and asserts three reports with the one identifier `vl-expr-function-timeformat`, in order of appearance, with
three distinct messages that name each spelling as written (so nothing is merged or lost).

##### Transforms Of Later Phases, Conditions, And Selections Stay Reported

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_LaterPhaseTransformsConditionsAndSelectionsStayReported`

Asserts that a drawn view with a selection parameter reports `vl-param-selection`, then the transforms `aggregate`,
`joinaggregate`, `window`, `bin`, `stack`, `impute`, `timeUnit`, `regression`, `loess`, `density`, `quantile`, and
`extent` as `vl-transform-<name>` (with `timeunit` in lower case) in document order, and the conditional encoding as
`vl-encoding-condition`.

##### Unsupported Parts Of Supported Transforms Are Reported

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedPartsOfSupportedTransformsAreReported`

Asserts the reports `vl-transform-filter-param` at the `param` and at the legacy `selection` of a filter on a selection
parameter, `vl-transform-filter-timeunit` at the `timeUnit` of a predicate, `vl-transform-filter-datetime` at a date
object in `oneOf` and in `equal`, `vl-transform-pivot-op` at an `op` other than `sum` and `count` (and none for
`count`), `vl-transform-lookup-param` at a lookup from a parameter, and `vl-data-remote-url` at the data object of a
lookup whose source is a remote URL, each at the pointer of the part, in document order, together with the selection
parameter itself.

##### Nested Views Report Their Transforms And Variable Parameters

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_NestedViewTransformsAndVariableParametersAreReported`

Asserts that, in a layer, the variable parameter of a nested view reports `vl-param-variable` and every transform of the
nested view (a `calculate` and a `lookup`) reports `vl-transform-<name>`, because only the root view is drawn, while the
variable parameter of the root reports nothing.

##### Expression References Are Reported Only At Deferred Sites

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_ExpressionReferencesAreReportedOnlyAtDeferredSites`

Asserts that `vl-expr-ref` is reported for an expression in a title, a width, a bar's `size`, a position channel
`value`, and a `size` channel value of a bar (not row-bound for a bar), in document order, while the mark `fill` with an
expression and a color channel value with an expression are silent (their functions, `toString` and `regexp`, are in the
function table, so no construct of an evaluated site is reported either).

##### Sort By A Field Is Reported Only For Unsupported Forms

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_SortByFieldIsReportedOnlyForUnsupportedForms`

Asserts, as a theory of twelve sort forms, that `ascending`, an array, a sort object with an `op` of `sum`, `count`, or
`median` and an optional field and order (also an `order` of null), are silent, and that the forms that the scale
resolver does not compute — an `encoding`, a missing `op` with only a `field`, the operation `valid`, an unknown order,
an unparsable field, and the shorthand `-y` — report `vl-encoding-sort`.

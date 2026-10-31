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
`FeatureCatalogTests` parses a specification and returns the reports as `(identifier, pointer)` pairs; the Phase 8
class `FeatureCatalogTimeTests` has its own `Visit` and `VisitView` helpers of the same kind.

Unit tests reside in `FeatureCatalogTests.cs` and `FeatureCatalogTimeTests.cs` within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `SpecModel` folder; the test of the channels that a mark does
not draw is in `MarkChannelGatingTests.cs` in the `Scales` folder, which owns the support table that the catalog
follows. Time is supported since Phase 8, so the
tests of the earlier phases that asserted that a temporal field, a time unit, a custom date format, the `timeUnit`
transform, or a time predicate was reported were changed to assert that nothing is reported (the identifiers that were
retired are asserted absent), and the expression tests that used `datetime` and `timeFormat` as an example of an
unsupported function now use `geoArea` and `gradient`, which are still outside the function table.

The line, area, trail, text, and arc marks are drawn since Phase 9, so the tests that used one of them as the
example of an unsupported mark were migrated: the sentinel of the unsupported mark is now `geoshape` (with
`longitude` for the channel that no mark draws, reported as `vl-channel-geo`), and `image` (which stays unsupported)
replaces `text` where a second unsupported mark is needed. The migrated assertions are the `geoshape` and the
`longitude` and `latitude` of the stable-identifier specification, the mark list of the mark theory (`arc`, `area`,
`line`, `text`, and `trail` are silent), the nested marks of the composition test (`geoshape` and `image`), and the
mark of the document-order test; the test that listed `vl-channel-detail` and `vl-channel-order` among the reported
channels no longer does, because every mark accepts those two. The new tests of the phase are the
`FeatureCatalog_Report_` tests, whose expected values are lists of identifiers and pointers (and kinds) written by hand
from the identifier table: they assert the whole report of each mark, channel, and property, and that what the renderer
draws is silent.

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
custom date format and a plain `number`, three transforms (`calculate`, `filter`, `regression`) in a root view that is a
composition (so it is not drawn and its transforms are reported by name), a `geoshape` mark, a temporal `x` with a time
unit (supported, so it is silent), a `y` with aggregate, bin, stack, sort, and condition, a color with `bin` (supported,
so it is silent), a log scale on a nominal field with a scheme
(which
cannot use the type, so it is the warning `vl-scale-type-ignored`), a legend with
`encode`, a `longitude` and a `latitude` channel, and a layer with a `geoshape` yields exactly seventeen reports whose
`(identifier, pointer)` list is asserted in document order (for example `vl-encoding-condition` at
`/encoding/y/condition` and `vl-mark-geoshape` at `/layer/0/mark`; the custom date format at `/data/format/parse/d`,
the temporal type and time unit of `x` (`vl-type-temporal` and `vl-encoding-timeunit` of the earlier phases, now
retired), the local
file, the plain `number`, the variable parameter of the root view, the expression strings of the `calculate` and
`filter` transforms, and the `aggregate`, `bin`, `stack`, and `sort` of `y` are silent, and no `vl-expr-calculate`,
`vl-expr-filter`, `vl-param-variable`, `vl-encoding-aggregate`, `vl-encoding-stack`, or `vl-encoding-sort` is produced for
them), that the schema version, the ignored sort, and the ignored scale type are the only warnings while
every other report is an unsupported feature,
that
all carry a file, line, column, and message (the schema report is at line 2, column 14), and that the supported
`tooltip` channel is absent; that reports are sorted by position in the text and not by the order in which the
constructs are visited (an encoding written before the mark, a nested view before the parent's later properties);
and that the location of a mark report is the value of the `mark` property, with its line and column.

##### Marks And Channels

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedMarkReportsMarkId`,
`FeatureCatalogTests.FeatureCatalog_Visit_ChannelsReportExceptSupportedAndIgnored`,
`FeatureCatalogTests.FeatureCatalog_Visit_ShapeAngleAndDashChannelsAreReportedForMarksThatDoNotDrawThem`,
`FeatureCatalogTests.FeatureCatalog_Visit_UnknownChannelIsNotReported`,
`MarkChannelGatingTests.FeatureCatalog_Visit_ReportsChannelsThatTheMarkDoesNotDraw`

Asserts, as a theory over seventeen mark types, that `bar`, `rect`, `point`, `circle`, `square`, `rule`, `tick`, `arc`,
`area`, `line`, `text`, and `trail` are silent (written as a string and as a definition, in two layers) and that
`image`, `geoshape`, `boxplot`, `errorbar`, and `errorband` each report `vl-mark-<type>` at the mark; that on a `point`
mark every channel except `x`, `y`, `theta`, `radius`, `theta2`, `radius2`, `text`, `color`, `fill`, `stroke`, `size`,
`opacity`, `shape`, `strokeWidth`, `strokeDash`, `fillOpacity`, `strokeOpacity`, and `angle` reports
`vl-channel-<name>` (the test shows `x2`, `text`, `row`, and `xOffset`, while the geographic `longitude` and `latitude2`
share the one identifier `vl-channel-geo`) while `tooltip`, `href`, `description`, and `key` are accepted silently, and
so are `detail` and `order` (every mark accepts them, so neither identifier exists any more), with the exception of the
secondary position channels, which are reported when the mark cannot draw the range (see the encoding rules below), as
the `x2` of a point mark shows; that, as a theory over nine marks and channels, `shape` is
silent for a `point` and reported with its own identifier (and ignored) for a `circle` or a `bar`, `angle` is silent for
a `point` and a `square` and reported for a `tick`, and `strokeDash` is silent for a `bar` and a `rule` and reported for
a `rect`; that an unknown channel name is not reported
again by the catalog (the parser warns about it once); and that the channels that a mark does not draw are reported at
the channel and ignored, by the table of the marks that draw each channel: a `size` on an `area` (a field) or on an
`arc` (a value) reports `vl-channel-size`, a `strokeDash` on a `trail`, on a `text`, and on an `area` reports
`vl-channel-strokedash`, a `shape` or an `angle` on a `line` reports `vl-channel-shape` and `vl-channel-angle`, while
the drawn channels are silent (a `size` and a `strokeDash` on a `line`, a `size` and an `angle` on a `text`, a `size` on
a `trail`, a `size` and a `strokeDash` on a `bar`, and a `size` on a `rect`, which is the long-standing exception that
the catalog does not report).

##### Data

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_RemoteUrlsRemainVlDataRemoteUrl`,
`FeatureCatalogTests.FeatureCatalog_IsSchemeBearingUrl_ClassifiesUrls`,
`FeatureCatalogTests.FeatureCatalog_Visit_LocalUrlsAndPhase4FormatsAreSupported`,
`FeatureCatalogTests.FeatureCatalog_Visit_CustomDateFormatIsSupportedAndReportsNothing`,
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
is supported (the data stage applies it), so the catalog reports nothing for it and the retired identifier
`vl-data-parse-date-format` never appears, which means that strict mode does not stop at it; and that empty or
sourceless inline data
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
`FeatureCatalogTests.FeatureCatalog_Visit_ImplicitBarStackIsNotReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_OrderChannelIsAcceptedForEveryMark`,
`FeatureCatalogTests.FeatureCatalog_Visit_SecondaryPositionChannelsAreAcceptedForRanges`,
`FeatureCatalogTests.FeatureCatalog_Visit_StackOnANonLinearScaleIsReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_ScaleTypesAndPropertiesReportOutsideTheSupportedSubset`,
`FeatureCatalogTests.FeatureCatalog_Visit_LogPowSqrtAndSymlogScalesAreSupported`,
`FeatureCatalogTests.FeatureCatalog_Visit_BinOnScaleChannelsAndTheBinOrdinalScaleAreSupported`,
`FeatureCatalogTests.FeatureCatalog_Visit_ContinuousColorsAndSchemesAreSupported`,
`FeatureCatalogTests.FeatureCatalog_Visit_CubehelixInterpolationIsReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_CustomPathShapesAreReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_ShapeAngleAndDashChannelsAreReportedForMarksThatDoNotDrawThem`,
`FeatureCatalogTests.FeatureCatalog_Visit_ScaleTypeIgnoredWarnsAboutTypesThatCannotBeUsed`,
`FeatureCatalogTests.FeatureCatalog_Visit_SchemeNamesAreNotReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_AxisPropertiesReportOnlyTheUnsupportedOnes`

Asserts that the encoding identifiers that remain (`vl-encoding-bin` for `bin: "binned"` on a channel other than `x` and
`y`,
`vl-encoding-impute` (narrowed by Phase 9 to a channel other than `x` and `y`: see the path mark scenario below), and
`vl-encoding-bandposition`, which is narrowed by Phase 8: see the band position scenario
below) are reported only when the property changes the chart (`bin: false`
and `null`, `stack: false` and `null`, a `null` aggregate, an ascending sort shorthand, a `null` sort, an array sort, and
a sort that names an `encoding` are silent, as are `timeUnit: null` and `impute: null`), and that the identifiers of the
features that are computed (`vl-encoding-aggregate`, `vl-encoding-stack`, and the supported forms of `vl-encoding-sort`)
are not produced; that, as a theory over five specifications, no bar chart is reported for being stacked implicitly,
whatever its channels (a color, a horizontal bar with a detail channel, a size channel, an explicit `stack: null`, and a
`y2` channel); that the `order` channel is accepted without a report by every mark, as a theory over nine
specifications shows (the renamed test: it said before that the channel is accepted only when it orders a stack):
a vertical bar with a color, an array of order fields with a descending one, a bar with an explicit `stack: null`, a
`point` mark with and without a stack, a bar with two measures that Vega-Lite reads as vertical, a horizontal bar, a
`line` with a detail channel, and a `tick` with an `order` that is a null value and a detail channel, so that neither
`vl-channel-order` nor `vl-channel-detail` is produced; that a secondary position channel is silent for a `bar`, `rect`,
`rule`, or `area` that has the channel it extends (the area rows are the Phase 9 addition: an area with `y` and `y2`)
and is reported as `vl-channel-x2` or `vl-channel-y2` for a `line`, a `point`, or a `tick`, for an area without the
primary channel, or for a bar without the primary channel; that
an explicit `stack` on a log scale reports `vl-encoding-stack-nonlinear` at the stack (and nothing else: the log type is
supported, and the message says that the marks are not stacked); that the unsupported scale type `identity` and the
property `bins` report
`vl-scale-<type>` and `vl-scale-property-<name>` while the supported subset does not, that the `bin` of a color, fill,
stroke, size, opacity, stroke width, and the other channels that have a scale and the `bin-ordinal` scale type of a
binned quantitative color report nothing, that `bin: "binned"` on a channel other than `x` and `y` and a `bin` of a secondary
channel report `vl-encoding-bin` at the `bin`, and that a `bin-ordinal` type on a field that is not binned, on a
nominal field, and on a size, shape, or opacity warns `vl-scale-type-ignored`, and that the log, pow, sqrt, and
symlog types with their `base`, `exponent`, and `constant` (on position, size, color, and opacity channels) produce no
report at all; that continuous color scales, the
color schemes of every kind, and the scheme object, `domainMid`, `domainMin`, `domainMax`, `rangeMin`, `rangeMax`, and
`interpolate` produce no report (the identifier `vl-scale-color-ramp` is retired), while a cubehelix interpolation reports
`vl-scale-interpolate-cubehelix`, a shape that is a custom path reports `vl-shape-path` (the mark, the channel value, and
the scale range) and every symbol name is silent; that the shape, angle, and stroke dash channels are silent for the marks
that draw them (`point`; `point`, `circle`, `square`, and `text`; every mark but `rect`, `area`, `trail`, `text`, and
`arc`) and reported and ignored for the others (the rows of the theory are the Phase 8 ones; the gating of the Phase 9
marks follows the support table and is asserted in the scenario of the marks and channels above);
that an explicit scale type that the channel or the field type cannot use warns `vl-scale-type-ignored` (a quantitative
field on `shape` or `strokeDash` as well) while accepted types and types reported otherwise do not; and that
guide `encode`, `labelExpr`, `formatType`, and the listed unsupported axis properties are reported while the honored
properties are not.

##### Legends

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_LegendsAreDrawnAndOnlyTheirUnsupportedPropertiesAreReported`

Asserts that legends are not reported (the identifier `vl-guide-legend` is retired), that the legend properties that are
not drawn are reported once each at the exact property (`vl-guide-legend-titleorient` for a title orient other than
`top`, `vl-guide-legend-clipheight`, `vl-expr-label`, `vl-format-type-custom` (for a format type that is not `number`,
`time`, or `utc`: the test uses `myFormat`), `vl-shape-path` for a symbol type that
is a path, and `vl-guide-encode`), in document order, and that a `titleOrient` of `top`, a `null` legend, a null scale,
and a legend object on a position channel produce no report.

##### Transforms, Expressions, Parameters, Compositions, And Autosize

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_TransformsExpressionsAndParametersAreReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_CompositionsAreReportedAndNestedViewsExamined`,
`FeatureCatalogTests.FeatureCatalog_Visit_AutosizeFitModesAreReported`

Asserts, on a view that is drawn, that expression references in properties that are not evaluated (a `width` that is an
expression) report `vl-expr-ref` while the row-bound color expression of an encoding value is silent, that the
selection parameter reports `vl-param-selection` while the variable parameter (with its `bind`, which is ignored) is
silent, that a `filter` on a field predicate is silent and so is the `timeUnit` transform of a drawn view (computed since
Phase 8, so the former `vl-transform-timeunit` is not produced for it; the `joinaggregate` of the same list is computed
and silent), and that an expression reference in a
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
and opacity channel values produces no reports at all, so strict mode accepts them. The grouped transforms are covered
by the Phase 6 scenario below.

##### Unsupported Expression Constructs Are Reported At The Expression

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedExpressionConstructsAreReportedAtTheExpression`,
`FeatureCatalogTests.FeatureCatalog_Visit_DifferentlyCasedFunctionNames_ShareOneLowerCaseIdentifier`

Asserts that the constructs that the compiled expressions list are reported once each, in document order, at the string
value of the expression (the pointer of the `expr`, `calculate`, or `filter` string, and of an expression inside a
predicate such as `/transform/4/filter/or/0`), as unsupported features with the message form "… (at expression offset
N)": `vl-expr-function-geoarea`, `vl-expr-function-gradient`, `vl-expr-function-rgb`, and `vl-expr-function-format` (the
name lower-cased; the message of the `gradient` report names it as written; the date and time functions that the first
three phases used for this example, `datetime` and `timeFormat`, are in the function table since Phase 8 and are no
longer reported); `vl-expr-view-state` for `signal` and
`event` in one filter and an undeclared `height` while
`width`, which the specification declares as a parameter, is silent; and `vl-expr-construct-bitwise-and` and
`vl-expr-construct-in`. It asserts that the first report is at line 2 (the parameter's expression) and that the message
of the view-state report names `'height'`. A second scenario calls `gradient`, `GRADIENT`, and `Gradient` in one
expression and asserts three reports with the one identifier `vl-expr-function-gradient`, in order of appearance, with
three distinct messages that name each spelling as written (so nothing is merged or lost).

##### Transforms Of Later Phases, Conditions, And Selections Stay Reported

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_LaterPhaseTransformsConditionsAndSelectionsStayReported`

Asserts that a drawn view with a selection parameter and the twelve transforms `aggregate`, `joinaggregate`, `window`,
`bin`, `stack`, `impute`, `timeUnit`, `regression`, `loess`, `density`, `quantile`, and `extent` reports only
`vl-param-selection`, then the transforms of later phases as `vl-transform-<name>` (`regression`, `loess`, `density`,
`quantile`, and `extent`) at their indexes 7 to 11 in document order, and the
conditions on a parameter as `vl-encoding-condition-param`, while the six aggregating transforms and the `timeUnit`
transform at index 6 (computed since Phase 8; the former `vl-transform-timeunit` is not produced) are silent.

##### Phase 6 Features Are Silent And The Remaining Parts Are Reported

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_Phase6FeaturesAreNotReportedAndRemainingPartsAre`,
`FeatureCatalogTests.FeatureCatalog_Visit_NestedViewAggregatingTransformsAreStillReported`

Asserts that a bar with a binned `x` whose bin extent is a parameter, an aggregated and normalized `y` on a linear scale,
a binned color, a size sorted by the `encoding` `y`, an `x2` range, and an `order` field reports only
`vl-encoding-bin-extent-param` at `/encoding/x/bin/extent` (the binned color is supported and silent; the `x2`
draws a range, the `order` orders the stack, and the aggregate, the stack, and the sort are computed); and that every
aggregating transform of a nested view, which is not drawn, is still reported by name (`vl-transform-aggregate`,
`-joinaggregate`, `-window`, `-bin`, `-stack`, and `-impute` at `/layer/0/transform/0` to `/5`, after
`vl-composition-layer`), while for the drawn view only the bin extent that is a parameter is reported
(`vl-transform-bin-extent-param` at `/transform/0/bin/extent`) and an extent given as numbers is silent.

##### Unsupported Parts Of Supported Transforms Are Reported

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedPartsOfSupportedTransformsAreReported`

Asserts the reports `vl-transform-filter-param` at the `param` and at the legacy `selection` of a filter on a selection
parameter, `vl-transform-pivot-op` at an `op` other than `sum` and `count` (and none for
`count`), `vl-transform-lookup-param` at a lookup from a parameter, and `vl-data-remote-url` at the data object of a
lookup whose source is a remote URL, each at the pointer of the part, in document order, together with the selection
parameter itself. Since Phase 8 a time unit and a date-time object in a predicate are evaluated, so neither
`vl-transform-filter-timeunit` (at the `timeUnit` of a predicate) nor `vl-transform-filter-datetime` (at a date object in
`oneOf` and in `equal`) is produced; instead the same transform list asserts the time reports at the exact value:
`vl-predicate-timeunit-part` at the `timeUnit` of a filter with the unit `yearweek` (`/transform/9/filter/timeUnit`),
`vl-timeunit-maxbins` at a filter with `{"maxbins": 10}` (`/transform/10/filter/timeUnit`), `vl-timeunit-binned` at a
filter with a binned object unit (`/transform/11/filter/timeUnit`) and at a `timeUnit` transform with the name
`binnedyearmonth` (`/transform/12/timeUnit`), while the `timeUnit` transform with `{"maxbins": 10}` is silent because
the transform applies `maxbins`.

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

Asserts, as a theory of sixteen sort forms, that `ascending`, an array, a sort object with an orderable `op` (`sum`,
`count`, `median`, `valid`, or `q1`) and an optional field and order (also an `order` of null), a sort object with
only a `field`, the `encoding` form that names a channel of the encoding (`y`), and the channel shorthands `-y` and `x`
are silent, and that the forms that the scale resolver does not compute — an `encoding` that names a channel the
encoding does not have (`color`), the operations `values` and `exponential`, and the shorthand `-color` — report `vl-encoding-sort`.

##### Sort Diagnostics: A Missing Channel Is Named And A Continuous Scale Ignores The Sort

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_SortOfAMissingChannelNamesTheChannel`,
`FeatureCatalogTests.FeatureCatalog_Visit_SortOnAContinuousScaleIsIgnoredWithAWarning`

Asserts, as a theory of five encodings, that a sort that refers to a channel the encoding lacks (`{"encoding":"y"}`
without a `y`, `"-y"` when `y` has only a `value`, the shorthand `color`, and `{"encoding":"size"}` with a `size` that
has only a `value`) is one unsupported `vl-encoding-sort` at `/encoding/x/sort` whose message says that the sort refers
to the named channel and that the categories are sorted ascending, and that an operation that cannot order categories
(`values`) keeps the operation message; and, as a theory of thirteen definitions, that a sort array (also an empty one),
a channel shorthand, and a sort object with a field or an operation on a quantitative field with a continuous scale (or
an explicit `linear` scale) are one `vl-encoding-sort-ignored` warning at the sort, while `ascending`, `descending`, `null`,
an object that has only an order, a nominal or ordinal field, and a quantitative field with a `band` scale type are not
reported.

##### Conditional Encodings Are Reported Only Where They Are Not Applied

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_ConditionsWithATestAreNotReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_ParameterConditionsAreReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_TimeUnitAndDateTestsAreSupported`,
`FeatureCatalogTests.FeatureCatalog_Visit_ConditionOnAChannelThatTakesNoneIsReported`,
`FeatureCatalogTests.FeatureCatalog_Visit_ConditionExpressionsAndFieldPropertiesAreVisited`

Assert, as a theory of five color definitions, that a condition with an expression test, with a predicate object, with
a composition of a range and a negated set, with a field, and an array of conditions on a field are not reported; that a
condition on a `param` is `vl-encoding-condition-param` at the condition (at the item of an array), and a selection
inside a composition at the `param` of the test; that a `timeUnit` and a date object inside an `or` are supported and
not reported (the identifier `vl-encoding-condition-test` is retired), that the `yearweek` unit of a predicate is the
warning `vl-predicate-timeunit-part` at the `timeUnit` of the test (`/encoding/color/condition/test/timeUnit`), and that
a binned unit is `vl-timeunit-binned` at the same place; that a condition on
`x` is `vl-encoding-condition` at the condition; and that a function outside the list in a test or in a `value.expr`
is `vl-expr-function-now` at the text, a `temporal` field in a field condition is silent (the former `vl-type-temporal`
is retired), and a `geojson` field is `vl-type-geojson` at its `type`.

##### Time Is Supported: Temporal Fields, Units, Scales, And Predicates Report Nothing

**Tests**: `FeatureCatalogTimeTests.FeatureCatalog_Visit_TemporalFieldsAndTimeUnitsAreSupported`,
`FeatureCatalogTimeTests.FeatureCatalog_Visit_NestedTimeUnitTransformIsReportedAndRemovedIdsAreGone`

Asserts that one original specification that uses the whole supported time subset produces no reports at all, so strict
mode accepts it: two `timeUnit` transforms (`yearmonth`, and an object unit with a step and `utc`), a filter with a
`timeUnit` and a `range`, a filter with a date-time object in `oneOf`, a bar with a temporal `x` with the unit
`yearmonth`, the scale type `utc`, a `nice` of `month`, a domain of two date-time objects, an axis `tickCount` object
and a format, and a temporal color with a `time` scale and a legend `tickCount` of `year`. The identifiers that said
that time was not supported are retired and must not appear: a second specification that uses a temporal `x` with a
unit, a filter with a unit, a filter with a date-time object, and a condition with a unit predicate produces no report
at all (so none of `vl-type-temporal`, `vl-encoding-timeunit`, `vl-transform-timeunit` for a drawn view,
`vl-transform-filter-timeunit`, `vl-transform-filter-datetime`, `vl-encoding-condition-test`, `vl-data-parse-date-format`,
`vl-scale-time`, and `vl-scale-utc`); and a `timeUnit` transform in a nested view of a composition, which is not drawn,
is still reported by the generic rule as `vl-transform-timeunit` at `/layer/0/transform/0`.

##### Time Unit Reports: Binned And Maxbins

**Tests**: `FeatureCatalogTimeTests.FeatureCatalog_Visit_BinnedTimeUnitIsReportedAtTheValue`,
`FeatureCatalogTimeTests.FeatureCatalog_Visit_MaxBinsTimeUnitIsReportedInPredicatesOnly`,
`FeatureCatalogTimeTests.FeatureCatalog_Visit_WeekAndDayOfYearPartsOfPredicatesAreWarnings`,
`FeatureCatalogTimeTests.FeatureCatalog_Visit_TimeIdsAreDeliveredAndStrictModeThrows`

Asserts that a binned unit, by the name `binnedyearmonth` or by the object `{"unit":"yearmonth","binned":true}`, is
`vl-timeunit-binned` at the `timeUnit` value (`/encoding/x/timeUnit`) while `binned: false` is silent; that the
`{"maxbins": 10}` form is silent in an encoding and in the `timeUnit` transform (where the extent of the data resolves
it) and is `vl-timeunit-maxbins` in a predicate (`/transform/0/filter/timeUnit`), which has no data extent to bin; that
a predicate whose unit has a `week` or `dayofyear` part (`yearweek` in a filter, `dayofyear` inside an `and`) is the
warning `vl-predicate-timeunit-part` at the `timeUnit` of the predicate (`/transform/0/filter/timeUnit` and
`/transform/1/filter/and/0/timeUnit`), of kind `Warning`, while `yearmonth` in a predicate and `yearweek` in an
encoding are silent. Through the public API, a render in strict mode of a specification with a binned unit throws a
`VegaLiteUnsupportedFeatureException` whose `Feature` is `vl-timeunit-binned`, the same specification rendered leniently
delivers exactly one report `vl-timeunit-binned /encoding/x/timeUnit` to the unsupported-feature handler, and the
`yearweek` predicate is delivered as a warning (its message names the week) and does not throw in strict mode.

##### Band Position, Datum, And Interval Reports

**Tests**: `FeatureCatalogTimeTests.FeatureCatalog_Visit_BandPositionIdentifiers`,
`FeatureCatalogTimeTests.FeatureCatalog_Visit_DatumDatetimeIsReported`,
`FeatureCatalogTimeTests.FeatureCatalog_Visit_IntervalsOnScalesThatAreNotTimeScales`

Asserts that a `bandPosition` of 0.25 on the bar of a field with a time unit is `vl-encoding-bandposition-rect` at
`/encoding/x/bandPosition`, while 0.5 and the same value on a point are silent; that a `timeUnitBandPosition` that is
not 0.5 is reported at the place that sets it, in the mark definition of a `rect`
(`/mark/timeUnitBandPosition`), in `config.bar` (`/config/bar/timeUnitBandPosition`), or in `config.mark`
(`/config/mark/timeUnitBandPosition`), and a tick is silent; and that the narrowed `vl-encoding-bandposition` remains
for a `bandPosition` on a quantitative field and on a binned field (`/encoding/x/bandPosition`). It asserts that a
date-time object as the `datum` of a channel is `vl-encoding-datum-datetime` at the object (`/encoding/x/datum`), while
a number and the repeat reference `{"repeat":"column"}` are silent. It also asserts that an interval on a channel that
has no time scale is a warning that the default is used: on a quantitative `x` and color, a `nice` of `month` is
`vl-scale-nice-interval` at `/encoding/x/scale/nice`, and the `tickCount` objects of the axis and the legend are
`vl-guide-axis-tickcount-interval` at `/encoding/x/axis/tickCount` and `/encoding/color/legend/tickCount`, while the
same properties on a temporal field with the type `time` or with the default scale are silent, a number `nice` and
`tickCount` are silent, and a temporal field with the type `linear` reports only `vl-scale-type-ignored` at the type.

##### Scale Types And Zero Of Temporal Fields

**Tests**: `FeatureCatalogTimeTests.FeatureCatalog_Visit_TimeAndUtcScaleTypesFollowVegaLite`,
`FeatureCatalogTimeTests.FeatureCatalog_Visit_ZeroOnATimeScaleIsAWarning`

Asserts that a temporal field accepts only the `time` and `utc` scale types: `utc` and `time` on `x`, `y`, and color
are silent, while `band`, `sequential`, and `quantile` on temporal `x`, color, and size are each
`vl-scale-type-ignored` at the type (`/encoding/x/scale/type`, `/encoding/color/scale/type`, and
`/encoding/size/scale/type`), a temporal field on `shape` is `vl-scale-type-ignored` at the scale type when it names
`time` and at the field `type` (`/encoding/shape/type`) when it names none (a channel that shows categories gets an
ordinal scale), and `time` or `utc` on a nominal, ordinal, or quantitative field is the same warning. It also asserts
that a `scale.zero` on a temporal field whose scale type is absent or `utc` is the warning `vl-scale-zero-ignored` at
`/encoding/x/scale/zero` and `/encoding/y/scale/zero` (Vega-Lite drops it for a time scale), that `zero` on a
quantitative field is silent, and that a temporal field with the type `linear` and `zero` reports only the ignored type.

##### Format Types Are Narrowed

**Tests**: `FeatureCatalogTimeTests.FeatureCatalog_Visit_FormatTypesNumberTimeAndUtcAreEvaluated`

Asserts, as a theory of five format types on the axis and on the legend of a temporal field, that `number`, `time`, and
`utc` report nothing and that `custom` and the wrongly cased `Time` report `vl-format-type-custom` at the `formatType`
(`/encoding/x/axis/formatType` and `/encoding/color/legend/formatType`), and that the static
`FeatureCatalog.IsEvaluatedFormatType` agrees with each row. The legend scenario of the earlier phases was changed to
use the format type `myFormat` for its report, because `time` is evaluated now.

##### Path Marks Are Drawn, And Their Remaining Parts Are Reported

**Tests**: `FeatureCatalogTests.FeatureCatalog_Report_LineAreaTrailAreSupportedAndGeoshapeImageAreNot`,
`FeatureCatalogTests.FeatureCatalog_Report_ChannelGatingFollowsTheSupportTable`,
`FeatureCatalogTests.FeatureCatalog_Report_InvalidShowDomainsAndBundleAreaIds`,
`FeatureCatalogTests.FeatureCatalog_Report_ConfigValuesOfInvalidAndInterpolateAreReportedAtTheConfig`,
`FeatureCatalogTests.FeatureCatalog_Report_ImputeIsReportedOnlyOutsideXAndY`,
`FeatureCatalogTests.FeatureCatalog_Report_StackImputeBinWarnsAboutABinnedDimension`

Asserts that a `line`, an `area`, and a `trail`, written with every property that the mark builder reads
(`interpolate`, `tension`, `invalid` of `break-paths-show-path-domains`, `order: false`, a stroke dash and its offset, the
cap, the join, the miter limit, a `point` object, and `line: true`) and bare, produce no report, while `geoshape`,
`image`, `boxplot`, `errorbar`, and `errorband` are reported once, as `vl-mark-<type>` at `/mark` (and nothing for the
`invalid` and `interpolate` that they carry). As a table of eighteen mark and channel pairs, with the encoding of a
quantitative `x` and a `y` without a stack, it asserts that an `area` with a `y2` or an `x2` is silent while a `line`
with a `y2` is `vl-channel-y2` and a `trail` with an `x2` is `vl-channel-x2`; that a `size` is `vl-channel-size` on an
`area` and silent on a `line` and a `trail`; that a `strokeDash` is silent on a `line` and `vl-channel-strokedash` on an
`area` and a `trail`; that a `shape` on a `line` and an `angle` on an `area` are reported; that `detail` and `order`
(an array, a field, and a null value) are silent on a `line`, an `area`, and a `trail`, and a `detail` on a bar; and
that a `text` and a
`theta` on a `line` are `vl-channel-text` and `vl-channel-theta`. It asserts that the invalid mode `show` of a line and
the `null` of an area are `vl-mark-invalid-show`, unsupported, at `/mark/invalid`; that `filter` and
`break-paths-filter-domains` are `vl-mark-invalid-domains`, of kind `Warning`, at the same place; that
`break-paths-show-domains` and `break-paths-show-path-domains` are silent; and that the `bundle` interpolation of an
area is `vl-mark-interpolate-bundle-area`, unsupported, at `/mark/interpolate`, while the same interpolation of a line
and the `step` interpolation of an area are silent. The configuration scenario writes the mark with no property of
its own and gives the value in the configuration, and asserts the same reports at the value in the configuration:
`show` at `/config/line/invalid`, a null at `/config/mark/invalid` (for an area written as a mark definition),
`filter` of `config.mark` on a trail as the warning `vl-mark-invalid-domains`, the bundle interpolation of an area
from `config.area` and `config.mark` as `vl-mark-interpolate-bundle-area`, and an `interpolate` of `zigzag` (a
line, from `config.line`) or `catmull-rom-open` (an area, from `config.mark`) as `vl-mark-interpolate-unknown`;
silence for the configuration of another mark type, for the bundle of a line, for an unknown name on a trail, a
bar, or a number, and for a mode that keeps the domains; the precedence mark definition over `config.line` over
`config.mark`, a `config.style` entry of the mark's `style` over those (the later style of an array winning) and
the entry named by the mark type itself, as the mark builder reads them; that a value in `config.mark` used by a
`line` and a `point` of a layer is reported once; and that an unsupported mark is reported at the mark only. For
the impute it asserts, on a bar chart so that no mark report is
mixed in, that an impute object on `x` and on `y` (with `value`, `method`, and `keyvals`) is silent because the pipeline
runs it, that the one on a `color` is `vl-encoding-impute`, unsupported, at `/encoding/color/impute` with a message that
names the channel, and that `impute: null` on a `size` is silent. For the stack impute it asserts that the warning
`vl-encoding-stack-impute-bin` is reported, of kind `Warning`, at the dimension definition (`/encoding/x` for an area
with a binned `x`, a color layer, and a quantitative measure, and for a line whose `y` has `stack: "zero"`;
`/encoding/y` for an area whose binned dimension is `y`), with a message that names the binned `x` channel, and is not
reported without the bin, without a layer, with `impute: null` or `stack: null` on the measure, for a bar, or for a
line that is not stacked, while an impute object on the stacked channel does not switch the warning off.

##### The Text Mark Is Drawn And Its Channels Are Gated

**Tests**: `FeatureCatalogTests.FeatureCatalog_Report_TextIsSupportedAndItsChannelsAreGated`

Asserts that the `text` mark produces no report, as a string and as a definition with its own properties (`text` as a
list, `lineBreak`, `limit`, and a numeric `fontWeight`), that a text mark with a size field, an angle value, and a `text`
channel with a number format and a `bin` is silent, as are a temporal `text` with the format type `utc` and a `text`
value that is a list; that each of the ten other marks that carry encodings (point, circle, square, bar, rect, tick,
rule, line, area, and trail) reports `vl-channel-text` at `/encoding/text` and ignores the channel; that a custom format
type of the `text` channel reports `vl-format-type-custom` at the `formatType` (`/encoding/text/formatType`) while the
types `number`, `time`, and `utc` report nothing; and that a `bin: "binned"` on a `text` channel reports
`vl-encoding-bin` at the `bin` while a plain `bin` is silent.

##### The Arc Mark Is Drawn And The Polar Channels Are Gated

**Tests**: `FeatureCatalogTests.FeatureCatalog_Report_ArcIsSupportedAndPolarChannelsAreGated`

Asserts that the `arc` mark produces no report, as a string and as a definition with `innerRadius`, `outerRadius`,
`radius`, `radius2`, `theta`, `theta2`, `padAngle`, and `cornerRadius`, and with a stacked `theta`, a `radius` with a
`sqrt` scale (`zero` and `rangeMin`), a `theta2` value, a `radius2` field, and a color; that a text mark draws `theta`
and `radius` silently but reports `vl-channel-theta2` and `vl-channel-radius2` (at `/encoding/theta2` and
`/encoding/radius2`); that each of the ten other marks reports all four channels at their pointers in document order,
`vl-channel-theta`, `vl-channel-radius`, `vl-channel-theta2`, and `vl-channel-radius2`; that an arc with a `size`, a
`strokeDash`, and a `longitude` reports `vl-channel-size`, `vl-channel-strokedash`, and `vl-channel-geo`; that the
`bandPosition` of a stacked `theta` of a text mark (0.25, with a color layer) is silent because it places the label in
its slice, and is `vl-encoding-bandposition` at `/encoding/theta/bandPosition` for a text mark whose `theta` has
`stack: false` and for the `theta` of an arc; and that an explicit `stack: true` on a `radius` with a `sqrt` scale is
`vl-encoding-stack-nonlinear` at `/encoding/radius/stack`, as a stack on a `sqrt` `y` is.

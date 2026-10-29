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
unit has no dependencies other than the parser's tree types, so no mocking or stubbing is required; the internal types
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
`fit`, a variable and a selection parameter, a projection, file data with a `csv` format, a `parse`, and a
`property`, three transforms (`calculate`, `filter`, `aggregate`), an `arc` mark, a temporal `x` with a time unit, a
`y` with aggregate, bin, stack, sort, and condition, a log scale with a scheme, a legend with `encode`, a `theta` and
a `latitude` channel, and a layer with a `geoshape` yields exactly thirty-one reports whose `(identifier, pointer)`
list is asserted in document order (for example `vl-encoding-stack` at `/encoding/y/stack` and `vl-mark-geoshape` at
`/layer/0/mark`), that only the schema version is a warning while every other report is an unsupported feature, that
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

**Tests**: `FeatureCatalogTests.FeatureCatalog_Visit_RemoteDataReportsVlDataRemoteUrl`,
`FeatureCatalogTests.FeatureCatalog_IsSchemeBearingUrl_ClassifiesUrls`,
`FeatureCatalogTests.FeatureCatalog_Visit_DataSourcesReportTheirOwnIdentifiers`

Asserts that remote data reports `vl-data-remote-url` at the data object and a relative local path reports
`vl-data-file`; that, as a theory, `http`, upper-case `HTTPS`, `file` (`file:///`, `file:/`, and `FILE://`), `data`,
`javascript`, and `ftp` URLs, a drive letter path (`C:\`, `C:name`, and `d:/`), a protocol-relative path, a Unix absolute
path (`/etc/hosts`), a backslash-rooted path, a UNC path, and a URL with leading blanks are scheme-bearing on every host
operating system, while `a.csv`, `data/a.csv`, `../a.csv`, `a b.csv`, the empty string, and `1:2.csv` are not; and that
empty or sourceless inline data
warns with `vl-data-empty` while each deferred source kind has its own identifier (`vl-data-named`,
`vl-data-sequence`, `vl-data-parse`, `vl-data-format-csv`, `vl-data-property`, `vl-data-values-text`, and the geographic
forms).

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
`null` aggregate, and an ascending sort shorthand are silent); that, as a theory over nine specifications, bars that
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

Asserts that every transform reports `vl-transform-<name>` at its index, that expression references anywhere in the
view's properties report `vl-expr-ref` (and `calculate` and `filter` strings report `vl-expr-calculate` and
`vl-expr-filter`), that variable and selection parameters report `vl-param-variable` and `vl-param-selection` while
`bind` is ignored; that a specification with `hconcat`, a nested `vconcat`, `concat`, `facet`, `spec`, `repeat`,
`resolve`, and `projection` reports the compositions and the projection and also examines the views nested at any
depth (`/hconcat/0/mark`, `/hconcat/1/vconcat/0/mark`, `/spec/mark`); and that, as a theory, autosize `pad` and `none`
(also in object form) are silent while `fit`, `fit-x`, and `fit-y` report `vl-layout-autosize-<type>` at the type.

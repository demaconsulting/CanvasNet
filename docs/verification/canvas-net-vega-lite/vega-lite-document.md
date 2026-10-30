## VegaLiteDocument Unit Verification Design

This document describes the unit-level verification strategy for the `VegaLiteDocument` unit: the public types in the
`DemaConsulting.CanvasNet.VegaLite` namespace — `VegaLiteDocument` (the `Parse` and `Open` entry points, the
`SourceName`, `SchemaUrl`, `SchemaMajorVersion`, `Title`, `MarkTypes`, `DataReferences`, and `ParameterNames`
properties, and the `Measure` and `Render` methods that run the chart pipeline), `VegaLiteOpenOptions`,
`VegaLiteRenderOptions`, `VegaLiteFontOptions`, `VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteDiagnostic`,
`VegaLiteUnsupportedFeature`, `VegaLiteSize`, `VegaLiteDataReference`, `VegaLiteDataKind`, the three exceptions
`VegaLiteSpecException`, `VegaLiteRenderException`, and `VegaLiteUnsupportedFeatureException`, and the internal
report sink that orders, de-duplicates, caps, and delivers the diagnostics and unsupported-feature reports.

### Verification Approach

The unit is verified by calling the public API directly and asserting on returned values, thrown exceptions, and the
state of the document. The unit's collaborators are the internal subsystems (`SpecModel`, `Data`, `Scales`, `Guides`,
`Layout`, `Marks`, and `Scenegraph`), which are used for real rather than mocked: the unit's job is to validate
arguments and options, bound and decode the input, call the specification model, expose the description of the
document, drive the pipeline with validated options, and deliver the reports in order, and the behavior of each
subsystem is verified under its own verification design. The internal members made visible to the test assembly
through `InternalsVisibleTo` (the document's effective `Options`, the effective copy of the open options, and the
report sink) are used where the public surface cannot show the behavior. The error contract is asserted precisely:
a specification error is a `VegaLiteSpecException`, which is a `FormatException` (not an `InvalidDataException`);
source longer than the source limit throws exactly `InvalidDataException`; a render failure is a
`VegaLiteRenderException`, which is an `InvalidOperationException` carrying a location; and an unsupported feature in
strict mode is a `VegaLiteUnsupportedFeatureException`, which is an `IOException` carrying the stable identifier and a
location.

Option types are verified for their defaults, the storing of valid values, the rejection of invalid values at
initialization with the offending parameter name, copying of collections so that a later change by the caller has no
effect, and the exposure of collections as read-only. Value types (`VegaLiteLocation`, `VegaLiteSize`,
`VegaLiteDataReference`) are verified for their display form and value equality. The report sink is verified through
its internal API with recording delegates.

The measure and render scenarios drive `Measure` and `Render` over small in-memory specifications with explicit
options: handlers that record the events they receive, tiny limits, and scales. Titled views have natural sizes
that are derived by hand from the Vega layout constants (a 13 pixel bold title, a 4 pixel offset, a 3 pixel subtitle
padding, and 5 pixels of padding), and the tests assert that `Measure` reports exactly the size of the surface that
`Render` produces. The system-level behavior of the entry points (string, stream, and path agreement, limits,
cancellation, determinism) is verified in the _CanvasNetVegaLite System Verification Design_, which also holds the
data scenarios that this unit's requirements cite.

Unit tests reside in `VegaLiteOpenOptionsTests.cs` (the open options and, in the same file, the font options),
`VegaLiteRenderOptionsTests.cs`, `VegaLiteLimitsTests.cs`, `VegaLiteLocationTests.cs`,
`VegaLiteRecordsAndExceptionsTests.cs`, `VegaLiteReportSinkTests.cs`, `VegaLiteDocumentLayoutTests.cs`, and
`SpecModel/VegaLiteDocumentSpecModelTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the internal subsystems are exercised for real
- **Files**: None are read or written by the option and record scenarios of this unit; the data scenarios (see _Data
  Options_) use real files in per-test temporary directories and the fixtures, and never the network
- **Threads**: The layout scenario that proves determinism renders from eight concurrent tasks and compares each
  result with a baseline; all other scenarios run on the test thread
- **Isolation**: Each test constructs its own sources, options, and handlers

### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any unexpected exception, wrong exception type, wrong parameter name, wrong location, wrong reported
identifier, or measured size that differs from the rendered size constitutes a failure.

### Unit-Level Test Scenarios

#### Open Options: Defaults, Validation, And Copying

**Tests**: `VegaLiteOpenOptionsTests.VegaLiteOpenOptions_Defaults_AreUnsetAndEmpty`,
`VegaLiteOpenOptionsTests.VegaLiteOpenOptions_ValidValues_AreStored`,
`VegaLiteOpenOptionsTests.VegaLiteOpenOptions_InvalidValues_Throw`,
`VegaLiteOpenOptionsTests.VegaLiteOpenOptions_NullDataPaths_Throw`,
`VegaLiteOpenOptionsTests.VegaLiteOpenOptions_DataPaths_AreCopiedAndReadOnly`,
`VegaLiteOpenOptionsTests.VegaLiteOpenOptions_WithEffective_CopiesAndFillsDefaults`

Asserts that a new options object has no base directory, source name, file resolver, or limits, an empty data path
list, and the shared default limits as its effective limits; that valid values are stored as given; that empty and
whitespace base directories, source names, and data paths throw `ArgumentException` naming the property; that a
null list and a null entry throw `ArgumentNullException`; that the data paths are copied from the caller's list
(a later add has no effect) and exposed read-only; and that the effective copy carries every setting and fills the
base directory and source name that were not set.

#### Font Options: Bundled Only By Default

**Tests**: `VegaLiteFontOptionsTests.VegaLiteFontOptions_Defaults_AreBundledOnly`,
`VegaLiteFontOptionsTests.VegaLiteFontOptions_AdditionalDirectories_AreCopiedAndValidated`

Asserts that by default system fonts are not used and no additional directory is listed, that directories are copied
from the caller's list and read-only, and that a null list, a null entry, and a whitespace entry are rejected.

#### Render Options: Defaults And Validation

**Tests**: `VegaLiteRenderOptionsTests.VegaLiteRenderOptions_Defaults_AreDocumented`,
`VegaLiteRenderOptionsTests.VegaLiteRenderOptions_ValidValues_AreStored`,
`VegaLiteRenderOptionsTests.VegaLiteRenderOptions_InvalidScale_Throws`,
`VegaLiteRenderOptionsTests.VegaLiteRenderOptions_InvalidContainerSize_Throws`,
`VegaLiteRenderOptionsTests.VegaLiteRenderOptions_NullLimits_Throws`,
`VegaLiteRenderOptionsTests.VegaLiteRenderOptions_InvalidConfig_Throws`

Asserts the documented defaults (scale 1, no background, configuration, parameters, datasets, or time zone — which
means UTC — a 400 by 300 container, the shared default limits, no handlers, strict mode off, and no system fonts);
that valid values, including a `TimeZoneInfo.Utc` and the handlers, are stored by reference or value as given; that a
scale of zero, a negative scale, NaN, infinity, and a scale above 8,192 throw `ArgumentOutOfRangeException` naming
`Scale`; that a container width of zero or a container height above the surface maximum throws naming the property;
that null limits throw `ArgumentNullException`; and that configuration text that is empty, not an object, or not
valid JSON throws `ArgumentException` naming `Config`.

#### Render Options: Parameters And Datasets Are Copied And Validated

**Tests**: `VegaLiteRenderOptionsTests.VegaLiteRenderOptions_Parameters_AreCopiedAndValidated`,
`VegaLiteRenderOptionsTests.VegaLiteRenderOptions_Datasets_AreDeepCopiedAndValidated`

Asserts that parameter overrides are copied from the caller's dictionary (later changes have no effect), exposed
read-only, and checked for blank names, unsupported value kinds (the message names the parameter), and non-finite
numbers, and that datasets are deep-copied (rows and lists), exposed read-only, and rejected for blank names, null
tables, and null rows.

#### Limits: Documented Defaults, Minimums, And Ceilings

**Tests**: `VegaLiteLimitsTests.VegaLiteLimits_Default_HasDocumentedValues`,
`VegaLiteLimitsTests.VegaLiteLimits_Default_IsSharedInstance`,
`VegaLiteLimitsTests.VegaLiteLimits_ValidValues_AreStored`,
`VegaLiteLimitsTests.VegaLiteLimits_ZeroOrNegative_Throws`,
`VegaLiteLimitsTests.VegaLiteLimits_AboveCeiling_Throws`

Asserts every documented default (4,194,304 source characters, JSON depth 64, 64 data files, 64 MiB per data file,
1,000,000 data rows, 1,024 fields, 1,000 views, composition depth 16, 2,000,000 scene items, 100,000 domain values,
1,000 ticks, expression length 4,096 and depth 64, 50,000,000 evaluation steps, a 1 MiB string, and 1,000
diagnostics); that the default is a shared instance; that valid values are stored; that every limit rejects zero and
negative values with `ArgumentOutOfRangeException` naming the property (a theory over all sixteen limits); and that
the three depth limits accept their ceiling (256, 64, and 256) and reject one more.

#### Location: Display Form And Equality

**Tests**: `VegaLiteLocationTests.VegaLiteLocation_ToString_FileCoordinatesAndPointer_FormatsAllParts`,
`VegaLiteLocationTests.VegaLiteLocation_ToString_NoFile_OmitsFile`,
`VegaLiteLocationTests.VegaLiteLocation_ToString_UnknownPosition_PrintsPlaceholderAndPointer`,
`VegaLiteLocationTests.VegaLiteLocation_Default_IsUnknown`,
`VegaLiteLocationTests.VegaLiteLocation_IsKnown_PointerOnly_IsTrue`,
`VegaLiteLocationTests.VegaLiteLocation_Equality_SameValues_AreEqual`

Asserts the exact text `spec.vl.json:3:7 /layer/1/encoding/x` for a full location, `12:4` without a file,
`a.json:? /mark` for an unknown position with a pointer, and `?` for the default value; that a pointer alone makes a
location known; and that two locations with the same values are equal.

#### Records And Exceptions: Display Forms, Base Types, And Constructors

**Tests**: `VegaLiteRecordsAndExceptionsTests.VegaLiteDiagnostic_ToString_KnownLocation_PrefixesLocation`,
`VegaLiteRecordsAndExceptionsTests.VegaLiteDiagnostic_ToString_UnknownLocation_IsMessageOnly`,
`VegaLiteRecordsAndExceptionsTests.VegaLiteUnsupportedFeature_ToString_IncludesFeatureAndLocation`,
`VegaLiteRecordsAndExceptionsTests.VegaLiteSize_And_DataReference_HaveValueEquality`,
`VegaLiteRecordsAndExceptionsTests.VegaLiteSpecException_WithLocation_CarriesLocationAndPrefixesMessage`,
`VegaLiteRecordsAndExceptionsTests.VegaLiteSpecException_OtherConstructors_LeaveLocationUnknownOrKeepInner`,
`VegaLiteRecordsAndExceptionsTests.VegaLiteRenderException_Constructors_CarryLocationAndInner`,
`VegaLiteRecordsAndExceptionsTests.VegaLiteUnsupportedFeatureException_Constructors_CarryFeatureAndLocation`

Asserts that a diagnostic and an unsupported-feature report display as the location, a colon and a space, and the
message (for example `s.json:2:5 /mark: vl-mark-arc: not yet`), and as the message alone without a location; that sizes
and data references have value equality; that `VegaLiteSpecException` is a `FormatException` whose message is prefixed
with its location, that `VegaLiteRenderException` is an `InvalidOperationException` and
`VegaLiteUnsupportedFeatureException` is an `IOException`, that each carries its location (and the feature
identifier); and that the parameterless, message, inner-exception, and located constructors behave as documented.

#### Report Sink: Order, Duplicates, Cap, Strict Mode, And Handler Failures

**Tests**: `VegaLiteReportSinkTests.VegaLiteReportSink_Deliver_PreservesOrderAcrossKinds`,
`VegaLiteReportSinkTests.VegaLiteReportSink_Duplicates_AreRecordedOnce`,
`VegaLiteReportSinkTests.VegaLiteReportSink_Cap_CollapsesOverflowIntoOneNotice`,
`VegaLiteReportSinkTests.VegaLiteReportSink_Strict_ThrowsAtFirstUnsupportedFeatureAfterRecordingIt`,
`VegaLiteReportSinkTests.VegaLiteReportSink_HandlerFailure_PropagatesOnlyFromNormalDelivery`

Asserts that warnings and unsupported-feature reports are delivered in the order they were recorded to the right
handlers; that identical reports are recorded once; that more reports than the diagnostics cap collapse the overflow
into a single notice; that strict mode records the first unsupported feature and then throws, even when the buffer is
full; and that a handler exception propagates from normal delivery but is swallowed while a failure is already being
reported, so that the original failure is not hidden.

#### Document: Metadata, Wrong Types, And Reports

**Tests**: `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Open_SupportedChartHasMetadataAndNoReports`,
`VegaLiteDocumentSpecModelTests.VegaLiteDocument_Parse_WrongTypeFailsWithPointerAndSourceName`,
`VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_DeliversParseWarningsAndCatalogReportsInDocumentOrder`,
`VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_StrictModeIgnoresWarningsAndThrowsAtTheFirstUnsupportedFeature`

Asserts that a chart that uses only supported constructs has its metadata (schema, title, marks, data references,
and parameter names) read from the tree and produces no reports; that a wrong value in a checked property fails when
the document is opened, with the pointer and the source name; that parse warnings and catalog reports reach the
handlers together, in document order, once per call, and that measuring delivers the same events as rendering; and
that strict mode throws at the first unsupported feature while warnings never stop rendering.

#### Document: Configuration And Large Inline Data

**Tests**: `VegaLiteDocumentSpecModelTests.VegaLiteDocument_Render_ResolvesConfigurationFromSpecificationAndOptions`,
`VegaLiteDocumentSpecModelTests.SpecParser_Parse_LargeInlineDataIsReadInOnePass`

Asserts that rendering resolves the caller's configuration over the specification's, with the resulting canvas of a
view of 123 by 77 pixels being 133 by 87 and the background `#123456` taken from the configuration, and that a large
inline data set is read in one pass without a per-row cost beyond its size.

#### Measure And Render: Natural Size, Titles, Fit, And Determinism

**Tests**: `VegaLiteDocumentLayoutTests.VegaLiteDocument_Measure_ReportsExactlyTheRenderedSize`,
`VegaLiteDocumentLayoutTests.VegaLiteDocument_Measure_NaturalSizeFollowsPaddingTitleAndAutosize`,
`VegaLiteDocumentLayoutTests.VegaLiteDocument_Render_TitleIsPaintedAboveThePlotAndCenteredOnIt`,
`VegaLiteDocumentLayoutTests.VegaLiteDocument_Render_ExplicitSizeFitsUniformlyAndCenters`,
`VegaLiteDocumentLayoutTests.VegaLiteDocument_Render_TitledViewIsDeterministicAcrossThreadsAndFontOptions`

Asserts that `Measure` reports exactly the size `Render` produces for sizes, titles, paddings, and scales; that the
natural size of titled views equals the hand-derived numbers (a top title makes a 310 by 327 canvas with the data
rectangle origin at y = 22, a title with a subtitle is 341 pixels high, left and right titles add 17 pixels of width,
and `autosize: none` gives 110 by 110 whatever sticks out); that the title ink lies inside the band above the plot,
centered on it; that an explicit size equals rendering at the matching scale and is centered; and that a titled view
renders byte-identical pictures from several threads and under different font options (the installed fonts have no
influence).

#### Data Options: Files, Datasets, Errors, And No Network

**Tests**: `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_LocalFileLoadsFromOpenedSpecFolder`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_StreamNeedsResolverForLocalFile`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_MissingOrBlockedFileThrowsRenderException`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_SequenceAndNamedDatasetsRender`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_ParseFailuresAreDeliveredAsWarnings`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_EachCallReadsTheFileAgain`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_LimitsFailBeforeSceneBudget`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Data_RemoteUrlIsReportedNotFetched`

The public option contracts of the data stage are verified through the public API, because the unit's job here is to
create one loader per call and to hand it the effective open options, the caller's datasets, and the limits; the units
behind it are verified under their own designs. Asserts that a document opened by path reads its relative file from the
file's folder and that the base directory, a data path, and a callback find the same file for a document without a folder
(all drawing the inline chart's pixels); that a document from a string or a stream with no file source fails every
`Measure` and `Render`, lenient or strict, with a located `VegaLiteRenderException`; that a missing file, a path
outside the
sandbox, and invalid UTF-8 are located failures in both modes; that the caller's datasets, the specification's datasets,
and sequences render without a report; that the data warnings follow the specification's reports; that each call reads the
file again; that a data limit fails before the scene limit; and that a remote URL is reported, never fetched, and never
given to the callback. The pure option contracts (the validation and copying of `BaseDirectory`, `DataPaths`,
`FileResolver`, and `Datasets`, and the default and ceilings of `MaxDataFiles` and `MaxDataBytes`) are the scenarios
_Open Options_, _Render Options: Parameters And Datasets Are Copied And Validated_, and _Limits_ above.

#### Parameters, Expressions, And Transforms: Open-Time Errors, Stages, And Warning Order

**Tests**: `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Parameters_DefaultsOverridesAndFilterParam`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Parameters_OverridesReachTheTransforms`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Parameters_InvalidDefinitionsFailWhenTheDocumentIsOpened`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Expressions_ErrorsAndLimitsFailWhenTheDocumentIsOpened`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Expressions_HostileEvaluationFailsClosed`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_LimitsSandboxAndCancellationFailClosed`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Transforms_MeasureMatchesRenderAndRendersAreByteIdentical`,
`VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Diagnostics_ExpressionReportsAndStrictMode`,
`VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_Aggregation_LimitsAndCancellationApply`,
`VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_Aggregation_MeasureMatchesRenderAndRendersAreByteIdentical`,
`VegaLiteAggregationIntegrationTests.CanvasNetVegaLite_SystemIntegration_Aggregation_OnlyTheUnsupportedPartIsReported`

The unit creates one data loader, one expression evaluator with one step budget, and one parameter environment per call,
and runs the stages in the order data, parameters, transforms, the implicit encoding stage (the bins, the aggregate, and
the stack that the encoding asks for, which yields the Raw and the Main table), scales, marks, so the scenarios are
asserted through the
public API (the units behind the stages are verified under their own designs). Asserts that the values of the static
parameters are the defaults of the specification overridden by `VegaLiteRenderOptions.Parameters` (a value replaces a
whole definition, so an overridden expression is not evaluated, and the name of a selection parameter is accepted and
ignored), that the transforms see them, that a name the document does not declare is an `ArgumentException` when it is
used, and that `ParameterNames` lists the declared names in declaration order; that the expression and transform errors
of the specification (a syntax error, a length or nesting over `MaxExpressionLength` or `MaxExpressionDepth`, an
unknown parameter, a parameter cycle, a repeated name, a wrong transform shape) are located `VegaLiteSpecException`
errors that fail when the document is opened, before any render or event, while the failures that depend on data
(`MaxEvaluationSteps`, `MaxStringLength`, `MaxDataRows` and `MaxFieldCount` of a derived table, a lookup outside the
sandbox, and a regular expression that exceeds its time limit) are `VegaLiteRenderException` errors located at the
transform or expression at render time; that the one budget of the call is shared by the parameters, the transforms,
the implicit stage, and the mark values (the groups of an aggregate count in the row budget: the five rows and three
groups of the aggregate bars need `MaxDataRows` 8, and 7 fails located at `/encoding/y`); that the reports of the
specification are delivered first and the warnings of the stages after
them, in the order loader warnings, lookup-source loader warnings, and transform warnings (`vl-expr-runtime` and
`vl-transform-runtime`), and also when a later stage fails, as the data warnings are; that strict mode throws at the
first report and delivers nothing after it; that a canceled token delivers nothing; and that `Measure` runs the same
stages as `Render` and equals the rendered size, with byte-identical renders from concurrent tasks.

#### Time Zone: Default And Stored Value

**Tests**: `VegaLiteRenderOptionsTests.VegaLiteRenderOptions_Defaults_AreDocumented`,
`VegaLiteRenderOptionsTests.VegaLiteRenderOptions_ValidValues_AreStored`

Asserts that the time zone is unset (UTC) by default and that a supplied `TimeZoneInfo` is stored as given. No
temporal data is drawn in this release, so there is no behavioral scenario beyond the option contract.

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
`SpecModel/VegaLiteDocumentSpecModelTests.cs`, and, for the time zone option, `VegaLiteTemporalScaleTests.cs` and
`Data/DataLoaderCalendarTests.cs`, and, for the layers and the composite marks of Phase 10, the system-level tests
`VegaLiteLayerIntegrationTests.cs` and `VegaLiteCompositeMarkIntegrationTests.cs`, and, for the label expressions and
the
offset channels, `VegaLiteLabelExprIntegrationTests.cs` and `VegaLiteOffsetIntegrationTests.cs` (the document's pipeline
is verified
through the public API: its stages are internal and are exercised for real) within the
`DemaConsulting.CanvasNet.VegaLite.Tests` project.

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

#### CanvasNetVegaLite-VegaLiteDocument-ImageSource: The Image Source For `.vl` And `.vl.json` Files

**Tests**: `VegaLiteImageSource_Render_NoRequestedSize_DrawsNaturalSizeOnChartBackground`,
`VegaLiteImageSource_Render_RequestedSize_DrawsAtThatSize`,
`VegaLiteImageSource_Render_SizeBeyondRemainingPixels_IsRefusedBeforeDrawing`,
`VegaLiteImageSource_Render_DataFile_ResolvesFromDirectoryOfRequest`,
`VegaLiteImageSource_Render_UnsupportedFeature_ReturnsDiagnosticOrThrowsWhenStrict`,
`VegaLiteImageSource_Render_InvalidInput_Throws`,
`VegaLiteImageSource_Properties_NameExtensionsAndNoSchemes`

`VegaLiteImageSourceTests.cs` calls `VegaLiteImageSource.Render` with requests built by hand. Asserts that a chart
without a
requested size has the size `Measure` reports and keeps its opaque white background, that a requested box is exactly
that size and
one dimension keeps the aspect ratio, that a 4000 by 4000 size against a budget of 1000 pixels is refused with
`ImageSourceBudgetException` (16,000,000 and 1000 on the exception) before the chart is drawn, with an allocation far
below the 64 MB of the picture, while a budget of exactly 120 by 80 pixels draws, that a data file is found in the
request's folder and a request without a folder
fails, that
an unsupported feature (a binned time unit) is returned as an `Unsupported` note of the source `vega-lite` and throws
`VegaLiteUnsupportedFeatureException` when strict, that an invalid specification is `VegaLiteSpecException`, a request
without a file
`InvalidOperationException`, a null request `ArgumentNullException`, a canceled request `OperationCanceledException`,
and that the
source claims `.vl` and `.vl.json` but not `.json` and no scheme.

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
message (for example `s.json:2:5 /mark: vl-mark-geoshape: not yet`), and as the message alone without a location; that
sizes
and data references have value equality; that `VegaLiteSpecException` is a `FormatException` whose message is prefixed
with its location, that `VegaLiteRenderException` is an `InvalidOperationException` and
`VegaLiteUnsupportedFeatureException` is an `IOException`, that each carries its location (and the feature
identifier); and that the parameterless, message, inner-exception, and located constructors behave as documented.

#### Report Sink: Order, Duplicates, Cap, Strict Mode, And Handler Failures

**Tests**: `VegaLiteReportSinkTests.VegaLiteReportSink_Deliver_PreservesOrderAcrossKinds`,
`VegaLiteReportSinkTests.VegaLiteReportSink_Duplicates_AreRecordedOnce`,
`VegaLiteReportSinkTests.VegaLiteReportSink_WarnWithKey_DistinctKeysAreRecordedSeparately`,
`VegaLiteReportSinkTests.VegaLiteReportSink_Cap_CollapsesOverflowIntoOneNotice`,
`VegaLiteReportSinkTests.VegaLiteReportSink_Strict_ThrowsAtFirstUnsupportedFeatureAfterRecordingIt`,
`VegaLiteReportSinkTests.VegaLiteReportSink_HandlerFailure_PropagatesOnlyFromNormalDelivery`

Asserts that warnings and unsupported-feature reports are delivered in the order they were recorded to the right
handlers; that identical reports are recorded once, while warnings with the same message and location but different
optional de-duplication keys are recorded separately (an equal or absent key still collapses them, and keyed warnings
count toward the cap); that more reports than the diagnostics cap collapse the overflow
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

#### Text: Missing Glyphs Are Warned About Once Per Mark Location And Set

**Tests**:
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_TextMissingGlyph_FallbackCoveredCharactersAreSilentAndDrawn`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_TextMissingGlyph_UncoveredCharactersWarnOnceAtTheMark`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_TextMissingGlyph_OneWarningPerLocationAndSet`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_TextMissingGlyph_InvisibleTextIsNotReported`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_TextMissingGlyph_UsesThePlacementScale`,
`VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_TextMissingGlyph_SetsSharingTheFirstEightCodePointsAreBothReported`

Asserts, for the chart of the report (`"a ∝ b ≫ c"` in a text mark), that no `vl-text-missing-glyph` warning is
delivered and that the pixels differ from the same chart with private-use characters (the missing-glyph boxes); that a
text mark with `"a \uE000 \u4E2D \uE000"` delivers exactly one warning that starts with the identifier, lists
`U+4E2D, U+E000`, and points at `/mark`, also with `ThrowOnUnsupportedFeature` set (a warning never throws); and that
two
layers with the same text and a nominal axis whose label has the character give one warning for each of the two
marks (`/layer/0/mark`, `/layer/1/mark`) and one at the unknown location for the axis label, the same for a `Measure`;
and that a text mark with a missing character gives one warning with the default fill but none with the fill `none`, an
invalid fill color, a fill opacity of 0, an opacity of 0, or a font size of 0, 1e-300, or 1e300 (text that is not drawn
is not reported: the report uses the painter's own rule, `ScenePainter.IsDrawable`); and that the rule is applied at
the real placement scale, not at the scale 1: a font size of 20000 is reported at the scale 1 but not at the scale 2
(render or measure) nor when fitted to 2100 by 700 pixels, and a font size of 40000 (which the mark builder never puts
in
the scene) is not reported at the scale 1, at the scale 0.5 (measure), or when fitted to 20 by 6 pixels; and that a text
mark driven by data
whose two rows lack nine code points that differ only in the ninth give two warnings at `/mark`, both listing the same
first eight code points, while two identical rows give one.
The `Help topics` trigger table of the tool tests also renders a chart with the character and expects the warning
with this identifier in the diagnostics.

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
file's folder and that the base directory, a data path, and a callback find the same file for a document without a
folder
(all drawing the inline chart's pixels); that a document from a string or a stream with no file source fails every
`Measure` and `Render`, lenient or strict, with a located `VegaLiteRenderException`; that a missing file, a path
outside the
sandbox, and invalid UTF-8 are located failures in both modes; that the caller's datasets, the specification's datasets,
and sequences render without a report; that the data warnings follow the specification's reports; that each call reads
the
file again; that a data limit fails before the scene limit; and that a remote URL is reported, never fetched, and never
given to the callback. The pure option contracts (the validation and copying of `BaseDirectory`, `DataPaths`,
`FileResolver`, and `Datasets`, and the default and ceilings of `MaxDataFiles` and `MaxDataBytes`) are the scenarios
_Open Options_, _Render Options: Parameters And Datasets Are Copied And Validated_, and _Limits_ above.

#### Parameters, Expressions, And Transforms: Open-Time Errors, Stages, And Warning Order

**Tests**:
`VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Parameters_DefaultsOverridesAndFilterParam`,
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

#### Time Zone: Default, Stored Value, And Effect

**Tests**: `VegaLiteRenderOptionsTests.VegaLiteRenderOptions_Defaults_AreDocumented`,
`VegaLiteRenderOptionsTests.VegaLiteRenderOptions_ValidValues_AreStored`,
`DataLoaderCalendarTests.DataLoader_Render_TimeZoneOptionReachesTheDataStage`,
`DataLoaderCalendarTests.DataLoader_Render_CustomDateFormatRendersInStrictMode`,
`VegaLiteTemporalScaleTests.VegaLiteTemporalScale_Points_SitAtTheirDates`,
`VegaLiteTemporalScaleTests.VegaLiteTemporalScale_RangeBars_SpanFromStartToEnd`,
`VegaLiteTemporalScaleTests.VegaLiteTemporalScale_ColorOverDates_FollowsTheDates`

Asserts that the time zone is unset (UTC) by default and that a supplied `TimeZoneInfo` is stored as given, and that the
option takes effect through the whole pipeline, with every zone built in the tests. A chart of two rows with the same
wall-clock time (10:00 on March 5, 2024), one written with an explicit `Z` offset and one without, parsed with
`date`, and plotted as the number of milliseconds of the instant, puts both rows at the same x in UTC (a time without an
offset is read as UTC), while with `TimeZone` set to a zone five hours ahead the row without an offset is the instant
five hours earlier (05:00Z) and its point is to the left of the other; a `parse` of `date:'%d/%m/%Y'` renders in strict
mode (a custom date format is not an unsupported feature) and the 5th of March, written `05/03/2024`, is to the left of
the 15th. The temporal scale scenarios derive the positions of dates on a 200 pixel data rectangle: January 1, 6, and
11,
2024 are five days apart, so the three points are 100 pixels apart (to six decimals); a range of two temporal fields
(January 1 to 11 and January 6 to 11, `x` and `x2`) is a bar from its start to its end on the time scale whose domain
holds both, so the bars are 200 and 100 pixels wide, end at the same right edge, and the second starts 100 pixels after
the first; and a color over a `utcyearmonthdate` time unit renders without a report with three different fills. The
behavior of the scenarios is carried further by the time scenarios of the _CanvasNetVegaLite System Verification
Design_ (the zone shifting a point by exactly one day, the day of the daylight saving change, and the host's zone not
mattering), and the calendar and data details by the _TimeCalendar_ and _DataLoader_ designs.

#### Layers And Composite Marks: Inherited Data, Transforms, Guides, And Limits

**Tests**:
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_TopLevelCalculateIsAppliedAndNotReported`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_TransformsApplyTopLevelFirstThenPerLayer`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_LineAndPointShareOneAxisPair`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_BarsWithMeanRuleShareTheYScale`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_TextLabelsSitOnTheBars`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_NestedLayersInheritTheEncodingOfEveryLevel`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_OwnDataReplacesTheInheritedDataAndSharesTheScale`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_NullDataInheritsTheParentsRowsAndOwnDataReplacesThem`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_OwnDataLayerTemporalFieldDoesNotAffectInheritedTable`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_StepSizeUsesTheMergedDiscreteScale`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_MeasureMatchesRenderAndRenderIsDeterministic`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_UnsupportedLayerMarkIsReportedAndOthersAreDrawn`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_TransformInAConcatenationAndInAFacetIsApplied`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_EmptyLayerListDrawsOnlyTheBackground`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_VariableParameterInALayerIsStillReported`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_IndependentYScalesDrawTwoAxes`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_IndependentAxesOfASharedScaleBothShowTheSameTicks`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_SharedColorFieldGivesOneLegend`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_ColorFieldsOfDifferentNamesJoinTheLegendTitle`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_IndependentLegendsGiveOneLegendPerLayer`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_LegendPropertiesOfEveryLayerCountAndOrientConflictSplitsTheLegend`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_ScaleConflictWarnsAndStillDraws`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_WrittenAxisTitleWinsOverDerivedAndOthersJoin`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_BarsWithWrittenTitlesAndMeanRuleKeepTheWrittenTitles`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_ErrorBandWithWrittenTitlesAndMeanLineKeepTheWrittenTitles`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_ErrorBandWithoutTitlesWritesItsFieldAsTheTitle`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_PropertiesOfTheTopLevelViewAreIgnoredInALayer`,
`VegaLiteLayerIntegrationTests.CanvasNetVegaLite_SystemIntegration_Layer_ViewAndDepthLimitsAreEnforced`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_BoxPlot_VerticalTukeyIsDrawnFromTheQuartiles`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_BoxPlot_TopLevelTransformAppliesFirst`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_BoxPlot_LayersShareTheMeasureScale`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_BoxPlot_ExpandedViewsAreLimitedByMaxViews`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_BoxPlot_MeasureMatchesRenderAndIsDeterministic`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_ErrorBar_DefaultIsARuleOfTheMeanAndStandardError`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_ErrorBar_LayersOverABar`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_ErrorBar_ExpandedViewsAreLimitedByMaxViews`,
`VegaLiteCompositeMarkIntegrationTests.CanvasNetVegaLite_CompositeMark_ErrorBand_TwoDimensionalBandIsAnAreaWithLineBorders`

The document's pipeline for layered views is verified through the public API, because its stages (the layer tree, the
data
walk, the scales of the layers, the axis planning, and the legend scopes) are internal and meaningful only together. The
scenarios assert, with positions derived by hand, that the data is loaded where the nearest `data` is and that a layer's
own
`data` replaces the inherited table; that the transform of a layered view runs before the layers read the table (so a
top-level `calculate` is applied once, is seen by every layer, and is not reported) and the transform of each layer
after it,
the layers not seeing one another's results; that the layers draw in order, each in its own group of marks, over one
pair of
axes whose titles merge (a written title wins over a derived one, as in Vega-Lite; the others join), or over two axes
(the second on the opposite side and with no grid) and one legend for each
layer when
`resolve` says independent, and a shared legend that takes the properties of every layer (and splits when the explicit
orients differ); that a step sizes the union of the categories; that a layer with an unsupported mark is reported
and the others are drawn, that a variable parameter declared in a layer is reported and a transform inside a
concatenation is applied and not reported,
and that a layered view with no layer draws only the background; that the limits of the views and of the nesting fail
with a
located error at the exact boundary; that a composite mark is computed from the rows that the transforms of the views
above it
left, draws its parts as layers that share the scale of the measure, and counts its views against `MaxViews`; and that
`Measure` equals `Render` and the render is deterministic. The numbers of every scenario are listed in the
_CanvasNetVegaLite
System Verification Design_ (the layer and composite mark scenarios); the structure that they rely on is verified in the
_SpecNormalizer_, _ScaleResolver_, _AxisBuilder_, and _LegendBuilder_ unit designs.

#### Fit: A Fitted View Is The Size It Is Given

**Tests**: `VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_FitSurfaceHasTheGivenSize`,
`VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_FitXLayerView`,
`VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_FitCountsLegendsAcrossTheirSideOnly`,
`VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_ContainerUsesOptionsContainerSize`,
`VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_FitWithStepWarns`,
`VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_FitIsNotReportedAnymore`,
`VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_MeasureFitEqualsRender`,
`VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_FitThatDoesNotSettleDrawsAxesForTheFinalRectangle`,
`VegaLiteAutosizeIntegrationTests.CanvasNetVegaLite_SystemIntegration_Autosize_CompositionIgnoresFitAndContainerWithWarning`

A single or layered view with `autosize` `fit`, `fit-x`, or `fit-y`, or a `"container"` width or height (Vega-Lite's
fit to the container with `contains: padding`), goes through the iteration of the guides with the layout that a
`"container"` size already used (up to three passes until the data rectangle is stable; the extents of the legends are
combined with those of the axes counting only the legends' extent across their side). The layout takes the title into
the
fit and rounds the reach of each side up to whole pixels, so the canvas is the size, whatever the guides need. The
numbers of every scenario are listed in the _CanvasNetVegaLite System Verification Design_ (the autosize scenario) and
in
the _ViewLayout_ and _LegendLayout_ unit designs; the scenarios also verify that a step size drops the fit with the
warning `vl-layout-fit-step` (delivered after the data stage's own reports), that `Measure` equals `Render`, and that a
composition ignores a fit and a container size with `vl-composition-property-ignored`. A fit that does not settle in the
three passes (the width of the y axis labels
of a chart with data up to 12345 changes with the height that it leaves, for container heights of 403 and 60) has
its guides built once more for the final rectangle, so the axes span exactly the data rectangle.

#### Cells: The Scene Of A Single Or Layered View Is Unchanged By The Extraction

**Tests**: `VegaLiteRefactorSnapshotTests.VegaLiteDocument_Refactor_SceneIsUnchangedForSingleAndLayeredViews`,
`VegaLiteRefactorSnapshotTests.VegaLiteDocument_Refactor_SnapshotIsDeterministic`

The pipeline of a single or layered view was refactored for compositions (Phase 11): the data walk loads a table once
for each `data` object, and the guides and the marks of the view are built as a cell (`CreateCell`, `BuildCellGuides`,
`BuildCell`), which a composition builds on its own. The stages are internal, so the refactoring is verified by the
whole scene, with no tolerance. The test lays out 12 specifications (original, with made-up data) without painting and
writes the scene as text: the canvas, then every item in drawing order with its depth, its offset, and every property,
numbers in the round-trip format of the invariant culture. The text must equal the snapshot in
`Fixtures/SceneSnapshots/<case>.scene.txt`. The snapshots were captured from the Phase 10 pipeline before the
extraction and again, to be sure that the earlier Phase 11 work packages had not changed them, from a clean checkout
of the Phase 10 commit; the two captures were byte-identical, and every case passed after the refactoring on every
target framework. The one snapshot that Phase 11 changed deliberately is `single-container-fit`: when `autosize: fit`
was implemented (work package 8) the container chart came to follow Vega's `viewSizeLayout` (the reach of the guides
rounded up to whole pixels, the legend counted across its side only), so its canvas is exactly the container, 420 by
252, where it had been 421.3 by 252.8 (the old path took the raw guide extents from the container and padded the
rest); the other eleven snapshots are unchanged. The cases are single views (a bar chart with a title and a written
axis title, filled points with a
color and a size legend and a null value, a line with points over a temporal axis, stacked bars of a sum, a
histogram), a view that fits a container (which exercises the three-pass iteration of the guides with the layout), and
layered views (a bar with a rule of a mean over a top-level `calculate`, a line and points that share a color legend,
two independent y scales with two axes, a layer with its own data and independent legends, a boxplot that expands to
layers, and nested layers with text labels and a dashed rule). The second test renders one specification twice and
checks that the texts are equal, so that a difference to a snapshot is a change of the pipeline and not of the run. A
deliberate change of the output is captured again by running the test with the environment variable
`CANVASNET_SCENE_SNAPSHOT_DIR` set to the snapshot folder (see the fixtures README).

#### Compositions: Concatenation And Repeat Are Drawn Cell By Cell

**Tests**:
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_HConcat_TwoChartsAreLaidOutWithTheirOwnAxes`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_HConcat_SecondChartStartsAfterFirstBoundsPlusSpacing`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_VConcat_ChartsStackWithTwentyPixelsBetween`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_ColumnsWrapsAndAlignsEach`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_SpacingColumnAndRowObject`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_BoundsFlushIgnoresAxes`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_CenterCentersChildren`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_SharedColorLegendIsMergedAtTheRoot`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_ResolveColorIndependentKeepsLegendsInTheCharts`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_ResolveScaleXSharedUnionsDomainsButKeepsAxes`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_ChildrenWithOwnDataAndTransforms`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_TopLevelDataAndTransformAreInherited`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_ChildTitleStaysInsideItsCellAndCompositionTitleIsAnchoredAtTheStart`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_NestedVConcatOfHConcat`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_LayerChildRenders`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_CompositeMarkChildRenders`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_LegendOwnedByNestedConcatIsPlacedBesideIt`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_SecondAxisIsSeparatedPerChart`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_MeasureEqualsRenderSize`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_DeterministicAndConcurrent`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_ViewCountLimitIsEnforced`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_DepthLimitIsEnforced`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_SceneItemLimitIsEnforced`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_StrictModeHasNothingToReport`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_TransformsAreNotReported`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_ContainerWidthIsIgnoredWithWarning`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_AutosizeFitIsPadWithWarning`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_AxisSharedResolveWarns`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_UnsupportedMarkChildIsReportedAndTheRestDrawn`,
`VegaLiteConcatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Concat_FacetChildIsDrawnWithTheOtherCharts`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_ArrayRepeatsFieldAsConcatWithColumns`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_RowColumnMatrixIsRowMajor`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_RowOnlyIsOneColumnAndColumnOnlyIsOneRow`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_LayerRepeatDrawsLayers`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_LayerRepeatTakesTheSizeOfTheSpec`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_RefDrivesAxisTitles`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_DataIsLoadedOnceForAllCopies`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_ProductLimit`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_UnknownRefWarnsAndDropsChannel`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_FacetWithUnknownRefIsDropped`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_ColumnsWithRowColumnWarns`,
`VegaLiteRepeatIntegrationTests.CanvasNetVegaLite_SystemIntegration_Repeat_ColorLegendIsDrawnOnce`

The drawing of a concatenation and a repeat (Phase 11) is verified through the public API with the positions derived by
hand from the layout rules, because its stages (the view tree, the scales across the charts, the guides, the grid) are
internal and meaningful only together; the grid arithmetic itself is verified on fixed boxes in the _CompositionLayout_
design. The charts without axes have as bounds their data rectangle and the half pixel of the one pixel `#ddd` border
(Vega unions the bounds of a stroked group), so the second chart of a row of two 100 by 80 charts is at
`ceil(100.5) + 20 + ceil(0.5)` = 122 pixels from the first and the canvas is `1 + ceil(222.5) + 10` = 234 by
`1 + ceil(80.5) + 10` = 92 pixels; a `vconcat` stacks the charts 102 pixels apart; `columns` wraps (a third chart is
in the second row, 102 pixels down and back at the first column), `spacing` is a number or a `column` and `row` object
(`101 + 40 + 1` and `81 + 10 + 1`), `bounds: "flush"` places the data rectangles exactly 100 + 20 apart whatever the
axes, and `center` moves a 60 pixel wide chart 20 pixels right of a 100 pixel wide one in a column. The charts of a
concatenation keep their own axes (an `hconcat` of two charts with axes draws four axes and the titles `a`, `v`, `w`,
`v`; `resolve.axis` set to `shared` is the warning `vl-concat-axis-shared`, delivered once, and the axes stay), the
second
axis of a layered chart goes to the opposite side only within that chart (the chart next to it keeps its axis at the
left), `resolve.scale.x` set to `shared` gives both charts the domain of the union (the label `100` on both axes) while
`resolve.scale.color` set to `independent` keeps a legend in each chart (between the charts) and the default shares the
color scale, so a color that the charts share is one legend with the entries of the union of their domains at
`ceil(222.5) + 18` = 241 pixels from the origin of the concatenation and at its top; when a nested concatenation owns
the
legend (an outer `resolve.legend.color` of `independent`) it is beside that concatenation and the next chart starts
below
it. A chart's title stays in its cell and is centered over its data rectangle, and the title of the concatenation is
anchored at the start and drawn after the cells. A chart or a concatenation with data and transforms of its own, and the
data and transform of the concatenation that its charts inherit (`w = 10 v` makes bars 40, 80 and 60 pixels high), a
layered chart (one pair of axes, three marks groups) and a composite mark as a chart, a chart with an unsupported mark
(reported, and an empty cell of the default 300 by 300 size) and a facet as a child (its two cells are 122 pixels apart
and the next chart is `ceil(222.5) + 20 + ceil(0.5)` = 244 pixels from the first cell) are drawn with the other charts.
A repeat is a concatenation of copies of its `spec`: the values `[a, b, c]` with `columns` of 2 wrap like a `concat`,
rows and columns are the row-major cross product (the axis titles of the four cells are `c a`, `d a`, `c b`, `d b`), a
repeat of rows only is a column and of columns only a row, a repeat of layers is one layered chart (two marks groups,
one y axis titled `a, b`) that takes the size of its `spec` (cells of 160 by 70 pixels for a `width` of 160 and a
`height` of 70, whether with rows or alone, since Vega-Lite sizes a layered view from its layers), the `{"repeat": ...}`
references give the field and the axis title, the data of the repeated `spec` is read once (a limit of five rows is
enough for four copies), the product of the rows and the columns is checked against `MaxViews` before any copy is made
(40 by 40 is refused), a reference that the repeat does not define is the warning `vl-repeat-ref-unresolved` (delivered
once, the channel is dropped; a facet whose field is such a reference is dropped, and its `spec` is drawn without a
facet, at the size of the `spec`), `columns` next to rows and columns is the warning `vl-composition-columns-ignored`
(once,
and the grid stays two columns wide), and the color scale of the copies is shared, with one legend beside the grid. A
`"container"` width and a `fit` autosize of a concatenation are the warning `vl-composition-property-ignored` and the
canvas is the content plus the padding. The limits of the views, the nesting, and the scene items are enforced with a
located error, strict mode has nothing to throw for, `Measure` equals `Render`, and the render is deterministic also
when several threads render one document.

#### Compositions: Facets Are Drawn Cell By Cell With Headers, Footers, And Shared Axes

**Tests**:
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_ColumnChannelDrawsOneCellPerValueWithHeaderLabels`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_RowAndColumnCrossProductDrawsEmptyCells`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_WrapFacetUsesColumnsAndCellLabelTitles`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_WrapWithoutColumnsIsOneRowAndRowAloneIsOneColumn`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_SharedXAxisIsOnceUnderEachColumnAndSharedYAxisOncePerRow`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_IndependentAxisStaysInTheCells`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_IndependentYScaleGivesEachRowItsDomain`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_SharedScaleUsesTheUnionOfAllCellsAndStepWidthTheirCategories`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_IndependentStepSizesCellsByOwnCategoriesAndAlignFollowsTheScale`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_AggregateRunsPerCell`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_StackRunsPerCell`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_SortDescendingArrayAndNull`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_NullValueIsItsOwnCell`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_HeaderPropertiesChangeLabelAndTitleStyle`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_HeaderDefaultsAreTenAndElevenBoldWithTheFieldNameAsTitle`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_HeaderTitleNullAndLabelsFalseHideTheirText`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_HeaderNullHidesLabelsAndTitle`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_HeaderLabelsUseTheFormat`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_SharedLegendIsOutsideTheGrid`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_LayerSpecBody`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_UnitSpecWithFacetEncodingEqualsFacetOperator`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_OwnDataInBodyIsNotPartitioned`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_ColumnsIsIgnoredForAColumnFieldWithWarning`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_SpacingAndFlushBoundsApply`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_ColumnLabelsAndTitleAtTheBottomAreFootersAndTitleBelow`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_RowLabelsAndTitleAtTheRightAreFootersAndTitleBeside`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_CellLimitIsEnforcedBeforeAllocation`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_NotDrawnPartsAreReported`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_StrictModeHasNothingToReport`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_SceneItemLimitIsEnforced`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_SharedAxesAreChargedOncePerHeader`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_Cancellation`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_DeterministicConcurrentAndMeasureEqualsRenderSize`,
`VegaLiteFacetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Facet_EmptyDataDrawsNoCellsAndUnsupportedMarkDrawsEmptyCells`

The drawing of a facet (Phase 11) is verified through the public API with the positions derived by hand from the layout
rules and checked, for their structure, against the Vega-Lite 6 compiler and Vega 6 run outside the repository (the text
widths of that run are estimates, so no number that depends on text is taken from it): the cells of a bare 100 by 80
chart are 122 pixels apart in a row and 102 pixels in a column (`101 + 20 + 1` and `81 + 20 + 1`), a header lies at
`floor(-0.5)` = -1 from the first cell with its label 20 pixels further out (the label's offset of 10 and its height of
10), the title of the field is 10 pixels outside the headers (so 31 from the cell; the title of a `row` field is rotated
by -90 degrees and centered at `round(-0.5 + 183 / 2)` = 91 from the top, the title of a `column` field at
`round(-0.5 + 223 / 2)` = 111), the shared x axis is in a footer at `ceil(182.5)` = 183 below the first of two rows, and
a legend that the cells share is at `ceil(222.5) + 18` = 241 from the origin. A `row` and `column` mapping has the cross
product of the values, row-major, with empty cells for the combinations without data (the three rows give three bars in
four cells); a wrapped facet with `columns: 2` over three values puts the third cell in the second row at
`ceil(80.5) + 20 + ceil(20.5)` = 122 pixels (the labels of the cells are their titles, 20 above them, so the title of
the field has no extra offset: it is at `floor(-20.5)` = -21), and without `columns` is one row. The axes that the cells
share are once under each column (the footer) and once left of each row (the header), each cell keeping the grid of the
y axis only and no axis; an independent y scale keeps the axes in the cells (one in each) and a row facet of an
independent scale gives each row its own domain (the bars are 80, 80 and 60 pixels high instead of 40, 80 and 60 on the
shared 0 to 8), while a shared band scale is the union of the categories of all cells (every cell with a step of 20 is
60 pixels wide and the bar of the cell that has only B is in the middle band at 21) and an independent step scale sizes
each cell by its own categories (40, 20 and 60 pixels; the cells are not aligned, `ceil(40.5) + 20 + 1` = 62 and 42
pixels apart, and an explicit `align: all` makes the columns 82 pixels apart). An aggregate and a stack run on the rows
of each cell (a mean of 3 and of 10 gives bars of 24 and 80 pixels on the shared 0 to 10; the segments of 1, 2 and 4 are
20, 40 and 80 pixels and the last starts at the bottom). The order of the cells is ascending by default and for
`sort: null` (as Vega-Lite orders them), descending, or an array first and then the values it does not list in the order
they first appear; a null value is its own cell, labeled `null`. The header properties change the text (`labelFontSize`,
`labelColor`, `titleFontSize`, a `title` of the header that wins over the title of the field definition), the defaults
are a 10 pixel label and an 11 pixel bold title with the field name, `title: null`, `labels: false` and `header: null`
switch the text off (without labels and axes there is no header group), and the labels are formatted by the `format` of
the header (a number as `1.0` and `2.5`, a date as `2020` and `2021`). A `labelOrient` of `bottom` puts the labels of a
column into footers at `ceil(80.5)` = 81 and a `titleOrient` of `bottom` the title below at 81 + 20 + 10 = 111; on a
row, `right` puts the labels into footers at `ceil(100.5)` = 101 and the title at 101 + 20 + 10 = 131 turned by 90
degrees. The `spec` of a facet can be layered, a unit view with a `facet` channel is the same drawing as the operator, a
view with data of its own is not partitioned (each of two cells draws all three rows), `columns` next to a `column`
field is ignored with `vl-composition-columns-ignored`, `spacing` of 40 puts the columns `101 + 40 + 1` = 142 apart, and
`bounds: "flush"` lays out the data rectangles (120 apart whatever the axes). The limit on the number of views is
checked from the number of cells before any is made (six cells and the facet are seven views; refused at six, located at
the `row` field), a facet with unsupported parts reports `vl-facet-field-transform`, `vl-facet-sort`,
`vl-header-orient`, the unsupported function of a header `labelExpr` (`vl-expr-function-luminance`; `vl-expr-label` is
retired)
and `vl-format-type-custom` and draws the rest, strict mode has nothing to throw for
a facet that uses only what is drawn, the scene item limit (charged once for each header, so a 5 by 5 grid renders under
exactly the limit of its own scene's item count, and one less fails) and a canceled token stop the work, `Measure`
equals
`Render`, the render is deterministic also from several threads, a facet of no rows draws no cell and a facet of an
unsupported mark draws its empty cells and reports the mark once.

#### Label Expressions: Axes, Legends, And Headers Are Labeled By Their Expressions

**Tests**:
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_AxisLabelsAreReplacedAndStrictModeHasNothingToReport`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_TemporalAxisUsesTheDateValue`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_LegendLabelsAreReplaced`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_HeaderLabelsAreReplacedWithPrecedence`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_WrappedFacetTitlesAreReplaced`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_HeaderArrayResultIsSeveralLines`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_WrappedFacetTitleArrayResultIsSeveralLines`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_HeaderMultiLineLabelIsTallerThanOneLine`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_ParameterCanBeUsed`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_ResultDoesNotDependOnTheCulture`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_BudgetExceededIsARenderException`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_UnsupportedFunctionIsReportedAndEvaluatesToEmptyLabels`,
`VegaLiteLabelExprIntegrationTests.CanvasNetVegaLite_SystemIntegration_LabelExpr_FormatAndTimeFormatDrawWithoutDiagnostics`

The `labelExpr` of axes, legends, and facet headers runs on the evaluator of the call when the chart is laid out (the
document gives the `RowExpressions` to the axis and legend builders and to the facet headers). A line chart whose y
labels are `datum.label + '%'` and whose x labels are blanked by a conditional has the labels `FY2020` and `FY2022` and
not `2021`, and `20%` and `60%`, with strict mode reporting nothing; a temporal axis reads `datum.value` as a date
(`Q1 2021`); a legend is labeled with its index and value (`A (0)` and `B (1)`); the column and row headers of a facet
read `C:a`, `C:b` and `R:2020`, `R:2021`, `R:2022`, with the header taking precedence over the header configuration of
the channel and over `config.header`, and the cell titles of a wrapped facet take `config.header` as the fallback
(`a!a` and `b!b`); an expression reads a variable parameter (`20 kg`) and the labels under `tr-TR` are those of the
invariant culture. The array result of a header expression is several lines of the header: `[datum.label, 'n=' +
datum.value]` on the column header of a facet gives the lines `a`, `n=a` and `b`, `n=b`, `[datum.label, 'r']` on the
row header gives `2020`, `r` and so on, while a string result with a line feed stays one line (`a\na`); the cell title
of
a wrapped facet with `headerFacet.labelExpr` `[datum.label, '#' + datum.value]` has the lines `a`, `#a` and `b`, `#b`;
and the chart with a two-line column header is more than 8 pixels taller than the one with a one-line header (the
layout measures the lines). An expression that needs more than the step budget is a `VegaLiteRenderException` located at
`/encoding/x/axis/labelExpr` and naming `MaxEvaluationSteps`, and a call of an unsupported function
(`luminance('#fff')`) is reported as `vl-expr-function-luminance` at the text, gives empty labels, and fails strict mode
with a `VegaLiteUnsupportedFeatureException`. The functions `format` and `timeFormat` are evaluated and not reported:
the y labels `format(datum.value * 1000, ',.0f')` include `20,000`, the x label `timeFormat(datum.value, '%b %Y')` is
`Jan 2021`, the legend `format(datum.value, '+d')` gives `+2020`, `+2021`, and `+2022`, the column headers
`format(..., '03d')` give `001` and `002`, strict mode has nothing to throw, and the specifier `.2.3f` is a located
`VegaLiteRenderException` ("invalid format: .2.3f"). The command line renders the same kinds of chart in strict mode
with
no diagnostics
(`RenderCommandVegaLiteTests.RenderCommand_Run_VegaLiteOffsetAndLabelExpr_AreDrawnWithStrictAndNoDiagnostics`,
which also draws a chart whose labels use `format` and `timeFormat` and a `yearmonth` bar chart with an `xOffset`).

#### Offset Channels: Grouped Bars, Strips, And Shared Offset Scales

**Tests**:
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_GroupedBarsHaveTwentyPixelBarsAndReportNothing`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_AxisIsCenteredOnTheBandAndLegendListsTheGroups`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_YOffsetDrawsHorizontalGroupedBars`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_ScalePaddingOfTheOffsetLeavesAGapInsideTheGroup`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_FixedWidthSharesTheBandwidth`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_ConcatChartsHaveIndependentOffsetScalesUnlessShared`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_FacetCellsShareTheOffsetDomain`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_LayersShareTheOffsetScale`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_NotDrawnOffsetsAreReportedAndIgnored`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_MonthlyGroupedBars`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_NominalTimeUnitGroupedBars`,
`VegaLiteOffsetIntegrationTests.CanvasNetVegaLite_SystemIntegration_Offset_BoxplotBoxesSitSideBySide`

A grouped bar chart of three categories and two groups through the whole pipeline is 160 pixels wide, has bars 20 pixels
wide at 10, 30, 60, 80, and 110 from the left of the view (the position step 20 x 2 / 0.8 = 50, the nested paddings
0.2), and reports nothing, so strict mode has nothing to throw. The x axis has one tick for each category centered in
its
band (the offset does not move the axis) and the legend lists the groups; `yOffset` draws the horizontal variant (160
pixels high, bars 20 high at y = 10, 30, 60, 80, and 110); an offset padding makes the view 140 wide with bars of 15 and
a gap of 5 inside the group; a width of 200 gives bars of 25 from 12.5. The offset scales of the children of a
concatenation are independent (the second chart is 80 pixels wider, with bars of 20) unless `resolve.scale.xOffset` says
`shared`, the cells of a facet share the offset domain (every cell has the same grouped bars), and a bar layer and a
text layer over the same offsets share the position and the offset scales (each label is at the center of its bar). An
offset that is not drawn is reported and the marks are drawn at their position (`vl-offset-position` for a quantitative
position or a `day` time unit, and `vl-offset-ranged` for a continuous offset of a bar; a `month` unit reports nothing),
a grouped bar chart over months (`month` unit, width 300, three months of two groups: the domain is January 1 to April 1
of the leap year 2012, 91 days, so a day is 300 / 91 pixels and the reference month of 31 days is 31 x 300 / 91 =
102.2 pixels) has six bars 0.4 of a month (40.9 pixels) wide, the first two touching at 0.1 and 0.5 of a month from the
left of the view and the next two at one month plus 0.1 and 0.5 of a month, with a legend `a`, `b` and nothing
reported in strict mode, a grouped bar chart over a nominal `x` with the `month` unit (an ordinary band parent: width
300,
three bands with the nested paddings 0.2, the step 300 / 3.2 = 93.75) has six bars 37.5 wide at 18.75, 56.25, 112.5,
150,
206.25, and 243.75 and nothing reported, and the three boxes of a boxplot with an offset
channel sit side by side with equal widths.

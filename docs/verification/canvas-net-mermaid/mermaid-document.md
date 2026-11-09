## MermaidDocument Unit Verification Design

This document describes the unit-level verification strategy for the `MermaidDocument` unit in the root namespace
`DemaConsulting.CanvasNet.Mermaid`: the public `MermaidDocument`, its options, limits, records, codes, and
exceptions, the internal `MermaidReportSink`, and the `MermaidImageSource`.

### Verification Approach

The unit is verified through its public surface and, for the report sink, through `InternalsVisibleTo`. The tests
use small inline diagrams (a two-node flowchart and a one-message sequence diagram from `MermaidTestHelpers`), byte
arrays and memory streams for the decoding cases, and temporary files for `Open(path)`. The lower subsystems are real,
not mocked: the unit's responsibility is the contract around them (arguments, decoding, metadata, option validation,
error shapes, report delivery, and the image-source adapter), and the subsystems are verified by their own tests.
The image source is exercised with `ImageSourceRequest` values built in the test, including fragments parsed by the
core `ImageFragment`.

Unit tests reside in the root of the `DemaConsulting.CanvasNet.Mermaid.Tests` project: `MermaidDocumentTests.cs`,
`MermaidRenderOptionsTests.cs`, `MermaidLimitsTests.cs`, `MermaidRecordsAndExceptionsTests.cs`,
`MermaidReportSinkTests.cs`, and `MermaidImageSourceTests.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; report handlers are lists that collect the reports
- **Isolation**: temporary files are created in the system temporary folder and deleted; no network

### Acceptance Criteria

A unit-level test run passes when every scenario below passes without an exception beyond those explicitly
asserted. An entry point that accepts what it must refuse, an option that accepts an invalid value, a report that is
delivered twice, out of order, or beyond the cap, or an exception without its documented code and location
constitutes a failure.

### Unit-Level Test Scenarios

#### CanvasNetMermaid-MermaidDocument-ParseOpen: Entry Points And Decoding

**Tests**: `MermaidDocumentTests.MermaidDocument_Open_StreamWithBom_ParsesAndLeavesStreamOpen`,
`MermaidDocumentTests.MermaidDocument_Open_InvalidUtf8_ThrowsSyntaxException`,
`MermaidDocumentTests.MermaidDocument_Open_File_UsesPathAsSourceName`,
`MermaidDocumentTests.MermaidDocument_EntryPoints_InvalidArguments_Throw`,
`MermaidDocumentTests.MermaidDocument_Parse_OverMaxTextSize_ThrowsInvalidData`,
`MermaidDocumentTests.MermaidDocument_Open_StreamOverBound_ThrowsInvalidData`

Asserts that a byte-order mark is dropped and the stream stays open, that invalid UTF-8 is a syntax exception, that a
file is named by its path, that null, empty, and unreadable arguments throw the documented argument exceptions, and
that text and streams over the size limit throw `InvalidDataException`.

#### CanvasNetMermaid-MermaidDocument-Metadata: What A Document Exposes

**Tests**: `MermaidDocumentTests.MermaidDocument_Parse_Flowchart_IsSupportedFlowchart`,
`MermaidDocumentTests.MermaidDocument_Parse_SequenceDiagram_IsKnownButNotSupported`,
`MermaidDocumentTests.MermaidDocument_Parse_FrontmatterTitle_IsTitle`

Asserts the type, support, direction, and counts of a flowchart, that a sequence diagram opens as a known but
unsupported type, and that the frontmatter title becomes the title.

#### CanvasNetMermaid-MermaidDocument-ErrorContract: Exceptions And Locations

**Tests**: `MermaidDocumentTests.MermaidDocument_Parse_UnknownKeyword_ThrowsLocatedSyntaxException`,
`MermaidRecordsAndExceptionsTests.MermaidSyntaxException_Located_CarriesCodeLocationHintAndExpected`,
`MermaidRecordsAndExceptionsTests.MermaidSyntaxException_StandardConstructors_UseGeneralCode`,
`MermaidRecordsAndExceptionsTests.MermaidRenderException_Constructors_CarryCodeAndLocation`,
`MermaidRecordsAndExceptionsTests.MermaidUnsupportedFeatureException_Constructors_CarryFeatureAndLocation`,
`MermaidRecordsAndExceptionsTests.MermaidLocation_ToString_Forms_AreFileLineColumn`,
`MermaidRecordsAndExceptionsTests.MermaidLocation_IsKnown_DependsOnLine`

Asserts the code, location, hint, expected tokens, and message format of each exception type, the general code of
the standard constructors, the three text forms of a location, and when a location is known.

#### CanvasNetMermaid-MermaidDocument-DefectNet: Layout Defects Fail Closed

**Tests**: `MermaidDocumentTests.MermaidDocument_IsLayoutDefect_RuntimeDefects_AreDefectsOthersAreNot`,
`MermaidDocumentTests.MermaidDocument_Measure_ResolverThrows_PropagatesUnchanged`

Asserts that the null-reference, index, missing-key, invalid-cast, argument, overflow, and invalid-operation
exceptions are defects of the layout, while a render failure, an unsupported feature, cancellation, a disposed
object, invalid data, and an exception marked as the image resolver's are not; and that an exception thrown by the
caller's image resolver during a measure reaches the caller as the same instance.

#### CanvasNetMermaid-MermaidDocument-DiagnosticCodes: Stable, Distinct Codes

**Tests**: `MermaidRecordsAndExceptionsTests.MermaidDiagnosticCodes_AllCodes_AreDistinctMermaidIdentifiers`,
`MermaidRecordsAndExceptionsTests.MermaidDiagnostic_ToString_PrefixesLocationAndCode`,
`MermaidRecordsAndExceptionsTests.MermaidUnsupportedFeature_ToString_IncludesFeatureAndLocation`,
`MermaidRecordsAndExceptionsTests.MermaidSize_Equality_IsByValue`

Reflects over the code constants and asserts that each is distinct and starts with `mermaid-`, and asserts the text
form of the two report records and the value equality of the size record.

#### CanvasNetMermaid-MermaidDocument-Options: Validated Options With Documented Defaults

**Tests**: `MermaidRenderOptionsTests.MermaidRenderOptions_Defaults_AreDocumented`,
`MermaidRenderOptionsTests.MermaidRenderOptions_Scale_Invalid_Throws`,
`MermaidRenderOptionsTests.MermaidRenderOptions_Theme_OnlyMermaidThemes_AreAccepted`,
`MermaidRenderOptionsTests.MermaidRenderOptions_Config_NotJsonObject_Throws`,
`MermaidRenderOptionsTests.MermaidRenderOptions_Limits_Null_Throws`,
`MermaidRenderOptionsTests.MermaidOpenOptions_SourceName_Whitespace_Throws`

Asserts the defaults, and that zero, negative, infinite, and not-a-number scales, unknown themes, configuration text
that is not a JSON object, null limits, and a white-space source name are refused when set.

#### CanvasNetMermaid-MermaidDocument-Limits: Default And Validated Limits

**Tests**: `MermaidLimitsTests.MermaidLimits_Default_HasDocumentedValues`,
`MermaidLimitsTests.MermaidLimits_Init_BelowOne_Throws`,
`MermaidLimitsTests.MermaidLimits_Init_DepthOutsideRange_Throws`

Asserts every default value and that a limit below one and a depth limit outside its range are refused.

#### CanvasNetMermaid-MermaidDocument-Reports: Ordered, De-Duplicated, Capped Delivery

**Tests**: `MermaidReportSinkTests.MermaidReportSink_Deliver_KeepsOrderAcrossKinds`,
`MermaidReportSinkTests.MermaidReportSink_Warn_Duplicate_IsRecordedOnce`,
`MermaidReportSinkTests.MermaidReportSink_BeyondCap_AddsSingleSuppressedNotice`,
`MermaidReportSinkTests.MermaidReportSink_Unsupported_Strict_RecordsAndThrows`,
`MermaidReportSinkTests.MermaidReportSink_DeliverIgnoringHandlerFailures_SwallowsHandlerException`

Asserts the delivery order across warnings and unsupported features, that an exact duplicate is kept once, that the
cap adds one suppression notice, that strict mode records and then throws, and that a handler's exception is ignored
when delivering after a failure.

#### CanvasNetMermaid-MermaidDocument-ImageSource: The Image Source

**Tests**: `MermaidImageSourceTests.MermaidImageSource_Properties_ClaimMermaidExtensionsAndTheme`,
`MermaidImageSourceTests.MermaidImageSource_Render_UnsupportedType_ReturnsTransparentPictureAndNote`,
`MermaidImageSourceTests.MermaidImageSource_Render_Flowchart_DrawsTheDiagram`,
`MermaidImageSourceTests.MermaidImageSource_Render_NaturalSizeBeyondMaximum_DrawsAtRequestedWidth`,
`MermaidImageSourceTests.MermaidImageSource_Render_UnsupportedTypeStrict_Throws`,
`MermaidImageSourceTests.MermaidImageSource_Render_ThemeFragment_ValidatedAgainstMermaidThemes`,
`MermaidImageSourceTests.MermaidImageSource_Render_InvalidText_ThrowsSyntaxException`,
`MermaidImageSourceTests.MermaidImageSource_Render_NullRequest_Throws`

Asserts the claimed extensions, schemes, and fragment parameter; a transparent picture at the requested size with one
located unsupported note for a sequence diagram, and an exception in strict mode; a flowchart at its natural size and
at a requested size with the `theme=dark` fragment; a 120-node chain wider than the largest surface (whose
`Measure` refuses the natural size) drawn 200 pixels wide with the height of its natural aspect ratio; the refusal
of an unknown theme; and the propagation of a syntax error and of a null request.

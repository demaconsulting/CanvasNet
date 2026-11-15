## MermaidDocument Unit Verification Design

This document describes the unit-level verification strategy for the `MermaidDocument` unit in the root namespace
`DemaConsulting.CanvasNet.Mermaid`: the public `MermaidDocument`, its options, limits, records, codes, and
exceptions, the internal `MermaidReportSink`, and the `MermaidImageSource`.

### Verification Approach

The unit is verified through its public surface and, for the report sink, through `InternalsVisibleTo`. The tests
use small inline diagrams (a two-node flowchart and a titled user journey from `MermaidTestHelpers`), byte
arrays and memory streams for the decoding cases, and temporary files for `Open(path)`. The lower subsystems are real,
not mocked: the unit's responsibility is the contract around them (arguments, decoding, metadata, option validation,
error shapes, report delivery, and the image-source adapter), and the subsystems are verified by their own tests.
The image source is exercised with `ImageSourceRequest` values built in the test, including fragments parsed by the
core `ImageFragment`.

Unit tests reside in the root of the `DemaConsulting.CanvasNet.Mermaid.Tests` project: `MermaidDocumentTests.cs`,
`MermaidRenderOptionsTests.cs`, `MermaidLimitsTests.cs`, `MermaidRecordsAndExceptionsTests.cs`,
`MermaidReportSinkTests.cs`, `MermaidImageSourceTests.cs`, and `MermaidDocumentPositionTests.cs` (the position
handler of `Render`, with small inline diagrams of every type that reports positions).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; report and position handlers are lists that collect the reports
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
`MermaidDocumentTests.MermaidDocument_Parse_JourneyDiagram_IsKnownButNotSupported`,
`MermaidDocumentTests.MermaidDocument_Parse_FrontmatterTitle_IsTitle`,
`MermaidDocumentTests.MermaidDocument_Parse_TitleSanitizedToEmpty_ReplacesTheFrontmatterTitle`,
`MermaidDocumentTests.MermaidDocument_Parse_ClassDiagram_CountsFollowTheTextConfiguration`

Asserts the type, support, direction, and counts of a flowchart, that a user journey opens as a known but
unsupported type, that the frontmatter title becomes the title, and that a packet, pie, gantt, or sequence title
statement that sanitizes to empty leaves the document without a title (while a bare pie `title` keeps the
frontmatter title); and that a class diagram whose directive sets
`class.hierarchicalNamespaces` to false, with one class in `namespace Geo.Plane` and one relation to a second class,
opens as the supported `class` type with the direction `TB` and the counts 2 nodes, 1 edge, and 1 subgraph: the
dotted namespace counts once, as it is drawn under the text's own configuration, where the default configuration
would count the implicit `Geo` as well.

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
`MermaidRenderOptionsTests.MermaidRenderOptions_Now_CanBeSet`,
`MermaidRenderOptionsTests.MermaidRenderOptions_Scale_Invalid_Throws`,
`MermaidRenderOptionsTests.MermaidRenderOptions_Theme_OnlyMermaidThemes_AreAccepted`,
`MermaidRenderOptionsTests.MermaidRenderOptions_Config_NotJsonObject_Throws`,
`MermaidRenderOptionsTests.MermaidRenderOptions_Limits_Null_Throws`,
`MermaidRenderOptionsTests.MermaidOpenOptions_SourceName_Whitespace_Throws`

Asserts the defaults (no clock among them: `Now` is null), that the clock can be set to a time with an offset of four
hours west and reads back unchanged, and that zero, negative, infinite, and not-a-number scales, unknown themes,
configuration text that is not a JSON object, null limits, and a white-space source name are refused when set.

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
located unsupported note for a user journey, and an exception in strict mode; a flowchart at its natural size and
at a requested size with the `theme=dark` fragment; a 120-node chain wider than the largest surface (whose
`Measure` refuses the natural size) drawn 200 pixels wide with the height of its natural aspect ratio; the refusal
of an unknown theme; and the propagation of a syntax error and of a null request.

#### CanvasNetMermaid-MermaidDocument-Positions: The Position Handler Of Render

**Tests**: `MermaidDocumentPositionTests.MermaidDocument_Render_FlowchartPositions_ReportSourceIdsAndEdges`,
`MermaidDocumentPositionTests.MermaidDocument_Render_FilledNode_CenterHasTheFill`,
`MermaidDocumentPositionTests.MermaidDocument_Render_ScaleAndFit_MapPositions`,
`MermaidDocumentPositionTests.MermaidDocument_Render_WithHandler_PixelsUnchangedAndMeasureSilent`,
`MermaidDocumentPositionTests.MermaidDocument_Render_HandlerThrows_PropagatesAndDeliversWarningsOnce`,
`MermaidDocumentPositionTests.MermaidDocument_Render_SequencePositions_ParticipantsMessagesNotes`,
`MermaidDocumentPositionTests.MermaidDocument_Render_GanttPositions_TasksMilestonesSections`,
`MermaidDocumentPositionTests.MermaidDocument_Render_PiePositions_SlicesWithValuesAimInside`,
`MermaidDocumentPositionTests.MermaidDocument_Render_GitGraphPositions_CommitsAtBullets`,
`MermaidDocumentPositionTests.MermaidDocument_Render_OtherTypes_ReportSourceIds`

Renders a titled flowchart with a subgraph, two parallel edges, a self-loop, and a filled node, and asserts the kinds
in report order (the title, the group, four nodes, then five edges), the source ids `start`, `stock`, `pack`, and
`send` and the group `ship`, the title text, the parallel edges from `stock` to `pack` numbered 0 and 1 (the first
with the text `yes` and line 6), and the self-loop as one edge at line 8. Asserts that the filled node's pixel is the
center of its box with its fill color painted on the vertical through it and that every box lies within the surface;
that at scale 2 every pixel and box width doubles and that the render at the exact doubled size maps within half a
pixel of it; that the pixels rendered with a handler, and with a handler and a filter of `node`, equal those rendered
without one, that the filter gives the four nodes only, and that `Measure` never calls the handler; and that a handler
that throws propagates its exception after the one warning and the one unsupported feature are each delivered once.
Asserts the participants `A` (text `Alice`) and `B`, the messages `1`, `2`, and `3` with the indexes 0, 0, and 1 and
the first from `A` to `B` with the text `Hi`, the note `note1`, and a message's pixel on its line of zero height; the
gantt tasks `des`, `task1` (synthetic), and `tst`, the milestone `frz`, and the sections `Build` and `Test` merged over
their rows; the pie slices `Dogs` (value 386) and `Cats`, the title `Pets`, and each slice's pixel painted in its own
color; the git graph commits `init` and `fix-1` aimed at the centers of their 20 pixel bullets, the branches `main`
and `dev`, and the tag `v1`; and the class nodes and note, the state node and edge, the ER entities `MEMBER` and
`LOAN` with their edge, the mindmap nodes `root` and `a`, and the packet blocks `0-7` and `8-15` (text `Data`).

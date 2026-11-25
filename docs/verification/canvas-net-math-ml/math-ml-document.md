## MathMlDocument Unit Verification Design

This document describes the unit-level verification strategy for the `MathMlDocument` unit in the
`DemaConsulting.CanvasNet.MathMl` namespace: the entry points, the description, measuring and rendering, the options and
limits, the records and exceptions, and the report sink.

### Verification Approach

The unit is verified through its public API and, for the internal report sink and the defect classification, through
the test assembly's `InternalsVisibleTo` access. The Syntax and Layout subsystems, the painter, and the bundled font are
used as is; no mocking is required. The handler tests use collecting and throwing handlers; the limit tests use small
limits instead of large inputs. Unit tests reside in `MathMlDocumentTests.cs`, `MathMlRenderOptionsTests.cs`,
`MathMlLimitsTests.cs`, `MathMlRecordsAndExceptionsTests.cs`, and `MathMlReportSinkTests.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory text and streams and one temporary path that does not exist; no shared state

### Acceptance Criteria

A unit-level test run passes when every scenario below passes. An exception type, a code, a location, a value, a size,
or an order of reports that differs from the expected one constitutes a failure.

### Unit-Level Test Scenarios

#### CanvasNetMathMl-MathMlDocument-ParseOpen: Arguments, Encodings, And Streams

**Tests**: `MathMlDocumentTests.MathMlDocument_Parse_NullText_ThrowsArgumentNullException`,
`MathMlDocumentTests.MathMlDocument_Open_NullArguments_AreRejected`,
`MathMlDocumentTests.MathMlDocument_Open_UnreadableStream_ThrowsArgumentException`,
`MathMlDocumentTests.MathMlDocument_Open_MissingFile_ThrowsFileNotFoundException`,
`MathMlDocumentTests.MathMlDocument_Open_StreamWithByteOrderMark_DropsIt`,
`MathMlDocumentTests.MathMlDocument_Open_InvalidUtf8_ThrowsSyntaxException`

Asserts the exceptions of a null text, a null, empty, or whitespace path, a null stream, a stream that cannot be read,
and a missing file; that a stream with a byte-order mark reads and stays open; and that bytes that are not UTF-8 throw a
`mathml-syntax-error` instead of being replaced.

#### CanvasNetMathMl-MathMlDocument-Describe: The Defaults Of The Description

**Tests**: `MathMlDocumentTests.MathMlDocument_Parse_DescriptionDefaults_AreInlineAndEmpty`

Asserts an inline display, no alternative text, no annotations, and the element count of a plain equation, and that an
invalid `display` value reads as inline.

#### CanvasNetMathMl-MathMlDocument-MeasureRender: The Picture Frame

**Tests**: `MathMlDocumentTests.MathMlDocument_Measure_EmptyMath_IsOnePixel`,
`MathMlDocumentTests.MathMlDocument_Measure_HugeSpace_ThrowsLimitExceeded`

Asserts that an empty `math` measures 1 by 1 and renders transparent, and that an `mspace` wider than a surface can be
fails with `mathml-limit-exceeded` while the fitting overload still renders it into a small size.

#### CanvasNetMathMl-MathMlDocument-RenderOptions: Defaults And Validation

**Tests**: `MathMlRenderOptionsTests.MathMlRenderOptions_Defaults_AreBrowserDefaults`,
`MathMlRenderOptionsTests.MathMlRenderOptions_FontSizeAndScale_InvalidValue_Throws`,
`MathMlRenderOptionsTests.MathMlRenderOptions_Limits_Null_ThrowsArgumentNullException`,
`MathMlRenderOptionsTests.MathMlOpenOptions_SourceNameAndLimits_AreValidated`

Asserts 16 px, scale 1, the document's display, black, and a transparent background; that zero, negative, infinite,
not-a-number, and too large font sizes and scales throw (boundary values from a theory); that null limits throw; and
that a whitespace source name throws while no limits mean the defaults.

#### CanvasNetMathMl-MathMlDocument-Limits: Values And The Work Limit

**Tests**: `MathMlLimitsTests.MathMlLimits_Default_HasDocumentedValues`,
`MathMlLimitsTests.MathMlLimits_ValueBelowOne_Throws`,
`MathMlLimitsTests.MathMlLimits_MaxNestingDepth_OutsideRange_Throws`,
`MathMlDocumentTests.MathMlDocument_Render_LayoutWorkLimit_ThrowsRenderException`

Asserts every documented default, that zero throws for every limit (with the property name), that a nesting depth of 0
or 1,025 throws, and that a small `MaxLayoutWork` fails a render with a located `mathml-limit-exceeded` naming the
property.

#### CanvasNetMathMl-MathMlDocument-Records: Records, Exceptions, And Codes

**Tests**: `MathMlRecordsAndExceptionsTests.MathMlLocation_ToString_FormatsEditorPosition`,
`MathMlRecordsAndExceptionsTests.MathMlDiagnostic_ToString_PrefixesKnownLocation`,
`MathMlRecordsAndExceptionsTests.MathMlSize_AndAnnotation_HaveValueEquality`,
`MathMlRecordsAndExceptionsTests.MathMlExceptions_LocatedConstructors_CarryCodeLocationAndHint`,
`MathMlRecordsAndExceptionsTests.MathMlExceptions_StandardConstructors_UseDefaultCodes`,
`MathMlRecordsAndExceptionsTests.MathMlDiagnosticCodes_AllCodes_AreDistinctMathMlIdentifiers`

Asserts the `name:line:column` formatting with and without a name and position, the location prefix of the reports, the
value equality of the size and the annotation, the code, location, hint, and message of the located exceptions, the
default codes and unknown location of the standard constructors, and that every published code is a distinct
`mathml-*` identifier.

#### CanvasNetMathMl-MathMlDocument-Reports: Ordered, De-Duplicated, Capped Delivery

**Tests**: `MathMlReportSinkTests.MathMlReportSink_Deliver_KeepsOrderAcrossKinds`,
`MathMlReportSinkTests.MathMlReportSink_Warn_Duplicate_IsRecordedOnce`,
`MathMlReportSinkTests.MathMlReportSink_BeyondCap_AddsSingleSuppressedNotice`,
`MathMlReportSinkTests.MathMlReportSink_DeliverIgnoringHandlerFailures_SwallowsHandlerException`,
`MathMlReportSinkTests.MathMlPendingReport_Replay_RecordsItsKind`,
`MathMlDocumentTests.MathMlDocument_Render_HandlerFailures_FollowTheDeliveryRules`

Asserts the order across warnings and unsupported features, that an identical report is recorded once, that the
reports beyond the cap collapse into one `mathml-diagnostics-suppressed` notice, that a failing handler is ignored after
a failure and propagates otherwise (through the sink and through a render), and that pending reports replay as their
kind.

#### CanvasNetMathMl-MathMlDocument-StrictMode: The First Unsupported Feature Throws

**Tests**: `MathMlReportSinkTests.MathMlReportSink_Unsupported_Strict_RecordsAndThrows`

Asserts that in strict mode the first unsupported feature is recorded, even beyond the cap, and thrown as a
`MathMlUnsupportedFeatureException` with its code and location.

#### CanvasNetMathMl-MathMlDocument-FailClosed: Defects Become Render Failures

**Tests**: `MathMlDocumentTests.MathMlDocument_IsLayoutDefect_ClassifiesExceptions`

Asserts that malformed font data and the runtime's programming-error exceptions are classified as defects (which become
located `mathml-render-failed` errors), and that `MathMlRenderException`, `MathMlUnsupportedFeatureException`,
`OperationCanceledException`, and `ObjectDisposedException` are not.

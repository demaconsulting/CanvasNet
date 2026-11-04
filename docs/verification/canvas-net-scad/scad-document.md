## ScadDocument Unit Verification Design

<!-- cspell:ignore Underflowing unlit supersampling gimbal -->

This document describes the unit-level verification strategy for the `ScadDocument` unit: the public types in
the `DemaConsulting.CanvasNet.Scad` namespace — `ScadDocument` (the `Parse` and `Open` entry points, the
`SourceName`, `Directives`, `ModuleNames`, and `FunctionNames` properties, and the `Evaluate`, `BuildScene`, and
`Render` methods), `ScadOpenOptions`, `ScadSourceLocation`, `ScadSyntaxException`, `ScadDirective`,
`ScadDirectiveKind`, the evaluation contract `ScadEvaluationOptions`, `ScadEvaluationResult`, `ScadLimits`,
`ScadDiagnostic`, `ScadUnsupportedFeature`, `ScadUnsupportedFeatureException`, and `ScadEvaluationException`,
and the render options `ScadRenderOptions`, the public view report `ScadResolvedView` with `ScadViewSource`, and the
internal viewport mapping and camera choice `ScadViewport` (with `ScadViewportPlan`).

### Verification Approach

The unit is verified by calling the public API directly and asserting on returned values, thrown exceptions, and
the state of the document. The unit's collaborators are the internal `ScadParser` and the internal
`ScadEvaluation` subsystem, which are used for real rather than mocked: the unit's job is to validate arguments,
bound and decode the input, call the parser, expose the result, and drive an evaluation with validated options and
ordered handler delivery, and the behavior of the parser and the evaluation units is verified under their own
verification designs. Internals made visible to the test assembly through `InternalsVisibleTo` (the document's
effective `Options`) are used to verify the base-directory default. The error contract is asserted precisely: a
syntax error is a `ScadSyntaxException`, which is a `FormatException` (not an `InvalidDataException`), source
longer than 4,194,304 UTF-16 characters throws exactly `InvalidDataException`, and an evaluation failure is a
`ScadEvaluationException`, which is an `InvalidOperationException` carrying a location.

Stream handling is verified with real `MemoryStream` instances, and the unbounded-read defense is verified with
a test-local `EndlessBlankStream` that yields blanks forever and counts the bytes handed to the reader, so that
the test can assert that reading stopped within a small margin beyond the limit. File handling uses the original
`.scad` fixtures copied to the test output directory.

The evaluation scenarios drive `Evaluate` over small in-memory programs and the fixtures, with explicit options:
default and null options, handlers that record the events they receive and the thread they run on, handlers that
throw, tiny limits that make each limit fail, tokens that are canceled beforehand or during the run, an
in-memory resolver that records every path it is asked for, temporary directories for the file-system sandbox,
callers on 64 KiB and 256 KiB stacks, and other cultures. The golden fixture tests (`ScadFixtureEvaluationTests`)
evaluate each `eval-*.scad` fixture through the public API and compare the echo lines, the diagnostics
(`line:column: message`), the unsupported-feature reports, and, through a test-only dumper, the instantiation
tree with the sibling `.txt` expectation file, so that the language semantics are pinned end to end; the
expectation files were authored for this repository and reviewed line by line.

Unit tests reside in `ScadDocumentTests.cs`, `ScadOpenOptionsTests.cs`, `ScadSourceLocationTests.cs`,
`ScadSyntaxExceptionTests.cs`, `ScadDocumentEvaluateTests.cs`, `ScadDocumentEvaluateSafetyTests.cs`,
`ScadEvaluationOptionsTests.cs`, `ScadLimitsTests.cs`, `ScadDiagnosticTests.cs`,
`ScadUnsupportedFeatureTests.cs`, `ScadEvaluationExceptionTests.cs`, `ScadFixtureEvaluationTests.cs`,
`ScadRenderOptionsTests.cs`, `ScadDocumentBuildSceneTests.cs`, `ScadDocumentRenderTests.cs`,
`ScadDocumentRenderCsgTests.cs`, `ScadDocumentRenderExtrusionTests.cs`, `ScadDocumentRenderReportTests.cs`,
`ScadViewportTests.cs`, and `ScadDocumentViewportRenderTests.cs`
within the `DemaConsulting.CanvasNet.Scad.Tests` project; the shared fixture and syntax-error helpers are in
`ScadTestHelpers.cs`. The rendering scenarios use the real Render3D renderer and read pixels back from the
returned surface, with flat, ambient-only lighting and orthographic cameras wherever an exact pixel count is
asserted.

The viewport variables and the choice of the camera are verified at two tiers. `ScadViewportTests.cs` calls the
internal `ScadViewport` directly, with no document and no renderer: the mapping of the orbit view and of a general
camera to `$vpr` is compared with closed-form values (`[90 - elevation, 0, azimuth]` and the rotation matrices of
`rotate()`), the round trip from a camera to the variables and back is compared eye by eye and up vector by up
vector (with a roll and in the gimbal-lock orientation), and `ChooseCamera` is called with hand-built plans,
`ScadDocumentViewport` values, and small scenes (a box away from the origin, and a thin slab for a flat model), whose
fitted cameras are compared with `Camera.FitToBounds` of the same bounds. `ScadDocumentViewportRenderTests.cs` goes
through the public API: a model echoes the four variables to show what the document sees for every source, drawing
sheets and a head-up display are rendered, a view handler records the view that was used, and the picture is read back
(a flat gray, an exact color, or a count of covered pixels) with `Supersampling = 1` where an exact pixel is asserted.
The facing of `translate($vpt) rotate($vpr)` is verified numerically on the built scene (the extent of the text along
the view direction and along the up direction of the picture), never by eye.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the internal parser and evaluation subsystem are exercised for real
- **Files**: The `ScadFixtures/` folder copied to the test output directory (read only); the file-system sandbox
  scenarios create and delete a per-test temporary directory
- **Threads**: The small-stack scenarios run `Evaluate` from a caller thread with a 64 KiB or 256 KiB stack and join
  it before asserting (only outcomes are asserted there, because the stack size a platform really grants differs);
  the handler-thread scenario forces the evaluator's fallback through the internal
  `ScadEvaluator.ForceFallbackForCurrentThread` seam, so that the evaluator must restart on its own thread, and
  compares the thread on which each handler ran with the caller's
- **Isolation**: Each test constructs its own sources, streams, options, and temporary files

### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those
explicitly asserted. Any unexpected exception, wrong exception type, wrong parameter name, wrong location, or
wrong reported name or list constitutes a failure.

### Unit-Level Test Scenarios

#### CanvasNetScad-ScadDocument-ImageSource: The Image Source For `.scad` Files

**Tests**: `ScadImageSource_Render_NoRequestedSize_DrawsNaturalSizeOnTransparentBackground`,
`ScadImageSource_Render_RequestedSize_DrawsAtThatSize`,
`ScadImageSource_Render_FlatModel_DrawsFilledTopDownShape`,
`ScadImageSource_Render_ModelWithViewport_UsesTheViewTheLibraryPicks`,
`ScadImageSource_Render_SizeBeyondRemainingPixels_IsRefusedBeforeDrawing`,
`ScadImageSource_Render_ModelWithEchoAndUnsupportedFeature_ReturnsEchoAndDiagnostics`,
`ScadImageSource_Render_UnsupportedFeatureWhenStrict_ThrowsUnsupportedFeatureException`,
`ScadImageSource_Render_Include_ResolvesFromDirectoryOfRequest`, `ScadImageSource_Render_InvalidInput_Throws`,
`ScadImageSource_Properties_NameExtensionAndNoSchemes`

`ScadImageSourceTests.cs` calls `ScadImageSource.Render` with requests built by hand (the bytes of a model, a requested
size, a
folder, the strictness switch), so the source is verified without any resolver. Asserts that a model without a requested
size is
800 by 800 pixels with a transparent corner and a drawn model, that a requested box is exactly that size and one
dimension keeps
the square aspect ratio, that a flat 2D model (`square(20, center = true)`) is drawn top-down as an axis-aligned
rectangle with the same drawn extent on every row, centered on the picture, with the far corners transparent, that a
3D model with a `$vpr` assignment is drawn exactly as `ScadDocument.Render` draws it with no view chosen (byte-identical
at the same size, transparent background, and orthographic projection) and differently from the same model without
the assignment, that a 4000 by 4000 size against a budget of 1000 pixels is refused with
`ImageSourceBudgetException` (16,000,000 and 1000 on the exception) before the model is parsed, with an allocation far
below the 64 MB of the picture, while a budget of exactly 60 by 40 pixels draws, that `echo` output and an
unsupported feature (`offset`) are returned as echo lines and an
`Unsupported` note of the source `scad`, that the strict request throws `ScadUnsupportedFeatureException`, that an
`include` is
found in the request's folder, is a `scad-warning` note when the folder lacks the file, and fails with
`ScadEvaluationException` when the request has no folder, that a syntax error is `ScadSyntaxException`, a request
without a file
`InvalidOperationException`, a null request `ArgumentNullException`, a canceled request `OperationCanceledException`,
and the name,
extension, and absence of schemes.

#### CanvasNetScad-ScadDocument-ParseString: Parse Handles Null, Empty, And BOM Source

**Tests**: `ScadDocumentTests.ScadDocument_Parse_NullSource_ThrowsArgumentNullException`,
`ScadDocumentTests.ScadDocument_Parse_EmptyOrCommentOnlySource_ReturnsEmptyDocument`,
`ScadDocumentTests.ScadDocument_Parse_LeadingBom_IsIgnored`

Asserts that a null source throws `ArgumentNullException` naming `source`; that empty, whitespace-only,
line-comment-only, block-comment-only, BOM-only, and semicolon-only sources parse to a document with no
directives, module names, or function names and a null `SourceName`; and that a leading byte-order mark
character is ignored when followed by a module definition.

#### CanvasNetScad-ScadDocument-OpenStream: Open Reads A Stream As UTF-8 And Bounds The Read

**Tests**: `ScadDocumentTests.ScadDocument_Open_NullStream_ThrowsArgumentNullException`,
`ScadDocumentTests.ScadDocument_Open_UnreadableStream_ThrowsArgumentException`,
`ScadDocumentTests.ScadDocument_Open_Utf8WithAndWithoutBom_Parses`,
`ScadDocumentTests.ScadDocument_Open_Stream_IsLeftOpen`,
`ScadDocumentTests.ScadDocument_Open_NonTerminatingStream_ThrowsInvalidDataException`

Asserts that a null stream throws `ArgumentNullException` and a disposed (unreadable) stream throws
`ArgumentException`, each naming `stream`; that UTF-8 input with and without a byte-order mark, containing
non-ASCII text in a comment and a string, parses identically; that reading starts at the stream's current
position, consumes the remainder, and leaves the stream readable; and that a stream that never ends throws
`InvalidDataException` (not a `ScadSyntaxException`) after reading no more than 65,536 bytes beyond the limit.

#### CanvasNetScad-ScadDocument-OpenPath: Open Reads A File By Path

**Tests**: `ScadDocumentTests.ScadDocument_Open_NullPath_ThrowsArgumentNullException`,
`ScadDocumentTests.ScadDocument_Open_WhitespacePath_ThrowsArgumentException`,
`ScadDocumentTests.ScadDocument_Open_FixtureFile_ParsesAndNamesSource`,
`ScadDocumentTests.ScadDocument_Open_MissingFile_ThrowsFileNotFoundException`,
`ScadDocumentTests.ScadDocument_Open_DefaultBaseDirectory_IsFileFolder`

Asserts that a null path throws `ArgumentNullException`; that empty and whitespace paths throw exactly
`ArgumentException` rather than the null subclass; that a fixture file parses and names its source with the path
as supplied; that a missing file throws `FileNotFoundException` and a missing folder throws
`DirectoryNotFoundException`; and that the effective base directory defaults to the folder of the file, can be
overridden through the options, and stays unset for a string.

#### CanvasNetScad-ScadDocument-SourceName: Source Name Reflects Its Supplier

**Tests**: `ScadDocumentTests.ScadDocument_SourceName_FromOptionsOrPath_ReflectsSupplier`,
`ScadDocumentTests.ScadDocument_Parse_SourceNameOption_AppearsInErrorLocation`

Asserts that `SourceName` is null for an unnamed string or stream, equals the `SourceName` option when one is
given, equals the path for a file, and is overridden by an explicit option; and that the supplied name appears
in the location (and therefore the message prefix) of a syntax error raised for that document.

#### CanvasNetScad-ScadDocument-Directives: Directives Are Recorded In Order Without Resolution

**Tests**: `ScadDocumentTests.ScadDocument_Directives_IncludeAndUse_AreRecordedInOrder`,
`ScadDocumentTests.ScadDocument_Directives_ReadOnlyList`,
`ScadDocumentTests.ScadDocument_Directives_FileResolverNotInvokedDuringParse`

Asserts that include and use directives are listed in source order with their kind, path, and location
(including a directive indented on a later line); that the directive, module-name, and function-name lists are
read-only and refuse mutation with `NotSupportedException`; and that a supplied `FileResolver` is never invoked
while parsing a document with two directives.

#### CanvasNetScad-ScadDocument-Definitions: Module And Function Names Are File Scope Only

**Tests**: `ScadDocumentTests.ScadDocument_ModuleAndFunctionNames_FileScopeOnly_InOrder`,
`ScadDocumentTests.ScadDocument_ModuleAndFunctionNames_Duplicates_AreKept`

Asserts that module and function names are reported in source order for file scope only, excluding a module and
a function nested in a module body and a module inside a bare block, and that duplicate definitions are each
kept.

#### CanvasNetScad-ScadDocument-SyntaxException: ScadSyntaxException Is A Located FormatException

**Tests**: `ScadSyntaxExceptionTests.ScadSyntaxException_IsFormatException`,
`ScadSyntaxExceptionTests.ScadSyntaxException_Location_MessageFormat`,
`ScadSyntaxExceptionTests.ScadSyntaxException_UnknownLocation_MessageHasNoPrefix`,
`ScadSyntaxExceptionTests.ScadSyntaxException_StandardConstructors_SetMessageAndInnerException`,
`ScadDocumentTests.ScadDocument_Parse_SyntaxError_ThrowsScadSyntaxExceptionWithLocation`

Asserts that the exception is assignable to `FormatException`; that a known location is exposed through
`Location` and prefixed to the message (`x.scad:4:9: Unexpected token ';'`); that a default (unknown) location
leaves the message bare; that the parameterless, message, and message-with-inner-exception constructors set the
message and inner exception and leave the location unknown; and that a syntax error in parsed source is reported
as this exception at the exact line and column with a message that begins with the location.

#### CanvasNetScad-ScadDocument-SourceLocation: ScadSourceLocation Is A Value Record

**Tests**: `ScadSourceLocationTests.ScadSourceLocation_Equality_SameComponents_AreEqual`,
`ScadSourceLocationTests.ScadSourceLocation_ToString_WithAndWithoutFile_FormatsCoordinates`,
`ScadSourceLocationTests.ScadSourceLocation_Default_IsUnknown`

Asserts value equality and hash equality of two locations with the same components and inequality when one
component differs; that `ToString` renders `file:line:column` with a file and `line:column` without one; and
that the default value has line and column zero and no file and renders as `?`.

#### CanvasNetScad-ScadDocument-OpenOptions: ScadOpenOptions Is Validated And Immutable

**Tests**: `ScadOpenOptionsTests.ScadOpenOptions_Defaults_AreUnsetAndEmpty`,
`ScadOpenOptionsTests.ScadOpenOptions_ValidValues_AreStored`,
`ScadOpenOptionsTests.ScadOpenOptions_InvalidValues_Throw`,
`ScadOpenOptionsTests.ScadOpenOptions_NullLibraryPaths_ThrowArgumentNullException`,
`ScadOpenOptionsTests.ScadOpenOptions_NullTextValues_AreAccepted`,
`ScadOpenOptionsTests.ScadOpenOptions_LibraryPaths_AreCopied`,
`ScadOpenOptionsTests.ScadOpenOptions_EvaluationOnlySettings_DoNotAffectParse`,
`ScadOpenOptionsTests.ScadOpenOptions_Properties_AreInitOnly`

Asserts that a new instance has unset base directory, source name, and file resolver and an empty library path
list; that valid values are stored; that empty or whitespace base directories, source names, and library path
entries throw `ArgumentException` naming the property; that a null library path list or a null entry throws
`ArgumentNullException` naming `LibraryPaths` while null optional text values are accepted; that the library
paths are copied defensively and exposed read-only; that a nonexistent base directory, nonexistent library paths,
and a resolver that must never be called leave the parsed directives and module names unchanged; and, by
reflection, that the class is sealed and that all four public properties can be set only during initialization.

#### CanvasNetScad-ScadDocument-SourceLimit: Oversize Source Is Rejected

**Tests**: `ScadDocumentTests.ScadDocument_Parse_OverLimit_ThrowsInvalidDataException`,
`ScadDocumentTests.ScadDocument_Open_OverLimitStream_ThrowsInvalidDataException`

Asserts that a string of 4,194,305 characters and a stream of 4,194,305 bytes are each rejected with exactly
`InvalidDataException`, not a `ScadSyntaxException` (the exact-limit acceptance case is covered at the system
tier and in the parser unit's source-length scenarios).

#### CanvasNetScad-ScadDocument-Evaluate: Evaluate Runs A Document From Any Origin

**Tests**: `ScadDocumentEvaluateTests.ScadDocument_Evaluate_DefaultOptions_ReturnsEchoDiagnosticsAndInstanceCount`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_NullOptions_BehavesLikeDefaults`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_OpenPath_ResolvesRelativeIncludeAndUse`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_OpenStreamWithResolver_UsesTheResolver`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_IncludeWithoutBaseOrResolver_ThrowsEvaluationException`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_MissingInclude_IsWarning`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_SyntaxErrorInIncludedFile_ThrowsSyntaxExceptionWithFileName`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_ConcurrentCalls_AreIndependentAndIdentical`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_GoldenFixtures_AreDiscoveredWithExpectedOutput`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_ExpectedFiles_HaveAFixture`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_Evaluate_MatchesExpectedEcho`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_Evaluate_MatchesExpectedDiagnostics`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_Evaluate_MatchesExpectedInstanceTree`

Asserts that evaluating with default options and with null options gives the same echo, diagnostics, and instance
count; that a document opened from a path resolves a relative include and use against its folder, and a stream
document needs and uses the resolver; that an include with no base directory, library path, or resolver throws a
`ScadEvaluationException`; that a missing include file is a warning and evaluation continues; that a syntax error
in an included file is a `ScadSyntaxException` whose location names that file; that several threads evaluating one
document get independent, identical results; and, for every golden fixture, that the echo lines, the diagnostics,
and the instantiation tree (and that every expectation file has a fixture and every golden fixture has an
expectation) match the checked-in files.

#### CanvasNetScad-ScadDocument-EvaluationResult: The Result Is Read-Only

**Tests**: `ScadDocumentEvaluateTests.ScadDocument_Evaluate_DefaultOptions_ReturnsEchoDiagnosticsAndInstanceCount`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_InstanceCount_CountsModuleInstancesAndGroups`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_ResultLists_AreReadOnly`

Asserts the echo lines (with the echo prefix), the located diagnostics, and the instance count of a small program;
that the count includes the groups of user modules and loops; and that the echo, diagnostics, and
unsupported-feature lists refuse mutation and cannot be cast to a mutable collection.

#### CanvasNetScad-ScadDocument-EvaluationHandlers: Handlers Receive Events In Order On The Caller's Thread

**Tests**: `ScadDocumentEvaluateTests.ScadDocument_Evaluate_Handlers_ReceiveEventsInOrderAndMatchResult`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_Failure_StillDeliversEventsToHandlers`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_HandlerThrowsOnSuccess_Propagates`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_HandlerThrowsOnFailure_EvaluationFailureWins`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_Handlers_RunOnCallerThread_EvenAfterStackRestart`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_ThrowOnUnsupportedFeature_ThrowsAfterDeliveringEarlierEvents`

Asserts that the three handlers receive exactly the events of the result in chronological order; that when
evaluation fails the events recorded before the failure are still delivered; that a handler exception on success
propagates but on a failure path the evaluation failure wins; that with a forced restart (through the internal
fallback seam) the evaluator ran on a different thread while every handler ran on the caller's thread and
each event was delivered once; and that with `ThrowOnUnsupportedFeature` the first unsupported feature throws after
the earlier events were delivered.

#### CanvasNetScad-ScadDocument-EvaluationOptions: ScadEvaluationOptions Is Validated And Immutable

**Tests**: `ScadEvaluationOptionsTests.ScadEvaluationOptions_Defaults_AreEmptyAndDefaultLimits`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Time_NonFiniteIsRejected`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Limits_NullIsRejectedAndValueKept`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Handlers_AreStored`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_AreCopiedAndOrderedByKey`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_SequencesAreSnapshotted`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_AcceptSupportedKinds`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_InvalidNameIsRejected`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_ValidNameIsAccepted`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_UnsupportedValueIsRejected`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_TooDeepNestingIsRejected`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_EndlessAndSelfReferentialSequencesAreRejected`,
`ScadEvaluationOptionsTests.ScadEvaluationOptions_Variables_NullMeansNone`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_Variables_InvalidValueIsRejectedByOptions`

Asserts the defaults (no variables, time zero, default limits, no handlers); that a NaN or infinite time throws
`ArgumentOutOfRangeException` and a null limits value `ArgumentNullException`; that the handlers are stored; that
the variables are copied into a name-ordered read-only dictionary and sequences are enumerated exactly once into a
snapshot; that every supported kind of value is accepted and an unsupported value, an invalid name (and the
names that are valid, including `$` names), a sequence nested too deeply, and an endless or self-referential
sequence throw `ArgumentException` naming the key; and that a null dictionary means no variables.

#### CanvasNetScad-ScadDocument-VariableOverrides: Variables Replace And Create Assignments

**Tests**: `ScadDocumentEvaluateTests.ScadDocument_Evaluate_Variables_ReplaceAndCreateAssignments`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_Variables_ConvertEverySupportedKind`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_Variables_ReplaceIncludedAssignmentsButNotUsedLibraries`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_Variables_SpecialVariablesAndOrderIndependence`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_Time_SetsSpecialVariable`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_Overrides_MatchExpectedEcho`

Asserts that an override replaces every assignment of the name (without evaluating the replaced expression) and
creates an absent variable; that booleans, every numeric type, strings, null, and sequences convert; that an
assignment brought in by `include` is replaced while a used library is untouched; that special variables can be
overridden and the result does not depend on the order of the supplied variables; that `Time` sets `$t`; and that
the overrides fixture matches its checked-in overridden expectation.

#### CanvasNetScad-ScadDocument-IncludeUse: Include And Use Are Confined To The Sandbox

**Tests**: `ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_FileSystemSandbox_EscapesAreRejected`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_FileSystemSandbox_LibraryPathIsAllowedRoot`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_ResolverSandbox_HostilePathsNeverReachTheCallback`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_ResolverMode_RequestsNormalizedLogicalPaths`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_IncludeLimitsAndCycles_ThrowEvaluationException`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_IncludeMain_ThroughResolver_MatchesExpectedEcho`

Asserts, in a temporary directory tree, that a path escaping the base directory (and an absolute path) is rejected
with a `ScadEvaluationException` even though the target file exists, and that a library path is an allowed root;
that hostile paths in resolver mode are rejected before the callback is invoked and that the callback only ever sees
normalized logical paths; that include cycles, excessive depth, and too many included files throw
`ScadEvaluationException`; and that the include fixture evaluates through a resolver to its expected echo.

#### CanvasNetScad-ScadDocument-Cancellation: Cancellation Stops Evaluation

**Tests**: `ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_PreCanceledToken_ThrowsBeforeAnyWork`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_CanceledDuringRun_StopsPromptlyAndDeliversEarlierEvents`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_CanceledDuringIncludeLoading_StopsBeforeNextFile`

Asserts that an already canceled token throws `OperationCanceledException` before any resolver call or handler
call; that a token canceled during endless recursion stops the evaluation promptly and the earlier echo lines are
still delivered; and that a token canceled by the resolver while a file is being loaded stops before the next file
is requested.

#### CanvasNetScad-ScadDocument-Limits: ScadLimits Is Validated And Enforced

**Tests**: `ScadLimitsTests.ScadLimits_Default_HasDocumentedValues`,
`ScadLimitsTests.ScadLimits_Initialization_AcceptsValidValues`,
`ScadLimitsTests.ScadLimits_MaxTriangles_MustBeAtLeastOne`,
`ScadLimitsTests.ScadLimits_MaxRecursionDepth_IsRangeChecked`,
`ScadLimitsTests.ScadLimits_MaxIncludeDepth_IsRangeChecked`,
`ScadLimitsTests.ScadLimits_PositiveLimits_RejectZeroAndNegative`,
`ScadLimitsTests.ScadLimits_Properties_AreInitOnly`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_Limit_ThrowsEvaluationExceptionWithLocation`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_Limit_AtLimitSucceedsOneOverFails`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_Limits_ArePassedThrough`

Asserts the documented default of each of the eleven limits (the triangle limit is 5,000,000); that valid values
are stored; that the triangle limit rejects values below one; that the recursion limit
accepts 16 to 16,384 and the include depth 1 to 64 and reject values outside; that every other limit rejects zero
and negative values; that every property is init-only; and, through `Evaluate`, that each of the recursion, step,
loop, list, string, instance, and output limits throws a `ScadEvaluationException` with a location, and that a
program exactly at a limit succeeds while one unit over fails; and that `BuildScene` passes the limits on (a model
that exceeds a tiny instance limit fails, and a model whose geometry exceeds a tiny triangle limit throws a
`ScadEvaluationException` whose message is `2:1: Triangle limit exceeded (limit 20)`).

#### CanvasNetScad-ScadDocument-StackSafety: Evaluate Does Not Overflow A Small Stack

**Tests**:
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_DeepRecursionOnSmallStack_AtLimitSucceedsAndOverLimitFailsIdentically`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_MaximumRecursionLimitOnSmallStack_Succeeds`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_SmallAndNormalStack_ProduceIdenticalResults`

Asserts that a recursion at the exact default limit succeeds on a small caller stack and one level more fails with
the same message and location as on a normal stack; that evaluation at the largest permitted recursion limit
(16,384) succeeds on a 64 KiB caller stack; and that a program gives an identical result on a small and on a
normal stack.

#### CanvasNetScad-ScadDocument-Determinism: Evaluate Is Repeatable And Culture Independent

**Tests**: `ScadDocumentEvaluateTests.ScadDocument_Evaluate_RepeatedAndCultureIndependent_ProducesIdenticalResults`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_ConcurrentCalls_AreIndependentAndIdentical`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_Evaluate_OnSmallStackUnderGermanCulture_MatchesExpectedOutput`,
`ScadFixtureEvaluationTests.ScadFixtureEvaluation_Evaluate_TwiceIsDeterministic`

Asserts identical results from repeated calls, calls under other cultures, and concurrent calls, and that every
golden fixture evaluated on a small stack under the German culture, and evaluated twice, gives exactly the checked-in
expected output.

#### CanvasNetScad-ScadDocument-Diagnostic: ScadDiagnostic Carries A Located Message

**Tests**: `ScadDiagnosticTests.ScadDiagnostic_Properties_AreExposedWithValueEquality`,
`ScadDiagnosticTests.ScadDiagnostic_ToString_PrefixesLocation`,
`ScadDiagnosticTests.ScadDiagnostic_ToString_IsCultureInvariant`

Asserts that the message and location are exposed with value equality, that `ToString` renders the location, a
colon, and the message, and that the text is identical under other cultures.

#### CanvasNetScad-ScadDocument-UnsupportedFeature: Unsupported Features Are Typed

**Tests**: `ScadUnsupportedFeatureTests.ScadUnsupportedFeature_Record_ExposesMembersAndFormats`,
`ScadUnsupportedFeatureTests.ScadUnsupportedFeatureException_Location_PrefixesMessage`,
`ScadUnsupportedFeatureTests.ScadUnsupportedFeatureException_UnknownLocation_MessageHasNoPrefix`,
`ScadUnsupportedFeatureTests.ScadUnsupportedFeatureException_StandardConstructors_SetMessageAndInnerException`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_UnsupportedFeatures_ReportedOncePerLocation`,
`ScadDocumentEvaluateTests.ScadDocument_Evaluate_UnsupportedFeature_WithoutThrowFlag_Continues`

Asserts the record's members and text form; that the exception is an `IOException` that exposes the feature and
location, prefixes its message with a known location and leaves it bare for an unknown one, and provides the
standard constructors; that an unsupported feature at one location is reported once however often it executes;
and that without the throw flag evaluation continues with `undef`.

#### CanvasNetScad-ScadDocument-EvaluationException: ScadEvaluationException Is A Located InvalidOperationException

**Tests**: `ScadEvaluationExceptionTests.ScadEvaluationException_Inheritance_DerivesFromInvalidOperationException`,
`ScadEvaluationExceptionTests.ScadEvaluationException_Location_MessageFormat`,
`ScadEvaluationExceptionTests.ScadEvaluationException_UnknownLocation_MessageHasNoPrefix`,
`ScadEvaluationExceptionTests.ScadEvaluationException_StandardConstructors_SetMessageAndInnerException`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_Limit_ThrowsEvaluationExceptionWithLocation`

Asserts that the exception is assignable to `InvalidOperationException`, exposes its location and prefixes the
message with it, leaves the message bare for an unknown location, provides the standard constructors, and is what
`Evaluate` throws when a limit is exceeded.

#### CanvasNetScad-ScadDocument-BuildScene: BuildScene Builds A Scene Of The Model

**Tests**: `ScadDocumentBuildSceneTests.ScadDocument_BuildScene_DefaultOptions_BuildsSceneOfTheModel`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_VariablesAndTime_ChangeTheGeometry`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_FlatShapeThickness_SetsSlabHeight`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_CameraAndViewAngles_SteerTheViewportVariables`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_InvalidCamera_DefinesNoViewportVariables`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_Projection_DoesNotChangeTheScene`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_ConcurrentBuilds_AreEqual`

Builds small programs with the default options and asserts the mesh nodes and bounds of the scene; builds with
variable overrides and a different animation time and asserts that the geometry changes; builds a 2D shape with
two thicknesses and asserts the slab height; builds a cube sized by `$vpd` and one sized by `$vpr[2]` and asserts
that the scene follows the viewport variables that the options define (the default distance 140 without options, the
distance 50 of an explicit camera even when an override of `$vpd` is also given, the override 70 alone, and the azimuth
of the default view and of an explicit azimuth of 90 degrees); builds the cube sized by `$vpd` with a camera whose eye
equals its target and with an orthographic camera that has no height (both are rejected by the renderer) and asserts
that the build does not throw, that the scene has the default distance 140 (not the distance of the rejected camera),
that no warning about a zero distance is delivered, that an explicit azimuth of 90 degrees still defines `$vpr` beside
the rejected camera while the default azimuth applies without it, and that `Render` still throws `ArgumentException`
for the same cameras (its behavior is unchanged); builds the cube sized by `$vpd` with an orthographic
and with a perspective projection and asserts that the scene is the same as with the default options (the projection
changes no viewport variable); and builds one document from several threads at once, asserting
that every scene has the same bounds and the same meshes.

#### CanvasNetScad-ScadDocument-Render: Render Validates, Fits, And Draws

**Tests**: `ScadDocumentRenderTests.ScadDocument_Render_InvalidSize_ThrowsBeforeEvaluating`,
`ScadDocumentRenderTests.ScadDocument_Render_InvalidCamera_ThrowsBeforeEvaluating`,
`ScadDocumentRenderTests.ScadDocument_Render_EmptyModel_IsBackgroundOnlyAtRequestedSize`,
`ScadDocumentRenderTests.ScadDocument_Render_DefaultFittedView_EqualsRenderFittedOfSameMesh`,
`ScadDocumentRenderTests.ScadDocument_Render_ExplicitOrthographicTopCamera_CoversExactFootprint`,
`ScadDocumentRenderTests.ScadDocument_Render_FittedViewOptions_ChangeTheImage`,
`ScadDocumentRenderTests.ScadDocument_Render_DefaultColorOption_IsHonored`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_DefaultThreeDimensionalView_IsUnchangedAndReportedOnce`

Asserts, for sizes outside the permitted range, that `ArgumentOutOfRangeException` is thrown with the parameter
name **before** the document is evaluated (a resolver that records its calls is never asked), and that an
invalid camera throws `ArgumentException` the same way; that a document without geometry renders a background-only
surface of the requested size; that the default render is byte-identical to `SceneRenderer.RenderFitted` of the
same hand-built mesh; that an explicit orthographic top camera covers exactly the analytic footprint of a cube;
that the azimuth, elevation, and projection options change the fitted image; that the default color of the
scene options is used for geometry without a `color()`; and that a cube drawn with the default options is drawn
exactly as with the default angles set explicitly (nothing changes for a model with 3D objects), the picture being
reported once as the view `(45, 35.26 degrees, perspective, not flat, Default)`.

#### CanvasNetScad-ScadDocument-RenderOptions: ScadRenderOptions Is Immutable And Validated

**Tests**: `ScadRenderOptionsTests.ScadRenderOptions_New_HasDocumentedDefaults`,
`ScadRenderOptionsTests.ScadRenderOptions_Initialization_AcceptsValidValues`,
`ScadRenderOptionsTests.ScadRenderOptions_HighlightColorAndGhostColor_DefaultsAndOverrideChangeTheLook`,
`ScadRenderOptionsTests.ScadRenderOptions_ViewAngles_NonFinite_Throw`,
`ScadRenderOptionsTests.ScadRenderOptions_ViewProperties_RecordWhetherTheyWereSet`,
`ScadRenderOptionsTests.ScadRenderOptions_Projection_Undefined_Throws`,
`ScadRenderOptionsTests.ScadRenderOptions_FlatShapeThickness_MustBeFiniteAndPositive`,
`ScadRenderOptionsTests.ScadRenderOptions_Properties_AreInitOnly`,
`ScadRenderOptionsTests.ScadRenderOptions_Evaluation_IsComposedAndOptional`,
`ScadRenderOptionsTests.ScadRenderOptions_FontResolution_DefaultsAndCopiesDirectories`,
`ScadRenderOptionsTests.ScadRenderOptions_FontResolution_InvalidDirectoriesAreRejected`

Asserts the documented defaults (including the translucent pink highlight `(255, 64, 128, 110)`, the translucent
gray ghost `(128, 128, 128, 80)`, no font resolution options, and no `ResolvedViewHandler`), that valid values are
kept (overlay colors of any alpha), that a non-finite
angle (NaN and both infinities), an
undefined projection, and a thickness that is zero, negative, or not finite are rejected with
`ArgumentOutOfRangeException` when set, that every writable property is init-only (by reflection), that the
default overlay colors blend with what they cover while opaque overrides are drawn in exactly their colors (a
`#` cube and a `%` cube in an orthographic flat render), that the
evaluation options are kept by reference, may be omitted, and may be shared between calls; that the font resolution
options default to using the installed fonts and no additional directories, copy the caller's directory list (a
later change has no effect and the exposed list is read-only), reject a null list and a null, empty, or white-space
directory when set, are init-only, and reach the geometry through the public render options (a text whose font
family is found only in an additional directory differs from the bundled one, with the system fonts disabled); and
that an options object with nothing set has no explicit view and no explicit projection (the explicit-view flag is
false, and so is the explicit-projection flag), while setting the azimuth to its default value, setting the
elevation to its default value, and setting the projection to its default value each make exactly that property
explicit (an azimuth or an elevation makes the view explicit but not the projection, and the projection makes the
projection explicit but not the view).

#### CanvasNetScad-ScadDocument-RenderReporting: Events Of Every Stage Are Delivered Once, In Order

**Tests**: `ScadDocumentBuildSceneTests.ScadDocument_BuildScene_Handlers_ReceiveAllStagesOnceInOrder`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_StrictMode_ThrowsAndStillDeliversEvents`,
`ScadDocumentBuildSceneTests.ScadDocument_BuildScene_FailingHandler_PropagatesOnlyOnSuccess`,
`ScadDocumentRenderTests.ScadDocument_Render_ReportsThroughEvaluationHandlersOnly`,
`ScadDocumentRenderTests.ScadDocument_Render_StrictMode_ThrowsForUnsupportedFeature`,
`ScadDocumentRenderTests.ScadDocument_Render_EvaluationFailure_PropagatesAfterDeliveringEvents`,
`ScadDocumentRenderTests.ScadDocument_Render_FailingHandlerOnSuccess_Propagates`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_AllStages_AreDeliveredInOrderOnTheCallingThread`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_StrictMode_ThrowsTheMappedOperandReport`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_FragmentBudgetExceeded_IsAnEvaluationException`

Asserts, with handlers that record the events and the thread they run on, that echo lines, warnings, and
unsupported features of evaluation, geometry building, and the renderer's operand checks are delivered once each,
in the order they occurred and on the calling thread; that strict mode throws at the first unsupported feature of
any stage after delivering the earlier events; that a handler that throws on a failure path never hides the
failure while one that throws on success propagates (and `Render` does not leak its surface); that the
unsupported-feature handler and the strict flag of the nested scene options are ignored; and that exceeding the
renderer's fragment budget is a `ScadEvaluationException`.

#### CanvasNetScad-ScadDocument-RenderOperandReports: Renderer Reports Map Back To The Source

**Tests**:
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_OpenPolyhedronInDifference_IsReportedAtThePolyhedron`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_MeshDefects_AreMappedWithTheirFeature`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_InvertedShellNextToCorrectShell_IsReported`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_UnderflowingWorldTransform_IsReportedPerOperand`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_RepeatedOperand_IsReportedOnce`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_DefectInUsedFile_CarriesTheFile`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_NoBooleanOrNoHandler_ReportsNothing`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_BuildScene_DoesNotValidateOperands`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_MapFeature_MapsEveryRenderToken`

Renders differences whose operands are defective polyhedra (open, non-manifold, inconsistently wound,
degenerate, inverted) and asserts that each defect is reported as the matching `scad-csg-*` unsupported feature
at the line and column of the module that produced the mesh (and the file of a used library); that a collapsed
world transform is reported per operand; that a repeated operand is reported once; that nothing is reported
without a boolean, without a handler, or by `BuildScene`; and that every Render3D report token maps to the
documented identifier.

#### CanvasNetScad-ScadDocument-RenderCancellation: Cancellation Stops Building And Rendering

**Tests**: `ScadDocumentBuildSceneTests.ScadDocument_BuildScene_AlreadyCanceled_ThrowsBeforeAnyResolverCall`,
`ScadDocumentRenderTests.ScadDocument_Render_AlreadyCanceled_ThrowsBeforeAnyResolverCall`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_CanceledDuringEvaluation_StopsBuildAndRender`,
`ScadDocumentRenderReportTests.ScadDocumentRenderReport_CanceledDuringRendering_StopsTheRender`

Asserts that an already canceled token throws `OperationCanceledException` before any resolver call, and that a
token canceled while the evaluation is running, or while the image is being rendered, stops the work with the
plain exception.

#### CanvasNetScad-ScadDocument-RenderDeterminism: Renders Are Reproducible On Any Stack

**Tests**: `ScadDocumentRenderTests.ScadDocument_Render_IsDeterministicForEveryParallelism`,
`ScadDocumentRenderTests.ScadDocument_Render_ConcurrentRenders_AreIdentical`,
`ScadDocumentRenderTests.ScadDocument_Render_DeepModuleChain_OnSmallStack_MatchesBigStack`,
`ScadDocumentRenderCsgTests.ScadDocument_Render_CsgModel_IsDeterministicForEveryParallelism`,
`ScadDocumentRenderCsgTests.ScadDocument_Render_DeepDifferenceChain_OnSmallStack_MatchesBigStack`

Asserts that renders with different degrees of parallelism, and renders from several threads at once, are
byte-identical, for a model with and without booleans, and that a deep module chain and a deep difference chain
render on 64 KiB and 256 KiB caller stacks to the same image as on a large stack.

#### CanvasNetScad-ScadDocument-RenderGeometry: Booleans, Extrusions, And Revolutions Draw As Their Solids

**Tests**: `ScadDocumentRenderCsgTests.ScadDocument_Render_CubeWithCylinderHole_ShowsTheHole`,
`ScadDocumentRenderCsgTests.ScadDocument_Render_FlatDifference_ShowsTheHole`,
`ScadDocumentRenderCsgTests.ScadDocument_Render_FlatIntersection_ShowsTheOverlapOnly`,
`ScadDocumentRenderCsgTests.ScadDocument_Render_CubeSphereIntersection_IsClippedByTheCube`,
`ScadDocumentRenderCsgTests.ScadDocument_Render_ColoredDifference_UsesTheColor`,
`ScadDocumentRenderExtrusionTests.ScadDocument_Render_ExtrudedRing_ShowsTheAnnulus`,
`ScadDocumentRenderExtrusionTests.ScadDocument_Render_RevolvedVase_HasTheProfileSilhouette`,
`ScadDocumentRenderExtrusionTests.ScadDocument_Render_ExtrudedDifference_EqualsTheHandBuiltDifferenceOfExtrusions`,
`ScadDocumentRenderExtrusionTests.ScadDocument_Render_RevolvedDifference_EqualsTheHandBuiltDifferenceOfRevolutions`,
`ScadDocumentRenderExtrusionTests.ScadDocument_Render_ColoredAndTwistedExtrusions_AreVisible`

Renders small models and asserts exact pixel counts and positions (the hole in a block shows the background over
the analytic pixel count, a 2D difference shows its hole, a 2D intersection only the overlap, a sphere is clipped
by a cube, a colored difference is drawn in its color, an extruded ring is an annulus, and a revolved vase has
the profile's radius at every row), and that an extruded or revolved 2D difference is pixel-identical to the
hand-built Render3D difference of the extruded or revolved parts.

#### CanvasNetScad-ScadDocument-ViewportVariables: The Camera And The Four Variables

**Tests**: `ScadViewportTests.ScadViewport_Wrap_GivesTheAngleInZeroToThreeSixty`,
`ScadViewportTests.ScadViewport_FromOrbit_MapsAzimuthAndElevationToViewportRotation`,
`ScadViewportTests.ScadViewport_Defaults_AreTheIsometricViewAndTheViewerDefaults`,
`ScadViewportTests.ScadViewport_FromCamera_OfAnOrbitView_DescribesTheSameRotationAsTheOrbitMapping`,
`ScadViewportTests.ScadViewport_ToCamera_RoundTripsAGeneralCamera`,
`ScadViewportTests.ScadViewport_ToCamera_PlacesTheEyeAlongTheRotatedZAxis`,
`ScadViewportTests.ScadViewport_CameraToWorld_ToViewerAxisPointsFromTheTargetToTheEye`,
`ScadViewportTests.ScadViewport_ChooseCamera_DocumentRotation_SteersTheFit`,
`ScadViewportTests.ScadViewport_ChooseCamera_AssignedTargetDistanceOrFieldOfView_GiveAManualCamera`,
`ScadViewportTests.ScadViewport_ChooseCamera_ManualCameraOnAFlatModel_LooksDown`,
`ScadViewportTests.ScadViewport_ChooseCamera_OnlyRotationAssigned_StaysFitted`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_Evaluate_ShowsTheViewerDefaults`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_Variables_FollowTheDocumentedPrecedence`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_HudAsWritten_EvaluatesAndIsPlacedByItsRotations`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_RotateVpr_FacesTheCamera`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_AssignedDistance_GivesAManualCamera`

Asserts that an angle wraps into 0 to 360 (`-10` gives 350, `360` and `-360` give 0, `720.5` gives 0.5) and that
noise of
a billionth of a degree around a full turn becomes exactly 0, never a negative zero; that the orbit views map to
`$vpr = [90 - elevation, 0, azimuth]` (front `[90, 0, 0]`, right `[90, 0, 90]`, back `[90, 0, 180]`, top
`[0, 0, 0]`, bottom
`[180, 0, 0]`, azimuth -45 at elevation 30 `[60, 0, 315]`) with the target at the origin, the distance 140, and the
field of view 22.5; that the library default view is `[90 - 35.26, 0, 45]` and the viewer's own default is
`[55, 0, 25]`; that
the camera that `Camera.FitToBounds` fits for eight orbit views (including straight down and straight up, where the
angles are not unique) describes the same rotation of the camera axes as the orbit mapping, with its target, its
distance, and its field of view; that five general cameras (tilted up hints, and a horizontal camera rolled a
quarter turn either way, which is the gimbal-lock orientation) rebuilt from their variables have the same eye, the
same target, and the same
up direction to 1e-8; that the rotation `[90, 0, 0]` with the target `(1, 2, 3)` and the distance 200 puts the eye at
`(1, -198, 3)` with the up vector +Z and, for an orthographic camera, the height `2 * 200 * tan(15 degrees)`; and that
`rotate($vpr)` turns +Z into the direction from the target to the eye and +Y into an up direction across it for four
orbit views.

Asserts that a `$vpr` that the document defined steers the fitted view (an orbit rotation `[90, 0, 0]` on a flat model
gives the front fit, and a roll `[60, 20, 10]` gives the eye along the rotated +Z and the up vector along the rotated
+Y, both reported as `Document`); that assigning `$vpt`, `$vpd`, and `$vpf` gives the manual camera with the eye at
`(1, -198, 3)`, the target `(1, 2, 3)`, the orthographic height `2 * 200 * tan(15 degrees)`, and a view azimuth and
elevation of 0,
and that assigning only `$vpd` of 50 keeps the target at the origin, puts the eye 50 away, and is reported as
`Document`;
that a flat model with a manual camera and no `$vpr` has its eye 80 above the origin and a top view; and that a
document assigning only `$vpr` keeps the fitted camera, which looks at the center of the scene bounds and not at the
default `$vpt`.

Asserts through the public API that `Evaluate` shows `[0, 0, 0]`, `[55, 0, 25]`, `140`, and `22.5` and no diagnostic;
that a model which echoes the four variables sees `ECHO: [0, 0, 0], [54.7356, 0, 45], 140, 22.5` by default; that
the head-up
display of `render-viewport-hud.scad` evaluates without a warning, builds its text, and lies in the plane that its
three negated rotations give (thin along that normal, not facing the viewer for the default view, which is the
documented meaning of `$vpr`); that `translate($vpt) rotate($vpr) linear_extrude(0.1) text("H", size = 5)` is as
thick as
0.1 along the direction to the viewer and between 4.5 and 5.5 along the up direction of the picture for the default
view, the explicit views (0, 0), (0, 90), (30, 20), and (200, -35), and a `$vpr` of `[60, 0, 20]` set by the document;
and that a model with `$vpd = 30`, `$vpr = [0, 0, 0]`, and a cube 500 units to the side draws nothing (a fitted view
would have framed it), looking straight down, with the view reported as `Document`.

#### CanvasNetScad-ScadDocument-ViewportPrecedence: Option, Override, Assignment, Default

**Tests**: `ScadViewportTests.ScadViewport_Plan_DefaultOptions_HasNothingExplicit`,
`ScadViewportTests.ScadViewport_Plan_ExplicitAnglesAndProjection_AreRecorded`,
`ScadViewportTests.ScadViewport_Plan_ExplicitCamera_SuppliesAllFourVariables`,
`ScadViewportTests.ScadViewport_MergeOverrides_TheOptionWinsOverTheCallerOverride`,
`ScadViewportTests.ScadViewport_ChooseCamera_ExplicitCamera_IsUsedAsGiven`,
`ScadViewportTests.ScadViewport_ChooseCamera_ExplicitAngles_WinOverTheFlatViewAndTheDocument`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_Variables_FollowTheDocumentedPrecedence`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_FlatModel_ExplicitViewsOverrideTheFlatDefault`

Asserts that the plan of default options has no override, no explicit angles, no explicit projection, no camera, and the
perspective projection; that explicit angles (an azimuth equal to its default included) override `$vpr` only (a
view of azimuth 30 and elevation 20 has the rotation `[70, 0, 30]`), and that an explicit projection overrides nothing;
that an explicit camera overrides `$vpd`, `$vpf`, `$vpr`, and `$vpt` and makes its target the default; that merging
replaces a caller's `$vpr` override by the option's (the other override stays) and returns the caller's dictionary
itself when the plan adds nothing; that an explicit camera is used as given, whatever the document says, reported with
its direction and the source `Camera`; and that explicit angles win over the flat view and over a `$vpr` of the document
(the fit of azimuth 30 and elevation 20, source `Options`).

Asserts, with ten programs that echo the variables, that the document sees for each source exactly the documented
value: the default view, the angles `[70, 0, 30]`, an explicit camera (`[1, 2, 3]`, `[90, 0, 0]`, `50`, `22.5`, also
over an assignment of `$vpd` and over a variable override), an assignment (`$vpd = 60`), a variable override over the
assignment (`$vpd` 70 and `$vpr` `[10, 20, 30]`), explicit angles over an assigned `$vpr`, and a two-component `$vpt`
assignment printed as `[4, 5]`, always with no warning; and that for a model that is entirely 2D explicit angles
(source `Options`, azimuth 30), an explicit camera (`Camera`), and an assigned `$vpr` (`Document`, azimuth and elevation
30) each win over the flat default, while an explicit projection alone leaves the flat view (`FlatDefault`) and
changes only the projection.

#### CanvasNetScad-ScadDocument-FlatModelView: A Model Of Only 2D Objects

**Tests**: `ScadViewportTests.ScadViewport_ChooseCamera_DefaultAndFlat_FitTheModel`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_ProjectionSheet_IsDrawnFlatFromTheTop`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_ProjectionSheet_ShowsBothViewsTheBorderAndTheText`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_ProjectionViews_ShowTheHolesTheStepsAndTheSection`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_MixedTwoAndThreeDimensions_KeepsTheThreeDimensionalView`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_FlatModel_ExplicitViewsOverrideTheFlatDefault`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_FlatDrawing_IsUnlitUnlessLightsAreChosen`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_FlatModelWithDocumentView_IsShaded`

Asserts that, with nothing chosen, a scene with a box is fitted at the isometric angles (source `Default`, not flat)
and a flat scene is fitted from the top (azimuth 0, elevation 90) and orthographically (`FlatDefault`, flat), and with
the projection set explicitly to perspective still from the top but in perspective; that the drawing sheet
`render-projection-sheet.scad` renders with no warning and no unsupported feature, reports exactly one view (a top view,
orthographic, flat, `FlatDefault`), has every pixel white or exactly the default gray (with supersampling off), covers
more than 10,000 pixels, and spans the picture to within its margin (the first covered column between 15 and 25 and the
last between 774 and 785 of 800); that, sampled with a known mapping, the two projections of the cube are 30 by 20 and
30 by 15 around their centers, the two-unit border and the title text are drawn, and everything else is white; that the
bushing sheet `render-projection-views.scad` shows the bore and the mounting holes in the top view, the plate and the
boss in the front view, and the bore and the walls in the section; that a model with a square and a cube keeps the
isometric view (`Default`, not flat), reports the usual warning, and equals the picture of the cube alone; that a
flat square is exactly the default gray at its center, a red square exactly `(255, 0, 0)`, and a square under an
ambient light of 0.5 set by the caller `(90, 90, 90)`; and that a flat model that sets `$vpr = [45, 0, 0]` is lit
like any other (its center is neither white nor the flat gray).

#### CanvasNetScad-ScadDocument-ResolvedView: The View That A Render Reports

**Tests**: `ScadDocumentViewportRenderTests.ScadDocumentViewport_DefaultThreeDimensionalView_IsUnchangedAndReportedOnce`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_ProjectionSheet_IsDrawnFlatFromTheTop`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_FlatModel_ExplicitViewsOverrideTheFlatDefault`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_ExplicitCamera_IsReportedAsTheSource`,
`ScadDocumentViewportRenderTests.ScadDocumentViewport_BuildScene_DoesNotCallTheViewHandler`

Asserts that `Render` calls the handler exactly once with the view it used: `(45, 35.26, perspective, not flat, Default)`
for a cube drawn with the default options, the top view of the drawing sheet as `FlatDefault`, and, for a flat model, `Options`,
`Camera`, and `Document` for the explicit angles, the camera, and the assigned `$vpr`; that a camera looking from `(0, -40,
0)` at the origin is reported with azimuth 0, elevation 0, and the source `Camera`; and that `BuildScene` with a handler
never calls it.

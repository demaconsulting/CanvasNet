## ScadDocument Unit Verification Design

This document describes the unit-level verification strategy for the `ScadDocument` unit: the public types in
the `DemaConsulting.CanvasNet.Scad` namespace — `ScadDocument` (the `Parse` and `Open` entry points, the
`SourceName`, `Directives`, `ModuleNames`, and `FunctionNames` properties, and the `Evaluate` method),
`ScadOpenOptions`, `ScadSourceLocation`, `ScadSyntaxException`, `ScadDirective`, `ScadDirectiveKind`, and the
evaluation contract `ScadEvaluationOptions`, `ScadEvaluationResult`, `ScadLimits`, `ScadDiagnostic`,
`ScadUnsupportedFeature`, `ScadUnsupportedFeatureException`, and `ScadEvaluationException`.

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
`ScadUnsupportedFeatureTests.cs`, `ScadEvaluationExceptionTests.cs`, and `ScadFixtureEvaluationTests.cs` within
the `DemaConsulting.CanvasNet.Scad.Tests` project; the shared fixture and syntax-error helpers are in
`ScadTestHelpers.cs`.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the internal parser and evaluation subsystem are exercised for real
- **Files**: The `ScadFixtures/` folder copied to the test output directory (read only); the file-system sandbox
  scenarios create and delete a per-test temporary directory
- **Threads**: The small-stack scenarios run `Evaluate` from a caller thread with a 64 KiB or 256 KiB stack (below
  or near the stack guard's reserve, so that the evaluator must restart on its own thread) and join it before
  asserting; the handler-thread scenario compares the thread on which each handler ran with the caller's
- **Isolation**: Each test constructs its own sources, streams, options, and temporary files

### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those
explicitly asserted. Any unexpected exception, wrong exception type, wrong parameter name, wrong location, or
wrong reported name or list constitutes a failure.

### Unit-Level Test Scenarios

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
`ScadOpenOptionsTests.ScadOpenOptions_AcceptedButNotConsumedYet_DoNotAffectParse`,
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
propagates but on a failure path the evaluation failure wins; that with a recursion deep enough to force a restart
on a 64 KiB caller stack the evaluator ran on a different thread while every handler ran on the caller's thread and
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
`ScadLimitsTests.ScadLimits_MaxRecursionDepth_IsRangeChecked`,
`ScadLimitsTests.ScadLimits_MaxIncludeDepth_IsRangeChecked`,
`ScadLimitsTests.ScadLimits_PositiveLimits_RejectZeroAndNegative`,
`ScadLimitsTests.ScadLimits_Properties_AreInitOnly`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_Limit_ThrowsEvaluationExceptionWithLocation`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_Limit_AtLimitSucceedsOneOverFails`

Asserts the documented default of each of the ten limits; that valid values are stored; that the recursion limit
accepts 16 to 16,384 and the include depth 1 to 64 and reject values outside; that every other limit rejects zero
and negative values; that every property is init-only; and, through `Evaluate`, that each of the recursion, step,
loop, list, string, instance, and output limits throws a `ScadEvaluationException` with a location, and that a
program exactly at a limit succeeds while one unit over fails.

#### CanvasNetScad-ScadDocument-StackSafety: Evaluate Does Not Overflow A Small Stack

**Tests**: `ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_DeepRecursionOnSmallStack_AtLimitSucceedsAndOverLimitFailsIdentically`,
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

**Tests**: `ScadEvaluationExceptionTests.ScadEvaluationException_IsInvalidOperationException`,
`ScadEvaluationExceptionTests.ScadEvaluationException_Location_MessageFormat`,
`ScadEvaluationExceptionTests.ScadEvaluationException_UnknownLocation_MessageHasNoPrefix`,
`ScadEvaluationExceptionTests.ScadEvaluationException_StandardConstructors_SetMessageAndInnerException`,
`ScadDocumentEvaluateSafetyTests.ScadDocument_Evaluate_Limit_ThrowsEvaluationExceptionWithLocation`

Asserts that the exception is assignable to `InvalidOperationException`, exposes its location and prefixes the
message with it, leaves the message bare for an unknown location, provides the standard constructors, and is what
`Evaluate` throws when a limit is exceeded.

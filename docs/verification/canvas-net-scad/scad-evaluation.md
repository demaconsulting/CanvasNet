## ScadEvaluation Subsystem Verification Design

<!-- cspell:ignore evaluator undef unseeded -->

This document describes the subsystem-level verification strategy for the `ScadEvaluation` subsystem (internal
namespace `DemaConsulting.CanvasNet.Scad.Evaluation`), containing four units: `ScadValue`, `ScadEvaluator`,
`ScadBuiltinFunctions`, and `ScadFileResolver`.

### Verification Approach

The `ScadEvaluation` subsystem is verified at two tiers. The unit tests (see _ScadValue_, _ScadEvaluator_,
_ScadBuiltinFunctions_, and _ScadFileResolver Unit Verification Design_ under `scad-evaluation/`) verify each unit
in isolation, including its internal types. The subsystem-level scenarios below verify the four units working
together through the subsystem's internal boundary, `ScadEvaluator.Evaluate`, which resolves files with the real
`ScadFileResolver`, evaluates with the real `ScadValue` and `ScadBuiltinFunctions`, and returns the instantiation
tree and the buffered events. They run over the original authored fixtures, over small programs with explicit
settings, over in-memory file sets, and over hostile programs with tiny limits and small caller stacks.

The subsystem has no dedicated test class: the evaluator is its only entry point, so the tests that drive the
entry point across the units are the subsystem tier, and they are cited here by name. They reside in
`ScadEvaluatorEntryPointTests.cs` (the entry point, the settings defaults, and the output accounting),
`ScadEvaluatorFixtureTests.cs` (the authored fixtures), `ScadEvaluatorIncludeUseTests.cs` (include and use through
the real resolver), `ScadEvaluatorDiagnosticsTests.cs`, `ScadEvaluatorLimitTests.cs`, `ScadEvaluatorStackTests.cs`,
`ScadEvaluatorCancellationTests.cs`, and `ScadEvaluatorDeterminismTests.cs`, in the `Evaluation` folder of the
`DemaConsulting.CanvasNet.Scad.Tests` project, with the shared helpers in `ScadEvaluationTestHelpers.cs` and
`ScadEvaluatorTestSupport.cs`. The same files also host the detailed unit-level scenarios of the evaluator unit;
the subsystem scenarios cite the cross-unit subset.

Trees are compared through `ScadInstanceDumper`, a test helper that renders an instantiation tree as compact,
location-free text with one instance per line (and, for the golden files, an indented form that also shows
fragment settings that differ from the defaults). The dumper is **iterative**, because a recursive dumper
overflowed a 256 KiB test thread on a 5,000-level tree; the same constraint applies to the `ScadGeometry`
subsystem, whose builder never recurses (see _ScadEvaluation Subsystem Design_ and _ScadGeometry Subsystem
Design_). No mocking or stubbing is used: the only test doubles are
the in-memory `ScadVirtualFiles` resolver, which records every path it is asked for, and per-test temporary
directories. The subsystem is internal, so the tests reach it through the test assembly's `InternalsVisibleTo`
access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: The authored fixtures in the `ScadFixtures/` folder copied to the test output directory (read only)
- **Threads**: The stack-safety scenarios run the evaluation from a caller thread with a 64 KiB (tiny), a
  256 KiB or 1 MiB (small), or a 256 MiB (big, so that the whole pass fits inline) stack and join it before
  asserting; a normal evaluation runs on the caller's thread and only an evaluation that
  the caller's stack cannot hold restarts on the evaluator's own bounded 384 MiB thread (observed through the
  internal `LastRunThreadId`, `FallbackCount`, and `PeakConcurrentFallbackThreads` hooks)
- **Isolation**: Each test evaluates its own sources and settings; the file-system scenarios create and delete
  their own temporary directories

### Acceptance Criteria

The `ScadEvaluation` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the four unit verification documents passes without error or unexpected exception, on every target
framework. A wrong echo text, diagnostic text or location, exception type, limit message, instance-tree shape, or
thread observation constitutes a failure.

### Test Scenarios

- **Values Behave As Documented End To End** (requirement `CanvasNetScad-ScadEvaluation-Values`):
  `ScadEvaluatorFixtureTests.ScadEvaluatorFixture_Evaluate_CompletesWithoutWarnings`,
  `ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_UnderOtherCultures_GivesIdenticalResults` — evaluates
  every authored fixture and asserts that it completes without warnings (the fixtures exercise numbers, strings,
  vectors, ranges, function values, operators, indexing, and formatting), and evaluates a number-formatting
  program under other cultures and asserts the same text.
- **Evaluation Semantics On Realistic Programs** (requirement `CanvasNetScad-ScadEvaluation-Evaluation`):
  `ScadEvaluatorFixtureTests.ScadEvaluatorFixture_Evaluate_CompletesWithoutWarnings`,
  `ScadEvaluatorFixtureTests.ScadEvaluatorFixture_ParametricBracket_ProducesInstances`,
  `ScadEvaluatorEntryPointTests.ScadEvaluatorEntryPoint_Evaluate_ResolvedUnit_ProducesInstances` — evaluates the
  authored fixtures and the parametric bracket (modules, children, loops, functions, and special variables
  working together) and asserts the expected instances, and evaluates an already resolved unit to the expected
  instantiation tree.
- **Instantiation Tree Contract** (requirement `CanvasNetScad-ScadEvaluation-InstanceTree`):
  `ScadEvaluatorFixtureTests.ScadEvaluatorFixture_ParametricBracket_ProducesInstances`,
  `ScadEvaluatorEntryPointTests.ScadEvaluatorEntryPoint_Output_ReportsAccounting`,
  `ScadEvaluatorEntryPointTests.ScadEvaluatorEntryPoint_BuiltinModules_RecognizeGeometryNamesOnly` — asserts the
  shape of the tree for the bracket, that the output reports the instance, step, and loop-iteration counts, and
  that the recognized built-in module names are exactly the geometry names (so that an unknown name is a warning,
  not a node).
- **Built-In Functions In Context** (requirement `CanvasNetScad-ScadEvaluation-BuiltinFunctions`):
  `ScadEvaluatorFixtureTests.ScadEvaluatorFixture_Evaluate_CompletesWithoutWarnings`,
  `ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_Rands_AreReproduciblePerEvaluation` — evaluates the
  fixtures that call the built-in functions without warnings, and asserts that a program that draws random
  numbers without a seed gets the same numbers in every evaluation.
- **Include And Use Through The Resolver** (requirement `CanvasNetScad-ScadEvaluation-FileResolution`):
  `ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Include_BehavesAsTextualInclusion`,
  `ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Use_ImportsOnlyDefinitions`,
  `ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_MissingFile_WarnsAndContinues`,
  `ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_SyntaxErrorInIncludedFile_Propagates`,
  `ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Resolver_RunsOnCallingThreadBeforeEvaluation`,
  `ScadEvaluatorEntryPointTests.ScadEvaluatorEntryPoint_Evaluate_IncludeWithoutAnySource_IsAnEvaluationError` —
  asserts that included statements behave as if written in place, that a used library contributes only its
  definitions, that a missing file is a located warning and evaluation continues, that a syntax error in an
  included file names that file, that the resolver callback ran on the calling thread and before any echo output,
  and that a directive with no place to look is an evaluation error.
- **Resource Limits Across The Subsystem** (requirement `CanvasNetScad-ScadEvaluation-ResourceLimits`):
  `ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_AtLimitSucceeds_OneOverFails`,
  `ScadEvaluatorLimitTests.ScadEvaluatorLimit_LoopIterations_AtLimitSucceeds_OneOverFails`,
  `ScadEvaluatorLimitTests.ScadEvaluatorLimit_ListLength_AtLimitSucceeds_OneOverFails`,
  `ScadEvaluatorLimitTests.ScadEvaluatorLimit_Instances_AtLimitSucceeds_OneOverFails`,
  `ScadEvaluatorLimitTests.ScadEvaluatorLimit_OutputLength_AtLimitSucceeds_OneOverFails`,
  `ScadEvaluatorLimitTests.ScadEvaluatorLimit_Include_CycleAndDepth_AreEvaluationErrors`,
  `ScadEvaluatorLimitTests.ScadEvaluatorLimit_Diagnostics_AreCappedWithNotice` — for each limit, evaluates a
  program at the limit (accepted) and one unit over (a located `ScadEvaluationException` with the documented
  message), with small explicit limits so that the tests are fast, and asserts that an include cycle and
  excessive include depth are evaluation errors and that warnings beyond the cap collapse into one notice.
- **Stack Safety And The Restart** (requirement `CanvasNetScad-ScadEvaluation-StackSafety`):
  `ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_AtLimit_OnSmallCallerStack_MatchesBigStackResult`,
  `ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_OverLimit_IsIdenticalOnEveryStackSize`,
  `ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_ValueNestingAtLimit_OnSmallCallerStack_MatchesBigStack`,
  `ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_TinyCallerStack_FallsBackToDedicatedThread`,
  `ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_Restart_DoesNotRepeatOrMoveResolverCalls` — asserts that
  programs at the nominal limit give the same result on small and large caller stacks, that a program over the
  limit fails with the same message and location on every stack size, that values nested 256 levels deep survive
  a small stack, that a tiny caller stack hops to the evaluator thread, and that a restart neither repeats nor moves
  a call of the caller's resolver.
- **Cancellation Across The Subsystem** (requirement `CanvasNetScad-ScadEvaluation-Cancellation`):
  `ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_AlreadyCanceled_ThrowsBeforeAnyResolverCall`,
  `ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_DuringEndlessTailRecursion_StopsAndKeepsEarlierEvents`,
  `ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_DuringIncludeLoading_StopsBeforeNextFile` — asserts
  that an already canceled token throws before any resolver call, that cancellation during endless tail recursion
  stops promptly with the earlier events kept, and that cancellation while files are loading stops before the next
  file.
- **Variable Overrides Reach Included Files** (requirement `CanvasNetScad-ScadEvaluation-Overrides`):
  `ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Overrides_ReplaceIncludedAssignmentsButNotLibraries` —
  asserts that an override replaces the assignments of the main file and of the files it includes, and leaves a
  used library untouched.
- **Warnings Are Located And Do Not Abort** (requirement `CanvasNetScad-ScadEvaluation-Diagnostics`):
  `ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_Warning_HasExpectedTextAndLocation`,
  `ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_Location_CarriesTheSourceName`,
  `ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_MissingFile_WarnsAndContinues` — asserts the exact text
  and `line:column` of each warning kind, that a location names the file it is in, and that a missing file does
  not stop evaluation.
- **Unsupported Features** (requirement `CanvasNetScad-ScadEvaluation-UnsupportedFeatures`):
  `ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_UnsupportedFunction_IsReportedAndEvaluationContinues`,
  `ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_ThrowOnUnsupportedFeature_AbortsWithFeatureAndLocation`
  — asserts that a recognized but unsupported function is reported with its feature identifier while evaluation
  continues with `undef`, and that with the throw setting the first use aborts with the feature and location.
- **Errors Abort With A Location And Keep The Output So Far** (requirement `CanvasNetScad-ScadEvaluation-Errors`):
  `ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_AssertFailure_HasLocationAndMessage`,
  `ScadEvaluatorLimitTests.ScadEvaluatorLimit_FailedEvaluation_KeepsEventsRecordedBeforeTheFailure`,
  `ScadEvaluatorEntryPointTests.ScadEvaluatorEntryPoint_Evaluate_IncludeWithoutAnySource_IsAnEvaluationError` —
  asserts the located failure of an assertion with and without a message, that the sink still holds the echo and
  warnings recorded before a failure, and that an include with nowhere to look is a located evaluation error.
- **Determinism** (requirement `CanvasNetScad-ScadEvaluation-Determinism`):
  `ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_SameDocumentTwice_GivesIdenticalResults`,
  `ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_UnderOtherCultures_GivesIdenticalResults`,
  `ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_Rands_AreReproduciblePerEvaluation`,
  `ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_ConcurrentEvaluations_GiveIdenticalResults` — asserts
  identical echo, diagnostics, and tree dumps for the same program evaluated twice, under other cultures,
  with unseeded random numbers, and concurrently from several threads.

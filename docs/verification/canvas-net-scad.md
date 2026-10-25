# System Verification Design

This document describes the system-level verification strategy for CanvasNetScad.

## Verification Approach

`CanvasNetScad` is verified at three tiers. The system tier, described here, exercises the library purely
through its public API from the perspective of a consumer: it opens the original-work `.scad` fixture files and
in-memory source with `ScadDocument.Parse` and `ScadDocument.Open`, supplies `ScadOpenOptions` and
`ScadEvaluationOptions`, evaluates documents with `ScadDocument.Evaluate`, and asserts on observable outputs —
the `ScadDocument` properties (`SourceName`, `Directives`, `ModuleNames`, `FunctionNames`), the
`ScadSourceLocation` carried by a `ScadSyntaxException` or a `ScadEvaluationException`, the
`ScadEvaluationResult` (echo lines, diagnostics, unsupported features, instance count), the events delivered
to handlers, and the exception types thrown for invalid arguments and hostile input. No mocking or stubbing is
used. The subsystem tier (see _ScadLanguage Subsystem Verification Design_ and _ScadEvaluation Subsystem
Verification Design_ under `canvas-net-scad/`) verifies the lexer and parser working together and the
evaluation units working together, and the unit tier verifies each unit in isolation (see _ScadDocument Unit
Verification Design_ and, under `canvas-net-scad/scad-language/` and `canvas-net-scad/scad-evaluation/`, the
unit verification designs), including the internal types made visible to the test assembly through
`InternalsVisibleTo`.

The parse scenarios assert on what a document reports about its
source: that the three entry points (string, stream, and path) agree, that every
authored fixture parses, that malformed fixtures fail at an exact file, line, and column, that directives and
definitions are reported in source order, and that hostile input (excessive nesting, oversize source) fails
closed with a typed exception that callers can catch. The error contract is part of what is verified:
`ScadSyntaxException` derives from `FormatException` (it does **not** derive from `InvalidDataException`, which
is sealed in the base class library), while source longer than 4,194,304 characters throws a plain
`InvalidDataException` because no source location is meaningful for it.

The evaluation scenarios (Phase 5) assert on what evaluating a document produces, still without geometry: the
echo lines of a program and the located failure of a failed assertion; the instance count of the parametric
fixtures; the validation and immutability of the evaluation options; warnings for unknown names; unsupported
features reported or thrown; `include` and `use` through a resolver and through the file system, with the
sandbox rejecting an escaping path before it reaches the callback; every resource limit failing closed with a
located `ScadEvaluationException`; deep recursion on a small caller stack neither overflowing nor changing the
outcome; cancellation never being wrapped; variable overrides replacing assignments; and determinism across
repeated calls and cultures. The detailed semantics (scoping, special variables, children, built-in functions,
number formatting, and so on) are verified at the unit tier and, end to end through the public API, by the
golden fixture tests described in _ScadDocument Unit Verification Design_.

System tests reside in `ScadSystemIntegrationTests.cs` within the `DemaConsulting.CanvasNet.Scad.Tests`
project; the shared helpers (fixture discovery and the syntax-error assertion) are in `ScadTestHelpers.cs`.
The fixtures are original works, authored for this repository without reference to any third-party
implementation, grammar, manual, or example (clean-room provenance, documented in
`ScadFixtures/README.md` beside the fixtures).

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline, on every target framework
  (`net8.0`, `net9.0`, `net10.0`) and on the Windows, Linux, and macOS CI runners
- **Dependencies**: No external services, databases, or network access required. The fixture files in
  `test/DemaConsulting.CanvasNet.Scad.Tests/ScadFixtures/` (twenty-six valid `.scad` files — ten parse
  fixtures and sixteen golden evaluation fixtures `eval-*.scad` with their `.txt` expectation files — three
  `invalid-*.scad` files, the include libraries in `libs/`, and a provenance `README.md`) are copied to the
  test output directory and located relative to the test assembly
- **Threads**: The small-stack scenarios run `Evaluate` from a caller thread with a 256 KiB stack, and the
  culture scenario from threads with a chosen current culture, joining each thread before asserting
- **Isolation**: Each test method reads the fixtures it needs and builds its own options and sources; no shared
  state between tests. No test writes to the file system; the one test that supplies a base directory during
  parsing uses a path that does not exist, to prove that it is never touched, and the include scenarios read
  only the checked-in fixtures or an in-memory resolver

## External Interface Simulation

The external interfaces of the system are the file system (the `ScadDocument.Open(string)` overload and the
resolution of `include` and `use` files), streams (the `ScadDocument.Open(Stream)` overload), and the caller's
file resolver callback. The file system and streams are not simulated: file input uses the real fixture files
copied to the output directory, and stream input uses real `MemoryStream` instances over fixture or generated
bytes. The file resolver callback is a real delegate over in-memory streams that records every path it is
asked for, so that the sandbox scenario can assert that a hostile path never reaches it. The one case that must
never touch the file system while parsing — directive recording — is verified by supplying a base directory,
library paths, and a `FileResolver` that would fail the test if they were ever used, and by asserting that
parsing succeeds regardless. No mocking framework is used.

## System-Level Test Scenarios

### Open: String, Stream, And Path Entry Points Agree

**Test**: `CanvasNetScad_SystemIntegration_Open_ParseStringOpenStreamAndOpenPathAgree`

For every valid fixture, parses the file text with `ScadDocument.Parse`, the open file stream with
`ScadDocument.Open(Stream)`, and the path with `ScadDocument.Open(string)`, giving the string and stream
entry points the same `SourceName` option. Asserts that the three documents report the same source name,
directives, module names, and function names, confirming that the entry points yield an identical result.

### Open: Invalid Arguments Throw

**Test**: `CanvasNetScad_SystemIntegration_Open_InvalidArgumentsThrow`

Asserts `ArgumentNullException` for a null source text, a null path, and a null stream, `ArgumentException` for
a whitespace-only path and for an unreadable (disposed) stream, and `FileNotFoundException` for the path of a
file that does not exist. Confirms that invalid arguments fail closed with the documented exception contract
rather than being coerced.

### Options: Validated And Immutable

**Test**: `CanvasNetScad_SystemIntegration_Options_AreValidatedAndImmutable`

Asserts that `ScadOpenOptions` rejects a whitespace base directory, an empty source name, and a whitespace
library path with `ArgumentException` when they are set, that the library path list is copied (a later change
to the caller's list does not alter the options), and that a supplied `SourceName` flows into the location of a
`ScadSyntaxException` raised while parsing with those options.

### Syntax: All Authored Fixtures Parse

**Test**: `CanvasNetScad_SystemIntegration_Syntax_AllAuthoredFixturesParse`

Opens every valid fixture from disk with `ScadDocument.Open` and asserts that exactly twenty-six documents result
(the ten parse fixtures and the sixteen golden evaluation fixtures), each named by its path. The fixtures cover
assignments, modules and functions, control flow, expressions and
operators, list comprehensions, function literals, modifiers, include and use directives, lexical forms, a
realistic parametric bracket, and the evaluation topics. This is also the shared platform-proof test referenced by every
`CanvasNetScad-Platform-*` requirement, run on each operating system and target framework.

### Syntax: Parametric Part Fixture Reports Definitions

**Test**: `CanvasNetScad_SystemIntegration_Syntax_ParametricPartFixtureReportsDefinitions`

Opens the `parametric-bracket.scad` fixture and asserts that `ModuleNames` is `bracket_plate`, `bracket_rib`,
`mounting_holes`, `shelf_bracket` and `FunctionNames` is `rib_height`, `hole_pitch`, `hole_positions`, in source
order, with no nested or imported definitions and no directives.

### Syntax Errors: Malformed Fixtures Report Location

**Test**: `CanvasNetScad_SystemIntegration_SyntaxErrors_MalformedFixturesReportLocation`

Opens each of the three `invalid-*.scad` fixtures and asserts that a `ScadSyntaxException` is thrown whose
location carries the fixture path and the documented one-based line and column (an unterminated block comment at
4:1, a missing semicolon at 4:1, and an unbalanced brace at the end of input at 5:1), that the message begins
with that location, and that it contains the expected description. Also asserts that every invalid fixture in
the folder is covered.

### Syntax Errors: First Error Is Reported

**Test**: `CanvasNetScad_SystemIntegration_SyntaxErrors_FirstErrorIsReported`

Parses a source with three independent grammar errors on three lines and asserts that the exception reports
only the first, at line 1, column 5, with no file name, and that the exception is assignable to
`FormatException` (the exception is a `FormatException`, not an `InvalidDataException`).

### Directives: Include And Use Recorded Without File Access

**Test**: `CanvasNetScad_SystemIntegration_Directives_IncludeAndUseRecordedWithoutFileAccess`

Parses the include-and-use fixture text with a nonexistent base directory, a nonexistent library path, and a
file resolver that throws if invoked, and asserts that parsing succeeds and that the four directives are
recorded in source order with their kinds, paths, and line numbers (the referenced files do not exist, and none
is opened or resolved).

### Limits: Deep Nesting Fails Closed

**Test**: `CanvasNetScad_SystemIntegration_Limits_DeepNestingFailsClosed`

Parses three hostile sources nested 100,000 levels deep (parentheses, braces, and a long chain of additions) and
asserts that each fails with a `ScadSyntaxException` whose message contains `Nesting too deep`, rather than
crashing the process with a stack overflow.

### Limits: Oversized Source Is Rejected

**Test**: `CanvasNetScad_SystemIntegration_Limits_OversizedSourceIsRejected`

Asserts that a source of exactly 4,194,304 characters is accepted and that the same source with one more
character is rejected, both through `ScadDocument.Parse` and through `ScadDocument.Open(Stream)`, with exactly
`InvalidDataException` (not a `ScadSyntaxException`). The bounded read of a stream that never ends is verified at
the unit tier (see _ScadDocument Unit Verification Design_).

### Evaluate: Echo Output And Assertion Failure

**Test**: `CanvasNetScad_SystemIntegration_Evaluate_EchoAndAssertReportOutputAndFail`

Evaluates a passing program (an echo of a string and a sum, a true assertion, an echo of a vector with a string)
and asserts the echo lines `ECHO: "sum", 3` and `ECHO: [1, "a"]` in order. Evaluates a program whose assertion
fails with a message (named `check.scad`, with an echo before and after it) and asserts a
`ScadEvaluationException` (an `InvalidOperationException`) located at line 2, column 1 with the message
`check.scad:2:1: Assertion failed: order is wrong`, and that the echo handler received exactly the line produced
before the failure.

### Evaluate: Parametric Fixture Produces Instances

**Test**: `CanvasNetScad_SystemIntegration_Evaluate_ParametricFixtureProducesInstances`

Evaluates the `parametric-bracket.scad` fixture and asserts more than three instances, and evaluates the
`eval-instance-tree.scad` fixture and asserts exactly twenty-one instances (groups included) and the single echo
line `ECHO: "instances built", 3`, confirming that a realistic parametric model evaluates end to end.

### Evaluation Options: Validated And Immutable

**Test**: `CanvasNetScad_SystemIntegration_EvaluationOptions_AreValidatedAndImmutable`

Asserts that `ScadEvaluationOptions` and `ScadLimits` reject a non-finite time, a null limits value, a variable
name that is not a valid name, an unsupported variable value, and a zero step limit when they are set; that the
supplied variables dictionary is copied (a later change to the caller's dictionary has no effect); and, by
reflection, that every writable property of the options is init-only.

### Diagnostics: Unknown Names Are Warnings

**Test**: `CanvasNetScad_SystemIntegration_Diagnostics_UnknownNamesAreWarnings`

Evaluates a program that uses an unknown variable, function, and module and then echoes a line, with a
warning handler, and asserts the three located warning texts (`warn.scad:1:6: Ignoring unknown variable …`,
`warn.scad:2:6: Ignoring unknown function …`, `warn.scad:3:1: Ignoring unknown module …`), that the handler
received exactly the diagnostics of the result, and that evaluation continued to the final echo.

### Unsupported Features: Reported Or Thrown

**Test**: `CanvasNetScad_SystemIntegration_UnsupportedFeatures_ReportedOrThrown`

Evaluates the `eval-unsupported.scad` fixture and asserts that the three recognized-but-unsupported functions are
reported (and delivered to the handler) in evaluation order, and that with `ThrowOnUnsupportedFeature` the first
one throws a `ScadUnsupportedFeatureException` (an `IOException`) carrying the feature identifier and line 7.

### Include And Use: Resolver And Sandbox

**Test**: `CanvasNetScad_SystemIntegration_IncludeUse_ResolverAndSandbox`

Evaluates a document that uses a library supplied by an in-memory resolver and asserts that the library's
function is usable (echo 42), that its top-level echo never runs, and that the resolver was asked only for the
normalized logical path `lib/util.scad`; evaluates a document that includes `../secret.scad` and asserts a located
`ScadEvaluationException` without any further resolver request (the hostile path never reaches the callback);
and evaluates the `eval-include-main.scad` fixture from disk, asserting that its include and use of files under
`libs/` work through the file system.

### Evaluation Limits: Fail Closed

**Test**: `CanvasNetScad_SystemIntegration_EvaluationLimits_FailClosed`

For each of the recursion, step, loop-iteration, list-length, string-length, instance, and output limits, evaluates
a small program with a tiny explicit limit and asserts a `ScadEvaluationException` whose message names the limit
and whose location is on a real line.

### Evaluation Limits: Deep Recursion On A Small Stack

**Test**: `CanvasNetScad_SystemIntegration_EvaluationLimits_DeepRecursionOnSmallStackDoesNotOverflow`

Evaluates a program that recurses 2,400 levels (accepted) and then 100,000 levels (over the default limit) on a
thread with a 256 KiB stack and on the normal test thread, and asserts that both fail with the identical message
`Recursion too deep (limit 5000)` and the identical location, and that the process did not crash.

### Cancellation: Stops Evaluation

**Test**: `CanvasNetScad_SystemIntegration_Cancellation_StopsEvaluation`

With the step limit raised to the maximum and a program in endless recursion, asserts that an already canceled
token and a token canceled after 150 ms each end the evaluation with a plain `OperationCanceledException` (not a
`ScadEvaluationException`) well inside a generous timeout.

### Variable Overrides: Replace Assignments

**Test**: `CanvasNetScad_SystemIntegration_VariableOverrides_ReplaceAssignments`

Evaluates the `eval-overrides.scad` fixture with defaults and with overrides of two assigned variables, the
special variable for the fragment count, and an absent variable given as a list, and asserts the default echo
and the overridden echo (the replaced variables, the dependent computation, the special variable, and the
created list).

### Determinism: Invariant Culture And Repeatable

**Test**: `CanvasNetScad_SystemIntegration_Determinism_InvariantCultureAndRepeatable`

Evaluates the `eval-number-formatting.scad` fixture twice and under the `de-DE`, `fr-FR`, and `tr-TR` current
cultures (on 1 MiB threads) and asserts that every echo list equals the baseline, including the pinned line of
formatted fractions (`0.1, 0.5, 1.5, -2.5, 0.333333, 0.666667, 3.14159, 12345.7`).

## Acceptance Criteria

A system-level test run passes when every scenario above passes without error or unexpected exception, on every
target framework. Every requirement in `docs/reqstream/canvas-net-scad.yaml` and
`docs/reqstream/canvas-net-scad/platform-requirements.yaml` links to at least one passing test listed above. Any
unexpected exception, wrong exception type (in particular a `ScadSyntaxException` where an
`InvalidDataException` is documented, or the reverse), wrong location, or wrong reported name constitutes a
failure.

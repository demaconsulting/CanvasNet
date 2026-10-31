## Expressions Subsystem Verification Design

This document describes the subsystem-level verification strategy for the `Expressions` subsystem (internal namespace
`DemaConsulting.CanvasNet.VegaLite.Expressions`), containing three units: `ExpressionParser` (together with the
`CompiledExpression`, construct, and reference types of its files), `ExpressionEvaluator` (together with its coercion
and datum files), and `ExpressionFunctions` (together with the function context and the regular-expression
translation). The subsystem implements the supported subset of the Vega expression language: the parser compiles the
text of an expression into an immutable postfix program, the evaluator runs the program for one row, one parameter
environment, or no datum at all, and the function table holds the 99 whitelisted functions (the math, text, type,
regular-expression, and date and time functions). It performs no input or
output, reads no clock (there is no `now`), and uses no random source; the date and time functions use the calendar of
the evaluation, never the host's time zone.

### Verification Approach

The `Expressions` subsystem is verified at three tiers. The unit tests (see _ExpressionParser Unit Verification
Design_, _ExpressionEvaluator Unit Verification Design_, and _ExpressionFunctions Unit Verification Design_ under
`expressions/`) verify each unit in isolation, including its internal types. The subsystem-level scenarios verify the
three units working together on the texts that the specification parser hands over: the parser compiles an expression
and collects the constructs it does not execute and the parameter names it reads; the evaluator runs the compiled
program against a table row, a value, or the parameter environment of a call and calls into the function table through
its function context; the function context charges size-proportional work to the same step budget as the instructions
and holds the limits and the regular-expression cache; and the results are the `DataValue` model of the `Data`
subsystem, which holds arrays, objects, and regular expressions beside the scalars (see the composite-value scenario of
the _Data Subsystem Verification Design_). The unit tests already use this combination, because the parser is the only
source of a compiled program and the evaluator is the only caller of the function table, so the subsystem-level
scenarios name the unit tests that cross the boundary and the system-level scenarios that exercise the subsystem
through the public API.

No mocking or stubbing is used: the subsystem depends only on `VegaLiteLimits`, `VegaLiteLocation`,
`VegaLiteSpecException`, `VegaLiteRenderException`, and the `DataValue`, `DataObject`, and `DataTable` types of the
`Data` subsystem. A test datum that implements the evaluator's `IExpressionDatum` is the only test double, and it is a
real implementation of the seam that cancels at a chosen point. Expected values are written out by hand from the
JavaScript and Vega expression rules and never produced by running a JavaScript engine, no assertion measures time,
and the assertions hold under every culture and time zone. The subsystem is internal, so its tests reach it through the
test assembly's `InternalsVisibleTo` access.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Files**: None for the unit scenarios; the system-level scenarios use in-memory specifications
- **Isolation**: Each test compiles its own texts and uses its own evaluator, context, and parameter dictionary. The
  scenarios that run the units in parallel share only an immutable compiled expression

### Acceptance Criteria

The `Expressions` subsystem's verification passes when every subsystem-level scenario below and every unit-level
scenario in the three unit verification documents passes without error or unexpected exception. Any value that differs
from the hand-derived JavaScript result, a step budget that is off by one, a hostile expression that does not fail
closed with the location of its text, or a result that differs between repeated, concurrent, or culture-dependent
runs constitutes a failure.

### Test Scenarios

- **Parse Then Evaluate: A Compiled Program Gives The JavaScript Result**:
  `ExpressionParserTests.ExpressionParser_Parse_PrecedenceAndAssociativity_MatchJavaScript`,
  `ExpressionParserTests.ExpressionParser_Parse_LongOperatorChain_IsNotRecursive`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_ArithmeticAndConcatenation_FollowJavaScript`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_LargeProgramsAndReusedStack_AreCorrect` — texts with mixed
  operators compile and then evaluate to the hand-computed values, a chain of 2,048 operands evaluates iteratively to
  2,048 without recursion proportional to its length, and one evaluator serves a program that needs a deep stack and
  then a shallow one.
- **Parameters And Datum Reach The Evaluation**:
  `ExpressionParserTests.ExpressionParser_Parse_ParameterReferences_AreCollectedOncePerName`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Parameters_AreLookedUpByName`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_TableRow_ReadsFlattenedFields`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_UnsupportedConstruct_IsNull` — the names that the parser
  collects (once per name, with the offset of the first use) are the names the caller checks against the declared
  parameters, the evaluator reads a parameter by its exact name and a row of a table by field path (including a nested
  object rebuilt on demand and an escaped dot), and what is not supported (unknown functions, view state, deferred
  operators) is null at evaluation while the parser still lists it.
- **Parse, Evaluate, And Call: The Function Table Behind The Evaluator**:
  `ExpressionFunctionsTests.ExpressionFunctions_Names_AreExactlyTheWhitelist`,
  `ExpressionFunctionsTests.ExpressionFunctions_Math_ExactValuesAndSpecialCases`,
  `ExpressionFunctionsTests.ExpressionFunctions_Text_FollowJavaScriptAndVega`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_SizeProportionalWork_IsCharged` — the calls of the 99 names
  compile like any other call, run through the evaluator, and give the hand-derived values, while a call of any other
  name is an unsupported function, and the work of a function over a long text is charged to the same budget as the
  instructions (the instructions plus one step per 64 characters).
- **Values Are Composites, Compared By Identity**:
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Composites_CompareByIdentity`,
  `ExpressionParserTests.ExpressionParser_Parse_ArrayAndObjectLiterals_BuildCompositeValues`,
  `DataCompositeValueTests.DataValue_Composites_AreEqualOnlyToThemselves` — array, object, and regular-expression
  values built by literals and functions are the values of the `Data` subsystem, are equal only to themselves, and keep
  their insertion order.
- **Hostile Expressions Fail Closed**:
  `ExpressionParserTests.ExpressionParser_Parse_LengthLimit_AcceptsUpToLimit`,
  `ExpressionParserTests.ExpressionParser_Parse_DepthLimit_AcceptsUpToLimit`,
  `ExpressionParserTests.ExpressionParser_Parse_MaximumConfiguredDepth_DoesNotOverflowTheStack`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_StepBudget_IsExact`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_StringLimit_IsEnforcedOnConcatenation`,
  `ExpressionFunctionsTests.ExpressionFunctions_Regex_CatastrophicPatternsAreSafe`,
  `ExpressionFunctionsTests.ExpressionFunctions_Regex_BacktrackingFallbackFailsClosed`,
  `ExpressionFunctionsTests.ExpressionFunctions_StringResults_AreBoundedByMaxStringLength`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Expressions_ErrorsAndLimitsFailWhenTheDocumentIsOpened`,
  `VegaLiteSystemIntegrationTests.CanvasNetVegaLite_SystemIntegration_Expressions_HostileEvaluationFailsClosed` — a text
  over 4,096 characters or 64 levels of nesting is refused when it is compiled (4,096 and 64 are accepted), the
  nesting limit cannot overflow the stack even at its ceiling of 256, a program passes with exactly its number of
  instructions as the budget and fails with one fewer, a string over the limit is refused at the operation that makes
  it, a catastrophic regular expression finishes through the linear-time engine, one that needs backtracking fails with
  a located error instead of running on, and the failures at the document level carry the pointer and the position of
  the expression text with the offset inside it.
- **Unsupported Constructs Are Reported, Not Executed**:
  `ExpressionParserTests.ExpressionParser_Parse_UnsupportedConstructs_AreCollectedInOrder`,
  `FeatureCatalogTests.FeatureCatalog_Visit_UnsupportedExpressionConstructsAreReportedAtTheExpression`,
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Diagnostics_ExpressionReportsAndStrictMode`,
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Deferred_FeaturesAreStillReported` — the constructs
  that the parser collects (`vl-expr-function-<name>`, `vl-expr-view-state`, `vl-expr-construct-<name>`) are the reports
  of the catalog at the string value of the expression, in document order, and strict mode stops at the first of them
  with nothing after it, while a lenient render evaluates them to null and still draws the chart.
- **Row-Bound Expression References Reach The Marks**:
  `MarkBuilderTests.MarkBuilder_Build_RowBoundExpressionReferencesAreEvaluatedPerRow`,
  `VegaLiteChartIntegrationTests.CanvasNetVegaLite_SystemIntegration_Expressions_RowBoundMarkValuesAreEvaluatedPerRow` —
  a mark property or a channel value that is an expression reference is evaluated against the transformed row of each
  mark, and the pixels have the colors, opacities, and sizes the expressions give.
- **Determinism And Culture Independence**:
  `ExpressionParserTests.ExpressionParser_Parse_RepeatedAndConcurrent_GivesIdenticalResults`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_UnderAnyCulture_IsInvariant`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_CultureHelper_IsolatesTheCultureOnAWorkerThread`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Concurrent_IsDeterministic`,
  `ExpressionFunctionsTests.ExpressionFunctions_Repeated_ResultsAreIdentical` — one compiled expression shared by
  threads, each with its own evaluator, gives eight hundred identical results, the results under eight cultures
  (set on dedicated worker threads, never on the test thread; including Turkish and Arabic) are those of the invariant
  culture, and a function that would need a clock or a random
  source is not in the table.
- **Date And Time Functions Use The Calendar Of The Evaluation**:
  `ExpressionFunctionsTimeTests.ExpressionFunctions_Datetime_Components_AreALocalDate`,
  `ExpressionFunctionsTimeTests.ExpressionFunctions_Getters_LocalFormsFollowTheZone_UtcFormsDoNot`,
  `ExpressionFunctionsTimeTests.ExpressionFunctions_Offset_MovesByCalendarUnits`,
  `ExpressionFunctionsTimeTests.ExpressionFunctions_Sequence_IsBoundedByTheScaleDomainLimit`,
  `ExpressionFunctionsTimeTests.ExpressionFunctions_TimeFormat_FormatsWithTheSpecifier`,
  `ExpressionFunctionsTimeTests.ExpressionFunctions_Time_AreCultureInvariant`,
  `ExpressionFunctionsTimeTests.ExpressionFunctions_Now_StaysUnsupported`,
  `DataLoaderCalendarTests.ExpressionEvaluator_Constructor_ForwardsTheCalendar`,
  `DataLoaderCalendarTests.DataLoader_Render_TimeZoneOptionReachesTheDataStage` — the calendar that the evaluator is
  given reaches the context and from it every local date function: `datetime(2024, 1, 29, 12, 34, 56, 789)` is
  1,709,210,096,789 ms (the hand derivation is in the _ExpressionFunctions Unit Verification Design_: 19,782 days x
  86,400 s + 45,296 s, then x 1,000 + 789), at +05:30 the instant 2024-02-29T20:00Z has the wall time 01:30 on March 1
  (day of year 61) while the UTC getters keep the 29th, a month offset of January 31 is March 2 in the leap year 2024,
  a local day across the daylight-saving change of 2024-03-10 is 23 hours, the formats and the names do not depend on
  the culture, and a sequence is bounded by `MaxScaleDomainSize` and the step budget; the zone of the render options
  reaches the evaluator of a whole document (a date-time without an offset moves five hours at +05:00). `now` is not
  in the table, so no function reads a clock.
- **Cancellation**:
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_CanceledBeforeStart_ThrowsOperationCanceled`,
  `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_CancellationBetweenCheckPoints_StopsNextEvaluation`,
  `ExpressionFunctionsTests.ExpressionFunctionContext_StepsAndCharges_AreExactAndCancelable` — a canceled token stops
  an evaluation at its start, at the next evaluation after a cancellation between check points, and at the first charge
  that crosses a multiple of 1,024 steps, always as an `OperationCanceledException`.

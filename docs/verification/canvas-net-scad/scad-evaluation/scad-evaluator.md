### ScadEvaluator Unit Verification Design

<!-- cspell:ignore undef unseeded evaluator hoisted lexically memoized -->

This document describes the unit-level verification strategy for the `ScadEvaluator` unit: the internal evaluator
(`ScadEvaluator` and its partial files), its scopes, the instantiation tree (`ScadInstance`), the recognized
built-in module names, the evaluation settings and output, and the buffering `ScadEvaluationSink`, in the
`DemaConsulting.CanvasNet.Scad.Evaluation` namespace.

#### Verification Approach

The unit is verified by parsing small OpenSCAD-language programs, evaluating them through `ScadEvaluator.Evaluate`
with explicit settings, and asserting on what the language semantics promise: most scenarios are **echo based** —
the program echoes the values of interest and the test asserts the echo lines, which pins values, number text, and
the order of effects at once — and the rest assert the exact warning text and `line:column` location, the exact
exception type, message, and location, or the shape of the instantiation tree. The tree is compared as text through
the iterative, location-free `ScadInstanceDumper` of `ScadEvaluatorTestSupport` (for example
`translate([1, 0, 0]){cube(5)}`, with groups shown as `group<for>{…}` and modifiers as leading characters), so that
a tree assertion is a string comparison and a 5,000-level tree can be dumped on a small stack; the helpers `Run`,
`EchoLines`, `Warnings`, `Fails`, and `Tree` wrap the entry point. The unit's collaborators — the value model, the
built-in functions, and the file resolver — are used for real, so no mocking is needed; the include and use
scenarios use the in-memory `ScadVirtualFiles` resolver, which records every path requested and the thread each
request came from.

Limits are verified in pairs **at the limit succeeds, one over fails** with small explicit limits so that the
tests are fast; the frame accounting is pinned by a formula test (`f(n) = n == 0 ? 0 : 1 + f(n - 1)` costs two
frames per level plus four, so a limit of 104 admits depth 50 and rejects 51), and the module, `children()`, and
block accounting by their own formulas. Stack safety is verified by running programs from caller threads with a
64 KiB (tiny), 256 KiB or 1 MiB (small), and 256 MiB (big) stack: the result at the limit must match the big-stack
result, the over-limit failure must have the identical message and location on every stack size, a tiny stack must
hop to the evaluator thread (observed through `LastRunThreadId` and `FallbackCount`), a normal program must stay on
the caller's thread, exceptions and the culture must survive the hop, a restart must record each event once and
keep the warnings recorded while files were loaded, and a burst of tiny-stack callers must not exceed the bound on
concurrent fallback threads. Cancellation is verified with tokens that are canceled beforehand, after a short
delay during endless tail recursion and a huge loop (with the step limit raised to the maximum), and by the
resolver during file loading, always with a generous timeout. Determinism is verified by evaluating twice, under the
`de-DE` and `tr-TR` cultures, concurrently, and after a forced restart.

The golden fixtures are evaluated by `ScadEvaluatorFixtureTests` (every authored fixture must complete without a
warning, except `include-and-use.scad`, whose paths intentionally do not exist) and, with their expectation files,
through the public API (see _ScadDocument Unit Verification Design_).

Unit tests reside in `ScadEvaluatorScopingTests.cs`, `ScadEvaluatorSpecialVariableTests.cs`,
`ScadEvaluatorFunctionTests.cs`, `ScadEvaluatorTailCallTests.cs`, `ScadEvaluatorModuleTests.cs`,
`ScadEvaluatorControlFlowTests.cs`, `ScadEvaluatorComprehensionTests.cs`, `ScadEvaluatorExpressionTests.cs`,
`ScadEvaluatorInstanceTreeTests.cs`, `ScadEvaluatorIncludeUseTests.cs`, `ScadEvaluatorOverrideTests.cs`,
`ScadEvaluatorDiagnosticsTests.cs`, `ScadEvaluatorLimitTests.cs`, `ScadEvaluatorProductLimitTests.cs`,
`ScadEvaluatorStackTests.cs`,
`ScadEvaluatorCancellationTests.cs`, `ScadEvaluatorDeterminismTests.cs`, `ScadEvaluatorEntryPointTests.cs`,
`ScadEvaluatorFixtureTests.cs`, and `ScadEvaluationSinkTests.cs` within the `DemaConsulting.CanvasNet.Scad.Tests`
project, in the `Evaluation` folder, with the shared helpers in `ScadEvaluatorTestSupport.cs` and
`ScadEvaluationTestHelpers.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the in-memory `ScadVirtualFiles` resolver is a real implementation of the callback
- **Files**: The authored fixtures in `ScadFixtures/` (read only) for the fixture scenarios
- **Threads**: The stack-safety scenarios run the evaluation from a caller thread with a 64 KiB, 256 KiB, 1 MiB, or
  256 MiB stack and join it before asserting; the cancellation scenarios cancel from a timer or from the resolver;
  the concurrency scenarios start several threads at once
- **Cultures**: The determinism scenarios set the current culture for the duration of the call and restore it
- **Isolation**: Each test evaluates its own sources and settings

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong echo text, warning text or location, exception type or message, tree shape, thread observation,
or missing or unexpected limit behavior constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Scoping: Scopes And Hoisting

**Tests**: `ScadEvaluatorScopingTests.ScadEvaluatorScoping_Reassignment_LastWinsAndWarnsAtEarlierAssignment`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_ForwardReference_SeesLaterAssignment`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_Block_ShadowsWithoutLeaking`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_Definitions_AreHoisted`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_VariableAndFunctionOfSameName_Coexist`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_Function_UsesDefinitionScopeNotCallerScope`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_DuplicateDefinitions_LastWinsWithWarnings`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_NestedDefinitions_AreLocalToTheirBody`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_Pi_IsDefinedAndCanBeShadowed`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_SameNameInDifferentScopes_DoesNotWarn`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_IfBranch_HasOwnScope`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_Let_BindsSequentially`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_LoopBody_HasScopePerIteration`

Asserts that the last assignment in a scope wins and the warning is located at the earlier assignment, that a
forward reference sees a later assignment, that a block shadows without leaking, that definitions are visible before
they are written, that a variable and a function of one name coexist, that a function runs in its definition scope,
that duplicate functions and modules are reported, that nested definitions are local, that `PI` is defined and can be
shadowed, that the same name in different scopes does not warn, that an `if` branch and a loop body have their own
scopes, and that `let` bindings are sequential.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-LazyCells: Lazy, Memoized Variables

**Tests**: `ScadEvaluatorScopingTests.ScadEvaluatorScoping_Cells_AreForcedInSourceOrderWithDependenciesFirst`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_Cell_IsEvaluatedOnce`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_SelfReference_IsUndefWithWarning`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_MutualReference_IsUndefWithWarning`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_FileVariable_DefinedLater_IsReadable`

Asserts that the effects of the variables of a scope occur in source order with dependencies forced first, that a
cell with an echo in its expression runs exactly once, that a self-referencing and a mutually referencing variable
give `undef` and a circular-reference warning, and that a function reads variables defined after it.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-SpecialVariables: Dynamic Scope

**Tests**: `ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_Defaults_AreDocumentedValues`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_Time_IsTakenFromSettings`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_SetInCaller_IsVisibleThroughNestedCalls`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_BoundByLet_IsVisibleInCalledFunction`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_NamedArgumentWithoutParameter_BindsDynamically`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_Parameter_ShadowsOuterBinding`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_TailRecursionWithoutSpecials_DoesNotGrowChain`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_TailRecursionBindingSpecial_IsBoundedByRecursionLimit`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ChildListAssignment_ReachesModulesCalledByChildren`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ChildListAssignment_InRecursion_IsBoundedByRecursionLimit`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_For_SpecialLoopVariable_IsDynamicForBody`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Use_LibraryModulesSeeCallersSpecialVariables`

Asserts the defaults of the fragment variables, the time, and the preview flag, that the time comes from the
settings, that a binding made by a caller is visible through nested module and function calls, that a `let`
binding is visible in a called function, that a named `$` argument binds even without a parameter, that a parameter
shadows an outer binding, that a long tail recursion that binds no `$` name does not grow the chain, that one that
binds one at every level fails with the recursion error, that a `$` loop variable is dynamic for the body, that
a library module sees its caller's special variables, that a `$` variable assigned in a child list reaches the
modules and functions its children call, and that the frames child lists add are bounded by the recursion limit
(nesting a module a thousand levels deep ends in `Recursion too deep (limit 16)`, not a stack error).

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Functions: User Functions

**Tests**: `ScadEvaluatorFunctionTests.ScadEvaluatorFunction_Recursion_ComputesExpectedResult`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_ArgumentBinding_FollowsDocumentedRules`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_Default_SeesOnlyEarlierParameters`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_ExtraAndUnknownArguments_WarnAndAreIgnored`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_NamedArgument_OverridesPositional`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_BodyForms_LetEchoAssert_Compose`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_UserDefinitions_ShadowBuiltins`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_FunctionValue_PrintsParameterNames`

Asserts recursion and mutual recursion results, positional, named, and default binding (a default sees only
earlier parameters), the warning for surplus and unknown arguments, bodies that combine `let`, `echo`, and
`assert`, that a user function shadows a built-in, and the text of a function value.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Closures: Function Values

**Tests**: `ScadEvaluatorFunctionTests.ScadEvaluatorFunction_Closures_BehaveLexically`,
`ScadEvaluatorComprehensionTests.ScadEvaluatorComprehension_CStyleFor_ClosuresKeepTheirTurnsValues`

Asserts that function literals capture their defining scope, can be passed and returned and called through an
expression, and that closures created in the turns of a C-style comprehension keep the values of their own turn.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-TailCalls: Tail Recursion

**Tests**: `ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_DeepTailRecursion_SucceedsAtDefaultLimits`,
`ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_TailRecursion_UsesConstantFrames`,
`ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_TailPositions_AddNoFrames`,
`ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_MutualAndClosureRecursion_AddNoFrames`,
`ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_CallAsOperand_IsNotATailCall`,
`ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_InfiniteTailRecursion_EndsWithStepLimit`

Asserts that a 100,000-level tail recursion succeeds at the default limits with constant frames, that tail
positions through the conditional, `let`, `echo`, and `assert` add no frames (also for mutual and closure
recursion), that a call used as an operand is not a tail call, and that an infinite tail recursion ends with the
step-limit error and not a stack error.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Modules: User Modules

**Tests**: `ScadEvaluatorModuleTests.ScadEvaluatorModule_Call_ProducesNamedGroupOfBodyInstances`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_EmptyBody_ProducesEmptyGroup`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Recursion_NestsGroups`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_UserModule_ShadowsBuiltin`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_ArgumentBinding_FollowsFunctionRules`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_NestedDefinition_IsLocal`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Body_EchoLetAssertStatements`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Body_AssertFailure_Aborts`

Asserts the named group of a module call, the empty group for an empty body, recursion that nests groups, that a
user module shadows a built-in, the same binding rules as functions, local nested definitions, the statements of a
body, and that a failed assertion aborts.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Children: The children Pseudo-Module

**Tests**: `ScadEvaluatorModuleTests.ScadEvaluatorModule_Children_AllInOrder`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Children_Selection_PicksSlots`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Children_InvalidIndex_WarnsAndPlacesNothing`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Children_OutsideModule_WarnsAndPlacesNothing`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Children_SlotCounting`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Children_SeeAssignmentsOfTheirList`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Children_AreEvaluatedAtTheCallSite`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Children_ForwardedThroughNestedModule`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_Children_CountsSlots`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_SetBeforeChildren_IsVisibleToChildren`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ChildListAssignment_IsVisibleToChildren`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ChildListAssignment_ShadowsModuleBinding`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ChildListAssignment_DependsOnCallSiteSpecials`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ChildListAssignment_IsNotVisibleAtTheCallSite`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ChildListAssignment_ReachesChildrenForwardedThroughNestedModule`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ChildListAssignment_SelectedChild_SeesIt`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_ModuleBinding_WithoutChildListSpecials_StillReachesChildren`

Asserts all children in order, selection by index, vector, and range, the warnings for an invalid index and for a
use outside a module, slot counting (nested blocks flattened; a `for`, an `if`, and a disabled child one slot each),
that child assignments are visible to the slots, that children are evaluated at the call site, that children
forward through a nested module, the child-count special variable, and that special variables set before
`children()` are visible to the children. For special variables assigned in the child list itself, it asserts that
`module m() { children(); }` with `m() { $x = 4; echo($x); }` prints `ECHO: 4` without a warning, that the list's
binding shadows the module's while the module body keeps its own, that a list binding that depends on another
special variable reads the call site's value even when the module rebinds it, that the binding is not visible in
the module body or at the call site, that it reaches children forwarded through a nested module and a child selected
by index, and that a child list assigning only ordinary variables leaves the module's bindings visible.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-ControlFlow: Conditionals And Loops

**Tests**: `ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_If_SelectsBranchByTruthiness`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_For_IteratesExpectedValues`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_For_MultipleIterators_FirstIsOutermost`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_For_LaterIteratorSeesEarlierOne`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_For_ProducesGroupEvenWhenEmpty`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_IntersectionFor_ProducesIntersectionOfGroups`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_LetStatement_ScopesBindingsAndSplices`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_EchoAndAssertStatements_PlaceTheirChildren`

Asserts branch selection by truthiness, the iterated values for vectors, ranges, and single values, the order of
several iterators, a group even for an empty loop, the intersection shape of `intersection_for`, the scope and
splicing of a `let` statement, and the placement of the children of `echo` and `assert` statements.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Ranges: Range Iteration

**Tests**: `ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_For_IteratesExpectedValues`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_For_UndefOrInvalidRange_WarnsWithoutIterating`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_For_ChargesLoopIterations`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_RangeWithNonNumber_IsUndefWithWarning`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_LoopIterations_HugeRange_FailsImmediately`

Asserts ascending, descending, and fractional ranges, no iteration without error for a contradictory range,
a warning and no iteration for a zero step, a non-finite bound, or `undef`, the loop-iteration charge per turn,
`undef` with a warning for a non-numeric part, and that a huge range fails at once with the loop-iteration error.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Comprehensions: List Comprehensions

**Tests**: `ScadEvaluatorComprehensionTests.ScadEvaluatorComprehension_Forms_ProduceExpectedVector`,
`ScadEvaluatorComprehensionTests.ScadEvaluatorComprehension_CStyleFor_UpdatesStateSimultaneously`,
`ScadEvaluatorComprehensionTests.ScadEvaluatorComprehension_ForOverUndef_WarnsAndYieldsNothing`,
`ScadEvaluatorComprehensionTests.ScadEvaluatorComprehension_InsideRecursion_Works`,
`ScadEvaluatorComprehensionTests.ScadEvaluatorComprehension_ListLimit_AtLimitSucceeds_OneOverFails`,
`ScadEvaluatorComprehensionTests.ScadEvaluatorComprehension_EachOverHugeRange_IsRejectedUpFront`

Asserts every comprehension form (`for`, `if`/`else`, `let`, `each`, nested and parenthesized), the simultaneous
update of the C-style form, the warning for a comprehension over `undef`, use inside a recursive function, the list
limit at and over the limit, and the up-front rejection of an `each` over a huge range.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Operators: Expressions

**Tests**: `ScadEvaluatorExpressionTests.ScadEvaluatorExpression_Scalars_ProduceExpectedNumber`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_DivisionByZero_FollowsIeeeWithoutWarning`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_VectorAndMatrixArithmetic_ProducesExpectedValue`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_MismatchedVectors_YieldUndefWithWarning`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_IndexingAndMembers_ReturnExpectedValue`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_ComparisonAndLogic_ProduceExpectedValue`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_LogicalOperators_ShortCircuit`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_InvalidOperands_YieldUndefAndWarn`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_InvalidOperands_WarningNamesOperatorAndKinds`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_ParenthesizedLetAsOperand_Evaluates`

Asserts scalar results and precedence, IEEE division by zero without a warning, vector and matrix arithmetic,
the warning and `undef` for mismatched vectors and invalid operands (with the exact text naming the operator and
the kinds and the location of the operator), indexing and members, comparison and logic, short-circuit evaluation
(observed through the echo of a side effect), and a parenthesized `let` as an operand.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Echo: Echo Output

**Tests**: `ScadEvaluatorExpressionTests.ScadEvaluatorExpression_EchoForms_AreStable`,
`ScadEvaluatorExpressionTests.ScadEvaluatorExpression_Echo_NamedAndPositionalArguments_AreJoined`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Echo_BuffersLinesInOrder`

Asserts the stable echo text of strings with escapes, vectors, nested strings, ranges, and functions, the joining
of named and positional arguments with commas, and that the sink buffers lines in order.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Assert: Assertions

**Tests**: `ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_AssertStatement_FailureAbortsWithMessage`,
`ScadEvaluatorControlFlowTests.ScadEvaluatorControlFlow_AssertStatement_PassingHasNoEffect`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_AssertExpression_Fails_WithMessage`,
`ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_AssertFailure_HasLocationAndMessage`

Asserts that a failed assertion aborts with the message and the location of the assertion (with and without a
message, and as a statement and as an expression), and that a passing assertion has no effect.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Imports: Include And Use Semantics

**Tests**: `ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Include_BehavesAsTextualInclusion`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Include_LaterAssignmentOfIncludingFileWins_WarnsInIncludedFile`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Use_ImportsOnlyDefinitions`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Use_LibraryFunctionsSeeLibraryAssignments`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Use_LibrariesOwnUsesArePrivate`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Use_PrecedenceLocalThenLaterUse`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Use_MutuallyUsingLibraries_Work`

Asserts that included statements behave as if written in place (an assignment of the including file later in the
file wins and the warning names the included file), that a used library contributes only definitions and its
top-level echo never runs, that a library's functions see the library's variables, that a library's own uses are
private but callable from its functions, that a local definition beats an imported one and a later `use` beats an
earlier one, and that mutually using libraries work.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-InstanceTree: Tree Shape

**Tests**: `ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_BuiltinModules_CarryEvaluatedArgumentsAndChildren`,
`ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_RecognizedBuiltinName_ProducesBuiltinInstance`,
`ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_SplicingConstructs_HaveNoNodeOfTheirOwn`,
`ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_LookupOrder_UserBeforeBuiltinBeforeUnknown`,
`ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_EachStatement_IsAnUnknownModule`,
`ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_Location_IsTheModuleNameToken`,
`ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_InstanceCount_CountsEveryNode`,
`ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_TwoEvaluations_ProduceEqualDumps`,
`ScadEvaluatorModuleTests.ScadEvaluatorModule_Unknown_WarnsAndSkipsChildren`

Asserts golden dumps for built-in nodes with evaluated, named arguments and children, the group shapes of user
modules and loops, splicing constructs without nodes, the lookup order of user, built-in, and unknown modules
(an unknown module warns, emits nothing, and does not evaluate its children), that an `each` statement is an
unknown module, the location of a node (its name token), that the instance count counts every node, and that two
evaluations give equal dumps.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Modifiers: Modifier Characters

**Tests**: `ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_Modifiers_AreMergedOntoInstances`,
`ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_DisableModifier_IsNeverEvaluated`

Asserts that the show-only, highlight, and background flags are merged onto the single instance a statement
produces (or onto a wrapping group for several), and that a disabled statement produces nothing and is never
evaluated (a counted-step comparison shows that it uses no budget and its echo does not run).

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-FragmentSettings: Fragment Variables

**Tests**: `ScadEvaluatorInstanceTreeTests.ScadEvaluatorInstanceTree_Fragments_DefaultInheritAndOverride`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_Fragments_FollowDynamicBindingAndArgumentOverride`

Asserts the default fragment settings, inheritance through the dynamic scope, and the override by a named
argument on one instance only.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Overrides: Variable Overrides

**Tests**: `ScadEvaluatorOverrideTests.ScadEvaluatorOverride_ReplacesAssignment_WithoutEvaluatingIt`,
`ScadEvaluatorOverrideTests.ScadEvaluatorOverride_AbsentVariable_IsCreated`,
`ScadEvaluatorOverrideTests.ScadEvaluatorOverride_RepeatedAssignments_AreAllReplaced`,
`ScadEvaluatorOverrideTests.ScadEvaluatorOverride_Scope_AppliesToTopLevelOnly`,
`ScadEvaluatorOverrideTests.ScadEvaluatorOverride_VectorValue_IsUsableAsVector`,
`ScadEvaluatorOverrideTests.ScadEvaluatorOverride_Order_DoesNotMatter`,
`ScadEvaluatorOverrideTests.ScadEvaluatorOverride_DependentAssignments_SeeOverriddenValue`,
`ScadEvaluatorSpecialVariableTests.ScadEvaluatorSpecialVariable_OverrideOfSpecial_ReplacesDefault`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Overrides_ReplaceIncludedAssignmentsButNotLibraries`

Asserts that an override replaces an assignment without evaluating its expression (an echo in it does not fire),
creates an absent variable, replaces every assignment of the name, applies to the top level only, can be a vector,
gives the same result in any order, flows into dependent assignments, replaces a special variable's default, and
applies to included assignments but not to libraries.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Warnings: Located, De-Duplicated Warnings

**Tests**: `ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_Warning_HasExpectedTextAndLocation`,
`ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_RepeatedWarningAtOneLocation_IsReportedOnce`,
`ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_SameProblemAtTwoLocations_IsReportedTwice`,
`ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_Location_CarriesTheSourceName`,
`ScadEvaluatorScopingTests.ScadEvaluatorScoping_UnknownVariable_IsUndefWithWarning`,
`ScadEvaluatorFunctionTests.ScadEvaluatorFunction_CallingNonFunction_WarnsAndYieldsUndef`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Warn_DuplicatesAreDropped`

Asserts the exact text and location of each warning kind, that a warning in a loop is reported once, that the same
problem at two locations is reported twice, that locations carry the source name, the unknown-variable and
non-function-call warnings, and the sink's de-duplication.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-DiagnosticCap: Bounded Warnings

**Tests**: `ScadEvaluatorLimitTests.ScadEvaluatorLimit_Diagnostics_AreCappedWithNotice`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Warn_BeyondCap_CollapsesIntoOneNotice`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Warn_ExactlyAtCap_HasNoNotice`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Warn_ManyDistinctWarnings_StaysBounded`

Asserts that warnings beyond the cap collapse into one notice located at the first dropped warning, that exactly
the cap produces no notice, and that a very large number of distinct warnings stays bounded.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-UnsupportedFeatures: Unsupported-Feature Reports

**Tests**: `ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_UnsupportedFunction_IsReportedAndEvaluationContinues`,
`ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_UnsupportedFeature_IsDeduplicatedByLocation`,
`ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_ThrowOnUnsupportedFeature_AbortsWithFeatureAndLocation`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Unsupported_RecordsAndDeduplicates`

Asserts the report and continued evaluation, de-duplication by feature and location, the abort with the feature
and location when the caller asked for it, and the sink's recording.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-ErrorLocations: Located Errors

**Tests**: `ScadEvaluatorDiagnosticsTests.ScadEvaluatorDiagnostics_LimitError_IsLocatedAtOffendingConstruct`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_FailedEvaluation_KeepsEventsRecordedBeforeTheFailure`

Asserts that a limit error is located at the offending construct and that the sink holds the echo and warnings
recorded before a failed evaluation.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-RecursionLimit: Deterministic Recursion Limit

**Tests**: `ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_NonTailRecursion_CostsTwoFramesPerLevel`,
`ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_NonTailRecursion_AtDefaultLimit_Succeeds_OneOverFails`,
`ScadEvaluatorTailCallTests.ScadEvaluatorTailCall_InfiniteNonTailRecursion_EndsWithRecursionLimit`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_RecursionDepth_ModuleRecursion_AtLimitSucceeds_OneOverFails`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_OverLimit_IsIdenticalOnEveryStackSize`

Asserts the pinned frame cost per level of function and module recursion, that recursion at the exact default
limit succeeds and one level more fails, that infinite non-tail recursion ends with the recursion error, and that
the over-limit error is identical on every stack size.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-StepLimit: Step Accounting

**Tests**: `ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_AtLimitSucceeds_OneOverFails`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_VectorArithmetic_IsChargedPerElement`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_SharedNestedVectors_AreChargedDeeply`,
`ScadEvaluatorAggregateLimitTests.ScadEvaluatorAggregateLimit_SharedNestedVectors_DefaultLimits_FailsWithStepLimit`,
`ScadEvaluatorAggregateLimitTests.ScadEvaluatorAggregateLimit_SharedNestedVectors_UnlimitedSteps_FailsWithAggregateLimit`,
`ScadEvaluatorAggregateLimitTests.ScadEvaluatorAggregateLimit_SharedNestedVectors_BudgetNearCap_ChoosesTheSmallerLimit`,
`ScadEvaluatorAggregateLimitTests.ScadEvaluatorAggregateLimit_NestedOperand_WithinCap_IsChargedByElementCount`,
`ScadEvaluatorElementCounterTests.ScadEvaluatorElementCounter_Count_FlatVectors_CountTheirLength`,
`ScadEvaluatorElementCounterTests.ScadEvaluatorElementCounter_Count_NestedVectors_AddsNestedElements`,
`ScadEvaluatorElementCounterTests.ScadEvaluatorElementCounter_Count_AboveCap_StopsEarlyWithValueAboveCap`,
`ScadEvaluatorElementCounterTests.ScadEvaluatorElementCounter_Count_SharedLevels_SaturatesAndUsesTheMemo`,
`ScadEvaluatorElementCounterTests.ScadEvaluatorElementCounter_Count_CanceledToken_ThrowsOnlyOnceTheCountIsLong`,
`ScadEvaluatorElementCounterTests.ScadEvaluatorElementCounter_Count_ListOfSmallVectors_DoesNotUseTheMemo`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_LongScopeWalk_IsCharged`,
`ScadEvaluatorProductLimitTests.ScadEvaluatorProductLimit_MatrixTimesMatrix_WorkIsChargedUpFront`,
`ScadEvaluatorProductLimitTests.ScadEvaluatorProductLimit_MatrixTimesMatrix_DefaultLimits_LargeProductFailsFast`,
`ScadEvaluatorProductLimitTests.ScadEvaluatorProductLimit_MatrixTimesVector_StillComputesAndCharges`,
`ScadEvaluatorProductLimitTests.ScadEvaluatorProductLimit_VectorTimesScalar_ChargedBeforeAllocation`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_StrOfLargeVector_IsChargedPerElement`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_EchoOfLargeVector_IsChargedPerElement`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_StrInLoop_HitsStepLimit`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_StringIndexing_IsChargedByPosition`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_StringIndexNearStart_ChargesOneStep`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_Steps_StringIndexingInLoop_HitsStepLimit`

Asserts the exact step limit, that vector arithmetic is charged per element, that a nested vector shared many times
is charged by its full contents, and that a long scope walk is charged. The product scenarios assert that a matrix
product is charged at least `n³ / 16` steps beyond its operands and that a smaller budget fails at the operator; that
two 1000 × 1000 matrices (about the cost that ran for minutes before the work was charged; built from one shared
row, so the setup costs a thousand steps and the test does not depend on how busy the machine is) fail under the
default limits in well under a second of work; that matrix-by-vector, vector-by-matrix, and dot products keep their results;
and that scaling a vector is charged before it is allocated. The formatting scenarios assert that `str` and `echo` of a
2,000-number vector are charged at least eight steps per element, and that a 100,000-turn loop that formats such a
vector fails at a one-million-step budget. The string-index scenarios assert the exact extra steps for an index
99,000 characters into a 100,000-character string (a scan charge for a plain string, a scan plus a walk charge for
one that starts with a surrogate pair), that an index below 512 costs nothing extra, and that a loop indexing a
million-character string near its end fails at a five-million-step budget (asserted by the error, not by a wall-clock
bound, because a time bound is flaky when other test assemblies keep the machine busy). The aggregate scenarios
build seven levels of vectors that each hold a thousand references to the level below (every list is short, the
top one stands for about 10^21 logical elements) and apply `-`, `+`, `*`, `/`, `==`, and `<` to it: under the
default limits each fails with the ordinary step-limit error, and with `MaxEvaluationSteps = long.MaxValue` each
fails with `Operand too large (limit 67108864 nested elements)` at the operator, both within seconds (the test
runs the evaluation on a pool thread and fails if it is still running after 20 s, so a runaway count cannot hang
the run); a budget of one billion steps (above the cap) gets the aggregate error and one million (below it) the
step error; and an operand of 2,001,000 logical elements is still accepted and charged by its count. The counter
tests pin the count itself: exact counts for flat and nested vectors, early stop above the cap, saturation at
`long.MaxValue` for the seven shared levels (counted at once because the counts of shared sub-vectors are
remembered), a cancellation check only once 65,536 elements were counted, and no memo for a list of small
vectors.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-LoopLimit: Loop Iterations

**Tests**: `ScadEvaluatorLimitTests.ScadEvaluatorLimit_LoopIterations_AtLimitSucceeds_OneOverFails`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_LoopIterations_AreCountedAcrossNestedLoops`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_LoopIterations_HugeRange_FailsImmediately`

Asserts the exact loop limit, that nested loops are counted together, and that a huge range fails before the first
iteration.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-ListAndStringLimits: Value Size

**Tests**: `ScadEvaluatorLimitTests.ScadEvaluatorLimit_ListLength_AtLimitSucceeds_OneOverFails`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_StringLength_AtLimitSucceeds_OneOverFails`,
`ScadEvaluatorLimitTests.ScadEvaluatorLimit_ListNesting_AtLimitSucceeds_OneOverFails`,
`ScadEvaluatorComprehensionTests.ScadEvaluatorComprehension_ListLimit_AtLimitSucceeds_OneOverFails`,
`ScadEvaluatorProductLimitTests.ScadEvaluatorProductLimit_MatrixTimesMatrix_ResultAtListLimit_Succeeds_OneOverFails`,
`ScadEvaluatorProductLimitTests.ScadEvaluatorProductLimit_MatrixTimesMatrix_HugeResult_FailsBeforeAllocating`

Asserts the list-length, string-length, and 256-level nesting limits at the limit and one over, including inside a
comprehension; that a matrix product whose result has exactly the list-length limit's elements succeeds and one more
element fails with `List too long` located at the `*` operator; and that a product whose result would have four
million elements fails quickly without allocating anything near that size (the allocation is measured on the
calling thread, so it is asserted only when the evaluation ran inline, as `LastRunThreadId` shows; the value-level
product tests measure the same property deterministically).

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-InstanceLimit: Instance Count

**Tests**: `ScadEvaluatorLimitTests.ScadEvaluatorLimit_Instances_AtLimitSucceeds_OneOverFails`

Asserts that a tree with exactly the limit of instances (groups included) is accepted and one more fails.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-OutputLimit: Echo Output

**Tests**: `ScadEvaluatorLimitTests.ScadEvaluatorLimit_OutputLength_AtLimitSucceeds_OneOverFails`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Echo_AtLimit_Succeeds_OneOver_Throws`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Echo_SingleLineOverLimit_Throws`

Asserts the cumulative output limit (counting the characters after the prefix) at the limit and one over, and that a
single over-long line fails.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Cancellation: Cancellation

**Tests**: `ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_AlreadyCanceled_ThrowsBeforeAnyResolverCall`,
`ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_DuringEndlessTailRecursion_StopsAndKeepsEarlierEvents`,
`ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_DuringHugeLoop_Stops`,
`ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_DuringIncludeLoading_StopsBeforeNextFile`,
`ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_NotCanceled_EvaluatesNormally`,
`ScadEvaluatorProductLimitTests.ScadEvaluatorProductLimit_MatrixTimesMatrix_DuringProduct_CanBeCanceled`,
`ScadEvaluatorCancellationTests.ScadEvaluatorCancellation_DuringLargeFormatting_Stops`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_CancelWhileWaitingForFallbackSlot_ThrowsOperationCanceled`

Asserts the cancellation behavior described in the requirement, always a plain `OperationCanceledException`, that
the earlier events are kept, and that an uncanceled token changes nothing. The formatting scenario cancels from a
timer while `str` repeatedly formats a vector of 100,000 numbers with the step limit raised to the maximum. The
product scenario cancels from a timer while 21 products of 27 million multiply-adds each are running; the
deterministic proof that a single product polls at fixed intervals is the `ScadValueProduct` poll scenario in the
unit verification of `ScadValue`. The wait scenario holds every slot of a slot set of its own (so nothing else can
count against it), starts a tiny-stack caller that must hop, waits until that set shows exactly one waiter, cancels,
and asserts that the caller ends with an `OperationCanceledException` within 30 seconds while the held slots are
untouched.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-StackSafety: Restart On A Large Stack

**Tests**: `ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_AtLimit_OnSmallCallerStack_MatchesBigStackResult`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_OverLimit_OnSmallCallerStack_ThrowsSameRecursionError`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_ComprehensionNesting_AtAndOverLimit_OnSmallCallerStack`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_ValueNestingAtLimit_OnSmallCallerStack_MatchesBigStack`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_TinyCallerStack_FallsBackToDedicatedThread`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_NormalProgram_RunsOnCallerThread`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_OnTinyCallerStack_PreservesExceptionsAndCulture`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_ConcurrentTinyCallerStacks_BoundFallbackThreads`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_WaitingForSlot_ProceedsWhenSlotIsReleased`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_CanceledWait_DoesNotLeakASlot`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_InjectedSlots_ObserveOnlyTheirOwnCallers`

Asserts, over the shapes of function, closure, mutual, module, and `children()` recursion, `let`, `echo`, and
`assert` chains, ternary chains, nested expressions and vector literals inside recursion, and comprehensions, that
programs at the limit match the big-stack result on small stacks, that one frame over fails identically, that
value nesting at 256 levels survives, that a tiny stack hops to the evaluator thread while a normal program does
not, that exceptions and the culture survive the hop, and that concurrent tiny-stack callers never exceed the bound
on fallback threads. The slot scenarios do not touch the process-wide slot set: each creates its own
`ScadEvaluatorFallbackSlots` (two slots, with its own waiting, active, peak, and start counters), takes every slot
on the test thread (with a 30-second timeout on every wait and release in `finally`), starts a tiny-stack caller
whose thread has the set injected through the per-thread `ScadEvaluator.SlotsForCurrentThread` seam, waits until
that set's own `Waiting` counter shows exactly one waiter, and then releases one slot (the caller completes and
the set's start count rises by one) or cancels (see the Cancellation scenario); afterwards every slot of the set
is free again. Because nothing else can acquire, wait on, or count against the private set, evaluations that other
tests run in parallel cannot satisfy or disturb the waits (a process-wide waiter counter could be satisfied by
another test's fallback hop). A further scenario holds two independent sets, waits one caller on each, and asserts
that each set counts exactly its own waiter. The concurrent-fallback scenario still uses the process-wide set and
asserts its peak.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-BufferedDelivery: Invisible Restart

**Tests**: `ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_Restart_RecordsEachEventOnce`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_Restart_KeepsFileLoadingWarnings`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_Restart_DoesNotRepeatOrMoveResolverCalls`,
`ScadEvaluatorIncludeUseTests.ScadEvaluatorIncludeUse_Resolver_RunsOnCallingThreadBeforeEvaluation`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Reset_ClearsEverything`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Deliver_KeepsChronologicalOrder`,
`ScadEvaluationSinkTests.ScadEvaluationSink_Deliver_NullHandlersSkipped_HandlerExceptionPropagates`

Asserts that after a restart each echo line and warning is recorded once, the warnings recorded while files were
loaded are kept, the resolver callback is neither repeated nor moved to another thread, the resolver runs on the
calling thread before any evaluation output, and the sink resets completely and delivers chronologically, skipping
null handlers and propagating a handler exception.

##### CanvasNetScad-ScadEvaluation-ScadEvaluator-Determinism: Repeatable Results

**Tests**: `ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_SameDocumentTwice_GivesIdenticalResults`,
`ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_UnderOtherCultures_GivesIdenticalResults`,
`ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_Rands_AreReproduciblePerEvaluation`,
`ScadEvaluatorDeterminismTests.ScadEvaluatorDeterminism_ConcurrentEvaluations_GiveIdenticalResults`,
`ScadEvaluatorStackTests.ScadEvaluatorStack_Evaluate_Restart_RandsWithoutSeedAreReproducible`,
`ScadEvaluatorCultureTests.ScadEvaluatorCulture_LimitMessages_AreIdenticalInEveryCulture`,
`ScadEvaluatorCultureTests.ScadEvaluatorCulture_ParserAndLimitsMessages_AreIdenticalInEveryCulture`

Asserts identical echo, diagnostics, and tree dumps for the same program evaluated twice, under other cultures,
concurrently, with unseeded random numbers, and after a forced restart. The culture scenarios produce the text of
each resource-limit error (recursion, steps, loops, list, string, instances, output, nesting, the matrix-product list
size, include depth, included files, and included statements) and of the parser nesting, lexer escape, source size,
limits-range, built-in argument-count, and suppressed-diagnostics messages under the invariant culture, five named
cultures (`de-DE`, `fr-FR`, `tr-TR`, `ar-SA`, `sv-SE`), and a synthetic culture (another negative sign, Arabic-Indic
native digits with native digit substitution), and assert that the text is identical in all of them and contains
the documented message. Honest scope: for non-negative integers .NET output does not vary between cultures, so the
scenarios are a regression guard; the invariance itself is guaranteed by construction (the invariant-culture
formatting of every numeric message, audited site by site).

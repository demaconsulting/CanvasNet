### ExpressionEvaluator Unit Verification Design

This document describes the unit-level verification strategy for the `ExpressionEvaluator` unit in the
`DemaConsulting.CanvasNet.VegaLite.Expressions` namespace: the internal `ExpressionEvaluator` class (its constructor
with the limits, the cancellation token, and the parameter values of one render call; the four `Evaluate` overloads
without a datum, over a `DataTable` row, over a `DataValue` datum, and over a caller-defined `IExpressionDatum`; and
`StepsUsed`), its coercion and datum files (the JavaScript conversions `IsTruthy`, `ToNumber`, `ToJsString`,
`StrictEquals`, `LooseEquals`, and `NumbersEqual`), and the supporting types `IExpressionDatum` and
`ExpressionRuntimeException`. The evaluator runs a compiled postfix program iteratively on a value stack with the
semantics of JavaScript (and of the Vega expression documentation): coercing arithmetic, equality and relational
comparison, short-circuit logical operators that return an operand, member access that is null-safe, a step budget
that is exact, and checks of the string limit and of the cancellation token.

#### Verification Approach

The unit is verified by compiling small, original expression texts with the real `ExpressionParser` (the compiled
program is the evaluator's only input, so the parser is a real collaborator and not a mock), evaluating them with an
evaluator that has chosen limits, parameters, and datum, and asserting on the resulting `DataValue`, the steps used,
and the exceptions. No mocking or stubbing is used; a small test datum (`CallbackDatum`) implements `IExpressionDatum`
and runs a callback on the first field read, which is how the cancellation scenario cancels at a precise point
without timing. The internal types are reached through the test assembly's `InternalsVisibleTo` access.

The expected values are written out by hand from the ECMAScript rules and the Vega expression documentation, never
produced by running a JavaScript engine: `1 + '2'` is `12`, `'3' * '4'` is `12`, `null + 1` is `1`, `[1, 2] + 3` is
`1,23`, `'x' - 1` is `NaN`, `-5 % 3` is `-2` and `5 % -3` is `2`, `0.1 + 0.2` is `0.30000000000000004`, `1e21 + 1` is
`1e+21`, `null == 0` is false while `null < 1` and `null >= 0` are
true, `NaN == NaN` is false, `[1] == 1` is true while `[1] === [1]` is false (composite values are identical only to
themselves), `'B' < 'a'` is true and `'10' < '9'` is true (strings compare by ordinal code units), and `0 || 'x'`
returns the operand `x`, not a boolean. The sign of zero is shown through the infinity of a reciprocal (`1 / (-0)` is
`-Infinity`).

The step budget is counted by eye: `1 + 2` is the three instructions push, push, and add, so a budget of 3 passes and a
budget of 2 fails at the add, at expression offset 2, with the message "The evaluation exceeded the limit of 2 steps
(VegaLiteLimits.MaxEvaluationSteps) at expression offset 2."; a text of 640 characters searched by `indexof` costs the 3
instructions plus one extra step for each 64 characters (10 steps); and the short-circuit scenarios use a budget of 5
steps, which the skipped operand of 17 instructions would exceed.

Unit tests reside in `ExpressionEvaluatorTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Expressions` folder, with the shared helpers in `ExpressionTestSupport.cs` (the fixed location, `Compile`, `Eval`,
`EvalWith`, `EvalText`, `EvalNumber`, `Obj`, `Steps`, and `UnderCulture`).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test compiles and evaluates its own texts with its own evaluator (an evaluator is one call and
  one thread). The culture scenario runs its body on a dedicated worker thread whose own culture (and UI culture) is set
  to each of eight cultures (invariant, `en-US`, `de-DE`, `fr-FR`, `tr-TR`, `ar-SA`, `ja-JP`, and `sv-SE`), so the culture
  of the test thread and the process defaults are never changed (an exception of the body is rethrown on the test thread
  with its original stack, and a dedicated scenario asserts the isolation); the
  concurrency scenario runs eight parallel iterations, each with its own evaluator and datum, over one shared compiled
  expression

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong value or kind, a result that depends on the culture, a wrong step count or a budget that is off by
one, an operand evaluated that a short-circuit must skip, an exception of the wrong type or without the location of the
expression and the offset, a cancellation that is not noticed, or any difference between repeated or concurrent
evaluations constitutes a failure.

#### Unit-Level Test Scenarios

##### Arithmetic And Concatenation Follow JavaScript

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_ArithmeticAndConcatenation_FollowJavaScript`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_UnaryOperators_FollowJavaScript`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_NegativeZero_IsPreserved`

Asserts, as theories of thirty-six binary and twenty-one unary texts, the coercions of numbers, text, booleans, null,
arrays, and objects in `+`, `-`, `*`, `/`, and `%` (including the empty text, hexadecimal and exponent text, `Infinity`
in its exact spelling, division by zero, the sign of a remainder, and the shortest round-trip number text), the unary
`+`, `-`, and `!`, that the unary operators give the same result when repeated,
and that negative zero survives multiplication and unary minus where JavaScript keeps it.

##### Equality And Comparison Follow ECMAScript

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Equality_FollowsEcmaScriptTables`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_RelationalComparison_FollowsEcmaScript`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Composites_CompareByIdentity`

Asserts, as theories of thirty-one equality texts and twenty-three comparison texts (each also negated), the strict and
loose equality tables (`'1' === 1` is false, `'1' == 1` is true, `null == 0` is false, `true == 'true'` is false,
`NaN != NaN` is true, `({}) == '[object Object]'` is true), the relational rules (strings by ordinal code units, mixed
operands as numbers, `NaN` never ordered, null converted to zero), and that one composite read twice (`datum.o`) is
identical to itself while a literal with the same members is not.

##### Logical Operators And Short Circuit

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_LogicalOperators_ReturnOperands`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_ShortCircuit_SkipsTheOtherOperand`

Asserts that `&&`, `||`, and the conditional return an operand (`null && 1` is null, `'' || 0` is `0`, `0 || null ||
'z'` is `z`, an empty array is truthy), and, with a budget of 5 steps, that `0 && (…)`, `1 || (…)`, and the branch not
taken of `?:` are skipped (their evaluation uses at most five steps) while the operand that must run exceeds the budget
and fails naming `MaxEvaluationSteps`.

##### Member Access Is Null Safe

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_MemberAccess_FollowsJavaScriptAndIsNullSafe`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_ObjectLiteral_KeepsFirstPositionAndLastValue`

Asserts, as a theory of twenty-six texts evaluated twice by one evaluator, array indexing (an index outside the array,
negative, fractional, or not canonical, such as `'01'`, is null; `'1'` finds the second element), `length` of arrays
and text, text indexing, object members by name and by bracket, the members `source`, `flags`, and `global` of a regular
expression, and that a member of null, a number, or a boolean is null; and that `{a: 1, b: 2, a: 3}` has the keys `a`
and `b` with `a` equal to 3.

##### Datum Access: Tables, Values, And Absent Data

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_TableRow_ReadsFlattenedFields`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_TableRow_RebuildsIndexedFieldsAsArrays`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_TableRow_RebuiltCompositesHaveStableIdentity`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_TableRow_NullFieldWithDescendantsReadsAsComposite`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_TableRow_WholeDatumWithIndexNamesIsObject`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_TableRowOutOfRange_ThrowsArgumentOutOfRange`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_WithoutDatum_ReadsNull`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Dates_UseMillisecondsAndIsoText`

Asserts, on a two-row table whose fields are `a.b`, `a.c`, `a\.d` (a literal dot), `x`, and `name with space`, that
`datum.x`, `datum['x']`, `datum.a.b`, `datum['a.d']`, and the spaced name read the cells of the chosen row, that missing
fields and members of missing fields are null, that `datum.a` is rebuilt on demand as an object whose members `b` and
`c` are in field order and `datum` as a whole has the keys `a`, `a.d`, `name with space`, and `x`; that a row position
outside the table is an `ArgumentOutOfRangeException` and null arguments are `ArgumentNullException`; that without a
datum `datum` and `datum.x` are null and a non-object datum has no fields; and, with the date 2024-03-05T00:00:00Z
(1,709,596,800,000 milliseconds), that arithmetic and comparison use the milliseconds while concatenation uses the ISO
text `2024-03-05T00:00:00.000Z`, and that `isDate` and `isObject` hold. On a row whose fields are `tags.0` and `tags.1`
(and `o.list.0` to `o.list.2` below an object) it asserts that `datum.tags` is an array (`isArray`, `length` 2,
`datum.tags[1]`, the text `a,b`), that the names `0` and `2` without `1` stay an object, and that the whole datum holds
the rebuilt array. It asserts the identity of the rebuilt composites on a row with `a.b.c` and the tags: `datum.a ===
datum.a`, `datum.a === datum['a']`, `datum.a.b === datum.a.b`, `datum.tags === datum.tags`, and `datum === datum` hold,
a rebuilt array is not identical to a literal array with the same elements, a path read inside the whole datum and on
its own is one value in either order, and another row (or a later evaluation of the first) gets its own values, so the
cache never leaks between evaluations. A column that is null in a row and has fields below it reads as the array in the
row where it is nested (identical on every read), as the scalar in the row that has one, and as null when the elements
are null too. A row whose field names are `0` and `1` is still an object as a whole datum.

##### Parameters And Unsupported Constructs

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Parameters_AreLookedUpByName`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_UnsupportedConstruct_IsNull`

Asserts that parameter values are found by ordinal name (`p * 2` is 42, a text parameter concatenates, an object
parameter has members, `P` is null because names are case sensitive), that an unknown name and a missing dictionary
are null, and that unknown functions, `random()`, `now()`, `signal`, `event.x`, `item()`, a shift, a bitwise `&`, and
`in` evaluate to null while the compiled expression still lists the unsupported construct, so the strict-mode decision
is made from the compiled report and never from the evaluation.

##### Step Budget Is Exact

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_StepBudget_IsExact`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_StepBudget_AccumulatesAcrossEvaluations`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_SizeProportionalWork_IsCharged`

Asserts that `1 + 2` passes with a budget of 3 steps (three steps used) and fails with a budget of 2 with the exact
message above and the location of the expression; that the budget belongs to the evaluator, so a budget of 5 allows one
evaluation of a three-step program and the second fails while a new evaluator starts afresh; and that searching a
640-character text costs the instructions plus exactly 10 steps (one per 64 characters) and fails under a budget of 5,
so a hostile expression cannot do unbounded work with few instructions.

##### Operators Charge The Size Of What They Convert

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_OperatorsOnLongStrings_ChargeProportionalSteps`

Asserts, as a theory of thirteen expressions (unary `+` and `-`, `-`, `*`, `/`, `%`, `<`, `>=`, `==`, `!=`, a string
compared with itself, and an array holding the string under `+` and `==`), that an operator which reads a 12,800-character
string charges at least 200 steps beyond the instructions, and that a budget of 50 steps fails with the located
`MaxEvaluationSteps` error.

##### Strict Equality Charges The Size Of The Texts It Compares

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_StrictEqualityOfLongStringsConsumesSteps`,
`ExpressionEvaluatorTests.ExpressionEvaluator_StrictEquals_StringsAreChargedByTheShorterLength`

Asserts, as a theory of `===` and `!==` over two 12,800-character texts (and one text with itself), that the comparison
charges at least 200 steps beyond the instructions and that a budget of 50 fails with the located `MaxEvaluationSteps`
error; and that `StrictEquals` with a context charges the shorter length per 64 characters — two characters against 12,800
are free, 6,400 against 12,800 are 100 steps — charges nothing for values of different kinds or for numbers, and charges
nothing without a context.

##### Shared Arrays Are Checked Once Per Node And Their Text Is Bounded

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_SharedArrays_AreCheckedOncePerNodeAndTheirTextIsBounded`

Asserts, on a datum holding a chain of 40 `[x, x]` arrays (2^40 paths), that `[datum.a, datum.a]` is evaluated at once
(the result check visits every node once), and that `'' + datum.a`, `toString(datum.a)`, and `datum.a == 1` fail with the
located `MaxStringLength` error instead of building the repeated text.

##### String Limit

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_StringLimit_IsEnforcedOnConcatenation`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_ExternalStringLongerThanLimit_IsLocatedRenderError`

Asserts, with a `MaxStringLength` of 10, that `'abcde' + '12345'` (exactly the limit) is `abcde12345` and
`'abcde' + '123456'` is a `VegaLiteRenderException` at the location of the expression that names `MaxStringLength`; and
that a text of 12 characters that comes from outside the evaluation (a parameter or a field of a table row) is refused with
the same located failure (naming `VegaLiteLimits.MaxStringLength`) when it is the result as it is or is converted by
`toString`, while `length` of it, which does not keep the text, is 12.

##### Cancellation

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_CanceledBeforeStart_ThrowsOperationCanceled`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_CanceledDuringEvaluation_ThrowsOperationCanceled`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_CancellationBetweenCheckPoints_StopsNextEvaluation`

Asserts that an already canceled token throws `OperationCanceledException` before the first instruction, that a
cancellation during a long evaluation is noticed (the steps counter checks the token whenever it crosses a multiple of
1,024), and that a cancellation raised between two check points (by the test datum on the first field read) lets the
current short evaluation finish with its value (2) and stops the next evaluation with an `OperationCanceledException`,
never wrapped in a render exception.

##### Culture, Repetition, And Concurrency

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_UnderAnyCulture_IsInvariant`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_CultureHelper_IsolatesTheCultureOnAWorkerThread`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Repeated_IsDeterministic`,
`ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_Concurrent_IsDeterministic`

Asserts, under each of the eight cultures (applied to a dedicated worker thread, never to the test thread), that fifteen
expressions (decimal separators, exponents, number to text,
case mapping with the Turkish dotted and dotless i, ordering of `'ä' < 'z'`, `pad`, a case-insensitive regular
expression, `split`, and `substring`) give the same results as under the invariant culture and the documented values
(`1.5`, `0.3333333333333333`, `1e+21`, `-1234567.891`, `21.5`, `ABC I|xyz i`, `lt`, `ge`, `2`, `1.5,2.25`, `0007.5`,
and `true`); that the culture helper changes the cultures of its worker thread only, leaving those of the test thread
unchanged, and rethrows an exception of the body; that two hundred evaluations of one compiled expression give one
result (`9aB2`); and that eight threads,
each with its own evaluator, give eight hundred identical results (`f0 b072`) from one shared compiled expression.

##### Large Programs And A Reused Stack

**Tests**: `ExpressionEvaluatorTests.ExpressionEvaluator_Evaluate_LargeProgramsAndReusedStack_AreCorrect`

Asserts that an array literal of 400 elements (a program that needs a deep stack) is built with its 400 elements in
order, and that a one-instruction program on the same evaluator afterwards still gives 1, so the value stack is reused
without leaking its earlier contents.

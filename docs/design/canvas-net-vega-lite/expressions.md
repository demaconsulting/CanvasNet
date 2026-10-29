## Expressions

![Expressions Structure](VegaLiteExpressionsView.svg)

### Overview

The `Expressions` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Expressions`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Expressions/`) is the expression engine of the renderer: it compiles the text of
a Vega expression, evaluates it against a row and a set of parameter values, and owns the closed table of functions that
an expression may call. It is **internal**: its types are visible only to the other subsystems of the system and,
through `InternalsVisibleTo`, to the test assembly. It is a **closed world**: the language is a subset of JavaScript
with literals, `datum` access, a fixed set of operators, and calls of whitelisted pure functions; there is no
reflection, `eval`, assignment, `new`, function expression, file or network access, clock, or random source, so an
expression is a pure, deterministic function of its datum and its parameters. It contains three units:

- **ExpressionParser** - the internal `ExpressionParser` (with its tokenizer), `CompiledExpression` and its helper types,
  and the construct and reference records: compiles the text in one bounded pass into an immutable postfix program, under
  `MaxExpressionLength` and `MaxExpressionDepth`, and collects what it parses but does not execute; see
  _ExpressionParser Unit Design_ (`expressions/expression-parser.md`)
- **ExpressionEvaluator** - the internal `ExpressionEvaluator` and the `IExpressionDatum` interface: runs a compiled
  expression with JavaScript coercion, culture-independently, under the step budget and the cancellation token; see
  _ExpressionEvaluator Unit Design_ (`expressions/expression-evaluator.md`)
- **ExpressionFunctions** - the internal `ExpressionFunctions` table of 57 functions, the `ExpressionFunctionContext`,
  and `ExpressionRegex`: the math, text and array, type and conversion, and regular expression functions; see
  _ExpressionFunctions Unit Design_ (`expressions/expression-functions.md`)

The subsystem's namespace documentation is in `Expressions/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Expressions-*` requirements in `docs/reqstream/canvas-net-vega-lite/expressions.yaml`.

### Interfaces

**Exposed** (internal only): `ExpressionParser.Parse(string, VegaLiteLocation, VegaLiteLimits?)`, which returns an
immutable `CompiledExpression` (its `Source`, `Location`, `Depth`, `InstructionCount`, `ReferencesDatum`,
`UnsupportedConstructs`, and `ParameterReferences`); the `ExpressionEvaluator` constructor (limits, a cancellation token,
and the parameter values by name) with `Evaluate(CompiledExpression)`, `Evaluate(CompiledExpression, DataTable, int)`,
`Evaluate(CompiledExpression, DataValue)`, and `Evaluate(CompiledExpression, IExpressionDatum)`, `StepsUsed`, and the
coercion helpers `IsTruthy`, `ToNumber`, `ToJsString`, `StrictEquals`, `LooseEquals`, and `NumbersEqual` that the
transforms reuse; `ExpressionFunctions.Names()`, `TryGet(string, out ExpressionFunction)`, and
`Invoke(string, ReadOnlySpan<DataValue>, ExpressionFunctionContext)`.

**Consumed**: `DataValue`, `DataValueKind`, `DataObject`, `DataArray`, `DataTable`, and `JsNumber` of the `Data`
subsystem (the value model, with the array, object, and regular expression kinds that expressions produce), and the public
`VegaLiteLimits`, `VegaLiteLocation`, `VegaLiteSpecException`, and `VegaLiteRenderException` types of the
`VegaLiteDocument` unit. The .NET base class library supplies `System.Text.RegularExpressions`, `System.Collections.Frozen`,
`System.Globalization`, and `System.Math`.

**Consumers**: the `SpecModel` subsystem (`SpecParser` compiles the expression of every supported site when the document
is opened and keeps the result in the tree; `FeatureCatalog` reports `UnsupportedConstructs`), the `Transforms` subsystem
(`RowTransforms` and `ParameterEnvironment` evaluate the compiled expressions and reuse the coercion helpers), and the
`Marks` subsystem (`MarkBuilder` evaluates the row-bound paint values through the evaluator that the document hands it).

### Design

**Compile once, at open time; run many times.** Every expression the renderer can run is compiled when the document is
opened, by `SpecParser`, so a syntax error, an over-long or over-deep expression, or a reference to an unknown
parameter is a located `VegaLiteSpecException` before any data is read. The compiled program is immutable, thread-safe,
and shared by every `Measure` and `Render` call; the evaluator, the step counter, and the regular expression cache belong
to one call on one thread.

**A postfix program, not a syntax tree.** The parser emits instructions for a stack machine directly while it parses:
operands are pushed, operators replace their operands, and forward jumps implement `&&`, `||`, and `?:`. Nothing walks a
tree afterwards, so a left-associative chain such as `a + b + c + ...` of any length compiles in a loop and evaluates in
a loop. `MaxExpressionDepth` therefore means the **nesting** of the text (groups, brackets, call argument lists, array and
object literals, conditional branches, and unary operands), not its length: level 64 is accepted and level 65 is rejected,
and the call stack of the parser is bounded by the limit and never by the length of the text.

**What parses but does not run.** Three groups of constructs are valid text that the renderer deliberately does not
execute, so that they can be reported by stable identifier instead of failing the document: a call of a function outside
the table (`vl-expr-function-<name>`, the name lower-cased; the construct and the message keep it as written), view
state (`signal`, `event`, `item`, and a view signal
such as `width` that no parameter declares: `vl-expr-view-state`), and the deferred operators (the bitwise operators, the
shifts, and `in`: `vl-expr-construct-<name>`). The parser collects them in order of appearance; the evaluator gives null
for each; the `FeatureCatalog` reports them at the string value of the expression, and strict mode throws at the first
report. Method calls, assignment, `new`, function and arrow expressions, `**`, `??`, the comma, and the semicolon are
syntax errors instead, because they cannot be given a meaning without executing arbitrary code.

**JavaScript semantics over one value model.** The evaluator works on the `DataValue` of the `Data` subsystem, extended
with array, object, and regular expression values. Coercion follows ECMAScript (`+` concatenates when either operand is
text, `==` and `===` follow the equality tables, relational comparison orders text by code unit, `&&` and `||` return an
operand, division by zero is an infinity or NaN, negative zero is kept). The value model has one null for both `null` and
`undefined`, which is the documented deviation: null converts to 0 in arithmetic. Composite
values compare by identity, as
JavaScript's do. Everything is invariant of the current culture and the time zone.

**Bounded work, fail closed.** The text is bounded by `MaxExpressionLength` (checked before tokenizing), the nesting by
`MaxExpressionDepth`, the work by `MaxEvaluationSteps` (one step per executed instruction, plus one step per 64
characters or elements of the work of a size-proportional function; the budget is exact, and it is shared by the
parameters, the transforms, and the row-bound mark values of one call), strings by `MaxStringLength`, a regular
expression pattern by 1,024 characters, and a regular expression match by the linear-time engine where the pattern allows
it or a 100 ms timeout where it does not. Cancellation is checked when an evaluation starts and whenever the step counter
crosses a multiple of 1,024. A failure that depends on the data is a `VegaLiteRenderException` located at the expression
string with the offset in the message; a cancellation is an `OperationCanceledException`, never wrapped.

**Closed function table.** `ExpressionFunctions` is the whole security boundary of the
language: a function exists only if
it is in the frozen table. The families are the math functions, the text and array functions, the type and conversion
functions, and the regular expression functions. The date and time, formatting, color, geographic, scale, statistical, and
data-flow functions are deferred to later phases of the roadmap. There is no `random` function because reproducible
rendering is a system contract.

### ExpressionEvaluator

![Expressions Structure](VegaLiteExpressionsView.svg)

The `ExpressionEvaluator` unit is the second software unit in the `Expressions` subsystem. It comprises three source
files in the `DemaConsulting.CanvasNet.VegaLite.Expressions` namespace, all `internal`:

- `ExpressionEvaluator.cs` - `ExpressionEvaluator` (sealed partial class), the `IExpressionDatum` interface, the entry
  points, and the loop over the postfix program
- `ExpressionEvaluator.Coercion.cs` - the JavaScript coercion, equality, and relational helpers (static, shared with the
  `Transforms` subsystem)
- `ExpressionEvaluator.Datum.cs` - the reading of `datum` from a table row, a value, or a caller-defined object, including
  the rebuilding of nested objects from flattened fields

#### Purpose

`ExpressionEvaluator` runs a `CompiledExpression` against a datum and a set of parameter values and returns a `DataValue`,
with JavaScript and Vega semantics, independently of the culture and the time zone, under an explicit step budget and a
cancellation token. It exists so that the meaning of an expression is defined in one place, so that every stage that needs
a value computed from a row (a calculated field, a filter, a parameter, a mark paint) gets
the same answer, and so that the
amount of work a hostile expression can cause is bounded by construction.

The unit's behavior is specified by the `CanvasNetVegaLite-Expressions-ExpressionEvaluator-*` requirements in
`docs/reqstream/canvas-net-vega-lite/expressions/expression-evaluator.yaml`.

#### Data Model

An evaluator serves **one measure or render call on one thread**; it is not thread-safe, but it holds no shared mutable
state, so evaluators of different calls never influence each other, and a `CompiledExpression` is shared freely. It holds
the per-evaluation `ExpressionFunctionContext` (the step counter and budget, the cancellation
token, the string limit, and the
regular expression cache; see _ExpressionFunctions Unit Design_), the dictionary of parameter values by name (ordinal; a
name that is not in it reads null), a reusable value stack, and the datum of the current evaluation in one of four modes:
none, a row of a `DataTable`, a `DataValue` (normally an object), or a caller-defined `IExpressionDatum` (with
`GetField(string)` and `ToValue()`).

**Value semantics.** Values are the `DataValue` kinds null, boolean, number, date, and string and the composite kinds array,
object, and regular expression. The value model has **one null for both `null` and `undefined`**, so null converts to
0 in arithmetic (a documented deviation from JavaScript, where `undefined` would give NaN). Semantics follow ECMAScript:

- **Coercion**: `ToNumber` (text is trimmed and parsed as a JavaScript numeric string, with hexadecimal, octal, and binary
  prefixes; the empty text is 0; a boolean is 1 or 0; an array coerces through its text), `ToJsString` (JavaScript's
  `String(value)`: shortest round-trip number text, `[1,2]` as `1,2`, an object as `[object Object]`, a date as ISO 8601),
  truthiness (`false`, null, 0, NaN, and the empty text are falsy; composites are truthy).
- **Operators**: `+` is concatenation when either operand coerces to text, otherwise addition; `-`, `*`, `/`, and `%` are
  numeric (division by zero is an infinity or NaN; the sign of zero is kept; `%` is the
  JavaScript remainder); the unary `+`,
  `-`, and `!` (the unary `~` is a deferred construct that gives null);
  `===` and `!==` follow strict equality (NaN differs from itself,
  zero equals negative zero, composites compare by identity, so two separately written `[]`
  differ); `==` and `!=` follow the
  abstract equality table; `<`, `<=`, `>`, and `>=` follow the abstract relational
  comparison (numbers when either side is
  not text, text by ordinal comparison of code units, NaN gives false); `&&`, `||`, and `?:` evaluate only the operand they
  need, neither run nor charge the skipped one, and return an operand, not a boolean.
- **Member access**: on an object value by key, on an array by index and `length`, on text
  by index and `length`, and on a
  regular expression by `source`, `flags`, and `global`; the member of null, a missing member, and an out-of-range index
  are null (access is null-safe, never an error). An object literal
  keeps the position of the first occurrence of a repeated key and the value of the last.
- **Datum**: `datum.name` reads the field of the row whose canonical name is the name (escaped like a field path); when
  the table has no such cell that holds a value but has fields below it (a nested object stored as `name.child`), the
  object is rebuilt from them on demand, and an array from `name.0`, `name.1`, ... exactly when the names below it are
  the indexes 0 to n-1 (trailing nulls are not part of the rebuilt array; a cell that is null with only null elements
  below it stays null), as the data loader flattens them. The rebuilding is the shared flat-row tree of the data unit.
  Every rebuilt array or object is cached for the evaluation of the row by its canonical path, so reading it again, or
  reading it inside the whole datum, gives the same value (JavaScript identity: `datum.a === datum.a`); the cache is
  cleared at the start and at the end of every evaluation. Reading `datum` as a whole value builds an object of all the
  fields of the row (an object even when the field names are indexes), charged to the step budget in proportion to its
  size.
- **Strings from outside**: a text that is the result of an evaluation as it is (a parameter, a field of the data, a literal)
  or that is converted to text is checked against `MaxStringLength` like the results of the functions; `MaxStringLength`
  reports the limit so that the parameter environment can check the values it takes from the caller.
- **Unsupported constructs** (functions outside the table, `signal`, `event`, `item`, the
  deferred operators) evaluate to null.

**Step accounting.** One step is one executed instruction (jumps included); a function that does work proportional to the
size of its input charges one more step per 64 characters or elements. The operators charge the same way through the
evaluator's context: reading a string as a number (unary `+` and `-`, `-`, `*`, `/`, `%`, the relational operators, loose
equality, and the numeric form of `+`) charges its length, converting an array to a primitive charges the length of the
text it builds, an ordinal comparison of two strings and a strict (in)equality of two strings charge the shorter length,
and the check of the result charges the
elements and members of every distinct composite it scans (a composite that is shared is scanned once). The text of an
array is built with the length checked as it grows, so a shared array that would repeat its content past
`MaxStringLength` fails early. The budget is `MaxEvaluationSteps`, **shared by all
the evaluations of one evaluator**, and exact: a program of `N` instructions without jumps passes with a limit of `N` and
fails with `N - 1`. `StepsUsed` reports the total.

#### Key Methods

- `ExpressionEvaluator(limits, cancellationToken, parameters)` - creates the evaluator and its context; the parameter
  dictionary is read live, so the caller fills it before the first evaluation that needs a name.
- `Evaluate(expression)`, `Evaluate(expression, table, row)`, `Evaluate(expression, DataValue datum)`, and
  `Evaluate(expression, IExpressionDatum datum)` - set the datum mode, check the token, and run the program. The table
  overload requires a row in range (`ArgumentOutOfRangeException` otherwise).
- `Run` - the loop: it keeps the value stack in an array that grows to the largest stack
  height of the program, fetches the
  instruction at the program counter, charges one step, and executes it; forward jumps change the counter, and a call
  instruction passes a span of the topmost arguments to the function and replaces them by its result. There is no recursion
  in the evaluator, so neither the length nor the nesting of an expression can overflow the call stack.
- `IsTruthy`, `ToNumber`, `ToJsString`, `StrictEquals`, `LooseEquals`, `NumbersEqual`, and `RelationalLess` - the static
  coercion helpers, reused by the filter predicates of the `Transforms` subsystem so that a predicate and an expression
  agree on equality and ordering. `ToNumber`, `ToPrimitive`, `ToJsString`, `StrictEquals`, `LooseEquals`, and
  `RelationalLess` take an optional `ExpressionFunctionContext` to charge their work to; the evaluator always passes its
  own, and the filter predicates pass the same context (exposed to the unit by the internal `Context` property), so a
  predicate and an expression are charged alike.

**Preconditions and postconditions**: the expression was compiled with limits compatible with the evaluator's (the same
`VegaLiteLimits` instance in the pipeline); on return the result is a value or an exception was thrown; the step counter
never decreases; cancellation is checked at the start of every evaluation and whenever the counter crosses a multiple of
1,024.

#### Error Handling

A failure that depends on the data is a `VegaLiteRenderException` located at the `Location` of the compiled expression (the
JSON pointer and the line and column of the expression string), with the expression offset of
the instruction that failed in
the message: the step budget is exhausted, a string would exceed `MaxStringLength` (concatenation, `join`, `pad`, `replace`,
and the other string builders), or a regular expression is invalid, too long, or times out (see _ExpressionFunctions Unit
Design_). The functions signal these with the internal `ExpressionRuntimeException`, which the
evaluator converts and which never
reaches a public caller. A canceled token is an `OperationCanceledException`, never wrapped. A null expression, table, or
datum argument is an `ArgumentNullException`. Anything else that JavaScript would turn into `undefined` or NaN (a missing
field, a division by zero, a non-numeric text in arithmetic) is a value, not an error. The
unit performs no I/O and uses no
clock or random source.

#### Dependencies

- **`CompiledExpression`**, **`ExpressionInstruction`**, and **`ExpressionOpcode`** - the program, from the
  `ExpressionParser` unit
- **`ExpressionFunctionContext`**, **`ExpressionFunction`**, and
  **`ExpressionRuntimeException`** - the function context and
  the function delegate, from the `ExpressionFunctions` unit
- **`DataValue`**, **`DataObject`**, **`DataTable`**, **`FieldPath`**, and **`JsNumber`** -
  the value, row, field-name, and
  number-formatting model of the `DataTable` unit of the `Data` subsystem
- **`VegaLiteLimits`**, **`VegaLiteLocation`**, and **`VegaLiteRenderException`** - the public types of the
  `VegaLiteDocument` unit
- **.NET base class library** - `System.Globalization` (invariant-culture parsing and formatting) and `System.Math`

#### Callers

`VegaLiteDocument` creates one evaluator per `Measure` or `Render` call (with the parameter dictionary and the cancellation
token) and hands it to the `Transforms` subsystem (`ParameterEnvironment`, `RowTransforms`) and, wrapped in `RowExpressions`,
to `MarkBuilder`, so one step budget covers the parameters, the transforms, and the mark values of the call. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionEvaluatorTests.cs` call it directly through
`InternalsVisibleTo`.

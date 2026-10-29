### ExpressionFunctions

![Expressions Structure](VegaLiteExpressionsView.svg)

The `ExpressionFunctions` unit is the third software unit in the `Expressions` subsystem. It
comprises six source files in
the `DemaConsulting.CanvasNet.VegaLite.Expressions` namespace, all `internal`:

- `ExpressionFunctions.cs` - `ExpressionFunctions` (static partial class): the frozen function table, `Names`, `TryGet`,
  `Invoke`, and the math functions
- `ExpressionFunctions.Text.cs` - the string and array functions
- `ExpressionFunctions.Types.cs` - the type-checking and conversion functions
- `ExpressionFunctions.Regex.cs` - `regexp`, `test`, and the regular expression forms of `replace` and `split`
- `ExpressionFunctionContext.cs` - the `ExpressionFunction` delegate and `ExpressionFunctionContext`, the per-evaluation
  state
- `ExpressionRegex.cs` - `ExpressionRegex`, the translation of a JavaScript regular expression into a safe .NET one

#### Purpose

`ExpressionFunctions` is the closed table of the pure functions that an expression may call, with the per-evaluation
context that bounds their work. It exists so that the security boundary of the expression language is one frozen list: a
function is callable only if it is in this table, and every function in it is deterministic,
culture-invariant, free of side
effects, and bounded in time and memory. Date and time, formatting, color, geographic, scale, statistical, and data-flow
functions are not in the table (they are reported by the `FeatureCatalog` and evaluate to null), and there is no `random`
function.

The unit's behavior is specified by the `CanvasNetVegaLite-Expressions-ExpressionFunctions-*` requirements in
`docs/reqstream/canvas-net-vega-lite/expressions/expression-functions.yaml`.

#### Data Model

**The table** is a `FrozenDictionary<string, ExpressionFunction>` with ordinal, case-sensitive names, built once. An
`ExpressionFunction` is a delegate
`(ReadOnlySpan<DataValue> arguments, ExpressionFunctionContext context) -> DataValue`; an
argument that is not supplied is simply missing (JavaScript's `undefined`: NaN as a number, the
text `undefined` as text), and
an optional argument that is null counts as not supplied. The 57 functions are:

- **Math (27)**: `abs`, `acos`, `acosh`, `asin`, `asinh`, `atan`, `atan2`, `atanh`, `ceil`, `clamp`, `cos`, `cosh`, `exp`,
  `floor`, `log`, `log10`, `max`, `min`, `pow`, `round`, `sign`, `sin`, `sinh`, `sqrt`, `tan`,
  `tanh`, and `trunc`. They use
  the host math library (results for the transcendental functions can differ from a browser in the last bit); `round` is
  JavaScript's half-up rounding, `max` and `min` take any number of arguments, `sign` keeps NaN and zero, and negative
  zero is preserved.
- **Text and array (15)**: `lower`, `upper`, `trim`, `substring`, `slice`, `indexof`, `lastindexof`, `startswith`,
  `endswith`, `includes`, `replace`, `split`, `join`, `pad`, and `length`. Strings are UTF-16 code unit sequences as in
  JavaScript (indices, lengths, and slices count code units); comparison is ordinal and invariant; `lower` and `upper`
  apply JavaScript's full case mappings on top of the invariant culture of .NET (the sharp s becomes `SS`, the Latin and
  Armenian ligatures, the letters with a combining mark and the Greek letters with an iota subscript become several
  characters, the capital I with a dot above lowers to `i` and a combining dot, the dotless i maps to upper case `I`,
  and a capital sigma lowers to the final sigma at the end of a word) and their result is checked against `MaxStringLength`;
  `slice`, `indexof`, `lastindexof`, `includes`, and `length` accept a string or an array; `pad` follows Vega (the align
  `left` pads the start, `center` both sides, the default the end); `join` gives null for a non-array.
- **Type and conversion (13)**: `isArray`, `isBoolean`, `isDate`, `isFinite`, `isNaN`, `isNumber`, `isObject`, `isRegExp`,
  `isString`, `isValid`, `toBoolean`, `toNumber`, and `toString`. `isNaN` and `isFinite` do not coerce (as
  `Number.isNaN` and `Number.isFinite`), `isObject` is true for objects, arrays, dates, and regular expressions, `isValid`
  rejects null and NaN, and the three conversions give null for null and for the empty text.
- **Regular expressions (2 more names, plus the forms of `replace` and `split`)**: `regexp` and `test`.

**`ExpressionFunctionContext`** is the per-evaluation state: the step counter and budget (`MaxEvaluationSteps`), `Step()`
(one step per instruction; the step that exceeds the budget is the reported one), `ChargeSize(n)` (one extra step for every
64 characters or elements of size-proportional work, such as searching, splitting, replacing,
matching, padding, joining, and
materializing a datum), `ThrowIfCanceled()`, `CheckStringLength` (`MaxStringLength`), and
`GetRegex(source, flags)` with a
cache of at most 64 compiled expressions that is cleared when full. The cancellation token is polled whenever the step
counter crosses a multiple of 1,024.

**`ExpressionRegex`** is an immutable, thread-safe pair of the JavaScript source and flags and the .NET `Regex` that
implements them. The accepted flags are `g`, `i`, `m`, and `s` (a repeated or unknown flag is an error, and so are `u`,
`v`, `y`, and `d`, which the UTF-16 engine cannot honor, so no pattern silently matches differently from JavaScript; `g`
makes `replace` replace every match). The pattern is at most 1,024 characters. The translation makes the common cases
match JavaScript: `\d`, `\D`, `\w`, and `\W` are the ASCII classes (also inside a character class, negated or not),
`\s` and `\S` are the JavaScript white space, `$` without the `m` flag matches only at the end of
the text, and `[^]` and `[]` have their JavaScript meaning. The `Regex` is built with `RegexOptions.NonBacktracking |
CultureInvariant`, which matches in time linear in the input, with a 5 second timeout as a backstop; a pattern that
engine rejects (look-around, back-references, atomic groups) falls back to the backtracking engine with a **100
millisecond timeout that fails closed**. Known deviations: the numbering of mixed named and unnamed groups follows .NET,
and `.` and `\s` follow the .NET classes.

#### Key Methods

- `Names()` - the 57 names in ordinal order; `TryGet(name, out function)` - a case-sensitive lookup used by the parser to
  decide whether a call is supported; `Invoke(name, arguments, context)` - calls a function (a name that is not in the table
  is an `ArgumentException`, a programming error because the parser never compiles such a call).
- Each family registers its functions in one method (`AddMath`, `AddText`, `AddTypes`,
  `AddRegex`); `BuildTable` freezes the
  result. The helpers convert arguments the JavaScript way (`Num`, `Text`, an integer-or-infinity conversion, a position
  clamp, and a position that counts from the end) and check results against `MaxStringLength` before a string is built.
- `replace(string, pattern, replacement)` replaces the first occurrence of a text pattern or the matches of a regular
  expression, with JavaScript's replacement patterns (`$&`, `$1`, `$<name>`, `$$`, `` $` ``, and `$'`); `split(string,
  separator[, limit])` splits at a text or a regular expression with JavaScript's results for the empty inputs.
- `ExpressionRegex.Create(source, flags)` - validates the length and flags, translates the pattern, and chooses the engine.

**Preconditions and postconditions**: the arguments are values the evaluator produced; on return the result is a value and
the context has been charged for the work, or an exception was thrown.

#### Error Handling

A function signals a failure with `ExpressionRuntimeException`, which the `ExpressionEvaluator` converts into a located
`VegaLiteRenderException`: the step budget is exhausted, a result string would be longer than `MaxStringLength`, a regular
expression pattern is longer than 1,024 characters, invalid, or has an unknown or repeated flag, and a backtracking match
exceeds its 100 millisecond timeout (a pattern that would take exponential time fails closed instead of hanging). A canceled
token is an `OperationCanceledException`. A wrong type of argument is never an error: JavaScript coercion gives a value
(often NaN, null, or false). An unknown function name passed to `Invoke` is an `ArgumentException`.

#### Dependencies

- **`DataValue`**, **`DataValueKind`**, **`DataObject`**, and **`JsNumber`** - the value model
  of the `DataTable` unit of the
  `Data` subsystem
- **`ExpressionEvaluator`** - the coercion helpers (`ToNumber`, `ToJsString`, `IsTruthy`, `StrictEquals`, `NumbersEqual`)
  that the functions share so that a function and an operator agree
- **`VegaLiteLimits`** - the public limits (`MaxStringLength`, `MaxEvaluationSteps`)
- **.NET base class library** - `System.Collections.Frozen`, `System.Text.RegularExpressions`, `System.Math`, and
  `System.Globalization` (invariant culture)

#### Callers

`ExpressionParser` calls `TryGet` to decide whether a call is supported and stores the function in the program;
`ExpressionEvaluator` invokes the function delegates of a program and owns the `ExpressionFunctionContext`. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Expressions/ExpressionFunctionsTests.cs` call `Names`, `Invoke`, and the context
directly through `InternalsVisibleTo`.

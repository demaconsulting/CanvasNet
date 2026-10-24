### ScadValue

<!-- cspell:ignore evaluator undef lexicographic lexicographically multiplicand infinities scalars unseeded -->
<!-- cspell:ignore reentrancy hashable ordinally fmod trunc -->

The `ScadValue` unit is the first software unit in the `ScadEvaluation` subsystem: the internal value model of
the evaluator. It comprises thirteen source files in the `DemaConsulting.CanvasNet.Scad.Evaluation`
namespace, all `internal`:

- `ScadValueKind.cs` — the enumeration of value kinds
- `ScadValue.cs` — `ScadValue` (readonly partial struct): the kind, the payload accessors, the factories,
  truthiness, equality, and hashing
- `ScadValue.Compare.cs` — ordering (`TryCompare`)
- `ScadValue.Arithmetic.cs` — the binary and unary operators, indexing, and member access
- `ScadValue.Matrix.cs` — dot, matrix-vector, vector-matrix, and matrix-matrix products
- `ScadValue.Format.cs` — the string form and the echo form
- `ScadValue.Convert.cs` — `TryFromObject`, the conversion from caller-supplied objects
- `ScadVector.cs` and `ScadRange.cs` — the vector and range payloads
- `IScadFunctionValue.cs` — the interface a function value implements (the evaluator's closure)
- `ScadNumberFormatter.cs` — the deterministic number format
- `ScadNumbers.cs` — named IEEE equality of two doubles
- `ScadText.cs` — Unicode-scalar helpers for strings

#### Purpose

`ScadValue` represents every value a program can compute and defines, in one place, the rules that every
operator and built-in function relies on: truthiness, equality, ordering, arithmetic, indexing, text forms,
and number formatting. The rules are the CanvasNetScad dialect's own decisions, specified by general
language-design reasoning and pinned by tests; no OpenSCAD source, manual, or test material was consulted
(see _CanvasNetScad System Design_, Clean-Room Provenance). The unit does not know about scopes, calls, or
limits: it never charges evaluation steps (the evaluator does) and it never enforces the list and string
limits (the constructs that build values do).

The unit's behavior is specified by the `CanvasNetScad-ScadEvaluation-ScadValue-*` requirements (`Kinds`,
`Truthiness`, `Equality`, `Ordering`, `Arithmetic`, `Products`, `Indexing`, `Ranges`, `NumberFormatting`,
`TextForms`, `NestingLimit`, `Conversion`, and `Immutability`) in
`docs/reqstream/canvas-net-scad/scad-evaluation/scad-value.yaml`.

#### Data Model

**`ScadValue`** is a readonly struct of three fields: the `ScadValueKind`, a `double` payload (numbers, and
booleans as 0 or 1), and an `object?` reference payload (strings, vectors, ranges, and functions). Numbers and
booleans therefore never allocate. `default(ScadValue)` is **undef**. The kinds are `Undef`, `Boolean`,
`Number`, `String`, `Vector`, `Range`, and `Function`. The payload accessors `AsNumber`, `AsBoolean`,
`AsString`, `AsVector`, `AsRange`, and `AsFunction` throw `InvalidOperationException` when the value has a
different kind (they are named so that they do not shadow the base class library types `String`, `Boolean`,
and so on). `KindDescription` names a kind for diagnostics (`undef`, `a boolean`, `a number`, `a string`,
`a vector`, `a range`, `a function`).

**`ScadVector`** is an immutable sequence of values with `Count`, an indexer, `AsSpan()` (a read-only span),
and `Depth`, the nesting depth computed once from its items (a vector of scalars has depth 1). `Depth` of a
non-vector value is 0. The factory `FromVector(ScadValue[])` takes ownership of the array (the caller must not
modify it afterwards); `FromVector(IReadOnlyCollection<ScadValue>)` copies. Because values are immutable,
vectors can be shared freely, which the lazy variable cells and closures of the evaluator rely on.

**`ScadRange`** holds `Start`, `Step`, and `End` as written. It is **lazy**: it is never materialized except
by iteration. `Count` is `floor((end - start) / step + 1e-9) + 1` for a step in the right direction and 0
otherwise, and saturates at `long.MaxValue - 1` for enormous ranges so that `[0 : 1e300]` can be measured
without overflow; `ElementAt(i)` returns `start + i * step` (never an accumulated sum, so rounding does not
drift); `IsInvalid` is true for a zero step or any non-finite part, which the evaluator reports as a warning.

**Limits owned by the unit**: `MaxNestingDepth = 256` (a vector may be nested at most 256 levels deep) and
`MaxConvertedElements = 1,000,000` (the most elements a converted object may contain). The factories do **not**
check the nesting depth; the constructs that build values (vector literals, comprehensions, `concat`) check
`Depth` against `MaxNestingDepth` and raise the evaluation error.

**`IScadFunctionValue`** exposes `ParameterNames` and is implemented by the evaluator's closure type, so the
value unit does not depend on scopes.

#### Key Methods

##### Truthiness (`IsTrue`)

`undef`, `false`, `0` and `-0`, the empty string, and the empty vector are false. **Everything else is true**,
including `NaN` (any non-zero double), non-empty strings and vectors, every range, and every function.
Conditions in `if`, `?:`, `!`, `&&`, `||`, comprehension `if`, the C-style `for` condition, and `assert` use
this table. `&&` and `||` short-circuit and yield booleans; `!x` yields a boolean; `?:` yields the chosen
branch's value.

##### Equality and hashing (`Equals`, `GetHashCode`)

Equality requires **the same kind**: a number is never equal to a boolean. Numbers compare by IEEE rules (so
`NaN` is not equal to `NaN`, and `-0` equals `0`; the comparison is `ScadNumbers.AreEqual`, a named helper because
direct `==` between doubles is forbidden by the project's static analysis); strings compare ordinally; vectors
compare element by element with equal length (deep); `undef` equals `undef`; ranges compare by start, step,
and end; functions compare by reference. Equal values have equal hash codes (the hash of `-0` equals that
of `0`, and `NaN` values hash alike even though they are not equal).

##### Ordering (`TryCompare` and the relational operators)

Two numbers order numerically; two strings ordinally (UTF-16 code units); two booleans with `false` before
`true`; two vectors lexicographically, with a shorter prefix smaller. **Every other pairing is unordered**
— including `NaN`, mixed kinds, and `undef` — and then all four relational operators (`<`, `<=`, `>`, `>=`)
are `false` without any diagnostic.

##### Arithmetic (`Binary`, `Unary`)

`Binary(op, left, right, out typeError)` and `Unary(op, operand, out typeError)` return the result and set
`typeError` when the operands do not fit the operator; the result is then `undef` (or, for vectors, a vector
of the same shape whose bad elements are `undef`), and the evaluator turns the indication into a warning.

- **Numbers**: `+ - * /` and `%` follow IEEE (`x / 0` is an infinity, `0 / 0` is `NaN`; `%` is the C `fmod`,
  so `-7 % 3` is `-1`); `^` is `Math.Pow` and binds tighter than unary minus (`-2 ^ 2` is `-4`).
- **Vectors**: `+` and `-` are element-wise for equal lengths (recursively for nested vectors; otherwise
  `undef`); `*` of a number and a vector, and `/` of a vector by a number, apply to every element; unary `-`
  negates every element; string `+` is `undef` (use `str` or `concat`).
- **Products** (`ScadValue.Matrix.cs`): the product of two equal-length numeric vectors is the **dot
  product**; a matrix (a rectangular list of numeric lists) times a vector, a vector times a matrix, and two
  matrices give the linear-algebra product; mismatched dimensions give `undef`.
- **Equality and logic** (`==`, `!=`, `&&`, `||`, `!`) never produce a type error.

The evaluator charges evaluation steps for vector work (the unit itself does not), because a product of two
large matrices allocates quadratic work.

##### Indexing and members (`Index`, `Member`)

`v[i]` on a vector with a finite numeric `i` returns the element at `trunc(i)` (so `v[1.5]` is `v[1]` and
`v[-0.5]` is element 0); a negative or too large index gives `undef` without a warning. A string indexes by
**Unicode scalar value**, returning a one-character string (a surrogate pair is one scalar). A non-numeric
index, a range, a number, a boolean, or `undef` as the target gives `undef`. `.x`, `.y`, and `.z` on a vector
are elements 0, 1, and 2; any other member is `undef`.

##### Text forms (`ToStr`, `ToEchoText`, `TryToStr`, `TryToEchoText`)

- **String form** (used by `str`): a string is written raw at the top level and quoted when nested in a vector;
  numbers use the formatter below; `true`, `false`, and `undef` are written as such; a vector is `[1, 2, 3]`
  (separator `,`); a range is `[0 : 1 : 5]` — **start : step : end**, in written order, each part formatted; a
  function is `function(a, b)` with its parameter names.
- **Echo form** (used by `echo`): strings are **always quoted** with `"`, and `\\`, `\"`, `\n`, `\t`, and `\r`
  are escaped so that a line never contains a raw line break; everything else is as in the string form.
- The `Try…` variants take a maximum length and **stop as soon as it is exceeded**, so a huge vector cannot
  allocate an enormous string before the string-length limit is checked.

##### Number formatting (`ScadNumberFormatter.Format`)

The format is the one pinned by echo output, diagnostics, and the golden files; it never uses a
culture-sensitive conversion.

1. `NaN` is `nan`, `+∞` is `inf`, `-∞` is `-inf`, zero is `0`, and negative zero is `-0`.
2. Otherwise the absolute value is rendered with the invariant-culture `E5` format (six significant digits,
   correctly rounded from the exact binary value on every supported runtime); the six digits and the decimal
   exponent `X` are taken from that text.
3. Trailing zeros are removed (at least one digit remains).
4. If `X < -4` or `X >= 6` the result is **scientific**: the first digit, a point and the remaining digits when
   there are any, `e`, the sign `+` or `-`, and the exponent with **at least two digits** (`1e+06`, `1.5e-07`,
   `1e+100`).
5. Otherwise it is **fixed**: for `X >= 0` the digits with the point after position `X + 1`, padded with zeros
   when the digits are shorter; for `X < 0` the text `0.` followed by `-X - 1` zeros and the digits.
6. A negative value is prefixed with `-`.

An exact decimal tie (for example `1234565`, halfway between two six-digit results) is resolved by the exact
digit generation to the **even digit**: `1234565` prints as `1.23456e+06` (and `1234575` as `1.23458e+06`); a
test pins this. Pinned results include `0.1` → `0.1`, `1/3` → `0.333333`, `100000` → `100000`, `123456` →
`123456`, `1234567` → `1.23457e+06`, `999999.5` → `1e+06`, `12345.67` → `12345.7`, `0.0001` → `0.0001`,
`0.00001` → `1e-05`, `1e10` → `1e+10`, `-2.5` → `-2.5`, `3.14159265` → `3.14159`, and the same text under the
`de-DE`, `fr-FR`, and `tr-TR` cultures.

##### Conversion (`TryFromObject`)

Converts a caller-supplied object to a value: `null` → undef; `bool`; every integer and floating-point type and
`decimal` → number (non-finite allowed); `string`; and any non-string `IEnumerable` of supported values →
vector. It returns `false` for any other object, for nesting deeper than 256 levels, and for more than
`MaxConvertedElements` elements in total, which also makes an endless or self-referential enumerable fail
instead of running forever.

##### Stack guard

Every recursive operation (equality, hashing, ordering, formatting, arithmetic, depth computation) calls
`RuntimeHelpers.EnsureSufficientExecutionStack` once per level. At depth 256 the operations need little
stack, but on a very small stack the guard throws `InsufficientExecutionStackException`; this is **the
trigger for the evaluator's restart** on a large-stack thread (see _ScadEvaluator Unit Design_). Measured:
in an unoptimized build, equality, hashing, comparison, and formatting of a depth-256 vector pass on a
256 KiB stack, while arithmetic on depth-256 vectors needs more (the tests pin 1 MiB for it). Arithmetic
never deepens a vector.

#### Error Handling

Operators never throw for wrong operand kinds: they return `undef` and set `typeError`. `TryFromObject` and
the `Try…` text methods report failure through their `bool` result. The payload accessors throw
`InvalidOperationException` for a programming error (reading the wrong kind). The only exception that
escapes an operation by design is `InsufficientExecutionStackException`, which the evaluator handles
(see _Stack guard_). Nothing in the unit throws a `ScadEvaluationException`: limits are enforced by the
callers.

#### Dependencies

- **`ScadParser` syntax types** — `ScadBinaryOperator` and `ScadUnaryOperator` (from the `ScadLanguage`
  subsystem) name the operators; there is no other dependency on the syntax tree.
- **.NET base class library** — `System.Globalization` (invariant-culture formatting), `System.Text`,
  `System.Runtime.CompilerServices.RuntimeHelpers`, and `System.Collections.Generic`.

#### Callers

`ScadEvaluator` calls `Binary`, `Unary`, `Index`, `Member`, `TryCompare`, the text forms, and the
conversion; `ScadBuiltinFunctions` produces and consumes values and the number formatter; and
`ScadDocument` (through `ScadEvaluationOptions`) calls `TryFromObject` to validate and convert the `Variables`
of the evaluation options. The tests in `test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadValue*Tests.cs`,
`ScadNumberFormatterTests.cs`, `ScadRangeTests.cs`, and `ScadTextTests.cs` call the unit directly through
`InternalsVisibleTo`.

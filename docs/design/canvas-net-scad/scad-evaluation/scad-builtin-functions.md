### ScadBuiltinFunctions

<!-- cspell:ignore evaluator undef unseeded SplitMix splitmix libm ULP tangent tangents scalars atan -->
<!-- cspell:ignore parent_module textmetrics fontmetrics -->

The `ScadBuiltinFunctions` unit is the third software unit in the `ScadEvaluation` subsystem. It comprises
nine source files in the `DemaConsulting.CanvasNet.Scad.Evaluation` namespace, all `internal`:

- `IScadBuiltinHost.cs` — the services the evaluator provides to a function (location, limits, warnings,
  unsupported-feature reports, step charging, and the unseeded random stream)
- `ScadBuiltinFunctions.cs` — `ScadBuiltinFunctions` (static partial class): the frozen registry, argument
  binding, and `IsKnown` and `TryInvoke`
- `ScadBuiltinFunctions.Math.cs` — mathematics, trigonometry, `min`, and `max`
- `ScadBuiltinFunctions.Lists.cs` — `len`, `concat`, `norm`, `cross`, and `lookup`
- `ScadBuiltinFunctions.Search.cs` — `search`
- `ScadBuiltinFunctions.Strings.cs` — `str`, `chr`, and `ord`
- `ScadBuiltinFunctions.Types.cs` — the type tests and the version functions
- `ScadBuiltinFunctions.Random.cs` — `rands`
- `ScadRandom.cs` — the SplitMix64 pseudo-random generator

#### Purpose

The unit is the library of built-in functions of the dialect. The evaluator decides **whether** a name refers
to a built-in function (a user function or closure of the same name shadows it) and the unit decides **how**
the function behaves. Built-ins never throw for bad arguments: a wrong argument type, a wrong count, or an
unknown named argument produces a warning and `undef`. The only exceptions are the resource-limit errors
raised through the host. The set of functions, every edge-case result, and the random generator are original
decisions of the CanvasNetScad dialect, pinned by tests; no OpenSCAD source, manual, or test material was
consulted (see _CanvasNetScad System Design_, Clean-Room Provenance).

The unit's behavior is specified by the `CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-*` requirements
(`Math`, `Trigonometry`, `Aggregates`, `Vectors`, `Lookup`, `Search`, `Strings`, `TypeTests`, `Random`,
`UnsupportedFunctions`, `Arguments`, and `Limits`) in
`docs/reqstream/canvas-net-scad/scad-evaluation/scad-builtin-functions.yaml`.

#### Data Model

**Registry.** A `FrozenDictionary<string, Descriptor>` keyed by the case-sensitive, ordinal function name. A
`Descriptor` holds the name, the parameter names accepted for named arguments (in order; empty when the
function takes none), the minimum and maximum argument counts (maximum −1 for any number), and the
implementation delegate. A second frozen set holds the names that are **recognized but not implemented**:
`parent_module`, `textmetrics`, and `fontmetrics`. The registry is immutable and lookup-only (no iteration
order reaches any output), so every member is thread-safe.

**Host (`IScadBuiltinHost`).** `Location` (the location of the call, for warnings and errors),
`MaxStringLength`, `UnseededRandom` (the per-evaluation random stream), `Warn`, `Unsupported`,
`CheckListLength`, `CheckStringLength`, `Charge`, and `Poll` (a cancellation poll that long-running work calls at
bounded, deterministic intervals; matrix products use it). The evaluator implements it in `ScadEvaluator.Host.cs`;
the limit checks raise the same `ScadEvaluationException` as the evaluator's own checks.

**Random generator (`ScadRandom`).** A SplitMix64 generator over a 64-bit state: `NextUInt64` advances the
state by the golden-ratio constant and mixes it; `NextDouble` is `(NextUInt64() >> 11) * 2^-53`, a value in
`[0, 1)`; `FromSeed(double)` starts from the 64 bits of the seed's `BitConverter.DoubleToInt64Bits`; and
`DefaultState` is the fixed initial state of the unseeded stream. The generator is specified bit for bit so
that the sequences are identical on every platform; golden sequences are pinned in the tests.

#### Key Methods

- `IsKnown(name)` — true for an implemented or known-unsupported function; used by the evaluator to decide
  whether to warn about an unknown function.
- `TryInvoke(name, args, host, out result)` — `args` are the already evaluated arguments in call order, each
  with an optional name. Returns `false` **only** for an unknown name (the caller then warns
  `Ignoring unknown function`); returns `true` even when the call warned or produced `undef`.
  A known-unsupported name reports the feature `scad-function-<name>` through the host and yields `undef`.
- **Argument binding.** Positional arguments fill the parameter slots in order; a named argument is placed by
  parameter name and **overrides** a positional value of the same parameter; a skipped parameter becomes
  `undef`. A name that matches no parameter, and any named argument to a function that has no parameter
  names, gives `Ignoring unknown argument 'n' of function 'f'`; surplus positional arguments give
  `Ignoring extra arguments to function 'f'` and are dropped; too few arguments give
  `Function 'f' expects N arguments, got M` and `undef`; a wrong kind gives
  `Function 'f' expects a number, got a string` (and so on) and `undef`. Only `rands` (`min_value`,
  `max_value`, `value_count`, `seed_value`), `search` (`match_value`, `search_vector`, `num_returns_per_match`,
  `index_col_num`), and `lookup` (`key`, `table`) and the two-argument numeric functions `atan2` and `pow`
  accept named arguments.

##### The functions

<!-- markdownlint-disable MD013 -->
| Group | Functions | Behavior |
| ----- | --------- | -------- |
| Mathematics | `abs`, `sign`, `sqrt`, `exp`, `ln`, `log`, `floor`, `ceil`, `round`, `pow` | `log` is **base ten** and `ln` natural; `round` is half away from zero; `sqrt` of a negative number is `NaN`; non-numbers give `undef` and a warning |
| Trigonometry | `sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `atan2` | Angles in **degrees**; exact `0`, `1`, and `-1` at every multiple of 90 degrees (the angle is reduced with `%` first) and an infinity for the tangent at odd multiples of 90; otherwise `System.Math` on radians; the inverse functions return degrees; `atan2` covers all four quadrants |
| Aggregates | `min`, `max` | Several number arguments, or one vector of numbers; empty or other operands give `undef` with a warning; `NaN` propagates |
| Lists | `len`, `concat`, `norm`, `cross` | `len` is the element count of a vector or the scalar count of a string (otherwise `undef` with a warning, also for a range); `concat` flattens **one** level of vector arguments and appends other values, and can never deepen a value; `norm` is the Euclidean length (`norm([])` is 0); `cross` of two 3-vectors is a vector and of two 2-vectors a scalar |
| Lookup | `lookup(key, table)` | Linear interpolation between the nearest keys below and above, with **no** assumption that the table is sorted; clamped to the value of the extreme key outside the table; rows that are not a number pair are skipped; the first of equal keys wins; numeric values only |
| Search | `search(match, source, count, column)` | A match string (per Unicode scalar), number, or vector (per element) against a string, a vector of scalars, or a vector of rows (the column selects the element). `count = 1` gives one index (or an empty list) per match element; `0` gives all indices as a list; `n > 1` gives up to `n`. **Every other form** reports the unsupported feature `scad-search-form` and yields `undef`. The work and the result size are checked against the limits before they are done. **Cost model:** `Charge(1 + match size + source size)` first (a string counts its UTF-16 length, a vector its element count, a number one), computed in constant time before any pass over the arguments; then the exact match and source counts (the scalar count of a string uses the non-allocating `ScadText.ScalarCount`); then `CheckListLength(match count)` before the result array is allocated; then `Charge(max(1, match count × source count))` before the first comparison. The scan itself counts its candidate comparisons (across all match elements) and calls `host.Poll()` every `SearchPollInterval = 65,536` of them, counted and not timed, so a long scan that fits the step budget can still be canceled. The source is read in place: a string is walked by scalar and compared with ordinal string comparison against the match element (a number never equals a string scalar), a vector is read by element or row column, so no list of per-scalar strings exists and a search over 100,000 ASCII characters allocates nothing per scalar. For `0` and `n > 1` the list-length limit is checked for each index as it is collected (the total over all match elements), so a result over the limit stops at once; the default form builds no per-element list |
| Strings | `str`, `chr`, `ord` | `str` concatenates the string forms of its arguments (invariant culture); `chr` converts code points (a number, or a vector of numbers) to a string and warns and skips invalid ones; `ord` gives the first code point of a string (`undef` for an empty or non-string argument). All operate on Unicode scalar values, so `chr` and `ord` round-trip and a surrogate pair is one character |
| Type tests | `is_undef`, `is_num`, `is_bool`, `is_string`, `is_list`, `is_function` | Kind tests (`is_num(nan)` is true) |
| Version | `version`, `version_num` | The emulated language level: `[2021, 1, 0]` and `20210100` |
| Random | `rands(min, max, count, seed_value)` | See below |
<!-- markdownlint-enable MD013 -->

**`rands`.** Returns `count` numbers `min + (max - min) * u`, with `u` from the generator, so each lies in the
half-open range. The count is truncated to a whole number; a negative or NaN count, non-numeric arguments, or a
seed that is neither a number nor `undef` give `undef` with a warning; an infinite or very large count is
checked against `MaxListLength` **before** the list is
allocated. A call **with a seed** starts a fresh generator from that seed at every call, so the same call gives
the same numbers on every platform. A call **without** a seed (or with an `undef` seed) draws from the shared
per-evaluation stream, which starts from the fixed `DefaultState` and advances call after call: the stream is
deterministic and a restarted pass reproduces it exactly. This deliberately differs from "random".

##### Determinism of the transcendental functions

The functions of `System.Math` other than the IEEE-exact ones (`Sqrt`, `Floor`, `Ceiling`) may differ in the
last bit between runtimes and operating systems. The library therefore makes the commonly observable special
cases exact (multiples of 90 degrees, as above) and the tests compare six-digit text or tolerances for other
angles; no fused multiply-add is used.

#### Error Handling

- Bad arguments are **warnings** with a result of `undef` (see _Argument binding_); the warning text names
  the function and the kinds involved.
- Recognized but unimplemented functions and unimplemented `search` forms are **unsupported features**
  (`scad-function-parent_module`, `scad-function-textmetrics`, `scad-function-fontmetrics`,
  `scad-search-form`); the host de-duplicates them by location and may turn the first into an exception.
- **Resource limits** are errors raised through the host: `concat`, `chr`, `str`, `rands`, and `search` check
  the list-length and string-length limits **before** building their result, and every function that does work
  proportional to its input charges `max(1, n)` evaluation steps through `Charge`, so a hostile call cannot
  allocate or compute beyond the limits. The evaluator additionally charges the call expression itself. `str`
  passes the host to the formatter as a meter, so its formatting work is charged per element **while** it formats
  (see _ScadValue_, Text forms) and a step limit or cancellation stops a huge value midway.

#### Dependencies

- **`ScadValue`** — values, number formatting, the Unicode-scalar text helpers, and the text forms.
- **`IScadBuiltinHost`** — implemented by `ScadEvaluator`; it is the only coupling to the evaluator.
- **.NET base class library** — `System.Collections.Frozen`, `System.Math`, `System.BitConverter`, and
  `System.Globalization` (invariant culture).

#### Callers

`ScadEvaluator` is the only production caller: it asks `IsKnown`/`TryInvoke` after user functions have been
ruled out. The tests in `test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadBuiltin*Tests.cs` call the
registry directly through `InternalsVisibleTo`, with the test-only host `ScadTestBuiltinHost`, which records the
warnings, unsupported features, and charged steps and enforces explicit limits.

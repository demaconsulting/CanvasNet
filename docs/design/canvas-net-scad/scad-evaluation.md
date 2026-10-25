## ScadEvaluation

<!-- cspell:ignore evaluator evaluators lexer lexical undef tessellator iteratively reentrant -->
<!-- cspell:ignore backtrace memoized textmetrics fontmetrics multmatrix minkowski -->
<!-- cspell:ignore polyhedron -->

![ScadEvaluation Structure](ScadEvaluationView.svg)

### Overview

The `ScadEvaluation` subsystem (namespace `DemaConsulting.CanvasNet.Scad.Evaluation`, folder
`src/DemaConsulting.CanvasNet.Scad/Evaluation/`) runs a parsed OpenSCAD-language program. It resolves the
`include` and `use` directives of the program, evaluates it with the semantics of the CanvasNetScad dialect,
and produces a **geometry-agnostic instantiation tree** together with buffered echo lines, warnings, and
unsupported-feature reports. It never interprets geometry: a `cube` is a tree node with evaluated arguments,
and turning nodes into solids is the job of the later geometry phase. The subsystem is **internal**: its types
are visible only to the `ScadDocument` unit and, through `InternalsVisibleTo`, to the test assembly, so the
value model and the instantiation tree can evolve while geometry generation is added. The public entry point
is `ScadDocument.Evaluate` (see _ScadDocument Unit Design_, `scad-document.md`). The subsystem contains four
units:

- **ScadValue** — the value model: the `ScadValue` struct, `ScadVector`, `ScadRange`, the number formatter,
  and the Unicode-scalar text helpers; see _ScadValue Unit Design_ (`scad-evaluation/scad-value.md`)
- **ScadEvaluator** — the evaluator, its scopes, the instantiation tree, and the buffering
  `ScadEvaluationSink`; see _ScadEvaluator Unit Design_ (`scad-evaluation/scad-evaluator.md`)
- **ScadBuiltinFunctions** — the library of built-in functions and the deterministic random-number
  generator; see _ScadBuiltinFunctions Unit Design_ (`scad-evaluation/scad-builtin-functions.md`)
- **ScadFileResolver** — the `include`/`use` resolver and its sandbox; see _ScadFileResolver Unit Design_
  (`scad-evaluation/scad-file-resolver.md`)

The subsystem's namespace documentation is in `Evaluation/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetScad-ScadEvaluation-*` requirements in `docs/reqstream/canvas-net-scad/scad-evaluation.yaml`.

### Interfaces

**Exposed** (internal only):

- `ScadEvaluator.Evaluate(ScadDocument, ScadEvaluationSettings, ScadEvaluationSink)` — resolves files (phase
  A, on the calling thread) and then evaluates (phase B); the overload `Evaluate(ScadSourceUnit,
  ScadEvaluationSettings, ScadEvaluationSink)` evaluates an already resolved unit (used by the unit tests).
  It returns a `ScadEvaluationOutput` and throws the exceptions listed under _Event Classification_.
- `ScadEvaluationSettings` — init-only: `Limits`, `Time`, `Overrides` (the converted `-D`-style variables, in
  ordinal key order), `ThrowOnUnsupportedFeature`, and `Cancellation`.
- `ScadEvaluationSink` — the buffer that receives echo lines, warnings, and unsupported-feature reports; it
  keeps them even when evaluation fails and replays them chronologically through `Deliver`.
- `ScadEvaluationOutput` — the roots of the instantiation tree, snapshots of the echo, diagnostics, and
  unsupported features, the instance count, and the step and loop-iteration counts (test hooks).
- The instantiation tree: `ScadInstance`, `ScadInstanceKind`, `ScadInstanceArgument`,
  `ScadFragmentSettings`, and the recognized built-in module names `ScadBuiltinModules`.

**Consumed**: the public `ScadLimits`, `ScadDiagnostic`, `ScadUnsupportedFeature`,
`ScadUnsupportedFeatureException`, `ScadEvaluationException`, `ScadOpenOptions`, `ScadSourceLocation`, and
`ScadSyntaxException` of the `ScadDocument` unit; and, from the `ScadLanguage` subsystem, the immutable syntax
tree (`ScadProgram` and its nodes) and `ScadParser.Parse`, which `ScadFileResolver` calls to parse each
included or used file. The .NET base class library supplies `System.Threading`,
`System.Runtime.CompilerServices`, `System.Runtime.ExceptionServices`, `System.Globalization`, `System.IO`,
`System.Collections.Frozen`, and `System.Collections.Generic`.

**Consumers**: `ScadDocument.Evaluate`, which converts the public options to settings, creates the sink,
calls `ScadEvaluator.Evaluate`, delivers the buffered events to the caller's handlers on the calling thread,
and assembles the public result; and, in a later phase, the geometry builder that converts the instantiation
tree to a `CanvasNetRender3D` scene.

### Design

#### Two Phases: Resolve, Then Evaluate

`include` and `use` paths are literal in the syntax tree and the directives are legal only at file scope, so
the whole file graph can be resolved **before** anything is evaluated.

- **Phase A — `ScadFileResolver`** runs on the calling thread. It is the only code that touches the file
  system or calls the caller's `FileResolver` callback. It enforces the sandbox (including the refusal of any
  symbolic link or junction below a root), the depth, cycle, and file
  count limits, parses each distinct file once, and produces an immutable, include-expanded `ScadSourceUnit`
  for the main document plus the list of `use`d library units.
- **Phase B — `ScadEvaluator`** is pure computation over those immutable units. It records every echo line,
  warning, and unsupported-feature report in the `ScadEvaluationSink` and never calls user code.

Consequences: a missing-file warning appears before any echo output; the user's callback never runs on a
fallback thread; and, because phase B has no observable side effect until the sink is delivered, a pass can be
discarded and re-run on a larger stack without the caller noticing (see _Stack Safety_).

#### Event Classification

Every problem a program can cause falls into exactly one class. The class decides whether evaluation
continues, which type carries the information, and where the caller sees it.

<!-- markdownlint-disable MD013 -->
| Class | Mechanism | Examples | Continues? |
| ----- | --------- | -------- | ---------- |
| **Warning** | `ScadDiagnostic` in `Result.Diagnostics` and the `WarningHandler`; de-duplicated by (message, location); capped at `MaxDiagnostics` (the cap is reported once as `Further diagnostics suppressed (limit N)`) | unknown variable, function, or module (value `undef`, nothing emitted); a variable assigned twice in one scope (reported at the **earlier** assignment); circular variable reference; invalid operand types for an operator or built-in (result `undef`); calling a non-function; wrong, extra, or unknown arguments; `children()` outside a module or with an invalid index; a range with a zero step or non-finite bound; a `for` over `undef`; a missing `include` or `use` file | Yes |
| **Unsupported feature** | `ScadUnsupportedFeature` (feature identifier, message, location) in `Result.UnsupportedFeatures` and the `UnsupportedFeatureHandler`; de-duplicated by (feature, location); with `ThrowOnUnsupportedFeature`, a `ScadUnsupportedFeatureException` at the first one | Phase 5 reports only language-level items: the recognized but unimplemented functions `parent_module`, `textmetrics`, and `fontmetrics` (`scad-function-<name>`) and unimplemented argument forms of `search` (`scad-search-form`) | Yes, with `undef`, unless the caller asked to throw |
| **Error** | `ScadEvaluationException` with the location of the offending construct | a failed `assert`; every limit of `ScadLimits` exceeded; an include cycle; a sandbox violation; an `include` or `use` when the document has no base directory, library path, or resolver | No |
| **Syntax error in a referenced file** | `ScadSyntaxException` whose location carries the included file's name, propagated unchanged | a parse failure of any file loaded in phase A | No |
| **Cancellation** | `OperationCanceledException`, never wrapped | the token is canceled before or during evaluation or loading | No |
| **I/O failure** | the natural `IOException`, and `InvalidDataException` for a file over 4,194,304 characters | an included file that cannot be read | No |
| **Argument error** | `ArgumentException` family, thrown by the public option types when a value is set | an invalid limit, variable name or value, or a non-finite time | n/a |
<!-- markdownlint-enable MD013 -->

The rule of thumb is: a _mistake in the model_ that still allows a meaningful result is a warning; something
this implementation _does not provide_ is an unsupported feature; a problem that makes the result
_meaningless or dangerous_ is an error. Geometry-level unsupported features (`text`, `offset`, `projection`,
and similar) are not detected here: the evaluator emits them as ordinary instances and the later geometry
phase reports them through the same sink.

#### The Instantiation Tree (The Phase 5 / Phase 6 Boundary)

The tree is immutable and independent of the syntax tree and of the scopes. The geometry phase never needs
anything else from evaluation. `ScadEvaluationOutput.Roots` is the list of top-level instances (their implicit
union).

<!-- markdownlint-disable MD013 -->
| Member of `ScadInstance` | Meaning |
| ------------------------ | ------- |
| `Kind` | `BuiltinModule`, or `Group` (the implicit union of its children) |
| `Name` | The built-in module name; for a group, `for`, `intersection_for`, `group`, or the user module's name |
| `Arguments` | The **evaluated** arguments in call order, each with an optional name; empty for a group |
| `Children` | The child instances in order |
| `Modifiers` | Only `ShowOnly`, `Highlight`, and `Background` can be set; a disabled statement is never evaluated and never appears |
| `Fragments` | The effective `$fn`, `$fa`, and `$fs` for the instance: the dynamically scoped values, overridden by a named argument of the same name on the instance |
| `Location` | The identifying token of the statement (the module name or keyword) |
<!-- markdownlint-enable MD013 -->

Shape rules (each is pinned by a test):

- **Built-in module names** come from the fixed set in `ScadBuiltinModules` (`cube`, `sphere`, `cylinder`,
  `polyhedron`, `square`, `circle`, `polygon`, `text`, `translate`, `rotate`, `scale`, `mirror`, `multmatrix`,
  `resize`, `color`, `union`, `difference`, `intersection`, `linear_extrude`, `rotate_extrude`, `hull`,
  `minkowski`, `offset`, `projection`, `render`, `surface`, `import`, and `roof`). The evaluator evaluates
  their arguments and children but **never interprets them**: it does not validate argument names, and
  `import("x.stl")` performs no file access.
- **Lookup order** for `name(arguments) children`: the `children` pseudo-module, then a user module (innermost
  scope outward), then a built-in module, otherwise a warning `Ignoring unknown module 'name'` with nothing
  emitted and the arguments and children **not evaluated**. A user module shadows a built-in of the same name.
- A **user module call** produces one `Group` named after the module, always, even when the body produces
  nothing, so positional semantics such as the first child of `difference` are preserved.
- **`for`** produces one `Group("for")`, always, even with zero iterations. **`intersection_for`** produces
  `BuiltinModule("intersection")` whose children are one `Group("intersection_for")` per iteration.
- **Splicing constructs** — `if`, a braced block, `let`, `echo`, `assert`, and `children()` — place their result
  instances directly into the parent's child list and have no node of their own.
- **Modifiers**: `*` skips the statement entirely (no evaluation, no side effects, no budget use); `!`, `#`,
  and `%` are OR-ed onto the single instance the statement produced, or onto a `Group("group")` that wraps
  several. The evaluator does not interpret `!`.
- An **`each` statement** is an unknown module (it is only meaningful inside a vector).
- Every instance, groups included, counts against `MaxInstances`; `ScadEvaluationOutput.InstanceCount` is
  that count.

**Warning for consumers of the tree.** The depth of the tree is **not** bounded by the 256-level nesting limit
of the syntax tree: recursion of user modules nests groups, and the depth can reach about one third of
`MaxRecursionDepth` (thousands of levels at the default, more than five thousand at the ceiling). Any code
that walks the tree — the geometry builder, a dumper, equality, printing — must therefore traverse it
**iteratively** (with an explicit stack) or guard each recursive step with
`RuntimeHelpers.EnsureSufficientExecutionStack` and a bounded restart as described under _Stack Safety_. The
test helper that dumps trees is iterative for exactly this reason: a recursive dumper overflowed a 256 KiB
test thread on a 5,000-level tree.

#### Stack Safety (Deterministic Recursion Limits)

The evaluator recurses over the syntax tree and over user-defined function and module calls, so a hostile
program could overflow the call stack, which cannot be caught and terminates the host process. The design
mirrors the `ScadParser` mechanism (see _ScadParser Unit Design_), and an outcome that depends on the stack of
the calling thread is not acceptable:

1. A **frame counter** defines the nominal limit. It is incremented on entry to every evaluation frame and
   compared with `ScadLimits.MaxRecursionDepth` (16 to 16,384; default 5,000). The counter, never the
   stack, decides whether a program is over the limit, so the same program succeeds or fails identically, with
   the same message and location, on every platform and stack size.
2. The pass runs **inline on the caller's thread**, guarded by `RuntimeHelpers.EnsureSufficientExecutionStack`
   at every frame (and inside the recursive value operations).
3. Only when that guard reports a short stack (`InsufficientExecutionStackException`, a property of the caller)
   is the pass **discarded and restarted** on a dedicated thread named `ScadEvaluator` with a **384 MiB**
   stack. The number of such threads running at once is bounded by a static `SemaphoreSlim` (`Max(2,
   ProcessorCount)`) that callers wait on **with their cancellation token**, so a queued caller can always be
   abandoned, exceptions are rethrown on the caller with `ExceptionDispatchInfo`, and the caller's
   culture and UI culture are applied to the thread.
4. The restart is invisible because phase B only fills the buffering sink, which is reset before the restart;
   the warnings recorded during phase A are re-recorded after the reset, the caller's resolver callback is not
   called again, and the unseeded random stream starts from the same fixed state in every pass.

The stack size was derived by measurement and is documented in _ScadEvaluator Unit Design_ (_Measured Stack
Figures_); the worst case at the ceiling of 16,384 frames needs about 37 MiB in an unoptimized build, so the
384 MiB reservation (address space committed only as touched) has a margin of about ten times, and the
per-frame cost stays far below the 8 KiB at which the ceiling would have to be lowered. The reservation is
at most `max(2, ProcessorCount)` threads times 384 MiB (768 MiB on one or two cores, 24 GiB on 64 cores) of address
space; measured, a fallback thread commits about 0.1 MiB for an ordinary program and about 20 MiB at the deepest
legal recursion. It is meant for 64-bit hosts and is not supported in a 32-bit process or under strict overcommit
or a tight virtual-memory limit (see _ScadEvaluator Unit Design_, _Memory reservation and cost_).

#### Resource Limits

<!-- markdownlint-disable MD013 -->
| Limit (`ScadLimits`) | Default | Enforced where | Failure |
| -------------------- | ------- | -------------- | ------- |
| `MaxRecursionDepth` | 5,000 (16 to 16,384) | every evaluation frame | `Recursion too deep (limit N)` |
| `MaxEvaluationSteps` | 50,000,000 | every expression, statement, loop turn, and tail-call turn; vector work, matrix-product work (result elements plus multiply-adds / 16), `str`/`echo` formatting (8 per element, while formatting), string indexing (by position), and long scope walks charge more | `Evaluation step limit exceeded (limit N)` |
| `MaxLoopIterations` | 10,000,000 | every loop turn, plus an up-front check of a range's count (total across the whole evaluation) | `Loop iteration limit exceeded (limit N)` |
| `MaxListLength` | 1,000,000 | every vector append, `concat`, `rands`, comprehension; also the result elements of a matrix product (checked before it is computed) | `List too long (limit N)` |
| `MaxStringLength` | 1,048,576 | `str`, `chr`, echo formatting | `String too long (limit N)` |
| value nesting (constant) | 256 | vector literal, comprehension, and `concat` construction | `List nesting too deep (limit 256)` |
| `MaxIncludeDepth` | 16 (1 to 64) | the resolver | `Include nesting too deep (limit N)` |
| `MaxIncludedFiles` | 1,000 | the resolver (every expansion and each distinct library) | `Included file limit exceeded (limit N)` |
| `MaxInstances` | 1,000,000 | every instance created; also the number of statements include expansion may produce (phase A, before any step accounting exists) | `Instance limit exceeded (limit N)`, or `Included statement limit exceeded (limit N)` during expansion |
| `MaxOutputLength` | 4,194,304 | the echo sink (cumulative characters after the prefix) | `Echo output limit exceeded (limit N)` |
| `MaxDiagnostics` | 1,000 | the warning sink | not an error: collapsed into one notice |
| cancellation | — | every 1,024 steps (so inside `str`/`echo` formatting), every 65,536 multiply-adds of a matrix product, every loop turn, every file load, entry | `OperationCanceledException` |
<!-- markdownlint-enable MD013 -->

#### Determinism

The same document with the same options gives bit-identical echo, diagnostics, and tree on every platform,
in every culture, on every thread, and across a restart. The mechanisms are: the invariant culture and ordinal
comparers everywhere (analyzers CA1305, CA1307, and CA1309 enforce this); an own number formatter instead of
`double.ToString`; an own PRNG (SplitMix64) with a fixed unseeded state; no iteration order of any hash
container reaching the output (overrides are applied in ordinal key order and frozen registries are
lookup-only); IEEE-exact operations (`%`, `Floor`, `Ceiling`, `Sqrt`) and no fused multiply-add; and results
that never depend on `Environment.ProcessorCount` (only the number of fallback threads does). The **text of every
diagnostic** is culture-free too: each message that interpolates a number (every limit message, the built-in
argument-count warnings, the parser and lexer messages, the range message of `ScadLimits`) is built with
`string.Create(CultureInfo.InvariantCulture, $"…")`, as `ScadSourceLocation` already does. The analyzers do not flag
an interpolated string, so this is held by an audit of every `$"…"` site and by `ScadEvaluatorCultureTests`. For
non-negative integers .NET output does not vary between the standard cultures, so that test is a regression guard
(messages identical under several cultures and under a synthetic culture with another negative sign and native
digits); invariance itself is by construction. The
transcendental functions of `System.Math` can differ in the last bit between runtimes, so trigonometry is exact
at multiples of 90 degrees and the tests compare six-digit text or use tolerances elsewhere.

#### Clean-Room Provenance

The subsystem is an original implementation. No OpenSCAD source code, grammar, test corpus, manual prose,
manual example, or library was read, copied, ported, or paraphrased, and none was consulted to decide a
detail. The semantics in this document and its unit documents — the number format, the scoping and
special-variable rules, the `children()` rules, include and use, overrides, and the limits — are the
**CanvasNetScad dialect's own decisions**, taken by general language-design reasoning and pinned by in-repo
tests. The golden fixtures (`ScadFixtures/eval-*.scad` and their `.txt` expectation files) were authored fresh
for this repository, and their `README.md` records their provenance. Exact parity with any other
implementation is an explicit non-goal. The review sets of the system check this statement (see
_CanvasNetScad System Design_, `../canvas-net-scad.md`, Clean-Room Provenance).

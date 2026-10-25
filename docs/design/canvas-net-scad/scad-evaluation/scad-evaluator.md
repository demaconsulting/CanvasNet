### ScadEvaluator

<!-- cspell:ignore evaluator evaluators undef unseeded lexically memoized hoisted hoisting preorder reentrancy -->
<!-- cspell:ignore fmod ULP bisect bisection unoptimized tessellation multmatrix polyhedron minkowski -->

The `ScadEvaluator` unit is the second software unit in the `ScadEvaluation` subsystem. It comprises fourteen
source files in the `DemaConsulting.CanvasNet.Scad.Evaluation` namespace, all `internal`:

- `ScadEvaluator.cs` — `ScadEvaluator` (sealed partial class): the entry points, the restart mechanics, and
  the frame, step, loop, and cancellation accounting
- `ScadEvaluator.Scopes.cs` — the root scope, scope construction, hoisting, lazy cells, and variable lookup
- `ScadEvaluator.Expressions.cs` — expression evaluation, operators, indexing, and ranges
- `ScadEvaluator.Calls.cs` — argument binding, user function and closure calls, and the tail-call loop
- `ScadEvaluator.Statements.cs` — statement execution, instance creation, `echo`, and `assert`
- `ScadEvaluator.Modules.cs` — user and built-in module instantiation and `children()`
- `ScadEvaluator.Comprehensions.cs` — list comprehensions and iteration
- `ScadEvaluator.Host.cs` — the `IScadBuiltinHost` implementation the evaluator gives to the built-in functions
- `ScadScope.cs` — `ScadScope`, `ScadCell`, `ScadSpecialFrame`, `ScadModuleBinding`, `ScadChildren`, and the
  closure type `ScadClosure`
- `ScadInstance.cs` — `ScadInstance`, `ScadInstanceKind`, `ScadInstanceArgument`, and `ScadFragmentSettings`
- `ScadBuiltinModules.cs` — the recognized built-in module names
- `ScadEvaluationSettings.cs` — the evaluation settings
- `ScadEvaluationOutput.cs` — the result of one evaluation
- `ScadEvaluationSink.cs` — the buffer for echo lines, warnings, and unsupported-feature reports

#### Purpose

`ScadEvaluator` turns a resolved program into the instantiation tree and the buffered echo, warnings, and
unsupported-feature reports, or throws an evaluation error. It implements the **normative semantics of the
CanvasNetScad dialect** described below, which are original decisions pinned by in-repo tests: no OpenSCAD
source, grammar, manual text, or test material was consulted, and parity with any other implementation is a
non-goal (see _CanvasNetScad System Design_, Clean-Room Provenance). It never interprets geometry, never
touches the file system, and never calls user code; it is pure computation over immutable inputs.

The unit's behavior is specified by the `CanvasNetScad-ScadEvaluation-ScadEvaluator-*` requirements
(`Scoping`, `LazyCells`, `SpecialVariables`, `Functions`, `Closures`, `TailCalls`, `Modules`, `Children`,
`ControlFlow`, `Ranges`, `Comprehensions`, `Operators`, `Echo`, `Assert`, `Imports`, `InstanceTree`,
`Modifiers`, `FragmentSettings`, `Overrides`, `Warnings`, `DiagnosticCap`, `UnsupportedFeatures`,
`ErrorLocations`, `RecursionLimit`, `StepLimit`, `LoopLimit`, `ListAndStringLimits`, `InstanceLimit`,
`OutputLimit`, `Cancellation`, `StackSafety`, `BufferedDelivery`, and `Determinism`) in
`docs/reqstream/canvas-net-scad/scad-evaluation/scad-evaluator.yaml`.

#### Data Model

**Evaluator state.** `ScadEvaluator` is a single-use, non-thread-safe instance holding the include-expanded
main `ScadSourceUnit`, the `ScadEvaluationSettings`, the `ScadLimits`, the `ScadEvaluationSink`, the current
frame depth, and the counters for steps, loop iterations, and instances, plus the unseeded random generator
(a fresh `ScadRandom` in its fixed initial state for every pass). The static `Evaluate` entry points are safe
for concurrent use because each call creates its own instance; the only shared state is the thread-safe
fallback gate and the test-hook counters.

**Scope (`ScadScope`).** A scope has a lexical parent, three namespaces (variable cells, function
definitions, module definitions), the list of lazy cells in source order, and a pointer to the head of the
dynamic `$` chain that applies inside it. A **cell (`ScadCell`)** is either lazy (an expression plus the scope
it is evaluated in) or known; it has a state (`Pending`, `Evaluating`, `Done`), a value, the location of its
binding, and a `Superseded` flag set when a later assignment in the same scope replaced it.

**Special frames (`ScadSpecialFrame`).** The `$` chain is a persistent linked list of frames, one per scope
that binds at least one name beginning with `$`. A scope that binds none reuses its dynamic parent's pointer,
so no allocation occurs and a long tail-recursive loop does not grow the chain. Each frame knows its depth in
the chain.

**Children (`ScadChildren`).** The child statements of a user module call, held unevaluated as a list of
**slots** plus the scope in which they are later evaluated.

**Closure (`ScadClosure`)** implements `IScadFunctionValue`: the name (or none for a literal), the parameters,
the result expression, the scope the function closes over, and the definition location.

**Instance (`ScadInstance`).** The immutable tree node described in _ScadEvaluation Subsystem Design_
(`scad-evaluation.md`, _The Instantiation Tree_): kind, name, evaluated arguments (with optional names),
children, modifiers, fragment settings, and location.

**Settings and output.** `ScadEvaluationSettings` carries `Limits`, `Time`, `Overrides` (a dictionary of
name to value; a name beginning with `$` becomes a special binding), `ThrowOnUnsupportedFeature`, and
`Cancellation`, with safe defaults. `ScadEvaluationOutput` carries the root instances, snapshots of the sink,
the instance count, and the step and loop-iteration counts.

**Sink (`ScadEvaluationSink`).** Buffers the echo lines (checking the cumulative output limit), the warnings
(de-duplicated by message and location, capped at `MaxDiagnostics` real warnings **plus** one
`Further diagnostics suppressed (limit N)` notice located at the first dropped warning, and bounded in memory
however many distinct warnings are produced), and the unsupported-feature reports (de-duplicated by feature and
location; `Unsupported` returns whether the report was new so the evaluator can throw on the first one).
`Reset` clears everything for a restart, and `Deliver` replays all three kinds **chronologically** (not
category by category) to the supplied handlers; a null handler is skipped and a handler exception propagates.

**Limit and hook constants.** `EvaluatorStackSize` (384 MiB), `FallbackThreadName` (`ScadEvaluator`),
`MaxConcurrentFallbackThreads` (`Math.Max(2, Environment.ProcessorCount)`) enforced by a static
`SemaphoreSlim`, the cancellation poll interval (1,024 steps), and the test hooks `LastRunThreadId` (per
calling thread: the managed thread that completed that thread's latest evaluation), `FallbackCount`, and
`PeakConcurrentFallbackThreads`.

#### Key Methods

**Entry points.**

- `Evaluate(ScadDocument, ScadEvaluationSettings, ScadEvaluationSink)` — throws `ArgumentNullException` for a
  null argument, honors an already canceled token before anything else (so no resolver call happens), runs
  phase A with a `ScadFileResolver` over the document's `Options`, `Program`, and `SourceName`, and then calls
  the unit overload.
- `Evaluate(ScadSourceUnit, ScadEvaluationSettings, ScadEvaluationSink)` — snapshots the warnings recorded so
  far (phase A's), runs the pass **inline**, and, only when the pass throws
  `InsufficientExecutionStackException`, resets the sink, re-records the snapshot, and runs the pass again on
  the large-stack thread (`RunOnEvaluatorThread`).
- `RunPass(stackShortageIsRecursionError)` — builds the root scope, executes the file's statements, and
  assembles the output. On the large-stack thread a tripped guard is reported as the recursion-limit error
  (defense in depth), located at the last frame entered.
- `EnterFrame`, `LeaveFrame`, `Charge`, `ChargeLoopTurn`, `CheckLoopBudget`, `ChargeHops` — the accounting
  (see _Accounting_).

##### Normative Semantics: Scoping

_Scopes_ are the file root, a module body, a function call, a braced block or child list, an `if` branch,
a `let` (statement and expression), a `for` iteration, and a comprehension element. Names live in three
namespaces per scope: variables, functions, and modules.

**Building a scope** (one pass over its statements): create a cell for every `name = expression;`. If the name
already has a cell **in this scope**, the earlier cell is dropped and a warning
`'name' is assigned more than once in this scope; the last assignment is used` is recorded **at the earlier
assignment**; duplicate functions and modules are handled alike (`Function 'f' is defined more than once…`,
`Module 'm' is defined more than once…`, last definition wins). Definitions are **hoisted**: they are visible
throughout the scope wherever they are written, which makes the rules position independent. The root layer
below the file scope defines `PI` and the special variables `$fn = 0`, `$fa = 12`, `$fs = 2`, `$t` (the
`Time` setting), and `$preview = true`.

**Variable lookup** walks from the innermost scope outward; the first scope that defines the name wins. The
same variable may be assigned in different scopes without a warning (the inner one shadows the outer one).

**Function lookup** for a call with an identifier callee goes innermost-outward; within one scope a function
definition beats a variable that holds a function value; if no scope defines the name the built-in functions
are tried; otherwise the warning `Ignoring unknown function 'f'` is recorded and the result is `undef`. A user
function shadows a built-in of the same name; a variable holding a non-function never hides a built-in. A
callee that is not an identifier (`(function(x) x)(1)`, `fs[0](2)`) is evaluated; a non-function result gives
the warning `Cannot call a value that is not a function` and `undef`.

**Lazy cells.** A cell's expression is evaluated **in its defining scope**, at most once, and memoized. All
cells of a scope are _forced in source order when the scope is entered_ (so an unreferenced assignment still
runs its `echo` and `assert` effects in source order, and a forward reference simply forces the cell it
refers to first). Statements — instantiations, `echo`, `assert`, and so on — execute **after** the scope's
cells have been forced. Reading a cell that is in the `Evaluating` state is a circular reference: the warning
`Circular reference while evaluating 'x'` is recorded and the read yields `undef` (so `a = a + 1;` gives
`undef`, and so do mutually dependent variables).

**Sequential bindings.** `let` (statement and expression), `for` iterator lists, the C-style `for`
initializer, and comprehension `let` bind **sequentially and are not hoisted**: each value is evaluated in a
scope in which the earlier bindings of the same list are visible; a repeated name shadows the earlier one. For
memory economy a `let` or initializer list binds in **one** scope, so a closure created inside a `let` can see
later bindings of the same list (a documented, rarely visible divergence).

##### Normative Semantics: Special Variables (`$` Dynamic Scoping)

A name beginning with `$` is a special variable with **dynamic scope**:

- A block, `let`, `for`, or comprehension scope's dynamic parent is its lexical parent's chain.
- A **user function or module call** scope has the **definition scope** as its lexical parent but the
  **calling scope's chain** as its dynamic parent; a closure call likewise (lexical parent is the captured
  scope).
- Lookup of `$name` walks the chain from the evaluating scope; the first frame that binds the name wins (its
  cell is forced); then the root defaults; otherwise the warning `Ignoring unknown variable '$name'` gives
  `undef`.
- A **named argument whose name starts with `$`** always binds in the callee's special frame, whether or not
  it is a declared parameter. A `$` parameter's default is evaluated in the call scope like any default.
- `$children` (modules only) is the number of child **slots** of the call (see _Children_).
- A tail call that binds no `$` name does not grow the chain; a recursion that binds a `$` name at every level
  adds one frame per level, so the chain depth is capped by `MaxRecursionDepth` and such a recursion fails with
  the recursion error (also for `$` tail recursion).

##### Normative Semantics: Functions, Modules, And Argument Binding

Arguments are evaluated **eagerly, left to right, in the caller's scope**, and then bound:

1. Create the call scope (lexical parent: the definition scope; dynamic parent: the caller's chain).
2. Positional arguments bind to the parameters in order; extra positional arguments give the warning
   `Ignoring extra argument for function 'f'` (or module, or function literal).
3. A named argument binds by parameter name and **overrides** a positional binding of the same parameter; an
   unknown non-`$` name gives the warning `Ignoring unknown argument 'z' for module 'm'`.
4. The parameters are added to the call scope **one at a time, in declaration order**: the value is the
   supplied argument, otherwise the default expression **evaluated in the call scope as built so far** (so a
   default sees the earlier parameters, and falls through to the definition scope for anything else), otherwise
   `undef`. A later parameter is invisible to an earlier default.
5. A module body is then built, hoisted, and forced as a block scope that is a child of the parameter scope.

A **user module call** produces one `Group` named after the module, containing the instances the body produced
(always emitted, even with zero children). A module or function body may contain definitions, which are local
to it. A function literal creates a closure over the current scope; recursion through a named variable works
because lookup is lazy.

**Unknown module**: the warning `Ignoring unknown module 'm'` is recorded, nothing is emitted, and **neither
the arguments nor the children are evaluated**. An unknown function does not evaluate its arguments either.

##### Normative Semantics: Children

`children()` is a pseudo-module (it cannot be user-defined) that places the child statements of the enclosing
module call. The children of a call are **captured, not evaluated**, as **slots**: the braced child list with
nested braced blocks flattened recursively; every other statement — a module call, `for`, `if`, `let`,
`echo`, and so on — is **one slot**, however many instances it later produces. A disabled (`*`) child still
counts as a slot. The assignments and definitions in the child list are bound in a scope that extends the
**call-site scope** and are visible to the slots (they are not slots themselves).

- `children()` places all slots in order; `children(i)` (positional, or `index=`) one slot; a vector its
  listed slots in order; a range its integer elements.
- Each selected slot executes in a fresh block scope whose **lexical parent is the call-site scope** (extended by
  the child list's own assignments and definitions) and whose **dynamic `$` chain** is the `$` bindings made by
  the **child list itself** on top of the chain in effect where `children()` executes. Precisely: (a) special
  variables set inside the module before `children()` are visible to the children; (b) `$` variables assigned in
  the child list are visible to the children whatever the module does — `m() { $x = 4; echo($x); }` behaves like
  `{ $x = 4; echo($x); }`, because the child list is the lexically innermost block — and they **shadow** a
  same-named binding of the module, while the module body itself never sees them; (c) the list's `$` cells are
  forced once, when the call's children are captured, under the call site's chain, so `$b = $a * 3` reads the
  call site's `$a`; (d) `$` bindings made outside the child list (in the block that contains the call) keep the
  old behavior: the module's executing chain wins; (e) the list's frame is copied in front of the executing
  chain each time a slot runs (the cells themselves are shared, not re-evaluated), and the copy is bounded by
  the recursion limit like every other binding of a special variable (`Recursion too deep (limit N)`).
- A non-integer, negative, or too large index gives the warning `Ignoring invalid child index` and places
  nothing for that index. `children()` outside a module gives `children() used outside a module`.
- Children can be forwarded through a nested module (`children()` inside a call whose own children list
  contains `children()`).

##### Normative Semantics: Control Flow, Ranges, Comprehensions

- **`if (c) A else B`** selects by truthiness; the chosen statement runs in its own block scope; the
  instances splice into the parent.
- **`for (i = X, j = Y) body`** is equivalent to nested loops with the first iterator outermost; later
  iterators see earlier ones. Iterating **X**: a vector yields its elements; a range its generated elements; a
  number, boolean, string, or function yields exactly **one** iteration with that value; `undef` yields zero
  iterations and the warning `Cannot iterate over undef`. Each iteration has its own scope for the iterator
  variables and a child block scope for the body, so body assignments are per iteration. The result is one
  `Group("for")`, always.
- **Ranges** `[a : s : b]` (written order; the step defaults to 1): a zero step or a non-finite part gives
  the warning `Ignoring a range with a zero step or a non-finite bound` and no iterations; `s > 0` requires
  `a <= b` and `s < 0` requires `a >= b`, otherwise the range is empty **without** a diagnostic; the count is
  `floor((b - a) / s + 1e-9) + 1` and element `i` is `a + i * s`. A range with a non-numeric part evaluates to
  `undef` with the warning `Range bounds and step must be numbers`. The range count is checked against the
  **remaining loop budget before the first iteration**, so `[0 : 1e12]` never loops: it fails at once with the
  loop-iteration error. Indexing a range gives `undef`, and `len` of a range is `undef` with a warning.
- **`intersection_for`** and the `let`, `echo`, and `assert` statements place their children as the tree rules
  describe.
- **List comprehensions** — `[for (…) …]`, the C-style `for (init; condition; update)`, `if`/`else`, `let`,
  and `each` — append zero or more values per element, left to right, and may nest. `each v` splices a
  vector's elements or a range's elements and appends any other value (including `undef`) itself. In the
  C-style form, each turn evaluates the condition (by truthiness), then the body, then **all update
  assignments simultaneously from the old state**; closures created in a turn keep that turn's values. The
  length is checked on every append; an `each` over a range too large for the remaining budget is rejected up
  front; a comprehension element used outside a vector gives a warning.
- **Expressions**: `&&` and `||` short-circuit; invalid operands give `undef` plus the warning
  `Cannot apply operator '+' to a number and a string` (the kinds named with `KindDescription`); `undef`
  operands propagate `undef` and also warn; division by zero follows IEEE without a warning; `echo`, `let`, and
  `assert` also exist as expressions.

##### Normative Semantics: Tail Calls

A call of a **user function or closure in tail position** — the branch of a `?:`, or the body of a `let`,
`echo`, or `assert` expression — runs in a loop: the callee's arguments are bound in the current scope and
the loop continues with the callee's body, **adding no frame**. Steps are still charged on every turn, so
`f(n) = f(n + 1)` ends with the step-limit error, never with a stack error, and a non-tail recursion is still
bounded by the frame counter. Built-in calls, operators, and comprehension bodies are not tail positions, and
a call used as an operand (`1 + f(n - 1)`) is an ordinary recursive call.

##### Normative Semantics: Imports (`include` And `use`)

The resolver (phase A) has already replaced each `include` by the included statements, keeping their own
locations. The evaluator therefore treats included statements as if they were written in the including file.
For `use`, the evaluator builds one root scope per library unit; the importing scope receives the library's
**own** top-level module and function definitions (including those expanded from the library's includes) as
closures over the library's scope — **not** the libraries that library itself uses (those stay private to it, but its
functions can call them). A library's top-level geometry, `echo`, and `assert` statements are **never
executed**; its top-level assignments are lazy cells, evaluated only when one of its functions or modules
refers to them. A definition in the importing file wins over an imported one, and among several `use`s the
**later** wins. Libraries are not subject to variable overrides. `$` variables bound by a caller are visible
to a library's modules through the dynamic chain.

##### Normative Semantics: Variable Overrides (`-D` Style)

`ScadEvaluationSettings.Overrides` is applied while the **main** unit's root scope is built: an override for
`N` **replaces every top-level assignment to `N`** in the main document — including statements expanded from
includes — **without evaluating their right-hand sides**, and creates the cell when no assignment exists; a
`$` name creates a special binding at the root, replacing the default. Assignments that depend on `N` see the
override. Overrides are applied in ordinal key order, and an override is not applied to a `use`d library or to
assignments nested in modules or blocks.

##### Accounting: Frames, Steps, Loops, Instances

**Frames.** Entering a frame costs one count of `MaxRecursionDepth`; leaving it gives the count back. A frame
is entered for: each expression node, each statement, each comprehension element, each `for` iterator level,
each `children()` expansion, and each user-module body. A user **function call adds no frame of its own** (its
body is an expression, which does), and **tail calls and built-in calls add none**.

<!-- markdownlint-disable MD013 -->
| Shape | Cost |
| ----- | ---- |
| `f(n) = n == 0 ? 0 : 1 + f(n - 1)` (non-tail) | 2 frames per level plus 4 at the bottom, so a limit of 104 admits depth 50 and rejects 51; the default 5,000 admits 2,498 levels |
| user module recursion | 3 frames per level plus 5 |
| `children()` chains and nested blocks | 5 frames per level |
| tail recursion of a user function | constant |
<!-- markdownlint-enable MD013 -->

These formulas are pinned by tests in `ScadEvaluatorStackTests`, `ScadEvaluatorTailCallTests`, and
`ScadEvaluatorLimitTests`.

**Steps.** Every expression node, statement, loop turn, and tail-call turn charges **one step**; an `echo`,
`let`, or other construct that does extra work charges for it. Operations on vectors charge the **deep
element count** (so shared nested vectors cannot dodge the limit; counting stops once the remaining budget is
exceeded). A **matrix product** additionally charges its result elements plus its multiply-add work divided by
16, rounded up, before it computes anything (see _ScadValue_, Products and limits). `str`, `echo`, and the
assert message charge `8` steps per vector element **while** they format (a string element one more per 16
characters), and indexing a string charges `position / 512` steps for the scan and, when the prefix has a
surrogate, `position / 16` more for the walk (see _ScadValue_, Text forms and Indexing). Scope and special-chain walks
of 32 hops or more charge `hops / 32` extra steps. Built-in functions charge their own O(n) work through the host.
Every element a vector operation creates is charged at least one step before it is allocated, so live elements
are bounded by `MaxEvaluationSteps` as well as by the per-list `MaxListLength`; there is no separate
total-element limit. When the total exceeds `MaxEvaluationSteps` the error
`Evaluation step limit exceeded (limit N)` is raised at the last frame's location (at the `*` operator for a
matrix product).

**Loop iterations** are counted across the whole evaluation (every `for` turn and every comprehension loop
turn), checked against `MaxLoopIterations`, and each turn also polls cancellation. **Instances** are counted at
creation. **Cancellation** is polled whenever the step count crosses a multiple of 1,024, at every loop turn,
at every file load, at entry, and (through the host's `Poll`) every 65,536 multiply-adds inside a matrix
product.

##### Stack Safety: Restart On A Large Stack

See _ScadEvaluation Subsystem Design_ (`scad-evaluation.md`, _Stack Safety_) for the design. In summary,
`Evaluate` holds no state between calls; the nominal recursion limit is a **counter**; the
pass runs inline guarded by `RuntimeHelpers.EnsureSufficientExecutionStack`; and only
`InsufficientExecutionStackException` (a property of the caller's stack, never of the nominal limit) causes the
bounded restart on the `ScadEvaluator` thread. `RunOnEvaluatorThread` is a line-for-line mirror of the parser's
`RunOnParserThread`: it waits for a slot of the static gate, starts a background thread with
`EvaluatorStackSize`, joins it, releases the slot in `finally`, and rethrows a captured exception with
`ExceptionDispatchInfo`; the caller's `CurrentCulture` and `CurrentUICulture` are applied to the thread.

_Measured stack figures._ For each worst-case shape at the ceiling `MaxRecursionDepth = 16,384`, the smallest
**caller** stack on which the whole pass completes inline with the runtime guard active was found by
bisection (a hop is detected through `LastRunThreadId`). The figures include the 64 to 128 KiB the guard
reserves:

<!-- markdownlint-disable MD013 -->
| Shape at 16,384 frames | Debug minimum | Debug per frame | Release minimum | Release per frame |
| ---------------------- | ------------- | --------------- | --------------- | ----------------- |
| function, closure, and mutual recursion; `let`, `echo`, and `assert` chains; ternary chains | about 37.4 MiB | about 2,390 bytes | about 17.1 MiB | about 1,090 bytes |
| nested expressions inside recursion (100 binary operators per level) | 28.8 MiB | 1,840 bytes | 14.6 MiB | 930 bytes |
| vector literals inside recursion | 25.8 MiB | 1,650 bytes | 11.5 MiB | 730 bytes |
| module recursion, blocks, `children()` chains | 11.4 to 12.7 MiB | 730 to 810 bytes | 6.2 to 6.9 MiB | 390 to 440 bytes |
| comprehensions (value nesting bounds them at 255 levels) | 1.7 MiB | — | 0.9 MiB | — |
<!-- markdownlint-enable MD013 -->

The default of 5,000 frames needs about 12.6 MiB (Debug) for function recursion. A split of expression
evaluation into a small dispatcher and per-node methods reduced the worst case by 9 percent (from about
2.63 to 2.39 KiB per frame). The worst case is far below the 8 KiB per frame at which the ceiling would have
to be lowered, so the ceiling stays 16,384. The plan's first estimate of 128 MiB was too small for the
required margin of at least eight times, so `EvaluatorStackSize` is **384 MiB**: about ten times the Debug
worst case and about twenty-two times the Release worst case, which also absorbs larger frames on other
architectures and runtimes. The memory is reserved address space committed only as it is touched, and the
gate bounds the reservation of a burst of concurrent small-stack callers.

**Phase A warnings survive a restart**: the snapshot of the sink's warnings taken before the pass (for
example `Can't open include file` from file loading) is re-recorded after the reset, a behavior pinned by a
test. The unseeded `rands` stream starts from the same fixed state in every pass, so a restarted pass is
bit-identical to an uninterrupted one.

#### Error Handling

The classification of events into warnings, unsupported features, errors, cancellation, and I/O failures is in
_ScadEvaluation Subsystem Design_ (`scad-evaluation.md`, _Event Classification_). In this unit:

- A **warning** is recorded through `Warn`, which forwards to the sink; evaluation continues with `undef` or
  nothing. Locations: an unknown function at the identifier; call-site problems at the `(` token (the parser's
  call location); instances at the module-name token; the reassignment warning at the earlier assignment.
  Message texts are listed with the semantics above and pinned by tests.
- An **error** is a `ScadEvaluationException` created by `Fail(description, location)`: a failed `assert`
  (`Assertion failed`, followed by `:` and the message when one was given), every limit
  (`Recursion too deep (limit N)`, `Evaluation step limit exceeded (limit N)`,
  `Loop iteration limit exceeded (limit N)`, `List too long (limit N)`, `String too long (limit N)`,
  `List nesting too deep (limit 256)`, `Instance limit exceeded (limit N)`, `Echo output limit exceeded
  (limit N)`), and the errors of the resolver. The recursion error is located at the offending node on the
  nominal path (identical on every stack size, as tested) and at the last frame entered on the defense-in-depth
  path.
- An **unsupported feature** is recorded with `sink.Unsupported`; when `ThrowOnUnsupportedFeature` is set the
  first _new_ report throws `ScadUnsupportedFeatureException`.
- **Cancellation** throws `OperationCanceledException` from `Charge`, `ChargeLoopTurn`, and the entry, and is
  never wrapped. The sink holds the events recorded before any failure.
- `InsufficientExecutionStackException` is caught **only** at the entry point; no `try`/`finally` is needed
  around frames because any exception aborts the whole single-use evaluation.

#### Dependencies

- **`ScadValue`** — values, operators, text forms, and conversion (see _ScadValue Unit Design_).
- **`ScadBuiltinFunctions`** — `TryInvoke` and `IsKnown`, through the `IScadBuiltinHost` that
  `ScadEvaluator.Host.cs` implements (see _ScadBuiltinFunctions Unit Design_).
- **`ScadFileResolver`** and **`ScadSourceUnit`** — phase A (see _ScadFileResolver Unit Design_).
- **`ScadLanguage` subsystem** — the syntax tree (`ScadProgram`, statements, expressions, `ScadModifiers`,
  and the operator enumerations).
- **`ScadDocument` unit** — `ScadLimits`, `ScadDiagnostic`, `ScadUnsupportedFeature`,
  `ScadEvaluationException`, `ScadUnsupportedFeatureException`, `ScadSourceLocation`, and the document's
  internal `Program`, `Options`, and `SourceName`.
- **.NET base class library** — `System.Runtime.CompilerServices.RuntimeHelpers`,
  `InsufficientExecutionStackException`, `System.Threading` (`Thread`, `SemaphoreSlim`, `Interlocked`,
  `CancellationToken`), `System.Runtime.ExceptionServices.ExceptionDispatchInfo`,
  `System.Globalization.CultureInfo`, and `System.Collections.Frozen`.

#### Callers

`ScadDocument.Evaluate` is the only production caller: it builds `ScadEvaluationSettings` from the public
options (converting the `Variables`), creates the `ScadEvaluationSink`, calls `ScadEvaluator.Evaluate`, and
delivers the sink on the calling thread in both the success and the failure path. The tests in
`test/DemaConsulting.CanvasNet.Scad.Tests/Evaluation/ScadEvaluator*Tests.cs` and
`ScadEvaluationSinkTests.cs` call the entry points directly through `InternalsVisibleTo`, using
`ScadEvaluatorTestSupport` (including the iterative `ScadInstanceDumper`, which renders the tree as location-free
text) to compare trees and `OnSmallStack` to run an evaluation on a caller thread of a chosen stack size.

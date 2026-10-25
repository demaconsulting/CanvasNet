# System Design

<!-- cspell:ignore lexer lexing tokenization tokenizes tokenized tokenize parsers recursive-descent -->
<!-- cspell:ignore nupkg unpackable evaluator evaluators polyhedra Minkowski undef reproducibly -->
<!-- cspell:ignore textmetrics fontmetrics -->

This document provides the system-level design for CanvasNetScad.

![CanvasNetScad Structure](CanvasNetScadView.svg)

## Architecture

CanvasNetScad is a clean-room front end for the OpenSCAD language, delivered incrementally as its own
NuGet package (`DemaConsulting.CanvasNet.Scad`, root namespace `DemaConsulting.CanvasNet.Scad`),
independent of the other document systems in this repository. It reads OpenSCAD-language source text
and, in the current phase (Phase 5), can both **parse** it — validating the source against a documented
grammar and reporting what the source declares at file scope, or the first syntax error with its file,
line, and column — and **evaluate** it: `ScadDocument.Evaluate` runs the program (values, scoping,
functions, modules and their children, control flow, comprehensions, built-in functions, `include` and
`use` resolution inside a sandbox, variable overrides, resource limits, and cancellation) and returns the
`echo` output, the warnings, the unsupported-feature reports, and the number of module instances produced.
Evaluation builds an internal, geometry-agnostic instantiation tree but **generates no geometry and renders
nothing**; those capabilities are the subject of a later phase (see the _Implementation Phase Plan_ below).
The project is not yet packable: its `IsPackable` property is `false` until Phase 6 delivers the first
user-visible rendering capability, so that no mid-roadmap release publishes a package without it.

`CanvasNetScad` consists of one direct unit and two subsystems:

- **ScadDocument** (unit; namespace `DemaConsulting.CanvasNet.Scad`, folder
  `src/DemaConsulting.CanvasNet.Scad/`): the public API. `ScadDocument` offers the entry points
  `Parse(string)`, `Open(string)` (a file path), and `Open(Stream)`, exposes `SourceName`, `Directives`,
  `ModuleNames`, and `FunctionNames`, and offers `Evaluate`. The supporting public types are
  `ScadOpenOptions` (immutable, validated open options), `ScadSourceLocation` (file, line, column),
  `ScadSyntaxException` (the parse error type, a `FormatException`), `ScadDirective`, `ScadDirectiveKind`,
  and the evaluation contract: `ScadEvaluationOptions`, `ScadEvaluationResult`, `ScadLimits`,
  `ScadDiagnostic`, `ScadUnsupportedFeature`, `ScadUnsupportedFeatureException`, and
  `ScadEvaluationException`. See _ScadDocument Unit Design_ (`canvas-net-scad/scad-document.md`).
- **ScadLanguage** (internal subsystem; namespace `DemaConsulting.CanvasNet.Scad.Language`, folder
  `Language/`): the language front end, with two units. `ScadLexer` converts source text into a token
  list (the internal `ScadLexer`, `ScadToken`, `ScadTokenKind`). `ScadParser` converts the tokens into
  an immutable syntax tree (the internal `ScadParser`, the syntax-tree records, and `ScadTreeDepth`,
  which verifies the finished tree's height). The subsystem has no public types: the syntax tree is
  deliberately internal so that it can evolve while geometry generation is added. See
  _ScadLanguage Subsystem Design_ (`canvas-net-scad/scad-language.md`).
- **ScadEvaluation** (internal subsystem; namespace `DemaConsulting.CanvasNet.Scad.Evaluation`, folder
  `Evaluation/`): the evaluator, with four units. `ScadValue` is the value model. `ScadEvaluator`
  evaluates a resolved program into the instantiation tree. `ScadBuiltinFunctions` is the built-in function
  library with a platform-independent random generator. `ScadFileResolver` resolves `include` and `use`
  inside a sandbox before evaluation. The subsystem has no public types of its own: the value model and the
  instantiation tree stay internal so that they can evolve with the geometry phase. See _ScadEvaluation
  Subsystem Design_ (`canvas-net-scad/scad-evaluation.md`).

Dependencies inside the system are one-directional: `ScadDocument` calls `ScadParser`, which calls
`ScadLexer`, and `ScadEvaluator`, which calls `ScadFileResolver` (which calls `ScadParser` for each included
file), `ScadValue`, and `ScadBuiltinFunctions`; the `ScadLanguage` subsystem never calls back into
`ScadDocument` or `ScadEvaluation` (it uses only the shared `ScadSourceLocation`, `ScadSyntaxException`, and
`ScadDirectiveKind` value types), and `ScadEvaluation` depends on `ScadLanguage`, never the reverse.

## External Interfaces

The system exposes the following public API in the `DemaConsulting.CanvasNet.Scad` namespace:

- **`ScadDocument`** (sealed, immutable, thread-safe, not `IDisposable`): the static
  `Parse(string, ScadOpenOptions?)`, `Open(string, ScadOpenOptions?)`, and
  `Open(Stream, ScadOpenOptions?)` entry points; the `SourceName`, `Directives`, `ModuleNames`, and
  `FunctionNames` properties; and `Evaluate(ScadEvaluationOptions?, CancellationToken)`, which returns a
  `ScadEvaluationResult`.
- **`ScadOpenOptions`** (sealed): init-only `BaseDirectory`, `LibraryPaths`, `FileResolver`, and
  `SourceName`. Parsing consumes only `SourceName`; the other three govern `include` and `use`
  resolution when the document is evaluated.
- **`ScadEvaluationOptions`** (sealed): init-only `Variables` (name-ordered `-D`-style overrides), `Time`
  (the `$t` special variable), `Limits`, the handlers `EchoHandler`, `WarningHandler`, and
  `UnsupportedFeatureHandler`, and `ThrowOnUnsupportedFeature`.
- **`ScadEvaluationResult`** (sealed): read-only `Echo` (full lines including the `ECHO:` prefix),
  `Diagnostics`, `UnsupportedFeatures`, and `InstanceCount` (module instances, groups included).
- **`ScadLimits`** (sealed, init-only): `MaxRecursionDepth`, `MaxEvaluationSteps`, `MaxLoopIterations`,
  `MaxListLength`, `MaxStringLength`, `MaxIncludeDepth`, `MaxIncludedFiles`, `MaxInstances`,
  `MaxOutputLength`, and `MaxDiagnostics`, with the `Default` instance.
- **`ScadDiagnostic`** and **`ScadUnsupportedFeature`** (sealed records): a warning and an
  unsupported-feature report, each with its location.
- **`ScadSourceLocation`** (readonly record struct): `File`, one-based `Line`, and one-based `Column`;
  `default` (line zero) means "unknown".
- **`ScadSyntaxException`** (sealed, derives from `FormatException`): carries the `Location` of the
  first syntax error; the message is prefixed with the location when it is known.
- **`ScadEvaluationException`** (sealed, derives from `InvalidOperationException`) and
  **`ScadUnsupportedFeatureException`** (sealed, derives from `IOException`): carry the `Location` (and the
  `Feature`) of an evaluation failure or of an unsupported feature when the caller asked to throw.
- **`ScadDirective`** (sealed record) and **`ScadDirectiveKind`** (`Include`, `Use`): one recorded
  file-scope directive.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| --------- | --------- | ------ | ----------- |
| `ScadDocument.Parse(string, ...)` | Inbound | OpenSCAD-language source text | Non-null; at most 4,194,304 UTF-16 characters; empty, whitespace-only, and comment-only text is a valid empty document; a leading U+FEFF is ignored |
| `ScadDocument.Open(string, ...)` | Inbound | File path | Non-null and not empty or whitespace; read as UTF-8 with byte-order-mark detection; missing file or directory surfaces the natural .NET exception |
| `ScadDocument.Open(Stream, ...)` | Inbound | Readable stream | Non-null and readable; read from the current position as UTF-8; left open; reading stops one 8 KiB chunk past the size limit |
| `ScadOpenOptions` properties | Inbound | Text, list, delegate | Each value validated when initialized (text null or non-whitespace; library paths a non-null list without null or whitespace entries, copied); instance therefore always valid |
| `ScadDocument` properties | Outbound | Names, read-only lists | `Directives`, `ModuleNames`, and `FunctionNames` are file-scope only, in source order, read-only |
| `ScadDocument.Evaluate(...)` | Inbound and outbound | `ScadEvaluationOptions`, a `CancellationToken`; a `ScadEvaluationResult` | Synchronous; thread-safe on a shared document; options validated when set; handlers run on the calling thread after evaluation; the file resolver callback runs only on the calling thread |
| `ScadEvaluationOptions.Variables` | Inbound | Name to value dictionary | Names match an optional `$` then ASCII letters, digits, and underscores (not starting with a digit); values are null, booleans, numeric types, strings, or non-string sequences of these (nested at most 256 levels, at most 1,000,000 elements in total); invalid entries throw `ArgumentException` when set |
| `ScadOpenOptions.FileResolver` | Inbound | `Func<string, Stream?>` | Receives only normalized logical paths, only on the calling thread; a null result means "not found"; the stream is disposed after reading |
| `ScadEvaluationResult` | Outbound | Read-only lists and a count | Cannot be turned into mutable collections |
| `ScadSyntaxException` | Outbound | Exception with `Location` | Thrown for the first lexical or grammatical error, for nesting deeper than 256 levels, and for a syntax error in a file loaded by `include` or `use` |
| `ScadEvaluationException` | Outbound | Exception with `Location` | Thrown for a failed assertion, an exceeded limit, an include cycle or sandbox violation, and a directive with no place to look |
| `ScadUnsupportedFeatureException` | Outbound | Exception with `Feature` and `Location` | Thrown only when `ThrowOnUnsupportedFeature` is set |
| `OperationCanceledException` | Outbound | Exception | Thrown, never wrapped, when the token is canceled before or during evaluation |
| `InvalidDataException` | Outbound | Exception | Thrown, not as a `ScadSyntaxException`, when the source or an included file exceeds 4,194,304 characters (no location is meaningful) |
<!-- markdownlint-enable MD013 -->

Parsing never opens any file named by an `include` or `use` directive: directives are recorded only. Files
are accessed only by `Evaluate`, only through the open options, and only inside the sandbox described in
_Risk Control Measures_. The types of the `ScadLanguage` and `ScadEvaluation` subsystems are internal and
visible only to the test assembly through `InternalsVisibleTo`. See the unit and subsystem design documents
for every type's complete constructor, property, and exception detail.

## Dependencies

`CanvasNetScad` references the separate `CanvasNet` system and the `CanvasNetRender3D` system through
project references in
`src/DemaConsulting.CanvasNet.Scad/DemaConsulting.CanvasNet.Scad.csproj`:

- `CanvasNet` (its own package, `DemaConsulting.CanvasNet`), for the future `Surface`/`Rgba32` output
- `CanvasNetRender3D` (its own package, `DemaConsulting.CanvasNet.Render3D`), whose `Scene` the later
  phases will build from evaluated geometry

**In Phases 4 and 5 no type from either system is used by any code.** The references are architectural
scaffolding added now so that the project graph does not have to be restructured when Phase 6
renders a document through `CanvasNetRender3D`; they are modeled in the SysML2 model as the
`usesCanvas` and `usesRender3D` dependencies. Consequently the Phase 4 and Phase 5 behavior is independent
of both systems.

**`CanvasNetScad` must never reference `DemaConsulting.CanvasNet.Svg`, `DemaConsulting.CanvasNet.Pdf`,
`DemaConsulting.CanvasNet.Pptx`, `DemaConsulting.CanvasNet.Vsdx`, or `DemaConsulting.CanvasNet.Charts`.**
It is a source-language front end for 3D models, unrelated to those document-format systems.

Beyond these project references, `CanvasNetScad` uses only the .NET base class library: `System.IO`
(`StreamReader`, `File`, `Path`), `System.Text` (`StringBuilder`, `Encoding`), `System.Globalization`
(invariant-culture number parsing and formatting), `System.Collections.ObjectModel`
(`ReadOnlyCollection<T>`), `System.Collections.Frozen` (the immutable registries),
`System.Threading` (`Thread`, `SemaphoreSlim`, `Interlocked`, `CancellationToken`),
`System.Runtime.ExceptionServices` (`ExceptionDispatchInfo`, for the bounded restart on a large stack), and
`System.Runtime.CompilerServices` (the stack-space guard). These are
platform types available on every target framework, not OTS Software Items, and require no runtime
NuGet dependency; the project references exactly the same build-time analyzers and tooling packages
as its sibling projects.

This is an ordinary, same-repository, system-to-system dependency: it is neither an OTS Software
Item nor a Shared Package (that category is scoped to a package produced by a _different_
repository). `CanvasNetScad` introduces no new OTS Software Item — see _OTS Integration Design_
(`docs/design/ots.md`).

## Risk Control Measures

`CanvasNetScad` has no segregation requirement for safety. Because OpenSCAD-language source may come
from an untrusted origin, its risk control measures are defensive-correctness measures against
invalid and hostile input:

- **Fail-closed validation.** Every entry point and every `ScadOpenOptions` property validates its
  argument eagerly and throws a specific exception (`ArgumentNullException`, `ArgumentException`)
  rather than coercing it; options are immutable once constructed, so the parser trusts every
  instance it receives.
- **Fail-fast syntax errors.** The first lexical or grammatical error aborts parsing with one
  `ScadSyntaxException` carrying its location. There is no error recovery and no partial syntax tree,
  so a consumer can never observe a half-parsed program.
- **Bounded recursion.** Nesting deeper than 256 levels is rejected with a `ScadSyntaxException`.
  The limit is enforced twice: by a recursion counter while parsing (backed by
  `RuntimeHelpers.EnsureSufficientExecutionStack`, whose failure is converted to the same error), and
  by an iterative post-parse check of the finished tree's height (`ScadTreeDepth`). The second check
  is necessary because left-associative operator chains and postfix chains are built in loops: a
  hostile input can build a very deep, left-leaning tree without any parser recursion, which would
  overflow the stack of a later recursive consumer. Every later consumer of the tree (an evaluator, a
  dumper, record equality, record printing) may therefore assume a tree depth of at most 256. A stack
  overflow cannot be caught and would terminate the host process.
- **Bounded input size.** Source longer than 4,194,304 UTF-16 characters is rejected with an
  `InvalidDataException`. `Open(Stream)` reads in chunks and stops one chunk past the limit, so a
  stream that never ends cannot consume unbounded memory. The size of the syntax tree is
  proportional to the number of tokens, so this also bounds tree memory.
- **No file or network access while parsing.** Parsing reads only the source it is given. `include` and
  `use` paths are recorded as text and never opened, and the file resolver and base directory in
  `ScadOpenOptions` are not consumed by parsing. No code in the source is ever executed.
- **Deterministic, culture-independent behavior.** Numbers are parsed and locations formatted with the
  invariant culture, so results do not depend on the host's locale.

Evaluation runs untrusted programs, so it adds the following measures (details in _ScadEvaluation Subsystem
Design_, `canvas-net-scad/scad-evaluation.md`):

- **Resource limits, fail closed.** Recursion depth, evaluation steps, loop iterations, list length, string
  length, value nesting (256), include depth, included files, instance count, echo output length, and the number
  of kept diagnostics are each bounded by a limit in `ScadLimits` that the caller can set (a limit is validated
  when set and enforced at its exact value). Exceeding one raises a `ScadEvaluationException` located at the
  offending construct. A range too large for the remaining loop budget is rejected before its first
  iteration, and every built-in function that can grow its output checks the limits before building it. A matrix
  product is bounded the same way: its result must fit the list-length limit and its multiply-add work is charged
  against the step budget before it is computed.
- **Deterministic recursion limit and stack safety.** The recursion limit is counted (a frame counter), never
  measured, so a program succeeds or fails identically on every platform and stack size. The pass runs inline
  and, only when the runtime stack guard reports that the caller's stack is too small, is discarded and
  restarted on a bounded number of dedicated large-stack threads (384 MiB reserved, committed only as touched),
  exactly as the parser does. The restart is invisible because evaluation has no observable side effect until
  the buffered events are delivered.
- **File-access sandbox.** `include` and `use` are resolved before evaluation, on the calling thread, and only
  through the open options: files under the base directory or a library path, or, when a resolver callback is
  supplied, **only** that callback with no file-system access at all. Environment variables are never
  consulted. Absolute paths, drive letters, colons, control characters, and `..` above the root are rejected
  with an evaluation error (and, in resolver mode, never reach the callback); a file that is, or lies beneath, a
  symbolic link or junction below its root is refused whatever the link's target (the root itself is trusted;
  to use linked content, add the real directory as a library path); include cycles, nesting depth, the number of
  inclusions, and the
  number of statements include expansion may produce are bounded. A document with `include` or `use` but no
  base directory, library path, or resolver is an error rather than a search of the current directory.
- **Cancellation.** A `CancellationToken` is honored before any work (and before any resolver call), between
  files, every 1,024 evaluation steps, every 65,536 multiply-adds of a matrix product, and at every loop turn; the
  exception is never wrapped, so even a program with the step limit raised to the maximum can be abandoned.
- **Buffered, ordered event delivery.** Echo lines, warnings, and unsupported-feature reports are buffered
  while evaluating and delivered to the caller's handlers afterwards, in order, on the calling thread only
  (also when evaluation fails; a handler exception on a failure path cannot hide the failure). User code
  therefore never runs inside the evaluator or on a fallback thread.
- **Bounded diagnostics.** Warnings are de-duplicated by message and location and capped at `MaxDiagnostics`
  (the excess collapses into one notice), so a warning inside a large loop cannot exhaust memory.
- **Validated overrides.** `ScadEvaluationOptions.Variables` entries are validated and converted when the
  options are initialized (names, kinds, nesting depth 256, at most 1,000,000 elements, and endless or
  self-referential sequences rejected) and sequences are enumerated exactly once into a snapshot.

## Data Flow

**Parse path (`ScadDocument.Parse`, `Open(string)`, `Open(Stream)`):**

1. **Validate** the arguments and options (null source, null or whitespace path, unreadable stream).
   `Open(string)` fixes the effective source name (the path as supplied, unless overridden) and the
   effective base directory (the folder of the path's full name, unless overridden).
2. **Read**: `Open` reads the file or stream as UTF-8 in chunks, detecting and removing a byte-order
   mark, and fails with `InvalidDataException` as soon as the running length exceeds the limit.
   `Parse` checks the length of the supplied string.
3. **Tokenize**: `ScadLexer.Tokenize` converts the whole source into a token list ending with an
   end-of-file token, skipping white space and comments and tracking one-based line and column
   positions. Any lexical error aborts here, so a lexical error anywhere in the file is reported
   before any grammatical error, even one earlier in the file (a documented consequence of
   tokenizing before parsing, pinned by a test).
4. **Parse**: `ScadParser` runs recursive descent for statements and precedence climbing for
   expressions over the token list and builds the immutable syntax tree, counting nesting depth as it
   goes.
5. **Verify**: `ScadTreeDepth.Verify` iteratively checks the height of the finished tree.
6. **Extract**: `ScadDocument` reads the file-scope directives, module names, and function names
   from the tree once, as read-only lists.
7. **Output**: an immutable `ScadDocument`, safe to share between threads.

**Evaluate path (`ScadDocument.Evaluate`):**

1. **Check the token**: an already canceled token throws before anything else.
2. **Prepare**: the public options become internal settings (the `Variables` were converted when the options
   were initialized) and an empty buffering sink is created.
3. **Phase A — resolve** (on the calling thread): `ScadFileResolver` expands every `include` in place, loads
   every `use`d file as a library unit, enforces the sandbox and the include limits, and parses each distinct
   file once with `ScadParser`.
4. **Phase B — evaluate**: `ScadEvaluator` builds the root scope (the defaults, the imports of the libraries,
   the file's own hoisted definitions and assignments, and the variable overrides), forces the variable cells
   in source order, executes the statements, and builds the instantiation tree, recording echo lines,
   warnings, and unsupported-feature reports in the sink. If the caller's stack runs short, the pass is
   discarded and repeated on a large-stack thread.
5. **Deliver**: the sink replays its events to the caller's handlers in order on the calling thread (on a
   failure, too, with a handler exception on that path ignored so that the failure wins), and the public
   `ScadEvaluationResult` is assembled from the sink and the instance count.
6. **Output**: the result, or the first error as a typed exception.

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **Dependency direction**: `CanvasNetScad` must never reference `CanvasNetSvg`, `CanvasNetPdf`,
  `CanvasNetPptx`, `CanvasNetVsdx`, or `CanvasNetCharts` (see Dependencies above)
- **Determinism**: The same source always yields the same tree or the same error at the same location,
  on every platform and in every culture. Likewise the same document with the same evaluation options
  always yields the same echo output, diagnostics, and instance count, on every platform, in every culture,
  on every thread, on any stack size, and across a restart on a larger stack; there is no wall-clock or
  environment dependence, and the random-number generator is specified bit for bit
- **Number formatting contract**: A number is rendered (by `str` and `echo`) with at most six significant
  digits, trailing zeros removed, fixed notation for a decimal exponent from -4 to 5 and scientific notation
  (`1e+06`, `1.5e-07`) otherwise, `nan`, `inf`, `-inf`, and `-0` for the special values, using only
  invariant-culture characters; the exact algorithm is in _ScadValue Unit Design_
- **Name-agnostic parsing**: The parser does not know any built-in module or function. Every
  `name(arguments) children` statement is a module instantiation, so an unknown or unsupported name
  never produces a syntax error; resolving names is the job of the evaluation subsystem
- **Geometry-agnostic evaluation**: The evaluator recognizes the names of built-in modules but never
  interprets them; it emits a tree of instances with evaluated arguments for a later phase to convert to
  geometry, and that later phase reports geometry-level unsupported features through the same channel
- **Event classification**: A mistake in a model that still allows a meaningful result is a warning, a
  feature this implementation does not provide is an unsupported-feature report, and a problem that makes
  the result meaningless or dangerous is an error (see _ScadEvaluation Subsystem Design_)
- **Additive evolution**: The public types are sealed, options are init-only, results are read-only
  lists, and records and enumerations tolerate added members, so later phases extend the API without
  breaking it

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it references,
enabling compatibility across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs. No platform-specific native interop, OS-specific APIs, or framework-version-specific features
are used. Locations count UTF-16 code units and treat `\r\n`, `\n`, and a lone `\r` each as one line
break, so results are identical on every platform regardless of the checked-out line-ending style of
the source.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution, enabled in Phase 6
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
- **Front-end role**: A later phase converts the instantiation tree of an evaluated `ScadDocument` into a
  `CanvasNetRender3D` `Scene` and renders it; neither the parser nor the evaluator ever calls into the
  renderer, and the renderer has no knowledge of the OpenSCAD language

### Clean-Room Provenance

OpenSCAD is licensed under the GPL and its manual under CC BY-SA. `CanvasNetScad` is an
**original, clean-room implementation**, written from general knowledge of C-family language design
and the approved roadmap for this repository. No OpenSCAD source code, grammar or lexer definition,
test corpus, manual prose, manual example, or library (and no parser, grammar, or syntax-highlighter
derived from any of these) was read, copied, ported, or paraphrased, and none was consulted to decide
a detail. Where the roadmap was silent, a decision was made by general language-design reasoning and
recorded in this document. This holds for the **evaluator** as well as the parser: the scoping rules,
the special-variable rules, the `children()` rules, the number format, the built-in functions and their edge
cases, `include` and `use` resolution, variable overrides, and the limits are the **dialect's own
decisions**, written down in the design documents and pinned by in-repo tests, and not a reproduction of the
behavior of any other implementation. The test fixtures
(`test/DemaConsulting.CanvasNet.Scad.Tests/ScadFixtures/`) — including the evaluation fixtures
`eval-*.scad` and their golden `.txt` expectation files (echo, diagnostics, unsupported features, and
instance trees) — are original works authored for this repository and licensed with it; their `README.md`
records their provenance. Exact parity with the behavior of any existing implementation is an explicit
non-goal.

The provenance statement is repeated in the namespace documentation, the project description, the
fixture README, and the purpose of each unit design, and every `CanvasNetScad` review-set checks it
(see `.reviewmark.yaml`). Any future contribution to this system shall observe the same rule.

### Supported And Deferred Features

<!-- markdownlint-disable MD013 -->
| Feature | Status | Notes |
| ------- | ------ | ----- |
| Lexical forms: numbers, strings with escapes, identifiers, special variables, comments, directives | Supported (Phase 4) | See _ScadLexer Unit Design_ |
| Assignments, module and function definitions, module instantiations with positional and named arguments | Supported (Phase 4) | The parser does not know built-in names |
| Modifier characters (disable, show-only, highlight, background) | Supported (Phase 4) | On instantiation-like statements and blocks |
| `if`/`else`, `for`, `intersection_for`, `let`, `echo`, `assert` (statements and expressions) | Supported (Phase 4) | |
| Expressions: literals, vectors, ranges, postfix index/member/call, function literals, conditional, operators | Supported (Phase 4) | Precedence table in _ScadParser Unit Design_ |
| List comprehensions: `for`, C-style `for`, `if`/`else`, `let`, `each` | Supported (Phase 4) | Valid only inside a vector |
| `include`/`use` directives | Recorded (Phase 4); resolved on evaluation (Phase 5) | File-scope only; never opened while parsing |
| `ScadOpenOptions.BaseDirectory`, `LibraryPaths`, `FileResolver` | Supported (Phase 5) | Govern `include`/`use` resolution; the sandbox is described in _Risk Control Measures_ |
| Expression evaluation, variable scoping, special-variable semantics | Supported (Phase 5) | Dialect semantics in _ScadEvaluator Unit Design_ |
| Built-in functions: mathematics, trigonometry in degrees, `len`, `concat`, `norm`, `cross`, `lookup`, `search` (documented forms), `str`, `chr`, `ord`, type tests, `version`, `rands` | Supported (Phase 5) | See _ScadBuiltinFunctions Unit Design_ |
| `parent_module`, `textmetrics`, `fontmetrics`, and unimplemented `search` argument forms | Reported as unsupported features (Phase 5) | Feature ids `scad-function-<name>` and `scad-search-form` |
| Module evaluation into an instantiation tree, `children()`, user functions, closures, tail calls | Supported (Phase 5) | The tree is internal and geometry-agnostic |
| `include`/`use` file resolution, file-access sandbox, library paths, resolver callback | Supported (Phase 5) | Environment variables are never consulted |
| Variable overrides (`-D` style), special-variable overrides, animation time | Supported (Phase 5) | `ScadEvaluationOptions.Variables` and `Time` |
| Resource limits, cancellation, deterministic recursion limit | Supported (Phase 5) | `ScadLimits`, `CancellationToken` |
| Primitive solids and 2D shapes, transforms, boolean operations, extrusions | Deferred (Phase 6) | Mapped onto `CanvasNetRender3D`; the evaluator already emits them as instances |
| Public render entry point, render options, geometry-level unsupported-feature reporting | Deferred (Phase 6) | Adds `Render`, `ScadRenderOptions`, `MaxTriangles` and the geometry diagnostics |
| Packaging as a NuGet package, README and user guide | Deferred (Phase 6) | `IsPackable` is `false` until then |
| Hull and Minkowski operations | Deferred (Phase 7) | Depends on `CanvasNetRender3D` support |
| Text and external-file import | Deferred (later) | `text` and `import` are emitted as instances and reported by the geometry phase |
| Multiple error reporting and error recovery | Not planned | Additive later, for example as an errors list |
<!-- markdownlint-enable MD013 -->

### Deliberate Strictness And Deviations

Because the implementation is clean-room, the accepted language is the documented grammar of this
design, not a verified clone of any other implementation. Each point below is either a superset of,
or stricter than, what a user might expect; relaxing any of them later is additive. Each is pinned by
a test.

1. Identifiers are ASCII only and cannot start with a digit (`2cm` lexes as a number then an
   identifier and is then a syntax error).
2. An unknown escape in a string is an error. `\x` takes exactly two hexadecimal digits, `\u` four,
   and `\U` six; surrogate code points and code points above U+10FFFF are rejected; `\x00` and
   `\u0000` are accepted.
3. `include` and `use` are accepted only at file scope and must be followed by an angle-bracketed
   path on the same line; both words are reserved.
4. An assignment or a module or function definition is not accepted as an unbraced child statement
   ("Use braces around a declaration").
5. A C-style `for(init; condition; increment)` is accepted only inside a list comprehension; in
   statement position it is an error.
6. A modifier is accepted only in front of an instantiation-like statement or a block.
7. The power operator binds tighter than unary minus and is right-associative (so `-2^2` is
   `-(2^2)` and `2^3^2` is `2^(3^2)`); relational and equality operators are two separate levels, as
   in C-family languages.
8. A trailing comma is accepted in vectors, parameter lists, argument lists, `let` assignment lists,
   and iterator lists.
9. Parsing is fail-fast, reports a single error, and reports lexical errors before grammatical errors.
10. Nesting is limited to 256 levels and the source length to 4,194,304 characters.

The evaluation phase adds the following dialect decisions, each pinned by a test and each described in the
unit design documents:

 1. Variable assignment is position independent and **the last assignment in a scope wins** (with a warning at
    the earlier one); variables are lazy, memoized cells that are forced in source order, and a circular
    reference is `undef` with a warning.
 2. Special variables (`$name`) are dynamically scoped through calls and `children()`; a `$` assignment made in a
    child list is visible to that list's children and shadows a same-named binding of the module.
 3. Truthiness: `undef`, `false`, `0`, `-0`, the empty string, and the empty vector are false; everything
    else, including `NaN`, ranges, and functions, is true. Equality never coerces between kinds
    (`true == 1` is false).
 4. A `for` over a single value (a number, string, boolean, or function) iterates once; over `undef`, zero
    times with a warning.
 5. Trigonometry works in degrees and is exact at multiples of 90 degrees; `log` is base ten; `rands`
    without a seed is deterministic per evaluation, not random.
 6. Unknown variables, functions, and modules are warnings (with `undef` or no instance), not errors.
 7. Indexing out of range or with a non-integer yields `undef` (a fractional index is truncated); strings
    are indexed and measured by Unicode scalar value.
 8. `use` imports only definitions; a library's own top-level geometry, `echo`, and `assert` never run.
 9. Environment variables, including search-path variables used by other implementations, are ignored; a
    document with `include` or `use` and no base directory, library path, or resolver is an error.

### Determinism And Immutability

Every public result type is immutable. A `ScadDocument` and its internal syntax tree can be shared
between threads, and parsing the same text any number of times, on any thread, in any culture, yields
equal trees and identical errors. `ScadDocument.Evaluate` holds no state between calls, so a document may be
evaluated from several threads at once, and each call gives the same result for the same options.

## Implementation Phase Plan

`CanvasNetScad` is delivered incrementally; each phase is a separate pull request that adds
requirements, design, verification, and tests for its own scope.

1. **Phase 4 — Scaffolding, lexer, parser (delivered).** The project and test project, the
   `ScadLanguage` subsystem (`ScadLexer`, `ScadParser`), the parse-only public `ScadDocument` surface,
   original fixtures with a provenance README, and the complete companion documentation. (The phase
   number continues the numbering of the repository's 3D roadmap, whose first three phases delivered
   `CanvasNetRender3D`.)
2. **Phase 5 — Evaluation (delivered).** The `ScadEvaluation` subsystem (`ScadValue`, `ScadEvaluator`,
   `ScadBuiltinFunctions`, `ScadFileResolver`): values, scoping, functions, modules and children, control
   flow, comprehensions, built-in functions, special variables, `include`/`use` resolution governed by
   `ScadOpenOptions` inside a sandbox, variable overrides, deterministic stack-independent recursion limits,
   resource limits and cancellation, and a geometry-agnostic instantiation tree; the public surface is
   `ScadDocument.Evaluate` with `ScadEvaluationOptions`, `ScadEvaluationResult`, `ScadLimits`,
   `ScadDiagnostic`, `ScadUnsupportedFeature`, `ScadUnsupportedFeatureException`, and
   `ScadEvaluationException`; original golden evaluation fixtures; and the complete companion documentation.
3. **Phase 6 — Geometry and rendering (planned).** Mapping the instantiation tree onto `CanvasNetRender3D`
   scenes, a public render entry point (`BuildScene` and `Render`) and `ScadRenderOptions`, which composes
   or copies the evaluation options, the additive `MaxTriangles` limit and the geometry-level
   unsupported-feature reports, the flip of `IsPackable`, and the README and user guide. The geometry builder
   must traverse the instantiation tree iteratively (see _ScadEvaluation Subsystem Design_).
4. **Phase 7 — Hull and Minkowski (planned)**, depending on `CanvasNetRender3D` support.
5. **Later — Text and external-file import.**

Every later phase adds members and types rather than changing the signatures delivered here.

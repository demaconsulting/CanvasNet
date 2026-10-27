# System Design

<!-- cspell:ignore lexer lexing tokenization tokenizes tokenized tokenize parsers recursive-descent -->
<!-- cspell:ignore nupkg unpackable evaluator evaluators polyhedra Minkowski undef reproducibly -->
<!-- cspell:ignore textmetrics fontmetrics halign -->
<!-- cspell:ignore multmatrix tessellators -->

This document provides the system-level design for CanvasNetScad.

![CanvasNetScad Structure](CanvasNetScadView.svg)

## Architecture

CanvasNetScad is a clean-room front end for the OpenSCAD language, delivered incrementally as its own
NuGet package (`DemaConsulting.CanvasNet.Scad`, root namespace `DemaConsulting.CanvasNet.Scad`),
independent of the other document systems in this repository. It reads OpenSCAD-language source text
and, in the current phase (Phase 6), can **parse** it — validating the source against a documented
grammar and reporting what the source declares at file scope, or the first syntax error with its file,
line, and column — **evaluate** it: `ScadDocument.Evaluate` runs the program (values, scoping,
functions, modules and their children, control flow, comprehensions, built-in functions, `include` and
`use` resolution inside a sandbox, variable overrides, resource limits, and cancellation) and returns the
`echo` output, the warnings, the unsupported-feature reports, and the number of module instances produced —
**build its geometry**: `ScadDocument.BuildScene` converts the instantiation tree into a
`CanvasNetRender3D` `Scene` (primitives, 2D shapes, transforms, color, booleans, extrusions, modifiers, and
the special variables that control curve resolution) — and **render** it: `ScadDocument.Render` draws the scene
onto a `Surface` (the core `CanvasNet` pixel buffer) with a camera fitted to the model or supplied by the
caller. Everything that cannot be built is reported, never silently skipped. The package is packable and is
the first release in which the roadmap's user-visible rendering capability exists; the 2D Minkowski sum
and a few other constructs remain deferred and are reported as unsupported features (see _Supported And
Deferred Features_). Phase 7 added `hull` and `minkowski`; Phase 8 added transparency (the alpha of a color)
and the `#` and `%` modifiers as translucent overlays; the text delivery added `text()`, which lays a string
out with a TrueType font (the bundled Liberation Sans unless a font is named) into flat 2D shapes. The text
dialect is deliberately simple and is CanvasNetScad's own: no shaping or ligatures, `language` and `script`
accepted and ignored, simple vertical directions, `size` as the ink height of `H`, and alignment by the measured
ink extents; `textmetrics()` and `fontmetrics()` stay unsupported (see _ScadTextGeometry Unit Design_).

`CanvasNetScad` consists of one direct unit and three subsystems:

- **ScadDocument** (unit; namespace `DemaConsulting.CanvasNet.Scad`, folder
  `src/DemaConsulting.CanvasNet.Scad/`): the public API. `ScadDocument` offers the entry points
  `Parse(string)`, `Open(string)` (a file path), and `Open(Stream)`, exposes `SourceName`, `Directives`,
  `ModuleNames`, and `FunctionNames`, and offers `Evaluate`, `BuildScene`, and `Render`. The supporting public
  types are `ScadOpenOptions` (immutable, validated open options), `ScadSourceLocation` (file, line, column),
  `ScadSyntaxException` (the parse error type, a `FormatException`), `ScadDirective`, `ScadDirectiveKind`,
  the evaluation contract (`ScadEvaluationOptions`, `ScadEvaluationResult`, `ScadLimits`, `ScadDiagnostic`,
  `ScadUnsupportedFeature`, `ScadUnsupportedFeatureException`, and `ScadEvaluationException`), and the render
  options `ScadRenderOptions`. See _ScadDocument Unit Design_ (`canvas-net-scad/scad-document.md`).
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
- **ScadGeometry** (internal subsystem; namespace `DemaConsulting.CanvasNet.Scad.Geometry`, folder
  `Geometry/`): the geometry builder, with five units. `ScadGeometryBuilder` walks the instantiation tree
  iteratively and combines it into a `Scene` (implicit unions, modifiers, booleans, the `hull` and `minkowski`
  convex combinations of `ScadHulls`, the triangle budget, reporting, and the node-to-location map).
  `ScadPrimitives` builds `cube`, `sphere`, `cylinder`, `polyhedron`,
  `square`, `circle`, and `polygon` and resolves `$fn`, `$fa`, and `$fs`. `ScadTransforms` applies `translate`,
  `rotate`, `scale`, `mirror`, `multmatrix`, `resize`, and `color`. `ScadExtrusions` builds `linear_extrude` and
  `rotate_extrude` over a symbolic 2D shape tree. `ScadTextGeometry` builds `text` from the glyph outlines of a
  TrueType font into flat 2D shapes. The subsystem has no public types. See _ScadGeometry
  Subsystem Design_ (`canvas-net-scad/scad-geometry.md`).

Dependencies inside the system are one-directional: `ScadDocument` calls `ScadParser`, which calls
`ScadLexer`, `ScadEvaluator`, which calls `ScadFileResolver` (which calls `ScadParser` for each included
file), `ScadValue`, and `ScadBuiltinFunctions`, and `ScadGeometryBuilder`, which calls `ScadPrimitives`,
`ScadTransforms`, `ScadExtrusions`, and `ScadTextGeometry`; the `ScadLanguage` subsystem never calls back into
`ScadDocument` or `ScadEvaluation` (it uses only the shared `ScadSourceLocation`, `ScadSyntaxException`, and
`ScadDirectiveKind` value types), `ScadEvaluation` depends on `ScadLanguage`, never the reverse, and
`ScadGeometry` depends on the instantiation tree and sink of `ScadEvaluation`, never the reverse and never on
`ScadLanguage`. Outside the system, `ScadGeometry` and the `Render` and `BuildScene` methods of `ScadDocument`
use `CanvasNetRender3D` and the core `CanvasNet` (see _Dependencies_).

## External Interfaces

The system exposes the following public API in the `DemaConsulting.CanvasNet.Scad` namespace:

- **`ScadDocument`** (sealed, immutable, thread-safe, not `IDisposable`): the static
  `Parse(string, ScadOpenOptions?)`, `Open(string, ScadOpenOptions?)`, and
  `Open(Stream, ScadOpenOptions?)` entry points; the `SourceName`, `Directives`, `ModuleNames`, and
  `FunctionNames` properties; `Evaluate(ScadEvaluationOptions?, CancellationToken)`, which returns a
  `ScadEvaluationResult`; `BuildScene(ScadRenderOptions?, CancellationToken)`, which returns a Render3D
  `Scene`; and `Render(int width, int height, ScadRenderOptions?, CancellationToken)`, which returns a new
  `Surface` that the caller owns and disposes.
- **`ScadRenderOptions`** (sealed): init-only `Evaluation` (the `ScadEvaluationOptions` that steer evaluation,
  reporting, and strict mode for every stage), `Camera` (null fits the view to the model),
  `ViewAzimuthDegrees` and `ViewElevationDegrees` (the fitted view; the isometric angles by default),
  `Projection` (the fitted view; perspective by default), `SceneOptions` (the renderer's look-and-feel and
  performance settings; its handler and strict flag are ignored), `FlatShapeThickness` (the slab thickness
  of a 2D shape that is not extruded; 0.1 by default), and the Phase 8 properties `HighlightColor` (the color of
  the translucent copy that `#` draws; `(255, 64, 128, 110)` by default) and `GhostColor` (the color of the
  translucent ghost that `%` draws; `(128, 128, 128, 80)` by default), both accepting any alpha, and the text
  property `FontResolution` (a `ScadFontResolutionOptions`, or `null` for the defaults; it controls where an
  explicitly named `text()` font is found). Every numeric value is validated when set.
- **`ScadFontResolutionOptions`** (sealed, immutable): `UseSystemFonts` (whether installed fonts are searched
  for an explicitly named font; `true` by default) and `AdditionalDirectories` (extra directories scanned
  before the installed fonts; empty by default, copied and validated when set).
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
  `MaxOutputLength`, `MaxDiagnostics`, and `MaxTriangles` (the bound on generated geometry), with the
  `Default` instance.
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
| `ScadDocument.BuildScene(...)` | Inbound and outbound | `ScadRenderOptions`, a `CancellationToken`; a Render3D `Scene` | Synchronous; thread-safe on a shared document; every call returns a new scene; events delivered once, in order, on the calling thread; bounded by `ScadLimits` including `MaxTriangles`; never recurses, so it is safe on any caller stack |
| `ScadDocument.Render(...)` | Inbound and outbound | Width and height, `ScadRenderOptions`, a `CancellationToken`; a `Surface` | Width and height from 1 to `Surface.MaxDimension` and a valid camera, validated before anything is evaluated; the caller owns the surface; byte-identical output for identical input |
| `ScadRenderOptions` properties | Inbound | Numbers, an enumeration, `ScadEvaluationOptions`, `Camera`, `SceneRenderOptions`, two `Rgba32` colors | View angles finite, projection defined, thickness finite and positive, overlay colors of any alpha; instance immutable |
| `ScadEvaluationOptions.Variables` | Inbound | Name to value dictionary | Names match an optional `$` then ASCII letters, digits, and underscores (not starting with a digit); values are null, booleans, numeric types, strings, or non-string sequences of these (nested at most 256 levels, at most 1,000,000 elements in total); invalid entries throw `ArgumentException` when set |
| `ScadOpenOptions.FileResolver` | Inbound | `Func<string, Stream?>` | Receives only normalized logical paths, only on the calling thread; a null result means "not found"; the stream is disposed after reading |
| `ScadEvaluationResult` | Outbound | Read-only lists and a count | Cannot be turned into mutable collections |
| `ScadSyntaxException` | Outbound | Exception with `Location` | Thrown for the first lexical or grammatical error, for nesting deeper than 256 levels, and for a syntax error in a file loaded by `include` or `use` |
| `ScadEvaluationException` | Outbound | Exception with `Location` | Thrown for a failed assertion, an exceeded limit (including the triangle limit, with the module's location), an include cycle or sandbox violation, a directive with no place to look, and, without a location, the renderer's fragment budget |
| `ScadUnsupportedFeatureException` | Outbound | Exception with `Feature` and `Location` | Thrown only when `ThrowOnUnsupportedFeature` is set, at the first unsupported feature of any stage (evaluation, geometry, renderer operand checks) |
| `ArgumentOutOfRangeException`, `ArgumentException` | Outbound | Exception | `Render` throws them, before any work, for a width or height outside 1 to `Surface.MaxDimension` and for an invalid camera |
| `OperationCanceledException` | Outbound | Exception | Thrown, never wrapped, when the token is canceled before or during evaluation, geometry building, or rendering |
| `InvalidDataException` | Outbound | Exception | Thrown, not as a `ScadSyntaxException`, when the source or an included file exceeds 4,194,304 characters (no location is meaningful) |
<!-- markdownlint-enable MD013 -->

Parsing never opens any file named by an `include` or `use` directive: directives are recorded only. Files
are accessed only by `Evaluate`, `BuildScene`, and `Render` (which evaluate), only through the open options,
and only inside the sandbox described in _Risk Control Measures_. The types of the `ScadLanguage`,
`ScadEvaluation`, and `ScadGeometry` subsystems are internal and visible only to the test assembly through
`InternalsVisibleTo`. The unsupported-feature identifiers that the geometry phase adds (`scad-module-*`,
`scad-modifier-*` (a `#` or `%` on a 2D shape that is not extruded only), `scad-csg-*`, the Phase 7 `scad-hull-*`
and `scad-minkowski-*`, and the
others) are listed with their triggers in
_ScadGeometry Subsystem Design_ and in the user guide. See the unit and subsystem design documents
for every type's complete constructor, property, and exception detail.

## Dependencies

`CanvasNetScad` references the separate `CanvasNet` system and the `CanvasNetRender3D` system through
project references in
`src/DemaConsulting.CanvasNet.Scad/DemaConsulting.CanvasNet.Scad.csproj`:

- `CanvasNet` (its own package, `DemaConsulting.CanvasNet`), for the `Surface` that `Render` returns, the
  `Rgba32` colors of the scene and the renderer options, and, for `text`, the `Fonts` subsystem (`TrueTypeFont`
  and `SystemFontCatalog`, including the bundled Liberation fonts) and its `Geometry.Path` outlines
- `CanvasNetRender3D` (its own package, `DemaConsulting.CanvasNet.Render3D`), whose scene model
  (`Scene`, `SceneNode`, `GroupNode`, `MeshNode`, `CsgNode`, `Mesh`, `Matrix4x4D`, `Vector3D`), tessellators and
  extruders (`PrimitiveTessellator`, `PolygonTriangulator`, `PolygonRegion`, `Extruder`, `ConvexHull`,
  `MinkowskiSum`), and renderer
  (`SceneRenderer`, `Camera`, `SceneRenderOptions`) the `ScadGeometry` subsystem and the `BuildScene` and
  `Render` methods of `ScadDocument` use

Both are consumed as public API only and are declared as the package dependencies of
`DemaConsulting.CanvasNet.Scad` (the package therefore pulls in both packages on every target framework); the
public API of `CanvasNetScad` exposes the Render3D types `Scene`, `Camera`, `CameraProjection`, and
`SceneRenderOptions` and the core `Surface`. The `ScadLanguage` and `ScadEvaluation` subsystems use neither
system, so parsing and evaluation behave independently of both. The dependencies are modeled in the SysML2 model
as the `usesCanvas` and `usesRender3D` dependencies.

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
  restarted on a bounded number of dedicated large-stack threads (`max(2, ProcessorCount)` threads, each reserving
  384 MiB of address space that is committed only as touched: 768 MiB on one or two cores, 24 GiB on 64 cores; for
  64-bit hosts, not supported in a 32-bit process or under strict overcommit, where failing to create or start the
  thread is reported as a `ScadEvaluationException`, or, for the parser, a `ScadSyntaxException`), exactly as the
  parser does; a
  caller waiting for a thread slot can be canceled. The restart is invisible because evaluation has no observable
  side effect until the buffered events are delivered.
- **File-access sandbox.** `include` and `use` are resolved before evaluation, on the calling thread, and only
  through the open options: files under the base directory or a library path, or, when a resolver callback is
  supplied, **only** that callback with no file-system access at all. Environment variables are never
  consulted. Absolute paths, drive letters, colons, control characters, and `..` above the root are rejected
  with an evaluation error (and, in resolver mode, never reach the callback); a file that is, or lies beneath, a
  symbolic link or junction below its root is refused whatever the link's target (the root itself is trusted;
  to use linked content, add the real directory as a library path). **The sandbox is not a security boundary
  against a concurrent local attacker:** it protects against path-based escapes in static file trees, but the check
  and the open are separate operations, so an attacker with write access to the included folders can swap a
  component for a link or junction between them, or place a hard link (which cannot be detected) to a file outside
  the roots; controlling who can write to those folders is the caller's responsibility. Include cycles, nesting
  depth, the number of inclusions, and the
  number of statements include expansion may produce are bounded. A document with `include` or `use` but no
  base directory, library path, or resolver is an error rather than a search of the current directory.
- **Cancellation.** A `CancellationToken` is honored before any work (and before any resolver call), between
  files, every 1,024 evaluation steps, every 65,536 multiply-adds of a matrix product, every 65,536 candidate
  comparisons of a `search`, every 65,536 elements counted for an operand, and at every loop turn; the
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

- **Bounded geometry.** The geometry a model generates is bounded by `ScadLimits.MaxTriangles`: every sphere,
  cylinder, polyhedron, slab, and extrusion computes the exact triangle count of its mesh (for an extrusion,
  the caps of a profile with holes included), with saturating arithmetic, before it allocates, and exceeding
  the budget raises a `ScadEvaluationException` located at the module. A `hull` and a `minkowski` know the exact
  count of their result only once they have computed it, so they charge its upper bound first (`2d - 4` triangles for
  a hull of `d` distinct points, `2 * Va * Vb - 4` for a pairwise sum) before any hull or sums memory is allocated,
  fail without starting when the bound exceeds the remaining budget, settle the charge to the actual count afterwards
  (refunding the whole pre-charge on a rejection, failure or cancellation), and charge the transformed copies of the
  convex parts of a `minkowski` fold (made only for the sum that needs them; a lone child needs none) while they live
  (the pairwise sums of a Minkowski sum are bounded by
  `MinkowskiSum.MaxPairwiseSums`).
  The renderer's per-tile fragment budget is mapped to the same exception.
- **Recursion-free geometry building.** `ScadGeometryBuilder` walks the instantiation tree, and every 2D
  shape-tree walk, with explicit stacks; no code in the `Geometry` namespace recurses, so a model nested
  thousands of levels deep builds identically on a caller thread with a 64 KiB stack. The renderer is likewise
  iterative for deep scenes.
- **Nothing is silently skipped.** Invalid arguments are warnings, constructs that are recognized but not built
  are unsupported-feature reports with stable identifiers, and a Render3D rejection that argument validation did
  not anticipate, and an instance whose coordinates are not finite or exceed 1e100 in magnitude, are reported as
  `scad-geometry-rejected`; the geometry builder never throws an
  `ArgumentException` out of the library. All reports of evaluation, geometry building, and the renderer's
  operand checks share one buffer, one ordering, one de-duplication, one cap, and one strict-mode switch, and are
  delivered once on the calling thread.
- **Validated render arguments.** The size and the camera of `Render` are validated before any evaluation, so an
  invalid call fails without doing work; the returned surface is disposed when a handler throws after the
  drawing succeeded.

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

**Build and render path (`ScadDocument.BuildScene`, `ScadDocument.Render`):**

1. **Validate** (`Render` only): the width and height (1 to `Surface.MaxDimension`) and the camera (rejected by
   rendering an empty scene with it, so the renderer's own rules decide), then the token; `BuildScene` checks
   only the token.
2. **Evaluate**: steps 2 to 4 of the evaluate path, into a fresh sink that is kept open (nothing is delivered
   yet).
3. **Build geometry**: `ScadGeometryBuilder.Build` receives the roots, the triangle limit, the flat-shape
   thickness, strict mode, and the token, and the **same** sink. It finds the `!` roots, walks the tree
   iteratively, dispatches each instance to the primitive, transform, color, boolean, or extrusion builder (or
   reports it as unsupported), and returns the `Scene` and the node-to-location map. A model without geometry is a
   scene with an empty root.
4. **Fit the camera and render** (`Render` only): the camera is the caller's, or fitted to the bounds of the
   scene with the configured azimuth, elevation, and projection (an unusable box, which the coordinate guard
   of the geometry builder makes impossible, falls back to the unit-cube framing instead of throwing); the
   renderer options are the caller's scene
   options copied with a handler that maps the renderer's operand reports into the sink (installed only when a
   handler or strict mode exists); `SceneRenderer.Render` draws the scene with the token. The renderer's
   fragment-budget overflow becomes a `ScadEvaluationException`.
5. **Deliver**: the sink replays the events of all stages to the caller's handlers, in order, once, on the
   calling thread (on a failure too, with a handler exception on that path ignored so that the failure wins; on
   success a handler exception propagates and `Render` disposes its surface first).
6. **Output**: the scene or the surface, or the first error as a typed exception.

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
  interprets them; it emits a tree of instances with evaluated arguments, and the geometry subsystem
  converts the tree to a scene and reports geometry-level unsupported features through the same channel
- **Geometry by construction, not by kernel**: There is no 2D boolean kernel and no mesh-boolean kernel in this
  system; 2D results are symbolic trees that extrusion distributes over, and solid booleans are scene nodes that
  the renderer resolves per pixel, so the system never needs to repair or classify a mesh itself
- **Fail-visible**: Nothing the model asks for may disappear without a report; see _Event Classification_ in
  _ScadEvaluation Subsystem Design_ and _ScadGeometry Subsystem Design_
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

- **NuGet Packaging**: Standard .NET library packaging and distribution; the package is packable from Phase 6
  and declares `DemaConsulting.CanvasNet` and `DemaConsulting.CanvasNet.Render3D` as dependencies
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests
- **Front-end role**: `ScadDocument.BuildScene` converts the instantiation tree of an evaluated document into a
  `CanvasNetRender3D` `Scene` and `ScadDocument.Render` draws it; neither the parser nor the evaluator ever
  calls into the renderer, and the renderer has no knowledge of the OpenSCAD language

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
instance trees) — and the Phase 6 render fixtures `render-*.scad`, whose assertions are analytic (areas, bounds,
pixel rectangles, exact volumes) rather than golden images, are original works authored for this repository and
licensed with it; their `README.md` records their provenance. The table of named colors is the CSS table that the
repository's own SVG codec already carries (a public W3C table authored in this repository), not material from
OpenSCAD. Exact parity with the behavior of any existing implementation (pixels, tessellation, edge cases) is an
explicit non-goal.

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
| `cube`, `sphere`, `cylinder` (cones and frusta), `polyhedron`, `square`, `circle`, `polygon`; `$fn`, `$fa`, `$fs` | Supported (Phase 6) | See _ScadPrimitives Unit Design_; the fragment rule is public and documented |
| `translate`, `rotate`, `scale`, `mirror`, `multmatrix`, `resize`, `color` | Supported (Phase 6) | See _ScadTransforms Unit Design_; `multmatrix` with a projective row is `scad-multmatrix-projective` |
| `union`, `render`, implicit unions, `difference`, `intersection`, `intersection_for`, for solids and 2D shapes | Supported (Phase 6) | 2D booleans are symbolic and are distributed over by extrusion; mixed dimensions warn |
| `linear_extrude`, `rotate_extrude` | Supported (Phase 6) | See _ScadExtrusions Unit Design_; `convexity` is accepted and ignored |
| `!` (show only) | Supported (Phase 6) | The marked subtree becomes the new root of the design, without the transforms and colors of its ancestors |
| `#` (highlight) | Supported (Phase 8) | On a solid: the subtree renders normally **and** as a translucent copy (`HighlightColor`) that shares its meshes and still takes part in the booleans around it; on a 2D shape that is not extruded: rendered normally and reported as `scad-modifier-highlight` |
| `%` (background) | Supported (Phase 8) | On a solid: a translucent ghost (`GhostColor`) that is not part of the model (it does not cut or add, contributes no propagated bounds so `resize` ignores it, but is part of `Scene.ComputeBounds()` and so of the fitted view); on a 2D shape that is not extruded: omitted and reported as `scad-modifier-background` |
| Color with alpha below 1, `transparent` | Supported (Phase 8) | The alpha becomes the alpha of the appearance and surfaces are composited; nothing is reported |
| `ScadDocument.BuildScene`, `ScadDocument.Render`, `ScadRenderOptions`, `ScadLimits.MaxTriangles` | Supported (Phase 6) | Evaluation options, strict mode, and handlers cover every stage |
| Geometry-level unsupported-feature reporting | Supported (Phase 6) | Identifier table in _ScadGeometry Subsystem Design_ |
| Packaging as a NuGet package, README and user guide | Delivered (Phase 6) | `IsPackable` is enabled |
| `hull` (3D and 2D children) | Supported (Phase 7) | The convex hull of the children's points; booleans below it, mixed dimensions and degenerate points are `scad-hull-csg-operand`, `scad-hull-mixed-dimensions`, `scad-hull-degenerate`; colors inside are dropped with a warning |
| `minkowski` (convex 3D children) | Supported (Phase 7) | Folded from the left, unions distribute; `convexity` ignored; a non-convex operand is `scad-minkowski-nonconvex`, too much work is `scad-minkowski-too-complex`, mixed dimensions are `scad-minkowski-mixed-dimensions` |
| `minkowski` of 2D children | Deferred | Reported as `scad-minkowski-2d`; omitted |
| `text` (`text`, `size`, `font`, `halign`, `valign`, `spacing`, `direction`; `language` and `script` accepted and ignored) | Supported (text delivery) | See _ScadTextGeometry Unit Design_; flat 2D glyph shapes (holes kept) laid out left to right, right to left, top to bottom, or bottom to top without shaping; `size` is the ink height of `H`; alignment by the measured ink extents; the default font is the bundled Liberation Sans; an unusable outline is `scad-text-invalid-outline` or `scad-text-overlapping-contours` |
| `import`, `offset`, `projection`, `surface`, `roof` | Not supported | Reported as `scad-module-<name>`; omitted |
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

The geometry phase adds the following dialect decisions, each pinned by a test and described in
_ScadGeometry Subsystem Design_ and the unit designs:

 1. Invalid arguments (a non-positive size, a wrong type, a non-finite number) are a **warning at the
    instance's location and an empty instance**; things the language allows but this implementation cannot
    build are **unsupported-feature reports**; resource exhaustion is an **error**. A singular transform is a
    warning and an empty instance, not a renderer report.
 2. `$fn`, `$fa`, and `$fs` follow the public fragment rule (`$fn` wins; otherwise
    `ceil(max(min(360 / $fa, 2 pi r / $fs), 5))`, at least three); a radius of 1 gives 5 fragments and 10 gives 30
    with the defaults.
 3. `rotate([x, y, z])` applies the X rotation first, then Y, then Z; `multmatrix` takes a column-vector matrix
    (4x4, or 3x4); `mirror(v)` reflects through the plane with normal `v`; `resize` scales about the origin.
 4. A positive `twist` turns **clockwise seen from +Z**; `linear_extrude` has the default height 100;
    `rotate_extrude` starts at `+X`, sweeps counter-clockwise, clamps angles beyond 360 degrees, sweeps negative
    angles clockwise, and sweeps a profile that lies on the negative side of the axis where it is (a point at
    `(-4, 0)` with `angle = 90` sweeps from 180 to 270 degrees; a profile with both signs is reported).
 5. An extrusion that cannot be built is **omitted whole** and reported (axis crossing or contact, invalid
    scale, a Render3D rejection), so a boolean over it never silently changes meaning.
 6. A 2D shape that is not extruded is a thin slab (`FlatShapeThickness`); a union that mixes dimensions keeps
    the 3D members, and a difference or intersection keeps the dimension of its first non-empty operand; each
    warns about what it drops.
 7. The `!` modifier makes the outermost marked subtrees the new root of the design, **without** the transforms
    and colors of their ancestors (`translate([10, 0, 0]) { !cube(1); }` renders the cube at the origin).
    `%` draws a **ghost** (a translucent gray body that does not take part in the model: it is false in every
    enclosing union, difference, and intersection, contributes no propagated bounds (so `resize` ignores
    it), but is part of `Scene.ComputeBounds()`, the bounds that fit the default camera - a documented
    CanvasNet choice, not a parity claim, because excluding it could push the ghost out of the frame).
    `#` renders the subtree normally **and** as a translucent pink copy that
    shares its meshes and keeps taking part in the booleans (a highlighted subtrahend still cuts, and its copy
    is drawn where it cut), so the highlight is visible even where the subtree was subtracted. A color alpha
    below 1 makes the surface translucent. A union of translucent bodies shows only the boundary of the union
    (overlapping members merge, like any CSG result); independent see-through bodies are expressed with `%`.
    Translucent models and overlays are resolved with signed crossing counters, so their meshes must be closed
    solids (an open `polyhedron` is reported through the `scad-csg-*` identifiers). A `#` or `%` on a 2D shape that
    is not extruded has no overlay form: `#` is rendered normally and `%` omitted, and both are reported.
    A `%` that is a child of `hull` or `minkowski` is not part of the hull or the sum (and a highlight there is
    not drawn). The triangles of a `#` copy count against `ScadLimits.MaxTriangles` a second time.
 8. Rendering is deterministic (byte-identical images for any degree of parallelism) and uses a neutral gray on
    a white background, with a view fitted to the model by default, unless the model or the options say
    otherwise.

### Determinism And Immutability

Every public result type is immutable. A `ScadDocument` and its internal syntax tree can be shared
between threads, and parsing the same text any number of times, on any thread, in any culture, yields
equal trees and identical errors. `ScadDocument.Evaluate`, `BuildScene`, and `Render` hold no state between
calls, so a document may be evaluated, built, or rendered from several threads at once, and each call gives the
same result for the same options (a render is byte-identical for every degree of parallelism).

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
3. **Phase 6 — Geometry and rendering (delivered).** The `ScadGeometry` subsystem (`ScadGeometryBuilder`,
   `ScadPrimitives`, `ScadTransforms`, `ScadExtrusions`) mapping the instantiation tree onto `CanvasNetRender3D`
   scenes iteratively; the public `ScadDocument.BuildScene` and `ScadDocument.Render` and `ScadRenderOptions`,
   which composes the evaluation options; the additive `ScadLimits.MaxTriangles` limit; the geometry-level
   unsupported-feature reports; original render fixtures and analytic system tests; the flip of `IsPackable`; and
   the README, the user guide, and the complete companion documentation. The phase also tightened the Render3D
   rotational extruder to reject sweeps whose angular step exceeds 120 degrees, which the `rotate_extrude`
   segment rule always satisfies.
4. **Phase 7 — Hull and Minkowski (delivered).** The `hull` module (the convex hull of the points of 3D
   children, or of 2D children, built on the `CanvasNetRender3D` `ConvexHull`) and the `minkowski` module of convex
   3D children (built on `MinkowskiSum`, with the sum distributed over unions) in `ScadGeometryBuilder` (the internal
   `ScadHulls`), the seven `scad-hull-*` and `scad-minkowski-*` unsupported-feature identifiers, original render
   fixtures and analytic system tests, and the complete companion documentation. The 2D Minkowski sum remains
   deferred and is reported as `scad-minkowski-2d`.
5. **Phase 8 — Highlight, background, and transparency (delivered).** `ScadColors` carries the alpha of a color
   into the appearance; `%` builds a ghost and `#` a translucent copy as Render3D `OverlayNode` instances (built
   in the Phase 8 transparency work of `CanvasNetRender3D`), configured by the additive `ScadRenderOptions`
   properties `HighlightColor` and `GhostColor`; `resize` sizes from the bounds without overlays; the
   `scad-color-alpha` identifier was removed, and `scad-modifier-highlight` and `scad-modifier-background` remain
   for 2D shapes that are not extruded only. A new original fixture, `render-transparency.scad`, and analytic
   system tests, and the complete companion documentation.
6. **Text (delivered).** The `text` module in the new `ScadTextGeometry` unit: layout with a TrueType font from the
   core `Fonts` subsystem (deterministic bundled Liberation Sans by default, explicit `font` resolution through
   additional directories, installed fonts, and the bundled families), flattening of the glyph outlines into
   flat 2D regions with holes, and integration with every consumer of 2D shapes; the additive public
   `ScadFontResolutionOptions` and `ScadRenderOptions.FontResolution`; the `scad-text-invalid-outline` and
   `scad-text-overlapping-contours` identifiers; the additive `SystemFontCatalog.DiscoverFonts` and
   `FindBestMatchAmong` in the core package; a new original fixture, `render-text.scad`, and analytic system
   tests; and the complete companion documentation. `textmetrics()` and `fontmetrics()` remain unsupported.
7. **Later — External-file import.**

Every later phase adds members and types rather than changing the signatures delivered here.

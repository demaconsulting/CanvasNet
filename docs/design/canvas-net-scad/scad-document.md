## ScadDocument Unit Design

![CanvasNetScad Structure](CanvasNetScadView.svg)

<!-- cspell:ignore lexer lexical evaluator undef textmetrics -->

### Purpose

`ScadDocument` is the public entry point of `CanvasNetScad`. It is the front end for
OpenSCAD-language source: it reads source text from a string, a stream or a file, drives the internal
`ScadLanguage` subsystem to tokenize and parse it, and exposes the validated result. A document
proves that the source is syntactically valid and reports what it declares at file scope (the
`include`/`use` directives and the names of the file-scope modules and functions). **Parsing never evaluates
the program and never opens another file.** A document can then be **evaluated**: `Evaluate` drives the
internal `ScadEvaluation` subsystem and returns the echo output, the warnings, the unsupported-feature
reports, and the number of module instances produced; only evaluation opens other files (the files named
by `include` and `use`), and only through the document's `ScadOpenOptions` and inside the sandbox
described in _CanvasNetScad System Design_. A document can also be **built** into a Render3D `Scene`
(`BuildScene`) and **rendered** onto a `Surface` (`Render`): both evaluate the document, drive the internal
`ScadGeometry` subsystem, and (for `Render`) the Render3D renderer, steered by `ScadRenderOptions`. The package is
an original implementation written from
general language-design knowledge; see _ScadLexer_ (`scad-language/scad-lexer.md`) for the clean-room
statement that applies to the whole system. The unit covers the files `ScadDocument.cs`,
`ScadDocument.Evaluate.cs`, `ScadOpenOptions.cs`, `ScadSourceLocation.cs`, `ScadSyntaxException.cs`,
`ScadDirective.cs`, `ScadDirectiveKind.cs`, the evaluation contract `ScadEvaluationOptions.cs`,
`ScadEvaluationResult.cs`, `ScadLimits.cs`, `ScadDiagnostic.cs`, `ScadUnsupportedFeature.cs`,
`ScadUnsupportedFeatureException.cs`, and `ScadEvaluationException.cs`, the geometry and rendering contract
`ScadDocument.Render.cs` (the `BuildScene` and `Render` methods) and `ScadRenderOptions.cs`, and the namespace
documentation `NamespaceDoc.cs`.

The unit's behavior is specified by the `CanvasNetScad-ScadDocument-*` requirements in
`docs/reqstream/canvas-net-scad/scad-document.yaml` and, at system level, by `CanvasNetScad-Open`,
`CanvasNetScad-OpenOptions`, `CanvasNetScad-Directives`, `CanvasNetScad-Definitions`,
`CanvasNetScad-SyntaxErrors`, `CanvasNetScad-InputLimits`, `CanvasNetScad-Evaluate`,
`CanvasNetScad-EvaluationOptions`, `CanvasNetScad-Diagnostics`, `CanvasNetScad-UnsupportedFeatures`,
`CanvasNetScad-EvaluationErrors`, `CanvasNetScad-IncludeUse`, `CanvasNetScad-EvaluationLimits`,
`CanvasNetScad-Cancellation`, `CanvasNetScad-VariableOverrides`, `CanvasNetScad-Determinism`,
`CanvasNetScad-BuildScene`, `CanvasNetScad-Render`, `CanvasNetScad-RenderOptions`,
`CanvasNetScad-GeometryUnsupported`, `CanvasNetScad-GeometryLimits`, `CanvasNetScad-RenderCancellation`, and
`CanvasNetScad-RenderDeterminism`.

### Data Model

- **`ScadDocument`** (sealed partial class, immutable) — holds the internal `ScadProgram` syntax tree, the
  effective `ScadOpenOptions`, and three public read-only lists derived from the tree once at
  construction: `Directives`, `ModuleNames`, and `FunctionNames`. The constructor is private; the only
  ways to obtain a document are `Parse` and the two `Open` overloads. The `Program` and
  `Options` properties are `internal` so that the evaluator can consume them without widening the public
  surface. The class is `partial` only so that `Evaluate` and the geometry entry points can live in
  `ScadDocument.Evaluate.cs` and `ScadDocument.Render.cs`. A document
  holds no unmanaged resources, is not disposable, and keeps no state between calls: it is safe for
  concurrent use, including concurrent `Evaluate`, `BuildScene`, and `Render` calls.
- **`ScadOpenOptions`** (sealed class, init-only) — `BaseDirectory` (`string?`), `LibraryPaths`
  (`IReadOnlyList<string>`, empty by default), `FileResolver` (`Func<string, Stream?>?`), and
  `SourceName` (`string?`). Every property is validated in its `init` accessor: null is allowed for
  the nullable properties, but empty or whitespace text throws `ArgumentException`; a null
  `LibraryPaths` list or entry throws `ArgumentNullException`, and a whitespace entry throws
  `ArgumentException`. `LibraryPaths` is copied defensively into a read-only list, so a later change to
  the caller's list has no effect. **Parsing consumes only `SourceName`** (it appears in every
  `ScadSourceLocation`); `BaseDirectory`, `LibraryPaths`, and `FileResolver` are consumed by `Evaluate`,
  where they define the file-access sandbox. A document that was not opened from a file has no base
  directory: there is **no implicit current directory**, so a stream or string document needs a resolver or
  library paths to use `include`/`use`. The internal `WithEffective(baseDirectory, sourceName)` creates a
  copy with those two values filled in.
- **`ScadSourceLocation`** (`readonly record struct (string? File, int Line, int Column)`) — a
  one-based position. A column counts UTF-16 code units, a tab counts as one column, and `\r\n`,
  `\n`, and a lone `\r` each end exactly one line. `default(ScadSourceLocation)` has `Line == 0` and
  means "unknown". `ToString()` returns `{File}:{Line}:{Column}`, `{Line}:{Column}` when `File` is
  null, or `?` when the line is zero; numbers use the invariant culture.
- **`ScadSyntaxException`** (sealed, derives from `FormatException`) — carries a `Location`
  (`ScadSourceLocation`, `default` when unknown). The constructor taking `(description, location)`
  builds the message `{location}: {description}`, or just the description when the location is unknown;
  the standard parameterless, message, and message-plus-inner-exception constructors leave the location
  unknown. The type derives from `FormatException` because `InvalidDataException` is sealed in the base
  class library.
- **`ScadDirective`** (sealed record) — `Kind` (`ScadDirectiveKind`), `Path` (the exact text between
  the angle brackets, never empty and never trimmed), and `Location` (the directive keyword).
- **`ScadDirectiveKind`** (enum) — `Include` and `Use`. The same enum is shared by the public API and
  the internal syntax tree (`ScadDirectiveStatement`); consumers should handle unknown values
  defensively.
- **`ScadEvaluationOptions`** (sealed class, init-only) — `Variables` (`IReadOnlyDictionary<string, object?>?`,
  default null), `Time` (`double`, default 0, must be finite, else `ArgumentOutOfRangeException`), `Limits`
  (`ScadLimits`, default `ScadLimits.Default`, null throws `ArgumentNullException`), the handlers
  `EchoHandler` (`Action<string>?`), `WarningHandler` (`Action<ScadDiagnostic>?`), and
  `UnsupportedFeatureHandler` (`Action<ScadUnsupportedFeature>?`), and `ThrowOnUnsupportedFeature` (default
  false). `Variables` is validated when set: each key must be an optional `$` followed by ASCII letters,
  digits, and underscores (an ordinary name must not start with a digit); each value is converted with
  `ScadValue.TryFromObject` (null, boolean, any numeric type, string, or a non-string sequence of these,
  nested at most 256 levels, with at most 1,000,000 elements in total), and anything else throws
  `ArgumentException` naming the key. The public copy is an ordinal-sorted read-only dictionary in which
  scalars are kept as supplied and **sequences are snapshotted** into arrays (enumerated exactly once); the
  converted values are kept in an internal `Overrides` dictionary in ordinal key order.
- **`ScadEvaluationResult`** (sealed class, internal constructor, get-only) — `Echo` (each full line,
  including the `ECHO:` prefix), `Diagnostics`, `UnsupportedFeatures`, and `InstanceCount` (module instances,
  groups included). The lists are `ReadOnlyCollection<T>` copies, not castable to a mutable type.
- **`ScadLimits`** (sealed class, init-only) — eleven limits with their defaults and valid ranges:
  `MaxRecursionDepth` (5,000; 16 to 16,384), `MaxEvaluationSteps` (50,000,000), `MaxLoopIterations`
  (10,000,000), `MaxListLength` (1,000,000), `MaxStringLength` (1,048,576), `MaxIncludeDepth` (16; 1 to 64),
  `MaxIncludedFiles` (1,000), `MaxInstances` (1,000,000), `MaxOutputLength` (4,194,304 echo characters),
  `MaxDiagnostics` (1,000), and `MaxTriangles` (a `long`, 5,000,000; at least 1; the bound on the triangles the
  geometry of one `BuildScene` or `Render` call may generate). Each is validated in its `init` accessor
  (`ArgumentOutOfRangeException`), and `ScadLimits.Default` is a shared instance.
- **`ScadRenderOptions`** (sealed class, init-only) — `Evaluation` (`ScadEvaluationOptions?`, default null: the
  defaults apply; the single source of the variable overrides, `$t`, the limits, the handlers, and strict mode
  for every stage), `Camera` (`Camera?`, default null: fit the view to the model), `ViewAzimuthDegrees` (default
  `Camera.DefaultAzimuthDegrees`, 45) and `ViewElevationDegrees` (default `Camera.DefaultElevationDegrees`, about
  35.26), both finite else `ArgumentOutOfRangeException`, `Projection` (`CameraProjection`, default
  `Perspective`, must be a defined value), `SceneOptions` (`SceneRenderOptions?`, default null: the Render3D
  defaults, a white background and neutral gray surfaces; its unsupported-feature handler and strict flag are
  ignored), and `FlatShapeThickness` (default 0.1; finite and greater than zero). The options **compose**
  rather than copy the evaluation options, so there is one definition of strict mode, handlers, and limits.
- **`ScadDiagnostic`** (sealed record) — `Message` and `Location`; `ToString()` is `{Location}: {Message}` with
  the invariant culture. **`ScadUnsupportedFeature`** (sealed record) — `Feature` (an identifier such as
  `scad-function-textmetrics`), `Message`, and `Location`.
- **`ScadEvaluationException`** (sealed, derives from `InvalidOperationException`) and
  **`ScadUnsupportedFeatureException`** (sealed, derives from `IOException`, with a `Feature` property) — each
  carries a `Location`, builds the message `{location}: {description}` when the location is known, and has the
  standard constructors. `InvalidDataException` is sealed in the base class library, which is why these types
  derive from other base classes (the same reason `ScadSyntaxException` is a `FormatException`).

**Invariants**: a `ScadDocument` exists only for source that tokenized and parsed without error, so
`Directives`, `ModuleNames`, and `FunctionNames` are always consistent with a valid program. The
three lists are in source order, are read-only, and keep duplicates. `ModuleNames` and `FunctionNames`
list only definitions at file scope; definitions nested in blocks or in other definitions are not
listed. Directives are only legal at file scope, so `Directives` is the complete set.

### Key Methods

- `ScadDocument.Parse(string source, ScadOpenOptions? options = null)` — throws
  `ArgumentNullException` for a null source, uses default options when `options` is null, and parses
  the text. Empty, whitespace-only, and comment-only text is a valid empty document; a leading U+FEFF is
  ignored by the lexer. No base directory or source name is invented, so locations are unnamed unless
  the caller supplies `SourceName`.
- `ScadDocument.Open(string path, ScadOpenOptions? options = null)` — validates the path
  (`ArgumentException.ThrowIfNullOrWhiteSpace`), computes the effective options, opens the file with
  `File.OpenRead`, reads it as bounded UTF-8 text, and parses. Unless overridden by `options`, the
  source name is the path exactly as supplied and the base directory is the folder of
  `Path.GetFullPath(path)`. A missing file or folder surfaces as `FileNotFoundException` or
  `DirectoryNotFoundException` from the file system.
- `ScadDocument.Open(Stream stream, ScadOpenOptions? options = null)` — throws
  `ArgumentNullException` for a null stream and `ArgumentException` for an unreadable stream, reads
  from the stream's current position to its end, and leaves the stream open. No base directory or
  source name is invented.
- `ScadDocument.Evaluate(ScadEvaluationOptions? options = null, CancellationToken cancellationToken = default)` —
  uses default options when `options` is null, throws `OperationCanceledException` before any work (and before
  any resolver call) when the token is already canceled, converts the options to internal settings, creates the
  buffering `ScadEvaluationSink`, and calls `ScadEvaluator.Evaluate(this, settings, sink)`. On success it
  delivers the buffered events to the handlers **on the calling thread, in order** (a handler exception
  propagates) and returns a `ScadEvaluationResult` built from the sink and the instance count. On any
  failure — an evaluation error, a syntax error in an included file, an I/O error, cancellation — the events
  recorded before the failure are still delivered, and an exception thrown by a handler on that path is
  **ignored so that the original failure wins**. The method is synchronous and thread-safe; it never invokes
  a handler or the file resolver on a thread other than the caller's.
- `ScadDocument.BuildScene(ScadRenderOptions? options = null, CancellationToken cancellationToken = default)` —
  uses default options when `options` is null and the default `ScadEvaluationOptions` when `Evaluation` is
  null, throws `OperationCanceledException` before any work when the token is already canceled, creates a
  buffering `ScadEvaluationSink`, runs the shared evaluation step (`RunEvaluation`, also used by `Evaluate`),
  and calls `ScadGeometryBuilder.Build` with the roots, `ScadGeometrySettings` (the triangle limit, the
  flat-shape thickness, strict mode, and the token), and the **same** sink. On success it delivers the buffered
  events of both stages once, on the calling thread, in order (a handler exception propagates) and returns the
  scene; on any failure the events recorded so far are still delivered and a handler exception on that path is
  ignored so that the failure wins. A model without geometry gives a scene with an empty root.
- `ScadDocument.Render(int width, int height, ScadRenderOptions? options = null, CancellationToken
  cancellationToken = default)` — first validates the size (`ArgumentOutOfRangeException` for a width or height
  outside 1 to `Surface.MaxDimension`), then the token, then the camera (an invalid camera is rejected by
  rendering an empty 1 by 1 scene with it, so the renderer's own rules decide, with `ArgumentException`), all
  before any evaluation. It then builds the geometry as `BuildScene` does, installs a renderer
  unsupported-feature handler **only when** the evaluation options have a handler or strict mode (the renderer
  checks boolean operands only when it has one) that maps each report to a `scad-` identifier and to the source
  location of the module that produced the mesh (through the node-to-location map), copies the scene options
  with that handler (`ScadSceneOptions.Create`), fits the camera to the scene bounds with the configured angles
  and projection unless the caller supplied one, and draws with `SceneRenderer.Render` and the token. The
  renderer's fragment-budget overflow (`RenderBudgetExceededException`) becomes a `ScadEvaluationException`
  advising to raise `SceneOptions.MaxFragmentsPerTile` or to simplify the model. Events are delivered as for
  `BuildScene`; when a handler throws after a successful drawing, the surface is disposed before the exception
  propagates. The caller owns and disposes the returned surface.
- `ReadBounded(Stream)` (internal static) — reads UTF-8 with byte-order-mark detection through a
  `StreamReader` (`leaveOpen: true`) in 8,192-character chunks and checks the running length after each
  chunk. As soon as the length exceeds `ScadParser.MaxSourceLength` (4,194,304 UTF-16 characters) it
  throws, so a stream that never ends cannot consume unbounded memory. It is `internal` so that
  `ScadFileResolver` reads included files with the same bound.
- `Create(source, effective)` (private) — calls `ScadParser.Parse(source, effective.SourceName)` and
  wraps the resulting `ScadProgram` in a document. The constructor then extracts the three public lists
  with one pass of type filtering over the program's top-level statements
  (`ScadDirectiveStatement`, `ScadModuleDefinition`, `ScadFunctionDefinition`).

**Guarantees**: every parse entry point either returns a fully populated,
immutable document or throws; there is no partially valid document. Parsing is a pure function of the
source text and options: no file other than the one named by `Open(path)` is accessed, so an
`include`/`use` path need not exist and `FileResolver` is never invoked. `Evaluate` is a pure function of the
document, the options, and the files the options make reachable.

### Error Handling

- Programming-error arguments (a null source, path, or stream; an empty or whitespace path; an
  unreadable stream) throw `ArgumentNullException` or `ArgumentException` synchronously and are never
  caught or converted. `ScadOpenOptions`, `ScadEvaluationOptions`, and `ScadLimits` fail closed in the same
  way when a property is initialized with an invalid value.
- Source that cannot be tokenized or parsed throws `ScadSyntaxException` with the location of the
  first error (see _ScadLanguage_, `scad-language.md`, for the fail-fast model and the exact message
  catalogue in _ScadParser_ and _ScadLexer_). Nesting deeper than 256 levels is reported the same way
  rather than overflowing the stack. Every `Parse`/`Open` call parses inline on the caller's thread and
  restarts on a dedicated 16 MiB-stack thread (at most a bounded number at once) only when the caller's
  stack is too small, so the limit does not depend on the caller's stack size;
  exceptions are rethrown on the calling thread with their original type and stack trace. The
  parse operations take no cancellation token, so none is honored.
- Source longer than 4,194,304 UTF-16 characters throws a plain `InvalidDataException` with the message
  `Source exceeds the maximum length of 4194304 characters`, not a `ScadSyntaxException`, because no
  source location is meaningful for it. This applies to `Parse` (checked before any lexing), to
  `Open(Stream)`, and to `Open(path)` (checked while reading), and to every file loaded by `include`/`use`.
- File-system exceptions from `File.OpenRead` and stream read failures (`IOException` and derivatives)
  propagate unchanged.
- `Evaluate` throws `ScadEvaluationException` (with the location) for a failed `assert`, an exceeded
  `ScadLimits` limit, an include cycle, a sandbox violation, and a directive with no place to look;
  `ScadUnsupportedFeatureException` when `ThrowOnUnsupportedFeature` is set and the program uses an
  unsupported feature; `ScadSyntaxException` (naming the included file) for a syntax error in a referenced
  file; `InvalidDataException` for an oversize referenced file; `OperationCanceledException`, never wrapped,
  for cancellation; and `IOException` for a referenced file that cannot be read. Recoverable problems (an
  unknown name, a bad operand, a missing include file) are **warnings** in the result, not exceptions; see
  _ScadEvaluation_ (`scad-evaluation.md`, _Event Classification_).
- `BuildScene` and `Render` throw everything `Evaluate` throws, plus `ScadEvaluationException` (located) when
  the triangle limit would be exceeded, `ScadUnsupportedFeatureException` at the first unsupported feature of
  **any** stage (evaluation, geometry building, or the renderer's operand checks) when strict mode is on, and,
  for `Render`, `ArgumentOutOfRangeException` for a width or height outside 1 to `Surface.MaxDimension`,
  `ArgumentException` for an invalid camera, and `ScadEvaluationException` (without a location) for the
  renderer's fragment-budget overflow. Invalid arguments of a model are warnings, constructs that are not
  supported are unsupported-feature reports (see _ScadGeometry_, `scad-geometry.md`), and neither stops the
  build. Cancellation is never wrapped.

### Dependencies

- **`ScadLanguage` subsystem** (internal, namespace `DemaConsulting.CanvasNet.Scad.Language`) —
  `ScadParser.Parse(string, string?)` (which tokenizes with `ScadLexer` and parses), the `ScadProgram`
  syntax tree and its statement nodes (`ScadDirectiveStatement`, `ScadModuleDefinition`,
  `ScadFunctionDefinition`), and the constants and factory behind the source-length limit
  (`ScadParser.MaxSourceLength`, `ScadParser.CreateSourceTooLongException`). See _ScadLanguage_
  (`scad-language.md`).
- **`ScadEvaluation` subsystem** (internal, namespace `DemaConsulting.CanvasNet.Scad.Evaluation`) —
  `ScadEvaluator.Evaluate`, `ScadEvaluationSettings`, `ScadEvaluationSink`, `ScadEvaluationOutput`, and
  `ScadValue.TryFromObject` (for the `Variables` of the evaluation options). See _ScadEvaluation_
  (`scad-evaluation.md`).
- **`ScadGeometry` subsystem** (internal, namespace `DemaConsulting.CanvasNet.Scad.Geometry`) —
  `ScadGeometryBuilder.Build`, `ScadGeometrySettings`, `ScadBuiltScene`, `ScadSceneMap`, `ScadReporter`, and
  `ScadSceneOptions` (the copy of the renderer options and the report-identifier mapping). See _ScadGeometry_
  (`scad-geometry.md`).
- **`CanvasNetRender3D` package** — `Scene`, `Camera`, `CameraProjection`, `SceneRenderer`,
  `SceneRenderOptions`, `SceneRenderUnsupportedFeature`, and `RenderBudgetExceededException` (public API only;
  `Scene`, `Camera`, `CameraProjection`, and `SceneRenderOptions` appear in the public signatures of this unit).
- **`CanvasNet` core package** — `Surface` (the result of `Render`).
- **.NET base class library** — `System.IO` (`File`, `Stream`, `StreamReader`, `Path`,
  `InvalidDataException`, `FileNotFoundException`), `System.Text.Encoding.UTF8`, `System.Text.StringBuilder`,
  `System.Threading` (`CancellationToken`), and `System.Linq` for the derived lists.

### Callers

`ScadDocument` is the sole public entry point for reading and evaluating OpenSCAD-language source; it is
called directly by consuming applications. Within this repository no other unit calls it, except that
`ScadEvaluator` and `ScadFileResolver` read its internal `Program`, `Options`, and
`ReadBounded` (and its public `SourceName`) during evaluation. System-level integration tests in
`test/DemaConsulting.CanvasNet.Scad.Tests/ScadSystemIntegrationTests.cs` exercise it through its public API, and
the unit tests in `ScadDocumentTests.cs`, `ScadOpenOptionsTests.cs`, `ScadSourceLocationTests.cs`,
`ScadSyntaxExceptionTests.cs`, `ScadDocumentEvaluateTests.cs`, `ScadDocumentEvaluateSafetyTests.cs`,
`ScadEvaluationOptionsTests.cs`, `ScadLimitsTests.cs`, `ScadDiagnosticTests.cs`,
`ScadUnsupportedFeatureTests.cs`, `ScadEvaluationExceptionTests.cs`, `ScadFixtureEvaluationTests.cs`,
`ScadRenderOptionsTests.cs`, `ScadDocumentBuildSceneTests.cs`, `ScadDocumentRenderTests.cs`,
`ScadDocumentRenderCsgTests.cs`, `ScadDocumentRenderExtrusionTests.cs`, and `ScadDocumentRenderReportTests.cs`
verify the individual types and the geometry and rendering entry points.

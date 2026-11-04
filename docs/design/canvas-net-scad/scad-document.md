## ScadDocument Unit Design

![CanvasNetScad Structure](CanvasNetScadView.svg)

<!-- cspell:ignore lexer lexical evaluator undef textmetrics gimbal -->

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
`ScadDocument.Render.cs` (the `BuildScene` and `Render` methods) and `ScadRenderOptions.cs`, the viewport contract
`ScadViewport.cs` (the internal viewport plan and the camera choice), `ScadResolvedView.cs`, and `ScadViewSource.cs`,
and the namespace documentation `NamespaceDoc.cs`.

`Render` also chooses the **camera** of the picture, and that choice is part of this unit: the explicit camera or
view options of `ScadRenderOptions`, the viewport variables `$vpt`, `$vpr`, `$vpd`, and `$vpf` that the document
assigns (or that an override supplies), the flat top view of a model that is entirely 2D, or the isometric default,
in that order of precedence per variable. The mapping between the renderer's camera and the four variables, the
precedence, and the circularity that results from choosing the camera after the build are described under
_ScadViewport_ in _Key Methods_.

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
  ignored), `FlatShapeThickness` (default 0.1; finite and greater than zero), the Phase 8 `HighlightColor`
  (`Rgba32`, default `(255, 64, 128, 110)`) and `GhostColor` (`Rgba32`, default `(128, 128, 128, 80)`), the colors
  of the translucent overlays that the `#` and `%` modifiers draw (any alpha is accepted; they are handed to the
  geometry builder through `ScadGeometrySettings`), and `FontResolution` (`ScadFontResolutionOptions?`, default
  null: the defaults of that type; handed to the geometry builder through `ScadGeometrySettings.FontResolution`
  for the `text` module), and the view reporting hook `ResolvedViewHandler` (`Action<ScadResolvedView>?`, default
  null). The options also **record whether the caller set** `ViewAzimuthDegrees`, `ViewElevationDegrees`, and
  `Projection` (set in the `init` accessors; the internal `IsViewExplicit` is true when either angle was set, even to
  its default value, and `IsProjectionExplicit` when the projection was set), because an explicit value takes
  precedence over everything the model says about the view, whereas a default does not. The options **compose**
  rather than copy the evaluation options, so there is one definition of strict mode, handlers, and limits.
- **`ScadResolvedView`** (sealed record, public) — what `Render` actually used: `AzimuthDegrees` and
  `ElevationDegrees` (the direction from the target to the eye in the orbit convention of `Camera.FitToBounds`,
  whichever way the camera was chosen; `0` and `90` for a flat top view), `Projection` (`CameraProjection`),
  `IsFlatModel` (whether the whole result of the model is 2D), and `Source` (`ScadViewSource`). It is delivered to
  `ScadRenderOptions.ResolvedViewHandler` and is public API, additive like the other records of the package.
- **`ScadViewSource`** (enum, public; consumers should handle unknown values defensively) — where the view came
  from: `Default` (the requested or default isometric angles, fitted to the model), `FlatDefault` (a model that is
  entirely 2D and nothing chose a view: a top-down orthographic drawing), `Options` (the view angles were set in
  `ScadRenderOptions`), `Document` (the document or an override defined a viewport variable), and `Camera` (the
  caller supplied `ScadRenderOptions.Camera`).
- **`ScadViewportPlan`** (internal sealed class, in `ScadViewport.cs`) — the viewport decisions made before any
  evaluation, derived from the options alone: `Defaults` (the `ScadViewportValues` the document sees in the
  variables it does not assign), `ExplicitOverrides` (the variables an explicit option fixes), `Camera`,
  `AnglesExplicit`, `ProjectionExplicit`, and the requested `Azimuth`, `Elevation`, and `Projection`.
- **`ScadFontResolutionOptions`** (sealed class, init-only) — `UseSystemFonts` (default `true`) and
  `AdditionalDirectories` (`IReadOnlyList<string>`, default empty; a null list is an `ArgumentNullException`, a
  null, empty, or white-space entry an `ArgumentException`, both on initialization; a private copy is taken and
  exposed read-only). Where an explicitly named `text` font is found is specified in _ScadTextGeometry Unit
  Design_.
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
  flat-shape thickness, strict mode, and the token), and the **same** sink. The evaluation step is given the viewport
  plan (`ScadViewport.Plan`) so that the document sees `$vpt`, `$vpr`, `$vpd`, and `$vpf` (a scene has no camera,
  so only the defaults and the explicit options of the plan apply). The plan is derived from the render options: an
  explicit `Camera` sets all four variables, explicit view angles set `$vpr`, both beating an override of the
  evaluation options and a top-level assignment of the document, and otherwise the defaults of the default view
  apply. An explicit `Camera` counts only when the renderer's own rules accept it (the same empty 1 by 1 scene probe
  that `Render` uses, so there is one definition of an invalid camera; `IsUsableCamera`): a camera that is rejected
  (for example eye equal to target) defines none of the four variables, the plan is built as without a camera
  (explicit view angles still define `$vpr`, otherwise the defaults apply), and `BuildScene` never throws for it,
  whereas `Render` throws `ArgumentException`. A model that reads these variables (to size or place geometry)
  therefore builds a different scene for a
  different camera or view angle, as `Render` does for the same options. Besides the evaluation options, the
  flat-shape thickness, the highlight and ghost colors, and the font resolution, the camera and the view angles
  therefore matter to `BuildScene`; `Projection` and the surface size do not (they change no variable). On success it
  delivers the buffered
  events of both stages once, on the calling thread, in order (a handler exception propagates) and returns the
  scene; on any failure the events recorded so far are still delivered and a handler exception on that path is
  ignored so that the failure wins. A model without geometry gives a scene with an empty root. `BuildScene` never
  chooses a camera and never calls `ResolvedViewHandler`.
- `ScadDocument.Render(int width, int height, ScadRenderOptions? options = null, CancellationToken
  cancellationToken = default)` — first validates the size (`ArgumentOutOfRangeException` for a width or height
  outside 1 to `Surface.MaxDimension`), then the token, then the camera (an invalid camera is rejected by
  rendering an empty 1 by 1 scene with it, so the renderer's own rules decide, with `ArgumentException`), all
  before any evaluation. It then plans the viewport, builds the geometry as `BuildScene` does (the build also
  returns the viewport variables the document defined and whether the whole model is 2D), installs a renderer
  unsupported-feature handler **only when** the evaluation options have a handler or strict mode (the renderer
  checks boolean operands only when it has one) that maps each report to a `scad-` identifier and to the source
  location of the module that produced the mesh (through the node-to-location map), chooses the camera with
  `ScadViewport.ChooseCamera` (see below), calls `ResolvedViewHandler` once with the view that was chosen (on the
  calling thread, after the build and before drawing; an exception of the handler propagates after the events were
  delivered, like any failure), copies the scene options with that handler (`ScadSceneOptions.Create`, which
  substitutes ambient light of intensity 1 when the view is the flat default and the caller set no lights), and
  draws with `SceneRenderer.Render` and the token. The
  renderer's fragment-budget overflow (`RenderBudgetExceededException`) becomes a `ScadEvaluationException`
  advising to raise `SceneOptions.MaxFragmentsPerTile` or to simplify the model. Events are delivered as for
  `BuildScene`; when a handler throws after a successful drawing, the surface is disposed before the exception
  propagates. The caller owns and disposes the returned surface.
- `ScadViewport.Plan(ScadRenderOptions?, bool useCamera = true)` (internal static) — derives the `ScadViewportPlan`
  from the options; `useCamera` is false when the renderer rejected the camera, and the plan is then the one of an
  options object without a camera.
  `Defaults` are the values of the requested view (`FromOrbit(azimuth, elevation)`: `$vpt = [0, 0, 0]`,
  `$vpr = [90 - elevation, 0, azimuth]` with every angle wrapped into 0 to 360 (an angle within 1e-9 of a full turn
  becomes 0, so rounding noise never shows as 359.999999999), `$vpd = 140`, `$vpf = 22.5`), or, for an explicit
  `Camera`, `FromCamera(camera)`. `ExplicitOverrides` holds all four variables for an explicit camera, only `$vpr`
  when the view angles were set explicitly (even to their defaults), and nothing otherwise; `Projection` alone
  never fixes a variable. `ScadViewport.ForEvaluation()` is the plan of `Evaluate`, which has no view: the literal
  defaults `$vpr = [55, 0, 25]`, `$vpt = [0, 0, 0]`, `$vpd = 140`, `$vpf = 22.5`.
  `MergeOverrides(user, plan)` adds the explicit overrides to the caller's `ScadEvaluationOptions.Variables`
  overrides; the explicit option **replaces** an override of the same name. The merged dictionary is what the
  evaluator injects as top-level overrides, and `plan.Defaults` becomes the default of the four variables.
- **Mapping between the camera and the variables** (`FromOrbit`, `FromCamera`, `ToCamera`, `CameraToWorld`). The
  orbit view of `Camera.FitToBounds` (azimuth `az`, elevation `el`) is `$vpr = [90 - el, 0, az]`. In general `$vpr`
  is `[a, b, c]` such that the rotation that takes the camera's own axes (right = +X, camera up = +Y, toward the
  viewer = +Z) into the world is `R = Rz(c) * Ry(b) * Rx(a)`, composed like `rotate($vpr)`, so
  `translate($vpt) rotate($vpr)` places an object in the plane of the screen, facing the camera. For an arbitrary
  camera (eye, target, up) `FromCamera` builds the columns of `R` (right, camera up, toward the viewer; the up hint
  is replaced by an alternate axis when the view direction is parallel to it, the rule of the renderer) and
  decomposes it: `b = asin(-R20)`, `a = atan2(R21, R22)`, `c = atan2(R10, R00)`, and, in the gimbal-lock case
  (`|cos b| < 1e-9`), `c = 0` and `a = atan2(-R12, R11)`; the angles are wrapped to 0 to 360. `$vpt` is the target,
  `$vpd` the distance `|eye - target|`, and `$vpf` the `FieldOfViewDegrees` of the camera. `ToCamera` is the inverse:
  the eye is `$vpt + R * (0, 0, 1) * $vpd`, the target `$vpt`, the up vector `R * (0, 1, 0)`, the field of view
  `$vpf`, and the orthographic height `2 * $vpd * tan($vpf / 2)`.
- **Precedence** (per variable, highest first). (1) An explicit option of `ScadRenderOptions`: `Camera` supplies all
  four variables (only when the renderer accepts it; a camera it would reject supplies nothing); explicitly set
  `ViewAzimuthDegrees` or `ViewElevationDegrees` set `$vpr` only; `Projection` only
  changes the projection. (2) An override from `ScadEvaluationOptions.Variables` (a `-D` of the command line). (3) A
  top-level assignment in the main file and its `include`d files (not in a `use`d library and not in a nested
  scope). (4) The defaults above. The first two are injected into the evaluator as overrides, so they also win
  over an assignment in the file; the evaluator reads back what the document ended up with (see
  _ScadEvaluator Unit Design_).
- `ScadViewport.ChooseCamera(plan, document, isFlatModel, scene, aspectRatio, out view)` (internal static) — chooses
  the camera after the build, from the bounds of the scene, and reports the `ScadResolvedView`.
  - An explicit `Camera` is used as given (`Source = Camera`).
  - The document is **manual** when it (or an override) defined `$vpt`, `$vpd`, or `$vpf`: the camera is
    `ToCamera` of the values that are defined, the others keeping their defaults, and nothing is fitted to the
    model; `$vpr` falls back to the requested view or, for a flat model with no `$vpr`, to a top view (`[0, 0, 0]`).
  - Otherwise the model is fitted. The orientation comes from the explicit angles, else from a `$vpr` of the
    document, else from the flat top view (azimuth 0, elevation 90) of a model whose whole result is 2D, else from
    the requested or default view. A `$vpr` that is an orbit rotation (`$vpr.y = 0` and `$vpr.x` between 0 and 180)
    uses the same fit as the angles `[az = $vpr.z, el = 90 - $vpr.x]`; any other `$vpr` (a roll, a rotation about
    Y) uses a generalized fit with the same 5 % margin (the box is inflated by the margin and the distance, or the
    orthographic height, is chosen so that the most demanding corner touches the frame).
  - The projection is orthographic for the flat default unless `Projection` was set explicitly; otherwise it is
    the requested one (perspective by default).
  - `Source` is `Options` for explicit angles, `Document` when the document or an override defined a variable,
    `FlatDefault` for the flat top view, and `Default` otherwise.
  - A scene whose bounds are not usable frames the unit cube instead of failing, as before.
- **Flat default view.** A model whose **whole** result is 2D (`ScadBuiltScene.IsFlatModel`, true when the top-level
  result of the geometry builder is a 2D shape; a union that mixes 2D and 3D is a 3D model and keeps today's
  behavior and warning) with no explicit camera, no explicit angles, and no `$vpr` from the document or an override
  is drawn from the top (azimuth 0, elevation 90), orthographic unless `Projection` was set, fitted with the 5 %
  margin, and lit by ambient light of intensity 1 only (unless `SceneOptions.Lights` is set), so every shape is
  exactly its `color()` or the neutral gray 180. The slabs of the 2D shapes and their thickness are unchanged, and
  `BuildScene` chooses no camera, so this view changes no result of `BuildScene`; only the viewport variables that
  the options define (above) can make a built scene depend on the view.
- **Circularity (documented and accepted).** The camera is chosen after the build, from the bounds of the built
  scene, so the document sees only the defaults of the variables it does not assign, and the fit includes the
  geometry that the variables placed (for example a heads-up display). The document also cannot see that the model
  turned out to be entirely 2D: it sees the default 3D `$vpr`, and the flat top view is applied afterwards. A second
  evaluation pass that would resolve this is out of scope.
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
  build. A viewport variable with an unusable value is a warning of the evaluator and is ignored (see
  _ScadEvaluator Unit Design_); `ResolvedViewHandler` runs on the calling thread and an exception it throws
  propagates like any other failure. Cancellation is never wrapped.

### Dependencies

- **`ScadLanguage` subsystem** (internal, namespace `DemaConsulting.CanvasNet.Scad.Language`) —
  `ScadParser.Parse(string, string?)` (which tokenizes with `ScadLexer` and parses), the `ScadProgram`
  syntax tree and its statement nodes (`ScadDirectiveStatement`, `ScadModuleDefinition`,
  `ScadFunctionDefinition`), and the constants and factory behind the source-length limit
  (`ScadParser.MaxSourceLength`, `ScadParser.CreateSourceTooLongException`). See _ScadLanguage_
  (`scad-language.md`).
- **`ScadEvaluation` subsystem** (internal, namespace `DemaConsulting.CanvasNet.Scad.Evaluation`) —
  `ScadEvaluator.Evaluate`, `ScadEvaluationSettings` (including its `Viewport` defaults), `ScadEvaluationSink`,
  `ScadEvaluationOutput` (including the `Viewport` the document defined), `ScadViewportValues`,
  `ScadDocumentViewport`, and `ScadValue.TryFromObject` (for the `Variables` of the evaluation options). See
  _ScadEvaluation_ (`scad-evaluation.md`).
- **`ScadGeometry` subsystem** (internal, namespace `DemaConsulting.CanvasNet.Scad.Geometry`) —
  `ScadGeometryBuilder.Build`, `ScadGeometrySettings`, `ScadBuiltScene` (including `IsFlatModel`), `ScadSceneMap`,
  `ScadReporter`, and `ScadSceneOptions` (the copy of the renderer options, the ambient-only lights of a flat
  drawing, and the report-identifier mapping). See _ScadGeometry_ (`scad-geometry.md`).
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
`ScadDocumentRenderCsgTests.cs`, `ScadDocumentRenderExtrusionTests.cs`, `ScadDocumentRenderReportTests.cs`,
`ScadViewportTests.cs`, and `ScadDocumentViewportRenderTests.cs` verify the individual types and the geometry and
rendering entry points. The command-line tool reads `ScadRenderOptions.ResolvedViewHandler` and
`ScadResolvedView` to report the view it used (see _SourceLoader Unit Design_ in `canvas-net-tool`).

### Image Source

`ScadImageSource` (`ScadImageSource.cs`) is the `IImageSource` of the package for `.scad` files, so that a document can
link to a model (an SVG `image`, for example) through the shared `FolderImageResolver`. `Render(request)` parses the
model with `ScadDocument.Open(request.OpenContent(), new ScadOpenOptions { BaseDirectory = request.Directory, SourceName
= request.FileName })`, so the `include` and `use` directives resolve in the folder of the file and are confined there
by the library; draws it with `ScadDocument.Render` at the size `request.SizeFor(800, 800)` gives (a model has no
natural size, so 800 by 800 pixels, the `NaturalSize` constant, when none is requested), checking that size first with
`request.EnsureWithinBudget(width, height)` (a size that does not fit in the budget of the request is refused with `ImageSourceBudgetException`
before the model is parsed or any surface is allocated), from the default isometric view
with an orthographic projection, with a transparent `SceneRenderOptions.BackgroundColor` so the embedding document shows
through; and passes the cancellation token to the render. The warnings of the library become `scad-warning` notes, its
unsupported features notes with the feature name as the code (the request's `ThrowOnUnsupportedFeature` makes the
library throw instead), and its `echo` output the echo lines of the result. A missing `include` file is a `scad-warning`
note, not an error, and a request without a folder fails a model that uses `include` with `ScadEvaluationException`. The
class is stateless, thread-safe, and claims no URI scheme; it references only the core package (and the Render3D types
the package already uses).

The behavior of the class is specified by the `CanvasNetScad-ScadDocument-ImageSource` requirement and verified by
`ScadImageSourceTests.cs`. Its **callers** are the shared `FolderImageResolver` of the core package (through
`IImageSource`), the `CanvasNetViewer` system, which lists it with the other built-in sources, and applications. The
unit depends on the core package only through the `ImageSources` contract types.

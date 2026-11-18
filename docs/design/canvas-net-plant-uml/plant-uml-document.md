## PlantUmlDocument

![CanvasNetPlantUml Structure](CanvasNetPlantUmlView.svg)

The `PlantUmlDocument` unit is the public API of the system, a direct unit of `CanvasNetPlantUml`. Its source files are
in the root of `src/DemaConsulting.CanvasNet.PlantUml/`: `PlantUmlDocument.cs` (the entry points and the metadata),
`PlantUmlDocument.Render.cs` (the measure and render entry points), `PlantUmlDocument.Pipeline.cs` (the pipeline seam),
`PlantUmlOpenOptions.cs`, `PlantUmlRenderOptions.cs`, `PlantUmlLimits.cs`, `PlantUmlLocation.cs`,
`PlantUmlDiagnostic.cs`, `PlantUmlUnsupportedFeature.cs`, `PlantUmlSize.cs`, `PlantUmlDiagnosticCodes.cs`,
`PlantUmlSyntaxException.cs`, `PlantUmlRenderException.cs`, `PlantUmlUnsupportedFeatureException.cs`,
`PlantUmlReportSink.cs`, `PlantUmlImageSource.cs`, and `NamespaceDoc.cs`.

### Purpose

`PlantUmlDocument` gives a consumer one validated, immutable object for a PlantUML diagram: it reads the text from a
string, a file, or a stream with the same rules, detects the diagram type, parses a supported type, exposes what the
diagram declares without drawing it, and measures and renders it. Its records, codes, and exceptions are the vocabulary
in which every part of the system reports problems, so that a host can react to a stable code and present a location as
editors do. The unit's behavior is specified by the `CanvasNetPlantUml-PlantUmlDocument-*` requirements in
`docs/reqstream/canvas-net-plant-uml/plant-uml-document.yaml`.

### Data Model

- `PlantUmlDocument` - `SourceName` (the name in every location, or null), the internal `Source`
  (`PreprocessedSource`), `Detected` (`DetectedDiagram`), and `Diagram` (the parsed `IPlantUmlDiagram` of a supported
  type, or null); the public `DiagramType`, `IsSupported`, `Title`, `NodeCount`, `EdgeCount`, and `SubgraphCount` read
  from them (null and zero for a type that is not drawn).
- `PlantUmlOpenOptions` - `SourceName` (null or not blank), `Limits` (null for the defaults), `BaseDirectory` (null or
  not blank; where the include directives start, the folder of an opened file by default), and `FileAccess` (not null;
  the policy of the include files, `FileAccessPolicy.AllowAll` by default).
- `PlantUmlImageSource` - the `IImageSource` named `plantuml` for `.puml`, `.plantuml`, and `.pu`, with no scheme and
  no fragment parameters; `WarningCode` is `plantuml-warning`.
- `PlantUmlRenderOptions` - `Scale` (finite, positive, at most `Surface.MaxDimension`), `Background` (null for the
  diagram's own background), `Limits` (not null), `WarningHandler`, `UnsupportedFeatureHandler`, and
  `ThrowOnUnsupportedFeature`. The options of a measure or render call.
- `PlantUmlLimits` - `MaxTextSize` (50,000), `MaxNodes` (2,000), `MaxEdges` (500), `MaxLabelLength` (4,096),
  `MaxNestingDepth` (32, at most 256), `MaxIncludeDepth` (16, at most 64), `MaxIncludes` (64), `MaxLayoutWork`
  (50,000,000), `MaxPaintedPixels` (250,000,000), and `MaxDiagnostics` (1,000); every value is validated when it is set.
- `PlantUmlLocation(File, Line, Column)` - one-based, `IsKnown` when the line is not zero; formatted
  `file:line:column`.
- `PlantUmlDiagnostic(Code, Message, Location)` and `PlantUmlUnsupportedFeature(Feature, Message, Location)` with an
  optional `Hint`; `PlantUmlSize(Width, Height)`.
- `PlantUmlDiagnosticCodes` - the seventeen stable `plantuml-*` codes.
- `PlantUmlPendingReport(IsWarning, Code, Message, Location, Hint)` - a report found while reading, replayed into the
  sink of every call.
- `IPlantUmlDiagram` - the seam to a parsed diagram: `Title`, `NodeCount`, `EdgeCount`, `SubgraphCount`, the pending
  `Reports` of its parser, and `Layout(DiagramRenderContext)`, which returns the scene of a call.
- `DiagramRenderContext(Options, Sink, Text, CancellationToken)` - the options, the report sink, the text layout, and
  the token of one call.
- `UnsupportedDiagramSize` - 20, the side of the empty picture of a type that is not drawn (twice PlantUML's document
  margin of 10).

### Key Methods

- `Parse(string, PlantUmlOpenOptions?)` - preprocesses, detects, and (for a supported type) parses the text through
  `DiagramRegistry.Parse`. Precondition: the text is not null. Postcondition: an immutable document.
- `Open(string path, ...)` and `Open(Stream, ...)` - read the bytes in chunks of 8,192 bytes, stop as soon as the byte
  count certainly exceeds three bytes per character of four times `MaxTextSize`, drop a UTF-8 byte-order mark, decode
  strictly, and continue as `Parse`; a file's source name defaults to its path, and its include directives start in
  its folder (its full path is the referrer of the policy and counts as already included). Every entry point creates
  one `IncludeResolver` for the call from `BaseDirectory` and `FileAccess`.
- `PlantUmlImageSource.Render(request)` - opens the bytes of the request with its file name, folder (the base directory
  of the includes), and file-access policy; measures the natural size without handlers; sizes the picture with
  `request.SizeFor` and checks the budget; renders over a transparent background with handlers that turn a warning into
  a `plantuml-warning` note (the specific code in `Detail`, the location) and an unsupported feature into a note with
  its code; strict mode follows the request.
- `Measure(options, token)` - runs the pipeline and returns the scaled size: the view box rounded up to whole pixels
  (with a tolerance of 0.000001), times the scale of the call and the diagram's own scale, rounded half away from zero;
  a side outside 1 to `Surface.MaxDimension` throws `ArgumentOutOfRangeException`.
- `Render(options, token)` - the same size, the scene placed uniformly at the product of the two scales and painted;
  `Render(width, height, options, token)` validates the size first and fits the scene. `LayoutScene` and
  `MeasureNatural` are the internal forms that return the scene and the natural size.
- `Run` - creates the sink (capped by `MaxDiagnostics`, strict per the options) and the context with a new
  `TextLayout`, builds the scene, finishes the result, and delivers the reports once: after a failure, ignoring handler
  failures; after success, disposing the result if a handler throws.
- `BuildScene` - replays the pending reports of reading (the preprocessor's, then the diagram parser's), then calls the
  diagram's `Layout`, mapping a defect (`IsLayoutDefect`: a null reference, an index, key, cast, argument, arithmetic,
  or invalid-operation exception that is not a documented failure) to a located `plantuml-render-failed` error with the
  defect as its inner exception; a type that is not drawn reports `plantuml-diagram-unsupported` with the hint of the
  detector and gets an empty scene.
- `PlantUmlReportSink.Warn` / `Unsupported` / `Deliver` / `DeliverIgnoringHandlerFailures` - record the reports of one
  call in order, drop duplicates, keep at most `MaxDiagnostics` (the first overflow becomes one
  `plantuml-diagnostics-suppressed` notice), throw in strict mode after recording the report, and deliver once on the
  calling thread.

### Error Handling

Null, empty, and unreadable arguments throw `ArgumentNullException` or `ArgumentException`; a missing file throws
`FileNotFoundException` or `DirectoryNotFoundException`. An invalid render size throws `ArgumentOutOfRangeException`
before any work; a canceled token throws `OperationCanceledException`; a limit met while laying out or painting throws a
located `PlantUmlRenderException` with `plantuml-limit-exceeded`. Invalid UTF-8 is a `PlantUmlSyntaxException` with the
code `plantuml-syntax-error` at an unknown position of the named source. Text over the limit is an
`InvalidDataException` whose message names `PlantUmlLimits.MaxTextSize`. Located syntax errors of the Syntax subsystem
and of the diagram parsers propagate unchanged, including `plantuml-include-failed` (with the refusal of the file
engine as the inner exception). A handler exception after a successful call propagates; after a failed call it is
ignored so that the original failure is reported. The image source passes every exception of the document through;
a request without content throws `InvalidOperationException` and a picture over the budget `ImageSourceBudgetException`.

### Dependencies

The unit uses the `Preprocessor` and `DiagramDetector` units of the Syntax subsystem, the `TextLayout`, `SceneModel`,
and `ScenePainter` units of the Scenegraph subsystem, and the core `Canvas` (`Surface`, `Rgba32`), `Files`
(`FileAccessPolicy`), and `ImageSources` (`IImageSource`, `ImageSourceRequest`, `ImageSourceResult`,
`ImageSourceDiagnostic`) subsystems.

### Callers

Consumers of the package (the Viewer and the tool, through their image sources) call the public API; the image
resolvers of the other formats call `PlantUmlImageSource` for a linked diagram; the Syntax subsystem and the diagram
parsers raise its exceptions and pending reports.

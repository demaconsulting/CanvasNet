## PlantUmlDocument

![CanvasNetPlantUml Structure](CanvasNetPlantUmlView.svg)

The `PlantUmlDocument` unit is the public API of the system, a direct unit of `CanvasNetPlantUml`. Its source files are
in the root of `src/DemaConsulting.CanvasNet.PlantUml/`: `PlantUmlDocument.cs` (the entry points and the metadata),
`PlantUmlDocument.Pipeline.cs` (the pipeline seam), `PlantUmlOpenOptions.cs`, `PlantUmlRenderOptions.cs`,
`PlantUmlLimits.cs`, `PlantUmlLocation.cs`, `PlantUmlDiagnostic.cs`, `PlantUmlUnsupportedFeature.cs`, `PlantUmlSize.cs`,
`PlantUmlDiagnosticCodes.cs`, `PlantUmlSyntaxException.cs`, `PlantUmlRenderException.cs`,
`PlantUmlUnsupportedFeatureException.cs`, `PlantUmlReportSink.cs`, and `NamespaceDoc.cs`.

### Purpose

`PlantUmlDocument` gives a consumer one validated, immutable object for a PlantUML diagram: it reads the text from a
string, a file, or a stream with the same rules, detects the diagram type, parses a supported type, and exposes what
the diagram declares without drawing it. Its records, codes, and exceptions are the vocabulary in which every part of
the system reports problems, so that a host can react to a stable code and present a location as editors do. The
unit's behavior is specified by the `CanvasNetPlantUml-PlantUmlDocument-*` requirements in
`docs/reqstream/canvas-net-plant-uml/plant-uml-document.yaml`.

### Data Model

- `PlantUmlDocument` - `SourceName` (the name in every location, or null), the internal `Source`
  (`PreprocessedSource`), `Detected` (`DetectedDiagram`), and `Diagram` (the parsed `IPlantUmlDiagram` of a supported
  type, or null); the public `DiagramType`, `IsSupported`, `Title`, `NodeCount`, `EdgeCount`, and `SubgraphCount` read
  from them (null and zero for a type that is not drawn).
- `PlantUmlOpenOptions` - `SourceName` (null or not blank) and `Limits` (null for the defaults).
- `PlantUmlRenderOptions` - `Scale` (finite, positive, at most `Surface.MaxDimension`), `Background` (null for the
  diagram's own background), `Limits` (not null), `WarningHandler`, `UnsupportedFeatureHandler`, and
  `ThrowOnUnsupportedFeature`. The options of a measure or render call; the calls themselves are added with the
  Scenegraph subsystem.
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
- `IPlantUmlDiagram` - the seam to a parsed diagram: `Title`, `NodeCount`, `EdgeCount`, `SubgraphCount`, and the
  pending `Reports` of its parser.

### Key Methods

- `Parse(string, PlantUmlOpenOptions?)` - preprocesses, detects, and (for a supported type) parses the text through
  `DiagramRegistry.Parse`. Precondition: the text is not null. Postcondition: an immutable document.
- `Open(string path, ...)` and `Open(Stream, ...)` - read the bytes in chunks of 8,192 bytes, stop as soon as the byte
  count certainly exceeds three bytes per character of four times `MaxTextSize`, drop a UTF-8 byte-order mark, decode
  strictly, and continue as `Parse`; a file's source name defaults to its path.
- `PlantUmlReportSink.Warn` / `Unsupported` / `Deliver` / `DeliverIgnoringHandlerFailures` - record the reports of one
  call in order, drop duplicates, keep at most `MaxDiagnostics` (the first overflow becomes one
  `plantuml-diagnostics-suppressed` notice), throw in strict mode after recording the report, and deliver once on the
  calling thread.

### Error Handling

Null, empty, and unreadable arguments throw `ArgumentNullException` or `ArgumentException`; a missing file throws
`FileNotFoundException` or `DirectoryNotFoundException`. Invalid UTF-8 is a `PlantUmlSyntaxException` with the code
`plantuml-syntax-error` at an unknown position of the named source. Text over the limit is an `InvalidDataException`
whose message names `PlantUmlLimits.MaxTextSize`. Located syntax errors of the Syntax subsystem and of the diagram
parsers propagate unchanged. A handler exception after a successful call propagates; after a failed call it is
ignored so that the original failure is reported.

### Dependencies

The unit uses the `Preprocessor` and `DiagramDetector` units of the Syntax subsystem, and the core `Canvas` subsystem
(`Rgba32`, `Surface.MaxDimension`) for the render options.

### Callers

Consumers of the package (the Viewer and the tool, through their image sources) call the public API; the Syntax
subsystem and the diagram parsers raise its exceptions and pending reports.

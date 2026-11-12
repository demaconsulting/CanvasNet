## MermaidDocument

![CanvasNetMermaid Structure](CanvasNetMermaidView.svg)

`MermaidDocument` is the one direct unit of `CanvasNetMermaid` and its whole public API, in the root namespace
`DemaConsulting.CanvasNet.Mermaid` (folder `src/DemaConsulting.CanvasNet.Mermaid/`). Its source files are
`MermaidDocument.cs` (entry points and metadata), `MermaidDocument.Render.cs` (`Measure`, `Render`, the per-call
pipeline), `MermaidDocument.Pipeline.cs` (the internal seam `IMermaidDiagram`, `DiagramRenderContext`, and
`DiagramRegistry`), `MermaidOpenOptions.cs`, `MermaidRenderOptions.cs`, `MermaidLimits.cs`, `MermaidLocation.cs`,
`MermaidDiagnostic.cs`, `MermaidUnsupportedFeature.cs`, `MermaidSize.cs`, `MermaidDiagnosticCodes.cs`,
`MermaidSyntaxException.cs`, `MermaidRenderException.cs`, `MermaidUnsupportedFeatureException.cs`,
`MermaidReportSink.cs` (internal), `MermaidImageSource.cs`, and `NamespaceDoc.cs`.

### Purpose

The unit reads Mermaid text from a string, a stream, or a file, drives the `Syntax` subsystem and the diagram parser
to validate it once, exposes what the diagram declares, and on every `Measure` or `Render` call drives the
configuration, layout, and painting subsystems with the call's options. It owns the error and report contract of the
whole system (codes, locations, hints, ordered delivery, strict mode), the validation of options and limits, and the
adapter to the CanvasNet image-source contract. Opening never opens another file, never loads a picture, and never
lays out the diagram.

### Data Model

- **`MermaidDocument`** (sealed, immutable, thread-safe): the `PreprocessedSource` (lines, code, parser text,
  frontmatter, directives, and the location map), the `DetectedDiagram` (Mermaid id, public type, keyword, support,
  location), the parsed diagram (`IMermaidDiagram`, null for a type this version does not draw), and the source name.
  Properties: `SourceName`, `DiagramType` (Mermaid's id with `class`, `state`, and `flowchart` aliases folded),
  `IsSupported`, `Title` (the diagram's own title, else the frontmatter title; when the diagram's `IsTitleSet` is true,
  default false on `IMermaidDiagram`, its title is used even when null, so a title that sanitizes to empty leaves none,
  as drawn), `AccTitle`, `Direction`, `NodeCount`, `EdgeCount`, and `SubgraphCount` (zero and null for an unsupported
  type). The counts are the parsed diagram's and
  are fixed when the text is opened, under the configuration of the text itself (its frontmatter and directives): for
  a class diagram `NodeCount` is the classes, `EdgeCount` the relations, and `SubgraphCount` the namespaces that are
  drawn under that configuration (with `class.hierarchicalNamespaces` false, only the declared namespaces, so
  `namespace Geo.Plane` counts once); for a state diagram `NodeCount` is the states drawn as nodes (plain states,
  states with descriptions, start and end states, forks, joins, and choices, but not notes, note groups, or
  concurrency regions), `EdgeCount` the transitions (not the links of notes), and `SubgraphCount` the composite states
  (not the concurrency regions or note groups); for an entity relationship diagram `NodeCount` is the entities drawn
  (an entity named after a subgraph is not drawn), `EdgeCount` the relationships, and `SubgraphCount` the subgraphs.
  The `Direction` of a state or entity relationship diagram is the last top-level `direction` statement (the direction
  the diagram is laid out in), else `TB`. For a pie chart `NodeCount` is the sections (each label once), and for a
  gantt chart the tasks (vertical markers included) with `SubgraphCount` the sections whose titles are drawn; both
  have no edges and a null `Direction`. For a mindmap `NodeCount` is the nodes and `EdgeCount` one fewer (0 for an
  empty mindmap), with no subgraphs, a null `Direction`, and the frontmatter title as its only title. For a packet
  diagram `NodeCount` is the block statements (a block split across two rows counted once), with no edges or
  subgraphs and a null `Direction`. For a git graph `NodeCount` is the commits and `EdgeCount` the links to their
  parents, with no subgraphs, counted with the main branch of the text's own configuration, and `Direction` is `LR`,
  `TB`, or `BT`. A site-level
  setting in `MermaidRenderOptions.Config` is not known when the
  text is opened, so it changes the drawing but not the counts, as for every other descriptive property.
- **`MermaidOpenOptions`**: `SourceName` (not white space) and `Limits`.
- **`MermaidRenderOptions`**: `Scale` (finite, above zero, 1 by default), `Background` (`Rgba32?`, null for
  transparent), `Config` (a JSON object text of at most a fixed length and depth, validated when set), `Theme` (one of
  the static `ThemeNames`: `default`, `base`, `dark`, `forest`, `neutral`, compared exactly; hosts canonicalize a user's
  spelling first), `ImageResolver`, `Now` (`DateTimeOffset?`, null by default: the only clock of the library, which a
  gantt chart reads for its today line and the dates Mermaid takes from today), `Limits` (non-null,
  `MermaidLimits.Default` by default), `WarningHandler`, `UnsupportedFeatureHandler`, and `ThrowOnUnsupportedFeature`.
- **`MermaidLimits`**: the ten limits of the system design (_Untrusted Input and Limits_), each validated to be at
  least 1, the two depth limits at most 256; `Default` is a shared instance.
- **Records**: `MermaidLocation(File, Line, Column)` (`IsKnown` when the line is positive; `ToString` gives
  `file:line:column`, `line:column`, or the file alone); `MermaidDiagnostic(Code, Message, Location)` and
  `MermaidUnsupportedFeature(Feature, Message, Location)` with an optional `Hint`; `MermaidSize(Width, Height)`.
- **Exceptions**: `MermaidSyntaxException` (`FormatException`; `Code`, `Location`, `Hint`, `Expected`),
  `MermaidRenderException` (`InvalidOperationException`; `Code`, `Location`, `Hint`), and
  `MermaidUnsupportedFeatureException` (`IOException`; `Feature`, `Location`, `Hint`). The message of a located
  exception is `location: code: description`; the standard constructors use the general code.
- **`MermaidReportSink`** (internal, one per call): the reports in occurrence order across both kinds, two sets for
  exact de-duplication, the cap (`MaxDiagnostics`), the strict flag, and whether the suppression notice was added.
- **`MermaidImageSource`**: stateless; `Extensions` `.mmd` and `.mermaid`, no schemes, `FragmentParameters`
  `theme`, `WarningCode` `mermaid-warning`.

### Key Methods

- **`Parse(string text, MermaidOpenOptions? options)`**: runs `Preprocessor.Run` (which checks the raw length of four
  times `MaxTextSize` and then `MaxTextSize`), detects the diagram with `DiagramDetector.Detect`, resolves the text's
  own configuration (`MermaidConfigResolver.Resolve` without options and without a sink: the defaults, the frontmatter,
  and the directives), detects again when that configuration chooses another renderer (for example
  `flowchart.defaultRenderer: elk`), and asks `DiagramRegistry.Parse(detected, source, limits, textConfig)` for the
  diagram. The registry dispatches on the public type: `flowchart` to `FlowchartParser` and `FlowchartModel`,
  `sequence` to `SequenceParser` and `SequenceModel`, `class` (from the `classDiagram` and legacy `class`
  detectors, so `classDiagram` and `classDiagram-v2` texts alike) to `ClassParser` and `ClassModel`, to which it
  passes the text configuration's `class.hierarchicalNamespaces` so that the namespace count follows what is drawn,
  `state` (from the `stateDiagram` and legacy `state` detectors, so `stateDiagram` and `stateDiagram-v2` texts alike,
  whichever state renderer the configuration names) to `StateParser` and `StateModel`, and `er` (from the `er`
  detector, an `erDiagram` text) to `ErParser` and `ErModel`, `pie` to `PieParser` (whose `PieDiagram` implements
  `IMermaidDiagram`), `gantt` to `GanttParser` and `GanttModel`, `mindmap` to `MindmapParser` (whose
  `MindmapDiagram` implements `IMermaidDiagram`), `packet` to `PacketParser` (whose `PacketDiagram` implements
  `IMermaidDiagram`), and `gitGraph` to `GitGraphParser` (whose `GitGraphDiagram` implements `IMermaidDiagram`), with
  the text's own configuration, whose `gitGraph.mainBranchName` and `gitGraph.mainBranchOrder` the statements run
  with. A
  known type that is not drawn is not parsed (the document's diagram is null). Postcondition: an immutable document,
  or an exception.
- **`Open(string path, ...)`**: opens the file for reading and reads it as a stream; the document is named by the
  path unless the options name it. **`Open(Stream, ...)`**: reads in chunks, stopping as soon as the text is certainly
  over the limit, decodes with the strict UTF-8 decoder (no replacement characters), drops a byte-order mark, and
  leaves the stream open.
- **`Measure(options, cancellationToken)`**: runs the pipeline to a scene and returns the natural size: the view box
  rounded up to whole pixels (at least 1, with a 1e-6 tolerance, as `mmdc` rounds), multiplied by `Scale`, rounded
  half away from zero, and checked to lie within 1 and `Surface.MaxDimension`.
- **`MeasureNatural(options, cancellationToken)`** (internal): as `Measure` at scale 1 but without the check against
  `Surface.MaxDimension` (each side capped at `int.MaxValue`; a view box that is not finite fails with a located
  `mermaid-render-failed`), for `MermaidImageSource`, which needs only the aspect ratio of a diagram that may be
  larger than the largest surface.
- **`Render(options, cancellationToken)`**: as `Measure`, then paints with `ScenePlacement.Uniform` at that size.
  **`Render(width, height, ...)`**: validates the size and paints with `ScenePlacement.Fit` (uniform scale, centered).
- **`Run`** (private): creates the call's `MermaidReportSink`, resolves the configuration with
  `MermaidConfigResolver.Resolve` (silently for a type that is not drawn), creates the `DiagramRenderContext` (source,
  detected diagram, options, configuration, sink, a fresh `FontResolver`, token), builds the scene, and delivers the
  reports: after success with `Deliver` (a handler's exception propagates and the surface is disposed), after a
  failure with `DeliverIgnoringHandlerFailures` (the original failure wins).
- **`BuildScene`** (private): `Diagram.Layout(context)` for a supported diagram; otherwise records one
  `mermaid-diagram-unsupported` report at the keyword with `DiagramDetector.UnsupportedHint` and returns an empty
  16 by 16 scene. The layout is wrapped in the defect net: an exception that `IsLayoutDefect` accepts (a
  `NullReferenceException`, `IndexOutOfRangeException`, `KeyNotFoundException`, `InvalidCastException`,
  `ArgumentException`, `ArithmeticException`, or an `InvalidOperationException` other than
  `MermaidRenderException` and `ObjectDisposedException`, and never one marked by `NodeImages` as the caller's
  resolver's) becomes a `MermaidRenderException` (`mermaid-render-failed`) located at the keyword, with the
  original as its inner exception.
- **`MermaidImageSource.Render(ImageSourceRequest)`**: decodes the content, opens the document named by the file name,
  reads the `theme` fragment (refusing an unknown theme with `ImageSourceRefusedException` and the fragment-invalid
  code), takes the natural size from `MeasureNatural` (so a requested size smaller than the largest surface works
  for a diagram whose natural size is larger), renders at the size `ImageSourceRequest.SizeFor` gives (each side
  at most `Surface.MaxDimension`, the diagram fitted and centered) over a transparent background with the request's
  resolver, strict flag, and token, and maps warnings to `mermaid-warning` diagnostics (`Detail` is the specific code)
  and unsupported features to unsupported diagnostics with their location.
- **`MermaidReportSink.Warn`/`Unsupported`/`Deliver`**: record a report unless it is an exact duplicate, add one
  `mermaid-diagnostics-suppressed` notice at the cap, throw `MermaidUnsupportedFeatureException` after recording in
  strict mode, and deliver in order.

### Error Handling

- Argument errors (null text, stream, or path; an unreadable stream; an invalid option value) throw the standard
  argument exceptions when the call or the option setter runs.
- Oversized text throws `InvalidDataException` (as the Vega-Lite and SCAD packages do, so hosts map it to
  corrupt input).
- Invalid text, refused YAML, and the open-time limits throw `MermaidSyntaxException` from the Syntax, Flowchart,
  Sequence, Class, State, Er, Pie, Gantt, Mindmap, Packet, and GitGraph subsystems; the unit adds nothing but the
  source name.
- Layout, picture, and painting limits and render failures throw `MermaidRenderException` from the lower
  subsystems (for a mindmap, the Mindmap and Cose subsystems); a size outside the surface range throws
  `ArgumentOutOfRangeException` naming the scale and the size.
- Strict mode throws `MermaidUnsupportedFeatureException` from the sink. Cancellation throws
  `OperationCanceledException` from the stage that observes the token.
- An unexpected exception of the layout (a defect) is reported as a located `MermaidRenderException`
  (`mermaid-render-failed`) by `BuildScene`'s defect net; cancellation, the documented failures, and an exception
  of the caller's image resolver pass unchanged.
- Reports recorded before any failure are still delivered. Nothing is caught and swallowed except a handler's
  exception after a failure.

### Dependencies

- **Syntax**: `Preprocessor`, `DiagramDetector`, and the `MappedText` location map.
- **Configuration**: `MermaidConfigResolver` (the effective configuration, the text's own configuration, and the
  detector settings).
- **Flowchart**: through `DiagramRegistry` (`FlowchartParser`, `FlowchartModel`, which implements `IMermaidDiagram`).
- **Sequence**: through `DiagramRegistry` (`SequenceParser`, `SequenceModel`, which implements `IMermaidDiagram`).
- **Class**: through `DiagramRegistry` (`ClassParser`, `ClassModel`, which implements `IMermaidDiagram`).
- **State**: through `DiagramRegistry` (`StateParser`, `StateModel`, which implements `IMermaidDiagram`).
- **Er**: through `DiagramRegistry` (`ErParser`, `ErModel`, which implements `IMermaidDiagram`).
- **Pie**: through `DiagramRegistry` (`PieParser`, whose `PieDiagram` implements `IMermaidDiagram`).
- **Gantt**: through `DiagramRegistry` (`GanttParser`, `GanttModel`, which implements `IMermaidDiagram`); a render
  passes `Now` to the model in the render context.
- **Mindmap**: through `DiagramRegistry` (`MindmapParser`, whose `MindmapDiagram` implements `IMermaidDiagram`); its
  renderer uses the `Cose` subsystem for the layout.
- **Packet**: through `DiagramRegistry` (`PacketParser`, whose `PacketDiagram` implements `IMermaidDiagram`).
- **GitGraph**: through `DiagramRegistry` (`GitGraphParser`, whose `GitGraphDiagram` implements `IMermaidDiagram`).
- **Scenegraph**: `Scene`, `ScenePlacement`, `ScenePainter` (and `ScenePainter.CreateFontResolver`).
- **Core CanvasNet**: `Surface`, `Rgba32` (Canvas), `IImageResolver`, `IImageSource`, `IFragmentImageSource`,
  `ImageFragment`, `ImageSourceRequest`, `ImageSourceResult`, `ImageDiagnostic` (ImageSources).
- **.NET base class library**: `System.Text.Json` (option validation), `System.Text.UTF8Encoding` (strict decoding),
  `System.IO`.

### Callers

Library callers and hosts: the Viewer library (`SourceLoader.Mermaid.cs` opens, measures, and renders documents, and
`CanvasNetViewer` registers `MermaidImageSource` as a built-in source), and through it the `canvasnet` tool and the
documentation filter. Inside the system nothing calls the unit; it calls the subsystems. The unit tests reach the
internal sink through `InternalsVisibleTo`.

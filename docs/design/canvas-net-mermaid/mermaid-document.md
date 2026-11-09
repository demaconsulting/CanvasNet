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
  `IsSupported`, `Title` (the diagram's own title, else the frontmatter title), `AccTitle`, `Direction`, `NodeCount`,
  `EdgeCount`, and `SubgraphCount` (zero and null for an unsupported type).
- **`MermaidOpenOptions`**: `SourceName` (not white space) and `Limits`.
- **`MermaidRenderOptions`**: `Scale` (finite, above zero, 1 by default), `Background` (`Rgba32?`, null for
  transparent), `Config` (a JSON object text of at most a fixed length and depth, validated when set), `Theme` (one of
  the static `ThemeNames`: `default`, `base`, `dark`, `forest`, `neutral`, compared exactly; hosts canonicalize a
  user's spelling first), `ImageResolver`, `Limits` (non-null, `MermaidLimits.Default` by default), `WarningHandler`,
  `UnsupportedFeatureHandler`, and `ThrowOnUnsupportedFeature`.
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
  times `MaxTextSize` and then `MaxTextSize`), detects the diagram with `DiagramDetector.Detect`, detects again when
  the text's own configuration chooses another renderer (for example `flowchart.defaultRenderer: elk`), and asks
  `DiagramRegistry.Parse` for the diagram (`FlowchartParser` and `FlowchartModel` for a flowchart, `SequenceParser`
  and `SequenceModel` for a sequence diagram; null for a known type that is not drawn). Postcondition: an immutable
  document, or an exception.
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
- Invalid text, refused YAML, and the open-time limits throw `MermaidSyntaxException` from the Syntax, Flowchart, and
  Sequence subsystems; the unit adds nothing but the source name.
- Layout, picture, and painting limits and render failures throw `MermaidRenderException` from the lower
  subsystems; a size outside the surface range throws `ArgumentOutOfRangeException` naming the scale and the size.
- Strict mode throws `MermaidUnsupportedFeatureException` from the sink. Cancellation throws
  `OperationCanceledException` from the stage that observes the token.
- An unexpected exception of the layout (a defect) is reported as a located `MermaidRenderException`
  (`mermaid-render-failed`) by `BuildScene`'s defect net; cancellation, the documented failures, and an exception
  of the caller's image resolver pass unchanged.
- Reports recorded before any failure are still delivered. Nothing is caught and swallowed except a handler's
  exception after a failure.

### Dependencies

- **Syntax**: `Preprocessor`, `DiagramDetector`, and the `MappedText` location map.
- **Configuration**: `MermaidConfigResolver` (the effective configuration and detector settings).
- **Flowchart**: through `DiagramRegistry` (`FlowchartParser`, `FlowchartModel`, which implements `IMermaidDiagram`).
- **Sequence**: through `DiagramRegistry` (`SequenceParser`, `SequenceModel`, which implements `IMermaidDiagram`).
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

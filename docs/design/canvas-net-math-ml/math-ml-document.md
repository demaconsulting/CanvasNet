## MathMlDocument

![CanvasNetMathMl Structure](CanvasNetMathMlView.svg)

The `MathMlDocument` unit is the public API of CanvasNetMathMl, a direct unit of the system in the root namespace
`DemaConsulting.CanvasNet.MathMl`. Its source files are `MathMlDocument.cs` (the entry points and the description),
`MathMlDocument.Render.cs` (measuring, rendering, and the pipeline), `MathMlDisplay.cs`, `MathMlAnnotation.cs`,
`MathMlOpenOptions.cs`, `MathMlRenderOptions.cs`, `MathMlLimits.cs`, `MathMlLocation.cs`, `MathMlDiagnostic.cs`,
`MathMlUnsupportedFeature.cs`, `MathMlSize.cs`, `MathMlDiagnosticCodes.cs`, `MathMlSyntaxException.cs`,
`MathMlRenderException.cs`, `MathMlUnsupportedFeatureException.cs`, `MathMlReportSink.cs` (with
`MathMlPendingReport`), `MathMlImageSource.cs` (the image source), and `NamespaceDoc.cs`.

### Purpose

`MathMlDocument` is the entry point of the package: it reads an equation, describes it, and measures and renders it,
with options, limits, records, codes, and exceptions shaped like those of the other format packages of the
repository. `MathMlImageSource` is the composition hook: it draws a `.mml` file that another document links to, as the
image sources of the other formats do. The unit's behavior is specified by the `CanvasNetMathMl-MathMlDocument-*`
requirements in
`docs/reqstream/canvas-net-math-ml/math-ml-document.yaml`.

### Data Model

- `MathMlDocument` (sealed, immutable) - `SourceName`, `Display` (the `display` attribute of `math`; inline when absent
  or invalid), `ElementCount` (the MathML elements with `math`, without annotation content and skipped foreign
  elements), `AltText`, `Annotations`; internally the `Root` (`MathElement`) and the pending `Reports` of reading
  (at most `MaxDiagnostics` of the open limits, then one suppression notice).
- `MathMlDisplay` - `Inline` and `Block`. `MathMlAnnotation(Encoding, Text)` - an annotation and its encoding.
- `MathMlOpenOptions` - `SourceName` (null or non-whitespace) and `Limits` (null means `MathMlLimits.Default`).
- `MathMlRenderOptions` - `FontSize` (16 CSS pixels; finite, greater than zero, at most 4,096), `Scale` (1; finite,
  greater than zero, at most `Surface.MaxDimension`), `Display` (null keeps the document's), `Color` (opaque black),
  `Background` (null is transparent), `Limits` (not null), `WarningHandler`, `UnsupportedFeatureHandler`, and
  `ThrowOnUnsupportedFeature`.
- `MathMlLimits` - `MaxTextSize` (1,048,576), `MaxElements` (100,000, the skipped elements, every element inside them,
  and every element inside an annotation counted), `MaxNestingDepth` (256; 1 to 1,024, which bounds the recursion of
  the layout, and applies inside skipped content and annotations too), `MaxTokenLength` (10,000), `MaxLayoutWork`
  (10,000,000), `MaxPaintedGlyphs` (1,000,000), `MaxDiagnostics` (1,000, which the open limits also apply to the
  reports of reading), `MaxAssemblyParts` (1,000, the glyphs of one stretched operator), and `MaxTableCells` (100,000,
  the grid slots of one table, rows times columns after the spans); every value is at least one, validated when set.
- `MathMlLocation(SourceName, Line, Column)` - one-based, `IsKnown` when the line is not zero, printed as
  `name:line:column`. `MathMlDiagnostic(Code, Message, Location) { Hint }` and
  `MathMlUnsupportedFeature(Feature, Message, Location) { Hint }` - the reports, printed with a known location as a
  prefix. `MathMlSize(Width, Height, Baseline)` - pixels, with the baseline row from the top.
- `MathMlDiagnosticCodes` - `mathml-syntax-error`, `mathml-limit-exceeded`, `mathml-render-failed`,
  `mathml-element-unsupported`, `mathml-attribute-unsupported`, `mathml-attribute-invalid`, `mathml-css-unsupported`,
  `mathml-text-missing-glyph`, `mathml-stretch-unavailable`, `mathml-child-count`, and `mathml-diagnostics-suppressed`.
  Every element of MathML Core is drawn: `mathml-element-unsupported` reports an element outside MathML Core (an
  element of MathML 4 Full such as `mfenced`, `menclose`, or `mlabeledtr`, laid out as an `mrow` of its children as
  MathML Core lays out an unknown element), an element of another namespace or inside a token (skipped with its
  subtree), or text directly inside a layout element (drawn like an `mtext`).
- `MathMlSyntaxException` (a `FormatException`), `MathMlRenderException` (an `InvalidOperationException`), and
  `MathMlUnsupportedFeatureException` (an `IOException`) - each with a code (`Feature` for the last), a `Location`, and
  a `Hint`; the standard constructors use the default code and an unknown location.
- `MathMlReportSink` (internal, one per call) - the ordered reports, the sets that de-duplicate them, the cap, the
  strict flag, whether the suppression notice was recorded, and the count of remembered reports (`TrackedCount`).
  `MathMlPendingReport(IsWarning, Code, Message, Location, Hint)` - a report of reading, replayed into every call.
- `MathMlImageSource` (sealed, stateless, thread-safe) - `Name` `mathml`, `Extensions` `[".mml"]` (decision D4: the
  `.mml` extension only), no `Schemes`, `FragmentParameters` `["display"]`, and the constants `DisplayParameter`
  (`display`) and `WarningCode` (`mathml-warning`).

### Key Methods

- `Parse(text, options)` - rejects a null text, refuses a text longer than `MaxTextSize` with an
  `InvalidDataException`, and reads it with `MathMlReader.Read`.
- `Open(path, options)` and `Open(stream, options)` - validate the arguments, read the bytes in chunks of 8,192 and
  stop with an `InvalidDataException` once they exceed three bytes per allowed character (plus a byte-order mark),
  drop the UTF-8 byte-order mark, decode as strict UTF-8 (invalid bytes are a `MathMlSyntaxException` with a hint),
  and continue as `Parse`; the source name of a file is its path unless the options name it.
- `Measure(options, token)` - runs the pipeline and returns the frame of the picture: the content box of the `math`
  box united with its ink grown on every side by the antialiasing allowance (`PictureExtent(box, scale)`;
  `InkAllowance(scale)` is one CSS pixel, or one device pixel below scale 1), scaled and rounded out to whole pixels
  with a tolerance of 1e-6, at least one pixel on each side, with the origin on a whole row so that the baseline is
  exact. The allowance keeps the ink, antialiasing included, off the outermost rows and columns of the picture, so a
  stretched fence, the tail of a radical, or the limit of an integral that reaches the edge of the content box is
  never cut; a side grows only where the ink comes within the allowance of the content box.
- `Render(options, token)` - the same frame, painted with `MathMlPainter` at the scale of the options.
- `Render(width, height, options, token)` - validates the size first, then fits the picture extent into the size with
  the smaller of the two ratios, centered (an empty picture keeps scale 1). The scale is chosen with the allowance of
  one CSS pixel; when it comes out below 1 the allowance is one device pixel, which widens each side by at most two
  device pixels, so the scale that fits the extent without the allowance into the size less two pixels is used (a
  size of two pixels or less fits the extent without the allowance).
- `MeasureNatural(options, token)` (internal) - the size of the picture at the scale, rounded out like `Measure` but
  without the `Surface.MaxDimension` check (each side at most `int.MaxValue`), for the image source, which only needs
  the aspect ratio of a picture larger than a surface.
- `MathMlImageSource.Render(request)` - checks the request and its cancellation; reads the `display` fragment
  parameter (`block` or `inline`, ASCII case-insensitive, trimmed) and refuses any other value with an
  `ImageSourceRefusedException` carrying `FolderImageResolver.FragmentInvalidCode` before the content is read; opens
  the document from `request.OpenContent()` with the file name as the source name; measures the natural size with
  `MeasureNatural` (without handlers, so that every report is delivered once, by the render); asks the request for the
  size to draw (`SizeFor`) and checks it against the pixel budget (`EnsureWithinBudget`) before allocating; and renders
  with `Render(width, height, ...)` on a transparent background with the display mode, the strict flag of the request,
  and handlers that turn a warning into an `ImageSourceDiagnostic` with the code `mathml-warning` and the specific code
  in `Detail`, and an unsupported feature into one with its feature code, each on one line and with its location. MathML
  Core has no external references, so the request's resolver and file access are never used.
- `Run` (the pipeline; internal so that a test can drive a defect through it) - creates a `MathMlReportSink`, replays
  the pending reports of reading, creates a `LayoutEngine` over `MathFont.Default`, resolves the style of `math` from
  the options, lays the tree out, and finishes (measure or paint). It runs on the calling thread first; the layout
  checks the stack at every element (`EnsureSufficientExecutionStack`), and when the stack of the calling thread runs
  short (a property of the caller, not the equation) the call starts again from a fresh sink on a dedicated thread with
  a 32 MiB stack (`RunOnLayoutThread`, the pattern of the OpenSCAD parser): at most one per processor (at least two)
  run at once, a thread that cannot be started (no address space, or a platform without threads) or a stack that still
  runs short is a located `mathml-limit-exceeded`, and any other exception is rethrown on the calling thread
  unchanged. The reports are delivered after the work; see *Error Handling*.
- `MathMlReportSink.Warn` and `Unsupported` - record a report unless an identical one was recorded; beyond
  `MaxDiagnostics` the first further report becomes one `mathml-diagnostics-suppressed` notice and the rest are dropped
  without being remembered, so the de-duplication sets stop growing too; in strict mode the first unsupported feature is
  always recorded and then thrown.
- `MathMlReportSink.Deliver` and `DeliverIgnoringHandlerFailures` - deliver the reports in order, each to its handler;
  the second protects each handler call.

### Error Handling

Arguments are validated first (`ArgumentNullException`, `ArgumentException`, `ArgumentOutOfRangeException`, and the
file exceptions of `File.OpenRead`). A text over the size limit is an `InvalidDataException`; reading errors are
`MathMlSyntaxException`s from the Syntax subsystem. A picture with no finite size is a `mathml-render-failed`
`MathMlRenderException`, and one larger than `Surface.MaxDimension` on a side is a `mathml-limit-exceeded` one with a
hint. An exception that escapes the layout or the painting and is a defect (`IsLayoutDefect`: malformed font data or a
runtime programming error, never the documented failures or a cancellation) becomes a located `mathml-render-failed`
`MathMlRenderException` with the original exception inside. After a failure the recorded reports are still delivered
but a failing handler is ignored, so the caller sees the original failure; after success a failing handler's exception
propagates and the new surface is disposed first. A surface whose painting fails is disposed before the exception
propagates.

### Dependencies

The `MathMlReader` and `MathTree` units of the Syntax subsystem, the `LayoutEngine`, `StyleResolver`, `MathFont`, and
`MathBox` units of the Layout subsystem, the `MathMlPainter` unit, the core `Canvas` subsystem (`Surface`, `Rgba32`),
and the core `ImageSources` subsystem (`IImageSource`, `IFragmentImageSource`, `ImageSourceRequest`,
`ImageSourceResult`, `ImageSourceDiagnostic`, `ImageSourceRefusedException`, and `FolderImageResolver`'s codes).

### Callers

Applications that render MathML, through the public API, and image resolvers (such as `FolderImageResolver`) that
draw linked `.mml` files with `MathMlImageSource`. Within the repository, the test project (whose system tests also
draw an equation through the image source with a pre-canceled request and from a request content alone), and the
`CanvasNetViewer` system, whose `SourceLoader` opens, measures, and draws `.mml` files and whose built-in MathML image
source draws a linked equation; the tool reaches the library through the Viewer and bundles it.

### NodeImages

![Rendering Structure](MermaidRenderingView.svg)

The `NodeImages` unit is the fifth software unit in the `Rendering` subsystem. It is one source file, `NodeImages.cs`,
in the `DemaConsulting.CanvasNet.Mermaid.Rendering` namespace, and defines one `internal` type, the sealed class
`NodeImages`, which implements the `ISceneImageLoader` interface of the `Scenegraph` subsystem.

#### Purpose

`NodeImages` holds the pictures of one rendering: the `@{ img: ... }` nodes and the `<img>` elements of labels. A link
is handed, as text, to the caller's `MermaidRenderOptions.ImageResolver` and to nothing else, so the caller decides
what a link may refer to (a `FolderImageResolver` refuses a remote or absolute link). The unit gives the layout the
natural size of each picture and the painter its pixels at the size they are drawn, reports a picture that is not
drawn, and bounds the number of resolver calls. The unit's behavior is specified by the
`CanvasNetMermaid-Rendering-NodeImages-*` requirements in `docs/reqstream/canvas-net-mermaid/rendering/node-images.yaml`.

#### Data Model

- The resolver of the caller (or null), the most resolver calls of the rendering (`MermaidLimits.MaxImages`), and the
  report sink of the call.
- `ProbePixels` (256) - the size in device pixels at which a picture is asked for to learn its natural size.
- The natural size of every link asked for (null for a link that gave nothing), the location of the first element
  that named each link, and the links already reported.
- `Calls` - the number of resolver calls made so far.

#### Key Methods

- `NodeImages(IImageResolver? resolver, int maxImages, MermaidReportSink sink)` - the pictures of one call.
- `NaturalSize(string href, MermaidLocation location)` - asks the resolver once per link for the picture at 256 by
  256 device pixels and takes the size of what it returns, disposing it: a raster comes back at its own size (the
  resolver's natural-size behavior), and a vector picture, which has no natural size, at the size it was drawn for. The
  answer is kept for the rest of the rendering; null when there is no resolver or the resolver gives nothing.
- `Load(string href, int pixelWidth, int pixelHeight)` - the picture at the device size it is drawn at, for the
  painter, which disposes it; the size is clamped to 1 through `Surface.MaxDimension`.

**Preconditions and postconditions**: an instance belongs to one call on one thread; the same instance serves the
layout (natural sizes) and the painting (loads), so the image limit counts both.

#### Error Handling

- No resolver, an empty link, or a resolver that gives no picture: the link is reported once as the warning
  `mermaid-image-unresolved` (located at the first element that named it, the link shortened to 200 characters) and
  nothing is drawn.
- The call that would pass `MermaidLimits.MaxImages` is not made and throws a located `MermaidRenderException`
  (`mermaid-limit-exceeded`).
- An exception of the resolver propagates unchanged. It is marked in its `Exception.Data` with
  `ResolverExceptionMark` so that `MermaidDocument`'s defect net never takes it for a defect of the layout.

#### Dependencies

- **Scenegraph subsystem** - `ISceneImageLoader`
- **Package root** - `MermaidRenderException`, `MermaidDiagnosticCodes`, `MermaidReportSink`, `MermaidLocation`,
  `MermaidLimits`, `MermaidRenderOptions`
- **Core `CanvasNet`** - `Surface` (`Canvas`) and `IImageResolver` (`ImageSources`)

#### Callers

`FlowchartModel.Layout` (the `Flowchart` subsystem) creates one `NodeImages` per render, hands it to the
`LabelBuilder` and the `ShapeContext`, and passes it with the scene to the painter; `ShapeLibrary` asks it for the
natural sizes of image nodes, `LabelBuilder` for those of label pictures, and the `Scenegraph` painter loads the
pictures through it. The tests in `NodeImagesTests.cs` call the unit directly through `InternalsVisibleTo`.

#### Known Differences From Mermaid

- The natural size of a picture is the size of what the resolver returns for a 256 by 256 request (Mermaid reads the
  browser's `naturalWidth` and `naturalHeight`), so a vector picture has the probe size.
- A picture that cannot be loaded, on which Mermaid's render fails, is reported and not drawn; the image shape then
  keeps its frame (see _ShapeLibrary Unit Design_).

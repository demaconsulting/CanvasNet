## Scenegraph

![Scenegraph Structure](MermaidScenegraphView.svg)

### Overview

The `Scenegraph` subsystem (namespace `DemaConsulting.CanvasNet.Mermaid.Scenegraph`, folder
`src/DemaConsulting.CanvasNet.Mermaid/Scenegraph/`) is the last stage of the pipeline and the only one that produces
pixels, in two steps: an immutable scene that describes what to draw in Mermaid's SVG coordinates (the **assertion
surface** of the tests and of the reference comparison) and a painter that draws a scene onto a core `Surface`. It is
**internal**: its types are visible only to the other subsystems of the system and, through `InternalsVisibleTo`, to
the test assembly. It contains two units:

- **SceneModel** - the scene records in `SceneModel.cs`: `Scene` (the view box, the items, the picture loader, and the
  helpers `Walk`, `BoundsOf`, and `ToPath`), the items `SceneItem`, `SceneGroup`, `ScenePath`, `SceneText`, and
  `SceneImage`, and their parts `SceneRect`, `SceneSegment` and `SceneSegmentKind`, `SceneStroke`, `SceneShadow`,
  `SceneFont`, `SceneTextRun`, and `SceneTextAnchor`; see _SceneModel Unit Design_ (`scenegraph/scene-model.md`)
- **ScenePainter** - the painter `ScenePainter` (a partial class in `ScenePainter.cs`, `ScenePainter.Shadow.cs`,
  `ScenePainter.Text.cs`, and `ScenePainter.Images.cs`), the placement `ScenePlacement`, and the picture-loader
  contract `ISceneImageLoader`: fills, strokes and dashes, drop shadows, text in the bundled fonts (filled and, when
  it has a stroke, stroked), pictures, the painted-pixel limit, and cancellation; see _ScenePainter Unit Design_
  (`scenegraph/scene-painter.md`)

The subsystem's namespace documentation is in `Scenegraph/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetMermaid-Scenegraph-*` requirements in `docs/reqstream/canvas-net-mermaid/scenegraph.yaml`. The SysML2 model
names the subsystem `MermaidScenegraph` and its units `MermaidSceneModel` and `MermaidScenePainter`, because the
`CanvasNetVegaLite` system already has a scene model and a scene painter in the shared model (see _CanvasNetMermaid
System Design_).

The subsystem is original work of this repository; it ports no upstream code. It reproduces the drawing rules of the
SVG that Mermaid produces (the path commands, the fill rules, the dash rules of browsers, the bounding box of
`getBBox`, the `preserveAspectRatio="none"` of Mermaid's node pictures, and the `drop-shadow` filter of the boxes and
menus of sequence diagrams, blurred as the SVG specification approximates the Gaussian blur).

### Interfaces

**Exposed** (internal only): the immutable scene types; `Scene.Walk`, `Scene.BoundsOf`, and `Scene.ToPath`;
`SceneRect.Union` and `Offset`; the `SceneSegment` factories `Move`, `Line`, `Quadratic`, `Cubic`, `Arc`, and
`Close`; `ScenePainter(FontResolver, ISceneImageLoader?, long)`, `ScenePainter.CreateFontResolver`, and
`ScenePainter.Paint(Scene, int, int, ScenePlacement, Rgba32?, CancellationToken)`; `ScenePlacement.Uniform` and
`ScenePlacement.Fit`; and the interface `ISceneImageLoader.Load(string, int, int)`.

**Consumed**: the public `MermaidLocation`, `MermaidRenderException`, `MermaidDiagnosticCodes`, and `MermaidLimits`
of the system's public API; and the core `CanvasNet` public API: `Surface` and `Rgba32` (`Canvas`), `PathFiller`,
`PathStroker`, `StrokeStyle`, `FillRule`, `LineCap`, and `LineJoin` (`Drawing`), `Path` and `PathBuilder`
(`Geometry`), `FontResolver`, `FontResolverOptions`, and `TrueTypeFont` (`Fonts`), and `TextRenderer` (`Rendering`).

**Consumers**: the units of the `Rendering` subsystem (`ShapeLibrary`, `EdgeBuilder`, `LabelBuilder`, and
`ClusterLayout`), the `FlowchartModel` unit of the `Flowchart` subsystem, and the `SequenceLayout` and
`SequenceRenderer` units of the `Sequence` subsystem build the items of a scene;
`NodeImages` of the `Rendering` subsystem implements `ISceneImageLoader` and is carried by the scene; and the
`MermaidDocument` unit creates the font resolver of a call with `ScenePainter.CreateFontResolver`, lays the diagram
out into a scene, and paints it with `ScenePlacement.Uniform` for the natural size or `ScenePlacement.Fit` for an
exact size.

### Design

**An immutable scene is the contract between layout and painting.** The layout never draws: it produces records with
resolved geometry (SVG path commands in absolute scene units), resolved colors (the opacity already folded into the
alpha), and resolved fonts (a CSS family list, a size, and a style), grouped as Mermaid's SVG groups them, with the
view box of the diagram. Every item carries a stable role (`node`, `edge`, `edge-label`, `cluster`, `marker`,
`label`, and so on), Mermaid's element id where there is one, and the location of its construct in the original text,
so a test or the reference comparison finds an item and compares its geometry with a number instead of comparing
pixels, and a failure while painting an item points at its construct. `Scene.BoundsOf` gives bounds as Mermaid gets
them from `getBBox` (curves exactly, strokes and drop shadows excluded); the layout uses it for the bounds of the edge
paths, which enter the view box of the diagram.

**The painter adds only what the scene leaves open.** It fills the surface with the background, then draws the items
in list order, parents before children, translated by their groups and transformed by the placement (device = scene x
scale + offset). Paths are converted with `Scene.ToPath`, filled with the core path filler, and stroked in device space
with the width and dashes scaled, so a diagram looks the same at every scale; a path's drop shadow is painted under
it first, the silhouette moved by the scaled offset and blurred with three box blurs. Text is drawn with the face that
the font resolver of the call resolves - the same resolver the layout measured the labels with, so measuring and
painting agree - and the resolver maps the common family names (Mermaid's `"trebuchet ms", verdana, arial`) to the
bundled Liberation families and never uses the fonts installed on the machine; a line with a stroke has its glyph
outlines stroked over the fill. Pictures are asked from the loader that the
scene carries (the same `NodeImages` that measured them, so the `MaxImages` budget is shared) at the device size of
their box and stretched to it.

**Painting is bounded.** Every coordinate is checked before it is drawn: an item with a value that is not finite or
beyond 32,768 device pixels is skipped, so flattening one item costs a bounded amount. Before an item or its drop
shadow is drawn, the device pixels of its box, clipped to the surface, are charged to `MermaidLimits.MaxPaintedPixels`;
the item that
would pass the limit fails the call with a located `mermaid-limit-exceeded` before any of its pixels is painted. The
cancellation token is checked before every item, and the surface that the painter created is released on any failure.

**Determinism.** The scene types are immutable and safe for concurrent use. A painter holds the count of the pixels of
one painting, so an instance paints one scene on one thread; the same scene, size, and placement always give the same
pixels.

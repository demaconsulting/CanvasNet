## Scenegraph

![Scenegraph Structure](VegaLiteScenegraphView.svg)

### Overview

The `Scenegraph` subsystem (namespace `DemaConsulting.CanvasNet.VegaLite.Scenegraph`, folder
`src/DemaConsulting.CanvasNet.VegaLite/Scenegraph/`) is where a chart becomes pixels, in two steps: an immutable scene
that holds every resolved item (the **assertion surface** of the tests) and a painter that draws the scene onto a
core `Surface`. It is **internal**: its types are visible only to the other subsystems of the system and, through
`InternalsVisibleTo`, to the test assembly. It contains three units:

- **SceneModel** - the internal scene types: `Scene`, the items `SceneItem`, `SceneGroup`, `SceneRect`, `SceneRule`,
  `ScenePath`, `SceneSymbol`, and `SceneText`, the value types `ScenePoint`, `SceneClip`, and `SceneEntry`, `SceneBounds`,
  `SceneLists`, and the `SceneBudget`; see _SceneModel Unit Design_ (`scenegraph/scene-model.md`)
- **ScenePainter** - the internal `ScenePainter` and `ScenePlacement`: drawing a scene onto a `Surface` with fills,
  strokes, dashes, opacity, rotation, uniform scale, clipping, and text; see _ScenePainter Unit Design_
  (`scenegraph/scene-painter.md`)
- **CssColor** - the internal static `CssColor`: CSS color text to `Rgba32`; see _CssColor Unit Design_
  (`scenegraph/css-color.md`)

The subsystem's namespace documentation is in `Scenegraph/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetVegaLite-Scenegraph-*` requirements in `docs/reqstream/canvas-net-vega-lite/scenegraph.yaml`. The SysML2 model
names the subsystem `VegaLiteScenegraph` and the scene unit `VegaLiteSceneModel` (see _CanvasNetVegaLite System Design_).

### Interfaces

**Exposed** (internal only): the immutable scene types and their enumeration helpers (`Scene.Enumerate`, `Find`,
`FindAll`, `OfType`, `CountItems`); `SceneBounds` with `Union`, `Translate`, and `Rotate`; `SceneBudget.Add`;
`ScenePainter.Paint(Scene, int, int, ScenePlacement, CancellationToken)` and `Paint(Scene, Surface, ScenePlacement,
CancellationToken)`, `ScenePainter.SymbolPath`, and `ScenePlacement.Uniform` and `ScenePlacement.Fit`; and `CssColor.TryParse`,
`Parse`, `IsNone`, `TryResolvePaint`, `WithOpacity`, and `ToCss`.

**Consumed**: `TextMeasurer` from the `Guides` subsystem (the painter resolves fonts and draws the lines the measurer
produces); the public `VegaLiteLocation` and `VegaLiteRenderException` of the `VegaLiteDocument` unit; and the core
`CanvasNet` public API: `Surface` and `Rgba32` (`Canvas`), `PathFiller` and `PathStroker` (`Drawing`), `Path` and
`PathBuilder` (`Geometry`), and `TextRenderer` (`Rendering`).

**Consumers**: the builders of the `Guides`, `Layout`, and `Marks` subsystems create the items; `VegaLiteDocument` paints
the scene.

### Design

**An immutable scene is the contract between building and painting.** The builders (`TitleBuilder`, `AxisBuilder`,
`ViewLayout`, `MarkBuilder`) never draw: they produce items with resolved geometry and CSS color strings, in a fixed order,
so that two runs produce the same list and tests can assert exact numbers and exact strings without pixels. Items have a
stable role (`root`, `view`, `title-text`, `axis-tick`, `mark-bar`, and so on) by which tests and later stages find
them, and the enumeration is iterative (an explicit stack), so a deep scene cannot overflow the stack.

**The painter adds what the core lacks.** The core library has fills, strokes, dashes, and text, but no scale and no
clip, so the painter transforms every path by the placement (uniform scale plus translation plus the enclosing groups'
translations) before filling it, draws text at the scaled font size, and implements clipping itself by drawing a clipped
group onto a transparent layer the size of the clip's pixel box and compositing it back with the coverage of the clip
rectangle. Fills are flattened at a tolerance of 0.02 pixel so that small symbols stay round, strokes are built in the item's
own units so widths and dashes scale with the chart, and a paint that is `none`, unparseable, or fully transparent draws
nothing.

**Deterministic.** The painter holds no state and the same scene, size, and placement give the same bytes on every platform;
colors parse with the invariant culture and no named color depends on the host.

**Bounded.** The scene item count is bounded by `SceneBudget` (`MaxSceneItems`), and the pixel size by the surface
(`Surface.MaxDimension`); a clip layer is only as large as the clip's pixel box.

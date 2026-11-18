## Scenegraph

![Scenegraph Structure](PlantUmlScenegraphView.svg)

### Overview

The `Scenegraph` subsystem (namespace `DemaConsulting.CanvasNet.PlantUml.Scenegraph`, folder
`src/DemaConsulting.CanvasNet.PlantUml/Scenegraph/`) is the drawing stage of the pipeline, independent of the diagram
type. It is **internal**. It contains three units:

- **SceneModel** - the scene records and the shape builders (`SceneModel.cs`); see *SceneModel Unit Design*
- **TextLayout** - the measurement and layout of text with the bundled fonts and the creole subset (`TextLayout.cs`,
  `TextLayout.Creole.cs`); see *TextLayout Unit Design*
- **ScenePainter** - the painter and the placement (`ScenePainter.cs`, `ScenePainter.Text.cs`); see *ScenePainter Unit
  Design*

The subsystem's namespace documentation is in `Scenegraph/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetPlantUml-Scenegraph-*` requirements in `docs/reqstream/canvas-net-plant-uml/scenegraph.yaml`. The SysML2 model
names the subsystem `PlantUmlScenegraph` and its units `PlantUmlSceneModel`, `PlantUmlTextLayout`, and
`PlantUmlScenePainter`. The subsystem is the package's own and is not shared with the Mermaid package, so the two
packages stay independent.

### Interfaces

**Exposed** (internal only): the scene records (`SceneRect`, `SceneSegment`, `SceneStroke`, `SceneFont`,
`SceneTextRun`, `SceneItem`, `ScenePath`, `SceneText`, `Scene` with `BoundsOf` and `ToPath`) and `SceneShapes`;
`TextLayout` (`CreateFontResolver`, `CssFamily`, `Resolve`, `LineMetrics`, `Width`, `Layout`, `SplitLines`) with
`TextAlign`, `TextLine`, and `TextBlock` (`Width`, `Height`, `Text`, `Draw`); `ScenePainter.Paint` and
`ScenePlacement` (`Uniform`, `Fit`).

**Consumed**: the core `Canvas` (`Surface`, `Rgba32`), `Geometry` (`Path`, `PathBuilder`), `Drawing` (`PathFiller`,
`PathStroker`, `StrokeStyle`), `Fonts` (`FontResolver` without system fonts), and `Rendering` (`TextRenderer`)
subsystems; the Styling subsystem's `SkinParams.TryParseColor`; and the root namespace's report sink, codes,
locations, limits, and render exception.

**Consumers**: `PlantUmlDocument` creates the text layout of a call, measures the scene, and paints it; the diagram
layouts measure their texts and build scenes.

### Design

A call creates one font resolver and one `TextLayout` over it; the diagram layout measures every text with it and
builds a `Scene` of paths and text lines whose view box is the natural size; the `ScenePainter` paints the scene with
the same text layout, so the faces that were measured are the faces that are drawn, and each run is drawn at the
advance it was measured with.

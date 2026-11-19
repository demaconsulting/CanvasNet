## Activity

![Activity Structure](PlantUmlActivityView.svg)

### Overview

The `Activity` subsystem (namespace `DemaConsulting.CanvasNet.PlantUml.Activity`, folder
`src/DemaConsulting.CanvasNet.PlantUml/Activity/`) draws PlantUML activity diagrams of the new syntax. It is
**internal**. It contains four units:

- **ActivityParser** - the internal class `ActivityParser` (`ActivityParser.cs`, `ActivityParser.Statements.cs`): the
  statements of an activity diagram read into a tree of instructions, with the reports of what is not drawn, located
  errors, and the limits of a text; see *ActivityParser Unit Design*
- **ActivityModel** - the internal class `ActivityModel` and the instruction classes (`ActivityModel.cs`,
  `ActivityModel.Elements.cs`): the instructions and swimlanes of a diagram, its facts, and its layout entry point; see
  *ActivityModel Unit Design*
- **ActivityLayout** - the internal class `ActivityLayout` with its tiles and shapes (`ActivityLayout.cs` and the
  partial files `ActivityLayout.Build.cs`, `.Branches.cs`, `.IfTiles.cs`, `.Loops.cs`, `.Parallel.cs`, `.Notes.cs`,
  `.Leaves.cs`, `.Tiles.cs`, `.Canvas.cs`, `.Shapes.cs`, `.Worm.cs`, `.Swimlanes.cs`): the tile composition and the
  compression of PlantUML's activity engine; see *ActivityLayout Unit Design*
- **ActivityRenderer** - the internal static class `ActivityRenderer` (`ActivityRenderer.cs`): the scene items of the
  placed diagram; see *ActivityRenderer Unit Design*

The subsystem's namespace documentation is in `Activity/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetPlantUml-Activity-*` requirements in `docs/reqstream/canvas-net-plant-uml/activity.yaml`. The SysML2 model
names the subsystem `PlantUmlActivity`.

### Interfaces

**Exposed** (internal only): `ActivityParser.Parse(source, detected, limits)`, which `DiagramRegistry` calls for the
`activity` type; the `ActivityModel` it returns, an `IPlantUmlDiagram` whose `Layout(context)` returns the scene of
the diagram; `ActivityLayout.Run(model, context)` and `ActivityRenderer.Render(model, geometry)` for the tests.

**Consumed**: the Syntax subsystem (`PreprocessedSource`, `SourceStatement`, `DetectedDiagram`, and the
`DiagramDetector` helpers `FirstWord`, `BlockCloser`, `IsCloser`, `OpensAction`, and `EndsAction`), the Styling
subsystem (`SkinTheme`, `SkinParams`), the Scenegraph subsystem (`Scene`, `SceneShapes`, `TextLayout`, `TextBlock`,
`ScenePage`, `PageMetrics`), and the root namespace's `PlantUmlLimits`, `PlantUmlLocation`, `PlantUmlDiagnosticCodes`,
`PlantUmlPendingReport`, `PlantUmlSyntaxException`, `PlantUmlRenderException`, and `DiagramRenderContext`.

**Consumers**: `PlantUmlDocument` through `DiagramRegistry` and `IPlantUmlDiagram`.

### Design

`DiagramRegistry.Parse` hands a detected activity diagram to `ActivityParser.Parse`, which reads the statements into
an `ActivityModel` once, when the document is opened. Each measure or render call asks the model for a scene:
`ActivityLayout.Run` builds a tile for every instruction, composes them, draws them onto a canvas of shapes, compresses
the empty bands, and places the drawing at the margins (an `ActivityGeometry`); `ActivityRenderer.Render` turns the
placed shapes into scene items; and `ScenePage.Decorate` adds the page texts with the spacing of an activity diagram.
PlantUML lays activity diagrams of the new syntax out without Graphviz, with its "ftile" composition: a list is a
column of tiles joined by arrows, an if, a loop, a fork, or a split is a tile made of the tiles of its branches, and the
whole drawing is then compressed. The layout ports that composition from the MIT-licensed sources of PlantUML
1.2026.10 (`activitydiagram3/ftile/**`, `klimt/compress/*`, `klimt/drawing/LimitFinder`) and was checked against the
pictures the jar draws for the original fixtures (see
`test/DemaConsulting.CanvasNet.PlantUml.Tests/Fixtures/reference/README.md`).

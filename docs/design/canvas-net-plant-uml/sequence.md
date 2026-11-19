## Sequence

![Sequence Structure](PlantUmlSequenceView.svg)

### Overview

The `Sequence` subsystem (namespace `DemaConsulting.CanvasNet.PlantUml.Sequence`, folder
`src/DemaConsulting.CanvasNet.PlantUml/Sequence/`) draws PlantUML sequence diagrams. It is **internal**. It contains
four units:

- **SequenceParser** - the internal class `SequenceParser` (`SequenceParser.cs`, `SequenceParser.Statements.cs`): the
  statements of a sequence diagram read into a model, with the reports of what is not drawn, located errors, and the
  limits of a text; see *SequenceParser Unit Design*
- **SequenceModel** - the internal class `SequenceModel` and the element records (`SequenceModel.cs`,
  `SequenceModel.Elements.cs`): the participants, boxes, and events of a diagram, its facts, and its layout entry point;
  see *SequenceModel Unit Design*
- **SequenceLayout** - the internal class `SequenceLayout` and the placed records (`SequenceLayout.cs`,
  `SequenceLayout.Messages.cs`, `SequenceLayout.Blocks.cs`): the columns and rows of PlantUML's sequence engine; see
  *SequenceLayout Unit Design*
- **SequenceRenderer** - the internal static class `SequenceRenderer` (`SequenceRenderer.cs`,
  `SequenceRenderer.Participants.cs`, `SequenceRenderer.Messages.cs`, `SequenceRenderer.Blocks.cs`): the scene items of
  the placed diagram; see *SequenceRenderer Unit Design*

The subsystem's namespace documentation is in `Sequence/NamespaceDoc.cs`. Its behavior is specified by the
`CanvasNetPlantUml-Sequence-*` requirements in `docs/reqstream/canvas-net-plant-uml/sequence.yaml`. The SysML2 model
names the subsystem `PlantUmlSequence`.

### Interfaces

**Exposed** (internal only): `SequenceParser.Parse(source, detected, limits)`, which `DiagramRegistry` calls for the
`sequence` type; the `SequenceModel` it returns, an `IPlantUmlDiagram` whose `Layout(context)` returns the scene of
the diagram; `SequenceLayout.Run(model, context)` and `SequenceRenderer.Render(model, geometry)` for the tests.

**Consumed**: the Syntax subsystem (`PreprocessedSource`, `SourceStatement`, `DetectedDiagram`, and the
`DiagramDetector` helpers `FirstWord`, `IsSequenceArrow`, `BlockCloser`, and `IsCloser`), the Styling subsystem
(`SkinTheme`, `SkinParams`), the Scenegraph subsystem (`Scene`, `SceneShapes`, `TextLayout`, `TextBlock`, `ScenePage`),
and the root namespace's `PlantUmlLimits`, `PlantUmlLocation`, `PlantUmlDiagnosticCodes`, `PlantUmlPendingReport`,
`PlantUmlSyntaxException`, `PlantUmlRenderException`, and `DiagramRenderContext`.

**Consumers**: `PlantUmlDocument` through `DiagramRegistry` and `IPlantUmlDiagram`.

### Design

`DiagramRegistry.Parse` hands a detected sequence diagram to `SequenceParser.Parse`, which reads the statements into a
`SequenceModel` once, when the document is opened. Each measure or render call asks the model for a scene:
`SequenceLayout.Run` measures the texts and solves the columns and the rows into a `SequenceGeometry`,
`SequenceRenderer.Render` draws the geometry as scene items, and `ScenePage.Decorate` adds the page texts. The layout
follows PlantUML 1.2026.10, whose sequence diagrams are always laid out by its "teoz" engine: the constants were
derived from its MIT-licensed sources (`sequencediagram/teoz/*`, `skin/rose/*`) and checked against the pictures it
draws for the original fixtures (see `test/DemaConsulting.CanvasNet.PlantUml.Tests/Fixtures/reference/README.md`).

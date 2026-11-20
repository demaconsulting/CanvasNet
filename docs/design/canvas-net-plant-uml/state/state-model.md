### StateModel

![State Structure](PlantUmlStateView.svg)

The `StateModel` unit is the second software unit in the `State` subsystem. Its source files are `State/StateModel.cs`
(the diagram, its facts, its lowering, and its layout) and `State/StateModel.Elements.cs` (the elements and the
enumeration of their kinds).

#### Purpose

`StateModel` is the immutable result of reading a state diagram. It answers the document's questions (title, facts,
reports) and turns the diagram into a scene for every measure or render call through the Svek engine. The unit's
behavior is specified by the `CanvasNetPlantUml-State-StateModel-*` requirements in
`docs/reqstream/canvas-net-plant-uml/state/state-model.yaml`.

#### Data Model

- `StateModel` - the elements in order of creation, the notes and the transitions in text order, the page texts, the
  theme with the diagram's skin parameters, the reports, the location the type was detected at, the rank direction,
  `nodesep`, `ranksep`, the diagram's own scale, and `hide empty description`. `NodeCount` is the number of simple
  states and pseudo-states, `EdgeCount` the number of transitions that do not touch a note, and `SubgraphCount` the
  number of composite states.
- `StateKind` - state, start, end, choice, fork or join, junction, history, and deep history.
- `StateNode` - the id (PlantUML's qualified name), the location, and the region the node is in (null at the top).
- `StateElement` - the display, the kind, the description lines, the fill and line colors, the regions of a composite
  state with their arrangement (`ClusterConcurrency`), and the nesting depth.
- `StateRegion` - the prefix of its members' ids (the composite's id, or `Composite.CONCn`), the composite state, and
  its index; a region after the first is concurrent.
- `StateNote` - the text, the fill, and the target state and side, the transition, or none for a floating note.
- `StateTransition` - the ends, the decorations, the style, the thickness, the color, the length, the direction, and
  the label.

#### Key Methods

- `Lower(context)` - builds a `SvekDiagram` for one call: a `SvekCluster` per composite state, parents first, with the
  image of `StateRenderer.CompositeImage` and, when it has more than one region, the arrangement of its regions and a
  cluster without image per region (id `RegionClusterId`: the composite's id, `"region`, and the index, never the id of
  an element or a note, as no name holds a quote); a `SvekEntity` per simple state and pseudo-state with the image of
  `StateRenderer.Image` in the cluster of its region; the floating notes; the transitions with every setting; the
  notes attached to states or transitions; notes kept in text order; the direction, the layout settings, the theme,
  and the location. Cancellation is observed between elements.
- `Layout(context)` - lowers, runs `SvekLayout.Run`, draws with `SvekRenderer.Render`, and decorates with the page
  texts (`ScenePage.Decorate` with `PageMetrics.Svek`), over the theme's background at the diagram's scale.

#### Error Handling

Null arguments throw `ArgumentNullException`. Layout failures (the work limit) are the located errors of the Svek
layout; a canceled call throws `OperationCanceledException`.

#### Dependencies

The `StateRenderer` unit, the Svek subsystem (`SvekDiagram` and its elements, `SvekLayout`, `SvekRenderer`), the
Styling subsystem (`SkinTheme`), and the Scenegraph subsystem (`Scene`, `PageTexts`, `ScenePage`, `PageMetrics`).

#### Callers

The `StateParser` unit builds it; `PlantUmlDocument` measures, renders, and describes it; the tests.

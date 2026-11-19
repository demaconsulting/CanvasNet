### SvekModel

![Svek Structure](PlantUmlSvekView.svg)

The `SvekModel` unit is the first software unit in the `Svek` subsystem. Its source files are `Svek/SvekModel.cs` and
`Svek/SvekModel.Elements.cs`.

#### Purpose

`SvekModel` describes a diagram lowered for the shared engine, so that one layout and one renderer serve every diagram
type that PlantUML lays out with Graphviz. The unit's behavior is specified by the `CanvasNetPlantUml-Svek-SvekModel-*`
requirements in `docs/reqstream/canvas-net-plant-uml/svek/svek-model.yaml`.

#### Data Model

- `SvekDiagram` - the lists `Entities`, `Clusters` (parents before children), `Notes`, and `Links` in the order of the
  text, the `RankDir` (`TopToBottom` or `LeftToRight`), the optional `NodeSep` and `RankSep`, the `Theme`, and the
  `Location` that failures are reported at. A diagram belongs to one call.
- `SvekNode` (abstract) - `Id` (the qualified name the scene items carry; unique in a diagram), `Parent` (the
  `SvekCluster` that contains it, or null), and `Location`.
- `SvekEntity` - a node with an `ISvekEntityImage`: `Width`, `Height`, `Clip` (a `SvekClip` of a `ClipShape` -
  `Rectangle`, `Ellipse`, or `Diamond` - and a box relative to the entity, so an interface circle with its name below
  clips to the circle only), and `Draw(x, y)`, which returns the items of the entity at a place.
- `SvekCluster` - a node with an optional `ISvekClusterImage` (`HeaderHeight`, `MinWidth`, `Padding`, `Draw(box)`; none
  for a region), a `Concurrency` (`None`, `Stacked`, `SideBySide`), and `Autonomous` (always laid out on its own).
- `SvekNote` - a node with a `Text`, an optional `Fill`, and an attachment: a `Target` entity with a `Position`
  (`Left`, `Right`, `Top`, `Bottom`), a `Link` whose label it joins, or neither (floating).
- `SvekLink` - an `Id`, the ends `From` and `To` (any node), the decorations `TailDecor` and `HeadDecor` (a `LinkDecor`:
  `None`, `Extends`, `Composition`, `Aggregation`, `NotNavigable`, `Arrow`, `ArrowTriangle`, `Circle`, `CircleFill`,
  `CircleConnect`, `Parenthesis`, `Square`, `Plus`, `HalfArrowUp`, `HalfArrowDown`, and the read-only `Redefines`,
  `DefinedBy`, and crow's foot family), `Style` (`Solid`, `Dashed`, `Dotted`, `Bold`, `Hidden`), `Thickness`, `Color`,
  `Length` (2 by default; at least 1), `Direction` (`None`, `Up`, `Down`, `Left`, `Right`), `Label` with its
  `LabelArrow` (`None`, `Forward`, `Backward`), `TailLabel`, `HeadLabel`, `Weight` (1 by default; positive), and
  `Location`.
- `SvekPoint(X, Y)` and `SvekFacts(Nodes, Edges, Subgraphs)` - value records.

#### Key Methods

- `MembersOf(cluster)` - the direct members of a cluster or of the top: its entities, then its floating notes, then
  its child clusters, in the order of the diagram (attached notes are placed by their targets).
- `CountFacts()` - the entities, the links that touch no note, and the clusters with members (regions not counted).
- `Validate()` - checks that the ids are unique, that every parent, target, and link end belongs to the diagram, that
  the clusters nest without cycles, that no link ends at an attached note, that the lengths and weights are valid, and
  that a cluster with concurrent regions holds only regions.

#### Error Handling

`Validate` throws `ArgumentException` for an inconsistent diagram; the constructors throw `ArgumentNullException` for a
missing image, text, or end and `ArgumentException` for an empty id. A lowering defect therefore fails before the
layout.

#### Dependencies

The Scenegraph subsystem (`SceneItem`, `SceneRect`), the Styling subsystem (`SkinTheme`), and the root namespace's
`PlantUmlLocation`.

#### Callers

The diagram subsystems that lower their models (none in this step), `SvekLayout`, `SvekRenderer`, and the tests.

### DescriptionModel

![Description Structure](PlantUmlDescriptionView.svg)

The `DescriptionModel` unit is the second software unit in the `Description` subsystem. Its source files are
`Description/DescriptionModel.cs` (the diagram, its facts, its lowering, and its layout) and
`Description/DescriptionModel.Elements.cs` (the elements and the enumeration of their symbols).

#### Purpose

`DescriptionModel` is the immutable result of reading a description diagram. It answers the document's questions
(title, facts, reports) and turns the diagram into a scene for every measure or render call through the Svek engine.
The unit's behavior is specified by the `CanvasNetPlantUml-Description-DescriptionModel-*` requirements in
`docs/reqstream/canvas-net-plant-uml/description/description-model.yaml`.

#### Data Model

- `DescriptionModel` - the elements in order of creation, the notes and the links in text order, the page texts, the
  theme with the diagram's skin parameters, the reports, the location the type was detected at, the rank direction,
  `nodesep`, `ranksep`, and the diagram's own scale. `NodeCount` is the number of leaf elements, `EdgeCount` the
  number of links that do not touch a note (hidden links included), and `SubgraphCount` the number of containers.
- `DescriptionSymbol` - every symbol of PlantUML's description diagrams (component, interface, actor and business
  actor, use case and business use case, node, rectangle, folder, package, frame, cloud, database, card, artifact,
  storage, file, queue, stack, agent, hexagon, person, boundary, control, entity, collections, label, action, process,
  circle, archimate) and `Unknown` for a name only written in links until the parser gives it its symbol.
- `DescriptionNode` - the id (PlantUML's qualified name), the location, and the container the node is in (null at the
  top).
- `DescriptionElement` - the name it was created with, the display (a multi-line description, or the title of a
  container, empty for an anonymous one), the symbol, the stereotypes, the fill, line, and text colors, whether it is a
  container, and the nesting depth.
- `DescriptionNote` - the text, the fill, and the target leaf element and side, the link, or none for a floating note
  (a note on a container floats in it).
- `DescriptionLink` - the ends, the decorations, the style, the thickness, the color, the length, the direction, the
  label with its direction mark, the end labels, and the weight.

#### Key Methods

- `Lower(context)` - builds a `SvekDiagram` for one call: a `SvekCluster` per container, parents first, with the image
  of `DescriptionRenderer.ContainerImage`; a `SvekEntity` per leaf element with the image of `DescriptionRenderer.Image`
  in the cluster of its container; the floating notes in the cluster of their container; the links with every setting
  (an end may be a container's cluster); the notes attached to elements or links; notes kept in text order; the
  direction, the layout settings, the theme, and the location. Cancellation is observed between elements.
- `Layout(context)` - lowers, runs `SvekLayout.Run`, draws with `SvekRenderer.Render`, and decorates with the page
  texts (`ScenePage.Decorate` with `PageMetrics.Svek`), over the theme's background at the diagram's scale.

#### Error Handling

Null arguments throw `ArgumentNullException`. Layout failures (the work limit) are the located errors of the Svek
layout; a canceled call throws `OperationCanceledException`.

#### Dependencies

The `DescriptionRenderer` unit, the Svek subsystem (`SvekDiagram` and its elements, `SvekLayout`, `SvekRenderer`), the
Styling subsystem (`SkinTheme`), and the Scenegraph subsystem (`Scene`, `PageTexts`, `ScenePage`, `PageMetrics`).

#### Callers

The `DescriptionParser` unit builds it; `PlantUmlDocument` measures, renders, and describes it; the tests.

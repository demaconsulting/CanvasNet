### SvekLayout

![Svek Structure](PlantUmlSvekView.svg)

The `SvekLayout` unit is the third software unit in the `Svek` subsystem. Its source files are `Svek/SvekLayout.cs`
(the entry point, the placement records, the units of entities and notes, the spacing), `Svek/SvekLayout.Clusters.cs`
(the scopes, the compound clusters, the bands, the regions, the placement under construction),
`Svek/SvekLayout.Edges.cs` (the edges, the rows, the routes, the labels, the note pointers), and
`Svek/PlantUmlLayoutWorkMeter.cs` (the work meter).

#### Purpose

`SvekLayout` places the entities, clusters, notes, and links of a `SvekDiagram` with the dagre layered layout of the
CanvasNetLayout system, as PlantUML places them with Graphviz, within the layout work limit. The unit's behavior is
specified by the `CanvasNetPlantUml-Svek-SvekLayout-*` requirements in
`docs/reqstream/canvas-net-plant-uml/svek/svek-layout.yaml`.

#### Data Model

- The result `SvekPlacement(Width, Height, Entities, Clusters, Notes, Links)`, with `PlacedEntity(Entity, Box)`,
  `PlacedCluster(Cluster, Box)` with its `Separators`, `PlacedNote(Note, Text, Box)` with `PointerSide`,
  `PointerBase`, and `PointerTip`, `PlacedLink(Link, Route)` with `Drawn`, `Label`, `TailLabel`, and `HeadLabel`, and
  `PlacedLabel(Text, Box)` with `ArrowAngle`; the drawing's top-left corner is at the origin and the lists keep the
  order of the diagram.
- `PlantUmlLayoutWorkMeter` - derives the CanvasNetLayout system's `LayoutWorkMeter` with PlantUML's failures (see
  below); one meter per call.
- The per-call state of a layout: the graph ids (`e`, `n`, `c`, `a`, `r` followed by an index, never array-like keys),
  the clusters laid out on their own, the notes attached to each entity and link, the floating notes drawn as pointers,
  the scope of every link, and the spacing. Inside a scope: the units (an entity with its attached notes, a floating
  note, or a cluster laid out on its own), the rows, the compound clusters, and the edges.
- The constants of the engine: `MinNodeSep` 35 and `MinRankSep` 60 (PlantUML's `DotStringFactory`), `EdgeSep` 10,
  `NoteGap` 18 (measured from the reference pictures), `NoteStackGap` 10, `LabelMargin` 1 (PlantUML's `marginLabel`),
  `LabelOffset` 2 (dagre's label offset), `LabelClearance` 4 (the least space between a label and the lines of its
  link, as PlantUML leaves about 4 pixels), `RowLabelSpace` 20 (the space a label between two neighbors of a row leaves
  to them together, as PlantUML leaves about 10 pixels on each side), the note margins 6, 15, and 5 (PlantUML's
  `Opale`), `RegionPadding` 10, `SeparatorInset` 5 (the space between the sides of a cluster and the ends of its
  region separators), and `ParallelArcStep` 50.

#### Key Methods

- `Run(diagram, context)` and `Run(diagram, context, meter)` - validate the diagram, check the cancellation, and lay it
  out: prepare, lay out the top scope, normalize to the origin.
- `Prepare` - the ids; the attached and link notes; the pointer notes (a floating note with exactly one link to a node
  that is not a note, as PlantUML's `isOpalisable`); the clusters laid out on their own (marked so, with concurrent
  regions, a region, or crossed by no link: no link with exactly one end inside); the scope of every link (the
  innermost scope that holds both ends); the spacing: `NodeSep` and `RankSep` from the diagram, else PlantUML's minimums
  raised by a tenth of the widest horizontal and tallest vertical label sum of a link and its decoration margins (its
  "dzeta"), and at least what the cluster paddings need to stay inside dagre's cluster boxes.
- `LayoutScope(scope)` - a cluster with concurrent regions is `ArrangeRegions`; any other scope is one compound
  multigraph: `AddMembers` (units, the units of the notes attached to each entity, and compound nodes for the clusters
  that links cross), `AddEdges` (for every link of
  the scope: the representatives of its ends in the scope, `MinLen` = length - 1, 0 for left and right, at least 1 for
  up and down, the edge reversed for up and left, the label slot of its label, direction triangle, and note; then
  `NoteEdge` for every attached note, the hidden edge PlantUML's `CommandFactoryNoteOnEntity` gives Graphviz: a note
  left or right of its entity (top or bottom in a left to right diagram) shares its rank, `MinLen` 0, the others are
  one rank before (left, top) or after (right, bottom) it, `MinLen` 1, so the link routes keep clear of the notes),
  `FormRows`, `AddGraphNodes`, `AddGraphEdges`, `DagreLayout.Run`, then `ReadBack`, `TightenCompounds`,
  `InsertBands`, `RouteEdges`, and `BuildFrame`.
- `FormRows` - dagre's network simplex raises every edge to at least one rank (its `simplify` step), so the units that
  links of length 1 join (with the same compound parent) become one row node, side by side (stacked in a left to right
  diagram) in a topological order of the links (`OrderRow`: every first end before its second, the earliest unit of
  the diagram first among the free ones, the earliest remaining one when the links form a cycle; linear in the units
  and links), `NodeSep` apart or as far as the label between them needs (its width plus `RowLabelSpace`; `NoteGap`
  between a note and its entity, `NoteStackGap` between two notes of one entity).
- `ClusterUnit` - lays a cluster out on its own (`LayoutScope`) and frames it: width `max(content + 2 padding,
  MinWidth)`, height `content + 2 padding + header`, content centered.
- `ArrangeRegions` - lays out every region on its own and stacks them (or puts them side by side) across the widest
  (tallest), with the boundaries that become the dashed separators (`SeparatorInset` inside the sides of the cluster,
  starting below its header); a link between two regions is left unrouted.
- `TightenCompounds` and `InsertBands` - the box of a compound cluster is made the union of its members and anchor with
  its padding, innermost first; then, from the top down, a header band is inserted below the top of every compound
  cluster (everything whose center or start is below the line moves down by the header, the clusters the line crosses
  grow), and a band at the right of a cluster narrower than its title. Nothing overlaps by construction.
- `RouteEdges` - for every edge: the route of a link in a row (straight between the centers), a loop at the right of a
  node for a link to itself (each further loop wider), or dagre's route turned back for a reversed edge; a straight
  route that crosses another unit becomes an arc over it; further links in a row between the same two units become arcs
  below and above in turn, each pair `ParallelArcStep` further out (`SpreadParallel`); the end of a route at a unit of a
  row moves in front of that unit on the row's border and continues to its center (`SnapRowEnds`: dagre aims at the
  center of the row, which may be over an attached note); a link to a compound cluster goes to a hidden 1-by-1 anchor
  inside it and its route is cut at the cluster's border; the ends are moved onto the outline of the entity's clip
  (`ClipEnds`: aiming at the centers for a straight route, else at the next point of the route, and, for a rectangle
  that the next point lies beyond across the ranks, on the side facing the ranks in line with that point
  (`ClipToFacingSide`), never at a corner; a route across one label rank with no label, which dagre bends through a
  point in line with one end, is drawn straight between the facing sides of its two rectangles when that line crosses
  no other unit, as Graphviz draws a short edge (`StraightAims`: straight across the ranks at the middle of the narrower
  box where the two boxes face each other off their corners, else each end in line with the other box); the label slot
  is placed at dagre's label center, or beside the middle of the route (`BesideMiddle`: `LabelClearance` above a
  route that runs across, right of one that runs down, at the middle of the curve of an arc), and a label center that
  dagre puts inside a compound cluster the link leaves is replaced by the place beside the middle of the route outside
  it; the label is then moved away from the lines its link draws (`DrawnLines`: the curve of the route measured at 16
  points per cubic piece, and the outlines of its decorations) until it keeps `LabelClearance` from them and overlaps
  no unit (`ClearOfLink`: one pixel at a time, at most 32, away from the nearest point of the lines, each step
  charging the meter), since dagre placed it beside its own route, which the drawn curve does not follow exactly; end
  labels go beside the line just off the end and are moved along the line until they overlap no entity.
- `BuildFrame` and `PointNotes` - the placements of the units (entities, attached notes with the pointer from the side
  of the note that faces the entity - the side with the widest gap between the two boxes, the note's own position on a
  tie - to the entity's border, floating notes, clusters with their content and separators), of the compound
  clusters, of the links and link notes; a pointer note's short link (at most three route points) becomes the note's
  pointer, from the side of the note nearest the link (PlantUML's `getOpaleStrategy`), and is not drawn.

#### Error Handling

`Run` throws `ArgumentNullException` for missing arguments and `ArgumentException` for an inconsistent diagram (through
`SvekDiagram.Validate`). The meter bounds the work: every scope, member, edge, row, band, and route charges it, and
dagre charges its stages, so a layout past `PlantUmlLimits.MaxLayoutWork` throws
`PlantUmlLayoutWorkMeter.LimitExceeded()`: a `PlantUmlRenderException` with the code `plantuml-limit-exceeded`, the
message "the layout needs more than N units of work (PlantUmlLimits.MaxLayoutWork)", the hint of the sequence and
activity layouts, and the diagram's location; a layout that dagre cannot complete throws `Failure(description)` with
`plantuml-render-failed`; `IsFailure` recognizes every `PlantUmlRenderException`, so PlantUML's failures pass through
dagre unchanged. The meter observes the cancellation token (`OperationCanceledException`). The charges are
deterministic, so a diagram always uses the same work.

#### Dependencies

The CanvasNetLayout system (`Graph`, `DagreGraph`, `DagreNode`, `DagreEdge`, `DagreLayout.Run`, `LayoutWorkMeter`), the
`SvekModel` and `SvekGeometry` units, the Styling subsystem (`SkinTheme`, `SkinElement`), the Scenegraph subsystem
(`TextLayout`, `TextBlock`, `SceneFont`, `SceneRect`), and the root namespace's `DiagramRenderContext`,
`PlantUmlLimits`, `PlantUmlLocation`, `PlantUmlDiagnosticCodes`, and `PlantUmlRenderException`.

#### Callers

The diagram subsystems of the graph diagrams (the `ClassModel` unit of the Class subsystem, the `StateModel` unit of
the State subsystem, and the `DescriptionModel` unit of the Description subsystem) and the tests.

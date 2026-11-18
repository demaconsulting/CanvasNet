### ActivityModel

![Activity Structure](PlantUmlActivityView.svg)

The `ActivityModel` unit is the second software unit in the `Activity` subsystem. Its source files are
`Activity/ActivityModel.cs` (the model, its facts, and its layout entry point) and `Activity/ActivityModel.Elements.cs`
(the instructions, arrows, swimlanes, and notes).

#### Purpose

`ActivityModel` holds a parsed activity diagram: its tree of instructions, its swimlanes, its page texts, and its
look, and it lays the diagram out for every measure or render call. The unit's behavior is specified by the
`CanvasNetPlantUml-Activity-ActivityModel-*` requirements in
`docs/reqstream/canvas-net-plant-uml/activity/activity-model.yaml`.

#### Data Model

- `ActivityModel` - `Root` (an `ActivityList`), `Lanes`, `Page`, `Theme`, `Reports`, `KeywordLocation`, `Scale`, and
  the facts `Title`, `NodeCount`, `EdgeCount`, and `SubgraphCount`.
- `ActivityLink(Label, Color, Style, Location)` - an arrow with its label, color, and style (`Normal`, `Dashed`,
  `Dotted`, `Bold`, `Hidden`); `ActivityLink.None` is no arrow.
- `ActivityLane(Name, Display, Index, Fill, Location)` - a swimlane; `ActivityNote(Text, Position, Floating, Fill,
  Lane, Location)` - a note.
- `ActivityInstruction` - the base of every instruction: its swimlane, the arrow into it, its location, its notes, and
  `Kill`. The instructions are `ActivityList` (a column), `ActivityAction`, `ActivityStart`, `ActivityStop`,
  `ActivityEnd`, `ActivitySpot` (a connector), `ActivityIf` with its `ActivityBranch` list, `ActivityWhile`,
  `ActivityRepeat`, `ActivityFork` (with `ActivityForkStyle`), `ActivitySplit`, and `ActivityGroup`, as PlantUML's
  `Instruction*` classes hold them.

#### Key Methods

- **Facts** - the nodes are the actions and connectors, the start, stop, and end nodes, the tests of the ifs (one per
  if or elseif), the loops (one each, plus their backward actions and special exits); the edges are the arrows (one per
  instruction that follows another in a list, two per branch of an if, a fork, or a split, and three per loop); the
  subgraphs are the swimlanes and the framed groups; the title is the page title.
- `Layout(context)` - `ActivityLayout.Run`, `ActivityRenderer.Render`, and `ScenePage.Decorate` with
  `PageMetrics.Activity`, with the theme's background and the diagram's scale.

#### Error Handling

Null arguments throw `ArgumentNullException`; the layout's exceptions pass through.

#### Dependencies

The Styling subsystem (`SkinTheme`), the Scenegraph subsystem (`Scene`, `PageTexts`, `ScenePage`), and the layout and
renderer of the subsystem.

#### Callers

`ActivityParser` builds it; `PlantUmlDocument` asks it for its facts, reports, and scenes; the tests.

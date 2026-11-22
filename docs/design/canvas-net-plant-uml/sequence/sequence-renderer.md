### SequenceRenderer

![Sequence Structure](PlantUmlSequenceView.svg)

The `SequenceRenderer` unit is the fourth software unit in the `Sequence` subsystem. Its source files are
`Sequence/SequenceRenderer.cs` (the paint order, boxes, group fills, life lines, activations, and destroy crosses),
`Sequence/SequenceRenderer.Participants.cs` (the participant shapes), `Sequence/SequenceRenderer.Messages.cs` (arrows,
heads, self messages, and notes), and `Sequence/SequenceRenderer.Blocks.cs` (group frames and dividers).

#### Purpose

`SequenceRenderer` turns the placed diagram into scene items with PlantUML's shapes and the theme's colors. The unit's
behavior is specified by the `CanvasNetPlantUml-Sequence-SequenceRenderer-*` requirements in
`docs/reqstream/canvas-net-plant-uml/sequence/sequence-renderer.yaml`.

#### Data Model

N/A - the unit is stateless; it reads a `SequenceGeometry` and returns a `Scene`.

#### Key Methods

- `Render(model, geometry)` - draws, in PlantUML's paint order, the boxes (`box`, `box-text`), the group fills
  (`group-fill`), the life lines (`lifeline`: dashed 5 and 5, dotted 1 and 4 in delays), the activation boxes
  (`activation`: 10 wide, white, outlined), the heads and tails (`participant-head`, `participant-tail`, with the
  participant code as id), and the events in order; every item is then moved by the layout's offset and the view box is
  the body size.
- Participants: a box with corners of 2.5 and the text 7 pixels in; an actor's stick figure (a head of radius 8, a body
  27 long, arms 26 wide, legs 15 down and 13 out) with the text below the head and above the tail; a boundary (a T-bar
  and a circle of radius 12), a control (a circle with an arrow at its top), and an entity (a circle over a line), with
  their text 28 pixels below their top; a database cylinder 36 wide and 46 high; two stacked boxes 4 pixels apart; a
  queue; symbol texts are centered 3 pixels left of the column's center, as PlantUML draws them.
- Messages (`message`, `message-head`, `message-number`, `message-text`): the line in the arrow's color and the arrow
  thickness, dashed 2 and 2 when dotted; a filled head is a 10 by 8 arrow with its notch 6 from the tip, outlined, its
  tip 2 pixels inside on the right (1 on the left) and the line ending 4 pixels before it; an open head is two lines;
  a half head is the upper or lower half; a cross is two lines of width 2 in a 10-pixel square 13 pixels inside; a
  circle of radius 4 at the end moves the head 5.5 pixels in. The open head's lines have the arrow thickness, while the
  outline of a filled or half head is 1 pixel wide at any thickness, as in PlantUML (a thick mitered outline would grow
  the head over the label). A self message is three lines and a head pointing back
  to the life line. The number is drawn, then the label 4 pixels after it.
- Notes (`note`, `note-text`): a sheet with a 10-pixel folded corner (and the fold drawn again), a hexagon with
  10-pixel points, or a rectangle, in the note's or the theme's color.
- Groups (`group-header`, `group`, `group-text`, `group-else`): the tab (the keyword's width plus 45 wide, its height
  with a 10-pixel cut corner) filled `#EEEEEE`, the frame outline, the keyword 15 pixels in, the condition 15 pixels
  after the tab, and for each section a dashed line with its condition 5 pixels in.
- Dividers (`divider`, `divider-text`): a 3-pixel band and two thin lines across the diagram, and the text in an
  outlined box centered over them; delay texts (`delay-text`); a destroyed life line's cross (`destroy`), 18 pixels
  wide in PlantUML's `#A80036`.

#### Error Handling

Null arguments throw `ArgumentNullException`; the unit draws only what the layout placed.

#### Dependencies

The Styling subsystem (`SkinTheme`, `ElementStyle`), the Scenegraph subsystem (`Scene`, `ScenePath`, `SceneText`,
`SceneShapes`, `ScenePage.Translate`, `TextBlock.Draw`), the core `Canvas` subsystem (`Rgba32`), and the placed records.

#### Callers

`SequenceModel.Layout`; the Description subsystem's `DescriptionRenderer`, which draws the actors of use case
diagrams with the internal `StickFigure`, and the boundaries, controls, entities, and queues of deployment diagrams with
the internal `Symbol` and `Queue`; the tests.

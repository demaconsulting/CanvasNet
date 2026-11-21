### SequenceLayout

![Sequence Structure](PlantUmlSequenceView.svg)

The `SequenceLayout` unit is the third software unit in the `Sequence` subsystem. Its source files are
`Sequence/SequenceLayout.cs` (the placed records, the measurement, and the work meter),
`Sequence/SequenceLayout.Messages.cs` (the rows of messages, self messages, notes, and life changes, and the assembly of
the geometry), and `Sequence/SequenceLayout.Blocks.cs` (the column constraints, the group frames, the dividers, the
delays, the boxes, and the life lines).

#### Purpose

`SequenceLayout` places a sequence diagram as PlantUML's "teoz" sequence engine does, so that the picture agrees with
PlantUML's element by element. The unit's behavior is specified by the `CanvasNetPlantUml-Sequence-SequenceLayout-*`
requirements in `docs/reqstream/canvas-net-plant-uml/sequence/sequence-layout.yaml`.

#### Data Model

- `SequenceGeometry(Participants, Lifelines, Activations, Items, Boxes, OffsetX, Width, Height)` - the placed diagram
  before the page texts; `OffsetX` moves the leftmost extent to the 10-pixel margin; the width and height include the
  margins.
- `PlacedParticipant(Participant, PosB, PosC, Width, PrefHeight, Text, Style, HeadBottom, TailTop)` - the column (left
  edge, life line, width), the head height, and where the head ends and the tail starts (NaN with no foot box).
- `PlacedLifeline(Participant, X, Y1, Y2, Delay)` and `PlacedActivation(Participant, X, Y1, Y2, Fill)`.
- The placed events (`PlacedItem` with `Location`):
  `PlacedMessage(Message, XLeft, XRight, ArrowY, ToLeft, Label, Number, TextX, TextTop)`,
  `PlacedSelf(Message, StartX, LoopX, TopY, BottomY, Label, Number, TextX, TextTop)`,
  `PlacedNote(Note, X, Y, Width, Height, Text, TextX, TextTop)`,
  `PlacedGroup(Start, X, Y, Width, Height, Keyword, Label, TabWidth, TabHeight, Sections)` with `PlacedElse(Y, Label)`,
  `PlacedDivider(Text, X1, X2, Top, Height, RectWidth, RectHeight)`, `PlacedDelay(Text, X, Top)`, and
  `PlacedDestroy(X, Y)`; and `PlacedBox(Box, X, Y, Width, Height, Title)`.

#### Key Methods

- `Run(model, context)` - measures, solves the columns, and places the rows.
- **Measure.** Texts are laid out with the styles of the theme (participants 14 pixels, messages and notes 13, group
  conditions and delays 11, group tabs and dividers bold 13). A column is as wide as the text plus 14 (a box), at least
  27, 49, 32, 32, or 36 for the actor, boundary, control, entity, and database symbols (the text plus 6 when wider), the
  text plus 18 for collections and plus 20 for a queue; a head is as high as the text plus 15 (a box), 60, 28, 28, 28,
  or 46 plus the text for the symbols, the text plus 19 for collections, and plus 10 for a queue. A message's text is
  its label (wrapped at `maxMessageSize`) after its number and 4 pixels, plus 2 pixels of height. The activation level
  of both ends of every message is worked out in event order (an activation on the arrow counts at both ends, a
  deactivation only after it); a box title wider than its participants side by side widens the box by half the
  difference on each side.
- **Columns.** Every constraint reads "the life line of column j is at least d right of the life line of column i <
  j", so one pass in column order solves them: neighbor columns 10 pixels apart (plus 10 and the box widening at a box
  edge); a message pushes its right participant until the arrow is its text plus 24 (plus 5 for each level of the left
  end and 5 when the right end is active); a self message pushes the next participant past 5 times the participant's
  deepest level plus the wider of the text plus 14 and 50 (and the participant after its box past that plus 20); a
  group pushes the participant after its last participant 25 pixels away, past its header (its tab of the keyword
  plus 45, then 15 and the condition) and past the reach of its self messages and notes plus 9, and its first
  participant 25 pixels from the one before (nested groups reach 25 further on the right and 19 on the left). Notes do
  not push participants, as in PlantUML. The diagram is then moved so that its leftmost extent (a column, a note's
  layout box, a frame 3 pixels outside its line) is at 10.
- **Rows.** The heads end at the bottom of the head row (10, or below the box titles plus 5), the life lines start
  there, and the first row starts 8 pixels lower. A message row is its text height plus 12, with the arrow 4 pixels
  below the text; the arrow meets the right edge of the innermost activation box on the left (the life line plus 5
  per level) and its left edge on the right; the text starts 7 pixels right of the left end (17 when a head is there).
  For an arrow thicker than 1 pixel (`sequenceArrowThickness`), the text height of a message gains half the extra
  thickness (`ArrowClearance`), which moves the arrow (and a self message's loop) that much further from a label above
  it and grows the row; a label below a response arrow (`responseMessageBelowArrow`) moves down by as much. At the
  default thickness the clearance is 0 and the rows are PlantUML's.
  A self message row is its text plus 25: the loop leaves at the life line plus 5 per level before the arrow, measures
  from the level after it, runs 42 pixels right, 13 down, and back. A note's layout box is its text width plus 31 (a
  hexagon plus 34, a rectangle plus 18) and its text height plus 20 (plus 18): left of a life line it ends there, right
  of it it starts there (plus 5 per level), over one participant it is centered, over two it spans their columns when
  that is wider; its outline is 5 pixels inside the box, in whole pixels. Notes on a message's row share the row. A
  group adds 4 pixels, its tab (bold 13-pixel text plus 2), and 10 above its content, and 14 below; its frame is 16
  pixels outside its content and at least its header plus 16; an `else` row is its condition plus 6 with the dashed
  line 1 pixel down. A divider row is its text box (the text and a space plus 14 wide, plus 8 high) plus 20, across the
  diagram from its left extent to its right extent (or the box plus 24); a delay row is 28 pixels (plus its text, drawn
  14 pixels down, centered between the first and last life lines) with dotted life lines; a space is its height. The
  life lines end 10 pixels below the last row (a destroyed one 8 pixels below its arrow); the tails start there unless
  the foot box is hidden. The body is as wide as its extents plus the margins and as high as the bottom of the tails (or
  the life lines) plus 10 (plus 10 with boxes, which end 6 pixels below the tails).
- **Work.** Every text, event, and constraint counts as work against `MaxLayoutWork`, and the cancellation token is
  checked every 256 units.

#### Error Handling

More work than `MaxLayoutWork` throws `PlantUmlRenderException` with `plantuml-limit-exceeded`, located at the
construct the type was detected by; cancellation throws `OperationCanceledException`. Null arguments throw
`ArgumentNullException`.

#### Dependencies

The Styling subsystem (`SkinTheme`, `ElementStyle`), the Scenegraph subsystem (`TextLayout`, `TextBlock`,
`SceneFont`), and the model records.

#### Callers

`SequenceModel.Layout`; the tests.

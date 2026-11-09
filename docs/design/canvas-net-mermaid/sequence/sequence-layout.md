### SequenceLayout

![Sequence Structure](MermaidSequenceView.svg)

The `SequenceLayout` unit is the third software unit in the `Sequence` subsystem. It comprises four source files in
the `DemaConsulting.CanvasNet.Mermaid.Sequence` namespace: `SequenceLayout.cs` (the entry point `Draw`, the
participant margins and placement, the boxes, and the view box), `SequenceLayout.Bounds.cs` (the layout state types
and Mermaid's `bounds` object), `SequenceLayout.Messages.cs` (the walk over the messages: message and note models,
activations, blocks, and created and destroyed participants), and `SequenceLayout.Blocks.cs` (the end of a block and
the menus). `SequenceLayout` is an `internal sealed partial` class, so the four files are one type.
`SequenceLayout.Bounds.cs` defines the layout state types `SequenceActorLayout`, `SequenceBoxLayout`,
`SequenceMessageModel`, `SequenceNoteModel`, `SequenceLoopModel`, `SequenceLoopSection`, `SequenceActivation`,
`SequenceMessageState`, and `SequenceBounds` (sealed classes) and `SequenceRectData` (a sealed record). Every file
carries the provenance header comment for the parts of Mermaid it ports.

#### Purpose

`SequenceLayout` is Mermaid's sequence renderer (`sequenceRenderer.ts`) for one render of a `SequenceModel` under the
effective configuration of that render: it sizes the participants and the margins between them from the texts they
carry, places every message, note, activation, and block from top to bottom with Mermaid's `bounds` object, draws the
participants, the mirrored participants, the backgrounds, the boxes, and the menus through the `SequenceRenderer`,
and computes the view box. It exists so that the size of a sequence diagram and the place of every element are
Mermaid's for the same text and configuration.

The unit's behavior is specified by the `CanvasNetMermaid-Sequence-SequenceLayout-*` requirements in
`docs/reqstream/canvas-net-mermaid/sequence/sequence-layout.yaml`.

#### Data Model

**A layout instance** draws one render on one thread and holds the model, the render context, the `SequenceConfig`
of the render, the `LayoutWorkMeter` of the render (with `MaxLayoutWork`, the location of the diagram keyword, and
the cancellation token), a `LabelBuilder` charged to that meter, the `SequenceBounds`, the `SequenceRenderer` with the
`SequenceStyles` of the configuration, and the per-render state of every participant, box, and message:

- **`SequenceActorLayout`** - Mermaid's actor object with the fields its renderer adds: the description after
  wrapping, the width, height, and margin, the position, the vertical extent of the drawing and its lifeline, the box,
  the `SequenceRectData` of the last drawing (which places its menu; null for the types without a box), and the
  number of the participant drawn at the top (`actorCnt`).
- **`SequenceBoxLayout`** - a box with its margin, the height of its title, its extent, and its fill.
- **`SequenceMessageState`** - the text and wrap setting of a message after the renderer changed them, and its
  drawing model: a **`SequenceMessageModel`** (the ends of the line, the wrapped text, its width and height, and the
  sequence number) or a **`SequenceNoteModel`** (the left edge, the width, the top, and the wrapped text).
- **`SequenceBounds`** - Mermaid's `bounds` object: the extent of the drawing (`StartX`, `StartY`, `StopX`, `StopY`,
  null until something is inserted), the vertical position of the next item, the stack of open blocks
  (`SequenceLoopModel`, with its sections `SequenceLoopSection`, title, fill, and the location of its start), the open
  activations (`SequenceActivation`, with the anchor group it is drawn into), and the models drawn so far.

`ActorTypeWidth` (36) is Mermaid's `ACTOR_TYPE_WIDTH`, the width of the drawings of `actor`, `control`, `entity`, and
`database`.

#### Key Methods

- **`Draw()`** is Mermaid's `draw`, in Mermaid's order:
  1. `GetMaxMessageWidthPerActor` measures every message between neighbors, every self message, and every note (wrapped
     to the participant width less the wrap padding when it wraps), and `CalculateActorMargins` sizes the
     participants from their measured descriptions and widens the margin between two neighbors so that the widest text
     between them fits with the wrap padding; it also sizes the boxes and their titles and gives every participant
     the height of the tallest;
  2. the room for the boxes and the title of the first box is left above the participants;
  3. the participants drawn are all of them, or with `hideUnusedParticipants` only those that a message names;
     `AddActorRenderingData` places them from left to right with the box margins and the created participants;
  4. `CalculateLoopBounds` sizes the blocks in a dry run over the messages, building the message and note models with
     the activations of the dry run;
  5. `DrawMessages` walks every message in order with the bounds: notes are drawn as they come (`DrawNote`),
     activations are opened and closed and drawn (`NewActivation`, `ActiveEnd`, stacked activations moving right by
     half the activation width), block starts and sections leave room for their wrapped and bracketed titles
     (`AdjustLoopHeightForWrap`), block ends close the block and draw its frame (`EndLoop`; a `rect` block is kept to
     draw behind everything), `autonumber` statements set the numbering (start, step, rounded to hundredths, and
     `off`), and every arrow is placed (`BoundMessage`), moved for a created or destroyed participant
     (`AdjustCreatedDestroyedData`), and kept to be drawn after the participants;
  6. the participants are drawn (`DrawActors`), then the kept arrows with their sequence numbers when `autonumber` or
     `showSequenceNumbers` turns them on, then the mirrored participants when `mirrorActors` is set;
  7. the `rect` backgrounds go behind everything drawn so far, the lifelines are ended (at the mirrored participant,
     or at y 2000 without mirrored participants), and `LayoutBoxes` sizes the boxes and draws them behind everything;
  8. `DrawActorsPopup` draws the menus of the drawn participants with links and gets the size they need (a hidden menu
     still counts), and `ViewBox` draws the title (the `title` statement's, else the frontmatter's, sanitized) and
     returns the scene.
- **`BuildMessageModel`** places the ends of an arrow at the activations of both participants (`ActivationBounds`),
  shortened for the arrowheads, moved for central connections (`CentralConnectionOffset`), with a self message as
  Mermaid's loop to the right of the lifeline; **`BuildNoteModel`** places a note by its placement (left of, right
  of, or over one or two participants) with its width and wrapped text.
- **`AdjustCreatedDestroyedData`** draws a created participant at its creating message, centered on its line, which
  ends at the side of its box (`ActorTypeWidth` for the figure types), and ends a destroyed participant's lifeline at
  its destroying message, where its mirrored drawing is placed.
- **`ViewBox`** computes Mermaid's view box: the extent of the bounds (at least the size the menus need) grown by
  `diagramMarginX` and `diagramMarginY`, with `bottomMarginAdj` less the box margin when the participants are
  mirrored, and 40 pixels more at the top for a title, drawn 25 pixels above the diagram.
- **`JsMax`** and **`JsMin`** are Mermaid's `common.getMax` and `getMin`: values that are not set or not a number are
  left out.
- **`SequenceBounds`**: `Insert` grows the drawing and every open block and activation, each block by its depth times
  the box margin (`UpdateBounds`); `BumpVerticalPos` moves the vertical position down; `NewLoop`, `AddSectionToLoop`,
  and `EndLoop` open, divide, and close blocks; `ActorActivations` and `EndActivation` give and close the open
  activations of a participant. Every insertion is charged to the layout work meter.

**Preconditions and postconditions**: the model is a parsed `SequenceModel`; the result is a `Scene` whose view box
is Mermaid's and whose items are in Mermaid's paint order. The layout does not change the model.

#### Error Handling

- The work of the measuring, wrapping, and placing is charged to the layout work meter; beyond `MaxLayoutWork` it is a
  `MermaidRenderException` with code `mermaid-limit-exceeded` located at the diagram keyword, and a canceled token is
  an `OperationCanceledException`.
- A block with nothing inside, which Mermaid gives no extent and so draws the whole diagram at coordinates that are not
  numbers, is a `MermaidRenderException` with code `mermaid-render-failed` located at the block's start, with a hint.
- The render errors of the renderer and of the text utilities (links on a participant type without a box, an icon that
  is not text, a font size of zero, a line of only spaces between breaks) propagate unchanged.

#### Dependencies

- **SequenceModel** - the participants, messages, notes, boxes, created and destroyed participants, the title, and
  `EffectiveWrap`.
- **SequenceRenderer** - every drawing (participants, messages, notes, activations, frames, backgrounds, boxes,
  menus, and the title), which returns the heights the layout continues from, and the paint order.
- **Configuration subsystem** - `SequenceConfig` (every setting of the layout) and `SequenceStyles.Create` (the style
  sheet of the render).
- **Rendering subsystem** - `LabelBuilder` (`CalculateTextDimensions` and `WrapLabel`).
- **Dagre subsystem** - `LayoutWorkMeter` (the work budget and cancellation of the render).
- **Syntax subsystem** - `HtmlSanitizer` (the frontmatter title).
- **Scenegraph subsystem** - `Scene` and `SceneRect`.
- **System** - `DiagramRenderContext`, `MermaidLocation`, `MermaidRenderException`, and `MermaidDiagnosticCodes`.

#### Callers

- **SequenceModel** - `Layout` creates a layout for each render and calls `Draw`.
- The unit tests lay texts out through the document pipeline and call `JsMax` and `JsMin` directly.

#### Known Differences From Mermaid

- **Measured heights.** Where Mermaid reads `getBBox` of the SVG it has drawn, the layout uses the heights that the
  renderer returns from the text utilities of `LabelBuilder`, which emulate the browser's SVG text boxes with the
  bundled fonts (see _CanvasNetMermaid System Design_, Fidelity and Known Differences).
- **An empty block** fails the render at its start instead of giving a picture with coordinates that are not numbers.
- **Work limit.** The layout work is bounded by `MaxLayoutWork`, which Mermaid does not have.
- **Hidden menus** are empty groups whose size still counts in the view box, as Mermaid's hidden menus do.

---
title: Sequence diagrams
summary: Participants, messages, activations, notes, groups, boxes, and numbers of a sequence diagram.
tags:
  - plantuml
  - authoring
see-also:
  - plantuml/style
  - plantuml/options
  - plantuml/diagnostics
examples:
  - name: sequence
    kind: source
    language: plantuml
    file: sequence.puml
    description: A small sequence diagram with an actor, an activation, numbered messages, an alt group, and a note.
---

## Supported

- Sequence diagrams: a diagram whose statements are all sequence statements is a
  sequence diagram. It is laid out like PlantUML's teoz engine: the participants
  stand in a row, and every message, note, and group is placed below the one
  before it.
- Participants: `participant`, `actor`, `boundary`, `control`, `entity`,
  `database`, `collections`, and `queue`, with a quoted display name, `as` for
  an alias, a `#color`, and `order N`; a participant named only by a message is
  a participant too.
- Messages: the arrows `->`, `-->`, `->>`, `-->>`, `<-`, `<--`, `<->`, `-\`,
  `-/`, `->x`, `->o`, and `-[#color]>` (two dashes are dotted), self messages,
  and a label after a colon with `\n` line breaks.
- Activations: `activate` and `deactivate` (with a color), `destroy`, `return`,
  and the `++`, `--`, `**`, and `!!` shorthands of a message.
- Notes: `note`, `hnote`, and `rnote` `left`, `right`, or `over` one or two
  participants, on one line or up to `end note`, with a color.
- Groups: `alt` and `else`, `opt`, `loop`, `par`, `break`, `critical`, and
  `group`, nested, with a color and a condition, closed by `end`.
- Separators: `== divider ==`, `...` delays (with a text), and `|||` or `||N||`
  space.
- Boxes: `box "title" #color` ... `end box` around participants.
- Numbers: `autonumber` with a start, a step, and a format, `autonumber stop`,
  and `autonumber resume`.
- Page texts: `title`, `header`, `footer`, `caption`, and `legend` ...
  `endlegend`, and `hide footbox`.
- Scale: `scale N` and `scale N/M` multiply the natural size.

## Known differences

- ref over, newpage, create, mainframe, autoactivate, messages to or from the
  border ([->, ->], ?->), parallel messages (&), and stereotypes of participants
  are reported as plantuml-statement-unsupported and not drawn.
- With sequenceArrowThickness above 1, a message label is moved half the extra
  thickness further from its arrow (PlantUML lets the thick line touch it).
- !pragma teoz is reported as ignored: PlantUML 1.2026.10 draws every sequence
  diagram with teoz, and so does this version.

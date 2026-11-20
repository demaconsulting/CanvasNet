---
title: State diagrams
summary: States, transitions, composite states and regions, pseudo-states, and notes of a state diagram.
tags:
  - plantuml
  - authoring
see-also:
  - plantuml/class
  - plantuml/style
  - plantuml/options
  - plantuml/diagnostics
examples:
  - name: state
    kind: source
    language: plantuml
    file: state.puml
    description: A small state diagram with a start and an end, a composite state, a choice, a history, and a note.
---

## Supported

- State diagrams: a diagram of state declarations and transitions (`state`,
  `[*]`) is a state diagram, recognized in the order PlantUML tries its
  diagram types. It is drawn with PlantUML's shapes and look and laid out with
  the dagre layered layout instead of Graphviz.
- States: `state Name`, `state "Long name" as Name` (or `state Name as "Long
  name"`), and a name used only by a transition, drawn as a rounded box with
  the name above a line; `#color` fills a state and `##color` outlines it.
- Descriptions: `state Name : text` and `Name : text` add a line below the
  name, `\n` breaks a line, and a line of four or more dashes (`----`) draws a
  rule; `hide empty description` draws a state without descriptions as a box
  with its name alone.
- Transitions: `A --> B` with a label after a colon (`A --> B : event [guard]
  / action`), `B <-- A`, the length of the arrow (one dash keeps both ends
  side by side, each further dash adds a rank), `-up->`, `-down->`, `-left->`,
  and `-right->` (or `-u->`, `-d->`, `-l->`, `-r->`), and the styles
  `-[hidden]->`, `-[#color]->`, `-[dashed]->`, `-[dotted]->`, `-[bold]->`, and
  `-[thickness=N]->`.
- Start and end: `[*]` is the start of the current composite state when it
  begins a transition and its end when it ends one, drawn as a dark circle and
  as a ring around a dark circle.
- Composite states: `state Name { ... }` (or `state Name begin` ... `end
  state`), nested, each with its own start and end; a transition can end at a
  composite state or at a state inside one.
- Concurrent regions: `--` stacks the regions of a composite state and `||`
  puts them side by side, separated by dashed lines.
- Pseudo-states: `<<choice>>` (a diamond), `<<fork>>` and `<<join>>` (a bar),
  `<<end>>`, `<<start>>`, and `<<junction>>`, and the history `[H]` and the
  deep history `[H*]` of the current composite state, or `Name[H]` and
  `Name[H*]` of a named one (also `<<history>>` and `<<history*>>`).
- Notes: `note left`, `right`, `top`, or `bottom of Name`, a note after a
  declaration (`note left : text`), `note on link`, and floating notes (`note
  "text" as N`), on one line or up to `end note`, with a color.
- Page texts: `title`, `header`, `footer`, `caption`, and `legend` ...
  `endlegend`, `left to right direction` and `top to bottom direction`, and
  `scale N`.

## Known differences

- The states, pseudo-states, composite states, notes, and arrows have
  PlantUML's shapes, sizes, and look, but the dagre layout places them
  differently from Graphviz, so the picture has another size; a one-dash,
  `-left->`, or `-right->` transition always keeps both ends side by side.
- Entry and exit points and pins (`<<entryPoint>>`, `<<exitPoint>>`,
  `<<inputPin>>`, `<<outputPin>>`, `<<expansionInput>>`,
  `<<expansionOutput>>`, drawn as states), `<<sdlreceive>>`, a cross at the
  start of a transition (`x-->`), stereotypes of a transition, `hide` forms
  other than `hide empty description`, and a transition that leaves the first
  region of a composite state with concurrent regions (drawn from its border)
  are reported as plantuml-statement-unsupported.
- A transition between two concurrent regions is a plantuml-syntax-error, as
  PlantUML refuses it.
- A state named like a floating note is a plantuml-syntax-error, where
  PlantUML accepts it; both refuse a floating note named like a state.

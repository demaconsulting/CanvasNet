---
title: Use case diagrams
summary: Actors, use cases, boundaries, include and extend links, and notes of a use case diagram.
see-also:
  - plantuml/component
  - plantuml/style
  - plantuml/options
  - plantuml/diagnostics
examples:
  - name: usecase
    kind: source
    language: plantuml
    file: usecase.puml
    description: A small use case diagram with actors, a boundary, include and extend links, and a note.
---

## Supported

- Use case diagrams: a diagram of description elements and links is a use case
  diagram when a statement names an actor or a use case (`actor`, `usecase`,
  `:Name:`, `(Name)`), recognized in the order PlantUML tries its diagram
  types. It is drawn with PlantUML's shapes and look and laid out with the
  dagre layered layout instead of Graphviz.
- Actors: `actor Name`, `:Name:`, `actor "Long name" as A` (or `actor A as
  "Long name"`), and `:Long name: as A`, drawn as PlantUML's stick figure with
  the name below it; `actor/ Name` and `:Name:/` draw a business actor, with a
  slash across the head.
- Use cases: `usecase Name`, `(Name)`, `usecase "Long name" as U` (or `usecase
  U as "Long name"`), and `(Long name) as U`, drawn as an ellipse sized around
  the name; `usecase/ Name` and `(Name)/` draw a business use case, with a
  slash across the ellipse.
- Names: `\n` breaks a line of a name, stereotypes (`<<name>>`, several) are
  written above it, and `#color` or `#back;line:color;text:color` colors an
  element; a name used only by a link is an actor.
- Boundaries: `rectangle Name { ... }`, `package Name { ... }`, and the other
  containers of a component diagram, nested, with a quoted title, stereotypes,
  and a `#color`, around actors and use cases.
- Links: `-->` and `..>` (dashed) with an arrowhead, `--` and `..` without
  one, extension `<|--` and `--|>`, in either direction, a label after a
  colon, `<<include>>` and `<<extend>>` labels written with guillemets
  (`«include»`), and the lengths, direction hints, and styles of the links of
  a component diagram.
- Notes: `note left`, `right`, `top`, or `bottom of Name`, a note after a
  declaration (`note left : text`), `note on link`, and floating notes (`note
  "text" as N`) linked with `..`, on one line or up to `end note`, with a
  color.
- Page texts: `title`, `header`, `footer`, `caption`, and `legend` ...
  `endlegend`, `left to right direction` and `top to bottom direction`, and
  `scale N`.

## Known differences

- The actors, use cases, boundaries, notes, and arrows have PlantUML's shapes,
  sizes, and look (a use case's ellipse is sized around its texts as PlantUML
  sizes it), but the dagre layout places them differently from Graphviz, so
  the picture has another size; the differences of component diagrams apply
  too (see `canvasnet help plantuml component`).
- Double angle brackets in the name of an element (`(Check <<x>>)`) are
  drawn as written, where PlantUML writes guillemets in every text.
- skinparam actorStyle (the awesome and hollow actors) is reported as
  plantuml-skinparam-ignored.
- An actor or a use case that holds elements is a plantuml-syntax-error, as in
  PlantUML.

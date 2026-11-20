---
title: Deployment diagrams
summary: Nodes, artifacts, databases, clouds, and the other symbols and containers of a deployment diagram.
tags:
  - plantuml
  - authoring
see-also:
  - plantuml/component
  - plantuml/style
  - plantuml/options
  - plantuml/diagnostics
examples:
  - name: deployment
    kind: source
    language: plantuml
    file: deployment.puml
    description: A small deployment diagram with a cloud, a node that holds an artifact, a database, and a queue.
---

## Supported

- Deployment diagrams: a diagram of description elements and links without an
  actor, a use case, a component, an interface, or a port is a deployment
  diagram (`node`, `artifact`, `database`, `cloud`, ...), recognized in the
  order PlantUML tries its diagram types. It is drawn with PlantUML's shapes
  and look and laid out with the dagre layered layout instead of Graphviz.
- Elements: `node`, `artifact`, `database`, `cloud`, `folder`, `package`,
  `frame`, `rectangle`, `card`, `file`, `storage`, `queue`, `stack`, `agent`,
  `hexagon`, `person`, `boundary`, `control`, `entity`, `collections`, and
  `label`, each drawn with PlantUML's symbol, and `circle`, drawn as an
  interface, each with a display name (`as`), stereotypes, and `#color` or
  `#back;line:color;text:color`.
- Containers: every symbol but `person`, `boundary`, `control`, `entity`,
  `collections`, `label`, `agent`, and `circle` holds elements with `{ ... }`,
  nested, drawn with its own outline, a quoted title, stereotypes, and a
  `#color`.
- Links, notes, and page texts: the links, notes, page texts, direction, and
  `scale` of a component diagram, between elements and containers at any
  depth.

## Known differences

- The symbols, containers, notes, and arrows have PlantUML's shapes, sizes,
  and look, but the dagre layout places them differently from Graphviz, so the
  picture has another size; the differences of component diagrams apply too
  (see [[plantuml/component]]).
- A hexagon is drawn with the outline PlantUML's Graphviz pipeline gives it;
  the Smetana layout of PlantUML draws only its texts.
- A floating note with one link whose route is longer than a direct one is
  drawn with a dashed link, where PlantUML draws the note's pointer.
- The action, the process, and archimate elements are drawn as rectangles and
  reported as plantuml-statement-unsupported; a circle is drawn as an
  interface, as in PlantUML.
- A person, a boundary, a control, an entity, a collections symbol, a label, an
  agent, or a circle that holds elements is a plantuml-syntax-error, as in
  PlantUML.

---
title: Object diagrams
summary: Objects with fields, maps, and the links between them of an object diagram.
tags:
  - plantuml
  - authoring
see-also:
  - plantuml/class
  - plantuml/style
  - plantuml/options
examples:
  - name: object
    kind: source
    language: plantuml
    file: object.puml
    description: Two objects with fields, a map, and labeled links.
---

## Supported

- Object diagrams: a diagram whose first declaration is an `object` or a `map`
  is an object diagram; it is read and drawn like a class diagram, and classes
  and objects can be mixed.
- Objects: `object Name` or `object "Display" as Name`, with stereotypes and a
  `#color`, and fields in a body `{ ... }` or as `Name : field = value`;
  separators divide the fields.
- Maps: `map Name { key => value }`, drawn as a table of the keys and their
  values.
- Links: the relationships, labels, layout hints, notes, packages, and page
  texts of a class diagram.

## Known differences

- An object diagram is laid out with the dagre layered layout instead of
  Graphviz, like a class diagram.
- A link from a map entry (key *--> Name) is drawn from the map and reported
  as plantuml-statement-unsupported.

---
title: PlantUML diagrams (.puml, .plantuml, .pu)
summary: 'PlantUML (.puml): sequence and activity diagrams.'
aliases:
  - puml
children:
  - sequence
  - activity
  - preprocessor
  - style
  - composition
  - options
  - diagnostics
  - unsupported
  - differences
see-also:
  - style
  - composition
  - svg
  - limits
  - pandoc
---

A PlantUML sequence diagram or activity diagram (the new syntax) is laid out
like PlantUML 1.2026.10 and drawn with its default look and the bundled fonts,
with no Java, no Graphviz, and no network. Every other PlantUML diagram type is
recognized and reported (see `canvasnet help plantuml unsupported`).

Read the card of your diagram type, for example `canvasnet help plantuml
sequence`; each has a small source example (`--example sequence`). Render with
`canvasnet render seq.puml -o seq.png --json` and look at the PNG.

For colors and fonts read `canvasnet help plantuml style`; to place a diagram
in a figure or a Mermaid node, `canvasnet help plantuml composition`.

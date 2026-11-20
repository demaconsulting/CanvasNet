---
title: PlantUML diagrams in figures, nodes, and documents
summary: A PlantUML diagram placed in an SVG figure, a Mermaid node, or a contact sheet.
tags:
  - plantuml
  - composition
see-also:
  - composition
  - plantuml/style
  - svg/embedding
  - svg/composition
---

A PlantUML diagram is drawn wherever a CanvasNet picture can be placed, sharply
at the size of its placement and over a transparent background.

## Supported

- SVG figure: `<image href="seq.puml" x="40" y="120" width="560" height="420"/>`
  draws the diagram that lies beside the SVG file; keep the aspect ratio of its
  natural size (the size it is drawn at when no size is asked for).
- Mermaid node: `p@{ img: "seq.puml", w: 240, h: 200 }` shows the diagram in
  a node of a Mermaid flowchart (see [[mermaid/composition]]).
- Contact sheets and flattening: a contact sheet (`MontageBuilder` of the
  Imaging namespace) takes .puml inputs, and flattening
  ([[svg/flatten]]) replaces a linked .puml with a PNG for other
  SVG readers.
- Includes: an embedded diagram reads its !include files from its own folder
  under the same file-access mode.

## Unsupported

- A fragment on a .puml link (seq.puml#theme=dark) is drawn without it and
  reported as image-fragment-unknown-parameter: a PlantUML diagram takes no
  fragment parameter.
- A PlantUML diagram reports no feature positions, so position selectors and SVG
  position bindings cannot point at its participants or actions.

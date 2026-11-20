---
title: PlantUML diagrams in figures, nodes, and documents
summary: A PlantUML diagram placed in an SVG figure, a Mermaid node, a montage, or a Pandoc document.
see-also:
  - composition
  - plantuml/style
  - svg/embedding
  - svg/composition
  - pandoc
examples:
  - name: figure
    kind: command
    language: shell
    content: 'canvasnet render figure.svg --width 1200 -o figure.png --strict --json'
    description: Draw an SVG figure whose image elements link .puml diagrams beside it.
  - name: side-by-side
    kind: command
    language: shell
    content: 'canvasnet montage login.puml checkout.puml --labels --cell 480x480 -o flows.png'
    description: Two diagrams side by side, one cell each.
---

A PlantUML diagram is drawn wherever a CanvasNet picture can be placed, sharply
at the size of its placement and over a transparent background.

## Supported

- SVG figure: `<image href="seq.puml" x="40" y="120" width="560"
  height="420"/>` draws the diagram that lies beside the SVG file; read its
  natural size with `canvasnet info seq.puml --json` and keep that aspect
  ratio.
- Mermaid node: `p@{ img: "seq.puml", w: 240, h: 200 }` shows the diagram in
  a node of a Mermaid flowchart (see `canvasnet help mermaid composition`).
- Pandoc: a code block of class plantuml and a picture whose source ends in
  .puml, .plantuml, or .pu are drawn by the filter (see `canvasnet help
  pandoc`).
- Montage and flatten: canvasnet montage takes .puml inputs, and canvasnet
  flatten replaces a linked .puml with a PNG for other SVG readers.
- Includes: an embedded diagram reads its !include files from its own folder
  under the same file-access mode.

## Unsupported

- A fragment on a .puml link (seq.puml#theme=dark) is drawn without it and
  reported as image-fragment-unknown-parameter: a PlantUML diagram takes no
  fragment parameter.
- A PlantUML diagram reports no feature positions, so --positions and SVG
  position bindings cannot point at its participants or actions.

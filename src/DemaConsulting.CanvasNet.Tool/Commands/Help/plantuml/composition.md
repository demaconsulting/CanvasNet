---
extends: plantuml/composition
summary: A PlantUML diagram placed in an SVG figure, a Mermaid node, a montage, or a Pandoc document.
see-also:
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

## Command line

- Natural size: read it with `canvasnet info seq.puml --json`.
- Pandoc: a code block of class plantuml and a picture whose source ends in
  .puml, .plantuml, or .pu are drawn by the filter (see
  [[pandoc]]).
- Montage and flatten: canvasnet montage takes .puml inputs, and canvasnet
  flatten replaces a linked .puml with a PNG for other SVG readers.
- `--positions`: Not for a PlantUML diagram.

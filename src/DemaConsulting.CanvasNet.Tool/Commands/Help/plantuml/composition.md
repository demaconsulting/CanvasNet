---
extends: plantuml/composition
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

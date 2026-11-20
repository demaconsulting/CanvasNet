---
extends: svg/bindings
examples:
  - name: render-bound
    kind: command
    language: shell
    content: 'canvasnet render framed-flow.svg --width 1200 -o framed.png --strict --json'
    description: Draw the framed flowchart; each resolved binding is a bind operation with the numbers it wrote, and a binding that fails is an error.
  - name: flatten-bound
    kind: command
    language: shell
    content: 'canvasnet flatten framed-flow.svg -o framed-flat.svg --strict --json'
    description: Write a copy for other SVG readers with the bound numbers baked in and the cn attributes removed.
---

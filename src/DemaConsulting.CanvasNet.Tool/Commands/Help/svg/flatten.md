---
extends: svg/flatten
examples:
  - name: flatten
    kind: command
    language: shell
    content: 'canvasnet flatten dashboard.svg -o dashboard-flat.svg --scale 2 --json'
    description: Write a copy of a drawing whose linked diagrams, models, charts, and document pages are PNG pictures twice their placement size, for an SVG reader that cannot draw them.
---

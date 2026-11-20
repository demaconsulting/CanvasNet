---
extends: scad/anchors
see-also:
  - json/results
examples:
  - name: render-anchors
    kind: command
    language: shell
    content: 'canvasnet render anchors.scad --view top --size 600x400 -o anchors.png --json'
    description: Draw anchors.scad from above and list its anchors with their pixel positions in the JSON result.
---

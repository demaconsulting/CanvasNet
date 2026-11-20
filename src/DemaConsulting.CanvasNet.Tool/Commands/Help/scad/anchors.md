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

## Command line

- With --json, render adds anchors[] for a model: {name, point: [x, y, z],
  pixel: [x, y], inside}. inside is true when pixel lies within the picture
  (false when pixel is null). Without --json nothing is added.
- The pixel steps --crop, --trim, --resize, --fit, --pad, --flip, and --rotate
  are applied to pixel, and inside is tested against the final picture. After a
  --deskew that corrects the image, pixel is null.

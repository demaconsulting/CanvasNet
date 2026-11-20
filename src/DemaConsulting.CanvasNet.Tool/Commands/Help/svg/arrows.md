---
extends: svg/arrows
examples:
  - name: render-arrows
    kind: command
    language: shell
    content: 'canvasnet render arrows.svg --width 1200 -o arrows.png --strict --json'
    description: Draw the figure 1200 pixels wide; look at every tip in the PNG.
---

## Command line

- Render with `--strict` and look at the PNG; draw it at a larger `--width` with
  `--crop` around a tip to see it land.

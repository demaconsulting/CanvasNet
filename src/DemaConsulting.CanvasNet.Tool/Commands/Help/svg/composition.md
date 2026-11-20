---
extends: svg/composition
examples:
  - name: render-report
    kind: command
    language: shell
    content: 'canvasnet render report.svg --width 1200 -o report.png --strict --json'
    description: Draw the figure 1200 pixels wide and fail if a picture is not drawn.
---

## Command line

- `canvasnet info styled-flow.mmd --json` reports the natural size of a picture
  (`image.width` and `image.height`).
- Render a model alone with `--json` to read the pixel of each anchor
  (`anchors[].pixel`).
- Render with `--strict`, and look at the PNG; `--file-access` chooses which
  local files the links may read.
- Write the copy for other SVG readers with
  `canvasnet flatten report.svg -o report.flat.svg`.

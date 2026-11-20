---
extends: composition
examples:
  - name: themes
    kind: command
    language: shell
    content: 'canvasnet montage "flow.mmd#theme=default" "flow.mmd#theme=forest" "flow.mmd#theme=neutral" --labels --cell 480x240 -o themes.png'
    description: Three themes of one diagram side by side, each chosen with the fragment of its input.
  - name: publish
    kind: command
    language: shell
    content: 'canvasnet flatten figure.svg -o figure.flat.svg'
    description: Write a copy of a figure whose diagrams, charts, models, and pages are PNG pictures, for any SVG reader.
---

## Command line

- Variants or pages side by side: `canvasnet montage`, one cell per input; the
  fragment works on a command-line input and in a montage.
- Natural size: `canvasnet info file --json` (`image.width`, `image.height`).
- A feature: render the picture alone with `--size` equal to its box and
  `--json`: a model reports `anchors[].pixel`, a diagram or chart
  `positions[].pixel` for what `--positions` selects. For a point no selector
  names, read its x,y from `canvasnet render file --size WxH --grid 50` and
  confirm it with `canvasnet inspect pixel file x,y --size WxH`.
- `--strict` makes a picture that is not drawn an error; `--file-access`
  confines the links.
- Render, look, fix:
  `canvasnet render figure.svg --width 1200 -o figure.png --strict --json`, then
  `canvasnet compare` measures what changed.

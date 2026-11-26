---
extends: mathml/composition
summary: An equation placed in an SVG figure, a Mermaid node, a chart, a montage, or a Pandoc document.
see-also:
  - pandoc
examples:
  - name: figure
    kind: command
    language: shell
    content: 'canvasnet render figure.svg --width 1200 -o figure.png --strict --json'
    description: Draw an SVG figure whose image elements link .mml equations beside it.
  - name: side-by-side
    kind: command
    language: shell
    content: 'canvasnet montage ohm.mml quadratic.mml --labels --cell 480x160 -o equations.png'
    description: Two equations side by side, one cell each.
---

## Command line

- Natural size: read it with `canvasnet info quadratic.mml --json` (image
  width and height, and the baseline in the equation object).
- Example file: `canvasnet help mathml fractions --example quadratic` prints
  the quadratic formula; save it as quadratic.mml.
- Pandoc: a code block of class mathml and a picture whose source ends in .mml
  are drawn by the filter (see [[pandoc]]). Unless size or
  scale is given the equation is drawn at scale 2, and without a size, width,
  or height it is shown at its natural width (width="138px" for the quadratic
  formula), so every equation of the document shows at the 16 px font size; an
  author's size, width, height, or scale is kept.
- Montage and flatten: canvasnet montage takes .mml inputs, and canvasnet
  flatten replaces a linked .mml with a PNG for other SVG readers.
- `--positions`: Not for an equation.

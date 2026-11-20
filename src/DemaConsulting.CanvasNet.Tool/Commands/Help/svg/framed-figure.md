---
extends: svg/framed-figure
examples:
  - name: read-anchors
    kind: command
    language: shell
    content: 'canvasnet render anchors.scad --size 440x300 -o slot.png --json'
    description: Read the anchors of the model at the size of its image; add the image x and y to each anchors[].pixel.
  - name: read-positions
    kind: command
    language: shell
    content: 'canvasnet render flowchart.mmd --size 430x280 --positions node:send --positions node:stock -o slot.png --json'
    description: Read the positions of two nodes of the diagram at the size of its image; add the image x and y.
  - name: render-framed
    kind: command
    language: shell
    content: 'canvasnet render framed.svg --width 1200 -o framed.png --strict --json'
    description: Draw the framed figure 1200 pixels wide and fail if the model is not drawn.
---

## Command line

- `canvasnet info file --json` reports the natural size of a diagram, chart, or
  SVG file (`image.width`, `image.height`).
- Render the picture alone at the size of its image with `--json`: a model
  reports `anchors[].pixel` ([[#read-anchors]]), a diagram or chart
  `positions[].pixel` for what `--positions` selects
  ([[#read-positions]]).
- For a point no selector names, draw a labeled grid at the box size (see
  `canvasnet render --help`), read x,y off it, and confirm with `inspect pixel`:

```shell
canvasnet render flowchart.mmd --size 430x280 --grid 50 -o grid.png
canvasnet inspect pixel flowchart.mmd 380,125 --size 430x280 --radius 2
```

- Render with `--strict` and look at the PNG; to see a tip land, draw the figure
  at a larger `--width` with `--crop` around the feature.

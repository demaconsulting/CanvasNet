---
title: Feature positions (callout points)
summary: Where the nodes, edges, marks, and axes land in a picture.
aliases:
  - position
children:
  - selectors
  - mermaid
  - vega-lite
  - geometry
see-also:
  - svg/bindings
  - svg/framed-figure
  - scad/anchors
  - json/results
  - limits
examples:
  - name: read-positions
    kind: command
    language: shell
    content: 'canvasnet render flowchart.mmd --size 430x280 --positions node:send --positions edge -o slot.png --json'
    description: The Send node and every edge of the flowchart example at the size of its image, with their boxes and pixels.
  - name: read-marks
    kind: command
    language: shell
    content: 'canvasnet render bar.vl.json --positions mark:x=B --positions axis -o bar.png --json'
    description: The bar of stall B and both axes of the bar chart example.
---

A Mermaid diagram or a Vega-Lite chart reports where its features land: render
it with `--positions <selector> --json`, and `positions[]` gives the box and the
attach pixel of every feature the selector matches, in the written picture,
ready for callouts and leader lines drawn over it. A model reports anchors
instead (`canvasnet help scad anchors`). In an SVG figure, bind the callout to
the selector instead of copying numbers (`canvasnet help svg bindings`).

## Read a position

1. Render the picture alone at the size it will have; for a figure, the size of
   its `<image>` (`--example read-positions`).
2. Each item is {kind, id, ..., box: {x, y, width, height}, pixel: [x, y],
   inside}. For a figure, add the image `x` and `y` to the pixel (see
   `canvasnet help svg framed-figure`).
3. Start with the selector `all` to see the ids, then narrow it: `node:send`,
   `edge:@from=pack`, `mark:x=B` (see `canvasnet help positions selectors`).

## Rules

- `--positions` needs `--json` and a diagram or chart; repeat it to ask for
  more. Each item is listed once, in the order of the picture (structural
  items first), whatever the order of the selectors.
- A selector that matches nothing is the warning positions-unmatched; with
  `--strict` it is the error positions-unmatched and nothing is written.
- At most {{positionsMax}} items are returned; positionsTruncated is true when
  more matched. Narrow the selectors to see the rest.
- pixel is an attach point (the center of a box, the middle of an edge, a point
  of a line), not a promise that it is painted (see `canvasnet help positions
  geometry`).

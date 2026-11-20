---
title: Feature positions (callout points)
summary: Where the nodes, edges, marks, and axes land in a picture.
tags:
  - mermaid
  - vega-lite
  - composition
  - hosting
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
  - limits
---

A Mermaid diagram or a Vega-Lite chart reports where its features land: render
it with position selectors (`ViewerRenderOptions.Positions`, a
`PositionSelection`), and `ViewerImage.Positions` gives the box and the attach
pixel of every feature a selector matches, in the picture, ready for callouts
and leader lines drawn over it. A model reports anchors instead
([[scad/anchors]]). In an SVG figure, bind the callout to the
selector instead of copying numbers ([[svg/bindings]]).

## Read a position

1. Render the picture alone at the size it will have; for a figure, the size of
   its `<image>`.
2. Each item is an `ImagePosition`: its `Kind`, its `Id`, ..., its `Box` (x, y,
   width, height), and its `Pixel` (x, y). For a figure, add the image `x` and
   `y` to the pixel (see [[svg/framed-figure]]).
3. Start with the selector `all` to see the ids, then narrow it: `node:send`,
   `edge:@from=pack`, `mark:x=B` (see [[positions/selectors]]).

## Rules

- Only a diagram or a chart reports positions; give several selectors to ask for
  more. Each item is listed once, in the order of the picture (structural items
  first), whatever the order of the selectors.
- A selector that matches nothing is the warning positions-unmatched; in strict
  mode it is the error positions-unmatched and nothing is returned.
- At most {{positionsMax}} items are returned; `ViewerImage.PositionsTruncated`
  is true when more matched. Narrow the selectors to see the rest.
- The pixel is an attach point (the center of a box, the middle of an edge, a
  point of a line), not a promise that it is painted (see
  [[positions/geometry]]).

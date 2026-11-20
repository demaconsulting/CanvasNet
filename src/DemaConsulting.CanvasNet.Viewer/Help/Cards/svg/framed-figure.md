---
title: Framed figures with callouts
summary: A picture in a designed frame - title band, accents, caption - with callout arrows on its features.
tags:
  - svg
  - design
  - composition
aliases:
  - framed
  - callouts
see-also:
  - svg/bindings
  - composition
  - svg/composition
  - svg/style
  - svg/arrows
  - scad/anchors
  - positions
  - svg/supported
examples:
  - name: framed
    kind: snippet
    language: xml
    file: framed.svg
    description: 'A framed figure of anchors.scad (save it beside the figure) with a gradient title band, chevrons, an accent stripe, a caption, and three callout arrows that end on anchors of the model.'
  - name: framed-flow
    kind: snippet
    language: xml
    file: framed-flow.svg
    description: 'The same frame around flowchart.mmd; each arrow tip is bound with cn:x2y2 to a position of the diagram.'
---

A framed figure shows one picture - a model, a diagram, or a chart - in a
designed card, with labeled arrows that point at its features. Print a complete
one with [[svg/framed-figure#framed]], and save
[[scad/anchors#anchors]] beside it as `anchors.scad`.

## Layers of the frame

Draw the layers in this order; each covers the one before:

1. Container: a page `<rect>`, then a white card with `rx="20"` and a thin
   gray stroke.
2. Title band: a `<path>` with rounded top corners, filled with a horizontal
   `linearGradient` (`#1e3a8a` to `#2563eb`); white title, light lead line.
3. Decoration: an accent stripe under the band (`#f59e0b`) and chevrons, white
   `<polyline>` strokes with round caps at rising opacity.
4. Picture slot: a soft panel (a light vertical gradient) and the `<image>`
   inside it, with a margin.
5. Callout layer: for each callout an accent bar, a bold label, a muted detail
   line, and an arrow from the label to the feature.
6. Caption: italic and muted, under the slot; SVG text does not wrap, so break
   a long caption into several `<text>` lines.

## Size the picture

- Give the `<image>` the aspect ratio of the picture: `canvasnet info file
  --json` reports the natural size of a diagram, chart, or SVG file
  (`image.width`, `image.height`). A model has no natural aspect; it is fitted
  to the box it gets.
- A diagram, chart, or model is drawn into the box, scaled to fit with its own
  aspect ratio and centered; the `preserveAspectRatio` of the `<image>` does
  not move it, and an SVG file follows the `preserveAspectRatio` of its own
  root. A raster (PNG, JPEG, ...) is fitted by the `preserveAspectRatio` of the
  `<image>`: `xMidYMid meet` by default; any align, `slice`, and `none` work.

## Arrows

- Draw each arrow from the label to the feature with a `<marker>` on
  `marker-end`; `refX="10"` puts the tip on the last point, the feature:

```xml
<marker id="arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="5" markerHeight="5" orient="auto">
  <path d="M0,0 L10,5 L0,10 z" fill="#ea580c"/>
</marker>
<polyline points="748,191 712,191 500,278.2" fill="none" stroke="#ea580c" stroke-width="2.5" marker-end="url(#arrow)"/>
```

- A short level leg before the slanted one keeps the labels in tidy columns.
  Curves and start arrows: [[svg/arrows]].

## Find where a feature is

- Bind the arrow first: give the `<image>` an id and put
  `cn:x2y2="#id selector"` on the `<line>` that ends the arrow; the tip follows
  the picture at any size ([[#framed-flow]],
  [[svg/bindings]]), and a model binds its anchors (`anchor:tip`).
- Without a binding, render the picture alone at the size of its image with
  `--json` and add the image `x` and `y` to each pixel: a model reports
  `anchors[].pixel` (`--example read-anchors`; same `view=` and `def.` values),
  a diagram or chart `positions[].pixel` for what `--positions` selects
  (`--example read-positions`).
- For a point no selector names, draw a labeled grid at the box size (see
  `canvasnet render --help`), read x,y off it, and confirm with `inspect pixel`
  (the Send node of [[mermaid/flowchart#flowchart]]):

```shell
canvasnet render flowchart.mmd --size 430x280 --grid 50 -o grid.png
canvasnet inspect pixel flowchart.mmd 380,125 --size 430x280 --radius 2
```

- Measure an unbound tip again after a change to the source or the box; keep
  the box size in viewBox units so the picture and the callouts scale together.

## Check

- Render with `--strict` and look at the PNG; to see a tip land, draw the
  figure at a larger `--width` with `--crop` around the feature.

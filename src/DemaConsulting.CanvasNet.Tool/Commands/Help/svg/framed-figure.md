---
title: Framed figures with callouts
summary: A picture in a designed frame - title band, accents, caption - with callout arrows on its features.
aliases:
  - framed
  - callouts
see-also:
  - composition
  - svg/composition
  - svg/style
  - scad/anchors
  - svg/supported
examples:
  - name: framed
    kind: snippet
    language: xml
    file: framed.svg
    description: 'A framed figure of anchors.scad (save it beside the figure) with a gradient title band, chevrons, an accent stripe, a caption, and three callout arrows that end on anchors of the model.'
  - name: read-anchors
    kind: command
    language: shell
    content: 'canvasnet render anchors.scad --size 440x300 -o slot.png --json'
    description: Read the anchors of the model at the size of its image; add the image x and y to each anchors[].pixel.
  - name: render-framed
    kind: command
    language: shell
    content: 'canvasnet render framed.svg --width 1200 -o framed.png --strict --json'
    description: Draw the framed figure 1200 pixels wide and fail if the model is not drawn.
---

A framed figure shows one picture - a model, a diagram, or a chart - in a
designed card, with labeled arrows that point at its features. Print a complete
one with `canvasnet help svg framed-figure --example framed`, and save
`canvasnet help scad anchors --example anchors` beside it as `anchors.scad`.

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

- `<marker>` is drawn on a `line`, `polyline`, or `path` through `marker-end`
  (and `marker-start`, `marker-mid`), as an attribute or in CSS, with
  `orient="auto"` or `auto-start-reverse`, `refX`, `refY`, and `viewBox`:

```xml
<marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="5" markerHeight="5" orient="auto">
  <path d="M0,0 L10,5 L0,10 z" fill="#ea580c"/>
</marker>
<polyline points="748,191 712,191 500,279.7" fill="none" stroke="#ea580c" stroke-width="2.5" marker-end="url(#arrow)"/>
```

- The last point of the line is the feature; `refX` near the tip of the head
  puts the point there. The head scales with `stroke-width`.
- A short level leg before the slanted one keeps the labels in tidy columns.

## Find where a feature is

- A model: declare `echo("anchor", "name", [x, y, z]);`, render the model alone
  at the size of its image with `--json` (`--example read-anchors`), and add
  the image `x` and `y` to each `anchors[].pixel`. The embedded model is drawn
  with the same camera, so the arrow lands on the point at any output width.
  Give both the same `view=` and `def.` values (see
  `canvasnet help scad anchors`).
- A Mermaid diagram or a Vega-Lite chart reports no positions of its nodes,
  marks, or axes. Its layout is deterministic, and rendering it alone with
  `--size` equal to the box gives the picture the figure embeds: find the
  feature in that PNG, check it with `canvasnet inspect pixel slot.png 212,96`,
  and add the image `x` and `y`. Measure again after any change to the source
  or the box.
- Keep the box size in viewBox units: a figure drawn at any `--width` then
  scales the picture and the callouts together.

## Check

- Render with `--strict` and look at the PNG; to see a tip land, draw the
  figure at a larger `--width` with `--crop` around the feature.

---
title: Arrows on connectors
summary: Arrowheads that follow their connector - markers, direction, final tangent, and tip placement.
aliases:
  - markers
  - arrowheads
see-also:
  - svg/style
  - svg/framed-figure
  - svg/bindings
  - composition
  - svg/diagnostics
examples:
  - name: arrows
    kind: source
    language: xml
    file: arrows.svg
    description: Two S-curve connectors, one horizontal and one vertical, each with a marker whose tip lands on the edge of its target box.
  - name: render-arrows
    kind: command
    language: shell
    content: 'canvasnet render arrows.svg --width 1200 -o arrows.png --strict --json'
    description: Draw the figure 1200 pixels wide; look at every tip in the PNG.
---

An arrowhead is part of its connector: draw it with a `<marker>`, never as a
separate shape, so it turns and moves with the line. Print a complete figure
with `canvasnet help svg arrows --example arrows`.

## Use a marker

- Define the head once in `<defs>` and put `marker-end="url(#arrow)"` on the
  `line`, `polyline`, or `path` (or `marker-end` in a CSS class):

```xml
<marker id="arrow" viewBox="0 0 10 10" refX="10" refY="5" markerWidth="6" markerHeight="6" orient="auto">
  <path d="M0,0 L10,5 L0,10 z" fill="#ea580c"/>
</marker>
```

- `orient="auto"` turns the head along the path at its end. For a head at the
  start, use `orient="auto-start-reverse"` on `marker-start`: plain `auto`
  there points the head forward, into the line.
- Do not draw a head as a separate triangle `<polygon>` or `<path>`: it does
  not follow the line when the line changes. CanvasNet reports such a shape as
  the warning `svg-arrowhead-shape`.

## Direction

- Draw each connector from its source to its target: the end of the path is
  the end with the head.

## The final tangent

- The head follows the direction of the path at its very end, not the line
  from the start to the target. For a cubic `C` that is the direction from the
  last control point to the end; for a quadratic `Q`, from its control point;
  for an arc `A`, the tangent of the arc at its end. When the last control
  point equals the end, the direction comes from the previous distinct control
  point.
- To enter a box from the left, put the last control point left of the end at
  the same y; to enter from the top, above it at the same x.
- A horizontal S-curve from (x0,y0) to (x1,y1), with xm halfway:
  `M x0,y0 C xm,y0 xm,y1 x1,y1`. The vertical one, with ym halfway:
  `M x0,y0 C x0,ym x1,ym x1,y1`. Both leave and enter level.

## Where the tip lands

- With `viewBox="0 0 10 10"`, the head `M0,0 L10,5 L0,10 z`, and
  `refX="10" refY="5"`, the tip is the last point of the path: end the path on
  the edge of the target, not at its center.
- `refX="0"` anchors the base of the head at the end instead. For a thick
  stroke, whose flat end shows beside a sharp tip, end the path one head
  length short of the edge and use `refX="0"`.
- `markerUnits="strokeWidth"` (the default) scales the head with
  `stroke-width`: `markerWidth="6"` on a stroke of 2 is 12 units long.
  `markerUnits="userSpaceOnUse"` keeps it a fixed size in the drawing's user
  units.

## Check

- Render with `--strict` and look at the PNG; draw it at a larger `--width`
  with `--crop` around a tip to see it land.
- To land a tip on a node of an embedded diagram, chart, or model at any size,
  end the arrow with a `<line>` and bind its end with `cn:x2y2`
  (`canvasnet help svg bindings`).

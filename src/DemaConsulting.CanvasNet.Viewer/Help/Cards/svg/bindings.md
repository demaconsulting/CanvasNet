---
title: Position bindings (cn:)
summary: Bind an arrow tip, a dot, or a label to a feature of an embedded diagram, chart, or model.
tags:
  - svg
  - composition
aliases:
  - binding
see-also:
  - svg/framed-figure
  - positions
  - positions/selectors
  - scad/anchors
  - svg/flatten
  - errors/codes
---

A binding moves a point of a shape onto a feature of a picture that the same
SVG file embeds, so an arrow lands on a node at any output size. CanvasNet
resolves it from the very picture it draws; other SVG readers ignore it and
show the numbers written in the file.

```xml
<svg xmlns="http://www.w3.org/2000/svg" xmlns:cn="https://demaconsulting.com/canvasnet" viewBox="0 0 1000 568">
  <image id="flow" href="flowchart.mmd" x="285" y="162" width="430" height="280"/>
  <line x1="712" y1="221" x2="677.2" y2="286.9" cn:x2y2="#flow node:send"/>
</svg>
```

The complete figure is
[[svg/framed-figure#framed-flow]].

## Attributes

- namespace: `https://demaconsulting.com/canvasnet` with any prefix (`cn` by
  convention). It is a name only and is never fetched; the attributes are
  matched by the namespace, not by the prefix.
- cn:x1y1, cn:x2y2: the start or the end of a `<line>` (`x1`, `y1` or `x2`,
  `y2`).
- cn:center: the center of a `<circle>` or an `<ellipse>` (`cx`, `cy`).
- cn:xy: the first `x` and `y` of a `<text>`; `dx`, `dy`, `text-anchor`, and
  `rotate` still apply.
- value: `#imageId selector`: the id of an `<image>` of the same file, white
  space, and one `--positions` selector (see
  [[positions/selectors]]), which must match exactly one feature.
- features: a Mermaid diagram and a Vega-Lite chart report the features
  [[positions]] lists; an OpenSCAD model reports its anchors as
  the kind `anchor` (`anchor:tip`). Rasters, documents, and SVG files report
  none.

## How it is resolved

- The picture is drawn once. The pixel of the feature is placed as the picture
  is placed (x, y, width, height, the `preserveAspectRatio` fit of the drawn
  size, and the transforms of the image), then taken into the user space of the
  bound element through the inverse of its own transforms. Both numbers are
  written together, rounded to 3 decimals.
- A binding may come before its image. Keep the written numbers near the
  result: they are what other SVG readers show.

## Failures

A binding that fails keeps the written numbers and is a warning; with
`--strict` it is the error `svg-binding-failed` and nothing is written. A
picture that was not drawn is reported first, so a denied file
(`image-access-denied`) is still `file-access-denied` under `--strict`.

- `svg-binding-malformed`: The value is not `#imageId selector`, or the selector
  is not valid.
- `svg-binding-image-missing`: No `<image>` has the id.
- `svg-binding-unsupported`: The attribute is on the wrong element; the element
  or the image is inside `defs`, a pattern, marker, mask, clip path, or nested
  `<svg>`, or is drawn by a `<use>`; a `<text>` has a list of x or y; the image
  is a `data:` URI; or its file reports no features.
- `svg-binding-unmatched`: The selector matched no feature.
- `svg-binding-ambiguous`: The selector matched more than one feature; make it
  narrower.
- `svg-binding-unavailable`: The picture was not drawn (missing, denied, over a
  limit, or of no size), or the feature has no pixel.
- `svg-binding-singular`: A transform of the element or of the image cannot be
  inverted (`scale(0)`).

## Flatten

`canvasnet flatten` bakes each resolved binding into the copy with the numbers
of the PNG picture that replaces the link, removes its `cn:` attribute, and
removes the namespace declaration when nothing uses it any more; the JSON
result counts `bindings.resolved` and `bindings.unresolved`. A binding that
fails keeps its attribute and numbers. A nested SVG file is replaced as one
picture, with its own bindings drawn into it; one that is kept as a link is
not rewritten.

## Not yet

The points of a `<path>`, `<polyline>`, or `<polygon>`, `data-cn-*` attributes
for SVG inside HTML, and bindings inside the content of a `<use>`.

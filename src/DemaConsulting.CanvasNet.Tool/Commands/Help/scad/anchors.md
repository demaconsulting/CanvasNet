---
title: Anchors (callout points)
summary: Named points of a model and where they land in the picture, for callouts and labels.
see-also:
  - scad/views
  - json/results
examples:
  - name: anchors
    kind: source
    language: openscad
    file: anchors.scad
    description: A plate with a hole and three callout anchors, one of them inside translate and rotate.
  - name: render-anchors
    kind: command
    language: shell
    content: 'canvasnet render anchors.scad --view top --size 600x400 -o anchors.png --json'
    description: Draw anchors.scad from above and list its anchors with their pixel positions in the JSON result.
---

Declare a point with `echo("anchor", "name", [x, y, z]);` and render with
`--json`: `anchors[]` gives the pixel of every point in the written picture,
ready for leader lines and labels drawn over it.

## Anchors

- echo("anchor", "name", [x, y, z]); declares a named point of the model, for a
  callout or a leader line drawn on the picture later. The echo line is still
  printed. The point is in the coordinate system where the statement stands:
  every enclosing translate, rotate, scale, mirror, and multmatrix is applied,
  so the anchor is a world point. Only the statement form counts; an echo used
  as an expression, or inside a function, is a plain echo.
- A module, for, if, union, difference, intersection, render, color, hull, or
  minkowski does not move its children, so the point passes through unchanged.
  An anchor inside resize, linear_extrude, rotate_extrude, projection, or
  another module that cannot give its children a position is skipped with a
  scad-anchor-invalid warning.
- `scad-anchor-invalid`: Severity warning (code scad-warning, the identifier
  starts the message): a malformed declaration (a name that is not a non-empty
  string, a point that is not three finite numbers, a named or an extra
  argument) or an anchor in an unsupported context. The anchor is skipped; the
  echo line stays.
- The same name may be declared more than once; every declaration is kept, in
  echo order.
- With --json, render adds anchors[] for a model: {name, point: [x, y, z],
  pixel: [x, y], inside}. pixel is the position in the written picture in pixels
  from its top-left corner (continuous: pixel i spans [i, i+1), so the center of
  the first pixel is 0.5), projected with the camera that drew the picture,
  perspective included. inside is true when pixel lies within the picture.
  Without --json nothing is added.
- The pixel steps move the anchors with the picture: --crop, --trim, --resize,
  --fit, --pad, --flip, and --rotate are applied to pixel, and inside is tested
  against the final picture. After a --deskew that corrects the image, pixel is
  null; it is also null for a point behind a perspective camera (inside is then
  false).
- pixel says where a point projects, not whether it can be seen: an anchor
  hidden behind other geometry still has a pixel and inside true.

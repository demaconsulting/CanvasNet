---
title: Diagnostics of a model
summary: The scad-* identifiers, echo output, warnings, and the errors of a model.
tags:
  - scad
  - diagnostics
see-also:
  - errors
  - scad/language
---

## Diagnostics

- Without strict mode an unsupported construct is left out and reported as a
  diagnostic (`ViewerImage.Diagnostics`) with the source scad, the severity
  unsupported, and its code. In strict mode the first one fails with
  unsupported-feature and nothing is drawn.
- The output of echo() is kept as 'ECHO: <text>' lines (`ViewerImage.Echo`).
- `scad-module-offset`: Also scad-module-surface, scad-module-import, and
  scad-module-roof: the module and its children are omitted.
- `scad-projection-too-complex`: A projection needs more work than one
  projection may do (too many triangles, or a difference or an intersection
  whose silhouette costs too much to classify): the projection is omitted.
- `scad-projection-open-mesh`: A projection found a mesh that is not a closed
  solid (its cross-section does not close): the projection is omitted.
- `scad-hull-csg-operand`: A hull has a difference or an intersection among its
  children.
- `scad-hull-mixed-dimensions`: A hull has both 2D and 3D children.
- `scad-minkowski-nonconvex`: A minkowski has a child that is not convex.
- `scad-minkowski-2d`: A minkowski of 2D children.
- `scad-minkowski-mixed-dimensions`: A minkowski has both 2D and 3D children.
- `scad-modifier-highlight`: # on a 2D shape that is not extruded: drawn
  normally, without highlight.
- `scad-modifier-background`: % on a 2D shape that is not extruded: omitted.
- `scad-multmatrix-projective`: multmatrix with a bottom row other than [0, 0,
  0, 1]: omitted.
- `scad-resize-approximate`: resize over children that contain a difference or
  an intersection: estimated bounds are used.
- `scad-function-<name>`: An unimplemented built-in function, for example
  scad-function-textmetrics: the call evaluates to undef.
- `scad-warning`: Severity warning: a questionable argument (an invalid
  argument, for example) that still renders.
- `scad-error`: Error code: a syntax error, an evaluation error, or an include
  that cannot be read; the message names file, line, and column.
- `budget-exceeded`: Error code: the model needs more triangles than
  `MaxTriangles` allows.
- See the OpenSCAD section of the Scad user guide for the complete list of
  identifiers.

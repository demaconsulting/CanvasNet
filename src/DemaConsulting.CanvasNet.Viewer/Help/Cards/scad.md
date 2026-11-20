---
title: OpenSCAD models (.scad)
summary: OpenSCAD models (.scad) drawn as one still picture.
tags:
  - scad
  - authoring
aliases:
  - openscad
children:
  - language
  - views
  - anchors
  - style
  - composition
  - options
  - diagnostics
  - differences
see-also:
  - style
  - composition
  - svg
  - limits
---

A model is evaluated and drawn as one still picture from a named viewpoint. The
model is evaluated by an independent dialect of the OpenSCAD language and drawn
with a fitted camera, 800x800 pixels by default.

Start with [[scad/language]] for what the language supports, then
[[scad/views]] for the camera. Render it, then look at the picture.
A model can also be placed inside an SVG drawing with
`<image href="part.scad" .../>` (see [[svg]]).

For a good-looking picture read [[scad/style]]; to label a
model or place it in a figure, [[scad/composition]].

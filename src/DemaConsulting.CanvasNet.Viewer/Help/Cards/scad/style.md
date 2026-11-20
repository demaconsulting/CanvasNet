---
title: Styling model pictures
summary: Colors, translucent context, smooth curves, views, size, and background for good-looking models.
tags:
  - scad
  - design
see-also:
  - style
  - scad/composition
  - scad/views
  - scad/options
---

Color the parts that matter, draw everything else in one quiet color, and pick
the view that shows the feature.
[[scad#hull-ghost]] shows a colored part beside
a translucent ghost.

## Color

- `color("SteelBlue")`, `color("#2f6db3")`, or `color([0.2, 0.4, 0.7])` colors
  the children; an alpha below 1 (`color([0.5, 0.5, 0.5, 0.3])`) draws them
  translucent, which shows context parts without hiding the part in front.
- The default color (`ScadViewOptions.DefaultColor`) colors every surface the
  model does not color (gray by default): a soft color such as `"#9fb7d0"` reads
  better than gray in a document, and black makes a flat 2D drawing crisp.
- Use one accent color for the feature the text talks about, and the default
  color for the rest.

## Shape and view

- Round parts need enough facets in a close-up: set `$fn = 64` (or `$fa` and
  `$fs`) on curved shapes; a low value looks faceted.
- `iso` (the default) shows three faces and suits most parts; `front` and `top`
  suit a dimension drawing; an azimuth, an elevation, and a perspective
  projection give a free view (see [[scad/views]]).

## Size and background

- A size of 1200x800 draws a large, sharp picture; the camera fits the model and
  nothing is stretched (800x800 by default).
- A transparent background keeps the picture clear for a page, a slide, or an
  SVG figure (a model linked from an SVG drawing is always transparent); a dark
  background needs light model colors.
- Pictures of several variants share one scale only with a fixed camera
  ($vpd=300 in the definitions; see [[scad/composition]]).

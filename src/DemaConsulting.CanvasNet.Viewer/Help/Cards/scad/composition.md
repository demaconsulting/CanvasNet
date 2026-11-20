---
title: Models in figures
summary: Labels and leader lines from anchors, models in SVG figures and Mermaid nodes, and variants.
tags:
  - scad
  - composition
see-also:
  - composition
  - scad/anchors
  - scad/style
  - svg/composition
  - svg/framed-figure
  - mermaid/composition
---

A model rarely stands alone in a document: label its features, place it in a
figure, or show its variants at one scale.

## Labels and leader lines

1. Declare the points to label in the model:
   `echo("anchor", "hole-center", [15, 10, 5]);`
   ([[scad#anchors]] declares three).
2. Render the picture you will show (600x400 over a transparent background) and
   save it as `part.png`: each of its anchors (`ViewerImage.Anchors`) gives the
   pixel [x, y] of a point in `part.png`.
3. Place `part.png` in an SVG drawing at its pixel size and draw each leader
   line from the anchor pixel plus the offset of the placement:

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="800" height="440" viewBox="0 0 800 440">
  <image href="part.png" x="0" y="20" width="600" height="400"/>
  <circle cx="300" cy="190.2" r="5" fill="none" stroke="#c62828" stroke-width="2"/>
  <polyline points="300,190.2 640,110 660,110" fill="none" stroke="#c62828" stroke-width="2"/>
  <text x="668" y="116" font-family="sans-serif" font-size="18">Hole center</text>
</svg>
```

- The pixels belong to that picture, its size, and its view. Steps such as a
  crop or a trim move the anchors with the picture (see
  [[scad/anchors]]).
- Or link the model itself in an image of the same size and view: it is drawn
  with the same camera, so the same pixels plus the image `x` and `y` land on
  the points ([[svg/framed-figure#framed]]).

## A model in a figure

- `<image href="part.scad#view=front&amp;def.width=20" .../>` places a view of a
  variant in an SVG figure, over a transparent background (see
  [[svg/composition]]).
- `part@{ img: "part.scad", label: "Part", w: 120, h: 120 }` shows it in a
  Mermaid node (see [[mermaid/composition]]).

## Variants side by side

- Give each picture of a contact sheet (`MontageBuilder` of the Imaging
  namespace) its own fragment, and fix the camera with a definition of $vpd (in
  `Defines`, or def.$vpd=... in each fragment) so every cell has the same scale;
  without it each cell is fitted to its own geometry.

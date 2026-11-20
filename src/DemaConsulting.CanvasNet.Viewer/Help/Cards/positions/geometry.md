---
title: Boxes, pixels, and image steps
summary: What the box and the pixel mean, and how positions follow the size and the pixel steps.
tags:
  - mermaid
  - vega-lite
  - composition
see-also:
  - scad/anchors
  - svg/framed-figure
---

## Box and pixel

- Both are in pixels of the picture from its top-left corner, rounded
  to 3 decimals; pixel i spans [i, i+1).
- box is the bounds of the geometry of the feature, as SVG getBBox measures
  it: the stroke is not included, so a stroked outline reaches about half its
  stroke width beyond the box.
- pixel is the attach point of the kind, a fixed point for the tip of an arrow.
  It is not a promise that the feature is painted there or can be seen: a label
  or another feature may cover it.
- A pixel within the picture does not mean that the feature is visible.

## Size

- A scale factor multiplies every coordinate. A width, a height, or a box draws
  the picture into the box, scaled and centered, and the positions follow the
  same scale and offset.

## Pixel steps

- The crop, trim, resize, fit, pad, flip, and rotate operations move the
  positions with the pixels; the color operations and a grid change nothing.
- The steps are combined first, and the four corners of the original box are
  moved once: a rotated box is the smallest upright box around them.
- A crop moves a box but does not cut it: a feature the crop cuts off keeps
  its whole box, partly or wholly outside the picture.
- After a deskew that corrects the image, the box and the pixel are null.

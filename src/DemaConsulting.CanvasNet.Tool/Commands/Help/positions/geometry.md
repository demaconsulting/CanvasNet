---
title: Boxes, pixels, and image steps
summary: What box, pixel, and inside mean, and how positions follow the size and the pixel steps.
see-also:
  - scad/anchors
  - svg/framed-figure
---

## Box and pixel

- Both are in pixels of the written picture from its top-left corner, rounded
  to 3 decimals; pixel i spans [i, i+1).
- box is the bounds of the geometry of the feature, as SVG getBBox measures
  it: the stroke is not included, so a stroked outline reaches about half its
  stroke width beyond the box.
- pixel is the attach point of the kind, a fixed point for the tip of an arrow.
  It is not a promise that the feature is painted there or can be seen: a label
  or another feature may cover it.
- inside is true when pixel lies within the written picture. It does not mean
  that the feature is visible.

## Size

- `--scale` multiplies every coordinate. `--size`, `--width`, and `--height`
  draw the picture into the box, scaled and centered, and the positions follow
  the same scale and offset.

## Pixel steps

- `--crop`, `--trim`, `--resize`, `--fit`, `--pad`, `--flip`, and `--rotate`
  move the positions with the pixels; the color steps and `--grid` change
  nothing.
- The steps are combined first, and the four corners of the original box are
  moved once: a rotated box is the smallest upright box around them.
- A crop moves a box but does not cut it: a feature the crop cuts off keeps
  its whole box, partly or wholly outside the picture (inside is then false
  when its pixel is outside).
- After a `--deskew` that corrects the image, box and pixel are null and
  inside is false.

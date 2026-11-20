---
extends: positions/geometry
summary: What box, pixel, and inside mean, and how positions follow the size and the pixel steps.
---

## Command line

- inside: True when pixel lies within the written picture; it does not mean that
  the feature is visible. It is false when a crop leaves the pixel outside, and
  after a `--deskew` that corrects the image.
- `--scale` multiplies every coordinate; `--size`, `--width`, and `--height`
  draw the picture into the box, and the positions follow.
- `--crop`, `--trim`, `--resize`, `--fit`, `--pad`, `--flip`, and `--rotate`
  move the positions with the pixels; the color steps and `--grid` change
  nothing.

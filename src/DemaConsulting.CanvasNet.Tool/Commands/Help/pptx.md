---
extends: pptx
examples:
  - name: all-slides
    kind: command
    language: shell
    content: 'canvasnet render slides.pptx --all-pages -o ''out/slide-{page}.png'''
    description: Draw every slide into the folder out, one file per slide.
  - name: one-slide-strict
    kind: command
    language: shell
    content: 'canvasnet render slides.pptx --page 3 --dpi 150 --strict -o slide3.png --json'
    description: Draw slide 3 at 150 dpi and fail if a picture would be skipped.
---

## Command line

- `--page <N>`: The slide to draw, counted from 1 (default 1). The fragment of
  the input chooses the slide too and overrides --page: canvasnet render
  "deck.pptx#slide=3" -o s3.png.
- `--all-pages`: Draw every slide; for a deck of more than one slide the output
  must contain {page} or be a directory.
- `--dpi <D>`: Resolution in dots per inch (default 96, at most {{maxDpi}}).
- `--background <color>`: The color the slide is cleared to first (white by
  default).
- `--strict`: Fail with unsupported-feature when an EMF or WMF picture would be
  skipped; the other pptx-* identifiers are not in diagnostics[].

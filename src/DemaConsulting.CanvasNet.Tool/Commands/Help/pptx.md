---
extends: pptx
examples:
  - name: all-slides
    kind: command
    language: shell
    content: 'canvasnet render slides.pptx --all-pages -o out/slide-{page}.png'
    description: Draw every slide into the folder out, one file per slide.
  - name: one-slide-strict
    kind: command
    language: shell
    content: 'canvasnet render slides.pptx --page 3 --dpi 150 --strict -o slide3.png --json'
    description: Draw slide 3 at 150 dpi and fail if a picture would be skipped.
---

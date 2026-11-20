---
extends: scad/style
examples:
  - name: product-shot
    kind: command
    language: shell
    content: 'canvasnet render part.scad --size 1200x800 --default-color "#9fb7d0" --background transparent -o part.png'
    description: Draw a large picture in a soft blue-gray over a transparent background, ready to place on a page.
---

## Command line

- `--default-color` sets the default color (`--default-color black` for a crisp
  2D drawing); `--azimuth`, `--elevation`, and `--projection perspective` give a
  free view.
- `--size 1200x800` draws a large, sharp picture; `--background transparent`
  keeps it clear, and a dark `--background` needs light model colors.
- A fixed camera for variants: `-D '$vpd=300'`.

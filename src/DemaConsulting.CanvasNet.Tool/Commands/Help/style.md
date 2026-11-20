---
extends: style
examples:
  - name: check
    kind: command
    language: shell
    content: 'canvasnet render chart.vl.json --scale 2 -o chart.png --strict --json'
    description: Draw at twice the natural size, fail if anything is left out, and print the result to check.
  - name: colors
    kind: command
    language: shell
    content: 'canvasnet inspect colors chart.png --count 6'
    description: List the dominant colors of a rendered picture, to check its palette and background.
---

## Command line

- `--scale 2` doubles text and lines, and `--width` scales the picture to fit;
  `--height` and `--size` size it too. `canvasnet info file --json` reports the
  natural size (`image.width` and `image.height`).
- `--background` chooses the color under the picture (white by default);
  `--background transparent` keeps it clear. For a dark page use a dark
  `--background` with `--theme dark` for Mermaid.
- Render with `--strict --json`: `diagnostics[]` lists the warnings that remain.
  `canvasnet inspect colors out.png` lists the dominant colors of the PNG, and
  `canvasnet compare old.png new.png` measures what an edit changed.

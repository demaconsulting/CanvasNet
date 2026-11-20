---
extends: mermaid/style
examples:
  - name: theme
    kind: command
    language: shell
    content: 'canvasnet render flowchart.mmd --theme dark --scale 2 --background transparent -o flowchart-dark.png'
    description: Draw at twice the size in the dark theme over a transparent background.
---

## Command line

- Choose a theme with `--theme dark`; a theme the diagram sets itself wins over
  `--theme`.
- Draw with `--scale 2` (or `--width`) for a sharp figure, and look at the PNG.
- A diagram is drawn over white unless `--background` says otherwise;
  `--background transparent` keeps it clear.

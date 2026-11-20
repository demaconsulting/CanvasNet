---
extends: mermaid/options
summary: Theme, size, background, and --strict for a diagram, and what info reports.
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render flowchart.mmd -o flowchart.png --json'
    description: Draw flowchart.mmd and print the JSON result.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info flowchart.mmd --json'
    description: Report the type, direction, and counts without drawing.
---

## Command line

- `--theme`: The theme: default, base, dark, forest, or neutral. On any other
  kind of input --theme is a usage-error.
- `--scale`: Draws the diagram sharply at another size, as do --width and
  --height.
- `--background`: The color under the diagram: white by default, transparent
  keeps the transparency of Mermaid's own SVG.
- `--strict`: Strict mode: every unsupported construct, and a picture that is
  not drawn, becomes an unsupported-feature error.
- info reports what a probe reports, without drawing the diagram.

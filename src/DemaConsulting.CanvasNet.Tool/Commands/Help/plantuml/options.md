---
extends: plantuml/options
summary: Size, background, --strict, file access, and what info reports for a PlantUML diagram.
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render sequence.puml -o sequence.png --json'
    description: Draw sequence.puml and print the JSON result.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info sequence.puml --json'
    description: Report the type, title, and counts without drawing.
---

## Command line

- `--scale`: Draws the diagram sharply at another size, as do --width and
  --height.
- `--background`: The color under the diagram; transparent gives a transparent
  PNG.
- `--strict`: Strict mode: every unsupported construct, and a type that is not
  drawn, becomes an unsupported-feature error.
- `--file-access`: The mode the !include files are read under, with --allow-root
  (see [[files]]).
- `--theme`: Not for a PlantUML diagram: --theme and --positions are a
  usage-error on a .puml input.
- info reports what a probe reports (direction and accTitle are absent).

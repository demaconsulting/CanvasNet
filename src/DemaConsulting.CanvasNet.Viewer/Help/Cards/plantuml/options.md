---
title: Render options of a PlantUML diagram
summary: Size, background, --strict, file access, and what info reports for a PlantUML diagram.
see-also:
  - plantuml/diagnostics
  - limits
  - files
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

## Options

- `--scale`: The natural size is PlantUML's SVG size in pixels (times the
  diagram's own scale); --scale, --width, and --height draw the diagram sharply
  at another size (a diagram larger than {{maxDimension}} pixels on a side
  needs one of them).
- `--background`: The color under the diagram: the diagram's own background
  (white, or its skinparam backgroundColor) by default; transparent gives a
  transparent PNG.
- `--strict`: Every unsupported construct, and a type that is not drawn,
  becomes an unsupported-feature error.
- `--file-access`: The mode the !include files are read under, with
  --allow-root (see `canvasnet help files`).
- `--theme`: Not for a PlantUML diagram: --theme and --positions are a
  usage-error on a .puml input.

## Supported

- info reports the type (sequence, activity, or the name of a type that is not
  drawn, such as class or usecase), whether this version draws it, the title,
  and the numbers of nodes, edges, and subgraphs: the participants, messages,
  and boxes of a sequence diagram; the actions, start, stop, end, connector,
  and decision nodes, the arrows, and the swimlanes and partitions of an
  activity diagram. direction and accTitle are absent.

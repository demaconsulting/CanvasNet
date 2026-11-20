---
extends: svg/flatten
summary: 'canvasnet flatten: a copy of a drawing that browsers and other SVG readers can show.'
examples:
  - name: flatten
    kind: command
    language: shell
    content: 'canvasnet flatten dashboard.svg -o dashboard-flat.svg --scale 2 --json'
    description: Write a copy of a drawing whose linked diagrams, models, charts, and document pages are PNG pictures twice their placement size, for an SVG reader that cannot draw them.
---

## Command line

- command: Run `canvasnet flatten page.svg -o page.flat.svg` to write the copy;
  `--scale` multiplies the size of every picture.
- JSON: The JSON document counts bindings.resolved and bindings.unresolved, and
  `flattened` is false when nothing was flattened.
- exit code: When there is nothing to flatten, nothing is written and the exit
  code is 0; the text says nothing to flatten.
- `--strict`: Strict mode, as above.

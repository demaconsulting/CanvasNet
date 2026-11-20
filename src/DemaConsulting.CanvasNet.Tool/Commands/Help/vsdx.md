---
extends: vsdx
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render diagram.vsdx --dpi 150 -o diagram.png --json'
    description: Draw the first page at 150 dpi and print the JSON result.
  - name: info-pages
    kind: command
    language: shell
    content: 'canvasnet info diagram.vsdx --all-pages --json'
    description: List every page with its name and size as JSON.
---

## Command line

- `--page <N>`: The page to draw, counted from 1 (default 1). The fragment of
  the input chooses the page too and overrides --page: canvasnet render
  "drawing.vsdx#page=2" -o p2.png. 'canvasnet info' lists the page names.
- `--all-pages`: Draw every page; for a drawing of more than one page the output
  must contain {page} or be a directory.
- `--dpi <D>`: Resolution in dots per inch (default 96, a whole number at most
  {{maxDpi}}).
- `--background <color>`: The color transparency is flattened over (white by
  default).
- `--strict`: Fail with unsupported-feature instead of drawing with an
  approximation.
- Each report is listed in diagnostics[] as {source: vsdx, severity:
  unsupported, code: <token>} (without --json, also as an 'Unsupported: <token>:
  <message>' line on standard error).

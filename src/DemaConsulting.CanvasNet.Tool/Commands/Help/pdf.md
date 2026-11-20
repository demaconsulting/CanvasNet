---
extends: pdf
examples:
  - name: info-pages
    kind: command
    language: shell
    content: 'canvasnet info report.pdf --all-pages --json'
    description: List every page with its size and rotation as JSON.
  - name: render-page
    kind: command
    language: shell
    content: 'canvasnet render report.pdf --page 2 --dpi 150 -o page2.png --json'
    description: Draw page 2 at 150 dpi and print the JSON result.
  - name: overview
    kind: command
    language: shell
    content: 'canvasnet montage report.pdf --all-pages --labels -o overview.png'
    description: Compose every page into one labeled contact sheet.
---

## Command line

- `--page <N>`: The page to draw, counted from 1 (default 1); a page beyond the
  last is page-out-of-range. The fragment of the input chooses the page too and
  overrides --page: canvasnet render "report.pdf#page=3" -o p3.png.
- `--all-pages`: Draw every page; for a document of more than one page the
  output must contain {page} or be a directory.
- `--dpi <D>`: Resolution in dots per inch (default 96, at most {{maxDpi}}).
- `--scale <factor>`: Multiply the natural size by a factor; --width, --height,
  and --size also size the page.
- `--background <color>`: The color the page is flattened over (white by
  default).
- `--strict`: Accepted, but it changes nothing: the diagnostics[] list of a PDF
  is always empty.
- The tool has no option to supply a password or a font folder.

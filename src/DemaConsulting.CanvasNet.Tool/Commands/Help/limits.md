---
extends: limits
examples:
  - name: formats
    kind: command
    language: shell
    content: 'canvasnet formats --json'
    description: Print the limits of the installed tool as JSON.
  - name: too-large
    kind: command
    language: shell
    content: 'canvasnet render big.svg --width 9000 -o big.png'
    description: A width above the limit fails with size-limit before any pixels are allocated.
---

## Command line

- `--dpi`: Above 0 and at most {{maxDpi}}; 'canvasnet formats' reports the image
  side as maxDimension.
- `--quality`: JPEG quality {{qualityMin}} to {{qualityMax}}, default
  {{qualityDefault}}.
- `--max-triangles`: Default {{scadMaxTriangles}} triangles.
- `--size`: A model is 800x800 pixels unless --size, --width, --height, or
  --scale is given; --scale, --width, or --height makes a diagram smaller.
- `--positions`: render --positions returns at most {{positionsMax}} items
  (positionsTruncated is then true).
- `--strict`: An unusable link fragment is an unsupported-feature error.
  canvasnet flatten uses the pixel budget of render, and every limit holds in
  every --file-access mode.

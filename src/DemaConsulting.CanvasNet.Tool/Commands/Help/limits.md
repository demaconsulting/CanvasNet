---
title: Size and resource limits
summary: Size and resource limits and the errors they produce.
see-also:
  - errors
  - files
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

The largest sizes and budgets the tool accepts, and the error each one produces.

## Images and output

- **Image side**: At most {{maxDimension}} pixels on a side for any image the
  tool reads, draws, or writes; a larger size is refused with size-limit before
  any pixels are allocated. 'canvasnet formats' reports it as maxDimension.
- `--dpi`: Above 0 and at most {{maxDpi}} (PDF, PPTX, VSDX; a VSDX needs a whole
  number). The default is 96. A page is its size in points times the dpi divided
  by 72.
- `--quality`: JPEG quality {{qualityMin}} to {{qualityMax}}, default
  {{qualityDefault}}.
- **Memory**: An image {{maxDimension}} by {{maxDimension}} pixels uses about
  256 MB per copy, so large pipelines need memory.

## OpenSCAD models

- `--max-triangles`: Default {{scadMaxTriangles}} triangles; a model that needs
  more fails with budget-exceeded. A model source longer than the library reads
  is corrupt-input.
- **Default size**: 800x800 pixels unless --size, --width, --height, or --scale
  is given.

## Vega-Lite charts

- **Specification**: At most {{vlMaxSourceLength}} characters (corrupt-input)
  and JSON nesting depth {{vlMaxJsonDepth}} (vega-lite-error).
- **Data files**: At most {{vlMaxDataFiles}} files, {{vlMaxDataBytes}} bytes per
  file, and {{vlMaxDataRows}} rows in total; beyond that the chart fails with
  budget-exceeded.
- **Pictures**: At most {{vlMaxImages}} different pictures (image marks); beyond
  that the chart fails with budget-exceeded.
- **Painting**: At most {{vlMaxPaintedPixels}} device pixels painted in one
  render (fills, strokes, text, and pictures that cover the image count by the
  area they cover, so marks with huge sizes or stroke widths, or pictures
  without a size, are stopped); beyond that the chart fails with
  budget-exceeded, located at the mark.
- **Size**: A chart that lays out larger than {{maxDimension}} pixels on a side
  is size-limit.

## Mermaid diagrams

- **Text**: At most {{mmdMaxTextSize}} characters (Mermaid's maxTextSize); a
  longer text is corrupt-input.
- **Graph**: At most {{mmdMaxNodes}} nodes and {{mmdMaxEdges}} edges (Mermaid's
  maxEdges); beyond that the diagram fails with budget-exceeded
  (mermaid-limit-exceeded). The text cannot raise a limit: a directive that
  tries is ignored with the warning mermaid-config-secure-key.
- **Pictures**: At most {{mmdMaxImages}} picture loads (calls of the image
  resolver): a picture of an @{ img } node or an `<img>` tag in a label counts
  when it is measured and again when it is drawn; beyond that the diagram fails
  with budget-exceeded.
- **Size**: A diagram that lays out larger than {{maxDimension}} pixels on a
  side is size-limit unless --scale, --width, or --height makes it smaller.

## SVG and documents

- **SVG**: At most 5,000,000 characters, nesting depth 100, and 100,000 rendered
  elements; beyond that the file is corrupt-input.
- **PPTX**: Opening a presentation buffers the whole package in memory, up to
  256 MiB.
- **SVG flatten**: canvasnet flatten draws at most 64 pictures (nested ones
  included) within the pixel budget of render, and writes a copy of at most 64
  MiB; a larger copy is size-limit and nothing is written.
- **Default resolution**: A document page is drawn at 96 dpi.
- **Link fragment**: The fragment of an image href in an SVG file, or of an
  input path on the command line (the part after #, as in report.pdf#page=2), is
  at most {{fragmentMaxLength}} characters and {{fragmentMaxParameters}}
  parameters; beyond that the image is not drawn and image-fragment-invalid is
  reported (an unsupported-feature error with --strict; for an input path, an
  error).
- **Positions**: render --positions returns at most {{positionsMax}} items
  (positionsTruncated is then true) and takes at most {{positionsMaxSelectors}}
  selectors of at most {{positionsMaxSelectorLength}} characters and
  {{positionsMaxPredicates}} predicates each.
- **File access**: Every limit here holds in every file-access mode
  (--file-access): a mode decides which files may be read, never how much. See
  'canvasnet help files'.

---
title: Size and resource limits
summary: Size and resource limits and the errors they produce.
tags:
  - limits
see-also:
  - errors
  - files
---

The largest sizes and budgets CanvasNet accepts, and the error each one
produces.

## Images and output

- **Image side**: At most {{maxDimension}} pixels on a side
  (`Surface.MaxDimension`) for any image CanvasNet reads, draws, or writes; a
  larger size is refused with size-limit before any pixels are allocated.
- `Dpi`: Above 0 and at most {{maxDpi}} (`SourceLoader.MaxDpi`; PDF, PPTX, VSDX;
  a VSDX needs a whole number). The default is 96. A page is its size in points
  times the dpi divided by 72.
- `Quality`: JPEG quality {{qualityMin}} to {{qualityMax}}, default
  {{qualityDefault}} (`WriteOptions.Quality`).
- **Memory**: An image {{maxDimension}} by {{maxDimension}} pixels uses about
  256 MB per copy, so large pipelines need memory.

## OpenSCAD models

- `MaxTriangles`: Default {{scadMaxTriangles}} triangles
  (`ScadViewOptions.MaxTriangles`); a model that needs more fails with
  budget-exceeded. A model source longer than the library reads is
  corrupt-input.
- **Default size**: 800x800 pixels unless a width, a height, or a scale factor
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
  side is size-limit unless a scale factor, a width, or a height makes it
  smaller.

## PlantUML diagrams

- **Text**: At most {{pumlMaxTextSize}} characters of statements after the
  preprocessor (comments do not count); a longer text is corrupt-input.
- **Graph**: At most {{pumlMaxNodes}} nodes (participants or activity nodes)
  and {{pumlMaxEdges}} edges (messages or arrows); beyond that the diagram fails
  with budget-exceeded (plantuml-limit-exceeded).
- **Includes**: At most {{pumlMaxIncludes}} !include directives followed and
  {{pumlMaxIncludeDepth}} levels of nesting; beyond that the diagram fails with
  budget-exceeded. A URL is never followed.
- **Size**: A diagram that lays out larger than {{maxDimension}} pixels on a
  side is size-limit unless a scale factor, a width, or a height makes it
  smaller.

## MathML equations

- **Text**: At most {{mmlMaxTextSize}} characters; a longer text is
  corrupt-input.
- **Structure**: At most {{mmlMaxElements}} elements nested at most
  {{mmlMaxNestingDepth}} deep, and at most {{mmlMaxTableCells}} cells in one
  table; beyond that the equation fails with budget-exceeded
  (mathml-limit-exceeded).
- **Tokens**: At most {{mmlMaxTokenLength}} characters in the text of one token
  element (mi, mn, mo, mtext, ms); a longer token fails with budget-exceeded
  (mathml-limit-exceeded).
- **Layout work**: At most {{mmlMaxLayoutWork}} units of layout work (each
  element and character, again for a stretched operator); beyond that the
  equation fails with budget-exceeded.
- **Painted glyphs**: At most {{mmlMaxPaintedGlyphs}} glyphs painted by one
  render; beyond that the equation fails with budget-exceeded before anything is
  painted.
- **Size**: An equation larger than {{maxDimension}} pixels on a side at its
  natural size fails with budget-exceeded; a target size above it is
  size-limit.

## Setting the chart, diagram, and equation limits

- **Hosts**: A program sets the Vega-Lite, Mermaid, PlantUML, and MathML limits
  for one render with `ViewerRenderOptions.VegaLiteLimits`, `MermaidLimits`,
  `PlantUmlLimits`, and `MathMlLimits`, lower or higher than the defaults, for
  the file and every file it embeds. Raise a limit only for content you trust.

## SVG and documents

- **SVG**: At most 5,000,000 characters, nesting depth 100, and 100,000 rendered
  elements; beyond that the file is corrupt-input.
- **PPTX**: Opening a presentation buffers the whole package in memory, up to
  256 MiB.
- **SVG flatten**: Flattening (`SvgFlattener`) draws at most 64 pictures (nested
  ones included) within the pixel budget of a render, and writes a copy of at
  most 64 MiB; a larger copy is size-limit and nothing is written.
- **Default resolution**: A document page is drawn at 96 dpi.
- **Link fragment**: The fragment of an image href in an SVG file, or of an
  input path (the part after #, as in report.pdf#page=2), is at most
  {{fragmentMaxLength}} characters and {{fragmentMaxParameters}} parameters;
  beyond that the image is not drawn and image-fragment-invalid is reported (an
  unsupported-feature error in strict mode; for an input path, an error).
- **Positions**: A render returns at most {{positionsMax}} items
  (`ViewerImage.PositionsTruncated` is then true) and takes at most
  {{positionsMaxSelectors}} selectors of at most {{positionsMaxSelectorLength}}
  characters and {{positionsMaxPredicates}} predicates each.
- **File access**: Every limit here holds in every file-access mode: a mode
  decides which files may be read, never how much. See [[files]].

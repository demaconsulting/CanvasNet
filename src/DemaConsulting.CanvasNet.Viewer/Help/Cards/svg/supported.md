---
title: Supported SVG
summary: The SVG elements, attributes, and CSS that are drawn, and what is not.
tags:
  - svg
  - authoring
see-also:
  - svg/style
  - svg/units
  - svg/embedding
examples:
  - name: shapes
    kind: source
    language: xml
    file: shapes.svg
    description: A rectangle, a circle outline, and a triangle path.
  - name: group
    kind: source
    language: xml
    file: group.svg
    description: A clip path, use of a shared star, and transforms.
---

## Supported

- Shapes: rect, circle, ellipse, line, polyline, polygon, path.
- Grouping with g and a, cascading presentation attributes; transform; use and
  symbol; marker; switch draws its first child without foreignObject.
- Linear and radial gradients (with href inheritance), pattern, clipPath, and
  mask. A paint may name a fallback color (url(#g) red). A gradient or pattern
  in objectBoundingBox units is ignored on a shape with no width or no height,
  such as a horizontal line, as in browsers: the fallback color is drawn, or
  nothing. A reference to a missing id, to an element that is not a gradient or
  pattern, or to another file draws the fallback color too, or nothing.
- A filter, mask, or clip path in objectBoundingBox units uses the shape's fill
  box, without stroke or markers, as in browsers; on a shape with no width or no
  height (a stroked horizontal line) it draws nothing.
- Filters: the fe* primitives blend, color matrix, component transfer,
  composite, convolve matrix, lighting, displacement map, drop shadow, flood,
  Gaussian blur, image, merge, morphology, offset, tile, and turbulence.
- image elements with a base64 data: URI in a supported raster format (an SVG
  given as a data: URI is not drawn); image-rendering selects smooth or
  pixelated drawing.
- image elements with an href to a local file, relative to the folder of the SVG
  file: a raster, an SVG file, an OpenSCAD model (.scad), a Vega-Lite chart
  (.vl.json), a Mermaid diagram (.mmd), a PlantUML diagram (.puml), or a page of
  a PDF, PPTX, or VSDX document (page 1 unless the href ends in #page=N). The
  format is decided by the file name. A model, chart, or diagram is drawn at the
  size of the image, a raster at its own size and then fitted; under a skew or a
  non-uniform scale the picture is laid out in the image and then transformed
  with it, as in browsers; a local file: URI and a percent-encoded path also
  work, and a nested SVG file finds its own images beside itself.
- An embedded model is drawn with the view the library chooses for it (isometric
  for a 3D model, top-down for a flat 2D model, or the model's own $vpr, $vpt,
  $vpd, and $vpf variables) over a transparent background, so the diagram shows
  through; an embedded chart keeps its own background (white unless it names
  another).
- text, best effort, with weight and style: an installed family is used; the
  generic families and unknown families use the bundled Liberation Sans, Serif,
  and Mono.
- tspan and a in text, as in browsers: a style per run, x, y, dx, and dy lists,
  a new chunk at an absolute x or y, text-anchor per chunk, baseline-shift,
  letter-spacing and word-spacing, and white space collapsed
  (xml:space="preserve" keeps it).
- The root viewBox and preserveAspectRatio (all aligns, meet and slice), and
  percent lengths.
- CSS in a style element (type, class, id, universal, and compound selectors;
  descendant and child combinators; specificity) and inline style.
- The intrinsic size is the viewBox, else width and height (converted from their
  units to user units), else 300x150.

## Unsupported

- Not drawn: animation (SMIL) and unknown filter primitives, silently;
  foreignObject, nested svg, and textPath, as svg-text-not-drawn if they hold
  text. The text features rotate, textLength, vertical or rtl writing, and
  decoration are ignored (svg-text-unsupported).
- A filter over its work budget is skipped: the element is drawn unfiltered.
- Scripting, and image references CanvasNet never follows: a network URL, a UNC
  path, and a file: URI with a host. Each is reported as svg-image-unresolved
  and not drawn.
- A feImage filter primitive reads only a data: URI, never a file.
- An embedded file is not shown with the options of the outer render: a model
  uses the view the library chooses for it (its own $vp* variables are honored,
  or the view= of its href), a document page is the one its href names (page 1
  by default), and the page, the resolution, the model variables, and the chart
  parameters of the render options apply to the SVG file only (an embedded model
  or chart takes its values from the def. and param. parameters of its href, and
  an embedded Mermaid diagram its theme from theme=).
- At most 64 files are embedded in one render, SVG files nest 4 levels deep, an
  SVG file that embeds itself stops at once, and the pictures of one render may
  use at most 64 million pixels in all.
- A malformed document, or one over a limit (5,000,000 characters, nesting depth
  100, 100,000 rendered elements), is rejected as corrupt-input.

## Known differences

- No scripting, animation, or network resources; images are read relative to the
  folder of the SVG file (any local file, as in Pandoc, unless the file-access
  policy says otherwise), and only through an image resolver:
  `CanvasNetViewer.Render` uses one, and `SvgCodec.Load` opts in with
  SvgLoadOptions.ImageResolver.
- Text is best effort without shaping. An installed family is used, so text can
  differ between machines; generic and unknown families use the bundled
  Liberation fonts. A character those lack (such as U+221D) is taken from the
  bundled Noto fonts; a character nothing covers draws the font's missing-glyph
  box and is reported as svg-text-unsupported.
- The drawing is rendered sharply at the target size, not rasterized first and
  scaled afterwards.

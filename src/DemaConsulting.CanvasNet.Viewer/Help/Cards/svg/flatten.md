---
title: Flattening for other SVG readers
summary: Flattening - a copy of a drawing that browsers and other SVG readers can show.
tags:
  - svg
  - composition
  - hosting
see-also:
  - svg/embedding
  - svg/bindings
  - svg/diagnostics
---

A browser or WeasyPrint cannot draw a linked `.mmd`, `.puml`, `.scad`,
`.vl.json`, or document page. Flattening (`SvgFlattener.Execute` of the Imaging
namespace) writes a copy of the drawing that replaces each such link with a PNG
drawn at its placement.

## Reference

- replaced: An `image` whose link names a file only CanvasNet draws: a Mermaid
  or PlantUML diagram, an OpenSCAD model, a Vega-Lite chart, or a page of a PDF,
  PPTX, or VSDX document, with or without a fragment (`#page=2`,
  `#def.width=20`, `#param.t=5`, `#theme=dark`), and an SVG file that links such
  a file directly or through further SVG files. Its `href` (or its `xlink:href`
  when it has no `href`) becomes a `data:image/png;base64` URI of the picture.
- kept: Everything else, byte for byte: comments, formatting, line endings, a
  byte order mark, raster pictures, SVG files that link only rasters and SVG
  files, `data:` URIs (never followed), same-document `#` links, and network
  links.
- size: Each picture is drawn at the size of its placement in the drawing at its
  natural size (through the `viewBox` and every transform, as a render draws
  it), times the scale factor (`FlattenRequest.Scale`, 1 by default); the SVG
  reader still fits it with `preserveAspectRatio`. A link used twice is drawn
  once, at the larger placement.
- other folder: When the copy is written to another folder, the relative links
  that are left are rewritten so they find their files from there; a drive
  letter or UNC path (`C:\x.png`, `\\server\share\x.png`) is left as written on
  every system. Links of other elements (an external `use`, a CSS `url()`) are
  not rewritten.
- bindings: A position binding (cn:x2y2 and the others, see
  [[svg/bindings]]) is baked with the numbers of the PNG picture
  that replaces the link; its `cn:` attribute is removed, and the namespace
  declaration too when nothing uses it. The result counts them
  (`FlattenResult.ResolvedBindings` and `UnresolvedBindings`); a binding that
  fails keeps its attribute and numbers.
- nothing to flatten: When the drawing links no such picture, or shows none,
  nothing is written and the result replaces no picture; it is not a failure.
- limits: The links are drawn by the resolver of a render, with its link rule,
  file-access mode, and limits (at most 64 pictures, nested ones included, 4
  levels, and the pixel budget). The copy is at most 64 MiB (`size-limit`), and
  the drawing must be UTF-8 (`unsupported-feature` for another declared encoding
  unless the file is plain ASCII).
- strict mode: With `FlattenRequest.Strict`, a link that is not drawn
  (`svg-image-unresolved`, `svg-image-too-large`, `image-fragment-invalid`, or
  `image-page-out-of-range`) is an `unsupported-feature` error, as for render,
  and so is text that a linked SVG file does not draw or draws without a feature
  (`svg-text-not-drawn`, `svg-text-unsupported`) and a filter it draws without
  a feature (`svg-filter-unsupported`); a link the file-access mode
  denies (`image-access-denied`) is the error `file-access-denied`; a position
  binding that fails is the error `svg-binding-failed`;
  `svg-flatten-image-not-drawn` stays a warning.

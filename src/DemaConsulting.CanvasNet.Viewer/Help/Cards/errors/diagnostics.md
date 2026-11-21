---
title: Diagnostics are not errors
summary: Notes about a picture that was still written, and how they are reported.
tags:
  - diagnostics
see-also:
  - errors
---

- A diagnostic is a note of a successful render (`ViewerImage.Diagnostics`, each
  with its `Severity`, `Code`, `Message`, and `Detail`): the picture was drawn
  without a feature (severity unsupported) or with a questionable construct
  (severity warning). Read the diagnostics even when the render succeeded.
- A SCAD model's echo output is kept as the model printed it ('ECHO: <text>',
  `ViewerImage.Echo`).
- Only SCAD models, Vega-Lite charts, Mermaid and PlantUML diagrams, PPTX
  documents (skipped EMF and WMF pictures), VSDX documents, and SVG files (an
  image that was not drawn, text that is not drawn or lacks a feature,
  svg-text-not-drawn and svg-text-unsupported, a filter that lacks a feature,
  svg-filter-unsupported, a hand-drawn arrowhead, and the
  notes of the files it embeds) report diagnostics. PDF and raster images do
  not.
- The warning codes are scad-warning for a model, vega-lite-warning for a chart,
  mermaid-warning for a Mermaid diagram (the mermaid-* code is in detail),
  plantuml-warning for a PlantUML diagram (the plantuml-* code is in detail),
  svg-image-unresolved for an SVG image that was not drawn, svg-image-too-large
  for an SVG image whose picture is over the pixel budget,
  image-access-denied for an image the file-access policy denies, and
  svg-arrowhead-shape for an arrowhead drawn by hand instead of with a marker
  (it never fails a render, not even in strict mode).
- The fragment of an input path (part.scad#def.width=20) adds three warning
  codes, the same as for an SVG link: image-fragment-unknown-parameter,
  image-fragment-duplicate-parameter, and vl-param-unknown (a chart). They never
  fail a render, not even in strict mode. A fragment value that cannot be used
  is the error image-fragment-invalid, and a page the file does not have is
  page-out-of-range.

---
title: Diagnostics of a drawing
summary: Images and text that were not drawn, the fragment codes, and the errors of a drawing.
tags:
  - svg
  - diagnostics
see-also:
  - svg/embedding
  - svg/bindings
  - svg/arrows
  - errors
---

## Diagnostics

- `svg-image-unresolved`: Warning: an image href was not drawn. The message
  names the href and why: not found, a network URL, a format the tool cannot
  draw, a cycle, or a limit. With --strict it is an unsupported-feature error.
- `svg-image-too-large`: Warning: an image was not drawn because its picture
  does not fit in the pixel budget of the render. The message names the href and
  the pixels it needs. With --strict it is an unsupported-feature error.
- `image-access-denied`: Warning: an image href names a local file that the
  file-access mode (--file-access, --allow-root) denies, so it was not read. The
  message names the href, the mode, and why. --strict makes it the error
  file-access-denied.
- `svg-binding-*`: Warning: a position binding (cn:x2y2 and the others) was not
  resolved and keeps the numbers written in the file; the code says why (see
  [[svg/bindings]]). --strict makes it the error
  svg-binding-failed.
- `svg-arrowhead-shape`: Warning: a filled triangle (or notched arrow shape)
  is drawn by hand on the end of a line or path that has no marker there, so it
  does not follow the connector. One per drawing; the message counts them and
  names the first. Draw it with a marker (see [[svg/arrows]]).
  --strict does not fail on it.
- `svg-text-not-drawn`: Unsupported: text that is not drawn: text on a
  textPath, an unknown element inside a text, the HTML of a foreignObject, a
  text inside a nested svg or an unknown element, a negative font-size, or text
  beyond the supported coordinates. One per drawing; the message lists the
  reasons, counts the elements, and names the first. With --strict it is an
  unsupported-feature error.
- `svg-text-unsupported`: Unsupported: text drawn without a feature it uses:
  rotate, textLength, vertical writing-mode, direction rtl, dominant-baseline,
  alignment-baseline, text-decoration, an unsupported baseline-shift, or a
  character no font has (drawn as a box). One per drawing, like
  svg-text-not-drawn. With --strict it is an unsupported-feature error.
- `image-fragment-unknown-parameter`: Warning: the fragment of an href names a
  parameter the linked file does not use (a typo such as pag=2, or any parameter
  on a raster or an SVG file). The picture is drawn without it. --strict does
  not fail on it.
- `image-fragment-duplicate-parameter`: Warning: the fragment gives a parameter
  twice with different values; the last one is used. --strict does not fail on
  it.
- `vl-param-unknown`: Warning: a param.<name> of the fragment names a param the
  chart does not declare (a typo, or a chart without params). The chart is drawn
  without it. --strict does not fail on it.
- `image-fragment-invalid`: Unsupported: a value that is not allowed (page=0,
  page=abc, page=2.5, an empty value, an unknown view name, a def. value that is
  not valid JSON when it starts with [ or a name that is not an identifier, a
  param. value that is not valid JSON) or a fragment over its limit. The image
  is not drawn; with --strict it is an unsupported-feature error. For an input
  path of the command line the same mistake is an error with this code.
- `image-page-out-of-range`: Unsupported: the page or slide does not exist
  (page=9 of a 2-page document). The image is not drawn and the message tells
  how many pages there are; with --strict it is an unsupported-feature error.
- `svg-flatten-image-not-drawn`: Warning of canvasnet flatten: an image links a
  file only CanvasNet draws, but the drawing, or an SVG file it shows, never
  shows it (hidden, an unused definition, outside the canvas, or over a limit of
  the drawing), so it was left as a link. The location gives its file, line and
  column; for a link of a nested SVG file the origin lists the files that
  contain it. --strict does not fail on it.
- With --json each of these reports carries the embedding file in origin (see
  'canvasnet help json'); the message starts with the href as written.
- The notes of an embedded model, chart, diagram, or SVG file are reported too,
  each starting with the href of the file, and the lines an embedded model
  prints with echo are kept.
- An embedded file that is found but is broken (a corrupt picture, a model with
  a syntax error, a chart with a bad specification, a diagram with a syntax
  error) is an error with its own code, and the message names both files.
- `corrupt-input`: Error code: malformed XML (the message names the line and
  column), a root element that is not svg, or a document over a limit.

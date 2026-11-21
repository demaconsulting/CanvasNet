---
title: Errors, diagnostics, and strict mode
summary: Error codes, diagnostics, and strict mode.
tags:
  - diagnostics
  - hosting
children:
  - codes
  - diagnostics
see-also:
  - files
---

How a failure is reported, the stable error codes, and what strict mode changes.

## How failures are reported

- A failure is an exception. A `ViewerException` carries the stable error code
  (`Code`, one of the `ErrorCodes` constants), the message, and, when there is
  advice, a `Hint`.
- An option out of range is an `ArgumentOutOfRangeException` before the file is
  read, and a file that is found but cannot be drawn is an
  `ImageSourceException` with the original failure as its inner exception.
- A successful render returns its notes in `ViewerImage.Diagnostics` (see
  [[errors/diagnostics]]); read them even when nothing failed.

## What strict mode does

Strict mode is `ViewerRenderOptions.ThrowOnUnsupportedFeature`
(`SourceOptions.Strict` for the operations of the Imaging namespace):

- `scad, vega-lite, mermaid, plantuml, vsdx`: Every unsupported-feature
  diagnostic becomes an unsupported-feature error and no output is written.
- `pptx`: A skipped EMF or WMF picture becomes an error. Other unsupported
  shapes, fills, and pictures are errors with or without strict mode.
- `svg`: An image that was not drawn (svg-image-unresolved, or
  svg-image-too-large) and an unsupported feature of a model, chart, or diagram
  it embeds become unsupported-feature errors, and an image the file-access
  policy denied (image-access-denied) becomes the error file-access-denied. Text
  that is not drawn or drawn without a feature (svg-text-not-drawn,
  svg-text-unsupported) is an unsupported-feature error, also in an embedded
  SVG file.
- `pdf, raster`: No effect: they have no diagnostics channel. A PDF feature that
  cannot be drawn is an error with or without strict mode.

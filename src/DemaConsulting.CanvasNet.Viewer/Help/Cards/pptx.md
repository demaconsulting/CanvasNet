---
title: PowerPoint presentations (.pptx)
summary: 'PowerPoint presentations: each slide drawn as an image.'
tags:
  - pptx
  - hosting
  - diagnostics
aliases:
  - powerpoint
see-also:
  - svg/fragments
  - limits
---

Each slide is a page, drawn as an image.

## Supported

- Slides as pages: `Page` (from 1), every slide with `RenderRequest.AllPages` of
  the Imaging namespace, and `Dpi`.
- Embedding: `<image href="deck.pptx#slide=3" .../>` in an SVG file draws slide
  3 (#page=3 means the same; slide 1 without the fragment); a slide that does
  not exist is image-page-out-of-range. See [[svg]]. The same
  fragment on an input path chooses the slide (it overrides `Page`).
- The shape tree including nested groups; preset and custom geometry with solid,
  linear-gradient, and covered pattern fills, and outlines.
- Connectors (straight, elbow, and curved) with arrowheads.
- Raster pictures (BMP, PNG, TIFF, JPEG, GIF, with srcRect cropping) and
  pictures that only carry an SVG.
- Tables and embedded charts.
- Text with placeholder, layout, master, and theme inheritance, wrapping,
  alignment, vertical anchoring, autofit, bullets, and numbering.
- Slide, layout, and master backgrounds.

## Unsupported

- EMF and WMF pictures: they are skipped and reported (see diagnostics);
  strict mode turns the report into an error. Those on a layout or master are
  skipped silently.
- These always fail the page with unsupported-feature, with or without strict
  mode: radial and path gradients (pptx-gradient-path), preset shapes or pattern
  fills the library lacks (pptx-pattern-fill), linked images that are not
  embedded (pptx-image-link), and picture formats it cannot decode
  (pptx-image-format).
- `pptx-chart-<token>`: A chart feature that cannot be drawn fails the page with
  unsupported-feature; the token names the feature.
- A shape, picture, or graphic frame that has no position anywhere in its
  ancestry is skipped silently.

## Options

The host chooses these when it renders a slide: the properties of
`ViewerRenderOptions` for `CanvasNetViewer.Render`, or of `SourceOptions` for
the operations of the Imaging namespace.

- `Page`: The slide to draw, counted from 1 (default 1).
- `Dpi`: Resolution in dots per inch (default 96, above 0 and at most
  {{maxDpi}}).
- `Background`: The color the slide is cleared to first (white by
  default); a declared slide background is painted over it.
- `ThrowOnUnsupportedFeature`: Strict mode: fail with unsupported-feature when
  an EMF or WMF picture would be skipped.

## Diagnostics

- `pptx-image-metafile`: {source: pptx, severity: unsupported}: an EMF or WMF
  picture was skipped. It is the only skipped-picture report; in strict mode it
  is an unsupported-feature error.
- The other pptx-* identifiers appear in the message of an unsupported-feature
  error, not in the diagnostics.
- `unsupported-feature`: Error code: see the list of failures above.
- `page-out-of-range`: Error code: the page is beyond the last slide.

## Known differences

- Fonts that are not embedded come from the machine, so text can differ between
  machines.
- EMF and WMF pictures are not drawn (a known gap).
- The result is a still picture of one slide.

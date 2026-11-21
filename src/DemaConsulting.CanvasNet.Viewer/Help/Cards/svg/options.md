---
title: Render options of a drawing
summary: Size, scale, background, file access, and strict mode for a drawing.
tags:
  - svg
  - hosting
see-also:
  - files
  - svg/diagnostics
---

The host chooses these when it renders a drawing: the properties of
`ViewerRenderOptions` for `CanvasNetViewer.Render`, or of `SourceOptions` for
the operations of the Imaging namespace.

## Options

- `Width`, `Height`: Draw at this width or height, keeping the aspect ratio;
  with both, draw inside that box, centered and never stretched. The drawing is
  sharp, never enlarged afterwards.
- `Scale`: Multiply the intrinsic size by a factor (`SourceOptions.Scale`; it
  takes precedence over `Width` and `Height`).
- `Background`: The color transparency is flattened over.
  `CanvasNetViewer.Render` keeps the transparency by default; `SourceOptions`
  flattens over white by default, and a transparent color keeps alpha.
- `Folder` (`ViewerRenderOptions` only): The folder the image hrefs are relative
  to; by default the folder of the drawing.
- `FileAccess`: The `FileAccessPolicy` that decides which local files the image
  elements may read: `AllowAll` (any, the default), `UnderRoots(folders)`
  (roots), `NoSymlinks` (no-symlinks), or `Combine` of the two. See
  [[files]].
- `ThrowOnUnsupportedFeature`: Strict mode (`SourceOptions.Strict`). An image
  that was not drawn (svg-image-unresolved, svg-image-too-large,
  image-fragment-invalid, or image-page-out-of-range) is an unsupported-feature
  error instead of a warning, image-access-denied is the error
  file-access-denied, and an unsupported feature of an embedded model, chart, or
  diagram is an unsupported-feature error too. The warnings
  image-fragment-unknown-parameter, image-fragment-duplicate-parameter, and
  vl-param-unknown stay warnings: the picture was drawn. So does
  svg-arrowhead-shape, a hint about how an arrowhead is drawn. Text that is not
  drawn or drawn without a feature (svg-text-not-drawn, svg-text-unsupported)
  and a filter drawn without a feature (svg-filter-unsupported) are
  unsupported-feature errors, also in an embedded SVG file.

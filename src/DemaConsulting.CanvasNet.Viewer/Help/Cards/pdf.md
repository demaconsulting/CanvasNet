---
title: PDF documents (.pdf)
summary: 'PDF documents: a page drawn as an image.'
tags:
  - pdf
  - hosting
  - diagnostics
see-also:
  - svg/fragments
  - limits
---

Multi-page documents; a page is drawn as an image. There is no diagnostics
channel.

## Supported

- Pages: one page per render (`Page`, from 1), or every page with
  `RenderRequest.AllPages` of the Imaging namespace; the page rotation is
  applied.
- Embedding: `<image href="report.pdf#page=3" .../>` in an SVG file draws page 3
  (page 1 without the fragment); a page that does not exist is
  image-page-out-of-range. See [[svg]]. The same fragment on an
  input path chooses the page (it overrides `Page`; a page that does not exist
  is page-out-of-range).
- Resolution: `Dpi`, default 96, at most {{maxDpi}}.
- Paths, fills, strokes, clipping, and images (resampled once to the size they
  cover).
- Color spaces Device, Cal, Lab, ICCBased (chosen by component count; the
  profile is not applied), Indexed, Separation, DeviceN, and Pattern.
- Axial, radial, and mesh shadings (types 2 to 7) and tiling patterns.
- Text with simple TrueType fonts. An embedded font is used; a font that is not
  embedded is substituted silently by an installed font, else by the bundled
  Liberation fonts (Symbol and ZapfDingbats use bundled Noto fonts).

## Unsupported

- These make the page fail with unsupported-feature, with or without strict
  mode, and no partial picture is written: MMType1 fonts, function-based
  shadings (type 1), transparency groups, Type 3 glyphs under a text clip mode,
  and symbolic fonts that are not embedded and have no substitute.
- `pdf-image-lab-decode`: An image in a Lab color space with a Decode array.
- `pdf-image-tint-budget`: A DeviceN image with more distinct pixels than the
  tint-evaluation budget allows.
- ICC profile data is ignored silently.
- There is no option to supply a password, a font folder, or a page range other
  than one page or all pages.

## Options

The host chooses these when it renders a page: the properties of
`ViewerRenderOptions` for `CanvasNetViewer.Render`, or of `SourceOptions` for
the operations of the Imaging namespace.

- `Page`: The page to draw, counted from 1 (default 1); a page beyond the last
  is page-out-of-range. `ViewerImage.PageCount` tells how many pages there are.
- `Dpi`: Resolution in dots per inch (default 96, above 0 and at most
  {{maxDpi}}). A 72 by 48 point page is 96 by 64 pixels at 96 dpi.
- `Scale`: Multiply the natural size by a factor; a `Width` and a `Height` also
  size the page, fitted inside the box and never stretched.
- `Background`: The color the page is flattened over (white by default);
  a transparent color keeps alpha.
- `ThrowOnUnsupportedFeature`: Accepted, but PDF has no diagnostics channel, so
  strict mode changes nothing.

## Diagnostics

- None: the diagnostics of a PDF are always empty and strict mode has no effect.
  A feature the renderer cannot draw is an error instead.
- `unsupported-feature`: Error code: the message names the feature, for example
  pdf-image-lab-decode or pdf-image-tint-budget.
- `corrupt-input`: Error code: a damaged or truncated document.
- `page-out-of-range`: Error code: the page is beyond the last page.

## Known differences

- ICC profiles are not applied, so colors in ICCBased spaces can differ slightly
  from a viewer.
- A substituted font is chosen from the installed fonts, so text with fonts that
  are not embedded can differ between machines.
- Mesh shadings in DeviceCMYK, Separation, and DeviceN spaces are approximated:
  each vertex is converted to RGB first and the result is blended.
- Images are resampled once, so thin one pixel rules are attenuated rather than
  dropped.

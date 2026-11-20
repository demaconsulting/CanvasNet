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

- Pages: --page N (from 1) or --all-pages; the page rotation is applied.
- Embedding: `<image href="report.pdf#page=3" .../>` in an SVG file draws page 3
  (page 1 without the fragment); a page that does not exist is
  image-page-out-of-range. See [[svg]]. On the command line the
  same fragment on the input chooses the page: canvasnet render
  "report.pdf#page=3" -o p3.png (it overrides --page; a page that does not exist
  is page-out-of-range).
- Resolution: --dpi, default 96, at most {{maxDpi}}.
- Paths, fills, strokes, clipping, and images (resampled once to the size they
  cover).
- Color spaces Device, Cal, Lab, ICCBased (chosen by component count; the
  profile is not applied), Indexed, Separation, DeviceN, and Pattern.
- Axial, radial, and mesh shadings (types 2 to 7) and tiling patterns.
- Text with simple TrueType fonts. An embedded font is used; a font that is not
  embedded is substituted silently by an installed font, else by the bundled
  Liberation fonts (Symbol and ZapfDingbats use bundled Noto fonts).

## Unsupported

- These make the page fail with unsupported-feature, with or without --strict,
  and no partial picture is written: MMType1 fonts, function-based shadings
  (type 1), transparency groups, Type 3 glyphs under a text clip mode, and
  symbolic fonts that are not embedded and have no substitute.
- `pdf-image-lab-decode`: An image in a Lab color space with a Decode array.
- `pdf-image-tint-budget`: A DeviceN image with more distinct pixels than the
  tint-evaluation budget allows.
- ICC profile data is ignored silently.
- The tool has no option to supply a password, a font folder, or a page range
  other than one page or all pages.

## Options

- `--page <N>`: The page to draw, counted from 1 (default 1); a page beyond the
  last is page-out-of-range.
- `--all-pages`: Draw every page; for a document of more than one page the
  output must contain {page} or be a directory.
- `--dpi <D>`: Resolution in dots per inch (default 96, above 0 and at most
  {{maxDpi}}). A 72 by 48 point page is 96 by 64 pixels at 96 dpi.
- `--scale <factor>`: Multiply the natural size by a factor; --width, --height,
  and --size also size the page.
- `--background <color>`: The color the page is flattened over (white by
  default).
- `--strict`: Accepted, but PDF has no diagnostics channel, so it changes
  nothing.

## Diagnostics

- None: the diagnostics[] list of a PDF is always empty and --strict has no
  effect. A feature the renderer cannot draw is an error instead.
- `unsupported-feature`: Error code: the message names the feature, for example
  pdf-image-lab-decode or pdf-image-tint-budget.
- `corrupt-input`: Error code: a damaged or truncated document.
- `page-out-of-range`: Error code: --page is beyond the last page.

## Known differences

- ICC profiles are not applied, so colors in ICCBased spaces can differ slightly
  from a viewer.
- A substituted font is chosen from the installed fonts, so text with fonts that
  are not embedded can differ between machines.
- Mesh shadings in DeviceCMYK, Separation, and DeviceN spaces are approximated:
  each vertex is converted to RGB first and the result is blended.
- Images are resampled once, so thin one pixel rules are attenuated rather than
  dropped.

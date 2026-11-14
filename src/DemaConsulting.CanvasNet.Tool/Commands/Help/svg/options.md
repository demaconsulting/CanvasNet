---
title: Render options of a drawing
summary: Size, scale, background, file access, and --strict for a drawing.
see-also:
  - files
  - svg/diagnostics
examples:
  - name: render-width
    kind: command
    language: shell
    content: 'canvasnet render shapes.svg --width 480 -o shapes.png'
    description: Draw shapes.svg 480 pixels wide.
  - name: transparent
    kind: command
    language: shell
    content: 'canvasnet render shapes.svg --background transparent -o shapes-alpha.png'
    description: Keep the transparency of the drawing.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info shapes.svg --json'
    description: Report the intrinsic size without drawing.
---

## Options

- `--width <pixels>`: Draw at this width, keeping the aspect ratio; --height
  does the same for the height. The drawing is sharp, never enlarged afterwards.
- `--size <WxH>`: Draw inside this box, centered and never stretched.
- `--scale <factor>`: Multiply the intrinsic size by a factor.
- `--background <color>`: The color transparency is flattened over (white by
  default); transparent keeps alpha.
- `--file-access <mode>`: Which local files the image elements may read: any
  (the default), roots, no-symlinks, or roots,no-symlinks; --allow-root names
  the folders of roots. See 'canvasnet help files'.
- `--strict`: An image that was not drawn (svg-image-unresolved,
  svg-image-too-large, image-fragment-invalid, or image-page-out-of-range) is an
  unsupported-feature error instead of a warning, image-access-denied is the
  error file-access-denied, and an unsupported feature of an embedded model,
  chart, or diagram is an unsupported-feature error too. The warnings
  image-fragment-unknown-parameter, image-fragment-duplicate-parameter, and
  vl-param-unknown stay warnings: the picture was drawn.

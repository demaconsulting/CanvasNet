---
extends: svg/options
summary: Size, scale, background, file access, and --strict for a drawing.
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

## Command line

- `--width <pixels>`: Draw at this width, keeping the aspect ratio; --height
  does the same for the height. The drawing is sharp, never enlarged afterwards.
- `--size <WxH>`: Draw inside this box, centered and never stretched.
- `--scale <factor>`: Multiply the intrinsic size by a factor.
- `--background <color>`: The color transparency is flattened over (white by
  default); transparent keeps alpha.
- `--file-access <mode>`: Which local files the image elements may read: any
  (the default), roots, no-symlinks, or roots,no-symlinks; --allow-root names
  the folders of roots. See [[files]].
- `--strict`: Strict mode, as `ThrowOnUnsupportedFeature` above: an image that
  was not drawn is an error, and the warnings that say the picture was drawn
  stay warnings.

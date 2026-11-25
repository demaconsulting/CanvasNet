---
extends: mathml/options
summary: Scale, size, background, display, --strict, and what info reports for an equation.
examples:
  - name: render
    kind: command
    language: shell
    content: 'canvasnet render quadratic.mml --scale 3 -o quadratic.png --json'
    description: Draw the equation at three times its natural size and print the JSON result.
  - name: info
    kind: command
    language: shell
    content: 'canvasnet info quadratic.mml --json'
    description: Report the natural size, display, elements, and baseline without drawing.
---

## Command line

- `--scale <factor>`: Lays the equation out again at that multiple of its
  natural size (16 pixels per em), so it stays sharp; 2 or 3 suits print.
- `--width <pixels>`: Draws the equation into that width, keeping its aspect
  ratio; --height does the same for the height, and --size fits it inside a
  box, centered.
- `--background <color>`: The color under the equation, white by default;
  transparent gives a transparent PNG.
- `--strict`: Strict mode: the first element, attribute, or style that is not
  drawn becomes an unsupported-feature error and no file is written.
- `--theme`: Not for an equation: --theme and --positions are a usage-error on
  a .mml input.
- `display`: An input path takes the fragment: canvasnet render
  "eq.mml#display=inline" -o eq.png lays the equation out inline.
- `info`: Reports the natural size (image) and the equation object: display,
  elements, baseline, altText, and annotations (see
  [[json/results]]).

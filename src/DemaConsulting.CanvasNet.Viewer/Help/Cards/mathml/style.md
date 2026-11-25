---
title: Styling equations
summary: Colors to highlight a term, sizes, display style, script levels, and the picture's background.
tags:
  - mathml
  - design
see-also:
  - style
  - mathml/composition
  - mathml/attributes
  - mathml/differences
examples:
  - name: highlight
    kind: source
    language: xml
    file: highlight.mml
    description: The quadratic formula with its discriminant highlighted in red on a pale red background.
---

An equation is black on white (transparent when it is placed in another
picture), in Noto Sans Math at 16 pixels, the default size of a browser. Keep it
that way and use color only to point at a term.

## Color

- `mathcolor="#B3261E"` colors an element and everything inside it, and
  `mathbackground="#FDECEA"` fills its box: put both on an `mrow` around the
  term you discuss (see [[#highlight]]), or on `mstyle` to color a
  whole part. Colors are CSS colors: names, `#RGB`, `#RRGGBB`, `rgb(...)`, and
  the like.
- Use one accent color and a pale background of the same hue; several colors in
  one equation are hard to read, and color alone must not carry meaning.

## Size and style

- `mathsize="1.2em"` (or a percentage, or pixels) scales an element and its
  children. To make the whole equation larger, draw it at a larger scale
  instead: it is laid out again at that size, so it stays sharp.
- `displaystyle="true"` lays a part out in display style (full-size fractions,
  large operators, limits under and over), `displaystyle="false"` in compact
  style; `scriptlevel="+1"` makes a part one script size smaller, and
  `scriptlevel="0"` brings it back to full size.
- The inline `style` attribute takes `color`, `background-color`, `font-size`,
  `font-style`, `math-style` (normal or compact), `math-depth`, and `display:
  none`; other declarations are reported as unsupported.

## The picture

- Drawn alone, the equation is black on white; a background of the host options
  replaces the white, and a transparent one keeps the picture transparent.
  Placed in an SVG figure, a diagram, or a chart it is always transparent, so
  the page color shows.
- The natural size of the picture is the equation at 16 pixels per em, and a
  scale factor multiplies it: give equations of one document the same scale (or
  place them at the same multiple of their natural size) so their text has one
  size.

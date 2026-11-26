---
title: Supported attributes and styles
summary: The attributes of every element, the lengths and colors they take, and the inline style subset.
tags:
  - mathml
  - authoring
see-also:
  - mathml/elements
  - mathml/style
  - mathml/unsupported
---

An attribute with a value it cannot use is ignored with the warning
`mathml-attribute-invalid`; an attribute outside this list is ignored and
reported as `mathml-attribute-unsupported`.

## Supported

- Global attributes: `displaystyle`, `scriptlevel` (a number, +N, or -N),
  `mathvariant`, `mathsize`, `mathcolor`, `mathbackground`, `style`, and
  `dir="ltr"` on every element; `id`, `class`, `data-*`, `intent`, `arg`,
  `nonce`, `tabindex`, `autofocus`, and event handlers are accepted and change
  nothing in the picture.
- mathvariant: every MathML 3 value (normal, bold, italic, bold-italic,
  double-struck, script, bold-script, fraktur, bold-fraktur, sans-serif,
  bold-sans-serif, sans-serif-italic, sans-serif-bold-italic, monospace,
  initial, tailed, looped, and stretched) maps the characters to the
  Mathematical Alphanumeric Symbols, as Firefox draws them.
- math: `display` (block or inline) and `alttext`.
- mo: `form` (prefix, infix, postfix), `lspace`, `rspace`, `stretchy`,
  `symmetric`, `minsize`, `maxsize`, `largeop`, and `movablelimits` override the
  operator dictionary; `fence`, `separator`, and `accent` are accepted and
  change nothing.
- mfrac: `linethickness` (a length; 0 for no bar).
- munder, mover, and munderover: `accentunder` and `accent` (true or false).
- mspace and mpadded: `width`, `height`, and `depth`, and on mpadded also
  `lspace` and `voffset`.
- Tables: `columnalign`, `rowalign`, `columnspacing`, `rowspacing`,
  `framespacing`, `columnlines`, `rowlines`, and `frame` on mtable,
  `columnalign` and `rowalign` on mtr and mtd, and `columnspan` and `rowspan` on
  mtd, drawn as Firefox draws them (Chrome ignores the table attributes).
- maction and semantics: `actiontype`, `selection`, `encoding`, and
  `definitionURL` are accepted; the first child is drawn.
- Lengths: a number with em, ex, px, pt, pc, in, cm, or mm, a bare 0, and the
  named spaces veryverythinmathspace to veryverythickmathspace (and their
  negative forms where a negative length is allowed, as in lspace); the width of
  mspace cannot be negative, and a percentage is ignored on mspace and mpadded.
- Colors: CSS colors - names, #RGB, #RGBA, #RRGGBB, #RRGGBBAA, rgb(), rgba(),
  hsl(), hsla(), and transparent.
- Inline style: `color`, `background-color`, `font-size`, `font-style`,
  `math-style`, `math-depth`, and `display: none`.

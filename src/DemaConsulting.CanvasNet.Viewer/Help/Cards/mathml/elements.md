---
title: Supported elements
summary: Every element of MathML Core that is drawn, by group, and how invalid markup is drawn.
tags:
  - mathml
  - authoring
see-also:
  - mathml/attributes
  - mathml/unsupported
  - mathml/basics
---

Every element of MathML Core is drawn as the specification and Chrome lay it
out; an element outside MathML Core is reported (see
[[mathml/unsupported]]).

## Supported

- Tokens: `mi` (one letter italic, several upright), `mn`, `mo` (spaced,
  stretched, and sized by the operator dictionary of MathML Core), `mtext`, `ms`
  (drawn without quotes), and `mspace` (an empty box of the given width, height,
  and depth).
- Rows and grouping: `math`, `mrow`, `mstyle`, `mpadded` (with its width,
  height, depth, lspace, and voffset), `mphantom` (laid out, not painted), and
  `merror` (a red border on a light yellow background).
- Fractions: `mfrac`, with or without a bar (`linethickness`), the bar on the
  math axis.
- Radicals: `msqrt` and `mroot`, with a radical sign that stretches over the
  base and a raised index.
- Scripts: `msub`, `msup`, and `msubsup`, placed by the script constants of the
  math font.
- Under and over: `munder`, `mover`, and `munderover`, with limits, accents,
  stretchy horizontal operators (arrows, bars, braces), and movable limits that
  become scripts outside display style.
- Prescripts: `mmultiscripts` with `mprescripts` and `none`.
- Tables: `mtable`, `mtr`, and `mtd`, with spans, centered on the math axis.
- Semantics and actions: `semantics` and `maction` draw their first child;
  `annotation` and `annotation-xml` are kept, reported by a probe, and never
  drawn.
- Invalid markup: an element without the children MathML Core requires (an
  `mfrac` or `mroot` without exactly two, a script without its base and scripts)
  is drawn as a row of its children with the warning `mathml-child-count`, as
  browsers draw it.

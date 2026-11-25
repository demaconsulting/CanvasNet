---
title: MathML equations (.mml)
summary: MathML Core equations (.mml) drawn as one picture.
tags:
  - mathml
  - authoring
aliases:
  - mml
  - equation
children:
  - basics
  - fractions
  - scripts
  - large-operators
  - fences
  - matrices
  - aligned
  - units
  - symbols
  - accents
  - formulas
  - style
  - pitfalls
  - composition
  - elements
  - attributes
  - options
  - diagnostics
  - unsupported
  - differences
see-also:
  - style
  - composition
  - svg
  - limits
---

An equation file holds one `<math>` element of MathML Core, the subset of MathML
that Chrome and Firefox draw, in the namespace
<http://www.w3.org/1998/Math/MathML>, saved as UTF-8 with the extension .mml. It
is laid out like a browser lays it out, with the bundled Noto Sans Math font,
and drawn as one still picture.

Start with [[mathml/basics]], then read the card of what you write,
for example [[mathml/fractions]] or
[[mathml/matrices]]; each has worked examples
([[#quadratic]]). Read
[[mathml/pitfalls]] before you write a long equation, render it in
strict mode, and look at the picture.

To place an equation in a figure, a diagram, or a chart, link it with `<image
href="eq.mml" .../>` (see [[mathml/composition]] and
[[svg]]).

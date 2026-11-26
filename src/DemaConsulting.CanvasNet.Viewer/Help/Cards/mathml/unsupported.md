---
title: Unsupported MathML
summary: The elements, attributes, styles, and inputs that are reported and not drawn.
tags:
  - mathml
  - diagnostics
see-also:
  - mathml/diagnostics
  - mathml/differences
  - mathml/pitfalls
---

A construct that is not drawn is reported once, with its line and column and a
hint, and the rest of the equation is drawn; strict mode makes the first one an
unsupported-feature error.

## Unsupported

- `mathml-element-unsupported`: An element outside MathML Core (an element of
  MathML 4 Full such as `mfenced`, `menclose`, `mglyph`, `mlabeledtr`, or
  `mstack`, or of Content MathML such as `apply`), drawn as a row of its
  children; an element of another namespace or inside a token, skipped; or text
  directly inside a layout element, drawn like an `mtext`.
- `mathml-attribute-unsupported`: An attribute that has no effect in MathML
  Core, such as `linebreak`, `columnwidth`, `bevelled`, the quotes of `ms`,
  `dir="rtl"` (the equation is laid out left to right), or a link (`href`,
  `xlink:href`), which is never followed; it is ignored.
- `mathml-css-unsupported`: A declaration of the `style` attribute outside the
  supported subset (such as `border` or `padding`); it is ignored.
- Line breaking: an equation is drawn on one line however long it is; break it
  into the rows of a table yourself (see [[mathml/aligned]]).
- LaTeX: a LaTeX formula is not read (a LaTeX front end is planned in issue
  #198); write MathML, and keep the LaTeX in an annotation.
- Style sheets: there is no style sheet; only the inline `style` attribute and
  the MathML attributes style an equation.
- Interaction: `maction` draws its first child, and links are not followed; the
  picture is static.

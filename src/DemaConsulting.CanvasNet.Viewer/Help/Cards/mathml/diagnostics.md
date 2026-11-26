---
title: Diagnostics of an equation
summary: The mathml-* warnings and errors of an equation and how a host sees them.
tags:
  - mathml
  - diagnostics
see-also:
  - mathml/unsupported
  - mathml/pitfalls
  - errors
---

Every diagnostic has a stable code, the line and column in the file, and a hint.
A render continues after a warning, also in strict mode.

## Diagnostics

- `mathml-warning`: The code of every warning of an equation; the specific
  mathml-* code is in detail and the line and column in location.
- `mathml-syntax-error`: Error (mathml-error): the text is not valid UTF-8 or
  not well-formed XML, declares a document type or an encoding other than UTF-8,
  uses a named reference that is not in the W3C set, or its root is not `math`;
  the message gives the file, the line, and the column (an empty file is line 1,
  column 1), and the hint suggests the fix.
- `mathml-limit-exceeded`: Error (budget-exceeded): a limit of the library
  (elements, nesting, token length, table cells, layout work, painted glyphs, or
  a picture larger than the largest image); see [[limits]].
- `mathml-render-failed`: Error (mathml-error): the equation cannot be laid out;
  please report it.
- `mathml-attribute-invalid`: Warning: an attribute value its attribute cannot
  use (a length without a unit, a negative width, an unknown keyword); the
  attribute is ignored.
- `mathml-child-count`: Warning: an element without the children MathML Core
  requires (an `mfrac` or `mroot` without exactly two); it is drawn as a row of
  its children.
- `mathml-text-missing-glyph`: Warning: a character that neither the math font
  nor the bundled fallback fonts have; it is drawn as a box.
- `mathml-stretch-unavailable`: Warning: an operator that must stretch, but that
  neither the font nor a fallback can stretch; it keeps its normal size.
- `mathml-diagnostics-suppressed`: Warning: more reports than the diagnostics
  limit; the rest are not listed.
- A text longer than {{mmlMaxTextSize}} characters is corrupt-input.

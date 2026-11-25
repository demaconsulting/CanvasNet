---
title: Common engineering formulas
summary: Worked formulas of circuits, control, signals, and fields, and the pattern each one uses.
tags:
  - mathml
  - authoring
see-also:
  - mathml/aligned
  - mathml/accents
  - mathml/large-operators
examples:
  - name: kirchhoff
    kind: source
    language: xml
    file: kirchhoff.mml
    description: Kirchhoff's current and voltage laws - sums with limits and a word under the sum.
  - name: transfer-function
    kind: source
    language: xml
    file: transfer-function.mml
    description: The transfer function of a second-order system - tight parentheses and a subscript with a square.
  - name: fourier-transform
    kind: source
    language: xml
    file: fourier-transform.mml
    description: The Fourier transform - a hat accent, an integral with limits, and an upright e, i, and d.
  - name: maxwell
    kind: source
    language: xml
    file: maxwell.mml
    description: Maxwell's equations in differential form - an aligned table with bold vectors and partial derivatives.
  - name: state-space
    kind: source
    language: xml
    file: state-space.mml
    description: A state-space model - dot accents for the time derivative and bold matrices and vectors.
---

Print an example, change the symbols, and render it. Ohm's law is the basics
example ohm and the quadratic formula the fractions example quadratic.

## Patterns

- Kirchhoff ([[#kirchhoff]]): `munderover` puts the limits on the sum,
  and `<munder><mo>&sum;</mo><mtext>loop</mtext></munder>` a word under it.
- Transfer function ([[#transfer-function]]): H(s) is
  `<mi>H</mi><mo>&ApplyFunction;</mo>` and `stretchy="false"` parentheses; ω
  with the subscript n and the square 2 is
  `<msubsup><mi>&omega;</mi><mi>n</mi><mn>2</mn></msubsup>`.
- Fourier transform ([[#fourier-transform]]): the hat is `<mover
  accent="true"><mi>f</mi><mo>&#x2C6;</mo></mover>`; the constants e and i and
  the d of dx are upright with `mathvariant="normal"`.
- Maxwell ([[#maxwell]]): one aligned table (see
  [[mathml/aligned]]), vectors in `mathvariant="bold"`, the
  divergence and the curl as `<mo>&nabla;</mo><mo>&sdot;</mo>` and
  `<mo>&nabla;</mo><mo>&times;</mo>`, and a negated fraction in its own row,
  `<mrow><mo>&minus;</mo><mfrac>...</mfrac></mrow>`, so its minus is a sign.
- State space ([[#state-space]]): the derivative of the state is
  `<mover accent="true"><mi mathvariant="bold">x</mi><mo>&#x2D9;</mo> </mover>`,
  and `&InvisibleTimes;` joins a matrix to its vector.

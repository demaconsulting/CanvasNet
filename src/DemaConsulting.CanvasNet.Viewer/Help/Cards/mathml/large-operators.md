---
title: Sums, integrals, and limits
summary: Sums and products with limits, integrals, limits of functions, and differentials.
tags:
  - mathml
  - authoring
see-also:
  - mathml/scripts
  - mathml/fences
  - mathml/units
examples:
  - name: sum
    kind: source
    language: xml
    file: sum.mml
    description: The binomial theorem - a sum with limits under and over it in display style.
  - name: integral
    kind: source
    language: xml
    file: integral.mml
    description: The Gaussian integral - limits beside the integral sign, an upright d, and a square root.
  - name: limit
    kind: source
    language: xml
    file: limit.mml
    description: A limit of a function - lim with movablelimits and a thin space after sin.
---

## Sums and products

- Put the limits on the operator with `munderover` (or `munder`):
  `<munderover><mo>&sum;</mo><mrow>...</mrow><mi>n</mi></munderover>`, then the
  summand after it. `&sum;`, `&prod;`, `&coprod;`, `&bigcup;`, and `&bigcap;`
  are large operators with movable limits: in display style they are drawn large
  with the limits under and over, and in an inline equation smaller with the
  limits as scripts (see [[#sum]] and the basics example inline-sum).
- `movablelimits="false"` keeps the limits under and over in an inline equation
  too; `largeop="true"` makes another operator large in display style.

## Integrals

- Write the limits of an integral with `msubsup`, so they stay beside the sign
  as they should: `<msubsup><mo>&int;</mo><mn>0</mn><mn>1</mn> </msubsup>` (with
  `munderover` they would go under and over it). Also `&Int;`, `&tint;`, and
  `&conint;` for double, triple, and contour integrals.
- Write the differential as an upright d after a thin space: `<mspace
  width="0.1667em"/><mi mathvariant="normal">d</mi><mi>x</mi>`.

## Limits

- `<munder><mo movablelimits="true">lim</mo><mrow><mi>x</mi><mo>&rarr;</mo>
  <mn>0</mn></mrow></munder>`: a word operator such as lim, max, or sup is not
  in the operator dictionary, so give it `movablelimits="true"`; then its limit
  goes under it in display style and becomes a subscript inline.

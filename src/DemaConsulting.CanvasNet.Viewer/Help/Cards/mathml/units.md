---
title: Units, numbers, and engineering notation
summary: Numbers, negative values, powers of ten, upright units, and invisible operators.
tags:
  - mathml
  - authoring
see-also:
  - mathml/symbols
  - mathml/pitfalls
  - mathml/formulas
examples:
  - name: units
    kind: source
    language: xml
    file: units.mml
    description: Quantities with units - an acceleration, a negative temperature, a resistance, and a modulus.
---

## Numbers

- A number is one `mn`, decimal point and all: `<mn>9.81</mn>`, `<mn>4.7</mn>`.
- A negative number is a minus sign and a number in their own row:
  `<mrow><mo>&minus;</mo><mn>40</mn></mrow>`. Without the `mrow`, after an `=`
  the minus is spaced like a binary operator (T = - 40).
- Powers of ten: `<mn>2.1</mn><mo>&times;</mo><msup><mn>10</mn><mn>11</mn>
  </msup>`.

## Units

- Write units upright, with a thin space between the number and the unit:
  `<mn>9.81</mn><mspace width="0.1667em"/><mi mathvariant="normal">m</mi>`.
- A unit of one letter needs `mathvariant="normal"` (a single-letter `mi` is
  italic); a unit of several letters is upright already: `<mi>Pa</mi>`,
  `<mi>k&Omega;</mi>`, `<mi>&deg;C</mi>`.
- Divide with `<mo>/</mo>` and raise a unit with `msup`: the example
  [[#units]] writes m/s² with `<msup><mi mathvariant="normal">s</mi>
  <mn>2</mn></msup>`.

## Invisible operators

- `&InvisibleTimes;` (U+2062) between factors, as in `<mi>I</mi>
  <mo>&InvisibleTimes;</mo><mi>R</mi>`, and `&ApplyFunction;` (U+2061) after a
  function name: both are drawn as nothing and take no space, but they tell the
  layout and a screen reader what the juxtaposition means.
- After `&ApplyFunction;` and before a bare argument add a thin space, `<mspace
  width="0.1667em"/>`, or x touches sin.

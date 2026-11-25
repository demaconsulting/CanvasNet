---
title: Fractions and roots
summary: Fractions, nested and continued fractions, binomials, square roots, and nth roots.
tags:
  - mathml
  - authoring
see-also:
  - mathml/scripts
  - mathml/pitfalls
  - mathml/style
examples:
  - name: quadratic
    kind: source
    language: xml
    file: quadratic.mml
    description: The quadratic formula - a fraction whose numerator holds a square root.
  - name: continued-fraction
    kind: source
    language: xml
    file: continued-fraction.mml
    description: A continued fraction kept at full size with displaystyle on the inner fractions.
  - name: binomial-root
    kind: source
    language: xml
    file: binomial-root.mml
    description: A binomial coefficient (a fraction without a bar in stretchy parentheses) and a cube root.
---

## Fractions

- `<mfrac>numerator denominator</mfrac>` takes exactly two children: wrap a
  numerator or a denominator of several parts in `mrow`
  (`<mfrac><mrow><mi>a</mi><mo>+</mo><mi>b</mi></mrow><mn>2</mn></mfrac>`).
- The bar sits on the math axis, centered on the minus sign of the row, and is
  as wide as the wider part.
- In display style the parts of a fraction are drawn at full size; inside a
  fraction, a script, or an inline equation they shrink one step. Put
  `displaystyle="true"` on an inner `mfrac` to keep a nested or continued
  fraction at full size, as [[#continued-fraction]] does.
- `linethickness="0"` removes the bar, for a binomial coefficient in stretchy
  parentheses; `linethickness="2px"` or `"0.1em"` makes it thicker.

## Roots

- `<msqrt>...</msqrt>` takes any number of children (they form one row) and
  stretches the radical sign over them.
- `<mroot>base index</mroot>` takes exactly two children, the base first and the
  index second: `<mroot><mi>x</mi><mn>3</mn></mroot>` is the cube root.

A short example, drawn in display style:

```xml
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
  <mfrac><mn>1</mn><mn>2</mn></mfrac>
  <mo>+</mo>
  <mfrac><mn>1</mn><mn>3</mn></mfrac>
  <mo>=</mo>
  <mfrac><mn>5</mn><mn>6</mn></mfrac>
  <mo>,</mo>
  <mspace width="1em"/>
  <msqrt><mn>2</mn></msqrt>
  <mo>&approx;</mo>
  <mn>1.414</mn>
</math>
```

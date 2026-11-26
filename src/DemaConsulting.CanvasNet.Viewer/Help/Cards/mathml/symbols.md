---
title: Greek letters, symbols, and named entities
summary: Greek letters, operators and relations by name, double-struck sets, and bold vectors.
tags:
  - mathml
  - authoring
see-also:
  - mathml/basics
  - mathml/units
  - mathml/accents
examples:
  - name: greek
    kind: source
    language: xml
    file: greek.mml
    description: The epsilon-delta definition of continuity - Greek letters, quantifiers, and relations.
---

## Greek letters

- By name: `&alpha;` to `&omega;` and `&Alpha;` to `&Omega;`, with the variants
  `&epsilon;` and `&varepsilon;`, `&phi;` and `&varphi;`, `&theta;` and
  `&vartheta;`.
- A Greek letter in `mi` is italic like any one-letter identifier; an upright
  capital, such as the Delta of a difference, needs `mathvariant="normal"`: `<mi
  mathvariant="normal">&Delta;</mi><mi>T</mi>`.

## Operators and relations

- Calculus: `&PartialD;` (the partial derivative), `&nabla;`, `&infin;`.
- Relations: `&le;`, `&ge;`, `&ne;`, `&approx;`, `&equiv;`, `&prop;`, `&sim;`,
  `&lt;` and `&gt;` (a bare < cannot appear in XML text).
- Logic and sets: `&forall;`, `&exist;`, `&isin;`, `&notin;`, `&sub;`, `&cup;`,
  `&cap;`, `&empty;`, `&rArr;`, `&hArr;`.
- Arrows: `&rarr;`, `&larr;`, `&harr;`, `&map;` (maps to).
- Products: `&times;`, `&sdot;` (a centered dot), `&compfn;`.

## Alphabets

- `mathvariant="double-struck"` writes the number sets: `<mi
  mathvariant="double-struck">R</mi>` is the real numbers.
- `mathvariant="bold"` writes vectors and matrices upright and bold (`<mi
  mathvariant="bold">v</mi>`), `bold-italic` slanted; `script`, `fraktur`,
  `sans-serif`, and `monospace` (and their bold forms) are available too.

```xml
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
  <mi mathvariant="normal">&Delta;</mi>
  <mi>T</mi>
  <mo>&ne;</mo>
  <mn>0</mn>
  <mo>,</mo>
  <mspace width="1em"/>
  <mi>x</mi>
  <mo>&isin;</mo>
  <mi mathvariant="double-struck">R</mi>
</math>
```

---
title: Parentheses, brackets, and stretchy fences
summary: Fences that grow with their content, absolute values, norms, brackets, and fixed sizes.
tags:
  - mathml
  - authoring
see-also:
  - mathml/matrices
  - mathml/pitfalls
  - mathml/attributes
examples:
  - name: fences
    kind: source
    language: xml
    file: fences.mml
    description: An absolute value around a fraction, a norm, a tight bra-ket, and parentheses with a minimum size.
---

## Stretchy fences

- A fence is an `mo` at the start and the end of an `mrow`:
  `<mrow><mo>(</mo>...<mo>)</mo></mrow>`. Parentheses, brackets, braces, `|`,
  `&#x2016;` (a norm), `&lang;` and `&rang;`, `&lfloor;` and `&rfloor;`, and
  `&lceil;` and `&rceil;` stretch to the height of the row they enclose,
  symmetrically about the math axis.
- Group what a fence encloses: the fences stretch to their own row only, so
  `<mrow><mo>(</mo><mfrac>...</mfrac><mo>)</mo></mrow>` grows around the
  fraction and the rest of the equation stays outside.
- An opening fence without a closing one is fine: `<mrow><mo>{</mo>
  <mtable>...</mtable></mrow>` is the left brace of a case analysis (see
  [[mathml/matrices]]).

## Tight fences around short content

- A stretchy fence is centered in the width of its widest size, as Chrome draws
  it, so around a single letter it looks loose. Write `<mo
  stretchy="false">(</mo>` for the parentheses of f(x), |x|, and other short
  arguments; keep the fences stretchy around fractions, sums, and tables.
- In a bra-ket write the middle bar as `<mo stretchy="false" lspace="0"
  rspace="0">|</mo>`.

## Sizes

- `minsize="2em"` makes a fence at least that tall (in em of its font, or a
  percentage of its normal height), `maxsize` caps it, and `symmetric="false"`
  lets it follow the row instead of centering on the axis.
- `stretchy="true"` makes any operator stretch; the `fence` and `separator`
  attributes are accepted and change nothing in the picture.

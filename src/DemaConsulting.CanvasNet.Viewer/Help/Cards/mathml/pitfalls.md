---
title: Pitfalls and good style
summary: The mistakes that make an equation look wrong, the markup to write instead, and what is not drawn.
tags:
  - mathml
  - authoring
see-also:
  - mathml/basics
  - mathml/unsupported
  - mathml/diagnostics
examples:
  - name: good-style
    kind: source
    language: xml
    file: good-style.mml
    description: A derivative written with care - a prime after the base, tight parentheses, a real minus, thin spaces.
---

## Write the markup, not the look

- Numbers in `mn`, identifiers in `mi`, operators in `mo`: the operator
  dictionary spaces `=`, `+`, and `&minus;` for you, so do not add `mspace`
  around them.
- A function name is one token, `<mi>sin</mi>`, which is upright because it has
  several letters; three one-letter tokens would be three italic letters.
- Group with `mrow`. A fraction, a root with an index, and every script take an
  exact number of children; another number is reported as `mathml-child-count`
  and the element is drawn as a plain row.

## Characters

- Minus is `&minus;` (U+2212); the hyphen `-` is drawn as a short hyphen.
- A hat is `&#x2C6;`, not `^`, which is drawn large and offset.
- A prime follows its base as an operator, `<mi>f</mi><mo>&prime;</mo>`;
  `<msup><mi>f</mi><mo>&prime;</mo></msup>` draws it tiny and too high.
- In XML text `<` is `&lt;` and `&` is `&amp;`.

## Spacing and form

- After `&ApplyFunction;`, before a bare argument, put `<mspace
  width="0.1667em"/>`; without it x touches sin.
- Around a short argument write `<mo stretchy="false">(</mo>`: a stretchy fence
  is centered in its widest size and leaves gaps, so f(x) looks loose.
- An operator's spacing follows its place in its row: first is a prefix, last a
  postfix, otherwise infix. Wrap a negative number in its own `mrow`
  (`<mrow><mo>&minus;</mo><mn>3</mn></mrow>`), and write a `+` that starts a
  continuation row as `<mo form="infix">+</mo>`.

## Not drawn

- There is no automatic line breaking: split a long equation into rows of an
  aligned table (see [[mathml/aligned]]).
- `mfenced` is not part of MathML Core: write the fences as `mo` in an `mrow`.
  `menclose` neither: box a result with `<mtable frame="solid">`. `mglyph`,
  `mlabeledtr` (use a third column for the numbers), and Content MathML are not
  drawn either; see [[mathml/unsupported]].
- LaTeX is not read: write MathML (a LaTeX front end is planned, issue #198);
  keep the LaTeX in an annotation if you have it.

## Check it

- Render in strict mode (`ThrowOnUnsupportedFeature`): every construct that is
  not drawn fails with its `mathml-*` code, its line and column, and a hint.
  Warnings, such as `mathml-child-count`, stay diagnostics in strict mode too:
  treat every diagnostic as a mistake to fix. Then look at the picture.

---
title: Aligned multi-line equations
summary: Derivations aligned at the equals sign, equation numbers, and long equations over rows.
tags:
  - mathml
  - authoring
see-also:
  - mathml/matrices
  - mathml/formulas
  - mathml/pitfalls
examples:
  - name: derivation
    kind: source
    language: xml
    file: derivation.mml
    description: A three-step derivation of an integral, aligned at the equals signs, in display style.
  - name: numbered
    kind: source
    language: xml
    file: numbered.mml
    description: Two equations aligned at their equals signs with right-aligned equation numbers.
---

## Align at the equals sign

- Write the derivation as one `mtable` of two columns: `<mtable
  columnalign="right left" columnspacing="0" displaystyle="true">`. The left
  cell holds the left-hand side (empty in the rows that continue), and the right
  cell starts with the `=` and holds the right-hand side, so every `=` lines up
  and keeps its normal spacing.
- `displaystyle="true"` on the table keeps the fractions, sums, and integrals of
  the cells at display size; without it the cells are compact.
- `rowspacing="0.5em"` opens the rows for tall content.

## Equation numbers

- MathML Core has no `mlabeledtr`: give the table a third column for the
  numbers, with `columnalign="right left right"` and `columnspacing="0 4em"`,
  and write each number in an `mtext`, such as `<mtd><mtext>(1)</mtext></mtd>`
  (see [[#numbered]]).

## Long equations

- There is no automatic line breaking: an equation is drawn on one line however
  long it is. Break a long right-hand side into rows of the table yourself, each
  continuation row starting with its operator in the right cell. Mark that
  operator infix, `<mtd><mo form="infix">+</mo>...</mtd>`: first in its row it
  would otherwise be a tight prefix sign (+c).

---
title: Matrices, determinants, and systems
summary: Matrices in brackets, determinants, case analyses, augmented matrices, lines, and boxes.
tags:
  - mathml
  - authoring
see-also:
  - mathml/aligned
  - mathml/fences
  - mathml/attributes
examples:
  - name: matrix
    kind: source
    language: xml
    file: matrix.mml
    description: A 2 by 2 matrix in brackets and its determinant between vertical bars.
  - name: cases
    kind: source
    language: xml
    file: cases.mml
    description: The absolute value as a case analysis - an open brace and a left-aligned table.
  - name: augmented
    kind: source
    language: xml
    file: augmented.mml
    description: An augmented matrix of a linear system with a solid line before the right-hand side.
---

## Tables

- `mtable` holds rows (`mtr`), and each row holds cells (`mtd`); a cell is a row
  of its children. The table is centered on the math axis, so it lines up with
  the operators around it.
- Put the table in fences to make a matrix: `[` and `]`, `(` and `)`, or `|` for
  a determinant (see [[#matrix]]). The fences stretch to the table.
- A case analysis is an open brace and a table whose columns align left:
  `<mrow><mo>{</mo><mtable columnalign="left left">...</mtable></mrow>`, with
  the condition in an `mtext` (see [[#cases]]).

## Spacing, lines, and spans

- `columnalign` (left, center, right) and `rowalign` (top, bottom, center,
  baseline, axis) take one value per column or row, the last repeated; `mtr` and
  `mtd` take their own.
- `columnspacing="0.8em"` and `rowspacing="1ex"` set the gaps between the cells
  (one value, or one per gap).
- `columnlines` and `rowlines` draw lines between the columns or rows (`none`,
  `solid`, or `dashed` per gap): `columnlines="none none solid"` puts a bar
  before the last of four columns (see [[#augmented]]).
- `frame="solid"` draws a box around the table and `framespacing` sets its
  margin; a one-cell table with a frame boxes a result.
- `columnspan="2"` and `rowspan="2"` on an `mtd` join cells.

A boxed result:

```xml
<math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
  <mtable frame="solid">
    <mtr>
      <mtd><mi>E</mi><mo>=</mo><mi>m</mi><mo>&InvisibleTimes;</mo><msup><mi>c</mi><mn>2</mn></msup></mtd>
    </mtr>
  </mtable>
</math>
```

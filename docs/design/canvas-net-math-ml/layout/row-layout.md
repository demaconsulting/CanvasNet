### RowLayout

![Layout Structure](MathMlLayoutView.svg)

The `RowLayout` unit is a software unit of the `Layout` subsystem. Its source file is `RowLayout.cs`, which defines the
internal sealed class `RowLayout`.

#### Purpose

`RowLayout` lays out `mrow` and every element laid out like it (the inferred row of `math`, `mstyle`, `mphantom`,
`merror`, `semantics`, `maction`, and every element this version does not draw), stretches the stretchy operators of a
row, and builds the boxes that `mpadded` and `merror` put around a row. The unit's behavior is specified by the
`CanvasNetMathMl-Layout-RowLayout-*` requirements in `docs/reqstream/canvas-net-math-ml/layout/row-layout.yaml`.

#### Data Model

- `ErrorBorderWidth` - 1 CSS pixel; the border color of `merror` (CSS `red`).
- Per call: the style resolver, which resolves the length attributes of `mpadded`.

#### Key Methods

- `LayoutRow(children, isMath, relayout)` - `isMath` marks a row that is never an embellished operator (the inferred
  rows of `math` and `msqrt`); first, when a relayout function is given, `Stretch`; then MathML Core
  3.3.1.2: the children are placed left to right with their alphabetic baselines aligned; the ascent and descent are
  the maximums of the children's. When the row is itself an embellished operator (one embellished-operator child and
  any number of space-like children, MathML Core 3.2.4.1) and is not `math`, it adds no spacing inside and passes the
  operator to its parent; otherwise an embellished-operator child gets its leading space before it and its trailing
  space after it (the `lspace` and `rspace` that the token layout resolved from the operator dictionary). A row of
  space-like children is space-like (3.2.5.1). The italic correction of the last child becomes the row's (for the
  scripts of a later stage), but it is not added between children: MathML Core adds the italic correction of a slanted
  child before the next upright one, and Chrome, whose layout the reference tests compare with, does not.
- `Stretch(children, relayout)` - MathML Core 3.3.1.1: the children whose embellished operator is stretchy with a block
  stretch axis are laid out again by the relayout function (the engine) with a block stretch size constraint: the
  largest ascent and descent of the other children, or, when every child stretches, of the operators first laid out
  with the constraint (0, 0). A row that is an embellished operator with a constraint of its parent gets no relayout
  function: the engine passes the constraint to its operator instead.
- `LayoutPadded(element, style, inner)` - MathML Core 3.3.6: the requested `width`, `height`, and `depth` replace the
  inner box's (absent, invalid, or percentage values keep the inner box's; negative height and depth are clamped to
  zero), and the inner row is moved right by `lspace` (clamped to zero) and up by `voffset`; the operator and
  space-likeness of the inner row are kept.
- `LayoutError(inner)` - the user agent style of `merror`: the box grows by the border width on every side and a
  `BorderItem` of 1 px red covers it (its light yellow background comes from the style resolver).

#### Error Handling

N/A for errors of its own - invalid and percentage values of the `mpadded` attributes are reported by the style
resolver and ignored; the arithmetic is on finite, clamped lengths.

#### Dependencies

The `MathBox` and `StyleResolver` units, the `OperatorDictionary` unit (its `StretchAxis`), the `MathTree` unit of the
Syntax subsystem, and the core `Canvas` subsystem (`Rgba32`).

#### Callers

`LayoutEngine` lays out every row-like element, every element this version does not draw, `mpadded`, and `merror`
with it, and supplies the relayout function of the stretch.

### LayoutEngine

![Layout Structure](MathMlLayoutView.svg)

The `LayoutEngine` unit is a software unit of the `Layout` subsystem. Its source files are `LayoutEngine.cs` and
`LayoutEngine.Elements.cs`, which define the internal sealed partial class `LayoutEngine` (the second file holds the
walk of the fractions and radicals, their in-flow children, and the child-count rule).

#### Purpose

`LayoutEngine` lays an equation tree out into a tree of boxes: it walks the elements, computes their styles, dispatches
each to its layout, applies the child-count rule of MathML Core, decides what this version does not draw and reports
it, and keeps the layout bounded. The unit's
behavior is specified by the `CanvasNetMathMl-Layout-LayoutEngine-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/layout-engine.yaml`.

#### Data Model

- The element sets: the tokens (`mi`, `mn`, `mo`, `mtext`, `ms`, `mspace`); the rows (`math`, `mrow`, `mstyle`,
  `mphantom`, `merror`, `mpadded`); the MathML Core elements a later version draws (`msub`, `msup`, `msubsup`,
  `munder`, `mover`, `munderover`, `mmultiscripts`, `mprescripts`, `none`, `mtable`, `mtr`, `mtd`);
  the global attributes of MathML Core 2.1.3 (`id`, `class`, `style`, `dir`, the style attributes, `nonce`, `tabindex`,
  `autofocus`, `intent`, `arg`, with `data-*` and the event handlers); and the attributes of each drawn element beyond
  them (`display` and `alttext` of `math`; `linethickness` of `mfrac`, so that its legacy `bevelled`, `numalign`, and
  `denomalign` are reported; the operator attributes of `mo`; the lengths of `mspace` and `mpadded`; the attributes of
  `maction` and `semantics`).
- Per call: the limits, the report sink, the cancellation token, the `StyleResolver`, `TokenLayout`, `RowLayout`,
  `FractionLayout`, and `RadicalLayout` of the call, the work charged so far (`Work`), and the cached predictions of
  which elements are space-like and which are embellished operators.

#### Key Methods

- `Layout(root, rootStyle)` - lays out the `math` element with the style the document resolved for it and returns its
  box (an empty box of the element when it is hidden by `display: none`).
- `LayoutElement(element, style, depth, context)` - checks the token and the depth against `MaxNestingDepth`, charges
  one unit of work, and dispatches: a token is laid out by `TokenLayout.LayoutToken` with the operator context (its
  characters charged too); `semantics` and `maction` are laid out as rows of their first child only, as the user agent
  stylesheet of MathML Core hides the others; `mfrac` goes to `LayoutFraction`, `msqrt` and `mroot` to `LayoutRadical`;
  every other element is laid out as a row of its children (`LayoutRowLike`), and an element that is neither a row, a
  token, a fraction, nor a radical is reported first as `mathml-element-unsupported`. The attributes of a drawn element
  are checked, and the box is decorated with its element, its background (a background item over its content box, behind
  everything it paints), and the phantom flag of `mphantom`.
- `FlowChildren(element, style, depth, rules)` - resolves the style of each child element (with the user agent rule of
  its index among the element children, as the selectors of MathML Core count them, when the parent gives rules) and
  skips those with `display: none` and the annotations (still charged and depth-checked); text written directly inside
  the element is an in-flow child with the element's style.
- `LayoutFraction(element, style, depth, context)` - the in-flow children under `mfrac > * { math-depth: auto-add;
  math-style: compact }` (and `math-shift: compact` for the second); without exactly two, a row of them with the
  `mathml-child-count` warning at the element; otherwise the numerator (with the context of the parent when the
  fraction is an embellished operator) and the denominator are laid out and `FractionLayout` builds the box with the
  resolved `linethickness`, carrying the operator of an embellished fraction.
- `LayoutRadical(element, style, depth, context)` - `msqrt`: its children laid out as a row (`LayoutRowLike`, so its
  operators infer their forms, and its row is never an embellished operator) and `RadicalLayout.LayoutSquareRoot`;
  `mroot`: the base and the index under `mroot > :not(:first-child) { math-depth: add(2); math-style: compact }`, or a
  row with the `mathml-child-count` warning without exactly two in-flow children, then `RadicalLayout.LayoutRoot`.
- `LayoutRowLike(element, style, depth, context)` - lays out the in-flow children (`FlowChildren`); infers the form of
  each child that is not
  space-like from its position among those children (`OperatorDictionary.InferForm`); lays the children out
  (`LayoutChild`: text written directly inside the element is laid out with `TokenLayout.LayoutText` as an `mtext`,
  space-like); then lays out the row (`RowLayout.LayoutRow`) with a relayout function that lays a child out again with
  a block stretch size constraint, and around it the box of `mpadded` (`LayoutPadded`) or the border of `merror`
  (`LayoutError`). An element that is itself an embellished operator and gets a form or a constraint from its parent
  passes them on to its embellished child; with a constraint, it gives the row no relayout function (MathML Core
  3.3.1.1). The rows of `math` and `msqrt` are never embellished operators: they always space their operators.
- `IsSpaceLike` and `IsEmbellishedOperator` - predict from the tree, before the children are laid out, which children
  are space-like (`mtext`, `mspace`, and row-like elements of space-like children, MathML Core 3.2.5.1; never a fraction
  or a radical with valid children) and which are embellished operators (`mo`, an `mfrac` with two in-flow children
  whose first is one, and row-like elements of one embellished operator and space-like children, 3.2.4.1; never
  `msqrt`, nor an `mroot` with valid children); an `mfrac` or `mroot` with an invalid number of children is predicted
  as the row it becomes;
  cached per element, so the predictions cost linear work. An element hidden by a `display: none` declaration below the
  child is not seen; the boxes, built bottom-up, decide the spacing.
- `CheckAttributes` - reports, as `mathml-attribute-unsupported` at the attribute, an attribute that is neither global
  nor defined for the element, and `dir="rtl"` (the equation is laid out left to right).

#### Error Handling

A nesting deeper than `MaxNestingDepth` or work beyond `MaxLayoutWork` throws a `MathMlRenderException` with the code
`mathml-limit-exceeded`, located at the element and naming the property; a canceled token throws
`OperationCanceledException`. An operator laid out again to stretch is charged again, so the work of the stretch passes
is bounded by the same limit. Elements and attributes this version does not use are reported, never errors; a fraction
or a root without two in-flow children is a `mathml-child-count` warning (invalid markup, laid out as MathML Core lays
it out; strict mode does not throw for it). The walk is
recursive, and its depth is bounded by the nesting limit (at most 1,024), which the reader already enforced.

#### Dependencies

The `StyleResolver`, `TokenLayout`, `RowLayout`, `FractionLayout`, `RadicalLayout`, `MathBox`, `MathFont`, and
`OperatorDictionary` units, the `MathTree`
unit of the Syntax subsystem, and the root namespace's `MathMlLimits`, `MathMlReportSink`, `MathMlRenderException`, and
`MathMlDiagnosticCodes`.

#### Callers

`MathMlDocument` creates one engine for every measure and render call.

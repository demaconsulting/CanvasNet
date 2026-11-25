### LayoutEngine

![Layout Structure](MathMlLayoutView.svg)

The `LayoutEngine` unit is a software unit of the `Layout` subsystem. Its source file is `LayoutEngine.cs`, which
defines the internal sealed class `LayoutEngine`.

#### Purpose

`LayoutEngine` lays an equation tree out into a tree of boxes: it walks the elements, computes their styles, dispatches
each to its layout, decides what this version does not draw and reports it, and keeps the layout bounded. The unit's
behavior is specified by the `CanvasNetMathMl-Layout-LayoutEngine-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/layout-engine.yaml`.

#### Data Model

- The element sets: the tokens (`mi`, `mn`, `mo`, `mtext`, `ms`, `mspace`); the rows (`math`, `mrow`, `mstyle`,
  `mphantom`, `merror`, `mpadded`); the MathML Core elements a later version draws (`mfrac`, `msqrt`, `mroot`, `msub`,
  `msup`, `msubsup`, `munder`, `mover`, `munderover`, `mmultiscripts`, `mprescripts`, `none`, `mtable`, `mtr`, `mtd`);
  the global attributes of MathML Core 2.1.3 (`id`, `class`, `style`, `dir`, the style attributes, `nonce`, `tabindex`,
  `autofocus`, `intent`, `arg`, with `data-*` and the event handlers); and the attributes of each drawn element beyond
  them (`display` and `alttext` of `math`; the operator attributes of `mo`; the lengths of `mspace` and `mpadded`; the
  attributes of `maction` and `semantics`).
- Per call: the limits, the report sink, the cancellation token, the `StyleResolver`, `TokenLayout`, and `RowLayout` of
  the call, and the work charged so far (`Work`).

#### Key Methods

- `Layout(root, rootStyle)` - lays out the `math` element with the style the document resolved for it and returns its
  box (an empty box of the element when it is hidden by `display: none`).
- `LayoutElement(element, style, depth)` - checks the token and the depth against `MaxNestingDepth`, charges one unit of
  work, and dispatches: a token is laid out by `TokenLayout.LayoutToken` (its characters charged too); `semantics` and
  `maction` are laid out as rows of their first child only, as the user agent stylesheet of MathML Core hides the
  others; every other element is laid out as a row of its children (`LayoutRowLike`), and an element that is neither a
  row nor a token is reported first as `mathml-element-unsupported`. The attributes of a drawn element are checked, and
  the box is decorated with its element, its background (a background item over its content box, behind everything it
  paints), and the phantom flag of `mphantom`.
- `LayoutRowLike` - lays out the children (`LayoutChildren`), then the row (`RowLayout.LayoutRow`), and around it the
  box of `mpadded` (`LayoutPadded`) or the border of `merror` (`LayoutError`).
- `LayoutChildren` - resolves the style of each child element and skips those with `display: none` and the annotations;
  text written directly inside the element is laid out with `TokenLayout.LayoutText` as an `mtext` (space-like).
- `CheckAttributes` - reports, as `mathml-attribute-unsupported` at the attribute, an attribute that is neither global
  nor defined for the element, and `dir="rtl"` (the equation is laid out left to right).

#### Error Handling

A nesting deeper than `MaxNestingDepth` or work beyond `MaxLayoutWork` throws a `MathMlRenderException` with the code
`mathml-limit-exceeded`, located at the element and naming the property; a canceled token throws
`OperationCanceledException`. Elements and attributes this version does not use are reported, never errors. The walk is
recursive, and its depth is bounded by the nesting limit (at most 1,024), which the reader already enforced.

#### Dependencies

The `StyleResolver`, `TokenLayout`, `RowLayout`, `MathBox`, and `MathFont` units, the `MathTree` unit of the Syntax
subsystem, and the root namespace's `MathMlLimits`, `MathMlReportSink`, `MathMlRenderException`, and
`MathMlDiagnosticCodes`.

#### Callers

`MathMlDocument` creates one engine for every measure and render call.

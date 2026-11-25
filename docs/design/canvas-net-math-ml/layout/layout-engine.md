### LayoutEngine

![Layout Structure](MathMlLayoutView.svg)

The `LayoutEngine` unit is a software unit of the `Layout` subsystem. Its source files are `LayoutEngine.cs` and
`LayoutEngine.Elements.cs`, which define the internal sealed partial class `LayoutEngine` (the second file holds the
walk of the fractions, radicals, scripted elements, under- and overscript elements, and tables, their in-flow children,
and the child-count rules).

#### Purpose

`LayoutEngine` lays an equation tree out into a tree of boxes: it walks the elements, computes their styles, dispatches
each to its layout, applies the child-count rules of MathML Core, moves the limits of a movable operator to the script
positions outside display style, wraps stray table children in anonymous rows and cells, reports the elements outside
MathML Core and the attributes without effect, and keeps the layout bounded.
The unit's
behavior is specified by the `CanvasNetMathMl-Layout-LayoutEngine-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/layout-engine.yaml`.

#### Data Model

- The element sets: the tokens (`mi`, `mn`, `mo`, `mtext`, `ms`, `mspace`); the rows (`math`, `mrow`, `mstyle`,
  `mphantom`, `merror`, `mpadded`, and the empty `mprescripts` and `none`); the scripted elements (`msub`, `msup`,
  `msubsup`, `munder`, `mover`, `munderover`, `mmultiscripts`);
  the global attributes of MathML Core 2.1.3 (`id`, `class`, `style`, `dir`, the style attributes, `nonce`, `tabindex`,
  `autofocus`, `intent`, `arg`, with `data-*` and the event handlers); and the attributes of each drawn element beyond
  them (`display` and `alttext` of `math`; `linethickness` of `mfrac`, so that its legacy `bevelled`, `numalign`, and
  `denomalign` are reported; `accentunder` of `munder`, `accent` of `mover`, and both of `munderover`; the operator
  attributes of `mo`; the lengths of `mspace` and `mpadded`; the attributes of `maction` and `semantics`; the D2
  attributes `columnalign`, `rowalign`, `columnspacing`, `rowspacing`, `framespacing`, `columnlines`, `rowlines`, and
  `frame` of `mtable`, `columnalign` and `rowalign` of `mtr`, and `rowspan`, `columnspan`, `columnalign`, and
  `rowalign` of `mtd`, so that the other legacy table attributes are reported).
- Per call: the limits, the report sink, the cancellation token, the `StyleResolver`, `TokenLayout`, `RowLayout`,
  `FractionLayout`, `RadicalLayout`, `ScriptLayout`, `UnderOverLayout`, and `TableLayout` of the call, the work charged
  so far
  (`Work`), and the cached predictions of which elements are space-like and which are embellished operators.

#### Key Methods

- `Layout(root, rootStyle)` - lays out the `math` element with the style the document resolved for it and returns its
  box (an empty box of the element when it is hidden by `display: none`).
- `LayoutElement(element, style, depth, context)` - checks the token and the depth against `MaxNestingDepth`, charges
  one unit of work, and dispatches: a token is laid out by `TokenLayout.LayoutToken` with the operator context (its
  characters charged too); `semantics` and `maction` are laid out as rows of their first child only, as the user agent
  stylesheet of MathML Core hides the others; `mfrac` goes to `LayoutFraction`, `msqrt` and `mroot` to `LayoutRadical`,
  `msub`, `msup`, and `msubsup` to `LayoutScripted`, `munder`, `mover`, and `munderover` to `LayoutUnderOver`, and
  `mmultiscripts` to `LayoutMultiscripts`, and `mtable` to `LayoutTable`; an `mtr` or `mtd` reached here is outside a
  table and is laid out as a row of its children with a `mathml-child-count` warning; every other element is laid out
  as a row of its children (`LayoutRowLike`), and an element that none of these lays out (an element outside MathML
  Core) is reported first as `mathml-element-unsupported`. The attributes of a
  drawn element are checked, and the box is decorated with its element, its background (a background item over its
  content box, behind everything it paints), and the phantom flag of `mphantom`.
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
- `LayoutScripted(element, style, depth, context)` - the in-flow children under `StyleResolver.ScriptRule`; without
  two (three for `msubsup`), a row with the `mathml-child-count` warning; otherwise the base (with the context of the
  parent when the element is an embellished operator) and the scripts (with the postfix form, MathML Core 3.2.4.2) are
  laid out and `ScriptLayout.Layout` builds the box, carrying the operator of an embellished element.
- `LayoutUnderOver(element, style, depth, context)` - the in-flow children under `StyleResolver.ScriptRule`; without
  two (three for `munderover`), a row with the `mathml-child-count` warning. The `accent` and `accentunder` attributes
  (the ones the element defines) are accents when they are an ASCII case-insensitive match to `true`; another value
  than `true` or `false` is a `mathml-attribute-invalid` warning and false. The base and the postfix scripts are laid
  out; when the math style of the element is compact and its base is an embellished operator with `movablelimits`, the
  scripts are laid out by `ScriptLayout.Layout` (MathML Core 3.4.2.1); otherwise `UnderOverLayout.StretchInline` lays
  the children stretchy along the inline axis out again with an inline stretch size constraint (a base laid out with
  the constraint of the parent does not take part) and `UnderOverLayout.Layout` builds the box.
- `LayoutMultiscripts(element, style, depth, context)` - the in-flow children under `StyleResolver.ScriptRule`;
  without a base that is not `mprescripts`, with more than one `mprescripts`, or with an odd number of postscripts or
  prescripts, a row with a `mathml-child-count` warning naming the problem; otherwise the base, the postfix scripts in
  pairs, and the `mprescripts` box are laid out and `ScriptLayout.LayoutMultiscripts` builds the box.
- `LayoutTable(element, style, depth)` - the in-flow children of the `mtable` (no positional rule; the table itself is
  `math-style: compact` through `StyleResolver`): each `mtr` is checked like an element (bounds, attributes) and its
  in-flow children become cells, each `mtd` checked likewise and its content laid out as a row of its children
  (`LayoutRowLike`, with form inference); consecutive children of the table that are not `mtr` form one anonymous row,
  and consecutive children of an `mtr` (or of such an anonymous row) that are not `mtd` one anonymous cell, each run
  reported once as `mathml-child-count` at its first child; then `TableLayout.Layout` builds the box with a `charge`
  delegate that checks the cancellation token and charges the work budget (every grid slot).
- `LayoutRowLike(element, style, depth, context)` - lays out the in-flow children (`FlowChildren`); infers the form of
  each child that is not
  space-like from its position among those children (`OperatorDictionary.InferForm`); lays the children out
  (`LayoutChild`: text written directly inside the element is laid out with `TokenLayout.LayoutText` as an `mtext`,
  space-like); then lays out the row (`RowLayout.LayoutRow`) with a relayout function that lays a child out again with
  a block stretch size constraint, and around it the box of `mpadded` (`LayoutPadded`) or the border of `merror`
  (`LayoutError`). An element that is itself an embellished operator and gets a form or a constraint (block or inline)
  from its parent passes them on to its embellished child; with a constraint, it gives the row no relayout function
  (MathML Core 3.3.1.1). The rows of `math`, `msqrt`, and the table elements are never embellished operators: they
  always space their operators.
- `IsSpaceLike` and `IsEmbellishedOperator` - predict from the tree, before the children are laid out, which children
  are space-like (`mtext`, `mspace`, and row-like elements of space-like children, MathML Core 3.2.5.1; never a
  fraction, a radical, a table element, or a scripted element with valid children) and which are embellished operators
  (`mo`, an `mfrac` or a scripted element with valid children whose first is one, and row-like elements of one
  embellished operator and space-like children, 3.2.4.1; never `msqrt`, a table element, nor an `mroot` with valid
  children); an element with invalid children is
  predicted as the row it becomes; cached per element, so the predictions cost linear work. The predictions leave out
  the annotations and the children hidden by `display: none` in their inline `style` (read with the pure
  `StyleDeclarations.Parse`, which reports nothing), the children the layout leaves out, so a hidden child changes
  neither the form of an operator nor the count of the children; the boxes, built bottom-up, decide the spacing.
- `CheckAttributes` - reports, as `mathml-attribute-unsupported` at the attribute, an attribute that is neither global
  nor defined for the element, and `dir="rtl"` (the equation is laid out left to right).

#### Error Handling

A nesting deeper than `MaxNestingDepth` or work beyond `MaxLayoutWork` throws a `MathMlRenderException` with the code
`mathml-limit-exceeded`, located at the element and naming the property; a canceled token throws
`OperationCanceledException`. An operator laid out again to stretch is charged again, so the work of the stretch passes
is bounded by the same limit; a table charges every slot of its grid, and a grid with more slots than `MaxTableCells`
fails in `TableLayout` before it is allocated. Elements and attributes this version does not use are reported, never
errors; a fraction, a root, or a scripted element without the in-flow children MathML Core requires is a
`mathml-child-count` warning, as are a stray table child and an `mtr` or `mtd` outside a table (invalid markup, laid out
as MathML Core lays it out; strict mode does not throw for it), and an invalid `accent` or `accentunder` a
`mathml-attribute-invalid` warning. The walk is recursive, and its depth is bounded by the nesting limit (at most
1,024), which the reader already enforced.

#### Dependencies

The `StyleResolver`, `TokenLayout`, `RowLayout`, `FractionLayout`, `RadicalLayout`, `ScriptLayout`, `UnderOverLayout`,
`TableLayout`, `MathBox`, `MathFont`, and `OperatorDictionary` units, the `MathTree`
unit of the Syntax subsystem, and the root namespace's `MathMlLimits`, `MathMlReportSink`, `MathMlRenderException`, and
`MathMlDiagnosticCodes`.

#### Callers

`MathMlDocument` creates one engine for every measure and render call.

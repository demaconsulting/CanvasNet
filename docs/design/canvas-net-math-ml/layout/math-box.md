### MathBox

![Layout Structure](MathMlLayoutView.svg)

The `MathBox` unit is a data-model unit of the `Layout` subsystem. Its source file is `MathBox.cs`, which defines the
record struct `MathRect`, the interface `IPaintItem` with the records `GlyphItem`, `RuleItem`, `BackgroundItem`, and
`BorderItem`, the record `EmbellishedOperator`, the record structs `BlockStretchConstraint`, `OperatorContext`, and
`PlacedBox`, and the class `MathBox`.

#### Purpose

`MathBox` is the result of the layout and the input of the painter: the laid-out box of each element with its metrics,
its placed children, and what it paints. The unit's behavior is specified by the `CanvasNetMathMl-Layout-MathBox-*`
requirements in `docs/reqstream/canvas-net-math-ml/layout/math-box.yaml`.

#### Data Model

- Coordinates - CSS pixels at the font size of the call, relative to the origin of a box: the left edge of its content
  box on its alphabetic baseline, with y growing downward.
- `MathRect(X, Y, Width, Height)` with `Right`, `Bottom`, `Union`, and `Offset`.
- `IPaintItem.Bounds` - what an item paints, relative to its box, or null when it paints nothing.
- `GlyphItem(Font, Glyph, Size, X, Baseline, Color, Ink)` - a glyph at its pen position, with its ink bounds (no bounds
  when the ink is empty); `RuleItem(Rect, Color)` - a filled rectangle of the content (a stretched line character, a
  fraction bar, the overbar of a radical); `BackgroundItem(Rect, Color)` - painted before every glyph;
  `BorderItem(Rect, Width, Color)` - painted after every glyph.
- `EmbellishedOperator(LeadingSpace, TrailingSpace)` - the properties of an embellished operator (MathML Core 3.2.4.2)
  resolved by the token layout: what a row adds before and after it, and `CoreText`, `Form`, `Category`, `Stretchy`,
  `Symmetric`, `LargeOp`, `MovableLimits`, `Fence`, `Separator`, `Axis`, `MinSize`, and `MaxSize` (infinity when there
  is none). Rows read the spacing and whether to stretch; the under- and overscript layouts read `LargeOp`, `Stretchy`
  with `Axis`, and `MovableLimits`.
- `BlockStretchConstraint(Ascent, Descent)` - the block stretch size constraint that a row gives its stretchy operators
  (MathML Core 3.3.1.1).
- `OperatorContext(Form, Stretch, InlineStretch)` - what the parent of an element tells the layout of an embellished
  operator: the form inferred from its position, the block constraint when a row stretches it, and the inline stretch
  size constraint (a width) when an under- or overscript element stretches it (MathML Core 3.4.2.2); `HasStretch`
  tells whether either constraint is given.
- `PlacedBox(Box, X, Y)` - a child box at its offset.
- `MathBox(width, ascent, descent, children, items)` - the inline size, line-ascent, and line-descent; the placed
  children and the box's own paint items; `ItalicCorrection`, `TopAccentAttachment`, `Operator`, `IsSpaceLike`
  (MathML Core 3.2.5.1), `Element`, and `IsPhantom`. Boxes are built bottom-up and never changed, so a finished tree is
  safe for concurrent reads.

#### Key Methods

- `ContentRect` - `[0, Width]` by `[-Ascent, Descent]`, with negative extents clamped to zero.
- `InkBounds()` - the union of the bounds of the box's items and of the ink of its children at their offsets; null for
  a phantom box and for a box that paints nothing.
- `LeadingInk()` - the ink and the box of the token at the start of a box (following the first child down through
  rows and scripted elements), relative to the box; null at a phantom, at a box that paints items around its children
  (a radical, a fraction, a table, a background), or at a token that paints nothing.
- `Clearance(previousInk, previousItalic, next, x, y)` - how far the next box must move right so that its leading ink
  does not overlap the ink of the token before it: nothing when the inks stay apart, or when the next box does not
  start before its origin and the token before is not slanted or the next box starts with a slanted token; otherwise
  the overlap, and at least the italic correction of a slanted token before a box that is not slanted (MathML Core
  3.3.1.2 and 3.4.1.3). `RowLayout` and `ScriptLayout` use it so that glyphs never collide.
- `TrailingInkBounds()` - the ink of the token at the end of a box (following the last child down), relative to the box,
  with the same stops as `LeadingInk()`.
- `ScriptClearance(baseInk, baseItalic, sup, x, y)` - how far a superscript must move right so that its leading ink does
  not overlap the ink at the end of its base, whatever the two glyphs: nothing when the inks stay apart, otherwise the
  overlap and at least the italic correction of the base. `ScriptLayout` uses it.
- `MathRect.Union` and `Offset` - the smallest rectangle around two, and a rectangle moved.

#### Error Handling

N/A - the types hold values computed by the layout units; they perform no parsing and raise no errors of their own.

#### Dependencies

The `MathTree` unit of the Syntax subsystem (`MathElement`), the `OperatorDictionary` unit (`OperatorForm`,
`OperatorCategory`, `StretchAxis`), the core `Fonts` subsystem (`TrueTypeFont` of a glyph), and the core `Canvas`
subsystem (`Rgba32`).

#### Callers

`TokenLayout`, `RowLayout`, `FractionLayout`, `RadicalLayout`, `ScriptLayout`, `UnderOverLayout`, `TableLayout`, and
`LayoutEngine` build boxes (and `LayoutEngine` passes operator contexts); `StretchyGlyphs` measures with `MathRect`;
`MathMlDocument` frames the picture from the content box and the ink of the `math` box; `MathMlPainter` paints the
items.

### MathBox

![Layout Structure](MathMlLayoutView.svg)

The `MathBox` unit is a data-model unit of the `Layout` subsystem. Its source file is `MathBox.cs`, which defines the
record struct `MathRect`, the interface `IPaintItem` with the records `GlyphItem`, `RuleItem`, `BackgroundItem`, and
`BorderItem`, the record `EmbellishedOperator`, the record struct `PlacedBox`, and the class `MathBox`.

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
  when the ink is empty); `RuleItem(Rect, Color)` - a filled rectangle of the content (for the bars of a later stage);
  `BackgroundItem(Rect, Color)` - painted before every glyph; `BorderItem(Rect, Width, Color)` - painted after every
  glyph.
- `EmbellishedOperator(LeadingSpace, TrailingSpace)` - what a row adds before and after an operator (MathML Core
  3.2.4.1).
- `PlacedBox(Box, X, Y)` - a child box at its offset.
- `MathBox(width, ascent, descent, children, items)` - the inline size, line-ascent, and line-descent; the placed
  children and the box's own paint items; `ItalicCorrection`, `TopAccentAttachment`, `Operator`, `IsSpaceLike`
  (MathML Core 3.2.5.1), `Element`, and `IsPhantom`. Boxes are built bottom-up and never changed, so a finished tree is
  safe for concurrent reads.

#### Key Methods

- `ContentRect` - `[0, Width]` by `[-Ascent, Descent]`, with negative extents clamped to zero.
- `InkBounds()` - the union of the bounds of the box's items and of the ink of its children at their offsets; null for
  a phantom box and for a box that paints nothing.
- `MathRect.Union` and `Offset` - the smallest rectangle around two, and a rectangle moved.

#### Error Handling

N/A - the types hold values computed by the layout units; they perform no parsing and raise no errors of their own.

#### Dependencies

The `MathTree` unit of the Syntax subsystem (`MathElement`), the core `Fonts` subsystem (`TrueTypeFont` of a glyph), and
the core `Canvas` subsystem (`Rgba32`).

#### Callers

`TokenLayout`, `RowLayout`, and `LayoutEngine` build boxes; `MathMlDocument` frames the picture from the content box and
the ink of the `math` box; `MathMlPainter` paints the items.

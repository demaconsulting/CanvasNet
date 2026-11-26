## MathMlPainter

![CanvasNetMathMl Structure](CanvasNetMathMlView.svg)

The `MathMlPainter` unit is a direct unit of CanvasNetMathMl in the root namespace `DemaConsulting.CanvasNet.MathMl`.
Its source file is `MathMlPainter.cs`, which defines the record struct `MathPlacement` and the internal static class
`MathMlPainter`.

### Purpose

`MathMlPainter` is the only part of the package that produces pixels: it draws a laid-out equation (a `MathBox` tree)
onto a surface, antialiased and deterministic, bounded in the glyphs it paints and stoppable by the caller. The unit's
behavior is specified by the `CanvasNetMathMl-MathMlPainter-*` requirements in
`docs/reqstream/canvas-net-math-ml/math-ml-painter.yaml`.

### Data Model

- `MathPlacement(Scale, OriginX, OriginY)` - device = CSS pixel × scale + origin; the origin is the device position of
  the left edge of the `math` box on its baseline.
- `CancellationInterval` - 64: the number of items painted between two checks of the cancellation token.
- The class is stateless; each call collects a list of `(item, x, y, location)` entries (the paint item, the origin of
  its box relative to the `math` origin, and the location of its nearest element).

### Key Methods

- `Paint(surface, root, placement, background, maxPaintedGlyphs, token)` - checks the token; collects the items of the
  visible boxes in tree order; counts the glyph items and throws before painting anything when they exceed the limit;
  clears the surface to the background when one is given; then paints in four passes, each in tree order: the
  background items, the glyph items, the rule items, and the border items, checking the token every 64 items.
- `Collect(root)` - walks the box tree with an explicit stack (children pushed in reverse to keep tree order), skips
  phantom subtrees, and accumulates the offsets of the placed children; the depth of the tree never threatens the stack.
- `PaintGlyph` - takes the outline of the glyph from its font (design units, y up), scales it by size / units per em ×
  placement scale, flips it, moves it to its pen position on its baseline, and fills it with the nonzero rule.
- `PaintBorder` - fills the top and bottom bands and the left and right bands between them, each as wide as the border
  (at most half the smaller side), leaving the inside untouched.
- `FillRect` - fills a rectangle of the layout, moved and scaled to device space; an empty rectangle is skipped.

### Error Handling

More glyphs than `maxPaintedGlyphs` throws a `MathMlRenderException` with the code `mathml-limit-exceeded`, located at
the element of the first glyph beyond the limit and naming `MathMlLimits.MaxPaintedGlyphs`, before any pixel is painted;
a canceled token throws `OperationCanceledException` (before anything is painted when it is already canceled). Null
arguments throw `ArgumentNullException`. The caller disposes the surface on failure.

### Dependencies

The `MathBox` unit (the boxes and paint items) and the `MathTree` unit (the locations of the elements) of the Layout and
Syntax subsystems, and the core `Canvas` (`Surface`, `Rgba32`), `Drawing` (`PathFiller`), `Geometry` (`Path`), and
`Fonts` (`TrueTypeFont.GetGlyphOutline`) subsystems.

### Callers

`MathMlDocument` paints the boxes of every render with it.

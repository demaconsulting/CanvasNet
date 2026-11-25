### RadicalLayout

![Layout Structure](MathMlLayoutView.svg)

The `RadicalLayout` unit is a software unit of the `Layout` subsystem. Its source file is `RadicalLayout.cs`, which
defines the internal sealed class `RadicalLayout`.

#### Purpose

`RadicalLayout` lays out `msqrt` and `mroot` as MathML Core 3.3.3 does: the radical symbol stretched over its base, the
overbar, and the index of a root, from the boxes that the layout engine laid out (the row of the children of `msqrt`;
the base and the index of `mroot`). The unit's behavior is specified by the `CanvasNetMathMl-Layout-RadicalLayout-*`
requirements in `docs/reqstream/canvas-net-math-ml/layout/radical-layout.yaml`.

#### Data Model

- `RadicalSymbol` - U+221A SQUARE ROOT, the symbol of every radical.
- The layout unit of Chrome (1/64 CSS pixel), to which the raise percentage of an index is truncated.
- Per call: the math font (the radical constants and the constructions of the symbol) and the limits of the call
  (`MaxAssemblyParts`).

#### Key Methods

- `LayoutSquareRoot(radicand, style, location)` - with the gap `RadicalVerticalGap` (`RadicalDisplayStyleVerticalGap`
  in display style), the overbar thickness `RadicalRuleThickness`, and the extra ascender `RadicalExtraAscender`:
  - the symbol is stretched by `StretchyGlyphs.Stretch` along the block axis to the thickness + the gap + the height of
    the base (its base glyph, a size variant, or its glyph assembly; its base glyph when nothing stretches it);
  - the ascent is the base's ascent + the gap + the thickness + the extra ascender; the descent is the larger of the
    base's descent and the height of the symbol + the extra ascender − the ascent (3.3.3.2);
  - the symbol is painted at the left with its top the extra ascender below the top of the box; the base follows the
    symbol, and a `RuleItem` overbar of the thickness covers the base with its top at the top of the symbol, both in
    the color of the radical;
  - as Chrome lays it out, the inline size of the symbol is its advance (a glyph or a size variant) or the ink width of
    an assembly rounded out to whole pixels, and the box is as wide as the base plus the preferred inline size of the
    symbol (`StretchyGlyphs.PreferredInlineSize`, its widest construction).
- `LayoutRoot(radicand, index, style, location)` - the square root of the base, after the index (3.3.3.3): the index
  starts after `RadicalKernBeforeDegree` (not negative) and the square root follows the index after
  `RadicalKernAfterDegree` (not less than minus the width of the index; negative in Noto Sans Math, so the symbol slides
  under the index); the bottom of the index is raised from the bottom of the square root by
  `RadicalDegreeBottomRaisePercent` (at Chrome's 1/64 precision) of the height of the symbol; the ascent grows to the
  top of the index, and the descent follows Chrome's formula (the larger of the square root's descent and its descent −
  the raise + the height of the index).

#### Error Handling

A symbol whose assembly needs more glyphs than `MaxAssemblyParts` throws, through `StretchyGlyphs`, a
`MathMlRenderException` with the code `mathml-limit-exceeded` located at the radical, before any glyph is placed.

#### Dependencies

The `MathFont` unit (the radical constants and the glyph metrics), the `StretchyGlyphs` unit (the stretched symbol and
its preferred inline size), the `MathBox` unit (boxes, `GlyphItem`, `RuleItem`), and the root namespace's
`MathMlLimits`, `MathMlLocation`, and `MathMlRenderException`.

#### Callers

`LayoutEngine` lays out every `msqrt` (after laying out its children as a row) and every `mroot` with two in-flow
children (after laying out its base and its index under the user agent rule of its position) with it.

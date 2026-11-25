### TokenLayout

![Layout Structure](MathMlLayoutView.svg)

The `TokenLayout` unit is a software unit of the `Layout` subsystem. Its source files are `TokenLayout.cs`, which
defines the internal sealed class `TokenLayout`, `MathVariants.cs`, which defines the enum `MathVariant` and the static
partial class `MathVariants`, and the generated `MathVariants.Data.cs`, which holds the italic and `mathvariant`
mapping pairs.

#### Purpose

`TokenLayout` lays out the token elements (`mi`, `mn`, `mo`, `mtext`, `ms`, `mspace`) and text written directly inside a
layout element: the transformed characters, their glyphs, the box of the token, the properties of an operator, and
stretched and large operators. The unit's behavior is specified by the `CanvasNetMathMl-Layout-TokenLayout-*`
requirements in `docs/reqstream/canvas-net-math-ml/layout/token-layout.yaml`.

#### Data Model

- `MathVariant` - the 18 `mathvariant` values of MathML 3: `Normal`, `Bold`, `Italic`, `BoldItalic`, `DoubleStruck`,
  `BoldFraktur`, `Script`, `BoldScript`, `Fraktur`, `SansSerif`, `BoldSansSerif`, `SansSerifItalic`,
  `SansSerifBoldItalic`, `Monospace`, `Initial`, `Tailed`, `Looped`, and `Stretched`.
- The tables (built once, frozen) - the value names (ASCII case-insensitive); the automatic italic table of MathML Core
  Appendix C.1 (Latin letters, the dotless i and j, the Greek letters, nabla, and the partial differential); and the
  table of the other values by value and base code point, generated from the `<font>` decompositions of UnicodeData.txt
  (a Mathematical Alphanumeric Symbol wins over a Letterlike Symbol of the same style and base, so the letterlike
  symbols fill the holes, such as U+2102 for the double-struck C and U+210E for the italic h).
- Per call: the math font, the style resolver, the report sink, and the limits (`MaxAssemblyParts`).

#### Key Methods

- `LayoutToken(element, style, context)` - `mspace` is a blank box; `ms` draws its text as written, without quotes; an
  `mo` is laid out by `LayoutOperator` with the form and the stretch constraint of the context; every other token lays
  out its text content (`LayoutText`) with the automatic italic for an `mi` of a single character; `mtext` and `mspace`
  are space-like.
- `LayoutOperator(element, style, context)` - MathML Core 3.2.4: the text is laid out as an `mtext`, then the properties
  are resolved (`ResolveOperator`): the form is the valid `form` attribute, else the form of the context, else infix;
  the dictionary gives the category of the text and form (`OperatorDictionary.Lookup`, with the fallback to the other
  forms when the form was inferred); `lspace` and `rspace` resolve against the style with a percentage of the
  dictionary value; `stretchy`, `symmetric`, `largeop`, `movablelimits`, `fence`, and `separator` are booleans that
  override the dictionary; `minsize` (default 100%) and `maxsize` (default infinity) resolve with a percentage of the
  unstretched height. An operator of one character then:
  - with the `stretchy` property, a block stretch axis, and a constraint (`StretchBlock`), gets the target of MathML
    Core 3.2.4.3: the ascent and descent of the constraint, made symmetric about the math axis (`AxisHeight`) when the
    operator is `symmetric`, then scaled about the axis to `minsize` when smaller and to `maxsize` when larger (an empty
    target becomes `minsize` centered on the axis). `StretchyGlyphs.Stretch` gives the glyph, variant, assembly, or
    rule; it is shifted vertically so that its center is the center of the target, and centered horizontally in a box of
    the preferred inline size (the widest construction), as Chrome draws it. A character that cannot stretch keeps its
    glyph, centered on the target as Chrome centers it, and is reported when the glyph is shorter than the target;
  - with the `largeop` property in display style (`LargeOperator`), gets `StretchyGlyphs.LargeVariant` for
    `DisplayOperatorMinHeight`, centered on the math axis as Chrome centers it, and keeps its glyph when there is no
    variant;
  - otherwise keeps the `mtext` layout.

  The box is an embellished operator (`EmbellishedOperator` with every resolved property) and is not space-like.
- `LayoutText(text, style, autoItalic, location)` - transforms the characters (`Transform`), resolves each with the math
  font, and places the glyphs by their advances (no kerning, no glyph substitution): an invisible format character (the
  invisible operators U+2061 to U+2064) takes no space and draws nothing; a white-space character advances by its width;
  a character that no bundled font has is drawn as the missing-glyph box of the math font and reported. The box is the
  sum of the advances wide, and its ascent and descent are those of the ink of the glyphs rounded out to whole CSS
  pixels (with a tolerance of 1e-6), also for a text with a space, as Chrome lays tokens out. A single glyph gives the
  box its italic correction and top accent attachment from the `MATH` table.
- `Transform(text, variant, autoItalic)` - maps each code point by the `mathvariant` in effect, or to its mathematical
  italic when the automatic italic applies and no `mathvariant` is set; `mathvariant="normal"` cancels it.
- `MathVariants.TryParse`, `Map`, and `AutoItalic` - the lookups; a character without a styled form is unchanged.
- `LayoutSpace` - the `width`, `height`, and `depth` of `mspace` resolved by the style resolver; a negative depth or
  height is clamped to zero, a negative width is ignored with a `mathml-attribute-invalid` warning, and a percentage is
  ignored and reported.

#### Error Handling

A character that no bundled font has is a `mathml-text-missing-glyph` warning located at the text, naming the code point
(`U+E000`), and the missing-glyph box is drawn. A stretchy operator that nothing can stretch to a target taller than its
glyph is a `mathml-stretch-unavailable` warning located at the `mo`. An invalid `form` or boolean attribute of an `mo`
is a `mathml-attribute-invalid` warning and is ignored; other invalid and unsupported attribute values are reported
through the style resolver and ignored. An assembly that needs more glyphs than `MathMlLimits.MaxAssemblyParts` throws
the located `mathml-limit-exceeded` render exception of `StretchyGlyphs`; nothing else throws for the content of a
token.

#### Dependencies

The `MathFont`, `MathBox`, `StyleResolver`, `OperatorDictionary`, and `StretchyGlyphs` units, the `MathTree` and
`AttributeValues` units of the Syntax subsystem, the root namespace's `MathMlReportSink`, `MathMlLimits`, and
`MathMlDiagnosticCodes`, and the core `Fonts` subsystem (`TrueTypeFont`, `GlyphFallback`).

#### Callers

`LayoutEngine` lays out every token (an `mo` with the form and stretch constraint of its row) and every run of text
directly inside a layout element with it; `StyleResolver` parses `mathvariant` with `MathVariants`.

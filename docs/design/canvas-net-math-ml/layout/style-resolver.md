### StyleResolver

![Layout Structure](MathMlLayoutView.svg)

The `StyleResolver` unit is a software unit of the `Layout` subsystem. Its source file is `StyleResolver.cs`, which
defines the internal sealed record `MathStyle`, the internal enum `MathDepthRule`, the internal record struct
`ChildStyleRule`, and the internal sealed class `StyleResolver`.

#### Purpose

`StyleResolver` computes the style of each element from its parent's, as MathML Core expresses it with CSS properties
(Appendix A, the user agent stylesheet, and the presentational hints of 2.1): the math style, the math depth, the font
size, the colors, the `mathvariant`, the math shift, and the supported subset of the inline `style` attribute, with the
user agent rules that a fraction, a root, or a scripted element applies to its children by position. It also resolves
the length attributes
of the layouts. The unit's behavior is specified by the `CanvasNetMathMl-Layout-StyleResolver-*`
requirements in `docs/reqstream/canvas-net-math-ml/layout/style-resolver.yaml`.

#### Data Model

- `MathStyle(FontSize, DisplayStyle, MathDepth, Color, Variant)` - inherited: the font size in CSS pixels, the math
  style (true for normal, false for compact), the math depth, the text color, and the `mathvariant` (null for none);
  not inherited: `Background` (null for none) and `DisplayNone`; inherited and set with an initializer so that the
  positional constructor stays: `MathShiftCompact` (true for `math-shift: compact`, which the superscripts read for
  their cramped shift). The record is immutable.
- `MathDepthRule` - `None`, `AutoAdd` (one more level only under a compact parent), and `Add` (a number of levels).
- `ChildStyleRule(Compact, Depth, DepthAdd, MathShiftCompact, FontSizeInherit)` - a user agent rule of MathML Core
  Appendix A for a child position: `math-style: compact`, the `math-depth` rule, the `math-shift` (null to inherit), and
  `font-size: inherit` (for the accents); with the presets `FractionNumerator` (`auto-add`, compact),
  `FractionDenominator` (the same with `math-shift: compact`), `RootIndex` (`add(2)`, compact), `Script` (`add(1)`,
  compact), and `AccentBase` (`math-shift: compact` only).
- `MaxMathDepth` - 64, the largest magnitude of the math depth; `ScaleFactorPerLevel` - 0.71 (MathML Core 4.5); the
  light yellow background of `merror` (255, 255, 224).
- Per call: the math font (for the scale-down constants and the x-height) and the report sink.

#### Key Methods

- `TryParseDisplay(value, out display)` - `block` or `inline` (ASCII case-insensitive and exact: whitespace around the
  value makes it invalid, as MathML Core matches `math[display="block" i]`).
- `ScriptRule(element, index)` - the user agent rule of the child of a scripted element at an index among the element
  children (MathML Core 3.4.4 and Appendix A): none for the base, except `AccentBase` for the base of an `mover` or
  `munderover` with an accent; `Script` for every other child, with `math-shift: compact` for the subscript of `msub`
  and `msubsup` and for the children of `mmultiscripts` at even positions (counting from 1) before `mprescripts` and at
  odd positions after it (the others inherit), and with `font-size: inherit` for an accent script (the second child of
  an `munder` or `munderover` with `accentunder`, the second of an `mover` and the third of an `munderover` with
  `accent`). `IsTrue(element, name)` tells whether an attribute is an ASCII case-insensitive match to `true`, as the
  attribute selectors `[accent="true" i]` match.
- `ResolveRoot(math, fontSize, color, displayOverride)` - the style of the `math` element: the font size and color of
  the options, the math style from the override or the `display` attribute (normal for block, compact for inline; an
  invalid `display` is a warning), a math depth reset to zero, and `math-shift: normal`; then the attributes and the
  inline style as for any element, except that the font size is inherited instead of scaled.
- `Resolve(element, parent)` and `ResolveChild(child, parent, rule)` - in the order of the CSS cascade: the user agent
  rules (the parent's inherited values; the rule of the child's position, when given: the math style, the math depth
  from the parent's math style and depth, the math shift, and an inherited font size; the light yellow background of
  `merror`; `math-shift: compact` for `msqrt` and `mroot`; `math-style: compact` for `mtable`); the presentational
  hints, which replace the user agent
  values (`displaystyle`; `scriptlevel` setting the depth or changing the depth of the parent, so `scriptlevel="+1"` on
  a numerator is one level more than the fraction's, as `math-depth: add(1)` computes from the inherited depth;
  `mathsize` as a length or a percentage of the inherited size, not negative; `mathcolor`; `mathbackground`;
  `mathvariant` through `MathVariants.TryParse`); and the inline style declarations (`color`, `background-color`,
  `math-style`, `display: none`, `font-size` resolved against the inherited size, `math-depth` with `auto-add` adding
  one only under the compact style, and `font-style` setting the variant to italic or normal). The depth is clamped to
  plus or minus 64, and unless a size was set the font size is the inherited size times
  `ScaleFactor(parent depth, depth)`.
- `ScaleFactor(from, to)` - the product of the per-level factors between two depths (MathML Core 4.5,
  `font-size: math`): the `ScriptPercentScaleDown` constant (60 % in Noto Sans Math) from level 0 to 1, the
  `ScriptScriptPercentScaleDown` constant (50 % overall) from 1 to 2, and 0.71 per further level; the inverse when the
  depth decreases.
- `ResolveLength(element, name, style, percentBase)` - parses a length attribute and resolves it against the font size
  of the style, its x-height, and a percentage base; null when the attribute is absent, invalid (a warning), or a
  percentage where the layout has no base (reported as `mathml-attribute-unsupported`, as MathML Core ignores it).

#### Error Handling

An invalid attribute value is a `mathml-attribute-invalid` warning located at the attribute, with the expected form as
a hint, and is ignored; an inline declaration outside the supported subset is a `mathml-css-unsupported` report and an
invalid value of a supported one is a warning, both located at the `style` attribute. The clamping of the math depth and
of the lengths keeps every font size finite; nothing throws.

#### Dependencies

The `MathFont` unit (the scale-down constants and the x-height), the `TokenLayout` unit (`MathVariants.TryParse`), the
`MathTree` and `AttributeValues` units of the Syntax subsystem, the root namespace's `MathMlReportSink`,
`MathMlDisplay`, and `MathMlDiagnosticCodes`, and the core `Canvas` subsystem (`Rgba32`).

#### Callers

`LayoutEngine` resolves the style of every element with it (the children of `mfrac` and `mroot` with `ResolveChild`),
and `MathMlDocument` resolves the style of the `math` element through the engine; `TokenLayout`, `RowLayout`, and
`FractionLayout` resolve the length attributes of `mspace`, `mo`, `mpadded`, and `mfrac` with it; `TokenLayout`,
`FractionLayout`, `RadicalLayout`, `ScriptLayout`, `UnderOverLayout`, and `TableLayout` take the `MathStyle` of the
element they lay out (its font size, math style, math shift, and color).

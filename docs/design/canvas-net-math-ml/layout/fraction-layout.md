### FractionLayout

![Layout Structure](MathMlLayoutView.svg)

The `FractionLayout` unit is a software unit of the `Layout` subsystem. Its source file is `FractionLayout.cs`, which
defines the internal sealed class `FractionLayout`.

#### Purpose

`FractionLayout` lays out `mfrac` as MathML Core 3.3.2 does: the numerator over the denominator, with a fraction bar on
the math axis or, for a zero line thickness, as a stack, from the boxes of the two children that the layout engine laid
out. It also resolves the `linethickness` attribute. The unit's behavior is specified by the
`CanvasNetMathMl-Layout-FractionLayout-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/fraction-layout.yaml`.

#### Data Model

- `InlinePadding` - 1 CSS pixel, the `padding-inline` of `mfrac` in the user agent stylesheet of MathML Core.
- Per call: the math font (the constants of its `MATH` table) and the style resolver of the call (the `linethickness`
  length and its report).

#### Key Methods

- `ResolveThickness(fraction, style)` - the default is `FractionRuleThickness` at the font size of the fraction;
  `linethickness` is resolved by `StyleResolver.ResolveLength` with the default as the base of a percentage; an absent
  value keeps the default, an invalid one (including the MathML 3 names `thin`, `medium`, and `thick`, which MathML Core
  does not define) keeps it with a `mathml-attribute-invalid` warning, and a negative value is zero (Chrome draws no bar
  for it).
- `Layout(numerator, denominator, style, thickness, embellished)` - with t the thickness, the constants in their
  display-style variants when the math style of the fraction is normal:
  - **Bar (t > 0, 3.3.2.1)** - the numerator shift is the larger of `FractionNumeratorShiftUp` and `AxisHeight` + t/2 +
    `FractionNumeratorGapMin` + the numerator's descent; the denominator shift is the larger of
    `FractionDenominatorShiftDown` and t/2 + `FractionDenominatorGapMin` + the denominator's ascent − `AxisHeight`. The
    ascent is the largest of the numerator shift + its ascent, the denominator's ascent − its shift, and `AxisHeight` +
    t/2; the descent is the largest of 0, the numerator's descent − its shift, the denominator shift + its descent,
    and t/2 − `AxisHeight` (the 0 of MathML Core, as Chrome: a denominator wholly above the baseline does not raise the
    bottom of the box). A `RuleItem` of height t centered on the math axis spans the content box in the color of the
    fraction.
  - **Stack (t = 0, 3.3.2.2)** - the shifts are `StackTopShiftUp` and `StackBottomShiftDown`; when the gap between the
    bottom of the numerator and the top of the denominator is less than `StackGapMin`, both shifts grow by half of the
    missing gap. No bar. The ascent and the descent are as with a bar, without the bar terms, the descent at least 0.
  - **Box** - the content box is as wide as the wider child and each child is centered in it; the box adds 1 px of
    padding on each side, so the children and the bar move right by 1 px. The ascent and the descent of a child are
    those of its box: a token box is its ink, as Chrome lays tokens out, which is what the "ink" metrics of the MathML
    Core formulas become. The operator of an embellished fraction (given by the engine) is kept on the box.

#### Error Handling

N/A for errors of its own - an invalid `linethickness` is reported by the style resolver and replaced by the default;
the arithmetic is on finite, clamped lengths.

#### Dependencies

The `MathFont` unit (the fraction, stack, and axis constants), the `StyleResolver` unit (`ResolveLength`), the `MathBox`
unit (boxes, `RuleItem`, `EmbellishedOperator`), and the `MathTree` unit of the Syntax subsystem (the `mfrac` element).

#### Callers

`LayoutEngine` lays every `mfrac` with two in-flow children out with it, after laying out its numerator and
denominator under the user agent rules of their positions.

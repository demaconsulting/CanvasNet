### UnderOverLayout

![Layout Structure](MathMlLayoutView.svg)

The `UnderOverLayout` unit is a software unit of the `Layout` subsystem. Its source file is `UnderOverLayout.cs`, which
defines the internal sealed class `UnderOverLayout`.

#### Purpose

`UnderOverLayout` lays out `munder`, `mover`, and `munderover` as MathML Core 3.4.2.3-3.4.2.5 do (limits, accents, the
stack of a horizontally stretched operator, and other scripts), from the boxes of the base and scripts that the layout
engine laid out, and stretches the operators of such an element along the inline axis (3.4.2.2). The unit's behavior is
specified by the `CanvasNetMathMl-Layout-UnderOverLayout-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/under-over-layout.yaml`.

#### Data Model

Per instance: the math font (the constants of its `MATH` table). The class holds no other state.

#### Key Methods

- `StretchInline(children, baseIsFixed, relayout)` - the children laid out without an inline constraint are split into
  the embellished operators stretchy along the inline axis (`Operator { Stretchy: true, Axis: Inline }`) and the others;
  a base laid out with the constraint of its parent (the element is itself an embellished operator with a constraint)
  stays with the others, so its stretched width counts toward the target (MathML Core 3.4.2.2 takes the target from the
  other children only; a known difference, which Chrome cannot show because it stretches no scripted operator). The
  target T is the widest of the others, or, when every child stretches, the widest of them
  laid out with a zero constraint; each stretchy child is laid out again with T through `relayout`.
- `Layout(base, under, over, style, accentUnder, accent, embellished)` - with the constants at the font size of the
  element:
  - **Horizontal** - the children are centered on the center of the base; with LargeOpItalicCorrection the italic
    correction of a large operator base, the underscript moves left and the overscript right by half of it. The box is
    as wide as the union of the children, which are shifted so that the leftmost starts at 0.
  - **Over gap** - between the top of the base and the bottom of the overscript: for a large operator base, the larger
    of `UpperLimitGapMin` and `UpperLimitBaselineRiseMin` − the overscript's descent, without extra space; for a base
    stretchy along the inline axis, the larger of `StretchStackGapBelowMin` and `StretchStackTopShiftUp` − the
    overscript's descent, without extra space; otherwise `AccentBaseHeight` − the base's ascent (at least zero) for an
    accent and `OverbarVerticalGap` for another script, with `OverbarExtraAscender` above it.
  - **Under gap** - between the bottom of the base and the top of the underscript: for a large operator base, the larger
    of `LowerLimitGapMin` and `LowerLimitBaselineDropMin` − the underscript's ascent; for a base stretchy along the
    inline axis, the larger of `StretchStackGapAboveMin` and `StretchStackBottomShiftDown` − the underscript's ascent;
    otherwise zero for an accent and `UnderbarVerticalGap` for another script, with `UnderbarExtraDescender` below it.
  - **Extents** - the ascent and the descent are the largest over the base and the placed scripts with their extra
    space, and at least zero. The operator of an embellished element (given by the engine) is kept on the box.

#### Behaviors Matched With Chrome

The reference layouts come from Chrome, which differs from a literal reading of MathML Core in three points that
`UnderOverLayout` follows:

- An overscript is centered on the base; MathML Core places it by its top accent attachment when it has one.
- The gaps of an accent and of a script that is neither a limit nor on a stretchy base are measured between the boxes;
  MathML Core measures them from the baseline of the script, which would move it by its descent or ascent.
- The ascent and the descent of the element are at least zero (a brace above the baseline still reaches down to it).

#### Error Handling

`Layout` throws `ArgumentException` when both scripts are null (a programming error of the caller). The arithmetic is on
finite, clamped lengths; there are no reports.

#### Dependencies

The `MathFont` unit (the limit, stretch stack, overbar, underbar, and accent constants), the `OperatorDictionary`
unit (`StretchAxis`), the `StyleResolver` unit (`MathStyle`), and the `MathBox` unit (boxes, `PlacedBox`,
`EmbellishedOperator`). The stretching itself is done by `TokenLayout` through the `relayout` of the
engine, so the unit does not depend on `StretchyGlyphs`.

#### Callers

`LayoutEngine` lays every valid `munder`, `mover`, and `munderover` out with it, unless its limits move to the script
positions (`ScriptLayout`).

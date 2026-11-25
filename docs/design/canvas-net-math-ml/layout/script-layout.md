### ScriptLayout

![Layout Structure](MathMlLayoutView.svg)

The `ScriptLayout` unit is a software unit of the `Layout` subsystem. Its source file is `ScriptLayout.cs`, which
defines the internal sealed class `ScriptLayout` and the internal record struct `ScriptPair`.

#### Purpose

`ScriptLayout` lays out `msub`, `msup`, `msubsup` (MathML Core 3.4.1) and `mmultiscripts` (3.4.3) from the boxes of
their base and scripts that the layout engine laid out, and the `munder`, `mover`, and `munderover` elements whose
limits move to the script positions (`movablelimits` outside display style, 3.4.2.1). The unit's behavior is specified
by the `CanvasNetMathMl-Layout-ScriptLayout-*` requirements in
`docs/reqstream/canvas-net-math-ml/layout/script-layout.yaml`.

#### Data Model

- `ScriptPair(Sub, Sup)` - a subscript and superscript pair of `mmultiscripts`; a `none` element is an empty box.
- Per instance: the math font (the constants of its `MATH` table). The class holds no other state.

#### Key Methods

- `Layout(base, sub, sup, style, embellished)` - with the constants at the font size of the scripted element:
  - **SubShift (3.4.1.2)** - the largest of `SubscriptShiftDown`, the subscript's ascent − `SubscriptTopMax`, and
    `SubscriptBaselineDropMin` + the base's descent.
  - **SuperShift (3.4.1.3)** - the largest of `SuperscriptShiftUp` (`SuperscriptShiftUpCramped` when the computed
    `math-shift` of the element is `compact`), `SuperscriptBottomMin` + the superscript's descent, and the base's
    ascent − `SuperscriptBaselineDropMax`.
  - **Gap (3.4.1.4)** - with both scripts, when (SubShift − the subscript's ascent) + (SuperShift − the superscript's
    descent) is less than `SubSuperscriptGapMin`, the superscript is raised by the missing gap, but no further than its
    bottom at `SuperscriptBottomMaxWithSubscript`, then the subscript is lowered by the rest.
  - **Placement** - the base at 0; the subscript at the base's width − LargeOpItalicCorrection; the superscript at the
    base's width + ItalicCorrection (`ItalicCorrections`). The width is the base's plus the wider script's plus
    `SpaceAfterScript`; the ascent and the descent are the largest over the base and the shifted scripts, and at least
    zero. The operator of an embellished element (given by the engine) is kept on the box.
- `LayoutMultiscripts(base, postscripts, prescripts, separator, style, embellished)` - 3.4.3.1: the shifts are the
  largest over the pairs, each pair computed as an `msubsup` with its gap; each prescript pair follows
  `SpaceAfterScript` and its scripts are aligned right in the column of the wider one; the base and the box of
  `mprescripts` follow the prescripts; each postscript pair follows the base like the scripts of `msubsup` and is
  followed by `SpaceAfterScript`. The width is the final offset.
- `ItalicCorrections(base)` - (ItalicCorrection, LargeOpItalicCorrection): none for a base that is not an embellished
  operator; the italic correction of the box of a large operator as LargeOpItalicCorrection, of another operator as
  ItalicCorrection. `TokenLayout` gives an operator an italic correction only when it draws a size variant or an
  assembly.

#### Behaviors Matched With Chrome

The reference layouts come from Chrome, which differs from a literal reading of MathML Core in three points that
`ScriptLayout` follows:

- The italic correction of a token glyph (an italic `mi` such as *f*) does not move a superscript; only the italic
  correction of a stretched or large operator does. MathML Core 3.2.1.1 gives a single slanted glyph its italic
  correction.
- The width does not include the italic corrections (MathML Core adds them).
- The ascent and the descent of the element are at least zero.

#### Error Handling

`Layout` throws `ArgumentException` when both scripts are null (a programming error of the caller). The arithmetic is on
finite, clamped lengths; there are no reports.

#### Dependencies

The `MathFont` unit (the script constants) and the `MathBox` unit (boxes, `PlacedBox`, `EmbellishedOperator`).

#### Callers

`LayoutEngine` lays every valid `msub`, `msup`, `msubsup`, and `mmultiscripts` out with it, and every `munder`, `mover`,
and `munderover` whose math style is compact and whose base is an embellished operator with `movablelimits`.

### LayoutEngine Unit Verification Design

This document describes the unit-level verification strategy for the `LayoutEngine` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by laying out small equations through the engine with the real `StyleResolver`, `TokenLayout`,
`RowLayout`, `FractionLayout`, `RadicalLayout`, and bundled `MathFont` (the engine creates its collaborators, and the
font is deterministic), through the
test assembly's `InternalsVisibleTo` access, and by asserting the boxes, their elements, the reports of the sink, and
the bounds. The bounds scenarios use small limits and a pre-canceled token (no clock). Unit tests reside in
`Layout/LayoutEngineTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory equations and the bundled font; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A box, an element, a report, a work count, or an
exception that differs from the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-LayoutEngine-Dispatch: Tokens And Rows

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_Dispatch_LaysOutTokensAndRows`,
`LayoutEngineTests.LayoutEngine_Layout_FractionsAndRadicals_AreDispatched`,
`LayoutEngineTests.LayoutEngine_Layout_ScriptsAndUnderOver_AreDispatched`,
`LayoutEngineTests.LayoutEngine_Layout_NoneAndPrescripts_AreNotReported`,
`LayoutEngineTests.LayoutEngine_Layout_Tables_AreDispatched`,
`LayoutEngineTests.LayoutEngine_Layout_EveryCoreElement_IsDrawn`,
`LayoutEngineTests.LayoutEngine_Layout_Table_IsCompact`

Asserts that tokens paint glyphs, that rows place their children, and that every box keeps its element; that a
fraction, a square root, and a root are drawn without reports: the numerator above and the denominator below the
baseline with a bar, the radical symbol and the overbar around the row of the children of `msqrt`, and the index of
`mroot` first at the 50 px of its two added script levels; that the seven scripted elements are drawn without reports,
their subscripts and underscripts below and their superscripts and overscripts above the baseline, every script at the
60 px of its added script level; that `none` and `mprescripts`, inside and outside `mmultiscripts`, are empty boxes
without reports; that a table holds its rows, which hold their cells (with their elements), which hold the anonymous
row of their content; that an equation with every element of MathML Core (the 31 elements with `maction`, the
annotations, and `mprescripts` and `none`) is laid out without a warning or an unsupported report; and that a fraction
in a cell of a display equation has the height of an inline fraction (the table is compact).

##### CanvasNetMathMl-Layout-LayoutEngine-TableStructure: Anonymous Rows And Cells

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_TableStructure_AnonymousRowsAndCellsWarn`,
`LayoutEngineTests.LayoutEngine_Layout_CellOutsideTable_IsRowWithWarning`

Asserts that two `mi` before an `mtr` become one anonymous row with one anonymous cell of both, that two `mi` after the
`mtd` of an `mtr` become one anonymous cell of that row, and that an `mtd` directly in the table becomes an anonymous
row holding that cell, with one `mathml-child-count` warning per run naming the element and what it is not; and that an
`mtd` and an `mtr` outside a table are rows of their children, as wide as the equivalent `mrow`s, with one
`mathml-child-count` warning each.

##### CanvasNetMathMl-Layout-LayoutEngine-ChildCount: Invalid Fractions And Roots

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_WrongChildCount_IsRowWithChildCountWarning`,
`LayoutEngineTests.LayoutEngine_Layout_InvalidMultiscripts_IsRowWithChildCountWarning`

Asserts that an `mfrac` with one child and an `mroot` with three are rows of their children (every child on the
baseline) with `mathml-child-count` warnings naming the counts and no unsupported report, that the child of the invalid
fraction still gets the script size of the user agent rule (60 px), and that a fraction whose third child is hidden by
`display: none` has two in-flow children and is drawn with its bar; and that an `msub` with one child, an `munderover`
with two, and `mmultiscripts` with an odd number of postscripts, an `mprescripts` base, two `mprescripts`, an odd
number of prescripts, and no children are rows with seven `mathml-child-count` warnings naming each problem.

##### CanvasNetMathMl-Layout-LayoutEngine-Embellishment: Embellished Fractions And Radicals

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_EmbellishedFraction_CarriesItsOperator`,
`LayoutEngineTests.LayoutEngine_Layout_SqrtChildren_InferForms`,
`LayoutEngineTests.LayoutEngine_Layout_EmbellishedScripted_StretchesInRow`

Asserts that a fraction whose numerator is `=` carries the infix operator `=` and is spaced by its row with the leading
and trailing space of the operator, that the row of a square root and the square root itself, and the row of a table
cell and the cell itself, carry no operator, and
that an `msup` whose base is a parenthesis carries the prefix operator of its position.

##### CanvasNetMathMl-Layout-LayoutEngine-ScriptForms: Scripts Are Postfix

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_Scripts_ArePostfixOperators`

Asserts that an operator superscript, an operator wrapped in an `mrow` as an overscript, and an operator underscript are
postfix; that `accent="maybe"` is one `mathml-attribute-invalid` warning naming the attribute and makes the overscript
a script at 60 px; and that `accent` on `munder` is reported as `mathml-attribute-unsupported`.

##### CanvasNetMathMl-Layout-LayoutEngine-MovableLimits: Limits Become Scripts Inline

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_MovableLimits_InlineBecomeScripts`

Asserts that the limits of an inline summation start at its right edge, as scripts, while in display style and with
`movablelimits="false"` they are centered within its width, and that the scripted element carries the operator.

##### CanvasNetMathMl-Layout-LayoutEngine-UnsupportedElements: Elements Outside MathML Core

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_NonCoreElement_FallsBackToRowWithReport`,
`LayoutEngineTests.LayoutEngine_Layout_EveryCoreElement_IsDrawn`

Asserts that `mfenced` and `menclose` are laid out as rows of their children, as wide as the equivalent `mrow`s, and
reported as `mathml-element-unsupported` as elements outside MathML Core, the first with a hint to write `mo` elements;
and that no element of MathML Core is reported.

##### CanvasNetMathMl-Layout-LayoutEngine-HiddenElements: No Box

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_HiddenElements_GetNoBox`,
`LayoutEngineTests.LayoutEngine_Layout_HiddenChild_KeepsTheEmbellishedOperator`,
`LayoutEngineTests.LayoutEngine_Layout_HiddenGrandchild_KeepsTheRowSpaceLike`

Asserts that an element with `display: none` and the annotations get no box, and that `semantics` and `maction` lay out
their first child only. Asserts that a hidden third child of an `msup` or `mfrac` whose first child is `+` leaves the
element an embellished operator, infix in its row, as wide as without the hidden child, and that a hidden grandchild
leaves a row of an `mspace` space-like, so the minus sign after it is a prefix operator and the equation is as wide as
without the hidden grandchild; neither reports a warning or an unsupported feature.

##### CanvasNetMathMl-Layout-LayoutEngine-Phantom: mphantom

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_Mphantom_IsPhantomWithSize`

Asserts that the box of `mphantom` is a phantom with the size of its content.

##### CanvasNetMathMl-Layout-LayoutEngine-UnsupportedAttributes: Attribute Reports

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_UnsupportedAttributes_AreReported`

Asserts that legacy and unknown attributes and `dir="rtl"` are reported as `mathml-attribute-unsupported`, that the
legacy table attributes `align`, `equalrows`, `columnwidth`, and `groupalign` (on `mtable` and on `mtd`) are reported
the same way, five reports in all, and that global attributes that do not change a picture are silent.

##### CanvasNetMathMl-Layout-LayoutEngine-TextInRows: Text In A Row

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_TextInRow_IsLaidOutLikeMtext`

Asserts that text directly inside a row is laid out as a space-like text box.

##### CanvasNetMathMl-Layout-LayoutEngine-OperatorForms: Forms From Positions

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_OperatorForms_FollowPositionInRow`,
`LayoutEngineTests.LayoutEngine_Layout_SqrtChildren_InferForms`

Asserts that in a row with a space-like `mtext` the first operator is prefix and the middle ones infix, that an `mrow`
wrapping an operator and an `mspace` passes its own (infix) form to the operator, that an explicit form wins, and that
the lone operator of a row is infix; and that inside `msqrt` the first and last operators of several children are
prefix and postfix, and a lone operator is infix and gets no operator spacing in the anonymous row of `msqrt`, as in
the anonymous row of an `mtd` (the row is as wide as the operator).

##### CanvasNetMathMl-Layout-LayoutEngine-Stretching: Embellished Operators Stretch

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_EmbellishedInMrow_StretchesCoreOperator`,
`LayoutEngineTests.LayoutEngine_Layout_EmbellishedScripted_StretchesInRow`

Asserts that a bracket wrapped in an `mrow` and one wrapped in an `mstyle` take the prefix and postfix forms of their
wrappers and stretch through them to cover an `mspace` of 2 em by 1 em, with the same extents, and that the wrapper has
the extents of its operator; that the parenthesis at the base of an `msup` stretches through it to cover the same
`mspace`; and that an over brace is stretched at least as wide as the row under it.

##### CanvasNetMathMl-Layout-LayoutEngine-Bounds: Depth, Work, And Cancellation

**Tests**: `LayoutEngineTests.LayoutEngine_Layout_Bounds_AreEnforced`,
`LayoutEngineTests.LayoutEngine_Layout_StretchRelayout_IsCharged`,
`LayoutEngineTests.LayoutEngine_Layout_Table_ChargesGridSlots`

Asserts that a nesting limit of 2 and a work limit of 5 fail with messages naming `MathMlLimits.MaxNestingDepth` and
`MathMlLimits.MaxLayoutWork`, that a pre-canceled token throws, that the work of the equation is 7 (four elements
and three characters), that a stretchy parenthesis laid out again to stretch is charged twice (two more units than
the same row with `stretchy="false"`), and that a table whose cell spans 50 columns and all three rows costs at least
the 149 further grid slots and 49 further columns of its span more than a table of one cell.

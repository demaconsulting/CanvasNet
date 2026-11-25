### StyleResolver Unit Verification Design

This document describes the unit-level verification strategy for the `StyleResolver` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by resolving the styles of small trees (built directly or read with the Syntax subsystem) with a
resolver over the bundled `MathFont` and a collecting report sink, through the test assembly's `InternalsVisibleTo`
access, and by asserting the computed styles and the reports. The font is used as is because its `MATH` constants are
the expected scale factors. Unit tests reside in `Layout/StyleResolverTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: In-memory trees and the bundled font; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A computed style value or a report that differs from
the expected one constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-StyleResolver-MathStyle: Display And displaystyle

**Tests**: `StyleResolverTests.StyleResolver_ResolveRoot_Display_SetsMathStyle`,
`StyleResolverTests.StyleResolver_Resolve_ScriptLevelAndDisplayStyle_SetDepthAndStyle`

Asserts the normal math style of a block equation and the compact style of an inline one, the override of the option,
the warning of an invalid `display`, and the effect of `displaystyle`.

##### CanvasNetMathMl-Layout-StyleResolver-MathDepth: scriptlevel And Clamping

**Tests**: `StyleResolverTests.StyleResolver_Resolve_ScriptLevelAndDisplayStyle_SetDepthAndStyle`,
`StyleResolverTests.StyleResolver_Resolve_ExtremeScriptLevel_IsClamped`

Asserts the three forms of `scriptlevel` and that an extreme level is clamped so the font size stays finite.

##### CanvasNetMathMl-Layout-StyleResolver-FontSize: Scaling And mathsize

**Tests**: `StyleResolverTests.StyleResolver_ScaleFactor_UsesScriptPercentScaleDownConstants`,
`StyleResolverTests.StyleResolver_Resolve_MathSize_SetsFontSize`

Asserts the factors 0.6 and 0.5 for one and two levels and 0.71 per further level, in both directions, and that
`mathsize` as a percentage or an em of the inherited size wins over the scaling, with a warning for an invalid value.

##### CanvasNetMathMl-Layout-StyleResolver-Colors: Color, Background, And merror

**Tests**: `StyleResolverTests.StyleResolver_Resolve_Colors_InheritColorNotBackground`,
`StyleResolverTests.StyleResolver_Resolve_Merror_HasLightYellowBackground`

Asserts that the color is inherited and the background is not, the warnings of invalid colors, and the light yellow
background of `merror`, which an attribute overrides.

##### CanvasNetMathMl-Layout-StyleResolver-Variant: mathvariant

**Tests**: `StyleResolverTests.StyleResolver_Resolve_MathVariant_IsInherited`

Asserts that `mathvariant` is inherited, that `normal` cancels the automatic italic, and the warning of an invalid
value.

##### CanvasNetMathMl-Layout-StyleResolver-InlineStyle: The style Attribute

**Tests**: `StyleResolverTests.StyleResolver_Resolve_InlineStyle_OverridesAttributes`

Asserts that the inline style overrides the attributes, sets the math depth and the font style, hides with
`display: none`, and reports declarations outside the subset as `mathml-css-unsupported`.

##### CanvasNetMathMl-Layout-StyleResolver-ChildRules: The Children Of Fractions And Roots

**Tests**: `StyleResolverTests.StyleResolver_ResolveChild_FractionChildren_AutoAddAndCompact`,
`StyleResolverTests.StyleResolver_ResolveChild_RootIndex_AddsTwoLevels`,
`StyleResolverTests.StyleResolver_ResolveChild_Hints_ReplaceUaRules`

Asserts that the numerator and the denominator of a compact fraction get depth 1, the compact style, and 60 px, those
of a display fraction keep depth 0 and 100 px in the compact style; that the index of a root in display style gets
depth 2, the compact style, and 50 px while a child without a rule keeps the parent's style; and that the hints of a
child replace the rule: `scriptlevel="+1"` on an index is one level over the parent (depth 1, not 3),
`scriptlevel="0"` with `displaystyle="true"` undoes the numerator rule, and `mathsize` sets the size while the depth
still follows the rule.

##### CanvasNetMathMl-Layout-StyleResolver-MathShift: math-shift

**Tests**: `StyleResolverTests.StyleResolver_ResolveChild_MathShift_CompactAndInherited`

Asserts that the `math` element has `math-shift: normal`, that a denominator, `msqrt`, and `mroot` get
`math-shift: compact` while a numerator does not, and that a compact math shift is inherited by a row and by a numerator
under it.

##### CanvasNetMathMl-Layout-StyleResolver-ScriptRules: The Children Of Scripted Elements

**Tests**: `StyleResolverTests.StyleResolver_ResolveChild_Scripts_AddOneAndCompact`,
`StyleResolverTests.StyleResolver_ResolveChild_AccentScript_KeepsFontSize`,
`StyleResolverTests.StyleResolver_ResolveChild_Multiscripts_MathShiftByPosition`

Asserts that under a display-style parent the base of an `msubsup` keeps depth 0, display style, and 100 px while its
subscript and superscript and the overscript of an `mover` get depth 1, compact style, and 60 px, with
`math-shift: compact` for the subscript only; that an accent script (`accent="TRUE"` on `mover`, both accents on
`munderover`) keeps 100 px at depth 1 and the base of an accent `mover` gets `math-shift: compact`, while
`accent="yes"` and `accent` on `munder` are not accents; and that the children of an `mmultiscripts` with
`mprescripts` get `math-shift: compact` at positions 2, 4, and 5 (counting from 1) and inherit it elsewhere.

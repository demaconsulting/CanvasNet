### OperatorDictionary Unit Verification Design

This document describes the unit-level verification strategy for the `OperatorDictionary` unit in the
`DemaConsulting.CanvasNet.MathMl.Layout` namespace.

#### Verification Approach

The unit is verified by looking operators up in the generated tables through the test assembly's `InternalsVisibleTo`
access and asserting their categories and values against Figures 24 to 26 and 28 of MathML Core Appendix B.1 (the
values in the tests are read from the specification, not from the tables), by checking the form inference and the
parsing of the `form` attribute, and by checking that the generated tables are well-formed. Unit tests reside in
`Layout/OperatorDictionaryTests.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions over generated tables; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes. A category, a spacing, a property, a form, or an axis
that differs from the expected value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMathMl-Layout-OperatorDictionary-Categories: Categories And Values

**Tests**: `OperatorDictionaryTests.OperatorDictionary_Lookup_PlusInfix_IsCategoryB`,
`OperatorDictionaryTests.OperatorDictionary_Lookup_EqualsInfix_IsCategoryA`,
`OperatorDictionaryTests.OperatorDictionary_Lookup_LeftParenPrefix_IsFenceStretchySymmetric`,
`OperatorDictionaryTests.OperatorDictionary_Lookup_SumPrefix_IsLargeOpMovableLimits`,
`OperatorDictionaryTests.OperatorDictionary_Lookup_IntegralPrefix_IsLargeOpNotMovable`,
`OperatorDictionaryTests.OperatorDictionary_Lookup_VerticalBarInfix_IsForceDefault`,
`OperatorDictionaryTests.OperatorDictionary_Lookup_UnlistedCharacter_IsDefault`,
`OperatorDictionaryTests.OperatorDictionary_Data_RangesAreSortedAndDisjoint`

Asserts category B for an infix plus (4/18 em, no property), A for an infix arrow (5/18 em, stretchy) and Default for
an equals sign (5/18 em, not stretchy), F and G for the parentheses (no space, stretchy, symmetric), J for a summation
(3/18 em, symmetric, largeop, movablelimits) also when its form was inferred as infix but Default when infix is
explicit, H for an integral (largeop without movable limits), ForceDefault for the infix vertical line and tilde
operator while the prefix and postfix vertical lines are F and G, Default for an unlisted character, a long or empty
text, and the reserved range, I for a postfix Arabic operator of the supplementary planes, and that every category has
its values and the tables are sorted and disjoint.

##### CanvasNetMathMl-Layout-OperatorDictionary-SpecialTables: Two-Character Operators, Overlays, Fences

**Tests**: `OperatorDictionaryTests.OperatorDictionary_Lookup_TwoAsciiChars_MapsThroughSpecialTable`,
`OperatorDictionaryTests.OperatorDictionary_Lookup_NegatedOperator_StripsCombiningSolidus`,
`OperatorDictionaryTests.OperatorDictionary_Lookup_LeftParenPrefix_IsFenceStretchySymmetric`

Asserts that `&&`, `**`, `||`, and `++` map to the categories of U+0320 plus their index (B, C, D, and E), that `->`
and `:=` have no entry, that a combining long solidus or vertical line overlay is dropped (`+` with U+0338 is B), and
the fence and separator defaults (`(` and `||` are fences, `,` is a separator, `+` and `(` are not).

##### CanvasNetMathMl-Layout-OperatorDictionary-Forms: Form Inference And The Attribute

**Tests**: `OperatorDictionaryTests.OperatorDictionary_InferForm_FirstMiddleLast_PrefixInfixPostfix`,
`OperatorDictionaryTests.OperatorDictionary_InferForm_ExplicitAttribute_Wins`

Asserts prefix, infix, and postfix for the first, middle, and last of three children and infix for a lone child; that
`POSTFIX` parses as postfix and `sideways` does not; and that an explicit infix form of `(` gives Default while an
inferred one falls back to F.

##### CanvasNetMathMl-Layout-OperatorDictionary-Axis: The Stretch Axis

**Tests**: `OperatorDictionaryTests.OperatorDictionary_Axis_InlineList_IsInline`

Asserts the inline axis for the right arrow, the equals sign, and U+1EEF0, and the block axis for the parenthesis, the
up arrow, a text of two characters, and an empty text.

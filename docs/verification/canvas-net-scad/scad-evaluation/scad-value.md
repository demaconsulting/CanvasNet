### ScadValue Unit Verification Design

<!-- cspell:ignore undef lexicographic lexicographically scalars infinities -->

This document describes the unit-level verification strategy for the `ScadValue` unit: the internal value model of
the evaluator in the `DemaConsulting.CanvasNet.Scad.Evaluation` namespace — `ScadValue` (kinds, truthiness,
equality, ordering, arithmetic, indexing, text forms, and conversion), `ScadVector`, `ScadRange`,
`ScadNumberFormatter`, `ScadNumbers`, and `ScadText`.

#### Verification Approach

The unit is verified by constructing values through the factories and asserting on the results of the operations:
returned values and kinds, the `typeError` indication, text, and the exceptions thrown. The unit has no
dependencies other than the operator enumerations of the syntax tree, so nothing is mocked. Operator coverage is
table driven: the truthiness table, the ordering table, and the number-formatting table are `[Theory]` data, so
that every pinned case is a separate result. The internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Culture independence is verified by running the formatting and text-form scenarios under the `de-DE`, `fr-FR`, and
`tr-TR` current cultures and asserting the same text as under the invariant culture. Stack behavior is verified by
running recursive operations on values nested 256 levels deep from caller threads with a small stack: equality,
hashing, ordering, and formatting must succeed on a 256 KiB stack, arithmetic on a 1 MiB stack, and an equality
check on an absurdly deep value (built iteratively) on a tiny stack must signal `InsufficientExecutionStackException`
rather than crash the process. Conversion is verified with endless and self-referential sequences, which must be
rejected instead of running forever.

Unit tests reside in `ScadValueTests.cs` (kinds, truthiness, equality, hashing, ordering, immutability, depth),
`ScadValueArithmeticTests.cs` (operators, products, indexing, members), `ScadValueProductLimitTests.cs` (matrix
product limits under a host), `ScadValueFormatTests.cs` (text forms),
`ScadValueConversionTests.cs`, `ScadValueNestingTests.cs`, `ScadNumberFormatterTests.cs`, `ScadRangeTests.cs`, and
`ScadTextTests.cs` within the `DemaConsulting.CanvasNet.Scad.Tests` project, in the `Evaluation` folder, with the
shared helpers (`N`, `S`, `B`, `Vec`, `Nums`, `Nested`, `OnSmallStack`, and `WithCulture`) in
`ScadEvaluationTestHelpers.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Threads**: The nesting scenarios run on caller threads with a 256 KiB, 1 MiB, or tiny stack and join before
  asserting
- **Cultures**: The formatting scenarios set the current culture and current UI culture for the duration of the call
  and restore them
- **Isolation**: Each test builds its own values; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong result, kind, error indication, text, or exception type constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadEvaluation-ScadValue-Kinds: Kinds, Default, And Payload Access

**Tests**: `ScadValueTests.ScadValue_Default_IsUndef`, `ScadValueTests.ScadValue_Factories_ProduceMatchingKindAndPayload`,
`ScadValueTests.ScadValue_Payload_WrongKind_Throws`, `ScadValueTests.ScadValue_KindDescription_NamesEachKind`

Asserts that `default` is `undef`, that each factory produces its kind and payload, that reading a payload of
another kind throws `InvalidOperationException`, and the description text of each kind.

##### CanvasNetScad-ScadEvaluation-ScadValue-Truthiness: Truth Table

**Tests**: `ScadValueTests.ScadValue_IsTrue_Table`

Asserts the table: `undef`, `false`, `0`, `-0`, the empty string, and the empty vector are false; `NaN`, non-zero
numbers, `true`, non-empty strings and vectors, ranges, and functions are true.

##### CanvasNetScad-ScadEvaluation-ScadValue-Equality: Equality And Hashing

**Tests**: `ScadValueTests.ScadValue_Equals_SameKind_ComparesByValue`,
`ScadValueTests.ScadValue_Equals_DifferentKinds_AreNotEqual`,
`ScadValueTests.ScadValue_Equals_NaNAndNegativeZero_FollowIeee`, `ScadValueTests.ScadValue_Equals_Functions_ByReference`,
`ScadValueTests.ScadValue_GetHashCode_EqualValues_HaveEqualHashCodes`, `ScadTextTests.ScadNumbers_AreEqual_FollowsIeee`

Asserts value equality within a kind (including deep vectors and ranges), inequality across kinds (`1` and `true`
are different), `NaN` unequal to itself and `0` equal to `-0`, reference equality of functions, equal hash codes
for equal values, and the IEEE helper.

##### CanvasNetScad-ScadEvaluation-ScadValue-Ordering: Ordering And Relational Operators

**Tests**: `ScadValueTests.ScadValue_TryCompare_OrderedPairs_ReturnsSign`,
`ScadValueTests.ScadValue_Relational_UnorderedPairs_AreAllFalse`,
`ScadValueTests.ScadValue_Binary_Relational_OrderedPair_AppliesOperators`

Asserts the sign for number, string, boolean, and vector pairs (including a shorter prefix), that every other
pairing is unordered and all four relational operators are false for it, and the operator results for ordered
pairs.

##### CanvasNetScad-ScadEvaluation-ScadValue-Arithmetic: Operators

**Tests**: `ScadValueArithmeticTests.ScadValue_Binary_Numbers_ComputesResult`,
`ScadValueArithmeticTests.ScadValue_Binary_DivisionByZero_FollowsIeee`,
`ScadValueArithmeticTests.ScadValue_Binary_EqualityAndLogic_NeverTypeError`,
`ScadValueArithmeticTests.ScadValue_Binary_InvalidOperands_ReturnsUndefWithError`,
`ScadValueArithmeticTests.ScadValue_Binary_VectorAddSubtract_IsElementWise`,
`ScadValueArithmeticTests.ScadValue_Binary_VectorWithBadElement_KeepsShapeAndFlagsError`,
`ScadValueArithmeticTests.ScadValue_Binary_ScaleAndDivide_ApplyToEveryElement`,
`ScadValueArithmeticTests.ScadValue_Unary_NegatePlusNot_Apply`,
`ScadValueArithmeticTests.ScadValue_Unary_InvalidOperand_ReturnsUndefWithError`

Asserts the number results (including the C remainder and the power), the IEEE results for division by zero, that
equality and logic never set the error indication, `undef` with the error indication for an invalid operand pair,
element-wise addition and subtraction with the shape kept when one element is bad, scaling and division of vectors,
and the unary operators.

##### CanvasNetScad-ScadEvaluation-ScadValue-Products: Dot And Matrix Products

**Tests**: `ScadValueArithmeticTests.ScadValue_Binary_VectorTimesVector_IsDotProduct`,
`ScadValueArithmeticTests.ScadValue_Binary_MatrixTimesVector_ReturnsProduct`,
`ScadValueArithmeticTests.ScadValue_Binary_VectorTimesMatrix_ReturnsProduct`,
`ScadValueArithmeticTests.ScadValue_Binary_MatrixTimesMatrix_ReturnsProduct`,
`ScadValueArithmeticTests.ScadValue_Binary_IdentityMatrixTimesVector_ReturnsSameVector`,
`ScadValueProductLimitTests.ScadValueProduct_Binary_MatrixTimesMatrix_ResultOverListLimit_ThrowsBeforeCharging`,
`ScadValueProductLimitTests.ScadValueProduct_Binary_MatrixTimesMatrix_ResultOverListLimit_DoesNotCopyOperands`,
`ScadValueProductLimitTests.ScadValueProduct_Binary_MatrixTimesMatrix_OverStepBudget_DoesNotCopyOperands`,
`ScadValueProductLimitTests.ScadValueProduct_Binary_MatrixTimesMatrix_ChargesResultElementsPlusScaledWork`,
`ScadValueProductLimitTests.ScadValueProduct_Binary_MatrixTimesMatrix_OverStepBudget_ThrowsWithoutComputing`,
`ScadValueProductLimitTests.ScadValueProduct_ProductWork_HugeDimensions_SaturatesWithoutOverflow`,
`ScadValueProductLimitTests.ScadValueProduct_Binary_LargeProducts_PollCancellationAtBoundedIntervals`,
`ScadValueProductLimitTests.ScadValueProduct_Binary_WithoutHost_ComputesUnbounded`

Asserts the dot product, the three matrix and vector products with known results, undef for mismatched
dimensions, and that the identity matrix leaves a vector unchanged. The limit scenarios use the recording
`ScadTestBuiltinHost` (which counts steps and cancellation polls): a matrix product with more result elements than
the list limit throws `List too long` before any step is charged (a product of exactly the limit succeeds); the
charge is `rows × columns + ceil(rows × inner × columns / 16)` for several shapes, so the constant is pinned; a
product over the step budget throws before it multiplies (no poll happens); the work computation saturates for
`int.MaxValue` dimensions instead of overflowing; matrix-by-matrix, matrix-by-vector, and vector-by-matrix products
poll exactly once per 65,536 multiply-adds (counted, so the assertion is exact and independent of timing), a poll
that throws stops the product, and a single dot product longer than the interval polls inside itself; and, without
a host, the product is unbounded and returns the same values as before. Two further scenarios multiply
shared-row 1000 × 1000 matrices (building them allocates almost nothing) and measure the bytes the calling thread
allocates: a 1000 × 1000 by 1000 × 1001 product (result over the list limit) and a 1000 × 1000 by 1000 × 1000 product
(over a one-million-step budget) are both refused from the dimensions alone with under 100 KB allocated, where
copying the operands first took about 16 MB; the measurement is valid because the value-level call runs on the test
thread.

##### CanvasNetScad-ScadEvaluation-ScadValue-Indexing: Indexing, Members, And Unicode Scalars

**Tests**: `ScadValueArithmeticTests.ScadValue_Index_Vector_ReturnsElementOrUndef`,
`ScadValueArithmeticTests.ScadValue_Index_NonIndexableTarget_ReturnsUndef`,
`ScadValueArithmeticTests.ScadValue_Index_String_UsesUnicodeScalars`,
`ScadValueArithmeticTests.ScadValue_Member_XyzOnVector_ReturnsElements`,
`ScadTextTests.ScadText_ScalarCount_CountsPairsOnce`, `ScadTextTests.ScadText_ScalarAt_ReturnsScalarOrNull`,
`ScadTextTests.ScadText_Scalars_SplitsIntoScalars`, `ScadTextTests.ScadText_FirstCodePoint_HandlesPairsAndLoneSurrogates`,
`ScadTextTests.ScadText_FromCodePoint_ValidAndInvalid`

Asserts element access with negative (including `-0.5`, which must not truncate to element 0), NaN, infinite, too large,
non-negative fractional (`[10, 20, 30][1.5]` is `20`), and non-numeric indexes, for vectors and for strings, undef for non-indexable
targets, string indexing over a string with a surrogate pair, the x, y, and z members, and the scalar helpers,
including a lone surrogate and invalid code points.

##### CanvasNetScad-ScadEvaluation-ScadValue-Ranges: Range Counting

**Tests**: `ScadRangeTests.ScadRange_Count_ReturnsExpected`, `ScadRangeTests.ScadRange_Count_HugeRange_Saturates`,
`ScadRangeTests.ScadRange_Count_AbsorbsRoundingErrorButNotRealOvershoot`,
`ScadRangeTests.ScadRange_ElementAt_LastElementIsWithinEndAndNextIsBeyond`,
`ScadRangeTests.ScadRange_ElementAt_ComputesFromIndex`, `ScadRangeTests.ScadRange_IsInvalid_ZeroOrNonFiniteParts`,
`ScadRangeTests.ScadRange_Equals_ComparesStartStepEnd`

Asserts counts for ascending, descending, fractional, and contradictory ranges, saturation for an enormous range,
elements computed from the index, the invalid cases, and equality by start, step, and end. The rounding scenarios
assert that `[0 : 0.1 : 0.3]` keeps its last element while `[0 : 0.1 : 0.2999999995]` does not reach `0.3` (a fixed
absolute tolerance would include it), for ascending and descending ranges, negative steps, large magnitudes (`1e15`,
`1e16`), large and tiny steps, and that the last element passes the end by no more than rounding error while the
next element is clearly beyond it.

##### CanvasNetScad-ScadEvaluation-ScadValue-NumberFormatting: Deterministic Number Text

**Tests**: `ScadNumberFormatterTests.ScadNumberFormatter_Format_Table_ReturnsPinnedText`,
`ScadNumberFormatterTests.ScadNumberFormatter_Format_OtherCultures_IsIdentical`,
`ScadNumberFormatterTests.ScadNumberFormatter_Format_ExactTie_RoundsToEvenDigit`,
`ScadNumberFormatterTests.ScadNumberFormatter_Format_SmallestFixed_UsesFixedNotation`,
`ScadNumberFormatterTests.ScadNumberFormatter_Format_AnyValue_UsesOnlyInvariantCharacters`

Asserts the pinned table (zero, negative zero, the non-finite values, fixed and scientific boundaries, six-digit
rounding, trailing zeros, and exponents with at least two digits), the same text under `de-DE`, `fr-FR`, and
`tr-TR`, that the exact tie `1234565` prints as `1.23456e+06`, that `0.0001` stays fixed, and that no value
produces a character outside digits, sign, point, `e`, and the special words.

##### CanvasNetScad-ScadEvaluation-ScadValue-TextForms: String And Echo Forms

**Tests**: `ScadValueFormatTests.ScadValue_ToStr_Scalars_AreRawAndFormatted`,
`ScadValueFormatTests.ScadValue_ToEchoText_Strings_AreQuotedAndEscaped`,
`ScadValueFormatTests.ScadValue_ToEchoText_StringWithBreaks_HasNoRawLineBreak`,
`ScadValueFormatTests.ScadValue_ToStr_Vectors_UseCommaSpaceAndQuoteNestedStrings`,
`ScadValueFormatTests.ScadValue_ToStr_Range_ShowsStartStepEnd`, `ScadValueFormatTests.ScadValue_ToStr_Function_ShowsParameterNames`,
`ScadValueFormatTests.ScadValue_ToStr_OtherCultures_IsIdentical`, `ScadValueFormatTests.ScadValue_TryToStr_Limit_FitsOrFails`,
`ScadValueFormatTests.ScadValue_TryToStr_HugeVector_StopsEarly`,
`ScadValueFormatTests.ScadValueFormat_TryToStr_WithMeter_ChargesPerVisitedElement`,
`ScadValueFormatTests.ScadValueFormat_TryToStr_StepLimitReached_StopsMidFormat`,
`ScadValueFormatTests.ScadValueFormat_TryToEchoText_WithMeter_QuotedStringsChargedByLength`

Asserts the raw top-level string and the quoted nested string, the escapes of the echo form and that a rendered
value never contains a raw line break, the vector, range, and function forms, identical text under other
cultures, and that the length-limited variants succeed at the limit, fail one over, and stop early for a huge
vector. The metered scenarios use the recording `ScadTestBuiltinHost`: every visited element, nested ones
included, is charged eight steps; a budget of 100 steps stops the formatting of a thousand-element vector at the
thirteenth element (104 steps charged) instead of after the whole value; and a quoted string element is charged one
extra step per sixteen characters.

##### CanvasNetScad-ScadEvaluation-ScadValue-NestingLimit: Depth And Stack Behavior

**Tests**: `ScadValueNestingTests.ScadValue_MaxNestingDepth_Is256_AndFactoryDoesNotLimit`,
`ScadValueTests.ScadValue_Depth_ReflectsNesting`,
`ScadValueNestingTests.ScadValue_ReadOnlyOperations_AtDepth256_OnSmallStack_Succeed`,
`ScadValueNestingTests.ScadValue_Arithmetic_AtDepth256_OnModerateStack_Succeeds`,
`ScadValueNestingTests.ScadValue_Arithmetic_NeverIncreasesDepth`,
`ScadValueNestingTests.ScadValue_Equals_ExtremeDepthOnTinyStack_ThrowsInsufficientStackNotCrash`

Asserts the constant 256, that the factories do not enforce it, the depth reported for nested vectors, that
equality, hashing, ordering, and formatting at depth 256 succeed on a 256 KiB stack and arithmetic on a 1 MiB
stack, that arithmetic never deepens a vector, and that an extreme depth on a tiny stack signals
`InsufficientExecutionStackException` instead of crashing.

##### CanvasNetScad-ScadEvaluation-ScadValue-Conversion: Conversion From Objects

**Tests**: `ScadValueConversionTests.ScadValue_TryFromObject_Scalars_Convert`,
`ScadValueConversionTests.ScadValue_TryFromObject_NumericTypes_ConvertToNumbers`,
`ScadValueConversionTests.ScadValue_TryFromObject_Sequences_ConvertToVectors`,
`ScadValueConversionTests.ScadValue_TryFromObject_UnsupportedObjects_AreRejected`,
`ScadValueConversionTests.ScadValue_TryFromObject_EndlessSequence_IsRejected`,
`ScadValueConversionTests.ScadValue_TryFromObject_SelfReferentialList_IsRejected`,
`ScadValueConversionTests.ScadValue_TryFromObject_Nesting_IsBoundedAt256`,
`ScadValueConversionTests.ScadValue_TryFromObject_TooManyElements_IsRejected`

Asserts the conversion of null, booleans, strings, every numeric type, and nested sequences, the rejection of
unsupported objects, and the rejection (not a hang) of an endless sequence, a self-referential list, nesting beyond
256 levels, and more than 1,000,000 elements.

##### CanvasNetScad-ScadEvaluation-ScadValue-Immutability: Read-Only Vectors

**Tests**: `ScadValueTests.ScadValue_Vector_IsImmutableView`

Asserts that a vector exposes only a read-only view of its elements and that building from a collection copies it.

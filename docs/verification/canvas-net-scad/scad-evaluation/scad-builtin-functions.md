### ScadBuiltinFunctions Unit Verification Design

<!-- cspell:ignore undef unseeded SplitMix evaluator -->

This document describes the unit-level verification strategy for the `ScadBuiltinFunctions` unit: the internal
registry of built-in functions and its implementations (mathematics, trigonometry, list, string, search, type, and
random functions), the `IScadBuiltinHost` interface through which a function reaches the evaluator's services, and
the `ScadRandom` generator, in the `DemaConsulting.CanvasNet.Scad.Evaluation` namespace.

#### Verification Approach

The unit is verified by calling `ScadBuiltinFunctions.TryInvoke` directly with evaluated arguments (built with the
helpers `N`, `S`, `B`, `Vec`, and `Nums`) and a test-only host, `ScadTestBuiltinHost`, that implements
`IScadBuiltinHost` by recording every warning, unsupported-feature report, and charged step, and by enforcing
explicit list-length and string-length limits (throwing the same `ScadEvaluationException` as the evaluator).
The host is the only test double; the registry, the argument binding, the value model, and the generator are
real. Each function is exercised with normal inputs, edge inputs, and wrong-type inputs (which must return `undef`
and exactly one warning); the limit scenarios assert the pair "at the limit succeeds, one over throws".

Floating-point results are pinned carefully, because the transcendental functions of `System.Math` can differ in
the last bit between runtimes: the exact cases (`sin(90)`, `cos(90)`, `sin(180)`, `tan(90)`) are asserted exactly,
other angles are compared as six-digit text or with a tolerance, and `log(1000)` is compared as text. The
generator is verified against **golden sequences** pinned in the tests, so any platform drift fails; `rands` is
verified for seeded repeatability, range, count truncation, the deterministic unseeded stream, and the limit.
Culture independence of `str` is verified under the `de-DE` culture.

Unit tests reside in `ScadBuiltinMathTests.cs`, `ScadBuiltinListTests.cs`, `ScadBuiltinStringTests.cs`,
`ScadBuiltinRandomTests.cs` (including the `ScadRandom` scenarios), and `ScadBuiltinRegistryTests.cs` (type tests,
version, unsupported names, and argument binding) within the `DemaConsulting.CanvasNet.Scad.Tests` project, in the
`Evaluation` folder, with the host in `ScadTestBuiltinHost.cs`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: The test host `ScadTestBuiltinHost` only; no mocking framework
- **Cultures**: One scenario sets the current culture to German for the call and restores it
- **Isolation**: Each test builds its own arguments and host; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong value, wrong number of warnings, wrong warning text, wrong feature identifier, or missing or
unexpected limit exception constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Math: Elementary Functions

**Tests**: `ScadBuiltinMathTests.ScadBuiltinMath_Unary_ReturnsExpected`,
`ScadBuiltinMathTests.ScadBuiltinMath_Pow_ComputesPowers`, `ScadBuiltinMathTests.ScadBuiltinMath_SpecialValues_FollowIeee`,
`ScadBuiltinMathTests.ScadBuiltinMath_Unary_WrongType_ReturnsUndefWithWarning`,
`ScadBuiltinMathTests.ScadBuiltinMath_Binary_WrongType_ReturnsUndefWithWarning`

Asserts the results of each single-number function (including the base-ten logarithm of 1000, rounding of negative
halves away from zero, and the square root of a negative number as `NaN`), powers, the infinities and `NaN` of the
IEEE special values, and `undef` plus one warning for a non-number operand.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Trigonometry: Degrees

**Tests**: `ScadBuiltinMathTests.ScadBuiltinMath_Trigonometry_IsExactAtRightAngles`,
`ScadBuiltinMathTests.ScadBuiltinMath_Tan_AtOddRightAngles_IsInfinite`,
`ScadBuiltinMathTests.ScadBuiltinMath_Trigonometry_OtherAngles_MatchesToSixDigits`,
`ScadBuiltinMathTests.ScadBuiltinMath_InverseTrigonometry_ReturnsDegrees`,
`ScadBuiltinMathTests.ScadBuiltinMath_Atan2_CoversQuadrantsAndNamedArguments`

Asserts exact values at multiples of 90 degrees, the infinite tangent at 90 and 270 degrees, six-digit text for
other angles, inverse functions returning degrees, and `atan2` in all four quadrants (with named arguments).

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Aggregates: Minimum And Maximum

**Tests**: `ScadBuiltinMathTests.ScadBuiltinMath_MinMax_AcceptNumbersOrOneVector`,
`ScadBuiltinMathTests.ScadBuiltinMath_MinMax_InvalidOperands_ReturnUndefWithWarning`

Asserts several numbers and one vector, that `NaN` propagates, and `undef` with a warning for an empty vector and
for other operands.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Vectors: Length, Concatenation, Norm, And Cross Product

**Tests**: `ScadBuiltinListTests.ScadBuiltinList_Len_VectorsAndStrings_ReturnsCount`,
`ScadBuiltinListTests.ScadBuiltinList_Len_OtherKinds_ReturnsUndefWithWarning`,
`ScadBuiltinListTests.ScadBuiltinList_Concat_FlattensOneLevel`, `ScadBuiltinListTests.ScadBuiltinList_Norm_ReturnsEuclideanLength`,
`ScadBuiltinListTests.ScadBuiltinList_Cross_ThreeAndTwoDimensions`,
`ScadBuiltinListTests.ScadBuiltinList_Cross_InvalidOperands_ReturnsUndefWithWarning`

Asserts the count of a vector and of a string with a surrogate pair, `undef` plus a warning for other kinds, the
single-level flattening of `concat`, the norm of numeric vectors (and the empty vector), the three- and
two-dimensional cross products, and the warning for invalid operands.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Lookup: Table Interpolation

**Tests**: `ScadBuiltinListTests.ScadBuiltinList_Lookup_InterpolatesAndClamps`,
`ScadBuiltinListTests.ScadBuiltinList_Lookup_UnsortedTable_StillFindsNeighbors`,
`ScadBuiltinListTests.ScadBuiltinList_Lookup_InvalidInput_SkipsRowsOrWarns`

Asserts linear interpolation, clamping outside the table, correct neighbors in an unsorted table, skipped
malformed rows, and `undef` with a warning when no usable row exists.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Search: Supported And Unsupported Forms

**Tests**: `ScadBuiltinListTests.ScadBuiltinList_Search_DefaultReturnsFirstIndexOrEmpty`,
`ScadBuiltinListTests.ScadBuiltinList_Search_Strings_MatchesScalars`, `ScadBuiltinListTests.ScadBuiltinList_Search_Rows_UsesColumn`,
`ScadBuiltinListTests.ScadBuiltinList_Search_ReturnsPerMatch_AllOrBounded`,
`ScadBuiltinListTests.ScadBuiltinList_Search_UnsupportedForms_ReportFeature`

Asserts the default single-index result, string matching by scalar, matching rows by column, the all-indices and
bounded-count forms, and that every other argument form reports the unsupported feature `scad-search-form` and
returns `undef`.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Strings: Text Functions

**Tests**: `ScadBuiltinStringTests.ScadBuiltinString_Str_ConcatenatesTextForms`,
`ScadBuiltinStringTests.ScadBuiltinString_Str_UnderGermanCulture_UsesInvariantNumbers`,
`ScadBuiltinStringTests.ScadBuiltinString_Chr_ConvertsCodePoints`,
`ScadBuiltinStringTests.ScadBuiltinString_Chr_InvalidInput_WarnsAndSkips`,
`ScadBuiltinStringTests.ScadBuiltinString_Ord_ReturnsFirstCodePoint`,
`ScadBuiltinStringTests.ScadBuiltinString_Ord_NonString_ReturnsUndefWithWarning`,
`ScadBuiltinStringTests.ScadBuiltinString_ChrOrd_RoundTrip`

Asserts the concatenation of the text forms of several values, invariant numbers under the German culture, code
point conversion for numbers and vectors, the warning and skip for invalid code points, `ord` of a first scalar
(including a surrogate pair) and `undef` for non-strings, and the round trip.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-TypeTests: Type Tests And Version

**Tests**: `ScadBuiltinRegistryTests.ScadBuiltinRegistry_TypeTests_ClassifyValues`,
`ScadBuiltinRegistryTests.ScadBuiltinRegistry_Version_ReportsEmulatedLanguageLevel`

Asserts each type test for every kind of value (`NaN` is a number) and the version vector and number.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Random: Deterministic Random Numbers

**Tests**: `ScadBuiltinRandomTests.ScadRandom_NextUInt64_MatchesGoldenSequence`,
`ScadBuiltinRandomTests.ScadRandom_NextDouble_MatchesGoldenValues`, `ScadBuiltinRandomTests.ScadRandom_DefaultState_MatchesGoldenSequence`,
`ScadBuiltinRandomTests.ScadBuiltinRandom_Rands_WithSeed_IsDeterministicAndRestartsEachCall`,
`ScadBuiltinRandomTests.ScadBuiltinRandom_Rands_ScalesToRange`, `ScadBuiltinRandomTests.ScadBuiltinRandom_Rands_WithoutSeed_AdvancesDeterministicStream`,
`ScadBuiltinRandomTests.ScadBuiltinRandom_Rands_NamedArguments_AreAccepted`, `ScadBuiltinRandomTests.ScadBuiltinRandom_Rands_Count_IsTruncated`,
`ScadBuiltinRandomTests.ScadBuiltinRandom_Rands_InvalidArguments_ReturnUndefWithWarning`

Asserts the golden 64-bit sequence for a seed, the golden doubles, and the golden sequence of the default state;
that a seeded call gives the same numbers every time and starts afresh per call; that values fall in the half-open
range; that unseeded calls continue one deterministic stream; that named arguments work; that the count is
truncated; and `undef` with a warning for invalid arguments.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-UnsupportedFunctions: Recognized But Not Implemented

**Tests**: `ScadBuiltinRegistryTests.ScadBuiltinRegistry_IsKnown_ImplementedAndUnsupportedNames_AreKnown`,
`ScadBuiltinRegistryTests.ScadBuiltinRegistry_UnknownName_IsNotHandled`,
`ScadBuiltinRegistryTests.ScadBuiltinRegistry_KnownUnsupported_ReportsFeatureAndReturnsUndef`

Asserts that implemented and known-unsupported names are known, that an unknown name is reported as not handled,
and that a known-unsupported name reports `scad-function-<name>` and returns `undef`.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Arguments: Positional And Named Binding

**Tests**: `ScadBuiltinRegistryTests.ScadBuiltinRegistry_NamedArgument_ToUnnamedFunction_IsIgnoredWithWarning`,
`ScadBuiltinRegistryTests.ScadBuiltinRegistry_UnknownNamedArgument_IsIgnoredWithWarning`,
`ScadBuiltinRegistryTests.ScadBuiltinRegistry_NamedArgument_OverridesPositional`,
`ScadBuiltinRegistryTests.ScadBuiltinRegistry_TooFewArguments_WarnsAndReturnsUndef`,
`ScadBuiltinRegistryTests.ScadBuiltinRegistry_ExtraArguments_AreDroppedWithWarning`,
`ScadBuiltinRegistryTests.ScadBuiltinRegistry_NamedArgumentGap_IsFilledWithUndef`

Asserts the exact warning texts and results for a named argument to a function without parameter names, an
unknown parameter name, a named argument overriding a positional one, too few and surplus arguments, and a skipped
parameter filled with `undef`.

##### CanvasNetScad-ScadEvaluation-ScadBuiltinFunctions-Limits: Resource Limits

**Tests**: `ScadBuiltinListTests.ScadBuiltinList_Concat_AtLimit_Succeeds_OneOver_Throws`,
`ScadBuiltinListTests.ScadBuiltinList_Search_TooMuchWorkOrResult_Throws`,
`ScadBuiltinStringTests.ScadBuiltinString_Str_AtLimit_Succeeds_OneOver_Throws`,
`ScadBuiltinStringTests.ScadBuiltinString_Str_LargeVector_ChargesPerElement`,
`ScadBuiltinStringTests.ScadBuiltinString_Str_StepLimit_StopsDuringFormatting`,
`ScadBuiltinStringTests.ScadBuiltinString_Chr_AtLimit_Succeeds_OneOver_Throws`,
`ScadBuiltinRandomTests.ScadBuiltinRandom_Rands_AtLimit_Succeeds_OneOver_Throws`

Asserts, for `concat`, `str`, `chr`, and `rands`, that a result exactly at the limit is produced and one more
throws the limit error before the result is built, and that a `search` that would do too much work or produce too
large a result throws. For `str`, it asserts that a large vector is charged at least eight steps per element and
that a step limit stops the call during formatting, not after it.

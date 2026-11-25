# System Verification Design

This document describes the system-level verification strategy for CanvasNetMathMl.

## Verification Approach

`CanvasNetMathMl` is verified at three tiers with xUnit v3 in the test project
`test/DemaConsulting.CanvasNet.MathMl.Tests/`, which references only the MathMl package and, through it, the core
`CanvasNet` package (no other format package, so the tests stay independent of the Viewer):

- **System tests** (described here) use only the public API, as a consumer would, apart from reading the box tree of a
  layout (`MathMlDocument.LayoutBox`) to locate the pixels and boxes they check: they open original-work equations from
  in-memory text, streams, and temporary files with `MathMlDocument.Parse` and `MathMlDocument.Open`, supply
  `MathMlOpenOptions`, `MathMlRenderOptions`, and `MathMlLimits`, and assert on observable outputs: the description
  properties, the exceptions with their codes, locations, and hints, the sizes, baselines, and pixels of rendered
  surfaces, and the reports delivered to the handlers. The system test classes are `MathMlSystemIntegrationTests`
  (reading, description, rendering, fractions, radicals, limits, cancellation, determinism, reports, strict mode,
  offline, notices) and
  `MathMlReferenceLayoutTests` (the layout of the original fixtures compared with the reference layouts that headless
  Chrome computes for them).
- **Subsystem tests** exercise each internal subsystem through its own entry points (see *Syntax Subsystem
  Verification Design* and *Layout Subsystem Verification Design*).
- **Unit tests** exercise each unit in isolation (see the unit verification documents).

The system test methods are prefixed `CanvasNetMathMl_`, the subsystem test methods `Syntax_` and `Layout_`, and the
unit test methods with the name of their unit (`CharacterReferences_...`, `TokenLayout_...`). No other system of the
repository has test methods with these names, so the requirements trace never mixes the evidence of two systems.

The **Chrome reference layouts** are made offline by the developer script `tools/visual-diff/update-mathml-goldens.ps1`
(headless Chrome through the puppeteer of the repository's pinned mermaid-cli, with the bundled Noto Sans Math font, at
100 CSS pixels) and committed next to their fixtures as `*.layout.json`; no browser runs at test time. The tolerance
(1 CSS pixel plus Chrome's layout unit of 1/64 pixel), the Chrome version, the behaviors matched, and the documented
exceptions are recorded in `Fixtures/reference/README.md`.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on Windows, Linux, and macOS, for net8.0, net9.0, net10.0, and
  net11.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Files**: temporary files written and deleted by the tests, the fixtures and reference layouts of
  `Fixtures/reference/`, and the third-party notices copied to the test output; no network, no browser
- **Mocking**: None
- **Platform subset**: `CanvasNetMathMl_Render_TokensAndRow_PaintsExactPixels` and`
  `CanvasNetMathMl_Render_StretchedParentheses_PaintsExactPixels`carry`[Trait("Category", "Platform")]` and are the
  evidence of the platform requirements on every operating system and runtime

## Acceptance Criteria

A system-level test run passes when every scenario below passes without an exception beyond those explicitly asserted,
on every platform and runtime of the platform requirements. A size, a baseline, a pixel, a code, a location, or a hint
that differs from the expected value, or a reference box outside the tolerance, constitutes a failure.

## Test Scenarios

### CanvasNetMathMl-Open: The Entry Points Agree

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Open_StringStreamAndPath_Agree`

Opens the same block equation (with a comment, a named reference, and an annotation) from a string, a stream, and a
temporary file, and asserts that all three have the display `Block`, 7 elements, and the alternative text, that the
file's source name is its path, and that the three measure the same.

### CanvasNetMathMl-LocatedErrors: Invalid Text Fails Located

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Open_InvalidXml_FailsLocated`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Open_UnknownNamedReference_FailsLocatedWithHint`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Open_DocumentTypeDeclaration_IsRefused`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Open_ForeignEncodingDeclaration_IsRefused`

Asserts `mathml-syntax-error` with a hint for a mismatched end tag (`bad.mml`, line 2), an unknown name after a
rewritten one (line 2, column 12, the hint naming `&#x2062;`), a document type declaration (line 1, the hint naming the
document type), and an XML declaration of ISO-8859-1 (line 1, column 21, the hint naming UTF-8).

### CanvasNetMathMl-CharacterReferences: Named References Are Decoded

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Open_NamedReferences_AreDecoded`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Open_UnknownNamedReference_FailsLocatedWithHint`

Parses `&alpha;`, `&lt;`, and `&#x3B2;` in an attribute and `&alpha;`, `&InvisibleTimes;`, and `&Bopf;` in tokens, and
asserts the decoded alternative text and token texts (an astral character included); an unknown name fails located.

### CanvasNetMathMl-Describe: The Description

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Describe_ReportsDisplayCountAltTextAndAnnotations`

Asserts the display `Block`, 7 elements, the alternative text, and the TeX annotation with its encoding.

### CanvasNetMathMl-Tokens: Tokens Are Drawn

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_TokensAndRow_PaintsExactPixels`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_AutoItalicAndMathVariant_UseMathAlphanumerics`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceGoldens_CoverEveryFixture`

Asserts that a single-character `mi` renders exactly like its mathematical italic written directly and unlike the
upright letter, that `mathvariant="double-struck"` renders exactly like the double-struck letter written directly, the
inked operator of the exact-pixel equation, and that the fixtures `tokens-mi-italic`, `tokens-mn-digits`,
`tokens-mtext-space`, `tokens-ms`, `mspace`, and `mathvariant-normal` match the boxes Chrome computes, with every
fixture paired with a golden.

### CanvasNetMathMl-Rows: Rows And Grouping Elements

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_TokensAndRow_PaintsExactPixels`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts the pixels of a row with a styled group and an `merror` (the background between the tokens, the red border) and
that the fixtures `row-nested`, `mpadded`, `mphantom`, `mstyle-scriptlevel`, and `merror` match Chrome's boxes.

### CanvasNetMathMl-Operators: Dictionary Spacing

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Operators_SpacingFollowsDictionary`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts at 18 px that an infix plus adds 4 px on each side, a prefix minus nothing, and a comma 3 px after it only
(measured against the same equations with `lspace="0"` and `rspace="0"`), and that the fixtures `operators-spacing`
(categories A, B, C, K, M, and Default) and `operators-forms` (prefix, infix, and postfix forms, an explicit form,
`lspace` and `rspace` with a percentage, a large operator inline, a text operator, a prime, the ForceDefault vertical
line, and two-character operators) match Chrome's boxes.

### CanvasNetMathMl-StretchyOperators: Stretched And Large Operators

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_StretchedParentheses_PaintsExactPixels`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_LargeOperatorInDisplayStyle_IsTaller`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_StretchUnavailable_IsReported`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts the exact pixels of parentheses stretched around an `mspace` of 4 em by 3 em at 32 px: pure black in the middle
of the stroke of every extender, ink in every row between the ends of each parenthesis, and no ink just outside them;
that a summation is taller in display style than inline; that a stretchy `x` beside a tall `mspace` is reported once as
`mathml-stretch-unavailable` at line 2, column 2, and still drawn; and that the fixtures `stretchy-parens` (base
glyph, variants, and an assembly), `stretchy-brackets-braces`, `stretchy-symmetric` (symmetric and non-symmetric
targets, `stretchy="false"`, an operator without a construction), `largeop-display`, `largeop-inline`, and
`minsize-maxsize` (lengths and percentages, all children stretchy, embellished operators in `mrow` and `mstyle`)
match Chrome's boxes.

### CanvasNetMathMl-Fractions: Fractions Are Drawn

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_FractionBar_PaintsExactPixels`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Fractions_LegacyAttributes_AreReported`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts at 32 px that a `linethickness="4px"` bar between two empty children is 4 px thick, 30 px wide after the 1 px
padding, centered on the math axis, pure black in every pixel inside it, and transparent above, below, and in the
padding; that `bevelled`, `numalign`, and `denomalign` are reported as three `mathml-attribute-unsupported` reports
while the fraction renders pixel for pixel like the one without them; and that the fixtures `mfrac-basic` (inline and
display style), `mfrac-linethickness` (`0`, `0px`, `200%`, `3px`, `0.2em`, and a negative value), `mfrac-nested`
(continued fractions, inline and display, whose levels follow `math-depth: auto-add`), `mfrac-operators` (fractions
between operators and an embellished fraction spaced as its operator), and `mfrac-fenced` (parentheses and brackets
stretched around fractions) match Chrome's boxes.

### CanvasNetMathMl-Radicals: Radicals Are Drawn

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_SquareRoot_PaintsSurdAndOverbar`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts at 32 px that the overbar of a square root of an empty 40 px base is pure black along its middle row over the
whole base, that nothing is painted over the empty base under it, and that the radical symbol is inked left of the base;
and that the fixtures `msqrt-basic` (one and several children, operators that infer their forms), `msqrt-tall`
(fractions and tall `mspace` bases that select size variants and the glyph assembly), and `mroot-index` (a cube root
with a narrow index, a wide index, and nested roots in display style) match Chrome's boxes.

### CanvasNetMathMl-ChildCount: Invalid Fractions And Roots

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_ChildCount_IsReportedAndLaidOutAsRow`

Renders in strict mode an `mfrac` with one child and an `mroot` with three and asserts that no unsupported feature is
reported and nothing throws, that two `mathml-child-count` warnings are delivered at lines 2 and 3, column 2, that the
root is a row of its three children on its baseline, and that the picture has ink.

### CanvasNetMathMl-Style: Style Attributes Apply

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_StyleAttributes_ApplyColorBackgroundAndSize`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts that `mathcolor="red"`, `mathbackground="lime"`, and `mathsize="200%"` give red ink, a lime background, no black
pixel, and twice the width (within 2 pixels), and that the `mstyle-scriptlevel` fixture and the fraction and radical
fixtures, whose children are drawn at the script levels of the user agent rules, match Chrome.

### CanvasNetMathMl-MathFont: The Bundled Math Font And Its Fallback

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_MissingGlyph_FallsBackAndReports`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts that `café` renders ink without a warning (the accented letter from a fallback font), that U+E000 renders the
missing-glyph box with one `mathml-text-missing-glyph` warning naming the code point, and that the reference fixtures,
laid out with the metrics of Noto Sans Math, match Chrome.

### CanvasNetMathMl-Render: Measure And Render

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Measure_MatchesRenderAndReportsBaseline`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_FitsExactSize`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_StyleAttributes_ApplyColorBackgroundAndSize`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_TokensAndRow_PaintsExactPixels`

Asserts that the measured size equals the rendered size at scale 1.5 and that the digits are inked just above the
baseline row and not below it; that a 300 by 200 render keeps a white margin above and below the centered equation with
ink near both sides, and that a zero width is refused; and the exact pixels (a transparent corner, the blue background,
the red border, the black operator) at positions derived from the layout.

### CanvasNetMathMl-Limits: Untrusted Input Is Bounded

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Limits_OversizedText_IsRefused`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Limits_DeepNesting_IsRefusedWithoutStackOverflow`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Limits_HugeStretch_IsRefused`

Asserts that a text and a stream over a size limit of 100 throw `InvalidDataException` naming
`MathMlLimits.MaxTextSize`, that 10,000 nested rows fail with `mathml-limit-exceeded` naming
`MathMlLimits.MaxNestingDepth` instead of a stack overflow, and that a parenthesis beside an `mspace` of 50 em fails
with `mathml-limit-exceeded` at the operator, naming `MathMlLimits.MaxAssemblyParts`, when the limit is 20 glyphs, and
renders within the default limits.

### CanvasNetMathMl-Cancellation: A Canceled Token Stops The Work

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_PreCanceledToken_Throws`

Asserts that `Measure`, `Render`, and `Render(width, height)` throw `OperationCanceledException` for a pre-canceled
token.

### CanvasNetMathMl-Determinism: Identical Pixels

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_IsDeterministicAcrossCallsAndThreads`

Renders the same document eight times in parallel and asserts that every surface equals the reference pixel for pixel.

### CanvasNetMathMl-UnsupportedFeatures: Elements Not Drawn Are Rows With Reports

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_UnsupportedElements_AreDrawnAsRowsAndReported`

Measures an equation with an `menclose` and an `mfenced` and asserts the same size as the equivalent rows, with two
`mathml-element-unsupported` reports at lines 2 and 3, column 2.

### CanvasNetMathMl-StrictMode: The First Unsupported Feature Throws

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_StrictMode_FirstUnsupportedFeatureThrows`

Renders an equation with an `menclose` and an `mfenced` in strict mode and asserts a `MathMlUnsupportedFeatureException`
for `<menclose>` after exactly one report reached the handler.

### CanvasNetMathMl-Offline: Links Are Not Followed

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Links_AreReportedNotFetched`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Open_DocumentTypeDeclaration_IsRefused`

Renders tokens with an `https` `href` and a `file` `xlink:href` and asserts two `mathml-attribute-unsupported` reports
that say the links are not followed; a document type declaration (the vehicle of external entities) is refused.

### CanvasNetMathMl-ThirdPartyNotices: The Notices Ship With The Package

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_ThirdPartyNotices_ArePresent`

Asserts that the W3C and Unicode license texts and the provenance table ship next to the package, that the W3C
notice names the W3C entity file and Appendices B.1, B.4, and C.1 of MathML Core as its sources, and that the provenance
table names both license files and states that the package is not affiliated.

### CanvasNetMathMl-Platform-Windows, Linux, MacOS, Net8, Net9, Net10, Net11: Every Platform And Runtime

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_TokensAndRow_PaintsExactPixels` and
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_StretchedParentheses_PaintsExactPixels` (with the `windows@`,
`ubuntu@`, `macos@`, `net8.0@`, `net9.0@`, `net10.0@`, and `net11.0@` source filters)

The exact-pixel tests read, lay out, and paint equations with the bundled math font and its `MATH` table (tokens, rows,
and parentheses stretched from glyph assemblies) on every operating system and runtime of the CI matrix; their results
from each are the evidence of the platform requirement of that platform.

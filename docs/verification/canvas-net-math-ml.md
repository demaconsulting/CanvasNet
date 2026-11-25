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
  (reading, description, rendering, fractions, radicals, scripts, under- and overscripts, horizontal stretching,
  tables, the image source, limits, cancellation, determinism, reports, strict mode, offline, notices),
  `MathMlReferenceLayoutTests` (the layout of the original fixtures compared with the reference layouts that headless
  Chrome computes for them, and the MathML 3 table attributes compared with headless Firefox), and
  `MathMlHardeningTests` (table, span, stretch, nesting, and length limits with deterministic work measures, and a
  seeded mutation fuzzer), and `MathMlGalleryTests` (the equations of the user guide and README galleries, rendered in
  strict mode).
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
exceptions are recorded in `Fixtures/reference/README.md`. The **Firefox reference layouts** of the MathML 3 table
attributes (which Chrome ignores) are made by the same script with `-Browser Firefox` (headless Firefox through the same
puppeteer, after a one-time `npx puppeteer browsers install firefox`) into `Fixtures/reference-firefox/`; they compare
only the boxes of the tables, rows, cells, and `mspace` contents relative to their table, within 1 CSS pixel plus
Firefox's layout unit of 1/60 pixel, as recorded in `Fixtures/reference-firefox/README.md`. Neither browser runs at test
time, and no test reads a clock.

## Test Environment

- **Framework**: xUnit v3 running under the .NET SDK on Windows, Linux, and macOS, for net8.0, net9.0, net10.0, and
  net11.0
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Files**: temporary files written and deleted by the tests, the fixtures and reference layouts of
  `Fixtures/reference/` and `Fixtures/reference-firefox/`, the gallery equations, and the third-party notices copied to
  the test output; no network, no browser
- **Mocking**: None
- **Platform subset**: `CanvasNetMathMl_Render_TokensAndRow_PaintsExactPixels` and
  `CanvasNetMathMl_Render_StretchedParentheses_PaintsExactPixels` carry `[Trait("Category", "Platform")]` and are the
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
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_Overbrace_StretchesHorizontally`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts the exact pixels of parentheses stretched around an `mspace` of 4 em by 3 em at 32 px: pure black in the middle
of the stroke of every extender, ink in every row between the ends of each parenthesis, and no ink just outside them;
that a summation is taller in display style than inline; that a stretchy `x` beside a tall `mspace` is reported once as
`mathml-stretch-unavailable` at line 2, column 2, and still drawn; and that the fixtures `stretchy-parens` (base
glyph, variants, and an assembly), `stretchy-brackets-braces`, `stretchy-symmetric` (symmetric and non-symmetric
targets, `stretchy="false"`, an operator without a construction), `largeop-display`, `largeop-inline`, and
`minsize-maxsize` (lengths and percentages, all children stretchy, embellished operators in `mrow` and `mstyle`)
match Chrome's boxes. The horizontal stretching is asserted by `CanvasNetMathMl_Render_Overbrace_StretchesHorizontally`
(see *CanvasNetMathMl-UnderOver*).

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

### CanvasNetMathMl-Scripts: Scripts Are Drawn

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_ScriptsAndLimits_PlaceScripts`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts at 40 px in a block equation that the ink of the superscript of `x²` starts at the right of its base and above
its top, that the ink of the subscript of `a₁` starts at the right of its base and below its bottom, and that nothing is
reported; and that the fixtures `msub-msup` (letter, italic *f*, digit, and row bases), `msubsup-gap` (the gap of both
scripts with `mspace` scripts, the cramped superscript under a radical and in a denominator), `scripts-largeop`
(integrals inline and in display style, whose subscript follows the italic correction of the large variant),
`mmultiscripts-tensor` (postscript pairs with `none`), `mmultiscripts-prescripts` (the nuclide notation and prescripts
with `none`), and `scripts-embellished-fence` (scripts on a fenced row, embellished scripted operators spaced by their
operators) match Chrome's boxes.

### CanvasNetMathMl-UnderOver: Under- And Overscripts Are Drawn

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_ScriptsAndLimits_PlaceScripts`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_MovableLimits_FollowDisplayStyle`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_Overbrace_StretchesHorizontally`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts at 40 px that the limits of a summation in display style are inked above and below it, the upper limit centered
on it within 3 pixels; that the limits of the same summation are scripts after it in an inline equation (so the block
picture is taller and narrower); that an over brace is assembled at least as wide as the expression under it, starting
within a pixel of its left edge, with ink in more than 90% of its columns; that a caret over two letters keeps its glyph
silently while the same caret with `stretchy="true"` is reported once as `mathml-stretch-unavailable` at line 4, column
56; and that the fixtures `munderover-limits` (summation, union, and product limits in display style),
`movablelimits-inline` (limits moved to the script positions inline, `movablelimits="false"`, a text operator, and a
letter base), `mover-accents` (stretched combining circumflexes, a tilde, an accent `munderover` with an upper-case
`TRUE`, an accent underline, and an overscript that is not an accent), `overbrace-underbrace` (braces stretched over and
under rows, a labeled brace, and a brace base with a label), and `arrow-over` (arrows as bases with labels at the
stretch stack gaps and as an overscript) match Chrome's boxes, the arrow values and the height of the under brace within
the documented exceptions.

### CanvasNetMathMl-Tables: Tables Are Drawn

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_Matrix_CenteredOnTheAxis`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Renders at 32 px a framed 2 by 2 table after `A =` and asserts that nothing is reported, that the table's ascent exceeds
its descent by twice AxisHeight (the table is centered on the math axis), that a cell's content starts 0.4em into the
cell, that the frame is inked along the left edge of the table, and that the inside of an empty cell is transparent; and
that the fixtures `table-matrix` (a 3 by 3 matrix of letters and digits), `table-spans` (`columnspan`, `rowspan`, and
`rowspan="0"`, whose excess Chrome spreads in proportion), `table-fenced-matrix` (a matrix in stretched parentheses next
to a fraction in a display equation), and `table-cell-baselines` (fractions, scripts, a radical, and a deep `mspace` in
cells, compact in a table) match Chrome's boxes.

### CanvasNetMathMl-TableExtensions: The MathML 3 Table Attributes

**Tests**: `MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesFirefox`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceGoldens_CoverEveryFixture`

Asserts that the boxes of the tables, rows, cells, and `mspace` contents of the Firefox fixtures, relative to their
table, match headless Firefox within 1 pixel plus 1/60 pixel: `table-columnalign` (`columnalign="left right"` with the
last value repeated, an `mtr` and an `mtd` override), `table-rowalign` (`top`, `bottom`, `center`, `baseline`, and
`axis` rows, an `mtr` and an `mtd` override), `table-spacing` (`columnspacing`, `rowspacing`, and `framespacing` lists
with a frame, and zero spacing with a dashed frame), and `table-lines-frame` (`columnlines`, `rowlines`, and a solid and
a dashed `frame`); and that every fixture of both reference folders has its golden. The reports of invalid values and of
the other legacy attributes are verified by the unit tests of `TableLayout`.

### CanvasNetMathMl-ImageSource: Linked Equations Are Drawn

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_ImageSource_DrawsALinkedEquation`

Through a `FolderImageResolver` over the reference fixtures with a `MathMlImageSource`, asserts that `table-matrix.mml`
is drawn at the size `Measure` gives without notes, that `table-fenced-matrix.mml` is fitted into 100 by 100 pixels,
that a link with `#display=block` is drawn without notes, that a link with `#display=wide` is not drawn and is reported
with the invalid-fragment code, and that the resolver claims `.mml`.

### CanvasNetMathMl-ChildCount: Invalid Fractions, Roots, And Tables

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_ChildCount_IsReportedAndLaidOutAsRow`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_MutatedFixtures_ThrowOnlyDocumentedExceptions`

Renders in strict mode an `mfrac` with one child and an `mroot` with three and asserts that no unsupported feature is
reported and nothing throws, that two `mathml-child-count` warnings are delivered at lines 2 and 3, column 2, that the
root is a row of its three children on its baseline, and that the picture has ink; the fuzzer, which inserts stray table
elements, fractions, and scripts into every fixture, asserts that such markup never throws beyond the documented
exceptions. The anonymous rows and cells of stray table children are verified by the unit tests of `LayoutEngine`.

### CanvasNetMathMl-Style: Style Attributes Apply

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_StyleAttributes_ApplyColorBackgroundAndSize`,
`MathMlReferenceLayoutTests.CanvasNetMathMl_ReferenceLayout_MatchesChrome`

Asserts that `mathcolor="red"`, `mathbackground="lime"`, and `mathsize="200%"` give red ink, a lime background, no black
pixel, and twice the width (within 2 pixels), and that the `mstyle-scriptlevel` fixture and the fraction, radical,
script, and under- and overscript fixtures, whose children are drawn at the script levels of the user agent rules (an
accent at the size of its base), match Chrome.

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
`MathMlSystemIntegrationTests.CanvasNetMathMl_Render_TokensAndRow_PaintsExactPixels`,
`MathMlGalleryTests.CanvasNetMathMl_Gallery_RendersStrictWithoutWarnings`,
`MathMlGalleryTests.CanvasNetMathMl_Gallery_ShowsTheMajorConstructs`

Asserts that the measured size equals the rendered size at scale 1.5 and that the digits are inked just above the
baseline row and not below it; that a 300 by 200 render keeps a white margin above and below the centered equation with
ink near both sides, and that a zero width is refused; and the exact pixels (a transparent corner, the blue background,
the red border, the black operator) at positions derived from the layout.

The gallery tests render every equation of the documentation galleries (the user guide gallery
`docs/user_guide/examples/mathml-*.mml` and the README gallery `docs/images/mathml/*.mml`, copied next to the test
assembly) in strict mode, and assert for each that no warning and no unsupported feature is reported, that the measured
size equals the rendered size, and that more than 2% of the pixels are inked. They also assert that the user guide
gallery holds at least 12 equations and the README gallery 6, that every equation is a block equation with a text
alternative, and that the user guide gallery together uses `mfrac`, `msqrt`, `mroot`, `msubsup`, `munderover`,
`mover`, `mtable`, and `mmultiscripts`, so the published equations can never use a construct that is only reported.

### CanvasNetMathMl-Limits: Untrusted Input Is Bounded

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Limits_OversizedText_IsRefused`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Limits_DeepNesting_IsRefusedWithoutStackOverflow`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Limits_HugeStretch_IsRefused`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_MutatedFixtures_ThrowOnlyDocumentedExceptions`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_TableCells_AreLimited`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_HugeSpans_FailBeforeAllocation`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_LayoutWork_ChargesTableSlots`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_HorizontalStretch_IsLimited`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_DeepFractions_AreBounded`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_HugeLengths_AreRefused`,
`MathMlHardeningTests.CanvasNetMathMl_Hardening_LargeTablesWithinLimits_Render`

Asserts that a text and a stream over a size limit of 100 throw `InvalidDataException` naming
`MathMlLimits.MaxTextSize`, that 10,000 nested rows fail with `mathml-limit-exceeded` naming
`MathMlLimits.MaxNestingDepth` instead of a stack overflow, and that a parenthesis beside an `mspace` of 50 em fails
with `mathml-limit-exceeded` at the operator, naming `MathMlLimits.MaxAssemblyParts`, when the limit is 20 glyphs, and
renders within the default limits. The hardening tests measure work and limits, never time: a 20 by 20 table fails
under 399 table cells with a located, hinted error naming `MathMlLimits.MaxTableCells` and renders under the defaults;
a cell spanning 1,000 columns and 200 rows fails on `MaxTableCells`, not on a work budget of 5,000, so the grid was
neither allocated nor charged; spans that make 50,000 grid slots exceed a work budget of 20,000 that the same elements
without spans fit in; an arrow stretched under a 400 em script fails on 20 assembly parts; fractions nested 300 deep
fail while read on `MaxNestingDepth`, and 100 deep fail a work budget of 200 and render under the defaults; a
`linethickness` and a `columnspacing` of 99,999,999 em fail on `Surface.MaxDimension`; and a 40 by 40 table with spans,
lines, and a frame renders identically twice. A seeded fuzzer (`Random(20261125)`, 24 mutations of every reference
fixture: truncations, deletions, inserted MathML elements, attributes, and references, swapped lines, and flipped
characters, every third run in strict mode) opens, measures, and renders each text and asserts that only
`MathMlSyntaxException` and `MathMlRenderException` with a `mathml-*` code and a location (a text without markup has no
location), `MathMlUnsupportedFeatureException` in strict mode, and `InvalidDataException` occur, and that every
successful render has the measured size.

### CanvasNetMathMl-Cancellation: A Canceled Token Stops The Work

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_PreCanceledToken_Throws`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_ImageSource_PreCanceledRequest_Throws`

Asserts that `Measure`, `Render`, and `Render(width, height)` throw `OperationCanceledException` for a pre-canceled
token, and that a `FolderImageResolver` with a `MathMlImageSource` and a pre-canceled token throws it when it renders
`table-matrix.mml`, so the cancellation reaches the image source through the composition hook (its unit detail is
verified by the unit tests of `MathMlDocument`).

### CanvasNetMathMl-Determinism: Identical Pixels

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Render_IsDeterministicAcrossCallsAndThreads`

Renders the same document eight times in parallel and asserts that every surface equals the reference pixel for pixel.

### CanvasNetMathMl-UnsupportedFeatures: Elements Outside MathML Core Are Rows With Reports

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_UnsupportedElements_AreDrawnAsRowsAndReported`

Measures an equation with an `menclose` and an `mfenced` and asserts the same size as the equivalent rows, with two
`mathml-element-unsupported` reports at lines 2 and 3, column 2.

### CanvasNetMathMl-StrictMode: The First Unsupported Feature Throws

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_StrictMode_FirstUnsupportedFeatureThrows`

Renders an equation with an `menclose` and an `mfenced` in strict mode and asserts a `MathMlUnsupportedFeatureException`
for `<menclose>` after exactly one report reached the handler.

### CanvasNetMathMl-Offline: Links Are Not Followed

**Tests**: `MathMlSystemIntegrationTests.CanvasNetMathMl_Links_AreReportedNotFetched`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_Open_DocumentTypeDeclaration_IsRefused`,
`MathMlSystemIntegrationTests.CanvasNetMathMl_ImageSource_ReadsOnlyTheRequestContent`

Renders tokens with an `https` `href` and a `file` `xlink:href` and asserts two `mathml-attribute-unsupported` reports
that say the links are not followed; a document type declaration (the vehicle of external entities) is refused. Through
a `FolderImageResolver` with a `MathMlImageSource` and a file-access policy that records every file it is asked about,
an equation in a temporary folder whose tokens link a file beside it (`decoy.mml`) and a web address is drawn, the
policy is asked about the linked equation only (the other file is never opened), and the two links are reported as
`mathml-attribute-unsupported`.

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

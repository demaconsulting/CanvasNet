### CssPaint Unit Verification Design

This document describes the unit-level verification strategy for the `CssPaint` unit in the
`DemaConsulting.CanvasNet.VegaLite.Scenegraph` namespace: the internal static `CssPaint` class (`IsNone`,
`TryResolvePaint`, and `WithOpacity`). The unit resolves a paint (a color string and an opacity) to the color that is
drawn, on top of the shared CSS color grammar of the core `CssColor` unit, whose own tests verify the grammar.

#### Verification Approach

The unit is verified by resolving paints and comparing them with the pixel values that CSS defines for the text, which
the tests state as `Rgba32` literals worked out by hand: `#000` at the opacity 0.5 has the alpha 0.5 x 255 = 127.5, which
rounds to 128; `rgba(0,0,0,0.5)` at 0.5 has the alpha 128 x 0.5 = 64; `#123456` at the opacity 7 clamps to opaque;
`steelblue` is (70, 130, 180, 255); `#abcd` is (170, 187, 204, 221); and `hsl(120 100% 25%)` is (0, 128, 0). The last
three prove that the unit uses the shared grammar (a named color, a short hexadecimal with alpha, and an `hsl()`
function). The unit has no dependencies other than the core `Rgba32` and `CssColor`, so no mocking or stubbing is
required; the internal type is reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `CssPaintTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Scenegraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Pure functions of text; no files, threads, or shared state

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any paint that resolves to a value other than the hand-derived pixel, or that is resolved when none is
documented, constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetVegaLite-Scenegraph-CssPaint-NoPaint: No Paint Resolves To Nothing

**Tests**: `CssPaintTests.CssPaint_IsNone_NullEmptyAndNone_AreNoPaint`,
`CssPaintTests.CssPaint_TryResolvePaint_NothingToDraw_ReturnsFalseWithDefaultColor`

Asserts that null, empty, blank, and ` NONE ` are no paint while `transparent` and `#000` are colors, and that no paint,
`none`, garbage text, `transparent`, `rgba(0,0,0,0)`, and an opacity of 0 each resolve to nothing with the default color.

##### CanvasNetVegaLite-Scenegraph-CssPaint-Opacity: Opacity Multiplies The Alpha

**Tests**: `CssPaintTests.CssPaint_TryResolvePaint_Opacity_MultipliesTheAlphaAndRoundsHalfAwayFromZero`

Asserts that the opacity multiplies the alpha of the color (`#000` at 0.5 is alpha 128, `rgba(0,0,0,0.5)` at 0.5 is
alpha 64 because 0.25 x 255 = 63.75 rounds to 64), that an opacity above 1 keeps the color opaque, that a negative
opacity gives alpha 0, and that NaN counts as 1.

##### CanvasNetVegaLite-Scenegraph-CssPaint-SharedGrammar: Paints Use The Shared CSS Grammar

**Tests**: `CssPaintTests.CssPaint_TryResolvePaint_ColorText_UsesTheSharedCssGrammar`

Asserts that a named color, a short hexadecimal with alpha, and an `hsl()` function resolve to the pixels CSS defines
for them.

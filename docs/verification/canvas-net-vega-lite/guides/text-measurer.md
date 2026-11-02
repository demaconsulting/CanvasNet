### TextMeasurer Unit Verification Design

This document describes the unit-level verification strategy for the `TextMeasurer` unit in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: the internal `TextMeasurer` class (`Resolve`, `MeasureWidth`,
`Truncate`, `TextValue`, `Measure`, `GetBounds`, `GetMetrics`, and the static `BaselineOffset`, `LineHeight`,
`RoundHalfUp`, `SplitLines`, `IsMappedFamily`, `IsBold`, and `IsItalic`), and the `TextFont`, `TextExtent`, and
`FontMetrics` types of its file (the resolved face is the core `ResolvedFont`). The unit resolves CSS family lists to
fonts through the core `FontResolver` (the bundled Liberation fonts by default, additional directories and installed fonts
only on request), measures text with
the advance widths of the resolved font, and applies the Vega text rules for truncation, line height, baselines, and
bounds.

#### Verification Approach

The unit is verified by resolving families and measuring texts with the real bundled fonts, and by comparing the
results with values that are either derived by hand from the Vega constants or computed independently from the core
`TextRenderer.MeasureText` of the same font. No mocking or stubbing is used: font resolution and measurement are the
contract of the unit, and the internal types are reached through the test assembly's `InternalsVisibleTo` access.

The Vega constants are verified with hand-derived numbers. The line height defaults to the font size plus 2 (12 for a
10 pixel font), so three lines of a 10 pixel font are 10 + 2 x 12 = 34 pixels high and 50 with a line height of 20.
The baseline offsets for a 10 pixel font are the JavaScript rounding (`Math.round`, half up) of 0.79 x size for `top`
(7.9 gives 8), 0.3 x size for `middle` (3), and -0.21 x size for `bottom` (-2.1 gives -2); the line-based baselines are
0.29 x size (2.9) plus or minus half the line height of 12, so `line-top` is 2.9 + 6 = 8.9, which gives 9, and
`line-bottom` is 2.9 - 6 = -3.1, which gives -3; and `alphabetic` is 0. For the 13 pixel title they are 10.27 (10),
3.9 (4), and -2.73 (-3); an explicit line height of 20 gives 2.9 + 10 = 13 and 2.9 - 10 = -7; and the half-way case
-0.21 x 50 = -10.5 rounds up to -10. The text bounds follow Vega: for a 13 pixel `top`-baseline bold text anchored at
(10, 20), the box starts at the anchor and is as wide as the measured text and as high as the font (the baseline
offset 10 minus the 80% box offset 10 is 0, so the box ends at 20 + 13 = 33); centering moves the box by half its
width; rotating by -90 degrees turns the width into the height; three lines are 10 + 2 x 12 high; and an alphabetic
baseline sits at the box's 80% line, so the box of a 10 pixel text spans from -8 to 2.

Truncation follows Vega's rule, which the tests verify as properties of the result for a 100 pixel limit and the text
"The quick brown fox jumps over the lazy dog": the result ends with the ellipsis, its prefix is a prefix of the text,
it is narrower than the limit, and the same prefix plus one more character plus the ellipsis would not fit; a text that
fits is unchanged; a limit of 1 leaves only the ellipsis; and cutting at the start (right to left) keeps the end.

Unit tests reside in `TextMeasurerTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Guides` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts. The one test of the system-font opt-in picks an installed family that the
  measurer does not map itself and skips itself (`Assert.Skip`) on a machine that has none; the checks before the skip
  (a family that does not exist resolves to the bundled sans-serif font, with and without system fonts) always run
- **Files**: The additional-directory tests copy one bundled font resource (Noto Sans) into a per-test temporary
  directory next to files that are not fonts (one directory, and one seven levels deep), and delete the directory afterwards
- **Isolation**: Each test creates its own measurer

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any family that does not resolve to the documented font, any width that differs from the core text measure, any
truncation that violates the rule, any baseline or bounds value that differs from the hand-derived number, or any
dependence on installed fonts in a test that must not have it constitutes a failure.

#### Unit-Level Test Scenarios

##### Default And Generic Families Resolve To The Bundled Fonts

**Tests**: `TextMeasurerTests.TextMeasurer_Resolve_DefaultUsesBundledFonts`

Asserts, for a measurer with system fonts off and one with them on, that no family, an empty and a blank family,
`sans-serif`, `system-ui`, `Helvetica`, `Arial`, `Helvetica Neue`, `Verdana`, `ARIAL`, quoted lists, `Segoe UI`, and
families that do not exist all resolve to the very same bundled Liberation Sans font object; that `serif`, `Times`,
`Times New Roman`, `Georgia`, and lists ending in `serif` resolve to Liberation Serif; and that `monospace`,
`Courier`, `Courier New`, `Menlo`, `Consolas`, and `ui-monospace, monospace` resolve to Liberation Mono, in every case
with the source `Bundled`.

##### Weight And Style Select The Face

**Tests**: `TextMeasurerTests.TextMeasurer_Resolve_WeightAndStyleSelectTheFace`

Asserts that weights `bold`, `700`, and `600` select the bold face and `599` and `normal` the regular face; that
`italic` and `oblique` select the italic face; that a bold italic serif is the bold italic Liberation Serif; that
`bolder` is bold, `lighter` and null are not, and `normal` is not italic; and that resolving the same font twice
returns the same object.

##### Named Families Need The System Option Or A Directory

**Tests**: `TextMeasurerTests.TextMeasurer_Resolve_NamedFamilyNeedsSystemFontsOrDirectories`,
`TextMeasurerTests.TextMeasurer_Resolve_AdditionalDirectoryFontIsFoundByFamilyName`,
`TextMeasurerTests.TextMeasurer_Resolve_AdditionalDirectoryScanIsRecursive`

Asserts that a family that does not exist resolves to the bundled sans-serif; that, on a machine that has one, an
installed family that the measurer does not map itself (the test asks `TextMeasurer.IsMappedFamily`, so a name such as
`Helvetica Neue` that macOS installs is not chosen), that a family list can carry unchanged and whose face loads, is
ignored by default and by an explicit `UseSystemFonts = false`, is loaded from the host
(case-insensitive) with `UseSystemFonts`, and that a missing first family in a list falls through to it; that a font
file in an additional directory is found by family name (case-insensitive, also from a list and with a requested weight
the file does not have), with a missing directory, a broken file, and a text file ignored, that generic families never
come from the directories, that without the option the family is not found, and that the directory font measures
differently from the fallback, so the option really changes the layout; and that a font file seven directories deep in an
additional directory is found (the scan is the core scanner's: recursive without the earlier depth limit, with its
bounds, determinism, and link policy verified by the `FontDirectoryScanner` unit tests and the resolution order by the
`FontResolver` unit tests).

##### Measurement, Truncation, And Line Blocks

**Tests**: `TextMeasurerTests.TextMeasurer_Measure_TruncatesAndSplitsLines`

Asserts that lines split at line feeds, carriage returns, and both (`a\nb\r\nc\rd` gives four lines) and that null and
empty text have none; that the width equals the core `TextRenderer.MeasureText` advance width of the bundled font,
scales linearly with the size (20 pixels gives twice 10 pixels), is zero for empty text or size zero, is larger for
bold, and is equal for `iiii` and `WWWW` in the monospace font; the truncation properties derived above; that drawn
text is trimmed and truncated only when a limit is set; the block heights 34, 50, and 10 (a single line and no lines
are the font size high, no lines have no width); and that truncation is part of the measurement, so the widest line of
a block is the limited one.

##### Baselines, Line Height, And Rounding

**Tests**: `TextMeasurerTests.TextMeasurer_BaselineOffset_FollowsVegaConstants`

Asserts the derived baseline offsets for 10 and 13 pixel fonts, the explicit line height variants, the half-way
rounding, and the line height of 12 for a 10 pixel font and 15 for an explicit 15. (The splitting of family lists, which
this unit used to own, is now `GenericFontFamilies.SplitFamilyList` and is verified by that unit.)

##### Text Bounds

**Tests**: `TextMeasurerTests.TextMeasurer_GetBounds_FollowsVegaTextBounds`

Asserts the derived bounds above: anchored, centered, rotated by -90 degrees, three lines, and the alphabetic baseline.

##### A Font Size That Does Not Fit A Single Is Unmeasurable

**Tests**: `TextMeasurerTests.TextMeasurer_UnrepresentableFontSizeIsUnmeasurableAndNeverThrows`

For each of the sizes 1e300 and `double.MaxValue` (finite as a double, infinity as a single), 1e39, 1e-300 and 1e-46
(positive as a double, zero as a single), 0, -5, NaN, and infinity, asserts that the width of "Hello" is 0, the
metrics are (0, 0), the truncation and the drawn text of "Hello" at a limit of 10 are the text itself (a text of no
width is below any positive limit), and the extent has width 0, without an exception (the core text renderer throws
`ArgumentOutOfRangeException` for a size that is not a finite single); that the largest finite single, 3e38, is
measured without a result that is not a number; and that the bounds of a text item of font size 1e300 and limit 50
have width 0.

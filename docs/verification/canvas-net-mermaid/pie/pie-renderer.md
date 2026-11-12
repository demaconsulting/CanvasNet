### PieRenderer Unit Verification Design

This document describes the unit-level verification strategy for the `PieRenderer` unit in the
`DemaConsulting.CanvasNet.Mermaid.Pie` namespace: the internal sealed `PieRenderer` (the drawing of Mermaid's pie
renderer and the ports of d3's `pie()`, `arc()`, and `centroid` and of JavaScript's `toFixed(0)`, in two partial files)
and the arc `PieArc`.

#### Verification Approach

The unit is verified in two ways. The functions that decide a coordinate or a text are called directly and compared
with the values that d3 and JavaScript give for the same input, stated in the tests as literals: the angles of `pie()`,
the path text of `arc()`, the centroid, the text of `toFixed(0)`, and the text the browser shows. The drawing is
verified by laying out short texts through the document and inspecting the scene: the slices, labels, legend entries,
and title by their roles, ids, and texts; their offsets; the colors of the squares and slices; the arc segments of the
slices; and the view box, compared with the values of Mermaid's `pieRenderer.ts` for the same input. The positions and
sizes of whole fixtures against the goldens drawn by Mermaid are verified at the system level by the reference layout
tests.

No mocking or stubbing is used; the parser, the configuration resolver, the pie style sheet, the text metrics, and the
bundled fonts are real (the helper `SceneQuery` opens and lays out a text). The internal types are reached through the
test assembly's `InternalsVisibleTo` access.

Unit tests reside in `PieRendererTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `Pie`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled fonts for the scenarios that lay out
- **Isolation**: Each test draws its own text; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any angle, path text, coordinate, text, color, count, or view box that differs from the asserted Mermaid
value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Pie-PieRenderer-Angles: The Slices And Their Angles

**Tests**: `PieRendererTests.PieRenderer_Pie_Values_GiveSharesOfATurnInOrder`,
`PieRendererTests.PieRenderer_Draw_RightLegend_ListsEverySectionAndWidensTheViewBox`,
`PieRendererTests.PieRenderer_Draw_ZeroSum_DrawsTheLegendOnly`

Asserts that the values 1, 0, 1, and 2 give the arcs from 0 to π/2, from π/2 to π/2 (the zero value takes no angle),
from π/2 to π, and from π to 2π, in the order of the values, and that the values 0 and 0 give empty arcs at 0; that of
the sections `Big` (1000), `Tiny` (5), `Zero` (0), and `Small` (20) only `Big` and `Small` get slices, in that order
(`Tiny` and `Zero` are under 1% of the sum); and that a chart whose values are all 0 draws no slice and no slice label.

##### CanvasNetMermaid-Pie-PieRenderer-Arcs: The Pie And The Outlines Of The Slices

**Tests**: `PieRendererTests.PieRenderer_ArcPath_Sectors_MatchD3`,
`PieRendererTests.PieRenderer_Draw_RightLegend_ListsEverySectionAndWidensTheViewBox`

Asserts that with a radius of 185 the path of a quarter is `M0,-185A185,185,0,0,1,185,0L0,0Z`, of a whole turn
`M0,-185A185,185,0,1,1,0,185A185,185,0,1,1,0,-185Z` (two half circles), of a half turn with an inner radius of 74
`M0,-185A185,185,0,1,1,0,185L0,74A74,74,0,1,0,0,-74Z`, and of an arc of 5 radians
`M0,-185A185,185,0,1,1,-177.401,-52.478L0,0Z` (the large-arc flag set and the numbers rounded to 3 digits), as d3
writes them; and that the chart is one group at (225, 225) whose outer circle is 372 wide (twice the radius of 185 plus
half the default outer stroke width of 2).

##### CanvasNetMermaid-Pie-PieRenderer-Labels: The Percentage Labels And The Texts

**Tests**: `PieRendererTests.PieRenderer_Centroid_IsAtTheMiddleRadiusAndAngle`,
`PieRendererTests.PieRenderer_ToFixed0_RoundsAsJavaScript`,
`PieRendererTests.PieRenderer_DisplayText_DecodesOnlyThePlaceholders`,
`PieRendererTests.PieRenderer_Draw_RightLegend_ListsEverySectionAndWidensTheViewBox`

Asserts that the centroid of a half turn at radius 100 is (100, 0), at the middle radius and the middle angle; that
`toFixed(0)` (a theory with ten cases) gives `1` for 0.5, `2` for 1.5, `3` for 2.5, `0` for 0.49999999999999994, `79`
for 79.36507936507937, `-0` for -0.4, `0` for a negative zero, `-3` for -2.5, `NaN` for NaN, and `Infinity` for
infinity (ties away from zero on the exact value); that the text `A`, the placeholder of `amp`, `B &amp;`, a line feed,
a no-break space, the placeholder of `#35`, and two spaces is shown as `A & B &amp;`, a space, a no-break space, and
`#` (only Mermaid's placeholders decoded, the white space collapsed); and that the slices of `Big` and `Small` are
labeled `98%` and `2%`.

##### CanvasNetMermaid-Pie-PieRenderer-Colors: The Theme's Pie Colors

**Tests**: `PieRendererTests.PieRenderer_Draw_Colors_WrapAfterTwelve`

Asserts that of thirteen sections of equal value the first legend square has the default theme's `pie1` `#ECECFF`,
the thirteenth the same color again, and the second another color, and that the first slice is drawn in `#ECECFF` with
an alpha of 179 (the theme's slice opacity of 0.7 folded in).

##### CanvasNetMermaid-Pie-PieRenderer-Legend: The Legend Entries And Their Positions

**Tests**: `PieRendererTests.PieRenderer_Draw_RightLegend_ListsEverySectionAndWidensTheViewBox`,
`PieRendererTests.PieRenderer_Draw_LegendPosition_PlacesTheLegendAndThePie`,
`PieRendererTests.PieRenderer_Draw_ZeroSum_DrawsTheLegendOnly`

Asserts that the legend lists every section in order, `Big`, `Tiny`, `Zero`, and `Small`, also those without a slice,
the first entry at (441, 181) and the fourth at (441, 247) (216 to the right of the pie's center and centered
vertically, 22 apart); that for two sections (a theory with four legend positions) the entries are 22 apart and the
first entry is at y 40 for `top`, at y 432 for `bottom`, and at y 203 for `left` and `center`, the chart group moving
right only for `left` and down by 66 only for `top`; and that with `showData` the legend texts of two sections of value
0 are `a [0]` and `b [0]`.

##### CanvasNetMermaid-Pie-PieRenderer-Title: The Title

**Tests**: `PieRendererTests.PieRenderer_Draw_LongTitle_WidensTheViewBoxToTheLeft`,
`PieRendererTests.PieRenderer_Draw_TitleSanitizedToEmpty_ReplacesTheFrontmatterTitle`

Asserts that the title of a `title` statement is one text anchored in the middle at x 0 and the baseline -200 from the
pie's center, that a chart without a `title` statement draws the frontmatter title `From the frontmatter`, and that a
`title` statement that sanitizes to empty (`<script>x</script>`) replaces the frontmatter title `FM` with an empty
title while a bare `title` keeps `FM`, as in Mermaid.

##### CanvasNetMermaid-Pie-PieRenderer-ViewBox: The View Box

**Tests**: `PieRendererTests.PieRenderer_Draw_RightLegend_ListsEverySectionAndWidensTheViewBox`,
`PieRendererTests.PieRenderer_Draw_LegendPosition_PlacesTheLegendAndThePie`,
`PieRendererTests.PieRenderer_Draw_EmptyChart_ViewBoxIsTheLeftHalf`,
`PieRendererTests.PieRenderer_Draw_LongTitle_WidensTheViewBoxToTheLeft`

Asserts that with the default legend at the right the view box is at (0, 0), 450 high, and 490 + 22 wide plus the
width of the widest legend text; that its height is 494 with a `top` or `bottom` legend of two entries and 450 with a
`left` or `center` legend; that a chart without sections has no slice and no legend and the view box (0, 0, 225, 450),
the left half of the chart, because Mermaid's widest legend text is then minus infinity; and that a title of 40 times
the word `wide` and a space widens the view box to the left of 0, evenly on both sides of the chart's center (its x is
225 less half its width).

##### CanvasNetMermaid-Pie-PieRenderer-Donut: The Donut Hole

**Tests**: `PieRendererTests.PieRenderer_Draw_DonutHole_CutsTheInnerArcInRange`

Asserts that with a `donutHole` of 0.5 the first slice has two arcs, the inner one of radius 92.5 (half the radius of
185), and that with a `donutHole` of 0.95, out of range, it has one arc only.

##### CanvasNetMermaid-Pie-PieRenderer-Highlight: The Highlighted Slice

**Tests**: `PieRendererTests.PieRenderer_Draw_HighlightSlice_EnlargesTheSlice`

Asserts that with the `highlightSlice` `b` the arc of the slice `b` has the radius 185 times 1.05 and an opaque fill (an
alpha of 255), while the slice `a` keeps the radius 185 and the fill alpha of 179.

### TitleBuilder Unit Verification Design

This document describes the unit-level verification strategy for the `TitleBuilder` unit in the
`DemaConsulting.CanvasNet.VegaLite.Guides` namespace: the internal `TitleBuilder` class (`Build`) and the `TitleBlock`
class it returns (the orient, anchor, frame, offset, scene items, extent, and the `Place` method that positions the
title group around the data rectangle and the guides, as Vega's title layout does). The unit turns the `title`
property of a view (a string, an array of lines, or an object with a subtitle and font, anchor, orient, offset, and limit
properties) into text scene items with the Vega-Lite 6 defaults and the configuration applied.

#### Verification Approach

The unit is verified by parsing small, original specifications with the real `SpecParser`, resolving the configuration
with the real `ConfigResolver`, building titles with the real `TextMeasurer`, and asserting on the title block's scene
items, extent, and placement. The expected numbers are derived by hand from the Vega-Lite defaults and the Vega layout
arithmetic; the tests measure the title width with the bundled font directly so that no width is a recorded constant:

- Default title: orient top, anchor middle, offset 4, subtitle padding 3, frame `group`; the title text is 13 pixel bold
  black sans-serif, centered, with a top baseline; its extent is as wide as the measured title and 13 high (the baseline
  offset 10 less 80% of 13 is 0), centered on x = 0.
- Subtitle: a 12 pixel regular text below the title at 13 + 3 padding = 16; its box starts one pixel higher (Vega rounds
  0.79 x 12 to 9 and 0.8 x 12 to 10), so the extent ends at 16 - 1 + 12 = 27.
- Placement above a 300 by 200 plot: x at the center, y = 0 - 27 - 4; a bottom title 4 below the plot; `start` and
  `end` anchors at the left and right edge of the plot.
- Left and right titles are rotated by -90 and 90 degrees with a 13 wide extent, placed at x = 0 - 13 - 4 beside the
  plot and centered on y = 100; the subtitle sits 13 + 3 beside the title, and both extents are 27 wide; the anchors
  on a left title run bottom to top.
- Frames: for a view whose axes reach 40 to the left, 20 above, 10 to the right, and 30 below the 300 by 200 plot, the
  `group` frame centers on the plot (x = 150) and the `bounds` frame on the guides ((-40 + 310) / 2 = 135); a top title
  above the guides is at y = -20 - 13 - 4 = -37 and a bottom title at 230 + 4; a left title is at x = -40 - 13 - 4,
  centered on the guides' height ((230 - 20) / 2 = 105).
- Lines: two lines are 13 + (13 + 2) = 28 high and a line feed splits like an array does (three lines: 13 + 2 x 15); a
  title that sets its own properties (a 20 pixel italic serif title, a 9 pixel subtitle with its own color, weight, and
  line height, offsets, a limit) wins over the style defaults, with the subtitle 6 pixels of padding plus one title box
  (20 high) below the anchor, at y = 26.

The unit's collaborators are real, so no mocking or stubbing is used; the internal types are reached through the test
assembly's `InternalsVisibleTo` access. The title's interaction with the layout is verified in the _ViewLayout Unit
Verification Design_ and at the document level.

Unit tests reside in `TitleBuilderTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the
`Guides` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts through a real `TextMeasurer`
- **Isolation**: Each test parses its own specification text; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any wrong text, style, position, extent, anchor, orient, or frame, or a title built where none is documented,
constitutes a failure.

#### Unit-Level Test Scenarios

##### Orient, Anchor, And Subtitle Produce The Expected Extent

**Tests**: `TitleBuilderTests.TitleBuilder_Build_OrientAnchorAndSubtitleProduceExpectedExtent`

Asserts the default title and its extent, the subtitle and its 27 pixel extent, the placement above a 300 by 200 plot at
y = 0 - 27 - 4, a bottom title 4 below the plot, and `start` and `end` anchors at the plot's edges, all as derived
above.

##### Left And Right Titles Are Rotated

**Tests**: `TitleBuilderTests.TitleBuilder_Build_LeftAndRightTitlesAreRotated`

Asserts the rotation (-90 and 90 degrees), the 13 pixel wide extent, the placement beside the plot, the subtitle beside
the title with 27 pixel wide extents, and the anchors on a left title, all as derived above.

##### Frame Centers Over The Plot Or The Guides

**Tests**: `TitleBuilderTests.TitleBuilder_Place_FrameBoundsCentersOverTheGuides`

Asserts, for guides reaching 40, 20, 10, and 30 pixels beyond a 300 by 200 plot, the group and bounds frame
positions 150 and 135 and the placements above, below, and left of the guides derived above.

##### Properties, Lines, Configuration, And Limit

**Tests**: `TitleBuilderTests.TitleBuilder_Build_PropertiesLinesConfigurationAndLimit`

Asserts that an array of lines and a string with line feeds are split into lines with the derived heights; that the
title's own font, size, weight, style, color, line height, offsets, and limit win over the style defaults and that the
subtitle has its own; that the limit applies to the measured text (the extent is narrower than the limit and the text is
kept whole in the scene item, to be truncated by the painter); and that `config.title` applies where the title is
silent (an 18 pixel `#336699` title anchored at the start with a bottom orient, an offset of 7, and a 10 pixel gray
subtitle at y = 20).

##### Empty Titles And Compositions

**Tests**: `TitleBuilderTests.TitleBuilder_Build_EmptyTitlesAndCompositions`

Asserts that no title, a `null`, empty, or blank title, an empty array, an empty object, and a subtitle without a title
build nothing; that a composition anchors its title at the start and frames it by the bounds, left aligned; that an
invalid orient, anchor, or frame from the configuration falls back to `top`, `middle`, and `bounds`; and that null
arguments are rejected.

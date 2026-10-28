### ViewLayout Unit Verification Design

This document describes the unit-level verification strategy for the `ViewLayout` unit in the
`DemaConsulting.CanvasNet.VegaLite.Layout` namespace: the internal static `ViewLayout` class (`Resolve`, `Measure`,
`Build`, and `Compute`) and the records of its file (`ViewPadding`, `ViewExtents`, `AutosizeType`, `ViewStyle`,
`ViewSizeHints`, `ViewLayoutRequest`, `ViewMeasure`, and `ViewLayoutResult`). The unit resolves the data rectangle size,
padding, autosize, view style, and background of a single view from the specification, the configuration, and hints
from the scale stage; measures the canvas for the guide extents and the title (phase one); and assembles the scene with
the content and the title (phase two).

#### Verification Approach

The unit is verified by resolving layout requests for small, original specifications with the real `SpecParser` and
`ConfigResolver`, measuring and building with explicit guide extents and a real `TitleBuilder`, and asserting on the
measured sizes, origins, bounds, and the structure of the scene. No mocking or stubbing is used; the internal types are
reached through the test assembly's `InternalsVisibleTo` access. The unit's behavior with real axes and the painter is
verified at the subsystem tier and at the document level.

The expected numbers are derived by hand from the Vega-Lite 6 defaults and the Vega layout rules:

- An empty view: the 300 by 300 default data rectangle (Vega-Lite 6; the 200 of version 5 is not used) and 5 pixels of
  padding give a 310 by 310 canvas with the data rectangle at (5, 5); explicit sizes of 200 by 100 with a padding of 20
  give 240 by 140; a padding object leaves the sides it does not name at 0 (so a padding of left 1, top 2 gives a
  canvas of 301 by 302 with the data rectangle at (1, 2)).
- A top title: 13 pixels of text and 4 pixels of offset reach 17 pixels above the data rectangle, so the canvas is 310 by
  327 with the origin at (5, 22) and the title group at (150, -17); a bottom title makes the canvas 310 by 327 with the
  origin at (5, 5); left and right titles make it 327 by 310 with the origin at (22, 5) and (5, 5).
- Autosize `none` keeps the given size plus padding whatever sticks out (100 by 100 with a title is 110 by 110); with
  `contains: padding` the width and height are the whole canvas, so 100 by 80 has a 90 by 70 data rectangle; an autosize
  type the layout does not support is laid out as `pad` (the catalog reports it).
- A title wider than the plot: a 20 pixel plot under a long title overhangs by (w - 20) / 2 on each side, which rounds
  up to whole pixels; the test measures the title width with the bundled font, so the expected growth is not a
  recorded constant.
- Guide extents: a left axis 40 wide and a bottom axis 25 high give 300 + 40 + 10 by 300 + 25 + 10 = 350 by 335 with the
  origin at (45, 5); guides of 40, 20, 10, and 30 plus a centered title put the title at y = -20 - 13 - 4 = -37, so the
  canvas grows by 37 and 30 at the top and the bottom (360 by 377); the `bounds` frame centers the title at
  (-40 + 310) / 2 = 135; negative extents count as 0.
- Sizes: the configuration changes the 300 default (a `view.continuousWidth` of 120 and height of 80), the view of the
  specification wins over it, a number wins over everything and negative sizes count as 0, a `step` (or a missing size
  on a discrete scale) takes the size the scale stage computed from the step, and a `container` size fills the
  container less padding and the guides' room (400 - 10 - 30 = 360 wide in a canvas of exactly 400, and 250 - 10 = 240
  high in a canvas of 250).
- Rounding: a 100.4 by 50.5 data rectangle with a title is 110.4 by 77.5, whose natural size rounds half away from zero
  to 110 by 78 pixels, while the scene keeps the fractions; a size of 1e12 cannot overflow the pixel size.

Unit tests reside in `ViewLayoutTests.cs` within the `DemaConsulting.CanvasNet.VegaLite.Tests` project, in the `Layout`
folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fonts**: The bundled Liberation fonts through a real `TextMeasurer` for the titles
- **Isolation**: Each test parses its own specification text; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any canvas size, origin, data rectangle, bounds, title position, view style, background, scene structure, or
rounding that differs from the hand-derived value, or any missing argument check, constitutes a failure.

#### Unit-Level Test Scenarios

##### Padding, Title, And Autosize Produce The Expected Data Rectangle

**Tests**: `ViewLayoutTests.ViewLayout_Compute_PaddingTitleAndAutosizeProduceExpectedDataRectangle`

Asserts the derived empty view (a 310 by 310 canvas, data rectangle at (5, 5), a scene of root and a view group with the
data rectangle's size and the one pixel `#ddd` border), explicit sizes and padding, the padding object, the four title
sides, autosize `none` in string, object, and configuration forms, `contains: padding`, and the `fit` fallback to
`pad`.

##### Wide Title Grows The Canvas By Whole Pixels

**Tests**: `ViewLayoutTests.ViewLayout_Compute_WideTitleGrowsCanvasByWholePixelsOnBothSides`

Asserts that a title wider than a 20 pixel plot grows the canvas on both sides by the whole pixels derived above, with
the origin and the view bounds shifted accordingly and the height growing only by the title's 17 pixels.

##### Guide Extents Grow The Canvas

**Tests**: `ViewLayoutTests.ViewLayout_Compute_GuideExtentsGrowTheCanvas`

Asserts the derived canvas sizes, origins, and view bounds for guide extents, the title placement above the top guide
for the `group` frame (y = -37) and at 135 for the `bounds` frame, and that negative extents count as 0.

##### Sizes Come From The Specification, Configuration, And Hints

**Tests**: `ViewLayoutTests.ViewLayout_Resolve_SizesComeFromSpecificationConfigurationAndHints`

Asserts the derived data rectangle sizes for the default, the configuration, the view of the specification, explicit
numbers, steps with and without the scale stage's hints, and a container size.

##### View Style And Background Precedence

**Tests**: `ViewLayoutTests.ViewLayout_Resolve_ViewStyleAndBackgroundFollowPrecedence`

Asserts Vega's cell style by default (a transparent fill, a `#ddd` stroke, a white canvas, and no stroke width, dash, or
corner radius), that the view of the specification (including an explicit `null` stroke, a fill, a stroke width of 2, a
corner radius of 4, a dash of 4 and 2, and the three opacities) and its background apply, that `config.view` and
`config.background` apply when the specification is silent (and a `null` stroke in the configuration removes the
stroke), that the caller's background override (`#336699`) wins over both, and that the scene carries the view style on
the view group and the background on the scene.

##### Content, Title, Clip, Rounding, And Argument Checks

**Tests**: `ViewLayoutTests.ViewLayout_Build_ContentTitleClipAndRoundingFollowTheMeasure`

Asserts that the two-phase API (measure, then build) gives the same sizes and scene structure as the combined
`Compute`; that the content comes first in the view group and the title last (`mark-bar`, `axis-domain`, `title`); the
derived rounding (110.4 by 77.5 for a natural size of 110 by 78); that a clipped view does not grow for its title and
clips its content to the data rectangle (a canvas of 110.4 by 60.5); that null arguments throw `ArgumentNullException`;
and that a huge size gives `int.MaxValue` instead of overflowing.

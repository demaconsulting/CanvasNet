### GridOverlay

<!-- cspell:ignore canvasnet luma -->

#### Verification Approach

`GridOverlay` is verified with unit tests in `GridOverlayTests.cs`. The lines and the blending are checked on tiny solid
surfaces with hand-computed expected pixels (an opaque line, a translucent line over an opaque, a transparent, and a
half-transparent pixel). The labels are checked on a mid-gray surface with a black or white line color, where the box
behind a label has a known backing color (`224` for the white backing and `32` for the black backing at alpha 192) in
pixels that no glyph reaches, and where the text pixels are darker than the backing; the count of labels and the places
where no label may be are checked for a spacing that allows every label, one that allows every n-th label, and surfaces
that are too small. The unit needs no files and no mocking; the bundled font makes the labels the same on every machine.

#### Test Environment

N/A - standard test environment; the tests use only in-memory surfaces.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Lines are one pixel wide at the multiples of the spacing, the first column and row included, and a crossing is blended
  once; an opaque color replaces the pixels.
- A translucent color is blended with the documented integer formulas over opaque, transparent, and half-transparent
  pixels.
- Labels are drawn at the top and left edges with the backing that contrasts with the line color, only every n-th line
  when the spacing is small, and not at all when their box would not fit inside the surface.
- The counts of lines and labels are reported.
- The same input gives identical pixels.
- Invalid arguments are rejected.

#### Test Scenarios

**GridOverlay_Draw_Spacing_DrawsOnePixelLinesAtMultiples**: on a 100 by 100 white surface with spacing 32 there are 4
vertical and 4 horizontal lines; the pixels at x = 32 and 96 and at y = 64 are the red line color, their neighbors are
white, and the first row and column are lines.

**GridOverlay_Draw_Intersections_AreBlendedOnce**: black at alpha 128 over white gives 127 on a line and also at a crossing
of two lines.

**GridOverlay_Draw_OpaqueColor_ReplacesPixels**: an opaque blue line over a transparent surface gives opaque blue on the
lines and leaves the neighbors transparent.

**GridOverlay_Draw_TranslucentColorOverOpaque_UsesIntegerSourceOver**: `(200, 100, 0, 100)` over `(100, 150, 200, 255)`
gives `(139, 130, 122, 255)`.

**GridOverlay_Draw_TranslucentColorOverTransparent_ComputesResultAlpha**: the same color over a transparent pixel gives
itself, `(250, 0, 0, 128)` over `(50, 50, 50, 128)` gives `(183, 17, 17, 192)`, and a color with alpha 0 changes nothing.

**GridOverlay_Draw_Labels_DrawnAtTopAndLeftEdgesInBundledFont**: on a 200 by 200 gray surface with spacing 50 and black
lines all 4 vertical labels and the 3 horizontal labels after the first are drawn (7), the corner pixels of the boxes
have the white backing, some pixels of the text are dark, and pixels away from lines and labels are unchanged.

**GridOverlay_Draw_SmallSpacing_LabelsOnlyEveryNthLineWithoutOverlap**: with spacing 8 on a 200 by 200 surface only every
fourth vertical line and every third horizontal line is labeled; the pixels where the labels of the other lines would
be are unchanged and fewer labels are drawn than there are lines.

**GridOverlay_Draw_LabelOutsideImage_IsSkipped**: with spacing 96 on a 100 by 100 surface the labels at 96 would not fit
and only the origin label is drawn; a 30 by 10 surface is too small for any label (0) but still has its lines.

**GridOverlay_Draw_DarkAndLightColors_ChooseLightAndDarkBacking**: black lines give the white backing 224 over gray 128
and white lines give the black backing 32.

**GridOverlay_Draw_ReturnsLineAndLabelCounts**: a 250 by 120 surface with spacing 100 gives 3 vertical lines, 2 horizontal
lines, and 4 labels.

**GridOverlay_Draw_SameInputTwice_ProducesIdenticalPixels**: two grids drawn on equal surfaces are identical and the
default color is `#FF00FFA0`.

**GridOverlay_Draw_InvalidArguments_Throw**: a null surface and spacings of 7, 4097, and -1 throw.

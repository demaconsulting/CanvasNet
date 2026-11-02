### SurfaceTransforms Unit Verification Design

<!-- cspell:ignore premultiplied transposed transversed -->

The whole-surface transformations of the `Surface` class (the `SurfaceTransforms` unit of the `Canvas` subsystem) are
verified by `test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceTransformsTests.cs` and, for the EXIF orientation,
`test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceExifOrientationTests.cs`.

#### Verification Approach

`SurfaceTransforms` is verified with unit tests on small in-memory surfaces whose pixels are known exactly, so every
expected value is a hand-derived number or a pixel of the source: a flip is checked by comparing every pixel with its
mirrored source pixel, a pad by the corners and edges of the border and the copied block, a trim by hand-placed content
blocks and noise at known distances from the background, a fit by the size and the centered position of the image, and
an EXIF orientation by the letters of a numbered image.
The moved fit, trim, and orientation tests keep the expected values of the command-line tool tests they came from; the
flip, pad, flatten, transparency, and disposal tests are new. `FitInside` is public and tested directly. The unit needs
no files and no mocking.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required; the tests use only in-memory surfaces
- **Isolation**: Each test method builds its own surfaces; no shared state between tests

#### Acceptance Criteria

- All unit tests pass with zero failures on every target framework.
- A flip or half turn puts every pixel at its mirrored position, twice restores the original, and leaves the source
  unchanged.
- Each EXIF orientation 1 to 8 puts every pixel where the specification says, swapping the size for 5 to 8.
- A pad has the documented size, border color, and copied block, and refuses a negative or oversize request.
- A trim finds the content box under the corner-majority background, the tolerance, and the transparent-pixel rule,
  and an image without content is copied unchanged.
- A contain fit scales to the limiting side, rounds half away from zero, and centers on the fill; a stretch fills the
  box; a same-size fit is an equal copy.
- Flattening composites over the background; the transparency test finds a single translucent pixel.
- Every transformation of a disposed surface throws.

#### Test Scenarios

##### CanvasNet-Canvas-SurfaceTransforms-Flip: Flip and Half Turn

**Tests**: `Surface_Flip_Pixels_MirrorAboutTheAxes`, `Surface_Flip_Twice_RestoresOriginalAndBothAxesEqualHalfTurn`

A 5 by 3 surface with a different pixel everywhere is flipped both ways and turned by 180 degrees; every pixel equals
the mirrored source pixel and the source is unchanged. Flipping the same way twice restores the original and a
horizontal then vertical flip equals the half turn.

##### CanvasNet-Canvas-SurfaceTransforms-Pad: Pad

**Tests**: `Surface_Pad_Border_AddsColoredBorderAndCopiesPixels`,
`Surface_Pad_TransparentColorOrNoBorder_LeavesBorderTransparentOrCopies`, `Surface_Pad_InvalidBorder_Throws`

A half-transparent 2 by 2 surface padded by 1, 2, 3, and 4 is 6 by 8, with the source pixels copied as they are at
(1, 2) and the fill color around them; a transparent color leaves the border transparent and a zero border is an equal
copy; a negative
width, a result wider or higher than 8192 (including `int.MaxValue` borders, which must not overflow) throw, and the
largest permitted result is allowed.

##### CanvasNet-Canvas-SurfaceTransforms-Trim: Trim

**Tests**: `Surface_FindTrimBounds_UniformBorder_ReturnsContentBox`,
`Surface_FindTrimBounds_Tolerance_IgnoresNoiseBelowTolerance`,
`Surface_FindTrimBounds_TransparentBorder_TreatsAllTransparentPixelsAsEqual`,
`Surface_FindTrimBounds_CornerMajority_ChoosesMostFrequentCorner`, `Surface_FindTrimBounds_AllBackground_ReturnsNull`,
`Surface_Trim_ContentTouchingEdges_ReturnsFullImage`, `Surface_Trim_InvalidTolerance_Throws`

A 5 by 3 black block at (4, 2) in a white 20 by 10 image is the box (4, 2, 5, 3) and `Trim` returns exactly those
pixels. Noise at distance 5 and 7 from white is ignored at tolerance 8 and 7 and counts as content at 4 and 0.
Transparent pixels of different colors are one background. With three white corners and one red the red pixel is
content; with a two and two tie the top left wins. A uniform image, an image whose only difference is within the
tolerance, and a 1 by 1 image have no box, and `Trim` returns an equal copy that is a different object. Content on all
four edges gives the whole image. A tolerance of -1 or 256 throws.

##### CanvasNet-Canvas-SurfaceTransforms-FitInto: Fit Into a Box

**Tests**: `Surface_FitInside_RoundsAndClamps`, `Surface_FitInside_NonPositiveSize_ThrowsArgumentOutOfRange`,
`Surface_FitInto_Contain_ScalesToLimitingSideAndCenters`, `Surface_FitInto_Stretch_ResamplesToExactBox`,
`Surface_FitInto_SameSize_ReturnsEqualPixels`, `Surface_FitInto_InvalidArguments_Throw`

`FitInside` of 100 by 50 into 20 by 20 is 20 by 10, 3 by 2 into 10 by 10 is 10 by 7 (6.67 rounds up), and a size below
one throws. A 4 by 2 image contained in 20 by 20 becomes 20 by 10 in rows 5 to 14 with the fill above and below; a
stretched image fills the box; an image of the box size is an equal, different object in both modes; an undefined mode
and a box of 0 or above 8192 throw.

##### CanvasNet-Canvas-SurfaceTransforms-Flatten: Flatten and Transparency

**Tests**: `Surface_FlattenOver_TranslucentPixels_CompositesOverBackground`,
`Surface_HasTransparency_OpaqueAndTranslucentSurfaces_ReportsCorrectly`

Red at alpha 128 flattened over white is (255, 127, 127, 255), a transparent pixel becomes the background, the source
is unchanged, and the result has no transparency. A single pixel of alpha 254 makes a surface translucent; an opaque
surface is not.

##### CanvasNet-Canvas-SurfaceTransforms-ExifOrientation: EXIF Orientation

**Tests**: `Surface_ApplyExifOrientation_Orientation_MapsPixels`,
`Surface_ApplyExifOrientation_Pixels_AreExactCopiesIncludingAlpha`, `Surface_ApplyExifOrientation_InvalidArguments_Throw`
(in `test/DemaConsulting.CanvasNet.Tests/Canvas/SurfaceExifOrientationTests.cs`)

A 3 by 2 image whose six pixels are numbered `a` to `f` in reading order is turned by each orientation 1 to 8 and the
letters of the result are compared with the table of the EXIF specification (for example orientation 6 gives
`da/eb/fc`, a 2 by 3 image); the source is unchanged. Every moved pixel, alpha included, equals its source pixel;
orientation 1 is an equal copy that is a different object, which changes nothing in the source when written to. An
orientation of 0 or 9 throws `ArgumentOutOfRangeException` and a disposed surface throws `ObjectDisposedException`.

##### CanvasNet-Canvas-SurfaceTransforms-Disposed: Disposed Surface

**Test**: `Surface_Transforms_AfterDispose_ThrowObjectDisposedException`

Every transformation and `HasTransparency` of a disposed surface throws `ObjectDisposedException`.

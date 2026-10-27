### InspectCommand

<!-- cspell:ignore canvasnet jpeg luma otsu grayscale deskew colours pixle frobnicate -->

#### Verification Approach

`InspectCommand` is verified with unit tests in `InspectCommandTests.cs` (the subcommands, their options, results, and
errors) and `InspectCommandSourceTests.cs` (the kinds of input). The command is run in-process: the tests create a
`Context` (with `--json` when the JSON document is wanted), call `Run`, and capture the console with the `ToolHost`
helper; failures are asserted as the `ToolException` the command throws. The main input is a generated 20x10 PNG, red
where x is below 15 and blue from there on, whose colors, shares, luma statistics (the luma of red is 77 and of blue
29), and window means are hand-computed; the other inputs (a JPEG with an EXIF orientation, an SVG drawing, a model, a
PDF, and a transparent PNG) are the generated fixtures of the test project. The interaction with the default command
registry is verified at the subsystem level by `CommandsSubsystemTests.cs`.

#### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because they redirect the console.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The command is dispatched to the subcommand named, in any case; a missing or unknown subcommand, an option of the
  other subcommand, and a wrong number of operands are usage errors, with a suggestion for a mistyped subcommand.
- The options are those of render without the options of the output, plus count and radius, and the help shows both
  usages.
- `inspect colors` gives the documented text and JSON for known pixels, limits the list with count, and rejects a count
  out of range.
- `inspect pixel` gives the documented text and JSON, the clipped neighborhood with a radius, and usage errors for a
  malformed or outside point and a bad radius.
- The pipeline runs first: the colors are those of the cropped image and the point is in the coordinates after the
  operations.
- An EXIF orientation, a drawing, a model, a page of a document, and transparency are handled as in render.
- No file is written, errors are expected errors with stable codes, and the same input gives the same bytes.

#### Test Scenarios

**InspectCommand_Run_ColorsAndPixel_AreDispatched**: both subcommands (and `COLORS` in capitals) run on the 20x10 image
and print their own first line; the command is named `inspect`, has no alias, and has a summary.

**InspectCommand_Run_MissingOrUnknownSubcommand_ThrowsUsageErrorWithSuggestion**: no subcommand names the choices;
`colours` suggests `colors`, `pixle` suggests `pixel`, and `frobnicate` suggests nothing and lists the subcommands.

**InspectCommand_Run_OptionOfOtherSubcommand_ThrowsUsageError**: `--radius` with `colors` and `--count` with `pixel` name
the subcommand they belong to; a pixel without a point, a colors with a point, and a colors without an input name the
number of operands that were given.

**InspectCommand_Options_SharedWithRenderExceptOutputOptions** and **InspectCommand_Help_ListsBothSubcommands**: the
options are those of render in their order without `--output`, `--format`, `--quality`, `--no-clobber`, and
`--all-pages` (the same declaration objects), then `--count` and `--radius` with their prefixed descriptions, every
option has a value name exactly when it takes a value, the photo options and `--page` are present, and `-o` is an
unknown option; the usage and the registry help show both usage lines, an example, and the options.

**InspectCommand_Colors_Text_PrintsColorsAndLumaLines**: the 20x10 image gives exactly
`colors of <input> (20x10): #FF0000 75.00%, #0000FF 25.00%` and
`luma: mean 65.00, min 29, max 77, p1 29, p50 77, p99 77, otsu 52` (the Otsu threshold of two levels is the middle of the
gap, (29 + 76) / 2).

**InspectCommand_Colors_Json_HasDocumentedShape**: one line with the fields in the documented order, the tool, the input
with its natural size, no operations, 200 pixels, the background `#FFFFFF`, the two colors with their channels, pixel
counts 150 and 50 and fractions 0.75 and 0.25, the luma statistics, a 16-bin histogram with 50 pixels in bin 1 and 150 in
bin 4 summing to 200, and empty diagnostics and echo.

**InspectCommand_Colors_Count_LimitsTheList**: an image of three colors with 5, 3, and 1 pixels lists three colors,
two with
`--count 2`, one with `--count 1`; counts of 0, 33, and a word are usage errors.

**InspectCommand_Pixel_Text_PrintsHexRgbAlphaAndLuma**: the red pixel at 3,4 is
`#FF0000 rgb(255,0,0) alpha 255 luma 77` and the blue pixel at 19,9 `#0000FF rgb(0,0,255) alpha 255 luma 29`.

**InspectCommand_Pixel_Json_HasDocumentedShape**: the fields in order, the point, and the pixel as stored (a pixel
`(161, 178, 195)` with alpha 128 has the hex color `#A1B2C380` and the luma over white 215), no neighborhood without a
radius, and the image of 12 pixels.

**InspectCommand_Pixel_RadiusWithJson_ListsNeighborhood**: a radius of 1 at 14,5 reads the 3x3 window at 13,4 whose
rows are
red, red, blue, with the mean `#AA0055` (the text has the second line `mean of 3x3 at 13,4: #AA0055`); a radius of 2
at the
corner is clipped to 3x3 at 0,0; the JSON has the neighborhood after the pixel.

**InspectCommand_Pixel_BadOrOutsidePoint_ThrowsUsageError**: `abc`, `1`, `1,2,3`, `1.5,2`, `x,y`, and `,` are malformed
(naming `<x,y>`); `20,0`, `0,10`, `5000,10`, and `-1,0` are outside the 20x10 image (naming the size); radii of 9
and -1 are
usage errors.

**InspectCommand_Colors_AfterCrop_AnalyzesTheCroppedRegion** and **InspectCommand_Pixel_AfterPipeline_UsesPipelineCoordinates**:
a crop of the right quarter gives one blue color for all 50 pixels, the image size 5x10, and the operation `crop`; with
`--grayscale` the color is the gray `#1D1D1D`; after the crop the point 0,0 is blue and 5,0 is outside the 5x10 image; after
a rotation by 90 degrees the image is 10x20 and 0,19 is blue and 0,0 red.

**InspectCommand_Run_WritesNoFiles**: both subcommands with operations that would draw or rotate, as text and as JSON, leave
the directory exactly as it was and the input unchanged.

**InspectCommand_Errors_AreExpectedErrorsWithStableCodes**: a missing file is `input-not-found`, a text file
`unsupported-format`, a bad angle, an unknown option, a rotation fill without a rotation, and a model option for a PNG are
usage errors, a page 2 of a PNG is `page-out-of-range`, and a rotation of an 8192-pixel-wide image is `size-limit`.

**InspectCommand_Json_SameInputTwice_IsByteIdentical**: both subcommands on a generated sketch with `--auto-levels` and
`--deskew` give the same bytes twice, and the result lists the deskew.

**InspectCommand_Colors_JpegWithExif_UsesUprightImage**: a JPEG stored 12x8 with the EXIF orientation 6 is analyzed upright
(8x12, with an `auto-orient` operation) and as stored (12x8) with `--no-auto-orient`; the natural size is as stored.

**InspectCommand_Colors_SvgAndScad_AreRenderedFirst**: an SVG is rendered at 100x80 (50x40 with `--width 50`) and a
model at
800x800, each with a `render` operation first, over the white background.

**InspectCommand_Colors_DocumentPage_UsesChosenPage**: the two pages of the PDF have swapped sizes, the document facts name
the page and the page count, and page 3 is `page-out-of-range`.

**InspectCommand_Colors_TransparentPng_FlattensOverBackground**: a fully transparent red PNG is one white color by default
and with a transparent background, and one green color with `--background #00FF00`, which is reported as the background.

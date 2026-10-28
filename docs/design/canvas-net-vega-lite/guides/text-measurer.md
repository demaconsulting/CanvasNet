### TextMeasurer

![Guides Structure](VegaLiteGuidesView.svg)

The `TextMeasurer` unit is the first unit in the `Guides` subsystem. It is one source file, `TextMeasurer.cs`, in
the `DemaConsulting.CanvasNet.VegaLite.Guides` namespace, and defines six `internal` types: `TextMeasurer` (sealed class),
`TextFont` (readonly record struct), `FontSource` (enumeration), `ResolvedFont` (sealed record), `TextExtent` (readonly
record struct), and `FontMetrics` (readonly record struct). The file carries the provenance header comment for the Vega
text rules it ports.

#### Purpose

`TextMeasurer` resolves CSS font requests to fonts and measures text with them, so that chart layout gives the same
numbers on every machine. By default every request resolves to one of the Liberation fonts bundled with the core library
whatever fonts are installed. It exists so font resolution, measurement, line splitting, truncation, and the bounds of
a text item are defined once and shared by the guide builders, the layout, and the painter.

The unit's behavior is specified by the `CanvasNetVegaLite-Guides-TextMeasurer-*` requirements in
`docs/reqstream/canvas-net-vega-lite/guides/text-measurer.yaml`.

#### Data Model

- **`TextFont`** `(string? Family, double Size, string? Weight, string? Style)` - a font request: a CSS family list, a
  size in pixels, a weight, and a style.
- **`FontSource`** - `Bundled`, `AdditionalDirectory`, or `System`: where a resolved face came from.
- **`ResolvedFont`** `(TrueTypeFont Font, string Family, FontSource Source, bool Bold, bool Italic)` - a resolved face.
- **`TextExtent`** `(double Width, double Height, double LineHeight, IReadOnlyList<string> Lines)` - a measured block of
  lines (the lines as they are drawn, after trimming and truncation).
- **`FontMetrics`** `(double Ascent, double Descent)` - the font's own line box at a size (for painters that need it; the
  layout does not).
- **`TextMeasurer`** (sealed class, thread-safe) holds the `VegaLiteFontOptions`, a concurrent cache of resolved fonts by
  family list, bold, and italic, and a lazily read list of the faces found in the additional directories.

**Resolution rules.** The family list is split at commas (quotes respected) and read left to right; the first family that
resolves wins. `sans-serif`, `system-ui`, and the common sans families (Helvetica, Helvetica Neue, Arial, Verdana, Tahoma,
Segoe UI, Trebuchet MS, Roboto, Open Sans, DejaVu Sans, and similar) resolve to Liberation Sans; `serif`, Times, Times New
Roman, Georgia, Palatino, Cambria, Garamond, and similar to Liberation Serif; `monospace`, Courier, Courier New, Menlo,
Consolas, Monaco, Lucida Console, and similar to Liberation Mono; these never consult the installed fonts. Any other family
is looked up by name (case-insensitive) first in additional directories and then, when `UseSystemFonts` is set, in
the installed fonts, preferring the face whose weight and style match; when nothing matches, the next family is tried, and
an empty or exhausted list means Liberation Sans. A weight of 600 or more, `bold`, or `bolder` selects the bold face;
`italic` or `oblique` the italic face; a family that lacks the requested style is used as it is.

**Directory scan.** The additional directories are scanned on first use, recursively to a depth of 4, for `.ttf`, `.otf`,
and `.ttc` files, skipping reparse points and system files, in ordinal path order within a directory, reading at most 4,096
files in total; a directory that does not exist, a file that cannot be read, and a face that cannot be loaded are skipped.

#### Key Methods

- `Resolve(TextFont)` and `Resolve(family, weight, style)` - the resolution above; never null, because Liberation Sans is
  the last resort; results are cached per instance.
- `MeasureWidth(text, font)` - the advance width with kerning from the core `TextRenderer`, never rounded; 0 for empty
  text or a size that is not positive and finite.
- `Truncate(text, font, limit, ellipsis, rightToLeft)` - Vega's truncation: text narrower than the limit is returned
  unchanged; otherwise the line is cut by binary search at the longest prefix whose width plus the ellipsis (the horizontal
  ellipsis by default) stays below the limit, or, right to left, at the longest suffix.
- `TextValue(line, font, limit, ellipsis)` - the line trimmed and, when a limit is set, truncated (Vega's `textValue`).
- `Measure(lines, font, limit, lineHeight, ellipsis)` - the extent of a block: the width of the widest line and the height
  of the font size plus one line height per further line.
- `GetBounds(SceneText)` - the bounding box of a text item in its group's coordinates: the measured box aligned around the
  position, shifted by the baseline offset less 80% of the size (Vega's 4/5 offset), rotated about the position, and
  translated.
- `GetMetrics(font)` - the ascent and descent of the font's own line box.
- Static helpers: `IsBold`, `IsItalic`, `SplitFamilies`, `SplitLines` (at line feeds; a carriage return also ends a
  line), `LineHeight(fontSize, lineHeight)` (font size plus 2 by default), `BaselineOffset(baseline, fontSize, lineHeight)`
  (Vega's offsets: top 0.79, middle 0.30, bottom -0.21 of the font size, and line-top and line-bottom 0.29 of it plus or
  minus half a line, rounded half up), and `RoundHalfUp` (JavaScript's `Math.round`).

**Preconditions and postconditions**: text and lines are non-null where documented; results are deterministic for the same
options and fonts, and every measurement of one instance uses the same resolved font.

#### Error Handling

Resolution never fails: an unresolvable family falls back to the bundled sans font. File problems while scanning the
additional directories or the installed fonts (missing, locked, or malformed files, access errors) are treated as "no such
font" and skipped, never thrown. A null text, line list, or text item is `ArgumentNullException`, which is not converted.
A size that is not positive or not finite measures as zero. The unit performs no network access and writes nothing.

#### Dependencies

- **`VegaLiteFontOptions`** - the public font options of the `VegaLiteDocument` unit
- **`SceneText`** and **`SceneBounds`** - the scene types of the `SceneModel` unit
- **Core `CanvasNet`** - `TrueTypeFont` and `SystemFontCatalog` (the `Fonts` subsystem, including the bundled Liberation
  fonts) and `TextRenderer` (the `Rendering` subsystem), through their public API
- **.NET base class library** - `System.Collections.Concurrent`, `System.IO`, `System.Globalization`

#### Callers

`VegaLiteDocument` creates one measurer per render. `TitleBuilder` and `AxisBuilder` measure text with it, and
`ScenePainter` resolves fonts and draws the lines it produces. The tests in
`test/DemaConsulting.CanvasNet.VegaLite.Tests/Guides/TextMeasurerTests.cs` call it directly through `InternalsVisibleTo`.

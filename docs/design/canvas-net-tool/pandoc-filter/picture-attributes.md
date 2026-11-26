### PictureAttributes

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`PictureAttributes` reads the attributes of a picture or a code block (Mermaid, PlantUML, MathML, Vega-Lite, or OpenSCAD
marked `draw`) for the Pandoc filter: which
kind of
input a picture names, the checks of every drawing attribute with the messages of the Lua filter, the defaults of each
kind, the sorted tool options, and the attributes that stay on the picture. It is a literal port of `kind_of`,
`is_svg`, the `OPTIONS` table, `build_options`, and `block_name` of the Lua filter (version 16). The unit is the
internal static class `PictureAttributes` in `PictureAttributes.cs`, which also holds the supporting records
`PictureOptions` and `BlockClass`. The class is stateless and thread-safe.

#### Data Model

**PictureOptions** (internal sealed record): `Options` (`IReadOnlyList<string>`, the `--name=value` options sorted by
their bytes) and `Keep` (`IReadOnlyList<KeyValuePair<string, string>>`, the attributes that stay, in their original
order).

**BlockClass** (internal sealed record): `Class` (the class as the author writes it), `Kind` (the kind the block is
drawn as), and `NeedsDraw` (`true` when the block is drawn only together with the class `draw`).

**Kinds** (constants): `vegalite`, `scad`, `vsdx`, `pptx`, `pdf`, `mermaid`, `plantuml`, and `mathml`.

**Suffixes**: the name ends checked in order: `.vl.json` and `.vl` (`vegalite`), `.scad`, `.vsdx`, `.pptx`, `.pdf`,
`.mmd` and `.mermaid` (`mermaid`), `.puml`, `.plantuml`, and `.pu` (`plantuml`; an `.iuml` include fragment is not
a picture), and `.mml` (`mathml`). `SvgSuffix` = `.svg` is the name end of a drawing (`IsSvg`).

**BlockClassTable** (private): the `BlockClass` rows of the code blocks the filter draws, in order: `mermaid`
(`mermaid`), `plantuml` (`plantuml`), `mathml` (`mathml`), `vega-lite` (`vegalite`), `vegalite` (`vegalite`), and
`openscad` (`scad`, `NeedsDraw`). The first row a block has wins. An `openscad` block is opt-in because documentation
often shows OpenSCAD source as highlighted code; `DrawClass` = `draw` is the class that asks for the picture.

**PictureSuffixes**: `SvgSuffix` followed by the `Suffixes`, in order, the lowercase name ends of every local picture
the filter may change. **BlockClasses**: the classes of the rows without `NeedsDraw` (drawn by default).
**DrawBlockClasses**: the classes of the rows with `NeedsDraw` (`openscad`). These lists are the filter's own registry
of what it handles: `PandocFilterShim.Generate` writes them into the shim's fast path, so the shim starts the tool for
exactly the pictures and code blocks the engine does not leave alone.

**Limits and defaults**: `ScadDefaultSize` = `1600x1200`, `DefaultBackground` = `white` (models only),
`MaxTriangles` = `2000000`, `MathMlDefaultScale` = `2` (equations only), and `MaxPixels` = 8192.

**Word lists**: views `iso`, `front`, `back`, `left`, `right`, `top`, `bottom`; projections `ortho`, `orthographic`,
`perspective`; themes `default`, `base`, `dark`, `forest`, `neutral`.

**KeepList** (private): the kept attributes in the order first met; setting a known key replaces its value in place.

#### Key Methods

**KindOf(src)**: returns `null` when `src` has a URL scheme (`HasUrlScheme`: a scheme of two or more characters, so
`C:` is not one), otherwise the kind of the first suffix the ASCII-lowercased name ends with, or `null`.

**IsSvg(src)**: `true` when `src` has no URL scheme and ends in `.svg` in any letter case; the engine checks it before
`KindOf`. Neither method cuts a fragment or query off `src`, so `report.pdf#page=2` is left alone.

**BlockOf(classes)**: the first row of `BlockClassTable` whose class the block has (an exact, case-sensitive
comparison, so `Mermaid` is not `mermaid`), where a `NeedsDraw` row counts only when the classes also hold `draw`; or
`null` when it finds none and the block is left alone (a plain `openscad` block included). **BlockKindOf(classes)**:
the `Kind` of that row, or `null`.

**Build(name, kind, attributes)**: walks the attributes in order. Every key that starts with `param-` or `define-` is
checked, a bare `param-` or `define-` included, so none stays on the picture. `param-NAME` (Vega-Lite only, `NAME` not
empty and without `=`) adds `--param=NAME=value`; `define-NAME` (`.scad` only, `NAME` matching
`^[A-Za-z_][A-Za-z0-9_]*$`) adds `--define=NAME=value`; a drawing option (`size`, `scale`, `background`, `page`, `dpi`,
`view`, `azimuth`, `elevation`, `projection`, `theme`) is checked for its kind and value and stored by key, so a
repeated option keeps its last value; every other attribute goes to the keep list. Each value must be plain text (not
empty, no character 0 to 31 or 127). Then it refuses `size` with `scale`, adds the model defaults (`size` unless `size`
or `scale` is given, `background` unless given, and `max-triangles` always), adds `page=1` to a paged kind without
`page`, adds `scale=2` to an equation without `size` or `scale` (sharp in print; an equation takes `size`, `scale`,
and `background` only, so `theme` and the options of the other kinds are refused), and sorts all options by their
bytes. The value checks are:

- `size`: `WIDTHxHEIGHT`, each 1 to 8192, written without leading zeros.
- `scale`: 0.001 to 8192; `azimuth`: -360000 to 360000; `elevation`: -90 to 90; `dpi`: above 0 up to 2400, a whole
  number for `.vsdx`. Only plain decimals (`-?d+`, `-?d+.d+`, `-?.d+`) are numbers, written with `FormatNumber`.
- `background`: `transparent`, `#RRGGBB`, `#RRGGBBAA`, or a name of ASCII letters, kept as written.
- `page`: a whole number from 1, written without leading zeros.
- `view`, `projection`, `theme`: one of the word lists, ignoring ASCII case, written in lowercase.

**NaturalSizeNeeded(kind, attributes)**: `true` for a `mathml` picture whose attributes have none of `size`, `width`,
and `height`, and whose `style` declares no `width` or `height` (property names compared without case): the engine then
shows it at its natural size (see *PandocFilterEngine*), so every equation shows at the size of the text around it
whatever scale it is drawn at. An author's own `size`, `width`, `height`, or sizing `style` is kept as written and
nothing is added; an author's `scale` still gets the natural size.

**MathMlFontSize**: 16, the font size in pixels an equation's natural size is measured at (the default
`MathMlRenderOptions.FontSize`, pinned by a test), and so one `em` of the size the filter shows an equation at.

**Em(pixels)**: a length of the natural size in `em`: `pixels / 16` written with `FormatNumber`, followed by `em`
(`52` gives `3.25em`). A whole number of pixels over 16 has at most four decimals, so the text is exact.

**WithNaturalSize(keep, width, height, baseline, inRunningText)**: the keep list with `width` set to `Em(width)` after
the other attributes. No `height` is set. In running text, `style` is first set to
`display:inline;margin:0;vertical-align:-D` (where `D` is `Em(height - baseline)`, the depth below the baseline, and
`vertical-align:0` when the depth is 0). The author's own style follows it, trimmed of white space and trailing `;`,
after a `;`, in the position of the author's `style` attribute when there is one. On its own, the author's style is
kept in its position, trimmed the same way, so Pandoc's appended `;width:` joins it with one `;`. An author's style with
nothing left once trimmed (only `;` and white space) is not written on its own. The choices are:

- **em, not px.** The natural size is measured at a 16 pixel font, so `1em` is 16 natural pixels. A browser at its
  default 16 px font shows exactly the natural size, a 10 point PDF body shows the equation at body-text size (a pixel
  width would be 1.2 times too big there), and an equation in a caption, heading, or table follows that text's size.
  Pandoc's HTML writer turns an `em` width into a `style` width (`style="width:3.25em"`), which every browser and
  WeasyPrint honor; a pixel width becomes the HTML attribute `width`, which WeasyPrint ignores without
  `--presentational-hints`. The LaTeX and Word writers read an `em` width too.
- **Width only.** The picture keeps its aspect ratio (`height:auto` in a style sheet, `keepaspectratio` in LaTeX), so
  an equation wider than a narrow column is scaled down whole instead of distorted.
- **Placement in running text.** `display:inline` and `margin:0` keep the equation in the line under a style sheet that
  makes every picture a block with a margin (as the DEMA template does); both are the HTML defaults of an image, so they
  are harmless elsewhere. `vertical-align` lowers the picture by its depth, so its baseline sits on the text's.
- **Author first in the cascade.** The filter's declarations come before the author's in one `style`, so any author
  declaration (`vertical-align`, `display`) wins by order. Pandoc 3.0 and later append the width of the attribute to
  that `style` (`consolidateStyles`), giving one attribute.

**BuildSvg(name, attributes)**: reads only `scale` (checked as above), one `--scale=` option per `scale` attribute in
order, and keeps every other attribute.

**SplitCaption(attributes)**: returns the last `caption` value (or `null`) and the other attributes as a keep list.

**BlockName(identifier, text, blockClass)**: `<class> block #<identifier>`, or `<class> block (<first line>)`, where the
class is the one the author wrote (`mermaid` by default; `plantuml`, `mathml`, `vega-lite`, `vegalite`, or `openscad`
for the other blocks) and the first line
follows any leading white space, has its trailing white space removed, and is cut to 40 characters (runes) followed by
`...`.

**FormatNumber(value)** and **FormatG(value, precision)**: C's `%.15g` and `%.Pg`: significant digits without trailing
zeros, fixed notation for exponents from -4 below the precision, otherwise a lowercase `e`, a sign, and at least two
exponent digits (`1e-05`); zero is `0` or `-0`.

#### Error Handling

Every refusal is a `ToolException` with `ErrorCodes.PandocFilterError` (`pandoc-filter-error`) and no hint, with the
message of the Lua filter, for example:

- `'<name>' has <key>=<value>; the value must be text without control characters`
- `'<name>' has size=<value>; write WIDTHxHEIGHT in pixels (each 1 to 8192), for example 1600x1200`
- `'<name>' has <key>=<value>; write a number from <min> up to <max>` (`a whole number` and `above` for `dpi` as
  applicable)
- `'<name>' has background=<value>; write a color such as #F0F0F0, a name such as white, or transparent`
- `'<name>' has page=<value>; write the page number, counting from 1`
- `'<name>' has <key>=<value>; write <words>` for `view`, `projection`, and `theme`
- `'<name>' has <key>=<value>, but <key> applies to <kinds> only` (`.vsdx, .pptx and .pdf files`, `.scad models`, or
  `Mermaid diagrams`), and the same for `define-NAME` (`.scad models`) and `param-NAME` (`Vega-Lite charts`)
- `'<name>' has <key>; the variable name must be letters, digits and underscores, not starting with a digit`
- `'<name>' has <key>; the parameter name cannot be empty`
- `'<name>' has <key>; the parameter name cannot contain an equals sign`
- `'<name>' has both size and scale; use one of them`

#### Dependencies

- **FilterPaths** - `AsciiLower`, `SchemeOf`, `SortBytes`, and the Lua white-space trims.
- **ToolException** (tool) and **ErrorCodes** (Viewer library) - the error and its code.

#### Callers

- **PandocFilterShim** - `BlockClasses`, `DrawBlockClasses`, and `PictureSuffixes`, written into the shim's fast path.
- **PandocFilterEngine** - `IsSvg`, `KindOf`, `BlockOf`, `DrawClass`, `Build`, `NaturalSizeNeeded`,
  `WithNaturalSize`, `BuildSvg`, `BlockName`, and `SplitCaption` while it plans the pictures of a document.
- **DependencyHasher** - the kind constants and `KindOf` to find the composite links of an SVG drawing.

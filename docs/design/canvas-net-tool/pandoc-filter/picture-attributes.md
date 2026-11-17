### PictureAttributes

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`PictureAttributes` reads the attributes of a picture or a Mermaid code block for the Pandoc filter: which kind of
input a picture names, the checks of every drawing attribute with the messages of the Lua filter, the defaults of each
kind, the sorted tool options, and the attributes that stay on the picture. It is a literal port of `kind_of`,
`is_svg`, the `OPTIONS` table, `build_options`, and `block_name` of the Lua filter (version 16). The unit is the
internal static class `PictureAttributes` in `PictureAttributes.cs`, which also holds the supporting record
`PictureOptions`. The class is stateless and thread-safe.

#### Data Model

**PictureOptions** (internal sealed record): `Options` (`IReadOnlyList<string>`, the `--name=value` options sorted by
their bytes) and `Keep` (`IReadOnlyList<KeyValuePair<string, string>>`, the attributes that stay, in their original
order).

**Kinds** (constants): `vegalite`, `scad`, `vsdx`, `pptx`, `pdf`, and `mermaid`.

**Suffixes**: the name ends checked in order: `.vl.json` and `.vl` (`vegalite`), `.scad`, `.vsdx`, `.pptx`, `.pdf`,
`.mmd` and `.mermaid` (`mermaid`). `SvgSuffix` = `.svg` is the name end of a drawing (`IsSvg`).

**BlockClassKinds** (private): the classes of the code blocks the filter draws, each with its kind: `mermaid`
(`mermaid`).

**PictureSuffixes**: `SvgSuffix` followed by the `Suffixes`, in order, the lowercase name ends of every local picture
the filter may change. **BlockClasses**: the classes of `BlockClassKinds`. These two lists are the filter's own
registry of what it handles: `PandocFilterShim.Generate` writes them into the shim's fast path, so the shim starts
the tool for exactly the pictures and code blocks the engine does not leave alone.

**Limits and defaults**: `ScadDefaultSize` = `1600x1200`, `DefaultBackground` = `white` (models only),
`MaxTriangles` = `2000000`, and `MaxPixels` = 8192.

**Word lists**: views `iso`, `front`, `back`, `left`, `right`, `top`, `bottom`; projections `ortho`, `orthographic`,
`perspective`; themes `default`, `base`, `dark`, `forest`, `neutral`.

**KeepList** (private): the kept attributes in the order first met; setting a known key replaces its value in place.

#### Key Methods

**KindOf(src)**: returns `null` when `src` has a URL scheme (`HasUrlScheme`: a scheme of two or more characters, so
`C:` is not one), otherwise the kind of the first suffix the ASCII-lowercased name ends with, or `null`.

**IsSvg(src)**: `true` when `src` has no URL scheme and ends in `.svg` in any letter case; the engine checks it before
`KindOf`. Neither method cuts a fragment or query off `src`, so `report.pdf#page=2` is left alone.

**BlockKindOf(classes)**: the kind of the first entry of `BlockClassKinds` whose class the block has (an exact,
case-sensitive comparison, so `Mermaid` is not `mermaid`), or `null` when it has none and is left alone.

**Build(name, kind, attributes)**: walks the attributes in order. Every key that starts with `param-` or `define-` is
checked, a bare `param-` or `define-` included, so none stays on the picture. `param-NAME` (Vega-Lite only, `NAME` not
empty and without `=`) adds `--param=NAME=value`; `define-NAME` (`.scad` only, `NAME` matching
`^[A-Za-z_][A-Za-z0-9_]*$`) adds `--define=NAME=value`; a drawing option (`size`, `scale`, `background`, `page`, `dpi`,
`view`, `azimuth`, `elevation`, `projection`, `theme`) is checked for its kind and value and stored by key, so a
repeated option keeps its last value; every other attribute goes to the keep list. Each value must be plain text (not
empty, no character 0 to 31 or 127). Then it refuses `size` with `scale`, adds the model defaults (`size` unless `size`
or `scale` is given, `background` unless given, and `max-triangles` always), adds `page=1` to a paged kind without
`page`, and sorts all options by their bytes. The value checks are:

- `size`: `WIDTHxHEIGHT`, each 1 to 8192, written without leading zeros.
- `scale`: 0.001 to 8192; `azimuth`: -360000 to 360000; `elevation`: -90 to 90; `dpi`: above 0 up to 2400, a whole
  number for `.vsdx`. Only plain decimals (`-?d+`, `-?d+.d+`, `-?.d+`) are numbers, written with `FormatNumber`.
- `background`: `transparent`, `#RRGGBB`, `#RRGGBBAA`, or a name of ASCII letters, kept as written.
- `page`: a whole number from 1, written without leading zeros.
- `view`, `projection`, `theme`: one of the word lists, ignoring ASCII case, written in lowercase.

**BuildSvg(name, attributes)**: reads only `scale` (checked as above), one `--scale=` option per `scale` attribute in
order, and keeps every other attribute.

**SplitCaption(attributes)**: returns the last `caption` value (or `null`) and the other attributes as a keep list.

**BlockName(identifier, text)**: `mermaid block #<identifier>`, or `mermaid block (<first line>)` where the first line
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

- **PandocFilterShim** - `BlockClasses` and `PictureSuffixes`, written into the shim's fast path.
- **PandocFilterEngine** - `IsSvg`, `KindOf`, `BlockKindOf`, `Build`, `BuildSvg`, `BlockName`, and `SplitCaption` while
  it plans the pictures of a document.
- **DependencyHasher** - the kind constants and `KindOf` to find the composite links of an SVG drawing.

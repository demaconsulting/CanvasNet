# SVG Test Fixtures

Most of these SVG files are small, hand-authored documents created specifically for this
repository to exercise `SvgCodec`'s supported subset end to end via real files on disk (mirroring
the pattern used by `TiffFixtures`/`JpegFixtures`, but with no third-party source corpus behind
them, unlike `PngSuite`). For those files, there is no external provenance to document: each was
written from scratch for CanvasNet and is licensed under the same MIT license as the rest of this
repository. Two files - `SvgGradient.svg` and `InkscapeFilters.svg` - are real-world third-party
fixtures sourced from Wikimedia Commons; see the "Real-world third-party fixtures" section below
for their provenance and licensing.

| File | Exercises |
| ------ | ----------- |
| `shapes.svg` | `rect`, `circle`, `ellipse`, `polygon` basic shapes with solid fills |
| `groups-and-transforms.svg` | `g` grouping with fill inheritance, combined `translate`/`rotate` transforms |
| `gradient.svg` | `linearGradient` with `stop` children, referenced via `fill="url(#id)"` |
| `use-reference.svg` | `defs` (non-rendering template storage) and `use` (reference + `x`/`y` offset) |
| `text.svg` | `text` rendering, paired in tests with the real `FontFixtures/OpenSans-Regular.ttf` font |
| `tolerant-unsupported.svg` | A well-formed but out-of-scope `filter`, alongside a `rect` that still renders |
| `arrow-markers.svg` | `marker` referenced via `marker-end`, `orient="auto"`, `markerUnits="userSpaceOnUse"` |

## Real-world third-party fixtures

Unlike the hand-authored files above, `SvgGradient.svg` and `InkscapeFilters.svg` are real,
unmodified, third-party documents sourced from Wikimedia Commons - each dedicated to the public
domain under the CC0 1.0 Universal Public Domain Dedication. See `WikimediaCommons.LICENSE` in
this folder for full source URLs, authorship, license text, and retrieval dates.

| File | Exercises |
| ------ | ----------- |
| `SvgGradient.svg` | `userSpaceOnUse`/default `linearGradient`s, percentage stop offsets, `rgb()`/hex `stop-color` |
| `InkscapeFilters.svg` | `defs`/`use` templating, composed `rotate`/`scale`/`translate`, tolerated `filter` refs |

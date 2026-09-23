# SVG Test Fixtures

These SVG files are small, hand-authored documents created specifically for this repository to
exercise `SvgCodec`'s supported subset end to end via real files on disk (mirroring the pattern
used by `TiffFixtures`/`JpegFixtures`, but with no third-party source corpus behind them, unlike
`PngSuite`). There is no external provenance to document: every file was written from scratch for
CanvasNet and is licensed under the same MIT license as the rest of this repository.

| File | Exercises |
| ------ | ----------- |
| `shapes.svg` | `rect`, `circle`, `ellipse`, `polygon` basic shapes with solid fills |
| `groups-and-transforms.svg` | `g` grouping with fill inheritance, and combined `translate`/`rotate` transform functions |
| `gradient.svg` | `linearGradient` with `stop` children, referenced via `fill="url(#id)"` |
| `use-reference.svg` | `defs` (non-rendering template storage) and `use` (reference + `x`/`y` offset) |
| `text.svg` | `text` rendering, paired in tests with the real `FontFixtures/OpenSans-Regular.ttf` font |
| `tolerant-unsupported.svg` | A well-formed but out-of-scope `filter` element alongside an ordinary `rect`, proving the rest of the document still renders |

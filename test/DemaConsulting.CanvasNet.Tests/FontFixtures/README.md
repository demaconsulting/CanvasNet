# Font Test Fixtures

`OpenSans-Regular.ttf` is the real, production "Open Sans" TrueType font (Regular weight),
sourced from the [Open Sans](https://www.opensans.com/) project and licensed under the
[SIL Open Font License, Version 1.1](https://openfontlicense.org/) - see `OpenSans.LICENSE`
(the license's full text, as required for redistribution) alongside it in this folder.

`SourceSans3-Regular.otf` is the real, production "Source Sans 3" CFF/OpenType font (Regular
weight), sourced from Adobe's [source-sans](https://github.com/adobe-fonts/source-sans) project
(the `OTF/SourceSans3-Regular.otf` release asset - Adobe's own repository, unlike most Google
Fonts family repositories, ships genuine `OTTO`-tagged/CFF-outline `.otf` files rather than only
`.ttf`) and licensed under the SIL Open Font License, Version 1.1 - see `SourceSans3.LICENSE`
(the license's full text) alongside it in this folder.

`OpenSans-SourceSans3.ttc` is a 2-face TrueType Collection (`ttcf`) container, locally assembled
(not redistributed as obtained from either upstream project) by concatenating the exact bytes of
`OpenSans-Regular.ttf` (face 0) and `SourceSans3-Regular.otf` (face 1) behind a `ttcf` header,
with each face's own table directory entries rewritten so their `offset` fields are absolute from
the start of the container rather than from the start of that face's own bytes - the same
transformation a genuine multi-face `ttcf` container's table directory entries already reflect.
No table data byte was altered, added, deduplicated, or removed; every table remains wholly
contained within its own face's original byte range. This container was assembled directly from
this folder's two already-present, unmodified upstream fixtures via a short one-off script (not
itself part of this repository) purely to exercise `TrueTypeFont`'s multi-face collection support
against genuine SFNT table data end to end, since no sufficiently small, permissively-licensed
real-world `.ttc` fixture was readily available to source instead. The SIL OFL's Section 2 permits
bundling a Reserved Font Name-carrying font together with other software/fonts (including inside
a single container file) provided the font itself is not sold by itself and any modified version
does not retain the original name as its own font name; this container carries both source fonts'
unmodified bytes side by side rather than a modified derivative of either, and is not distributed
or sold on its own, so no restriction is violated. Both `OpenSans.LICENSE` and
`SourceSans3.LICENSE` therefore continue to attribute and license every byte this container
contains.

These fixtures are used only by real-world integration tests that exercise `TrueTypeFont` against
actual production font files, complementing the hand-rolled synthetic fixtures built by
`TestSupport/SyntheticFontBuilder` for `Fonts` unit tests. The OFL permits embedding/bundling and
redistributing a font with software; it may not be sold standalone, and a modified version may not
use the original font's name - neither restriction applies to this repository's use of these fonts
as unmodified, bundled test fixtures (and their locally-assembled `ttcf` combination, per the
paragraph above).

`SourceSans3-Regular.otf`'s real-font integration test deliberately selects a single,
straight-line-only glyph (capital `H`) rather than every Latin letter: this library's CFF Type 2
charstring interpreter supports a fixed operator set (see `CffCharstringInterpreter`'s own
documentation) that deliberately excludes the two-byte flex escape operators and the `rcurveline`/
`rlinecurve` operators (24/25), all of which this actual production font's more rounded/complex
glyphs (for example capital `A` and `V`) do use. `H`, like several other straight-sided capitals
(`I`, `L`, `T`, `F`) and the digit `1`, was verified to decode successfully with this
implementation's supported operator set.

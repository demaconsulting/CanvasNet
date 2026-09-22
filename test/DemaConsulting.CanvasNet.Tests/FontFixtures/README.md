# Font Test Fixtures

`OpenSans-Regular.ttf` is the real, production "Open Sans" TrueType font (Regular weight),
sourced from the [Open Sans](https://www.opensans.com/) project and licensed under the
[SIL Open Font License, Version 1.1](https://openfontlicense.org/) - see `OpenSans.LICENSE`
(the license's full text, as required for redistribution) alongside it in this folder.

It is used only by real-world integration tests that exercise `TrueTypeFont` against an actual
production font file, complementing the hand-rolled synthetic fixtures built by
`TestSupport/SyntheticFontBuilder` for `Fonts` unit tests. The OFL permits embedding/bundling and
redistributing the font with software; it may not be sold standalone, and a modified version may
not use the "Open Sans" name - neither restriction applies to this repository's use of the font
as an unmodified, bundled test fixture.

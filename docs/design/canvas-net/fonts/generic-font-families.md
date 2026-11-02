### GenericFontFamilies

<!-- cspell:ignore Menlo Consolas Segoe -->

`GenericFontFamilies` is a public software unit in the `Fonts` subsystem. It is a small static
class that holds the one table of CSS generic font families and well-known aliases that maps a
family name onto one of the three bundled Liberation designs, and the one CSS `font-family` list
splitter. It is the policy data `FontResolver` is built on, and is public so a caller that
resolves fonts its own way (for example the Pdf `FindBestMatch` flow) can classify names the same
way.

#### Purpose

Several libraries each carried their own copy of "which family names mean sans-serif, serif or
monospace" and of "split a CSS font-family list": the Svg codec, the Vega-Lite text measurer and
the OpenSCAD font resolver all differed slightly. `GenericFontFamilies` states the policy once:
the supported CSS generic keywords (`sans-serif`, `serif`, `monospace`, `system-ui`, the `ui-`
sans-serif, serif and monospace variants, `cursive` and `fantasy`; not `ui-rounded`, `math`,
`emoji` or `fangsong`) are always recognized, and the well-known aliases (`Arial`,
`Times New Roman`, `Courier New`, ...) only when the caller asks for them, because mapping them
to the bundled fonts is a determinism decision (the same result on every machine) and not every
caller wants it.

#### Data Model

- Three read-only `HashSet<string>` tables of CSS generic keywords (sans-serif: `sans-serif`,
  `system-ui`, `ui-sans-serif`, `cursive`, `fantasy`; serif: `serif`, `ui-serif`; monospace:
  `monospace`, `ui-monospace`), compared with `StringComparer.OrdinalIgnoreCase`.
- Three read-only `HashSet<string>` tables of common aliases, compared the same way: sans
  (`-apple-system`, `blinkmacsystemfont`, `helvetica`, `helvetica neue`, `arial`, `verdana`,
  `tahoma`, `trebuchet ms`, `segoe ui`, `liberation sans`, `dejavu sans`, `roboto`, `open sans`),
  serif (`times`, `times new roman`, `georgia`, `liberation serif`, `palatino`,
  `palatino linotype`, `book antiqua`, `cambria`, `garamond`) and monospace (`courier`,
  `courier new`, `menlo`, `consolas`, `monaco`, `lucida console`, `liberation mono`,
  `dejavu sans mono`).

#### Key Methods

##### TryClassify(familyName, includeCommonAliases, out serif, out fixedPitch)

Returns `false` (both outputs `false`) for a `null` or empty name. Otherwise checks the sans
keywords (and, when `includeCommonAliases`, the sans aliases), then the serif ones, then the
monospace ones; the first table that contains the name decides the outputs (neither flag set
means sans-serif). The name is neither trimmed nor unquoted - that is `SplitFamilyList`'s job - and
nothing is case-folded beyond the ordinal case-insensitive comparison.

##### SplitFamilyList(cssFontFamily)

Reads the list once, left to right: a comma outside quotes ends a name; a single or a double quote
opens a quoted run that only the same quote character closes (an unterminated quote runs to the
end); the quote characters themselves are dropped; each name is trimmed of surrounding white
space and empty names are dropped. A `null` or white-space list gives an empty list.

#### Error Handling

Neither method throws: `TryClassify` reports an unrecognized or absent name as `false`, and
`SplitFamilyList` tolerates any text.

#### Dependencies

None beyond the .NET base class library.

#### Callers

- `FontResolver` classifies every family of a list and splits the list.
- A library user may call either method directly.

#### Thread Safety

The class holds only read-only tables, so every member is safe to call concurrently.

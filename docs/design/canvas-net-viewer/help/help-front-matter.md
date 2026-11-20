### HelpFrontMatter

![Help Structure](HelpView.svg)

#### Purpose

`HelpFrontMatter` splits a help card file into its YAML front matter and its Markdown body. It reads only the small
subset of YAML the cards use, without a package dependency, and reports anything else as an error that names the card
file and the line, so a broken card fails the unit tests instead of being guessed at. The meaning of the keys is not its
concern; `HelpCatalog` interprets them. It is the internal static class `HelpFrontMatter` in `HelpFrontMatter.cs`,
with its two records.

#### Data Model

- `FrontMatterValue` (internal sealed record): `Line` (the 1-based line the value starts on) and exactly one of
  `Scalar` (the text), `Items` (the values of a list), or `Map` (the entries of a list item that is a map, in order).
- `FrontMatterDocument` (internal sealed record): `Properties` (the top-level keys and values, in order), `Body` (the
  text after the closing line, with LF line endings, without one leading blank line, and without trailing line feeds),
  and `BodyLine` (the 1-based line the body starts on, so that `HelpCatalog` can report a bad link at its line).

The records are immutable.

#### Key Methods

**Parse(text, fileName)**: requires the first line to be `---` and finds the closing `---` line, parses the lines
between them, and extracts the body. The subset it reads:

- `key: value` at column 0, where a key is lower case letters and dashes;
- `key:` alone, followed by list items `- value` indented by two spaces;
- a list item that is a map: `- key: value` indented by two spaces, followed by `key: value` lines indented by four;
- plain, single-quoted (`''` escapes a quote), and double-quoted (only `\\` and `\"`) values on one line;
- blank lines and full-line `#` comments.

**Error(fileName, line, reason)**: creates the `InvalidOperationException` with the message
`Help card '<file>', line <n>: <reason>.`; `HelpCatalog` uses it for its own errors so every message reads the same.

#### Error Handling

Anything outside the subset is an `InvalidOperationException` from `Error`: a tab, a flow sequence or mapping, a block
scalar, an anchor, an alias, a tag, a trailing comment, an unclosed quote, an unsupported escape, a plain value that
holds a colon followed by a space, a repeated key, a key without a value or items, deeper nesting, or a missing opening
or closing fence. A null argument throws `ArgumentNullException`. Nothing is handled locally.

#### Dependencies

- None beyond the BCL (`System.Text.RegularExpressions` for the key pattern).

#### Callers

- **HelpCatalog** - parses every card file of the library and of a host's extension.

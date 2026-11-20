### HelpFrontMatter

#### Verification Approach

`HelpFrontMatter` is verified with unit tests in `HelpFrontMatterTests.cs` that call the internal `Parse` on card texts
written in the tests: one text that uses every construct of the subset, a table of texts that each use one construct
outside it, and texts without an opening or a closing fence. The parser has no dependencies, so nothing is mocked. Its
use on the real cards is verified by the tests of `HelpCatalog`, which load every embedded card.

#### Test Environment

N/A - standard test environment; the tests use only in-memory text.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Every construct of the subset is read with its value, order, and line, and the body and its first line are returned.
- Every construct outside the subset, and a missing fence, is an `InvalidOperationException` whose message names the
  file and the line.

#### Test Scenarios

**HelpFrontMatter_Parse_SupportedSubset_ReadsEveryConstruct**: a card with a plain scalar, a single-quoted scalar
with an escaped quote and a colon, a list of scalars (one double-quoted with escapes), a list of maps, comments, and
blank lines is read key by key in order with the values and their lines, and the body that follows the closing fence
(without its leading blank line) is returned with the line it starts on (17).

**HelpFrontMatter_Parse_OutsideTheSubset_ThrowsWithFileAndLine**: a tab, a flow sequence and a flow mapping, the two
block scalars, an anchor, an alias, a tag, a trailing comment (also after a quoted value), an unclosed quote, an
unsupported escape, a colon followed by a space in a plain value, a plain value that starts with a backtick, a
repeated key (also in a map), wrong indentation (of a list item, a nested item, and a deeper map entry), a map entry
without a value, a key without a value or items, and a key that is not lower case each fail with
`Help card '<file>', line <n>: ` at the line of the construct and the reason.

**HelpFrontMatter_Parse_MissingFence_ThrowsWithFileAndLine**: a card that does not start with `---` fails at line 1
("opens the front matter"), and a card without a closing `---` fails at its last line ("no closing '---'").

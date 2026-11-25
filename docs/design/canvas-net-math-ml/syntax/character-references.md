### CharacterReferences

![Syntax Structure](MathMlSyntaxView.svg)

The `CharacterReferences` unit is a software unit of the `Syntax` subsystem. Its source files are
`CharacterReferences.cs`, which defines the internal static partial class `CharacterReferences`, the record
`PrePassResult`, and the class `LocationMap` with its `Breakpoint` record, and the generated
`CharacterReferences.Table.cs`, which holds the name and value pairs of the W3C set.

#### Purpose

MathML is usually written with the named references of the W3C HTML+MathML set (`&alpha;`, `&InvisibleTimes;`), which
an XML reader only knows from a document type declaration, while untrusted XML is read with document types prohibited.
`CharacterReferences` rewrites those references into numeric references that every XML reader decodes, refuses what
cannot be read safely, and maps the positions of the rewritten text back to the original. The unit's behavior is
specified by the `CanvasNetMathMl-Syntax-CharacterReferences-*` requirements in
`docs/reqstream/canvas-net-math-ml/syntax/character-references.yaml`.

#### Data Model

- The table - a `FrozenDictionary<string, string>` (ordinal, case-sensitive) of the 2125 names of
  `htmlmathml-f.ent` with their values of one or two code points, built once from the generated pairs (a property, so
  the order of static initializers across the partial files cannot matter). The double-escaped values of the source
  (`&#38;#38;`) are already unescaped by the generator.
- The five XML names (`lt`, `gt`, `amp`, `quot`, `apos`) and the longest name read as a reference (32 characters).
- `PrePassResult(Text, Map, DoctypeLocation)` - the rewritten text, its location map, and the location of a document
  type declaration, if any.
- `LocationMap` - the source name and, per changed line, a sorted list of `Breakpoint(OutputColumn, Delta,
  PinnedColumn)`: where a replacement starts in the output, the column shift after it, and the original column of its
  ampersand. A finished map is only read, so it is safe for concurrent reads.
- Per call (private): the `Scanner` with the position, line, and column in the original text, the shift of the current
  line, the scan state (`Text`, `Tag`, `Comment`, `CData`, `ProcessingInstruction`), and the quote of the current
  attribute value.

#### Key Methods

- `Rewrite(text, sourceName)` - one linear scan. In text, `<!--`, `<![CDATA[`, and `<?` enter the states that copy up to
  their terminators unchanged (an unterminated construct copies the rest for the XML reader to report); `<!DOCTYPE`
  records its location and copies the rest of the text unchanged, so that the XML reader refuses it; an XML declaration
  is checked for its encoding first. In text and inside the quoted attribute values of a tag, `&` followed by `#` is
  kept; `&` followed by 1 to 32 ASCII letters or digits and `;` is kept for the five XML names, replaced by one
  `&#xHHHH;` per code point for a known name (a numeric reference, never the literal character, because `&LT;`,
  `&AMP;`, and `&nvlt;` contain markup characters), and refused for an unknown name; other `&` text is left for the XML
  reader. Every replacement adds a breakpoint to its line.
- `CheckXmlDeclaration` - reads the `encoding` pseudo-attribute of `<?xml ...?>` and refuses every value other than
  UTF-8 and US-ASCII (ASCII case-insensitive), because the text was already decoded as UTF-8.
- `LocationMap.MapBack(line, column)` - a line without breakpoints maps to itself; otherwise the column inside a
  replacement maps to its ampersand and a column after it is shifted back by the delta of the last breakpoint before
  it.
- `PositionOf(text, index)` - the one-based line and column of an index, counting line breaks (`\r\n`, a lone `\r`, or
  `\n`) and UTF-16 code units as the XML reader does.
- `TryGetValue(name)` and `Count` - the lookup and the size of the table.

#### Error Handling

An unknown name is a `MathMlSyntaxException` (`mathml-syntax-error`) at its ampersand with the hint to check the
spelling (names are case-sensitive) or use a numeric character reference such as `&#x2062;`; a foreign encoding is one
at the encoding name with a hint to save the text as UTF-8. A reference never spans a line break, so the lines of the
two texts are the same and the output is at most 9/4 of the input (the shortest name, `&ac;`, becomes at most two
references of nine characters).

#### Dependencies

The root namespace's `MathMlLocation`, `MathMlSyntaxException`, and `MathMlDiagnosticCodes`, and the .NET base class
library.

#### Callers

`MathMlReader` rewrites every text with it and maps every reported position through its map.

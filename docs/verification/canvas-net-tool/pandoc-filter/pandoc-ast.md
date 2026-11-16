### PandocAst

#### Verification Approach

`PandocAst` is verified with unit tests in `PandocAstTests.cs` (xUnit). The tests call the static methods directly on
real Pandoc JSON: a sample document written by `pandoc -t json` (Pandoc 3.12.1) with a title that holds an image, a
figure whose image has an image in its description, a pipe table with relative column widths, non-ASCII text, and a
mermaid code block, and small documents built with the `PandocDoc` helper of `FilterTestSupport.cs`. Nothing is stubbed;
the expected visit order is the order a probe of Pandoc 3.12.1 printed for the same document.

#### Test Environment

N/A - standard test environment. The tests read and write only in memory.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- Images are found in Pandoc's visit order, and an unchanged document is written back byte for byte.
- A document nested 300 levels deep is read, walked, and written.
- API 1.23 and later 1.x versions are accepted; an older or another major version, input that is not JSON, and a
  malformed image are `corrupt-input`.
- The built elements, the caption placeholder, the warnings, and a rewritten image have the exact JSON Pandoc reads.

#### Test Scenarios

**PandocAst_FindAll_RealDocument_MetadataFirstAndChildrenBeforeParents**: the images of the sample are found as
`meta.mmd`, `in.mmd`, `in.mmd`, `out.mmd` (the metadata first, the image in the description of another before it), and
the code blocks separately as `id1`.

**PandocAst_Write_UnchangedRealDocument_IsByteIdentical**: reading and writing the sample gives the same bytes, without
a byte order mark, including the table's column widths and the non-ASCII text.

**PandocAst_Parse_NestingDeeperThan64_RoundTrips**: an image inside 300 block quotes is found, read as `deep.mmd`, and
the document is written back unchanged.

**PandocAst_Parse_ApiVersion_AcceptsPandoc3Only** (a theory over four versions): `1,23,1,2` and `1,24` are accepted;
`1,22,1` and `2,0` are `corrupt-input` whose message contains `(Pandoc 2.19)` and whose hint is
`This filter needs Pandoc 3 or later.`

**PandocAst_ReadAndParse_MalformedInput_ThrowsCorruptInput**: the text `not json` and an image whose target is not a
pair of strings are both `corrupt-input`, the second with the message `The document has a malformed Image element.`

**PandocAst_Builders_FigureParaSpanAndWarnings_HaveTheExactJson**: a figure with a caption placeholder and an image of a
class and an attribute, a paragraph with an image of an identifier, and two warnings in the metadata have the exact
JSON: the placeholder is a `Span` of class `canvasnet-markdown` with the attribute `markdown`, the figure has a null
short caption, and `canvasnet-warnings` is a `MetaList` of `MetaString` in order.

**PandocAst_RewriteImage_ChangesOnlySourceAndAttributes**: rewriting an image with an identifier, a class, two
attributes, a non-ASCII description, and a title to `gen/a.png` with one kept attribute changes only the source and the
attributes.

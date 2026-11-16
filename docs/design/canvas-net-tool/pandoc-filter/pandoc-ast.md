### PandocAst

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`PandocAst` reads, walks, and writes a Pandoc document in its JSON form (the document Pandoc writes with
`pandoc -t json` and the Lua shim pipes to the `pandoc-filter` command). Its single responsibility is the document
format: it touches only the elements the filter handles and writes every other element back as it was read, so the
filter never needs a full model of Pandoc's AST. The unit is the internal static class `PandocAst` in `PandocAst.cs`,
together with the supporting internal records `PandocAttr`, `PandocImage`, and `PandocCodeBlock` in the same file. The
class is stateless and thread-safe; a document tree itself is not safe for concurrent use.

#### Data Model

The document is a `System.Text.Json.Nodes` tree (`JsonObject` root with `pandoc-api-version`, `meta`, and `blocks`).
An element is a JSON object `{"t": type, "c": content}`; an element the filter does not handle is never interpreted.

**PandocAttr(Id, Classes, Attributes)** (record): the attributes of an element, `[id, [class], [[key, value]]]`; the
identifier is empty when there is none, the classes are in order, and the key-value attributes are in order with a
repeated key appearing each time.

**PandocImage(Attr, Src)** (record): the parts of an `Image` element the filter reads, its attributes and its source as
written.

**PandocCodeBlock(Attr, Text)** (record): the parts of a `CodeBlock` element the filter reads, its attributes and text.

**Constants**:

- `MaxDepth` = 4096: the deepest nesting the reader and the writer accept (the default of 64 is exceeded by a document
  with about ten levels of nested lists, quotes, or tables).
- `WarningsKey` = `canvasnet-warnings`: the reserved metadata key the warnings of a run are returned in.
- `MarkdownClass` = `canvasnet-markdown` and `MarkdownAttribute` = `markdown`: the class of the placeholder `Span` the
  shim replaces with a caption parsed as Markdown, and the attribute that holds the caption.
- `MinApiMinor` = 23 (private): the oldest Pandoc API read, 1.23 (Pandoc 3, which added `Figure`).

The writer options are compact output, `MaxDepth`, and `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`, so non-ASCII text
is written unescaped.

#### Key Methods

**Parse(utf8, pandocVersion)**: skips a leading UTF-8 byte order mark and parses the JSON with `MaxDepth`. The root
must be an object with a `blocks` array and a `meta` object, and `pandoc-api-version` must be an array of at least two
integers whose major is 1 and whose minor is 23 or later (a newer minor is accepted because the touched shapes are
checked). Returns the root object.

**Write(document)**: writes the tree with the writer options into a buffer and returns it as UTF-8 JSON without a byte
order mark. An unmodified element is written with the same content (a number keeps its text), so a document Pandoc
wrote and the filter did not change is written back byte for byte.

**FindAll(document, type)**: returns every JSON object whose `t` is the type, in the order Pandoc's Lua filter visits
it: the metadata first, then the blocks, each in document order, and a node's children before the node (post-order). It
uses an explicit stack, so a deep document cannot overflow the call stack. The engine calls it once for `Image` and once
for `CodeBlock`, which matches Pandoc's type-by-type traversal (every `Image` before any `CodeBlock`).

**ReadImage(node)** and **ReadCodeBlock(node)**: read `{"t":"Image","c":[Attr, [Inline], [src, title]]}` and
`{"t":"CodeBlock","c":[Attr, text]}`. The source, title, text, identifier, every class, and every key and value must be
strings and every attribute pair must have two items.

**RewriteImage(node, src, attributes)**: replaces only the source and the key-value attributes of an `Image` read by
`ReadImage`; the identifier, the classes, the description, and the title stay.

**Image(attr, description, src)**, **Para(...)**, **Plain(...)**: build the elements, an image with an empty
title. **Figure(identifier, caption, body)**: builds `{"t":"Figure","c":[Attr, [null, caption], body]}` with no
short caption and no classes or attributes. **MarkdownPlaceholder(markdown)**: builds an empty `Span` whose attributes
are `["", ["canvasnet-markdown"], [["markdown", markdown]]]`.

**SetWarnings(document, warnings)**: removes `canvasnet-warnings` from the metadata (an incoming value is replaced) and,
when there are warnings, sets it to a `MetaList` of `MetaString` in order.

#### Error Handling

Every failure is a `ToolException` with the code `corrupt-input` (`ErrorCodes.CorruptInput`):

- Input that is not JSON: `The input is not a Pandoc JSON document: <parser message>`.
- A root without `blocks` and `meta`: `The input is not a Pandoc JSON document: it has no blocks and meta.`
- A missing or non-integer API version: `The input is not a Pandoc JSON document: it has no pandoc-api-version.`
- These three have the hint `The pandoc-filter command reads the document Pandoc writes with 'pandoc -t json' (Pandoc 3
  or later).`
- An unsupported API version: `The document has Pandoc API version <version> (Pandoc <pandocVersion>); the pandoc-filter
  command reads version 1.23 or a later 1.x.` (the Pandoc part only when the version is known), with the hint
  `This filter needs Pandoc 3 or later.`
- An `Image` or `CodeBlock` of another shape: `The document has a malformed Image element.` (or `CodeBlock`), with the
  same hint as the first three.

`RewriteImage` expects a node already read by `ReadImage` and does not check the shape again.

#### Dependencies

- **System.Text.Json** (`JsonNode`, `Utf8JsonWriter`) - the document tree, the reader, and the writer.
- **ToolException** and **ErrorCodes** (Viewer library) - the `corrupt-input` errors.

#### Callers

- **PandocFilterEngine** - parses the input, finds the images and code blocks, reads and rewrites them, builds the
  replacement elements and caption placeholders, sets the warnings, and writes the output.
- **canvasnet.lua** (the shim, see *PandocFilterShim*) - relies on the `canvasnet-warnings` key and the
  `canvasnet-markdown` placeholder this unit defines; it does not call the class.

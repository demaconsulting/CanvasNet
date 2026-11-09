### HtmlSanitizer

![Syntax Structure](MermaidSyntaxView.svg)

The `HtmlSanitizer` unit is the fifth software unit in the `Syntax` subsystem. It is two source files in the
`DemaConsulting.CanvasNet.Mermaid.Syntax` namespace: `HtmlSanitizer.cs` defines the internal static class
`HtmlSanitizer` with its private records of a tag, an open element, and the tree being written, and `HtmlEntities.cs`
defines the grouped internal static class `HtmlEntities`, the table of named character references that the
sanitizer and the label builder decode.

#### Purpose

Mermaid stores every label through its `sanitizeText`, which runs DOMPurify: text without a `<` is kept as written,
and text with one is parsed as an HTML fragment, stripped of what DOMPurify removes, and written back as HTML. The
label builder reads what Mermaid stores, so `HtmlSanitizer` reproduces the results of that clean-up for the inline
HTML of labels: the same elements, attributes, and text survive, written character for character as a browser
serializes them. The clean-up is also the safety boundary of label HTML: scripts, event handlers, and links with an
unsafe scheme never reach the label builder.

The unit's behavior is specified by the `CanvasNetMermaid-Syntax-HtmlSanitizer-*` requirements in
`docs/reqstream/canvas-net-mermaid/syntax/html-sanitizer.yaml`.

#### Data Model

- The allow lists of DOMPurify's defaults: the HTML elements (less the `style` element, which Mermaid's
  configuration forbids) and the HTML attributes, plus any `data-` and `aria-` attribute; the attributes whose value
  is never checked as a URI; the elements removed with their content (DOMPurify's forbidden contents such as
  `script`, `style`, `template`, `iframe`, and `noscript`, plus `svg`, `math`, and their content elements); the
  raw-text elements; the void elements; the formatting elements that the HTML parser reopens; and the elements that
  the fragment parser ignores (`html`, `head`, and `body`).
- `HtmlEntities.Named` - a frozen, case-sensitive table of the 252 named references of HTML 4 (which HTML 5 keeps)
  and the HTML 5 names that Mermaid's own encoding produces (`apos`, `equals`, `num`, `semi`, and `colon`).
- The tree being written - the output, the stack of open elements (each with whether it is written and whether its
  content is dropped), and the formatting elements waiting to reopen.

The tables are immutable and every call has its own tree, so the unit is safe for concurrent use.

#### Key Methods

- `Sanitize(string text)` - DOMPurify's fast path (a text without `<` is returned unchanged); otherwise one pass over
  the text: text runs are decoded and escaped, comments and bogus comments are removed, start tags are read with
  their attributes (lower-case names, the first of two attributes of the same name winning), raw-text elements take
  everything up to their end tag, end tags close up to their open element, and every element still open at the end
  is closed, innermost first.
- `CleanAttributes` - keeps the allowed attributes whose value passes the URI check, and applies Mermaid's link
  hook: the `target` of a link is kept and written last, after a `rel="noopener"` that a `_blank` target adds.
- `IsAllowedUri` - DOMPurify's `IS_ALLOWED_URI` after removing white space and control characters: no scheme, one of
  the safe schemes (`http`, `https`, `ftp`, `ftps`, `mailto`, `tel`, `callto`, `sms`, `cid`, `xmpp`, and `matrix`), or
  a `data:` URI on an image or media element.
- `CloseElement` - closes the innermost open element of the name and every element inside it; the formatting
  elements inside it reopen before the next content, as the HTML parser's reconstruction does; an end tag without an
  open element is ignored, except `</br>`, which is a `<br>`.
- `DecodeReferences(string)` - decodes numeric references (null, surrogates, and values beyond Unicode become the
  replacement character) and the named references of `HtmlEntities` with their semicolon; an unknown name stays as
  written. The label builder also calls it.

The output is written as the HTML serializer writes it: lower-case names, attributes in double quotes, `&amp;`,
`&lt;`, `&gt;`, and `&nbsp;` in text (and `&quot;` in attributes), and no end tag for a void element.

**Preconditions and postconditions**: `text` must not be null. The result contains only allowed elements and
attributes, every element it opens is closed, and a text without `<` is returned unchanged.

#### Error Handling

No input makes the unit fail: markup that cannot be read (a `<` that does not start a tag, a tag cut off by the end
of the text) is text or is removed, as the HTML parser treats it. A null argument is an `ArgumentNullException`. The
unit performs no I/O and uses no recursion; the open elements are an explicit list.

#### Dependencies

- **.NET base class library** - `System.Collections.Frozen`, `System.Text.StringBuilder`, and `System.Globalization`

The unit has no dependency on another unit of the system.

#### Callers

`FlowchartModel` of the `Flowchart` subsystem sanitizes the text of every node, edge, and subgraph title, the
accessible title and description, and the diagram title, as Mermaid's flowchart database does; `LabelBuilder` of
the `Rendering` subsystem sanitizes the HTML of labels and decodes
character references with `DecodeReferences`. The tests in
`test/DemaConsulting.CanvasNet.Mermaid.Tests/Syntax/HtmlSanitizerTests.cs` call the unit directly through
`InternalsVisibleTo`; their expected texts are what Mermaid's `sanitizeText` gives for the same labels in a browser.

#### Known Differences From Mermaid

- **Simplified tree building.** Labels use a small inline subset of HTML, so the tree is built with a plain stack of
  open elements: an end tag closes up to its open element and is ignored otherwise, there is no implied closing of
  paragraphs, no table fixing, and only the common case of the adoption of formatting elements closed out of order.
- **SVG and MathML are removed** with their content, where DOMPurify would keep the allowed SVG and MathML elements.
- **Named references.** Only the references of `HtmlEntities` (the HTML 4 set and `apos`, `equals`, `num`, `semi`,
  and `colon`) are decoded; a browser decodes every one of the more than two thousand HTML 5 names, which stay as
  written here.
- **Original work.** The unit is not a port of DOMPurify's code; it reproduces DOMPurify's results for the label
  subset, checked against Mermaid in a browser, with the standard HTML names that DOMPurify allows by default.

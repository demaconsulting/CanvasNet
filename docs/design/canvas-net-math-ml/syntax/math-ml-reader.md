### MathMlReader

![Syntax Structure](MathMlSyntaxView.svg)

The `MathMlReader` unit is a software unit of the `Syntax` subsystem. Its source file is `MathMlReader.cs`, which
defines the internal static partial class `MathMlReader` and the record `MathReadResult`.

#### Purpose

`MathMlReader` turns MathML text into the equation tree with the location of every element, attribute, and text in the
original text, through the reference pre-pass and the hardened XML reader of the core, bounded and without recursion.
The unit's behavior is specified by the `CanvasNetMathMl-Syntax-MathMlReader-*` requirements in
`docs/reqstream/canvas-net-math-ml/syntax/math-ml-reader.yaml`.

#### Data Model

- `MathReadResult(Root, ElementCount, Reports, Annotations)` - the `math` element, the number of MathML elements read
  (annotation content and skipped elements excluded), the pending reports in text order (at most `MaxDiagnostics`,
  then one suppression notice), and the annotations in text order.
- `MathMlNamespace` - `http://www.w3.org/1998/Math/MathML`; the XLink namespace marks link attributes.
- Per call (private): `ReadState` with the `LocationMap`, the limits, the stack of open `Frame`s (name, attributes,
  location, children, the raw text of a token or of a run of text, and its location), the root, the element count, the
  count of the other elements charged to the element limit (the skipped elements, every element inside them, and every
  element inside an annotation), the reports with the flag of their suppression notice, and the annotations.

#### Key Methods

- `Read(text, sourceName, limits)` - rewrites the references (`CharacterReferences.Rewrite`), refuses an unpaired
  surrogate at its location, encodes the text as strict UTF-8, creates the reader with `SafeXml.CreateReader(stream,
  cap)` where the cap is the UTF-8 byte count of the rewritten text plus one, and runs the state over every node. The
  cap is in bytes because the XML reader counts the bytes of a short text before it has settled its decoder (a cap of
  the character count refused short texts with literal characters outside ASCII), and a text never has more characters
  than UTF-8 bytes; the rewritten text is at most 16/5 of the original (`&nGt;` becomes `&#x226B;&#x20D2;`), which was
  already checked against `MaxTextSize`. The declared encoding is always UTF-8 here (the pre-pass refuses any other),
  so the reader decodes what was encoded. An `XmlException` is mapped to a
  `mathml-syntax-error` at its position mapped back (or at the document type declaration the pre-pass recorded, when the
  reader refused it without a line, or at line 1, column 1 for a text with no markup at all, empty or blank, which the
  reader refuses without a line too), with the position suffix of its message removed and every position its message
  cites ("line 1 position 26") mapped back to the original text (`DescribeXmlError`), and a hint (for a document type:
  remove the declaration, because the W3C named references are built in; otherwise: close every element and escape a
  literal `&` or `<`).
- Start of an element - the first element must be `math` in the MathML namespace or in no namespace, else a located
  error with a hint. An element of another namespace, or any element inside a token, is charged to `MaxElements`,
  checked against `MaxNestingDepth`, reported as `mathml-element-unsupported`, and skipped with its subtree.
  `annotation` and `annotation-xml` are read as raw content (the text, or the inner markup) with their `encoding`,
  listed in `Annotations`, and never laid out or reported. Every other element is pushed, checked against
  `MaxNestingDepth` and `MaxElements` (the MathML elements and the other charged elements together), with its
  attributes read with their own locations: `href` and the `xlink` attributes are reported as
  `mathml-attribute-unsupported` ("not followed") and not kept; other attributes of other namespaces are ignored.
- Skipped and raw content (`WalkSubtree`) - the subtree of a skipped element and the content of an annotation are
  walked node by node, never consumed unseen by `XmlReader.Skip`, `ReadSubtree`, or `ReadInnerXml`: every element
  inside is charged to `MaxElements` and checked against `MaxNestingDepth` (its depth below the skipped or annotation
  element added to that element's depth), failing at its own location, so a single skipped element cannot hide more
  elements or deeper nesting than the limits allow. Nothing inside is reported, so the reports of skipped content stay
  one per skipped element. The text of skipped content is discarded (it is bounded by the character cap of the
  reader); the text of an annotation is kept, and the markup of an `annotation-xml` is rewritten node by node
  (`CopyNode`) exactly as `XmlWriter.WriteNode` writes it, so the kept markup is the one `ReadInnerXml` gives.
- Text - inside a token the raw text is accumulated (checked against `MaxTokenLength` on the raw text) and, at the end
  of the token, trimmed and collapsed (`CollapseWhitespace`: every run of XML whitespace becomes one space). Elsewhere,
  whitespace-only text is dropped and other text is kept as a `MathText` child (drawn like an `mtext`) and reported as
  `mathml-element-unsupported`, because text belongs in a token.
- End of an element - completes the `MathElement` with its attributes and children and adds it to its parent, or makes
  it the root.
- Reports - every pending report goes through `Report`, which keeps at most `MaxDiagnostics` of them (the limits of
  the open options) and replaces the first one beyond with a single `mathml-diagnostics-suppressed` warning, so that the
  reports a document keeps, and replays on every call, are bounded however many skipped elements or links the text
  holds. A render sink with the same limit turns that notice into its own, so the delivered reports are unchanged.

#### Error Handling

Every refusal is a `MathMlSyntaxException` located in the original text: `mathml-syntax-error` for malformed XML, a
document type, another root, an unknown reference or a foreign encoding (from the pre-pass), and an unpaired surrogate;
`mathml-limit-exceeded` for the element, nesting, and token-length limits (inside skipped content and annotations too),
naming the `MathMlLimits` property. Foreign
content, links, and stray text are not errors: they are kept as pending reports (bounded) for every measure and render
call. The walk is iterative, so deep nesting cannot overflow the stack.

#### Dependencies

The `CharacterReferences` and `MathTree` units, the core `Packaging` subsystem (`SafeXml`), and the root namespace's
`MathMlLimits`, `MathMlLocation`, `MathMlAnnotation`, `MathMlSyntaxException`, `MathMlDiagnosticCodes`, and
`MathMlPendingReport`.

#### Callers

`MathMlDocument` reads every text it parses or opens with it.

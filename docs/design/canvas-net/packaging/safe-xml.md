### SafeXml Unit Design

![Packaging Structure](PackagingView.svg)

<!-- cspell:ignore XXE -->

`SafeXml` is the second public software unit in the `Packaging` subsystem: a stateless static class
that loads XML from untrusted input with a hardened, bounded reader.

#### Purpose

Parsing untrusted XML with default settings can resolve external entities (reading local files or
making network requests: XML External Entity, XXE, injection), expand nested entities into
gigabytes ("billion laughs"), or read an unbounded document. Three places in CanvasNet parsed
untrusted XML with their own copy of the safe settings (the OPC package layers of `CanvasNetPptx`
and `CanvasNetVsdx`, and the two parses of `SvgCodec`). `SafeXml` is the one place those settings
live, so that they cannot drift apart or be lost by an incomplete edit.

#### Data Model

`SafeXml` has no instance state. Every reader it builds uses the same `XmlReaderSettings`:
`DtdProcessing = Prohibit`, `XmlResolver = null`, `MaxCharactersInDocument` = the caller's
`maxCharacters`, and `MaxCharactersFromEntities` = 1,024. (With DTD processing prohibited no entity
can be declared, so the last setting is defense in depth only.)

#### Key Methods

##### Load(stream, maxCharacters) and Load(xml, maxCharacters)

Create the hardened reader and return `XDocument.Load` of it (`LoadOptions.None`). The stream
overload leaves the stream open. A document that declares a DTD, is larger than `maxCharacters`
characters (the reader fails once it has consumed that many, before the tree is built), or is not
well-formed throws `XmlException`, which is left for the caller to translate into its own exception
type and wording (`OpcPackage` raises `OpcFormatException`, `SvgCodec` raises
`InvalidDataException`).

##### CreateReader(stream, maxCharacters)

Returns the same hardened reader for a caller that must stream rather than build a tree (the SVG
codec reads only a root start tag this way). The reader does not close the stream; the caller
disposes the reader.

#### Error Handling

Arguments are validated before any parsing: a null stream or string throws `ArgumentNullException`;
a stream that cannot be read throws `ArgumentException` (parameter `stream`); `maxCharacters` of
zero or less throws `ArgumentOutOfRangeException` (parameter `maxCharacters`), because zero would
mean "unlimited" to `XmlReaderSettings.MaxCharactersInDocument`. Everything that is wrong with the
XML itself is an `XmlException` raised while reading.

#### Dependencies

`SafeXml` depends only on the base class library's `System.Xml` and `System.Xml.Linq`.

#### Callers

- `OpcPackage` loads every XML part through it.
- `SvgCodec` (`Load` and `GetInfo`) loads and streams its document through it.

#### Thread Safety

The class is stateless and safe from any number of threads. The `XDocument` and `XmlReader`
instances it returns are not thread-safe.

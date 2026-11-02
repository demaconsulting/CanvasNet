### OpcPackage Unit Design

![Packaging Structure](PackagingView.svg)

<!-- cspell:ignore ECMA ordinally reparse navigations -->

`OpcPackage` is the first public software unit in the `Packaging` subsystem. It is a read-only,
size-bounded view of an Open Packaging Conventions (OPC, ECMA-376 Part 2) ZIP package, such as a
`.pptx` or `.vsdx` file. The unit includes the public `OpcFormatException` that it throws for a
malformed package.

#### Purpose

A reader of an OPC-based format needs the same generic services whatever the format: open the ZIP
container, learn each part's content type from `[Content_Types].xml`, and follow relationships
between parts. `OpcPackage` provides exactly those and nothing format specific, and it treats the
package as untrusted: the input, a part's decompressed size, and an XML part's length are all
bounded, an XML part is parsed with `SafeXml`, and a relationship target that would leave the
package is rejected. Before this unit, `PptxDocument` and `VsdxDocument` each carried a copy of this
code (the Pptx copy also bounded a binary part's size; the Vsdx copy did not); `OpcPackage` keeps
the stricter behavior of the two.

#### Data Model

- **Limits** - three public constants: `MaxPackageBytes` (268,435,456 bytes, the most `Open`
  buffers from the input stream), `MaxPartBytes` (the same value, the most `GetPartBytes` returns
  for one part's decompressed content) and `MaxXmlPartCharacters` (2,000,000, the most characters
  any XML part may hold).
- **Package state** - the non-writable `MemoryStream` over the buffered bytes and the read-only
  `ZipArchive` opened on it; a case-sensitive (ordinal) lookup from entry full name to entry; the
  `Default` extension-to-content-type map (case-insensitive extension) and the `Override`
  part-to-content-type map, parsed from `[Content_Types].xml` at open time; and a cache of each
  source part's parsed relationships, keyed by normalized part path (the empty string is the
  package root), filled on first use, except for the package-level relationships, which are parsed at
  open time.
- **`PackageRelationship`** (private record struct) - one parsed `<Relationship>`: its raw `Target`,
  its `Type` (never empty) and whether `TargetMode="External"` was declared.
- **`OpcFormatException`** - a `sealed` exception deriving from `Exception` (the BCL
  `InvalidDataException` is sealed and cannot be a base) with the three standard constructors.

#### Key Methods

##### Open(stream)

Validates `stream` (`ArgumentNullException`; `ArgumentException` with parameter name `stream` when
it cannot be read). Copies it into a `MemoryStream` in 80 KiB chunks, throwing `OpcFormatException`
the moment the running total exceeds `MaxPackageBytes`, so neither memory nor read time is
unbounded for a huge or non-terminating stream, and never closes the caller's stream. The package is
then opened over that buffer without a second copy. Opening reads `[Content_Types].xml` and
`_rels/.rels`: both must exist, be well-formed XML within `MaxXmlPartCharacters`, and have the OPC
`Types`/`Relationships` root element; every `<Default>` needs `Extension` and `ContentType`, every
`<Override>` needs `PartName` and `ContentType`, every `<Relationship>` needs `Id`, `Type` and
`Target`, and two relationships of one part must not share an `Id`. A ZIP entry name that occurs
twice is rejected. If opening fails part-way the buffer and archive are disposed before the
exception propagates.

##### Part names and content types

Part names are compared ordinally, so `doc/Page.xml` and `doc/page.xml` are two distinct parts and
`[content_types].xml` is never accepted as `[Content_Types].xml`; `NormalizePartPath` strips a
single leading `/` and every method taking a part path accepts it with or without that slash.
`ContainsPart` reports whether an entry of exactly that name exists. `ResolvePart` returns the
`Override` content type for the part if there is one, otherwise the `Default` for its extension, and
throws when the part does not exist or neither supplies a type.

##### Relationships

`ResolveRelationship(source, id)`, `GetRelationshipType(source, id)`,
`ResolveRelationshipByType(source, suffix)`, `TryResolveRelationshipByType(source, suffix)` and
`TryResolveRelationshipById(source, id)` read the source part's relationships (the `.rels` part
beside it: `a/b.xml` has `a/_rels/b.xml.rels`; the empty source is the package root with
`_rels/.rels`; a part with no `.rels` part has none). A target starting with `/` is resolved from
the package root, any other from the source part's directory, `.` segments are dropped and `..`
segments pop the previous segment; a `..` with nothing to pop throws (the target would escape the
package. An external relationship is never resolved: `ResolveRelationship` throws for it and the
by-type and by-id searches skip it. The by-type search returns a non-external relationship whose
`Type` ends with the suffix (ordinal, case-sensitive); when several match, which one is returned is
unspecified, so it is meant for navigations that are unique by type.

##### LoadPartXmlRoot(partPath) and GetPartBytes(partPath)

`LoadPartXmlRoot` loads the part with `SafeXml.Load(stream, MaxXmlPartCharacters)` and returns the
root element; an `XmlException` (not well-formed, a DTD, too long) becomes an `OpcFormatException`
naming the part. `GetPartBytes` copies the part into a new array in chunks, counting what it
actually reads rather than trusting the entry's declared length, and throws `OpcFormatException`
once the count exceeds `MaxPartBytes`, so a "zip bomb" part cannot allocate beyond the bound.

#### Error Handling

All conditions above surface as `OpcFormatException`, whose message names the part, relationship or
bound concerned. Null arguments throw `ArgumentNullException` with the parameter name; an
unreadable stream throws `ArgumentException`; any member of a disposed instance throws
`ObjectDisposedException`. A ZIP entry whose compressed data is corrupt surfaces as the
`InvalidDataException` raised by the .NET ZIP classes when the entry is read, which is not wrapped.
A caller that exposes its own exception type catches `OpcFormatException` and rethrows its own.

The unit deliberately does not limit the number of ZIP entries or check a compression ratio: the
package bound limits how large the central directory can be, and every part read is bounded by
size.

#### Dependencies

`OpcPackage` depends on `SafeXml` (within the `Packaging` subsystem) and on the base class library's
`System.IO.Compression` and `System.Xml.Linq`. It uses no other CanvasNet type.

#### Callers

- `PptxDocument` and `VsdxDocument` open their packages with `OpcPackage.Open`, delegate their
  package-layer members to it, and rethrow `OpcFormatException` as `InvalidDataException` with the
  same message.
- A library user may use it directly to read any OPC-based format.

#### Thread Safety

An instance is not thread-safe: it caches parsed relationship parts and reads from one shared ZIP
archive. Use one instance per thread or synchronize externally. `Open` and `NormalizePartPath` are
static and share no state.

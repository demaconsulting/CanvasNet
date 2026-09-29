# System Design

<!-- cspell:ignore renderable -->

This document provides the system-level design for CanvasNetPdf.

![CanvasNetPdf Structure](CanvasNetPdfView.svg)

<!-- cspell:ignore xref startxref -->

## Architecture

CanvasNetPdf is a .NET library providing PDF page-rendering support, distributed as its own
NuGet package (`DemaConsulting.CanvasNet.Pdf`, namespace `DemaConsulting.CanvasNet.Pdf`),
independent of, but depending on, the core `CanvasNet` system (its own separate package,
`DemaConsulting.CanvasNet`) — see the Dependencies section below. The system consists of a
single implemented unit:

- **PdfDocument** (namespace `DemaConsulting.CanvasNet.Pdf`, folder
  `src/DemaConsulting.CanvasNet.Pdf/`, flat — no further nesting): a hand-rolled PDF document
  parser and page-rendering codec, opening a PDF document once and reporting its page
  count/size/rotation, then rasterizing a requested page into a
  `DemaConsulting.CanvasNet.Canvas.Surface` pixel buffer. See _PdfDocument Unit Design_
  (`canvas-net-pdf/pdf-document.md`).

`CanvasNetPdf` is modeled as its own top-level software system (rather than as a further unit of
the `CanvasNet` system's `Codecs` subsystem) because it is distributed as its own
independently-versioned NuGet package — per `software-items.md`'s rule that a software package
contains exactly one software system, a system whose entire content ships in a distinct package
must itself be modeled as a distinct system, not folded into the package it depends on.
`CanvasNetPdf` contains exactly one unit, so no Subsystem tier is interposed between the system
and `PdfDocument`: inserting one here would not separate anything (there is nothing else in the
system to separate it from), mirroring `CanvasNetSvg`'s own single-unit precedent (see
_CanvasNetSvg System Design_, `docs/design/canvas-net-svg.md`).

**Phase 1 scope.** `PdfDocument` is being delivered incrementally. Phase 1 (this design)
implements document structure parsing only: cross-reference resolution (in all of PDF's common
forms), the trailer/catalog/page tree, and page count/size/rotation reporting. `Render` already
exposes its final call shape (a page index plus a caller-chosen output size) but, in Phase 1,
returns a correctly sized, fully transparent `Surface` rather than interpreting the page's
content stream — page content rendering (text, paths, images, color) is planned for later
phases and is explicitly out of scope here.

## External Interfaces

The system exposes the following public API to external consumers, all on the sealed
`PdfDocument` class:

- **PdfDocument.Open(Stream stream)** / **PdfDocument.Open(string path)**: Opens and parses a
  PDF document exactly once, from a stream or a file path, buffering the input fully in memory.
  `Open(Stream)` never takes ownership of (or disposes) the caller's stream. `Open(string)`
  opens, reads, and closes its own internal `FileStream` before returning. Throws
  `ArgumentNullException` for a null `stream`/`path`, `ArgumentException` for an
  empty/whitespace-only `path`, `InvalidDataException` for malformed/unresolvable document
  structure, and `Codecs.UnsupportedImageFeatureException` (feature `"pdf-encrypted"`) for an
  encrypted document.
- **PdfDocument.PageCount**: Reports the document's true resolved page count. Throws
  `ObjectDisposedException` once the document has been disposed.
- **PdfDocument.GetPageInfo(int pageIndex)**: Reports the given page's display
  (rotation-adjusted) width/height and normalized effective rotation as a `PdfPageInfo`. Throws
  `ArgumentOutOfRangeException` for `pageIndex < 0 || pageIndex >= PageCount`, and
  `ObjectDisposedException` once disposed.
- **PdfDocument.Render(int pageIndex, int width, int height)**: Returns a `Surface` of the
  caller-specified `width`x`height` for the given page. Phase 1: the returned `Surface` is
  always fully transparent (blank) — no content-stream interpretation is performed yet. Validates
  `pageIndex` the same way as `GetPageInfo`, propagates `Surface`'s own `width`/`height`
  validation unwrapped, and throws `ObjectDisposedException` once disposed.
- **PdfDocument.Dispose()**: Idempotent; releases the buffered/parsed document state. No other
  public member may be called afterward without throwing `ObjectDisposedException`.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| -------------------------------------- | ---------------- | ------------------------------------ | ------------------------------- |
| `PdfDocument.Open(...)` | Inbound/Outbound | Method call / `PdfDocument` return | Valid PDF stream or path |
| `PdfDocument.PageCount` | Outbound | Property read / `int` return | Not disposed |
| `PdfDocument.GetPageInfo(...)` | Inbound/Outbound | Method call / `PdfPageInfo` return | `0 <= pageIndex < PageCount`; not disposed |
| `PdfDocument.Render(...)` | Inbound/Outbound | Method call / `Surface` return | `0 <= pageIndex < PageCount`; `0 < width, height <= 8192`; not disposed |
<!-- markdownlint-enable MD013 -->

See _PdfDocument Unit Design_ (`canvas-net-pdf/pdf-document.md`) for the complete parsing
internals (tokenizer, object model, all three cross-reference forms, the linear-scan fallback,
and page-tree traversal/inheritance) and every method's full parameter and exception detail.

## Dependencies

`CanvasNetPdf` depends on the separate `CanvasNet` system (its own package,
`DemaConsulting.CanvasNet`, referenced via a project reference from
`src/DemaConsulting.CanvasNet.Pdf/DemaConsulting.CanvasNet.Pdf.csproj`), specifically:

- The `Canvas` subsystem's `Surface` unit — constructing the blank (Phase 1) or, in a later
  phase, content-rendered destination raster `Render` returns
- The `Codecs` subsystem's shared `UnsupportedImageFeatureException` type — reused, unmodified,
  to signal a well-formed-but-unsupported `/Encrypt`ed document (see Risk Control Measures below)

Phase 1 introduces no dependency on the `CanvasNet` system's `Geometry`, `Drawing`, or `Fonts`
subsystems: no Phase 1 file constructs a `Path`, rasterizes a fill/stroke, or looks up a glyph —
`Render` only constructs a blank `Surface`. A later phase that begins interpreting page content
streams will add those dependencies explicitly, at the point they are actually first used, rather
than declaring them here in advance of any real usage.

This is an ordinary, same-repository, system-to-system dependency: both `CanvasNet` and
`CanvasNetPdf` are produced by this repository, so it is neither an OTS Software Item (not a
third-party/external-program dependency) nor a Shared Package (that category is scoped to a
package produced by a _different_ repository within the same program). Beyond `CanvasNet`,
`PdfDocument` uses only the .NET base class library's `System.IO.Compression.DeflateStream`
(decompressing `FlateDecode`-filtered object streams), available on every one of CanvasNetPdf's
target frameworks with no new runtime NuGet dependency. `CanvasNetPdf` introduces no new OTS
Software Item beyond those already used to build and verify the `CanvasNet` system (BuildMark,
FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark, VersionMark, WeasyPrint, xUnit)
— see _OTS Integration Design_ (`docs/design/ots.md`).

## Risk Control Measures

`PdfDocument.Open` parses an externally-supplied, potentially untrusted PDF document (a binary
format whose cross-reference tables, object offsets, and page-tree references may be malformed,
inconsistent, or adversarially constructed). Risk control for this untrusted-input parsing is
segregated entirely within the single `PdfDocument` unit and falls into two distinct categories,
each with its own consistently-applied behavior:

- **Tolerated recovery** — a document whose normal cross-reference parsing fails, or resolves to
  a trailer that does not describe a valid `/Type /Catalog` root, falls back to a linear scan of
  the buffered document for `N G obj` markers to reconstruct an object-offset table directly,
  rather than immediately rejecting an otherwise-recoverable document.
- **Rejected reference cycles and malformed structure** — an unbounded page-tree reference cycle
  (a `/Kids` entry referencing one of its own ancestors) is explicitly detected and rejected with
  a thrown `InvalidDataException`, rather than being tolerated: recursing into a genuine cycle
  would otherwise never terminate. Any other unresolvable/malformed structure (an indirect object
  that cannot be located even after the linear-scan fallback, an invalid `/Rotate` value that is
  not a multiple of 90, and so on) is likewise rejected with `InvalidDataException`.

An `/Encrypt` key present in the trailer is detected explicitly and fails closed: `PdfDocument`
never attempts to interpret the (still-encrypted) bytes of an encrypted document as plaintext
content, instead throwing `Codecs.UnsupportedImageFeatureException` (feature `"pdf-encrypted"`)
immediately upon detection. No other segregation is required at the system level: `CanvasNetPdf`
contains exactly one unit, so this risk control is inherently contained within it (IEC 62304
§5.3.3).

## Data Flow

**PDF open/parse path:**

1. **Input**: A PDF document (stream or file path)
2. **Validation**: `Open` rejects a null `stream`/`path` with `ArgumentNullException` and an
   empty/whitespace `path` with `ArgumentException`
3. **Processing**: Fully buffers the input into memory, tokenizes and parses its cross-reference
   data (classic table, cross-reference stream, object stream, and hybrid combinations, with a
   linear-scan fallback), checks the resolved trailer for an `/Encrypt` key, then traverses the
   catalog's page tree (`/Pages` → recursive `/Kids` → `/Type /Page` leaves), inheriting
   `/MediaBox`/`/Rotate` from the nearest ancestor that declares one and computing each page's
   already-rotated `PdfPageInfo`
4. **Output**: A new `PdfDocument` instance whose `PageCount`/`GetPageInfo` reflect the fully
   resolved page tree — parsed once, reused across every subsequent call on that instance

**PDF render path (Phase 1):**

1. **Input**: A `pageIndex` and a caller-requested output `width`/`height`
2. **Validation**: Rejects an out-of-range `pageIndex` with `ArgumentOutOfRangeException`
   (identically to `GetPageInfo`); a non-positive `width`/`height`, or a `width`/`height`
   exceeding `Surface.MaxDimension` (8192), propagates, unwrapped, as `Surface`'s own
   `ArgumentOutOfRangeException`
3. **Processing**: Phase 1 performs no content-stream interpretation
4. **Output**: A new, fully transparent `Canvas.Surface` of exactly the requested size

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **One-time parse, reused across calls**: `Open` performs the full parse exactly once;
  `PageCount`/`GetPageInfo`/`Render` all read from that already-resolved state rather than
  re-parsing on every call — a design property a stateless (static-method-only) API could not
  express, and the direct motivation for `PdfDocument` being an instantiable, disposable class
  rather than a set of static methods like `SvgCodec`
- **Decode-only**: `PdfDocument` provides no encode/`Save` direction, matching every other
  decode-only codec in this repository

### Platform Support

The library targets the following frameworks, identical to the `CanvasNet` system it depends on,
enabling compatibility across modern, currently supported .NET runtimes:

| Target Framework | Runtime / Environment |
| ---------------- | --------------------- |
| `net8.0`         | .NET 8 LTS            |
| `net9.0`         | .NET 9                |
| `net10.0`        | .NET 10               |

The library is supported on the following operating systems:

- **Windows** — primary developer and CI platform
- **Linux** — CI/CD and containerized environments
- **macOS** — developer workstations using Apple platforms

Portability is achieved by restricting the implementation exclusively to Base Class Library (BCL)
APIs and the `CanvasNet` system's own public API, available across all target frameworks. No
platform-specific native interop, OS-specific APIs, or framework-version-specific features are
used.

### Integration Patterns

- **NuGet Packaging**: Standard .NET library packaging and distribution, referencing the
  `CanvasNet` package as an ordinary NuGet dependency
- **CI/CD Integration**: Automated build, test, and quality validation
- **Requirements Traceability**: All features linked to passing tests

# System Design

<!-- cspell:ignore Linq renderable -->

This document provides the system-level design for CanvasNetSvg.

![CanvasNetSvg Structure](CanvasNetSvgView.svg)

<!-- cspell:ignore rasterizing rrggbb -->

## Architecture

CanvasNetSvg is a .NET library providing SVG (Scalable Vector Graphics) rasterization,
distributed as its own NuGet package (`DemaConsulting.CanvasNet.Svg`, namespace
`DemaConsulting.CanvasNet.Svg`), independent of, but depending on, the core `CanvasNet` system
(its own separate package, `DemaConsulting.CanvasNet`) — see the Dependencies section below. The
system consists of a single implemented unit:

- **SvgCodec** (namespace `DemaConsulting.CanvasNet.Svg`, folder
  `src/DemaConsulting.CanvasNet.Svg/`, flat — no further nesting): a hand-rolled,
  decode/rasterize-only codec for a common real-world subset of SVG documents, rasterizing them
  directly into a `DemaConsulting.CanvasNet.Canvas.Surface` pixel buffer. Unlike the `CanvasNet`
  system's own raster codecs (`BmpCodec`, `PngCodec`, `TiffCodec`, `JpegCodec`), `SvgCodec` has no
  `Save` method: an SVG document is XML markup, not pixel data, so there is no meaningful inverse
  operation that would turn an arbitrary `Surface` back into equivalent vector markup. See
  _SvgCodec Unit Design_ (`canvas-net-svg/svg-codec.md`).

`CanvasNetSvg` is modeled as its own top-level software system (rather than as a further unit of
the `CanvasNet` system's `Codecs` subsystem, as it was in an earlier revision of this design)
because it is distributed as its own independently-versioned NuGet package — per
`software-items.md`'s rule that a software package contains exactly one software system, a system
whose entire content ships in a distinct package must itself be modeled as a distinct system, not
folded into the package it depends on. `CanvasNetSvg` contains exactly one unit, so no Subsystem
tier is interposed between the system and `SvgCodec`: inserting one here would not separate
anything (there is nothing else in the system to separate it from), unlike `CanvasNet`'s own
`Fonts` subsystem, whose single-unit shape exists to keep a consistent finer-grained
decomposition within a System that already contains several other subsystems.

## External Interfaces

The system exposes the following public API to external consumers, all on the static `SvgCodec`
class:

- **SvgCodec.Load(Stream stream, int width, int height, fonts)** /
  **SvgCodec.Load(string path, int width, int height, fonts)**: Loads a `Surface` by rasterizing
  a supported SVG document from a stream or file path into a caller-requested `width`x`height`
  raster, with an optional single-font-per-family `fonts` dictionary. Throws
  `ArgumentNullException` for a null `stream`/`path`, `ArgumentException` for an empty/whitespace
  `path`, `InvalidDataException` for malformed or unsupported SVG data, and
  `ArgumentOutOfRangeException` (propagated, unwrapped, from `Surface`'s own constructor) for a
  non-positive `width`/`height`.
- **SvgCodec.LoadWithFontFaces(Stream stream, int width, int height, fontFaces)** /
  **SvgCodec.LoadWithFontFaces(string path, int width, int height, fontFaces)**: The richer
  loading entry point, accepting more than one registered `SvgFontFace` (weight/style variant)
  per font-family, matching each text element's own cascaded font-weight/font-style to the
  closest registered face. Same exception contract as `Load` above.
- **SvgCodec.GetInfo(Stream stream)** / **SvgCodec.GetInfo(string path)**: Reports an SVG
  document's resolved intrinsic width, height, channel count (always 4), and alpha presence
  (always true) via the shared `Codecs.ImageInfo` record struct, given a stream or a file path,
  without rasterizing the document, falling back from `viewBox` to `width`/`height` to a
  300x150 CSS default. Throws `ArgumentNullException` for a null `stream`/`path`,
  `ArgumentException` for an empty/whitespace `path`, and `InvalidDataException` for malformed
  SVG data.

<!-- markdownlint-disable MD013 -->
| Interface | Direction | Format | Constraints |
| -------------------------------------- | ---------------- | ------------------------------ | ------------------------------- |
| `SvgCodec.Load(...)` | Inbound/Outbound | Method call / `Surface` return | Valid SVG stream or path; `width`/`height` > 0 |
| `SvgCodec.LoadWithFontFaces(...)` | Inbound/Outbound | Method call / `Surface` return | Valid SVG stream or path; `width`/`height` > 0 |
| `SvgCodec.GetInfo(...)` | Inbound/Outbound | Method call / `ImageInfo` return | Valid SVG stream or path |
<!-- markdownlint-enable MD013 -->

See _SvgCodec Unit Design_ (`canvas-net-svg/svg-codec.md`) for the complete in-scope SVG feature
subset (shapes, path data, transforms, gradients, patterns, filters, clip-path/mask, markers,
image, text, and CSS styling) and every method's full parameter and exception detail.

## Dependencies

`CanvasNetSvg` depends on the separate `CanvasNet` system (its own package,
`DemaConsulting.CanvasNet`, referenced via a project reference from
`src/DemaConsulting.CanvasNet.Svg/DemaConsulting.CanvasNet.Svg.csproj`), specifically:

- The `Canvas` subsystem's `Surface` unit — constructing the destination raster and compositing
  filled/stroked pixels into it
- The `Geometry` subsystem's `Path`/`PathBuilder` (assembling transformed shape/glyph outlines)
  and `SvgArcConverter` (converting elliptical arc path commands to Bezier curves)
- The `Drawing` subsystem's `PathFiller`/`PathStroker` (rasterizing the already-transformed,
  pixel-space paths `SvgCodec` builds) and `Gradient`/`LinearGradient`/`RadialGradient`/
  `GradientStop`/`GradientSpread` (representing resolved gradient paint)
- The `Fonts` subsystem's `TrueTypeFont` (glyph outline/metrics/kerning lookup for text
  rendering)
- The `Codecs` subsystem's shared `ImageInfo` record struct (the return type of `GetInfo`) — see
  _Codecs Subsystem Design_ (`docs/design/canvas-net/codecs.md`)

This is an ordinary, same-repository, system-to-system dependency: both `CanvasNet` and
`CanvasNetSvg` are produced by this repository, so it is neither an OTS Software Item (not a
third-party/external-program dependency) nor a Shared Package (that category is scoped to a
package produced by a _different_ repository within the same program). Beyond `CanvasNet`,
`SvgCodec` uses only the .NET base class library's `System.Xml.Linq` (`XDocument`/`XElement`) and
`System.Numerics` (`Matrix3x2`/`Vector2`) namespaces, available on every one of CanvasNetSvg's
target frameworks with no new runtime NuGet dependency. `CanvasNetSvg` introduces no new OTS
Software Item beyond those already used to build and verify the `CanvasNet` system (BuildMark,
FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark, VersionMark, WeasyPrint, xUnit)
— see _OTS Integration Design_ (`docs/design/ots.md`).

## Risk Control Measures

`SvgCodec`'s `Load`/`LoadWithFontFaces` operations parse externally-supplied, potentially
untrusted SVG documents (XML markup that may embed references, gradients, filters, and
base64-encoded raster images). Risk control for this untrusted-input parsing is segregated
entirely within the single `SvgCodec` unit — explicit DTD/external-entity hardening (rejecting
any document containing a DOCTYPE declaration), bounded element-nesting depth, total-element,
geometry-work, and number-list-length budgets, and tolerant-fallback (rather than
whole-document-rejecting) handling of dangling references, reference cycles, and
resource-disproportionate constructs — see _SvgCodec Unit Design_ (`canvas-net-svg/svg-codec.md`)
for the complete set of budgets and fallback behaviors. No other segregation is required at the
system level: `CanvasNetSvg` contains exactly one unit, so this risk control is inherently
contained within it (IEC 62304 §5.3.3).

## Data Flow

**SVG load/rasterize path:**

1. **Input**: An SVG document (stream or file path), the caller-requested output raster
   `width`/`height`, and an optional font dictionary
2. **Validation**: `Load`/`LoadWithFontFaces` reject a null `stream`/`path` with
   `ArgumentNullException`, an empty/whitespace `path` with `ArgumentException`, and malformed or
   resource-disproportionate SVG data (non-well-formed XML, a DOCTYPE declaration, a malformed
   `viewBox`/`transform`/path `d` attribute, or a budget exceeded) with `InvalidDataException`;
   a non-positive `width`/`height` propagates, unwrapped, as `Surface`'s own
   `ArgumentOutOfRangeException`
3. **Processing**: Parses the document with `System.Xml.Linq`, builds an id→element index,
   resolves the root `viewBox`/`width`/`height` into an intrinsic size and computes the
   `preserveAspectRatio`-driven fit transform into the requested raster, then recursively walks
   the element tree — baking every composed transform directly into `Geometry.Path` points,
   resolving presentation attributes/CSS cascade, gradients, patterns, clip-path/mask, and
   filters, and rasterizing each renderable element via `Drawing.PathFiller`/`Drawing.PathStroker`
   onto a new `Canvas.Surface`, with text elements shaped through `Fonts.TrueTypeFont` glyph
   outlines
4. **Output**: A new `Canvas.Surface` containing the rasterized pixels

**SVG GetInfo path:**

1. **Input**: An SVG document (stream or file path)
2. **Validation**: Rejects a null `stream`/`path` with `ArgumentNullException`, an empty/
   whitespace `path` with `ArgumentException`, and malformed SVG data (up to and including the
   root `svg` start-tag) with `InvalidDataException`
3. **Processing**: Reads only the root `svg` start-tag's own attributes via a forward-only,
   character-budget-capped `XmlReader`, resolving intrinsic size by falling back from `viewBox`
   to `width`/`height` to a 300x150 CSS default
4. **Output**: A `Codecs.ImageInfo` record struct reporting the resolved width, height,
   `Channels = 4`, and `HasAlpha = true`

## Design Constraints

- **Simplicity**: Minimal functionality kept easy to understand and extend
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full test coverage, complete documentation
- **Portability**: Compatible across supported .NET platforms
- **Decode-only**: `SvgCodec` provides no `Save`/encode direction (see Architecture above)

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

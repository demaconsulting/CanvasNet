## Packaging

![Packaging Structure](PackagingView.svg)

<!-- cspell:ignore ECMA -->

The `Packaging` subsystem is the seventh software subsystem in CanvasNet. It groups the hardened
building blocks for reading untrusted Open Packaging Conventions (OPC, ECMA-376 Part 2) packages -
the ZIP-based container behind `.pptx`, `.vsdx` and the other Office Open XML formats - and for
loading untrusted XML in general.

### Purpose

The package readers in other CanvasNet packages (`CanvasNetPptx`, `CanvasNetVsdx`) and the SVG
codec (`CanvasNetSvg`) all take attacker-controlled bytes and must open them without exhausting
memory, following a hostile relationship target out of the package, or resolving an external
entity. Before this subsystem each of them carried its own copy of that code. The subsystem keeps
it in one place, as public API, so that every reader gets the same bounds and so that a library
user who needs to read some other OPC-based format can reuse them.

### Units

- **OpcPackage** - a read-only, size-bounded view of an OPC ZIP package: bounded open from a
  stream, content-type resolution, relationship resolution, and part reads as XML or bytes. The
  unit includes the `OpcFormatException` it throws for a malformed package; see _OpcPackage Unit
  Design_ (`packaging/opc-package.md`).
- **SafeXml** - a stateless static class that loads XML with no DTD processing, no external
  resolution, and a character bound; see _SafeXml Unit Design_ (`packaging/safe-xml.md`).

### Dependencies

The `Packaging` subsystem depends only on the .NET base class library (`System.IO.Compression`,
`System.Xml`, `System.Xml.Linq`) and, within the subsystem, `OpcPackage` depends on `SafeXml`. It
depends on no other CanvasNet subsystem and adds no NuGet package.

### Callers

The subsystem is a public API entry point. Within this repository `PptxDocument` and `VsdxDocument`
(units of the `CanvasNetPptx` and `CanvasNetVsdx` systems) open their packages with `OpcPackage` and
rethrow its `OpcFormatException` as `InvalidDataException`; `SvgCodec` (the `CanvasNetSvg` system)
loads its document through `SafeXml`. The packages that call it are released in lockstep with the
core package, so they always see the same version of the bounds.

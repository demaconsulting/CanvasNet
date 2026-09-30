### SystemFontCatalog

<!-- cspell:ignore Dejavu Nimbus Consolas ttcf rescanned Segoe LOCALAPPDATA dogfood dogfooding -->
<!-- cspell:ignore Zapf -->

`SystemFontCatalog` is the second public software unit in the `Fonts` subsystem, added this
phase alongside the unchanged `TrueTypeFont`. It provides directory-scan-only discovery of fonts
installed on the host operating system, best-effort family-name/style matching against that
discovered catalog, and a bundled, always-available Liberation Sans/Serif/Mono last-resort
fallback font shipped as an embedded resource of this assembly.

#### Purpose

`SystemFontCatalog` lets a caller (today, `DemaConsulting.CanvasNet.Pdf`'s `PdfDocument`)
substitute a font it needs to render but does not have embedded font bytes for. It answers three
distinct questions: what fonts does this process consider "installed" (`Fonts`), which one of
those best matches a requested family name and bold/italic/serif/fixed-pitch style
(`FindBestMatch`), and, when nothing on the host machine matches at all, which bundled font
should be used instead (`LoadBundledFallback`). The unit is deliberately format-agnostic: it has
no knowledge of any particular document format's font-naming conventions (for example a PDF
subset tag or a Standard-14 name) - a caller is responsible for reducing a format-specific font
reference to a plain family-name hint and a set of style booleans before calling `FindBestMatch`.

#### Data Model

- **`SystemFontInfo`** - a `readonly record struct` recording one discovered font file's (or,
  for a `.ttc` collection, one discovered face's) identity and metadata: `FamilyName`,
  `SubfamilyName`, `Bold`, `Italic`, `FixedPitch` (all resolved via `TrueTypeFont.GetNameInfo()`
  and the `IsBold`/`IsItalic`/`IsFixedPitch` properties already exposed by the unchanged
  `TrueTypeFont` unit), plus `FilePath` and `FaceIndex` so the exact face can be reloaded later.
  `FamilyName` falls back to the font file's own name (without its extension) when the font has
  no usable `name` table, so every catalog entry always has a non-null, non-empty family name.
- **`Fonts`** - an `IReadOnlyList<SystemFontInfo>` backed by a `Lazy<IReadOnlyList<SystemFontInfo>>`
  built exactly once per process, on first access, using `LazyThreadSafetyMode.ExecutionAndPublication`.
  It is never rescanned afterward: installing or removing a font on the host machine while the
  process is running is not detected.
- **Well-known generic-family lists** - three hand-maintained, priority-ordered `string[]`
  constants (`GenericSansFamilies`, `GenericSerifFamilies`, `GenericMonospaceFamilies`), each
  listing common cross-platform family names (for example `Arial`, `Helvetica`, `Liberation
  Sans`, `DejaVu Sans`, `Nimbus Sans`, `Segoe UI` for the sans-serif bucket) most likely to
  actually be installed on a given operating system, in the order they are tried.
- **`BundledFontCache`** - a `Dictionary<string, TrueTypeFont>` (guarded by a plain
  `private static readonly object` lock, since the core package multi-targets `net8.0` and
  therefore cannot rely on the newer `System.Threading.Lock` type) caching every bundled Liberation
  style loaded so far by its bundled file name (for example `"LiberationSans-Bold.ttf"`) - at
  most 12 entries are ever created, each created at most once, for the process lifetime.

#### Key Methods

##### Fonts

A property returning `LazyFonts.Value`. Building the catalog scans every well-known font
directory for the current operating system (see Dependencies/Platform Scan Roots below),
recursively enumerating `.ttf`/`.ttc`/`.otf` candidate files (case-insensitive extension match)
and loading every face of every candidate with `TrueTypeFont.GetFaceCount`/`TrueTypeFont.Load`. A
missing or inaccessible directory, an unparseable candidate file, or a single corrupt face within
an otherwise well-formed `.ttc` is silently skipped (catching `InvalidDataException`,
`IOException`, and `UnauthorizedAccessException` at the narrowest possible scope) rather than
aborting the whole scan - an empty result is a valid, expected outcome (for example on a minimal
container image with no fonts installed at all), never an error.

##### FindBestMatch(familyNameHint, bold, italic, serif, fixedPitch)

Implements a two-tier scoring algorithm over `Fonts`:

1. **Tier 1 - exact family match.** Every candidate whose `FamilyName` case-insensitively equals
   `familyNameHint` is considered; among them, the one minimizing the count of mismatched
   bold/italic flags wins (a tie is broken by first-encountered order). If any exact-family
   candidate exists, its best style match is returned immediately - tier 2 is never consulted.
2. **Tier 2 - generic-bucket fallback.** Only when tier 1 finds nothing, the appropriate
   generic-family list is selected (`GenericMonospaceFamilies` when `fixedPitch` is
   `true` - taking priority over `serif` - otherwise `GenericSerifFamilies` when `serif` is
   `true`, otherwise `GenericSansFamilies`), and each name in that list is tried, in order, as an
   exact-family search of its own; the first name in the list that actually has at least one
   candidate in `Fonts` wins (again resolved to its own best style match).

Returns `null` when `Fonts` is empty, or genuinely has no exact family match and no
generic-bucket match either. `FindBestMatch` never itself consults `LoadBundledFallback` -
composing "system match, else bundled fallback" is left to the caller.

##### LoadBundledFallback(serif, fixedPitch, bold, italic)

Derives a bundled file name of the form `"{Family}-{Style}.ttf"` (`Family` one of
`LiberationSans`/`LiberationSerif`/`LiberationMono`, mirroring `FindBestMatch`'s own
`fixedPitch`-over-`serif` priority; `Style` one of `Regular`/`Bold`/`Italic`/`BoldItalic`), then
loads (or returns the process-lifetime-cached) `TrueTypeFont` for that resource via
`Assembly.GetManifestResourceStream("DemaConsulting.CanvasNet.Fonts.BundledFonts.{fileName}")`.
Never consults `Fonts` or the host operating system's installed fonts at all, so it is available
even when the host machine has no fonts installed whatsoever.

##### Internal Test-Support Entry Points

- **`FindBestMatchCore(candidates, ...)`** - the scoring algorithm above, exposed over an
  injectable candidate list so its tier-1/tier-2/no-match correctness can be tested
  deterministically, without depending on what happens to be installed on the machine running
  the tests.
- **`LoadBundledFallbackCore(bundledFileName)`** - the embedded-resource load/cache logic above,
  exposed by explicit bundled file name so a test can exercise the missing-resource defensive
  guard with a deliberately wrong name, and so a test can dogfood every one of the 12 real bundled
  files individually.
- **`BuildCatalogFromRoots(roots)`** - the directory-scan/tolerance logic above, exposed over an
  explicit, caller-supplied set of directory roots so the "missing root"/"unparseable file"
  tolerance behavior can be tested against controlled temporary directories, without depending on
  the real host machine's fonts. This entry point is a deliberate addition beyond this phase's
  original plan text (which specified only an internal scoring entry point), added because it is
  necessary to exercise `Fonts`'s own tolerance behavior deterministically.

All three internal entry points are visible to `DemaConsulting.CanvasNet.Tests` only, via the
existing `InternalsVisibleTo` attribute on the core project.

#### Error Handling

`SystemFontCatalog`'s public surface never throws for a normal "nothing matched" or "nothing
installed" outcome - `Fonts` returns an empty list, and `FindBestMatch` returns `null`. The one
exception this unit ever throws is `InvalidOperationException` from `LoadBundledFallback` (via
`LoadBundledFallbackCore`), and only when the expected embedded resource is unexpectedly absent -
a defensive, effectively-unreachable packaging-integrity guard under normal operation, since all
12 bundled files are guaranteed present at build time as embedded resources of this assembly.

#### Dependencies

`SystemFontCatalog` depends on `TrueTypeFont` (`GetFaceCount`, `Load(path[, faceIndex])`,
`GetNameInfo()`, `IsBold`, `IsItalic`, `IsFixedPitch`) within the same `Fonts` subsystem, and on
`System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform` to select this process's
well-known font-directory scan roots:

| Operating System | Scan Roots |
| --- | --- |
| Windows | `Environment.SpecialFolder.Fonts`; `%LOCALAPPDATA%\Microsoft\Windows\Fonts` (per-user), when set |
| macOS | `/System/Library/Fonts`; `/Library/Fonts`; `~/Library/Fonts`, when the user profile path is set |
| Linux (and other) | `/usr/share/fonts`; `/usr/local/share/fonts`; `~/.local/share/fonts`, `~/.fonts`, when set |

No Windows registry access is used anywhere in this unit; discovery is directory-scan-only on
every supported operating system, matching this repository's existing CI OS matrix.
`LoadBundledFallback` additionally depends on this assembly's own embedded `BundledFonts/*.ttf`
resources (see the `Fonts` subsystem's Purpose section and `BundledFonts/README.md` for their
sourcing/licensing provenance).

#### Callers

`SystemFontCatalog` is called by `DemaConsulting.CanvasNet.Pdf`'s `PdfDocument`
(`PdfDocument.FontFallback.cs`'s `ResolveFallbackFont`) when resolving a `/Subtype /TrueType`
simple font lacking an embedded `/FontFile2` stream; see _PdfDocument Unit Design_
(`../../canvas-net-pdf/pdf-document.md`). The unit's public API is intentionally
format-agnostic and generic, so a future non-PDF document-format consumer could call it directly
without any change to this unit.

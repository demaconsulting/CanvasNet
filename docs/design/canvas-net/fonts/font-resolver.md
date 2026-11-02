### FontResolver

<!-- cspell:ignore Liberation -->

`FontResolver` is a public software unit in the `Fonts` subsystem. It resolves a CSS
`font-family` list, a bold flag and an italic flag to a loaded font, deterministically and without
ever failing: the bundled Liberation Sans is the last resort. The unit includes the small
`FontResolverOptions` configuration record, the `ResolvedFont` result record and the `FontSource`
enumeration.

#### Purpose

Svg, Vega-Lite and OpenSCAD each carried a private implementation of "find the font for this
family list": per family, a generic-keyword mapping to the bundled fonts, a look in the caller's
additional font directories, a look at the installed fonts, and the bundled Sans as the last
resort. The implementations differed in details that mattered (the default for installed fonts,
whether `Arial` maps to the bundled Sans before the installed fonts are consulted, what a missing
font is reported as, how deep a directory is scanned and how it is bounded). `FontResolver` is the
one implementation; each consumer chooses its policy through `FontResolverOptions`, and keeps only
what is specific to it (its option types, its warning wording, its registered-font matching).

#### Data Model

- **`FontResolverOptions`** - a `sealed record` of `init`-only properties: `AdditionalDirectories`
  (`IReadOnlyList<string>`, empty by default), `UseSystemFonts` (`false` by default, so a
  resolution does not depend on the machine unless asked), `MapCommonAliasesToBundled` (`false`
  by default) and `ScanLimits` (a `FontScanLimits`, defaults of that record).
- **`FontSource`** - `Bundled`, `AdditionalDirectory`, `System`: where the face came from.
- **`ResolvedFont`** - a `sealed record` `(TrueTypeFont Font, string Family, FontSource Source,
  bool Bold, bool Italic)`. For a bundled face `Family` is `Liberation Sans`, `Liberation Serif`
  or `Liberation Mono` and `Bold`/`Italic` are the requested flags; otherwise they are what the
  scanned face reports.
- **`FontResolver`** holds a private copy of the options, the cancellation token, a supplier of
  the installed faces (`SystemFontCatalog.Fonts`; replaceable through an internal constructor so
  tests can inject faces), a `ConcurrentDictionary` of resolutions keyed by `(family list, bold,
  italic)` (each value the `ResolvedFont` and the failure text), the lock guarding the one-time
  scan, and the scanned faces of the additional directories (`null` until first needed).

#### Key Methods

##### Resolve(cssFamilyList, bold, italic, out failure)

Looks the request up in the cache, else resolves it:

1. Split the list with `GenericFontFamilies.SplitFamilyList`. For each family, in order:
2. `GenericFontFamilies.TryClassify(family, MapCommonAliasesToBundled, ...)`: a generic keyword
   (and, when the option is set, a common alias) resolves to the bundled Liberation font of the
   classified design, in the requested style.
3. Otherwise the family is looked up by name: first in the faces of the additional directories
   (scanned once, lazily, on the first lookup that needs them, with `FontDirectoryScanner.Scan`,
   `trustLinkedFiles: false`, the configured limits and the cancellation token; a canceled scan
   throws and is not kept), then - only when `UseSystemFonts` is set - in the installed faces,
   each with `SystemFontCatalog.FindInFamily` (exact family, ignoring case, closest style, first
   wins a tie) and a load that returns `null` for a vanished or damaged file (a face of an additional
   directory is loaded and cached by the resolver instance, a failed load included, so it is released
   with the resolver; an installed face goes through `SystemFontCatalog.Load`, the process-wide cache). A
   face that is found but cannot be loaded makes the family fail: the later sources are not tried
   for it, the reason (`the font file '<path>' could not be loaded`) is remembered, and the next
   family of the list is tried.
4. A family no directory or installed font has, that is one of the three bundled family names
   (`Liberation Sans`, `Liberation Serif`, `Liberation Mono`, ignoring case), resolves to that
   bundled font; any other such family fails with the reason `no such font family was found`.
5. After the last family, the bundled Liberation Sans in the requested style. `failure` is `null`
   when a family was met (or the list names no family) and otherwise the reason of the first
   family that failed.

The generic substitution of `SystemFontCatalog.FindBestMatch` is deliberately not used: it would
make the result depend on what the host has installed. Generic keywords always resolve to the bundled
fonts first (CSS reserves those names), so a family literally named `serif` or `monospace` in a
directory or among the installed fonts is never used.

#### Error Handling

`Resolve` never fails for a font that is missing, damaged or vanished. It throws
`OperationCanceledException` only when the token given to the constructor is canceled during the
one-time directory scan, and the constructor throws `ArgumentNullException` for options whose
`AdditionalDirectories` or `ScanLimits` is `null`. Scan problems (missing directory, unparseable
file) are skipped by `FontDirectoryScanner`.

#### Dependencies

`FontResolver` depends on `FontDirectoryScanner` (and its `FontScanLimits`), `SystemFontCatalog`
(`Fonts`, `FindInFamily`, `Load`, the internal `LoadUncached`, `LoadBundledFallback`), `SystemFontInfo`,
`TrueTypeFont` and
`GenericFontFamilies`, all within the `Fonts` subsystem, and on `System.Collections.Concurrent`.

#### Callers

- `SvgCodec` resolves the default font of a `text` element with a resolver per render
  (`UseSystemFonts` from `SvgLoadOptions`, default `true`; aliases not mapped).
- Vega-Lite's `TextMeasurer` resolves every font request with one (`MapCommonAliasesToBundled`
  `true`; `UseSystemFonts` default `false`).
- Scad's `ScadFontResolver` resolves the `font` argument of `text()` with one per build (aliases
  not mapped; its cancellation token and scan limits passed through).
- A library user may use it directly.

#### Thread Safety

Safe for concurrent use: the one-time scan runs under a lock (a published scan is read without
one), the cache is a `ConcurrentDictionary` (two threads resolving the same new request at once
may both resolve it, with the same result, because the fonts they load are shared: by the resolver's
own directory-face cache or by `SystemFontCatalog`), and the options are copied at construction. The
cache holds one entry per
distinct request, so a resolver is meant to live as long as one render or job, not as long as a
process that resolves untrusted family lists.

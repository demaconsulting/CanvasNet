// cspell:ignore Nonsymbolic Oblique Dingbats Zapf Noto
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     Maps every Standard-14 PDF font name to its fixed (Serif, FixedPitch, Bold, Italic)
    ///     style tuple, used by <see cref="ResolveFallbackFlavor"/> in preference to (and
    ///     regardless of) any <c>/FontDescriptor</c> flags/weight/angle a font dictionary may
    ///     also declare - a Standard-14 name's style is a fixed property of that name, not
    ///     something a producer's own (possibly absent, possibly wrong) descriptor should
    ///     override. <c>Symbol</c> and <c>ZapfDingbats</c> are deliberately excluded from this
    ///     table - they resolve via a dedicated bundled Noto substitute (see
    ///     <see cref="ResolveFallbackFont"/>'s <see cref="ResolveSymbolicNotoFallback"/> branch)
    ///     that bypasses serif/fixed-pitch/bold/italic flavor classification entirely, since this
    ///     table's style tuple has no meaning for a symbol/dingbat glyph set.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (bool Serif, bool FixedPitch, bool Bold, bool Italic)>
        Standard14Flavors = new Dictionary<string, (bool Serif, bool FixedPitch, bool Bold, bool Italic)>(StringComparer.Ordinal)
        {
            ["Helvetica"] = (false, false, false, false),
            ["Helvetica-Bold"] = (false, false, true, false),
            ["Helvetica-Oblique"] = (false, false, false, true),
            ["Helvetica-BoldOblique"] = (false, false, true, true),
            ["Times-Roman"] = (true, false, false, false),
            ["Times-Bold"] = (true, false, true, false),
            ["Times-Italic"] = (true, false, false, true),
            ["Times-BoldItalic"] = (true, false, true, true),
            ["Courier"] = (false, true, false, false),
            ["Courier-Bold"] = (false, true, true, false),
            ["Courier-Oblique"] = (false, true, false, true),
            ["Courier-BoldOblique"] = (false, true, true, true),
        };

    /// <summary>
    ///     Caches every <see cref="TrueTypeFont"/> loaded from a <see cref="SystemFontCatalog.FindBestMatch"/>
    ///     hit, keyed by the matched font's <c>(FilePath, FaceIndex)</c> - deliberately a
    ///     process-lifetime cache (unlike <see cref="_fontCache"/>'s per-<see cref="Render(int, int, int, PdfRenderOptions?)"/>
    ///     call scope), since a system font file's bytes never change between calls, so
    ///     re-parsing it from disk on every fallback lookup would reintroduce the "hundreds of
    ///     files scanned repeatedly" cost <see cref="SystemFontCatalog.Fonts"/>'s own laziness
    ///     already avoids at the discovery layer.
    /// </summary>
    private static readonly Dictionary<(string Path, int FaceIndex), TrueTypeFont> FallbackFontCache = new();

    /// <summary>Guards <see cref="FallbackFontCache"/> against concurrent population.</summary>
    private static readonly object FallbackFontCacheLock = new();

    /// <summary>
    ///     Resolves the ordered <see cref="Fonts.TrueTypeFont"/> candidate list to substitute for
    ///     a simple TrueType font with no embedded <c>/FontDescriptor/FontFile2</c>: a
    ///     <c>Symbol</c>/<c>ZapfDingbats</c> <c>/BaseFont</c> resolves via a dedicated bundled
    ///     Noto substitute union (<see cref="ResolveSymbolicNotoFallback"/>); any other font whose
    ///     <c>/FontDescriptor/Flags</c> declares <c>Symbolic</c> without also declaring
    ///     <c>Nonsymbolic</c> still fails closed, since such a font's symbol/dingbat glyph set has
    ///     no meaningful generic-family equivalent; otherwise classifies the font's serif/
    ///     fixed-pitch/bold/italic flavor (via the Standard-14 table, or else
    ///     <c>/FontDescriptor</c> flags/weight/angle/name heuristics), searches the host
    ///     operating system's installed fonts via <see cref="SystemFontCatalog.FindBestMatch"/>
    ///     (falling back to a style-matched bundled Liberation Sans/Serif/Mono font via
    ///     <see cref="SystemFontCatalog.LoadBundledFallback"/> when no system font matches), and
    ///     appends a second, always-present, plain (non-serif, non-fixed-pitch, non-bold,
    ///     non-italic) bundled Liberation Sans candidate - a per-character glyph-coverage
    ///     fallback, consulted by <see cref="ResolvedSimpleFont.Resolve"/>'s existing first-
    ///     non-zero-glyph-wins candidate search only when the primary match's own font actually
    ///     lacks a given codepoint, mirroring <c>PptxDocument.TextLayout.cs</c>'s own
    ///     per-character fallback for the parallel PPTX text-rendering path.
    /// </summary>
    /// <param name="baseFontName">The font dictionary's <c>/BaseFont</c> name.</param>
    /// <param name="descriptor">The font dictionary's resolved <c>/FontDescriptor</c>.</param>
    /// <returns>
    ///     The ordered, non-empty list of candidate substitute <see cref="Fonts.TrueTypeFont"/>
    ///     instances - every non-<c>Symbol</c>/<c>ZapfDingbats</c> case now returns a two-element
    ///     list (primary match, then the generic bundled Liberation Sans coverage fallback); see
    ///     <see cref="ResolveSymbolicNotoFallback"/> for the (three- or one-element)
    ///     <c>Symbol</c>/<c>ZapfDingbats</c> case.
    /// </returns>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="descriptor"/>'s <c>/Flags</c> declares <c>Symbolic</c>
    ///     without also declaring <c>Nonsymbolic</c> for a <c>/BaseFont</c> other than
    ///     <c>Symbol</c>/<c>ZapfDingbats</c> - such a font's symbol/dingbat glyph set has no
    ///     meaningful generic-family equivalent and is never substituted with an unrelated system
    ///     or bundled font. <c>Symbol</c>/<c>ZapfDingbats</c> itself no longer throws this - see
    ///     <see cref="ResolveSymbolicNotoFallback"/>.
    /// </exception>
    private IReadOnlyList<TrueTypeFont> ResolveFallbackFont(string baseFontName, PdfObject descriptor)
    {
        if (baseFontName is "Symbol" or "ZapfDingbats")
        {
            return ResolveSymbolicNotoFallback(baseFontName);
        }

        if (IsSymbolicWithoutNonsymbolic(descriptor))
        {
            throw new UnsupportedImageFeatureException(
                "pdf-font-symbolic-not-embedded",
                $"Font '/BaseFont /{baseFontName}' has no embedded /FontDescriptor/FontFile2; " +
                "a symbolic font (per /FontDescriptor/Flags) other than Symbol/ZapfDingbats is " +
                "never substituted with an unrelated system or bundled font, since its " +
                "symbol/dingbat glyph set has no meaningful generic-family equivalent.");
        }

        var (serif, fixedPitch, bold, italic) = ResolveFallbackFlavor(baseFontName, descriptor);
        var familyNameHint = StripFontNameDecoration(baseFontName);

        var match = SystemFontCatalog.FindBestMatch(familyNameHint, bold, italic, serif, fixedPitch);
        var primary = match is { } found
            ? LoadFallbackFontFromDisk(found.FilePath, found.FaceIndex)
            : SystemFontCatalog.LoadBundledFallback(serif, fixedPitch, bold, italic);

        // Per-character glyph-coverage fallback: the primary match above (a system font, or the
        // style-matched bundled Liberation substitute) is still just one font, picked purely from
        // the family/flavor classification above, before any character is inspected - it may lack
        // a specific codepoint a wider-coverage bundled font does cover. A plain
        // (serif: false, fixedPitch: false, bold: false, italic: false) bundled Liberation Sans is
        // appended as a second, always-present candidate; ResolvedSimpleFont.Resolve (see
        // PdfDocument.Fonts.cs) already implements the stateless, first-non-zero-glyph-wins search
        // across a candidate list this returns to - the same mechanism that already backs the
        // Symbol/ZapfDingbats branch above. Listing the primary match first preserves existing
        // behavior for every font whose primary match already has full coverage: the bundled
        // fallback is only ever consulted on an actual glyph-0 miss.
        var genericFallback = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);
        return [primary, genericFallback];
    }

    /// <summary>
    ///     The bundled Noto substitute font(s) (see <c>Fonts/BundledFonts/README.md</c> for
    ///     sourcing/licensing provenance), in a fixed glyph-lookup priority order, used in place
    ///     of the fail-closed policy every other symbolic font still gets: a <c>Symbol</c>
    ///     <c>/BaseFont</c> tries <c>NotoSans-Regular.ttf</c> (its Greek-letter and general-symbol
    ///     glyphs) first, then <c>NotoSansMath-Regular.ttf</c> (mathematical operators), then
    ///     <c>NotoSansSymbols2-Regular.ttf</c> (Private-Use-Area-adjacent/rare symbols); a
    ///     <c>ZapfDingbats</c> <c>/BaseFont</c> uses only <c>NotoSansSymbols2-Regular.ttf</c>. See
    ///     <see cref="ResolvedSimpleFont.Resolve"/> for how a multi-font list is searched (first
    ///     non-zero <see cref="Fonts.TrueTypeFont.GetGlyphIndex"/> wins).
    /// </summary>
    /// <param name="baseFontName">Either <c>"Symbol"</c> or <c>"ZapfDingbats"</c>.</param>
    /// <returns>The ordered Noto substitute font list for <paramref name="baseFontName"/>.</returns>
    private static IReadOnlyList<TrueTypeFont> ResolveSymbolicNotoFallback(string baseFontName)
    {
        return baseFontName == "Symbol"
            ?
            [
                SystemFontCatalog.LoadBundledFallbackCore("NotoSans-Regular.ttf"),
                SystemFontCatalog.LoadBundledFallbackCore("NotoSansMath-Regular.ttf"),
                SystemFontCatalog.LoadBundledFallbackCore("NotoSansSymbols2-Regular.ttf"),
            ]
            : [SystemFontCatalog.LoadBundledFallbackCore("NotoSansSymbols2-Regular.ttf")];
    }

    /// <summary>
    ///     Loads (or returns the process-lifetime cached) <see cref="TrueTypeFont"/> for a
    ///     specific system font file and face, as matched by <see cref="SystemFontCatalog.FindBestMatch"/>.
    /// </summary>
    private static TrueTypeFont LoadFallbackFontFromDisk(string path, int faceIndex)
    {
        lock (FallbackFontCacheLock)
        {
            var key = (path, faceIndex);
            if (FallbackFontCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var font = TrueTypeFont.Load(path, faceIndex);
            FallbackFontCache[key] = font;
            return font;
        }
    }

    /// <summary>
    ///     Determines whether a font dictionary's <c>/FontDescriptor/Flags</c> declares the
    ///     <c>Symbolic</c> bit (bit 3) without also declaring the <c>Nonsymbolic</c> bit (bit 6) -
    ///     the PDF specification's own "symbolic, non-Latin-text glyph set" declaration. Absence
    ///     of both bits (a common real-world producer omission) is treated leniently as
    ///     <c>Nonsymbolic</c>, not as symbolic.
    /// </summary>
    private bool IsSymbolicWithoutNonsymbolic(PdfObject descriptor)
    {
        var flags = GetFontDescriptorFlags(descriptor);
        if (flags is not { } flagsValue)
        {
            return false;
        }

        const int symbolicBit = 1 << 2;
        const int nonsymbolicBit = 1 << 5;
        return (flagsValue & symbolicBit) != 0 && (flagsValue & nonsymbolicBit) == 0;
    }

    /// <summary>
    ///     Classifies a font's serif/fixed-pitch/bold/italic flavor: a Standard-14 name's fixed
    ///     table entry (see <see cref="Standard14Flavors"/>) takes priority and bypasses
    ///     <paramref name="descriptor"/> entirely when it matches; otherwise the flavor is derived
    ///     from <c>/FontDescriptor/Flags</c> bit 1 (<c>FixedPitch</c>) and bit 2 (<c>Serif</c>),
    ///     <c>/FontWeight</c> &gt;= 600 (else a case-insensitive <c>"Bold"</c> substring in
    ///     <paramref name="baseFontName"/>) for bold, and <c>/FontDescriptor/Flags</c> bit 7
    ///     (<c>Italic</c>) OR a non-zero <c>/ItalicAngle</c> OR a case-insensitive <c>"Italic"</c>/
    ///     <c>"Oblique"</c> substring in <paramref name="baseFontName"/> for italic.
    /// </summary>
    private (bool Serif, bool FixedPitch, bool Bold, bool Italic) ResolveFallbackFlavor(
        string baseFontName,
        PdfObject descriptor)
    {
        if (Standard14Flavors.TryGetValue(baseFontName, out var standardFlavor))
        {
            return standardFlavor;
        }

        var flags = GetFontDescriptorFlags(descriptor) ?? 0;
        const int fixedPitchBit = 1 << 0;
        const int serifBit = 1 << 1;
        const int italicBit = 1 << 6;

        var fixedPitch = (flags & fixedPitchBit) != 0;
        var serif = (flags & serifBit) != 0;

        var fontWeight = GetFontWeight(descriptor);
        var bold = fontWeight is { } weight
            ? weight >= 600
            : baseFontName.Contains("Bold", StringComparison.OrdinalIgnoreCase);

        var italicAngle = GetItalicAngle(descriptor);
        var italic = (flags & italicBit) != 0 ||
            (italicAngle is { } angle && angle != 0) ||
            baseFontName.Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
            baseFontName.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

        return (serif, fixedPitch, bold, italic);
    }

    /// <summary>
    ///     Style-name suffixes (checked longest-first, case-insensitive) stripped from a
    ///     <c>/BaseFont</c> name (after subset-tag removal) by <see cref="StripFontNameDecoration"/>,
    ///     leaving just the plain family name - for example <c>"Arial,BoldItalic"</c> or
    ///     <c>"Arial-BoldItalic"</c> both become <c>"Arial"</c>.
    /// </summary>
    private static readonly string[] FontNameStyleSuffixes =
    [
        ",BoldItalic", "-BoldItalic", ",BoldOblique", "-BoldOblique",
        ",Bold", "-Bold", ",Italic", "-Italic", ",Oblique", "-Oblique",
    ];

    /// <summary>
    ///     Strips a PDF subset tag (six uppercase letters followed by <c>+</c>) and any trailing
    ///     style-name suffix (<see cref="FontNameStyleSuffixes"/>) from a <c>/BaseFont</c> name,
    ///     leaving a plain family-name hint suitable for <see cref="SystemFontCatalog.FindBestMatch"/>
    ///     - <see cref="SystemFontCatalog"/> itself has no knowledge of PDF-specific naming
    ///     conventions.
    /// </summary>
    private static string StripFontNameDecoration(string baseFontName)
    {
        var name = baseFontName;
        if (name.Length >= 7 && name[6] == '+' && name[..6].All(char.IsAsciiLetterUpper))
        {
            name = name[7..];
        }

        var matchedSuffix = FontNameStyleSuffixes.FirstOrDefault(
            suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        if (matchedSuffix is not null)
        {
            name = name[..^matchedSuffix.Length];
        }

        return name;
    }

    /// <summary>Reads a font descriptor's <c>/Flags</c> entry, if present and numeric.</summary>
    private int? GetFontDescriptorFlags(PdfObject descriptor)
    {
        var entry = descriptor.Get("Flags");
        return entry is not null && Resolve(entry) is { Kind: PdfKind.Number } value ? (int)value.Number : null;
    }

    /// <summary>Reads a font descriptor's <c>/FontWeight</c> entry, if present and numeric.</summary>
    private double? GetFontWeight(PdfObject descriptor)
    {
        var entry = descriptor.Get("FontWeight");
        return entry is not null && Resolve(entry) is { Kind: PdfKind.Number } value ? value.Number : null;
    }

    /// <summary>Reads a font descriptor's <c>/ItalicAngle</c> entry, if present and numeric.</summary>
    private double? GetItalicAngle(PdfObject descriptor)
    {
        var entry = descriptor.Get("ItalicAngle");
        return entry is not null && Resolve(entry) is { Kind: PdfKind.Number } value ? value.Number : null;
    }
}

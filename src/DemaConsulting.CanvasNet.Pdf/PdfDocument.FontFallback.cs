// cspell:ignore Nonsymbolic Oblique Dingbats Zapf
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
    ///     table - see <see cref="ResolveFallbackFont"/>'s fail-closed check, applied before this
    ///     table is ever consulted.
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
    ///     process-lifetime cache (unlike <see cref="_fontCache"/>'s per-<see cref="Render(int, int, int)"/>
    ///     call scope), since a system font file's bytes never change between calls, so
    ///     re-parsing it from disk on every fallback lookup would reintroduce the "hundreds of
    ///     files scanned repeatedly" cost <see cref="SystemFontCatalog.Fonts"/>'s own laziness
    ///     already avoids at the discovery layer.
    /// </summary>
    private static readonly Dictionary<(string Path, int FaceIndex), TrueTypeFont> FallbackFontCache = new();

    /// <summary>Guards <see cref="FallbackFontCache"/> against concurrent population.</summary>
    private static readonly object FallbackFontCacheLock = new();

    /// <summary>
    ///     Resolves a <see cref="Fonts.TrueTypeFont"/> to substitute for a simple TrueType font
    ///     with no embedded <c>/FontDescriptor/FontFile2</c>: fails closed for <c>Symbol</c>/
    ///     <c>ZapfDingbats</c> (and any font whose <c>/FontDescriptor/Flags</c> declares
    ///     <c>Symbolic</c> without also declaring <c>Nonsymbolic</c>); otherwise classifies the
    ///     font's serif/fixed-pitch/bold/italic flavor (via the Standard-14 table, or else
    ///     <c>/FontDescriptor</c> flags/weight/angle/name heuristics), searches the host
    ///     operating system's installed fonts via <see cref="SystemFontCatalog.FindBestMatch"/>,
    ///     and falls back to a bundled Liberation Sans/Serif/Mono font
    ///     (<see cref="SystemFontCatalog.LoadBundledFallback"/>) when no system font matches.
    /// </summary>
    /// <param name="baseFontName">The font dictionary's <c>/BaseFont</c> name.</param>
    /// <param name="descriptor">The font dictionary's resolved <c>/FontDescriptor</c>.</param>
    /// <returns>The resolved substitute <see cref="Fonts.TrueTypeFont"/>.</returns>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="baseFontName"/> is <c>Symbol</c> or <c>ZapfDingbats</c>, or
    ///     when <paramref name="descriptor"/>'s <c>/Flags</c> declares <c>Symbolic</c> without also
    ///     declaring <c>Nonsymbolic</c> - such fonts' symbol/dingbat glyph sets have no meaningful
    ///     generic-family equivalent and are never substituted with an unrelated system or bundled
    ///     font.
    /// </exception>
    private TrueTypeFont ResolveFallbackFont(string baseFontName, PdfObject descriptor)
    {
        if (baseFontName is "Symbol" or "ZapfDingbats" || IsSymbolicWithoutNonsymbolic(descriptor))
        {
            throw new UnsupportedImageFeatureException(
                "pdf-font-symbolic-not-embedded",
                $"Font '/BaseFont /{baseFontName}' has no embedded /FontDescriptor/FontFile2; " +
                "Symbol/ZapfDingbats and other symbolic fonts are never substituted with an " +
                "unrelated system or bundled font, since their symbol/dingbat glyph sets have " +
                "no meaningful generic-family equivalent.");
        }

        var (serif, fixedPitch, bold, italic) = ResolveFallbackFlavor(baseFontName, descriptor);
        var familyNameHint = StripFontNameDecoration(baseFontName);

        var match = SystemFontCatalog.FindBestMatch(familyNameHint, bold, italic, serif, fixedPitch);
        if (match is { } found)
        {
            return LoadFallbackFontFromDisk(found.FilePath, found.FaceIndex);
        }

        return SystemFontCatalog.LoadBundledFallback(serif, fixedPitch, bold, italic);
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

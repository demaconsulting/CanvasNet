// cspell:ignore WinAnsi WinAnsiEncoding MacRoman MacRomanEncoding notdef Zcaron Ycaron
// cspell:ignore zcaron ycaron dieresis dbase perthousand Lslash lslash guilsingl
// cspell:ignore periodcentered onesuperior twosuperior threesuperior ordfeminine ordmasculine
// cspell:ignore adieresis odieresis udieresis ecircumflex icircumflex ocircumflex ucircumflex
// cspell:ignore ntilde ccedilla eacute egrave igrave ograve ugrave agrave
// cspell:ignore codepoints Aacute Acircumflex Aring Atilde Edieresis Iacute Idieresis
// cspell:ignore Oacute Oslash Otilde Scaron Uacute Yacute Ydieresis aacute acircumflex
// cspell:ignore approxequal aring asciicircum asciitilde atilde braceleft braceright
// cspell:ignore bracketleft bracketright brokenbar caron daggerdbl dotaccent dotlessi
// cspell:ignore edieresis emdash endash exclam exclamdown germandbls greaterequal
// cspell:ignore guillemotleft guillemotright guilsinglleft guilsinglright hungarumlaut
// cspell:ignore iacute idieresis lessequal logicalnot nbspace notequal numbersign oacute
// cspell:ignore ogonek onehalf onequarter oslash otilde parenleft parenright partialdiff
// cspell:ignore plusminus questiondown quotedbl quotedblbase quotedblleft quotedblright
// cspell:ignore quoteleft quoteright quotesinglbase quotesingle scaron threequarters
// cspell:ignore uacute yacute ydieresis Zapf Nonsymbolic fontfile stdenc
// cspell:ignore ZapfDingbats Dingbats registerserif registersans copyrightserif copyrightsans
// cspell:ignore trademarkserif trademarksans radicalex Noto thinspace
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     A font resolved from a <c>/Resources/Font</c> entry, abstracting over the two
    ///     concrete resolution strategies this class supports: <see cref="ResolvedSimpleFont"/>
    ///     (a single-byte-code <c>/Subtype /TrueType</c> font) and <see cref="ResolvedCompositeFont"/>
    ///     (a two-byte-code <c>/Subtype /Type0</c>/<c>/Encoding /Identity-H</c> composite font, see
    ///     <c>PdfDocument.Fonts.Type0.cs</c>), and (as of this phase) <c>ResolvedType3Font</c> (a
    ///     single-byte-code <c>/Subtype /Type3</c> procedure-painted font, see
    ///     <c>PdfDocument.Fonts.Type3.cs</c>). <see cref="ShowText"/> decodes each shown string
    ///     into a sequence of codes using <see cref="CodeByteWidth"/> bytes per code, then resolves
    ///     each code's glyph index and advance width via <see cref="Resolve"/> - the same entry
    ///     point regardless of which concrete implementation is active, so <see cref="ShowGlyph"/>
    ///     never needs to know which one produced the font currently selected via <c>Tf</c>
    ///     (beyond its own early <c>ResolvedType3Font</c> pattern-match branch - see
    ///     <see cref="ShowGlyph"/>'s own remarks).
    /// </summary>
    private interface IResolvedFont
    {
        /// <summary>
        ///     Gets the number of bytes <see cref="ShowText"/> decodes per character code: <c>1</c>
        ///     for a simple or Type 3 font, <c>2</c> for a composite <c>/Identity-H</c> font.
        /// </summary>
        int CodeByteWidth { get; }

        /// <summary>
        ///     Resolves a single decoded character code to the loaded <see cref="Fonts.TrueTypeFont"/>
        ///     whose outline <see cref="ShowGlyph"/> paints (<see langword="null"/> for a
        ///     <c>ResolvedType3Font</c>, since a Type 3 font has no outline-glyph font program at
        ///     all - its glyphs are content-stream procedures, see
        ///     <c>PdfDocument.Fonts.Type3.cs</c>'s <c>PaintType3Glyph</c> - so this component is
        ///     never consulted for it), the resolved glyph index within that font (meaningless/
        ///     unused for a <c>ResolvedType3Font</c> - see that type's own remarks), and the
        ///     code's advance width in text-space units, already converted from the font's own
        ///     glyph-space convention (1/1000 em for a simple/composite font, via
        ///     <c>/FontMatrix</c> for a Type 3 font - see <c>ResolvedType3Font.Resolve</c>'s own
        ///     remarks), not yet scaled by <see cref="GraphicsState.FontSize"/>. For a
        ///     <see cref="ResolvedSimpleFont"/> whose <see cref="ResolvedSimpleFont.Fonts"/> holds
        ///     more than one candidate font (the <c>Symbol</c>/<c>ZapfDingbats</c> Noto-
        ///     substitute union - see <c>PdfDocument.FontFallback.cs</c>'s
        ///     <c>ResolveSymbolicNotoFallback</c>), the returned font is whichever list entry's
        ///     <see cref="Fonts.TrueTypeFont.GetGlyphIndex"/> first resolves the code's mapped
        ///     codepoint to a non-zero glyph index, so glyph-outline/advance-width/
        ///     <see cref="Fonts.TrueTypeFont.UnitsPerEm"/> lookups always stay bound to the same
        ///     font instance that produced the returned glyph index.
        /// </summary>
        /// <param name="code">The decoded character code (a byte for a simple or Type 3 font, a 16-bit big-endian value for a composite font).</param>
        /// <returns>The resolved font (or <see langword="null"/> for a Type 3 font), glyph index, and text-space advance width.</returns>
        (TrueTypeFont? Font, int GlyphIndex, double Width) Resolve(int code);
    }

    /// <summary>
    ///     A simple (single-byte-code) TrueType font, fully resolved from its <c>/Resources/Font</c>
    ///     dictionary: the loaded <see cref="Fonts.TrueTypeFont"/>, its effective code-to-Unicode-
    ///     codepoint encoding table, and its code-to-declared-advance-width table.
    /// </summary>
    /// <remarks>
    ///     Instances are built once by <see cref="BuildResolvedSimpleFont"/> and cached by
    ///     <see cref="ResolveFont"/> in <see cref="_fontCache"/> for the lifetime of a single
    ///     <see cref="Render(int, int, int, PdfRenderOptions?)"/> call - see <see cref="_fontCache"/>'s own remarks
    ///     for why the cache is never shared across calls.
    /// </remarks>
    private sealed class ResolvedSimpleFont : IResolvedFont
    {
        /// <summary>
        ///     Gets the ordered, non-empty list of candidate loaded TrueType fonts backing this
        ///     resolved font: every existing resolution path (embedded <c>/FontFile2</c>/
        ///     <c>/FontFile</c>/<c>/FontFile3</c>, a Standard-14/system-match, or a bundled-
        ///     Liberation fallback) produces exactly one element here, resolving identically to
        ///     this type's pre-union single-<c>Font</c> behavior; only the bundled Noto
        ///     <c>Symbol</c>/<c>ZapfDingbats</c> substitute path (see
        ///     <c>PdfDocument.FontFallback.cs</c>'s <c>ResolveSymbolicNotoFallback</c>) produces
        ///     more than one, in a fixed glyph-lookup priority order - see <see cref="Resolve"/>.
        /// </summary>
        internal required IReadOnlyList<TrueTypeFont> Fonts { get; init; }

        /// <summary>A simple font always decodes exactly one byte per character code.</summary>
        int IResolvedFont.CodeByteWidth => 1;

        /// <summary>
        ///     Gets the effective code (0-255) to Unicode codepoint map, after applying the
        ///     font's <c>/Encoding</c> base encoding and any <c>/Differences</c> overrides. A
        ///     code absent from this map has no mapped codepoint (the PDF specification's
        ///     "undefined" slot in the base encoding table, never overridden by
        ///     <c>/Differences</c>) - <see cref="ShowText"/> treats such a code as mapping to
        ///     Unicode codepoint <c>0</c>, which almost always resolves to glyph <c>0</c>
        ///     (<c>.notdef</c>) via <see cref="TrueTypeFont.GetGlyphIndex"/>.
        /// </summary>
        internal required IReadOnlyDictionary<int, int> Encoding { get; init; }

        /// <summary>
        ///     Gets the code (0-255) to declared advance width map, in glyph-space units per
        ///     1000 (text-space thousandths), from the font dictionary's <c>/FirstChar</c>/
        ///     <c>/LastChar</c>/<c>/Widths</c> entries. A code absent from this map has no
        ///     explicit declared width - see <see cref="MissingWidth"/> and
        ///     <see cref="ShowText"/>'s remarks for the documented fallback priority.
        /// </summary>
        internal required IReadOnlyDictionary<int, double> Widths { get; init; }

        /// <summary>
        ///     Gets the font's <c>/FontDescriptor/MissingWidth</c> value (in the same
        ///     per-1000 units as <see cref="Widths"/>), or <c>0</c> when not declared - the PDF
        ///     specification's own documented default.
        /// </summary>
        internal required double MissingWidth { get; init; }

        /// <summary>
        ///     Resolves <paramref name="code"/> to a font and glyph index via <see cref="Encoding"/>/
        ///     <see cref="TrueTypeFont.GetGlyphIndex"/>, and its advance width with the
        ///     documented priority: an explicit <see cref="Widths"/> entry first, else
        ///     <see cref="MissingWidth"/> (when nonzero), else the winning font's own
        ///     <see cref="TrueTypeFont.GetAdvanceWidth"/>/<see cref="TrueTypeFont.UnitsPerEm"/>
        ///     metric. When <see cref="Fonts"/> holds more than one candidate (the <c>Symbol</c>/
        ///     <c>ZapfDingbats</c> Noto-substitute union), each is tried in list order and the
        ///     first one whose <see cref="TrueTypeFont.GetGlyphIndex"/> returns a non-zero
        ///     glyph index wins; when none cover the codepoint, the first font's glyph <c>0</c>
        ///     (<c>.notdef</c>) is returned instead, exactly matching a single-font resolution's
        ///     own "no match" behavior.
        /// </summary>
        public (TrueTypeFont? Font, int GlyphIndex, double Width) Resolve(int code)
        {
            var codepoint = Encoding.TryGetValue(code, out var mapped) ? mapped : 0;

            var winningFont = Fonts[0];
            var glyphIndex = 0;
            foreach (var candidate in Fonts)
            {
                var candidateGlyphIndex = candidate.GetGlyphIndex(codepoint);
                if (candidateGlyphIndex != 0)
                {
                    winningFont = candidate;
                    glyphIndex = candidateGlyphIndex;
                    break;
                }
            }

            if (Widths.TryGetValue(code, out var declaredWidth))
            {
                return (winningFont, glyphIndex, declaredWidth / 1000.0);
            }

            if (MissingWidth != 0)
            {
                return (winningFont, glyphIndex, MissingWidth / 1000.0);
            }

            return (winningFont, glyphIndex, winningFont.GetAdvanceWidth(glyphIndex) / (double)winningFont.UnitsPerEm);
        }
    }

    /// <summary>
    ///     Caches every font dictionary resolved (via <see cref="ResolveFont"/>) by the <c>Tf</c>
    ///     operator during the content stream currently being executed by
    ///     <see cref="ExecuteContentStream"/>, keyed by the resolved (indirect-reference-followed)
    ///     font dictionary <see cref="PdfObject"/>'s own reference identity - <see cref="PdfObject"/>
    ///     never overrides <see cref="object.Equals(object?)"/>/<see cref="object.GetHashCode"/>,
    ///     so this is exactly the same "same instance in, same instance out" identity
    ///     <see cref="GetObject(int)"/>'s own <c>_objectCache</c> already guarantees for every
    ///     indirect reference resolved more than once - a directly-inlined (non-indirect-reference)
    ///     font dictionary is keyed just as safely, since <see cref="ParseValue(PdfTokenizer)"/>
    ///     never re-parses the same source bytes into two different <see cref="PdfObject"/>
    ///     instances within one document parse.
    /// </summary>
    /// <remarks>
    ///     Reinitialized (cleared) at the start of every <see cref="ExecuteContentStream"/> call,
    ///     alongside <see cref="_gsStack"/>/<see cref="_pathBuilder"/> - an explicit, documented
    ///     "per-<see cref="Render(int, int, int, PdfRenderOptions?)"/>-call only" cache scope, never shared or reused
    ///     across separate <see cref="Render(int, int, int, PdfRenderOptions?)"/> calls on the same
    ///     <see cref="PdfDocument"/> instance (each of which may, in principle, execute a
    ///     different page's content stream against the same font resource name, so caching a
    ///     resolved font beyond one call's lifetime could serve a stale/wrong font).
    /// </remarks>
    private Dictionary<PdfObject, IResolvedFont> _fontCache = null!;

    /// <summary>
    ///     Handles the <c>Tf</c> operator's font-name lookup: resolves a named font (simple or
    ///     composite) from the current page's <c>/Resources/Font</c> dictionary, building and
    ///     caching (in <see cref="_fontCache"/>) an <see cref="IResolvedFont"/> the first time
    ///     this particular font dictionary object is resolved during the current content-stream
    ///     execution.
    /// </summary>
    /// <param name="name">The font resource name (without the leading slash).</param>
    /// <returns>The resolved font.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="name"/> is not declared in the current page's
    ///     <c>/Resources/Font</c> dictionary (or no <c>/Resources</c> exists at all).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Propagated from <see cref="BuildResolvedFont"/> for an unsupported <c>/Subtype</c>, a
    ///     non-embedded <c>Symbol</c>/<c>ZapfDingbats</c>/symbolic font, an unrecognized
    ///     <c>/Encoding</c> base encoding, or an unsupported composite-font <c>/Encoding</c>/
    ///     descendant <c>/Subtype</c> (see <c>PdfDocument.Fonts.Type0.cs</c>).
    /// </exception>
    private IResolvedFont ResolveFont(string name)
    {
        var fontsEntry = _resources?.Get("Font");
        var fontsDictionary = fontsEntry is null ? null : Resolve(fontsEntry);
        var entry = fontsDictionary?.Get(name);
        if (entry is null)
        {
            throw new InvalidDataException(
                $"Undefined font '/{name}' (not declared in the current page's /Resources/Font).");
        }

        var fontDict = Resolve(entry);
        if (_fontCache.TryGetValue(fontDict, out var cached))
        {
            return cached;
        }

        var resolved = BuildResolvedFont(fontDict);
        _fontCache[fontDict] = resolved;
        return resolved;
    }

    /// <summary>
    ///     Builds an <see cref="IResolvedFont"/> from a font dictionary, dispatching on
    ///     <c>/Subtype</c>: <c>/TrueType</c> and <c>/Type1</c> both resolve a simple font via
    ///     <see cref="BuildResolvedSimpleFont"/> (see that method's own remarks for how each
    ///     subtype's embedded-font-program key differs); <c>/Type0</c> resolves a composite
    ///     <c>/Encoding /Identity-H</c>/<c>/CIDFontType2</c> font via
    ///     <see cref="BuildResolvedCompositeFont"/> (see <c>PdfDocument.Fonts.Type0.cs</c>);
    ///     <c>/Type3</c> resolves a procedure-painted font via
    ///     <see cref="BuildResolvedType3Font"/> (see <c>PdfDocument.Fonts.Type3.cs</c>); any other
    ///     <c>/Subtype</c> (for example <c>MMType1</c>) fails closed.
    /// </summary>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/Subtype</c> is not <c>TrueType</c>, <c>Type1</c>, <c>Type0</c>, or
    ///     <c>Type3</c> (for example <c>MMType1</c>).
    /// </exception>
    private IResolvedFont BuildResolvedFont(PdfObject fontDict)
    {
        var subtype = GetNameValue(fontDict, "Subtype");
        return subtype switch
        {
            "TrueType" or "Type1" => BuildResolvedSimpleFont(fontDict, subtype),
            "Type0" => BuildResolvedCompositeFont(fontDict),
            "Type3" => BuildResolvedType3Font(fontDict),
            _ => throw new UnsupportedImageFeatureException(
                $"pdf-font-subtype-{subtype ?? "missing"}",
                $"Font /Subtype '{subtype ?? "(missing)"}' is not supported; only simple " +
                "/Subtype /TrueType and /Subtype /Type1 fonts, composite /Subtype /Type0 " +
                "fonts, and procedure-painted /Subtype /Type3 fonts are supported (MMType1 " +
                "fonts are not supported)."),
        };
    }

    /// <summary>
    ///     Builds a <see cref="ResolvedSimpleFont"/> from a <c>/Subtype /TrueType</c> or
    ///     <c>/Subtype /Type1</c> font dictionary: loads the embedded
    ///     <c>/FontDescriptor/FontFile2</c> (a TrueType-outline program) if present, or else the
    ///     embedded <c>/FontDescriptor/FontFile</c> (a classic PostScript Type 1 program, via
    ///     <see cref="LoadType1Font"/> - see <c>PdfDocument.Fonts.Type1.cs</c>) if present, or else
    ///     the embedded <c>/FontDescriptor/FontFile3</c> (a bare Type1C/CFF program, via
    ///     <see cref="LoadType1CFont"/> - see <c>PdfDocument.Fonts.Type1.cs</c>) if present - each
    ///     always takes priority over any later-checked key or substitute - or else resolves a
    ///     substitute font via <see cref="ResolveFallbackFont"/> (a Standard-14/system/bundled-
    ///     Liberation match), and resolves <c>/Encoding</c> and <c>/Widths</c>.
    /// </summary>
    /// <param name="fontDict">The font dictionary being resolved.</param>
    /// <param name="subtype">
    ///     The font dictionary's own <c>/Subtype</c> name (<c>"TrueType"</c> or <c>"Type1"</c>),
    ///     consulted only to decide whether a <c>/FontFile3</c>-only descriptor resolves via
    ///     <see cref="LoadType1CFont"/> (a <c>/Type1</c> font's embedded Type1C/CFF program) or
    ///     falls back exactly as before <c>/Type1</c> dispatch existed (an unrelated, pre-existing
    ///     <c>/TrueType</c> font with a stray <c>/FontFile3</c>).
    /// </param>
    /// <remarks>
    ///     A simple font with no <c>/FontDescriptor</c> at all (permitted by PDF 32000-1
    ///     §9.6.2.2 for the standard 14 fonts, and leniently accepted here regardless of
    ///     <c>/BaseFont</c>) or with a <c>/FontDescriptor</c> but no embedded
    ///     <c>/FontFile2</c>/<c>/FontFile</c>/<c>/FontFile3</c> no longer fails closed
    ///     unconditionally: <see cref="ResolveFallbackFont"/> substitutes the closest-matching
    ///     system font, or - when no system font matches - a bundled Liberation Sans/Serif/Mono
    ///     font, fully automatically and silently (no new public API, no "fallback occurred"
    ///     diagnostics). A <c>/BaseFont</c> of <c>Symbol</c> or <c>ZapfDingbats</c> resolves
    ///     instead via a bundled Noto substitute font union (see
    ///     <c>PdfDocument.FontFallback.cs</c>'s <c>ResolveSymbolicNotoFallback</c>), and that
    ///     branch's <see cref="ResolvedSimpleFont.Encoding"/> also defaults to
    ///     <see cref="SymbolEncodingTable"/>/<see cref="ZapfDingbatsEncodingTable"/> (ISO 32000-1
    ///     Appendix D's own built-in encoding for that font) in place of the usual
    ///     <see cref="WinAnsiEncodingTable"/> default - an explicit <c>/Encoding</c> dictionary's
    ///     <c>/Differences</c> still applies on top, exactly as for any other simple font. Any
    ///     other symbolic font (one whose <c>/FontDescriptor/Flags</c> declares <c>Symbolic</c>
    ///     without also declaring <c>Nonsymbolic</c>) with no embedded font program still fails
    ///     closed, since such a font's glyph set has no meaningful generic-family equivalent - see
    ///     the <see cref="PdfDocument"/> class remarks and <c>PdfDocument.FontFallback.cs</c>. A
    ///     <c>/Subtype /Type1</c> descriptor's <c>/FontFile3</c> is only ever attempted via
    ///     <see cref="LoadType1CFont"/> - which itself fails closed with
    ///     <see cref="UnsupportedImageFeatureException"/> for any <c>/Subtype</c> other than
    ///     <c>Type1C</c> on that stream (see that method's own remarks) - gated on
    ///     <c>subtype == "Type1"</c> specifically, so a pre-existing <c>/Subtype /TrueType</c>
    ///     font with only a stray <c>/FontFile3</c> is unaffected and continues to resolve via
    ///     fallback exactly as before this phase.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontDescriptor/FontFile2</c> does not resolve to a stream,
    ///     <c>/BaseFont</c> is missing (only consulted on the fallback path), or propagated from
    ///     <see cref="LoadType1Font"/>/<see cref="LoadType1CFont"/> for a malformed embedded Type 1
    ///     or Type1C program.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when a symbolic font other than <c>Symbol</c>/<c>ZapfDingbats</c> has no
    ///     embedded font program (see <see cref="ResolveFallbackFont"/>), or propagated from
    ///     <see cref="LoadType1CFont"/> when a <c>/Subtype /Type1</c> descriptor's
    ///     <c>/FontFile3</c> stream declares a <c>/Subtype</c> other than <c>Type1C</c>.
    /// </exception>
    private ResolvedSimpleFont BuildResolvedSimpleFont(PdfObject fontDict, string? subtype)
    {
        // Per PDF 32000-1 §9.6.2.2, /FontDescriptor is optional for the standard 14 fonts (and,
        // leniently here, for any other non-embedded font too - consistent with this method's
        // existing "substitute automatically, regardless of name" fallback policy): a missing
        // /FontDescriptor is treated as "no descriptor, no embedded font program", not an error.
        var descriptorEntry = fontDict.Get("FontDescriptor");
        var descriptor = descriptorEntry is null ? PdfObject.NullValue : Resolve(descriptorEntry);


        var fontFile2Entry = descriptor.Get("FontFile2");
        var fontFileEntry = descriptor.Get("FontFile");
        IReadOnlyList<TrueTypeFont> fonts;
        int[]? defaultBaseTable = null;

        // Captured before loading any embedded font program (not after - see this method's own
        // remarks) so a /FontFile or /FontFile3 program's synthetic cmap-equivalent lookup (built
        // by LoadType1Font/LoadType1CFont) is keyed by this font dictionary's own /Differences
        // glyph-name vocabulary, not only the generic Adobe-glyph-name guess - see
        // BuildEmbeddedFontGlyphNameMap's own remarks for why the literal declared name wins.
        var embeddedFontGlyphNames = BuildEmbeddedFontGlyphNameMap(fontDict.Get("Encoding"));

        if (fontFile2Entry is not null)
        {
            var fontFileStream = Resolve(fontFile2Entry);
            if (fontFileStream.Kind != PdfKind.Stream)
            {
                throw new InvalidDataException("/FontDescriptor/FontFile2 does not resolve to a stream.");
            }

            var fontBytes = GetStreamDecodedBytes(fontFileStream);
            fonts = [TrueTypeFont.Load(new MemoryStream(fontBytes))];
        }
        else if (fontFileEntry is not null)
        {
            fonts = [LoadType1Font(descriptor, embeddedFontGlyphNames)];
        }
        else if (subtype == "Type1" && descriptor.Get("FontFile3") is not null)
        {
            fonts = [LoadType1CFont(descriptor, embeddedFontGlyphNames)];
        }
        else
        {
            var baseFontName = GetNameValue(fontDict, "BaseFont")
                ?? throw new InvalidDataException("Font dictionary is missing required /BaseFont.");
            fonts = ResolveFallbackFont(baseFontName, descriptor);
            defaultBaseTable = baseFontName switch
            {
                "Symbol" => SymbolEncodingTable,
                "ZapfDingbats" => ZapfDingbatsEncodingTable,
                _ => null,
            };
        }

        var encoding = ResolveEncoding(fontDict.Get("Encoding"), defaultBaseTable);
        var (widths, missingWidth) = ResolveWidths(fontDict, descriptor);

        return new ResolvedSimpleFont
        {
            Fonts = fonts,
            Encoding = encoding,
            Widths = widths,
            MissingWidth = missingWidth,
        };
    }

    /// <summary>
    ///     Resolves a font dictionary's <c>/Encoding</c> entry into a full 256-entry code-to-
    ///     Unicode-codepoint map, applying the named base encoding (defaulting to
    ///     <paramref name="defaultBaseTable"/>, or <c>/WinAnsiEncoding</c> when
    ///     <paramref name="defaultBaseTable"/> is <see langword="null"/>, when <c>/Encoding</c> is
    ///     absent) and any <c>/Differences</c> overrides.
    /// </summary>
    /// <param name="encodingEntry">
    ///     The font dictionary's <c>/Encoding</c> entry (a name, a dictionary, or
    ///     <see langword="null"/> when absent).
    /// </param>
    /// <param name="defaultBaseTable">
    ///     The 256-entry code-to-Unicode-codepoint table to seed the result from when
    ///     <paramref name="encodingEntry"/> supplies no base encoding name of its own, or
    ///     <see langword="null"/> (the default for every font other than a bundled-Noto-
    ///     substituted <c>Symbol</c>/<c>ZapfDingbats</c>) to fall back to
    ///     <see cref="WinAnsiEncodingTable"/> - see <see cref="SymbolEncodingTable"/>/
    ///     <see cref="ZapfDingbatsEncodingTable"/> and <see cref="BuildResolvedSimpleFont"/>'s own
    ///     remarks for the one case that passes a non-null value. An explicit <c>/Encoding</c>
    ///     dictionary's <c>/BaseEncoding</c> name or <c>/Differences</c> array still applies on
    ///     top of this default exactly as before, regardless of which table seeds it.
    /// </param>
    /// <returns>A code-to-codepoint map covering every code with a defined mapping.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Encoding</c> is neither a name nor a dictionary, when
    ///     <c>/Encoding/BaseEncoding</c> is not a name, when <c>/Encoding/Differences</c> is not
    ///     an array, when a <c>/Differences</c> array entry is neither a number nor a name, or
    ///     when a <c>/Differences</c> name appears before any starting code. A <c>/Differences</c>
    ///     name that is not a recognized glyph name (see <see cref="StandardGlyphNames"/>'s own
    ///     remarks for the covered name set) is tolerated, not an error - see
    ///     <see cref="ApplyDifferences"/>'s own remarks.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the named base encoding is neither <c>/WinAnsiEncoding</c>,
    ///     <c>/MacRomanEncoding</c>, nor <c>/StandardEncoding</c>.
    /// </exception>
    private IReadOnlyDictionary<int, int> ResolveEncoding(PdfObject? encodingEntry, int[]? defaultBaseTable = null)
    {
        var table = (int[])(defaultBaseTable ?? WinAnsiEncodingTable).Clone();
        PdfObject? differences = null;

        if (encodingEntry is not null)
        {
            var resolved = Resolve(encodingEntry);
            switch (resolved.Kind)
            {
                case PdfKind.Name:
                    ApplyBaseEncoding(resolved.Text, table);
                    break;

                case PdfKind.Dictionary:
                    var baseEncodingEntry = resolved.Get("BaseEncoding");
                    if (baseEncodingEntry is not null)
                    {
                        var baseEncoding = Resolve(baseEncodingEntry);
                        if (baseEncoding.Kind != PdfKind.Name)
                        {
                            throw new InvalidDataException("/Encoding/BaseEncoding must be a name.");
                        }

                        ApplyBaseEncoding(baseEncoding.Text, table);
                    }

                    differences = resolved.Get("Differences");
                    break;

                default:
                    throw new InvalidDataException("/Encoding must be a name or a dictionary.");
            }
        }

        if (differences is not null)
        {
            ApplyDifferences(Resolve(differences), table);
        }

        var map = new Dictionary<int, int>(256);
        for (var code = 0; code < 256; code++)
        {
            if (table[code] != 0)
            {
                map[code] = table[code];
            }
        }

        return map;
    }

    /// <summary>Applies a named base encoding's 256-entry code-to-codepoint table onto <paramref name="table"/>.</summary>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="baseEncodingName"/> is none of <c>WinAnsiEncoding</c>,
    ///     <c>MacRomanEncoding</c>, or <c>StandardEncoding</c>.
    /// </exception>
    private static void ApplyBaseEncoding(string baseEncodingName, int[] table)
    {
        switch (baseEncodingName)
        {
            case "WinAnsiEncoding":
                Array.Copy(WinAnsiEncodingTable, table, 256);
                break;

            case "MacRomanEncoding":
                Array.Copy(MacRomanEncodingTable, table, 256);
                break;

            case "StandardEncoding":
                Array.Copy(StandardEncodingTable, table, 256);
                break;

            default:
                throw new UnsupportedImageFeatureException(
                    $"pdf-font-encoding-{baseEncodingName}",
                    $"/Encoding base encoding '/{baseEncodingName}' is not supported; only " +
                    "/WinAnsiEncoding, /MacRomanEncoding, and /StandardEncoding are supported.");
        }
    }

    /// <summary>
    ///     Applies a <c>/Differences</c> array's code/glyph-name pairs onto <paramref name="table"/>,
    ///     per the PDF specification's own alternating "a starting code, then zero or more names
    ///     assigned to consecutive codes from that starting code" grammar - a thin wrapper over
    ///     <see cref="ParseDifferences"/> that additionally resolves each glyph name to a Unicode
    ///     codepoint via <see cref="TryResolveGlyphNameToCodepoint"/> (a simple/composite font's
    ///     own <c>/Differences</c> entries name Adobe-Glyph-List glyphs, unlike a Type 3 font's
    ///     own <c>/CharProcs</c>-keyed glyph names - see <c>ResolveType3Encoding</c> in
    ///     <c>PdfDocument.Fonts.Type3.cs</c>, which reuses <see cref="ParseDifferences"/> directly
    ///     without this codepoint-resolution step).
    /// </summary>
    /// <remarks>
    ///     A glyph name <see cref="TryResolveGlyphNameToCodepoint"/> cannot resolve (for example a
    ///     producer-specific name such as <c>/gXX</c>) is tolerated: that one code is simply left
    ///     at whatever the base encoding already assigned it, rather than rejecting the entire
    ///     document - a single unrecognized override name is not evidence the document is
    ///     corrupt, and real-world PDF producers routinely emit such names.
    ///     <see cref="ShowText"/>'s own "no glyph for this codepoint" fallback (see this class's
    ///     remarks) still applies to that code if the base encoding also leaves it undefined.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="differences"/> is not an array, when an array entry is
    ///     neither a number nor a name, or when a name entry appears before any starting code (or
    ///     after the code has advanced past 255).
    /// </exception>
    private void ApplyDifferences(PdfObject differences, int[] table) =>
        ParseDifferences(differences, (code, name) =>
        {
            if (TryResolveGlyphNameToCodepoint(name, out var codepoint))
            {
                table[code] = codepoint;
            }
        });

    /// <summary>
    ///     Resolves a <c>/Differences</c> glyph name to its Adobe-Glyph-List Unicode codepoint:
    ///     first consults <see cref="StandardGlyphNames"/> (the common-name subset), then - for a
    ///     name that subset does not itself cover - falls back to the AGL's own generic
    ///     <c>uniXXXX</c>/<c>uXXXX</c> hex-codepoint naming convention (four uppercase hex digits
    ///     after <c>uni</c>, or four-to-six uppercase hex digits after <c>u</c>), then finally to
    ///     the AGL's own underscore ligature-naming convention (see
    ///     <see cref="TryResolveLigatureUnderscoreName"/>), per the published Adobe Glyph List
    ///     specification. Introduced so a <c>/Differences</c> array naming a glyph this way (for
    ///     example <c>/uni03BC</c>, seen in real-world subsetted fonts whose own CFF charset uses
    ///     that exact literal spelling) resolves to the correct codepoint instead of being
    ///     silently tolerated as unrecognized (see <see cref="ApplyDifferences"/>'s own remarks
    ///     for that tolerant fallback).
    /// </summary>
    /// <param name="name">The <c>/Differences</c> array's glyph name to resolve.</param>
    /// <param name="codepoint">The resolved Unicode codepoint, or <c>0</c> when unresolved.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> resolved to a codepoint.</returns>
    private static bool TryResolveGlyphNameToCodepoint(string name, out int codepoint) =>
        StandardGlyphNames.TryGetValue(name, out codepoint) ||
        TryParseAdobeGlyphListHexName(name, out codepoint) ||
        TryResolveLigatureUnderscoreName(name, out codepoint);

    /// <summary>
    ///     Resolves an Adobe-Glyph-List "ligature" glyph name - two or more component glyph names
    ///     joined by underscores (for example <c>f_i</c>, a common real-world subsetted-font
    ///     spelling of the "fi" ligature glyph, as distinct from the AGL's own dedicated <c>fi</c>
    ///     name already covered by <see cref="StandardGlyphNames"/>) - per the published AGL
    ///     specification's own ligature-naming convention: each underscore-separated component is
    ///     itself resolved to a Unicode codepoint (recursively, via
    ///     <see cref="TryResolveGlyphNameToCodepoint"/>, so a component may itself use the
    ///     <c>uniXXXX</c>/<c>uXXXX</c> hex convention, for example <c>uni0066_uni0069</c>), the
    ///     resolved characters are concatenated in order, and the resulting string is looked up as
    ///     an AGL glyph name in its own right - for example <c>f_i</c> concatenates to <c>fi</c>,
    ///     which <see cref="StandardGlyphNames"/> already maps to the dedicated "LATIN SMALL
    ///     LIGATURE FI" codepoint <c>U+FB01</c>. Introduced because real-world PDF producers (for
    ///     example XeTeX/LuaTeX and several PostScript-derived toolchains) routinely name a
    ///     subsetted font's ligature glyphs this way rather than with the AGL's own dedicated
    ///     ligature names, leaving a <c>/Differences</c> array entry naming such a glyph otherwise
    ///     unresolved (and its text rendered as missing/tofu glyphs) even though
    ///     <see cref="StandardGlyphNames"/> already has a perfectly good codepoint for the exact
    ///     same ligature under its dedicated name.
    /// </summary>
    /// <param name="name">The glyph name to resolve.</param>
    /// <param name="codepoint">The resolved Unicode codepoint, or <c>0</c> when unresolved.</param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="name"/> has at least two non-empty
    ///     underscore-separated components, every component itself resolves to a codepoint, and
    ///     the components' concatenated characters match a name in <see cref="StandardGlyphNames"/>.
    /// </returns>
    private static bool TryResolveLigatureUnderscoreName(string name, out int codepoint)
    {
        codepoint = 0;

        var components = name.Split('_');
        if (components.Length < 2)
        {
            return false;
        }

        var concatenated = new StringBuilder();
        foreach (var component in components)
        {
            if (component.Length == 0 || !TryResolveGlyphNameToCodepoint(component, out var componentCodepoint))
            {
                return false;
            }

            concatenated.Append(char.ConvertFromUtf32(componentCodepoint));
        }

        return StandardGlyphNames.TryGetValue(concatenated.ToString(), out codepoint);
    }

    /// <summary>
    ///     Parses the Adobe Glyph List's generic hex-codepoint glyph-name conventions: <c>uniXXXX</c>
    ///     (exactly four uppercase hex digits) or <c>uXXXX</c>/<c>uXXXXX</c>/<c>uXXXXXX</c> (four
    ///     to six uppercase hex digits) - both name a single Unicode codepoint directly from the
    ///     glyph name's own digits, rather than through a lookup table. Lowercase hex digits are
    ///     deliberately rejected (not merely tolerated case-insensitively): the published AGL
    ///     specification requires uppercase digits for both conventions, and a producer emitting
    ///     lowercase digits is using a different, non-AGL naming convention this method has no
    ///     basis to guess the meaning of.
    /// </summary>
    /// <param name="name">The glyph name to parse.</param>
    /// <param name="codepoint">The parsed Unicode codepoint, or <c>0</c> when unparsed.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> matched one of these conventions.</returns>
    private static bool TryParseAdobeGlyphListHexName(string name, out int codepoint)
    {
        if (name.Length == 7 && name.StartsWith("uni", StringComparison.Ordinal) &&
            TryParseUppercaseHexDigits(name.AsSpan(3), out codepoint))
        {
            return true;
        }

        if (name.Length is >= 5 and <= 7 && name[0] == 'u' &&
            TryParseUppercaseHexDigits(name.AsSpan(1), out codepoint))
        {
            return true;
        }

        codepoint = 0;
        return false;
    }

    /// <summary>
    ///     Parses <paramref name="digits"/> as a Unicode codepoint, requiring every character to
    ///     be an uppercase hex digit (<c>0-9</c>/<c>A-F</c>) - shared by
    ///     <see cref="TryParseAdobeGlyphListHexName"/>'s two naming conventions so both reject a
    ///     lowercase (or otherwise non-hex) digit identically. Also rejects a syntactically valid
    ///     hex value above <c>0x10FFFF</c> (the highest valid Unicode codepoint): the four-to-six
    ///     digit <c>uXXXX</c>/.../<c>uXXXXXX</c> convention can spell a six-digit value as large as
    ///     <c>0xFFFFFF</c>, which is not a valid AGL codepoint name at all, so such a name must
    ///     stay unresolved (per <see cref="ApplyDifferences"/>'s own tolerant handling for
    ///     unrecognized names) rather than being treated as resolved.
    /// </summary>
    /// <param name="digits">The candidate hex digit span to parse.</param>
    /// <param name="value">
    ///     The parsed value, or <c>0</c> when <paramref name="digits"/> contains any
    ///     non-uppercase-hex character, or when the parsed value exceeds <c>0x10FFFF</c>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if every character in <paramref name="digits"/> was an
    ///     uppercase hex digit and the parsed value is a valid Unicode codepoint (at most
    ///     <c>0x10FFFF</c>).
    /// </returns>
    private static bool TryParseUppercaseHexDigits(ReadOnlySpan<char> digits, out int value)
    {
        foreach (var c in digits)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'A' and <= 'F')))
            {
                value = 0;
                return false;
            }
        }

        if (!int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) ||
            value > 0x10FFFF)
        {
            value = 0;
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Parses a <c>/Differences</c> array's code/glyph-name pairs, per the PDF specification's
    ///     own alternating "a starting code, then zero or more names assigned to consecutive codes
    ///     from that starting code" grammar, invoking <paramref name="assign"/> once per
    ///     code/glyph-name pair in array order. Shared by <see cref="ApplyDifferences"/> (a
    ///     simple/composite font's own codepoint-resolving policy) and <c>ResolveType3Encoding</c>
    ///     (<c>PdfDocument.Fonts.Type3.cs</c>'s own "keep the glyph name verbatim, as a direct
    ///     <c>/CharProcs</c> key" policy) - the shared state machine (starting-code tracking,
    ///     operand-kind/range validation) is identical between both callers; only what each caller
    ///     does with a resolved <c>(code, name)</c> pair differs.
    /// </summary>
    /// <param name="differences">The font dictionary's resolved <c>/Encoding/Differences</c> array.</param>
    /// <param name="assign">Invoked once per <c>(code, glyphName)</c> pair, in array order.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="differences"/> is not an array, when an array entry is
    ///     neither a number nor a name, or when a name entry appears before any starting code (or
    ///     after the code has advanced past 255).
    /// </exception>
    private void ParseDifferences(PdfObject differences, Action<int, string> assign)
    {
        if (differences.Kind != PdfKind.Array)
        {
            throw new InvalidDataException("/Encoding/Differences must be an array.");
        }

        var code = -1;
        foreach (var item in differences.Items)
        {
            var resolved = Resolve(item);
            if (resolved.Kind == PdfKind.Number)
            {
                code = (int)resolved.Number;
                continue;
            }

            if (resolved.Kind != PdfKind.Name)
            {
                throw new InvalidDataException("/Encoding/Differences array entries must be numbers or names.");
            }

            if (code is < 0 or > 255)
            {
                throw new InvalidDataException(
                    "/Encoding/Differences glyph name is not preceded by a valid starting code in [0, 255].");
            }

            assign(code, resolved.Text);
            code++;
        }
    }

    /// <summary>
    ///     Builds the codepoint-to-glyph-name map <see cref="LoadType1Font"/>/
    ///     <see cref="LoadType1CFont"/> (<c>PdfDocument.Fonts.Type1.cs</c>) pass to
    ///     <see cref="Fonts.TrueTypeFont.LoadType1"/>/<see cref="Fonts.TrueTypeFont.LoadType1C"/>
    ///     to build an embedded font program's synthetic <c>cmap</c>-equivalent lookup: starts
    ///     from <see cref="CodepointToStandardGlyphName"/>'s generic Adobe-glyph-name guess, then
    ///     - for this specific font dictionary's own <c>/Encoding/Differences</c> array, if any -
    ///     overwrites that guess, per codepoint, with the array's own literal declared glyph name
    ///     wherever that name itself resolves to a codepoint (via
    ///     <see cref="TryResolveGlyphNameToCodepoint"/>).
    /// </summary>
    /// <remarks>
    ///     This exists because the generic reverse-name guess and the embedded font program's own
    ///     real glyph-name vocabulary can diverge for the exact same codepoint: a subsetted font
    ///     may spell a glyph differently than <see cref="CodepointToStandardGlyphName"/>'s own
    ///     first-wins guess (for example naming a ligature glyph with an AGL
    ///     <c>uniXXXX</c>-convention name that has no entry in <see cref="StandardGlyphNames"/> at
    ///     all, so the generic map has no name for that codepoint to try in the first place). Since
    ///     a <c>/Differences</c> array's own declared name is the document author's (and often the
    ///     embedded font's own charset's) literal spelling for that codepoint, it is strictly more
    ///     likely to match the embedded font's real glyph name than a generic guess - so it wins on
    ///     a per-codepoint basis here, before the embedded font program is ever loaded. A
    ///     <c>/Differences</c> name that does not itself resolve to a codepoint (see
    ///     <see cref="ApplyDifferences"/>'s own tolerant handling of that case) contributes nothing
    ///     here either - this method never throws for it, mirroring that same leniency.
    /// </remarks>
    /// <param name="encodingEntry">
    ///     The font dictionary's <c>/Encoding</c> entry (a name, a dictionary, or
    ///     <see langword="null"/> when absent) - only a dictionary's own <c>/Differences</c> array
    ///     (if present) contributes any enrichment; a bare base-encoding name contributes none.
    /// </param>
    /// <returns>
    ///     The enriched codepoint-to-glyph-name map, newly allocated so mutating it never affects
    ///     <see cref="CodepointToStandardGlyphName"/> itself.
    /// </returns>
    private IReadOnlyDictionary<int, string> BuildEmbeddedFontGlyphNameMap(PdfObject? encodingEntry)
    {
        var map = new Dictionary<int, string>(CodepointToStandardGlyphName);

        if (encodingEntry is not null && Resolve(encodingEntry) is { Kind: PdfKind.Dictionary } resolvedEncoding)
        {
            var differencesEntry = resolvedEncoding.Get("Differences");
            if (differencesEntry is not null)
            {
                ParseDifferences(Resolve(differencesEntry), (_, name) =>
                {
                    if (TryResolveGlyphNameToCodepoint(name, out var codepoint))
                    {
                        map[codepoint] = name;
                    }
                });
            }
        }

        return map;
    }

    /// <summary>
    ///     Resolves a font dictionary's <c>/FirstChar</c>/<c>/LastChar</c>/<c>/Widths</c> entries
    ///     into a code-to-declared-width map, plus the font descriptor's <c>/MissingWidth</c>
    ///     fallback.
    /// </summary>
    /// <returns>
    ///     The code-to-width map (empty when <c>/FirstChar</c>/<c>/Widths</c> is absent or
    ///     malformed - a documented leniency, since <see cref="ResolvedSimpleFont.Resolve"/>
    ///     itself falls back to <see cref="ResolvedSimpleFont.MissingWidth"/> and then the font's
    ///     own metrics for any code missing from this map), and the resolved <c>/MissingWidth</c>
    ///     value (<c>0</c> when not declared).
    /// </returns>
    private (IReadOnlyDictionary<int, double> Widths, double MissingWidth) ResolveWidths(PdfObject fontDict, PdfObject descriptor)
    {
        var missingWidth = 0.0;
        var missingWidthEntry = descriptor.Get("MissingWidth");
        if (missingWidthEntry is not null && Resolve(missingWidthEntry) is { Kind: PdfKind.Number } missingWidthValue)
        {
            missingWidth = missingWidthValue.Number;
        }

        var widths = new Dictionary<int, double>();
        var firstCharEntry = fontDict.Get("FirstChar");
        var widthsEntry = fontDict.Get("Widths");
        if (firstCharEntry is not null && widthsEntry is not null &&
            Resolve(firstCharEntry) is { Kind: PdfKind.Number } firstCharValue &&
            Resolve(widthsEntry) is { Kind: PdfKind.Array } widthsArray)
        {
            var firstChar = (int)firstCharValue.Number;
            for (var i = 0; i < widthsArray.Items.Count; i++)
            {
                if (Resolve(widthsArray.Items[i]) is { Kind: PdfKind.Number } widthValue)
                {
                    widths[firstChar + i] = widthValue.Number;
                }
            }
        }

        return (widths, missingWidth);
    }

    /// <summary>
    ///     Maps a subset of the Adobe Glyph List's standard glyph names to their Unicode
    ///     codepoints, sufficient to build <see cref="WinAnsiEncodingTable"/>/
    ///     <see cref="MacRomanEncodingTable"/> and to resolve every common ASCII/Latin-1
    ///     <c>/Differences</c> glyph name.
    /// </summary>
    /// <remarks>
    ///     This is a deliberately partial subset of the full Adobe Glyph List (not every glyph
    ///     name ever defined) - a glyph name outside this set is additionally checked against the
    ///     AGL's generic <c>uniXXXX</c>/<c>uXXXX</c> hex-codepoint naming convention by
    ///     <see cref="TryResolveGlyphNameToCodepoint"/>, and a name neither this table nor that
    ///     convention resolves is tolerated, not an error: <see cref="ApplyDifferences"/> simply
    ///     leaves that one code at whatever the base encoding already assigned it, rather than
    ///     rejecting the entire document (a silent "keep the prior mapping" leniency, not a
    ///     silent mis-mapping to codepoint <c>0</c>/<c>.notdef</c>) - see that method's own
    ///     remarks.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, int> StandardGlyphNames = new Dictionary<string, int>
    {
        ["A"] = 0x0041,
        ["AE"] = 0x00C6,
        ["Aacute"] = 0x00C1,
        ["Acircumflex"] = 0x00C2,
        ["Adieresis"] = 0x00C4,
        ["Agrave"] = 0x00C0,
        ["Aring"] = 0x00C5,
        ["Atilde"] = 0x00C3,
        ["B"] = 0x0042,
        ["C"] = 0x0043,
        ["Ccedilla"] = 0x00C7,
        ["D"] = 0x0044,
        ["Delta"] = 0x2206,
        ["E"] = 0x0045,
        ["Eacute"] = 0x00C9,
        ["Ecircumflex"] = 0x00CA,
        ["Edieresis"] = 0x00CB,
        ["Egrave"] = 0x00C8,
        ["Eth"] = 0x00D0,
        ["Euro"] = 0x20AC,
        ["F"] = 0x0046,
        ["G"] = 0x0047,
        ["H"] = 0x0048,
        ["I"] = 0x0049,
        ["Iacute"] = 0x00CD,
        ["Icircumflex"] = 0x00CE,
        ["Idieresis"] = 0x00CF,
        ["Igrave"] = 0x00CC,
        ["J"] = 0x004A,
        ["K"] = 0x004B,
        ["L"] = 0x004C,
        ["M"] = 0x004D,
        ["N"] = 0x004E,
        ["Ntilde"] = 0x00D1,
        ["O"] = 0x004F,
        ["OE"] = 0x0152,
        ["Oacute"] = 0x00D3,
        ["Ocircumflex"] = 0x00D4,
        ["Odieresis"] = 0x00D6,
        ["Ograve"] = 0x00D2,
        ["Omega"] = 0x03A9,
        ["Oslash"] = 0x00D8,
        ["Otilde"] = 0x00D5,
        ["P"] = 0x0050,
        ["Q"] = 0x0051,
        ["R"] = 0x0052,
        ["S"] = 0x0053,
        ["Scaron"] = 0x0160,
        ["T"] = 0x0054,
        ["Thorn"] = 0x00DE,
        ["U"] = 0x0055,
        ["Uacute"] = 0x00DA,
        ["Ucircumflex"] = 0x00DB,
        ["Udieresis"] = 0x00DC,
        ["Ugrave"] = 0x00D9,
        ["V"] = 0x0056,
        ["W"] = 0x0057,
        ["X"] = 0x0058,
        ["Y"] = 0x0059,
        ["Yacute"] = 0x00DD,
        ["Ydieresis"] = 0x0178,
        ["Z"] = 0x005A,
        ["Zcaron"] = 0x017D,
        ["a"] = 0x0061,
        ["aacute"] = 0x00E1,
        ["acircumflex"] = 0x00E2,
        ["acute"] = 0x00B4,
        ["adieresis"] = 0x00E4,
        ["ae"] = 0x00E6,
        ["agrave"] = 0x00E0,
        ["ampersand"] = 0x0026,
        ["apple"] = 0xF8FF,
        ["approxequal"] = 0x2248,
        ["aring"] = 0x00E5,
        ["asciicircum"] = 0x005E,
        ["asciitilde"] = 0x007E,
        ["asterisk"] = 0x002A,
        ["at"] = 0x0040,
        ["atilde"] = 0x00E3,
        ["b"] = 0x0062,
        ["backslash"] = 0x005C,
        ["bar"] = 0x007C,
        ["braceleft"] = 0x007B,
        ["braceright"] = 0x007D,
        ["bracketleft"] = 0x005B,
        ["bracketright"] = 0x005D,
        ["breve"] = 0x02D8,
        ["brokenbar"] = 0x00A6,
        ["bullet"] = 0x2022,
        ["c"] = 0x0063,
        ["caron"] = 0x02C7,
        ["ccedilla"] = 0x00E7,
        ["cedilla"] = 0x00B8,
        ["cent"] = 0x00A2,
        ["circumflex"] = 0x02C6,
        ["colon"] = 0x003A,
        ["comma"] = 0x002C,
        ["copyright"] = 0x00A9,
        ["currency"] = 0x00A4,
        ["d"] = 0x0064,
        ["dagger"] = 0x2020,
        ["daggerdbl"] = 0x2021,
        ["degree"] = 0x00B0,
        ["dieresis"] = 0x00A8,
        ["divide"] = 0x00F7,
        ["dollar"] = 0x0024,
        ["dotaccent"] = 0x02D9,
        ["dotlessi"] = 0x0131,
        ["e"] = 0x0065,
        ["eacute"] = 0x00E9,
        ["ecircumflex"] = 0x00EA,
        ["edieresis"] = 0x00EB,
        ["egrave"] = 0x00E8,
        ["eight"] = 0x0038,
        ["ellipsis"] = 0x2026,
        ["emdash"] = 0x2014,
        ["endash"] = 0x2013,
        ["equal"] = 0x003D,
        ["eth"] = 0x00F0,
        ["exclam"] = 0x0021,
        ["exclamdown"] = 0x00A1,
        ["f"] = 0x0066,
        ["ff"] = 0xFB00,
        ["ffi"] = 0xFB03,
        ["ffl"] = 0xFB04,
        ["fi"] = 0xFB01,
        ["five"] = 0x0035,
        ["fl"] = 0xFB02,
        ["florin"] = 0x0192,
        ["four"] = 0x0034,
        ["fraction"] = 0x2044,
        ["g"] = 0x0067,
        ["germandbls"] = 0x00DF,
        ["grave"] = 0x0060,
        ["greater"] = 0x003E,
        ["greaterequal"] = 0x2265,
        ["guillemotleft"] = 0x00AB,
        ["guillemotright"] = 0x00BB,
        ["guilsinglleft"] = 0x2039,
        ["guilsinglright"] = 0x203A,
        ["h"] = 0x0068,
        ["hungarumlaut"] = 0x02DD,
        ["hyphen"] = 0x002D,
        ["i"] = 0x0069,
        ["iacute"] = 0x00ED,
        ["icircumflex"] = 0x00EE,
        ["idieresis"] = 0x00EF,
        ["igrave"] = 0x00EC,
        ["infinity"] = 0x221E,
        ["integral"] = 0x222B,
        ["j"] = 0x006A,
        ["k"] = 0x006B,
        ["l"] = 0x006C,
        ["less"] = 0x003C,
        ["lessequal"] = 0x2264,
        ["logicalnot"] = 0x00AC,
        ["lozenge"] = 0xF8E7,
        ["m"] = 0x006D,
        ["macron"] = 0x00AF,
        ["mu"] = 0x00B5,
        ["multiply"] = 0x00D7,
        ["n"] = 0x006E,
        ["nacute"] = 0x0144,
        ["nbspace"] = 0x00A0,
        ["nine"] = 0x0039,
        ["notequal"] = 0x2260,
        ["ntilde"] = 0x00F1,
        ["numbersign"] = 0x0023,
        ["o"] = 0x006F,
        ["oacute"] = 0x00F3,
        ["ocircumflex"] = 0x00F4,
        ["odieresis"] = 0x00F6,
        ["oe"] = 0x0153,
        ["ogonek"] = 0x02DB,
        ["ograve"] = 0x00F2,
        ["one"] = 0x0031,
        ["onehalf"] = 0x00BD,
        ["onequarter"] = 0x00BC,
        ["onesuperior"] = 0x00B9,
        ["ordfeminine"] = 0x00AA,
        ["ordmasculine"] = 0x00BA,
        ["oslash"] = 0x00F8,
        ["otilde"] = 0x00F5,
        ["p"] = 0x0070,
        ["paragraph"] = 0x00B6,
        ["parenleft"] = 0x0028,
        ["parenright"] = 0x0029,
        ["partialdiff"] = 0x2202,
        ["percent"] = 0x0025,
        ["period"] = 0x002E,
        ["periodcentered"] = 0x00B7,
        ["perthousand"] = 0x2030,
        ["pi"] = 0x03C0,
        ["plus"] = 0x002B,
        ["plusminus"] = 0x00B1,
        ["product"] = 0x220F,
        ["q"] = 0x0071,
        ["question"] = 0x003F,
        ["questiondown"] = 0x00BF,
        ["quotedbl"] = 0x0022,
        ["quotedblbase"] = 0x201E,
        ["quotedblleft"] = 0x201C,
        ["quotedblright"] = 0x201D,
        ["quoteleft"] = 0x2018,
        ["quoteright"] = 0x2019,
        ["quotesinglbase"] = 0x201A,
        ["quotesingle"] = 0x0027,
        ["r"] = 0x0072,
        ["radical"] = 0x221A,
        ["registered"] = 0x00AE,
        ["ring"] = 0x02DA,
        ["s"] = 0x0073,
        ["scaron"] = 0x0161,
        ["section"] = 0x00A7,
        ["semicolon"] = 0x003B,
        ["seven"] = 0x0037,
        ["six"] = 0x0036,
        ["slash"] = 0x002F,
        ["space"] = 0x0020,
        ["sterling"] = 0x00A3,
        ["summation"] = 0x2211,
        ["t"] = 0x0074,
        ["thinspace"] = 0x2009,
        ["thorn"] = 0x00FE,
        ["three"] = 0x0033,
        ["threequarters"] = 0x00BE,
        ["threesuperior"] = 0x00B3,
        ["tilde"] = 0x02DC,
        ["trademark"] = 0x2122,
        ["two"] = 0x0032,
        ["twosuperior"] = 0x00B2,
        ["u"] = 0x0075,
        ["uacute"] = 0x00FA,
        ["ucircumflex"] = 0x00FB,
        ["udieresis"] = 0x00FC,
        ["ugrave"] = 0x00F9,
        ["underscore"] = 0x005F,
        ["v"] = 0x0076,
        ["w"] = 0x0077,
        ["x"] = 0x0078,
        ["y"] = 0x0079,
        ["yacute"] = 0x00FD,
        ["ydieresis"] = 0x00FF,
        ["yen"] = 0x00A5,
        ["z"] = 0x007A,
        ["zcaron"] = 0x017E,
        ["zero"] = 0x0030,
    };

    /// <summary>
    ///     The PDF specification (Appendix D) <c>/WinAnsiEncoding</c> base encoding, as a
    ///     256-entry code-to-Unicode-codepoint table (<c>0</c> for an undefined code).
    /// </summary>
    private static readonly int[] WinAnsiEncodingTable =
    [
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x0026, 0x0027,
        0x0028, 0x0029, 0x002A, 0x002B, 0x002C, 0x002D, 0x002E, 0x002F,
        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037,
        0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F,
        0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047,
        0x0048, 0x0049, 0x004A, 0x004B, 0x004C, 0x004D, 0x004E, 0x004F,
        0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057,
        0x0058, 0x0059, 0x005A, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F,
        0x0060, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067,
        0x0068, 0x0069, 0x006A, 0x006B, 0x006C, 0x006D, 0x006E, 0x006F,
        0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077,
        0x0078, 0x0079, 0x007A, 0x007B, 0x007C, 0x007D, 0x007E, 0x2022,
        0x20AC, 0x2022, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021,
        0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x2022, 0x017D, 0x2022,
        0x2022, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014,
        0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x2022, 0x017E, 0x0178,
        0x0020, 0x00A1, 0x00A2, 0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7,
        0x00A8, 0x00A9, 0x00AA, 0x00AB, 0x00AC, 0x002D, 0x00AE, 0x00AF,
        0x00B0, 0x00B1, 0x00B2, 0x00B3, 0x00B4, 0x00B5, 0x00B6, 0x00B7,
        0x00B8, 0x00B9, 0x00BA, 0x00BB, 0x00BC, 0x00BD, 0x00BE, 0x00BF,
        0x00C0, 0x00C1, 0x00C2, 0x00C3, 0x00C4, 0x00C5, 0x00C6, 0x00C7,
        0x00C8, 0x00C9, 0x00CA, 0x00CB, 0x00CC, 0x00CD, 0x00CE, 0x00CF,
        0x00D0, 0x00D1, 0x00D2, 0x00D3, 0x00D4, 0x00D5, 0x00D6, 0x00D7,
        0x00D8, 0x00D9, 0x00DA, 0x00DB, 0x00DC, 0x00DD, 0x00DE, 0x00DF,
        0x00E0, 0x00E1, 0x00E2, 0x00E3, 0x00E4, 0x00E5, 0x00E6, 0x00E7,
        0x00E8, 0x00E9, 0x00EA, 0x00EB, 0x00EC, 0x00ED, 0x00EE, 0x00EF,
        0x00F0, 0x00F1, 0x00F2, 0x00F3, 0x00F4, 0x00F5, 0x00F6, 0x00F7,
        0x00F8, 0x00F9, 0x00FA, 0x00FB, 0x00FC, 0x00FD, 0x00FE, 0x00FF,
    ];

    /// <summary>
    ///     The PDF specification (Appendix D) <c>/MacRomanEncoding</c> base encoding, as a
    ///     256-entry code-to-Unicode-codepoint table (<c>0</c> for an undefined code).
    /// </summary>
    private static readonly int[] MacRomanEncodingTable =
    [
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x0026, 0x0027,
        0x0028, 0x0029, 0x002A, 0x002B, 0x002C, 0x002D, 0x002E, 0x002F,
        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037,
        0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F,
        0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047,
        0x0048, 0x0049, 0x004A, 0x004B, 0x004C, 0x004D, 0x004E, 0x004F,
        0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057,
        0x0058, 0x0059, 0x005A, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F,
        0x0060, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067,
        0x0068, 0x0069, 0x006A, 0x006B, 0x006C, 0x006D, 0x006E, 0x006F,
        0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077,
        0x0078, 0x0079, 0x007A, 0x007B, 0x007C, 0x007D, 0x007E, 0x0000,
        0x00C4, 0x00C5, 0x00C7, 0x00C9, 0x00D1, 0x00D6, 0x00DC, 0x00E1,
        0x00E0, 0x00E2, 0x00E4, 0x00E3, 0x00E5, 0x00E7, 0x00E9, 0x00E8,
        0x00EA, 0x00EB, 0x00ED, 0x00EC, 0x00EE, 0x00EF, 0x00F1, 0x00F3,
        0x00F2, 0x00F4, 0x00F6, 0x00F5, 0x00FA, 0x00F9, 0x00FB, 0x00FC,
        0x2020, 0x00B0, 0x00A2, 0x00A3, 0x00A7, 0x2022, 0x00B6, 0x00DF,
        0x00AE, 0x00A9, 0x2122, 0x00B4, 0x00A8, 0x2260, 0x00C6, 0x00D8,
        0x221E, 0x00B1, 0x2264, 0x2265, 0x00A5, 0x00B5, 0x2202, 0x2211,
        0x220F, 0x03C0, 0x222B, 0x00AA, 0x00BA, 0x03A9, 0x00E6, 0x00F8,
        0x00BF, 0x00A1, 0x00AC, 0x221A, 0x0192, 0x2248, 0x2206, 0x00AB,
        0x00BB, 0x2026, 0x0020, 0x00C0, 0x00C3, 0x00D5, 0x0152, 0x0153,
        0x2013, 0x2014, 0x201C, 0x201D, 0x2018, 0x2019, 0x00F7, 0xF8E7,
        0x00FF, 0x0178, 0x2044, 0x00A4, 0x2039, 0x203A, 0xFB01, 0xFB02,
        0x2021, 0x00B7, 0x201A, 0x201E, 0x2030, 0x00C2, 0x00CA, 0x00C1,
        0x00CB, 0x00C8, 0x00CD, 0x00CE, 0x00CF, 0x00CC, 0x00D3, 0x00D4,
        0xF8FF, 0x00D2, 0x00DA, 0x00DB, 0x00D9, 0x0131, 0x02C6, 0x02DC,
        0x00AF, 0x02D8, 0x02D9, 0x02DA, 0x00B8, 0x02DD, 0x02DB, 0x02C7,
    ];

    /// <summary>
    ///     The PDF specification (Appendix D) <c>/StandardEncoding</c> base encoding (Adobe's
    ///     original PostScript font encoding, and PDF's own default for a <c>/Subtype /Type1</c>
    ///     font's built-in encoding), as a 256-entry code-to-Unicode-codepoint table (<c>0</c> for
    ///     an undefined code) - derived from the Unicode Consortium's own published
    ///     <c>stdenc.txt</c> cross-reference mapping, the canonical source for this table.
    /// </summary>
    private static readonly int[] StandardEncodingTable =
    [
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x0026, 0x2019,
        0x0028, 0x0029, 0x002A, 0x002B, 0x002C, 0x002D, 0x002E, 0x002F,
        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037,
        0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F,
        0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047,
        0x0048, 0x0049, 0x004A, 0x004B, 0x004C, 0x004D, 0x004E, 0x004F,
        0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057,
        0x0058, 0x0059, 0x005A, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F,
        0x2018, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067,
        0x0068, 0x0069, 0x006A, 0x006B, 0x006C, 0x006D, 0x006E, 0x006F,
        0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077,
        0x0078, 0x0079, 0x007A, 0x007B, 0x007C, 0x007D, 0x007E, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x00A1, 0x00A2, 0x00A3, 0x2044, 0x00A5, 0x0192, 0x00A7,
        0x00A4, 0x0027, 0x201C, 0x00AB, 0x2039, 0x203A, 0xFB01, 0xFB02,
        0x0000, 0x2013, 0x2020, 0x2021, 0x00B7, 0x0000, 0x00B6, 0x2022,
        0x201A, 0x201E, 0x201D, 0x00BB, 0x2026, 0x2030, 0x0000, 0x00BF,
        0x0000, 0x0060, 0x00B4, 0x02C6, 0x02DC, 0x00AF, 0x02D8, 0x02D9,
        0x00A8, 0x0000, 0x02DA, 0x00B8, 0x0000, 0x02DD, 0x02DB, 0x02C7,
        0x2014, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x00C6, 0x0000, 0x00AA, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0141, 0x00D8, 0x0152, 0x00BA, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x00E6, 0x0000, 0x0000, 0x0000, 0x0131, 0x0000, 0x0000,
        0x0142, 0x00F8, 0x0153, 0x00DF, 0x0000, 0x0000, 0x0000, 0x0000,
    ];

    /// <summary>
    ///     Implements PDF 32000-1 Appendix D's "Symbol Set and ZapfDingbats Encoding" table (the
    ///     <c>Symbol</c> column): the built-in encoding of the Standard-14 <c>Symbol</c> font, as
    ///     a 256-entry code-to-Unicode-codepoint table (<c>0</c> for an undefined code). Used by
    ///     <see cref="ResolveEncoding"/> as the default base table (in place of
    ///     <see cref="WinAnsiEncodingTable"/>) only for a <c>/BaseFont /Symbol</c> font resolved
    ///     via the bundled Noto substitute path - see <see cref="BuildResolvedSimpleFont"/>'s own
    ///     remarks.
    /// </summary>
    /// <remarks>
    ///     Six Private-Use-Area-only glyph names (<c>registerserif</c>/<c>registersans</c>,
    ///     <c>copyrightserif</c>/<c>copyrightsans</c>, <c>trademarkserif</c>/<c>trademarksans</c>)
    ///     are deliberately mapped to their plain/generic Unicode equivalent (U+00AE, U+00A9, and
    ///     U+2122 respectively) rather than left unmapped, since a generic substitute font has no
    ///     reason to carry the Symbol font's own Private-Use-Area glyph variants. Roughly 20 other
    ///     Private-Use-Area-only glyph names (extensible delimiter pieces, for example
    ///     <c>radicalex</c>) are deliberately left as <c>0x0000</c> (unmapped), since they have no
    ///     meaningful standalone Unicode equivalent. Of the 163 distinct mapped codepoints this
    ///     table defines, 161 are covered by the bundled Noto substitute fonts (see
    ///     <c>Fonts/BundledFonts/README.md</c>) - only U+2329/U+232A (codes <c>0xE1</c>/<c>0xD1</c>)
    ///     are not - a documented, accepted fidelity limitation.
    /// </remarks>
    private static readonly int[] SymbolEncodingTable =
    [
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0020, 0x0021, 0x2200, 0x0023, 0x2203, 0x0025, 0x0026, 0x220B,
        0x0028, 0x0029, 0x2217, 0x002B, 0x002C, 0x2212, 0x002E, 0x002F,
        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037,
        0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F,
        0x2245, 0x0391, 0x0392, 0x03A7, 0x2206, 0x0395, 0x03A6, 0x0393,
        0x0397, 0x0399, 0x03D1, 0x039A, 0x039B, 0x039C, 0x039D, 0x039F,
        0x03A0, 0x0398, 0x03A1, 0x03A3, 0x03A4, 0x03A5, 0x03C2, 0x2126,
        0x039E, 0x03A8, 0x0396, 0x005B, 0x2234, 0x005D, 0x22A5, 0x005F,
        0x0000, 0x03B1, 0x03B2, 0x03C7, 0x03B4, 0x03B5, 0x03C6, 0x03B3,
        0x03B7, 0x03B9, 0x03D5, 0x03BA, 0x03BB, 0x00B5, 0x03BD, 0x03BF,
        0x03C0, 0x03B8, 0x03C1, 0x03C3, 0x03C4, 0x03C5, 0x03D6, 0x03C9,
        0x03BE, 0x03C8, 0x03B6, 0x007B, 0x007C, 0x007D, 0x223C, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x20AC, 0x03D2, 0x2032, 0x2264, 0x2044, 0x221E, 0x0192, 0x2663,
        0x2666, 0x2665, 0x2660, 0x2194, 0x2190, 0x2191, 0x2192, 0x2193,
        0x00B0, 0x00B1, 0x2033, 0x2265, 0x00D7, 0x221D, 0x2202, 0x2022,
        0x00F7, 0x2260, 0x2261, 0x2248, 0x2026, 0x0000, 0x0000, 0x21B5,
        0x2135, 0x2111, 0x211C, 0x2118, 0x2297, 0x2295, 0x2205, 0x2229,
        0x222A, 0x2283, 0x2287, 0x2284, 0x2282, 0x2286, 0x2208, 0x2209,
        0x2220, 0x2207, 0x00AE, 0x00A9, 0x2122, 0x220F, 0x221A, 0x22C5,
        0x00AC, 0x2227, 0x2228, 0x21D4, 0x21D0, 0x21D1, 0x21D2, 0x21D3,
        0x25CA, 0x2329, 0x00AE, 0x00A9, 0x2122, 0x2211, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x232A, 0x222B, 0x2320, 0x0000, 0x2321, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
    ];

    /// <summary>
    ///     Implements PDF 32000-1 Appendix D's "Symbol Set and ZapfDingbats Encoding" table (the
    ///     <c>ZapfDingbats</c> column): the built-in encoding of the Standard-14
    ///     <c>ZapfDingbats</c> font, as a 256-entry code-to-Unicode-codepoint table (<c>0</c> for
    ///     an undefined code). Used by <see cref="ResolveEncoding"/> as the default base table (in
    ///     place of <see cref="WinAnsiEncodingTable"/>) only for a <c>/BaseFont /ZapfDingbats</c>
    ///     font resolved via the bundled Noto substitute path - see
    ///     <see cref="BuildResolvedSimpleFont"/>'s own remarks.
    /// </summary>
    /// <remarks>
    ///     Of the 202 distinct mapped codepoints this table defines, 158 are covered by the
    ///     bundled <c>NotoSansSymbols2-Regular.ttf</c> substitute font (see
    ///     <c>Fonts/BundledFonts/README.md</c>) - the circled-digit Dingbats U+2460-U+2469 and
    ///     U+2776-U+2793, and U+271D/U+271E/U+271F/U+2721, are not - a documented, accepted
    ///     fidelity limitation.
    /// </remarks>
    private static readonly int[] ZapfDingbatsEncodingTable =
    [
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0020, 0x2701, 0x2702, 0x2703, 0x2704, 0x260E, 0x2706, 0x2707,
        0x2708, 0x2709, 0x261B, 0x261E, 0x270C, 0x270D, 0x270E, 0x270F,
        0x2710, 0x2711, 0x2712, 0x2713, 0x2714, 0x2715, 0x2716, 0x2717,
        0x2718, 0x2719, 0x271A, 0x271B, 0x271C, 0x271D, 0x271E, 0x271F,
        0x2720, 0x2721, 0x2722, 0x2723, 0x2724, 0x2725, 0x2726, 0x2727,
        0x2605, 0x2729, 0x272A, 0x272B, 0x272C, 0x272D, 0x272E, 0x272F,
        0x2730, 0x2731, 0x2732, 0x2733, 0x2734, 0x2735, 0x2736, 0x2737,
        0x2738, 0x2739, 0x273A, 0x273B, 0x273C, 0x273D, 0x273E, 0x273F,
        0x2740, 0x2741, 0x2742, 0x2743, 0x2744, 0x2745, 0x2746, 0x2747,
        0x2748, 0x2749, 0x274A, 0x274B, 0x25CF, 0x274D, 0x25A0, 0x274F,
        0x2750, 0x2751, 0x2752, 0x25B2, 0x25BC, 0x25C6, 0x2756, 0x25D7,
        0x2758, 0x2759, 0x275A, 0x275B, 0x275C, 0x275D, 0x275E, 0x0000,
        0x2768, 0x2769, 0x276A, 0x276B, 0x276C, 0x276D, 0x276E, 0x276F,
        0x2770, 0x2771, 0x2772, 0x2773, 0x2774, 0x2775, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000,
        0x0000, 0x2761, 0x2762, 0x2763, 0x2764, 0x2765, 0x2766, 0x2767,
        0x2663, 0x2666, 0x2665, 0x2660, 0x2460, 0x2461, 0x2462, 0x2463,
        0x2464, 0x2465, 0x2466, 0x2467, 0x2468, 0x2469, 0x2776, 0x2777,
        0x2778, 0x2779, 0x277A, 0x277B, 0x277C, 0x277D, 0x277E, 0x277F,
        0x2780, 0x2781, 0x2782, 0x2783, 0x2784, 0x2785, 0x2786, 0x2787,
        0x2788, 0x2789, 0x278A, 0x278B, 0x278C, 0x278D, 0x278E, 0x278F,
        0x2790, 0x2791, 0x2792, 0x2793, 0x2794, 0x2192, 0x2194, 0x2195,
        0x2798, 0x2799, 0x279A, 0x279B, 0x279C, 0x279D, 0x279E, 0x279F,
        0x27A0, 0x27A1, 0x27A2, 0x27A3, 0x27A4, 0x27A5, 0x27A6, 0x27A7,
        0x27A8, 0x27A9, 0x27AA, 0x27AB, 0x27AC, 0x27AD, 0x27AE, 0x27AF,
        0x0000, 0x27B1, 0x27B2, 0x27B3, 0x27B4, 0x27B5, 0x27B6, 0x27B7,
        0x27B8, 0x27B9, 0x27BA, 0x27BB, 0x27BC, 0x27BD, 0x27BE, 0x0000,
    ];

    /// <summary>
    ///     The reverse mapping (Unicode codepoint to glyph name) built once from
    ///     <see cref="StandardGlyphNames"/>, used by <see cref="BuildEmbeddedFontGlyphNameMap"/>
    ///     as the generic starting point for the enriched, per-font-dictionary map
    ///     <see cref="LoadType1Font"/>/<see cref="LoadType1CFont"/> (see
    ///     <c>PdfDocument.Fonts.Type1.cs</c>) pass as <see cref="Fonts.TrueTypeFont.LoadType1"/>/
    ///     <see cref="Fonts.TrueTypeFont.LoadType1C"/>'s own <c>codepointToGlyphName</c> argument -
    ///     reusing this class's existing Adobe-glyph-name vocabulary rather than introducing a
    ///     second, separately-maintained glyph-name table.
    /// </summary>
    /// <remarks>
    ///     When more than one glyph name in <see cref="StandardGlyphNames"/> maps to the same
    ///     codepoint (for example a hypothetical synonym pair), the first one encountered (in
    ///     <see cref="StandardGlyphNames"/>'s own declaration order) wins - the same "first-wins
    ///     on collision" convention <c>Fonts.Type1StandardGlyphNames</c> uses for its own reverse
    ///     map. <see cref="BuildEmbeddedFontGlyphNameMap"/> may further overwrite an entry built
    ///     from this generic map with a document's own literal <c>/Differences</c>-declared name
    ///     for that same codepoint - see that method's own remarks for why.
    /// </remarks>
    private static readonly IReadOnlyDictionary<int, string> CodepointToStandardGlyphName =
        BuildCodepointToStandardGlyphName();

    private static IReadOnlyDictionary<int, string> BuildCodepointToStandardGlyphName()
    {
        var result = new Dictionary<int, string>();
        foreach (var (name, codepoint) in StandardGlyphNames)
        {
            result.TryAdd(codepoint, name);
        }

        return result;
    }

    /// <summary>
    ///     An empty codepoint-to-glyph-name map, reused wherever a <c>/FontFile3</c> loader must
    ///     supply <see cref="Fonts.TrueTypeFont.LoadType1C"/>'s required
    ///     <c>codepointToGlyphName</c> argument but the resulting font's own synthesized
    ///     <c>cmap</c>-equivalent lookup is never actually consulted - specifically, a composite
    ///     <c>/Type0</c> font's <c>CIDFontType0</c> descendant (see
    ///     <c>PdfDocument.Fonts.Type0.cs</c>'s <c>LoadCidFontType0Font</c>), whose CID-to-glyph-
    ///     index resolution is always the identity function, bypassing
    ///     <see cref="Fonts.TrueTypeFont.GetGlyphIndex"/> (and therefore this map) entirely -
    ///     unlike <see cref="LoadType1CFont"/>'s own simple-font <c>/FontFile3</c> path (see
    ///     <c>PdfDocument.Fonts.Type1.cs</c>), which passes the enriched map
    ///     <see cref="BuildEmbeddedFontGlyphNameMap"/> builds (seeded from
    ///     <see cref="CodepointToStandardGlyphName"/>) instead, since a simple font's codes are
    ///     resolved to glyphs by codepoint.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string> EmptyCodepointToGlyphName =
        new Dictionary<int, string>();

    /// <summary>
    ///     The actual byte-level container shape of an embedded <c>/FontFile3</c> font program,
    ///     sniffed from the stream's own decoded bytes (see <see cref="SniffFontFile3Shape"/>)
    ///     rather than trusted from its declared <c>/Subtype</c> name - the PDF specification
    ///     permits, and real-world producers sometimes emit, a mismatch between the two.
    /// </summary>
    private enum FontFile3Shape
    {
        /// <summary>
        ///     An SFNT-wrapped font program (TrueType, classic Mac <c>'true'</c>, CFF/OpenType
        ///     <c>'OTTO'</c>, or a <c>'ttcf'</c> collection) - see
        ///     <see cref="Fonts.SfntContainer.LooksLikeSfnt"/>. Loaded via
        ///     <see cref="Fonts.TrueTypeFont.Load(Stream)"/>.
        /// </summary>
        Sfnt,

        /// <summary>
        ///     A bare (standalone, non-SFNT-wrapped) CFF font program - see
        ///     <see cref="Fonts.CffTable.LooksLikeCffHeader"/>. Loaded via
        ///     <see cref="Fonts.TrueTypeFont.LoadType1C"/>.
        /// </summary>
        BareCff,
    }

    /// <summary>
    ///     Sniffs an already-decoded <c>/FontFile3</c> stream's bytes for their actual container
    ///     shape - an SFNT wrapper (<see cref="Fonts.SfntContainer.LooksLikeSfnt"/>) or a bare CFF
    ///     header (<see cref="Fonts.CffTable.LooksLikeCffHeader"/>) - used by both
    ///     <see cref="LoadType1CFont"/> (<c>PdfDocument.Fonts.Type1.cs</c>) and
    ///     <see cref="LoadCidFontType0Font"/> (<c>PdfDocument.Fonts.Type0.cs</c>) to dispatch to
    ///     the matching <c>Fonts.TrueTypeFont</c> loader regardless of the stream's own declared
    ///     <c>/Subtype</c> name, since the PDF specification permits (and real-world producers
    ///     sometimes emit) a mismatch between that declared name and the font program's actual
    ///     byte container shape.
    /// </summary>
    /// <param name="fontBytes">The <c>/FontFile3</c> stream's already-decoded bytes.</param>
    /// <returns>
    ///     The sniffed <see cref="FontFile3Shape"/>, or <see langword="null"/> when
    ///     <paramref name="fontBytes"/> matches neither a recognized SFNT container nor a
    ///     structurally plausible bare CFF header.
    /// </returns>
    private static FontFile3Shape? SniffFontFile3Shape(byte[] fontBytes)
    {
        if (SfntContainer.LooksLikeSfnt(fontBytes))
        {
            return FontFile3Shape.Sfnt;
        }

        if (CffTable.LooksLikeCffHeader(fontBytes))
        {
            return FontFile3Shape.BareCff;
        }

        return null;
    }
}

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
        ///     Gets the loaded embedded TrueType font backing this resolved font, or
        ///     <see langword="null"/> for a <c>ResolvedType3Font</c>: a Type 3 font has no
        ///     outline-glyph font program at all - its glyphs are content-stream procedures (see
        ///     <c>PdfDocument.Fonts.Type3.cs</c>'s <c>PaintType3Glyph</c>), so this member is
        ///     never consulted for it. Every non-Type3 concrete implementation declares this as a
        ///     <see langword="required"/> non-nullable property, so <see cref="ShowGlyph"/>'s own
        ///     non-Type3 branch may safely null-forgive it.
        /// </summary>
        TrueTypeFont? Font { get; }

        /// <summary>
        ///     Gets the number of bytes <see cref="ShowText"/> decodes per character code: <c>1</c>
        ///     for a simple or Type 3 font, <c>2</c> for a composite <c>/Identity-H</c> font.
        /// </summary>
        int CodeByteWidth { get; }

        /// <summary>
        ///     Resolves a single decoded character code to the glyph index <see cref="ShowGlyph"/>
        ///     paints (meaningless/unused for a <c>ResolvedType3Font</c> - see that type's own
        ///     remarks), and the code's advance width in text-space units, already converted from
        ///     the font's own glyph-space convention (1/1000 em for a simple/composite font, via
        ///     <c>/FontMatrix</c> for a Type 3 font - see <c>ResolvedType3Font.Resolve</c>'s own
        ///     remarks), not yet scaled by <see cref="GraphicsState.FontSize"/>.
        /// </summary>
        /// <param name="code">The decoded character code (a byte for a simple or Type 3 font, a 16-bit big-endian value for a composite font).</param>
        /// <returns>The resolved glyph index and text-space advance width.</returns>
        (int GlyphIndex, double Width) Resolve(int code);
    }

    /// <summary>
    ///     A simple (single-byte-code) TrueType font, fully resolved from its <c>/Resources/Font</c>
    ///     dictionary: the loaded <see cref="Fonts.TrueTypeFont"/>, its effective code-to-Unicode-
    ///     codepoint encoding table, and its code-to-declared-advance-width table.
    /// </summary>
    /// <remarks>
    ///     Instances are built once by <see cref="BuildResolvedSimpleFont"/> and cached by
    ///     <see cref="ResolveFont"/> in <see cref="_fontCache"/> for the lifetime of a single
    ///     <see cref="Render(int, int, int)"/> call - see <see cref="_fontCache"/>'s own remarks
    ///     for why the cache is never shared across calls.
    /// </remarks>
    private sealed class ResolvedSimpleFont : IResolvedFont
    {
        /// <summary>Gets the loaded embedded TrueType font.</summary>
        internal required TrueTypeFont Font { get; init; }

        TrueTypeFont IResolvedFont.Font => Font;

        /// <summary>A simple font always decodes exactly one byte per character code.</summary>
        int IResolvedFont.CodeByteWidth => 1;

        /// <summary>
        ///     Gets the effective code (0-255) to Unicode codepoint map, after applying the
        ///     font's <c>/Encoding</c> base encoding and any <c>/Differences</c> overrides. A
        ///     code absent from this map has no mapped codepoint (the PDF specification's
        ///     "undefined" slot in the base encoding table, never overridden by
        ///     <c>/Differences</c>) - <see cref="ShowText"/> treats such a code as mapping to
        ///     Unicode codepoint <c>0</c>, which almost always resolves to glyph <c>0</c>
        ///     (<c>.notdef</c>) via <see cref="Fonts.TrueTypeFont.GetGlyphIndex"/>.
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
        ///     Resolves <paramref name="code"/> to a glyph index via <see cref="Encoding"/>/
        ///     <see cref="Fonts.TrueTypeFont.GetGlyphIndex"/>, and its advance width with the
        ///     documented priority: an explicit <see cref="Widths"/> entry first, else
        ///     <see cref="MissingWidth"/> (when nonzero), else the font's own
        ///     <see cref="Fonts.TrueTypeFont.GetAdvanceWidth"/>/<see cref="Fonts.TrueTypeFont.UnitsPerEm"/>
        ///     metric.
        /// </summary>
        public (int GlyphIndex, double Width) Resolve(int code)
        {
            var codepoint = Encoding.TryGetValue(code, out var mapped) ? mapped : 0;
            var glyphIndex = Font.GetGlyphIndex(codepoint);

            if (Widths.TryGetValue(code, out var declaredWidth))
            {
                return (glyphIndex, declaredWidth / 1000.0);
            }

            if (MissingWidth != 0)
            {
                return (glyphIndex, MissingWidth / 1000.0);
            }

            return (glyphIndex, Font.GetAdvanceWidth(glyphIndex) / (double)Font.UnitsPerEm);
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
    ///     "per-<see cref="Render(int, int, int)"/>-call only" cache scope, never shared or reused
    ///     across separate <see cref="Render(int, int, int)"/> calls on the same
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
    ///     diagnostics). Only a <c>Symbol</c>/<c>ZapfDingbats</c> (or otherwise symbolic, per
    ///     <c>/FontDescriptor/Flags</c>) font with no embedded font program still fails
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
    ///     Thrown when a <c>Symbol</c>/<c>ZapfDingbats</c>/symbolic font has no embedded font
    ///     program (see <see cref="ResolveFallbackFont"/>), or propagated from
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
        TrueTypeFont font;
        if (fontFile2Entry is not null)
        {
            var fontFileStream = Resolve(fontFile2Entry);
            if (fontFileStream.Kind != PdfKind.Stream)
            {
                throw new InvalidDataException("/FontDescriptor/FontFile2 does not resolve to a stream.");
            }

            var fontBytes = GetStreamDecodedBytes(fontFileStream);
            font = TrueTypeFont.Load(new MemoryStream(fontBytes));
        }
        else if (fontFileEntry is not null)
        {
            font = LoadType1Font(descriptor);
        }
        else if (subtype == "Type1" && descriptor.Get("FontFile3") is not null)
        {
            font = LoadType1CFont(descriptor);
        }
        else
        {
            var baseFontName = GetNameValue(fontDict, "BaseFont")
                ?? throw new InvalidDataException("Font dictionary is missing required /BaseFont.");
            font = ResolveFallbackFont(baseFontName, descriptor);
        }

        var encoding = ResolveEncoding(fontDict.Get("Encoding"));
        var (widths, missingWidth) = ResolveWidths(fontDict, descriptor);

        return new ResolvedSimpleFont
        {
            Font = font,
            Encoding = encoding,
            Widths = widths,
            MissingWidth = missingWidth,
        };
    }

    /// <summary>
    ///     Resolves a font dictionary's <c>/Encoding</c> entry into a full 256-entry code-to-
    ///     Unicode-codepoint map, applying the named base encoding (defaulting to
    ///     <c>/WinAnsiEncoding</c> when <c>/Encoding</c> is absent) and any <c>/Differences</c>
    ///     overrides.
    /// </summary>
    /// <param name="encodingEntry">
    ///     The font dictionary's <c>/Encoding</c> entry (a name, a dictionary, or
    ///     <see langword="null"/> when absent).
    /// </param>
    /// <returns>A code-to-codepoint map covering every code with a defined mapping.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/Encoding</c> is neither a name nor a dictionary, when
    ///     <c>/Encoding/BaseEncoding</c> is not a name, when <c>/Encoding/Differences</c> is not
    ///     an array, when a <c>/Differences</c> array entry is neither a number nor a name, when a
    ///     <c>/Differences</c> name appears before any starting code, or when a
    ///     <c>/Differences</c> name is not a recognized glyph name (see
    ///     <see cref="StandardGlyphNames"/>'s own remarks for the covered name set).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the named base encoding is neither <c>/WinAnsiEncoding</c>,
    ///     <c>/MacRomanEncoding</c>, nor <c>/StandardEncoding</c>.
    /// </exception>
    private IReadOnlyDictionary<int, int> ResolveEncoding(PdfObject? encodingEntry)
    {
        var table = (int[])WinAnsiEncodingTable.Clone();
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
    ///     codepoint via <see cref="StandardGlyphNames"/> (a simple/composite font's own
    ///     <c>/Differences</c> entries name Adobe-Glyph-List glyphs, unlike a Type 3 font's own
    ///     <c>/CharProcs</c>-keyed glyph names - see <c>ResolveType3Encoding</c> in
    ///     <c>PdfDocument.Fonts.Type3.cs</c>, which reuses <see cref="ParseDifferences"/> directly
    ///     without this codepoint-resolution step).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="differences"/> is not an array, when an array entry is
    ///     neither a number nor a name, when a name entry appears before any starting code (or
    ///     after the code has advanced past 255), or when a name is not a recognized glyph name.
    /// </exception>
    private void ApplyDifferences(PdfObject differences, int[] table) =>
        ParseDifferences(differences, (code, name) =>
        {
            if (!StandardGlyphNames.TryGetValue(name, out var codepoint))
            {
                throw new InvalidDataException(
                    $"/Encoding/Differences references unrecognized glyph name '/{name}'.");
            }

            table[code] = codepoint;
        });

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
    ///     name ever defined) - a glyph name outside this set encountered in a <c>/Differences</c>
    ///     array is a malformed/unsupported reference to an undefined name from this
    ///     implementation's point of view, and <see cref="ApplyDifferences"/> throws
    ///     <see cref="InvalidDataException"/> for it (a fail-closed policy, not a silent
    ///     mis-mapping to codepoint <c>0</c>/<c>.notdef</c>).
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
    ///     The reverse mapping (Unicode codepoint to glyph name) built once from
    ///     <see cref="StandardGlyphNames"/>, passed as <see cref="Fonts.TrueTypeFont.LoadType1"/>'s
    ///     <c>codepointToGlyphName</c> argument by <see cref="LoadType1Font"/> (see
    ///     <c>PdfDocument.Fonts.Type1.cs</c>) - reusing this class's existing Adobe-glyph-name
    ///     vocabulary rather than introducing a second, separately-maintained glyph-name table.
    /// </summary>
    /// <remarks>
    ///     When more than one glyph name in <see cref="StandardGlyphNames"/> maps to the same
    ///     codepoint (for example a hypothetical synonym pair), the first one encountered (in
    ///     <see cref="StandardGlyphNames"/>'s own declaration order) wins - the same "first-wins
    ///     on collision" convention <c>Fonts.Type1StandardGlyphNames</c> uses for its own reverse
    ///     map.
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
}

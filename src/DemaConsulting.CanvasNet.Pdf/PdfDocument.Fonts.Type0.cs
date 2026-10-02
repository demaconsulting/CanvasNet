// cspell:ignore cidfonttype fontfile
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     A composite (two-byte-code) <c>/Subtype /Type0</c> font, fully resolved from its
    ///     <c>/Resources/Font</c> dictionary: the loaded descendant-font <see cref="Fonts.TrueTypeFont"/>,
    ///     its code (CID, since only <c>/Encoding /Identity-H</c> is supported - see
    ///     <see cref="BuildResolvedCompositeFont"/>) to glyph-index map, and its CID-to-declared-
    ///     advance-width table.
    /// </summary>
    /// <remarks>
    ///     Instances are built once by <see cref="BuildResolvedCompositeFont"/> and cached by
    ///     <see cref="ResolveFont"/> in <see cref="_fontCache"/>, exactly like
    ///     <see cref="ResolvedSimpleFont"/> - see that type's own remarks for the cache's scope.
    ///     The descendant font may be a <c>CIDFontType2</c> (TrueType-outline, embedded
    ///     <c>/FontFile2</c>) or a <c>CIDFontType0</c> (non-CID-keyed-CFF-outline, embedded
    ///     <c>/FontFile3</c> - either SFNT-wrapped or a bare, standalone CFF stream, sniffed from
    ///     the stream's own bytes rather than trusted from its declared <c>/Subtype</c> - see
    ///     <see cref="LoadCidFontType0Font"/>) font - see <see cref="BuildResolvedCompositeFont"/>
    ///     for the exact dispatch and each subtype's scope/non-goals.
    /// </remarks>
    private sealed class ResolvedCompositeFont : IResolvedFont
    {
        /// <summary>Gets the loaded embedded descendant-font TrueType font.</summary>
        internal required TrueTypeFont Font { get; init; }

        /// <summary>
        ///     A composite <c>/Identity-H</c> font always decodes exactly two (big-endian) bytes
        ///     per character code.
        /// </summary>
        int IResolvedFont.CodeByteWidth => 2;

        /// <summary>
        ///     Gets the CID-to-glyph-index map, resolved from the descendant font's
        ///     <c>/CIDToGIDMap</c> entry (the identity map when absent or <c>/Identity</c>, or an
        ///     explicit per-CID table for a stream - see <see cref="ResolveCidToGidMap"/>).
        /// </summary>
        internal required Func<int, int> CidToGid { get; init; }

        /// <summary>
        ///     Gets the descendant font's <c>/DW</c> value (default width, in glyph-space units
        ///     per 1000), used for any CID absent from <see cref="CidWidths"/>.
        /// </summary>
        internal required double DefaultWidth { get; init; }

        /// <summary>
        ///     Gets the CID (since <c>/Encoding /Identity-H</c> maps a code directly to a CID) to
        ///     declared advance width map (in glyph-space units per 1000), resolved from the
        ///     descendant font's <c>/W</c> array.
        /// </summary>
        internal required IReadOnlyDictionary<int, double> CidWidths { get; init; }

        /// <summary>
        ///     Gets the font dictionary's resolved <c>/ToUnicode</c> CMap (see
        ///     <see cref="ResolveToUnicodeMap"/>), or <see langword="null"/> when <c>/ToUnicode</c>
        ///     is absent or malformed. A resolved-but-unconsumed field as of Phase 10 - nothing
        ///     reads this at rendering time; it exists purely as groundwork for a future
        ///     text-extraction feature and Phase 11's composite-font fallback-substitution
        ///     decision.
        /// </summary>
        internal IReadOnlyDictionary<int, int>? ToUnicode { get; init; }

        /// <summary>
        ///     Resolves a two-byte <c>/Identity-H</c> code (which, per the PDF specification, is
        ///     the CID directly - no CMap indirection) to this font and a glyph index via
        ///     <see cref="CidToGid"/>, and its advance width from <see cref="CidWidths"/>,
        ///     falling back to <see cref="DefaultWidth"/> when the CID has no declared width.
        /// </summary>
        public (TrueTypeFont? Font, int GlyphIndex, double Width) Resolve(int code)
        {
            var glyphIndex = CidToGid(code);
            var width = CidWidths.TryGetValue(code, out var declaredWidth) ? declaredWidth : DefaultWidth;
            return (Font, glyphIndex, width / 1000.0);
        }
    }

    /// <summary>
    ///     Builds a <see cref="ResolvedCompositeFont"/> from a <c>/Subtype /Type0</c> font
    ///     dictionary: requires <c>/Encoding /Identity-H</c> and a single-element
    ///     <c>/DescendantFonts</c> array whose sole element is either a
    ///     <c>/Subtype /CIDFontType2</c> dictionary with an embedded
    ///     <c>/FontDescriptor/FontFile2</c> (resolving that descendant's <c>/CIDToGIDMap</c>), or
    ///     a <c>/Subtype /CIDFontType0</c> dictionary with an embedded, non-CID-keyed-CFF
    ///     <c>/FontDescriptor/FontFile3</c> - either SFNT-wrapped or a bare, standalone CFF
    ///     stream (see <see cref="LoadCidFontType0Font"/> - CID is used directly as glyph index,
    ///     identity; any non-standard <c>/CIDToGIDMap</c> on such a descendant is ignored), and
    ///     resolves the descendant's <c>/DW</c>/<c>/W</c> entries either way.
    /// </summary>
    /// <remarks>
    ///     No fallback substitution is attempted for a composite font with no embedded
    ///     descendant-font program (unlike <see cref="BuildResolvedSimpleFont"/>'s
    ///     <see cref="ResolveFallbackFont"/> path) - this is a deliberate, documented Non-Goal of
    ///     this phase: composite fonts are always required to embed their descendant font.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/DescendantFonts</c> is missing, does not resolve to a single-element
    ///     array whose element resolves to a dictionary, when the descendant's
    ///     <c>/FontDescriptor</c> is missing, when a <c>CIDFontType2</c> descendant's
    ///     <c>/FontDescriptor/FontFile2</c> is missing or does not resolve to a stream, when a
    ///     <c>CIDFontType0</c> descendant's <c>/FontDescriptor/FontFile3</c> is missing or does
    ///     not resolve to a stream, or when the embedded font program cannot be parsed (for
    ///     example a CID-keyed CFF program, which <see cref="Fonts.CffTable.Parse"/> rejects).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/Encoding</c> is not the name <c>Identity-H</c> (for example
    ///     <c>Identity-V</c> or a predefined CJK encoding), when the descendant font's
    ///     <c>/Subtype</c> is neither <c>CIDFontType2</c> nor <c>CIDFontType0</c>, or when a
    ///     <c>CIDFontType0</c> descendant's <c>/FontFile3</c> stream's decoded bytes match
    ///     neither a recognized SFNT container nor a structurally plausible bare CFF header (see
    ///     <see cref="LoadCidFontType0Font"/>).
    /// </exception>
    private IResolvedFont BuildResolvedCompositeFont(PdfObject fontDict)
    {
        var encodingEntry = fontDict.Get("Encoding")
            ?? throw new InvalidDataException("Type0 font dictionary is missing required /Encoding.");
        var encoding = Resolve(encodingEntry);
        if (encoding.Kind != PdfKind.Name || encoding.Text != "Identity-H")
        {
            var encodingName = encoding.Kind == PdfKind.Name ? encoding.Text : "(non-name)";
            throw new UnsupportedImageFeatureException(
                $"pdf-font-type0-encoding-{encodingName}",
                $"Type0 font /Encoding '{encodingName}' is not supported; only /Identity-H is " +
                "supported (/Identity-V and predefined CJK encodings are not supported).");
        }

        var descendantFontsEntry = fontDict.Get("DescendantFonts")
            ?? throw new InvalidDataException("Type0 font dictionary is missing required /DescendantFonts.");
        var descendantFonts = Resolve(descendantFontsEntry);
        if (descendantFonts.Kind != PdfKind.Array || descendantFonts.Items.Count != 1)
        {
            throw new InvalidDataException("/DescendantFonts must be an array of exactly one element.");
        }

        var descendantFont = Resolve(descendantFonts.Items[0]);
        if (descendantFont.Kind != PdfKind.Dictionary && descendantFont.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/DescendantFonts element must resolve to a dictionary.");
        }

        var descendantSubtype = GetNameValue(descendantFont, "Subtype");
        if (descendantSubtype != "CIDFontType2" && descendantSubtype != "CIDFontType0")
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-font-cidfonttype-{descendantSubtype ?? "missing"}",
                $"Descendant font /Subtype '{descendantSubtype ?? "(missing)"}' is not " +
                "supported; only /CIDFontType2 and /CIDFontType0 are supported.");
        }

        var descriptorEntry = descendantFont.Get("FontDescriptor")
            ?? throw new InvalidDataException("Descendant font dictionary is missing required /FontDescriptor.");
        var descriptor = Resolve(descriptorEntry);

        var (font, cidToGid) = descendantSubtype switch
        {
            "CIDFontType2" => (LoadCidFontType2Font(descriptor), ResolveCidToGidMap(descendantFont)),
            _ => (LoadCidFontType0Font(descriptor), (Func<int, int>)(cid => cid)),
        };

        var (cidWidths, defaultWidth) = ResolveCompositeWidths(descendantFont);
        var toUnicode = ResolveToUnicodeMap(fontDict);

        return new ResolvedCompositeFont
        {
            Font = font,
            CidToGid = cidToGid,
            DefaultWidth = defaultWidth,
            CidWidths = cidWidths,
            ToUnicode = toUnicode,
        };
    }

    /// <summary>
    ///     Loads a <c>CIDFontType2</c> descendant's embedded <c>/FontDescriptor/FontFile2</c>
    ///     TrueType-outline font program, unchanged from the pre-Phase-12 behavior.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontDescriptor/FontFile2</c> is missing or does not resolve to a
    ///     stream.
    /// </exception>
    private TrueTypeFont LoadCidFontType2Font(PdfObject descriptor)
    {
        var fontFileEntry = descriptor.Get("FontFile2")
            ?? throw new InvalidDataException("Descendant font /FontDescriptor is missing required /FontFile2.");
        var fontFileStream = Resolve(fontFileEntry);
        if (fontFileStream.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/FontDescriptor/FontFile2 does not resolve to a stream.");
        }

        var fontBytes = GetStreamDecodedBytes(fontFileStream);
        return TrueTypeFont.Load(new MemoryStream(fontBytes));
    }

    /// <summary>
    ///     Loads a <c>CIDFontType0</c> descendant's embedded <c>/FontDescriptor/FontFile3</c>
    ///     font program - either an SFNT-wrapped (<c>'OTTO'</c>) CFF program or a bare,
    ///     standalone (<c>/CIDFontType0C</c>-style) CFF program: decodes the stream via
    ///     <see cref="GetStreamDecodedBytes"/>, sniffs its actual byte container shape via
    ///     <see cref="SniffFontFile3Shape"/> (rather than trusting the stream's own declared
    ///     <c>/Subtype</c> name - see this method's own remarks), and dispatches to
    ///     <see cref="Fonts.TrueTypeFont.Load(Stream)"/> (SFNT-wrapped) or
    ///     <see cref="Fonts.TrueTypeFont.LoadType1C"/> (bare CFF, passing
    ///     <see cref="EmptyCodepointToGlyphName"/> since a composite font's CID-to-glyph-index
    ///     resolution never consults the loaded font's own <c>cmap</c>-equivalent lookup - see
    ///     below). Per PDF 32000-1 &#xA7;9.7.4.2, a CID is interpreted directly as a glyph index
    ///     (identity) for a <c>CIDFontType0</c> whose CFF program is not CID-keyed - this method
    ///     does not, and cannot, distinguish that case itself; it is <see cref="Fonts.CffTable.Parse"/>'s
    ///     own CFF parsing that rejects a CID-keyed (<c>ROS</c>-bearing) CFF program by throwing
    ///     <see cref="InvalidDataException"/>, which is allowed to propagate uncaught here -
    ///     consistent with this codebase's "composite fonts fail closed on any embedded-font
    ///     problem, no fallback" convention.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Per PDF 32000-1 &#xA7;9.6.2.2/Table 126, a <c>CIDFontType0</c> descendant's
    ///     <c>/FontFile3</c> stream is supposed to declare its own <c>/Subtype</c> as
    ///     <c>OpenType</c> (an SFNT-wrapped CFF stream) or <c>CIDFontType0C</c> (a bare, standalone
    ///     CFF stream) - but the specification does not actually require that declared name to
    ///     match the stream's real byte container shape, and real-world producers sometimes emit
    ///     exactly that mismatch (for example declaring <c>CIDFontType0C</c> while writing an
    ///     SFNT-wrapped stream, or vice versa). This method therefore never consults the stream's
    ///     own <c>/Subtype</c> at all for dispatch - it sniffs the actual bytes instead,
    ///     mirroring <see cref="LoadType1CFont"/>'s own identical shape-sniffing precedent
    ///     (<c>PdfDocument.Fonts.Type1.cs</c>) for a simple font's <c>/FontFile3</c>. Bytes
    ///     matching neither a recognized SFNT container nor a structurally plausible bare CFF
    ///     header are rejected with <see cref="UnsupportedImageFeatureException"/> rather than
    ///     guessed at.
    ///     </para>
    ///     <para>
    ///     A non-standard <c>/CIDToGIDMap</c> key on the <c>CIDFontType0</c> descendant dictionary
    ///     (not a valid key for this subtype per the PDF specification) is deliberately never
    ///     consulted by this method or its caller.
    ///     </para>
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontDescriptor/FontFile3</c> is missing or does not resolve to a
    ///     stream, or propagated from <see cref="Fonts.TrueTypeFont.Load(Stream)"/>/
    ///     <see cref="Fonts.TrueTypeFont.LoadType1C"/> for a malformed or CID-keyed embedded CFF
    ///     program, or any other malformed embedded SFNT font.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the <c>/FontFile3</c> stream's decoded bytes match neither a recognized
    ///     SFNT container nor a structurally plausible bare CFF header.
    /// </exception>
    private TrueTypeFont LoadCidFontType0Font(PdfObject descriptor)
    {
        var fontFileEntry = descriptor.Get("FontFile3")
            ?? throw new InvalidDataException("Descendant font /FontDescriptor is missing required /FontFile3.");
        var fontFileStream = Resolve(fontFileEntry);
        if (fontFileStream.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/FontDescriptor/FontFile3 does not resolve to a stream.");
        }

        var fontBytes = GetStreamDecodedBytes(fontFileStream);
        return SniffFontFile3Shape(fontBytes) switch
        {
            FontFile3Shape.Sfnt => TrueTypeFont.Load(new MemoryStream(fontBytes)),
            FontFile3Shape.BareCff => TrueTypeFont.LoadType1C(new MemoryStream(fontBytes), EmptyCodepointToGlyphName),
            _ => throw new UnsupportedImageFeatureException(
                "pdf-font-fontfile3-unrecognized-shape",
                "/FontFile3 stream's decoded bytes are neither a recognized SFNT container " +
                "(TrueType/OpenType/CFF) nor a structurally plausible bare Type1C/CFF program " +
                $"(declared /Subtype '{GetNameValue(fontFileStream, "Subtype") ?? "(missing)"}')."),
        };
    }

    /// <summary>
    ///     Resolves a <c>CIDFontType2</c> descendant dictionary's <c>/CIDToGIDMap</c> entry into a
    ///     CID-to-glyph-index function: the identity map when absent or the name <c>/Identity</c>,
    ///     or an explicit big-endian <c>uint16</c>-per-CID lookup table when a stream (an
    ///     out-of-range/negative CID maps to glyph <c>0</c>/<c>.notdef</c>, per the PDF
    ///     specification). <c>/CIDToGIDMap</c> is a <c>CIDFontType2</c>-only key per the PDF
    ///     specification - this method is never called for a <c>CIDFontType0</c> descendant,
    ///     which unconditionally uses the identity CID-to-glyph-index map instead (see
    ///     <see cref="BuildResolvedCompositeFont"/> and <see cref="LoadCidFontType0Font"/>'s own
    ///     remarks).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/CIDToGIDMap</c> resolves to anything other than the name
    ///     <c>Identity</c> or a stream, or when a stream's decoded byte count is odd.
    /// </exception>
    private Func<int, int> ResolveCidToGidMap(PdfObject descendantFont)
    {
        var cidToGidMapEntry = descendantFont.Get("CIDToGIDMap");
        if (cidToGidMapEntry is null)
        {
            return cid => cid;
        }

        var cidToGidMap = Resolve(cidToGidMapEntry);
        if (cidToGidMap.Kind == PdfKind.Name)
        {
            if (cidToGidMap.Text != "Identity")
            {
                throw new InvalidDataException(
                    $"/CIDToGIDMap name '/{cidToGidMap.Text}' is not supported; only /Identity is supported.");
            }

            return cid => cid;
        }

        if (cidToGidMap.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/CIDToGIDMap must be the name /Identity or a stream.");
        }

        var bytes = GetStreamDecodedBytes(cidToGidMap);
        if (bytes.Length % 2 != 0)
        {
            throw new InvalidDataException("/CIDToGIDMap stream byte length must be a multiple of 2.");
        }

        var table = new int[bytes.Length / 2];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (bytes[i * 2] << 8) | bytes[(i * 2) + 1];
        }

        return cid => cid >= 0 && cid < table.Length ? table[cid] : 0;
    }

    /// <summary>
    ///     The maximum number of CIDs a single <c>/W</c> array <c>cFirst cLast w</c> range-form
    ///     entry may expand, bounding a crafted range (for example <c>[0 999999999 500]</c>) from
    ///     looping effectively unbounded and exhausting memory. Generous for any real CID-keyed
    ///     font, which rarely exceeds the 16-bit CID space in practice for a single contiguous
    ///     <c>/W</c> range.
    /// </summary>
    private const int MaxCompositeWidthRangeSpan = 65536;

    /// <summary>
    ///     Resolves a descendant CIDFontType2 dictionary's <c>/DW</c> (default width, defaulting
    ///     to <c>1000</c> per the PDF specification when absent) and <c>/W</c> (per-CID declared
    ///     width overrides, supporting both the <c>c [w1 w2 ... wn]</c> individual-width form and
    ///     the <c>cFirst cLast w</c> range form) entries.
    /// </summary>
    /// <returns>
    ///     The CID-to-width map (empty when <c>/W</c> is absent or not an array - a documented
    ///     leniency, since <see cref="ResolvedCompositeFont.Resolve"/> itself falls back to the
    ///     resolved <c>/DW</c> for any CID missing from this map), and the resolved <c>/DW</c>
    ///     value (<c>1000</c> when not declared).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a <c>/W</c> array's shape does not match either documented sub-form (for
    ///     example a starting code not followed by an array or a number, a range form whose third
    ///     element is not a number, a range form whose <c>cLast</c> is less than its
    ///     <c>cFirst</c>, or a range form spanning more than <see cref="MaxCompositeWidthRangeSpan"/>
    ///     CIDs).
    /// </exception>
    private (IReadOnlyDictionary<int, double> CidWidths, double DefaultWidth) ResolveCompositeWidths(PdfObject descendantFont)
    {
        var defaultWidth = 1000.0;
        var dwEntry = descendantFont.Get("DW");
        if (dwEntry is not null && Resolve(dwEntry) is { Kind: PdfKind.Number } dwValue)
        {
            defaultWidth = dwValue.Number;
        }

        var cidWidths = new Dictionary<int, double>();
        var wEntry = descendantFont.Get("W");
        if (wEntry is not null && Resolve(wEntry) is { Kind: PdfKind.Array } wArray)
        {
            var items = wArray.Items;
            var i = 0;
            while (i < items.Count)
            {
                if (Resolve(items[i]) is not { Kind: PdfKind.Number } firstValue)
                {
                    throw new InvalidDataException("/W array entries must start with a CID number.");
                }

                var firstCid = (int)firstValue.Number;
                if (i + 1 >= items.Count)
                {
                    throw new InvalidDataException("/W array is missing the widths for its final entry.");
                }

                var second = Resolve(items[i + 1]);
                if (second.Kind == PdfKind.Array)
                {
                    // c [w1 w2 ... wn] individual-width form.
                    for (var j = 0; j < second.Items.Count; j++)
                    {
                        if (Resolve(second.Items[j]) is { Kind: PdfKind.Number } w)
                        {
                            cidWidths[firstCid + j] = w.Number;
                        }
                    }

                    i += 2;
                }
                else if (second.Kind == PdfKind.Number)
                {
                    // cFirst cLast w range form.
                    var cLast = (int)second.Number;
                    if (i + 2 >= items.Count || Resolve(items[i + 2]) is not { Kind: PdfKind.Number } wValue)
                    {
                        throw new InvalidDataException("/W array's cFirst-cLast-w range form must be followed by a width number.");
                    }

                    if (cLast < firstCid)
                    {
                        throw new InvalidDataException("/W array's cFirst-cLast-w range form must have cLast >= cFirst.");
                    }

                    if ((long)cLast - firstCid > MaxCompositeWidthRangeSpan)
                    {
                        throw new InvalidDataException(
                            $"/W array's cFirst-cLast-w range form spans more than {MaxCompositeWidthRangeSpan} CIDs.");
                    }

                    // Iterate with a long counter rather than an int one: even with a small,
                    // already-validated span, a cLast at or near int.MaxValue would make an
                    // int-typed increment overflow to int.MinValue on the final iteration, which
                    // would still satisfy the loop's exit condition and hang indefinitely.
                    for (var cid = (long)firstCid; cid <= cLast; cid++)
                    {
                        cidWidths[(int)cid] = wValue.Number;
                    }

                    i += 3;
                }
                else
                {
                    throw new InvalidDataException(
                        "/W array entries must be followed by either an array of widths or a cLast/w pair.");
                }
            }
        }

        return (cidWidths, defaultWidth);
    }
}

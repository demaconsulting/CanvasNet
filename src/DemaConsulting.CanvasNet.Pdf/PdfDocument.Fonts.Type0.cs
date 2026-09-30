// cspell:ignore cidfonttype
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
    /// </remarks>
    private sealed class ResolvedCompositeFont : IResolvedFont
    {
        /// <summary>Gets the loaded embedded descendant-font TrueType font.</summary>
        internal required TrueTypeFont Font { get; init; }

        TrueTypeFont IResolvedFont.Font => Font;

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
        ///     the CID directly - no CMap indirection) to a glyph index via
        ///     <see cref="CidToGid"/>, and its advance width from <see cref="CidWidths"/>,
        ///     falling back to <see cref="DefaultWidth"/> when the CID has no declared width.
        /// </summary>
        public (int GlyphIndex, double Width) Resolve(int code)
        {
            var glyphIndex = CidToGid(code);
            var width = CidWidths.TryGetValue(code, out var declaredWidth) ? declaredWidth : DefaultWidth;
            return (glyphIndex, width / 1000.0);
        }
    }

    /// <summary>
    ///     Builds a <see cref="ResolvedCompositeFont"/> from a <c>/Subtype /Type0</c> font
    ///     dictionary: requires <c>/Encoding /Identity-H</c>, a single-element
    ///     <c>/DescendantFonts</c> array whose sole element is a <c>/Subtype /CIDFontType2</c>
    ///     dictionary with an embedded <c>/FontDescriptor/FontFile2</c>, and resolves that
    ///     descendant's <c>/CIDToGIDMap</c> and <c>/DW</c>/<c>/W</c> entries.
    /// </summary>
    /// <remarks>
    ///     No fallback substitution is attempted for a composite font with no embedded
    ///     <c>/FontFile2</c> (unlike <see cref="BuildResolvedSimpleFont"/>'s
    ///     <see cref="ResolveFallbackFont"/> path) - this is a deliberate, documented Non-Goal of
    ///     this phase: composite fonts are always required to embed their descendant font.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/DescendantFonts</c> is missing, does not resolve to a single-element
    ///     array whose element resolves to a dictionary, when the descendant's
    ///     <c>/FontDescriptor</c> is missing, or when <c>/FontDescriptor/FontFile2</c> is missing
    ///     or does not resolve to a stream.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/Encoding</c> is not the name <c>Identity-H</c> (for example
    ///     <c>Identity-V</c> or a predefined CJK encoding), or when the descendant font's
    ///     <c>/Subtype</c> is not <c>CIDFontType2</c> (for example <c>CIDFontType0</c>).
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
        if (descendantSubtype != "CIDFontType2")
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-font-cidfonttype-{descendantSubtype ?? "missing"}",
                $"Descendant font /Subtype '{descendantSubtype ?? "(missing)"}' is not " +
                "supported; only /CIDFontType2 is supported (/CIDFontType0 is not supported).");
        }

        var descriptorEntry = descendantFont.Get("FontDescriptor")
            ?? throw new InvalidDataException("Descendant font dictionary is missing required /FontDescriptor.");
        var descriptor = Resolve(descriptorEntry);

        var fontFileEntry = descriptor.Get("FontFile2")
            ?? throw new InvalidDataException("Descendant font /FontDescriptor is missing required /FontFile2.");
        var fontFileStream = Resolve(fontFileEntry);
        if (fontFileStream.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/FontDescriptor/FontFile2 does not resolve to a stream.");
        }

        var fontBytes = GetStreamDecodedBytes(fontFileStream);
        var font = TrueTypeFont.Load(new MemoryStream(fontBytes));

        var cidToGid = ResolveCidToGidMap(descendantFont);
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
    ///     Resolves a descendant CIDFontType2 dictionary's <c>/CIDToGIDMap</c> entry into a
    ///     CID-to-glyph-index function: the identity map when absent or the name <c>/Identity</c>,
    ///     or an explicit big-endian <c>uint16</c>-per-CID lookup table when a stream (an
    ///     out-of-range/negative CID maps to glyph <c>0</c>/<c>.notdef</c>, per the PDF
    ///     specification).
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
    ///     element is not a number, or a range form whose <c>cLast</c> is less than its
    ///     <c>cFirst</c>).
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

                    for (var cid = firstCid; cid <= cLast; cid++)
                    {
                        cidWidths[cid] = wValue.Number;
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

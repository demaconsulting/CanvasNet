// cspell:ignore cidrange cidchar usecmap findresource defineresource currentdict endcmap bfrange bfchar endbfrange endbfchar begincmap begincodespacerange endcodespacerange tounicode codepoints codespacerange beginbfchar beginbfrange
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     Resolves a font dictionary's optional <c>/ToUnicode</c> CMap stream into a
    ///     code-to-Unicode-codepoint map, by tokenizing its <c>bfchar</c>/<c>bfrange</c>
    ///     operators (see <see cref="PdfTokenizer"/>/<see cref="ParseValue(PdfTokenizer)"/>,
    ///     reused verbatim - no new tokenizer type).
    /// </summary>
    /// <remarks>
    ///     This is deliberately a narrow, <c>bfchar</c>/<c>bfrange</c>-only CMap parser, not a
    ///     general CMap/PostScript interpreter. Every real-world <c>/ToUnicode</c> stream wraps
    ///     its <c>bfchar</c>/<c>bfrange</c> operators in the Adobe CMap/CID-keyed-font
    ///     specification's mandatory PostScript resource-management boilerplate (PDF
    ///     32000-1:2008 &#xA7;9.10.3's own worked example): <c>/CIDInit /ProcSet findresource
    ///     begin ... begincmap ... endcmap CMapName currentdict /CMap defineresource pop end
    ///     end</c>. Every keyword in that boilerplate (<c>begin</c>/<c>end</c>/<c>dict</c>/
    ///     <c>def</c>/<c>findresource</c>/<c>defineresource</c>/<c>pop</c>/<c>currentdict</c>/
    ///     <c>begincmap</c>/<c>endcmap</c>), plus <c>begincodespacerange</c>/
    ///     <c>endcodespacerange</c>, is silently ignored - it carries no mapping-relevant data,
    ///     and a blanket fail-closed rule against every non-<c>bfchar</c>/<c>bfrange</c> keyword
    ///     would reject 100% of real-world <c>/ToUnicode</c> streams. <c>usecmap</c>,
    ///     <c>cidrange</c>, and <c>cidchar</c>, however, <em>are</em> data-bearing operators this
    ///     narrow parser does not implement - silently ignoring them could silently produce a
    ///     wrong/incomplete map, so they instead fail closed with
    ///     <see cref="UnsupportedImageFeatureException"/>. A <c>bfrange</c> whose array
    ///     destination contains a nested array (the CIDSystemInfo-style "array of arrays"
    ///     destination sub-form) is out of scope for the same reason and also fails closed. An
    ///     empty hex/literal-string destination (<c>&lt;&gt;</c>) - seen from real-world
    ///     producers such as WeasyPrint to mean "this code has no single-character Unicode
    ///     equivalent" - is not an unbalanced/malformed block shape, so it is skipped (no mapping
    ///     is added for the affected code(s)) rather than failing closed; see
    ///     <see cref="TryDecodeFirstUtf16CodePoint"/>.
    /// </remarks>
    /// <param name="fontDict">The (already-resolved) font dictionary to query.</param>
    /// <returns>
    ///     The resolved code-to-Unicode-codepoint map, or <see langword="null"/> when
    ///     <c>/ToUnicode</c> is absent or does not resolve to a stream (a malformed/absent
    ///     <c>/ToUnicode</c> is treated leniently as "no map", mirroring
    ///     <see cref="ResolveWidths"/>'s own documented leniency conventions).
    /// </returns>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the CMap stream contains a <c>usecmap</c>, <c>cidrange</c>, or
    ///     <c>cidchar</c> operator (feature <c>pdf-font-tounicode-{operator}</c>), or a
    ///     <c>bfrange</c> array destination containing a nested array (feature
    ///     <c>pdf-font-tounicode-bfrange-array-destination</c>).
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the CMap stream is not lexically well-formed, or contains an unbalanced
    ///     <c>bfchar</c>/<c>bfrange</c> block shape (for example a source/destination operand
    ///     that is not a string, or a destination array element that is not a string or array).
    /// </exception>
    internal IReadOnlyDictionary<int, int>? ResolveToUnicodeMap(PdfObject fontDict)
    {
        var toUnicodeEntry = fontDict.Get("ToUnicode");
        if (toUnicodeEntry is null)
        {
            return null;
        }

        var toUnicode = Resolve(toUnicodeEntry);
        if (toUnicode.Kind != PdfKind.Stream)
        {
            return null;
        }

        var bytes = GetStreamDecodedBytes(toUnicode);
        var tokenizer = new PdfTokenizer(bytes);
        var map = new Dictionary<int, int>();

        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == PdfTokenKind.EndOfFile)
            {
                break;
            }

            if (token.Kind != PdfTokenKind.Keyword)
            {
                // A bare number/string/name/array/dictionary operand outside a recognized
                // bfchar/bfrange block (for example the "12 dict begin" preamble's leading
                // number, or the /CIDSystemInfo dictionary operand of a "def") carries no
                // mapping-relevant data - it is simply discarded, mirroring this method's own
                // "silently ignore non-data-bearing CMap structure" posture.
                continue;
            }

            switch (token.Text)
            {
                case "beginbfchar":
                    ParseBfCharBlock(tokenizer, map);
                    break;

                case "beginbfrange":
                    ParseBfRangeBlock(tokenizer, map);
                    break;

                case "begincodespacerange":
                    SkipCMapBlock(tokenizer, "endcodespacerange");
                    break;

                case "usecmap":
                case "cidrange":
                case "cidchar":
                    throw new UnsupportedImageFeatureException(
                        $"pdf-font-tounicode-{token.Text}",
                        $"/ToUnicode CMap operator '{token.Text}' is not supported; only " +
                        "bfchar/bfrange (plus the standard CMap PostScript wrapper) are supported.");

                default:
                    // Every other keyword (begin/end/dict/def/findresource/defineresource/pop/
                    // currentdict/begincmap/endcmap/anything else) is the mandatory Adobe CMap
                    // PostScript wrapper boilerplate (or otherwise non-data-bearing) - silently
                    // ignored, see this method's own remarks.
                    break;
            }
        }

        return map;
    }

    /// <summary>
    ///     Parses a <c>beginbfchar</c> ... <c>endbfchar</c> block: each pair is a hex-string
    ///     source code and a hex-string or literal-string (UTF-16BE) destination.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the block is unterminated, or a source/destination operand is not a
    ///     string.
    /// </exception>
    private static void ParseBfCharBlock(PdfTokenizer tokenizer, Dictionary<int, int> map)
    {
        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == PdfTokenKind.Keyword && token.Text == "endbfchar")
            {
                return;
            }

            if (token.Kind == PdfTokenKind.EndOfFile)
            {
                throw new InvalidDataException("Unterminated beginbfchar/endbfchar block in /ToUnicode CMap.");
            }

            if (token.Kind is not (PdfTokenKind.HexString or PdfTokenKind.LiteralString))
            {
                throw new InvalidDataException("A bfchar entry's source code must be a string.");
            }

            var code = DecodeBigEndianCode(token.Bytes ?? []);

            var destinationToken = tokenizer.NextToken();
            if (destinationToken.Kind is not (PdfTokenKind.HexString or PdfTokenKind.LiteralString))
            {
                throw new InvalidDataException("A bfchar entry's destination must be a string.");
            }

            // An empty destination string (e.g. "<0003> <>", seen from real-world producers such
            // as WeasyPrint) signals "no single-character Unicode equivalent" for this code - skip
            // it rather than treating it as malformed.
            if (TryDecodeFirstUtf16CodePoint(destinationToken.Bytes ?? [], out var codepoint))
            {
                map[code] = codepoint;
            }
        }
    }

    /// <summary>
    ///     Parses a <c>beginbfrange</c> ... <c>endbfrange</c> block: each triple is a
    ///     <c>srcLo</c>/<c>srcHi</c> hex-string pair plus a destination that is either a single
    ///     hex/literal string (consecutive codes map to consecutive incrementing codepoints
    ///     starting at that destination's codepoint) or an array of per-code hex/literal-string
    ///     destinations (one element per code in <c>[srcLo, srcHi]</c>).
    /// </summary>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the destination array contains a nested array (the out-of-scope
    ///     CIDSystemInfo-style "array of arrays" destination sub-form).
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the block is unterminated, <c>srcLo</c>/<c>srcHi</c> is not a string, the
    ///     destination is not a string or array, or a destination array element is not a string.
    /// </exception>
    private static void ParseBfRangeBlock(PdfTokenizer tokenizer, Dictionary<int, int> map)
    {
        while (true)
        {
            var loToken = tokenizer.NextToken();
            if (loToken.Kind == PdfTokenKind.Keyword && loToken.Text == "endbfrange")
            {
                return;
            }

            if (loToken.Kind == PdfTokenKind.EndOfFile)
            {
                throw new InvalidDataException("Unterminated beginbfrange/endbfrange block in /ToUnicode CMap.");
            }

            if (loToken.Kind is not (PdfTokenKind.HexString or PdfTokenKind.LiteralString))
            {
                throw new InvalidDataException("A bfrange entry's srcLo must be a string.");
            }

            var hiToken = tokenizer.NextToken();
            if (hiToken.Kind is not (PdfTokenKind.HexString or PdfTokenKind.LiteralString))
            {
                throw new InvalidDataException("A bfrange entry's srcHi must be a string.");
            }

            var srcLo = DecodeBigEndianCode(loToken.Bytes ?? []);
            var srcHi = DecodeBigEndianCode(hiToken.Bytes ?? []);

            var destinationToken = tokenizer.NextToken();
            if (destinationToken.Kind is PdfTokenKind.HexString or PdfTokenKind.LiteralString)
            {
                // A single hex/literal-string destination: consecutive codes map to consecutive
                // incrementing codepoints starting at the destination's own first codepoint. An
                // empty destination string means the whole range has no Unicode equivalent - skip
                // it rather than treating it as malformed.
                if (TryDecodeFirstUtf16CodePoint(destinationToken.Bytes ?? [], out var startCodepoint))
                {
                    for (var code = srcLo; code <= srcHi; code++)
                    {
                        map[code] = startCodepoint + (code - srcLo);
                    }
                }

                continue;
            }

            if (destinationToken.Kind == PdfTokenKind.ArrayStart)
            {
                // An array-of-per-code destinations - one hex/literal-string element per code in
                // [srcLo, srcHi]. A nested array element is the out-of-scope CIDSystemInfo-style
                // "array of arrays" destination sub-form.
                var code = srcLo;
                while (true)
                {
                    var elementToken = tokenizer.NextToken();
                    if (elementToken.Kind == PdfTokenKind.ArrayEnd)
                    {
                        break;
                    }

                    if (elementToken.Kind == PdfTokenKind.EndOfFile)
                    {
                        throw new InvalidDataException("Unterminated bfrange destination array in /ToUnicode CMap.");
                    }

                    if (elementToken.Kind == PdfTokenKind.ArrayStart)
                    {
                        throw new UnsupportedImageFeatureException(
                            "pdf-font-tounicode-bfrange-array-destination",
                            "A bfrange destination array containing a nested array (the " +
                            "CIDSystemInfo-style destination sub-form) is not supported.");
                    }

                    if (elementToken.Kind is not (PdfTokenKind.HexString or PdfTokenKind.LiteralString))
                    {
                        throw new InvalidDataException("A bfrange destination array element must be a string.");
                    }

                    // An empty destination string means this specific code has no Unicode
                    // equivalent - skip mapping it rather than treating it as malformed.
                    if (TryDecodeFirstUtf16CodePoint(elementToken.Bytes ?? [], out var elementCodepoint))
                    {
                        map[code] = elementCodepoint;
                    }

                    code++;
                }

                continue;
            }

            throw new InvalidDataException("A bfrange entry's destination must be a string or an array.");
        }
    }

    /// <summary>
    ///     Discards every token from the tokenizer's current position up to and including the
    ///     given ending keyword, tolerating (but ignoring) any nested content - used for
    ///     <c>begincodespacerange</c>/<c>endcodespacerange</c>, which carries no
    ///     <c>bfchar</c>/<c>bfrange</c>-mapping-relevant data.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the block is unterminated.</exception>
    private static void SkipCMapBlock(PdfTokenizer tokenizer, string endKeyword)
    {
        while (true)
        {
            var token = tokenizer.NextToken();
            if (token.Kind == PdfTokenKind.EndOfFile)
            {
                throw new InvalidDataException($"Unterminated CMap block (expected '{endKeyword}').");
            }

            if (token.Kind == PdfTokenKind.Keyword && token.Text == endKeyword)
            {
                return;
            }
        }
    }

    /// <summary>
    ///     Decodes a source-code string's raw bytes as a single big-endian integer (mirroring
    ///     <see cref="ResolveCidToGidMap"/>'s own big-endian decode idiom, generalized to any
    ///     byte count, since a CMap's own <c>codespacerange</c> may declare a 1-, 2-, or wider
    ///     byte source-code width).
    /// </summary>
    private static int DecodeBigEndianCode(byte[] bytes)
    {
        var value = 0;
        foreach (var b in bytes)
        {
            value = (value << 8) | b;
        }

        return value;
    }

    /// <summary>
    ///     Attempts to decode a destination string's raw bytes as UTF-16BE, returning only its
    ///     first decoded UTF-16 code unit's codepoint.
    /// </summary>
    /// <remarks>
    ///     A <c>bfchar</c>/<c>bfrange</c> destination may legitimately decode to more than one
    ///     UTF-16 code unit (for example a ligature mapping to a multi-character string) - this
    ///     is a deliberate, documented simplification (per this phase's scope): only the first
    ///     codepoint is kept, since <see cref="ResolvedCompositeFont.ToUnicode"/> is a
    ///     resolved-but-unconsumed groundwork field this phase, not a full multi-codepoint
    ///     text-extraction map.
    /// </remarks>
    /// <returns>
    ///     <see langword="false"/> (with <paramref name="codepoint"/> left at zero) when
    ///     <paramref name="bytes"/> is empty - real-world producers (for example WeasyPrint) emit
    ///     an empty destination string (<c>&lt;&gt;</c>) to mean "this code has no single-character
    ///     Unicode equivalent", which is skipped rather than treated as malformed. Otherwise
    ///     <see langword="true"/>.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the destination string is non-empty but has an odd byte count (not a
    ///     valid UTF-16BE encoding of a whole number of code units).
    /// </exception>
    private static bool TryDecodeFirstUtf16CodePoint(byte[] bytes, out int codepoint)
    {
        if (bytes.Length == 0)
        {
            codepoint = 0;
            return false;
        }

        if (bytes.Length % 2 != 0)
        {
            throw new InvalidDataException(
                "A non-empty bfchar/bfrange destination string must be an even-length UTF-16BE encoding.");
        }

        codepoint = (bytes[0] << 8) | bytes[1];
        return true;
    }
}

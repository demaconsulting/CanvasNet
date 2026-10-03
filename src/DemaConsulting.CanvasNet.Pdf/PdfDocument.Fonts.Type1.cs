// cspell:ignore fontfile
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     Loads a simple font's embedded <c>/FontDescriptor/FontFile</c> classic PostScript
    ///     Type 1 font program: requires the stream itself (not the font descriptor) to declare
    ///     numeric <c>/Length1</c>/<c>/Length2</c> entries (the cleartext/encrypted segment byte
    ///     counts <see cref="Fonts.TrueTypeFont.LoadType1"/> needs), decodes the stream via
    ///     <see cref="GetStreamDecodedBytes"/>, and loads it via
    ///     <see cref="Fonts.TrueTypeFont.LoadType1"/>, passing <paramref name="codepointToGlyphName"/>
    ///     as the codepoint-to-glyph-name encoding - this specific font dictionary's own enriched
    ///     map (see <see cref="BuildEmbeddedFontGlyphNameMap"/>'s own remarks for why it can differ,
    ///     per codepoint, from this class's generic <see cref="CodepointToStandardGlyphName"/>
    ///     Adobe-glyph-name vocabulary that seeds it).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Per PDF 32000-1 &#xA7;9.9, a Type 1 font program's own <c>/Length1</c>/<c>/Length2</c>
    ///     byte counts are declared on the <c>/FontFile</c> stream dictionary itself, not on the
    ///     font descriptor that references it - a subtle, easy-to-get-backwards detail this method
    ///     deliberately reads from <paramref name="descriptor"/>'s resolved <c>/FontFile</c>
    ///     stream object, never from <paramref name="descriptor"/> directly.
    ///     </para>
    ///     <para>
    ///     Any <see cref="InvalidDataException"/> <see cref="Fonts.TrueTypeFont.LoadType1"/> itself
    ///     throws for a malformed <c>eexec</c>-encrypted segment, an unparsable
    ///     <c>/CharStrings</c>/<c>/Subrs</c> dictionary, or a rejected <c>seac</c> charstring is
    ///     allowed to propagate uncaught here - consistent with this codebase's "embedded fonts
    ///     fail closed on any embedded-font problem, no fallback" convention, the same convention
    ///     <c>LoadCidFontType0Font</c> documents for the composite-font CFF path
    ///     (<c>PdfDocument.Fonts.Type0.cs</c>).
    ///     </para>
    /// </remarks>
    /// <param name="descriptor">The resolved <c>/FontDescriptor</c> dictionary.</param>
    /// <param name="codepointToGlyphName">
    ///     The codepoint-to-glyph-name map used to build the loaded font's synthetic
    ///     <c>cmap</c>-equivalent lookup - this font dictionary's own enriched map from
    ///     <see cref="BuildEmbeddedFontGlyphNameMap"/>, not necessarily
    ///     <see cref="CodepointToStandardGlyphName"/> itself.
    /// </param>
    /// <returns>The loaded <see cref="Fonts.TrueTypeFont"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontDescriptor/FontFile</c> is missing or does not resolve to a
    ///     stream, when the <c>/FontFile</c> stream's own <c>/Length1</c> or <c>/Length2</c> entry
    ///     is missing or does not resolve to a number, or propagated from
    ///     <see cref="Fonts.TrueTypeFont.LoadType1"/> for a malformed embedded Type 1 program.
    /// </exception>
    private TrueTypeFont LoadType1Font(PdfObject descriptor, IReadOnlyDictionary<int, string> codepointToGlyphName)
    {
        var fontFileEntry = descriptor.Get("FontFile")
            ?? throw new InvalidDataException("Descriptor /FontDescriptor is missing required /FontFile.");
        var fontFileStream = Resolve(fontFileEntry);
        if (fontFileStream.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/FontDescriptor/FontFile does not resolve to a stream.");
        }

        var length1Entry = fontFileStream.Get("Length1")
            ?? throw new InvalidDataException("/FontDescriptor/FontFile is missing required /Length1.");
        if (Resolve(length1Entry) is not { Kind: PdfKind.Number } length1Value)
        {
            throw new InvalidDataException("/FontDescriptor/FontFile's /Length1 does not resolve to a number.");
        }

        var length2Entry = fontFileStream.Get("Length2")
            ?? throw new InvalidDataException("/FontDescriptor/FontFile is missing required /Length2.");
        if (Resolve(length2Entry) is not { Kind: PdfKind.Number } length2Value)
        {
            throw new InvalidDataException("/FontDescriptor/FontFile's /Length2 does not resolve to a number.");
        }

        var fontBytes = GetStreamDecodedBytes(fontFileStream);
        return TrueTypeFont.LoadType1(
            new MemoryStream(fontBytes), (int)length1Value.Number, (int)length2Value.Number, codepointToGlyphName);
    }

    /// <summary>
    ///     Loads a simple font's embedded <c>/FontDescriptor/FontFile3</c> font program -
    ///     either a bare Type1C (standalone, non-SFNT-wrapped CFF) program or an SFNT-wrapped
    ///     (<c>'OTTO'</c>) CFF program: decodes the stream via <see cref="GetStreamDecodedBytes"/>,
    ///     sniffs its actual byte container shape via <see cref="SniffFontFile3Shape"/> (rather
    ///     than trusting the stream's own declared <c>/Subtype</c> name - see this method's own
    ///     remarks), and dispatches to <see cref="Fonts.TrueTypeFont.LoadType1C"/> (bare CFF) or
    ///     <see cref="Fonts.TrueTypeFont.Load(Stream)"/> (SFNT-wrapped) accordingly, passing
    ///     <paramref name="codepointToGlyphName"/> as the bare-CFF codepoint-to-glyph-name
    ///     encoding - exactly as <see cref="LoadType1Font"/> does for the classic <c>/FontFile</c>
    ///     path (see that method's own remarks and <see cref="BuildEmbeddedFontGlyphNameMap"/>'s
    ///     own remarks for how this font-dict-specific map differs from the generic
    ///     <see cref="CodepointToStandardGlyphName"/> vocabulary that seeds it).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Per PDF 32000-1 &#xA7;9.6.2.2/Table 126, a simple <c>/Type1</c> font's
    ///     <c>/FontFile3</c> stream is supposed to declare its own <c>/Subtype</c> as
    ///     <c>Type1C</c> (a bare CFF stream) or <c>OpenType</c> (an SFNT-wrapped CFF stream) - but
    ///     the specification does not actually require that declared name to match the stream's
    ///     real byte container shape, and real-world producers sometimes emit exactly that
    ///     mismatch (for example declaring <c>OpenType</c> while writing a bare CFF stream, or
    ///     vice versa). This method therefore never consults the stream's own <c>/Subtype</c> at
    ///     all for dispatch - it sniffs the actual bytes instead, mirroring
    ///     <see cref="LoadCidFontType0Font"/>'s own identical shape-sniffing precedent
    ///     (<c>PdfDocument.Fonts.Type0.cs</c>) for a composite font's <c>CIDFontType0</c>
    ///     descendant. Bytes matching neither a recognized SFNT container nor a structurally
    ///     plausible bare CFF header are rejected with
    ///     <see cref="UnsupportedImageFeatureException"/> rather than guessed at.
    ///     </para>
    ///     <para>
    ///     A CID-keyed (<c>ROS</c>-bearing) CFF program - bare or SFNT-wrapped - is rejected
    ///     transitively: this method never itself inspects the CFF Top DICT for <c>ROS</c>, but
    ///     <see cref="Fonts.CffTable.Parse"/> (invoked by both
    ///     <see cref="Fonts.TrueTypeFont.LoadType1C"/> and <see cref="Fonts.TrueTypeFont.Load(Stream)"/>'s
    ///     own CFF-outline branch) already does, throwing <see cref="InvalidDataException"/>,
    ///     which is allowed to propagate uncaught here - the same "embedded fonts fail closed on
    ///     any embedded-font problem, no fallback" convention <see cref="LoadType1Font"/>
    ///     documents for its own <c>/FontFile</c> path.
    ///     </para>
    /// </remarks>
    /// <param name="descriptor">The resolved <c>/FontDescriptor</c> dictionary.</param>
    /// <param name="codepointToGlyphName">
    ///     The codepoint-to-glyph-name map used to build the loaded bare-CFF font's synthetic
    ///     <c>cmap</c>-equivalent lookup - this font dictionary's own enriched map from
    ///     <see cref="BuildEmbeddedFontGlyphNameMap"/>, not necessarily
    ///     <see cref="CodepointToStandardGlyphName"/> itself. Unused for the SFNT-wrapped branch,
    ///     which derives its own glyph lookup from the SFNT container's own <c>cmap</c> table.
    /// </param>
    /// <returns>The loaded <see cref="Fonts.TrueTypeFont"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontDescriptor/FontFile3</c> is missing or does not resolve to a
    ///     stream, or propagated from <see cref="Fonts.TrueTypeFont.LoadType1C"/>/
    ///     <see cref="Fonts.TrueTypeFont.Load(Stream)"/> for a malformed or CID-keyed embedded CFF
    ///     program, or any other malformed embedded SFNT font.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the <c>/FontFile3</c> stream's decoded bytes match neither a recognized
    ///     SFNT container nor a structurally plausible bare CFF header.
    /// </exception>
    private TrueTypeFont LoadType1CFont(PdfObject descriptor, IReadOnlyDictionary<int, string> codepointToGlyphName)
    {
        var fontFileEntry = descriptor.Get("FontFile3")
            ?? throw new InvalidDataException("Descriptor /FontDescriptor is missing required /FontFile3.");
        var fontFileStream = Resolve(fontFileEntry);
        if (fontFileStream.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/FontDescriptor/FontFile3 does not resolve to a stream.");
        }

        var fontBytes = GetStreamDecodedBytes(fontFileStream);
        return SniffFontFile3Shape(fontBytes) switch
        {
            FontFile3Shape.Sfnt => TrueTypeFont.Load(new MemoryStream(fontBytes)),
            FontFile3Shape.BareCff => TrueTypeFont.LoadType1C(new MemoryStream(fontBytes), codepointToGlyphName),
            _ => throw new UnsupportedImageFeatureException(
                "pdf-font-fontfile3-unrecognized-shape",
                "/FontFile3 stream's decoded bytes are neither a recognized SFNT container " +
                "(TrueType/OpenType/CFF) nor a structurally plausible bare Type1C/CFF program " +
                $"(declared /Subtype '{GetNameValue(fontFileStream, "Subtype") ?? "(missing)"}')."),
        };
    }
}

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
    ///     <see cref="Fonts.TrueTypeFont.LoadType1"/>, passing <see cref="CodepointToStandardGlyphName"/>
    ///     as the codepoint-to-glyph-name encoding (this class's own existing Adobe-glyph-name
    ///     vocabulary, reused rather than duplicated - see that field's own remarks).
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
    /// <returns>The loaded <see cref="Fonts.TrueTypeFont"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontDescriptor/FontFile</c> is missing or does not resolve to a
    ///     stream, when the <c>/FontFile</c> stream's own <c>/Length1</c> or <c>/Length2</c> entry
    ///     is missing or does not resolve to a number, or propagated from
    ///     <see cref="Fonts.TrueTypeFont.LoadType1"/> for a malformed embedded Type 1 program.
    /// </exception>
    private TrueTypeFont LoadType1Font(PdfObject descriptor)
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
            new MemoryStream(fontBytes), (int)length1Value.Number, (int)length2Value.Number, CodepointToStandardGlyphName);
    }

    /// <summary>
    ///     Loads a simple font's embedded <c>/FontDescriptor/FontFile3</c> bare Type1C (CFF, no
    ///     SFNT/OpenType wrapper) font program: requires the stream itself to declare its own
    ///     <c>/Subtype</c> as the name <c>Type1C</c>, decodes the stream via
    ///     <see cref="GetStreamDecodedBytes"/>, and loads it via
    ///     <see cref="Fonts.TrueTypeFont.LoadType1C"/>, passing
    ///     <see cref="CodepointToStandardGlyphName"/> as the codepoint-to-glyph-name encoding -
    ///     exactly as <see cref="LoadType1Font"/> does for the classic <c>/FontFile</c> path (see
    ///     that method's own remarks for why no font-dict-specific <c>/Encoding</c> resolution is
    ///     needed here either).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Mirrors <c>LoadCidFontType0Font</c>'s own <c>/FontFile3</c> <c>/Subtype</c> validation
    ///     precedent (<c>PdfDocument.Fonts.Type0.cs</c>) exactly, but for the opposite expected
    ///     value: a simple <c>/Type1</c> font's <c>/FontFile3</c> must declare <c>/Subtype
    ///     /Type1C</c> (a bare CFF stream), never <c>/OpenType</c> (an SFNT-wrapped CFF stream,
    ///     which <see cref="LoadCidFontType0Font"/> requires instead, for composite fonts only).
    ///     Any other <c>/Subtype</c> value, or an absent one, is rejected with
    ///     <see cref="UnsupportedImageFeatureException"/> rather than guessed at.
    ///     </para>
    ///     <para>
    ///     A CID-keyed (<c>ROS</c>-bearing) Type1C program is rejected transitively: this method
    ///     never itself inspects the CFF Top DICT for <c>ROS</c>, but
    ///     <see cref="Fonts.CffTable.Parse"/> (invoked by <see cref="Fonts.TrueTypeFont.LoadType1C"/>)
    ///     already does, throwing <see cref="InvalidDataException"/>, which is allowed to
    ///     propagate uncaught here - the same "embedded fonts fail closed on any embedded-font
    ///     problem, no fallback" convention <see cref="LoadType1Font"/> documents for its own
    ///     <c>/FontFile</c> path.
    ///     </para>
    /// </remarks>
    /// <param name="descriptor">The resolved <c>/FontDescriptor</c> dictionary.</param>
    /// <returns>The loaded <see cref="Fonts.TrueTypeFont"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/FontDescriptor/FontFile3</c> is missing or does not resolve to a
    ///     stream, or propagated from <see cref="Fonts.TrueTypeFont.LoadType1C"/> for a malformed
    ///     or CID-keyed embedded CFF program.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the <c>/FontFile3</c> stream's own <c>/Subtype</c> is not the name
    ///     <c>Type1C</c> (for example <c>OpenType</c>, or when the key is absent entirely).
    /// </exception>
    private TrueTypeFont LoadType1CFont(PdfObject descriptor)
    {
        var fontFileEntry = descriptor.Get("FontFile3")
            ?? throw new InvalidDataException("Descriptor /FontDescriptor is missing required /FontFile3.");
        var fontFileStream = Resolve(fontFileEntry);
        if (fontFileStream.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/FontDescriptor/FontFile3 does not resolve to a stream.");
        }

        var fontFileSubtype = GetNameValue(fontFileStream, "Subtype");
        if (fontFileSubtype != "Type1C")
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-font-fontfile3-subtype-{fontFileSubtype ?? "missing"}",
                $"/FontFile3 /Subtype '{fontFileSubtype ?? "(missing)"}' is not supported; only " +
                "/Type1C is supported for a simple /Type1 font's /FontFile3 (an SFNT-wrapped " +
                "/OpenType CFF stream is not supported here).");
        }

        var fontBytes = GetStreamDecodedBytes(fontFileStream);
        return TrueTypeFont.LoadType1C(new MemoryStream(fontBytes), CodepointToStandardGlyphName);
    }
}

// cspell:ignore fontfile
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
}

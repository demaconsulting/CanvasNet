// cspell:ignore Zapf ZapfDingbats registerserif copyrightserif trademarkserif radicalex Noto
using System.Reflection;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Spot-checks representative, known-correct entries of the two new PDF 32000-1 Appendix D
///     "Symbol Set and ZapfDingbats Encoding" code-to-Unicode-codepoint tables
///     (<c>SymbolEncodingTable</c>/<c>ZapfDingbatsEncodingTable</c> in
///     <c>PdfDocument.Fonts.cs</c>), rather than re-asserting the entire 256-entry table
///     verbatim (which would be a tautological "test" proving nothing beyond a successful
///     copy-paste). Accessed via reflection since both tables are <c>private static</c> fields -
///     there is no existing precedent in this test suite for exposing such data publicly purely
///     for testing, and the plan deliberately favors reflection over widening the fields'
///     accessibility.
/// </summary>
public class PdfDocumentSymbolicEncodingTests
{
    private static int[] GetEncodingTable(string fieldName)
    {
        var field = typeof(PdfDocument).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(nameof(PdfDocument), fieldName);
        return (int[])field.GetValue(null)!;
    }

    /// <summary>
    ///     Proves <c>SymbolEncodingTable</c> correctly maps a representative sample of Symbol
    ///     font codes to their known-correct Unicode codepoints, per PDF 32000-1 Appendix D:
    ///     <c>alpha</c> (lowercase Greek alpha, code <c>0x61</c>) to U+03B1, <c>Alpha</c>
    ///     (uppercase Greek Alpha, code <c>0x41</c>) to U+0391, the Private-Use-Area-fallback
    ///     glyph name <c>registerserif</c> (code <c>0xD2</c>) to the plain/generic U+00AE
    ///     ("registered sign") rather than a Private-Use-Area codepoint, and the deliberately
    ///     unmapped extensible-delimiter-piece glyph name <c>radicalex</c> (code <c>0x60</c>) to
    ///     <c>0</c> (no defined glyph).
    /// </summary>
    [Fact]
    public void SymbolEncodingTable_RepresentativeSample_MapsToKnownCorrectCodepoints()
    {
        var table = GetEncodingTable("SymbolEncodingTable");

        Assert.Equal(256, table.Length);
        Assert.Equal(0x03B1, table[0x61]); // alpha
        Assert.Equal(0x0391, table[0x41]); // Alpha
        Assert.Equal(0x00AE, table[0xD2]); // registerserif -> generic U+00AE, not a PUA codepoint
        Assert.Equal(0x0000, table[0x60]); // radicalex -> deliberately unmapped
    }

    /// <summary>
    ///     Proves <c>ZapfDingbatsEncodingTable</c> correctly maps a representative sample of
    ///     ZapfDingbats font codes to their known-correct Unicode codepoints, per PDF 32000-1
    ///     Appendix D: <c>space</c> (code <c>0x20</c>) to U+0020, <c>a1</c> (the first dingbat
    ///     glyph, code <c>0x21</c>) to U+2701, and the documented, accepted-fidelity-limitation
    ///     uncovered case <c>a120</c> (circled digit one, code <c>0xAC</c>) to U+2460 - the table
    ///     itself still defines this mapping; only the bundled Noto substitute font does not
    ///     cover this particular codepoint.
    /// </summary>
    [Fact]
    public void ZapfDingbatsEncodingTable_RepresentativeSample_MapsToKnownCorrectCodepoints()
    {
        var table = GetEncodingTable("ZapfDingbatsEncodingTable");

        Assert.Equal(256, table.Length);
        Assert.Equal(0x0020, table[0x20]); // space
        Assert.Equal(0x2701, table[0x21]); // a1
        Assert.Equal(0x2460, table[0xAC]); // a120 (circled digit one) - defined, but not substitute-font-covered
    }
}

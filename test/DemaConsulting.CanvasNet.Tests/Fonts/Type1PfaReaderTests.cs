// cspell:ignore eexec sniff
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="Type1PfaReader"/>.
/// </summary>
public class Type1PfaReaderTests
{
    private static (byte[] FontFileBytes, int Length1, int Length2) BuildSimpleFontFileBytes()
    {
        var charstring = new List<byte>();
        SyntheticFontBuilder.WriteType1CharstringNumber(charstring, 0);
        SyntheticFontBuilder.WriteType1CharstringNumber(charstring, 600);
        SyntheticFontBuilder.WriteType1CharstringOperator(charstring, 13); // hsbw
        SyntheticFontBuilder.WriteType1CharstringOperator(charstring, 14); // endchar

        return SyntheticFontBuilder.Type1([(".notdef", [.. charstring])]);
    }

    /// <summary>
    ///     Proves that Type1PfaReader TrySniff RecognizesPercentBangPrefix.
    /// </summary>
    [Fact]
    public void Type1PfaReader_TrySniff_RecognizesPercentBangPrefix()
    {
        Assert.True(Type1PfaReader.TrySniff("%!PS-AdobeFont-1.0"u8.ToArray()));
    }

    /// <summary>
    ///     Proves that Type1PfaReader TrySniff NonPfaData ReturnsFalse.
    /// </summary>
    [Fact]
    public void Type1PfaReader_TrySniff_NonPfaData_ReturnsFalse()
    {
        Assert.False(Type1PfaReader.TrySniff([0x80, 0x01, 0, 0, 0, 0]));
        Assert.False(Type1PfaReader.TrySniff([]));
        Assert.False(Type1PfaReader.TrySniff([(byte)'%']));
    }

    /// <summary>
    ///     Proves that Type1PfaReader Read WellFormedFile ReassemblesLength1AndLength2.
    /// </summary>
    [Fact]
    public void Type1PfaReader_Read_WellFormedFile_ReassemblesLength1AndLength2()
    {
        var program = BuildSimpleFontFileBytes();
        var pfa = SyntheticFontBuilder.Type1Pfa(program.FontFileBytes, program.Length1, program.Length2);

        var (fontFileBytes, length1, length2) = Type1PfaReader.Read(pfa);

        Assert.Equal(program.Length2, length2);
        Assert.Equal(
            program.FontFileBytes.AsSpan(program.Length1, program.Length2).ToArray(),
            fontFileBytes.AsSpan(length1, length2).ToArray());
    }

    /// <summary>
    ///     Proves that Type1PfaReader Read MultiLineWhitespaceWrappedHex ToleratesEmbeddedWhitespace.
    /// </summary>
    [Fact]
    public void Type1PfaReader_Read_MultiLineWhitespaceWrappedHex_ToleratesEmbeddedWhitespace()
    {
        var program = BuildSimpleFontFileBytes();
        var pfa = SyntheticFontBuilder.Type1Pfa(program.FontFileBytes, program.Length1, program.Length2, hexLineWidth: 4);

        var (fontFileBytes, length1, length2) = Type1PfaReader.Read(pfa);

        Assert.Equal(program.Length2, length2);
        Assert.Equal(
            program.FontFileBytes.AsSpan(program.Length1, program.Length2).ToArray(),
            fontFileBytes.AsSpan(length1, length2).ToArray());
    }

    /// <summary>
    ///     Proves that Type1PfaReader Read TrailingZeroPadding IsTrimmed.
    /// </summary>
    [Fact]
    public void Type1PfaReader_Read_TrailingZeroPadding_IsTrimmed()
    {
        var program = BuildSimpleFontFileBytes();
        var pfa = SyntheticFontBuilder.Type1Pfa(program.FontFileBytes, program.Length1, program.Length2);

        var withPadding = new List<byte>(pfa);
        withPadding.RemoveRange(withPadding.Count - 1, 1); // drop the trailing newline added by the helper
        withPadding.AddRange("00000000000000000000000000000000\n"u8.ToArray()); // 17 zero bytes of padding

        var (_, _, length2) = Type1PfaReader.Read([.. withPadding]);

        Assert.Equal(program.Length2, length2);
    }

    /// <summary>
    ///     Proves that Type1PfaReader Read MissingEexecKeyword ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfaReader_Read_MissingEexecKeyword_ThrowsInvalidDataException()
    {
        var data = "%!PS-AdobeFont-1.0: NoEexecHere\n"u8.ToArray();
        Assert.Throws<InvalidDataException>(() => Type1PfaReader.Read(data));
    }

    /// <summary>
    ///     Proves that Type1PfaReader Read OddHexDigitCount ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfaReader_Read_OddHexDigitCount_ThrowsInvalidDataException()
    {
        var data = "%!PS-AdobeFont-1.0\neexec\nABC\n"u8.ToArray(); // 3 hex digits - odd count
        Assert.Throws<InvalidDataException>(() => Type1PfaReader.Read(data));
    }

    /// <summary>
    ///     Proves that Type1PfaReader Read NonHexByteBeforeAnyHexDigit ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfaReader_Read_NonHexByteBeforeAnyHexDigit_ThrowsInvalidDataException()
    {
        var data = "%!PS-AdobeFont-1.0\neexec\nZZ\n"u8.ToArray(); // 'Z' is not a hex digit or whitespace
        Assert.Throws<InvalidDataException>(() => Type1PfaReader.Read(data));
    }

    /// <summary>
    ///     Proves that Type1PfaReader Read ZeroBytesAfterTrim ThrowsInvalidDataException.
    /// </summary>
    [Fact]
    public void Type1PfaReader_Read_ZeroBytesAfterTrim_ThrowsInvalidDataException()
    {
        var data = "%!PS-AdobeFont-1.0\neexec\n0000\n"u8.ToArray(); // decodes to all-zero bytes, trimmed to none
        Assert.Throws<InvalidDataException>(() => Type1PfaReader.Read(data));
    }

    /// <summary>
    ///     Proves that Type1PfaReader Read LowercaseAndUppercaseHexDigits AreBothAccepted.
    /// </summary>
    [Fact]
    public void Type1PfaReader_Read_LowercaseAndUppercaseHexDigits_AreBothAccepted()
    {
        var data = "%!PS-AdobeFont-1.0\neexec\naAbBcC\n"u8.ToArray();
        var (fontFileBytes, length1, length2) = Type1PfaReader.Read(data);

        Assert.Equal(3, length2);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, fontFileBytes.AsSpan(length1, length2).ToArray());
    }
}

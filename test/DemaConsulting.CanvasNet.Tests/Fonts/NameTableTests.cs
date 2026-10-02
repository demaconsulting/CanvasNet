// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="NameTable"/>.
/// </summary>
public class NameTableTests
{
    /// <summary>
    ///     Proves that NameTable Parse WindowsAndMacintoshRecords PrefersWindowsRecord.
    /// </summary>
    [Fact]
    public void NameTable_Parse_WindowsAndMacintoshRecords_PrefersWindowsRecord()
    {
        // Arrange: build a name table with both a Windows and a Macintosh record for nameID 1
        var table = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "Windows Family"),
            new SyntheticFontBuilder.NameRecord(1, 0, 0, 1, "Macintosh Family"),
        ]);

        // Act: parse the name table
        var name = NameTable.Parse(table, 0, table.Length);

        // Assert: the Windows/Unicode BMP record is preferred over the Macintosh record
        Assert.Equal("Windows Family", name.FamilyName);
    }

    /// <summary>
    ///     Proves that NameTable Parse OnlyMacintoshRecord ResolvesMacintoshRecord.
    /// </summary>
    [Fact]
    public void NameTable_Parse_OnlyMacintoshRecord_ResolvesMacintoshRecord()
    {
        // Arrange: build a name table with only a Macintosh record for nameID 1
        var table = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(1, 0, 0, 1, "Macintosh Only Family"),
        ]);

        // Act: parse the name table
        var name = NameTable.Parse(table, 0, table.Length);

        // Assert: the Macintosh record is resolved as a fallback when no Windows record exists
        Assert.Equal("Macintosh Only Family", name.FamilyName);
    }

    /// <summary>
    ///     Proves that NameTable Parse TypographicNamesPresent PreferredOverStandardNames.
    /// </summary>
    [Fact]
    public void NameTable_Parse_TypographicNamesPresent_PreferredOverStandardNames()
    {
        // Arrange: build a name table with both standard (1/2) and typographic (16/17) names
        var table = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "Standard Family"),
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 2, "Standard Subfamily"),
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 16, "Typographic Family"),
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 17, "Typographic Subfamily"),
        ]);

        // Act: parse the name table
        var name = NameTable.Parse(table, 0, table.Length);

        // Assert: the typographic names are preferred over the standard names
        Assert.Equal("Typographic Family", name.FamilyName);
        Assert.Equal("Typographic Subfamily", name.SubfamilyName);
    }

    /// <summary>
    ///     Proves that NameTable Parse TypographicNamesAbsent FallsBackToStandardNames.
    /// </summary>
    [Fact]
    public void NameTable_Parse_TypographicNamesAbsent_FallsBackToStandardNames()
    {
        // Arrange: build a name table with only standard (1/2) names, no typographic (16/17)
        var table = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "Standard Family"),
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 2, "Standard Subfamily"),
        ]);

        // Act: parse the name table
        var name = NameTable.Parse(table, 0, table.Length);

        // Assert: the standard names are resolved when no typographic names are present
        Assert.Equal("Standard Family", name.FamilyName);
        Assert.Equal("Standard Subfamily", name.SubfamilyName);
    }

    /// <summary>
    ///     Proves that NameTable Parse MissingRecord ReturnsNullForThatField.
    /// </summary>
    [Fact]
    public void NameTable_Parse_MissingRecord_ReturnsNullForThatField()
    {
        // Arrange: build a name table with a family name but no PostScript name (nameID 6)
        var table = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "Some Family"),
        ]);

        // Act: parse the name table
        var name = NameTable.Parse(table, 0, table.Length);

        // Assert: the present field resolves, and the missing field is null
        Assert.Equal("Some Family", name.FamilyName);
        Assert.Null(name.PostScriptName);
    }

    /// <summary>
    ///     Proves that NameTable Parse TruncatedTable ReturnsEmpty.
    /// </summary>
    [Fact]
    public void NameTable_Parse_TruncatedTable_ReturnsEmpty()
    {
        // Arrange: a table too short to contain the 6-byte header
        byte[] data = [0, 0, 0, 1];

        // Act: parse the truncated name table
        var name = NameTable.Parse(data, 0, data.Length);

        // Assert: the table is treated as empty (no throw, every field null)
        Assert.Same(NameTable.Empty, name);
    }

    /// <summary>
    ///     Proves that NameTable Parse DeclaredCountTooLargeForBounds ReturnsEmpty.
    /// </summary>
    [Fact]
    public void NameTable_Parse_DeclaredCountTooLargeForBounds_ReturnsEmpty()
    {
        // Arrange: a well-formed 6-byte header declaring far more records than the table can hold
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // format
        SyntheticFontBuilder.WriteUInt16(buf, 1000); // count: absurdly large, does not fit
        SyntheticFontBuilder.WriteUInt16(buf, 6); // stringOffset
        var data = buf.ToArray();

        // Act: parse the malformed name table
        var name = NameTable.Parse(data, 0, data.Length);

        // Assert: the whole table is treated as absent (the header itself is malformed)
        Assert.Same(NameTable.Empty, name);
    }

    /// <summary>
    ///     Proves that NameTable Parse RecordStringOutOfBounds SkipsRecord.
    /// </summary>
    [Fact]
    public void NameTable_Parse_RecordStringOutOfBounds_SkipsRecord()
    {
        // Arrange: hand-build a name table with one well-formed record and one record whose
        // string bytes would fall outside the table's bounds
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // format
        SyntheticFontBuilder.WriteUInt16(buf, 2); // count
        SyntheticFontBuilder.WriteUInt16(buf, 6 + 2 * 12); // stringOffset

        // Record 0: well-formed, resolves "OK"
        SyntheticFontBuilder.WriteUInt16(buf, 3); // platformID
        SyntheticFontBuilder.WriteUInt16(buf, 1); // encodingID
        SyntheticFontBuilder.WriteUInt16(buf, 0x0409); // languageID
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nameID
        SyntheticFontBuilder.WriteUInt16(buf, 4); // length ("OK" as UTF-16BE = 4 bytes)
        SyntheticFontBuilder.WriteUInt16(buf, 0); // offset

        // Record 1: malformed - offset/length reaches far beyond the table's bounds
        SyntheticFontBuilder.WriteUInt16(buf, 3); // platformID
        SyntheticFontBuilder.WriteUInt16(buf, 1); // encodingID
        SyntheticFontBuilder.WriteUInt16(buf, 0x0409); // languageID
        SyntheticFontBuilder.WriteUInt16(buf, 4); // nameID (full name)
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // length: absurdly large
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // offset: absurdly large

        buf.AddRange(System.Text.Encoding.BigEndianUnicode.GetBytes("OK"));
        var data = buf.ToArray();

        // Act: parse the table containing one well-formed and one malformed record
        var name = NameTable.Parse(data, 0, data.Length);

        // Assert: the well-formed record still resolves, and the malformed record is skipped
        // (not resolved, and does not cause the whole table to be discarded)
        Assert.Equal("OK", name.FamilyName);
        Assert.Null(name.FullName);
    }

    /// <summary>
    ///     Proves that NameTable Parse MultipleWindowsLanguages PrefersEnUs.
    /// </summary>
    [Fact]
    public void NameTable_Parse_MultipleWindowsLanguages_PrefersEnUs()
    {
        // Arrange: build a name table with two Windows records for the same nameID, in different
        // languages, en-US listed second
        var table = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0407, 1, "German Family"), // de-DE
            new SyntheticFontBuilder.NameRecord(3, 1, 0x0409, 1, "US English Family"), // en-US
        ]);

        // Act: parse the name table
        var name = NameTable.Parse(table, 0, table.Length);

        // Assert: the en-US record is preferred over the other-language record
        Assert.Equal("US English Family", name.FamilyName);
    }

    /// <summary>
    ///     Proves that NameTable Parse UnsupportedPlatformEncoding IgnoresRecord.
    /// </summary>
    [Fact]
    public void NameTable_Parse_UnsupportedPlatformEncoding_IgnoresRecord()
    {
        // Arrange: build a name table with only a record under an unsupported platform/encoding
        // pair (platform 0, "Unicode direct" - out of scope per this class's documented contract)
        var table = SyntheticFontBuilder.Name(
        [
            new SyntheticFontBuilder.NameRecord(0, 3, 0, 1, "Unicode Direct Family"),
        ]);

        // Act: parse the name table
        var name = NameTable.Parse(table, 0, table.Length);

        // Assert: the unsupported platform/encoding record is ignored entirely
        Assert.Null(name.FamilyName);
    }

    /// <summary>
    ///     Proves that NameTable Empty AllFieldsNull.
    /// </summary>
    [Fact]
    public void NameTable_Empty_AllFieldsNull()
    {
        // Arrange/Act: use the empty name table singleton
        // Assert: every field is null
        Assert.Null(NameTable.Empty.FamilyName);
        Assert.Null(NameTable.Empty.SubfamilyName);
        Assert.Null(NameTable.Empty.FullName);
        Assert.Null(NameTable.Empty.PostScriptName);
    }
}

// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO
// cspell:ignore unwidened
using System.Reflection;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Tests.TestSupport;

namespace DemaConsulting.CanvasNet.Tests.Fonts;

/// <summary>
///     Unit tests for <see cref="KernTable"/>.
/// </summary>
public class KernTableTests
{
    /// <summary>
    ///     Proves that KernTable Format0 KnownPair ReturnsValue.
    /// </summary>
    [Fact]
    public void KernTable_Format0_KnownPair_ReturnsValue()
    {
        // Arrange: build a format-0 kern subtable with two known glyph pairs
        var table = SyntheticFontBuilder.KernFormat0([(3, 4, 120), (5, 6, -30)]);

        // Act: parse the kern table
        var kern = KernTable.Parse(table, 0, table.Length);

        // Assert: both pairs resolve to their configured kerning values
        Assert.Equal(120, kern.GetKerning(3, 4));
        Assert.Equal(-30, kern.GetKerning(5, 6));
    }

    /// <summary>
    ///     Proves that KernTable Format0 UnknownPair ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_Format0_UnknownPair_ReturnsZero()
    {
        // Arrange: build a format-0 kern subtable with a single known glyph pair
        var table = SyntheticFontBuilder.KernFormat0([(3, 4, 120)]);

        // Act: parse the kern table
        var kern = KernTable.Parse(table, 0, table.Length);

        // Assert: pairs absent from the table (including a swapped-order pair) resolve to zero
        Assert.Equal(0, kern.GetKerning(4, 3));
        Assert.Equal(0, kern.GetKerning(99, 100));
    }

    /// <summary>
    ///     Proves that KernTable Empty GetKerningReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_Empty_GetKerningReturnsZero()
    {
        // Arrange/Act: use the empty kern table singleton
        // Assert: any lookup on the empty table resolves to zero
        Assert.Equal(0, KernTable.Empty.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable NoPairs ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_NoPairs_ReturnsZero()
    {
        // Arrange: build a format-0 kern subtable with zero pairs
        var table = SyntheticFontBuilder.KernFormat0([]);

        // Act: parse the kern table
        var kern = KernTable.Parse(table, 0, table.Length);

        // Assert: with no pairs defined, any lookup resolves to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable ZeroLengthTable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_ZeroLengthTable_IsTolerant_ReturnsZero()
    {
        // Arrange/Act: parse a zero-length kern table
        var kern = KernTable.Parse([], 0, 0);

        // Assert: the empty table is tolerated and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable TruncatedTable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_TruncatedTable_IsTolerant_ReturnsZero()
    {
        // Arrange: build a kern table truncated right after declaring one subtable
        byte[] data = [0, 0, 0, 1, 0]; // version=0, numSubtables=1, then truncated

        // Act: parse the truncated kern table
        var kern = KernTable.Parse(data, 0, data.Length);

        // Assert: the truncation is tolerated and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable MalformedPairCount IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_MalformedPairCount_IsTolerant_ReturnsZero()
    {
        // Arrange: build a format-0 kern subtable declaring an nPairs value too large to fit
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // kern table version
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nTables
        SyntheticFontBuilder.WriteUInt16(buf, 0); // subtable version
        SyntheticFontBuilder.WriteUInt16(buf, 14); // subtable length
        SyntheticFontBuilder.WriteUInt16(buf, 0x0001); // coverage: format 0, horizontal
        SyntheticFontBuilder.WriteUInt16(buf, 0xFFFF); // nPairs: absurdly large, does not fit
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);
        SyntheticFontBuilder.WriteUInt16(buf, 0);

        // Act: parse the kern table with the malformed pair count
        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        // Assert: the malformed pair count is tolerated and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable UnsupportedFormat2Subtable IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_UnsupportedFormat2Subtable_IsTolerant_ReturnsZero()
    {
        // Arrange: build a kern subtable declaring the unsupported format-2 coverage
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // kern table version
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nTables
        SyntheticFontBuilder.WriteUInt16(buf, 0); // subtable version
        SyntheticFontBuilder.WriteUInt16(buf, 6); // subtable length (header only)
        SyntheticFontBuilder.WriteUInt16(buf, 0x0201); // coverage: format 2, horizontal

        // Act: parse the kern table with the unsupported subtable format
        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        // Assert: the unsupported format is ignored and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Proves that KernTable SubtableLengthOverflowsPosition IsTolerant ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_SubtableLengthOverflowsPosition_IsTolerant_ReturnsZero()
    {
        // Arrange: choose a huge table offset such that, computed with unwidened 32-bit `int`
        // arithmetic, `pos + subtableLength` (pos = tableOffset + 4, subtableLength = 0xFFFF, the
        // maximum ushort) wraps past int.MaxValue to a large negative value. That wrapped value
        // would incorrectly pass the `subtableEnd > tableEnd` bounds guard, and - because the
        // first subtable's coverage below is deliberately unsupported (format 2), so the loop
        // advances `pos` to that corrupted, wrapped value - drive a subsequent read to a deeply
        // out-of-bounds (negative) array index on the second declared subtable.
        const int subtableLength = 0xFFFF;
        const long overflowSum = (long)int.MaxValue + 1; // wraps to int.MinValue in 32-bit arithmetic
        var tableOffset = (int)(overflowSum - 4 - subtableLength);
        const int tableLength = 20;

        var data = new byte[tableOffset + tableLength];
        WriteUInt16BigEndian(data, tableOffset, 0); // kern table version
        WriteUInt16BigEndian(data, tableOffset + 2, 2); // numSubtables: a second subtable is declared
        WriteUInt16BigEndian(data, tableOffset + 4, 0); // subtable version (unused)
        WriteUInt16BigEndian(data, tableOffset + 6, subtableLength); // subtable length: overflow-prone
        WriteUInt16BigEndian(data, tableOffset + 8, 0x0201); // coverage: format 2 (unsupported), horizontal

        // Act: parse the kern table with the crafted overflow-prone subtable length
        var kern = KernTable.Parse(data, tableOffset, tableLength);

        // Assert: the overflowing subtable length is rejected (rather than wrapping into a
        // corrupted position that would read out of bounds) and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }

    /// <summary>
    ///     Writes a big-endian, unsigned 16-bit integer directly into a raw byte array at the
    ///     given offset (bypasses <see cref="SyntheticFontBuilder"/>'s <c>List&lt;byte&gt;</c>-based
    ///     helpers, which are impractical for the near-2GB sparse array this test requires).
    /// </summary>
    private static void WriteUInt16BigEndian(byte[] data, int offset, int value)
    {
        data[offset] = (byte)((value >> 8) & 0xFF);
        data[offset + 1] = (byte)(value & 0xFF);
    }

    /// <summary>
    ///     Proves that KernTable Format0 BodyOffsetArithmeticOverflow IsRejected.
    /// </summary>
    /// <remarks>
    ///     A genuine `bodyOffset + 8` 32-bit overflow cannot be reached through the public
    ///     <see cref="KernTable.Parse"/> API with a real backing array: any `bodyOffset` value
    ///     large enough to overflow when adding a small constant like 8 would itself have to
    ///     exceed <see cref="Array.MaxLength"/> (a .NET byte array cannot be indexed that far),
    ///     and every earlier bounds check in <see cref="KernTable.Parse"/> is already
    ///     long-widened, so a corrupted/oversized `bodyOffset` can never actually arrive at the
    ///     format-0 body parser. This test therefore exercises the private
    ///     <c>TryParseFormat0</c> helper directly via reflection, to prove its own arithmetic is
    ///     safe in isolation (defense-in-depth), independent of whether current callers can reach
    ///     the overflow-prone input.
    /// </remarks>
    [Fact]
    public void KernTable_Format0_BodyOffsetArithmeticOverflow_IsRejected()
    {
        // Arrange: a bodyOffset chosen so that `bodyOffset + 8`, computed with unwidened 32-bit
        // `int` arithmetic, wraps past int.MaxValue to a negative value - which would incorrectly
        // pass an unwidened `bodyOffset + 8 > subtableEnd` bounds guard - paired with a small,
        // deliberately undersized backing array (so that, pre-fix, the wrongly-accepted bodyOffset
        // drives an out-of-bounds read that throws, rather than being safely rejected).
        const int bodyOffset = int.MaxValue - 3;
        const int subtableEnd = 100;
        var data = new byte[16];

        var method = typeof(KernTable).GetMethod("TryParseFormat0", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act: invoke the format-0 body parser directly with the overflow-prone bodyOffset
        var result = method.Invoke(null, [data, bodyOffset, subtableEnd]);

        // Assert: the overflow-prone bodyOffset is rejected (rather than wrapping to a small
        // value that would be misread as in-bounds and drive an out-of-bounds read) so parsing
        // returns null
        Assert.Null(result);
    }

    /// <summary>
    ///     Proves that KernTable CrossStreamCoverage IsSkipped ReturnsZero.
    /// </summary>
    [Fact]
    public void KernTable_CrossStreamCoverage_IsSkipped_ReturnsZero()
    {
        // Arrange: build a kern subtable declaring the cross-stream coverage bit
        var buf = new List<byte>();
        SyntheticFontBuilder.WriteUInt16(buf, 0); // kern table version
        SyntheticFontBuilder.WriteUInt16(buf, 1); // nTables
        SyntheticFontBuilder.WriteUInt16(buf, 0); // subtable version
        SyntheticFontBuilder.WriteUInt16(buf, 6); // subtable length (header only)
        SyntheticFontBuilder.WriteUInt16(buf, 0x0005); // coverage: format 0, horizontal + cross-stream

        // Act: parse the kern table with the cross-stream subtable
        var data = buf.ToArray();
        var kern = KernTable.Parse(data, 0, data.Length);

        // Assert: the cross-stream subtable is skipped and lookups resolve to zero
        Assert.Equal(0, kern.GetKerning(3, 4));
    }
}

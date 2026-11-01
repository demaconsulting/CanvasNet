using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tests.Codecs;

/// <summary>
///     Unit tests for the internal <see cref="PngFilter"/> scanline-filter reversal shared by
///     <see cref="PngCodec"/> and the PDF PNG predictors.
/// </summary>
public class PngFilterTests
{
    /// <summary>Reverses one filter and returns the reconstructed row.</summary>
    private static byte[] Defilter(byte filterType, byte[] filtered, byte[] previousRow, int bpp)
    {
        var output = new byte[filtered.Length];
        PngFilter.DefilterRow(filterType, filtered, previousRow, output, bpp);
        return output;
    }

    /// <summary>Proves filter type 0 (None) copies the row unchanged.</summary>
    [Fact]
    public void PngFilter_DefilterRow_None_CopiesRow()
    {
        Assert.Equal<byte>([9, 8, 7], Defilter(0, [9, 8, 7], [1, 1, 1], 1));
    }

    /// <summary>Proves filter type 1 (Sub) adds the byte one pixel to the left, using the bytes-per-pixel stride.</summary>
    [Fact]
    public void PngFilter_DefilterRow_Sub_AddsLeftNeighborAtBppStride()
    {
        Assert.Equal<byte>([1, 2, 3, 4], Defilter(1, [1, 1, 1, 1], [0, 0, 0, 0], 1));
        Assert.Equal<byte>([1, 2, 2, 4], Defilter(1, [1, 2, 1, 2], [0, 0, 0, 0], 2));
    }

    /// <summary>Proves filter type 2 (Up) adds the previous row, wrapping modulo 256.</summary>
    [Fact]
    public void PngFilter_DefilterRow_Up_AddsPreviousRowModulo256()
    {
        Assert.Equal<byte>([11, 22, 44], Defilter(2, [1, 2, 200], [10, 20, 100], 1));
    }

    /// <summary>Proves filter type 3 (Average) adds the floor of the mean of the left and upper bytes.</summary>
    [Fact]
    public void PngFilter_DefilterRow_Average_AddsFloorMeanOfLeftAndUp()
    {
        Assert.Equal<byte>([10, 20, 30], Defilter(3, [5, 5, 5], [10, 20, 30], 1));
        Assert.Equal<byte>([1, 2, 6], Defilter(3, [1, 2, 3], [0, 0, 5], 1));
    }

    /// <summary>Proves filter type 4 (Paeth) picks the left, upper, or upper-left predictor per the PNG rules.</summary>
    [Fact]
    public void PngFilter_DefilterRow_Paeth_PicksClosestPredictor()
    {
        // First row (previous row all zero): the predictor is always the left byte
        Assert.Equal<byte>([5, 10, 15], Defilter(4, [5, 5, 5], [0, 0, 0], 1));

        // Upper byte chosen
        Assert.Equal<byte>([11, 21, 31], Defilter(4, [1, 1, 1], [10, 20, 30], 1));

        // Upper-left byte chosen for the second pixel (a=10, b=20, c=15 gives p=15, nearest c)
        Assert.Equal<byte>([10, 16], Defilter(4, [251, 1], [15, 20], 1));
    }

    /// <summary>Proves an undefined filter type is rejected with <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void PngFilter_DefilterRow_UnknownFilterType_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => Defilter(5, [1], [0], 1));
    }
}

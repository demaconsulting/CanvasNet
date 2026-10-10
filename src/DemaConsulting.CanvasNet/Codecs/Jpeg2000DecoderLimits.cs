using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     The resource limits enforced by <see cref="Jpeg2000Codec"/> while decoding untrusted JPEG 2000 data.
/// </summary>
/// <remarks>
///     <para>
///         The limits are checked while the headers are parsed, before any plane, tile, precinct or
///         code-block structure is allocated, so a header of a few hundred bytes cannot force an
///         allocation larger than the limits allow. Exceeding any limit raises
///         <see cref="InvalidDataException"/>.
///     </para>
///     <para>
///         Independently of these absolute limits, the decoder also bounds its structures by the amount
///         of data actually present: a tile cannot declare more packets than its tile-part data has
///         bits (every packet has at least a one-bit header), and the progression-iteration work is
///         bounded by a small constant plus a multiple of the input length.
///     </para>
///     <para>
///         The defaults follow <see cref="Surface.MaxDimension"/>. Callers that know more about the
///         expected image (for example a PDF image dictionary with its own <c>/Width</c> and
///         <c>/Height</c>) can pass tighter limits to
///         <see cref="Jpeg2000Codec.Decode(byte[], Jpeg2000DecoderLimits)"/>.
///     </para>
/// </remarks>
public sealed record Jpeg2000DecoderLimits
{
    /// <summary>Gets the default limits used by <see cref="Jpeg2000Codec.Load(Stream)"/> and <see cref="Jpeg2000Codec.Decode(byte[])"/>.</summary>
    public static Jpeg2000DecoderLimits Default { get; } = new();

    /// <summary>Gets the maximum number of input bytes accepted. The default is 256 MiB.</summary>
    public long MaxInputBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>Gets the maximum image width in pixels. The default is <see cref="Surface.MaxDimension"/>.</summary>
    public int MaxWidth { get; init; } = Surface.MaxDimension;

    /// <summary>Gets the maximum image height in pixels. The default is <see cref="Surface.MaxDimension"/>.</summary>
    public int MaxHeight { get; init; } = Surface.MaxDimension;

    /// <summary>
    ///     Gets the maximum number of samples (width times height times channels) of the decoded image and of
    ///     the component planes held while decoding. The default is 2^27, i.e. 256 MiB of component planes.
    /// </summary>
    public long MaxTotalSamples { get; init; } = 1L << 27;

    /// <summary>Gets the maximum number of component samples of a single tile. The default is 2^26.</summary>
    public long MaxTileSamples { get; init; } = 1L << 26;

    /// <summary>Gets the maximum number of tiles. The default is 65535, the largest number the format can address.</summary>
    public int MaxTiles { get; init; } = 65535;

    /// <summary>Gets the maximum number of precincts of a single tile. The default is 2^18.</summary>
    public long MaxTilePrecincts { get; init; } = 1L << 18;

    /// <summary>Gets the maximum number of code-blocks of a single tile. The default is 2^20.</summary>
    public long MaxTileCodeBlocks { get; init; } = 1L << 20;

    /// <summary>Gets the maximum number of packets (precincts times layers) of a single tile. The default is 2^22.</summary>
    public long MaxTilePackets { get; init; } = 1L << 22;

    /// <summary>
    ///     Gets the maximum number of progression-order entries (POC) in effect for the main header or for a
    ///     tile. The default is 128.
    /// </summary>
    public int MaxProgressionChanges { get; init; } = 128;

    /// <summary>
    ///     Gets the absolute ceiling of progression-iteration steps (candidate packets plus position steps)
    ///     for one decode, cumulative over all tiles and progression changes. The default is 2^30. The
    ///     effective ceiling is lower for small inputs because it also scales with the input length.
    /// </summary>
    public long MaxProgressionSteps { get; init; } = 1L << 30;

    /// <summary>
    ///     Gets the absolute ceiling of entropy-decoding work (sample-passes) for one decode. The default is
    ///     2^34 (at least the largest default image, <see cref="MaxTotalSamples"/> samples at 88 passes, so
    ///     a valid image never reaches it). The effective ceiling is lower for small inputs because it also
    ///     scales with the input length, which is what bounds hostile streams.
    /// </summary>
    public long MaxTier1Work { get; init; } = 1L << 34;

    /// <summary>Checks that every limit is positive and within the range the decoder can honor.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a limit is not positive or is too large.</exception>
    internal void Validate()
    {
        Check(MaxInputBytes, int.MaxValue, nameof(MaxInputBytes));
        Check(MaxWidth, int.MaxValue, nameof(MaxWidth));
        Check(MaxHeight, int.MaxValue, nameof(MaxHeight));
        Check(MaxTotalSamples, int.MaxValue, nameof(MaxTotalSamples));
        Check(MaxTileSamples, int.MaxValue, nameof(MaxTileSamples));
        Check(MaxTiles, 65535, nameof(MaxTiles));
        Check(MaxTilePrecincts, int.MaxValue, nameof(MaxTilePrecincts));
        Check(MaxTileCodeBlocks, int.MaxValue, nameof(MaxTileCodeBlocks));
        Check(MaxTilePackets, int.MaxValue, nameof(MaxTilePackets));
        Check(MaxProgressionChanges, 65535, nameof(MaxProgressionChanges));
        Check(MaxProgressionSteps, long.MaxValue / 2, nameof(MaxProgressionSteps));
        Check(MaxTier1Work, long.MaxValue / 2, nameof(MaxTier1Work));
    }

    private static void Check(long value, long max, string name)
    {
        if (value < 1 || value > max)
        {
            throw new ArgumentOutOfRangeException(name, value, "The limit must be between 1 and " + max + ".");
        }
    }
}

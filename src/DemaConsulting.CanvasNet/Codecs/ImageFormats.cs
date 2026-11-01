using System.Buffers.Binary;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Recognizes the <see cref="ImageFormat"/> of an image from its leading bytes or from its media
///     (MIME) type.
/// </summary>
/// <remarks>
///     <para>
///         Both methods are pure, never throw for malformed input (an unrecognized input yields
///         <see langword="null"/>), and are thread-safe. Recognition is shallow: it looks only at
///         the signature or the media type token and does not validate the rest of the file - the
///         decoder does that.
///     </para>
/// </remarks>
public static class ImageFormats
{
    /// <summary>
    ///     The number of leading bytes that are enough for <see cref="Detect(ReadOnlySpan{byte})"/> to
    ///     recognize every supported format (the longest check is the JPEG 2000 signature box, and the
    ///     bitmap check, which reads the bitmap-header size at offset 14).
    /// </summary>
    internal const int HeaderLength = 32;

    /// <summary>The minimum number of bytes needed to validate a bitmap file header.</summary>
    private const int BmpHeaderLength = 18;

    /// <summary>The bitmap-header sizes (OS/2 core, Windows info header, and V2 to V5) that are accepted as a BMP.</summary>
    private static readonly int[] BmpDibHeaderSizes = [12, 40, 52, 56, 64, 108, 124];

    /// <summary>
    ///     Recognizes the format of an image from the first bytes of its file.
    /// </summary>
    /// <param name="header">
    ///     The first bytes of the file; at least 18 bytes are needed to recognize every format (fewer
    ///     are enough for all but a bitmap, which needs its whole file header checked).
    /// </param>
    /// <returns>The format, or <see langword="null"/> when no supported image signature matches.</returns>
    /// <remarks>
    ///     A PNG, JPEG, GIF, TIFF (either byte order), or JPEG 2000 (JP2 signature box or raw
    ///     codestream) is recognized by its signature. The two-letter bitmap signature <c>BM</c> alone
    ///     would also match text files, so a bitmap is recognized only when its reserved fields (which
    ///     are zero) and the size of the bitmap header that follows are also plausible.
    /// </remarks>
    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ImageFormat.Png;
        }

        if (header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return ImageFormat.Jpeg;
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return ImageFormat.Gif;
        }

        if (header.StartsWith(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || header.StartsWith(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }))
        {
            return ImageFormat.Tiff;
        }

        // JP2 signature box, or a raw codestream (SOC followed by SIZ)
        if (header.StartsWith(new byte[] { 0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A })
            || header.StartsWith(new byte[] { 0xFF, 0x4F, 0xFF, 0x51 }))
        {
            return ImageFormat.Jpeg2000;
        }

        return LooksLikeBmp(header) ? ImageFormat.Bmp : null;
    }

    /// <summary>
    ///     Recognizes the format of an image from its media (MIME) type.
    /// </summary>
    /// <param name="mediaType">
    ///     The media type, for example <c>image/png</c> or <c>Image/JPEG; q=1</c>; <see langword="null"/>
    ///     is allowed. The comparison ignores case, surrounding whitespace, and any <c>;</c> parameters.
    /// </param>
    /// <returns>The format, or <see langword="null"/> for a <see langword="null"/>, empty, or unsupported media type (including SVG and EMF/WMF metafiles).</returns>
    /// <remarks>
    ///     The recognized types are <c>image/png</c>; <c>image/jpeg</c> and <c>image/jpg</c>;
    ///     <c>image/gif</c>; <c>image/bmp</c>, <c>image/x-bmp</c> and <c>image/x-ms-bmp</c>;
    ///     <c>image/tiff</c> and <c>image/x-tiff</c>; and <c>image/jp2</c>.
    /// </remarks>
    public static ImageFormat? FromMediaType(string? mediaType)
    {
        if (mediaType is null)
        {
            return null;
        }

        var semicolon = mediaType.IndexOf(';');
        var token = (semicolon >= 0 ? mediaType[..semicolon] : mediaType).Trim().ToLowerInvariant();
        return token switch
        {
            "image/png" => ImageFormat.Png,
            "image/jpeg" or "image/jpg" => ImageFormat.Jpeg,
            "image/gif" => ImageFormat.Gif,
            "image/bmp" or "image/x-bmp" or "image/x-ms-bmp" => ImageFormat.Bmp,
            "image/tiff" or "image/x-tiff" => ImageFormat.Tiff,
            "image/jp2" => ImageFormat.Jpeg2000,
            _ => null
        };
    }

    /// <summary>
    ///     Determines whether the header starts like a BMP file.
    /// </summary>
    /// <param name="header">The first bytes of the file.</param>
    /// <returns><see langword="true"/> when the file starts with <c>BM</c> followed by a plausible bitmap header.</returns>
    private static bool LooksLikeBmp(ReadOnlySpan<byte> header)
    {
        if (header.Length < BmpHeaderLength || header[0] != (byte)'B' || header[1] != (byte)'M')
        {
            return false;
        }

        var reservedIsZero = header[6] == 0 && header[7] == 0 && header[8] == 0 && header[9] == 0;
        var dibSize = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(14, 4));
        return reservedIsZero && Array.IndexOf(BmpDibHeaderSizes, dibSize) >= 0;
    }
}

// cspell:ignore canvasnet jpeg tiff jpeg2000 pptx vsdx scad jfif Visio
using System.Buffers.Binary;
using System.Text;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     Format detection of <see cref="SourceLoader"/>.
/// </summary>
internal static partial class SourceLoader
{
    /// <summary>
    ///     The number of bytes read from the start of a file to recognize its format.
    /// </summary>
    private const int HeaderLength = 4096;

    /// <summary>
    ///     The length of the BMP file header plus the size field of the bitmap header that follows it.
    /// </summary>
    private const int BmpHeaderLength = 18;

    /// <summary>
    ///     The sizes of the bitmap header (<c>BITMAPCOREHEADER</c> and the <c>BITMAPINFOHEADER</c> family) that
    ///     follows the BMP file header.
    /// </summary>
    private static readonly int[] BmpDibHeaderSizes = [12, 40, 52, 56, 64, 108, 124];

    /// <summary>
    ///     The source kind of each file name extension (lower case, with the dot).
    /// </summary>
    /// <remarks>
    ///     The <c>formats</c> command lists the same extensions; a test keeps the two tables consistent.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, SourceKind> ExtensionKinds =
        new Dictionary<string, SourceKind>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = SourceKind.Png,
            [".jpg"] = SourceKind.Jpeg,
            [".jpeg"] = SourceKind.Jpeg,
            [".bmp"] = SourceKind.Bmp,
            [".tif"] = SourceKind.Tiff,
            [".tiff"] = SourceKind.Tiff,
            [".gif"] = SourceKind.Gif,
            [".jp2"] = SourceKind.Jpeg2000,
            [".j2k"] = SourceKind.Jpeg2000,
            [".j2c"] = SourceKind.Jpeg2000,
            [".svg"] = SourceKind.Svg,
            [".pdf"] = SourceKind.Pdf,
            [".pptx"] = SourceKind.Pptx,
            [".vsdx"] = SourceKind.Vsdx,
            [".scad"] = SourceKind.Scad
        };

    /// <summary>
    ///     Recognizes the format of a file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The kind of the file.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>input-not-found</c> when the file does not exist, <c>input-unreadable</c> when it is a
    ///     directory or cannot be read, and <c>unsupported-format</c> when its format is not recognized.
    /// </exception>
    /// <remarks>
    ///     The content decides first: the signature bytes of the raster formats and PDF, then a text sniff for SVG.
    ///     Only when the content says nothing does the file name extension decide, so that a damaged or empty file
    ///     with a known extension is reported as corrupt (by the load that follows) rather than as unsupported. A
    ///     ZIP package (PowerPoint, Visio) is distinguished by its extension.
    /// </remarks>
    public static SourceKind Detect(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (Directory.Exists(path))
        {
            throw new ToolException(ErrorCodes.InputUnreadable, $"'{path}' is a directory, not a file.");
        }

        if (!File.Exists(path))
        {
            throw new ToolException(ErrorCodes.InputNotFound, $"'{path}' does not exist.", "Check the path.");
        }

        var header = Read(path, null, () => ReadHeader(path));
        var kind = DetectContent(header, Path.GetExtension(path));
        return kind ?? throw new ToolException(
            ErrorCodes.UnsupportedFormat,
            $"'{path}' is not in a supported file format.",
            "Run 'canvasnet formats' to list the formats the tool reads.");
    }

    /// <summary>
    ///     Recognizes the format from the first bytes of a file and its extension.
    /// </summary>
    /// <param name="header">The first bytes of the file (up to a few kilobytes).</param>
    /// <param name="extension">The file name extension, with the dot (or empty).</param>
    /// <returns>The kind, or <see langword="null"/> when the format is not recognized.</returns>
    internal static SourceKind? DetectContent(ReadOnlySpan<byte> header, string extension)
    {
        var bySignature = DetectSignature(header);
        if (bySignature != null)
        {
            return bySignature;
        }

        // A ZIP package is told apart by its extension; the entries decide in a later release
        if (header.StartsWith("PK\u0003\u0004"u8)
            && ExtensionKinds.TryGetValue(extension, out var packageKind)
            && packageKind is SourceKind.Pptx or SourceKind.Vsdx)
        {
            return packageKind;
        }

        if (LooksLikeSvg(header))
        {
            return SourceKind.Svg;
        }

        return ExtensionKinds.TryGetValue(extension, out var byExtension) ? byExtension : null;
    }

    /// <summary>
    ///     Recognizes a format by its signature bytes.
    /// </summary>
    /// <param name="header">The first bytes of the file.</param>
    /// <returns>The kind, or <see langword="null"/> when no signature matches.</returns>
    private static SourceKind? DetectSignature(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return SourceKind.Png;
        }

        if (header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return SourceKind.Jpeg;
        }

        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return SourceKind.Gif;
        }

        if (header.StartsWith(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || header.StartsWith(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }))
        {
            return SourceKind.Tiff;
        }

        // JP2 signature box, or a raw codestream (SOC followed by SIZ)
        if (header.StartsWith(new byte[] { 0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A })
            || header.StartsWith(new byte[] { 0xFF, 0x4F, 0xFF, 0x51 }))
        {
            return SourceKind.Jpeg2000;
        }

        if (header.StartsWith("%PDF-"u8))
        {
            return SourceKind.Pdf;
        }

        return LooksLikeBmp(header) ? SourceKind.Bmp : null;
    }

    /// <summary>
    ///     Determines whether the header starts like a BMP file.
    /// </summary>
    /// <param name="header">The first bytes of the file.</param>
    /// <returns><see langword="true"/> when the file starts with <c>BM</c> followed by a plausible bitmap header.</returns>
    /// <remarks>
    ///     The two-letter signature alone would also match text files, so the reserved fields (which are zero) and
    ///     the size of the bitmap header that follows are checked as well.
    /// </remarks>
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

    /// <summary>
    ///     Determines whether the header looks like the start of an SVG document.
    /// </summary>
    /// <param name="header">The first bytes of the file.</param>
    /// <returns><see langword="true"/> when the header is text that contains an <c>svg</c> start tag.</returns>
    private static bool LooksLikeSvg(ReadOnlySpan<byte> header)
    {
        // Binary data (a NUL byte) is not an SVG document
        if (header.IndexOf((byte)0) >= 0)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(header);
        return text.Contains("<svg", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Reads the first bytes of a file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The bytes; fewer than <see cref="HeaderLength"/> when the file is shorter.</returns>
    private static byte[] ReadHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[HeaderLength];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return buffer.AsSpan(0, total).ToArray();
    }
}

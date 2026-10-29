// cspell:ignore canvasnet jpeg tiff jpeg2000 pptx vsdx scad jfif Visio
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

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
            [".scad"] = SourceKind.Scad,
            [".vl"] = SourceKind.VegaLite
        };

    /// <summary>
    ///     The file name suffix of a Vega-Lite chart that <see cref="ExtensionKinds"/> cannot express, because the
    ///     extension of such a file is <c>.json</c>.
    /// </summary>
    internal const string VegaLiteJsonSuffix = ".vl.json";

    /// <summary>
    ///     The text a Vega-Lite specification names its schema with: every version of the schema is under this address.
    /// </summary>
    private const string VegaLiteSchemaMarker = "vega.github.io/schema/vega-lite/";

    /// <summary>
    ///     The text a plain Vega specification names its schema with.
    /// </summary>
    private const string VegaSchemaMarker = "vega.github.io/schema/vega/";

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
    ///     The content decides first: the signature bytes of the raster formats and PDF, then the entries of a ZIP
    ///     package (PowerPoint, Visio), then a text sniff for SVG. A model has no signature: a file named <c>.scad</c> is
    ///     an OpenSCAD model (and is never taken for an SVG, whatever its text mentions). A chart has no signature either:
    ///     a file named <c>.vl.json</c> or <c>.vl</c> is a Vega-Lite chart, and so is a <c>.json</c> file whose top-level
    ///     <c>$schema</c> string property (read from the first 4 KiB) names a Vega-Lite schema; the address in any other
    ///     property or nested value does not count. A JSON file is never taken for an SVG.
    ///     Only when the content says nothing does the file name extension decide, so that a damaged or empty file
    ///     with a known extension is reported as corrupt (by the load that follows) rather than as unsupported. A
    ///     damaged ZIP package is distinguished by its extension.
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
        if (header.AsSpan().StartsWith("PK\u0003\u0004"u8))
        {
            // An Office or Visio package is told apart by its entries; any other readable archive is not supported
            var (readable, packageKind) = Read(path, null, () => DetectPackage(path));
            if (readable)
            {
                return packageKind ?? throw new ToolException(
                    ErrorCodes.UnsupportedFormat,
                    $"'{path}' is a ZIP archive that is not a PowerPoint (.pptx) or Visio (.vsdx) package.",
                    "Run 'canvasnet formats' to list the formats the tool reads.");
            }
        }

        var kind = DetectContent(header, Path.GetExtension(path), Path.GetFileName(path));
        return kind ?? throw UnsupportedFormat(path, header);
    }

    /// <summary>
    ///     Creates the exception for a file whose format is not recognized.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="header">The first bytes of the file.</param>
    /// <returns>
    ///     The <c>unsupported-format</c> exception. A plain Vega specification and a JSON file that is not a Vega-Lite
    ///     chart get a hint that says what is read instead.
    /// </returns>
    private static ToolException UnsupportedFormat(string path, ReadOnlySpan<byte> header)
    {
        if (!Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return new ToolException(
                ErrorCodes.UnsupportedFormat,
                $"'{path}' is not in a supported file format.",
                "Run 'canvasnet formats' to list the formats the tool reads.");
        }

        return ReadTopLevelSchema(header)?.Contains(VegaSchemaMarker, StringComparison.Ordinal) == true
            ? new ToolException(
                ErrorCodes.UnsupportedFormat,
                $"'{path}' is a Vega specification; only Vega-Lite charts are supported.",
                "Vega is not supported. A Vega-Lite chart names a schema under vega.github.io/schema/vega-lite/.")
            : new ToolException(
                ErrorCodes.UnsupportedFormat,
                $"'{path}' is a JSON file that is not a Vega-Lite chart.",
                "A Vega-Lite chart is recognized by the suffix .vl.json (or .vl), or by a top-level $schema under vega.github.io/schema/vega-lite/ in the first 4 KiB; rename the file to .vl.json if it is one.");
    }

    /// <summary>
    ///     Reads the top-level <c>$schema</c> string property of a JSON document from its first bytes.
    /// </summary>
    /// <param name="header">The first bytes of the file; the document may be cut off anywhere.</param>
    /// <returns>
    ///     The value of the <c>$schema</c> property of the root object, or <see langword="null"/> when the header is not
    ///     JSON text, the root is not an object, the property is missing, is not a string, or lies beyond the header.
    /// </returns>
    /// <remarks>
    ///     Only the properties of the root object are looked at (nested values are skipped), so a schema address that
    ///     appears in another property or deeper in the document does not count. A document cut off by the end of the
    ///     header is not an error: the properties before the cut are still read.
    /// </remarks>
    private static string? ReadTopLevelSchema(ReadOnlySpan<byte> header)
    {
        // Binary data (a NUL byte) is not a JSON document; a UTF-8 byte order mark is not part of the text
        if (header.IndexOf((byte)0) >= 0)
        {
            return null;
        }

        if (header.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            header = header[3..];
        }

        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var reader = new Utf8JsonReader(header, isFinalBlock: false, new JsonReaderState(options));
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var isSchema = reader.ValueTextEquals("$schema"u8);
                if (!reader.Read())
                {
                    return null;
                }

                if (isSchema)
                {
                    return reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                }

                if (!reader.TrySkip())
                {
                    return null;
                }
            }
        }
        catch (JsonException)
        {
            // Text that is not JSON (or is damaged before the property is reached) names no schema
        }

        return null;
    }

    /// <summary>
    ///     Recognizes the format from the first bytes of a file and its extension.
    /// </summary>
    /// <param name="header">The first bytes of the file (up to a few kilobytes).</param>
    /// <param name="extension">The file name extension, with the dot (or empty).</param>
    /// <param name="fileName">
    ///     The file name, which is needed to see the two-part suffix <c>.vl.json</c>; empty when it is not known (the
    ///     extension alone then decides).
    /// </param>
    /// <returns>The kind, or <see langword="null"/> when the format is not recognized.</returns>
    internal static SourceKind? DetectContent(ReadOnlySpan<byte> header, string extension, string fileName = "")
    {
        var bySignature = DetectSignature(header);
        if (bySignature != null)
        {
            return bySignature;
        }

        // A ZIP package is told apart by its extension here (a damaged file); Detect looks at the entries of a readable one
        if (header.StartsWith("PK\u0003\u0004"u8)
            && ExtensionKinds.TryGetValue(extension, out var packageKind)
            && packageKind is SourceKind.Pptx or SourceKind.Vsdx)
        {
            return packageKind;
        }

        // A chart is named by its suffix (checked before the extension, which is only .json) or by its schema; a
        // JSON file is text but never an SVG, so it does not reach the SVG sniff
        if (fileName.EndsWith(VegaLiteJsonSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return SourceKind.VegaLite;
        }

        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return ReadTopLevelSchema(header)?.Contains(VegaLiteSchemaMarker, StringComparison.Ordinal) == true ? SourceKind.VegaLite : null;
        }

        if (LooksLikeSvg(header) && !extension.Equals(".scad", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".vl", StringComparison.OrdinalIgnoreCase))
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
    ///     Looks at the entries of a ZIP archive to tell what package it is.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>
    ///     Whether the archive could be read, and, when it could, the kind of package: a presentation when it has
    ///     <c>ppt/presentation.xml</c>, a drawing when it has <c>visio/document.xml</c>, and <see langword="null"/>
    ///     for any other archive (a Word or Excel file, for example).
    /// </returns>
    /// <remarks>
    ///     A file that starts like a ZIP archive but cannot be read as one is reported as not readable, so that the
    ///     file name extension decides and the load that follows reports the damage.
    /// </remarks>
    private static (bool Readable, SourceKind? Kind) DetectPackage(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var name in archive.Entries.Select(entry => entry.FullName))
            {
                if (string.Equals(name, "ppt/presentation.xml", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, SourceKind.Pptx);
                }

                if (string.Equals(name, "visio/document.xml", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, SourceKind.Vsdx);
                }
            }

            return (true, null);
        }
        catch (InvalidDataException)
        {
            return (false, null);
        }
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

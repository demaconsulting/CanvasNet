// cspell:ignore jpeg jpg tiff tif
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     How an image is written.
/// </summary>
/// <param name="Format">The output format, or <see langword="null"/> to take it from the output file's extension.</param>
/// <param name="Quality">The JPEG quality, 1 to 100; ignored by the lossless formats.</param>
/// <param name="NoClobber"><see langword="true"/> to refuse to overwrite an existing file.</param>
/// <param name="FlattenBackground">The color a JPEG is flattened over, because JPEG has no transparency.</param>
internal sealed record WriteOptions(OutputFormat? Format, int Quality, bool NoClobber, Rgba32 FlattenBackground)
{
    /// <summary>
    ///     Gets the options with every default: format from the extension, quality 90, overwrite, flatten over white.
    /// </summary>
    public static WriteOptions Default { get; } = new(null, ImageWriter.DefaultQuality, false, new Rgba32(255, 255, 255, 255));
}

/// <summary>
///     What was written.
/// </summary>
/// <param name="Path">The path of the file as it was given.</param>
/// <param name="Format">The format that was written.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Bytes">The size of the file in bytes.</param>
/// <param name="Flattened"><see langword="true"/> when transparency was flattened over the background (JPEG only).</param>
internal sealed record WriteResult(string Path, OutputFormat Format, int Width, int Height, long Bytes, bool Flattened);

/// <summary>
///     Writes an image to a file: it picks the format, encodes the pixels, and replaces the target atomically.
/// </summary>
/// <remarks>
///     <para>
///         The format comes from the <c>--format</c> option or else from the file name extension. The image is
///         encoded to a temporary file in the target directory and then moved over the target, so a failure never
///         leaves a half-written output and an existing output is only replaced by a complete one. The target is
///         overwritten unless <see cref="WriteOptions.NoClobber"/> is set, and an output that is the same file as
///         the input is always refused. A missing target directory is created.
///     </para>
///     <para>
///         The encoders are deterministic, so the same pixels written with the same options are the same bytes.
///         JPEG cannot hold transparency, so a surface with transparent pixels is flattened over
///         <see cref="WriteOptions.FlattenBackground"/> first. Write failures (a path that cannot be created or
///         written) are reported as <c>output-write-failed</c>.
///     </para>
/// </remarks>
internal static class ImageWriter
{
    /// <summary>The smallest JPEG quality setting.</summary>
    public const int MinQuality = 1;

    /// <summary>The largest JPEG quality setting.</summary>
    public const int MaxQuality = 100;

    /// <summary>The JPEG quality setting used when none is given.</summary>
    public const int DefaultQuality = 90;

    /// <summary>
    ///     The output format of each file name extension (lower case, with the dot).
    /// </summary>
    /// <remarks>
    ///     The <c>formats</c> command lists the same extensions; a test keeps the two tables consistent.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, OutputFormat> ExtensionFormats =
        new Dictionary<string, OutputFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = OutputFormat.Png,
            [".jpg"] = OutputFormat.Jpeg,
            [".jpeg"] = OutputFormat.Jpeg,
            [".bmp"] = OutputFormat.Bmp,
            [".tif"] = OutputFormat.Tiff,
            [".tiff"] = OutputFormat.Tiff
        };

    /// <summary>
    ///     The names accepted by the <c>--format</c> option.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, OutputFormat> FormatNames =
        new Dictionary<string, OutputFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["png"] = OutputFormat.Png,
            ["jpeg"] = OutputFormat.Jpeg,
            ["jpg"] = OutputFormat.Jpeg,
            ["bmp"] = OutputFormat.Bmp,
            ["tiff"] = OutputFormat.Tiff,
            ["tif"] = OutputFormat.Tiff
        };

    /// <summary>
    ///     Decides the output format from the <c>--format</c> option or the file name extension.
    /// </summary>
    /// <param name="outputPath">The output path.</param>
    /// <param name="formatOption">The value of the <c>--format</c> option, or <see langword="null"/>.</param>
    /// <returns>The format; the option wins over the extension.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="outputPath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>usage-error</c> when the option names an unknown format, or when there is no option and
    ///     the extension is missing or unknown.
    /// </exception>
    public static OutputFormat ResolveFormat(string outputPath, string? formatOption)
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        if (formatOption != null)
        {
            return FormatNames.TryGetValue(formatOption.Trim(), out var named)
                ? named
                : throw new ToolException(
                    ErrorCodes.UsageError,
                    $"Unknown output format '{formatOption}'.",
                    "Use png, jpeg, bmp, or tiff.");
        }

        var extension = Path.GetExtension(outputPath);
        if (ExtensionFormats.TryGetValue(extension, out var byExtension))
        {
            return byExtension;
        }

        var problem = extension.Length == 0
            ? $"Cannot tell the output format from '{outputPath}': it has no extension."
            : $"Cannot tell the output format from the extension '{extension}' of '{outputPath}'.";
        throw new ToolException(ErrorCodes.UsageError, problem, "Use .png, .jpg, .bmp, or .tif, or choose the format with --format.");
    }

    /// <summary>
    ///     Checks that an output may be written, without writing anything.
    /// </summary>
    /// <param name="outputPath">The output path.</param>
    /// <param name="inputPath">The input path; the output must not be the same file.</param>
    /// <param name="noClobber"><see langword="true"/> to refuse an existing output.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>same-input-output</c> when the output is the input, <c>output-exists</c> when it exists and
    ///     <paramref name="noClobber"/> is set, and <c>output-write-failed</c> when the output is an existing directory.
    /// </exception>
    /// <remarks>
    ///     A command calls this before it loads the input, so that a mistake in the output is reported before any
    ///     work is done; <see cref="Write"/> checks again.
    /// </remarks>
    public static void CheckTarget(string outputPath, string inputPath, bool noClobber)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(inputPath);

        if (IsSameFile(outputPath, inputPath))
        {
            throw new ToolException(
                ErrorCodes.SameInputOutput,
                $"The output '{outputPath}' is the same file as the input.",
                "Choose a different output path.");
        }

        if (Directory.Exists(outputPath))
        {
            throw new ToolException(ErrorCodes.OutputWriteFailed, $"The output '{outputPath}' is a directory, not a file.");
        }

        if (noClobber && File.Exists(outputPath))
        {
            throw new ToolException(
                ErrorCodes.OutputExists,
                $"The output '{outputPath}' already exists.",
                "Remove --no-clobber to overwrite it, or choose a different output path.");
        }
    }

    /// <summary>
    ///     Writes a surface to a file.
    /// </summary>
    /// <param name="surface">The pixels; the surface is not modified or disposed.</param>
    /// <param name="outputPath">The output path.</param>
    /// <param name="inputPathForGuard">The input path; the output must not be the same file.</param>
    /// <param name="options">The write options.</param>
    /// <returns>What was written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the quality is not 1 to 100.</exception>
    /// <exception cref="ToolException">
    ///     Thrown for the failures of <see cref="ResolveFormat"/> and <see cref="CheckTarget"/>, and with code
    ///     <c>output-write-failed</c> when the file or its directory cannot be created or written.
    /// </exception>
    public static WriteResult Write(Surface surface, string outputPath, string inputPathForGuard, WriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(inputPathForGuard);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Quality, MinQuality);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Quality, MaxQuality);

        var format = ResolveFormat(outputPath, options.Format?.ToFormatName());
        CheckTarget(outputPath, inputPathForGuard, options.NoClobber);

        // JPEG has no transparency: flatten first, but only when there is something to flatten
        var flatten = format == OutputFormat.Jpeg && SurfaceOperations.HasTransparency(surface);
        var pixels = flatten ? SurfaceOperations.FlattenOver(surface, options.FlattenBackground) : surface;
        try
        {
            var bytes = WriteAtomically(pixels, format, options, outputPath);
            return new WriteResult(outputPath, format, surface.Width, surface.Height, bytes, flatten);
        }
        finally
        {
            if (flatten)
            {
                pixels.Dispose();
            }
        }
    }

    /// <summary>
    ///     Encodes the pixels to a temporary file next to the target and moves it over the target.
    /// </summary>
    /// <param name="pixels">The pixels to encode.</param>
    /// <param name="format">The format.</param>
    /// <param name="options">The write options.</param>
    /// <param name="outputPath">The target path.</param>
    /// <returns>The size of the written file in bytes.</returns>
    private static long WriteAtomically(Surface pixels, OutputFormat format, WriteOptions options, string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Encode(pixels, format, options.Quality, stream);
            }

            File.Move(temporary, fullPath, overwrite: !options.NoClobber);
            return new FileInfo(fullPath).Length;
        }
        catch (IOException) when (options.NoClobber && File.Exists(fullPath))
        {
            // Another process created the target between the check and the move
            throw new ToolException(
                ErrorCodes.OutputExists,
                $"The output '{outputPath}' already exists.",
                "Remove --no-clobber to overwrite it, or choose a different output path.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new ToolException(
                ErrorCodes.OutputWriteFailed,
                $"The output '{outputPath}' cannot be written: {ex.Message}",
                null,
                ex);
        }
        finally
        {
            // Nothing is left behind when the encode or the move failed
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The temporary file is only a leftover; failing to remove it must not hide the real result
            }
        }
    }

    /// <summary>
    ///     Encodes pixels to a stream in the given format.
    /// </summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="format">The format.</param>
    /// <param name="quality">The JPEG quality.</param>
    /// <param name="stream">The stream to write to.</param>
    private static void Encode(Surface pixels, OutputFormat format, int quality, Stream stream)
    {
        switch (format)
        {
            case OutputFormat.Png:
                PngCodec.Save(pixels, stream);
                break;
            case OutputFormat.Jpeg:
                JpegCodec.Save(pixels, stream, quality);
                break;
            case OutputFormat.Bmp:
                BmpCodec.Save(pixels, stream);
                break;
            default:
                TiffCodec.Save(pixels, stream);
                break;
        }
    }

    /// <summary>
    ///     Determines whether two paths name the same file.
    /// </summary>
    /// <param name="first">The first path.</param>
    /// <param name="second">The second path.</param>
    /// <returns><see langword="true"/> when both resolve to the same full path, or are the same existing file.</returns>
    /// <remarks>
    ///     Windows and macOS file systems are case-insensitive by default, so their paths are compared ignoring case;
    ///     this errs on the side of refusing, which is harmless. An existing pair of paths that reach the same file
    ///     through a link is not detected.
    /// </remarks>
    private static bool IsSameFile(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison);
    }
}

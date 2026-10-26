// cspell:ignore canvasnet jpeg jpg tiff tif
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
///         encoded to a temporary file in the target directory and then moved over the target, so a failure while
///         encoding never leaves a half-written output and an existing output is only replaced by a complete one. The
///         target is overwritten unless <see cref="WriteOptions.NoClobber"/> is set, and an output that is the same
///         file as the input (by path, after link resolution) is always refused. A missing target directory is created.
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

    /// <summary>The most links followed, one after another, when a path is resolved; the same limit as the operating system's.</summary>
    private const int MaxLinkDepth = 40;

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
    ///     <paramref name="noClobber"/> is set, and <c>output-write-failed</c> when the output is an existing directory,
    ///     is not a valid path, or has a file where one of its folders should be.
    /// </exception>
    /// <remarks>
    ///     A command calls this before it loads the input, so that a mistake in the output is reported before any
    ///     work is done; <see cref="Write"/> checks again.
    /// </remarks>
    public static void CheckTarget(string outputPath, string inputPath, bool noClobber)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(inputPath);

        CheckPathIsUsable(outputPath);
        CheckNotInput(outputPath, inputPath);
        CheckWritable(outputPath, noClobber);
    }

    /// <summary>
    ///     Checks that an output is not the same file as an input.
    /// </summary>
    /// <param name="outputPath">The output path.</param>
    /// <param name="inputPath">The input path.</param>
    /// <exception cref="ToolException">Thrown with code <c>same-input-output</c> when the paths name the same file.</exception>
    private static void CheckNotInput(string outputPath, string inputPath)
    {
        if (IsSameFile(outputPath, inputPath))
        {
            throw new ToolException(
                ErrorCodes.SameInputOutput,
                $"The output '{outputPath}' is the same file as the input.",
                "Choose a different output path.");
        }
    }

    /// <summary>
    ///     Checks that an output is not a directory and, with <paramref name="noClobber"/>, does not exist.
    /// </summary>
    /// <param name="outputPath">The output path.</param>
    /// <param name="noClobber"><see langword="true"/> to refuse an existing output.</param>
    /// <exception cref="ToolException">Thrown with code <c>output-write-failed</c> or <c>output-exists</c>.</exception>
    private static void CheckWritable(string outputPath, bool noClobber)
    {
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
    ///     Checks that every output of a command may be written, without writing anything.
    /// </summary>
    /// <param name="outputPaths">The output paths; none may be the same file as another or as any input.</param>
    /// <param name="inputPaths">The input paths; no output may be the same file as any of them.</param>
    /// <param name="noClobber"><see langword="true"/> to refuse an existing output.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument or an element of a list is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown for every failure of <see cref="CheckTarget"/> applied to each output and each input, and with code
    ///     <c>usage-error</c> when two outputs name the same file.
    /// </exception>
    /// <remarks>
    ///     A command with several inputs and outputs calls this once before it loads anything, so that every mistake in
    ///     the targets is reported before any work is done. Outputs are checked in the order given, each against every
    ///     input in the order given, then against the outputs before it.
    /// </remarks>
    public static void CheckTargets(IReadOnlyList<string> outputPaths, IReadOnlyList<string> inputPaths, bool noClobber)
    {
        ArgumentNullException.ThrowIfNull(outputPaths);
        ArgumentNullException.ThrowIfNull(inputPaths);

        for (var i = 0; i < outputPaths.Count; i++)
        {
            var output = outputPaths[i] ?? throw new ArgumentNullException(nameof(outputPaths));
            CheckPathIsUsable(output);
            foreach (var input in inputPaths)
            {
                CheckNotInput(output, input ?? throw new ArgumentNullException(nameof(inputPaths)));
            }

            CheckWritable(output, noClobber);

            for (var earlier = 0; earlier < i; earlier++)
            {
                if (IsSameFile(output, outputPaths[earlier]))
                {
                    throw new ToolException(
                        ErrorCodes.UsageError,
                        $"The outputs '{outputPaths[earlier]}' and '{output}' are the same file.",
                        "Give each output its own path.");
                }
            }
        }
    }

    /// <summary>
    ///     Checks that an output path is a valid path whose directory can exist.
    /// </summary>
    /// <param name="outputPath">The output path.</param>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>output-write-failed</c> when the path is not valid on this system (for example it is
    ///     empty, contains a null character, or is too long), or when a folder in front of the file name exists as a
    ///     file.
    /// </exception>
    /// <remarks>
    ///     A folder that does not exist is fine: <see cref="Write"/> creates it. Reporting the mistakes here means they
    ///     are found before the input is decoded, and as an expected error rather than a crash.
    /// </remarks>
    private static void CheckPathIsUsable(string outputPath)
    {
        string full;
        try
        {
            full = Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            throw new ToolException(
                ErrorCodes.OutputWriteFailed,
                $"The output '{outputPath}' is not a valid path: {ex.Message}",
                null,
                ex);
        }

        // The nearest folder that exists must be a folder: a file in the way can never become a directory
        for (var parent = Path.GetDirectoryName(full); parent != null; parent = Path.GetDirectoryName(parent))
        {
            if (Directory.Exists(parent))
            {
                return;
            }

            if (File.Exists(parent))
            {
                throw new ToolException(
                    ErrorCodes.OutputWriteFailed,
                    $"The output '{outputPath}' cannot be written: '{parent}' is a file, not a folder.");
            }
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
        using var staged = Stage(surface, outputPath, inputPathForGuard, options);
        return staged.Commit();
    }

    /// <summary>
    ///     Encodes a surface to a temporary file next to the output, without publishing it.
    /// </summary>
    /// <param name="surface">The pixels; the surface is not modified or disposed.</param>
    /// <param name="outputPath">The output path.</param>
    /// <param name="inputPathForGuard">The input path; the output must not be the same file.</param>
    /// <param name="options">The write options.</param>
    /// <returns>
    ///     The staged output; <see cref="StagedOutput.Commit"/> moves it into place and disposing it deletes the
    ///     temporary file when it was not committed.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the quality is not 1 to 100.</exception>
    /// <exception cref="ToolException">
    ///     Thrown for the failures of <see cref="ResolveFormat"/> and <see cref="CheckTarget"/>, and with code
    ///     <c>output-write-failed</c> when the temporary file or its directory cannot be created or written.
    /// </exception>
    /// <remarks>
    ///     A command that writes several outputs stages every one of them first and publishes them with
    ///     <see cref="PublishAll"/> only when all were staged, so a failure while rendering or encoding leaves no new or
    ///     partial output and every existing output untouched, and a failure while publishing is rolled back.
    /// </remarks>
    public static StagedOutput Stage(Surface surface, string outputPath, string inputPathForGuard, WriteOptions options)
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
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath)!;
            // A short fixed name: a name made from the target could be too long for the file system when the target's is long
            var temporary = Path.Combine(directory, $".canvasnet-{Guid.NewGuid():N}.tmp");
            var staged = new StagedOutput(temporary, fullPath, outputPath, options.NoClobber);
            try
            {
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    Encode(pixels, format, options.Quality, stream);
                }

                staged.Result = new WriteResult(
                    outputPath, format, surface.Width, surface.Height, new FileInfo(temporary).Length, flatten);
                return staged;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                staged.Dispose();
                throw new ToolException(
                    ErrorCodes.OutputWriteFailed,
                    $"The output '{outputPath}' cannot be written: {ex.Message}",
                    null,
                    ex);
            }
            catch
            {
                staged.Dispose();
                throw;
            }
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
    ///     Publishes every staged output, all or nothing.
    /// </summary>
    /// <param name="outputs">The staged outputs, in the order they are published; every one is already encoded.</param>
    /// <param name="beforeStep">
    ///     A test seam called with the step and the index of an output just before an existing target is renamed to
    ///     its backup (<see cref="PublishStep.Backup"/>, only when the target exists) and just before its temporary
    ///     file is moved into place (<see cref="PublishStep.Move"/>); a failure it throws is handled like a failure
    ///     of that step.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="outputs"/> is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown with code <c>output-exists</c> when a target appeared and no-clobber is set, and
    ///     <c>output-write-failed</c> when a step fails.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         For each target in order, an existing target is first renamed to a unique backup in the same
    ///         directory (<c>.canvasnet-{guid}.bak</c>) and then the temporary file is moved into place. When any step
    ///         fails, every target that was touched, including the one whose step failed, is rolled back: a published
    ///         new file is deleted and a backup is restored over whatever now holds the name (a target that did not
    ///         exist is left absent, and one whose backup rename failed has nothing to restore). Every restore is
    ///         attempted even when another fails. The remaining temporary files are deleted and the original error
    ///         is reported, so the set is published completely or not at all. On success the backups are deleted;
    ///         failing to delete one is ignored.
    ///     </para>
    ///     <para>
    ///         The one residual case is a failure to restore a backup: the error then also names the backup file, which
    ///         still holds the previous content of that target.
    ///     </para>
    /// </remarks>
    internal static void PublishAll(IReadOnlyList<StagedOutput> outputs, Action<PublishStep, int>? beforeStep = null)
    {
        ArgumentNullException.ThrowIfNull(outputs);

        // Per target: the backup of its previous file (set only once the rename succeeded), and whether its temporary
        // file reached the target. A target is touched from the moment its step starts, so the one that fails is
        // rolled back too.
        var backups = new string?[outputs.Count];
        var published = new bool[outputs.Count];
        var touched = 0;
        ToolException? failure = null;
        for (var index = 0; index < outputs.Count && failure == null; index++)
        {
            touched = index + 1;
            var output = outputs[index];
            try
            {
                if (File.Exists(output.FullPath))
                {
                    if (output.NoClobber)
                    {
                        throw new ToolException(
                            ErrorCodes.OutputExists,
                            $"The output '{output.OutputPath}' already exists.",
                            "Remove --no-clobber to overwrite it, or choose a different output path.");
                    }

                    var backup = Path.Combine(Path.GetDirectoryName(output.FullPath)!, $".canvasnet-{Guid.NewGuid():N}.bak");
                    beforeStep?.Invoke(PublishStep.Backup, index);
                    File.Move(output.FullPath, backup);
                    backups[index] = backup;
                }

                beforeStep?.Invoke(PublishStep.Move, index);
                File.Move(output.Temporary, output.FullPath, overwrite: false);
                published[index] = true;
            }
            catch (ToolException ex)
            {
                failure = ex;
            }
            catch (IOException) when (output.NoClobber && File.Exists(output.FullPath))
            {
                // Another process created the target between the check and the move
                failure = new ToolException(
                    ErrorCodes.OutputExists,
                    $"The output '{output.OutputPath}' already exists.",
                    "Remove --no-clobber to overwrite it, or choose a different output path.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                failure = new ToolException(
                    ErrorCodes.OutputWriteFailed,
                    $"The output '{output.OutputPath}' cannot be written: {ex.Message}",
                    null,
                    ex);
            }
        }

        if (failure == null)
        {
            foreach (var output in outputs)
            {
                output.MarkCommitted();
            }

            foreach (var backup in backups.OfType<string>())
            {
                TryDelete(backup);
            }

            return;
        }

        // Roll back: undo what was published, put every previous file back, and remove what is left of the temporaries
        // (in reverse, so a target listed twice unwinds correctly); every restore is attempted even when one fails
        var notRestored = new List<string>();
        for (var i = touched - 1; i >= 0; i--)
        {
            var backup = backups[i];
            try
            {
                if (published[i])
                {
                    File.Delete(outputs[i].FullPath);
                }

                if (backup != null)
                {
                    // Replaces whatever now occupies the name, such as a partial file
                    File.Move(backup, outputs[i].FullPath, overwrite: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                notRestored.Add(backup != null
                    ? $"'{outputs[i].OutputPath}' (its previous content is kept in '{backup}')"
                    : $"'{outputs[i].OutputPath}' (the new file could not be removed)");
            }
        }

        foreach (var output in outputs)
        {
            output.Dispose();
        }

        if (notRestored.Count == 0)
        {
            throw failure;
        }

        throw new ToolException(
            failure.Code,
            $"{failure.Message} Rolling back also failed for {string.Join(", ", notRestored)}.",
            failure.Hint,
            failure);
    }

    /// <summary>
    ///     The steps of publishing one output that the test seam of <see cref="PublishAll"/> can fail.
    /// </summary>
    internal enum PublishStep
    {
        /// <summary>Renaming an existing target to its backup.</summary>
        Backup,

        /// <summary>Moving the temporary file into place.</summary>
        Move,
    }

    /// <summary>
    ///     Deletes a file, ignoring failure.
    /// </summary>
    /// <param name="path">The file.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover backup must not fail a command whose outputs were all published
        }
    }

    /// <summary>
    ///     An output that has been encoded to a temporary file but not yet moved into place.
    /// </summary>
    internal sealed class StagedOutput : IDisposable
    {
        /// <summary>Whether the temporary file has been moved into place and kept there.</summary>
        private bool _committed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="StagedOutput"/> class.
        /// </summary>
        /// <param name="temporary">The temporary file.</param>
        /// <param name="fullPath">The full path of the target.</param>
        /// <param name="outputPath">The target path as it was given.</param>
        /// <param name="noClobber"><see langword="true"/> to refuse an existing target.</param>
        internal StagedOutput(string temporary, string fullPath, string outputPath, bool noClobber)
        {
            Temporary = temporary;
            FullPath = fullPath;
            OutputPath = outputPath;
            NoClobber = noClobber;
            Result = null!;
        }

        /// <summary>
        ///     Gets the temporary file.
        /// </summary>
        internal string Temporary { get; }

        /// <summary>
        ///     Gets the full path of the target.
        /// </summary>
        internal string FullPath { get; }

        /// <summary>
        ///     Gets the target path as it was given.
        /// </summary>
        internal string OutputPath { get; }

        /// <summary>
        ///     Gets a value indicating whether an existing target is refused.
        /// </summary>
        internal bool NoClobber { get; }

        /// <summary>
        ///     Records that the output was published and must be kept.
        /// </summary>
        internal void MarkCommitted()
        {
            _committed = true;
        }

        /// <summary>
        ///     Gets what will be written when the output is committed.
        /// </summary>
        public WriteResult Result { get; internal set; }

        /// <summary>
        ///     Publishes this output alone; see <see cref="PublishAll"/>.
        /// </summary>
        /// <returns>What was written.</returns>
        /// <exception cref="ToolException">Thrown as <see cref="PublishAll"/> does.</exception>
        public WriteResult Commit()
        {
            PublishAll([this]);
            return Result;
        }

        /// <summary>
        ///     Deletes the temporary file when it was not committed.
        /// </summary>
        public void Dispose()
        {
            if (_committed)
            {
                return;
            }

            // Nothing is left behind when the encode or the move failed
            try
            {
                File.Delete(Temporary);
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
    /// <returns>
    ///     <see langword="true"/> when both paths are identical after full-path normalization and symbolic link or
    ///     junction resolution.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         The check compares path names; it does not compare file identity, so it does not detect a hard link (a
    ///         second directory entry for the same file). That is safe: the input is read and decoded completely
    ///         before any output is written, and an output is written to a new temporary file and then moved over the
    ///         target name, which replaces that directory entry rather than writing into the existing file. An output
    ///         name that is a hard link to the input therefore receives a new file, and the input keeps its content.
    ///     </para>
    ///     <para>
    ///         Windows and macOS file systems are case-insensitive by default, so their paths are compared ignoring
    ///         case; this errs on the side of refusing, which is harmless. Symbolic links and junctions (to files or
    ///         to any parent directory) are resolved before the comparison, so two paths that reach the same file
    ///         through a link are detected.
    ///     </para>
    /// </remarks>
    private static bool IsSameFile(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison)
            || string.Equals(ResolveLinks(first), ResolveLinks(second), comparison);
    }

    /// <summary>
    ///     Resolves symbolic links in every component of a path.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <param name="depth">How many links have been followed to reach this path; bounds a chain of links.</param>
    /// <returns>The full path with links replaced by their final targets; the plain full path when resolution fails.</returns>
    /// <remarks>
    ///     The target of a link is itself resolved, because the target text may name a path that goes through a link.
    ///     This is the normal case on macOS, where the temporary folder <c>/var/...</c> is reached through the link
    ///     <c>/var</c> to <c>/private/var</c>: a link in that folder stores a target that begins with <c>/var</c>.
    /// </remarks>
    private static string ResolveLinks(string path, int depth = 0)
    {
        var full = Path.GetFullPath(path);
        if (depth > MaxLinkDepth)
        {
            return full;
        }

        try
        {
            var root = Path.GetPathRoot(full) ?? string.Empty;
            var current = root;
            var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            foreach (var segment in full[root.Length..].Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if (info.LinkTarget != null && info.ResolveLinkTarget(true) is { } target)
                {
                    current = ResolveLinks(target.FullName, depth + 1);
                }
            }

            return current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return full;
        }
    }
}

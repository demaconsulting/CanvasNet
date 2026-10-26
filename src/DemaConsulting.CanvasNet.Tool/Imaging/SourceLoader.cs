// cspell:ignore canvasnet jpeg tiff jpeg2000 pptx vsdx scad Visio RRGGBB RRGGBBAA
using System.Globalization;
using System.Xml;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     Detects, probes, and loads the files the tool reads: it turns a path into a <see cref="SourceInfo"/> or into
///     pixels.
/// </summary>
/// <remarks>
///     <para>
///         The class is split into partial files by concern: this file holds the public operations and the
///         shared helpers (size policy, error mapping); <c>SourceLoader.Detect.cs</c> recognizes the format;
///         <c>SourceLoader.Raster.cs</c> and <c>SourceLoader.Svg.cs</c> handle the two families of source that are
///         implemented. A further source kind is added by a new partial file with a probe method and a load
///         method and one new case in each of <see cref="Probe"/> and <see cref="Load(SourceInfo, SourceOptions)"/>.
///     </para>
///     <para>
///         The loader never lets an expected failure escape as a library exception. The library exceptions that
///         mean "this file is missing, unreadable, corrupt, or uses something unsupported" are mapped, by an
///         explicit allow-list of types in <see cref="Read{T}"/>, to a <see cref="ToolException"/> with a stable
///         code; every other exception is a defect and propagates so that the entry point re-throws it. An image
///         larger than <see cref="Surface.MaxDimension"/> is refused with <c>size-limit</c> before it is decoded
///         wherever the file header gives the size.
///     </para>
/// </remarks>
internal static partial class SourceLoader
{
    /// <summary>
    ///     Reads the facts about a file without rendering it.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The facts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown for a missing, unreadable, unsupported, or corrupt file (see <see cref="Detect"/> and the mapping
    ///     table in <see cref="Read{T}"/>).
    /// </exception>
    public static SourceInfo Probe(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var kind = Detect(path);
        var bytes = Read(path, null, () => new FileInfo(path).Length);
        return kind switch
        {
            SourceKind.Png or SourceKind.Jpeg or SourceKind.Bmp or SourceKind.Tiff or SourceKind.Gif or SourceKind.Jpeg2000
                => ProbeRaster(path, kind, bytes),
            SourceKind.Svg => ProbeSvg(path, bytes),
            _ => throw NotYetSupported(path, kind)
        };
    }

    /// <summary>
    ///     Loads a file into pixels.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="options">The loading options.</param>
    /// <returns>
    ///     The pixels and the facts about the source. A vector source is rendered directly at its final size; a raster
    ///     source is decoded at its own size and then oriented, resized, and flattened as the options say.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown for a missing, unreadable, unsupported, or corrupt file, and with code <c>size-limit</c> when the
    ///     source or the requested size is larger than <see cref="Surface.MaxDimension"/> pixels per side.
    /// </exception>
    public static LoadedSource Load(string path, SourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);

        return Load(Probe(path), options);
    }

    /// <summary>
    ///     Loads a file into pixels, given the facts that <see cref="Probe"/> already read.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="options">The loading options.</param>
    /// <returns>The pixels and the facts about the source; see <see cref="Load(string, SourceOptions)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">See <see cref="Load(string, SourceOptions)"/>.</exception>
    public static LoadedSource Load(SourceInfo info, SourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(options);

        return info.Format switch
        {
            SourceKind.Png or SourceKind.Jpeg or SourceKind.Bmp or SourceKind.Tiff or SourceKind.Gif or SourceKind.Jpeg2000
                => LoadRaster(info, options),
            SourceKind.Svg => LoadSvg(info, options),
            _ => throw NotYetSupported(info.Path, info.Format)
        };
    }

    /// <summary>
    ///     Creates the error for a source kind that is recognized but cannot be loaded yet.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="kind">The recognized kind.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException NotYetSupported(string path, SourceKind kind)
    {
        return new ToolException(
            ErrorCodes.UnsupportedFormat,
            $"'{path}' is a {kind.ToFormatName()} file, which this version of the tool cannot read yet.",
            "Run 'canvasnet formats' to list the formats the tool reads.");
    }

    /// <summary>
    ///     Computes the size an image is produced at from its natural size and the sizing options.
    /// </summary>
    /// <param name="path">The path of the source, for messages.</param>
    /// <param name="naturalWidth">The natural width in pixels.</param>
    /// <param name="naturalHeight">The natural height in pixels.</param>
    /// <param name="options">The options: scale, width, and height.</param>
    /// <returns>The width and height in pixels, each at least 1.</returns>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when a side would exceed <see cref="Surface.MaxDimension"/>.</exception>
    /// <remarks>
    ///     A scale multiplies both sides; a width alone keeps the aspect ratio; a height alone keeps the aspect
    ///     ratio; both together are exact; with no sizing option the natural size is kept. Results are rounded to
    ///     the nearest pixel (half away from zero).
    /// </remarks>
    internal static (int Width, int Height) ComputeTargetSize(string path, int naturalWidth, int naturalHeight, SourceOptions options)
    {
        double width = naturalWidth;
        double height = naturalHeight;
        if (options.Scale is { } scale)
        {
            width = naturalWidth * scale;
            height = naturalHeight * scale;
        }
        else if (options.Width is { } w && options.Height is { } h)
        {
            width = w;
            height = h;
        }
        else if (options.Width is { } onlyWidth)
        {
            width = onlyWidth;
            height = (double)naturalHeight * onlyWidth / naturalWidth;
        }
        else if (options.Height is { } onlyHeight)
        {
            height = onlyHeight;
            width = (double)naturalWidth * onlyHeight / naturalHeight;
        }

        width = Math.Max(1, Math.Round(width, MidpointRounding.AwayFromZero));
        height = Math.Max(1, Math.Round(height, MidpointRounding.AwayFromZero));
        if (width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            var sized = options.Scale != null || options.Width != null || options.Height != null;
            var size = string.Create(CultureInfo.InvariantCulture, $"{width:0.#}x{height:0.#}");
            throw new ToolException(
                ErrorCodes.SizeLimit,
                sized
                    ? $"The requested size of {size} pixels is larger than the maximum of {Surface.MaxDimension} pixels per side."
                    : $"'{path}' is {size} pixels, which is larger than the maximum of {Surface.MaxDimension} pixels per side.",
                sized
                    ? "Use a smaller --scale, --width, or --height."
                    : "Use --width, --height, or --scale to choose a smaller size.");
        }

        return ((int)width, (int)height);
    }

    /// <summary>
    ///     Refuses an image whose stored size is larger than the maximum, before it is decoded.
    /// </summary>
    /// <param name="info">The facts about the source.</param>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when a side exceeds <see cref="Surface.MaxDimension"/>.</exception>
    private static void EnsureStoredSizeSupported(SourceInfo info)
    {
        if (info.Width > Surface.MaxDimension || info.Height > Surface.MaxDimension)
        {
            throw new ToolException(
                ErrorCodes.SizeLimit,
                $"'{info.Path}' is {info.Width}x{info.Height} pixels, which is larger than the maximum of {Surface.MaxDimension} pixels per side.");
        }
    }

    /// <summary>
    ///     Formats a color as <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> when it is not opaque.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The text.</returns>
    internal static string FormatColor(Rgba32 color)
    {
        return color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }

    /// <summary>
    ///     Creates an operation record from alternating names and values.
    /// </summary>
    /// <param name="op">The operation name.</param>
    /// <param name="fields">The parameters, in the order they are written.</param>
    /// <returns>The record.</returns>
    internal static AppliedOperation Operation(string op, params (string Name, object Value)[] fields)
    {
        return new AppliedOperation(op, fields.Select(f => new KeyValuePair<string, object>(f.Name, f.Value)).ToList());
    }

    /// <summary>
    ///     Runs a read of a source file, mapping the library exceptions that mean "bad input" to a
    ///     <see cref="ToolException"/>.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="path">The path of the file, for messages.</param>
    /// <param name="kind">The kind being read, for messages, or <see langword="null"/> when it is not known yet.</param>
    /// <param name="action">The read.</param>
    /// <returns>The result of <paramref name="action"/>.</returns>
    /// <exception cref="ToolException">
    ///     Thrown for the allow-listed exceptions: a missing file or directory (<c>input-not-found</c>), an access
    ///     failure or any other I/O failure while reading (<c>input-unreadable</c>), an unsupported image feature
    ///     (<c>unsupported-feature</c>), and invalid, truncated, or malformed data - <see cref="InvalidDataException"/>,
    ///     <see cref="XmlException"/>, <see cref="EndOfStreamException"/>, and <see cref="FormatException"/>
    ///     (<c>corrupt-input</c>).
    /// </exception>
    /// <remarks>
    ///     This is deliberately not a catch-all: an unexpected exception (a null reference, an index out of range, an
    ///     out-of-memory failure) is a defect and is allowed to escape to the entry point, which reports and re-throws it.
    ///     A truncated or garbage file that produces such an exception is a gap in this list and is fixed by extending
    ///     the list.
    /// </remarks>
    internal static T Read<T>(string path, SourceKind? kind, Func<T> action)
    {
        var what = kind?.ToFormatName() ?? "image";
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ToolException(ErrorCodes.InputNotFound, $"'{path}' does not exist.", "Check the path.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ToolException(ErrorCodes.InputUnreadable, $"'{path}' cannot be read: {OneLine(ex.Message)}", null, ex);
        }
        catch (UnsupportedImageFeatureException ex)
        {
            throw new ToolException(
                ErrorCodes.UnsupportedFeature,
                $"'{path}' uses a {what} feature that is not supported: {OneLine(ex.Message)}",
                null,
                ex);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or EndOfStreamException or FormatException)
        {
            throw new ToolException(ErrorCodes.CorruptInput, $"'{path}' is not a valid {what} file: {OneLine(ex.Message)}", null, ex);
        }
        catch (IOException ex)
        {
            throw new ToolException(ErrorCodes.InputUnreadable, $"'{path}' cannot be read: {OneLine(ex.Message)}", null, ex);
        }
    }

    /// <summary>
    ///     Reduces a library message to one line, so that an error stays a single concise line.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The first line of the message, without trailing whitespace.</returns>
    private static string OneLine(string message)
    {
        var end = message.IndexOfAny(['\r', '\n']);
        return (end < 0 ? message : message[..end]).TrimEnd();
    }
}

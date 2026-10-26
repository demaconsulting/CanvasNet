// cspell:ignore canvasnet jpeg tiff jpeg2000 pptx vsdx scad Visio RRGGBB RRGGBBAA
using System.Globalization;
using System.Xml;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     Detects, probes, and loads the files the tool reads: it turns a path into a <see cref="SourceInfo"/> or into
///     pixels.
/// </summary>
/// <remarks>
///     <para>
///         The class is split into partial files by concern: this file holds the public operations and the
///         shared helpers (size policy, error mapping); <c>SourceLoader.Detect.cs</c> recognizes the format;
///         <c>SourceLoader.Raster.cs</c> and <c>SourceLoader.Svg.cs</c> handle the single-image sources;
///         <c>SourceLoader.Documents.cs</c> holds what the page-based sources share, and <c>SourceLoader.Pdf.cs</c>,
///         <c>SourceLoader.Pptx.cs</c>, and <c>SourceLoader.Vsdx.cs</c> handle the documents; <c>SourceLoader.Scad.cs</c>
///         handles an OpenSCAD model drawn from one viewpoint. A further source kind is
///         added by a new partial file with a probe method and a page source and one new case in each of
///         <see cref="Probe"/> and <see cref="OpenPages"/>.
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
    /// <returns>The facts; for a document they include every page.</returns>
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
            SourceKind.Pdf => ProbePdf(path, bytes),
            SourceKind.Pptx => ProbePptx(path, bytes),
            SourceKind.Vsdx => ProbeVsdx(path, bytes),
            SourceKind.Scad => ProbeScad(path, bytes),
            _ => throw UnhandledKind(kind)
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
    ///     Thrown for a missing, unreadable, unsupported, or corrupt file, with code <c>page-out-of-range</c> when
    ///     <see cref="SourceOptions.Page"/> is not a page of the source, and with code <c>size-limit</c> when the
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

        using var pages = OpenPages(info, options);
        return pages.ReadPage(options.Page);
    }

    /// <summary>
    ///     Opens a source so that one or more of its pages can be loaded.
    /// </summary>
    /// <param name="info">The facts about the file, from <see cref="Probe"/>.</param>
    /// <param name="options">The loading options; <see cref="SourceOptions.Page"/> is not used, the page is passed to <see cref="PageSource.ReadPage"/>.</param>
    /// <returns>
    ///     The page source, which the caller disposes. A document is parsed once when it is opened, so loading every
    ///     page of it does not parse it again for each page.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ToolException">
    ///     Thrown when the file cannot be opened (see <see cref="Read{T}"/>) or when <see cref="SourceOptions.Dpi"/> does
    ///     not suit the source.
    /// </exception>
    public static PageSource OpenPages(SourceInfo info, SourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(options);

        return info.Format switch
        {
            SourceKind.Png or SourceKind.Jpeg or SourceKind.Bmp or SourceKind.Tiff or SourceKind.Gif or SourceKind.Jpeg2000
                or SourceKind.Svg => new ImagePageSource(info, options),
            SourceKind.Pdf => PdfPageSource.Open(info, options),
            SourceKind.Pptx => PptxPageSource.Open(info, options),
            SourceKind.Vsdx => VsdxPageSource.Open(info, options),
            SourceKind.Scad => ScadPageSource.Open(info, options),
            _ => throw UnhandledKind(info.Format)
        };
    }

    /// <summary>
    ///     A source opened for loading its pages, one at a time.
    /// </summary>
    internal abstract class PageSource : IDisposable
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PageSource"/> class.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        protected PageSource(SourceInfo info, SourceOptions options)
        {
            Info = info;
            Options = options;
        }

        /// <summary>
        ///     Gets the facts about the file.
        /// </summary>
        public SourceInfo Info { get; }

        /// <summary>
        ///     Gets the loading options.
        /// </summary>
        protected SourceOptions Options { get; }

        /// <summary>
        ///     Loads one page.
        /// </summary>
        /// <param name="page">The 1-based page number.</param>
        /// <returns>The loaded page; the caller disposes it.</returns>
        /// <exception cref="ToolException">
        ///     Thrown with code <c>page-out-of-range</c> when <paramref name="page"/> is not a page of the source, and
        ///     for the failures described at <see cref="SourceLoader.Load(string, SourceOptions)"/>.
        /// </exception>
        public LoadedSource ReadPage(int page)
        {
            CheckPage(Info, page);
            return LoadPageCore(page);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Loads a page that is known to exist.
        /// </summary>
        /// <param name="page">The 1-based page number.</param>
        /// <returns>The loaded page.</returns>
        protected abstract LoadedSource LoadPageCore(int page);

        /// <summary>
        ///     Releases what the source holds.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }

    /// <summary>
    ///     A single-image source (a raster image or an SVG drawing), which has one page.
    /// </summary>
    private sealed class ImagePageSource : PageSource
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ImagePageSource"/> class.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        public ImagePageSource(SourceInfo info, SourceOptions options)
            : base(info, options)
        {
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            return Info.Format == SourceKind.Svg ? LoadSvg(Info, Options) : LoadRaster(Info, Options);
        }
    }

    /// <summary>
    ///     Checks that a page exists in a source.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <exception cref="ToolException">Thrown with code <c>page-out-of-range</c> when the source has no such page.</exception>
    internal static void CheckPage(SourceInfo info, int page)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (page < 1 || page > info.PageCount)
        {
            var pages = info.PageCount == 1 ? "has 1 page" : $"has {info.PageCount} pages";
            throw new ToolException(
                ErrorCodes.PageOutOfRange,
                $"Page {page} does not exist: '{info.Path}' {pages}.",
                "Pages are numbered from 1.");
        }
    }

    /// <summary>
    ///     Creates the exception for a source kind that the loader has no case for: a defect, since every kind of
    ///     <see cref="SourceKind"/> is handled.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The exception to throw; it is not a <see cref="ToolException"/>, so the entry point reports it as unexpected.</returns>
    private static InvalidOperationException UnhandledKind(SourceKind kind)
    {
        return new InvalidOperationException($"The source kind {kind} is not handled by the source loader.");
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
    ///     failure or any other I/O failure while reading (<c>input-unreadable</c>), an unsupported feature -
    ///     <see cref="UnsupportedImageFeatureException"/>, <see cref="PptxUnsupportedFeatureException"/>, and
    ///     <see cref="VsdxUnsupportedFeatureException"/>, and <see cref="ScadUnsupportedFeatureException"/>
    ///     (<c>unsupported-feature</c>), a syntax error of a model - <see cref="ScadSyntaxException"/> - or a failure
    ///     of its evaluation - <see cref="ScadEvaluationException"/> (<c>scad-error</c>, or <c>budget-exceeded</c> when a
    ///     limit was exceeded; see <see cref="MapScadEvaluation"/>), and
    ///     invalid, truncated, or malformed data - <see cref="InvalidDataException"/>,
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
        catch (Exception ex) when (ex is UnsupportedImageFeatureException or PptxUnsupportedFeatureException or VsdxUnsupportedFeatureException)
        {
            var feature = ex switch
            {
                UnsupportedImageFeatureException image => image.Feature,
                PptxUnsupportedFeatureException presentation => presentation.Feature,
                VsdxUnsupportedFeatureException drawing => drawing.Feature,
                _ => string.Empty
            };
            var named = feature.Length > 0 ? $" ({feature})" : string.Empty;
            throw new ToolException(
                ErrorCodes.UnsupportedFeature,
                $"'{path}' uses a {what} feature that is not supported{named}: {OneLine(ex.Message)}",
                null,
                ex);
        }
        catch (ScadUnsupportedFeatureException ex)
        {
            // The message of the library already names the file and the position
            throw new ToolException(
                ErrorCodes.UnsupportedFeature,
                $"The SCAD model uses a feature that is not supported ({ex.Feature}): {OneLine(ex.Message)}",
                "Without --strict the feature is left out and reported in the diagnostics.",
                ex);
        }
        catch (ScadSyntaxException ex)
        {
            throw new ToolException(ErrorCodes.ScadError, $"SCAD syntax error: {OneLine(ex.Message)}", null, ex);
        }
        catch (ScadEvaluationException ex)
        {
            throw MapScadEvaluation(ex);
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

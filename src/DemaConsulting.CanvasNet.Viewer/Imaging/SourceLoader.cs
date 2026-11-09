// cspell:ignore canvasnet jpeg tiff jpeg2000 pptx vsdx scad Visio RRGGBB RRGGBBAA
using System.Globalization;
using System.Xml;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     Detects, probes, and loads the files the imaging operations read: it turns a path into a
///     <see cref="SourceInfo"/> or into pixels.
/// </summary>
/// <remarks>
///     <para>
///         The class is split into partial files by concern: this file holds the public operations and the
///         shared helpers (size policy, error mapping); <c>SourceLoader.Detect.cs</c> recognizes the format;
///         <c>SourceLoader.Raster.cs</c> and <c>SourceLoader.Svg.cs</c> handle the single-image sources;
///         <c>SourceLoader.Documents.cs</c> holds what the page-based sources share, and <c>SourceLoader.Pdf.cs</c>,
///         <c>SourceLoader.Pptx.cs</c>, and <c>SourceLoader.Vsdx.cs</c> handle the documents; <c>SourceLoader.Scad.cs</c>
///         handles an OpenSCAD model drawn from one viewpoint, <c>SourceLoader.VegaLite.cs</c> handles a Vega-Lite
///         chart, and <c>SourceLoader.Mermaid.cs</c> handles a Mermaid diagram. A further source kind is
///         added by a new partial file with a probe method and a page source and one new case in each of
///         <see cref="Probe(string)"/> and <see cref="OpenPages"/>.
///     </para>
///     <para>
///         The loader never lets an expected failure escape as a library exception. The library exceptions that
///         mean "this file is missing, unreadable, corrupt, or uses something unsupported" are mapped, by an
///         explicit allow-list of types in <see cref="Read{T}"/>, to a <see cref="ViewerException"/> with a stable
///         code; every other exception is a defect and propagates so that the entry point re-throws it. An image
///         larger than <see cref="Surface.MaxDimension"/> is refused with <c>size-limit</c> before it is decoded
///         wherever the file header gives the size.
///     </para>
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     Reads the facts about a file without rendering it.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The facts; for a document they include every page.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown for a missing, unreadable, unsupported, or corrupt file (see <see cref="Detect"/> and the mapping
    ///     table in <see cref="Read{T}"/>).
    /// </exception>
    public static SourceInfo Probe(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var kind = Detect(path);
        return ProbeInput(SourceInput.FromFile(path), kind);
    }

    /// <summary>
    ///     Reads the facts about the file of an input that may have a fragment (<c>part.scad#def.width=20</c>), and checks the
    ///     fragment against the kind of the file.
    /// </summary>
    /// <param name="input">The input, from <see cref="InputReference.Parse(string)"/>.</param>
    /// <returns>
    ///     The facts about the file, as <see cref="Probe(string)"/> gives them, with <see cref="SourceInfo.Selection"/> set
    ///     when the input has a fragment; every operation of this namespace that is given the facts applies the selection to
    ///     the options it loads the source with.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown for the failures of <see cref="Probe(string)"/>, with code <c>image-fragment-invalid</c> when the
    ///     fragment or a value in it cannot be used, and with code <c>page-out-of-range</c> when it names a page the file does
    ///     not have. When the file is missing and the input has a fragment, the message says that a <c>#</c> starts a fragment
    ///     unless a file with that exact name exists.
    /// </exception>
    public static SourceInfo Probe(InputReference input)
    {
        ArgumentNullException.ThrowIfNull(input);

        SourceInfo info;
        try
        {
            info = Probe(input.Path);
        }
        catch (ViewerException ex) when (ex.Code == ErrorCodes.InputNotFound && input.Fragment.Length > 0)
        {
            throw new ViewerException(
                ErrorCodes.InputNotFound,
                $"'{input.Path}' does not exist (and neither does a file named '{input.Text}').",
                "A '#' in an input starts a fragment (page=3, def.width=20) unless a file with that exact name exists. Check the path.",
                ex);
        }

        return input.Fragment.Length == 0 ? info : info with { Selection = InputSelection.Create(input, info) };
    }

    /// <summary>
    ///     Reads the facts about a source of a known kind without rendering it.
    /// </summary>
    /// <param name="input">Where the bytes come from.</param>
    /// <param name="kind">The kind of the source, already recognized by the caller.</param>
    /// <returns>The facts; for a document they include every page.</returns>
    /// <exception cref="ViewerException">Thrown for an unreadable or corrupt source (see <see cref="Read{T}"/>).</exception>
    internal static SourceInfo ProbeInput(SourceInput input, SourceKind kind)
    {
        var bytes = Read(input.Name, null, () => input.Length);
        return kind switch
        {
            SourceKind.Png or SourceKind.Jpeg or SourceKind.Bmp or SourceKind.Tiff or SourceKind.Gif or SourceKind.Jpeg2000
                => ProbeRaster(input, kind, bytes),
            SourceKind.Svg => ProbeSvg(input, bytes),
            SourceKind.Pdf => ProbePdf(input, bytes),
            SourceKind.Pptx => ProbePptx(input, bytes),
            SourceKind.Vsdx => ProbeVsdx(input, bytes),
            SourceKind.Scad => ProbeScad(input, bytes),
            SourceKind.VegaLite => ProbeVegaLite(input, bytes),
            SourceKind.Mermaid => ProbeMermaid(input, bytes),
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
    ///     source is decoded at its own size and then oriented, resized, and flattened as the options say. The caller
    ///     disposes the <see cref="LoadedSource"/> (it owns the surface).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown for a missing, unreadable, unsupported, or corrupt file, with code <c>page-out-of-range</c> when
    ///     <see cref="SourceOptions.Page"/> is not a page of the source, with code <c>size-limit</c> when the
    ///     source or the requested size is larger than <see cref="Surface.MaxDimension"/> pixels per side, and with
    ///     code <c>usage-error</c>, before the file is touched, when <paramref name="options"/> is not usable (a size or
    ///     scale out of range, a resolution above <see cref="MaxDpi"/>, an undefined filter, a model view, angle, time,
    ///     triangle limit, or definition that is not valid, or a Mermaid theme that does not exist).
    /// </exception>
    public static LoadedSource Load(string path, SourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(options);

        // The options are checked first, so a mistake is reported the same way whatever the file is
        ValidateOptions(options);
        return Load(Probe(path), options);
    }

    /// <summary>
    ///     Loads a file into pixels, given the facts that <see cref="Probe(string)"/> already read.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="options">The loading options.</param>
    /// <returns>
    ///     The pixels and the facts about the source; see <see cref="Load(string, SourceOptions)"/>. The caller disposes
    ///     the <see cref="LoadedSource"/> (it owns the surface).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ViewerException">See <see cref="Load(string, SourceOptions)"/>.</exception>
    public static LoadedSource Load(SourceInfo info, SourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(options);

        // The page may be the one the fragment of the input names
        options = ApplySelection(info, options);
        using var pages = OpenPages(info, options);
        return pages.ReadPage(options.Page);
    }

    /// <summary>
    ///     Lays what the fragment of an input chose over the options of a command.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="options">The options of the command.</param>
    /// <returns>The options with the selection applied, or <paramref name="options"/> when the input had no fragment.</returns>
    internal static SourceOptions ApplySelection(SourceInfo info, SourceOptions options)
    {
        return info.Selection is { } selection ? selection.ApplyTo(options) : options;
    }

    /// <summary>
    ///     Opens a source so that one or more of its pages can be loaded.
    /// </summary>
    /// <param name="info">The facts about the file, from <see cref="Probe(string)"/>.</param>
    /// <param name="options">The loading options; <see cref="SourceOptions.Page"/> is not used, the page is passed to <see cref="PageSource.ReadPage"/>.</param>
    /// <returns>
    ///     The page source, which the caller disposes. A document is parsed once when it is opened, so loading every
    ///     page of it does not parse it again for each page.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ViewerException">
    ///     Thrown when the file cannot be opened (see <see cref="Read{T}"/>), with code <c>usage-error</c> when
    ///     <paramref name="options"/> is not usable (see <see cref="Load(string, SourceOptions)"/>) or
    ///     <see cref="SourceOptions.Dpi"/> does not suit the source.
    /// </exception>
    public static PageSource OpenPages(SourceInfo info, SourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(options);

        return OpenInputPages(SourceInput.FromFile(info.Path), info, ApplySelection(info, options), LoadContext.Default);
    }

    /// <summary>
    ///     Reads the facts about a source of a known kind and keeps a document open, so a render of it does not parse it again.
    /// </summary>
    /// <param name="input">Where the bytes come from.</param>
    /// <param name="kind">The kind of the source, already recognized by the caller.</param>
    /// <returns>The facts and, for a PDF, presentation or drawing, the parsed document; the caller disposes it.</returns>
    /// <exception cref="ViewerException">Thrown for an unreadable or corrupt source (see <see cref="Read{T}"/>).</exception>
    internal static OpenedSource OpenInput(SourceInput input, SourceKind kind)
    {
        switch (kind)
        {
            case SourceKind.Pdf:
                return KeepOpen(Read(input.Name, SourceKind.Pdf, () => OpenPdf(input)), (d, bytes) => DescribePdf(input.Name, d, bytes), input);
            case SourceKind.Pptx:
                return KeepOpen(Read(input.Name, SourceKind.Pptx, () => OpenPptx(input)), (d, bytes) => DescribePptx(input.Name, d, bytes), input);
            case SourceKind.Vsdx:
                return KeepOpen(Read(input.Name, SourceKind.Vsdx, () => OpenVsdx(input)), (d, bytes) => DescribeVsdx(input.Name, d, bytes), input);
            default:
                return new OpenedSource(ProbeInput(input, kind), null);
        }
    }

    /// <summary>
    ///     Describes an opened document and keeps it open, disposing it when it cannot be described.
    /// </summary>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="document">The opened document; ownership passes to the result.</param>
    /// <param name="describe">Reads the facts about the document, given the size of the file.</param>
    /// <param name="input">Where the bytes come from.</param>
    /// <returns>The opened source.</returns>
    private static OpenedSource KeepOpen<T>(T document, Func<T, long, SourceInfo> describe, SourceInput input)
        where T : IDisposable
    {
        try
        {
            var bytes = Read(input.Name, null, () => input.Length);
            return new OpenedSource(describe(document, bytes), document);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Opens a source, wherever its bytes come from, so that its pages can be loaded.
    /// </summary>
    /// <param name="input">Where the bytes come from.</param>
    /// <param name="info">The facts about the source, from <see cref="ProbeInput"/>.</param>
    /// <param name="options">The loading options.</param>
    /// <param name="context">The hooks of the caller.</param>
    /// <param name="opened">The source as <see cref="OpenInput"/> opened it, whose parsed document is reused, or <see langword="null"/> to parse it again.</param>
    /// <returns>The page source, which the caller disposes.</returns>
    /// <exception cref="ViewerException">See <see cref="OpenPages(SourceInfo, SourceOptions)"/>.</exception>
    internal static PageSource OpenInputPages(SourceInput input, SourceInfo info, SourceOptions options, LoadContext context, OpenedSource? opened = null)
    {
        // The options are checked before the source is opened, so a mistake costs no I/O
        ValidateOptions(options);
        return info.Format switch
        {
            SourceKind.Png or SourceKind.Jpeg or SourceKind.Bmp or SourceKind.Tiff or SourceKind.Gif or SourceKind.Jpeg2000
                or SourceKind.Svg => new ImagePageSource(info, options, input, context),
            SourceKind.Pdf => PdfPageSource.Open(input, info, options, context, opened),
            SourceKind.Pptx => PptxPageSource.Open(input, info, options, context, opened),
            SourceKind.Vsdx => VsdxPageSource.Open(input, info, options, context, opened),
            SourceKind.Scad => ScadPageSource.Open(input, info, options, context),
            SourceKind.VegaLite => VegaLitePageSource.Open(input, info, options, context),
            SourceKind.Mermaid => MermaidPageSource.Open(input, info, options, context),
            _ => throw UnhandledKind(info.Format)
        };
    }

    /// <summary>
    ///     A source opened for loading its pages, one at a time.
    /// </summary>
    /// <remarks>
    ///     The class cannot be derived from outside the library: the kinds of source are the ones the loader knows.
    ///     Dispose the page source when the pages are read. A page that <see cref="ReadPage"/> returned does not depend
    ///     on the source, so it stays valid after the source is disposed; dispose each page too.
    /// </remarks>
    public abstract class PageSource : IDisposable
    {
        /// <summary>
        ///     Whether the warnings about the fragment of the input were added to a page already.
        /// </summary>
        private bool _fragmentNotesReported;

        /// <summary>
        ///     Initializes a new instance of the <see cref="PageSource"/> class.
        /// </summary>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="input">Where the bytes of the source come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        private protected PageSource(SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
        {
            Info = info;
            Options = options;
            Input = input;
            Context = context;
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
        ///     Gets where the bytes of the source come from.
        /// </summary>
        private protected SourceInput Input { get; }

        /// <summary>
        ///     Gets the hooks of the caller: cancellation, the pixel budget and the resolver of nested pictures.
        /// </summary>
        private protected LoadContext Context { get; }

        /// <summary>
        ///     Loads one page.
        /// </summary>
        /// <param name="page">The 1-based page number.</param>
        /// <returns>The loaded page; the caller disposes it.</returns>
        /// <exception cref="ViewerException">
        ///     Thrown with code <c>page-out-of-range</c> when <paramref name="page"/> is not a page of the source, and
        ///     for the failures described at <see cref="SourceLoader.Load(string, SourceOptions)"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown when the caller canceled the load.</exception>
        /// <exception cref="ImageSources.ImageSourceBudgetException">Thrown when the caller's pixel budget cannot hold the page.</exception>
        public LoadedSource ReadPage(int page)
        {
            CheckPage(Info, page);
            Context.CancellationToken.ThrowIfCancellationRequested();
            var loaded = LoadPageCore(page);

            // The warnings about the fragment of the input are reported once, with the first page that is loaded
            if (!_fragmentNotesReported && Info.Selection is { Diagnostics.Count: > 0 } selection)
            {
                _fragmentNotesReported = true;
                loaded = loaded with { Diagnostics = [.. selection.Diagnostics, .. loaded.Diagnostics] };
            }

            return loaded;
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
        /// <param name="input">Where the bytes of the source come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        public ImagePageSource(SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
            : base(info, options, input, context)
        {
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            return Info.Format == SourceKind.Svg ? LoadSvg(Info, Options, Input, Context) : LoadRaster(Info, Options, Input, Context);
        }
    }

    /// <summary>
    ///     Gets the options that load a source into a box of a given size.
    /// </summary>
    /// <param name="info">The facts about the source.</param>
    /// <param name="options">The loading options.</param>
    /// <param name="width">The width of the box in pixels.</param>
    /// <param name="height">The height of the box in pixels.</param>
    /// <returns>
    ///     For a vector source (see <see cref="SourceKindExtensions.IsVector"/>), a copy of <paramref name="options"/>
    ///     whose <see cref="SourceOptions.Width"/> and <see cref="SourceOptions.Height"/> are the box and whose
    ///     <see cref="SourceOptions.Scale"/> is cleared, so that the source is rendered sharply at the box (a document
    ///     page is fitted inside it and centered, an SVG follows its own aspect-ratio rule, and a model fills it).
    ///     For a raster source, <paramref name="options"/> unchanged: the caller fits the loaded pixels with
    ///     <see cref="Surface.FitInto"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="info"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    internal static SourceOptions BoxOptions(SourceInfo info, SourceOptions options, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(options);

        return info.Format.IsVector()
            ? options with { Width = width, Height = height, Scale = null }
            : options;
    }

    /// <summary>
    ///     Checks that a page exists in a source.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="page">The 1-based page number (the first page is 1).</param>
    /// <exception cref="ViewerException">Thrown with code <c>page-out-of-range</c> when the source has no such page.</exception>
    /// <remarks>
    ///     <see cref="Load(SourceInfo, SourceOptions)"/> and the operations of this namespace check the page themselves
    ///     before they decode anything; call this method first only to get the same <c>page-out-of-range</c> error earlier,
    ///     for example before you create an output folder.
    /// </remarks>
    public static void CheckPage(SourceInfo info, int page)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (page < 1 || page > info.PageCount)
        {
            var pages = info.PageCount == 1 ? "has 1 page" : $"has {info.PageCount} pages";
            throw new ViewerException(
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
    /// <returns>The exception to throw; it is not a <see cref="ViewerException"/>, so the entry point reports it as unexpected.</returns>
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
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when a side would exceed <see cref="Surface.MaxDimension"/>.</exception>
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
            throw new ViewerException(
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
    /// <exception cref="ViewerException">Thrown with code <c>size-limit</c> when a side exceeds <see cref="Surface.MaxDimension"/>.</exception>
    private static void EnsureStoredSizeSupported(SourceInfo info)
    {
        if (info.Width > Surface.MaxDimension || info.Height > Surface.MaxDimension)
        {
            throw new ViewerException(
                ErrorCodes.SizeLimit,
                $"'{info.Path}' is {info.Width}x{info.Height} pixels, which is larger than the maximum of {Surface.MaxDimension} pixels per side.");
        }
    }

    /// <summary>
    ///     Formats a color as <c>#RRGGBB</c>, or <c>#RRGGBBAA</c> when it is not opaque.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <returns>The text.</returns>
    public static string FormatColor(Rgba32 color)
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
    ///     <see cref="ViewerException"/>.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="path">The path of the file, for messages.</param>
    /// <param name="kind">The kind being read, for messages, or <see langword="null"/> when it is not known yet.</param>
    /// <param name="action">The read.</param>
    /// <returns>The result of <paramref name="action"/>.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown for the allow-listed exceptions: a missing file or directory (<c>input-not-found</c>), an access
    ///     failure or any other I/O failure while reading (<c>input-unreadable</c>), an unsupported feature -
    ///     <see cref="UnsupportedImageFeatureException"/>, <see cref="PptxUnsupportedFeatureException"/>, and
    ///     <see cref="VsdxUnsupportedFeatureException"/>, and <see cref="ScadUnsupportedFeatureException"/>
    ///     (<c>unsupported-feature</c>), a syntax error of a model - <see cref="ScadSyntaxException"/> - or a failure
    ///     of its evaluation - <see cref="ScadEvaluationException"/> (<c>scad-error</c>, or <c>budget-exceeded</c> when a
    ///     limit was exceeded; see <see cref="MapScadEvaluation"/>), <see cref="VegaLiteUnsupportedFeatureException"/>
    ///     (<c>unsupported-feature</c>), an invalid chart specification - <see cref="VegaLiteSpecException"/> - or a
    ///     chart that cannot be rendered - <see cref="VegaLiteRenderException"/> (<c>vega-lite-error</c>, or
    ///     <c>budget-exceeded</c> when a limit was exceeded; see <see cref="MapVegaLiteRender"/>),
    ///     <see cref="MermaidUnsupportedFeatureException"/> (<c>unsupported-feature</c>), a diagram that is not valid -
    ///     <see cref="MermaidSyntaxException"/> - or that cannot be laid out or drawn - <see cref="MermaidRenderException"/>
    ///     (<c>mermaid-error</c> with the hint of the library, or <c>budget-exceeded</c> when a limit was exceeded; see
    ///     <see cref="MapMermaid"/>), and
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
            throw new ViewerException(ErrorCodes.InputNotFound, $"'{path}' does not exist.", "Check the path.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ViewerException(ErrorCodes.InputUnreadable, $"'{path}' cannot be read: {OneLine(ex.Message)}", null, ex);
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
            throw new ViewerException(
                ErrorCodes.UnsupportedFeature,
                $"'{path}' uses a {what} feature that is not supported{named}: {OneLine(ex.Message)}",
                null,
                ex);
        }
        catch (ScadUnsupportedFeatureException ex)
        {
            // The message of the library already names the file and the position
            throw new ViewerException(
                ErrorCodes.UnsupportedFeature,
                $"The SCAD model uses a feature that is not supported ({ex.Feature}): {OneLine(ex.Message)}",
                "Without --strict the feature is left out and reported in the diagnostics.",
                ex);
        }
        catch (ScadSyntaxException ex)
        {
            throw new ViewerException(ErrorCodes.ScadError, $"SCAD syntax error: {OneLine(ex.Message)}", null, ex);
        }
        catch (ScadEvaluationException ex)
        {
            throw MapScadEvaluation(ex);
        }
        catch (VegaLiteUnsupportedFeatureException ex)
        {
            // The message of the library already names the file, the position, and the pointer
            throw new ViewerException(
                ErrorCodes.UnsupportedFeature,
                $"The Vega-Lite chart uses a feature that is not supported ({ex.Feature}): {OneLine(ex.Message)}",
                "Without --strict the feature is left out and reported in the diagnostics.",
                ex);
        }
        catch (VegaLiteSpecException ex)
        {
            throw new ViewerException(ErrorCodes.VegaLiteError, $"Vega-Lite specification error: {OneLine(ex.Message)}", null, ex);
        }
        catch (VegaLiteRenderException ex)
        {
            throw MapVegaLiteRender(ex);
        }
        catch (MermaidUnsupportedFeatureException ex)
        {
            // An IOException, so it comes before the input-unreadable mapping; the message already names the file and the position
            throw new ViewerException(
                ErrorCodes.UnsupportedFeature,
                $"The Mermaid diagram uses a feature that is not supported ({ex.Feature}): {OneLine(ex.Message)}",
                ex.Hint is { } hint
                    ? $"{hint}. Without --strict the feature is left out and reported in the diagnostics."
                    : "Without --strict the feature is left out and reported in the diagnostics.",
                ex);
        }
        catch (MermaidSyntaxException ex)
        {
            // A FormatException, so it comes before the corrupt-input mapping
            throw MapMermaid(ex.Code, "syntax", Named(path, ex.Message, ex.Location.IsKnown), ex.Hint, ex);
        }
        catch (MermaidRenderException ex)
        {
            throw MapMermaid(ex.Code, "render", Named(path, ex.Message, ex.Location.IsKnown), ex.Hint, ex);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or EndOfStreamException or FormatException)
        {
            throw new ViewerException(ErrorCodes.CorruptInput, $"'{path}' is not a valid {what} file: {OneLine(ex.Message)}", null, ex);
        }
        catch (IOException ex)
        {
            throw new ViewerException(ErrorCodes.InputUnreadable, $"'{path}' cannot be read: {OneLine(ex.Message)}", null, ex);
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

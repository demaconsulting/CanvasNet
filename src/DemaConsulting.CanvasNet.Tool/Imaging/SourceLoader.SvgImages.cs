// cspell:ignore svg scad href vsdx pptx embedder
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The pictures an SVG file embeds with <c>&lt;image href="..."&gt;</c>: models, charts, other SVG files, pages of
///     documents, and rasters.
/// </summary>
/// <remarks>
///     <para>
///         The SVG library never reads a file for an <c>image</c> reference on its own; it asks the
///         <see cref="IImageResolver"/> that <see cref="SvgLoadOptions.ImageResolver"/> carries. The tool hands it the
///         resolver of <see cref="CanvasNetViewer.CreateImageResolver(string, FolderImageResolverOptions?)"/>, confined
///         to the folder of the SVG file the user named, so the sandbox, the limits, the dispatch by file name
///         extension and the nesting of SVG files are the library's (see <c>CanvasNetViewer.Extensions</c> for the
///         linkable types). This file only wires that resolver into the tool: it maps what the resolver reports to
///         the tool's notes, text and operations, and applies the tool-only policy of <c>--strict</c> and of the
///         error codes.
///     </para>
///     <para>
///         A reference that cannot be resolved (missing, outside the folder, a network URL, an unknown format, a cycle,
///         or over a limit) is a warning <c>svg-image-unresolved</c> and draws nothing, and so is a picture that is too
///         large for the pixel budget (<c>svg-image-too-large</c>); with
///         <see cref="SourceOptions.Strict"/> either is an <c>unsupported-feature</c> error. A file that is found but is
///         broken (corrupt, a model with a syntax error, a chart with a bad specification) is an error that names both
///         files.
///     </para>
/// </remarks>
internal static partial class SourceLoader
{
    /// <summary>
    ///     The pattern of the message of an unresolved reference: the link and the reason.
    /// </summary>
    private static readonly Regex UnresolvedMessage = new(
        "^image '(?<href>.*)' was not drawn: (?<reason>.*)$",
        RegexOptions.CultureInvariant | RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     The embedded pictures of one SVG file that is rendered: the resolver that serves it and the mapping of what
    ///     the resolver reports.
    /// </summary>
    internal sealed class SvgImages
    {
        /// <summary>
        ///     The name of the SVG file being rendered, for messages.
        /// </summary>
        private readonly string _svgName;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SvgImages"/> class.
        /// </summary>
        /// <param name="svgPath">The path of the SVG file being rendered; the folder of the file is the sandbox.</param>
        /// <param name="options">The loading options of the SVG file.</param>
        public SvgImages(string svgPath, SourceOptions options)
        {
            var fullPath = Path.GetFullPath(svgPath);
            _svgName = Path.GetFileName(fullPath);
            var folder = Path.GetDirectoryName(fullPath) ?? Path.GetPathRoot(fullPath) ?? fullPath;
            Resolver = CanvasNetViewer.CreateImageResolver(
                folder,
                new FolderImageResolverOptions { ThrowOnUnsupportedFeature = options.Strict });
        }

        /// <summary>
        ///     Gets the resolver for <see cref="SvgLoadOptions.ImageResolver"/>.
        /// </summary>
        public FolderImageResolver Resolver { get; }

        /// <summary>
        ///     Gets the notes reported for the embedded files: unresolved references and the notes of the files themselves.
        /// </summary>
        /// <returns>The notes, in the order they were reported.</returns>
        public List<ImageDiagnostic> Notes()
        {
            return [.. Resolver.Diagnostics.Select(d => new ImageDiagnostic(d.Source, SeverityName(d.Severity), d.Code, d.Message))];
        }

        /// <summary>
        ///     Gets one <c>embed</c> operation for every file the SVG file itself embedded and that was drawn.
        /// </summary>
        /// <returns>The operations; files embedded by embedded SVG files are not listed.</returns>
        public List<AppliedOperation> Operations()
        {
            return [.. Resolver.Embedded
                .Where(e => e.Depth == 0)
                .Select(e => Operation("embed", ("href", e.Href), ("format", e.Format), ("width", e.Width), ("height", e.Height)))];
        }

        /// <summary>
        ///     Applies the <c>--strict</c> policy: a reference that was not drawn, or whose picture is too large for the
        ///     pixel budget, is an error.
        /// </summary>
        /// <exception cref="ToolException">Thrown with code <c>unsupported-feature</c> for the first such reference.</exception>
        public void ThrowForUnresolved()
        {
            var unresolved = Resolver.Diagnostics.FirstOrDefault(
                d => d.Code is FolderImageResolver.UnresolvedCode or FolderImageResolver.TooLargeCode);
            if (unresolved == null)
            {
                return;
            }

            var match = UnresolvedMessage.Match(unresolved.Message);
            var what = match.Success
                ? $"embeds '{match.Groups["href"].Value}', which was not drawn: {match.Groups["reason"].Value}"
                : $"embeds an image that was not drawn: {unresolved.Message}";
            throw new ToolException(
                ErrorCodes.UnsupportedFeature,
                $"'{_svgName}' {what}.",
                $"Without --strict the image is left out and reported as the warning {unresolved.Code}.");
        }

        /// <summary>
        ///     Maps the failure of a file that was found but could not be drawn to the tool's errors.
        /// </summary>
        /// <param name="failure">The failure the resolver reported.</param>
        /// <returns>
        ///     The exception to throw: the code and hint the tool gives the underlying failure, with a message that
        ///     names both files. An underlying failure that is not an allow-listed one is a defect and is rethrown as itself.
        /// </returns>
        public ToolException Map(ImageSourceException failure)
        {
            var inner = failure.InnerException ?? failure;
            try
            {
                Read<object>(failure.Href, KindOfName(failure.Href), () =>
                {
                    ExceptionDispatchInfo.Capture(inner).Throw();
                    return string.Empty;
                });
            }
            catch (ToolException ex)
            {
                var embedder = failure.Embedder ?? _svgName;
                return new ToolException(ex.Code, $"'{embedder}' embeds '{failure.Href}': {ex.Message}", ex.Hint, ex);
            }

            // Read maps every exception it lets through to a ToolException or rethrows it, so this is not reached
            throw failure;
        }

        /// <summary>
        ///     Gets the name of a severity as the tool reports it.
        /// </summary>
        /// <param name="severity">The severity.</param>
        /// <returns>The name: <c>info</c>, <c>warning</c>, or <c>unsupported</c>.</returns>
        private static string SeverityName(ImageDiagnosticSeverity severity)
        {
            return severity switch
            {
                ImageDiagnosticSeverity.Info => "info",
                ImageDiagnosticSeverity.Unsupported => "unsupported",
                _ => "warning"
            };
        }

        /// <summary>
        ///     Gets the kind of file a link names, for the wording of a message.
        /// </summary>
        /// <param name="href">The link as written.</param>
        /// <returns>The kind, or <see langword="null"/> when the name is not one of a known kind.</returns>
        private static SourceKind? KindOfName(string href)
        {
            // A query or fragment is not part of the file name (the resolver cuts it off too)
            var cut = href.AsSpan().IndexOfAny('?', '#');
            var name = cut < 0 ? href : href[..cut];
            if (name.EndsWith(VegaLiteJsonSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return SourceKind.VegaLite;
            }

            return ExtensionKinds.TryGetValue(Path.GetExtension(name), out var kind) ? kind : null;
        }
    }
}

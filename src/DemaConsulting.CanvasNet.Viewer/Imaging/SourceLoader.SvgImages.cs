// cspell:ignore svg scad href vsdx pptx embedder Emmd
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The pictures an SVG file embeds with <c>&lt;image href="..."&gt;</c> and the pictures the <c>image</c> marks of a Vega-Lite chart show (<c>"url"</c>): models, charts,
///     other SVG files, pages of documents, and rasters.
/// </summary>
/// <remarks>
///     <para>
///         The SVG library never reads a file for an <c>image</c> reference on its own; it asks the
///         <see cref="IImageResolver"/> that <see cref="SvgLoadOptions.ImageResolver"/> carries. The loader hands it the
///         resolver of <see cref="CanvasNetViewer.CreateImageResolver(string, FolderImageResolverOptions?)"/>, confined
///         to the folder of the SVG file the user named, so the sandbox, the limits, the dispatch by file name
///         extension and the nesting of SVG files are the library's (see <c>CanvasNetViewer.Extensions</c> for the
///         linkable types). This file only wires that resolver into the loader: it maps what the resolver reports to
///         the loader's notes, text and operations, and applies the loader-only policy of <c>--strict</c> and of the
///         error codes.
///     </para>
///     <para>
///         A reference that cannot be resolved (missing, outside the folder, a network URL, an unknown format, a cycle,
///         or over a limit) is a warning <c>svg-image-unresolved</c> and draws nothing, and so is a picture that is too
///         large for the pixel budget (<c>svg-image-too-large</c>); with
///         <see cref="SourceOptions.Strict"/> either is an <c>unsupported-feature</c> error, and so is a link whose
///         fragment cannot be used (<c>image-fragment-invalid</c>) or that asks for a page the document does not have
///         (<c>image-page-out-of-range</c>), which are not drawn either; an unknown or repeated fragment parameter
///         (<c>image-fragment-unknown-parameter</c>, <c>image-fragment-duplicate-parameter</c>) is a warning only. A
///         file that is found but is
///         broken (corrupt, a model with a syntax error, a chart with a bad specification) is an error that names both
///         files.
///     </para>
/// </remarks>
public static partial class SourceLoader
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
            return [.. Resolver.Diagnostics.Select(ToImageDiagnostic)];
        }

        /// <summary>
        ///     Converts a note of the resolver to the note shape of the loader, keeping every member.
        /// </summary>
        /// <param name="note">The note of the resolver.</param>
        /// <returns>The note with the same source, severity, code, message, page, detail, location and origin.</returns>
        internal static ImageDiagnostic ToImageDiagnostic(ImageSourceDiagnostic note)
        {
            return new ImageDiagnostic(note.Source, SeverityName(note.Severity), note.Code, note.Message, note.Page)
            {
                Detail = note.Detail,
                Location = note.Location,
                Origin = note.Origin
            };
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
        ///     Applies the <c>--strict</c> policy: a reference that was not drawn, whose picture is too large for the
        ///     pixel budget, whose fragment cannot be used, or that names a page the document does not have, is an error;
        ///     a fragment parameter that is unknown or given twice is only a warning.
        /// </summary>
        /// <exception cref="ViewerException">Thrown with code <c>unsupported-feature</c> for the first such reference.</exception>
        public void ThrowForUnresolved()
        {
            ThrowForUnresolved(Resolver.Diagnostics, _svgName, false);
        }

        /// <summary>
        ///     Applies the <c>--strict</c> policy to the notes of a resolver that served a whole render, for a render that
        ///     does not own the resolver of its nested pictures (the embedding path of <c>CanvasNetViewer</c>).
        /// </summary>
        /// <param name="notes">The notes the shared resolver collected, from every level of the render.</param>
        /// <param name="svgName">The name of the file that was rendered.</param>
        /// <param name="nameHolder">
        ///     <see langword="true"/> to name the file that holds the link (the last file of the note's origin) instead of
        ///     <paramref name="svgName"/>.
        /// </param>
        /// <exception cref="ViewerException">Thrown with code <c>unsupported-feature</c> for the first reference that was not drawn.</exception>
        internal static void ThrowForUnresolved(IEnumerable<ImageSourceDiagnostic> notes, string svgName, bool nameHolder = true)
        {
            var unresolved = notes.FirstOrDefault(
                d => d.Code is FolderImageResolver.UnresolvedCode
                    or FolderImageResolver.TooLargeCode
                    or FolderImageResolver.FragmentInvalidCode
                    or FolderImageResolver.PageOutOfRangeCode);
            if (unresolved == null)
            {
                return;
            }

            // The file that holds the link is the last one of the chain the note came through
            var holder = nameHolder && unresolved.Origin.Count > 0 ? unresolved.Origin[^1] : svgName;
            var match = UnresolvedMessage.Match(unresolved.Message);
            var what = match.Success
                ? $"embeds '{match.Groups["href"].Value}', which was not drawn: {match.Groups["reason"].Value}"
                : $"embeds an image that was not drawn: {unresolved.Message}";
            throw new ViewerException(
                ErrorCodes.UnsupportedFeature,
                $"'{holder}' {what}.",
                $"Without --strict the image is left out and reported as the warning {unresolved.Code}.");
        }

        /// <summary>
        ///     Maps the failure of a file that was found but could not be drawn to the library's errors.
        /// </summary>
        /// <param name="failure">The failure the resolver reported.</param>
        /// <returns>
        ///     The exception to throw: the code and hint the loader gives the underlying failure, with a message that
        ///     names both files. An underlying failure that is not an allow-listed one is a defect and is rethrown as itself.
        /// </returns>
        public ViewerException Map(ImageSourceException failure)
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
            catch (ViewerException ex)
            {
                var embedder = failure.Embedder ?? _svgName;
                return new ViewerException(ex.Code, $"'{embedder}' embeds '{failure.Href}': {ex.Message}", ex.Hint, ex);
            }

            // Read maps every exception it lets through to a ViewerException or rethrows it, so this is not reached
            throw failure;
        }

        /// <summary>
        ///     Gets the name of a severity as the tool reports it.
        /// </summary>
        /// <param name="severity">The severity.</param>
        /// <returns>The name: <c>info</c>, <c>warning</c>, or <c>unsupported</c>.</returns>
        internal static string SeverityName(ImageDiagnosticSeverity severity)
        {
            return severity switch
            {
                ImageDiagnosticSeverity.Info => "info",
                ImageDiagnosticSeverity.Unsupported => "unsupported",
                _ => "warning"
            };
        }

        /// <summary>
        ///     Gets the kind of file a link names, as the resolver reads the link.
        /// </summary>
        /// <param name="href">The link as written.</param>
        /// <returns>The kind, or <see langword="null"/> when the name is not one of a known kind.</returns>
        /// <remarks>
        ///     The name is the path the resolver chooses its source by (<see cref="FolderImageResolver.DecodeReference"/>:
        ///     the query and the fragment cut off, then percent-decoded), so <c>flow%2Emmd</c> is a Mermaid diagram.
        ///     <see cref="SvgFlattener"/> uses the same rule to tell the links only CanvasNet can draw from the plain
        ///     pictures, so both agree on what a link is.
        /// </remarks>
        internal static SourceKind? KindOfName(string href)
        {
            return KindOfFile(FolderImageResolver.DecodeReference(href));
        }

        /// <summary>
        ///     Gets the kind of a file by its name, which is not a link: nothing is cut off or decoded.
        /// </summary>
        /// <param name="name">The file name or path.</param>
        /// <returns>The kind, or <see langword="null"/> when the name is not one of a known kind.</returns>
        internal static SourceKind? KindOfFile(string name)
        {
            if (name.EndsWith(VegaLiteJsonSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return SourceKind.VegaLite;
            }

            return ExtensionKinds.TryGetValue(Path.GetExtension(name), out var kind) ? kind : null;
        }
    }
}

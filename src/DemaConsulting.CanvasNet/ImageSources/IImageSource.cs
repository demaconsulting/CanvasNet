// cspell:ignore href hrefs
namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     Draws one kind of linked picture: the contract by which a format (or a live source) plugs into
///     <see cref="FolderImageResolver"/>. Anyone can implement it, in or outside this repository.
/// </summary>
/// <remarks>
///     <para>
///         <b>What a source claims.</b> A source claims links in one or both of two ways. <see cref="Extensions"/>
///         makes it a <i>file-based</i> source: a link that names a file ending in one of the extensions is resolved
///         through the folder sandbox of the resolver (so the source never sees a path it could not safely read), and the
///         source receives the file's bytes. <see cref="Schemes"/> makes it a <i>scheme-claimed</i> source: a link such as
///         <c>camera:front</c> is handed to the source <b>as written</b>, with no sandbox and no file read, and the
///         source alone decides what the link means and what it may reach (and must therefore treat the text as
///         untrusted). When several sources claim the same extension or scheme the <b>later</b> one in the sequence given
///         to <see cref="ImageSourceSet"/> or <see cref="FolderImageResolver"/> wins, so a caller's own source placed
///         after the built-in ones overrides them.
///     </para>
///     <para>
///         <b>Rendering.</b> <see cref="Render(ImageSourceRequest)"/> receives an <see cref="ImageSourceRequest"/> and
///         returns an <see cref="ImageSourceResult"/>. The size to draw at, how to report notes, how to honor
///         cancellation and budgets, and how a source that draws a document which itself links to other pictures asks
///         the resolver to load them, are documented on <see cref="ImageSourceRequest"/>. The surface returned is owned by
///         the receiver, which disposes it after drawing; a source must not keep or reuse it, and must produce a new
///         picture on every call, because no layer caches rendered pictures.
///     </para>
///     <para>
///         <b>Thread safety.</b> The built-in sources are stateless and safe for concurrent use, and an instance can be
///         shared by many resolvers. A custom source should be too, unless its documentation says otherwise; a
///         <see cref="FolderImageResolver"/> itself calls its sources from one thread at a time.
///     </para>
/// </remarks>
public interface IImageSource
{
    /// <summary>
    ///     Gets the stable name of the format: lower case, for example <c>raster</c>, <c>svg</c>, <c>scad</c>,
    ///     <c>vega-lite</c>. It is the default <see cref="EmbeddedImage.Format"/>, the <see cref="ImageSourceDiagnostic.Source"/>
    ///     a source usually reports under, and appears in error messages. Must not be null or white space.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets the file name extensions this source claims: lower case, with the leading dot, for example
    ///     <c>.png</c>; compound extensions such as <c>.vl.json</c> are allowed. Matching is case-insensitive on the file
    ///     name, and when several claimed extensions match a name the longest one wins (so <c>.vl.json</c> beats a
    ///     <c>.json</c> claimed by another source). A source that claims no file returns an empty list. The list must not
    ///     be null and must not change over the life of the source.
    /// </summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>
    ///     Gets the URI schemes this source claims: lower case, without the colon, for example <c>camera</c>, <c>myapp</c>
    ///     or <c>test-scheme</c> (letters, digits, <c>+</c>, <c>-</c> and <c>.</c>, starting with a letter, at least two
    ///     characters: a single letter is a Windows drive). A link <c>scheme:rest</c> is handed to the source as written. The
    ///     names <c>file</c> (always a path inside the folder, handled by the resolver) and <c>data</c> (decoded by the SVG
    ///     codec) cannot be claimed. A source that claims no scheme returns an empty list. The list must not be null and
    ///     must not change over the life of the source.
    /// </summary>
    /// <remarks>
    ///     No built-in source claims <c>http</c> or <c>https</c>: such links stay unresolved (a warning, nothing drawn)
    ///     unless the caller supplies a source that claims them and takes responsibility for what it may fetch.
    /// </remarks>
    IReadOnlyList<string> Schemes { get; }

    /// <summary>
    ///     Draws the picture a request asks for.
    /// </summary>
    /// <param name="request">The request; see <see cref="ImageSourceRequest"/> for what each member means.</param>
    /// <returns>
    ///     The picture and its notes. The surface is owned by the receiver; it must be a new surface (never one the source
    ///     keeps) and its size is up to the source (see <see cref="ImageSourceResult.Surface"/>).
    /// </returns>
    /// <exception cref="Exception">
    ///     A source throws for a file that is found but cannot be drawn (corrupt, a syntax error, an unsupported feature
    ///     when <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/> is set). <see cref="FolderImageResolver"/>
    ///     wraps the exception in an <see cref="ImageSourceException"/> that names the link and the embedding file, except
    ///     for <see cref="OperationCanceledException"/>, which passes through. A link that merely cannot be found is not
    ///     the source's business: the resolver reports it before the source is called.
    /// </exception>
    ImageSourceResult Render(ImageSourceRequest request);
}

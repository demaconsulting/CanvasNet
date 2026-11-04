// cspell:ignore href embedder
namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The exception <see cref="FolderImageResolver"/> throws when a source fails to draw a file that was found: the
///     file is broken (a model with a syntax error, a corrupt picture) or the source cannot draw it.
/// </summary>
/// <remarks>
///     <para>
///         A link that cannot be resolved (missing, outside the folder, over a limit) is <b>not</b> an exception; it is
///         a warning and nothing is drawn. This exception is for a file that exists and is allowed but cannot be drawn.
///         The original failure is the <see cref="Exception.InnerException"/> with its type intact, so a caller can map
///         it (for example to an error code) and keep <see cref="Href"/> and <see cref="Embedder"/> to say which files
///         are involved.
///     </para>
///     <para>
///         The resolver wraps every exception a source throws except <see cref="OperationCanceledException"/> (so
///         cancellation stays cancellation) and an <see cref="ImageSourceException"/> that a deeper level already threw
///         (so the innermost embedder and link survive).
///     </para>
/// </remarks>
public sealed class ImageSourceException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageSourceException"/> class.
    /// </summary>
    /// <param name="message">The description of the failure.</param>
    /// <param name="href">The link that was being loaded, as written in the document.</param>
    /// <param name="embedder">
    ///     The file that contains the link (relative to the folder of the resolver), or <see langword="null"/> when the
    ///     top-level document that is being rendered contains it.
    /// </param>
    /// <param name="innerException">The failure of the source.</param>
    public ImageSourceException(string message, string href, string? embedder, Exception innerException)
        : base(message, innerException)
    {
        Href = href;
        Embedder = embedder;
    }

    /// <summary>
    ///     Gets the link that was being loaded, as written in the document.
    /// </summary>
    public string Href { get; }

    /// <summary>
    ///     Gets the file that contains the link (relative to the folder of the resolver), or <see langword="null"/>
    ///     when the top-level document being rendered contains it.
    /// </summary>
    public string? Embedder { get; }
}

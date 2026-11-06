namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The exception an <see cref="IImageSource"/> throws when it declines to draw the picture a link asks for because
///     of the link itself (a page that does not exist, a fragment value it cannot use), as opposed to a file that is
///     broken.
/// </summary>
/// <remarks>
///     <para>
///         A source must return a surface, so this exception is how it says "not this picture". For a link in a document
///         <see cref="FolderImageResolver"/> turns it into a note with the severity
///         <see cref="ImageDiagnosticSeverity.Unsupported"/> and the code <see cref="Code"/>, draws nothing, and the
///         render carries on (a strict caller can then fail on the note). The budget the request charged is given back.
///         The exception passes through the resolver unchanged, never wrapped in an <see cref="ImageSourceException"/>,
///         and a direct render does not swallow it.
///     </para>
///     <para>
///         The codes this repository uses are <see cref="FolderImageResolver.PageOutOfRangeCode"/> and
///         <see cref="FolderImageResolver.FragmentInvalidCode"/>.
///     </para>
/// </remarks>
public sealed class ImageSourceRefusedException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageSourceRefusedException"/> class.
    /// </summary>
    /// <param name="code">The stable, machine-readable code of the note that is reported; not null or empty.</param>
    /// <param name="message">A single-line description of why the picture is not drawn, without the link.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public ImageSourceRefusedException(string code, string message)
        : base(message ?? throw new ArgumentNullException(nameof(message)))
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        Code = code;
    }

    /// <summary>
    ///     Gets the code of the note that is reported.
    /// </summary>
    public string Code { get; }
}

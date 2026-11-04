namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The exception an <see cref="IImageSource"/> throws, before it allocates anything, when the picture it would
///     create does not fit in the pixel budget of the render (the pixels already charged for the request,
///     <see cref="ImageSourceRequest.ReservedPixels"/>, plus <see cref="ImageSourceRequest.RemainingPixels"/>).
/// </summary>
/// <remarks>
///     <para>
///         This is a refusal, not a failure of the file. When a source throws it for a link in a document,
///         <see cref="FolderImageResolver"/> turns it into a warning with the code
///         <see cref="FolderImageResolver.TooLargeCode"/> and draws nothing; the render carries on. A source normally
///         does not construct it by hand: <see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/> checks the size
///         and throws it. A source outside this repository can call that method, or throw this exception itself, so
///         nothing about the budget needs the source to know the resolver.
///     </para>
///     <para>
///         The exception passes through the resolver unchanged: it is never wrapped in an
///         <see cref="ImageSourceException"/>. A direct render (<see cref="FolderImageResolver.Render(string, int?, int?)"/>)
///         does not swallow it, because that call fails loudly.
///     </para>
/// </remarks>
public sealed class ImageSourceBudgetException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageSourceBudgetException"/> class.
    /// </summary>
    /// <param name="requestedPixels">The number of pixels the source would have had to allocate.</param>
    /// <param name="remainingPixels">
    ///     The number of pixels that were still allowed (for a source, the pixels reserved for the request plus those
    ///     still free).
    /// </param>
    public ImageSourceBudgetException(long requestedPixels, long remainingPixels)
        : base($"The picture needs {requestedPixels} pixels but only {remainingPixels} remain in the pixel budget.")
    {
        RequestedPixels = requestedPixels;
        RemainingPixels = remainingPixels;
    }

    /// <summary>
    ///     Gets the number of pixels the source would have had to allocate.
    /// </summary>
    public long RequestedPixels { get; }

    /// <summary>
    ///     Gets the number of pixels that were still allowed when the source refused.
    /// </summary>
    public long RemainingPixels { get; }
}

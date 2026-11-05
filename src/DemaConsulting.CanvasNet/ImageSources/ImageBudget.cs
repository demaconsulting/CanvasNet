namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The live pixel budget that one render shares between every picture it allocates: a counter of how many pixels
///     may still be allocated.
/// </summary>
/// <remarks>
///     <para>
///         The contract, and what a source has to do, is described once, in the remarks of
///         <see cref="ImageSourceRequest"/>. This type is only the shared counter: <see cref="FolderImageResolver"/>
///         creates one from <see cref="FolderImageResolverOptions.MaxDecodedPixels"/> and gives the <b>same</b> object
///         to every request it builds, nested requests included, so all levels of a render draw on one number that is
///         always current. A caller that builds a request by hand (a test of a custom source, for example) creates one
///         and sets <see cref="ImageSourceRequest.Budget"/> to give the request a limit.
///     </para>
///     <para>
///         Pixels can only be charged by <see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/> and by the
///         resolver itself, so the budget cannot be overdrawn by accident. The class is <b>not thread-safe</b>, like
///         the render it belongs to.
///     </para>
/// </remarks>
public sealed class ImageBudget
{
    /// <summary>
    ///     The pixels that may still be charged.
    /// </summary>
    private long _remaining;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageBudget"/> class.
    /// </summary>
    /// <param name="maxPixels">The total number of pixels (width times height, summed over every picture) that may be charged. Zero refuses every picture.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxPixels"/> is negative.</exception>
    public ImageBudget(long maxPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxPixels);
        _remaining = maxPixels;
    }

    /// <summary>
    ///     Gets the number of pixels that may still be charged: the maximum minus everything charged and not refunded
    ///     so far. It is live, so it already reflects the pictures that nested requests have charged.
    /// </summary>
    public long Remaining => _remaining;

    /// <summary>
    ///     Charges pixels to the budget, if they fit.
    /// </summary>
    /// <param name="pixels">The number of pixels to charge; not negative.</param>
    /// <exception cref="ImageSourceBudgetException">
    ///     Thrown when <paramref name="pixels"/> exceeds <see cref="Remaining"/>; nothing is charged then.
    /// </exception>
    internal void Charge(long pixels)
    {
        if (pixels > _remaining)
        {
            throw new ImageSourceBudgetException(pixels, _remaining);
        }

        _remaining -= pixels;
    }

    /// <summary>
    ///     Gives pixels back, for a picture whose allocation failed or was discarded.
    /// </summary>
    /// <param name="pixels">The number of pixels to give back; what was charged for the picture.</param>
    internal void Refund(long pixels)
    {
        _remaining += pixels;
    }
}

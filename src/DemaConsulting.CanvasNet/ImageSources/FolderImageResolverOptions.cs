using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     The limits and switches of a <see cref="FolderImageResolver"/>.
/// </summary>
/// <remarks>
///     The record is immutable once built and may be shared by any number of resolvers and threads. The limits are
///     per resolver, and a resolver is meant to serve one top-level render (see <see cref="FolderImageResolver"/>), so
///     they bound what one render can cause.
/// </remarks>
public sealed record FolderImageResolverOptions
{
    /// <summary>
    ///     Gets the deepest nesting of files embedding files that is followed: a file that is already this many levels
    ///     deep cannot embed another one (the link is reported as unresolved). The default is 4. Must not be negative.
    /// </summary>
    public int MaxDepth { get; init; } = 4;

    /// <summary>
    ///     Gets the most images that one resolver loads in total, over all levels of nesting (every call that reaches a
    ///     source counts, including a repeated link to the same file). The default is 64. Must not be negative.
    /// </summary>
    /// <remarks>
    ///     A nested document has budgets of its own in the library that draws it, so without this limit a tree of
    ///     documents could multiply the work. It is also the limit of distinct files the sandbox reads for the resolver.
    /// </remarks>
    public int MaxImages { get; init; } = 64;

    /// <summary>
    ///     Gets the most pixels (width times height, summed over every picture) that one resolver lets its sources
    ///     allocate in total. The default is 64,000,000 (64 megapixels, about 256 MB of 32-bit pixels in all), so a single
    ///     picture of the largest size, <see cref="Surface.MaxDimension"/> squared, is refused. Must
    ///     not be negative; zero refuses every picture.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The budget exists because a small placement can ask for a huge picture: a raster is decoded at its own
    ///         size, so an 8192 × 8192 PNG placed in a 1 × 1 rectangle would otherwise allocate about 256 MiB, again for
    ///         every link. It is one live counter shared by every picture of the render, nested ones included; the
    ///         contract (who charges what, when, and what happens on failure) is described once, in the remarks of
    ///         <see cref="ImageSourceRequest"/>. Pictures are disposed after use, so the budget is a cumulative cap on
    ///         work, not on memory held at one time.
    ///     </para>
    ///     <para>
    ///         A picture that does not fit is not drawn and is reported as a warning with the code
    ///         <see cref="FolderImageResolver.TooLargeCode"/> (the render carries on); a direct render fails with an
    ///         <see cref="ImageSourceBudgetException"/> instead.
    ///     </para>
    /// </remarks>
    public long MaxDecodedPixels { get; init; } = 64_000_000L;

    /// <summary>
    ///     Gets a value indicating whether a source must throw when a file uses a feature it does not draw, instead of
    ///     drawing without it and reporting a note. It is passed to every source as
    ///     <see cref="ImageSourceRequest.ThrowOnUnsupportedFeature"/>. The default is <see langword="false"/>.
    /// </summary>
    public bool ThrowOnUnsupportedFeature { get; init; }

    /// <summary>
    ///     Gets the token that cancels the work. The resolver checks it before every image it renders (and before
    ///     every file it reads) and passes it to every source as <see cref="ImageSourceRequest.CancellationToken"/>.
    ///     The default is <see cref="CancellationToken.None"/>.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

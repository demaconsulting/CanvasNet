// cspell:ignore href hrefs
using System.Runtime.InteropServices;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     Everything an <see cref="IImageSource"/> needs to draw one picture: what to draw, how big, and how to behave.
/// </summary>
/// <remarks>
///     <para>
///         <b>The two kinds of request.</b> A request for a <i>file-based</i> source has <see cref="HasContent"/> set:
///         <see cref="Content"/> holds the file's bytes (use <see cref="OpenContent"/> for a stream),
///         <see cref="FileName"/> its path relative to the folder of the resolver (for messages and source names) and
///         <see cref="Directory"/> the full path of the folder that holds it (the base for any <c>include</c> or data
///         file the format itself references, which a format library resolves and confines itself). A request for a
///         <i>scheme-claimed</i> source has <see cref="HasContent"/> unset, <see cref="Content"/> empty,
///         <see cref="FileName"/> and <see cref="Directory"/> <see langword="null"/>, and only <see cref="Href"/>, the
///         link exactly as written, says what is wanted. <see cref="Href"/> is always set.
///     </para>
///     <para>
///         <b>Size.</b> <see cref="PixelWidth"/> and <see cref="PixelHeight"/> are the size, in device pixels, of the
///         rectangle the picture is placed in (the renderer fits whatever comes back into it). They are
///         <see langword="null"/> when the caller wants the natural size of the picture. A vector source (a model, a chart,
///         a document page) should draw at the requested size so the result is sharp; a raster has no natural way to
///         honor it and simply returns its own size. <see cref="SizeFor(int, int, bool)"/> turns the natural size of the
///         picture and the request into the size to draw at: no request gives the natural size, one dimension keeps the
///         aspect ratio, both give that box (or, with <c>fitInside</c>, the largest size of the picture's aspect ratio
///         inside the box), always clamped to 1 to <see cref="Surface.MaxDimension"/>.
///     </para>
///     <para>
///         <b>Diagnostics.</b> A source reports notes in <see cref="ImageSourceResult.Diagnostics"/> (not by printing or
///         throwing). The resolver forwards them in <see cref="FolderImageResolver.Diagnostics"/> with the
///         embedding link in front of the message. When <see cref="ThrowOnUnsupportedFeature"/> is set the source throws
///         at the first feature it cannot draw instead of reporting it.
///     </para>
///     <para>
///         <b>Cancellation.</b> The resolver checks <see cref="CancellationToken"/> before every render and enforces
///         the depth and image-count limits (<see cref="FolderImageResolverOptions"/>) before it calls a source; a
///         source should still observe the token during long work and throw <see cref="OperationCanceledException"/>,
///         and a source that is expensive per call should keep the work proportional to the requested size. The returned
///         surface is owned by the caller and disposed after it is drawn: never return a surface the source keeps, and
///         produce a fresh picture on every call (nothing caches rendered pictures, so a live source such as a video
///         frame is asked again each time).
///     </para>
///     <para>
///         <b>The pixel budget.</b> A render may allocate only so many pixels in all
///         (<see cref="FolderImageResolverOptions.MaxDecodedPixels"/>), however many pictures it embeds, because a small
///         placement can ask for a huge picture (a raster is decoded at its own size, whatever size the request names).
///         The resolver has already charged the requested size (<see cref="ReservedPixels"/>, the width times the height
///         of <see cref="PixelWidth"/> and <see cref="PixelHeight"/>; zero for a natural-size render) before it calls the
///         source, and <see cref="RemainingPixels"/> is what is still free after that charge: the room for pictures the
///         source loads through <see cref="Resolver"/> (those are charged against the live budget, so the number is a
///         snapshot) and for any surface larger than the requested size. <b>A source must check before it
///         allocates</b>: a vector source checks the size it is about to draw at (which always fits when it is the
///         requested size), a raster source the size its header declares (before decoding anything), by calling
///         <see cref="EnsureWithinBudget(int, int)"/>, which throws an <see cref="ImageSourceBudgetException"/> that the
///         resolver turns into a warning (<see cref="FolderImageResolver.TooLargeCode"/>) and an undrawn picture. The
///         resolver also charges what it receives (the larger of the requested size and the returned size), so a source
///         that does not check cannot overspend: an oversized surface is discarded and the link is refused the same way,
///         but only after the source has already allocated, so checking first is what protects the memory.
///     </para>
///     <para>
///         <b>Nested links.</b> A source that draws a document which links to other pictures (the SVG source) loads them
///         through <see cref="Resolver"/>, an <see cref="IImageResolver"/> scoped to this request: it shares the budgets,
///         diagnostics and cycle chain of the whole render, so recursion is bounded without any state in the source. For a
///         file-based request a relative nested link resolves against the folder of the file itself
///         (<see cref="Directory"/>); for a scheme-claimed request there is no folder of its own, so a nested relative link
///         resolves against the root folder of the resolver. <see cref="Resolver"/> is <see langword="null"/> only when the
///         request was built by hand.
///     </para>
///     <para>
///         The class is immutable once built and anyone (a test of a custom source, for example) may construct one.
///     </para>
/// </remarks>
public sealed class ImageSourceRequest
{
    /// <summary>
    ///     Gets the link as written in the document (or the path or file name passed to a direct render). For a
    ///     scheme-claimed request it is the whole link, <c>scheme:rest</c>, in the case it was written. It is untrusted
    ///     text.
    /// </summary>
    public required string Href { get; init; }

    /// <summary>
    ///     Gets the bytes of the file. Empty when <see cref="HasContent"/> is <see langword="false"/>. The bytes are shared:
    ///     a source must not modify them.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>
    ///     Gets the path of the file relative to the folder of the resolver, with forward slashes, for messages and as
    ///     the source name a format library reports. <see langword="null"/> for a scheme-claimed request.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    ///     Gets the full path of the folder that holds the file, the base for the <c>include</c>, <c>use</c> or data
    ///     files the format references (the format library confines them to it), or <see langword="null"/> for a
    ///     scheme-claimed request and when the resolver has no folder.
    /// </summary>
    public string? Directory { get; init; }

    /// <summary>
    ///     Gets the requested width in device pixels, or <see langword="null"/> for the natural width. See
    ///     <see cref="SizeFor(int, int, bool)"/>.
    /// </summary>
    public int? PixelWidth { get; init; }

    /// <summary>
    ///     Gets the requested height in device pixels, or <see langword="null"/> for the natural height. See
    ///     <see cref="SizeFor(int, int, bool)"/>.
    /// </summary>
    public int? PixelHeight { get; init; }

    /// <summary>
    ///     Gets the number of pixels (width times height) of the render budget that are still free after the
    ///     <see cref="ReservedPixels"/> were charged: what is left for pictures loaded through <see cref="Resolver"/> and
    ///     for any allocation beyond the requested size; see the remarks of the class. It is a snapshot taken when the
    ///     request is built. The default is <see cref="long.MaxValue"/> (no limit), which is what a request built by hand
    ///     has.
    /// </summary>
    public long RemainingPixels { get; init; } = long.MaxValue;

    /// <summary>
    ///     Gets the number of pixels the resolver has already charged to the budget for this request: the requested size
    ///     (<see cref="PixelWidth"/> times <see cref="PixelHeight"/>), or zero for a natural-size direct render. A surface
    ///     of up to this many pixels needs nothing more from <see cref="RemainingPixels"/>. The default is zero.
    /// </summary>
    public long ReservedPixels { get; init; }

    /// <summary>
    ///     Gets the resolver a source uses to load the pictures that its own picture links to, scoped to this request
    ///     (see the remarks of the class), or <see langword="null"/> when the file's own links are not resolved.
    /// </summary>
    public IImageResolver? Resolver { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the source must throw, instead of reporting a note, when the file uses a
    ///     feature it cannot draw. The default is <see langword="false"/>.
    /// </summary>
    public bool ThrowOnUnsupportedFeature { get; init; }

    /// <summary>
    ///     Gets the token that cancels the render. A source observes it during long work and throws
    ///     <see cref="OperationCanceledException"/>. The default is <see cref="System.Threading.CancellationToken.None"/>.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the request carries the bytes of a file (<see langword="true"/>: a
    ///     file-based request, with <see cref="FileName"/>) or only a link (<see langword="false"/>: a scheme-claimed
    ///     request). It is true exactly when <see cref="FileName"/> is not null.
    /// </summary>
    public bool HasContent => FileName is not null;

    /// <summary>
    ///     Opens the bytes of the file as a read-only, seekable stream.
    /// </summary>
    /// <returns>A new stream over <see cref="Content"/> (not a copy when the bytes are array-backed); the caller disposes it.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <see cref="HasContent"/> is <see langword="false"/>: a scheme-claimed request has no file to read.
    /// </exception>
    public Stream OpenContent()
    {
        if (!HasContent)
        {
            throw new InvalidOperationException(
                $"The request for '{Href}' has no content: it was handed to the source by URI scheme, not read from a file.");
        }

        return MemoryMarshal.TryGetArray(Content, out var segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, false)
            : new MemoryStream(Content.ToArray(), false);
    }

    /// <summary>
    ///     Checks that a picture of the given size fits in the budget of this request; call it before allocating the
    ///     picture (or decoding it), with the size the picture will have. The picture fits when the pixels beyond
    ///     <see cref="ReservedPixels"/> (already charged) do not exceed <see cref="RemainingPixels"/>.
    /// </summary>
    /// <param name="width">The width in pixels the source is about to allocate.</param>
    /// <param name="height">The height in pixels the source is about to allocate.</param>
    /// <exception cref="ImageSourceBudgetException">
    ///     Thrown when width times height minus <see cref="ReservedPixels"/> exceeds <see cref="RemainingPixels"/>. The
    ///     exception reports width times height as the requested pixels and the sum of <see cref="ReservedPixels"/> and
    ///     <see cref="RemainingPixels"/> (the most the source could have used) as the remaining pixels.
    /// </exception>
    public void EnsureWithinBudget(int width, int height)
    {
        var pixels = (long)Math.Max(width, 0) * Math.Max(height, 0);
        var extra = Math.Max(0L, pixels - ReservedPixels);
        if (extra > RemainingPixels)
        {
            var usable = RemainingPixels > long.MaxValue - ReservedPixels ? long.MaxValue : RemainingPixels + ReservedPixels;
            throw new ImageSourceBudgetException(pixels, usable);
        }
    }

    /// <summary>
    ///     Works out the size to draw a picture at, from its natural size and the size the request asks for.
    /// </summary>
    /// <param name="naturalWidth">The natural width of the picture in pixels; at least 1.</param>
    /// <param name="naturalHeight">The natural height of the picture in pixels; at least 1.</param>
    /// <param name="fitInside">
    ///     <see langword="true"/> to shrink or enlarge the picture, keeping its aspect ratio, to the largest size that
    ///     fits inside the requested box when both dimensions are requested (the right choice for a page of a document);
    ///     <see langword="false"/> (the default) to draw at exactly the requested box (the right choice for a chart that
    ///     lays itself out to the size it is given).
    /// </param>
    /// <returns>
    ///     The width and height to draw at. With no requested size, the natural size. With one requested dimension, that
    ///     dimension and the other scaled to keep the aspect ratio (rounded to the nearest pixel, at least 1). With both,
    ///     the box, or the fitted size when <paramref name="fitInside"/> is set. Each value is clamped to 1 to
    ///     <see cref="Surface.MaxDimension"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a natural size is less than 1.</exception>
    public (int Width, int Height) SizeFor(int naturalWidth, int naturalHeight, bool fitInside = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(naturalWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(naturalHeight, 1);

        // A requested dimension below one pixel is meaningless, so it is treated as one pixel
        var requestedWidth = PixelWidth is { } w ? Math.Max(w, 1) : (int?)null;
        var requestedHeight = PixelHeight is { } h ? Math.Max(h, 1) : (int?)null;

        int width;
        int height;
        if (requestedWidth is { } boxWidth && requestedHeight is { } boxHeight)
        {
            // Both dimensions: the box itself, or the picture fitted inside it
            (width, height) = fitInside
                ? Surface.FitInside(
                    naturalWidth,
                    naturalHeight,
                    Math.Min(boxWidth, Surface.MaxDimension),
                    Math.Min(boxHeight, Surface.MaxDimension))
                : (boxWidth, boxHeight);
        }
        else if (requestedWidth is { } widthOnly)
        {
            // One dimension: the other follows the aspect ratio of the picture
            width = widthOnly;
            height = ScaleSide(naturalHeight, widthOnly, naturalWidth);
        }
        else if (requestedHeight is { } heightOnly)
        {
            height = heightOnly;
            width = ScaleSide(naturalWidth, heightOnly, naturalHeight);
        }
        else
        {
            (width, height) = (naturalWidth, naturalHeight);
        }

        return (Math.Clamp(width, 1, Surface.MaxDimension), Math.Clamp(height, 1, Surface.MaxDimension));
    }

    /// <summary>
    ///     Scales one side of a picture by the ratio of the requested to the natural length of the other side.
    /// </summary>
    /// <param name="side">The natural length of the side to scale.</param>
    /// <param name="requested">The requested length of the other side.</param>
    /// <param name="natural">The natural length of the other side.</param>
    /// <returns>The scaled length, rounded to the nearest pixel and at least 1 (not yet clamped above).</returns>
    private static int ScaleSide(int side, int requested, int natural)
    {
        var scaled = Math.Round((double)side * requested / natural, MidpointRounding.AwayFromZero);
        return (int)Math.Clamp(scaled, 1d, int.MaxValue);
    }
}

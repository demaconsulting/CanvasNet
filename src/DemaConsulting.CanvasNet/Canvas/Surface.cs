using System.Buffers;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;

namespace DemaConsulting.CanvasNet.Canvas;

/// <summary>
///     Represents a mutable, in-memory 32-bit RGBA pixel buffer.
/// </summary>
/// <remarks>
///     <c>Surface</c> is the second software unit in CanvasNet, providing the core pixel-storage
///     primitive that future drawing and codec functionality will build upon. It stores pixels in
///     a single contiguous <see cref="byte"/> array in row-major order, four bytes per pixel
///     (red, green, blue, alpha), so that entire rows can be exposed as <see cref="Span{T}"/>
///     without any copying, and so that <see cref="Crop"/> can copy whole rows at once rather than
///     iterating pixel by pixel.
///
///     Internally, each row is physically padded up to a multiple of 16 pixels (64 bytes) so that
///     whole-row vectorized bulk pixel operations never need scalar-remainder handling. This
///     padding is purely an internal storage-layout detail: <see cref="Width"/>/<see cref="Height"/>
///     and every public row accessor (<see cref="GetRowSpanBytes"/>, <see cref="GetRowSpan"/>)
///     always return exactly <c>Width</c>-length data - the padding bytes are never observable
///     through any public member.
///
///     <c>Surface</c> implements <see cref="IDisposable"/> so that its pixel-storage strategy can
///     be changed in a future release (for example, to an <see cref="ArrayPool{T}"/>-rented
///     array) without another breaking API change. In this release the internal buffer is a
///     plain managed <see cref="byte"/> array, not rented from a pool, so <see cref="Dispose"/>
///     has nothing to actually release yet; it exists purely to establish the disposal contract
///     early. Because the only backing storage is managed memory that the garbage collector
///     already reclaims safely on its own, <c>Surface</c> deliberately declares no finalizer -
///     forgetting to call <see cref="Dispose"/> only forgoes the (currently nonexistent) prompt
///     release, it can never leak an unmanaged or pooled resource. Calling <see cref="Dispose"/>
///     is idempotent (safe to call more than once), and every other public member that touches
///     the pixel buffer throws <see cref="ObjectDisposedException"/> once the instance has been
///     disposed.
/// </remarks>
public sealed class Surface : IDisposable
{
    /// <summary>
    ///     The number of bytes used to store a single pixel (one byte per RGBA channel).
    /// </summary>
    private const int BytesPerPixel = 4;

    /// <summary>
    ///     The row alignment, in pixels, that every physical row is padded up to. Chosen as the
    ///     smallest common multiple of the vector widths CanvasNet's target platforms are likely
    ///     to use for elementwise per-channel operations: SSE2 (4 px), AVX2 (8 px), and AVX-512
    ///     (16 px) all divide evenly into 16 px, so a full-row SIMD loop over the padded stride
    ///     processes only whole vector-width chunks, with zero scalar remainder handling required
    ///     regardless of which vector width the runtime picks at JIT time.
    /// </summary>
    private const int RowAlignmentPixels = 16;

    /// <summary>
    ///     The largest permitted value for either <see cref="Width"/> or <see cref="Height"/>.
    ///     Chosen so that the padded-stride and total-buffer-size arithmetic performed in the
    ///     constructor is provably safe using plain <see cref="int"/> arithmetic: with both
    ///     dimensions bounded by this value, the padded row width is at most 8192 pixels, the row
    ///     stride is at most <c>8192 * 4 = 32768</c> bytes, and the total buffer size is at most
    ///     <c>8192 * 32768 = 268,435,456</c> bytes - comfortably below <see cref="int.MaxValue"/>
    ///     (2,147,483,647), with no risk of overflow.
    /// </summary>
    /// <remarks>
    ///     Declared <see langword="public"/> (rather than <see langword="internal"/>) so that the
    ///     codecs in the same assembly can validate a decoded file's width/height against this same
    ///     bound before performing their own header-derived arithmetic (stride/buffer-size
    ///     calculations), and reject oversized images with a codec-appropriate
    ///     <see cref="System.IO.InvalidDataException"/> instead of letting the out-of-range value
    ///     reach this constructor and surface as an <see cref="ArgumentOutOfRangeException"/>; and
    ///     so that external callers can perform the same "bomb triage" comparison themselves. Each
    ///     codec's <c>GetInfo</c> method (for example
    ///     <see cref="Codecs.BmpCodec.GetInfo(Stream)"/>) reads only a file's header and reports its
    ///     raw declared dimensions without enforcing this bound, deliberately leaving the decision
    ///     of whether to reject an oversized image to the caller - comparing the returned
    ///     <see cref="Codecs.ImageInfo.Width"/>/<see cref="Codecs.ImageInfo.Height"/> against
    ///     <c>MaxDimension</c> before calling the corresponding <c>Load</c> method lets a caller
    ///     detect a maliciously or accidentally oversized image ("decompression bomb") without ever
    ///     allocating the pixel buffer that decoding it would require.
    /// </remarks>
    public const int MaxDimension = 8192;

    /// <summary>
    ///     The number of bytes physically occupied by a single row in <see cref="_buffer"/>,
    ///     including any trailing padding bytes beyond <c>Width * 4</c>. Always a multiple of
    ///     <c>RowAlignmentPixels * BytesPerPixel</c> (64 bytes).
    /// </summary>
    private readonly int _strideBytes;

    /// <summary>
    ///     The contiguous, row-major pixel buffer, sized <c>Height * _strideBytes</c> bytes. Each
    ///     row occupies <see cref="_strideBytes"/> bytes, which may be larger than
    ///     <c>Width * 4</c> to satisfy the row-alignment padding documented on
    ///     <see cref="RowAlignmentPixels"/>; the padding bytes are never exposed by any public
    ///     accessor.
    /// </summary>
    private readonly byte[] _buffer;

    /// <summary>
    ///     Set once <see cref="Dispose"/> has been called; every other public member that touches
    ///     <see cref="_buffer"/> checks this flag first via <see cref="ObjectDisposedException"/>'s
    ///     <c>ThrowIf</c> helper and rejects further use once it is <see langword="true"/>.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Surface"/> class with the specified
    ///     dimensions, fully transparent (all pixel bytes zero).
    /// </summary>
    /// <param name="width">
    ///     The width of the surface, in pixels. Must be greater than zero and no more than 8192.
    /// </param>
    /// <param name="height">
    ///     The height of the surface, in pixels. Must be greater than zero and no more than 8192.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="width"/> or <paramref name="height"/> is less than or
    ///     equal to zero, or when <paramref name="width"/> or <paramref name="height"/> exceeds
    ///     8192.
    /// </exception>
    /// <remarks>
    ///     Architectural decision: a freshly constructed surface is always fully transparent black
    ///     (every channel, including alpha, is zero) rather than opaque black or any other color.
    ///     This is a deliberate default because a zero-filled <see cref="byte"/> array is what the
    ///     runtime already provides at no extra cost, and "fully transparent" is the least
    ///     surprising default for a compositing/rendering surface: newly allocated regions
    ///     contribute nothing until something is explicitly drawn into them. Callers that require a
    ///     different initial fill (for example, opaque white) must set it explicitly after
    ///     construction.
    /// </remarks>
    public Surface(int width, int height)
    {
        // Reject non-positive dimensions immediately so that a Surface instance never exists
        // with a buffer that could not be correctly indexed
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        // Reject dimensions above MaxDimension so that the padded-stride/buffer-size arithmetic
        // below is guaranteed to stay within plain int range (see MaxDimension for the exact
        // bound analysis)
        if (width > MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must not exceed 8192.");
        }

        if (height > MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must not exceed 8192.");
        }

        Width = width;
        Height = height;

        // Width and height are now validated to be within (0, MaxDimension] above, so the
        // following plain int arithmetic is provably safe from overflow: the padded row width is
        // at most MaxDimension (8192) pixels, the row stride is at most 8192 * 4 = 32768 bytes,
        // and the total buffer size is at most 8192 * 32768 = 268,435,456 bytes - comfortably
        // under int.MaxValue (2,147,483,647). No long/checked arithmetic is required.
        // Round the row width up to the next multiple of RowAlignmentPixels, then convert to
        // bytes, so every physical row is a whole number of vector-width chunks (see
        // RowAlignmentPixels for the rationale)
        var paddedWidthPixels = ((width + RowAlignmentPixels - 1) / RowAlignmentPixels) * RowAlignmentPixels;
        _strideBytes = paddedWidthPixels * BytesPerPixel;

        // A newly allocated array is already zero-filled by the runtime, which is exactly the
        // "fully transparent" initial state documented above - no explicit clearing is needed
        _buffer = new byte[height * _strideBytes];
    }

    /// <summary>
    ///     Releases the resources held by this <see cref="Surface"/>.
    /// </summary>
    /// <remarks>
    ///     Idempotent: calling this method more than once has no additional effect. In this
    ///     release <see cref="_buffer"/> is a plain managed array, not rented from a pool, so
    ///     there is nothing to actually release yet - this method exists to establish the
    ///     disposal contract before a future release backs <see cref="_buffer"/> with an
    ///     <see cref="ArrayPool{T}"/>-rented array (at which point this method would return that
    ///     array to the pool here). After this method has been called, every other public member
    ///     that touches the pixel buffer throws <see cref="ObjectDisposedException"/>.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    /// <summary>
    ///     Gets the width of the surface, in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    ///     Gets the height of the surface, in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    ///     Gets or sets the pixel at the specified coordinates.
    /// </summary>
    /// <param name="x">The zero-based column of the pixel.</param>
    /// <param name="y">The zero-based row of the pixel.</param>
    /// <returns>The <see cref="Rgba32"/> value stored at <paramref name="x"/>, <paramref name="y"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="x"/> is outside <c>[0, Width)</c> or <paramref name="y"/> is
    ///     outside <c>[0, Height)</c>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     This indexer is a convenience for single-pixel access and is slower than working
    ///     directly with <see cref="GetRowSpan"/> or <see cref="GetRowSpanBytes"/>, because each
    ///     call re-validates <paramref name="y"/> and re-slices the row. Callers that need to
    ///     read or write many pixels in the same row should obtain the row span once and index
    ///     into it directly instead of repeatedly using this indexer.
    /// </remarks>
    public Rgba32 this[int x, int y]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var row = GetRowSpan(y);
            if (x < 0 || x >= Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X must be within the surface width.");
            }

            return row[x];
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var row = GetRowSpan(y);
            if (x < 0 || x >= Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X must be within the surface width.");
            }

            row[x] = value;
        }
    }

    /// <summary>
    ///     Returns the raw bytes of the specified row, as a mutable <see cref="Span{T}"/> over
    ///     the underlying buffer.
    /// </summary>
    /// <param name="y">The zero-based row to retrieve.</param>
    /// <returns>
    ///     A <see cref="Span{T}"/> of length <c>Width * 4</c> containing the row's raw RGBA
    ///     bytes, in-place over the surface's own storage.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="y"/> is outside <c>[0, Height)</c>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     The returned span aliases this surface's internal buffer directly - no data is copied,
    ///     so writes through the span are immediately visible through the indexer and vice versa.
    ///     The span's length is always exactly <c>Width * 4</c>, regardless of the surface's
    ///     internal row padding (see the class-level remarks): any padding bytes physically
    ///     stored beyond the row's <c>Width * 4</c> pixel bytes are never included in, or
    ///     observable through, the returned span.
    ///     <para>
    ///         <b>Span lifetime</b>: the returned span is only valid until this surface is
    ///         disposed. A caller that retains the span and continues to read or write through it
    ///         after calling <see cref="Dispose"/> gets undefined behavior - today that means
    ///         reading or writing the backing array after it has merely been marked unavailable
    ///         (harmless but unsupported); under a future pooled-array implementation, the same
    ///         array could already have been rented out to, and be actively used by, a different
    ///         <see cref="Surface"/>, so the same stale-span access would corrupt unrelated pixel
    ///         data. Callers must not retain a span returned by this method past a call to
    ///         <see cref="Dispose"/> on the same surface.
    ///     </para>
    /// </remarks>
    public Span<byte> GetRowSpanBytes(int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "Y must be within the surface height.");
        }

        return _buffer.AsSpan(y * _strideBytes, Width * BytesPerPixel);
    }

    /// <summary>
    ///     Returns the specified row reinterpreted as a mutable <see cref="Span{T}"/> of
    ///     <see cref="Rgba32"/> pixels.
    /// </summary>
    /// <param name="y">The zero-based row to retrieve.</param>
    /// <returns>
    ///     A <see cref="Span{T}"/> of length <c>Width</c> containing the row's pixels, in-place
    ///     over the surface's own storage.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="y"/> is outside <c>[0, Height)</c>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     Uses <see cref="MemoryMarshal"/>'s span-reinterpretation cast to reinterpret the raw
    ///     byte row returned by <see cref="GetRowSpanBytes"/> as <see cref="Rgba32"/> values
    ///     without copying any data, so writes through the returned span are immediately visible
    ///     through the indexer and <see cref="GetRowSpanBytes"/>, and vice versa.
    ///     <para>
    ///         <b>Span lifetime</b>: see <see cref="GetRowSpanBytes"/> - the same rule applies
    ///         here, since the returned span aliases the same underlying storage. Callers must
    ///         not retain a span returned by this method past a call to <see cref="Dispose"/> on
    ///         the same surface.
    ///     </para>
    /// </remarks>
    public Span<Rgba32> GetRowSpan(int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return MemoryMarshal.Cast<byte, Rgba32>(GetRowSpanBytes(y));
    }

    /// <summary>
    ///     Returns the full physical (padded) bytes of the specified row, including any trailing
    ///     padding bytes beyond <c>Width * 4</c>.
    /// </summary>
    /// <param name="y">The zero-based row to retrieve.</param>
    /// <returns>
    ///     A <see cref="Span{T}"/> of length <c>_strideBytes</c> covering the row's raw bytes
    ///     plus any trailing alignment padding, in-place over the surface's own storage.
    /// </returns>
    /// <remarks>
    ///     This is a private helper used only by bulk, whole-row vectorized pixel operations
    ///     (<see cref="PremultiplyAlpha"/>, <see cref="UnpremultiplyAlpha"/>,
    ///     <see cref="CompositeOver(Surface)"/>, <see cref="CompositeOver(Rgba32)"/>,
    ///     <see cref="Clear"/>) so that they can process the full physical row in one vectorized
    ///     pass with zero scalar remainder. The padding bytes it exposes are never read back
    ///     through any public accessor, and callers of this helper must never persist or observe
    ///     them as meaningful pixel data.
    /// </remarks>
    private Span<byte> GetPaddedRowSpanBytes(int y) => _buffer.AsSpan(y * _strideBytes, _strideBytes);

    /// <summary>
    ///     Creates a new, independent <see cref="Surface"/> containing a copy of the specified
    ///     rectangular sub-region of this surface.
    /// </summary>
    /// <param name="x">The zero-based column of the top-left corner of the region to copy.</param>
    /// <param name="y">The zero-based row of the top-left corner of the region to copy.</param>
    /// <param name="width">The width, in pixels, of the region to copy. Must be greater than zero.</param>
    /// <param name="height">The height, in pixels, of the region to copy. Must be greater than zero.</param>
    /// <returns>
    ///     A new <see cref="Surface"/> of size <paramref name="width"/> by <paramref name="height"/>
    ///     containing an independent copy of the requested pixels.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="x"/> is negative, <paramref name="y"/> is negative,
    ///     <paramref name="width"/> is less than or equal to zero, <paramref name="height"/> is
    ///     less than or equal to zero, <c>x + width</c> exceeds <see cref="Width"/>, or
    ///     <c>y + height</c> exceeds <see cref="Height"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     The returned surface owns an entirely separate buffer: subsequent writes to either
    ///     surface never affect the other. Each row of the sub-region is copied in a single
    ///     <see cref="Span{T}.CopyTo"/> call rather than pixel by pixel, which is both simpler
    ///     and faster than a per-pixel loop for contiguous row data.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     var surface = new Surface(4, 4);
    ///     surface[1, 1] = new Rgba32(0, 255, 0, 255); // opaque green pixel
    ///
    ///     var cropped = surface.Crop(1, 1, 2, 2);
    ///     Console.WriteLine(cropped.Width);   // Output: 2
    ///     Console.WriteLine(cropped.Height);  // Output: 2
    ///     Console.WriteLine(cropped[0, 0].G); // Output: 255
    ///     </code>
    /// </example>
    public Surface Crop(int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Validate each argument individually so that callers get a precise parameter name
        // identifying exactly which value was invalid
        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "X must not be negative.");
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "Y must not be negative.");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");
        }

        if (x + width > Width)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The region exceeds the surface width.");
        }

        if (y + height > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "The region exceeds the surface height.");
        }

        // Allocate the destination surface up front; its constructor already zero-fills the
        // buffer, which every copied row will fully overwrite below
        var result = new Surface(width, height);

        // Copy one row at a time: each source row is a contiguous run of "width" pixels
        // starting at column x, which Span<T>.CopyTo transfers in a single bulk operation
        for (var row = 0; row < height; row++)
        {
            var sourceRow = GetRowSpanBytes(y + row).Slice(x * BytesPerPixel, width * BytesPerPixel);
            var destinationRow = result.GetRowSpanBytes(row);
            sourceRow.CopyTo(destinationRow);
        }

        return result;
    }

    /// <summary>
    ///     The value <c>255</c> as a <see cref="float"/>, used throughout the vectorized bulk
    ///     pixel operations to convert between byte-scale (<c>[0, 255]</c>) and normalized
    ///     (<c>[0, 1]</c>) channel representations.
    /// </summary>
    private const float ByteMax = 255f;

    /// <summary>
    ///     Converts this surface's pixel buffer, in place, from straight (unassociated) alpha -
    ///     the representation every codec reads and writes - to premultiplied alpha.
    /// </summary>
    /// <remarks>
    ///     For each pixel, every color channel is replaced by
    ///     <c>round(channel * alpha / 255)</c>, using round-half-away-from-zero and clamping to
    ///     <c>[0, 255]</c>; the alpha channel itself is unchanged. Every possible input byte
    ///     pattern is valid, so this method never throws.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <example>
    ///     <code>
    ///     var surface = new Surface(1, 1);
    ///     surface[0, 0] = new Rgba32(200, 100, 50, 128);
    ///     surface.PremultiplyAlpha();
    ///     // surface[0, 0] is now Rgba32(100, 50, 25, 128) - round(200 * 128 / 255) == 100, etc.
    ///     </code>
    /// </example>
    public void PremultiplyAlpha()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var pixelsPerRow = _strideBytes / BytesPerPixel;
        using var row = new RowChannelBuffers(pixelsPerRow);

        for (var y = 0; y < Height; y++)
        {
            var padded = GetPaddedRowSpanBytes(y);
            DeinterleaveRow(padded, row, pixelsPerRow);
            WidenAllToFloat(row, pixelsPerRow);

            PremultiplyChannel(row.RFloat, row.AFloat, row.RBytes, pixelsPerRow);
            PremultiplyChannel(row.GFloat, row.AFloat, row.GBytes, pixelsPerRow);
            PremultiplyChannel(row.BFloat, row.AFloat, row.BBytes, pixelsPerRow);
            // The alpha channel bytes (row.ABytes) are left unchanged by premultiplication.

            ReinterleaveRow(padded, row, pixelsPerRow);
        }
    }

    /// <summary>
    ///     Converts this surface's pixel buffer, in place, from premultiplied alpha back to
    ///     straight (unassociated) alpha - the inverse of <see cref="PremultiplyAlpha"/>.
    /// </summary>
    /// <remarks>
    ///     For each pixel with a non-zero alpha, every color channel is replaced by
    ///     <c>round(channel * 255 / alpha)</c>, using round-half-away-from-zero and clamping to
    ///     <c>[0, 255]</c> (the clamp is load-bearing here, since premultiplication is lossy and
    ///     this division can genuinely overshoot 255). For a fully transparent pixel
    ///     (<c>alpha == 0</c>), no color information is recoverable, so the defined result is
    ///     <c>R = G = B = 0</c>, matching common raster-graphics convention. The alpha channel
    ///     itself is unchanged. Every possible input byte pattern is valid, so this method never
    ///     throws.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <example>
    ///     <code>
    ///     var surface = new Surface(1, 1);
    ///     surface[0, 0] = new Rgba32(0, 0, 0, 0); // fully transparent
    ///     surface.UnpremultiplyAlpha();
    ///     // surface[0, 0] is still Rgba32(0, 0, 0, 0) - the defined degenerate-case result.
    ///     </code>
    /// </example>
    public void UnpremultiplyAlpha()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var pixelsPerRow = _strideBytes / BytesPerPixel;
        using var row = new RowChannelBuffers(pixelsPerRow);

        for (var y = 0; y < Height; y++)
        {
            var padded = GetPaddedRowSpanBytes(y);
            DeinterleaveRow(padded, row, pixelsPerRow);
            WidenAllToFloat(row, pixelsPerRow);

            UnpremultiplyChannel(row.RFloat, row.AFloat, row.RBytes, pixelsPerRow);
            UnpremultiplyChannel(row.GFloat, row.AFloat, row.GBytes, pixelsPerRow);
            UnpremultiplyChannel(row.BFloat, row.AFloat, row.BBytes, pixelsPerRow);

            // Degenerate case: for pixels with alpha == 0, no color information is recoverable -
            // the defined result is R = G = B = 0, overriding whatever the (division-by-zero)
            // arithmetic above produced for those lanes.
            ZeroColorWhereAlphaByteIsZero(row.ABytes, row.RBytes, row.GBytes, row.BBytes, pixelsPerRow);

            ReinterleaveRow(padded, row, pixelsPerRow);
        }
    }

    /// <summary>
    ///     Composites <paramref name="foreground"/> "over" this surface using standard
    ///     Porter-Duff "over" alpha compositing, writing the result back into this surface in
    ///     place.
    /// </summary>
    /// <param name="foreground">
    ///     The surface to composite over this one. Must be the same size as this surface.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="foreground"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="foreground"/>'s <see cref="Width"/> or <see cref="Height"/>
    ///     does not match this surface's.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///     Thrown when this surface, or <paramref name="foreground"/>, has been disposed.
    /// </exception>
    /// <remarks>
    ///     Both this surface and <paramref name="foreground"/> are assumed to hold straight
    ///     (unassociated) alpha on input, and the result is also straight alpha - callers do not
    ///     need to call <see cref="PremultiplyAlpha"/> first. Internally, per channel normalized
    ///     to <c>[0, 1]</c> (dividing by 255): <c>outA = fgA + bgA * (1 - fgA)</c>; if
    ///     <c>outA == 0</c>, every output channel is <c>0</c>; otherwise
    ///     <c>outC = (fgC * fgA + bgC * bgA * (1 - fgA)) / outA</c>. Results are converted back
    ///     to bytes with round-half-away-from-zero, clamped to <c>[0, 255]</c>. Only equal-size,
    ///     axis-aligned compositing is supported; offset or differently sized compositing is not.
    /// </remarks>
    public void CompositeOver(Surface foreground)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(foreground);

        ObjectDisposedException.ThrowIf(foreground._disposed, foreground);

        if (foreground.Width != Width || foreground.Height != Height)
        {
            throw new ArgumentException(
                "The foreground surface must have the same dimensions as this surface.",
                nameof(foreground));
        }

        var pixelsPerRow = _strideBytes / BytesPerPixel;
        using var bg = new RowChannelBuffers(pixelsPerRow);
        using var fg = new RowChannelBuffers(pixelsPerRow);
        using var work = new CompositeWorkBuffers(pixelsPerRow);

        for (var y = 0; y < Height; y++)
        {
            var bgRow = GetPaddedRowSpanBytes(y);
            var fgRow = foreground.GetPaddedRowSpanBytes(y);

            DeinterleaveRow(bgRow, bg, pixelsPerRow);
            DeinterleaveRow(fgRow, fg, pixelsPerRow);
            WidenAllToFloat(bg, pixelsPerRow);
            WidenAllToFloat(fg, pixelsPerRow);

            CompositeOverRow(bg, fg, work, pixelsPerRow);

            NarrowRoundedClamp(work.OutR, bg.RBytes, pixelsPerRow);
            NarrowRoundedClamp(work.OutG, bg.GBytes, pixelsPerRow);
            NarrowRoundedClamp(work.OutB, bg.BBytes, pixelsPerRow);
            NarrowRoundedClamp(work.OutA, bg.ABytes, pixelsPerRow);
            ZeroColorWhereAlphaByteIsZero(bg.ABytes, bg.RBytes, bg.GBytes, bg.BBytes, pixelsPerRow);

            ReinterleaveRow(bgRow, bg, pixelsPerRow);
        }
    }

    /// <summary>
    ///     Composites a single constant <paramref name="color"/> "over" every pixel of this
    ///     surface using standard Porter-Duff "over" alpha compositing, writing the result back
    ///     into this surface in place.
    /// </summary>
    /// <param name="color">The constant foreground color to composite over every pixel.</param>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     Uses the same formula and rounding rule as <see cref="CompositeOver(Surface)"/>, with
    ///     <paramref name="color"/> acting as the foreground at every pixel. Every possible
    ///     <see cref="Rgba32"/> value is valid, so this method never throws.
    /// </remarks>
    /// <example>
    ///     <code>
    ///     var surface = new Surface(1, 1);
    ///     surface[0, 0] = new Rgba32(0, 255, 0, 255); // opaque green background
    ///     surface.CompositeOver(new Rgba32(255, 0, 0, 128)); // semi-transparent red overlay
    ///     // surface[0, 0] is now Rgba32(128, 127, 0, 255) (round-half-away-from-zero applied).
    ///     </code>
    /// </example>
    public void CompositeOver(Rgba32 color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Build a single-pixel-wide "foreground surface" whose one row is broadcast as the
        // constant foreground for every row of this surface - this reuses exactly the same
        // per-row compositing formula as CompositeOver(Surface) without duplicating it,
        // at the cost of one extra small buffer of constant per-pixel bytes.
        var pixelsPerRow = _strideBytes / BytesPerPixel;
        using var bg = new RowChannelBuffers(pixelsPerRow);
        using var fg = new RowChannelBuffers(pixelsPerRow);
        using var work = new CompositeWorkBuffers(pixelsPerRow);

        Array.Fill(fg.RBytes, color.R, 0, pixelsPerRow);
        Array.Fill(fg.GBytes, color.G, 0, pixelsPerRow);
        Array.Fill(fg.BBytes, color.B, 0, pixelsPerRow);
        Array.Fill(fg.ABytes, color.A, 0, pixelsPerRow);
        WidenAllToFloat(fg, pixelsPerRow);

        for (var y = 0; y < Height; y++)
        {
            var bgRow = GetPaddedRowSpanBytes(y);

            DeinterleaveRow(bgRow, bg, pixelsPerRow);
            WidenToFloat(bg.RBytes, bg.RFloat, pixelsPerRow);
            WidenToFloat(bg.GBytes, bg.GFloat, pixelsPerRow);
            WidenToFloat(bg.BBytes, bg.BFloat, pixelsPerRow);
            WidenToFloat(bg.ABytes, bg.AFloat, pixelsPerRow);

            CompositeOverRow(bg, fg, work, pixelsPerRow);

            NarrowRoundedClamp(work.OutR, bg.RBytes, pixelsPerRow);
            NarrowRoundedClamp(work.OutG, bg.GBytes, pixelsPerRow);
            NarrowRoundedClamp(work.OutB, bg.BBytes, pixelsPerRow);
            NarrowRoundedClamp(work.OutA, bg.ABytes, pixelsPerRow);
            ZeroColorWhereAlphaByteIsZero(bg.ABytes, bg.RBytes, bg.GBytes, bg.BBytes, pixelsPerRow);

            ReinterleaveRow(bgRow, bg, pixelsPerRow);
        }
    }

    /// <summary>
    ///     Fills every pixel of this surface with the constant <paramref name="color"/>,
    ///     overwriting any existing pixel data in place.
    /// </summary>
    /// <param name="color">The color to fill the entire surface with.</param>
    /// <remarks>
    ///     Unlike <see cref="CompositeOver(Rgba32)"/>, this is a pure overwrite - no alpha
    ///     blending against existing pixel data is performed, so <paramref name="color"/> becomes
    ///     the exact value of every pixel regardless of its alpha component. It builds a single
    ///     padded interleaved row containing <paramref name="color"/> repeated across every pixel
    ///     (reusing the same <see cref="RowChannelBuffers"/>/<see cref="ReinterleaveRow"/>
    ///     broadcast-and-reuse pattern <see cref="CompositeOver(Rgba32)"/> uses to build its
    ///     constant foreground row) and then bulk-copies that one pattern row into every row of
    ///     the surface via <see cref="Span{T}.CopyTo"/>, so the fill cost is one vectorized memory
    ///     copy per row rather than a per-pixel store. Every possible <see cref="Rgba32"/> value
    ///     is valid, so this method never throws for that reason.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <example>
    ///     <code>
    ///     var surface = new Surface(4, 4);
    ///     surface.Clear(new Rgba32(0, 0, 0, 255)); // fill the whole surface with opaque black
    ///     </code>
    /// </example>
    public void Clear(Rgba32 color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var pixelsPerRow = _strideBytes / BytesPerPixel;
        using var pattern = new RowChannelBuffers(pixelsPerRow);

        Array.Fill(pattern.RBytes, color.R, 0, pixelsPerRow);
        Array.Fill(pattern.GBytes, color.G, 0, pixelsPerRow);
        Array.Fill(pattern.BBytes, color.B, 0, pixelsPerRow);
        Array.Fill(pattern.ABytes, color.A, 0, pixelsPerRow);

        // Build one padded interleaved pattern row and reuse it for every row of the surface -
        // a bulk memory copy per row rather than a per-pixel loop.
        var patternRow = new byte[_strideBytes];
        ReinterleaveRow(patternRow, pattern, pixelsPerRow);

        for (var y = 0; y < Height; y++)
        {
            patternRow.AsSpan().CopyTo(GetPaddedRowSpanBytes(y));
        }
    }

    /// <summary>
    ///     Composites a single constant <paramref name="color"/> "over" a horizontal run of
    ///     pixels in row <paramref name="y"/> starting at column <paramref name="x"/>, scaling
    ///     <paramref name="color"/>'s effective alpha at each pixel by the corresponding entry of
    ///     <paramref name="coverage"/>, using standard Porter-Duff "over" alpha compositing.
    /// </summary>
    /// <param name="y">The zero-based row to composite into. Must be within <c>[0, Height)</c>.</param>
    /// <param name="x">
    ///     The zero-based column at which the run starts. Must be within <c>[0, Width]</c> (equal
    ///     to <see cref="Width"/> is permitted only when <paramref name="coverage"/> is empty).
    /// </param>
    /// <param name="coverage">
    ///     One coverage value per pixel of the run, in left-to-right order. Each value is
    ///     implicitly clamped to <c>[0, 1]</c>: a value less than or equal to zero leaves that
    ///     pixel unchanged, a value greater than or equal to one is equivalent to calling
    ///     <see cref="CompositeOver(Rgba32)"/> at that one pixel, and values in between linearly
    ///     scale <paramref name="color"/>'s alpha before compositing.
    /// </param>
    /// <param name="color">The constant foreground color to composite over the run.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="y"/> is outside <c>[0, Height)</c>, or when
    ///     <paramref name="x"/> or <c>x + coverage.Length</c> is outside <c>[0, Width]</c>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     This is the first partial-row compositing primitive in <see cref="Surface"/>: every
    ///     other <c>CompositeOver</c> overload always processes a full row (or the whole
    ///     surface). It exists so per-pixel-coverage callers - such as
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.ScanlineRasterizer"/>'s antialiased fill
    ///     rasterizer - can composite an arbitrary sub-range of a row directly, without allocating
    ///     a full-row or full-surface buffer merely to hold mostly-untouched pixels. It reuses the
    ///     exact same private per-channel <see cref="CompositeOverRow"/> pipeline as every other
    ///     <c>CompositeOver</c> overload - only the foreground buffer's population differs (here,
    ///     a constant color whose per-pixel alpha is scaled by <paramref name="coverage"/>, rather
    ///     than a constant alpha or another surface's pixels) - so the blend math itself is never
    ///     duplicated.
    /// </remarks>
    public void CompositeOverSpan(int y, int x, ReadOnlySpan<float> coverage, Rgba32 color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The public entry point always rents its own scratch buffers for the single call - see
        // CompositeOverSpan(int, int, ReadOnlySpan<float>, Rgba32, CompositeSpanWorkspace) for the
        // amortized-workspace overload used by hot loops such as
        // DemaConsulting.CanvasNet.Drawing.ScanlineRasterizer.Fill.
        ValidateCompositeOverSpanArgs(y, x, coverage.Length);

        var count = coverage.Length;
        if (count == 0)
        {
            return;
        }

        using var bg = new RowChannelBuffers(count);
        using var fg = new RowChannelBuffers(count);
        using var work = new CompositeWorkBuffers(count);

        CompositeOverSpanCore(y, x, coverage, color, bg, fg, work, count);
    }

    /// <summary>
    ///     Amortized-workspace counterpart of <see cref="CompositeOverSpan(int, int, ReadOnlySpan{float}, Rgba32)"/>
    ///     for callers - such as <see cref="DemaConsulting.CanvasNet.Drawing.ScanlineRasterizer.Fill(Surface, System.Collections.Generic.IReadOnlyList{System.Collections.Generic.List{System.Numerics.Vector2}}, Rgba32, DemaConsulting.CanvasNet.Drawing.FillRule, DemaConsulting.CanvasNet.Geometry.Rect)"/> -
    ///     that composite many equal-or-smaller-width spans in a tight per-row loop.
    /// </summary>
    /// <param name="y">The zero-based row to composite into. Must be within <c>[0, Height)</c>.</param>
    /// <param name="x">
    ///     The zero-based column at which the run starts. Must be within <c>[0, Width]</c> (equal
    ///     to <see cref="Width"/> is permitted only when <paramref name="coverage"/> is empty).
    /// </param>
    /// <param name="coverage">
    ///     One coverage value per pixel of the run, in left-to-right order. Same semantics as
    ///     <see cref="CompositeOverSpan(int, int, ReadOnlySpan{float}, Rgba32)"/>.
    /// </param>
    /// <param name="color">The constant foreground color to composite over the run.</param>
    /// <param name="workspace">
    ///     A previously constructed <see cref="CompositeSpanWorkspace"/> whose
    ///     <see cref="CompositeSpanWorkspace.Capacity"/> is at least <c>coverage.Length</c>. The
    ///     caller owns this workspace's lifetime - typically allocating it once before, and
    ///     disposing it once after, a whole row loop - so its rented scratch buffers are reused
    ///     across every call instead of being rented and returned per call.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown for the same reasons as
    ///     <see cref="CompositeOverSpan(int, int, ReadOnlySpan{float}, Rgba32)"/>, or when
    ///     <c>coverage.Length</c> exceeds <paramref name="workspace"/>'s capacity.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     This overload is internal because it exposes an implementation-detail allocation
    ///     strategy (buffer reuse), not new externally observable behavior: for a given
    ///     <paramref name="y"/>/<paramref name="x"/>/<paramref name="coverage"/>/<paramref name="color"/>,
    ///     it produces byte-for-byte identical surface output to the public overload - see
    ///     <c>ScanlineRasterizerFillWorkspaceTests</c> for the equivalence proof.
    /// </remarks>
    internal void CompositeOverSpan(int y, int x, ReadOnlySpan<float> coverage, Rgba32 color, CompositeSpanWorkspace workspace)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ValidateCompositeOverSpanArgs(y, x, coverage.Length);
        ArgumentNullException.ThrowIfNull(workspace);

        var count = coverage.Length;
        if (count == 0)
        {
            return;
        }

        if (count > workspace.Capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coverage), coverage.Length, "Coverage length must not exceed the workspace's capacity.");
        }

        CompositeOverSpanCore(y, x, coverage, color, workspace.Bg, workspace.Fg, workspace.Work, count);
    }

    /// <summary>
    ///     Composites one <paramref name="colors"/> value per pixel over a horizontal run of this
    ///     surface, each scaled by that pixel's own <paramref name="coverage"/> value.
    /// </summary>
    /// <param name="y">The zero-based row to composite into. Must be within <c>[0, Height)</c>.</param>
    /// <param name="x">
    ///     The zero-based column at which the run starts. Must be within <c>[0, Width]</c> (equal
    ///     to <see cref="Width"/> is permitted only when <paramref name="coverage"/> is empty).
    /// </param>
    /// <param name="coverage">
    ///     One coverage value per pixel of the run, in left-to-right order. Same semantics as
    ///     <see cref="CompositeOverSpan(int, int, ReadOnlySpan{float}, Rgba32)"/>.
    /// </param>
    /// <param name="colors">
    ///     One foreground color per pixel of the run, in left-to-right order. Must have the same
    ///     length as <paramref name="coverage"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="y"/> is outside <c>[0, Height)</c>, or when
    ///     <paramref name="x"/> or <c>x + coverage.Length</c> is outside <c>[0, Width]</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="colors"/>'s length does not equal <paramref name="coverage"/>'s
    ///     length.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     This overload exists for per-pixel-color callers - such as
    ///     <see cref="DemaConsulting.CanvasNet.Drawing.ScanlineRasterizer"/>'s gradient fill
    ///     overload - which need a different color at every pixel of a run rather than one
    ///     constant color. It shares the exact same <see cref="CompositeOverRow"/> blend pipeline
    ///     as the constant-color <see cref="CompositeOverSpan(int, int, ReadOnlySpan{float}, Rgba32)"/>
    ///     overload - only the foreground buffer's population differs (here, one <see cref="Rgba32"/>
    ///     read per pixel from <paramref name="colors"/>, rather than a single constant color
    ///     broadcast to every pixel).
    /// </remarks>
    public void CompositeOverSpan(int y, int x, ReadOnlySpan<float> coverage, ReadOnlySpan<Rgba32> colors)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ValidateCompositeOverSpanArgs(y, x, coverage.Length);

        if (colors.Length != coverage.Length)
        {
            throw new ArgumentException(
                "Colors must have the same length as coverage.", nameof(colors));
        }

        var count = coverage.Length;
        if (count == 0)
        {
            return;
        }

        using var bg = new RowChannelBuffers(count);
        using var fg = new RowChannelBuffers(count);
        using var work = new CompositeWorkBuffers(count);

        CompositeOverSpanCore(y, x, coverage, colors, bg, fg, work, count);
    }

    /// <summary>
    ///     Amortized-workspace counterpart of <see cref="CompositeOverSpan(int, int, ReadOnlySpan{float}, ReadOnlySpan{Rgba32})"/>,
    ///     for callers - such as <see cref="DemaConsulting.CanvasNet.Drawing.ScanlineRasterizer.Fill(Surface, IReadOnlyList{List{System.Numerics.Vector2}}, DemaConsulting.CanvasNet.Drawing.Gradient, DemaConsulting.CanvasNet.Drawing.FillRule, Geometry.Rect)"/> -
    ///     that composite many equal-or-smaller-width spans in a tight per-row loop.
    /// </summary>
    /// <param name="y">The zero-based row to composite into. Must be within <c>[0, Height)</c>.</param>
    /// <param name="x">
    ///     The zero-based column at which the run starts. Must be within <c>[0, Width]</c> (equal
    ///     to <see cref="Width"/> is permitted only when <paramref name="coverage"/> is empty).
    /// </param>
    /// <param name="coverage">One coverage value per pixel of the run. Same semantics as the public overload.</param>
    /// <param name="colors">One foreground color per pixel of the run. Must have the same length as <paramref name="coverage"/>.</param>
    /// <param name="workspace">
    ///     A previously constructed <see cref="CompositeSpanWorkspace"/> whose <see cref="CompositeSpanWorkspace.Capacity"/>
    ///     is at least <c>coverage.Length</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown for the same reasons as the public overload, or when <c>coverage.Length</c>
    ///     exceeds <paramref name="workspace"/>'s capacity.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="colors"/>'s length does not equal <paramref name="coverage"/>'s
    ///     length.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when this surface has been disposed.</exception>
    /// <remarks>
    ///     Internal for the same reason as <see cref="CompositeOverSpan(int, int, ReadOnlySpan{float}, Rgba32, CompositeSpanWorkspace)"/>:
    ///     it exposes an implementation-detail allocation strategy, not new externally observable
    ///     behavior - for a given set of arguments it produces byte-for-byte identical surface
    ///     output to the public overload.
    /// </remarks>
    internal void CompositeOverSpan(int y, int x, ReadOnlySpan<float> coverage, ReadOnlySpan<Rgba32> colors, CompositeSpanWorkspace workspace)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ValidateCompositeOverSpanArgs(y, x, coverage.Length);
        ArgumentNullException.ThrowIfNull(workspace);

        if (colors.Length != coverage.Length)
        {
            throw new ArgumentException(
                "Colors must have the same length as coverage.", nameof(colors));
        }

        var count = coverage.Length;
        if (count == 0)
        {
            return;
        }

        if (count > workspace.Capacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coverage), coverage.Length, "Coverage length must not exceed the workspace's capacity.");
        }

        CompositeOverSpanCore(y, x, coverage, colors, workspace.Bg, workspace.Fg, workspace.Work, count);
    }

    /// <summary>
    ///     Validates the <paramref name="y"/>/<paramref name="x"/>/<paramref name="coverageLength"/>
    ///     arguments shared by both <c>CompositeOverSpan</c> overloads.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="y"/> is outside <c>[0, Height)</c>, or when
    ///     <paramref name="x"/> or <c>x + coverageLength</c> is outside <c>[0, Width]</c>.
    /// </exception>
    private void ValidateCompositeOverSpanArgs(int y, int x, int coverageLength)
    {
        if (y < 0 || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "Y must be within the surface height.");
        }

        if (x < 0 || x > Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "X must be within the surface width.");
        }

        // Compare without computing "x + coverageLength": that sum can overflow a positive x
        // with an enormous coverageLength, wrapping negative and bypassing this check entirely.
        // "Width - x" cannot itself overflow because the check above already guarantees
        // "x <= Width", so "Width - x >= 0" always holds here.
        if (coverageLength > Width - x)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coverageLength), coverageLength, "X plus the coverage length must not exceed the surface width.");
        }
    }

    /// <summary>
    ///     Populates <paramref name="fg"/>'s byte channels from a single constant
    ///     <paramref name="color"/> broadcast to every pixel, scaling its alpha by that pixel's
    ///     own <paramref name="coverage"/> value, then shares the exact same
    ///     <see cref="CompositeOverSpanCoreShared"/> blend body - the only foreground-population
    ///     difference from the per-pixel-color overload below.
    /// </summary>
    private void CompositeOverSpanCore(
        int y, int x, ReadOnlySpan<float> coverage, Rgba32 color, RowChannelBuffers bg, RowChannelBuffers fg,
        CompositeWorkBuffers work, int count)
    {
        for (var i = 0; i < count; i++)
        {
            fg.RBytes[i] = color.R;
            fg.GBytes[i] = color.G;
            fg.BBytes[i] = color.B;
            var scaledAlpha = color.A * Math.Clamp(coverage[i], 0f, 1f);
            fg.ABytes[i] = (byte)Math.Clamp(MathF.Round(scaledAlpha, MidpointRounding.AwayFromZero), 0f, 255f);
        }

        CompositeOverSpanCoreShared(y, x, coverage, bg, fg, work, count);
    }

    /// <summary>
    ///     Per-pixel-color counterpart of <see cref="CompositeOverSpanCore(int, int, ReadOnlySpan{float}, Rgba32, RowChannelBuffers, RowChannelBuffers, CompositeWorkBuffers, int)"/>:
    ///     populates <paramref name="fg"/> from one <see cref="Rgba32"/> per pixel in
    ///     <paramref name="colors"/>, rather than broadcasting a single constant color, then
    ///     shares the exact same <see cref="CompositeOverSpanCoreShared"/> blend body.
    /// </summary>
    private void CompositeOverSpanCore(
        int y, int x, ReadOnlySpan<float> coverage, ReadOnlySpan<Rgba32> colors, RowChannelBuffers bg, RowChannelBuffers fg,
        CompositeWorkBuffers work, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var color = colors[i];
            fg.RBytes[i] = color.R;
            fg.GBytes[i] = color.G;
            fg.BBytes[i] = color.B;
            var scaledAlpha = color.A * Math.Clamp(coverage[i], 0f, 1f);
            fg.ABytes[i] = (byte)Math.Clamp(MathF.Round(scaledAlpha, MidpointRounding.AwayFromZero), 0f, 255f);
        }

        CompositeOverSpanCoreShared(y, x, coverage, bg, fg, work, count);
    }

    /// <summary>
    ///     Shared blend body for every <c>CompositeOverSpan</c> overload, regardless of whether
    ///     the foreground was populated from a single constant color or from one color per pixel:
    ///     reads the background row via <see cref="DeinterleaveRow"/>, blends via the shared
    ///     <see cref="CompositeOverRow"/> pipeline, and writes the result back into the surface
    ///     via <see cref="ReinterleaveRow"/>. <paramref name="fg"/> must already be populated (its
    ///     byte channels only - this method widens it to float itself) by the caller before this
    ///     method is invoked.
    /// </summary>
    private void CompositeOverSpanCoreShared(
        int y, int x, ReadOnlySpan<float> coverage, RowChannelBuffers bg, RowChannelBuffers fg,
        CompositeWorkBuffers work, int count)
    {
        var bgRow = GetRowSpanBytes(y).Slice(x * BytesPerPixel, count * BytesPerPixel);

        DeinterleaveRow(bgRow, bg, count);
        WidenAllToFloat(bg, count);
        WidenAllToFloat(fg, count);

        CompositeOverRow(bg, fg, work, count);

        NarrowRoundedClamp(work.OutR, bg.RBytes, count);
        NarrowRoundedClamp(work.OutG, bg.GBytes, count);
        NarrowRoundedClamp(work.OutB, bg.BBytes, count);
        NarrowRoundedClamp(work.OutA, bg.ABytes, count);
        ZeroColorWhereAlphaByteIsZero(bg.ABytes, bg.RBytes, bg.GBytes, bg.BBytes, count);

        // Restore the original background bytes for every column whose coverage was zero or
        // negative: per the documented contract such a column must be left completely untouched,
        // but it still flowed through the shared blend/normalization pipeline above (with its
        // foreground alpha forced to zero) so its blended result can be discarded here. Without
        // this, ZeroColorWhereAlphaByteIsZero would overwrite a fully-transparent background
        // pixel that legitimately holds nonzero RGB (for example, a premultiplied-adjacent
        // transparent fringe pixel) with (0, 0, 0, 0), corrupting data the caller never asked to
        // change. "bgRow" still holds the untouched original surface bytes at this point, because
        // only ReinterleaveRow (below) ever writes back into it.
        for (var i = 0; i < count; i++)
        {
            if (coverage[i] > 0f)
            {
                continue;
            }

            var offset = i * BytesPerPixel;
            bg.RBytes[i] = bgRow[offset];
            bg.GBytes[i] = bgRow[offset + 1];
            bg.BBytes[i] = bgRow[offset + 2];
            bg.ABytes[i] = bgRow[offset + 3];
        }

        ReinterleaveRow(bgRow, bg, count);
    }

    /// <summary>
    ///     Splits an interleaved RGBA row into the four planar byte channels of
    ///     <paramref name="destination"/>. This is a layout transform, not numeric work, so it is
    ///     kept as a plain scalar loop for clarity rather than forced through a tensor API.
    /// </summary>
    private static void DeinterleaveRow(ReadOnlySpan<byte> interleavedRow, RowChannelBuffers destination, int pixelCount)
    {
        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * BytesPerPixel;
            destination.RBytes[i] = interleavedRow[offset];
            destination.GBytes[i] = interleavedRow[offset + 1];
            destination.BBytes[i] = interleavedRow[offset + 2];
            destination.ABytes[i] = interleavedRow[offset + 3];
        }
    }

    /// <summary>
    ///     Combines the four planar byte channels of <paramref name="source"/> back into an
    ///     interleaved RGBA row (the inverse of <see cref="DeinterleaveRow"/>, kept as a plain
    ///     scalar loop for the same reason).
    /// </summary>
    private static void ReinterleaveRow(Span<byte> interleavedRow, RowChannelBuffers source, int pixelCount)
    {
        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * BytesPerPixel;
            interleavedRow[offset] = source.RBytes[i];
            interleavedRow[offset + 1] = source.GBytes[i];
            interleavedRow[offset + 2] = source.BBytes[i];
            interleavedRow[offset + 3] = source.ABytes[i];
        }
    }

    /// <summary>
    ///     Widens all four planar byte channels of <paramref name="buffers"/> to their planar
    ///     float counterparts via a vectorized, saturating conversion.
    /// </summary>
    private static void WidenAllToFloat(RowChannelBuffers buffers, int count)
    {
        WidenToFloat(buffers.RBytes, buffers.RFloat, count);
        WidenToFloat(buffers.GBytes, buffers.GFloat, count);
        WidenToFloat(buffers.BBytes, buffers.BFloat, count);
        WidenToFloat(buffers.ABytes, buffers.AFloat, count);
    }

    /// <summary>
    ///     Widens a planar byte channel to a planar float channel via a vectorized, saturating
    ///     conversion.
    /// </summary>
    private static void WidenToFloat(byte[] byteBuffer, float[] floatBuffer, int count) =>
        TensorPrimitives.ConvertSaturating<byte, float>(byteBuffer.AsSpan(0, count), floatBuffer.AsSpan(0, count));

    /// <summary>
    ///     Rounds a planar float channel (round-half-away-from-zero) and narrows it back to a
    ///     planar byte channel via a vectorized, saturating (clamping) conversion.
    /// </summary>
    private static void NarrowRoundedClamp(float[] floatBuffer, byte[] byteBuffer, int count)
    {
        var floatSpan = floatBuffer.AsSpan(0, count);
        TensorPrimitives.Round(floatSpan, MidpointRounding.AwayFromZero, floatSpan);
        TensorPrimitives.ConvertSaturating<float, byte>(floatSpan, byteBuffer.AsSpan(0, count));
    }

    /// <summary>
    ///     Computes <c>round(component * alpha / 255)</c> for a whole planar channel - the
    ///     premultiply formula - clamped to <c>[0, 255]</c>.
    /// </summary>
    private static void PremultiplyChannel(float[] componentFloat, float[] alphaFloat, byte[] destinationBytes, int count)
    {
        var componentSpan = componentFloat.AsSpan(0, count);
        var alphaSpan = alphaFloat.AsSpan(0, count);
        TensorPrimitives.Multiply(componentSpan, alphaSpan, componentSpan);
        TensorPrimitives.Divide(componentSpan, ByteMax, componentSpan);
        NarrowRoundedClamp(componentFloat, destinationBytes, count);
    }

    /// <summary>
    ///     Computes <c>round(premultipliedComponent * 255 / alpha)</c> for a whole planar
    ///     channel - the unpremultiply formula - clamped to <c>[0, 255]</c> (before the caller
    ///     applies the <c>alpha == 0</c> degenerate-case override).
    /// </summary>
    private static void UnpremultiplyChannel(float[] componentFloat, float[] alphaFloat, byte[] destinationBytes, int count)
    {
        var componentSpan = componentFloat.AsSpan(0, count);
        var alphaSpan = alphaFloat.AsSpan(0, count);
        TensorPrimitives.Multiply(componentSpan, ByteMax, componentSpan);
        TensorPrimitives.Divide(componentSpan, alphaSpan, componentSpan);
        NarrowRoundedClamp(componentFloat, destinationBytes, count);
    }

    /// <summary>
    ///     Computes the Porter-Duff "over" formula for a whole row - background
    ///     <paramref name="bg"/> composited under foreground <paramref name="fg"/> - writing the
    ///     <c>[0, 255]</c>-scaled result into <paramref name="work"/>. Both <paramref name="bg"/>
    ///     and <paramref name="fg"/> must already hold widened (byte-scale) float channels.
    /// </summary>
    private static void CompositeOverRow(RowChannelBuffers bg, RowChannelBuffers fg, CompositeWorkBuffers work, int count)
    {
        var bgA255 = bg.AFloat.AsSpan(0, count);
        var fgA255 = fg.AFloat.AsSpan(0, count);
        var bgAn = work.BgAn.AsSpan(0, count);
        var fgAn = work.FgAn.AsSpan(0, count);
        var oneMinusFgAn = work.OneMinusFgA.AsSpan(0, count);
        var outAn = work.OutA.AsSpan(0, count);

        // Normalize alpha channels from [0, 255] to [0, 1] into dedicated scratch spans, rather
        // than in place, so that bg.AFloat/fg.AFloat are left untouched for any other use within
        // the same row (and, for CompositeOver(Rgba32), so the constant foreground's widened
        // float channels remain valid to reuse unmodified across every row).
        TensorPrimitives.Divide(bgA255, ByteMax, bgAn);
        TensorPrimitives.Divide(fgA255, ByteMax, fgAn);
        TensorPrimitives.Subtract(1f, fgAn, oneMinusFgAn);

        // outA = fgA + bgA * (1 - fgA), kept normalized [0, 1] until every channel has divided by
        // it, then scaled back to [0, 255] as the very last step
        TensorPrimitives.Multiply(bgAn, oneMinusFgAn, outAn);
        TensorPrimitives.Add(outAn, fgAn, outAn);

        CompositeOverChannel(bg.RFloat, fg.RFloat, bgAn, fgAn, oneMinusFgAn, outAn, work.Term, work.OutR, count);
        CompositeOverChannel(bg.GFloat, fg.GFloat, bgAn, fgAn, oneMinusFgAn, outAn, work.Term, work.OutG, count);
        CompositeOverChannel(bg.BFloat, fg.BFloat, bgAn, fgAn, oneMinusFgAn, outAn, work.Term, work.OutB, count);

        // Only now scale the normalized outA up to [0, 255] for narrowing - every channel above
        // divided by the still-normalized outAn.
        TensorPrimitives.Multiply(outAn, ByteMax, outAn);
    }

    /// <summary>
    ///     Computes <c>(fgC * fgA + bgC * bgA * (1 - fgA)) / outA</c> for one <c>[0, 255]</c>-
    ///     scaled color channel of a whole row, where the alpha terms (<paramref name="fgAn"/>,
    ///     <paramref name="oneMinusFgAn"/>, <paramref name="outAn"/>) are <c>[0, 1]</c>-scaled.
    /// </summary>
    private static void CompositeOverChannel(
        float[] bgC, float[] fgC, Span<float> bgAn, Span<float> fgAn, Span<float> oneMinusFgAn, Span<float> outAn,
        float[] termScratch, float[] outC, int count)
    {
        var bgCSpan = bgC.AsSpan(0, count);
        var fgCSpan = fgC.AsSpan(0, count);
        var outCSpan = outC.AsSpan(0, count);
        var termSpan = termScratch.AsSpan(0, count);

        // outC = fgC * fgA
        TensorPrimitives.Multiply(fgCSpan, fgAn, outCSpan);

        // term = bgC * bgA * (1 - fgA); outC += term
        TensorPrimitives.Multiply(bgCSpan, bgAn, termSpan);
        TensorPrimitives.Multiply(termSpan, oneMinusFgAn, termSpan);
        TensorPrimitives.Add(outCSpan, termSpan, outCSpan);

        // outC /= outA (normalized [0, 1] outA - see remarks on ZeroColorWhereAlphaByteIsZero for
        // why the 0/0 = NaN this produces when outA == 0 is never observed downstream)
        TensorPrimitives.Divide(outCSpan, outAn, outCSpan);
    }

    /// <summary>
    ///     Zeroes the color channel bytes of every pixel whose alpha byte is zero, implementing
    ///     the documented "alpha == 0 implies every color channel is 0" degenerate case shared by
    ///     <see cref="UnpremultiplyAlpha"/> (fully transparent premultiplied input) and
    ///     <see cref="CompositeOver(Surface)"/>/<see cref="CompositeOver(Rgba32)"/> (fully
    ///     transparent composited output, where the "over" formula's division by <c>outA</c>
    ///     otherwise produces an unobserved <c>0 / 0 = NaN</c>).
    /// </summary>
    private static void ZeroColorWhereAlphaByteIsZero(byte[] alphaBytes, byte[] rBytes, byte[] gBytes, byte[] bBytes, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (alphaBytes[i] == 0)
            {
                rBytes[i] = 0;
                gBytes[i] = 0;
                bBytes[i] = 0;
            }
        }
    }

    /// <summary>
    ///     A set of <see cref="ArrayPool{T}"/>-rented planar scratch buffers (four byte channels
    ///     plus their widened float counterparts) sized once to a row's padded pixel count and
    ///     reused across every row of a single bulk pixel operation, released together on
    ///     <see cref="Dispose"/>.
    /// </summary>
    internal sealed class RowChannelBuffers : IDisposable
    {
        public RowChannelBuffers(int pixelsPerRow)
        {
            RBytes = ArrayPool<byte>.Shared.Rent(pixelsPerRow);
            GBytes = ArrayPool<byte>.Shared.Rent(pixelsPerRow);
            BBytes = ArrayPool<byte>.Shared.Rent(pixelsPerRow);
            ABytes = ArrayPool<byte>.Shared.Rent(pixelsPerRow);
            RFloat = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            GFloat = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            BFloat = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            AFloat = ArrayPool<float>.Shared.Rent(pixelsPerRow);
        }

        public byte[] RBytes { get; }

        public byte[] GBytes { get; }

        public byte[] BBytes { get; }

        public byte[] ABytes { get; }

        public float[] RFloat { get; }

        public float[] GFloat { get; }

        public float[] BFloat { get; }

        public float[] AFloat { get; }

        public void Dispose()
        {
            // Clear rented arrays on return: they hold caller-provided pixel channel data, and
            // ArrayPool.Return defaults to clearArray: false, which would otherwise let a later
            // renter of ArrayPool<T>.Shared observe leftover image data from this operation.
            ArrayPool<byte>.Shared.Return(RBytes, clearArray: true);
            ArrayPool<byte>.Shared.Return(GBytes, clearArray: true);
            ArrayPool<byte>.Shared.Return(BBytes, clearArray: true);
            ArrayPool<byte>.Shared.Return(ABytes, clearArray: true);
            ArrayPool<float>.Shared.Return(RFloat, clearArray: true);
            ArrayPool<float>.Shared.Return(GFloat, clearArray: true);
            ArrayPool<float>.Shared.Return(BFloat, clearArray: true);
            ArrayPool<float>.Shared.Return(AFloat, clearArray: true);
        }
    }

    /// <summary>
    ///     A set of <see cref="ArrayPool{T}"/>-rented planar float scratch buffers used to
    ///     accumulate the intermediate and final results of the "over" compositing formula for a
    ///     single row, released together on <see cref="Dispose"/>.
    /// </summary>
    internal sealed class CompositeWorkBuffers : IDisposable
    {
        public CompositeWorkBuffers(int pixelsPerRow)
        {
            OutR = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            OutG = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            OutB = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            OutA = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            BgAn = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            FgAn = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            OneMinusFgA = ArrayPool<float>.Shared.Rent(pixelsPerRow);
            Term = ArrayPool<float>.Shared.Rent(pixelsPerRow);
        }

        public float[] OutR { get; }

        public float[] OutG { get; }

        public float[] OutB { get; }

        public float[] OutA { get; }

        public float[] BgAn { get; }

        public float[] FgAn { get; }

        public float[] OneMinusFgA { get; }

        public float[] Term { get; }

        public void Dispose()
        {
            // Clear rented arrays on return: they hold derived pixel-channel values, and
            // ArrayPool.Return defaults to clearArray: false, which would otherwise let a later
            // renter of ArrayPool<T>.Shared observe leftover image data from this operation.
            ArrayPool<float>.Shared.Return(OutR, clearArray: true);
            ArrayPool<float>.Shared.Return(OutG, clearArray: true);
            ArrayPool<float>.Shared.Return(OutB, clearArray: true);
            ArrayPool<float>.Shared.Return(OutA, clearArray: true);
            ArrayPool<float>.Shared.Return(BgAn, clearArray: true);
            ArrayPool<float>.Shared.Return(FgAn, clearArray: true);
            ArrayPool<float>.Shared.Return(OneMinusFgA, clearArray: true);
            ArrayPool<float>.Shared.Return(Term, clearArray: true);
        }
    }

    /// <summary>
    ///     A reusable set of scratch buffers for the internal
    ///     <see cref="CompositeOverSpan(int, int, ReadOnlySpan{float}, Rgba32, CompositeSpanWorkspace)"/>
    ///     overload, so a caller that composites many spans of the same or smaller width in a
    ///     tight loop - for example <see cref="DemaConsulting.CanvasNet.Drawing.ScanlineRasterizer.Fill(Surface, System.Collections.Generic.IReadOnlyList{System.Collections.Generic.List{System.Numerics.Vector2}}, Rgba32, DemaConsulting.CanvasNet.Drawing.FillRule, DemaConsulting.CanvasNet.Geometry.Rect)"/>,
    ///     once per rasterized row - can rent its <see cref="RowChannelBuffers"/>/
    ///     <see cref="CompositeWorkBuffers"/> scratch arrays exactly once for the whole loop
    ///     instead of once per row, eliminating that hot path's per-row
    ///     <see cref="ArrayPool{T}"/> rent/return churn.
    /// </summary>
    /// <remarks>
    ///     Internal (not public): this type exposes an implementation-detail allocation strategy,
    ///     not new externally observable behavior. Construct one instance sized to the largest
    ///     span width used within a loop, reuse it for every call in that loop, then dispose it
    ///     once the loop completes.
    /// </remarks>
    internal sealed class CompositeSpanWorkspace : IDisposable
    {
        /// <summary>
        ///     Initializes a workspace whose scratch buffers can serve any span of up to
        ///     <paramref name="capacity"/> pixels.
        /// </summary>
        /// <param name="capacity">
        ///     The largest coverage-span length this workspace will be asked to serve. Must be
        ///     greater than zero.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="capacity"/> is less than or equal to zero.
        /// </exception>
        public CompositeSpanWorkspace(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
            }

            Capacity = capacity;
            Bg = new RowChannelBuffers(capacity);
            Fg = new RowChannelBuffers(capacity);
            Work = new CompositeWorkBuffers(capacity);
        }

        /// <summary>
        ///     The largest coverage-span length this workspace's buffers can serve.
        /// </summary>
        public int Capacity { get; }

        /// <summary>The reused background per-channel scratch buffers.</summary>
        internal RowChannelBuffers Bg { get; }

        /// <summary>The reused foreground per-channel scratch buffers.</summary>
        internal RowChannelBuffers Fg { get; }

        /// <summary>The reused blend intermediate/result scratch buffers.</summary>
        internal CompositeWorkBuffers Work { get; }

        /// <summary>
        ///     Returns every rented scratch array to <see cref="ArrayPool{T}.Shared"/>. Must be
        ///     called once the whole loop reusing this workspace has completed.
        /// </summary>
        public void Dispose()
        {
            Bg.Dispose();
            Fg.Dispose();
            Work.Dispose();
        }
    }
}

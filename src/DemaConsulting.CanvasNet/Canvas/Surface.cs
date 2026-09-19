using System.Runtime.InteropServices;

namespace CanvasNet.Canvas;

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
/// </remarks>
public sealed class Surface
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
    ///     Initializes a new instance of the <see cref="Surface"/> class with the specified
    ///     dimensions, fully transparent (all pixel bytes zero).
    /// </summary>
    /// <param name="width">The width of the surface, in pixels. Must be greater than zero.</param>
    /// <param name="height">The height of the surface, in pixels. Must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="width"/> or <paramref name="height"/> is less than or
    ///     equal to zero.
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

        Width = width;
        Height = height;

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
            var row = GetRowSpan(y);
            if (x < 0 || x >= Width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "X must be within the surface width.");
            }

            return row[x];
        }
        set
        {
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
    /// <remarks>
    ///     The returned span aliases this surface's internal buffer directly - no data is copied,
    ///     so writes through the span are immediately visible through the indexer and vice versa.
    ///     The span's length is always exactly <c>Width * 4</c>, regardless of the surface's
    ///     internal row padding (see the class-level remarks): any padding bytes physically
    ///     stored beyond the row's <c>Width * 4</c> pixel bytes are never included in, or
    ///     observable through, the returned span.
    /// </remarks>
    public Span<byte> GetRowSpanBytes(int y)
    {
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
    /// <remarks>
    ///     Uses <see cref="MemoryMarshal"/>'s span-reinterpretation cast to reinterpret the raw
    ///     byte row returned by <see cref="GetRowSpanBytes"/> as <see cref="Rgba32"/> values
    ///     without copying any data, so writes through the returned span are immediately visible
    ///     through the indexer and <see cref="GetRowSpanBytes"/>, and vice versa.
    /// </remarks>
    public Span<Rgba32> GetRowSpan(int y) => MemoryMarshal.Cast<byte, Rgba32>(GetRowSpanBytes(y));

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
}

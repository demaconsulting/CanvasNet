// cspell:ignore hrefs embedder
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     Unit tests for the contract types of the image sources: <see cref="ImageSourceRequest"/> (its sizing rule and
///     its content), <see cref="ImageSourceResult"/>, <see cref="ImageSourceDiagnostic"/>,
///     <see cref="EmbeddedImage"/> and <see cref="ImageSourceException"/>.
/// </summary>
public class ImageSourceContractTests
{
    /// <summary>
    ///     Creates a request with a requested size.
    /// </summary>
    /// <param name="width">The requested width.</param>
    /// <param name="height">The requested height.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Sized(int? width, int? height)
    {
        return new ImageSourceRequest { Href = "x", PixelWidth = width, PixelHeight = height };
    }

    /// <summary>
    ///     Test the sizing rule of <see cref="ImageSourceRequest.SizeFor(int, int, bool)"/>: natural, one dimension, both
    ///     (the box or fitted inside it), and the clamping.
    /// </summary>
    /// <param name="requestedWidth">The requested width.</param>
    /// <param name="requestedHeight">The requested height.</param>
    /// <param name="fitInside">Whether to fit inside the box.</param>
    /// <param name="expectedWidth">The expected width.</param>
    /// <param name="expectedHeight">The expected height.</param>
    [Theory]
    [InlineData(null, null, false, 200, 100)]
    [InlineData(null, null, true, 200, 100)]
    [InlineData(50, null, false, 50, 25)]
    [InlineData(null, 50, false, 100, 50)]
    [InlineData(30, 30, false, 30, 30)]
    [InlineData(30, 30, true, 30, 15)]
    [InlineData(400, 100, true, 200, 100)]
    [InlineData(1, null, false, 1, 1)]
    [InlineData(0, -5, false, 1, 1)]
    [InlineData(20000, 20000, false, 8192, 8192)]
    [InlineData(20000, null, false, 8192, 8192)]
    public void ImageSourceRequest_SizeFor_AppliesSizingRule(int? requestedWidth, int? requestedHeight, bool fitInside, int expectedWidth, int expectedHeight)
    {
        // Arrange: a picture of 200 by 100 pixels
        var request = Sized(requestedWidth, requestedHeight);

        // Act
        var size = request.SizeFor(200, 100, fitInside);

        // Assert
        Assert.Equal((expectedWidth, expectedHeight), size);
    }

    /// <summary>
    ///     Test that a natural size below one pixel is rejected, and a natural size above the maximum is clamped.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_SizeFor_InvalidOrHugeNaturalSize_ThrowsOrClamps()
    {
        // Arrange
        var request = Sized(null, null);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => request.SizeFor(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => request.SizeFor(10, 0));
        Assert.Equal((Surface.MaxDimension, 5), request.SizeFor(100000, 5));
    }

    /// <summary>
    ///     Test that a request with a file name has content that opens as a read-only seekable stream, over both an
    ///     array-backed and a non-array memory, and that an empty file still counts as having content.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_OpenContent_FileRequest_ReturnsReadOnlySeekableStream()
    {
        // Arrange: an array slice, a memory that is not array-backed, and an empty file
        var bytes = new byte[] { 9, 1, 2, 3, 9 };
        var slice = new ImageSourceRequest { Href = "a", FileName = "a", Content = new ReadOnlyMemory<byte>(bytes, 1, 3) };
        var native = new ImageSourceRequest { Href = "a", FileName = "a", Content = new ForeignMemory([4, 5]).Memory };
        var empty = new ImageSourceRequest { Href = "a", FileName = "a" };

        // Act
        using var sliceStream = slice.OpenContent();
        using var nativeStream = native.OpenContent();
        using var emptyStream = empty.OpenContent();

        // Assert
        Assert.True(slice.HasContent);
        Assert.True(empty.HasContent);
        Assert.False(sliceStream.CanWrite);
        Assert.True(sliceStream.CanSeek);
        var copy = new MemoryStream();
        sliceStream.CopyTo(copy);
        Assert.Equal([1, 2, 3], copy.ToArray());
        var copy2 = new MemoryStream();
        nativeStream.CopyTo(copy2);
        Assert.Equal([4, 5], copy2.ToArray());
        Assert.Equal(0, emptyStream.Length);
    }

    /// <summary>
    ///     Test that a request without a file name (a scheme-claimed request) has no content and refuses to open it.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_OpenContent_SchemeRequest_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new ImageSourceRequest { Href = "test-scheme:frame" };

        // Act / Assert
        Assert.False(request.HasContent);
        Assert.Null(request.FileName);
        Assert.Null(request.Directory);
        Assert.Null(request.Resolver);
        Assert.False(request.ThrowOnUnsupportedFeature);
        Assert.Equal(CancellationToken.None, request.CancellationToken);
        var error = Assert.Throws<InvalidOperationException>(() => request.OpenContent());
        Assert.Contains("test-scheme:frame", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a result needs a surface, has empty notes by default and keeps what it is given.
    /// </summary>
    [Fact]
    public void ImageSourceResult_Constructor_RequiresSurfaceAndDefaultsToEmpty()
    {
        // Arrange
        using var surface = new Surface(2, 2);

        // Act
        var plain = new ImageSourceResult(surface);
        var full = new ImageSourceResult(surface)
        {
            Diagnostics = [new ImageSourceDiagnostic("s", ImageDiagnosticSeverity.Info, "c", "m")],
            Echo = ["e"],
            Format = "f"
        };

        // Assert
        Assert.Throws<ArgumentNullException>(() => new ImageSourceResult(null!));
        Assert.Same(surface, plain.Surface);
        Assert.Empty(plain.Diagnostics);
        Assert.Empty(plain.Echo);
        Assert.Null(plain.Format);
        Assert.Equal(("s", ImageDiagnosticSeverity.Info, "c", "m"), (full.Diagnostics[0].Source, full.Diagnostics[0].Severity, full.Diagnostics[0].Code, full.Diagnostics[0].Message));
        Assert.Equal(["e"], full.Echo);
        Assert.Equal("f", full.Format);
    }

    /// <summary>
    ///     Test that <see cref="ImageSourceRequest.RemainingPixels"/> is unlimited on a hand-built request, that
    ///     <see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/> accepts what fits (exactly the remainder too)
    ///     and throws <see cref="ImageSourceBudgetException"/> with the numbers for what does not, and that the
    ///     exception message names them.
    /// </summary>
    [Fact]
    public void ImageSourceRequest_EnsureWithinBudget_ThrowsOnlyWhenOverRemainingPixels()
    {
        // Arrange
        var unlimited = Sized(null, null);
        var limited = new ImageSourceRequest { Href = "x", RemainingPixels = 600 };

        // Act
        unlimited.EnsureWithinBudget(Surface.MaxDimension, Surface.MaxDimension);
        limited.EnsureWithinBudget(20, 30);
        limited.EnsureWithinBudget(0, 1000);
        var ex = Assert.Throws<ImageSourceBudgetException>(() => limited.EnsureWithinBudget(21, 30));

        // Assert
        Assert.Equal(long.MaxValue, unlimited.RemainingPixels);
        Assert.Equal((630L, 600L), (ex.RequestedPixels, ex.RemainingPixels));
        Assert.Contains("630", ex.Message, StringComparison.Ordinal);
        Assert.Contains("600", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <see cref="ImageSourceRequest.EnsureWithinBudget(int, int)"/> treats
    ///     <see cref="ImageSourceRequest.ReservedPixels"/> as already charged: only the pixels beyond it must fit in
    ///     <see cref="ImageSourceRequest.RemainingPixels"/>, and the exception reports the pixels the source could have
    ///     used (reserved plus remaining).
    /// </summary>
    [Fact]
    public void ImageSourceRequest_EnsureWithinBudget_ReservedPixelsAreAlreadyCharged()
    {
        // Arrange: 1000 reserved and 500 more allowed
        var request = new ImageSourceRequest { Href = "x", ReservedPixels = 1000, RemainingPixels = 500 };
        var byDefault = Sized(null, null);

        // Act
        request.EnsureWithinBudget(10, 100);
        request.EnsureWithinBudget(10, 50);
        request.EnsureWithinBudget(10, 150);
        var ex = Assert.Throws<ImageSourceBudgetException>(() => request.EnsureWithinBudget(10, 151));

        // Assert
        Assert.Equal(0L, byDefault.ReservedPixels);
        Assert.Equal((1510L, 1500L), (ex.RequestedPixels, ex.RemainingPixels));
    }
    /// <summary>
    ///     Test the shape of the small value types: severity names, the embedded image record and its depth.
    /// </summary>
    [Fact]
    public void ImageSourceContract_ValueTypes_HaveDocumentedShape()
    {
        // Arrange / Act
        var embedded = new EmbeddedImage("a.png", "png", 4, 3) { Depth = 2 };

        // Assert
        Assert.Equal(["Info", "Warning", "Unsupported"], Enum.GetNames<ImageDiagnosticSeverity>());
        Assert.Equal(("a.png", "png", 4, 3, 2), (embedded.Href, embedded.Format, embedded.Width, embedded.Height, embedded.Depth));
        Assert.Equal(0, new EmbeddedImage("a", "b", 1, 1).Depth);
        Assert.Equal("svg-image-unresolved", FolderImageResolver.UnresolvedCode);
        Assert.Equal("svg-image-too-large", FolderImageResolver.TooLargeCode);
        var options = new FolderImageResolverOptions();
        Assert.Equal((4, 64, false), (options.MaxDepth, options.MaxImages, options.ThrowOnUnsupportedFeature));
        Assert.Equal(CancellationToken.None, options.CancellationToken);
    }

    /// <summary>
    ///     Test that the exception keeps the link, the embedder and the inner exception.
    /// </summary>
    [Fact]
    public void ImageSourceException_Constructor_KeepsHrefEmbedderAndInner()
    {
        // Arrange
        var inner = new InvalidDataException("broken");

        // Act
        var exception = new ImageSourceException("failed", "a.png", "outer.svg", inner);
        var top = new ImageSourceException("failed", "a.png", null, inner);

        // Assert
        Assert.Equal("failed", exception.Message);
        Assert.Equal("a.png", exception.Href);
        Assert.Equal("outer.svg", exception.Embedder);
        Assert.Same(inner, exception.InnerException);
        Assert.Null(top.Embedder);
    }

    /// <summary>
    ///     A memory owner that is not backed by a plain array, to reach the copying branch of
    ///     <see cref="ImageSourceRequest.OpenContent"/>.
    /// </summary>
    private sealed class ForeignMemory : System.Buffers.MemoryManager<byte>
    {
        /// <summary>The bytes.</summary>
        private readonly byte[] _bytes;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ForeignMemory"/> class.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        public ForeignMemory(byte[] bytes)
        {
            _bytes = bytes;
        }

        /// <inheritdoc />
        public override Span<byte> GetSpan()
        {
            return _bytes;
        }

        /// <inheritdoc />
        public override System.Buffers.MemoryHandle Pin(int elementIndex = 0)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Unpin()
        {
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
        }
    }
}

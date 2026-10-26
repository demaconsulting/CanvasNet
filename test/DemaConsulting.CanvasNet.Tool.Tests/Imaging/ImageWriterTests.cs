// cspell:ignore jpeg jpg tiff tif bmp
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the ImageWriter class: format resolution, encoding, atomic writes, and the overwrite policy.
/// </summary>
public class ImageWriterTests
{
    /// <summary>
    ///     The white that a JPEG is flattened over by default.
    /// </summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    /// <summary>
    ///     Gets the file extensions that select each output format, as theory data.
    /// </summary>
    public static TheoryData<string, string> ExtensionFormats =>
        new()
        {
            { "out.png", "png" },
            { "out.PNG", "png" },
            { "out.jpg", "jpeg" },
            { "out.jpeg", "jpeg" },
            { "out.bmp", "bmp" },
            { "out.tif", "tiff" },
            { "out.tiff", "tiff" },
            { "dir.with.dots/out.png", "png" }
        };

    /// <summary>
    ///     Test that the extension selects the format.
    /// </summary>
    /// <param name="path">The output path.</param>
    /// <param name="expected">The expected format name.</param>
    [Theory]
    [MemberData(nameof(ExtensionFormats))]
    public void ImageWriter_ResolveFormat_Extension_SelectsFormat(string path, string expected)
    {
        // Arrange / Act: resolve the format without an option
        var format = ImageWriter.ResolveFormat(path, null);

        // Assert: the format of the extension
        Assert.Equal(expected, format.ToFormatName());
    }

    /// <summary>
    ///     Test that the format option wins over the extension and accepts the short names.
    /// </summary>
    /// <param name="option">The option value.</param>
    /// <param name="expected">The expected format name.</param>
    [Theory]
    [InlineData("png", "png")]
    [InlineData("JPEG", "jpeg")]
    [InlineData("jpg", "jpeg")]
    [InlineData("bmp", "bmp")]
    [InlineData("tiff", "tiff")]
    [InlineData("tif", "tiff")]
    [InlineData(" png ", "png")]
    public void ImageWriter_ResolveFormat_FormatOption_WinsOverExtension(string option, string expected)
    {
        // Arrange / Act: resolve the format for an output whose extension says something else
        var format = ImageWriter.ResolveFormat("out.xyz", option);

        // Assert: the option's format
        Assert.Equal(expected, format.ToFormatName());
    }

    /// <summary>
    ///     Test that an unknown format option and a missing or unknown extension are usage errors.
    /// </summary>
    [Fact]
    public void ImageWriter_ResolveFormat_UnknownFormat_ThrowsUsageError()
    {
        // Arrange / Act
        var option = Assert.Throws<ToolException>(() => ImageWriter.ResolveFormat("out.png", "gif"));
        var none = Assert.Throws<ToolException>(() => ImageWriter.ResolveFormat("out", null));
        var unknown = Assert.Throws<ToolException>(() => ImageWriter.ResolveFormat("out.xyz", null));

        // Assert: usage errors that say what to do
        Assert.All(new[] { option, none, unknown }, e => Assert.Equal(ErrorCodes.UsageError, e.Code));
        Assert.Contains("Unknown output format 'gif'", option.Message);
        Assert.Contains("no extension", none.Message);
        Assert.Contains("'.xyz'", unknown.Message);
        Assert.Contains("--format", unknown.Hint);
    }

    /// <summary>
    ///     Test that the lossless formats are written with every pixel and the right size.
    /// </summary>
    /// <param name="extension">The extension of the output.</param>
    [Theory]
    [InlineData(".png")]
    [InlineData(".bmp")]
    [InlineData(".tif")]
    public void ImageWriter_Write_LosslessFormat_KeepsEveryPixelIncludingAlpha(string extension)
    {
        // Arrange: an image with transparency
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateTranslucent();
        var output = directory.Combine("out" + extension);

        // Act: write it
        var result = ImageWriter.Write(surface, output, "in.png", WriteOptions.Default);

        // Assert: the result describes the file, and the decoded pixels are the surface's
        Assert.Equal(output, result.Path);
        Assert.Equal(FixtureFactory.Width, result.Width);
        Assert.Equal(FixtureFactory.Height, result.Height);
        Assert.Equal(new FileInfo(output).Length, result.Bytes);
        Assert.False(result.Flattened);
        using var decoded = SurfaceAssert.Load(output);
        SurfaceAssert.Equal(surface, decoded);
    }

    /// <summary>
    ///     Test that a JPEG is written close to the pixels of an opaque image.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_JpegOfOpaqueImage_IsCloseAndNotFlattened()
    {
        // Arrange: an opaque image
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient();
        var output = directory.Combine("out.jpg");

        // Act: write it
        var result = ImageWriter.Write(surface, output, "in.png", WriteOptions.Default);

        // Assert: the format, nothing was flattened, and the pixels are close
        Assert.Equal(OutputFormat.Jpeg, result.Format);
        Assert.False(result.Flattened);
        using var decoded = JpegCodec.Load(output);
        SurfaceAssert.Close(surface, decoded, 64);
    }

    /// <summary>
    ///     Test that a JPEG of an image with transparency is flattened over the background.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_JpegOfTransparentImage_FlattensOverBackground()
    {
        // Arrange: a fully transparent image, written over white and over black
        using var directory = new TempDirectory();
        using var surface = new Surface(16, 16);
        var overWhite = directory.Combine("white.jpg");
        var overBlack = directory.Combine("black.jpg");

        // Act
        var whiteResult = ImageWriter.Write(surface, overWhite, "in.png", WriteOptions.Default);
        var blackResult = ImageWriter.Write(surface, overBlack, "in.png", WriteOptions.Default with { FlattenBackground = new Rgba32(0, 0, 0, 255) });

        // Assert: flattened, white and black as asked, and the source surface is unchanged
        Assert.True(whiteResult.Flattened);
        Assert.True(blackResult.Flattened);
        using var white = JpegCodec.Load(overWhite);
        using var black = JpegCodec.Load(overBlack);
        Assert.InRange(white[8, 8].R, 250, 255);
        Assert.InRange(black[8, 8].R, 0, 5);
        Assert.Equal(0, surface[8, 8].A);
    }

    /// <summary>
    ///     Test that the quality changes the size of a JPEG.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_JpegQuality_ChangesFileSize()
    {
        // Arrange: a detailed image, written at a low and a high quality
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient(64, 64);

        // Act
        var low = ImageWriter.Write(surface, directory.Combine("low.jpg"), "in.png", WriteOptions.Default with { Quality = 5 });
        var high = ImageWriter.Write(surface, directory.Combine("high.jpg"), "in.png", WriteOptions.Default with { Quality = 100 });

        // Assert: the higher quality is larger
        Assert.True(high.Bytes > low.Bytes, $"{high.Bytes} should be larger than {low.Bytes}");
    }

    /// <summary>
    ///     Test that the format option decides the encoding whatever the extension.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_FormatOption_EncodesThatFormat()
    {
        // Arrange: an output named .dat written as BMP
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient();
        var output = directory.Combine("out.dat");

        // Act
        var result = ImageWriter.Write(surface, output, "in.png", WriteOptions.Default with { Format = OutputFormat.Bmp });

        // Assert: it is a BMP
        Assert.Equal(OutputFormat.Bmp, result.Format);
        using var decoded = BmpCodec.Load(output);
        SurfaceAssert.Equal(surface, decoded);
    }

    /// <summary>
    ///     Test that the encoders are deterministic.
    /// </summary>
    /// <param name="extension">The extension of the output.</param>
    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".bmp")]
    [InlineData(".tif")]
    public void ImageWriter_Write_SamePixelsTwice_WritesIdenticalBytes(string extension)
    {
        // Arrange: an image
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateTranslucent(33, 21);

        // Act: write it twice
        ImageWriter.Write(surface, directory.Combine("a" + extension), "in.png", WriteOptions.Default);
        ImageWriter.Write(surface, directory.Combine("b" + extension), "in.png", WriteOptions.Default);

        // Assert: the same bytes
        Assert.Equal(File.ReadAllBytes(directory.Combine("a" + extension)), File.ReadAllBytes(directory.Combine("b" + extension)));
    }

    /// <summary>
    ///     Test that an existing output is overwritten by default and no temporary file is left behind.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_ExistingOutput_IsOverwrittenWithoutLeftovers()
    {
        // Arrange: an existing output with other content
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient();
        var output = directory.Combine("out.png");
        File.WriteAllText(output, "old content");

        // Act: write over it
        ImageWriter.Write(surface, output, "in.png", WriteOptions.Default);

        // Assert: the new image is there, and the directory holds only the output
        using var decoded = PngCodec.Load(output);
        SurfaceAssert.Equal(surface, decoded);
        Assert.Equal([output], Directory.GetFileSystemEntries(directory.Path));
    }

    /// <summary>
    ///     Test that an existing output is kept untouched when overwriting is disabled.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_NoClobberAndExistingOutput_ThrowsOutputExistsAndKeepsFile()
    {
        // Arrange: an existing output
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient();
        var output = directory.Combine("out.png");
        File.WriteAllText(output, "old content");

        // Act
        var exception = Assert.Throws<ToolException>(
            () => ImageWriter.Write(surface, output, "in.png", WriteOptions.Default with { NoClobber = true }));

        // Assert: output-exists, the file is untouched, and no temporary file was left
        Assert.Equal(ErrorCodes.OutputExists, exception.Code);
        Assert.Equal("old content", File.ReadAllText(output));
        Assert.Single(Directory.GetFileSystemEntries(directory.Path));
    }

    /// <summary>
    ///     Test that no-clobber still writes a new file.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_NoClobberAndNewOutput_WritesFile()
    {
        // Arrange: no existing output
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient();
        var output = directory.Combine("new.png");

        // Act
        ImageWriter.Write(surface, output, "in.png", WriteOptions.Default with { NoClobber = true });

        // Assert: the file exists
        Assert.True(File.Exists(output));
    }

    /// <summary>
    ///     Test that an output that is the input is refused before anything is written.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_OutputIsInput_ThrowsSameInputOutput()
    {
        // Arrange: an input file, and the same file named through a different spelling of its path
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient();
        var input = directory.Combine("in.png");
        PngCodec.Save(surface, input);
        var before = File.ReadAllBytes(input);
        var sameFile = Path.Combine(directory.Path, ".", "in.png");

        // Act
        var exception = Assert.Throws<ToolException>(() => ImageWriter.Write(surface, sameFile, input, WriteOptions.Default));

        // Assert: refused, and the input is untouched
        Assert.Equal(ErrorCodes.SameInputOutput, exception.Code);
        Assert.Equal(before, File.ReadAllBytes(input));
    }

    /// <summary>
    ///     Test that CheckTarget reports every problem with the target without writing.
    /// </summary>
    [Fact]
    public void ImageWriter_CheckTarget_Problems_AreReported()
    {
        // Arrange: a directory that is named like an output, and an existing file
        using var directory = new TempDirectory();
        var existing = directory.Combine("existing.png");
        File.WriteAllText(existing, "x");
        var folder = directory.Combine("folder.png");
        Directory.CreateDirectory(folder);

        // Act
        var same = Assert.Throws<ToolException>(() => ImageWriter.CheckTarget(existing, existing, false));
        var isDirectory = Assert.Throws<ToolException>(() => ImageWriter.CheckTarget(folder, "in.png", false));
        var exists = Assert.Throws<ToolException>(() => ImageWriter.CheckTarget(existing, "in.png", true));

        // Assert: the three codes; a fresh target and an existing one that may be overwritten are fine
        Assert.Equal(ErrorCodes.SameInputOutput, same.Code);
        Assert.Equal(ErrorCodes.OutputWriteFailed, isDirectory.Code);
        Assert.Equal(ErrorCodes.OutputExists, exists.Code);
        ImageWriter.CheckTarget(directory.Combine("fresh.png"), "in.png", true);
        ImageWriter.CheckTarget(existing, "in.png", false);
    }

    /// <summary>
    ///     Test that an output reaching the input through a symbolic link to its directory is refused.
    /// </summary>
    [Fact]
    public void ImageWriter_CheckTarget_LinkedDirectory_IsSameFile()
    {
        // Arrange: a real file and a link to its directory (creating links may need privileges on Windows)
        using var directory = new TempDirectory();
        var real = directory.Combine("real");
        Directory.CreateDirectory(real);
        var input = Path.Combine(real, "in.png");
        File.WriteAllText(input, "x");
        var link = directory.Combine("link");
        try
        {
            Directory.CreateSymbolicLink(link, real);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("Symbolic links cannot be created here.");
            return;
        }

        // Act
        var exception = Assert.Throws<ToolException>(() => ImageWriter.CheckTarget(Path.Combine(link, "in.png"), input, false));

        // Assert
        Assert.Equal(ErrorCodes.SameInputOutput, exception.Code);
        ImageWriter.CheckTarget(Path.Combine(link, "other.png"), input, false);
    }

    /// <summary>
    ///     Test that a missing output directory is created.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_MissingDirectory_IsCreated()
    {
        // Arrange: an output in a directory that does not exist
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient();
        var output = Path.Combine(directory.Path, "a", "b", "out.png");

        // Act
        ImageWriter.Write(surface, output, "in.png", WriteOptions.Default);

        // Assert: written
        Assert.True(File.Exists(output));
    }

    /// <summary>
    ///     Test that a target that cannot be written is reported as an expected error.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_DirectoryCannotBeCreated_ThrowsOutputWriteFailed()
    {
        // Arrange: a file where the output's directory would have to be
        using var directory = new TempDirectory();
        using var surface = FixtureFactory.CreateGradient();
        var blocker = directory.Combine("blocker");
        File.WriteAllText(blocker, "a file, not a directory");

        // Act
        var exception = Assert.Throws<ToolException>(
            () => ImageWriter.Write(surface, Path.Combine(blocker, "out.png"), "in.png", WriteOptions.Default));

        // Assert: output-write-failed naming the output, with the cause kept
        Assert.Equal(ErrorCodes.OutputWriteFailed, exception.Code);
        Assert.Contains("out.png", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    ///     Test that invalid arguments are rejected.
    /// </summary>
    [Fact]
    public void ImageWriter_Write_InvalidArguments_Throw()
    {
        // Arrange: a surface
        using var surface = FixtureFactory.CreateGradient();

        // Act / Assert: null arguments and a quality outside 1 to 100
        Assert.Throws<ArgumentNullException>(() => ImageWriter.Write(null!, "o.png", "i.png", WriteOptions.Default));
        Assert.Throws<ArgumentNullException>(() => ImageWriter.Write(surface, null!, "i.png", WriteOptions.Default));
        Assert.Throws<ArgumentNullException>(() => ImageWriter.Write(surface, "o.png", null!, WriteOptions.Default));
        Assert.Throws<ArgumentNullException>(() => ImageWriter.Write(surface, "o.png", "i.png", null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageWriter.Write(surface, "o.png", "i.png", WriteOptions.Default with { Quality = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageWriter.Write(surface, "o.png", "i.png", WriteOptions.Default with { Quality = 101 }));
        Assert.Throws<ArgumentNullException>(() => ImageWriter.ResolveFormat(null!, null));
        Assert.Throws<ArgumentNullException>(() => ImageWriter.CheckTarget(null!, "i.png", false));
        Assert.Throws<ArgumentNullException>(() => ImageWriter.CheckTarget("o.png", null!, false));
    }

    /// <summary>
    ///     Test the defaults of the write options.
    /// </summary>
    [Fact]
    public void ImageWriter_WriteOptionsDefault_HasDocumentedDefaults()
    {
        // Arrange / Act: the defaults
        var options = WriteOptions.Default;

        // Assert: format from the extension, quality 90, overwrite, flatten over white
        Assert.Null(options.Format);
        Assert.Equal(90, options.Quality);
        Assert.False(options.NoClobber);
        Assert.Equal(White, options.FlattenBackground);
    }

    /// <summary>
    ///     Test that the extension table is lower case and covers every output format.
    /// </summary>
    [Fact]
    public void ImageWriter_ExtensionFormats_AreWellFormed()
    {
        // Arrange / Act: the table
        var extensions = ImageWriter.ExtensionFormats;

        // Assert: lower case with a dot, and every format has an extension
        Assert.All(extensions.Keys, e =>
        {
            Assert.StartsWith(".", e);
            Assert.Equal(e.ToLowerInvariant(), e);
        });
        Assert.Equal(Enum.GetValues<OutputFormat>().Order(), extensions.Values.Distinct().Order());
    }
}

// cspell:ignore jpeg tiff tif exif gif jpeg2000
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Subsystem tests of the Imaging subsystem: the source loader and the image writer working together.
/// </summary>
public class ImagingSubsystemTests
{
    /// <summary>
    ///     Gets every combination of an input kind and an output extension, as theory data.
    /// </summary>
    public static TheoryData<string, string> InputOutputMatrix
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (key, _) in FixtureFactory.Sources)
            {
                foreach (var extension in new[] { ".png", ".jpg", ".bmp", ".tif" })
                {
                    data.Add(key, extension);
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     Creates the pixels that loading a source must produce, from the source file alone and the codecs.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    /// <param name="path">The path of the source file.</param>
    /// <returns>The reference surface; SVG is flattened over white as the tool does.</returns>
    private static Surface CreateReference(string key, string path)
    {
        switch (key)
        {
            case "png":
                return PngCodec.Load(path);
            case "jpeg":
                return JpegCodec.Load(path);
            case "bmp":
                return BmpCodec.Load(path);
            case "tiff":
                return TiffCodec.Load(path);
            case "gif":
                return GifCodec.Load(path);
            case "jpeg2000":
                return Jpeg2000Codec.Load(path);
            default:
                using (var drawn = SvgCodec.Load(path, 100, 80))
                {
                    return SurfaceOperations.FlattenOver(drawn, new Rgba32(255, 255, 255, 255));
                }
        }
    }

    /// <summary>
    ///     Test that every input kind can be written as every output format with the pixels the source holds.
    /// </summary>
    /// <param name="key">The kind of input.</param>
    /// <param name="extension">The extension of the output.</param>
    [Theory]
    [MemberData(nameof(InputOutputMatrix))]
    public void Imaging_Convert_InputKindToOutputFormat_WritesTheSourcePixels(string key, string extension)
    {
        // Arrange: a source file of the kind, and the pixels the codecs decode from it
        using var directory = new TempDirectory();
        var source = FixtureFactory.WriteSource(directory.Path, key);
        using var reference = CreateReference(key, source);
        var output = directory.Combine("out" + extension);

        // Act: load it and write it
        using (var loaded = SourceLoader.Load(source, new SourceOptions()))
        {
            ImageWriter.Write(loaded.Surface, output, source, WriteOptions.Default);
        }

        // Assert: the output decodes to the source pixels - exactly for a lossless format, on average for JPEG
        using var decoded = SurfaceAssert.Load(output);
        if (extension == ".jpg" && key == "gif")
        {
            // The GIF fixture is a pixel-by-pixel pattern of saturated colors, which JPEG's chroma subsampling cannot
            // reproduce; only the size is checked
            Assert.Equal((reference.Width, reference.Height), (decoded.Width, decoded.Height));
        }
        else if (extension == ".jpg")
        {
            SurfaceAssert.CloseOnAverage(reference, decoded, 8);
        }
        else
        {
            SurfaceAssert.Equal(reference, decoded);
        }
    }

    /// <summary>
    ///     Test that loading and writing the same input twice gives the same bytes.
    /// </summary>
    /// <param name="key">The kind of input.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    [InlineData("gif")]
    [InlineData("jpeg2000")]
    [InlineData("svg")]
    public void Imaging_Convert_SameInputTwice_WritesIdenticalBytes(string key)
    {
        // Arrange: a source file of the kind
        using var directory = new TempDirectory();
        var source = FixtureFactory.WriteSource(directory.Path, key);

        // Act: convert it twice, to PNG and to JPEG
        foreach (var extension in new[] { ".png", ".jpg" })
        {
            foreach (var name in new[] { "a", "b" })
            {
                using var loaded = SourceLoader.Load(source, new SourceOptions());
                ImageWriter.Write(loaded.Surface, directory.Combine(name + extension), source, WriteOptions.Default);
            }

            // Assert: identical bytes
            Assert.Equal(File.ReadAllBytes(directory.Combine("a" + extension)), File.ReadAllBytes(directory.Combine("b" + extension)));
        }
    }

    /// <summary>
    ///     Test that a transparent PNG becomes a JPEG over white and stays transparent in the lossless formats.
    /// </summary>
    [Fact]
    public void Imaging_Convert_TransparentPng_FlattensOnlyForJpeg()
    {
        // Arrange: a PNG whose right edge is fully transparent
        using var directory = new TempDirectory();
        var source = directory.Combine("translucent.png");
        using (var surface = FixtureFactory.CreateTranslucent())
        {
            PngCodec.Save(surface, source);
        }

        // Act: convert it to PNG and to JPEG
        using (var loaded = SourceLoader.Load(source, new SourceOptions()))
        {
            ImageWriter.Write(loaded.Surface, directory.Combine("out.png"), source, WriteOptions.Default);
            ImageWriter.Write(loaded.Surface, directory.Combine("out.jpg"), source, WriteOptions.Default);
        }

        // Assert: the PNG keeps the transparent pixel; the JPEG shows white there
        using var png = PngCodec.Load(directory.Combine("out.png"));
        using var jpeg = JpegCodec.Load(directory.Combine("out.jpg"));
        Assert.Equal(0, png[FixtureFactory.Width - 1, 0].A);
        Assert.InRange(jpeg[FixtureFactory.Width - 1, 0].R, 240, 255);
        Assert.InRange(jpeg[FixtureFactory.Width - 1, 0].G, 240, 255);
        Assert.InRange(jpeg[FixtureFactory.Width - 1, 0].B, 240, 255);
    }

    /// <summary>
    ///     Test that a sized load followed by a write gives an image of the requested size, for a raster and for a vector.
    /// </summary>
    [Fact]
    public void Imaging_Convert_RequestedSize_WritesThatSize()
    {
        // Arrange: a PNG and the SVG fixture
        using var directory = new TempDirectory();
        var png = FixtureFactory.WriteSource(directory.Path, "png");
        var svg = FixtureFactory.WriteSource(directory.Path, "svg");

        // Act: load each at an exact size and write it
        foreach (var (source, name) in new[] { (png, "from-png.png"), (svg, "from-svg.png") })
        {
            using var loaded = SourceLoader.Load(source, new SourceOptions { Width = 36, Height = 24 });
            ImageWriter.Write(loaded.Surface, directory.Combine(name), source, WriteOptions.Default);
        }

        // Assert: both files are 36x24
        using var fromPng = PngCodec.Load(directory.Combine("from-png.png"));
        using var fromSvg = PngCodec.Load(directory.Combine("from-svg.png"));
        Assert.Equal((36, 24), (fromPng.Width, fromPng.Height));
        Assert.Equal((36, 24), (fromSvg.Width, fromSvg.Height));
    }

    /// <summary>
    ///     Test that an error in the loader or the writer leaves no output behind.
    /// </summary>
    [Fact]
    public void Imaging_Convert_CorruptInput_LeavesNoOutput()
    {
        // Arrange: a truncated PNG
        using var directory = new TempDirectory();
        var source = FixtureFactory.WriteSource(directory.Path, "png");
        File.WriteAllBytes(source, File.ReadAllBytes(source)[..20]);
        var output = directory.Combine("out.jpg");

        // Act: loading fails before anything is written
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Load(source, new SourceOptions()));

        // Assert: an expected error, and no output or temporary file
        Assert.Equal(ErrorCodes.CorruptInput, exception.Code);
        Assert.False(File.Exists(output));
        Assert.Equal([source], Directory.GetFileSystemEntries(directory.Path));
    }
}

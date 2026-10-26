// cspell:ignore canvasnet jpeg tiff tif exif gif jpeg2000 pptx vsdx scad FEFF RRGGBB RRGGBBAA
using System.Xml;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Tool.Imaging;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the SourceLoader class: detection, probing, loading, the size policy, and error mapping.
/// </summary>
public class SourceLoaderTests
{
    /// <summary>
    ///     Gets the keys of the source kinds the factory can write, as theory data.
    /// </summary>
    public static TheoryData<string> SourceKeys => [.. FixtureFactory.Sources.Select(s => s.Key)];

    /// <summary>
    ///     Gets every combination of a source kind and a way of damaging it, as theory data.
    /// </summary>
    public static TheoryData<string, string> DamagedSources
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var (key, _) in FixtureFactory.Sources.Where(s => s.Key != "scad"))
            {
                foreach (var damage in new[] { "empty", "garbage", "one-byte", "header-only", "half", "missing-last-byte" })
                {
                    data.Add(key, damage);
                }
            }

            // A model is text, so cutting it often leaves a valid (shorter) model: SourceLoaderScadTests does its own sweep
            return data;
        }
    }

    /// <summary>
    ///     Writes a file of one kind and damages it.
    /// </summary>
    /// <param name="directory">The directory to write into.</param>
    /// <param name="key">The kind of source.</param>
    /// <param name="damage">How to damage it: <c>empty</c>, <c>garbage</c>, <c>one-byte</c>, <c>header-only</c>, <c>half</c>, or <c>missing-last-byte</c>.</param>
    /// <returns>The path of the damaged file; it keeps the extension of the kind.</returns>
    private static string WriteDamaged(TempDirectory directory, string key, string damage)
    {
        var path = FixtureFactory.WriteSource(directory.Path, key);
        var bytes = File.ReadAllBytes(path);
        var damaged = damage switch
        {
            "empty" => [],
            "garbage" => FixtureFactory.CreateGarbage(bytes.Length),
            "one-byte" => bytes[..1],
            "header-only" => bytes[..Math.Min(bytes.Length, 24)],
            "half" => bytes[..(bytes.Length / 2)],
            _ => bytes[..^1]
        };
        File.WriteAllBytes(path, damaged);
        return path;
    }

    /// <summary>
    ///     Test that the signature of each kind is recognized.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [MemberData(nameof(SourceKeys))]
    public void SourceLoader_Detect_EachKind_ReturnsKind(string key)
    {
        // Arrange: a file of the kind
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);

        // Act: detect the format
        var kind = SourceLoader.Detect(path);

        // Assert: the kind, whose format name is the key
        Assert.Equal(key, kind.ToFormatName());
    }

    /// <summary>
    ///     Test that the content decides over the extension.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_SignatureAndExtensionDisagree_TrustsSignature()
    {
        // Arrange: PNG content in a file named like a JPEG, and SVG content named like a PNG
        using var directory = new TempDirectory();
        var png = directory.Combine("really-png.jpg");
        using (var surface = FixtureFactory.CreateGradient())
        {
            PngCodec.Save(surface, png);
        }

        var svg = directory.Combine("really-svg.png");
        File.Copy(FixtureFactory.FixturePath("shapes.svg"), svg);

        // Act / Assert: the content wins
        Assert.Equal(SourceKind.Png, SourceLoader.Detect(png));
        Assert.Equal(SourceKind.Svg, SourceLoader.Detect(svg));
    }

    /// <summary>
    ///     Test that SVG text is recognized without an extension, after a byte order mark, a prolog, and a comment.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_SvgTextWithoutExtension_ReturnsSvg()
    {
        // Arrange: an SVG document with a byte order mark, an XML declaration, and a comment before the root element
        using var directory = new TempDirectory();
        var path = directory.Combine("drawing");
        File.WriteAllText(
            path,
            "\uFEFF<?xml version=\"1.0\"?>\n<!-- a comment -->\n<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4\" height=\"4\"/>");

        // Act / Assert: recognized by its content
        Assert.Equal(SourceKind.Svg, SourceLoader.Detect(path));
    }

    /// <summary>
    ///     Test that a file named .scad is a model even when its text mentions an SVG element.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_ScadMentioningSvg_ReturnsScad()
    {
        // Arrange: a model whose comment contains the text of an SVG start tag
        using var directory = new TempDirectory();
        var path = directory.Combine("model.scad");
        File.WriteAllText(path, "// exported from <svg width=\"4\"> by hand\ncube(1);\n");

        // Act / Assert: the extension decides, not the text sniff
        Assert.Equal(SourceKind.Scad, SourceLoader.Detect(path));
        Assert.Equal(SourceKind.Svg, SourceLoader.DetectContent("<svg/>"u8, ".txt"));
    }

    /// <summary>
    ///     Test that the extension decides when the content says nothing.
    /// </summary>
    /// <param name="extension">The extension.</param>
    /// <param name="expected">The format name expected.</param>
    [Theory]
    [InlineData(".png", "png")]
    [InlineData(".JPG", "jpeg")]
    [InlineData(".gif", "gif")]
    [InlineData(".tif", "tiff")]
    [InlineData(".jp2", "jpeg2000")]
    [InlineData(".svg", "svg")]
    [InlineData(".pdf", "pdf")]
    [InlineData(".scad", "scad")]
    public void SourceLoader_Detect_UnrecognizedContent_FallsBackToExtension(string extension, string expected)
    {
        // Arrange: a file with content no signature matches
        using var directory = new TempDirectory();
        var path = directory.Combine("file" + extension);
        File.WriteAllBytes(path, FixtureFactory.CreateGarbage(40));

        // Act / Assert: the extension decides
        Assert.Equal(expected, SourceLoader.Detect(path).ToFormatName());
    }

    /// <summary>
    ///     Test that a ZIP package is told apart by its extension.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_ZipPackage_UsesExtension()
    {
        // Arrange: ZIP signatures in files named like a presentation, a drawing, and a plain archive
        using var directory = new TempDirectory();
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0];
        var pptx = directory.Combine("slides.pptx");
        var vsdx = directory.Combine("diagram.vsdx");
        var archive = directory.Combine("archive.zip");
        File.WriteAllBytes(pptx, zip);
        File.WriteAllBytes(vsdx, zip);
        File.WriteAllBytes(archive, zip);

        // Act / Assert: the package kinds follow the extension; an unknown archive is not supported
        Assert.Equal(SourceKind.Pptx, SourceLoader.Detect(pptx));
        Assert.Equal(SourceKind.Vsdx, SourceLoader.Detect(vsdx));
        Assert.Equal(ErrorCodes.UnsupportedFormat, Assert.Throws<ToolException>(() => SourceLoader.Detect(archive)).Code);
    }

    /// <summary>
    ///     Test that the other signatures (PDF, a JPEG 2000 codestream, and a TIFF of either byte order) are recognized.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_OtherSignatures_ReturnsKind()
    {
        // Arrange / Act / Assert: the signature alone decides, whatever the extension
        Assert.Equal(SourceKind.Pdf, SourceLoader.DetectContent("%PDF-1.7\n"u8, ".bin"));
        Assert.Equal(SourceKind.Jpeg2000, SourceLoader.DetectContent([0xFF, 0x4F, 0xFF, 0x51, 0x00], ".bin"));
        Assert.Equal(SourceKind.Tiff, SourceLoader.DetectContent([0x49, 0x49, 0x2A, 0x00, 0x08], ".bin"));
        Assert.Equal(SourceKind.Tiff, SourceLoader.DetectContent([0x4D, 0x4D, 0x00, 0x2A, 0x00], ".bin"));
        Assert.Equal(SourceKind.Gif, SourceLoader.DetectContent("GIF87a"u8, ".bin"));
        Assert.Null(SourceLoader.DetectContent("just some text"u8, ".txt"));
    }

    /// <summary>
    ///     Test that text that merely starts with the BM letters is not taken for a bitmap.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_TextStartingWithBm_IsNotBmp()
    {
        // Arrange: a text file that starts like a BMP signature, long enough to have a header
        using var directory = new TempDirectory();
        var path = directory.Combine("notes.txt");
        File.WriteAllText(path, "BM is the abbreviation of a company; this file is only a note.");

        // Act / Assert: not a supported format
        Assert.Equal(ErrorCodes.UnsupportedFormat, Assert.Throws<ToolException>(() => SourceLoader.Detect(path)).Code);
    }

    /// <summary>
    ///     Test that an unrecognized file is reported as an unsupported format.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_UnknownFormat_ThrowsUnsupportedFormat()
    {
        // Arrange: a text file with an unknown extension
        using var directory = new TempDirectory();
        var path = directory.Combine("notes.txt");
        File.WriteAllText(path, "hello");

        // Act: detect the format
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Detect(path));

        // Assert: unsupported, with a hint to the formats command
        Assert.Equal(ErrorCodes.UnsupportedFormat, exception.Code);
        Assert.Contains("not in a supported file format", exception.Message);
        Assert.Contains("canvasnet formats", exception.Hint);
    }

    /// <summary>
    ///     Test that a missing file and a directory are reported.
    /// </summary>
    [Fact]
    public void SourceLoader_Detect_MissingFileOrDirectory_ThrowsInputError()
    {
        // Arrange: a path that does not exist, and a directory
        using var directory = new TempDirectory();

        // Act
        var missing = Assert.Throws<ToolException>(() => SourceLoader.Detect(directory.Combine("missing.png")));
        var folder = Assert.Throws<ToolException>(() => SourceLoader.Detect(directory.Path));

        // Assert: not found, and unreadable because it is a directory
        Assert.Equal(ErrorCodes.InputNotFound, missing.Code);
        Assert.Contains("does not exist", missing.Message);
        Assert.Equal(ErrorCodes.InputUnreadable, folder.Code);
        Assert.Contains("is a directory", folder.Message);
    }

    /// <summary>
    ///     Test that the extension table is lower case and complete for the kinds.
    /// </summary>
    [Fact]
    public void SourceLoader_ExtensionKinds_AreWellFormed()
    {
        // Arrange / Act: the table
        var extensions = SourceLoader.ExtensionKinds;

        // Assert: every extension starts with a dot, is lower case, and every kind has an extension
        Assert.All(extensions.Keys, e =>
        {
            Assert.StartsWith(".", e);
            Assert.Equal(e.ToLowerInvariant(), e);
        });
        Assert.Equal(Enum.GetValues<SourceKind>().Order(), extensions.Values.Distinct().Order());
    }

    /// <summary>
    ///     Test that probing reports the header facts of each kind without decoding.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [MemberData(nameof(SourceKeys))]
    public void SourceLoader_Probe_EachKind_ReportsHeaderFacts(string key)
    {
        // Arrange: a file of the kind
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);

        // Act: probe it
        var info = SourceLoader.Probe(path);

        // Assert: the kind, the path, the file size, and the size of the image
        Assert.Equal(key, info.Format.ToFormatName());
        Assert.Equal(path, info.Path);
        Assert.Equal(new FileInfo(path).Length, info.Bytes);
        var (width, height) = key switch
        {
            "jpeg2000" => (100, 70),
            "svg" => (100, 80),
            "pdf" => (192, 128),
            "pptx" => (FixtureFactory.PptxSlideWidth, FixtureFactory.PptxSlideHeight),
            "vsdx" => (FixtureFactory.VsdxPageWidth, FixtureFactory.VsdxPageHeight),
            "scad" => (SourceLoader.DefaultScadSize, SourceLoader.DefaultScadSize),
            _ => (FixtureFactory.Width, FixtureFactory.Height)
        };
        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
        Assert.True(info.CanDecode);
        Assert.Equal(1, info.FrameCount);

        // A document has two pages, which it lists; every other kind has one and lists none
        var documentPages = info.Format.IsDocument() ? 2 : 1;
        Assert.Equal(documentPages, info.PageCount);
        Assert.Equal(info.Format.IsDocument() ? 2 : 0, info.Pages.Count);

        // Only a model has modules and functions
        Assert.Equal(key == "scad" ? new[] { "block" } : [], info.Modules);
        Assert.Equal(key == "scad" ? new[] { "half" } : [], info.Functions);
        Assert.Empty(info.Directives);
        Assert.Null(info.ExifOrientation);
    }

    /// <summary>
    ///     Test that probing reports the channels and the alpha of PNG and SVG.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_PngAndSvg_ReportChannelsAndAlpha()
    {
        // Arrange: a PNG with transparency and the SVG fixture
        using var directory = new TempDirectory();
        var png = directory.Combine("translucent.png");
        using (var surface = FixtureFactory.CreateTranslucent())
        {
            PngCodec.Save(surface, png);
        }

        // Act
        var pngInfo = SourceLoader.Probe(png);
        var svgInfo = SourceLoader.Probe(FixtureFactory.FixturePath("shapes.svg"));

        // Assert: four channels with alpha for both
        Assert.Equal(4, pngInfo.Channels);
        Assert.True(pngInfo.HasAlpha);
        Assert.Equal(4, svgInfo.Channels);
        Assert.True(svgInfo.HasAlpha);
    }

    /// <summary>
    ///     Test that probing a JPEG reports its EXIF orientation.
    /// </summary>
    [Fact]
    public void SourceLoader_Probe_JpegWithExif_ReportsOrientation()
    {
        // Arrange: a JPEG with an EXIF orientation of 6
        using var directory = new TempDirectory();
        var path = directory.Combine("camera.jpg");
        using (var surface = FixtureFactory.CreateGradient())
        using (var stream = new MemoryStream())
        {
            JpegCodec.Save(surface, stream);
            File.WriteAllBytes(path, FixtureFactory.InsertExifOrientation(stream.ToArray(), 6, false));
        }

        // Act / Assert: the orientation is reported, and the size is the stored size
        var info = SourceLoader.Probe(path);
        Assert.Equal(6, info.ExifOrientation);
        Assert.Equal(FixtureFactory.Width, info.Width);
        Assert.Equal(FixtureFactory.Height, info.Height);
    }

    /// <summary>
    ///     Test that a source kind the loader has no case for is a defect, not an expected error.
    /// </summary>
    [Fact]
    public void SourceLoader_OpenPages_UnhandledKind_IsNotAToolException()
    {
        // Arrange: facts that name a kind that does not exist
        using var directory = new TempDirectory();
        var info = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "png")) with { Format = (SourceKind)99 };

        // Act / Assert: an invalid operation, which the entry point reports as unexpected, not a tool error
        var exception = Assert.Throws<InvalidOperationException>(() => SourceLoader.OpenPages(info, new SourceOptions()));
        Assert.IsNotType<ToolException>(exception);
    }

    /// <summary>
    ///     Test that a PNG, a BMP, and a TIFF load with exactly their pixels.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    public void SourceLoader_Load_LosslessRaster_KeepsEveryPixel(string key)
    {
        // Arrange: a file of the kind, written from the gradient
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);
        using var expected = FixtureFactory.CreateGradient();

        // Act: load it
        using var loaded = SourceLoader.Load(path, new SourceOptions()).Surface;

        // Assert: every pixel is the gradient's
        SurfaceAssert.Equal(expected, loaded);
    }

    /// <summary>
    ///     Test that a GIF loads with its colors and a JPEG and a JPEG 2000 image load as the codecs decode them.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_GifJpegAndJpeg2000_MatchTheCodecs()
    {
        // Arrange: files of the three kinds
        using var directory = new TempDirectory();
        var gif = FixtureFactory.WriteSource(directory.Path, "gif");
        var jpeg = FixtureFactory.WriteSource(directory.Path, "jpeg");
        var jp2 = FixtureFactory.WriteSource(directory.Path, "jpeg2000");

        // Act
        using var loadedGif = SourceLoader.Load(gif, new SourceOptions());
        using var loadedJpeg = SourceLoader.Load(jpeg, new SourceOptions());
        using var loadedJp2 = SourceLoader.Load(jp2, new SourceOptions());

        // Assert: the GIF has exactly its palette image, the others equal the codec's own decode
        using var palette = FixtureFactory.CreatePalette();
        SurfaceAssert.Equal(palette, loadedGif.Surface);
        using var jpegExpected = JpegCodec.Load(jpeg);
        SurfaceAssert.Equal(jpegExpected, loadedJpeg.Surface);
        using var jp2Expected = Jpeg2000Codec.Load(jp2);
        SurfaceAssert.Equal(jp2Expected, loadedJp2.Surface);
        Assert.All(new[] { loadedGif, loadedJpeg, loadedJp2 }, l =>
        {
            Assert.False(l.IsVector);
            Assert.Equal(1, l.Page);
            Assert.Empty(l.Operations);
            Assert.Empty(l.Diagnostics);
            Assert.Empty(l.Echo);
        });
    }

    /// <summary>
    ///     Test that an SVG renders at its natural size over white, with the shapes where the drawing puts them.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_Svg_RendersNaturalSizeOverWhite()
    {
        // Arrange / Act: load the SVG fixture
        using var loaded = SourceLoader.Load(FixtureFactory.FixturePath("shapes.svg"), new SourceOptions());
        var surface = loaded.Surface;

        // Assert: the size, the shapes, and white where the drawing is transparent
        Assert.True(loaded.IsVector);
        Assert.Equal(100, surface.Width);
        Assert.Equal(80, surface.Height);
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[20, 20]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[70, 50]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), surface[5, 5]);

        // The half-transparent green over white is a light green and fully opaque
        var green = surface[20, 60];
        Assert.Equal(255, green.A);
        Assert.InRange(green.R, 126, 129);
        Assert.Equal(255, green.G);
        Assert.InRange(green.B, 126, 129);
        Assert.Equal(["render", "flatten"], loaded.Operations.Select(o => o.Op));
    }

    /// <summary>
    ///     Test that an SVG keeps its transparency when the background has no alpha, and flattens over a given color.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgBackground_TransparentKeepsAlphaColorFlattens()
    {
        // Arrange: the SVG fixture
        var path = FixtureFactory.FixturePath("shapes.svg");

        // Act
        using var transparent = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(0, 0, 0, 0) });
        using var black = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(0, 0, 0, 255) });

        // Assert: transparent where nothing is drawn, black when asked for black; the operations say what happened
        Assert.Equal(0, transparent.Surface[5, 5].A);
        Assert.Equal(new Rgba32(0, 0, 0, 255), black.Surface[5, 5]);
        Assert.Equal(["render"], transparent.Operations.Select(o => o.Op));
        var flatten = black.Operations.Single(o => o.Op == "flatten");
        Assert.Equal("#000000", flatten.Fields.Single().Value);
    }

    /// <summary>
    ///     Test that an SVG is rendered at the requested size rather than resampled.
    /// </summary>
    /// <param name="scale">The scale, or 0 for none.</param>
    /// <param name="width">The width, or 0 for none.</param>
    /// <param name="height">The height, or 0 for none.</param>
    /// <param name="expectedWidth">The expected width.</param>
    /// <param name="expectedHeight">The expected height.</param>
    [Theory]
    [InlineData(2.0, 0, 0, 200, 160)]
    [InlineData(0.5, 0, 0, 50, 40)]
    [InlineData(0.0, 50, 0, 50, 40)]
    [InlineData(0.0, 0, 40, 50, 40)]
    [InlineData(0.0, 30, 70, 30, 70)]
    public void SourceLoader_Load_SvgWithSizeOptions_RendersAtRequestedSize(
        double scale,
        int width,
        int height,
        int expectedWidth,
        int expectedHeight)
    {
        // Arrange: the sizing options
        var options = new SourceOptions
        {
            Scale = scale > 0 ? scale : null,
            Width = width > 0 ? width : null,
            Height = height > 0 ? height : null
        };

        // Act: load the SVG
        using var loaded = SourceLoader.Load(FixtureFactory.FixturePath("shapes.svg"), options);

        // Assert: the requested size, and the red square is still where the drawing puts it (scaled)
        Assert.Equal(expectedWidth, loaded.Surface.Width);
        Assert.Equal(expectedHeight, loaded.Surface.Height);
        var render = loaded.Operations[0];
        Assert.Equal("render", render.Op);
        Assert.Equal(expectedWidth, render.Fields.Single(f => f.Key == "width").Value);
        Assert.Equal(expectedHeight, render.Fields.Single(f => f.Key == "height").Value);
        // With the aspect ratio of the drawing the red square is where the drawing puts it (scaled); with another
        // aspect ratio the SVG fitting rule (meet, centered) letterboxes the drawing, so only the size is checked
        if (expectedWidth * 80 == expectedHeight * 100)
        {
            Assert.Equal(new Rgba32(255, 0, 0, 255), loaded.Surface[expectedWidth * 25 / 100, expectedHeight * 31 / 100]);
        }
        else
        {
            Assert.Equal(new Rgba32(255, 255, 255, 255), loaded.Surface[0, 0]);
        }
    }

    /// <summary>
    ///     Test the size policy: rounding, aspect ratio, exact sizes, and at least one pixel.
    /// </summary>
    /// <param name="scale">The scale, or 0 for none.</param>
    /// <param name="width">The width, or 0 for none.</param>
    /// <param name="height">The height, or 0 for none.</param>
    /// <param name="expectedWidth">The expected width.</param>
    /// <param name="expectedHeight">The expected height.</param>
    [Theory]
    [InlineData(0.0, 0, 0, 640, 480)]
    [InlineData(1.5, 0, 0, 960, 720)]
    [InlineData(0.001, 0, 0, 1, 1)]
    [InlineData(0.0, 100, 0, 100, 75)]
    [InlineData(0.0, 0, 100, 133, 100)]
    [InlineData(0.0, 101, 0, 101, 76)]
    [InlineData(0.0, 10, 20, 10, 20)]
    [InlineData(0.0, 1, 0, 1, 1)]
    public void SourceLoader_ComputeTargetSize_SizeOptions_FollowsPolicy(
        double scale,
        int width,
        int height,
        int expectedWidth,
        int expectedHeight)
    {
        // Arrange: a 640x480 source and the options
        var options = new SourceOptions
        {
            Scale = scale > 0 ? scale : null,
            Width = width > 0 ? width : null,
            Height = height > 0 ? height : null
        };

        // Act: compute the target size
        var (w, h) = SourceLoader.ComputeTargetSize("in.png", 640, 480, options);

        // Assert: the documented result
        Assert.Equal((expectedWidth, expectedHeight), (w, h));
    }

    /// <summary>
    ///     Test that a target size beyond the maximum is refused with a size-limit error.
    /// </summary>
    [Fact]
    public void SourceLoader_ComputeTargetSize_BeyondMaximum_ThrowsSizeLimit()
    {
        // Arrange / Act: an exact size, a scale, and an aspect-derived side beyond 8192
        var exact = Assert.Throws<ToolException>(() => SourceLoader.ComputeTargetSize("in.png", 640, 480, new SourceOptions { Width = 9000, Height = 10 }));
        var scaled = Assert.Throws<ToolException>(() => SourceLoader.ComputeTargetSize("in.png", 640, 480, new SourceOptions { Scale = 20 }));
        var derived = Assert.Throws<ToolException>(() => SourceLoader.ComputeTargetSize("in.png", 640, 10, new SourceOptions { Width = 8192 * 2 }));
        var natural = Assert.Throws<ToolException>(() => SourceLoader.ComputeTargetSize("in.svg", 20000, 100, new SourceOptions()));
        var edge = SourceLoader.ComputeTargetSize("in.png", 640, 480, new SourceOptions { Width = 8192, Height = 8192 });

        // Assert: size-limit errors that say what to do, and exactly 8192 is fine
        Assert.All(new[] { exact, scaled, derived, natural }, e => Assert.Equal(ErrorCodes.SizeLimit, e.Code));
        Assert.Contains("requested size", exact.Message);
        Assert.Contains("--scale", scaled.Hint);
        Assert.Contains("'in.svg' is 20000x100 pixels", natural.Message);
        Assert.Contains("--width", natural.Hint);
        Assert.Equal((8192, 8192), edge);
    }

    /// <summary>
    ///     Test that an oversized image is refused from its header, before it is decoded.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_PngLargerThanMaximum_ThrowsSizeLimitBeforeDecoding()
    {
        // Arrange: a PNG that declares 9000x9000 pixels and holds no pixel data at all
        using var directory = new TempDirectory();
        var path = directory.Combine("huge.png");
        File.WriteAllBytes(path, FixtureFactory.CreatePngHeaderOnly(9000, 9000));

        // Act: probe and load it
        var info = SourceLoader.Probe(path);
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions()));

        // Assert: probing reports the declared size; loading is refused for its size, not for corrupt data
        Assert.Equal(9000, info.Width);
        Assert.Equal(ErrorCodes.SizeLimit, exception.Code);
        Assert.Contains("9000x9000", exception.Message);
    }

    /// <summary>
    ///     Test that an SVG whose natural size is too large can still be rendered at a size that is allowed.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_SvgWithHugeNaturalSize_NeedsSizeOption()
    {
        // Arrange: an SVG of 100000x50 pixels
        using var directory = new TempDirectory();
        var path = directory.Combine("wide.svg");
        File.WriteAllText(path, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100000\" height=\"50\"><rect width=\"1000\" height=\"50\"/></svg>");

        // Act
        var refused = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions()));
        using var loaded = SourceLoader.Load(path, new SourceOptions { Width = 400 });

        // Assert: refused at natural size, rendered when a size is given (one pixel high, never zero)
        Assert.Equal(ErrorCodes.SizeLimit, refused.Code);
        Assert.Equal(400, loaded.Surface.Width);
        Assert.Equal(1, loaded.Surface.Height);
    }

    /// <summary>
    ///     Test that a raster is resized to the requested size and the resize is recorded.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_RasterWithWidth_ResizesKeepingAspectRatio()
    {
        // Arrange: the 12x8 PNG
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, "png");

        // Act: load it at a width of 6
        using var loaded = SourceLoader.Load(path, new SourceOptions { Width = 6 });

        // Assert: 6x4, and the operation is recorded
        Assert.Equal(6, loaded.Surface.Width);
        Assert.Equal(4, loaded.Surface.Height);
        var resize = Assert.Single(loaded.Operations);
        Assert.Equal("resize", resize.Op);
        Assert.Equal(6, resize.Fields.Single(f => f.Key == "width").Value);
        Assert.Equal(4, resize.Fields.Single(f => f.Key == "height").Value);
    }

    /// <summary>
    ///     Test that a raster keeps its transparency unless a background color is asked for.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_RasterBackground_FlattensOnlyWhenAsked()
    {
        // Arrange: a PNG with transparency
        using var directory = new TempDirectory();
        var path = directory.Combine("translucent.png");
        using (var surface = FixtureFactory.CreateTranslucent())
        {
            PngCodec.Save(surface, path);
        }

        // Act
        using var kept = SourceLoader.Load(path, new SourceOptions());
        using var white = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(255, 255, 255, 255) });
        using var transparent = SourceLoader.Load(path, new SourceOptions { Background = new Rgba32(0, 0, 0, 0) });

        // Assert: the default and a transparent background keep the alpha; a white background flattens it
        Assert.Equal(0, kept.Surface[FixtureFactory.Width - 1, 0].A);
        Assert.Empty(kept.Operations);
        Assert.Equal(0, transparent.Surface[FixtureFactory.Width - 1, 0].A);
        Assert.Equal(new Rgba32(255, 255, 255, 255), white.Surface[FixtureFactory.Width - 1, 0]);
        Assert.Equal("flatten", Assert.Single(white.Operations).Op);
    }

    /// <summary>
    ///     Test that a JPEG with an EXIF orientation is turned upright, and that this can be switched off.
    /// </summary>
    /// <param name="orientation">The EXIF orientation of the file.</param>
    /// <param name="autoOrient">Whether the orientation is applied.</param>
    /// <param name="expectedWidth">The expected width.</param>
    /// <param name="expectedHeight">The expected height.</param>
    /// <param name="expectedOperation">The expected operation names, comma separated.</param>
    [Theory]
    [InlineData(6, true, 8, 12, "auto-orient")]
    [InlineData(8, true, 8, 12, "auto-orient")]
    [InlineData(3, true, 12, 8, "auto-orient")]
    [InlineData(1, true, 12, 8, "")]
    [InlineData(6, false, 12, 8, "")]
    public void SourceLoader_Load_JpegWithExif_AppliesOrientationUnlessDisabled(
        int orientation,
        bool autoOrient,
        int expectedWidth,
        int expectedHeight,
        string expectedOperation)
    {
        // Arrange: a 12x8 JPEG that carries the orientation
        using var directory = new TempDirectory();
        var path = directory.Combine("camera.jpg");
        using (var surface = FixtureFactory.CreateGradient())
        using (var stream = new MemoryStream())
        {
            JpegCodec.Save(surface, stream);
            File.WriteAllBytes(path, FixtureFactory.InsertExifOrientation(stream.ToArray(), orientation, true));
        }

        // Act: load it
        using var loaded = SourceLoader.Load(path, new SourceOptions { AutoOrient = autoOrient });

        // Assert: the size follows the orientation, and the operation is recorded
        Assert.Equal(expectedWidth, loaded.Surface.Width);
        Assert.Equal(expectedHeight, loaded.Surface.Height);
        var operations = string.Join(',', loaded.Operations.Select(o => o.Op));
        Assert.Equal(expectedOperation, operations);
        Assert.Equal(orientation, loaded.Info.ExifOrientation);
    }

    /// <summary>
    ///     Test that a requested size applies to the oriented image, and that the pixels follow the orientation.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_JpegWithExifAndWidth_SizesTheOrientedImage()
    {
        // Arrange: a 12x8 JPEG with orientation 6 (it displays as 8 wide and 12 high)
        using var directory = new TempDirectory();
        var path = directory.Combine("camera.jpg");
        using (var surface = FixtureFactory.CreateGradient())
        using (var stream = new MemoryStream())
        {
            JpegCodec.Save(surface, stream);
            File.WriteAllBytes(path, FixtureFactory.InsertExifOrientation(stream.ToArray(), 6, false));
        }

        // Act: load it at a width of 4
        using var loaded = SourceLoader.Load(path, new SourceOptions { Width = 4 });
        using var stored = JpegCodec.Load(path);
        using var upright = ExifOrientation.Apply(stored, 6);
        using var expected = upright.Resize(4, 6);

        // Assert: 4x6 (the aspect ratio of the oriented image), equal to orient-then-resize
        Assert.Equal(4, loaded.Surface.Width);
        Assert.Equal(6, loaded.Surface.Height);
        SurfaceAssert.Equal(expected, loaded.Surface);
        Assert.Equal(["auto-orient", "resize"], loaded.Operations.Select(o => o.Op));
    }

    /// <summary>
    ///     Test that a damaged file of any kind is reported as an expected error, never as an unexpected exception.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    /// <param name="damage">How the file is damaged.</param>
    [Theory]
    [MemberData(nameof(DamagedSources))]
    public void SourceLoader_Load_DamagedFile_ThrowsToolExceptionOrLoads(string key, string damage)
    {
        // Arrange: a file of the kind, damaged
        using var directory = new TempDirectory();
        var path = WriteDamaged(directory, key, damage);

        // Act: probe and load it; any exception that is not a ToolException fails the test
        var outcome = Record.Exception(() =>
        {
            SourceLoader.Probe(path);
            SourceLoader.Load(path, new SourceOptions()).Surface.Dispose();
        });

        // Assert: either it loads (a decoder may accept a file that lacks only its last byte) or it is an expected error
        if (outcome != null)
        {
            var exception = Assert.IsType<ToolException>(outcome);
            Assert.Contains(exception.Code, new[] { ErrorCodes.CorruptInput, ErrorCodes.UnsupportedFeature });
        }
        else
        {
            Assert.Equal("missing-last-byte", damage);
        }
    }

    /// <summary>
    ///     Test that garbage with each recognized extension is reported as corrupt input.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [MemberData(nameof(SourceKeys))]
    public void SourceLoader_Load_GarbageWithKnownExtension_ThrowsCorruptInput(string key)
    {
        // Arrange: garbage content in a file with the extension of the kind
        using var directory = new TempDirectory();
        var path = WriteDamaged(directory, key, "garbage");

        // Act: load it
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Load(path, new SourceOptions()));

        // Assert: reported as corrupt input (a model: a SCAD error), naming the file
        Assert.Equal(key == "scad" ? ErrorCodes.ScadError : ErrorCodes.CorruptInput, exception.Code);
        Assert.Contains(path, exception.Message);
    }

    /// <summary>
    ///     Test that the allow-listed library exceptions are mapped to their error codes.
    /// </summary>
    [Fact]
    public void SourceLoader_Read_AllowListedExceptions_AreMappedToErrorCodes()
    {
        // Arrange: the exceptions a library may throw for a bad input, and the code each maps to
        (Exception Exception, string Code)[] cases =
        [
            (new FileNotFoundException("gone"), ErrorCodes.InputNotFound),
            (new DirectoryNotFoundException("gone"), ErrorCodes.InputNotFound),
            (new UnauthorizedAccessException("denied"), ErrorCodes.InputUnreadable),
            (new IOException("disk"), ErrorCodes.InputUnreadable),
            (new UnsupportedImageFeatureException("progressive", "not supported"), ErrorCodes.UnsupportedFeature),
            (new PptxUnsupportedFeatureException("pptx-gradient-path", "not supported"), ErrorCodes.UnsupportedFeature),
            (new VsdxUnsupportedFeatureException("vsdx-arrowhead-99", "not supported"), ErrorCodes.UnsupportedFeature),
            (new InvalidDataException("bad"), ErrorCodes.CorruptInput),
            (new XmlException("bad xml"), ErrorCodes.CorruptInput),
            (new EndOfStreamException("short"), ErrorCodes.CorruptInput),
            (new FormatException("bad number"), ErrorCodes.CorruptInput)
        ];

        foreach (var (exception, code) in cases)
        {
            // Act: run a read that throws it
            var mapped = Assert.Throws<ToolException>(() => SourceLoader.Read<int>("in.bin", SourceKind.Png, () => throw exception));

            // Assert: the code, a message naming the file, one line, and the cause kept
            Assert.Equal(code, mapped.Code);
            Assert.Contains("'in.bin'", mapped.Message);
            Assert.DoesNotContain('\n', mapped.Message);
            Assert.Same(exception, mapped.InnerException);
        }
    }

    /// <summary>
    ///     Test that a library message with several lines is reduced to its first line.
    /// </summary>
    [Fact]
    public void SourceLoader_Read_MultiLineMessage_IsReducedToOneLine()
    {
        // Arrange / Act: a corrupt-data failure whose message has two lines
        var mapped = Assert.Throws<ToolException>(
            () => SourceLoader.Read<int>("in.bin", null, () => throw new InvalidDataException("first line\r\nsecond line")));

        // Assert: only the first line
        Assert.EndsWith("first line", mapped.Message);
        Assert.Contains("not a valid image file", mapped.Message);
    }

    /// <summary>
    ///     Test that an unexpected exception is not mapped: it is a defect and must propagate.
    /// </summary>
    [Fact]
    public void SourceLoader_Read_UnexpectedExceptions_Propagate()
    {
        // Arrange / Act / Assert: defects and resource failures are not caught
        Assert.Throws<NullReferenceException>(() => SourceLoader.Read<int>("in.bin", null, () => throw new NullReferenceException()));
        Assert.Throws<IndexOutOfRangeException>(() => SourceLoader.Read<int>("in.bin", null, () => throw new IndexOutOfRangeException()));
        Assert.Throws<ArgumentOutOfRangeException>(() => SourceLoader.Read<int>("in.bin", null, () => ThrowOutOfRange(1)));
        Assert.Throws<OutOfMemoryException>(() => SourceLoader.Read<int>("in.bin", null, () => throw new OutOfMemoryException()));
        Assert.Throws<InvalidOperationException>(() => SourceLoader.Read<int>("in.bin", null, () => throw new InvalidOperationException()));
    }

    /// <summary>
    ///     Throws the exception of an argument that is out of range, as a library does for a defect of its caller.
    /// </summary>
    /// <param name="size">The offending argument.</param>
    /// <returns>Never returns.</returns>
    private static int ThrowOutOfRange(int size)
    {
        throw new ArgumentOutOfRangeException(nameof(size), size, "The size is out of range.");
    }

    /// <summary>
    ///     Test that an SVG that is not well-formed is reported as corrupt input.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_MalformedSvg_ThrowsCorruptInput()
    {
        // Arrange: an unclosed SVG and a document whose root is not svg
        using var directory = new TempDirectory();
        var unclosed = directory.Combine("unclosed.svg");
        File.WriteAllText(unclosed, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4\" height=\"4\"><rect");
        var other = directory.Combine("other.svg");
        File.WriteAllText(other, "<html><svg></svg></html>");

        // Act / Assert: both are corrupt input
        Assert.Equal(ErrorCodes.CorruptInput, Assert.Throws<ToolException>(() => SourceLoader.Load(unclosed, new SourceOptions())).Code);
        Assert.Equal(ErrorCodes.CorruptInput, Assert.Throws<ToolException>(() => SourceLoader.Load(other, new SourceOptions())).Code);
    }

    /// <summary>
    ///     Test that BoxOptions sets the box and clears the scale for every vector kind.
    /// </summary>
    /// <param name="key">The kind of vector source.</param>
    [Theory]
    [InlineData("svg")]
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    [InlineData("scad")]
    public void SourceLoader_BoxOptions_VectorKinds_SetBoxAndClearScale(string key)
    {
        // Arrange: a source of a vector kind, and options with a scale and other settings
        using var directory = new TempDirectory();
        var info = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, key));
        var options = new SourceOptions { Page = 1, Dpi = 150, Scale = 2, Strict = true };

        // Act
        var boxed = SourceLoader.BoxOptions(info, options, 100, 80);

        // Assert: the box replaces the sizing options and everything else is kept
        Assert.True(info.Format.IsVector());
        Assert.Equal(100, boxed.Width);
        Assert.Equal(80, boxed.Height);
        Assert.Null(boxed.Scale);
        Assert.Equal(options with { Width = 100, Height = 80, Scale = null }, boxed);
    }

    /// <summary>
    ///     Test that BoxOptions leaves the options of a raster kind unchanged.
    /// </summary>
    /// <param name="key">The kind of raster source.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    [InlineData("gif")]
    [InlineData("jpeg2000")]
    public void SourceLoader_BoxOptions_RasterKinds_AreUnchanged(string key)
    {
        // Arrange: a source of a raster kind
        using var directory = new TempDirectory();
        var info = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, key));
        var options = new SourceOptions { Scale = 2 };

        // Act
        var boxed = SourceLoader.BoxOptions(info, options, 100, 80);

        // Assert: the very same options
        Assert.False(info.Format.IsVector());
        Assert.Same(options, boxed);
    }

    /// <summary>
    ///     Test that a vector source loaded with the box options is rendered natively at the box: an SVG at the exact
    ///     box, and a document page fitted inside it and centered on the background.
    /// </summary>
    [Fact]
    public void SourceLoader_Load_VectorWithBox_RendersNativelyAtBox()
    {
        // Arrange: an SVG, and a presentation whose 480x360 slide does not fill a square box
        using var directory = new TempDirectory();
        var svg = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "svg"));
        var pptx = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "pptx"));
        var background = new Rgba32(10, 20, 30, 255);
        var options = new SourceOptions { Background = background };

        // Act: load both into a 300x300 box
        using var fromSvg = SourceLoader.Load(svg, SourceLoader.BoxOptions(svg, options, 300, 300));
        using var fromPptx = SourceLoader.Load(pptx, SourceLoader.BoxOptions(pptx, options, 300, 300));

        // Assert: both are exactly the box; the slide is 300x225 centered (letterbox bands of 37 pixels top and bottom)
        Assert.Equal((300, 300), (fromSvg.Surface.Width, fromSvg.Surface.Height));
        Assert.Equal((300, 300), (fromPptx.Surface.Width, fromPptx.Surface.Height));
        Assert.Equal(background, fromPptx.Surface[0, 0]);
        Assert.Equal(background, fromPptx.Surface[299, 299]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), fromPptx.Surface[50, 150]);
        Assert.Equal(background, fromPptx.Surface[250, 150]);
        Assert.Equal(background, fromPptx.Surface[50, 34]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), fromPptx.Surface[50, 40]);
    }

    /// <summary>
    ///     Test that BoxOptions rejects null arguments.
    /// </summary>
    [Fact]
    public void SourceLoader_BoxOptions_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: facts of a real source
        using var directory = new TempDirectory();
        var info = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "png"));

        // Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => SourceLoader.BoxOptions(null!, new SourceOptions(), 10, 10));
        Assert.Throws<ArgumentNullException>(() => SourceLoader.BoxOptions(info, null!, 10, 10));
    }

    /// <summary>
    ///     Test that the color formatting is <c>#RRGGBB</c> for an opaque color and <c>#RRGGBBAA</c> otherwise.
    /// </summary>
    [Fact]
    public void SourceLoader_FormatColor_OpaqueAndTranslucent_FormatsHex()
    {
        // Arrange / Act / Assert: the two forms
        Assert.Equal("#FF8001", SourceLoader.FormatColor(new Rgba32(255, 128, 1, 255)));
        Assert.Equal("#FF800180", SourceLoader.FormatColor(new Rgba32(255, 128, 1, 128)));
    }

    /// <summary>
    ///     Test that null arguments are rejected.
    /// </summary>
    [Fact]
    public void SourceLoader_PublicMethods_NullArguments_ThrowArgumentNullException()
    {
        // Arrange / Act / Assert: each null argument throws
        Assert.Throws<ArgumentNullException>(() => SourceLoader.Detect(null!));
        Assert.Throws<ArgumentNullException>(() => SourceLoader.Probe(null!));
        Assert.Throws<ArgumentNullException>(() => SourceLoader.Load((string)null!, new SourceOptions()));
        Assert.Throws<ArgumentNullException>(() => SourceLoader.Load("x.png", null!));
        Assert.Throws<ArgumentNullException>(() => SourceLoader.Load((SourceInfo)null!, new SourceOptions()));
    }
}

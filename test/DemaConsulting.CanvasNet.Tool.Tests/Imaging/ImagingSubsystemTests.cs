// cspell:ignore jpeg tiff tif exif gif jpeg2000 scad
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Pdf;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Tool.Imaging;
using DemaConsulting.CanvasNet.Vsdx;

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
            case "pdf":
                using (var document = PdfDocument.Open(path))
                {
                    return document.Render(0, 192, 128);
                }

            case "pptx":
                using (var document = PptxDocument.Open(path))
                {
                    return document.Render(0, FixtureFactory.PptxSlideWidth, FixtureFactory.PptxSlideHeight);
                }

            case "vsdx":
                using (var document = VsdxDocument.Open(path))
                {
                    return document.Render(0, FixtureFactory.VsdxPageWidth, FixtureFactory.VsdxPageHeight);
                }

            case "scad":
                return ScadDocument.Open(path).Render(
                    SourceLoader.DefaultScadSize,
                    SourceLoader.DefaultScadSize,
                    new ScadRenderOptions { Projection = CameraProjection.Orthographic });

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
    ///     Gets every combination of a named view of a model and an output extension, as theory data.
    /// </summary>
    public static TheoryData<string, string> ScadViewOutputMatrix
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var view in SourceLoader.ScadViews)
            {
                foreach (var extension in new[] { ".png", ".jpg", ".bmp", ".tif" })
                {
                    data.Add(view.Name, extension);
                }
            }

            return data;
        }
    }

    /// <summary>
    ///     Test that every named view of a model can be written as every output format with the pixels the library draws for that view.
    /// </summary>
    /// <param name="view">The name of the view.</param>
    /// <param name="extension">The extension of the output.</param>
    [Theory]
    [MemberData(nameof(ScadViewOutputMatrix))]
    public void Imaging_Convert_ScadViewToOutputFormat_WritesTheLibraryPixels(string view, string extension)
    {
        // Arrange: the asymmetric model, and what the library draws for the view
        using var directory = new TempDirectory();
        var source = FixtureFactory.WriteText(directory.Path, "model.scad", FixtureFactory.AsymmetricModel);
        var named = SourceLoader.ScadViews.Single(v => v.Name == view);
        using var reference = ScadDocument.Open(source).Render(
            48,
            48,
            new ScadRenderOptions
            {
                Projection = CameraProjection.Orthographic,
                ViewAzimuthDegrees = named.Azimuth,
                ViewElevationDegrees = named.Elevation
            },
            TestContext.Current.CancellationToken);
        var output = directory.Combine("out" + extension);

        // Act: load the view and write it
        using (var loaded = SourceLoader.Load(source, new SourceOptions { Width = 48, Height = 48, Scad = new ScadViewOptions { View = view } }))
        {
            ImageWriter.Write(loaded.Surface, output, source, WriteOptions.Default);
        }

        // Assert: exactly the library's pixels for a lossless format, close on average for JPEG
        using var decoded = SurfaceAssert.Load(output);
        if (extension == ".jpg")
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
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    [InlineData("scad")]
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
        // Arrange: a PNG, the SVG fixture, and the SCAD model
        using var directory = new TempDirectory();
        var png = FixtureFactory.WriteSource(directory.Path, "png");
        var svg = FixtureFactory.WriteSource(directory.Path, "svg");
        var scad = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act: load each at an exact size and write it
        foreach (var (source, name) in new[] { (png, "from-png.png"), (svg, "from-svg.png"), (scad, "from-scad.png") })
        {
            using var loaded = SourceLoader.Load(source, new SourceOptions { Width = 36, Height = 24 });
            ImageWriter.Write(loaded.Surface, directory.Combine(name), source, WriteOptions.Default);
        }

        // Assert: all files are 36x24
        using var fromPng = PngCodec.Load(directory.Combine("from-png.png"));
        using var fromSvg = PngCodec.Load(directory.Combine("from-svg.png"));
        using var fromScad = PngCodec.Load(directory.Combine("from-scad.png"));
        Assert.Equal((36, 24), (fromPng.Width, fromPng.Height));
        Assert.Equal((36, 24), (fromSvg.Width, fromSvg.Height));
        Assert.Equal((36, 24), (fromScad.Width, fromScad.Height));
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

    /// <summary>
    ///     Test that a page that does not exist is an expected error and leaves no output behind.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    [Theory]
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    public void Imaging_Convert_PageOutOfRange_LeavesNoOutput(string key)
    {
        // Arrange: a two-page document
        using var directory = new TempDirectory();
        var source = FixtureFactory.WriteSource(directory.Path, key);

        // Act: loading page 3 fails before anything is written
        var exception = Assert.Throws<ToolException>(() => SourceLoader.Load(source, new SourceOptions { Page = 3 }));

        // Assert: an expected error, and nothing but the source in the directory
        Assert.Equal(ErrorCodes.PageOutOfRange, exception.Code);
        Assert.Equal([source], Directory.GetFileSystemEntries(directory.Path));
    }

    /// <summary>
    ///     Test that every page of a document, loaded from one opened source and written, holds the pixels the library
    ///     renders for that page.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    [Theory]
    [InlineData("pdf")]
    [InlineData("pptx")]
    [InlineData("vsdx")]
    public void Imaging_Convert_DocumentPages_WritesEachPageWithItsPixels(string key)
    {
        // Arrange: a two-page document and its facts
        using var directory = new TempDirectory();
        var source = FixtureFactory.WriteSource(directory.Path, key);
        var info = SourceLoader.Probe(source);

        // Act: write every page from one opened source
        using (var pages = SourceLoader.OpenPages(info, new SourceOptions()))
        {
            for (var page = 1; page <= info.PageCount; page++)
            {
                using var loaded = pages.ReadPage(page);
                ImageWriter.Write(loaded.Surface, directory.Combine($"page-{page}.png"), source, WriteOptions.Default);
            }
        }

        // Assert: each file has the size of its page, and the pixels the library renders at that size
        for (var page = 1; page <= info.PageCount; page++)
        {
            using var written = PngCodec.Load(directory.Combine($"page-{page}.png"));
            var size = info.Pages[page - 1];
            Assert.Equal((size.Width, size.Height), (written.Width, written.Height));
            using var expected = RenderPageWithLibrary(key, source, page - 1, size.Width, size.Height);
            SurfaceAssert.Equal(expected, written);
        }
    }

    /// <summary>
    ///     Renders a page with the library of its kind, as the reference for what the tool must produce.
    /// </summary>
    /// <param name="key">The kind of document.</param>
    /// <param name="path">The path of the document.</param>
    /// <param name="index">The 0-based page index.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The rendered page.</returns>
    private static Surface RenderPageWithLibrary(string key, string path, int index, int width, int height)
    {
        switch (key)
        {
            case "pdf":
                using (var document = PdfDocument.Open(path))
                {
                    return document.Render(index, width, height);
                }

            case "pptx":
                using (var document = PptxDocument.Open(path))
                {
                    return document.Render(index, width, height);
                }

            default:
                using (var document = VsdxDocument.Open(path))
                {
                    return document.Render(index, width, height);
                }
        }
    }

    /// <summary>
    ///     Test that a source loaded from every kind goes through the pipeline and the writer as one decode and one
    ///     encode: the written pixels are the transformed surface, which equals the operations applied by hand, and the
    ///     pipeline leaves the loaded surface untouched.
    /// </summary>
    /// <param name="key">The kind of source.</param>
    [Theory]
    [InlineData("png")]
    [InlineData("svg")]
    [InlineData("pdf")]
    [InlineData("scad")]
    public void Imaging_Pipeline_LoadedSource_TransformedAndWrittenOnce(string key)
    {
        // Arrange: the source, loaded at its natural size
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteSource(directory.Path, key);
        using var loaded = SourceLoader.Load(path, new SourceOptions());
        using var before = loaded.Surface.Crop(0, 0, loaded.Surface.Width, loaded.Surface.Height);
        IReadOnlyList<IImageOperation> operations =
        [
            new CropOperation(new GeometrySpec(new Dimension(0, false), new Dimension(0, false), new Dimension(50, true), new Dimension(100, true))),
            new RotateOperation(90),
            new PadOperation(new PadSpec(1, 1, 1, 1), new Rgba32(0, 0, 255, 255))
        ];

        // Act: apply the pipeline, write the result, and apply it again by hand to compare
        using var transformed = ImagePipeline.Apply(loaded.Surface, operations, new Rgba32(0, 0, 0, 0), out var applied);
        var result = ImageWriter.Write(transformed, directory.Combine("out.png"), path, WriteOptions.Default);
        using var cropped = loaded.Surface.Crop(0, 0, (int)Math.Round(loaded.Surface.Width / 2.0, MidpointRounding.AwayFromZero), loaded.Surface.Height);
        using var expected = ImagePipeline.Apply(cropped, operations.Skip(1).ToList(), new Rgba32(0, 0, 0, 0), out _);

        // Assert: three reported steps, the written file is the transformed surface, and the loaded surface is as it was
        Assert.Equal(["crop", "rotate", "pad"], applied.Select(a => a.Op));
        Assert.Equal((transformed.Width, transformed.Height), (result.Width, result.Height));
        using var decoded = PngCodec.Load(directory.Combine("out.png"));
        SurfaceAssert.Equal(transformed, decoded);
        SurfaceAssert.Equal(expected, transformed);
        SurfaceAssert.Equal(before, loaded.Surface);
        Assert.Equal(new Rgba32(0, 0, 255, 255), decoded[0, 0]);
    }

    /// <summary>
    ///     Compares the SVG fixture with a raster copy of it that differs in one pixel, and writes the three pictures.
    /// </summary>
    /// <param name="directory">The directory for the files.</param>
    /// <param name="tag">A name that makes the files of one run different from those of another.</param>
    /// <returns>The result, and the paths of the three pictures.</returns>
    private static (ComparisonResult Result, string[] Pictures) CompareFixtureAndWrite(TempDirectory directory, string tag)
    {
        var svg = FixtureFactory.FixturePath("shapes.svg");
        var raster = directory.Combine("raster.png");
        if (!File.Exists(raster))
        {
            using var rendered = SourceLoader.Load(svg, new SourceOptions());
            rendered.Surface[50, 40] = new Rgba32(0, 0, 0, 255);
            PngCodec.Save(rendered.Surface, raster);
        }

        // The raster is loaded at its own size; the SVG is rendered into a box of that size, sharply
        var rasterInfo = SourceLoader.Probe(raster);
        using var reference = SourceLoader.Load(rasterInfo, new SourceOptions());
        var svgInfo = SourceLoader.Probe(svg);
        var options = SourceLoader.BoxOptions(svgInfo, new SourceOptions(), reference.Surface.Width, reference.Surface.Height);
        using var drawing = SourceLoader.Load(svgInfo, options);

        var result = ImageComparison.Compare(reference.Surface, drawing.Surface, ImageComparison.DefaultTolerance, 2, 2);
        string[] pictures = [directory.Combine($"diff-{tag}.png"), directory.Combine($"sbs-{tag}.png"), directory.Combine($"overlay-{tag}.png")];
        using (var diff = ImageComparison.CreateDiff(reference.Surface, drawing.Surface, DiffStyle.Heat, ImageComparison.DefaultGain))
        {
            ImageWriter.Write(diff, pictures[0], raster, WriteOptions.Default);
            using var sheet = ImageComparison.CreateSideBySide(reference.Surface, drawing.Surface, diff);
            ImageWriter.Write(sheet, pictures[1], raster, WriteOptions.Default);
        }

        using (var overlay = ImageComparison.CreateOverlay(reference.Surface, drawing.Surface, ImageComparison.DefaultOverlayWeight))
        {
            ImageWriter.Write(overlay, pictures[2], raster, WriteOptions.Default);
        }

        return (result, pictures);
    }

    /// <summary>
    ///     Test that sources loaded by the loader are compared, the differences are drawn, and the writer writes the pictures.
    /// </summary>
    [Fact]
    public void Imaging_Compare_LoadedSourcesAreComparedAndVisualized()
    {
        // Arrange / Act: the SVG fixture against its raster, which has one black pixel at 50,40
        using var directory = new TempDirectory();
        var (result, pictures) = CompareFixtureAndWrite(directory, "one");

        // Assert: exactly one pixel differs, and it is in the grid cell that holds it
        Assert.Equal((100, 80), (result.Width, result.Height));
        Assert.False(result.Metrics.Identical);
        Assert.Equal(1, result.Metrics.DifferingPixels);
        var cell = Assert.Single(result.WorstCells(3));
        Assert.Equal((1, 1), (cell.Column, cell.Row));

        // Assert: the pictures were written with the sizes of the design, and the difference is a single bright pixel
        using var diff = PngCodec.Load(pictures[0]);
        using var sheet = PngCodec.Load(pictures[1]);
        using var overlay = PngCodec.Load(pictures[2]);
        Assert.Equal((100, 80), (diff.Width, diff.Height));
        Assert.Equal((316, 80), (sheet.Width, sheet.Height));
        Assert.Equal((100, 80), (overlay.Width, overlay.Height));
        Assert.NotEqual(new Rgba32(0, 0, 0, 255), diff[50, 40]);
        Assert.Equal(new Rgba32(0, 0, 0, 255), diff[0, 0]);
    }

    /// <summary>
    ///     Test that a comparison done twice writes the same picture bytes.
    /// </summary>
    [Fact]
    public void Imaging_Compare_SameInputsTwice_WriteIdenticalBytes()
    {
        // Arrange / Act: the same comparison, twice
        using var directory = new TempDirectory();
        var (firstResult, first) = CompareFixtureAndWrite(directory, "first");
        var (secondResult, second) = CompareFixtureAndWrite(directory, "second");

        // Assert: equal results and equal bytes of every picture
        Assert.Equal(firstResult.Metrics, secondResult.Metrics);
        Assert.Equal(firstResult.Cells, secondResult.Cells);
        for (var i = 0; i < first.Length; i++)
        {
            Assert.Equal(File.ReadAllBytes(first[i]), File.ReadAllBytes(second[i]));
        }
    }

    /// <summary>
    ///     Test that sources of several kinds are loaded into cells, composed onto one sheet, and written.
    /// </summary>
    [Fact]
    public void Imaging_Montage_LoadedSourcesAreComposedAndWritten()
    {
        // Arrange: an SVG, a page of a PDF, and a PNG, in cells of 64x64 with labels
        using var directory = new TempDirectory();
        var paths = new[] { "svg", "pdf", "png" }.Select(key => FixtureFactory.WriteSource(directory.Path, key)).ToList();
        var options = new SheetOptions(2, 64, 64, 4, 4, new Rgba32(255, 255, 255, 255), true, FitMode.Contain, ResamplingMode.Auto);

        // Act: each source is opened, loaded into its cell, added to the sheet, and disposed in turn
        using var sheet = new ContactSheet(paths.Count, options);
        foreach (var path in paths)
        {
            var info = SourceLoader.Probe(path);
            using var source = SourceLoader.OpenPages(info, SourceLoader.BoxOptions(info, new SourceOptions(), 64, 64));
            using var loaded = source.ReadPage(1);
            sheet.Add(loaded.Surface, Path.GetFileName(path));
        }

        var output = directory.Combine("sheet.png");
        using (var surface = sheet.Detach())
        {
            ImageWriter.Write(surface, output, paths[0], WriteOptions.Default);
        }

        // Assert: the layout (2 columns, 2 rows of 64 + 22, spacing and padding 4), and a cell with pixels for every source
        Assert.Equal((2, 2, 140, 184), (sheet.Layout.Columns, sheet.Layout.Rows, sheet.Layout.Width, sheet.Layout.Height));
        using var decoded = PngCodec.Load(output);
        Assert.Equal((140, 184), (decoded.Width, decoded.Height));
        for (var index = 0; index < paths.Count; index++)
        {
            var (x, y) = sheet.Layout.CellOrigin(index);
            var drawn = false;
            for (var row = y; row < y + 64 && !drawn; row++)
            {
                drawn = decoded.GetRowSpan(row).Slice(x, 64).ToArray().Any(pixel => pixel != new Rgba32(255, 255, 255, 255));
            }

            Assert.True(drawn, $"Cell {index} is empty");
        }
    }
}

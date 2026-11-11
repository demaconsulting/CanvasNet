// cspell:ignore jpeg tiff tif exif gif jpeg2000 scad deskew deskewed
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Pdf;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.Render3D.SceneRendering;
using DemaConsulting.CanvasNet.Scad;
using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.VegaLite;
using DemaConsulting.CanvasNet.Viewer.Imaging;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Imaging;

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

            case "mermaid":
                return MermaidDocument.Open(path).Render(new MermaidRenderOptions { Background = new Rgba32(255, 255, 255, 255) }, TestContext.Current.CancellationToken);

            case "vega-lite":
                var chart = VegaLiteDocument.Open(path);
                var size = chart.Measure();
                return chart.Render(size.Width, size.Height);

            default:
                using (var drawn = SvgCodec.Load(path, 100, 80))
                {
                    return drawn.FlattenOver(new Rgba32(255, 255, 255, 255));
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
        var exception = Assert.Throws<ViewerException>(() => SourceLoader.Load(source, new SourceOptions()));

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
        var exception = Assert.Throws<ViewerException>(() => SourceLoader.Load(source, new SourceOptions { Page = 3 }));

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

        var result = reference.Surface.Compare(drawing.Surface, ImageComparison.DefaultTolerance, 2, 2);
        string[] pictures = [directory.Combine($"diff-{tag}.png"), directory.Combine($"sbs-{tag}.png"), directory.Combine($"overlay-{tag}.png")];
        using (var diff = reference.Surface.CreateDiff(drawing.Surface, DiffStyle.Heat, ImageComparison.DefaultGain))
        {
            ImageWriter.Write(diff, pictures[0], raster, WriteOptions.Default);
            using var sheet = reference.Surface.CreateSideBySide(drawing.Surface, diff);
            ImageWriter.Write(sheet, pictures[1], raster, WriteOptions.Default);
        }

        using (var overlay = reference.Surface.CreateOverlay(drawing.Surface, ImageComparison.DefaultOverlayWeight))
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
        var options = new SheetOptions { Columns = 2, CellWidth = 64, CellHeight = 64, Spacing = 4, Padding = 4, Labels = true };

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

    /// <summary>
    ///     Test that the render operation takes a probed document through the loader, the pipeline, and the writer, one
    ///     file for every page, and that the pages carry the rotated pixels.
    /// </summary>
    [Fact]
    public void Imaging_Render_DocumentPages_AreLoadedTransformedAndWritten()
    {
        // Arrange: the two-page PDF, to be rotated a quarter turn and written into a folder
        using var directory = new TempDirectory();
        var pdf = FixtureFactory.WriteSource(directory.Path, "pdf");
        var info = SourceLoader.Probe(pdf);
        var folder = directory.Combine("pages") + Path.DirectorySeparatorChar;
        var request = new RenderRequest(folder) { AllPages = true, Operations = [new RotateOperation(90)] };

        // Act
        var result = ImageRenderer.Execute(info, request);

        // Assert: both pages are written at the swapped size, and the step is reported
        Assert.Equal([1, 2], result.Pages.Select(p => p.Page));
        foreach (var page in result.Pages)
        {
            using var decoded = PngCodec.Load(page.Result.Path);
            Assert.Equal((page.Result.Width, page.Result.Height), (decoded.Width, decoded.Height));
            Assert.Contains(page.Operations, o => o.Op == "rotate");
        }
    }

    /// <summary>
    ///     Test that the inspect operation loads a source, runs the pipeline, and reports the pixel under a point and the
    ///     dominant colors of the same pixels.
    /// </summary>
    [Fact]
    public void Imaging_Inspect_LoadedSource_ReportsColorsAndPixel()
    {
        // Arrange: a PNG of one red color
        using var directory = new TempDirectory();
        var path = directory.Combine("red.png");
        using (var surface = new Surface(12, 8))
        {
            surface.Clear(new Rgba32(255, 0, 0, 255));
            PngCodec.Save(surface, path);
        }

        var info = SourceLoader.Probe(path);

        // Act
        var colors = ImageInspector.Execute(info, InspectRequest.ForColors(4));
        var pixel = ImageInspector.Execute(info, InspectRequest.ForPixel(new PixelPoint(3, 2)));

        // Assert: the one color covers the image, and the sample is that color
        Assert.Equal((12, 8), (colors.Width, colors.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), Assert.Single(colors.Colors!.Colors).Color);
        Assert.NotNull(pixel.Pixel);
        Assert.Null(pixel.Colors);
    }

    /// <summary>
    ///     Test that the compare operation loads two sources of different kinds at one size, measures them, and writes
    ///     the picture that was asked for.
    /// </summary>
    [Fact]
    public void Imaging_Compare_ComparerLoadsAndMeasuresTwoKinds_AndWritesThePicture()
    {
        // Arrange: the SVG and the PNG fixture, compared at the size of the PNG, with a difference picture
        using var directory = new TempDirectory();
        var svg = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "svg"));
        var png = SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "png"));
        var diff = directory.Combine("diff.png");
        var request = new CompareRequest(new CompareInput("a", png), new CompareInput("b", svg))
        {
            Resize = ResizeTarget.ToInputA,
            DiffPath = diff
        };

        // Act
        var result = ImageComparer.Execute(request);

        // Assert: one size, a measure, and the picture that was asked for
        Assert.Equal((result.A.Width, result.A.Height), (result.B.Width, result.B.Height));
        Assert.NotNull(result.Comparison);
        var picture = Assert.Single(result.Pictures);
        Assert.Equal(diff, picture.Result.Path);
        Assert.True(File.Exists(diff));
    }

    /// <summary>
    ///     Test that the flatten operation draws the linked diagram and model of a drawing through the loader, keeps the
    ///     raster link, and writes a copy whose pictures are PNG data URIs.
    /// </summary>
    [Fact]
    public void Imaging_Flatten_ComposedSvg_ReplacesOnlyCanvasNetImages()
    {
        // Arrange: a drawing that links a Mermaid diagram, an OpenSCAD model, and a PNG
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), directory.Combine("flow.mmd"));
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        File.Copy(FixtureFactory.WriteSource(directory.Path, "png"), directory.Combine("logo.png"));
        var input = FixtureFactory.WriteText(
            directory.Path,
            "dashboard.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'>"
            + "<image href='flow.mmd' width='100' height='50'/><image href='part.scad' x='100' width='50' height='50'/>"
            + "<image href='logo.png' y='50' width='20' height='20'/></svg>");
        var output = directory.Combine("flat.svg");

        // Act
        var result = SvgFlattener.Execute(SourceLoader.Probe(input), new FlattenRequest(output));

        // Assert: two pictures replaced, the raster link kept, and the copy is an SVG file the loader reads
        Assert.Equal(["flow.mmd", "part.scad"], result.Replaced.Select(r => r.Href));
        Assert.Equal(["mermaid", "scad"], result.Replaced.Select(r => r.Format));
        var text = File.ReadAllText(output);
        Assert.Contains("href='logo.png'", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split("data:image/png;base64,").Length - 1);
        var info = SourceLoader.Probe(output);
        Assert.Equal((SourceKind.Svg, 200, 100), (info.Format, info.Width, info.Height));
    }

    /// <summary>
    ///     Test that the montage operation opens sources of several kinds one at a time and writes one sheet.
    /// </summary>
    [Fact]
    public void Imaging_Montage_BuilderComposesSeveralKinds_AndWritesOneSheet()
    {
        // Arrange: an SVG, a PDF (its first page), and a PNG
        using var directory = new TempDirectory();
        var infos = new[] { "svg", "pdf", "png" }
            .Select(key => SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, key)))
            .ToList();
        var sheet = new SheetOptions { Columns = 2, CellWidth = 64, CellHeight = 64, Spacing = 4, Padding = 4, Labels = true };
        var output = directory.Combine("sheet.png");

        // Act
        var result = MontageBuilder.Execute(
            infos, new MontageRequest(output) { Sheet = sheet });

        // Assert: the layout of the sheet and a file of that size
        Assert.Equal((3, 2, 2), (result.Layout.Count, result.Layout.Columns, result.Layout.Rows));
        using var decoded = PngCodec.Load(output);
        Assert.Equal((result.Layout.Width, result.Layout.Height), (decoded.Width, decoded.Height));
    }

    /// <summary>
    ///     Writes a JPEG of a tilted sketch whose EXIF orientation says it must be turned a quarter turn, runs the photo
    ///     pipeline over it as the render command does, and writes the result as a PNG.
    /// </summary>
    /// <param name="directory">The directory for the files.</param>
    /// <param name="tag">A name that makes the files of one run different from those of another.</param>
    /// <returns>The loaded size, what the pipeline did, the transformed pixels, and the path of the PNG.</returns>
    private static (int Width, int Height, IReadOnlyList<AppliedOperation> Applied, Surface Result, string Path) RunPhotoPipeline(TempDirectory directory, string tag)
    {
        // The stored picture is the upright sketch turned a quarter turn counter-clockwise; orientation 6 turns it back
        var jpeg = directory.Combine("napkin.jpg");
        if (!File.Exists(jpeg))
        {
            using var upright = FixtureFactory.SketchPhoto(400, 300, 4.0);
            using var stored = ImagePipeline.Apply(upright, [new RotateOperation(270)], new Rgba32(0, 0, 0, 0), out _);
            using var stream = new MemoryStream();
            JpegCodec.Save(stored, stream);
            File.WriteAllBytes(jpeg, FixtureFactory.InsertExifOrientation(stream.ToArray(), 6, true));
        }

        using var loaded = SourceLoader.Load(jpeg, new SourceOptions());
        IReadOnlyList<IImageOperation> operations =
        [
            new AutoLevelsOperation(Surface.DefaultAutoLevelsClipPercent),
            new DeskewOperation(),
            new TrimOperation(ImagePipeline.DefaultTrimTolerance)
        ];
        var transformed = ImagePipeline.Apply(loaded.Surface, operations, new Rgba32(0, 0, 0, 0), out var applied, null);
        var output = directory.Combine($"sketch-{tag}.png");
        ImageWriter.Write(transformed, output, jpeg, WriteOptions.Default);
        return (loaded.Surface.Width, loaded.Surface.Height, applied, transformed, output);
    }

    /// <summary>
    ///     Test that a photo is loaded upright, its contrast stretched, straightened, trimmed to its content, and written.
    /// </summary>
    [Fact]
    public void Imaging_Photo_LoadedSourceAdjustedDeskewedTrimmedAndWritten()
    {
        // Arrange / Act
        using var directory = new TempDirectory();
        var (width, height, applied, transformed, output) = RunPhotoPipeline(directory, "one");
        using var result = transformed;

        // Assert: the EXIF orientation made the 400x300 sketch upright before the pipeline ran
        Assert.Equal((400, 300), (width, height));
        Assert.Equal(["auto-levels", "deskew", "trim"], applied.Select(a => a.Op));

        // The contrast was stretched, the tilt of 4 degrees found and undone, and the white border trimmed away
        Assert.Contains(applied[0].Fields, f => f.Key == "applied" && f.Value is true);
        var skew = (double)applied[1].Fields.Single(f => f.Key == "skewDegrees").Value;
        Assert.InRange(skew, 3.6, 4.4);
        Assert.Contains(applied[1].Fields, f => f.Key == "applied" && f.Value is true);
        Assert.Contains(applied[2].Fields, f => f.Key == "trimmed" && f.Value is true);
        var deskewedHeight = (int)applied[1].Fields.Single(f => f.Key == "height").Value;
        Assert.True(result.Height < deskewedHeight);

        // The written PNG is the transformed surface, and the straightened lines are level
        using var decoded = PngCodec.Load(output);
        SurfaceAssert.Equal(result, decoded);
        Assert.InRange(decoded.EstimateSkew().Degrees, -0.4, 0.4);
    }

    /// <summary>
    ///     Test that the photo pipeline gives the same bytes every time.
    /// </summary>
    [Fact]
    public void Imaging_Photo_SameInputsTwice_WriteIdenticalBytes()
    {
        // Arrange / Act
        using var directory = new TempDirectory();
        var (_, _, firstApplied, first, firstPath) = RunPhotoPipeline(directory, "first");
        var (_, _, secondApplied, second, secondPath) = RunPhotoPipeline(directory, "second");
        first.Dispose();
        second.Dispose();

        // Assert: the same reports and the same bytes
        Assert.Equal(firstApplied.Select(a => a.Op), secondApplied.Select(a => a.Op));
        Assert.Equal(
            firstApplied.SelectMany(a => a.Fields).Select(f => $"{f.Key}={f.Value}"),
            secondApplied.SelectMany(a => a.Fields).Select(f => $"{f.Key}={f.Value}"));
        Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
    }

    /// <summary>
    ///     Test that a comparison request built from its two inputs alone carries every default of the compare command.
    /// </summary>
    [Fact]
    public void Imaging_RequestDefaults_CompareRequestFromInputsAlone_CarriesCommandDefaults()
    {
        // Arrange: two inputs of one file, with no other member given
        using var directory = new TempDirectory();
        var path = directory.Combine("a.png");
        using (var surface = new Surface(4, 4))
        {
            PngCodec.Save(surface, path);
        }

        var info = SourceLoader.Probe(path);

        // Act
        var request = new CompareRequest(new CompareInput("a", info), new CompareInput("b", info));

        // Assert: the required members are kept, and everything else is the command default
        Assert.Equal(("a", info, 1), (request.A.Label, request.A.Info, request.A.Page));
        Assert.Equal(new SourceOptions(), request.A.Source);
        Assert.Null(request.Resize);
        Assert.Equal((FitMode.Contain, ResamplingMode.Auto), (request.Fit, request.Filter));
        Assert.Equal(new Rgba32(255, 255, 255, 255), request.Background);
        Assert.Equal(ImageComparison.DefaultTolerance, request.Tolerance);
        Assert.Equal((ImageComparison.DefaultGridColumns, ImageComparison.DefaultGridRows), (request.GridColumns, request.GridRows));
        Assert.Empty(request.Thresholds);
        Assert.Equal((null, null, null), (request.DiffPath, request.SideBySidePath, request.OverlayPath));
        Assert.Equal((DiffStyle.Heat, ImageComparison.DefaultGain, ImageComparison.DefaultOverlayWeight), (request.DiffStyle, request.Gain, request.OverlayWeight));
        Assert.Equal(WriteOptions.Default, request.Write);
    }

    /// <summary>
    ///     Test that the render, inspect, and montage requests, the sheet options, and the write options built from their
    ///     required members alone carry the defaults of the commands.
    /// </summary>
    [Fact]
    public void Imaging_RequestDefaults_OptionalMembers_CarryCommandDefaults()
    {
        // Arrange / Act
        var render = new RenderRequest("out.png");
        var colors = InspectRequest.ForColors();
        var pixel = InspectRequest.ForPixel(new PixelPoint(3, 2), 1);
        var montage = new MontageRequest("sheet.png");
        var sheet = new SheetOptions();
        var write = new WriteOptions();

        // Assert
        Assert.Equal("out.png", render.Output);
        Assert.False(render.AllPages);
        Assert.Equal(new SourceOptions(), render.Source);
        Assert.Equal(WriteOptions.Default, render.Write);
        Assert.Empty(render.Operations);

        Assert.Equal(InspectKind.Colors, colors.Kind);
        Assert.Equal(ImageInspector.DefaultColors, colors.Count);
        Assert.Null(colors.Point);
        Assert.Empty(colors.Operations);
        Assert.Equal((InspectKind.Pixel, new PixelPoint(3, 2), 1), (pixel.Kind, pixel.Point, pixel.Radius));
        Assert.Equal(new SourceOptions(), pixel.Source);

        Assert.Equal("sheet.png", montage.Output);
        Assert.Equal((null, false, null, null, false, true), (montage.Page, montage.AllPages, montage.Dpi, montage.Scad, montage.Strict, montage.AutoOrient));
        Assert.Equal(sheet, montage.Sheet);
        Assert.Equal(WriteOptions.Default, montage.Write);

        Assert.Equal(
            (0, ContactSheet.DefaultCellSize, ContactSheet.DefaultCellSize, ContactSheet.DefaultSpacing, ContactSheet.DefaultPadding),
            (sheet.Columns, sheet.CellWidth, sheet.CellHeight, sheet.Spacing, sheet.Padding));
        Assert.Equal((new Rgba32(255, 255, 255, 255), false, FitMode.Contain, ResamplingMode.Auto), (sheet.Background, sheet.Labels, sheet.Fit, sheet.Filter));

        Assert.Equal(WriteOptions.Default, write);
        Assert.Equal((null, ImageWriter.DefaultQuality, false, new Rgba32(255, 255, 255, 255)), (write.Format, write.Quality, write.NoClobber, write.FlattenBackground));
    }

    /// <summary>
    ///     Test that the published default values are the ones the commands document: 8 colors, a 256 pixel cell, and a
    ///     spacing and padding of 8.
    /// </summary>
    [Fact]
    public void Imaging_RequestDefaults_PublishedConstants_AreTheCommandDefaults()
    {
        // Arrange / Act / Assert
        Assert.Equal(8, ImageInspector.DefaultColors);
        Assert.Equal(256, ContactSheet.DefaultCellSize);
        Assert.Equal(8, ContactSheet.DefaultSpacing);
        Assert.Equal(8, ContactSheet.DefaultPadding);
    }

    /// <summary>
    ///     Test that the optional members of a request are init properties: they can be set in an initializer and copied
    ///     with <see langword="with"/>, without touching the required members.
    /// </summary>
    [Fact]
    public void Imaging_RequestDefaults_InitProperties_CanBeSetAndCopied()
    {
        // Arrange
        var request = new MontageRequest("sheet.png") { AllPages = true, Sheet = new SheetOptions { Labels = true, Columns = 3 } };

        // Act
        var copy = request with { Strict = true };

        // Assert
        Assert.True(copy.AllPages);
        Assert.True(copy.Strict);
        Assert.True(copy.Sheet.Labels);
        Assert.Equal(3, copy.Sheet.Columns);
        Assert.Equal("sheet.png", copy.Output);
        Assert.False(request.Strict);
    }

    /// <summary>
    ///     Test that the inspect factories give the same requests as the constructor with the members set, and that the
    ///     operation defaults are the command defaults.
    /// </summary>
    [Fact]
    public void Imaging_RequestDefaults_OperationDefaults_AreTheCommandDefaults()
    {
        // Arrange / Act
        var levels = new AutoLevelsOperation();
        var resize = new ResizeOperation(10, 20);
        var fit = new FitOperation(10, 20);

        // Assert
        Assert.Equal(Surface.DefaultAutoLevelsClipPercent, levels.ClipPercent);
        Assert.Equal(ResamplingMode.Auto, resize.Filter);
        Assert.False(fit.AllowUpscale);
        Assert.Equal(ResamplingMode.Auto, fit.Filter);
        Assert.Equal(new Dimension(20, false), Dimension.Pixels(20));
        Assert.Equal(new Dimension(5, true), Dimension.Percent(5));
        Assert.Equal(InspectRequest.ForColors(4), new InspectRequest(InspectKind.Colors) { Count = 4 });
    }

    /// <summary>
    ///     Test that a note's origin takes part in value equality, compared entry by entry, and is empty by default.
    /// </summary>
    [Fact]
    public void Imaging_Diagnostics_Origin_DefaultsToEmptyAndIsComparedByEntries()
    {
        // Arrange
        var plain = new ImageDiagnostic("svg", "warning", "code", "message", 2);
        var withOrigin = plain with { Origin = ["a.svg", "b.scad"] };

        // Act / Assert
        Assert.Empty(plain.Origin);
        Assert.Equal(withOrigin, plain with { Origin = new List<string> { "a.svg", "b.scad" } });
        Assert.Equal(withOrigin.GetHashCode(), (plain with { Origin = new List<string> { "a.svg", "b.scad" } }).GetHashCode());
        Assert.NotEqual(plain, withOrigin);
        Assert.NotEqual(withOrigin, plain with { Origin = ["a.svg"] });
        Assert.NotEqual(plain, plain with { Page = 3 });
    }
}

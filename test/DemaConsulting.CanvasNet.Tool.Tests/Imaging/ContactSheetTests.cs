// cspell:ignore ellipsis
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Imaging;

/// <summary>
///     Unit tests for the ContactSheet class: layout, composition, labels, and limits.
/// </summary>
public class ContactSheetTests
{
    /// <summary>
    ///     The background used by most tests.
    /// </summary>
    private static readonly Rgba32 Background = new(10, 20, 30, 255);

    /// <summary>
    ///     The color of the images that are added.
    /// </summary>
    private static readonly Rgba32 Ink = new(200, 100, 50, 255);

    /// <summary>
    ///     Creates sheet options with the values the tests usually need.
    /// </summary>
    /// <param name="columns">The columns; 0 for automatic.</param>
    /// <param name="cellWidth">The cell width.</param>
    /// <param name="cellHeight">The cell height.</param>
    /// <param name="spacing">The spacing.</param>
    /// <param name="padding">The padding.</param>
    /// <param name="labels">Whether the sheet has labels.</param>
    /// <param name="stretch">Whether images are stretched instead of contained.</param>
    /// <param name="background">The background; <see langword="null"/> for the default.</param>
    /// <returns>The options.</returns>
    private static SheetOptions Options(
        int columns = 0,
        int cellWidth = 20,
        int cellHeight = 20,
        int spacing = 2,
        int padding = 4,
        bool labels = false,
        bool stretch = false,
        Rgba32? background = null)
    {
        return new SheetOptions(
            columns,
            cellWidth,
            cellHeight,
            spacing,
            padding,
            background ?? Background,
            labels,
            stretch ? FitMode.Stretch : FitMode.Contain,
            ResamplingMode.Nearest);
    }

    /// <summary>
    ///     Creates a surface of one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The surface.</returns>
    private static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        surface.Clear(color);
        return surface;
    }

    /// <summary>
    ///     Finds the pixels in a rectangle that differ from a color.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color to compare with.</param>
    /// <returns>The pixels that are not <paramref name="color"/>.</returns>
    private static List<Rgba32> PixelsOtherThan(Surface surface, int left, int top, int width, int height, Rgba32 color)
    {
        var found = new List<Rgba32>();
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                if (surface[x, y] != color)
                {
                    found.Add(surface[x, y]);
                }
            }
        }

        return found;
    }

    /// <summary>
    ///     Test that rows and the sheet size follow the layout formulas for a given number of columns.
    /// </summary>
    [Fact]
    public void ContactSheet_CalculateLayout_ColumnsAndCount_ComputesRowsAndSheetSize()
    {
        // Arrange: five cells of 100x80 in two columns, with spacing 10 and padding 5
        var options = Options(columns: 2, cellWidth: 100, cellHeight: 80, spacing: 10, padding: 5);

        // Act
        var layout = ContactSheet.CalculateLayout(5, options);

        // Assert: three rows; width 2*5 + 2*100 + 10 and height 2*5 + 3*80 + 2*10
        Assert.Equal((5, 2, 3, 100, 80, 0, 10, 5), (layout.Count, layout.Columns, layout.Rows, layout.CellWidth, layout.CellHeight, layout.LabelHeight, layout.Spacing, layout.Padding));
        Assert.Equal((220, 270), (layout.Width, layout.Height));
    }

    /// <summary>
    ///     Test that the automatic number of columns is the ceiling of the square root of the count.
    /// </summary>
    /// <param name="count">The number of images.</param>
    /// <param name="expected">The expected number of columns.</param>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(10, 4)]
    [InlineData(17, 5)]
    [InlineData(4225, 64)]
    public void ContactSheet_CalculateLayout_AutomaticColumns_IsCeilingOfSquareRoot(int count, int expected)
    {
        // Arrange / Act: no columns given
        var layout = ContactSheet.CalculateLayout(count, Options(columns: 0));

        // Assert
        Assert.Equal(expected, layout.Columns);
        Assert.Equal((count + expected - 1) / expected, layout.Rows);
    }

    /// <summary>
    ///     Test that labels add a 22-pixel strip to every row.
    /// </summary>
    [Fact]
    public void ContactSheet_CalculateLayout_WithLabels_AddsLabelStripPerRow()
    {
        // Arrange: four cells in two columns, with and without labels
        var plain = ContactSheet.CalculateLayout(4, Options(columns: 2, cellHeight: 30, spacing: 6, padding: 3));
        var labeled = ContactSheet.CalculateLayout(4, Options(columns: 2, cellHeight: 30, spacing: 6, padding: 3, labels: true));

        // Assert: two rows of 22 pixels more; the width does not change
        Assert.Equal(0, plain.LabelHeight);
        Assert.Equal(22, labeled.LabelHeight);
        Assert.Equal(plain.Width, labeled.Width);
        Assert.Equal(plain.Height + (2 * 22), labeled.Height);
        Assert.Equal((2 * 3) + (2 * 30) + 6, plain.Height);
    }

    /// <summary>
    ///     Test that a sheet larger than the maximum on a side is refused with the size-limit code.
    /// </summary>
    [Fact]
    public void ContactSheet_CalculateLayout_SheetTooLarge_ThrowsSizeLimit()
    {
        // Arrange: 40 rows of 256 pixels in one column is 10240 pixels high
        var tall = Options(columns: 1, cellWidth: 256, cellHeight: 256);
        var wide = Options(columns: 33, cellWidth: 256, cellHeight: 256);

        // Act
        var height = Assert.Throws<ToolException>(() => ContactSheet.CalculateLayout(40, tall));
        var width = Assert.Throws<ToolException>(() => ContactSheet.CalculateLayout(33, wide));

        // Assert: size-limit with the size and a hint
        Assert.Equal(ErrorCodes.SizeLimit, height.Code);
        Assert.Contains("8192", height.Message);
        Assert.Contains("--cell", height.Hint);
        Assert.Equal(ErrorCodes.SizeLimit, width.Code);

        // The largest sheet that fits is accepted: 32 columns of 256 pixels with no spacing or padding
        var fits = ContactSheet.CalculateLayout(32, Options(columns: 32, cellWidth: 256, cellHeight: 256, spacing: 0, padding: 0));
        Assert.Equal(8192, fits.Width);
    }

    /// <summary>
    ///     Test that cells are placed in row-major order using the padding, spacing, and label strip.
    /// </summary>
    [Fact]
    public void ContactSheet_CellOrigin_RowMajor_UsesPaddingAndSpacing()
    {
        // Arrange: five cells of 100x80 in two columns with labels, spacing 10, padding 5
        var layout = ContactSheet.CalculateLayout(5, Options(columns: 2, cellWidth: 100, cellHeight: 80, spacing: 10, padding: 5, labels: true));

        // Act / Assert: the second column starts at 5 + 100 + 10; each row is 80 + 22 + 10 below the previous
        Assert.Equal((5, 5), layout.CellOrigin(0));
        Assert.Equal((115, 5), layout.CellOrigin(1));
        Assert.Equal((5, 117), layout.CellOrigin(2));
        Assert.Equal((115, 117), layout.CellOrigin(3));
        Assert.Equal((5, 229), layout.CellOrigin(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.CellOrigin(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.CellOrigin(5));
    }

    /// <summary>
    ///     Test that a contained image keeps its aspect ratio, fills the limiting side, and is centered on the background.
    /// </summary>
    [Fact]
    public void ContactSheet_Add_Contain_KeepsAspectAndCentersOnBackground()
    {
        // Arrange: a 40x20 image for a 20x20 cell in a sheet of one cell with padding 4
        using var sheet = new ContactSheet(1, Options(columns: 1, padding: 4));
        using var image = Solid(40, 20, Ink);

        // Act
        var origin = sheet.Add(image, null);
        using var pixels = sheet.Detach();

        // Assert: the cell is at (4, 4); the image became 20x10 in rows 9 to 18; everything else is the background
        Assert.Equal((4, 4), origin);
        Assert.Equal((28, 28), (pixels.Width, pixels.Height));
        Assert.Equal(Ink, pixels[4, 9]);
        Assert.Equal(Ink, pixels[23, 18]);
        Assert.Equal(Background, pixels[4, 8]);
        Assert.Equal(Background, pixels[4, 19]);
        Assert.Equal(Background, pixels[0, 0]);
        Assert.Equal(Background, pixels[27, 27]);
        Assert.Equal(20 * 10, PixelsOtherThan(pixels, 0, 0, 28, 28, Background).Count);
    }

    /// <summary>
    ///     Test that a stretched image fills the whole cell.
    /// </summary>
    [Fact]
    public void ContactSheet_Add_Stretch_FillsTheWholeCell()
    {
        // Arrange: the same 40x20 image, stretched
        using var sheet = new ContactSheet(1, Options(columns: 1, padding: 4, stretch: true));
        using var image = Solid(40, 20, Ink);

        // Act
        sheet.Add(image, null);
        using var pixels = sheet.Detach();

        // Assert: all 400 pixels of the cell are the image's color, and the padding is the background
        Assert.Equal(Ink, pixels[4, 4]);
        Assert.Equal(Ink, pixels[23, 23]);
        Assert.Equal(Background, pixels[3, 3]);
        Assert.Equal(Background, pixels[24, 24]);
        Assert.Equal(20 * 20, PixelsOtherThan(pixels, 0, 0, 28, 28, Background).Count);
    }

    /// <summary>
    ///     Test that images are placed in the cells in row-major order and that spacing separates them.
    /// </summary>
    [Fact]
    public void ContactSheet_Add_SeveralImages_FillCellsInRowMajorOrder()
    {
        // Arrange: three solid images in two columns (so the second row has one cell), with spacing 2 and padding 4
        using var sheet = new ContactSheet(3, Options(columns: 2));
        using var red = Solid(20, 20, new Rgba32(255, 0, 0, 255));
        using var green = Solid(20, 20, new Rgba32(0, 255, 0, 255));
        using var blue = Solid(20, 20, new Rgba32(0, 0, 255, 255));

        // Act
        var origins = new[] { sheet.Add(red, null), sheet.Add(green, null), sheet.Add(blue, null) };
        using var pixels = sheet.Detach();

        // Assert: cells at (4,4), (26,4), (4,26); the gap between the first two is the background
        Assert.Equal([(4, 4), (26, 4), (4, 26)], origins);
        Assert.Equal((50, 50), (pixels.Width, pixels.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), pixels[23, 23]);
        Assert.Equal(Background, pixels[24, 10]);
        Assert.Equal(Background, pixels[25, 10]);
        Assert.Equal(new Rgba32(0, 255, 0, 255), pixels[26, 4]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), pixels[4, 26]);
        Assert.Equal(Background, pixels[30, 30]);
    }

    /// <summary>
    ///     Test that transparent pixels of an image are flattened over the background of the sheet.
    /// </summary>
    [Fact]
    public void ContactSheet_Add_TransparentImage_IsFlattenedOverBackground()
    {
        // Arrange: a fully transparent image and a half-transparent one
        using var sheet = new ContactSheet(2, Options(columns: 2, padding: 0, spacing: 0, stretch: true));
        using var clear = Solid(20, 20, new Rgba32(255, 255, 255, 0));
        using var solid = Solid(20, 20, new Rgba32(255, 255, 255, 255));

        // Act
        sheet.Add(clear, null);
        sheet.Add(solid, null);
        using var pixels = sheet.Detach();

        // Assert: the transparent image shows the background, and every pixel of the sheet is opaque
        Assert.Equal(Background, pixels[5, 5]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), pixels[25, 5]);
        Assert.False(SurfaceOperations.HasTransparency(pixels));
    }

    /// <summary>
    ///     Test that labels draw text in the strip below the cell image and that nothing is drawn without labels.
    /// </summary>
    [Fact]
    public void ContactSheet_Add_Labels_DrawsTextInStripAndNothingWithoutLabels()
    {
        // Arrange: one cell with labels, and one without; both get the label text
        using var labeled = new ContactSheet(1, Options(columns: 1, cellWidth: 100, cellHeight: 40, labels: true));
        using var plain = new ContactSheet(1, Options(columns: 1, cellWidth: 100, cellHeight: 40));
        using var image = Solid(100, 40, Ink);

        // Act
        labeled.Add(image, "Hello");
        plain.Add(image, "Hello");
        using var withLabel = labeled.Detach();
        using var withoutLabel = plain.Detach();

        // Assert: the labeled sheet is 22 pixels taller; its strip (below the cell at y 4 + 40) has text in it and the
        // rest of the sheet outside the image has none; the plain sheet has only the image
        Assert.Equal(withoutLabel.Height + 22, withLabel.Height);
        var strip = PixelsOtherThan(withLabel, 4, 44, 100, 22, Background);
        Assert.NotEmpty(strip);
        Assert.Empty(PixelsOtherThan(withLabel, 0, 0, withLabel.Width, 4, Background));
        Assert.Empty(PixelsOtherThan(withLabel, 0, 66, withLabel.Width, 4, Background));
        Assert.Empty(PixelsOtherThan(withLabel, 0, 4, 4, 62, Background));
        Assert.Equal(100 * 40, PixelsOtherThan(withoutLabel, 0, 0, withoutLabel.Width, withoutLabel.Height, Background).Count);

        // A label that is null draws nothing either
        using var unlabeled = new ContactSheet(1, Options(columns: 1, cellWidth: 100, cellHeight: 40, labels: true));
        unlabeled.Add(image, null);
        using var noText = unlabeled.Detach();
        Assert.Empty(PixelsOtherThan(noText, 4, 44, 100, 22, Background));
    }

    /// <summary>
    ///     Test that a dark background gets light text.
    /// </summary>
    [Fact]
    public void ContactSheet_Add_DarkBackground_UsesLightText()
    {
        // Arrange: a black sheet
        var black = new Rgba32(0, 0, 0, 255);
        using var sheet = new ContactSheet(1, Options(columns: 1, cellWidth: 100, cellHeight: 20, labels: true, background: black));
        using var image = Solid(100, 20, black);

        // Act
        sheet.Add(image, "Light text");
        using var pixels = sheet.Detach();

        // Assert: the strip has pixels, none of them dark-on-black, and the brightest are white
        var strip = PixelsOtherThan(pixels, 4, 24, 100, 22, black);
        Assert.NotEmpty(strip);
        Assert.Contains(new Rgba32(255, 255, 255, 255), strip);
        Assert.All(strip, p => Assert.True(p.R == p.G && p.G == p.B && p.A == 255));
    }

    /// <summary>
    ///     Test that a light background gets dark text.
    /// </summary>
    [Fact]
    public void ContactSheet_Add_LightBackground_UsesDarkText()
    {
        // Arrange: a white sheet
        var white = new Rgba32(255, 255, 255, 255);
        using var sheet = new ContactSheet(1, Options(columns: 1, cellWidth: 100, cellHeight: 20, labels: true, background: white));
        using var image = Solid(100, 20, white);

        // Act
        sheet.Add(image, "Dark text");
        using var pixels = sheet.Detach();

        // Assert: the strip has pixels, the darkest are black, and none is brighter than the background
        var strip = PixelsOtherThan(pixels, 4, 24, 100, 22, white);
        Assert.NotEmpty(strip);
        Assert.Contains(new Rgba32(0, 0, 0, 255), strip);
        Assert.All(strip, p => Assert.True(p.R == p.G && p.G == p.B && p.A == 255));
    }

    /// <summary>
    ///     Test that a label that is too wide is cut to fit and ends with the ASCII text "...".
    /// </summary>
    [Fact]
    public void ContactSheet_FitLabel_LongText_IsTruncatedWithAsciiEllipsisToFit()
    {
        // Arrange: the font the labels use, and a long text
        var font = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);
        var text = "a-very-long-file-name-that-cannot-fit-in-a-small-cell.png";

        // Act
        var fitted = ContactSheet.FitLabel(text, 100, font, ContactSheet.LabelFontSize);
        var unchanged = ContactSheet.FitLabel("short.png", 100, font, ContactSheet.LabelFontSize);
        var tiny = ContactSheet.FitLabel(text, 1, font, ContactSheet.LabelFontSize);

        // Assert: it fits, starts with the beginning of the text, ends with ASCII dots, and one more character would not fit
        Assert.EndsWith("...", fitted);
        Assert.StartsWith(fitted[..^3], text, StringComparison.Ordinal);
        Assert.True(TextRenderer.MeasureText(fitted, font, ContactSheet.LabelFontSize).Width <= 100);
        var longer = text[..(fitted.Length - 2)] + "...";
        Assert.True(TextRenderer.MeasureText(longer, font, ContactSheet.LabelFontSize).Width > 100);
        Assert.Equal("short.png", unchanged);
        Assert.Equal("...", tiny);
        Assert.Throws<ArgumentNullException>(() => ContactSheet.FitLabel(null!, 10, font, 14));
        Assert.Throws<ArgumentNullException>(() => ContactSheet.FitLabel("x", 10, null!, 14));
    }

    /// <summary>
    ///     Test that adding more images than there are cells, or after the sheet was handed out, throws.
    /// </summary>
    [Fact]
    public void ContactSheet_Add_MoreImagesThanCells_Throws()
    {
        // Arrange: a sheet of one cell
        using var sheet = new ContactSheet(1, Options());
        using var image = Solid(20, 20, Ink);
        sheet.Add(image, null);

        // Act / Assert: no room for a second image
        Assert.Throws<InvalidOperationException>(() => sheet.Add(image, null));
        Assert.Throws<ArgumentNullException>(() => sheet.Add(null!, null));

        // The sheet can be handed out once, and nothing can be added after that
        using var pixels = sheet.Detach();
        Assert.Throws<InvalidOperationException>(() => sheet.Detach());
        Assert.Throws<InvalidOperationException>(() => sheet.Add(image, null));
    }

    /// <summary>
    ///     Test that invalid sheet options are rejected.
    /// </summary>
    [Fact]
    public void ContactSheet_Constructor_InvalidArguments_Throw()
    {
        // Arrange / Act / Assert: each invalid value is rejected
        Assert.Throws<ArgumentNullException>(() => new ContactSheet(1, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(0, Options()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(1, Options(columns: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(1, Options(columns: 65)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(1, Options(cellWidth: 15)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(1, Options(cellHeight: 15)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(1, Options(spacing: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(1, Options(padding: -1)));
        Assert.Throws<ArgumentException>(() => new ContactSheet(1, Options(background: new Rgba32(1, 2, 3, 254))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(1, Options() with { Fit = (FitMode)9 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSheet(1, Options() with { Filter = (ResamplingMode)99 }));
        Assert.Throws<ArgumentNullException>(() => ContactSheet.CalculateLayout(1, null!));

        // The smallest cell and the most columns are accepted
        Assert.Equal(16, ContactSheet.CalculateLayout(1, Options(cellWidth: 16, cellHeight: 16)).CellWidth);
        Assert.Equal(64, ContactSheet.CalculateLayout(64, Options(columns: 64, cellWidth: 16, spacing: 0, padding: 0)).Columns);
    }

    /// <summary>
    ///     Test that the same inputs give identical sheet pixels, with labels.
    /// </summary>
    [Fact]
    public void ContactSheet_Detach_SameInputsTwice_ProduceIdenticalPixels()
    {
        // Arrange: two images and the same options
        var options = Options(columns: 2, cellWidth: 60, cellHeight: 40, labels: true);
        using var first = FixtureFactory.CreateGradient(30, 20);
        using var second = FixtureFactory.CreatePalette(20, 30);

        // Act: compose the sheet twice
        Surface Compose()
        {
            using var sheet = new ContactSheet(2, options);
            sheet.Add(first, "gradient.png");
            sheet.Add(second, "a-very-long-palette-file-name.png");
            return sheet.Detach();
        }

        using var one = Compose();
        using var two = Compose();

        // Assert: identical pixels
        SurfaceAssert.Equal(one, two);
    }
}

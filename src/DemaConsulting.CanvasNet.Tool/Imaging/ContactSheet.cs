using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;
using DemaConsulting.CanvasNet.Rendering;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     The options of a contact sheet.
/// </summary>
/// <param name="Columns">The number of columns, 1 to <see cref="ContactSheet.MaxColumns"/>, or 0 for the ceiling of the square root of the number of images.</param>
/// <param name="CellWidth">The width in pixels of the image area of a cell, at least <see cref="ContactSheet.MinCell"/>.</param>
/// <param name="CellHeight">The height in pixels of the image area of a cell, at least <see cref="ContactSheet.MinCell"/>.</param>
/// <param name="Spacing">The space in pixels between cells (and between a label strip and the next row), 0 or more.</param>
/// <param name="Padding">The space in pixels around the sheet, 0 or more.</param>
/// <param name="Background">The opaque color of the sheet, of the margin of a contained image, and of the label strips.</param>
/// <param name="Labels"><see langword="true"/> to reserve a label strip below each cell image and draw the labels.</param>
/// <param name="Fit">How an image is fitted into its cell.</param>
/// <param name="Filter">The resampling filter of a fit.</param>
internal sealed record SheetOptions(
    int Columns,
    int CellWidth,
    int CellHeight,
    int Spacing,
    int Padding,
    Rgba32 Background,
    bool Labels,
    FitMode Fit,
    ResamplingMode Filter);

/// <summary>
///     The geometry of a contact sheet, derived from the number of images and the options.
/// </summary>
/// <param name="Count">The number of cells.</param>
/// <param name="Columns">The number of columns.</param>
/// <param name="Rows">The number of rows.</param>
/// <param name="CellWidth">The width of the image area of a cell.</param>
/// <param name="CellHeight">The height of the image area of a cell.</param>
/// <param name="LabelHeight">The height of the label strip below each cell image: 22, or 0 without labels.</param>
/// <param name="Spacing">The space between cells.</param>
/// <param name="Padding">The space around the sheet.</param>
/// <param name="Width">The width of the sheet in pixels.</param>
/// <param name="Height">The height of the sheet in pixels.</param>
internal readonly record struct SheetLayout(
    int Count,
    int Columns,
    int Rows,
    int CellWidth,
    int CellHeight,
    int LabelHeight,
    int Spacing,
    int Padding,
    int Width,
    int Height)
{
    /// <summary>
    ///     Gets the position of the top-left corner of the image area of a cell.
    /// </summary>
    /// <param name="index">The 0-based index of the cell, in row-major order.</param>
    /// <returns>
    ///     <c>(Padding + column * (CellWidth + Spacing), Padding + row * (CellHeight + LabelHeight + Spacing))</c>, where the
    ///     column is <c>index % Columns</c> and the row is <c>index / Columns</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not a cell of the sheet.</exception>
    public (int X, int Y) CellOrigin(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        var column = index % Columns;
        var row = index / Columns;
        return (Padding + (column * (CellWidth + Spacing)), Padding + (row * (CellHeight + LabelHeight + Spacing)));
    }
}

/// <summary>
///     A contact sheet: a grid of images, each fitted into a cell, optionally labeled, composed one at a time.
/// </summary>
/// <remarks>
///     <para>
///         The sheet is allocated and cleared to the background when the object is created; each call to
///         <see cref="Add"/> fits one image into its cell, copies it onto the sheet, and draws its label, so the memory
///         in use is the sheet plus the image being added. The caller loads an image, adds it, and disposes it before it
///         loads the next.
///     </para>
///     <para>
///         Labels are drawn with the Rendering text renderer and the bundled Liberation Sans font (never a system
///         font), so the same inputs give the same pixels on every machine.
///     </para>
/// </remarks>
internal sealed class ContactSheet : IDisposable
{
    /// <summary>The height in pixels of the label strip below a cell image.</summary>
    public const int LabelHeight = 22;

    /// <summary>The size in pixels of the label text.</summary>
    public const int LabelFontSize = 14;

    /// <summary>The smallest width or height in pixels of a cell image area.</summary>
    public const int MinCell = 16;

    /// <summary>The largest number of columns.</summary>
    public const int MaxColumns = 64;

    /// <summary>The text that replaces the end of a label that is too wide.</summary>
    private const string Ellipsis = "...";

    /// <summary>The geometry of the sheet.</summary>
    private readonly SheetLayout _layout;

    /// <summary>The options of the sheet.</summary>
    private readonly SheetOptions _options;

    /// <summary>The pixels of the sheet; <see langword="null"/> once handed to the caller.</summary>
    private Surface? _sheet;

    /// <summary>The number of images added so far.</summary>
    private int _added;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContactSheet"/> class and allocates the sheet.
    /// </summary>
    /// <param name="count">The number of images that will be added, at least 1.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the background is not opaque.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for the invalid values listed at <see cref="CalculateLayout"/>.</exception>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when the sheet would be larger than the maximum.</exception>
    public ContactSheet(int count, SheetOptions options)
    {
        _layout = CalculateLayout(count, options);
        _options = options;
        _sheet = new Surface(_layout.Width, _layout.Height);
        _sheet.Clear(options.Background);
    }

    /// <summary>
    ///     Gets the geometry of the sheet.
    /// </summary>
    public SheetLayout Layout => _layout;

    /// <summary>
    ///     Calculates the geometry of a sheet without allocating it.
    /// </summary>
    /// <param name="count">The number of images, at least 1.</param>
    /// <param name="options">The options.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the background is not opaque.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the count is less than 1, the columns are not 0 or 1 to 64, a cell side is below 16, the spacing or
    ///     padding is negative, or the fit mode or the filter is not defined.
    /// </exception>
    /// <exception cref="ToolException">Thrown with code <c>size-limit</c> when the sheet would be larger than the maximum on a side.</exception>
    /// <remarks>
    ///     With <c>rows = ceil(count / columns)</c> (the columns being the ceiling of the square root of the count when
    ///     <c>Columns</c> is 0), <c>width = 2 padding + columns cellWidth + (columns - 1) spacing</c> and
    ///     <c>height = 2 padding + rows (cellHeight + labelHeight) + (rows - 1) spacing</c>, the label height being 22 with
    ///     labels and 0 without.
    /// </remarks>
    public static SheetLayout CalculateLayout(int count, SheetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(options.Columns);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Columns, MaxColumns);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.CellWidth, MinCell);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.CellHeight, MinCell);
        ArgumentOutOfRangeException.ThrowIfNegative(options.Spacing);
        ArgumentOutOfRangeException.ThrowIfNegative(options.Padding);
        if (options.Background.A != 255)
        {
            throw new ArgumentException("The background must be opaque.", nameof(options));
        }

        if (!Enum.IsDefined(options.Fit))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Fit, "The fit mode is not defined.");
        }

        if (!Enum.IsDefined(options.Filter))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Filter, "The resampling filter is not defined.");
        }

        var columns = options.Columns == 0 ? AutomaticColumns(count) : options.Columns;
        var rows = (count + columns - 1) / columns;
        var labelHeight = options.Labels ? LabelHeight : 0;
        var width = (2L * options.Padding) + ((long)columns * options.CellWidth) + ((columns - 1L) * options.Spacing);
        var height = (2L * options.Padding) + ((long)rows * (options.CellHeight + labelHeight)) + ((rows - 1L) * options.Spacing);
        if (width > Surface.MaxDimension || height > Surface.MaxDimension)
        {
            throw new ToolException(
                ErrorCodes.SizeLimit,
                $"The contact sheet would be {width}x{height} pixels, which is larger than the maximum of {Surface.MaxDimension} pixels per side.",
                "Use a smaller --cell, more --columns, or less --spacing and --padding.");
        }

        return new SheetLayout(
            count,
            columns,
            rows,
            options.CellWidth,
            options.CellHeight,
            labelHeight,
            options.Spacing,
            options.Padding,
            (int)width,
            (int)height);
    }

    /// <summary>
    ///     Fits an image into the next cell, copies it onto the sheet, and draws its label.
    /// </summary>
    /// <param name="image">The image; it is not modified or disposed.</param>
    /// <param name="label">The label, or <see langword="null"/> for none; ignored when the sheet has no labels.</param>
    /// <returns>The position of the top-left corner of the cell's image area.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when every cell is already filled, or the sheet was handed out with <see cref="Detach"/>.</exception>
    /// <remarks>
    ///     The image is fitted with <see cref="SurfaceOperations.FitInto"/> (a contained image keeps its aspect ratio, is
    ///     scaled to fill the limiting side, and is centered; a stretched image fills the cell) and any transparency is
    ///     flattened over the background. The label is the text cut to the cell width with <see cref="FitLabel"/>, drawn
    ///     centered in the strip below the cell image in 14-pixel text, black on a light background and white on a dark one
    ///     (luma below 128).
    /// </remarks>
    public (int X, int Y) Add(Surface image, string? label)
    {
        ArgumentNullException.ThrowIfNull(image);
        var sheet = _sheet ?? throw new InvalidOperationException("The contact sheet was handed out with Detach.");
        if (_added >= _layout.Count)
        {
            throw new InvalidOperationException($"The contact sheet has room for {_layout.Count} images.");
        }

        var (x, y) = _layout.CellOrigin(_added);
        _added++;

        var fitted = SurfaceOperations.FitInto(
            image, _layout.CellWidth, _layout.CellHeight, _options.Fit, _options.Background, _options.Filter);
        try
        {
            if (SurfaceOperations.HasTransparency(fitted))
            {
                var flattened = SurfaceOperations.FlattenOver(fitted, _options.Background);
                fitted.Dispose();
                fitted = flattened;
            }

            for (var row = 0; row < fitted.Height; row++)
            {
                fitted.GetRowSpan(row).CopyTo(sheet.GetRowSpan(y + row)[x..]);
            }
        }
        finally
        {
            fitted.Dispose();
        }

        if (_options.Labels && label != null)
        {
            DrawLabel(sheet, x, y + _layout.CellHeight, label);
        }

        return (x, y);
    }

    /// <summary>
    ///     Hands the sheet to the caller.
    /// </summary>
    /// <returns>The sheet; the caller owns it and must dispose it. It can be taken once.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the sheet was already handed out.</exception>
    public Surface Detach()
    {
        var sheet = _sheet ?? throw new InvalidOperationException("The contact sheet was already handed out with Detach.");
        _sheet = null;
        return sheet;
    }

    /// <summary>
    ///     Cuts a label to a width.
    /// </summary>
    /// <param name="text">The label.</param>
    /// <param name="maxWidth">The width in pixels the text may take.</param>
    /// <param name="font">The font the label is drawn with.</param>
    /// <param name="size">The text size in pixels.</param>
    /// <returns>
    ///     The text itself when it fits; otherwise the longest beginning of the text (in whole characters) that, followed
    ///     by the ASCII text <c>...</c>, fits - or just <c>...</c> when no beginning does.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="font"/> is <see langword="null"/>.</exception>
    internal static string FitLabel(string text, int maxWidth, TrueTypeFont font, float size)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);

        if (TextRenderer.MeasureText(text, font, size).Width <= maxWidth)
        {
            return text;
        }

        var characters = text.EnumerateRunes().Select(rune => rune.ToString()).ToList();
        for (var length = characters.Count - 1; length > 0; length--)
        {
            var candidate = string.Concat(characters.Take(length)) + Ellipsis;
            if (TextRenderer.MeasureText(candidate, font, size).Width <= maxWidth)
            {
                return candidate;
            }
        }

        return Ellipsis;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sheet?.Dispose();
        _sheet = null;
    }

    /// <summary>
    ///     Finds the number of columns of a sheet that is as square as the cells allow.
    /// </summary>
    /// <param name="count">The number of images.</param>
    /// <returns>The ceiling of the square root of the count.</returns>
    private static int AutomaticColumns(int count)
    {
        var columns = 1;
        while (columns * columns < count)
        {
            columns++;
        }

        return columns;
    }

    /// <summary>
    ///     Draws a label centered in the strip below a cell image.
    /// </summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="x">The x position of the cell's left edge.</param>
    /// <param name="stripTop">The y position of the top of the label strip.</param>
    /// <param name="label">The label.</param>
    private void DrawLabel(Surface sheet, int x, int stripTop, string label)
    {
        var font = SystemFontCatalog.LoadBundledFallback(serif: false, fixedPitch: false, bold: false, italic: false);
        var text = FitLabel(label, _layout.CellWidth, font, LabelFontSize);
        var metrics = TextRenderer.MeasureText(text, font, LabelFontSize);
        var background = _options.Background;
        var luma = ((77 * background.R) + (150 * background.G) + (29 * background.B) + 128) >> 8;
        var color = luma < 128 ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255);
        var baseline = stripTop + ((LabelHeight - (metrics.Ascent + metrics.Descent)) / 2) + metrics.Ascent;
        new RenderCanvas(sheet).DrawText(text, x + (_layout.CellWidth / 2f), baseline, TextAlign.Center, font, LabelFontSize, color);
    }
}

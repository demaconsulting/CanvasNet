// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Description;

/// <summary>
///     The image of a component container (the large form of PlantUML's <c>USymbolComponent2</c>): a rectangle with the
///     component icon at its top right, its stereotypes 13 from the top, and its bold title below them, both centered.
/// </summary>
/// <remarks>
///     The header, the narrowest width, and the space around the members are a rectangle cluster's
///     (<see cref="SvekClusterImage"/>), as PlantUML's Smetana pipeline reserves them for every container. An image is
///     immutable and safe for concurrent use.
/// </remarks>
internal sealed class DescriptionComponentContainerImage : ISvekClusterImage
{
    /// <summary>The container.</summary>
    private readonly DescriptionElement _element;

    /// <summary>The laid-out title, or null.</summary>
    private readonly TextBlock? _title;

    /// <summary>The laid-out stereotypes, or null.</summary>
    private readonly TextBlock? _stereotype;

    /// <summary>The fill, or null.</summary>
    private readonly Rgba32? _fill;

    /// <summary>The stroke of the outline, or null.</summary>
    private readonly SceneStroke? _stroke;

    /// <summary>The corner radius.</summary>
    private readonly double _radius;

    /// <summary>The sizes of a rectangle cluster with the same texts.</summary>
    private readonly SvekClusterImage _sizes;

    /// <summary>
    ///     Initializes an image, laying out the title and the stereotypes.
    /// </summary>
    /// <param name="element">The container.</param>
    /// <param name="title">The title, or null.</param>
    /// <param name="fill">The fill, or null for none.</param>
    /// <param name="line">The line color, or null for the cluster style's.</param>
    /// <param name="theme">The look.</param>
    /// <param name="text">The text layout of the call.</param>
    /// <param name="sink">The report sink of the call, or null.</param>
    public DescriptionComponentContainerImage(DescriptionElement element, string? title, Rgba32? fill, Rgba32? line, SkinTheme theme, TextLayout text, PlantUmlReportSink? sink)
    {
        _element = element;
        var style = theme[SkinElement.Container];
        _title = title is null
            ? null
            : text.Layout(title, new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic), style.FontColor, 0, sink, element.Location);
        _stereotype = element.Stereotypes.Count == 0
            ? null
            : text.Layout(string.Join("\n", element.Stereotypes.Select(s => "\u00AB" + s + "\u00BB")), new SceneFont(style.FontFamily, style.FontSize, false, true), style.FontColor, 0, sink, element.Location);
        _fill = fill ?? style.Fill;
        var color = line ?? style.Line ?? SkinTheme.LineColor;
        _stroke = style.LineWidth > 0 ? new SceneStroke(color, style.LineWidth) : null;
        _radius = style.RoundCorner / 2;
        _sizes = new SvekClusterImage(ClusterSymbol.Rectangle, element.Id, _title, _stereotype, style, fill, line, element.Location);
    }

    /// <inheritdoc/>
    public double HeaderHeight => _sizes.HeaderHeight;

    /// <inheritdoc/>
    public double MinWidth => _sizes.MinWidth;

    /// <inheritdoc/>
    public double Padding => _sizes.Padding;

    /// <inheritdoc/>
    public IReadOnlyList<SceneItem> Draw(SceneRect box)
    {
        var (x, y, w, h) = (box.X, box.Y, box.Width, box.Height);
        var id = _element.Id;
        var location = _element.Location;
        SceneItem Shape(IReadOnlyList<SceneSegment> segments, bool filled) =>
            new ScenePath(segments) { Role = "cluster", Id = id, Location = location, Fill = filled ? _fill : null, Stroke = _stroke };

        var items = new List<SceneItem> { Shape(SceneShapes.Rect(x, y, w, h, _radius), filled: true) };
        DescriptionRenderer.ComponentIcon(items, x + w, y, (segments, _) => Shape(segments, filled: false));
        var stereoHeight = _stereotype?.Height ?? 0;
        if (_stereotype is not null)
        {
            items.AddRange(_stereotype.Draw(x + ((w - _stereotype.Width) / 2), y + 13, TextAlign.Center, "cluster-stereotype", location).Select(t => t with { Id = id }));
        }

        if (_title is not null)
        {
            items.AddRange(_title.Draw(x + ((w - _title.Width) / 2), y + 13 + stereoHeight, TextAlign.Center, "cluster-title", location).Select(t => t with { Id = id }));
        }

        return items;
    }
}

/// <summary>
///     The leaf symbols of <see cref="DescriptionRenderer"/>: node, folder, package, frame, cloud, database, and card.
/// </summary>
internal static partial class DescriptionRenderer
{
    /// <summary>
    ///     Builds the image of a node (<c>USymbolNode</c>): a box with a 3D top and right side 10 deep, margins of 15, 25,
    ///     20, and 10.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage NodeImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 40, p.TextHeight + 30);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SceneShapes.Polygon((x, y + 10), (x + 10, y), (x + w, y), (x + w, y + h - 10), (x + w - 10, y + h), (x, y + h)), "description"));
            items.Add(p.Path(SceneShapes.Line(x + w - 10, y + 10, x + w, y), "description-detail", filled: false));
            items.Add(p.Path(SceneShapes.Line(x, y + 10, x + w - 10, y + 10), "description-detail", filled: false));
            items.Add(p.Path(SceneShapes.Line(x + w - 10, y + 10, x + w - 10, y + h), "description-detail", filled: false));
            p.DrawTexts(items, x + 15, y + 20);
        });
    }

    /// <summary>
    ///     Builds the image of a folder or a package (<c>USymbolFolder</c>): a tab at the top left and margins of 10, 20,
    ///     13, and 10 around the tab's text and the texts. A folder's tab is empty (40 by 15) and its texts are its
    ///     stereotypes and name; a package shows its name in bold in the tab (with margins of 6) and, below, its
    ///     stereotypes and a display that differs from its name.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <param name="packageTitle">Whether the name is written in the tab (a package).</param>
    /// <returns>The image.</returns>
    private static DescriptionImage FolderImage(Painter p, bool packageTitle)
    {
        TextBlock? title = null;
        var (titleWidth, titleHeight) = (40.0, 15.0);
        if (packageTitle)
        {
            title = p.Bold;
            p.Label = p.Name.Text == title.Text ? TextBlock.Empty : p.Name;
            (titleWidth, titleHeight) = (title.Width + 12, title.Height);
        }

        var (w, h) = (Math.Max(titleWidth, p.TextWidth) + 30, titleHeight + p.TextHeight + 23);
        var (tabWidth, tabHeight) = (titleWidth + 6, titleHeight + 6);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SvekRenderer.FolderOutline(x, y, w, h, tabWidth, tabHeight, p.Radius * 2), "description"));
            items.Add(p.Path(SceneShapes.Line(x, y + tabHeight, x + tabWidth + 7, y + tabHeight), "description-detail", filled: false));
            if (title is not null)
            {
                items.AddRange(p.DrawText(title, x + 10, y + 3, TextAlign.Left, "description-name"));
            }

            p.DrawTexts(items, x + 10, y + 13 + titleHeight);
        });
    }

    /// <summary>
    ///     Builds the image of a frame (<c>USymbolFrame</c>): a rectangle with an empty title corner a third as wide and
    ///     12 high, margins of 15, 25, 20, and 10.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage FrameImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 40, p.TextHeight + 30);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SceneShapes.Rect(x, y, w, h, p.Radius), "description"));
            var corner = w / 3;
            items.Add(p.Path(SceneShapes.Polyline((x + corner, y), (x + corner, y + 5), (x + corner - 7, y + 12), (x, y + 12)), "description-detail", filled: false));
            p.DrawTexts(items, x + 15, y + 20);
        });
    }

    /// <summary>
    ///     Builds the image of a cloud (<c>USymbolCloud</c>): PlantUML's bubbly outline, margins of 15.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage CloudImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 30, p.TextHeight + 30);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SvekRenderer.CloudOutline(x, y, w, h), "description"));
            p.DrawTexts(items, x + 15, y + 15);
        });
    }

    /// <summary>
    ///     Builds the image of a database (<c>USymbolDatabase</c>): a cylinder with curved ends 10 high and the front
    ///     curve of its top, margins of 10, 10, 24, and 5.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage DatabaseImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 20, p.TextHeight + 29);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SvekRenderer.DatabaseOutline(x, y, w, h), "description"));
            items.Add(p.Path(
                [
                    SceneSegment.Move(x, y + 10),
                    SceneSegment.Cubic(x, y + 20, x + (w / 2), y + 20, x + (w / 2), y + 20),
                    SceneSegment.Cubic(x + (w / 2), y + 20, x + w, y + 20, x + w, y + 10),
                ],
                "description-detail",
                filled: false));
            p.DrawTexts(items, x + 10, y + 24);
        });
    }

    /// <summary>
    ///     Builds the image of a card (<c>USymbolCard</c>): a rectangle with margins of 10, 10, 3, and 3.
    /// </summary>
    /// <param name="p">The painter.</param>
    /// <returns>The image.</returns>
    private static DescriptionImage CardImage(Painter p)
    {
        var (w, h) = (p.TextWidth + 20, p.TextHeight + 6);
        return Painter.Box(w, h, (x, y, items) =>
        {
            items.Add(p.Path(SceneShapes.Rect(x, y, w, h, p.Radius), "description"));
            p.DrawTexts(items, x + 10, y + 3);
        });
    }
}

// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
// cspell:ignore scenegraph
using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     A built title: the title and subtitle text laid out inside their own group, and the rules to place that group
///     next to the data rectangle once the other guides are measured.
/// </summary>
/// <remarks>
///     <para>
///         The group is built at the origin: the title text is at (0, 0) and the subtitle below it (for the top and bottom
///         orients) or beside it (left and right). <see cref="Extent"/> is the bounding box of both in the coordinates of
///         the group, so <c>Extent.Width</c> and <c>Extent.Height</c> are the room the title needs. Placing it is a second
///         step because, with the <c>bounds</c> frame, the title is centered over everything the view contains, which is
///         only known after the axes are measured; <see cref="Place"/> is that step.
///     </para>
/// </remarks>
internal sealed class TitleBlock
{
    /// <summary>The children of the title group.</summary>
    private readonly IReadOnlyList<SceneItem> _children;

    /// <summary>
    ///     Initializes a title block.
    /// </summary>
    /// <param name="orient">The side of the data rectangle: <c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>.</param>
    /// <param name="anchor">The anchor along that side: <c>start</c>, <c>middle</c> or <c>end</c>.</param>
    /// <param name="frame">The frame the anchor refers to: <c>group</c> (the data rectangle) or <c>bounds</c> (everything the view contains).</param>
    /// <param name="offset">The distance between the title and the view's content, in pixels.</param>
    /// <param name="children">The title and subtitle text items.</param>
    /// <param name="extent">The bounding box of the children in the coordinates of the group.</param>
    public TitleBlock(string orient, string anchor, string frame, double offset, IReadOnlyList<SceneItem> children, SceneBounds extent)
    {
        Orient = orient;
        Anchor = anchor;
        Frame = frame;
        Offset = offset;
        _children = SceneLists.Copy(children);
        Extent = extent;
    }

    /// <summary>Gets the side of the data rectangle: <c>top</c>, <c>bottom</c>, <c>left</c> or <c>right</c>.</summary>
    public string Orient { get; }

    /// <summary>Gets the anchor along that side: <c>start</c>, <c>middle</c> or <c>end</c>.</summary>
    public string Anchor { get; }

    /// <summary>Gets the frame the anchor refers to: <c>group</c> or <c>bounds</c>.</summary>
    public string Frame { get; }

    /// <summary>Gets the distance between the title and the view's content, in pixels.</summary>
    public double Offset { get; }

    /// <summary>Gets the title and subtitle text items, in drawing order, in the coordinates of the title group.</summary>
    public IReadOnlyList<SceneItem> Children => _children;

    /// <summary>Gets the bounding box of the text in the coordinates of the title group.</summary>
    public SceneBounds Extent { get; }

    /// <summary>
    ///     Places the title next to the view (Vega's title layout).
    /// </summary>
    /// <remarks>
    ///     With the <c>group</c> frame the anchor refers to the data rectangle: <c>start</c>, <c>middle</c> and
    ///     <c>end</c> are its left edge, center and right edge for a top or bottom title (for a left title the bottom
    ///     edge, center and top edge, so that the rotated text reads upwards from its start). With the <c>bounds</c>
    ///     frame they refer to the bounding box of everything in the view instead. The title then sits
    ///     <see cref="Offset"/> pixels beyond the content on its side.
    /// </remarks>
    /// <param name="dataWidth">The width of the data rectangle.</param>
    /// <param name="dataHeight">The height of the data rectangle.</param>
    /// <param name="viewBounds">The bounding box of the data rectangle and the guides, in the coordinates of the view.</param>
    /// <returns>The title group in the coordinates of the view, and its bounding box there.</returns>
    public (SceneGroup Group, SceneBounds Bounds) Place(double dataWidth, double dataHeight, SceneBounds viewBounds)
    {
        var vertical = Orient is "left" or "right";
        double start = 0;
        var end = vertical ? dataHeight : dataWidth;
        if (!string.Equals(Frame, "group", StringComparison.Ordinal))
        {
            switch (Orient)
            {
                case "left":
                    start = viewBounds.Y2;
                    end = viewBounds.Y1;
                    break;
                case "right":
                    start = viewBounds.Y1;
                    end = viewBounds.Y2;
                    break;
                default:
                    start = viewBounds.X1;
                    end = viewBounds.X2;
                    break;
            }
        }
        else if (Orient == "left")
        {
            start = dataHeight;
            end = 0;
        }

        var position = Anchor switch
        {
            "start" => start,
            "end" => end,
            _ => (start + end) / 2,
        };

        var (x, y) = Orient switch
        {
            "left" => (viewBounds.X1 - Extent.Width - Offset, position),
            "right" => (viewBounds.X2 + Extent.Width + Offset, position),
            "bottom" => (position, viewBounds.Y2 + Offset),
            _ => (position, viewBounds.Y1 - Extent.Height - Offset),
        };

        var group = new SceneGroup("title", x, y) { Children = _children };
        return (group, Extent.Translate(x, y));
    }
}

/// <summary>
///     Builds the title and subtitle of a chart from the <c>title</c> of the specification and the configuration.
/// </summary>
/// <remarks>
///     <para>
///         <b>Input.</b> The specification's <c>title</c> is a string, an array of strings (one per line) or an object
///         with <c>text</c> and the properties <c>subtitle</c>, <c>anchor</c>, <c>orient</c>, <c>offset</c>, <c>frame</c>,
///         <c>limit</c>, <c>dx</c>, <c>dy</c>, <c>align</c>, <c>baseline</c>, <c>angle</c>, <c>font</c>, <c>fontSize</c>,
///         <c>fontStyle</c>, <c>fontWeight</c>, <c>color</c>, <c>lineHeight</c> and the <c>subtitle*</c> counterparts
///         (<c>subtitleFont</c>, <c>subtitleFontSize</c>, <c>subtitleFontStyle</c>, <c>subtitleFontWeight</c>,
///         <c>subtitleColor</c>, <c>subtitleLineHeight</c>, <c>subtitlePadding</c>). A property that the title does not give
///         comes from <c>config.title</c> and then from the <c>group-title</c> or <c>group-subtitle</c> style (13
///         pixel bold black sans-serif for the title, 12 pixel for the subtitle), so with no configuration at all the
///         title is centered above the plot, 4 pixels from it. Text in a string is also split at line feeds.
///     </para>
///     <para>
///         <b>Layout.</b> A title line is as tall as the font size, further lines are a line height (default font size
///         plus 2) apart; the subtitle starts below the title (or beside it for the left and right orients) by
///         <c>subtitlePadding</c> (default 3). A left title is rotated by -90 degrees and a right title by 90, and its
///         text is aligned by the anchor (<c>start</c> left, <c>middle</c> center, <c>end</c> right). The frame is
///         <c>group</c> for single and layered views whose anchor is <c>middle</c> or not set, as Vega-Lite assigns it,
///         and <c>bounds</c> otherwise. Everything is measured with the <see cref="TextMeasurer"/>.
///     </para>
///     <para>
///         Expressions in titles are not interpreted (the feature catalog reports them). The class is stateless and
///         safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class TitleBuilder
{
    /// <summary>The text measurer.</summary>
    private readonly TextMeasurer _measurer;

    /// <summary>
    ///     Initializes a title builder.
    /// </summary>
    /// <param name="measurer">The measurer that gives the size of the text.</param>
    public TitleBuilder(TextMeasurer measurer)
    {
        ArgumentNullException.ThrowIfNull(measurer);
        _measurer = measurer;
    }

    /// <summary>
    ///     Builds the title of a view.
    /// </summary>
    /// <param name="title">The <c>title</c> property of the specification (string, array or object), or null when there is none.</param>
    /// <param name="config">The effective configuration.</param>
    /// <param name="unitOrLayer"><see langword="true"/> for a single or layered view (the default); other views anchor their title at the start.</param>
    /// <returns>The title block, or null when there is no title text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
    public TitleBlock? Build(SpecNode? title, ResolvedConfig config, bool unitOrLayer = true)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (title is null || title.IsNull)
        {
            return null;
        }

        SpecNode? properties = title.IsObject ? title : null;
        var titleLines = ReadLines(properties is null ? title : properties["text"]);
        if (titleLines.Count == 0)
        {
            return null;
        }

        var subtitleLines = properties is null ? [] : ReadLines(properties["subtitle"]);

        // Layout properties: the title's own, else config.title, else the Vega defaults (orient top, anchor middle, offset 4)
        var orient = ReadString(properties, config, "orient") ?? "top";
        if (orient is not ("top" or "bottom" or "left" or "right"))
        {
            orient = "top";
        }

        var anchorText = ReadString(properties, config, "anchor");
        if (!unitOrLayer && properties?["anchor"]?.AsString() is null)
        {
            // Vega-Lite anchors the title of a composition at the start; only a single or layered view centers it
            anchorText = "start";
        }

        var anchor = anchorText is "start" or "end" ? anchorText : "middle";
        var frame = ReadString(properties, config, "frame") ?? (unitOrLayer && anchor == "middle" ? "group" : "bounds");
        if (frame is not ("group" or "bounds"))
        {
            frame = "bounds";
        }
        var offset = ReadNumber(properties, config, "offset") ?? 0;
        var padding = ReadNumber(properties, config, "subtitlePadding") ?? 0;
        var limit = ReadNumber(properties, config, "limit") ?? 0;
        var align = ReadString(properties, config, "align") ?? DefaultAlign(anchor);
        var angle = ReadNumber(properties, config, "angle") ?? DefaultAngle(orient);
        var baseline = ReadString(properties, config, "baseline") ?? "top";

        var titleText = CreateText("title-text", 0, 0, titleLines, properties, config, subtitle: false, align, baseline, angle, limit);
        var children = new List<SceneItem> { titleText };
        var extent = _measurer.GetBounds(titleText);

        if (subtitleLines.Count > 0)
        {
            // The subtitle sits after the title: below it, or beside it for the rotated orients
            var titleBounds = extent;
            var (sx, sy) = orient switch
            {
                "left" => (titleBounds.Width + padding, 0.0),
                "right" => (-titleBounds.Width - padding, 0.0),
                _ => (0.0, titleBounds.Height + padding),
            };
            var subtitleText = CreateText("title-subtitle", sx, sy, subtitleLines, properties, config, subtitle: true, align, baseline, angle, limit);
            children.Add(subtitleText);
            extent = _measurer.GetBounds(subtitleText).Union(extent);
        }

        return new TitleBlock(orient, anchor, frame, offset, children, extent);
    }

    /// <summary>
    ///     Gets the text alignment that an anchor implies: the title starts, centers or ends at its position.
    /// </summary>
    /// <param name="anchor">The anchor.</param>
    /// <returns><c>left</c>, <c>right</c> or <c>center</c>.</returns>
    private static string DefaultAlign(string anchor) => anchor switch
    {
        "start" => "left",
        "end" => "right",
        _ => "center",
    };

    /// <summary>
    ///     Gets the rotation that an orient implies: a left title reads upwards and a right title downwards.
    /// </summary>
    /// <param name="orient">The orient.</param>
    /// <returns>-90, 90 or 0 degrees.</returns>
    private static double DefaultAngle(string orient) => orient switch
    {
        "left" => -90,
        "right" => 90,
        _ => 0,
    };

    /// <summary>
    ///     Creates the text item of the title or the subtitle.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="x">The x position in the title group.</param>
    /// <param name="y">The y position in the title group.</param>
    /// <param name="lines">The lines.</param>
    /// <param name="properties">The title object of the specification, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="subtitle">Whether the item is the subtitle.</param>
    /// <param name="align">The horizontal alignment.</param>
    /// <param name="baseline">The baseline.</param>
    /// <param name="angle">The rotation in degrees.</param>
    /// <param name="limit">The width limit.</param>
    /// <returns>The text item.</returns>
    private static SceneText CreateText(
        string role,
        double x,
        double y,
        IReadOnlyList<string> lines,
        SpecNode? properties,
        ResolvedConfig config,
        bool subtitle,
        string align,
        string baseline,
        double angle,
        double limit)
    {
        string? Text(string property) => ReadText(properties, config, property, subtitle);
        double? Number(string property) => ReadNumber(properties, config, property, subtitle);
        return new SceneText(role, x, y, lines)
        {
            Fill = Text("fill"),
            FontFamily = Text("font"),
            FontSize = Number("fontSize") ?? (subtitle ? 12 : 13),
            FontStyle = Text("fontStyle"),
            FontWeight = Text("fontWeight"),
            LineHeight = Number("lineHeight"),
            Dx = ReadNumber(properties, config, "dx") ?? 0,
            Dy = ReadNumber(properties, config, "dy") ?? 0,
            Align = align,
            Baseline = baseline,
            Angle = angle,
            Limit = limit,
        };
    }

    /// <summary>
    ///     Reads the lines of a title or subtitle: a string (split at line feeds), or an array of strings.
    /// </summary>
    /// <param name="node">The node, or null.</param>
    /// <returns>The lines; none for a missing, null, empty or non-text value.</returns>
    private static List<string> ReadLines(SpecNode? node)
    {
        var lines = new List<string>();
        if (node is null)
        {
            return lines;
        }

        if (node.IsString)
        {
            lines.AddRange(TextMeasurer.SplitLines(node.StringValue));
        }
        else if (node.IsArray)
        {
            foreach (var item in node.Items)
            {
                if (item.IsString)
                {
                    lines.Add(item.StringValue ?? string.Empty);
                }
                else if (item.IsNumber)
                {
                    lines.Add(item.NumberValue.ToString("R", CultureInfo.InvariantCulture));
                }
            }
        }

        // A title that has only empty lines is no title
        return lines.TrueForAll(string.IsNullOrWhiteSpace) ? [] : lines;
    }

    /// <summary>
    ///     Gets the name under which the specification or the configuration spells a title property.
    /// </summary>
    /// <param name="property">The property as the text mark knows it (<c>fill</c>, <c>fontSize</c>, ...) or a layout property.</param>
    /// <param name="subtitle">Whether the property is the subtitle's.</param>
    /// <returns>The key.</returns>
    private static string Key(string property, bool subtitle)
    {
        if (!subtitle)
        {
            return property == "fill" ? "color" : property;
        }

        return property switch
        {
            "fill" => "subtitleColor",
            "font" => "subtitleFont",
            "fontSize" => "subtitleFontSize",
            "fontStyle" => "subtitleFontStyle",
            "fontWeight" => "subtitleFontWeight",
            "lineHeight" => "subtitleLineHeight",
            _ => property,
        };
    }

    /// <summary>
    ///     Reads a text property from the title object, else from the configuration chain.
    /// </summary>
    /// <param name="properties">The title object, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="property">The property.</param>
    /// <param name="subtitle">Whether the property is the subtitle's.</param>
    /// <returns>The value as text (numbers are formatted), or null when no level defines it.</returns>
    private static string? ReadText(SpecNode? properties, ResolvedConfig config, string property, bool subtitle = false)
    {
        var own = properties?[Key(property, subtitle)];
        if (own is not null)
        {
            return TextOf(own);
        }

        var node = config.ResolveTitleProperty(property, subtitle);
        return node is null
            ? null
            : node.AsString() ?? node.AsNumber()?.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Reads a string or a number of the specification as text.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The text; numbers are formatted culture invariant; null for anything else.</returns>
    private static string? TextOf(SpecNode node)
    {
        if (node.IsString)
        {
            return node.StringValue;
        }

        return node.IsNumber ? node.NumberValue.ToString("R", CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    ///     Reads a string property of the title layout.
    /// </summary>
    /// <param name="properties">The title object, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="property">The property.</param>
    /// <returns>The value, or null.</returns>
    private static string? ReadString(SpecNode? properties, ResolvedConfig config, string property) =>
        properties?[property]?.AsString() ?? config.ResolveTitleProperty(property)?.AsString();

    /// <summary>
    ///     Reads a number property.
    /// </summary>
    /// <param name="properties">The title object, or null.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="property">The property.</param>
    /// <param name="subtitle">Whether the property is the subtitle's.</param>
    /// <returns>The value, or null.</returns>
    private static double? ReadNumber(SpecNode? properties, ResolvedConfig config, string property, bool subtitle = false) =>
        properties?[Key(property, subtitle)]?.AsNumber() ?? config.ResolveTitleProperty(property, subtitle)?.AsNumber();
}

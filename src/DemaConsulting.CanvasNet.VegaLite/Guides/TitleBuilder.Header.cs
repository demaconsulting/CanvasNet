// Portions derived from Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Layout;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Guides;

/// <summary>
///     Which text of a facet header a block is: the label of a cell (its value) or the title of the channel (its field).
/// </summary>
internal enum HeaderPart
{
    /// <summary>The label of one row, column or cell: the <c>guide-label</c> style (10 pixel), configured by the <c>label*</c> header properties.</summary>
    Label,

    /// <summary>The title of the channel: the <c>guide-title</c> style (11 pixel, bold), configured by the <c>title*</c> header properties.</summary>
    Title,
}

/// <summary>
///     The header part of the title builder: the label and title blocks of the headers of a facet.
/// </summary>
/// <remarks>
///     <para>
///         <b>What Vega-Lite makes.</b> The header of a facet channel is a Vega title (<c>assembleLabelTitle</c>, <c>assembleTitleGroup</c> of <c>header/assemble.ts</c>) with the style
///         <c>guide-label</c> (the labels: <c>frame: group</c>, so the text is centered over the width of the cell) or <c>guide-title</c> (the title of the channel), whose properties
///         are the <c>label*</c> or <c>title*</c> properties of the header (<see cref="HeaderSettings"/>): <c>labelFontSize</c> becomes the font size, <c>labelPadding</c> the
///         <c>offset</c> of the title from the cells, <c>labelAngle</c> the angle, <c>labelAnchor</c> the anchor, <c>labelLimit</c> the limit and so on. The label of a row is
///         on the <c>left</c> (rotated by -90 degrees), that of a column on the <c>top</c>; <c>labelOrient</c> and <c>titleOrient</c> change the side. When the header gives an angle,
///         Vega-Lite also chooses the alignment and the baseline that fit the angle (<c>defaultHeaderGuideAlign</c> and <c>defaultHeaderGuideBaseline</c>, ported here), so a row label with
///         <c>labelAngle: 0</c> is right aligned and vertically centered at the edge of its row.
///     </para>
///     <para>
///         <b>Where the text comes from.</b> A property is looked up in the header, then in the header configuration of the channel and <c>config.header</c> (<see cref="HeaderSettings.Get"/>),
///         then in <c>config.title</c> (Vega's title configuration applies to these titles too, as in Vega) and last in the style. The facet header itself is a group with the size of a cell along
///         the headers and no size across them; the text is laid out against that group and the axes that the header holds (<see cref="BuildHeader"/>), as Vega's title layout does, by
///         the edge of the title's own bounds (<see cref="TitleBlock.PlacesByExtent"/>).
///     </para>
/// </remarks>
internal sealed partial class TitleBuilder
{
    /// <summary>
    ///     Builds the label or the title of a facet header as a title block, to be placed beside a group of a known size (<see cref="TitleBlock.Place"/>).
    /// </summary>
    /// <remarks>
    ///     This is the entry point for a label that is the title of a cell (the label of a wrapped facet is the title of the cell's group) and the building block of <see cref="BuildHeader"/>.
    /// </remarks>
    /// <param name="lines">The text, one entry per line; a text with nothing but empty lines draws nothing.</param>
    /// <param name="settings">The header properties of the channel.</param>
    /// <param name="part">Whether the text is a label or the title of the channel.</param>
    /// <param name="budget">The scene budget that is charged (the title group and its text) before they are created, or null for no limit.</param>
    /// <param name="location">The location of the field definition, for a budget failure.</param>
    /// <returns>The block; null when there is no text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> or <paramref name="settings"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, at <paramref name="location"/>, when the text would pass the scene item limit.</exception>
    public TitleBlock? BuildHeaderBlock(IReadOnlyList<string> lines, HeaderSettings settings, HeaderPart part, SceneBudget? budget = null, VegaLiteLocation location = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(settings);
        if (lines.Count == 0 || lines.All(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        var prefix = part == HeaderPart.Label ? "label" : "title";
        var style = part == HeaderPart.Label ? "guide-label" : "guide-title";
        var config = settings.Config;
        var titleConfig = config.Root["title"];

        // Layout: the side (a row is on the left, the others on top), the anchor, the angle and the distance from the cells
        var orient = (part == HeaderPart.Label ? settings.LabelOrient : settings.TitleOrient) ?? (settings.Channel == "row" ? "left" : "top");
        var headerChannel = part == HeaderPart.Label ? settings.LabelHeaderChannel : settings.TitleHeaderChannel;
        var anchorText = settings.Text(prefix + "Anchor") ?? titleConfig?["anchor"]?.AsString();
        var anchor = anchorText is "start" or "end" ? anchorText : "middle";
        var angleValue = settings.Number(prefix + "Angle");
        var defaultsAngle = angleValue;
        if (part == HeaderPart.Title && angleValue is { } raw)
        {
            // Vega-Lite normalizes the angle of a title into [0, 360) to pick the alignment and the baseline, and gives the title the angle as written
            defaultsAngle = ((raw % 360) + 360) % 360;
        }

        var offset = settings.Number(prefix + "Padding") ?? titleConfig?["offset"]?.AsNumber() ?? 0;
        var frame = "group";
        if (part == HeaderPart.Title)
        {
            frame = titleConfig?["frame"]?.AsString() == "group" ? "group" : "bounds";
        }

        // Vega-Lite's defaults for the alignment and the baseline that suit the angle, then the header's own
        var align = settings.Text(prefix + "Align") ?? DefaultHeaderAlign(headerChannel, defaultsAngle, anchor) ?? DefaultAlign(anchor);
        var baseline = settings.Text(prefix + "Baseline") ?? DefaultHeaderBaseline(defaultsAngle, headerChannel) ?? "top";
        var angle = angleValue ?? DefaultAngle(orient);

        string? TextOf(string name, string titleKey, string styleKey) =>
            settings.Text(prefix + name) ?? TextOfNode(titleConfig?[titleKey]) ?? TextOfNode(config.ResolveStyleProperty([style], styleKey));
        double? NumberOf(string name, string titleKey, string styleKey) =>
            settings.Number(prefix + name) ?? titleConfig?[titleKey]?.AsNumber() ?? config.ResolveStyleProperty([style], styleKey)?.AsNumber();

        budget?.Add(2, location);
        var text = new SceneText("title-text", 0, 0, [.. lines])
        {
            Fill = TextOf("Color", "color", "fill"),
            FontFamily = TextOf("Font", "font", "font"),
            FontSize = NumberOf("FontSize", "fontSize", "fontSize") ?? (part == HeaderPart.Label ? 10 : 11),
            FontStyle = TextOf("FontStyle", "fontStyle", "fontStyle"),
            FontWeight = TextOf("FontWeight", "fontWeight", "fontWeight"),
            LineHeight = NumberOf("LineHeight", "lineHeight", "lineHeight"),
            Align = align,
            Baseline = baseline,
            Angle = angle,
            Limit = NumberOf("Limit", "limit", "limit") ?? 0,
        };
        return new TitleBlock(orient, anchor, frame, offset, [text], _measurer.GetBounds(text)) { PlacesByExtent = true };
    }

    /// <summary>
    ///     Builds a header or a footer of a facet: a group with the label (or the title) of the channel and the shared axes that Vega-Lite moved into it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The group is built at the origin and has the size of the cell along the header (<paramref name="width"/> for a column header or footer, <paramref name="height"/> for a row
    ///         header or footer) and none across it, which is how Vega-Lite gives the header the size signal of the child. Its children are the axes (built
    ///         <see cref="AxisRequest.InHeader"/>, so they are in the coordinates of this group) and the label block, which Vega lays out against the group and the axes
    ///         (<see cref="TitleBlock.Place"/>: centered over the group for a label, <c>offset</c> beyond the axes). The bounds are those of the axes and the text; the group's own
    ///         rectangle is not part of them, as it is neither filled nor stroked. A label that would be empty leaves a group of axes, and nothing when there are no axes either.
    ///     </para>
    ///     <para>The scene budget is charged for the group, the title group and the text before they are created.</para>
    /// </remarks>
    /// <param name="role">The role of the group: <c>row-header</c>, <c>row-footer</c>, <c>column-header</c>, <c>column-footer</c>, <c>row-title</c> or <c>column-title</c>.</param>
    /// <param name="lines">The text, or none for a header that holds only axes.</param>
    /// <param name="settings">The header properties of the channel.</param>
    /// <param name="part">Whether the text is a label or the title of the channel.</param>
    /// <param name="width">The width of the group: the width of a cell for a column header, 0 for a row header and for a title.</param>
    /// <param name="height">The height of the group: the height of a cell for a row header, 0 for a column header and for a title.</param>
    /// <param name="axes">The axes the header holds, built <see cref="AxisRequest.InHeader"/>; null or empty for none.</param>
    /// <param name="budget">The scene budget, or null for no limit.</param>
    /// <param name="location">The location of the field definition, for a budget failure.</param>
    /// <returns>The group with its bounds in the coordinates of the group; null when there is neither text nor an axis.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="role"/>, <paramref name="lines"/> or <paramref name="settings"/> is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown, at <paramref name="location"/>, when the group would pass the scene item limit.</exception>
    public HeaderGroup? BuildHeader(
        string role,
        IReadOnlyList<string> lines,
        HeaderSettings settings,
        HeaderPart part,
        double width,
        double height,
        IReadOnlyList<AxisResult>? axes = null,
        SceneBudget? budget = null,
        VegaLiteLocation location = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(settings);
        var hasText = lines.Count > 0 && !lines.All(string.IsNullOrWhiteSpace);
        if (!hasText && (axes is null || axes.Count == 0))
        {
            return null;
        }

        budget?.Add(1, location);
        var children = new List<SceneItem>();
        var contents = SceneBounds.Empty;
        var view = new SceneBounds(0, 0, width, height);
        foreach (var axis in axes ?? [])
        {
            children.Add(axis.Group);
            contents = contents.Union(axis.Bounds);
            view = view.Union(axis.Bounds);
        }

        var block = BuildHeaderBlock(lines, settings, part, budget, location);
        if (block is not null)
        {
            var (group, bounds) = block.Place(width, height, view);
            children.Add(group);
            contents = contents.Union(bounds);
        }

        return new HeaderGroup(new SceneGroup(role, 0, 0) { Children = children, Width = width, Height = height }, contents);
    }

    /// <summary>
    ///     Gets the alignment that a text with an angle needs: Vega-Lite's <c>defaultHeaderGuideAlign</c> over <c>defaultLabelAlign</c>.
    /// </summary>
    /// <param name="headerChannel">The kind of header: <c>row</c> (text on the left) or <c>column</c> (text on top).</param>
    /// <param name="angle">The angle in degrees, or null for none given.</param>
    /// <param name="anchor">The anchor: <c>start</c> and <c>end</c> align the text left and right whatever the angle.</param>
    /// <returns>The alignment; null when Vega-Lite leaves it to the title's anchor.</returns>
    private static string? DefaultHeaderAlign(string headerChannel, double? angle, string anchor)
    {
        if (anchor == "start")
        {
            return "left";
        }

        if (anchor == "end")
        {
            return "right";
        }

        if (angle is not { } a)
        {
            return null;
        }

        // The channel x (a column header) has its main side at the bottom and the header is on top; the channel y has it on the left, like the header
        var isX = headerChannel != "row";
        var startAngle = isX ? 0 : 90;
        if ((a + startAngle) % 180 == 0)
        {
            return isX ? null : "center";
        }

        var orientIsMain = !isX;
        return (startAngle < a && a < 180 + startAngle) == orientIsMain ? "left" : "right";
    }

    /// <summary>
    ///     Gets the baseline that a text with an angle needs: Vega-Lite's <c>defaultHeaderGuideBaseline</c> over <c>defaultLabelBaseline</c> (with the middle included).
    /// </summary>
    /// <param name="angle">The angle in degrees, or null for none given.</param>
    /// <param name="headerChannel">The kind of header: <c>row</c> (text on the left) or <c>column</c> (text on top).</param>
    /// <returns>The baseline; null when Vega-Lite gives none.</returns>
    private static string? DefaultHeaderBaseline(double? angle, string headerChannel)
    {
        if (angle is not { } a)
        {
            return null;
        }

        if (headerChannel != "row")
        {
            if ((45 < a && a < 135) || (225 < a && a < 315))
            {
                return "middle";
            }

            // The orient is top: the text of an upright angle hangs from the bottom edge of its box
            return a <= 45 || 315 <= a ? "bottom" : "top";
        }

        if (a <= 45 || 315 <= a || (135 <= a && a <= 225))
        {
            return "middle";
        }

        return 45 <= a && a <= 135 ? "top" : "bottom";
    }

    /// <summary>
    ///     Reads a configuration value as text (a number is formatted culture invariant).
    /// </summary>
    /// <param name="node">The node, or null.</param>
    /// <returns>The text, or null for any other kind.</returns>
    private static string? TextOfNode(ConfigNode? node) => node?.Kind switch
    {
        ConfigNodeKind.String => node.StringValue,
        ConfigNodeKind.Number => node.NumberValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        _ => null,
    };
}

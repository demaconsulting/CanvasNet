// Portions derived from Vega-Lite (BSD-3-Clause): spec/base.ts (extractCompositionLayout), normalize/core.ts (normalizeFacetedUnit, getFacetMappingAndLayout), compile/resolve.ts and compile/concat.ts (resolve defaults, axes of a concatenation); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     How the cells of a concatenation or a facet are aligned in one direction (Vega's layout <c>align</c>).
/// </summary>
internal enum CompositionAlign
{
    /// <summary>All the rows (columns) have the size of the largest cell.</summary>
    All,

    /// <summary>Each row (column) has the size of its own largest cell.</summary>
    Each,

    /// <summary>The cells are not aligned.</summary>
    None,
}

/// <summary>
///     The layout of a concatenation or a facet: the <c>columns</c>, <c>spacing</c>, <c>align</c>, <c>center</c> and <c>bounds</c> that Vega-Lite's <c>extractCompositionLayout</c> reads.
/// </summary>
/// <param name="Columns">The number of columns the cells wrap at; null for one row (<c>hconcat</c>, and a facet whose cells follow its <c>column</c> field); a <c>vconcat</c> has one.</param>
/// <param name="SpacingColumn">The space between the columns (<c>spacing</c>, or <c>spacing.column</c>), in pixels.</param>
/// <param name="SpacingRow">The space between the rows (<c>spacing</c>, or <c>spacing.row</c>), in pixels.</param>
/// <param name="AlignColumn">How the columns are aligned (<c>align</c>, or <c>align.column</c>).</param>
/// <param name="AlignRow">How the rows are aligned (<c>align</c>, or <c>align.row</c>).</param>
/// <param name="CenterColumn">Whether the cells are centered in their column (<c>center</c>, or <c>center.column</c>).</param>
/// <param name="CenterRow">Whether the cells are centered in their row (<c>center</c>, or <c>center.row</c>).</param>
/// <param name="Flush">Whether the layout uses the width and height of the cells only (<c>bounds: "flush"</c>) instead of their full bounds including the axes and the legends.</param>
internal sealed record CompositionSettings(
    int? Columns,
    double SpacingColumn,
    double SpacingRow,
    CompositionAlign AlignColumn,
    CompositionAlign AlignRow,
    bool CenterColumn,
    bool CenterRow,
    bool Flush);

/// <summary>
///     What a facet is faceted by: a <c>row</c> and a <c>column</c> field, or one field whose values wrap into rows of cells.
/// </summary>
/// <param name="Row">The field definition of the rows, or null.</param>
/// <param name="Column">The field definition of the columns, or null.</param>
/// <param name="Wrap">The field definition of a wrapped facet (<c>facet</c>), or null; it is null when <paramref name="Row"/> or <paramref name="Column"/> is set.</param>
internal sealed record FacetDefinition(SpecNode? Row, SpecNode? Column, SpecNode? Wrap)
{
    /// <summary>Gets a value indicating whether the cells wrap (one field, <c>columns</c> per row) instead of following a <c>row</c> and a <c>column</c> field.</summary>
    public bool IsWrap => Wrap is not null;
}

/// <summary>
///     One cell of an instantiated facet: where it is in the grid and the values of the facet fields that its rows share.
/// </summary>
/// <param name="Index">The position of the cell in the order the cells are laid out: row-major for a <c>row</c> and <c>column</c> facet, the order of the values for a wrapped facet.</param>
/// <param name="RowIndex">The row of the cell in the grid of a <c>row</c> and <c>column</c> facet; zero when it has no row field; -1 for a wrapped facet.</param>
/// <param name="ColumnIndex">The column of the cell in the grid of a <c>row</c> and <c>column</c> facet; zero when it has no column field; -1 for a wrapped facet.</param>
/// <param name="RowValue">The value of the row field; null when the facet has none.</param>
/// <param name="ColumnValue">The value of the column field; null when the facet has none.</param>
/// <param name="WrapValue">The value of the field of a wrapped facet; null for a <c>row</c> and <c>column</c> facet.</param>
internal sealed record FacetCellInfo(int Index, int RowIndex, int ColumnIndex, DataValue RowValue, DataValue ColumnValue, DataValue WrapValue);

/// <content>
///     The concatenations and facets of the view tree: their layout, their facet fields, and the faceted unit view.
/// </content>
internal static partial class SpecNormalizer
{
    /// <summary>The default space between the cells of a concatenation or a facet (Vega-Lite's <c>DEFAULT_SPACING</c>).</summary>
    private const double DefaultSpacing = 20;

    /// <summary>
    ///     Makes a view a concatenation and queues its children.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    /// <param name="name">The operator: <c>concat</c>, <c>hconcat</c> or <c>vconcat</c>.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the property, when it is not an array.</exception>
    private static void VisitConcat(BuildState state, LayerNode node, string name)
    {
        var view = node.Source;
        if (view[name] is not { IsArray: true } items)
        {
            throw new VegaLiteSpecException("A view composition must be an array of views.", state.Tree.Locate(view[name]!));
        }

        var depth = EnterContainer(state, node, items, NestingMessage(state));

        // hconcat has one row and vconcat one column; the columns of a concat come from the specification or the configuration
        int? columns = name switch
        {
            "vconcat" => 1,
            "hconcat" => null,
            _ => ReadColumns(view["columns"], state.Config.GetNumber("concat.columns")),
        };
        FinishConcat(state, node, name, ReadLayout(state, view, null, "concat", CompositionAlign.Each, columns));
        AddChildren(state, node, items.Items.Where(i => i.IsObject).ToList(), depth, node.IsSynthetic);
    }

    /// <summary>
    ///     Makes a view a concatenation.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    /// <param name="operatorName">The operator: <c>concat</c>, <c>hconcat</c>, <c>vconcat</c> or <c>repeat</c>.</param>
    /// <param name="layout">The layout.</param>
    private static void FinishConcat(BuildState state, LayerNode node, string operatorName, CompositionSettings layout)
    {
        var resolve = node.Source["resolve"];
        node.MakeConcat(operatorName, LayerResolve.Read(resolve, ViewKind.Concat), layout);
        state.Compositions.Add(node);

        // The children of a concatenation cannot share an axis: Vega-Lite warns (CONCAT_CANNOT_SHARE_AXIS) and keeps the axes
        if (resolve?["axis"] is { IsObject: true } axes)
        {
            foreach (var entry in axes.Properties.Where(p => p.Name is "x" or "y" && p.StringValue == "shared"))
            {
                Warn(state, "vl-concat-axis-shared", $"The axes of the views of a concatenation cannot be shared; the '{entry.Name}' axes stay independent.", entry);
            }
        }
    }

    /// <summary>
    ///     Makes a view a facet over its <c>spec</c> and queues the <c>spec</c>.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    /// <exception cref="VegaLiteSpecException">Thrown, at the <c>facet</c>, when the view has no <c>spec</c>.</exception>
    private static void VisitFacet(BuildState state, LayerNode node)
    {
        var view = node.Source;
        var facet = view["facet"]!;
        if (view["spec"] is not { IsObject: true } spec)
        {
            throw new VegaLiteSpecException("A facet needs a 'spec' view.", state.Tree.Locate(facet));
        }

        var depth = EnterContainer(state, node, facet, NestingMessage(state));
        FacetDefinition definition;
        if (facet["field"] is not null)
        {
            definition = new FacetDefinition(null, null, ReplaceRepeaterInDefinition(state, node.Repeater, facet));
        }
        else
        {
            var row = facet["row"] is { IsObject: true } rowField ? ReplaceRepeaterInDefinition(state, node.Repeater, rowField) : null;
            var column = facet["column"] is { IsObject: true } columnField ? ReplaceRepeaterInDefinition(state, node.Repeater, columnField) : null;
            definition = new FacetDefinition(row, column, null);
        }

        if (definition.Row is null && definition.Column is null && definition.Wrap is null)
        {
            // Every field was a repeat reference without a value (reported at the reference): there is nothing to facet by, so the facet is dropped and the spec is the view, as a layer of one
            node.MakeLayer(LayerResolve.Default);
            node.SetChartRoot(ChartOf(node));
            AddChildren(state, node, [spec], depth, false);
            return;
        }

        var columns = FacetColumns(state, view, null, definition);
        node.MakeFacet("facet", LayerResolve.Read(view["resolve"], ViewKind.Facet), ReadLayout(state, view, null, "facet", CompositionAlign.All, columns), definition);
        state.Compositions.Add(node);
        AddChildren(state, node, [spec], depth, false);
    }

    /// <summary>
    ///     Determines whether a unit view is one that Vega-Lite facets when it has faceting channels: the root, or a child of a concatenation (a unit view in a layer or in the <c>spec</c> of a facet is not faceted).
    /// </summary>
    /// <param name="node">The view.</param>
    /// <returns><see langword="true"/> when a <c>row</c>, <c>column</c> or <c>facet</c> channel makes it a facet.</returns>
    private static bool IsFacetedUnit(LayerNode node) =>
        (node.Parent is null || node.Parent.Kind == ViewKind.Concat)
        && node.Source["encoding"] is { IsObject: true } encoding
        && FacetChannels.Any(channel => encoding[channel] is { IsObject: true });

    /// <summary>
    ///     Makes a unit view with faceting channels a facet over a synthetic chart that has the mark and the encoding without those channels (Vega-Lite's <c>normalizeFacetedUnit</c>).
    /// </summary>
    /// <remarks>
    ///     The <c>row</c> and <c>column</c> channels win over the <c>facet</c> channel. The <c>align</c>, <c>center</c>, <c>spacing</c> and <c>columns</c> of the <c>facet</c> channel are the
    ///     layout of the facet (the ones written on the view stay when they are not on the channel). When every field of the channels is a repeat reference that has no value the view is not faceted.
    /// </remarks>
    /// <param name="state">The state of the walk.</param>
    /// <param name="node">The view.</param>
    /// <returns><see langword="true"/> when the view was made a facet; <see langword="false"/> when it stays a unit view.</returns>
    private static bool VisitFacetedUnit(BuildState state, LayerNode node)
    {
        var view = node.Source;
        var encoding = view["encoding"]!;
        var hasMapping = encoding["row"] is { IsObject: true } || encoding["column"] is { IsObject: true };
        SpecNode? layoutField = null;
        FacetDefinition definition;
        if (hasMapping)
        {
            var row = encoding["row"] is { IsObject: true } rowField ? ReplaceRepeaterInDefinition(state, node.Repeater, rowField) : null;
            var column = encoding["column"] is { IsObject: true } columnField ? ReplaceRepeaterInDefinition(state, node.Repeater, columnField) : null;
            definition = new FacetDefinition(row, column, null);
        }
        else
        {
            layoutField = encoding["facet"]!;
            definition = new FacetDefinition(null, null, ReplaceRepeaterInDefinition(state, node.Repeater, layoutField));
        }

        if (definition.Row is null && definition.Column is null && definition.Wrap is null)
        {
            return false;
        }

        var location = layoutField ?? encoding[definition.Row is not null ? "row" : "column"] ?? view;
        var depth = EnterContainer(state, node, location, NestingMessage(state));
        var columns = FacetColumns(state, view, layoutField, definition);
        node.MakeFacet("facet", LayerResolve.Read(view["resolve"], ViewKind.Facet), ReadLayout(state, view, layoutField, "facet", CompositionAlign.All, columns), definition);
        state.Compositions.Add(node);

        var body = new LayerNode(view, node, depth);
        body.MakeFacetBody();
        node.AddChild(body);
        QueueChildren(state, node);
        return true;
    }

    /// <summary>
    ///     Reads the <c>columns</c> of a facet: only a wrapped facet has them (a <c>row</c> and <c>column</c> mapping has one cell per column value).
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <param name="view">The view object.</param>
    /// <param name="channelDefinition">The <c>facet</c> channel definition, whose <c>columns</c> win over the view's; null for the facet operator.</param>
    /// <param name="definition">What the facet is faceted by.</param>
    /// <returns>The columns; null when the facet is not wrapped.</returns>
    private static int? FacetColumns(BuildState state, SpecNode view, SpecNode? channelDefinition, FacetDefinition definition)
    {
        var written = channelDefinition?["columns"] ?? view["columns"];
        if (definition.IsWrap)
        {
            return ReadColumns(written, state.Config.GetNumber("facet.columns"));
        }

        if (written is not null)
        {
            Warn(state, "vl-composition-columns-ignored", "The 'columns' are ignored for a facet by 'row' and 'column': it has one cell for each value of the column field.", written);
        }

        return null;
    }

    /// <summary>
    ///     Gets the message of the error for a composition nested too deeply.
    /// </summary>
    /// <param name="state">The state of the walk.</param>
    /// <returns>The message.</returns>
    private static string NestingMessage(BuildState state) =>
        $"The views are nested more than {state.Limits.MaxCompositionDepth} levels deep, the limit (MaxCompositionDepth).";

    /// <summary>
    ///     Reads the layout of a concatenation or a facet (Vega-Lite's <c>extractCompositionLayout</c>).
    /// </summary>
    /// <remarks>
    ///     The configuration gives the <c>spacing</c> (20 unless <c>config.concat.spacing</c> or <c>config.facet.spacing</c> says otherwise); the view's own <c>spacing</c>, <c>align</c>,
    ///     <c>center</c> and <c>bounds</c> override it, a <c>spacing</c>, <c>align</c> or <c>center</c> that is an object giving the row and the column separately
    ///     (what the object leaves out is the default).
    /// </remarks>
    /// <param name="state">The state of the walk.</param>
    /// <param name="view">The view object, whose layout properties are read.</param>
    /// <param name="channelDefinition">The <c>facet</c> channel definition whose layout properties win over the view's, or null.</param>
    /// <param name="configKey">The configuration object of the kind: <c>concat</c> or <c>facet</c>.</param>
    /// <param name="defaultAlign">The alignment when the view has none: each for a concatenation, all for a facet and a repeat.</param>
    /// <param name="columns">The columns, decided by the caller.</param>
    /// <returns>The layout.</returns>
    private static CompositionSettings ReadLayout(BuildState state, SpecNode view, SpecNode? channelDefinition, string configKey, CompositionAlign defaultAlign, int? columns)
    {
        SpecNode? Written(string name) => channelDefinition?[name] ?? view[name];

        var fallback = state.Config.GetNumber(configKey + ".spacing") ?? DefaultSpacing;
        var (spacingRow, spacingColumn) = (fallback, fallback);
        if (Written("spacing") is { } spacing)
        {
            if (spacing.IsNumber)
            {
                (spacingRow, spacingColumn) = (spacing.NumberValue, spacing.NumberValue);
            }
            else if (spacing.IsObject)
            {
                spacingRow = spacing["row"]?.AsNumber() ?? fallback;
                spacingColumn = spacing["column"]?.AsNumber() ?? fallback;
            }
        }

        var (alignRow, alignColumn) = (defaultAlign, defaultAlign);
        if (Written("align") is { } align)
        {
            if (align.IsString)
            {
                (alignRow, alignColumn) = (ParseAlign(align.StringValue, defaultAlign), ParseAlign(align.StringValue, defaultAlign));
            }
            else if (align.IsObject)
            {
                alignRow = ParseAlign(align["row"]?.AsString(), defaultAlign);
                alignColumn = ParseAlign(align["column"]?.AsString(), defaultAlign);
            }
        }

        var (centerRow, centerColumn) = (false, false);
        if (Written("center") is { } center)
        {
            if (center.IsBoolean)
            {
                (centerRow, centerColumn) = (center.BoolValue, center.BoolValue);
            }
            else if (center.IsObject)
            {
                centerRow = center["row"]?.AsBool() ?? false;
                centerColumn = center["column"]?.AsBool() ?? false;
            }
        }

        return new CompositionSettings(
            columns,
            spacingColumn,
            spacingRow,
            alignColumn,
            alignRow,
            centerColumn,
            centerRow,
            Written("bounds")?.AsString() == "flush");
    }

    /// <summary>
    ///     Reads the number of columns of a concatenation or a wrapped facet.
    /// </summary>
    /// <param name="written">The <c>columns</c> of the view, or null.</param>
    /// <param name="configured">The <c>columns</c> of the configuration, or null.</param>
    /// <returns>The columns (never negative); null when neither says.</returns>
    private static int? ReadColumns(SpecNode? written, double? configured)
    {
        var value = written?.AsNumber() ?? configured;
        return value is { } number ? (int)Math.Clamp(Math.Truncate(number), 0, int.MaxValue) : null;
    }

    /// <summary>
    ///     Parses an alignment.
    /// </summary>
    /// <param name="text"><c>all</c>, <c>each</c> or <c>none</c>, or null.</param>
    /// <param name="fallback">The alignment for any other text.</param>
    /// <returns>The alignment.</returns>
    private static CompositionAlign ParseAlign(string? text, CompositionAlign fallback) => text switch
    {
        "all" => CompositionAlign.All,
        "each" => CompositionAlign.Each,
        "none" => CompositionAlign.None,
        _ => fallback,
    };
}
